using System;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Graphics.Common.Backends.Odyssey
{
    /// <summary>
    /// Odyssey engine graphics device implementation.
    /// Wraps the OpenGL context created by Kotor1/Kotor2GraphicsBackend.
    /// </summary>
    /// <remarks>
    /// Odyssey Graphics Device:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Original game graphics device: OpenGL context with WGL extensions
    /// - Graphics device operations: glClear, glViewport, glDrawArrays, glDrawElements
    /// - swkotor.exe: Graphics functions at 0x0044dab0 (init), 0x00427c90 (textures)
    /// - swkotor2.exe: Graphics functions at 0x00461c50 (init), 0x0042a100 (textures)
    /// - This implementation: Wraps OpenGL context for IGraphicsDevice interface
    /// </remarks>
    public class OdysseyGraphicsDevice : IGraphicsDevice
    {
        // OpenGL context handles from the backend
        private readonly IntPtr _glContext;
        private readonly IntPtr _glDevice;
        private readonly IntPtr _windowHandle;
        
        // Viewport and render state
        private Viewport _viewport;
        private IRenderTarget _currentRenderTarget;
        private IDepthStencilBuffer _depthStencilBuffer;
        
        // Backend reference for GL operations
        private readonly OdysseyGraphicsBackend _backend;
        
        #region OpenGL P/Invoke
        
        [DllImport("opengl32.dll", EntryPoint = "glClear")]
        private static extern void glClear(uint mask);
        
        [DllImport("opengl32.dll", EntryPoint = "glClearColor")]
        private static extern void glClearColor(float red, float green, float blue, float alpha);
        
        [DllImport("opengl32.dll", EntryPoint = "glClearDepth")]
        private static extern void glClearDepth(double depth);
        
        [DllImport("opengl32.dll", EntryPoint = "glClearStencil")]
        private static extern void glClearStencil(int s);
        
        [DllImport("opengl32.dll", EntryPoint = "glViewport")]
        private static extern void glViewport(int x, int y, int width, int height);
        
        [DllImport("opengl32.dll", EntryPoint = "glEnable")]
        private static extern void glEnable(uint cap);
        
        [DllImport("opengl32.dll", EntryPoint = "glDisable")]
        private static extern void glDisable(uint cap);
        
        [DllImport("opengl32.dll", EntryPoint = "glGenTextures")]
        private static extern void glGenTextures(int n, uint[] textures);
        
        [DllImport("opengl32.dll", EntryPoint = "glBindTexture")]
        private static extern void glBindTexture(uint target, uint texture);
        
        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D")]
        private static extern void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);
        
        [DllImport("opengl32.dll", EntryPoint = "glTexParameteri")]
        private static extern void glTexParameteri(uint target, uint pname, int param);
        
        [DllImport("opengl32.dll", EntryPoint = "glDeleteTextures")]
        private static extern void glDeleteTextures(int n, uint[] textures);
        
        [DllImport("opengl32.dll", EntryPoint = "glDrawArrays")]
        private static extern void glDrawArrays(uint mode, int first, int count);
        
        [DllImport("opengl32.dll", EntryPoint = "glDrawElements")]
        private static extern void glDrawElements(uint mode, int count, uint type, IntPtr indices);
        
        // Vertex Buffer Object (VBO) functions
        [DllImport("opengl32.dll", EntryPoint = "glGenBuffers")]
        private static extern void glGenBuffers(int n, uint[] buffers);
        
        [DllImport("opengl32.dll", EntryPoint = "glBindBuffer")]
        private static extern void glBindBuffer(uint target, uint buffer);
        
        [DllImport("opengl32.dll", EntryPoint = "glBufferData")]
        private static extern void glBufferData(uint target, int size, IntPtr data, uint usage);
        
        [DllImport("opengl32.dll", EntryPoint = "glBufferSubData")]
        private static extern void glBufferSubData(uint target, int offset, int size, IntPtr data);
        
        [DllImport("opengl32.dll", EntryPoint = "glGetBufferSubData")]
        private static extern void glGetBufferSubData(uint target, int offset, int size, IntPtr data);
        
        [DllImport("opengl32.dll", EntryPoint = "glDeleteBuffers")]
        private static extern void glDeleteBuffers(int n, uint[] buffers);
        
        // OpenGL constants
        private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
        private const uint GL_DEPTH_BUFFER_BIT = 0x00000100;
        private const uint GL_STENCIL_BUFFER_BIT = 0x00000400;
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_RGBA = 0x1908;
        private const uint GL_UNSIGNED_BYTE = 0x1401;
        private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
        private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
        private const uint GL_LINEAR = 0x2601;
        private const uint GL_TRIANGLES = 0x0004;
        private const uint GL_TRIANGLE_STRIP = 0x0005;
        private const uint GL_LINES = 0x0001;
        private const uint GL_LINE_STRIP = 0x0003;
        private const uint GL_POINTS = 0x0000;
        private const uint GL_DEPTH_TEST = 0x0B71;
        private const uint GL_STENCIL_TEST = 0x0B90;
        private const uint GL_BLEND = 0x0BE2;
        private const uint GL_CULL_FACE = 0x0B44;
        
        // Buffer object constants
        private const uint GL_ARRAY_BUFFER = 0x8892;
        private const uint GL_ELEMENT_ARRAY_BUFFER = 0x8893;
        private const uint GL_STATIC_DRAW = 0x88E4;
        private const uint GL_DYNAMIC_DRAW = 0x88E8;
        private const uint GL_STREAM_DRAW = 0x88E0;
        
        #endregion
        
        /// <summary>
        /// Creates a new Odyssey graphics device.
        /// </summary>
        /// <param name="backend">Parent backend that created the OpenGL context.</param>
        /// <param name="glContext">OpenGL rendering context (HGLRC).</param>
        /// <param name="glDevice">OpenGL device context (HDC).</param>
        /// <param name="windowHandle">Window handle (HWND).</param>
        /// <param name="width">Viewport width.</param>
        /// <param name="height">Viewport height.</param>
        public OdysseyGraphicsDevice(OdysseyGraphicsBackend backend, IntPtr glContext, IntPtr glDevice, IntPtr windowHandle, int width, int height)
        {
            _backend = backend;
            _glContext = glContext;
            _glDevice = glDevice;
            _windowHandle = windowHandle;
            _viewport = new Viewport(0, 0, width, height, 0.0f, 1.0f);
        }
        
        /// <summary>
        /// Gets the viewport dimensions.
        /// </summary>
        public Viewport Viewport => _viewport;
        
        /// <summary>
        /// Gets or sets the render target (null for backbuffer).
        /// </summary>
        public IRenderTarget RenderTarget
        {
            get { return _currentRenderTarget; }
            set { _currentRenderTarget = value; }
        }
        
        /// <summary>
        /// Gets or sets the depth-stencil buffer.
        /// </summary>
        public IDepthStencilBuffer DepthStencilBuffer
        {
            get { return _depthStencilBuffer; }
            set { _depthStencilBuffer = value; }
        }
        
        /// <summary>
        /// Gets the native graphics device handle (OpenGL context).
        /// </summary>
        public IntPtr NativeHandle => _glContext;
        
        /// <summary>
        /// Clears the render target with the specified color.
        /// Based on swkotor.exe/swkotor2.exe: glClear(GL_COLOR_BUFFER_BIT)
        /// </summary>
        /// <param name="color">Clear color.</param>
        public void Clear(Color color)
        {
            glClearColor(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            glClear(GL_COLOR_BUFFER_BIT);
        }
        
        /// <summary>
        /// Clears the depth buffer.
        /// Based on swkotor.exe/swkotor2.exe: glClear(GL_DEPTH_BUFFER_BIT)
        /// </summary>
        /// <param name="depth">Depth value (0.0 to 1.0).</param>
        public void ClearDepth(float depth)
        {
            glClearDepth(depth);
            glClear(GL_DEPTH_BUFFER_BIT);
        }
        
        /// <summary>
        /// Clears the stencil buffer.
        /// Based on swkotor.exe/swkotor2.exe: glClear(GL_STENCIL_BUFFER_BIT)
        /// </summary>
        /// <param name="stencil">Stencil value.</param>
        public void ClearStencil(int stencil)
        {
            glClearStencil(stencil);
            glClear(GL_STENCIL_BUFFER_BIT);
        }
        
        /// <summary>
        /// Creates a texture from pixel data.
        /// Based on swkotor.exe: FUN_00427c90 @ 0x00427c90 (texture initialization)
        /// Based on swkotor2.exe: FUN_0042a100 @ 0x0042a100 (texture initialization)
        /// </summary>
        /// <param name="width">Texture width.</param>
        /// <param name="height">Texture height.</param>
        /// <param name="data">Pixel data (RGBA format).</param>
        /// <returns>Created texture.</returns>
        public ITexture2D CreateTexture2D(int width, int height, byte[] data)
        {
            uint[] textureIds = new uint[1];
            glGenTextures(1, textureIds);
            uint textureId = textureIds[0];
            
            glBindTexture(GL_TEXTURE_2D, textureId);
            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
            
            // Upload texture data
            if (data != null && data.Length > 0)
            {
                IntPtr dataPtr = Marshal.AllocHGlobal(data.Length);
                try
                {
                    Marshal.Copy(data, 0, dataPtr, data.Length);
                    glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, dataPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(dataPtr);
                }
            }
            else
            {
                glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
            }
            
            return new OdysseyTexture2D(textureId, width, height);
        }
        
        /// <summary>
        /// Creates a render target.
        /// </summary>
        public IRenderTarget CreateRenderTarget(int width, int height, bool hasDepthStencil = true)
        {
            // TODO: STUB - Implement OpenGL framebuffer objects (FBO) for render targets
            // Original game uses glCopyTexImage2D for render-to-texture
            return new OdysseyRenderTarget(width, height);
        }
        
        /// <summary>
        /// Creates a depth-stencil buffer.
        /// </summary>
        public IDepthStencilBuffer CreateDepthStencilBuffer(int width, int height)
        {
            // TODO: STUB - Implement OpenGL depth-stencil buffer
            return new OdysseyDepthStencilBuffer(width, height);
        }
        
        /// <summary>
        /// Creates a vertex buffer.
        /// </summary>
        public IVertexBuffer CreateVertexBuffer<T>(T[] data) where T : struct
        {
            // TODO: STUB - Implement OpenGL vertex buffer objects (VBO)
            return new OdysseyVertexBuffer<T>(data);
        }
        
        /// <summary>
        /// Creates an index buffer.
        /// </summary>
        public IIndexBuffer CreateIndexBuffer(int[] indices, bool isShort = true)
        {
            // TODO: STUB - Implement OpenGL index buffer objects (IBO)
            return new OdysseyIndexBuffer(indices, isShort);
        }
        
        /// <summary>
        /// Creates a sprite batch for 2D rendering.
        /// </summary>
        public ISpriteBatch CreateSpriteBatch()
        {
            return new OdysseySpriteBatch(this);
        }
        
        /// <summary>
        /// Sets the vertex buffer for rendering.
        /// Based on swkotor.exe/swkotor2.exe: glBindBuffer(GL_ARRAY_BUFFER, vbo)
        /// </summary>
        public void SetVertexBuffer(IVertexBuffer vertexBuffer)
        {
            if (vertexBuffer == null)
            {
                // Unbind VBO
                glBindBuffer(GL_ARRAY_BUFFER, 0);
                return;
            }
            
            // Bind the VBO
            // Matching xoreos: glBindBuffer(GL_ARRAY_BUFFER, _vbo)
            // Matching PyKotor: glBindBuffer(GL_ARRAY_BUFFER, self._vbo)
            uint vboId = (uint)vertexBuffer.NativeHandle.ToInt32();
            glBindBuffer(GL_ARRAY_BUFFER, vboId);
        }
        
        /// <summary>
        /// Sets the index buffer for rendering.
        /// </summary>
        public void SetIndexBuffer(IIndexBuffer indexBuffer)
        {
            // TODO: STUB - Bind IBO for rendering
        }
        
        /// <summary>
        /// Draws indexed primitives.
        /// Based on swkotor.exe/swkotor2.exe: glDrawElements
        /// </summary>
        public void DrawIndexedPrimitives(PrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount)
        {
            uint mode = GetGLPrimitiveType(primitiveType);
            int indexCount = GetIndexCount(primitiveType, primitiveCount);
            // TODO: STUB - Implement proper indexed drawing with offset
            // glDrawElements(mode, indexCount, GL_UNSIGNED_INT, startIndexOffset);
        }
        
        /// <summary>
        /// Draws primitives.
        /// Based on swkotor.exe/swkotor2.exe: glDrawArrays
        /// </summary>
        public void DrawPrimitives(PrimitiveType primitiveType, int vertexOffset, int primitiveCount)
        {
            uint mode = GetGLPrimitiveType(primitiveType);
            int vertexCount = GetVertexCount(primitiveType, primitiveCount);
            glDrawArrays(mode, vertexOffset, vertexCount);
        }
        
        /// <summary>
        /// Sets the rasterizer state.
        /// </summary>
        public void SetRasterizerState(IRasterizerState rasterizerState)
        {
            if (rasterizerState != null)
            {
                // TODO: STUB - Apply rasterizer state (culling, fill mode, etc.)
                if (rasterizerState is OdysseyRasterizerState odysseyState)
                {
                    if (odysseyState.CullMode != CullMode.None)
                    {
                        glEnable(GL_CULL_FACE);
                    }
                    else
                    {
                        glDisable(GL_CULL_FACE);
                    }
                }
            }
        }
        
        /// <summary>
        /// Sets the depth-stencil state.
        /// </summary>
        public void SetDepthStencilState(IDepthStencilState depthStencilState)
        {
            if (depthStencilState != null)
            {
                if (depthStencilState is OdysseyDepthStencilState odysseyState)
                {
                    if (odysseyState.DepthEnabled)
                    {
                        glEnable(GL_DEPTH_TEST);
                    }
                    else
                    {
                        glDisable(GL_DEPTH_TEST);
                    }
                    
                    if (odysseyState.StencilEnabled)
                    {
                        glEnable(GL_STENCIL_TEST);
                    }
                    else
                    {
                        glDisable(GL_STENCIL_TEST);
                    }
                }
            }
        }
        
        /// <summary>
        /// Sets the blend state.
        /// </summary>
        public void SetBlendState(IBlendState blendState)
        {
            if (blendState != null)
            {
                if (blendState is OdysseyBlendState odysseyState)
                {
                    if (odysseyState.BlendEnabled)
                    {
                        glEnable(GL_BLEND);
                    }
                    else
                    {
                        glDisable(GL_BLEND);
                    }
                }
            }
        }
        
        /// <summary>
        /// Sets the sampler state for a texture slot.
        /// </summary>
        public void SetSamplerState(int index, ISamplerState samplerState)
        {
            // TODO: STUB - Apply sampler state (filtering, wrapping, etc.)
        }
        
        /// <summary>
        /// Creates a basic effect for simple 3D rendering.
        /// </summary>
        public IBasicEffect CreateBasicEffect()
        {
            return new OdysseyBasicEffect(this);
        }
        
        /// <summary>
        /// Creates a default rasterizer state.
        /// </summary>
        public IRasterizerState CreateRasterizerState()
        {
            return new OdysseyRasterizerState();
        }
        
        /// <summary>
        /// Creates a default depth-stencil state.
        /// </summary>
        public IDepthStencilState CreateDepthStencilState()
        {
            return new OdysseyDepthStencilState();
        }
        
        /// <summary>
        /// Creates a default blend state.
        /// </summary>
        public IBlendState CreateBlendState()
        {
            return new OdysseyBlendState();
        }
        
        /// <summary>
        /// Creates a default sampler state.
        /// </summary>
        public ISamplerState CreateSamplerState()
        {
            return new OdysseySamplerState();
        }
        
        /// <summary>
        /// Sets the viewport.
        /// Based on swkotor.exe/swkotor2.exe: glViewport
        /// </summary>
        public void SetViewport(int x, int y, int width, int height)
        {
            _viewport = new Viewport(x, y, width, height, 0.0f, 1.0f);
            glViewport(x, y, width, height);
        }
        
        /// <summary>
        /// Disposes the graphics device.
        /// </summary>
        public void Dispose()
        {
            // OpenGL context cleanup is handled by the backend
        }
        
        #region Helper Methods
        
        private uint GetGLPrimitiveType(PrimitiveType primitiveType)
        {
            switch (primitiveType)
            {
                case PrimitiveType.TriangleList:
                    return GL_TRIANGLES;
                case PrimitiveType.TriangleStrip:
                    return GL_TRIANGLE_STRIP;
                case PrimitiveType.LineList:
                    return GL_LINES;
                case PrimitiveType.LineStrip:
                    return GL_LINE_STRIP;
                case PrimitiveType.PointList:
                    return GL_POINTS;
                default:
                    return GL_TRIANGLES;
            }
        }
        
        private int GetIndexCount(PrimitiveType primitiveType, int primitiveCount)
        {
            switch (primitiveType)
            {
                case PrimitiveType.TriangleList:
                    return primitiveCount * 3;
                case PrimitiveType.TriangleStrip:
                    return primitiveCount + 2;
                case PrimitiveType.LineList:
                    return primitiveCount * 2;
                case PrimitiveType.LineStrip:
                    return primitiveCount + 1;
                case PrimitiveType.PointList:
                    return primitiveCount;
                default:
                    return primitiveCount;
            }
        }
        
        private int GetVertexCount(PrimitiveType primitiveType, int primitiveCount)
        {
            return GetIndexCount(primitiveType, primitiveCount);
        }
        
        #endregion
    }
}


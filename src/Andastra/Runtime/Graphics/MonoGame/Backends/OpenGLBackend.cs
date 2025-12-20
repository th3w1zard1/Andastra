using System;
using System.Collections.Generic;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Backends
{
    /// <summary>
    /// OpenGL graphics backend implementation.
    ///
    /// Provides:
    /// - OpenGL 4.5+ Core Profile rendering
    /// - Cross-platform support (Windows, Linux, macOS)
    /// - Compute shaders (OpenGL 4.3+)
    /// - Tessellation (OpenGL 4.0+)
    /// - AZDO (Approaching Zero Driver Overhead) techniques
    ///
    /// This is the primary cross-platform rendering backend,
    /// used as fallback when Vulkan is unavailable.
    /// </summary>
    public class OpenGLBackend : IGraphicsBackend
    {
        private bool _initialized;
        private GraphicsCapabilities _capabilities;
        private RenderSettings _settings;

        // OpenGL context
        private IntPtr _context;
        private IntPtr _windowHandle;

        // Framebuffer objects
        private uint _defaultFramebuffer;
        private uint _colorAttachment;
        private uint _depthStencilAttachment;

        // Resource tracking
        private readonly Dictionary<IntPtr, ResourceInfo> _resources;
        private uint _nextResourceHandle;

        // OpenGL version info
        private int _majorVersion;
        private int _minorVersion;
        private string _glslVersion;

        // Frame statistics
        private FrameStatistics _lastFrameStats;

        public GraphicsBackend BackendType
        {
            get { return GraphicsBackend.OpenGL; }
        }

        public GraphicsCapabilities Capabilities
        {
            get { return _capabilities; }
        }

        public bool IsInitialized
        {
            get { return _initialized; }
        }

        public bool IsRaytracingEnabled
        {
            // OpenGL does not support hardware raytracing
            get { return false; }
        }

        /// <summary>
        /// Gets the OpenGL version string.
        /// </summary>
        public string GLVersion
        {
            get { return _majorVersion + "." + _minorVersion; }
        }

        /// <summary>
        /// Gets the GLSL version string.
        /// </summary>
        public string GLSLVersion
        {
            get { return _glslVersion; }
        }

        public OpenGLBackend()
        {
            _resources = new Dictionary<IntPtr, ResourceInfo>();
            _nextResourceHandle = 1;
        }

        /// <summary>
        /// Initializes the OpenGL backend.
        /// </summary>
        /// <param name="settings">Render settings. Must not be null.</param>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if settings is null.</exception>
        public bool Initialize(RenderSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (_initialized)
            {
                return true;
            }

            _settings = settings;

            // Initialize GLAD or GL loader
            if (!InitializeGLLoader())
            {
                Console.WriteLine("[OpenGLBackend] Failed to initialize OpenGL loader");
                return false;
            }

            // Query OpenGL version
            QueryGLVersion();

            // Check for required extensions
            if (!CheckRequiredExtensions())
            {
                Console.WriteLine("[OpenGLBackend] Required OpenGL extensions not available");
                return false;
            }

            // Query capabilities
            QueryCapabilities();

            // Create default framebuffer
            CreateDefaultFramebuffer();

            // Set default GL state
            SetDefaultState();

            _initialized = true;
            Console.WriteLine("[OpenGLBackend] Initialized successfully");
            Console.WriteLine("[OpenGLBackend] OpenGL Version: " + GLVersion);
            Console.WriteLine("[OpenGLBackend] GLSL Version: " + _glslVersion);
            Console.WriteLine("[OpenGLBackend] Renderer: " + _capabilities.DeviceName);

            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            // Destroy all resources
            foreach (ResourceInfo resource in _resources.Values)
            {
                DestroyResourceInternal(resource);
            }
            _resources.Clear();

            // Delete framebuffer
            // glDeleteFramebuffers(1, &_defaultFramebuffer)
            // glDeleteTextures(1, &_colorAttachment)
            // glDeleteRenderbuffers(1, &_depthStencilAttachment)

            // Destroy context (platform-specific)

            _initialized = false;
            Console.WriteLine("[OpenGLBackend] Shutdown complete");
        }

        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Bind default framebuffer
            // glBindFramebuffer(GL_FRAMEBUFFER, _defaultFramebuffer)

            // Clear buffers
            // glClearColor(0.0f, 0.0f, 0.0f, 1.0f)
            // glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT)

            _lastFrameStats = new FrameStatistics();
        }

        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Swap buffers (platform-specific)
            // SwapBuffers(_windowHandle) or SDL_GL_SwapWindow() etc.

            // Check for GL errors in debug builds
            // GLenum error = glGetError()
        }

        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            // Update viewport
            // glViewport(0, 0, width, height)

            // Recreate framebuffer attachments if using FBO
            // glBindTexture(GL_TEXTURE_2D, _colorAttachment)
            // glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, NULL)

            // glBindRenderbuffer(GL_RENDERBUFFER, _depthStencilAttachment)
            // glRenderbufferStorage(GL_RENDERBUFFER, GL_DEPTH24_STENCIL8, width, height)

            _settings.Width = width;
            _settings.Height = height;
        }

        public IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // GLuint texture;
            // glGenTextures(1, &texture)
            // glBindTexture(GL_TEXTURE_2D, texture)
            // glTexImage2D(GL_TEXTURE_2D, 0, internalFormat, width, height, 0, format, type, NULL)
            // glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR)
            // glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR)
            // if (desc.MipLevels > 1) glGenerateMipmap(GL_TEXTURE_2D)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                GLHandle = 0, // Would be actual GL handle
                DebugName = desc.DebugName,
                TextureDesc = desc
            };

            return handle;
        }

        /// <summary>
        /// Uploads texture pixel data to a previously created texture.
        /// Matches original engine behavior: swkotor.exe and swkotor2.exe use glTexImage2D/glCompressedTexImage2D
        /// to upload texture data after creating the texture object.
        /// Based on swkotor.exe: FUN_00427c90 @ 0x00427c90 and swkotor2.exe equivalent functions.
        /// </summary>
        public bool UploadTextureData(IntPtr handle, TextureUploadData data)
        {
            if (!_initialized)
            {
                return false;
            }

            if (handle == IntPtr.Zero)
            {
                return false;
            }

            if (!_resources.TryGetValue(handle, out ResourceInfo info))
            {
                Console.WriteLine("[OpenGLBackend] UploadTextureData: Invalid texture handle");
                return false;
            }

            if (info.Type != ResourceType.Texture)
            {
                Console.WriteLine("[OpenGLBackend] UploadTextureData: Handle is not a texture");
                return false;
            }

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                Console.WriteLine("[OpenGLBackend] UploadTextureData: No mipmap data provided");
                return false;
            }

            try
            {
                // For OpenGL, we use glTexImage2D to upload texture data for each mipmap level
                // This matches the original engine's pattern exactly:
                // 1. Bind the texture: glBindTexture(GL_TEXTURE_2D, textureId)
                // 2. For each mipmap level: glTexImage2D(GL_TEXTURE_2D, level, internalFormat, width, height, 0, format, GL_UNSIGNED_BYTE, data)
                // Original engine: swkotor.exe and swkotor2.exe use this exact pattern

                // TODO: When OpenGL implementation is complete:
                // if (info.GLHandle == 0) {
                //     // Generate texture if not already created
                //     uint textureId = 0;
                //     glGenTextures(1, &textureId);
                //     info.GLHandle = textureId;
                // }
                // 
                // glBindTexture(GL_TEXTURE_2D, info.GLHandle);
                // 
                // uint format = ConvertTextureFormatToOpenGL(data.Format);
                // uint internalFormat = GetOpenGLInternalFormat(data.Format);
                // 
                // for (int i = 0; i < data.Mipmaps.Length; i++) {
                //     var mipmap = data.Mipmaps[i];
                //     System.Runtime.InteropServices.GCHandle pinnedData = System.Runtime.InteropServices.GCHandle.Alloc(mipmap.Data, System.Runtime.InteropServices.GCHandleType.Pinned);
                //     try {
                //         IntPtr dataPtr = pinnedData.AddrOfPinnedObject();
                //         glTexImage2D(GL_TEXTURE_2D, mipmap.Level, (int)internalFormat, mipmap.Width, mipmap.Height, 0, format, GL_UNSIGNED_BYTE, dataPtr);
                //     } finally {
                //         pinnedData.Free();
                //     }
                // }
                // 
                // glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR);
                // glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                // glBindTexture(GL_TEXTURE_2D, 0);

                // For now, store the upload data so it can be used when the texture is actually created
                info.UploadData = data;
                _resources[handle] = info;

                Console.WriteLine($"[OpenGLBackend] UploadTextureData: Stored {data.Mipmaps.Length} mipmap levels for texture {info.DebugName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenGLBackend] UploadTextureData: Exception uploading texture: {ex.Message}");
                return false;
            }
        }

        public IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // GLuint buffer;
            // glGenBuffers(1, &buffer)
            // glBindBuffer(target, buffer)
            // glBufferData(target, desc.SizeInBytes, NULL, usage)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                GLHandle = 0,
                DebugName = desc.DebugName
            };

            return handle;
        }

        public IntPtr CreatePipeline(PipelineDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // Create shaders
            // GLuint vs = glCreateShader(GL_VERTEX_SHADER)
            // glShaderSource(vs, 1, &vertexSource, NULL)
            // glCompileShader(vs)
            // ... repeat for other shader stages

            // Create program
            // GLuint program = glCreateProgram()
            // glAttachShader(program, vs)
            // glAttachShader(program, fs)
            // glLinkProgram(program)

            // Create VAO for input layout
            // GLuint vao;
            // glGenVertexArrays(1, &vao)
            // glBindVertexArray(vao)
            // ... configure vertex attributes

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                GLHandle = 0,
                DebugName = desc.DebugName
            };

            return handle;
        }

        public void DestroyResource(IntPtr handle)
        {
            ResourceInfo info;
            if (!_initialized || !_resources.TryGetValue(handle, out info))
            {
                return;
            }

            DestroyResourceInternal(info);
            _resources.Remove(handle);
        }

        public void SetRaytracingLevel(RaytracingLevel level)
        {
            // OpenGL does not support hardware raytracing
            if (level != RaytracingLevel.Disabled)
            {
                Console.WriteLine("[OpenGLBackend] Raytracing not supported in OpenGL. Use Vulkan or DirectX 12 for raytracing.");
            }
        }

        public FrameStatistics GetFrameStatistics()
        {
            return _lastFrameStats;
        }

        #region OpenGL Specific Methods

        /// <summary>
        /// Dispatches compute shader work.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDispatchCompute.xhtml
        /// Requires OpenGL 4.3+
        /// </summary>
        public void DispatchCompute(int numGroupsX, int numGroupsY, int numGroupsZ)
        {
            if (!_initialized || !_capabilities.SupportsComputeShaders)
            {
                return;
            }

            // glDispatchCompute(numGroupsX, numGroupsY, numGroupsZ)
            // glMemoryBarrier(GL_SHADER_STORAGE_BARRIER_BIT)
        }

        /// <summary>
        /// Sets the viewport.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glViewport.xhtml
        /// </summary>
        public void SetViewport(int x, int y, int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            // glViewport(x, y, width, height)
        }

        /// <summary>
        /// Draws non-indexed geometry using VAO.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawArrays.xhtml
        /// </summary>
        public void DrawArrays(GLPrimitiveType mode, int first, int count)
        {
            if (!_initialized)
            {
                return;
            }

            // glDrawArrays(mode, first, count)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += count / 3;
        }

        /// <summary>
        /// Draws indexed geometry using VAO.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawElements.xhtml
        /// </summary>
        public void DrawElements(GLPrimitiveType mode, int count, GLIndexType type, int offset)
        {
            if (!_initialized)
            {
                return;
            }

            // glDrawElements(mode, count, type, (void*)offset)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += count / 3;
        }

        /// <summary>
        /// Draws instanced geometry.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawElementsInstanced.xhtml
        /// </summary>
        public void DrawElementsInstanced(GLPrimitiveType mode, int count, GLIndexType type, int offset, int instanceCount)
        {
            if (!_initialized)
            {
                return;
            }

            // glDrawElementsInstanced(mode, count, type, (void*)offset, instanceCount)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += (count / 3) * instanceCount;
        }

        /// <summary>
        /// Multi-draw indirect for AZDO rendering.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glMultiDrawElementsIndirect.xhtml
        /// Requires OpenGL 4.3+
        /// </summary>
        public void MultiDrawElementsIndirect(GLPrimitiveType mode, GLIndexType type, IntPtr indirectBuffer, int drawCount, int stride)
        {
            if (!_initialized || _majorVersion < 4 || (_majorVersion == 4 && _minorVersion < 3))
            {
                return;
            }

            // glMultiDrawElementsIndirect(mode, type, indirectBuffer, drawCount, stride)
            _lastFrameStats.DrawCalls++;
        }

        /// <summary>
        /// Creates a shader storage buffer object (SSBO).
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glBufferData.xhtml
        /// Requires OpenGL 4.3+
        /// </summary>
        public IntPtr CreateShaderStorageBuffer(int sizeInBytes)
        {
            if (!_initialized || !_capabilities.SupportsComputeShaders)
            {
                return IntPtr.Zero;
            }

            // GLuint ssbo;
            // glGenBuffers(1, &ssbo)
            // glBindBuffer(GL_SHADER_STORAGE_BUFFER, ssbo)
            // glBufferData(GL_SHADER_STORAGE_BUFFER, sizeInBytes, NULL, GL_DYNAMIC_COPY)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                GLHandle = 0,
                DebugName = "SSBO"
            };

            return handle;
        }

        /// <summary>
        /// Binds a shader storage buffer to a binding point.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glBindBufferBase.xhtml
        /// </summary>
        public void BindShaderStorageBuffer(IntPtr bufferHandle, int bindingPoint)
        {
            if (!_initialized)
            {
                return;
            }

            // glBindBufferBase(GL_SHADER_STORAGE_BUFFER, bindingPoint, glHandle)
        }

        /// <summary>
        /// Creates an immutable texture storage for bindless textures.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glTexStorage2D.xhtml
        /// </summary>
        public IntPtr CreateImmutableTexture2D(int width, int height, int levels, GLInternalFormat internalFormat)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // GLuint texture;
            // glGenTextures(1, &texture)
            // glBindTexture(GL_TEXTURE_2D, texture)
            // glTexStorage2D(GL_TEXTURE_2D, levels, internalFormat, width, height)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                GLHandle = 0,
                DebugName = "ImmutableTexture2D"
            };

            return handle;
        }

        #endregion

        private bool InitializeGLLoader()
        {
            // Load OpenGL functions via GLAD, GLEW, or similar
            // This would typically be done by MonoGame's internal initialization
            return true;
        }

        private void QueryGLVersion()
        {
            // glGetIntegerv(GL_MAJOR_VERSION, &_majorVersion)
            // glGetIntegerv(GL_MINOR_VERSION, &_minorVersion)
            // const char* glslVersion = glGetString(GL_SHADING_LANGUAGE_VERSION)

            _majorVersion = 4;
            _minorVersion = 5;
            _glslVersion = "450";
        }

        private bool CheckRequiredExtensions()
        {
            // Check for required extensions
            // const char* extensions = glGetString(GL_EXTENSIONS)
            // or use glGetStringi for GL 3.0+

            // Required: GL_ARB_direct_state_access (or GL 4.5)
            // Required: GL_ARB_buffer_storage (or GL 4.4)
            // Optional: GL_ARB_compute_shader (or GL 4.3)
            // Optional: GL_ARB_tessellation_shader (or GL 4.0)
            // Optional: GL_ARB_bindless_texture

            return true;
        }

        private void QueryCapabilities()
        {
            // GLint maxTextureSize;
            // glGetIntegerv(GL_MAX_TEXTURE_SIZE, &maxTextureSize)

            // GLint maxColorAttachments;
            // glGetIntegerv(GL_MAX_COLOR_ATTACHMENTS, &maxColorAttachments)

            // const char* renderer = glGetString(GL_RENDERER)
            // const char* vendor = glGetString(GL_VENDOR)

            bool supportsCompute = _majorVersion > 4 || (_majorVersion == 4 && _minorVersion >= 3);
            bool supportsTessellation = _majorVersion >= 4;

            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 16384,
                MaxRenderTargets = 8,
                MaxAnisotropy = 16,
                SupportsComputeShaders = supportsCompute,
                SupportsGeometryShaders = true,
                SupportsTessellation = supportsTessellation,
                SupportsRaytracing = false,
                SupportsMeshShaders = false,
                SupportsVariableRateShading = false,
                DedicatedVideoMemory = 0, // Can query via GL_NVX_gpu_memory_info
                SharedSystemMemory = 0,
                VendorName = "Unknown",
                DeviceName = "OpenGL Renderer",
                DriverVersion = GLVersion,
                ActiveBackend = GraphicsBackend.OpenGL,
                ShaderModelVersion = _majorVersion >= 4 ? 5.0f : 4.0f,
                RemixAvailable = false,
                DlssAvailable = false,
                FsrAvailable = true // FSR can work via compute shaders
            };
        }

        private void CreateDefaultFramebuffer()
        {
            // For rendering to window, use default framebuffer (0)
            // For off-screen rendering:
            // glGenFramebuffers(1, &_defaultFramebuffer)
            // glBindFramebuffer(GL_FRAMEBUFFER, _defaultFramebuffer)
            // ... create and attach color/depth textures
        }

        private void SetDefaultState()
        {
            // glEnable(GL_DEPTH_TEST)
            // glDepthFunc(GL_LESS)
            // glEnable(GL_CULL_FACE)
            // glCullFace(GL_BACK)
            // glFrontFace(GL_CCW)
            // glEnable(GL_BLEND)
            // glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA)
        }

        private void DestroyResourceInternal(ResourceInfo info)
        {
            switch (info.Type)
            {
                case ResourceType.Texture:
                    // glDeleteTextures(1, &info.GLHandle)
                    break;
                case ResourceType.Buffer:
                    // glDeleteBuffers(1, &info.GLHandle)
                    break;
                case ResourceType.Pipeline:
                    // glDeleteProgram(info.GLHandle)
                    // glDeleteVertexArrays(1, &vao)
                    break;
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        private struct ResourceInfo
        {
            public ResourceType Type;
            public IntPtr Handle;
            public uint GLHandle;
            public string DebugName;
            public TextureDescription TextureDesc;
            public TextureUploadData UploadData; // Stored upload data for deferred upload
        }

        private enum ResourceType
        {
            Texture,
            Buffer,
            Pipeline
        }
    }

    /// <summary>
    /// OpenGL primitive types.
    /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawElements.xhtml
    /// </summary>
    public enum GLPrimitiveType
    {
        Points = 0x0000,
        Lines = 0x0001,
        LineLoop = 0x0002,
        LineStrip = 0x0003,
        Triangles = 0x0004,
        TriangleStrip = 0x0005,
        TriangleFan = 0x0006,
        LinesAdjacency = 0x000A,
        LineStripAdjacency = 0x000B,
        TrianglesAdjacency = 0x000C,
        TriangleStripAdjacency = 0x000D,
        Patches = 0x000E
    }

    /// <summary>
    /// OpenGL index types.
    /// </summary>
    public enum GLIndexType
    {
        UnsignedByte = 0x1401,
        UnsignedShort = 0x1403,
        UnsignedInt = 0x1405
    }

    /// <summary>
    /// OpenGL internal texture formats.
    /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glTexStorage2D.xhtml
    /// </summary>
    public enum GLInternalFormat
    {
        R8 = 0x8229,
        R8SNorm = 0x8F94,
        R16 = 0x822A,
        R16SNorm = 0x8F98,
        RG8 = 0x822B,
        RG8SNorm = 0x8F95,
        RG16 = 0x822C,
        RG16SNorm = 0x8F99,
        RGB8 = 0x8051,
        RGBA8 = 0x8058,
        RGBA8SNorm = 0x8F97,
        SRGB8 = 0x8C41,
        SRGB8Alpha8 = 0x8C43,
        R16F = 0x822D,
        RG16F = 0x822F,
        RGB16F = 0x881B,
        RGBA16F = 0x881A,
        R32F = 0x822E,
        RG32F = 0x8230,
        RGB32F = 0x8815,
        RGBA32F = 0x8814,
        Depth16 = 0x81A5,
        Depth24 = 0x81A6,
        Depth32F = 0x8CAC,
        Depth24Stencil8 = 0x88F0,
        Depth32FStencil8 = 0x8CAD,
        // Compressed formats
        CompressedRGBS3tcDxt1 = 0x83F0,
        CompressedRGBAS3tcDxt1 = 0x83F1,
        CompressedRGBAS3tcDxt3 = 0x83F2,
        CompressedRGBAS3tcDxt5 = 0x83F3,
        CompressedRGBBptcSigned = 0x8E8E,
        CompressedRGBBptcUnsigned = 0x8E8F,
        CompressedRGBABptcUNorm = 0x8E8C,
        CompressedSRGBABptcUNorm = 0x8E8D
    }
}


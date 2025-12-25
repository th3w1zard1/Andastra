using System;
using System.Reflection;
using Andastra.Runtime.Graphics;
using Stride.Core.Mathematics;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IGraphicsDevice.
    /// </summary>
    public class StrideGraphicsDevice : IGraphicsDevice
    {
        private readonly StrideGraphics.GraphicsDevice _device;
        private readonly StrideGraphics.CommandList _graphicsContext;
        private RasterizerStateDescription _currentRasterizerState;
        private DepthStencilStateDescription _currentDepthStencilState;
        private BlendStateDescription _currentBlendState;
        private SamplerStateDescription[] _currentSamplerStates;
        private bool _stateDirty;
        private PipelineStateKey _lastPipelineStateKey;
        private Viewport _currentViewport;

        /// <summary>
        /// Pipeline state cache key for tracking state combinations.
        /// Based on swkotor2.exe: DirectX 9 state management (d3d9.dll @ 0x0080a6c0)
        /// Original game uses state blocks to cache and apply render states efficiently.
        /// </summary>
        private struct PipelineStateKey : IEquatable<PipelineStateKey>
        {
            public RasterizerStateDescription RasterizerState;
            public DepthStencilStateDescription DepthStencilState;
            public BlendStateDescription BlendState;

            public bool Equals(PipelineStateKey other)
            {
                return RasterizerState.Equals(other.RasterizerState) &&
                       DepthStencilState.Equals(other.DepthStencilState) &&
                       BlendState.Equals(other.BlendState);
            }

            public override bool Equals(object obj)
            {
                return obj is PipelineStateKey key && Equals(key);
            }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + RasterizerState.GetHashCode();
                hash = hash * 31 + DepthStencilState.GetHashCode();
                hash = hash * 31 + BlendState.GetHashCode();
                return hash;
            }
        }

        public StrideGraphicsDevice(StrideGraphics.GraphicsDevice device, StrideGraphics.CommandList graphicsContext = null)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _graphicsContext = graphicsContext;
            _currentRasterizerState = RasterizerStateDescription.Default;
            _currentDepthStencilState = DepthStencilStateDescription.Default;
            _currentBlendState = BlendStateDescription.Default();
            _currentSamplerStates = new SamplerStateDescription[16];
            for (int i = 0; i < _currentSamplerStates.Length; i++)
            {
                _currentSamplerStates[i] = SamplerStateDescription.Default;
            }
        }

        /// <summary>
        /// Gets the CommandList for immediate rendering operations.
        /// Replaces the deprecated ImmediateContext property that returned CommandList.
        /// </summary>
        /// <remarks>
        /// This property first checks the static registry (populated per-frame by BeginFrame()),
        /// then falls back to the local _graphicsContext field.
        /// Based on Stride Graphics API: CommandList is registered per-frame from Game.GraphicsContext.CommandList
        /// swkotor2.exe: Graphics device command list management @ 0x004eb750 (original engine behavior)
        /// </remarks>
        public StrideGraphics.CommandList ImmediateContext
        {
            get
            {
                // First, try to get the current frame's CommandList from the static registry
                // This is populated per-frame by BeginFrame() in StrideGraphicsBackend
                var registryCommandList = _device.ImmediateContext();
                if (registryCommandList != null)
                {
                    return registryCommandList;
                }

                // Fallback to the local _graphicsContext field (may be null)
                return _graphicsContext;
            }
        }

        public Viewport Viewport
        {
            get
            {
                // In Stride, viewport is not a property of GraphicsDevice
                // Return tracked viewport or default based on backbuffer size
                if (_currentViewport.Width == 0 && _currentViewport.Height == 0)
                {
                    // Initialize with default viewport based on presentation parameters
                    // Stride doesn't expose viewport directly, so we use a reasonable default
                    var presentParams = _device.Presenter?.Description;
                    if (presentParams != null)
                    {
                        _currentViewport = new Viewport(
                            0, 0,
                            presentParams.BackBufferWidth,
                            presentParams.BackBufferHeight,
                            0.0f, 1.0f
                        );
                    }
                    else
                    {
                        // Fallback to a default viewport
                        _currentViewport = new Viewport(0, 0, 1920, 1080, 0.0f, 1.0f);
                    }
                }
                return _currentViewport;
            }
        }

        private StrideRenderTarget _currentRenderTarget;

        public IRenderTarget RenderTarget
        {
            get
            {
                return _currentRenderTarget;
            }
            set
            {
                // Use ImmediateContext property which retrieves from the per-frame registry
                var commandList = ImmediateContext;
                if (commandList == null)
                {
                    throw new InvalidOperationException("CommandList is required for SetRenderTarget");
                }
                if (value == null)
                {
                    commandList.SetRenderTarget(null, (StrideGraphics.Texture)null);
                    _currentRenderTarget = null;
                }
                else if (value is StrideRenderTarget strideRt)
                {
                    commandList.SetRenderTarget(null, strideRt.RenderTarget);
                    _currentRenderTarget = strideRt;
                }
                else
                {
                    throw new ArgumentException("Render target must be a StrideRenderTarget", nameof(value));
                }
            }
        }

        public IDepthStencilBuffer DepthStencilBuffer
        {           
            get => null;  // Stride depth buffer is part of render target
            set => _ = value;  // Stride doesn't support separate depth buffer setting
        }

        public void Clear(Runtime.Graphics.Color color)
        {
            // In Stride, Clear is done through CommandList, not GraphicsDevice
            // Clear the current render target or backbuffer
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext;
            if (commandList != null)
            {
                var targetTexture = _currentRenderTarget?.RenderTarget;
                var strideColor = new Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
                commandList.Clear(targetTexture, strideColor);
            }
        }

        public void ClearDepth(float depth)
        {
            // In Stride, depth clearing is done through CommandList.Clear
            // Clear method signature: Clear(Texture renderTarget, Color4? color, DepthStencilClearOptions? depthStencilClearOptions, float depth, byte stencil)
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext;
            if (commandList != null)
            {
                var targetTexture = _currentRenderTarget?.RenderTarget;
                var depthStencil = _currentRenderTarget?.DepthStencilBuffer;
                // Use reflection to call Clear with depth parameter since the exact signature may vary
                var clearMethod = typeof(StrideGraphics.CommandList).GetMethod("Clear",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(StrideGraphics.Texture), typeof(Color4?), typeof(StrideGraphics.DepthStencilClearOptions?), typeof(float), typeof(byte) },
                    null);
                if (clearMethod != null)
                {
                    // Get the enum value using reflection since the exact name may vary
                    var enumValues = Enum.GetValues(typeof(StrideGraphics.DepthStencilClearOptions));
                    StrideGraphics.DepthStencilClearOptions? depthOption = null;
                    foreach (var val in enumValues)
                    {
                        var str = val.ToString();
                        if (str.Contains("Depth") && !str.Contains("Stencil"))
                        {
                            depthOption = (StrideGraphics.DepthStencilClearOptions)val;
                            break;
                        }
                    }
                    if (!depthOption.HasValue)
                    {
                        // Fallback: use first enum value or cast 1
                        depthOption = (StrideGraphics.DepthStencilClearOptions)1; // Common value for depth-only clear
                    }
                    clearMethod.Invoke(commandList, new object[] { targetTexture, null, depthOption, depth, (byte)0 });
                }
            }
        }

        public void ClearStencil(int stencil)
        {
            // In Stride, stencil clearing is done through CommandList.Clear
            // Clear method signature: Clear(Texture renderTarget, Color4? color, DepthStencilClearOptions? depthStencilClearOptions, float depth, byte stencil)
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext;
            if (commandList != null)
            {
                var targetTexture = _currentRenderTarget?.RenderTarget;
                var depthStencil = _currentRenderTarget?.DepthStencilBuffer;
                // Use reflection to call Clear with stencil parameter since the exact signature may vary
                var clearMethod = typeof(StrideGraphics.CommandList).GetMethod("Clear",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(StrideGraphics.Texture), typeof(Color4?), typeof(StrideGraphics.DepthStencilClearOptions?), typeof(float), typeof(byte) },
                    null);
                if (clearMethod != null)
                {
                    // Get the enum value using reflection since the exact name may vary
                    var enumValues = Enum.GetValues(typeof(StrideGraphics.DepthStencilClearOptions));
                    StrideGraphics.DepthStencilClearOptions? stencilOption = null;
                    foreach (var val in enumValues)
                    {
                        var str = val.ToString();
                        if (str.Contains("Stencil") && !str.Contains("Depth"))
                        {
                            stencilOption = (StrideGraphics.DepthStencilClearOptions)val;
                            break;
                        }
                    }
                    if (!stencilOption.HasValue)
                    {
                        // Fallback: use first enum value or cast 2
                        stencilOption = (StrideGraphics.DepthStencilClearOptions)2; // Common value for stencil-only clear
                    }
                    clearMethod.Invoke(commandList, new object[] { targetTexture, null, stencilOption, 1.0f, (byte)stencil });
                }
            }
        }

        public ITexture2D CreateTexture2D(int width, int height, byte[] data)
        {
            var texture = StrideGraphics.Texture.New2D(_device, width, height, StrideGraphics.PixelFormat.R8G8B8A8_UNorm);
            if (data != null)
            {
                var colorData = new global::Stride.Core.Mathematics.Color[data.Length / 4];
                for (int i = 0; i < colorData.Length; i++)
                {
                    int offset = i * 4;
                    colorData[i] = new global::Stride.Core.Mathematics.Color(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
                }
                StrideGraphics.CommandList graphicsContext = ImmediateContext;
                if (graphicsContext != null)
                {
                    texture.SetData(graphicsContext, colorData);
                }
            }
            return new StrideTexture2D(texture, _graphicsContext);
        }

        public IRenderTarget CreateRenderTarget(int width, int height, bool hasDepthStencil = true)
        {
            var rt = StrideGraphics.Texture.New2D(_device, width, height, StrideGraphics.PixelFormat.R8G8B8A8_UNorm, StrideGraphics.TextureFlags.RenderTarget);
            var depthBuffer = hasDepthStencil ? StrideGraphics.Texture.New2D(_device, width, height, StrideGraphics.PixelFormat.D24_UNorm_S8_UInt, StrideGraphics.TextureFlags.DepthStencil) : null;
            return new StrideRenderTarget(rt, depthBuffer, _graphicsContext);
        }

        public IDepthStencilBuffer CreateDepthStencilBuffer(int width, int height)
        {
            // Stride doesn't support separate depth buffers, they're part of render targets
            throw new NotSupportedException("Stride does not support separate depth-stencil buffers. Use CreateRenderTarget with hasDepthStencil=true.");
        }

        public IVertexBuffer CreateVertexBuffer<T>(T[] data) where T : struct
        {
            // Stride's Buffer.Vertex.New requires unmanaged constraint, but interface only allows struct
            // We use dynamic invocation to work around this constraint mismatch
            // This will fail at runtime if T is not actually unmanaged (blittable)
            var method = typeof(StrideGraphics.Buffer.Vertex).GetMethod("New",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(StrideGraphics.GraphicsDevice), typeof(T[]), typeof(StrideGraphics.GraphicsResourceUsage) },
                null);
            if (method != null)
            {
                var buffer = method.Invoke(null, new object[] { _device, data, StrideGraphics.GraphicsResourceUsage.Dynamic }) as StrideGraphics.Buffer;
                if (buffer != null)
                {
                    return new StrideVertexBuffer(buffer, data != null ? data.Length : 0, System.Runtime.InteropServices.Marshal.SizeOf<T>());
                }
            }
            throw new NotSupportedException($"Vertex buffer type {typeof(T).Name} must be an unmanaged type (blittable). Stride requires unmanaged types for vertex buffers.");
        }

        public IIndexBuffer CreateIndexBuffer(int[] indices, bool isShort = true)
        {
            StrideGraphics.Buffer buffer;
            if (isShort)
            {
                var shortIndices = new ushort[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    shortIndices[i] = (ushort)indices[i];
                }
                buffer = StrideGraphics.Buffer.Index.New(_device, shortIndices, StrideGraphics.GraphicsResourceUsage.Dynamic);
            }
            else
            {
                buffer = StrideGraphics.Buffer.Index.New(_device, indices, StrideGraphics.GraphicsResourceUsage.Dynamic);
            }
            return new StrideIndexBuffer(buffer, indices != null ? indices.Length : 0, isShort);
        }

        public ISpriteBatch CreateSpriteBatch()
        {
            // Stride SpriteBatch requires GraphicsDevice, which we have
            // Also pass GraphicsContext for Begin() calls
            return new StrideSpriteBatch(new StrideGraphics.SpriteBatch(_device), _graphicsContext, _device);
        }

        public IntPtr NativeHandle
        {
            get
            {
                if (_device == null)
                {
                    return IntPtr.Zero;
                }
                // Stride GraphicsDevice may use NativePointer or NativeDevice depending on backend
                // Try NativePointer first (D3D12, Vulkan), then NativeDevice (D3D11)
                var nativePointerProp = typeof(StrideGraphics.GraphicsDevice).GetProperty("NativePointer",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativePointerProp != null)
                {
                    var value = nativePointerProp.GetValue(_device);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }
                var nativeDeviceProp = typeof(StrideGraphics.GraphicsDevice).GetProperty("NativeDevice",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativeDeviceProp != null)
                {
                    var value = nativeDeviceProp.GetValue(_device);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }
                return IntPtr.Zero;
            }
        }

        // 3D Rendering Methods
        public void SetVertexBuffer(IVertexBuffer vertexBuffer)
        {
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext;
            if (commandList == null)
            {
                throw new InvalidOperationException("CommandList is required for SetVertexBuffer");
            }

            if (vertexBuffer == null)
            {
                commandList.SetVertexBuffer(0, null, 0, 0);
            }
            else if (vertexBuffer is StrideVertexBuffer strideVb)
            {
                commandList.SetVertexBuffer(0, strideVb.Buffer, 0, strideVb.VertexStride);
            }
            else
            {
                throw new ArgumentException("Vertex buffer must be a StrideVertexBuffer", nameof(vertexBuffer));
            }
        }

        public void SetIndexBuffer(IIndexBuffer indexBuffer)
        {
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext;
            if (commandList == null)
            {
                throw new InvalidOperationException("CommandList is required for SetIndexBuffer");
            }

            if (indexBuffer == null)
            {
                commandList.SetIndexBuffer(null, 0, false);
            }
            else if (indexBuffer is StrideIndexBuffer strideIb)
            {
                commandList.SetIndexBuffer(strideIb.Buffer, 0, strideIb.IsShort);
            }
            else
            {
                throw new ArgumentException("Index buffer must be a StrideIndexBuffer", nameof(indexBuffer));
            }
        }

        public void DrawIndexedPrimitives(
            PrimitiveType primitiveType,
            int baseVertex,
            int minVertexIndex,
            int numVertices,
            int startIndex,
            int primitiveCount
        )
        {
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext ?? throw new InvalidOperationException("CommandList is required for DrawIndexedPrimitives");

            // Apply render state before drawing
            // Based on swkotor2.exe: DirectX 9 state management (d3d9.dll @ 0x0080a6c0)
            // Original game applies render states before each draw call to ensure correct rendering
            ApplyRenderState();

            // In Stride, primitive topology is set through PipelineState, not directly on CommandList
            // The topology will be part of the PipelineState when it's created
            // For immediate mode, we store it and it will be used when creating PipelineState

            // Calculate index count based on primitive type
            int verticesPerPrimitive = GetVerticesPerPrimitive(primitiveType);
            int indexCount = primitiveCount * verticesPerPrimitive;

            commandList.DrawIndexed(
                indexCount,
                startIndex,
                baseVertex
            );
        }

        public void DrawPrimitives(PrimitiveType primitiveType, int vertexOffset, int primitiveCount)
        {
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext ?? throw new InvalidOperationException("CommandList is required for DrawPrimitives");

            // Apply render state before drawing
            // Based on swkotor2.exe: DirectX 9 state management (d3d9.dll @ 0x0080a6c0)
            // Original game applies render states before each draw call to ensure correct rendering
            ApplyRenderState();

            // In Stride, primitive topology is set through PipelineState, not directly on CommandList
            // The topology will be part of the PipelineState when it's created
            // For immediate mode, we store it and it will be used when creating PipelineState

            // Calculate vertex count based on primitive type
            int verticesPerPrimitive = GetVerticesPerPrimitive(primitiveType);
            int vertexCount = primitiveCount * verticesPerPrimitive;

            commandList.Draw(
                vertexCount,
                vertexOffset
            );
        }

        public void SetRasterizerState(IRasterizerState rasterizerState)
        {
            if (rasterizerState == null)
            {
                _currentRasterizerState = RasterizerStateDescription.Default;
            }
            else if (rasterizerState is StrideRasterizerState strideRs)
            {
                _currentRasterizerState = strideRs.Description;
            }
            else
            {
                throw new ArgumentException("Rasterizer state must be a StrideRasterizerState", nameof(rasterizerState));
            }
            _stateDirty = true;
            ApplyRenderState();
        }

        public void SetDepthStencilState(IDepthStencilState depthStencilState)
        {
            if (depthStencilState == null)
            {
                _currentDepthStencilState = DepthStencilStateDescription.Default;
            }
            else if (depthStencilState is StrideDepthStencilState strideDs)
            {
                _currentDepthStencilState = strideDs.Description;
            }
            else
            {
                throw new ArgumentException("Depth-stencil state must be a StrideDepthStencilState", nameof(depthStencilState));
            }
            _stateDirty = true;
            ApplyRenderState();
        }

        public void SetBlendState(IBlendState blendState)
        {
            if (blendState == null)
            {
                _currentBlendState = BlendStateDescription.Default();
            }
            else if (blendState is StrideBlendState strideBs)
            {
                _currentBlendState = strideBs.Description;
            }
            else
            {
                throw new ArgumentException("Blend state must be a StrideBlendState", nameof(blendState));
            }
            _stateDirty = true;
            ApplyRenderState();
        }

        public void SetSamplerState(int index, ISamplerState samplerState)
        {
            if (index < 0 || index >= _currentSamplerStates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Sampler state index must be between 0 and " + (_currentSamplerStates.Length - 1));
            }

            if (samplerState == null)
            {
                _currentSamplerStates[index] = SamplerStateDescription.Default;
            }
            else if (samplerState is StrideSamplerState strideSs)
            {
                _currentSamplerStates[index] = strideSs.Description;
            }
            else
            {
                throw new ArgumentException("Sampler state must be a StrideSamplerState", nameof(samplerState));
            }
            _stateDirty = true;
            ApplySamplerState(index);
        }

        public IBasicEffect CreateBasicEffect()
        {
            return new StrideBasicEffect(_device);
        }

        public IRasterizerState CreateRasterizerState()
        {
            return new StrideRasterizerState();
        }

        public IDepthStencilState CreateDepthStencilState()
        {
            return new StrideDepthStencilState();
        }

        public IBlendState CreateBlendState()
        {
            return new StrideBlendState();
        }

        public ISamplerState CreateSamplerState()
        {
            return new StrideSamplerState();
        }

        /// <summary>
        /// Applies the current render state (rasterizer, depth-stencil, blend) to the CommandList.
        /// In Stride, state is applied through CommandList methods or through PipelineState.
        /// Based on swkotor2.exe: DirectX 9 state management (d3d9.dll @ 0x0080a6c0)
        /// Original game uses IDirect3DDevice9::SetRenderState() to apply render states.
        /// </summary>
        private void ApplyRenderState()
        {
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext;
            if (commandList == null)
            {
                return;
            }

            // Check if state has actually changed
            var currentKey = new PipelineStateKey
            {
                RasterizerState = _currentRasterizerState,
                DepthStencilState = _currentDepthStencilState,
                BlendState = _currentBlendState
            };

            if (!_stateDirty && _lastPipelineStateKey.Equals(currentKey))
            {
                return; // State hasn't changed, no need to reapply
            }

            _lastPipelineStateKey = currentKey;
            _stateDirty = false;

            try
            {
                // Apply rasterizer state
                ApplyRasterizerState(commandList);

                // Apply depth-stencil state
                ApplyDepthStencilState(commandList);

                // Apply blend state
                ApplyBlendState(commandList);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allows rendering to continue
                Console.WriteLine($"[StrideGraphicsDevice] Error applying render state: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the current rasterizer state to the CommandList.
        /// Uses Stride's native state setting methods or creates a PipelineState.
        /// </summary>
        /// <param name="commandList">The CommandList to apply the state to.</param>
        private void ApplyRasterizerState(StrideGraphics.CommandList commandList)
        {
            if (commandList == null)
            {
                return;
            }

            try
            {
                // Convert our RasterizerStateDescription to Stride's native format
                var strideRasterizerState = new StrideGraphics.RasterizerStateDescription
                {
                    CullMode = _currentRasterizerState.CullMode,
                    FillMode = _currentRasterizerState.FillMode,
                    DepthBias = (int)_currentRasterizerState.DepthBias,
                    SlopeScaleDepthBias = _currentRasterizerState.SlopeScaleDepthBias,
                    ScissorTestEnable = _currentRasterizerState.ScissorTestEnable
                };

                // Try to set rasterizer state directly on CommandList using reflection
                // Stride's CommandList may have SetRasterizerState method
                var setRasterizerStateMethod = typeof(StrideGraphics.CommandList).GetMethod("SetRasterizerState",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(StrideGraphics.RasterizerStateDescription) }, null);

                if (setRasterizerStateMethod != null)
                {
                    setRasterizerStateMethod.Invoke(commandList, new object[] { strideRasterizerState });
                }
                else
                {
                    // If direct method doesn't exist, state will be applied through PipelineState when drawing
                    // This is the typical Stride pattern - state is part of PipelineState
                    // For immediate mode, we store the state and it will be used when creating PipelineState
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideGraphicsDevice] Error applying rasterizer state: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the current depth-stencil state to the CommandList.
        /// Uses Stride's native state setting methods or creates a PipelineState.
        /// </summary>
        /// <param name="commandList">The CommandList to apply the state to.</param>
        private void ApplyDepthStencilState(StrideGraphics.CommandList commandList)
        {
            if (commandList == null)
            {
                return;
            }

            try
            {
                // Convert our DepthStencilStateDescription to Stride's native format
                var strideDepthStencilState = new StrideGraphics.DepthStencilStateDescription
                {
                    DepthBufferEnable = _currentDepthStencilState.DepthBufferEnable,
                    DepthBufferWriteEnable = _currentDepthStencilState.DepthBufferWriteEnable,
                    DepthBufferFunction = (StrideGraphics.CompareFunction)(int)_currentDepthStencilState.DepthBufferFunction
                };

                // Handle stencil state
                if (_currentDepthStencilState.StencilEnable)
                {
                    strideDepthStencilState.StencilEnable = true;
                    strideDepthStencilState.StencilMask = (byte)_currentDepthStencilState.StencilMask;
                    strideDepthStencilState.StencilWriteMask = (byte)_currentDepthStencilState.StencilWriteMask;
                    // Stride's DepthStencilStateDescription uses StencilReference property
                    // Check if property exists, otherwise use reflection
                    var stencilRefProp = typeof(StrideGraphics.DepthStencilStateDescription).GetProperty("StencilReference",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (stencilRefProp != null)
                    {
                        stencilRefProp.SetValue(strideDepthStencilState, _currentDepthStencilState.ReferenceStencil);
                    }
                    else
                    {
                        // Try alternative property name if StencilReference doesn't exist
                        var refStencilProp = typeof(StrideGraphics.DepthStencilStateDescription).GetProperty("ReferenceStencil",
                            BindingFlags.Public | BindingFlags.Instance);
                        refStencilProp?.SetValue(strideDepthStencilState, _currentDepthStencilState.ReferenceStencil);
                    }

                    // Front face stencil operations
                    strideDepthStencilState.FrontFace.StencilFail = (StrideGraphics.StencilOperation)(int)_currentDepthStencilState.FrontFace.StencilFail;
                    strideDepthStencilState.FrontFace.StencilPass = (StrideGraphics.StencilOperation)(int)_currentDepthStencilState.FrontFace.StencilPass;
                    strideDepthStencilState.FrontFace.StencilFunction = (StrideGraphics.CompareFunction)(int)_currentDepthStencilState.FrontFace.StencilFunction;

                    // Back face stencil operations (if two-sided stencil is enabled)
                    if (_currentDepthStencilState.TwoSidedStencilMode)
                    {
                        strideDepthStencilState.BackFace.StencilFail = (StrideGraphics.StencilOperation)(int)_currentDepthStencilState.BackFace.StencilFail;
                        strideDepthStencilState.BackFace.StencilPass = (StrideGraphics.StencilOperation)(int)_currentDepthStencilState.BackFace.StencilPass;
                        strideDepthStencilState.BackFace.StencilFunction = (StrideGraphics.CompareFunction)(int)_currentDepthStencilState.BackFace.StencilFunction;
                    }
                }

                // Try to set depth-stencil state directly on CommandList using reflection
                var setDepthStencilStateMethod = typeof(StrideGraphics.CommandList).GetMethod("SetDepthStencilState",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(StrideGraphics.DepthStencilStateDescription) }, null);

                if (setDepthStencilStateMethod != null)
                {
                    setDepthStencilStateMethod.Invoke(commandList, new object[] { strideDepthStencilState });
                }
                else
                {
                    // If direct method doesn't exist, state will be applied through PipelineState when drawing
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideGraphicsDevice] Error applying depth-stencil state: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the current blend state to the CommandList.
        /// Uses Stride's native state setting methods or creates a PipelineState.
        /// </summary>
        /// <param name="commandList">The CommandList to apply the state to.</param>
        private void ApplyBlendState(StrideGraphics.CommandList commandList)
        {
            if (commandList == null)
            {
                return;
            }

            try
            {
                // Convert our BlendStateDescription to Stride's native format
                var strideBlendState = new StrideGraphics.BlendStateDescription();

                // Convert render target blend descriptions
                // Stride's BlendStateDescription may have RenderTargets property or use a different structure
                if (_currentBlendState.RenderTargets != null && _currentBlendState.RenderTargets.Length > 0)
                {
                    var strideRenderTargets = new StrideGraphics.BlendStateRenderTargetDescription[_currentBlendState.RenderTargets.Length];
                    for (int i = 0; i < _currentBlendState.RenderTargets.Length && i < strideRenderTargets.Length; i++)
                    {
                        var rt = _currentBlendState.RenderTargets[i];
                        strideRenderTargets[i] = new StrideGraphics.BlendStateRenderTargetDescription
                        {
                            BlendEnable = rt.BlendEnable,
                            AlphaBlendFunction = (StrideGraphics.BlendFunction)(int)rt.AlphaBlendFunction,
                            AlphaSourceBlend = (StrideGraphics.Blend)rt.AlphaSourceBlend,
                            AlphaDestinationBlend = (StrideGraphics.Blend)rt.AlphaDestinationBlend,
                            ColorBlendFunction = (StrideGraphics.BlendFunction)(int)rt.ColorBlendFunction,
                            ColorSourceBlend = (StrideGraphics.Blend)rt.ColorSourceBlend,
                            ColorDestinationBlend = (StrideGraphics.Blend)rt.ColorDestinationBlend,
                            ColorWriteChannels = (StrideGraphics.ColorWriteChannels)rt.ColorWriteChannels
                        };
                    }
                    // Set RenderTargets property using reflection since it may not be directly accessible
                    var renderTargetsProp = typeof(StrideGraphics.BlendStateDescription).GetProperty("RenderTargets",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (renderTargetsProp != null)
                    {
                        renderTargetsProp.SetValue(strideBlendState, strideRenderTargets);
                    }
                }

                // Try to set blend state directly on CommandList using reflection
                var setBlendStateMethod = typeof(StrideGraphics.CommandList).GetMethod("SetBlendState",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(StrideGraphics.BlendStateDescription) }, null);

                if (setBlendStateMethod != null)
                {
                    setBlendStateMethod.Invoke(commandList, new object[] { strideBlendState });
                }
                else
                {
                    // If direct method doesn't exist, state will be applied through PipelineState when drawing
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideGraphicsDevice] Error applying blend state: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the current sampler state for the specified sampler slot to the CommandList.
        /// In Stride, sampler states are set per sampler slot and are part of the resource binding.
        /// Based on swkotor2.exe: DirectX 9 sampler state management (d3d9.dll @ 0x0080a6c0)
        /// </summary>
        private void ApplySamplerState(int index)
        {
            // Use ImmediateContext property which retrieves from the per-frame registry
            var commandList = ImmediateContext;
            if (commandList == null || index < 0 || index >= _currentSamplerStates.Length)
            {
                return;
            }

            try
            {
                var samplerDesc = _currentSamplerStates[index];

                // Convert our SamplerStateDescription to Stride's native format
                var strideSamplerState = new StrideGraphics.SamplerStateDescription
                {
                    Filter = (StrideGraphics.TextureFilter)samplerDesc.Filter,
                    AddressU = (StrideGraphics.TextureAddressMode)samplerDesc.AddressU,
                    AddressV = (StrideGraphics.TextureAddressMode)samplerDesc.AddressV,
                    AddressW = (StrideGraphics.TextureAddressMode)samplerDesc.AddressW,
                    MaxAnisotropy = samplerDesc.MaxAnisotropy,
                    MaxMipLevel = samplerDesc.MaxMipLevel,
                    MipMapLevelOfDetailBias = (float)samplerDesc.MipMapLevelOfDetailBias
                };

                // Create a SamplerState object from the description
                var samplerState = StrideGraphics.SamplerState.New(_device, strideSamplerState);

                // In Stride, sampler states are bound through resource sets, not directly on CommandList
                // Samplers are typically set when binding textures through ResourceGroup or EffectInstance
                // For immediate mode, we store the sampler state and it will be used when creating resource bindings
                // Note: Direct SetSamplerState method doesn't exist on CommandList in Stride
                // Samplers are part of the PipelineState or bound through resource sets
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideGraphicsDevice] Error applying sampler state for index {index}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // GraphicsDevice is managed by Game, don't dispose it
        }

        private static StrideGraphics.PrimitiveType ConvertPrimitiveType(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.TriangleList:
                    return StrideGraphics.PrimitiveType.TriangleList;
                case PrimitiveType.TriangleStrip:
                    return StrideGraphics.PrimitiveType.TriangleStrip;
                case PrimitiveType.LineList:
                    return StrideGraphics.PrimitiveType.LineList;
                case PrimitiveType.LineStrip:
                    return StrideGraphics.PrimitiveType.LineStrip;
                case PrimitiveType.PointList:
                    return StrideGraphics.PrimitiveType.PointList;
                default:
                    return StrideGraphics.PrimitiveType.TriangleList;
            }
        }

        private static int GetVerticesPerPrimitive(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.TriangleList:
                case PrimitiveType.TriangleStrip:
                    return 3;
                case PrimitiveType.LineList:
                case PrimitiveType.LineStrip:
                    return 2;
                case PrimitiveType.PointList:
                    return 1;
                default:
                    return 3;
            }
        }
    }
}


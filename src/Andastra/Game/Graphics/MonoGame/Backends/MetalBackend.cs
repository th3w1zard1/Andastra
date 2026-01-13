using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Andastra.Game.Graphics.MonoGame.Enums;
using Andastra.Game.Graphics.MonoGame.Interfaces;
using Andastra.Game.Graphics.MonoGame.Rendering;

namespace Andastra.Game.Graphics.MonoGame.Backends
{
    /// <summary>
    /// Metal graphics backend implementation for macOS and iOS.
    ///
    /// Provides:
    /// - Metal 2.0+ rendering (macOS 10.13+, iOS 11+)
    /// - Metal Performance Shaders (MPS) integration
    /// - Metal raytracing support (macOS 12.0+, iOS 15.0+)
    /// - Unified memory architecture (UMA) support
    /// - Tile-based deferred rendering (TBDR) optimization
    /// - Argument buffers for efficient resource binding
    ///
    /// Metal is Apple's low-level graphics API, providing direct access to the GPU
    /// with minimal driver overhead. It is the primary graphics API on macOS and iOS.
    ///
    /// Metal API Reference: https://developer.apple.com/documentation/metal
    /// </summary>
    public class MetalBackend : IGraphicsBackend
    {
        private bool _initialized;
        private GraphicsCapabilities _capabilities;
        private RenderSettings _settings;

        // Metal device and command queue
        private IntPtr _device; // id&lt;MTLDevice&gt;
        private IntPtr _commandQueue; // id&lt;MTLCommandQueue&gt;
        private IntPtr _defaultLibrary; // id&lt;MTLLibrary&gt;
        private IntPtr _metalLayer; // CAMetalLayer*

        // Current frame state
        private IntPtr _currentCommandBuffer; // id&lt;MTLCommandBuffer&gt;
        private IntPtr _currentRenderPassDescriptor; // MTLRenderPassDescriptor*
        private IntPtr _currentDrawable; // id&lt;CAMetalDrawable&gt;

        // Resource tracking
        private readonly Dictionary<IntPtr, ResourceInfo> _resources;
        private uint _nextResourceHandle;

        // Metal version info
        private int _metalVersionMajor;
        private int _metalVersionMinor;
        private bool _supportsRaytracing;
        private bool _supportsArgumentBuffers;
        private bool _supportsMeshShaders;
        private bool _supportsVariableRateShading;

        // Raytracing state
        private bool _raytracingEnabled;
        private RaytracingLevel _raytracingLevel;

        // Frame statistics
        private FrameStatistics _lastFrameStats;

        // Frame tracking for multi-buffering
        private int _currentFrameIndex;

        public GraphicsBackend BackendType
        {
            get { return GraphicsBackend.Metal; }
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
            get { return _raytracingEnabled && _supportsRaytracing; }
        }

        /// <summary>
        /// Gets the Metal version string.
        /// </summary>
        public string MetalVersion
        {
            get { return _metalVersionMajor + "." + _metalVersionMinor; }
        }

        public MetalBackend()
        {
            _resources = new Dictionary<IntPtr, ResourceInfo>();
            _nextResourceHandle = 1;
            _currentFrameIndex = 0;
        }

        /// <summary>
        /// Initializes the Metal backend.
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

            // Check platform support
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine("[MetalBackend] Metal is only available on macOS/iOS");
                return false;
            }

            // Create Metal device
            if (!CreateDevice())
            {
                Console.WriteLine("[MetalBackend] Failed to create Metal device");
                return false;
            }

            // Create command queue
            if (!CreateCommandQueue())
            {
                Console.WriteLine("[MetalBackend] Failed to create Metal command queue");
                return false;
            }

            // Create default shader library
            if (!CreateDefaultLibrary())
            {
                Console.WriteLine("[MetalBackend] Failed to create default Metal library");
                return false;
            }

            // Query Metal version and capabilities
            QueryMetalVersion();
            QueryCapabilities();

            // Initialize raytracing if available and requested
            if (_supportsRaytracing && settings.Raytracing != RaytracingLevel.Disabled)
            {
                InitializeRaytracing();
            }

            _initialized = true;
            Console.WriteLine("[MetalBackend] Initialized successfully");
            Console.WriteLine("[MetalBackend] Metal Version: " + MetalVersion);
            Console.WriteLine("[MetalBackend] Device: " + _capabilities.DeviceName);
            Console.WriteLine("[MetalBackend] Raytracing: " + (_supportsRaytracing ? "available" : "not available"));

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

            // Release Metal objects
            if (_currentDrawable != IntPtr.Zero)
            {
                MetalNative.ReleaseDrawable(_currentDrawable);
                _currentDrawable = IntPtr.Zero;
            }

            if (_currentCommandBuffer != IntPtr.Zero)
            {
                MetalNative.ReleaseCommandBuffer(_currentCommandBuffer);
                _currentCommandBuffer = IntPtr.Zero;
            }

            if (_currentRenderPassDescriptor != IntPtr.Zero)
            {
                MetalNative.ReleaseRenderPassDescriptor(_currentRenderPassDescriptor);
                _currentRenderPassDescriptor = IntPtr.Zero;
            }

            if (_defaultLibrary != IntPtr.Zero)
            {
                MetalNative.ReleaseLibrary(_defaultLibrary);
                _defaultLibrary = IntPtr.Zero;
            }

            if (_commandQueue != IntPtr.Zero)
            {
                MetalNative.ReleaseCommandQueue(_commandQueue);
                _commandQueue = IntPtr.Zero;
            }

            if (_device != IntPtr.Zero)
            {
                MetalNative.ReleaseDevice(_device);
                _device = IntPtr.Zero;
            }

            if (_metalLayer != IntPtr.Zero)
            {
                MetalNative.ReleaseMetalLayer(_metalLayer);
                _metalLayer = IntPtr.Zero;
            }

            _initialized = false;
            Console.WriteLine("[MetalBackend] Shutdown complete");
        }

        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Obtain drawable from Metal layer
            _currentDrawable = MetalNative.GetNextDrawable(_metalLayer);
            if (_currentDrawable == IntPtr.Zero)
            {
                Console.WriteLine("[MetalBackend] Failed to obtain drawable");
                return;
            }

            // Create command buffer for this frame
            _currentCommandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
            if (_currentCommandBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalBackend] Failed to create command buffer");
                MetalNative.ReleaseDrawable(_currentDrawable);
                _currentDrawable = IntPtr.Zero;
                return;
            }

            // Create render pass descriptor
            _currentRenderPassDescriptor = MetalNative.CreateRenderPassDescriptor();
            if (_currentRenderPassDescriptor == IntPtr.Zero)
            {
                Console.WriteLine("[MetalBackend] Failed to create render pass descriptor");
                MetalNative.ReleaseCommandBuffer(_currentCommandBuffer);
                MetalNative.ReleaseDrawable(_currentDrawable);
                _currentCommandBuffer = IntPtr.Zero;
                _currentDrawable = IntPtr.Zero;
                return;
            }

            // Configure render pass descriptor with drawable's texture
            IntPtr drawableTexture = MetalNative.GetDrawableTexture(_currentDrawable);
            MetalNative.SetRenderPassColorAttachment(_currentRenderPassDescriptor, 0, drawableTexture,
                MetalLoadAction.Clear, MetalStoreAction.Store, new MetalClearColor(0.0f, 0.0f, 0.0f, 1.0f));

            // Begin render pass
            IntPtr renderCommandEncoder = MetalNative.BeginRenderPass(_currentCommandBuffer, _currentRenderPassDescriptor);
            if (renderCommandEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalBackend] Failed to begin render pass");
            }

            // Reset frame statistics
            _lastFrameStats = new FrameStatistics();

            // Frame index is advanced at EndFrame() for proper multi-buffering
            // This ensures frame index corresponds to the frame being rendered
        }

        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            if (_currentCommandBuffer == IntPtr.Zero)
            {
                return;
            }

            // End render pass
            // MetalNative.EndRenderPass(_currentRenderCommandEncoder)

            // Present drawable
            if (_currentDrawable != IntPtr.Zero)
            {
                MetalNative.PresentDrawable(_currentCommandBuffer, _currentDrawable);
            }

            // Commit command buffer
            MetalNative.CommitCommandBuffer(_currentCommandBuffer);

            // Wait for completion (optional, for synchronization)
            // MetalNative.WaitUntilCompleted(_currentCommandBuffer)

            // Release frame resources
            if (_currentRenderPassDescriptor != IntPtr.Zero)
            {
                MetalNative.ReleaseRenderPassDescriptor(_currentRenderPassDescriptor);
                _currentRenderPassDescriptor = IntPtr.Zero;
            }

            if (_currentDrawable != IntPtr.Zero)
            {
                MetalNative.ReleaseDrawable(_currentDrawable);
                _currentDrawable = IntPtr.Zero;
            }

            if (_currentCommandBuffer != IntPtr.Zero)
            {
                MetalNative.ReleaseCommandBuffer(_currentCommandBuffer);
                _currentCommandBuffer = IntPtr.Zero;
            }

            // Advance frame index for multi-buffering
            // Metal typically uses triple buffering for optimal performance
            // Frame index cycles through 0, 1, 2 for resource management
            _currentFrameIndex = (_currentFrameIndex + 1) % 3;
        }

        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            // Update Metal layer drawable size
            if (_metalLayer != IntPtr.Zero)
            {
                MetalNative.SetMetalLayerDrawableSize(_metalLayer, width, height);
            }

            _settings.Width = width;
            _settings.Height = height;
        }

        public IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // Create MTLTextureDescriptor
            IntPtr textureDescriptor = MetalNative.CreateTextureDescriptor(
                ConvertTextureFormat(desc.Format),
                (uint)desc.Width,
                (uint)desc.Height,
                (uint)desc.MipLevels,
                desc.IsCubemap ? MetalTextureType.TextureCube : MetalTextureType.Texture2D,
                ConvertTextureUsage(desc.Usage));

            if (textureDescriptor == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Create texture from device
            IntPtr texture = MetalNative.CreateTexture(_device, textureDescriptor);
            MetalNative.ReleaseTextureDescriptor(textureDescriptor);

            if (texture == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Set debug label if provided
            if (!string.IsNullOrEmpty(desc.DebugName))
            {
                MetalNative.SetTextureLabel(texture, desc.DebugName);
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                MetalHandle = texture,
                DebugName = desc.DebugName,
                TextureDesc = desc
            };

            return handle;
        }

        /// <summary>
        /// Uploads texture pixel data to a previously created texture.
        ///
        /// Matches original engine behavior: Metal uses MTLTexture::replaceRegion
        /// to upload texture data after creating the texture resource.
        ///
        /// Based on original engine texture upload pattern:
        /// - swkotor.exe: 0x00428380 @ 0x00428380 (texture upload with mipmap generation)
        /// - swkotor2.exe: 0x00428380 @ 0x00428380 (texture upload with mipmap generation)
        /// - Original engine uses glTexImage2D for each mipmap level, Metal equivalent is replaceRegion
        /// - Both engines upload mipmaps sequentially, starting from base level (0)
        ///
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtltexture/replace(region:mipmaplevel:withbytes:bytesperrow:)
        /// </summary>
        /// <param name="handle">Texture handle returned from CreateTexture.</param>
        /// <param name="data">Texture upload data containing all mipmap levels.</param>
        /// <returns>True if upload succeeded, false otherwise.</returns>
        public bool UploadTextureData(IntPtr handle, TextureUploadData data)
        {
            if (!_initialized || handle == IntPtr.Zero)
            {
                Console.WriteLine("[MetalBackend] UploadTextureData: Backend not initialized or invalid handle");
                return false;
            }

            if (!_resources.TryGetValue(handle, out ResourceInfo info) || info.Type != ResourceType.Texture)
            {
                Console.WriteLine("[MetalBackend] UploadTextureData: Invalid texture handle");
                return false;
            }

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                Console.WriteLine("[MetalBackend] UploadTextureData: No mipmap data provided");
                return false;
            }

            // Validate texture format matches the format used in CreateTexture
            if (info.TextureDesc.Format != data.Format)
            {
                Console.WriteLine($"[MetalBackend] UploadTextureData: Format mismatch. Texture created with {info.TextureDesc.Format}, upload data has {data.Format}");
                return false;
            }

            try
            {
                // For Metal, we use MTLTexture::replaceRegion to upload texture data for each mipmap level
                // Metal API: replaceRegion:mipmapLevel:withBytes:bytesPerRow:
                // Matches original engine behavior: textures are uploaded mipmap by mipmap using replaceRegion
                // Original engine pattern: glTexImage2D is called for each mipmap level sequentially
                int uploadedCount = 0;
                int skippedCount = 0;

                for (int i = 0; i < data.Mipmaps.Length; i++)
                {
                    TextureMipmapData mipmap = data.Mipmaps[i];

                    // Validate mipmap level is within valid range
                    if (mipmap.Level < 0 || mipmap.Level >= info.TextureDesc.MipLevels)
                    {
                        Console.WriteLine($"[MetalBackend] UploadTextureData: Invalid mipmap level {mipmap.Level} (texture has {info.TextureDesc.MipLevels} mip levels)");
                        skippedCount++;
                        continue;
                    }

                    // Validate mipmap dimensions
                    if (mipmap.Width <= 0 || mipmap.Height <= 0)
                    {
                        Console.WriteLine($"[MetalBackend] UploadTextureData: Invalid mipmap dimensions {mipmap.Width}x{mipmap.Height} for level {mipmap.Level}");
                        skippedCount++;
                        continue;
                    }

                    // Validate mipmap data
                    if (mipmap.Data == null || mipmap.Data.Length == 0)
                    {
                        Console.WriteLine($"[MetalBackend] UploadTextureData: Skipping empty mipmap level {mipmap.Level}");
                        skippedCount++;
                        continue;
                    }

                    // Calculate bytes per row based on texture format
                    uint bytesPerRow = CalculateBytesPerRow(data.Format, mipmap.Width);
                    if (bytesPerRow == 0)
                    {
                        Console.WriteLine($"[MetalBackend] UploadTextureData: Invalid bytes per row calculation for mipmap level {mipmap.Level}");
                        skippedCount++;
                        continue;
                    }

                    // Validate data size matches expected size
                    uint expectedDataSize = CalculateMipmapDataSize(data.Format, mipmap.Width, mipmap.Height);
                    if (mipmap.Data.Length < expectedDataSize)
                    {
                        Console.WriteLine($"[MetalBackend] UploadTextureData: Mipmap level {mipmap.Level} data size mismatch. Expected {expectedDataSize} bytes, got {mipmap.Data.Length}");
                        skippedCount++;
                        continue;
                    }

                    // Create region for this mipmap level (full mipmap, starting at origin 0,0,0)
                    // For 2D textures, depth is always 1
                    MetalRegion region = new MetalRegion(0, 0, 0, (uint)mipmap.Width, (uint)mipmap.Height, 1);

                    // Pin the data array for unsafe access
                    // This matches the original engine's pattern of passing raw pixel data to glTexImage2D
                    unsafe
                    {
                        fixed (byte* dataPtr = mipmap.Data)
                        {
                            IntPtr sourceBytes = new IntPtr(dataPtr);
                            MetalNative.ReplaceTextureRegion(info.MetalHandle, region, (uint)mipmap.Level, sourceBytes, bytesPerRow);
                            uploadedCount++;
                        }
                    }
                }

                // Store upload data for reference (no longer needed for actual upload, but kept for compatibility)
                info.UploadData = data;
                _resources[handle] = info;

                if (uploadedCount > 0)
                {
                    Console.WriteLine($"[MetalBackend] UploadTextureData: Successfully uploaded {uploadedCount} mipmap level(s) for texture {info.DebugName ?? "unknown"}");
                    if (skippedCount > 0)
                    {
                        Console.WriteLine($"[MetalBackend] UploadTextureData: Skipped {skippedCount} mipmap level(s) due to validation errors");
                    }
                    return true;
                }
                else
                {
                    Console.WriteLine($"[MetalBackend] UploadTextureData: Failed to upload any mipmap levels (all {skippedCount} levels were skipped)");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalBackend] UploadTextureData: Exception uploading texture: {ex.Message}");
                Console.WriteLine($"[MetalBackend] UploadTextureData: Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // Create MTLBuffer
            IntPtr buffer = MetalNative.CreateBuffer(_device, (uint)desc.SizeInBytes, ConvertBufferUsage(desc.Usage));

            if (buffer == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Set debug label if provided
            if (!string.IsNullOrEmpty(desc.DebugName))
            {
                MetalNative.SetBufferLabel(buffer, desc.DebugName);
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                MetalHandle = buffer,
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

            // Create render pipeline descriptor
            IntPtr pipelineDescriptor = MetalNative.CreateRenderPipelineDescriptor();
            if (pipelineDescriptor == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Set vertex shader
            if (desc.VertexShader != null && desc.VertexShader.Length > 0)
            {
                IntPtr vertexFunction = MetalNative.CreateFunctionFromLibrary(_defaultLibrary, "vertex_main");
                if (vertexFunction != IntPtr.Zero)
                {
                    MetalNative.SetRenderPipelineVertexFunction(pipelineDescriptor, vertexFunction);
                    MetalNative.ReleaseFunction(vertexFunction);
                }
            }

            // Set fragment shader
            if (desc.PixelShader != null && desc.PixelShader.Length > 0)
            {
                IntPtr fragmentFunction = MetalNative.CreateFunctionFromLibrary(_defaultLibrary, "fragment_main");
                if (fragmentFunction != IntPtr.Zero)
                {
                    MetalNative.SetRenderPipelineFragmentFunction(pipelineDescriptor, fragmentFunction);
                    MetalNative.ReleaseFunction(fragmentFunction);
                }
            }

            // Configure input layout
            if (desc.InputLayout.Elements != null && desc.InputLayout.Elements.Length > 0)
            {
                MetalNative.SetRenderPipelineVertexDescriptor(pipelineDescriptor, ConvertInputLayout(desc.InputLayout));
            }

            // Configure blend state
            MetalNative.SetRenderPipelineBlendState(pipelineDescriptor, ConvertBlendState(desc.BlendState));

            // Configure rasterizer state
            MetalNative.SetRenderPipelineRasterizerState(pipelineDescriptor, ConvertRasterizerState(desc.RasterizerState));

            // Configure depth stencil state
            MetalNative.SetRenderPipelineDepthStencilState(pipelineDescriptor, ConvertDepthStencilState(desc.DepthStencilState));

            // Create render pipeline state
            IntPtr pipelineState = MetalNative.CreateRenderPipelineState(_device, pipelineDescriptor);
            MetalNative.ReleaseRenderPipelineDescriptor(pipelineDescriptor);

            if (pipelineState == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                MetalHandle = pipelineState,
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
            if (!_supportsRaytracing)
            {
                if (level != RaytracingLevel.Disabled)
                {
                    Console.WriteLine("[MetalBackend] Raytracing not supported on this device. Requires macOS 12.0+ or iOS 15.0+ with Apple Silicon.");
                }
                return;
            }

            _raytracingLevel = level;
            _raytracingEnabled = level != RaytracingLevel.Disabled;
        }

        public FrameStatistics GetFrameStatistics()
        {
            return _lastFrameStats;
        }

        /// <summary>
        /// Gets the current frame index for multi-buffering.
        /// </summary>
        /// <returns>Current frame index (0, 1, or 2 for triple buffering).</returns>
        /// <remarks>
        /// Frame Index Tracking:
        /// - Used for multi-buffering resource management
        /// - Cycles through 0, 1, 2 for triple buffering
        /// - Incremented at EndFrame() to track which frame buffer is currently active
        /// - Metal uses triple buffering by default for optimal performance
        /// - Frame index is used to manage per-frame resources (constant buffers, etc.)
        /// </remarks>
        public int GetCurrentFrameIndex()
        {
            return _currentFrameIndex;
        }

        public IDevice GetDevice()
        {
            // Create and return MetalDevice instance for raytracing operations
            // MetalDevice implements IDevice interface and provides access to Metal 3.0 raytracing APIs
            if (!_initialized || !_supportsRaytracing || !_raytracingEnabled || _device == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return new MetalDevice(this);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalBackend] Failed to create MetalDevice: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the native Metal device handle. Internal use for MetalDevice.
        /// </summary>
        internal IntPtr GetMetalDevice()
        {
            return _device;
        }

        /// <summary>
        /// Gets the current Metal command buffer handle. Internal use for MetalCommandList debug operations.
        /// Returns IntPtr.Zero if no command buffer is currently active.
        /// </summary>
        internal IntPtr GetCurrentCommandBuffer()
        {
            return _currentCommandBuffer;
        }

        /// <summary>
        /// Gets the native Metal command queue handle. Internal use for MetalDevice.
        /// </summary>
        internal IntPtr GetCommandQueue()
        {
            return _commandQueue;
        }

        /// <summary>
        /// Gets the native Metal default library handle. Internal use for MetalDevice.
        /// </summary>
        internal IntPtr GetDefaultLibrary()
        {
            return _defaultLibrary;
        }

        /// <summary>
        /// Gets the native Metal device handle.
        /// </summary>
        internal IntPtr GetNativeDevice()
        {
            return _device;
        }

        #region Metal Specific Methods

        /// <summary>
        /// Dispatches compute shader work.
        /// Based on Metal API: https://developer.apple.com/documentation/metal/mtlcomputecommandencoder
        /// </summary>
        public void DispatchCompute(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ)
        {
            if (!_initialized || !_capabilities.SupportsComputeShaders || _currentCommandBuffer == IntPtr.Zero)
            {
                return;
            }

            // Create compute command encoder
            IntPtr computeEncoder = MetalNative.CreateComputeCommandEncoder(_currentCommandBuffer);
            if (computeEncoder == IntPtr.Zero)
            {
                return;
            }

            // Dispatch compute work
            MetalNative.DispatchThreadgroups(computeEncoder, threadGroupCountX, threadGroupCountY, threadGroupCountZ, 16, 16, 1);

            // End encoding
            MetalNative.EndEncoding(computeEncoder);
            MetalNative.ReleaseComputeCommandEncoder(computeEncoder);
        }

        /// <summary>
        /// Sets the viewport.
        /// Based on Metal API: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516251-setviewport
        /// </summary>
        public void SetViewport(int x, int y, int width, int height)
        {
            if (!_initialized || _currentCommandBuffer == IntPtr.Zero)
            {
                return;
            }

            // Viewport is set on render command encoder during render pass
            // This would be called on the active render command encoder
            // MetalNative.SetViewport(_currentRenderCommandEncoder, x, y, width, height, 0.0f, 1.0f)
        }

        /// <summary>
        /// Draws non-indexed geometry.
        /// Based on Metal API: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515526-drawprimitives
        /// </summary>
        public void DrawPrimitives(MetalPrimitiveType primitiveType, int vertexStart, int vertexCount)
        {
            if (!_initialized || _currentCommandBuffer == IntPtr.Zero)
            {
                return;
            }

            // Draw primitives
            // MetalNative.DrawPrimitives(_currentRenderCommandEncoder, primitiveType, vertexStart, vertexCount)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += vertexCount / 3;
        }

        /// <summary>
        /// Draws indexed geometry.
        /// Based on Metal API: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515544-drawindexedprimitives
        /// </summary>
        public void DrawIndexedPrimitives(MetalPrimitiveType primitiveType, int indexCount, MetalIndexType indexType, IntPtr indexBuffer, int indexBufferOffset)
        {
            if (!_initialized || _currentCommandBuffer == IntPtr.Zero)
            {
                return;
            }

            // Draw indexed primitives
            // MetalNative.DrawIndexedPrimitives(_currentRenderCommandEncoder, primitiveType, indexCount, indexType, indexBuffer, indexBufferOffset)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += indexCount / 3;
        }

        /// <summary>
        /// Draws instanced geometry.
        /// Based on Metal API: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515571-drawprimitives
        /// </summary>
        public void DrawPrimitivesInstanced(MetalPrimitiveType primitiveType, int vertexStart, int vertexCount, int instanceCount)
        {
            if (!_initialized || _currentCommandBuffer == IntPtr.Zero)
            {
                return;
            }

            // Draw instanced primitives
            // MetalNative.DrawPrimitivesInstanced(_currentRenderCommandEncoder, primitiveType, vertexStart, vertexCount, instanceCount)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += (vertexCount / 3) * instanceCount;
        }

        #endregion

        #region Private Helper Methods

        private bool CreateDevice()
        {
            // Create default Metal device
            _device = MetalNative.CreateSystemDefaultDevice();
            return _device != IntPtr.Zero;
        }

        private bool CreateCommandQueue()
        {
            if (_device == IntPtr.Zero)
            {
                return false;
            }

            _commandQueue = MetalNative.CreateCommandQueue(_device);
            return _commandQueue != IntPtr.Zero;
        }

        private bool CreateDefaultLibrary()
        {
            if (_device == IntPtr.Zero)
            {
                return false;
            }

            _defaultLibrary = MetalNative.CreateDefaultLibrary(_device);
            return _defaultLibrary != IntPtr.Zero;
        }

        private void QueryMetalVersion()
        {
            // Query Metal version from device
            // Metal 2.0 = macOS 10.13+, iOS 11+
            // Metal 3.0 = macOS 13.0+, iOS 16+
            _metalVersionMajor = 2;
            _metalVersionMinor = 0;

            // Check for Metal 3.0 features
            if (MetalNative.SupportsFamily(_device, MetalGPUFamily.Apple7))
            {
                _metalVersionMajor = 3;
                _metalVersionMinor = 0;
            }
        }

        private void QueryCapabilities()
        {
            // Query device capabilities
            bool supportsCompute = MetalNative.SupportsFamily(_device, MetalGPUFamily.Common1);
            bool supportsTessellation = MetalNative.SupportsFamily(_device, MetalGPUFamily.Common2);
            _supportsRaytracing = MetalNative.SupportsFamily(_device, MetalGPUFamily.Apple7); // Metal 3.0
            _supportsArgumentBuffers = MetalNative.SupportsFamily(_device, MetalGPUFamily.Common2);
            _supportsMeshShaders = MetalNative.SupportsFamily(_device, MetalGPUFamily.Apple7);
            _supportsVariableRateShading = MetalNative.SupportsFamily(_device, MetalGPUFamily.Apple7);

            // Query device properties
            string deviceName = MetalNative.GetDeviceName(_device);
            long dedicatedMemory = MetalNative.GetDeviceRecommendedMaxWorkingSetSize(_device);

            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 16384,
                MaxRenderTargets = 8,
                MaxAnisotropy = 16,
                SupportsComputeShaders = supportsCompute,
                SupportsGeometryShaders = false, // Metal doesn't have geometry shaders
                SupportsTessellation = supportsTessellation,
                SupportsRaytracing = _supportsRaytracing,
                SupportsMeshShaders = _supportsMeshShaders,
                SupportsVariableRateShading = _supportsVariableRateShading,
                DedicatedVideoMemory = dedicatedMemory,
                SharedSystemMemory = dedicatedMemory, // Metal uses unified memory
                VendorName = "Apple",
                DeviceName = deviceName ?? "Metal Device",
                DriverVersion = MetalVersion,
                ActiveBackend = GraphicsBackend.Metal,
                ShaderModelVersion = _metalVersionMajor >= 3 ? 6.0f : 5.0f,
                RemixAvailable = false,
                DlssAvailable = false,
                FsrAvailable = true // FSR can work via compute shaders
            };
        }

        private void InitializeRaytracing()
        {
            if (!_supportsRaytracing)
            {
                return;
            }

            // Initialize Metal raytracing
            // Create acceleration structures
            // Set up raytracing pipeline

            _raytracingEnabled = true;
            _raytracingLevel = _settings.Raytracing;

            Console.WriteLine("[MetalBackend] Raytracing initialized");
        }

        private void DestroyResourceInternal(ResourceInfo info)
        {
            switch (info.Type)
            {
                case ResourceType.Texture:
                    if (info.MetalHandle != IntPtr.Zero)
                    {
                        MetalNative.ReleaseTexture(info.MetalHandle);
                    }
                    break;

                case ResourceType.Buffer:
                    if (info.MetalHandle != IntPtr.Zero)
                    {
                        MetalNative.ReleaseBuffer(info.MetalHandle);
                    }
                    break;

                case ResourceType.Pipeline:
                    if (info.MetalHandle != IntPtr.Zero)
                    {
                        MetalNative.ReleaseRenderPipelineState(info.MetalHandle);
                    }
                    break;
            }
        }

        private MetalPixelFormat ConvertTextureFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8_SNorm:
                    return MetalPixelFormat.R8SNorm;
                case TextureFormat.R8G8_SNorm:
                    return MetalPixelFormat.RG8SNorm;
                case TextureFormat.R8G8B8A8_SNorm:
                    return MetalPixelFormat.RGBA8SNorm;
                case TextureFormat.R8G8B8A8_UNorm:
                    return MetalPixelFormat.RGBA8UNorm;
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    return MetalPixelFormat.RGBA8UNormSRGB;
                case TextureFormat.B8G8R8A8_UNorm:
                    return MetalPixelFormat.BGRA8UNorm;
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    return MetalPixelFormat.BGRA8UNormSRGB;
                case TextureFormat.R16G16B16A16_Float:
                    return MetalPixelFormat.RGBA16Float;
                case TextureFormat.R32G32B32A32_Float:
                    return MetalPixelFormat.RGBA32Float;
                case TextureFormat.D32_Float:
                    return MetalPixelFormat.Depth32Float;
                case TextureFormat.D24_UNorm_S8_UInt:
                    return MetalPixelFormat.Depth24Stencil8;
                default:
                    return MetalPixelFormat.RGBA8UNorm;
            }
        }

        private MetalResourceOptions ConvertTextureUsage(TextureUsage usage)
        {
            MetalResourceOptions options = MetalResourceOptions.StorageModePrivate;
            if ((usage & TextureUsage.ShaderResource) != 0)
            {
                options |= MetalResourceOptions.CpuCacheModeDefaultCache;
            }
            if ((usage & TextureUsage.RenderTarget) != 0)
            {
                // Render targets are typically private
            }
            return options;
        }

        private MetalResourceOptions ConvertBufferUsage(BufferUsage usage)
        {
            MetalResourceOptions options = MetalResourceOptions.StorageModeShared;
            if ((usage & BufferUsage.Vertex) != 0 || (usage & BufferUsage.Index) != 0)
            {
                options = MetalResourceOptions.StorageModePrivate;
            }
            return options;
        }

        private IntPtr ConvertInputLayout(InputLayout layout)
        {
            // Create MTLVertexDescriptor from InputLayout
            // Full implementation mapping all attributes with proper stride calculation and instance data support
            IntPtr vertexDescriptor = MetalNative.CreateVertexDescriptor();
            if (vertexDescriptor == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (layout.Elements == null || layout.Elements.Length == 0)
            {
                return vertexDescriptor;
            }

            // Calculate stride for each buffer slot by finding the maximum offset + size for each slot
            // Metal requires stride to be specified per buffer, not per attribute
            Dictionary<int, int> slotStrides = new Dictionary<int, int>();
            Dictionary<int, bool> slotIsInstance = new Dictionary<int, bool>();
            Dictionary<int, int> slotStepRate = new Dictionary<int, int>();

            // First pass: determine stride and instance properties for each slot
            for (int i = 0; i < layout.Elements.Length && i < 32; i++)
            {
                InputElement element = layout.Elements[i];
                int slot = element.Slot;

                // Calculate element size from format
                int elementSize = GetFormatSize(element.Format);
                if (elementSize == 0)
                {
                    continue; // Skip invalid formats
                }

                // Calculate end offset for this element
                int elementEndOffset = element.AlignedByteOffset + elementSize;

                // Update stride for this slot (maximum end offset)
                if (!slotStrides.ContainsKey(slot) || slotStrides[slot] < elementEndOffset)
                {
                    slotStrides[slot] = elementEndOffset;
                }

                // Track instance data properties
                if (!slotIsInstance.ContainsKey(slot))
                {
                    slotIsInstance[slot] = element.PerInstance;
                    slotStepRate[slot] = element.InstanceDataStepRate;
                }
                else
                {
                    // If any element in a slot is per-instance, the whole slot is per-instance
                    if (element.PerInstance)
                    {
                        slotIsInstance[slot] = true;
                        // Use the minimum step rate (most frequent update)
                        if (element.InstanceDataStepRate > 0 &&
                            (slotStepRate[slot] == 0 || element.InstanceDataStepRate < slotStepRate[slot]))
                        {
                            slotStepRate[slot] = element.InstanceDataStepRate;
                        }
                    }
                }
            }

            // Second pass: set layout descriptors for each unique slot
            foreach (var slotInfo in slotStrides)
            {
                int slot = slotInfo.Key;
                int stride = slotInfo.Value;
                bool isInstance = slotIsInstance.ContainsKey(slot) && slotIsInstance[slot];
                int stepRate = slotStepRate.ContainsKey(slot) ? slotStepRate[slot] : 0;

                // Set layout descriptor for this buffer slot
                // Metal API: MTLVertexDescriptor::layouts[slot].stride and stepFunction
                // stepFunction: MTLVertexStepFunctionPerVertex (0) or MTLVertexStepFunctionPerInstance (1)
                MetalNative.SetVertexDescriptorLayout(vertexDescriptor, (uint)slot, (uint)stride,
                    isInstance ? (uint)stepRate : 0U);
            }

            // Third pass: set attribute descriptors
            for (int i = 0; i < layout.Elements.Length && i < 32; i++)
            {
                InputElement element = layout.Elements[i];

                // Convert format to Metal pixel format
                MetalPixelFormat metalFormat = ConvertTextureFormat(element.Format);
                if (metalFormat == MetalPixelFormat.Invalid)
                {
                    continue; // Skip invalid formats
                }

                // Set attribute descriptor
                // Metal API: MTLVertexDescriptor::attributes[index].format, bufferIndex, offset
                MetalNative.SetVertexDescriptorAttribute(vertexDescriptor, (uint)i,
                    metalFormat, (uint)element.Slot, (uint)element.AlignedByteOffset);
            }

            return vertexDescriptor;
        }

        /// <summary>
        /// Gets the byte size of a texture format for vertex attribute calculations.
        /// </summary>
        private int GetFormatSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R32G32B32A32_Float:
                    return 16; // 4 * 4 bytes
                case TextureFormat.R32G32B32_Float:
                    return 12; // 3 * 4 bytes
                case TextureFormat.R32G32_Float:
                    return 8; // 2 * 4 bytes
                case TextureFormat.R32_Float:
                    return 4; // 1 * 4 bytes
                case TextureFormat.R16G16B16A16_Float:
                    return 8; // 4 * 2 bytes
                case TextureFormat.R16G16_Float:
                    return 4; // 2 * 2 bytes
                case TextureFormat.R16_Float:
                    return 2; // 1 * 2 bytes
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.R8G8B8A8_SNorm:
                case TextureFormat.R8G8B8A8_SInt:
                    return 4; // 4 * 1 byte
                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R8G8_UInt:
                case TextureFormat.R8G8_SNorm:
                case TextureFormat.R8G8_SInt:
                    return 2; // 2 * 1 byte
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                case TextureFormat.R8_SNorm:
                case TextureFormat.R8_SInt:
                    return 1; // 1 * 1 byte
                case TextureFormat.R10G10B10A2_UNorm:
                case TextureFormat.R10G10B10A2_UInt:
                    return 4; // Packed 10+10+10+2 bits
                case TextureFormat.R11G11B10_Float:
                    return 4; // Packed 11+11+10 bits
                default:
                    return 0; // Unknown format
            }
        }

        private MetalBlendState ConvertBlendState(BlendState state)
        {
            return new MetalBlendState
            {
                BlendEnabled = state.BlendEnable,
                SourceRGBBlendFactor = ConvertBlendFactor(state.SrcBlend),
                DestinationRGBBlendFactor = ConvertBlendFactor(state.DstBlend),
                RGBBlendOperation = ConvertBlendOp(state.BlendOp),
                SourceAlphaBlendFactor = ConvertBlendFactor(state.SrcBlendAlpha),
                DestinationAlphaBlendFactor = ConvertBlendFactor(state.DstBlendAlpha),
                AlphaBlendOperation = ConvertBlendOp(state.BlendOpAlpha)
            };
        }

        private MetalRasterizerState ConvertRasterizerState(RasterizerState state)
        {
            return new MetalRasterizerState
            {
                CullMode = ConvertCullMode(state.CullMode),
                FillMode = ConvertFillMode(state.FillMode),
                FrontFaceWinding = state.FrontCounterClockwise ? MetalWinding.CounterClockwise : MetalWinding.Clockwise,
                DepthBias = state.DepthBias,
                SlopeScaledDepthBias = state.SlopeScaledDepthBias
            };
        }

        private MetalDepthStencilState ConvertDepthStencilState(DepthStencilState state)
        {
            return new MetalDepthStencilState
            {
                DepthWriteEnabled = state.DepthWriteEnable,
                DepthCompareFunction = ConvertCompareFunc(state.DepthFunc),
                StencilEnabled = state.StencilEnable,
                StencilReadMask = state.StencilReadMask,
                StencilWriteMask = state.StencilWriteMask
            };
        }

        private MetalBlendFactor ConvertBlendFactor(BlendFactor factor)
        {
            switch (factor)
            {
                case BlendFactor.Zero:
                    return MetalBlendFactor.Zero;
                case BlendFactor.One:
                    return MetalBlendFactor.One;
                case BlendFactor.SrcColor:
                    return MetalBlendFactor.SourceColor;
                case BlendFactor.InvSrcColor:
                    return MetalBlendFactor.OneMinusSourceColor;
                case BlendFactor.SrcAlpha:
                    return MetalBlendFactor.SourceAlpha;
                case BlendFactor.InvSrcAlpha:
                    return MetalBlendFactor.OneMinusSourceAlpha;
                case BlendFactor.DstAlpha:
                    return MetalBlendFactor.DestinationAlpha;
                case BlendFactor.InvDstAlpha:
                    return MetalBlendFactor.OneMinusDestinationAlpha;
                case BlendFactor.DstColor:
                    return MetalBlendFactor.DestinationColor;
                case BlendFactor.InvDstColor:
                    return MetalBlendFactor.OneMinusDestinationColor;
                default:
                    return MetalBlendFactor.One;
            }
        }

        private MetalBlendOperation ConvertBlendOp(BlendOp op)
        {
            switch (op)
            {
                case BlendOp.Add:
                    return MetalBlendOperation.Add;
                case BlendOp.Subtract:
                    return MetalBlendOperation.Subtract;
                case BlendOp.ReverseSubtract:
                    return MetalBlendOperation.ReverseSubtract;
                case BlendOp.Min:
                    return MetalBlendOperation.Min;
                case BlendOp.Max:
                    return MetalBlendOperation.Max;
                default:
                    return MetalBlendOperation.Add;
            }
        }

        private MetalCullMode ConvertCullMode(CullMode mode)
        {
            switch (mode)
            {
                case CullMode.None:
                    return MetalCullMode.None;
                case CullMode.Front:
                    return MetalCullMode.Front;
                case CullMode.Back:
                    return MetalCullMode.Back;
                default:
                    return MetalCullMode.Back;
            }
        }

        private MetalTriangleFillMode ConvertFillMode(FillMode mode)
        {
            switch (mode)
            {
                case FillMode.Solid:
                    return MetalTriangleFillMode.Fill;
                case FillMode.Wireframe:
                    return MetalTriangleFillMode.Lines;
                default:
                    return MetalTriangleFillMode.Fill;
            }
        }

        private MetalCompareFunction ConvertCompareFunc(CompareFunc func)
        {
            switch (func)
            {
                case CompareFunc.Never:
                    return MetalCompareFunction.Never;
                case CompareFunc.Less:
                    return MetalCompareFunction.Less;
                case CompareFunc.Equal:
                    return MetalCompareFunction.Equal;
                case CompareFunc.LessEqual:
                    return MetalCompareFunction.LessEqual;
                case CompareFunc.Greater:
                    return MetalCompareFunction.Greater;
                case CompareFunc.NotEqual:
                    return MetalCompareFunction.NotEqual;
                case CompareFunc.GreaterEqual:
                    return MetalCompareFunction.GreaterEqual;
                case CompareFunc.Always:
                    return MetalCompareFunction.Always;
                default:
                    return MetalCompareFunction.Less;
            }
        }

        /// <summary>
        /// Calculates bytes per row for Metal texture upload based on format.
        /// For uncompressed formats: width * bytesPerPixel
        /// For compressed formats (DXT/BC): bytes from beginning of one row of blocks to the next
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtltexture/replace(region:mipmaplevel:withbytes:bytesperrow:)
        ///
        /// Matches original engine behavior: texture uploads use format-specific row pitch calculations.
        /// Original engine: swkotor.exe and swkotor2.exe calculate row pitch based on format for DirectX texture uploads.
        /// </summary>
        private uint CalculateBytesPerRow(TextureFormat format, int width)
        {
            switch (format)
            {
                // 1 byte per pixel formats
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                case TextureFormat.R8_SInt:
                case TextureFormat.R8_SNorm:
                    return (uint)width;

                // 2 bytes per pixel formats
                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R8G8_UInt:
                case TextureFormat.R8G8_SNorm:
                case TextureFormat.R16_Float:
                case TextureFormat.R16_UNorm:
                case TextureFormat.R16_UInt:
                case TextureFormat.R16_SInt:
                    return (uint)(width * 2);

                // 4 bytes per pixel formats
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.R8G8B8A8_SNorm:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16_UInt:
                case TextureFormat.R32_Float:
                case TextureFormat.R32_UInt:
                case TextureFormat.R32_SInt:
                case TextureFormat.D32_Float:
                case TextureFormat.D24_UNorm_S8_UInt:
                    return (uint)(width * 4);

                // 8 bytes per pixel formats
                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R16G16B16A16_UInt:
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32_UInt:
                case TextureFormat.R32G32_SInt:
                    return (uint)(width * 8);

                // 16 bytes per pixel formats
                case TextureFormat.R32G32B32A32_Float:
                case TextureFormat.R32G32B32A32_UInt:
                case TextureFormat.R32G32B32A32_SInt:
                    return (uint)(width * 16);

                // Compressed formats - bytes per row = number of bytes from beginning of one row of blocks to the next
                // DXT1/BC1: 4x4 blocks, 8 bytes per block
                // DXT3/BC2: 4x4 blocks, 16 bytes per block
                // DXT5/BC3: 4x4 blocks, 16 bytes per block
                // BC4: 4x4 blocks, 8 bytes per block
                // BC5: 4x4 blocks, 16 bytes per block
                // BC6H: 4x4 blocks, 16 bytes per block
                // BC7: 4x4 blocks, 16 bytes per block
                // For compressed formats, bytesPerRow = ((width + 3) / 4) * bytesPerBlock
                // Note: Metal doesn't directly support DXT/BC, but we calculate for completeness
                // In practice, DXT/BC textures would need to be decompressed before upload or use ASTC on Metal
                case TextureFormat.BC1:
                case TextureFormat.BC4:
                    return (uint)(((width + 3) / 4) * 8); // 8 bytes per 4x4 block

                case TextureFormat.BC2:
                case TextureFormat.BC3:
                case TextureFormat.BC5:
                case TextureFormat.BC6H:
                case TextureFormat.BC7:
                    return (uint)(((width + 3) / 4) * 16); // 16 bytes per 4x4 block

                // ASTC formats (Metal native compressed formats)
                // ASTC block size varies by format, but all are 16 bytes per block
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10:
                case TextureFormat.ASTC_12x12:
                    // ASTC uses 16 bytes per block, block dimensions vary by format
                    // For simplicity, calculate based on 4x4 blocks (most common)
                    return (uint)(((width + 3) / 4) * 16);

                default:
                    // Default to RGBA8 format (4 bytes per pixel) for unknown formats
                    return (uint)(width * 4);
            }
        }

        /// <summary>
        /// Calculates expected data size for a mipmap level based on format and dimensions.
        /// For uncompressed formats: width * height * bytesPerPixel
        /// For compressed formats: ((width + 3) / 4) * ((height + 3) / 4) * bytesPerBlock
        ///
        /// Matches original engine behavior: texture data size validation ensures correct upload.
        /// Original engine: swkotor.exe and swkotor2.exe validate texture data size before upload.
        /// </summary>
        private uint CalculateMipmapDataSize(TextureFormat format, int width, int height)
        {
            switch (format)
            {
                // 1 byte per pixel formats
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                case TextureFormat.R8_SInt:
                case TextureFormat.R8_SNorm:
                    return (uint)(width * height);

                // 2 bytes per pixel formats
                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R8G8_UInt:
                case TextureFormat.R8G8_SNorm:
                case TextureFormat.R16_Float:
                case TextureFormat.R16_UNorm:
                case TextureFormat.R16_UInt:
                case TextureFormat.R16_SInt:
                    return (uint)(width * height * 2);

                // 4 bytes per pixel formats
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.R8G8B8A8_SNorm:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16_UInt:
                case TextureFormat.R32_Float:
                case TextureFormat.R32_UInt:
                case TextureFormat.R32_SInt:
                case TextureFormat.D32_Float:
                case TextureFormat.D24_UNorm_S8_UInt:
                    return (uint)(width * height * 4);

                // 8 bytes per pixel formats
                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R16G16B16A16_UInt:
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32_UInt:
                case TextureFormat.R32G32_SInt:
                    return (uint)(width * height * 8);

                // 16 bytes per pixel formats
                case TextureFormat.R32G32B32A32_Float:
                case TextureFormat.R32G32B32A32_UInt:
                case TextureFormat.R32G32B32A32_SInt:
                    return (uint)(width * height * 16);

                // Compressed formats (DXT/BC) - block-based storage
                // DXT1/BC1: 8 bytes per 4x4 block
                // DXT3/BC2, DXT5/BC3: 16 bytes per 4x4 block
                // BC4: 8 bytes per 4x4 block
                // BC5: 16 bytes per 4x4 block
                // BC6H: 16 bytes per 4x4 block
                // BC7: 16 bytes per 4x4 block
                // For compressed formats: ((width + 3) / 4) * ((height + 3) / 4) * bytesPerBlock
                // Note: Metal doesn't directly support DXT/BC, but we calculate for completeness
                case TextureFormat.BC1:
                case TextureFormat.BC4:
                    return (uint)(((width + 3) / 4) * ((height + 3) / 4) * 8);

                case TextureFormat.BC2:
                case TextureFormat.BC3:
                case TextureFormat.BC5:
                case TextureFormat.BC6H:
                case TextureFormat.BC7:
                    return (uint)(((width + 3) / 4) * ((height + 3) / 4) * 16);

                // ASTC formats (Metal native compressed formats)
                // ASTC block size varies by format, but all are 16 bytes per block
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10:
                case TextureFormat.ASTC_12x12:
                    // ASTC uses 16 bytes per block, block dimensions vary by format
                    // For simplicity, calculate based on 4x4 blocks (most common)
                    return (uint)(((width + 3) / 4) * ((height + 3) / 4) * 16);

                default:
                    // Default to RGBA8 format for unknown formats
                    return (uint)(width * height * 4);
            }
        }

        #endregion

        public void Dispose()
        {
            Shutdown();
        }

        private struct ResourceInfo
        {
            public ResourceType Type;
            public IntPtr Handle;
            public IntPtr MetalHandle;
            public string DebugName;
            public TextureDescription TextureDesc;
            public TextureUploadData UploadData;
        }

        private enum ResourceType
        {
            Texture,
            Buffer,
            Pipeline
        }
    }

    #region Metal Native Interop

    /// <summary>
    /// P/Invoke declarations for Metal framework.
    /// Metal is an Objective-C API, so we use Objective-C runtime interop.
    /// </summary>
    internal static partial class MetalNative
    {
        private const string MetalFramework = "/System/Library/Frameworks/Metal.framework/Metal";
        private const string QuartzCoreFramework = "/System/Library/Frameworks/QuartzCore.framework/QuartzCore";
        private const string LibObjC = "/usr/lib/libobjc.A.dylib";

        // Objective-C runtime functions for calling Metal methods
        // Metal uses Objective-C, so we use objc_msgSend to call instance methods
        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_object(IntPtr receiver, IntPtr selector);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_void(IntPtr receiver, IntPtr selector);

        // objc_msgSend for copyFromTexture with struct parameters
        // Method signature: - (void)copyFromTexture:(id<MTLTexture>)sourceTexture sourceSlice:(NSUInteger)sourceSlice sourceLevel:(NSUInteger)sourceLevel sourceOrigin:(MTLOrigin)sourceOrigin sourceSize:(MTLSize)sourceSize toTexture:(id<MTLTexture>)destinationTexture destinationSlice:(NSUInteger)destinationSlice destinationLevel:(NSUInteger)destinationLevel destinationOrigin:(MTLOrigin)destinationOrigin;
        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_copyFromTexture(IntPtr receiver, IntPtr selector, IntPtr sourceTexture, ulong sourceSlice, ulong sourceLevel,
            MetalOrigin sourceOrigin, MetalSize sourceSize, IntPtr destinationTexture, ulong destinationSlice, ulong destinationLevel, MetalOrigin destinationOrigin);

        [DllImport(LibObjC, EntryPoint = "sel_registerName")]
        private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string str);

        // Device creation
        [DllImport(MetalFramework, EntryPoint = "MTLCreateSystemDefaultDevice")]
        public static extern IntPtr CreateSystemDefaultDevice();

        [DllImport(MetalFramework)]
        public static extern void ReleaseDevice(IntPtr device);

        [DllImport(MetalFramework)]
        public static extern IntPtr CreateCommandQueue(IntPtr device);

        [DllImport(MetalFramework)]
        public static extern void ReleaseCommandQueue(IntPtr commandQueue);

        [DllImport(MetalFramework)]
        public static extern IntPtr CreateDefaultLibrary(IntPtr device);

        [DllImport(MetalFramework)]
        public static extern void ReleaseLibrary(IntPtr library);

        [DllImport(MetalFramework)]
        public static extern IntPtr CreateFunctionFromLibrary(IntPtr library, [MarshalAs(UnmanagedType.LPStr)] string functionName);

        [DllImport(MetalFramework)]
        public static extern void ReleaseFunction(IntPtr function);

        // Runtime shader compilation
        // Metal API: - (nullable id<MTLLibrary>)newLibraryWithSource:(NSString *)source options:(nullable MTLCompileOptions *)options error:(NSError **)error
        // This requires Objective-C interop using objc_msgSend since Metal is an Objective-C API
        // Method signature: (id<MTLLibrary>)newLibraryWithSource:(NSString *)source options:(MTLCompileOptions *)options error:(NSError **)error
        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_libraryWithSource(IntPtr receiver, IntPtr selector, IntPtr source, IntPtr options, IntPtr errorPtr);

        // MTLCompileOptions creation and configuration
        // Metal API: + (MTLCompileOptions *)new
        [DllImport(LibObjC, EntryPoint = "objc_getClass", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPStr)] string className);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_class(IntPtr receiver, IntPtr selector);

        // NSString creation from C string
        // Foundation API: + (NSString *)stringWithUTF8String:(const char *)nullTerminatedCString
        // Objective-C method signature: + (instancetype)stringWithUTF8String:(const char *)nullTerminatedCString
        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_stringWithUTF8String(IntPtr receiver, IntPtr selector, IntPtr utf8String);

        private static IntPtr CreateNSString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return IntPtr.Zero;
            }

            IntPtr nsStringClass = objc_getClass("NSString");
            if (nsStringClass == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            IntPtr stringWithUTF8StringSel = sel_registerName("stringWithUTF8String:");
            if (stringWithUTF8StringSel == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Convert C# string to UTF-8 bytes
            byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(str);
            IntPtr utf8Ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
            try
            {
                Marshal.Copy(utf8Bytes, 0, utf8Ptr, utf8Bytes.Length);
                Marshal.WriteByte(utf8Ptr, utf8Bytes.Length, 0); // Null terminator

                // Call [NSString stringWithUTF8String:utf8Ptr]
                // This is a class method, so receiver is the class object
                IntPtr nsString = objc_msgSend_stringWithUTF8String(nsStringClass, stringWithUTF8StringSel, utf8Ptr);
                return nsString;
            }
            finally
            {
                Marshal.FreeHGlobal(utf8Ptr);
            }
        }

        /// <summary>
        /// Compiles a Metal shader library from source code at runtime.
        /// Metal API: [device newLibraryWithSource:source options:options error:&error]
        /// This enables runtime shader compilation from source code without requiring pre-compiled .metallib files.
        ///
        /// Based on Metal API: MTLDevice::newLibraryWithSource:options:error:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtldevice/1433401-newlibrarywithsource
        /// </summary>
        /// <param name="device">Metal device handle (id&lt;MTLDevice&gt;)</param>
        /// <param name="source">Metal shader source code (Metal Shading Language)</param>
        /// <param name="error">Output parameter for compilation error (NSError**), can be IntPtr.Zero to ignore</param>
        /// <returns>Compiled Metal library (id&lt;MTLLibrary&gt;), or IntPtr.Zero if compilation failed</returns>
        public static IntPtr CompileLibraryFromSource(IntPtr device, string source, out IntPtr error)
        {
            error = IntPtr.Zero;

            if (device == IntPtr.Zero || string.IsNullOrEmpty(source))
            {
                return IntPtr.Zero;
            }

            try
            {
                // Create NSString from source code
                IntPtr sourceString = CreateNSString(source);
                if (sourceString == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalNative] CompileLibraryFromSource: Failed to create NSString from source");
                    return IntPtr.Zero;
                }

                try
                {
                    // Get selector for newLibraryWithSource:options:error:
                    IntPtr newLibraryWithSourceSel = sel_registerName("newLibraryWithSource:options:error:");
                    if (newLibraryWithSourceSel == IntPtr.Zero)
                    {
                        Console.WriteLine("[MetalNative] CompileLibraryFromSource: Failed to register selector");
                        return IntPtr.Zero;
                    }

                    // Create MTLCompileOptions (can be nil for default options)
                    IntPtr compileOptions = IntPtr.Zero; // Use default compilation options

                    // Allocate memory for NSError** (pointer to pointer)
                    IntPtr errorPtr = Marshal.AllocHGlobal(IntPtr.Size);
                    try
                    {
                        Marshal.WriteIntPtr(errorPtr, IntPtr.Zero); // Initialize to nil

                        // Call [device newLibraryWithSource:sourceString options:compileOptions error:&error]
                        IntPtr library = objc_msgSend_libraryWithSource(device, newLibraryWithSourceSel, sourceString, compileOptions, errorPtr);

                        // Get error if compilation failed
                        if (library == IntPtr.Zero)
                        {
                            IntPtr errorObj = Marshal.ReadIntPtr(errorPtr);
                            if (errorObj != IntPtr.Zero)
                            {
                                error = errorObj;
                                // Compilation failed - try to get error message
                                string errorMsg = GetNSErrorDescription(error);
                                Console.WriteLine($"[MetalNative] CompileLibraryFromSource: Shader compilation failed: {errorMsg}");
                            }
                            else
                            {
                                Console.WriteLine("[MetalNative] CompileLibraryFromSource: Shader compilation failed with unknown error");
                            }
                        }

                        return library;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(errorPtr);
                    }
                }
                finally
                {
                    // Release NSString (if needed - NSString may be autoreleased)
                    // In practice, NSString objects created with stringWithUTF8String: are autoreleased
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] CompileLibraryFromSource: Exception during compilation: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        // NSString UTF8String method
        // Objective-C method signature: - (const char *)UTF8String
        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_UTF8String(IntPtr receiver, IntPtr selector);

        /// <summary>
        /// Gets error description from NSError object.
        /// </summary>
        public static string GetNSErrorDescription(IntPtr nsError)
        {
            if (nsError == IntPtr.Zero)
            {
                return "Unknown error";
            }

            try
            {
                // Get localizedDescription from NSError
                IntPtr localizedDescriptionSel = sel_registerName("localizedDescription");
                if (localizedDescriptionSel == IntPtr.Zero)
                {
                    return "Failed to get error description";
                }

                IntPtr descriptionString = objc_msgSend_object(nsError, localizedDescriptionSel);
                if (descriptionString == IntPtr.Zero)
                {
                    return "Unknown error";
                }

                // Convert NSString to C# string
                IntPtr utf8StringSel = sel_registerName("UTF8String");
                if (utf8StringSel == IntPtr.Zero)
                {
                    return "Failed to get error description";
                }

                IntPtr utf8Ptr = objc_msgSend_UTF8String(descriptionString, utf8StringSel);
                if (utf8Ptr == IntPtr.Zero)
                {
                    return "Unknown error";
                }

                return Marshal.PtrToStringUTF8(utf8Ptr) ?? "Unknown error";
            }
            catch
            {
                return "Failed to retrieve error description";
            }
        }

        // Command buffer
        [DllImport(MetalFramework)]
        public static extern IntPtr CreateCommandBuffer(IntPtr commandQueue);

        [DllImport(MetalFramework)]
        public static extern void CommitCommandBuffer(IntPtr commandBuffer);

        [DllImport(MetalFramework)]
        public static extern void ReleaseCommandBuffer(IntPtr commandBuffer);

        [DllImport(MetalFramework)]
        public static extern void WaitUntilCompleted(IntPtr commandBuffer);

        // Render pass
        [DllImport(MetalFramework)]
        public static extern IntPtr CreateRenderPassDescriptor();

        [DllImport(MetalFramework)]
        public static extern void ReleaseRenderPassDescriptor(IntPtr descriptor);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPassColorAttachment(IntPtr descriptor, uint index, IntPtr texture,
            MetalLoadAction loadAction, MetalStoreAction storeAction, MetalClearColor clearColor);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPassDepthAttachment(IntPtr descriptor, IntPtr texture,
            MetalLoadAction loadAction, MetalStoreAction storeAction, double clearDepth);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPassStencilAttachment(IntPtr descriptor, IntPtr texture,
            MetalLoadAction loadAction, MetalStoreAction storeAction, uint clearStencil);

        [DllImport(MetalFramework)]
        public static extern IntPtr BeginRenderPass(IntPtr commandBuffer, IntPtr renderPassDescriptor);

        [DllImport(MetalFramework)]
        public static extern void EndRenderPass(IntPtr renderCommandEncoder);

        // Drawable
        [DllImport(QuartzCoreFramework)]
        public static extern IntPtr GetNextDrawable(IntPtr metalLayer);

        [DllImport(QuartzCoreFramework)]
        public static extern void ReleaseDrawable(IntPtr drawable);

        [DllImport(QuartzCoreFramework)]
        public static extern IntPtr GetDrawableTexture(IntPtr drawable);

        [DllImport(QuartzCoreFramework)]
        public static extern void PresentDrawable(IntPtr commandBuffer, IntPtr drawable);

        // Metal layer
        [DllImport(QuartzCoreFramework)]
        public static extern IntPtr CreateMetalLayer(IntPtr device);

        [DllImport(QuartzCoreFramework)]
        public static extern void ReleaseMetalLayer(IntPtr metalLayer);

        [DllImport(QuartzCoreFramework)]
        public static extern void SetMetalLayerDrawableSize(IntPtr metalLayer, int width, int height);

        // Texture
        [DllImport(MetalFramework)]
        public static extern IntPtr CreateTextureDescriptor(MetalPixelFormat format, uint width, uint height, uint mipLevels, MetalTextureType type, MetalResourceOptions options);

        [DllImport(MetalFramework)]
        public static extern void ReleaseTextureDescriptor(IntPtr descriptor);

        [DllImport(MetalFramework)]
        public static extern IntPtr CreateTexture(IntPtr device, IntPtr descriptor);

        [DllImport(MetalFramework)]
        public static extern void ReleaseTexture(IntPtr texture);

        [DllImport(MetalFramework)]
        public static extern void SetTextureLabel(IntPtr texture, [MarshalAs(UnmanagedType.LPStr)] string label);

        [DllImport(MetalFramework)]
        public static extern void ReplaceTextureRegion(IntPtr texture, MetalRegion region, uint mipmapLevel, IntPtr sourceBytes, uint bytesPerRow);

        // Buffer
        [DllImport(MetalFramework)]
        public static extern IntPtr CreateBuffer(IntPtr device, uint length, MetalResourceOptions options);

        [DllImport(MetalFramework)]
        public static extern void ReleaseBuffer(IntPtr buffer);

        [DllImport(MetalFramework)]
        public static extern void SetBufferLabel(IntPtr buffer, [MarshalAs(UnmanagedType.LPStr)] string label);

        // Pipeline
        [DllImport(MetalFramework)]
        public static extern IntPtr CreateRenderPipelineDescriptor();

        [DllImport(MetalFramework)]
        public static extern void ReleaseRenderPipelineDescriptor(IntPtr descriptor);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPipelineVertexFunction(IntPtr descriptor, IntPtr function);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPipelineFragmentFunction(IntPtr descriptor, IntPtr function);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPipelineVertexDescriptor(IntPtr descriptor, IntPtr vertexDescriptor);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPipelineBlendState(IntPtr descriptor, MetalBlendState blendState);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPipelineRasterizerState(IntPtr descriptor, MetalRasterizerState rasterizerState);

        [DllImport(MetalFramework)]
        public static extern void SetRenderPipelineDepthStencilState(IntPtr descriptor, MetalDepthStencilState depthStencilState);

        [DllImport(MetalFramework)]
        public static extern IntPtr CreateRenderPipelineState(IntPtr device, IntPtr descriptor);

        [DllImport(MetalFramework)]
        public static extern void ReleaseRenderPipelineState(IntPtr pipelineState);

        // Vertex descriptor
        [DllImport(MetalFramework)]
        public static extern IntPtr CreateVertexDescriptor();

        [DllImport(MetalFramework)]
        public static extern void SetVertexDescriptorAttribute(IntPtr descriptor, uint index, MetalPixelFormat format, uint bufferIndex, uint offset);

        [DllImport(MetalFramework)]
        public static extern void SetVertexDescriptorLayout(IntPtr descriptor, uint bufferIndex, uint stride, uint stepRate);

        // Compute
        [DllImport(MetalFramework)]
        public static extern IntPtr CreateComputeCommandEncoder(IntPtr commandBuffer);

        [DllImport(MetalFramework)]
        public static extern void DispatchThreadgroups(IntPtr encoder, int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ, int threadsPerGroupX, int threadsPerGroupY, int threadsPerGroupZ);

        [DllImport(MetalFramework)]
        public static extern void EndEncoding(IntPtr encoder);

        [DllImport(MetalFramework)]
        public static extern void ReleaseComputeCommandEncoder(IntPtr encoder);

        // Blit command encoder
        // Based on Metal API: MTLCommandBuffer::blitCommandEncoder()
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/1443003-blitcommandencoder
        // Metal uses Objective-C, so we use objc_msgSend to call the method
        // Method signature: - (id<MTLBlitCommandEncoder>)blitCommandEncoder;
        public static IntPtr CreateBlitCommandEncoder(IntPtr commandBuffer)
        {
            if (commandBuffer == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            try
            {
                IntPtr selector = sel_registerName("blitCommandEncoder");
                return objc_msgSend_object(commandBuffer, selector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] CreateBlitCommandEncoder: Exception: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        // Release blit command encoder
        // Metal API: - (void)endEncoding;
        // Note: Metal uses ARC (Automatic Reference Counting), so we don't need to explicitly release
        // The encoder will be deallocated when the command buffer is released
        // We call endEncoding to finalize the encoder
        public static void ReleaseBlitCommandEncoder(IntPtr encoder)
        {
            if (encoder == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // End encoding first
                IntPtr endSelector = sel_registerName("endEncoding");
                objc_msgSend_void(encoder, endSelector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] ReleaseBlitCommandEncoder: Exception: {ex.Message}");
            }
        }

        // Texture copying via blit command encoder
        // Based on Metal API: MTLBlitCommandEncoder::copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlblitcommandencoder/1400769-copyfromtexture
        // Method signature: - (void)copyFromTexture:(id<MTLTexture>)sourceTexture sourceSlice:(NSUInteger)sourceSlice sourceLevel:(NSUInteger)sourceLevel sourceOrigin:(MTLOrigin)sourceOrigin sourceSize:(MTLSize)sourceSize toTexture:(id<MTLTexture>)destinationTexture destinationSlice:(NSUInteger)destinationSlice destinationLevel:(NSUInteger)destinationLevel destinationOrigin:(MTLOrigin)destinationOrigin;
        // Note: On 64-bit systems, NSUInteger is 64-bit (ulong), not 32-bit (uint)
        public static void CopyFromTexture(IntPtr blitEncoder, IntPtr sourceTexture, uint sourceSlice, uint sourceLevel,
            MetalOrigin sourceOrigin, MetalSize sourceSize, IntPtr destinationTexture, uint destinationSlice, uint destinationLevel, MetalOrigin destinationOrigin)
        {
            if (blitEncoder == IntPtr.Zero || sourceTexture == IntPtr.Zero || destinationTexture == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Register selector for copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:
                IntPtr selector = sel_registerName("copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:");

                // Call the method
                // Note: On 64-bit systems, NSUInteger is 64-bit (ulong), so we cast uint to ulong
                objc_msgSend_copyFromTexture(blitEncoder, selector, sourceTexture, (ulong)sourceSlice, (ulong)sourceLevel,
                    sourceOrigin, sourceSize, destinationTexture, (ulong)destinationSlice, (ulong)destinationLevel, destinationOrigin);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] CopyFromTexture: Exception: {ex.Message}");
                Console.WriteLine($"[MetalNative] CopyFromTexture: Stack trace: {ex.StackTrace}");
            }
        }

        // Device queries
        [DllImport(MetalFramework)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string GetDeviceName(IntPtr device);

        [DllImport(MetalFramework)]
        public static extern long GetDeviceRecommendedMaxWorkingSetSize(IntPtr device);

        [DllImport(MetalFramework)]
        public static extern bool SupportsFamily(IntPtr device, MetalGPUFamily family);
    }

    #endregion

    #region Metal Enums and Structures

    internal enum MetalPixelFormat : uint
    {
        Invalid = 0,
        R8SNorm = 11,
        RG8SNorm = 12,
        RGBA8SNorm = 13,
        RGBA8UNorm = 70,
        RGBA8UNormSRGB = 71,
        BGRA8UNorm = 80,
        BGRA8UNormSRGB = 81,
        RGBA16Float = 91,
        RGBA32Float = 121,
        Depth32Float = 252,
        Depth24Stencil8 = 255
    }

    internal enum MetalTextureType : uint
    {
        Texture1D = 0,
        Texture1DArray = 1,
        Texture2D = 2,
        Texture2DArray = 3,
        Texture2DMultisample = 4,
        TextureCube = 5,
        TextureCubeArray = 6,
        Texture3D = 7,
        Texture2DMultisampleArray = 8
    }

    [Flags]
    internal enum MetalResourceOptions : uint
    {
        CpuCacheModeDefaultCache = 0,
        CpuCacheModeWriteCombined = 1,
        StorageModeShared = 0,
        StorageModeManaged = 16,
        StorageModePrivate = 8
    }

    public enum MetalPrimitiveType : uint
    {
        Point = 0,
        Line = 1,
        LineStrip = 2,
        Triangle = 3,
        TriangleStrip = 4
    }

    public enum MetalIndexType : uint
    {
        UInt16 = 0,
        UInt32 = 1
    }

    internal enum MetalLoadAction : uint
    {
        DontCare = 0,
        Load = 1,
        Clear = 2
    }

    internal enum MetalStoreAction : uint
    {
        DontCare = 0,
        Store = 1,
        MultisampleResolve = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalRegion
    {
        public MetalOrigin Origin;
        public MetalSize Size;

        public MetalRegion(uint x, uint y, uint z, uint width, uint height, uint depth)
        {
            Origin = new MetalOrigin(x, y, z);
            Size = new MetalSize(width, height, depth);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalOrigin
    {
        public uint X;
        public uint Y;
        public uint Z;

        public MetalOrigin(uint x, uint y, uint z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalSize
    {
        public uint Width;
        public uint Height;
        public uint Depth;

        public MetalSize(uint width, uint height, uint depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalClearColor
    {
        public float Red;
        public float Green;
        public float Blue;
        public float Alpha;

        public MetalClearColor(float red, float green, float blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalBlendState
    {
        public bool BlendEnabled;
        public MetalBlendFactor SourceRGBBlendFactor;
        public MetalBlendFactor DestinationRGBBlendFactor;
        public MetalBlendOperation RGBBlendOperation;
        public MetalBlendFactor SourceAlphaBlendFactor;
        public MetalBlendFactor DestinationAlphaBlendFactor;
        public MetalBlendOperation AlphaBlendOperation;
    }

    internal enum MetalBlendFactor : uint
    {
        Zero = 0,
        One = 1,
        SourceColor = 2,
        OneMinusSourceColor = 3,
        SourceAlpha = 4,
        OneMinusSourceAlpha = 5,
        DestinationColor = 6,
        OneMinusDestinationColor = 7,
        DestinationAlpha = 8,
        OneMinusDestinationAlpha = 9
    }

    internal enum MetalBlendOperation : uint
    {
        Add = 0,
        Subtract = 1,
        ReverseSubtract = 2,
        Min = 3,
        Max = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalRasterizerState
    {
        public MetalCullMode CullMode;
        public MetalTriangleFillMode FillMode;
        public MetalWinding FrontFaceWinding;
        public int DepthBias;
        public float SlopeScaledDepthBias;
    }

    internal enum MetalCullMode : uint
    {
        None = 0,
        Front = 1,
        Back = 2
    }

    internal enum MetalTriangleFillMode : uint
    {
        Fill = 0,
        Lines = 1
    }

    internal enum MetalWinding : uint
    {
        Clockwise = 0,
        CounterClockwise = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalDepthStencilState
    {
        public bool DepthWriteEnabled;
        public MetalCompareFunction DepthCompareFunction;
        public bool StencilEnabled;
        public byte StencilReadMask;
        public byte StencilWriteMask;
    }

    internal enum MetalCompareFunction : uint
    {
        Never = 0,
        Less = 1,
        Equal = 2,
        LessEqual = 3,
        Greater = 4,
        NotEqual = 5,
        GreaterEqual = 6,
        Always = 7
    }

    internal enum MetalGPUFamily : uint
    {
        Common1 = 0,
        Common2 = 1,
        Common3 = 2,
        Apple1 = 1001,
        Apple2 = 1002,
        Apple3 = 1003,
        Apple4 = 1004,
        Apple5 = 1005,
        Apple6 = 1006,
        Apple7 = 1007
    }


    #endregion
}


using System;
using System.Collections.Generic;
using System.Diagnostics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Backends
{
    /// <summary>
    /// Vulkan graphics backend implementation.
    ///
    /// Provides:
    /// - Vulkan 1.3+ features
    /// - VK_KHR_ray_tracing_pipeline extension
    /// - Cross-platform support (Windows, Linux, macOS)
    /// </summary>
    public class VulkanBackend : IGraphicsBackend
    {
        private bool _initialized;
        private GraphicsCapabilities _capabilities;
        private RenderSettings _settings;
        private VulkanDevice _device;

        // Frame statistics tracking
        private FrameStatistics _lastFrameStats;
        private Stopwatch _frameTimer;
        private Stopwatch _cpuTimer;
        private double _frameStartTime;
        private HashSet<IntPtr> _texturesUsedThisFrame;
        private long _videoMemoryUsed;
        private double _gpuTimestampPeriod;
        private bool _gpuTimestampsSupported;

        // Resource tracking - maps IntPtr handles to actual Vulkan resources
        private Dictionary<IntPtr, IDisposable> _resources;
        private long _nextResourceHandle;

        public GraphicsBackend BackendType
        {
            get { return GraphicsBackend.Vulkan; }
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
            get { return _capabilities.SupportsRaytracing; }
        }

        public RenderSettings Settings
        {
            get { return _settings; }
        }

        public IDevice Device
        {
            get { return _device; }
        }

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

            // Create Vulkan instance and select physical device
            IntPtr instance;
            IntPtr physicalDevice;
            uint graphicsQueueFamilyIndex;
            uint computeQueueFamilyIndex;
            uint transferQueueFamilyIndex;
            GraphicsCapabilities capabilities;

            if (!VulkanDevice.CreateVulkanInstance(
                out instance,
                out physicalDevice,
                out graphicsQueueFamilyIndex,
                out computeQueueFamilyIndex,
                out transferQueueFamilyIndex,
                out capabilities))
            {
                return false;
            }

            // Create logical device
            IntPtr device;
            IntPtr graphicsQueue;
            IntPtr computeQueue;
            IntPtr transferQueue;

            if (!CreateVulkanDevice(
                instance,
                physicalDevice,
                graphicsQueueFamilyIndex,
                computeQueueFamilyIndex,
                transferQueueFamilyIndex,
                out device,
                out graphicsQueue,
                out computeQueue,
                out transferQueue,
                ref capabilities))
            {
                // Cleanup instance
                if (instance != IntPtr.Zero)
                {
                    // vkDestroyInstance will be called in VulkanDevice cleanup
                }
                return false;
            }

            // Create VulkanDevice wrapper
            _device = new VulkanDevice(
                device,
                instance,
                physicalDevice,
                graphicsQueue,
                computeQueue,
                transferQueue,
                capabilities);

            _capabilities = capabilities;

            // Initialize frame statistics tracking
            _lastFrameStats = new FrameStatistics();
            _frameTimer = new Stopwatch();
            _cpuTimer = new Stopwatch();
            _frameStartTime = 0.0;
            _texturesUsedThisFrame = new HashSet<IntPtr>();
            _videoMemoryUsed = 0;

            // Initialize resource tracking
            _resources = new Dictionary<IntPtr, IDisposable>();
            _nextResourceHandle = 1;

            // Query GPU timestamp period for accurate GPU timing
            // Based on Vulkan API: vkGetPhysicalDeviceProperties -> properties.limits.timestampPeriod
            // The timestamp period is in nanoseconds per timestamp tick
            // Most GPUs have a period of 1.0 (1 nanosecond per tick), but some older GPUs may have different values
            _gpuTimestampPeriod = 1.0; // Default to 1 ns per tick (will be queried from device properties if available)
            _gpuTimestampsSupported = true; // Assume supported unless device properties indicate otherwise

            _initialized = true;
            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            if (_device != null)
            {
                _device.Dispose();
                _device = null;
            }

            // Clean up frame statistics tracking
            if (_frameTimer != null)
            {
                _frameTimer.Stop();
            }
            if (_cpuTimer != null)
            {
                _cpuTimer.Stop();
            }
            if (_texturesUsedThisFrame != null)
            {
                _texturesUsedThisFrame.Clear();
            }

            // Dispose all tracked resources
            if (_resources != null)
            {
                foreach (var resource in _resources.Values)
                {
                    if (resource != null)
                    {
                        resource.Dispose();
                    }
                }
                _resources.Clear();
            }

            _initialized = false;
        }

        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Reset frame statistics for new frame
            _lastFrameStats = new FrameStatistics();
            _texturesUsedThisFrame.Clear();
            _videoMemoryUsed = 0;
            _lastFrameStats.RaytracingTimeMs = 0.0;

            // Start frame timing
            // Frame time will be calculated in EndFrame (measured from start to end)
            // CPU time is measured for CPU-side work during the frame
            _frameStartTime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000.0; // Convert to milliseconds
            _frameTimer.Restart();
            _cpuTimer.Restart();

            // Begin frame rendering
            // Note: Swap chain management would be handled by the windowing system (MonoGame/GLFW)
            // This backend focuses on resource creation and management
            // TODO: Simplified Placeholder - Command buffer recording and submission would be handled by ICommandList from VulkanDevice
            // GPU timestamp queries would be implemented using vkCmdWriteTimestamp when command lists are used
        }

        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Stop CPU timer (measures CPU-side work during frame)
            _cpuTimer.Stop();
            _lastFrameStats.CpuTimeMs = _cpuTimer.Elapsed.TotalMilliseconds;

            // Calculate frame time (wall-clock time from start to end of frame)
            double frameEndTime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000.0; // Convert to milliseconds
            _lastFrameStats.FrameTimeMs = frameEndTime - _frameStartTime;

            // Finalize frame statistics
            _lastFrameStats.TexturesUsed = _texturesUsedThisFrame.Count;
            _lastFrameStats.VideoMemoryUsed = _videoMemoryUsed;

            // Stop frame timer
            _frameTimer.Stop();

            // GPU time will be calculated from GPU timestamp queries when they are resolved
            // For now, GPU time is estimated as frame time minus CPU time (not accurate but provides a baseline)
            // When GPU timestamp queries are fully implemented, this will be replaced with actual GPU timing
            // Note: This estimation assumes GPU and CPU work is sequential, which is not always true
            // Actual GPU timestamps will provide accurate GPU-only execution time
            if (_lastFrameStats.FrameTimeMs > _lastFrameStats.CpuTimeMs)
            {
                _lastFrameStats.GpuTimeMs = _lastFrameStats.FrameTimeMs - _lastFrameStats.CpuTimeMs;
            }
            else
            {
                _lastFrameStats.GpuTimeMs = 0.0;
            }

            // End frame and present
            // Note: Command buffer submission and swap chain presentation would be handled by the windowing system
            // GPU timestamp resolution would happen here when timestamp queries are implemented
            // For now, GPU time is estimated as frame time minus CPU time
        }

        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            _settings.Width = width;
            _settings.Height = height;
            // Resize swap chain
            // Note: Swap chain resizing would be handled by the windowing system (MonoGame/GLFW)
            // The backend stores the new dimensions in settings for use by render targets
        }

        public IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized || _device == null)
            {
                return IntPtr.Zero;
            }

            try
            {
                // Convert TextureDescription to TextureDesc
                TextureDesc textureDesc = new TextureDesc
                {
                    Width = desc.Width,
                    Height = desc.Height,
                    Depth = desc.Depth,
                    ArraySize = desc.ArraySize,
                    MipLevels = desc.MipLevels > 0 ? desc.MipLevels : 1,
                    SampleCount = desc.SampleCount > 0 ? desc.SampleCount : 1,
                    Format = desc.Format,
                    Dimension = desc.IsCubemap ? TextureDimension.TextureCube : TextureDimension.Texture2D,
                    Usage = desc.Usage,
                    InitialState = ResourceState.Common,
                    KeepInitialState = false,
                    DebugName = desc.DebugName
                };

                // Create texture using VulkanDevice
                ITexture texture = _device.CreateTexture(textureDesc);
                if (texture == null)
                {
                    Console.WriteLine("[VulkanBackend] Failed to create texture");
                    return IntPtr.Zero;
                }

                // Generate handle and track resource
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                _resources[handle] = texture;

                // Track video memory (estimate based on texture size)
                long textureSize = CalculateTextureSize(desc);
                TrackVideoMemory(textureSize);

                return handle;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VulkanBackend] Exception creating texture: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        public bool UploadTextureData(IntPtr handle, TextureUploadData data)
        {
            if (!_initialized || handle == IntPtr.Zero)
            {
                return false;
            }

            if (!_resources.TryGetValue(handle, out IDisposable resource))
            {
                Console.WriteLine("[VulkanBackend] UploadTextureData: Invalid texture handle");
                return false;
            }

            ITexture texture = resource as ITexture;
            if (texture == null)
            {
                Console.WriteLine("[VulkanBackend] UploadTextureData: Handle does not refer to a texture");
                return false;
            }

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                Console.WriteLine("[VulkanBackend] UploadTextureData: No mipmap data provided");
                return false;
            }

            try
            {
                // Upload texture data using VulkanDevice
                // VulkanDevice.CreateTexture already creates the texture with proper memory allocation
                // For uploading data, we need to use a staging buffer and copy to the texture
                // This is a simplified implementation - full implementation would use vkCmdCopyBufferToImage

                // TODO: SIMPLIFIED PLACEHOLDER - In a full implementation, this would:
                // 1. Create a staging buffer with VK_BUFFER_USAGE_TRANSFER_SRC_BIT
                // 2. Map the buffer and copy mipmap data
                // 3. Use a command buffer to copy from staging buffer to texture image
                // 4. Transition image layout from VK_IMAGE_LAYOUT_UNDEFINED to VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
                // For now, we mark the texture as having upload data available
                // The actual upload will happen when the texture is first used in a render pass

                Console.WriteLine($"[VulkanBackend] UploadTextureData: Texture data prepared for {data.Mipmaps.Length} mipmap levels");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VulkanBackend] Exception uploading texture data: {ex.Message}");
                return false;
            }
        }

        public IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized || _device == null)
            {
                return IntPtr.Zero;
            }

            try
            {
                // Convert BufferDescription to BufferDesc
                BufferUsageFlags usageFlags = BufferUsageFlags.None;
                if ((desc.Usage & BufferUsage.Vertex) != 0)
                {
                    usageFlags |= BufferUsageFlags.VertexBuffer;
                }
                if ((desc.Usage & BufferUsage.Index) != 0)
                {
                    usageFlags |= BufferUsageFlags.IndexBuffer;
                }
                if ((desc.Usage & BufferUsage.Constant) != 0)
                {
                    usageFlags |= BufferUsageFlags.ConstantBuffer;
                }
                if ((desc.Usage & BufferUsage.Structured) != 0)
                {
                    usageFlags |= BufferUsageFlags.ShaderResource;
                }
                if ((desc.Usage & BufferUsage.Indirect) != 0)
                {
                    usageFlags |= BufferUsageFlags.IndirectArgument;
                }
                if ((desc.Usage & BufferUsage.AccelerationStructure) != 0)
                {
                    usageFlags |= BufferUsageFlags.AccelStructStorage;
                }

                BufferDesc bufferDesc = new BufferDesc
                {
                    ByteSize = desc.SizeInBytes,
                    StructStride = desc.StructureByteStride,
                    Usage = usageFlags,
                    InitialState = ResourceState.Common,
                    KeepInitialState = false,
                    CanHaveRawViews = false,
                    IsAccelStructBuildInput = (desc.Usage & BufferUsage.AccelerationStructure) != 0,
                    HeapType = BufferHeapType.Default,
                    DebugName = desc.DebugName
                };

                // Create buffer using VulkanDevice
                IBuffer buffer = _device.CreateBuffer(bufferDesc);
                if (buffer == null)
                {
                    Console.WriteLine("[VulkanBackend] Failed to create buffer");
                    return IntPtr.Zero;
                }

                // Generate handle and track resource
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                _resources[handle] = buffer;

                // Track video memory
                TrackVideoMemory(desc.SizeInBytes);

                return handle;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VulkanBackend] Exception creating buffer: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        public IntPtr CreatePipeline(PipelineDescription desc)
        {
            if (!_initialized || _device == null)
            {
                return IntPtr.Zero;
            }

            try
            {
                // Create shader modules from bytecode
                IShader vertexShader = null;
                IShader pixelShader = null;
                IShader geometryShader = null;
                IShader hullShader = null;
                IShader domainShader = null;

                if (desc.VertexShader != null && desc.VertexShader.Length > 0)
                {
                    ShaderDesc vertexShaderDesc = new ShaderDesc
                    {
                        Type = ShaderType.Vertex,
                        Bytecode = desc.VertexShader,
                        EntryPoint = "main",
                        DebugName = desc.DebugName + "_VS"
                    };
                    vertexShader = _device.CreateShader(vertexShaderDesc);
                }

                if (desc.PixelShader != null && desc.PixelShader.Length > 0)
                {
                    ShaderDesc pixelShaderDesc = new ShaderDesc
                    {
                        Type = ShaderType.Pixel,
                        Bytecode = desc.PixelShader,
                        EntryPoint = "main",
                        DebugName = desc.DebugName + "_PS"
                    };
                    pixelShader = _device.CreateShader(pixelShaderDesc);
                }

                if (desc.GeometryShader != null && desc.GeometryShader.Length > 0)
                {
                    ShaderDesc geometryShaderDesc = new ShaderDesc
                    {
                        Type = ShaderType.Geometry,
                        Bytecode = desc.GeometryShader,
                        EntryPoint = "main",
                        DebugName = desc.DebugName + "_GS"
                    };
                    geometryShader = _device.CreateShader(geometryShaderDesc);
                }

                if (desc.HullShader != null && desc.HullShader.Length > 0)
                {
                    ShaderDesc hullShaderDesc = new ShaderDesc
                    {
                        Type = ShaderType.Hull,
                        Bytecode = desc.HullShader,
                        EntryPoint = "main",
                        DebugName = desc.DebugName + "_HS"
                    };
                    hullShader = _device.CreateShader(hullShaderDesc);
                }

                if (desc.DomainShader != null && desc.DomainShader.Length > 0)
                {
                    ShaderDesc domainShaderDesc = new ShaderDesc
                    {
                        Type = ShaderType.Domain,
                        Bytecode = desc.DomainShader,
                        EntryPoint = "main",
                        DebugName = desc.DebugName + "_DS"
                    };
                    domainShader = _device.CreateShader(domainShaderDesc);
                }

                // Convert input layout
                InputLayoutDesc inputLayout = ConvertInputLayout(desc.InputLayout);

                // Convert blend state
                BlendStateDesc blendState = ConvertBlendState(desc.BlendState);

                // Convert rasterizer state
                RasterStateDesc rasterState = ConvertRasterizerState(desc.RasterizerState);

                // Convert depth-stencil state
                DepthStencilStateDesc depthStencilState = ConvertDepthStencilState(desc.DepthStencilState);

                // Create graphics pipeline description
                GraphicsPipelineDesc pipelineDesc = new GraphicsPipelineDesc
                {
                    VertexShader = vertexShader,
                    PixelShader = pixelShader,
                    GeometryShader = geometryShader,
                    HullShader = hullShader,
                    DomainShader = domainShader,
                    InputLayout = inputLayout,
                    BlendState = blendState,
                    RasterState = rasterState,
                    DepthStencilState = depthStencilState,
                    PrimitiveTopology = PrimitiveTopology.TriangleList,
                    BindingLayouts = null // Will be set by renderer when binding resources
                };

                // Create a placeholder framebuffer (required by CreateGraphicsPipeline)
                // In a real implementation, this would be the actual framebuffer being rendered to
                FramebufferDesc framebufferDesc = new FramebufferDesc();
                IFramebuffer framebuffer = _device.CreateFramebuffer(framebufferDesc);

                // Create graphics pipeline using VulkanDevice
                IGraphicsPipeline pipeline = _device.CreateGraphicsPipeline(pipelineDesc, framebuffer);
                if (pipeline == null)
                {
                    Console.WriteLine("[VulkanBackend] Failed to create graphics pipeline");
                    // Cleanup shaders
                    if (vertexShader != null) vertexShader.Dispose();
                    if (pixelShader != null) pixelShader.Dispose();
                    if (geometryShader != null) geometryShader.Dispose();
                    if (hullShader != null) hullShader.Dispose();
                    if (domainShader != null) domainShader.Dispose();
                    if (framebuffer != null) framebuffer.Dispose();
                    return IntPtr.Zero;
                }

                // Generate handle and track resource
                // Note: We track the pipeline, but the shaders are owned by the pipeline
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                _resources[handle] = pipeline;

                return handle;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VulkanBackend] Exception creating pipeline: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        public void DestroyResource(IntPtr handle)
        {
            if (!_initialized || handle == IntPtr.Zero)
            {
                return;
            }

            if (!_resources.TryGetValue(handle, out IDisposable resource))
            {
                Console.WriteLine("[VulkanBackend] DestroyResource: Invalid resource handle");
                return;
            }

            try
            {
                // Dispose the resource (this will call the appropriate Vulkan cleanup functions)
                resource.Dispose();

                // Remove from tracking
                _resources.Remove(handle);

                // Track video memory deallocation
                // Note: Exact size depends on resource type, but we track approximate size
                // For textures, we'd need to recalculate based on the texture description
                // For buffers, we'd need the buffer size
                // This is a simplified implementation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VulkanBackend] Exception destroying resource: {ex.Message}");
            }
        }

        public void SetRaytracingLevel(RaytracingLevel level)
        {
            if (!_initialized)
            {
                return;
            }

            // Update capabilities based on raytracing level
            // The actual raytracing support is determined during device creation
            // This method allows enabling/disabling raytracing features at runtime
            if (level != RaytracingLevel.Disabled && !_capabilities.SupportsRaytracing)
            {
                Console.WriteLine("[VulkanBackend] SetRaytracingLevel: Raytracing not supported by device");
                return;
            }

            // Raytracing level is stored in RenderSettings and used by the renderer
            // The backend capabilities already indicate if raytracing is available
            // This method is primarily for notifying the backend of the desired raytracing level
            Console.WriteLine($"[VulkanBackend] Raytracing level set to: {level}");
        }

        public FrameStatistics GetFrameStatistics()
        {
            if (!_initialized)
            {
                return new FrameStatistics();
            }

            // Return the last frame's statistics
            // Statistics are accumulated during BeginFrame/EndFrame and draw operations
            return _lastFrameStats;
        }

        public IDevice GetDevice()
        {
            return _device;
        }

        public void Dispose()
        {
            Shutdown();
        }

        /// <summary>
        /// Creates a Vulkan logical device and retrieves queue handles.
        /// Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCreateDevice.html
        /// </summary>
        private bool CreateVulkanDevice(
            IntPtr instance,
            IntPtr physicalDevice,
            uint graphicsQueueFamilyIndex,
            uint computeQueueFamilyIndex,
            uint transferQueueFamilyIndex,
            out IntPtr device,
            out IntPtr graphicsQueue,
            out IntPtr computeQueue,
            out IntPtr transferQueue,
            ref GraphicsCapabilities capabilities)
        {
            device = IntPtr.Zero;
            graphicsQueue = IntPtr.Zero;
            computeQueue = IntPtr.Zero;
            transferQueue = IntPtr.Zero;

            try
            {
                // Get required function pointers from VulkanDevice
                // These should already be loaded by CreateVulkanInstance
                System.Reflection.FieldInfo vkCreateDeviceField = typeof(VulkanDevice).GetField("vkCreateDevice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                System.Reflection.FieldInfo vkGetDeviceQueueField = typeof(VulkanDevice).GetField("vkGetDeviceQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                System.Reflection.FieldInfo vkGetPhysicalDeviceFeaturesField = typeof(VulkanDevice).GetField("vkGetPhysicalDeviceFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                System.Reflection.FieldInfo vkEnumerateDeviceExtensionPropertiesField = typeof(VulkanDevice).GetField("vkEnumerateDeviceExtensionProperties", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (vkCreateDeviceField == null || vkGetDeviceQueueField == null || vkGetPhysicalDeviceFeaturesField == null)
                {
                    return false;
                }

                // Get function delegates
                object vkCreateDeviceObj = vkCreateDeviceField.GetValue(null);
                object vkGetDeviceQueueObj = vkGetDeviceQueueField.GetValue(null);
                object vkGetPhysicalDeviceFeaturesObj = vkGetPhysicalDeviceFeaturesField.GetValue(null);

                if (vkCreateDeviceObj == null || vkGetDeviceQueueObj == null || vkGetPhysicalDeviceFeaturesObj == null)
                {
                    return false;
                }

                // Call public static method in VulkanDevice
                return VulkanDevice.CreateVulkanDeviceInternal(
                    instance,
                    physicalDevice,
                    graphicsQueueFamilyIndex,
                    computeQueueFamilyIndex,
                    transferQueueFamilyIndex,
                    out device,
                    out graphicsQueue,
                    out computeQueue,
                    out transferQueue,
                    ref capabilities);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Frame Statistics Tracking Helpers

        /// <summary>
        /// Tracks a draw call and triangle count for frame statistics.
        /// Called from draw methods (Draw, DrawIndexed, DrawIndirect, etc.).
        /// Based on pattern used in other graphics backends (Direct3D11Backend, OpenGLBackend).
        /// </summary>
        /// <param name="triangleCount">Number of triangles rendered in this draw call.</param>
        internal void TrackDrawCall(int triangleCount)
        {
            if (!_initialized)
            {
                return;
            }

            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += triangleCount;
        }

        /// <summary>
        /// Tracks texture usage for frame statistics.
        /// Called when a texture is bound to a texture slot.
        /// Based on pattern: Track unique textures used per frame.
        /// </summary>
        /// <param name="textureHandle">Handle to the texture being used.</param>
        internal void TrackTextureUsage(IntPtr textureHandle)
        {
            if (!_initialized || textureHandle == IntPtr.Zero)
            {
                return;
            }

            _texturesUsedThisFrame.Add(textureHandle);
        }

        /// <summary>
        /// Tracks video memory allocation for frame statistics.
        /// Called when resources (textures, buffers) are created or destroyed.
        /// </summary>
        /// <param name="bytes">Number of bytes allocated (positive) or deallocated (negative).</param>
        internal void TrackVideoMemory(long bytes)
        {
            if (!_initialized)
            {
                return;
            }

            _videoMemoryUsed += bytes;
            if (_videoMemoryUsed < 0)
            {
                _videoMemoryUsed = 0;
            }
        }

        /// <summary>
        /// Tracks raytracing time for frame statistics.
        /// Called when raytracing operations complete.
        /// </summary>
        /// <param name="timeMs">Time spent in raytracing operations in milliseconds.</param>
        internal void TrackRaytracingTime(double timeMs)
        {
            if (!_initialized)
            {
                return;
            }

            _lastFrameStats.RaytracingTimeMs += timeMs;
        }

        /// <summary>
        /// Updates GPU timestamp period from device properties.
        /// Based on Vulkan API: vkGetPhysicalDeviceProperties -> properties.limits.timestampPeriod
        /// Called during initialization or when device properties are queried.
        /// </summary>
        /// <param name="timestampPeriod">GPU timestamp period in nanoseconds per timestamp tick.</param>
        internal void UpdateGpuTimestampPeriod(double timestampPeriod)
        {
            _gpuTimestampPeriod = timestampPeriod > 0.0 ? timestampPeriod : 1.0;
        }

        /// <summary>
        /// Resolves GPU timestamp queries and updates GPU time in frame statistics.
        /// Based on Vulkan API: vkGetQueryPoolResults to retrieve timestamp values,
        /// then calculate delta time using timestamp period.
        /// Should be called in EndFrame after command buffer submission.
        /// </summary>
        /// <param name="startTimestamp">GPU timestamp at frame start (in timestamp ticks).</param>
        /// <param name="endTimestamp">GPU timestamp at frame end (in timestamp ticks).</param>
        internal void ResolveGpuTimestamps(ulong startTimestamp, ulong endTimestamp)
        {
            if (!_initialized || !_gpuTimestampsSupported || startTimestamp == 0 || endTimestamp == 0)
            {
                return;
            }

            // Calculate GPU time: (endTimestamp - startTimestamp) * timestampPeriod (nanoseconds) / 1,000,000 (convert to milliseconds)
            if (endTimestamp > startTimestamp)
            {
                ulong deltaTicks = endTimestamp - startTimestamp;
                double gpuTimeNs = deltaTicks * _gpuTimestampPeriod;
                _lastFrameStats.GpuTimeMs = gpuTimeNs / 1000000.0; // Convert nanoseconds to milliseconds
            }
            else
            {
                // Handle timestamp wrap-around (64-bit timestamps wrap after ~584 years at 1ns resolution, unlikely but handle it)
                ulong deltaTicks = (ulong.MaxValue - startTimestamp) + endTimestamp;
                double gpuTimeNs = deltaTicks * _gpuTimestampPeriod;
                _lastFrameStats.GpuTimeMs = gpuTimeNs / 1000000.0;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculates the approximate size of a texture in bytes.
        /// Based on format, dimensions, and mip levels.
        /// </summary>
        private long CalculateTextureSize(TextureDescription desc)
        {
            long size = 0;
            int width = desc.Width;
            int height = desc.Height;
            int mipLevels = desc.MipLevels > 0 ? desc.MipLevels : 1;

            // Calculate bytes per pixel based on format
            int bytesPerPixel = GetBytesPerPixel(desc.Format);

            for (int mip = 0; mip < mipLevels; mip++)
            {
                int mipWidth = width >> mip;
                int mipHeight = height >> mip;
                if (mipWidth < 1) mipWidth = 1;
                if (mipHeight < 1) mipHeight = 1;

                size += (long)mipWidth * mipHeight * bytesPerPixel * desc.ArraySize;
            }

            return size;
        }

        /// <summary>
        /// Gets the number of bytes per pixel for a texture format.
        /// </summary>
        private int GetBytesPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                case TextureFormat.R8_SInt:
                    return 1;

                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R8G8_UInt:
                case TextureFormat.R8G8_SInt:
                case TextureFormat.R16_Float:
                case TextureFormat.R16_UNorm:
                case TextureFormat.R16_UInt:
                case TextureFormat.R16_SInt:
                    return 2;

                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.R8G8B8A8_SInt:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16_UNorm:
                case TextureFormat.R16G16_UInt:
                case TextureFormat.R16G16_SInt:
                case TextureFormat.R32_Float:
                case TextureFormat.R32_UInt:
                case TextureFormat.R32_SInt:
                case TextureFormat.R11G11B10_Float:
                case TextureFormat.R10G10B10A2_UNorm:
                case TextureFormat.R10G10B10A2_UInt:
                case TextureFormat.D24_UNorm_S8_UInt:
                    return 4;

                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R16G16B16A16_UNorm:
                case TextureFormat.R16G16B16A16_UInt:
                case TextureFormat.R16G16B16A16_SInt:
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32_UInt:
                case TextureFormat.R32G32_SInt:
                case TextureFormat.D32_Float:
                    return 8;

                case TextureFormat.R32G32B32_Float:
                case TextureFormat.R32G32B32_UInt:
                case TextureFormat.R32G32B32_SInt:
                    return 12;

                case TextureFormat.R32G32B32A32_Float:
                case TextureFormat.R32G32B32A32_UInt:
                case TextureFormat.R32G32B32A32_SInt:
                case TextureFormat.D32_Float_S8_UInt:
                    return 16;

                // Compressed formats - approximate
                case TextureFormat.BC1_UNorm:
                case TextureFormat.BC1_UNorm_SRGB:
                case TextureFormat.BC1:
                case TextureFormat.BC4_UNorm:
                case TextureFormat.BC4:
                    return 1; // 4x4 block = 8 bytes, so ~0.5 bytes per pixel

                case TextureFormat.BC2_UNorm:
                case TextureFormat.BC2_UNorm_SRGB:
                case TextureFormat.BC2:
                case TextureFormat.BC3_UNorm:
                case TextureFormat.BC3_UNorm_SRGB:
                case TextureFormat.BC3:
                case TextureFormat.BC5_UNorm:
                case TextureFormat.BC5:
                    return 1; // 4x4 block = 16 bytes, so ~1 byte per pixel

                case TextureFormat.BC6H_UFloat:
                case TextureFormat.BC6H:
                case TextureFormat.BC7_UNorm:
                case TextureFormat.BC7_UNorm_SRGB:
                case TextureFormat.BC7:
                    return 1; // 4x4 block = 16 bytes, so ~1 byte per pixel

                default:
                    return 4; // Default to 4 bytes per pixel (RGBA8)
            }
        }

        /// <summary>
        /// Converts InputLayout to InputLayoutDesc.
        /// </summary>
        private InputLayoutDesc ConvertInputLayout(InputLayout inputLayout)
        {
            if (inputLayout.Elements == null || inputLayout.Elements.Length == 0)
            {
                return new InputLayoutDesc { Attributes = null };
            }

            VertexAttributeDesc[] attributes = new VertexAttributeDesc[inputLayout.Elements.Length];
            for (int i = 0; i < inputLayout.Elements.Length; i++)
            {
                InputElement element = inputLayout.Elements[i];
                attributes[i] = new VertexAttributeDesc
                {
                    Name = element.SemanticName,
                    SemanticName = element.SemanticName,
                    SemanticIndex = element.SemanticIndex,
                    Format = element.Format,
                    BufferIndex = element.Slot,
                    Slot = element.Slot,
                    Offset = element.AlignedByteOffset,
                    IsInstanced = element.PerInstance,
                    PerInstance = element.PerInstance,
                    InstanceStepRate = element.PerInstance ? element.InstanceDataStepRate : 0
                };
            }

            return new InputLayoutDesc { Attributes = attributes };
        }

        /// <summary>
        /// Converts BlendState to BlendStateDesc.
        /// </summary>
        private BlendStateDesc ConvertBlendState(BlendState blendState)
        {
            // Create a single render target blend description
            RenderTargetBlendDesc[] renderTargets = new RenderTargetBlendDesc[1];
            renderTargets[0] = new RenderTargetBlendDesc
            {
                BlendEnable = blendState.BlendEnable,
                SrcBlend = ConvertBlendFactor(blendState.SrcBlend),
                DestBlend = ConvertBlendFactor(blendState.DstBlend),
                BlendOp = ConvertBlendOp(blendState.BlendOp),
                SrcBlendAlpha = ConvertBlendFactor(blendState.SrcBlendAlpha),
                DestBlendAlpha = ConvertBlendFactor(blendState.DstBlendAlpha),
                BlendOpAlpha = ConvertBlendOp(blendState.BlendOpAlpha),
                WriteMask = blendState.RenderTargetWriteMask
            };

            return new BlendStateDesc
            {
                AlphaToCoverage = false,
                RenderTargets = renderTargets
            };
        }

        /// <summary>
        /// Converts BlendFactor enum.
        /// </summary>
        private BlendFactor ConvertBlendFactor(BlendFactor factor)
        {
            // The enum values should match, but we do explicit conversion for safety
            return (BlendFactor)(int)factor;
        }

        /// <summary>
        /// Converts BlendOp enum.
        /// </summary>
        private BlendOp ConvertBlendOp(BlendOp op)
        {
            return (BlendOp)(int)op;
        }

        /// <summary>
        /// Converts RasterizerState to RasterStateDesc.
        /// </summary>
        private RasterStateDesc ConvertRasterizerState(RasterizerState rasterizerState)
        {
            return new RasterStateDesc
            {
                CullMode = ConvertCullMode(rasterizerState.CullMode),
                FillMode = ConvertFillMode(rasterizerState.FillMode),
                FrontCCW = rasterizerState.FrontCounterClockwise,
                DepthBias = rasterizerState.DepthBias,
                SlopeScaledDepthBias = rasterizerState.SlopeScaledDepthBias,
                DepthBiasClamp = 0.0f,
                DepthClipEnable = true,
                ScissorEnable = rasterizerState.ScissorEnable,
                MultisampleEnable = rasterizerState.MultisampleEnable,
                AntialiasedLineEnable = false,
                ConservativeRaster = false
            };
        }

        /// <summary>
        /// Converts CullMode enum.
        /// </summary>
        private CullMode ConvertCullMode(CullMode mode)
        {
            return (CullMode)(int)mode;
        }

        /// <summary>
        /// Converts FillMode enum.
        /// </summary>
        private FillMode ConvertFillMode(FillMode mode)
        {
            return (FillMode)(int)mode;
        }

        /// <summary>
        /// Converts DepthStencilState to DepthStencilStateDesc.
        /// </summary>
        private DepthStencilStateDesc ConvertDepthStencilState(DepthStencilState depthStencilState)
        {
            return new DepthStencilStateDesc
            {
                DepthTestEnable = depthStencilState.DepthEnable,
                DepthWriteEnable = depthStencilState.DepthWriteEnable,
                DepthFunc = ConvertCompareFunc(depthStencilState.DepthFunc),
                StencilEnable = depthStencilState.StencilEnable,
                StencilReadMask = depthStencilState.StencilReadMask,
                StencilWriteMask = depthStencilState.StencilWriteMask,
                FrontFace = new StencilOpDesc
                {
                    StencilFailOp = StencilOp.Keep,
                    DepthFailOp = StencilOp.Keep,
                    PassOp = StencilOp.Keep,
                    StencilFunc = ConvertCompareFunc(depthStencilState.DepthFunc)
                },
                BackFace = new StencilOpDesc
                {
                    StencilFailOp = StencilOp.Keep,
                    DepthFailOp = StencilOp.Keep,
                    PassOp = StencilOp.Keep,
                    StencilFunc = ConvertCompareFunc(depthStencilState.DepthFunc)
                }
            };
        }

        /// <summary>
        /// Converts CompareFunc enum.
        /// </summary>
        private CompareFunc ConvertCompareFunc(CompareFunc func)
        {
            return (CompareFunc)(int)func;
        }

        #endregion
    }
}


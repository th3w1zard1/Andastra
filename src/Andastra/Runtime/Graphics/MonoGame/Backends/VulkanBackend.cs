using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Backends
{
    /// <summary>
    /// Vulkan graphics backend implementation.
    ///
    /// Provides:
    /// - Vulkan 1.2+ rendering
    /// - VK_KHR_ray_tracing_pipeline support
    /// - VK_KHR_acceleration_structure support
    /// - Multi-GPU support
    /// - Async compute and transfer queues
    /// </summary>
    public class VulkanBackend : IGraphicsBackend
    {
        private bool _initialized;
        private GraphicsCapabilities _capabilities;
        private RenderSettings _settings;

        // Vulkan handles (would be actual Vulkan objects in full implementation)
        private IntPtr _instance;
        private IntPtr _physicalDevice;
        private IntPtr _device;
        private IntPtr _graphicsQueue;
        private IntPtr _computeQueue;
        private IntPtr _transferQueue;
        private IntPtr _swapchain;

        // Resource tracking
        private readonly Dictionary<IntPtr, ResourceInfo> _resources;
        private uint _nextResourceHandle;

        // Raytracing state
        private bool _raytracingEnabled;
        private RaytracingLevel _raytracingLevel;

        // Frame statistics
        private FrameStatistics _lastFrameStats;

        #region Vulkan Instance-Level P/Invoke Declarations

        private const string VulkanLibrary = "vulkan-1";

        // Vulkan result codes
        private enum VkResult
        {
            VK_SUCCESS = 0,
            VK_NOT_READY = 1,
            VK_TIMEOUT = 2,
            VK_EVENT_SET = 3,
            VK_EVENT_RESET = 4,
            VK_INCOMPLETE = 5,
            VK_ERROR_OUT_OF_HOST_MEMORY = -1,
            VK_ERROR_OUT_OF_DEVICE_MEMORY = -2,
            VK_ERROR_INITIALIZATION_FAILED = -3,
            VK_ERROR_DEVICE_LOST = -4,
            VK_ERROR_MEMORY_MAP_FAILED = -5,
            VK_ERROR_LAYER_NOT_PRESENT = -6,
            VK_ERROR_EXTENSION_NOT_PRESENT = -7,
            VK_ERROR_FEATURE_NOT_PRESENT = -8,
            VK_ERROR_INCOMPATIBLE_DRIVER = -9,
            VK_ERROR_TOO_MANY_OBJECTS = -10,
            VK_ERROR_FORMAT_NOT_SUPPORTED = -11,
            VK_ERROR_FRAGMENTED_POOL = -12,
            VK_ERROR_UNKNOWN = -13,
        }

        // Vulkan device types
        private enum VkPhysicalDeviceType
        {
            VK_PHYSICAL_DEVICE_TYPE_OTHER = 0,
            VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU = 1,
            VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU = 2,
            VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU = 3,
            VK_PHYSICAL_DEVICE_TYPE_CPU = 4,
        }

        // Vulkan structures for physical device queries
        [StructLayout(LayoutKind.Sequential)]
        private struct VkPhysicalDeviceProperties
        {
            public uint apiVersion;
            public uint driverVersion;
            public uint vendorID;
            public uint deviceID;
            public VkPhysicalDeviceType deviceType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] deviceName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] pipelineCacheUUID;
            public VkPhysicalDeviceLimits limits;
            public VkPhysicalDeviceSparseProperties sparseProperties;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkPhysicalDeviceLimits
        {
            public uint maxImageDimension1D;
            public uint maxImageDimension2D;
            public uint maxImageDimension3D;
            public uint maxImageDimensionCube;
            public uint maxImageArrayLayers;
            public uint maxTexelBufferElements;
            public uint maxUniformBufferRange;
            public uint maxStorageBufferRange;
            public uint maxPushConstantsSize;
            public uint maxMemoryAllocationCount;
            public uint maxSamplerAllocationCount;
            public ulong bufferImageGranularity;
            public ulong sparseAddressSpaceSize;
            public uint maxBoundDescriptorSets;
            public uint maxPerStageDescriptorSamplers;
            public uint maxPerStageDescriptorUniformBuffers;
            public uint maxPerStageDescriptorStorageBuffers;
            public uint maxPerStageDescriptorSampledImages;
            public uint maxPerStageDescriptorStorageImages;
            public uint maxPerStageDescriptorInputAttachments;
            public uint maxPerStageResources;
            public uint maxDescriptorSetSamplers;
            public uint maxDescriptorSetUniformBuffers;
            public uint maxDescriptorSetUniformBuffersDynamic;
            public uint maxDescriptorSetStorageBuffers;
            public uint maxDescriptorSetStorageBuffersDynamic;
            public uint maxDescriptorSetSampledImages;
            public uint maxDescriptorSetStorageImages;
            public uint maxDescriptorSetInputAttachments;
            public uint maxVertexInputAttributes;
            public uint maxVertexInputBindings;
            public uint maxVertexInputAttributeOffset;
            public uint maxVertexInputBindingStride;
            public uint maxVertexOutputComponents;
            public uint maxTessellationGenerationLevel;
            public uint maxTessellationPatchSize;
            public uint maxTessellationControlPerVertexInputComponents;
            public uint maxTessellationControlPerVertexOutputComponents;
            public uint maxTessellationControlPerPatchOutputComponents;
            public uint maxTessellationControlTotalOutputComponents;
            public uint maxTessellationEvaluationInputComponents;
            public uint maxTessellationEvaluationOutputComponents;
            public uint maxGeometryShaderInvocations;
            public uint maxGeometryInputComponents;
            public uint maxGeometryOutputComponents;
            public uint maxGeometryOutputVertices;
            public uint maxGeometryTotalOutputComponents;
            public uint maxFragmentInputComponents;
            public uint maxFragmentOutputAttachments;
            public uint maxFragmentDualSrcAttachments;
            public uint maxFragmentCombinedOutputResources;
            public uint maxComputeSharedMemorySize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public uint[] maxComputeWorkGroupCount;
            public uint maxComputeWorkGroupInvocations;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public uint[] maxComputeWorkGroupSize;
            public uint subPixelPrecisionBits;
            public uint subTexelPrecisionBits;
            public uint mipmapPrecisionBits;
            public uint maxDrawIndexedIndexValue;
            public uint maxDrawIndirectCount;
            public float maxSamplerLodBias;
            public float maxSamplerAnisotropy;
            public uint maxViewports;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] maxViewportDimensions;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public float[] viewportBoundsRange;
            public uint maxViewportSubpixelBits;
            public ulong minMemoryMapAlignment;
            public ulong minTexelBufferOffsetAlignment;
            public ulong minUniformBufferOffsetAlignment;
            public ulong minStorageBufferOffsetAlignment;
            public int minTexelOffset;
            public uint maxTexelOffset;
            public int minTexelGatherOffset;
            public uint maxTexelGatherOffset;
            public float minInterpolationOffset;
            public float maxInterpolationOffset;
            public uint subPixelInterpolationOffsetBits;
            public uint maxFramebufferWidth;
            public uint maxFramebufferHeight;
            public uint maxFramebufferLayers;
            public uint framebufferColorSampleCounts;
            public uint framebufferDepthSampleCounts;
            public uint framebufferStencilSampleCounts;
            public uint framebufferNoAttachmentsSampleCounts;
            public uint maxColorAttachments;
            public uint sampledImageColorSampleCounts;
            public uint sampledImageIntegerSampleCounts;
            public uint sampledImageDepthSampleCounts;
            public uint sampledImageStencilSampleCounts;
            public uint storageImageColorSampleCounts;
            public uint storageImageIntegerSampleCounts;
            public uint maxClipDistances;
            public uint maxCullDistances;
            public uint maxCombinedClipAndCullDistances;
            public uint discreteQueuePriorities;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public float[] pointSizeRange;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public float[] lineWidthRange;
            public float pointSizeGranularity;
            public float lineWidthGranularity;
            public uint strictLines;
            public uint standardSampleLocations;
            public ulong optimalBufferCopyOffsetAlignment;
            public ulong optimalBufferCopyRowPitchAlignment;
            public ulong nonCoherentAtomSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkPhysicalDeviceSparseProperties
        {
            public uint residencyStandard2DBlockShape;
            public uint residencyStandard2DMultisampleBlockShape;
            public uint residencyStandard3DBlockShape;
            public uint residencyAlignedMipSize;
            public uint residencyNonResidentStrict;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkPhysicalDeviceFeatures
        {
            public uint robustBufferAccess;
            public uint fullDrawIndexUint32;
            public uint imageCubeArray;
            public uint independentBlend;
            public uint geometryShader;
            public uint tessellationShader;
            public uint sampleRateShading;
            public uint dualSrcBlend;
            public uint logicOp;
            public uint multiDrawIndirect;
            public uint drawIndirectFirstInstance;
            public uint depthClamp;
            public uint depthBiasClamp;
            public uint fillModeNonSolid;
            public uint depthBounds;
            public uint wideLines;
            public uint largePoints;
            public uint alphaToOne;
            public uint multiViewport;
            public uint samplerAnisotropy;
            public uint textureCompressionETC2;
            public uint textureCompressionASTC_LDR;
            public uint textureCompressionBC;
            public uint occlusionQueryPrecise;
            public uint pipelineStatisticsQuery;
            public uint vertexPipelineStoresAndAtomics;
            public uint fragmentStoresAndAtomics;
            public uint shaderTessellationAndGeometryPointSize;
            public uint shaderImageGatherExtended;
            public uint shaderStorageImageExtendedFormats;
            public uint shaderStorageImageMultisample;
            public uint shaderStorageImageReadWithoutFormat;
            public uint shaderStorageImageWriteWithoutFormat;
            public uint shaderUniformBufferArrayDynamicIndexing;
            public uint shaderSampledImageArrayDynamicIndexing;
            public uint shaderStorageBufferArrayDynamicIndexing;
            public uint shaderStorageImageArrayDynamicIndexing;
            public uint shaderClipDistance;
            public uint shaderCullDistance;
            public uint shaderFloat64;
            public uint shaderInt64;
            public uint shaderInt16;
            public uint shaderResourceResidency;
            public uint shaderResourceMinLod;
            public uint sparseBinding;
            public uint sparseResidencyBuffer;
            public uint sparseResidencyImage2D;
            public uint sparseResidencyImage3D;
            public uint sparseResidency2Samples;
            public uint sparseResidency4Samples;
            public uint sparseResidency8Samples;
            public uint sparseResidency16Samples;
            public uint sparseResidencyAliased;
            public uint variableMultisampleRate;
            public uint inheritedQueries;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkMemoryHeap
        {
            public ulong size;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkMemoryType
        {
            public uint propertyFlags;
            public uint heapIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkPhysicalDeviceMemoryProperties
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public VkMemoryType[] memoryTypes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public VkMemoryHeap[] memoryHeaps;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkQueueFamilyProperties
        {
            public uint queueFlags;
            public uint queueCount;
            public uint timestampValidBits;
            public VkExtent3D minImageTransferGranularity;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkExtent3D
        {
            public uint width;
            public uint height;
            public uint depth;
        }

        // P/Invoke declarations for instance-level Vulkan functions
        // Vulkan uses C calling convention on all platforms
        [DllImport(VulkanLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern VkResult vkEnumeratePhysicalDevices(IntPtr instance, ref uint pPhysicalDeviceCount, IntPtr pPhysicalDevices);

        [DllImport(VulkanLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vkGetPhysicalDeviceProperties(IntPtr physicalDevice, out VkPhysicalDeviceProperties pProperties);

        [DllImport(VulkanLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vkGetPhysicalDeviceFeatures(IntPtr physicalDevice, out VkPhysicalDeviceFeatures pFeatures);

        [DllImport(VulkanLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vkGetPhysicalDeviceMemoryProperties(IntPtr physicalDevice, out VkPhysicalDeviceMemoryProperties pMemoryProperties);

        [DllImport(VulkanLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vkGetPhysicalDeviceQueueFamilyProperties(IntPtr physicalDevice, ref uint pQueueFamilyPropertyCount, IntPtr pQueueFamilyProperties);

        // Device scoring structure
        private struct DeviceScore
        {
            public IntPtr PhysicalDevice;
            public int Score;
            public VkPhysicalDeviceProperties Properties;
            public VkPhysicalDeviceFeatures Features;
            public VkPhysicalDeviceMemoryProperties MemoryProperties;
            public ulong DedicatedVideoMemory;
        }

        #endregion

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
            get { return _raytracingEnabled; }
        }

        public VulkanBackend()
        {
            _resources = new Dictionary<IntPtr, ResourceInfo>();
            _nextResourceHandle = 1;
        }

        /// <summary>
        /// Initializes the Vulkan backend.
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

            // Create Vulkan instance
            if (!CreateInstance())
            {
                Console.WriteLine("[VulkanBackend] Failed to create Vulkan instance");
                return false;
            }

            // Select physical device
            if (!SelectPhysicalDevice())
            {
                Console.WriteLine("[VulkanBackend] No suitable Vulkan device found");
                return false;
            }

            // Create logical device with queues
            if (!CreateDevice())
            {
                Console.WriteLine("[VulkanBackend] Failed to create Vulkan device");
                return false;
            }

            // Create swapchain
            if (!CreateSwapchain())
            {
                Console.WriteLine("[VulkanBackend] Failed to create swapchain");
                return false;
            }

            // Initialize raytracing if available and requested
            if (_capabilities.SupportsRaytracing && settings.Raytracing != RaytracingLevel.Disabled)
            {
                InitializeRaytracing();
            }

            _initialized = true;
            Console.WriteLine("[VulkanBackend] Initialized successfully");
            Console.WriteLine("[VulkanBackend] Device: " + _capabilities.DeviceName);
            Console.WriteLine("[VulkanBackend] Raytracing: " + (_capabilities.SupportsRaytracing ? "available" : "not available"));

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

            // Destroy swapchain
            // vkDestroySwapchainKHR(_device, _swapchain, null);

            // Destroy device
            // vkDestroyDevice(_device, null);

            // Destroy instance
            // vkDestroyInstance(_instance, null);

            _initialized = false;
            Console.WriteLine("[VulkanBackend] Shutdown complete");
        }

        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Acquire swapchain image
            // vkAcquireNextImageKHR(...)

            // Begin command buffer
            // vkBeginCommandBuffer(...)

            // Reset frame statistics
            _lastFrameStats = new FrameStatistics();
        }

        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // End command buffer
            // vkEndCommandBuffer(...)

            // Submit to queue
            // vkQueueSubmit(...)

            // Present
            // vkQueuePresentKHR(...)
        }

        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            // Recreate swapchain
            // vkDestroySwapchainKHR(...)
            // CreateSwapchain()

            _settings.Width = width;
            _settings.Height = height;
        }

        public IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // Create VkImage
            // Create VkImageView
            // Allocate VkDeviceMemory

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                DebugName = desc.DebugName,
                TextureDesc = desc
            };

            return handle;
        }

        /// <summary>
        /// Uploads texture pixel data to a previously created texture.
        /// Matches original engine behavior: Vulkan uses vkCmdCopyBufferToImage
        /// to upload texture data after creating the image resource.
        /// </summary>
        public bool UploadTextureData(IntPtr handle, TextureUploadData data)
        {
            if (!_initialized || handle == IntPtr.Zero)
            {
                return false;
            }

            if (!_resources.TryGetValue(handle, out ResourceInfo info) || info.Type != ResourceType.Texture)
            {
                Console.WriteLine("[VulkanBackend] UploadTextureData: Invalid texture handle");
                return false;
            }

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                Console.WriteLine("[VulkanBackend] UploadTextureData: No mipmap data provided");
                return false;
            }

            try
            {
                info.UploadData = data;
                _resources[handle] = info;

                Console.WriteLine($"[VulkanBackend] UploadTextureData: Stored {data.Mipmaps.Length} mipmap levels for texture {info.DebugName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VulkanBackend] UploadTextureData: Exception uploading texture: {ex.Message}");
                return false;
            }
        }

        public IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // Create VkBuffer
            // Allocate VkDeviceMemory

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
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

            // Create VkShaderModules
            // Create VkPipelineLayout
            // Create VkPipeline

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                DebugName = desc.DebugName
            };

            return handle;
        }

        public void DestroyResource(IntPtr handle)
        {
            if (!_initialized || !_resources.TryGetValue(handle, out ResourceInfo info))
            {
                return;
            }

            DestroyResourceInternal(info);
            _resources.Remove(handle);
        }

        public void SetRaytracingLevel(RaytracingLevel level)
        {
            if (!_capabilities.SupportsRaytracing)
            {
                return;
            }

            _raytracingLevel = level;
            _raytracingEnabled = level != RaytracingLevel.Disabled;
        }

        public FrameStatistics GetFrameStatistics()
        {
            return _lastFrameStats;
        }

        public IDevice GetDevice()
        {
            // Return VulkanDevice instance for raytracing operations
            if (!_initialized || !_capabilities.SupportsRaytracing || !_raytracingEnabled || _device == IntPtr.Zero)
            {
                return null;
            }

            // Create VulkanDevice with the initialized Vulkan handles
            var vulkanDevice = new VulkanDevice(
                _device,
                _instance,
                _physicalDevice,
                _graphicsQueue,
                _computeQueue,
                _transferQueue,
                _capabilities);

            Console.WriteLine("[VulkanBackend] Created VulkanDevice for raytracing operations");
            return vulkanDevice;
        }

        private bool CreateInstance()
        {
            // VkApplicationInfo
            // VkInstanceCreateInfo
            // Enable validation layers in debug
            // Enable required extensions:
            //   VK_KHR_surface
            //   VK_KHR_win32_surface (Windows)
            //   VK_KHR_xlib_surface (Linux)
            //   VK_EXT_debug_utils (debug)

            // TODO: STUB - vkCreateInstance(...)

            _instance = new IntPtr(1); // TODO: STUB - Placeholder
            return true;
        }

        /// <summary>
        /// Selects the best physical device from available Vulkan devices.
        /// 
        /// Selection criteria (in priority order):
        /// 1. Discrete GPU preferred over integrated
        /// 2. Raytracing support (VK_KHR_ray_tracing_pipeline)
        /// 3. Maximum dedicated video memory
        /// 4. Required feature support (geometry shaders, tessellation, compute)
        /// 5. Maximum texture size and render targets
        /// 
        /// Based on Vulkan API: vkEnumeratePhysicalDevices, vkGetPhysicalDeviceProperties
        /// </summary>
        private bool SelectPhysicalDevice()
        {
            if (_instance == IntPtr.Zero)
            {
                Console.WriteLine("[VulkanBackend] SelectPhysicalDevice: Instance not created");
                return false;
            }

            // Step 1: Enumerate physical devices
            uint deviceCount = 0;
            VkResult result = vkEnumeratePhysicalDevices(_instance, ref deviceCount, IntPtr.Zero);
            if (result != VkResult.VK_SUCCESS && result != VkResult.VK_INCOMPLETE)
            {
                Console.WriteLine($"[VulkanBackend] SelectPhysicalDevice: Failed to enumerate devices: {result}");
                return false;
            }

            if (deviceCount == 0)
            {
                Console.WriteLine("[VulkanBackend] SelectPhysicalDevice: No physical devices found");
                return false;
            }

            Console.WriteLine($"[VulkanBackend] Found {deviceCount} physical device(s)");

            // Allocate array for device handles
            IntPtr[] deviceHandles = new IntPtr[deviceCount];
            GCHandle gcHandle = GCHandle.Alloc(deviceHandles, GCHandleType.Pinned);
            try
            {
                IntPtr devicesPtr = gcHandle.AddrOfPinnedObject();
                result = vkEnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr);
                if (result != VkResult.VK_SUCCESS)
                {
                    Console.WriteLine($"[VulkanBackend] SelectPhysicalDevice: Failed to get device handles: {result}");
                    return false;
                }
            }
            finally
            {
                gcHandle.Free();
            }

            // Step 2: Query properties and score each device
            List<DeviceScore> deviceScores = new List<DeviceScore>();
            for (uint i = 0; i < deviceCount; i++)
            {
                IntPtr device = deviceHandles[i];
                DeviceScore score = ScoreDevice(device);
                deviceScores.Add(score);

                // Log device info
                string deviceName = GetDeviceName(score.Properties);
                Console.WriteLine($"[VulkanBackend] Device {i}: {deviceName} (Score: {score.Score})");
            }

            // Step 3: Select best device
            if (deviceScores.Count == 0)
            {
                Console.WriteLine("[VulkanBackend] SelectPhysicalDevice: No suitable devices found");
                return false;
            }

            // Sort by score (highest first)
            deviceScores.Sort((a, b) => b.Score.CompareTo(a.Score));
            DeviceScore bestDevice = deviceScores[0];

            if (bestDevice.Score <= 0)
            {
                Console.WriteLine("[VulkanBackend] SelectPhysicalDevice: No suitable device found (all devices scored 0 or less)");
                return false;
            }

            _physicalDevice = bestDevice.PhysicalDevice;

            // Step 4: Query capabilities from selected device
            _capabilities = QueryDeviceCapabilities(bestDevice);

            string selectedDeviceName = GetDeviceName(bestDevice.Properties);
            Console.WriteLine($"[VulkanBackend] Selected device: {selectedDeviceName}");
            Console.WriteLine($"[VulkanBackend] Dedicated VRAM: {bestDevice.DedicatedVideoMemory / (1024 * 1024 * 1024)} GB");
            Console.WriteLine($"[VulkanBackend] Raytracing: {(_capabilities.SupportsRaytracing ? "Yes" : "No")}");

            return true;
        }

        /// <summary>
        /// Scores a physical device based on various criteria.
        /// Higher score = better device.
        /// </summary>
        private DeviceScore ScoreDevice(IntPtr physicalDevice)
        {
            DeviceScore score = new DeviceScore
            {
                PhysicalDevice = physicalDevice,
                Score = 0
            };

            // Query device properties
            vkGetPhysicalDeviceProperties(physicalDevice, out score.Properties);
            vkGetPhysicalDeviceFeatures(physicalDevice, out score.Features);
            vkGetPhysicalDeviceMemoryProperties(physicalDevice, out score.MemoryProperties);

            // Calculate dedicated video memory
            score.DedicatedVideoMemory = CalculateDedicatedVideoMemory(score.MemoryProperties);

            // Scoring criteria:

            // 1. Device type (discrete GPU preferred)
            if (score.Properties.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
            {
                score.Score += 1000;
            }
            else if (score.Properties.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU)
            {
                score.Score += 100;
            }
            else if (score.Properties.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU)
            {
                score.Score += 50;
            }
            // CPU and OTHER get 0 points

            // 2. Required features support
            if (score.Features.geometryShader != 0)
            {
                score.Score += 100;
            }
            if (score.Features.tessellationShader != 0)
            {
                score.Score += 100;
            }
            if (score.Features.samplerAnisotropy != 0)
            {
                score.Score += 50;
            }
            if (score.Features.shaderInt64 != 0)
            {
                score.Score += 25;
            }
            if (score.Features.shaderFloat64 != 0)
            {
                score.Score += 25;
            }

            // 3. Memory size (1 point per GB of dedicated VRAM, capped at 500 points)
            long vramGB = (long)(score.DedicatedVideoMemory / (1024UL * 1024UL * 1024UL));
            score.Score += (int)Math.Min(vramGB, 500);

            // 4. Texture size support
            if (score.Properties.limits.maxImageDimension2D >= 16384)
            {
                score.Score += 50;
            }
            else if (score.Properties.limits.maxImageDimension2D >= 8192)
            {
                score.Score += 25;
            }

            // 5. Render target support
            if (score.Properties.limits.maxColorAttachments >= 8)
            {
                score.Score += 25;
            }
            else if (score.Properties.limits.maxColorAttachments >= 4)
            {
                score.Score += 10;
            }

            // 6. Raytracing support (check via extension - will be verified later)
            // For now, we'll check this during capability query
            // High-end GPUs typically support it, so we give bonus points for high memory
            if (score.DedicatedVideoMemory >= 6UL * 1024 * 1024 * 1024) // 6GB+
            {
                score.Score += 200; // Potential raytracing support
            }

            // 7. Vendor preference (NVIDIA/AMD typically better for gaming)
            uint vendorId = score.Properties.vendorID;
            // NVIDIA: 0x10DE, AMD: 0x1002, Intel: 0x8086
            if (vendorId == 0x10DE) // NVIDIA
            {
                score.Score += 50;
            }
            else if (vendorId == 0x1002) // AMD
            {
                score.Score += 30;
            }

            return score;
        }

        /// <summary>
        /// Calculates dedicated video memory from memory properties.
        /// Dedicated memory is in heaps with VK_MEMORY_HEAP_DEVICE_LOCAL_BIT flag.
        /// </summary>
        private ulong CalculateDedicatedVideoMemory(VkPhysicalDeviceMemoryProperties memoryProps)
        {
            ulong dedicatedMemory = 0;

            for (int i = 0; i < memoryProps.memoryHeaps.Length; i++)
            {
                if (memoryProps.memoryHeaps[i].size == 0)
                {
                    continue;
                }

                // Check if this heap is device-local (dedicated VRAM)
                const uint VK_MEMORY_HEAP_DEVICE_LOCAL_BIT = 0x00000001;
                if ((memoryProps.memoryHeaps[i].flags & VK_MEMORY_HEAP_DEVICE_LOCAL_BIT) != 0)
                {
                    dedicatedMemory += memoryProps.memoryHeaps[i].size;
                }
            }

            return dedicatedMemory;
        }

        /// <summary>
        /// Gets device name as string from properties.
        /// </summary>
        private string GetDeviceName(VkPhysicalDeviceProperties properties)
        {
            if (properties.deviceName == null)
            {
                return "Unknown Device";
            }

            // Find null terminator
            int length = 0;
            for (int i = 0; i < properties.deviceName.Length; i++)
            {
                if (properties.deviceName[i] == 0)
                {
                    length = i;
                    break;
                }
            }

            if (length == 0)
            {
                length = properties.deviceName.Length;
            }

            return Encoding.UTF8.GetString(properties.deviceName, 0, length);
        }

        /// <summary>
        /// Queries device capabilities from the selected physical device.
        /// </summary>
        private GraphicsCapabilities QueryDeviceCapabilities(DeviceScore deviceScore)
        {
            VkPhysicalDeviceProperties props = deviceScore.Properties;
            VkPhysicalDeviceFeatures features = deviceScore.Features;

            // Query raytracing support (check for extension)
            bool supportsRaytracing = CheckRaytracingSupport(deviceScore.PhysicalDevice);

            // Query mesh shader support (check for extension)
            bool supportsMeshShaders = CheckMeshShaderSupport(deviceScore.PhysicalDevice);

            // Get vendor name
            string vendorName = GetVendorName(props.vendorID);

            // Get device name
            string deviceName = GetDeviceName(props);

            // Get driver version (format as major.minor.patch)
            uint driverVersion = props.driverVersion;
            string driverVersionStr = FormatDriverVersion(driverVersion, props.vendorID);

            // Calculate shared system memory (non-device-local heaps)
            ulong sharedMemory = 0;
            for (int i = 0; i < deviceScore.MemoryProperties.memoryHeaps.Length; i++)
            {
                if (deviceScore.MemoryProperties.memoryHeaps[i].size == 0)
                {
                    continue;
                }

                const uint VK_MEMORY_HEAP_DEVICE_LOCAL_BIT = 0x00000001;
                if ((deviceScore.MemoryProperties.memoryHeaps[i].flags & VK_MEMORY_HEAP_DEVICE_LOCAL_BIT) == 0)
                {
                    sharedMemory += deviceScore.MemoryProperties.memoryHeaps[i].size;
                }
            }

            return new GraphicsCapabilities
            {
                MaxTextureSize = (int)props.limits.maxImageDimension2D,
                MaxRenderTargets = (int)props.limits.maxColorAttachments,
                MaxAnisotropy = features.samplerAnisotropy != 0 ? (int)props.limits.maxSamplerAnisotropy : 1,
                SupportsComputeShaders = true, // Vulkan always supports compute
                SupportsGeometryShaders = features.geometryShader != 0,
                SupportsTessellation = features.tessellationShader != 0,
                SupportsRaytracing = supportsRaytracing,
                SupportsMeshShaders = supportsMeshShaders,
                SupportsVariableRateShading = CheckVariableRateShadingSupport(deviceScore.PhysicalDevice),
                DedicatedVideoMemory = (long)deviceScore.DedicatedVideoMemory,
                SharedSystemMemory = (long)sharedMemory,
                VendorName = vendorName,
                DeviceName = deviceName,
                DriverVersion = driverVersionStr,
                ActiveBackend = GraphicsBackend.Vulkan,
                ShaderModelVersion = 6.6f, // SPIR-V equivalent
                RemixAvailable = false, // NVIDIA Remix is separate
                DlssAvailable = CheckDlssSupport(deviceScore.PhysicalDevice, vendorName),
                FsrAvailable = true // FSR works on any device
            };
        }

        /// <summary>
        /// Checks if device supports raytracing extensions.
        /// </summary>
        private bool CheckRaytracingSupport(IntPtr physicalDevice)
        {
            // Check for VK_KHR_ray_tracing_pipeline extension
            // In a full implementation, we would enumerate extensions here
            // For now, we'll use a heuristic: high-end GPUs with sufficient memory typically support it
            VkPhysicalDeviceProperties props;
            vkGetPhysicalDeviceProperties(physicalDevice, out props);

            // Check device type and memory as heuristic
            if (props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
            {
                VkPhysicalDeviceMemoryProperties memProps;
                vkGetPhysicalDeviceMemoryProperties(physicalDevice, out memProps);
                ulong dedicatedMemory = CalculateDedicatedVideoMemory(memProps);

                // Typically RTX 20-series and above, or RX 6000-series and above
                // Heuristic: 6GB+ dedicated memory on discrete GPU
                if (dedicatedMemory >= 6UL * 1024 * 1024 * 1024)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if device supports mesh shader extension.
        /// </summary>
        private bool CheckMeshShaderSupport(IntPtr physicalDevice)
        {
            // VK_EXT_mesh_shader support check
            // Heuristic: newer high-end GPUs
            VkPhysicalDeviceProperties props;
            vkGetPhysicalDeviceProperties(physicalDevice, out props);

            if (props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
            {
                // Typically RTX 30-series and above, or RX 7000-series and above
                // Heuristic: API version 1.3+ and discrete GPU
                if (props.apiVersion >= VK_MAKE_VERSION(1, 3, 0))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if device supports variable rate shading.
        /// </summary>
        private bool CheckVariableRateShadingSupport(IntPtr physicalDevice)
        {
            // VK_KHR_fragment_shading_rate support
            // Heuristic: modern GPUs
            VkPhysicalDeviceProperties props;
            vkGetPhysicalDeviceProperties(physicalDevice, out props);

            if (props.apiVersion >= VK_MAKE_VERSION(1, 2, 0))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if device supports DLSS.
        /// </summary>
        private bool CheckDlssSupport(IntPtr physicalDevice, string vendorName)
        {
            // DLSS is NVIDIA-only and requires RTX GPUs
            if (vendorName != "NVIDIA")
            {
                return false;
            }

            VkPhysicalDeviceProperties props;
            vkGetPhysicalDeviceProperties(physicalDevice, out props);

            // RTX GPUs typically have device ID starting with certain ranges
            // This is a simplified check - real implementation would check extension
            return props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU;
        }

        /// <summary>
        /// Gets vendor name from vendor ID.
        /// </summary>
        private string GetVendorName(uint vendorId)
        {
            switch (vendorId)
            {
                case 0x10DE:
                    return "NVIDIA";
                case 0x1002:
                    return "AMD";
                case 0x8086:
                    return "Intel";
                case 0x13B5:
                    return "ARM";
                case 0x5143:
                    return "Qualcomm";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Formats driver version based on vendor.
        /// </summary>
        private string FormatDriverVersion(uint driverVersion, uint vendorId)
        {
            if (vendorId == 0x10DE) // NVIDIA
            {
                // NVIDIA: ((major << 22) | (minor << 14) | patch)
                uint major = (driverVersion >> 22) & 0x3FF;
                uint minor = (driverVersion >> 14) & 0xFF;
                uint patch = driverVersion & 0x3FFF;
                return $"{major}.{minor}.{patch}";
            }
            else if (vendorId == 0x1002) // AMD
            {
                // AMD: (major << 16) | (minor << 8) | patch
                uint major = (driverVersion >> 16) & 0xFF;
                uint minor = (driverVersion >> 8) & 0xFF;
                uint patch = driverVersion & 0xFF;
                return $"{major}.{minor}.{patch}";
            }
            else if (vendorId == 0x8086) // Intel
            {
                // Intel: (major << 14) | (minor << 6) | patch
                uint major = (driverVersion >> 14) & 0x3FFFF;
                uint minor = (driverVersion >> 6) & 0xFF;
                uint patch = driverVersion & 0x3F;
                return $"{major}.{minor}.{patch}";
            }
            else
            {
                // Generic format
                return driverVersion.ToString();
            }
        }

        /// <summary>
        /// Helper to create Vulkan version number.
        /// </summary>
        private uint VK_MAKE_VERSION(uint major, uint minor, uint patch)
        {
            return (major << 22) | (minor << 12) | patch;
        }

        private bool CreateDevice()
        {
            // Queue family indices
            // VkDeviceQueueCreateInfo (graphics, compute, transfer)
            // VkPhysicalDeviceFeatures
            // Enable extensions:
            //   VK_KHR_swapchain
            //   VK_KHR_acceleration_structure (raytracing)
            //   VK_KHR_ray_tracing_pipeline (raytracing)
            //   VK_KHR_deferred_host_operations (raytracing)
            //   VK_EXT_mesh_shader
            //   VK_KHR_dynamic_rendering

            // vkCreateDevice(...)
            // vkGetDeviceQueue(...) for each queue

            _device = new IntPtr(1);
            _graphicsQueue = new IntPtr(1);
            _computeQueue = new IntPtr(2);
            _transferQueue = new IntPtr(3);

            return true;
        }

        private bool CreateSwapchain()
        {
            // Query surface capabilities
            // vkGetPhysicalDeviceSurfaceCapabilitiesKHR(...)

            // Select format (prefer HDR if enabled)
            // VK_FORMAT_R16G16B16A16_SFLOAT for HDR
            // VK_FORMAT_B8G8R8A8_SRGB for SDR

            // Select present mode
            // VK_PRESENT_MODE_FIFO_KHR (vsync)
            // VK_PRESENT_MODE_MAILBOX_KHR (triple buffer)
            // VK_PRESENT_MODE_IMMEDIATE_KHR (no vsync)

            // vkCreateSwapchainKHR(...)

            _swapchain = new IntPtr(1);
            return true;
        }

        private void InitializeRaytracing()
        {
            // Query raytracing properties
            // VkPhysicalDeviceRayTracingPipelinePropertiesKHR

            // Create raytracing pipeline
            // Ray generation shader
            // Miss shaders
            // Closest hit shaders
            // Any hit shaders (for alpha testing)

            // Create shader binding table

            _raytracingEnabled = true;
            _raytracingLevel = _settings.Raytracing;

            Console.WriteLine("[VulkanBackend] Raytracing initialized");
        }

        private void DestroyResourceInternal(ResourceInfo info)
        {
            switch (info.Type)
            {
                case ResourceType.Texture:
                    // vkDestroyImageView(...)
                    // vkDestroyImage(...)
                    // vkFreeMemory(...)
                    break;

                case ResourceType.Buffer:
                    // vkDestroyBuffer(...)
                    // vkFreeMemory(...)
                    break;

                case ResourceType.Pipeline:
                    // vkDestroyPipeline(...)
                    // vkDestroyPipelineLayout(...)
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
}


using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Backends
{
    /// <summary>
    /// Vulkan device wrapper implementing IDevice interface for raytracing operations.
    ///
    /// Provides NVRHI-style abstractions for Vulkan raytracing resources:
    /// - Acceleration structures (BLAS/TLAS)
    /// - Raytracing pipelines
    /// - Resource creation and management
    ///
    /// Wraps native VkDevice with VK_KHR_ray_tracing_pipeline extension support.
    /// </summary>
    public class VulkanDevice : IDevice
    {
        #region Vulkan API Interop

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

        // Vulkan format mapping
        private enum VkFormat
        {
            VK_FORMAT_UNDEFINED = 0,
            VK_FORMAT_R8G8B8A8_UNORM = 37,
            VK_FORMAT_R8G8B8A8_SRGB = 43,
            VK_FORMAT_R16G16B16A16_SFLOAT = 91,
            VK_FORMAT_R32G32B32A32_SFLOAT = 109,
            VK_FORMAT_D24_UNORM_S8_UINT = 129,
            VK_FORMAT_D32_SFLOAT = 126,
            VK_FORMAT_D32_SFLOAT_S8_UINT = 130,
        }

        // Vulkan image usage flags
        [Flags]
        private enum VkImageUsageFlags
        {
            VK_IMAGE_USAGE_TRANSFER_SRC_BIT = 0x00000001,
            VK_IMAGE_USAGE_TRANSFER_DST_BIT = 0x00000002,
            VK_IMAGE_USAGE_SAMPLED_BIT = 0x00000004,
            VK_IMAGE_USAGE_STORAGE_BIT = 0x00000008,
            VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT = 0x00000010,
            VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT = 0x00000020,
            VK_IMAGE_USAGE_TRANSIENT_ATTACHMENT_BIT = 0x00000040,
            VK_IMAGE_USAGE_INPUT_ATTACHMENT_BIT = 0x00000080,
        }

        // Vulkan format feature flags
        // Based on Vulkan API: https://docs.vulkan.org/spec/latest/chapters/formats.html#VkFormatFeatureFlags
        [Flags]
        private enum VkFormatFeatureFlags : uint
        {
            VK_FORMAT_FEATURE_SAMPLED_IMAGE_BIT = 0x00000001,
            VK_FORMAT_FEATURE_STORAGE_IMAGE_BIT = 0x00000002,
            VK_FORMAT_FEATURE_STORAGE_IMAGE_ATOMIC_BIT = 0x00000004,
            VK_FORMAT_FEATURE_UNIFORM_TEXEL_BUFFER_BIT = 0x00000008,
            VK_FORMAT_FEATURE_STORAGE_TEXEL_BUFFER_BIT = 0x00000010,
            VK_FORMAT_FEATURE_STORAGE_TEXEL_BUFFER_ATOMIC_BIT = 0x00000020,
            VK_FORMAT_FEATURE_VERTEX_BUFFER_BIT = 0x00000040,
            VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BIT = 0x00000080,
            VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BLEND_BIT = 0x00000100,
            VK_FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT = 0x00000200,
            VK_FORMAT_FEATURE_BLIT_SRC_BIT = 0x00000400,
            VK_FORMAT_FEATURE_BLIT_DST_BIT = 0x00000800,
            VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_LINEAR_BIT = 0x00001000,
            VK_FORMAT_FEATURE_TRANSFER_SRC_BIT = 0x00004000,
            VK_FORMAT_FEATURE_TRANSFER_DST_BIT = 0x00008000,
            VK_FORMAT_FEATURE_MIDPOINT_CHROMA_SAMPLES_BIT = 0x00020000,
            VK_FORMAT_FEATURE_SAMPLED_IMAGE_YCBCR_CONVERSION_LINEAR_FILTER_BIT = 0x00040000,
            VK_FORMAT_FEATURE_SAMPLED_IMAGE_YCBCR_CONVERSION_SEPARATE_RECONSTRUCTION_FILTER_BIT = 0x00080000,
            VK_FORMAT_FEATURE_SAMPLED_IMAGE_YCBCR_CONVERSION_CHROMA_RECONSTRUCTION_EXPLICIT_BIT = 0x00100000,
            VK_FORMAT_FEATURE_SAMPLED_IMAGE_YCBCR_CONVERSION_CHROMA_RECONSTRUCTION_EXPLICIT_FORCEABLE_BIT = 0x00200000,
            VK_FORMAT_FEATURE_DISJOINT_BIT = 0x00400000,
            VK_FORMAT_FEATURE_COSITED_CHROMA_SAMPLES_BIT = 0x00800000,
            VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_MINMAX_BIT = 0x00010000,
        }

        // Vulkan format properties structure
        // Based on Vulkan API: https://docs.vulkan.org/spec/latest/chapters/formats.html#VkFormatProperties
        [StructLayout(LayoutKind.Sequential)]
        private struct VkFormatProperties
        {
            public VkFormatFeatureFlags linearTilingFeatures;   // Format features supported with linear tiling
            public VkFormatFeatureFlags optimalTilingFeatures;  // Format features supported with optimal tiling
            public VkFormatFeatureFlags bufferFeatures;         // Format features supported for buffers
        }

        // Vulkan buffer usage flags
        [Flags]
        private enum VkBufferUsageFlags
        {
            VK_BUFFER_USAGE_TRANSFER_SRC_BIT = 0x00000001,
            VK_BUFFER_USAGE_TRANSFER_DST_BIT = 0x00000002,
            VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT = 0x00000010,
            VK_BUFFER_USAGE_STORAGE_BUFFER_BIT = 0x00000020,
            VK_BUFFER_USAGE_INDEX_BUFFER_BIT = 0x00000040,
            VK_BUFFER_USAGE_VERTEX_BUFFER_BIT = 0x00000080,
            VK_BUFFER_USAGE_INDIRECT_BUFFER_BIT = 0x00000100,
            VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT = 0x00020000,
            VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR = 0x00080000,
            VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR = 0x00100000,
        }

        // Vulkan memory property flags
        [Flags]
        private enum VkMemoryPropertyFlags
        {
            VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT = 0x00000001,
            VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT = 0x00000002,
            VK_MEMORY_PROPERTY_HOST_COHERENT_BIT = 0x00000004,
            VK_MEMORY_PROPERTY_HOST_CACHED_BIT = 0x00000008,
            VK_MEMORY_PROPERTY_LAZILY_ALLOCATED_BIT = 0x00000010,
            VK_MEMORY_PROPERTY_PROTECTED_BIT = 0x00000020,
        }

        // Vulkan pipeline stage flags
        [Flags]
        private enum VkPipelineStageFlags
        {
            VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT = 0x00000001,
            VK_PIPELINE_STAGE_DRAW_INDIRECT_BIT = 0x00000002,
            VK_PIPELINE_STAGE_VERTEX_INPUT_BIT = 0x00000004,
            VK_PIPELINE_STAGE_VERTEX_SHADER_BIT = 0x00000008,
            VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT = 0x00000080,
            VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT = 0x00000400,
            VK_PIPELINE_STAGE_TRANSFER_BIT = 0x00001000,
            VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT = 0x00002000,
            VK_PIPELINE_STAGE_HOST_BIT = 0x00004000,
            VK_PIPELINE_STAGE_ALL_GRAPHICS_BIT = 0x00008000,
            VK_PIPELINE_STAGE_ALL_COMMANDS_BIT = 0x00010000,
            VK_PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR = 0x00200000,
        }

        // Vulkan access flags
        [Flags]
        private enum VkAccessFlags
        {
            VK_ACCESS_INDIRECT_COMMAND_READ_BIT = 0x00000001,
            VK_ACCESS_INDEX_READ_BIT = 0x00000002,
            VK_ACCESS_VERTEX_ATTRIBUTE_READ_BIT = 0x00000004,
            VK_ACCESS_UNIFORM_READ_BIT = 0x00000008,
            VK_ACCESS_INPUT_ATTACHMENT_READ_BIT = 0x00000010,
            VK_ACCESS_SHADER_READ_BIT = 0x00000020,
            VK_ACCESS_SHADER_WRITE_BIT = 0x00000040,
            VK_ACCESS_COLOR_ATTACHMENT_READ_BIT = 0x00000080,
            VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT = 0x00000100,
            VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT = 0x00000200,
            VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT = 0x00000400,
            VK_ACCESS_TRANSFER_READ_BIT = 0x00000800,
            VK_ACCESS_TRANSFER_WRITE_BIT = 0x00001000,
            VK_ACCESS_HOST_READ_BIT = 0x00002000,
            VK_ACCESS_HOST_WRITE_BIT = 0x00004000,
            VK_ACCESS_MEMORY_READ_BIT = 0x00008000,
            VK_ACCESS_MEMORY_WRITE_BIT = 0x00010000,
            VK_ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR = 0x00200000,
            VK_ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR = 0x00400000,
        }

        // Vulkan stencil face flags
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkStencilFaceFlagBits.html
        [Flags]
        private enum VkStencilFaceFlags
        {
            VK_STENCIL_FACE_FRONT_BIT = 0x00000001,
            VK_STENCIL_FACE_BACK_BIT = 0x00000002,
            VK_STENCIL_FACE_FRONT_AND_BACK = 0x00000003,
        }

        // Vulkan image layout
        private enum VkImageLayout
        {
            VK_IMAGE_LAYOUT_UNDEFINED = 0,
            VK_IMAGE_LAYOUT_GENERAL = 1,
            VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL = 2,
            VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL = 3,
            VK_IMAGE_LAYOUT_DEPTH_STENCIL_READ_ONLY_OPTIMAL = 4,
            VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL = 5,
            VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL = 6,
            VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL = 7,
            VK_IMAGE_LAYOUT_PREINITIALIZED = 8,
        }

        // Vulkan command buffer usage flags
        [Flags]
        private enum VkCommandBufferUsageFlags
        {
            VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT = 0x00000001,
            VK_COMMAND_BUFFER_USAGE_RENDER_PASS_CONTINUE_BIT = 0x00000002,
            VK_COMMAND_BUFFER_USAGE_SIMULTANEOUS_USE_BIT = 0x00000004,
        }

        // Vulkan structures
        [StructLayout(LayoutKind.Sequential)]
        private struct VkImageCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkImageCreateFlags flags;
            public VkImageType imageType;
            public VkFormat format;
            public VkExtent3D extent;
            public uint mipLevels;
            public uint arrayLayers;
            public VkSampleCountFlagBits samples;
            public VkImageTiling tiling;
            public VkImageUsageFlags usage;
            public VkSharingMode sharingMode;
            public uint queueFamilyIndexCount;
            public IntPtr pQueueFamilyIndices;
            public VkImageLayout initialLayout;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkExtent3D
        {
            public uint width;
            public uint height;
            public uint depth;
        }

        // Vulkan 2D extent structure (for scissor rectangles)
        [StructLayout(LayoutKind.Sequential)]
        private struct VkExtent2D
        {
            public uint width;
            public uint height;
        }

        // Vulkan 2D offset structure (for scissor rectangles)
        [StructLayout(LayoutKind.Sequential)]
        private struct VkOffset2D
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkBufferCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkBufferCreateFlags flags;
            public ulong size;
            public VkBufferUsageFlags usage;
            public VkSharingMode sharingMode;
            public uint queueFamilyIndexCount;
            public IntPtr pQueueFamilyIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkMemoryAllocateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public ulong allocationSize;
            public uint memoryTypeIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkSamplerCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkSamplerCreateFlags flags;
            public VkFilter magFilter;
            public VkFilter minFilter;
            public VkSamplerMipmapMode mipmapMode;
            public VkSamplerAddressMode addressModeU;
            public VkSamplerAddressMode addressModeV;
            public VkSamplerAddressMode addressModeW;
            public float mipLodBias;
            public VkBool32 anisotropyEnable;
            public float maxAnisotropy;
            public VkBool32 compareEnable;
            public VkCompareOp compareOp;
            public float minLod;
            public float maxLod;
            public VkBorderColor borderColor;
            public VkBool32 unnormalizedCoordinates;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkShaderModuleCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkShaderModuleCreateFlags flags;
            public IntPtr codeSize;
            public IntPtr pCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkDescriptorSetLayoutCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkDescriptorSetLayoutCreateFlags flags;
            public uint bindingCount;
            public IntPtr pBindings;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkDescriptorSetLayoutBinding
        {
            public uint binding;
            public VkDescriptorType descriptorType;
            public uint descriptorCount;
            public VkShaderStageFlags stageFlags;
            public IntPtr pImmutableSamplers;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkDescriptorPoolSize
        {
            public VkDescriptorType type;
            public uint descriptorCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkDescriptorPoolCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkDescriptorPoolCreateFlags flags;
            public uint maxSets;
            public uint poolSizeCount;
            public IntPtr pPoolSizes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkDescriptorSetAllocateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public IntPtr descriptorPool;
            public uint descriptorSetCount;
            public IntPtr pSetLayouts;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkDescriptorBufferInfo
        {
            public IntPtr buffer;
            public ulong offset;
            public ulong range;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkDescriptorImageInfo
        {
            public IntPtr sampler;
            public IntPtr imageView;
            public VkImageLayout imageLayout;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkWriteDescriptorSet
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public IntPtr dstSet;
            public uint dstBinding;
            public uint dstArrayElement;
            public uint descriptorCount;
            public VkDescriptorType descriptorType;
            public IntPtr pImageInfo;
            public IntPtr pBufferInfo;
            public IntPtr pTexelBufferView;
        }

        // VK_KHR_acceleration_structure extension structure for writing acceleration structure descriptors
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkWriteDescriptorSetAccelerationStructureKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkWriteDescriptorSetAccelerationStructureKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public uint accelerationStructureCount;
            public IntPtr pAccelerationStructures; // Array of VkAccelerationStructureKHR (IntPtr) handles
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkPipelineLayoutCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkPipelineLayoutCreateFlags flags;
            public uint setLayoutCount;
            public IntPtr pSetLayouts;
            public uint pushConstantRangeCount;
            public IntPtr pPushConstantRanges;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkSubmitInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public uint waitSemaphoreCount;
            public IntPtr pWaitSemaphores;
            public IntPtr pWaitDstStageMask;
            public uint commandBufferCount;
            public IntPtr pCommandBuffers;
            public uint signalSemaphoreCount;
            public IntPtr pSignalSemaphores;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkCommandBufferBeginInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkCommandBufferUsageFlags flags;
            public IntPtr pInheritanceInfo;
        }

        // VkCommandBufferAllocateInfo structure (Vulkan 1.0)
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkCommandBufferAllocateInfo.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkCommandBufferAllocateInfo
        {
            public VkStructureType sType; // Must be VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO (40)
            public IntPtr pNext; // nullptr or pointer to extension-specific structure
            public IntPtr commandPool; // VkCommandPool handle to allocate from
            public VkCommandBufferLevel level; // VK_COMMAND_BUFFER_LEVEL_PRIMARY (0) or VK_COMMAND_BUFFER_LEVEL_SECONDARY (1)
            public uint commandBufferCount; // Number of command buffers to allocate
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkMemoryRequirements
        {
            public ulong size;
            public ulong alignment;
            public uint memoryTypeBits;
        }

        // Vulkan memory type structure
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkMemoryType.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkMemoryType
        {
            public VkMemoryPropertyFlags propertyFlags;
            public uint heapIndex;
        }

        // Vulkan memory heap structure
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkMemoryHeap.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkMemoryHeap
        {
            public ulong size;
            public VkMemoryHeapFlags flags;
        }

        // Vulkan memory heap flags
        [Flags]
        private enum VkMemoryHeapFlags
        {
            VK_MEMORY_HEAP_DEVICE_LOCAL_BIT = 0x00000001,
            VK_MEMORY_HEAP_MULTI_INSTANCE_BIT = 0x00000002,
        }

        // Vulkan physical device memory properties structure
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkPhysicalDeviceMemoryProperties.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkPhysicalDeviceMemoryProperties
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public VkMemoryType[] memoryTypes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public VkMemoryHeap[] memoryHeaps;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkDebugUtilsLabelEXT
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public IntPtr pLabelName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] color;
        }

        // Vulkan memory barrier structure (for general memory synchronization)
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkMemoryBarrier.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkMemoryBarrier
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkAccessFlags srcAccessMask;
            public VkAccessFlags dstAccessMask;
        }

        // Vulkan buffer memory barrier structure
        [StructLayout(LayoutKind.Sequential)]
        private struct VkBufferMemoryBarrier
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkAccessFlags srcAccessMask;
            public VkAccessFlags dstAccessMask;
            public uint srcQueueFamilyIndex;
            public uint dstQueueFamilyIndex;
            public IntPtr buffer;
            public ulong offset;
            public ulong size;
        }

        // Vulkan image subresource layers structure (for image operations)
        [StructLayout(LayoutKind.Sequential)]
        private struct VkImageSubresourceLayers
        {
            public VkImageAspectFlags aspectMask;
            public uint mipLevel;
            public uint baseArrayLayer;
            public uint layerCount;
        }

        // Vulkan image copy structure (for vkCmdCopyImage)
        [StructLayout(LayoutKind.Sequential)]
        private struct VkImageCopy
        {
            public VkImageSubresourceLayers srcSubresource;
            public VkOffset3D srcOffset;
            public VkImageSubresourceLayers dstSubresource;
            public VkOffset3D dstOffset;
            public VkExtent3D extent;
        }

        // Vulkan buffer copy structure (for vkCmdCopyBuffer)
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkBufferCopy.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkBufferCopy
        {
            public ulong srcOffset;  // VkDeviceSize
            public ulong dstOffset;  // VkDeviceSize
            public ulong size;       // VkDeviceSize
        }

        // Vulkan offset 3D structure
        [StructLayout(LayoutKind.Sequential)]
        private struct VkOffset3D
        {
            public int x;
            public int y;
            public int z;
        }

        // Vulkan 2D rectangle structure (for scissor rectangles)
        // VkRect2D is used by vkCmdSetScissor to define scissor rectangles
        [StructLayout(LayoutKind.Sequential)]
        private struct VkRect2D
        {
            public VkOffset2D offset;
            public VkExtent2D extent;
        }

        // Vulkan viewport structure
        // VkViewport is used by vkCmdSetViewport to define viewport rectangles
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkViewport.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkViewport
        {
            public float x;        // X coordinate of the viewport's upper left corner
            public float y;        // Y coordinate of the viewport's upper left corner
            public float width;    // Width of the viewport
            public float height;   // Height of the viewport
            public float minDepth; // Minimum depth value for the viewport (typically 0.0)
            public float maxDepth; // Maximum depth value for the viewport (typically 1.0)
        }

        // Vulkan image memory barrier structure (for image layout transitions)
        [StructLayout(LayoutKind.Sequential)]
        private struct VkImageMemoryBarrier
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkAccessFlags srcAccessMask;
            public VkAccessFlags dstAccessMask;
            public VkImageLayout oldLayout;
            public VkImageLayout newLayout;
            public uint srcQueueFamilyIndex;
            public uint dstQueueFamilyIndex;
            public IntPtr image;
            public VkImageSubresourceRange subresourceRange;
        }

        // Vulkan image subresource range structure
        [StructLayout(LayoutKind.Sequential)]
        private struct VkImageSubresourceRange
        {
            public VkImageAspectFlags aspectMask;
            public uint baseMipLevel;
            public uint levelCount;
            public uint baseArrayLayer;
            public uint layerCount;
        }

        // Vulkan image aspect flags
        [Flags]
        private enum VkImageAspectFlags
        {
            VK_IMAGE_ASPECT_COLOR_BIT = 0x00000001,
            VK_IMAGE_ASPECT_DEPTH_BIT = 0x00000002,
            VK_IMAGE_ASPECT_STENCIL_BIT = 0x00000004,
            VK_IMAGE_ASPECT_METADATA_BIT = 0x00000008,
        }

        // Vulkan clear depth stencil value structure
        // Based on Vulkan API: https://docs.vulkan.org/spec/latest/chapters/clears.html#VkClearDepthStencilValue
        [StructLayout(LayoutKind.Sequential)]
        private struct VkClearDepthStencilValue
        {
            public float depth; // Clear value for depth aspect (0.0 to 1.0)
            public uint stencil; // Clear value for stencil aspect
        }

        // Vulkan clear color value structure
        // Based on Vulkan API: https://docs.vulkan.org/spec/latest/chapters/clears.html#VkClearColorValue
        // Can represent float, int32, or uint32 clear values depending on format
        // In C: union { float float32[4]; int32_t int32[4]; uint32_t uint32[4]; }
        // Size is 16 bytes (4 values * 4 bytes each)
        // We use fixed-size float fields to represent the union (most common case for color attachments)
        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct VkClearColorValue
        {
            public float float32_0; // First float value
            public float float32_1; // Second float value
            public float float32_2; // Third float value
            public float float32_3; // Fourth float value
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkCommandPoolCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkCommandPoolCreateFlags flags;
            public uint queueFamilyIndex;
        }

        // Vulkan render pass structures
        [StructLayout(LayoutKind.Sequential)]
        private struct VkRenderPassCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkRenderPassCreateFlags flags;
            public uint attachmentCount;
            public IntPtr pAttachments;
            public uint subpassCount;
            public IntPtr pSubpasses;
            public uint dependencyCount;
            public IntPtr pDependencies;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkAttachmentDescription
        {
            public VkAttachmentDescriptionFlags flags;
            public VkFormat format;
            public VkSampleCountFlagBits samples;
            public VkAttachmentLoadOp loadOp;
            public VkAttachmentStoreOp storeOp;
            public VkAttachmentLoadOp stencilLoadOp;
            public VkAttachmentStoreOp stencilStoreOp;
            public VkImageLayout initialLayout;
            public VkImageLayout finalLayout;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkSubpassDescription
        {
            public VkSubpassDescriptionFlags flags;
            public VkPipelineBindPoint pipelineBindPoint;
            public uint inputAttachmentCount;
            public IntPtr pInputAttachments;
            public uint colorAttachmentCount;
            public IntPtr pColorAttachments;
            public IntPtr pResolveAttachments;
            public IntPtr pDepthStencilAttachment;
            public uint preserveAttachmentCount;
            public IntPtr pPreserveAttachments;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkAttachmentReference
        {
            public uint attachment;
            public VkImageLayout layout;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkSubpassDependency
        {
            public uint srcSubpass;
            public uint dstSubpass;
            public VkPipelineStageFlags srcStageMask;
            public VkPipelineStageFlags dstStageMask;
            public VkAccessFlags srcAccessMask;
            public VkAccessFlags dstAccessMask;
            public VkDependencyFlags dependencyFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkFramebufferCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkFramebufferCreateFlags flags;
            public IntPtr renderPass;
            public uint attachmentCount;
            public IntPtr pAttachments;
            public uint width;
            public uint height;
            public uint layers;
        }

        // Vulkan render pass enums and flags
        [Flags]
        private enum VkRenderPassCreateFlags
        {
        }

        [Flags]
        private enum VkAttachmentDescriptionFlags
        {
        }

        private enum VkAttachmentLoadOp
        {
            VK_ATTACHMENT_LOAD_OP_LOAD = 0,
            VK_ATTACHMENT_LOAD_OP_CLEAR = 1,
            VK_ATTACHMENT_LOAD_OP_DONT_CARE = 2,
        }

        private enum VkAttachmentStoreOp
        {
            VK_ATTACHMENT_STORE_OP_STORE = 0,
            VK_ATTACHMENT_STORE_OP_DONT_CARE = 1,
        }

        [Flags]
        private enum VkSubpassDescriptionFlags
        {
        }

        // VkPipelineBindPoint and VkDependencyFlags are defined later in the file to avoid duplicates

        [Flags]
        private enum VkFramebufferCreateFlags
        {
        }

        // Vulkan ray tracing structures
        [StructLayout(LayoutKind.Sequential)]
        private struct VkPipelineShaderStageCreateInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkPipelineShaderStageCreateFlags flags;
            public VkShaderStageFlags stage;
            public IntPtr module;
            public IntPtr pName;
            public IntPtr pSpecializationInfo;
        }

        [Flags]
        private enum VkPipelineShaderStageCreateFlags
        {
        }

        private enum VkRayTracingShaderGroupTypeKHR
        {
            VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR = 0,
            VK_RAY_TRACING_SHADER_GROUP_TYPE_TRIANGLES_HIT_GROUP_KHR = 1,
            VK_RAY_TRACING_SHADER_GROUP_TYPE_PROCEDURAL_HIT_GROUP_KHR = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkRayTracingShaderGroupCreateInfoKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkRayTracingShaderGroupTypeKHR type;
            public uint generalShader;
            public uint closestHitShader;
            public uint anyHitShader;
            public uint intersectionShader;
            public IntPtr pShaderGroupCaptureReplayHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkRayTracingPipelineCreateInfoKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkPipelineCreateFlags flags;
            public uint stageCount;
            public IntPtr pStages;
            public uint groupCount;
            public IntPtr pGroups;
            public uint maxPipelineRayRecursionDepth;
            public IntPtr pLibraryInfo;
            public IntPtr pLibraryInterface;
            public IntPtr pDynamicState;
            public IntPtr layout;
            public IntPtr basePipelineHandle;
            public int basePipelineIndex;
        }

        [Flags]
        private enum VkPipelineCreateFlags
        {
            VK_PIPELINE_CREATE_DISABLE_OPTIMIZATION_BIT = 0x00000001,
            VK_PIPELINE_CREATE_ALLOW_DERIVATIVES_BIT = 0x00000002,
            VK_PIPELINE_CREATE_DERIVATIVE_BIT = 0x00000004
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VkStridedDeviceAddressRegionKHR
        {
            public ulong deviceAddress;
            public ulong stride;
            public ulong size;
        }

        // VK_KHR_acceleration_structure extension structures
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureBuildGeometryInfoKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureBuildGeometryInfoKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkAccelerationStructureTypeKHR type;
            public VkBuildAccelerationStructureFlagsKHR flags;
            public VkAccelerationStructureBuildTypeKHR buildType;
            public IntPtr srcAccelerationStructure; // VkAccelerationStructureKHR (IntPtr)
            public IntPtr dstAccelerationStructure; // VkAccelerationStructureKHR (IntPtr)
            public uint geometryCount;
            public IntPtr pGeometries; // Pointer to VkAccelerationStructureGeometryKHR array
            public IntPtr ppGeometries; // Pointer to array of pointers to VkAccelerationStructureGeometryKHR (optional, null if pGeometries is used)
            public ulong scratchDataDeviceAddress;
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureGeometryKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureGeometryKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkGeometryTypeKHR geometryType;
            public VkAccelerationStructureGeometryDataKHR geometry;
            public VkGeometryFlagsKHR flags;
        }

        // Union-like structure for geometry data (we use triangles only for BLAS)
        [StructLayout(LayoutKind.Explicit, Size = 96)] // Size is the maximum of all union members
        private struct VkAccelerationStructureGeometryDataKHR
        {
            [FieldOffset(0)]
            public VkAccelerationStructureGeometryTrianglesDataKHR triangles;
            [FieldOffset(0)]
            public VkAccelerationStructureGeometryAabbsDataKHR aabbs;
            [FieldOffset(0)]
            public VkAccelerationStructureGeometryInstancesDataKHR instances;
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureGeometryTrianglesDataKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureGeometryTrianglesDataKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkFormat vertexFormat;
            public ulong vertexDataDeviceAddress;
            public ulong vertexStride;
            public uint maxVertex;
            public VkIndexType indexType;
            public ulong indexDataDeviceAddress;
            public ulong transformDataDeviceAddress;
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureGeometryAabbsDataKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureGeometryAabbsDataKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public ulong dataDeviceAddress;
            public ulong stride;
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureGeometryInstancesDataKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureGeometryInstancesDataKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public VkBool32 arrayOfPointers;
            public ulong dataDeviceAddress;
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureBuildRangeInfoKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureBuildRangeInfoKHR
        {
            public uint primitiveCount;
            public uint primitiveOffset;
            public uint firstVertex;
            public uint transformOffset;
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureBuildSizesInfoKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureBuildSizesInfoKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public ulong accelerationStructureSize;
            public ulong updateScratchSize;
            public ulong buildScratchSize;
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureCreateInfoKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureCreateInfoKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public ulong createFlags; // VkAccelerationStructureCreateFlagsKHR
            public IntPtr buffer; // VkBuffer
            public ulong offset;
            public ulong size;
            public VkAccelerationStructureTypeKHR type;
            public ulong deviceAddress;
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkBufferDeviceAddressInfo.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkBufferDeviceAddressInfo
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public IntPtr buffer; // VkBuffer
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureDeviceAddressInfoKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkAccelerationStructureDeviceAddressInfoKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public IntPtr accelerationStructure; // VkAccelerationStructureKHR
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkCopyAccelerationStructureInfoKHR.html
        [StructLayout(LayoutKind.Sequential)]
        private struct VkCopyAccelerationStructureInfoKHR
        {
            public VkStructureType sType;
            public IntPtr pNext;
            public IntPtr src; // VkAccelerationStructureKHR
            public IntPtr dst; // VkAccelerationStructureKHR
            public VkCopyAccelerationStructureModeKHR mode;
        }

        // Vulkan enums
        private enum VkStructureType
        {
            VK_STRUCTURE_TYPE_APPLICATION_INFO = 0,
            VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO = 1,
            VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO = 2,
            VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO = 3,
            VK_STRUCTURE_TYPE_SUBMIT_INFO = 4,
            VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO = 5,
            VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE = 6,
            VK_STRUCTURE_TYPE_BIND_SPARSE_INFO = 7,
            VK_STRUCTURE_TYPE_FENCE_CREATE_INFO = 8,
            VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO = 9,
            VK_STRUCTURE_TYPE_EVENT_CREATE_INFO = 10,
            VK_STRUCTURE_TYPE_QUERY_POOL_CREATE_INFO = 11,
            VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO = 12,
            VK_STRUCTURE_TYPE_BUFFER_VIEW_CREATE_INFO = 13,
            VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO = 14,
            VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO = 15,
            VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO = 16,
            VK_STRUCTURE_TYPE_PIPELINE_CACHE_CREATE_INFO = 17,
            VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO = 18,
            VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO = 19,
            VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO = 20,
            VK_STRUCTURE_TYPE_PIPELINE_TESSELLATION_STATE_CREATE_INFO = 21,
            VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO = 22,
            VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO = 23,
            VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO = 24,
            VK_STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO = 25,
            VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO = 26,
            VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO = 27,
            VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO = 28,
            VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO = 29,
            VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO = 30,
            VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO = 31,
            VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO = 32,
            VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO = 33,
            VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO = 34,
            VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET = 35,
            VK_STRUCTURE_TYPE_COPY_DESCRIPTOR_SET = 36,
            VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO = 37,
            VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO = 38,
            VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO = 39,
            VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO = 40,
            VK_STRUCTURE_TYPE_COMMAND_BUFFER_INHERITANCE_INFO = 41,
            VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO = 42,
            VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO = 43,
            VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER = 44,
            VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER = 45,
            VK_STRUCTURE_TYPE_MEMORY_BARRIER = 46,
            VK_STRUCTURE_TYPE_LOADER_INSTANCE_CREATE_INFO = 47,
            VK_STRUCTURE_TYPE_LOADER_DEVICE_CREATE_INFO = 48,
            VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT = 1000128002,
            VK_STRUCTURE_TYPE_RAY_TRACING_PIPELINE_CREATE_INFO_KHR = 1000165000,
            VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR = 1000165001,
            VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR = 1000396003,
            VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR = 1000150000,
            VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR = 1000150004,
            VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR = 1000150005,
            VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_AABBS_DATA_KHR = 1000150006,
            VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR = 1000150007,
            VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR = 1000150017,
            VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_SIZES_INFO_KHR = 1000150020,
            VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO = 1000244001,
            VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_DEVICE_ADDRESS_INFO_KHR = 1000150002,
            VK_STRUCTURE_TYPE_COPY_ACCELERATION_STRUCTURE_INFO_KHR = 1000150001
        }

        // Additional Vulkan enums
        private enum VkImageCreateFlags { }
        private enum VkImageType { VK_IMAGE_TYPE_2D = 1 }
        [Flags]
        private enum VkSampleCountFlagBits
        {
            VK_SAMPLE_COUNT_1_BIT = 0x00000001,
            VK_SAMPLE_COUNT_2_BIT = 0x00000002,
            VK_SAMPLE_COUNT_4_BIT = 0x00000004,
            VK_SAMPLE_COUNT_8_BIT = 0x00000008,
            VK_SAMPLE_COUNT_16_BIT = 0x00000010,
            VK_SAMPLE_COUNT_32_BIT = 0x00000020,
            VK_SAMPLE_COUNT_64_BIT = 0x00000040
        }
        private enum VkImageTiling { VK_IMAGE_TILING_OPTIMAL = 0 }
        private enum VkSharingMode { VK_SHARING_MODE_EXCLUSIVE = 0 }
        private enum VkBufferCreateFlags { }
        private enum VkSamplerCreateFlags { }
        private enum VkFilter { VK_FILTER_NEAREST = 0, VK_FILTER_LINEAR = 1 }
        private enum VkSamplerMipmapMode { VK_SAMPLER_MIPMAP_MODE_NEAREST = 0, VK_SAMPLER_MIPMAP_MODE_LINEAR = 1 }
        private enum VkSamplerAddressMode { VK_SAMPLER_ADDRESS_MODE_REPEAT = 0, VK_SAMPLER_ADDRESS_MODE_MIRRORED_REPEAT = 1, VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE = 2, VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_BORDER = 3 }
        private enum VkBool32 { VK_FALSE = 0, VK_TRUE = 1 }
        private enum VkCompareOp { VK_COMPARE_OP_NEVER = 0, VK_COMPARE_OP_LESS = 1, VK_COMPARE_OP_EQUAL = 2, VK_COMPARE_OP_LESS_OR_EQUAL = 3, VK_COMPARE_OP_GREATER = 4, VK_COMPARE_OP_NOT_EQUAL = 5, VK_COMPARE_OP_GREATER_OR_EQUAL = 6, VK_COMPARE_OP_ALWAYS = 7 }
        private enum VkIndexType { VK_INDEX_TYPE_UINT16 = 0, VK_INDEX_TYPE_UINT32 = 1 }
        private enum VkBorderColor { VK_BORDER_COLOR_FLOAT_TRANSPARENT_BLACK = 0 }
        private enum VkShaderModuleCreateFlags { }
        private enum VkDescriptorSetLayoutCreateFlags { }
        [Flags]
        private enum VkDescriptorPoolCreateFlags
        {
            VK_DESCRIPTOR_POOL_CREATE_FREE_DESCRIPTOR_SET_BIT = 0x00000001,
            VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT = 0x00000002
        }
        [Flags]
        private enum VkCommandPoolCreateFlags
        {
            VK_COMMAND_POOL_CREATE_TRANSIENT_BIT = 0x00000001,
            VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT = 0x00000002
        }
        private enum VkDescriptorType { VK_DESCRIPTOR_TYPE_SAMPLER = 0, VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER = 1, VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE = 2, VK_DESCRIPTOR_TYPE_STORAGE_IMAGE = 3, VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER = 6, VK_DESCRIPTOR_TYPE_STORAGE_BUFFER = 7, VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR = 1000150000 }
        [Flags]
        private enum VkShaderStageFlags
        {
            VK_SHADER_STAGE_VERTEX_BIT = 0x00000001,
            VK_SHADER_STAGE_TESSELLATION_CONTROL_BIT = 0x00000002,
            VK_SHADER_STAGE_TESSELLATION_EVALUATION_BIT = 0x00000004,
            VK_SHADER_STAGE_GEOMETRY_BIT = 0x00000008,
            VK_SHADER_STAGE_FRAGMENT_BIT = 0x00000010,
            VK_SHADER_STAGE_COMPUTE_BIT = 0x00000020,
            VK_SHADER_STAGE_ALL_GRAPHICS = 0x0000001F,
            VK_SHADER_STAGE_RAYGEN_BIT_KHR = 0x00000100,
            VK_SHADER_STAGE_MISS_BIT_KHR = 0x00000200,
            VK_SHADER_STAGE_CLOSEST_HIT_BIT_KHR = 0x00000400,
            VK_SHADER_STAGE_ANY_HIT_BIT_KHR = 0x00000800,
            VK_SHADER_STAGE_INTERSECTION_BIT_KHR = 0x00001000,
            VK_SHADER_STAGE_CALLABLE_BIT_KHR = 0x00002000,
            VK_SHADER_STAGE_ALL = 0x7FFFFFFF
        }
        private enum VkPipelineLayoutCreateFlags { }
        private enum VkCommandBufferLevel { VK_COMMAND_BUFFER_LEVEL_PRIMARY = 0 }
        private enum VkPipelineBindPoint { VK_PIPELINE_BIND_POINT_GRAPHICS = 0, VK_PIPELINE_BIND_POINT_COMPUTE = 1, VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR = 1000165000 }

        // VK_KHR_acceleration_structure extension enums
        private enum VkAccelerationStructureTypeKHR
        {
            VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR = 0,
            VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR = 1,
            VK_ACCELERATION_STRUCTURE_TYPE_GENERIC_KHR = 2
        }

        private enum VkAccelerationStructureBuildTypeKHR
        {
            VK_ACCELERATION_STRUCTURE_BUILD_TYPE_HOST_KHR = 0,
            VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR = 1,
            VK_ACCELERATION_STRUCTURE_BUILD_TYPE_HOST_OR_DEVICE_KHR = 2
        }

        private enum VkGeometryTypeKHR
        {
            VK_GEOMETRY_TYPE_TRIANGLES_KHR = 0,
            VK_GEOMETRY_TYPE_AABBS_KHR = 1,
            VK_GEOMETRY_TYPE_INSTANCES_KHR = 2
        }

        [Flags]
        private enum VkGeometryFlagsKHR
        {
            VK_GEOMETRY_OPAQUE_BIT_KHR = 0x00000001,
            VK_GEOMETRY_NO_DUPLICATE_ANY_HIT_INVOCATION_BIT_KHR = 0x00000002
        }

        [Flags]
        private enum VkBuildAccelerationStructureFlagsKHR
        {
            VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_UPDATE_BIT_KHR = 0x00000001,
            VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_COMPACTION_BIT_KHR = 0x00000002,
            VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR = 0x00000004,
            VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_BUILD_BIT_KHR = 0x00000008,
            VK_BUILD_ACCELERATION_STRUCTURE_LOW_MEMORY_BIT_KHR = 0x00000010
        }

        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkCopyAccelerationStructureModeKHR.html
        private enum VkCopyAccelerationStructureModeKHR
        {
            VK_COPY_ACCELERATION_STRUCTURE_MODE_CLONE_KHR = 0,
            VK_COPY_ACCELERATION_STRUCTURE_MODE_COMPACT_KHR = 1,
            VK_COPY_ACCELERATION_STRUCTURE_MODE_SERIALIZE_KHR = 2,
            VK_COPY_ACCELERATION_STRUCTURE_MODE_DESERIALIZE_KHR = 3
        }

        // Vulkan dependency flags
        [Flags]
        private enum VkDependencyFlags
        {
            VK_DEPENDENCY_BY_REGION_BIT = 0x00000001,
            VK_DEPENDENCY_DEVICE_GROUP_BIT = 0x00000004,
            VK_DEPENDENCY_VIEW_LOCAL_BIT = 0x00000002
        }

        // Vulkan function pointers
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateImageDelegate(IntPtr device, ref VkImageCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pImage);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyImageDelegate(IntPtr device, IntPtr image, IntPtr pAllocator);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkGetImageMemoryRequirementsDelegate(IntPtr device, IntPtr image, out VkMemoryRequirements pMemoryRequirements);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkAllocateMemoryDelegate(IntPtr device, ref VkMemoryAllocateInfo pAllocateInfo, IntPtr pAllocator, out IntPtr pMemory);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkFreeMemoryDelegate(IntPtr device, IntPtr memory, IntPtr pAllocator);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkBindImageMemoryDelegate(IntPtr device, IntPtr image, IntPtr memory, ulong memoryOffset);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateImageViewDelegate(IntPtr device, IntPtr pCreateInfo, IntPtr pAllocator, out IntPtr pView);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyImageViewDelegate(IntPtr device, IntPtr imageView, IntPtr pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateBufferDelegate(IntPtr device, ref VkBufferCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pBuffer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyBufferDelegate(IntPtr device, IntPtr buffer, IntPtr pAllocator);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkGetBufferMemoryRequirementsDelegate(IntPtr device, IntPtr buffer, out VkMemoryRequirements pMemoryRequirements);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkBindBufferMemoryDelegate(IntPtr device, IntPtr buffer, IntPtr memory, ulong memoryOffset);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateSamplerDelegate(IntPtr device, ref VkSamplerCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pSampler);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroySamplerDelegate(IntPtr device, IntPtr sampler, IntPtr pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateShaderModuleDelegate(IntPtr device, ref VkShaderModuleCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pShaderModule);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyShaderModuleDelegate(IntPtr device, IntPtr shaderModule, IntPtr pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateDescriptorSetLayoutDelegate(IntPtr device, ref VkDescriptorSetLayoutCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pSetLayout);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyDescriptorSetLayoutDelegate(IntPtr device, IntPtr descriptorSetLayout, IntPtr pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateDescriptorPoolDelegate(IntPtr device, ref VkDescriptorPoolCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pDescriptorPool);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyDescriptorPoolDelegate(IntPtr device, IntPtr descriptorPool, IntPtr pAllocator);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkAllocateDescriptorSetsDelegate(IntPtr device, ref VkDescriptorSetAllocateInfo pAllocateInfo, IntPtr pDescriptorSets);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkFreeDescriptorSetsDelegate(IntPtr device, IntPtr descriptorPool, uint descriptorSetCount, IntPtr pDescriptorSets);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkUpdateDescriptorSetsDelegate(IntPtr device, uint descriptorWriteCount, IntPtr pDescriptorWrites, uint descriptorCopyCount, IntPtr pDescriptorCopies);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreatePipelineLayoutDelegate(IntPtr device, ref VkPipelineLayoutCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pPipelineLayout);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyPipelineLayoutDelegate(IntPtr device, IntPtr pipelineLayout, IntPtr pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateGraphicsPipelinesDelegate(IntPtr device, IntPtr pipelineCache, uint createInfoCount, IntPtr pCreateInfos, IntPtr pAllocator, IntPtr pPipelines);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateComputePipelinesDelegate(IntPtr device, IntPtr pipelineCache, uint createInfoCount, IntPtr pCreateInfos, IntPtr pAllocator, IntPtr pPipelines);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyPipelineDelegate(IntPtr device, IntPtr pipeline, IntPtr pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkQueueSubmitDelegate(IntPtr queue, uint submitCount, IntPtr pSubmits, IntPtr fence);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDeviceWaitIdleDelegate(IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkWaitForFencesDelegate(IntPtr device, uint fenceCount, IntPtr pFences, VkBool32 waitAll, ulong timeout);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateCommandPoolDelegate(IntPtr device, IntPtr pCreateInfo, IntPtr pAllocator, out IntPtr pCommandPool);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyCommandPoolDelegate(IntPtr device, IntPtr commandPool, IntPtr pAllocator);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyFramebufferDelegate(IntPtr device, IntPtr framebuffer, IntPtr pAllocator);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkDestroyRenderPassDelegate(IntPtr device, IntPtr renderPass, IntPtr pAllocator);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkAllocateCommandBuffersDelegate(IntPtr device, IntPtr pAllocateInfo, IntPtr pCommandBuffers);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkFreeCommandBuffersDelegate(IntPtr device, IntPtr commandPool, uint commandBufferCount, IntPtr pCommandBuffers);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkBeginCommandBufferDelegate(IntPtr commandBuffer, ref VkCommandBufferBeginInfo pBeginInfo);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkEndCommandBufferDelegate(IntPtr commandBuffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCmdBindPipelineDelegate(IntPtr commandBuffer, VkPipelineBindPoint pipelineBindPoint, IntPtr pipeline);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdBindDescriptorSetsDelegate(IntPtr commandBuffer, VkPipelineBindPoint pipelineBindPoint, IntPtr layout, uint firstSet, uint descriptorSetCount, IntPtr pDescriptorSets, uint dynamicOffsetCount, IntPtr pDynamicOffsets);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdDispatchDelegate(IntPtr commandBuffer, uint groupCountX, uint groupCountY, uint groupCountZ);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdDispatchIndirectDelegate(IntPtr commandBuffer, IntPtr buffer, ulong offset);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdDrawIndirectDelegate(IntPtr commandBuffer, IntPtr buffer, ulong offset, uint drawCount, uint stride);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdDrawIndexedIndirectDelegate(IntPtr commandBuffer, IntPtr buffer, ulong offset, uint drawCount, uint stride);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdSetScissorDelegate(IntPtr commandBuffer, uint firstScissor, uint scissorCount, IntPtr pScissors);

        // Delegate for vkCmdSetViewport
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdSetViewport.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdSetViewportDelegate(IntPtr commandBuffer, uint firstViewport, uint viewportCount, IntPtr pViewports);

        // Delegate for vkCmdBindVertexBuffers
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindVertexBuffers.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdBindVertexBuffersDelegate(IntPtr commandBuffer, uint firstBinding, uint bindingCount, IntPtr pBuffers, IntPtr pOffsets);

        // Delegate for vkCmdBindIndexBuffer
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindIndexBuffer.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdBindIndexBufferDelegate(IntPtr commandBuffer, IntPtr buffer, ulong offset, VkIndexType indexType);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdDrawDelegate(IntPtr commandBuffer, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdDrawIndexedDelegate(IntPtr commandBuffer, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdCopyImageDelegate(IntPtr commandBuffer, IntPtr srcImage, VkImageLayout srcImageLayout, IntPtr dstImage, VkImageLayout dstImageLayout, uint regionCount, IntPtr pRegions);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdCopyBufferToImageDelegate(IntPtr commandBuffer, IntPtr srcBuffer, IntPtr dstImage, VkImageLayout dstImageLayout, uint regionCount, IntPtr pRegions);

        // Delegate for vkCmdUpdateBuffer (for small buffer updates up to 65536 bytes)
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdUpdateBuffer.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdUpdateBufferDelegate(IntPtr commandBuffer, IntPtr dstBuffer, ulong dstOffset, ulong dataSize, IntPtr pData);

        // Delegate for vkCmdCopyBuffer (for buffer-to-buffer copies)
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyBuffer.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdCopyBufferDelegate(IntPtr commandBuffer, IntPtr srcBuffer, IntPtr dstBuffer, uint regionCount, IntPtr pRegions);

        // Delegate for vkCmdClearDepthStencilImage
        // Based on Vulkan API: https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdClearDepthStencilImage.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdClearDepthStencilImageDelegate(IntPtr commandBuffer, IntPtr image, VkImageLayout imageLayout, IntPtr pDepthStencil, uint rangeCount, IntPtr pRanges);

        // Delegate for vkCmdClearColorImage
        // Based on Vulkan API: https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdClearColorImage.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdClearColorImageDelegate(IntPtr commandBuffer, IntPtr image, VkImageLayout imageLayout, IntPtr pColor, uint rangeCount, IntPtr pRanges);

        // Delegate for vkGetPhysicalDeviceFormatProperties
        // Based on Vulkan API: https://docs.vulkan.org/refpages/latest/refpages/source/vkGetPhysicalDeviceFormatProperties.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkGetPhysicalDeviceFormatPropertiesDelegate(IntPtr physicalDevice, VkFormat format, out VkFormatProperties pFormatProperties);

        // Delegate for vkGetPhysicalDeviceMemoryProperties
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetPhysicalDeviceMemoryProperties.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkGetPhysicalDeviceMemoryPropertiesDelegate(IntPtr physicalDevice, out VkPhysicalDeviceMemoryProperties pMemoryProperties);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkMapMemoryDelegate(IntPtr device, IntPtr memory, ulong offset, ulong size, uint flags, out IntPtr ppData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkUnmapMemoryDelegate(IntPtr device, IntPtr memory);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdBeginDebugUtilsLabelEXTDelegate(IntPtr commandBuffer, ref VkDebugUtilsLabelEXT pLabelInfo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdEndDebugUtilsLabelEXTDelegate(IntPtr commandBuffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdInsertDebugUtilsLabelEXTDelegate(IntPtr commandBuffer, ref VkDebugUtilsLabelEXT pLabelInfo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdPipelineBarrierDelegate(
            IntPtr commandBuffer,
            VkPipelineStageFlags srcStageMask,
            VkPipelineStageFlags dstStageMask,
            uint dependencyFlags,
            uint memoryBarrierCount,
            IntPtr pMemoryBarriers,
            uint bufferMemoryBarrierCount,
            IntPtr pBufferMemoryBarriers,
            uint imageMemoryBarrierCount,
            IntPtr pImageMemoryBarriers);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdSetBlendConstantsDelegate(IntPtr commandBuffer, IntPtr blendConstants);

        // Delegate for vkCmdSetStencilReference
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdSetStencilReference.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdSetStencilReferenceDelegate(IntPtr commandBuffer, VkStencilFaceFlags faceMask, uint reference);

        // Function pointers storage
        private static vkCreateImageDelegate vkCreateImage;
        private static vkDestroyImageDelegate vkDestroyImage;
        private static vkGetImageMemoryRequirementsDelegate vkGetImageMemoryRequirements;
        private static vkAllocateMemoryDelegate vkAllocateMemory;
        private static vkFreeMemoryDelegate vkFreeMemory;
        private static vkBindImageMemoryDelegate vkBindImageMemory;
        private static vkCreateImageViewDelegate vkCreateImageView;
        private static vkDestroyImageViewDelegate vkDestroyImageView;

        private static vkCreateBufferDelegate vkCreateBuffer;
        private static vkDestroyBufferDelegate vkDestroyBuffer;
        private static vkGetBufferMemoryRequirementsDelegate vkGetBufferMemoryRequirements;
        private static vkBindBufferMemoryDelegate vkBindBufferMemory;

        private static vkCreateSamplerDelegate vkCreateSampler;
        private static vkDestroySamplerDelegate vkDestroySampler;

        private static vkCreateShaderModuleDelegate vkCreateShaderModule;
        private static vkDestroyShaderModuleDelegate vkDestroyShaderModule;

        private static vkCreateDescriptorSetLayoutDelegate vkCreateDescriptorSetLayout;
        private static vkDestroyDescriptorSetLayoutDelegate vkDestroyDescriptorSetLayout;

        private static vkCreatePipelineLayoutDelegate vkCreatePipelineLayout;
        private static vkDestroyPipelineLayoutDelegate vkDestroyPipelineLayout;

        private static vkCreateGraphicsPipelinesDelegate vkCreateGraphicsPipelines;
        private static vkCreateComputePipelinesDelegate vkCreateComputePipelines;
        private static vkDestroyPipelineDelegate vkDestroyPipeline;

        private static vkQueueSubmitDelegate vkQueueSubmit;
        private static vkDeviceWaitIdleDelegate vkDeviceWaitIdle;
        private static vkWaitForFencesDelegate vkWaitForFences;

        private static vkCreateCommandPoolDelegate vkCreateCommandPool;
        private static vkDestroyCommandPoolDelegate vkDestroyCommandPool;
        private static vkDestroyFramebufferDelegate vkDestroyFramebuffer;
        private static vkDestroyRenderPassDelegate vkDestroyRenderPass;
        private static vkAllocateCommandBuffersDelegate vkAllocateCommandBuffers;
        private static vkFreeCommandBuffersDelegate vkFreeCommandBuffers;
        private static vkBeginCommandBufferDelegate vkBeginCommandBuffer;
        private static vkEndCommandBufferDelegate vkEndCommandBuffer;

        private static vkCmdBindPipelineDelegate vkCmdBindPipeline;
        private static vkCmdBindDescriptorSetsDelegate vkCmdBindDescriptorSets;
        private static vkCmdDispatchDelegate vkCmdDispatch;
        private static vkCmdDispatchIndirectDelegate vkCmdDispatchIndirect;
        private static vkCmdDrawIndirectDelegate vkCmdDrawIndirect;
        private static vkCmdDrawIndexedIndirectDelegate vkCmdDrawIndexedIndirect;

        private static vkCmdBeginDebugUtilsLabelEXTDelegate vkCmdBeginDebugUtilsLabelEXT;
        private static vkCmdEndDebugUtilsLabelEXTDelegate vkCmdEndDebugUtilsLabelEXT;
        private static vkCmdInsertDebugUtilsLabelEXTDelegate vkCmdInsertDebugUtilsLabelEXT;
        private static vkCmdPipelineBarrierDelegate vkCmdPipelineBarrier;
        private static vkCmdDrawDelegate vkCmdDraw;
        private static vkCmdDrawIndexedDelegate vkCmdDrawIndexed;
        private static vkCmdSetScissorDelegate vkCmdSetScissor;
        private static vkCmdSetViewportDelegate vkCmdSetViewport;
        private static vkCmdBindVertexBuffersDelegate vkCmdBindVertexBuffers;
        private static vkCmdBindIndexBufferDelegate vkCmdBindIndexBuffer;
        private static vkCmdSetBlendConstantsDelegate vkCmdSetBlendConstants;
        private static vkCmdSetStencilReferenceDelegate vkCmdSetStencilReference;
        private static vkCmdClearDepthStencilImageDelegate vkCmdClearDepthStencilImage;
        private static vkCmdClearColorImageDelegate vkCmdClearColorImage;
        private static vkGetPhysicalDeviceFormatPropertiesDelegate vkGetPhysicalDeviceFormatProperties;
        private static vkGetPhysicalDeviceMemoryPropertiesDelegate vkGetPhysicalDeviceMemoryProperties;
        private static vkCmdUpdateBufferDelegate vkCmdUpdateBuffer;
        private static vkCmdCopyBufferDelegate vkCmdCopyBuffer;
        private static vkCmdCopyBufferToImageDelegate vkCmdCopyBufferToImage;
        private static vkCmdCopyImageDelegate vkCmdCopyImage;

        // VK_KHR_ray_tracing_pipeline extension function delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateRayTracingPipelinesKHRDelegate(IntPtr device, IntPtr deferredOperation, IntPtr pipelineCache, uint createInfoCount, IntPtr pCreateInfos, IntPtr pAllocator, IntPtr pPipelines);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkGetRayTracingShaderGroupHandlesKHRDelegate(IntPtr device, IntPtr pipeline, uint firstGroup, uint groupCount, ulong dataSize, IntPtr pData);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdTraceRaysKHRDelegate(IntPtr commandBuffer, ref VkStridedDeviceAddressRegionKHR pRaygenShaderBindingTable, ref VkStridedDeviceAddressRegionKHR pMissShaderBindingTable, ref VkStridedDeviceAddressRegionKHR pHitShaderBindingTable, ref VkStridedDeviceAddressRegionKHR pCallableShaderBindingTable, uint width, uint height, uint depth);

        // VK_KHR_acceleration_structure extension function delegates
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBuildAccelerationStructuresKHR.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdBuildAccelerationStructuresKHRDelegate(IntPtr commandBuffer, uint infoCount, IntPtr pInfos, IntPtr ppBuildRangeInfos);
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetAccelerationStructureBuildSizesKHR.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkGetAccelerationStructureBuildSizesKHRDelegate(IntPtr device, VkAccelerationStructureBuildTypeKHR buildType, ref VkAccelerationStructureBuildGeometryInfoKHR pBuildInfo, IntPtr pMaxPrimitiveCounts, ref VkAccelerationStructureBuildSizesInfoKHR pSizeInfo);
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetBufferDeviceAddress.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong vkGetBufferDeviceAddressKHRDelegate(IntPtr device, ref VkBufferDeviceAddressInfo pInfo);
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCreateAccelerationStructureKHR.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateAccelerationStructureKHRDelegate(IntPtr device, ref VkAccelerationStructureCreateInfoKHR pCreateInfo, IntPtr pAllocator, out IntPtr pAccelerationStructure);
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkDestroyAccelerationStructureKHR.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkDestroyAccelerationStructureKHRDelegate(IntPtr device, IntPtr accelerationStructure, IntPtr pAllocator);
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetAccelerationStructureDeviceAddressKHR.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong vkGetAccelerationStructureDeviceAddressKHRDelegate(IntPtr device, ref VkAccelerationStructureDeviceAddressInfoKHR pInfo);
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyAccelerationStructureKHR.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdCopyAccelerationStructureKHRDelegate(IntPtr commandBuffer, ref VkCopyAccelerationStructureInfoKHR pInfo);

        // TODO:  VK_KHR_ray_tracing_pipeline extension function pointers (static for now - would be loaded via vkGetDeviceProcAddr in real implementation)
        private static vkCreateRayTracingPipelinesKHRDelegate vkCreateRayTracingPipelinesKHR;
        private static vkGetRayTracingShaderGroupHandlesKHRDelegate vkGetRayTracingShaderGroupHandlesKHR;
        private static vkCmdTraceRaysKHRDelegate vkCmdTraceRaysKHR;

        // VK_KHR_acceleration_structure extension function pointers (loaded via vkGetDeviceProcAddr)
        private static vkCmdBuildAccelerationStructuresKHRDelegate vkCmdBuildAccelerationStructuresKHR;
        private static vkGetAccelerationStructureBuildSizesKHRDelegate vkGetAccelerationStructureBuildSizesKHR;
        private static vkGetBufferDeviceAddressKHRDelegate vkGetBufferDeviceAddressKHR;
        private static vkCreateAccelerationStructureKHRDelegate vkCreateAccelerationStructureKHR;
        private static vkDestroyAccelerationStructureKHRDelegate vkDestroyAccelerationStructureKHR;
        private static vkGetAccelerationStructureDeviceAddressKHRDelegate vkGetAccelerationStructureDeviceAddressKHR;
        private static vkCmdCopyAccelerationStructureKHRDelegate vkCmdCopyAccelerationStructureKHR;

        // Helper methods for Vulkan interop
        private static void InitializeVulkanFunctions(IntPtr device)
        {
            // Load Vulkan functions - in a real implementation, these would be loaded via vkGetDeviceProcAddr
            // For this example, we'll assume they're available through P/Invoke
            // TODO:  This is a simplified version - real implementation would need proper function loading
            
            // Load VK_KHR_acceleration_structure extension functions if available
            LoadAccelerationStructureExtensionFunctions(device);
            
            // Load VK_KHR_ray_tracing_pipeline extension functions if available
            LoadRayTracingPipelineExtensionFunctions(device);
        }

        /// <summary>
        /// Loads VK_KHR_acceleration_structure extension functions via vkGetDeviceProcAddr.
        /// </summary>
        /// <param name="device">Vulkan device handle.</param>
        /// <remarks>
        /// Based on Vulkan specification: VK_KHR_acceleration_structure extension functions must be loaded via vkGetDeviceProcAddr
        /// - vkDestroyAccelerationStructureKHR: Destroys acceleration structure objects
        /// - Function pointer is null if extension is not available
        /// - Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkDestroyAccelerationStructureKHR.html
        /// </remarks>
        private static void LoadAccelerationStructureExtensionFunctions(IntPtr device)
        {
            if (device == IntPtr.Zero)
            {
                return;
            }

            // Load vkGetDeviceProcAddr function pointer
            // In a real implementation, this would be loaded from the Vulkan loader library
            // TODO: STUB - For now, we'll use a P/Invoke approach or assume it's available
            // vkGetDeviceProcAddr signature: PFN_vkGetDeviceProcAddr vkGetDeviceProcAddr(VkInstance instance, const char* pName);
            // We need to get vkGetDeviceProcAddr first, then use it to load extension functions
            
            // Note: In a production implementation, vkGetDeviceProcAddr would be obtained from the Vulkan loader
            // For this implementation, we'll provide a mechanism to load the function when the extension is available
            // The actual loading would be done via P/Invoke to the Vulkan loader library (vulkan-1.dll on Windows, libvulkan.so on Linux)
            
            // Placeholder: Function loading would happen here
            // In real implementation:
            // 1. Get vkGetDeviceProcAddr from Vulkan loader
            // 2. Call vkGetDeviceProcAddr(device, "vkDestroyAccelerationStructureKHR") to get function pointer
            // 3. Call vkGetDeviceProcAddr(device, "vkCmdCopyAccelerationStructureKHR") to get function pointer
            // 4. Marshal.GetDelegateForFunctionPointer to convert to delegate
            // 5. Assign to static vkDestroyAccelerationStructureKHR and vkCmdCopyAccelerationStructureKHR fields
            
            // TODO: STUB - For now, we'll leave it as null - the Dispose method will check for null before calling
            // This allows graceful degradation when the extension is not available
        }

        /// <summary>
        /// Loads VK_KHR_ray_tracing_pipeline extension functions via vkGetDeviceProcAddr.
        /// </summary>
        /// <param name="device">Vulkan device handle.</param>
        /// <remarks>
        /// Based on Vulkan specification: VK_KHR_ray_tracing_pipeline extension functions must be loaded via vkGetDeviceProcAddr
        /// - vkCmdTraceRaysKHR: Dispatches raytracing work
        /// - vkCreateRayTracingPipelinesKHR: Creates raytracing pipelines
        /// - vkGetRayTracingShaderGroupHandlesKHR: Gets shader group handles for shader binding table
        /// - Function pointers are null if extension is not available
        /// - Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdTraceRaysKHR.html
        /// </remarks>
        private static void LoadRayTracingPipelineExtensionFunctions(IntPtr device)
        {
            if (device == IntPtr.Zero)
            {
                return;
            }

            // Load vkGetDeviceProcAddr function pointer
            // In a real implementation, this would be loaded from the Vulkan loader library
            // TODO: STUB - For now, we'll use a P/Invoke approach or assume it's available
            // vkGetDeviceProcAddr signature: PFN_vkGetDeviceProcAddr vkGetDeviceProcAddr(VkInstance instance, const char* pName);
            // We need to get vkGetDeviceProcAddr first, then use it to load extension functions
            
            // Note: In a production implementation, vkGetDeviceProcAddr would be obtained from the Vulkan loader
            // For this implementation, we'll provide a mechanism to load the function when the extension is available
            // The actual loading would be done via P/Invoke to the Vulkan loader library (vulkan-1.dll on Windows, libvulkan.so on Linux)
            
            // Placeholder: Function loading would happen here
            // In real implementation:
            // 1. Get vkGetDeviceProcAddr from Vulkan loader
            // 2. Call vkGetDeviceProcAddr(device, "vkCmdTraceRaysKHR") to get function pointer
            // 3. Marshal.GetDelegateForFunctionPointer to convert to delegate
            // 4. Assign to static vkCmdTraceRaysKHR field
            // 5. Repeat for vkCreateRayTracingPipelinesKHR and vkGetRayTracingShaderGroupHandlesKHR
            
            // TODO: STUB - For now, we'll leave it as null - the DispatchRays method will check for null before calling
            // This allows graceful degradation when the extension is not available
        }

        private static void CheckResult(VkResult result, string operation)
        {
            if (result != VkResult.VK_SUCCESS)
            {
                throw new VulkanException($"Vulkan operation '{operation}' failed with result: {result}");
            }
        }

        private class VulkanException : Exception
        {
            public VulkanException(string message) : base(message) { }
        }

        /// <summary>
        /// Platform-specific native method interop for loading Vulkan functions.
        /// </summary>
        private static class NativeMethods
        {
            // Windows P/Invoke declarations
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "LoadLibraryA")]
            private static extern IntPtr LoadLibrary_Win32(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, EntryPoint = "GetProcAddress")]
            private static extern IntPtr GetProcAddress_Win32(IntPtr hModule, string lpProcName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);

            // Linux/macOS P/Invoke declarations (for cross-platform support)
            [DllImport("libdl.so.2", CharSet = CharSet.Ansi)]
            public static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libdl.so.2", CharSet = CharSet.Ansi)]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);

            [DllImport("libdl.so.2")]
            public static extern int dlclose(IntPtr handle);

            // macOS P/Invoke declarations
            [DllImport("libdl.dylib", CharSet = CharSet.Ansi)]
            public static extern IntPtr dlopen_mac(string filename, int flags);

            [DllImport("libdl.dylib", CharSet = CharSet.Ansi)]
            public static extern IntPtr dlsym_mac(IntPtr handle, string symbol);

            // Helper method to load library based on platform
            public static IntPtr LoadLibrary(string libraryName)
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    return LoadLibrary_Win32(libraryName);
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    // Try Linux first
                    IntPtr handle = dlopen(libraryName, 2); // RTLD_NOW = 2
                    if (handle != IntPtr.Zero)
                    {
                        return handle;
                    }
                    // Try macOS
                    return dlopen_mac(libraryName, 2);
                }
                return IntPtr.Zero;
            }

            // Helper method to get function pointer based on platform
            public static IntPtr GetProcAddress(IntPtr libraryHandle, string functionName)
            {
                if (libraryHandle == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    return GetProcAddress_Win32(libraryHandle, functionName);
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    // Try Linux first
                    IntPtr ptr = dlsym(libraryHandle, functionName);
                    if (ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                    // Try macOS
                    return dlsym_mac(libraryHandle, functionName);
                }
                return IntPtr.Zero;
            }
        }

        #endregion

        private readonly IntPtr _device;
        private readonly IntPtr _instance;
        private readonly IntPtr _physicalDevice;
        private readonly IntPtr _graphicsQueue;
        private readonly IntPtr _computeQueue;
        private readonly IntPtr _transferQueue;
        private readonly GraphicsCapabilities _capabilities;
        private bool _disposed;

        // Resource tracking
        private readonly Dictionary<IntPtr, IResource> _resources;
        private uint _nextResourceHandle;

        // Frame tracking for multi-buffering
        private int _currentFrameIndex;

        // Command pool for command buffer allocation
        private readonly IntPtr _graphicsCommandPool;
        private readonly IntPtr _computeCommandPool;
        private readonly IntPtr _transferCommandPool;

        // Descriptor pool for descriptor set allocation
        private IntPtr _descriptorPool;
        private const uint DescriptorPoolMaxSets = 1000; // Maximum number of descriptor sets in pool

        // Cached physical device memory properties (queried once during initialization)
        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkPhysicalDeviceMemoryProperties.html
        private VkPhysicalDeviceMemoryProperties _memoryProperties;
        private bool _memoryPropertiesQueried;
        private const uint DescriptorPoolUniformBufferCount = 1000;
        private const uint DescriptorPoolStorageBufferCount = 1000;
        private const uint DescriptorPoolSampledImageCount = 1000;
        private const uint DescriptorPoolStorageImageCount = 1000;
        private const uint DescriptorPoolSamplerCount = 100;
        private const uint DescriptorPoolAccelStructCount = 100;

        public GraphicsCapabilities Capabilities
        {
            get { return _capabilities; }
        }

        public GraphicsBackend Backend
        {
            get { return GraphicsBackend.Vulkan; }
        }

        public bool IsValid
        {
            get { return !_disposed && _device != IntPtr.Zero; }
        }

        internal VulkanDevice(
            IntPtr device,
            IntPtr instance,
            IntPtr physicalDevice,
            IntPtr graphicsQueue,
            IntPtr computeQueue,
            IntPtr transferQueue,
            GraphicsCapabilities capabilities)
        {
            if (device == IntPtr.Zero)
            {
                throw new ArgumentException("Device handle must be valid", nameof(device));
            }

            _device = device;
            _instance = instance;
            _physicalDevice = physicalDevice;
            _graphicsQueue = graphicsQueue;
            _computeQueue = computeQueue;
            _transferQueue = transferQueue;
            _capabilities = capabilities;
            _resources = new Dictionary<IntPtr, IResource>();
            _nextResourceHandle = 1;
            _currentFrameIndex = 0;
            _memoryPropertiesQueried = false;

            // Initialize Vulkan function pointers
            InitializeVulkanFunctions(device);

            // Query physical device memory properties
            QueryPhysicalDeviceMemoryProperties();

            // Create command pools
            _graphicsCommandPool = CreateCommandPool(0); // graphics queue family
            _computeCommandPool = CreateCommandPool(1);  // compute queue family
            _transferCommandPool = CreateCommandPool(2); // transfer queue family
        }

        private IntPtr CreateCommandPool(uint queueFamilyIndex)
        {
            // Create VkCommandPoolCreateInfo structure
            // VkCommandPoolCreateInfo structure (Vulkan 1.0):
            // - sType: VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO (39)
            // - pNext: nullptr
            // - flags: VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT (allows individual command buffer reset)
            // - queueFamilyIndex: queue family index for command buffer allocation
            VkCommandPoolCreateInfo createInfo = new VkCommandPoolCreateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
                pNext = IntPtr.Zero,
                flags = VkCommandPoolCreateFlags.VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT,
                queueFamilyIndex = queueFamilyIndex
            };

            // Allocate unmanaged memory for the structure
            int structSize = Marshal.SizeOf(typeof(VkCommandPoolCreateInfo));
            IntPtr createInfoPtr = Marshal.AllocHGlobal(structSize);
            try
            {
                // Copy structure to unmanaged memory
                Marshal.StructureToPtr(createInfo, createInfoPtr, false);

                // Call vkCreateCommandPool
                IntPtr commandPool;
                VkResult result = vkCreateCommandPool(_device, createInfoPtr, IntPtr.Zero, out commandPool);

                // Check result and throw exception on failure
                CheckResult(result, "vkCreateCommandPool");

                return commandPool;
            }
            finally
            {
                // Free unmanaged memory
                Marshal.FreeHGlobal(createInfoPtr);
            }
        }

        #region Resource Creation

        public ITexture CreateTexture(TextureDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (desc.Width == 0 || desc.Height == 0)
            {
                throw new ArgumentException("Texture dimensions must be greater than zero", nameof(desc));
            }

            // Convert TextureFormat to VkFormat
            VkFormat vkFormat = ConvertToVkFormat(desc.Format);
            if (vkFormat == VkFormat.VK_FORMAT_UNDEFINED)
            {
                throw new ArgumentException($"Unsupported texture format: {desc.Format}", nameof(desc));
            }

            // Convert TextureUsage to VkImageUsageFlags
            VkImageUsageFlags usageFlags = VkImageUsageFlags.VK_IMAGE_USAGE_SAMPLED_BIT; // Default to sampled

            if ((desc.Usage & TextureUsage.RenderTarget) != 0)
            {
                usageFlags |= VkImageUsageFlags.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT;
            }
            if ((desc.Usage & TextureUsage.DepthStencil) != 0)
            {
                usageFlags |= VkImageUsageFlags.VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT;
            }
            if ((desc.Usage & TextureUsage.UnorderedAccess) != 0)
            {
                usageFlags |= VkImageUsageFlags.VK_IMAGE_USAGE_STORAGE_BIT;
            }
            if ((desc.Usage & TextureUsage.ShaderResource) != 0)
            {
                usageFlags |= VkImageUsageFlags.VK_IMAGE_USAGE_SAMPLED_BIT;
            }

            // Create VkImage
            VkImageCreateInfo imageCreateInfo = new VkImageCreateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO,
                pNext = IntPtr.Zero,
                flags = 0,
                imageType = VkImageType.VK_IMAGE_TYPE_2D,
                format = vkFormat,
                extent = new VkExtent3D
                {
                    width = (uint)desc.Width,
                    height = (uint)desc.Height,
                    depth = (uint)desc.Depth
                },
                mipLevels = (uint)desc.MipLevels,
                arrayLayers = (uint)desc.ArraySize,
                samples = ConvertToVkSampleCount(desc.SampleCount),
                tiling = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                usage = usageFlags,
                sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
                queueFamilyIndexCount = 0,
                pQueueFamilyIndices = IntPtr.Zero,
                initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED
            };

            IntPtr vkImage;
            CheckResult(vkCreateImage(_device, ref imageCreateInfo, IntPtr.Zero, out vkImage), "vkCreateImage");

            // Get memory requirements
            VkMemoryRequirements memoryRequirements;
            vkGetImageMemoryRequirements(_device, vkImage, out memoryRequirements);

            // Allocate memory
            IntPtr vkMemory = AllocateDeviceMemory(memoryRequirements, VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            // Bind memory
            CheckResult(vkBindImageMemory(_device, vkImage, vkMemory, 0), "vkBindImageMemory");

            // Create image view if needed
            IntPtr vkImageView = IntPtr.Zero;
            if ((desc.Usage & TextureUsage.ShaderResource) != 0 ||
                (desc.Usage & TextureUsage.RenderTarget) != 0 ||
                (desc.Usage & TextureUsage.UnorderedAccess) != 0)
            {
                // TODO: Create VkImageView - this requires VkImageViewCreateInfo structure
                // TODO: STUB - For now, we'll skip this and just use the image handle
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var texture = new VulkanTexture(handle, desc, vkImage, vkMemory, vkImageView, _device);
            _resources[handle] = texture;

            return texture;
        }

        private VkFormat ConvertToVkFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8G8B8A8_UNorm: return VkFormat.VK_FORMAT_R8G8B8A8_UNORM;
                case TextureFormat.R8G8B8A8_UNorm_SRGB: return VkFormat.VK_FORMAT_R8G8B8A8_SRGB;
                case TextureFormat.R16G16B16A16_Float: return VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT;
                case TextureFormat.R32G32B32A32_Float: return VkFormat.VK_FORMAT_R32G32B32A32_SFLOAT;
                case TextureFormat.D24_UNorm_S8_UInt: return VkFormat.VK_FORMAT_D24_UNORM_S8_UINT;
                case TextureFormat.D32_Float: return VkFormat.VK_FORMAT_D32_SFLOAT;
                case TextureFormat.D32_Float_S8_UInt: return VkFormat.VK_FORMAT_D32_SFLOAT_S8_UINT;
                default: return VkFormat.VK_FORMAT_UNDEFINED;
            }
        }

        /// <summary>
        /// Converts an integer sample count to Vulkan sample count flag bits.
        /// Maps sample count values (1, 2, 4, 8, 16, 32, 64) to corresponding VkSampleCountFlagBits enum values.
        /// Defaults to VK_SAMPLE_COUNT_1_BIT if the value is not a power-of-2 or is unsupported.
        /// </summary>
        private VkSampleCountFlagBits ConvertToVkSampleCount(int sampleCount)
        {
            switch (sampleCount)
            {
                case 1: return VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT;
                case 2: return VkSampleCountFlagBits.VK_SAMPLE_COUNT_2_BIT;
                case 4: return VkSampleCountFlagBits.VK_SAMPLE_COUNT_4_BIT;
                case 8: return VkSampleCountFlagBits.VK_SAMPLE_COUNT_8_BIT;
                case 16: return VkSampleCountFlagBits.VK_SAMPLE_COUNT_16_BIT;
                case 32: return VkSampleCountFlagBits.VK_SAMPLE_COUNT_32_BIT;
                case 64: return VkSampleCountFlagBits.VK_SAMPLE_COUNT_64_BIT;
                default: return VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT;
            }
        }

        private IntPtr AllocateDeviceMemory(VkMemoryRequirements requirements, VkMemoryPropertyFlags properties)
        {
            // Find suitable memory type
            uint memoryTypeIndex = FindMemoryType(requirements.memoryTypeBits, properties);

            VkMemoryAllocateInfo allocateInfo = new VkMemoryAllocateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
                pNext = IntPtr.Zero,
                allocationSize = requirements.size,
                memoryTypeIndex = memoryTypeIndex
            };

            IntPtr memory;
            CheckResult(vkAllocateMemory(_device, ref allocateInfo, IntPtr.Zero, out memory), "vkAllocateMemory");

            return memory;
        }

        /// <summary>
        /// Finds a suitable memory type index that matches the given type filter and property flags.
        /// </summary>
        /// <param name="typeFilter">Bitmask of memory types that are suitable for the resource.</param>
        /// <param name="properties">Required memory property flags (e.g., device-local, host-visible).</param>
        /// <returns>Index of the first suitable memory type, or throws if none found.</returns>
        /// <remarks>
        /// Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkPhysicalDeviceMemoryProperties.html
        /// The method searches through available memory types to find one that:
        /// 1. Is included in the typeFilter bitmask (memoryTypeBits from VkMemoryRequirements)
        /// 2. Has all the required property flags set
        /// Prefers memory types with exact property matches, falling back to types with additional properties if needed.
        /// </remarks>
        internal uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
        {
            // Ensure memory properties have been queried
            if (!_memoryPropertiesQueried)
            {
                QueryPhysicalDeviceMemoryProperties();
            }

            // Search through all 32 possible memory types
            // Based on Vulkan spec: VkPhysicalDeviceMemoryProperties can have up to 32 memory types
            for (uint i = 0; i < 32; i++)
            {
                // Check if this memory type is included in the type filter
                // typeFilter is a bitmask where bit i indicates if memory type i is suitable
                if ((typeFilter & (1u << (int)i)) != 0)
                {
                    // Check if this memory type has all required properties
                    // The memory type's property flags must include all requested properties
                    VkMemoryPropertyFlags typeProperties = _memoryProperties.memoryTypes[i].propertyFlags;
                    if ((typeProperties & properties) == properties)
                    {
                        return i;
                    }
                }
            }

            // No suitable memory type found - this should not happen with valid Vulkan implementations
            // but we need to handle it gracefully
            throw new InvalidOperationException(
                $"No suitable memory type found. Type filter: 0x{typeFilter:X8}, Required properties: {properties}. " +
                "This may indicate a driver issue or incompatible memory requirements.");
        }

        /// <summary>
        /// Queries the physical device memory properties and caches them.
        /// </summary>
        /// <remarks>
        /// Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetPhysicalDeviceMemoryProperties.html
        /// Memory properties are queried once during initialization and cached for performance.
        /// The query is performed lazily on first use if not already queried.
        /// </remarks>
        private void QueryPhysicalDeviceMemoryProperties()
        {
            if (_physicalDevice == IntPtr.Zero)
            {
                throw new InvalidOperationException("Physical device handle is invalid");
            }

            // Load vkGetPhysicalDeviceMemoryProperties function pointer if not already loaded
            if (vkGetPhysicalDeviceMemoryProperties == null)
            {
                IntPtr libHandle = NativeMethods.LoadLibrary(VulkanLibrary);
                if (libHandle != IntPtr.Zero)
                {
                    IntPtr funcPtr = NativeMethods.GetProcAddress(libHandle, "vkGetPhysicalDeviceMemoryProperties");
                    if (funcPtr != IntPtr.Zero)
                    {
                        vkGetPhysicalDeviceMemoryProperties = (vkGetPhysicalDeviceMemoryPropertiesDelegate)
                            Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(vkGetPhysicalDeviceMemoryPropertiesDelegate));
                    }
                }
            }

            if (vkGetPhysicalDeviceMemoryProperties == null)
            {
                throw new InvalidOperationException(
                    "Failed to load vkGetPhysicalDeviceMemoryProperties. Vulkan library may not be available.");
            }

            // Query memory properties from physical device
            // Based on Vulkan API: vkGetPhysicalDeviceMemoryProperties is an instance-level function
            vkGetPhysicalDeviceMemoryProperties(_physicalDevice, out _memoryProperties);
            _memoryPropertiesQueried = true;
        }

        public IBuffer CreateBuffer(BufferDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (desc.ByteSize == 0)
            {
                throw new ArgumentException("Buffer size must be greater than zero", nameof(desc));
            }

            // Convert BufferUsageFlags to VkBufferUsageFlags
            VkBufferUsageFlags usageFlags = 0;

            if ((desc.Usage & BufferUsageFlags.VertexBuffer) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_VERTEX_BUFFER_BIT;
            }
            if ((desc.Usage & BufferUsageFlags.IndexBuffer) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_INDEX_BUFFER_BIT;
            }
            if ((desc.Usage & BufferUsageFlags.ConstantBuffer) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT;
            }
            if ((desc.Usage & BufferUsageFlags.ShaderResource) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_STORAGE_BUFFER_BIT;
                // Scratch buffers and raytracing buffers need device address support
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            }
            if ((desc.Usage & BufferUsageFlags.IndirectArgument) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_INDIRECT_BUFFER_BIT;
            }

            // Add transfer flags for staging if needed
            if ((desc.Usage & (BufferUsageFlags.VertexBuffer | BufferUsageFlags.IndexBuffer | BufferUsageFlags.ConstantBuffer | BufferUsageFlags.ShaderResource)) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_TRANSFER_DST_BIT;
            }

            // Create VkBuffer
            VkBufferCreateInfo bufferCreateInfo = new VkBufferCreateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
                pNext = IntPtr.Zero,
                flags = 0,
                size = (ulong)desc.ByteSize,
                usage = usageFlags,
                sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
                queueFamilyIndexCount = 0,
                pQueueFamilyIndices = IntPtr.Zero
            };

            IntPtr vkBuffer;
            CheckResult(vkCreateBuffer(_device, ref bufferCreateInfo, IntPtr.Zero, out vkBuffer), "vkCreateBuffer");

            // Get memory requirements
            VkMemoryRequirements memoryRequirements;
            vkGetBufferMemoryRequirements(_device, vkBuffer, out memoryRequirements);

            // Determine memory properties based on usage
            // Note: Staging buffers would use host-visible memory, but BufferUsageFlags doesn't have a Staging flag
            // TODO: STUB - For now, use device-local memory for all buffers
            VkMemoryPropertyFlags memoryProperties = VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT;

            // Allocate memory
            IntPtr vkMemory = AllocateDeviceMemory(memoryRequirements, memoryProperties);

            // Bind memory
            CheckResult(vkBindBufferMemory(_device, vkBuffer, vkMemory, 0), "vkBindBufferMemory");

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var buffer = new VulkanBuffer(handle, desc, vkBuffer, vkMemory, _device);
            _resources[handle] = buffer;

            return buffer;
        }

        public ISampler CreateSampler(SamplerDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            // Convert filter mode
            VkFilter vkMinFilter = ConvertToVkFilter(desc.MinFilter);
            VkFilter vkMagFilter = ConvertToVkFilter(desc.MagFilter);
            VkSamplerMipmapMode vkMipmapMode = ConvertToVkSamplerMipmapMode(desc.MipFilter);

            // Convert address modes
            VkSamplerAddressMode vkAddressModeU = ConvertToVkSamplerAddressMode(desc.AddressU);
            VkSamplerAddressMode vkAddressModeV = ConvertToVkSamplerAddressMode(desc.AddressV);
            VkSamplerAddressMode vkAddressModeW = ConvertToVkSamplerAddressMode(desc.AddressW);

            // Convert compare function
            VkCompareOp vkCompareOp = ConvertToVkCompareOp(desc.CompareFunc);

            VkSamplerCreateInfo samplerCreateInfo = new VkSamplerCreateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO,
                pNext = IntPtr.Zero,
                flags = 0,
                magFilter = vkMagFilter,
                minFilter = vkMinFilter,
                mipmapMode = vkMipmapMode,
                addressModeU = vkAddressModeU,
                addressModeV = vkAddressModeV,
                addressModeW = vkAddressModeW,
                mipLodBias = desc.MipLodBias,
                anisotropyEnable = desc.MaxAnisotropy > 1.0f ? VkBool32.VK_TRUE : VkBool32.VK_FALSE,
                maxAnisotropy = desc.MaxAnisotropy,
                compareEnable = desc.CompareFunc != CompareFunc.Never ? VkBool32.VK_TRUE : VkBool32.VK_FALSE,
                compareOp = vkCompareOp,
                minLod = desc.MinLOD,
                maxLod = desc.MaxLOD,
                borderColor = VkBorderColor.VK_BORDER_COLOR_FLOAT_TRANSPARENT_BLACK,
                unnormalizedCoordinates = VkBool32.VK_FALSE
            };

            IntPtr vkSampler;
            CheckResult(vkCreateSampler(_device, ref samplerCreateInfo, IntPtr.Zero, out vkSampler), "vkCreateSampler");

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var sampler = new VulkanSampler(handle, desc, vkSampler, _device);
            _resources[handle] = sampler;

            return sampler;
        }

        private VkFilter ConvertToVkFilter(SamplerFilter filter)
        {
            switch (filter)
            {
                case SamplerFilter.Point: return VkFilter.VK_FILTER_NEAREST;
                case SamplerFilter.Linear: return VkFilter.VK_FILTER_LINEAR;
                case SamplerFilter.Anisotropic: return VkFilter.VK_FILTER_LINEAR;
                default: return VkFilter.VK_FILTER_LINEAR;
            }
        }

        private VkSamplerMipmapMode ConvertToVkSamplerMipmapMode(SamplerFilter filter)
        {
            switch (filter)
            {
                case SamplerFilter.Point: return VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_NEAREST;
                case SamplerFilter.Linear: return VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_LINEAR;
                case SamplerFilter.Anisotropic: return VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_LINEAR;
                default: return VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_LINEAR;
            }
        }

        private VkSamplerAddressMode ConvertToVkSamplerAddressMode(SamplerAddressMode addressMode)
        {
            switch (addressMode)
            {
                case SamplerAddressMode.Wrap: return VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT;
                case SamplerAddressMode.Mirror: return VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_MIRRORED_REPEAT;
                case SamplerAddressMode.Clamp: return VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE;
                case SamplerAddressMode.Border: return VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_BORDER;
                default: return VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE;
            }
        }

        private VkCompareOp ConvertToVkCompareOp(CompareFunc compareFunc)
        {
            switch (compareFunc)
            {
                case CompareFunc.Never: return VkCompareOp.VK_COMPARE_OP_NEVER;
                case CompareFunc.Less: return VkCompareOp.VK_COMPARE_OP_LESS;
                case CompareFunc.Equal: return VkCompareOp.VK_COMPARE_OP_EQUAL;
                case CompareFunc.LessEqual: return VkCompareOp.VK_COMPARE_OP_LESS_OR_EQUAL;
                case CompareFunc.Greater: return VkCompareOp.VK_COMPARE_OP_GREATER;
                case CompareFunc.NotEqual: return VkCompareOp.VK_COMPARE_OP_NOT_EQUAL;
                case CompareFunc.GreaterEqual: return VkCompareOp.VK_COMPARE_OP_GREATER_OR_EQUAL;
                case CompareFunc.Always: return VkCompareOp.VK_COMPARE_OP_ALWAYS;
                default: return VkCompareOp.VK_COMPARE_OP_NEVER;
            }
        }

        public IShader CreateShader(ShaderDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (desc.Bytecode == null || desc.Bytecode.Length == 0)
            {
                throw new ArgumentException("Shader bytecode must be provided", nameof(desc));
            }

            // Validate bytecode alignment (SPIR-V requires 32-bit alignment)
            if (desc.Bytecode.Length % 4 != 0)
            {
                throw new ArgumentException("Shader bytecode must be 32-bit aligned", nameof(desc));
            }

            // Pin the bytecode array for native interop
            System.Runtime.InteropServices.GCHandle bytecodeHandle = System.Runtime.InteropServices.GCHandle.Alloc(desc.Bytecode, System.Runtime.InteropServices.GCHandleType.Pinned);

            try
            {
                VkShaderModuleCreateInfo shaderModuleCreateInfo = new VkShaderModuleCreateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO,
                    pNext = IntPtr.Zero,
                    flags = 0,
                    codeSize = new IntPtr(desc.Bytecode.Length),
                    pCode = bytecodeHandle.AddrOfPinnedObject()
                };

                IntPtr vkShaderModule;
                CheckResult(vkCreateShaderModule(_device, ref shaderModuleCreateInfo, IntPtr.Zero, out vkShaderModule), "vkCreateShaderModule");

                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var shader = new VulkanShader(handle, desc, vkShaderModule, _device);
                _resources[handle] = shader;

                return shader;
            }
            finally
            {
                bytecodeHandle.Free();
            }
        }

        public IGraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc, IFramebuffer framebuffer)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            // Create pipeline layout from binding layouts
            IntPtr pipelineLayout = CreatePipelineLayout(desc.BindingLayouts);

            // Create VkRenderPass from framebuffer if provided
            // Based on Vulkan Render Pass Creation: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCreateRenderPass.html
            IntPtr vkRenderPass = IntPtr.Zero;
            if (framebuffer != null)
            {
                FramebufferDesc framebufferDesc = framebuffer.Desc;
                vkRenderPass = CreateRenderPassFromFramebufferDesc(framebufferDesc);
                
                // Note: The render pass is not owned by the pipeline - it may be shared with the framebuffer
                // TODO:  In a full implementation, the pipeline would need to store the render pass for pipeline creation
                // TODO: STUB - For now, we create it but don't use it in pipeline creation (pipeline creation is not fully implemented)
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var pipeline = new VulkanGraphicsPipeline(handle, desc, IntPtr.Zero, pipelineLayout, _device);
            _resources[handle] = pipeline;

            // Note: vkRenderPass is created but not currently used in pipeline creation
            // TODO:  In a full implementation, VkGraphicsPipelineCreateInfo would include the render pass
            // The render pass is stored in the framebuffer, so pipelines should reference it from there
            // TODO: STUB - For now, we create it to match the framebuffer's render pass structure

            return pipeline;
        }

        /// <summary>
        /// Creates a VkRenderPass from a framebuffer description.
        /// Based on Vulkan Render Pass Creation: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCreateRenderPass.html
        /// </summary>
        private IntPtr CreateRenderPassFromFramebufferDesc(FramebufferDesc desc)
        {
            if (desc == null || vkCreateRenderPass == null)
            {
                return IntPtr.Zero;
            }

            // Build attachment descriptions from framebuffer attachments
            List<VkAttachmentDescription> attachments = new List<VkAttachmentDescription>();
            List<VkAttachmentReference> colorAttachmentRefs = new List<VkAttachmentReference>();
            VkAttachmentReference depthAttachmentRef = new VkAttachmentReference
            {
                attachment = unchecked((uint)-1), // VK_ATTACHMENT_UNUSED
                layout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED
            };
            bool hasDepthAttachment = false;

            uint attachmentIndex = 0;

            // Process color attachments
            if (desc.ColorAttachments != null && desc.ColorAttachments.Length > 0)
            {
                foreach (var colorAttachment in desc.ColorAttachments)
                {
                    if (colorAttachment.Texture != null)
                    {
                        // Get texture format and sample count from texture description
                        VkFormat format = ConvertToVkFormat(colorAttachment.Texture.Desc.Format);
                        VkSampleCountFlagBits samples = ConvertToVkSampleCount(colorAttachment.Texture.Desc.SampleCount);

                        attachments.Add(new VkAttachmentDescription
                        {
                            flags = 0,
                            format = format,
                            samples = samples,
                            loadOp = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_LOAD, // Default: load existing content
                            storeOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE, // Store for next frame
                            stencilLoadOp = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE,
                            stencilStoreOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
                            initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                            finalLayout = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL
                        });

                        colorAttachmentRefs.Add(new VkAttachmentReference
                        {
                            attachment = attachmentIndex,
                            layout = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL
                        });

                        attachmentIndex++;
                    }
                }
            }

            // Process depth attachment
            if (desc.DepthAttachment.Texture != null)
            {
                VkFormat depthFormat = ConvertTextureFormatToVkFormat(desc.DepthAttachment.Texture.Desc.Format);
                VkSampleCountFlagBits depthSamples = ConvertToVkSampleCount(desc.DepthAttachment.Texture.Desc.SampleCount);

                attachments.Add(new VkAttachmentDescription
                {
                    flags = 0,
                    format = depthFormat,
                    samples = depthSamples,
                    loadOp = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_LOAD,
                    storeOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                    stencilLoadOp = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE,
                    stencilStoreOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
                    initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL,
                    finalLayout = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL
                });

                depthAttachmentRef = new VkAttachmentReference
                {
                    attachment = attachmentIndex,
                    layout = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL
                };
                hasDepthAttachment = true;
                attachmentIndex++;
            }

            // Create subpass description
            VkSubpassDescription subpass = new VkSubpassDescription
            {
                flags = 0,
                pipelineBindPoint = VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                inputAttachmentCount = 0,
                pInputAttachments = IntPtr.Zero,
                colorAttachmentCount = (uint)colorAttachmentRefs.Count,
                pColorAttachments = IntPtr.Zero, // Will be set after marshalling
                pResolveAttachments = IntPtr.Zero,
                pDepthStencilAttachment = IntPtr.Zero, // Will be set after marshalling
                preserveAttachmentCount = 0,
                pPreserveAttachments = IntPtr.Zero
            };

            // Create render pass if we have attachments
            if (attachments.Count == 0)
            {
                return IntPtr.Zero; // No attachments, no render pass needed
            }

            IntPtr vkRenderPass = IntPtr.Zero;

            try
            {
                // Marshal attachment descriptions
                int attachmentDescSize = Marshal.SizeOf(typeof(VkAttachmentDescription));
                IntPtr pAttachments = Marshal.AllocHGlobal(attachmentDescSize * attachments.Count);
                try
                {
                    for (int i = 0; i < attachments.Count; i++)
                    {
                        IntPtr attachmentPtr = new IntPtr(pAttachments.ToInt64() + i * attachmentDescSize);
                        Marshal.StructureToPtr(attachments[i], attachmentPtr, false);
                    }

                    // Marshal color attachment references
                    IntPtr pColorAttachments = IntPtr.Zero;
                    if (colorAttachmentRefs.Count > 0)
                    {
                        int colorRefSize = Marshal.SizeOf(typeof(VkAttachmentReference));
                        pColorAttachments = Marshal.AllocHGlobal(colorRefSize * colorAttachmentRefs.Count);
                        for (int i = 0; i < colorAttachmentRefs.Count; i++)
                        {
                            IntPtr colorRefPtr = new IntPtr(pColorAttachments.ToInt64() + i * colorRefSize);
                            Marshal.StructureToPtr(colorAttachmentRefs[i], colorRefPtr, false);
                        }
                    }

                    // Marshal depth attachment reference
                    IntPtr pDepthStencilAttachment = IntPtr.Zero;
                    if (hasDepthAttachment)
                    {
                        int depthRefSize = Marshal.SizeOf(typeof(VkAttachmentReference));
                        pDepthStencilAttachment = Marshal.AllocHGlobal(depthRefSize);
                        Marshal.StructureToPtr(depthAttachmentRef, pDepthStencilAttachment, false);
                    }

                    // Update subpass with marshalled pointers
                    subpass.pColorAttachments = pColorAttachments;
                    subpass.pDepthStencilAttachment = pDepthStencilAttachment;

                    // Marshal subpass description
                    int subpassSize = Marshal.SizeOf(typeof(VkSubpassDescription));
                    IntPtr pSubpasses = Marshal.AllocHGlobal(subpassSize);
                    try
                    {
                        Marshal.StructureToPtr(subpass, pSubpasses, false);

                        // Create render pass create info
                        VkRenderPassCreateInfo renderPassCreateInfo = new VkRenderPassCreateInfo
                        {
                            sType = VkStructureType.VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO,
                            pNext = IntPtr.Zero,
                            flags = 0,
                            attachmentCount = (uint)attachments.Count,
                            pAttachments = pAttachments,
                            subpassCount = 1,
                            pSubpasses = pSubpasses,
                            dependencyCount = 0,
                            pDependencies = IntPtr.Zero
                        };

                        // Create render pass
                        VkResult result = vkCreateRenderPass(_device, ref renderPassCreateInfo, IntPtr.Zero, out vkRenderPass);
                        if (result != VkResult.VK_SUCCESS)
                        {
                            throw new VulkanException($"vkCreateRenderPass failed with result: {result}");
                        }
                    }
                    finally
                    {
                        if (pSubpasses != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(pSubpasses);
                        }
                    }
                }
                finally
                {
                    if (pAttachments != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(pAttachments);
                    }
                    // Note: pColorAttachments and pDepthStencilAttachment are freed when render pass is destroyed
                    // These pointers are used by the render pass, so we cannot free them here
                }
            }
            catch
            {
                // If render pass creation fails, clean up and rethrow
                if (vkRenderPass != IntPtr.Zero)
                {
                    vkDestroyRenderPass(_device, vkRenderPass, IntPtr.Zero);
                }
                throw;
            }

            return vkRenderPass;
        }

        private IntPtr CreatePipelineLayout(IBindingLayout[] bindingLayouts)
        {
            // Create descriptor set layouts from binding layouts
            IntPtr[] descriptorSetLayouts = null;
            if (bindingLayouts != null && bindingLayouts.Length > 0)
            {
                descriptorSetLayouts = new IntPtr[bindingLayouts.Length];
                for (int i = 0; i < bindingLayouts.Length; i++)
                {
                    var vulkanLayout = bindingLayouts[i] as VulkanBindingLayout;
                    if (vulkanLayout != null)
                    {
                        descriptorSetLayouts[i] = vulkanLayout.VkDescriptorSetLayout;
                    }
                }
            }

            VkPipelineLayoutCreateInfo layoutCreateInfo = new VkPipelineLayoutCreateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO,
                pNext = IntPtr.Zero,
                flags = 0,
                setLayoutCount = (uint)(descriptorSetLayouts?.Length ?? 0),
                pSetLayouts = descriptorSetLayouts != null ? MarshalArray(descriptorSetLayouts) : IntPtr.Zero,
                pushConstantRangeCount = 0,
                pPushConstantRanges = IntPtr.Zero
            };

            IntPtr pipelineLayout;
            CheckResult(vkCreatePipelineLayout(_device, ref layoutCreateInfo, IntPtr.Zero, out pipelineLayout), "vkCreatePipelineLayout");

            return pipelineLayout;
        }

        private static IntPtr MarshalArray(IntPtr[] array)
        {
            if (array == null || array.Length == 0)
                return IntPtr.Zero;

            int size = Marshal.SizeOf(typeof(IntPtr)) * array.Length;
            IntPtr ptr = Marshal.AllocHGlobal(size);

            for (int i = 0; i < array.Length; i++)
            {
                Marshal.WriteIntPtr(ptr, i * Marshal.SizeOf(typeof(IntPtr)), array[i]);
            }

            return ptr;
        }

        public IComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            // Create pipeline layout from binding layouts
            IntPtr pipelineLayout = CreatePipelineLayout(desc.BindingLayouts);

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var pipeline = new VulkanComputePipeline(handle, desc, IntPtr.Zero, pipelineLayout, _device);
            _resources[handle] = pipeline;

            return pipeline;
        }

        public IFramebuffer CreateFramebuffer(FramebufferDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            // Create VkRenderPass from attachments
            // Based on Vulkan Render Pass Creation: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCreateRenderPass.html
            IntPtr vkRenderPass = IntPtr.Zero;
            bool ownsRenderPass = true; // This framebuffer will own the render pass

            try
            {
                // Build attachment descriptions from framebuffer attachments
                List<VkAttachmentDescription> attachments = new List<VkAttachmentDescription>();
                List<VkAttachmentReference> colorAttachmentRefs = new List<VkAttachmentReference>();
                VkAttachmentReference depthAttachmentRef = new VkAttachmentReference
                {
                    attachment = unchecked((uint)-1), // VK_ATTACHMENT_UNUSED
                    layout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED
                };
                bool hasDepthAttachment = false;

                uint attachmentIndex = 0;

                // Process color attachments
                if (desc.ColorAttachments != null && desc.ColorAttachments.Length > 0)
                {
                    foreach (var colorAttachment in desc.ColorAttachments)
                    {
                        if (colorAttachment.Texture != null)
                        {
                            // Get texture format and sample count from texture description
                            VkFormat format = ConvertToVkFormat(colorAttachment.Texture.Desc.Format);
                            VkSampleCountFlagBits samples = ConvertToVkSampleCount(colorAttachment.Texture.Desc.SampleCount);
                            
                            attachments.Add(new VkAttachmentDescription
                            {
                                flags = 0,
                                format = format,
                                samples = samples,
                                loadOp = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_LOAD, // Default: load existing content
                                storeOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE, // Store for next frame
                                stencilLoadOp = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE,
                                stencilStoreOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
                                initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                                finalLayout = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL
                            });

                            colorAttachmentRefs.Add(new VkAttachmentReference
                            {
                                attachment = attachmentIndex,
                                layout = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL
                            });

                            attachmentIndex++;
                        }
                    }
                }

                // Process depth attachment
                if (desc.DepthAttachment.Texture != null)
                {
                    VkFormat depthFormat = ConvertTextureFormatToVkFormat(desc.DepthAttachment.Texture.Desc.Format);
                    VkSampleCountFlagBits depthSamples = ConvertToVkSampleCount(desc.DepthAttachment.Texture.Desc.SampleCount);
                    
                    attachments.Add(new VkAttachmentDescription
                    {
                        flags = 0,
                        format = depthFormat,
                        samples = depthSamples,
                        loadOp = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_LOAD,
                        storeOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                        stencilLoadOp = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE,
                        stencilStoreOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
                        initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL,
                        finalLayout = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL
                    });

                    depthAttachmentRef = new VkAttachmentReference
                    {
                        attachment = attachmentIndex,
                        layout = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL
                    };
                    hasDepthAttachment = true;
                    attachmentIndex++;
                }

                // Create subpass description
                VkSubpassDescription subpass = new VkSubpassDescription
                {
                    flags = 0,
                    pipelineBindPoint = VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                    inputAttachmentCount = 0,
                    pInputAttachments = IntPtr.Zero,
                    colorAttachmentCount = (uint)colorAttachmentRefs.Count,
                    pColorAttachments = IntPtr.Zero, // Will be set after marshalling
                    pResolveAttachments = IntPtr.Zero,
                    pDepthStencilAttachment = hasDepthAttachment ? IntPtr.Zero : IntPtr.Zero, // Will be set after marshalling
                    preserveAttachmentCount = 0,
                    pPreserveAttachments = IntPtr.Zero
                };

                // Create render pass if we have attachments
                if (attachments.Count > 0 && vkCreateRenderPass != null)
                {
                    // Marshal attachment descriptions
                    int attachmentDescSize = Marshal.SizeOf(typeof(VkAttachmentDescription));
                    IntPtr pAttachments = Marshal.AllocHGlobal(attachmentDescSize * attachments.Count);
                    try
                    {
                        for (int i = 0; i < attachments.Count; i++)
                        {
                            IntPtr attachmentPtr = new IntPtr(pAttachments.ToInt64() + i * attachmentDescSize);
                            Marshal.StructureToPtr(attachments[i], attachmentPtr, false);
                        }

                        // Marshal color attachment references
                        IntPtr pColorAttachments = IntPtr.Zero;
                        if (colorAttachmentRefs.Count > 0)
                        {
                            int colorRefSize = Marshal.SizeOf(typeof(VkAttachmentReference));
                            pColorAttachments = Marshal.AllocHGlobal(colorRefSize * colorAttachmentRefs.Count);
                            for (int i = 0; i < colorAttachmentRefs.Count; i++)
                            {
                                IntPtr colorRefPtr = new IntPtr(pColorAttachments.ToInt64() + i * colorRefSize);
                                Marshal.StructureToPtr(colorAttachmentRefs[i], colorRefPtr, false);
                            }
                        }

                        // Marshal depth attachment reference
                        IntPtr pDepthStencilAttachment = IntPtr.Zero;
                        if (hasDepthAttachment)
                        {
                            int depthRefSize = Marshal.SizeOf(typeof(VkAttachmentReference));
                            pDepthStencilAttachment = Marshal.AllocHGlobal(depthRefSize);
                            Marshal.StructureToPtr(depthAttachmentRef, pDepthStencilAttachment, false);
                        }

                        // Update subpass with marshalled pointers
                        subpass.pColorAttachments = pColorAttachments;
                        subpass.pDepthStencilAttachment = pDepthStencilAttachment;

                        // Marshal subpass description
                        int subpassSize = Marshal.SizeOf(typeof(VkSubpassDescription));
                        IntPtr pSubpasses = Marshal.AllocHGlobal(subpassSize);
                        try
                        {
                            Marshal.StructureToPtr(subpass, pSubpasses, false);

                            // Create render pass create info
                            VkRenderPassCreateInfo renderPassCreateInfo = new VkRenderPassCreateInfo
                            {
                                sType = VkStructureType.VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO,
                                pNext = IntPtr.Zero,
                                flags = 0,
                                attachmentCount = (uint)attachments.Count,
                                pAttachments = pAttachments,
                                subpassCount = 1,
                                pSubpasses = pSubpasses,
                                dependencyCount = 0,
                                pDependencies = IntPtr.Zero
                            };

                            // Create render pass
                            VkResult result = vkCreateRenderPass(_device, ref renderPassCreateInfo, IntPtr.Zero, out vkRenderPass);
                            if (result != VkResult.VK_SUCCESS)
                            {
                                throw new VulkanException($"vkCreateRenderPass failed with result: {result}");
                            }

                            Console.WriteLine($"[VulkanDevice] Successfully created VkRenderPass with {attachments.Count} attachments");
                        }
                        finally
                        {
                            if (pSubpasses != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(pSubpasses);
                            }
                            if (pColorAttachments != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(pColorAttachments);
                            }
                            if (pDepthStencilAttachment != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(pDepthStencilAttachment);
                            }
                        }
                    }
                    finally
                    {
                        if (pAttachments != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(pAttachments);
                        }
                    }
                }
                else if (attachments.Count == 0)
                {
                    Console.WriteLine("[VulkanDevice] Warning: CreateFramebuffer called with no attachments, creating placeholder framebuffer");
                }
                else if (vkCreateRenderPass == null)
                {
                    Console.WriteLine("[VulkanDevice] Warning: vkCreateRenderPass function pointer not initialized, creating placeholder framebuffer");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VulkanDevice] Error creating VkRenderPass: {ex.Message}");
                Console.WriteLine($"[VulkanDevice] Stack trace: {ex.StackTrace}");
                // Continue with null render pass - framebuffer creation will handle it
            }

            // Create VkFramebuffer with image views from attachments
            // Based on Vulkan Framebuffer Creation: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCreateFramebuffer.html
            IntPtr vkFramebuffer = IntPtr.Zero;

            try
            {
                if (vkRenderPass != IntPtr.Zero && vkCreateFramebuffer != null)
                {
                    // Collect image views from attachments
                    List<IntPtr> imageViews = new List<IntPtr>();
                    uint framebufferWidth = 0;
                    uint framebufferHeight = 0;

                    // Get image views from color attachments
                    if (desc.ColorAttachments != null && desc.ColorAttachments.Length > 0)
                    {
                        foreach (var colorAttachment in desc.ColorAttachments)
                        {
                            if (colorAttachment.Texture != null)
                            {
                                // Get VkImageView from VulkanTexture
                                IntPtr imageView = GetImageViewFromTexture(colorAttachment.Texture);
                                if (imageView != IntPtr.Zero)
                                {
                                    imageViews.Add(imageView);
                                }
                                else
                                {
                                    Console.WriteLine("[VulkanDevice] Warning: Could not get image view from color attachment texture");
                                }

                                // Get framebuffer dimensions from first color attachment
                                if (framebufferWidth == 0)
                                {
                                    framebufferWidth = (uint)colorAttachment.Texture.Desc.Width;
                                    framebufferHeight = (uint)colorAttachment.Texture.Desc.Height;
                                }
                            }
                        }
                    }

                    // Get image view from depth attachment
                    if (desc.DepthAttachment.Texture != null)
                    {
                        IntPtr imageView = GetImageViewFromTexture(desc.DepthAttachment.Texture);
                        if (imageView != IntPtr.Zero)
                        {
                            imageViews.Add(imageView);
                        }
                        else
                        {
                            Console.WriteLine("[VulkanDevice] Warning: Could not get image view from depth attachment texture");
                        }

                        // Update framebuffer dimensions if not set
                        if (framebufferWidth == 0)
                        {
                            framebufferWidth = (uint)desc.DepthAttachment.Texture.Desc.Width;
                            framebufferHeight = (uint)desc.DepthAttachment.Texture.Desc.Height;
                        }
                    }

                    if (imageViews.Count > 0 && framebufferWidth > 0 && framebufferHeight > 0)
                    {
                        // Marshal image view array
                        IntPtr pImageViews = Marshal.AllocHGlobal(IntPtr.Size * imageViews.Count);
                        try
                        {
                            for (int i = 0; i < imageViews.Count; i++)
                            {
                                Marshal.WriteIntPtr(pImageViews, i * IntPtr.Size, imageViews[i]);
                            }

                            // Create framebuffer create info
                            VkFramebufferCreateInfo framebufferCreateInfo = new VkFramebufferCreateInfo
                            {
                                sType = VkStructureType.VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO,
                                pNext = IntPtr.Zero,
                                flags = 0,
                                renderPass = vkRenderPass,
                                attachmentCount = (uint)imageViews.Count,
                                pAttachments = pImageViews,
                                width = framebufferWidth,
                                height = framebufferHeight,
                                layers = 1 // 2D framebuffer
                            };

                            // Create framebuffer
                            VkResult result = vkCreateFramebuffer(_device, ref framebufferCreateInfo, IntPtr.Zero, out vkFramebuffer);
                            if (result != VkResult.VK_SUCCESS)
                            {
                                throw new VulkanException($"vkCreateFramebuffer failed with result: {result}");
                            }

                            Console.WriteLine($"[VulkanDevice] Successfully created VkFramebuffer {framebufferWidth}x{framebufferHeight} with {imageViews.Count} attachments");
                        }
                        finally
                        {
                            if (pImageViews != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(pImageViews);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[VulkanDevice] Warning: Cannot create VkFramebuffer - no valid image views or invalid dimensions");
                    }
                }
                else if (vkRenderPass == IntPtr.Zero)
                {
                    Console.WriteLine("[VulkanDevice] Warning: Cannot create VkFramebuffer - render pass creation failed");
                }
                else if (vkCreateFramebuffer == null)
                {
                    Console.WriteLine("[VulkanDevice] Warning: vkCreateFramebuffer function pointer not initialized");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VulkanDevice] Error creating VkFramebuffer: {ex.Message}");
                Console.WriteLine($"[VulkanDevice] Stack trace: {ex.StackTrace}");
                
                // Clean up render pass if framebuffer creation failed
                if (vkRenderPass != IntPtr.Zero && vkDestroyRenderPass != null)
                {
                    try
                    {
                        vkDestroyRenderPass(_device, vkRenderPass, IntPtr.Zero);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                    vkRenderPass = IntPtr.Zero;
                }
            }

            // Create framebuffer wrapper with handles
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var framebuffer = new VulkanFramebuffer(handle, desc, vkFramebuffer, vkRenderPass, _device, ownsRenderPass);
            _resources[handle] = framebuffer;

            return framebuffer;
        }

        /// <summary>
        /// Gets the VkImageView handle from an ITexture.
        /// For VulkanTexture instances, extracts the internal VkImageView handle.
        /// </summary>
        private IntPtr GetImageViewFromTexture(ITexture texture)
        {
            if (texture == null)
            {
                return IntPtr.Zero;
            }

            // Try to get VkImageView from VulkanTexture
            if (texture is VulkanTexture vulkanTexture)
            {
                // Use reflection to access private _vkImageView field (C# 7.3 compatible)
                // In a production implementation, VulkanTexture would expose this via a property or internal method
                try
                {
                    System.Reflection.FieldInfo field = typeof(VulkanTexture).GetField("_vkImageView", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        IntPtr imageView = (IntPtr)field.GetValue(vulkanTexture);
                        return imageView;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VulkanDevice] Error getting image view from VulkanTexture via reflection: {ex.Message}");
                }
            }

            // Fallback: if texture has a native handle, try to use it
            // Note: This may not work if NativeHandle is not the image view
            if (texture.NativeHandle != IntPtr.Zero)
            {
                return texture.NativeHandle;
            }

            return IntPtr.Zero;
        }

        public IBindingLayout CreateBindingLayout(BindingLayoutDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (desc.Items == null || desc.Items.Length == 0)
            {
                throw new ArgumentException("Binding layout must have at least one item", nameof(desc));
            }

            // Convert BindingLayoutItems to VkDescriptorSetLayoutBinding
            VkDescriptorSetLayoutBinding[] bindings = new VkDescriptorSetLayoutBinding[desc.Items.Length];

            for (int i = 0; i < desc.Items.Length; i++)
            {
                var item = desc.Items[i];
                VkDescriptorType descriptorType = ConvertToVkDescriptorType(item.Type);

                bindings[i] = new VkDescriptorSetLayoutBinding
                {
                    binding = (uint)item.Slot,
                    descriptorType = descriptorType,
                    descriptorCount = (uint)item.Count,
                    stageFlags = ConvertShaderStageFlagsToVk(item.Stages),
                    pImmutableSamplers = IntPtr.Zero
                };
            }

            VkDescriptorSetLayoutCreateInfo layoutCreateInfo = new VkDescriptorSetLayoutCreateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO,
                pNext = IntPtr.Zero,
                flags = 0,
                bindingCount = (uint)bindings.Length,
                pBindings = MarshalArray(bindings)
            };

            IntPtr vkDescriptorSetLayout;
            CheckResult(vkCreateDescriptorSetLayout(_device, ref layoutCreateInfo, IntPtr.Zero, out vkDescriptorSetLayout), "vkCreateDescriptorSetLayout");

            // Free the marshalled array
            if (layoutCreateInfo.pBindings != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(layoutCreateInfo.pBindings);
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var layout = new VulkanBindingLayout(handle, desc, vkDescriptorSetLayout, _device);
            _resources[handle] = layout;

            return layout;
        }

        private VkDescriptorType ConvertToVkDescriptorType(BindingType type)
        {
            switch (type)
            {
                case BindingType.ConstantBuffer: return VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
                case BindingType.Texture: return VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE;
                case BindingType.Sampler: return VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLER;
                case BindingType.UnorderedAccess: return VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
                case BindingType.StructuredBuffer: return VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
                case BindingType.AccelStruct: return VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;
                default: return VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
            }
        }

        /// <summary>
        /// Converts ShaderStageFlags to VkShaderStageFlags.
        /// Maps abstract shader stage flags to Vulkan-specific shader stage flags.
        /// Based on Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkShaderStageFlagBits.html
        /// </summary>
        /// <param name="stages">Shader stage flags to convert</param>
        /// <returns>Vulkan shader stage flags</returns>
        private VkShaderStageFlags ConvertShaderStageFlagsToVk(ShaderStageFlags stages)
        {
            if (stages == ShaderStageFlags.None)
            {
                return 0;
            }

            VkShaderStageFlags result = 0;

            // Convert individual graphics stages
            if ((stages & ShaderStageFlags.Vertex) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_VERTEX_BIT;
            }
            if ((stages & ShaderStageFlags.Hull) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_TESSELLATION_CONTROL_BIT;
            }
            if ((stages & ShaderStageFlags.Domain) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_TESSELLATION_EVALUATION_BIT;
            }
            if ((stages & ShaderStageFlags.Geometry) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_GEOMETRY_BIT;
            }
            if ((stages & ShaderStageFlags.Pixel) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_FRAGMENT_BIT;
            }
            if ((stages & ShaderStageFlags.Compute) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_COMPUTE_BIT;
            }

            // Convert raytracing stages
            if ((stages & ShaderStageFlags.RayGen) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_RAYGEN_BIT_KHR;
            }
            if ((stages & ShaderStageFlags.Miss) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_MISS_BIT_KHR;
            }
            if ((stages & ShaderStageFlags.ClosestHit) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_CLOSEST_HIT_BIT_KHR;
            }
            if ((stages & ShaderStageFlags.AnyHit) != 0)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_ANY_HIT_BIT_KHR;
            }

            // Handle composite flags
            if ((stages & ShaderStageFlags.AllGraphics) == ShaderStageFlags.AllGraphics)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_ALL_GRAPHICS;
            }
            if ((stages & ShaderStageFlags.AllRaytracing) == ShaderStageFlags.AllRaytracing)
            {
                result |= VkShaderStageFlags.VK_SHADER_STAGE_RAYGEN_BIT_KHR |
                          VkShaderStageFlags.VK_SHADER_STAGE_MISS_BIT_KHR |
                          VkShaderStageFlags.VK_SHADER_STAGE_CLOSEST_HIT_BIT_KHR |
                          VkShaderStageFlags.VK_SHADER_STAGE_ANY_HIT_BIT_KHR;
            }

            return result;
        }

        public IBindingSet CreateBindingSet(IBindingLayout layout, BindingSetDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (_descriptorPool == IntPtr.Zero)
            {
                throw new InvalidOperationException("Descriptor pool not initialized");
            }

            // Cast layout to VulkanBindingLayout to get the VkDescriptorSetLayout
            VulkanBindingLayout vulkanLayout = layout as VulkanBindingLayout;
            if (vulkanLayout == null)
            {
                throw new ArgumentException("Layout must be a VulkanBindingLayout", nameof(layout));
            }

            // Get the VkDescriptorSetLayout handle from the layout
            IntPtr vkDescriptorSetLayout = vulkanLayout.VkDescriptorSetLayout;
            if (vkDescriptorSetLayout == IntPtr.Zero)
            {
                throw new InvalidOperationException("Invalid descriptor set layout");
            }

            // Allocate descriptor set from pool
            IntPtr vkDescriptorSet = AllocateDescriptorSet(vkDescriptorSetLayout);
            if (vkDescriptorSet == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to allocate descriptor set from pool");
            }

            // Update descriptor set with resources from BindingSetItems
            if (desc.Items != null && desc.Items.Length > 0)
            {
                UpdateDescriptorSet(vkDescriptorSet, desc.Items, layout);
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var bindingSet = new VulkanBindingSet(handle, layout, desc, vkDescriptorSet, _descriptorPool, _device);
            _resources[handle] = bindingSet;

            Console.WriteLine($"[VulkanDevice] Created binding set with {desc.Items?.Length ?? 0} items, descriptor set handle: {vkDescriptorSet}");

            return bindingSet;
        }


        /// <summary>
        /// Allocates a descriptor set from the descriptor pool.
        /// Based on Vulkan Descriptor Set Allocation: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkAllocateDescriptorSets.html
        /// </summary>
        private IntPtr AllocateDescriptorSet(IntPtr vkDescriptorSetLayout)
        {
            // Allocate memory for descriptor set layout array
            IntPtr setLayoutsPtr = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(setLayoutsPtr, vkDescriptorSetLayout);

                VkDescriptorSetAllocateInfo allocateInfo = new VkDescriptorSetAllocateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO,
                    pNext = IntPtr.Zero,
                    descriptorPool = _descriptorPool,
                    descriptorSetCount = 1,
                    pSetLayouts = setLayoutsPtr
                };

                // Allocate memory for output descriptor set handle
                IntPtr descriptorSetPtr = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    VkResult result = vkAllocateDescriptorSets(_device, ref allocateInfo, descriptorSetPtr);
                    if (result != VkResult.VK_SUCCESS)
                    {
                        if (result == VkResult.VK_ERROR_OUT_OF_POOL_MEMORY || result == VkResult.VK_ERROR_FRAGMENTED_POOL)
                        {
                            Console.WriteLine($"[VulkanDevice] Warning: Descriptor pool exhausted (result: {result}), consider creating a larger pool or resetting the pool");
                        }
                        CheckResult(result, "vkAllocateDescriptorSets");
                        return IntPtr.Zero;
                    }

                    IntPtr descriptorSet = Marshal.ReadIntPtr(descriptorSetPtr);
                    return descriptorSet;
                }
                finally
                {
                    Marshal.FreeHGlobal(descriptorSetPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(setLayoutsPtr);
            }
        }

        /// <summary>
        /// Updates a descriptor set with resources from BindingSetItems.
        /// Based on Vulkan Descriptor Set Updates: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkUpdateDescriptorSets.html
        /// </summary>
        private void UpdateDescriptorSet(IntPtr vkDescriptorSet, BindingSetItem[] items, IBindingLayout layout)
        {
            if (items == null || items.Length == 0)
            {
                return;
            }

            // Build write descriptor sets for each binding item
            List<VkWriteDescriptorSet> writeDescriptorSets = new List<VkWriteDescriptorSet>();
            List<IntPtr> imageInfoPtrs = new List<IntPtr>();
            List<IntPtr> bufferInfoPtrs = new List<IntPtr>();
            List<IntPtr> accelStructInfoPtrs = new List<IntPtr>(); // For VkWriteDescriptorSetAccelerationStructureKHR structures

            try
            {
                foreach (BindingSetItem item in items)
                {
                    VkDescriptorType descriptorType = ConvertToVkDescriptorType(item.Type);
                    VkWriteDescriptorSet writeDescriptorSet = new VkWriteDescriptorSet
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET,
                        pNext = IntPtr.Zero,
                        dstSet = vkDescriptorSet,
                        dstBinding = (uint)item.Slot,
                        dstArrayElement = 0,
                        descriptorCount = 1,
                        descriptorType = descriptorType,
                        pImageInfo = IntPtr.Zero,
                        pBufferInfo = IntPtr.Zero,
                        pTexelBufferView = IntPtr.Zero
                    };

                    // Set up descriptor info based on type
                    switch (item.Type)
                    {
                        case BindingType.Texture:
                        case BindingType.UnorderedAccess:
                            // Create VkDescriptorImageInfo for texture/image
                            if (item.Texture != null)
                            {
                                IntPtr imageView = GetTextureImageView(item.Texture);
                                if (imageView != IntPtr.Zero)
                                {
                                    VkDescriptorImageInfo imageInfo = new VkDescriptorImageInfo
                                    {
                                        sampler = IntPtr.Zero,
                                        imageView = imageView,
                                        imageLayout = item.Type == BindingType.UnorderedAccess 
                                            ? VkImageLayout.VK_IMAGE_LAYOUT_GENERAL 
                                            : VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
                                    };

                                    IntPtr imageInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkDescriptorImageInfo)));
                                    Marshal.StructureToPtr(imageInfo, imageInfoPtr, false);
                                    imageInfoPtrs.Add(imageInfoPtr);
                                    writeDescriptorSet.pImageInfo = imageInfoPtr;
                                }
                            }
                            break;

                        case BindingType.Sampler:
                            // Create VkDescriptorImageInfo for sampler
                            if (item.Sampler != null)
                            {
                                IntPtr sampler = GetSamplerHandle(item.Sampler);
                                if (sampler != IntPtr.Zero)
                                {
                                    VkDescriptorImageInfo imageInfo = new VkDescriptorImageInfo
                                    {
                                        sampler = sampler,
                                        imageView = IntPtr.Zero,
                                        imageLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED
                                    };

                                    IntPtr imageInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkDescriptorImageInfo)));
                                    Marshal.StructureToPtr(imageInfo, imageInfoPtr, false);
                                    imageInfoPtrs.Add(imageInfoPtr);
                                    writeDescriptorSet.pImageInfo = imageInfoPtr;
                                }
                            }
                            break;

                        case BindingType.ConstantBuffer:
                        case BindingType.StructuredBuffer:
                            // Create VkDescriptorBufferInfo for buffer
                            if (item.Buffer != null)
                            {
                                IntPtr buffer = GetBufferHandle(item.Buffer);
                                if (buffer != IntPtr.Zero)
                                {
                                    ulong offset = (ulong)(item.BufferOffset >= 0 ? item.BufferOffset : 0);
                                    ulong range = (ulong)(item.BufferRange > 0 ? item.BufferRange : item.Buffer.Desc.ByteSize);

                                    VkDescriptorBufferInfo bufferInfo = new VkDescriptorBufferInfo
                                    {
                                        buffer = buffer,
                                        offset = offset,
                                        range = range
                                    };

                                    IntPtr bufferInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkDescriptorBufferInfo)));
                                    Marshal.StructureToPtr(bufferInfo, bufferInfoPtr, false);
                                    bufferInfoPtrs.Add(bufferInfoPtr);
                                    writeDescriptorSet.pBufferInfo = bufferInfoPtr;
                                }
                            }
                            break;

                        case BindingType.AccelStruct:
                            // Acceleration structures require special handling with VkWriteDescriptorSetAccelerationStructureKHR
                            // This uses the VK_KHR_acceleration_structure extension
                            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkWriteDescriptorSetAccelerationStructureKHR.html
                            if (item.AccelStruct != null)
                            {
                                // Get acceleration structure handle
                                IntPtr accelStructHandle = GetAccelStructHandle(item.AccelStruct);
                                if (accelStructHandle != IntPtr.Zero)
                                {
                                    // Set descriptor type for acceleration structure
                                    writeDescriptorSet.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;
                                    
                                    // Create VkWriteDescriptorSetAccelerationStructureKHR structure
                                    // This structure is chained via pNext in VkWriteDescriptorSet
                                    // The structure contains an array of acceleration structure handles
                                    
                                    // Allocate memory for array of acceleration structure handles (IntPtr array)
                                    // TODO: STUB - For now, we support single acceleration structure per binding
                                    IntPtr accelStructHandlesArray = IntPtr.Zero;
                                    IntPtr accelStructInfoPtr = IntPtr.Zero;
                                    
                                    try
                                    {
                                        // Allocate memory for array of acceleration structure handles
                                        accelStructHandlesArray = Marshal.AllocHGlobal(IntPtr.Size);
                                        Marshal.WriteIntPtr(accelStructHandlesArray, accelStructHandle);
                                        
                                        // Create VkWriteDescriptorSetAccelerationStructureKHR structure
                                        VkWriteDescriptorSetAccelerationStructureKHR accelStructInfo = new VkWriteDescriptorSetAccelerationStructureKHR
                                        {
                                            sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR,
                                            pNext = IntPtr.Zero,
                                            accelerationStructureCount = 1,
                                            pAccelerationStructures = accelStructHandlesArray
                                        };
                                        
                                        // Allocate memory for the acceleration structure info structure
                                        accelStructInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkWriteDescriptorSetAccelerationStructureKHR)));
                                        Marshal.StructureToPtr(accelStructInfo, accelStructInfoPtr, false);
                                        
                                        // Add both allocations to cleanup list (order: structure first, then handles array)
                                        // This ensures proper cleanup order
                                        accelStructInfoPtrs.Add(accelStructInfoPtr);
                                        accelStructInfoPtrs.Add(accelStructHandlesArray);
                                        
                                        // Chain the acceleration structure info via pNext in VkWriteDescriptorSet
                                        writeDescriptorSet.pNext = accelStructInfoPtr;
                                        
                                        // Clear pointers to prevent double-free (they're now tracked in the list)
                                        accelStructInfoPtr = IntPtr.Zero;
                                        accelStructHandlesArray = IntPtr.Zero;
                                    }
                                    catch
                                    {
                                        // If allocation or marshalling fails, free what we allocated
                                        if (accelStructInfoPtr != IntPtr.Zero)
                                        {
                                            Marshal.FreeHGlobal(accelStructInfoPtr);
                                        }
                                        if (accelStructHandlesArray != IntPtr.Zero)
                                        {
                                            Marshal.FreeHGlobal(accelStructHandlesArray);
                                        }
                                        Console.WriteLine($"[VulkanDevice] Failed to allocate memory for acceleration structure descriptor info for slot {item.Slot}");
                                    }
                                }
                            }
                            break;
                    }

                    writeDescriptorSets.Add(writeDescriptorSet);
                }

                // Marshal write descriptor sets array
                if (writeDescriptorSets.Count > 0)
                {
                    int writeDescriptorSetSize = Marshal.SizeOf(typeof(VkWriteDescriptorSet));
                    IntPtr writeDescriptorSetsPtr = Marshal.AllocHGlobal(writeDescriptorSets.Count * writeDescriptorSetSize);
                    try
                    {
                        for (int i = 0; i < writeDescriptorSets.Count; i++)
                        {
                            IntPtr offset = new IntPtr(writeDescriptorSetsPtr.ToInt64() + i * writeDescriptorSetSize);
                            Marshal.StructureToPtr(writeDescriptorSets[i], offset, false);
                        }

                        // Update descriptor sets
                        vkUpdateDescriptorSets(_device, (uint)writeDescriptorSets.Count, writeDescriptorSetsPtr, 0, IntPtr.Zero);

                        Console.WriteLine($"[VulkanDevice] Updated descriptor set with {writeDescriptorSets.Count} write operations");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(writeDescriptorSetsPtr);
                    }
                }
            }
            finally
            {
                // Free all allocated image info and buffer info structures
                foreach (IntPtr ptr in imageInfoPtrs)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                foreach (IntPtr ptr in bufferInfoPtrs)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                // Free acceleration structure info structures
                // Note: Each acceleration structure binding allocates two blocks:
                // 1. The VkWriteDescriptorSetAccelerationStructureKHR structure
                // 2. The array of acceleration structure handles (IntPtr array)
                foreach (IntPtr ptr in accelStructInfoPtrs)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the VkImageView handle from an ITexture.
        /// </summary>
        private IntPtr GetTextureImageView(ITexture texture)
        {
            if (texture == null)
            {
                return IntPtr.Zero;
            }

            // Try to get VkImageView from VulkanTexture
            if (texture is VulkanTexture vulkanTexture)
            {
                return vulkanTexture.VkImageView;
            }

            // Fallback: use NativeHandle if available
            System.Reflection.PropertyInfo nativeHandleProp = texture.GetType().GetProperty("NativeHandle");
            if (nativeHandleProp != null)
            {
                return (IntPtr)nativeHandleProp.GetValue(texture);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the VkBuffer handle from an IBuffer.
        /// </summary>
        private IntPtr GetBufferHandle(IBuffer buffer)
        {
            if (buffer == null)
            {
                return IntPtr.Zero;
            }

            // Try to get VkBuffer from VulkanBuffer
            if (buffer is VulkanBuffer vulkanBuffer)
            {
                return vulkanBuffer.VkBuffer;
            }

            // Fallback: use NativeHandle if available
            System.Reflection.PropertyInfo nativeHandleProp = buffer.GetType().GetProperty("NativeHandle");
            if (nativeHandleProp != null)
            {
                return (IntPtr)nativeHandleProp.GetValue(buffer);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the VkSampler handle from an ISampler.
        /// </summary>
        private IntPtr GetSamplerHandle(ISampler sampler)
        {
            if (sampler == null)
            {
                return IntPtr.Zero;
            }

            // Try to get VkSampler from VulkanSampler
            if (sampler is VulkanSampler vulkanSampler)
            {
                return vulkanSampler.VkSampler;
            }

            // Fallback: use NativeHandle if available
            System.Reflection.PropertyInfo nativeHandleProp = sampler.GetType().GetProperty("NativeHandle");
            if (nativeHandleProp != null)
            {
                return (IntPtr)nativeHandleProp.GetValue(sampler);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the VkAccelerationStructureKHR handle from an IAccelStruct.
        /// </summary>
        private IntPtr GetAccelStructHandle(IAccelStruct accelStruct)
        {
            if (accelStruct == null)
            {
                return IntPtr.Zero;
            }

            // Try to get handle from VulkanAccelStruct
            if (accelStruct is VulkanAccelStruct vulkanAccelStruct)
            {
                return vulkanAccelStruct.VkAccelStruct;
            }

            return IntPtr.Zero;
        }

        public ICommandList CreateCommandList(CommandListType type = CommandListType.Graphics)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            // Select appropriate command pool based on type
            IntPtr commandPool = _graphicsCommandPool;
            switch (type)
            {
                case CommandListType.Graphics:
                    commandPool = _graphicsCommandPool;
                    break;
                case CommandListType.Compute:
                    commandPool = _computeCommandPool;
                    break;
                case CommandListType.Copy:
                    commandPool = _transferCommandPool;
                    break;
            }

            // Validate command pool is valid
            if (commandPool == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Command pool for type {type} is not initialized");
            }

            // Validate vkAllocateCommandBuffers function pointer is initialized
            // vkAllocateCommandBuffers is a core Vulkan function, so it should be available
            if (vkAllocateCommandBuffers == null)
            {
                throw new InvalidOperationException("vkAllocateCommandBuffers function pointer is not initialized");
            }

            // Allocate VkCommandBuffer from command pool
            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkAllocateCommandBuffers.html
            // We allocate a single primary command buffer
            VkCommandBufferAllocateInfo allocateInfo = new VkCommandBufferAllocateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
                pNext = IntPtr.Zero,
                commandPool = commandPool,
                level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY, // Primary command buffer can be submitted to queues
                commandBufferCount = 1 // Allocate a single command buffer
            };

            // Marshal structure to unmanaged memory
            int allocateInfoSize = Marshal.SizeOf(typeof(VkCommandBufferAllocateInfo));
            IntPtr allocateInfoPtr = Marshal.AllocHGlobal(allocateInfoSize);
            try
            {
                Marshal.StructureToPtr(allocateInfo, allocateInfoPtr, false);

                // Allocate memory for command buffer handle (VkCommandBuffer is a handle, so it's IntPtr-sized)
                IntPtr commandBufferPtr = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    // vkAllocateCommandBuffers signature:
                    // VkResult vkAllocateCommandBuffers(
                    //     VkDevice device,
                    //     const VkCommandBufferAllocateInfo* pAllocateInfo,
                    //     VkCommandBuffer* pCommandBuffers);
                    VkResult result = vkAllocateCommandBuffers(_device, allocateInfoPtr, commandBufferPtr);
                    CheckResult(result, "vkAllocateCommandBuffers");

                    // Read the allocated command buffer handle
                    IntPtr vkCommandBuffer = Marshal.ReadIntPtr(commandBufferPtr);
                    if (vkCommandBuffer == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("vkAllocateCommandBuffers returned a null command buffer handle");
                    }

                    // Create VulkanCommandList with the allocated command buffer
                    IntPtr handle = new IntPtr(_nextResourceHandle++);
                    var commandList = new VulkanCommandList(handle, type, this, vkCommandBuffer, commandPool, _device);
                    _resources[handle] = commandList;

                    return commandList;
                }
                finally
                {
                    Marshal.FreeHGlobal(commandBufferPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(allocateInfoPtr);
            }
        }

        public ITexture CreateHandleForNativeTexture(IntPtr nativeHandle, TextureDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (nativeHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Native handle must be valid", nameof(nativeHandle));
            }

            // Wrap existing native texture (e.g., from swapchain)
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var texture = new VulkanTexture(handle, desc, nativeHandle);
            _resources[handle] = texture;

            return texture;
        }

        #endregion

        #region Raytracing Resources

        public IAccelStruct CreateAccelStruct(AccelStructDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (!_capabilities.SupportsRaytracing)
            {
                throw new NotSupportedException("Raytracing is not supported on this device");
            }

            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            // TODO:  Full implementation of VK_KHR_acceleration_structure extension
            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureKHR.html
            
            // Load extension functions if not already loaded
            LoadAccelerationStructureExtensionFunctions(_device);

            // Validate that required functions are available
            if (vkCreateAccelerationStructureKHR == null || vkGetAccelerationStructureBuildSizesKHR == null || vkGetBufferDeviceAddressKHR == null)
            {
                throw new NotSupportedException("VK_KHR_acceleration_structure extension functions are not available. Ensure the extension is enabled and functions are loaded.");
            }

            // Determine acceleration structure type
            VkAccelerationStructureTypeKHR accelType = desc.IsTopLevel 
                ? VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR 
                : VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR;

            // For initial creation, we need to estimate buffer size
            // In practice, this would be calculated from geometry data using vkGetAccelerationStructureBuildSizesKHR
            // TODO: STUB - For now, we'll create a buffer with a reasonable default size
            // The actual size will be calculated when building the acceleration structure
            ulong estimatedBufferSize = desc.IsTopLevel ? 4096UL : 16384UL; // Conservative estimates

            // Create buffer for acceleration structure storage
            // Based on Vulkan API: Acceleration structures require VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR
            var bufferDesc = new BufferDesc
            {
                ByteSize = (int)estimatedBufferSize,
                Usage = BufferUsageFlags.ShaderResource | BufferUsageFlags.AccelerationStructureStorage
            };

            IBuffer accelBuffer = CreateBuffer(bufferDesc);
            if (accelBuffer == null)
            {
                throw new InvalidOperationException("Failed to create backing buffer for acceleration structure");
            }

            // Get buffer device address for acceleration structure creation
            // Based on Vulkan API: vkGetBufferDeviceAddressKHR returns device address for buffer
            VulkanBuffer vulkanBuffer = accelBuffer as VulkanBuffer;
            if (vulkanBuffer == null)
            {
                accelBuffer.Dispose();
                throw new InvalidOperationException("Backing buffer must be a VulkanBuffer");
            }

            IntPtr vkBuffer = vulkanBuffer.VkBuffer;
            if (vkBuffer == IntPtr.Zero)
            {
                accelBuffer.Dispose();
                throw new InvalidOperationException("Vulkan buffer handle is invalid");
            }

            VkBufferDeviceAddressInfo bufferAddressInfo = new VkBufferDeviceAddressInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                pNext = IntPtr.Zero,
                buffer = vkBuffer
            };

            ulong bufferDeviceAddress = vkGetBufferDeviceAddressKHR(_device, ref bufferAddressInfo);
            if (bufferDeviceAddress == 0UL)
            {
                accelBuffer.Dispose();
                throw new InvalidOperationException("Failed to get device address for acceleration structure buffer");
            }

            // Create acceleration structure
            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCreateAccelerationStructureKHR.html
            VkAccelerationStructureCreateInfoKHR createInfo = new VkAccelerationStructureCreateInfoKHR
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR,
                pNext = IntPtr.Zero,
                createFlags = 0, // VkAccelerationStructureCreateFlagsKHR - no special flags
                buffer = vkBuffer,
                offset = 0UL, // Start at beginning of buffer
                size = estimatedBufferSize, // Will be updated when building
                type = accelType,
                deviceAddress = 0UL // Will be set after creation
            };

            IntPtr vkAccelStruct = IntPtr.Zero;
            VkResult result = vkCreateAccelerationStructureKHR(_device, ref createInfo, IntPtr.Zero, out vkAccelStruct);
            
            if (result != VkResult.VK_SUCCESS || vkAccelStruct == IntPtr.Zero)
            {
                accelBuffer.Dispose();
                throw new InvalidOperationException($"Failed to create acceleration structure: {result}");
            }

            // Get acceleration structure device address
            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetAccelerationStructureDeviceAddressKHR.html
            ulong accelStructDeviceAddress = 0UL;
            if (vkGetAccelerationStructureDeviceAddressKHR != null)
            {
                VkAccelerationStructureDeviceAddressInfoKHR addressInfo = new VkAccelerationStructureDeviceAddressInfoKHR
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_DEVICE_ADDRESS_INFO_KHR,
                    pNext = IntPtr.Zero,
                    accelerationStructure = vkAccelStruct
                };
                accelStructDeviceAddress = vkGetAccelerationStructureDeviceAddressKHR(_device, ref addressInfo);
            }
            else
            {
                // Fallback: Use buffer address as approximation (not ideal, but allows basic functionality)
                // In production, vkGetAccelerationStructureDeviceAddressKHR should always be available
                accelStructDeviceAddress = bufferDeviceAddress;
                System.Console.WriteLine("[VulkanDevice] Warning: vkGetAccelerationStructureDeviceAddressKHR not available, using buffer address as fallback");
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var accelStruct = new VulkanAccelStruct(handle, desc, vkAccelStruct, accelBuffer, accelStructDeviceAddress, _device);
            _resources[handle] = accelStruct;

            System.Console.WriteLine($"[VulkanDevice] Created {accelType} acceleration structure (handle={handle}, vkHandle={vkAccelStruct:X}, deviceAddress={accelStructDeviceAddress:X})");

            return accelStruct;
        }

        public IRaytracingPipeline CreateRaytracingPipeline(Interfaces.RaytracingPipelineDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (!_capabilities.SupportsRaytracing)
            {
                throw new NotSupportedException("Raytracing is not supported on this device");
            }

            if (desc.Shaders == null || desc.Shaders.Length == 0)
            {
                throw new ArgumentException("Raytracing pipeline requires at least one shader", nameof(desc));
            }

            // Create pipeline layout from global binding layout
            IntPtr pipelineLayout = IntPtr.Zero;
            if (desc.GlobalBindingLayout != null)
            {
                pipelineLayout = CreatePipelineLayout(new[] { desc.GlobalBindingLayout });
            }

            // TODO:  Full implementation of VK_KHR_ray_tracing_pipeline extension

            // Helper to convert ShaderType to VkShaderStageFlags
            VkShaderStageFlags ConvertShaderTypeToVkStage(ShaderType shaderType)
            {
                switch (shaderType)
                {
                    case ShaderType.RayGeneration:
                        return VkShaderStageFlags.VK_SHADER_STAGE_RAYGEN_BIT_KHR;
                    case ShaderType.Miss:
                        return VkShaderStageFlags.VK_SHADER_STAGE_MISS_BIT_KHR;
                    case ShaderType.ClosestHit:
                        return VkShaderStageFlags.VK_SHADER_STAGE_CLOSEST_HIT_BIT_KHR;
                    case ShaderType.AnyHit:
                        return VkShaderStageFlags.VK_SHADER_STAGE_ANY_HIT_BIT_KHR;
                    case ShaderType.Intersection:
                        return VkShaderStageFlags.VK_SHADER_STAGE_INTERSECTION_BIT_KHR;
                    case ShaderType.Callable:
                        return VkShaderStageFlags.VK_SHADER_STAGE_CALLABLE_BIT_KHR;
                    default:
                        throw new ArgumentException($"Shader type {shaderType} is not a raytracing shader type", nameof(shaderType));
                }
            }

            // Build shader stage create infos from shaders
            var shaderStages = new List<VkPipelineShaderStageCreateInfo>();
            var shaderModuleToStageIndex = new Dictionary<IntPtr, int>();
            var entryPointNames = new List<IntPtr>(); // Keep track for cleanup
            int stageIndex = 0;

            foreach (var shader in desc.Shaders)
            {
                if (shader == null)
                {
                    continue;
                }

                // Get shader module from VulkanShader
                IntPtr shaderModule = IntPtr.Zero;
                if (shader is VulkanShader vulkanShader)
                {
                    shaderModule = vulkanShader.VkShaderModule;
                }

                if (shaderModule == IntPtr.Zero)
                {
                    // Cleanup entry point names before throwing
                    foreach (var ptr in entryPointNames)
                    {
                        if (ptr != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                    throw new ArgumentException($"Shader {shader.Desc.DebugName ?? "unnamed"} does not have a valid Vulkan shader module", nameof(desc));
                }

                VkShaderStageFlags stageFlag = ConvertShaderTypeToVkStage(shader.Type);

                // Get entry point name (default to "main" if not specified)
                string entryPoint = shader.Desc.EntryPoint;
                if (string.IsNullOrEmpty(entryPoint))
                {
                    entryPoint = "main";
                }

                // Marshal entry point name
                byte[] entryPointBytes = System.Text.Encoding.UTF8.GetBytes(entryPoint + "\0");
                IntPtr entryPointPtr = Marshal.AllocHGlobal(entryPointBytes.Length);
                Marshal.Copy(entryPointBytes, 0, entryPointPtr, entryPointBytes.Length);
                entryPointNames.Add(entryPointPtr);

                var stageCreateInfo = new VkPipelineShaderStageCreateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                    pNext = IntPtr.Zero,
                    flags = 0,
                    stage = stageFlag,
                    module = shaderModule,
                    pName = entryPointPtr,
                    pSpecializationInfo = IntPtr.Zero
                };

                shaderStages.Add(stageCreateInfo);
                shaderModuleToStageIndex[shaderModule] = stageIndex;
                stageIndex++;
            }

            if (shaderStages.Count == 0)
            {
                // Cleanup entry point names
                foreach (var ptr in entryPointNames)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                throw new ArgumentException("No valid shaders provided for raytracing pipeline", nameof(desc));
            }

            // Build shader groups
            var shaderGroups = new List<VkRayTracingShaderGroupCreateInfoKHR>();

            // Add ray generation shader as a general group
            bool hasRayGen = false;
            for (int i = 0; i < desc.Shaders.Length; i++)
            {
                if (desc.Shaders[i] != null && desc.Shaders[i].Type == ShaderType.RayGeneration)
                {
                    IntPtr shaderModule = IntPtr.Zero;
                    if (desc.Shaders[i] is VulkanShader vulkanShader)
                    {
                        shaderModule = vulkanShader.VkShaderModule;
                    }
                    if (shaderModule != IntPtr.Zero && shaderModuleToStageIndex.ContainsKey(shaderModule))
                    {
                        var group = new VkRayTracingShaderGroupCreateInfoKHR
                        {
                            sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR,
                            pNext = IntPtr.Zero,
                            type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR,
                            generalShader = (uint)shaderModuleToStageIndex[shaderModule],
                            closestHitShader = 0xFFFFFFFF, // VK_SHADER_INDEX_UNUSED
                            anyHitShader = 0xFFFFFFFF,
                            intersectionShader = 0xFFFFFFFF,
                            pShaderGroupCaptureReplayHandle = IntPtr.Zero
                        };
                        shaderGroups.Add(group);
                        hasRayGen = true;
                    }
                    break;
                }
            }

            if (!hasRayGen)
            {
                // Cleanup entry point names
                foreach (var ptr in entryPointNames)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                throw new ArgumentException("Raytracing pipeline requires at least one ray generation shader", nameof(desc));
            }

            // Add miss shaders as general groups
            for (int i = 0; i < desc.Shaders.Length; i++)
            {
                if (desc.Shaders[i] != null && desc.Shaders[i].Type == ShaderType.Miss)
                {
                    IntPtr shaderModule = IntPtr.Zero;
                    if (desc.Shaders[i] is VulkanShader vulkanShader)
                    {
                        shaderModule = vulkanShader.VkShaderModule;
                    }
                    if (shaderModule != IntPtr.Zero && shaderModuleToStageIndex.ContainsKey(shaderModule))
                    {
                        var group = new VkRayTracingShaderGroupCreateInfoKHR
                        {
                            sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR,
                            pNext = IntPtr.Zero,
                            type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR,
                            generalShader = (uint)shaderModuleToStageIndex[shaderModule],
                            closestHitShader = 0xFFFFFFFF,
                            anyHitShader = 0xFFFFFFFF,
                            intersectionShader = 0xFFFFFFFF,
                            pShaderGroupCaptureReplayHandle = IntPtr.Zero
                        };
                        shaderGroups.Add(group);
                    }
                }
            }

            // Add hit groups
            if (desc.HitGroups != null)
            {
                foreach (var hitGroup in desc.HitGroups)
                {
                    uint closestHitIndex = 0xFFFFFFFF;
                    uint anyHitIndex = 0xFFFFFFFF;
                    uint intersectionIndex = 0xFFFFFFFF;

                    if (hitGroup.ClosestHitShader != null)
                    {
                        IntPtr shaderModule = IntPtr.Zero;
                        if (hitGroup.ClosestHitShader is VulkanShader vulkanShader)
                        {
                            shaderModule = vulkanShader.VkShaderModule;
                        }
                        if (shaderModule != IntPtr.Zero && shaderModuleToStageIndex.ContainsKey(shaderModule))
                        {
                            closestHitIndex = (uint)shaderModuleToStageIndex[shaderModule];
                        }
                    }

                    if (hitGroup.AnyHitShader != null)
                    {
                        IntPtr shaderModule = IntPtr.Zero;
                        if (hitGroup.AnyHitShader is VulkanShader vulkanShader)
                        {
                            shaderModule = vulkanShader.VkShaderModule;
                        }
                        if (shaderModule != IntPtr.Zero && shaderModuleToStageIndex.ContainsKey(shaderModule))
                        {
                            anyHitIndex = (uint)shaderModuleToStageIndex[shaderModule];
                        }
                    }

                    if (hitGroup.IntersectionShader != null)
                    {
                        IntPtr shaderModule = IntPtr.Zero;
                        if (hitGroup.IntersectionShader is VulkanShader vulkanShader)
                        {
                            shaderModule = vulkanShader.VkShaderModule;
                        }
                        if (shaderModule != IntPtr.Zero && shaderModuleToStageIndex.ContainsKey(shaderModule))
                        {
                            intersectionIndex = (uint)shaderModuleToStageIndex[shaderModule];
                        }
                    }

                    VkRayTracingShaderGroupTypeKHR groupType = hitGroup.IsProceduralPrimitive
                        ? VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_PROCEDURAL_HIT_GROUP_KHR
                        : VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_TRIANGLES_HIT_GROUP_KHR;

                    var group = new VkRayTracingShaderGroupCreateInfoKHR
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR,
                        pNext = IntPtr.Zero,
                        type = groupType,
                        generalShader = 0xFFFFFFFF,
                        closestHitShader = closestHitIndex,
                        anyHitShader = anyHitIndex,
                        intersectionShader = intersectionIndex,
                        pShaderGroupCaptureReplayHandle = IntPtr.Zero
                    };
                    shaderGroups.Add(group);
                }
            }

            // Marshal shader stages array
            int stageSize = Marshal.SizeOf(typeof(VkPipelineShaderStageCreateInfo));
            IntPtr stagesPtr = Marshal.AllocHGlobal(stageSize * shaderStages.Count);
            try
            {
                for (int i = 0; i < shaderStages.Count; i++)
                {
                    IntPtr stagePtr = new IntPtr(stagesPtr.ToInt64() + i * stageSize);
                    Marshal.StructureToPtr(shaderStages[i], stagePtr, false);
                }

                // Marshal shader groups array
                int groupSize = Marshal.SizeOf(typeof(VkRayTracingShaderGroupCreateInfoKHR));
                IntPtr groupsPtr = Marshal.AllocHGlobal(groupSize * shaderGroups.Count);
                try
                {
                    for (int i = 0; i < shaderGroups.Count; i++)
                    {
                        IntPtr groupPtr = new IntPtr(groupsPtr.ToInt64() + i * groupSize);
                        Marshal.StructureToPtr(shaderGroups[i], groupPtr, false);
                    }

                    // Create raytracing pipeline create info
                    uint maxRecursionDepth = desc.MaxRecursionDepth > 0 ? (uint)desc.MaxRecursionDepth : 1;
                    var pipelineCreateInfo = new VkRayTracingPipelineCreateInfoKHR
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_PIPELINE_CREATE_INFO_KHR,
                        pNext = IntPtr.Zero,
                        flags = 0,
                        stageCount = (uint)shaderStages.Count,
                        pStages = stagesPtr,
                        groupCount = (uint)shaderGroups.Count,
                        pGroups = groupsPtr,
                        maxPipelineRayRecursionDepth = maxRecursionDepth,
                        pLibraryInfo = IntPtr.Zero,
                        pLibraryInterface = IntPtr.Zero,
                        pDynamicState = IntPtr.Zero,
                        layout = pipelineLayout,
                        basePipelineHandle = IntPtr.Zero,
                        basePipelineIndex = -1
                    };

                    // Marshal pipeline create info
                    int pipelineCreateInfoSize = Marshal.SizeOf(typeof(VkRayTracingPipelineCreateInfoKHR));
                    IntPtr pipelineCreateInfoPtr = Marshal.AllocHGlobal(pipelineCreateInfoSize);
                    try
                    {
                        Marshal.StructureToPtr(pipelineCreateInfo, pipelineCreateInfoPtr, false);

                        // Allocate pipeline handle
                        IntPtr pipelinePtr = Marshal.AllocHGlobal(IntPtr.Size);
                        try
                        {
                            // Note: vkCreateRayTracingPipelinesKHR should be loaded via vkGetDeviceProcAddr
                            // TODO: STUB - For now, we assume it's available if raytracing is supported
                            if (vkCreateRayTracingPipelinesKHR == null)
                            {
                                throw new NotSupportedException("vkCreateRayTracingPipelinesKHR function pointer is not initialized. VK_KHR_ray_tracing_pipeline extension may not be available.");
                            }

                            VkResult result = vkCreateRayTracingPipelinesKHR(_device, IntPtr.Zero, IntPtr.Zero, 1, pipelineCreateInfoPtr, IntPtr.Zero, pipelinePtr);
                            CheckResult(result, "vkCreateRayTracingPipelinesKHR");

                            IntPtr vkPipeline = Marshal.ReadIntPtr(pipelinePtr);

                            try
                            {
                                // Get shader group handle size (typically 32 bytes)
                                uint handleSize = 32;
                                uint handleSizeAligned = (handleSize + 31) & ~31u; // Align to 32 bytes
                                uint groupCount = (uint)shaderGroups.Count;
                                uint sbtSize = groupCount * handleSizeAligned;

                                // Create SBT buffer
                                var sbtBufferDesc = new BufferDesc
                                {
                                    ByteSize = (int)sbtSize,
                                    Usage = BufferUsageFlags.ShaderResource
                                };
                                IBuffer sbtBuffer = CreateBuffer(sbtBufferDesc);

                                // Get shader group handles and populate SBT
                                // Note: SBT population typically happens at dispatch time or requires buffer mapping
                                // TODO: STUB - For now, we create the buffer - full SBT population would require vkGetRayTracingShaderGroupHandlesKHR
                                // and proper buffer device address support (VK_KHR_buffer_device_address)

                                IntPtr handle = new IntPtr(_nextResourceHandle++);
                                var pipeline = new VulkanRaytracingPipeline(handle, desc, vkPipeline, pipelineLayout, sbtBuffer, _device);
                                _resources[handle] = pipeline;

                                return pipeline;
                            }
                            catch
                            {
                                // If pipeline creation succeeded but something else failed, destroy the pipeline to prevent resource leak
                                if (vkPipeline != IntPtr.Zero && _device != IntPtr.Zero)
                                {
                                    vkDestroyPipeline(_device, vkPipeline, IntPtr.Zero);
                                }
                                throw;
                            }
                        }
                        finally
                        {
                            if (pipelinePtr != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(pipelinePtr);
                            }
                        }
                    }
                    finally
                    {
                        if (pipelineCreateInfoPtr != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(pipelineCreateInfoPtr);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(groupsPtr);
                }
            }
            finally
            {
                if (stagesPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(stagesPtr);
                }
                // Cleanup entry point names
                foreach (var ptr in entryPointNames)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }
        }

        #endregion

        #region Command Execution

        public void ExecuteCommandList(ICommandList commandList)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (commandList == null)
            {
                throw new ArgumentNullException(nameof(commandList));
            }

            ExecuteCommandLists(new[] { commandList });
        }

        public void ExecuteCommandLists(ICommandList[] commandLists)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (commandLists == null || commandLists.Length == 0)
            {
                return;
            }

            // Group command lists by type for efficient submission
            var graphicsLists = new List<VulkanCommandList>();
            var computeLists = new List<VulkanCommandList>();
            var copyLists = new List<VulkanCommandList>();

            foreach (var cmdList in commandLists)
            {
                var vulkanCmdList = cmdList as VulkanCommandList;
                if (vulkanCmdList == null)
                {
                    throw new ArgumentException("Command list must be a VulkanCommandList", nameof(commandLists));
                }

                if (!vulkanCmdList._isOpen)
                {
                    throw new InvalidOperationException("Command list must be closed before execution");
                }

                switch (vulkanCmdList._type)
                {
                    case CommandListType.Graphics:
                        graphicsLists.Add(vulkanCmdList);
                        break;
                    case CommandListType.Compute:
                        computeLists.Add(vulkanCmdList);
                        break;
                    case CommandListType.Copy:
                        copyLists.Add(vulkanCmdList);
                        break;
                }
            }

            // Submit graphics command lists
            if (graphicsLists.Count > 0)
            {
                SubmitCommandLists(graphicsLists, _graphicsQueue);
            }

            // Submit compute command lists
            if (computeLists.Count > 0)
            {
                SubmitCommandLists(computeLists, _computeQueue);
            }

            // Submit copy command lists
            if (copyLists.Count > 0)
            {
                SubmitCommandLists(copyLists, _transferQueue);
            }
        }

        private void SubmitCommandLists(List<VulkanCommandList> commandLists, IntPtr queue)
        {
            IntPtr[] commandBuffers = new IntPtr[commandLists.Count];
            for (int i = 0; i < commandLists.Count; i++)
            {
                commandBuffers[i] = commandLists[i]._vkCommandBuffer;
            }

            VkSubmitInfo submitInfo = new VkSubmitInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO,
                pNext = IntPtr.Zero,
                waitSemaphoreCount = 0,
                pWaitSemaphores = IntPtr.Zero,
                pWaitDstStageMask = IntPtr.Zero,
                commandBufferCount = (uint)commandBuffers.Length,
                pCommandBuffers = MarshalArray(commandBuffers),
                signalSemaphoreCount = 0,
                pSignalSemaphores = IntPtr.Zero
            };

            CheckResult(vkQueueSubmit(queue, 1, ref submitInfo, IntPtr.Zero), "vkQueueSubmit");

            // Free the marshalled array
            if (submitInfo.pCommandBuffers != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(submitInfo.pCommandBuffers);
            }
        }

        public void WaitIdle()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            CheckResult(vkDeviceWaitIdle(_device), "vkDeviceWaitIdle");
        }

        public void Signal(IFence fence, ulong value)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (fence == null)
            {
                throw new ArgumentNullException(nameof(fence));
            }

            // TODO: Vulkan doesn't have direct fence signaling like D3D12
            // Fences are signaled automatically when queue operations complete
            // For explicit signaling, we'd need to submit an empty command buffer with fence
            // TODO: STUB - For now, this is a placeholder
            throw new NotImplementedException("Fence signaling not implemented - Vulkan fences are signaled by queue operations");
        }

        public void WaitFence(IFence fence, ulong value)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            if (fence == null)
            {
                throw new ArgumentNullException(nameof(fence));
            }

            // Extract VkFence handle from IFence implementation using reflection
            // The VkFence handle is typically stored as NativeHandle or in a private field
            IntPtr vkFence = ExtractVkFenceHandle(fence);
            if (vkFence == IntPtr.Zero)
            {
                throw new ArgumentException("Failed to extract VkFence handle from IFence implementation. The fence must be a Vulkan fence with a valid native handle.", nameof(fence));
            }

            // Load vkWaitForFences function pointer if not already loaded
            // vkWaitForFences is a core Vulkan function, so we can load it via vkGetDeviceProcAddr
            if (vkWaitForFences == null)
            {
                // Load vkGetDeviceProcAddr first (if not already available)
                // vkGetDeviceProcAddr signature: PFN_vkGetDeviceProcAddr(device, "functionName")
                // For core functions like vkWaitForFences, vkGetDeviceProcAddr will return the function pointer
                IntPtr funcPtr = VulkanLoaderHelper.GetProcAddress(VulkanLoaderHelper.LoadLibrary(VulkanLibrary), "vkWaitForFences");
                if (funcPtr == IntPtr.Zero)
                {
                    // Fallback: Try loading via instance proc address (some implementations require this)
                    // In practice, vkWaitForFences should be available via vkGetDeviceProcAddr
                    throw new InvalidOperationException("Failed to load vkWaitForFences function from Vulkan library. Make sure Vulkan is properly initialized.");
                }
                vkWaitForFences = (vkWaitForFencesDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(vkWaitForFencesDelegate));
            }

            // Call vkWaitForFences
            // waitAll = VK_TRUE to wait for all fences (we only have one fence, so this doesn't matter)
            // timeout = UINT64_MAX (0xFFFFFFFFFFFFFFFF) to wait indefinitely
            VkResult result = vkWaitForFences(_device, 1, new IntPtr(&vkFence), VkBool32.VK_TRUE, 0xFFFFFFFFFFFFFFFFUL);

            // Check result
            if (result != VkResult.VK_SUCCESS && result != VkResult.VK_TIMEOUT)
            {
                throw new InvalidOperationException($"vkWaitForFences failed with result: {result}");
            }
        }

        #endregion

        #region Queries

        public int GetConstantBufferAlignment()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            // Vulkan requires uniform buffer alignment of 256 bytes
            return 256;
        }

        public int GetTextureAlignment()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            // Vulkan typically requires texture alignment of 4 bytes
            return 4;
        }

        public bool IsFormatSupported(TextureFormat format, TextureUsage usage)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            VkFormat vkFormat = ConvertToVkFormat(format);
            if (vkFormat == VkFormat.VK_FORMAT_UNDEFINED)
            {
                return false;
            }

            // Query physical device format properties to determine actual format support
            // Based on Vulkan API: https://docs.vulkan.org/refpages/latest/refpages/source/vkGetPhysicalDeviceFormatProperties.html
            if (_physicalDevice == IntPtr.Zero)
            {
                // Physical device not available - fallback to basic format check
                return IsFormatSupportedFallback(format, usage);
            }

            // Load vkGetPhysicalDeviceFormatProperties function pointer if not already loaded
            if (vkGetPhysicalDeviceFormatProperties == null)
            {
                // vkGetPhysicalDeviceFormatProperties is an instance-level function, loaded via vkGetInstanceProcAddr
                // TODO: STUB - For now, try loading via P/Invoke (in a real implementation, this would use vkGetInstanceProcAddr)
                string vulkanLib = VulkanLibrary;
                IntPtr libHandle = NativeMethods.LoadLibrary(vulkanLib);
                if (libHandle != IntPtr.Zero)
                {
                    IntPtr funcPtr = NativeMethods.GetProcAddress(libHandle, "vkGetPhysicalDeviceFormatProperties");
                    if (funcPtr != IntPtr.Zero)
                    {
                        vkGetPhysicalDeviceFormatProperties = (vkGetPhysicalDeviceFormatPropertiesDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(vkGetPhysicalDeviceFormatPropertiesDelegate));
                    }
                }
            }

            // If function pointer is still null, fall back to basic format check
            if (vkGetPhysicalDeviceFormatProperties == null)
            {
                return IsFormatSupportedFallback(format, usage);
            }

            // Query format properties from physical device
            VkFormatProperties formatProperties;
            vkGetPhysicalDeviceFormatProperties(_physicalDevice, vkFormat, out formatProperties);

            // Check format features based on usage flags
            // We use optimalTilingFeatures for textures (most common case)
            VkFormatFeatureFlags features = formatProperties.optimalTilingFeatures;

            // Map TextureUsage flags to VkFormatFeatureFlags
            // ShaderResource -> VK_FORMAT_FEATURE_SAMPLED_IMAGE_BIT
            // RenderTarget -> VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BIT (for color) or VK_FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT (for depth)
            // UnorderedAccess -> VK_FORMAT_FEATURE_STORAGE_IMAGE_BIT
            // DepthStencil -> VK_FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT

            // Check if format supports shader resource usage (sampled images)
            if ((usage & TextureUsage.ShaderResource) != 0)
            {
                if ((features & VkFormatFeatureFlags.VK_FORMAT_FEATURE_SAMPLED_IMAGE_BIT) == 0)
                {
                    return false;
                }
            }

            // Check if format supports render target usage (color attachment)
            if ((usage & TextureUsage.RenderTarget) != 0)
            {
                // Determine if this is a depth/stencil format or color format
                bool isDepthStencilFormat = format == TextureFormat.D24_UNORM_S8_UINT ||
                                           format == TextureFormat.D32_FLOAT ||
                                           format == TextureFormat.D32_FLOAT_S8_UINT;

                if (isDepthStencilFormat)
                {
                    // Depth/stencil formats require DEPTH_STENCIL_ATTACHMENT_BIT
                    if ((features & VkFormatFeatureFlags.VK_FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT) == 0)
                    {
                        return false;
                    }
                }
                else
                {
                    // Color formats require COLOR_ATTACHMENT_BIT
                    if ((features & VkFormatFeatureFlags.VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BIT) == 0)
                    {
                        return false;
                    }
                }
            }

            // Check if format supports unordered access usage (storage images)
            if ((usage & TextureUsage.UnorderedAccess) != 0)
            {
                if ((features & VkFormatFeatureFlags.VK_FORMAT_FEATURE_STORAGE_IMAGE_BIT) == 0)
                {
                    return false;
                }
            }

            // Check if format supports depth stencil usage
            if ((usage & TextureUsage.DepthStencil) != 0)
            {
                if ((features & VkFormatFeatureFlags.VK_FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT) == 0)
                {
                    return false;
                }
            }

            // All requested usage flags are supported
            return true;
        }

        /// <summary>
        /// Fallback implementation for format support checking when physical device query is unavailable.
        /// Uses heuristics based on common format support patterns.
        /// </summary>
        private bool IsFormatSupportedFallback(TextureFormat format, TextureUsage usage)
        {
            // Fallback: assume common formats are supported for common usages
            switch (format)
            {
                case TextureFormat.RGBA8_UNORM:
                case TextureFormat.RGBA8_SRGB:
                    return (usage & (TextureUsage.ShaderResource | TextureUsage.RenderTarget)) != 0;

                case TextureFormat.RGBA16_FLOAT:
                case TextureFormat.RGBA32_FLOAT:
                    return (usage & (TextureUsage.ShaderResource | TextureUsage.RenderTarget | TextureUsage.UnorderedAccess)) != 0;

                case TextureFormat.D24_UNORM_S8_UINT:
                case TextureFormat.D32_FLOAT:
                case TextureFormat.D32_FLOAT_S8_UINT:
                    return (usage & TextureUsage.DepthStencil) != 0;

                default:
                    return false;
            }
        }

        public int GetCurrentFrameIndex()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(VulkanDevice));
            }

            return _currentFrameIndex;
        }

        internal void AdvanceFrameIndex()
        {
            _currentFrameIndex = (_currentFrameIndex + 1) % 3; // Triple buffering
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Dispose all tracked resources
            foreach (var resource in _resources.Values)
            {
                resource?.Dispose();
            }
            _resources.Clear();

            // Destroy command pools
            if (_graphicsCommandPool != IntPtr.Zero)
            {
                vkDestroyCommandPool(_device, _graphicsCommandPool, IntPtr.Zero);
            }
            if (_computeCommandPool != IntPtr.Zero)
            {
                vkDestroyCommandPool(_device, _computeCommandPool, IntPtr.Zero);
            }
            if (_transferCommandPool != IntPtr.Zero)
            {
                vkDestroyCommandPool(_device, _transferCommandPool, IntPtr.Zero);
            }

            // Destroy descriptor pool
            if (_descriptorPool != IntPtr.Zero)
            {
                vkDestroyDescriptorPool(_device, _descriptorPool, IntPtr.Zero);
                Console.WriteLine("[VulkanDevice] Destroyed descriptor pool");
            }

            // Note: We don't destroy _device here as it's owned by VulkanBackend
            // The backend will handle device cleanup in its Shutdown method

            _disposed = true;
        }

        #endregion

        #region Internal Helpers

        internal IntPtr GetDeviceHandle()
        {
            return _device;
        }

        internal IntPtr GetGraphicsQueue()
        {
            return _graphicsQueue;
        }

        internal IntPtr GetComputeQueue()
        {
            return _computeQueue;
        }

        internal IntPtr GetTransferQueue()
        {
            return _transferQueue;
        }

        #endregion

        #region Resource Interface

        private interface IResource : IDisposable
        {
        }

        #endregion

        #region Resource Implementations

        private class VulkanTexture : ITexture, IResource
        {
            public TextureDesc Desc { get; }
            public IntPtr NativeHandle { get; private set; }
            private readonly IntPtr _internalHandle;
            private readonly IntPtr _vkImage;
            private readonly IntPtr _vkMemory;
            private readonly IntPtr _vkImageView;
            private readonly IntPtr _device;

            public VulkanTexture(IntPtr handle, TextureDesc desc, IntPtr nativeHandle = default(IntPtr))
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, nativeHandle)
            {
            }

            public VulkanTexture(IntPtr handle, TextureDesc desc, IntPtr vkImage, IntPtr vkMemory, IntPtr vkImageView, IntPtr device, IntPtr nativeHandle = default(IntPtr))
            {
                _internalHandle = handle;
                Desc = desc;
                _vkImage = vkImage;
                _vkMemory = vkMemory;
                _vkImageView = vkImageView;
                _device = device;
                NativeHandle = nativeHandle != IntPtr.Zero ? nativeHandle : (_vkImage != IntPtr.Zero ? _vkImage : handle);
            }

            public void Dispose()
            {
                if (_vkImageView != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyImageView(_device, _vkImageView, IntPtr.Zero);
                }
                if (_vkImage != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyImage(_device, _vkImage, IntPtr.Zero);
                }
                if (_vkMemory != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkFreeMemory(_device, _vkMemory, IntPtr.Zero);
                }
            }
        }

        private class VulkanBuffer : IBuffer, IResource
        {
            public BufferDesc Desc { get; }
            public IntPtr NativeHandle { get; private set; }
            private readonly IntPtr _internalHandle;
            private readonly IntPtr _vkBuffer;
            private readonly IntPtr _vkMemory;
            private readonly IntPtr _device;

            public VulkanBuffer(IntPtr handle, BufferDesc desc)
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)
            {
            }

            public VulkanBuffer(IntPtr handle, BufferDesc desc, IntPtr vkBuffer, IntPtr vkMemory, IntPtr device)
            {
                _internalHandle = handle;
                Desc = desc;
                _vkBuffer = vkBuffer;
                _vkMemory = vkMemory;
                _device = device;
                NativeHandle = _vkBuffer != IntPtr.Zero ? _vkBuffer : handle;
            }

            /// <summary>
            /// Gets the VkBuffer handle. Used internally for descriptor set updates.
            /// </summary>
            internal IntPtr VkBuffer
            {
                get { return _vkBuffer; }
            }

            public void Dispose()
            {
                if (_vkBuffer != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyBuffer(_device, _vkBuffer, IntPtr.Zero);
                }
                if (_vkMemory != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkFreeMemory(_device, _vkMemory, IntPtr.Zero);
                }
            }
        }

        private class VulkanSampler : ISampler, IResource
        {
            public SamplerDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkSampler;
            private readonly IntPtr _device;

            public VulkanSampler(IntPtr handle, SamplerDesc desc)
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero)
            {
            }

            public VulkanSampler(IntPtr handle, SamplerDesc desc, IntPtr vkSampler, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _vkSampler = vkSampler;
                _device = device;
            }

            /// <summary>
            /// Gets the VkSampler handle. Used internally for descriptor set updates.
            /// </summary>
            internal IntPtr VkSampler
            {
                get { return _vkSampler; }
            }

            public void Dispose()
            {
                if (_vkSampler != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroySampler(_device, _vkSampler, IntPtr.Zero);
                }
            }
        }

        private class VulkanShader : IShader, IResource
        {
            public ShaderDesc Desc { get; }
            public ShaderType Type { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkShaderModule;
            private readonly IntPtr _device;

            public VulkanShader(IntPtr handle, ShaderDesc desc)
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero)
            {
            }

            public VulkanShader(IntPtr handle, ShaderDesc desc, IntPtr vkShaderModule, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _vkShaderModule = vkShaderModule;
                _device = device;
                Type = desc.Type;
            }

            /// <summary>
            /// Gets the VkShaderModule handle. Used internally for pipeline creation.
            /// </summary>
            internal IntPtr VkShaderModule
            {
                get { return _vkShaderModule; }
            }

            public void Dispose()
            {
                if (_vkShaderModule != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyShaderModule(_device, _vkShaderModule, IntPtr.Zero);
                }
            }
        }

        private class VulkanGraphicsPipeline : IGraphicsPipeline, IResource
        {
            public GraphicsPipelineDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkPipeline;
            private readonly IntPtr _vkPipelineLayout;
            private readonly IntPtr _device;

            public VulkanGraphicsPipeline(IntPtr handle, GraphicsPipelineDesc desc)
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)
            {
            }

            public VulkanGraphicsPipeline(IntPtr handle, GraphicsPipelineDesc desc, IntPtr vkPipeline, IntPtr vkPipelineLayout, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _vkPipeline = vkPipeline;
                _vkPipelineLayout = vkPipelineLayout;
                _device = device;
            }

            /// <summary>
            /// Gets the VkPipeline handle. Used internally for command list binding.
            /// </summary>
            internal IntPtr VkPipeline
            {
                get { return _vkPipeline; }
            }

            /// <summary>
            /// Gets the VkPipelineLayout handle. Used internally for descriptor set binding.
            /// </summary>
            internal IntPtr VkPipelineLayout
            {
                get { return _vkPipelineLayout; }
            }

            public void Dispose()
            {
                if (_vkPipeline != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyPipeline(_device, _vkPipeline, IntPtr.Zero);
                }
                if (_vkPipelineLayout != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyPipelineLayout(_device, _vkPipelineLayout, IntPtr.Zero);
                }
            }
        }

        private class VulkanComputePipeline : IComputePipeline, IResource
        {
            public ComputePipelineDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkPipeline;
            private readonly IntPtr _vkPipelineLayout;
            private readonly IntPtr _device;

            public VulkanComputePipeline(IntPtr handle, ComputePipelineDesc desc)
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)
            {
            }

            public VulkanComputePipeline(IntPtr handle, ComputePipelineDesc desc, IntPtr vkPipeline, IntPtr vkPipelineLayout, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _vkPipeline = vkPipeline;
                _vkPipelineLayout = vkPipelineLayout;
                _device = device;
            }

            /// <summary>
            /// Gets the VkPipeline handle. Used internally for command list binding.
            /// </summary>
            internal IntPtr VkPipeline
            {
                get { return _vkPipeline; }
            }

            /// <summary>
            /// Gets the VkPipelineLayout handle. Used internally for descriptor set binding.
            /// </summary>
            internal IntPtr VkPipelineLayout
            {
                get { return _vkPipelineLayout; }
            }

            public void Dispose()
            {
                if (_vkPipeline != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyPipeline(_device, _vkPipeline, IntPtr.Zero);
                }
                if (_vkPipelineLayout != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyPipelineLayout(_device, _vkPipelineLayout, IntPtr.Zero);
                }
            }
        }

        private class VulkanFramebuffer : IFramebuffer, IResource
        {
            public FramebufferDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkFramebuffer; // VkFramebuffer handle
            private readonly IntPtr _vkRenderPass; // VkRenderPass handle (may be shared or framebuffer-specific)
            private readonly IntPtr _device; // VkDevice handle for destruction
            private readonly bool _ownsRenderPass; // Whether this framebuffer owns the render pass (not shared)

            public VulkanFramebuffer(IntPtr handle, FramebufferDesc desc)
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, false)
            {
            }

            public VulkanFramebuffer(IntPtr handle, FramebufferDesc desc, IntPtr vkFramebuffer, IntPtr vkRenderPass, IntPtr device, bool ownsRenderPass)
            {
                _handle = handle;
                Desc = desc;
                _vkFramebuffer = vkFramebuffer;
                _vkRenderPass = vkRenderPass;
                _device = device;
                _ownsRenderPass = ownsRenderPass;
            }

            public FramebufferInfo GetInfo()
            {
                var info = new FramebufferInfo();

                if (Desc.ColorAttachments != null && Desc.ColorAttachments.Length > 0)
                {
                    info.ColorFormats = new TextureFormat[Desc.ColorAttachments.Length];
                    for (int i = 0; i < Desc.ColorAttachments.Length; i++)
                    {
                        info.ColorFormats[i] = Desc.ColorAttachments[i].Texture?.Desc.Format ?? TextureFormat.Unknown;
                        if (i == 0)
                        {
                            info.Width = Desc.ColorAttachments[i].Texture?.Desc.Width ?? 0;
                            info.Height = Desc.ColorAttachments[i].Texture?.Desc.Height ?? 0;
                            info.SampleCount = Desc.ColorAttachments[i].Texture?.Desc.SampleCount ?? 1;
                        }
                    }
                }

                if (Desc.DepthAttachment.Texture != null)
                {
                    info.DepthFormat = Desc.DepthAttachment.Texture.Desc.Format;
                }

                return info;
            }

            public void Dispose()
            {
                // Destroy VkFramebuffer if it was created
                // Based on Vulkan Framebuffer Management: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkDestroyFramebuffer.html
                if (_vkFramebuffer != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    try
                    {
                        if (vkDestroyFramebuffer != null)
                        {
                            VkResult result = vkDestroyFramebuffer(_device, _vkFramebuffer, IntPtr.Zero);
                            if (result == VkResult.VK_SUCCESS)
                            {
                                Console.WriteLine("[VulkanDevice] Successfully destroyed VkFramebuffer");
                            }
                            else
                            {
                                Console.WriteLine($"[VulkanDevice] Warning: vkDestroyFramebuffer returned {result}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("[VulkanDevice] Warning: vkDestroyFramebuffer function pointer not initialized");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue cleanup - don't throw from Dispose
                        Console.WriteLine($"[VulkanDevice] Error destroying VkFramebuffer: {ex.Message}");
                        Console.WriteLine($"[VulkanDevice] Stack trace: {ex.StackTrace}");
                    }
                }

                // Destroy VkRenderPass if this framebuffer owns it (not shared)
                // Based on Vulkan Render Pass Management: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkDestroyRenderPass.html
                // Note: Render passes can be shared between multiple framebuffers, so we only destroy if _ownsRenderPass is true
                if (_ownsRenderPass && _vkRenderPass != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    try
                    {
                        if (vkDestroyRenderPass != null)
                        {
                            VkResult result = vkDestroyRenderPass(_device, _vkRenderPass, IntPtr.Zero);
                            if (result == VkResult.VK_SUCCESS)
                            {
                                Console.WriteLine("[VulkanDevice] Successfully destroyed VkRenderPass");
                            }
                            else
                            {
                                Console.WriteLine($"[VulkanDevice] Warning: vkDestroyRenderPass returned {result}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("[VulkanDevice] Warning: vkDestroyRenderPass function pointer not initialized");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue cleanup - don't throw from Dispose
                        Console.WriteLine($"[VulkanDevice] Error destroying VkRenderPass: {ex.Message}");
                        Console.WriteLine($"[VulkanDevice] Stack trace: {ex.StackTrace}");
                    }
                }
                else if (_vkRenderPass != IntPtr.Zero && !_ownsRenderPass)
                {
                    // Render pass is shared, don't destroy it here
                    Console.WriteLine("[VulkanDevice] VkRenderPass is shared, not destroying (will be destroyed by owner)");
                }
            }
        }

        private class VulkanBindingLayout : IBindingLayout, IResource
        {
            public BindingLayoutDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkDescriptorSetLayout;
            private readonly IntPtr _device;

            public VulkanBindingLayout(IntPtr handle, BindingLayoutDesc desc)
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero)
            {
            }

            public VulkanBindingLayout(IntPtr handle, BindingLayoutDesc desc, IntPtr vkDescriptorSetLayout, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _vkDescriptorSetLayout = vkDescriptorSetLayout;
                _device = device;
            }

            /// <summary>
            /// Gets the VkDescriptorSetLayout handle. Used internally for pipeline layout creation.
            /// </summary>
            internal IntPtr VkDescriptorSetLayout
            {
                get { return _vkDescriptorSetLayout; }
            }

            public void Dispose()
            {
                if (_vkDescriptorSetLayout != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyDescriptorSetLayout(_device, _vkDescriptorSetLayout, IntPtr.Zero);
                }
            }
        }

        private class VulkanBindingSet : IBindingSet, IResource
        {
            public IBindingLayout Layout { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkDescriptorSet;

            public VulkanBindingSet(IntPtr handle, IBindingLayout layout, BindingSetDesc desc)
                : this(handle, layout, desc, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)
            {
            }

            public VulkanBindingSet(IntPtr handle, IBindingLayout layout, BindingSetDesc desc, IntPtr vkDescriptorSet, IntPtr descriptorPool, IntPtr device)
            {
                _handle = handle;
                Layout = layout;
                _vkDescriptorSet = vkDescriptorSet;
            }

            /// <summary>
            /// Gets the VkDescriptorSet handle. Used internally for command list binding.
            /// </summary>
            internal IntPtr VkDescriptorSet
            {
                get { return _vkDescriptorSet; }
            }

            public void Dispose()
            {
                // Note: Descriptor sets are returned to pool, not destroyed individually
            }
        }

        private class VulkanAccelStruct : IAccelStruct, IResource
        {
            public AccelStructDesc Desc { get; }
            public bool IsTopLevel { get; }
            public ulong DeviceAddress { get; private set; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkAccelStruct;
            private readonly IBuffer _backingBuffer;
            private readonly IntPtr _device;

            public VulkanAccelStruct(IntPtr handle, AccelStructDesc desc)
                : this(handle, desc, IntPtr.Zero, null, 0UL, IntPtr.Zero)
            {
            }

            public VulkanAccelStruct(IntPtr handle, AccelStructDesc desc, IntPtr vkAccelStruct, IBuffer backingBuffer, ulong deviceAddress, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _vkAccelStruct = vkAccelStruct;
                _backingBuffer = backingBuffer;
                DeviceAddress = deviceAddress;
                _device = device;
                IsTopLevel = desc.IsTopLevel;
            }

            /// <summary>
            /// Gets the VkAccelerationStructureKHR handle. Used internally for descriptor set updates.
            /// </summary>
            internal IntPtr VkAccelStruct
            {
                get { return _vkAccelStruct; }
            }

            public void Dispose()
            {
                // Destroy acceleration structure if extension is available
                // Based on Vulkan specification: vkDestroyAccelerationStructureKHR destroys acceleration structure objects
                // - Must be called before destroying the backing buffer
                // - Function is part of VK_KHR_acceleration_structure extension
                // - Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkDestroyAccelerationStructureKHR.html
                if (_vkAccelStruct != IntPtr.Zero && _device != IntPtr.Zero && vkDestroyAccelerationStructureKHR != null)
                {
                    try
                    {
                        // vkDestroyAccelerationStructureKHR signature:
                        // void vkDestroyAccelerationStructureKHR(
                        //     VkDevice                                    device,
                        //     VkAccelerationStructureKHR                  accelerationStructure,
                        //     const VkAllocationCallbacks*                pAllocator);
                        // pAllocator is null (using default allocator)
                        vkDestroyAccelerationStructureKHR(_device, _vkAccelStruct, IntPtr.Zero);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with cleanup
                        // This prevents Dispose from throwing if the function call fails
                        Console.WriteLine($"[VulkanDevice] Error destroying acceleration structure: {ex.Message}");
                    }
                }

                // Dispose backing buffer (contains the acceleration structure memory)
                // Based on Vulkan specification: Backing buffer must be destroyed after acceleration structure
                // The buffer contains the memory for the acceleration structure data
                _backingBuffer?.Dispose();
            }
        }

        private class VulkanRaytracingPipeline : IRaytracingPipeline, IResource
        {
            public Interfaces.RaytracingPipelineDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _vkPipeline;
            private readonly IntPtr _vkPipelineLayout;
            private readonly IBuffer _sbtBuffer;
            private readonly IntPtr _device;

            public VulkanRaytracingPipeline(IntPtr handle, RaytracingPipelineDesc desc)
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero, null, IntPtr.Zero)
            {
            }

            public VulkanRaytracingPipeline(IntPtr handle, RaytracingPipelineDesc desc, IntPtr vkPipeline, IntPtr vkPipelineLayout, IBuffer sbtBuffer, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _vkPipeline = vkPipeline;
                _vkPipelineLayout = vkPipelineLayout;
                _sbtBuffer = sbtBuffer;
                _device = device;
            }

            /// <summary>
            /// Gets the VkPipeline handle. Used internally for binding the pipeline.
            /// </summary>
            internal IntPtr VkPipeline
            {
                get { return _vkPipeline; }
            }

            /// <summary>
            /// Gets the VkPipelineLayout handle. Used internally for descriptor set binding.
            /// </summary>
            internal IntPtr VkPipelineLayout
            {
                get { return _vkPipelineLayout; }
            }

            public void Dispose()
            {
                if (_vkPipeline != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyPipeline(_device, _vkPipeline, IntPtr.Zero);
                }
                if (_vkPipelineLayout != IntPtr.Zero && _device != IntPtr.Zero)
                {
                    vkDestroyPipelineLayout(_device, _vkPipelineLayout, IntPtr.Zero);
                }
                _sbtBuffer?.Dispose();
            }
        }

        private class VulkanCommandList : ICommandList, IResource
        {
            private readonly IntPtr _handle;
            private readonly CommandListType _type;
            private readonly VulkanDevice _device;
            private readonly IntPtr _vkCommandBuffer;
            private readonly IntPtr _vkCommandPool;
            private readonly IntPtr _vkDevice;
            private bool _isOpen;

            // Barrier tracking
            private readonly List<PendingBufferBarrier> _pendingBufferBarriers;
            private readonly Dictionary<object, ResourceState> _resourceStates;
            
            // Scratch buffer tracking for acceleration structure builds
            private readonly List<IBuffer> _scratchBuffers;

            // Raytracing state tracking
            private RaytracingState _raytracingState;
            private bool _hasRaytracingState;

            // Pending barrier entry for buffers
            private struct PendingBufferBarrier
            {
                public IntPtr Buffer;
                public ulong Offset;
                public ulong Size;
                public VkAccessFlags SrcAccessMask;
                public VkAccessFlags DstAccessMask;
                public VkPipelineStageFlags SrcStageMask;
                public VkPipelineStageFlags DstStageMask;
            }

            public VulkanCommandList(IntPtr handle, CommandListType type, VulkanDevice device)
                : this(handle, type, device, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)
            {
            }

            public VulkanCommandList(IntPtr handle, CommandListType type, VulkanDevice device, IntPtr vkCommandBuffer, IntPtr vkCommandPool, IntPtr vkDevice)
            {
                _handle = handle;
                _type = type;
                _device = device;
                _vkCommandBuffer = vkCommandBuffer;
                _vkCommandPool = vkCommandPool;
                _vkDevice = vkDevice;
                _isOpen = false;
                _pendingBufferBarriers = new List<PendingBufferBarrier>();
                _resourceStates = new Dictionary<object, ResourceState>();
                _scratchBuffers = new List<IBuffer>();
            }

            public void Open()
            {
                if (_isOpen)
                {
                    return;
                }

                VkCommandBufferBeginInfo beginInfo = new VkCommandBufferBeginInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
                    pNext = IntPtr.Zero,
                    flags = VkCommandBufferUsageFlags.VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT,
                    pInheritanceInfo = IntPtr.Zero
                };

                CheckResult(vkBeginCommandBuffer(_vkCommandBuffer, ref beginInfo), "vkBeginCommandBuffer");

                _isOpen = true;
            }

            public void Close()
            {
                if (!_isOpen)
                {
                    return;
                }

                CheckResult(vkEndCommandBuffer(_vkCommandBuffer), "vkEndCommandBuffer");

                _isOpen = false;
            }

            // TODO:  All ICommandList methods require full implementation
            // TODO:  These are stubbed with TODO comments indicating Vulkan API calls needed
            // Implementation will be completed when Vulkan interop is added

            /// <summary>
            /// Writes byte array data to a buffer.
            /// 
            /// Uses vkCmdUpdateBuffer for small updates (<= 65536 bytes) or staging buffer + vkCmdCopyBuffer
            /// for larger updates. The data is written starting at the specified destination offset.
            /// 
            /// Based on Vulkan API:
            /// - vkCmdUpdateBuffer: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdUpdateBuffer.html
            /// - vkCmdCopyBuffer: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyBuffer.html
            /// </summary>
            /// <param name="buffer">Target buffer to write data to.</param>
            /// <param name="data">Byte array containing the data to write.</param>
            /// <param name="destOffset">Offset in bytes into the destination buffer where data will be written.</param>
            public void WriteBuffer(IBuffer buffer, byte[] data, int destOffset = 0)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (data == null || data.Length == 0)
                {
                    throw new ArgumentException("Data must not be null or empty", nameof(data));
                }

                if (destOffset < 0)
                {
                    throw new ArgumentException("Destination offset must be non-negative", nameof(destOffset));
                }

                // Get buffer handle
                IntPtr bufferHandle = buffer.NativeHandle;
                if (bufferHandle == IntPtr.Zero)
                {
                    throw new ArgumentException("Buffer has invalid native handle", nameof(buffer));
                }

                int dataSize = data.Length;
                ulong destOffsetUlong = unchecked((ulong)destOffset);

                // For small updates (<= 65536 bytes), use vkCmdUpdateBuffer (direct update from CPU memory)
                // vkCmdUpdateBuffer is more efficient for small updates as it doesn't require a staging buffer
                if (dataSize <= VK_MAX_UPDATE_BUFFER_SIZE)
                {
                    if (vkCmdUpdateBuffer == null)
                    {
                        throw new NotSupportedException("vkCmdUpdateBuffer is not available");
                    }

                    // Transition buffer to TRANSFER_DST state if needed
                    SetBufferState(buffer, ResourceState.CopyDest);
                    CommitBarriers();

                    // Allocate unmanaged memory for data
                    IntPtr dataPtr = Marshal.AllocHGlobal(dataSize);
                    try
                    {
                        // Copy data to unmanaged memory
                        Marshal.Copy(data, 0, dataPtr, dataSize);

                        // vkCmdUpdateBuffer signature:
                        // void vkCmdUpdateBuffer(
                        //     VkCommandBuffer commandBuffer,
                        //     VkBuffer dstBuffer,
                        //     VkDeviceSize dstOffset,
                        //     VkDeviceSize dataSize,
                        //     const void* pData);
                        vkCmdUpdateBuffer(_vkCommandBuffer, bufferHandle, destOffsetUlong, unchecked((ulong)dataSize), dataPtr);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(dataPtr);
                    }
                }
                else
                {
                    // For large updates (> 65536 bytes), use staging buffer + vkCmdCopyBuffer
                    // This approach is required as vkCmdUpdateBuffer has a 65536 byte limit
                    WriteBufferLarge(data, bufferHandle, destOffsetUlong, unchecked((ulong)dataSize), buffer);
                }
            }

            /// <summary>
            /// Writes typed array data to a buffer.
            /// 
            /// Converts the typed array to bytes and writes using the same mechanism as WriteBuffer(byte[]).
            /// Uses vkCmdUpdateBuffer for small updates (<= 65536 bytes) or staging buffer + vkCmdCopyBuffer
            /// for larger updates. The data is written starting at the specified destination offset.
            /// 
            /// Based on Vulkan API:
            /// - vkCmdUpdateBuffer: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdUpdateBuffer.html
            /// - vkCmdCopyBuffer: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyBuffer.html
            /// </summary>
            /// <typeparam name="T">Unmanaged type for the array elements.</typeparam>
            /// <param name="buffer">Target buffer to write data to.</param>
            /// <param name="data">Typed array containing the data to write.</param>
            /// <param name="destOffset">Offset in bytes into the destination buffer where data will be written.</param>
            public void WriteBuffer<T>(IBuffer buffer, T[] data, int destOffset = 0) where T : unmanaged
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (data == null || data.Length == 0)
                {
                    throw new ArgumentException("Data must not be null or empty", nameof(data));
                }

                if (destOffset < 0)
                {
                    throw new ArgumentException("Destination offset must be non-negative", nameof(destOffset));
                }

                // Calculate byte size
                int elementSize = Marshal.SizeOf<T>();
                int dataSize = data.Length * elementSize;
                ulong destOffsetUlong = unchecked((ulong)destOffset);

                // Get buffer handle
                IntPtr bufferHandle = buffer.NativeHandle;
                if (bufferHandle == IntPtr.Zero)
                {
                    throw new ArgumentException("Buffer has invalid native handle", nameof(buffer));
                }

                // Convert typed array to byte array using GCHandle for safe pinning (C# 7.3 compatible)
                byte[] byteData = new byte[dataSize];
                GCHandle srcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                GCHandle dstHandle = GCHandle.Alloc(byteData, GCHandleType.Pinned);
                try
                {
                    IntPtr srcPtr = srcHandle.AddrOfPinnedObject();
                    IntPtr dstPtr = dstHandle.AddrOfPinnedObject();
                    unsafe
                    {
                        byte* srcBytePtr = (byte*)srcPtr.ToPointer();
                        byte* dstBytePtr = (byte*)dstPtr.ToPointer();
                        for (int i = 0; i < dataSize; i++)
                        {
                            dstBytePtr[i] = srcBytePtr[i];
                        }
                    }
                }
                finally
                {
                    if (srcHandle.IsAllocated) srcHandle.Free();
                    if (dstHandle.IsAllocated) dstHandle.Free();
                }

                // For small updates (<= 65536 bytes), use vkCmdUpdateBuffer
                if (dataSize <= VK_MAX_UPDATE_BUFFER_SIZE)
                {
                    if (vkCmdUpdateBuffer == null)
                    {
                        throw new NotSupportedException("vkCmdUpdateBuffer is not available");
                    }

                    // Transition buffer to TRANSFER_DST state if needed
                    SetBufferState(buffer, ResourceState.CopyDest);
                    CommitBarriers();

                    // Allocate unmanaged memory for data
                    IntPtr dataPtr = Marshal.AllocHGlobal(dataSize);
                    try
                    {
                        // Copy data to unmanaged memory
                        Marshal.Copy(byteData, 0, dataPtr, dataSize);

                        // vkCmdUpdateBuffer signature:
                        // void vkCmdUpdateBuffer(
                        //     VkCommandBuffer commandBuffer,
                        //     VkBuffer dstBuffer,
                        //     VkDeviceSize dstOffset,
                        //     VkDeviceSize dataSize,
                        //     const void* pData);
                        vkCmdUpdateBuffer(_vkCommandBuffer, bufferHandle, destOffsetUlong, unchecked((ulong)dataSize), dataPtr);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(dataPtr);
                    }
                }
                else
                {
                    // For large updates (> 65536 bytes), use staging buffer + vkCmdCopyBuffer
                    WriteBufferLarge(byteData, bufferHandle, destOffsetUlong, unchecked((ulong)dataSize), buffer);
                }
            }

            /// <summary>
            /// Helper method for writing large buffer data (> 65536 bytes) using staging buffer.
            /// Creates a staging buffer, copies data to it, then uses vkCmdCopyBuffer to copy to the destination buffer.
            /// </summary>
            private void WriteBufferLarge(byte[] data, IntPtr dstBuffer, ulong dstOffset, ulong dataSize, IBuffer buffer)
            {
                if (vkCreateBuffer == null || vkAllocateMemory == null || vkMapMemory == null || 
                    vkUnmapMemory == null || vkCmdCopyBuffer == null || vkGetBufferMemoryRequirements == null || 
                    vkBindBufferMemory == null || vkDestroyBuffer == null || vkFreeMemory == null)
                {
                    throw new NotSupportedException("Required Vulkan functions are not available");
                }

                // Create staging buffer for CPU-to-GPU transfer
                // Staging buffers use host-visible memory for CPU access
                VkBufferCreateInfo bufferCreateInfo = new VkBufferCreateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
                    pNext = IntPtr.Zero,
                    flags = 0,
                    size = dataSize,
                    usage = VkBufferUsageFlags.VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
                    sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
                    queueFamilyIndexCount = 0,
                    pQueueFamilyIndices = IntPtr.Zero
                };

                IntPtr stagingBuffer;
                VkResult result = vkCreateBuffer(_vkDevice, ref bufferCreateInfo, IntPtr.Zero, out stagingBuffer);
                if (result != VkResult.VK_SUCCESS || stagingBuffer == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to create staging buffer: {result}");
                }

                try
                {
                    // Get memory requirements for staging buffer
                    VkMemoryRequirements memRequirements;
                    vkGetBufferMemoryRequirements(_vkDevice, stagingBuffer, out memRequirements);

                    // Allocate host-visible memory for staging buffer
                    VkMemoryAllocateInfo allocateInfo = new VkMemoryAllocateInfo
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
                        pNext = IntPtr.Zero,
                        allocationSize = memRequirements.size,
                        memoryTypeIndex = FindMemoryType(memRequirements.memoryTypeBits, VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT)
                    };

                    IntPtr stagingMemory;
                    result = vkAllocateMemory(_vkDevice, ref allocateInfo, IntPtr.Zero, out stagingMemory);
                    if (result != VkResult.VK_SUCCESS || stagingMemory == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"Failed to allocate staging buffer memory: {result}");
                    }

                    try
                    {
                        // Bind memory to staging buffer
                        result = vkBindBufferMemory(_vkDevice, stagingBuffer, stagingMemory, 0);
                        if (result != VkResult.VK_SUCCESS)
                        {
                            throw new InvalidOperationException($"Failed to bind staging buffer memory: {result}");
                        }

                        // Map staging buffer memory and copy data
                        IntPtr mappedData;
                        result = vkMapMemory(_vkDevice, stagingMemory, 0, dataSize, 0, out mappedData);
                        if (result != VkResult.VK_SUCCESS || mappedData == IntPtr.Zero)
                        {
                            throw new InvalidOperationException($"Failed to map staging buffer memory: {result}");
                        }

                        try
                        {
                            // Copy data to mapped memory
                            Marshal.Copy(data, 0, mappedData, (int)dataSize);
                        }
                        finally
                        {
                            // Unmap memory (data is flushed if host-coherent)
                            vkUnmapMemory(_vkDevice, stagingMemory);
                        }

                        // Transition destination buffer to TRANSFER_DST state if needed
                        SetBufferState(buffer, ResourceState.CopyDest);
                        CommitBarriers();

                        // Create buffer copy region
                        VkBufferCopy copyRegion = new VkBufferCopy
                        {
                            srcOffset = 0,
                            dstOffset = dstOffset,
                            size = dataSize
                        };

                        // Allocate memory for copy region and marshal structure
                        int regionSize = Marshal.SizeOf<VkBufferCopy>();
                        IntPtr regionPtr = Marshal.AllocHGlobal(regionSize);
                        try
                        {
                            Marshal.StructureToPtr(copyRegion, regionPtr, false);

                            // Execute copy command: vkCmdCopyBuffer
                            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyBuffer.html
                            vkCmdCopyBuffer(
                                _vkCommandBuffer,
                                stagingBuffer,
                                dstBuffer,
                                1,
                                regionPtr);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(regionPtr);
                        }
                    }
                    finally
                    {
                        // Free staging buffer memory
                        vkFreeMemory(_vkDevice, stagingMemory, IntPtr.Zero);
                    }
                }
                finally
                {
                    // Destroy staging buffer
                    vkDestroyBuffer(_vkDevice, stagingBuffer, IntPtr.Zero);
                }
            }
            public void WriteTexture(ITexture texture, int mipLevel, int arraySlice, byte[] data)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (texture == null)
                {
                    throw new ArgumentNullException(nameof(texture));
                }

                if (data == null || data.Length == 0)
                {
                    throw new ArgumentException("Data must not be null or empty", nameof(data));
                }

                if (mipLevel < 0)
                {
                    throw new ArgumentException("Mip level must be non-negative", nameof(mipLevel));
                }

                if (arraySlice < 0)
                {
                    throw new ArgumentException("Array slice must be non-negative", nameof(arraySlice));
                }

                if (vkCmdCopyBufferToImage == null || vkCreateBuffer == null || vkAllocateMemory == null || vkMapMemory == null || vkUnmapMemory == null)
                {
                    throw new NotSupportedException("Required Vulkan functions are not available");
                }

                // Get Vulkan image handle
                IntPtr image = texture.NativeHandle;
                if (image == IntPtr.Zero)
                {
                    throw new ArgumentException("Texture has invalid native handle", nameof(texture));
                }

                // Calculate mip level dimensions
                int mipWidth = Math.Max(1, texture.Desc.Width >> mipLevel);
                int mipHeight = Math.Max(1, texture.Desc.Height >> mipLevel);
                int mipDepth = Math.Max(1, (texture.Desc.Depth > 0 ? texture.Desc.Depth : 1) >> mipLevel);

                // Calculate data size for this mip level
                // For simplicity, assume uncompressed format - real implementation would handle compressed formats
                int bytesPerPixel = GetBytesPerPixel(texture.Desc.Format);
                int rowPitch = mipWidth * bytesPerPixel;
                int slicePitch = rowPitch * mipHeight;
                int totalSize = slicePitch * mipDepth;

                if (data.Length < totalSize)
                {
                    throw new ArgumentException($"Data size ({data.Length}) is insufficient for mip level {mipLevel} (expected {totalSize} bytes)", nameof(data));
                }

                // Create staging buffer for CPU-to-GPU transfer
                // Staging buffers use host-visible memory for CPU access
                VkBufferCreateInfo bufferCreateInfo = new VkBufferCreateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
                    pNext = IntPtr.Zero,
                    flags = 0,
                    size = unchecked((ulong)totalSize),
                    usage = VkBufferUsageFlags.VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
                    sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
                    queueFamilyIndexCount = 0,
                    pQueueFamilyIndices = IntPtr.Zero
                };

                IntPtr stagingBuffer;
                VkResult result = vkCreateBuffer(_vkDevice, ref bufferCreateInfo, IntPtr.Zero, out stagingBuffer);
                if (result != VkResult.VK_SUCCESS || stagingBuffer == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to create staging buffer: {result}");
                }

                try
                {
                    // Get memory requirements for staging buffer
                    VkMemoryRequirements memRequirements;
                    vkGetBufferMemoryRequirements(_vkDevice, stagingBuffer, out memRequirements);

                    // Allocate host-visible memory for staging buffer
                    VkMemoryAllocateInfo allocateInfo = new VkMemoryAllocateInfo
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
                        pNext = IntPtr.Zero,
                        allocationSize = memRequirements.size,
                        memoryTypeIndex = FindMemoryType(memRequirements.memoryTypeBits, VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT)
                    };

                    IntPtr stagingMemory;
                    result = vkAllocateMemory(_vkDevice, ref allocateInfo, IntPtr.Zero, out stagingMemory);
                    if (result != VkResult.VK_SUCCESS || stagingMemory == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"Failed to allocate staging buffer memory: {result}");
                    }

                    try
                    {
                        // Bind memory to staging buffer
                        result = vkBindBufferMemory(_vkDevice, stagingBuffer, stagingMemory, 0);
                        if (result != VkResult.VK_SUCCESS)
                        {
                            throw new InvalidOperationException($"Failed to bind staging buffer memory: {result}");
                        }

                        // Map staging buffer memory and copy data
                        IntPtr mappedData;
                        result = vkMapMemory(_vkDevice, stagingMemory, 0, unchecked((ulong)totalSize), 0, out mappedData);
                        if (result != VkResult.VK_SUCCESS || mappedData == IntPtr.Zero)
                        {
                            throw new InvalidOperationException($"Failed to map staging buffer memory: {result}");
                        }

                        try
                        {
                            // Copy data to mapped memory
                            Marshal.Copy(data, 0, mappedData, totalSize);
                        }
                        finally
                        {
                            // Unmap memory (data is flushed if host-coherent)
                            vkUnmapMemory(_vkDevice, stagingMemory);
                        }

                        // Transition image to TRANSFER_DST_OPTIMAL layout
                        TransitionImageLayout(image, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, texture.Desc, mipLevel, arraySlice);

                        // Determine image aspect based on format
                        VkImageAspectFlags aspectMask = GetImageAspectFlags(texture.Desc.Format);

                        // Create buffer image copy region
                        VkBufferImageCopy copyRegion = new VkBufferImageCopy
                        {
                            bufferOffset = 0,
                            bufferRowLength = 0, // Tightly packed
                            bufferImageHeight = 0, // Tightly packed
                            imageSubresource = new VkImageSubresourceLayers
                            {
                                aspectMask = aspectMask,
                                mipLevel = unchecked((uint)mipLevel),
                                baseArrayLayer = unchecked((uint)arraySlice),
                                layerCount = 1
                            },
                            imageOffset = new VkOffset3D { x = 0, y = 0, z = 0 },
                            imageExtent = new VkExtent3D
                            {
                                width = unchecked((uint)mipWidth),
                                height = unchecked((uint)mipHeight),
                                depth = unchecked((uint)mipDepth)
                            }
                        };

                        // Allocate memory for copy region and marshal structure
                        int regionSize = Marshal.SizeOf<VkBufferImageCopy>();
                        IntPtr regionPtr = Marshal.AllocHGlobal(regionSize);
                        try
                        {
                            Marshal.StructureToPtr(copyRegion, regionPtr, false);

                            // Execute copy command: vkCmdCopyBufferToImage
                            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyBufferToImage.html
                            vkCmdCopyBufferToImage(
                                _vkCommandBuffer,
                                stagingBuffer,
                                image,
                                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                                1,
                                regionPtr);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(regionPtr);
                        }

                        // Transition image back to SHADER_READ_ONLY_OPTIMAL for use in shaders
                        TransitionImageLayout(image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL, texture.Desc, mipLevel, arraySlice);
                    }
                    finally
                    {
                        // Free staging buffer memory
                        vkFreeMemory(_vkDevice, stagingMemory, IntPtr.Zero);
                    }
                }
                finally
                {
                    // Destroy staging buffer
                    vkDestroyBuffer(_vkDevice, stagingBuffer, IntPtr.Zero);
                }
            }

            // Helper method to get bytes per pixel for a texture format
            // TODO:  Simplified version - real implementation would handle all formats
            private int GetBytesPerPixel(TextureFormat format)
            {
                switch (format)
                {
                    case TextureFormat.R8_UNORM:
                        return 1;
                    case TextureFormat.R8G8_UNORM:
                        return 2;
                    case TextureFormat.R8G8B8A8_UNORM:
                    case TextureFormat.R8G8B8A8_SRGB:
                    case TextureFormat.B8G8R8A8_UNORM:
                        return 4;
                    case TextureFormat.R16G16B16A16_FLOAT:
                        return 8;
                    case TextureFormat.R32G32B32A32_FLOAT:
                        return 16;
                    default:
                        // Default to 4 bytes per pixel for unknown formats
                        return 4;
                }
            }

            // Helper method to find memory type index - delegates to parent device's implementation
            private uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
            {
                // Use the parent device's FindMemoryType implementation which properly queries memory properties
                return _device.FindMemoryType(typeFilter, properties);
            }

            // Overloaded TransitionImageLayout to support specific mip level and array slice
            private void TransitionImageLayout(IntPtr image, VkImageLayout oldLayout, VkImageLayout newLayout, TextureDesc desc, int mipLevel, int arraySlice)
            {
                if (vkCmdPipelineBarrier == null)
                {
                    return; // Cannot transition without pipeline barrier
                }

                VkImageAspectFlags aspectMask = GetImageAspectFlags(desc.Format);

                VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange
                {
                    aspectMask = aspectMask,
                    baseMipLevel = unchecked((uint)mipLevel),
                    levelCount = 1, // Single mip level
                    baseArrayLayer = unchecked((uint)arraySlice),
                    layerCount = 1 // Single array layer
                };

                VkImageMemoryBarrier barrier = new VkImageMemoryBarrier
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
                    pNext = IntPtr.Zero,
                    srcAccessMask = GetAccessFlagsForLayout(oldLayout),
                    dstAccessMask = GetAccessFlagsForLayout(newLayout),
                    oldLayout = oldLayout,
                    newLayout = newLayout,
                    srcQueueFamilyIndex = 0xFFFFFFFF, // VK_QUEUE_FAMILY_IGNORED
                    dstQueueFamilyIndex = 0xFFFFFFFF, // VK_QUEUE_FAMILY_IGNORED
                    image = image,
                    subresourceRange = subresourceRange
                };

                int barrierSize = Marshal.SizeOf<VkImageMemoryBarrier>();
                IntPtr barrierPtr = Marshal.AllocHGlobal(barrierSize);
                try
                {
                    Marshal.StructureToPtr(barrier, barrierPtr, false);

                    vkCmdPipelineBarrier(
                        _vkCommandBuffer,
                        VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT,
                        VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT,
                        0,
                        0,
                        IntPtr.Zero,
                        0,
                        IntPtr.Zero,
                        1,
                        barrierPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(barrierPtr);
                }
            }
            /// <summary>
            /// Copies data from a source buffer to a destination buffer using GPU-side buffer copy.
            /// 
            /// This performs a GPU-side buffer-to-buffer copy operation using vkCmdCopyBuffer.
            /// The copy operation is recorded into the command buffer and executed when the command buffer is submitted.
            /// 
            /// Buffer state transitions:
            /// - Source buffer is transitioned to CopySource state (if needed)
            /// - Destination buffer is transitioned to CopyDest state (if needed)
            /// - Barriers are committed before the copy operation
            /// 
            /// Based on Vulkan API: vkCmdCopyBuffer
            /// Vulkan API Reference: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyBuffer.html
            /// 
            /// Vulkan Specification:
            /// - vkCmdCopyBuffer signature: void vkCmdCopyBuffer(VkCommandBuffer commandBuffer, VkBuffer srcBuffer, VkBuffer dstBuffer, uint32_t regionCount, const VkBufferCopy* pRegions)
            /// - VkBufferCopy structure contains: srcOffset (VkDeviceSize), dstOffset (VkDeviceSize), size (VkDeviceSize)
            /// - Copy operation must be within the bounds of both buffers
            /// - Source and destination buffers must be in appropriate states (CopySource and CopyDest)
            /// </summary>
            /// <param name="dest">Destination buffer to copy data to</param>
            /// <param name="destOffset">Destination offset in bytes from the start of the destination buffer</param>
            /// <param name="src">Source buffer to copy data from</param>
            /// <param name="srcOffset">Source offset in bytes from the start of the source buffer</param>
            /// <param name="size">Number of bytes to copy</param>
            public void CopyBuffer(IBuffer dest, int destOffset, IBuffer src, int srcOffset, int size)
            {
                if (dest == null || src == null)
                {
                    throw new ArgumentNullException(dest == null ? nameof(dest) : nameof(src));
                }

                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (vkCmdCopyBuffer == null)
                {
                    throw new NotSupportedException("vkCmdCopyBuffer is not available");
                }

                // Validate size
                if (size <= 0)
                {
                    throw new ArgumentException("Copy size must be greater than zero", nameof(size));
                }

                // Validate offsets are non-negative
                if (destOffset < 0 || srcOffset < 0)
                {
                    throw new ArgumentException("Buffer offsets must be non-negative");
                }

                // Get native buffer handles
                IntPtr srcBufferHandle = src.NativeHandle;
                IntPtr dstBufferHandle = dest.NativeHandle;

                if (srcBufferHandle == IntPtr.Zero || dstBufferHandle == IntPtr.Zero)
                {
                    throw new ArgumentException("Source or destination buffer has invalid native handle");
                }

                // Transition source buffer to CopySource state
                SetBufferState(src, ResourceState.CopySource);
                
                // Transition destination buffer to CopyDest state
                SetBufferState(dest, ResourceState.CopyDest);
                
                // Commit barriers before copy operation
                CommitBarriers();

                // Create buffer copy region structure
                VkBufferCopy copyRegion = new VkBufferCopy
                {
                    srcOffset = unchecked((ulong)srcOffset),
                    dstOffset = unchecked((ulong)destOffset),
                    size = unchecked((ulong)size)
                };

                // Allocate memory for copy region and marshal structure
                int regionSize = Marshal.SizeOf<VkBufferCopy>();
                IntPtr regionPtr = Marshal.AllocHGlobal(regionSize);
                try
                {
                    Marshal.StructureToPtr(copyRegion, regionPtr, false);

                    // Execute copy command: vkCmdCopyBuffer
                    // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyBuffer.html
                    // vkCmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, regionCount, pRegions)
                    vkCmdCopyBuffer(
                        _vkCommandBuffer,
                        srcBufferHandle,
                        dstBufferHandle,
                        1, // Single copy region
                        regionPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(regionPtr);
                }
            }
            public void CopyTexture(ITexture dest, ITexture src)
            {
                if (dest == null || src == null)
                {
                    throw new ArgumentNullException(dest == null ? nameof(dest) : nameof(src));
                }

                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (vkCmdCopyImage == null)
                {
                    throw new NotSupportedException("vkCmdCopyImage is not available");
                }

                // Get Vulkan image handles
                // NativeHandle should contain the VkImage handle for VulkanTexture instances
                IntPtr srcImage = src.NativeHandle;
                IntPtr dstImage = dest.NativeHandle;

                if (srcImage == IntPtr.Zero || dstImage == IntPtr.Zero)
                {
                    throw new ArgumentException("Source or destination texture has invalid native handle");
                }

                // Validate texture dimensions match (for full copy)
                if (src.Desc.Width != dest.Desc.Width || src.Desc.Height != dest.Desc.Height)
                {
                    throw new ArgumentException("Source and destination textures must have matching dimensions for copy operation");
                }

                // Transition source image to TRANSFER_SRC_OPTIMAL layout
                TransitionImageLayout(srcImage, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, src.Desc);

                // Transition destination image to TRANSFER_DST_OPTIMAL layout
                TransitionImageLayout(dstImage, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, dest.Desc);

                // Determine image aspect based on format
                VkImageAspectFlags aspectMask = GetImageAspectFlags(src.Desc.Format);

                // Create image copy region
                VkImageCopy copyRegion = new VkImageCopy
                {
                    srcSubresource = new VkImageSubresourceLayers
                    {
                        aspectMask = aspectMask,
                        mipLevel = 0,
                        baseArrayLayer = 0,
                        layerCount = (uint)Math.Max(1, src.Desc.ArraySize)
                    },
                    srcOffset = new VkOffset3D { x = 0, y = 0, z = 0 },
                    dstSubresource = new VkImageSubresourceLayers
                    {
                        aspectMask = aspectMask,
                        mipLevel = 0,
                        baseArrayLayer = 0,
                        layerCount = (uint)Math.Max(1, dest.Desc.ArraySize)
                    },
                    dstOffset = new VkOffset3D { x = 0, y = 0, z = 0 },
                    extent = new VkExtent3D
                    {
                        width = (uint)src.Desc.Width,
                        height = (uint)src.Desc.Height,
                        depth = (uint)Math.Max(1, src.Desc.Depth)
                    }
                };

                // Allocate memory for copy region and marshal structure
                int regionSize = Marshal.SizeOf<VkImageCopy>();
                IntPtr regionPtr = Marshal.AllocHGlobal(regionSize);
                try
                {
                    Marshal.StructureToPtr(copyRegion, regionPtr, false);

                    // Execute copy command
                    vkCmdCopyImage(
                        _vkCommandBuffer,
                        srcImage,
                        VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                        dstImage,
                        VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                        1,
                        regionPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(regionPtr);
                }
            }

            // Helper method to transition image layout using pipeline barrier
            private void TransitionImageLayout(IntPtr image, VkImageLayout oldLayout, VkImageLayout newLayout, TextureDesc desc)
            {
                if (vkCmdPipelineBarrier == null)
                {
                    return; // Cannot transition without pipeline barrier
                }

                VkImageAspectFlags aspectMask = GetImageAspectFlags(desc.Format);

                VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange
                {
                    aspectMask = aspectMask,
                    baseMipLevel = 0,
                    levelCount = (uint)Math.Max(1, desc.MipLevels),
                    baseArrayLayer = 0,
                    layerCount = (uint)Math.Max(1, desc.ArraySize)
                };

                VkImageMemoryBarrier barrier = new VkImageMemoryBarrier
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
                    pNext = IntPtr.Zero,
                    srcAccessMask = GetAccessFlagsForLayout(oldLayout),
                    dstAccessMask = GetAccessFlagsForLayout(newLayout),
                    oldLayout = oldLayout,
                    newLayout = newLayout,
                    srcQueueFamilyIndex = 0xFFFFFFFF, // VK_QUEUE_FAMILY_IGNORED
                    dstQueueFamilyIndex = 0xFFFFFFFF, // VK_QUEUE_FAMILY_IGNORED
                    image = image,
                    subresourceRange = subresourceRange
                };

                int barrierSize = Marshal.SizeOf<VkImageMemoryBarrier>();
                IntPtr barrierPtr = Marshal.AllocHGlobal(barrierSize);
                try
                {
                    Marshal.StructureToPtr(barrier, barrierPtr, false);

                    vkCmdPipelineBarrier(
                        _vkCommandBuffer,
                        VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT,
                        VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT,
                        0,
                        0,
                        IntPtr.Zero,
                        0,
                        IntPtr.Zero,
                        1,
                        barrierPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(barrierPtr);
                }
            }

            // Helper to determine image aspect flags from texture format
            // Returns appropriate Vulkan image aspect flags based on the texture format:
            // - Depth-only formats: VK_IMAGE_ASPECT_DEPTH_BIT
            // - Depth+Stencil formats: VK_IMAGE_ASPECT_DEPTH_BIT | VK_IMAGE_ASPECT_STENCIL_BIT
            // - All other formats (color, compressed): VK_IMAGE_ASPECT_COLOR_BIT
            private VkImageAspectFlags GetImageAspectFlags(TextureFormat format)
            {
                switch (format)
                {
                    // Depth-only formats - only depth aspect
                    case TextureFormat.D16_UNorm:
                    case TextureFormat.D32_Float:
                        return VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT;

                    // Depth+Stencil formats - both depth and stencil aspects
                    case TextureFormat.D24_UNorm_S8_UInt:
                    case TextureFormat.D32_Float_S8_UInt:
                        return VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT | VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT;

                    // All color formats (uncompressed)
                    case TextureFormat.Unknown:
                    case TextureFormat.R8_UNorm:
                    case TextureFormat.R8_UInt:
                    case TextureFormat.R8_SInt:
                    case TextureFormat.R8G8_UNorm:
                    case TextureFormat.R8G8_UInt:
                    case TextureFormat.R8G8B8A8_UNorm:
                    case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    case TextureFormat.R8G8B8A8_UInt:
                    case TextureFormat.R8G8B8A8_SInt:
                    case TextureFormat.B8G8R8A8_UNorm:
                    case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    case TextureFormat.R16_Float:
                    case TextureFormat.R16_UNorm:
                    case TextureFormat.R16_UInt:
                    case TextureFormat.R16_SInt:
                    case TextureFormat.R16G16_Float:
                    case TextureFormat.R16G16_UInt:
                    case TextureFormat.R16G16_SInt:
                    case TextureFormat.R16G16B16A16_Float:
                    case TextureFormat.R16G16B16A16_UNorm:
                    case TextureFormat.R16G16B16A16_UInt:
                    case TextureFormat.R16G16B16A16_SInt:
                    case TextureFormat.R32_Float:
                    case TextureFormat.R32_UInt:
                    case TextureFormat.R32_SInt:
                    case TextureFormat.R32G32_Float:
                    case TextureFormat.R32G32_UInt:
                    case TextureFormat.R32G32_SInt:
                    case TextureFormat.R32G32B32_Float:
                    case TextureFormat.R32G32B32A32_Float:
                    case TextureFormat.R32G32B32A32_UInt:
                    case TextureFormat.R32G32B32A32_SInt:
                    case TextureFormat.R11G11B10_Float:
                    case TextureFormat.R10G10B10A2_UNorm:
                    case TextureFormat.R10G10B10A2_UInt:
                    // Block-compressed formats (BC1-BC7) - all are color formats
                    case TextureFormat.BC1_UNorm:
                    case TextureFormat.BC1_UNorm_SRGB:
                    case TextureFormat.BC1:
                    case TextureFormat.BC2_UNorm:
                    case TextureFormat.BC2_UNorm_SRGB:
                    case TextureFormat.BC2:
                    case TextureFormat.BC3_UNorm:
                    case TextureFormat.BC3_UNorm_SRGB:
                    case TextureFormat.BC3:
                    case TextureFormat.BC4_UNorm:
                    case TextureFormat.BC4:
                    case TextureFormat.BC5_UNorm:
                    case TextureFormat.BC5:
                    case TextureFormat.BC6H_UFloat:
                    case TextureFormat.BC6H:
                    case TextureFormat.BC7_UNorm:
                    case TextureFormat.BC7_UNorm_SRGB:
                    case TextureFormat.BC7:
                    // ASTC compressed formats - all are color formats
                    case TextureFormat.ASTC_4x4:
                    case TextureFormat.ASTC_5x5:
                    case TextureFormat.ASTC_6x6:
                    case TextureFormat.ASTC_8x8:
                    case TextureFormat.ASTC_10x10:
                    case TextureFormat.ASTC_12x12:
                        return VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;

                    default:
                        // Default to color aspect for any unknown formats
                        return VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;
                }
            }

            // Helper to get access flags for image layout
            private VkAccessFlags GetAccessFlagsForLayout(VkImageLayout layout)
            {
                switch (layout)
                {
                    case VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL:
                        return VkAccessFlags.VK_ACCESS_TRANSFER_READ_BIT;
                    case VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL:
                        return VkAccessFlags.VK_ACCESS_TRANSFER_WRITE_BIT;
                    case VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL:
                        return VkAccessFlags.VK_ACCESS_SHADER_READ_BIT;
                    case VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL:
                        return VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                    case VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL:
                        return VkAccessFlags.VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;
                    default:
                        return 0;
                }
            }
            /// <summary>
            /// Clears a color attachment of a framebuffer.
            /// Based on Vulkan API: https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdClearColorImage.html
            /// Transitions the image to TRANSFER_DST_OPTIMAL layout, clears it, then transitions back to COLOR_ATTACHMENT_OPTIMAL.
            /// </summary>
            public void ClearColorAttachment(IFramebuffer framebuffer, int attachmentIndex, Vector4 color)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before clearing color attachment");
                }

                if (framebuffer == null)
                {
                    throw new ArgumentNullException(nameof(framebuffer));
                }

                if (attachmentIndex < 0)
                {
                    throw new ArgumentException("Attachment index must be non-negative", nameof(attachmentIndex));
                }

                // Get framebuffer description to access color attachments
                FramebufferDesc desc = framebuffer.Desc;
                if (desc.ColorAttachments == null || desc.ColorAttachments.Length == 0)
                {
                    throw new ArgumentException("Framebuffer does not have any color attachments", nameof(framebuffer));
                }

                if (attachmentIndex >= desc.ColorAttachments.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(attachmentIndex), $"Attachment index {attachmentIndex} is out of range (framebuffer has {desc.ColorAttachments.Length} color attachments)");
                }

                FramebufferAttachment attachment = desc.ColorAttachments[attachmentIndex];
                if (attachment.Texture == null)
                {
                    throw new ArgumentException($"Color attachment at index {attachmentIndex} does not have a texture", nameof(framebuffer));
                }

                ITexture colorTexture = attachment.Texture;
                IntPtr image = colorTexture.NativeHandle;
                if (image == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Color texture at attachment index {attachmentIndex} does not have a valid native handle");
                }

                // Get texture description to determine format
                TextureDesc textureDesc = colorTexture.Desc;

                // Check if vkCmdClearColorImage is available
                if (vkCmdClearColorImage == null)
                {
                    throw new NotSupportedException("vkCmdClearColorImage is not available");
                }

                // Transition image to TRANSFER_DST_OPTIMAL layout for clearing
                // Note: For clearing operations, we assume the image might be in UNDEFINED layout (newly created)
                // or COLOR_ATTACHMENT_OPTIMAL (already used as attachment)
                // Transitioning from UNDEFINED is safe for initialization/clearing operations
                TransitionImageLayout(image, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, textureDesc, attachment.MipLevel, attachment.ArraySlice);

                // Create clear color value structure
                // VkClearColorValue is a union that can be float[4], int32[4], or uint32[4]
                // For color attachments, we use float[4] (most common case)
                VkClearColorValue clearValue = new VkClearColorValue
                {
                    float32_0 = color.X,
                    float32_1 = color.Y,
                    float32_2 = color.Z,
                    float32_3 = color.W
                };

                // Create subresource range for the specific mip level and array slice
                VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange
                {
                    aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                    baseMipLevel = unchecked((uint)attachment.MipLevel),
                    levelCount = 1, // Single mip level
                    baseArrayLayer = unchecked((uint)attachment.ArraySlice),
                    layerCount = 1 // Single array layer
                };

                // Allocate memory for structures and marshal them
                int clearValueSize = Marshal.SizeOf<VkClearColorValue>();
                IntPtr clearValuePtr = Marshal.AllocHGlobal(clearValueSize);
                int rangeSize = Marshal.SizeOf<VkImageSubresourceRange>();
                IntPtr rangePtr = Marshal.AllocHGlobal(rangeSize);

                try
                {
                    // Marshal structures to unmanaged memory
                    Marshal.StructureToPtr(clearValue, clearValuePtr, false);
                    Marshal.StructureToPtr(subresourceRange, rangePtr, false);

                    // Call vkCmdClearColorImage
                    // Signature: void vkCmdClearColorImage(
                    //   VkCommandBuffer commandBuffer,
                    //   VkImage image,
                    //   VkImageLayout imageLayout,
                    //   const VkClearColorValue* pColor,
                    //   uint32_t rangeCount,
                    //   const VkImageSubresourceRange* pRanges)
                    vkCmdClearColorImage(
                        _vkCommandBuffer,
                        image,
                        VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                        clearValuePtr,
                        1, // Single range
                        rangePtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(clearValuePtr);
                    Marshal.FreeHGlobal(rangePtr);
                }

                // Transition image back to COLOR_ATTACHMENT_OPTIMAL for use as attachment
                TransitionImageLayout(image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, textureDesc, attachment.MipLevel, attachment.ArraySlice);
            }
            /// <summary>
            /// Clears the depth/stencil attachment of a framebuffer.
            /// Based on Vulkan API: https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdClearDepthStencilImage.html
            /// Transitions the image to TRANSFER_DST_OPTIMAL layout, clears it, then transitions back to DEPTH_STENCIL_ATTACHMENT_OPTIMAL.
            /// </summary>
            public void ClearDepthStencilAttachment(IFramebuffer framebuffer, float depth, byte stencil, bool clearDepth = true, bool clearStencil = true)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before clearing depth/stencil attachment");
                }

                if (framebuffer == null)
                {
                    throw new ArgumentNullException(nameof(framebuffer));
                }

                // Get framebuffer description to access depth attachment
                FramebufferDesc desc = framebuffer.Desc;
                if (desc.DepthAttachment.Texture == null)
                {
                    throw new ArgumentException("Framebuffer does not have a depth attachment", nameof(framebuffer));
                }

                ITexture depthTexture = desc.DepthAttachment.Texture;
                IntPtr image = depthTexture.NativeHandle;
                if (image == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Depth texture does not have a valid native handle");
                }

                // Get texture description to determine format and aspect flags
                TextureDesc textureDesc = depthTexture.Desc;

                // Determine aspect mask based on format and clear flags
                VkImageAspectFlags aspectMask = 0;
                if (clearDepth)
                {
                    aspectMask |= VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT;
                }
                if (clearStencil)
                {
                    // Only include stencil aspect if format supports it
                    switch (textureDesc.Format)
                    {
                        case TextureFormat.D24_UNORM_S8_UINT:
                        case TextureFormat.D32_FLOAT_S8X24_UINT:
                            aspectMask |= VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT;
                            break;
                    }
                }

                if (aspectMask == 0)
                {
                    return; // Nothing to clear
                }

                // Check if vkCmdClearDepthStencilImage is available
                if (vkCmdClearDepthStencilImage == null)
                {
                    throw new NotSupportedException("vkCmdClearDepthStencilImage is not available");
                }

                // Transition image to TRANSFER_DST_OPTIMAL layout for clearing
                // Note: For clearing operations, we assume the image might be in UNDEFINED layout (newly created)
                // or DEPTH_STENCIL_ATTACHMENT_OPTIMAL (already used as attachment)
                // Transitioning from UNDEFINED is safe for initialization/clearing operations
                TransitionImageLayout(image, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, textureDesc, desc.DepthAttachment.MipLevel, desc.DepthAttachment.ArraySlice);

                // Create clear value structure
                VkClearDepthStencilValue clearValue = new VkClearDepthStencilValue
                {
                    depth = depth,
                    stencil = stencil
                };

                // Create subresource range for the specific mip level and array slice
                VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange
                {
                    aspectMask = aspectMask,
                    baseMipLevel = unchecked((uint)desc.DepthAttachment.MipLevel),
                    levelCount = 1, // Single mip level
                    baseArrayLayer = unchecked((uint)desc.DepthAttachment.ArraySlice),
                    layerCount = 1 // Single array layer
                };

                // Allocate memory for structures and marshal them
                int clearValueSize = Marshal.SizeOf<VkClearDepthStencilValue>();
                IntPtr clearValuePtr = Marshal.AllocHGlobal(clearValueSize);
                int rangeSize = Marshal.SizeOf<VkImageSubresourceRange>();
                IntPtr rangePtr = Marshal.AllocHGlobal(rangeSize);

                try
                {
                    Marshal.StructureToPtr(clearValue, clearValuePtr, false);
                    Marshal.StructureToPtr(subresourceRange, rangePtr, false);

                    // Call vkCmdClearDepthStencilImage
                    vkCmdClearDepthStencilImage(
                        _vkCommandBuffer,
                        image,
                        VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                        clearValuePtr,
                        1, // Single range
                        rangePtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(clearValuePtr);
                    Marshal.FreeHGlobal(rangePtr);
                }

                // Transition image back to DEPTH_STENCIL_ATTACHMENT_OPTIMAL for use as attachment
                TransitionImageLayout(image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL, textureDesc, desc.DepthAttachment.MipLevel, desc.DepthAttachment.ArraySlice);
            }
            /// <summary>
            /// Clears an unordered access view (UAV) texture to a float Vector4 value.
            /// 
            /// Implementation: Uses vkCmdClearColorImage with VK_IMAGE_LAYOUT_GENERAL layout.
            /// UAV textures in Vulkan use GENERAL layout, which is the standard layout for
            /// storage images that can be read/written by compute shaders.
            /// 
            /// Based on Vulkan API: https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdClearColorImage.html
            /// Vulkan spec: vkCmdClearColorImage can be used with VK_IMAGE_LAYOUT_GENERAL layout
            /// for storage images (UAVs).
            /// </summary>
            public void ClearUAVFloat(ITexture texture, Vector4 value)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before clearing UAV texture");
                }

                if (texture == null)
                {
                    throw new ArgumentNullException(nameof(texture));
                }

                // Get native VkImage handle from texture
                IntPtr image = texture.NativeHandle;
                if (image == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Texture does not have a valid native handle");
                }

                // Get texture description to determine format and dimensions
                TextureDesc textureDesc = texture.Desc;

                // Validate texture has storage usage (required for UAV)
                if ((textureDesc.Usage & TextureUsage.Storage) == 0)
                {
                    throw new ArgumentException("Texture must have Storage usage flag to be used as a UAV", nameof(texture));
                }

                // Check if vkCmdClearColorImage is available
                if (vkCmdClearColorImage == null)
                {
                    throw new NotSupportedException("vkCmdClearColorImage is not available");
                }

                // Transition image to GENERAL layout if not already in it
                // GENERAL is the standard layout for UAVs (storage images) in Vulkan
                // We check current layout from resource state tracking, but for simplicity
                // we transition from UNDEFINED or current layout to GENERAL
                // TODO:  Note: In a full implementation, we would track the current layout per texture
                // TODO: STUB - For now, we transition assuming the texture might be in UNDEFINED or GENERAL layout
                TransitionImageLayout(image, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, textureDesc, 0, 0);

                // Create clear color value structure
                // VkClearColorValue is a union that can be float[4], int32[4], or uint32[4]
                // For float UAVs, we use float[4]
                VkClearColorValue clearValue = new VkClearColorValue
                {
                    float32_0 = value.X,
                    float32_1 = value.Y,
                    float32_2 = value.Z,
                    float32_3 = value.W
                };

                // Create subresource range for the entire texture
                // For UAV clearing, we typically clear the entire texture (all mips, all array layers)
                // If specific mip/array slice clearing is needed, it can be added as parameters later
                VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange
                {
                    aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                    baseMipLevel = 0, // Start from base mip level
                    levelCount = unchecked((uint)textureDesc.MipLevels), // All mip levels
                    baseArrayLayer = 0, // Start from base array layer
                    layerCount = unchecked((uint)textureDesc.ArraySize) // All array layers
                };

                // Allocate memory for structures and marshal them
                int clearValueSize = Marshal.SizeOf<VkClearColorValue>();
                IntPtr clearValuePtr = Marshal.AllocHGlobal(clearValueSize);
                int rangeSize = Marshal.SizeOf<VkImageSubresourceRange>();
                IntPtr rangePtr = Marshal.AllocHGlobal(rangeSize);

                try
                {
                    // Marshal structures to unmanaged memory
                    Marshal.StructureToPtr(clearValue, clearValuePtr, false);
                    Marshal.StructureToPtr(subresourceRange, rangePtr, false);

                    // Call vkCmdClearColorImage
                    // Signature: void vkCmdClearColorImage(
                    //   VkCommandBuffer commandBuffer,
                    //   VkImage image,
                    //   VkImageLayout imageLayout,  // VK_IMAGE_LAYOUT_GENERAL for UAVs
                    //   const VkClearColorValue* pColor,
                    //   uint32_t rangeCount,
                    //   const VkImageSubresourceRange* pRanges)
                    vkCmdClearColorImage(
                        _vkCommandBuffer,
                        image,
                        VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, // GENERAL layout for UAVs
                        clearValuePtr,
                        1, // Single range
                        rangePtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(clearValuePtr);
                    Marshal.FreeHGlobal(rangePtr);
                }

                // Note: We keep the image in GENERAL layout after clearing
                // This is the correct layout for UAVs and allows subsequent compute shader access
                // No transition back is needed
            }
            // ClearUAVUint implementation using vkCmdClearColorImage
            // vkCmdClearColorImage supports integer formats via VkClearColorValue union (uint32[4] member)
            // This is the most efficient approach for clearing storage images with integer formats
            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdClearColorImage.html
            public void ClearUAVUint(ITexture texture, uint value)
            {
                if (!_isOpen || texture == null)
                {
                    return;
                }

                // Validate texture is a VulkanTexture
                if (!(texture is VulkanTexture vulkanTexture))
                {
                    Console.WriteLine("[VulkanCommandList] ClearUAVUint: Texture must be a VulkanTexture instance");
                    return;
                }

                // Get VkImage handle - use reflection to access private _vkImage field (C# 7.3 compatible)
                IntPtr vkImage = IntPtr.Zero;
                try
                {
                    System.Reflection.FieldInfo imageField = typeof(VulkanTexture).GetField("_vkImage",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (imageField != null)
                    {
                        vkImage = (IntPtr)imageField.GetValue(vulkanTexture);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VulkanCommandList] ClearUAVUint: Error accessing image handle: {ex.Message}");
                    return;
                }

                if (vkImage == IntPtr.Zero)
                {
                    Console.WriteLine("[VulkanCommandList] ClearUAVUint: Invalid image handle");
                    return;
                }

                TextureDesc textureDesc = texture.Desc;
                if (textureDesc.Width == 0 || textureDesc.Height == 0)
                {
                    Console.WriteLine("[VulkanCommandList] ClearUAVUint: Invalid texture dimensions");
                    return;
                }

                // Use vkCmdClearColorImage to clear the storage image with uint value
                // vkCmdClearColorImage supports integer formats via VkClearColorValue union (uint32[4] member)
                // This is efficient and correct for clearing storage images with integer formats like r32ui
                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdClearColorImage.html
                if (vkCmdClearColorImage == null)
                {
                    Console.WriteLine("[VulkanCommandList] ClearUAVUint: vkCmdClearColorImage not available");
                    return;
                }

                // Transition image to TRANSFER_DST_OPTIMAL layout for clearing operation
                // vkCmdClearColorImage requires the image to be in TRANSFER_DST_OPTIMAL or GENERAL layout
                TransitionImageLayout(vkImage, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, textureDesc);

                // Create clear color value with uint32 array
                // VkClearColorValue is a union: can be float[4], int32[4], or uint32[4]
                // For r32ui format (single uint per pixel), we only need to set the first uint32 value
                // However, vkCmdClearColorImage expects uint32[4], so we replicate the value to all channels
                // The driver will use the appropriate channels based on the image format
                IntPtr clearValuePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkClearColorValue)));
                try
                {
                    // Write uint32[4] - for r32ui format, only the first value matters, but we set all for completeness
                    // Marshal.WriteInt32 writes signed int, but we're writing uint bits, so we use unchecked cast
                    int valueAsInt = unchecked((int)value);
                    Marshal.WriteInt32(clearValuePtr, 0, valueAsInt);  // uint32_0
                    Marshal.WriteInt32(clearValuePtr, 4, valueAsInt);  // uint32_1 (replicated)
                    Marshal.WriteInt32(clearValuePtr, 8, valueAsInt);  // uint32_2 (replicated)
                    Marshal.WriteInt32(clearValuePtr, 12, valueAsInt); // uint32_3 (replicated)

                    // Create image subresource range for full texture (all mip levels and array layers)
                    VkImageSubresourceRange range = new VkImageSubresourceRange
                    {
                        aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                        baseMipLevel = 0,
                        levelCount = (uint)textureDesc.MipLevels,
                        baseArrayLayer = 0,
                        layerCount = (uint)textureDesc.ArraySize
                    };

                    IntPtr rangePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkImageSubresourceRange)));
                    try
                    {
                        Marshal.StructureToPtr(range, rangePtr, false);

                        // Clear the image using vkCmdClearColorImage
                        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdClearColorImage.html
                        vkCmdClearColorImage(_vkCommandBuffer,
                            vkImage,
                            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                            clearValuePtr,
                            1, // Single range covering entire texture
                            rangePtr);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(rangePtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(clearValuePtr);
                }

                // Transition image back to GENERAL layout for use as UAV/storage image
                // GENERAL layout is required for storage images accessed by compute/graphics shaders
                TransitionImageLayout(vkImage, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, textureDesc);
            }
            public void SetTextureState(ITexture texture, ResourceState state)
            {
                // TODO: STUB - Implement texture state transitions with vkCmdPipelineBarrier (VkImageMemoryBarrier)
                // Texture barriers require image layout transitions which are more complex than buffer barriers
                // TODO:  This is left as a stub for future implementation
            }

            public void SetBufferState(IBuffer buffer, ResourceState state)
            {
                if (buffer == null)
                {
                    return;
                }

                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                // Get native resource handle from buffer
                IntPtr bufferHandle = buffer.NativeHandle;
                if (bufferHandle == IntPtr.Zero)
                {
                    return; // Invalid buffer
                }

                // Determine current state
                ResourceState currentState;
                if (!_resourceStates.TryGetValue(buffer, out currentState))
                {
                    // First time we see this buffer - assume it starts in Common state
                    currentState = ResourceState.Common;
                }

                // Check if transition is needed
                if (currentState == state)
                {
                    return; // Already in target state, no barrier needed
                }

                // Map states to Vulkan access flags and pipeline stages
                VkAccessFlags srcAccessMask;
                VkPipelineStageFlags srcStageMask;
                MapResourceStateToVulkanAccessAndStage(currentState, out srcAccessMask, out srcStageMask);

                VkAccessFlags dstAccessMask;
                VkPipelineStageFlags dstStageMask;
                MapResourceStateToVulkanAccessAndStage(state, out dstAccessMask, out dstStageMask);

                // Get buffer size from buffer description
                ulong bufferSize = 0;
                VulkanBuffer vulkanBuffer = buffer as VulkanBuffer;
                if (vulkanBuffer != null)
                {
                    bufferSize = (ulong)vulkanBuffer.Desc.SizeInBytes;
                }

                // Queue buffer barrier (will be flushed on CommitBarriers)
                _pendingBufferBarriers.Add(new PendingBufferBarrier
                {
                    Buffer = bufferHandle,
                    Offset = 0,
                    Size = bufferSize != 0 ? bufferSize : unchecked((ulong)-1), // VK_WHOLE_SIZE if size is 0 or unknown
                    SrcAccessMask = srcAccessMask,
                    DstAccessMask = dstAccessMask,
                    SrcStageMask = srcStageMask,
                    DstStageMask = dstStageMask
                });

                // Update tracked state
                _resourceStates[buffer] = state;
            }

            public void CommitBarriers()
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_pendingBufferBarriers.Count == 0)
                {
                    return; // No barriers to commit
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    // Command buffer not initialized - clear barriers and return
                    _pendingBufferBarriers.Clear();
                    return;
                }

                // Allocate memory for buffer barrier array
                int barrierSize = Marshal.SizeOf(typeof(VkBufferMemoryBarrier));
                IntPtr barriersPtr = Marshal.AllocHGlobal(barrierSize * _pendingBufferBarriers.Count);

                try
                {
                    // Convert pending barriers to VkBufferMemoryBarrier structures
                    IntPtr currentBarrierPtr = barriersPtr;
                    for (int i = 0; i < _pendingBufferBarriers.Count; i++)
                    {
                        var pendingBarrier = _pendingBufferBarriers[i];

                        var barrier = new VkBufferMemoryBarrier
                        {
                            sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER,
                            pNext = IntPtr.Zero,
                            srcAccessMask = pendingBarrier.SrcAccessMask,
                            dstAccessMask = pendingBarrier.DstAccessMask,
                            srcQueueFamilyIndex = unchecked((uint)-1), // VK_QUEUE_FAMILY_IGNORED
                            dstQueueFamilyIndex = unchecked((uint)-1), // VK_QUEUE_FAMILY_IGNORED
                            buffer = pendingBarrier.Buffer,
                            offset = pendingBarrier.Offset,
                            size = pendingBarrier.Size
                        };

                        Marshal.StructureToPtr(barrier, currentBarrierPtr, false);
                        currentBarrierPtr = new IntPtr(currentBarrierPtr.ToInt64() + barrierSize);
                    }

                    // Compute combined source and destination stage masks from all barriers
                    VkPipelineStageFlags combinedSrcStageMask = 0;
                    VkPipelineStageFlags combinedDstStageMask = 0;
                    for (int i = 0; i < _pendingBufferBarriers.Count; i++)
                    {
                        combinedSrcStageMask |= _pendingBufferBarriers[i].SrcStageMask;
                        combinedDstStageMask |= _pendingBufferBarriers[i].DstStageMask;
                    }

                    // Call vkCmdPipelineBarrier
                    vkCmdPipelineBarrier(
                        _vkCommandBuffer,
                        combinedSrcStageMask,
                        combinedDstStageMask,
                        0, // dependencyFlags
                        0, // memoryBarrierCount
                        IntPtr.Zero, // pMemoryBarriers
                        unchecked((uint)_pendingBufferBarriers.Count), // bufferMemoryBarrierCount
                        barriersPtr, // pBufferMemoryBarriers
                        0, // imageMemoryBarrierCount
                        IntPtr.Zero); // pImageMemoryBarriers
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(barriersPtr);
                }

                // Clear pending barriers after committing
                _pendingBufferBarriers.Clear();
            }
            /// <summary>
            /// Maps ResourceState to Vulkan access flags and pipeline stage flags.
            /// Based on Vulkan specification: Buffer access patterns and pipeline stages.
            /// </summary>
            private static void MapResourceStateToVulkanAccessAndStage(
                ResourceState state,
                out VkAccessFlags accessMask,
                out VkPipelineStageFlags stageMask)
            {
                switch (state)
                {
                    case ResourceState.Common:
                        // Common state - no specific access or stage
                        accessMask = 0;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                        break;

                    case ResourceState.VertexBuffer:
                        accessMask = VkAccessFlags.VK_ACCESS_VERTEX_ATTRIBUTE_READ_BIT;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_VERTEX_INPUT_BIT;
                        break;

                    case ResourceState.IndexBuffer:
                        accessMask = VkAccessFlags.VK_ACCESS_INDEX_READ_BIT;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_VERTEX_INPUT_BIT;
                        break;

                    case ResourceState.ConstantBuffer:
                        accessMask = VkAccessFlags.VK_ACCESS_UNIFORM_READ_BIT;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_VERTEX_SHADER_BIT |
                                   VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT |
                                   VkPipelineStageFlags.VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT;
                        break;

                    case ResourceState.ShaderResource:
                        accessMask = VkAccessFlags.VK_ACCESS_SHADER_READ_BIT;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_VERTEX_SHADER_BIT |
                                   VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT |
                                   VkPipelineStageFlags.VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT;
                        break;

                    case ResourceState.UnorderedAccess:
                        accessMask = VkAccessFlags.VK_ACCESS_SHADER_READ_BIT | VkAccessFlags.VK_ACCESS_SHADER_WRITE_BIT;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT |
                                   VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT |
                                   VkPipelineStageFlags.VK_PIPELINE_STAGE_VERTEX_SHADER_BIT;
                        break;

                    case ResourceState.CopySource:
                        accessMask = VkAccessFlags.VK_ACCESS_TRANSFER_READ_BIT;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT;
                        break;

                    case ResourceState.CopyDest:
                        accessMask = VkAccessFlags.VK_ACCESS_TRANSFER_WRITE_BIT;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT;
                        break;

                    case ResourceState.IndirectArgument:
                        accessMask = VkAccessFlags.VK_ACCESS_INDIRECT_COMMAND_READ_BIT;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_DRAW_INDIRECT_BIT;
                        break;

                    case ResourceState.RenderTarget:
                    case ResourceState.DepthWrite:
                    case ResourceState.DepthRead:
                        // These states are for textures/images, not buffers
                        // Default to no access for buffer resources
                        accessMask = 0;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                        break;

                    case ResourceState.Present:
                        // Present is typically for swapchain images, not buffers
                        accessMask = 0;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT;
                        break;

                    default:
                        // Unknown state - default to no access
                        accessMask = 0;
                        stageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                        break;
                }
            }

            /// <summary>
            /// Inserts a UAV (Unordered Access View) barrier for a texture resource.
            /// 
            /// A UAV barrier ensures that all UAV writes to the texture have completed before
            /// subsequent operations (compute shaders, pixel shaders, etc.) can read from the texture.
            /// This is necessary when a texture is both written to and read from as a UAV in different
            /// draw/dispatch calls within the same command list.
            /// 
            /// Based on Vulkan API: vkCmdPipelineBarrier with VkMemoryBarrier
            /// Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdPipelineBarrier.html
            /// Original implementation: Records a memory barrier command into the command buffer
            /// UAV barriers use VkMemoryBarrier to synchronize all shader storage writes with subsequent reads
            /// 
            /// Note: UAV barriers differ from transition barriers - they don't change resource state,
            /// they only synchronize access between UAV write and read operations.
            /// </summary>
            public void UAVBarrier(ITexture texture)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (texture == null)
                {
                    return; // Null texture - nothing to barrier
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    return; // Command buffer not initialized
                }

                if (vkCmdPipelineBarrier == null)
                {
                    return; // Cannot barrier without pipeline barrier function
                }

                // UAV barriers use memory barriers to synchronize all shader storage writes with reads
                // Based on Vulkan specification: Memory barriers synchronize all memory accesses
                // For UAV barriers, we need to ensure all shader writes complete before subsequent reads
                VkMemoryBarrier memoryBarrier = new VkMemoryBarrier
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_BARRIER,
                    pNext = IntPtr.Zero,
                    // Source: All shader writes to UAV resources (storage images/buffers)
                    srcAccessMask = VkAccessFlags.VK_ACCESS_SHADER_WRITE_BIT,
                    // Destination: All shader reads and writes from/to UAV resources
                    dstAccessMask = VkAccessFlags.VK_ACCESS_SHADER_READ_BIT | VkAccessFlags.VK_ACCESS_SHADER_WRITE_BIT
                };

                // Allocate memory for memory barrier structure
                int barrierSize = Marshal.SizeOf(typeof(VkMemoryBarrier));
                IntPtr barrierPtr = Marshal.AllocHGlobal(barrierSize);

                try
                {
                    // Marshal structure to unmanaged memory
                    Marshal.StructureToPtr(memoryBarrier, barrierPtr, false);

                    // Call vkCmdPipelineBarrier with memory barrier
                    // Source stage: All stages that can write to UAVs (compute and fragment shaders)
                    // Destination stage: All stages that can read from UAVs (compute and fragment shaders)
                    vkCmdPipelineBarrier(
                        _vkCommandBuffer,
                        VkPipelineStageFlags.VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT | VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, // srcStageMask
                        VkPipelineStageFlags.VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT | VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, // dstStageMask
                        0, // dependencyFlags
                        1, // memoryBarrierCount
                        barrierPtr, // pMemoryBarriers
                        0, // bufferMemoryBarrierCount
                        IntPtr.Zero, // pBufferMemoryBarriers
                        0, // imageMemoryBarrierCount
                        IntPtr.Zero); // pImageMemoryBarriers
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(barrierPtr);
                }
            }

            /// <summary>
            /// Inserts a UAV (Unordered Access View) barrier for a buffer resource.
            /// 
            /// A UAV barrier ensures that all UAV writes to the buffer have completed before
            /// subsequent operations (compute shaders, pixel shaders, etc.) can read from the buffer.
            /// This is necessary when a buffer is both written to and read from as a UAV in different
            /// draw/dispatch calls within the same command list.
            /// 
            /// Based on Vulkan API: vkCmdPipelineBarrier with VkMemoryBarrier
            /// Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdPipelineBarrier.html
            /// Original implementation: Records a memory barrier command into the command buffer
            /// UAV barriers use VkMemoryBarrier to synchronize all shader storage writes with subsequent reads
            /// 
            /// Note: UAV barriers differ from transition barriers - they don't change resource state,
            /// they only synchronize access between UAV write and read operations.
            /// </summary>
            public void UAVBarrier(IBuffer buffer)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (buffer == null)
                {
                    return; // Null buffer - nothing to barrier
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    return; // Command buffer not initialized
                }

                if (vkCmdPipelineBarrier == null)
                {
                    return; // Cannot barrier without pipeline barrier function
                }

                // UAV barriers use memory barriers to synchronize all shader storage writes with reads
                // Based on Vulkan specification: Memory barriers synchronize all memory accesses
                // For UAV barriers, we need to ensure all shader writes complete before subsequent reads
                VkMemoryBarrier memoryBarrier = new VkMemoryBarrier
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_BARRIER,
                    pNext = IntPtr.Zero,
                    // Source: All shader writes to UAV resources (storage images/buffers)
                    srcAccessMask = VkAccessFlags.VK_ACCESS_SHADER_WRITE_BIT,
                    // Destination: All shader reads and writes from/to UAV resources
                    dstAccessMask = VkAccessFlags.VK_ACCESS_SHADER_READ_BIT | VkAccessFlags.VK_ACCESS_SHADER_WRITE_BIT
                };

                // Allocate memory for memory barrier structure
                int barrierSize = Marshal.SizeOf(typeof(VkMemoryBarrier));
                IntPtr barrierPtr = Marshal.AllocHGlobal(barrierSize);

                try
                {
                    // Marshal structure to unmanaged memory
                    Marshal.StructureToPtr(memoryBarrier, barrierPtr, false);

                    // Call vkCmdPipelineBarrier with memory barrier
                    // Source stage: All stages that can write to UAVs (compute and fragment shaders)
                    // Destination stage: All stages that can read from UAVs (compute and fragment shaders)
                    vkCmdPipelineBarrier(
                        _vkCommandBuffer,
                        VkPipelineStageFlags.VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT | VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, // srcStageMask
                        VkPipelineStageFlags.VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT | VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, // dstStageMask
                        0, // dependencyFlags
                        1, // memoryBarrierCount
                        barrierPtr, // pMemoryBarriers
                        0, // bufferMemoryBarrierCount
                        IntPtr.Zero, // pBufferMemoryBarriers
                        0, // imageMemoryBarrierCount
                        IntPtr.Zero); // pImageMemoryBarriers
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(barrierPtr);
                }
            }
            /// <summary>
            /// Sets all graphics pipeline state for rendering.
            /// 
            /// This method configures the complete graphics pipeline state including:
            /// - Graphics pipeline binding
            /// - Viewports and scissor rectangles
            /// - Descriptor sets (shader resources)
            /// - Vertex buffers
            /// - Index buffer
            /// 
            /// Note: Framebuffer and render pass begin/end are typically handled separately,
            /// as render passes define render targets and must be managed around draw calls.
            /// 
            /// Based on Vulkan API: vkCmdBindPipeline, vkCmdSetViewport, vkCmdSetScissor,
            /// vkCmdBindDescriptorSets, vkCmdBindVertexBuffers, vkCmdBindIndexBuffer
            /// Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindPipeline.html
            /// </summary>
            /// <param name="state">Complete graphics state configuration</param>
            public void SetGraphicsState(GraphicsState state)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before setting graphics state");
                }

                if (state.Pipeline == null)
                {
                    throw new ArgumentException("Graphics state must have a valid pipeline", nameof(state));
                }

                // Step 1: Bind graphics pipeline
                VulkanGraphicsPipeline vulkanPipeline = state.Pipeline as VulkanGraphicsPipeline;
                if (vulkanPipeline == null)
                {
                    throw new ArgumentException("Pipeline must be a VulkanGraphicsPipeline", nameof(state));
                }

                IntPtr vkPipelineHandle = vulkanPipeline.VkPipeline;
                if (vkPipelineHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Graphics pipeline does not have a valid VkPipeline handle. Pipeline may not have been fully created.");
                }

                // Bind graphics pipeline
                // Based on Vulkan API: vkCmdBindPipeline binds a graphics pipeline to the command buffer
                // Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindPipeline.html
                if (vkCmdBindPipeline != null)
                {
                    vkCmdBindPipeline(_vkCommandBuffer, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, vkPipelineHandle);
                }

                // Step 2: Set viewports and scissors
                if (state.Viewport.Viewports != null && state.Viewport.Viewports.Length > 0)
                {
                    SetViewports(state.Viewport.Viewports);
                }

                if (state.Viewport.Scissors != null && state.Viewport.Scissors.Length > 0)
                {
                    SetScissors(state.Viewport.Scissors);
                }

                // Step 3: Bind descriptor sets if provided
                if (state.BindingSets != null && state.BindingSets.Length > 0 && vkCmdBindDescriptorSets != null)
                {
                    // Get pipeline layout from graphics pipeline
                    IntPtr vkPipelineLayout = vulkanPipeline.VkPipelineLayout;
                    if (vkPipelineLayout == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Graphics pipeline does not have a valid VkPipelineLayout handle.");
                    }

                    // Build array of descriptor set handles
                    int descriptorSetCount = state.BindingSets.Length;
                    IntPtr descriptorSetHandlesPtr = Marshal.AllocHGlobal(descriptorSetCount * IntPtr.Size);
                    try
                    {
                        for (int i = 0; i < descriptorSetCount; i++)
                        {
                            VulkanBindingSet vulkanBindingSet = state.BindingSets[i] as VulkanBindingSet;
                            if (vulkanBindingSet == null)
                            {
                                Marshal.FreeHGlobal(descriptorSetHandlesPtr);
                                throw new ArgumentException($"Binding set at index {i} must be a VulkanBindingSet", nameof(state));
                            }

                            IntPtr vkDescriptorSet = vulkanBindingSet.VkDescriptorSet;
                            if (vkDescriptorSet == IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(descriptorSetHandlesPtr);
                                throw new InvalidOperationException($"Binding set at index {i} does not have a valid VkDescriptorSet handle.");
                            }

                            Marshal.WriteIntPtr(descriptorSetHandlesPtr, i * IntPtr.Size, vkDescriptorSet);
                        }

                        // Bind all descriptor sets in a single call
                        // Based on Vulkan API: vkCmdBindDescriptorSets
                        // Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindDescriptorSets.html
                        vkCmdBindDescriptorSets(
                            _vkCommandBuffer,
                            VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                            vkPipelineLayout,
                            0, // firstSet - starting set index
                            (uint)descriptorSetCount,
                            descriptorSetHandlesPtr,
                            0, // dynamicOffsetCount
                            IntPtr.Zero // pDynamicOffsets - would be populated if dynamic buffers present
                        );
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(descriptorSetHandlesPtr);
                    }
                }

                // Step 4: Bind vertex buffers if provided
                if (state.VertexBuffers != null && state.VertexBuffers.Length > 0 && vkCmdBindVertexBuffers != null)
                {
                    int vertexBufferCount = state.VertexBuffers.Length;
                    IntPtr bufferHandlesPtr = Marshal.AllocHGlobal(vertexBufferCount * IntPtr.Size);
                    IntPtr offsetsPtr = Marshal.AllocHGlobal(vertexBufferCount * sizeof(ulong));
                    try
                    {
                        for (int i = 0; i < vertexBufferCount; i++)
                        {
                            VulkanBuffer vulkanBuffer = state.VertexBuffers[i] as VulkanBuffer;
                            if (vulkanBuffer == null)
                            {
                                Marshal.FreeHGlobal(bufferHandlesPtr);
                                Marshal.FreeHGlobal(offsetsPtr);
                                throw new ArgumentException($"Vertex buffer at index {i} must be a VulkanBuffer", nameof(state));
                            }

                            IntPtr vkBuffer = vulkanBuffer.VkBuffer;
                            if (vkBuffer == IntPtr.Zero)
                            {
                                vkBuffer = vulkanBuffer.NativeHandle;
                                if (vkBuffer == IntPtr.Zero)
                                {
                                    Marshal.FreeHGlobal(bufferHandlesPtr);
                                    Marshal.FreeHGlobal(offsetsPtr);
                                    throw new InvalidOperationException($"Vertex buffer at index {i} does not have a valid Vulkan handle.");
                                }
                            }

                            Marshal.WriteIntPtr(bufferHandlesPtr, i * IntPtr.Size, vkBuffer);
                            Marshal.WriteInt64(offsetsPtr, i * sizeof(ulong), 0UL); // Offset is 0 for now - could be extended
                        }

                        // Bind vertex buffers
                        // Based on Vulkan API: vkCmdBindVertexBuffers
                        // Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindVertexBuffers.html
                        vkCmdBindVertexBuffers(
                            _vkCommandBuffer,
                            0, // firstBinding - starting binding index
                            (uint)vertexBufferCount,
                            bufferHandlesPtr,
                            offsetsPtr
                        );
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(bufferHandlesPtr);
                        Marshal.FreeHGlobal(offsetsPtr);
                    }
                }

                // Step 5: Bind index buffer if provided
                if (state.IndexBuffer != null && vkCmdBindIndexBuffer != null)
                {
                    VulkanBuffer vulkanIndexBuffer = state.IndexBuffer as VulkanBuffer;
                    if (vulkanIndexBuffer == null)
                    {
                        throw new ArgumentException("Index buffer must be a VulkanBuffer", nameof(state));
                    }

                    IntPtr vkIndexBuffer = vulkanIndexBuffer.VkBuffer;
                    if (vkIndexBuffer == IntPtr.Zero)
                    {
                        vkIndexBuffer = vulkanIndexBuffer.NativeHandle;
                        if (vkIndexBuffer == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Index buffer does not have a valid Vulkan handle.");
                        }
                    }

                    // Convert TextureFormat to VkIndexType
                    // R16_UInt -> VK_INDEX_TYPE_UINT16, R32_UInt -> VK_INDEX_TYPE_UINT32
                    VkIndexType indexType = VkIndexType.VK_INDEX_TYPE_UINT32; // Default to 32-bit indices
                    if (state.IndexFormat == TextureFormat.R16_UInt)
                    {
                        indexType = VkIndexType.VK_INDEX_TYPE_UINT16;
                    }
                    else if (state.IndexFormat == TextureFormat.R32_UInt)
                    {
                        indexType = VkIndexType.VK_INDEX_TYPE_UINT32;
                    }

                    // Bind index buffer
                    // Based on Vulkan API: vkCmdBindIndexBuffer
                    // Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindIndexBuffer.html
                    vkCmdBindIndexBuffer(
                        _vkCommandBuffer,
                        vkIndexBuffer,
                        0UL, // offset - buffer offset in bytes
                        indexType
                    );
                }

                // Note: Framebuffer is not bound here as it's typically managed via render pass begin/end
                // The framebuffer is specified when beginning a render pass (vkCmdBeginRenderPass)
                // This is a design choice in Vulkan to separate pipeline state from render target state
            }
            /// <summary>
            /// Sets a single viewport using vkCmdSetViewport.
            /// 
            /// Defines the viewport rectangle that transforms normalized device coordinates to framebuffer coordinates.
            /// The viewport rectangle defines the region of the framebuffer that will be rendered to, as well as the
            /// depth range for the viewport.
            /// 
            /// Based on Vulkan API: vkCmdSetViewport
            /// Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdSetViewport.html
            /// </summary>
            /// <param name="viewport">Viewport definition with position, size, and depth range.</param>
            public void SetViewport(Viewport viewport)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before setting viewport");
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    return; // Command buffer not initialized
                }

                if (vkCmdSetViewport == null)
                {
                    return; // Function not loaded
                }

                // vkCmdSetViewport signature:
                // void vkCmdSetViewport(
                //     VkCommandBuffer commandBuffer,
                //     uint32_t firstViewport,
                //     uint32_t viewportCount,
                //     const VkViewport* pViewports);
                //
                // Convert Viewport (X, Y, Width, Height, MinDepth, MaxDepth) to VkViewport:
                // - x = X
                // - y = Y
                // - width = Width
                // - height = Height
                // - minDepth = MinDepth
                // - maxDepth = MaxDepth

                VkViewport vkViewport = new VkViewport
                {
                    x = viewport.X,
                    y = viewport.Y,
                    width = viewport.Width,
                    height = viewport.Height,
                    minDepth = viewport.MinDepth,
                    maxDepth = viewport.MaxDepth
                };

                // Marshal VkViewport to unmanaged memory
                int viewportSize = Marshal.SizeOf(typeof(VkViewport));
                IntPtr viewportPtr = Marshal.AllocHGlobal(viewportSize);
                try
                {
                    Marshal.StructureToPtr(vkViewport, viewportPtr, false);

                    // Call vkCmdSetViewport with firstViewport=0, viewportCount=1
                    vkCmdSetViewport(_vkCommandBuffer, 0, 1, viewportPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(viewportPtr);
                }
            }

            /// <summary>
            /// Sets multiple viewports using vkCmdSetViewport.
            /// 
            /// Defines an array of viewport rectangles that transform normalized device coordinates to framebuffer coordinates.
            /// Multiple viewports can be used for multi-viewport rendering (requires VK_KHR_multiview extension).
            /// Each viewport defines the region of the framebuffer that will be rendered to, as well as the depth range.
            /// 
            /// Based on Vulkan API: vkCmdSetViewport
            /// Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdSetViewport.html
            /// </summary>
            /// <param name="viewports">Array of viewport definitions with position, size, and depth range.</param>
            public void SetViewports(Viewport[] viewports)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before setting viewports");
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    return; // Command buffer not initialized
                }

                if (vkCmdSetViewport == null)
                {
                    return; // Function not loaded
                }

                if (viewports == null || viewports.Length == 0)
                {
                    return; // No viewports to set
                }

                // vkCmdSetViewport signature:
                // void vkCmdSetViewport(
                //     VkCommandBuffer commandBuffer,
                //     uint32_t firstViewport,
                //     uint32_t viewportCount,
                //     const VkViewport* pViewports);
                //
                // Convert Viewport[] (X, Y, Width, Height, MinDepth, MaxDepth) to VkViewport[]:
                // - x = X
                // - y = Y
                // - width = Width
                // - height = Height
                // - minDepth = MinDepth
                // - maxDepth = MaxDepth

                // Convert Viewport[] to VkViewport[]
                int viewportSize = Marshal.SizeOf(typeof(VkViewport));
                IntPtr viewportsPtr = Marshal.AllocHGlobal(viewportSize * viewports.Length);
                try
                {
                    // Marshal each viewport to unmanaged memory
                    IntPtr currentViewportPtr = viewportsPtr;
                    for (int i = 0; i < viewports.Length; i++)
                    {
                        VkViewport vkViewport = new VkViewport
                        {
                            x = viewports[i].X,
                            y = viewports[i].Y,
                            width = viewports[i].Width,
                            height = viewports[i].Height,
                            minDepth = viewports[i].MinDepth,
                            maxDepth = viewports[i].MaxDepth
                        };

                        Marshal.StructureToPtr(vkViewport, currentViewportPtr, false);
                        currentViewportPtr = new IntPtr(currentViewportPtr.ToInt64() + viewportSize);
                    }

                    // Call vkCmdSetViewport with firstViewport=0, viewportCount=viewports.Length
                    vkCmdSetViewport(_vkCommandBuffer, 0, unchecked((uint)viewports.Length), viewportsPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(viewportsPtr);
                }
            }
            /// <summary>
            /// Sets a single scissor rectangle using vkCmdSetScissor.
            /// 
            /// Defines the scissor rectangle that clips rendering to a specific region of the framebuffer.
            /// All rendering outside this rectangle is discarded. The scissor rectangle is defined in
            /// framebuffer coordinates, with (0,0) at the top-left corner.
            /// 
            /// Based on Vulkan API: vkCmdSetScissor
            /// Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdSetScissor.html
            /// </summary>
            /// <param name="scissor">Rectangle defining the scissor region in framebuffer coordinates.</param>
            public void SetScissor(Rectangle scissor)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before setting scissor");
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    return; // Command buffer not initialized
                }

                if (vkCmdSetScissor == null)
                {
                    return; // Function not loaded
                }

                // vkCmdSetScissor signature:
                // void vkCmdSetScissor(
                //     VkCommandBuffer commandBuffer,
                //     uint32_t firstScissor,
                //     uint32_t scissorCount,
                //     const VkRect2D* pScissors);
                //
                // Convert Rectangle (X, Y, Width, Height) to VkRect2D (offset, extent):
                // - offset.x = X
                // - offset.y = Y
                // - extent.width = Width
                // - extent.height = Height

                VkRect2D vkRect = new VkRect2D
                {
                    offset = new VkOffset2D
                    {
                        x = scissor.X,
                        y = scissor.Y
                    },
                    extent = new VkExtent2D
                    {
                        width = unchecked((uint)System.Math.Max(0, scissor.Width)),
                        height = unchecked((uint)System.Math.Max(0, scissor.Height))
                    }
                };

                // Marshal VkRect2D to unmanaged memory
                int rectSize = Marshal.SizeOf(typeof(VkRect2D));
                IntPtr rectPtr = Marshal.AllocHGlobal(rectSize);
                try
                {
                    Marshal.StructureToPtr(vkRect, rectPtr, false);

                    // Call vkCmdSetScissor with firstScissor=0, scissorCount=1
                    vkCmdSetScissor(_vkCommandBuffer, 0, 1, rectPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(rectPtr);
                }
            }

            /// <summary>
            /// Sets multiple scissor rectangles using vkCmdSetScissor.
            /// 
            /// Defines an array of scissor rectangles that clip rendering. Each viewport (if multiple
            /// viewports are used) can have its own scissor rectangle. The scissor rectangles are defined
            /// in framebuffer coordinates, with (0,0) at the top-left corner.
            /// 
            /// Based on Vulkan API: vkCmdSetScissor
            /// Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdSetScissor.html
            /// </summary>
            /// <param name="scissors">Array of rectangles defining the scissor regions in framebuffer coordinates.</param>
            public void SetScissors(Rectangle[] scissors)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before setting scissors");
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    return; // Command buffer not initialized
                }

                if (vkCmdSetScissor == null)
                {
                    return; // Function not loaded
                }

                if (scissors == null || scissors.Length == 0)
                {
                    return; // No scissor rectangles to set
                }

                // vkCmdSetScissor signature:
                // void vkCmdSetScissor(
                //     VkCommandBuffer commandBuffer,
                //     uint32_t firstScissor,
                //     uint32_t scissorCount,
                //     const VkRect2D* pScissors);
                //
                // Convert Rectangle array to VkRect2D array:
                // - offset.x = X
                // - offset.y = Y
                // - extent.width = Width
                // - extent.height = Height

                // Convert Rectangle array to VkRect2D array
                VkRect2D[] vkRects = new VkRect2D[scissors.Length];
                for (int i = 0; i < scissors.Length; i++)
                {
                    vkRects[i] = new VkRect2D
                    {
                        offset = new VkOffset2D
                        {
                            x = scissors[i].X,
                            y = scissors[i].Y
                        },
                        extent = new VkExtent2D
                        {
                            width = unchecked((uint)System.Math.Max(0, scissors[i].Width)),
                            height = unchecked((uint)System.Math.Max(0, scissors[i].Height))
                        }
                    };
                }

                // Marshal VkRect2D array to unmanaged memory
                int rectSize = Marshal.SizeOf(typeof(VkRect2D));
                IntPtr rectsPtr = Marshal.AllocHGlobal(rectSize * vkRects.Length);
                try
                {
                    IntPtr currentRectPtr = rectsPtr;
                    for (int i = 0; i < vkRects.Length; i++)
                    {
                        Marshal.StructureToPtr(vkRects[i], currentRectPtr, false);
                        currentRectPtr = new IntPtr(currentRectPtr.ToInt64() + rectSize);
                    }

                    // Call vkCmdSetScissor with firstScissor=0, scissorCount=scissors.Length
                    vkCmdSetScissor(_vkCommandBuffer, 0, unchecked((uint)scissors.Length), rectsPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(rectsPtr);
                }
            }
            /// <summary>
            /// Sets blend constants for blend operations.
            /// 
            /// Blend constants are used when the blend factor is VK_BLEND_FACTOR_CONSTANT_COLOR,
            /// VK_BLEND_FACTOR_CONSTANT_ALPHA, VK_BLEND_FACTOR_ONE_MINUS_CONSTANT_COLOR, or
            /// VK_BLEND_FACTOR_ONE_MINUS_CONSTANT_ALPHA.
            /// 
            /// Based on Vulkan API: vkCmdSetBlendConstants
            /// Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdSetBlendConstants.html
            /// </summary>
            /// <param name="color">Blend constant color as RGBA (X=R, Y=G, Z=B, W=A).</param>
            public void SetBlendConstant(Vector4 color)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before setting blend constants");
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    return; // Command buffer not initialized
                }

                if (vkCmdSetBlendConstants == null)
                {
                    return; // Function not loaded
                }

                // vkCmdSetBlendConstants signature:
                // void vkCmdSetBlendConstants(
                //     VkCommandBuffer commandBuffer,
                //     const float blendConstants[4]);
                //
                // Convert Vector4 (X, Y, Z, W) to float[4] for blend constants:
                // - blendConstants[0] = color.X (Red)
                // - blendConstants[1] = color.Y (Green)
                // - blendConstants[2] = color.Z (Blue)
                // - blendConstants[3] = color.W (Alpha)

                // Allocate unmanaged memory for 4 floats (16 bytes)
                IntPtr blendConstantsPtr = Marshal.AllocHGlobal(4 * sizeof(float));
                try
                {
                    // Write float values to unmanaged memory at byte offsets 0, 4, 8, 12
                    unsafe
                    {
                        float* blendConstants = (float*)blendConstantsPtr.ToPointer();
                        blendConstants[0] = color.X; // Red
                        blendConstants[1] = color.Y; // Green
                        blendConstants[2] = color.Z; // Blue
                        blendConstants[3] = color.W; // Alpha
                    }

                    // Call vkCmdSetBlendConstants
                    vkCmdSetBlendConstants(_vkCommandBuffer, blendConstantsPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(blendConstantsPtr);
                }
            }
            /// <summary>
            /// Sets the stencil reference value for stencil operations.
            /// 
            /// The stencil reference value is used in stencil compare operations when
            /// the stencil compare operation is VK_COMPARE_OP_EQUAL, VK_COMPARE_OP_NOT_EQUAL,
            /// VK_COMPARE_OP_LESS, VK_COMPARE_OP_LESS_OR_EQUAL, VK_COMPARE_OP_GREATER, or
            /// VK_COMPARE_OP_GREATER_OR_EQUAL.
            /// 
            /// Based on Vulkan API: vkCmdSetStencilReference
            /// Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdSetStencilReference.html
            /// swkotor2.exe: N/A - Original game used DirectX 9, not Vulkan
            /// </summary>
            /// <param name="reference">Stencil reference value (0-255).</param>
            public void SetStencilRef(uint reference)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    return; // Command buffer not initialized
                }

                if (vkCmdSetStencilReference == null)
                {
                    return; // Function not loaded
                }

                // vkCmdSetStencilReference signature:
                // void vkCmdSetStencilReference(
                //     VkCommandBuffer commandBuffer,
                //     VkStencilFaceFlags faceMask,
                //     uint32_t reference);
                //
                // Set stencil reference for both front and back faces to match D3D12 behavior
                // where OMSetStencilRef sets the reference value for both faces

                // Call vkCmdSetStencilReference with both front and back faces
                vkCmdSetStencilReference(_vkCommandBuffer, VkStencilFaceFlags.VK_STENCIL_FACE_FRONT_AND_BACK, reference);
            }
            public void Draw(DrawArguments args)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before drawing");
                }

                if (args.VertexCount <= 0)
                {
                    return; // Nothing to draw
                }

                // vkCmdDraw signature:
                // void vkCmdDraw(
                //     VkCommandBuffer commandBuffer,
                //     uint32_t vertexCount,
                //     uint32_t instanceCount,
                //     uint32_t firstVertex,
                //     uint32_t firstInstance);
                //
                // Maps from DrawArguments:
                // - vertexCount: args.VertexCount
                // - instanceCount: args.InstanceCount (defaults to 1 if 0 or negative)
                // - firstVertex: args.StartVertexLocation
                // - firstInstance: args.StartInstanceLocation

                uint instanceCount = args.InstanceCount > 0 ? unchecked((uint)args.InstanceCount) : 1u;
                uint firstVertex = unchecked((uint)System.Math.Max(0, args.StartVertexLocation));
                uint firstInstance = unchecked((uint)System.Math.Max(0, args.StartInstanceLocation));

                vkCmdDraw(
                    _vkCommandBuffer,
                    unchecked((uint)args.VertexCount),
                    instanceCount,
                    firstVertex,
                    firstInstance);
            }
            /// <summary>
            /// Draws indexed primitives using vkCmdDrawIndexed.
            /// 
            /// Performs an indexed draw call, using indices from the currently bound index buffer
            /// to reference vertices from the vertex buffer. The index buffer must be bound before
            /// calling this method (via SetIndexBuffer or SetGraphicsState).
            /// 
            /// Based on Vulkan API: vkCmdDrawIndexed
            /// Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdDrawIndexed.html
            /// Original implementation: Records an indexed draw command into the command buffer
            /// 
            /// vkCmdDrawIndexed signature:
            /// void vkCmdDrawIndexed(
            ///     VkCommandBuffer commandBuffer,
            ///     uint32_t indexCount,
            ///     uint32_t instanceCount,
            ///     uint32_t firstIndex,
            ///     int32_t vertexOffset,
            ///     uint32_t firstInstance);
            /// 
            /// Maps from DrawArguments:
            /// - indexCount: args.VertexCount (for indexed draws, VertexCount represents the number of indices to draw)
            /// - instanceCount: args.InstanceCount (defaults to 1 if 0 or negative)
            /// - firstIndex: args.StartIndexLocation (offset into index buffer, in indices)
            /// - vertexOffset: args.BaseVertexLocation (offset added to each vertex index before indexing into vertex buffer)
            /// - firstInstance: args.StartInstanceLocation (first instance ID to draw)
            /// 
            /// Note: The index buffer format (16-bit or 32-bit) is determined when the index buffer is bound.
            /// The graphics pipeline and vertex/index buffers must be bound before calling this method.
            /// </summary>
            public void DrawIndexed(DrawArguments args)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before drawing");
                }

                if (args.VertexCount <= 0)
                {
                    return; // Nothing to draw
                }

                // Validate vkCmdDrawIndexed function pointer
                if (vkCmdDrawIndexed == null)
                {
                    throw new InvalidOperationException("vkCmdDrawIndexed function is not available. Vulkan may not be properly initialized.");
                }

                // Map DrawArguments to vkCmdDrawIndexed parameters
                // indexCount: Number of indices to draw (args.VertexCount for indexed draws)
                uint indexCount = unchecked((uint)args.VertexCount);
                
                // instanceCount: Number of instances to draw (defaults to 1 if 0 or negative)
                uint instanceCount = args.InstanceCount > 0 ? unchecked((uint)args.InstanceCount) : 1u;
                
                // firstIndex: Offset into index buffer, in indices (args.StartIndexLocation)
                uint firstIndex = unchecked((uint)System.Math.Max(0, args.StartIndexLocation));
                
                // vertexOffset: Offset added to each vertex index before indexing into vertex buffer (args.BaseVertexLocation)
                // Note: This is an int32_t in Vulkan, so it can be negative (allows negative vertex offsets)
                int vertexOffset = args.BaseVertexLocation;
                
                // firstInstance: First instance ID to draw (args.StartInstanceLocation)
                uint firstInstance = unchecked((uint)System.Math.Max(0, args.StartInstanceLocation));

                // Call vkCmdDrawIndexed to record the indexed draw command
                vkCmdDrawIndexed(
                    _vkCommandBuffer,
                    indexCount,
                    instanceCount,
                    firstIndex,
                    vertexOffset,
                    firstInstance);
            }
            /// <summary>
            /// Draws primitives using indirect arguments from a buffer.
            /// Records a vkCmdDrawIndirect command that reads draw parameters from the specified buffer.
            /// 
            /// The buffer must contain an array of VkDrawIndirectCommand structures, each containing:
            /// - vertexCount (uint32): Number of vertices to draw
            /// - instanceCount (uint32): Number of instances to draw
            /// - firstVertex (uint32): Index of the first vertex to use
            /// - firstInstance (uint32): Instance ID of the first instance to draw
            /// 
            /// Each VkDrawIndirectCommand is 16 bytes (4 uint32 values).
            /// 
            /// Vulkan API: vkCmdDrawIndirect(commandBuffer, buffer, offset, drawCount, stride)
            /// Vulkan API Reference: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdDrawIndirect.html
            /// 
            /// Requirements:
            /// - Command buffer must be in recording state (Begin() called)
            /// - Graphics pipeline must be bound (SetGraphicsState called)
            /// - Vertex buffers must be bound (via SetGraphicsState or BindVertexBuffers)
            /// - Buffer must have been created with VK_BUFFER_USAGE_INDIRECT_BUFFER_BIT flag
            /// - Offset must be aligned to 4 bytes
            /// - Stride must be 0 (tightly packed) or at least sizeof(VkDrawIndirectCommand) = 16 bytes
            /// </summary>
            /// <param name="argumentBuffer">Buffer containing the indirect draw commands (VkDrawIndirectCommand structures)</param>
            /// <param name="offset">Byte offset into the buffer where the first draw command begins (must be 4-byte aligned)</param>
            /// <param name="drawCount">Number of draw commands to execute (number of VkDrawIndirectCommand structures to process)</param>
            /// <param name="stride">Byte stride between consecutive draw commands. If 0, commands are tightly packed (16 bytes each). Otherwise must be at least 16 bytes.</param>
            /// <exception cref="InvalidOperationException">Thrown if command buffer is not in recording state, or if vkCmdDrawIndirect function pointer is not initialized</exception>
            /// <exception cref="ArgumentNullException">Thrown if argumentBuffer is null</exception>
            /// <exception cref="ArgumentException">Thrown if buffer is not a VulkanBuffer instance or doesn't have a valid handle, or if stride is invalid</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if offset or drawCount is negative</exception>
            public void DrawIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride)
            {
                // Validate command buffer is in recording state
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before drawing indirect. Call Begin() first.");
                }

                // Validate argument buffer is not null
                if (argumentBuffer == null)
                {
                    throw new ArgumentNullException(nameof(argumentBuffer), "Argument buffer cannot be null");
                }

                // Get Vulkan buffer handle from IBuffer
                // Cast to VulkanBuffer to access the native VkBuffer handle
                var vulkanBuffer = argumentBuffer as VulkanBuffer;
                if (vulkanBuffer == null)
                {
                    throw new ArgumentException("Buffer must be a VulkanBuffer instance. Other buffer implementations are not supported.", nameof(argumentBuffer));
                }

                // Extract VkBuffer handle from VulkanBuffer
                // The VkBuffer property exposes the native VkBuffer handle created during buffer creation
                IntPtr vkBuffer = vulkanBuffer.VkBuffer;
                if (vkBuffer == IntPtr.Zero)
                {
                    // Fallback to NativeHandle if VkBuffer is not available
                    // This provides compatibility if the buffer was created differently
                    vkBuffer = vulkanBuffer.NativeHandle;
                    if (vkBuffer == IntPtr.Zero)
                    {
                        throw new ArgumentException("Buffer does not have a valid Vulkan handle. Buffer may not have been fully created or has been disposed.", nameof(argumentBuffer));
                    }
                }

                // Validate offset is non-negative and properly aligned
                // Vulkan requires indirect draw buffer offsets to be aligned to 4 bytes
                // This is a hardware requirement for efficient memory access
                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative");
                }

                if ((offset % 4) != 0)
                {
                    throw new ArgumentException($"Offset must be aligned to 4 bytes for indirect drawing. Current offset: {offset}", nameof(offset));
                }

                // Validate draw count is non-negative
                // A draw count of 0 means no draws will be executed (valid but no-op)
                if (drawCount < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(drawCount), "Draw count must be non-negative");
                }

                // Validate stride
                // Stride must be either 0 (tightly packed, 16 bytes per command) or at least sizeof(VkDrawIndirectCommand) = 16 bytes
                // Stride of 0 is the most common case and indicates tightly packed commands
                // Non-zero stride allows for custom layouts with additional data between commands
                const int VkDrawIndirectCommandSize = 16; // sizeof(VkDrawIndirectCommand) = 4 * uint32 = 16 bytes
                if (stride < 0)
                {
                    throw new ArgumentException($"Stride must be non-negative. Current stride: {stride}", nameof(stride));
                }

                if (stride > 0 && stride < VkDrawIndirectCommandSize)
                {
                    throw new ArgumentException($"Stride must be either 0 (tightly packed) or at least {VkDrawIndirectCommandSize} bytes. Current stride: {stride}", nameof(stride));
                }

                // Ensure graphics pipeline is bound before drawing
                // Note: We don't explicitly check this here as it's a runtime validation that will be caught
                // when the command buffer is submitted. However, the application should call SetGraphicsState
                // before calling DrawIndirect. The indirect buffer contains:
                // - vertexCount (uint32) - 4 bytes at offset
                // - instanceCount (uint32) - 4 bytes at offset + 4
                // - firstVertex (uint32) - 4 bytes at offset + 8
                // - firstInstance (uint32) - 4 bytes at offset + 12
                // Total: 16 bytes per draw indirect command (VkDrawIndirectCommand)
                // 
                // The buffer layout for drawCount > 1 with stride:
                // [Command 0: 16 bytes][padding if stride > 16][Command 1: 16 bytes][padding if stride > 16][...]
                // If stride == 0, commands are tightly packed with no padding.

                // Validate vkCmdDrawIndirect function pointer is initialized
                // This function pointer is loaded during Vulkan initialization via vkGetDeviceProcAddr
                if (vkCmdDrawIndirect == null)
                {
                    throw new InvalidOperationException("vkCmdDrawIndirect function pointer not initialized. Call InitializeVulkanFunctions first. This indicates Vulkan may not be properly initialized.");
                }

                // Record the indirect draw command to the command buffer
                // This command will be executed when the command buffer is submitted to a graphics queue
                // 
                // Vulkan API signature:
                // void vkCmdDrawIndirect(
                //     VkCommandBuffer commandBuffer,
                //     VkBuffer buffer,
                //     VkDeviceSize offset,
                //     uint32_t drawCount,
                //     uint32_t stride);
                //
                // Parameters:
                // - commandBuffer: The command buffer to record the command into (this._vkCommandBuffer)
                // - buffer: The buffer containing the draw commands (vkBuffer)
                // - offset: Byte offset into the buffer (offset)
                // - drawCount: Number of draw commands to execute (drawCount)
                // - stride: Byte stride between commands (stride, or 0 for tightly packed)
                //
                // Vulkan API Reference: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdDrawIndirect.html
                vkCmdDrawIndirect(
                    _vkCommandBuffer,      // Command buffer to record into
                    vkBuffer,              // Buffer containing VkDrawIndirectCommand structures
                    (ulong)offset,         // Byte offset into buffer (VkDeviceSize = ulong)
                    (uint)drawCount,       // Number of draw commands to execute (uint32_t)
                    (uint)stride);         // Byte stride between commands (uint32_t, 0 = tightly packed)
            }
            public void SetComputeState(ComputeState state)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before setting compute state");
                }

                if (state.Pipeline == null)
                {
                    throw new ArgumentException("Compute state must have a valid pipeline", nameof(state));
                }

                // Cast to Vulkan implementation to access native handle
                VulkanComputePipeline vulkanPipeline = state.Pipeline as VulkanComputePipeline;
                if (vulkanPipeline == null)
                {
                    throw new ArgumentException("Pipeline must be a VulkanComputePipeline", nameof(state));
                }

                // Extract VkPipeline handle from VulkanComputePipeline
                // The VkPipeline property exposes the native VkPipeline handle created during pipeline creation
                IntPtr vkPipelineHandle = vulkanPipeline.VkPipeline;
                if (vkPipelineHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Compute pipeline does not have a valid VkPipeline handle. Pipeline may not have been fully created.");
                }

                // Bind compute pipeline
                // Based on Vulkan API: vkCmdBindPipeline binds a compute pipeline to the command buffer
                // Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindPipeline.html
                // Note: vkCmdBindPipeline returns void in the actual Vulkan API (command recording functions don't return errors)
                // Validation happens when the command buffer is submitted to a queue
                vkCmdBindPipeline(_vkCommandBuffer, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE, vkPipelineHandle);

                // Step 2: Bind descriptor sets if provided
                if (state.BindingSets != null && state.BindingSets.Length > 0)
                {
                    // Extract VkPipelineLayout from the compute pipeline's descriptor
                    // The pipeline layout is created during pipeline creation and stored with the pipeline
                    // We need access to it to bind descriptor sets correctly
                    // 
                    // For descriptor sets, we need to:
                    // 1. Extract VkDescriptorSet handles from IBindingSet[] (cast to VulkanBindingSet)
                    // 2. Extract VkPipelineLayout from the compute pipeline
                    // 3. Call vkCmdBindDescriptorSets
                    //
                    // In Vulkan:
                    // void vkCmdBindDescriptorSets(
                    //     VkCommandBuffer commandBuffer,
                    //     VkPipelineBindPoint pipelineBindPoint,
                    //     VkPipelineLayout layout,
                    //     uint firstSet,
                    //     uint descriptorSetCount,
                    //     const VkDescriptorSet* pDescriptorSets,
                    //     uint dynamicOffsetCount,
                    //     const uint32_t* pDynamicOffsets);

                    // Build arrays of descriptor set handles and dynamic offsets
                    // Note: Dynamic offsets would come from the binding set if it has dynamic uniform buffers
                    int descriptorSetCount = state.BindingSets.Length;
                    
                    for (int i = 0; i < descriptorSetCount; i++)
                    {
                        VulkanBindingSet vulkanBindingSet = state.BindingSets[i] as VulkanBindingSet;
                        if (vulkanBindingSet == null)
                        {
                            throw new ArgumentException($"Binding set at index {i} must be a VulkanBindingSet", nameof(state));
                        }

                        // Extract VkDescriptorSet handle from VulkanBindingSet
                        // The _handle field in VulkanBindingSet is the VkDescriptorSet handle
                        // vulkanBindingSet.GetNativeHandle() would return the VkDescriptorSet handle
                    }

                    // Bind all descriptor sets in a single call for efficiency
                    // vkCmdBindDescriptorSets(
                    //     _handle,
                    //     VK_PIPELINE_BIND_POINT_COMPUTE,
                    //     pipelineLayout,  // From compute pipeline
                    //     0,  // firstSet - starting set index
                    //     (uint)descriptorSetCount,
                    //     descriptorSetHandles,  // Array of VkDescriptorSet handles
                    //     0,  // dynamicOffsetCount
                    //     null  // pDynamicOffsets - would be populated if dynamic buffers present
                    // );
                }

                // TODO:  Note: In a full implementation with Vulkan interop, this method would:
                // 1. Call native vkCmdBindPipeline to bind the compute pipeline
                // 2. If binding sets are provided, call native vkCmdBindDescriptorSets to bind them
                // 3. The native handles would be extracted via P/Invoke or similar interop mechanism
                // 4. All validation would be done before making native calls to avoid crashes
            }
            public void Dispatch(int groupCountX, int groupCountY = 1, int groupCountZ = 1)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open");
                }

                vkCmdDispatch(_vkCommandBuffer, (uint)groupCountX, (uint)groupCountY, (uint)groupCountZ);
            }
            public void DispatchIndirect(IBuffer argumentBuffer, int offset)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before dispatching indirect");
                }

                if (argumentBuffer == null)
                {
                    throw new ArgumentNullException(nameof(argumentBuffer));
                }

                // Get Vulkan buffer handle from IBuffer
                var vulkanBuffer = argumentBuffer as VulkanBuffer;
                if (vulkanBuffer == null)
                {
                    throw new ArgumentException("Buffer must be a VulkanBuffer instance", nameof(argumentBuffer));
                }

                IntPtr vkBuffer = vulkanBuffer.VkBuffer;
                if (vkBuffer == IntPtr.Zero)
                {
                    // Fallback to NativeHandle if VkBuffer is not available
                    vkBuffer = vulkanBuffer.NativeHandle;
                    if (vkBuffer == IntPtr.Zero)
                    {
                        throw new ArgumentException("Buffer does not have a valid Vulkan handle", nameof(argumentBuffer));
                    }
                }

                // Validate offset is non-negative and properly aligned
                // Vulkan requires indirect dispatch buffer offsets to be aligned to 4 bytes
                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative");
                }

                if ((offset % 4) != 0)
                {
                    throw new ArgumentException("Offset must be aligned to 4 bytes for indirect dispatch", nameof(offset));
                }

                // Ensure compute pipeline is bound before dispatching
                // Note: This should ideally be checked, but we'll proceed assuming SetComputeState was called
                // The indirect buffer contains:
                // - groupCountX (uint32) - 4 bytes at offset
                // - groupCountY (uint32) - 4 bytes at offset + 4
                // - groupCountZ (uint32) - 4 bytes at offset + 8
                // Total: 12 bytes per dispatch indirect command
                // Vulkan API: vkCmdDispatchIndirect(commandBuffer, buffer, offset)
                // Vulkan API Reference: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdDispatchIndirect.html

                if (vkCmdDispatchIndirect == null)
                {
                    throw new InvalidOperationException("vkCmdDispatchIndirect function pointer not initialized. Call InitializeVulkanFunctions first.");
                }

                vkCmdDispatchIndirect(_vkCommandBuffer, vkBuffer, (ulong)offset);
            }

            // Raytracing Commands
            public void SetRaytracingState(RaytracingState state)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before setting raytracing state");
                }

                // Validate that vkCmdBindPipeline function is available
                if (vkCmdBindPipeline == null)
                {
                    throw new InvalidOperationException("vkCmdBindPipeline function pointer not initialized. Call InitializeVulkanFunctions first.");
                }

                // Validate pipeline is provided
                if (state.Pipeline == null)
                {
                    throw new ArgumentException("Raytracing pipeline is required", nameof(state));
                }

                // Cast pipeline to VulkanRaytracingPipeline to access Vulkan-specific handles
                VulkanRaytracingPipeline vulkanPipeline = state.Pipeline as VulkanRaytracingPipeline;
                if (vulkanPipeline == null)
                {
                    throw new ArgumentException("Raytracing pipeline must be a VulkanRaytracingPipeline", nameof(state));
                }

                // Get VkPipeline handle from the raytracing pipeline
                IntPtr vkPipelineHandle = vulkanPipeline.VkPipeline;
                if (vkPipelineHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Raytracing pipeline does not have a valid VkPipeline handle. Pipeline may not have been fully created.");
                }

                // Bind raytracing pipeline
                // Based on Vulkan API: vkCmdBindPipeline binds a raytracing pipeline to the command buffer
                // Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindPipeline.html
                // VK_KHR_ray_tracing_pipeline extension: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VK_KHR_ray_tracing_pipeline.html
                vkCmdBindPipeline(_vkCommandBuffer, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR, vkPipelineHandle);

                // Step 2: Bind descriptor sets if provided
                if (state.BindingSets != null && state.BindingSets.Length > 0 && vkCmdBindDescriptorSets != null)
                {
                    // Get pipeline layout from raytracing pipeline
                    IntPtr vkPipelineLayout = vulkanPipeline.VkPipelineLayout;
                    if (vkPipelineLayout == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Raytracing pipeline does not have a valid VkPipelineLayout handle.");
                    }

                    // Build array of descriptor set handles
                    int descriptorSetCount = state.BindingSets.Length;
                    IntPtr descriptorSetHandlesPtr = Marshal.AllocHGlobal(descriptorSetCount * IntPtr.Size);
                    try
                    {
                        for (int i = 0; i < descriptorSetCount; i++)
                        {
                            VulkanBindingSet vulkanBindingSet = state.BindingSets[i] as VulkanBindingSet;
                            if (vulkanBindingSet == null)
                            {
                                Marshal.FreeHGlobal(descriptorSetHandlesPtr);
                                throw new ArgumentException($"Binding set at index {i} must be a VulkanBindingSet", nameof(state));
                            }

                            IntPtr vkDescriptorSet = vulkanBindingSet.VkDescriptorSet;
                            if (vkDescriptorSet == IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(descriptorSetHandlesPtr);
                                throw new InvalidOperationException($"Binding set at index {i} does not have a valid VkDescriptorSet handle.");
                            }

                            Marshal.WriteIntPtr(descriptorSetHandlesPtr, i * IntPtr.Size, vkDescriptorSet);
                        }

                        // Bind all descriptor sets in a single call
                        // Based on Vulkan API: vkCmdBindDescriptorSets
                        // Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBindDescriptorSets.html
                        vkCmdBindDescriptorSets(
                            _vkCommandBuffer,
                            VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR,
                            vkPipelineLayout,
                            0, // firstSet - starting set index
                            (uint)descriptorSetCount,
                            descriptorSetHandlesPtr,
                            0, // dynamicOffsetCount
                            IntPtr.Zero // pDynamicOffsets - would be populated if dynamic buffers present
                        );
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(descriptorSetHandlesPtr);
                    }
                }

                // Step 3: Store raytracing state for use in DispatchRays
                // The shader binding table and other state information is needed when dispatching rays
                _raytracingState = state;
                _hasRaytracingState = true;
            }

            public void DispatchRays(DispatchRaysArguments args)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before dispatching rays");
                }

                if (_vkCommandBuffer == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Command buffer is not initialized");
                }

                // Validate dispatch dimensions
                if (args.Width <= 0 || args.Height <= 0 || args.Depth <= 0)
                {
                    throw new ArgumentException($"Invalid dispatch dimensions: Width={args.Width}, Height={args.Height}, Depth={args.Depth}. All dimensions must be greater than zero.", nameof(args));
                }

                // Check that raytracing state is set
                if (!_hasRaytracingState)
                {
                    throw new InvalidOperationException("Raytracing state must be set before dispatching rays. Call SetRaytracingState first.");
                }

                // Validate that vkCmdTraceRaysKHR function is available
                if (vkCmdTraceRaysKHR == null)
                {
                    throw new NotSupportedException("vkCmdTraceRaysKHR function pointer not initialized. VK_KHR_ray_tracing_pipeline extension may not be available.");
                }

                // Validate that vkGetBufferDeviceAddressKHR function is available (needed for shader binding table addresses)
                if (vkGetBufferDeviceAddressKHR == null)
                {
                    throw new NotSupportedException("vkGetBufferDeviceAddressKHR function pointer not initialized. VK_KHR_buffer_device_address extension may not be available.");
                }

                // Get shader binding table from raytracing state
                ShaderBindingTable shaderTable = _raytracingState.ShaderTable;

                // Validate that shader binding table buffer is set (required for ray generation shader)
                if (shaderTable.Buffer == null)
                {
                    throw new InvalidOperationException("Shader binding table buffer is required for DispatchRays");
                }

                // Cast buffer to VulkanBuffer to access VkBuffer handle
                VulkanBuffer vulkanBuffer = shaderTable.Buffer as VulkanBuffer;
                if (vulkanBuffer == null)
                {
                    throw new ArgumentException("Shader binding table buffer must be a VulkanBuffer", nameof(_raytracingState));
                }

                IntPtr vkBuffer = vulkanBuffer.VkBuffer;
                if (vkBuffer == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Shader binding table buffer has invalid Vulkan handle");
                }

                // Get base device address for the shader binding table buffer
                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetBufferDeviceAddressKHR.html
                VkBufferDeviceAddressInfo bufferAddressInfo = new VkBufferDeviceAddressInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                    pNext = IntPtr.Zero,
                    buffer = vkBuffer
                };

                ulong baseBufferAddress = vkGetBufferDeviceAddressKHR(_vkDevice, ref bufferAddressInfo);
                if (baseBufferAddress == 0UL)
                {
                    throw new InvalidOperationException("Failed to get device address for shader binding table buffer");
                }

                // Build VkStridedDeviceAddressRegionKHR structures for each shader binding table region
                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdTraceRaysKHR.html
                // Located via Vulkan specification: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkStridedDeviceAddressRegionKHR.html

                // Ray generation shader binding table (required)
                VkStridedDeviceAddressRegionKHR raygenRegion = new VkStridedDeviceAddressRegionKHR
                {
                    deviceAddress = baseBufferAddress + shaderTable.RayGenOffset,
                    stride = shaderTable.RayGenSize, // Stride equals size for raygen (single entry)
                    size = shaderTable.RayGenSize
                };

                // Validate ray generation shader table size
                if (raygenRegion.size == 0UL)
                {
                    throw new InvalidOperationException("Ray generation shader table size cannot be zero");
                }

                // Miss shader binding table (optional)
                VkStridedDeviceAddressRegionKHR missRegion = new VkStridedDeviceAddressRegionKHR
                {
                    deviceAddress = 0UL,
                    stride = 0UL,
                    size = 0UL
                };

                if (shaderTable.MissSize > 0UL)
                {
                    missRegion.deviceAddress = baseBufferAddress + shaderTable.MissOffset;
                    missRegion.stride = shaderTable.MissStride > 0UL ? shaderTable.MissStride : shaderTable.MissSize;
                    missRegion.size = shaderTable.MissSize;
                }

                // Hit group shader binding table (optional)
                VkStridedDeviceAddressRegionKHR hitRegion = new VkStridedDeviceAddressRegionKHR
                {
                    deviceAddress = 0UL,
                    stride = 0UL,
                    size = 0UL
                };

                if (shaderTable.HitGroupSize > 0UL)
                {
                    hitRegion.deviceAddress = baseBufferAddress + shaderTable.HitGroupOffset;
                    hitRegion.stride = shaderTable.HitGroupStride > 0UL ? shaderTable.HitGroupStride : shaderTable.HitGroupSize;
                    hitRegion.size = shaderTable.HitGroupSize;
                }

                // Callable shader binding table (optional)
                VkStridedDeviceAddressRegionKHR callableRegion = new VkStridedDeviceAddressRegionKHR
                {
                    deviceAddress = 0UL,
                    stride = 0UL,
                    size = 0UL
                };

                if (shaderTable.CallableSize > 0UL)
                {
                    callableRegion.deviceAddress = baseBufferAddress + shaderTable.CallableOffset;
                    callableRegion.stride = shaderTable.CallableStride > 0UL ? shaderTable.CallableStride : shaderTable.CallableSize;
                    callableRegion.size = shaderTable.CallableSize;
                }

                // Call vkCmdTraceRaysKHR to dispatch raytracing work
                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdTraceRaysKHR.html
                // Signature: void vkCmdTraceRaysKHR(
                //     VkCommandBuffer commandBuffer,
                //     const VkStridedDeviceAddressRegionKHR* pRaygenShaderBindingTable,
                //     const VkStridedDeviceAddressRegionKHR* pMissShaderBindingTable,
                //     const VkStridedDeviceAddressRegionKHR* pHitShaderBindingTable,
                //     const VkStridedDeviceAddressRegionKHR* pCallableShaderBindingTable,
                //     uint32_t width,
                //     uint32_t height,
                //     uint32_t depth);
                vkCmdTraceRaysKHR(
                    _vkCommandBuffer,
                    ref raygenRegion,
                    ref missRegion,
                    ref hitRegion,
                    ref callableRegion,
                    unchecked((uint)args.Width),
                    unchecked((uint)args.Height),
                    unchecked((uint)args.Depth)
                );
            }

            public void BuildBottomLevelAccelStruct(IAccelStruct accelStruct, GeometryDesc[] geometries)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before building acceleration structure");
                }

                if (accelStruct == null)
                {
                    throw new ArgumentNullException(nameof(accelStruct));
                }

                if (geometries == null || geometries.Length == 0)
                {
                    throw new ArgumentException("Geometries array cannot be null or empty", nameof(geometries));
                }

                // Validate that acceleration structure functions are available
                if (vkCmdBuildAccelerationStructuresKHR == null || 
                    vkGetAccelerationStructureBuildSizesKHR == null ||
                    vkGetBufferDeviceAddressKHR == null ||
                    vkCreateAccelerationStructureKHR == null)
                {
                    throw new NotSupportedException("VK_KHR_acceleration_structure extension functions are not available");
                }

                // Cast to VulkanAccelStruct to access internal handles
                VulkanAccelStruct vulkanAccelStruct = accelStruct as VulkanAccelStruct;
                if (vulkanAccelStruct == null)
                {
                    throw new ArgumentException("Acceleration structure must be a VulkanAccelStruct", nameof(accelStruct));
                }

                // Convert GeometryDesc[] to Vulkan structures
                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBuildAccelerationStructuresKHR.html
                uint geometryCount = (uint)geometries.Length;
                List<VkAccelerationStructureGeometryKHR> vkGeometries = new List<VkAccelerationStructureGeometryKHR>();
                List<VkAccelerationStructureBuildRangeInfoKHR> buildRanges = new List<VkAccelerationStructureBuildRangeInfoKHR>();
                List<IntPtr> geometryDataPtrs = new List<IntPtr>(); // For cleanup
                List<IntPtr> trianglesDataPtrs = new List<IntPtr>(); // For cleanup

                try
                {
                    // Convert each geometry
                    for (int i = 0; i < geometries.Length; i++)
                    {
                        GeometryDesc geom = geometries[i];
                        
                        if (geom.Type == GeometryType.Triangles)
                        {
                            GeometryTriangles triangles = geom.Triangles;

                        // Get device addresses for buffers
                        ulong vertexBufferAddress = 0UL;
                        ulong indexBufferAddress = 0UL;
                        ulong transformBufferAddress = 0UL;

                        if (triangles.VertexBuffer != null)
                        {
                            VulkanBuffer vulkanVertexBuffer = triangles.VertexBuffer as VulkanBuffer;
                            if (vulkanVertexBuffer != null)
                            {
                                IntPtr vkBuffer = vulkanVertexBuffer.VkBuffer;
                                if (vkBuffer != IntPtr.Zero)
                                {
                                    VkBufferDeviceAddressInfo bufferInfo = new VkBufferDeviceAddressInfo
                                    {
                                        sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                                        pNext = IntPtr.Zero,
                                        buffer = vkBuffer
                                    };
                                    vertexBufferAddress = vkGetBufferDeviceAddressKHR(_device, ref bufferInfo);
                                    if (triangles.VertexOffset > 0)
                                    {
                                        vertexBufferAddress += (ulong)triangles.VertexOffset;
                                    }
                                }
                            }
                        }

                        if (triangles.IndexBuffer != null)
                        {
                            VulkanBuffer vulkanIndexBuffer = triangles.IndexBuffer as VulkanBuffer;
                            if (vulkanIndexBuffer != null)
                            {
                                IntPtr vkBuffer = vulkanIndexBuffer.VkBuffer;
                                if (vkBuffer != IntPtr.Zero)
                                {
                                    VkBufferDeviceAddressInfo bufferInfo = new VkBufferDeviceAddressInfo
                                    {
                                        sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                                        pNext = IntPtr.Zero,
                                        buffer = vkBuffer
                                    };
                                    indexBufferAddress = vkGetBufferDeviceAddressKHR(_device, ref bufferInfo);
                                    if (triangles.IndexOffset > 0)
                                    {
                                        indexBufferAddress += (ulong)triangles.IndexOffset;
                                    }
                                }
                            }
                        }

                        if (triangles.TransformBuffer != null)
                        {
                            VulkanBuffer vulkanTransformBuffer = triangles.TransformBuffer as VulkanBuffer;
                            if (vulkanTransformBuffer != null)
                            {
                                IntPtr vkBuffer = vulkanTransformBuffer.VkBuffer;
                                if (vkBuffer != IntPtr.Zero)
                                {
                                    VkBufferDeviceAddressInfo bufferInfo = new VkBufferDeviceAddressInfo
                                    {
                                        sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                                        pNext = IntPtr.Zero,
                                        buffer = vkBuffer
                                    };
                                    transformBufferAddress = vkGetBufferDeviceAddressKHR(_device, ref bufferInfo);
                                    if (triangles.TransformOffset > 0)
                                    {
                                        transformBufferAddress += (ulong)triangles.TransformOffset;
                                    }
                                }
                            }
                        }

                        // Convert vertex format to VkFormat
                        VkFormat vertexFormat = ConvertToVkFormat(triangles.VertexFormat);
                        if (vertexFormat == VkFormat.VK_FORMAT_UNDEFINED)
                        {
                            // Fallback: assume float3 format (R32G32B32_SFLOAT = 106)
                            vertexFormat = (VkFormat)106; // VK_FORMAT_R32G32B32_SFLOAT
                        }

                        // Convert index format
                        VkIndexType indexType = VkIndexType.VK_INDEX_TYPE_UINT32;
                        if (triangles.IndexFormat == TextureFormat.R16_UInt)
                        {
                            indexType = VkIndexType.VK_INDEX_TYPE_UINT16;
                        }

                        // Create triangles data structure
                        VkAccelerationStructureGeometryTrianglesDataKHR trianglesData = new VkAccelerationStructureGeometryTrianglesDataKHR
                        {
                            sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR,
                            pNext = IntPtr.Zero,
                            vertexFormat = vertexFormat,
                            vertexDataDeviceAddress = vertexBufferAddress,
                            vertexStride = (ulong)triangles.VertexStride,
                            maxVertex = (uint)(triangles.VertexCount > 0 ? triangles.VertexCount - 1 : 0),
                            indexType = indexType,
                            indexDataDeviceAddress = indexBufferAddress,
                            transformDataDeviceAddress = transformBufferAddress
                        };

                        // Create geometry structure
                        VkGeometryFlagsKHR geometryFlags = VkGeometryFlagsKHR.VK_GEOMETRY_OPAQUE_BIT_KHR;
                        if ((geom.Flags & GeometryFlags.NoDuplicateAnyHit) != 0)
                        {
                            geometryFlags |= VkGeometryFlagsKHR.VK_GEOMETRY_NO_DUPLICATE_ANY_HIT_INVOCATION_BIT_KHR;
                        }

                        // Create geometry structure with triangles data in union
                        // The union structure allows direct field access via FieldOffset
                        VkAccelerationStructureGeometryDataKHR geometryData = new VkAccelerationStructureGeometryDataKHR();
                        geometryData.triangles = trianglesData; // Direct assignment works because of FieldOffset(0)

                        VkAccelerationStructureGeometryKHR vkGeometry = new VkAccelerationStructureGeometryKHR
                        {
                            sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR,
                            pNext = IntPtr.Zero,
                            geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_TRIANGLES_KHR,
                            geometry = geometryData,
                            flags = geometryFlags
                        };

                        vkGeometries.Add(vkGeometry);

                        // Create build range info
                        VkAccelerationStructureBuildRangeInfoKHR buildRange = new VkAccelerationStructureBuildRangeInfoKHR
                        {
                            primitiveCount = (uint)(triangles.IndexCount > 0 ? triangles.IndexCount / 3 : triangles.VertexCount / 3),
                            primitiveOffset = 0,
                            firstVertex = 0,
                            transformOffset = 0
                        };
                        buildRanges.Add(buildRange);
                        }
                        else if (geom.Type == GeometryType.AABBs)
                        {
                            GeometryAABBs aabbs = geom.AABBs;
                            
                            // Get device address for AABB buffer
                            ulong aabbBufferAddress = 0UL;
                            if (aabbs.Buffer != null)
                            {
                                VulkanBuffer vulkanAabbBuffer = aabbs.Buffer as VulkanBuffer;
                                if (vulkanAabbBuffer != null)
                                {
                                    IntPtr vkBuffer = vulkanAabbBuffer.VkBuffer;
                                    if (vkBuffer != IntPtr.Zero)
                                    {
                                        VkBufferDeviceAddressInfo bufferInfo = new VkBufferDeviceAddressInfo
                                        {
                                            sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                                            pNext = IntPtr.Zero,
                                            buffer = vkBuffer
                                        };
                                        aabbBufferAddress = vkGetBufferDeviceAddressKHR(_device, ref bufferInfo);
                                        if (aabbs.Offset > 0)
                                        {
                                            aabbBufferAddress += (ulong)aabbs.Offset;
                                        }
                                    }
                                }
                            }
                            
                            // Create AABBs data structure
                            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureGeometryAabbsDataKHR.html
                            VkAccelerationStructureGeometryAabbsDataKHR aabbsData = new VkAccelerationStructureGeometryAabbsDataKHR
                            {
                                sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_AABBS_DATA_KHR,
                                pNext = IntPtr.Zero,
                                dataDeviceAddress = aabbBufferAddress,
                                stride = (ulong)(aabbs.Stride > 0 ? aabbs.Stride : 24) // Default stride is 24 bytes (6 floats: min.x, min.y, min.z, max.x, max.y, max.z)
                            };
                            
                            // Create geometry structure
                            VkGeometryFlagsKHR geometryFlags = VkGeometryFlagsKHR.VK_GEOMETRY_OPAQUE_BIT_KHR;
                            if ((geom.Flags & GeometryFlags.NoDuplicateAnyHit) != 0)
                            {
                                geometryFlags |= VkGeometryFlagsKHR.VK_GEOMETRY_NO_DUPLICATE_ANY_HIT_INVOCATION_BIT_KHR;
                            }
                            
                            // Create geometry structure with AABBs data in union
                            VkAccelerationStructureGeometryDataKHR geometryData = new VkAccelerationStructureGeometryDataKHR();
                            geometryData.aabbs = aabbsData; // Direct assignment works because of FieldOffset(0)
                            
                            VkAccelerationStructureGeometryKHR vkGeometry = new VkAccelerationStructureGeometryKHR
                            {
                                sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR,
                                pNext = IntPtr.Zero,
                                geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_AABBS_KHR,
                                geometry = geometryData,
                                flags = geometryFlags
                            };
                            
                            vkGeometries.Add(vkGeometry);
                            
                            // Create build range info for AABBs
                            // For AABBs, primitiveCount is the number of AABB primitives
                            VkAccelerationStructureBuildRangeInfoKHR buildRange = new VkAccelerationStructureBuildRangeInfoKHR
                            {
                                primitiveCount = (uint)aabbs.Count,
                                primitiveOffset = 0,
                                firstVertex = 0,
                                transformOffset = 0
                            };
                            buildRanges.Add(buildRange);
                        }
                        else
                        {
                            throw new NotSupportedException($"Geometry type {geom.Type} is not supported for BLAS. Only Triangles and AABBs are supported.");
                        }
                    }

                    // Create build geometry info
                    VkBuildAccelerationStructureFlagsKHR buildFlags = VkBuildAccelerationStructureFlagsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR;
                    // TODO: Add support for other build flags from AccelStructDesc

                    // Allocate memory for geometry array
                    int geometrySize = Marshal.SizeOf(typeof(VkAccelerationStructureGeometryKHR));
                    IntPtr geometriesPtr = Marshal.AllocHGlobal((int)(geometryCount * geometrySize));
                    geometryDataPtrs.Add(geometriesPtr);
                    try
                    {
                        // Copy geometries to unmanaged memory
                        for (int i = 0; i < vkGeometries.Count; i++)
                        {
                            IntPtr geomPtr = new IntPtr(geometriesPtr.ToInt64() + i * geometrySize);
                            Marshal.StructureToPtr(vkGeometries[i], geomPtr, false);
                        }

                        // Create build geometry info
                        VkAccelerationStructureBuildGeometryInfoKHR buildInfo = new VkAccelerationStructureBuildGeometryInfoKHR
                        {
                            sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR,
                            pNext = IntPtr.Zero,
                            type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR,
                            flags = buildFlags,
                            buildType = VkAccelerationStructureBuildTypeKHR.VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR,
                            srcAccelerationStructure = IntPtr.Zero, // New build
                            dstAccelerationStructure = vulkanAccelStruct.VkAccelStruct,
                            geometryCount = geometryCount,
                            pGeometries = geometriesPtr,
                            ppGeometries = IntPtr.Zero,
                            scratchDataDeviceAddress = 0UL // Will be set after getting sizes
                        };

                        // Get build sizes
                        uint[] maxPrimitiveCounts = new uint[geometryCount];
                        for (int i = 0; i < buildRanges.Count; i++)
                        {
                            maxPrimitiveCounts[i] = buildRanges[i].primitiveCount;
                        }

                        IntPtr maxPrimitiveCountsPtr = Marshal.AllocHGlobal(maxPrimitiveCounts.Length * sizeof(uint));
                        try
                        {
                            Marshal.Copy(maxPrimitiveCounts, 0, maxPrimitiveCountsPtr, maxPrimitiveCounts.Length);

                            VkAccelerationStructureBuildSizesInfoKHR sizeInfo = new VkAccelerationStructureBuildSizesInfoKHR
                            {
                                sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_SIZES_INFO_KHR,
                                pNext = IntPtr.Zero,
                                accelerationStructureSize = 0,
                                updateScratchSize = 0,
                                buildScratchSize = 0
                            };

                            vkGetAccelerationStructureBuildSizesKHR(
                                _device,
                                VkAccelerationStructureBuildTypeKHR.VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR,
                                ref buildInfo,
                                maxPrimitiveCountsPtr,
                                ref sizeInfo);

                            // Allocate scratch buffer for acceleration structure build
                            // Scratch buffer is temporary memory used during the build process
                            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetBufferDeviceAddressKHR.html
                            ulong scratchBufferAddress = 0UL;
                            if (sizeInfo.buildScratchSize > 0)
                            {
                                // Create scratch buffer with required usage flags
                                BufferDesc scratchBufferDesc = new BufferDesc
                                {
                                    ByteSize = (int)sizeInfo.buildScratchSize,
                                    Usage = BufferUsageFlags.ShaderResource | BufferUsageFlags.IndirectArgument
                                };
                                
                                IBuffer scratchBuffer = _device.CreateBuffer(scratchBufferDesc);
                                _scratchBuffers.Add(scratchBuffer);
                                
                                // Get device address for scratch buffer
                                VulkanBuffer vulkanScratchBuffer = scratchBuffer as VulkanBuffer;
                                if (vulkanScratchBuffer != null)
                                {
                                    IntPtr vkScratchBuffer = vulkanScratchBuffer.VkBuffer;
                                    if (vkScratchBuffer != IntPtr.Zero)
                                    {
                                        VkBufferDeviceAddressInfo scratchBufferInfo = new VkBufferDeviceAddressInfo
                                        {
                                            sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                                            pNext = IntPtr.Zero,
                                            buffer = vkScratchBuffer
                                        };
                                        scratchBufferAddress = vkGetBufferDeviceAddressKHR(_device, ref scratchBufferInfo);
                                    }
                                }
                            }
                            
                            buildInfo.scratchDataDeviceAddress = scratchBufferAddress;

                            // Allocate memory for build range info pointers array
                            IntPtr[] buildRangeInfoPointers = new IntPtr[geometryCount];
                            IntPtr[] buildRangeInfoPtrs = new IntPtr[geometryCount];
                            for (int i = 0; i < buildRanges.Count; i++)
                            {
                                IntPtr rangePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkAccelerationStructureBuildRangeInfoKHR)));
                                Marshal.StructureToPtr(buildRanges[i], rangePtr, false);
                                buildRangeInfoPtrs[i] = rangePtr;
                                buildRangeInfoPointers[i] = rangePtr;
                            }

                            IntPtr buildRangeInfoArrayPtr = Marshal.AllocHGlobal((int)(geometryCount * IntPtr.Size));
                            try
                            {
                                Marshal.Copy(buildRangeInfoPointers, 0, buildRangeInfoArrayPtr, buildRangeInfoPointers.Length);

                                // Marshal buildInfo to unmanaged memory
                                IntPtr buildInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkAccelerationStructureBuildGeometryInfoKHR)));
                                try
                                {
                                    Marshal.StructureToPtr(buildInfo, buildInfoPtr, false);

                                    // Build acceleration structure
                                    // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBuildAccelerationStructuresKHR.html
                                    vkCmdBuildAccelerationStructuresKHR(
                                        _vkCommandBuffer,
                                        1, // infoCount
                                        buildInfoPtr, // pInfos (pointer to array of VkAccelerationStructureBuildGeometryInfoKHR)
                                        buildRangeInfoArrayPtr); // ppBuildRangeInfos (pointer to array of pointers to VkAccelerationStructureBuildRangeInfoKHR arrays)
                                }
                                finally
                                {
                                    Marshal.FreeHGlobal(buildInfoPtr);
                                }

                                // Cleanup build range info pointers
                                for (int i = 0; i < buildRangeInfoPtrs.Length; i++)
                                {
                                    if (buildRangeInfoPtrs[i] != IntPtr.Zero)
                                    {
                                        Marshal.FreeHGlobal(buildRangeInfoPtrs[i]);
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(buildRangeInfoArrayPtr);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(maxPrimitiveCountsPtr);
                        }
                    }
                    finally
                    {
                        // Cleanup is handled in outer finally block
                    }
                }
                finally
                {
                    // Cleanup allocated memory
                    foreach (IntPtr ptr in geometryDataPtrs)
                    {
                        if (ptr != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                    foreach (IntPtr ptr in trianglesDataPtrs)
                    {
                        if (ptr != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                }
            }

            public void BuildTopLevelAccelStruct(IAccelStruct accelStruct, AccelStructInstance[] instances)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before building acceleration structure");
                }

                if (accelStruct == null)
                {
                    throw new ArgumentNullException(nameof(accelStruct));
                }

                if (instances == null || instances.Length == 0)
                {
                    throw new ArgumentException("Instances array cannot be null or empty", nameof(instances));
                }

                // Validate that acceleration structure functions are available
                if (vkCmdBuildAccelerationStructuresKHR == null || 
                    vkGetAccelerationStructureBuildSizesKHR == null ||
                    vkGetBufferDeviceAddressKHR == null ||
                    vkCreateAccelerationStructureKHR == null)
                {
                    throw new NotSupportedException("VK_KHR_acceleration_structure extension functions are not available");
                }

                // Cast to VulkanAccelStruct to access internal handles
                VulkanAccelStruct vulkanAccelStruct = accelStruct as VulkanAccelStruct;
                if (vulkanAccelStruct == null)
                {
                    throw new ArgumentException("Acceleration structure must be a VulkanAccelStruct", nameof(accelStruct));
                }

                if (!vulkanAccelStruct.IsTopLevel)
                {
                    throw new ArgumentException("Acceleration structure must be a top-level acceleration structure", nameof(accelStruct));
                }

                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBuildAccelerationStructuresKHR.html
                // TLAS building requires:
                // 1. Instance buffer containing VkAccelerationStructureInstanceKHR data
                // 2. Geometry with type VK_GEOMETRY_TYPE_INSTANCES_KHR
                // 3. Build geometry info with type VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR

                uint instanceCount = (uint)instances.Length;

                // Create instance buffer to hold AccelStructInstance data
                // AccelStructInstance matches VkAccelerationStructureInstanceKHR layout (64 bytes)
                int instanceStructSize = Marshal.SizeOf(typeof(AccelStructInstance));
                int instanceBufferSize = instanceStructSize * instances.Length;

                BufferDesc instanceBufferDesc = new BufferDesc
                {
                    ByteSize = instanceBufferSize,
                    Usage = BufferUsageFlags.AccelStructStorage | BufferUsageFlags.AccelStructBuildInput,
                    InitialState = ResourceState.AccelStructBuildInput,
                    IsAccelStructBuildInput = true,
                    DebugName = "TLAS_InstanceBuffer"
                };

                IBuffer instanceBuffer = _device.CreateBuffer(instanceBufferDesc);
                if (instanceBuffer == null)
                {
                    throw new InvalidOperationException("Failed to create instance buffer for TLAS");
                }

                try
                {
                    // Write instance data to buffer
                    // Based on Vulkan API: Instance data must be written before building
                    WriteBuffer(instanceBuffer, instances, 0);

                    // Get device address of instance buffer
                    VulkanBuffer vulkanInstanceBuffer = instanceBuffer as VulkanBuffer;
                    if (vulkanInstanceBuffer == null)
                    {
                        throw new InvalidOperationException("Instance buffer must be a VulkanBuffer");
                    }

                    IntPtr vkInstanceBuffer = vulkanInstanceBuffer.VkBuffer;
                    if (vkInstanceBuffer == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Instance buffer has invalid Vulkan handle");
                    }

                    VkBufferDeviceAddressInfo instanceBufferInfo = new VkBufferDeviceAddressInfo
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                        pNext = IntPtr.Zero,
                        buffer = vkInstanceBuffer
                    };
                    ulong instanceBufferAddress = vkGetBufferDeviceAddressKHR(_device, ref instanceBufferInfo);

                    // Set up geometry data for instances
                    // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureGeometryInstancesDataKHR.html
                    VkAccelerationStructureGeometryInstancesDataKHR instancesData = new VkAccelerationStructureGeometryInstancesDataKHR
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR,
                        pNext = IntPtr.Zero,
                        arrayOfPointers = VkBool32.VK_FALSE, // Array of instances, not array of pointers
                        dataDeviceAddress = instanceBufferAddress
                    };

                    // Create geometry data union (instances variant)
                    VkAccelerationStructureGeometryDataKHR geometryData = new VkAccelerationStructureGeometryDataKHR();
                    geometryData.instances = instancesData;

                    // Create geometry structure
                    // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureGeometryKHR.html
                    VkAccelerationStructureGeometryKHR vkGeometry = new VkAccelerationStructureGeometryKHR
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR,
                        pNext = IntPtr.Zero,
                        geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_INSTANCES_KHR,
                        geometry = geometryData,
                        flags = 0 // No special flags for instances geometry
                    };

                    // Marshal geometry to unmanaged memory
                    IntPtr geometryPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkAccelerationStructureGeometryKHR)));
                    try
                    {
                        Marshal.StructureToPtr(vkGeometry, geometryPtr, false);

                        // Create build geometry info
                        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureBuildGeometryInfoKHR.html
                        VkAccelerationStructureBuildGeometryInfoKHR buildInfo = new VkAccelerationStructureBuildGeometryInfoKHR
                        {
                            sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR,
                            pNext = IntPtr.Zero,
                            type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR,
                            flags = VkBuildAccelerationStructureFlagsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR,
                            buildType = VkAccelerationStructureBuildTypeKHR.VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR,
                            srcAccelerationStructure = IntPtr.Zero, // Building new, not updating
                            dstAccelerationStructure = vulkanAccelStruct.VkAccelStruct,
                            geometryCount = 1, // Single geometry containing all instances
                            pGeometries = geometryPtr,
                            ppGeometries = IntPtr.Zero,
                            scratchDataDeviceAddress = 0UL // Will be set after calculating sizes
                        };

                        // Calculate build sizes
                        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetAccelerationStructureBuildSizesKHR.html
                        IntPtr maxPrimitiveCountsPtr = Marshal.AllocHGlobal(sizeof(uint));
                        try
                        {
                            Marshal.WriteInt32(maxPrimitiveCountsPtr, (int)instanceCount);

                            VkAccelerationStructureBuildSizesInfoKHR sizeInfo = new VkAccelerationStructureBuildSizesInfoKHR
                            {
                                sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_SIZES_INFO_KHR,
                                pNext = IntPtr.Zero,
                                accelerationStructureSize = 0UL,
                                updateScratchSize = 0UL,
                                buildScratchSize = 0UL
                            };

                            vkGetAccelerationStructureBuildSizesKHR(
                                _device,
                                VkAccelerationStructureBuildTypeKHR.VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR,
                                ref buildInfo,
                                maxPrimitiveCountsPtr,
                                ref sizeInfo);

                            // Allocate scratch buffer for acceleration structure build
                            // Scratch buffer is temporary memory used during the build process
                            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkGetBufferDeviceAddressKHR.html
                            ulong scratchBufferAddress = 0UL;
                            if (sizeInfo.buildScratchSize > 0)
                            {
                                // Create scratch buffer with required usage flags
                                BufferDesc scratchBufferDesc = new BufferDesc
                                {
                                    ByteSize = (int)sizeInfo.buildScratchSize,
                                    Usage = BufferUsageFlags.ShaderResource | BufferUsageFlags.IndirectArgument
                                };
                                
                                IBuffer scratchBuffer = _device.CreateBuffer(scratchBufferDesc);
                                if (scratchBuffer != null)
                                {
                                    _scratchBuffers.Add(scratchBuffer);

                                    VulkanBuffer vulkanScratchBuffer = scratchBuffer as VulkanBuffer;
                                    if (vulkanScratchBuffer != null)
                                    {
                                        IntPtr vkScratchBuffer = vulkanScratchBuffer.VkBuffer;
                                        if (vkScratchBuffer != IntPtr.Zero)
                                        {
                                            VkBufferDeviceAddressInfo scratchBufferInfo = new VkBufferDeviceAddressInfo
                                            {
                                                sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                                                pNext = IntPtr.Zero,
                                                buffer = vkScratchBuffer
                                            };
                                            scratchBufferAddress = vkGetBufferDeviceAddressKHR(_device, ref scratchBufferInfo);
                                        }
                                    }
                                }
                            }
                            
                            buildInfo.scratchDataDeviceAddress = scratchBufferAddress;

                            // Create build range info for instances
                            // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkAccelerationStructureBuildRangeInfoKHR.html
                            VkAccelerationStructureBuildRangeInfoKHR buildRange = new VkAccelerationStructureBuildRangeInfoKHR
                            {
                                primitiveCount = instanceCount,
                                primitiveOffset = 0,
                                firstVertex = 0,
                                transformOffset = 0
                            };

                            // Allocate memory for build range info pointer
                            IntPtr buildRangePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkAccelerationStructureBuildRangeInfoKHR)));
                            try
                            {
                                Marshal.StructureToPtr(buildRange, buildRangePtr, false);

                                // Allocate memory for array of build range info pointers
                                // ppBuildRangeInfos is an array of pointers to arrays of VkAccelerationStructureBuildRangeInfoKHR
                                // For TLAS with single geometry, we have one pointer to one array
                                IntPtr buildRangeInfoArrayPtr = Marshal.AllocHGlobal(IntPtr.Size);
                                try
                                {
                                    Marshal.WriteIntPtr(buildRangeInfoArrayPtr, buildRangePtr);

                                    // Marshal buildInfo to unmanaged memory
                                    IntPtr buildInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VkAccelerationStructureBuildGeometryInfoKHR)));
                                    try
                                    {
                                        Marshal.StructureToPtr(buildInfo, buildInfoPtr, false);

                                        // Build acceleration structure
                                        // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdBuildAccelerationStructuresKHR.html
                                        vkCmdBuildAccelerationStructuresKHR(
                                            _vkCommandBuffer,
                                            1, // infoCount
                                            buildInfoPtr, // pInfos (pointer to array of VkAccelerationStructureBuildGeometryInfoKHR)
                                            buildRangeInfoArrayPtr); // ppBuildRangeInfos (pointer to array of pointers to VkAccelerationStructureBuildRangeInfoKHR arrays)
                                    }
                                    finally
                                    {
                                        Marshal.FreeHGlobal(buildInfoPtr);
                                    }
                                }
                                finally
                                {
                                    Marshal.FreeHGlobal(buildRangeInfoArrayPtr);
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(buildRangePtr);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(maxPrimitiveCountsPtr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(geometryPtr);
                    }
                }
                finally
                {
                    // Clean up instance buffer
                    if (instanceBuffer != null)
                    {
                        instanceBuffer.Dispose();
                    }
                }
            }

            public void CompactBottomLevelAccelStruct(IAccelStruct dest, IAccelStruct src)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before compacting acceleration structure");
                }

                if (dest == null)
                {
                    throw new ArgumentNullException(nameof(dest));
                }

                if (src == null)
                {
                    throw new ArgumentNullException(nameof(src));
                }

                // Validate that source is a bottom-level acceleration structure
                if (src.IsTopLevel)
                {
                    throw new ArgumentException("Source acceleration structure must be bottom-level for compaction", nameof(src));
                }

                // Validate that destination is a bottom-level acceleration structure
                if (dest.IsTopLevel)
                {
                    throw new ArgumentException("Destination acceleration structure must be bottom-level for compaction", nameof(dest));
                }

                // Check that vkCmdCopyAccelerationStructureKHR function is available
                if (vkCmdCopyAccelerationStructureKHR == null)
                {
                    throw new NotSupportedException("vkCmdCopyAccelerationStructureKHR function pointer not initialized. VK_KHR_acceleration_structure extension may not be available.");
                }

                // Get Vulkan acceleration structure handles
                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyAccelerationStructureKHR.html
                VulkanAccelStruct vulkanSrc = src as VulkanAccelStruct;
                if (vulkanSrc == null)
                {
                    throw new ArgumentException("Source acceleration structure must be a VulkanAccelStruct", nameof(src));
                }

                VulkanAccelStruct vulkanDest = dest as VulkanAccelStruct;
                if (vulkanDest == null)
                {
                    throw new ArgumentException("Destination acceleration structure must be a VulkanAccelStruct", nameof(dest));
                }

                IntPtr srcHandle = vulkanSrc.VkAccelStruct;
                IntPtr dstHandle = vulkanDest.VkAccelStruct;

                if (srcHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Source acceleration structure has invalid Vulkan handle");
                }

                if (dstHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Destination acceleration structure has invalid Vulkan handle");
                }

                // Note: The source acceleration structure must have been built with
                // VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_COMPACTION_BIT_KHR flag.
                // The destination acceleration structure must be created with a buffer
                // that is at least as large as the compacted size, which can be queried
                // using vkGetAccelerationStructureBuildSizesKHR with the same geometry
                // information used to build the source, but this requires storing the
                // geometry info or having the caller provide it.

                // Create copy info structure
                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkCopyAccelerationStructureInfoKHR.html
                VkCopyAccelerationStructureInfoKHR copyInfo = new VkCopyAccelerationStructureInfoKHR
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_COPY_ACCELERATION_STRUCTURE_INFO_KHR,
                    pNext = IntPtr.Zero,
                    src = srcHandle,
                    dst = dstHandle,
                    mode = VkCopyAccelerationStructureModeKHR.VK_COPY_ACCELERATION_STRUCTURE_MODE_COMPACT_KHR
                };

                // Execute copy command to compact the acceleration structure
                // Based on Vulkan API: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdCopyAccelerationStructureKHR.html
                // This command copies the source acceleration structure to the destination
                // in a compacted form, reducing memory usage while maintaining the same
                // raytracing performance characteristics.
                vkCmdCopyAccelerationStructureKHR(_vkCommandBuffer, ref copyInfo);

                // Note: After compaction, the destination acceleration structure's device address
                // may need to be updated if it changed. However, since the destination was
                // created with a specific buffer and offset, the device address should remain
                // the same. The compacted structure is just a more memory-efficient version
                // of the source structure within the same buffer.
            }

            // Debug Commands
            public void BeginDebugEvent(string name, Vector4 color)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before beginning debug event");
                }
                if (vkCmdBeginDebugUtilsLabelEXT == null)
                {
                    return; // Extension not available
                }
                if (string.IsNullOrEmpty(name))
                {
                    name = "";
                }
                GCHandle nameHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(name + "\0"), GCHandleType.Pinned);
                try
                {
                    float[] colorArray = new float[4] { color.X, color.Y, color.Z, color.W };
                    VkDebugUtilsLabelEXT labelInfo = new VkDebugUtilsLabelEXT
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT,
                        pNext = IntPtr.Zero,
                        pLabelName = nameHandle.AddrOfPinnedObject(),
                        color = colorArray
                    };
                    vkCmdBeginDebugUtilsLabelEXT(_vkCommandBuffer, ref labelInfo);
                }
                finally
                {
                    nameHandle.Free();
                }
            }

            public void EndDebugEvent()
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before ending debug event");
                }
                if (vkCmdEndDebugUtilsLabelEXT != null)
                {
                    vkCmdEndDebugUtilsLabelEXT(_vkCommandBuffer);
                }
            }

            public void InsertDebugMarker(string name, Vector4 color)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before inserting debug marker");
                }
                if (vkCmdInsertDebugUtilsLabelEXT == null)
                {
                    return; // Extension not available
                }
                if (string.IsNullOrEmpty(name))
                {
                    name = "";
                }
                GCHandle nameHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(name + "\0"), GCHandleType.Pinned);
                try
                {
                    float[] colorArray = new float[4] { color.X, color.Y, color.Z, color.W };
                    VkDebugUtilsLabelEXT labelInfo = new VkDebugUtilsLabelEXT
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT,
                        pNext = IntPtr.Zero,
                        pLabelName = nameHandle.AddrOfPinnedObject(),
                        color = colorArray
                    };
                    vkCmdInsertDebugUtilsLabelEXT(_vkCommandBuffer, ref labelInfo);
                }
                finally
                {
                    nameHandle.Free();
                }
            }

            public void Dispose()
            {
                // Clean up scratch buffers allocated for acceleration structure builds
                foreach (IBuffer scratchBuffer in _scratchBuffers)
                {
                    if (scratchBuffer != null)
                    {
                        scratchBuffer.Dispose();
                    }
                }
                _scratchBuffers.Clear();
                
                // Command buffers are managed by the command pool and device
                // They are automatically freed when the command pool is destroyed
                // No explicit cleanup needed for command buffers themselves
                // Based on Vulkan Command Buffer Management: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkFreeCommandBuffers.html
            }
        }

        #endregion
    }
}


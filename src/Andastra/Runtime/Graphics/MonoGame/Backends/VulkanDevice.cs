using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct VkMemoryRequirements
        {
            public ulong size;
            public ulong alignment;
            public uint memoryTypeBits;
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

        private enum VkPipelineBindPoint
        {
            VK_PIPELINE_BIND_POINT_GRAPHICS = 0,
            VK_PIPELINE_BIND_POINT_COMPUTE = 1,
            VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR = 1000165000,
        }

        [Flags]
        private enum VkDependencyFlags
        {
            VK_DEPENDENCY_BY_REGION_BIT = 0x00000001,
        }

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
            VK_STRUCTURE_TYPE_RAY_TRACING_PIPELINE_CREATE_INFO_KHR = 1000165000,
            VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR = 1000165001
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
        private delegate VkResult vkCreateCommandPoolDelegate(IntPtr device, IntPtr pCreateInfo, IntPtr pAllocator, out IntPtr pCommandPool);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
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

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdCopyImageDelegate(IntPtr commandBuffer, IntPtr srcImage, VkImageLayout srcImageLayout, IntPtr dstImage, VkImageLayout dstImageLayout, uint regionCount, IntPtr pRegions);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdCopyBufferToImageDelegate(IntPtr commandBuffer, IntPtr srcBuffer, IntPtr dstImage, VkImageLayout dstImageLayout, uint regionCount, IntPtr pRegions);

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
        private static vkCmdSetBlendConstantsDelegate vkCmdSetBlendConstants;

        // VK_KHR_ray_tracing_pipeline extension function delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateRayTracingPipelinesKHRDelegate(IntPtr device, IntPtr deferredOperation, IntPtr pipelineCache, uint createInfoCount, IntPtr pCreateInfos, IntPtr pAllocator, IntPtr pPipelines);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkGetRayTracingShaderGroupHandlesKHRDelegate(IntPtr device, IntPtr pipeline, uint firstGroup, uint groupCount, ulong dataSize, IntPtr pData);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkCmdTraceRaysKHRDelegate(IntPtr commandBuffer, ref VkStridedDeviceAddressRegionKHR pRaygenShaderBindingTable, ref VkStridedDeviceAddressRegionKHR pMissShaderBindingTable, ref VkStridedDeviceAddressRegionKHR pHitShaderBindingTable, ref VkStridedDeviceAddressRegionKHR pCallableShaderBindingTable, uint width, uint height, uint depth);

        // VK_KHR_ray_tracing_pipeline extension function pointers (static for now - would be loaded via vkGetDeviceProcAddr in real implementation)
        private static vkCreateRayTracingPipelinesKHRDelegate vkCreateRayTracingPipelinesKHR;
        private static vkGetRayTracingShaderGroupHandlesKHRDelegate vkGetRayTracingShaderGroupHandlesKHR;
        private static vkCmdTraceRaysKHRDelegate vkCmdTraceRaysKHR;

        // Helper methods for Vulkan interop
        private static void InitializeVulkanFunctions(IntPtr device)
        {
            // Load Vulkan functions - in a real implementation, these would be loaded via vkGetDeviceProcAddr
            // For this example, we'll assume they're available through P/Invoke
            // This is a simplified version - real implementation would need proper function loading
            
            // Load VK_KHR_acceleration_structure extension functions if available
            LoadAccelerationStructureExtensionFunctions(device);
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
            // For now, we'll use a P/Invoke approach or assume it's available
            // vkGetDeviceProcAddr signature: PFN_vkGetDeviceProcAddr vkGetDeviceProcAddr(VkInstance instance, const char* pName);
            // We need to get vkGetDeviceProcAddr first, then use it to load extension functions
            
            // Note: In a production implementation, vkGetDeviceProcAddr would be obtained from the Vulkan loader
            // For this implementation, we'll provide a mechanism to load the function when the extension is available
            // The actual loading would be done via P/Invoke to the Vulkan loader library (vulkan-1.dll on Windows, libvulkan.so on Linux)
            
            // Placeholder: Function loading would happen here
            // In real implementation:
            // 1. Get vkGetDeviceProcAddr from Vulkan loader
            // 2. Call vkGetDeviceProcAddr(device, "vkDestroyAccelerationStructureKHR") to get function pointer
            // 3. Marshal.GetDelegateForFunctionPointer to convert to delegate
            // 4. Assign to static vkDestroyAccelerationStructureKHR field
            
            // For now, we'll leave it as null - the Dispose method will check for null before calling
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
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

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
                    return LoadLibrary(libraryName);
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
                    return GetProcAddress(libraryHandle, functionName);
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
            _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            _resources = new Dictionary<IntPtr, IResource>();
            _nextResourceHandle = 1;
            _currentFrameIndex = 0;

            // Initialize Vulkan function pointers
            InitializeVulkanFunctions(device);

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
                samples = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT, // TODO: Convert from desc.SampleCount
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
                // For now, we'll skip this and just use the image handle
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
                case TextureFormat.RGBA8_UNORM: return VkFormat.VK_FORMAT_R8G8B8A8_UNORM;
                case TextureFormat.RGBA8_SRGB: return VkFormat.VK_FORMAT_R8G8B8A8_SRGB;
                case TextureFormat.RGBA16_FLOAT: return VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT;
                case TextureFormat.RGBA32_FLOAT: return VkFormat.VK_FORMAT_R32G32B32A32_SFLOAT;
                case TextureFormat.D24_UNORM_S8_UINT: return VkFormat.VK_FORMAT_D24_UNORM_S8_UINT;
                case TextureFormat.D32_FLOAT: return VkFormat.VK_FORMAT_D32_SFLOAT;
                case TextureFormat.D32_FLOAT_S8_UINT: return VkFormat.VK_FORMAT_D32_SFLOAT_S8_UINT;
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

        private uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
        {
            // TODO: Query physical device memory properties and find suitable type
            // For now, return a default - real implementation would query VkPhysicalDeviceMemoryProperties
            return 0; // Assume type 0 is suitable
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

            // Convert BufferUsage to VkBufferUsageFlags
            VkBufferUsageFlags usageFlags = 0;

            if ((desc.Usage & BufferUsage.Vertex) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_VERTEX_BUFFER_BIT;
            }
            if ((desc.Usage & BufferUsage.Index) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_INDEX_BUFFER_BIT;
            }
            if ((desc.Usage & BufferUsage.Constant) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT;
            }
            if ((desc.Usage & BufferUsage.Shader) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_STORAGE_BUFFER_BIT;
            }
            if ((desc.Usage & BufferUsage.Indirect) != 0)
            {
                usageFlags |= VkBufferUsageFlags.VK_BUFFER_USAGE_INDIRECT_BUFFER_BIT;
            }

            // Add transfer flags for staging if needed
            if ((desc.Usage & (BufferUsage.Vertex | BufferUsage.Index | BufferUsage.Constant | BufferUsage.Shader)) != 0)
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
            VkMemoryPropertyFlags memoryProperties = VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            if ((desc.Usage & BufferUsage.Staging) != 0)
            {
                memoryProperties = VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT |
                                  VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT;
            }

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
                mipLodBias = desc.MipLODBias,
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

            // For now, we'll create a basic pipeline without render pass (assuming VK_KHR_dynamic_rendering)
            // TODO: Full implementation would create VkRenderPass from framebuffer

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var pipeline = new VulkanGraphicsPipeline(handle, desc, IntPtr.Zero, pipelineLayout, _device);
            _resources[handle] = pipeline;

            return pipeline;
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
                        // TODO: Extract VkDescriptorSetLayout from VulkanBindingLayout
                        descriptorSetLayouts[i] = IntPtr.Zero; // Placeholder
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
                    
                    attachments.Add(new VkAttachmentDescription
                    {
                        flags = 0,
                        format = depthFormat,
                        samples = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT, // TODO: Get from texture desc
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
                    stageFlags = VkShaderStageFlags.VK_SHADER_STAGE_ALL, // TODO: Convert from item.ShaderStages
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
            // We need to access the private field, so we'll use reflection or add a property
            // For now, we'll assume VulkanBindingLayout has a way to get the layout handle
            IntPtr vkDescriptorSetLayout = GetDescriptorSetLayoutHandle(vulkanLayout);
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
        /// Gets the VkDescriptorSetLayout handle from a VulkanBindingLayout.
        /// Uses reflection to access the private field since we can't modify the class.
        /// </summary>
        private IntPtr GetDescriptorSetLayoutHandle(VulkanBindingLayout layout)
        {
            // Use reflection to get the private _vkDescriptorSetLayout field
            System.Reflection.FieldInfo field = typeof(VulkanBindingLayout).GetField("_vkDescriptorSetLayout", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                return (IntPtr)field.GetValue(layout);
            }

            // Fallback: try to get it from the layout's Desc if available
            // This is a workaround - ideally VulkanBindingLayout would expose this
            Console.WriteLine("[VulkanDevice] Warning: Could not access VkDescriptorSetLayout handle from VulkanBindingLayout");
            return IntPtr.Zero;
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
                            if (item.AccelStruct != null)
                            {
                                // Get acceleration structure handle
                                IntPtr accelStructHandle = GetAccelStructHandle(item.AccelStruct);
                                if (accelStructHandle != IntPtr.Zero)
                                {
                                    // Acceleration structures are written using a chained structure
                                    // VkWriteDescriptorSetAccelerationStructureKHR
                                    // For now, we'll set up the write descriptor set with the acceleration structure type
                                    // The actual acceleration structure handle would be set via pNext chain
                                    // This requires VK_KHR_acceleration_structure extension structures
                                    
                                    // Note: Full implementation would require:
                                    // 1. VkWriteDescriptorSetAccelerationStructureKHR structure
                                    // 2. Chain it via pNext in VkWriteDescriptorSet
                                    // 3. Set descriptorType to VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR
                                    
                                    writeDescriptorSet.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;
                                    
                                    // TODO: Add VkWriteDescriptorSetAccelerationStructureKHR support when extension is available
                                    // For now, we log that acceleration structure binding is partially implemented
                                    Console.WriteLine($"[VulkanDevice] Acceleration structure binding for slot {item.Slot} - requires VK_KHR_acceleration_structure extension structures");
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

            // TODO: Allocate VkCommandBuffer from command pool
            // For now, we'll create a placeholder command buffer handle
            IntPtr vkCommandBuffer = new IntPtr(_nextResourceHandle++); // Placeholder

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var commandList = new VulkanCommandList(handle, type, this, vkCommandBuffer, commandPool, _device);
            _resources[handle] = commandList;

            return commandList;
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

            // TODO: Full implementation requires VK_KHR_acceleration_structure extension
            // For now, create placeholder with basic structure

            // Allocate buffer for acceleration structure storage
            ulong bufferSize = desc.IsTopLevel ? 1024UL : 4096UL; // Placeholder sizes
            var bufferDesc = new BufferDesc
            {
                ByteSize = (int)bufferSize,
                Usage = BufferUsage.Shader // Acceleration structures need shader access
            };

            IBuffer accelBuffer = CreateBuffer(bufferDesc);

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var accelStruct = new VulkanAccelStruct(handle, desc, IntPtr.Zero, accelBuffer, 0UL, _device);
            _resources[handle] = accelStruct;

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

            // Full implementation of VK_KHR_ray_tracing_pipeline extension

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
                            // For now, we assume it's available if raytracing is supported
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
                                    Usage = BufferUsage.Shader
                                };
                                IBuffer sbtBuffer = CreateBuffer(sbtBufferDesc);

                                // Get shader group handles and populate SBT
                                // Note: SBT population typically happens at dispatch time or requires buffer mapping
                                // For now, we create the buffer - full SBT population would require vkGetRayTracingShaderGroupHandlesKHR
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
            // For now, this is a placeholder
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

            // TODO: Extract VkFence from IFence implementation and call vkWaitForFences
            // For now, this is a placeholder
            throw new NotImplementedException("Fence waiting not implemented - requires IFence implementation with VkFence access");
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

            // TODO: Query physical device format properties
            // For now, assume common formats are supported for common usages
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

            public VulkanBindingSet(IntPtr handle, IBindingLayout layout, BindingSetDesc desc)
            {
                _handle = handle;
                Layout = layout;
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

            // All ICommandList methods require full implementation
            // These are stubbed with TODO comments indicating Vulkan API calls needed
            // Implementation will be completed when Vulkan interop is added

            public void WriteBuffer(IBuffer buffer, byte[] data, int destOffset = 0) { /* TODO: vkCmdUpdateBuffer or staging buffer */ }
            public void WriteBuffer<T>(IBuffer buffer, T[] data, int destOffset = 0) where T : unmanaged { /* TODO: vkCmdUpdateBuffer or staging buffer */ }
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
            // Simplified version - real implementation would handle all formats
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

            // Helper method to find memory type index
            // This is a simplified version - real implementation would query physical device memory properties
            private uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
            {
                // Simplified: return first matching type
                // Real implementation would iterate through VkPhysicalDeviceMemoryProperties.memoryTypes
                // and find the first type that matches both typeFilter and properties
                // For now, return 0 as a placeholder (this would need proper memory type querying)
                return 0;
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
            public void CopyBuffer(IBuffer dest, int destOffset, IBuffer src, int srcOffset, int size) { /* TODO: vkCmdCopyBuffer */ }
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
            private VkImageAspectFlags GetImageAspectFlags(TextureFormat format)
            {
                // Determine if format is depth/stencil or color
                // This is a simplified version - full implementation would check all format types
                switch (format)
                {
                    case TextureFormat.D24_UNORM_S8_UINT:
                    case TextureFormat.D32_FLOAT:
                    case TextureFormat.D32_FLOAT_S8X24_UINT:
                        return VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT | VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT;
                    default:
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
            public void ClearColorAttachment(IFramebuffer framebuffer, int attachmentIndex, Vector4 color) { /* TODO: vkCmdClearColorImage */ }
            public void ClearDepthStencilAttachment(IFramebuffer framebuffer, float depth, byte stencil, bool clearDepth = true, bool clearStencil = true) { /* TODO: vkCmdClearDepthStencilImage */ }
            public void ClearUAVFloat(ITexture texture, Vector4 value) { /* TODO: vkCmdFillBuffer or compute shader */ }
            public void ClearUAVUint(ITexture texture, uint value) { /* TODO: vkCmdFillBuffer or compute shader */ }
            public void SetTextureState(ITexture texture, ResourceState state)
            {
                // TODO: STUB - Implement texture state transitions with vkCmdPipelineBarrier (VkImageMemoryBarrier)
                // Texture barriers require image layout transitions which are more complex than buffer barriers
                // This is left as a stub for future implementation
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

            public void UAVBarrier(ITexture texture) { /* TODO: vkCmdMemoryBarrier */ }
            public void UAVBarrier(IBuffer buffer) { /* TODO: vkCmdMemoryBarrier */ }
            public void SetGraphicsState(GraphicsState state) { /* TODO: Set all graphics state */ }
            public void SetViewport(Viewport viewport) { /* TODO: vkCmdSetViewport */ }
            public void SetViewports(Viewport[] viewports) { /* TODO: vkCmdSetViewport */ }
            public void SetScissor(Rectangle scissor) { /* TODO: vkCmdSetScissor */ }
            public void SetScissors(Rectangle[] scissors) { /* TODO: vkCmdSetScissor */ }
            public void SetBlendConstant(Vector4 color) { /* TODO: vkCmdSetBlendConstants */ }
            public void SetStencilRef(uint reference) { /* TODO: vkCmdSetStencilReference */ }
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
            public void DrawIndexed(DrawArguments args) { /* TODO: vkCmdDrawIndexed */ }
            public void DrawIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride) { /* TODO: vkCmdDrawIndirect */ }
            public void DrawIndexedIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride) { /* TODO: vkCmdDrawIndexedIndirect */ }
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

                // Note: In a full implementation with Vulkan interop, this method would:
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
            public void SetRaytracingState(RaytracingState state) { /* TODO: Set raytracing state */ }
            public void DispatchRays(DispatchRaysArguments args) { /* TODO: vkCmdTraceRaysKHR */ }
            public void BuildBottomLevelAccelStruct(IAccelStruct accelStruct, GeometryDesc[] geometries) { /* TODO: vkCmdBuildAccelerationStructuresKHR */ }
            public void BuildTopLevelAccelStruct(IAccelStruct accelStruct, AccelStructInstance[] instances) { /* TODO: vkCmdBuildAccelerationStructuresKHR */ }
            public void CompactBottomLevelAccelStruct(IAccelStruct dest, IAccelStruct src) { /* TODO: vkCmdCopyAccelerationStructureKHR */ }
            public void BeginDebugEvent(string name, Vector4 color)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before beginning debug event");
                }

                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Debug event name cannot be null or empty", nameof(name));
                }

                // Pin the string for native interop
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name + "\0");
                GCHandle nameHandle = GCHandle.Alloc(nameBytes, GCHandleType.Pinned);
                try
                {
                    // Create debug label structure
                    float[] colorArray = new float[4] { color.X, color.Y, color.Z, color.W };
                    VkDebugUtilsLabelEXT labelInfo = new VkDebugUtilsLabelEXT
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT,
                        pNext = IntPtr.Zero,
                        pLabelName = nameHandle.AddrOfPinnedObject(),
                        color = colorArray
                    };

                    // Call Vulkan function if available
                    if (vkCmdBeginDebugUtilsLabelEXT != null)
                    {
                        vkCmdBeginDebugUtilsLabelEXT(_vkCommandBuffer, ref labelInfo);
                    }
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

                // Call Vulkan function if available
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

                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Debug marker name cannot be null or empty", nameof(name));
                }

                // Pin the string for native interop
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name + "\0");
                GCHandle nameHandle = GCHandle.Alloc(nameBytes, GCHandleType.Pinned);
                try
                {
                    // Create debug label structure
                    float[] colorArray = new float[4] { color.X, color.Y, color.Z, color.W };
                    VkDebugUtilsLabelEXT labelInfo = new VkDebugUtilsLabelEXT
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT,
                        pNext = IntPtr.Zero,
                        pLabelName = nameHandle.AddrOfPinnedObject(),
                        color = colorArray
                    };

                    // Call Vulkan function if available
                    if (vkCmdInsertDebugUtilsLabelEXT != null)
                    {
                        vkCmdInsertDebugUtilsLabelEXT(_vkCommandBuffer, ref labelInfo);
                    }
                }
                finally
                {
                    nameHandle.Free();
                }
            }

            public void Dispose()
            {
                if (_vkCommandBuffer != IntPtr.Zero && _vkCommandPool != IntPtr.Zero && _vkDevice != IntPtr.Zero)
                {
                    vkFreeCommandBuffers(_vkDevice, _vkCommandPool, 1, ref _vkCommandBuffer);
                }
            }
        }

        #endregion
    }
}





        #endregion
    }
}



    }
}



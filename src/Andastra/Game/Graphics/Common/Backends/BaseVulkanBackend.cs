using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Game.Graphics.Common.Backends
{
    /// <summary>
    /// Abstract base class for Vulkan backend implementations.
    ///
    /// Provides shared Vulkan logic that can be inherited by both
    /// MonoGame and Stride implementations.
    ///
    /// Features:
    /// - Vulkan 1.2+ rendering
    /// - VK_KHR_ray_tracing_pipeline support
    /// - VK_KHR_acceleration_structure support
    /// - Multi-GPU support
    /// - Async compute and transfer queues
    /// - Bindless resources (VK_EXT_descriptor_indexing)
    /// - Dynamic rendering (VK_KHR_dynamic_rendering)
    ///
    /// Based on Vulkan API: https://www.khronos.org/vulkan/
    /// </summary>
    /// <remarks>
    /// Vulkan Backend:
    /// - This is a modern graphics API abstraction (Vulkan was released after KOTOR2)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game did not use Vulkan; this is a modern enhancement for cross-platform support and advanced features
    /// - Raytracing: Original game did not support raytracing; this is a modern enhancement
    /// - This abstraction: Provides Vulkan backend for modern systems, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public abstract class BaseVulkanBackend : BaseGraphicsBackend, IComputeBackend, IRaytracingBackend
    {
        protected IntPtr _instance;
        protected IntPtr _physicalDevice;
        protected IntPtr _device;
        protected IntPtr _graphicsQueue;
        protected IntPtr _computeQueue;
        protected IntPtr _transferQueue;
        protected IntPtr _swapchain;

        protected bool _raytracingEnabled;
        protected RaytracingLevel _raytracingLevel;

        public override GraphicsBackendType BackendType => GraphicsBackendType.Vulkan;
        public override bool IsRaytracingEnabled => _raytracingEnabled;

        #region Template Method Implementations

        protected override void InitializeCapabilities()
        {
            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 16384,
                MaxRenderTargets = 8,
                MaxAnisotropy = 16,
                SupportsComputeShaders = true,
                SupportsGeometryShaders = true,
                SupportsTessellation = true,
                SupportsRaytracing = QueryRaytracingSupport(),
                SupportsMeshShaders = QueryMeshShaderSupport(),
                SupportsVariableRateShading = QueryVrsSupport(),
                DedicatedVideoMemory = QueryVideoMemory(),
                SharedSystemMemory = 16L * 1024 * 1024 * 1024,
                VendorName = QueryVendorName(),
                DeviceName = QueryDeviceName(),
                DriverVersion = QueryDriverVersion(),
                ActiveBackend = GraphicsBackendType.Vulkan,
                ShaderModelVersion = 6.6f, // SPIR-V equivalent
                RemixAvailable = false,
                DlssAvailable = QueryDlssSupport(),
                FsrAvailable = true
            };
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            if (_capabilities.SupportsRaytracing && _settings.Raytracing != RaytracingLevel.Disabled)
            {
                InitializeRaytracing();
            }
        }

        #endregion

        #region IComputeBackend Implementation

        public virtual void Dispatch(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ)
        {
            if (!_initialized) return;
            OnDispatch(threadGroupCountX, threadGroupCountY, threadGroupCountZ);
        }

        public virtual IntPtr CreateStructuredBuffer(int elementCount, int elementStride, bool cpuWritable)
        {
            if (!_initialized) return IntPtr.Zero;

            var handle = AllocateHandle();
            var resource = CreateStructuredBufferInternal(elementCount, elementStride, cpuWritable, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public abstract IntPtr MapBuffer(IntPtr bufferHandle, MapType mapType);
        public abstract void UnmapBuffer(IntPtr bufferHandle);

        #endregion

        #region IRaytracingBackend Implementation

        public override void SetRaytracingLevel(RaytracingLevel level)
        {
            if (!_capabilities.SupportsRaytracing)
            {
                if (level != RaytracingLevel.Disabled)
                {
                    Console.WriteLine($"[{BackendType}] Hardware does not support raytracing");
                }
                return;
            }

            _raytracingLevel = level;
            _raytracingEnabled = level != RaytracingLevel.Disabled;

            Console.WriteLine($"[{BackendType}] Raytracing level set to: {level}");
        }

        public virtual IntPtr CreateBlas(MeshGeometry geometry)
        {
            if (!_raytracingEnabled) return IntPtr.Zero;

            var handle = AllocateHandle();
            var resource = CreateBlasInternal(geometry, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual IntPtr CreateTlas(int maxInstances)
        {
            if (!_raytracingEnabled) return IntPtr.Zero;

            var handle = AllocateHandle();
            var resource = CreateTlasInternal(maxInstances, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual IntPtr CreateRaytracingPso(RaytracingPipelineDesc desc)
        {
            if (!_raytracingEnabled) return IntPtr.Zero;

            var handle = AllocateHandle();
            var resource = CreateRaytracingPsoInternal(desc, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual void DispatchRays(DispatchRaysDesc desc)
        {
            if (!_raytracingEnabled) return;
            OnDispatchRays(desc);
        }

        public virtual void UpdateTlasInstance(IntPtr tlas, int instanceIndex, System.Numerics.Matrix4x4 transform)
        {
            if (!_raytracingEnabled) return;
            OnUpdateTlasInstance(tlas, instanceIndex, transform);
        }

        #endregion

        #region Vulkan Specific Methods

        /// <summary>
        /// Submits a command buffer to a queue.
        /// Based on Vulkan API: vkQueueSubmit
        /// </summary>
        public virtual void QueueSubmit(QueueType queue, IntPtr commandBuffer)
        {
            if (!_initialized) return;
            OnQueueSubmit(queue, commandBuffer);
        }

        /// <summary>
        /// Waits for a queue to become idle.
        /// Based on Vulkan API: vkQueueWaitIdle
        /// </summary>
        public virtual void QueueWaitIdle(QueueType queue)
        {
            if (!_initialized) return;
            OnQueueWaitIdle(queue);
        }

        /// <summary>
        /// Inserts a pipeline barrier.
        /// Based on Vulkan API: vkCmdPipelineBarrier
        /// </summary>
        public virtual void PipelineBarrier(IntPtr commandBuffer, VkPipelineStage srcStage, VkPipelineStage dstStage)
        {
            if (!_initialized) return;
            OnPipelineBarrier(commandBuffer, srcStage, dstStage);
        }

        /// <summary>
        /// Begins dynamic rendering pass.
        /// Based on Vulkan API: vkCmdBeginRendering (VK_KHR_dynamic_rendering)
        /// </summary>
        public virtual void BeginDynamicRendering(DynamicRenderingInfo info)
        {
            if (!_initialized) return;
            OnBeginDynamicRendering(info);
        }

        /// <summary>
        /// Ends dynamic rendering pass.
        /// Based on Vulkan API: vkCmdEndRendering
        /// </summary>
        public virtual void EndDynamicRendering()
        {
            if (!_initialized) return;
            OnEndDynamicRendering();
        }

        #endregion

        #region Abstract Methods for Derived Classes

        protected abstract void InitializeRaytracing();
        protected abstract void OnDispatch(int x, int y, int z);
        protected abstract void OnDispatchRays(DispatchRaysDesc desc);
        protected abstract void OnUpdateTlasInstance(IntPtr tlas, int instanceIndex, System.Numerics.Matrix4x4 transform);
        protected abstract void OnQueueSubmit(QueueType queue, IntPtr commandBuffer);
        protected abstract void OnQueueWaitIdle(QueueType queue);
        protected abstract void OnPipelineBarrier(IntPtr commandBuffer, VkPipelineStage srcStage, VkPipelineStage dstStage);
        protected abstract void OnBeginDynamicRendering(DynamicRenderingInfo info);
        protected abstract void OnEndDynamicRendering();

        protected abstract ResourceInfo CreateStructuredBufferInternal(int elementCount, int elementStride,
            bool cpuWritable, IntPtr handle);
        protected abstract ResourceInfo CreateBlasInternal(MeshGeometry geometry, IntPtr handle);
        protected abstract ResourceInfo CreateTlasInternal(int maxInstances, IntPtr handle);
        protected abstract ResourceInfo CreateRaytracingPsoInternal(RaytracingPipelineDesc desc, IntPtr handle);

        #endregion

        #region Capability Queries

        protected virtual bool QueryRaytracingSupport() => true;
        protected virtual bool QueryMeshShaderSupport() => true;
        protected virtual bool QueryVrsSupport() => true;
        protected virtual bool QueryDlssSupport() => true;
        protected virtual long QueryVideoMemory() => 8L * 1024 * 1024 * 1024;
        protected virtual string QueryVendorName() => "Unknown";
        protected virtual string QueryDeviceName() => "Vulkan Device";
        protected virtual string QueryDriverVersion() => "Unknown";

        #endregion
    }

    /// <summary>
    /// Vulkan queue types.
    /// </summary>
    public enum QueueType
    {
        Graphics,
        Compute,
        Transfer
    }

    /// <summary>
    /// Vulkan pipeline stages.
    /// Based on VkPipelineStageFlagBits
    /// </summary>
    [Flags]
    public enum VkPipelineStage : uint
    {
        TopOfPipe = 0x00000001,
        DrawIndirect = 0x00000002,
        VertexInput = 0x00000004,
        VertexShader = 0x00000008,
        TessellationControl = 0x00000010,
        TessellationEvaluation = 0x00000020,
        GeometryShader = 0x00000040,
        FragmentShader = 0x00000080,
        EarlyFragmentTests = 0x00000100,
        LateFragmentTests = 0x00000200,
        ColorAttachmentOutput = 0x00000400,
        ComputeShader = 0x00000800,
        Transfer = 0x00001000,
        BottomOfPipe = 0x00002000,
        Host = 0x00004000,
        AllGraphics = 0x00008000,
        AllCommands = 0x00010000,
        RayTracingShader = 0x00200000,
        AccelerationStructureBuild = 0x02000000
    }

    /// <summary>
    /// Dynamic rendering info for VK_KHR_dynamic_rendering.
    /// </summary>
    public struct DynamicRenderingInfo
    {
        public int ViewMask;
        public int ColorAttachmentCount;
        public IntPtr ColorAttachments;
        public IntPtr DepthAttachment;
        public IntPtr StencilAttachment;
        public int RenderAreaX;
        public int RenderAreaY;
        public int RenderAreaWidth;
        public int RenderAreaHeight;
        public int LayerCount;
    }
}


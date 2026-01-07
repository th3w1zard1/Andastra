using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Backends
{
    /// <summary>
    /// Abstract base class for DirectX 12 backend implementations.
    /// 
    /// Provides shared D3D12 logic that can be inherited by both
    /// MonoGame and Stride implementations.
    /// 
    /// Features:
    /// - DirectX 12 Ultimate features
    /// - DXR 1.1 raytracing
    /// - Mesh shaders
    /// - Variable rate shading
    /// - DirectStorage support
    /// - Bindless resources
    /// - GPU-driven rendering
    /// 
    /// Based on D3D12 API: https://docs.microsoft.com/en-us/windows/win32/direct3d12/
    /// </summary>
    /// <remarks>
    /// DirectX 12 Backend:
    /// - This is a modern graphics API abstraction (DirectX 12 was released after KOTOR2)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game did not use DirectX 12; this is a modern enhancement for raytracing and advanced features
    /// - Raytracing: Original game did not support raytracing; this is a modern enhancement
    /// - This abstraction: Provides DirectX 12 backend for modern Windows systems, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public abstract class BaseDirect3D12Backend : BaseGraphicsBackend, IComputeBackend, IRaytracingBackend,
        IMeshShaderBackend, IVariableRateShadingBackend, ISamplerFeedbackBackend, IBindlessResourcesBackend
    {
        protected IntPtr _factory;
        protected IntPtr _adapter;
        protected IntPtr _device;
        protected IntPtr _commandQueue;
        protected IntPtr _commandAllocator;
        protected IntPtr _commandList;
        protected IntPtr _swapChain;
        protected IntPtr _rtvHeap;
        protected IntPtr _dsvHeap;
        protected IntPtr _srvHeap;
        protected IntPtr _raytracingDevice;

        protected bool _raytracingEnabled;
        protected RaytracingLevel _raytracingLevel;

        public override GraphicsBackendType BackendType => GraphicsBackendType.Direct3D12;
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
                ActiveBackend = GraphicsBackendType.Direct3D12,
                ShaderModelVersion = 6.6f, // SM 6.6 for DX12 Ultimate
                RemixAvailable = false,
                DlssAvailable = QueryDlssSupport(),
                FsrAvailable = true
            };
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            // Initialize DXR if available and requested
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

        /// <summary>
        /// Creates a bottom-level acceleration structure for raytracing.
        /// Based on DXR API: BuildRaytracingAccelerationStructure
        /// </summary>
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

        /// <summary>
        /// Creates a top-level acceleration structure.
        /// Based on DXR API: BuildRaytracingAccelerationStructure
        /// </summary>
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

        /// <summary>
        /// Creates a raytracing pipeline state object.
        /// Based on DXR API: CreateStateObject
        /// </summary>
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

        /// <summary>
        /// Dispatches raytracing work.
        /// Based on DXR API: DispatchRays
        /// </summary>
        public virtual void DispatchRays(DispatchRaysDesc desc)
        {
            if (!_raytracingEnabled) return;
            OnDispatchRays(desc);
        }

        /// <summary>
        /// Updates an instance transform in the TLAS.
        /// </summary>
        public virtual void UpdateTlasInstance(IntPtr tlas, int instanceIndex, System.Numerics.Matrix4x4 transform)
        {
            if (!_raytracingEnabled) return;
            OnUpdateTlasInstance(tlas, instanceIndex, transform);
        }

        #endregion

        #region D3D12 Specific Methods

        /// <summary>
        /// Executes a command list.
        /// Based on D3D12 API: ExecuteCommandLists
        /// </summary>
        public virtual void ExecuteCommandList()
        {
            if (!_initialized) return;
            OnExecuteCommandList();
        }

        /// <summary>
        /// Resets the command allocator and list.
        /// </summary>
        public virtual void ResetCommandList()
        {
            if (!_initialized) return;
            OnResetCommandList();
        }

        /// <summary>
        /// Inserts a resource barrier.
        /// Based on D3D12 API: ResourceBarrier
        /// </summary>
        public virtual void ResourceBarrier(IntPtr resource, ResourceState before, ResourceState after)
        {
            if (!_initialized) return;
            OnResourceBarrier(resource, before, after);
        }

        /// <summary>
        /// Waits for GPU to complete pending work.
        /// </summary>
        public virtual void WaitForGpu()
        {
            if (!_initialized) return;
            OnWaitForGpu();
        }

        #endregion

        #region Abstract Methods for Derived Classes

        protected abstract void InitializeRaytracing();
        protected abstract void OnDispatch(int x, int y, int z);
        protected abstract void OnDispatchRays(DispatchRaysDesc desc);
        protected abstract void OnUpdateTlasInstance(IntPtr tlas, int instanceIndex, System.Numerics.Matrix4x4 transform);
        protected abstract void OnExecuteCommandList();
        protected abstract void OnResetCommandList();
        protected abstract void OnResourceBarrier(IntPtr resource, ResourceState before, ResourceState after);
        protected abstract void OnWaitForGpu();

        protected abstract ResourceInfo CreateStructuredBufferInternal(int elementCount, int elementStride,
            bool cpuWritable, IntPtr handle);
        protected abstract ResourceInfo CreateBlasInternal(MeshGeometry geometry, IntPtr handle);
        protected abstract ResourceInfo CreateTlasInternal(int maxInstances, IntPtr handle);
        protected abstract ResourceInfo CreateRaytracingPsoInternal(RaytracingPipelineDesc desc, IntPtr handle);

        #endregion

        #region IMeshShaderBackend Implementation

        public virtual bool MeshShadersAvailable => _capabilities.SupportsMeshShaders;

        public virtual IntPtr CreateMeshShaderPipeline(byte[] amplificationShader, byte[] meshShader,
            byte[] pixelShader, MeshPipelineDescription desc)
        {
            if (!MeshShadersAvailable || meshShader == null || pixelShader == null)
            {
                return IntPtr.Zero;
            }

            var handle = AllocateHandle();
            var resource = CreateMeshShaderPipelineInternal(amplificationShader, meshShader, pixelShader, desc, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual void DispatchMesh(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ)
        {
            if (!MeshShadersAvailable || !_initialized) return;
            OnDispatchMesh(threadGroupCountX, threadGroupCountY, threadGroupCountZ);
        }

        public virtual void DispatchMeshIndirect(IntPtr indirectBuffer, int offset)
        {
            if (!MeshShadersAvailable || !_initialized) return;
            OnDispatchMeshIndirect(indirectBuffer, offset);
        }

        protected abstract ResourceInfo CreateMeshShaderPipelineInternal(byte[] amplificationShader, byte[] meshShader,
            byte[] pixelShader, MeshPipelineDescription desc, IntPtr handle);
        protected abstract void OnDispatchMesh(int x, int y, int z);
        protected abstract void OnDispatchMeshIndirect(IntPtr indirectBuffer, int offset);

        #endregion

        #region IVariableRateShadingBackend Implementation

        public virtual bool VariableRateShadingAvailable => _capabilities.SupportsVariableRateShading;
        public virtual int VrsTier => QueryVrsTier();

        public virtual void SetShadingRate(VrsShadingRate rate)
        {
            if (!VariableRateShadingAvailable || !_initialized) return;
            OnSetShadingRate(rate);
        }

        public virtual void SetShadingRateCombiner(VrsCombiner combiner0, VrsCombiner combiner1, VrsShadingRate rate)
        {
            if (!VariableRateShadingAvailable || VrsTier < 1 || !_initialized) return;
            OnSetShadingRateCombiner(combiner0, combiner1, rate);
        }

        public virtual void SetPerPrimitiveShadingRate(bool enable)
        {
            if (!VariableRateShadingAvailable || VrsTier < 1 || !_initialized) return;
            OnSetPerPrimitiveShadingRate(enable);
        }

        public virtual void SetShadingRateImage(IntPtr shadingRateImage, int width, int height)
        {
            if (!VariableRateShadingAvailable || VrsTier < 2 || !_initialized) return;
            OnSetShadingRateImage(shadingRateImage, width, height);
        }

        protected abstract void OnSetShadingRate(VrsShadingRate rate);
        protected abstract void OnSetShadingRateCombiner(VrsCombiner combiner0, VrsCombiner combiner1, VrsShadingRate rate);
        protected abstract void OnSetPerPrimitiveShadingRate(bool enable);
        protected abstract void OnSetShadingRateImage(IntPtr shadingRateImage, int width, int height);
        protected virtual int QueryVrsTier() => VariableRateShadingAvailable ? 2 : 0;

        #endregion

        #region ISamplerFeedbackBackend Implementation

        public virtual bool SamplerFeedbackAvailable => QuerySamplerFeedbackSupport();

        public virtual IntPtr CreateSamplerFeedbackTexture(int width, int height, TextureFormat format)
        {
            if (!SamplerFeedbackAvailable || !_initialized) return IntPtr.Zero;
            var handle = AllocateHandle();
            var resource = CreateSamplerFeedbackTextureInternal(width, height, format, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual void SetSamplerFeedbackTexture(IntPtr feedbackTexture, int slot)
        {
            if (!SamplerFeedbackAvailable || !_initialized) return;
            OnSetSamplerFeedbackTexture(feedbackTexture, slot);
        }

        public virtual void ReadSamplerFeedback(IntPtr feedbackTexture, byte[] data, int sizeInBytes)
        {
            if (!SamplerFeedbackAvailable || !_initialized) return;
            OnReadSamplerFeedback(feedbackTexture, data, sizeInBytes);
        }

        protected abstract ResourceInfo CreateSamplerFeedbackTextureInternal(int width, int height, TextureFormat format, IntPtr handle);
        protected abstract void OnSetSamplerFeedbackTexture(IntPtr feedbackTexture, int slot);
        protected abstract void OnReadSamplerFeedback(IntPtr feedbackTexture, byte[] data, int sizeInBytes);
        protected virtual bool QuerySamplerFeedbackSupport() => true;

        #endregion

        #region IBindlessResourcesBackend Implementation

        public virtual bool BindlessResourcesAvailable => QueryBindlessResourcesSupport();
        public virtual int MaxBindlessResources => 1000000; // D3D12 default limit

        public virtual IntPtr CreateBindlessTextureHeap(int capacity)
        {
            if (!BindlessResourcesAvailable || !_initialized) return IntPtr.Zero;
            var handle = AllocateHandle();
            var resource = CreateBindlessTextureHeapInternal(capacity, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual IntPtr CreateBindlessSamplerHeap(int capacity)
        {
            if (!BindlessResourcesAvailable || !_initialized) return IntPtr.Zero;
            var handle = AllocateHandle();
            var resource = CreateBindlessSamplerHeapInternal(capacity, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual int AddBindlessTexture(IntPtr heap, IntPtr texture)
        {
            if (!BindlessResourcesAvailable || !_initialized) return -1;
            return OnAddBindlessTexture(heap, texture);
        }

        public virtual int AddBindlessSampler(IntPtr heap, IntPtr sampler)
        {
            if (!BindlessResourcesAvailable || !_initialized) return -1;
            return OnAddBindlessSampler(heap, sampler);
        }

        public virtual void RemoveBindlessTexture(IntPtr heap, int index)
        {
            if (!BindlessResourcesAvailable || !_initialized) return;
            OnRemoveBindlessTexture(heap, index);
        }

        public virtual void RemoveBindlessSampler(IntPtr heap, int index)
        {
            if (!BindlessResourcesAvailable || !_initialized) return;
            OnRemoveBindlessSampler(heap, index);
        }

        public virtual void SetBindlessHeap(IntPtr heap, int slot, ShaderStage stage)
        {
            if (!BindlessResourcesAvailable || !_initialized) return;
            OnSetBindlessHeap(heap, slot, stage);
        }

        protected abstract ResourceInfo CreateBindlessTextureHeapInternal(int capacity, IntPtr handle);
        protected abstract ResourceInfo CreateBindlessSamplerHeapInternal(int capacity, IntPtr handle);
        protected abstract int OnAddBindlessTexture(IntPtr heap, IntPtr texture);
        protected abstract int OnAddBindlessSampler(IntPtr heap, IntPtr sampler);
        protected abstract void OnRemoveBindlessTexture(IntPtr heap, int index);
        protected abstract void OnRemoveBindlessSampler(IntPtr heap, int index);
        protected abstract void OnSetBindlessHeap(IntPtr heap, int slot, ShaderStage stage);
        protected virtual bool QueryBindlessResourcesSupport() => true;

        #endregion

        #region Capability Queries

        protected virtual bool QueryRaytracingSupport() => true;
        protected virtual bool QueryMeshShaderSupport() => true;
        protected virtual bool QueryVrsSupport() => true;
        protected virtual bool QueryDlssSupport() => true;
        protected virtual long QueryVideoMemory() => 8L * 1024 * 1024 * 1024;
        protected virtual string QueryVendorName() => "Unknown";
        protected virtual string QueryDeviceName() => "DirectX 12 Device";
        protected virtual string QueryDriverVersion() => "Unknown";

        #endregion
    }

    /// <summary>
    /// D3D12 resource states for barriers.
    /// </summary>
    public enum ResourceState
    {
        Common = 0,
        VertexAndConstantBuffer = 1,
        IndexBuffer = 2,
        RenderTarget = 4,
        UnorderedAccess = 8,
        DepthWrite = 16,
        DepthRead = 32,
        NonPixelShaderResource = 64,
        PixelShaderResource = 128,
        StreamOut = 256,
        IndirectArgument = 512,
        CopyDest = 1024,
        CopySource = 2048,
        ResolveDest = 4096,
        ResolveSource = 8192,
        RaytracingAccelerationStructure = 4194304,
        GenericRead = 2755
    }
}


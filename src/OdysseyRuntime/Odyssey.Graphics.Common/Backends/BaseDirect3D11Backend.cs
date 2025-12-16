using System;
using Odyssey.Graphics.Common.Enums;
using Odyssey.Graphics.Common.Interfaces;
using Odyssey.Graphics.Common.Structs;

namespace Odyssey.Graphics.Common.Backends
{
    /// <summary>
    /// Abstract base class for DirectX 11 backend implementations.
    ///
    /// Provides shared D3D11 logic that can be inherited by both
    /// MonoGame and Stride implementations.
    ///
    /// Features:
    /// - DirectX 11 rendering (Windows 7+)
    /// - Shader Model 5.0/5.1 support
    /// - Compute shaders
    /// - Tessellation (hull and domain shaders)
    /// - Multi-threaded resource creation
    /// - Feature level fallback (11_1, 11_0, 10_1, 10_0)
    ///
    /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/direct3d11/
    /// </summary>
    public abstract class BaseDirect3D11Backend : BaseGraphicsBackend, IComputeBackend
    {
        protected IntPtr _factory;
        protected IntPtr _adapter;
        protected IntPtr _device;
        protected IntPtr _immediateContext;
        protected IntPtr _swapChain;
        protected IntPtr _renderTargetView;
        protected IntPtr _depthStencilView;
        protected IntPtr _deferredContext;
        protected D3D11FeatureLevel _featureLevel;

        public override GraphicsBackendType BackendType => GraphicsBackendType.Direct3D11;

        /// <summary>
        /// Gets the current D3D11 feature level.
        /// </summary>
        public D3D11FeatureLevel FeatureLevel => _featureLevel;

        #region Template Method Implementations

        protected override void InitializeCapabilities()
        {
            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 16384,
                MaxRenderTargets = 8,
                MaxAnisotropy = 16,
                SupportsComputeShaders = _featureLevel >= D3D11FeatureLevel.Level_11_0,
                SupportsGeometryShaders = _featureLevel >= D3D11FeatureLevel.Level_10_0,
                SupportsTessellation = _featureLevel >= D3D11FeatureLevel.Level_11_0,
                SupportsRaytracing = false, // DX11 does not support DXR
                SupportsMeshShaders = false, // Requires DX12
                SupportsVariableRateShading = false, // Requires DX12
                DedicatedVideoMemory = QueryVideoMemory(),
                SharedSystemMemory = 8L * 1024 * 1024 * 1024,
                VendorName = QueryVendorName(),
                DeviceName = QueryDeviceName(),
                DriverVersion = QueryDriverVersion(),
                ActiveBackend = GraphicsBackendType.Direct3D11,
                ShaderModelVersion = GetShaderModelVersion(_featureLevel),
                RemixAvailable = false,
                DlssAvailable = false,
                FsrAvailable = true // FSR works on DX11 via compute
            };
        }

        #endregion

        #region IComputeBackend Implementation

        /// <summary>
        /// Dispatches compute shader work.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-dispatch
        /// Method signature: Dispatch(UINT ThreadGroupCountX, UINT ThreadGroupCountY, UINT ThreadGroupCountZ)
        /// </summary>
        public virtual void Dispatch(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ)
        {
            if (!_initialized || !_capabilities.SupportsComputeShaders)
            {
                return;
            }

            // Implemented by derived classes with actual D3D11 calls
            // ID3D11DeviceContext::Dispatch(threadGroupCountX, threadGroupCountY, threadGroupCountZ)
            OnDispatch(threadGroupCountX, threadGroupCountY, threadGroupCountZ);
        }

        /// <summary>
        /// Creates a structured buffer for compute shaders.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11device-createbuffer
        /// </summary>
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

        /// <summary>
        /// Maps a buffer for CPU access.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-map
        /// </summary>
        public abstract IntPtr MapBuffer(IntPtr bufferHandle, MapType mapType);

        /// <summary>
        /// Unmaps a previously mapped buffer.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-unmap
        /// </summary>
        public abstract void UnmapBuffer(IntPtr bufferHandle);

        #endregion

        #region D3D11 Specific Methods

        /// <summary>
        /// Sets the viewport.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-rssetviewports
        /// </summary>
        public virtual void SetViewport(int x, int y, int width, int height, float minDepth = 0f, float maxDepth = 1f)
        {
            if (!_initialized) return;
            OnSetViewport(x, y, width, height, minDepth, maxDepth);
        }

        /// <summary>
        /// Sets the primitive topology.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-iasetprimitivetopology
        /// </summary>
        public virtual void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            if (!_initialized) return;
            OnSetPrimitiveTopology(topology);
        }

        /// <summary>
        /// Draws non-indexed geometry.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-draw
        /// </summary>
        public virtual void Draw(int vertexCount, int startVertexLocation)
        {
            if (!_initialized) return;
            OnDraw(vertexCount, startVertexLocation);
            TrackDrawCall(vertexCount / 3);
        }

        /// <summary>
        /// Draws indexed geometry.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-drawindexed
        /// </summary>
        public virtual void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            if (!_initialized) return;
            OnDrawIndexed(indexCount, startIndexLocation, baseVertexLocation);
            TrackDrawCall(indexCount / 3);
        }

        /// <summary>
        /// Draws instanced geometry.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-drawindexedinstanced
        /// </summary>
        public virtual void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount,
            int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            if (!_initialized) return;
            OnDrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation,
                baseVertexLocation, startInstanceLocation);
            TrackDrawCall((indexCountPerInstance / 3) * instanceCount);
        }

        #endregion

        #region Abstract Methods for Derived Classes

        protected abstract void OnDispatch(int x, int y, int z);
        protected abstract void OnSetViewport(int x, int y, int w, int h, float minD, float maxD);
        protected abstract void OnSetPrimitiveTopology(PrimitiveTopology topology);
        protected abstract void OnDraw(int vertexCount, int startVertexLocation);
        protected abstract void OnDrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation);
        protected abstract void OnDrawIndexedInstanced(int indexCountPerInstance, int instanceCount,
            int startIndexLocation, int baseVertexLocation, int startInstanceLocation);
        protected abstract ResourceInfo CreateStructuredBufferInternal(int elementCount, int elementStride,
            bool cpuWritable, IntPtr handle);

        #endregion

        #region Utility Methods

        protected virtual long QueryVideoMemory() => 4L * 1024 * 1024 * 1024; // Default 4GB
        protected virtual string QueryVendorName() => "Unknown";
        protected virtual string QueryDeviceName() => "DirectX 11 Device";
        protected virtual string QueryDriverVersion() => "Unknown";

        protected float GetShaderModelVersion(D3D11FeatureLevel level)
        {
            switch (level)
            {
                case D3D11FeatureLevel.Level_11_1: return 5.1f;
                case D3D11FeatureLevel.Level_11_0: return 5.0f;
                case D3D11FeatureLevel.Level_10_1: return 4.1f;
                case D3D11FeatureLevel.Level_10_0: return 4.0f;
                case D3D11FeatureLevel.Level_9_3: return 3.0f;
                case D3D11FeatureLevel.Level_9_2: return 2.0f;
                case D3D11FeatureLevel.Level_9_1: return 2.0f;
                default: return 5.0f;
            }
        }

        #endregion
    }
}


using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Backends
{
    /// <summary>
    /// Abstract base class for DirectX 10 backend implementations.
    /// 
    /// Provides shared D3D10 logic that can be inherited by both
    /// MonoGame and Stride implementations.
    /// 
    /// Features:
    /// - DirectX 10 rendering (Windows Vista+)
    /// - Shader Model 4.0 support
    /// - Geometry shaders
    /// - Multi-threaded resource creation
    /// - Feature level fallback (10_1, 10_0)
    /// 
    /// Based on D3D10 API: https://docs.microsoft.com/en-us/windows/win32/direct3d10/
    /// </summary>
    /// <remarks>
    /// DirectX 10 Backend:
    /// - This is a modern graphics API abstraction (DirectX 10 was released after KOTOR2)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game did not use DirectX 10; this is a modern enhancement for better hardware support
    /// - This abstraction: Provides DirectX 10 backend for modern Windows systems, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public abstract class BaseDirect3D10Backend : BaseGraphicsBackend
    {
        protected IntPtr _factory;
        protected IntPtr _adapter;
        protected IntPtr _device;
        protected IntPtr _immediateContext;
        protected IntPtr _swapChain;
        protected IntPtr _renderTargetView;
        protected IntPtr _depthStencilView;
        protected D3D10FeatureLevel _featureLevel;

        public override GraphicsBackendType BackendType => GraphicsBackendType.Direct3D10;

        /// <summary>
        /// Gets the current D3D10 feature level.
        /// </summary>
        public D3D10FeatureLevel FeatureLevel => _featureLevel;

        #region Template Method Implementations

        protected override void InitializeCapabilities()
        {
            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 8192,
                MaxRenderTargets = 8,
                MaxAnisotropy = 16,
                SupportsComputeShaders = false, // D3D10 doesn't support compute shaders
                SupportsGeometryShaders = true, // D3D10 supports geometry shaders
                SupportsTessellation = false, // Requires D3D11
                SupportsRaytracing = false, // Requires D3D12 DXR or Vulkan RT
                SupportsMeshShaders = false, // Requires D3D12
                SupportsVariableRateShading = false, // Requires D3D12
                DedicatedVideoMemory = QueryVideoMemory(),
                SharedSystemMemory = 4L * 1024 * 1024 * 1024,
                VendorName = QueryVendorName(),
                DeviceName = QueryDeviceName(),
                DriverVersion = QueryDriverVersion(),
                ActiveBackend = GraphicsBackendType.Direct3D10,
                ShaderModelVersion = 4.0f, // Shader Model 4.0 max for D3D10
                RemixAvailable = false, // Remix requires D3D9
                DlssAvailable = false, // DLSS requires D3D11+
                FsrAvailable = false // FSR requires compute shaders (D3D11+)
            };
        }

        #endregion

        #region D3D10 Specific Methods

        /// <summary>
        /// Sets the viewport.
        /// Based on D3D10 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d10/nf-d3d10-id3d10device-rssetviewports
        /// </summary>
        public virtual void SetViewport(int x, int y, int width, int height, float minDepth = 0f, float maxDepth = 1f)
        {
            if (!_initialized) return;
            OnSetViewport(x, y, width, height, minDepth, maxDepth);
        }

        /// <summary>
        /// Sets the primitive topology.
        /// Based on D3D10 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d10/nf-d3d10-id3d10device-iasetprimitivetopology
        /// </summary>
        public virtual void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            if (!_initialized) return;
            OnSetPrimitiveTopology(topology);
        }

        /// <summary>
        /// Draws non-indexed geometry.
        /// Based on D3D10 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d10/nf-d3d10-id3d10device-draw
        /// </summary>
        public virtual void Draw(int vertexCount, int startVertexLocation)
        {
            if (!_initialized) return;
            OnDraw(vertexCount, startVertexLocation);
            TrackDrawCall(vertexCount / 3);
        }

        /// <summary>
        /// Draws indexed geometry.
        /// Based on D3D10 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d10/nf-d3d10-id3d10device-drawindexed
        /// </summary>
        public virtual void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            if (!_initialized) return;
            OnDrawIndexed(indexCount, startIndexLocation, baseVertexLocation);
            TrackDrawCall(indexCount / 3);
        }

        /// <summary>
        /// Draws geometry with instancing.
        /// Based on D3D10 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d10/nf-d3d10-id3d10device-drawinstanced
        /// </summary>
        public virtual void DrawInstanced(int vertexCountPerInstance, int instanceCount, int startVertexLocation, int startInstanceLocation)
        {
            if (!_initialized) return;
            OnDrawInstanced(vertexCountPerInstance, instanceCount, startVertexLocation, startInstanceLocation);
            TrackDrawCall((vertexCountPerInstance * instanceCount) / 3);
        }

        /// <summary>
        /// Draws indexed geometry with instancing.
        /// Based on D3D10 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d10/nf-d3d10-id3d10device-drawindexedinstanced
        /// </summary>
        public virtual void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            if (!_initialized) return;
            OnDrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
            TrackDrawCall((indexCountPerInstance * instanceCount) / 3);
        }

        #endregion

        #region Abstract Methods for Derived Classes

        protected abstract void OnSetViewport(int x, int y, int width, int height, float minDepth, float maxDepth);
        protected abstract void OnSetPrimitiveTopology(PrimitiveTopology topology);
        protected abstract void OnDraw(int vertexCount, int startVertexLocation);
        protected abstract void OnDrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation);
        protected abstract void OnDrawInstanced(int vertexCountPerInstance, int instanceCount, int startVertexLocation, int startInstanceLocation);
        protected abstract void OnDrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation);

        #endregion

        #region Capability Queries

        protected virtual long QueryVideoMemory() => 1L * 1024 * 1024 * 1024; // Default 1GB for D3D10 era
        protected virtual string QueryVendorName() => "Unknown";
        protected virtual string QueryDeviceName() => "DirectX 10 Device";
        protected virtual string QueryDriverVersion() => "Unknown";

        #endregion
    }

    #region D3D10 Enums

    /// <summary>
    /// D3D10 feature levels.
    /// Based on D3D10 API: https://docs.microsoft.com/en-us/windows/win32/api/d3dcommon/ne-d3dcommon-d3d_feature_level
    /// </summary>
    public enum D3D10FeatureLevel
    {
        Level_10_0 = 0xa000,
        Level_10_1 = 0xa100
    }

    #endregion
}


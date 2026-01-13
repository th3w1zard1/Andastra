using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Game.Graphics.Common.Backends
{
    /// <summary>
    /// Abstract base class for DirectX 9 backend implementations.
    /// 
    /// Provides shared D3D9 logic that can be inherited by both
    /// MonoGame and Stride implementations.
    /// 
    /// Features:
    /// - DirectX 9 rendering (Windows XP+)
    /// - Fixed-function pipeline support
    /// - Shader Model 2.0/3.0 support
    /// - Legacy compatibility mode
    /// - NVIDIA RTX Remix wrapper support (via D3D9Remix)
    /// 
    /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/direct3d9/
    /// </summary>
    /// <remarks>
    /// DirectX 9 Backend:
    /// - This matches the original game's primary graphics API (DirectX 9)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Graphics initialization: 0x00404250 @ 0x00404250 (main game loop, WinMain equivalent) handles graphics setup
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8, "2D3DBias" @ 0x007c612c
    /// - Original game graphics device: DirectX 9 device creation and management (D3D9CreateDevice, Present, etc.)
    /// - RTX Remix: This backend can be used with NVIDIA RTX Remix for path tracing injection (wraps DX9 calls)
    /// - This abstraction: Provides DirectX 9 backend matching original game's graphics API
    /// </remarks>
    public abstract class BaseDirect3D9Backend : BaseGraphicsBackend
    {
        protected IntPtr _device;
        protected IntPtr _swapChain;
        protected IntPtr _presentParameters;
        protected D3D9DeviceType _deviceType;
        protected bool _isSoftwareDevice;
        protected D3D9PresentInterval _presentInterval;

        public override GraphicsBackendType BackendType => GraphicsBackendType.Direct3D9;

        #region Template Method Implementations

        protected override void InitializeCapabilities()
        {
            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 2048, // D3D9 limit
                MaxRenderTargets = 1, // D3D9 limit
                MaxAnisotropy = 16,
                SupportsComputeShaders = false, // D3D9 doesn't support compute shaders
                SupportsGeometryShaders = false, // Requires D3D10+
                SupportsTessellation = false, // Requires D3D11+
                SupportsRaytracing = false, // Requires D3D12 DXR or Vulkan RT
                SupportsMeshShaders = false, // Requires D3D12
                SupportsVariableRateShading = false, // Requires D3D12
                DedicatedVideoMemory = QueryVideoMemory(),
                SharedSystemMemory = 2L * 1024 * 1024 * 1024,
                VendorName = QueryVendorName(),
                DeviceName = QueryDeviceName(),
                DriverVersion = QueryDriverVersion(),
                ActiveBackend = GraphicsBackendType.Direct3D9,
                ShaderModelVersion = 3.0f, // Shader Model 3.0 max for D3D9
                RemixAvailable = QueryRemixSupport(),
                DlssAvailable = false, // DLSS requires D3D11+
                FsrAvailable = false // FSR requires compute shaders (D3D11+)
            };
        }

        #endregion

        #region D3D9 Specific Methods

        /// <summary>
        /// Creates a DirectX 9 device.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3d9-createdevice
        /// </summary>
        public virtual bool CreateDevice(int adapter, D3D9DeviceType deviceType, IntPtr focusWindow, int behaviorFlags)
        {
            if (!_initialized) return false;

            _deviceType = deviceType;
            _isSoftwareDevice = (deviceType & D3D9DeviceType.Software) != 0;

            return OnCreateDevice(adapter, deviceType, focusWindow, behaviorFlags);
        }

        /// <summary>
        /// Sets the presentation parameters.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dpresent-parameters
        /// </summary>
        public virtual void SetPresentParameters(D3D9PresentParameters parameters)
        {
            if (!_initialized) return;
            OnSetPresentParameters(parameters);
        }

        /// <summary>
        /// Presents the back buffer to the screen.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-present
        /// </summary>
        public virtual bool Present()
        {
            if (!_initialized) return false;
            return OnPresent();
        }

        /// <summary>
        /// Resets the device with new parameters.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-reset
        /// </summary>
        public virtual bool ResetDevice(D3D9PresentParameters parameters)
        {
            if (!_initialized) return false;
            return OnResetDevice(parameters);
        }

        /// <summary>
        /// Sets a render state.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-setrenderstate
        /// </summary>
        public virtual void SetRenderState(D3D9RenderState state, uint value)
        {
            if (!_initialized) return;
            OnSetRenderState(state, value);
        }

        /// <summary>
        /// Sets a texture sampler state.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-setsamplerstate
        /// </summary>
        public virtual void SetSamplerState(uint sampler, D3D9SamplerStateType type, uint value)
        {
            if (!_initialized) return;
            OnSetSamplerState(sampler, type, value);
        }

        /// <summary>
        /// Sets a texture.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-settexture
        /// </summary>
        public virtual void SetTexture(uint stage, IntPtr texture)
        {
            if (!_initialized) return;
            OnSetTexture(stage, texture);
        }

        /// <summary>
        /// Draws primitives.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-drawprimitive
        /// </summary>
        public virtual void DrawPrimitive(D3D9PrimitiveType primitiveType, int startVertex, int primitiveCount)
        {
            if (!_initialized) return;
            OnDrawPrimitive(primitiveType, startVertex, primitiveCount);
            TrackDrawCall(primitiveCount);
        }

        /// <summary>
        /// Draws indexed primitives.
        /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-drawindexedprimitive
        /// </summary>
        public virtual void DrawIndexedPrimitive(D3D9PrimitiveType primitiveType, int minIndex, int numVertices, int startIndex, int primitiveCount)
        {
            if (!_initialized) return;
            OnDrawIndexedPrimitive(primitiveType, minIndex, numVertices, startIndex, primitiveCount);
            TrackDrawCall(primitiveCount);
        }

        #endregion

        #region Abstract Methods for Derived Classes

        protected abstract bool OnCreateDevice(int adapter, D3D9DeviceType deviceType, IntPtr focusWindow, int behaviorFlags);
        protected abstract void OnSetPresentParameters(D3D9PresentParameters parameters);
        protected abstract bool OnPresent();
        protected abstract bool OnResetDevice(D3D9PresentParameters parameters);
        protected abstract void OnSetRenderState(D3D9RenderState state, uint value);
        protected abstract void OnSetSamplerState(uint sampler, D3D9SamplerStateType type, uint value);
        protected abstract void OnSetTexture(uint stage, IntPtr texture);
        protected abstract void OnDrawPrimitive(D3D9PrimitiveType primitiveType, int startVertex, int primitiveCount);
        protected abstract void OnDrawIndexedPrimitive(D3D9PrimitiveType primitiveType, int minIndex, int numVertices, int startIndex, int primitiveCount);

        #endregion

        #region Capability Queries

        protected virtual bool QueryRemixSupport() => false; // Override in Remix wrapper
        protected virtual long QueryVideoMemory() => 512L * 1024 * 1024; // Default 512MB for D3D9 era
        protected virtual string QueryVendorName() => "Unknown";
        protected virtual string QueryDeviceName() => "DirectX 9 Device";
        protected virtual string QueryDriverVersion() => "Unknown";

        #endregion
    }

    #region D3D9 Enums and Types

    /// <summary>
    /// D3D9 device types.
    /// Based on D3D9 API: D3DDEVTYPE enumeration
    /// </summary>
    [Flags]
    public enum D3D9DeviceType : uint
    {
        Hardware = 1,
        Reference = 2,
        Software = 3,
        NullReference = 4,
        HardwareVertexProcessing = 0x00000040,
        SoftwareVertexProcessing = 0x00000020,
        MixedVertexProcessing = 0x00000080
    }

    /// <summary>
    /// D3D9 present intervals.
    /// Based on D3D9 API: D3DPRESENT_INTERVAL enumeration
    /// </summary>
    public enum D3D9PresentInterval : uint
    {
        Default = 0,
        One = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Immediate = 0x80000000
    }

    /// <summary>
    /// D3D9 render states.
    /// Based on D3D9 API: D3DRENDERSTATETYPE enumeration
    /// </summary>
    public enum D3D9RenderState : uint
    {
        ZEnable = 7,
        FillMode = 8,
        ShadeMode = 9,
        ZWriteEnable = 14,
        AlphaTestEnable = 15,
        LastPixel = 16,
        SrcBlend = 19,
        DestBlend = 20,
        CullMode = 22,
        ZFunc = 23,
        AlphaRef = 24,
        AlphaFunc = 25,
        DitherEnable = 26,
        AlphaBlendEnable = 27,
        FogEnable = 28,
        SpecularEnable = 29,
        FogColor = 34,
        FogTableMode = 35,
        FogStart = 36,
        FogEnd = 37,
        FogDensity = 38,
        RangeFogEnable = 48,
        StencilEnable = 52,
        StencilFail = 53,
        StencilZFail = 54,
        StencilPass = 55,
        StencilFunc = 56,
        StencilRef = 57,
        StencilMask = 58,
        StencilWriteMask = 59,
        TextureFactor = 145,
        Wrap0 = 128,
        Wrap1 = 129,
        Wrap2 = 130,
        Wrap3 = 131,
        Wrap4 = 132,
        Wrap5 = 133,
        Wrap6 = 134,
        Wrap7 = 135,
        Clipping = 136,
        Lighting = 137,
        Ambient = 139,
        FogVertexMode = 140,
        ColorVertex = 141,
        LocalViewer = 142,
        NormalizeNormals = 143,
        ColorWriteEnable = 168,
        BlendOp = 171,
        BlendOpAlpha = 209,
        PointSize = 154,
        PointSizeMin = 155,
        PointSpriteEnable = 156,
        PointScaleEnable = 157,
        PointScaleA = 158,
        PointScaleB = 159,
        PointScaleC = 160,
        MultiSampleAntialias = 161,
        MultiSampleMask = 162,
        PatchEdgeStyle = 163,
        DebugMonitorToken = 165,
        IndexedVertexBlendEnable = 167,
        ColorWriteEnable1 = 169,
        ColorWriteEnable2 = 170,
        ColorWriteEnable3 = 171,
        SeparableAlphaBlendEnable = 172,
        SourceBlendAlpha = 206,
        DestBlendAlpha = 207,
        BlendFactor = 208,
        SampleMask = 210,
        ScissorTestEnable = 174,
        SlopeScaleDepthBias = 175,
        AntiAliasedLineEnable = 176,
        MinTessellationLevel = 177,
        MaxTessellationLevel = 178,
        AdaptiveTessX = 179,
        AdaptiveTessY = 180,
        AdaptiveTessZ = 181,
        AdaptiveTessW = 182,
        EnableAdaptiveTessellation = 183,
        TwoSidedStencilMode = 184,
        CcwStencilFail = 185,
        CcwStencilZFail = 186,
        CcwStencilPass = 187,
        CcwStencilFunc = 188,
        ColorWriteEnableIndexed = 189,
        BlendOpIndexed = 190,
        SourceBlendIndexed = 191,
        DestBlendIndexed = 192,
        BlendFactorIndexed = 193,
        MaxVShaderInstructionsExecuted = 194,
        MaxPixelShaderInstructionsExecuted = 195
    }

    /// <summary>
    /// D3D9 sampler state types.
    /// Based on D3D9 API: D3DSAMPLERSTATETYPE enumeration
    /// </summary>
    public enum D3D9SamplerStateType : uint
    {
        AddressU = 1,
        AddressV = 2,
        AddressW = 3,
        BorderColor = 4,
        MagFilter = 5,
        MinFilter = 6,
        MipFilter = 7,
        MipMapLodBias = 8,
        MaxMipLevel = 9,
        MaxAnisotropy = 10,
        SrgbTexture = 11,
        ElementIndex = 12,
        DmapOffset = 13
    }

    /// <summary>
    /// D3D9 primitive types.
    /// Based on D3D9 API: D3DPRIMITIVETYPE enumeration
    /// </summary>
    public enum D3D9PrimitiveType : uint
    {
        PointList = 1,
        LineList = 2,
        LineStrip = 3,
        TriangleList = 4,
        TriangleStrip = 5,
        TriangleFan = 6
    }

    /// <summary>
    /// D3D9 present parameters structure.
    /// Based on D3D9 API: D3DPRESENT_PARAMETERS structure
    /// </summary>
    public struct D3D9PresentParameters
    {
        public int BackBufferWidth;
        public int BackBufferHeight;
        public int BackBufferFormat;
        public int BackBufferCount;
        public int MultiSampleType;
        public int MultiSampleQuality;
        public int SwapEffect;
        public IntPtr DeviceWindow;
        public bool Windowed;
        public bool EnableAutoDepthStencil;
        public int AutoDepthStencilFormat;
        public int Flags;
        public uint FullScreenRefreshRateInHz;
        public uint PresentationInterval;
    }

    #endregion
}


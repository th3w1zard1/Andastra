using System;

namespace Andastra.Runtime.Graphics.Common.Enums
{
    /// <summary>
    /// Supported graphics API backends.
    /// </summary>
    /// <remarks>
    /// Graphics Backend Type Enumeration:
    /// - This enum represents modern graphics API abstractions
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game did not support DirectX 11/12, Vulkan, or Metal; these are modern enhancements
    /// - DirectX 9 Remix: Special mode for NVIDIA RTX Remix path tracing injection (wraps DX9 calls)
    /// </remarks>
    [Flags]
    public enum GraphicsBackendType
    {
        /// <summary>
        /// Automatic selection based on platform and hardware.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Vulkan 1.2+ - Cross-platform, modern API with raytracing support.
        /// Primary backend for Linux and modern Windows.
        /// </summary>
        Vulkan = 1 << 0,

        /// <summary>
        /// DirectX 11 - Windows legacy support.
        /// Good compatibility with older hardware.
        /// </summary>
        Direct3D11 = 1 << 1,

        /// <summary>
        /// DirectX 12 - Windows modern API with raytracing support (DXR).
        /// Best performance on Windows with RTX hardware.
        /// </summary>
        Direct3D12 = 1 << 2,

        /// <summary>
        /// DirectX 9 - Legacy Windows support.
        /// </summary>
        Direct3D9 = 1 << 3,

        /// <summary>
        /// DirectX 9 compatibility mode - For NVIDIA RTX Remix injection.
        /// Uses a DX9 wrapper layer to enable Remix path tracing.
        /// </summary>
        Direct3D9Remix = 1 << 8,

        /// <summary>
        /// DirectX 10 - Windows Vista+ support.
        /// Transitional API between DX9 and DX11.
        /// </summary>
        Direct3D10 = 1 << 7,

        /// <summary>
        /// OpenGL 4.5+ - Cross-platform fallback.
        /// Used when Vulkan is unavailable.
        /// </summary>
        OpenGL = 1 << 4,

        /// <summary>
        /// OpenGL ES 3.2 - Mobile and embedded platforms.
        /// </summary>
        OpenGLES = 1 << 5,

        /// <summary>
        /// Metal - macOS and iOS native API.
        /// </summary>
        Metal = 1 << 6,

        /// <summary>
        /// MonoGame - Cross-platform 2D/3D game framework.
        /// Uses OpenGL, DirectX, or Metal depending on platform.
        /// </summary>
        MonoGame = 1 << 9,

        /// <summary>
        /// Stride (formerly Xenko) - Modern 3D game engine.
        /// Uses DirectX 11/12, Vulkan, or OpenGL depending on platform.
        /// </summary>
        Stride = 1 << 10,

        /// <summary>
        /// Original Engine Backend - Matches original game engine rendering exactly 1:1.
        /// Uses the same DirectX/OpenGL APIs and rendering pipeline as the original games.
        /// </summary>
        OriginalEngine = 1 << 11,

        /// <summary>
        /// Eclipse Engine - Dragon Age Origins and Dragon Age 2 rendering backend.
        /// Matches daorigins.exe and DragonAge2.exe rendering implementation exactly.
        /// </summary>
        EclipseEngine = 1 << 12,

        /// <summary>
        /// Odyssey Engine - KOTOR 1 and KOTOR 2 rendering backend.
        /// Matches swkotor.exe and swkotor2.exe rendering implementation exactly.
        /// </summary>
        OdysseyEngine = 1 << 13,

        /// <summary>
        /// Aurora Engine - Neverwinter Nights Enhanced Edition rendering backend.
        /// Matches nwmain.exe rendering implementation exactly.
        /// </summary>
        AuroraEngine = 1 << 14
    }

    /// <summary>
    /// Rendering quality presets.
    /// </summary>
    public enum RenderQuality
    {
        /// <summary>
        /// Lowest quality - maximum performance.
        /// </summary>
        Low,

        /// <summary>
        /// Balanced quality and performance.
        /// </summary>
        Medium,

        /// <summary>
        /// High quality rendering.
        /// </summary>
        High,

        /// <summary>
        /// Maximum quality with all features enabled.
        /// </summary>
        Ultra,

        /// <summary>
        /// Custom user-defined settings.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Raytracing feature levels.
    /// </summary>
    public enum RaytracingLevel
    {
        /// <summary>
        /// No raytracing - rasterization only.
        /// </summary>
        Disabled,

        /// <summary>
        /// Raytraced shadows only.
        /// </summary>
        ShadowsOnly,

        /// <summary>
        /// Raytraced reflections only.
        /// </summary>
        ReflectionsOnly,

        /// <summary>
        /// Raytraced shadows and reflections.
        /// </summary>
        ShadowsAndReflections,

        /// <summary>
        /// Full raytracing with global illumination.
        /// </summary>
        Full,

        /// <summary>
        /// Path tracing mode (NVIDIA RTX Remix compatible).
        /// </summary>
        PathTracing
    }

    /// <summary>
    /// Anti-aliasing modes.
    /// </summary>
    public enum AntiAliasingMode
    {
        /// <summary>
        /// No anti-aliasing.
        /// </summary>
        None,

        /// <summary>
        /// Fast approximate anti-aliasing.
        /// </summary>
        FXAA,

        /// <summary>
        /// Subpixel morphological anti-aliasing.
        /// </summary>
        SMAA,

        /// <summary>
        /// Temporal anti-aliasing.
        /// </summary>
        TAA,

        /// <summary>
        /// Multi-sample anti-aliasing (2x).
        /// </summary>
        MSAA2x,

        /// <summary>
        /// Multi-sample anti-aliasing (4x).
        /// </summary>
        MSAA4x,

        /// <summary>
        /// Multi-sample anti-aliasing (8x).
        /// </summary>
        MSAA8x,

        /// <summary>
        /// NVIDIA DLSS (Deep Learning Super Sampling).
        /// </summary>
        DLSS,

        /// <summary>
        /// AMD FSR (FidelityFX Super Resolution).
        /// </summary>
        FSR
    }

    /// <summary>
    /// Shadow quality levels.
    /// </summary>
    public enum ShadowQuality
    {
        /// <summary>
        /// Shadows disabled.
        /// </summary>
        Disabled,

        /// <summary>
        /// Low quality shadows (512px maps).
        /// </summary>
        Low,

        /// <summary>
        /// Medium quality shadows (1024px maps).
        /// </summary>
        Medium,

        /// <summary>
        /// High quality shadows (2048px maps).
        /// </summary>
        High,

        /// <summary>
        /// Ultra quality shadows (4096px maps).
        /// </summary>
        Ultra,

        /// <summary>
        /// Raytraced shadows (requires RTX hardware).
        /// </summary>
        Raytraced
    }

    /// <summary>
    /// Denoiser types for raytracing.
    /// </summary>
    public enum DenoiserType
    {
        /// <summary>
        /// No denoising.
        /// </summary>
        None,

        /// <summary>
        /// NVIDIA Real-Time Denoiser (NRD).
        /// </summary>
        NvidiaRealTimeDenoiser,

        /// <summary>
        /// Intel Open Image Denoise (OIDN).
        /// </summary>
        IntelOpenImageDenoise,

        /// <summary>
        /// Simple temporal accumulation.
        /// </summary>
        Temporal,

        /// <summary>
        /// NVIDIA Optix denoiser.
        /// </summary>
        Optix
    }

    /// <summary>
    /// Light types for rendering.
    /// </summary>
    public enum LightType
    {
        /// <summary>
        /// Directional light (sun, moon).
        /// </summary>
        Directional,

        /// <summary>
        /// Point light (omnidirectional).
        /// </summary>
        Point,

        /// <summary>
        /// Spot light (cone).
        /// </summary>
        Spot,

        /// <summary>
        /// Area light (rectangular or disc).
        /// </summary>
        Area,

        /// <summary>
        /// Ambient light (uniform).
        /// </summary>
        Ambient
    }

    /// <summary>
    /// Global illumination modes.
    /// </summary>
    public enum GlobalIlluminationMode
    {
        /// <summary>
        /// No global illumination.
        /// </summary>
        Disabled,

        /// <summary>
        /// Baked lightmaps only.
        /// </summary>
        BakedOnly,

        /// <summary>
        /// Screen-space global illumination.
        /// </summary>
        ScreenSpace,

        /// <summary>
        /// Light probe based GI.
        /// </summary>
        LightProbes,

        /// <summary>
        /// Voxel-based global illumination.
        /// </summary>
        Voxel,

        /// <summary>
        /// Hardware raytraced global illumination.
        /// </summary>
        Raytraced
    }

    /// <summary>
    /// Ambient occlusion modes.
    /// </summary>
    public enum AmbientOcclusionMode
    {
        /// <summary>
        /// No ambient occlusion.
        /// </summary>
        Disabled,

        /// <summary>
        /// Screen-space ambient occlusion.
        /// </summary>
        SSAO,

        /// <summary>
        /// Horizon-based ambient occlusion.
        /// </summary>
        HBAO,

        /// <summary>
        /// Ground truth ambient occlusion.
        /// </summary>
        GTAO,

        /// <summary>
        /// Raytraced ambient occlusion.
        /// </summary>
        RTAO
    }

    /// <summary>
    /// Tonemapping operators.
    /// </summary>
    public enum TonemapOperator
    {
        /// <summary>
        /// No tonemapping.
        /// </summary>
        None,

        /// <summary>
        /// Reinhard tonemapping.
        /// </summary>
        Reinhard,

        /// <summary>
        /// Extended Reinhard tonemapping.
        /// </summary>
        ReinhardExtended,

        /// <summary>
        /// Academy Color Encoding System.
        /// </summary>
        ACES,

        /// <summary>
        /// Fitted ACES approximation.
        /// </summary>
        ACESFitted,

        /// <summary>
        /// Uncharted 2 filmic tonemapping.
        /// </summary>
        Uncharted2,

        /// <summary>
        /// AgX tonemapping.
        /// </summary>
        AgX,

        /// <summary>
        /// Neutral tonemapping.
        /// </summary>
        Neutral
    }

    /// <summary>
    /// NVIDIA DLSS modes.
    /// </summary>
    public enum DlssMode
    {
        /// <summary>
        /// DLSS disabled.
        /// </summary>
        Off,

        /// <summary>
        /// DLAA - Native resolution AA only.
        /// </summary>
        DLAA,

        /// <summary>
        /// Quality mode (highest quality upscaling).
        /// </summary>
        Quality,

        /// <summary>
        /// Balanced mode.
        /// </summary>
        Balanced,

        /// <summary>
        /// Performance mode.
        /// </summary>
        Performance,

        /// <summary>
        /// Ultra performance mode (lowest quality, highest performance).
        /// </summary>
        UltraPerformance
    }

    /// <summary>
    /// AMD FSR modes.
    /// </summary>
    public enum FsrMode
    {
        /// <summary>
        /// FSR disabled.
        /// </summary>
        Off,

        /// <summary>
        /// Quality mode (highest quality upscaling).
        /// </summary>
        Quality,

        /// <summary>
        /// Balanced mode.
        /// </summary>
        Balanced,

        /// <summary>
        /// Performance mode.
        /// </summary>
        Performance,

        /// <summary>
        /// Ultra performance mode (lowest quality, highest performance).
        /// </summary>
        UltraPerformance
    }

    /// <summary>
    /// Intel XeSS modes.
    /// </summary>
    public enum XeSSMode
    {
        /// <summary>
        /// XeSS disabled.
        /// </summary>
        Off,

        /// <summary>
        /// Quality mode (highest quality upscaling).
        /// </summary>
        Quality,

        /// <summary>
        /// Balanced mode.
        /// </summary>
        Balanced,

        /// <summary>
        /// Performance mode.
        /// </summary>
        Performance,

        /// <summary>
        /// Ultra performance mode (lowest quality, highest performance).
        /// </summary>
        UltraPerformance
    }

    /// <summary>
    /// DirectX 11 feature levels.
    /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3dcommon/ne-d3dcommon-d3d_feature_level
    /// </summary>
    public enum D3D11FeatureLevel
    {
        Level_9_1 = 0x9100,
        Level_9_2 = 0x9200,
        Level_9_3 = 0x9300,
        Level_10_0 = 0xa000,
        Level_10_1 = 0xa100,
        Level_11_0 = 0xb000,
        Level_11_1 = 0xb100
    }

    /// <summary>
    /// D3D11 primitive topology enumeration.
    /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_primitive_topology
    /// </summary>
    public enum PrimitiveTopology
    {
        Undefined = 0,
        PointList = 1,
        LineList = 2,
        LineStrip = 3,
        TriangleList = 4,
        TriangleStrip = 5,
        LineListAdj = 10,
        LineStripAdj = 11,
        TriangleListAdj = 12,
        TriangleStripAdj = 13,
        PatchList_1_ControlPoint = 33,
        PatchList_2_ControlPoint = 34,
        PatchList_3_ControlPoint = 35,
        PatchList_4_ControlPoint = 36,
        PatchList_32_ControlPoint = 64
    }

    /// <summary>
    /// D3D11 map types for buffer mapping.
    /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_map
    /// </summary>
    public enum MapType
    {
        Read = 1,
        Write = 2,
        ReadWrite = 3,
        WriteDiscard = 4,
        WriteNoOverwrite = 5
    }

    /// <summary>
    /// Texture formats.
    /// </summary>
    public enum TextureFormat
    {
        Unknown,
        R8_UNorm,
        R8_UInt,
        R8_SInt,
        R8_SNorm,
        R8G8_UNorm,
        R8G8_UInt,
        R8G8_SNorm,
        R8G8B8A8_UNorm,
        R8G8B8A8_UNorm_SRGB,
        R8G8B8A8_UInt,
        R8G8B8A8_SInt,
        R8G8B8A8_SNorm,
        B8G8R8A8_UNorm,
        B8G8R8A8_UNorm_SRGB,
        R16_Float,
        R16_UNorm,
        R16_UInt,
        R16_SInt,
        R16G16_Float,
        R16G16_UInt,
        R16G16_SInt,
        R16G16B16A16_Float,
        R16G16B16A16_UNorm,
        R16G16B16A16_UInt,
        R16G16B16A16_SInt,
        R32_Float,
        R32_UInt,
        R32_SInt,
        R32G32_Float,
        R32G32_UInt,
        R32G32_SInt,
        R32G32B32_Float,
        R32G32B32A32_Float,
        R32G32B32A32_UInt,
        R32G32B32A32_SInt,
        R11G11B10_Float,
        R10G10B10A2_UNorm,
        R10G10B10A2_UInt,
        // Block-compressed formats (BC1-BC7 aliases for DirectX/Metal compatibility)
        BC1_UNorm,
        BC1_UNorm_SRGB,
        BC1,
        BC2_UNorm,
        BC2_UNorm_SRGB,
        BC2,
        BC3_UNorm,
        BC3_UNorm_SRGB,
        BC3,
        BC4_UNorm,
        BC4,
        BC5_UNorm,
        BC5,
        BC6H_UFloat,
        BC6H,
        BC7_UNorm,
        BC7_UNorm_SRGB,
        BC7,
        // ASTC compressed formats (for Metal/mobile)
        ASTC_4x4,
        ASTC_5x5,
        ASTC_6x6,
        ASTC_8x8,
        ASTC_10x10,
        ASTC_12x12,
        // Depth formats
        D16_UNorm,
        D24_UNorm_S8_UInt,
        D32_Float,
        D32_Float_S8_UInt
    }

    /// <summary>
    /// Texture usage flags.
    /// </summary>
    [Flags]
    public enum TextureUsage
    {
        ShaderResource = 1 << 0,
        RenderTarget = 1 << 1,
        DepthStencil = 1 << 2,
        UnorderedAccess = 1 << 3
    }

    /// <summary>
    /// Buffer usage flags.
    /// </summary>
    [Flags]
    public enum BufferUsage
    {
        Vertex = 1 << 0,
        Index = 1 << 1,
        Constant = 1 << 2,
        Structured = 1 << 3,
        Indirect = 1 << 4,
        AccelerationStructure = 1 << 5
    }

    /// <summary>
    /// Blend factors.
    /// </summary>
    public enum BlendFactor
    {
        Zero,
        One,
        SrcColor,
        InvSrcColor,
        SrcAlpha,
        InvSrcAlpha,
        DstAlpha,
        InvDstAlpha,
        DstColor,
        InvDstColor
    }

    /// <summary>
    /// Blend operations.
    /// </summary>
    public enum BlendOp
    {
        Add,
        Subtract,
        ReverseSubtract,
        Min,
        Max
    }

    /// <summary>
    /// Cull modes.
    /// </summary>
    public enum CullMode
    {
        None,
        Front,
        Back
    }

    /// <summary>
    /// Fill modes.
    /// </summary>
    public enum FillMode
    {
        Solid,
        Wireframe
    }

    /// <summary>
    /// Comparison functions.
    /// </summary>
    public enum CompareFunc
    {
        Never,
        Less,
        Equal,
        LessEqual,
        Greater,
        NotEqual,
        GreaterEqual,
        Always
    }
}


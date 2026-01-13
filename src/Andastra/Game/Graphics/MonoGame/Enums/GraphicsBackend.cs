using System;

namespace Andastra.Game.Graphics.MonoGame.Enums
{
    /// <summary>
    /// Supported graphics API backends.
    /// </summary>
    [Flags]
    public enum GraphicsBackend
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
        /// DirectX 9 compatibility mode - For NVIDIA RTX Remix injection.
        /// Uses a DX9 wrapper layer to enable Remix path tracing.
        /// </summary>
        Direct3D9Remix = 1 << 3,

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
        Metal = 1 << 6
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
}


using System;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Runtime.Graphics.Common.Rendering
{
    /// <summary>
    /// Comprehensive render settings for the Odyssey engine.
    /// Shared across all backend implementations.
    /// </summary>
    /// <remarks>
    /// Render Settings:
    /// - This is a configuration class for modern graphics rendering settings
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Graphics initialization: FUN_00404250 @ 0x00404250 (main game loop, WinMain equivalent) handles graphics setup
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8, "2D3DBias" @ 0x007c612c
    /// - Original game settings: Read from swkotor2.ini @ 0x007b5740, configuration file for graphics options
    /// - Original game did not support modern features like raytracing, DLSS, FSR, or advanced post-processing
    /// - This class: Provides modern rendering settings for advanced graphics features, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public class RenderSettings
    {
        #region Display Settings

        /// <summary>
        /// Preferred graphics backend.
        /// Auto = Let the engine select the best available backend.
        /// Vulkan = Cross-platform modern API with raytracing support.
        /// Direct3D12 = Windows modern API with DXR raytracing.
        /// Direct3D11 = Windows legacy API with wide compatibility.
        /// Direct3D10 = Windows Vista+ transitional API.
        /// Direct3D9Remix = DirectX 9 wrapper for NVIDIA RTX Remix.
        /// OpenGL = Cross-platform fallback.
        /// </summary>
        public GraphicsBackendType PreferredBackend { get; set; } = GraphicsBackendType.Auto;

        /// <summary>
        /// Fallback backends in order of preference.
        /// The engine will try each backend in order until one succeeds.
        /// </summary>
        public GraphicsBackendType[] FallbackBackends { get; set; } = new GraphicsBackendType[]
        {
            GraphicsBackendType.Vulkan,
            GraphicsBackendType.Direct3D12,
            GraphicsBackendType.Direct3D11,
            GraphicsBackendType.Direct3D10,
            GraphicsBackendType.OpenGL
        };

        /// <summary>
        /// Window width in pixels.
        /// </summary>
        public int Width { get; set; } = 1920;

        /// <summary>
        /// Window height in pixels.
        /// </summary>
        public int Height { get; set; } = 1080;

        /// <summary>
        /// Whether to run in fullscreen mode.
        /// </summary>
        public bool Fullscreen { get; set; } = false;

        /// <summary>
        /// Whether to use borderless fullscreen.
        /// </summary>
        public bool BorderlessFullscreen { get; set; } = true;

        /// <summary>
        /// VSync mode (0 = off, 1 = on, 2+ = adaptive).
        /// </summary>
        public int VSync { get; set; } = 1;

        /// <summary>
        /// Target frame rate (0 = unlimited).
        /// </summary>
        public int TargetFrameRate { get; set; } = 0;

        /// <summary>
        /// Render scale (0.5 to 2.0).
        /// </summary>
        public float RenderScale { get; set; } = 1.0f;

        /// <summary>
        /// HDR output enabled.
        /// </summary>
        public bool HdrEnabled { get; set; } = false;

        /// <summary>
        /// HDR maximum luminance in nits.
        /// </summary>
        public float HdrMaxLuminance { get; set; } = 1000f;

        #endregion

        #region Quality Settings

        /// <summary>
        /// Overall render quality preset.
        /// </summary>
        public RenderQuality Quality { get; set; } = RenderQuality.High;

        /// <summary>
        /// Anti-aliasing mode.
        /// </summary>
        public AntiAliasingMode AntiAliasing { get; set; } = AntiAliasingMode.TAA;

        /// <summary>
        /// Anisotropic filtering level (1, 2, 4, 8, 16).
        /// </summary>
        public int AnisotropicFiltering { get; set; } = 16;

        /// <summary>
        /// Texture quality (0 = lowest, 4 = highest).
        /// </summary>
        public int TextureQuality { get; set; } = 4;

        /// <summary>
        /// Shadow quality.
        /// </summary>
        public ShadowQuality ShadowQuality { get; set; } = ShadowQuality.High;

        /// <summary>
        /// Shadow cascade count (1-4).
        /// </summary>
        public int ShadowCascades { get; set; } = 4;

        /// <summary>
        /// Maximum shadow distance.
        /// </summary>
        public float ShadowDistance { get; set; } = 100f;

        /// <summary>
        /// Shadow softness / penumbra width.
        /// </summary>
        public float ShadowSoftness { get; set; } = 1.0f;

        #endregion

        #region Lighting Settings

        /// <summary>
        /// Enable dynamic lighting (vs baked only).
        /// </summary>
        public bool DynamicLighting { get; set; } = true;

        /// <summary>
        /// Maximum number of dynamic lights per tile/cluster.
        /// </summary>
        public int MaxDynamicLights { get; set; } = 256;

        /// <summary>
        /// Global illumination mode.
        /// </summary>
        public GlobalIlluminationMode GlobalIllumination { get; set; } = GlobalIlluminationMode.ScreenSpace;

        /// <summary>
        /// Ambient occlusion mode.
        /// </summary>
        public AmbientOcclusionMode AmbientOcclusion { get; set; } = AmbientOcclusionMode.GTAO;

        /// <summary>
        /// Ambient occlusion intensity.
        /// </summary>
        public float AmbientOcclusionIntensity { get; set; } = 1.0f;

        /// <summary>
        /// Screen-space reflections enabled.
        /// </summary>
        public bool ScreenSpaceReflections { get; set; } = true;

        /// <summary>
        /// Reflection quality (0-4).
        /// </summary>
        public int ReflectionQuality { get; set; } = 2;

        /// <summary>
        /// Volumetric lighting enabled (god rays, fog).
        /// </summary>
        public bool VolumetricLighting { get; set; } = true;

        /// <summary>
        /// Volumetric lighting quality (0-4).
        /// </summary>
        public int VolumetricQuality { get; set; } = 2;

        #endregion

        #region Raytracing Settings

        /// <summary>
        /// Raytracing feature level.
        /// </summary>
        public RaytracingLevel Raytracing { get; set; } = RaytracingLevel.Disabled;

        /// <summary>
        /// Raytracing samples per pixel.
        /// </summary>
        public int RaytracingSamplesPerPixel { get; set; } = 1;

        /// <summary>
        /// Maximum raytracing bounces.
        /// </summary>
        public int RaytracingMaxBounces { get; set; } = 3;

        /// <summary>
        /// Raytracing denoiser enabled.
        /// </summary>
        public bool RaytracingDenoiser { get; set; } = true;

        /// <summary>
        /// Raytracing denoiser type.
        /// </summary>
        public DenoiserType RaytracingDenoiserType { get; set; } = DenoiserType.NvidiaRealTimeDenoiser;

        /// <summary>
        /// Enable NVIDIA RTX Remix compatibility mode.
        /// When enabled, the engine will use the Direct3D9Remix backend
        /// which allows RTX Remix to intercept rendering calls and apply
        /// path-traced graphics enhancements.
        /// Requires NVIDIA RTX GPU (20-series or newer) and RTX Remix Runtime.
        /// </summary>
        public bool RemixCompatibility { get; set; } = false;

        /// <summary>
        /// Path to NVIDIA RTX Remix runtime DLLs (d3d9.dll, NvRemixBridge.dll, etc).
        /// If empty, the engine will check the current directory and
        /// RTX_REMIX_PATH environment variable.
        /// </summary>
        public string RemixRuntimePath { get; set; } = "";

        #endregion

        #region Post-Processing Settings

        /// <summary>
        /// Bloom enabled.
        /// </summary>
        public bool BloomEnabled { get; set; } = true;

        /// <summary>
        /// Bloom intensity.
        /// </summary>
        public float BloomIntensity { get; set; } = 0.5f;

        /// <summary>
        /// Bloom threshold.
        /// </summary>
        public float BloomThreshold { get; set; } = 1.0f;

        /// <summary>
        /// Motion blur enabled.
        /// </summary>
        public bool MotionBlurEnabled { get; set; } = false;

        /// <summary>
        /// Motion blur intensity.
        /// </summary>
        public float MotionBlurIntensity { get; set; } = 0.5f;

        /// <summary>
        /// Depth of field enabled.
        /// </summary>
        public bool DepthOfFieldEnabled { get; set; } = false;

        /// <summary>
        /// Chromatic aberration enabled.
        /// </summary>
        public bool ChromaticAberration { get; set; } = false;

        /// <summary>
        /// Film grain enabled.
        /// </summary>
        public bool FilmGrain { get; set; } = false;

        /// <summary>
        /// Vignette enabled.
        /// </summary>
        public bool Vignette { get; set; } = false;

        /// <summary>
        /// Tonemapping operator.
        /// </summary>
        public TonemapOperator Tonemapper { get; set; } = TonemapOperator.ACES;

        /// <summary>
        /// Exposure value (EV).
        /// </summary>
        public float Exposure { get; set; } = 0f;

        /// <summary>
        /// Gamma correction value.
        /// </summary>
        public float Gamma { get; set; } = 2.2f;

        /// <summary>
        /// Color grading LUT texture path.
        /// </summary>
        public string ColorGradingLut { get; set; } = null;

        #endregion

        #region Performance Settings

        /// <summary>
        /// Level of detail bias (-2 to 2).
        /// </summary>
        public float LodBias { get; set; } = 0f;

        /// <summary>
        /// Draw distance multiplier.
        /// </summary>
        public float DrawDistanceMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Enable GPU culling (compute-based).
        /// </summary>
        public bool GpuCulling { get; set; } = true;

        /// <summary>
        /// Enable mesh shader pipeline (if available).
        /// </summary>
        public bool MeshShaders { get; set; } = false;

        /// <summary>
        /// Enable variable rate shading (if available).
        /// </summary>
        public bool VariableRateShading { get; set; } = false;

        /// <summary>
        /// Async compute enabled.
        /// </summary>
        public bool AsyncCompute { get; set; } = true;

        #endregion

        #region NVIDIA Settings

        /// <summary>
        /// DLSS mode.
        /// </summary>
        public DlssMode DlssMode { get; set; } = DlssMode.Off;

        /// <summary>
        /// DLSS sharpness (0-1).
        /// </summary>
        public float DlssSharpness { get; set; } = 0.5f;

        /// <summary>
        /// NVIDIA Reflex enabled (low latency mode).
        /// </summary>
        public bool NvidiaReflex { get; set; } = false;

        #endregion

        #region AMD Settings

        /// <summary>
        /// FSR mode.
        /// </summary>
        public FsrMode FsrMode { get; set; } = FsrMode.Off;

        /// <summary>
        /// FSR sharpness (0-1).
        /// </summary>
        public float FsrSharpness { get; set; } = 0.5f;

        #endregion

        /// <summary>
        /// Creates default settings for the specified quality preset.
        /// </summary>
        public static RenderSettings CreatePreset(RenderQuality quality)
        {
            var settings = new RenderSettings { Quality = quality };

            switch (quality)
            {
                case RenderQuality.Low:
                    settings.RenderScale = 0.75f;
                    settings.AntiAliasing = AntiAliasingMode.FXAA;
                    settings.AnisotropicFiltering = 4;
                    settings.TextureQuality = 1;
                    settings.ShadowQuality = ShadowQuality.Low;
                    settings.ShadowCascades = 2;
                    settings.DynamicLighting = false;
                    settings.GlobalIllumination = GlobalIlluminationMode.Disabled;
                    settings.AmbientOcclusion = AmbientOcclusionMode.Disabled;
                    settings.ScreenSpaceReflections = false;
                    settings.VolumetricLighting = false;
                    settings.BloomEnabled = false;
                    break;

                case RenderQuality.Medium:
                    settings.AntiAliasing = AntiAliasingMode.TAA;
                    settings.AnisotropicFiltering = 8;
                    settings.TextureQuality = 2;
                    settings.ShadowQuality = ShadowQuality.Medium;
                    settings.ShadowCascades = 3;
                    settings.GlobalIllumination = GlobalIlluminationMode.Disabled;
                    settings.AmbientOcclusion = AmbientOcclusionMode.SSAO;
                    settings.ReflectionQuality = 1;
                    settings.VolumetricQuality = 1;
                    break;

                case RenderQuality.High:
                    // Default settings are already high quality
                    break;

                case RenderQuality.Ultra:
                    settings.AntiAliasing = AntiAliasingMode.TAA;
                    settings.AnisotropicFiltering = 16;
                    settings.TextureQuality = 4;
                    settings.ShadowQuality = ShadowQuality.Ultra;
                    settings.ShadowCascades = 4;
                    settings.GlobalIllumination = GlobalIlluminationMode.Raytraced;
                    settings.AmbientOcclusion = AmbientOcclusionMode.RTAO;
                    settings.ReflectionQuality = 4;
                    settings.VolumetricQuality = 4;
                    settings.Raytracing = RaytracingLevel.Full;
                    settings.MeshShaders = true;
                    break;
            }

            return settings;
        }

        /// <summary>
        /// Validates settings and clamps values to valid ranges.
        /// </summary>
        public void Validate()
        {
            Width = Math.Max(320, Width);
            Height = Math.Max(240, Height);
            RenderScale = Math.Max(0.25f, Math.Min(4.0f, RenderScale));
            HdrMaxLuminance = Math.Max(100f, Math.Min(10000f, HdrMaxLuminance));
            AnisotropicFiltering = Math.Max(1, Math.Min(16, AnisotropicFiltering));
            TextureQuality = Math.Max(0, Math.Min(4, TextureQuality));
            ShadowCascades = Math.Max(1, Math.Min(4, ShadowCascades));
            ShadowDistance = Math.Max(10f, Math.Min(1000f, ShadowDistance));
            ShadowSoftness = Math.Max(0f, Math.Min(2f, ShadowSoftness));
            MaxDynamicLights = Math.Max(1, Math.Min(4096, MaxDynamicLights));
            AmbientOcclusionIntensity = Math.Max(0f, Math.Min(4f, AmbientOcclusionIntensity));
            ReflectionQuality = Math.Max(0, Math.Min(4, ReflectionQuality));
            VolumetricQuality = Math.Max(0, Math.Min(4, VolumetricQuality));
            RaytracingSamplesPerPixel = Math.Max(1, Math.Min(64, RaytracingSamplesPerPixel));
            RaytracingMaxBounces = Math.Max(1, Math.Min(32, RaytracingMaxBounces));
            BloomIntensity = Math.Max(0f, Math.Min(4f, BloomIntensity));
            BloomThreshold = Math.Max(0f, Math.Min(4f, BloomThreshold));
            MotionBlurIntensity = Math.Max(0f, Math.Min(2f, MotionBlurIntensity));
            Exposure = Math.Max(-10f, Math.Min(10f, Exposure));
            Gamma = Math.Max(1f, Math.Min(3f, Gamma));
            LodBias = Math.Max(-2f, Math.Min(2f, LodBias));
            DrawDistanceMultiplier = Math.Max(0.1f, Math.Min(4f, DrawDistanceMultiplier));
            DlssSharpness = Math.Max(0f, Math.Min(1f, DlssSharpness));
            FsrSharpness = Math.Max(0f, Math.Min(1f, FsrSharpness));
        }
    }
}


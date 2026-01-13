using System;
using System.Collections.Generic;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common;

namespace Andastra.Game.GUI
{
    /// <summary>
    /// Graphics settings presets for quick configuration.
    /// </summary>
    public enum GraphicsPreset
    {
        Low,
        Medium,
        High,
        Ultra,
        Custom
    }

    /// <summary>
    /// Factory for creating graphics settings presets.
    /// </summary>
    public static class GraphicsSettingsPresetFactory
    {
        /// <summary>
        /// Creates a graphics settings preset based on the specified preset level.
        /// </summary>
        public static GraphicsSettingsData CreatePreset(GraphicsPreset preset)
        {
            switch (preset)
            {
                case GraphicsPreset.Low:
                    return CreateLowPreset();
                case GraphicsPreset.Medium:
                    return CreateMediumPreset();
                case GraphicsPreset.High:
                    return CreateHighPreset();
                case GraphicsPreset.Ultra:
                    return CreateUltraPreset();
                default:
                    return new GraphicsSettingsData();
            }
        }

        private static GraphicsSettingsData CreateLowPreset()
        {
            return new GraphicsSettingsData
            {
                // Window
                WindowWidth = 1024,
                WindowHeight = 768,
                WindowFullscreen = false,
                WindowIsMouseVisible = true,
                WindowVSync = false,

                // MonoGame
                MonoGameSynchronizeWithVerticalRetrace = false,
                MonoGamePreferMultiSampling = false,
                MonoGameGraphicsProfile = "Reach",

                // Rasterizer
                RasterizerCullMode = Runtime.Graphics.Common.Enums.CullMode.Back,
                RasterizerFillMode = Runtime.Graphics.Common.Enums.FillMode.Solid,
                RasterizerDepthBiasEnabled = false,
                RasterizerMultiSampleAntiAlias = false,

                // Depth Stencil
                DepthStencilDepthBufferEnable = true,
                DepthStencilDepthBufferWriteEnable = true,
                DepthStencilDepthBufferFunction = (Andastra.Runtime.Graphics.CompareFunction?)Runtime.Graphics.CompareFunction.LessEqual,
                DepthStencilStencilEnable = false,

                // Blend
                BlendBlendEnable = true,
                BlendAlphaBlendFunction = (Andastra.Runtime.Graphics.BlendFunction?)Runtime.Graphics.BlendFunction.Add,
                BlendAlphaSourceBlend = Runtime.Graphics.Blend.SourceAlpha,
                BlendAlphaDestinationBlend = Runtime.Graphics.Blend.InverseSourceAlpha,
                BlendColorBlendFunction = (Andastra.Runtime.Graphics.BlendFunction?)Runtime.Graphics.BlendFunction.Add,
                BlendColorSourceBlend = Runtime.Graphics.Blend.SourceAlpha,
                BlendColorDestinationBlend = Runtime.Graphics.Blend.InverseSourceAlpha,
                BlendColorWriteChannels = Runtime.Graphics.ColorWriteChannels.All,

                // Sampler
                SamplerAddressU = TextureAddressMode.Wrap,
                SamplerAddressV = TextureAddressMode.Wrap,
                SamplerFilter = TextureFilter.Linear,
                SamplerMaxAnisotropy = 0,
                SamplerMaxMipLevel = 0,

                // Basic Effect
                BasicEffectLightingEnabled = false,
                BasicEffectTextureEnabled = true,
                BasicEffectVertexColorEnabled = false,

                // SpriteBatch
                SpriteBatchSortMode = SpriteSortMode.Deferred,

                // Spatial Audio
                SpatialAudioDopplerFactor = 0.5f,
                SpatialAudioSpeedOfSound = 343.0f
            };
        }

        private static GraphicsSettingsData CreateMediumPreset()
        {
            var settings = CreateLowPreset();
            settings.WindowWidth = 1280;
            settings.WindowHeight = 720;
            settings.WindowVSync = true;
            settings.MonoGameSynchronizeWithVerticalRetrace = true;
            settings.MonoGameGraphicsProfile = "HiDef";
            settings.RasterizerMultiSampleAntiAlias = false;
            settings.SamplerMaxAnisotropy = 2;
            settings.BasicEffectLightingEnabled = true;
            settings.SpatialAudioDopplerFactor = 1.0f;
            return settings;
        }

        private static GraphicsSettingsData CreateHighPreset()
        {
            var settings = CreateMediumPreset();
            settings.WindowWidth = 1920;
            settings.WindowHeight = 1080;
            settings.MonoGamePreferMultiSampling = true;
            settings.RasterizerMultiSampleAntiAlias = true;
            settings.SamplerMaxAnisotropy = 4;
            settings.SamplerFilter = TextureFilter.Anisotropic;
            settings.BasicEffectLightingEnabled = true;
            settings.SpatialAudioDopplerFactor = 1.5f;
            return settings;
        }

        private static GraphicsSettingsData CreateUltraPreset()
        {
            var settings = CreateHighPreset();
            settings.WindowWidth = 2560;
            settings.WindowHeight = 1440;
            settings.WindowFullscreen = true;
            settings.MonoGamePreferMultiSampling = true;
            settings.RasterizerMultiSampleAntiAlias = true;
            settings.SamplerMaxAnisotropy = 16;
            settings.SamplerFilter = TextureFilter.Anisotropic;
            settings.DepthStencilStencilEnable = true;
            settings.BasicEffectLightingEnabled = true;
            settings.SpatialAudioDopplerFactor = 2.0f;
            return settings;
        }
    }

    /// <summary>
    /// Helper class for graphics settings tooltips and descriptions.
    /// </summary>
    public static class GraphicsSettingsHelp
    {
        private static readonly Dictionary<string, string> HelpTexts = new Dictionary<string, string>
        {
            // Window Settings
            { "WindowTitle", "The title displayed in the game window's title bar." },
            { "WindowWidth", "The width of the game window in pixels. Minimum: 320, Maximum: 7680." },
            { "WindowHeight", "The height of the game window in pixels. Minimum: 240, Maximum: 4320." },
            { "WindowFullscreen", "Enable fullscreen mode. The game will run in exclusive fullscreen mode." },
            { "WindowIsMouseVisible", "Show or hide the mouse cursor in the game window." },
            { "WindowVSync", "Enable VSync (vertical synchronization) to synchronize frame rendering with the monitor's refresh rate. Reduces screen tearing but may limit FPS to the monitor's refresh rate. Based on swkotor.exe and swkotor2.exe: VSync controlled via DirectX Present parameters. Can be toggled in real-time without requiring a restart." },

            // MonoGame Settings
            { "MonoGameSynchronizeWithVerticalRetrace", "Enable VSync to synchronize frame rendering with the monitor's refresh rate. Reduces screen tearing but may limit FPS." },
            { "MonoGamePreferMultiSampling", "Enable multi-sampling anti-aliasing (MSAA) for smoother edges. Requires HiDef profile." },
            { "MonoGamePreferHalfPixelOffset", "Apply half-pixel offset for better sprite rendering on some platforms." },
            { "MonoGameHardwareModeSwitch", "Allow hardware mode switching for fullscreen transitions." },
            { "MonoGameGraphicsProfile", "Graphics profile: Reach (Xbox 360 level) or HiDef (modern hardware)." },
            { "MonoGameSupportedOrientationsPortrait", "Support portrait orientation (for mobile platforms)." },
            { "MonoGameSupportedOrientationsLandscape", "Support landscape orientation (for mobile platforms)." },

            // Rasterizer State
            { "RasterizerCullMode", "Face culling mode: None (render both sides), CullClockwiseFace, or CullCounterClockwiseFace." },
            { "RasterizerFillMode", "Polygon fill mode: Solid (filled) or WireFrame (outline only)." },
            { "RasterizerDepthBiasEnabled", "Enable depth bias to prevent z-fighting artifacts." },
            { "RasterizerDepthBias", "Depth bias value. Adjust to prevent z-fighting." },
            { "RasterizerSlopeScaleDepthBias", "Slope scale depth bias for shadow mapping." },
            { "RasterizerScissorTestEnabled", "Enable scissor test to clip rendering to a rectangular region." },
            { "RasterizerMultiSampleAntiAlias", "Enable multi-sample anti-aliasing (MSAA) for smoother edges." },

            // Depth Stencil State
            { "DepthStencilDepthBufferEnable", "Enable depth buffer for proper 3D depth sorting." },
            { "DepthStencilDepthBufferWriteEnable", "Allow writing to the depth buffer." },
            { "DepthStencilDepthBufferFunction", "Depth comparison function (LessEqual is standard)." },
            { "DepthStencilStencilEnable", "Enable stencil buffer for advanced rendering effects." },
            { "DepthStencilTwoSidedStencilMode", "Enable two-sided stencil operations." },
            { "DepthStencilStencilFail", "Operation to perform when stencil test fails." },
            { "DepthStencilStencilDepthFail", "Operation to perform when stencil test passes but depth test fails." },
            { "DepthStencilStencilPass", "Operation to perform when both stencil and depth tests pass." },
            { "DepthStencilStencilFunction", "Stencil comparison function." },
            { "DepthStencilReferenceStencil", "Reference value for stencil comparison (0-255)." },
            { "DepthStencilStencilMask", "Mask for reading stencil values (0-255)." },
            { "DepthStencilStencilWriteMask", "Mask for writing stencil values (0-255)." },

            // Blend State
            { "BlendAlphaBlendFunction", "Blend function for alpha channel." },
            { "BlendAlphaSourceBlend", "Source blend factor for alpha channel." },
            { "BlendAlphaDestinationBlend", "Destination blend factor for alpha channel." },
            { "BlendColorBlendFunction", "Blend function for color channels." },
            { "BlendColorSourceBlend", "Source blend factor for color channels." },
            { "BlendColorDestinationBlend", "Destination blend factor for color channels." },
            { "BlendColorWriteChannels", "Which color channels to write to (Red, Green, Blue, Alpha, All, or None)." },
            { "BlendBlendEnable", "Enable alpha blending for transparency effects." },
            { "BlendBlendFactorR", "Red component of blend factor (0-255)." },
            { "BlendBlendFactorG", "Green component of blend factor (0-255)." },
            { "BlendBlendFactorB", "Blue component of blend factor (0-255)." },
            { "BlendBlendFactorA", "Alpha component of blend factor (0-255)." },
            { "BlendMultiSampleMask", "Multi-sample mask for per-sample blending (-1 for all samples)." },

            // Sampler State
            { "SamplerAddressU", "Texture addressing mode for U coordinate (Wrap, Mirror, Clamp, Border, MirrorOnce)." },
            { "SamplerAddressV", "Texture addressing mode for V coordinate." },
            { "SamplerAddressW", "Texture addressing mode for W coordinate (3D textures)." },
            { "SamplerFilter", "Texture filtering mode: Point, Linear, Anisotropic, or PyramidalQuad." },
            { "SamplerMaxAnisotropy", "Maximum anisotropic filtering level (0-16). Higher values improve texture quality at angles." },
            { "SamplerMaxMipLevel", "Maximum mipmap level to use (0-15)." },
            { "SamplerMipMapLevelOfDetailBias", "Mipmap LOD bias for texture quality adjustment." },

            // Basic Effect
            { "BasicEffectVertexColorEnabled", "Use vertex colors for rendering." },
            { "BasicEffectLightingEnabled", "Enable per-pixel lighting calculations." },
            { "BasicEffectTextureEnabled", "Enable texture mapping." },
            { "BasicEffectAmbientLightColorX", "Red component of ambient light color (0.0-1.0)." },
            { "BasicEffectAmbientLightColorY", "Green component of ambient light color (0.0-1.0)." },
            { "BasicEffectAmbientLightColorZ", "Blue component of ambient light color (0.0-1.0)." },
            { "BasicEffectDiffuseColorX", "Red component of diffuse material color (0.0-1.0)." },
            { "BasicEffectDiffuseColorY", "Green component of diffuse material color (0.0-1.0)." },
            { "BasicEffectDiffuseColorZ", "Blue component of diffuse material color (0.0-1.0)." },
            { "BasicEffectEmissiveColorX", "Red component of emissive material color (0.0-1.0)." },
            { "BasicEffectEmissiveColorY", "Green component of emissive material color (0.0-1.0)." },
            { "BasicEffectEmissiveColorZ", "Blue component of emissive material color (0.0-1.0)." },
            { "BasicEffectSpecularColorX", "Red component of specular material color (0.0-1.0)." },
            { "BasicEffectSpecularColorY", "Green component of specular material color (0.0-1.0)." },
            { "BasicEffectSpecularColorZ", "Blue component of specular material color (0.0-1.0)." },
            { "BasicEffectSpecularPower", "Specular power for shininess (0.0-1000.0). Higher values = more focused highlights." },
            { "BasicEffectAlpha", "Alpha transparency value (0.0-1.0). 1.0 = fully opaque, 0.0 = fully transparent." },

            // SpriteBatch
            { "SpriteBatchSortMode", "Sprite sorting mode: Deferred, Immediate, Texture, BackToFront, or FrontToBack." },
            { "SpriteBatchBlendStateAlphaBlend", "Use alpha blending for sprite rendering." },
            { "SpriteBatchBlendStateAdditive", "Use additive blending for sprite rendering." },

            // Spatial Audio
            { "SpatialAudioDopplerFactor", "Doppler effect intensity (0.0-10.0). Higher values = more pronounced Doppler shift." },
            { "SpatialAudioSpeedOfSound", "Speed of sound in meters per second (default: 343 m/s)." },

            // Content Manager
            { "ContentManagerRootDirectory", "Root directory path for game content files." }
        };

        /// <summary>
        /// Gets help text for a setting by key.
        /// </summary>
        public static string GetHelpText(string key)
        {
            return HelpTexts.TryGetValue(key, out var text) ? text : "No help available for this setting.";
        }

        /// <summary>
        /// Gets a short description for a setting by key.
        /// </summary>
        public static string GetShortDescription(string key)
        {
            var fullText = GetHelpText(key);
            var periodIndex = fullText.IndexOf('.');
            return periodIndex > 0 ? fullText.Substring(0, periodIndex) : fullText;
        }
    }
}


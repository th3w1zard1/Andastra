using System;
using Odyssey.MonoGame.Interfaces;

namespace Odyssey.MonoGame.Rendering
{
    /// <summary>
    /// Render quality presets for common performance profiles.
    /// 
    /// Provides pre-configured quality settings for different hardware
    /// and performance targets.
    /// 
    /// Features:
    /// - Low/Medium/High/Ultra presets
    /// - Customizable quality settings
    /// - Automatic quality selection
    /// </summary>
    public static class QualityPresets
    {
        /// <summary>
        /// Quality level.
        /// </summary>
        public enum QualityLevel
        {
            Low,
            Medium,
            High,
            Ultra
        }

        /// <summary>
        /// Quality settings.
        /// </summary>
        public struct QualitySettings
        {
            public QualityLevel Level;
            public int ResolutionScalePercent;
            public bool ShadowsEnabled;
            public int ShadowResolution;
            public int ShadowCascadeCount;
            public bool SSAOEnabled;
            public bool SSREnabled;
            public bool TAAEnabled;
            public bool MotionBlurEnabled;
            public int MaxLights;
            public float LODDistance;
            public bool OcclusionCullingEnabled;
            public bool FrustumCullingEnabled;
        }

        /// <summary>
        /// Gets quality settings for a specific level.
        /// </summary>
        public static QualitySettings GetSettings(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low:
                    return new QualitySettings
                    {
                        Level = QualityLevel.Low,
                        ResolutionScalePercent = 75,
                        ShadowsEnabled = false,
                        ShadowResolution = 512,
                        ShadowCascadeCount = 1,
                        SSAOEnabled = false,
                        SSREnabled = false,
                        TAAEnabled = false,
                        MotionBlurEnabled = false,
                        MaxLights = 4,
                        LODDistance = 0.5f,
                        OcclusionCullingEnabled = true,
                        FrustumCullingEnabled = true
                    };

                case QualityLevel.Medium:
                    return new QualitySettings
                    {
                        Level = QualityLevel.Medium,
                        ResolutionScalePercent = 90,
                        ShadowsEnabled = true,
                        ShadowResolution = 1024,
                        ShadowCascadeCount = 2,
                        SSAOEnabled = true,
                        SSREnabled = false,
                        TAAEnabled = true,
                        MotionBlurEnabled = false,
                        MaxLights = 16,
                        LODDistance = 0.75f,
                        OcclusionCullingEnabled = true,
                        FrustumCullingEnabled = true
                    };

                case QualityLevel.High:
                    return new QualitySettings
                    {
                        Level = QualityLevel.High,
                        ResolutionScalePercent = 100,
                        ShadowsEnabled = true,
                        ShadowResolution = 2048,
                        ShadowCascadeCount = 3,
                        SSAOEnabled = true,
                        SSREnabled = true,
                        TAAEnabled = true,
                        MotionBlurEnabled = true,
                        MaxLights = 64,
                        LODDistance = 1.0f,
                        OcclusionCullingEnabled = true,
                        FrustumCullingEnabled = true
                    };

                case QualityLevel.Ultra:
                    return new QualitySettings
                    {
                        Level = QualityLevel.Ultra,
                        ResolutionScalePercent = 100,
                        ShadowsEnabled = true,
                        ShadowResolution = 4096,
                        ShadowCascadeCount = 4,
                        SSAOEnabled = true,
                        SSREnabled = true,
                        TAAEnabled = true,
                        MotionBlurEnabled = true,
                        MaxLights = 128,
                        LODDistance = 1.5f,
                        OcclusionCullingEnabled = true,
                        FrustumCullingEnabled = true
                    };

                default:
                    return GetSettings(QualityLevel.Medium);
            }
        }

        /// <summary>
        /// Automatically selects quality level based on GPU capabilities.
        /// </summary>
        public static QualityLevel SelectQualityLevel(GraphicsCapabilities capabilities)
        {
            // Simple heuristic based on GPU capabilities
            // Can be enhanced with actual benchmarking
            if (capabilities.MaxTextureSize >= 8192 && capabilities.MaxAnisotropy >= 16)
            {
                return QualityLevel.Ultra;
            }
            else if (capabilities.MaxTextureSize >= 4096 && capabilities.MaxAnisotropy >= 8)
            {
                return QualityLevel.High;
            }
            else if (capabilities.MaxTextureSize >= 2048)
            {
                return QualityLevel.Medium;
            }
            else
            {
                return QualityLevel.Low;
            }
        }
    }
}


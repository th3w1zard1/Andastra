using System;
using Andastra.Runtime.Graphics.Common.Rendering;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Base interface for all post-processing effects.
    /// </summary>
    public interface IPostProcessingEffect : IDisposable
    {
        /// <summary>
        /// Whether the effect is enabled.
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Effect priority for ordering (lower = earlier in pipeline).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Effect name for debugging.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Updates effect parameters from settings.
        /// </summary>
        void UpdateSettings(RenderSettings settings);
    }

    /// <summary>
    /// Bloom post-processing effect interface.
    /// </summary>
    public interface IBloomEffect : IPostProcessingEffect
    {
        /// <summary>
        /// Brightness threshold for bloom extraction.
        /// </summary>
        float Threshold { get; set; }

        /// <summary>
        /// Bloom intensity multiplier.
        /// </summary>
        float Intensity { get; set; }

        /// <summary>
        /// Number of blur passes.
        /// </summary>
        int BlurPasses { get; set; }
    }

    /// <summary>
    /// Screen-space ambient occlusion effect interface.
    /// </summary>
    public interface ISsaoEffect : IPostProcessingEffect
    {
        /// <summary>
        /// Sample radius in world units.
        /// </summary>
        float Radius { get; set; }

        /// <summary>
        /// AO intensity/power.
        /// </summary>
        float Power { get; set; }

        /// <summary>
        /// Number of samples per pixel.
        /// </summary>
        int SampleCount { get; set; }
    }

    /// <summary>
    /// Screen-space reflections effect interface.
    /// </summary>
    public interface ISsrEffect : IPostProcessingEffect
    {
        /// <summary>
        /// Maximum ray distance.
        /// </summary>
        float MaxDistance { get; set; }

        /// <summary>
        /// Ray step size.
        /// </summary>
        float StepSize { get; set; }

        /// <summary>
        /// Maximum iterations for ray marching.
        /// </summary>
        int MaxIterations { get; set; }

        /// <summary>
        /// Reflection intensity.
        /// </summary>
        float Intensity { get; set; }
    }

    /// <summary>
    /// Temporal anti-aliasing effect interface.
    /// </summary>
    public interface ITemporalAaEffect : IPostProcessingEffect
    {
        /// <summary>
        /// Jitter scale for sub-pixel sampling.
        /// </summary>
        float JitterScale { get; set; }

        /// <summary>
        /// History blend factor.
        /// </summary>
        float BlendFactor { get; set; }

        /// <summary>
        /// Whether motion vectors are available.
        /// </summary>
        bool UseMotionVectors { get; set; }
    }

    /// <summary>
    /// Motion blur effect interface.
    /// </summary>
    public interface IMotionBlurEffect : IPostProcessingEffect
    {
        /// <summary>
        /// Motion blur intensity/scale.
        /// </summary>
        float Intensity { get; set; }

        /// <summary>
        /// Maximum blur velocity in pixels.
        /// </summary>
        float MaxVelocity { get; set; }

        /// <summary>
        /// Number of samples for blur.
        /// </summary>
        int SampleCount { get; set; }
    }

    /// <summary>
    /// Tone mapping effect interface.
    /// </summary>
    public interface IToneMappingEffect : IPostProcessingEffect
    {
        /// <summary>
        /// Exposure value.
        /// </summary>
        float Exposure { get; set; }

        /// <summary>
        /// Gamma correction value.
        /// </summary>
        float Gamma { get; set; }

        /// <summary>
        /// White point for HDR.
        /// </summary>
        float WhitePoint { get; set; }
    }

    /// <summary>
    /// Color grading effect interface.
    /// </summary>
    public interface IColorGradingEffect : IPostProcessingEffect
    {
        /// <summary>
        /// LUT texture path or handle.
        /// </summary>
        object LutTexture { get; set; }

        /// <summary>
        /// LUT blend strength.
        /// </summary>
        float Strength { get; set; }

        /// <summary>
        /// Contrast adjustment.
        /// </summary>
        float Contrast { get; set; }

        /// <summary>
        /// Saturation adjustment.
        /// </summary>
        float Saturation { get; set; }
    }
}


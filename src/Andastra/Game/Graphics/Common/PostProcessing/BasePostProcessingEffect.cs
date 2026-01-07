using System;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Rendering;

namespace Andastra.Runtime.Graphics.Common.PostProcessing
{
    /// <summary>
    /// Abstract base class for all post-processing effects.
    /// Contains shared logic that both MonoGame and Stride implementations inherit.
    /// </summary>
    public abstract class BasePostProcessingEffect : IPostProcessingEffect
    {
        protected bool _enabled;
        protected bool _initialized;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public abstract int Priority { get; }
        public abstract string Name { get; }

        public abstract void UpdateSettings(RenderSettings settings);

        public virtual void Dispose()
        {
            OnDispose();
            _initialized = false;
        }

        protected virtual void OnDispose() { }
    }

    /// <summary>
    /// Abstract base class for bloom post-processing effect.
    /// </summary>
    public abstract class BaseBloomEffect : BasePostProcessingEffect, IBloomEffect
    {
        protected float _threshold;
        protected float _intensity;
        protected int _blurPasses;

        public override int Priority => 100; // After main rendering
        public override string Name => "Bloom";

        public float Threshold
        {
            get => _threshold;
            set => _threshold = Math.Max(0.0f, value);
        }

        public float Intensity
        {
            get => _intensity;
            set => _intensity = Math.Max(0.0f, value);
        }

        public int BlurPasses
        {
            get => _blurPasses;
            set => _blurPasses = Math.Max(1, Math.Min(8, value));
        }

        protected BaseBloomEffect()
        {
            _threshold = 1.0f;
            _intensity = 1.0f;
            _blurPasses = 3;
        }

        public override void UpdateSettings(RenderSettings settings)
        {
            _enabled = settings.BloomEnabled;
            _intensity = settings.BloomIntensity;
            _threshold = settings.BloomThreshold;
        }
    }

    /// <summary>
    /// Abstract base class for SSAO post-processing effect.
    /// </summary>
    public abstract class BaseSsaoEffect : BasePostProcessingEffect, ISsaoEffect
    {
        protected float _radius;
        protected float _power;
        protected int _sampleCount;

        public override int Priority => 50; // Before lighting
        public override string Name => "SSAO";

        public float Radius
        {
            get => _radius;
            set => _radius = Math.Max(0.01f, value);
        }

        public float Power
        {
            get => _power;
            set => _power = Math.Max(0.1f, value);
        }

        public int SampleCount
        {
            get => _sampleCount;
            set => _sampleCount = Math.Max(4, Math.Min(64, value));
        }

        protected BaseSsaoEffect()
        {
            _radius = 0.5f;
            _power = 2.0f;
            _sampleCount = 16;
        }

        public override void UpdateSettings(RenderSettings settings)
        {
            _enabled = settings.AmbientOcclusion != Enums.AmbientOcclusionMode.Disabled;
            _power = settings.AmbientOcclusionIntensity;
        }
    }

    /// <summary>
    /// Abstract base class for SSR post-processing effect.
    /// </summary>
    public abstract class BaseSsrEffect : BasePostProcessingEffect, ISsrEffect
    {
        protected float _maxDistance;
        protected float _stepSize;
        protected int _maxIterations;
        protected float _intensity;

        public override int Priority => 60; // After G-buffer
        public override string Name => "SSR";

        public float MaxDistance
        {
            get => _maxDistance;
            set => _maxDistance = Math.Max(1f, value);
        }

        public float StepSize
        {
            get => _stepSize;
            set => _stepSize = Math.Max(0.01f, value);
        }

        public int MaxIterations
        {
            get => _maxIterations;
            set => _maxIterations = Math.Max(16, Math.Min(256, value));
        }

        public float Intensity
        {
            get => _intensity;
            set => _intensity = Math.Max(0f, Math.Min(1f, value));
        }

        protected BaseSsrEffect()
        {
            _maxDistance = 100f;
            _stepSize = 0.1f;
            _maxIterations = 64;
            _intensity = 1.0f;
        }

        public override void UpdateSettings(RenderSettings settings)
        {
            _enabled = settings.ScreenSpaceReflections;
            // Map reflection quality 0-4 to iterations
            _maxIterations = 32 + (settings.ReflectionQuality * 32);
        }
    }

    /// <summary>
    /// Abstract base class for TAA post-processing effect.
    /// </summary>
    public abstract class BaseTemporalAaEffect : BasePostProcessingEffect, ITemporalAaEffect
    {
        protected float _jitterScale;
        protected float _blendFactor;
        protected bool _useMotionVectors;

        public override int Priority => 200; // After all other effects
        public override string Name => "TAA";

        public float JitterScale
        {
            get => _jitterScale;
            set => _jitterScale = Math.Max(0.1f, Math.Min(2f, value));
        }

        public float BlendFactor
        {
            get => _blendFactor;
            set => _blendFactor = Math.Max(0f, Math.Min(1f, value));
        }

        public bool UseMotionVectors
        {
            get => _useMotionVectors;
            set => _useMotionVectors = value;
        }

        protected BaseTemporalAaEffect()
        {
            _jitterScale = 1.0f;
            _blendFactor = 0.9f;
            _useMotionVectors = true;
        }

        public override void UpdateSettings(RenderSettings settings)
        {
            _enabled = settings.AntiAliasing == Enums.AntiAliasingMode.TAA;
        }
    }

    /// <summary>
    /// Abstract base class for motion blur post-processing effect.
    /// </summary>
    public abstract class BaseMotionBlurEffect : BasePostProcessingEffect, IMotionBlurEffect
    {
        protected float _intensity;
        protected float _maxVelocity;
        protected int _sampleCount;

        public override int Priority => 150; // After bloom, before TAA
        public override string Name => "MotionBlur";

        public float Intensity
        {
            get => _intensity;
            set => _intensity = Math.Max(0f, Math.Min(2f, value));
        }

        public float MaxVelocity
        {
            get => _maxVelocity;
            set => _maxVelocity = Math.Max(1f, value);
        }

        public int SampleCount
        {
            get => _sampleCount;
            set => _sampleCount = Math.Max(4, Math.Min(32, value));
        }

        protected BaseMotionBlurEffect()
        {
            _intensity = 0.5f;
            _maxVelocity = 40f;
            _sampleCount = 8;
        }

        public override void UpdateSettings(RenderSettings settings)
        {
            _enabled = settings.MotionBlurEnabled;
            _intensity = settings.MotionBlurIntensity;
        }
    }

    /// <summary>
    /// Abstract base class for tone mapping post-processing effect.
    /// </summary>
    public abstract class BaseToneMappingEffect : BasePostProcessingEffect, IToneMappingEffect
    {
        protected float _exposure;
        protected float _gamma;
        protected float _whitePoint;

        public override int Priority => 250; // Near end of pipeline
        public override string Name => "ToneMapping";

        public float Exposure
        {
            get => _exposure;
            set => _exposure = Math.Max(-10f, Math.Min(10f, value));
        }

        public float Gamma
        {
            get => _gamma;
            set => _gamma = Math.Max(1f, Math.Min(3f, value));
        }

        public float WhitePoint
        {
            get => _whitePoint;
            set => _whitePoint = Math.Max(0.1f, value);
        }

        protected BaseToneMappingEffect()
        {
            _exposure = 0f;
            _gamma = 2.2f;
            _whitePoint = 11.2f;
        }

        public override void UpdateSettings(RenderSettings settings)
        {
            _enabled = settings.Tonemapper != Enums.TonemapOperator.None;
            _exposure = settings.Exposure;
            _gamma = settings.Gamma;
        }
    }

    /// <summary>
    /// Abstract base class for color grading post-processing effect.
    /// </summary>
    public abstract class BaseColorGradingEffect : BasePostProcessingEffect, IColorGradingEffect
    {
        protected object _lutTexture;
        protected float _strength;
        protected float _contrast;
        protected float _saturation;

        public override int Priority => 260; // After tone mapping
        public override string Name => "ColorGrading";

        public object LutTexture
        {
            get => _lutTexture;
            set => _lutTexture = value;
        }

        public float Strength
        {
            get => _strength;
            set => _strength = Math.Max(0f, Math.Min(1f, value));
        }

        public float Contrast
        {
            get => _contrast;
            set => _contrast = Math.Max(-1f, Math.Min(1f, value));
        }

        public float Saturation
        {
            get => _saturation;
            set => _saturation = Math.Max(0f, Math.Min(2f, value));
        }

        protected BaseColorGradingEffect()
        {
            _strength = 1.0f;
            _contrast = 0f;
            _saturation = 1.0f;
        }

        public override void UpdateSettings(RenderSettings settings)
        {
            _enabled = !string.IsNullOrEmpty(settings.ColorGradingLut);
        }
    }
}


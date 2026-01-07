using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Rendering;

namespace Andastra.Runtime.Graphics.Common.Upscaling
{
    /// <summary>
    /// Abstract base class for upscaling/super-resolution systems.
    /// Provides shared logic for DLSS, FSR, XeSS implementations.
    /// </summary>
    public abstract class BaseUpscalingSystem : IUpscalingSystem
    {
        protected bool _initialized;
        protected bool _enabled;
        protected UpscalingQuality _quality;
        protected float _sharpness;
        protected RenderSettings _settings;

        public abstract bool IsAvailable { get; }
        public bool IsEnabled => _enabled && _initialized;
        public abstract string Name { get; }
        public abstract string Version { get; }

        protected BaseUpscalingSystem()
        {
            _quality = UpscalingQuality.Quality;
            _sharpness = 0.5f;
        }

        public virtual bool Initialize(RenderSettings settings)
        {
            if (_initialized) return true;

            if (!IsAvailable)
            {
                Console.WriteLine($"[{Name}] Not available on this hardware");
                return false;
            }

            _settings = settings;

            if (!InitializeInternal())
            {
                Console.WriteLine($"[{Name}] Failed to initialize");
                return false;
            }

            _initialized = true;
            _enabled = true;

            Console.WriteLine($"[{Name}] Initialized successfully (v{Version})");

            return true;
        }

        public virtual void Shutdown()
        {
            if (!_initialized) return;

            ShutdownInternal();

            _initialized = false;
            _enabled = false;

            Console.WriteLine($"[{Name}] Shutdown complete");
        }

        public virtual void SetQualityMode(UpscalingQuality quality)
        {
            _quality = quality;
            OnQualityModeChanged(quality);
        }

        public virtual (int width, int height) GetRenderResolution(int targetWidth, int targetHeight)
        {
            float scale = GetScaleFactor();
            int renderWidth = (int)(targetWidth * scale);
            int renderHeight = (int)(targetHeight * scale);

            // Ensure even dimensions for most upscalers
            renderWidth = (renderWidth + 1) & ~1;
            renderHeight = (renderHeight + 1) & ~1;

            return (renderWidth, renderHeight);
        }

        public virtual float GetScaleFactor()
        {
            switch (_quality)
            {
                case UpscalingQuality.Native: return 1.0f;
                case UpscalingQuality.Quality: return 0.67f;
                case UpscalingQuality.Balanced: return 0.59f;
                case UpscalingQuality.Performance: return 0.50f;
                case UpscalingQuality.UltraPerformance: return 0.33f;
                default: return 1.0f;
            }
        }

        public virtual void SetSharpness(float sharpness)
        {
            _sharpness = Math.Max(0f, Math.Min(1f, sharpness));
            OnSharpnessChanged(_sharpness);
        }

        public virtual void Dispose()
        {
            Shutdown();
        }

        protected abstract bool InitializeInternal();
        protected abstract void ShutdownInternal();
        protected virtual void OnQualityModeChanged(UpscalingQuality quality) { }
        protected virtual void OnSharpnessChanged(float sharpness) { }
    }

    /// <summary>
    /// Abstract base class for NVIDIA DLSS implementations.
    /// </summary>
    public abstract class BaseDlssSystem : BaseUpscalingSystem, IDlssSystem
    {
        protected DlssMode _mode;
        protected bool _rayReconstructionEnabled;
        protected bool _frameGenerationEnabled;

        public override string Name => "DLSS";

        public DlssMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                OnModeChanged(value);
            }
        }

        public abstract bool RayReconstructionAvailable { get; }
        public abstract bool FrameGenerationAvailable { get; }

        public bool RayReconstructionEnabled
        {
            get => _rayReconstructionEnabled;
            set
            {
                if (RayReconstructionAvailable)
                {
                    _rayReconstructionEnabled = value;
                    OnRayReconstructionChanged(value);
                }
            }
        }

        public bool FrameGenerationEnabled
        {
            get => _frameGenerationEnabled;
            set
            {
                if (FrameGenerationAvailable)
                {
                    _frameGenerationEnabled = value;
                    OnFrameGenerationChanged(value);
                }
            }
        }

        public override float GetScaleFactor()
        {
            switch (_mode)
            {
                case DlssMode.Off: return 1.0f;
                case DlssMode.DLAA: return 1.0f;
                case DlssMode.Quality: return 0.67f;
                case DlssMode.Balanced: return 0.58f;
                case DlssMode.Performance: return 0.50f;
                case DlssMode.UltraPerformance: return 0.33f;
                default: return 1.0f;
            }
        }

        protected virtual void OnModeChanged(DlssMode mode) { }
        protected virtual void OnRayReconstructionChanged(bool enabled) { }
        protected virtual void OnFrameGenerationChanged(bool enabled) { }
    }

    /// <summary>
    /// Abstract base class for AMD FSR implementations.
    /// Supports FSR 1.0, 2.0, 2.1, 2.2, 3.0, and 3.1.
    /// Based on AMD FidelityFX SDK: https://gpuopen.com/fidelityfx-superresolution/
    /// </summary>
    public abstract class BaseFsrSystem : BaseUpscalingSystem, IFsrSystem
    {
        protected FsrMode _mode;
        protected bool _frameGenerationEnabled;

        public override string Name => "FSR";

        public FsrMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                OnModeChanged(value);
            }
        }

        public abstract int FsrVersion { get; }
        public abstract bool FrameGenerationAvailable { get; }

        public bool FrameGenerationEnabled
        {
            get => _frameGenerationEnabled;
            set
            {
                if (FrameGenerationAvailable)
                {
                    _frameGenerationEnabled = value;
                    OnFrameGenerationChanged(value);
                }
            }
        }

        public override float GetScaleFactor()
        {
            switch (_mode)
            {
                case FsrMode.Off: return 1.0f;
                case FsrMode.Quality: return 0.67f;
                case FsrMode.Balanced: return 0.59f;
                case FsrMode.Performance: return 0.50f;
                case FsrMode.UltraPerformance: return 0.33f;
                default: return 1.0f;
            }
        }

        protected virtual void OnModeChanged(FsrMode mode) { }
        protected virtual void OnFrameGenerationChanged(bool enabled) { }
    }

    /// <summary>
    /// Abstract base class for Intel XeSS implementations.
    /// Supports XeSS 1.0, 1.1, 1.2, and 1.3.
    /// Based on Intel XeSS SDK: https://www.intel.com/content/www/us/en/developer/articles/technical/xess.html
    /// </summary>
    public abstract class BaseXeSSSystem : BaseUpscalingSystem, IXeSSSystem
    {
        protected XeSSMode _mode;
        protected bool _dpaEnabled;

        public override string Name => "XeSS";

        public XeSSMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                OnModeChanged(value);
            }
        }

        public abstract int XeSSVersion { get; }
        public abstract bool DpaAvailable { get; }

        public bool DpaEnabled
        {
            get => _dpaEnabled;
            set
            {
                if (DpaAvailable)
                {
                    _dpaEnabled = value;
                    OnDpaChanged(value);
                }
            }
        }

        public override float GetScaleFactor()
        {
            switch (_mode)
            {
                case XeSSMode.Off: return 1.0f;
                case XeSSMode.Quality: return 0.67f;
                case XeSSMode.Balanced: return 0.59f;
                case XeSSMode.Performance: return 0.50f;
                case XeSSMode.UltraPerformance: return 0.33f;
                default: return 1.0f;
            }
        }

        protected virtual void OnModeChanged(XeSSMode mode) { }
        protected virtual void OnDpaChanged(bool enabled) { }
    }
}


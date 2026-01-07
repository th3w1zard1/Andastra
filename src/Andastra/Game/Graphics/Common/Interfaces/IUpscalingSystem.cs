using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Rendering;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Interface for upscaling/super-resolution systems.
    /// Supports NVIDIA DLSS, AMD FSR, Intel XeSS, and others.
    /// </summary>
    public interface IUpscalingSystem : IDisposable
    {
        /// <summary>
        /// Whether the upscaling system is available on current hardware.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Whether the upscaling system is currently enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Upscaling system name (DLSS, FSR, XeSS, etc).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Current version of the upscaling system.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the upscaling system.
        /// </summary>
        bool Initialize(RenderSettings settings);

        /// <summary>
        /// Shuts down the upscaling system.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Sets the upscaling quality mode.
        /// </summary>
        void SetQualityMode(UpscalingQuality quality);

        /// <summary>
        /// Gets the render resolution for the current quality mode.
        /// </summary>
        (int width, int height) GetRenderResolution(int targetWidth, int targetHeight);

        /// <summary>
        /// Gets the upscaling scale factor.
        /// </summary>
        float GetScaleFactor();

        /// <summary>
        /// Sets the sharpness parameter (0-1).
        /// </summary>
        void SetSharpness(float sharpness);
    }

    /// <summary>
    /// NVIDIA DLSS (Deep Learning Super Sampling) interface.
    /// </summary>
    public interface IDlssSystem : IUpscalingSystem
    {
        /// <summary>
        /// DLSS mode.
        /// </summary>
        DlssMode Mode { get; set; }

        /// <summary>
        /// Whether DLSS Ray Reconstruction is available.
        /// </summary>
        bool RayReconstructionAvailable { get; }

        /// <summary>
        /// Whether DLSS Frame Generation is available.
        /// </summary>
        bool FrameGenerationAvailable { get; }

        /// <summary>
        /// Enables/disables DLSS Ray Reconstruction.
        /// </summary>
        bool RayReconstructionEnabled { get; set; }

        /// <summary>
        /// Enables/disables DLSS Frame Generation.
        /// </summary>
        bool FrameGenerationEnabled { get; set; }
    }

    /// <summary>
    /// AMD FSR (FidelityFX Super Resolution) interface.
    /// Supports FSR 1.0, 2.0, 2.1, 2.2, 3.0, 3.1, and 4.0 (RDNA4).
    /// </summary>
    public interface IFsrSystem : IUpscalingSystem
    {
        /// <summary>
        /// FSR mode.
        /// </summary>
        FsrMode Mode { get; set; }

        /// <summary>
        /// FSR version (1, 2, 3, 4).
        /// </summary>
        int FsrVersion { get; }

        /// <summary>
        /// Whether FSR 3/4 Frame Generation is available.
        /// </summary>
        bool FrameGenerationAvailable { get; }

        /// <summary>
        /// Enables/disables FSR Frame Generation.
        /// </summary>
        bool FrameGenerationEnabled { get; set; }
    }

    /// <summary>
    /// Intel XeSS (Xe Super Sampling) interface.
    /// Supports XeSS 1.0, 1.1, 1.2, and 1.3.
    /// </summary>
    public interface IXeSSSystem : IUpscalingSystem
    {
        /// <summary>
        /// XeSS mode.
        /// </summary>
        XeSSMode Mode { get; set; }

        /// <summary>
        /// XeSS version (1).
        /// </summary>
        int XeSSVersion { get; }

        /// <summary>
        /// Whether DPA (Deep Performance Analyzer) is available.
        /// </summary>
        bool DpaAvailable { get; }

        /// <summary>
        /// Enables/disables DPA.
        /// </summary>
        bool DpaEnabled { get; set; }
    }

    /// <summary>
    /// Upscaling quality presets.
    /// </summary>
    public enum UpscalingQuality
    {
        /// <summary>
        /// Native resolution (no upscaling).
        /// </summary>
        Native,

        /// <summary>
        /// Quality mode (~67% render resolution).
        /// </summary>
        Quality,

        /// <summary>
        /// Balanced mode (~59% render resolution).
        /// </summary>
        Balanced,

        /// <summary>
        /// Performance mode (~50% render resolution).
        /// </summary>
        Performance,

        /// <summary>
        /// Ultra Performance mode (~33% render resolution).
        /// </summary>
        UltraPerformance
    }
}


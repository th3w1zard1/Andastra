using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Interface for graphics backends that support temporal upsampling technologies.
    /// Temporal upsampling uses data from multiple frames to reconstruct higher-resolution images,
    /// providing better quality than spatial upsampling alone.
    ///
    /// Technologies:
    /// - Temporal Super Resolution (TSR) - Unreal Engine 5
    /// - FSR 2/3 temporal upsampling
    /// - DLSS 2/3 temporal upsampling
    /// - Custom temporal reconstruction
    ///
    /// Based on Unreal Engine TSR: https://dev.epicgames.com/documentation/en-us/unreal-engine/temporal-super-resolution-in-unreal-engine
    /// </summary>
    public interface ITemporalUpsamplingBackend : ILowLevelBackend
    {
        /// <summary>
        /// Whether temporal upsampling is available and supported.
        /// </summary>
        bool TemporalUpsamplingAvailable { get; }

        /// <summary>
        /// Creates a temporal upsampling context.
        /// </summary>
        /// <param name="inputWidth">Input render resolution width.</param>
        /// <param name="inputHeight">Input render resolution height.</param>
        /// <param name="outputWidth">Output display resolution width.</param>
        /// <param name="outputHeight">Output display resolution height.</param>
        /// <param name="format">Color format for input/output textures.</param>
        /// <returns>Handle to the temporal upsampling context, or IntPtr.Zero on failure.</returns>
        IntPtr CreateTemporalUpsampler(int inputWidth, int inputHeight, int outputWidth, int outputHeight, TextureFormat format);

        /// <summary>
        /// Applies temporal upsampling to the current frame.
        /// </summary>
        /// <param name="context">Temporal upsampling context handle.</param>
        /// <param name="inputColor">Input color texture (low resolution).</param>
        /// <param name="inputDepth">Input depth buffer.</param>
        /// <param name="motionVectors">Per-pixel motion vectors (2D screen space).</param>
        /// <param name="historyColor">Previous frame's upsampled color (for temporal accumulation).</param>
        /// <param name="historyDepth">Previous frame's depth (for temporal accumulation).</param>
        /// <param name="exposure">Current frame exposure value.</param>
        /// <param name="deltaTime">Time delta since last frame.</param>
        /// <returns>Handle to the upsampled output texture.</returns>
        IntPtr UpsampleTemporal(IntPtr context, IntPtr inputColor, IntPtr inputDepth, IntPtr motionVectors,
            IntPtr historyColor, IntPtr historyDepth, float exposure, float deltaTime);

        /// <summary>
        /// Sets temporal upsampling quality settings.
        /// </summary>
        /// <param name="context">Temporal upsampling context handle.</param>
        /// <param name="settings">Quality and algorithm settings.</param>
        void SetTemporalUpsamplingSettings(IntPtr context, TemporalUpsamplingSettings settings);

        /// <summary>
        /// Gets the current temporal upsampling settings.
        /// </summary>
        /// <param name="context">Temporal upsampling context handle.</param>
        /// <returns>Current settings.</returns>
        TemporalUpsamplingSettings GetTemporalUpsamplingSettings(IntPtr context);
    }

    /// <summary>
    /// Temporal upsampling quality and algorithm settings.
    /// </summary>
    public struct TemporalUpsamplingSettings
    {
        /// <summary>
        /// Quality preset (Cinematic, High, Medium, Low).
        /// </summary>
        public TemporalUpsamplingQuality Quality;

        /// <summary>
        /// Screen percentage for history buffer (1.0 = 100%, 2.0 = 200%).
        /// Higher values improve quality but use more memory.
        /// </summary>
        public float HistoryScreenPercentage;

        /// <summary>
        /// Motion vector weight clamping sample count.
        /// Higher values reduce ghosting but may reduce temporal stability.
        /// </summary>
        public float VelocityWeightClampingSampleCount;

        /// <summary>
        /// Whether to use reactive mask for better disocclusion handling.
        /// </summary>
        public bool UseReactiveMask;

        /// <summary>
        /// Whether to use separate translucency rendering.
        /// </summary>
        public bool UseSeparateTranslucency;

        /// <summary>
        /// Sharpness adjustment (0.0 = no sharpening, 1.0 = maximum sharpening).
        /// </summary>
        public float Sharpness;
    }

    /// <summary>
    /// Temporal upsampling quality presets.
    /// </summary>
    public enum TemporalUpsamplingQuality
    {
        /// <summary>
        /// Low quality - fastest, lowest memory usage.
        /// </summary>
        Low,

        /// <summary>
        /// Medium quality - balanced performance and quality.
        /// </summary>
        Medium,

        /// <summary>
        /// High quality - better quality, higher memory usage.
        /// </summary>
        High,

        /// <summary>
        /// Cinematic quality - best quality, highest memory usage.
        /// </summary>
        Cinematic
    }
}


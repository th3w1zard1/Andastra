using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Interface for graphics backends that support Variable Rate Shading (VRS).
    /// VRS allows shading at different rates across different regions of the screen.
    ///
    /// Based on DirectX 12 Variable Rate Shading: https://devblogs.microsoft.com/directx/variable-rate-shading-a-scalable-rendering-feature-for-dx12/
    /// </summary>
    public interface IVariableRateShadingBackend : ILowLevelBackend
    {
        /// <summary>
        /// Whether Variable Rate Shading is available and supported.
        /// </summary>
        bool VariableRateShadingAvailable { get; }

        /// <summary>
        /// Gets the VRS tier (1 or 2).
        /// Tier 1: Per-draw, per-primitive shading rates.
        /// Tier 2: Screen-space image-based shading rates, additional per-draw options.
        /// </summary>
        int VrsTier { get; }

        /// <summary>
        /// Sets the per-draw shading rate.
        /// </summary>
        /// <param name="rate">Shading rate combination (e.g., 1x1, 1x2, 2x1, 2x2, etc.).</param>
        void SetShadingRate(VrsShadingRate rate);

        /// <summary>
        /// Sets shading rate using a combination table (Tier 1 only).
        /// </summary>
        /// <param name="combiner0">Shading rate combiner for output merger.</param>
        /// <param name="combiner1">Shading rate combiner for primitive.</param>
        /// <param name="rate">Base shading rate.</param>
        void SetShadingRateCombiner(VrsCombiner combiner0, VrsCombiner combiner1, VrsShadingRate rate);

        /// <summary>
        /// Sets per-primitive shading rate (Tier 1 only).
        /// Requires vertex/amplification shader to output SV_ShadingRate.
        /// </summary>
        /// <param name="enable">Whether to enable per-primitive shading rate.</param>
        void SetPerPrimitiveShadingRate(bool enable);

        /// <summary>
        /// Sets screen-space shading rate image (Tier 2 only).
        /// </summary>
        /// <param name="shadingRateImage">Texture containing per-pixel shading rates.</param>
        /// <param name="width">Image width in tiles.</param>
        /// <param name="height">Image height in tiles.</param>
        void SetShadingRateImage(IntPtr shadingRateImage, int width, int height);
    }

    /// <summary>
    /// Variable Rate Shading rate combinations.
    /// Based on D3D12_SHADING_RATE enum.
    /// </summary>
    public enum VrsShadingRate
    {
        /// <summary>
        /// 1 pixel per sample (1x1).
        /// </summary>
        Rate1x1 = 0,

        /// <summary>
        /// 1 pixel horizontally, 2 pixels vertically (1x2).
        /// </summary>
        Rate1x2 = 1,

        /// <summary>
        /// 2 pixels horizontally, 1 pixel vertically (2x1).
        /// </summary>
        Rate2x1 = 2,

        /// <summary>
        /// 2 pixels per sample (2x2).
        /// </summary>
        Rate2x2 = 3,

        /// <summary>
        /// 2 pixels horizontally, 4 pixels vertically (2x4).
        /// </summary>
        Rate2x4 = 4,

        /// <summary>
        /// 4 pixels horizontally, 2 pixels vertically (4x2).
        /// </summary>
        Rate4x2 = 5,

        /// <summary>
        /// 4 pixels per sample (4x4).
        /// </summary>
        Rate4x4 = 6
    }

    /// <summary>
    /// VRS combiner operation for combining shading rates.
    /// Based on D3D12_SHADING_RATE_COMBINER enum.
    /// </summary>
    public enum VrsCombiner
    {
        /// <summary>
        /// Passthrough (use the other input).
        /// </summary>
        Passthrough = 0,

        /// <summary>
        /// Override (use this input, ignore the other).
        /// </summary>
        Override = 1,

        /// <summary>
        /// Min (use minimum of both inputs).
        /// </summary>
        Min = 2,

        /// <summary>
        /// Max (use maximum of both inputs).
        /// </summary>
        Max = 3,

        /// <summary>
        /// Sum (add both inputs, clamp to max).
        /// </summary>
        Sum = 4
    }
}


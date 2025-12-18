using System;

namespace HolocronToolset.Windows.DesignerControls
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/designer_controls.py:79-116
    // Original: class InputAccelerator
    /// <summary>
    /// Provides acceleration curves for input.
    ///
    /// Makes precise movements easier while allowing fast movements
    /// when needed. Uses a power curve.
    /// </summary>
    public class InputAccelerator
    {
        private readonly float _power;
        private readonly float _threshold;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/designer_controls.py:86-94
        // Original: def __init__(self, power: float = 1.5, threshold: float = 2.0)
        /// <summary>
        /// Initialize the accelerator.
        /// </summary>
        /// <param name="power">Power for the acceleration curve. >1 = acceleration.</param>
        /// <param name="threshold">Input values below this use linear response.</param>
        public InputAccelerator(float power = 1.5f, float threshold = 2.0f)
        {
            _power = power;
            _threshold = threshold;
        }

        /// <summary>
        /// Gets the power value.
        /// </summary>
        public float Power => _power;

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Threshold => _threshold;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/designer_controls.py:96-116
        // Original: def accelerate(self, value: float) -> float
        /// <summary>
        /// Apply acceleration to an input value.
        /// </summary>
        /// <param name="value">Raw input value.</param>
        /// <returns>Accelerated value.</returns>
        public float Accelerate(float value)
        {
            float sign = value >= 0 ? 1.0f : -1.0f;
            float magnitude = Math.Abs(value);

            // Below threshold: linear response for precise control
            if (magnitude < _threshold)
            {
                return value;
            }

            // Above threshold: power curve for fast movements
            float excess = magnitude - _threshold;
            float acceleratedExcess = (float)Math.Pow(excess, _power);

            return sign * (_threshold + acceleratedExcess);
        }
    }
}


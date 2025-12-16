using System;

namespace BioWareEngines.MonoGame.PostProcessing
{
    /// <summary>
    /// Color grading system for artistic control.
    /// 
    /// Color grading adjusts the color and tone of rendered images to achieve
    /// specific artistic looks and cinematic effects.
    /// 
    /// Features:
    /// - Lift/Gamma/Gain controls
    /// - Color temperature/tint
    /// - Saturation adjustment
    /// - Look-up tables (LUTs)
    /// - Presets
    /// </summary>
    public class ColorGrading
    {
        private float _lift;
        private float _gamma;
        private float _gain;
        private float _temperature;
        private float _tint;
        private float _saturation;
        private float _contrast;

        /// <summary>
        /// Gets or sets the lift value (shadow adjustment).
        /// </summary>
        public float Lift
        {
            get { return _lift; }
            set { _lift = Math.Max(-1.0f, Math.Min(1.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the gamma value (mid-tone adjustment).
        /// </summary>
        public float Gamma
        {
            get { return _gamma; }
            set { _gamma = Math.Max(0.1f, Math.Min(5.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the gain value (highlight adjustment).
        /// </summary>
        public float Gain
        {
            get { return _gain; }
            set { _gain = Math.Max(0.0f, Math.Min(5.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the color temperature (K).
        /// </summary>
        public float Temperature
        {
            get { return _temperature; }
            set { _temperature = Math.Max(-100.0f, Math.Min(100.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the tint adjustment.
        /// </summary>
        public float Tint
        {
            get { return _tint; }
            set { _tint = Math.Max(-100.0f, Math.Min(100.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the saturation adjustment.
        /// </summary>
        public float Saturation
        {
            get { return _saturation; }
            set { _saturation = Math.Max(-1.0f, Math.Min(1.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the contrast adjustment.
        /// </summary>
        public float Contrast
        {
            get { return _contrast; }
            set { _contrast = Math.Max(-1.0f, Math.Min(1.0f, value)); }
        }

        /// <summary>
        /// Initializes a new color grading processor.
        /// </summary>
        public ColorGrading()
        {
            _lift = 0.0f;
            _gamma = 1.0f;
            _gain = 1.0f;
            _temperature = 0.0f;
            _tint = 0.0f;
            _saturation = 1.0f;
            _contrast = 0.0f;
        }
    }
}


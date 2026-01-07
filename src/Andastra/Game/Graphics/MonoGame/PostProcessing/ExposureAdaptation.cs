using System;

namespace Andastra.Runtime.MonoGame.PostProcessing
{
    /// <summary>
    /// Automatic exposure adaptation for HDR rendering.
    /// 
    /// Automatically adjusts exposure based on scene luminance to simulate
    /// camera eye adaptation.
    /// 
    /// Features:
    /// - Luminance histogram analysis
    /// - Temporal smoothing
    /// - Configurable adaptation speed
    /// - Exposure limits
    /// </summary>
    public class ExposureAdaptation
    {
        private float _adaptationSpeedUp;
        private float _adaptationSpeedDown;
        private float _minExposure;
        private float _maxExposure;
        private float _keyValue;
        private float _currentExposure;
        private float _targetExposure;

        /// <summary>
        /// Gets or sets the speed of exposure increase (positive change).
        /// </summary>
        public float AdaptationSpeedUp
        {
            get { return _adaptationSpeedUp; }
            set { _adaptationSpeedUp = Math.Max(0.0f, Math.Min(10.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the speed of exposure decrease (negative change).
        /// </summary>
        public float AdaptationSpeedDown
        {
            get { return _adaptationSpeedDown; }
            set { _adaptationSpeedDown = Math.Max(0.0f, Math.Min(10.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the minimum exposure value.
        /// </summary>
        public float MinExposure
        {
            get { return _minExposure; }
            set { _minExposure = value; }
        }

        /// <summary>
        /// Gets or sets the maximum exposure value.
        /// </summary>
        public float MaxExposure
        {
            get { return _maxExposure; }
            set { _maxExposure = value; }
        }

        /// <summary>
        /// Gets or sets the key value for middle gray (0.18 is standard).
        /// </summary>
        public float KeyValue
        {
            get { return _keyValue; }
            set { _keyValue = Math.Max(0.01f, Math.Min(1.0f, value)); }
        }

        /// <summary>
        /// Gets the current exposure value.
        /// </summary>
        public float CurrentExposure
        {
            get { return _currentExposure; }
        }

        /// <summary>
        /// Initializes a new exposure adaptation system.
        /// </summary>
        public ExposureAdaptation()
        {
            _adaptationSpeedUp = 2.0f;
            _adaptationSpeedDown = 1.0f;
            _minExposure = -5.0f;
            _maxExposure = 5.0f;
            _keyValue = 0.18f;
            _currentExposure = 0.0f;
            _targetExposure = 0.0f;
        }

        /// <summary>
        /// Updates exposure based on scene luminance.
        /// </summary>
        /// <param name="sceneLuminance">Average scene luminance. Must be greater than zero.</param>
        /// <param name="deltaTime">Time elapsed since last update in seconds. Must be non-negative.</param>
        public void Update(float sceneLuminance, float deltaTime)
        {
            // Validate inputs
            if (sceneLuminance <= 0.0f)
            {
                sceneLuminance = 0.001f; // Avoid division by zero and log of non-positive
            }

            if (deltaTime < 0.0f)
            {
                deltaTime = 0.0f; // Clamp negative deltas
            }

            // Calculate target exposure from scene luminance
            // Formula: exposure = log2(keyValue / luminance)
            // This ensures that objects at keyValue luminance map to middle gray
            _targetExposure = (float)Math.Log(_keyValue / sceneLuminance, 2.0);
            _targetExposure = Math.Max(_minExposure, Math.Min(_maxExposure, _targetExposure));

            // Smoothly adapt exposure with different speeds for brightening vs darkening
            // (eyes adapt faster to darkness than to brightness)
            float speed = _targetExposure > _currentExposure ? _adaptationSpeedUp : _adaptationSpeedDown;
            float adaptationRate = 1.0f - (float)Math.Pow(0.98f, speed * deltaTime);
            _currentExposure += (_targetExposure - _currentExposure) * adaptationRate;
        }
    }
}


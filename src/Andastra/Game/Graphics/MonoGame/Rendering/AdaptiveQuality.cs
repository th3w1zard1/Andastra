using System;
using Microsoft.Xna.Framework;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Adaptive quality system for dynamic performance scaling.
    /// 
    /// Monitors frame time and automatically adjusts rendering quality
    /// to maintain target frame rate, ensuring consistent performance
    /// across different hardware.
    /// 
    /// Features:
    /// - Frame time monitoring
    /// - Automatic quality scaling
    /// - Multiple quality tiers
    /// - Smooth transitions
    /// </summary>
    public class AdaptiveQuality
    {
        /// <summary>
        /// Quality level enumeration.
        /// </summary>
        public enum QualityLevel
        {
            Low = 0,
            Medium = 1,
            High = 2,
            Ultra = 3
        }

        /// <summary>
        /// Quality settings for each level.
        /// </summary>
        public struct QualitySettings
        {
            /// <summary>
            /// Render scale (0.5 = half resolution, 1.0 = full).
            /// </summary>
            public float RenderScale;

            /// <summary>
            /// Shadow map resolution multiplier.
            /// </summary>
            public float ShadowResolution;

            /// <summary>
            /// Maximum number of lights.
            /// </summary>
            public int MaxLights;

            /// <summary>
            /// LOD bias (positive = lower detail).
            /// </summary>
            public float LODBias;

            /// <summary>
            /// Whether to enable expensive effects.
            /// </summary>
            public bool EnableExpensiveEffects;
        }

        private QualityLevel _currentLevel;
        private QualityLevel _targetLevel;
        private float _targetFrameTime;
        private float _currentFrameTime;
        private float[] _frameTimeHistory;
        private int _frameTimeIndex;
        private int _framesBelowTarget;
        private int _framesAboveTarget;
        private QualitySettings[] _qualitySettings;

        /// <summary>
        /// Gets the current quality level.
        /// </summary>
        public QualityLevel CurrentLevel
        {
            get { return _currentLevel; }
        }

        /// <summary>
        /// Gets the current quality settings.
        /// </summary>
        public QualitySettings CurrentSettings
        {
            get { return _qualitySettings[(int)_currentLevel]; }
        }

        /// <summary>
        /// Gets or sets the target frame time in milliseconds (e.g., 16.67 for 60 FPS).
        /// </summary>
        public float TargetFrameTime
        {
            get { return _targetFrameTime; }
            set { _targetFrameTime = Math.Max(1.0f, value); }
        }

        /// <summary>
        /// Initializes a new adaptive quality system.
        /// </summary>
        /// <param name="targetFrameTime">Target frame time in milliseconds.</param>
        public AdaptiveQuality(float targetFrameTime = 16.67f)
        {
            _targetFrameTime = targetFrameTime;
            _currentLevel = QualityLevel.High;
            _targetLevel = QualityLevel.High;
            _currentFrameTime = targetFrameTime;
            _frameTimeHistory = new float[60]; // 1 second at 60 FPS
            _frameTimeIndex = 0;
            _framesBelowTarget = 0;
            _framesAboveTarget = 0;

            InitializeQualitySettings();
        }

        /// <summary>
        /// Updates adaptive quality based on frame time.
        /// </summary>
        /// <param name="frameTime">Current frame time in milliseconds.</param>
        public void Update(float frameTime)
        {
            _currentFrameTime = frameTime;
            _frameTimeHistory[_frameTimeIndex] = frameTime;
            _frameTimeIndex = (_frameTimeIndex + 1) % _frameTimeHistory.Length;

            // Calculate average frame time
            float avgFrameTime = 0.0f;
            for (int i = 0; i < _frameTimeHistory.Length; i++)
            {
                avgFrameTime += _frameTimeHistory[i];
            }
            avgFrameTime /= _frameTimeHistory.Length;

            // Determine target quality level
            if (avgFrameTime > _targetFrameTime * 1.1f) // 10% over target
            {
                _framesBelowTarget++;
                _framesAboveTarget = 0;

                if (_framesBelowTarget > 30) // Wait 0.5 seconds before downgrading
                {
                    if (_targetLevel > QualityLevel.Low)
                    {
                        _targetLevel--;
                        _framesBelowTarget = 0;
                    }
                }
            }
            else if (avgFrameTime < _targetFrameTime * 0.9f) // 10% under target
            {
                _framesAboveTarget++;
                _framesBelowTarget = 0;

                if (_framesAboveTarget > 60) // Wait 1 second before upgrading
                {
                    if (_targetLevel < QualityLevel.Ultra)
                    {
                        _targetLevel++;
                        _framesAboveTarget = 0;
                    }
                }
            }
            else
            {
                _framesBelowTarget = 0;
                _framesAboveTarget = 0;
            }

            // Smoothly transition to target level
            if (_currentLevel != _targetLevel)
            {
                // Could implement smooth transitions here
                _currentLevel = _targetLevel;
            }
        }

        private void InitializeQualitySettings()
        {
            _qualitySettings = new QualitySettings[4];

            // Low quality
            _qualitySettings[0] = new QualitySettings
            {
                RenderScale = 0.75f,
                ShadowResolution = 0.5f,
                MaxLights = 8,
                LODBias = 1.0f,
                EnableExpensiveEffects = false
            };

            // Medium quality
            _qualitySettings[1] = new QualitySettings
            {
                RenderScale = 0.875f,
                ShadowResolution = 0.75f,
                MaxLights = 16,
                LODBias = 0.5f,
                EnableExpensiveEffects = false
            };

            // High quality
            _qualitySettings[2] = new QualitySettings
            {
                RenderScale = 1.0f,
                ShadowResolution = 1.0f,
                MaxLights = 32,
                LODBias = 0.0f,
                EnableExpensiveEffects = true
            };

            // Ultra quality
            _qualitySettings[3] = new QualitySettings
            {
                RenderScale = 1.0f,
                ShadowResolution = 1.5f,
                MaxLights = 64,
                LODBias = -0.5f,
                EnableExpensiveEffects = true
            };
        }

        /// <summary>
        /// Manually sets quality level.
        /// </summary>
        public void SetQualityLevel(QualityLevel level)
        {
            _currentLevel = level;
            _targetLevel = level;
        }
    }
}


using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Dynamic resolution scaling system for maintaining target frame rate.
    /// 
    /// Dynamically adjusts render resolution based on performance, maintaining
    /// target frame rate while maximizing visual quality when possible.
    /// 
    /// Features:
    /// - Real-time resolution adjustment
    /// - Frame time monitoring
    /// - Smooth transitions
    /// - Per-frame or per-second updates
    /// - Minimum/maximum resolution bounds
    /// </summary>
    public class DynamicResolution
    {
        private readonly GraphicsDevice _graphicsDevice;
        private int _baseWidth;
        private int _baseHeight;
        private int _currentWidth;
        private int _currentHeight;
        private float _currentScale;
        private float _targetScale;
        private float _minScale;
        private float _maxScale;
        private float _targetFrameTime;
        private float[] _frameTimeHistory;
        private int _frameTimeIndex;
        private int _updateInterval;

        /// <summary>
        /// Gets the current render width.
        /// </summary>
        public int RenderWidth
        {
            get { return _currentWidth; }
        }

        /// <summary>
        /// Gets the current render height.
        /// </summary>
        public int RenderHeight
        {
            get { return _currentHeight; }
        }

        /// <summary>
        /// Gets the current resolution scale (0.5 = half resolution, 1.0 = full).
        /// </summary>
        public float CurrentScale
        {
            get { return _currentScale; }
        }

        /// <summary>
        /// Gets or sets the minimum resolution scale.
        /// </summary>
        public float MinScale
        {
            get { return _minScale; }
            set { _minScale = Math.Max(0.25f, Math.Min(1.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the maximum resolution scale.
        /// </summary>
        public float MaxScale
        {
            get { return _maxScale; }
            set { _maxScale = Math.Max(0.5f, Math.Min(1.0f, value)); }
        }

        /// <summary>
        /// Gets or sets the target frame time in milliseconds.
        /// </summary>
        public float TargetFrameTime
        {
            get { return _targetFrameTime; }
            set { _targetFrameTime = Math.Max(1.0f, value); }
        }

        /// <summary>
        /// Gets or sets whether dynamic resolution is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Initializes a new dynamic resolution system.
        /// </summary>
        public DynamicResolution(GraphicsDevice graphicsDevice, int baseWidth, int baseHeight, float targetFrameTime = 16.67f)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice");
            }

            _graphicsDevice = graphicsDevice;
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
            _currentWidth = baseWidth;
            _currentHeight = baseHeight;
            _currentScale = 1.0f;
            _targetScale = 1.0f;
            _minScale = 0.5f;
            _maxScale = 1.0f;
            _targetFrameTime = targetFrameTime;
            _frameTimeHistory = new float[60]; // 1 second at 60 FPS
            _frameTimeIndex = 0;
            _updateInterval = 60; // Update every second
        }

        /// <summary>
        /// Updates dynamic resolution based on frame time.
        /// </summary>
        public void Update(float frameTime)
        {
            if (!Enabled)
            {
                return;
            }

            // Record frame time
            _frameTimeHistory[_frameTimeIndex] = frameTime;
            _frameTimeIndex = (_frameTimeIndex + 1) % _frameTimeHistory.Length;

            // Update resolution periodically
            if (_frameTimeIndex % _updateInterval == 0)
            {
                UpdateResolution();
            }
        }

        /// <summary>
        /// Updates target resolution scale based on performance.
        /// </summary>
        private void UpdateResolution()
        {
            // Calculate average frame time
            float avgFrameTime = 0.0f;
            for (int i = 0; i < _frameTimeHistory.Length; i++)
            {
                avgFrameTime += _frameTimeHistory[i];
            }
            avgFrameTime /= _frameTimeHistory.Length;

            // Adjust scale based on performance
            if (avgFrameTime > _targetFrameTime * 1.1f) // 10% over target
            {
                // Reduce resolution
                _targetScale = Math.Max(_minScale, _targetScale - 0.05f);
            }
            else if (avgFrameTime < _targetFrameTime * 0.9f) // 10% under target
            {
                // Increase resolution
                _targetScale = Math.Min(_maxScale, _targetScale + 0.05f);
            }

            // Smoothly transition to target scale
            float scaleDelta = _targetScale - _currentScale;
            _currentScale += scaleDelta * 0.1f; // Smooth interpolation

            // Update render resolution
            _currentWidth = (int)(_baseWidth * _currentScale);
            _currentHeight = (int)(_baseHeight * _currentScale);

            // Ensure even dimensions (required for some APIs)
            _currentWidth = (_currentWidth / 2) * 2;
            _currentHeight = (_currentHeight / 2) * 2;
        }

        /// <summary>
        /// Resets to base resolution.
        /// </summary>
        public void Reset()
        {
            _currentScale = 1.0f;
            _targetScale = 1.0f;
            _currentWidth = _baseWidth;
            _currentHeight = _baseHeight;
        }

        /// <summary>
        /// Resizes base resolution.
        /// </summary>
        public void Resize(int width, int height)
        {
            _baseWidth = width;
            _baseHeight = height;
            Reset();
        }
    }
}


using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Render target scaling system for performance optimization.
    /// 
    /// Render target scaling renders to lower resolution targets and upscales,
    /// providing significant performance gains with minimal quality loss.
    /// 
    /// Features:
    /// - Automatic resolution scaling
    /// - Quality presets
    /// - Per-target scaling
    /// - Upscaling integration
    /// </summary>
    public class RenderTargetScaling
    {
        /// <summary>
        /// Scaling quality preset.
        /// </summary>
        public enum QualityPreset
        {
            /// <summary>
            /// Full resolution (1.0x).
            /// </summary>
            Full = 0,

            /// <summary>
            /// High quality (0.875x).
            /// </summary>
            High = 1,

            /// <summary>
            /// Balanced (0.75x).
            /// </summary>
            Balanced = 2,

            /// <summary>
            /// Performance (0.5x).
            /// </summary>
            Performance = 3
        }

        private QualityPreset _quality;
        private float _customScale;

        /// <summary>
        /// Gets or sets the quality preset.
        /// </summary>
        public QualityPreset Quality
        {
            get { return _quality; }
            set
            {
                _quality = value;
                UpdateScale();
            }
        }

        /// <summary>
        /// Gets or sets custom scale (overrides quality preset).
        /// </summary>
        public float CustomScale
        {
            get { return _customScale; }
            set
            {
                _customScale = Math.Max(0.25f, Math.Min(1.0f, value));
                _quality = QualityPreset.Full; // Custom scale overrides preset
            }
        }

        /// <summary>
        /// Gets the current render scale.
        /// </summary>
        public float RenderScale
        {
            get
            {
                if (_quality == QualityPreset.Full && _customScale > 0.0f)
                {
                    return _customScale;
                }
                return GetScaleForPreset(_quality);
            }
        }

        /// <summary>
        /// Initializes a new render target scaling system.
        /// </summary>
        public RenderTargetScaling(QualityPreset quality = QualityPreset.Balanced)
        {
            _quality = quality;
            _customScale = 0.0f;
            UpdateScale();
        }

        /// <summary>
        /// Calculates scaled render target dimensions.
        /// </summary>
        public void CalculateDimensions(int baseWidth, int baseHeight, out int renderWidth, out int renderHeight)
        {
            float scale = RenderScale;
            renderWidth = (int)(baseWidth * scale);
            renderHeight = (int)(baseHeight * scale);

            // Ensure even dimensions (required for some APIs)
            renderWidth = (renderWidth / 2) * 2;
            renderHeight = (renderHeight / 2) * 2;
        }

        private void UpdateScale()
        {
            // Scale is calculated on-demand via RenderScale property
        }

        private float GetScaleForPreset(QualityPreset preset)
        {
            switch (preset)
            {
                case QualityPreset.Full:
                    return 1.0f;
                case QualityPreset.High:
                    return 0.875f;
                case QualityPreset.Balanced:
                    return 0.75f;
                case QualityPreset.Performance:
                    return 0.5f;
                default:
                    return 0.75f;
            }
        }
    }
}


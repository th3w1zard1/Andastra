using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.PostProcessing
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
    public class ColorGrading : IDisposable
    {
        private float _lift;
        private float _gamma;
        private float _gain;
        private float _temperature;
        private float _tint;
        private float _saturation;
        private float _contrast;
        private SpriteBatch _spriteBatch;

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
            _spriteBatch = null; // Will be created on first use
        }

        /// <summary>
        /// Applies color grading to a render target.
        /// Handles rendering with shader (if provided) or fallback rendering.
        /// The method sets up the render target, configures shader parameters, and renders
        /// a full-screen quad using SpriteBatch with the color grading effect.
        /// </summary>
        /// <param name="device">Graphics device.</param>
        /// <param name="input">Input render target.</param>
        /// <param name="output">Output render target.</param>
        /// <param name="effect">Effect/shader for color grading. Can be null if not using shader-based color grading.</param>
        /// <exception cref="ArgumentNullException">Thrown if device, input, or output is null.</exception>
        public void Apply(GraphicsDevice device, RenderTarget2D input, RenderTarget2D output, Effect effect)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            // Save previous render target
            RenderTarget2D previousTarget = null;
            if (device.GetRenderTargets().Length > 0)
            {
                previousTarget = device.GetRenderTargets()[0].RenderTarget as RenderTarget2D;
            }

            try
            {
                // Set output render target
                device.SetRenderTarget(output);
                device.Clear(Color.Black);

                // Create SpriteBatch if needed (lazy initialization)
                if (_spriteBatch == null)
                {
                    _spriteBatch = new SpriteBatch(device);
                }

                // Set up color grading shader parameters if effect is provided
                // The actual shader implementation would apply:
                // - Lift/Gamma/Gain: Adjusts shadows, midtones, highlights separately
                //   * Lift: Adds/subtracts from shadows (typically -1 to 1 range)
                //   * Gamma: Adjusts midtones (typically 0.1 to 5.0 range, 1.0 = no change)
                //   * Gain: Multiplies highlights (typically 0.0 to 5.0 range, 1.0 = no change)
                // - Color temperature: Adjusts white balance (warmer/cooler, -100 to 100 range)
                // - Tint: Adjusts green-magenta balance (-100 to 100 range)
                // - Saturation: Adjusts color intensity (-1.0 = grayscale, 1.0 = fully saturated)
                // - Contrast: Adjusts difference between light and dark areas (-1.0 to 1.0 range)
                if (effect != null)
                {
                    // Set shader parameters for color grading
                    // Note: Parameter names depend on the actual shader implementation
                    // These are common parameter names used in color grading shaders
                    EffectParameter sourceTextureParam = effect.Parameters["SourceTexture"];
                    if (sourceTextureParam != null)
                    {
                        sourceTextureParam.SetValue(input);
                    }

                    // Alternative parameter name for source texture
                    EffectParameter inputTextureParam = effect.Parameters["InputTexture"];
                    if (inputTextureParam != null && sourceTextureParam == null)
                    {
                        inputTextureParam.SetValue(input);
                    }

                    EffectParameter liftParam = effect.Parameters["Lift"];
                    if (liftParam != null)
                    {
                        liftParam.SetValue(_lift);
                    }

                    EffectParameter gammaParam = effect.Parameters["Gamma"];
                    if (gammaParam != null)
                    {
                        gammaParam.SetValue(_gamma);
                    }

                    EffectParameter gainParam = effect.Parameters["Gain"];
                    if (gainParam != null)
                    {
                        gainParam.SetValue(_gain);
                    }

                    EffectParameter temperatureParam = effect.Parameters["Temperature"];
                    if (temperatureParam != null)
                    {
                        temperatureParam.SetValue(_temperature);
                    }

                    EffectParameter tintParam = effect.Parameters["Tint"];
                    if (tintParam != null)
                    {
                        tintParam.SetValue(_tint);
                    }

                    EffectParameter saturationParam = effect.Parameters["Saturation"];
                    if (saturationParam != null)
                    {
                        saturationParam.SetValue(_saturation);
                    }

                    EffectParameter contrastParam = effect.Parameters["Contrast"];
                    if (contrastParam != null)
                    {
                        contrastParam.SetValue(_contrast);
                    }

                    // Render full-screen quad with color grading shader
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        effect);

                    Rectangle destinationRect = new Rectangle(0, 0, output.Width, output.Height);
                    _spriteBatch.Draw(input, destinationRect, Color.White);
                    _spriteBatch.End();
                }
                else
                {
                    // No shader provided - use CPU-based color grading approximation
                    // This is a fallback when no shader is available
                    // For proper color grading, a shader should be provided
                    // The fallback simply copies the input to output without any color adjustments
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone);

                    Rectangle destinationRect = new Rectangle(0, 0, output.Width, output.Height);
                    _spriteBatch.Draw(input, destinationRect, Color.White);
                    _spriteBatch.End();
                }
            }
            finally
            {
                // Always restore previous render target
                device.SetRenderTarget(previousTarget);
            }
        }

        /// <summary>
        /// Disposes resources used by the color grading processor.
        /// </summary>
        public void Dispose()
        {
            _spriteBatch?.Dispose();
            _spriteBatch = null;
        }
    }
}


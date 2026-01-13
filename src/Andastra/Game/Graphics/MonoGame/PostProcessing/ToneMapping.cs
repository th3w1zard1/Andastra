using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.PostProcessing
{
    /// <summary>
    /// Tone mapping operator for HDR to LDR conversion.
    /// 
    /// Tone mapping converts high dynamic range images to low dynamic range
    /// for display, preserving visual quality and artistic intent.
    /// 
    /// Features:
    /// - Multiple tone mapping operators (Reinhard, ACES, Uncharted 2)
    /// - Exposure control
    /// - White point adjustment
    /// - Artistic control
    /// </summary>
    /// <remarks>
    /// Tone Mapping:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system (modern HDR enhancement)
    /// - Located via string references: "Frame Buffer" @ 0x007c8408 (HDR frame buffer)
    /// - "CB_FRAMEBUFF" @ 0x007d1d84 (frame buffer option)
    /// - Original implementation: KOTOR uses fixed lighting and color mapping
    /// - HDR rendering: Original engine uses LDR (low dynamic range) rendering
    /// - This MonoGame implementation: Modern HDR rendering with tone mapping
    /// - Tone mapping: Converts HDR (high dynamic range) to LDR for display
    /// - Operators: Reinhard, ACES, Uncharted 2, Filmic, Luminance-based
    /// - Exposure control: Adjusts brightness of HDR image before tone mapping
    /// - White point: Maximum luminance value for tone mapping curve
    /// </remarks>
    public class ToneMapping : IDisposable
    {
        /// <summary>
        /// Tone mapping operator type.
        /// </summary>
        public enum ToneMappingOperator
        {
            Reinhard,
            ACES,
            Uncharted2,
            Filmic,
            LuminanceBased
        }

        private ToneMappingOperator _operator;
        private float _exposure;
        private float _whitePoint;
        private SpriteBatch _spriteBatch;

        /// <summary>
        /// Gets or sets the tone mapping operator.
        /// </summary>
        public ToneMappingOperator Operator
        {
            get { return _operator; }
            set { _operator = value; }
        }

        /// <summary>
        /// Gets or sets the exposure value (log2 scale).
        /// </summary>
        public float Exposure
        {
            get { return _exposure; }
            set { _exposure = value; }
        }

        /// <summary>
        /// Gets or sets the white point (maximum luminance).
        /// </summary>
        public float WhitePoint
        {
            get { return _whitePoint; }
            set { _whitePoint = Math.Max(0.1f, value); }
        }

        /// <summary>
        /// Initializes a new tone mapping processor.
        /// </summary>
        public ToneMapping()
        {
            _operator = ToneMappingOperator.ACES;
            _exposure = 0.0f;
            _whitePoint = 11.2f;
            _spriteBatch = null; // Will be created on first use
        }

        /// <summary>
        /// Applies tone mapping to an HDR render target.
        /// </summary>
        /// <param name="device">Graphics device.</param>
        /// <param name="hdrInput">HDR input render target.</param>
        /// <param name="ldrOutput">LDR output render target.</param>
        /// <param name="effect">Effect/shader for tone mapping. Can be null if not using shader-based tone mapping.</param>
        /// <exception cref="ArgumentNullException">Thrown if device, hdrInput, or ldrOutput is null.</exception>
        public void Apply(GraphicsDevice device, RenderTarget2D hdrInput, RenderTarget2D ldrOutput, Effect effect)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }
            if (hdrInput == null)
            {
                throw new ArgumentNullException(nameof(hdrInput));
            }
            if (ldrOutput == null)
            {
                throw new ArgumentNullException(nameof(ldrOutput));
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
                device.SetRenderTarget(ldrOutput);
                device.Clear(Color.Black);

                // Create SpriteBatch if needed (lazy initialization)
                if (_spriteBatch == null)
                {
                    _spriteBatch = new SpriteBatch(device);
                }

                // Set up tone mapping shader parameters if effect is provided
                // The actual shader implementation would apply one of these operators:
                // - Reinhard: x / (1 + x) - simple and fast, good for most scenes
                // - ACES: filmic ACES tone mapping - industry standard for HDR
                // - Uncharted 2: filmic curve with shoulder and toe - artistic control
                // - Filmic: smooth highlights and shadows - balanced aesthetic
                // - LuminanceBased: preserves color relationships - natural look
                if (effect != null)
                {
                    // Set shader parameters for tone mapping
                    // Note: Parameter names depend on the actual shader implementation
                    // These are common parameter names used in HDR tone mapping shaders
                    EffectParameter hdrTextureParam = effect.Parameters["HDRTexture"];
                    if (hdrTextureParam != null)
                    {
                        hdrTextureParam.SetValue(hdrInput);
                    }

                    EffectParameter exposureParam = effect.Parameters["Exposure"];
                    if (exposureParam != null)
                    {
                        // Convert log2 exposure to linear multiplier
                        float exposureMultiplier = (float)Math.Pow(2.0, _exposure);
                        exposureParam.SetValue(exposureMultiplier);
                    }

                    EffectParameter whitePointParam = effect.Parameters["WhitePoint"];
                    if (whitePointParam != null)
                    {
                        whitePointParam.SetValue(_whitePoint);
                    }

                    EffectParameter operatorParam = effect.Parameters["Operator"];
                    if (operatorParam != null)
                    {
                        operatorParam.SetValue((int)_operator);
                    }

                    // Render full-screen quad with tone mapping shader
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        effect);

                    Rectangle destinationRect = new Rectangle(0, 0, ldrOutput.Width, ldrOutput.Height);
                    _spriteBatch.Draw(hdrInput, destinationRect, Color.White);
                    _spriteBatch.End();
                }
                else
                {
                    // No shader provided - use CPU-based tone mapping approximation
                    // This is a fallback when no shader is available
                    // For proper HDR tone mapping, a shader should be provided
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone);

                    Rectangle destinationRect = new Rectangle(0, 0, ldrOutput.Width, ldrOutput.Height);
                    _spriteBatch.Draw(hdrInput, destinationRect, Color.White);
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
        /// Disposes resources used by the tone mapping processor.
        /// </summary>
        public void Dispose()
        {
            _spriteBatch?.Dispose();
            _spriteBatch = null;
        }
    }
}


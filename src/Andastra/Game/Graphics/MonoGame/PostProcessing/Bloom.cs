using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.PostProcessing
{
    /// <summary>
    /// Bloom post-processing effect for HDR rendering.
    /// 
    /// Creates a glow effect by extracting bright areas, blurring them,
    /// and adding them back to the image.
    /// 
    /// Features:
    /// - Threshold-based bright pass
    /// - Multi-pass Gaussian blur
    /// - Configurable intensity
    /// - Performance optimized
    /// </summary>
    /// <remarks>
    /// Bloom Post-Processing:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system (modern post-processing enhancement)
    /// - Located via string references: "Frame Buffer" @ 0x007c8408 (frame buffer for post-processing)
    /// - "CB_FRAMEBUFF" @ 0x007d1d84 (frame buffer checkbox in options)
    /// - Original implementation: KOTOR uses frame buffers for rendering and effects
    /// - Post-processing: Original engine applies visual effects during rendering pipeline
    /// - This MonoGame implementation: Modern bloom effect for HDR rendering
    /// - Bloom effect: Extracts bright areas, applies Gaussian blur, composites back for glow effect
    /// - HDR rendering: Works with high dynamic range render targets for realistic lighting
    /// </remarks>
    public class Bloom : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private SpriteBatch _spriteBatch;
        private RenderTarget2D _brightPassTarget;
        private RenderTarget2D[] _blurTargets;
        private RenderTarget2D _finalOutput;
        private float _threshold;
        private float _intensity;
        private int _blurPasses;

        /// <summary>
        /// Gets or sets the brightness threshold for bloom extraction.
        /// </summary>
        public float Threshold
        {
            get { return _threshold; }
            set { _threshold = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Gets or sets the bloom intensity.
        /// </summary>
        public float Intensity
        {
            get { return _intensity; }
            set { _intensity = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Gets or sets the number of blur passes.
        /// </summary>
        public int BlurPasses
        {
            get { return _blurPasses; }
            set { _blurPasses = Math.Max(1, Math.Min(8, value)); }
        }

        /// <summary>
        /// Initializes a new bloom effect.
        /// </summary>
        /// <summary>
        /// Initializes a new bloom effect.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public Bloom(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _threshold = 1.0f;
            _intensity = 1.0f;
            _blurPasses = 3;
        }

        /// <summary>
        /// Applies bloom to an HDR render target.
        /// </summary>
        /// <param name="hdrInput">HDR input render target.</param>
        /// <param name="effect">Effect/shader for bloom processing.</param>
        /// <returns>Bloom output render target, or input if disabled.</returns>
        /// <summary>
        /// Applies bloom to an HDR render target.
        /// </summary>
        /// <param name="hdrInput">HDR input render target. Must not be null.</param>
        /// <param name="effect">Effect/shader for bloom processing. Can be null.</param>
        /// <returns>Bloom output render target, or input if disabled.</returns>
        /// <exception cref="ArgumentNullException">Thrown if hdrInput is null.</exception>
        public RenderTarget2D Apply(RenderTarget2D hdrInput, Effect effect)
        {
            if (hdrInput == null)
            {
                throw new ArgumentNullException(nameof(hdrInput));
            }

            // Create or resize render targets if needed
            int width = hdrInput.Width;
            int height = hdrInput.Height;

            if (_brightPassTarget == null || _brightPassTarget.Width != width || _brightPassTarget.Height != height)
            {
                _brightPassTarget?.Dispose();
                _brightPassTarget = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    hdrInput.Format,
                    DepthFormat.None
                );
            }

            // Initialize blur targets array if needed
            if (_blurTargets == null || _blurTargets.Length != _blurPasses)
            {
                if (_blurTargets != null)
                {
                    foreach (RenderTarget2D rt in _blurTargets)
                    {
                        rt?.Dispose();
                    }
                }
                _blurTargets = new RenderTarget2D[_blurPasses];
                for (int i = 0; i < _blurPasses; i++)
                {
                    _blurTargets[i] = new RenderTarget2D(
                        _graphicsDevice,
                        width,
                        height,
                        false,
                        hdrInput.Format,
                        DepthFormat.None
                    );
                }
            }

            // Create or resize final output render target if needed
            if (_finalOutput == null || _finalOutput.Width != width || _finalOutput.Height != height)
            {
                _finalOutput?.Dispose();
                _finalOutput = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    hdrInput.Format,
                    DepthFormat.None
                );
            }

            // Save previous render target
            RenderTarget2D previousTarget = _graphicsDevice.GetRenderTargets().Length > 0
                ? _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                // Bloom processing pipeline:
                // 1. Extract bright areas (threshold pass) - pixels above threshold
                // 2. Blur the bright areas (multiple passes) - separable Gaussian blur
                // 3. Combine with original image - additive blending
                // swkotor2.exe: Frame buffer post-processing @ 0x007c8408, frame buffer option @ 0x007d1d84
                // Original implementation: Uses frame buffers for rendering and effects
                // This implementation: Full bloom pipeline with bright pass, blur, and compositing

                // Step 1: Bright pass extraction
                // Extract pixels above threshold for bloom processing
                _graphicsDevice.SetRenderTarget(_brightPassTarget);
                _graphicsDevice.Clear(Color.Black);

                Rectangle brightPassRect = new Rectangle(0, 0, width, height);
                if (effect != null)
                {
                    // Set shader parameters for bright pass extraction
                    EffectParameter sourceTextureParam = effect.Parameters["SourceTexture"];
                    if (sourceTextureParam != null)
                    {
                        sourceTextureParam.SetValue(hdrInput);
                    }

                    EffectParameter thresholdParam = effect.Parameters["Threshold"];
                    if (thresholdParam != null)
                    {
                        thresholdParam.SetValue(_threshold);
                    }

                    EffectParameter screenSizeParam = effect.Parameters["ScreenSize"];
                    if (screenSizeParam != null)
                    {
                        screenSizeParam.SetValue(new Vector2(width, height));
                    }

                    EffectParameter screenSizeInvParam = effect.Parameters["ScreenSizeInv"];
                    if (screenSizeInvParam != null)
                    {
                        screenSizeInvParam.SetValue(new Vector2(1.0f / width, 1.0f / height));
                    }

                    // Render full-screen quad with bright pass shader
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        effect);
                    _spriteBatch.Draw(hdrInput, brightPassRect, Color.White);
                    _spriteBatch.End();
                }
                else
                {
                    // Fallback: Copy input if no shader available (bright pass would be done in shader)
                    // For proper bloom, a shader should be provided, but we'll copy as fallback
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone);
                    _spriteBatch.Draw(hdrInput, brightPassRect, Color.White);
                    _spriteBatch.End();
                }

                // Step 2: Multi-pass blur (separable Gaussian)
                // Apply horizontal and vertical blur passes for smooth bloom glow
                RenderTarget2D blurSource = _brightPassTarget;
                for (int i = 0; i < _blurPasses; i++)
                {
                    _graphicsDevice.SetRenderTarget(_blurTargets[i]);
                    _graphicsDevice.Clear(Color.Black);

                    Rectangle blurRect = new Rectangle(0, 0, width, height);
                    bool isHorizontal = (i % 2 == 0);

                    if (effect != null)
                    {
                        // Set shader parameters for Gaussian blur
                        EffectParameter sourceTextureParam = effect.Parameters["SourceTexture"];
                        if (sourceTextureParam != null)
                        {
                            sourceTextureParam.SetValue(blurSource);
                        }

                        EffectParameter blurDirectionParam = effect.Parameters["BlurDirection"];
                        if (blurDirectionParam != null)
                        {
                            // Horizontal: (1, 0), Vertical: (0, 1)
                            blurDirectionParam.SetValue(isHorizontal ? Vector2.UnitX : Vector2.UnitY);
                        }

                        EffectParameter blurRadiusParam = effect.Parameters["BlurRadius"];
                        if (blurRadiusParam != null)
                        {
                            blurRadiusParam.SetValue(_intensity);
                        }

                        EffectParameter screenSizeParam = effect.Parameters["ScreenSize"];
                        if (screenSizeParam != null)
                        {
                            screenSizeParam.SetValue(new Vector2(width, height));
                        }

                        EffectParameter screenSizeInvParam = effect.Parameters["ScreenSizeInv"];
                        if (screenSizeInvParam != null)
                        {
                            screenSizeInvParam.SetValue(new Vector2(1.0f / width, 1.0f / height));
                        }

                        // Render full-screen quad with blur shader
                        _spriteBatch.Begin(
                            SpriteSortMode.Immediate,
                            BlendState.Opaque,
                            SamplerState.LinearClamp,
                            DepthStencilState.None,
                            RasterizerState.CullNone,
                            effect);
                        _spriteBatch.Draw(blurSource, blurRect, Color.White);
                        _spriteBatch.End();
                    }
                    else
                    {
                        // Fallback: Copy source if no shader available
                        _spriteBatch.Begin(
                            SpriteSortMode.Immediate,
                            BlendState.Opaque,
                            SamplerState.LinearClamp,
                            DepthStencilState.None,
                            RasterizerState.CullNone);
                        _spriteBatch.Draw(blurSource, blurRect, Color.White);
                        _spriteBatch.End();
                    }

                    blurSource = _blurTargets[i];
                }

                // Step 3: Combine with original image (additive blending)
                // Composite the blurred bloom with the original HDR input for final glow effect
                // swkotor2.exe: Frame buffer compositing for post-processing effects
                _graphicsDevice.SetRenderTarget(_finalOutput);
                _graphicsDevice.Clear(Color.Black);

                Rectangle finalRect = new Rectangle(0, 0, width, height);
                RenderTarget2D finalBlurred = _blurTargets[_blurPasses - 1];

                if (effect != null)
                {
                    // Set shader parameters for bloom compositing
                    EffectParameter originalTextureParam = effect.Parameters["OriginalTexture"];
                    if (originalTextureParam != null)
                    {
                        originalTextureParam.SetValue(hdrInput);
                    }

                    EffectParameter bloomTextureParam = effect.Parameters["BloomTexture"];
                    if (bloomTextureParam != null)
                    {
                        bloomTextureParam.SetValue(finalBlurred);
                    }

                    EffectParameter intensityParam = effect.Parameters["BloomIntensity"];
                    if (intensityParam != null)
                    {
                        intensityParam.SetValue(_intensity);
                    }

                    EffectParameter screenSizeParam = effect.Parameters["ScreenSize"];
                    if (screenSizeParam != null)
                    {
                        screenSizeParam.SetValue(new Vector2(width, height));
                    }

                    // Render full-screen quad with compositing shader (additive blend)
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        effect);
                    _spriteBatch.Draw(hdrInput, finalRect, Color.White);
                    _spriteBatch.End();
                }
                else
                {
                    // Fallback: Additive blending using SpriteBatch blend states
                    // First pass: Draw original HDR input
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone);
                    _spriteBatch.Draw(hdrInput, finalRect, Color.White);
                    _spriteBatch.End();

                    // Second pass: Additively blend blurred bloom on top
                    // Use additive blending to combine bloom with original
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Additive,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone);
                    // Apply intensity as color multiplier for bloom contribution
                    Color bloomColor = new Color(_intensity, _intensity, _intensity, 1.0f);
                    _spriteBatch.Draw(finalBlurred, finalRect, bloomColor);
                    _spriteBatch.End();
                }
            }
            finally
            {
                // Always restore previous render target
                _graphicsDevice.SetRenderTarget(previousTarget);
            }

            // Return the final composited output (original + bloom)
            return _finalOutput ?? hdrInput;
        }

        /// <summary>
        /// Disposes of all resources used by this bloom effect.
        /// </summary>
        public void Dispose()
        {
            _spriteBatch?.Dispose();
            _spriteBatch = null;

            _brightPassTarget?.Dispose();
            _brightPassTarget = null;

            if (_blurTargets != null)
            {
                foreach (RenderTarget2D rt in _blurTargets)
                {
                    rt?.Dispose();
                }
                _blurTargets = null;
            }

            _finalOutput?.Dispose();
            _finalOutput = null;
        }
    }
}


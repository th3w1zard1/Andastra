using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// High Dynamic Range (HDR) rendering pipeline.
    /// 
    /// Enables HDR rendering with proper tone mapping, exposure adaptation,
    /// and color grading for cinematic quality.
    /// 
    /// Features:
    /// - HDR render targets
    /// - Luminance calculation
    /// - Exposure adaptation
    /// - Tone mapping
    /// - Color grading
    /// - Bloom integration
    /// </summary>
    public class HDRPipeline : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _hdrTarget;
        private RenderTarget2D _luminanceTarget;
        private RenderTarget2D _toneMappingOutput;
        private RenderTarget2D _colorGradingOutput;
        private RenderTarget2D _finalOutput;
        private SpriteBatch _spriteBatch;
        private PostProcessing.ToneMapping _toneMapping;
        private PostProcessing.Bloom _bloom;
        private PostProcessing.ColorGrading _colorGrading;
        private PostProcessing.ExposureAdaptation _exposureAdaptation;
        private bool _enabled;

        /// <summary>
        /// Gets or sets whether HDR rendering is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Initializes a new HDR pipeline.
        /// </summary>
        /// <summary>
        /// Initializes a new HDR pipeline.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public HDRPipeline(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _toneMapping = new PostProcessing.ToneMapping();
            _bloom = new PostProcessing.Bloom(graphicsDevice);
            _colorGrading = new PostProcessing.ColorGrading();
            _exposureAdaptation = new PostProcessing.ExposureAdaptation();
            _enabled = true;
        }

        /// <summary>
        /// Gets the HDR render target for scene rendering.
        /// </summary>
        /// <param name="width">Target width in pixels.</param>
        /// <param name="height">Target height in pixels.</param>
        /// <returns>HDR render target.</returns>
        /// <summary>
        /// Gets the HDR render target for scene rendering.
        /// </summary>
        /// <param name="width">Target width in pixels. Must be greater than zero.</param>
        /// <param name="height">Target height in pixels. Must be greater than zero.</param>
        /// <returns>HDR render target.</returns>
        /// <exception cref="ArgumentException">Thrown if width or height is less than or equal to zero.</exception>
        public RenderTarget2D GetHDRTarget(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentException("Width must be greater than zero.", nameof(width));
            }
            if (height <= 0)
            {
                throw new ArgumentException("Height must be greater than zero.", nameof(height));
            }

            // Create or resize HDR target
            if (_hdrTarget == null || _hdrTarget.Width != width || _hdrTarget.Height != height)
            {
                _hdrTarget?.Dispose();
                _hdrTarget = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    SurfaceFormat.HdrBlendable,
                    DepthFormat.Depth24
                );
            }
            return _hdrTarget;
        }

        /// <summary>
        /// Processes HDR image through the pipeline.
        /// </summary>
        /// <param name="hdrInput">HDR input render target.</param>
        /// <param name="deltaTime">Time since last frame in seconds.</param>
        /// <param name="effect">Effect/shader for HDR processing.</param>
        /// <returns>Processed LDR output render target.</returns>
        /// <summary>
        /// Processes HDR image through the pipeline.
        /// </summary>
        /// <param name="hdrInput">HDR input render target. Must not be null.</param>
        /// <param name="deltaTime">Time since last frame in seconds. Will be clamped to non-negative.</param>
        /// <param name="effect">Effect/shader for HDR processing. Can be null.</param>
        /// <returns>Processed LDR output render target.</returns>
        /// <exception cref="ArgumentNullException">Thrown if hdrInput is null.</exception>
        public RenderTarget2D Process(RenderTarget2D hdrInput, float deltaTime, Effect effect)
        {
            if (!_enabled)
            {
                return hdrInput;
            }
            if (hdrInput == null)
            {
                throw new ArgumentNullException(nameof(hdrInput));
            }

            if (deltaTime < 0.0f)
            {
                deltaTime = 0.0f;
            }

            // Create luminance target if needed
            // Luminance buffer can be smaller (1/4 or 1/8 size) for performance
            int lumWidth = Math.Max(1, hdrInput.Width / 4);
            int lumHeight = Math.Max(1, hdrInput.Height / 4);
            if (_luminanceTarget == null || _luminanceTarget.Width != lumWidth || _luminanceTarget.Height != lumHeight)
            {
                _luminanceTarget?.Dispose();
                _luminanceTarget = new RenderTarget2D(
                    _graphicsDevice,
                    lumWidth,
                    lumHeight,
                    false,
                    SurfaceFormat.Single,
                    DepthFormat.None
                );
            }

            // HDR processing pipeline:
            // 1. Calculate luminance from HDR input (downsample to smaller buffer)
            // 2. Update exposure adaptation based on average luminance
            // 3. Apply bloom to bright areas
            // 4. Apply tone mapping (HDR to LDR conversion)
            // 5. Apply color grading (artistic adjustments)

            // Step 1: Calculate luminance from HDR input
            float sceneLuminance = CalculateLuminance(hdrInput);

            // Step 2: Update exposure adaptation
            _exposureAdaptation.Update(sceneLuminance, deltaTime);

            // Set exposure for tone mapping
            _toneMapping.Exposure = _exposureAdaptation.CurrentExposure;

            // Step 3: Apply bloom to bright areas
            RenderTarget2D bloomed = _bloom != null ? _bloom.Apply(hdrInput, effect) : hdrInput;

            // Step 4: Apply tone mapping (HDR to LDR conversion)
            RenderTarget2D toneMapped = ApplyToneMapping(bloomed, hdrInput.Width, hdrInput.Height, effect);

            // Step 5: Apply color grading (artistic adjustments)
            RenderTarget2D graded = ApplyColorGrading(toneMapped, hdrInput.Width, hdrInput.Height, effect);

            // Return the final processed output
            return graded;
        }

        /// <summary>
        /// Gets the current exposure value from exposure adaptation.
        /// </summary>
        public float GetCurrentExposure()
        {
            return _exposureAdaptation != null ? _exposureAdaptation.CurrentExposure : 0.0f;
        }

        /// <summary>
        /// Calculates average scene luminance from HDR input by downsampling and averaging.
        /// </summary>
        /// <param name="hdrInput">HDR input render target.</param>
        /// <returns>Average scene luminance.</returns>
        private float CalculateLuminance(RenderTarget2D hdrInput)
        {
            // Downsample HDR input to luminance target using progressive downsampling
            // TODO:  This is a simplified approach - a full implementation would use a luminance shader
            // that calculates luminance (Y = 0.2126*R + 0.7152*G + 0.0722*B) during downsampling

            RenderTarget2D previousTarget = null;
            if (_graphicsDevice.GetRenderTargets().Length > 0)
            {
                previousTarget = _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D;
            }

            try
            {
                // Create a temporary Color-format render target for reliable reading and downsampling
                // Single format may not render/read correctly with HDR inputs, so we use Color format
                RenderTarget2D tempLumReadTarget = null;
                try
                {
                    tempLumReadTarget = new RenderTarget2D(
                        _graphicsDevice,
                        _luminanceTarget.Width,
                        _luminanceTarget.Height,
                        false,
                        SurfaceFormat.Color,
                        DepthFormat.None
                    );

                    // Downsample HDR input directly to Color format target for reading
                    _graphicsDevice.SetRenderTarget(tempLumReadTarget);
                    _graphicsDevice.Clear(Color.Black);

                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone
                    );

                    Rectangle destRect = new Rectangle(0, 0, tempLumReadTarget.Width, tempLumReadTarget.Height);
                    _spriteBatch.Draw(hdrInput, destRect, Color.White);
                    _spriteBatch.End();

                    // Read pixel data from Color format buffer
                    Color[] pixelData = new Color[tempLumReadTarget.Width * tempLumReadTarget.Height];
                    tempLumReadTarget.GetData(pixelData);

                    // Calculate average luminance using ITU-R BT.709 luminance weights
                    // Y = 0.2126*R + 0.7152*G + 0.0722*B
                    // Note: This assumes sRGB color space. For proper HDR, we'd need linear values
                    float sumLuminance = 0.0f;
                    int pixelCount = 0;
                    foreach (Color pixel in pixelData)
                    {
                        // Convert sRGB to approximate linear for luminance calculation
                        // For true HDR, we'd read HDR values directly, but this works for approximation
                        float r = pixel.R / 255.0f;
                        float g = pixel.G / 255.0f;
                        float b = pixel.B / 255.0f;

                        // Calculate luminance: Y = 0.2126*R + 0.7152*G + 0.0722*B
                        float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                        sumLuminance += luminance;
                        pixelCount++;
                    }

                    float avgLuminance = pixelCount > 0 ? sumLuminance / pixelCount : 0.001f;
                    return Math.Max(0.001f, avgLuminance); // Avoid zero/negative values
                }
                finally
                {
                    tempLumReadTarget?.Dispose();
                }
            }
            finally
            {
                _graphicsDevice.SetRenderTarget(previousTarget);
            }
        }

        /// <summary>
        /// Applies tone mapping to convert HDR to LDR.
        /// </summary>
        /// <param name="hdrInput">HDR input render target.</param>
        /// <param name="width">Output width.</param>
        /// <param name="height">Output height.</param>
        /// <param name="effect">Optional effect for tone mapping.</param>
        /// <returns>Tone-mapped LDR render target.</returns>
        private RenderTarget2D ApplyToneMapping(RenderTarget2D hdrInput, int width, int height, Effect effect)
        {
            // Create or resize tone mapping output target
            if (_toneMappingOutput == null || _toneMappingOutput.Width != width || _toneMappingOutput.Height != height)
            {
                _toneMappingOutput?.Dispose();
                _toneMappingOutput = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None
                );
            }

            // Apply tone mapping using the ToneMapping processor
            // ToneMapping.Apply handles rendering with shader (if provided) or fallback rendering
            // The method sets up the render target, configures shader parameters, and renders
            // a full-screen quad using SpriteBatch with the tone mapping effect
            _toneMapping.Apply(_graphicsDevice, hdrInput, _toneMappingOutput, effect);

            return _toneMappingOutput;
        }

        /// <summary>
        /// Applies color grading for artistic adjustments.
        /// </summary>
        /// <param name="input">Input render target.</param>
        /// <param name="width">Output width.</param>
        /// <param name="height">Output height.</param>
        /// <param name="effect">Optional effect for color grading.</param>
        /// <returns>Color-graded render target.</returns>
        private RenderTarget2D ApplyColorGrading(RenderTarget2D input, int width, int height, Effect effect)
        {
            // Create or resize color grading output target
            if (_colorGradingOutput == null || _colorGradingOutput.Width != width || _colorGradingOutput.Height != height)
            {
                _colorGradingOutput?.Dispose();
                _colorGradingOutput = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None
                );
            }

            // Apply color grading using the ColorGrading processor
            // ColorGrading.Apply handles rendering with shader (if provided) or fallback rendering
            // The method sets up the render target, configures shader parameters, and renders
            // a full-screen quad using SpriteBatch with the color grading effect
            _colorGrading.Apply(_graphicsDevice, input, _colorGradingOutput, effect);

            return _colorGradingOutput;
        }

        public void Dispose()
        {
            _hdrTarget?.Dispose();
            _luminanceTarget?.Dispose();
            _toneMappingOutput?.Dispose();
            _colorGradingOutput?.Dispose();
            _finalOutput?.Dispose();
            _spriteBatch?.Dispose();
            _bloom?.Dispose();
            _toneMapping?.Dispose();
            
            // Reset references
            _hdrTarget = null;
            _luminanceTarget = null;
            _toneMappingOutput = null;
            _colorGradingOutput = null;
            _finalOutput = null;
            _spriteBatch = null;
            _bloom = null;
            _toneMapping = null;
            _colorGrading = null;
            _exposureAdaptation = null;
        }
    }
}


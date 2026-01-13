using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
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
        private Effect _luminanceEffect;
        private Rendering.ShaderCache _shaderCache;
        private List<RenderTarget2D> _downsampleTargets;

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
            _shaderCache = new Rendering.ShaderCache(graphicsDevice);
            _downsampleTargets = new List<RenderTarget2D>();
            _luminanceEffect = null;
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
        /// Calculates average scene luminance from HDR input using GPU-based luminance shader with progressive downsampling.
        /// </summary>
        /// <param name="hdrInput">HDR input render target.</param>
        /// <returns>Average scene luminance.</returns>
        /// <remarks>
        /// Based on HDR rendering best practices: GPU-based luminance calculation with progressive downsampling
        /// Original implementation: CPU-based luminance calculation (simplified approach)
        /// Full implementation: Uses luminance shader that calculates luminance (Y = 0.2126*R + 0.7152*G + 0.0722*B) during downsampling
        ///
        /// Luminance Calculation Pipeline:
        /// 1. Compile/load luminance shader (if not already loaded)
        /// 2. Progressive downsampling: Downsample HDR input through multiple passes (1/2, 1/4, 1/8, etc.)
        /// 3. Each downsampling pass calculates luminance using ITU-R BT.709 weights in the shader
        /// 4. Final pass: Downsample to 1x1 pixel and read average luminance
        /// 5. This GPU-based approach is much faster than CPU-based pixel reading
        ///
        /// ITU-R BT.709 Luminance Formula:
        /// Y = 0.2126*R + 0.7152*G + 0.0722*B
        /// This is the standard formula for converting RGB to luminance in sRGB/Rec.709 color space
        /// </remarks>
        private float CalculateLuminance(RenderTarget2D hdrInput)
        {
            if (hdrInput == null)
            {
                return 0.001f; // Default minimum luminance
            }

            // Ensure luminance shader is loaded
            if (_luminanceEffect == null)
            {
                _luminanceEffect = LoadLuminanceShader();
                if (_luminanceEffect == null)
                {
                    // Shader compilation failed - fall back to CPU-based calculation
                    return CalculateLuminanceCPU(hdrInput);
                }
            }

            RenderTarget2D previousTarget = null;
            if (_graphicsDevice.GetRenderTargets().Length > 0)
            {
                previousTarget = _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D;
            }

            try
            {
                // Progressive downsampling: Downsample HDR input through multiple passes
                // Each pass reduces size by half and calculates luminance in the shader
                // This is more efficient than single-pass downsampling and provides better quality
                RenderTarget2D currentSource = hdrInput;
                List<RenderTarget2D> tempTargets = new List<RenderTarget2D>();

                try
                {
                    // Calculate number of downsampling passes needed
                    // Target: 1x1 pixel for final average luminance
                    int currentWidth = hdrInput.Width;
                    int currentHeight = hdrInput.Height;
                    int passCount = 0;

                    // Progressive downsampling: Continue until we reach 1x1 or very small size
                    while (currentWidth > 1 && currentHeight > 1 && passCount < 10) // Limit to 10 passes for safety
                    {
                        int nextWidth = Math.Max(1, currentWidth / 2);
                        int nextHeight = Math.Max(1, currentHeight / 2);

                        // Create or reuse downsampling target
                        RenderTarget2D downsampleTarget = null;
                        if (passCount < _downsampleTargets.Count)
                        {
                            // Reuse existing target if size matches
                            RenderTarget2D existing = _downsampleTargets[passCount];
                            if (existing != null && existing.Width == nextWidth && existing.Height == nextHeight)
                            {
                                downsampleTarget = existing;
                            }
                            else
                            {
                                existing?.Dispose();
                                downsampleTarget = new RenderTarget2D(
                                    _graphicsDevice,
                                    nextWidth,
                                    nextHeight,
                                    false,
                                    SurfaceFormat.Single, // Use Single format for HDR luminance values
                                    DepthFormat.None
                                );
                                _downsampleTargets[passCount] = downsampleTarget;
                            }
                        }
                        else
                        {
                            // Create new downsampling target
                            downsampleTarget = new RenderTarget2D(
                                _graphicsDevice,
                                nextWidth,
                                nextHeight,
                                false,
                                SurfaceFormat.Single, // Use Single format for HDR luminance values
                                DepthFormat.None
                            );
                            _downsampleTargets.Add(downsampleTarget);
                            tempTargets.Add(downsampleTarget);
                        }

                        // Render downsampling pass with luminance shader
                        _graphicsDevice.SetRenderTarget(downsampleTarget);
                        _graphicsDevice.Clear(Color.Black);

                        // Set shader parameters
                        EffectParameter sourceTextureParam = _luminanceEffect.Parameters["SourceTexture"];
                        if (sourceTextureParam != null)
                        {
                            sourceTextureParam.SetValue(currentSource);
                        }

                        // Render full-screen quad with luminance shader
                        _spriteBatch.Begin(
                            SpriteSortMode.Immediate,
                            BlendState.Opaque,
                            SamplerState.LinearClamp,
                            DepthStencilState.None,
                            RasterizerState.CullNone,
                            _luminanceEffect
                        );

                        Rectangle destRect = new Rectangle(0, 0, nextWidth, nextHeight);
                        _spriteBatch.Draw(currentSource, destRect, Color.White);
                        _spriteBatch.End();

                        // Move to next pass
                        if (currentSource != hdrInput)
                        {
                            // Don't dispose the original input
                            tempTargets.Remove(currentSource);
                        }
                        currentSource = downsampleTarget;
                        currentWidth = nextWidth;
                        currentHeight = nextHeight;
                        passCount++;
                    }

                    // Final pass: Read luminance from 1x1 or smallest target
                    // Create a Color-format target for reading (Single format may not be readable)
                    RenderTarget2D readTarget = null;
                    try
                    {
                        readTarget = new RenderTarget2D(
                            _graphicsDevice,
                            currentWidth,
                            currentHeight,
                            false,
                            SurfaceFormat.Color,
                            DepthFormat.None
                        );

                        // Copy final luminance to readable format
                        _graphicsDevice.SetRenderTarget(readTarget);
                        _graphicsDevice.Clear(Color.Black);

                        _spriteBatch.Begin(
                            SpriteSortMode.Immediate,
                            BlendState.Opaque,
                            SamplerState.LinearClamp,
                            DepthStencilState.None,
                            RasterizerState.CullNone
                        );

                        Rectangle finalRect = new Rectangle(0, 0, currentWidth, currentHeight);
                        _spriteBatch.Draw(currentSource, finalRect, Color.White);
                        _spriteBatch.End();

                        // Read average luminance from final target
                        Color[] pixelData = new Color[currentWidth * currentHeight];
                        readTarget.GetData(pixelData);

                        // Calculate average luminance from final downsampled buffer
                        // The shader has already calculated luminance, so we just average the values
                        float sumLuminance = 0.0f;
                        int pixelCount = 0;
                        foreach (Color pixel in pixelData)
                        {
                            // Luminance shader outputs grayscale (R=G=B=luminance)
                            // Use red channel as luminance value (all channels should be equal)
                            float luminance = pixel.R / 255.0f;
                            sumLuminance += luminance;
                            pixelCount++;
                        }

                        float avgLuminance = pixelCount > 0 ? sumLuminance / pixelCount : 0.001f;
                        return Math.Max(0.001f, avgLuminance); // Avoid zero/negative values
                    }
                    finally
                    {
                        readTarget?.Dispose();
                    }
                }
                finally
                {
                    // Clean up temporary targets (but keep reusable ones in _downsampleTargets)
                    foreach (RenderTarget2D temp in tempTargets)
                    {
                        if (temp != currentSource && !_downsampleTargets.Contains(temp))
                        {
                            temp?.Dispose();
                        }
                    }
                }
            }
            finally
            {
                _graphicsDevice.SetRenderTarget(previousTarget);
            }
        }

        /// <summary>
        /// Fallback CPU-based luminance calculation when shader is not available.
        /// </summary>
        /// <param name="hdrInput">HDR input render target.</param>
        /// <returns>Average scene luminance.</returns>
        private float CalculateLuminanceCPU(RenderTarget2D hdrInput)
        {
            RenderTarget2D previousTarget = null;
            if (_graphicsDevice.GetRenderTargets().Length > 0)
            {
                previousTarget = _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D;
            }

            try
            {
                // Create a temporary Color-format render target for reliable reading and downsampling
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
                    float sumLuminance = 0.0f;
                    int pixelCount = 0;
                    foreach (Color pixel in pixelData)
                    {
                        // Convert sRGB to approximate linear for luminance calculation
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
        /// Loads or compiles the luminance shader.
        /// </summary>
        /// <returns>Compiled luminance effect, or null if compilation failed.</returns>
        /// <remarks>
        /// Luminance Shader (HLSL):
        /// - Calculates ITU-R BT.709 luminance: Y = 0.2126*R + 0.7152*G + 0.0722*B
        /// - Outputs grayscale luminance value (R=G=B=luminance)
        /// - Used for progressive downsampling in HDR pipeline
        /// - Based on HDR rendering best practices: GPU-based luminance calculation
        /// </remarks>
        private Effect LoadLuminanceShader()
        {
            // HLSL shader source for luminance calculation
            // This shader calculates ITU-R BT.709 luminance and outputs grayscale
            string shaderSource = @"
// Luminance Shader - Calculates ITU-R BT.709 luminance from HDR input
// Based on HDR rendering best practices: GPU-based luminance calculation
// Formula: Y = 0.2126*R + 0.7152*G + 0.0722*B

sampler SourceTexture : register(s0);

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TextureCoordinate : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TextureCoordinate : TEXCOORD0;
};

// Vertex shader: Pass through with full-screen quad
VertexShaderOutput VertexShaderMain(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = input.Position;
    output.TextureCoordinate = input.TextureCoordinate;
    return output;
}

// Pixel shader: Calculate luminance using ITU-R BT.709 weights
float4 PixelShaderMain(VertexShaderOutput input) : COLOR0
{
    // Sample HDR input texture
    float4 color = tex2D(SourceTexture, input.TextureCoordinate);

    // Calculate ITU-R BT.709 luminance
    // Y = 0.2126*R + 0.7152*G + 0.0722*B
    float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));

    // Output grayscale luminance (R=G=B=luminance, A=1.0)
    return float4(luminance, luminance, luminance, 1.0);
}

// Technique for luminance calculation
technique Luminance
{
    pass Pass0
    {
        VertexShader = compile vs_2_0 VertexShaderMain();
        PixelShader = compile ps_2_0 PixelShaderMain();
    }
}
";

            try
            {
                // Compile shader using ShaderCache
                Effect effect = _shaderCache.GetShader("LuminanceShader", shaderSource);
                return effect;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HDRPipeline] Failed to load luminance shader: {ex.Message}");
                return null;
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
            _luminanceEffect?.Dispose();
            // Note: ShaderCache doesn't implement IDisposable, so we just clear it
            _shaderCache?.Clear();

            // Dispose downsampling targets
            if (_downsampleTargets != null)
            {
                foreach (RenderTarget2D target in _downsampleTargets)
                {
                    target?.Dispose();
                }
                _downsampleTargets.Clear();
            }

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
            _luminanceEffect = null;
            _shaderCache = null;
            _downsampleTargets = null;
        }
    }
}


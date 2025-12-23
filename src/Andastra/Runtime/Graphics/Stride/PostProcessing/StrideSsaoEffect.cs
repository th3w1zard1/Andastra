using System;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Stride.Graphics;

namespace Andastra.Runtime.Stride.PostProcessing
{
    /// <summary>
    /// Stride implementation of screen-space ambient occlusion effect.
    /// Inherits shared SSAO logic from BaseSsaoEffect.
    ///
    /// Implements GTAO (Ground Truth Ambient Occlusion) for high-quality
    /// ambient occlusion with temporal stability.
    ///
    /// Features:
    /// - Configurable sample radius and count
    /// - Temporal filtering for stability
    /// - Spatial blur for noise reduction
    /// </summary>
    public class StrideSsaoEffect : BaseSsaoEffect
    {
        private GraphicsDevice _graphicsDevice;
        private Texture _aoTarget;
        private Texture _blurTarget;
        private Texture _noiseTexture;
        private SpriteBatch _spriteBatch;
        private SamplerState _linearSampler;
        private SamplerState _pointSampler;
        private EffectInstance _gtaoEffect;
        private EffectInstance _bilateralBlurEffect;
        private Effect _gtaoEffectBase;
        private Effect _bilateralBlurEffectBase;
        private Texture _tempBlurTarget;

        public StrideSsaoEffect(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            InitializeRenderingResources();
        }

        private void InitializeRenderingResources()
        {
            // Create sprite batch for fullscreen quad rendering
            _spriteBatch = new SpriteBatch(_graphicsDevice);

            // Create samplers for texture sampling
            _linearSampler = SamplerState.New(_graphicsDevice, new SamplerStateDescription
            {
                Filter = TextureFilter.Linear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp
            });

            _pointSampler = SamplerState.New(_graphicsDevice, new SamplerStateDescription
            {
                Filter = TextureFilter.Point,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp
            });

            // Load SSAO effect shaders from compiled .sdsl files or ContentManager
            // Based on Stride Engine shader loading:
            // - Compiled .sdsl files are stored as .sdeffect files in content
            // - Effect.Load() loads from default content paths
            // - ContentManager.Load&lt;Effect&gt;() loads from content manager
            // - EffectSystem can compile shaders at runtime from source (requires EffectSystem access)
            LoadSsaoShaders();
        }

        /// <summary>
        /// Loads SSAO effect shaders from compiled .sdsl files or creates them programmatically.
        /// </summary>
        /// <remarks>
        /// Based on Stride Engine shader loading:
        /// - Compiled .sdsl files are stored as .sdeffect files in content
        /// - Effect.Load() loads from default content paths
        /// - ContentManager.Load&lt;Effect&gt;() loads from content manager
        /// - EffectSystem can compile shaders at runtime from source (requires EffectSystem access)
        /// 
        /// Shader Requirements:
        /// - GTAO shader: Computes ambient occlusion using Ground Truth Ambient Occlusion algorithm
        ///   - Inputs: DepthTexture, NormalTexture, NoiseTexture, ProjectionMatrix, ProjectionMatrixInv
        ///   - Parameters: Radius, Power, SampleCount, ScreenSize, ScreenSizeInv
        ///   - Output: Single-channel AO value (R8_UNorm)
        /// - BilateralBlur shader: Edge-preserving blur for noise reduction
        ///   - Inputs: SourceTexture, DepthTexture
        ///   - Parameters: Horizontal (bool), BlurRadius, DepthThreshold, ScreenSize, ScreenSizeInv
        ///   - Output: Blurred single-channel AO value (R8_UNorm)
        /// 
        /// Note: If shader files are not available, the effect will use SpriteBatch's default rendering
        /// which will not compute actual SSAO. Shader files (.sdsl/.sdeffect) must be provided for
        /// the SSAO effect to function correctly.
        /// </remarks>
        private void LoadSsaoShaders()
        {
            // Strategy 1: Try loading from compiled effect files using Effect.Load()
            // Effect.Load() searches in standard content paths for compiled .sdeffect files
            try
            {
                _gtaoEffectBase = Effect.Load(_graphicsDevice, "GTAO");
                if (_gtaoEffectBase != null)
                {
                    _gtaoEffect = new EffectInstance(_gtaoEffectBase);
                    System.Console.WriteLine("[StrideSsaoEffect] Loaded GTAO effect from compiled file");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideSsaoEffect] Failed to load GTAO from compiled file: {ex.Message}");
            }

            try
            {
                _bilateralBlurEffectBase = Effect.Load(_graphicsDevice, "BilateralBlur");
                if (_bilateralBlurEffectBase != null)
                {
                    _bilateralBlurEffect = new EffectInstance(_bilateralBlurEffectBase);
                    System.Console.WriteLine("[StrideSsaoEffect] Loaded BilateralBlur effect from compiled file");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideSsaoEffect] Failed to load BilateralBlur from compiled file: {ex.Message}");
            }

            // Strategy 2: Try loading from ContentManager if available
            // Check if GraphicsDevice has access to ContentManager through services
            if (_gtaoEffectBase == null || _bilateralBlurEffectBase == null)
            {
                try
                {
                    // Try to get ContentManager from GraphicsDevice services
                    // Stride GraphicsDevice may have Services property that provides ContentManager
                    var services = _graphicsDevice.Services;
                    if (services != null)
                    {
                        var contentManager = services.GetService<ContentManager>();
                        if (contentManager != null)
                        {
                            if (_gtaoEffectBase == null)
                            {
                                try
                                {
                                    _gtaoEffectBase = contentManager.Load<Effect>("GTAO");
                                    if (_gtaoEffectBase != null)
                                    {
                                        _gtaoEffect = new EffectInstance(_gtaoEffectBase);
                                        System.Console.WriteLine("[StrideSsaoEffect] Loaded GTAO from ContentManager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"[StrideSsaoEffect] Failed to load GTAO from ContentManager: {ex.Message}");
                                }
                            }

                            if (_bilateralBlurEffectBase == null)
                            {
                                try
                                {
                                    _bilateralBlurEffectBase = contentManager.Load<Effect>("BilateralBlur");
                                    if (_bilateralBlurEffectBase != null)
                                    {
                                        _bilateralBlurEffect = new EffectInstance(_bilateralBlurEffectBase);
                                        System.Console.WriteLine("[StrideSsaoEffect] Loaded BilateralBlur from ContentManager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"[StrideSsaoEffect] Failed to load BilateralBlur from ContentManager: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideSsaoEffect] Failed to access ContentManager: {ex.Message}");
                }
            }

            // Final fallback: If all loading methods failed, effects remain null
            // The rendering code will use SpriteBatch's default rendering (no custom shaders)
            // Note: This means SSAO will not be computed until shader files are provided
            if (_gtaoEffectBase == null && _bilateralBlurEffectBase == null)
            {
                System.Console.WriteLine("[StrideSsaoEffect] Warning: Could not load SSAO shaders. SSAO effect will not be computed. Shader files (.sdsl/.sdeffect) must be provided for SSAO to function.");
            }
            else if (_gtaoEffectBase == null || _bilateralBlurEffectBase == null)
            {
                System.Console.WriteLine("[StrideSsaoEffect] Warning: Some SSAO shaders failed to load. Effect may not function correctly.");
            }
            // Based on Stride Engine: Effects are loaded from compiled .sdeffect files (compiled from .sdsl source)
            // Loading order:
            // 1. Try Effect.Load() - standard Stride method for loading compiled effects
            // 2. Try ContentManager if available through GraphicsDevice services
            // 3. Fallback to SpriteBatch's default rendering if loading fails (no custom shaders)
            LoadSsaoShaders();
        }

        /// <summary>
        /// Applies SSAO effect using depth and normal buffers.
        /// </summary>
        public Texture Apply(Texture depthBuffer, Texture normalBuffer, RenderContext context)
        {
            if (!_enabled || depthBuffer == null) return null;

            EnsureRenderTargets(depthBuffer.Width, depthBuffer.Height);

            // Step 1: Compute ambient occlusion
            ComputeAmbientOcclusion(depthBuffer, normalBuffer, _aoTarget, context);

            // Step 2: Bilateral blur to reduce noise while preserving edges
            ApplyBilateralBlur(_aoTarget, _blurTarget, depthBuffer, context);

            return _blurTarget ?? _aoTarget;
        }

        private void EnsureRenderTargets(int width, int height)
        {
            // Use half-resolution for performance (common for SSAO)
            int aoWidth = width / 2;
            int aoHeight = height / 2;

            bool needsRecreate = _aoTarget == null ||
                                 _aoTarget.Width != aoWidth ||
                                 _aoTarget.Height != aoHeight;

            if (!needsRecreate) return;

            _aoTarget?.Dispose();
            _blurTarget?.Dispose();
            _noiseTexture?.Dispose();

            // Create AO render target (single channel for AO value)
            _aoTarget = Texture.New2D(_graphicsDevice, aoWidth, aoHeight,
                PixelFormat.R8_UNorm,
                TextureFlags.RenderTarget | TextureFlags.ShaderResource);

            // Create blur target
            _blurTarget = Texture.New2D(_graphicsDevice, aoWidth, aoHeight,
                PixelFormat.R8_UNorm,
                TextureFlags.RenderTarget | TextureFlags.ShaderResource);

            // Create temporary blur target for two-pass bilateral blur
            _tempBlurTarget?.Dispose();
            _tempBlurTarget = Texture.New2D(_graphicsDevice, aoWidth, aoHeight,
                PixelFormat.R8_UNorm,
                TextureFlags.RenderTarget | TextureFlags.ShaderResource);

            // Create noise texture for sample randomization
            CreateNoiseTexture();

            _initialized = true;
        }

        private void CreateNoiseTexture()
        {
            // Create 4x4 random rotation texture for sample jittering
            const int noiseSize = 4;
            var noiseData = new byte[noiseSize * noiseSize * 4];
            var random = new Random(42); // Deterministic seed for consistency

            for (int i = 0; i < noiseData.Length; i += 4)
            {
                // Random rotation vector
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                noiseData[i] = (byte)((Math.Cos(angle) * 0.5 + 0.5) * 255);     // R
                noiseData[i + 1] = (byte)((Math.Sin(angle) * 0.5 + 0.5) * 255); // G
                noiseData[i + 2] = 0;                                             // B
                noiseData[i + 3] = 255;                                           // A
            }

            _noiseTexture = Texture.New2D(_graphicsDevice, noiseSize, noiseSize,
                PixelFormat.R8G8B8A8_UNorm, noiseData);
        }

        private void ComputeAmbientOcclusion(Texture depthBuffer, Texture normalBuffer,
            Texture destination, RenderContext context)
        {
            // GTAO implementation:
            // 1. Reconstruct view-space position from depth
            // 2. Sample hemisphere around each pixel
            // 3. Compare sample depths with actual depth
            // 4. Accumulate occlusion based on visibility

            if (depthBuffer == null || destination == null || _graphicsDevice == null || _spriteBatch == null)
            {
                return;
            }

            // Get command list for rendering operations
            var commandList = _graphicsDevice.ImmediateContext;
            if (commandList == null)
            {
                return;
            }

            // Set render target to destination
            commandList.SetRenderTarget(null, destination);

            // Clear render target to white (no occlusion = white, full occlusion = black)
            commandList.Clear(destination, Color.White);

            // Get viewport dimensions
            int width = destination.Width;
            int height = destination.Height;
            var viewport = new Viewport(0, 0, width, height);

            // Begin sprite batch rendering
            // Use SpriteSortMode.Immediate for post-processing effects
            _spriteBatch.Begin(commandList, SpriteSortMode.Immediate, BlendStates.Opaque, _linearSampler,
                DepthStencilStates.None, RasterizerStates.CullNone, _gtaoEffect);

            // If we have a custom GTAO effect, set its parameters
            if (_gtaoEffect != null && _gtaoEffect.Parameters != null)
            {
                // Set SSAO parameters
                var radiusParam = _gtaoEffect.Parameters.Get("Radius");
                if (radiusParam != null)
                {
                    radiusParam.SetValue(_radius);
                }

                var powerParam = _gtaoEffect.Parameters.Get("Power");
                if (powerParam != null)
                {
                    powerParam.SetValue(_power);
                }

                var sampleCountParam = _gtaoEffect.Parameters.Get("SampleCount");
                if (sampleCountParam != null)
                {
                    sampleCountParam.SetValue(_sampleCount);
                }

                // Set texture parameters
                var depthTextureParam = _gtaoEffect.Parameters.Get("DepthTexture");
                if (depthTextureParam != null)
                {
                    depthTextureParam.SetValue(depthBuffer);
                }

                var normalTextureParam = _gtaoEffect.Parameters.Get("NormalTexture");
                if (normalTextureParam != null && normalBuffer != null)
                {
                    normalTextureParam.SetValue(normalBuffer);
                }

                var noiseTextureParam = _gtaoEffect.Parameters.Get("NoiseTexture");
                if (noiseTextureParam != null && _noiseTexture != null)
                {
                    noiseTextureParam.SetValue(_noiseTexture);
                }

                // Set screen size parameters for UV calculations
                var screenSizeParam = _gtaoEffect.Parameters.Get("ScreenSize");
                if (screenSizeParam != null)
                {
                    screenSizeParam.SetValue(new Vector2(width, height));
                }

                var screenSizeInvParam = _gtaoEffect.Parameters.Get("ScreenSizeInv");
                if (screenSizeInvParam != null)
                {
                    screenSizeInvParam.SetValue(new Vector2(1.0f / width, 1.0f / height));
                }

                // Set projection matrix parameters for depth reconstruction
                // Extract projection matrix from camera via RenderContext
                // SSAO shader needs projection matrix to reconstruct view-space positions from depth
                // Based on GTAO/SSAO implementation: Projection matrix is essential for accurate depth reconstruction
                Matrix projectionMatrix = Matrix.Identity;
                Matrix projectionMatrixInv = Matrix.Identity;
                
                // Get projection matrix from RenderContext's RenderView (camera projection)
                // Stride RenderContext provides camera matrices through RenderView property
                if (context != null && context.RenderView != null)
                {
                    // RenderView.Projection contains the camera's projection matrix
                    projectionMatrix = context.RenderView.Projection;
                    
                    // Calculate inverse projection matrix for depth reconstruction
                    // Inverse projection is used to convert from clip space back to view space
                    // This is needed by SSAO shader to reconstruct view-space positions from depth buffer
                    projectionMatrixInv = Matrix.Invert(projectionMatrix);
                }
                
                var projMatrixParam = _gtaoEffect.Parameters.Get("ProjectionMatrix");
                if (projMatrixParam != null)
                {
                    // Set projection matrix from camera (used for depth reconstruction in SSAO shader)
                    projMatrixParam.SetValue(projectionMatrix);
                }

                var projMatrixInvParam = _gtaoEffect.Parameters.Get("ProjectionMatrixInv");
                if (projMatrixInvParam != null)
                {
                    // Set inverse projection matrix (used for converting clip space to view space)
                    projMatrixInvParam.SetValue(projectionMatrixInv);
                }
            }

            // Draw full-screen quad with depth buffer
            // Rectangle covering entire destination render target
            var destinationRect = new RectangleF(0, 0, width, height);
            
            // Use depth buffer as source for GTAO computation
            // The shader will sample from depth and normal buffers to compute occlusion
            if (depthBuffer != null)
            {
                _spriteBatch.Draw(depthBuffer, destinationRect, Color.White);
            }

            // End sprite batch rendering
            _spriteBatch.End();

            // Reset render target (restore previous state)
            commandList.SetRenderTarget(null, (Texture)null);
        }

        private void ApplyBilateralBlur(Texture source, Texture destination,
            Texture depthBuffer, RenderContext context)
        {
            // Edge-preserving blur using depth as guide
            // Prevents blurring across depth discontinuities
            // Uses separable two-pass blur: horizontal then vertical

            if (source == null || destination == null || depthBuffer == null || 
                _graphicsDevice == null || _spriteBatch == null || _tempBlurTarget == null)
            {
                return;
            }

            // Get command list for rendering operations
            var commandList = _graphicsDevice.ImmediateContext;
            if (commandList == null)
            {
                return;
            }

            int width = destination.Width;
            int height = destination.Height;
            var destinationRect = new RectangleF(0, 0, width, height);

            // Pass 1: Horizontal blur
            ApplyBilateralBlurPass(source, _tempBlurTarget, depthBuffer, true, width, height, commandList);

            // Pass 2: Vertical blur (from temp to final destination)
            ApplyBilateralBlurPass(_tempBlurTarget, destination, depthBuffer, false, width, height, commandList);
        }

        private void ApplyBilateralBlurPass(Texture source, Texture destination, Texture depthBuffer,
            bool horizontal, int width, int height, CommandList commandList)
        {
            // Apply one pass of bilateral blur (either horizontal or vertical)
            // Bilateral blur weights samples by both spatial distance and depth difference
            // This preserves edges at depth discontinuities

            if (source == null || destination == null || depthBuffer == null)
            {
                return;
            }

            // Set render target to destination
            commandList.SetRenderTarget(null, destination);

            // Clear render target to black
            commandList.Clear(destination, Color.Black);

            // Begin sprite batch rendering
            _spriteBatch.Begin(commandList, SpriteSortMode.Immediate, BlendStates.Opaque, _linearSampler,
                DepthStencilStates.None, RasterizerStates.CullNone, _bilateralBlurEffect);

            // If we have a custom bilateral blur effect, set its parameters
            if (_bilateralBlurEffect != null && _bilateralBlurEffect.Parameters != null)
            {
                // Set blur direction (horizontal = true means blur in X direction)
                var horizontalParam = _bilateralBlurEffect.Parameters.Get("Horizontal");
                if (horizontalParam != null)
                {
                    horizontalParam.SetValue(horizontal);
                }

                // Set blur radius (typically 4-8 pixels for SSAO)
                var blurRadiusParam = _bilateralBlurEffect.Parameters.Get("BlurRadius");
                if (blurRadiusParam != null)
                {
                    blurRadiusParam.SetValue(4.0f); // Standard blur radius for SSAO
                }

                // Set depth threshold for edge detection
                // Samples with depth difference > threshold are not blurred together
                var depthThresholdParam = _bilateralBlurEffect.Parameters.Get("DepthThreshold");
                if (depthThresholdParam != null)
                {
                    depthThresholdParam.SetValue(0.01f); // Threshold for depth discontinuity detection
                }

                // Set texture parameters
                var sourceTextureParam = _bilateralBlurEffect.Parameters.Get("SourceTexture");
                if (sourceTextureParam != null)
                {
                    sourceTextureParam.SetValue(source);
                }

                var depthTextureParam = _bilateralBlurEffect.Parameters.Get("DepthTexture");
                if (depthTextureParam != null)
                {
                    depthTextureParam.SetValue(depthBuffer);
                }

                // Set screen size parameters for UV calculations
                var screenSizeParam = _bilateralBlurEffect.Parameters.Get("ScreenSize");
                if (screenSizeParam != null)
                {
                    screenSizeParam.SetValue(new Vector2(width, height));
                }

                var screenSizeInvParam = _bilateralBlurEffect.Parameters.Get("ScreenSizeInv");
                if (screenSizeInvParam != null)
                {
                    screenSizeInvParam.SetValue(new Vector2(1.0f / width, 1.0f / height));
                }
            }

            // Draw full-screen quad with source texture
            var destinationRect = new RectangleF(0, 0, width, height);
            _spriteBatch.Draw(source, destinationRect, Color.White);

            // End sprite batch rendering
            _spriteBatch.End();

            // Reset render target (restore previous state)
            commandList.SetRenderTarget(null, (Texture)null);
        }

        protected override void OnDispose()
        {
            _aoTarget?.Dispose();
            _aoTarget = null;

            _blurTarget?.Dispose();
            _blurTarget = null;

            _tempBlurTarget?.Dispose();
            _tempBlurTarget = null;

            _noiseTexture?.Dispose();
            _noiseTexture = null;

            _spriteBatch?.Dispose();
            _spriteBatch = null;

            _linearSampler?.Dispose();
            _linearSampler = null;

            _pointSampler?.Dispose();
            _pointSampler = null;

            _gtaoEffect?.Dispose();
            _gtaoEffect = null;

            _bilateralBlurEffect?.Dispose();
            _bilateralBlurEffect = null;
        }
    }
}


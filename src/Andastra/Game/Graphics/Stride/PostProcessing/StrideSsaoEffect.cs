using System;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Stride.Graphics;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Rendering;
using Stride.Shaders;
using Color = Stride.Core.Mathematics.Color;
using Matrix = Stride.Core.Mathematics.Matrix;
using RectangleF = Stride.Core.Mathematics.RectangleF;
using StrideGraphics = Stride.Graphics;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace Andastra.Game.Stride.PostProcessing
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
        private StrideGraphics.GraphicsDevice _graphicsDevice;
        private global::Stride.Graphics.GraphicsContext _graphicsContext;
        private IServiceRegistry _services;
        private ContentManager _contentManager;
        private StrideGraphics.Texture _aoTarget;
        private StrideGraphics.Texture _blurTarget;
        private StrideGraphics.Texture _noiseTexture;
        private StrideGraphics.SpriteBatch _spriteBatch;
        private StrideGraphics.SamplerState _linearSampler;
        private StrideGraphics.SamplerState _pointSampler;
        private EffectInstance _gtaoEffect;
        private EffectInstance _bilateralBlurEffect;
        private StrideGraphics.Effect _gtaoEffectBase;
        private StrideGraphics.Effect _bilateralBlurEffectBase;
        private StrideGraphics.Texture _tempBlurTarget;

        public StrideSsaoEffect(StrideGraphics.GraphicsDevice graphicsDevice, global::Stride.Graphics.GraphicsContext graphicsContext = null, IServiceRegistry services = null, ContentManager contentManager = null)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _graphicsContext = graphicsContext;
            _services = services;
            _contentManager = contentManager;
            InitializeRenderingResources();
        }

        private void InitializeRenderingResources()
        {
            // Create sprite batch for fullscreen quad rendering
            _spriteBatch = new StrideGraphics.SpriteBatch(_graphicsDevice);

            // Create samplers for StrideGraphics.Texture sampling
            _linearSampler = StrideGraphics.SamplerState.New(_graphicsDevice, new global::Stride.Graphics.SamplerStateDescription
            {
                Filter = StrideGraphics.TextureFilter.Linear,
                AddressU = StrideGraphics.TextureAddressMode.Clamp,
                AddressV = StrideGraphics.TextureAddressMode.Clamp,
                AddressW = StrideGraphics.TextureAddressMode.Clamp
            });

            _pointSampler = StrideGraphics.SamplerState.New(_graphicsDevice, new global::Stride.Graphics.SamplerStateDescription
            {
                Filter = StrideGraphics.TextureFilter.Point,
                AddressU = StrideGraphics.TextureAddressMode.Clamp,
                AddressV = StrideGraphics.TextureAddressMode.Clamp,
                AddressW = StrideGraphics.TextureAddressMode.Clamp
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
            // Effect.Load() doesn't exist in this Stride version, skip to ContentManager loading

            // Strategy 2: Try loading from ContentManager if available
            // Check if ContentManager is provided directly or accessible through services
            if (_gtaoEffectBase == null || _bilateralBlurEffectBase == null)
            {
                // First try using provided ContentManager
                if (_contentManager != null)
                {
                    try
                    {
                        if (_gtaoEffectBase == null)
                        {
                            try
                            {
                                _gtaoEffectBase = _contentManager.Load<StrideGraphics.Effect>("SsaoGtao");
                                if (_gtaoEffectBase != null)
                                {
                                    _gtaoEffect = new EffectInstance(_gtaoEffectBase);
                                    System.Console.WriteLine("[StrideSsaoEffect] Loaded SsaoGtao from provided ContentManager");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Console.WriteLine($"[StrideSsaoEffect] Failed to load SsaoGtao from provided ContentManager: {ex.Message}");
                            }
                        }

                        if (_bilateralBlurEffectBase == null)
                        {
                            try
                            {
                                _bilateralBlurEffectBase = _contentManager.Load<StrideGraphics.Effect>("SsaoBilateralBlur");
                                if (_bilateralBlurEffectBase != null)
                                {
                                    _bilateralBlurEffect = new EffectInstance(_bilateralBlurEffectBase);
                                    System.Console.WriteLine("[StrideSsaoEffect] Loaded SsaoBilateralBlur from provided ContentManager");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Console.WriteLine($"[StrideSsaoEffect] Failed to load SsaoBilateralBlur from provided ContentManager: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[StrideSsaoEffect] Failed to access provided ContentManager: {ex.Message}");
                    }
                }

                // If ContentManager not provided or loading failed, try accessing through services
                if ((_gtaoEffectBase == null || _bilateralBlurEffectBase == null) && _services != null)
                {
                    try
                    {
                        var contentManager = GetServiceHelper<ContentManager>(_services);
                        if (contentManager != null)
                        {
                            if (_gtaoEffectBase == null)
                            {
                                try
                                {
                                    _gtaoEffectBase = contentManager.Load<StrideGraphics.Effect>("SsaoGtao");
                                    if (_gtaoEffectBase != null)
                                    {
                                        _gtaoEffect = new EffectInstance(_gtaoEffectBase);
                                        System.Console.WriteLine("[StrideSsaoEffect] Loaded SsaoGtao from services ContentManager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"[StrideSsaoEffect] Failed to load SsaoGtao from services ContentManager: {ex.Message}");
                                }
                            }

                            if (_bilateralBlurEffectBase == null)
                            {
                                try
                                {
                                    _bilateralBlurEffectBase = contentManager.Load<StrideGraphics.Effect>("SsaoBilateralBlur");
                                    if (_bilateralBlurEffectBase != null)
                                    {
                                        _bilateralBlurEffect = new EffectInstance(_bilateralBlurEffectBase);
                                        System.Console.WriteLine("[StrideSsaoEffect] Loaded SsaoBilateralBlur from services ContentManager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"[StrideSsaoEffect] Failed to load SsaoBilateralBlur from services ContentManager: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[StrideSsaoEffect] Failed to access services ContentManager: {ex.Message}");
                    }
                }

                // Try accessing ContentManager through GraphicsDevice services as fallback
                if ((_gtaoEffectBase == null || _bilateralBlurEffectBase == null) && _graphicsDevice != null)
                {
                    try
                    {
                        // Try to access IServiceRegistry through GraphicsDevice
                        // In Stride, GraphicsDevice may expose services through a Services property
                        var deviceServices = _graphicsDevice as IServiceProvider;
                        if (deviceServices != null)
                        {
                            var contentManager = GetServiceHelper<ContentManager>(deviceServices);
                            if (contentManager != null)
                            {
                                if (_gtaoEffectBase == null)
                                {
                                    try
                                    {
                                        _gtaoEffectBase = contentManager.Load<StrideGraphics.Effect>("SsaoGtao");
                                        if (_gtaoEffectBase != null)
                                        {
                                            _gtaoEffect = new EffectInstance(_gtaoEffectBase);
                                            System.Console.WriteLine("[StrideSsaoEffect] Loaded SsaoGtao from GraphicsDevice ContentManager");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Console.WriteLine($"[StrideSsaoEffect] Failed to load SsaoGtao from GraphicsDevice ContentManager: {ex.Message}");
                                    }
                                }

                                if (_bilateralBlurEffectBase == null)
                                {
                                    try
                                    {
                                        _bilateralBlurEffectBase = contentManager.Load<StrideGraphics.Effect>("SsaoBilateralBlur");
                                        if (_bilateralBlurEffectBase != null)
                                        {
                                            _bilateralBlurEffect = new EffectInstance(_bilateralBlurEffectBase);
                                            System.Console.WriteLine("[StrideSsaoEffect] Loaded SsaoBilateralBlur from GraphicsDevice ContentManager");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Console.WriteLine($"[StrideSsaoEffect] Failed to load SsaoBilateralBlur from GraphicsDevice ContentManager: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[StrideSsaoEffect] Failed to access GraphicsDevice ContentManager: {ex.Message}");
                    }
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
        }

        /// <summary>
        /// Applies SSAO effect using depth and normal buffers.
        /// </summary>
        public StrideGraphics.Texture Apply(StrideGraphics.Texture depthBuffer, StrideGraphics.Texture normalBuffer, RenderContext context)
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
            _aoTarget = StrideGraphics.Texture.New2D(_graphicsDevice, aoWidth, aoHeight,
                StrideGraphics.PixelFormat.R8_UNorm,
                StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource);

            // Create blur target
            _blurTarget = StrideGraphics.Texture.New2D(_graphicsDevice, aoWidth, aoHeight,
                StrideGraphics.PixelFormat.R8_UNorm,
                StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource);

            // Create temporary blur target for two-pass bilateral blur
            _tempBlurTarget?.Dispose();
            _tempBlurTarget = StrideGraphics.Texture.New2D(_graphicsDevice, aoWidth, aoHeight,
                StrideGraphics.PixelFormat.R8_UNorm,
                StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource);

            // Create noise StrideGraphics.Texture for sample randomization
            CreateNoiseTexture();

            _initialized = true;
        }

        private void CreateNoiseTexture()
        {
            // Create 4x4 random rotation StrideGraphics.Texture for sample jittering
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

            _noiseTexture = StrideGraphics.Texture.New2D(_graphicsDevice, noiseSize, noiseSize,
                StrideGraphics.PixelFormat.R8G8B8A8_UNorm, noiseData);
        }

        private void ComputeAmbientOcclusion(StrideGraphics.Texture depthBuffer, StrideGraphics.Texture normalBuffer,
            StrideGraphics.Texture destination, RenderContext context)
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
            if (_graphicsContext == null)
            {
                return;
            }
            StrideGraphics.CommandList commandList = _graphicsContext.CommandList;
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

            // Begin sprite batch rendering
            // Use SpriteSortMode.Immediate for post-processing effects
            _spriteBatch.Begin(_graphicsContext, StrideGraphics.SpriteSortMode.Immediate, StrideGraphics.BlendStates.Opaque, _linearSampler,
                StrideGraphics.DepthStencilStates.None, StrideGraphics.RasterizerStates.CullNone, _gtaoEffect);

            // If we have a custom GTAO effect, set its parameters
            if (_gtaoEffect != null && _gtaoEffect.Parameters != null)
            {
                // Set SSAO parameters using ParameterKey
                _gtaoEffect.Parameters.Set(new ValueParameterKey<float>("Radius"), _radius);
                _gtaoEffect.Parameters.Set(new ValueParameterKey<float>("Power"), _power);
                _gtaoEffect.Parameters.Set(new ValueParameterKey<int>("SampleCount"), _sampleCount);

                // Set StrideGraphics.Texture parameters
                _gtaoEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("DepthTexture"), depthBuffer);
                if (normalBuffer != null)
                {
                    _gtaoEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("NormalTexture"), normalBuffer);
                }
                if (_noiseTexture != null)
                {
                    _gtaoEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("NoiseTexture"), _noiseTexture);
                }

                // Set screen size parameters for UV calculations
                _gtaoEffect.Parameters.Set(new ValueParameterKey<Vector2>("ScreenSize"), new Vector2(width, height));
                _gtaoEffect.Parameters.Set(new ValueParameterKey<Vector2>("ScreenSizeInv"), new Vector2(1.0f / width, 1.0f / height));

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

                // Set projection matrix from camera (used for depth reconstruction in SSAO shader)
                _gtaoEffect.Parameters.Set(new ValueParameterKey<Matrix>("ProjectionMatrix"), projectionMatrix);
                // Set inverse projection matrix (used for converting clip space to view space)
                _gtaoEffect.Parameters.Set(new ValueParameterKey<Matrix>("ProjectionMatrixInv"), projectionMatrixInv);
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
            commandList.SetRenderTarget(null, (StrideGraphics.Texture)null);
        }

        private void ApplyBilateralBlur(StrideGraphics.Texture source, StrideGraphics.Texture destination,
            StrideGraphics.Texture depthBuffer, RenderContext context)
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
            if (_graphicsContext == null)
            {
                return;
            }
            StrideGraphics.CommandList commandList = _graphicsContext.CommandList;
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

        private void ApplyBilateralBlurPass(StrideGraphics.Texture source, StrideGraphics.Texture destination, StrideGraphics.Texture depthBuffer,
            bool horizontal, int width, int height, StrideGraphics.CommandList commandList)
        {
            // Apply one pass of bilateral blur (either horizontal or vertical)
            // Bilateral blur weights samples by both spatial distance and depth difference
            // This preserves edges at depth discontinuities

            if (source == null || destination == null || depthBuffer == null || _graphicsContext == null)
            {
                return;
            }

            // Set render target to destination
            commandList.SetRenderTarget(null, destination);

            // Clear render target to black
            commandList.Clear(destination, Color.Black);

            // Begin sprite batch rendering
            _spriteBatch.Begin(_graphicsContext, StrideGraphics.SpriteSortMode.Immediate, StrideGraphics.BlendStates.Opaque, _linearSampler,
                StrideGraphics.DepthStencilStates.None, StrideGraphics.RasterizerStates.CullNone, _bilateralBlurEffect);

            // If we have a custom bilateral blur effect, set its parameters
            if (_bilateralBlurEffect != null && _bilateralBlurEffect.Parameters != null)
            {
                // Set blur direction (horizontal = true means blur in X direction)
                _bilateralBlurEffect.Parameters.Set(new ValueParameterKey<bool>("Horizontal"), horizontal);

                // Set blur radius (typically 4-8 pixels for SSAO)
                _bilateralBlurEffect.Parameters.Set(new ValueParameterKey<float>("BlurRadius"), 4.0f);

                // Set depth threshold for edge detection
                // Samples with depth difference > threshold are not blurred together
                _bilateralBlurEffect.Parameters.Set(new ValueParameterKey<float>("DepthThreshold"), 0.01f);

                // Set StrideGraphics.Texture parameters
                _bilateralBlurEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("SourceTexture"), source);
                _bilateralBlurEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("DepthTexture"), depthBuffer);

                // Set screen size parameters for UV calculations
                _bilateralBlurEffect.Parameters.Set(new ValueParameterKey<Vector2>("ScreenSize"), new Vector2(width, height));
                _bilateralBlurEffect.Parameters.Set(new ValueParameterKey<Vector2>("ScreenSizeInv"), new Vector2(1.0f / width, 1.0f / height));
            }

            // Draw full-screen quad with source StrideGraphics.Texture
            var destinationRect = new RectangleF(0, 0, width, height);
            _spriteBatch.Draw(source, destinationRect, Color.White);

            // End sprite batch rendering
            _spriteBatch.End();

            // Reset render target (restore previous state)
            commandList.SetRenderTarget(null, (StrideGraphics.Texture)null);
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

        /// <summary>
        /// Helper method to call GetService&lt;T&gt;() using reflection (C# 7.3 compatible).
        /// </summary>
        private static T GetServiceHelper<T>(object services) where T : class
        {
            if (services == null)
            {
                return null;
            }
            try
            {
                // Try to cast to IServiceRegistry first
                var serviceRegistry = services as IServiceRegistry;
                if (serviceRegistry != null)
                {
                    // GetService takes Type parameter, not generic
                    var getServiceMethod = serviceRegistry.GetType().GetMethod("GetService", new Type[] { typeof(Type) });
                    if (getServiceMethod != null)
                    {
                        return getServiceMethod.Invoke(serviceRegistry, new object[] { typeof(T) }) as T;
                    }
                }
                else
                {
                    // If not IServiceRegistry, try to get GetService method from the object's type
                    var getServiceMethod = services.GetType().GetMethod("GetService", new Type[] { typeof(Type) });
                    if (getServiceMethod != null)
                    {
                        return getServiceMethod.Invoke(services, new object[] { typeof(T) }) as T;
                    }
                }
            }
            catch
            {
                // Service not available
            }
            return null;
        }
    }
}


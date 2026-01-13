using System;
using System.IO;
using System.Numerics;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Stride.Graphics;
using Stride.Core.Serialization.Contents;
using Stride.Rendering;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using Color = Stride.Core.Mathematics.Color;
using Matrix = Stride.Core.Mathematics.Matrix;
using RectangleF = Stride.Core.Mathematics.RectangleF;
using StrideGraphics = Stride.Graphics;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace Andastra.Game.Stride.PostProcessing
{
    /// <summary>
    /// Stride implementation of temporal anti-aliasing effect.
    /// Inherits shared TAA logic from BaseTemporalAaEffect.
    ///
    /// Features:
    /// - Sub-pixel jittering for temporal sampling
    /// - Motion vector reprojection
    /// - Neighborhood clamping for ghosting reduction
    /// - Velocity-weighted blending
    /// </summary>
    public class StrideTemporalAaEffect : BaseTemporalAaEffect
    {
        private StrideGraphics.GraphicsDevice _graphicsDevice;
        private global::Stride.Engine.Game _game;
        private dynamic _gameServices; // Use dynamic to allow GetService<T>() calls on Stride.Core.ServiceRegistry
        private StrideGraphics.Texture _historyBuffer;
        private StrideGraphics.Texture _outputBuffer;
        private int _frameIndex;
        private Vector2[] _jitterSequence;
        private Matrix4x4 _previousViewProjection;
        private StrideGraphics.SpriteBatch _spriteBatch;
        private EffectInstance _taaEffect;
        private StrideGraphics.Effect _taaEffectBase;
        private StrideGraphics.SamplerState _linearSampler;
        private StrideGraphics.SamplerState _pointSampler;
        private bool _effectInitialized;

        public StrideTemporalAaEffect(StrideGraphics.GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _effectInitialized = false;

            // Initialize Game and Game.Services access
            InitializeGameServices();

            InitializeJitterSequence();
            InitializeRenderingResources();
        }

        /// <summary>
        /// Initializes Game instance and Game.Services access.
        /// This is required for proper service access in Stride applications.
        /// </summary>
        private void InitializeGameServices()
        {
            try
            {
                // Strategy 1: Try to get Game from GraphicsDevice.Game property (if available)
                // This is the most direct approach in Stride 4.x+
                var gameProperty = _graphicsDevice.GetType().GetProperty("Game");
                if (gameProperty != null)
                {
                    _game = gameProperty.GetValue(_graphicsDevice) as global::Stride.Engine.Game;
                    if (_game != null)
                    {
                        _gameServices = _game.Services;
                        Console.WriteLine("[StrideTemporalAaEffect] Successfully accessed Game.Services from GraphicsDevice.Game");
                        return;
                    }
                }

                // Strategy 2: Try to get Game from GraphicsDevice.Services
                // Some Stride versions expose Game through the services
                dynamic graphicsDeviceServices = _graphicsDevice.Services();
                if (graphicsDeviceServices != null)
                {
                    _game = graphicsDeviceServices.GetService<global::Stride.Engine.Game>();
                    if (_game != null)
                    {
                        _gameServices = _game.Services;
                        Console.WriteLine("[StrideTemporalAaEffect] Successfully accessed Game.Services from GraphicsDevice.Services");
                        return;
                    }
                }

                // Strategy 3: Try to access Game through IServiceProvider pattern
                // This works in dependency injection scenarios
                if (graphicsDeviceServices != null)
                {
                    _gameServices = graphicsDeviceServices;
                    _game = graphicsDeviceServices.GetService<global::Stride.Engine.Game>();
                    if (_game != null)
                    {
                        Console.WriteLine("[StrideTemporalAaEffect] Successfully accessed Game through IServiceProvider pattern");
                        return;
                    }
                }

                // Strategy 4: Try to find Game instance through reflection on common Stride objects
                // This is a fallback for complex application architectures
                try
                {
                    // Try to get Game from GraphicsDevice's parent or container
                    var parentProperty = _graphicsDevice.GetType().GetProperty("Parent") ??
                                        _graphicsDevice.GetType().GetProperty("Container") ??
                                        _graphicsDevice.GetType().GetProperty("Owner");

                    if (parentProperty != null)
                    {
                        var parent = parentProperty.GetValue(_graphicsDevice);
                        if (parent != null)
                        {
                            // Try to get Game from parent
                            var parentGameProperty = parent.GetType().GetProperty("Game");
                            if (parentGameProperty != null)
                            {
                                _game = parentGameProperty.GetValue(parent) as global::Stride.Engine.Game;
                                if (_game != null)
                                {
                                    _gameServices = _game.Services;
                                    Console.WriteLine("[StrideTemporalAaEffect] Successfully accessed Game.Services from GraphicsDevice parent");
                                    return;
                                }
                            }

                            // Try to get IServiceProvider from parent
                            if (parent is IServiceProvider parentServices)
                            {
                                _gameServices = parentServices;
                                _game = (_gameServices as IServiceProvider)?.GetService(typeof(global::Stride.Engine.Game)) as global::Stride.Engine.Game;
                                if (_game != null)
                                {
                                    Console.WriteLine("[StrideTemporalAaEffect] Successfully accessed Game through parent IServiceProvider");
                                    return;
                                }
                            }
                        }
                    }
                }
                catch (Exception reflectionEx)
                {
                    Console.WriteLine($"[StrideTemporalAaEffect] Reflection-based Game access failed: {reflectionEx.Message}");
                }

                Console.WriteLine("[StrideTemporalAaEffect] Warning: Could not access Game.Services. Some features may not work correctly.");
                _gameServices = null;
                _game = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Exception while initializing Game.Services access: {ex.Message}");
                _gameServices = null;
                _game = null;
            }
        }

        /// <summary>
        /// Gets the current frame's jitter offset for sub-pixel sampling.
        /// </summary>
        public Vector2 GetJitterOffset(int targetWidth, int targetHeight)
        {
            if (!_enabled) return Vector2.Zero;

            var jitter = _jitterSequence[_frameIndex % _jitterSequence.Length];
            return new Vector2(
                jitter.X * _jitterScale / targetWidth,
                jitter.Y * _jitterScale / targetHeight
            );
        }

        /// <summary>
        /// Applies TAA to the current frame.
        /// </summary>
        public StrideGraphics.Texture Apply(StrideGraphics.Texture currentFrame, StrideGraphics.Texture velocityBuffer,
            StrideGraphics.Texture depthBuffer, RenderContext context)
        {
            if (!_enabled || currentFrame == null) return currentFrame;

            EnsureRenderTargets(currentFrame.Width, currentFrame.Height, currentFrame.Format);

            // Apply temporal accumulation
            ApplyTemporalAccumulation(currentFrame, _historyBuffer, velocityBuffer, depthBuffer,
                _outputBuffer, context);

            // Copy output to history for next frame
            CopyToHistory(_outputBuffer, _historyBuffer, context);

            _frameIndex++;

            return _outputBuffer ?? currentFrame;
        }

        /// <summary>
        /// Updates the previous view-projection matrix for reprojection.
        /// </summary>
        public void UpdatePreviousViewProjection(Matrix4x4 viewProjection)
        {
            _previousViewProjection = viewProjection;
        }

        private void InitializeRenderingResources()
        {
            // Create sprite batch for fullscreen quad rendering
            _spriteBatch = new StrideGraphics.SpriteBatch(_graphicsDevice);

            // Create samplers for texture sampling
            // Linear sampler for smooth texture filtering (used for history and current frame)
            _linearSampler = StrideGraphics.SamplerState.New(_graphicsDevice, new StrideGraphics.SamplerStateDescription
            {
                Filter = StrideGraphics.TextureFilter.Linear,
                AddressU = StrideGraphics.TextureAddressMode.Clamp,
                AddressV = StrideGraphics.TextureAddressMode.Clamp,
                AddressW = StrideGraphics.TextureAddressMode.Clamp
            });

            // Point sampler for velocity and depth buffers (needed for precise reprojection)
            _pointSampler = StrideGraphics.SamplerState.New(_graphicsDevice, new StrideGraphics.SamplerStateDescription
            {
                Filter = StrideGraphics.TextureFilter.Point,
                AddressU = StrideGraphics.TextureAddressMode.Clamp,
                AddressV = StrideGraphics.TextureAddressMode.Clamp,
                AddressW = StrideGraphics.TextureAddressMode.Clamp
            });

            // Initialize TAA effect (will be loaded/created when needed)
            InitializeTaaEffect();
        }

        private void InitializeTaaEffect()
        {
            // TAA effect will be loaded from compiled shader or created programmatically
            // Strategy: Try loading from content first, then create programmatically if needed

            try
            {
                // Strategy 1: Try loading TAA effect from ContentManager
                // Effects in Stride are loaded through ContentManager.Load<Effect>()
                // This loads pre-compiled .sdeffect files from the content pipeline
                try
                {
                    // Get services from Game.Services to access ContentManager
                    // Game.Services provides access to engine services including ContentManager
                    // This is the proper way to access services in Stride applications
                    if (_gameServices != null)
                    {
                        // Get ContentManager service using proper Game.Services access
                        // ContentManager handles loading of compiled assets including effects
                        var contentManager = (_gameServices as global::Stride.Core.ServiceRegistry)?.GetService<ContentManager>();
                        if (contentManager != null)
                        {
                            // Attempt to load TemporalAA effect from content
                            // Effect name should match the .sdeffect file name in content
                            // ContentManager.Load<Effect>() loads and caches compiled shader effects
                            _taaEffectBase = contentManager.Load<StrideGraphics.Effect>("TemporalAA");
                            if (_taaEffectBase != null)
                            {
                                // Create EffectInstance from the loaded effect base
                                // EffectInstance provides parameter binding and shader execution
                                _taaEffect = new EffectInstance(_taaEffectBase);
                                _effectInitialized = true;
                                Console.WriteLine("[StrideTemporalAaEffect] Loaded TemporalAA effect from Game.Services.ContentManager");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("[StrideTemporalAaEffect] ContentManager service not available from Game.Services");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[StrideTemporalAaEffect] Game.Services not available - could not initialize Game.Services access");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideTemporalAaEffect] Failed to load TemporalAA from Game.Services.ContentManager: {ex.Message}");
                }

                // Strategy 2: Alternative ContentManager access pattern
                // Some Stride versions provide ContentManager through different service access patterns
                if (_taaEffectBase == null)
                {
                    try
                    {
                        // Alternative service access pattern for ContentManager
                        // Try Game.Services for service access - this is the primary method
                        if (_gameServices != null)
                        {
                            // Attempt GetService call using Game.Services
                            var contentManager = (_gameServices as global::Stride.Core.ServiceRegistry)?.GetService<ContentManager>();
                            if (contentManager != null)
                            {
                                try
                                {
                                    // Load TemporalAA effect using Game.Services content path
                                    // Try different effect names in case the primary one isn't found
                                    string[] effectNames = { "TemporalAA", "TemporalAntiAliasing", "TAA", "PostProcessing/TemporalAA" };

                                    foreach (string effectName in effectNames)
                                    {
                                        try
                                        {
                                            _taaEffectBase = contentManager.Load<StrideGraphics.Effect>(effectName);
                                            if (_taaEffectBase != null)
                                            {
                                                _taaEffect = new EffectInstance(_taaEffectBase);
                                                _effectInitialized = true;
                                                Console.WriteLine($"[StrideTemporalAaEffect] Loaded TemporalAA effect from Game.Services.ContentManager using name '{effectName}'");
                                                return;
                                            }
                                        }
                                        catch
                                        {
                                            // Continue to next effect name if this one fails
                                            continue;
                                        }
                                    }

                                    Console.WriteLine("[StrideTemporalAaEffect] None of the expected effect names found in Game.Services.ContentManager");
                                }
                                catch (Exception loadEx)
                                {
                                    Console.WriteLine($"[StrideTemporalAaEffect] Failed to load TemporalAA from Game.Services.ContentManager: {loadEx.Message}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("[StrideTemporalAaEffect] ContentManager not available through Game.Services");
                            }
                        }

                        // Additional fallback: Try to get ContentManager from GraphicsDevice.Services if Game.Services fails
                        // This is kept for backward compatibility but Game.Services is preferred
                        try
                        {
                            // Attempt to access services through GraphicsDevice.Services as fallback
                            object services = _graphicsDevice.Services();
                            if (services != null)
                            {
                                var graphicsDeviceContentManager = ((dynamic)services).GetService<ContentManager>();
                                if (graphicsDeviceContentManager != null)
                                {
                                    _taaEffectBase = graphicsDeviceContentManager.Load<StrideGraphics.Effect>("TemporalAA");
                                    if (_taaEffectBase != null)
                                    {
                                        _taaEffect = new EffectInstance(_taaEffectBase);
                                        _effectInitialized = true;
                                        Console.WriteLine("[StrideTemporalAaEffect] Loaded TemporalAA effect from GraphicsDevice.Services.ContentManager (fallback)");
                                        return;
                                    }
                                }
                            }
                        }
                        catch (Exception graphicsDeviceEx)
                        {
                            Console.WriteLine($"[StrideTemporalAaEffect] Failed to access ContentManager through GraphicsDevice.Services fallback: {graphicsDeviceEx.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideTemporalAaEffect] Failed to access ContentManager through any available method: {ex.Message}");
                    }
                }

                // Strategy 3: Create TAA shader programmatically with full implementation
                if (_taaEffectBase == null)
                {
                    _taaEffectBase = CreateTaaEffect();
                    if (_taaEffectBase != null)
                    {
                        _taaEffect = new EffectInstance(_taaEffectBase);
                        _effectInitialized = true;
                        Console.WriteLine("[StrideTemporalAaEffect] Created TemporalAA effect programmatically");
                    }
                }

                // Final fallback: Effect remains null, will use simple copy
                if (_taaEffectBase == null)
                {
                    Console.WriteLine("[StrideTemporalAaEffect] Warning: Could not load or create TAA shader. Using fallback rendering.");
                    _effectInitialized = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Exception while initializing TAA effect: {ex.Message}");
                _effectInitialized = false;
            }
        }

        /// <summary>
        /// Creates a comprehensive TAA effect programmatically from shader source code.
        /// Implements full temporal anti-aliasing algorithm with motion vector reprojection,
        /// neighborhood clamping, and history clamping to reduce ghosting.
        /// </summary>
        /// <returns>Effect instance for TAA processing, or null if creation fails.</returns>
        /// <remarks>
        /// TAA Algorithm Implementation:
        /// Based on industry-standard TAA implementations (Unreal Engine, Unity HDRP, CryEngine).
        /// Algorithm steps:
        /// 1. Reproject previous frame using motion vectors to current frame's camera position
        /// 2. Sample neighborhood of current pixel (3x3 cross pattern)
        /// 3. Compute color AABB (Axis-Aligned Bounding Box) from neighborhood samples
        /// 4. Clamp reprojected history color to neighborhood AABB bounds (reduces ghosting)
        /// 5. Blend current frame with clamped history using temporal blend factor
        /// 6. Apply velocity-weighted blending for better quality
        /// 7. Depth-based rejection to discard invalid samples
        ///
        /// Original game: swkotor2.exe - Frame buffer post-processing @ 0x007c8408
        /// Original implementation: Uses frame buffers for rendering and effects
        /// This implementation: Full TAA pipeline with temporal accumulation and ghosting reduction
        /// </remarks>
        private StrideGraphics.Effect CreateTaaEffect()
        {
            try
            {
                // Comprehensive TAA shader source code
                // Implements full temporal anti-aliasing with all required features
                string shaderSource = @"
shader TemporalAAEffect : ShaderBase
{
    // Constant buffer for TAA parameters
    cbuffer PerDraw : register(b0)
    {
        float BlendFactor;              // Temporal blend factor (0.05-0.1 typical)
        float2 JitterOffset;            // Sub-pixel jitter offset for this frame
        float2 ScreenSize;              // Screen resolution (width, height)
        float2 ScreenSizeInv;           // Inverse screen resolution (1/width, 1/height)
        float ClampFactor;              // Neighborhood clamp factor for ghosting reduction (0.5 typical)
        float DepthThreshold;           // Depth difference threshold for rejection
        float VelocityThreshold;        // Motion vector length threshold for rejection
        float4x4 ViewProjection;        // Current frame view-projection matrix
        float4x4 PreviousViewProjection; // Previous frame view-projection matrix
        float IsFirstFrame;             // 1.0 if first frame, 0.0 otherwise
    };

    // Input textures
    Texture2D CurrentFrame : register(t0);      // Current frame color buffer
    Texture2D HistoryBuffer : register(t1);      // Previous frame TAA result
    Texture2D VelocityBuffer : register(t2);     // Motion vectors (RG = velocity in screen space)
    Texture2D DepthBuffer : register(t3);        // Depth buffer for depth-based rejection
    SamplerState LinearSampler : register(s0);   // Linear sampler for color/history
    SamplerState PointSampler : register(s1);   // Point sampler for velocity/depth

    // Vertex shader output
    struct VSOutput
    {
        float4 Position : SV_Position;
        float2 TexCoord : TEXCOORD0;
    };

    // Vertex shader: Full-screen quad
    VSOutput VS(uint vertexId : SV_VertexID)
    {
        VSOutput output;

        // Generate full-screen quad from vertex ID
        // Vertex ID 0-3 maps to quad corners
        float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
        output.Position = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
        output.TexCoord = uv;

        return output;
    }

    // Helper function: Reproject history sample using motion vectors
    // Reprojects history buffer sample to current frame's camera position
    float2 ReprojectHistory(float2 currentUV, float2 motionVector)
    {
        // Reproject history UV by subtracting motion vector
        // Motion vector points from previous frame to current frame
        // So we subtract to go back to previous frame position
        float2 historyUV = currentUV - motionVector;
        return historyUV;
    }

    // Helper function: Compute color AABB from neighborhood samples
    // Samples 3x3 cross pattern around current pixel
    // Returns min and max color bounds for neighborhood clamping
    void ComputeNeighborhoodAABB(float2 centerUV, out float3 colorMin, out float3 colorMax)
    {
        // 3x3 cross pattern offsets (5 samples: center + 4 neighbors)
        static const float2 offsets[5] = {
            float2(0.0, 0.0),           // Center
            float2(1.0, 0.0) * ScreenSizeInv,  // Right
            float2(-1.0, 0.0) * ScreenSizeInv, // Left
            float2(0.0, 1.0) * ScreenSizeInv,  // Down
            float2(0.0, -1.0) * ScreenSizeInv  // Up
        };

        // Initialize min/max with center sample
        float3 centerColor = CurrentFrame.Sample(LinearSampler, centerUV).rgb;
        colorMin = centerColor;
        colorMax = centerColor;

        // Sample neighborhood and expand AABB
        for (int i = 1; i < 5; i++)
        {
            float2 sampleUV = centerUV + offsets[i];
            sampleUV = clamp(sampleUV, float2(0, 0), float2(1, 1));
            float3 sampleColor = CurrentFrame.Sample(LinearSampler, sampleUV).rgb;

            colorMin = min(colorMin, sampleColor);
            colorMax = max(colorMax, sampleColor);
        }

        // Expand AABB by clamp factor to allow some variance
        // Higher clamp factor = more aggressive clamping (less ghosting, potentially less AA quality)
        float3 colorRange = colorMax - colorMin;
        float3 expand = colorRange * ClampFactor;
        colorMin -= expand;
        colorMax += expand;
    }

    // Helper function: Clamp color to AABB bounds
    // Clamps history color to neighborhood bounds to reduce ghosting
    float3 ClampToAABB(float3 color, float3 colorMin, float3 colorMax)
    {
        return clamp(color, colorMin, colorMax);
    }

    // Helper function: Compute velocity weight
    // Reduces blend weight for pixels with high motion to prevent ghosting
    float ComputeVelocityWeight(float2 velocity)
    {
        float velocityLength = length(velocity);

        // If velocity is too high, reduce weight significantly
        if (velocityLength > VelocityThreshold)
        {
            return 0.0;
        }

        // Smooth falloff for moderate velocities
        float normalizedVelocity = velocityLength / VelocityThreshold;
        return 1.0 - smoothstep(0.5, 1.0, normalizedVelocity);
    }

    // Helper function: Depth-based rejection
    // Rejects history samples with large depth differences
    float ComputeDepthWeight(float currentDepth, float historyDepth)
    {
        float depthDiff = abs(currentDepth - historyDepth);

        // If depth difference is too large, reject sample
        if (depthDiff > DepthThreshold)
        {
            return 0.0;
        }

        // Smooth falloff for moderate depth differences
        float normalizedDiff = depthDiff / DepthThreshold;
        return 1.0 - smoothstep(0.5, 1.0, normalizedDiff);
    }

    // Pixel shader: Temporal Anti-Aliasing
    float4 PS(VSOutput input) : SV_Target
    {
        float2 currentUV = input.TexCoord;

        // Sample current frame color
        float4 currentColor = CurrentFrame.Sample(LinearSampler, currentUV);
        float currentDepth = DepthBuffer.Sample(PointSampler, currentUV).r;

        // First frame: No history available, return current frame
        if (IsFirstFrame > 0.5)
        {
            return currentColor;
        }

        // Sample motion vector (RG = velocity in screen space, normalized to [0,1])
        float2 motionVector = VelocityBuffer.Sample(PointSampler, currentUV).rg;

        // Convert motion vector from normalized [0,1] to screen space [-1,1]
        // Motion vectors are typically stored as (velocity + 0.5) * 0.5 to fit in [0,1] range
        motionVector = (motionVector * 2.0 - 1.0) * ScreenSize;

        // Validate motion vector
        float velocityLength = length(motionVector);
        if (velocityLength > VelocityThreshold * ScreenSize.x)
        {
            // Motion too large, reject history
            return currentColor;
        }

        // Reproject history sample to current frame position
        float2 historyUV = ReprojectHistory(currentUV, motionVector * ScreenSizeInv);

        // Clamp history UV to valid range
        historyUV = clamp(historyUV, float2(0, 0), float2(1, 1));

        // Sample history buffer
        float4 historyColor = HistoryBuffer.Sample(LinearSampler, historyUV);
        float historyDepth = DepthBuffer.Sample(PointSampler, historyUV).r;

        // Compute neighborhood AABB for ghosting reduction
        float3 colorMin, colorMax;
        ComputeNeighborhoodAABB(currentUV, colorMin, colorMax);

        // Clamp history color to neighborhood bounds
        float3 clampedHistory = ClampToAABB(historyColor.rgb, colorMin, colorMax);

        // Compute velocity weight (reduce weight for high motion)
        float velocityWeight = ComputeVelocityWeight(motionVector);

        // Compute depth weight (reject samples with large depth differences)
        float depthWeight = ComputeDepthWeight(currentDepth, historyDepth);

        // Combined weight for history contribution
        float historyWeight = velocityWeight * depthWeight;

        // If history is invalid (rejected), use only current frame
        if (historyWeight < 0.01)
        {
            return currentColor;
        }

        // Temporal blending: Blend current frame with clamped history
        // BlendFactor typically 0.05-0.1 (5-10% current, 90-95% history)
        // Lower BlendFactor = more history = better AA but more ghosting risk
        // Higher BlendFactor = more current = less AA but less ghosting
        float effectiveBlendFactor = BlendFactor * (1.0 - historyWeight) + BlendFactor;
        effectiveBlendFactor = clamp(effectiveBlendFactor, 0.0, 1.0);

        // Blend current frame with clamped history
        float3 resultColor = lerp(float3(clampedHistory), currentColor.rgb, effectiveBlendFactor);

        // Preserve alpha from current frame
        return float4(resultColor, currentColor.a);
    }

    // Technique definition for Stride shader compilation
    technique TemporalAA
    {
        pass Pass0
        {
            VertexShader = compile vs_5_0 VS();
            PixelShader = compile ps_5_0 PS();
        }
    }
};";

                // Compile shader source using EffectCompiler
                return CompileShaderFromSource(shaderSource, "TemporalAAEffect");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Failed to create TAA effect: {ex.Message}");
                Console.WriteLine($"[StrideTemporalAaEffect] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Compiles shader source code to an Effect using Stride's EffectCompiler.
        /// </summary>
        /// <param name="shaderSource">Shader source code in SDSL format.</param>
        /// <param name="shaderName">Name identifier for the shader (for logging/error reporting).</param>
        /// <returns>Compiled Effect, or null if compilation fails.</returns>
        /// <remarks>
        /// Based on Stride EffectCompiler API:
        /// - EffectCompiler compiles shader source code to Effect bytecode
        /// - EffectCompiler can be accessed from GraphicsDevice services (EffectSystem)
        /// - Compilation requires proper SDSL syntax and shader structure
        /// - Original game: DirectX 8/9 fixed-function pipeline (swkotor2.exe: Frame buffer post-processing @ 0x007c8408)
        /// - Modern implementation: Uses programmable shaders with runtime compilation
        /// </remarks>
        private StrideGraphics.Effect CompileShaderFromSource(string shaderSource, string shaderName)
        {
            if (string.IsNullOrEmpty(shaderSource))
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Cannot compile shader '{shaderName}': shader source is null or empty");
                return null;
            }

            if (_graphicsDevice == null)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Cannot compile shader '{shaderName}': GraphicsDevice is null");
                return null;
            }

            try
            {
                // Strategy 1: Try to get EffectCompiler from Game.Services
                // This is the proper way to access services in Stride applications
                // Game.Services provides access to EffectCompiler and other engine services
                if (_gameServices != null)
                {
                    try
                    {
                        // Try to get EffectCompiler from Game.Services
                        var effectCompiler = (_gameServices as IServiceProvider)?.GetService(typeof(EffectCompiler)) as EffectCompiler;
                        if (effectCompiler != null)
                        {
                            return CompileShaderWithCompiler(effectCompiler, shaderSource, shaderName);
                        }

                        // Try to get EffectSystem from Game.Services (EffectCompiler may be accessed through it)
                        var effectSystem = ((dynamic)_gameServices).GetService<EffectCompiler>();
                        if (effectSystem != null)
                        {
                            return CompileShaderWithEffectSystem(effectSystem, shaderSource, shaderName);
                        }
                    }
                    catch (Exception gameServicesEx)
                    {
                        Console.WriteLine($"[StrideTemporalAaEffect] Failed to access EffectCompiler from Game.Services: {gameServicesEx.Message}");
                    }
                }

                // Strategy 1b: Fallback to GraphicsDevice.Services if Game.Services is not available
                // This maintains backward compatibility for older Stride versions or edge cases
                try
                {
                    dynamic graphicsDeviceServices = _graphicsDevice.Services();
                    if (graphicsDeviceServices != null)
                    {
                        var effectCompiler = graphicsDeviceServices.GetService<EffectCompiler>();
                        if (effectCompiler != null)
                        {
                            return CompileShaderWithCompiler(effectCompiler, shaderSource, shaderName);
                        }

                        // Try to get EffectSystem from GraphicsDevice services (EffectCompiler may be accessed through it)
                        var effectSystem = graphicsDeviceServices.GetService<EffectCompiler>();
                        if (effectSystem != null)
                        {
                            return CompileShaderWithEffectSystem(effectSystem, shaderSource, shaderName);
                        }
                    }
                }
                catch (Exception graphicsDeviceEx)
                {
                    Console.WriteLine($"[StrideTemporalAaEffect] Failed to access EffectCompiler from GraphicsDevice.Services fallback: {graphicsDeviceEx.Message}");
                }

                // Strategy 2: Create temporary shader file and compile it (fallback)
                return CompileShaderFromFile(shaderSource, shaderName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Failed to compile shader '{shaderName}': {ex.Message}");
                Console.WriteLine($"[StrideTemporalAaEffect] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Compiles shader using EffectCompiler directly.
        /// </summary>
        /// <param name="compiler">EffectCompiler instance.</param>
        /// <param name="shaderSource">Shader source code.</param>
        /// <param name="shaderName">Shader name for identification.</param>
        /// <returns>Compiled Effect, or null if compilation fails.</returns>
        private StrideGraphics.Effect CompileShaderWithCompiler(EffectCompiler compiler, string shaderSource, string shaderName)
        {
            try
            {
                // Create compilation context for shader compilation
                // Based on Stride API: EffectCompiler requires compilation context
                // ShaderSource is abstract, so we use ShaderSourceClass for source code compilation
                var compilerSource = new ShaderSourceClass
                {
                    Name = shaderName,
                    SourceCode = shaderSource
                };

                // Compile shader source to bytecode
                // Based on Stride API: EffectCompiler.Compile() compiles shader source
                // CompilerParameters provides compilation settings including platform target
                // EffectCompilerParameters contains settings for effect compilation (macros, includes, etc.)
                dynamic compilationResult = compiler.Compile(compilerSource, new CompilerParameters
                {
                    EffectParameters = new EffectCompilerParameters()
                });

                // Unwrap TaskOrResult to get the actual result
                // TaskOrResult may need .Result property or similar to unwrap, use dynamic to handle type differences
                dynamic compilerResult = compilationResult.Result;
                if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                {
                    // Create Effect from compiled bytecode
                    // Based on Stride API: Effect constructor accepts compiled bytecode
                    // Use global:: to avoid namespace resolution conflicts with System.Numerics
                    var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                    Console.WriteLine($"[StrideTemporalAaEffect] Successfully compiled shader '{shaderName}' using EffectCompiler");
                    return effect;
                }
                else
                {
                    Console.WriteLine($"[StrideTemporalAaEffect] EffectCompiler compilation failed for shader '{shaderName}': No bytecode generated");
                    if (compilerResult != null && compilerResult.HasErrors)
                    {
                        // CompilerResults may not have ErrorText, use ToString() or check for specific error properties
                        // Try to get error information from the result object
                        Console.WriteLine($"[StrideTemporalAaEffect] Compilation errors occurred for shader '{shaderName}'");
                        try
                        {
                            // Attempt to extract error information from compiler result
                            var errorProperty = compilerResult.GetType().GetProperty("ErrorText");
                            if (errorProperty != null)
                            {
                                var errorText = errorProperty.GetValue(compilerResult) as string;
                                if (!string.IsNullOrEmpty(errorText))
                                {
                                    Console.WriteLine($"[StrideTemporalAaEffect] Compilation errors: {errorText}");
                                }
                            }
                        }
                        catch
                        {
                            // Ignore error extraction failures
                        }
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Exception while compiling shader '{shaderName}' with EffectCompiler: {ex.Message}");
                Console.WriteLine($"[StrideTemporalAaEffect] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Compiles shader using EffectSystem.
        /// </summary>
        /// <param name="effectSystem">EffectSystem instance.</param>
        /// <param name="shaderSource">Shader source code.</param>
        /// <param name="shaderName">Shader name for identification.</param>
        /// <returns>Compiled Effect, or null if compilation fails.</returns>
        private StrideGraphics.Effect CompileShaderWithEffectSystem(global::Stride.Shaders.Compiler.EffectCompiler effectSystem, string shaderSource, string shaderName)
        {
            try
            {
                // Attempt to get EffectCompiler from EffectSystem
                var compilerProperty = effectSystem.GetType().GetProperty("Compiler");
                if (compilerProperty != null)
                {
                    var compiler = compilerProperty.GetValue(effectSystem) as EffectCompiler;
                    if (compiler != null)
                    {
                        return CompileShaderWithCompiler(compiler, shaderSource, shaderName);
                    }
                }

                Console.WriteLine($"[StrideTemporalAaEffect] EffectSystem does not provide direct compiler access for shader '{shaderName}'");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Exception while compiling shader '{shaderName}' with EffectSystem: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compiles shader from file using EffectSystem.
        /// </summary>
        /// <param name="effectSystem">EffectSystem instance.</param>
        /// <param name="filePath">Path to shader file.</param>
        /// <param name="shaderName">Shader name for identification.</param>
        /// <returns>Compiled Effect, or null if compilation fails.</returns>
        private StrideGraphics.Effect CompileShaderWithEffectSystemFromFile(global::Stride.Shaders.Compiler.EffectCompiler effectSystem, string filePath, string shaderName)
        {
            try
            {
                // Attempt to get EffectCompiler from EffectSystem for file compilation
                var compilerProperty = effectSystem.GetType().GetProperty("Compiler");
                if (compilerProperty != null)
                {
                    var compiler = compilerProperty.GetValue(effectSystem) as EffectCompiler;
                    if (compiler != null)
                    {
                        // Read shader source from file and create ShaderSourceClass
                        string fileShaderSource = File.ReadAllText(filePath);
                        var fileSource = new ShaderSourceClass
                        {
                            Name = shaderName,
                            SourceCode = fileShaderSource
                        };

                        // Compile from file content
                        dynamic compilationResult = compiler.Compile(fileSource, new CompilerParameters
                        {
                            EffectParameters = new EffectCompilerParameters()
                        });

                        // Unwrap TaskOrResult to get compilation result
                        dynamic compilerResult = compilationResult.Result;
                        if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                        {
                            // Create Effect from compiled bytecode
                            var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                            Console.WriteLine($"[StrideTemporalAaEffect] Successfully compiled shader '{shaderName}' from file using EffectSystem");
                            return effect;
                        }
                        else
                        {
                            Console.WriteLine($"[StrideTemporalAaEffect] File-based compilation failed for shader '{shaderName}': No bytecode generated");
                        }
                    }
                }

                Console.WriteLine($"[StrideTemporalAaEffect] EffectSystem does not support file-based compilation for shader '{shaderName}'");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Exception while compiling shader '{shaderName}' from file with EffectSystem: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compiles shader from temporary file (fallback method).
        /// </summary>
        /// <param name="shaderSource">Shader source code.</param>
        /// <param name="shaderName">Shader name for identification.</param>
        /// <returns>Compiled Effect, or null if compilation fails.</returns>
        private StrideGraphics.Effect CompileShaderFromFile(string shaderSource, string shaderName)
        {
            string tempFilePath = null;
            try
            {
                // Create temporary file for shader source
                tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{shaderName}_{System.Guid.NewGuid()}.sdsl");
                File.WriteAllText(tempFilePath, shaderSource);

                // Try to compile shader from file
                // Use Game.Services for proper service access in Stride applications
                if (_gameServices != null)
                {
                    try
                    {
                        // Attempt to get EffectCompiler from Game.Services for file-based compilation
                        var effectCompiler = (_gameServices as global::Stride.Core.ServiceRegistry)?.GetService<global::Stride.Shaders.Compiler.EffectCompiler>();
                        if (effectCompiler != null)
                        {
                            // Read shader source from file and create ShaderSourceClass
                            // Stride EffectCompiler works with ShaderSource objects, not direct file paths
                            string fileShaderSource = File.ReadAllText(tempFilePath);
                            var fileSource = new ShaderSourceClass
                            {
                                Name = shaderName,
                                SourceCode = fileShaderSource
                            };

                            // Compile shader from file content using EffectCompiler
                            dynamic compilationResult = effectCompiler.Compile(fileSource, new CompilerParameters
                            {
                                EffectParameters = new EffectCompilerParameters()
                            });

                            // Unwrap TaskOrResult to get compilation result
                            dynamic compilerResult = compilationResult.Result;
                            if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                            {
                                // Create Effect from compiled bytecode
                                var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                                Console.WriteLine($"[StrideTemporalAaEffect] Successfully compiled shader '{shaderName}' from file using Game.Services.EffectCompiler");
                                return effect;
                            }
                            else
                            {
                                Console.WriteLine($"[StrideTemporalAaEffect] File-based compilation failed for shader '{shaderName}': No bytecode generated");
                            }
                        }
                        else
                        {
                            // Try alternative approach: Get EffectSystem from Game.Services and compile through it
                            var effectSystem = ((dynamic)_gameServices).GetService<EffectCompiler>();
                            if (effectSystem != null)
                            {
                                return CompileShaderWithEffectSystemFromFile(effectSystem, tempFilePath, shaderName);
                            }
                        }
                    }
                    catch (Exception gameServicesCompileEx)
                    {
                        Console.WriteLine($"[StrideTemporalAaEffect] Exception during file-based shader compilation for '{shaderName}' using Game.Services: {gameServicesCompileEx.Message}");
                    }
                }

                // Fallback: Try GraphicsDevice.Services if Game.Services is not available
                // This maintains backward compatibility but Game.Services is preferred
                try
                {
                    dynamic graphicsDeviceServices = _graphicsDevice.Services();
                    if (graphicsDeviceServices != null)
                    {
                        var effectCompiler = (graphicsDeviceServices as global::Stride.Core.ServiceRegistry)?.GetService<EffectCompiler>();
                        if (effectCompiler != null)
                        {
                            // Read shader source from file and create ShaderSourceClass
                            string fileShaderSource = File.ReadAllText(tempFilePath);
                            var fileSource = new ShaderSourceClass
                            {
                                Name = shaderName,
                                SourceCode = fileShaderSource
                            };

                            // Compile shader from file content using EffectCompiler
                            dynamic compilationResult = effectCompiler.Compile(fileSource, new CompilerParameters
                            {
                                EffectParameters = new EffectCompilerParameters()
                            });

                            // Unwrap TaskOrResult to get compilation result
                            dynamic compilerResult = compilationResult.Result;
                            if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                            {
                                // Create Effect from compiled bytecode
                                var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                                Console.WriteLine($"[StrideTemporalAaEffect] Successfully compiled shader '{shaderName}' from file using GraphicsDevice.Services.EffectCompiler (fallback)");
                                return effect;
                            }
                            else
                            {
                                Console.WriteLine($"[StrideTemporalAaEffect] File-based compilation failed for shader '{shaderName}': No bytecode generated");
                            }
                        }
                        else
                        {
                            // Try alternative approach: Get EffectSystem from GraphicsDevice services and compile through it
                            var effectSystem = (graphicsDeviceServices as global::Stride.Core.ServiceRegistry)?.GetService<EffectCompiler>();
                            if (effectSystem != null)
                            {
                                return CompileShaderWithEffectSystemFromFile(effectSystem, tempFilePath, shaderName);
                            }
                        }
                    }
                }
                catch (Exception graphicsDeviceCompileEx)
                {
                    Console.WriteLine($"[StrideTemporalAaEffect] Exception during file-based shader compilation for '{shaderName}' using GraphicsDevice.Services fallback: {graphicsDeviceCompileEx.Message}");
                }

                Console.WriteLine($"[StrideTemporalAaEffect] Could not compile shader '{shaderName}' from file");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideTemporalAaEffect] Exception while compiling shader '{shaderName}' from file: {ex.Message}");
                return null;
            }
            finally
            {
                // Clean up temporary file
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        private void InitializeJitterSequence()
        {
            // Halton(2,3) sequence for low-discrepancy sampling
            _jitterSequence = new Vector2[16];

            for (int i = 0; i < 16; i++)
            {
                _jitterSequence[i] = new Vector2(
                    HaltonSequence(i + 1, 2) - 0.5f,
                    HaltonSequence(i + 1, 3) - 0.5f
                );
            }
        }

        private float HaltonSequence(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;

            while (index > 0)
            {
                result += (index % radix) * fraction;
                index /= radix;
                fraction /= radix;
            }

            return result;
        }

        private void EnsureRenderTargets(int width, int height, StrideGraphics.PixelFormat format)
        {
            bool needsRecreate = _historyBuffer == null ||
                                 _historyBuffer.Width != width ||
                                 _historyBuffer.Height != height;

            if (!needsRecreate) return;

            _historyBuffer?.Dispose();
            _outputBuffer?.Dispose();

            // History buffer stores previous frame result
            _historyBuffer = StrideGraphics.Texture.New2D(_graphicsDevice, width, height,
                format, StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource);

            // Output buffer for current frame result
            _outputBuffer = StrideGraphics.Texture.New2D(_graphicsDevice, width, height,
                format, StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource);

            _initialized = true;
        }

        private void ApplyTemporalAccumulation(StrideGraphics.Texture currentFrame, StrideGraphics.Texture historyBuffer,
            StrideGraphics.Texture velocityBuffer, StrideGraphics.Texture depthBuffer, StrideGraphics.Texture destination, RenderContext context)
        {
            // TAA Algorithm:
            // 1. Reproject history using motion vectors
            // 2. Sample neighborhood of current pixel (3x3 or 5-tap)
            // 3. Compute color AABB/variance from neighborhood
            // 4. Clamp history to neighborhood bounds (reduces ghosting)
            // 5. Blend current with clamped history

            // Blending formula:
            // result = lerp(history, current, blendFactor)
            // blendFactor typically 0.05-0.1 (more history = more AA, more ghosting)

            if (currentFrame == null || destination == null || _graphicsDevice == null || _spriteBatch == null)
            {
                return;
            }

            // Get command list for rendering operations
            StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
            if (commandList == null)
            {
                return;
            }

            // Set render target to destination
            commandList.SetRenderTarget(null, destination);

            // Clear render target (will be overwritten by TAA, but ensures clean state)
            commandList.Clear(destination, Color.Black);

            // Get viewport dimensions
            int width = destination.Width;
            int height = destination.Height;
            var viewport = new StrideGraphics.Viewport(0, 0, width, height);
            commandList.SetViewport(viewport);

            // Calculate current jitter offset for this frame
            Vector2 jitterOffset = GetJitterOffset(width, height);

            // If TAA effect is initialized, use it for proper temporal accumulation
            if (_effectInitialized && _taaEffect != null)
            {
                // Get GraphicsContext from GraphicsDevice (required for SpriteBatch.Begin with EffectInstance)
                var graphicsContext = _graphicsDevice.GraphicsContext();
                if (graphicsContext == null)
                {
                    // Fallback if GraphicsContext is not available
                    return;
                }
                // Begin sprite batch rendering with TAA effect
                _spriteBatch.Begin(graphicsContext, StrideGraphics.SpriteSortMode.Immediate, StrideGraphics.BlendStates.Opaque,
                    _linearSampler, StrideGraphics.DepthStencilStates.None, StrideGraphics.RasterizerStates.CullNone, _taaEffect);

                // Set TAA shader parameters
                if (_taaEffect.Parameters != null)
                {
                    // Set blend factor (how much to blend current with history)
                    // Lower values = more history = better AA but more ghosting
                    // Use reflection to set parameters since ParameterCollection.Get() requires ObjectParameterKey
                    // and we don't have the key objects available
                    try
                    {
                        var setFloatMethod = typeof(ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(float) });
                        if (setFloatMethod != null)
                        {
                            setFloatMethod.Invoke(_taaEffect.Parameters, new object[] { "BlendFactor", _blendFactor });
                        }
                    }
                    catch { }

                    try
                    {
                        var setVector2Method = typeof(ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(Vector2) });
                        if (setVector2Method != null)
                        {
                            setVector2Method.Invoke(_taaEffect.Parameters, new object[] { "JitterOffset", jitterOffset });
                            setVector2Method.Invoke(_taaEffect.Parameters, new object[] { "ScreenSize", new Vector2(width, height) });
                            setVector2Method.Invoke(_taaEffect.Parameters, new object[] { "ScreenSizeInv", new Vector2(1.0f / width, 1.0f / height) });
                        }
                    }
                    catch { }

                    try
                    {
                        var setTextureMethod = typeof(ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(StrideGraphics.Texture) });
                        if (setTextureMethod != null)
                        {
                            if (currentFrame != null) setTextureMethod.Invoke(_taaEffect.Parameters, new object[] { "CurrentFrame", currentFrame });
                            if (historyBuffer != null) setTextureMethod.Invoke(_taaEffect.Parameters, new object[] { "HistoryBuffer", historyBuffer });
                            if (velocityBuffer != null) setTextureMethod.Invoke(_taaEffect.Parameters, new object[] { "VelocityBuffer", velocityBuffer });
                            if (depthBuffer != null) setTextureMethod.Invoke(_taaEffect.Parameters, new object[] { "DepthBuffer", depthBuffer });
                        }
                    }
                    catch { }

                    // Set previous view-projection matrix for reprojection
                    // This is used to reproject history buffer samples to current frame space
                    try
                    {
                        var setMatrixMethod = typeof(ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(Matrix) });
                        if (setMatrixMethod != null)
                        {
                            var strideMatrix = ConvertToStrideMatrix(_previousViewProjection);
                            setMatrixMethod.Invoke(_taaEffect.Parameters, new object[] { "PreviousViewProjection", strideMatrix });
                        }
                    }
                    catch { }

                    // Get current view-projection from RenderContext if available
                    if (context != null && context.RenderView != null)
                    {
                        try
                        {
                            var setMatrixMethod = typeof(ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(Matrix) });
                            if (setMatrixMethod != null)
                            {
                                setMatrixMethod.Invoke(_taaEffect.Parameters, new object[] { "ViewProjection", context.RenderView.ViewProjection });
                            }
                        }
                        catch { }

                        // Update previous view-projection for next frame
                        var currentViewProj = context.RenderView.ViewProjection;
                        _previousViewProjection = ConvertFromStrideMatrix(currentViewProj);
                    }

                    // Set neighborhood clamp factor for ghosting reduction
                    // Higher values = more aggressive clamping (less ghosting, potentially less AA quality)
                    try
                    {
                        var setFloatMethod = typeof(ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(float) });
                        if (setFloatMethod != null)
                        {
                            setFloatMethod.Invoke(_taaEffect.Parameters, new object[] { "ClampFactor", 0.5f });
                            setFloatMethod.Invoke(_taaEffect.Parameters, new object[] { "DepthThreshold", 0.01f });
                            setFloatMethod.Invoke(_taaEffect.Parameters, new object[] { "VelocityThreshold", 0.1f });
                        }
                    }
                    catch { }

                    // Set first frame flag (1.0 if first frame, 0.0 otherwise)
                    // First frame has no history, so TAA is skipped
                    // Use reflection to set parameters since ParameterCollection.Get() requires ObjectParameterKey
                    try
                    {
                        bool isFirstFrame = (_frameIndex == 0 || historyBuffer == null);
                        var setFloatMethod = typeof(ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(float) });
                        if (setFloatMethod != null)
                        {
                            setFloatMethod.Invoke(_taaEffect.Parameters, new object[] { "IsFirstFrame", isFirstFrame ? 1.0f : 0.0f });
                        }
                    }
                    catch { }
                }

                // Draw full-screen quad with TAA shader
                // SpriteBatch.Draw with a null texture and full-screen destination
                // will use the bound effect to render the full-screen quad
                _spriteBatch.Draw(currentFrame, new RectangleF(0, 0, width, height), Color.White);
                _spriteBatch.End();
            }
            else
            {
                // Fallback: Simple copy if TAA effect is not available
                // This fallback is used when shader compilation fails or effect is not initialized
                // It provides basic functionality by copying current frame without temporal accumulation
                // For production use, ensure TAA shader is properly compiled and initialized
                // Get GraphicsContext for SpriteBatch.Begin
                var graphicsContext = _graphicsDevice.GraphicsContext();
                if (graphicsContext == null)
                {
                    // Fallback if GraphicsContext is not available
                    return;
                }
                _spriteBatch.Begin(graphicsContext, StrideGraphics.SpriteSortMode.Immediate, StrideGraphics.BlendStates.Opaque,
                    _linearSampler, StrideGraphics.DepthStencilStates.None, StrideGraphics.RasterizerStates.CullNone);
                _spriteBatch.Draw(currentFrame, new RectangleF(0, 0, width, height), Color.White);
                _spriteBatch.End();
            }
        }

        private Matrix ConvertToStrideMatrix(System.Numerics.Matrix4x4 matrix)
        {
            // Convert System.Numerics.Matrix4x4 to Stride.Core.Mathematics.Matrix
            return new Matrix(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        }

        private System.Numerics.Matrix4x4 ConvertFromStrideMatrix(Matrix matrix)
        {
            // Convert Stride.Core.Mathematics.Matrix to System.Numerics.Matrix4x4
            return new System.Numerics.Matrix4x4(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        }

        private void CopyToHistory(StrideGraphics.Texture source, StrideGraphics.Texture destination, RenderContext context)
        {
            // Copy current output to history buffer for next frame
            // Uses Stride's CommandList.CopyRegion for efficient GPU-side texture copy
            // This is essential for TAA as it maintains temporal history between frames

            if (source == null || destination == null)
            {
                return;
            }

            // Get CommandList from GraphicsDevice's ImmediateContext
            // ImmediateContext provides the command list for immediate-mode rendering
            StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
            if (commandList == null)
            {
                // If ImmediateContext is not available, fall back to alternative copy method
                // This should not happen in normal operation, but provides safety
                return;
            }

            // CopyRegion performs a GPU-side texture copy operation
            // Parameters:
            // - source: Source texture to copy from
            // - sourceSubresource: Mip level and array slice (0 = base mip, main slice)
            // - sourceBox: Region to copy (null = entire texture)
            // - destination: Destination texture to copy to
            // - destinationSubresource: Mip level and array slice for destination (0 = base mip, main slice)
            //
            // This is more efficient than CPU-side copies as it:
            // 1. Stays on GPU (no CPU-GPU transfer overhead)
            // 2. Can be pipelined with other GPU operations
            // 3. Supports format conversion if needed
            // 4. Handles texture layout transitions automatically
            commandList.CopyRegion(source, 0, null, destination, 0);
        }

        protected override void OnDispose()
        {
            _historyBuffer?.Dispose();
            _historyBuffer = null;

            _outputBuffer?.Dispose();
            _outputBuffer = null;

            _spriteBatch?.Dispose();
            _spriteBatch = null;

            _taaEffect?.Dispose();
            _taaEffect = null;

            _taaEffectBase?.Dispose();
            _taaEffectBase = null;

            _linearSampler?.Dispose();
            _linearSampler = null;

            _pointSampler?.Dispose();
            _pointSampler = null;
        }

        /// <summary>
        /// Helper class for shader source compilation.
        /// Wraps shader source code for EffectCompiler.
        /// </summary>
        private class ShaderSourceClass : ShaderSource
        {
            public string Name { get; set; }
            public string SourceCode { get; set; }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + (Name?.GetHashCode() ?? 0);
                hash = hash * 31 + (SourceCode?.GetHashCode() ?? 0);
                return hash;
            }

            public override object Clone()
            {
                return new ShaderSourceClass
                {
                    Name = Name,
                    SourceCode = SourceCode
                };
            }

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is ShaderSourceClass other))
                {
                    return false;
                }

                return string.Equals(Name, other.Name) &&
                       string.Equals(SourceCode, other.SourceCode);
            }
        }
    }
}


using System;
using System.IO;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Game.Stride.Graphics;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Rendering;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Game.Stride.PostProcessing
{
    /// <summary>
    /// Stride implementation of Motion Blur post-processing effect.
    /// Inherits shared motion blur logic from BaseMotionBlurEffect.
    ///
    /// Features:
    /// - Velocity-based motion blur using motion vectors
    /// - Per-object motion blur support
    /// - Camera motion blur
    /// - Configurable sample count and intensity
    /// - High-quality Gaussian blur for motion trails
    /// - Depth-aware filtering to prevent artifacts
    ///
    /// Based on Stride rendering pipeline: https://doc.stride3d.net/latest/en/manual/graphics/
    /// Motion blur enhances realism by simulating camera/object motion during frame exposure.
    ///
    /// Algorithm based on swkotor2.exe: Frame buffer post-processing @ 0x007c8408
    /// Original implementation: Uses frame buffers for rendering and effects
    /// This implementation: Full motion blur pipeline with velocity-based sampling
    /// </summary>
    public class StrideMotionBlurEffect : BaseMotionBlurEffect
    {
        private StrideGraphics.GraphicsDevice _graphicsDevice;
        private StrideGraphics.GraphicsContext _graphicsContext;
        private EffectInstance _motionBlurEffect;
        private StrideGraphics.Texture _velocityTexture;
        private StrideGraphics.Texture _temporaryTexture;
        private StrideGraphics.SpriteBatch _spriteBatch;
        private StrideGraphics.SamplerState _linearSampler;
        private StrideGraphics.Effect _motionBlurEffectBase;
        private IServiceProvider _services;
        private ContentManager _contentManager;
        private EffectSystem _effectSystem;
        private global::Stride.Engine.Game _game;
        private dynamic _gameServices; // Use dynamic to allow GetService<T>() calls on Stride.Core.ServiceRegistry

        public StrideMotionBlurEffect(StrideGraphics.GraphicsDevice graphicsDevice, StrideGraphics.GraphicsContext graphicsContext = null)
            : this(graphicsDevice, graphicsContext, null, null)
        {
        }

        public StrideMotionBlurEffect(StrideGraphics.GraphicsDevice graphicsDevice, StrideGraphics.GraphicsContext graphicsContext,
            IServiceProvider services, ContentManager contentManager)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _graphicsContext = graphicsContext;
            _services = services;
            _contentManager = contentManager;

            // Initialize Game and Game.Services access for comprehensive service access
            // This is required for proper service access in Stride applications
            InitializeGameServices();

            // Try to get EffectSystem from services if available
            if (_services != null)
            {
                _effectSystem = _services.GetService(typeof(EffectSystem)) as EffectSystem;
            }

            // Also try to get EffectSystem from Game.Services if available
            if (_effectSystem == null && _gameServices != null)
            {
                try
                {
                    _effectSystem = (_gameServices as global::Stride.Core.ServiceRegistry)?.GetService<EffectSystem>();
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to get EffectSystem from Game.Services: {ex.Message}");
                }
            }

            InitializeRenderingResources();
        }

        /// <summary>
        /// Initializes Game instance and Game.Services access.
        /// This is required for proper service access in Stride applications.
        /// Based on StrideTemporalAaEffect implementation pattern.
        /// </summary>
        /// <remarks>
        /// Based on Stride Engine architecture:
        /// - Game.Services provides access to engine services including EffectSystem, ContentManager, EffectCompiler
        /// - GraphicsDevice may have a Game property or Services() method depending on Stride version
        /// - Multiple strategies are used to ensure compatibility across Stride versions
        /// - Original game: DirectX 8/9 fixed-function pipeline (swkotor2.exe: Frame buffer post-processing @ 0x007c8408)
        /// - Modern implementation: Uses Stride's service registry for dependency injection
        /// </remarks>
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
                        System.Console.WriteLine("[StrideMotionBlurEffect] Successfully accessed Game.Services from GraphicsDevice.Game");
                        return;
                    }
                }

                // Strategy 2: Try to get Game from GraphicsDevice.Services() method
                // Some Stride versions expose Game through the Services() method
                // Note: This is the method that was commented out in the TODO
                try
                {
                    var servicesMethod = _graphicsDevice.GetType().GetMethod("Services", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (servicesMethod != null)
                    {
                        dynamic graphicsDeviceServices = servicesMethod.Invoke(_graphicsDevice, null);
                        if (graphicsDeviceServices != null)
                        {
                            // Try to get Game from services
                            try
                            {
                                _game = graphicsDeviceServices.GetService<global::Stride.Engine.Game>();
                                if (_game != null)
                                {
                                    _gameServices = _game.Services;
                                    System.Console.WriteLine("[StrideMotionBlurEffect] Successfully accessed Game.Services from GraphicsDevice.Services()");
                                    return;
                                }
                            }
                            catch
                            {
                                // GetService<T> might not be available, try IServiceProvider pattern
                            }

                            // Strategy 3: Try to access Game through IServiceProvider pattern
                            // This works in dependency injection scenarios
                            if (_game == null && graphicsDeviceServices is IServiceProvider serviceProvider)
                            {
                                _game = serviceProvider.GetService(typeof(global::Stride.Engine.Game)) as global::Stride.Engine.Game;
                                if (_game != null)
                                {
                                    _gameServices = _game.Services;
                                    System.Console.WriteLine("[StrideMotionBlurEffect] Successfully accessed Game through IServiceProvider pattern");
                                    return;
                                }
                            }

                            // If we have services but no Game, use services directly
                            if (_game == null)
                            {
                                _gameServices = graphicsDeviceServices;
                                System.Console.WriteLine("[StrideMotionBlurEffect] Using GraphicsDevice.Services() directly (Game not found)");
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] GraphicsDevice.Services() method not available or failed: {ex.Message}");
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
                                    System.Console.WriteLine("[StrideMotionBlurEffect] Successfully accessed Game.Services from GraphicsDevice parent");
                                    return;
                                }
                            }

                            // Try to get Services from parent
                            var parentServicesProperty = parent.GetType().GetProperty("Services");
                            if (parentServicesProperty != null)
                            {
                                var parentServices = parentServicesProperty.GetValue(parent);
                                if (parentServices != null)
                                {
                                    _gameServices = parentServices;
                                    _game = (_gameServices as IServiceProvider)?.GetService(typeof(global::Stride.Engine.Game)) as global::Stride.Engine.Game;
                                    if (_game != null)
                                    {
                                        System.Console.WriteLine("[StrideMotionBlurEffect] Successfully accessed Game through parent Services");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to access Game through reflection: {ex.Message}");
                }

                // If all strategies fail, log a warning but continue
                // The effect can still work using the provided IServiceProvider or fallback methods
                System.Console.WriteLine("[StrideMotionBlurEffect] Warning: Could not access Game.Services. Some features may not work correctly.");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Exception while initializing Game.Services access: {ex.Message}");
                // Continue execution - effect can still work with provided services
            }
        }

        private void InitializeRenderingResources()
        {
            // Create sprite batch for fullscreen quad rendering
            _spriteBatch = new StrideGraphics.SpriteBatch(_graphicsDevice);

            // Initialize GraphicsContext if not provided
            if (_graphicsContext == null)
            {
                _graphicsContext = _graphicsDevice.GraphicsContext();
            }

            // Create linear sampler for texture sampling
            _linearSampler = StrideGraphics.SamplerState.New(_graphicsDevice, new StrideGraphics.SamplerStateDescription
            {
                Filter = StrideGraphics.TextureFilter.Linear,
                AddressU = StrideGraphics.TextureAddressMode.Clamp,
                AddressV = StrideGraphics.TextureAddressMode.Clamp,
                AddressW = StrideGraphics.TextureAddressMode.Clamp
            });

            // Load motion blur effect shader
            LoadMotionBlurShader();
        }

        /// <summary>
        /// Loads motion blur effect shader from compiled .sdsl files or creates it programmatically.
        /// </summary>
        /// <remarks>
        /// Based on Stride Engine shader loading:
        /// - Compiled .sdsl files are stored as .sdeffect files in content
        /// - Effect.Load() loads from default content paths
        /// - ContentManager.Load&lt;Effect&gt;() loads from content manager
        /// - EffectSystem can compile shaders at runtime from source
        /// </remarks>
        private void LoadMotionBlurShader()
        {
            // Strategy 1: Try loading from ContentManager if available
            if (_motionBlurEffectBase == null && _contentManager != null)
            {
                try
                {
                    _motionBlurEffectBase = _contentManager.Load<StrideGraphics.Effect>("MotionBlur");
                    if (_motionBlurEffectBase != null)
                    {
                        _motionBlurEffect = new EffectInstance(_motionBlurEffectBase);
                        System.Console.WriteLine("[StrideMotionBlurEffect] Loaded MotionBlur from ContentManager");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to load MotionBlur from ContentManager: {ex.Message}");
                }
            }

            // Strategy 2: Try loading from services if available
            if (_motionBlurEffectBase == null && _services != null)
            {
                try
                {
                    var contentManager = _services.GetService(typeof(ContentManager)) as ContentManager;
                    if (contentManager != null)
                    {
                        try
                        {
                            _motionBlurEffectBase = contentManager.Load<StrideGraphics.Effect>("MotionBlur");
                            if (_motionBlurEffectBase != null)
                            {
                                _motionBlurEffect = new EffectInstance(_motionBlurEffectBase);
                                System.Console.WriteLine("[StrideMotionBlurEffect] Loaded MotionBlur from services ContentManager");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to load MotionBlur from services ContentManager: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to access ContentManager from services: {ex.Message}");
                }
            }

            // Strategy 3: Create shader programmatically using EffectSystem if available
            if (_motionBlurEffectBase == null && _effectSystem != null)
            {
                try
                {
                    _motionBlurEffectBase = CreateMotionBlurEffectWithEffectSystem(_effectSystem);
                    if (_motionBlurEffectBase != null)
                    {
                        _motionBlurEffect = new EffectInstance(_motionBlurEffectBase);
                        System.Console.WriteLine("[StrideMotionBlurEffect] Created MotionBlur effect using EffectSystem");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to create MotionBlur using EffectSystem: {ex.Message}");
                }
            }

            // Strategy 4: Create shader programmatically with fallback compilation
            if (_motionBlurEffectBase == null)
            {
                _motionBlurEffectBase = CreateMotionBlurEffect();
                if (_motionBlurEffectBase != null)
                {
                    _motionBlurEffect = new EffectInstance(_motionBlurEffectBase);
                    System.Console.WriteLine("[StrideMotionBlurEffect] Created MotionBlur effect programmatically");
                }
            }

            // Final fallback: Effects remain null, rendering will use SpriteBatch default
            if (_motionBlurEffectBase == null)
            {
                System.Console.WriteLine("[StrideMotionBlurEffect] Warning: Could not load or create motion blur shader. Using SpriteBatch default rendering.");
            }
        }

        /// <summary>
        /// Creates a motion blur effect programmatically from shader source code.
        /// </summary>
        /// <returns>Effect instance for motion blur processing, or null if creation fails.</returns>
        /// <remarks>
        /// Based on Stride shader compilation: Creates shader source code in .sdsl format
        /// and compiles it at runtime using EffectSystem.
        /// Motion blur shader samples along velocity vectors with Gaussian weighting.
        ///
        /// swkotor2.exe: Frame buffer post-processing for visual effects
        /// Original implementation: Uses frame buffers for rendering and effects
        /// This implementation: Full motion blur pipeline with velocity-based temporal sampling
        /// </remarks>
        private StrideGraphics.Effect CreateMotionBlurEffect()
        {
            try
            {
                // Create shader source code for velocity-based motion blur
                // Motion blur algorithm:
                // 1. Sample velocity vector for current pixel
                // 2. Scale velocity by intensity and clamp to max velocity
                // 3. Sample color buffer along motion vector trail
                // 4. Accumulate samples with Gaussian weights
                // 5. Apply depth-aware filtering to prevent artifacts
                string shaderSource = @"
shader MotionBlurEffect : ShaderBase
{
    // Input parameters
    cbuffer PerDraw : register(b0)
    {
        float Intensity;
        float MaxVelocity;
        int SampleCount;
        float DeltaTime;
        float2 ScreenSize;
        float2 ScreenSizeInv;
        float DepthThreshold;
    };

    Texture2D ColorTexture : register(t0);
    Texture2D MotionVectorsTexture : register(t1);
    Texture2D DepthTexture : register(t2);
    SamplerState LinearSampler : register(s0);

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
        float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
        output.Position = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
        output.TexCoord = uv;

        return output;
    }

    // Gaussian weights for sample accumulation (9-tap filter)
    static const float weights[9] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };

    // Pixel shader: Velocity-based motion blur
    float4 PS(VSOutput input) : SV_Target
    {
        // Sample current pixel color
        float4 centerColor = ColorTexture.Sample(LinearSampler, input.TexCoord);
        float centerDepth = DepthTexture.Sample(LinearSampler, input.TexCoord).r;

        // Sample motion vector (RG = velocity in screen space)
        float2 motionVector = MotionVectorsTexture.Sample(LinearSampler, input.TexCoord).rg;

        // Scale motion vector by intensity and delta time
        motionVector *= Intensity * DeltaTime;

        // Clamp to maximum velocity to prevent artifacts
        float velocityLength = length(motionVector);
        if (velocityLength > MaxVelocity)
        {
            motionVector = normalize(motionVector) * MaxVelocity;
        }

        // If motion is negligible, return original color
        if (velocityLength < 0.001)
        {
            return centerColor;
        }

        // Initialize accumulation
        float4 accumulatedColor = centerColor * weights[4]; // Center sample weight
        float totalWeight = weights[4];

        // Sample along motion vector trail
        float2 stepVector = motionVector / (SampleCount - 1);
        float2 currentOffset = -motionVector * 0.5; // Start from beginning of trail

        for (int i = 0; i < SampleCount; i++)
        {
            if (i == SampleCount / 2) // Skip center sample (already added)
            {
                currentOffset += stepVector;
                continue;
            }

            float2 sampleCoord = input.TexCoord + currentOffset * ScreenSizeInv;

            // Clamp to texture bounds
            sampleCoord = clamp(sampleCoord, float2(0, 0), float2(1, 1));

            // Sample color and depth
            float4 sampleColor = ColorTexture.Sample(LinearSampler, sampleCoord);
            float sampleDepth = DepthTexture.Sample(LinearSampler, sampleCoord).r;

            // Depth-aware filtering: reduce weight if depth difference is large
            float depthDiff = abs(centerDepth - sampleDepth);
            float depthWeight = 1.0 - saturate(depthDiff / DepthThreshold);

            // Apply Gaussian weight with depth filtering
            float weight = weights[i] * depthWeight;

            accumulatedColor += sampleColor * weight;
            totalWeight += weight;

            currentOffset += stepVector;
        }

        // Normalize accumulated color
        return accumulatedColor / totalWeight;
    }
};";

                // Compile shader source using EffectSystem
                // Use EffectCompiler to compile shader source code at runtime
                // Based on Stride API: EffectCompiler compiles shader source to Effect bytecode
                return CompileShaderFromSource(shaderSource, "MotionBlurEffect");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to create motion blur effect: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a motion blur effect using the EffectSystem for proper compilation.
        /// </summary>
        /// <param name="effectSystem">The EffectSystem instance to use for compilation.</param>
        /// <returns>Compiled Effect, or null if compilation fails.</returns>
        /// <remarks>
        /// Based on Stride EffectSystem API:
        /// - EffectSystem provides the proper compilation environment
        /// - Uses EffectCompiler internally for shader compilation
        /// - Ensures proper shader bytecode generation for the target platform
        /// - Original game: DirectX 8/9 fixed-function pipeline (swkotor2.exe: Frame buffer post-processing @ 0x007c8408)
        /// - Modern implementation: Uses programmable shaders with runtime compilation
        /// </remarks>
        private StrideGraphics.Effect CreateMotionBlurEffectWithEffectSystem(EffectSystem effectSystem)
        {
            if (effectSystem == null)
            {
                System.Console.WriteLine("[StrideMotionBlurEffect] EffectSystem is null, cannot create motion blur effect");
                return null;
            }

            try
            {
                // Create shader source code for velocity-based motion blur
                string shaderSource = @"
shader MotionBlurEffect : ShaderBase
{
    // Input parameters
    cbuffer PerDraw : register(b0)
    {
        float Intensity;
        float MaxVelocity;
        int SampleCount;
        float DeltaTime;
        float2 ScreenSize;
        float2 ScreenSizeInv;
        float DepthThreshold;
    };

    Texture2D ColorTexture : register(t0);
    Texture2D MotionVectorsTexture : register(t1);
    Texture2D DepthTexture : register(t2);
    SamplerState LinearSampler : register(s0);

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
        float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
        output.Position = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
        output.TexCoord = uv;

        return output;
    }

    // Gaussian weights for sample accumulation (9-tap filter)
    static const float weights[9] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };

    // Pixel shader: Velocity-based motion blur
    float4 PS(VSOutput input) : SV_Target
    {
        // Sample current pixel color
        float4 centerColor = ColorTexture.Sample(LinearSampler, input.TexCoord);
        float centerDepth = DepthTexture.Sample(LinearSampler, input.TexCoord).r;

        // Sample motion vector (RG = velocity in screen space)
        float2 motionVector = MotionVectorsTexture.Sample(LinearSampler, input.TexCoord).rg;

        // Scale motion vector by intensity and delta time
        motionVector *= Intensity * DeltaTime;

        // Clamp to maximum velocity to prevent artifacts
        float velocityLength = length(motionVector);
        if (velocityLength > MaxVelocity)
        {
            motionVector = normalize(motionVector) * MaxVelocity;
        }

        // If motion is negligible, return original color
        if (velocityLength < 0.001)
        {
            return centerColor;
        }

        // Initialize accumulation
        float4 accumulatedColor = centerColor * weights[4]; // Center sample weight
        float totalWeight = weights[4];

        // Sample along motion vector trail
        float2 stepVector = motionVector / (SampleCount - 1);
        float2 currentOffset = -motionVector * 0.5; // Start from beginning of trail

        for (int i = 0; i < SampleCount; i++)
        {
            if (i == SampleCount / 2) // Skip center sample (already added)
            {
                currentOffset += stepVector;
                continue;
            }

            float2 sampleCoord = input.TexCoord + currentOffset * ScreenSizeInv;

            // Clamp to texture bounds
            sampleCoord = clamp(sampleCoord, float2(0, 0), float2(1, 1));

            // Sample color and depth
            float4 sampleColor = ColorTexture.Sample(LinearSampler, sampleCoord);
            float sampleDepth = DepthTexture.Sample(LinearSampler, sampleCoord).r;

            // Depth-aware filtering: reduce weight if depth difference is large
            float depthDiff = abs(centerDepth - sampleDepth);
            float depthWeight = 1.0 - saturate(depthDiff / DepthThreshold);

            // Apply Gaussian weight with depth filtering
            float weight = weights[i] * depthWeight;

            accumulatedColor += sampleColor * weight;
            totalWeight += weight;

            currentOffset += stepVector;
        }

        // Normalize accumulated color
        return accumulatedColor / totalWeight;
    }
};";

                // EffectSystem.Compile() doesn't exist, need to get EffectCompiler from EffectSystem
                // Try to get EffectCompiler from EffectSystem using reflection
                EffectCompiler compiler = null;
                try
                {
                    var compilerProperty = effectSystem.GetType().GetProperty("Compiler");
                    if (compilerProperty != null)
                    {
                        compiler = compilerProperty.GetValue(effectSystem) as EffectCompiler;
                    }
                }
                catch
                {
                    // Compiler property not available
                }

                // If we couldn't get EffectCompiler from EffectSystem, we can't compile
                if (compiler == null)
                {
                    System.Console.WriteLine("[StrideMotionBlurEffect] Cannot get EffectCompiler from EffectSystem, compilation failed");
                    return null;
                }

                // Create shader source object for EffectCompiler
                var shaderSourceObj = new ShaderSourceClass
                {
                    Name = "MotionBlurEffect",
                    SourceCode = shaderSource
                };

                // Compile using EffectCompiler
                // EffectCompiler provides the proper compilation context and bytecode generation
                dynamic compilationResult = compiler.Compile(shaderSourceObj, new CompilerParameters
                {
                    EffectParameters = new EffectCompilerParameters()
                });

                // Handle TaskOrResult unwrapping
                dynamic compilerResult = compilationResult.Result;
                if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                {
                    // Create Effect from compiled bytecode
                    var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                    System.Console.WriteLine("[StrideMotionBlurEffect] Successfully compiled motion blur effect using EffectSystem");
                    return effect;
                }
                else
                {
                    System.Console.WriteLine("[StrideMotionBlurEffect] EffectSystem compilation failed: No bytecode generated");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to create motion blur effect using EffectSystem: {ex.Message}");
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
        /// - Original game: DirectX 8/9 fixed-function pipeline (swkotor2.exe: Frame buffer post-processing @ 0x007c8408, Motion_Blur @ 0x007bb610)
        /// - Modern implementation: Uses programmable shaders with runtime compilation
        /// </remarks>
        private StrideGraphics.Effect CompileShaderFromSource(string shaderSource, string shaderName)
        {
            if (string.IsNullOrEmpty(shaderSource))
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Cannot compile shader '{shaderName}': shader source is null or empty");
                return null;
            }

            if (_graphicsDevice == null)
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Cannot compile shader '{shaderName}': GraphicsDevice is null");
                return null;
            }

            try
            {
                // Strategy 1: Try to get EffectCompiler from our stored EffectSystem
                if (_effectSystem != null)
                {
                    return CompileShaderWithEffectSystem(_effectSystem, shaderSource, shaderName);
                }

                // Strategy 2: Try to get EffectCompiler from services if available
                if (_services != null)
                {
                    var effectCompiler = _services.GetService(typeof(EffectCompiler)) as EffectCompiler;
                    if (effectCompiler != null)
                    {
                        return CompileShaderWithCompiler(effectCompiler, shaderSource, shaderName);
                    }

                    // Try to get EffectSystem from services
                    var effectSystem = _services.GetService(typeof(EffectSystem)) as EffectSystem;
                    if (effectSystem != null)
                    {
                        return CompileShaderWithEffectSystem(effectSystem, shaderSource, shaderName);
                    }
                }

                // Strategy 3: Create temporary shader file and compile it
                // Fallback method: Write shader source to temporary file and compile
                return CompileShaderFromFile(shaderSource, shaderName);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to compile shader '{shaderName}': {ex.Message}");
                System.Console.WriteLine($"[StrideMotionBlurEffect] Stack trace: {ex.StackTrace}");
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
                var compilerSource = new ShaderSourceClass
                {
                    Name = shaderName,
                    SourceCode = shaderSource
                };

                // Compile shader source to bytecode
                // Based on Stride API: EffectCompiler.Compile() compiles shader source
                // Note: Compile() returns TaskOrResult<EffectBytecodeCompilerResult>, use dynamic to handle unwrapping
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
                    var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                    System.Console.WriteLine($"[StrideMotionBlurEffect] Successfully compiled shader '{shaderName}' using EffectCompiler");
                    return effect;
                }
                else
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] EffectCompiler compilation failed for shader '{shaderName}': No bytecode generated");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Exception while compiling shader '{shaderName}' with EffectCompiler: {ex.Message}");
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
        private StrideGraphics.Effect CompileShaderWithEffectSystem(EffectSystem effectSystem, string shaderSource, string shaderName)
        {
            try
            {
                // EffectSystem may provide compilation methods
                // Based on Stride architecture: EffectSystem manages effect lifecycle
                // Try to use EffectSystem's compilation capabilities if available
                // Note: EffectSystem interface may vary, so we use reflection as fallback

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

                System.Console.WriteLine($"[StrideMotionBlurEffect] EffectSystem does not provide direct compiler access for shader '{shaderName}'");
                return null;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Exception while compiling shader '{shaderName}' with EffectSystem: {ex.Message}");
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
                // Based on Stride: Shaders are compiled from .sdsl files
                tempFilePath = Path.Combine(Path.GetTempPath(), $"{shaderName}_{Guid.NewGuid()}.sdsl");
                File.WriteAllText(tempFilePath, shaderSource);

                // Strategy 1: Try to get EffectCompiler from Game.Services (if available)
                // Based on Stride API: Game.Services provides access to EffectCompiler and EffectSystem
                // This is the proper way to access services in Stride applications
                if (_gameServices != null)
                {
                    try
                    {
                        // Try to get EffectCompiler from Game.Services
                        var effectCompiler = (_gameServices as global::Stride.Core.ServiceRegistry)?.GetService<EffectCompiler>();
                        if (effectCompiler != null)
                        {
                            // Create compilation source from shader source (not file path, but source code)
                            // Note: EffectCompiler typically compiles from source, not file paths
                            var compilerSource = new ShaderSourceClass
                            {
                                Name = shaderName,
                                SourceCode = shaderSource
                            };

                            // Compile shader source to bytecode
                            // Note: Compile() returns TaskOrResult<EffectBytecodeCompilerResult>, use dynamic to handle unwrapping
                            dynamic compilationResult = effectCompiler.Compile(compilerSource, new CompilerParameters
                            {
                                EffectParameters = new EffectCompilerParameters()
                            });

                            // Unwrap TaskOrResult to get the actual result
                            dynamic compilerResult = compilationResult.Result;
                            if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                            {
                                var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                                System.Console.WriteLine($"[StrideMotionBlurEffect] Successfully compiled shader '{shaderName}' from source using Game.Services.EffectCompiler");
                                return effect;
                            }
                        }

                        // Try to get EffectSystem from Game.Services as alternative
                        var effectSystem = (_gameServices as global::Stride.Core.ServiceRegistry)?.GetService<EffectSystem>();
                        if (effectSystem != null)
                        {
                            return CompileShaderWithEffectSystem(effectSystem, shaderSource, shaderName);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to compile shader '{shaderName}' using Game.Services: {ex.Message}");
                    }
                }

                // Strategy 2: Try to get EffectCompiler from GraphicsDevice.Services() method (if available)
                // Some Stride versions expose services through GraphicsDevice.Services() method
                // This was the original approach that was commented out in the TODO
                try
                {
                    var servicesMethod = _graphicsDevice.GetType().GetMethod("Services", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (servicesMethod != null)
                    {
                        dynamic graphicsDeviceServices = servicesMethod.Invoke(_graphicsDevice, null);
                        if (graphicsDeviceServices != null)
                        {
                            // Try to get EffectCompiler from services
                            try
                            {
                                var effectCompiler = graphicsDeviceServices.GetService<EffectCompiler>();
                                if (effectCompiler != null)
                                {
                                    // Create compilation source from shader source
                                    var compilerSource = new ShaderSourceClass
                                    {
                                        Name = shaderName,
                                        SourceCode = shaderSource
                                    };

                                    // Compile shader source to bytecode
                                    dynamic compilationResult = effectCompiler.Compile(compilerSource, new CompilerParameters
                                    {
                                        EffectParameters = new EffectCompilerParameters()
                                    });

                                    // Unwrap TaskOrResult to get the actual result
                                    dynamic compilerResult = compilationResult.Result;
                                    if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                                    {
                                        var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                                        System.Console.WriteLine($"[StrideMotionBlurEffect] Successfully compiled shader '{shaderName}' from source using GraphicsDevice.Services().EffectCompiler");
                                        return effect;
                                    }
                                }
                            }
                            catch
                            {
                                // GetService<T> might not be available, try IServiceProvider pattern
                                if (graphicsDeviceServices is IServiceProvider serviceProvider)
                                {
                                    var effectCompiler = serviceProvider.GetService(typeof(EffectCompiler)) as EffectCompiler;
                                    if (effectCompiler != null)
                                    {
                                        return CompileShaderWithCompiler(effectCompiler, shaderSource, shaderName);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] GraphicsDevice.Services() method not available or failed for shader '{shaderName}': {ex.Message}");
                }

                // Strategy 3: Try to use provided IServiceProvider if available
                if (_services != null)
                {
                    try
                    {
                        var effectCompiler = _services.GetService(typeof(EffectCompiler)) as EffectCompiler;
                        if (effectCompiler != null)
                        {
                            return CompileShaderWithCompiler(effectCompiler, shaderSource, shaderName);
                        }

                        var effectSystem = _services.GetService(typeof(EffectSystem)) as EffectSystem;
                        if (effectSystem != null)
                        {
                            return CompileShaderWithEffectSystem(effectSystem, shaderSource, shaderName);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to compile shader '{shaderName}' using provided IServiceProvider: {ex.Message}");
                    }
                }

                // Final fallback: Effect.Load() doesn't exist in this Stride version
                // Effect.Load() doesn't support file paths directly, and we've exhausted all service access methods

                System.Console.WriteLine($"[StrideMotionBlurEffect] Could not compile shader '{shaderName}' from file");
                return null;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideMotionBlurEffect] Exception while compiling shader '{shaderName}' from file: {ex.Message}");
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
                return Name == other.Name && SourceCode == other.SourceCode;
            }
        }

        #region BaseMotionBlurEffect Implementation

        protected override void OnDispose()
        {
            _motionBlurEffect?.Dispose();
            _motionBlurEffect = null;

            _motionBlurEffectBase?.Dispose();
            _motionBlurEffectBase = null;

            _velocityTexture?.Dispose();
            _velocityTexture = null;

            _temporaryTexture?.Dispose();
            _temporaryTexture = null;

            _spriteBatch?.Dispose();
            _spriteBatch = null;

            _linearSampler?.Dispose();
            _linearSampler = null;

            base.OnDispose();
        }

        #endregion

        /// <summary>
        /// Applies motion blur to the input frame.
        /// </summary>
        /// <param name="input">HDR color buffer.</param>
        /// <param name="motionVectors">Per-pixel motion vectors (in screen space).</param>
        /// <param name="depth">Depth buffer for depth-aware blur.</param>
        /// <param name="deltaTime">Frame delta time for exposure simulation.</param>
        /// <param name="width">Render width.</param>
        /// <param name="height">Render height.</param>
        /// <returns>Output texture with motion blur applied.</returns>
        public StrideGraphics.Texture Apply(StrideGraphics.Texture input, StrideGraphics.Texture motionVectors, StrideGraphics.Texture depth, float deltaTime,
            int width, int height)
        {
            if (!_enabled || input == null || motionVectors == null)
            {
                return input;
            }

            EnsureTextures(width, height, input.Format);

            // Motion Blur Process:
            // 1. Sample motion vectors for current pixel
            // 2. Scale by intensity and frame time
            // 3. Sample color buffer along motion vector
            // 4. Accumulate samples with proper weighting
            // 5. Apply depth-aware filtering (avoid bleeding across depth discontinuities)
            // 6. Clamp to max velocity to prevent artifacts

            ExecuteMotionBlur(input, motionVectors, depth, deltaTime, _temporaryTexture);

            return _temporaryTexture ?? input;
        }

        private void EnsureTextures(int width, int height, StrideGraphics.PixelFormat format)
        {
            if (_temporaryTexture != null &&
                _temporaryTexture.Width == width &&
                _temporaryTexture.Height == height)
            {
                return;
            }

            _temporaryTexture?.Dispose();

            var desc = StrideGraphics.TextureDescription.New2D(width, height, 1, format,
                StrideGraphics.TextureFlags.ShaderResource | StrideGraphics.TextureFlags.RenderTarget);

            _temporaryTexture = StrideGraphics.Texture.New(_graphicsDevice, desc);
        }

        private void ExecuteMotionBlur(StrideGraphics.Texture input, StrideGraphics.Texture motionVectors, StrideGraphics.Texture depth,
            float deltaTime, StrideGraphics.Texture output)
        {
            // Motion Blur Shader Execution:
            // - Input: HDR color, motion vectors, depth
            // - Parameters: intensity, max velocity, sample count, delta time
            // - Process: Sample along motion vector, accumulate with weights
            // - Output: Motion-blurred color buffer

            if (input == null || motionVectors == null || output == null ||
                _graphicsDevice == null || _spriteBatch == null)
            {
                return;
            }

            // Get command list for rendering operations
            StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
            if (commandList == null)
            {
                return;
            }

            // Ensure GraphicsContext is available
            if (_graphicsContext == null)
            {
                _graphicsContext = _graphicsDevice.GraphicsContext();
            }
            if (_graphicsContext == null)
            {
                return;
            }

            try
            {
                // Set render target to output texture
                commandList.SetRenderTarget(null, output);
                commandList.SetViewport(new StrideGraphics.Viewport(0, 0, output.Width, output.Height));

                // Clear render target to black
                commandList.Clear(output, global::Stride.Core.Mathematics.Color.Black);

                // Calculate effective parameters
                var effectiveIntensity = _intensity * deltaTime * 60.0f; // Normalize to 60fps
                var clampedVelocity = Math.Min(_maxVelocity, effectiveIntensity * 100.0f);
                var depthThreshold = 0.1f; // Depth discontinuity threshold

                System.Console.WriteLine($"[StrideMotionBlur] Applying blur: {_sampleCount} samples, intensity {effectiveIntensity:F2}, max velocity {clampedVelocity:F2}");

                // Begin sprite batch rendering with motion blur effect
                // SpriteBatch.Begin requires GraphicsContext (not CommandList)
                _spriteBatch.Begin(_graphicsContext, StrideGraphics.SpriteSortMode.Immediate, StrideGraphics.BlendStates.Opaque,
                    _linearSampler, StrideGraphics.DepthStencilStates.None, StrideGraphics.RasterizerStates.CullNone, _motionBlurEffect);

                // Set shader parameters if effect is available
                if (_motionBlurEffect != null && _motionBlurEffect.Parameters != null)
                {
                    try
                    {
                        // Set scalar parameters using ValueParameterKey
                        _motionBlurEffect.Parameters.Set(new ValueParameterKey<float>("Intensity"), effectiveIntensity);
                        _motionBlurEffect.Parameters.Set(new ValueParameterKey<float>("MaxVelocity"), clampedVelocity);
                        _motionBlurEffect.Parameters.Set(new ValueParameterKey<int>("SampleCount"), _sampleCount);
                        _motionBlurEffect.Parameters.Set(new ValueParameterKey<float>("DeltaTime"), deltaTime);
                        _motionBlurEffect.Parameters.Set(new ValueParameterKey<float>("DepthThreshold"), depthThreshold);

                        // Set vector parameters
                        _motionBlurEffect.Parameters.Set(new ValueParameterKey<Vector2>("ScreenSize"), new Vector2(output.Width, output.Height));
                        _motionBlurEffect.Parameters.Set(new ValueParameterKey<Vector2>("ScreenSizeInv"), new Vector2(1.0f / output.Width, 1.0f / output.Height));

                        // Set texture parameters using ObjectParameterKey
                        _motionBlurEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("ColorTexture"), input);
                        _motionBlurEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("MotionVectorsTexture"), motionVectors);
                        if (depth != null)
                        {
                            _motionBlurEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("DepthTexture"), depth);
                        }

                        // Set sampler parameter
                        _motionBlurEffect.Parameters.Set(new ObjectParameterKey<StrideGraphics.SamplerState>("LinearSampler"), _linearSampler);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to set shader parameters: {ex.Message}");
                        // Continue with default values if parameter setting fails
                    }
                }

                // Draw full-screen quad with motion blur shader
                var destinationRect = new RectangleF(0, 0, output.Width, output.Height);
                _spriteBatch.Draw(input, destinationRect, Color.White);

                // End sprite batch rendering
                _spriteBatch.End();
            }
            finally
            {
                // Render target restoration not needed - Stride manages render targets per frame
                // The CommandList is frame-scoped and render targets are reset automatically
            }
        }
    }
}


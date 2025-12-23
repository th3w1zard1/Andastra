using System;
using System.IO;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Stride.Graphics;

namespace Andastra.Runtime.Stride.PostProcessing
{
    /// <summary>
    /// Stride implementation of bloom post-processing effect.
    /// Inherits shared bloom logic from BaseBloomEffect.
    ///
    /// Creates a glow effect by extracting bright areas, blurring them,
    /// and adding them back to the image.
    ///
    /// Features:
    /// - Threshold-based bright pass
    /// - Multi-pass Gaussian blur
    /// - Configurable intensity
    /// - Performance optimized for Stride's rendering pipeline
    /// </summary>
    public class StrideBloomEffect : BaseBloomEffect
    {
        private GraphicsDevice _graphicsDevice;
        private Texture _brightPassTarget;
        private Texture[] _blurTargets;
        private SpriteBatch _spriteBatch;
        private SamplerState _linearSampler;
        private SamplerState _pointSampler;
        private EffectInstance _brightPassEffect;
        private EffectInstance _blurEffect;
        private Effect _brightPassEffectBase;
        private Effect _blurEffectBase;

        public StrideBloomEffect(GraphicsDevice graphicsDevice)
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

            // Load bloom effect shaders from compiled .sdsl files
            // Based on Stride Engine: Effects are loaded from compiled .sdeffect files (compiled from .sdsl source)
            // Loading order:
            // 1. Try Effect.Load() - standard Stride method for loading compiled effects
            // 2. Try ContentManager if available through GraphicsDevice services
            // 3. Fallback to programmatically created shaders if loading fails
            LoadBloomShaders();
        }

        /// <summary>
        /// Loads bloom effect shaders from compiled .sdsl files or creates them programmatically.
        /// </summary>
        /// <remarks>
        /// Based on Stride Engine shader loading:
        /// - Compiled .sdsl files are stored as .sdeffect files in content
        /// - Effect.Load() loads from default content paths
        /// - ContentManager.Load&lt;Effect&gt;() loads from content manager
        /// - EffectSystem can compile shaders at runtime from source
        /// </remarks>
        private void LoadBloomShaders()
        {
            // Strategy 1: Try loading from compiled effect files using Effect.Load()
            // Effect.Load() searches in standard content paths for compiled .sdeffect files
            try
            {
                _brightPassEffectBase = Effect.Load(_graphicsDevice, "BloomBrightPass");
                if (_brightPassEffectBase != null)
                {
                    _brightPassEffect = new EffectInstance(_brightPassEffectBase);
                    System.Console.WriteLine("[StrideBloomEffect] Loaded BloomBrightPass effect from compiled file");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Failed to load BloomBrightPass from compiled file: {ex.Message}");
            }

            try
            {
                _blurEffectBase = Effect.Load(_graphicsDevice, "BloomBlur");
                if (_blurEffectBase != null)
                {
                    _blurEffect = new EffectInstance(_blurEffectBase);
                    System.Console.WriteLine("[StrideBloomEffect] Loaded BloomBlur effect from compiled file");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Failed to load BloomBlur from compiled file: {ex.Message}");
            }

            // Strategy 2: Try loading from ContentManager if available
            // Check if GraphicsDevice has access to ContentManager through services
            if (_brightPassEffectBase == null || _blurEffectBase == null)
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
                            if (_brightPassEffectBase == null)
                            {
                                try
                                {
                                    _brightPassEffectBase = contentManager.Load<Effect>("BloomBrightPass");
                                    if (_brightPassEffectBase != null)
                                    {
                                        _brightPassEffect = new EffectInstance(_brightPassEffectBase);
                                        System.Console.WriteLine("[StrideBloomEffect] Loaded BloomBrightPass from ContentManager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"[StrideBloomEffect] Failed to load BloomBrightPass from ContentManager: {ex.Message}");
                                }
                            }

                            if (_blurEffectBase == null)
                            {
                                try
                                {
                                    _blurEffectBase = contentManager.Load<Effect>("BloomBlur");
                                    if (_blurEffectBase != null)
                                    {
                                        _blurEffect = new EffectInstance(_blurEffectBase);
                                        System.Console.WriteLine("[StrideBloomEffect] Loaded BloomBlur from ContentManager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"[StrideBloomEffect] Failed to load BloomBlur from ContentManager: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideBloomEffect] Failed to access ContentManager: {ex.Message}");
                }
            }

            // Strategy 3: Create shaders programmatically if loading failed
            // This provides a functional fallback that works without pre-compiled shader files
            if (_brightPassEffectBase == null)
            {
                _brightPassEffectBase = CreateBrightPassEffect();
                if (_brightPassEffectBase != null)
                {
                    _brightPassEffect = new EffectInstance(_brightPassEffectBase);
                    System.Console.WriteLine("[StrideBloomEffect] Created BloomBrightPass effect programmatically");
                }
            }

            if (_blurEffectBase == null)
            {
                _blurEffectBase = CreateBlurEffect();
                if (_blurEffectBase != null)
                {
                    _blurEffect = new EffectInstance(_blurEffectBase);
                    System.Console.WriteLine("[StrideBloomEffect] Created BloomBlur effect programmatically");
                }
            }

            // Final fallback: If all loading methods failed, effects remain null
            // The rendering code will use SpriteBatch's default rendering (no custom shaders)
            if (_brightPassEffectBase == null && _blurEffectBase == null)
            {
                System.Console.WriteLine("[StrideBloomEffect] Warning: Could not load or create bloom shaders. Using SpriteBatch default rendering.");
            }
        }

        /// <summary>
        /// Creates a bright pass effect programmatically from shader source code.
        /// </summary>
        /// <returns>Effect instance for bright pass extraction, or null if creation fails.</returns>
        /// <remarks>
        /// Based on Stride shader compilation: Creates shader source code in .sdsl format
        /// and compiles it at runtime using EffectSystem.
        /// Bright pass shader extracts pixels above threshold for bloom effect.
        /// </remarks>
        private Effect CreateBrightPassEffect()
        {
            try
            {
                // Create shader source code for bright pass extraction
                // Bright pass: extracts pixels above threshold (typically 1.0 for HDR)
                // Pixels below threshold are set to black
                string shaderSource = @"
shader BrightPassEffect : ShaderBase
{
    // Input parameters
    cbuffer PerDraw : register(b0)
    {
        float Threshold;
        float2 ScreenSize;
        float2 ScreenSizeInv;
    };

    Texture2D SourceTexture : register(t0);
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

    // Pixel shader: Bright pass extraction
    float4 PS(VSOutput input) : SV_Target
    {
        float4 color = SourceTexture.Sample(LinearSampler, input.TexCoord);

        // Extract bright areas: keep pixels above threshold, set others to black
        float brightness = dot(color.rgb, float3(0.299, 0.587, 0.114)); // Luminance calculation
        if (brightness > Threshold)
        {
            return color;
        }
        else
        {
            return float4(0, 0, 0, color.a);
        }
    }
};";

                // Compile shader source using EffectCompiler
                // Based on Stride API: EffectCompiler.Compile() compiles shader source to Effect
                return CompileShaderFromSource(shaderSource, "BrightPassEffect");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Failed to create bright pass effect: {ex.Message}");
                System.Console.WriteLine($"[StrideBloomEffect] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Creates a blur effect programmatically from shader source code.
        /// </summary>
        /// <returns>Effect instance for Gaussian blur, or null if creation fails.</returns>
        /// <remarks>
        /// Based on Stride shader compilation: Creates shader source code in .sdsl format
        /// and compiles it at runtime using EffectSystem.
        /// Blur shader applies separable Gaussian blur in horizontal or vertical direction.
        /// </remarks>
        private Effect CreateBlurEffect()
        {
            try
            {
                // Create shader source code for separable Gaussian blur
                // Blur can be applied horizontally or vertically based on BlurDirection parameter
                string shaderSource = @"
shader BlurEffect : ShaderBase
{
    // Input parameters
    cbuffer PerDraw : register(b0)
    {
        float2 BlurDirection;
        float BlurRadius;
        float2 ScreenSize;
        float2 ScreenSizeInv;
    };

    Texture2D SourceTexture : register(t0);
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

    // Gaussian blur weights (9-tap filter)
    static const float weights[9] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };

    // Pixel shader: Separable Gaussian blur
    float4 PS(VSOutput input) : SV_Target
    {
        float4 color = float4(0, 0, 0, 0);
        float2 texelSize = ScreenSizeInv;
        float2 offset = BlurDirection * texelSize * BlurRadius;

        // Apply 9-tap Gaussian blur
        for (int i = 0; i < 9; i++)
        {
            float2 sampleCoord = input.TexCoord + offset * (float(i) - 4.0);
            color += SourceTexture.Sample(LinearSampler, sampleCoord) * weights[i];
        }

        return color;
    }
};";

                // Compile shader source using EffectCompiler
                // Based on Stride API: EffectCompiler.Compile() compiles shader source to Effect
                return CompileShaderFromSource(shaderSource, "BlurEffect");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Failed to create blur effect: {ex.Message}");
                System.Console.WriteLine($"[StrideBloomEffect] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Applies bloom effect to the input texture.
        /// </summary>
        public Texture Apply(Texture hdrInput, RenderContext context)
        {
            if (!_enabled || hdrInput == null) return hdrInput;

            EnsureRenderTargets(hdrInput.Width, hdrInput.Height, hdrInput.Format);

            // Step 1: Bright pass extraction
            ExtractBrightAreas(hdrInput, _brightPassTarget, context);

            // Step 2: Multi-pass blur
            Texture blurSource = _brightPassTarget;
            for (int i = 0; i < _blurPasses; i++)
            {
                ApplyGaussianBlur(blurSource, _blurTargets[i], i % 2 == 0, context);
                blurSource = _blurTargets[i];
            }

            // Step 3: Return blurred result (compositing done in final pass)
            return _blurTargets[_blurPasses - 1] ?? hdrInput;
        }

        private void EnsureRenderTargets(int width, int height, PixelFormat format)
        {
            bool needsRecreate = _brightPassTarget == null ||
                                 _brightPassTarget.Width != width ||
                                 _brightPassTarget.Height != height;

            if (!needsRecreate && _blurTargets != null && _blurTargets.Length == _blurPasses)
            {
                return;
            }

            // Dispose existing targets
            _brightPassTarget?.Dispose();
            if (_blurTargets != null)
            {
                foreach (var target in _blurTargets)
                {
                    target?.Dispose();
                }
            }

            // Create bright pass target
            _brightPassTarget = Texture.New2D(_graphicsDevice, width, height,
                format, TextureFlags.RenderTarget | TextureFlags.ShaderResource);

            // Create blur targets (at progressively lower resolutions for performance)
            _blurTargets = new Texture[_blurPasses];
            int blurWidth = width / 2;
            int blurHeight = height / 2;

            for (int i = 0; i < _blurPasses; i++)
            {
                _blurTargets[i] = Texture.New2D(_graphicsDevice,
                    Math.Max(1, blurWidth),
                    Math.Max(1, blurHeight),
                    format,
                    TextureFlags.RenderTarget | TextureFlags.ShaderResource);

                blurWidth /= 2;
                blurHeight /= 2;
            }

            _initialized = true;
        }

        private void ExtractBrightAreas(Texture source, Texture destination, RenderContext context)
        {
            // Apply threshold-based bright pass shader
            // Pixels above threshold are kept, others are set to black
            // threshold is typically 1.0 for HDR content

            if (source == null || destination == null || _graphicsDevice == null || _spriteBatch == null)
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

            // Clear render target to black using GraphicsDevice
            // Note: In Stride, clearing is typically done through GraphicsDevice after setting render target
            _graphicsDevice.Clear(destination, Color.Black);

            // Get viewport dimensions
            int width = destination.Width;
            int height = destination.Height;
            var viewport = new Viewport(0, 0, width, height);

            // Begin sprite batch rendering
            // Use SpriteSortMode.Immediate for post-processing effects
            _spriteBatch.Begin(commandList, SpriteSortMode.Immediate, BlendStates.Opaque, _linearSampler,
                DepthStencilStates.None, RasterizerStates.CullNone, _brightPassEffect);

            // If we have a custom bright pass effect, set its parameters
            if (_brightPassEffect != null && _brightPassEffect.Parameters != null)
            {
                // Set threshold parameter for bright pass extraction
                var thresholdParam = _brightPassEffect.Parameters.Get("Threshold");
                if (thresholdParam != null)
                {
                    thresholdParam.SetValue(_threshold);
                }

                // Set source texture parameter
                var sourceTextureParam = _brightPassEffect.Parameters.Get("SourceTexture");
                if (sourceTextureParam != null)
                {
                    sourceTextureParam.SetValue(source);
                }

                // Set screen size parameters for UV calculations
                var screenSizeParam = _brightPassEffect.Parameters.Get("ScreenSize");
                if (screenSizeParam != null)
                {
                    screenSizeParam.SetValue(new Vector2(width, height));
                }

                var screenSizeInvParam = _brightPassEffect.Parameters.Get("ScreenSizeInv");
                if (screenSizeInvParam != null)
                {
                    screenSizeInvParam.SetValue(new Vector2(1.0f / width, 1.0f / height));
                }
            }

            // Draw full-screen quad with source texture
            // Rectangle covering entire destination render target
            var destinationRect = new RectangleF(0, 0, width, height);
            _spriteBatch.Draw(source, destinationRect, Color.White);

            // End sprite batch rendering
            _spriteBatch.End();

            // Reset render target (restore previous state)
            commandList.SetRenderTarget(null, (Texture)null);
        }

        private void ApplyGaussianBlur(Texture source, Texture destination, bool horizontal, RenderContext context)
        {
            // Apply separable Gaussian blur
            // horizontal: blur in X direction
            // !horizontal: blur in Y direction

            if (source == null || destination == null || _graphicsDevice == null || _spriteBatch == null)
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

            // Clear render target to black using GraphicsDevice
            // Note: In Stride, clearing is typically done through GraphicsDevice after setting render target
            _graphicsDevice.Clear(destination, Color.Black);

            // Get viewport dimensions
            int width = destination.Width;
            int height = destination.Height;
            var viewport = new Viewport(0, 0, width, height);

            // Calculate blur radius based on intensity
            // Higher intensity = larger blur radius for stronger glow effect
            float blurRadius = _intensity * 2.0f; // Scale intensity to blur radius

            // Begin sprite batch rendering
            // Use SpriteSortMode.Immediate for post-processing effects
            _spriteBatch.Begin(commandList, SpriteSortMode.Immediate, BlendStates.Opaque, _linearSampler,
                DepthStencilStates.None, RasterizerStates.CullNone, _blurEffect);

            // If we have a custom blur effect, set its parameters
            if (_blurEffect != null && _blurEffect.Parameters != null)
            {
                // Set blur direction parameter
                // Horizontal: (1, 0) for X-direction blur
                // Vertical: (0, 1) for Y-direction blur
                var blurDirectionParam = _blurEffect.Parameters.Get("BlurDirection");
                if (blurDirectionParam != null)
                {
                    var direction = horizontal ? Vector2.UnitX : Vector2.UnitY;
                    blurDirectionParam.SetValue(direction);
                }

                // Set blur radius parameter
                var blurRadiusParam = _blurEffect.Parameters.Get("BlurRadius");
                if (blurRadiusParam != null)
                {
                    blurRadiusParam.SetValue(blurRadius);
                }

                // Set source texture parameter
                var sourceTextureParam = _blurEffect.Parameters.Get("SourceTexture");
                if (sourceTextureParam != null)
                {
                    sourceTextureParam.SetValue(source);
                }

                // Set screen size parameters for UV calculations
                var screenSizeParam = _blurEffect.Parameters.Get("ScreenSize");
                if (screenSizeParam != null)
                {
                    screenSizeParam.SetValue(new Vector2(width, height));
                }

                var screenSizeInvParam = _blurEffect.Parameters.Get("ScreenSizeInv");
                if (screenSizeInvParam != null)
                {
                    screenSizeInvParam.SetValue(new Vector2(1.0f / width, 1.0f / height));
                }
            }

            // Draw full-screen quad with source texture
            // Rectangle covering entire destination render target
            var destinationRect = new RectangleF(0, 0, width, height);
            _spriteBatch.Draw(source, destinationRect, Color.White);

            // End sprite batch rendering
            _spriteBatch.End();

            // Reset render target (restore previous state)
            commandList.SetRenderTarget(null, (Texture)null);
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
        /// - Original game: DirectX 8/9 fixed-function pipeline (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
        /// - Modern implementation: Uses programmable shaders with runtime compilation
        /// </remarks>
        private Effect CompileShaderFromSource(string shaderSource, string shaderName)
        {
            if (string.IsNullOrEmpty(shaderSource))
            {
                System.Console.WriteLine($"[StrideBloomEffect] Cannot compile shader '{shaderName}': shader source is null or empty");
                return null;
            }

            if (_graphicsDevice == null)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Cannot compile shader '{shaderName}': GraphicsDevice is null");
                return null;
            }

            try
            {
                // Strategy 1: Try to get EffectCompiler from GraphicsDevice services
                // Based on Stride API: GraphicsDevice.Services provides access to EffectSystem
                // EffectSystem contains EffectCompiler for runtime shader compilation
                var services = _graphicsDevice.Services;
                if (services != null)
                {
                    // Try to get EffectCompiler from services
                    // EffectCompiler is typically available through EffectSystem service
                    var effectCompiler = services.GetService<EffectCompiler>();
                    if (effectCompiler != null)
                    {
                        return CompileShaderWithCompiler(effectCompiler, shaderSource, shaderName);
                    }

                    // Try to get EffectSystem from services (EffectCompiler may be accessed through it)
                    // Based on Stride architecture: EffectSystem manages effect compilation
                    var effectSystem = services.GetService<Stride.Shaders.Compiler.EffectCompiler>();
                    if (effectSystem != null)
                    {
                        // EffectSystem may provide access to EffectCompiler
                        // Try to compile using EffectSystem's compilation capabilities
                        return CompileShaderWithEffectSystem(effectSystem, shaderSource, shaderName);
                    }
                }

                // Strategy 2: Create temporary shader file and compile it
                // Fallback method: Write shader source to temporary file and compile
                // Based on Stride: Shaders are typically compiled from .sdsl files
                return CompileShaderFromFile(shaderSource, shaderName);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Failed to compile shader '{shaderName}': {ex.Message}");
                System.Console.WriteLine($"[StrideBloomEffect] Stack trace: {ex.StackTrace}");
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
        private Effect CompileShaderWithCompiler(EffectCompiler compiler, string shaderSource, string shaderName)
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
                var compilationResult = compiler.Compile(compilerSource, new CompilerParameters
                {
                    EffectParameters = new EffectCompilerParameters(),
                    Platform = _graphicsDevice.Features.Profile
                });

                if (compilationResult != null && compilationResult.Bytecode != null && compilationResult.Bytecode.Length > 0)
                {
                    // Create Effect from compiled bytecode
                    // Based on Stride API: Effect constructor accepts compiled bytecode
                    var effect = new Effect(_graphicsDevice, compilationResult.Bytecode);
                    System.Console.WriteLine($"[StrideBloomEffect] Successfully compiled shader '{shaderName}' using EffectCompiler");
                    return effect;
                }
                else
                {
                    System.Console.WriteLine($"[StrideBloomEffect] EffectCompiler compilation failed for shader '{shaderName}': No bytecode generated");
                    if (compilationResult != null && compilationResult.HasErrors)
                    {
                        System.Console.WriteLine($"[StrideBloomEffect] Compilation errors: {compilationResult.ErrorText}");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Exception while compiling shader '{shaderName}' with EffectCompiler: {ex.Message}");
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
        private Effect CompileShaderWithEffectSystem(global::Stride.Shaders.Compiler.EffectCompiler effectSystem, string shaderSource, string shaderName)
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

                System.Console.WriteLine($"[StrideBloomEffect] EffectSystem does not provide direct compiler access for shader '{shaderName}'");
                return null;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Exception while compiling shader '{shaderName}' with EffectSystem: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compiles shader from temporary file (fallback method).
        /// </summary>
        /// <param name="shaderSource">Shader source code.</param>
        /// <param name="shaderName">Shader name for identification.</param>
        /// <returns>Compiled Effect, or null if compilation fails.</returns>
        private Effect CompileShaderFromFile(string shaderSource, string shaderName)
        {
            string tempFilePath = null;
            try
            {
                // Create temporary file for shader source
                // Based on Stride: Shaders are compiled from .sdsl files
                tempFilePath = Path.Combine(Path.GetTempPath(), $"{shaderName}_{Guid.NewGuid()}.sdsl");
                File.WriteAllText(tempFilePath, shaderSource);

                // Try to compile shader from file
                // Based on Stride API: EffectCompiler can compile from file paths
                var services = _graphicsDevice.Services;
                if (services != null)
                {
                    var effectCompiler = services.GetService<EffectCompiler>();
                    if (effectCompiler != null)
                    {
                        // Create compilation source from file
                        var compilerSource = new ShaderSourceClass
                        {
                            Name = shaderName,
                            SourceCode = shaderSource
                        };

                        var compilationResult = effectCompiler.Compile(compilerSource, new CompilerParameters
                        {
                            EffectParameters = new EffectCompilerParameters(),
                            Platform = _graphicsDevice.Features.Profile
                        });

                        if (compilationResult != null && compilationResult.Bytecode != null && compilationResult.Bytecode.Length > 0)
                        {
                            var effect = new Effect(_graphicsDevice, compilationResult.Bytecode);
                            System.Console.WriteLine($"[StrideBloomEffect] Successfully compiled shader '{shaderName}' from file");
                            return effect;
                        }
                    }
                }

                // Final fallback: Try Effect.Load() with file path
                // This may work if Stride can load shaders from absolute paths
                try
                {
                    var effect = Effect.Load(_graphicsDevice, tempFilePath);
                    if (effect != null)
                    {
                        System.Console.WriteLine($"[StrideBloomEffect] Successfully loaded shader '{shaderName}' from file");
                        return effect;
                    }
                }
                catch
                {
                    // Effect.Load() doesn't support file paths directly, continue to return null
                }

                System.Console.WriteLine($"[StrideBloomEffect] Could not compile shader '{shaderName}' from file");
                return null;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[StrideBloomEffect] Exception while compiling shader '{shaderName}' from file: {ex.Message}");
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

        protected override void OnDispose()
        {
            _brightPassTarget?.Dispose();
            _brightPassTarget = null;

            if (_blurTargets != null)
            {
                foreach (var target in _blurTargets)
                {
                    target?.Dispose();
                }
                _blurTargets = null;
            }

            _brightPassEffect?.Dispose();
            _brightPassEffect = null;

            _blurEffect?.Dispose();
            _blurEffect = null;

            _brightPassEffectBase?.Dispose();
            _brightPassEffectBase = null;

            _blurEffectBase?.Dispose();
            _blurEffectBase = null;

            _spriteBatch?.Dispose();
            _spriteBatch = null;

            _linearSampler?.Dispose();
            _linearSampler = null;

            _pointSampler?.Dispose();
            _pointSampler = null;
        }
    }
}


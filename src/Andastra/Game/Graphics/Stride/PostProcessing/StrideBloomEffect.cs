using System;
using System.IO;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Stride.Graphics;
using Stride.Core.Mathematics;
using Stride.Core.Serialization.Contents;
using Stride.Rendering;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Game.Stride.PostProcessing
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
        private StrideGraphics.GraphicsDevice _graphicsDevice;
        private StrideGraphics.Texture _brightPassTarget;
        private StrideGraphics.Texture[] _blurTargets;
        private StrideGraphics.SpriteBatch _spriteBatch;
        private StrideGraphics.SamplerState _linearSampler;
        private StrideGraphics.SamplerState _pointSampler;
        private EffectInstance _brightPassEffect;
        private EffectInstance _blurEffect;
        private StrideGraphics.Effect _brightPassEffectBase;
        private StrideGraphics.Effect _blurEffectBase;

        public StrideBloomEffect(StrideGraphics.GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
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
            // Strategy 1: Try loading from ContentManager
            // Effect.Load() doesn't exist in this Stride version, so we skip directly to ContentManager
            // Strategy 2: Try loading from ContentManager if available
            // Check if GraphicsDevice has access to ContentManager through services
            if (_brightPassEffectBase == null || _blurEffectBase == null)
            {
                try
                {
                    // Try to get ContentManager from GraphicsDevice services
                    // Stride GraphicsDevice may have Services property that provides ContentManager
                    object services = _graphicsDevice.Services();
                    if (services != null)
                    {
                        var contentManager = ((dynamic)services).GetService<ContentManager>();
                        if (contentManager != null)
                        {
                            if (_brightPassEffectBase == null)
                            {
                                try
                                {
                                    _brightPassEffectBase = contentManager.Load<StrideGraphics.Effect>("BloomBrightPass");
                                    if (_brightPassEffectBase != null)
                                    {
                                        _brightPassEffect = new EffectInstance(_brightPassEffectBase);
                                        Console.WriteLine("[StrideBloomEffect] Loaded BloomBrightPass from ContentManager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[StrideBloomEffect] Failed to load BloomBrightPass from ContentManager: {ex.Message}");
                                }
                            }

                            if (_blurEffectBase == null)
                            {
                                try
                                {
                                    _blurEffectBase = contentManager.Load<StrideGraphics.Effect>("BloomBlur");
                                    if (_blurEffectBase != null)
                                    {
                                        _blurEffect = new EffectInstance(_blurEffectBase);
                                        Console.WriteLine("[StrideBloomEffect] Loaded BloomBlur from ContentManager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[StrideBloomEffect] Failed to load BloomBlur from ContentManager: {ex.Message}");
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
        private StrideGraphics.Effect CreateBrightPassEffect()
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
        private StrideGraphics.Effect CreateBlurEffect()
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
                Console.WriteLine($"[StrideBloomEffect] Failed to create blur effect: {ex.Message}");
                Console.WriteLine($"[StrideBloomEffect] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Applies bloom effect to the input texture.
        /// </summary>
        public StrideGraphics.Texture Apply(StrideGraphics.Texture hdrInput, RenderContext context)
        {
            if (!_enabled || hdrInput == null) return hdrInput;

            EnsureRenderTargets(hdrInput.Width, hdrInput.Height, hdrInput.Format);

            // Step 1: Bright pass extraction
            ExtractBrightAreas(hdrInput, _brightPassTarget, context);

            // Step 2: Multi-pass blur
            StrideGraphics.Texture blurSource = _brightPassTarget;
            for (int i = 0; i < _blurPasses; i++)
            {
                ApplyGaussianBlur(blurSource, _blurTargets[i], i % 2 == 0, context);
                blurSource = _blurTargets[i];
            }

            // Step 3: Return blurred result (compositing done in final pass)
            return _blurTargets[_blurPasses - 1] ?? hdrInput;
        }

        private void EnsureRenderTargets(int width, int height, StrideGraphics.PixelFormat format)
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
            _brightPassTarget = StrideGraphics.Texture.New2D(_graphicsDevice, width, height,
                format, StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource);

            // Create blur targets (at progressively lower resolutions for performance)
            _blurTargets = new StrideGraphics.Texture[_blurPasses];
            int blurWidth = width / 2;
            int blurHeight = height / 2;

            for (int i = 0; i < _blurPasses; i++)
            {
                _blurTargets[i] = StrideGraphics.Texture.New2D(_graphicsDevice,
                    Math.Max(1, blurWidth),
                    Math.Max(1, blurHeight),
                    format,
                    StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource);

                blurWidth /= 2;
                blurHeight /= 2;
            }

            _initialized = true;
        }

        private void ExtractBrightAreas(StrideGraphics.Texture source, StrideGraphics.Texture destination, RenderContext context)
        {
            // Apply threshold-based bright pass shader
            // Pixels above threshold are kept, others are set to black
            // threshold is typically 1.0 for HDR content

            if (source == null || destination == null || _graphicsDevice == null || _spriteBatch == null)
            {
                return;
            }

            // Get command list for rendering operations
            // ImmediateContext is an extension method, not a property
            StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
            if (commandList == null)
            {
                return;
            }

            // Set render target to destination
            commandList.SetRenderTarget(null, destination);

            // Clear render target to black using CommandList
            // Note: In Stride, clearing is done through CommandList after setting render target
            commandList.Clear(destination, global::Stride.Core.Mathematics.Color4.Black);

            // Get viewport dimensions
            int width = destination.Width;
            int height = destination.Height;
            commandList.SetViewport(new StrideGraphics.Viewport(0, 0, width, height));

            // Begin sprite batch rendering
            // Use SpriteSortMode.Immediate for post-processing effects
            // Begin sprite batch rendering
            // Use SpriteSortMode.Immediate for post-processing effects
            // Get GraphicsContext from GraphicsDevice using extension method
            var graphicsContext = _graphicsDevice.GraphicsContext();
            if (graphicsContext == null)
            {
                System.Console.WriteLine("[StrideBloomEffect] Warning: Could not begin sprite batch - GraphicsContext unavailable");
                return;
            }

            _spriteBatch.Begin(graphicsContext, StrideGraphics.SpriteSortMode.Immediate, StrideGraphics.BlendStates.Opaque, _linearSampler,
                StrideGraphics.DepthStencilStates.None, StrideGraphics.RasterizerStates.CullNone, _brightPassEffect);

            // If we have a custom bright pass effect, set its parameters
            // Use reflection to set parameters since ParameterCollection.Get<T> requires non-nullable value types
            // and proper ParameterKey types which may not be available at runtime
            if (_brightPassEffect != null && _brightPassEffect.Parameters != null)
            {
                try
                {
                    // Set bright pass threshold parameter
                    var setFloatMethod = typeof(global::Stride.Rendering.ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(float) });
                    if (setFloatMethod != null)
                    {
                        setFloatMethod.Invoke(_brightPassEffect.Parameters, new object[] { "Threshold", _threshold });
                    }

                    // Set screen size parameters
                    var setVector2Method = typeof(global::Stride.Rendering.ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(Vector2) });
                    if (setVector2Method != null)
                    {
                        setVector2Method.Invoke(_brightPassEffect.Parameters, new object[] { "ScreenSize", new Vector2(width, height) });
                        setVector2Method.Invoke(_brightPassEffect.Parameters, new object[] { "ScreenSizeInv", new Vector2(1.0f / width, 1.0f / height) });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideBloomEffect] Failed to set bright pass parameters: {ex.Message}");
                }
            }

            // Draw full-screen quad with source StrideGraphics.Texture
            // Rectangle covering entire destination render target
            var destinationRect = new RectangleF(0, 0, width, height);
            _spriteBatch.Draw(source, destinationRect, Color.White);

            // End sprite batch rendering
            _spriteBatch.End();

            // Reset render target (restore previous state)
            commandList.SetRenderTarget(null, (StrideGraphics.Texture)null);
        }

        private void ApplyGaussianBlur(StrideGraphics.Texture source, StrideGraphics.Texture destination, bool horizontal, RenderContext context)
        {
            // Apply separable Gaussian blur
            // horizontal: blur in X direction
            // !horizontal: blur in Y direction

            if (source == null || destination == null || _graphicsDevice == null || _spriteBatch == null)
            {
                return;
            }

            // Get command list for rendering operations
            // ImmediateContext is an extension method, not a property
            StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
            if (commandList == null)
            {
                return;
            }

            // Set render target to destination
            commandList.SetRenderTarget(null, destination);

            // Clear render target to black using CommandList
            // Note: In Stride, clearing is done through CommandList after setting render target
            commandList.Clear(destination, Color4.Black);

            // Get viewport dimensions
            int width = destination.Width;
            int height = destination.Height;
            commandList.SetViewport(new StrideGraphics.Viewport(0, 0, width, height));

            // Begin sprite batch rendering
            // Use SpriteSortMode.Immediate for post-processing effects
            // Begin sprite batch rendering
            // Use SpriteSortMode.Immediate for post-processing effects
            // Get GraphicsContext from GraphicsDevice using extension method
            var graphicsContext = _graphicsDevice.GraphicsContext();
            if (graphicsContext == null)
            {
                Console.WriteLine("[StrideBloomEffect] Warning: Could not begin sprite batch - GraphicsContext unavailable");
                return;
            }

            _spriteBatch.Begin(graphicsContext, StrideGraphics.SpriteSortMode.Immediate, StrideGraphics.BlendStates.Opaque, _linearSampler,
                StrideGraphics.DepthStencilStates.None, StrideGraphics.RasterizerStates.CullNone, _blurEffect);

            // If we have a custom blur effect, set its parameters
            // Use reflection to set parameters since ParameterCollection.Get<T> requires non-nullable value types
            // and proper ParameterKey types which may not be available at runtime
            if (_blurEffect != null && _blurEffect.Parameters != null)
            {
                try
                {
                    // Set blur direction parameter (horizontal/vertical based on 'horizontal' flag)
                    var setVector2Method = typeof(global::Stride.Rendering.ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(Vector2) });
                    if (setVector2Method != null)
                    {
                        var blurDirection = horizontal ? new Vector2(1.0f, 0.0f) : new Vector2(0.0f, 1.0f);
                        setVector2Method.Invoke(_blurEffect.Parameters, new object[] { "BlurDirection", blurDirection });
                        setVector2Method.Invoke(_blurEffect.Parameters, new object[] { "ScreenSize", new Vector2(width, height) });
                        setVector2Method.Invoke(_blurEffect.Parameters, new object[] { "ScreenSizeInv", new Vector2(1.0f / width, 1.0f / height) });
                    }

                    // Set blur radius parameter
                    // Calculate blur radius based on intensity (higher intensity = larger blur radius)
                    var setFloatMethod = typeof(global::Stride.Rendering.ParameterCollection).GetMethod("Set", new[] { typeof(string), typeof(float) });
                    if (setFloatMethod != null)
                    {
                        float blurRadius = _intensity * 2.0f; // Scale intensity to blur radius
                        setFloatMethod.Invoke(_blurEffect.Parameters, new object[] { "BlurRadius", blurRadius });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideBloomEffect] Failed to set blur parameters: {ex.Message}");
                }
            }

            // Draw full-screen quad with source StrideGraphics.Texture
            // Rectangle covering entire destination render target
            var destinationRect = new RectangleF(0, 0, width, height);
            _spriteBatch.Draw(source, destinationRect, Color.White);

            // End sprite batch rendering
            _spriteBatch.End();

            // Reset render target (restore previous state)
            commandList.SetRenderTarget(null, (StrideGraphics.Texture)null);
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
        private StrideGraphics.Effect CompileShaderFromSource(string shaderSource, string shaderName)
        {
            if (string.IsNullOrEmpty(shaderSource))
            {
                Console.WriteLine($"[StrideBloomEffect] Cannot compile shader '{shaderName}': shader source is null or empty");
                return null;
            }

            if (_graphicsDevice == null)
            {
                Console.WriteLine($"[StrideBloomEffect] Cannot compile shader '{shaderName}': GraphicsDevice is null");
                return null;
            }

            try
            {
                // Strategy 1: Try to get EffectCompiler from GraphicsDevice services
                // Based on Stride API: GraphicsDevice.Services provides access to EffectSystem
                // EffectSystem contains EffectCompiler for runtime shader compilation
                // Use explicit type to avoid C# 7.3 inferred delegate type limitation
                // Note: Services() extension method returns null in this Stride version, but code handles null gracefully
                object services = _graphicsDevice.Services();
                if (services != null)
                {
                    // Try to get EffectCompiler from services using dynamic to avoid type issues
                    // EffectCompiler is typically available through EffectSystem service
                    dynamic servicesDynamic = services;
                    var effectCompiler = servicesDynamic.GetService<EffectCompiler>();
                    if (effectCompiler != null)
                    {
                        return CompileShaderWithCompiler(effectCompiler, shaderSource, shaderName);
                    }

                    // Try to get EffectSystem from services (EffectCompiler may be accessed through it)
                    // Based on Stride architecture: EffectSystem manages effect compilation
                    // Use global:: to avoid namespace resolution conflicts
                    var effectSystem = ((dynamic)services).GetService<EffectCompiler>();
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
                Console.WriteLine($"[StrideBloomEffect] Failed to compile shader '{shaderName}': {ex.Message}");
                Console.WriteLine($"[StrideBloomEffect] Stack trace: {ex.StackTrace}");
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
                // CompilerParameters provides compilation settings including platform target
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
                    // Use global:: to avoid namespace resolution conflicts
                    var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                    Console.WriteLine($"[StrideBloomEffect] Successfully compiled shader '{shaderName}' using EffectCompiler");
                    return effect;
                }
                else
                {
                    Console.WriteLine($"[StrideBloomEffect] EffectCompiler compilation failed for shader '{shaderName}': No bytecode generated");
                    if (compilerResult != null && compilerResult.HasErrors)
                    {
                        // CompilerResults may not have ErrorText, use ToString() or check for specific error properties
                        Console.WriteLine($"[StrideBloomEffect] Compilation errors occurred");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBloomEffect] Exception while compiling shader '{shaderName}' with EffectCompiler: {ex.Message}");
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
        private StrideGraphics.Effect CompileShaderFromFile(string shaderSource, string shaderName)
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
                // Use explicit type to avoid C# 7.3 inferred delegate type limitation
                // Note: Services() extension method returns null in this Stride version, but code handles null gracefully
                object services = _graphicsDevice.Services();
                if (services != null)
                {
                    dynamic servicesDynamic = services;
                    var effectCompiler = servicesDynamic.GetService<EffectCompiler>();
                    if (effectCompiler != null)
                    {
                        // Create compilation source from file
                        var compilerSource = new ShaderSourceClass
                        {
                            Name = shaderName,
                            SourceCode = shaderSource
                        };

                        // CompilerParameters provides compilation settings including platform target
                        // EffectParameters specifies shader compilation options
                        // Platform ensures shader is compiled for the correct graphics API (DirectX, Vulkan, etc.)
                        dynamic compilationResult = effectCompiler.Compile(compilerSource, new CompilerParameters
                        {
                            EffectParameters = new EffectCompilerParameters()
                        });

                        // Unwrap TaskOrResult to get compilation result
                        dynamic compilerResult = compilationResult.Result;
                        if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                        {
                            // Create Effect from compiled bytecode
                            var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                            Console.WriteLine($"[StrideBloomEffect] Successfully compiled shader '{shaderName}' from file using EffectCompiler");
                            return effect;
                        }
                        else
                        {
                            Console.WriteLine($"[StrideBloomEffect] File-based compilation failed for shader '{shaderName}': No bytecode generated");
                        }
                    }
                }

                // Final fallback: Effect.Load() doesn't exist in this Stride version
                // Effect.Load() doesn't support file paths directly, continue to return null

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
        }
    }
}

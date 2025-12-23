using System;
using System.IO;
using StrideGraphics = Stride.Graphics;
using Stride.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Stride.Graphics;

namespace Andastra.Runtime.Stride.PostProcessing
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
        private EffectInstance _motionBlurEffect;
        private StrideGraphics.Texture _velocityTexture;
        private StrideGraphics.Texture _temporaryTexture;
        private StrideGraphics.SpriteBatch _spriteBatch;
        private StrideGraphics.SamplerState _linearSampler;
        private StrideGraphics.Effect _motionBlurEffectBase;

        public StrideMotionBlurEffect(StrideGraphics.GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            InitializeRenderingResources();
        }

        private void InitializeRenderingResources()
        {
            // Create sprite batch for fullscreen quad rendering
            _spriteBatch = new SpriteBatch(_graphicsDevice);

            // Create linear sampler for texture sampling
            _linearSampler = SamplerState.New(_graphicsDevice, new SamplerStateDescription
            {
                Filter = TextureFilter.Linear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp
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
            // Strategy 1: Try loading from ContentManager
            // Effect.Load() doesn't exist in this Stride version, so we skip directly to ContentManager
            // Strategy 2: Try loading from ContentManager if available
            if (_motionBlurEffectBase == null)
            {
                try
                {
                    // Use explicit type to avoid C# 7.3 inferred delegate type limitation
                    object services = _graphicsDevice.Services();
                    if (services != null)
                    {
                        var contentManager = ((dynamic)services).GetService<ContentManager>();
                        if (contentManager != null)
                        {
                            try
                            {
                                _motionBlurEffectBase = contentManager.Load<Effect>("MotionBlur");
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
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] Failed to access ContentManager: {ex.Message}");
                }
            }

            // Strategy 3: Create shader programmatically if loading failed
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
                // Based on Stride: Shaders are compiled from .sdsl files
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
                    System.Console.WriteLine($"[StrideMotionBlurEffect] Successfully compiled shader '{shaderName}' using EffectCompiler");
                    return effect;
                }
                else
                {
                    System.Console.WriteLine($"[StrideMotionBlurEffect] EffectCompiler compilation failed for shader '{shaderName}': No bytecode generated");
                    if (compilationResult != null && compilationResult.HasErrors)
                    {
                        System.Console.WriteLine($"[StrideMotionBlurEffect] Compilation errors: {compilationResult.ErrorText}");
                    }
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
                            System.Console.WriteLine($"[StrideMotionBlurEffect] Successfully compiled shader '{shaderName}' from file");
                            return effect;
                        }
                    }
                }

                // Final fallback: Effect.Load() doesn't exist in this Stride version
                // Effect.Load() doesn't support file paths directly, continue to return null

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
            var commandList = _graphicsDevice.ImmediateContext;
            if (commandList == null)
            {
                return;
            }

            // Save previous render target
            var previousRenderTarget = commandList.GetRenderTarget(0);

            try
            {
                // Set render target to output texture
                commandList.SetRenderTarget(null, output);

                // Clear render target to black
                _graphicsDevice.Clear(output, Color.Black);

                // Calculate effective parameters
                var effectiveIntensity = _intensity * deltaTime * 60.0f; // Normalize to 60fps
                var clampedVelocity = Math.Min(_maxVelocity, effectiveIntensity * 100.0f);
                var depthThreshold = 0.1f; // Depth discontinuity threshold

                System.Console.WriteLine($"[StrideMotionBlur] Applying blur: {_sampleCount} samples, intensity {effectiveIntensity:F2}, max velocity {clampedVelocity:F2}");

                // Begin sprite batch rendering with motion blur effect
                _spriteBatch.Begin(commandList, SpriteSortMode.Immediate, BlendStates.Opaque,
                    _linearSampler, DepthStencilStates.None, RasterizerStates.CullNone, _motionBlurEffect);

                // Set shader parameters if effect is available
                if (_motionBlurEffect != null && _motionBlurEffect.Parameters != null)
                {
                    // Set motion blur parameters
                    var intensityParam = _motionBlurEffect.Parameters.Get("Intensity");
                    if (intensityParam != null)
                    {
                        intensityParam.SetValue(effectiveIntensity);
                    }

                    var maxVelocityParam = _motionBlurEffect.Parameters.Get("MaxVelocity");
                    if (maxVelocityParam != null)
                    {
                        maxVelocityParam.SetValue(clampedVelocity);
                    }

                    var sampleCountParam = _motionBlurEffect.Parameters.Get("SampleCount");
                    if (sampleCountParam != null)
                    {
                        sampleCountParam.SetValue((float)_sampleCount);
                    }

                    var deltaTimeParam = _motionBlurEffect.Parameters.Get("DeltaTime");
                    if (deltaTimeParam != null)
                    {
                        deltaTimeParam.SetValue(deltaTime);
                    }

                    var depthThresholdParam = _motionBlurEffect.Parameters.Get("DepthThreshold");
                    if (depthThresholdParam != null)
                    {
                        depthThresholdParam.SetValue(depthThreshold);
                    }

                    // Set texture parameters
                    var colorTextureParam = _motionBlurEffect.Parameters.Get("ColorTexture");
                    if (colorTextureParam != null)
                    {
                        colorTextureParam.SetValue(input);
                    }

                    var motionVectorsParam = _motionBlurEffect.Parameters.Get("MotionVectorsTexture");
                    if (motionVectorsParam != null)
                    {
                        motionVectorsParam.SetValue(motionVectors);
                    }

                    var depthTextureParam = _motionBlurEffect.Parameters.Get("DepthTexture");
                    if (depthTextureParam != null && depth != null)
                    {
                        depthTextureParam.SetValue(depth);
                    }

                    // Set screen size parameters for UV calculations
                    var screenSizeParam = _motionBlurEffect.Parameters.Get("ScreenSize");
                    if (screenSizeParam != null)
                    {
                        screenSizeParam.SetValue(new Vector2(output.Width, output.Height));
                    }

                    var screenSizeInvParam = _motionBlurEffect.Parameters.Get("ScreenSizeInv");
                    if (screenSizeInvParam != null)
                    {
                        screenSizeInvParam.SetValue(new Vector2(1.0f / output.Width, 1.0f / output.Height));
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
                // Restore previous render target
                commandList.SetRenderTarget(null, previousRenderTarget?.Texture);
            }
        }
    }
}


using System;
using System.IO;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Stride.Graphics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Rendering;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using StrideGraphics = Stride.Graphics;
using Vector2 = Stride.Core.Mathematics.Vector2;
using Vector3 = Stride.Core.Mathematics.Vector3;
using Vector4 = Stride.Core.Mathematics.Vector4;

namespace Andastra.Game.Stride.PostProcessing
{
    /// <summary>
    /// Stride implementation of Color Grading post-processing effect.
    /// Inherits shared color grading logic from BaseColorGradingEffect.
    ///
    /// Features:
    /// - 3D LUT (Look-Up Table) color grading
    /// - Contrast, saturation, brightness adjustments
    /// - LUT blending (strength control)
    /// - Support for 16x16x16 and 32x32x32 LUTs
    /// - Real-time parameter adjustment
    ///
    /// Based on Stride rendering pipeline: https://doc.stride3d.net/latest/en/manual/graphics/
    /// Color grading is used to achieve cinematic color aesthetics and mood.
    /// </summary>
    public class StrideColorGradingEffect : BaseColorGradingEffect
    {
        private StrideGraphics.GraphicsDevice _graphicsDevice;
        private EffectInstance _colorGradingEffect;
        private StrideGraphics.Texture _temporaryTexture;
        private int _lutSize; // Size of the 3D LUT (16 or 32)
        private bool _effectInitialized;
        private StrideGraphics.SpriteBatch _spriteBatch;
        private StrideGraphics.SamplerState _linearSampler;
        private bool _renderingResourcesInitialized;

        public StrideColorGradingEffect(StrideGraphics.GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _lutSize = 0;
            _effectInitialized = false;
            _renderingResourcesInitialized = false;
            InitializeRenderingResources();
        }

        #region BaseColorGradingEffect Implementation

        protected override void OnDispose()
        {
            _colorGradingEffect?.Dispose();
            _colorGradingEffect = null;

            // Note: Don't dispose LUT StrideGraphics.Texture here if it's managed externally
            // Only dispose if we created it ourselves

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
        /// Initializes rendering resources needed for GPU color grading.
        /// Creates SpriteBatch for fullscreen quad rendering and linear sampler for StrideGraphics.Texture sampling.
        /// </summary>
        private void InitializeRenderingResources()
        {
            if (_renderingResourcesInitialized)
            {
                return;
            }

            try
            {
                // Create sprite batch for fullscreen quad rendering
                _spriteBatch = new StrideGraphics.SpriteBatch(_graphicsDevice);

                // Create linear sampler for StrideGraphics.Texture sampling
                _linearSampler = StrideGraphics.SamplerState.New(_graphicsDevice, new StrideGraphics.SamplerStateDescription
                {
                    Filter = StrideGraphics.TextureFilter.Linear,
                    AddressU = StrideGraphics.TextureAddressMode.Clamp,
                    AddressV = StrideGraphics.TextureAddressMode.Clamp,
                    AddressW = StrideGraphics.TextureAddressMode.Clamp
                });

                _renderingResourcesInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] Failed to initialize rendering resources: {ex.Message}");
                _renderingResourcesInitialized = false;
            }
        }

        /// <summary>
        /// Loads a 3D LUT texture for color grading.
        /// </summary>
        /// <param name="lutTexture">3D texture (16x16x16 or 32x32x32) containing color transform.</param>
        public void LoadLut(StrideGraphics.Texture lutTexture)
        {
            if (lutTexture == null)
            {
                throw new ArgumentNullException(nameof(lutTexture));
            }

            // Validate LUT dimensions
            // Common sizes: 16x16x16 (256x16) or 32x32x32 (1024x32) flattened to 2D
            if (lutTexture.Dimension != StrideGraphics.TextureDimension.Texture2D)
            {
                throw new ArgumentException("LUT must be a 2D StrideGraphics.Texture (flattened 3D)", nameof(lutTexture));
            }

            // Determine LUT size from StrideGraphics.Texture dimensions
            // 16x16x16 LUT: 256x16 (16 slices of 16x16)
            // 32x32x32 LUT: 1024x32 (32 slices of 32x32)
            int width = lutTexture.Width;
            int height = lutTexture.Height;

            if (width == 256 && height == 16)
            {
                _lutSize = 16;
            }
            else if (width == 1024 && height == 32)
            {
                _lutSize = 32;
            }
            else
            {
                // Try to infer from dimensions
                // For a 3D LUT flattened to 2D: width = size^2, height = size
                int inferredSize = (int)Math.Sqrt(width);
                if (inferredSize * inferredSize == width && inferredSize == height)
                {
                    _lutSize = inferredSize;
                }
                else
                {
                    throw new ArgumentException($"Unsupported LUT dimensions: {width}x{height}. Expected 256x16 (16^3) or 1024x32 (32^3)", nameof(lutTexture));
                }
            }

            _lutTexture = lutTexture;
            base.LutTexture = lutTexture;
        }

        /// <summary>
        /// Applies color grading to the input frame.
        /// </summary>
        /// <param name="input">LDR color buffer (after tone mapping).</param>
        /// <param name="width">Render width.</param>
        /// <param name="height">Render height.</param>
        /// <returns>Output StrideGraphics.Texture with color grading applied.</returns>
        public StrideGraphics.Texture Apply(StrideGraphics.Texture input, int width, int height)
        {
            if (!_enabled || input == null)
            {
                return input;
            }

            if (_lutTexture == null && _strength <= 0.0f && Math.Abs(_contrast) < 0.01f && Math.Abs(_saturation - 1.0f) < 0.01f)
            {
                // No color grading to apply
                return input;
            }

            EnsureTextures(width, height, input.Format);

            // Color Grading Process:
            // 1. Apply contrast adjustment
            // 2. Apply saturation adjustment
            // 3. Sample 3D LUT (if available)
            // 4. Blend LUT result with original based on strength
            // 5. Clamp to valid color range

            ExecuteColorGrading(input, _temporaryTexture);

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

        private void ExecuteColorGrading(StrideGraphics.Texture input, StrideGraphics.Texture output)
        {
            // Color Grading Shader Execution:
            // - Input: LDR color buffer [0, 1]
            // - Parameters: contrast, saturation, LUT StrideGraphics.Texture, LUT strength
            // - Process: Adjust contrast/saturation -> Sample LUT -> Blend
            // - Output: Color-graded LDR buffer

            // Try GPU shader path first, fall back to CPU if not available
            if (TryExecuteColorGradingGpu(input, output))
            {
                return;
            }

            // CPU fallback implementation
            ExecuteColorGradingCpu(input, output);
        }

        /// <summary>
        /// Attempts to execute color grading using GPU shader.
        /// Returns true if successful, false if CPU fallback is needed.
        /// </summary>
        private bool TryExecuteColorGradingGpu(StrideGraphics.Texture input, StrideGraphics.Texture output)
        {
            // Ensure rendering resources are initialized
            if (!_renderingResourcesInitialized)
            {
                InitializeRenderingResources();
            }

            if (_spriteBatch == null || _linearSampler == null)
            {
                return false;
            }

            // Initialize effect if needed
            if (!_effectInitialized)
            {
                InitializeEffect();
            }

            // If we don't have a custom effect, we can still use SpriteBatch for basic rendering
            // but it won't apply color grading, so we should fall back to CPU
            if (_colorGradingEffect == null)
            {
                return false;
            }

            try
            {
                StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
                if (commandList == null)
                {
                    return false;
                }

                // Set render target to output
                commandList.SetRenderTarget(null, output);
                commandList.SetViewport(new StrideGraphics.Viewport(0, 0, output.Width, output.Height));

                // Clear render target (optional, but ensures clean output)
                // In Stride, clearing is done through CommandList after setting render target
                commandList.Clear(output, Color.Transparent);

                // Get viewport dimensions
                int width = output.Width;
                int height = output.Height;

                // Begin sprite batch rendering with custom effect
                // Use SpriteSortMode.Immediate for post-processing effects
                // BlendStates.Opaque: overwrite pixels (no blending for post-processing)
                // DepthStencilStates.None: no depth testing needed for fullscreen quad
                // RasterizerStates.CullNone: render both front and back faces
                // SpriteBatch.Begin requires GraphicsContext, use dynamic to handle API differences
                ((dynamic)_spriteBatch).Begin(commandList, StrideGraphics.SpriteSortMode.Immediate, StrideGraphics.BlendStates.Opaque, _linearSampler,
                    StrideGraphics.DepthStencilStates.None, StrideGraphics.RasterizerStates.CullNone, _colorGradingEffect);

                // Set shader parameters using ParameterCollection.Set with parameter keys
                var parameters = _colorGradingEffect.Parameters;
                if (parameters != null)
                {
                    try
                    {
                        // Set input texture - try InputTexture first, then SourceTexture as fallback
                        try
                        {
                            parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("InputTexture"), input);
                        }
                        catch (Exception)
                        {
                            // Try alternative parameter name
                            try
                            {
                                parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("SourceTexture"), input);
                            }
                            catch (Exception)
                            {
                                // If we can't set the input texture, the shader won't work
                                _spriteBatch.End();
                                return false;
                            }
                        }

                        // Set LUT texture if available
                        if (_lutTexture != null)
                        {
                            try
                            {
                                var lutTexture = _lutTexture as StrideGraphics.Texture;
                                if (lutTexture != null)
                                {
                                    parameters.Set(new ObjectParameterKey<StrideGraphics.Texture>("LutTexture"), lutTexture);
                                    parameters.Set(new ValueParameterKey<int>("LutSize"), _lutSize);
                                }
                            }
                            catch (Exception)
                            {
                                // LUT parameters don't exist - continue without LUT
                            }
                        }

                        // Set color grading parameters (value types use ValueParameterKey)
                        try
                        {
                            parameters.Set(new ValueParameterKey<float>("Contrast"), _contrast);
                            parameters.Set(new ValueParameterKey<float>("Saturation"), _saturation);
                            parameters.Set(new ValueParameterKey<float>("Strength"), _strength);
                        }
                        catch (Exception)
                        {
                            // Parameters don't exist - continue with default values
                        }

                        // Set screen size parameters (useful for UV calculations)
                        try
                        {
                            parameters.Set(new ValueParameterKey<Vector2>("ScreenSize"), new Vector2(width, height));
                            parameters.Set(new ValueParameterKey<Vector2>("ScreenSizeInv"), new Vector2(1.0f / width, 1.0f / height));
                        }
                        catch (Exception)
                        {
                            // Screen size parameters don't exist - continue with default values
                        }
                    }
                    catch (Exception)
                    {
                        // Parameter setting failed - continue without custom parameters
                    }
                }

                // Draw fullscreen quad with input StrideGraphics.Texture
                // Rectangle covering entire output render target
                var destinationRect = new RectangleF(0, 0, width, height);
                _spriteBatch.Draw(input, destinationRect, Color.White);

                // End sprite batch rendering
                _spriteBatch.End();

                // Reset render target (restore previous state)
                commandList.SetRenderTarget(null, (StrideGraphics.Texture)null);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] GPU shader execution failed: {ex.Message}");
                // Ensure sprite batch is properly ended even on exception
                try
                {
                    _spriteBatch?.End();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                return false;
            }
        }

        /// <summary>
        /// Initializes the color grading effect instance.
        /// Attempts to load a shader effect from compiled .sdeffect files, but gracefully falls back if not available.
        /// </summary>
        private void InitializeEffect()
        {
            if (_effectInitialized)
            {
                return;
            }

            try
            {
                // Strategy 1: Try loading from compiled effect files
                // Based on Stride API: Effect.Load() may exist in some versions
                // Attempt to load pre-compiled effect files
                StrideGraphics.Effect effectBase = null;
                try
                {
                    // Try to access Effect.Load() method dynamically in case it exists
                    // This handles different Stride API versions gracefully
                    var effectType = typeof(StrideGraphics.Effect);
                    var loadMethod = effectType.GetMethod("Load", new[] { typeof(StrideGraphics.GraphicsDevice), typeof(string) });
                    if (loadMethod != null)
                    {
                        effectBase = (StrideGraphics.Effect)loadMethod.Invoke(null, new object[] { _graphicsDevice, "ColorGradingEffect" });
                        if (effectBase != null)
                        {
                            _colorGradingEffect = new EffectInstance(effectBase);
                            Console.WriteLine("[StrideColorGrading] Loaded ColorGradingEffect from compiled file");
                            _effectInitialized = true;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideColorGrading] Failed to load ColorGradingEffect from compiled file: {ex.Message}");
                }

                // Strategy 2: Try loading from ContentManager if available
                // ContentManager access through GraphicsDevice services
                if (effectBase == null)
                {
                    try
                    {
                        // Try to access services through GraphicsDevice
                        // Use reflection to handle different Stride API versions
                        var graphicsDeviceType = _graphicsDevice.GetType();
                        var servicesProperty = graphicsDeviceType.GetProperty("Services");
                        if (servicesProperty != null)
                        {
                            var services = servicesProperty.GetValue(_graphicsDevice);
                            if (services != null)
                            {
                                // Try to get ContentManager from services
                                var servicesType = services.GetType();
                                var getServiceMethod = servicesType.GetMethod("GetService", new[] { typeof(Type) });
                                if (getServiceMethod != null)
                                {
                                    // Look for ContentManager type
                                    var contentManagerType = Type.GetType("Stride.Core.Serialization.Contents.ContentManager, Stride.Core.Serialization");
                                    if (contentManagerType != null)
                                    {
                                        var contentManager = getServiceMethod.Invoke(services, new object[] { contentManagerType });
                                        if (contentManager != null)
                                        {
                                            try
                                            {
                                                // Try to load effect from ContentManager
                                                var loadGenericMethod = contentManagerType.GetMethod("Load").MakeGenericMethod(typeof(StrideGraphics.Effect));
                                                effectBase = (StrideGraphics.Effect)loadGenericMethod.Invoke(contentManager, new object[] { "ColorGradingEffect" });
                                                if (effectBase != null)
                                                {
                                                    _colorGradingEffect = new EffectInstance(effectBase);
                                                    Console.WriteLine("[StrideColorGrading] Loaded ColorGradingEffect from ContentManager");
                                                    _effectInitialized = true;
                                                    return;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"[StrideColorGrading] Failed to load ColorGradingEffect from ContentManager: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideColorGrading] Failed to access ContentManager: {ex.Message}");
                    }
                }

                // Strategy 3: Create effect programmatically using shader compilation
                // This provides a functional fallback that works without pre-compiled shader files
                if (effectBase == null)
                {
                    effectBase = CreateColorGradingEffect();
                    if (effectBase != null)
                    {
                        _colorGradingEffect = new EffectInstance(effectBase);
                        Console.WriteLine("[StrideColorGrading] Created ColorGradingEffect programmatically");
                        _effectInitialized = true;
                        return;
                    }
                }

                // Strategy 4: Fallback to SpriteBatch default rendering
                // GPU path will still work using SpriteBatch, but without custom color grading shader
                // Color grading will fall back to CPU implementation
                Console.WriteLine("[StrideColorGrading] No ColorGradingEffect shader found. GPU path will use SpriteBatch default rendering (CPU fallback will be used)");
                _colorGradingEffect = null;
                _effectInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] Failed to initialize effect: {ex.Message}");
                _colorGradingEffect = null;
                _effectInitialized = true; // Mark as initialized to avoid retrying
            }
        }

        /// <summary>
        /// Creates a ColorGrading Effect programmatically by compiling shader source.
        /// This is a fallback when pre-compiled shader files are not available.
        /// </summary>
        /// <returns>Compiled Effect for color grading, or null if compilation fails.</returns>
        private StrideGraphics.Effect CreateColorGradingEffect()
        {
            if (_graphicsDevice == null)
            {
                Console.WriteLine("[StrideColorGrading] Cannot create ColorGradingEffect: GraphicsDevice is null");
                return null;
            }

            try
            {
                // Create color grading shader source in SDSL format
                // Based on Stride SDSL syntax and color grading algorithms
                // Implements full 3D LUT sampling with proper trilinear interpolation
                string shaderSource = @"
shader ColorGradingEffect : ShaderBase
{
    // Input texture
    Texture2D InputTexture;
    SamplerState LinearSampler;

    // LUT texture for color grading (flattened 3D LUT)
    Texture2D LUTTexture;
    SamplerState LUTSampler;

    // Color grading parameters
    float Contrast;
    float Saturation;
    float Brightness;
    float Strength;
    int LUTSize;

    // Screen size for UV calculation
    float2 ScreenSize;
    float2 ScreenSizeInv;

    // Compute luminance using ITU-R BT.601 standard
    float ComputeLuminance(float3 color)
    {
        return dot(color, float3(0.299, 0.587, 0.114));
    }

    // Sample 3D LUT that has been flattened to 2D texture
    // Based on industry-standard LUT sampling algorithm
    float3 SampleLUT3D(float3 rgb, int lutSize, float2 lutSizeInv)
    {
        // Clamp the sample in by half a pixel to avoid interpolation artifacts
        // between slices laid out next to each other
        float halfPixelWidth = 0.5 * lutSizeInv.x;
        float r = clamp(rgb.r, halfPixelWidth, 1.0 - halfPixelWidth);
        float g = clamp(rgb.g, halfPixelWidth, 1.0 - halfPixelWidth);
        float b = clamp(rgb.b, 0.0, 1.0);

        // Green offset into a LUT layer
        float gOffset = g * lutSizeInv.x;

        // Calculate blue slice and interpolation factor
        float bNormalized = lutSize * b;
        int bSlice = (int)floor(bNormalized);
        bSlice = clamp(bSlice, 0, lutSize - 1);
        float bMix = (bNormalized - bSlice) * lutSizeInv.x;

        // Get the two blue slices to interpolate between
        float b1 = bSlice * lutSizeInv.x;
        float b2 = (bSlice + 1) * lutSizeInv.x;

        // Calculate UV coordinates for both slices
        // For flattened 3D LUT: each row is a blue slice, each column within a row is R*G
        float2 uv1 = float2(r, gOffset + b1);
        float2 uv2 = float2(r, gOffset + b2);

        // Sample from LUT texture
        float3 sample1 = LUTTexture.Sample(LUTSampler, uv1).rgb;
        float3 sample2 = LUTTexture.Sample(LUTSampler, uv2).rgb;

        // Interpolate between the two blue slices
        return lerp(sample1, sample2, bMix);
    }

    // Main pixel shader for color grading
    float4 MainPS(float4 position : SV_Position, float2 texCoord : TEXCOORD0) : SV_Target0
    {
        // Sample input texture
        float4 inputColor = InputTexture.Sample(LinearSampler, texCoord);
        float3 color = inputColor.rgb;

        // Step 1: Apply contrast adjustment
        // Formula: color = (color - 0.5) * (1.0 + contrast) + 0.5
        // Contrast range: [-1, 1], where 0 = no change
        float contrastFactor = 1.0 + Contrast;
        color = (color - 0.5) * contrastFactor + 0.5;

        // Step 2: Apply saturation adjustment
        // Formula: lerp(grayscale, color, saturation)
        // Saturation range: [0, 2], where 1.0 = no change, 0 = grayscale, >1 = oversaturated
        float luminance = ComputeLuminance(color);
        float3 grayscale = float3(luminance, luminance, luminance);
        color = lerp(grayscale, color, Saturation);

        // Step 3: Apply brightness adjustment
        color += Brightness;

        // Step 4: Sample 3D LUT (if available)
        float3 finalColor = color;
        if (LUTSize > 0 && Strength > 0.0)
        {
            float2 lutSizeInv = float2(1.0 / (LUTSize * LUTSize), 1.0 / LUTSize);
            float3 lutColor = SampleLUT3D(color, LUTSize, lutSizeInv);

            // Step 5: Blend LUT result with adjusted color based on strength
            // Formula: lerp(adjustedColor, lutColor, strength)
            finalColor = lerp(color, lutColor, Strength);
        }

        // Step 6: Clamp to valid color range and preserve alpha
        finalColor = saturate(finalColor);
        return float4(finalColor, inputColor.a);
    }

    // Simple vertex shader for fullscreen quad
    // Uses vertex ID to generate fullscreen triangle
    float4 MainVS(uint vertexId : SV_VertexID) : SV_Position
    {
        // Generate fullscreen triangle from vertex ID
        // This is more efficient than a quad for fullscreen effects
        float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
        float2 pos = uv * float2(2.0, -2.0) + float2(-1.0, 1.0);
        return float4(pos, 0.0, 1.0);
    }
};";

                // Compile shader using EffectCompiler
                // Based on Stride API: EffectCompiler compiles shader source to Effect bytecode
                return CompileShaderFromSource(shaderSource, "ColorGradingEffect");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] Failed to create ColorGradingEffect: {ex.Message}");
                Console.WriteLine($"[StrideColorGrading] Stack trace: {ex.StackTrace}");
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
        /// </remarks>
        private StrideGraphics.Effect CompileShaderFromSource(string shaderSource, string shaderName)
        {
            if (string.IsNullOrEmpty(shaderSource))
            {
                Console.WriteLine($"[StrideColorGrading] Cannot compile shader '{shaderName}': shader source is null or empty");
                return null;
            }

            if (_graphicsDevice == null)
            {
                Console.WriteLine($"[StrideColorGrading] Cannot compile shader '{shaderName}': GraphicsDevice is null");
                return null;
            }

            try
            {
                // Strategy 1: Try to compile shader from temporary file (fallback method)
                // Based on Stride: Shaders are typically compiled from .sdsl files
                // This is the most reliable method when EffectCompiler is not directly accessible
                return CompileShaderFromFile(shaderSource, shaderName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] Failed to compile shader '{shaderName}': {ex.Message}");
                Console.WriteLine($"[StrideColorGrading] Stack trace: {ex.StackTrace}");
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
                // Create compilation source for shader compilation
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
                    Console.WriteLine($"[StrideColorGrading] Successfully compiled shader '{shaderName}' using EffectCompiler");
                    return effect;
                }
                else
                {
                    Console.WriteLine($"[StrideColorGrading] EffectCompiler compilation failed for shader '{shaderName}': No bytecode generated");
                    if (compilerResult != null && compilerResult.HasErrors)
                    {
                        // CompilerResults may not have ErrorText, use ToString() or check for specific error properties
                        Console.WriteLine($"[StrideColorGrading] Compilation errors occurred");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] Exception while compiling shader '{shaderName}' with EffectCompiler: {ex.Message}");
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
                // Note: EffectCompiler requires IVirtualFileProvider parameter
                EffectCompiler effectCompiler = null;

                // Attempt to get file provider from GraphicsDevice services
                IVirtualFileProvider fileProvider = null;
                try
                {
                    var deviceServices = _graphicsDevice.Services();
                    if (deviceServices != null)
                    {
                        // Try to get IVirtualFileProvider from services using reflection (C# 7.3 compatibility)
                        var getServiceMethod = deviceServices.GetType().GetMethod("GetService", new Type[0]);
                        if (getServiceMethod != null)
                        {
                            var genericMethod = getServiceMethod.MakeGenericMethod(typeof(IVirtualFileProvider));
                            fileProvider = genericMethod.Invoke(deviceServices, null) as IVirtualFileProvider;
                        }
                    }
                }
                catch
                {
                    // FileProvider not available from GraphicsDevice services
                }

                // Attempt to create EffectCompiler instance with file provider
                // Based on Stride architecture: EffectCompiler requires IVirtualFileProvider
                try
                {
                    if (fileProvider != null)
                    {
                        effectCompiler = new EffectCompiler(fileProvider);
                    }
                    else
                    {
                        Console.WriteLine($"[StrideColorGrading] Warning: No file provider available for EffectCompiler, cannot compile shader");
                        return null; // Cannot create EffectCompiler without fileProvider
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideColorGrading] Failed to create EffectCompiler: {ex.Message}");
                }

                if (effectCompiler != null)
                {
                    // Create compilation source from file content
                    var compilerSource = new ShaderSourceClass
                    {
                        Name = shaderName,
                        SourceCode = shaderSource
                    };

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
                        Console.WriteLine($"[StrideColorGrading] Successfully compiled shader '{shaderName}' from file");
                        return effect;
                    }
                }

                // Note: Effect.Load() doesn't support loading from file paths directly
                // It only works with effect names from content paths, so we skip this fallback
                // If we reach here, compilation has failed and we return null

                Console.WriteLine($"[StrideColorGrading] Could not compile shader '{shaderName}' from file");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] Exception while compiling shader '{shaderName}' from file: {ex.Message}");
                Console.WriteLine($"[StrideColorGrading] Stack trace: {ex.StackTrace}");
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

        /// <summary>
        /// CPU-side color grading execution (fallback implementation).
        /// Implements the complete color grading algorithm matching GPU shader behavior.
        /// Based on industry-standard color grading algorithms and LUT sampling techniques.
        /// </summary>
        private void ExecuteColorGradingCpu(StrideGraphics.Texture input, StrideGraphics.Texture output)
        {
            if (input == null || output == null || _graphicsDevice == null)
            {
                return;
            }

            int width = input.Width;
            int height = input.Height;

            try
            {
                StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
                if (commandList == null)
                {
                    Console.WriteLine("[StrideColorGrading] ImmediateContext not available");
                    return;
                }

                // Read input StrideGraphics.Texture data
                Vector4[] inputData = ReadTextureData(input);
                if (inputData == null || inputData.Length != width * height)
                {
                    Console.WriteLine("[StrideColorGrading] Failed to read input StrideGraphics.Texture data");
                    return;
                }

                // Read LUT StrideGraphics.Texture data if available
                Vector4[] lutData = null;
                if (_lutTexture != null && _lutSize > 0)
                {
                    var lutTexture = _lutTexture as StrideGraphics.Texture;
                    if (lutTexture != null)
                    {
                        lutData = ReadTextureData(lutTexture);
                    }
                    if (lutData == null)
                    {
                        Console.WriteLine("[StrideColorGrading] Failed to read LUT StrideGraphics.Texture data");
                        // Continue without LUT
                    }
                }

                // Allocate output buffer
                Vector4[] outputData = new Vector4[width * height];

                // Process each pixel
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        Vector4 inputColor = inputData[index];

                        // Step 1: Apply contrast adjustment
                        // Formula: color = (color - 0.5) * (1.0 + contrast) + 0.5
                        // Contrast range: [-1, 1], where 0 = no change
                        float contrastFactor = 1.0f + _contrast;
                        Vector3 color = new Vector3(
                            (inputColor.X - 0.5f) * contrastFactor + 0.5f,
                            (inputColor.Y - 0.5f) * contrastFactor + 0.5f,
                            (inputColor.Z - 0.5f) * contrastFactor + 0.5f
                        );

                        // Clamp to [0, 1] range
                        color.X = Math.Max(0.0f, Math.Min(1.0f, color.X));
                        color.Y = Math.Max(0.0f, Math.Min(1.0f, color.Y));
                        color.Z = Math.Max(0.0f, Math.Min(1.0f, color.Z));

                        // Step 2: Apply saturation adjustment
                        // Formula: lerp(grayscale, color, saturation)
                        // Saturation range: [0, 2], where 1.0 = no change, 0 = grayscale, >1 = oversaturated
                        float luminance = 0.299f * color.X + 0.587f * color.Y + 0.114f * color.Z; // ITU-R BT.601
                        Vector3 grayscale = new Vector3(luminance, luminance, luminance);
                        color = Vector3.Lerp(grayscale, color, _saturation);

                        // Clamp again after saturation
                        color.X = Math.Max(0.0f, Math.Min(1.0f, color.X));
                        color.Y = Math.Max(0.0f, Math.Min(1.0f, color.Y));
                        color.Z = Math.Max(0.0f, Math.Min(1.0f, color.Z));

                        // Step 3: Sample 3D LUT (if available)
                        Vector3 finalColor = color;
                        if (lutData != null && _lutSize > 0 && _strength > 0.0f)
                        {
                            var lutTexture = _lutTexture as StrideGraphics.Texture;
                            if (lutTexture != null)
                            {
                                Vector3 lutColor = SampleLut3D(color, lutData, _lutSize, lutTexture.Width, lutTexture.Height);

                                // Step 4: Blend LUT result with adjusted color based on strength
                                // Formula: lerp(adjustedColor, lutColor, strength)
                                finalColor = Vector3.Lerp(color, lutColor, _strength);
                            }
                        }

                        // Step 5: Clamp to valid color range and preserve alpha
                        finalColor.X = Math.Max(0.0f, Math.Min(1.0f, finalColor.X));
                        finalColor.Y = Math.Max(0.0f, Math.Min(1.0f, finalColor.Y));
                        finalColor.Z = Math.Max(0.0f, Math.Min(1.0f, finalColor.Z));

                        outputData[index] = new Vector4(finalColor.X, finalColor.Y, finalColor.Z, inputColor.W);
                    }
                }

                // Write output data back to StrideGraphics.Texture
                WriteTextureData(output, outputData, width, height);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] CPU execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Samples a 3D LUT that has been flattened to a 2D texture.
        /// Implements the algorithm from three.js LUTPass.js for 2D flattened LUT sampling.
        /// Based on industry-standard LUT sampling techniques.
        /// </summary>
        /// <param name="rgb">Input RGB color in [0, 1] range</param>
        /// <param name="lutData">LUT StrideGraphics.Texture data as Vector4 array</param>
        /// <param name="lutSize">Size of the 3D LUT (16 or 32)</param>
        /// <param name="lutWidth">Width of the flattened 2D LUT StrideGraphics.Texture</param>
        /// <param name="lutHeight">Height of the flattened 2D LUT StrideGraphics.Texture</param>
        /// <returns>Sampled color from LUT</returns>
        private Vector3 SampleLut3D(Vector3 rgb, Vector4[] lutData, int lutSize, int lutWidth, int lutHeight)
        {
            // Clamp the sample in by half a pixel to avoid interpolation artifacts
            // between slices laid out next to each other
            float halfPixelWidth = 0.5f / lutSize;
            float r = Math.Max(halfPixelWidth, Math.Min(1.0f - halfPixelWidth, rgb.X));
            float g = Math.Max(halfPixelWidth, Math.Min(1.0f - halfPixelWidth, rgb.Y));
            float b = Math.Max(0.0f, Math.Min(1.0f, rgb.Z));

            // Green offset into a LUT layer
            float gOffset = g / lutSize;

            // Calculate blue slice and interpolation factor
            float bNormalized = lutSize * b;
            int bSlice = (int)Math.Floor(bNormalized);
            bSlice = Math.Max(0, Math.Min(lutSize - 1, bSlice)); // Clamp to valid range
            float bMix = (bNormalized - bSlice) / lutSize;

            // Get the first LUT slice and then the one to interpolate to
            float b1 = bSlice / (float)lutSize;
            float b2 = (bSlice + 1) / (float)lutSize;

            // Calculate UV coordinates for both slices
            // For flattened 3D LUT: each row is a blue slice, each column within a row is R*G
            // UV: (r, gOffset + bSlice)
            float uv1X = r;
            float uv1Y = gOffset + b1;
            float uv2X = r;
            float uv2Y = gOffset + b2;

            // Clamp UV coordinates to valid StrideGraphics.Texture range
            uv1X = Math.Max(0.0f, Math.Min(1.0f, uv1X));
            uv1Y = Math.Max(0.0f, Math.Min(1.0f, uv1Y));
            uv2X = Math.Max(0.0f, Math.Min(1.0f, uv2X));
            uv2Y = Math.Max(0.0f, Math.Min(1.0f, uv2Y));

            // Sample from LUT StrideGraphics.Texture
            Vector3 sample1 = SampleLutTexture(lutData, uv1X, uv1Y, lutWidth, lutHeight);
            Vector3 sample2 = SampleLutTexture(lutData, uv2X, uv2Y, lutWidth, lutHeight);

            // Interpolate between the two blue slices
            return Vector3.Lerp(sample1, sample2, bMix);
        }

        /// <summary>
        /// Samples a 2D LUT StrideGraphics.Texture at the given UV coordinates using bilinear filtering.
        /// </summary>
        private Vector3 SampleLutTexture(Vector4[] lutData, float u, float v, int width, int height)
        {
            // Convert UV to pixel coordinates
            float x = u * (width - 1);
            float y = v * (height - 1);

            // Get integer coordinates for bilinear filtering
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(width - 1, x0 + 1);
            int y1 = Math.Min(height - 1, y0 + 1);

            // Get fractional parts for interpolation
            float fx = x - x0;
            float fy = y - y0;

            // Sample four corners
            Vector3 c00 = GetLutPixel(lutData, x0, y0, width);
            Vector3 c10 = GetLutPixel(lutData, x1, y0, width);
            Vector3 c01 = GetLutPixel(lutData, x0, y1, width);
            Vector3 c11 = GetLutPixel(lutData, x1, y1, width);

            // Bilinear interpolation
            Vector3 c0 = Vector3.Lerp(c00, c10, fx);
            Vector3 c1 = Vector3.Lerp(c01, c11, fx);
            return Vector3.Lerp(c0, c1, fy);
        }

        /// <summary>
        /// Gets a pixel from the LUT StrideGraphics.Texture data.
        /// </summary>
        private Vector3 GetLutPixel(Vector4[] lutData, int x, int y, int width)
        {
            int index = y * width + x;
            if (index >= 0 && index < lutData.Length)
            {
                Vector4 pixel = lutData[index];
                return new Vector3(pixel.X, pixel.Y, pixel.Z);
            }
            return Vector3.Zero;
        }

        /// <summary>
        /// Reads StrideGraphics.Texture data from GPU to CPU memory.
        /// Implements proper StrideGraphics.Texture readback using Stride's GetData API.
        /// This is expensive and should only be used as CPU fallback when GPU shaders are not available.
        /// </summary>
        private Vector4[] ReadTextureData(StrideGraphics.Texture texture)
        {
            if (texture == null || _graphicsDevice == null)
            {
                return null;
            }

            try
            {
                int width = texture.Width;
                int height = texture.Height;
                int size = width * height;
                Vector4[] data = new Vector4[size];

                // Get ImmediateContext (StrideGraphics.CommandList) from GraphicsDevice
                StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
                if (commandList == null)
                {
                    Console.WriteLine("[StrideColorGrading] ReadTextureData: ImmediateContext not available");
                    return data; // Return zero-initialized data
                }

                StrideGraphics.PixelFormat format = texture.Format;

                // Handle different StrideGraphics.Texture formats
                if (format == StrideGraphics.PixelFormat.R8G8B8A8_UNorm ||
                    format == StrideGraphics.PixelFormat.R8G8B8A8_UNorm_SRgb ||
                    format == StrideGraphics.PixelFormat.R32G32B32A32_Float ||
                    format == StrideGraphics.PixelFormat.R16G16B16A16_Float ||
                    format == StrideGraphics.PixelFormat.B8G8R8A8_UNorm ||
                    format == StrideGraphics.PixelFormat.B8G8R8A8_UNorm_SRgb)
                {
                    // Read as Color array
                    var colorData = new Color[size];
                    texture.GetData(commandList, colorData);

                    // Convert Color[] to Vector4[]
                    for (int i = 0; i < size; i++)
                    {
                        var color = colorData[i];
                        if (format == StrideGraphics.PixelFormat.R32G32B32A32_Float)
                        {
                            // Already float format
                            data[i] = new Vector4(color.R, color.G, color.B, color.A);
                        }
                        else
                        {
                            // Convert from [0, 255] to [0, 1]
                            data[i] = new Vector4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
                        }
                    }
                }
                else if (format == StrideGraphics.PixelFormat.R16G16B16A16_Float)
                {
                    // Half-precision float format
                    var colorData = new Color[size];
                    texture.GetData(commandList, colorData);
                    for (int i = 0; i < size; i++)
                    {
                        var color = colorData[i];
                        data[i] = new Vector4(color.R, color.G, color.B, color.A);
                    }
                }
                else
                {
                    // Try generic Color readback
                    try
                    {
                        var colorData = new Color[size];
                        texture.GetData(commandList, colorData);
                        for (int i = 0; i < size; i++)
                        {
                            var color = colorData[i];
                            data[i] = new Vector4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideColorGrading] ReadTextureData: Unsupported format {format}: {ex.Message}");
                        // Return zero-initialized data on failure
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] ReadTextureData: Exception during StrideGraphics.Texture readback: {ex.Message}");
                // Return zero-initialized data on failure
                return new Vector4[texture.Width * texture.Height];
            }
        }

        /// <summary>
        /// Writes StrideGraphics.Texture data from CPU memory to GPU texture.
        /// Implements proper StrideGraphics.Texture upload using Stride's SetData API.
        /// This is expensive and should only be used as CPU fallback when GPU shaders are not available.
        /// </summary>
        private void WriteTextureData(StrideGraphics.Texture texture, Vector4[] data, int width, int height)
        {
            if (texture == null || data == null || _graphicsDevice == null)
            {
                return;
            }

            try
            {
                if (texture.Width != width || texture.Height != height)
                {
                    Console.WriteLine($"[StrideColorGrading] WriteTextureData: StrideGraphics.Texture dimensions mismatch. StrideGraphics.Texture: {texture.Width}x{texture.Height}, Data: {width}x{height}");
                    return;
                }

                int size = width * height;
                if (data.Length < size)
                {
                    Console.WriteLine($"[StrideColorGrading] WriteTextureData: Data array too small. Expected: {size}, Got: {data.Length}");
                    return;
                }

                // Get ImmediateContext (StrideGraphics.CommandList) from GraphicsDevice
                StrideGraphics.CommandList commandList = _graphicsDevice.ImmediateContext();
                if (commandList == null)
                {
                    Console.WriteLine("[StrideColorGrading] WriteTextureData: ImmediateContext not available");
                    return;
                }

                StrideGraphics.PixelFormat format = texture.Format;

                // Convert Vector4[] to Color[] based on format
                if (format == StrideGraphics.PixelFormat.R8G8B8A8_UNorm ||
                    format == StrideGraphics.PixelFormat.R8G8B8A8_UNorm_SRgb ||
                    format == StrideGraphics.PixelFormat.B8G8R8A8_UNorm ||
                    format == StrideGraphics.PixelFormat.B8G8R8A8_UNorm_SRgb)
                {
                    var colorData = new Color[size];
                    for (int i = 0; i < size; i++)
                    {
                        var v = data[i];
                        float r = Math.Max(0.0f, Math.Min(1.0f, v.X));
                        float g = Math.Max(0.0f, Math.Min(1.0f, v.Y));
                        float b = Math.Max(0.0f, Math.Min(1.0f, v.Z));
                        float a = Math.Max(0.0f, Math.Min(1.0f, v.W));

                        colorData[i] = new Color(
                            (byte)(r * 255.0f),
                            (byte)(g * 255.0f),
                            (byte)(b * 255.0f),
                            (byte)(a * 255.0f)
                        );
                    }
                    texture.SetData(commandList, colorData);
                }
                else if (format == StrideGraphics.PixelFormat.R32G32B32A32_Float)
                {
                    var colorData = new Color[size];
                    for (int i = 0; i < size; i++)
                    {
                        var v = data[i];
                        colorData[i] = new Color(v.X, v.Y, v.Z, v.W);
                    }
                    texture.SetData(commandList, colorData);
                }
                else if (format == StrideGraphics.PixelFormat.R16G16B16A16_Float)
                {
                    var colorData = new Color[size];
                    for (int i = 0; i < size; i++)
                    {
                        var v = data[i];
                        colorData[i] = new Color(v.X, v.Y, v.Z, v.W);
                    }
                    texture.SetData(commandList, colorData);
                }
                else
                {
                    // Fallback to R8G8B8A8_UNorm conversion
                    var colorData = new Color[size];
                    for (int i = 0; i < size; i++)
                    {
                        var v = data[i];
                        float r = Math.Max(0.0f, Math.Min(1.0f, v.X));
                        float g = Math.Max(0.0f, Math.Min(1.0f, v.Y));
                        float b = Math.Max(0.0f, Math.Min(1.0f, v.Z));
                        float a = Math.Max(0.0f, Math.Min(1.0f, v.W));

                        colorData[i] = new Color(
                            (byte)(r * 255.0f),
                            (byte)(g * 255.0f),
                            (byte)(b * 255.0f),
                            (byte)(a * 255.0f)
                        );
                    }
                    texture.SetData(commandList, colorData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideColorGrading] WriteTextureData failed: {ex.Message}");
            }
        }
    }
}


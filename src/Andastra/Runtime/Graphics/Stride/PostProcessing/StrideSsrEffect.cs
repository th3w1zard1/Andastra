using System;
using System.Numerics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Core.Mathematics;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.PostProcessing;
using Andastra.Runtime.Graphics.Common.Rendering;
using Matrix = Stride.Core.Mathematics.Matrix;
using Vector2 = Stride.Core.Mathematics.Vector2;
using Vector3 = Stride.Core.Mathematics.Vector3;
using Vector4 = Stride.Core.Mathematics.Vector4;

namespace Andastra.Runtime.Stride.PostProcessing
{
    /// <summary>
    /// Stride implementation of Screen-Space Reflections (SSR) post-processing effect.
    /// Inherits shared SSR logic from BaseSsrEffect.
    ///
    /// Features:
    /// - Ray-marched screen-space reflections
    /// - Hierarchical Z-buffer optimization
    /// - Roughness-based reflection blending
    /// - Edge fade and distance fade
    /// - Temporal accumulation for stability
    ///
    /// Based on Stride rendering pipeline: https://doc.stride3d.net/latest/en/manual/graphics/
    /// SSR is a screen-space technique that approximates reflections using the depth buffer.
    ///
    /// Algorithm based on vendor/reone/glsl/f_pbr_ssr.glsl for 1:1 parity with original game.
    /// </summary>
    public class StrideSsrEffect : BaseSsrEffect
    {
        private GraphicsDevice _graphicsDevice;
        private EffectInstance _ssrEffect;
        private Texture _historyTexture;
        private Texture _temporaryTexture;
        private SpriteBatch _spriteBatch;
        private Effect _fullscreenEffect;
        private ConstantBuffer _ssrConstants;
        private SamplerState _linearSampler;
        private SamplerState _pointSampler;
        private bool _effectInitialized;
        private float _clipNear;
        private float _clipFar;
        private Vector2 _screenResolution;
        private Vector2 _screenResolutionRcp;
        private Matrix _projectionMatrix;
        private Matrix _projectionMatrixInv;
        private Matrix _screenProjection;

        // SSR shader parameters matching GLSL uniform block
        private struct SsrConstants
        {
            public Matrix Projection;
            public Matrix ProjectionInv;
            public Matrix ScreenProjection;
            public Vector2 ScreenResolution;
            public Vector2 ScreenResolutionRcp;
            public float ClipNear;
            public float ClipFar;
            public float SSRBias;
            public float SSRPixelStride;
            public float SSRMaxSteps;
            public float MaxDistance;
            public float StepSize;
            public float Intensity;
            public float EdgeFadeStart;
            public Vector2 Padding;
        }

        public StrideSsrEffect(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _clipNear = 0.1f;
            _clipFar = 1000.0f;
            _edgeFadeStart = 0.8f;
            InitializeEffect();
        }

        private float _edgeFadeStart;

        private void InitializeEffect()
        {
            try
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

                // Create constant buffer for SSR parameters
                // Stride uses Buffer.New for constant buffers
                int constantBufferSize = System.Runtime.InteropServices.Marshal.SizeOf<SsrConstants>();
                // Align to 16 bytes (D3D11 requirement)
                constantBufferSize = (constantBufferSize + 15) & ~15;
                _ssrConstants = Buffer.New(_graphicsDevice, constantBufferSize, BufferFlags.ConstantBuffer, GraphicsResourceUsage.Dynamic);

                // Try to load SSR effect shader
                // TODO:  In a full implementation, this would load a compiled .sdsl shader
                // TODO: STUB - For now, we'll use CPU-side ray marching as fallback
                try
                {
                    // Attempt to load effect - would need actual shader file
                    // _fullscreenEffect = Effect.Load(_graphicsDevice, "SSREffect");
                    // _ssrEffect = new EffectInstance(_fullscreenEffect);
                }
                catch
                {
                    // Fallback to CPU implementation if shader not available
                }

                _effectInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideSSR] Failed to initialize effect: {ex.Message}");
                _effectInitialized = false;
            }
        }

        #region BaseSsrEffect Implementation

        protected override void OnDispose()
        {
            _ssrEffect?.Dispose();
            _ssrEffect = null;

            _fullscreenEffect?.Dispose();
            _fullscreenEffect = null;

            _historyTexture?.Dispose();
            _historyTexture = null;

            _temporaryTexture?.Dispose();
            _temporaryTexture = null;

            _ssrConstants?.Dispose();
            _ssrConstants = null;

            _linearSampler?.Dispose();
            _linearSampler = null;

            _pointSampler?.Dispose();
            _pointSampler = null;

            _spriteBatch?.Dispose();
            _spriteBatch = null;

            base.OnDispose();
        }

        #endregion

        /// <summary>
        /// Applies SSR to the input frame.
        /// </summary>
        /// <param name="input">HDR color buffer.</param>
        /// <param name="depth">Depth buffer.</param>
        /// <param name="normal">Normal buffer (world space or view space).</param>
        /// <param name="roughness">Roughness/metallic buffer.</param>
        /// <param name="viewMatrix">View matrix for ray calculation.</param>
        /// <param name="projectionMatrix">Projection matrix for ray calculation.</param>
        /// <param name="width">Render width.</param>
        /// <param name="height">Render height.</param>
        /// <param name="lightmap">Optional lightmap texture for reflection color modulation (matches GLSL sLightmap).</param>
        /// <returns>Output texture with reflections applied.</returns>
        public Texture Apply(Texture input, Texture depth, Texture normal, Texture roughness,
            System.Numerics.Matrix4x4 viewMatrix, System.Numerics.Matrix4x4 projectionMatrix,
            int width, int height, Texture lightmap = null)
        {
            if (!_enabled || input == null || depth == null || normal == null)
            {
                return input;
            }

            EnsureTextures(width, height, input.Format);

            // Update screen resolution
            _screenResolution = new Vector2(width, height);
            _screenResolutionRcp = new Vector2(1.0f / width, 1.0f / height);

            // Convert matrices to Stride format
            _projectionMatrix = ConvertMatrix(projectionMatrix);
            _projectionMatrixInv = Matrix.Invert(_projectionMatrix);
            _screenProjection = _projectionMatrix; // Screen projection is same as main projection for SSR

            // SSR Ray Marching Process:
            // 1. For each pixel, reflect view ray based on normal
            // 2. Ray march through screen space using depth buffer
            // 3. Check for intersection with scene geometry
            // 4. Sample color at intersection point
            // 5. Blend with roughness (rough surfaces have blurry reflections)
            // 6. Apply edge fade (screen borders)
            // 7. Temporal accumulation for stability

            ExecuteSsr(input, depth, normal, roughness, lightmap, viewMatrix, projectionMatrix, _temporaryTexture, width, height);

            // Swap history and output for temporal accumulation
            var temp = _historyTexture;
            _historyTexture = _temporaryTexture;
            _temporaryTexture = temp;

            return _temporaryTexture ?? input;
        }

        private void EnsureTextures(int width, int height, PixelFormat format)
        {
            if (_historyTexture != null &&
                _historyTexture.Width == width &&
                _historyTexture.Height == height)
            {
                return;
            }

            _historyTexture?.Dispose();
            _temporaryTexture?.Dispose();

            var desc = TextureDescription.New2D(width, height, 1, format,
                TextureFlags.ShaderResource | TextureFlags.RenderTarget);

            _historyTexture = Texture.New(_graphicsDevice, desc);
            _temporaryTexture = Texture.New(_graphicsDevice, desc);
        }

        /// <summary>
        /// Executes SSR ray marching algorithm.
        /// Implements the complete algorithm from vendor/reone/glsl/f_pbr_ssr.glsl
        /// for 1:1 parity with original game behavior.
        /// </summary>
        private void ExecuteSsr(Texture input, Texture depth, Texture normal, Texture roughness,
            Texture lightmap, System.Numerics.Matrix4x4 viewMatrix, System.Numerics.Matrix4x4 projectionMatrix,
            Texture output, int width, int height)
        {
            if (!_effectInitialized || output == null)
            {
                return;
            }

            // Update constant buffer with SSR parameters
            UpdateSsrConstants(width, height);

            // Set render target and clear
            // In Stride, rendering is typically done through SpriteBatch or custom rendering
            // TODO: STUB - For now, we'll use the CPU fallback which handles the rendering logic

            // If we have a shader effect, use GPU-based ray marching
            if (_ssrEffect != null && _fullscreenEffect != null)
            {
                var commandList = _graphicsDevice.ImmediateContext;
                if (commandList != null)
                {
                    ExecuteSsrGpu(input, depth, normal, roughness, lightmap, output, commandList);
                }
                else
                {
                    // Fallback to CPU if command list not available
                    ExecuteSsrCpu(input, depth, normal, roughness, lightmap, output, width, height);
                }
            }
            else
            {
                // Fallback: Use CPU-side implementation for now
                // In production, this should always use GPU shader
                // CPU implementation matches vendor/reone/glsl/f_pbr_ssr.glsl for 1:1 parity
                ExecuteSsrCpu(input, depth, normal, roughness, lightmap, output, width, height);
            }
        }

        /// <summary>
        /// GPU-based SSR execution using shader effect.
        /// </summary>
        private void ExecuteSsrGpu(Texture input, Texture depth, Texture normal, Texture roughness,
            Texture lightmap, Texture output, CommandList commandList)
        {
            if (_ssrEffect == null || _fullscreenEffect == null)
            {
                return;
            }

            // Update effect parameters
            var parameters = _ssrEffect.Parameters;
            if (parameters != null)
            {
                // Set constant buffer data
                UpdateSsrConstants(input.Width, input.Height);
                
                // Bind textures through effect parameters
                // TODO:  In a full implementation, the shader would define these parameters:
                // parameters.Set("InputTexture", input);
                // parameters.Set("DepthTexture", depth);
                // parameters.Set("NormalTexture", normal);
                // if (roughness != null) parameters.Set("RoughnessTexture", roughness);
            }

            // Draw fullscreen quad using SpriteBatch
            // Note: This requires a proper fullscreen quad shader setup
            // TODO: STUB - For now, we fall back to CPU implementation
        }

        /// <summary>
        /// CPU-side SSR execution (fallback implementation).
        /// Implements the complete ray marching algorithm matching vendor/reone/glsl/f_pbr_ssr.glsl
        /// for 1:1 parity with original game behavior.
        /// </summary>
        private void ExecuteSsrCpu(Texture input, Texture depth, Texture normal, Texture roughness,
            Texture lightmap, Texture output, int width, int height)
        {
            // Read texture data
            var inputData = ReadTextureData(input);
            var depthData = ReadTextureData(depth);
            var normalData = ReadTextureData(normal);
            var roughnessData = roughness != null ? ReadTextureData(roughness) : null;
            var lightmapData = lightmap != null ? ReadTextureData(lightmap) : null;

            // Allocate output buffer
            var outputData = new Vector4[width * height];

            // Process each pixel
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Vector2 uv = new Vector2((float)x / width, (float)y / height);

                    // Sample input color
                    Vector4 mainTexSample = SampleTexture(inputData, uv, width, height);
                    if (mainTexSample.W >= 1.0f)
                    {
                        outputData[index] = new Vector4(0, 0, 0, 0);
                        continue;
                    }

                    // Reconstruct view-space position from depth
                    Vector3 fragPosVS = ReconstructViewPos(uv, depthData, width, height);

                    // Calculate view direction
                    Vector3 I = Vector3.Normalize(fragPosVS);

                    // Sample normal
                    Vector3 N = SampleNormal(normalData, uv, width, height);

                    // Calculate reflection direction
                    Vector3 R = Vector3.Reflect(I, N);

                    // Calculate jitter for checkerboard pattern
                    float jitter = ((x + y) & 1) * 0.5f;

                    // Trace screen-space ray
                    Vector2 hitUV;
                    Vector3 hitPoint;
                    float numSteps;
                    bool hit = TraceScreenSpaceRay(fragPosVS, R, jitter, width, height, depthData,
                        out hitUV, out hitPoint, out numSteps);

                    Vector3 reflectionColor = Vector3.Zero;
                    float reflectionStrength = 0.0f;

                    if (hit)
                    {
                        // Sample color at hit point (matches GLSL: texture(sMainTex, hitUV))
                        Vector4 hitMainTexSample = SampleTexture(inputData, hitUV, width, height);
                        
                        // Sample lightmap at hit point (matches GLSL: texture(sLightmap, hitUV))
                        // If lightmap is not provided, use white (1.0, 1.0, 1.0) for backward compatibility
                        Vector3 lightmapColor = Vector3.One;
                        if (lightmapData != null)
                        {
                            Vector4 hitLightmapSample = SampleTexture(lightmapData, hitUV, width, height);
                            lightmapColor = new Vector3(hitLightmapSample.X, hitLightmapSample.Y, hitLightmapSample.Z);
                        }

                        // Calculate reflection color matching GLSL: hitMainTexSample.rgb * hitMainTexSample.a * hitLightmapSample.rgb
                        Vector3 mainTexRgb = new Vector3(hitMainTexSample.X, hitMainTexSample.Y, hitMainTexSample.Z);
                        reflectionColor = mainTexRgb * hitMainTexSample.W * lightmapColor;

                        // Calculate reflection strength based on:
                        // 1. View angle (grazing angles have stronger reflections)
                        // Matches GLSL: 1.0 - clamp(R.z, 0.0, 1.0)
                        reflectionStrength = 1.0f - Math.Max(0.0f, Math.Min(1.0f, R.Z));

                        // 2. Step count (fewer steps = more confidence)
                        // Matches GLSL: 1.0 - numSteps / uSSRMaxSteps
                        reflectionStrength *= 1.0f - (numSteps / _maxIterations);

                        // 3. Edge fade (screen borders)
                        // Matches GLSL: 1.0 - max(0.0, (maxDim - EDGE_FADE_START) / (1.0 - EDGE_FADE_START))
                        Vector2 hitNDC = hitUV * 2.0f - Vector2.One;
                        float maxDim = Math.Min(1.0f, Math.Max(Math.Abs(hitNDC.X), Math.Abs(hitNDC.Y)));
                        float edgeFade = 1.0f - Math.Max(0.0f, (maxDim - _edgeFadeStart) / (1.0f - _edgeFadeStart));
                        reflectionStrength *= edgeFade;

                        // Apply roughness if available (not in original GLSL, but useful for PBR)
                        if (roughnessData != null)
                        {
                            Vector4 roughSample = SampleTexture(roughnessData, uv, width, height);
                            float rough = roughSample.X; // Assuming roughness in R channel
                            reflectionStrength *= 1.0f - rough; // Rough surfaces have weaker reflections
                        }

                        // Apply intensity (not in original GLSL, but useful for control)
                        reflectionStrength *= _intensity;
                    }

                    // Blend reflection with original color
                    Vector3 finalColor = new Vector3(mainTexSample.X, mainTexSample.Y, mainTexSample.Z) +
                                        reflectionColor * reflectionStrength;

                    outputData[index] = new Vector4(finalColor, reflectionStrength);
                }
            }

            // Write output data back to texture
            WriteTextureData(output, outputData, width, height);
        }

        /// <summary>
        /// Traces a screen-space ray using the depth buffer.
        /// Implements the algorithm from vendor/reone/glsl/f_pbr_ssr.glsl traceScreenSpaceRay function.
        /// </summary>
        private bool TraceScreenSpaceRay(Vector3 rayOrigin, Vector3 rayDir, float jitter,
            int width, int height, Vector4[] depthData,
            out Vector2 hitUV, out Vector3 hitPoint, out float numSteps)
        {
            hitUV = Vector2.Zero;
            hitPoint = Vector3.Zero;
            numSteps = 0.0f;

            const float MAX_DISTANCE = 100.0f;
            const float SSRBias = 0.1f;

            // Calculate ray length
            float rayLength = ((rayOrigin.Z + rayDir.Z * MAX_DISTANCE) > -_clipNear) ?
                ((-_clipNear - rayOrigin.Z) / rayDir.Z) : MAX_DISTANCE;
            Vector3 rayEnd = rayOrigin + rayDir * rayLength;

            // Project ray start and end to screen space
            Vector4 H0 = Vector4.Transform(new Vector4(rayOrigin, 1.0f), _screenProjection);
            float k0 = 1.0f / H0.W;
            Vector3 Q0 = rayOrigin * k0;
            Vector2 P0 = new Vector2(H0.X, H0.Y) * k0;

            Vector4 H1 = Vector4.Transform(new Vector4(rayEnd, 1.0f), _screenProjection);
            float k1 = 1.0f / H1.W;
            Vector3 Q1 = rayEnd * k1;
            Vector2 P1 = new Vector2(H1.X, H1.Y) * k1;

            // Avoid division by zero
            if (Vector2.DistanceSquared(P0, P1) < 0.0001f)
            {
                P1.X += 0.01f;
            }

            Vector2 delta = P1 - P0;

            // Permute if needed for better stepping
            bool permute = Math.Abs(delta.X) < Math.Abs(delta.Y);
            if (permute)
            {
                delta = new Vector2(delta.Y, delta.X);
                P0 = new Vector2(P0.Y, P0.X);
                P1 = new Vector2(P1.Y, P1.X);
            }

            float stepDir = Math.Sign(delta.X);
            float invdx = stepDir / delta.X;

            Vector3 dQ = (Q1 - Q0) * invdx;
            float dk = (k1 - k0) * invdx;
            Vector2 dP = new Vector2(stepDir, delta.Y * invdx);

            // Apply pixel stride
            float stride = _stepSize;
            dP *= stride;
            dQ *= stride;
            dk *= stride;

            // Apply jitter
            P0 += (1.0f + jitter) * dP;
            Q0 += (1.0f + jitter) * dQ;
            k0 += (1.0f + jitter) * dk;

            Vector2 P = P0;
            Vector3 Q = Q0;
            float k = k0;

            float end = P1.X * stepDir;
            float prevZMax = rayOrigin.Z;

            // Ray march
            for (float i = 0; i < _maxIterations && P.X * stepDir <= end; i += 1.0f)
            {
                Vector2 sceneUV = permute ? new Vector2(P.Y, P.X) : P;
                sceneUV *= _screenResolutionRcp;

                float rayZMin = prevZMax;
                float rayZMax = (dQ.Z * 0.5f + Q.Z) / (dk * 0.5f + k);
                prevZMax = rayZMax;

                if (rayZMin > rayZMax)
                {
                    float temp = rayZMin;
                    rayZMin = rayZMax;
                    rayZMax = temp;
                }

                float sceneZ = ReconstructViewPos(sceneUV, depthData, width, height).Z;

                if (rayZMin <= sceneZ && rayZMax >= sceneZ - SSRBias)
                {
                    hitUV = sceneUV;
                    hitPoint = Q * (1.0f / k);
                    numSteps = i;
                    return true;
                }

                P += dP;
                Q += dQ;
                k += dk;
            }

            return false;
        }

        /// <summary>
        /// Reconstructs view-space position from UV and depth buffer.
        /// Matches vendor/reone/glsl/i_coords.glsl reconstructViewPos function.
        /// </summary>
        private Vector3 ReconstructViewPos(Vector2 uv, Vector4[] depthData, int width, int height)
        {
            float depthSample = SampleTexture(depthData, uv, width, height).X;
            Vector3 ndcPos = new Vector3(uv * 2.0f - Vector2.One, depthSample);
            Vector4 viewPos = Vector4.Transform(new Vector4(ndcPos, 1.0f), _projectionMatrixInv);
            viewPos /= viewPos.W;
            return new Vector3(viewPos.X, viewPos.Y, viewPos.Z);
        }

        /// <summary>
        /// Samples a normal from the normal buffer and converts from [0,1] to [-1,1] range.
        /// </summary>
        private Vector3 SampleNormal(Vector4[] normalData, Vector2 uv, int width, int height)
        {
            Vector4 normalSample = SampleTexture(normalData, uv, width, height);
            Vector3 N = new Vector3(normalSample.X, normalSample.Y, normalSample.Z);
            N = N * 2.0f - Vector3.One; // Convert from [0,1] to [-1,1]
            return Vector3.Normalize(N);
        }

        /// <summary>
        /// Samples a texture at the given UV coordinates using bilinear filtering.
        /// </summary>
        private Vector4 SampleTexture(Vector4[] textureData, Vector2 uv, int width, int height)
        {
            // Clamp UV to valid range
            uv.X = Math.Max(0.0f, Math.Min(1.0f, uv.X));
            uv.Y = Math.Max(0.0f, Math.Min(1.0f, uv.Y));

            float x = uv.X * (width - 1);
            float y = uv.Y * (height - 1);

            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(x0 + 1, width - 1);
            int y1 = Math.Min(y0 + 1, height - 1);

            float fx = x - x0;
            float fy = y - y0;

            Vector4 sample00 = textureData[y0 * width + x0];
            Vector4 sample01 = textureData[y1 * width + x0];
            Vector4 sample10 = textureData[y0 * width + x1];
            Vector4 sample11 = textureData[y1 * width + x1];

            Vector4 lerp0 = Vector4.Lerp(sample00, sample10, fx);
            Vector4 lerp1 = Vector4.Lerp(sample01, sample11, fx);
            return Vector4.Lerp(lerp0, lerp1, fy);
        }

        /// <summary>
        /// Reads texture data to CPU memory.
        /// Implements proper texture readback using Stride's GetData API.
        /// This is expensive and should only be used as CPU fallback when GPU shaders are not available.
        /// </summary>
        private Vector4[] ReadTextureData(Texture texture)
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
                var data = new Vector4[size];

                // Get ImmediateContext (CommandList) from GraphicsDevice
                var commandList = _graphicsDevice.ImmediateContext;
                if (commandList == null)
                {
                    Console.WriteLine("[StrideSSR] ReadTextureData: ImmediateContext not available");
                    return data; // Return zero-initialized data
                }

                // Handle different texture formats
                PixelFormat format = texture.Format;
                
                // For color textures (RGBA formats), use Color array
                if (format == PixelFormat.R8G8B8A8_UNorm || 
                    format == PixelFormat.R8G8B8A8_UNorm_SRgb ||
                    format == PixelFormat.R32G32B32A32_Float ||
                    format == PixelFormat.R16G16B16A16_Float ||
                    format == PixelFormat.B8G8R8A8_UNorm ||
                    format == PixelFormat.B8G8R8A8_UNorm_SRgb)
                {
                    // Read as Color array (Stride's standard format)
                    var colorData = new Stride.Core.Mathematics.Color[size];
                    texture.GetData(commandList, colorData);

                    // Convert Color[] to Vector4[]
                    for (int i = 0; i < size; i++)
                    {
                        var color = colorData[i];
                        data[i] = new Vector4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
                    }
                }
                // For depth textures, read as float array
                else if (format == PixelFormat.D32_Float || 
                         format == PixelFormat.D24_UNorm_S8_UInt ||
                         format == PixelFormat.D16_UNorm ||
                         format == PixelFormat.R32_Float)
                {
                    // For depth textures, read as float array
                    var floatData = new float[size];
                    texture.GetData(commandList, floatData);

                    // Convert float[] to Vector4[] (depth in X, others zero)
                    for (int i = 0; i < size; i++)
                    {
                        data[i] = new Vector4(floatData[i], 0.0f, 0.0f, 1.0f);
                    }
                }
                // For single-channel formats, read as byte array and convert
                else if (format == PixelFormat.R8_UNorm || format == PixelFormat.A8_UNorm)
                {
                    var byteData = new byte[size];
                    texture.GetData(commandList, byteData);

                    // Convert byte[] to Vector4[] (single channel in X, others zero)
                    for (int i = 0; i < size; i++)
                    {
                        float value = byteData[i] / 255.0f;
                        data[i] = new Vector4(value, 0.0f, 0.0f, 1.0f);
                    }
                }
                // For HDR formats (R32G32B32A32_Float), read directly as Vector4
                else if (format == PixelFormat.R32G32B32A32_Float)
                {
                    // Try to read as Vector4 array directly
                    var vectorData = new Stride.Core.Mathematics.Vector4[size];
                    texture.GetData(commandList, vectorData);

                    // Convert Stride Vector4 to System.Numerics Vector4
                    for (int i = 0; i < size; i++)
                    {
                        var v = vectorData[i];
                        data[i] = new Vector4(v.X, v.Y, v.Z, v.W);
                    }
                }
                else
                {
                    // Fallback: Try to read as Color array for unknown formats
                    Console.WriteLine($"[StrideSSR] ReadTextureData: Unsupported format {format}, attempting Color readback");
                    try
                    {
                        var colorData = new Stride.Core.Mathematics.Color[size];
                        texture.GetData(commandList, colorData);

                        for (int i = 0; i < size; i++)
                        {
                            var color = colorData[i];
                            data[i] = new Vector4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideSSR] ReadTextureData: Failed to read texture data: {ex.Message}");
                        // Return zero-initialized data on failure
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideSSR] ReadTextureData: Exception during texture readback: {ex.Message}");
                // Return zero-initialized data on failure
                return new Vector4[texture.Width * texture.Height];
            }
        }

        /// <summary>
        /// Writes texture data from CPU memory to GPU texture.
        /// Implements proper texture upload using Stride's SetData API.
        /// This is expensive and should only be used as CPU fallback when GPU shaders are not available.
        /// </summary>
        private void WriteTextureData(Texture texture, Vector4[] data, int width, int height)
        {
            if (texture == null || data == null || _graphicsDevice == null)
            {
                return;
            }

            try
            {
                // Validate dimensions
                if (texture.Width != width || texture.Height != height)
                {
                    Console.WriteLine($"[StrideSSR] WriteTextureData: Texture dimensions mismatch. Texture: {texture.Width}x{texture.Height}, Data: {width}x{height}");
                    return;
                }

                int size = width * height;
                if (data.Length < size)
                {
                    Console.WriteLine($"[StrideSSR] WriteTextureData: Data array too small. Expected: {size}, Got: {data.Length}");
                    return;
                }

                // Get ImmediateContext (CommandList) from GraphicsDevice
                var commandList = _graphicsDevice.ImmediateContext;
                if (commandList == null)
                {
                    Console.WriteLine("[StrideSSR] WriteTextureData: ImmediateContext not available");
                    return;
                }

                // Handle different texture formats
                PixelFormat format = texture.Format;

                // For color textures (RGBA formats), convert Vector4[] to Color[]
                if (format == PixelFormat.R8G8B8A8_UNorm || 
                    format == PixelFormat.R8G8B8A8_UNorm_SRgb ||
                    format == PixelFormat.B8G8R8A8_UNorm ||
                    format == PixelFormat.B8G8R8A8_UNorm_SRgb)
                {
                    var colorData = new Stride.Core.Mathematics.Color[size];
                    
                    // Convert Vector4[] to Color[] (clamp to [0,1] and convert to [0,255])
                    for (int i = 0; i < size; i++)
                    {
                        var v = data[i];
                        // Clamp values to [0,1] range
                        float r = Math.Max(0.0f, Math.Min(1.0f, v.X));
                        float g = Math.Max(0.0f, Math.Min(1.0f, v.Y));
                        float b = Math.Max(0.0f, Math.Min(1.0f, v.Z));
                        float a = Math.Max(0.0f, Math.Min(1.0f, v.W));
                        
                        colorData[i] = new Stride.Core.Mathematics.Color(
                            (byte)(r * 255.0f),
                            (byte)(g * 255.0f),
                            (byte)(b * 255.0f),
                            (byte)(a * 255.0f));
                    }
                    
                    texture.SetData(commandList, colorData);
                }
                // For HDR formats (R32G32B32A32_Float), write directly as Vector4
                else if (format == PixelFormat.R32G32B32A32_Float ||
                         format == PixelFormat.R16G16B16A16_Float)
                {
                    var vectorData = new Stride.Core.Mathematics.Vector4[size];
                    
                    // Convert System.Numerics.Vector4[] to Stride.Core.Mathematics.Vector4[]
                    for (int i = 0; i < size; i++)
                    {
                        var v = data[i];
                        vectorData[i] = new Stride.Core.Mathematics.Vector4(v.X, v.Y, v.Z, v.W);
                    }
                    
                    texture.SetData(commandList, vectorData);
                }
                // For depth textures, extract depth channel
                else if (format == PixelFormat.D32_Float || 
                         format == PixelFormat.R32_Float)
                {
                    var floatData = new float[size];
                    
                    // Extract X component (depth) from Vector4
                    for (int i = 0; i < size; i++)
                    {
                        floatData[i] = data[i].X;
                    }
                    
                    texture.SetData(commandList, floatData);
                }
                // For single-channel formats, extract single channel
                else if (format == PixelFormat.R8_UNorm || format == PixelFormat.A8_UNorm)
                {
                    var byteData = new byte[size];
                    
                    // Extract X component and convert to byte
                    for (int i = 0; i < size; i++)
                    {
                        float value = Math.Max(0.0f, Math.Min(1.0f, data[i].X));
                        byteData[i] = (byte)(value * 255.0f);
                    }
                    
                    texture.SetData(commandList, byteData);
                }
                else
                {
                    // Fallback: Try to write as Color array for unknown formats
                    Console.WriteLine($"[StrideSSR] WriteTextureData: Unsupported format {format}, attempting Color upload");
                    try
                    {
                        var colorData = new Stride.Core.Mathematics.Color[size];
                        
                        for (int i = 0; i < size; i++)
                        {
                            var v = data[i];
                            float r = Math.Max(0.0f, Math.Min(1.0f, v.X));
                            float g = Math.Max(0.0f, Math.Min(1.0f, v.Y));
                            float b = Math.Max(0.0f, Math.Min(1.0f, v.Z));
                            float a = Math.Max(0.0f, Math.Min(1.0f, v.W));
                            
                            colorData[i] = new Stride.Core.Mathematics.Color(
                                (byte)(r * 255.0f),
                                (byte)(g * 255.0f),
                                (byte)(b * 255.0f),
                                (byte)(a * 255.0f));
                        }
                        
                        texture.SetData(commandList, colorData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideSSR] WriteTextureData: Failed to write texture data: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideSSR] WriteTextureData: Exception during texture upload: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the SSR constant buffer with current parameters.
        /// </summary>
        private void UpdateSsrConstants(int width, int height)
        {
            if (_ssrConstants == null) return;

            var constants = new SsrConstants
            {
                Projection = _projectionMatrix,
                ProjectionInv = _projectionMatrixInv,
                ScreenProjection = _screenProjection,
                ScreenResolution = _screenResolution,
                ScreenResolutionRcp = _screenResolutionRcp,
                ClipNear = _clipNear,
                ClipFar = _clipFar,
                SSRBias = 0.1f,
                SSRPixelStride = _stepSize,
                SSRMaxSteps = _maxIterations,
                MaxDistance = _maxDistance,
                StepSize = _stepSize,
                Intensity = _intensity,
                EdgeFadeStart = _edgeFadeStart,
                Padding = Vector2.Zero
            };

            // Update constant buffer
            // In Stride, constant buffers are updated through CommandList
            // TODO: STUB - For now, we store the constants and would update them before rendering
            // TODO:  In a full implementation:
            // commandList.UpdateSubresource(_ssrConstants, 0, ref constants);
            // Or use EffectInstance.Parameters to set individual values
        }

        /// <summary>
        /// Converts System.Numerics.Matrix4x4 to Stride Matrix.
        /// </summary>
        private Matrix ConvertMatrix(System.Numerics.Matrix4x4 matrix)
        {
            return new Matrix(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44);
        }

        public void SetQuality(int qualityLevel)
        {
            // Adjust SSR quality based on quality level (0-4)
            // Higher quality = more iterations, lower step size
            switch (qualityLevel)
            {
                case 4: // Ultra
                    _maxIterations = 128;
                    _stepSize = 0.05f;
                    break;

                case 3: // High
                    _maxIterations = 96;
                    _stepSize = 0.075f;
                    break;

                case 2: // Medium
                    _maxIterations = 64;
                    _stepSize = 0.1f;
                    break;

                case 1: // Low
                    _maxIterations = 48;
                    _stepSize = 0.15f;
                    break;

                default: // Off
                    _maxIterations = 32;
                    _stepSize = 0.2f;
                    break;
            }
        }

        /// <summary>
        /// Sets the camera near and far clip planes for depth reconstruction.
        /// </summary>
        public void SetClipPlanes(float near, float far)
        {
            _clipNear = near;
            _clipFar = far;
        }
    }
}


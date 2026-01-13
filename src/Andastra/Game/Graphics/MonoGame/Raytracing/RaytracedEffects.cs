using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;

namespace Andastra.Game.Graphics.MonoGame.Raytracing
{
    /// <summary>
    /// High-level raytraced visual effects coordinator.
    /// Manages the configuration and scheduling of various RT effects.
    /// </summary>
    public class RaytracedEffects : IDisposable
    {
        private readonly IRaytracingSystem _rtSystem;
        private RaytracedEffectsConfig _config;
        private bool _disposed;

        // Output textures
        private IntPtr _shadowMask;
        private IntPtr _reflectionBuffer;
        private IntPtr _aoBuffer;
        private IntPtr _giBuffer;

        // History buffers for temporal accumulation
        private IntPtr _shadowHistory;
        private IntPtr _reflectionHistory;
        private IntPtr _giHistory;

        // Frame counter for temporal jittering
        private int _frameIndex;

        public RaytracedEffects(IRaytracingSystem rtSystem)
        {
            _rtSystem = rtSystem;
            _config = RaytracedEffectsConfig.CreateDefault();
        }

        /// <summary>
        /// Configures the raytraced effects.
        /// </summary>
        public void Configure(RaytracedEffectsConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Traces all enabled raytraced effects for the current frame.
        /// </summary>
        public void Trace(RaytracedEffectsInput input)
        {
            if (!_rtSystem.IsEnabled)
            {
                return;
            }

            _frameIndex++;

            // Shadows
            if (_config.ShadowsEnabled && _rtSystem.CurrentLevel != RaytracingLevel.ReflectionsOnly)
            {
                TraceShadows(input);
            }

            // Reflections
            if (_config.ReflectionsEnabled && _rtSystem.CurrentLevel != RaytracingLevel.ShadowsOnly)
            {
                TraceReflections(input);
            }

            // Ambient Occlusion
            if (_config.AmbientOcclusionEnabled)
            {
                TraceAmbientOcclusion(input);
            }

            // Global Illumination
            if (_config.GlobalIlluminationEnabled && _rtSystem.CurrentLevel == RaytracingLevel.Full)
            {
                TraceGlobalIllumination(input);
            }
        }

        /// <summary>
        /// Gets the raytraced shadow mask texture.
        /// </summary>
        public IntPtr GetShadowMask()
        {
            return _shadowMask;
        }

        /// <summary>
        /// Gets the raytraced reflection buffer.
        /// </summary>
        public IntPtr GetReflectionBuffer()
        {
            return _reflectionBuffer;
        }

        /// <summary>
        /// Gets the raytraced AO buffer.
        /// </summary>
        public IntPtr GetAmbientOcclusionBuffer()
        {
            return _aoBuffer;
        }

        /// <summary>
        /// Gets the raytraced GI buffer.
        /// </summary>
        public IntPtr GetGlobalIlluminationBuffer()
        {
            return _giBuffer;
        }

        private void TraceShadows(RaytracedEffectsInput input)
        {
            var parameters = new ShadowRayParams
            {
                OutputTexture = _shadowMask,
                LightDirection = input.SunDirection,
                MaxDistance = _config.ShadowMaxDistance,
                SamplesPerPixel = _config.ShadowSamplesPerPixel,
                SoftShadowAngle = _config.SoftShadowAngle
            };

            _rtSystem.TraceShadowRays(parameters);

            // Denoise shadows
            if (_config.DenoiserEnabled)
            {
                _rtSystem.Denoise(new DenoiserParams
                {
                    InputTexture = _shadowMask,
                    OutputTexture = _shadowMask,
                    NormalTexture = input.GBufferNormals,
                    MotionTexture = input.MotionVectors,
                    Type = _config.DenoiserType,
                    BlendFactor = _config.TemporalBlendFactor
                });
            }
        }

        private void TraceReflections(RaytracedEffectsInput input)
        {
            var parameters = new ReflectionRayParams
            {
                OutputTexture = _reflectionBuffer,
                NormalTexture = input.GBufferNormals,
                RoughnessTexture = input.GBufferRoughness,
                DepthTexture = input.GBufferDepth,
                MaxBounces = _config.ReflectionMaxBounces,
                SamplesPerPixel = _config.ReflectionSamplesPerPixel,
                RoughnessThreshold = _config.ReflectionRoughnessThreshold
            };

            _rtSystem.TraceReflectionRays(parameters);

            // Denoise reflections
            if (_config.DenoiserEnabled)
            {
                _rtSystem.Denoise(new DenoiserParams
                {
                    InputTexture = _reflectionBuffer,
                    OutputTexture = _reflectionBuffer,
                    AlbedoTexture = input.GBufferAlbedo,
                    NormalTexture = input.GBufferNormals,
                    MotionTexture = input.MotionVectors,
                    Type = _config.DenoiserType,
                    BlendFactor = _config.TemporalBlendFactor
                });
            }
        }

        private void TraceAmbientOcclusion(RaytracedEffectsInput input)
        {
            var parameters = new AoRayParams
            {
                OutputTexture = _aoBuffer,
                NormalTexture = input.GBufferNormals,
                DepthTexture = input.GBufferDepth,
                Radius = _config.AoRadius,
                SamplesPerPixel = _config.AoSamplesPerPixel,
                Intensity = _config.AoIntensity
            };

            _rtSystem.TraceAmbientOcclusion(parameters);
        }

        private void TraceGlobalIllumination(RaytracedEffectsInput input)
        {
            var parameters = new GiRayParams
            {
                OutputTexture = _giBuffer,
                NormalTexture = input.GBufferNormals,
                DepthTexture = input.GBufferDepth,
                MaxBounces = _config.GiMaxBounces,
                SamplesPerPixel = _config.GiSamplesPerPixel,
                IndirectIntensity = _config.GiIntensity
            };

            _rtSystem.TraceGlobalIllumination(parameters);

            // GI needs aggressive denoising
            if (_config.DenoiserEnabled)
            {
                _rtSystem.Denoise(new DenoiserParams
                {
                    InputTexture = _giBuffer,
                    OutputTexture = _giBuffer,
                    AlbedoTexture = input.GBufferAlbedo,
                    NormalTexture = input.GBufferNormals,
                    MotionTexture = input.MotionVectors,
                    Type = _config.DenoiserType,
                    BlendFactor = _config.TemporalBlendFactor * 0.5f // More aggressive accumulation for GI
                });
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Release output textures
            // Release history buffers

            _disposed = true;
        }
    }

    /// <summary>
    /// Configuration for raytraced effects.
    /// </summary>
    public struct RaytracedEffectsConfig
    {
        // Shadows
        public bool ShadowsEnabled;
        public int ShadowSamplesPerPixel;
        public float ShadowMaxDistance;
        public float SoftShadowAngle;

        // Reflections
        public bool ReflectionsEnabled;
        public int ReflectionSamplesPerPixel;
        public int ReflectionMaxBounces;
        public float ReflectionRoughnessThreshold;

        // Ambient Occlusion
        public bool AmbientOcclusionEnabled;
        public int AoSamplesPerPixel;
        public float AoRadius;
        public float AoIntensity;

        // Global Illumination
        public bool GlobalIlluminationEnabled;
        public int GiSamplesPerPixel;
        public int GiMaxBounces;
        public float GiIntensity;

        // Denoising
        public bool DenoiserEnabled;
        public DenoiserType DenoiserType;
        public float TemporalBlendFactor;

        public static RaytracedEffectsConfig CreateDefault()
        {
            return new RaytracedEffectsConfig
            {
                ShadowsEnabled = true,
                ShadowSamplesPerPixel = 1,
                ShadowMaxDistance = 1000f,
                SoftShadowAngle = 0.5f,

                ReflectionsEnabled = true,
                ReflectionSamplesPerPixel = 1,
                ReflectionMaxBounces = 2,
                ReflectionRoughnessThreshold = 0.3f,

                AmbientOcclusionEnabled = true,
                AoSamplesPerPixel = 1,
                AoRadius = 2.0f,
                AoIntensity = 1.0f,

                GlobalIlluminationEnabled = false,
                GiSamplesPerPixel = 1,
                GiMaxBounces = 2,
                GiIntensity = 1.0f,

                DenoiserEnabled = true,
                DenoiserType = DenoiserType.Temporal,
                TemporalBlendFactor = 0.1f
            };
        }

        public static RaytracedEffectsConfig CreatePerformance()
        {
            RaytracedEffectsConfig config = CreateDefault();
            config.ReflectionsEnabled = false;
            config.GlobalIlluminationEnabled = false;
            config.AoSamplesPerPixel = 1;
            return config;
        }

        public static RaytracedEffectsConfig CreateQuality()
        {
            RaytracedEffectsConfig config = CreateDefault();
            config.ShadowSamplesPerPixel = 2;
            config.ReflectionSamplesPerPixel = 2;
            config.AoSamplesPerPixel = 2;
            config.GlobalIlluminationEnabled = true;
            config.GiSamplesPerPixel = 2;
            return config;
        }

        public static RaytracedEffectsConfig CreateUltra()
        {
            RaytracedEffectsConfig config = CreateQuality();
            config.ShadowSamplesPerPixel = 4;
            config.ReflectionSamplesPerPixel = 4;
            config.ReflectionMaxBounces = 4;
            config.AoSamplesPerPixel = 4;
            config.GiSamplesPerPixel = 4;
            config.GiMaxBounces = 4;
            return config;
        }
    }

    /// <summary>
    /// Input data for raytraced effects.
    /// </summary>
    public struct RaytracedEffectsInput
    {
        // G-buffer textures
        public IntPtr GBufferAlbedo;
        public IntPtr GBufferNormals;
        public IntPtr GBufferRoughness;
        public IntPtr GBufferMetallic;
        public IntPtr GBufferDepth;

        // Motion vectors for temporal accumulation
        public IntPtr MotionVectors;

        // Light information
        public Vector3 SunDirection;
        public Vector3 SunColor;
        public float SunIntensity;

        // Camera data
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 InverseViewProjection;
        public Vector3 CameraPosition;
    }
}


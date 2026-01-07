using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;

namespace Andastra.Runtime.MonoGame.Interfaces
{
    /// <summary>
    /// Hardware raytracing system interface.
    /// Supports both native DXR/Vulkan RT and NVIDIA RTX Remix path tracing.
    /// </summary>
    public interface IRaytracingSystem : IDisposable
    {
        /// <summary>
        /// Whether raytracing is available on this hardware.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Whether raytracing is currently enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Current raytracing feature level.
        /// </summary>
        RaytracingLevel CurrentLevel { get; }

        /// <summary>
        /// Whether RTX Remix runtime is detected.
        /// </summary>
        bool RemixAvailable { get; }

        /// <summary>
        /// Whether running in Remix path tracing mode.
        /// </summary>
        bool RemixActive { get; }

        /// <summary>
        /// Raytracing hardware tier (1.0, 1.1).
        /// </summary>
        float HardwareTier { get; }

        /// <summary>
        /// Maximum ray recursion depth.
        /// </summary>
        int MaxRecursionDepth { get; }

        /// <summary>
        /// Initializes the raytracing system.
        /// </summary>
        bool Initialize(RaytracingSettings settings);

        /// <summary>
        /// Shuts down the raytracing system.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Sets the raytracing feature level.
        /// </summary>
        void SetLevel(RaytracingLevel level);

        /// <summary>
        /// Builds/updates the top-level acceleration structure (TLAS).
        /// </summary>
        void BuildTopLevelAS();

        /// <summary>
        /// Builds a bottom-level acceleration structure (BLAS) for a mesh.
        /// </summary>
        IntPtr BuildBottomLevelAS(MeshGeometry geometry);

        /// <summary>
        /// Adds an instance to the TLAS.
        /// </summary>
        void AddInstance(IntPtr blas, Matrix4x4 transform, uint instanceMask, uint hitGroupIndex);

        /// <summary>
        /// Removes an instance from the TLAS.
        /// </summary>
        void RemoveInstance(IntPtr blas);

        /// <summary>
        /// Updates an instance transform.
        /// </summary>
        void UpdateInstanceTransform(IntPtr blas, Matrix4x4 transform);

        /// <summary>
        /// Traces rays for shadows.
        /// </summary>
        void TraceShadowRays(ShadowRayParams parameters);

        /// <summary>
        /// Traces rays for reflections.
        /// </summary>
        void TraceReflectionRays(ReflectionRayParams parameters);

        /// <summary>
        /// Traces rays for global illumination.
        /// </summary>
        void TraceGlobalIllumination(GiRayParams parameters);

        /// <summary>
        /// Traces rays for ambient occlusion.
        /// </summary>
        void TraceAmbientOcclusion(AoRayParams parameters);

        /// <summary>
        /// Runs the denoiser on raytraced output.
        /// </summary>
        void Denoise(DenoiserParams parameters);

        /// <summary>
        /// Gets raytracing performance statistics.
        /// </summary>
        RaytracingStatistics GetStatistics();
    }

    /// <summary>
    /// Raytracing initialization settings.
    /// </summary>
    public struct RaytracingSettings
    {
        /// <summary>
        /// Initial feature level.
        /// </summary>
        public RaytracingLevel Level;

        /// <summary>
        /// Maximum TLAS instances.
        /// </summary>
        public int MaxInstances;

        /// <summary>
        /// Enable async AS builds.
        /// </summary>
        public bool AsyncBuilds;

        /// <summary>
        /// Enable RTX Remix compatibility.
        /// </summary>
        public bool RemixCompatibility;

        /// <summary>
        /// Path to Remix runtime.
        /// </summary>
        public string RemixRuntimePath;

        /// <summary>
        /// Ray budget per frame.
        /// </summary>
        public int RayBudget;

        /// <summary>
        /// Enable denoising.
        /// </summary>
        public bool EnableDenoiser;

        /// <summary>
        /// Denoiser type.
        /// </summary>
        public DenoiserType Denoiser;
    }

    /// <summary>
    /// Mesh geometry for BLAS building.
    /// </summary>
    public struct MeshGeometry
    {
        public IntPtr VertexBuffer;
        public IntPtr IndexBuffer;
        public int VertexCount;
        public int IndexCount;
        public int VertexStride;
        public bool IsOpaque;
    }

    /// <summary>
    /// Shadow ray tracing parameters.
    /// </summary>
    public struct ShadowRayParams
    {
        public IntPtr OutputTexture;
        public Vector3 LightDirection;
        public float MaxDistance;
        public int SamplesPerPixel;
        public float SoftShadowAngle;
    }

    /// <summary>
    /// Reflection ray tracing parameters.
    /// </summary>
    public struct ReflectionRayParams
    {
        public IntPtr OutputTexture;
        public IntPtr NormalTexture;
        public IntPtr RoughnessTexture;
        public IntPtr DepthTexture;
        public int MaxBounces;
        public int SamplesPerPixel;
        public float RoughnessThreshold;
    }

    /// <summary>
    /// Global illumination ray parameters.
    /// </summary>
    public struct GiRayParams
    {
        public IntPtr OutputTexture;
        public IntPtr NormalTexture;
        public IntPtr DepthTexture;
        public int MaxBounces;
        public int SamplesPerPixel;
        public float IndirectIntensity;
    }

    /// <summary>
    /// Ambient occlusion ray parameters.
    /// </summary>
    public struct AoRayParams
    {
        public IntPtr OutputTexture;
        public IntPtr NormalTexture;
        public IntPtr DepthTexture;
        public float Radius;
        public int SamplesPerPixel;
        public float Intensity;
    }

    /// <summary>
    /// Denoiser parameters.
    /// </summary>
    public struct DenoiserParams
    {
        public IntPtr InputTexture;
        public IntPtr OutputTexture;
        public IntPtr AlbedoTexture;
        public IntPtr NormalTexture;
        public IntPtr MotionTexture;
        public DenoiserType Type;
        public float BlendFactor;
    }

    /// <summary>
    /// Denoiser types.
    /// </summary>
    public enum DenoiserType
    {
        None,
        Temporal,
        Spatial,
        NvidiaRealTimeDenoiser,
        IntelOpenImageDenoise
    }

    /// <summary>
    /// Raytracing performance statistics.
    /// </summary>
    public struct RaytracingStatistics
    {
        public double TraceTimeMs;
        public double DenoiseTimeMs;
        public double BuildTimeMs;
        public long RaysTraced;
        public int TlasInstanceCount;
        public int BlasCount;
        public long AccelerationStructureMemory;
    }
}


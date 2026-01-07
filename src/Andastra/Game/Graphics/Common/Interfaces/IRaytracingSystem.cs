using System;
using System.Numerics;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Hardware raytracing system interface.
    ///
    /// Provides abstraction for:
    /// - DXR 1.1 (DirectX 12)
    /// - Vulkan Ray Tracing (VK_KHR_ray_tracing_pipeline)
    /// - NVIDIA RTX Remix path tracing
    ///
    /// Features:
    /// - Bottom-level acceleration structure (BLAS) management
    /// - Top-level acceleration structure (TLAS) with instancing
    /// - Raytraced shadows (soft shadows with penumbra)
    /// - Raytraced reflections (glossy and mirror)
    /// - Raytraced ambient occlusion (RTAO)
    /// - Raytraced global illumination (RTGI)
    /// - Temporal denoising integration
    /// </summary>
    public interface IRaytracingSystem : IDisposable
    {
        /// <summary>
        /// Whether hardware raytracing is available.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Whether raytracing is enabled and active.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Current raytracing feature level.
        /// </summary>
        RaytracingLevel CurrentLevel { get; }

        /// <summary>
        /// Whether NVIDIA RTX Remix is available.
        /// </summary>
        bool RemixAvailable { get; }

        /// <summary>
        /// Whether Remix is currently active (path tracing mode).
        /// </summary>
        bool RemixActive { get; }

        /// <summary>
        /// Hardware raytracing tier (1.0, 1.1, etc).
        /// </summary>
        float HardwareTier { get; }

        /// <summary>
        /// Maximum ray recursion depth supported.
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
        /// Builds/rebuilds the top-level acceleration structure.
        /// </summary>
        void BuildTopLevelAS();

        /// <summary>
        /// Builds a bottom-level acceleration structure for mesh geometry.
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
        /// Traces shadow rays for soft shadows.
        /// </summary>
        void TraceShadowRays(ShadowRayParams parameters);

        /// <summary>
        /// Traces reflection rays.
        /// </summary>
        void TraceReflectionRays(ReflectionRayParams parameters);

        /// <summary>
        /// Traces global illumination rays.
        /// </summary>
        void TraceGlobalIllumination(GiRayParams parameters);

        /// <summary>
        /// Traces ambient occlusion rays.
        /// </summary>
        void TraceAmbientOcclusion(AoRayParams parameters);

        /// <summary>
        /// Applies denoising to raytraced output.
        /// </summary>
        void Denoise(DenoiserParams parameters);

        /// <summary>
        /// Gets raytracing statistics.
        /// </summary>
        RaytracingStatistics GetStatistics();
    }
}


using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Raytracing
{
    /// <summary>
    /// Abstract base class for hardware raytracing systems.
    ///
    /// Provides shared raytracing logic that can be inherited by both
    /// MonoGame and Stride implementations.
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
    public abstract class BaseRaytracingSystem : IRaytracingSystem
    {
        protected ILowLevelBackend _backend;
        protected RaytracingSettings _settings;
        protected bool _initialized;
        protected bool _enabled;
        protected RaytracingLevel _level;

        // Acceleration structures
        protected readonly Dictionary<IntPtr, BlasEntry> _blasEntries;
        protected readonly List<TlasInstance> _tlasInstances;
        protected IntPtr _tlas;
        protected bool _tlasDirty;

        // Ray tracing pipelines
        protected IntPtr _shadowPipeline;
        protected IntPtr _reflectionPipeline;
        protected IntPtr _aoPipeline;
        protected IntPtr _giPipeline;

        // Denoiser state
        protected IntPtr _denoiserShadow;
        protected IntPtr _denoiserReflection;
        protected IntPtr _denoiserGi;

        // Statistics
        protected RaytracingStatistics _lastStats;

        public virtual bool IsAvailable => _backend != null && QueryRaytracingSupport();
        public bool IsEnabled => _enabled && _initialized;
        public RaytracingLevel CurrentLevel => _level;
        public virtual bool RemixAvailable => false;
        public virtual bool RemixActive => false;
        public virtual float HardwareTier => 1.1f;
        public virtual int MaxRecursionDepth => _settings.Level == RaytracingLevel.PathTracing ? 8 : 3;

        protected BaseRaytracingSystem(ILowLevelBackend backend)
        {
            _backend = backend;
            _blasEntries = new Dictionary<IntPtr, BlasEntry>();
            _tlasInstances = new List<TlasInstance>();
            _tlasDirty = true;
        }

        #region IRaytracingSystem Implementation

        public virtual bool Initialize(RaytracingSettings settings)
        {
            if (_initialized) return true;

            if (!IsAvailable)
            {
                Console.WriteLine("[Raytracing] Hardware raytracing not available");
                return false;
            }

            _settings = settings;
            _level = settings.Level;

            // Create ray tracing pipelines
            if (!CreatePipelines())
            {
                Console.WriteLine("[Raytracing] Failed to create RT pipelines");
                return false;
            }

            // Initialize denoiser
            if (settings.EnableDenoiser)
            {
                InitializeDenoiser(settings.Denoiser);
            }

            _initialized = true;
            _enabled = settings.Level != RaytracingLevel.Disabled;

            LogInitialization();

            return true;
        }

        public virtual void Shutdown()
        {
            if (!_initialized) return;

            // Destroy all BLAS
            foreach (var entry in _blasEntries.Values)
            {
                DestroyBlas(entry.Handle);
            }
            _blasEntries.Clear();

            // Destroy TLAS
            if (_tlas != IntPtr.Zero)
            {
                DestroyTlas(_tlas);
                _tlas = IntPtr.Zero;
            }

            // Destroy pipelines
            DestroyPipelines();

            // Destroy denoiser
            ShutdownDenoiser();

            _initialized = false;
            _enabled = false;

            Console.WriteLine("[Raytracing] Shutdown complete");
        }

        public virtual void SetLevel(RaytracingLevel level)
        {
            _level = level;
            _enabled = level != RaytracingLevel.Disabled;
            OnLevelChanged(level);
        }

        public virtual void BuildTopLevelAS()
        {
            if (!_initialized || !_tlasDirty) return;

            OnBuildTopLevelAS();

            _tlasDirty = false;
            _lastStats.TlasInstanceCount = _tlasInstances.Count;
        }

        public virtual IntPtr BuildBottomLevelAS(MeshGeometry geometry)
        {
            if (!_initialized) return IntPtr.Zero;

            var handle = OnBuildBottomLevelAS(geometry);

            if (handle != IntPtr.Zero)
            {
                _blasEntries[handle] = new BlasEntry
                {
                    Handle = handle,
                    VertexCount = geometry.VertexCount,
                    IndexCount = geometry.IndexCount,
                    IsOpaque = geometry.IsOpaque
                };
                _lastStats.BlasCount = _blasEntries.Count;
            }

            return handle;
        }

        public virtual void AddInstance(IntPtr blas, Matrix4x4 transform, uint instanceMask, uint hitGroupIndex)
        {
            if (!_initialized || !_blasEntries.ContainsKey(blas)) return;

            _tlasInstances.Add(new TlasInstance
            {
                BlasHandle = blas,
                Transform = transform,
                InstanceMask = instanceMask,
                HitGroupIndex = hitGroupIndex
            });

            _tlasDirty = true;
        }

        public virtual void RemoveInstance(IntPtr blas)
        {
            _tlasInstances.RemoveAll(i => i.BlasHandle == blas);
            _tlasDirty = true;
        }

        public virtual void UpdateInstanceTransform(IntPtr blas, Matrix4x4 transform)
        {
            for (int i = 0; i < _tlasInstances.Count; i++)
            {
                if (_tlasInstances[i].BlasHandle == blas)
                {
                    var instance = _tlasInstances[i];
                    instance.Transform = transform;
                    _tlasInstances[i] = instance;
                }
            }
            _tlasDirty = true;
        }

        public virtual void TraceShadowRays(ShadowRayParams parameters)
        {
            if (!_enabled || !IsShadowsEnabled()) return;

            BuildTopLevelAS();
            OnTraceShadowRays(parameters);

            _lastStats.RaysTraced += (long)parameters.SamplesPerPixel * GetRenderWidth() * GetRenderHeight();
        }

        public virtual void TraceReflectionRays(ReflectionRayParams parameters)
        {
            if (!_enabled || !IsReflectionsEnabled()) return;

            BuildTopLevelAS();
            OnTraceReflectionRays(parameters);

            _lastStats.RaysTraced += (long)parameters.SamplesPerPixel * parameters.MaxBounces *
                GetRenderWidth() * GetRenderHeight();
        }

        public virtual void TraceGlobalIllumination(GiRayParams parameters)
        {
            if (!_enabled || _level != RaytracingLevel.Full) return;

            BuildTopLevelAS();
            OnTraceGlobalIllumination(parameters);

            _lastStats.RaysTraced += (long)parameters.SamplesPerPixel * parameters.MaxBounces *
                GetRenderWidth() * GetRenderHeight();
        }

        public virtual void TraceAmbientOcclusion(AoRayParams parameters)
        {
            if (!_enabled) return;

            BuildTopLevelAS();
            OnTraceAmbientOcclusion(parameters);

            _lastStats.RaysTraced += (long)parameters.SamplesPerPixel * GetRenderWidth() * GetRenderHeight();
        }

        public virtual void Denoise(DenoiserParams parameters)
        {
            if (!_initialized || parameters.Type == DenoiserType.None) return;
            OnDenoise(parameters);
        }

        public virtual RaytracingStatistics GetStatistics() => _lastStats;

        public virtual void Dispose()
        {
            Shutdown();
        }

        #endregion

        #region Abstract Methods

        protected abstract bool QueryRaytracingSupport();
        protected abstract bool CreatePipelines();
        protected abstract void DestroyPipelines();
        protected abstract void InitializeDenoiser(DenoiserType type);
        protected abstract void ShutdownDenoiser();
        protected abstract void OnBuildTopLevelAS();
        protected abstract IntPtr OnBuildBottomLevelAS(MeshGeometry geometry);
        protected abstract void DestroyBlas(IntPtr handle);
        protected abstract void DestroyTlas(IntPtr handle);
        protected abstract void OnTraceShadowRays(ShadowRayParams parameters);
        protected abstract void OnTraceReflectionRays(ReflectionRayParams parameters);
        protected abstract void OnTraceGlobalIllumination(GiRayParams parameters);
        protected abstract void OnTraceAmbientOcclusion(AoRayParams parameters);
        protected abstract void OnDenoise(DenoiserParams parameters);

        #endregion

        #region Virtual Hooks

        protected virtual void OnLevelChanged(RaytracingLevel level) { }
        protected virtual int GetRenderWidth() => 1920;
        protected virtual int GetRenderHeight() => 1080;

        #endregion

        #region Utility Methods

        protected bool IsShadowsEnabled()
        {
            return _level == RaytracingLevel.ShadowsOnly ||
                   _level == RaytracingLevel.ShadowsAndReflections ||
                   _level == RaytracingLevel.Full ||
                   _level == RaytracingLevel.PathTracing;
        }

        protected bool IsReflectionsEnabled()
        {
            return _level == RaytracingLevel.ReflectionsOnly ||
                   _level == RaytracingLevel.ShadowsAndReflections ||
                   _level == RaytracingLevel.Full ||
                   _level == RaytracingLevel.PathTracing;
        }

        protected void LogInitialization()
        {
            Console.WriteLine("[Raytracing] Initialized successfully");
            Console.WriteLine($"[Raytracing] Level: {_level}");
            Console.WriteLine($"[Raytracing] Hardware Tier: {HardwareTier}");
            Console.WriteLine($"[Raytracing] Max Recursion: {MaxRecursionDepth}");
            Console.WriteLine($"[Raytracing] Denoiser: {_settings.Denoiser}");
        }

        #endregion

        #region Nested Types

        protected struct BlasEntry
        {
            public IntPtr Handle;
            public int VertexCount;
            public int IndexCount;
            public bool IsOpaque;
        }

        protected struct TlasInstance
        {
            public IntPtr BlasHandle;
            public Matrix4x4 Transform;
            public uint InstanceMask;
            public uint HitGroupIndex;
        }

        #endregion
    }
}


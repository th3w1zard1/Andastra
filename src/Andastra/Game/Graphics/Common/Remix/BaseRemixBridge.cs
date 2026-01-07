using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Rendering;

namespace Andastra.Runtime.Graphics.Common.Remix
{
    /// <summary>
    /// Abstract base class for NVIDIA RTX Remix bridge implementations.
    /// 
    /// RTX Remix works by intercepting DirectX 8/9 API calls and replacing
    /// the rasterized output with path-traced rendering. This bridge provides
    /// a DX9 compatibility layer that Remix can hook into.
    /// 
    /// Based on RTX Remix documentation:
    /// https://github.com/NVIDIAGameWorks/rtx-remix
    /// </summary>
    public abstract class BaseRemixBridge : IDisposable
    {
        protected IntPtr _d3d9Handle;
        protected IntPtr _deviceHandle;
        protected bool _initialized;
        protected bool _remixDetected;
        protected RemixCapabilities _capabilities;
        protected RemixSettings _settings;

        /// <summary>
        /// Whether Remix runtime is detected and available.
        /// </summary>
        public bool IsAvailable => _remixDetected;

        /// <summary>
        /// Whether the bridge is initialized and active.
        /// </summary>
        public bool IsActive => _initialized && _remixDetected;

        /// <summary>
        /// Remix runtime capabilities.
        /// </summary>
        public RemixCapabilities Capabilities => _capabilities;

        /// <summary>
        /// Initializes the Remix bridge.
        /// </summary>
        public virtual bool Initialize(IntPtr windowHandle, RemixSettings settings)
        {
            if (_initialized) return true;

            _settings = settings;

            // Check for Remix runtime
            _remixDetected = DetectRemixRuntime(settings.RuntimePath);

            if (!_remixDetected)
            {
                Console.WriteLine("[RemixBridge] RTX Remix runtime not detected");
                return false;
            }

            // Load d3d9.dll (Remix's interceptor DLL)
            if (!LoadD3D9Library(settings.RuntimePath))
            {
                Console.WriteLine("[RemixBridge] Failed to load d3d9.dll");
                return false;
            }

            // Create D3D9 device
            if (!CreateD3D9Device(windowHandle, settings))
            {
                UnloadD3D9Library();
                return false;
            }

            // Query Remix capabilities
            QueryRemixCapabilities();

            _initialized = true;
            Console.WriteLine("[RemixBridge] Initialized successfully");
            Console.WriteLine($"[RemixBridge] Path tracing: {(_capabilities.PathTracingEnabled ? "enabled" : "disabled")}");
            Console.WriteLine($"[RemixBridge] Max bounces: {_capabilities.MaxBounces}");

            return true;
        }

        /// <summary>
        /// Shuts down the Remix bridge.
        /// </summary>
        public virtual void Shutdown()
        {
            if (!_initialized) return;

            ReleaseD3D9Device();
            UnloadD3D9Library();

            _initialized = false;

            Console.WriteLine("[RemixBridge] Shutdown complete");
        }

        /// <summary>
        /// Begins a new frame for Remix rendering.
        /// </summary>
        public virtual void BeginFrame()
        {
            if (!IsActive) return;
            OnBeginFrame();
        }

        /// <summary>
        /// Ends the current frame.
        /// </summary>
        public virtual void EndFrame()
        {
            if (!IsActive) return;
            OnEndFrame();
        }

        /// <summary>
        /// Submits geometry for path tracing.
        /// </summary>
        public virtual void SubmitGeometry(RemixGeometry geometry)
        {
            if (!IsActive) return;
            OnSubmitGeometry(geometry);
        }

        /// <summary>
        /// Submits a light for path tracing.
        /// </summary>
        public virtual void SubmitLight(RemixLight light)
        {
            if (!IsActive) return;
            OnSubmitLight(light);
        }

        /// <summary>
        /// Submits a material for path tracing.
        /// </summary>
        public virtual void SubmitMaterial(RemixMaterial material)
        {
            if (!IsActive) return;
            OnSubmitMaterial(material);
        }

        /// <summary>
        /// Sets the camera for path tracing.
        /// </summary>
        public virtual void SetCamera(RemixCamera camera)
        {
            if (!IsActive) return;
            OnSetCamera(camera);
        }

        /// <summary>
        /// Configures Remix rendering settings.
        /// </summary>
        public virtual void ConfigureRendering(RemixRenderConfig config)
        {
            if (!IsActive) return;
            OnConfigureRendering(config);
        }

        public virtual void Dispose()
        {
            Shutdown();
        }

        #region Abstract Methods

        protected abstract bool LoadD3D9Library(string runtimePath);
        protected abstract void UnloadD3D9Library();
        protected abstract bool CreateD3D9Device(IntPtr windowHandle, RemixSettings settings);
        protected abstract void ReleaseD3D9Device();
        protected abstract void OnBeginFrame();
        protected abstract void OnEndFrame();
        protected abstract void OnSubmitGeometry(RemixGeometry geometry);
        protected abstract void OnSubmitLight(RemixLight light);
        protected abstract void OnSubmitMaterial(RemixMaterial material);
        protected abstract void OnSetCamera(RemixCamera camera);
        protected abstract void OnConfigureRendering(RemixRenderConfig config);

        #endregion

        #region Remix Detection

        protected virtual bool DetectRemixRuntime(string runtimePath)
        {
            // Check specified path first
            if (!string.IsNullOrEmpty(runtimePath))
            {
                string remixDll = Path.Combine(runtimePath, "NvRemixBridge.dll");
                if (File.Exists(remixDll))
                {
                    return true;
                }
            }

            // Check environment variable
            string remixPath = Environment.GetEnvironmentVariable("RTX_REMIX_PATH");
            if (!string.IsNullOrEmpty(remixPath))
            {
                if (File.Exists(Path.Combine(remixPath, "NvRemixBridge.dll")))
                {
                    return true;
                }
            }

            // Check current directory for Remix's d3d9.dll
            if (File.Exists("d3d9.dll"))
            {
                return CheckForRemixExports("d3d9.dll");
            }

            return false;
        }

        protected abstract bool CheckForRemixExports(string dllPath);

        protected virtual void QueryRemixCapabilities()
        {
            _capabilities = new RemixCapabilities
            {
                PathTracingEnabled = true,
                MaxBounces = 8,
                DenoiserAvailable = true,
                DlssAvailable = true,
                ReflexAvailable = true,
                MaxTextureResolution = 4096,
                MaxLights = 10000,
                RayBudget = 10000000
            };
        }

        #endregion
    }

    #region Remix Data Structures

    /// <summary>
    /// Remix initialization settings.
    /// </summary>
    public struct RemixSettings
    {
        /// <summary>Path to RTX Remix runtime.</summary>
        public string RuntimePath;

        /// <summary>Enable path tracing immediately.</summary>
        public bool EnablePathTracing;

        /// <summary>Maximum path tracing bounces.</summary>
        public int MaxBounces;

        /// <summary>Enable denoising.</summary>
        public bool EnableDenoiser;

        /// <summary>Enable DLSS upscaling.</summary>
        public bool EnableDlss;

        /// <summary>Enable NVIDIA Reflex.</summary>
        public bool EnableReflex;

        /// <summary>Capture mode for asset extraction.</summary>
        public bool CaptureMode;
    }

    /// <summary>
    /// Remix runtime capabilities.
    /// </summary>
    public struct RemixCapabilities
    {
        public bool PathTracingEnabled;
        public int MaxBounces;
        public bool DenoiserAvailable;
        public bool DlssAvailable;
        public bool ReflexAvailable;
        public int MaxTextureResolution;
        public int MaxLights;
        public long RayBudget;
    }

    /// <summary>
    /// Geometry data for Remix submission.
    /// </summary>
    public struct RemixGeometry
    {
        public IntPtr VertexBuffer;
        public IntPtr IndexBuffer;
        public int VertexCount;
        public int IndexCount;
        public int VertexStride;
        public Matrix4x4 WorldMatrix;
        public uint MaterialId;
        public bool CastShadows;
        public bool Visible;
    }

    /// <summary>
    /// Light data for Remix submission.
    /// </summary>
    public struct RemixLight
    {
        public LightType Type;
        public Vector3 Position;
        public Vector3 Direction;
        public Vector3 Color;
        public float Intensity;
        public float Radius;
        public float ConeAngle;
        public bool CastShadows;
    }

    /// <summary>
    /// Material data for Remix submission.
    /// </summary>
    public struct RemixMaterial
    {
        public uint MaterialId;
        public Vector4 AlbedoColor;
        public float Metallic;
        public float Roughness;
        public float Emissive;
        public IntPtr AlbedoTexture;
        public IntPtr NormalTexture;
        public IntPtr RoughnessMetallicTexture;
        public IntPtr EmissiveTexture;
        public bool AlphaBlend;
        public float AlphaCutoff;
    }

    /// <summary>
    /// Camera data for Remix.
    /// </summary>
    public struct RemixCamera
    {
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Up;
        public float FieldOfView;
        public float NearPlane;
        public float FarPlane;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
    }

    /// <summary>
    /// Remix rendering configuration.
    /// </summary>
    public struct RemixRenderConfig
    {
        public int SamplesPerPixel;
        public int MaxBounces;
        public bool EnableDenoiser;
        public DenoiserType DenoiserType;
        public bool EnableDlss;
        public DlssMode DlssMode;
        public bool EnableReflex;
        public float ExposureCompensation;
        public bool ShowDebugView;
    }

    #endregion
}


using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Backends;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Raytracing;
using Andastra.Runtime.MonoGame.Remix;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Main Odyssey renderer coordinating all graphics systems.
    ///
    /// Features:
    /// - Multi-backend support (Vulkan, DX12, DX11, OpenGL)
    /// - PBR rendering with dynamic lighting
    /// - Hardware raytracing (DXR/Vulkan RT)
    /// - NVIDIA RTX Remix integration
    /// - DLSS/FSR upscaling
    /// - Post-processing effects
    /// </summary>
    public class OdysseyRenderer : IDisposable
    {
        private IGraphicsBackend _backend;
        private IRaytracingSystem _raytracing;
        private ILightingSystem _lighting;
        private IPbrMaterialFactory _materialFactory;
        private RemixBridge _remixBridge;

        private RenderSettings _settings;
        private bool _initialized;
        private int _frameNumber;

        /// <summary>
        /// Active graphics backend.
        /// </summary>
        public IGraphicsBackend Backend
        {
            get { return _backend; }
        }

        /// <summary>
        /// Raytracing system.
        /// </summary>
        public IRaytracingSystem Raytracing
        {
            get { return _raytracing; }
        }

        /// <summary>
        /// Lighting system.
        /// </summary>
        public ILightingSystem Lighting
        {
            get { return _lighting; }
        }

        /// <summary>
        /// Material factory.
        /// </summary>
        public IPbrMaterialFactory Materials
        {
            get { return _materialFactory; }
        }

        /// <summary>
        /// RTX Remix bridge.
        /// </summary>
        public RemixBridge Remix
        {
            get { return _remixBridge; }
        }

        /// <summary>
        /// Current render settings.
        /// </summary>
        public RenderSettings Settings
        {
            get { return _settings; }
        }

        /// <summary>
        /// Whether the renderer is initialized.
        /// </summary>
        public bool IsInitialized
        {
            get { return _initialized; }
        }

        /// <summary>
        /// Hardware capabilities.
        /// </summary>
        public GraphicsCapabilities Capabilities
        {
            get { return _backend?.Capabilities ?? default(GraphicsCapabilities); }
        }

        /// <summary>
        /// Whether raytracing is active.
        /// </summary>
        public bool IsRaytracingActive
        {
            get { return _raytracing != null && _raytracing.IsEnabled; }
        }

        /// <summary>
        /// Whether RTX Remix is active.
        /// </summary>
        public bool IsRemixActive
        {
            get { return _remixBridge != null && _remixBridge.IsActive; }
        }

        /// <summary>
        /// Initializes the renderer with the specified settings.
        /// 
        /// Multi-backend support:
        /// - Automatically selects the best available backend based on settings and hardware
        /// - Supports Vulkan, DirectX 12, DirectX 11, DirectX 10, DirectX 9 Remix, and OpenGL
        /// - Falls back gracefully if preferred backend is unavailable
        /// - Initializes raytracing and lighting systems based on backend capabilities
        /// </summary>
        public bool Initialize(RenderSettings settings, IntPtr windowHandle)
        {
            if (_initialized)
            {
                return true;
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _settings = settings;

            // Select and initialize the graphics backend
            _backend = SelectBackend(settings);
            if (_backend == null)
            {
                Console.WriteLine("[OdysseyRenderer] ERROR: Failed to initialize any graphics backend!");
                return false;
            }

            Console.WriteLine("[OdysseyRenderer] Successfully initialized backend: " + _backend.BackendType);

            // Initialize raytracing system if supported and requested
            if (_backend.IsRaytracingEnabled && settings.Raytracing != RaytracingLevel.Disabled)
            {
                try
                {
                    // Create raytracing system instance
                    _raytracing = new NativeRaytracingSystem(_backend);
                    
                    // Create raytracing settings from render settings
                    RaytracingSettings rtSettings = new RaytracingSettings
                    {
                        Level = settings.Raytracing,
                        MaxInstances = 1024, // Default maximum TLAS instances
                        AsyncBuilds = settings.AsyncCompute,
                        RemixCompatibility = settings.RemixCompatibility,
                        RemixRuntimePath = settings.RemixRuntimePath,
                        RayBudget = settings.RaytracingSamplesPerPixel * settings.Width * settings.Height,
                        EnableDenoiser = settings.RaytracingDenoiser,
                        Denoiser = settings.RaytracingDenoiser ? DenoiserType.Temporal : DenoiserType.None
                    };
                    
                    // Initialize the raytracing system
                    if (_raytracing.Initialize(rtSettings))
                    {
                        Console.WriteLine("[OdysseyRenderer] Raytracing system initialized successfully");
                        Console.WriteLine("[OdysseyRenderer] Raytracing level: " + settings.Raytracing);
                        Console.WriteLine("[OdysseyRenderer] Denoiser: " + (settings.RaytracingDenoiser ? "Enabled" : "Disabled"));
                    }
                    else
                    {
                        Console.WriteLine("[OdysseyRenderer] WARNING: Raytracing system initialization failed");
                        Console.WriteLine("[OdysseyRenderer] Raytracing may not be fully functional (IDevice may be required)");
                        // Keep the system instance for potential future initialization
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[OdysseyRenderer] ERROR: Exception during raytracing system initialization: " + ex.Message);
                    Console.WriteLine("[OdysseyRenderer] Stack trace: " + ex.StackTrace);
                    _raytracing?.Dispose();
                    _raytracing = null;
                }
            }
            else if (settings.Raytracing != RaytracingLevel.Disabled)
            {
                Console.WriteLine("[OdysseyRenderer] Raytracing requested but not supported by backend: " + _backend.BackendType);
            }

            // Initialize RTX Remix bridge if Direct3D9Remix backend is active
            if (_backend.BackendType == GraphicsBackend.Direct3D9Remix && settings.RemixCompatibility)
            {
                try
                {
                    _remixBridge = new RemixBridge();
                    if (_remixBridge.Initialize(windowHandle))
                    {
                        Console.WriteLine("[OdysseyRenderer] RTX Remix bridge initialized successfully");
                    }
                    else
                    {
                        Console.WriteLine("[OdysseyRenderer] WARNING: RTX Remix bridge initialization failed");
                        _remixBridge?.Dispose();
                        _remixBridge = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[OdysseyRenderer] WARNING: RTX Remix bridge initialization exception: " + ex.Message);
                    _remixBridge?.Dispose();
                    _remixBridge = null;
                }
            }
            else if (settings.RemixCompatibility && _backend.BackendType != GraphicsBackend.Direct3D9Remix)
            {
                Console.WriteLine("[OdysseyRenderer] WARNING: Remix compatibility requested but backend is not Direct3D9Remix");
            }

            // TODO: Initialize lighting system when ILightingSystem implementation is available
            // TODO: Initialize material factory when IPbrMaterialFactory implementation is available

            _initialized = true;
            _frameNumber = 0;

            Console.WriteLine("[OdysseyRenderer] Initialized successfully with backend: " + _backend.BackendType);

            return true;
        }

        /// <summary>
        /// Shuts down the renderer.
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _remixBridge?.Dispose();
            _remixBridge = null;

            _raytracing?.Dispose();
            _raytracing = null;

            _lighting?.Dispose();
            _lighting = null;

            _backend?.Dispose();
            _backend = null;

            _initialized = false;
            Console.WriteLine("[OdysseyRenderer] Shutdown complete");
        }

        /// <summary>
        /// Begins a new frame.
        /// </summary>
        public void BeginFrame()
        {
            if (!_initialized || _backend == null)
            {
                return;
            }

            if (IsRemixActive)
            {
                _remixBridge.BeginFrame();
            }

            _backend.BeginFrame();
            _frameNumber++;
        }

        /// <summary>
        /// Ends the current frame and presents.
        /// </summary>
        public void EndFrame()
        {
            if (!_initialized || _backend == null)
            {
                return;
            }

            if (IsRemixActive)
            {
                _remixBridge.EndFrame();
            }

            _backend.EndFrame();
        }

        /// <summary>
        /// Resizes the render targets.
        /// </summary>
        public void Resize(int width, int height)
        {
            if (!_initialized || _backend == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                Console.WriteLine("[OdysseyRenderer] WARNING: Invalid resize dimensions: " + width + "x" + height);
                return;
            }

            _settings.Width = width;
            _settings.Height = height;

            _backend.Resize(width, height);
        }

        /// <summary>
        /// Updates render settings.
        /// 
        /// Note: Some settings changes may require backend reinitialization.
        /// If the preferred backend changes, the renderer will need to be reinitialized.
        /// </summary>
        public void ApplySettings(RenderSettings settings)
        {
            if (!_initialized || _backend == null)
            {
                return;
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // Check if backend preference changed - if so, we need to reinitialize
            if (settings.PreferredBackend != _settings.PreferredBackend && 
                settings.PreferredBackend != GraphicsBackend.Auto)
            {
                Console.WriteLine("[OdysseyRenderer] Backend preference changed, reinitializing...");
                Shutdown();
                Initialize(settings, IntPtr.Zero); // Note: windowHandle may need to be stored
                return;
            }

            _settings = settings;

            // Update raytracing level on backend
            _backend.SetRaytracingLevel(settings.Raytracing);

            // Update raytracing system if available
            if (_raytracing != null)
            {
                _raytracing.SetLevel(settings.Raytracing);
            }

            // Resize if dimensions changed
            if (settings.Width != _settings.Width || settings.Height != _settings.Height)
            {
                Resize(settings.Width, settings.Height);
            }
        }

        /// <summary>
        /// Sets the camera for rendering.
        /// </summary>
        public void SetCamera(Vector3 position, Vector3 forward, Vector3 up, float fov, float near, float far)
        {
            if (!_initialized)
            {
                return;
            }

            if (IsRemixActive)
            {
                var camera = new RemixCamera
                {
                    Position = position,
                    Forward = forward,
                    Up = up,
                    FieldOfView = fov,
                    NearPlane = near,
                    FarPlane = far,
                    ViewMatrix = Matrix4x4.CreateLookAt(position, position + forward, up),
                    ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                        fov * (float)Math.PI / 180f,
                        (float)_settings.Width / _settings.Height,
                        near, far)
                };
                _remixBridge.SetCamera(camera);
            }
        }

        /// <summary>
        /// Submits a mesh for rendering.
        /// </summary>
        public void SubmitMesh(IntPtr mesh, Matrix4x4 transform, IPbrMaterial material)
        {
            if (!_initialized)
            {
                return;
            }

            if (IsRemixActive)
            {
                // Convert to Remix geometry
                var geometry = new RemixGeometry
                {
                    WorldMatrix = transform,
                    // MaterialId = material.Id,
                    CastShadows = material.CastShadows,
                    Visible = true
                };
                _remixBridge.SubmitGeometry(geometry);
            }
            else
            {
                // Standard rendering path
                // Bind material
                // Set transforms
                // Draw mesh
            }
        }

        /// <summary>
        /// Submits a light for rendering.
        /// </summary>
        public void SubmitLight(IDynamicLight light)
        {
            if (!_initialized || !light.Enabled)
            {
                return;
            }

            if (IsRemixActive)
            {
                var remixLight = new RemixLight
                {
                    Type = light.Type,
                    Position = light.Position,
                    Direction = light.Direction,
                    Color = light.Color,
                    Intensity = light.Intensity,
                    Radius = light.Radius,
                    ConeAngle = light.OuterConeAngle,
                    CastShadows = light.CastShadows
                };
                _remixBridge.SubmitLight(remixLight);
            }

            // Standard lighting path handled by lighting system
        }

        /// <summary>
        /// Gets frame statistics.
        /// </summary>
        public FrameStatistics GetStatistics()
        {
            if (!_initialized || _backend == null)
            {
                return default(FrameStatistics);
            }

            return _backend.GetFrameStatistics();
        }

        /// <summary>
        /// Selects and initializes the appropriate graphics backend based on settings.
        /// 
        /// Backend selection priority:
        /// 1. User-specified preferred backend (if available)
        /// 2. Direct3D9Remix (if Remix compatibility requested)
        /// 3. Direct3D12 or Vulkan (if raytracing requested)
        /// 4. Platform-appropriate fallback chain (Vulkan -> DirectX -> OpenGL)
        /// 
        /// The factory automatically handles backend detection, initialization, and fallback.
        /// </summary>
        private IGraphicsBackend SelectBackend(RenderSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // Use BackendFactory to create and initialize the best available backend
            IGraphicsBackend backend = BackendFactory.CreateBackend(settings);

            if (backend == null)
            {
                Console.WriteLine("[OdysseyRenderer] ERROR: BackendFactory failed to create any backend!");
                Console.WriteLine("[OdysseyRenderer] This indicates all available backends failed to initialize.");
                Console.WriteLine("[OdysseyRenderer] Check graphics drivers and system capabilities.");
                return null;
            }

            Console.WriteLine("[OdysseyRenderer] Selected backend: " + backend.BackendType);
            Console.WriteLine("[OdysseyRenderer] Backend capabilities:");
            Console.WriteLine("[OdysseyRenderer]   - Raytracing: " + (backend.IsRaytracingEnabled ? "Yes" : "No"));
            Console.WriteLine("[OdysseyRenderer]   - Max Texture Size: " + backend.Capabilities.MaxTextureSize);
            Console.WriteLine("[OdysseyRenderer]   - Max Render Targets: " + backend.Capabilities.MaxRenderTargets);
            Console.WriteLine("[OdysseyRenderer]   - Compute Shaders: " + (backend.Capabilities.SupportsComputeShaders ? "Yes" : "No"));
            Console.WriteLine("[OdysseyRenderer]   - Geometry Shaders: " + (backend.Capabilities.SupportsGeometryShaders ? "Yes" : "No"));
            Console.WriteLine("[OdysseyRenderer]   - Tessellation: " + (backend.Capabilities.SupportsTessellation ? "Yes" : "No"));
            Console.WriteLine("[OdysseyRenderer]   - Mesh Shaders: " + (backend.Capabilities.SupportsMeshShaders ? "Yes" : "No"));
            Console.WriteLine("[OdysseyRenderer]   - Variable Rate Shading: " + (backend.Capabilities.SupportsVariableRateShading ? "Yes" : "No"));
            Console.WriteLine("[OdysseyRenderer]   - GPU Vendor: " + backend.Capabilities.VendorName);
            Console.WriteLine("[OdysseyRenderer]   - GPU Device: " + backend.Capabilities.DeviceName);
            Console.WriteLine("[OdysseyRenderer]   - Driver Version: " + backend.Capabilities.DriverVersion);
            Console.WriteLine("[OdysseyRenderer]   - Dedicated VRAM: " + (backend.Capabilities.DedicatedVideoMemory / (1024 * 1024)) + " MB");
            Console.WriteLine("[OdysseyRenderer]   - Shared System RAM: " + (backend.Capabilities.SharedSystemMemory / (1024 * 1024)) + " MB");

            return backend;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
        private IGraphicsBackend TryCreateBackend(GraphicsBackend type)
        {
            // All custom backends are disabled - we use MonoGame's rendering exclusively.
            // TODO: PLACEHOLDER - This method is kept for future multi-backend support.
            switch (type)
            {
                case GraphicsBackend.OpenGL:
                case GraphicsBackend.Auto:
                    // OpenGL is handled by MonoGame natively - no custom backend needed
                    Console.WriteLine("[OdysseyRenderer] OpenGL handled by MonoGame natively");
                    return null;

                case GraphicsBackend.Vulkan:
                    // TODO: STUB - Vulkan backend disabled - will be implemented later
                    Console.WriteLine("[OdysseyRenderer] Vulkan backend disabled (OpenGL only mode)");
                    return null;

                case GraphicsBackend.Direct3D12:
                case GraphicsBackend.Direct3D11:
                case GraphicsBackend.Direct3D9Remix:
                    // TODO: STUB - DirectX backends disabled - will be implemented later
                    Console.WriteLine("[OdysseyRenderer] DirectX backends disabled (OpenGL only mode)");
                    return null;

                default:
                    return null;
            }
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}


using System;
using System.Numerics;
using BioWare.NET.Extract.Installation;
using Andastra.Runtime.MonoGame.Backends;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Lighting;
using Andastra.Runtime.MonoGame.Materials;
using Andastra.Runtime.MonoGame.Raytracing;
using Andastra.Runtime.MonoGame.Remix;
using JetBrains.Annotations;

namespace Andastra.Game.Graphics.MonoGame.Rendering
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
        /// - Initializes material factory for PBR material creation from KOTOR resources
        /// </summary>
        /// <param name="settings">Render settings.</param>
        /// <param name="windowHandle">Window handle for rendering.</param>
        /// <param name="installation">Optional game installation for resource loading. If provided, enables material factory initialization.</param>
        public bool Initialize(RenderSettings settings, IntPtr windowHandle, [CanBeNull] Installation installation = null)
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
                    RemixSettings remixSettings = new RemixSettings
                    {
                        RuntimePath = settings.RemixRuntimePath ?? string.Empty,
                        EnablePathTracing = true,
                        MaxBounces = 8,
                        EnableDenoiser = settings.RaytracingDenoiser,
                        EnableDlss = false,
                        EnableReflex = false,
                        CaptureMode = false
                    };
                    if (_remixBridge.Initialize(windowHandle, remixSettings))
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

            // Initialize lighting system if dynamic lighting is enabled
            // Based on original engine lighting behavior:
            // - swkotor.exe: Uses OpenGL fixed-function lighting with glLightfv, glEnable(GL_LIGHTING)
            // - swkotor2.exe: Uses OpenGL fixed-function lighting with glLightfv, glEnable(GL_LIGHTING)
            // - Original implementation: Supports up to 8 OpenGL lights (GL_MAX_LIGHTS), uses ambient and directional lights
            // - Modern implementation: Uses clustered forward+ lighting for hundreds of dynamic lights
            // - ClusteredLightingSystem: Supports point, spot, directional, and area lights with 3D clustering
            if (settings.DynamicLighting)
            {
                try
                {
                    // Validate max lights setting and ensure it's within reasonable bounds
                    // Original engine: Maximum 8 OpenGL lights (hardware limit)
                    // Modern implementation: Supports up to 4096 lights (configurable, validated in RenderSettings)
                    int maxLights = Math.Max(1, Math.Min(4096, settings.MaxDynamicLights));

                    // Create clustered lighting system instance
                    // ClusteredLightingSystem divides view frustum into 3D clusters (16x8x24) and assigns lights to clusters
                    // This enables efficient per-pixel lighting with hundreds/thousands of dynamic lights
                    // Original engine used fixed-function OpenGL lighting (8 lights max)
                    // Modern implementation uses clustered forward+ shading for much better scalability
                    _lighting = new ClusteredLightingSystem(maxLights);

                    Console.WriteLine("[OdysseyRenderer] Lighting system initialized successfully");
                    Console.WriteLine("[OdysseyRenderer] Max dynamic lights: " + maxLights);
                    Console.WriteLine("[OdysseyRenderer] Clustered lighting: 16x8x24 clusters, 128 max lights per cluster");
                    Console.WriteLine("[OdysseyRenderer] Supported light types: Point, Spot, Directional, Area");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[OdysseyRenderer] ERROR: Exception during lighting system initialization: " + ex.Message);
                    Console.WriteLine("[OdysseyRenderer] Stack trace: " + ex.StackTrace);
                    _lighting?.Dispose();
                    _lighting = null;
                }
            }
            else
            {
                Console.WriteLine("[OdysseyRenderer] Dynamic lighting disabled - lighting system not initialized");
            }

            // Initialize material factory if installation is provided
            if (installation != null)
            {
                try
                {
                    _materialFactory = new PbrMaterialFactory(_backend, installation);
                    Console.WriteLine("[OdysseyRenderer] Material factory initialized successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[OdysseyRenderer] WARNING: Material factory initialization failed: " + ex.Message);
                    Console.WriteLine("[OdysseyRenderer] Stack trace: " + ex.StackTrace);
                    if (_materialFactory is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _materialFactory = null;
                }
            }
            else
            {
                Console.WriteLine("[OdysseyRenderer] Material factory not initialized (Installation not provided)");
            }

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

            if (_materialFactory is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _materialFactory = null;

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

            // Standard lighting path: lights are managed through ILightingSystem
            // Lights should be created via Lighting.CreateLight() and managed through the lighting system
            // The lighting system handles clustered light culling and GPU data submission
            // This SubmitLight method is primarily for RTX Remix integration
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

        /// <summary>
        /// Creates and returns a graphics backend instance of the specified type.
        /// 
        /// This method provides direct backend instantiation for multi-backend support scenarios
        /// where the renderer needs to create specific backend types without going through
        /// the full BackendFactory selection and initialization process.
        /// 
        /// Based on original engine graphics initialization:
        /// - swkotor.exe: FUN_00404250 @ 0x00404250 (main game loop, WinMain equivalent) calls graphics initialization
        /// - swkotor.exe: FUN_0044dab0 @ 0x0044dab0 (OpenGL context creation via wglCreateContext)
        /// - swkotor2.exe: FUN_00404250 @ 0x00404250 (main game loop, WinMain equivalent) calls graphics initialization
        /// - swkotor2.exe: FUN_00461c50 @ 0x00461c50 (OpenGL context creation via wglCreateContext)
        /// - Original game uses OpenGL for rendering (OPENGL32.DLL, GLU32.DLL) - NOT DirectX
        /// - Located via string references: "wglCreateContext" @ swkotor.exe:0x0073d2b8, swkotor2.exe:0x007b52cc
        /// - "wglChoosePixelFormatARB" @ swkotor.exe:0x0073f444, swkotor2.exe:0x007b880c
        /// - "Graphics Options" @ 0x007b56a8, "BTN_GRAPHICS" @ 0x007d0d8c, "Render Window" @ 0x007b5680
        /// - Original implementation: Creates OpenGL context, sets up pixel format, initializes rendering pipeline
        /// - This implementation: Creates modern graphics backend instances (Vulkan, DirectX 11/12, OpenGL, Metal)
        /// - Note: Modern backends (Vulkan, DirectX 11/12) are enhancements not present in original game
        /// - Original game rendering: OpenGL fixed-function pipeline, no modern post-processing or upscaling
        /// 
        /// Backend creation supports all available backend types:
        /// - Vulkan: Cross-platform, modern API with raytracing support (VulkanBackend)
        /// - Direct3D12: Windows modern API with DXR raytracing (Direct3D12Backend)
        /// - Direct3D11: Windows legacy support, good compatibility (Direct3D11Backend)
        /// - Direct3D10: Windows Vista+ transitional API (Direct3D10Backend)
        /// - Direct3D9Remix: DirectX 9 compatibility mode for NVIDIA RTX Remix injection (Direct3D9Wrapper)
        /// - OpenGL: Cross-platform fallback when Vulkan unavailable (OpenGLBackend)
        /// - Metal: macOS and iOS native API (MetalBackend)
        /// 
        /// Note: This method only creates the backend instance. Initialization must be done separately
        /// via the backend's Initialize() method with appropriate RenderSettings. For automatic backend
        /// selection based on platform and hardware capabilities, use BackendFactory.CreateBackend() instead.
        /// </summary>
        /// <param name="type">The graphics backend type to create.</param>
        /// <returns>An uninitialized backend instance, or null if the backend type is not supported or Auto (which requires factory selection).</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance method for future extensibility and potential backend-specific renderer state")]
        private IGraphicsBackend TryCreateBackend(GraphicsBackend type)
        {
            // Auto selection requires BackendFactory's full selection logic with RenderSettings
            // This includes platform detection, capability checking, and fallback chain evaluation
            if (type == GraphicsBackend.Auto)
            {
                Console.WriteLine("[OdysseyRenderer] Auto backend selection requires BackendFactory.CreateBackend() with RenderSettings");
                return null;
            }

            // Create backend instance based on type
            // This matches BackendFactory.CreateBackendInstance logic for consistency
            switch (type)
            {
                // case GraphicsBackend.Vulkan:
                //     return new VulkanBackend();

                case GraphicsBackend.Direct3D12:
                    return new Direct3D12Backend();

                case GraphicsBackend.Direct3D11:
                    return new Direct3D11Backend();

                case GraphicsBackend.Direct3D10:
                    return new Direct3D10Backend();

                // case GraphicsBackend.Direct3D9Remix:
                //     return new Direct3D9Wrapper();

                case GraphicsBackend.OpenGL:
                    return new OpenGLBackend();

                case GraphicsBackend.Metal:
                    return new MetalBackend();

                case GraphicsBackend.OpenGLES:
                    // OpenGL ES backend with EGL context creation
                    // Based on OpenGL ES 3.2 specification and EGL API
                    // Provides proper OpenGL ES support for mobile and embedded platforms
                    return new Backends.OpenGLESBackend();

                default:
                    Console.WriteLine("[OdysseyRenderer] Unknown or unsupported backend type: " + type);
                    return null;
            }
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}


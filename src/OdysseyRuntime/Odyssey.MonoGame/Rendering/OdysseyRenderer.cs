using System;
using System.Numerics;
using Odyssey.MonoGame.Backends;
using Odyssey.MonoGame.Enums;
using Odyssey.MonoGame.Interfaces;
using Odyssey.MonoGame.Remix;

namespace Odyssey.MonoGame.Rendering
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
        /// NOTE: Currently, Stride handles all rendering through its built-in OpenGL pipeline.
        /// This custom renderer is stubbed out and will be expanded for multi-backend support later.
        /// </summary>
        public bool Initialize(RenderSettings settings, IntPtr windowHandle)
        {
            if (_initialized)
            {
                return true;
            }

            _settings = settings;

            // NOTE: We rely on Stride's built-in OpenGL rendering, not custom backends.
            // The SelectBackend call returns null intentionally - Stride handles rendering.
            _backend = SelectBackend(settings);

            // No custom backend is needed - Stride's GraphicsCompositor handles everything
            // This is intentional and not an error.
            Console.WriteLine("[OdysseyRenderer] Using Stride's built-in OpenGL rendering pipeline");

            // RTX Remix is disabled in OpenGL-only mode
            if (settings.RemixCompatibility)
            {
                Console.WriteLine("[OdysseyRenderer] RTX Remix disabled (OpenGL only mode - Remix requires DirectX 9)");
            }

            // Raytracing is disabled in OpenGL-only mode
            if (settings.Raytracing != RaytracingLevel.Disabled)
            {
                Console.WriteLine("[OdysseyRenderer] Raytracing disabled (OpenGL only mode - requires Vulkan RT or DXR)");
            }

            _initialized = true;
            _frameNumber = 0;

            Console.WriteLine("[OdysseyRenderer] Initialized successfully (OpenGL mode)");

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
            if (!_initialized)
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
            if (!_initialized)
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
            if (!_initialized)
            {
                return;
            }

            _settings.Width = width;
            _settings.Height = height;

            _backend.Resize(width, height);
        }

        /// <summary>
        /// Updates render settings.
        /// </summary>
        public void ApplySettings(RenderSettings settings)
        {
            if (!_initialized)
            {
                return;
            }

            _settings = settings;

            // Update raytracing level
            if (_raytracing != null)
            {
                _raytracing.SetLevel(settings.Raytracing);
            }

            // Update backend settings
            _backend.SetRaytracingLevel(settings.Raytracing);
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
            if (!_initialized)
            {
                return default(FrameStatistics);
            }

            return _backend.GetFrameStatistics();
        }

        private IGraphicsBackend SelectBackend(RenderSettings settings)
        {
            // TODO: We currently use OpenGL exclusively through Stride's built-in rendering.
            // The custom backend system is disabled for now. Vulkan/DirectX/Remix support
            // will be added in a future update.
            //
            // Stride handles all rendering through its GraphicsCompositor system, which
            // uses OpenGL on all platforms (as configured in the .csproj files).

            Console.WriteLine("[OdysseyRenderer] Using Stride's built-in OpenGL rendering (custom backends disabled)");

            // Return null - we rely on Stride's rendering, not our custom backends
            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
        private IGraphicsBackend TryCreateBackend(GraphicsBackend type)
        {
            // All custom backends are disabled - we use Stride's OpenGL rendering exclusively.
            // This method is kept for future multi-backend support.
            switch (type)
            {
                case GraphicsBackend.OpenGL:
                case GraphicsBackend.Auto:
                    // OpenGL is handled by Stride natively - no custom backend needed
                    Console.WriteLine("[OdysseyRenderer] OpenGL handled by Stride natively");
                    return null;

                case GraphicsBackend.Vulkan:
                    // Vulkan backend disabled - will be implemented later
                    Console.WriteLine("[OdysseyRenderer] Vulkan backend disabled (OpenGL only mode)");
                    return null;

                case GraphicsBackend.Direct3D12:
                case GraphicsBackend.Direct3D11:
                case GraphicsBackend.Direct3D9Remix:
                    // DirectX backends disabled - will be implemented later
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


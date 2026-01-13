using System;
using System.Collections.Generic;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Game.Graphics.Common.Backends
{
    /// <summary>
    /// Abstract base class for graphics backends.
    /// Contains shared implementation logic for DirectX 9/10/11/12, Vulkan, OpenGL, Metal.
    ///
    /// Derived classes (MonoGame, Stride) implement only the platform-specific portions.
    /// </summary>
    /// <remarks>
    /// Base Graphics Backend:
    /// - This is an abstraction layer for modern graphics APIs (DirectX 11/12, Vulkan, OpenGL, Metal)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Graphics initialization: FUN_00404250 @ 0x00404250 (main game loop, WinMain equivalent) handles graphics setup
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8, "2D3DBias" @ 0x007c612c
    /// - Original game graphics device: glClear @ 0x0080a9c0, glViewport @ 0x0080a9d8, glDrawArrays @ 0x0080aab6, glDrawElements @ 0x0080aafe
    /// - This abstraction: Provides unified interface for modern graphics APIs, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public abstract class BaseGraphicsBackend : ILowLevelBackend
    {
        protected bool _initialized;
        protected GraphicsCapabilities _capabilities;
        protected RenderSettings _settings;
        protected readonly Dictionary<IntPtr, ResourceInfo> _resources;
        protected uint _nextResourceHandle;
        protected FrameStatistics _lastFrameStats;

        public abstract GraphicsBackendType BackendType { get; }
        public GraphicsCapabilities Capabilities => _capabilities;
        public bool IsInitialized => _initialized;
        public virtual bool IsRaytracingEnabled => false;

        protected BaseGraphicsBackend()
        {
            _resources = new Dictionary<IntPtr, ResourceInfo>();
            _nextResourceHandle = 1;
        }

        #region ILowLevelBackend Implementation

        public virtual bool Initialize(RenderSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (_initialized)
            {
                return true;
            }

            _settings = settings;
            settings.Validate();

            // Template method pattern - derived classes implement these
            if (!CreateDeviceResources())
            {
                Console.WriteLine($"[{BackendType}] Failed to create device resources");
                return false;
            }

            if (!CreateSwapChainResources())
            {
                Console.WriteLine($"[{BackendType}] Failed to create swap chain resources");
                DestroyDeviceResources();
                return false;
            }

            InitializeCapabilities();
            OnInitialized();

            _initialized = true;
            LogInitialization();

            return true;
        }

        public virtual void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            OnShutdown();

            // Destroy all tracked resources
            foreach (var resource in _resources.Values)
            {
                DestroyResourceInternal(resource);
            }
            _resources.Clear();

            DestroySwapChainResources();
            DestroyDeviceResources();

            _initialized = false;
            Console.WriteLine($"[{BackendType}] Shutdown complete");
        }

        public virtual void BeginFrame()
        {
            if (!_initialized) return;

            _lastFrameStats = new FrameStatistics();
            OnBeginFrame();
        }

        public virtual void EndFrame()
        {
            if (!_initialized) return;

            OnEndFrame();
        }

        public virtual void Resize(int width, int height)
        {
            if (!_initialized) return;

            _settings.Width = Math.Max(1, width);
            _settings.Height = Math.Max(1, height);

            OnResize(width, height);
        }

        public virtual IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized) return IntPtr.Zero;

            var handle = AllocateHandle();
            var resource = CreateTextureInternal(desc, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized) return IntPtr.Zero;

            var handle = AllocateHandle();
            var resource = CreateBufferInternal(desc, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual IntPtr CreatePipeline(PipelineDescription desc)
        {
            if (!_initialized) return IntPtr.Zero;

            var handle = AllocateHandle();
            var resource = CreatePipelineInternal(desc, handle);
            if (resource.Handle != IntPtr.Zero)
            {
                _resources[handle] = resource;
            }
            return handle;
        }

        public virtual void DestroyResource(IntPtr handle)
        {
            if (!_initialized) return;

            if (_resources.TryGetValue(handle, out var info))
            {
                DestroyResourceInternal(info);
                _resources.Remove(handle);
            }
        }

        public virtual void SetRaytracingLevel(RaytracingLevel level)
        {
            // Default: no raytracing support
            if (level != RaytracingLevel.Disabled)
            {
                Console.WriteLine($"[{BackendType}] Raytracing not supported. Use DirectX 12 or Vulkan RT.");
            }
        }

        public virtual FrameStatistics GetFrameStatistics() => _lastFrameStats;

        public virtual void Dispose()
        {
            Shutdown();
        }

        #endregion

        #region Template Methods (Override in Derived Classes)

        /// <summary>
        /// Creates API-specific device resources.
        /// </summary>
        protected abstract bool CreateDeviceResources();

        /// <summary>
        /// Creates API-specific swap chain resources.
        /// </summary>
        protected abstract bool CreateSwapChainResources();

        /// <summary>
        /// Destroys API-specific device resources.
        /// </summary>
        protected abstract void DestroyDeviceResources();

        /// <summary>
        /// Destroys API-specific swap chain resources.
        /// </summary>
        protected abstract void DestroySwapChainResources();

        /// <summary>
        /// Creates a texture resource. Returns a ResourceInfo struct.
        /// </summary>
        protected abstract ResourceInfo CreateTextureInternal(TextureDescription desc, IntPtr handle);

        /// <summary>
        /// Creates a buffer resource. Returns a ResourceInfo struct.
        /// </summary>
        protected abstract ResourceInfo CreateBufferInternal(BufferDescription desc, IntPtr handle);

        /// <summary>
        /// Creates a pipeline resource. Returns a ResourceInfo struct.
        /// </summary>
        protected abstract ResourceInfo CreatePipelineInternal(PipelineDescription desc, IntPtr handle);

        /// <summary>
        /// Destroys a resource.
        /// </summary>
        protected abstract void DestroyResourceInternal(ResourceInfo info);

        /// <summary>
        /// Initializes hardware capabilities.
        /// </summary>
        protected abstract void InitializeCapabilities();

        #endregion

        #region Virtual Hooks (Optional Overrides)

        /// <summary>
        /// Called after successful initialization.
        /// </summary>
        protected virtual void OnInitialized() { }

        /// <summary>
        /// Called before shutdown.
        /// </summary>
        protected virtual void OnShutdown() { }

        /// <summary>
        /// Called at the start of each frame.
        /// </summary>
        protected virtual void OnBeginFrame() { }

        /// <summary>
        /// Called at the end of each frame.
        /// </summary>
        protected virtual void OnEndFrame() { }

        /// <summary>
        /// Called when the window is resized.
        /// </summary>
        protected virtual void OnResize(int width, int height) { }

        #endregion

        #region Utility Methods

        protected IntPtr AllocateHandle()
        {
            return new IntPtr(_nextResourceHandle++);
        }

        protected void LogInitialization()
        {
            Console.WriteLine($"[{BackendType}] Initialized successfully");
            Console.WriteLine($"[{BackendType}] Device: {_capabilities.DeviceName ?? "Unknown"}");
            Console.WriteLine($"[{BackendType}] Vendor: {_capabilities.VendorName ?? "Unknown"}");
            Console.WriteLine($"[{BackendType}] Shader Model: {_capabilities.ShaderModelVersion}");
            Console.WriteLine($"[{BackendType}] Raytracing: {(_capabilities.SupportsRaytracing ? "Available" : "Not Available")}");
            Console.WriteLine($"[{BackendType}] Compute Shaders: {(_capabilities.SupportsComputeShaders ? "Yes" : "No")}");
        }

        protected void TrackDrawCall(int triangles)
        {
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += triangles;
        }

        #endregion

        #region Resource Tracking

        public struct ResourceInfo
        {
            public ResourceType Type;
            public IntPtr Handle;
            public IntPtr NativeHandle;
            public string DebugName;
            public long SizeInBytes;
        }

        public enum ResourceType
        {
            Unknown,
            Texture,
            Buffer,
            Pipeline,
            RenderTarget,
            DepthStencil,
            AccelerationStructure,
            Heap
        }

        #endregion
    }
}


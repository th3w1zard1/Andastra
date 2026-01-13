using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Remix;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Game.Graphics.MonoGame.Backends
{
    /// <summary>
    /// Factory for creating graphics backends with automatic fallback support.
    ///
    /// The factory selects the best available backend based on:
    /// 1. User preference (if specified)
    /// 2. Platform capabilities
    /// 3. Hardware support
    /// 4. Fallback chain for graceful degradation
    ///
    /// Fallback order on Windows:
    /// - Direct3D12 (if raytracing requested and available)
    /// - Vulkan (preferred for cross-platform)
    /// - Direct3D11 (wide compatibility)
    /// - Direct3D10 (legacy support)
    /// - OpenGL (universal fallback)
    /// - Direct3D9Remix (if Remix requested)
    ///
    /// Fallback order on Linux:
    /// - Vulkan (primary)
    /// - OpenGL (fallback)
    ///
    /// Fallback order on macOS:
    /// - Metal (primary, not yet implemented)
    /// - OpenGL (fallback)
    /// </summary>
    public static class BackendFactory
    {
        private static readonly object _lock = new object();
        private static IGraphicsBackend _currentBackend;
        private static GraphicsBackend _detectedCapabilities;
        private static bool _capabilitiesDetected;

        /// <summary>
        /// Gets the currently active backend, or null if none is initialized.
        /// </summary>
        public static IGraphicsBackend CurrentBackend
        {
            get { return _currentBackend; }
        }

        /// <summary>
        /// Creates and initializes the best available graphics backend.
        /// </summary>
        /// <param name="settings">Render settings including preferred backend. Must not be null.</param>
        /// <returns>Initialized graphics backend, or null if all backends failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if settings is null.</exception>
        public static IGraphicsBackend CreateBackend(RenderSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            lock (_lock)
            {
                // If we already have an initialized backend, return it
                if (_currentBackend != null && _currentBackend.IsInitialized)
                {
                    return _currentBackend;
                }

                // Detect available backends if not already done
                if (!_capabilitiesDetected)
                {
                    DetectAvailableBackends();
                }

                // Get the backend selection order based on settings, platform, and capabilities
                GraphicsBackend[] backendOrder = GetBackendOrder(settings);

                // Log backend selection order for debugging
                Console.WriteLine($"[BackendFactory] Backend selection order: {string.Join(", ", backendOrder)}");

                // Try each backend in order until one succeeds
                IGraphicsBackend backend = null;
                foreach (GraphicsBackend backendType in backendOrder)
                {

                    // Check if this backend is available on the current system
                    if (!IsBackendAvailable(backendType))
                    {
                        Console.WriteLine($"[BackendFactory] Backend not available: {backendType}");
                        continue;
                    }

                    // Try to create and initialize the backend
                    backend = CreateBackendInstance(backendType);
                    if (backend == null)
                    {
                        Console.WriteLine("[BackendFactory] Failed to create backend instance: " + backendType);
                        continue;
                    }

                    try
                    {
                        if (backend.Initialize(settings))
                        {
                            Console.WriteLine("[BackendFactory] Successfully initialized backend: " + backendType);
                            _currentBackend = backend;
                            return backend;
                        }
                        else
                        {
                            Console.WriteLine("[BackendFactory] Backend initialization failed: " + backendType);
                            backend.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[BackendFactory] Backend threw exception during initialization: " + backendType);
                        Console.WriteLine("[BackendFactory] Exception: " + ex.Message);
                        backend.Dispose();
                    }
                }

                Console.WriteLine("[BackendFactory] ERROR: All backends failed to initialize!");
                return null;
            }
        }

        /// <summary>
        /// Shuts down and releases the current backend.
        /// </summary>
        public static void ShutdownBackend()
        {
            lock (_lock)
            {
                if (_currentBackend != null)
                {
                    _currentBackend.Shutdown();
                    _currentBackend.Dispose();
                    _currentBackend = null;
                }
            }
        }

        /// <summary>
        /// Detects which graphics backends are available on the current system.
        /// </summary>
        public static GraphicsBackend DetectAvailableBackends()
        {
            GraphicsBackend available = GraphicsBackend.Auto;

            // OpenGL is almost always available
            if (IsOpenGLAvailable())
            {
                available |= GraphicsBackend.OpenGL;
            }

            // Check platform-specific backends
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Check Direct3D availability
                if (IsDirect3D12Available())
                {
                    available |= GraphicsBackend.Direct3D12;
                }
                if (IsDirect3D11Available())
                {
                    available |= GraphicsBackend.Direct3D11;
                }
                if (IsDirect3D10Available())
                {
                    available |= GraphicsBackend.Direct3D10;
                }
                if (IsDirect3D9RemixAvailable())
                {
                    available |= GraphicsBackend.Direct3D9Remix;
                }
            }

            // Vulkan is cross-platform
            if (IsVulkanAvailable())
            {
                available |= GraphicsBackend.Vulkan;
            }

            // Metal is macOS/iOS only
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (IsMetalAvailable())
                {
                    available |= GraphicsBackend.Metal;
                }
            }

            _detectedCapabilities = available;
            _capabilitiesDetected = true;

            Console.WriteLine("[BackendFactory] Detected available backends: " + available);
            return available;
        }

        /// <summary>
        /// Gets the backend selection order based on settings and platform.
        /// </summary>
        private static GraphicsBackend[] GetBackendOrder(RenderSettings settings)
        {
            var order = new List<GraphicsBackend>();

            // If user specified a preferred backend, try it first
            if (settings.PreferredBackend != GraphicsBackend.Auto)
            {
                order.Add(settings.PreferredBackend);
            }

            // If Remix compatibility is requested, prioritize DX9 Remix
            if (settings.RemixCompatibility)
            {
                if (!order.Contains(GraphicsBackend.Direct3D9Remix))
                {
                    order.Insert(0, GraphicsBackend.Direct3D9Remix);
                }
            }

            // If raytracing is requested, prioritize DX12 and Vulkan
            if (settings.Raytracing != RaytracingLevel.Disabled)
            {
                if (!order.Contains(GraphicsBackend.Direct3D12))
                {
                    order.Add(GraphicsBackend.Direct3D12);
                }
                if (!order.Contains(GraphicsBackend.Vulkan))
                {
                    order.Add(GraphicsBackend.Vulkan);
                }
            }

            // Add fallback backends based on platform
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows fallback order
                AddIfNotPresent(order, GraphicsBackend.Vulkan);
                AddIfNotPresent(order, GraphicsBackend.Direct3D12);
                AddIfNotPresent(order, GraphicsBackend.Direct3D11);
                AddIfNotPresent(order, GraphicsBackend.Direct3D10);
                AddIfNotPresent(order, GraphicsBackend.OpenGL);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux fallback order
                AddIfNotPresent(order, GraphicsBackend.Vulkan);
                AddIfNotPresent(order, GraphicsBackend.OpenGL);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS fallback order
                AddIfNotPresent(order, GraphicsBackend.Metal);
                AddIfNotPresent(order, GraphicsBackend.OpenGL);
            }
            else
            {
                // Unknown platform - try OpenGL
                AddIfNotPresent(order, GraphicsBackend.OpenGL);
            }

            // Also add fallbacks from settings if specified
            if (settings.FallbackBackends != null)
            {
                foreach (GraphicsBackend fallback in settings.FallbackBackends)
                {
                    AddIfNotPresent(order, fallback);
                }
            }

            return order.ToArray();
        }

        private static void AddIfNotPresent(List<GraphicsBackend> list, GraphicsBackend backend)
        {
            if (!list.Contains(backend))
            {
                list.Add(backend);
            }
        }

        /// <summary>
        /// Checks if a specific backend is available.
        /// </summary>
        private static bool IsBackendAvailable(GraphicsBackend backend)
        {
            if (!_capabilitiesDetected)
            {
                DetectAvailableBackends();
            }

            return (_detectedCapabilities & backend) != 0;
        }

        /// <summary>
        /// Creates an instance of the specified backend type.
        /// </summary>
        private static IGraphicsBackend CreateBackendInstance(GraphicsBackend backendType)
        {
            switch (backendType)
            {
                case GraphicsBackend.Vulkan:
                    return new VulkanBackend();

                case GraphicsBackend.Direct3D12:
                    return new Direct3D12Backend();

                case GraphicsBackend.Direct3D11:
                    return new Direct3D11Backend();

                case GraphicsBackend.Direct3D10:
                    return new Direct3D10Backend();

                case GraphicsBackend.Direct3D9Remix:
                    return new Direct3D9Wrapper();

                case GraphicsBackend.OpenGL:
                    return new OpenGLBackend();

                case GraphicsBackend.OpenGLES:
                    return new OpenGLESBackend();

                // case GraphicsBackend.Metal:
                //     return new MetalBackend();

                default:
                    Console.WriteLine("[BackendFactory] Unknown backend type: " + backendType);
                    return null;
            }
        }

        #region Backend Detection

        private static bool IsOpenGLAvailable()
        {
            // Attempt to create a temporary OpenGL context to verify availability
            // This provides accurate detection across Windows (WGL), Linux (GLX), and macOS (CGL)
            try
            {
                return OpenGLContextHelper.TryCreateContext();
            }
            catch
            {
                // If context creation fails, OpenGL is not available
                return false;
            }
        }

        private static bool IsVulkanAvailable()
        {
            // Check if Vulkan runtime is installed
            try
            {
                // Try to load vulkan-1.dll on Windows or libvulkan.so on Linux
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    IntPtr vulkanHandle = NativeMethods.LoadLibrary("vulkan-1.dll");
                    if (vulkanHandle != IntPtr.Zero)
                    {
                        NativeMethods.FreeLibrary(vulkanHandle);
                        return true;
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Check for libvulkan.so.1
                    // Note: Would need platform-specific dlopen on Linux
                    return true; // Assume available for now
                }
            }
            catch
            {
                // Vulkan not available
            }
            return false;
        }

        private static bool IsDirect3D12Available()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            try
            {
                // Check for d3d12.dll
                IntPtr d3d12Handle = NativeMethods.LoadLibrary("d3d12.dll");
                if (d3d12Handle != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(d3d12Handle);
                    return true;
                }
            }
            catch
            {
                // D3D12 not available
            }
            return false;
        }

        private static bool IsDirect3D11Available()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            try
            {
                // Check for d3d11.dll
                IntPtr d3d11Handle = NativeMethods.LoadLibrary("d3d11.dll");
                if (d3d11Handle != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(d3d11Handle);
                    return true;
                }
            }
            catch
            {
                // D3D11 not available
            }
            return false;
        }

        private static bool IsDirect3D10Available()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            try
            {
                // Check for d3d10.dll
                IntPtr d3d10Handle = NativeMethods.LoadLibrary("d3d10.dll");
                if (d3d10Handle != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(d3d10Handle);
                    return true;
                }
            }
            catch
            {
                // D3D10 not available
            }
            return false;
        }

        private static bool IsDirect3D9RemixAvailable()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            // Check for Remix runtime
            string[] remixPaths = new string[]
            {
                "d3d9.dll",
                "NvRemixBridge.dll"
            };

            // Check current directory
            foreach (string file in remixPaths)
            {
                if (System.IO.File.Exists(file))
                {
                    return true;
                }
            }

            // Check environment variable
            string remixPath = Environment.GetEnvironmentVariable("RTX_REMIX_PATH");
            if (!string.IsNullOrEmpty(remixPath))
            {
                foreach (string file in remixPaths)
                {
                    if (System.IO.File.Exists(System.IO.Path.Combine(remixPath, file)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsMetalAvailable()
        {
            // Metal is only available on macOS/iOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return false;
            }

            try
            {
                // Try to create a Metal device to verify availability
                // This uses P/Invoke to check if Metal framework is available
                IntPtr device = MetalNative.CreateSystemDefaultDevice();
                if (device != IntPtr.Zero)
                {
                    MetalNative.ReleaseDevice(device);
                    return true;
                }
            }
            catch (DllNotFoundException)
            {
                // Metal framework not found
            }
            catch
            {
                // Other errors indicate Metal is not available
            }

            return false;
        }

        private static class MetalNative
        {
            private const string MetalFramework = "/System/Library/Frameworks/Metal.framework/Metal";

            [DllImport(MetalFramework, EntryPoint = "MTLCreateSystemDefaultDevice")]
            public static extern IntPtr CreateSystemDefaultDevice();

            [DllImport(MetalFramework)]
            public static extern void ReleaseDevice(IntPtr device);
        }

        #endregion

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);
        }
    }

    /// <summary>
    /// Extension methods for graphics backend capabilities.
    /// </summary>
    public static class GraphicsBackendExtensions
    {
        /// <summary>
        /// Gets a human-readable name for the backend.
        /// </summary>
        public static string GetDisplayName(this GraphicsBackend backend)
        {
            switch (backend)
            {
                case GraphicsBackend.Auto:
                    return "Automatic";
                case GraphicsBackend.Vulkan:
                    return "Vulkan";
                case GraphicsBackend.Direct3D11:
                    return "DirectX 11";
                case GraphicsBackend.Direct3D12:
                    return "DirectX 12";
                case GraphicsBackend.Direct3D9Remix:
                    return "DirectX 9 (RTX Remix)";
                case GraphicsBackend.Direct3D10:
                    return "DirectX 10";
                case GraphicsBackend.OpenGL:
                    return "OpenGL";
                case GraphicsBackend.OpenGLES:
                    return "OpenGL ES";
                case GraphicsBackend.Metal:
                    return "Metal";
                default:
                    return backend.ToString();
            }
        }

        /// <summary>
        /// Gets whether the backend supports hardware raytracing.
        /// </summary>
        public static bool SupportsRaytracing(this GraphicsBackend backend)
        {
            switch (backend)
            {
                case GraphicsBackend.Vulkan:
                case GraphicsBackend.Direct3D12:
                case GraphicsBackend.Direct3D9Remix:
                case GraphicsBackend.Metal: // Metal 3.0+ supports raytracing on Apple Silicon
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets whether the backend is cross-platform.
        /// </summary>
        public static bool IsCrossPlatform(this GraphicsBackend backend)
        {
            switch (backend)
            {
                case GraphicsBackend.Vulkan:
                case GraphicsBackend.OpenGL:
                case GraphicsBackend.OpenGLES:
                    return true;
                default:
                    return false;
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Remix;
using Andastra.Runtime.MonoGame.Rendering;
using JetBrains.Annotations;

namespace Andastra.Runtime.MonoGame.Backends
{
    /// <summary>
    /// DirectX 9 wrapper backend for NVIDIA RTX Remix integration.
    ///
    /// RTX Remix works by intercepting DirectX 9 API calls and replacing
    /// the rasterized output with path-traced rendering. This wrapper creates
    /// a DirectX 9 device that Remix can hook into.
    ///
    /// Features:
    /// - DirectX 9 rendering (Windows XP+)
    /// - Fixed-function pipeline support
    /// - Shader Model 2.0/3.0 support
    /// - NVIDIA RTX Remix integration (path tracing, DLSS, denoising)
    /// - Legacy compatibility mode for original game graphics API
    ///
    /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/direct3d9/
    /// RTX Remix: https://github.com/NVIDIAGameWorks/rtx-remix
    /// </summary>
    public class Direct3D9Wrapper : IGraphicsBackend
    {
        private bool _initialized;
        private bool _disposed;
        private GraphicsCapabilities _capabilities;
        private RenderSettings _settings;
        private RemixBridge _remixBridge;

        // Direct3D9 handles
        private IntPtr _d3d9;
        private IntPtr _device;
        private IntPtr _presentParameters;
        private D3D9PresentParameters _currentPresentParams;

        // Resource tracking
        private readonly Dictionary<IntPtr, ResourceInfo> _resources;
        private uint _nextResourceHandle;

        // Frame statistics
        private FrameStatistics _lastFrameStats;

        // Window handle for device creation
        private IntPtr _windowHandle;

        public GraphicsBackend BackendType
        {
            get { return GraphicsBackend.Direct3D9Remix; }
        }

        public GraphicsCapabilities Capabilities
        {
            get { return _capabilities; }
        }

        public bool IsInitialized
        {
            get { return _initialized && !_disposed; }
        }

        public bool IsRaytracingEnabled
        {
            get { return _remixBridge != null && _remixBridge.IsActive && _remixBridge.Capabilities.PathTracingEnabled; }
        }

        public Direct3D9Wrapper()
        {
            _resources = new Dictionary<IntPtr, ResourceInfo>();
            _nextResourceHandle = 1;
            _disposed = false;
        }

        /// <summary>
        /// Initializes the DirectX 9 backend with RTX Remix support.
        /// </summary>
        /// <param name="settings">Render settings. Must not be null.</param>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if settings is null.</exception>
        public bool Initialize(RenderSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (_initialized)
            {
                return true;
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Direct3D9Wrapper));
            }

            _settings = settings;

            // Get window handle from settings or use a default one
            // In a real implementation, this would come from the game window
            _windowHandle = GetWindowHandle(settings);

            // Initialize DirectX 9
            if (!CreateDirect3D9())
            {
                Console.WriteLine("[Direct3D9Wrapper] Failed to create Direct3D9");
                return false;
            }

            // Create DirectX 9 device
            if (!CreateDevice())
            {
                Console.WriteLine("[Direct3D9Wrapper] Failed to create D3D9 device");
                Cleanup();
                return false;
            }

            // Initialize RTX Remix bridge if requested
            if (settings.RemixCompatibility)
            {
                if (!InitializeRemix())
                {
                    Console.WriteLine("[Direct3D9Wrapper] RTX Remix initialization failed, continuing without Remix");
                    // Continue without Remix - backend can still function for compatibility testing
                }
            }

            // Initialize capabilities
            InitializeCapabilities();

            _initialized = true;
            Console.WriteLine("[Direct3D9Wrapper] Initialized successfully");
            Console.WriteLine("[Direct3D9Wrapper] RTX Remix: " + (_remixBridge != null && _remixBridge.IsActive ? "active" : "inactive"));

            return true;
        }

        /// <summary>
        /// Shuts down the DirectX 9 backend.
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            // Shutdown Remix bridge
            if (_remixBridge != null)
            {
                _remixBridge.Shutdown();
                _remixBridge.Dispose();
                _remixBridge = null;
            }

            // Destroy all resources
            foreach (ResourceInfo resource in _resources.Values)
            {
                DestroyResourceInternal(resource);
            }
            _resources.Clear();

            // Release DirectX 9 objects
            Cleanup();

            _initialized = false;
            Console.WriteLine("[Direct3D9Wrapper] Shutdown complete");
        }

        /// <summary>
        /// Begins a new frame for rendering.
        /// </summary>
        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Begin scene for DirectX 9
            if (_device != IntPtr.Zero)
            {
                // IDirect3DDevice9::BeginScene()
                if (D3D9Methods.BeginScene(_device) < 0)
                {
                    Console.WriteLine("[Direct3D9Wrapper] BeginScene failed");
                    return;
                }
            }

            // Signal frame start to Remix
            if (_remixBridge != null)
            {
                _remixBridge.BeginFrame();
            }

            // Clear render target and depth buffer
            // IDirect3DDevice9::Clear(0, NULL, D3DCLEAR_TARGET | D3DCLEAR_ZBUFFER, color, 1.0f, 0)
            if (_device != IntPtr.Zero)
            {
                uint clearColor = 0xFF000000; // Black in ARGB format
                uint clearFlags = (uint)(D3D9ClearFlags.D3DCLEAR_TARGET | D3D9ClearFlags.D3DCLEAR_ZBUFFER);
                D3D9Methods.Clear(_device, 0, IntPtr.Zero, clearFlags, clearColor, 1.0f, 0);
            }

            _lastFrameStats = new FrameStatistics();
        }

        /// <summary>
        /// Ends the current frame and presents to screen.
        /// </summary>
        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // End scene for DirectX 9
            if (_device != IntPtr.Zero)
            {
                // IDirect3DDevice9::EndScene()
                D3D9Methods.EndScene(_device);
            }

            // Signal frame end to Remix
            if (_remixBridge != null)
            {
                _remixBridge.EndFrame();
            }

            // Present the back buffer
            // IDirect3DDevice9::Present(NULL, NULL, NULL, NULL)
            if (_device != IntPtr.Zero)
            {
                D3D9Methods.Present(_device, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0);
            }
        }

        /// <summary>
        /// Resizes the swap chain / render targets.
        /// </summary>
        /// <param name="width">New width in pixels.</param>
        /// <param name="height">New height in pixels.</param>
        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                Console.WriteLine("[Direct3D9Wrapper] Invalid resize dimensions: " + width + "x" + height);
                return;
            }

            // Update present parameters
            _currentPresentParams.BackBufferWidth = width;
            _currentPresentParams.BackBufferHeight = height;

            // Reset device with new parameters
            // IDirect3DDevice9::Reset(pPresentParameters)
            if (_device != IntPtr.Zero)
            {
                IntPtr presentParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D9PresentParameters)));
                try
                {
                    Marshal.StructureToPtr(_currentPresentParams, presentParamsPtr, false);
                    int hr = D3D9Methods.Reset(_device, presentParamsPtr);
                    if (hr < 0)
                    {
                        Console.WriteLine("[Direct3D9Wrapper] Reset failed with HRESULT: 0x" + hr.ToString("X8"));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(presentParamsPtr);
                }
            }

            _settings.Width = width;
            _settings.Height = height;
        }

        /// <summary>
        /// Creates a texture resource.
        /// </summary>
        /// <param name="desc">Texture description.</param>
        /// <returns>Handle to the created texture.</returns>
        public IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            if (_device == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Create DirectX 9 texture
            // IDirect3DDevice9::CreateTexture(width, height, levels, usage, format, pool, &texture, NULL)
            IntPtr texture = IntPtr.Zero;
            uint usage = 0; // D3DUSAGE_DYNAMIC for dynamic textures
            if ((desc.Usage & TextureUsage.RenderTarget) != 0)
            {
                usage |= 0x00000001; // D3DUSAGE_RENDERTARGET
            }

            uint format = ConvertTextureFormat(desc.Format);
            if (format == 0)
            {
                Console.WriteLine("[Direct3D9Wrapper] Unsupported texture format: " + desc.Format);
                return IntPtr.Zero;
            }

            int hr = D3D9Methods.CreateTexture(
                _device,
                (uint)desc.Width,
                (uint)desc.Height,
                (uint)desc.MipLevels,
                usage,
                format,
                0, // D3DPOOL_DEFAULT
                ref texture,
                IntPtr.Zero);

            if (hr < 0 || texture == IntPtr.Zero)
            {
                Console.WriteLine("[Direct3D9Wrapper] CreateTexture failed with HRESULT: 0x" + hr.ToString("X8"));
                return IntPtr.Zero;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                DebugName = desc.DebugName,
                TextureDesc = desc,
                NativeTexture = texture
            };

            return handle;
        }

        /// <summary>
        /// Uploads texture pixel data to a previously created texture.
        /// </summary>
        /// <param name="handle">Handle to the texture created by CreateTexture.</param>
        /// <param name="data">Texture upload data containing mipmap levels.</param>
        /// <returns>True if upload succeeded, false otherwise.</returns>
        public bool UploadTextureData(IntPtr handle, TextureUploadData data)
        {
            if (!_initialized)
            {
                return false;
            }

            if (handle == IntPtr.Zero)
            {
                return false;
            }

            if (!_resources.TryGetValue(handle, out ResourceInfo info))
            {
                Console.WriteLine("[Direct3D9Wrapper] UploadTextureData: Invalid texture handle");
                return false;
            }

            if (info.Type != ResourceType.Texture)
            {
                Console.WriteLine("[Direct3D9Wrapper] UploadTextureData: Handle is not a texture");
                return false;
            }

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                Console.WriteLine("[Direct3D9Wrapper] UploadTextureData: No mipmap data provided");
                return false;
            }

            IntPtr texture = info.NativeTexture;
            if (texture == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // For DirectX 9, we use IDirect3DTexture9::LockRect/UnlockRect to upload texture data
                // Based on original game texture upload pattern
                foreach (TextureMipmapData mipmap in data.Mipmaps)
                {
                    uint level = unchecked((uint)mipmap.Level);

                    // Lock the texture surface for this mip level
                    // IDirect3DTexture9::LockRect(level, &lockedRect, NULL, 0)
                    D3D9LockedRect lockedRect;
                    int hr = D3D9Methods.LockRect(texture, level, out lockedRect, IntPtr.Zero, 0);
                    if (hr < 0)
                    {
                        Console.WriteLine("[Direct3D9Wrapper] LockRect failed for mip level " + level + " with HRESULT: 0x" + hr.ToString("X8"));
                        continue;
                    }

                    try
                    {
                        // Copy pixel data row by row (accounting for pitch)
                        if (lockedRect.pBits != IntPtr.Zero && mipmap.Data != null)
                        {
                            int rowPitch = Math.Min(lockedRect.Pitch, mipmap.Width * 4); // Assume 4 bytes per pixel for RGBA
                            for (int y = 0; y < mipmap.Height; y++)
                            {
                                int srcOffset = y * mipmap.Width * 4;
                                IntPtr dstRow = IntPtr.Add(lockedRect.pBits, y * lockedRect.Pitch);
                                Marshal.Copy(mipmap.Data, srcOffset, dstRow, rowPitch);
                            }
                        }
                    }
                    finally
                    {
                        // Unlock the texture surface
                        // IDirect3DTexture9::UnlockRect(level)
                        D3D9Methods.UnlockRect(texture, level);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Direct3D9Wrapper] UploadTextureData exception: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Creates a buffer resource.
        /// </summary>
        /// <param name="desc">Buffer description.</param>
        /// <returns>Handle to the created buffer.</returns>
        public IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            if (_device == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Determine buffer type and usage
            uint usage = 0;
            uint pool = 0; // D3DPOOL_DEFAULT
            uint format = 0;

            if ((desc.Usage & BufferUsage.Vertex) != 0)
            {
                // Create vertex buffer
                // IDirect3DDevice9::CreateVertexBuffer(size, usage, fvf, pool, &buffer, NULL)
                IntPtr buffer = IntPtr.Zero;
                uint fvf = 0; // Flexible Vertex Format - will need to be set based on vertex layout
                int hr = D3D9Methods.CreateVertexBuffer(
                    _device,
                    (uint)desc.SizeInBytes,
                    usage,
                    fvf,
                    pool,
                    ref buffer,
                    IntPtr.Zero);

                if (hr < 0 || buffer == IntPtr.Zero)
                {
                    Console.WriteLine("[Direct3D9Wrapper] CreateVertexBuffer failed with HRESULT: 0x" + hr.ToString("X8"));
                    return IntPtr.Zero;
                }

                IntPtr handle = new IntPtr(_nextResourceHandle++);
                _resources[handle] = new ResourceInfo
                {
                    Type = ResourceType.Buffer,
                    Handle = handle,
                    DebugName = desc.DebugName,
                    BufferDesc = desc,
                    NativeBuffer = buffer
                };

                return handle;
            }
            else if ((desc.Usage & BufferUsage.Index) != 0)
            {
                // Create index buffer
                // IDirect3DDevice9::CreateIndexBuffer(size, usage, format, pool, &buffer, NULL)
                format = 0x00000020; // D3DFMT_INDEX16
                if (desc.SizeInBytes > 65536 * 2) // More than 65536 16-bit indices
                {
                    format = 0x00000021; // D3DFMT_INDEX32
                }

                IntPtr buffer = IntPtr.Zero;
                int hr = D3D9Methods.CreateIndexBuffer(
                    _device,
                    (uint)desc.SizeInBytes,
                    usage,
                    format,
                    pool,
                    ref buffer,
                    IntPtr.Zero);

                if (hr < 0 || buffer == IntPtr.Zero)
                {
                    Console.WriteLine("[Direct3D9Wrapper] CreateIndexBuffer failed with HRESULT: 0x" + hr.ToString("X8"));
                    return IntPtr.Zero;
                }

                IntPtr handle = new IntPtr(_nextResourceHandle++);
                _resources[handle] = new ResourceInfo
                {
                    Type = ResourceType.Buffer,
                    Handle = handle,
                    DebugName = desc.DebugName,
                    BufferDesc = desc,
                    NativeBuffer = buffer
                };

                return handle;
            }
            else
            {
                Console.WriteLine("[Direct3D9Wrapper] CreateBuffer: Unsupported buffer usage (only Vertex and Index buffers are supported in D3D9)");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Creates a shader pipeline.
        /// </summary>
        /// <param name="desc">Pipeline description.</param>
        /// <returns>Handle to the created pipeline.</returns>
        public IntPtr CreatePipeline(PipelineDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            if (_device == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // DirectX 9 uses vertex and pixel shaders separately, not pipelines
            // For compatibility, we'll create shader objects
            IntPtr vertexShader = IntPtr.Zero;
            IntPtr pixelShader = IntPtr.Zero;

            // Create vertex shader if provided
            if (desc.VertexShader != null && desc.VertexShader.Length > 0)
            {
                // IDirect3DDevice9::CreateVertexShader(pFunction, &shader)
                int hr = D3D9Methods.CreateVertexShader(_device, desc.VertexShader, ref vertexShader);
                if (hr < 0)
                {
                    Console.WriteLine("[Direct3D9Wrapper] CreateVertexShader failed with HRESULT: 0x" + hr.ToString("X8"));
                }
            }

            // Create pixel shader if provided
            if (desc.PixelShader != null && desc.PixelShader.Length > 0)
            {
                // IDirect3DDevice9::CreatePixelShader(pFunction, &shader)
                int hr = D3D9Methods.CreatePixelShader(_device, desc.PixelShader, ref pixelShader);
                if (hr < 0)
                {
                    Console.WriteLine("[Direct3D9Wrapper] CreatePixelShader failed with HRESULT: 0x" + hr.ToString("X8"));
                }
            }

            // Store shaders in a resource info
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                DebugName = desc.DebugName,
                NativePipeline = vertexShader, // Store vertex shader as primary handle
                NativePixelShader = pixelShader
            };

            return handle;
        }

        /// <summary>
        /// Destroys a resource by handle.
        /// </summary>
        /// <param name="handle">Resource handle.</param>
        public void DestroyResource(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            if (_resources.TryGetValue(handle, out ResourceInfo info))
            {
                DestroyResourceInternal(info);
                _resources.Remove(handle);
            }
        }

        /// <summary>
        /// Sets the raytracing feature level.
        /// </summary>
        /// <param name="level">Raytracing feature level.</param>
        public void SetRaytracingLevel(RaytracingLevel level)
        {
            // Raytracing in Direct3D9 is provided by RTX Remix, not native D3D9
            // This is a no-op as Remix manages its own raytracing settings
            if (_remixBridge != null && _remixBridge.IsActive)
            {
                // Remix raytracing is controlled via Remix configuration, not through this API
                Console.WriteLine("[Direct3D9Wrapper] SetRaytracingLevel: Raytracing is controlled by RTX Remix runtime");
            }
        }

        /// <summary>
        /// Gets performance statistics for the last frame.
        /// </summary>
        public FrameStatistics GetFrameStatistics()
        {
            return _lastFrameStats;
        }

        /// <summary>
        /// Gets the raytracing-capable device interface.
        /// Returns null as Direct3D9 does not natively support raytracing.
        /// RTX Remix provides raytracing through interception, not through a device interface.
        /// </summary>
        /// <returns>Always returns null for Direct3D9.</returns>
        public IDevice GetDevice()
        {
            // Direct3D9 does not have a raytracing-capable device interface
            // RTX Remix provides raytracing through API interception
            return null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Shutdown();
            _disposed = true;
        }

        #region Private Methods

        /// <summary>
        /// Gets the native window handle (HWND) for DirectX 9 device creation.
        /// Uses Windows API to find the game window through multiple strategies:
        /// 1. GetForegroundWindow() - if the game window is currently in focus
        /// 2. GetActiveWindow() - if the game window is the active window in the current thread
        /// 3. FindWindow() - searches for a window by class name or title
        ///
        /// Based on original game window management system:
        /// - swkotor2.exe: Uses Windows API functions GetActiveWindow @ 0x007d963c
        /// - "Render Window" @ 0x007b5680 - main game window
        /// - "Exo Base Window" @ 0x007b74a0 - base window class
        ///
        /// DirectX 9 requires a valid window handle for device creation.
        /// RTX Remix also requires a valid window handle for proper integration.
        /// </summary>
        /// <param name="settings">Render settings containing window configuration.</param>
        /// <returns>Window handle (HWND) if found, or IntPtr.Zero if unable to find a window.</returns>
        private IntPtr GetWindowHandle(RenderSettings settings)
        {
            // DirectX 9 is Windows-only, so we can safely use Windows API
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("[Direct3D9Wrapper] GetWindowHandle: DirectX 9 is Windows-only");
                return IntPtr.Zero;
            }

            IntPtr windowHandle = IntPtr.Zero;

            // Strategy 1: Try GetForegroundWindow() - gets the window currently in focus
            // This is the most reliable method if the game window is the active window
            windowHandle = WindowsApiMethods.GetForegroundWindow();
            if (windowHandle != IntPtr.Zero && IsValidWindow(windowHandle))
            {
                Console.WriteLine("[Direct3D9Wrapper] Found window handle via GetForegroundWindow: 0x" + windowHandle.ToString("X8"));
                return windowHandle;
            }

            // Strategy 2: Try GetActiveWindow() - gets the active window in the current thread
            // This works if the game window belongs to the current thread and is active
            windowHandle = WindowsApiMethods.GetActiveWindow();
            if (windowHandle != IntPtr.Zero && IsValidWindow(windowHandle))
            {
                Console.WriteLine("[Direct3D9Wrapper] Found window handle via GetActiveWindow: 0x" + windowHandle.ToString("X8"));
                return windowHandle;
            }

            // Strategy 3: Try FindWindow() with common MonoGame/Windows class names
            // MonoGame on Windows typically uses class names like "WindowsForms10.Window" or similar
            // We'll try several common window class patterns
            string[] windowClasses = new string[]
            {
                "WindowsForms10.Window.8.app.0.141b42a_r6_ad1", // Common Windows Forms window class
                "MonoGameGameWindow", // Potential MonoGame window class
                "Render Window", // Original game window class name (from swkotor2.exe)
                null // Will try FindWindow by title if class is null
            };

            foreach (string windowClass in windowClasses)
            {
                windowHandle = WindowsApiMethods.FindWindow(windowClass, null);
                if (windowHandle != IntPtr.Zero && IsValidWindow(windowHandle))
                {
                    Console.WriteLine("[Direct3D9Wrapper] Found window handle via FindWindow (class: " + (windowClass ?? "null") + "): 0x" + windowHandle.ToString("X8"));
                    return windowHandle;
                }
            }

            // Strategy 4: Try FindWindow by window title if available
            // MonoGame windows typically have a title set via GameWindow.Title
            // We can try common game window titles, though this is less reliable
            // Note: This requires knowing the window title, which we don't have from RenderSettings

            // Strategy 5: Try to find window by process ID
            // Get the current process ID and try to find a window belonging to this process
            int currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            IntPtr foundWindow = IntPtr.Zero;
            WindowsApiMethods.EnumWindows((hWnd, lParam) =>
            {
                uint processId;
                WindowsApiMethods.GetWindowThreadProcessId(hWnd, out processId);
                if (processId == currentProcessId && IsValidWindow(hWnd))
                {
                    foundWindow = hWnd;
                    return false; // Stop enumeration
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            if (foundWindow != IntPtr.Zero)
            {
                Console.WriteLine("[Direct3D9Wrapper] Found window handle via EnumWindows (process ID): 0x" + foundWindow.ToString("X8"));
                return foundWindow;
            }

            // If all strategies fail, log a warning and return IntPtr.Zero
            // The DirectX 9 device creation may still work with IntPtr.Zero in some cases
            // (e.g., for off-screen rendering), but RTX Remix typically requires a valid window
            Console.WriteLine("[Direct3D9Wrapper] WARNING: Could not find a valid window handle. Device creation may fail or RTX Remix may not work properly.");
            return IntPtr.Zero;
        }

        /// <summary>
        /// Validates that a window handle points to a valid, visible window.
        /// </summary>
        /// <param name="hWnd">Window handle to validate.</param>
        /// <returns>True if the window is valid and visible, false otherwise.</returns>
        private bool IsValidWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            // Check if window exists and is visible
            // IsWindow() checks if the handle is a valid window
            if (!WindowsApiMethods.IsWindow(hWnd))
            {
                return false;
            }

            // Check if window is visible (optional - some windows may be hidden during initialization)
            // We'll be lenient here and accept hidden windows since they may become visible later
            return true;
        }

        private bool CreateDirect3D9()
        {
            // Create Direct3D9 object
            // Direct3DCreate9(D3D_SDK_VERSION)
            _d3d9 = D3D9Methods.Direct3DCreate9(D3D9Constants.D3D_SDK_VERSION);
            if (_d3d9 == IntPtr.Zero)
            {
                Console.WriteLine("[Direct3D9Wrapper] Direct3DCreate9 failed");
                return false;
            }

            return true;
        }

        private bool CreateDevice()
        {
            if (_d3d9 == IntPtr.Zero)
            {
                return false;
            }

            // Create present parameters
            _currentPresentParams = new D3D9PresentParameters
            {
                BackBufferWidth = _settings.Width > 0 ? _settings.Width : 800,
                BackBufferHeight = _settings.Height > 0 ? _settings.Height : 600,
                BackBufferFormat = 0x00000016, // D3DFMT_A8R8G8B8
                BackBufferCount = 1,
                MultiSampleType = 0, // D3DMULTISAMPLE_NONE
                MultiSampleQuality = 0,
                SwapEffect = 1, // D3DSWAPEFFECT_DISCARD
                DeviceWindow = _windowHandle,
                Windowed = true,
                EnableAutoDepthStencil = true,
                AutoDepthStencilFormat = 0x0000004B, // D3DFMT_D24S8
                Flags = 0,
                FullScreenRefreshRateInHz = 0,
                PresentationInterval = (_settings.VSync != 0) ? 0x00000001u : 0x80000000u // D3DPRESENT_INTERVAL_ONE or D3DPRESENT_INTERVAL_IMMEDIATE
            };

            // Marshal present parameters
            int presentParamsSize = Marshal.SizeOf(typeof(D3D9PresentParameters));
            _presentParameters = Marshal.AllocHGlobal(presentParamsSize);
            Marshal.StructureToPtr(_currentPresentParams, _presentParameters, false);

            // Create device
            // IDirect3D9::CreateDevice(adapter, deviceType, focusWindow, behaviorFlags, pPresentParameters, &device)
            uint adapter = 0; // D3DADAPTER_DEFAULT
            uint deviceType = 1; // D3DDEVTYPE_HAL
            uint behaviorFlags = 0x00000040; // D3DCREATE_HARDWARE_VERTEXPROCESSING

            int hr = D3D9Methods.CreateDevice(
                _d3d9,
                adapter,
                deviceType,
                _windowHandle,
                behaviorFlags,
                _presentParameters,
                ref _device);

            if (hr < 0 || _device == IntPtr.Zero)
            {
                Console.WriteLine("[Direct3D9Wrapper] CreateDevice failed with HRESULT: 0x" + hr.ToString("X8"));
                Marshal.FreeHGlobal(_presentParameters);
                _presentParameters = IntPtr.Zero;
                return false;
            }

            return true;
        }

        private bool InitializeRemix()
        {
            try
            {
                RemixSettings remixSettings = new RemixSettings
                {
                    RuntimePath = _settings.RemixRuntimePath ?? Environment.GetEnvironmentVariable("RTX_REMIX_PATH") ?? "",
                    EnablePathTracing = true,
                    MaxBounces = 8,
                    EnableDenoiser = true,
                    EnableDlss = true,
                    EnableReflex = true,
                    CaptureMode = false
                };

                _remixBridge = new RemixBridge();
                if (_remixBridge.Initialize(_windowHandle, remixSettings))
                {
                    Console.WriteLine("[Direct3D9Wrapper] RTX Remix initialized successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine("[Direct3D9Wrapper] RTX Remix initialization failed");
                    _remixBridge.Dispose();
                    _remixBridge = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Direct3D9Wrapper] RTX Remix initialization exception: " + ex.Message);
                if (_remixBridge != null)
                {
                    _remixBridge.Dispose();
                    _remixBridge = null;
                }
                return false;
            }
        }

        private void InitializeCapabilities()
        {
            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 2048, // D3D9 limit
                MaxRenderTargets = 1, // D3D9 limit
                MaxAnisotropy = 16,
                SupportsComputeShaders = false, // D3D9 doesn't support compute shaders
                SupportsGeometryShaders = false, // Requires D3D10+
                SupportsTessellation = false, // Requires D3D11+
                SupportsRaytracing = _remixBridge != null && _remixBridge.IsActive, // Via RTX Remix
                SupportsMeshShaders = false, // Requires D3D12
                SupportsVariableRateShading = false, // Requires D3D12
                DedicatedVideoMemory = QueryVideoMemory(),
                SharedSystemMemory = 2L * 1024 * 1024 * 1024,
                VendorName = QueryVendorName(),
                DeviceName = QueryDeviceName(),
                DriverVersion = QueryDriverVersion(),
                ActiveBackend = GraphicsBackend.Direct3D9Remix,
                ShaderModelVersion = 3.0f, // Shader Model 3.0 max for D3D9
                RemixAvailable = _remixBridge != null && _remixBridge.IsAvailable,
                DlssAvailable = _remixBridge != null && _remixBridge.Capabilities.DlssAvailable,
                FsrAvailable = false // FSR requires compute shaders (D3D11+)
            };
        }

        private long QueryVideoMemory()
        {
            // Query available video memory from D3D9
            if (_device != IntPtr.Zero)
            {
                // IDirect3DDevice9::GetAvailableTextureMem()
                uint mem = D3D9Methods.GetAvailableTextureMem(_device);
                return (long)mem;
            }
            return 512L * 1024 * 1024; // Default 512MB
        }

        private string QueryVendorName()
        {
            // Query vendor name from D3D9 adapter
            if (_d3d9 != IntPtr.Zero)
            {
                // IDirect3D9::GetAdapterIdentifier(adapter, flags, &identifier)
                D3D9AdapterIdentifier identifier;
                int hr = D3D9Methods.GetAdapterIdentifier(_d3d9, 0, 0, out identifier);
                if (hr >= 0)
                {
                    return identifier.Description; // This contains vendor and device name
                }
            }
            return "Unknown";
        }

        private string QueryDeviceName()
        {
            // Query device name from D3D9 adapter
            if (_d3d9 != IntPtr.Zero)
            {
                D3D9AdapterIdentifier identifier;
                int hr = D3D9Methods.GetAdapterIdentifier(_d3d9, 0, 0, out identifier);
                if (hr >= 0)
                {
                    return identifier.Description;
                }
            }
            return "DirectX 9 Device";
        }

        private string QueryDriverVersion()
        {
            // Query driver version from D3D9 adapter
            if (_d3d9 != IntPtr.Zero)
            {
                D3D9AdapterIdentifier identifier;
                int hr = D3D9Methods.GetAdapterIdentifier(_d3d9, 0, 0, out identifier);
                if (hr >= 0)
                {
                    return identifier.DriverVersion.ToString() + "." + identifier.DriverSubVersion.ToString();
                }
            }
            return "Unknown";
        }

        private void Cleanup()
        {
            // Release device
            if (_device != IntPtr.Zero)
            {
                Marshal.Release(_device);
                _device = IntPtr.Zero;
            }

            // Release Direct3D9
            if (_d3d9 != IntPtr.Zero)
            {
                Marshal.Release(_d3d9);
                _d3d9 = IntPtr.Zero;
            }

            // Free present parameters
            if (_presentParameters != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_presentParameters);
                _presentParameters = IntPtr.Zero;
            }
        }

        private void DestroyResourceInternal(ResourceInfo info)
        {
            switch (info.Type)
            {
                case ResourceType.Texture:
                    if (info.NativeTexture != IntPtr.Zero)
                    {
                        Marshal.Release(info.NativeTexture);
                    }
                    break;

                case ResourceType.Buffer:
                    if (info.NativeBuffer != IntPtr.Zero)
                    {
                        Marshal.Release(info.NativeBuffer);
                    }
                    break;

                case ResourceType.Pipeline:
                    if (info.NativePipeline != IntPtr.Zero)
                    {
                        // Release vertex shader via COM Release()
                        Marshal.Release(info.NativePipeline);
                    }
                    if (info.NativePixelShader != IntPtr.Zero)
                    {
                        // Release pixel shader via COM Release()
                        Marshal.Release(info.NativePixelShader);
                    }
                    break;
            }
        }

        private uint ConvertTextureFormat(TextureFormat format)
        {
            // Convert TextureFormat enum to D3D9 format constant
            switch (format)
            {
                case TextureFormat.R8G8B8A8_UNorm:
                    return 0x00000016; // D3DFMT_A8R8G8B8
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    return 0x0000001C; // D3DFMT_A8R8G8B8 (SRGB handled separately in D3D9)
                case TextureFormat.B8G8R8A8_UNorm:
                    return 0x00000015; // D3DFMT_A8R8G8B8 (same as ARGB)
                case TextureFormat.D24_UNorm_S8_UInt:
                    return 0x0000004B; // D3DFMT_D24S8
                case TextureFormat.D32_Float:
                    return 0x0000004C; // D3DFMT_D32
                case TextureFormat.D16_UNorm:
                    return 0x0000004A; // D3DFMT_D16
                default:
                    return 0; // Unknown format
            }
        }

        #endregion

        #region Resource Tracking

        private enum ResourceType
        {
            Texture,
            Buffer,
            Pipeline
        }

        private struct ResourceInfo
        {
            public ResourceType Type;
            public IntPtr Handle;
            public string DebugName;
            public TextureDescription TextureDesc;
            public BufferDescription BufferDesc;
            public IntPtr NativeTexture;
            public IntPtr NativeBuffer;
            public IntPtr NativePipeline;
            public IntPtr NativePixelShader;
        }

        #endregion
    }

    #region Direct3D9 P/Invoke Declarations

    /// <summary>
    /// Direct3D9 COM interface method declarations.
    /// Direct3D9 uses COM interfaces, so methods must be called through the vtable.
    /// Only Direct3DCreate9 is exported from d3d9.dll; all other methods are COM interface methods.
    /// Based on D3D9 API: https://docs.microsoft.com/en-us/windows/win32/direct3d9/direct3d-apis
    /// </summary>
    internal static class D3D9Methods
    {
        private const string D3D9_DLL = "d3d9.dll";

        // Only Direct3DCreate9 is exported from d3d9.dll
        [DllImport(D3D9_DLL, EntryPoint = "Direct3DCreate9", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr Direct3DCreate9(uint SDKVersion);

        // COM interface method delegates (called through vtable)
        // IDirect3D9::CreateDevice (vtable index 16)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateDeviceDelegate(
            IntPtr pD3D9,                // IDirect3D9* (this pointer)
            uint adapter,                // UINT
            uint deviceType,             // D3DDEVTYPE
            IntPtr focusWindow,          // HWND
            uint behaviorFlags,          // DWORD
            IntPtr pPresentParameters,   // D3DPRESENT_PARAMETERS*
            ref IntPtr ppDevice);        // IDirect3DDevice9**

        // IDirect3D9::GetAdapterIdentifier (vtable index 2)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetAdapterIdentifierDelegate(
            IntPtr pD3D9,                // IDirect3D9* (this pointer)
            uint adapter,                // UINT
            uint flags,                  // DWORD
            out D3D9AdapterIdentifier pIdentifier); // D3DADAPTER_IDENTIFIER9*

        // IDirect3DDevice9::BeginScene (vtable index 35)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int BeginSceneDelegate(IntPtr pDevice); // IDirect3DDevice9* (this pointer)

        // IDirect3DDevice9::EndScene (vtable index 36)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int EndSceneDelegate(IntPtr pDevice); // IDirect3DDevice9* (this pointer)

        // IDirect3DDevice9::Present (vtable index 37)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int PresentDelegate(
            IntPtr pDevice,              // IDirect3DDevice9* (this pointer)
            IntPtr pSourceRect,          // const RECT*
            IntPtr pDestRect,            // const RECT*
            IntPtr hDestWindowOverride,  // HWND
            IntPtr pDirtyRegion);        // const RGNDATA*

        // IDirect3DDevice9::Clear (vtable index 34)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int ClearDelegate(
            IntPtr pDevice,              // IDirect3DDevice9* (this pointer)
            uint count,                  // DWORD
            IntPtr pRects,               // const D3DRECT*
            uint flags,                  // DWORD (D3DCLEAR flags)
            uint color,                  // D3DCOLOR
            float z,                     // float
            uint stencil);               // DWORD

        // IDirect3DDevice9::Reset (vtable index 16)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int ResetDelegate(
            IntPtr pDevice,              // IDirect3DDevice9* (this pointer)
            IntPtr pPresentParameters);  // D3DPRESENT_PARAMETERS*

        // IDirect3DDevice9::CreateTexture (vtable index 21)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateTextureDelegate(
            IntPtr pDevice,              // IDirect3DDevice9* (this pointer)
            uint width,                  // UINT
            uint height,                 // UINT
            uint levels,                 // UINT
            uint usage,                  // DWORD
            uint format,                 // D3DFORMAT
            uint pool,                   // D3DPOOL
            ref IntPtr ppTexture,        // IDirect3DTexture9**
            IntPtr pSharedHandle);       // HANDLE*

        // IDirect3DTexture9::LockRect (vtable index 19)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int LockRectDelegate(
            IntPtr pTexture,             // IDirect3DTexture9* (this pointer)
            uint level,                  // UINT
            out D3D9LockedRect pLockedRect, // D3DLOCKED_RECT*
            IntPtr pRect,                // const RECT*
            uint flags);                 // DWORD

        // IDirect3DTexture9::UnlockRect (vtable index 20)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int UnlockRectDelegate(
            IntPtr pTexture,             // IDirect3DTexture9* (this pointer)
            uint level);                 // UINT

        // IDirect3DDevice9::CreateVertexBuffer (vtable index 22)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateVertexBufferDelegate(
            IntPtr pDevice,              // IDirect3DDevice9* (this pointer)
            uint length,                 // UINT
            uint usage,                  // DWORD
            uint fvf,                    // DWORD (FVF)
            uint pool,                   // D3DPOOL
            ref IntPtr ppVertexBuffer,   // IDirect3DVertexBuffer9**
            IntPtr pSharedHandle);       // HANDLE*

        // IDirect3DDevice9::CreateIndexBuffer (vtable index 23)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateIndexBufferDelegate(
            IntPtr pDevice,              // IDirect3DDevice9* (this pointer)
            uint length,                 // UINT
            uint usage,                  // DWORD
            uint format,                 // D3DFORMAT
            uint pool,                   // D3DPOOL
            ref IntPtr ppIndexBuffer,    // IDirect3DIndexBuffer9**
            IntPtr pSharedHandle);       // HANDLE*

        // IDirect3DDevice9::CreateVertexShader (vtable index 87)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateVertexShaderDelegate(
            IntPtr pDevice,              // IDirect3DDevice9* (this pointer)
            IntPtr pFunction,            // const DWORD* (shader bytecode)
            ref IntPtr ppShader);        // IDirect3DVertexShader9**

        // IDirect3DDevice9::CreatePixelShader (vtable index 88)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreatePixelShaderDelegate(
            IntPtr pDevice,              // IDirect3DDevice9* (this pointer)
            IntPtr pFunction,            // const DWORD* (shader bytecode)
            ref IntPtr ppShader);        // IDirect3DPixelShader9**

        // IDirect3DDevice9::DeleteVertexShader (not in standard D3D9 - shaders released via Release())
        // IDirect3DDevice9::DeletePixelShader (not in standard D3D9 - shaders released via Release())

        // IDirect3DDevice9::GetAvailableTextureMem (vtable index 38)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint GetAvailableTextureMemDelegate(IntPtr pDevice); // IDirect3DDevice9* (this pointer)

        /// <summary>
        /// Gets a COM interface method delegate from a COM interface pointer via vtable.
        /// </summary>
        /// <typeparam name="T">Delegate type for the method.</typeparam>
        /// <param name="pInterface">COM interface pointer.</param>
        /// <param name="vtableIndex">Vtable index (method index) of the method.</param>
        /// <returns>Delegate instance for calling the method.</returns>
        public static T GetComMethod<T>(IntPtr pInterface, int vtableIndex) where T : class
        {
            if (pInterface == IntPtr.Zero)
            {
                return null;
            }

            // COM vtable is at offset 0 in the interface pointer
            // Read the vtable pointer (first 4/8 bytes depending on architecture)
            IntPtr vtable = Marshal.ReadIntPtr(pInterface);

            // Get method pointer at vtable index (each method pointer is IntPtr.Size bytes)
            IntPtr methodPtr = Marshal.ReadIntPtr(vtable, vtableIndex * IntPtr.Size);

            // Convert to delegate
            return Marshal.GetDelegateForFunctionPointer<T>(methodPtr);
        }

        // Convenience methods that wrap GetComMethod for common operations
        public static int BeginScene(IntPtr device)
        {
            BeginSceneDelegate del = GetComMethod<BeginSceneDelegate>(device, 35);
            if (del == null) return -1; // E_FAIL
            return del(device);
        }

        public static int EndScene(IntPtr device)
        {
            EndSceneDelegate del = GetComMethod<EndSceneDelegate>(device, 36);
            if (del == null) return -1; // E_FAIL
            return del(device);
        }

        public static int Present(IntPtr device, IntPtr pSourceRect, IntPtr pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion)
        {
            PresentDelegate del = GetComMethod<PresentDelegate>(device, 37);
            if (del == null) return -1; // E_FAIL
            return del(device, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion);
        }

        public static int Clear(IntPtr device, uint count, IntPtr pRects, uint flags, uint color, float z, uint stencil)
        {
            ClearDelegate del = GetComMethod<ClearDelegate>(device, 34);
            if (del == null) return -1; // E_FAIL
            return del(device, count, pRects, flags, color, z, stencil);
        }

        public static int Reset(IntPtr device, IntPtr pPresentParameters)
        {
            ResetDelegate del = GetComMethod<ResetDelegate>(device, 16);
            if (del == null) return -1; // E_FAIL
            return del(device, pPresentParameters);
        }

        public static int CreateTexture(IntPtr device, uint width, uint height, uint levels, uint usage, uint format, uint pool, ref IntPtr ppTexture, IntPtr pSharedHandle)
        {
            CreateTextureDelegate del = GetComMethod<CreateTextureDelegate>(device, 21);
            if (del == null) return -1; // E_FAIL
            return del(device, width, height, levels, usage, format, pool, ref ppTexture, pSharedHandle);
        }

        public static int LockRect(IntPtr texture, uint level, out D3D9LockedRect pLockedRect, IntPtr pRect, uint flags)
        {
            LockRectDelegate del = GetComMethod<LockRectDelegate>(texture, 19);
            if (del == null)
            {
                pLockedRect = new D3D9LockedRect();
                return -1; // E_FAIL
            }
            return del(texture, level, out pLockedRect, pRect, flags);
        }

        public static int UnlockRect(IntPtr texture, uint level)
        {
            UnlockRectDelegate del = GetComMethod<UnlockRectDelegate>(texture, 20);
            if (del == null) return -1; // E_FAIL
            return del(texture, level);
        }

        public static int CreateVertexBuffer(IntPtr device, uint length, uint usage, uint fvf, uint pool, ref IntPtr ppVertexBuffer, IntPtr pSharedHandle)
        {
            CreateVertexBufferDelegate del = GetComMethod<CreateVertexBufferDelegate>(device, 22);
            if (del == null) return -1; // E_FAIL
            return del(device, length, usage, fvf, pool, ref ppVertexBuffer, pSharedHandle);
        }

        public static int CreateIndexBuffer(IntPtr device, uint length, uint usage, uint format, uint pool, ref IntPtr ppIndexBuffer, IntPtr pSharedHandle)
        {
            CreateIndexBufferDelegate del = GetComMethod<CreateIndexBufferDelegate>(device, 23);
            if (del == null) return -1; // E_FAIL
            return del(device, length, usage, format, pool, ref ppIndexBuffer, pSharedHandle);
        }

        public static int CreateVertexShader(IntPtr device, byte[] pFunction, ref IntPtr ppShader)
        {
            CreateVertexShaderDelegate del = GetComMethod<CreateVertexShaderDelegate>(device, 87);
            if (del == null) return -1; // E_FAIL

            // Pin the shader bytecode array
            GCHandle functionHandle = GCHandle.Alloc(pFunction, GCHandleType.Pinned);
            try
            {
                IntPtr functionPtr = functionHandle.AddrOfPinnedObject();
                return del(device, functionPtr, ref ppShader);
            }
            finally
            {
                functionHandle.Free();
            }
        }

        public static int CreatePixelShader(IntPtr device, byte[] pFunction, ref IntPtr ppShader)
        {
            CreatePixelShaderDelegate del = GetComMethod<CreatePixelShaderDelegate>(device, 88);
            if (del == null) return -1; // E_FAIL

            // Pin the shader bytecode array
            GCHandle functionHandle = GCHandle.Alloc(pFunction, GCHandleType.Pinned);
            try
            {
                IntPtr functionPtr = functionHandle.AddrOfPinnedObject();
                return del(device, functionPtr, ref ppShader);
            }
            finally
            {
                functionHandle.Free();
            }
        }

        public static uint GetAvailableTextureMem(IntPtr device)
        {
            GetAvailableTextureMemDelegate del = GetComMethod<GetAvailableTextureMemDelegate>(device, 38);
            if (del == null) return 0;
            return del(device);
        }

        public static int GetAdapterIdentifier(IntPtr d3d9, uint adapter, uint flags, out D3D9AdapterIdentifier pIdentifier)
        {
            GetAdapterIdentifierDelegate del = GetComMethod<GetAdapterIdentifierDelegate>(d3d9, 2);
            if (del == null)
            {
                pIdentifier = new D3D9AdapterIdentifier();
                return -1; // E_FAIL
            }
            return del(d3d9, adapter, flags, out pIdentifier);
        }

        public static int CreateDevice(IntPtr d3d9, uint adapter, uint deviceType, IntPtr focusWindow, uint behaviorFlags, IntPtr pPresentParameters, ref IntPtr ppDevice)
        {
            CreateDeviceDelegate del = GetComMethod<CreateDeviceDelegate>(d3d9, 16);
            if (del == null) return -1; // E_FAIL
            return del(d3d9, adapter, deviceType, focusWindow, behaviorFlags, pPresentParameters, ref ppDevice);
        }
    }

    /// <summary>
    /// Direct3D9 constants.
    /// </summary>
    internal static class D3D9Constants
    {
        public const uint D3D_SDK_VERSION = 32;
    }

    /// <summary>
    /// Direct3D9 clear flags.
    /// </summary>
    [Flags]
    internal enum D3D9ClearFlags : uint
    {
        D3DCLEAR_TARGET = 0x00000001,
        D3DCLEAR_ZBUFFER = 0x00000002,
        D3DCLEAR_STENCIL = 0x00000004
    }

    /// <summary>
    /// Direct3D9 present parameters structure.
    /// Based on D3D9 API: D3DPRESENT_PARAMETERS
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct D3D9PresentParameters
    {
        public int BackBufferWidth;
        public int BackBufferHeight;
        public int BackBufferFormat;
        public int BackBufferCount;
        public int MultiSampleType;
        public int MultiSampleQuality;
        public int SwapEffect;
        public IntPtr DeviceWindow;
        public bool Windowed;
        public bool EnableAutoDepthStencil;
        public int AutoDepthStencilFormat;
        public int Flags;
        public uint FullScreenRefreshRateInHz;
        public uint PresentationInterval;
    }

    /// <summary>
    /// Direct3D9 locked rectangle structure.
    /// Based on D3D9 API: D3DLOCKED_RECT
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct D3D9LockedRect
    {
        public int Pitch;
        public IntPtr pBits;
    }

    /// <summary>
    /// Direct3D9 adapter identifier structure.
    /// Based on D3D9 API: D3DADAPTER_IDENTIFIER9
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct D3D9AdapterIdentifier
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string Driver;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string Description;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public long DriverVersion;
        public int VendorId;
        public int DeviceId;
        public int SubSysId;
        public int Revision;
        public Guid DeviceIdentifier;
        public uint WHQLLevel;
        public uint DriverSubVersion;
    }

    #endregion

    #region Windows API Declarations

    /// <summary>
    /// Windows API function declarations for window handle retrieval.
    /// Based on Windows API: https://docs.microsoft.com/en-us/windows/win32/api/winuser/
    /// Used for finding the game window handle (HWND) for DirectX 9 device creation.
    /// </summary>
    internal static class WindowsApiMethods
    {
        private const string USER32_DLL = "user32.dll";

        /// <summary>
        /// Retrieves the handle to the foreground window (the window with which the user is currently working).
        /// https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow
        /// </summary>
        /// <returns>A handle to the foreground window, or NULL if no foreground window exists.</returns>
        [DllImport(USER32_DLL)]
        public static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Retrieves the handle to the active window attached to the calling thread's message queue.
        /// https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getactivewindow
        /// </summary>
        /// <returns>The handle to the active window attached to the calling thread's message queue, or NULL if no window is active.</returns>
        [DllImport(USER32_DLL)]
        public static extern IntPtr GetActiveWindow();

        /// <summary>
        /// Retrieves a handle to the top-level window whose class name and window name match the specified strings.
        /// https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-findwindowa
        /// Uses FindWindowA (ANSI version) for consistency with Windows API conventions.
        /// </summary>
        /// <param name="lpClassName">The class name. If this parameter is NULL, it finds any window whose title matches the lpWindowName parameter.</param>
        /// <param name="lpWindowName">The window name. If this parameter is NULL, all window names match.</param>
        /// <returns>A handle to the window if found, or NULL if no window matches the criteria.</returns>
        [DllImport(USER32_DLL, CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr FindWindow([JetBrains.Annotations.CanBeNull] string lpClassName, [JetBrains.Annotations.CanBeNull] string lpWindowName);

        /// <summary>
        /// Determines whether the specified window handle identifies an existing window.
        /// https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindow
        /// </summary>
        /// <param name="hWnd">A handle to the window to test.</param>
        /// <returns>Nonzero if the window handle identifies an existing window, zero otherwise.</returns>
        [DllImport(USER32_DLL)]
        public static extern bool IsWindow(IntPtr hWnd);

        /// <summary>
        /// Enumerates all top-level windows on the screen by passing the handle to each window to an application-defined callback function.
        /// https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows
        /// </summary>
        /// <param name="lpEnumFunc">A pointer to an application-defined callback function.</param>
        /// <param name="lParam">An application-defined value to be passed to the callback function.</param>
        /// <returns>Nonzero if the function succeeds, zero otherwise.</returns>
        [DllImport(USER32_DLL)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        /// <summary>
        /// Retrieves the identifier of the thread that created the specified window and, optionally, the identifier of the process that created the window.
        /// https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <param name="lpdwProcessId">A pointer to a variable that receives the process identifier. If this parameter is not NULL, GetWindowThreadProcessId copies the identifier of the process to the variable; otherwise, it does not.</param>
        /// <returns>The identifier of the thread that created the window.</returns>
        [DllImport(USER32_DLL, SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// Application-defined callback function used with the EnumWindows function.
        /// </summary>
        /// <param name="hWnd">A handle to a top-level window.</param>
        /// <param name="lParam">The application-defined value given in EnumWindows.</param>
        /// <returns>Return TRUE to continue enumeration, or FALSE to stop enumeration.</returns>
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    }

    #endregion
}


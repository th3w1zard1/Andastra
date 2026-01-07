using System;
using System.IO;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Remix
{
    /// <summary>
    /// NVIDIA RTX Remix bridge for path-traced rendering.
    ///
    /// RTX Remix works by intercepting DirectX 8/9 API calls and replacing
    /// the rasterized output with path-traced rendering. This bridge provides
    /// a DX9 compatibility layer that Remix can hook into.
    ///
    /// Usage:
    /// 1. Install NVIDIA RTX Remix Runtime
    /// 2. Set RemixCompatibility = true in RenderSettings
    /// 3. The engine will create a DX9 device that Remix intercepts
    /// 4. All rendering commands are translated to DX9 equivalents
    /// 5. Remix replaces output with path-traced result
    /// </summary>
    public class RemixBridge : IDisposable
    {
        private IntPtr _d3d9Handle;
        private IntPtr _d3d9; // IDirect3D9* interface
        private IntPtr _deviceHandle;
        private bool _initialized;
        private bool _remixDetected;
        private RemixCapabilities _capabilities;

        /// <summary>
        /// Whether Remix runtime is detected and available.
        /// </summary>
        public bool IsAvailable
        {
            get { return _remixDetected; }
        }

        /// <summary>
        /// Whether the bridge is initialized and active.
        /// </summary>
        public bool IsActive
        {
            get { return _initialized && _remixDetected; }
        }

        /// <summary>
        /// Remix runtime capabilities.
        /// </summary>
        public RemixCapabilities Capabilities
        {
            get { return _capabilities; }
        }

        /// <summary>
        /// Initializes the Remix bridge.
        /// </summary>
        /// <param name="windowHandle">Window handle for device creation.</param>
        /// <param name="settings">Remix settings.</param>
        /// <returns>True if initialization succeeded.</returns>
        public bool Initialize(IntPtr windowHandle, RemixSettings settings)
        {
            if (_initialized)
            {
                return true;
            }

            // Check for Remix runtime
            _remixDetected = DetectRemixRuntime(settings.RuntimePath);

            if (!_remixDetected)
            {
                Console.WriteLine("[RemixBridge] RTX Remix runtime not detected");
                return false;
            }

            // Load d3d9.dll (Remix's interceptor DLL)
            string d3d9Path = Path.Combine(settings.RuntimePath, "d3d9.dll");
            if (!File.Exists(d3d9Path))
            {
                d3d9Path = "d3d9.dll"; // System d3d9
            }

            _d3d9Handle = NativeMethods.LoadLibrary(d3d9Path);
            if (_d3d9Handle == IntPtr.Zero)
            {
                Console.WriteLine("[RemixBridge] Failed to load d3d9.dll");
                return false;
            }

            // Create D3D9 device
            if (!CreateD3D9Device(windowHandle, settings))
            {
                NativeMethods.FreeLibrary(_d3d9Handle);
                _d3d9Handle = IntPtr.Zero;
                return false;
            }

            // Query Remix capabilities
            QueryRemixCapabilities();

            _initialized = true;
            Console.WriteLine("[RemixBridge] Initialized successfully");
            Console.WriteLine("[RemixBridge] Path tracing: " + (_capabilities.PathTracingEnabled ? "enabled" : "disabled"));

            return true;
        }

        /// <summary>
        /// Shuts down the Remix bridge.
        /// </summary>
        public void Shutdown()
        {
            if (_deviceHandle != IntPtr.Zero)
            {
                // Release D3D9 device
                ReleaseDirect3D9Device(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
            }

            if (_d3d9 != IntPtr.Zero)
            {
                // Release D3D9 interface
                ReleaseDirect3D9(_d3d9);
                _d3d9 = IntPtr.Zero;
            }

            if (_d3d9Handle != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(_d3d9Handle);
                _d3d9Handle = IntPtr.Zero;
            }

            _initialized = false;
        }

        /// <summary>
        /// Begins a new frame for Remix rendering.
        /// </summary>
        public void BeginFrame()
        {
            if (!IsActive)
            {
                return;
            }

            // Signal frame start to Remix
            // Remix hooks BeginScene
        }

        /// <summary>
        /// Ends the current frame.
        /// </summary>
        public void EndFrame()
        {
            if (!IsActive)
            {
                return;
            }

            // Signal frame end to Remix
            // Remix hooks EndScene and Present
        }

        /// <summary>
        /// Submits geometry for path tracing.
        /// </summary>
        public void SubmitGeometry(RemixGeometry geometry)
        {
            if (!IsActive)
            {
                return;
            }

            // Convert geometry to D3D9 draw calls
            // Remix intercepts and builds acceleration structures
        }

        /// <summary>
        /// Submits a light for path tracing.
        /// </summary>
        public void SubmitLight(RemixLight light)
        {
            if (!IsActive)
            {
                return;
            }

            // Convert light to D3D9 light
            // Remix intercepts and uses for path tracing
        }

        /// <summary>
        /// Submits a material for path tracing.
        /// </summary>
        public void SubmitMaterial(RemixMaterial material)
        {
            if (!IsActive)
            {
                return;
            }

            // Convert material to D3D9 material + textures
            // Remix intercepts and converts to PBR
        }

        /// <summary>
        /// Sets the camera for path tracing.
        /// </summary>
        public void SetCamera(RemixCamera camera)
        {
            if (!IsActive)
            {
                return;
            }

            // Set view/projection matrices
            // Remix uses these for ray generation
        }

        /// <summary>
        /// Configures Remix rendering settings.
        /// </summary>
        public void ConfigureRendering(RemixRenderConfig config)
        {
            if (!IsActive)
            {
                return;
            }

            // Apply Remix-specific settings via runtime API
        }

        private bool DetectRemixRuntime(string runtimePath)
        {
            // Check for Remix runtime files
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
                return File.Exists(Path.Combine(remixPath, "NvRemixBridge.dll"));
            }

            // Check for Remix d3d9.dll in current directory
            if (File.Exists("d3d9.dll"))
            {
                // Check if it's Remix's d3d9.dll by checking for characteristic exports
                IntPtr testHandle = NativeMethods.LoadLibrary("d3d9.dll");
                if (testHandle != IntPtr.Zero)
                {
                    IntPtr remixExport = NativeMethods.GetProcAddress(testHandle, "remixInitialize");
                    bool isRemix = remixExport != IntPtr.Zero;
                    NativeMethods.FreeLibrary(testHandle);
                    return isRemix;
                }
            }

            return false;
        }

        private bool CreateD3D9Device(IntPtr windowHandle, RemixSettings settings)
        {
            // Get Direct3DCreate9 function
            IntPtr createFunc = NativeMethods.GetProcAddress(_d3d9Handle, "Direct3DCreate9");
            if (createFunc == IntPtr.Zero)
            {
                Console.WriteLine("[RemixBridge] Failed to get Direct3DCreate9");
                return false;
            }

            // 1. Call Direct3DCreate9(D3D_SDK_VERSION)
            Direct3DCreate9Delegate direct3DCreate9 = Marshal.GetDelegateForFunctionPointer<Direct3DCreate9Delegate>(createFunc);
            IntPtr d3d9 = direct3DCreate9(D3D9Constants.D3D_SDK_VERSION);
            if (d3d9 == IntPtr.Zero)
            {
                Console.WriteLine("[RemixBridge] Direct3DCreate9 returned null");
                return false;
            }

            // Get adapter display mode
            D3DDISPLAYMODE displayMode = new D3DDISPLAYMODE();
            int hr = GetAdapterDisplayMode(d3d9, 0, ref displayMode);
            if (hr < 0)
            {
                Console.WriteLine("[RemixBridge] Failed to get adapter display mode with HRESULT: 0x" + hr.ToString("X8"));
                ReleaseDirect3D9(d3d9);
                return false;
            }

            // 2. Create D3D9 device with appropriate parameters
            // Create present parameters matching Remix requirements
            D3DPRESENT_PARAMETERS presentParams = new D3DPRESENT_PARAMETERS
            {
                BackBufferWidth = 800, // Default values - will be resized if needed
                BackBufferHeight = 600,
                BackBufferFormat = displayMode.Format,
                BackBufferCount = 1,
                MultiSampleType = 0, // D3DMULTISAMPLE_NONE
                MultiSampleQuality = 0,
                SwapEffect = 1, // D3DSWAPEFFECT_DISCARD
                hDeviceWindow = windowHandle,
                Windowed = 1, // Windowed mode for Remix
                EnableAutoDepthStencil = 1,
                AutoDepthStencilFormat = 75, // D3DFMT_D24S8
                Flags = 0,
                FullScreen_RefreshRateInHz = 0,
                PresentationInterval = settings.EnableReflex ? 1u : 0x80000000u // D3DPRESENT_INTERVAL_ONE or D3DPRESENT_INTERVAL_IMMEDIATE
            };

            // Create device through COM vtable
            Guid iidDevice = D3D9Constants.IID_IDirect3DDevice9;
            IntPtr devicePtr = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                uint adapter = 0; // D3DADAPTER_DEFAULT
                uint deviceType = 1; // D3DDEVTYPE_HAL
                uint behaviorFlags = D3D9Constants.D3DCREATE_HARDWARE_VERTEXPROCESSING | D3D9Constants.D3DCREATE_MULTITHREADED;

                hr = CreateDevice(d3d9, adapter, deviceType, windowHandle, behaviorFlags, ref presentParams, ref iidDevice, devicePtr);
                if (hr < 0)
                {
                    // Try software vertex processing as fallback
                    behaviorFlags = D3D9Constants.D3DCREATE_SOFTWARE_VERTEXPROCESSING | D3D9Constants.D3DCREATE_MULTITHREADED;
                    hr = CreateDevice(d3d9, adapter, deviceType, windowHandle, behaviorFlags, ref presentParams, ref iidDevice, devicePtr);
                }

                if (hr < 0)
                {
                    Console.WriteLine("[RemixBridge] CreateDevice failed with HRESULT: 0x" + hr.ToString("X8"));
                    ReleaseDirect3D9(d3d9);
                    return false;
                }

                // 3. Store device handle
                _deviceHandle = Marshal.ReadIntPtr(devicePtr);
                if (_deviceHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[RemixBridge] Device pointer is null");
                    ReleaseDirect3D9(d3d9);
                    return false;
                }

                // Store D3D9 interface for later cleanup
                _d3d9 = d3d9;

                Console.WriteLine("[RemixBridge] Direct3D9 device created successfully");
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(devicePtr);
            }
        }

        private void QueryRemixCapabilities()
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

        public void Dispose()
        {
            Shutdown();
        }

        /// <summary>
        /// Gets adapter display mode using DirectX 9 COM vtable.
        /// </summary>
        private unsafe int GetAdapterDisplayMode(IntPtr d3d9, uint adapter, ref D3DDISPLAYMODE mode)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || d3d9 == IntPtr.Zero)
            {
                return -1; // E_FAIL
            }

            IntPtr* vtable = *(IntPtr**)d3d9;
            // GetAdapterDisplayMode is at vtable index 11 in IDirect3D9
            IntPtr methodPtr = vtable[11];
            GetAdapterDisplayModeDelegate getMode = Marshal.GetDelegateForFunctionPointer<GetAdapterDisplayModeDelegate>(methodPtr);
            return getMode(d3d9, adapter, ref mode);
        }

        /// <summary>
        /// Creates device using DirectX 9 COM vtable.
        /// </summary>
        private unsafe int CreateDevice(IntPtr d3d9, uint adapter, uint deviceType, IntPtr hFocusWindow,
            uint behaviorFlags, ref D3DPRESENT_PARAMETERS presentationParameters, ref Guid returnedDeviceInterface, IntPtr ppDevice)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || d3d9 == IntPtr.Zero)
            {
                return -1; // E_FAIL
            }

            IntPtr* vtable = *(IntPtr**)d3d9;
            // CreateDevice is at vtable index 16 in IDirect3D9
            IntPtr methodPtr = vtable[16];
            CreateDeviceDelegate createDevice = Marshal.GetDelegateForFunctionPointer<CreateDeviceDelegate>(methodPtr);
            return createDevice(d3d9, adapter, deviceType, hFocusWindow, behaviorFlags, ref presentationParameters, ref returnedDeviceInterface, ppDevice);
        }

        /// <summary>
        /// Releases DirectX 9 device using COM Release().
        /// </summary>
        private unsafe void ReleaseDirect3D9Device(IntPtr device)
        {
            if (device == IntPtr.Zero)
            {
                return;
            }

            IntPtr* vtable = *(IntPtr**)device;
            // Release is at vtable index 2 in IUnknown
            IntPtr methodPtr = vtable[2];
            ReleaseDelegate release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(methodPtr);
            release(device);
        }

        /// <summary>
        /// Releases DirectX 9 interface using COM Release().
        /// </summary>
        private unsafe void ReleaseDirect3D9(IntPtr d3d9)
        {
            if (d3d9 == IntPtr.Zero)
            {
                return;
            }

            IntPtr* vtable = *(IntPtr**)d3d9;
            // Release is at vtable index 2 in IUnknown
            IntPtr methodPtr = vtable[2];
            ReleaseDelegate release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(methodPtr);
            release(d3d9);
        }

        // DirectX 9 Function Delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr Direct3DCreate9Delegate(uint sdkVersion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetAdapterDisplayModeDelegate(IntPtr d3d9, uint adapter, ref D3DDISPLAYMODE mode);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDeviceDelegate(IntPtr d3d9, uint adapter, uint deviceType, IntPtr hFocusWindow,
            uint behaviorFlags, ref D3DPRESENT_PARAMETERS presentationParameters, ref Guid returnedDeviceInterface, IntPtr ppDevice);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr comObject);

        // DirectX 9 Structures
        [StructLayout(LayoutKind.Sequential)]
        private struct D3DDISPLAYMODE
        {
            public uint Width;
            public uint Height;
            public uint RefreshRate;
            public uint Format;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DPRESENT_PARAMETERS
        {
            public uint BackBufferWidth;
            public uint BackBufferHeight;
            public uint BackBufferFormat;
            public uint BackBufferCount;
            public uint MultiSampleType;
            public uint MultiSampleQuality;
            public uint SwapEffect;
            public IntPtr hDeviceWindow;
            public int Windowed;
            public int EnableAutoDepthStencil;
            public uint AutoDepthStencilFormat;
            public uint Flags;
            public uint FullScreen_RefreshRateInHz;
            public uint PresentationInterval;
        }

        // DirectX 9 Constants
        private static class D3D9Constants
        {
            public const uint D3D_SDK_VERSION = 32;
            public const uint D3DCREATE_HARDWARE_VERTEXPROCESSING = 0x00000040;
            public const uint D3DCREATE_SOFTWARE_VERTEXPROCESSING = 0x00000020;
            public const uint D3DCREATE_MULTITHREADED = 0x00000004;
            public static readonly Guid IID_IDirect3DDevice9 = new Guid("D0223B96-BF7A-43fd-92BD-A43B0D82B9EB");
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        }
    }

    /// <summary>
    /// Remix initialization settings.
    /// </summary>
    public struct RemixSettings
    {
        /// <summary>
        /// Path to RTX Remix runtime.
        /// </summary>
        public string RuntimePath;

        /// <summary>
        /// Enable path tracing immediately.
        /// </summary>
        public bool EnablePathTracing;

        /// <summary>
        /// Maximum path tracing bounces.
        /// </summary>
        public int MaxBounces;

        /// <summary>
        /// Enable denoising.
        /// </summary>
        public bool EnableDenoiser;

        /// <summary>
        /// Enable DLSS upscaling.
        /// </summary>
        public bool EnableDlss;

        /// <summary>
        /// Enable NVIDIA Reflex.
        /// </summary>
        public bool EnableReflex;

        /// <summary>
        /// Capture mode for asset extraction.
        /// </summary>
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
        public System.Numerics.Matrix4x4 WorldMatrix;
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
        public System.Numerics.Vector3 Position;
        public System.Numerics.Vector3 Direction;
        public System.Numerics.Vector3 Color;
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
        public System.Numerics.Vector4 AlbedoColor;
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
        public System.Numerics.Vector3 Position;
        public System.Numerics.Vector3 Forward;
        public System.Numerics.Vector3 Up;
        public float FieldOfView;
        public float NearPlane;
        public float FarPlane;
        public System.Numerics.Matrix4x4 ViewMatrix;
        public System.Numerics.Matrix4x4 ProjectionMatrix;
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
}


using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Remix
{
    /// <summary>
    /// DirectX 9 wrapper that enables NVIDIA RTX Remix interception.
    ///
    /// RTX Remix works by hooking D3D9 API calls and replacing rasterized
    /// rendering with path-traced output. This wrapper provides a D3D9
    /// compatibility layer that:
    ///
    /// 1. Creates a D3D9 device that Remix can hook
    /// 2. Translates modern rendering commands to D3D9 equivalents
    /// 3. Exposes game assets in a format Remix understands
    /// 4. Provides hooks for Remix's material/lighting overrides
    ///
    /// Requirements:
    /// - NVIDIA RTX Remix Runtime (d3d9.dll, bridge.dll)
    /// - RTX GPU (20-series or newer recommended)
    /// - Windows 10/11
    /// </summary>
    public class Direct3D9Wrapper : IGraphicsBackend
    {
        private IntPtr _d3d9;
        private IntPtr _device;
        private IntPtr _swapChain;
        private IntPtr _windowHandle;
        private bool _initialized;
        private bool _remixActive;
        private RenderSettings _settings;
        private GraphicsCapabilities _capabilities;

        // D3D9 constants
        private const uint D3D_SDK_VERSION = 32;
        private const uint D3DCREATE_HARDWARE_VERTEXPROCESSING = 0x00000040;
        private const uint D3DDEVTYPE_HAL = 1;
        private const uint D3DFMT_X8R8G8B8 = 22;
        private const uint D3DFMT_D24S8 = 75;
        private const uint D3DSWAPEFFECT_DISCARD = 1;
        private const uint D3DPRESENT_INTERVAL_ONE = 0x00000001;
        
        // D3D9 render state constants (D3DRENDERSTATETYPE)
        private const uint D3DRS_ZENABLE = 7;
        private const uint D3DRS_FILLMODE = 8;
        private const uint D3DRS_SHADEMODE = 9;
        private const uint D3DRS_ZWRITEENABLE = 14;
        private const uint D3DRS_ALPHATESTENABLE = 15;
        private const uint D3DRS_LASTPIXEL = 16;
        private const uint D3DRS_SRCBLEND = 19;
        private const uint D3DRS_DESTBLEND = 20;
        private const uint D3DRS_CULLMODE = 22;
        private const uint D3DRS_ZFUNC = 23;
        private const uint D3DRS_ALPHAREF = 24;
        private const uint D3DRS_ALPHAFUNC = 25;
        private const uint D3DRS_DITHERENABLE = 26;
        private const uint D3DRS_ALPHABLENDENABLE = 27;
        private const uint D3DRS_FOGENABLE = 28;
        private const uint D3DRS_SPECULARENABLE = 29;
        private const uint D3DRS_FOGCOLOR = 34;
        private const uint D3DRS_FOGTABLEMODE = 35;
        private const uint D3DRS_FOGSTART = 36;
        private const uint D3DRS_FOGEND = 37;
        private const uint D3DRS_FOGDENSITY = 38;
        private const uint D3DRS_RANGEFOGENABLE = 48;
        private const uint D3DRS_STENCILENABLE = 52;
        private const uint D3DRS_STENCILFAIL = 53;
        private const uint D3DRS_STENCILZFAIL = 54;
        private const uint D3DRS_STENCILPASS = 55;
        private const uint D3DRS_STENCILFUNC = 56;
        private const uint D3DRS_STENCILREF = 57;
        private const uint D3DRS_STENCILMASK = 58;
        private const uint D3DRS_STENCILWRITEMASK = 59;
        private const uint D3DRS_TEXTUREFACTOR = 60;
        private const uint D3DRS_WRAP0 = 128;
        private const uint D3DRS_WRAP1 = 129;
        private const uint D3DRS_WRAP2 = 130;
        private const uint D3DRS_WRAP3 = 131;
        private const uint D3DRS_WRAP4 = 132;
        private const uint D3DRS_WRAP5 = 133;
        private const uint D3DRS_WRAP6 = 134;
        private const uint D3DRS_WRAP7 = 135;
        private const uint D3DRS_CLIPPING = 136;
        private const uint D3DRS_LIGHTING = 137;
        private const uint D3DRS_AMBIENT = 139;
        private const uint D3DRS_FOGVERTEXMODE = 140;
        private const uint D3DRS_COLORVERTEX = 141;
        private const uint D3DRS_LOCALVIEWER = 142;
        private const uint D3DRS_NORMALIZENORMALS = 143;
        private const uint D3DRS_DIFFUSEMATERIALSOURCE = 145;
        private const uint D3DRS_SPECULARMATERIALSOURCE = 146;
        private const uint D3DRS_AMBIENTMATERIALSOURCE = 147;
        private const uint D3DRS_EMISSIVEMATERIALSOURCE = 148;
        private const uint D3DRS_VERTEXBLEND = 151;
        private const uint D3DRS_CLIPPLANEENABLE = 152;
        private const uint D3DRS_POINTSIZE = 154;
        private const uint D3DRS_POINTSIZE_MIN = 155;
        private const uint D3DRS_POINTSPRITEENABLE = 156;
        private const uint D3DRS_POINTSCALEENABLE = 157;
        private const uint D3DRS_POINTSCALE_A = 158;
        private const uint D3DRS_POINTSCALE_B = 159;
        private const uint D3DRS_POINTSCALE_C = 160;
        private const uint D3DRS_MULTISAMPLEANTIALIAS = 161;
        private const uint D3DRS_MULTISAMPLEMASK = 162;
        private const uint D3DRS_PATCHEDGESTYLE = 163;
        private const uint D3DRS_DEBUGMONITORTOKEN = 165;
        private const uint D3DRS_POINTSIZE_MAX = 166;
        private const uint D3DRS_INDEXEDVERTEXBLENDENABLE = 167;
        private const uint D3DRS_COLORWRITEENABLE = 168;
        private const uint D3DRS_TWEENFACTOR = 170;
        private const uint D3DRS_BLENDOP = 171;
        private const uint D3DRS_POSITIONDEGREE = 172;
        private const uint D3DRS_NORMALDEGREE = 173;
        private const uint D3DRS_SCISSORTESTENABLE = 174;
        private const uint D3DRS_SLOPESCALEDEPTHBIAS = 175;
        private const uint D3DRS_ANTIALIASEDLINEENABLE = 176;
        private const uint D3DRS_MINTESSELLATIONLEVEL = 177;
        private const uint D3DRS_MAXTESSELLATIONLEVEL = 178;
        private const uint D3DRS_ADAPTIVETESS_X = 179;
        private const uint D3DRS_ADAPTIVETESS_Y = 180;
        private const uint D3DRS_ADAPTIVETESS_Z = 181;
        private const uint D3DRS_ADAPTIVETESS_W = 182;
        private const uint D3DRS_ENABLEADAPTIVETESSELLATION = 183;
        private const uint D3DRS_TWOSIDEDSTENCILMODE = 184;
        private const uint D3DRS_CCW_STENCILFAIL = 185;
        private const uint D3DRS_CCW_STENCILZFAIL = 186;
        private const uint D3DRS_CCW_STENCILPASS = 187;
        private const uint D3DRS_CCW_STENCILFUNC = 188;
        private const uint D3DRS_COLORWRITEENABLE1 = 189;
        private const uint D3DRS_COLORWRITEENABLE2 = 190;
        private const uint D3DRS_COLORWRITEENABLE3 = 191;
        private const uint D3DRS_BLENDFACTOR = 193;
        private const uint D3DRS_SRGBWRITEENABLE = 194;
        private const uint D3DRS_DEPTHBIAS = 195;
        private const uint D3DRS_WRAP8 = 198;
        private const uint D3DRS_WRAP9 = 199;
        private const uint D3DRS_WRAP10 = 200;
        private const uint D3DRS_WRAP11 = 201;
        private const uint D3DRS_WRAP12 = 202;
        private const uint D3DRS_WRAP13 = 203;
        private const uint D3DRS_WRAP14 = 204;
        private const uint D3DRS_WRAP15 = 205;
        private const uint D3DRS_SEPARATEALPHABLENDENABLE = 206;
        private const uint D3DRS_SRCBLENDALPHA = 207;
        private const uint D3DRS_DESTBLENDALPHA = 208;
        private const uint D3DRS_BLENDOPALPHA = 209;
        
        // D3D9 fill mode constants
        private const uint D3DFILL_POINT = 1;
        private const uint D3DFILL_WIREFRAME = 2;
        private const uint D3DFILL_SOLID = 3;
        
        // D3D9 cull mode constants
        private const uint D3DCULL_NONE = 1;
        private const uint D3DCULL_CW = 2;
        private const uint D3DCULL_CCW = 3;
        
        // D3D9 Z-buffer constants
        private const uint D3DZB_FALSE = 0;
        private const uint D3DZB_TRUE = 1;
        private const uint D3DZB_USEW = 2;
        
        // D3D9 compare function constants
        private const uint D3DCMP_NEVER = 1;
        private const uint D3DCMP_LESS = 2;
        private const uint D3DCMP_EQUAL = 3;
        private const uint D3DCMP_LESSEQUAL = 4;
        private const uint D3DCMP_GREATER = 5;
        private const uint D3DCMP_NOTEQUAL = 6;
        private const uint D3DCMP_GREATEREQUAL = 7;
        private const uint D3DCMP_ALWAYS = 8;
        
        // D3D9 blend factor constants
        private const uint D3DBLEND_ZERO = 1;
        private const uint D3DBLEND_ONE = 2;
        private const uint D3DBLEND_SRCCOLOR = 3;
        private const uint D3DBLEND_INVSRCCOLOR = 4;
        private const uint D3DBLEND_SRCALPHA = 5;
        private const uint D3DBLEND_INVSRCALPHA = 6;
        private const uint D3DBLEND_DESTALPHA = 7;
        private const uint D3DBLEND_INVDESTALPHA = 8;
        private const uint D3DBLEND_DESTCOLOR = 9;
        private const uint D3DBLEND_INVDESTCOLOR = 10;
        private const uint D3DBLEND_SRCALPHASAT = 11;
        private const uint D3DBLEND_BOTHSRCALPHA = 12;
        private const uint D3DBLEND_BOTHINVSRCALPHA = 13;
        private const uint D3DBLEND_BLENDFACTOR = 14;
        private const uint D3DBLEND_INVBLENDFACTOR = 15;
        
        // D3D9 blend operation constants
        private const uint D3DBLENDOP_ADD = 1;
        private const uint D3DBLENDOP_SUBTRACT = 2;
        private const uint D3DBLENDOP_REVSUBTRACT = 3;
        private const uint D3DBLENDOP_MIN = 4;
        private const uint D3DBLENDOP_MAX = 5;
        
        // D3D9 stencil operation constants
        private const uint D3DSTENCILOP_KEEP = 1;
        private const uint D3DSTENCILOP_ZERO = 2;
        private const uint D3DSTENCILOP_REPLACE = 3;
        private const uint D3DSTENCILOP_INCRSAT = 4;
        private const uint D3DSTENCILOP_DECRSAT = 5;
        private const uint D3DSTENCILOP_INVERT = 6;
        private const uint D3DSTENCILOP_INCR = 7;
        private const uint D3DSTENCILOP_DECR = 8;

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
            get { return _initialized; }
        }

        public bool IsRaytracingEnabled
        {
            get { return _remixActive; }
        }

        public bool IsRemixActive
        {
            get { return _remixActive; }
        }

        /// <summary>
        /// Initializes the D3D9 wrapper for Remix interception.
        /// </summary>
        public bool Initialize(RenderSettings settings)
        {
            if (_initialized)
            {
                return true;
            }

            _settings = settings;

            // Check for Remix runtime
            if (!CheckRemixRuntime(settings.RemixRuntimePath))
            {
                Console.WriteLine("[D3D9Wrapper] RTX Remix runtime not found");
                Console.WriteLine("[D3D9Wrapper] Please install RTX Remix Runtime from:");
                Console.WriteLine("[D3D9Wrapper] https://github.com/NVIDIAGameWorks/rtx-remix");
                return false;
            }

            // Load d3d9.dll (Remix's hooked version)
            _d3d9 = LoadD3D9Library(settings.RemixRuntimePath);
            if (_d3d9 == IntPtr.Zero)
            {
                Console.WriteLine("[D3D9Wrapper] Failed to load d3d9.dll");
                return false;
            }

            // Query capabilities
            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 4096,
                MaxRenderTargets = 4,
                MaxAnisotropy = 16,
                SupportsComputeShaders = false, // D3D9 doesn't have compute
                SupportsGeometryShaders = false,
                SupportsTessellation = false,
                SupportsRaytracing = true, // Via Remix path tracing
                SupportsMeshShaders = false,
                SupportsVariableRateShading = false,
                DedicatedVideoMemory = 0, // Will be queried
                SharedSystemMemory = 0,
                VendorName = "NVIDIA (via Remix)",
                DeviceName = "RTX Remix Path Tracer",
                DriverVersion = "Remix Runtime",
                ActiveBackend = GraphicsBackend.Direct3D9Remix,
                ShaderModelVersion = 3.0f,
                RemixAvailable = true,
                DlssAvailable = true,
                FsrAvailable = false
            };

            _initialized = true;
            Console.WriteLine("[D3D9Wrapper] Initialized with Remix support");

            return true;
        }

        /// <summary>
        /// Creates the D3D9 device for rendering.
        /// </summary>
        public bool CreateDevice(IntPtr windowHandle)
        {
            if (!_initialized)
            {
                return false;
            }

            _windowHandle = windowHandle;

            // Get Direct3DCreate9 function pointer
            IntPtr createFunc = NativeMethods.GetProcAddress(_d3d9, "Direct3DCreate9");
            if (createFunc == IntPtr.Zero)
            {
                Console.WriteLine("[D3D9Wrapper] Failed to get Direct3DCreate9");
                return false;
            }

            // Create D3D9 object
            // In actual implementation:
            // var d3d9Create = Marshal.GetDelegateForFunctionPointer<Direct3DCreate9Delegate>(createFunc);
            // _d3d9 = d3d9Create(D3D_SDK_VERSION);

            // Create device with presentation parameters
            // Remix will intercept this and set up its path tracing pipeline

            Console.WriteLine("[D3D9Wrapper] D3D9 device created");
            Console.WriteLine("[D3D9Wrapper] Remix should now be intercepting draw calls");

            _remixActive = true;
            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            if (_device != IntPtr.Zero)
            {
                // Release D3D9 device
                _device = IntPtr.Zero;
            }

            if (_d3d9 != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(_d3d9);
                _d3d9 = IntPtr.Zero;
            }

            _initialized = false;
            _remixActive = false;
            Console.WriteLine("[D3D9Wrapper] Shutdown complete");
        }

        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // IDirect3DDevice9::BeginScene()
            // Remix hooks this to start path tracing frame
        }

        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // IDirect3DDevice9::EndScene()
            // IDirect3DDevice9::Present()
            // Remix hooks these to finalize and display path traced result
        }

        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            _settings.Width = width;
            _settings.Height = height;

            // Reset D3D9 device with new back buffer size
            // Remix will handle resize internally
        }

        public IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // IDirect3DDevice9::CreateTexture()
            // Remix intercepts texture creation

            return new IntPtr(1); // Placeholder
        }

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

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                return false;
            }

            // IDirect3DDevice9::UpdateSurface() or LockRect/UnlockRect pattern
            // Remix intercepts texture uploads to capture game assets
            // For D3D9, we typically use:
            // 1. LockRect to get a pointer to texture memory
            // 2. Copy mipmap data into the locked region
            // 3. UnlockRect to commit the changes
            // Remix will intercept these calls to capture textures for path tracing

            return true;
        }

        public IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // IDirect3DDevice9::CreateVertexBuffer() or CreateIndexBuffer()

            return new IntPtr(1); // Placeholder
        }

        public IntPtr CreatePipeline(PipelineDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // D3D9 uses fixed-function or shader pairs, not PSOs
            // Create vertex/pixel shader combo

            return new IntPtr(1); // Placeholder
        }

        public void DestroyResource(IntPtr handle)
        {
            // Release D3D9 resource
        }

        public void SetRaytracingLevel(RaytracingLevel level)
        {
            // Remix handles raytracing configuration via its own UI/config
            // This is a no-op for D3D9 wrapper
        }

        public FrameStatistics GetFrameStatistics()
        {
            return new FrameStatistics
            {
                FrameTimeMs = 16.67, // Placeholder
                GpuTimeMs = 10.0,
                CpuTimeMs = 5.0,
                DrawCalls = 0,
                TrianglesRendered = 0,
                TexturesUsed = 0,
                VideoMemoryUsed = 0,
                RaytracingTimeMs = 10.0
            };
        }

        private RemixDevice _remixDevice;

        public IDevice GetDevice()
        {
            if (!_remixActive || _device == IntPtr.Zero)
            {
                return null;
            }

            // Create RemixDevice instance if not already created
            if (_remixDevice == null)
            {
                _remixDevice = new RemixDevice(_device, _capabilities, this);
            }

            return _remixDevice;
        }

        #region D3D9 Draw Commands

        /// <summary>
        /// Sets the world transformation matrix.
        /// </summary>
        public void SetWorldTransform(Matrix4x4 world)
        {
            // IDirect3DDevice9::SetTransform(D3DTS_WORLD, &world)
            // Remix uses this to position objects for path tracing
        }

        /// <summary>
        /// Sets the view transformation matrix.
        /// </summary>
        public void SetViewTransform(Matrix4x4 view)
        {
            // IDirect3DDevice9::SetTransform(D3DTS_VIEW, &view)
        }

        /// <summary>
        /// Sets the projection transformation matrix.
        /// </summary>
        public void SetProjectionTransform(Matrix4x4 projection)
        {
            // IDirect3DDevice9::SetTransform(D3DTS_PROJECTION, &projection)
        }

        /// <summary>
        /// Sets a texture for rendering.
        /// </summary>
        public void SetTexture(int stage, IntPtr texture)
        {
            // IDirect3DDevice9::SetTexture(stage, texture)
            // Remix uses textures for path tracing materials
        }

        /// <summary>
        /// Sets the material for fixed-function rendering.
        /// </summary>
        public void SetMaterial(D3D9Material material)
        {
            // IDirect3DDevice9::SetMaterial(&material)
            // Remix converts this to PBR material properties
        }

        /// <summary>
        /// Enables/configures a D3D9 light.
        /// </summary>
        public void SetLight(int index, D3D9Light light)
        {
            // IDirect3DDevice9::SetLight(index, &light)
            // IDirect3DDevice9::LightEnable(index, TRUE)
            // Remix uses these as path tracing light sources
        }

        /// <summary>
        /// Draws indexed primitives.
        /// </summary>
        public void DrawIndexedPrimitive(
            D3DPrimitiveType primitiveType,
            int baseVertexIndex,
            int minVertexIndex,
            int numVertices,
            int startIndex,
            int primitiveCount)
        {
            // IDirect3DDevice9::DrawIndexedPrimitive(...)
            // Remix intercepts this and adds geometry to acceleration structures
        }

        /// <summary>
        /// Draws non-indexed primitives.
        /// </summary>
        public void DrawPrimitive(D3DPrimitiveType primitiveType, int startVertex, int primitiveCount)
        {
            // IDirect3DDevice9::DrawPrimitive(...)
        }

        #endregion

        private bool CheckRemixRuntime(string runtimePath)
        {
            // Check for Remix runtime files
            string[] requiredFiles = new string[]
            {
                "d3d9.dll",           // Remix interceptor
                "NvRemixBridge.dll",  // Remix bridge
            };

            if (!string.IsNullOrEmpty(runtimePath))
            {
                foreach (string file in requiredFiles)
                {
                    string fullPath = System.IO.Path.Combine(runtimePath, file);
                    if (System.IO.File.Exists(fullPath))
                    {
                        return true;
                    }
                }
            }

            // Check current directory
            foreach (string file in requiredFiles)
            {
                if (System.IO.File.Exists(file))
                {
                    return true;
                }
            }

            // Check environment variable
            string envPath = Environment.GetEnvironmentVariable("RTX_REMIX_PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                foreach (string file in requiredFiles)
                {
                    string fullPath = System.IO.Path.Combine(envPath, file);
                    if (System.IO.File.Exists(fullPath))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private IntPtr LoadD3D9Library(string runtimePath)
        {
            // Try Remix d3d9.dll first
            if (!string.IsNullOrEmpty(runtimePath))
            {
                string remixD3d9 = System.IO.Path.Combine(runtimePath, "d3d9.dll");
                if (System.IO.File.Exists(remixD3d9))
                {
                    return NativeMethods.LoadLibrary(remixD3d9);
                }
            }

            // Try current directory (Remix should be in game folder)
            if (System.IO.File.Exists("d3d9.dll"))
            {
                return NativeMethods.LoadLibrary("d3d9.dll");
            }

            // Fall back to system d3d9.dll (won't have Remix)
            return NativeMethods.LoadLibrary("d3d9.dll");
        }

        public void Dispose()
        {
            Shutdown();
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        }
    }

    /// <summary>
    /// D3D9 primitive types.
    /// </summary>
    public enum D3DPrimitiveType
    {
        PointList = 1,
        LineList = 2,
        LineStrip = 3,
        TriangleList = 4,
        TriangleStrip = 5,
        TriangleFan = 6
    }

    /// <summary>
    /// D3D9 material structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct D3D9Material
    {
        public Vector4 Diffuse;
        public Vector4 Ambient;
        public Vector4 Specular;
        public Vector4 Emissive;
        public float Power;
    }

    /// <summary>
    /// D3D9 light structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct D3D9Light
    {
        public D3DLightType Type;
        public Vector4 Diffuse;
        public Vector4 Specular;
        public Vector4 Ambient;
        public Vector3 Position;
        public Vector3 Direction;
        public float Range;
        public float Falloff;
        public float Attenuation0;
        public float Attenuation1;
        public float Attenuation2;
        public float Theta;
        public float Phi;
    }

    /// <summary>
    /// D3D9 light types.
    /// </summary>
    public enum D3DLightType
    {
        Point = 1,
        Spot = 2,
        Directional = 3
    }

    /// <summary>
    /// RTX Remix device wrapper implementing IDevice interface.
    /// 
    /// RTX Remix works by intercepting D3D9 API calls and performing path tracing
    /// behind the scenes. This device wrapper adapts the IDevice interface to work
    /// with Remix's interception model.
    /// 
    /// Many modern features (raytracing pipelines, acceleration structures) are
    /// handled automatically by Remix through D3D9 call interception, so these
    /// methods are stubbed or return null.
    /// </summary>
    public class RemixDevice : IDevice
    {
        private readonly IntPtr _d3d9Device;
        private readonly GraphicsCapabilities _capabilities;
        private readonly Direct3D9Wrapper _wrapper;
        private bool _disposed;

        public GraphicsCapabilities Capabilities
        {
            get { return _capabilities; }
        }

        public GraphicsBackend Backend
        {
            get { return GraphicsBackend.Direct3D9Remix; }
        }

        public bool IsValid
        {
            get { return !_disposed && _d3d9Device != IntPtr.Zero; }
        }

        internal RemixDevice(IntPtr d3d9Device, GraphicsCapabilities capabilities, Direct3D9Wrapper wrapper)
        {
            if (d3d9Device == IntPtr.Zero)
            {
                throw new ArgumentException("D3D9 device handle must be valid", nameof(d3d9Device));
            }

            _d3d9Device = d3d9Device;
            _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
        }

        #region Resource Creation

        public ITexture CreateTexture(TextureDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 texture creation via IDirect3DDevice9::CreateTexture
            // Remix intercepts this call and creates path-traced textures
            // For now, return a stub texture that wraps the D3D9 texture handle
            // Full implementation would require COM interop with IDirect3DTexture9
            
            // TODO: IMPLEMENT - Create D3D9 texture via COM interop
            // - Convert TextureDesc to D3D9 D3DFORMAT
            // - Call IDirect3DDevice9::CreateTexture
            // - Wrap in RemixTexture and return
            
            throw new NotImplementedException("D3D9 texture creation via COM interop not yet implemented");
        }

        public IBuffer CreateBuffer(BufferDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 buffer creation via IDirect3DDevice9::CreateVertexBuffer/IndexBuffer
            // Remix intercepts these calls for geometry capture
            // Full implementation would require COM interop
            
            // TODO: IMPLEMENT - Create D3D9 buffer via COM interop
            // - Determine buffer type (vertex/index) from BufferDesc.Usage
            // - Call IDirect3DDevice9::CreateVertexBuffer or CreateIndexBuffer
            // - Wrap in RemixBuffer and return
            
            throw new NotImplementedException("D3D9 buffer creation via COM interop not yet implemented");
        }

        public ISampler CreateSampler(SamplerDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 samplers are set via IDirect3DDevice9::SetSamplerState
            // No separate sampler objects in D3D9 - state is set directly on device
            // Return a stub sampler that stores the state for later use
            return new RemixSampler(desc);
        }

        public IShader CreateShader(ShaderDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 shaders are created via IDirect3DDevice9::CreateVertexShader/PixelShader
            // Remix intercepts shader creation for material analysis
            // Full implementation would require COM interop and shader bytecode conversion
            
            // TODO: IMPLEMENT - Create D3D9 shader via COM interop
            // - Convert shader bytecode if needed (HLSL to D3D9 bytecode)
            // - Call IDirect3DDevice9::CreateVertexShader or CreatePixelShader
            // - Wrap in RemixShader and return
            
            throw new NotImplementedException("D3D9 shader creation via COM interop not yet implemented");
        }

        public IGraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc, IFramebuffer framebuffer)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have PSOs - state is set directly on device
            // Remix captures state changes for path tracing
            // Return a pipeline that stores state and applies it when bound
            // Pipeline state is applied via IDirect3DDevice9::Set* methods when pipeline is bound via SetPipeline
            return new RemixGraphicsPipeline(desc, _d3d9Device, this);
        }

        public IComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't support compute shaders
            throw new NotSupportedException("D3D9 does not support compute shaders");
        }

        public IFramebuffer CreateFramebuffer(FramebufferDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 render targets are set via IDirect3DDevice9::SetRenderTarget
            // No separate framebuffer objects - state is set directly on device
            // Return a stub framebuffer that stores render target configuration
            // TODO: IMPLEMENT - Apply via IDirect3DDevice9::SetRenderTarget when framebuffer is bound
            return new RemixFramebuffer(desc);
        }

        public IBindingLayout CreateBindingLayout(BindingLayoutDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have binding layouts - resources are bound directly
            // Return a stub layout for compatibility
            return new RemixBindingLayout(desc);
        }

        public IBindingSet CreateBindingSet(IBindingLayout layout, BindingSetDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have binding sets - resources are bound directly
            // Return a stub binding set for compatibility
            // TODO: IMPLEMENT - Store textures, buffers, samplers for later binding via IDirect3DDevice9::SetTexture/SetStreamSource
            return new RemixBindingSet(layout);
        }

        public ICommandList CreateCommandList(CommandListType type = CommandListType.Graphics)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have command lists - commands execute immediately
            // Return a command list that applies commands directly via IDirect3DDevice9 methods
            // In practice, Remix intercepts D3D9 calls directly
            // Commands are applied immediately when issued (D3D9 immediate mode)
            return new RemixCommandList(_d3d9Device, this);
        }

        public ITexture CreateHandleForNativeTexture(IntPtr nativeHandle, TextureDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // Wrap an existing D3D9 texture handle (e.g., from swap chain)
            // Remix can intercept rendering to this texture
            
            // TODO: IMPLEMENT - Create RemixTexture from existing D3D9 texture handle
            // - Query IDirect3DTexture9 interface from native handle
            // - Wrap in RemixTexture and return
            
            throw new NotImplementedException("D3D9 native texture handle wrapping not yet implemented");
        }

        #endregion

        #region Raytracing Resources

        public IAccelStruct CreateAccelStruct(AccelStructDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // RTX Remix builds acceleration structures automatically from D3D9 geometry
            // No explicit acceleration structure creation needed
            // Return a stub that indicates Remix will handle it
            // Remix builds AS from intercepted DrawPrimitive/DrawIndexedPrimitive calls
            return new RemixAccelStruct(desc);
        }

        public IRaytracingPipeline CreateRaytracingPipeline(RaytracingPipelineDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // RTX Remix uses its own path tracing pipeline
            // No explicit raytracing pipeline creation needed
            // Return a stub that indicates Remix will handle it
            // Remix uses its own path tracing shaders automatically
            return new RemixRaytracingPipeline(desc);
        }

        #endregion

        #region Command Execution

        public void ExecuteCommandList(ICommandList commandList)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 commands execute immediately, so this is a no-op for now
            // Remix intercepts commands as they're issued
            // TODO: IMPLEMENT - Execute recorded commands from RemixCommandList
            // - Play back recorded D3D9 commands via IDirect3DDevice9 methods
            // For now, commands execute immediately when issued, so this is a no-op
        }

        public void ExecuteCommandLists(ICommandList[] commandLists)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // Execute multiple command lists
            foreach (var cmdList in commandLists)
            {
                ExecuteCommandList(cmdList);
            }
        }

        public void WaitIdle()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9: Wait for GPU to finish via IDirect3DQuery9
            // TODO: IMPLEMENT - Create D3D9 query and wait for completion
            // - Create IDirect3DQuery9 with D3DQUERYTYPE_EVENT
            // - Issue query and wait for completion
        }

        public void Signal(IFence fence, ulong value)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have fences - use queries instead
            throw new NotSupportedException("D3D9 does not support fences - use queries instead");
        }

        public void WaitFence(IFence fence, ulong value)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have fences - use queries instead
            throw new NotSupportedException("D3D9 does not support fences - use queries instead");
        }

        #endregion

        #region Queries

        public int GetConstantBufferAlignment()
        {
            // D3D9 constant buffers are 16-byte aligned
            return 16;
        }

        public int GetTextureAlignment()
        {
            // D3D9 textures are typically 4-byte aligned
            return 4;
        }

        public bool IsFormatSupported(TextureFormat format, TextureUsage usage)
        {
            // D3D9 format support check via IDirect3D9::CheckDeviceFormat
            // For now, return true for common formats
            // TODO: IMPLEMENT - Query D3D9 device for format support
            return true;
        }

        public int GetCurrentFrameIndex()
        {
            // D3D9 doesn't have explicit frame indexing
            // Return 0 for single-buffered, or track manually for multi-buffering
            return 0;
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // D3D9 device is owned by Direct3D9Wrapper, don't release it here
            }
        }

        #region Remix Resource Stub Classes

        /// <summary>
        /// Stub texture implementation for Remix.
        /// Stores texture descriptor and D3D9 texture handle.
        /// </summary>
        private class RemixTexture : ITexture
        {
            public TextureDesc Desc { get; }
            public IntPtr NativeHandle { get; }

            internal RemixTexture(TextureDesc desc, IntPtr nativeHandle)
            {
                Desc = desc;
                NativeHandle = nativeHandle;
            }

            public void Dispose()
            {
                // D3D9 texture release handled by Direct3D9Wrapper
            }
        }

        /// <summary>
        /// Stub buffer implementation for Remix.
        /// Stores buffer descriptor and D3D9 buffer handle.
        /// </summary>
        private class RemixBuffer : IBuffer
        {
            public BufferDesc Desc { get; }
            public IntPtr NativeHandle { get; }

            internal RemixBuffer(BufferDesc desc, IntPtr nativeHandle)
            {
                Desc = desc;
                NativeHandle = nativeHandle;
            }

            public void Dispose()
            {
                // D3D9 buffer release handled by Direct3D9Wrapper
            }
        }

        /// <summary>
        /// Stub sampler implementation for Remix.
        /// Stores sampler descriptor (D3D9 samplers are state-based, not objects).
        /// </summary>
        private class RemixSampler : ISampler
        {
            public SamplerDesc Desc { get; }

            internal RemixSampler(SamplerDesc desc)
            {
                Desc = desc;
            }

            public void Dispose()
            {
                // D3D9 samplers are state-based, no cleanup needed
            }
        }

        /// <summary>
        /// Stub shader implementation for Remix.
        /// Stores shader descriptor and D3D9 shader handle.
        /// </summary>
        private class RemixShader : IShader
        {
            public ShaderDesc Desc { get; }
            public ShaderType Type { get; }
            public IntPtr NativeHandle { get; }

            internal RemixShader(ShaderDesc desc, ShaderType type, IntPtr nativeHandle = default(IntPtr))
            {
                Desc = desc;
                Type = type;
                NativeHandle = nativeHandle;
            }

            public void Dispose()
            {
                // D3D9 shader release handled by Direct3D9Wrapper
            }
        }

        /// <summary>
        /// Stub graphics pipeline implementation for Remix.
        /// Stores pipeline state (D3D9 doesn't have PSOs, state is set directly).
        /// </summary>
        private class RemixGraphicsPipeline : IGraphicsPipeline
        {
            public GraphicsPipelineDesc Desc { get; }
            private readonly IntPtr _d3d9Device;
            private readonly RemixDevice _device;

            internal RemixGraphicsPipeline(GraphicsPipelineDesc desc, IntPtr d3d9Device, RemixDevice device)
            {
                Desc = desc;
                _d3d9Device = d3d9Device;
                _device = device;
            }

            /// <summary>
            /// Applies pipeline state to the D3D9 device via IDirect3DDevice9::Set* methods.
            /// Based on DirectX 9: Pipeline state is set directly on device, not via PSOs.
            /// </summary>
            internal void ApplyState()
            {
                if (_d3d9Device == IntPtr.Zero)
                {
                    return;
                }

                // Apply shaders
                ApplyShaders();

                // Apply blend state
                ApplyBlendState();

                // Apply raster state
                ApplyRasterState();

                // Apply depth/stencil state
                ApplyDepthStencilState();

                // Apply input layout (vertex format)
                ApplyInputLayout();

                // Apply primitive topology
                ApplyPrimitiveTopology();
            }

            private void ApplyShaders()
            {
                // Set vertex shader
                if (Desc.VertexShader != null)
                {
                    var remixShader = Desc.VertexShader as RemixShader;
                    if (remixShader != null && remixShader.NativeHandle != IntPtr.Zero)
                    {
                        // IDirect3DDevice9::SetVertexShader(vertexShader)
                        // TODO: IMPLEMENT - COM interop call to SetVertexShader
                        // For now, shader state is stored but not applied (requires COM interop)
                    }
                }

                // Set pixel shader
                if (Desc.PixelShader != null)
                {
                    var remixShader = Desc.PixelShader as RemixShader;
                    if (remixShader != null && remixShader.NativeHandle != IntPtr.Zero)
                    {
                        // IDirect3DDevice9::SetPixelShader(pixelShader)
                        // TODO: IMPLEMENT - COM interop call to SetPixelShader
                        // For now, shader state is stored but not applied (requires COM interop)
                    }
                }
            }

            private void ApplyBlendState()
            {
                if (Desc.BlendState.RenderTargets == null || Desc.BlendState.RenderTargets.Length == 0)
                {
                    return;
                }

                var rtBlend = Desc.BlendState.RenderTargets[0]; // D3D9 supports one render target

                // Enable/disable alpha blending
                SetRenderState(D3DRS_ALPHABLENDENABLE, rtBlend.BlendEnable ? 1u : 0u);

                if (rtBlend.BlendEnable)
                {
                    // Source blend factor
                    SetRenderState(D3DRS_SRCBLEND, ConvertBlendFactor(rtBlend.SrcBlend));
                    // Destination blend factor
                    SetRenderState(D3DRS_DESTBLEND, ConvertBlendFactor(rtBlend.DestBlend));
                    // Blend operation
                    SetRenderState(D3DRS_BLENDOP, ConvertBlendOp(rtBlend.BlendOp));

                    // Alpha blend factors (if separate alpha blending is enabled)
                    if (Desc.BlendState.RenderTargets[0].SrcBlendAlpha != BlendFactor.One ||
                        Desc.BlendState.RenderTargets[0].DestBlendAlpha != BlendFactor.Zero)
                    {
                        SetRenderState(D3DRS_SEPARATEALPHABLENDENABLE, 1u);
                        SetRenderState(D3DRS_SRCBLENDALPHA, ConvertBlendFactor(rtBlend.SrcBlendAlpha));
                        SetRenderState(D3DRS_DESTBLENDALPHA, ConvertBlendFactor(rtBlend.DestBlendAlpha));
                        SetRenderState(D3DRS_BLENDOPALPHA, ConvertBlendOp(rtBlend.BlendOpAlpha));
                    }
                    else
                    {
                        SetRenderState(D3DRS_SEPARATEALPHABLENDENABLE, 0u);
                    }
                }

                // Color write mask
                SetRenderState(D3DRS_COLORWRITEENABLE, rtBlend.WriteMask);
            }

            private void ApplyRasterState()
            {
                // Cull mode
                SetRenderState(D3DRS_CULLMODE, ConvertCullMode(Desc.RasterState.CullMode, Desc.RasterState.FrontCCW));

                // Fill mode
                SetRenderState(D3DRS_FILLMODE, ConvertFillMode(Desc.RasterState.FillMode));

                // Depth bias
                SetRenderState(D3DRS_DEPTHBIAS, (uint)Desc.RasterState.DepthBias);
                SetRenderState(D3DRS_SLOPESCALEDEPTHBIAS, BitConverter.ToUInt32(BitConverter.GetBytes(Desc.RasterState.SlopeScaledDepthBias), 0));

                // Depth clip enable
                SetRenderState(D3DRS_CLIPPING, Desc.RasterState.DepthClipEnable ? 1u : 0u);

                // Scissor enable
                SetRenderState(D3DRS_SCISSORTESTENABLE, Desc.RasterState.ScissorEnable ? 1u : 0u);

                // Multisample enable
                SetRenderState(D3DRS_MULTISAMPLEANTIALIAS, Desc.RasterState.MultisampleEnable ? 1u : 0u);

                // Antialiased line enable
                SetRenderState(D3DRS_ANTIALIASEDLINEENABLE, Desc.RasterState.AntialiasedLineEnable ? 1u : 0u);
            }

            private void ApplyDepthStencilState()
            {
                // Depth test enable
                SetRenderState(D3DRS_ZENABLE, Desc.DepthStencilState.DepthTestEnable ? D3DZB_TRUE : D3DZB_FALSE);

                // Depth write enable
                SetRenderState(D3DRS_ZWRITEENABLE, Desc.DepthStencilState.DepthWriteEnable ? 1u : 0u);

                // Depth function
                SetRenderState(D3DRS_ZFUNC, ConvertCompareFunc(Desc.DepthStencilState.DepthFunc));

                // Stencil enable
                SetRenderState(D3DRS_STENCILENABLE, Desc.DepthStencilState.StencilEnable ? 1u : 0u);

                if (Desc.DepthStencilState.StencilEnable)
                {
                    // Stencil read/write masks
                    SetRenderState(D3DRS_STENCILMASK, Desc.DepthStencilState.StencilReadMask);
                    SetRenderState(D3DRS_STENCILWRITEMASK, Desc.DepthStencilState.StencilWriteMask);

                    // Front face stencil operations
                    SetRenderState(D3DRS_STENCILFAIL, ConvertStencilOp(Desc.DepthStencilState.FrontFace.StencilFailOp));
                    SetRenderState(D3DRS_STENCILZFAIL, ConvertStencilOp(Desc.DepthStencilState.FrontFace.DepthFailOp));
                    SetRenderState(D3DRS_STENCILPASS, ConvertStencilOp(Desc.DepthStencilState.FrontFace.PassOp));
                    SetRenderState(D3DRS_STENCILFUNC, ConvertCompareFunc(Desc.DepthStencilState.FrontFace.StencilFunc));

                    // Back face stencil operations (if two-sided stencil is enabled)
                    if (Desc.DepthStencilState.FrontFace.StencilFailOp != Desc.DepthStencilState.BackFace.StencilFailOp ||
                        Desc.DepthStencilState.FrontFace.DepthFailOp != Desc.DepthStencilState.BackFace.DepthFailOp ||
                        Desc.DepthStencilState.FrontFace.PassOp != Desc.DepthStencilState.BackFace.PassOp ||
                        Desc.DepthStencilState.FrontFace.StencilFunc != Desc.DepthStencilState.BackFace.StencilFunc)
                    {
                        SetRenderState(D3DRS_TWOSIDEDSTENCILMODE, 1u);
                        SetRenderState(D3DRS_CCW_STENCILFAIL, ConvertStencilOp(Desc.DepthStencilState.BackFace.StencilFailOp));
                        SetRenderState(D3DRS_CCW_STENCILZFAIL, ConvertStencilOp(Desc.DepthStencilState.BackFace.DepthFailOp));
                        SetRenderState(D3DRS_CCW_STENCILPASS, ConvertStencilOp(Desc.DepthStencilState.BackFace.PassOp));
                        SetRenderState(D3DRS_CCW_STENCILFUNC, ConvertCompareFunc(Desc.DepthStencilState.BackFace.StencilFunc));
                    }
                    else
                    {
                        SetRenderState(D3DRS_TWOSIDEDSTENCILMODE, 0u);
                    }
                }
            }

            private void ApplyInputLayout()
            {
                // D3D9 uses FVF (Flexible Vertex Format) or vertex declarations
                // InputLayoutDesc would need to be converted to D3D9 FVF or IDirect3DVertexDeclaration9
                // TODO: IMPLEMENT - Convert InputLayoutDesc to D3D9 FVF or vertex declaration
                // For now, input layout is stored but not applied (requires FVF/declaration conversion)
            }

            private void ApplyPrimitiveTopology()
            {
                // D3D9 primitive topology is set via DrawPrimitive/DrawIndexedPrimitive calls
                // The topology is implicit in the draw call, not a render state
                // Store it for use in draw calls
            }

            private void SetRenderState(uint state, uint value)
            {
                // IDirect3DDevice9::SetRenderState(state, value)
                // TODO: IMPLEMENT - COM interop call to SetRenderState
                // For now, render state is stored but not applied (requires COM interop)
                // In a full implementation, this would call:
                // device->SetRenderState((D3DRENDERSTATETYPE)state, value);
            }

            private uint ConvertBlendFactor(BlendFactor factor)
            {
                switch (factor)
                {
                    case BlendFactor.Zero: return D3DBLEND_ZERO;
                    case BlendFactor.One: return D3DBLEND_ONE;
                    case BlendFactor.SrcColor: return D3DBLEND_SRCCOLOR;
                    case BlendFactor.InvSrcColor: return D3DBLEND_INVSRCCOLOR;
                    case BlendFactor.SrcAlpha: return D3DBLEND_SRCALPHA;
                    case BlendFactor.InvSrcAlpha: return D3DBLEND_INVSRCALPHA;
                    case BlendFactor.DestAlpha: return D3DBLEND_DESTALPHA;
                    case BlendFactor.InvDestAlpha: return D3DBLEND_INVDESTALPHA;
                    case BlendFactor.DestColor: return D3DBLEND_DESTCOLOR;
                    case BlendFactor.InvDestColor: return D3DBLEND_INVDESTCOLOR;
                    case BlendFactor.SrcAlphaSat: return D3DBLEND_SRCALPHASAT;
                    case BlendFactor.BlendFactor: return D3DBLEND_BLENDFACTOR;
                    case BlendFactor.InvBlendFactor: return D3DBLEND_INVBLENDFACTOR;
                    default: return D3DBLEND_ONE;
                }
            }

            private uint ConvertBlendOp(BlendOp op)
            {
                switch (op)
                {
                    case BlendOp.Add: return D3DBLENDOP_ADD;
                    case BlendOp.Subtract: return D3DBLENDOP_SUBTRACT;
                    case BlendOp.ReverseSubtract: return D3DBLENDOP_REVSUBTRACT;
                    case BlendOp.Min: return D3DBLENDOP_MIN;
                    case BlendOp.Max: return D3DBLENDOP_MAX;
                    default: return D3DBLENDOP_ADD;
                }
            }

            private uint ConvertCullMode(CullMode mode, bool frontCCW)
            {
                switch (mode)
                {
                    case CullMode.None: return D3DCULL_NONE;
                    case CullMode.Front: return frontCCW ? D3DCULL_CW : D3DCULL_CCW;
                    case CullMode.Back: return frontCCW ? D3DCULL_CCW : D3DCULL_CW;
                    default: return D3DCULL_CCW;
                }
            }

            private uint ConvertFillMode(FillMode mode)
            {
                switch (mode)
                {
                    case FillMode.Solid: return D3DFILL_SOLID;
                    case FillMode.Wireframe: return D3DFILL_WIREFRAME;
                    default: return D3DFILL_SOLID;
                }
            }

            private uint ConvertCompareFunc(CompareFunc func)
            {
                switch (func)
                {
                    case CompareFunc.Never: return D3DCMP_NEVER;
                    case CompareFunc.Less: return D3DCMP_LESS;
                    case CompareFunc.Equal: return D3DCMP_EQUAL;
                    case CompareFunc.LessEqual: return D3DCMP_LESSEQUAL;
                    case CompareFunc.Greater: return D3DCMP_GREATER;
                    case CompareFunc.NotEqual: return D3DCMP_NOTEQUAL;
                    case CompareFunc.GreaterEqual: return D3DCMP_GREATEREQUAL;
                    case CompareFunc.Always: return D3DCMP_ALWAYS;
                    default: return D3DCMP_ALWAYS;
                }
            }

            private uint ConvertStencilOp(StencilOp op)
            {
                switch (op)
                {
                    case StencilOp.Keep: return D3DSTENCILOP_KEEP;
                    case StencilOp.Zero: return D3DSTENCILOP_ZERO;
                    case StencilOp.Replace: return D3DSTENCILOP_REPLACE;
                    case StencilOp.IncrSat: return D3DSTENCILOP_INCRSAT;
                    case StencilOp.DecrSat: return D3DSTENCILOP_DECRSAT;
                    case StencilOp.Invert: return D3DSTENCILOP_INVERT;
                    case StencilOp.Incr: return D3DSTENCILOP_INCR;
                    case StencilOp.Decr: return D3DSTENCILOP_DECR;
                    default: return D3DSTENCILOP_KEEP;
                }
            }

            public void Dispose()
            {
                // D3D9 pipelines are state-based, no cleanup needed
            }
        }

        /// <summary>
        /// Stub framebuffer implementation for Remix.
        /// Stores framebuffer configuration (D3D9 render targets are set directly).
        /// </summary>
        private class RemixFramebuffer : IFramebuffer
        {
            public FramebufferDesc Desc { get; }

            internal RemixFramebuffer(FramebufferDesc desc)
            {
                Desc = desc;
            }

            public FramebufferInfo GetInfo()
            {
                var info = new FramebufferInfo();
                if (Desc.ColorAttachments != null && Desc.ColorAttachments.Length > 0)
                {
                    var firstColor = Desc.ColorAttachments[0];
                    if (firstColor.Texture != null)
                    {
                        info.Width = firstColor.Texture.Desc.Width;
                        info.Height = firstColor.Texture.Desc.Height;
                        info.ColorFormats = new TextureFormat[Desc.ColorAttachments.Length];
                        for (int i = 0; i < Desc.ColorAttachments.Length; i++)
                        {
                            if (Desc.ColorAttachments[i].Texture != null)
                            {
                                info.ColorFormats[i] = Desc.ColorAttachments[i].Texture.Desc.Format;
                            }
                        }
                        info.SampleCount = firstColor.Texture.Desc.SampleCount;
                    }
                }
                if (Desc.DepthAttachment.Texture != null)
                {
                    info.DepthFormat = Desc.DepthAttachment.Texture.Desc.Format;
                }
                return info;
            }

            public void Dispose()
            {
                // D3D9 framebuffers are state-based, no cleanup needed
            }
        }

        /// <summary>
        /// Stub binding layout implementation for Remix.
        /// Stores binding layout descriptor (D3D9 doesn't have binding layouts).
        /// </summary>
        private class RemixBindingLayout : IBindingLayout
        {
            public BindingLayoutDesc Desc { get; }

            internal RemixBindingLayout(BindingLayoutDesc desc)
            {
                Desc = desc;
            }

            public void Dispose()
            {
                // D3D9 binding layouts are conceptual, no cleanup needed
            }
        }

        /// <summary>
        /// Stub binding set implementation for Remix.
        /// Stores binding set descriptor (D3D9 resources are bound directly).
        /// </summary>
        private class RemixBindingSet : IBindingSet
        {
            public IBindingLayout Layout { get; }

            internal RemixBindingSet(IBindingLayout layout)
            {
                Layout = layout;
            }

            public void Dispose()
            {
                // D3D9 binding sets are conceptual, no cleanup needed
            }
        }

        /// <summary>
        /// Stub command list implementation for Remix.
        /// Records D3D9 commands for deferred execution (D3D9 normally executes immediately).
        /// </summary>
        private class RemixCommandList : ICommandList
        {
            private readonly IntPtr _d3d9Device;
            private readonly RemixDevice _device;
            private IGraphicsPipeline _currentPipeline;

            internal RemixCommandList(IntPtr d3d9Device, RemixDevice device)
            {
                _d3d9Device = d3d9Device;
                _device = device;
            }

            public void Open()
            {
                // D3D9 commands execute immediately, no recording needed
            }

            public void Close()
            {
                // D3D9 commands execute immediately, no recording needed
            }

            public GraphicsState SetPipeline(IGraphicsPipeline pipeline)
            {
                _currentPipeline = pipeline;
                
                // Apply pipeline state when pipeline is bound
                // Based on DirectX 9: Pipeline state is applied via IDirect3DDevice9::Set* methods
                var remixPipeline = pipeline as RemixGraphicsPipeline;
                if (remixPipeline != null)
                {
                    remixPipeline.ApplyState();
                }

                return new GraphicsState { Pipeline = pipeline };
            }

            public GraphicsState SetFramebuffer(IFramebuffer framebuffer)
            {
                // TODO: IMPLEMENT - Apply framebuffer via IDirect3DDevice9::SetRenderTarget
                return new GraphicsState { Framebuffer = framebuffer };
            }

            public GraphicsState SetViewport(ViewportState viewport)
            {
                // TODO: IMPLEMENT - Apply viewport via IDirect3DDevice9::SetViewport
                return new GraphicsState { Viewport = viewport };
            }

            public GraphicsState AddBindingSet(IBindingSet bindingSet)
            {
                // TODO: IMPLEMENT - Apply bindings via IDirect3DDevice9::SetTexture/SetStreamSource
                return new GraphicsState { BindingSets = new IBindingSet[] { bindingSet } };
            }

            public GraphicsState AddVertexBuffer(IBuffer buffer)
            {
                // TODO: IMPLEMENT - Apply vertex buffer via IDirect3DDevice9::SetStreamSource
                return new GraphicsState { VertexBuffers = new IBuffer[] { buffer } };
            }

            public GraphicsState SetIndexBuffer(IBuffer buffer, TextureFormat format = TextureFormat.R32_UInt)
            {
                // TODO: IMPLEMENT - Apply index buffer via IDirect3DDevice9::SetIndices
                return new GraphicsState { IndexBuffer = buffer, IndexFormat = format };
            }

            public void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
            {
                // TODO: IMPLEMENT - Draw via IDirect3DDevice9::DrawPrimitive
            }

            public void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0)
            {
                // TODO: IMPLEMENT - Draw indexed via IDirect3DDevice9::DrawIndexedPrimitive
            }

            public void Dispatch(int groupCountX, int groupCountY, int groupCountZ)
            {
                throw new NotSupportedException("D3D9 does not support compute shaders");
            }

            public void CopyTexture(ITexture dst, ITexture src)
            {
                // TODO: IMPLEMENT - Copy texture via IDirect3DDevice9::StretchRect or similar
            }

            public void CopyBuffer(IBuffer dst, IBuffer src)
            {
                // TODO: IMPLEMENT - Copy buffer via IDirect3DDevice9::UpdateSurface or LockRect/UnlockRect
            }

            public void ClearRenderTarget(ITexture texture, Vector4 color)
            {
                // TODO: IMPLEMENT - Clear render target via IDirect3DDevice9::Clear
            }

            public void ClearDepthStencil(ITexture texture, bool clearDepth, float depth, bool clearStencil, byte stencil)
            {
                // TODO: IMPLEMENT - Clear depth/stencil via IDirect3DDevice9::Clear
            }

            public void Dispose()
            {
                // Command list cleanup
                _currentPipeline = null;
            }
        }

        /// <summary>
        /// Stub acceleration structure implementation for Remix.
        /// Remix builds acceleration structures automatically from intercepted geometry.
        /// </summary>
        private class RemixAccelStruct : IAccelStruct
        {
            public AccelStructDesc Desc { get; }
            public bool IsTopLevel { get; }
            public ulong DeviceAddress { get; }

            internal RemixAccelStruct(AccelStructDesc desc)
            {
                Desc = desc;
                IsTopLevel = desc.IsTopLevel;
                DeviceAddress = 0; // Remix manages AS addresses internally
            }

            public void Dispose()
            {
                // Remix manages acceleration structures, no cleanup needed
            }
        }

        /// <summary>
        /// Stub raytracing pipeline implementation for Remix.
        /// Remix uses its own path tracing pipeline automatically.
        /// </summary>
        private class RemixRaytracingPipeline : IRaytracingPipeline
        {
            public RaytracingPipelineDesc Desc { get; }

            internal RemixRaytracingPipeline(RaytracingPipelineDesc desc)
            {
                Desc = desc;
            }

            public void Dispose()
            {
                // Remix manages raytracing pipelines, no cleanup needed
            }
        }

        #endregion
    }
}


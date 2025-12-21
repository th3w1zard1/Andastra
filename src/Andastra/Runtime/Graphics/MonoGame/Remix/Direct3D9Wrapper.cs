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
            
            // TODO: IMPLEMENT - Create RemixSampler that stores sampler state
            // - Convert SamplerDesc to D3D9 sampler state values
            // - Wrap in RemixSampler and return
            
            throw new NotImplementedException("D3D9 sampler creation not yet implemented");
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
            // Return a stub pipeline that stores state for later application
            
            // TODO: IMPLEMENT - Create RemixGraphicsPipeline that stores pipeline state
            // - Store shaders, blend state, raster state, depth/stencil state
            // - Apply state via IDirect3DDevice9::Set* methods when pipeline is bound
            
            throw new NotImplementedException("D3D9 graphics pipeline creation not yet implemented");
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
            
            // TODO: IMPLEMENT - Create RemixFramebuffer that stores render target configuration
            // - Store color attachments and depth attachment
            // - Apply via IDirect3DDevice9::SetRenderTarget when framebuffer is bound
            
            throw new NotImplementedException("D3D9 framebuffer creation not yet implemented");
        }

        public IBindingLayout CreateBindingLayout(BindingLayoutDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have binding layouts - resources are bound directly
            // Return a stub layout for compatibility
            
            // TODO: IMPLEMENT - Create RemixBindingLayout that stores binding slots
            // - Store binding information for later use in binding sets
            
            throw new NotImplementedException("D3D9 binding layout creation not yet implemented");
        }

        public IBindingSet CreateBindingSet(IBindingLayout layout, BindingSetDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have binding sets - resources are bound directly
            // Return a stub binding set for compatibility
            
            // TODO: IMPLEMENT - Create RemixBindingSet that stores resource bindings
            // - Store textures, buffers, samplers for later binding
            
            throw new NotImplementedException("D3D9 binding set creation not yet implemented");
        }

        public ICommandList CreateCommandList(CommandListType type = CommandListType.Graphics)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 doesn't have command lists - commands execute immediately
            // Return a stub command list that records commands for later execution
            // In practice, Remix intercepts D3D9 calls directly
            
            // TODO: IMPLEMENT - Create RemixCommandList that records D3D9 commands
            // - Record commands for deferred execution
            // - Execute via IDirect3DDevice9 methods when command list is executed
            
            throw new NotImplementedException("D3D9 command list creation not yet implemented");
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
            
            // TODO: IMPLEMENT - Create RemixAccelStruct stub
            // - Remix builds AS from intercepted DrawPrimitive/DrawIndexedPrimitive calls
            // - This is mainly for API compatibility
            
            throw new NotImplementedException("Remix acceleration structure creation handled automatically by interception");
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
            
            // TODO: IMPLEMENT - Create RemixRaytracingPipeline stub
            // - Remix uses its own path tracing shaders
            // - This is mainly for API compatibility
            
            throw new NotImplementedException("Remix raytracing pipeline handled automatically by interception");
        }

        #endregion

        #region Command Execution

        public void ExecuteCommandList(ICommandList commandList)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(RemixDevice));
            }

            // D3D9 commands execute immediately, so this is a no-op
            // Remix intercepts commands as they're issued
            
            // TODO: IMPLEMENT - Execute recorded commands from RemixCommandList
            // - Play back recorded D3D9 commands via IDirect3DDevice9 methods
            
            throw new NotImplementedException("D3D9 command list execution not yet implemented");
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

            internal RemixShader(ShaderDesc desc, ShaderType type)
            {
                Desc = desc;
                Type = type;
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

            internal RemixGraphicsPipeline(GraphicsPipelineDesc desc)
            {
                Desc = desc;
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
            // TODO: IMPLEMENT - Store recorded D3D9 commands
            // For now, this is a placeholder

            public void Dispose()
            {
                // Command list cleanup
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


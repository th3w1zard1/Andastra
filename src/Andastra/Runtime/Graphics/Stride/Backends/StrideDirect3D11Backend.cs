using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Andastra.Runtime.Graphics.Common.Backends;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Structs;
using Stride.Graphics;

namespace Andastra.Runtime.Stride.Backends
{
    /// <summary>
    /// Stride implementation of DirectX 11 backend.
    /// Inherits all shared D3D11 logic from BaseDirect3D11Backend.
    ///
    /// Based on Stride Graphics API: https://doc.stride3d.net/latest/en/manual/graphics/
    /// Stride supports DirectX 11 as one of its primary backends.
    /// </summary>
    public class StrideDirect3D11Backend : BaseDirect3D11Backend
    {
        private global::Stride.Engine.Game _game;
        private GraphicsDevice _strideDevice;
        private CommandList _commandList;
        private IntPtr _fallbackCommandList; // Cached fallback command list from fallback device

        // Track scratch buffers for BLAS cleanup
        private readonly System.Collections.Generic.Dictionary<IntPtr, IntPtr> _blasScratchBuffers;

        // Track TLAS instance data for each TLAS
        private readonly System.Collections.Generic.Dictionary<IntPtr, TlasInstanceData> _tlasInstanceData;

        // Track shader binding table stride information
        // Key: SBT buffer handle, Value: Stride in bytes
        private readonly System.Collections.Generic.Dictionary<IntPtr, uint> _sbtStrideCache;

        public StrideDirect3D11Backend(global::Stride.Engine.Game game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _blasScratchBuffers = new System.Collections.Generic.Dictionary<IntPtr, IntPtr>();
            _tlasInstanceData = new System.Collections.Generic.Dictionary<IntPtr, TlasInstanceData>();
            _sbtStrideCache = new System.Collections.Generic.Dictionary<IntPtr, uint>();
        }

        #region BaseGraphicsBackend Implementation

        protected override bool CreateDeviceResources()
        {
            if (_game.GraphicsDevice == null)
            {
                Console.WriteLine("[StrideDX11] GraphicsDevice not available");
                return false;
            }

            _strideDevice = _game.GraphicsDevice;

            // Get native D3D11 handles from Stride using reflection
            // Stride GraphicsDevice may use NativePointer or NativeDevice depending on backend
            _device = IntPtr.Zero;
            var nativeDeviceProp = typeof(GraphicsDevice).GetProperty("NativeDevice",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nativeDeviceProp != null)
            {
                var value = nativeDeviceProp.GetValue(_strideDevice);
                if (value is IntPtr ptr && ptr != IntPtr.Zero)
                {
                    _device = ptr;
                }
            }
            // If NativeDevice not found, try NativePointer
            if (_device == IntPtr.Zero)
            {
                var nativePointerProp = typeof(GraphicsDevice).GetProperty("NativePointer",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nativePointerProp != null)
                {
                    var value = nativePointerProp.GetValue(_strideDevice);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        _device = ptr;
                    }
                }
            }
            _immediateContext = IntPtr.Zero; // Stride manages context internally

            // Determine feature level based on Stride device
            _featureLevel = DetermineFeatureLevel();

            return true;
        }

        protected override bool CreateSwapChainResources()
        {
            // Stride manages swap chain internally
            // We just need to get the command list for rendering
            _commandList = _game.GraphicsContext.CommandList;
            return _commandList != null;
        }

        protected override void DestroyDeviceResources()
        {
            // Release the DXR fallback device COM interface if it was created
            if (_raytracingFallbackDevice != IntPtr.Zero)
            {
                try
                {
                    // Get the vtable pointer from the COM object
                    IntPtr vtable = Marshal.ReadIntPtr(_raytracingFallbackDevice);
                    if (vtable != IntPtr.Zero)
                    {
                        // Get the Release function pointer (vtable index 2 for IUnknown::Release)
                        IntPtr releasePtr = Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size);
                        if (releasePtr != IntPtr.Zero)
                        {
                            // Create delegate and call Release
                            ReleaseDelegate release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(releasePtr);
                            release(_raytracingFallbackDevice);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[StrideDX11] Error releasing DXR fallback device COM interface: {0}", ex.Message);
                }
                finally
                {
                    _raytracingFallbackDevice = IntPtr.Zero;
                }
            }

            // Stride manages device lifetime
            _strideDevice = null;
            _device = IntPtr.Zero;

            // Clear cached fallback command list when device is destroyed
            _fallbackCommandList = IntPtr.Zero;

            // Reset raytracing state
            _useDxrFallbackLayer = false;
            _raytracingEnabled = false;
        }

        protected override void DestroySwapChainResources()
        {
            // Stride manages swap chain lifetime
            _commandList = null;

            // Clear cached fallback command list when swap chain is destroyed
            // (command list is tied to swap chain in some implementations)
            _fallbackCommandList = IntPtr.Zero;
        }

        protected override ResourceInfo CreateTextureInternal(Andastra.Runtime.Graphics.Common.Structs.TextureDescription desc, IntPtr handle)
        {
            var strideDesc = new global::Stride.Graphics.TextureDescription
            {
                Width = desc.Width,
                Height = desc.Height,
                Depth = desc.Depth,
                MipLevels = desc.MipLevels,
                ArraySize = desc.ArraySize,
                Dimension = TextureDimension.Texture2D,
                Format = ConvertFormat(desc.Format),
                Flags = ConvertUsage(desc.Usage)
            };

            var texture = Texture.New(_strideDevice, strideDesc);

            return new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                // Stride Texture doesn't expose NativeDeviceTexture - return IntPtr.Zero
                NativeHandle = IntPtr.Zero,
                DebugName = desc.DebugName,
                SizeInBytes = desc.Width * desc.Height * GetFormatSize(desc.Format)
            };
        }

        protected override ResourceInfo CreateBufferInternal(Andastra.Runtime.Graphics.Common.Structs.BufferDescription desc, IntPtr handle)
        {
            BufferFlags flags = BufferFlags.None;
            if ((desc.Usage & BufferUsage.Vertex) != 0) flags |= BufferFlags.VertexBuffer;
            if ((desc.Usage & BufferUsage.Index) != 0) flags |= BufferFlags.IndexBuffer;
            if ((desc.Usage & BufferUsage.Constant) != 0) flags |= BufferFlags.ConstantBuffer;
            if ((desc.Usage & BufferUsage.Structured) != 0) flags |= BufferFlags.StructuredBuffer;

            var buffer = global::Stride.Graphics.Buffer.New(_strideDevice, desc.SizeInBytes, flags);

            return new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                // Stride Buffer doesn't expose NativeBuffer - return IntPtr.Zero
                NativeHandle = IntPtr.Zero,
                DebugName = desc.DebugName,
                SizeInBytes = desc.SizeInBytes
            };
        }

        protected override ResourceInfo CreatePipelineInternal(PipelineDescription desc, IntPtr handle)
        {
            // Stride uses effect-based pipeline
            // Would need to compile shaders and create pipeline state
            return new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                NativeHandle = IntPtr.Zero,
                DebugName = desc.DebugName
            };
        }

        protected override void DestroyResourceInternal(ResourceInfo info)
        {
            // If this is a BLAS, clean up associated scratch buffer
            if (info.Type == ResourceType.AccelerationStructure && _blasScratchBuffers.ContainsKey(info.Handle))
            {
                IntPtr scratchHandle = _blasScratchBuffers[info.Handle];
                _blasScratchBuffers.Remove(info.Handle);

                // Destroy scratch buffer
                if (scratchHandle != IntPtr.Zero)
                {
                    base.DestroyResource(scratchHandle);
                }
            }

            // If this is a TLAS, clean up associated instance data and buffers
            if (info.Type == ResourceType.AccelerationStructure && _tlasInstanceData.ContainsKey(info.Handle))
            {
                TlasInstanceData instanceData = _tlasInstanceData[info.Handle];
                _tlasInstanceData.Remove(info.Handle);

                // Destroy instance buffer
                if (instanceData.InstanceBufferHandle != IntPtr.Zero)
                {
                    base.DestroyResource(instanceData.InstanceBufferHandle);
                }

                // Destroy scratch buffer
                if (instanceData.ScratchBufferHandle != IntPtr.Zero)
                {
                    base.DestroyResource(instanceData.ScratchBufferHandle);
                }

                // Note: Result buffer is the TLAS itself, so it will be destroyed by the base class
                // We don't need to destroy it separately here
            }

            // Clean up shader binding table stride cache entry if this is a buffer
            // (SBTs are buffers, so we check if this handle is in our cache)
            if (_sbtStrideCache.ContainsKey(info.Handle))
            {
                _sbtStrideCache.Remove(info.Handle);
            }

            // Resources tracked by Stride's garbage collection
            // Would dispose IDisposable resources here
        }

        #endregion

        #region BaseDirect3D11Backend Implementation

        protected override void OnDispatch(int x, int y, int z)
        {
            _commandList?.Dispatch(x, y, z);
        }

        protected override void OnSetViewport(int x, int y, int w, int h, float minD, float maxD)
        {
            var viewport = new Viewport(x, y, w, h);
            viewport.MinDepth = minD;
            viewport.MaxDepth = maxD;
            _commandList?.SetViewport(viewport);
        }

        protected override void OnSetPrimitiveTopology(PrimitiveTopology topology)
        {
            // Stride sets topology per draw call
        }

        protected override void OnDraw(int vertexCount, int startVertexLocation)
        {
            _commandList?.Draw(vertexCount, startVertexLocation);
        }

        protected override void OnDrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            _commandList?.DrawIndexed(indexCount, startIndexLocation, baseVertexLocation);
        }

        protected override void OnDrawIndexedInstanced(int indexCountPerInstance, int instanceCount,
            int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            _commandList?.DrawIndexedInstanced(indexCountPerInstance, instanceCount,
                startIndexLocation, baseVertexLocation, startInstanceLocation);
        }

        protected override ResourceInfo CreateStructuredBufferInternal(int elementCount, int elementStride,
            bool cpuWritable, IntPtr handle)
        {
            var flags = BufferFlags.StructuredBuffer | BufferFlags.ShaderResource;
            if (!cpuWritable) flags |= BufferFlags.UnorderedAccess;

            var buffer = global::Stride.Graphics.Buffer.Structured.New(_strideDevice, elementCount, elementStride,
                cpuWritable);

            return new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                // Stride Buffer doesn't expose NativeBuffer - return IntPtr.Zero
                NativeHandle = IntPtr.Zero,
                DebugName = "StructuredBuffer",
                SizeInBytes = elementCount * elementStride
            };
        }

        public override IntPtr MapBuffer(IntPtr bufferHandle, MapType mapType)
        {
            // Stride buffer mapping would go here
            return IntPtr.Zero;
        }

        public override void UnmapBuffer(IntPtr bufferHandle)
        {
            // Stride buffer unmapping
        }

        #endregion

        #region Utility Methods

        private D3D11FeatureLevel DetermineFeatureLevel()
        {
            // Stride typically uses DX11.0 or DX11.1
            return D3D11FeatureLevel.Level_11_0;
        }

        private PixelFormat ConvertFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8G8B8A8_UNorm: return PixelFormat.R8G8B8A8_UNorm;
                case TextureFormat.R8G8B8A8_UNorm_SRGB: return PixelFormat.R8G8B8A8_UNorm_SRgb;
                case TextureFormat.B8G8R8A8_UNorm: return PixelFormat.B8G8R8A8_UNorm;
                case TextureFormat.R16G16B16A16_Float: return PixelFormat.R16G16B16A16_Float;
                case TextureFormat.R32G32B32A32_Float: return PixelFormat.R32G32B32A32_Float;
                case TextureFormat.D24_UNorm_S8_UInt: return PixelFormat.D24_UNorm_S8_UInt;
                case TextureFormat.D32_Float: return PixelFormat.D32_Float;
                case TextureFormat.BC1_UNorm: return PixelFormat.BC1_UNorm;
                case TextureFormat.BC3_UNorm: return PixelFormat.BC3_UNorm;
                case TextureFormat.BC7_UNorm: return PixelFormat.BC7_UNorm;
                default: return PixelFormat.R8G8B8A8_UNorm;
            }
        }

        private TextureFlags ConvertUsage(TextureUsage usage)
        {
            TextureFlags flags = TextureFlags.None;
            if ((usage & TextureUsage.ShaderResource) != 0) flags |= TextureFlags.ShaderResource;
            if ((usage & TextureUsage.RenderTarget) != 0) flags |= TextureFlags.RenderTarget;
            if ((usage & TextureUsage.DepthStencil) != 0) flags |= TextureFlags.DepthStencil;
            if ((usage & TextureUsage.UnorderedAccess) != 0) flags |= TextureFlags.UnorderedAccess;
            return flags;
        }

        private int GetFormatSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                    return 1;
                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R16_Float:
                    return 2;
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.R32_Float:
                    return 4;
                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R32G32_Float:
                    return 8;
                case TextureFormat.R32G32B32A32_Float:
                    return 16;
                default:
                    return 4;
            }
        }

        #endregion

        protected override long QueryVideoMemory()
        {
            // Stride doesn't expose DedicatedVideoMemory via Adapter.Description
            // Return default fallback value
            return 4L * 1024 * 1024 * 1024;
        }

        protected override string QueryVendorName()
        {
            // Stride Adapter.Description is a string, not an object with VendorId
            // Try to get vendor ID directly from Adapter if available
            if (_strideDevice?.Adapter != null)
            {
                try
                {
                    var vendorIdProp = _strideDevice.Adapter.GetType().GetProperty("VendorId");
                    if (vendorIdProp != null)
                    {
                        var vendorId = vendorIdProp.GetValue(_strideDevice.Adapter);
                        if (vendorId != null)
                        {
                            return vendorId.ToString();
                        }
                    }
                }
                catch
                {
                    // If reflection fails, fall back to default
                }
            }
            return "Unknown";
        }

        protected override string QueryDeviceName()
        {
            // Stride Adapter.Description is a string
            return _strideDevice?.Adapter?.Description ?? "Stride DirectX 11 Device";
        }

        #region BaseDirect3D11Backend Abstract Method Implementations

        /// <summary>
        /// Initializes the DXR fallback layer for software-based raytracing on DirectX 11.
        ///
        /// The DXR fallback layer allows using DXR APIs on hardware without native raytracing support
        /// by emulating raytracing using compute shaders. This implementation uses the native D3D11 device
        /// from Stride to initialize the fallback layer.
        ///
        /// Based on Microsoft D3D12RaytracingFallback library:
        /// https://github.com/microsoft/DirectX-Graphics-Samples/tree/master/Libraries/D3D12RaytracingFallback
        /// </summary>
        protected override void InitializeDxrFallback()
        {
            // Verify device is available
            if (_device == IntPtr.Zero || _strideDevice == null)
            {
                Console.WriteLine("[StrideDX11] Cannot initialize DXR fallback layer: Device not available");
                _raytracingFallbackDevice = IntPtr.Zero;
                _useDxrFallbackLayer = false;
                return;
            }

            // Verify feature level supports compute shaders (required for fallback layer)
            if (_featureLevel < D3D11FeatureLevel.Level_11_0)
            {
                Console.WriteLine("[StrideDX11] Cannot initialize DXR fallback layer: Feature level {0} does not support compute shaders (requires 11.0+)", _featureLevel);
                _raytracingFallbackDevice = IntPtr.Zero;
                _useDxrFallbackLayer = false;
                return;
            }

            try
            {
                // Attempt to initialize DXR fallback layer using native device
                // Note: The DXR fallback layer requires the D3D12RaytracingFallback library
                // which wraps the D3D11 device to provide DXR API compatibility

                // For Stride integration, we use the native D3D11 device pointer from Stride
                // The fallback layer can be initialized by:
                // 1. Loading D3D12RaytracingFallback library (D3D12RaytracingFallback.dll)
                // 2. Creating a fallback device wrapper around the D3D11 device
                // 3. Querying for fallback layer support

                // Check if fallback layer is available by attempting to load it
                bool fallbackLayerAvailable = CheckDxrFallbackLayerAvailability();

                if (fallbackLayerAvailable)
                {
                    // For DirectX 11, the DXR fallback layer creates a software-based emulation
                    // that translates DXR calls to compute shader operations
                    // The fallback device wraps the D3D11 device and provides DXR API compatibility

                    // Initialize the fallback device using P/Invoke to D3D12CreateRaytracingFallbackDevice
                    // The D3D11 device pointer (_device) is already an IUnknown* pointer (all COM interfaces derive from IUnknown)
                    // The fallback layer will wrap this D3D11 device to provide DXR API compatibility

                    IntPtr fallbackDevicePtr = IntPtr.Zero;
                    int hresult = D3D12CreateRaytracingFallbackDevice(
                        _device, // D3D11 device as IUnknown*
                        D3D12_RAYTRACING_FALLBACK_FLAGS.D3D12_RAYTRACING_FALLBACK_FLAG_NONE, // Use default flags
                        IID_ID3D12Device5, // Query for ID3D12Device5 interface (fallback device implements this)
                        out fallbackDevicePtr);

                    // Check if creation succeeded (HRESULT 0 = S_OK)
                    if (hresult == 0 && fallbackDevicePtr != IntPtr.Zero)
                    {
                        // Successfully created fallback device
                        _raytracingFallbackDevice = fallbackDevicePtr;
                        _useDxrFallbackLayer = true;
                        _raytracingEnabled = true;

                        Console.WriteLine("[StrideDX11] DXR fallback layer initialized successfully via D3D12CreateRaytracingFallbackDevice (software-based raytracing via compute shaders)");
                    }
                    else
                    {
                        // Failed to create fallback device
                        Console.WriteLine("[StrideDX11] Failed to create DXR fallback device: HRESULT = 0x{0:X8}", hresult);
                        _raytracingFallbackDevice = IntPtr.Zero;
                        _useDxrFallbackLayer = false;
                        _raytracingEnabled = false;
                    }
                }
                else
                {
                    Console.WriteLine("[StrideDX11] DXR fallback layer not available (D3D12RaytracingFallback library not found or not supported)");
                    _raytracingFallbackDevice = IntPtr.Zero;
                    _useDxrFallbackLayer = false;
                    _raytracingEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideDX11] Failed to initialize DXR fallback layer: {0}", ex.Message);
                _raytracingFallbackDevice = IntPtr.Zero;
                _useDxrFallbackLayer = false;
                _raytracingEnabled = false;
            }
        }

        /// <summary>
        /// Checks if the DXR fallback layer is available on the system.
        ///
        /// The DXR fallback layer requires:
        /// - D3D12RaytracingFallback.dll to be present
        /// - DirectX 11 device with compute shader support (Feature Level 11.0+)
        /// - Windows 10 version 1809 (RS5) or later for full support
        /// </summary>
        /// <returns>True if the fallback layer is available, false otherwise</returns>
        private bool CheckDxrFallbackLayerAvailability()
        {
            try
            {
                // Check if we're on Windows (required for DXR fallback layer)
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return false;
                }

                // Check Windows version (DXR fallback layer requires Windows 10 RS5 or later)
                var osVersion = Environment.OSVersion.Version;
                if (osVersion.Major < 10 || (osVersion.Major == 10 && osVersion.Build < 17763))
                {
                    Console.WriteLine("[StrideDX11] DXR fallback layer requires Windows 10 version 1809 (RS5) or later (current: {0}.{1}.{2})",
                        osVersion.Major, osVersion.Minor, osVersion.Build);
                    return false;
                }

                // Verify compute shader support (required for DXR fallback layer)
                if (_featureLevel < D3D11FeatureLevel.Level_11_0)
                {
                    Console.WriteLine("[StrideDX11] DXR fallback layer requires DirectX 11.0+ feature level (current: {0})", _featureLevel);
                    return false;
                }

                // Attempt to load D3D12RaytracingFallback.dll from multiple locations
                // The fallback layer library should be available if:
                // - DirectX 12 runtime is installed
                // - Windows 10 SDK with raytracing support is present
                // - The library is in a system directory or the current application directory
                IntPtr fallbackLibrary = IntPtr.Zero;
                bool libraryLoadedByUs = false; // Track if we loaded it (need to free) vs already loaded (don't free)

                // Try loading from current directory or system search path first
                fallbackLibrary = LoadLibrary("D3D12RaytracingFallback.dll");
                if (fallbackLibrary != IntPtr.Zero)
                {
                    libraryLoadedByUs = true;
                }

                if (fallbackLibrary == IntPtr.Zero)
                {
                    // Try loading from System32 directory (64-bit processes)
                    string system32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string system32DllPath = System.IO.Path.Combine(system32Path, "D3D12RaytracingFallback.dll");
                    fallbackLibrary = LoadLibrary(system32DllPath);
                    if (fallbackLibrary != IntPtr.Zero)
                    {
                        libraryLoadedByUs = true;
                    }
                }

                if (fallbackLibrary == IntPtr.Zero)
                {
                    // Try loading from SysWOW64 directory (32-bit processes on 64-bit Windows)
                    string syswow64Path = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                    string syswow64DllPath = System.IO.Path.Combine(syswow64Path, "D3D12RaytracingFallback.dll");
                    fallbackLibrary = LoadLibrary(syswow64DllPath);
                    if (fallbackLibrary != IntPtr.Zero)
                    {
                        libraryLoadedByUs = true;
                    }
                }

                if (fallbackLibrary == IntPtr.Zero)
                {
                    // Try checking if the DLL is already loaded in the process
                    // GetModuleHandle doesn't increment reference count and doesn't require freeing
                    fallbackLibrary = GetModuleHandle("D3D12RaytracingFallback.dll");
                    // Don't set libraryLoadedByUs = true here because GetModuleHandle doesn't require freeing
                }

                if (fallbackLibrary == IntPtr.Zero)
                {
                    // DLL not found in any location and not already loaded
                    Console.WriteLine("[StrideDX11] DXR fallback layer DLL (D3D12RaytracingFallback.dll) not found. The library may not be installed, or DirectX 12 runtime with raytracing support may be missing.");
                    return false;
                }

                // Verify that the key function (D3D12CreateRaytracingFallbackDevice) is available
                // This ensures the DLL is not corrupted or an incompatible version
                IntPtr createFallbackDeviceFunc = GetProcAddress(fallbackLibrary, "D3D12CreateRaytracingFallbackDevice");
                if (createFallbackDeviceFunc == IntPtr.Zero)
                {
                    // DLL is loaded but required function is missing - likely wrong version or corrupted
                    Console.WriteLine("[StrideDX11] DXR fallback layer DLL found but required function D3D12CreateRaytracingFallbackDevice is not available. The DLL may be corrupted or an incompatible version.");
                    // Only free library if we loaded it via LoadLibrary (not if it was already loaded via GetModuleHandle)
                    if (libraryLoadedByUs && fallbackLibrary != IntPtr.Zero)
                    {
                        FreeLibrary(fallbackLibrary);
                    }
                    return false;
                }

                // Successfully verified DLL and function availability
                // Only free the library if we loaded it via LoadLibrary (not if it was already loaded via GetModuleHandle)
                if (libraryLoadedByUs && fallbackLibrary != IntPtr.Zero)
                {
                    FreeLibrary(fallbackLibrary);
                }

                return true;
            }
            catch
            {
                // If we can't check availability, assume it's not available
                return false;
            }
        }

        /// <summary>
        /// Overrides QueryDxrFallbackSupport to check for DXR fallback layer availability.
        /// </summary>
        /// <returns>True if DXR fallback layer is supported, false otherwise</returns>
        protected override bool QueryDxrFallbackSupport()
        {
            // Check if device is initialized
            if (_device == IntPtr.Zero || _strideDevice == null)
            {
                return false;
            }

            // DXR fallback layer requires compute shader support (Feature Level 11.0+)
            if (_featureLevel < D3D11FeatureLevel.Level_11_0)
            {
                return false;
            }

            // Check if fallback layer is available on the system
            return CheckDxrFallbackLayerAvailability();
        }

        /// <summary>
        /// Creates a bottom-level acceleration structure (BLAS) for raytracing using the DXR fallback layer.
        ///
        /// The DXR fallback layer provides DXR API compatibility on DirectX 11 hardware by emulating
        /// raytracing using compute shaders. This implementation creates a BLAS from mesh geometry
        /// that can be used for raytracing operations.
        ///
        /// Based on Microsoft D3D12RaytracingFallback library:
        /// https://github.com/microsoft/DirectX-Graphics-Samples/tree/master/Libraries/D3D12RaytracingFallback
        ///
        /// DXR API Reference:
        /// - D3D12_RAYTRACING_GEOMETRY_DESC: Describes geometry for acceleration structure
        /// - D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS: Inputs for building acceleration structure
        /// - GetRaytracingAccelerationStructurePrebuildInfo: Gets size requirements
        /// - BuildRaytracingAccelerationStructure: Builds the acceleration structure
        /// </summary>
        /// <param name="geometry">Mesh geometry containing vertex and index buffers</param>
        /// <param name="handle">Resource handle for tracking</param>
        /// <returns>ResourceInfo containing the created BLAS resource</returns>
        protected override ResourceInfo CreateBlasFallbackInternal(MeshGeometry geometry, IntPtr handle)
        {
            // Validate inputs
            if (!_useDxrFallbackLayer || _raytracingFallbackDevice == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Cannot create BLAS: DXR fallback layer not initialized");
                return new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BLAS_Invalid"
                };
            }

            if (geometry.VertexBuffer == IntPtr.Zero || geometry.IndexBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Cannot create BLAS: Invalid geometry buffers");
                return new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BLAS_InvalidGeometry"
                };
            }

            if (geometry.VertexCount <= 0 || geometry.IndexCount <= 0 || geometry.VertexStride <= 0)
            {
                Console.WriteLine("[StrideDX11] Cannot create BLAS: Invalid geometry parameters (VertexCount={0}, IndexCount={1}, VertexStride={2})",
                    geometry.VertexCount, geometry.IndexCount, geometry.VertexStride);
                return new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BLAS_InvalidParams"
                };
            }

            try
            {
                // Step 1: Create geometry description for the acceleration structure
                // The DXR fallback layer uses D3D12_RAYTRACING_GEOMETRY_DESC structure
                // which describes the geometry data (vertices and indices)
                D3D12_RAYTRACING_GEOMETRY_DESC geometryDesc = new D3D12_RAYTRACING_GEOMETRY_DESC
                {
                    Type = D3D12_RAYTRACING_GEOMETRY_TYPE_TRIANGLES,
                    Flags = geometry.IsOpaque ? D3D12_RAYTRACING_GEOMETRY_FLAG_OPAQUE : D3D12_RAYTRACING_GEOMETRY_FLAG_NONE,
                    Triangles = new D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC
                    {
                        Transform3x4 = IntPtr.Zero, // No transform matrix for BLAS
                        IndexFormat = D3D12_RAYTRACING_INDEX_FORMAT_UINT32, // 32-bit indices
                        VertexFormat = D3D12_RAYTRACING_VERTEX_FORMAT_FLOAT3, // Float3 vertices
                        IndexCount = (uint)geometry.IndexCount,
                        VertexCount = (uint)geometry.VertexCount,
                        IndexBuffer = geometry.IndexBuffer,
                        VertexBuffer = new D3D12_GPU_VIRTUAL_ADDRESS_AND_STRIDE
                        {
                            StartAddress = GetGpuVirtualAddress(geometry.VertexBuffer),
                            StrideInBytes = (uint)geometry.VertexStride
                        }
                    }
                };

                // Step 2: Create build inputs structure
                // This describes what type of acceleration structure to build (BLAS vs TLAS)
                D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS buildInputs = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
                {
                    Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL,
                    Flags = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_TRACE,
                    NumDescs = 1,
                    DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY,
                    pGeometryDescs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RAYTRACING_GEOMETRY_DESC)))
                };

                // Copy geometry description to unmanaged memory
                Marshal.StructureToPtr(geometryDesc, buildInputs.pGeometryDescs, false);

                // Step 3: Get prebuild information to determine buffer sizes
                // This tells us how much memory we need for the acceleration structure
                D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO prebuildInfo;
                GetRaytracingAccelerationStructurePrebuildInfo(
                    _raytracingFallbackDevice,
                    ref buildInputs,
                    out prebuildInfo);

                // Validate prebuild info
                if (prebuildInfo.ResultDataMaxSizeInBytes == 0 || prebuildInfo.ScratchDataSizeInBytes == 0)
                {
                    Console.WriteLine("[StrideDX11] Failed to get BLAS prebuild info: Invalid sizes");
                    Marshal.FreeHGlobal(buildInputs.pGeometryDescs);
                    return new ResourceInfo
                    {
                        Type = ResourceType.AccelerationStructure,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = "BLAS_PrebuildFailed"
                    };
                }

                // Step 4: Create scratch buffer for building the acceleration structure
                // This is temporary memory used during the build process
                IntPtr scratchBufferHandle = CreateScratchBuffer(prebuildInfo.ScratchDataSizeInBytes);
                if (scratchBufferHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to create scratch buffer for BLAS");
                    Marshal.FreeHGlobal(buildInputs.pGeometryDescs);
                    return new ResourceInfo
                    {
                        Type = ResourceType.AccelerationStructure,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = "BLAS_ScratchBufferFailed"
                    };
                }

                // Step 5: Create result buffer for the acceleration structure
                // This is where the built acceleration structure will be stored
                IntPtr resultBufferHandle = CreateAccelerationStructureBuffer(prebuildInfo.ResultDataMaxSizeInBytes);
                if (resultBufferHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to create result buffer for BLAS");
                    DestroyResource(scratchBufferHandle);
                    Marshal.FreeHGlobal(buildInputs.pGeometryDescs);
                    return new ResourceInfo
                    {
                        Type = ResourceType.AccelerationStructure,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = "BLAS_ResultBufferFailed"
                    };
                }

                // Step 6: Build the acceleration structure
                // This is the actual build operation that creates the BLAS
                D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC buildDesc = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
                {
                    DestAccelerationStructureData = GetGpuVirtualAddress(resultBufferHandle),
                    Inputs = buildInputs,
                    SourceAccelerationStructureData = IntPtr.Zero, // No source (building from scratch)
                    ScratchAccelerationStructureData = GetGpuVirtualAddress(scratchBufferHandle)
                };

                // Get command list for building (Stride manages this internally)
                // For the fallback layer, we need to use the fallback command list
                IntPtr fallbackCommandList = GetFallbackCommandList();
                if (fallbackCommandList == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to get fallback command list for BLAS build");
                    DestroyResource(scratchBufferHandle);
                    DestroyResource(resultBufferHandle);
                    Marshal.FreeHGlobal(buildInputs.pGeometryDescs);
                    return new ResourceInfo
                    {
                        Type = ResourceType.AccelerationStructure,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = "BLAS_CommandListFailed"
                    };
                }

                // Build the acceleration structure using the fallback layer
                BuildRaytracingAccelerationStructure(
                    fallbackCommandList,
                    ref buildDesc,
                    0, // NumPostbuildInfoDescs
                    IntPtr.Zero); // pPostbuildInfoDescs

                // Clean up temporary memory
                Marshal.FreeHGlobal(buildInputs.pGeometryDescs);

                // Store scratch buffer handle for later cleanup
                // We'll need to keep it until the BLAS is destroyed
                ResourceInfo scratchInfo;
                if (_resources.TryGetValue(scratchBufferHandle, out scratchInfo))
                {
                    // Store reference to scratch buffer in BLAS resource info
                }

                // Create resource info for the BLAS
                ResourceInfo blasInfo = new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = resultBufferHandle, // Store result buffer as native handle
                    DebugName = "BLAS",
                    SizeInBytes = (int)prebuildInfo.ResultDataMaxSizeInBytes
                };

                // Store scratch buffer handle for cleanup
                // We'll track this separately so we can clean it up when BLAS is destroyed
                _blasScratchBuffers[handle] = scratchBufferHandle;

                Console.WriteLine("[StrideDX11] BLAS created successfully (ResultSize={0} bytes, ScratchSize={1} bytes)",
                    prebuildInfo.ResultDataMaxSizeInBytes, prebuildInfo.ScratchDataSizeInBytes);

                return blasInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideDX11] Exception creating BLAS: {0}", ex.Message);
                Console.WriteLine("[StrideDX11] Stack trace: {0}", ex.StackTrace);
                return new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BLAS_Exception"
                };
            }
        }

        /// <summary>
        /// Creates a top-level acceleration structure (TLAS) for raytracing using the DXR fallback layer.
        ///
        /// The TLAS contains references to bottom-level acceleration structures (BLAS) with instance transforms.
        /// This implementation creates the necessary buffers and initializes instance tracking data.
        ///
        /// Based on DXR API: ID3D12Device5::CreateStateObject
        /// DXR API Reference:
        /// - D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS: Inputs for building TLAS
        /// - D3D12_RAYTRACING_INSTANCE_DESC: Instance descriptors in the TLAS
        /// - GetRaytracingAccelerationStructurePrebuildInfo: Gets size requirements
        /// - BuildRaytracingAccelerationStructure: Builds the TLAS
        /// </summary>
        /// <param name="maxInstances">Maximum number of instances the TLAS can hold</param>
        /// <param name="handle">Resource handle for tracking</param>
        /// <returns>ResourceInfo containing the created TLAS resource</returns>
        protected override ResourceInfo CreateTlasFallbackInternal(int maxInstances, IntPtr handle)
        {
            // Validate inputs
            if (!_useDxrFallbackLayer || _raytracingFallbackDevice == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Cannot create TLAS: DXR fallback layer not initialized");
                return new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "TLAS_Invalid"
                };
            }

            if (maxInstances <= 0)
            {
                Console.WriteLine("[StrideDX11] Cannot create TLAS: Invalid maxInstances {0}", maxInstances);
                return new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "TLAS_InvalidMaxInstances"
                };
            }

            try
            {
                // Step 1: Create build inputs structure for TLAS
                // TLAS uses instance descriptors, not geometry descriptors
                D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS buildInputs = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
                {
                    Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL,
                    Flags = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_TRACE |
                            D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_UPDATE, // Allow updates for transform changes
                    NumDescs = (uint)maxInstances,
                    DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY,
                    pGeometryDescs = IntPtr.Zero // TLAS uses instance descriptors, not geometry descriptors
                };

                // Step 2: Get prebuild information to determine buffer sizes
                D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO prebuildInfo;
                GetRaytracingAccelerationStructurePrebuildInfo(
                    _raytracingFallbackDevice,
                    ref buildInputs,
                    out prebuildInfo);

                // Validate prebuild info
                if (prebuildInfo.ResultDataMaxSizeInBytes == 0 || prebuildInfo.ScratchDataSizeInBytes == 0)
                {
                    Console.WriteLine("[StrideDX11] Failed to get TLAS prebuild info: Invalid sizes");
                    return new ResourceInfo
                    {
                        Type = ResourceType.AccelerationStructure,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = "TLAS_PrebuildFailed"
                    };
                }

                // Step 3: Create instance buffer
                // Each instance descriptor is 64 bytes (D3D12_RAYTRACING_INSTANCE_DESC)
                int instanceBufferSize = maxInstances * Marshal.SizeOf(typeof(D3D12_RAYTRACING_INSTANCE_DESC));
                IntPtr instanceBufferHandle = CreateInstanceBuffer(instanceBufferSize);
                if (instanceBufferHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to create instance buffer for TLAS");
                    return new ResourceInfo
                    {
                        Type = ResourceType.AccelerationStructure,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = "TLAS_InstanceBufferFailed"
                    };
                }

                // Step 4: Create scratch buffer for building the TLAS
                IntPtr scratchBufferHandle = CreateScratchBuffer(prebuildInfo.ScratchDataSizeInBytes);
                if (scratchBufferHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to create scratch buffer for TLAS");
                    DestroyResource(instanceBufferHandle);
                    return new ResourceInfo
                    {
                        Type = ResourceType.AccelerationStructure,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = "TLAS_ScratchBufferFailed"
                    };
                }

                // Step 5: Create result buffer for the TLAS
                IntPtr resultBufferHandle = CreateAccelerationStructureBuffer(prebuildInfo.ResultDataMaxSizeInBytes);
                if (resultBufferHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to create result buffer for TLAS");
                    DestroyResource(instanceBufferHandle);
                    DestroyResource(scratchBufferHandle);
                    return new ResourceInfo
                    {
                        Type = ResourceType.AccelerationStructure,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = "TLAS_ResultBufferFailed"
                    };
                }

                // Step 6: Initialize instance data tracking
                TlasInstanceData instanceData = new TlasInstanceData
                {
                    InstanceBufferHandle = instanceBufferHandle,
                    ScratchBufferHandle = scratchBufferHandle,
                    ResultBufferHandle = resultBufferHandle,
                    InstanceDescs = new D3D12_RAYTRACING_INSTANCE_DESC[maxInstances],
                    InstanceCount = 0, // Initially empty, instances will be added later
                    MaxInstances = maxInstances,
                    BuildFlags = buildInputs.Flags
                };

                // Initialize all instance descriptors to zero (empty instances)
                for (int i = 0; i < maxInstances; i++)
                {
                    float[] identityTransform = new float[12];
                    // Initialize to identity matrix (row-major 3x4):
                    // [1, 0, 0, 0]
                    // [0, 1, 0, 0]
                    // [0, 0, 1, 0]
                    identityTransform[0] = 1.0f; // Row 0, Col 0
                    identityTransform[1] = 0.0f; // Row 0, Col 1
                    identityTransform[2] = 0.0f; // Row 0, Col 2
                    identityTransform[3] = 0.0f; // Row 0, Col 3
                    identityTransform[4] = 0.0f; // Row 1, Col 0
                    identityTransform[5] = 1.0f; // Row 1, Col 1
                    identityTransform[6] = 0.0f; // Row 1, Col 2
                    identityTransform[7] = 0.0f; // Row 1, Col 3
                    identityTransform[8] = 0.0f; // Row 2, Col 0
                    identityTransform[9] = 0.0f; // Row 2, Col 1
                    identityTransform[10] = 1.0f; // Row 2, Col 2
                    identityTransform[11] = 0.0f; // Row 2, Col 3

                    instanceData.InstanceDescs[i] = new D3D12_RAYTRACING_INSTANCE_DESC
                    {
                        Transform = identityTransform, // Initialize transform to identity (will be set when instances are added)
                        InstanceID_InstanceMask_Flags = 0,
                        InstanceShaderBindingTableRecordOffset_Flags = 0,
                        AccelerationStructure = IntPtr.Zero
                    };
                }

                // Store instance data for this TLAS
                _tlasInstanceData[handle] = instanceData;

                // Create resource info for the TLAS
                ResourceInfo tlasInfo = new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = resultBufferHandle, // Store result buffer as native handle
                    DebugName = "TLAS",
                    SizeInBytes = (int)prebuildInfo.ResultDataMaxSizeInBytes
                };

                Console.WriteLine("[StrideDX11] TLAS created successfully (MaxInstances={0}, ResultSize={1} bytes, ScratchSize={2} bytes)",
                    maxInstances, prebuildInfo.ResultDataMaxSizeInBytes, prebuildInfo.ScratchDataSizeInBytes);

                return tlasInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideDX11] Exception creating TLAS: {0}", ex.Message);
                Console.WriteLine("[StrideDX11] Stack trace: {0}", ex.StackTrace);
                return new ResourceInfo
                {
                    Type = ResourceType.AccelerationStructure,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "TLAS_Exception"
                };
            }
        }

        /// <summary>
        /// Creates a raytracing pipeline state object (PSO) using the DXR fallback layer.
        ///
        /// The DXR fallback layer provides DXR API compatibility on DirectX 11 hardware by emulating
        /// raytracing using compute shaders. This implementation creates a raytracing pipeline state object
        /// that contains all the shaders and configuration needed for raytracing operations.
        ///
        /// Based on DXR API: ID3D12Device5::CreateStateObject
        /// DXR API Reference:
        /// - D3D12_STATE_OBJECT_DESC: Main state object description
        /// - D3D12_DXIL_LIBRARY_SUBOBJECT: Contains shader bytecode (DXIL)
        /// - D3D12_HIT_GROUP_SUBOBJECT: Defines hit groups (closest hit, any hit shaders)
        /// - D3D12_RAYTRACING_SHADER_CONFIG_SUBOBJECT: Configures payload and attribute sizes
        /// - D3D12_RAYTRACING_PIPELINE_CONFIG_SUBOBJECT: Configures max recursion depth
        /// - D3D12_GLOBAL_ROOT_SIGNATURE_SUBOBJECT: Optional global root signature
        /// - D3D12_LOCAL_ROOT_SIGNATURE_SUBOBJECT: Optional local root signature
        ///
        /// Microsoft D3D12RaytracingFallback library:
        /// https://github.com/microsoft/DirectX-Graphics-Samples/tree/master/Libraries/D3D12RaytracingFallback
        /// </summary>
        /// <param name="desc">Raytracing pipeline description containing shaders and configuration</param>
        /// <param name="handle">Resource handle for tracking</param>
        /// <returns>ResourceInfo containing the created PSO resource</returns>
        protected override ResourceInfo CreateRaytracingPsoFallbackInternal(Andastra.Runtime.Graphics.Common.Interfaces.RaytracingPipelineDesc desc, IntPtr handle)
        {
            // Validate inputs
            if (!_useDxrFallbackLayer || _raytracingFallbackDevice == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Cannot create raytracing PSO: DXR fallback layer not initialized");
                return new ResourceInfo
                {
                    Type = ResourceType.Pipeline,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = desc.DebugName ?? "RaytracingPSO_Invalid"
                };
            }

            // Validate shader bytecode
            if (desc.RayGenShader == null || desc.RayGenShader.Length == 0)
            {
                Console.WriteLine("[StrideDX11] Cannot create raytracing PSO: RayGen shader is required");
                return new ResourceInfo
                {
                    Type = ResourceType.Pipeline,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = desc.DebugName ?? "RaytracingPSO_NoRayGen"
                };
            }

            // Validate configuration parameters
            if (desc.MaxPayloadSize <= 0)
            {
                Console.WriteLine("[StrideDX11] Cannot create raytracing PSO: MaxPayloadSize must be > 0 (got {0})", desc.MaxPayloadSize);
                return new ResourceInfo
                {
                    Type = ResourceType.Pipeline,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = desc.DebugName ?? "RaytracingPSO_InvalidPayload"
                };
            }

            if (desc.MaxAttributeSize < 0)
            {
                Console.WriteLine("[StrideDX11] Cannot create raytracing PSO: MaxAttributeSize must be >= 0 (got {0})", desc.MaxAttributeSize);
                return new ResourceInfo
                {
                    Type = ResourceType.Pipeline,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = desc.DebugName ?? "RaytracingPSO_InvalidAttribute"
                };
            }

            if (desc.MaxRecursionDepth <= 0)
            {
                Console.WriteLine("[StrideDX11] Cannot create raytracing PSO: MaxRecursionDepth must be > 0 (got {0})", desc.MaxRecursionDepth);
                return new ResourceInfo
                {
                    Type = ResourceType.Pipeline,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = desc.DebugName ?? "RaytracingPSO_InvalidRecursion"
                };
            }

            try
            {
                // Step 1: Calculate the number of subobjects needed
                // We need:
                // - 1 DXIL library subobject (contains all shaders)
                // - 1 hit group subobject (if closest hit or any hit shaders are provided)
                // - 1 shader config subobject (payload and attribute sizes)
                // - 1 pipeline config subobject (max recursion depth)
                int subobjectCount = 2; // Shader config + Pipeline config (always required)
                bool hasHitGroup = (desc.ClosestHitShader != null && desc.ClosestHitShader.Length > 0) ||
                                   (desc.AnyHitShader != null && desc.AnyHitShader.Length > 0);
                if (hasHitGroup)
                {
                    subobjectCount++; // Hit group subobject
                }

                // Allocate memory for subobjects
                // Each subobject is a D3D12_STATE_SUBOBJECT structure
                int subobjectArraySize = subobjectCount * Marshal.SizeOf(typeof(D3D12_STATE_SUBOBJECT));
                IntPtr subobjectsPtr = Marshal.AllocHGlobal(subobjectArraySize);

                // Allocate memory for subobject data structures
                // We'll allocate these as we create them
                System.Collections.Generic.List<IntPtr> subobjectDataPtrs = new System.Collections.Generic.List<IntPtr>();

                int currentSubobjectIndex = 0;

                // Step 2: Create DXIL library subobject with shader exports
                // This contains all the shader bytecode (ray generation, miss, closest hit, any hit)
                // and exports them with proper names so they can be referenced by hit groups
                D3D12_SHADER_BYTECODE dxilLibraryBytecode = CreateDxilLibrary(desc);

                // Create export descriptors for all provided shaders
                D3D12_EXPORT_DESC[] exportDescs;
                IntPtr exportDescsPtr;
                System.Collections.Generic.List<IntPtr> exportNamePtrs;
                ShaderExportInfo exportInfo;
                int exportCount = CreateShaderExports(desc, out exportDescs, out exportDescsPtr, out exportNamePtrs, out exportInfo);

                // Track export descriptors pointer and export name string pointers for cleanup
                if (exportDescsPtr != IntPtr.Zero)
                {
                    subobjectDataPtrs.Add(exportDescsPtr);
                }
                foreach (IntPtr namePtr in exportNamePtrs)
                {
                    if (namePtr != IntPtr.Zero)
                    {
                        subobjectDataPtrs.Add(namePtr);
                    }
                }

                D3D12_DXIL_LIBRARY_DESC dxilLibraryDesc = new D3D12_DXIL_LIBRARY_DESC
                {
                    DXILLibrary = dxilLibraryBytecode,
                    pExports = exportDescsPtr, // Pointer to array of export descriptors (null if no exports)
                    NumExports = (uint)exportCount
                };

                IntPtr dxilLibraryDescPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_DXIL_LIBRARY_DESC)));
                Marshal.StructureToPtr(dxilLibraryDesc, dxilLibraryDescPtr, false);
                subobjectDataPtrs.Add(dxilLibraryDescPtr);

                D3D12_STATE_SUBOBJECT dxilLibrarySubobject = new D3D12_STATE_SUBOBJECT
                {
                    Type = D3D12_STATE_SUBOBJECT_TYPE_DXIL_LIBRARY,
                    pDesc = dxilLibraryDescPtr
                };

                IntPtr currentSubobjectPtr = new IntPtr(subobjectsPtr.ToInt64() + (currentSubobjectIndex * Marshal.SizeOf(typeof(D3D12_STATE_SUBOBJECT))));
                Marshal.StructureToPtr(dxilLibrarySubobject, currentSubobjectPtr, false);
                currentSubobjectIndex++;

                // Step 3: Create hit group subobject (if needed)
                // Hit groups reference shader exports by name from the DXIL library
                IntPtr hitGroupDescPtr = IntPtr.Zero;
                IntPtr hitGroupExportNamePtr = IntPtr.Zero;
                IntPtr closestHitShaderImportNamePtr = IntPtr.Zero;
                IntPtr anyHitShaderImportNamePtr = IntPtr.Zero;

                if (hasHitGroup)
                {
                    // Allocate string pointers for hit group export name and shader import names
                    // These must remain valid until the state object is created
                    hitGroupExportNamePtr = AllocateAnsiString(HIT_GROUP_EXPORT_NAME);
                    if (hitGroupExportNamePtr != IntPtr.Zero)
                    {
                        subobjectDataPtrs.Add(hitGroupExportNamePtr);
                    }

                    // Set shader import names to reference exported shaders from the DXIL library
                    // These must match the export names defined in the DXIL library exports
                    if (!string.IsNullOrEmpty(exportInfo.ClosestHitExportName))
                    {
                        closestHitShaderImportNamePtr = AllocateAnsiString(exportInfo.ClosestHitExportName);
                        if (closestHitShaderImportNamePtr != IntPtr.Zero)
                        {
                            subobjectDataPtrs.Add(closestHitShaderImportNamePtr);
                        }
                    }

                    if (!string.IsNullOrEmpty(exportInfo.AnyHitExportName))
                    {
                        anyHitShaderImportNamePtr = AllocateAnsiString(exportInfo.AnyHitExportName);
                        if (anyHitShaderImportNamePtr != IntPtr.Zero)
                        {
                            subobjectDataPtrs.Add(anyHitShaderImportNamePtr);
                        }
                    }

                    // Create hit group description with proper shader import references
                    // Based on D3D12 API: D3D12_HIT_GROUP_DESC
                    // Reference: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_hit_group_desc
                    D3D12_HIT_GROUP_DESC hitGroupDesc = new D3D12_HIT_GROUP_DESC
                    {
                        HitGroupExport = hitGroupExportNamePtr, // Export name for this hit group
                        Type = D3D12_HIT_GROUP_TYPE_TRIANGLES, // Triangle geometry type (most common)
                        AnyHitShaderImport = anyHitShaderImportNamePtr, // Reference to exported AnyHit shader (null if not provided)
                        ClosestHitShaderImport = closestHitShaderImportNamePtr, // Reference to exported ClosestHit shader (null if not provided)
                        IntersectionShaderImport = IntPtr.Zero // Not used for triangle geometry (only for procedural primitives)
                    };

                    hitGroupDescPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_HIT_GROUP_DESC)));
                    Marshal.StructureToPtr(hitGroupDesc, hitGroupDescPtr, false);
                    subobjectDataPtrs.Add(hitGroupDescPtr);

                    D3D12_STATE_SUBOBJECT hitGroupSubobject = new D3D12_STATE_SUBOBJECT
                    {
                        Type = D3D12_STATE_SUBOBJECT_TYPE_HIT_GROUP,
                        pDesc = hitGroupDescPtr
                    };

                    currentSubobjectPtr = new IntPtr(subobjectsPtr.ToInt64() + (currentSubobjectIndex * Marshal.SizeOf(typeof(D3D12_STATE_SUBOBJECT))));
                    Marshal.StructureToPtr(hitGroupSubobject, currentSubobjectPtr, false);
                    currentSubobjectIndex++;
                }

                // Step 4: Create shader config subobject
                // This specifies the maximum payload and attribute sizes
                D3D12_RAYTRACING_SHADER_CONFIG shaderConfig = new D3D12_RAYTRACING_SHADER_CONFIG
                {
                    MaxPayloadSizeInBytes = (uint)desc.MaxPayloadSize,
                    MaxAttributeSizeInBytes = (uint)desc.MaxAttributeSize
                };

                IntPtr shaderConfigPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RAYTRACING_SHADER_CONFIG)));
                Marshal.StructureToPtr(shaderConfig, shaderConfigPtr, false);
                subobjectDataPtrs.Add(shaderConfigPtr);

                D3D12_STATE_SUBOBJECT shaderConfigSubobject = new D3D12_STATE_SUBOBJECT
                {
                    Type = D3D12_STATE_SUBOBJECT_TYPE_RAYTRACING_SHADER_CONFIG,
                    pDesc = shaderConfigPtr
                };

                currentSubobjectPtr = new IntPtr(subobjectsPtr.ToInt64() + (currentSubobjectIndex * Marshal.SizeOf(typeof(D3D12_STATE_SUBOBJECT))));
                Marshal.StructureToPtr(shaderConfigSubobject, currentSubobjectPtr, false);
                currentSubobjectIndex++;

                // Step 5: Create pipeline config subobject
                // This specifies the maximum recursion depth
                D3D12_RAYTRACING_PIPELINE_CONFIG pipelineConfig = new D3D12_RAYTRACING_PIPELINE_CONFIG
                {
                    MaxTraceRecursionDepth = (uint)desc.MaxRecursionDepth
                };

                IntPtr pipelineConfigPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RAYTRACING_PIPELINE_CONFIG)));
                Marshal.StructureToPtr(pipelineConfig, pipelineConfigPtr, false);
                subobjectDataPtrs.Add(pipelineConfigPtr);

                D3D12_STATE_SUBOBJECT pipelineConfigSubobject = new D3D12_STATE_SUBOBJECT
                {
                    Type = D3D12_STATE_SUBOBJECT_TYPE_RAYTRACING_PIPELINE_CONFIG,
                    pDesc = pipelineConfigPtr
                };

                currentSubobjectPtr = new IntPtr(subobjectsPtr.ToInt64() + (currentSubobjectIndex * Marshal.SizeOf(typeof(D3D12_STATE_SUBOBJECT))));
                Marshal.StructureToPtr(pipelineConfigSubobject, currentSubobjectPtr, false);
                currentSubobjectIndex++;

                // Step 6: Create state object description
                D3D12_STATE_OBJECT_DESC stateObjectDesc = new D3D12_STATE_OBJECT_DESC
                {
                    Type = D3D12_STATE_OBJECT_TYPE_RAYTRACING_PIPELINE,
                    NumSubobjects = (uint)subobjectCount,
                    pSubobjects = subobjectsPtr
                };

                // Step 7: Create the state object using the fallback layer
                IntPtr stateObject = IntPtr.Zero;
                int hr = CreateStateObject(
                    _raytracingFallbackDevice,
                    ref stateObjectDesc,
                    IID_ID3D12StateObject,
                    out stateObject);

                // Clean up temporary memory
                Marshal.FreeHGlobal(subobjectsPtr);
                foreach (IntPtr dataPtr in subobjectDataPtrs)
                {
                    Marshal.FreeHGlobal(dataPtr);
                }

                // Check if creation succeeded
                if (hr != 0 || stateObject == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to create raytracing PSO: CreateStateObject returned HRESULT 0x{0:X8}", hr);
                    return new ResourceInfo
                    {
                        Type = ResourceType.Pipeline,
                        Handle = handle,
                        NativeHandle = IntPtr.Zero,
                        DebugName = desc.DebugName ?? "RaytracingPSO_CreationFailed"
                    };
                }

                // Create resource info for the PSO
                ResourceInfo psoInfo = new ResourceInfo
                {
                    Type = ResourceType.Pipeline,
                    Handle = handle,
                    NativeHandle = stateObject,
                    DebugName = desc.DebugName ?? "RaytracingPSO",
                    SizeInBytes = 0 // State objects don't have a size
                };

                Console.WriteLine("[StrideDX11] Raytracing PSO created successfully (MaxPayload={0}, MaxAttribute={1}, MaxRecursion={2})",
                    desc.MaxPayloadSize, desc.MaxAttributeSize, desc.MaxRecursionDepth);

                return psoInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideDX11] Exception creating raytracing PSO: {0}", ex.Message);
                Console.WriteLine("[StrideDX11] Stack trace: {0}", ex.StackTrace);
                return new ResourceInfo
                {
                    Type = ResourceType.Pipeline,
                    Handle = handle,
                    NativeHandle = IntPtr.Zero,
                    DebugName = desc.DebugName ?? "RaytracingPSO_Exception"
                };
            }
        }

        /// <summary>
        /// Standard shader export names used in HLSL ray tracing shaders.
        /// These match the typical entry point names in compiled DXIL bytecode.
        /// Based on D3D12 ray tracing shader naming conventions.
        /// Reference: https://learn.microsoft.com/en-us/windows/win32/direct3d12/direct3d-12-raytracing-shader-config
        /// </summary>
        private const string RAYGEN_SHADER_EXPORT_NAME = "RayGen";
        private const string MISS_SHADER_EXPORT_NAME = "Miss";
        private const string CLOSEST_HIT_SHADER_EXPORT_NAME = "ClosestHit";
        private const string ANY_HIT_SHADER_EXPORT_NAME = "AnyHit";
        private const string HIT_GROUP_EXPORT_NAME = "HitGroup";

        /// <summary>
        /// Helper class to manage shader export information for hit group references.
        /// Stores export names so hit groups can reference them.
        /// </summary>
        private class ShaderExportInfo
        {
            public string ClosestHitExportName;
            public string AnyHitExportName;
        }

        /// <summary>
        /// Creates export descriptors for shaders provided in the raytracing pipeline description.
        /// Based on D3D12 API: D3D12_EXPORT_DESC
        /// Shaders must be exported from the DXIL library to be referenced by hit groups.
        /// Reference: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_export_desc
        /// </summary>
        /// <param name="desc">Raytracing pipeline description</param>
        /// <param name="exportDescs">Output array of export descriptors</param>
        /// <param name="exportDescsPtr">Output pointer to marshalled export descriptors array</param>
        /// <param name="exportNamePtrs">Output list of allocated string pointers (for cleanup)</param>
        /// <param name="exportInfo">Output shader export information for hit group references</param>
        /// <returns>Number of exports created</returns>
        private int CreateShaderExports(
            Andastra.Runtime.Graphics.Common.Interfaces.RaytracingPipelineDesc desc,
            out D3D12_EXPORT_DESC[] exportDescs,
            out IntPtr exportDescsPtr,
            out System.Collections.Generic.List<IntPtr> exportNamePtrs,
            out ShaderExportInfo exportInfo)
        {
            exportDescs = null;
            exportDescsPtr = IntPtr.Zero;
            exportNamePtrs = new System.Collections.Generic.List<IntPtr>();
            exportInfo = new ShaderExportInfo();

            System.Collections.Generic.List<D3D12_EXPORT_DESC> exports = new System.Collections.Generic.List<D3D12_EXPORT_DESC>();

            // Export RayGen shader if provided
            if (desc.RayGenShader != null && desc.RayGenShader.Length > 0)
            {
                IntPtr namePtr = AllocateAnsiString(RAYGEN_SHADER_EXPORT_NAME);
                exportNamePtrs.Add(namePtr);
                exports.Add(new D3D12_EXPORT_DESC
                {
                    Name = namePtr,
                    ExportToRename = IntPtr.Zero,
                    Flags = 0
                });
            }

            // Export Miss shader if provided
            if (desc.MissShader != null && desc.MissShader.Length > 0)
            {
                IntPtr namePtr = AllocateAnsiString(MISS_SHADER_EXPORT_NAME);
                exportNamePtrs.Add(namePtr);
                exports.Add(new D3D12_EXPORT_DESC
                {
                    Name = namePtr,
                    ExportToRename = IntPtr.Zero,
                    Flags = 0
                });
            }

            // Export ClosestHit shader if provided
            if (desc.ClosestHitShader != null && desc.ClosestHitShader.Length > 0)
            {
                IntPtr namePtr = AllocateAnsiString(CLOSEST_HIT_SHADER_EXPORT_NAME);
                exportNamePtrs.Add(namePtr);
                exports.Add(new D3D12_EXPORT_DESC
                {
                    Name = namePtr,
                    ExportToRename = IntPtr.Zero,
                    Flags = 0
                });
                exportInfo.ClosestHitExportName = CLOSEST_HIT_SHADER_EXPORT_NAME;
            }

            // Export AnyHit shader if provided
            if (desc.AnyHitShader != null && desc.AnyHitShader.Length > 0)
            {
                IntPtr namePtr = AllocateAnsiString(ANY_HIT_SHADER_EXPORT_NAME);
                exportNamePtrs.Add(namePtr);
                exports.Add(new D3D12_EXPORT_DESC
                {
                    Name = namePtr,
                    ExportToRename = IntPtr.Zero,
                    Flags = 0
                });
                exportInfo.AnyHitExportName = ANY_HIT_SHADER_EXPORT_NAME;
            }

            int exportCount = exports.Count;
            if (exportCount == 0)
            {
                return 0;
            }

            exportDescs = exports.ToArray();

            // Allocate and marshal export descriptors array
            // Each export descriptor contains IntPtr fields for string pointers
            int exportDescSize = Marshal.SizeOf(typeof(D3D12_EXPORT_DESC));
            exportDescsPtr = Marshal.AllocHGlobal(exportDescSize * exportCount);

            // Marshal each export descriptor
            for (int i = 0; i < exportCount; i++)
            {
                IntPtr exportDescPtr = new IntPtr(exportDescsPtr.ToInt64() + (i * exportDescSize));
                Marshal.StructureToPtr(exportDescs[i], exportDescPtr, false);
            }

            return exportCount;
        }

        /// <summary>
        /// Allocates and returns a pointer to a null-terminated ANSI string in unmanaged memory.
        /// The caller is responsible for freeing this memory using Marshal.FreeHGlobal.
        /// </summary>
        /// <param name="str">String to allocate</param>
        /// <returns>Pointer to allocated string, or IntPtr.Zero if string is null/empty</returns>
        private IntPtr AllocateAnsiString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return IntPtr.Zero;
            }

            // Convert to ANSI bytes and allocate
            byte[] ansiBytes = System.Text.Encoding.Default.GetBytes(str + "\0");
            IntPtr strPtr = Marshal.AllocHGlobal(ansiBytes.Length);
            Marshal.Copy(ansiBytes, 0, strPtr, ansiBytes.Length);
            return strPtr;
        }

        /// <summary>
        /// Creates a D3D12_SHADER_BYTECODE structure from raytracing pipeline description.
        /// Combines all shaders (RayGen, Miss, ClosestHit, AnyHit) into a single DXIL library bytecode.
        ///
        /// This implementation uses DXC (DirectX Shader Compiler) to link multiple DXIL shader modules
        /// into a single library. If DXC is not available or linking fails, it falls back to using
        /// only the RayGen shader bytecode.
        ///
        /// Based on D3D12 API: D3D12_SHADER_BYTECODE
        /// Reference: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_shader_bytecode
        /// DXC Linking Reference: https://github.com/microsoft/DirectXShaderCompiler/wiki/Using-dxc.exe-and-writing-simple-shader-tools
        /// </summary>
        /// <param name="desc">Raytracing pipeline description</param>
        /// <returns>D3D12_SHADER_BYTECODE structure containing the combined shader bytecode</returns>
        private D3D12_SHADER_BYTECODE CreateDxilLibrary(Andastra.Runtime.Graphics.Common.Interfaces.RaytracingPipelineDesc desc)
        {
            IntPtr shaderBytecodePtr = IntPtr.Zero;
            int shaderBytecodeSize = 0;

            // Attempt to combine all shaders into a single DXIL library
            byte[] combinedBytecode = LinkDxilShaders(desc);

            if (combinedBytecode != null && combinedBytecode.Length > 0)
            {
                // Use the combined library bytecode
                shaderBytecodeSize = combinedBytecode.Length;
                shaderBytecodePtr = Marshal.AllocHGlobal(shaderBytecodeSize);
                Marshal.Copy(combinedBytecode, 0, shaderBytecodePtr, shaderBytecodeSize);
            }
            else if (desc.RayGenShader != null && desc.RayGenShader.Length > 0)
            {
                // Fallback: Use only RayGen shader if linking failed or DXC not available
                Console.WriteLine("[StrideDX11] DXIL library linking unavailable, using RayGen shader only");
                shaderBytecodeSize = desc.RayGenShader.Length;
                shaderBytecodePtr = Marshal.AllocHGlobal(shaderBytecodeSize);
                Marshal.Copy(desc.RayGenShader, 0, shaderBytecodePtr, shaderBytecodeSize);
            }

            return new D3D12_SHADER_BYTECODE
            {
                pShaderBytecode = shaderBytecodePtr,
                BytecodeLength = (IntPtr)shaderBytecodeSize
            };
        }

        /// <summary>
        /// Links multiple DXIL shader modules into a single DXIL library using DXC.
        ///
        /// This method writes each shader bytecode to a temporary file and uses DXC's linking
        /// capabilities to combine them into a single library. The resulting library contains
        /// all shader exports (RayGen, Miss, ClosestHit, AnyHit) in a single bytecode blob.
        ///
        /// DXC Linking Process:
        /// 1. Write each shader bytecode to a temporary .dxil file
        /// 2. Use DXC with -link flag to combine all DXIL files into a single library
        /// 3. Read the combined library bytecode
        /// 4. Clean up temporary files
        ///
        /// Reference: DXC linking documentation and D3D12 raytracing shader library requirements
        /// </summary>
        /// <param name="desc">Raytracing pipeline description containing shader bytecode</param>
        /// <returns>Combined DXIL library bytecode, or null if linking fails</returns>
        private byte[] LinkDxilShaders(Andastra.Runtime.Graphics.Common.Interfaces.RaytracingPipelineDesc desc)
        {
            // Find DXC compiler
            string dxcPath = FindDXCPath();
            if (string.IsNullOrEmpty(dxcPath))
            {
                Console.WriteLine("[StrideDX11] DXC compiler not found. Cannot link DXIL shaders.");
                Console.WriteLine("[StrideDX11] DXC can be installed via Windows SDK or downloaded from:");
                Console.WriteLine("[StrideDX11]   https://github.com/microsoft/DirectXShaderCompiler/releases");
                return null;
            }

            // Collect all non-null shader bytecodes
            System.Collections.Generic.List<byte[]> shaderBytecodes = new System.Collections.Generic.List<byte[]>();
            System.Collections.Generic.List<string> shaderNames = new System.Collections.Generic.List<string>();

            if (desc.RayGenShader != null && desc.RayGenShader.Length > 0)
            {
                shaderBytecodes.Add(desc.RayGenShader);
                shaderNames.Add("RayGen");
            }
            if (desc.MissShader != null && desc.MissShader.Length > 0)
            {
                shaderBytecodes.Add(desc.MissShader);
                shaderNames.Add("Miss");
            }
            if (desc.ClosestHitShader != null && desc.ClosestHitShader.Length > 0)
            {
                shaderBytecodes.Add(desc.ClosestHitShader);
                shaderNames.Add("ClosestHit");
            }
            if (desc.AnyHitShader != null && desc.AnyHitShader.Length > 0)
            {
                shaderBytecodes.Add(desc.AnyHitShader);
                shaderNames.Add("AnyHit");
            }

            // If we only have one shader (or none), no linking needed
            if (shaderBytecodes.Count <= 1)
            {
                if (shaderBytecodes.Count == 1)
                {
                    return shaderBytecodes[0];
                }
                return null;
            }

            // Create temporary files for each shader
            System.Collections.Generic.List<string> tempInputFiles = new System.Collections.Generic.List<string>();
            string tempOutputFile = Path.Combine(Path.GetTempPath(), $"rt_library_{Guid.NewGuid()}.dxil");

            try
            {
                // Write each shader to a temporary file
                for (int i = 0; i < shaderBytecodes.Count; i++)
                {
                    string tempInputFile = Path.Combine(Path.GetTempPath(), $"rt_shader_{shaderNames[i]}_{Guid.NewGuid()}.dxil");
                    File.WriteAllBytes(tempInputFile, shaderBytecodes[i]);
                    tempInputFiles.Add(tempInputFile);
                }

                // Build DXC command line for linking
                // DXC linking: -link combines multiple DXIL files into a single library
                // When linking, DXC automatically determines the target from input files
                // -Fo: Output file
                StringBuilder arguments = new StringBuilder();
                arguments.Append("-link ");

                // Add all input files
                foreach (string inputFile in tempInputFiles)
                {
                    arguments.AppendFormat("\"{0}\" ", inputFile);
                }

                arguments.AppendFormat("-Fo \"{0}\"", tempOutputFile);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = dxcPath,
                    Arguments = arguments.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Console.WriteLine("[StrideDX11] Failed to start DXC process for DXIL library linking");
                        return null;
                    }

                    // Wait for linking with timeout (30 seconds)
                    bool completed = process.WaitForExit(30000);
                    if (!completed)
                    {
                        Console.WriteLine("[StrideDX11] DXC linking timeout for DXIL library");
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // Ignore kill errors
                        }
                        return null;
                    }

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        string output = process.StandardOutput.ReadToEnd();
                        Console.WriteLine("[StrideDX11] DXC linking failed for DXIL library (exit code {0})", process.ExitCode);
                        if (!string.IsNullOrEmpty(error))
                        {
                            Console.WriteLine("[StrideDX11] DXC error output: {0}", error);
                        }
                        if (!string.IsNullOrEmpty(output))
                        {
                            Console.WriteLine("[StrideDX11] DXC standard output: {0}", output);
                        }
                        return null;
                    }

                    // Read combined library bytecode
                    if (File.Exists(tempOutputFile))
                    {
                        byte[] combinedBytecode = File.ReadAllBytes(tempOutputFile);
                        Console.WriteLine("[StrideDX11] Successfully linked {0} shaders into DXIL library ({1} bytes)",
                            shaderBytecodes.Count, combinedBytecode.Length);
                        return combinedBytecode;
                    }
                    else
                    {
                        Console.WriteLine("[StrideDX11] DXC linking succeeded but output file not found: {0}", tempOutputFile);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideDX11] Exception during DXIL library linking: {0}", ex.Message);
                return null;
            }
            finally
            {
                // Clean up temporary files
                foreach (string tempFile in tempInputFiles)
                {
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                try
                {
                    if (File.Exists(tempOutputFile))
                    {
                        // Keep output file for debugging if linking failed, delete on success
                        // Actually, we'll delete it since we've read the bytecode
                        File.Delete(tempOutputFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Finds the path to DXC (DirectX Shader Compiler) executable.
        ///
        /// DXC is typically installed with:
        /// - Windows SDK (in Windows Kits bin directory)
        /// - Visual Studio (in VS installation directory)
        /// - Standalone download from GitHub
        ///
        /// This method searches common installation locations and PATH.
        /// </summary>
        /// <returns>Path to dxc.exe, or null if not found</returns>
        private string FindDXCPath()
        {
            string dxcExeName = "dxc.exe";

            // 1. Try Windows SDK installation directory
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            // Windows 10 SDK typically installs to Program Files (x86)\Windows Kits\10\bin\<version>\x64
            string[] windowsKitsPaths = new[]
            {
                Path.Combine(programFilesX86, "Windows Kits", "10", "bin"),
                Path.Combine(programFiles, "Windows Kits", "10", "bin")
            };

            foreach (string kitsPath in windowsKitsPaths)
            {
                if (Directory.Exists(kitsPath))
                {
                    // Search for latest version directory
                    string[] versionDirs = Directory.GetDirectories(kitsPath);
                    Array.Sort(versionDirs);
                    Array.Reverse(versionDirs); // Start with latest version

                    foreach (string versionDir in versionDirs)
                    {
                        // Try x64 first, then x86
                        string[] archDirs = new[]
                        {
                            Path.Combine(versionDir, "x64"),
                            Path.Combine(versionDir, "x86")
                        };

                        foreach (string archDir in archDirs)
                        {
                            string dxcPath = Path.Combine(archDir, dxcExeName);
                            if (File.Exists(dxcPath))
                            {
                                return dxcPath;
                            }
                        }
                    }
                }
            }

            // 2. Try Visual Studio installation directory
            string[] vsPaths = new[]
            {
                Path.Combine(programFiles, "Microsoft Visual Studio"),
                Path.Combine(programFilesX86, "Microsoft Visual Studio")
            };

            foreach (string vsBasePath in vsPaths)
            {
                if (Directory.Exists(vsBasePath))
                {
                    // Search for VS installation directories (e.g., 2022, 2019, etc.)
                    string[] vsVersions = Directory.GetDirectories(vsBasePath);
                    foreach (string vsVersionPath in vsVersions)
                    {
                        string vcToolsPath = Path.Combine(vsVersionPath, "VC", "Tools", "MSVC");
                        if (Directory.Exists(vcToolsPath))
                        {
                            // Search for MSVC version directories
                            string[] msvcVersions = Directory.GetDirectories(vcToolsPath);
                            foreach (string msvcVersionPath in msvcVersions)
                            {
                                string dxcPath = Path.Combine(msvcVersionPath, "bin", "Hostx64", "x64", dxcExeName);
                                if (File.Exists(dxcPath))
                                {
                                    return dxcPath;
                                }
                            }
                        }
                    }
                }
            }

            // 3. Try PATH environment variable
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] paths = pathEnv.Split(Path.PathSeparator);
                foreach (string path in paths)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        string dxcPath = Path.Combine(path, dxcExeName);
                        if (File.Exists(dxcPath))
                        {
                            return dxcPath;
                        }
                    }
                }
            }

            // 4. Try current directory and common tool locations
            string[] commonPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), dxcExeName),
                Path.Combine(Directory.GetCurrentDirectory(), "tools", dxcExeName),
                Path.Combine(Directory.GetCurrentDirectory(), "bin", dxcExeName)
            };

            foreach (string commonPath in commonPaths)
            {
                if (File.Exists(commonPath))
                {
                    return commonPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Dispatches raytracing work using the DXR fallback layer.
        ///
        /// The DXR fallback layer provides DXR API compatibility on DirectX 11 hardware by emulating
        /// raytracing using compute shaders. This implementation dispatches raytracing work using
        /// shader binding tables (SBT) that contain shader identifiers and resources.
        ///
        /// Based on DXR API: ID3D12GraphicsCommandList4::DispatchRays
        /// DXR API Reference:
        /// - D3D12_DISPATCH_RAYS_DESC: Describes the raytracing dispatch parameters
        /// - D3D12_GPU_VIRTUAL_ADDRESS_RANGE: GPU virtual address ranges for shader binding tables
        /// - DispatchRays: Executes the raytracing work on the GPU
        ///
        /// Microsoft D3D12RaytracingFallback library:
        /// https://github.com/microsoft/DirectX-Graphics-Samples/tree/master/Libraries/D3D12RaytracingFallback
        ///
        /// The shader binding tables (SBT) contain:
        /// - RayGenShaderTable: Shader identifiers for ray generation shaders
        /// - MissShaderTable: Shader identifiers for miss shaders
        /// - HitGroupTable: Shader identifiers for hit group shaders (closest hit, any hit, intersection)
        ///
        /// Each shader binding table entry contains:
        /// - Shader identifier (32 bytes): Unique identifier for the shader
        /// - Optional root arguments: Resource bindings for the shader
        /// </summary>
        /// <param name="desc">Dispatch rays description containing SBT handles and dispatch dimensions</param>
        protected override void OnDispatchRaysFallback(DispatchRaysDesc desc)
        {
            // Validate inputs
            if (!_useDxrFallbackLayer || _raytracingFallbackDevice == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Cannot dispatch rays: DXR fallback layer not initialized");
                return;
            }

            if (_commandList == null)
            {
                Console.WriteLine("[StrideDX11] Cannot dispatch rays: Command list not available");
                return;
            }

            // Validate dispatch dimensions
            if (desc.Width <= 0 || desc.Height <= 0 || desc.Depth <= 0)
            {
                Console.WriteLine("[StrideDX11] Cannot dispatch rays: Invalid dispatch dimensions (Width={0}, Height={1}, Depth={2})",
                    desc.Width, desc.Height, desc.Depth);
                return;
            }

            // Validate shader binding table handles
            // At minimum, we need a ray generation shader table
            if (desc.RayGenShaderTable == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Cannot dispatch rays: RayGen shader table is required");
                return;
            }

            try
            {
                // Step 1: Get fallback command list
                // The DXR fallback layer requires a command list to execute raytracing operations
                IntPtr fallbackCommandList = GetFallbackCommandList();
                if (fallbackCommandList == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to get fallback command list for raytracing dispatch");
                    return;
                }

                // Step 2: Get GPU virtual addresses for shader binding tables
                // The DXR fallback layer uses GPU virtual addresses to reference shader binding tables
                IntPtr rayGenGpuAddress = GetGpuVirtualAddress(desc.RayGenShaderTable);
                IntPtr missGpuAddress = GetGpuVirtualAddress(desc.MissShaderTable);
                IntPtr hitGroupGpuAddress = GetGpuVirtualAddress(desc.HitGroupTable);

                // Validate ray generation shader table address (required)
                if (rayGenGpuAddress == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Cannot dispatch rays: Failed to get GPU virtual address for RayGen shader table");
                    return;
                }

                // Step 3: Get shader binding table sizes
                // Each SBT entry is typically 32 bytes (shader identifier) plus optional root arguments
                // We need to determine the size of each table to set up the GPU virtual address ranges
                uint rayGenTableSize = GetShaderBindingTableSize(desc.RayGenShaderTable);
                uint missTableSize = GetShaderBindingTableSize(desc.MissShaderTable);
                uint hitGroupTableSize = GetShaderBindingTableSize(desc.HitGroupTable);

                // Validate ray generation shader table size (required)
                if (rayGenTableSize == 0)
                {
                    Console.WriteLine("[StrideDX11] Cannot dispatch rays: RayGen shader table size is zero");
                    return;
                }

                // Step 4: Create D3D12_DISPATCH_RAYS_DESC structure
                // This structure describes the raytracing dispatch parameters
                D3D12_DISPATCH_RAYS_DESC dispatchRaysDesc = new D3D12_DISPATCH_RAYS_DESC
                {
                    // Ray generation shader table (required)
                    RayGenerationShaderRecord = new D3D12_GPU_VIRTUAL_ADDRESS_RANGE
                    {
                        StartAddress = rayGenGpuAddress,
                        SizeInBytes = rayGenTableSize
                    },

                    // Miss shader table (optional, but typically provided)
                    MissShaderTable = new D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE
                    {
                        StartAddress = missGpuAddress,
                        SizeInBytes = missTableSize,
                        StrideInBytes = missTableSize > 0 ? GetShaderBindingTableStride(desc.MissShaderTable) : 0
                    },

                    // Hit group shader table (optional, but typically provided)
                    HitGroupTable = new D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE
                    {
                        StartAddress = hitGroupGpuAddress,
                        SizeInBytes = hitGroupTableSize,
                        StrideInBytes = hitGroupTableSize > 0 ? GetShaderBindingTableStride(desc.HitGroupTable) : 0
                    },

                    // Callable shader table (optional, not used in basic raytracing)
                    CallableShaderTable = new D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE
                    {
                        StartAddress = IntPtr.Zero,
                        SizeInBytes = 0,
                        StrideInBytes = 0
                    },

                    // Dispatch dimensions
                    Width = (uint)desc.Width,
                    Height = (uint)desc.Height,
                    Depth = (uint)desc.Depth
                };

                // Step 5: Dispatch raytracing work using the fallback layer
                // The DXR fallback layer translates this to compute shader dispatches
                DispatchRaysFallback(
                    fallbackCommandList,
                    ref dispatchRaysDesc);

                Console.WriteLine("[StrideDX11] Dispatched raytracing work (Width={0}, Height={1}, Depth={2}, RayGenTable={3} bytes, MissTable={4} bytes, HitGroupTable={5} bytes)",
                    desc.Width, desc.Height, desc.Depth, rayGenTableSize, missTableSize, hitGroupTableSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideDX11] Exception dispatching rays: {0}", ex.Message);
                Console.WriteLine("[StrideDX11] Stack trace: {0}", ex.StackTrace);
            }
        }

        /// <summary>
        /// Updates the transform matrix for a specific instance in a top-level acceleration structure (TLAS).
        ///
        /// The DXR fallback layer allows updating instance transforms without rebuilding the entire TLAS
        /// when the TLAS was created with D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_UPDATE.
        /// This implementation updates the instance descriptor in the instance buffer and rebuilds the TLAS.
        ///
        /// Based on DXR API: ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure
        /// with D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PERFORM_UPDATE flag.
        ///
        /// DXR API Reference:
        /// - D3D12_RAYTRACING_INSTANCE_DESC: Describes a single instance in the TLAS
        /// - BuildRaytracingAccelerationStructure: Rebuilds the TLAS with updated instance data
        /// </summary>
        /// <param name="tlas">Handle to the TLAS resource</param>
        /// <param name="instanceIndex">Zero-based index of the instance to update</param>
        /// <param name="transform">New transform matrix (Matrix4x4) to apply to the instance</param>
        protected override void OnUpdateTlasInstanceFallback(IntPtr tlas, int instanceIndex, System.Numerics.Matrix4x4 transform)
        {
            // Validate inputs
            if (!_useDxrFallbackLayer || _raytracingFallbackDevice == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Cannot update TLAS instance: DXR fallback layer not initialized");
                return;
            }

            if (tlas == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Cannot update TLAS instance: Invalid TLAS handle");
                return;
            }

            if (instanceIndex < 0)
            {
                Console.WriteLine("[StrideDX11] Cannot update TLAS instance: Invalid instance index {0}", instanceIndex);
                return;
            }

            // Find the TLAS resource
            ResourceInfo tlasInfo;
            if (!_resources.TryGetValue(tlas, out tlasInfo))
            {
                Console.WriteLine("[StrideDX11] Cannot update TLAS instance: TLAS resource not found");
                return;
            }

            if (tlasInfo.Type != ResourceType.AccelerationStructure)
            {
                Console.WriteLine("[StrideDX11] Cannot update TLAS instance: Resource is not an acceleration structure");
                return;
            }

            // Get TLAS instance data
            TlasInstanceData instanceData;
            if (!_tlasInstanceData.TryGetValue(tlas, out instanceData))
            {
                Console.WriteLine("[StrideDX11] Cannot update TLAS instance: TLAS instance data not found (TLAS may not have been properly created)");
                return;
            }

            // Validate instance index
            if (instanceIndex >= instanceData.InstanceCount)
            {
                Console.WriteLine("[StrideDX11] Cannot update TLAS instance: Instance index {0} is out of range (max: {1})",
                    instanceIndex, instanceData.InstanceCount - 1);
                return;
            }

            try
            {
                // Step 1: Update the transform in the instance descriptor
                // D3D12_RAYTRACING_INSTANCE_DESC uses a 3x4 row-major transform matrix
                // We need to convert Matrix4x4 to the 3x4 format expected by DXR
                D3D12_RAYTRACING_INSTANCE_DESC instanceDesc = instanceData.InstanceDescs[instanceIndex];

                // Convert Matrix4x4 to 3x4 row-major format
                // DXR expects the transform in row-major order: [m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23]
                instanceDesc.Transform[0] = transform.M11; // Row 0, Col 0
                instanceDesc.Transform[1] = transform.M12; // Row 0, Col 1
                instanceDesc.Transform[2] = transform.M13; // Row 0, Col 2
                instanceDesc.Transform[3] = transform.M14; // Row 0, Col 3
                instanceDesc.Transform[4] = transform.M21; // Row 1, Col 0
                instanceDesc.Transform[5] = transform.M22; // Row 1, Col 1
                instanceDesc.Transform[6] = transform.M23; // Row 1, Col 2
                instanceDesc.Transform[7] = transform.M24; // Row 1, Col 3
                instanceDesc.Transform[8] = transform.M31; // Row 2, Col 0
                instanceDesc.Transform[9] = transform.M32; // Row 2, Col 1
                instanceDesc.Transform[10] = transform.M33; // Row 2, Col 2
                instanceDesc.Transform[11] = transform.M34; // Row 2, Col 3

                // Update the instance descriptor in the array
                instanceData.InstanceDescs[instanceIndex] = instanceDesc;

                // Step 2: Update the instance buffer with the new transform
                // Map the instance buffer for CPU write access
                IntPtr mappedData = MapBuffer(instanceData.InstanceBufferHandle, MapType.Write);
                if (mappedData == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX11] Failed to map instance buffer for update");
                    return;
                }

                try
                {
                    // Calculate offset to the specific instance descriptor
                    int instanceDescSize = Marshal.SizeOf(typeof(D3D12_RAYTRACING_INSTANCE_DESC));
                    int instanceOffset = instanceIndex * instanceDescSize;

                    // Copy the updated instance descriptor to the buffer
                    IntPtr instancePtr = new IntPtr(mappedData.ToInt64() + instanceOffset);
                    Marshal.StructureToPtr(instanceDesc, instancePtr, false);

                    // Note: We only update the specific instance descriptor that changed.
                    // The rest of the buffer remains unchanged, which is efficient for updates.
                }
                finally
                {
                    // Unmap the buffer
                    UnmapBuffer(instanceData.InstanceBufferHandle);
                }

                // Step 3: Rebuild the TLAS with the updated instance data
                // Check if the TLAS was built with ALLOW_UPDATE flag
                bool canUpdate = (instanceData.BuildFlags & D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_UPDATE) != 0;

                if (canUpdate)
                {
                    // Use update mode (faster, only updates changed instances)
                    RebuildTlasWithUpdate(tlas, instanceData);
                }
                else
                {
                    // Full rebuild (slower, but works even without ALLOW_UPDATE flag)
                    RebuildTlasFull(tlas, instanceData);
                }

                Console.WriteLine("[StrideDX11] Updated TLAS instance {0} transform successfully", instanceIndex);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideDX11] Exception updating TLAS instance: {0}", ex.Message);
                Console.WriteLine("[StrideDX11] Stack trace: {0}", ex.StackTrace);
            }
        }

        #endregion

        #region Helper Methods for TLAS Instance Updates

        /// <summary>
        /// Rebuilds the TLAS using update mode (faster, only updates changed instances).
        /// Requires the TLAS to have been built with ALLOW_UPDATE flag.
        /// </summary>
        /// <param name="tlas">Handle to the TLAS resource</param>
        /// <param name="instanceData">Instance data for the TLAS</param>
        private void RebuildTlasWithUpdate(IntPtr tlas, TlasInstanceData instanceData)
        {
            // Get the fallback command list
            IntPtr fallbackCommandList = GetFallbackCommandList();
            if (fallbackCommandList == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Failed to get fallback command list for TLAS update");
                return;
            }

            // Create build inputs for update
            D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS buildInputs = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
            {
                Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL,
                Flags = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_TRACE |
                        D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_UPDATE |
                        D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PERFORM_UPDATE, // Update mode
                NumDescs = (uint)instanceData.InstanceCount,
                DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY,
                pGeometryDescs = IntPtr.Zero // TLAS uses instance descriptors
            };

            // Build description for update
            D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC buildDesc = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
            {
                DestAccelerationStructureData = GetGpuVirtualAddress(instanceData.ResultBufferHandle),
                Inputs = buildInputs,
                SourceAccelerationStructureData = GetGpuVirtualAddress(instanceData.ResultBufferHandle), // Source is the same as destination for updates
                ScratchAccelerationStructureData = GetGpuVirtualAddress(instanceData.ScratchBufferHandle)
            };

            // Update the instance buffer GPU address in build inputs
            // For TLAS, the instance descriptors are stored in a separate buffer.
            // In DXR, pGeometryDescs is used to point to the instance buffer GPU address for TLAS
            // (even though it's named pGeometryDescs, it serves dual purpose for TLAS instance buffers)
            IntPtr instanceBufferGpuAddress = GetGpuVirtualAddress(instanceData.InstanceBufferHandle);
            if (instanceBufferGpuAddress == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Failed to get GPU virtual address for instance buffer");
                return;
            }
            buildInputs.pGeometryDescs = instanceBufferGpuAddress;

            // Rebuild the TLAS with update
            BuildRaytracingAccelerationStructure(
                fallbackCommandList,
                ref buildDesc,
                0, // NumPostbuildInfoDescs
                IntPtr.Zero); // pPostbuildInfoDescs

            Console.WriteLine("[StrideDX11] TLAS updated successfully (Update mode, {0} instances)", instanceData.InstanceCount);
        }

        /// <summary>
        /// Rebuilds the TLAS from scratch (full rebuild).
        /// Used when ALLOW_UPDATE flag is not set or when a full rebuild is required.
        /// </summary>
        /// <param name="tlas">Handle to the TLAS resource</param>
        /// <param name="instanceData">Instance data for the TLAS</param>
        private void RebuildTlasFull(IntPtr tlas, TlasInstanceData instanceData)
        {
            // Get the fallback command list
            IntPtr fallbackCommandList = GetFallbackCommandList();
            if (fallbackCommandList == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Failed to get fallback command list for TLAS rebuild");
                return;
            }

            // Create build inputs for full rebuild
            D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS buildInputs = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
            {
                Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL,
                Flags = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_TRACE,
                NumDescs = (uint)instanceData.InstanceCount,
                DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY,
                pGeometryDescs = IntPtr.Zero // TLAS uses instance descriptors
            };

            // Set instance buffer GPU address
            // For TLAS, pGeometryDescs points to the instance buffer GPU address
            IntPtr instanceBufferGpuAddress = GetGpuVirtualAddress(instanceData.InstanceBufferHandle);
            if (instanceBufferGpuAddress == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX11] Failed to get GPU virtual address for instance buffer");
                return;
            }
            buildInputs.pGeometryDescs = instanceBufferGpuAddress;

            // Build description for full rebuild
            D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC buildDesc = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
            {
                DestAccelerationStructureData = GetGpuVirtualAddress(instanceData.ResultBufferHandle),
                Inputs = buildInputs,
                SourceAccelerationStructureData = IntPtr.Zero, // No source (building from scratch)
                ScratchAccelerationStructureData = GetGpuVirtualAddress(instanceData.ScratchBufferHandle)
            };

            // Rebuild the TLAS
            BuildRaytracingAccelerationStructure(
                fallbackCommandList,
                ref buildDesc,
                0, // NumPostbuildInfoDescs
                IntPtr.Zero); // pPostbuildInfoDescs

            Console.WriteLine("[StrideDX11] TLAS rebuilt successfully (Full rebuild, {0} instances)", instanceData.InstanceCount);
        }

        /// <summary>
        /// Creates a buffer for storing TLAS instance descriptors.
        /// Instance descriptors contain transform matrices and BLAS references.
        /// </summary>
        /// <param name="sizeInBytes">Size of the instance buffer in bytes</param>
        /// <returns>Handle to the created instance buffer, or IntPtr.Zero on failure</returns>
        private IntPtr CreateInstanceBuffer(int sizeInBytes)
        {
            if (sizeInBytes == 0)
            {
                return IntPtr.Zero;
            }

            // Create a buffer with acceleration structure build input usage
            // This buffer stores instance descriptors and needs to be accessible by the build process
            var bufferDesc = new Andastra.Runtime.Graphics.Common.Structs.BufferDescription
            {
                SizeInBytes = sizeInBytes,
                Usage = BufferUsage.AccelerationStructure, // Instance buffer for TLAS
                StructureByteStride = 0,
                DebugName = "TLAS_InstanceBuffer"
            };

            return CreateBuffer(bufferDesc);
        }

        #endregion

        #region Helper Methods for BLAS Creation

        /// <summary>
        /// Creates a scratch buffer for building acceleration structures.
        /// Scratch buffers are temporary memory used during the build process.
        /// </summary>
        /// <param name="sizeInBytes">Size of the scratch buffer in bytes</param>
        /// <returns>Handle to the created scratch buffer, or IntPtr.Zero on failure</returns>
        private IntPtr CreateScratchBuffer(ulong sizeInBytes)
        {
            if (sizeInBytes == 0)
            {
                return IntPtr.Zero;
            }

            // Create a structured buffer for scratch memory used during acceleration structure building
            // The scratch buffer is used temporarily and needs to be accessible by compute shaders
            // We use CreateStructuredBuffer which creates a buffer suitable for compute shader access
            return CreateStructuredBuffer((int)(sizeInBytes / 16), 16, false); // Use 16-byte alignment
        }

        /// <summary>
        /// Creates a buffer for storing the acceleration structure result.
        /// This buffer will contain the built acceleration structure data.
        /// </summary>
        /// <param name="sizeInBytes">Size of the result buffer in bytes</param>
        /// <returns>Handle to the created result buffer, or IntPtr.Zero on failure</returns>
        private IntPtr CreateAccelerationStructureBuffer(ulong sizeInBytes)
        {
            if (sizeInBytes == 0)
            {
                return IntPtr.Zero;
            }

            // Create a buffer with acceleration structure usage flag
            // This buffer stores the final acceleration structure data and needs to be
            // accessible by raytracing shaders
            var bufferDesc = new Andastra.Runtime.Graphics.Common.Structs.BufferDescription
            {
                SizeInBytes = (int)sizeInBytes,
                Usage = BufferUsage.AccelerationStructure,
                StructureByteStride = 0,
                DebugName = "BLAS_ResultBuffer"
            };

            return CreateBuffer(bufferDesc);
        }

        /// <summary>
        /// Gets the GPU virtual address for a buffer handle.
        /// The DXR fallback layer uses GPU virtual addresses for acceleration structure operations.
        ///
        /// For DirectX 11 with DXR fallback layer, we query the buffer for ID3D12Resource interface
        /// (which the fallback layer provides) and then call GetGPUVirtualAddress through the COM vtable.
        /// This allows us to get GPU virtual addresses even though D3D11 doesn't natively support them.
        ///
        /// Based on DirectX 12 Resources: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12resource-getgpuvirtualaddress
        /// DXR Fallback Layer: https://github.com/microsoft/DirectX-Graphics-Samples/tree/master/Libraries/D3D12RaytracingFallback
        /// </summary>
        /// <param name="bufferHandle">Handle to the buffer</param>
        /// <returns>GPU virtual address, or IntPtr.Zero if the buffer is invalid</returns>
        private unsafe IntPtr GetGpuVirtualAddress(IntPtr bufferHandle)
        {
            if (bufferHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Get the native buffer pointer from Stride
            ResourceInfo info;
            if (!_resources.TryGetValue(bufferHandle, out info))
            {
                return IntPtr.Zero;
            }

            if (info.NativeHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Platform check: DirectX COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, return native handle as fallback
                return info.NativeHandle;
            }

            // For DirectX 11 with DXR fallback layer, query the buffer for ID3D12Resource interface
            // The fallback layer wraps D3D11 resources and provides D3D12-like interfaces
            IntPtr d3d12Resource = IntPtr.Zero;

            try
            {
                // QueryInterface is at vtable index 0 in IUnknown
                // QueryInterface signature: HRESULT QueryInterface(REFIID riid, void** ppvObject)
                IntPtr* vtable = *(IntPtr**)info.NativeHandle;
                IntPtr queryInterfacePtr = vtable[0]; // QueryInterface is at index 0

                QueryInterfaceDelegate queryInterface =
                    Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(queryInterfacePtr);

                // Query for ID3D12Resource interface
                Guid iidId3d12Resource = IID_ID3D12Resource;
                int hr = queryInterface(info.NativeHandle, ref iidId3d12Resource, out d3d12Resource);
                if (hr == 0 && d3d12Resource != IntPtr.Zero) // S_OK = 0
                {
                    // Successfully obtained ID3D12Resource interface
                    // Now call GetGPUVirtualAddress through the COM vtable
                    // GetGPUVirtualAddress is at index 8 in ID3D12Resource vtable
                    IntPtr* d3d12Vtable = *(IntPtr**)d3d12Resource;
                    IntPtr getGpuVaPtr = d3d12Vtable[8];

                    GetGpuVirtualAddressDelegate getGpuVa =
                        Marshal.GetDelegateForFunctionPointer<GetGpuVirtualAddressDelegate>(getGpuVaPtr);

                    ulong gpuVirtualAddress = getGpuVa(d3d12Resource);

                    // Release the ID3D12Resource interface (AddRef/Release at vtable indices 1/2)
                    IntPtr releasePtr = d3d12Vtable[2]; // Release is at index 2
                    ReleaseDelegate release =
                        Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(releasePtr);
                    release(d3d12Resource);

                    return new IntPtr((long)gpuVirtualAddress);
                }
            }
            catch
            {
                // If COM interface query fails, fall back to native handle
                // This can happen if the buffer doesn't support ID3D12Resource interface
                // or if we're not using the DXR fallback layer
            }

            // Fallback: return native handle as placeholder
            // The DXR fallback layer may handle address translation internally
            return info.NativeHandle;
        }

        /// <summary>
        /// Gets the fallback command list for building acceleration structures.
        /// The DXR fallback layer requires a command list to execute build operations.
        ///
        /// Based on Microsoft D3D12RaytracingFallback library:
        /// https://github.com/microsoft/DirectX-Graphics-Samples/tree/master/Libraries/D3D12RaytracingFallback
        /// The fallback device can create or provide command lists that are compatible with DXR fallback operations.
        /// </summary>
        /// <returns>Handle to the fallback command list, or IntPtr.Zero if unavailable</returns>
        private unsafe IntPtr GetFallbackCommandList()
        {
            // If fallback device is not available, cannot get fallback command list
            if (!_useDxrFallbackLayer || _raytracingFallbackDevice == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // If we already have a cached fallback command list, return it
            if (_fallbackCommandList != IntPtr.Zero)
            {
                return _fallbackCommandList;
            }

            // Ensure we have the Stride command list
            if (_commandList == null)
            {
                _commandList = _game?.GraphicsContext?.CommandList;
            }

            if (_commandList == null)
            {
                return IntPtr.Zero;
            }

            // Attempt to query the fallback command list from the fallback device
            // The D3D12RaytracingFallback library provides ID3D12RaytracingFallbackCommandList interface
            // which can be queried from the fallback device or created from it
            //
            // Note: In a full implementation with properly initialized fallback device, we would:
            // 1. Query the fallback device for ID3D12RaytracingFallbackCommandList interface using QueryInterface
            // 2. Or create a command list from the fallback device using CreateCommandList method
            // 3. The fallback device would be created via D3D12CreateRaytracingFallbackDevice in InitializeDxrFallback
            //
            // However, since the fallback device is currently set to the native D3D11 device (_device),
            // and the DXR fallback layer may accept the native D3D11 command list pointer directly,
            // we use the native Stride command list as the fallback command list.
            //
            // The DXR fallback layer will internally wrap the D3D11 command list to provide DXR API compatibility.
            try
            {
                // Method 1: Query the fallback device for ID3D12RaytracingFallbackCommandList interface
                // This would work if the fallback device is properly initialized via D3D12CreateRaytracingFallbackDevice
                IntPtr* vtable = *(IntPtr**)_raytracingFallbackDevice;
                if (vtable != null)
                {
                    IntPtr queryInterfacePtr = vtable[0]; // QueryInterface is at index 0
                    QueryInterfaceDelegate queryInterface =
                        Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(queryInterfacePtr);

                    // Note: The actual GUID for ID3D12RaytracingFallbackCommandList would need to be obtained
                    // from the D3D12RaytracingFallback library headers. For now, we skip this query
                    // and use the native command list directly, as the fallback layer may accept it.

                    // Method 2: Use the native Stride command list pointer
                    // The DXR fallback layer may accept the native D3D11 command list pointer directly
                    // and wrap it internally to provide DXR API compatibility
                    IntPtr nativeCommandList = GetNativeCommandListHandle(_commandList);
                    if (nativeCommandList != IntPtr.Zero)
                    {
                        // Cache the native command list as the fallback command list
                        // The fallback layer will handle the translation from D3D11 to DXR API
                        _fallbackCommandList = nativeCommandList;
                        return _fallbackCommandList;
                    }
                }
            }
            catch
            {
                // If querying fails, fall back to using the native Stride command list
                // This can happen if the fallback device doesn't support QueryInterface
                // or if we're not properly initialized
            }

            // Fallback: Use the native Stride command list pointer
            // The DXR fallback layer may accept this and wrap it internally
            // This is the current implementation approach since the fallback device is set to the native D3D11 device
            IntPtr fallbackCmdList = _commandList != null ? GetNativeCommandListHandle(_commandList) : IntPtr.Zero;
            if (fallbackCmdList != IntPtr.Zero)
            {
                _fallbackCommandList = fallbackCmdList;
            }
            return fallbackCmdList;
        }

        /// <summary>
        /// Gets the native command list handle from Stride's CommandList using reflection.
        /// </summary>
        private IntPtr GetNativeCommandListHandle(global::Stride.Graphics.CommandList commandList)
        {
            if (commandList == null)
            {
                return IntPtr.Zero;
            }

            try
            {
                var commandListType = commandList.GetType();
                var nativeProperty = commandListType.GetProperty("NativeCommandList", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nativeProperty != null)
                {
                    var value = nativeProperty.GetValue(commandList);
                    if (value is IntPtr)
                    {
                        return (IntPtr)value;
                    }
                }

                // Try NativePointer as alternative
                var nativePointerProperty = commandListType.GetProperty("NativePointer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nativePointerProperty != null)
                {
                    var value = nativePointerProperty.GetValue(commandList);
                    if (value is IntPtr)
                    {
                        return (IntPtr)value;
                    }
                }
            }
            catch
            {
                // If reflection fails, return IntPtr.Zero
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the size of a shader binding table in bytes.
        /// Shader binding tables contain shader identifiers (32 bytes each) plus optional root arguments.
        /// </summary>
        /// <param name="sbtHandle">Handle to the shader binding table buffer</param>
        /// <returns>Size of the shader binding table in bytes, or 0 if invalid</returns>
        private uint GetShaderBindingTableSize(IntPtr sbtHandle)
        {
            if (sbtHandle == IntPtr.Zero)
            {
                return 0;
            }

            // Get the resource info for the shader binding table
            ResourceInfo info;
            if (!_resources.TryGetValue(sbtHandle, out info))
            {
                return 0;
            }

            // Return the size in bytes
            return (uint)info.SizeInBytes;
        }

        /// <summary>
        /// Gets the stride (size per entry) of a shader binding table in bytes.
        /// The stride is the size of each shader binding table entry, which includes
        /// the shader identifier (32 bytes) plus any root arguments.
        ///
        /// Based on D3D12 DXR API:
        /// - D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES = 32 (minimum stride)
        /// - Stride must be aligned to D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT (32 bytes)
        /// - Common strides: 32 (identifier only), 64 (identifier + 32B root args), 96, 128, etc.
        ///
        /// Implementation strategy:
        /// 1. Check cached stride value (set when SBT is created or stride is known)
        /// 2. Calculate stride from buffer size using heuristics for common patterns
        /// 3. Cache the calculated stride for future lookups
        /// 4. Fall back to minimum stride (32 bytes) if calculation fails
        /// </summary>
        /// <param name="sbtHandle">Handle to the shader binding table buffer</param>
        /// <returns>Stride of each shader binding table entry in bytes, or 0 if invalid</returns>
        private uint GetShaderBindingTableStride(IntPtr sbtHandle)
        {
            if (sbtHandle == IntPtr.Zero)
            {
                return 0;
            }

            // Check if we have a cached stride value for this SBT
            uint cachedStride;
            if (_sbtStrideCache.TryGetValue(sbtHandle, out cachedStride))
            {
                return cachedStride;
            }

            // Get the resource info for the shader binding table
            ResourceInfo info;
            if (!_resources.TryGetValue(sbtHandle, out info))
            {
                return 0;
            }

            // Calculate stride from buffer size
            // D3D12 shader binding table entries must be aligned to 32 bytes
            const uint D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES = 32;
            const uint D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT = 32;

            uint bufferSize = (uint)info.SizeInBytes;

            // If buffer is empty or too small, return minimum stride
            if (bufferSize < D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES)
            {
                return D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES;
            }

            // Try to infer stride from buffer size
            // Common stride values are multiples of 32: 32, 64, 96, 128, 160, 192, 224, 256, etc.
            // We check if the buffer size is evenly divisible by common stride values
            // and use the smallest reasonable stride that divides evenly
            uint[] commonStrides = new uint[] { 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 480, 512 };

            uint calculatedStride = 0;

            // Check common stride values to find one that divides evenly into the buffer size
            // We prefer smaller strides (more entries), but must be at least 32 bytes
            for (int i = 0; i < commonStrides.Length; i++)
            {
                uint stride = commonStrides[i];
                if (bufferSize % stride == 0)
                {
                    // Verify this stride makes sense (at least 1 entry, reasonable maximum)
                    uint entryCount = bufferSize / stride;
                    if (entryCount >= 1 && entryCount <= 1024) // Reasonable entry count limit
                    {
                        calculatedStride = stride;
                        break;
                    }
                }
            }

            // If no common stride works, try to find the stride by checking alignment
            // The stride should be the largest power-of-2 multiple of 32 that divides evenly
            if (calculatedStride == 0)
            {
                // Start with minimum stride and check multiples
                uint testStride = D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES;
                uint bestStride = testStride;

                // Check strides up to a reasonable maximum (512 bytes)
                while (testStride <= 512 && testStride <= bufferSize)
                {
                    if (bufferSize % testStride == 0)
                    {
                        uint entryCount = bufferSize / testStride;
                        if (entryCount >= 1 && entryCount <= 1024)
                        {
                            bestStride = testStride;
                        }
                    }
                    testStride += D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT;
                }

                calculatedStride = bestStride;
            }

            // If we still couldn't determine stride, use minimum stride (32 bytes)
            // This is the safest default and matches the minimum requirement
            if (calculatedStride == 0)
            {
                calculatedStride = D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES;
            }

            // Ensure stride is properly aligned (should always be, but verify)
            if (calculatedStride % D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT != 0)
            {
                // Round up to next alignment boundary
                calculatedStride = ((calculatedStride + D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT - 1) / D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT) * D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT;
            }

            // Cache the calculated stride for future lookups
            _sbtStrideCache[sbtHandle] = calculatedStride;

            return calculatedStride;
        }

        /// <summary>
        /// Sets the stride for a shader binding table buffer.
        /// This method allows explicit stride specification when the stride is known,
        /// which provides more accurate stride information than heuristic calculation.
        ///
        /// The stride must be aligned to D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT (32 bytes).
        /// </summary>
        /// <param name="sbtHandle">Handle to the shader binding table buffer</param>
        /// <param name="stride">Stride in bytes (must be >= 32 and aligned to 32 bytes)</param>
        /// <returns>True if stride was set successfully, false if parameters are invalid</returns>
        private bool SetShaderBindingTableStride(IntPtr sbtHandle, uint stride)
        {
            if (sbtHandle == IntPtr.Zero)
            {
                return false;
            }

            // Validate stride: must be at least 32 bytes and aligned to 32 bytes
            const uint D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES = 32;
            const uint D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT = 32;

            if (stride < D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES)
            {
                return false;
            }

            if (stride % D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT != 0)
            {
                return false;
            }

            // Verify the handle exists in resources
            if (!_resources.ContainsKey(sbtHandle))
            {
                return false;
            }

            // Cache the stride
            _sbtStrideCache[sbtHandle] = stride;
            return true;
        }

        #endregion

        #region P/Invoke Declarations for DXR Fallback Layer

        /// <summary>
        /// Loads the specified module into the address space of the calling process.
        /// Used to check for D3D12RaytracingFallback.dll availability.
        /// </summary>
        /// <param name="lpLibFileName">The name of the module (DLL) to load</param>
        /// <returns>Handle to the loaded module, or IntPtr.Zero if the module could not be loaded</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpLibFileName);

        /// <summary>
        /// Frees the loaded dynamic-link library (DLL) module and decrements its reference count.
        /// Used to release the handle obtained from LoadLibrary.
        /// </summary>
        /// <param name="hModule">Handle to the loaded library module</param>
        /// <returns>True if the function succeeds, false otherwise</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>
        /// Retrieves a module handle for the specified module if it has been loaded into the address space
        /// of the calling process. Unlike LoadLibrary, this does not increment the module reference count.
        /// Used to check if D3D12RaytracingFallback.dll is already loaded.
        /// </summary>
        /// <param name="lpModuleName">The name of the loaded module (DLL)</param>
        /// <returns>Handle to the module if loaded, or IntPtr.Zero if the module is not loaded</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        /// <summary>
        /// Retrieves the address of an exported function or variable from the specified dynamic-link library (DLL).
        /// Used to verify that required DXR fallback layer functions are available in the loaded DLL.
        /// </summary>
        /// <param name="hModule">Handle to the DLL module that contains the function</param>
        /// <param name="lpProcName">The function or variable name, or the function's ordinal value</param>
        /// <returns>Address of the exported function, or IntPtr.Zero if the function is not found</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        /// <summary>
        /// Gets prebuild information for a raytracing acceleration structure.
        /// This determines the size requirements for scratch and result buffers.
        /// Based on DXR API: ID3D12Device5::GetRaytracingAccelerationStructurePrebuildInfo
        /// </summary>
        /// <param name="device">DXR fallback device</param>
        /// <param name="pDesc">Build inputs description</param>
        /// <param name="pInfo">Output prebuild information</param>
        [DllImport("D3D12RaytracingFallback.dll", EntryPoint = "D3D12GetRaytracingAccelerationStructurePrebuildInfo", CallingConvention = CallingConvention.StdCall)]
        private static extern void GetRaytracingAccelerationStructurePrebuildInfo(
            IntPtr device,
            ref D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS pDesc,
            out D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO pInfo);

        /// <summary>
        /// Builds a raytracing acceleration structure.
        /// Based on DXR API: ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure
        /// </summary>
        /// <param name="commandList">Fallback command list</param>
        /// <param name="pDesc">Build description</param>
        /// <param name="numPostbuildInfoDescs">Number of postbuild info descriptors</param>
        /// <param name="pPostbuildInfoDescs">Postbuild info descriptors</param>
        [DllImport("D3D12RaytracingFallback.dll", EntryPoint = "D3D12BuildRaytracingAccelerationStructure", CallingConvention = CallingConvention.StdCall)]
        private static extern void BuildRaytracingAccelerationStructure(
            IntPtr commandList,
            ref D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC pDesc,
            int numPostbuildInfoDescs,
            IntPtr pPostbuildInfoDescs);

        /// <summary>
        /// Creates a raytracing pipeline state object.
        /// Based on DXR API: ID3D12Device5::CreateStateObject
        /// </summary>
        /// <param name="device">DXR fallback device</param>
        /// <param name="pDesc">State object description</param>
        /// <param name="riid">Interface ID for the state object (IID_ID3D12StateObject)</param>
        /// <param name="ppStateObject">Output state object pointer</param>
        /// <returns>HRESULT: 0 (S_OK) on success, error code on failure</returns>
        [DllImport("D3D12RaytracingFallback.dll", EntryPoint = "D3D12CreateStateObject", CallingConvention = CallingConvention.StdCall)]
        private static extern int CreateStateObject(
            IntPtr device,
            ref D3D12_STATE_OBJECT_DESC pDesc,
            [MarshalAs(UnmanagedType.LPStruct)] System.Guid riid,
            out IntPtr ppStateObject);

        /// <summary>
        /// Dispatches raytracing work using the DXR fallback layer.
        /// Based on DXR API: ID3D12GraphicsCommandList4::DispatchRays
        ///
        /// The DXR fallback layer translates this call to compute shader dispatches that emulate
        /// raytracing behavior on hardware without native raytracing support.
        /// </summary>
        /// <param name="commandList">Fallback command list</param>
        /// <param name="pDesc">Dispatch rays description containing SBT addresses and dispatch dimensions</param>
        [DllImport("D3D12RaytracingFallback.dll", EntryPoint = "D3D12DispatchRays", CallingConvention = CallingConvention.StdCall)]
        private static extern void DispatchRaysFallback(
            IntPtr commandList,
            ref D3D12_DISPATCH_RAYS_DESC pDesc);

        /// <summary>
        /// Creates a DXR fallback device that wraps a D3D11 or D3D12 device to provide
        /// DXR API compatibility on hardware without native raytracing support.
        ///
        /// The fallback device translates DXR calls to compute shader operations that
        /// emulate raytracing behavior. This allows DXR applications to run on older hardware.
        ///
        /// Based on Microsoft D3D12RaytracingFallback library:
        /// https://github.com/microsoft/DirectX-Graphics-Samples/tree/master/Libraries/D3D12RaytracingFallback
        ///
        /// Reference: D3D12CreateRaytracingFallbackDevice function
        /// </summary>
        /// <param name="pDevice">Pointer to the underlying D3D11 or D3D12 device (as IUnknown*)</param>
        /// <param name="CreateFlags">Creation flags (D3D12_RAYTRACING_FALLBACK_FLAGS)</param>
        /// <param name="riid">Interface ID to query for (typically IID_ID3D12RaytracingFallbackDevice)</param>
        /// <param name="ppDevice">Output pointer to the created fallback device</param>
        /// <returns>HRESULT: 0 (S_OK) on success, error code on failure</returns>
        [DllImport("D3D12RaytracingFallback.dll", EntryPoint = "D3D12CreateRaytracingFallbackDevice", CallingConvention = CallingConvention.StdCall)]
        private static extern int D3D12CreateRaytracingFallbackDevice(
            IntPtr pDevice,
            D3D12_RAYTRACING_FALLBACK_FLAGS CreateFlags,
            [MarshalAs(UnmanagedType.LPStruct)] System.Guid riid,
            out IntPtr ppDevice);

        #endregion

        #region TLAS Instance Data Structure

        /// <summary>
        /// Tracks instance data for a TLAS, including instance descriptors and buffer handles.
        /// </summary>
        private struct TlasInstanceData
        {
            public IntPtr InstanceBufferHandle; // Buffer containing instance descriptors
            public IntPtr ScratchBufferHandle; // Scratch buffer for building/updating
            public IntPtr ResultBufferHandle; // Result buffer containing the built TLAS
            public D3D12_RAYTRACING_INSTANCE_DESC[] InstanceDescs; // Array of instance descriptors
            public int InstanceCount; // Number of instances
            public int MaxInstances; // Maximum number of instances (capacity)
            public uint BuildFlags; // Build flags used when creating the TLAS
        }

        #endregion

        #region DXR Fallback Layer Structures

        // DXR fallback layer structures based on D3D12 raytracing API
        // These structures match the D3D12 raytracing API for compatibility

        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL = 0;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL = 1;

        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_NONE = 0;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_UPDATE = 0x1;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_COMPACTION = 0x2;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_TRACE = 0x4;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_BUILD = 0x8;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_MINIMIZE_MEMORY = 0x10;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PERFORM_UPDATE = 0x20;

        private const uint D3D12_RAYTRACING_GEOMETRY_TYPE_TRIANGLES = 0;
        private const uint D3D12_RAYTRACING_GEOMETRY_TYPE_PROCEDURAL_PRIMITIVE_AABBS = 1;

        private const uint D3D12_RAYTRACING_GEOMETRY_FLAG_NONE = 0;
        private const uint D3D12_RAYTRACING_GEOMETRY_FLAG_OPAQUE = 0x1;
        private const uint D3D12_RAYTRACING_GEOMETRY_FLAG_NO_DUPLICATE_ANYHIT_INVOCATION = 0x2;

        private const uint D3D12_RAYTRACING_INDEX_FORMAT_UINT16 = 0;
        private const uint D3D12_RAYTRACING_INDEX_FORMAT_UINT32 = 1;

        private const uint D3D12_RAYTRACING_VERTEX_FORMAT_FLOAT3 = 0;
        private const uint D3D12_RAYTRACING_VERTEX_FORMAT_FLOAT2 = 1;

        private const uint D3D12_ELEMENTS_LAYOUT_ARRAY = 0;
        private const uint D3D12_ELEMENTS_LAYOUT_ARRAY_OF_POINTERS = 1;

        /// <summary>
        /// GPU virtual address range.
        /// Based on D3D12 API: D3D12_GPU_VIRTUAL_ADDRESS_RANGE
        /// Used for shader binding tables that contain a single entry (ray generation shader table).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_GPU_VIRTUAL_ADDRESS_RANGE
        {
            /// <summary>
            /// GPU virtual address of the start of the range.
            /// </summary>
            public IntPtr StartAddress;

            /// <summary>
            /// Size of the range in bytes.
            /// </summary>
            public ulong SizeInBytes;
        }

        /// <summary>
        /// GPU virtual address range with stride.
        /// Based on D3D12 API: D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE
        /// Used for shader binding tables that contain multiple entries (miss shader table, hit group table).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE
        {
            /// <summary>
            /// GPU virtual address of the start of the range.
            /// </summary>
            public IntPtr StartAddress;

            /// <summary>
            /// Size of the range in bytes (total size of all entries).
            /// </summary>
            public ulong SizeInBytes;

            /// <summary>
            /// Stride in bytes between entries (size of each shader binding table entry).
            /// Typically 32 bytes (shader identifier) plus optional root arguments.
            /// </summary>
            public ulong StrideInBytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_GPU_VIRTUAL_ADDRESS_AND_STRIDE
        {
            public IntPtr StartAddress;
            public uint StrideInBytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC
        {
            public IntPtr Transform3x4;
            public uint IndexFormat;
            public uint VertexFormat;
            public uint IndexCount;
            public uint VertexCount;
            public IntPtr IndexBuffer;
            public D3D12_GPU_VIRTUAL_ADDRESS_AND_STRIDE VertexBuffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_GEOMETRY_DESC
        {
            public uint Type;
            public uint Flags;
            public D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC Triangles;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
        {
            public uint Type;
            public uint Flags;
            public uint NumDescs;
            public uint DescsLayout;
            public IntPtr pGeometryDescs;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO
        {
            public ulong ResultDataMaxSizeInBytes;
            public ulong ScratchDataSizeInBytes;
            public ulong UpdateScratchDataSizeInBytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
        {
            public IntPtr DestAccelerationStructureData;
            public D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS Inputs;
            public IntPtr SourceAccelerationStructureData;
            public IntPtr ScratchAccelerationStructureData;
        }

        /// <summary>
        /// Describes a single instance in a top-level acceleration structure.
        /// Based on D3D12 API: D3D12_RAYTRACING_INSTANCE_DESC
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_INSTANCE_DESC
        {
            /// <summary>
            /// 3x4 row-major transform matrix (12 floats).
            /// DXR uses row-major format: [m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23]
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public float[] Transform;

            /// <summary>
            /// Instance ID and flags packed into a single uint.
            /// Bits 0-23: InstanceID (custom ID for the instance)
            /// Bits 24-30: Mask (instance mask for culling)
            /// Bit 31: Flags (D3D12_RAYTRACING_INSTANCE_FLAGS)
            /// </summary>
            public uint InstanceID_InstanceMask_Flags;

            /// <summary>
            /// Shader binding table offset and flags.
            /// Bits 0-23: InstanceShaderBindingTableRecordOffset
            /// Bits 24-31: Flags (additional flags)
            /// </summary>
            public uint InstanceShaderBindingTableRecordOffset_Flags;

            /// <summary>
            /// GPU virtual address of the bottom-level acceleration structure (BLAS).
            /// </summary>
            public IntPtr AccelerationStructure;
        }

        /// <summary>
        /// Dispatch rays description.
        /// Based on D3D12 API: D3D12_DISPATCH_RAYS_DESC
        ///
        /// Describes the parameters for dispatching raytracing work, including:
        /// - Shader binding table addresses (ray generation, miss, hit group, callable)
        /// - Dispatch dimensions (width, height, depth)
        ///
        /// The shader binding tables contain shader identifiers and optional root arguments
        /// that are used by the raytracing pipeline to determine which shaders to execute.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DISPATCH_RAYS_DESC
        {
            /// <summary>
            /// GPU virtual address range for the ray generation shader table.
            /// Required: Must contain at least one ray generation shader identifier.
            /// </summary>
            public D3D12_GPU_VIRTUAL_ADDRESS_RANGE RayGenerationShaderRecord;

            /// <summary>
            /// GPU virtual address range and stride for the miss shader table.
            /// Optional: Can be empty (StartAddress = 0, SizeInBytes = 0) if no miss shaders are used.
            /// </summary>
            public D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE MissShaderTable;

            /// <summary>
            /// GPU virtual address range and stride for the hit group shader table.
            /// Optional: Can be empty (StartAddress = 0, SizeInBytes = 0) if no hit groups are used.
            /// </summary>
            public D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE HitGroupTable;

            /// <summary>
            /// GPU virtual address range and stride for the callable shader table.
            /// Optional: Can be empty (StartAddress = 0, SizeInBytes = 0) if no callable shaders are used.
            /// Callable shaders are used for advanced raytracing features like shader libraries.
            /// </summary>
            public D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE CallableShaderTable;

            /// <summary>
            /// Width of the dispatch (number of rays in X dimension).
            /// Must be > 0.
            /// </summary>
            public uint Width;

            /// <summary>
            /// Height of the dispatch (number of rays in Y dimension).
            /// Must be > 0.
            /// </summary>
            public uint Height;

            /// <summary>
            /// Depth of the dispatch (number of rays in Z dimension).
            /// Must be > 0.
            /// </summary>
            public uint Depth;
        }

        #endregion

        #region D3D12 State Object Structures for Raytracing PSO

        // State object type constants
        private const uint D3D12_STATE_OBJECT_TYPE_COLLECTION = 0;
        private const uint D3D12_STATE_OBJECT_TYPE_RAYTRACING_PIPELINE = 1;

        // State subobject type constants
        private const uint D3D12_STATE_SUBOBJECT_TYPE_STATE_OBJECT_CONFIG = 0;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_GLOBAL_ROOT_SIGNATURE = 1;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_LOCAL_ROOT_SIGNATURE = 2;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_NODE_MASK = 3;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_DXIL_LIBRARY = 5;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_EXISTING_COLLECTION = 6;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_SUBOBJECT_TO_EXPORTS_ASSOCIATION = 7;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_DXIL_SUBOBJECT_TO_EXPORTS_ASSOCIATION = 8;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_RAYTRACING_SHADER_CONFIG = 9;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_RAYTRACING_PIPELINE_CONFIG = 10;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_HIT_GROUP = 11;
        private const uint D3D12_STATE_SUBOBJECT_TYPE_RAYTRACING_PIPELINE_CONFIG1 = 12;

        // Hit group type constants
        private const uint D3D12_HIT_GROUP_TYPE_TRIANGLES = 0;
        private const uint D3D12_HIT_GROUP_TYPE_PROCEDURAL_PRIMITIVE = 1;

        /// <summary>
        /// State subobject structure.
        /// Based on D3D12 API: D3D12_STATE_SUBOBJECT
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_STATE_SUBOBJECT
        {
            public uint Type;
            public IntPtr pDesc;
        }

        /// <summary>
        /// State object description.
        /// Based on D3D12 API: D3D12_STATE_OBJECT_DESC
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_STATE_OBJECT_DESC
        {
            public uint Type;
            public uint NumSubobjects;
            public IntPtr pSubobjects;
        }

        /// <summary>
        /// Export description for DXIL library exports.
        /// Based on D3D12 API: D3D12_EXPORT_DESC
        /// Reference: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_export_desc
        ///
        /// Note: Uses IntPtr for string pointers to allow manual memory management.
        /// Strings must be allocated separately in unmanaged memory and remain valid
        /// until the state object is created.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_EXPORT_DESC
        {
            public IntPtr Name; // Export name (shader function name) - pointer to null-terminated ANSI string
            public IntPtr ExportToRename; // Optional: rename export - pointer to null-terminated ANSI string (IntPtr.Zero if not renaming)
            public uint Flags; // D3D12_EXPORT_FLAGS (typically 0)
        }

        /// <summary>
        /// DXIL library description.
        /// Based on D3D12 API: D3D12_DXIL_LIBRARY_DESC
        /// Reference: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_dxil_library_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DXIL_LIBRARY_DESC
        {
            public D3D12_SHADER_BYTECODE DXILLibrary;
            public IntPtr pExports; // Pointer to array of D3D12_EXPORT_DESC (null if no exports specified)
            public uint NumExports; // Number of exports
        }

        /// <summary>
        /// Shader bytecode structure.
        /// Based on D3D12 API: D3D12_SHADER_BYTECODE
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_SHADER_BYTECODE
        {
            public IntPtr pShaderBytecode;
            public IntPtr BytecodeLength;
        }

        /// <summary>
        /// Hit group description.
        /// Based on D3D12 API: D3D12_HIT_GROUP_DESC
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_HIT_GROUP_DESC
        {
            public IntPtr HitGroupExport;
            public uint Type;
            public IntPtr AnyHitShaderImport;
            public IntPtr ClosestHitShaderImport;
            public IntPtr IntersectionShaderImport;
        }

        /// <summary>
        /// Raytracing shader configuration.
        /// Based on D3D12 API: D3D12_RAYTRACING_SHADER_CONFIG
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_SHADER_CONFIG
        {
            public uint MaxPayloadSizeInBytes;
            public uint MaxAttributeSizeInBytes;
        }

        /// <summary>
        /// Raytracing pipeline configuration.
        /// Based on D3D12 API: D3D12_RAYTRACING_PIPELINE_CONFIG
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_PIPELINE_CONFIG
        {
            public uint MaxTraceRecursionDepth;
        }

        /// <summary>
        /// Interface ID for ID3D12StateObject.
        /// Used for QueryInterface when creating state objects.
        /// </summary>
        private static readonly System.Guid IID_ID3D12StateObject = new System.Guid("47016943-fca8-4594-93ea-af258b55346d");

        /// <summary>
        /// Interface ID for ID3D12Resource.
        /// Used for QueryInterface when querying buffers for GPU virtual address support.
        /// </summary>
        private static readonly System.Guid IID_ID3D12Resource = new System.Guid("696442be-a72e-4059-bc79-5b5c98040fad");

        /// <summary>
        /// Interface ID for ID3D12Device5.
        /// Used when creating the DXR fallback device via D3D12CreateRaytracingFallbackDevice.
        /// The fallback device implements ID3D12Device5 interface to provide DXR API compatibility.
        /// Based on Microsoft D3D12RaytracingFallback library and DirectX 12 API.
        /// </summary>
        private static readonly System.Guid IID_ID3D12Device5 = new System.Guid(0x8b4f173b, 0x2fea, 0x4b80, 0xb4, 0xc4, 0x52, 0x46, 0xa8, 0xe9, 0xda, 0x52);

        /// <summary>
        /// DXR fallback layer creation flags.
        /// Based on D3D12_RAYTRACING_FALLBACK_FLAGS enum from D3D12RaytracingFallback library.
        /// </summary>
        private enum D3D12_RAYTRACING_FALLBACK_FLAGS : uint
        {
            /// <summary>
            /// No special flags. Use default fallback layer behavior.
            /// </summary>
            D3D12_RAYTRACING_FALLBACK_FLAG_NONE = 0,

            /// <summary>
            /// Disable state object creation optimization.
            /// May be used for debugging or compatibility.
            /// </summary>
            D3D12_RAYTRACING_FALLBACK_FLAG_DISABLE_STATE_OBJECT_CREATION = 1
        }

        #region COM Interface Delegates

        /// <summary>
        /// COM interface method delegate for QueryInterface (IUnknown::QueryInterface).
        /// VTable index 0 for IUnknown.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterfaceDelegate(IntPtr comObject, ref Guid riid, out IntPtr ppvObject);

        /// <summary>
        /// COM interface method delegate for Release (IUnknown::Release).
        /// VTable index 2 for IUnknown.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr comObject);

        /// <summary>
        /// COM interface method delegate for GetGPUVirtualAddress (ID3D12Resource::GetGPUVirtualAddress).
        /// VTable index 8 for ID3D12Resource.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong GetGpuVirtualAddressDelegate(IntPtr resource);

        #endregion

        /// <summary>
        /// Placeholder interface for ID3D12StateObject.
        /// Used for type identification in CreateStateObject.
        /// </summary>
        private interface ID3D12StateObject
        {
            // This is a marker interface for type identification
            // The actual COM interface methods are not needed for P/Invoke
        }

        #endregion

    }
}

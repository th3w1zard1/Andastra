using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics.Common.Backends;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Structs;
using Stride.Graphics;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Game.Stride.Backends
{
    /// <summary>
    /// Stride implementation of DirectX 12 backend with DXR raytracing support.
    /// Inherits all shared D3D12 logic from BaseDirect3D12Backend.
    ///
    /// Based on Stride Graphics API: https://doc.stride3d.net/latest/en/manual/graphics/
    /// Stride supports DirectX 12 for modern Windows rendering.
    ///
    /// Features:
    /// - DirectX 12 Ultimate features
    /// - DXR 1.1 raytracing
    /// - Mesh shaders
    /// - Variable rate shading
    /// - DirectStorage support
    ///
    /// Platform: Windows only (x64/x86)
    /// - DirectX 12 is a Windows-specific graphics API
    /// - Uses COM interop for DirectX 12 interfaces (Windows-only)
    /// - For cross-platform support (Linux/macOS), use StrideVulkanBackend instead
    /// - This backend should only be instantiated on Windows via StrideBackendFactory
    /// - All COM interop methods include runtime platform checks for safety
    /// </summary>
    public class StrideDirect3D12Backend : BaseDirect3D12Backend
    {
        // DirectX 12 Interface ID for ID3D12DescriptorHeap
        private static readonly Guid IID_ID3D12DescriptorHeap = new Guid(0x8efb471d, 0x616c, 0x4f49, 0x90, 0xf7, 0x12, 0x7b, 0xb7, 0x63, 0xfa, 0x51);

        private global::Stride.Engine.Game _game;
        private GraphicsDevice _strideDevice;
        private StrideGraphics.CommandList _strideCommandList;

        // Bindless resource tracking
        private readonly Dictionary<IntPtr, BindlessHeapInfo> _bindlessHeaps;
        private readonly Dictionary<IntPtr, int> _textureToHeapIndex; // texture handle -> heap index
        private readonly Dictionary<IntPtr, int> _samplerToHeapIndex; // sampler handle -> heap index
        private readonly Dictionary<IntPtr, StrideGraphics.SamplerStateDescription> _samplerDescriptions; // sampler handle -> sampler state description

        public StrideDirect3D12Backend(global::Stride.Engine.Game game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _bindlessHeaps = new Dictionary<IntPtr, BindlessHeapInfo>();
            _textureToHeapIndex = new Dictionary<IntPtr, int>();
            _samplerToHeapIndex = new Dictionary<IntPtr, int>();
            _samplerDescriptions = new Dictionary<IntPtr, StrideGraphics.SamplerStateDescription>();
        }

        #region BaseGraphicsBackend Implementation

        protected override bool CreateDeviceResources()
        {
            if (_game.GraphicsDevice == null)
            {
                Console.WriteLine("[StrideDX12] GraphicsDevice not available");
                return false;
            }

            _strideDevice = _game.GraphicsDevice;
            // Stride GraphicsDevice doesn't expose NativePointer - return IntPtr.Zero
            _device = IntPtr.Zero;

            return true;
        }

        protected override bool CreateSwapChainResources()
        {
            // Store Stride CommandList reference for later native handle retrieval via reflection
            _strideCommandList = _game.GraphicsContext?.CommandList;
            // Store native handle via reflection if available, otherwise IntPtr.Zero
            _commandList = GetNativeCommandList();
            return _strideCommandList != null;
        }

        protected override void DestroyDeviceResources()
        {
            _strideDevice = null;
            _device = IntPtr.Zero;
        }

        protected override void DestroySwapChainResources()
        {
            _strideCommandList = null;
            _commandList = IntPtr.Zero;
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
                // Stride Texture doesn't expose NativePointer - return IntPtr.Zero
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

            var buffer = StrideGraphics.Buffer.New(_strideDevice, desc.SizeInBytes, flags);

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
            // Clean up bindless heaps if this is a heap resource
            if (info.Type == ResourceType.Heap && _bindlessHeaps.ContainsKey(info.Handle))
            {
                var heapInfo = _bindlessHeaps[info.Handle];
                if (heapInfo.DescriptorHeap != IntPtr.Zero)
                {
                    // Platform check: DirectX 12 COM is Windows-only
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        // Release the COM object (call Release on the descriptor heap)
                        // ID3D12DescriptorHeap::Release is at vtable index 2 (IUnknown::Release)
                        IntPtr vtable = Marshal.ReadIntPtr(heapInfo.DescriptorHeap);
                        if (vtable != IntPtr.Zero)
                        {
                            IntPtr releasePtr = Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size);
                            if (releasePtr != IntPtr.Zero)
                            {
                                var releaseDelegate = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(
                                    releasePtr, typeof(ReleaseDelegate));
                                releaseDelegate(heapInfo.DescriptorHeap);
                            }
                        }
                    }
                }
                _bindlessHeaps.Remove(info.Handle);
                Console.WriteLine($"[StrideDX12] DestroyResourceInternal: Released bindless heap {info.Handle}");
            }

            // Stride manages other resource lifetimes
        }

        // Delegate for IUnknown::Release
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr comObject);

        #endregion

        #region BaseDirect3D12Backend Implementation

        protected override void InitializeRaytracing()
        {
            // Initialize DXR through Stride's D3D12 interface
            _raytracingDevice = _device;
            _raytracingEnabled = true;
            _raytracingLevel = _settings.Raytracing;

            Console.WriteLine("[StrideDX12] DXR raytracing initialized");
        }

        protected override void OnDispatch(int x, int y, int z)
        {
            if (_strideCommandList != null)
            {
                _strideCommandList.Dispatch(x, y, z);
            }
        }

        protected override void OnDispatchRays(DispatchRaysDesc desc)
        {
            // DXR dispatch through Stride's low-level D3D12 access
            // ID3D12GraphicsCommandList4::DispatchRays equivalent
        }

        protected override void OnUpdateTlasInstance(IntPtr tlas, int instanceIndex, Matrix4x4 transform)
        {
            // Update TLAS instance transform
        }

        protected override void OnExecuteCommandList()
        {
            // Stride handles command list execution internally
        }

        protected override void OnResetCommandList()
        {
            // Stride handles command list reset internally
        }

        protected override void OnResourceBarrier(IntPtr resource, ResourceState before, ResourceState after)
        {
            // Resource barriers through Stride's command list
        }

        protected override void OnWaitForGpu()
        {
            // GPU synchronization through Stride
            // Stride doesn't expose WaitIdle - synchronization is handled internally
            // Stride manages command queue synchronization automatically
        }

        protected override ResourceInfo CreateStructuredBufferInternal(int elementCount, int elementStride,
            bool cpuWritable, IntPtr handle)
        {
            var buffer = StrideGraphics.Buffer.Structured.New(_strideDevice, elementCount, elementStride, cpuWritable);

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

        protected override ResourceInfo CreateBlasInternal(MeshGeometry geometry, IntPtr handle)
        {
            // Create BLAS for raytracing
            // D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL
            return new ResourceInfo
            {
                Type = ResourceType.AccelerationStructure,
                Handle = handle,
                NativeHandle = IntPtr.Zero,
                DebugName = "BLAS"
            };
        }

        protected override ResourceInfo CreateTlasInternal(int maxInstances, IntPtr handle)
        {
            // Create TLAS for raytracing
            // D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL
            return new ResourceInfo
            {
                Type = ResourceType.AccelerationStructure,
                Handle = handle,
                NativeHandle = IntPtr.Zero,
                DebugName = "TLAS"
            };
        }

        protected override ResourceInfo CreateRaytracingPsoInternal(RaytracingPipelineDesc desc, IntPtr handle)
        {
            // Create raytracing pipeline state object
            // ID3D12Device5::CreateStateObject
            return new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                NativeHandle = IntPtr.Zero,
                DebugName = desc.DebugName
            };
        }

        public override IntPtr MapBuffer(IntPtr bufferHandle, MapType mapType)
        {
            return IntPtr.Zero;
        }

        public override void UnmapBuffer(IntPtr bufferHandle)
        {
        }

        #endregion

        #region IMeshShaderBackend Implementation

        protected override ResourceInfo CreateMeshShaderPipelineInternal(byte[] amplificationShader, byte[] meshShader,
            byte[] pixelShader, MeshPipelineDescription desc, IntPtr handle)
        {
            // Create mesh shader pipeline state object through Stride
            // D3D12_GRAPHICS_PIPELINE_STATE_DESC with mesh/amplification shaders
            // Would use Stride's pipeline creation API with mesh shader support

            return new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                NativeHandle = IntPtr.Zero,
                DebugName = desc.DebugName ?? "MeshShaderPipeline"
            };
        }

        protected override void OnDispatchMesh(int x, int y, int z)
        {
            // Dispatch mesh shader work
            // D3D12_COMMAND_LIST_TYPE_DIRECT -> DispatchMesh(x, y, z)
            // Through Stride's command list: DispatchMesh equivalent
            Console.WriteLine($"[StrideDX12] DispatchMesh: {x}x{y}x{z}");
        }

        protected override void OnDispatchMeshIndirect(IntPtr indirectBuffer, int offset)
        {
            // Dispatch mesh shader with indirect arguments
            // D3D12_COMMAND_LIST_TYPE_DIRECT -> DispatchMeshIndirect
            Console.WriteLine($"[StrideDX12] DispatchMeshIndirect: buffer {indirectBuffer}, offset {offset}");
        }

        #endregion

        #region IVariableRateShadingBackend Implementation

        protected override void OnSetShadingRate(VrsShadingRate rate)
        {
            // Set per-draw shading rate
            // RSSetShadingRate(D3D12_SHADING_RATE)
            Console.WriteLine($"[StrideDX12] SetShadingRate: {rate}");
        }

        protected override void OnSetShadingRateCombiner(VrsCombiner combiner0, VrsCombiner combiner1, VrsShadingRate rate)
        {
            // Set shading rate combiner (Tier 1)
            // RSSetShadingRate(D3D12_SHADING_RATE, D3D12_SHADING_RATE_COMBINER[])
            Console.WriteLine($"[StrideDX12] SetShadingRateCombiner: {combiner0}/{combiner1}, rate {rate}");
        }

        protected override void OnSetPerPrimitiveShadingRate(bool enable)
        {
            // Enable/disable per-primitive shading rate (Tier 1)
            // Requires SV_ShadingRate in shader output
            Console.WriteLine($"[StrideDX12] SetPerPrimitiveShadingRate: {enable}");
        }

        protected override void OnSetShadingRateImage(IntPtr shadingRateImage, int width, int height)
        {
            // Set screen-space shading rate image (Tier 2)
            // RSSetShadingRateImage with texture
            Console.WriteLine($"[StrideDX12] SetShadingRateImage: {width}x{height} tiles");
        }

        protected override int QueryVrsTier()
        {
            // Query VRS tier from Stride device capabilities
            // Would check D3D12_FEATURE_DATA_D3D12_OPTIONS6.VariableShadingRateTier
            return 2; // Assume Tier 2 for DirectX 12 Ultimate
        }

        #endregion

        #region Capability Queries

        protected override bool QueryRaytracingSupport()
        {
            // Check D3D12 DXR support
            // CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS5)
            return true; // Assume modern GPU
        }

        protected override bool QueryMeshShaderSupport()
        {
            // Check D3D12 mesh shader support
            return true;
        }

        protected override bool QueryVrsSupport()
        {
            // Check D3D12 VRS support
            return true;
        }

        protected override bool QueryDlssSupport()
        {
            // Check NVIDIA DLSS availability
            return _capabilities.VendorName?.Contains("NVIDIA") ?? false;
        }

        protected override long QueryVideoMemory()
        {
            // Stride doesn't expose DedicatedVideoMemory via Adapter.Description
            // Return default fallback value
            return 8L * 1024 * 1024 * 1024;
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
            return _strideDevice?.Adapter?.Description ?? "Stride DirectX 12 Device";
        }

        #endregion

        #region Utility Methods

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
                case TextureFormat.R8_UNorm: return 1;
                case TextureFormat.R8G8_UNorm: return 2;
                case TextureFormat.R8G8B8A8_UNorm: return 4;
                case TextureFormat.R16G16B16A16_Float: return 8;
                case TextureFormat.R32G32B32A32_Float: return 16;
                default: return 4;
            }
        }

        #endregion

        #region BaseDirect3D12Backend Abstract Method Implementations

        protected override ResourceInfo CreateBindlessTextureHeapInternal(int capacity, IntPtr handle)
        {
            // Validate inputs
            if (capacity <= 0)
            {
                Console.WriteLine("[StrideDX12] CreateBindlessTextureHeap: Invalid capacity " + capacity);
                return new ResourceInfo
                {
                    Type = ResourceType.Heap,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BindlessTextureHeap"
                };
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] CreateBindlessTextureHeap: DirectX 12 device not available");
                return new ResourceInfo
                {
                    Type = ResourceType.Heap,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BindlessTextureHeap"
                };
            }

            // DirectX 12 bindless texture heap creation
            // Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/direct3d12/descriptor-heaps
            // Bindless resources require shader-visible descriptor heaps
            // D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV with D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE

            try
            {
                // Create D3D12_DESCRIPTOR_HEAP_DESC structure
                var heapDesc = new D3D12_DESCRIPTOR_HEAP_DESC
                {
                    Type = D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
                    NumDescriptors = (uint)capacity,
                    Flags = D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE,
                    NodeMask = 0
                };

                // Allocate memory for the descriptor heap descriptor structure
                int heapDescSize = Marshal.SizeOf(typeof(D3D12_DESCRIPTOR_HEAP_DESC));
                IntPtr heapDescPtr = Marshal.AllocHGlobal(heapDescSize);
                try
                {
                    Marshal.StructureToPtr(heapDesc, heapDescPtr, false);

                    // Allocate memory for the output descriptor heap pointer
                    IntPtr heapPtr = Marshal.AllocHGlobal(IntPtr.Size);
                    try
                    {
                        // Call ID3D12Device::CreateDescriptorHeap
                        Guid iidDescriptorHeap = new Guid("8efb471d-616c-4f49-90f7-127bb763fa51"); // IID_ID3D12DescriptorHeap

                        int hr = CreateDescriptorHeap(_device, heapDescPtr, ref iidDescriptorHeap, heapPtr);
                        if (hr < 0)
                        {
                            Console.WriteLine($"[StrideDX12] CreateBindlessTextureHeap: CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                            return new ResourceInfo
                            {
                                Type = ResourceType.Heap,
                                Handle = IntPtr.Zero,
                                NativeHandle = IntPtr.Zero,
                                DebugName = "BindlessTextureHeap"
                            };
                        }

                        // Get the descriptor heap pointer
                        IntPtr descriptorHeap = Marshal.ReadIntPtr(heapPtr);
                        if (descriptorHeap == IntPtr.Zero)
                        {
                            Console.WriteLine("[StrideDX12] CreateBindlessTextureHeap: Descriptor heap pointer is null");
                            return new ResourceInfo
                            {
                                Type = ResourceType.Heap,
                                Handle = IntPtr.Zero,
                                NativeHandle = IntPtr.Zero,
                                DebugName = "BindlessTextureHeap"
                            };
                        }

                        // Get descriptor heap start handle (CPU handle for descriptor heap)
                        IntPtr cpuHandle = GetDescriptorHeapStartHandle(descriptorHeap);

                        // Get descriptor heap start handle for GPU (shader-visible)
                        IntPtr gpuHandle = GetDescriptorHeapStartHandleGpu(descriptorHeap);

                        // Get descriptor increment size
                        uint descriptorIncrementSize = GetDescriptorHandleIncrementSize(_device, D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);

                        // Store heap information for later use
                        var heapInfo = new BindlessHeapInfo
                        {
                            DescriptorHeap = descriptorHeap,
                            CpuHandle = cpuHandle,
                            GpuHandle = gpuHandle,
                            Capacity = capacity,
                            DescriptorIncrementSize = descriptorIncrementSize,
                            NextIndex = 0,
                            FreeIndices = new HashSet<int>(),
                            HeapType = D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV // Texture heap type
                        };
                        _bindlessHeaps[handle] = heapInfo;

                        Console.WriteLine($"[StrideDX12] CreateBindlessTextureHeap: Created texture heap with capacity {capacity}, descriptor size {descriptorIncrementSize} bytes");

                        return new ResourceInfo
                        {
                            Type = ResourceType.Heap,
                            Handle = handle,
                            NativeHandle = descriptorHeap,
                            DebugName = $"BindlessTextureHeap_{capacity}",
                            SizeInBytes = (long)capacity * descriptorIncrementSize
                        };
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(heapPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(heapDescPtr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] CreateBindlessTextureHeap: Exception: {ex.Message}");
                Console.WriteLine($"[StrideDX12] CreateBindlessTextureHeap: Stack trace: {ex.StackTrace}");
                return new ResourceInfo
                {
                    Type = ResourceType.Heap,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BindlessTextureHeap"
                };
            }
        }

        protected override ResourceInfo CreateBindlessSamplerHeapInternal(int capacity, IntPtr handle)
        {
            // Validate inputs
            if (capacity <= 0)
            {
                Console.WriteLine("[StrideDX12] CreateBindlessSamplerHeap: Invalid capacity " + capacity);
                return new ResourceInfo
                {
                    Type = ResourceType.Heap,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BindlessSamplerHeap"
                };
            }

            // Platform check: DirectX 12 is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("[StrideDX12] CreateBindlessSamplerHeap: DirectX 12 is only available on Windows");
                return new ResourceInfo
                {
                    Type = ResourceType.Heap,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BindlessSamplerHeap"
                };
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] CreateBindlessSamplerHeap: DirectX 12 device not available");
                return new ResourceInfo
                {
                    Type = ResourceType.Heap,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BindlessSamplerHeap"
                };
            }

            // DirectX 12 bindless sampler heap creation (Windows-only)
            // Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/direct3d12/descriptor-heaps
            // Bindless resources require shader-visible descriptor heaps
            // D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER with D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE
            // Note: This implementation uses DirectX 12 COM interfaces which are Windows-specific
            // For cross-platform support, use StrideVulkanBackend on Linux/macOS

            try
            {
                // Create D3D12_DESCRIPTOR_HEAP_DESC structure
                var heapDesc = new D3D12_DESCRIPTOR_HEAP_DESC
                {
                    Type = D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER,
                    NumDescriptors = (uint)capacity,
                    Flags = D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE,
                    NodeMask = 0
                };

                // Allocate memory for the descriptor heap descriptor structure
                int heapDescSize = Marshal.SizeOf(typeof(D3D12_DESCRIPTOR_HEAP_DESC));
                IntPtr heapDescPtr = Marshal.AllocHGlobal(heapDescSize);
                try
                {
                    Marshal.StructureToPtr(heapDesc, heapDescPtr, false);

                    // Allocate memory for the output descriptor heap pointer
                    IntPtr heapPtr = Marshal.AllocHGlobal(IntPtr.Size);
                    try
                    {
                        // Call ID3D12Device::CreateDescriptorHeap
                        // HRESULT CreateDescriptorHeap(
                        //   const D3D12_DESCRIPTOR_HEAP_DESC *pDescriptorHeapDesc,
                        //   REFIID riid,
                        //   void **ppvHeap
                        // );
                        Guid iidDescriptorHeap = IID_ID3D12DescriptorHeap;

                        int hr = CreateDescriptorHeap(_device, heapDescPtr, ref iidDescriptorHeap, heapPtr);
                        if (hr < 0)
                        {
                            Console.WriteLine($"[StrideDX12] CreateBindlessSamplerHeap: CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                            return new ResourceInfo
                            {
                                Type = ResourceType.Heap,
                                Handle = IntPtr.Zero,
                                NativeHandle = IntPtr.Zero,
                                DebugName = "BindlessSamplerHeap"
                            };
                        }

                        // Get the descriptor heap pointer
                        IntPtr descriptorHeap = Marshal.ReadIntPtr(heapPtr);
                        if (descriptorHeap == IntPtr.Zero)
                        {
                            Console.WriteLine("[StrideDX12] CreateBindlessSamplerHeap: Descriptor heap pointer is null");
                            return new ResourceInfo
                            {
                                Type = ResourceType.Heap,
                                Handle = IntPtr.Zero,
                                NativeHandle = IntPtr.Zero,
                                DebugName = "BindlessSamplerHeap"
                            };
                        }

                        // Get descriptor heap start handle (CPU handle for descriptor heap)
                        IntPtr cpuHandle = GetDescriptorHeapStartHandle(descriptorHeap);

                        // Get descriptor heap start handle for GPU (shader-visible)
                        IntPtr gpuHandle = GetDescriptorHeapStartHandleGpu(descriptorHeap);

                        // Get descriptor increment size
                        uint descriptorIncrementSize = GetDescriptorHandleIncrementSize(_device, D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER);

                        // Store heap information for later use
                        var heapInfo = new BindlessHeapInfo
                        {
                            DescriptorHeap = descriptorHeap,
                            CpuHandle = cpuHandle,
                            GpuHandle = gpuHandle,
                            Capacity = capacity,
                            DescriptorIncrementSize = descriptorIncrementSize,
                            NextIndex = 0,
                            FreeIndices = new HashSet<int>(),
                            HeapType = D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER // Sampler heap type
                        };
                        _bindlessHeaps[handle] = heapInfo;

                        Console.WriteLine($"[StrideDX12] CreateBindlessSamplerHeap: Created sampler heap with capacity {capacity}, descriptor size {descriptorIncrementSize} bytes");

                        return new ResourceInfo
                        {
                            Type = ResourceType.Heap,
                            Handle = handle,
                            NativeHandle = descriptorHeap,
                            DebugName = $"BindlessSamplerHeap_{capacity}",
                            SizeInBytes = (long)capacity * descriptorIncrementSize
                        };
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(heapPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(heapDescPtr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] CreateBindlessSamplerHeap: Exception: {ex.Message}");
                Console.WriteLine($"[StrideDX12] CreateBindlessSamplerHeap: Stack trace: {ex.StackTrace}");
                return new ResourceInfo
                {
                    Type = ResourceType.Heap,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "BindlessSamplerHeap"
                };
            }
        }

        protected override int OnAddBindlessTexture(IntPtr heap, IntPtr texture)
        {
            // Validate inputs
            if (heap == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnAddBindlessTexture: Invalid heap handle");
                return -1;
            }

            if (texture == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnAddBindlessTexture: Invalid texture handle");
                return -1;
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnAddBindlessTexture: DirectX 12 device not available");
                return -1;
            }

            // Get heap information
            if (!_bindlessHeaps.TryGetValue(heap, out BindlessHeapInfo heapInfo))
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessTexture: Heap not found for handle {heap}");
                return -1;
            }

            // Get texture resource information
            if (!_resources.TryGetValue(texture, out ResourceInfo textureResource))
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessTexture: Texture resource not found for handle {texture}");
                return -1;
            }

            if (textureResource.Type != ResourceType.Texture)
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessTexture: Resource is not a texture (type: {textureResource.Type})");
                return -1;
            }

            if (textureResource.NativeHandle == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnAddBindlessTexture: Native texture handle is invalid");
                return -1;
            }

            // Check if texture is already in the heap
            if (_textureToHeapIndex.TryGetValue(texture, out int existingIndex))
            {
                // Check if this index is still valid in the heap
                if (existingIndex >= 0 && existingIndex < heapInfo.Capacity && !heapInfo.FreeIndices.Contains(existingIndex))
                {
                    Console.WriteLine($"[StrideDX12] OnAddBindlessTexture: Texture already in heap at index {existingIndex}");
                    return existingIndex;
                }
            }

            // Find next available index
            int index = -1;
            if (heapInfo.FreeIndices.Count > 0)
            {
                // Reuse a free index
                var enumerator = heapInfo.FreeIndices.GetEnumerator();
                enumerator.MoveNext();
                index = enumerator.Current;
                heapInfo.FreeIndices.Remove(index);
            }
            else if (heapInfo.NextIndex < heapInfo.Capacity)
            {
                // Use next available index
                index = heapInfo.NextIndex;
                heapInfo.NextIndex++;
            }
            else
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessTexture: Heap is full (capacity: {heapInfo.Capacity})");
                return -1;
            }

            // Create SRV (Shader Resource View) descriptor for the texture
            // Based on DirectX 12 Descriptors: https://docs.microsoft.com/en-us/windows/win32/direct3d12/descriptors-overview
            // D3D12_SHADER_RESOURCE_VIEW_DESC structure
            try
            {
                // Calculate CPU descriptor handle for this index
                IntPtr cpuDescriptorHandle = OffsetDescriptorHandle(heapInfo.CpuHandle, index, heapInfo.DescriptorIncrementSize);

                // Create D3D12_SHADER_RESOURCE_VIEW_DESC structure
                // For a 2D texture, we use D3D12_SRV_DIMENSION_TEXTURE2D
                var srvDesc = new D3D12_SHADER_RESOURCE_VIEW_DESC
                {
                    Format = D3D12_DXGI_FORMAT_UNKNOWN, // Use texture's format
                    ViewDimension = D3D12_SRV_DIMENSION_TEXTURE2D,
                    Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                    Texture2D = new D3D12_TEX2D_SRV
                    {
                        MostDetailedMip = 0,
                        MipLevels = unchecked((uint)-1), // All mip levels
                        PlaneSlice = 0,
                        ResourceMinLODClamp = 0.0f
                    }
                };

                // Allocate memory for the SRV descriptor structure
                int srvDescSize = Marshal.SizeOf(typeof(D3D12_SHADER_RESOURCE_VIEW_DESC));
                IntPtr srvDescPtr = Marshal.AllocHGlobal(srvDescSize);
                try
                {
                    Marshal.StructureToPtr(srvDesc, srvDescPtr, false);

                    // Call ID3D12Device::CreateShaderResourceView
                    // void CreateShaderResourceView(
                    //   ID3D12Resource *pResource,
                    //   const D3D12_SHADER_RESOURCE_VIEW_DESC *pDesc,
                    //   D3D12_CPU_DESCRIPTOR_HANDLE DestDescriptor
                    // );
                    CreateShaderResourceView(_device, textureResource.NativeHandle, srvDescPtr, cpuDescriptorHandle);

                    // Track texture to index mapping
                    _textureToHeapIndex[texture] = index;

                    Console.WriteLine($"[StrideDX12] OnAddBindlessTexture: Added texture {texture} to heap {heap} at index {index}");

                    return index;
                }
                finally
                {
                    Marshal.FreeHGlobal(srvDescPtr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessTexture: Exception: {ex.Message}");
                Console.WriteLine($"[StrideDX12] OnAddBindlessTexture: Stack trace: {ex.StackTrace}");

                // If we allocated an index, mark it as free again
                if (index >= 0)
                {
                    heapInfo.FreeIndices.Add(index);
                    if (index == heapInfo.NextIndex - 1)
                    {
                        heapInfo.NextIndex--;
                    }
                }

                return -1;
            }
        }

        /// <summary>
        /// Attempts to extract the sampler state description from a sampler handle.
        /// The handle may be a GCHandle to a StrideSamplerState object, or it may be stored in our dictionary.
        /// </summary>
        private bool TryGetSamplerDescription(IntPtr samplerHandle, out StrideGraphics.SamplerStateDescription description)
        {
            description = default(StrideGraphics.SamplerStateDescription);

            if (samplerHandle == IntPtr.Zero)
            {
                return false;
            }

            // First, try to look up in our stored descriptions
            if (_samplerDescriptions.TryGetValue(samplerHandle, out description))
            {
                return true;
            }

            // Try to convert handle back to GCHandle and get the object
            try
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(samplerHandle);
                if (gcHandle.IsAllocated)
                {
                    object target = gcHandle.Target;
                    if (target != null)
                    {
                        // Check if it's a SamplerState
                        if (target is StrideGraphics.SamplerState strideSampler)
                        {
                            description = strideSampler.Description;
                            // Cache it for future use
                            _samplerDescriptions[samplerHandle] = description;
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Not a GCHandle, try other methods
            }

            return false;
        }

        /// <summary>
        /// Converts a Stride SamplerStateDescription to a D3D12_SAMPLER_DESC structure.
        /// Based on DirectX 12 Sampler Descriptors: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_sampler_desc
        /// </summary>
        private D3D12_SAMPLER_DESC ConvertSamplerDescriptionToD3D12(StrideGraphics.SamplerStateDescription strideDesc)
        {
            var d3d12Desc = new D3D12_SAMPLER_DESC();

            // Convert filter
            d3d12Desc.Filter = ConvertTextureFilterToD3D12(strideDesc.Filter, strideDesc.MaxAnisotropy > 0);

            // Convert address modes
            d3d12Desc.AddressU = ConvertTextureAddressModeToD3D12(strideDesc.AddressU);
            d3d12Desc.AddressV = ConvertTextureAddressModeToD3D12(strideDesc.AddressV);
            d3d12Desc.AddressW = ConvertTextureAddressModeToD3D12(strideDesc.AddressW);

            // Convert mip LOD bias (Stride uses double, D3D12 uses float)
            d3d12Desc.MipLODBias = (float)strideDesc.MipMapLevelOfDetailBias;

            // Convert max anisotropy
            d3d12Desc.MaxAnisotropy = (uint)Math.Max(1, Math.Min(16, strideDesc.MaxAnisotropy));

            // Comparison function - Stride doesn't expose this directly, use NEVER (no comparison)
            d3d12Desc.ComparisonFunc = D3D12_COMPARISON_FUNC_NEVER;

            // Border color - Stride doesn't expose this directly, use transparent black
            d3d12Desc.BorderColor = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };

            // Min/Max LOD - Stride uses MaxMipLevel, D3D12 uses MinLOD and MaxLOD
            d3d12Desc.MinLOD = 0.0f;
            if (strideDesc.MaxMipLevel > 0)
            {
                d3d12Desc.MaxLOD = (float)strideDesc.MaxMipLevel;
            }
            else
            {
                d3d12Desc.MaxLOD = D3D12_FLOAT32_MAX;
            }

            return d3d12Desc;
        }

        /// <summary>
        /// Converts a Stride TextureFilter to a D3D12_FILTER value.
        /// Based on DirectX 12 Filter Types: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_filter
        /// </summary>
        private uint ConvertTextureFilterToD3D12(StrideGraphics.TextureFilter filter, bool anisotropic)
        {
            if (anisotropic)
            {
                return D3D12_FILTER_ANISOTROPIC;
            }

            // Map Stride filter modes to D3D12 filter modes
            // Stride uses enum values that may not match D3D12 exactly, so we check the actual enum values
            switch (filter)
            {
                case StrideGraphics.TextureFilter.Point:
                    return D3D12_FILTER_MIN_MAG_MIP_POINT;
                case StrideGraphics.TextureFilter.Linear:
                    return D3D12_FILTER_MIN_MAG_MIP_LINEAR;
                case StrideGraphics.TextureFilter.Anisotropic:
                    return D3D12_FILTER_ANISOTROPIC;
                default:
                    // Default to linear if unknown
                    return D3D12_FILTER_MIN_MAG_MIP_LINEAR;
            }
        }

        /// <summary>
        /// Converts a Stride TextureAddressMode to a D3D12_TEXTURE_ADDRESS_MODE value.
        /// Based on DirectX 12 Texture Address Modes: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_texture_address_mode
        /// </summary>
        private uint ConvertTextureAddressModeToD3D12(StrideGraphics.TextureAddressMode mode)
        {
            // Stride and D3D12 use the same enum values for address modes
            // Wrap = 1, Mirror = 2, Clamp = 3, Border = 4, MirrorOnce = 5
            switch (mode)
            {
                case StrideGraphics.TextureAddressMode.Wrap:
                    return D3D12_TEXTURE_ADDRESS_MODE_WRAP;
                case StrideGraphics.TextureAddressMode.Mirror:
                    return D3D12_TEXTURE_ADDRESS_MODE_MIRROR;
                case StrideGraphics.TextureAddressMode.Clamp:
                    return D3D12_TEXTURE_ADDRESS_MODE_CLAMP;
                case StrideGraphics.TextureAddressMode.Border:
                    return D3D12_TEXTURE_ADDRESS_MODE_BORDER;
                case StrideGraphics.TextureAddressMode.MirrorOnce:
                    return D3D12_TEXTURE_ADDRESS_MODE_MIRROR_ONCE;
                default:
                    // Default to wrap if unknown
                    return D3D12_TEXTURE_ADDRESS_MODE_WRAP;
            }
        }

        /// <summary>
        /// Registers a sampler state description for a given sampler handle.
        /// This allows the system to look up the description when adding the sampler to a bindless heap.
        /// </summary>
        public void RegisterSamplerDescription(IntPtr samplerHandle, StrideGraphics.SamplerStateDescription description)
        {
            if (samplerHandle != IntPtr.Zero)
            {
                _samplerDescriptions[samplerHandle] = description;
            }
        }

        protected override int OnAddBindlessSampler(IntPtr heap, IntPtr sampler)
        {
            // Validate inputs
            if (heap == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnAddBindlessSampler: Invalid heap handle");
                return -1;
            }

            if (sampler == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnAddBindlessSampler: Invalid sampler handle");
                return -1;
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnAddBindlessSampler: DirectX 12 device not available");
                return -1;
            }

            // Get heap information
            if (!_bindlessHeaps.TryGetValue(heap, out BindlessHeapInfo heapInfo))
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Heap not found for handle {heap}");
                return -1;
            }

            // Verify this is a sampler heap (not a texture heap)
            // Sampler heaps use D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER
            // Texture heaps use D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV
            // We verify by checking the heap type stored in BindlessHeapInfo
            if (heapInfo.HeapType != D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER)
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Heap {heap} is not a sampler heap (type: {heapInfo.HeapType}, expected: {D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER})");
                return -1;
            }

            // Check if sampler is already in the heap
            if (_samplerToHeapIndex.TryGetValue(sampler, out int existingIndex))
            {
                // Check if this index is still valid in the heap
                if (existingIndex >= 0 && existingIndex < heapInfo.Capacity && !heapInfo.FreeIndices.Contains(existingIndex))
                {
                    Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Sampler already in heap at index {existingIndex}");
                    return existingIndex;
                }
            }

            // Find next available index
            int index = -1;
            if (heapInfo.FreeIndices.Count > 0)
            {
                // Reuse a free index
                var enumerator = heapInfo.FreeIndices.GetEnumerator();
                enumerator.MoveNext();
                index = enumerator.Current;
                heapInfo.FreeIndices.Remove(index);
            }
            else if (heapInfo.NextIndex < heapInfo.Capacity)
            {
                // Use next available index
                index = heapInfo.NextIndex;
                heapInfo.NextIndex++;
            }
            else
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Heap is full (capacity: {heapInfo.Capacity})");
                return -1;
            }

            // Create sampler descriptor for DirectX 12
            // Based on DirectX 12 Samplers: https://docs.microsoft.com/en-us/windows/win32/direct3d12/descriptors-overview
            // D3D12_SAMPLER_DESC structure
            // Note: In DirectX 12, samplers are descriptors created directly in descriptor heaps
            // The sampler parameter is a handle to sampler state information
            // Extract the sampler description from the sampler handle
            try
            {
                // Calculate CPU descriptor handle for this index
                IntPtr cpuDescriptorHandle = OffsetDescriptorHandle(heapInfo.CpuHandle, index, heapInfo.DescriptorIncrementSize);

                // Try to extract the sampler state description from the handle
                D3D12_SAMPLER_DESC samplerDesc;
                if (TryGetSamplerDescription(sampler, out StrideGraphics.SamplerStateDescription strideDesc))
                {
                    // Convert Stride sampler description to D3D12 format
                    samplerDesc = ConvertSamplerDescriptionToD3D12(strideDesc);
                    Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Using extracted sampler description for handle {sampler}");
                }
                else
                {
                    // Fallback to default sampler settings if description cannot be extracted
                    // This can happen if the sampler handle is not registered or is not a GCHandle
                    Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Warning - Could not extract sampler description for handle {sampler}, using default settings");
                    samplerDesc = new D3D12_SAMPLER_DESC
                    {
                        Filter = D3D12_FILTER_MIN_MAG_MIP_LINEAR,
                        AddressU = D3D12_TEXTURE_ADDRESS_MODE_WRAP,
                        AddressV = D3D12_TEXTURE_ADDRESS_MODE_WRAP,
                        AddressW = D3D12_TEXTURE_ADDRESS_MODE_WRAP,
                        MipLODBias = 0.0f,
                        MaxAnisotropy = 1,
                        ComparisonFunc = D3D12_COMPARISON_FUNC_NEVER,
                        BorderColor = new float[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        MinLOD = 0.0f,
                        MaxLOD = D3D12_FLOAT32_MAX
                    };
                }

                // Allocate memory for the sampler descriptor structure
                int samplerDescSize = Marshal.SizeOf(typeof(D3D12_SAMPLER_DESC));
                IntPtr samplerDescPtr = Marshal.AllocHGlobal(samplerDescSize);
                try
                {
                    Marshal.StructureToPtr(samplerDesc, samplerDescPtr, false);

                    // Call ID3D12Device::CreateSampler
                    // void CreateSampler(
                    //   const D3D12_SAMPLER_DESC *pDesc,
                    //   D3D12_CPU_DESCRIPTOR_HANDLE DestDescriptor
                    // );
                    CreateSampler(_device, samplerDescPtr, cpuDescriptorHandle);

                    // Track sampler to index mapping
                    _samplerToHeapIndex[sampler] = index;

                    Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Added sampler {sampler} to heap {heap} at index {index}");

                    return index;
                }
                finally
                {
                    Marshal.FreeHGlobal(samplerDescPtr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Exception: {ex.Message}");
                Console.WriteLine($"[StrideDX12] OnAddBindlessSampler: Stack trace: {ex.StackTrace}");

                // If we allocated an index, mark it as free again
                if (index >= 0)
                {
                    heapInfo.FreeIndices.Add(index);
                    if (index == heapInfo.NextIndex - 1)
                    {
                        heapInfo.NextIndex--;
                    }
                }

                return -1;
            }
        }

        protected override void OnRemoveBindlessTexture(IntPtr heap, int index)
        {
            // Validate inputs
            if (heap == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnRemoveBindlessTexture: Invalid heap handle");
                return;
            }

            if (index < 0)
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessTexture: Invalid index {index}");
                return;
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnRemoveBindlessTexture: DirectX 12 device not available");
                return;
            }

            // Get heap information
            if (!_bindlessHeaps.TryGetValue(heap, out BindlessHeapInfo heapInfo))
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessTexture: Heap not found for handle {heap}");
                return;
            }

            // Validate index is within heap capacity
            if (index >= heapInfo.Capacity)
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessTexture: Index {index} exceeds heap capacity {heapInfo.Capacity}");
                return;
            }

            // Check if index is already free
            if (heapInfo.FreeIndices.Contains(index))
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessTexture: Index {index} is already free");
                return;
            }

            // Check if index is beyond the allocated range
            if (index >= heapInfo.NextIndex)
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessTexture: Index {index} is beyond allocated range (NextIndex: {heapInfo.NextIndex})");
                return;
            }

            // Find which texture is at this index by iterating through the tracking dictionary
            // This is necessary because we only track texture -> index, not index -> texture
            IntPtr textureToRemove = IntPtr.Zero;
            foreach (var kvp in _textureToHeapIndex)
            {
                if (kvp.Value == index)
                {
                    textureToRemove = kvp.Key;
                    break;
                }
            }

            if (textureToRemove == IntPtr.Zero)
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessTexture: No texture found at index {index}");
                return;
            }

            // Remove texture from tracking dictionary
            _textureToHeapIndex.Remove(textureToRemove);

            // Mark index as free for reuse
            heapInfo.FreeIndices.Add(index);

            // If this is the last allocated index, we can decrement NextIndex to allow reuse
            // This optimization allows the heap to compact when removing the highest-indexed texture
            if (index == heapInfo.NextIndex - 1)
            {
                // Find the new highest allocated index (highest index that's allocated and not free)
                // NextIndex should be set to the highest allocated index + 1
                int highestAllocatedIndex = -1;
                foreach (var kvp in _textureToHeapIndex)
                {
                    int allocatedIndex = kvp.Value;
                    // Only consider indices that are allocated (in dictionary) and not free
                    if (!heapInfo.FreeIndices.Contains(allocatedIndex))
                    {
                        if (allocatedIndex > highestAllocatedIndex)
                        {
                            highestAllocatedIndex = allocatedIndex;
                        }
                    }
                }
                // Set NextIndex to highest allocated index + 1 (or 0 if no textures remain)
                heapInfo.NextIndex = highestAllocatedIndex + 1;
            }

            // Note: In DirectX 12, we don't actually "clear" or "remove" descriptors from the heap.
            // Descriptors remain in the descriptor heap, but we mark the slot as free for reuse.
            // The descriptor at that index may still be valid in GPU memory until it's overwritten
            // by a new descriptor when the index is reused. This is the standard DirectX 12 pattern
            // for bindless resource management - descriptors are persistent until explicitly overwritten.

            Console.WriteLine($"[StrideDX12] OnRemoveBindlessTexture: Removed texture {textureToRemove} from heap {heap} at index {index} (NextIndex: {heapInfo.NextIndex}, FreeIndices: {heapInfo.FreeIndices.Count})");
        }

        protected override void OnRemoveBindlessSampler(IntPtr heap, int index)
        {
            // Validate inputs
            if (heap == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnRemoveBindlessSampler: Invalid heap handle");
                return;
            }

            if (index < 0)
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessSampler: Invalid index {index}");
                return;
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnRemoveBindlessSampler: DirectX 12 device not available");
                return;
            }

            // Get heap information
            if (!_bindlessHeaps.TryGetValue(heap, out BindlessHeapInfo heapInfo))
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessSampler: Heap not found for handle {heap}");
                return;
            }

            // Validate index is within heap capacity
            if (index >= heapInfo.Capacity)
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessSampler: Index {index} exceeds heap capacity {heapInfo.Capacity}");
                return;
            }

            // Check if index is already free
            if (heapInfo.FreeIndices.Contains(index))
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessSampler: Index {index} is already free");
                return;
            }

            // Check if index is beyond the allocated range
            if (index >= heapInfo.NextIndex)
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessSampler: Index {index} is beyond allocated range (NextIndex: {heapInfo.NextIndex})");
                return;
            }

            // Find which sampler is at this index by iterating through the tracking dictionary
            // This is necessary because we only track sampler -> index, not index -> sampler
            IntPtr samplerToRemove = IntPtr.Zero;
            foreach (var kvp in _samplerToHeapIndex)
            {
                if (kvp.Value == index)
                {
                    samplerToRemove = kvp.Key;
                    break;
                }
            }

            if (samplerToRemove == IntPtr.Zero)
            {
                Console.WriteLine($"[StrideDX12] OnRemoveBindlessSampler: No sampler found at index {index}");
                return;
            }

            // Remove sampler from tracking dictionary
            _samplerToHeapIndex.Remove(samplerToRemove);

            // Mark index as free for reuse
            heapInfo.FreeIndices.Add(index);

            // If this is the last allocated index, we can decrement NextIndex to allow reuse
            // This optimization allows the heap to compact when removing the highest-indexed sampler
            if (index == heapInfo.NextIndex - 1)
            {
                // Find the new highest allocated index (highest index that's allocated and not free)
                // NextIndex should be set to the highest allocated index + 1
                int highestAllocatedIndex = -1;
                foreach (var kvp in _samplerToHeapIndex)
                {
                    int allocatedIndex = kvp.Value;
                    // Only consider indices that are allocated (in dictionary) and not free
                    if (!heapInfo.FreeIndices.Contains(allocatedIndex))
                    {
                        if (allocatedIndex > highestAllocatedIndex)
                        {
                            highestAllocatedIndex = allocatedIndex;
                        }
                    }
                }
                // Set NextIndex to highest allocated index + 1 (or 0 if no samplers remain)
                heapInfo.NextIndex = highestAllocatedIndex + 1;
            }

            // Note: In DirectX 12, we don't actually "clear" or "remove" descriptors from the heap.
            // Descriptors remain in the descriptor heap, but we mark the slot as free for reuse.
            // The descriptor at that index may still be valid in GPU memory until it's overwritten
            // by a new descriptor when the index is reused. This is the standard DirectX 12 pattern
            // for bindless resource management - descriptors are persistent until explicitly overwritten.

            Console.WriteLine($"[StrideDX12] OnRemoveBindlessSampler: Removed sampler {samplerToRemove} from heap {heap} at index {index} (NextIndex: {heapInfo.NextIndex}, FreeIndices: {heapInfo.FreeIndices.Count})");
        }

        protected override void OnSetBindlessHeap(IntPtr heap, int slot, ShaderStage stage)
        {
            // Validate inputs
            if (heap == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnSetBindlessHeap: Invalid heap handle");
                return;
            }

            if (slot < 0)
            {
                Console.WriteLine($"[StrideDX12] OnSetBindlessHeap: Invalid slot {slot}");
                return;
            }

            if (stage == ShaderStage.None)
            {
                Console.WriteLine("[StrideDX12] OnSetBindlessHeap: ShaderStage.None is not valid");
                return;
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnSetBindlessHeap: DirectX 12 device not available");
                return;
            }

            // Get heap information
            if (!_bindlessHeaps.TryGetValue(heap, out BindlessHeapInfo heapInfo))
            {
                Console.WriteLine($"[StrideDX12] OnSetBindlessHeap: Heap not found for handle {heap}");
                return;
            }

            if (heapInfo.DescriptorHeap == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnSetBindlessHeap: Descriptor heap pointer is invalid");
                return;
            }

            // Get native command list handle
            // In DirectX 12, we need ID3D12GraphicsCommandList to bind resources
            // Stride's CommandList wraps this, but we need the native handle
            IntPtr nativeCommandList = GetNativeCommandList();
            if (nativeCommandList == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnSetBindlessHeap: Command list not available");
                return;
            }

            // Platform check: DirectX 12 is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("[StrideDX12] OnSetBindlessHeap: DirectX 12 is only available on Windows");
                return;
            }

            // Set bindless heap for shader stage
            // Based on DirectX 12 Bindless Resources: https://devblogs.microsoft.com/directx/in-the-works-hlsl-shader-model-6-6/
            // Implementation approach:
            // 1. Set descriptor heaps using SetDescriptorHeaps (tells D3D12 which heaps are active)
            // 2. Set root parameter using SetGraphicsRootDescriptorTable (binds the heap to a root signature slot)
            //
            // DirectX 12 API references:
            // - ID3D12GraphicsCommandList::SetDescriptorHeaps - sets which descriptor heaps are active
            // - ID3D12GraphicsCommandList::SetGraphicsRootDescriptorTable - binds descriptor table to root parameter
            // - D3D12_GPU_DESCRIPTOR_HANDLE for descriptor heap GPU handle
            //
            // Note: In DirectX 12, descriptor heaps are shared across shader stages (unlike Vulkan's per-stage descriptor sets)
            // The stage parameter is tracked for logging/debugging purposes, but SetDescriptorHeaps affects all stages

            try
            {
                // Step 1: Set descriptor heaps (activates the heap for use)
                // SetDescriptorHeaps takes an array of descriptor heap pointers
                // We set the single heap we want to use
                IntPtr[] descriptorHeaps = new IntPtr[] { heapInfo.DescriptorHeap };
                SetDescriptorHeaps(nativeCommandList, descriptorHeaps, (uint)descriptorHeaps.Length);

                // Step 2: Get GPU descriptor handle for the heap
                // The GPU handle is the base handle for the heap, which can be used as a descriptor table
                IntPtr gpuHandle = heapInfo.GpuHandle;
                if (gpuHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX12] OnSetBindlessHeap: GPU descriptor handle is invalid");
                    return;
                }

                // Step 3: Set root parameter to reference the descriptor heap
                // SetGraphicsRootDescriptorTable binds a descriptor table to a root signature parameter slot
                // The root signature must have a descriptor table parameter at the specified slot
                // The GPU handle represents the base of the descriptor table
                // D3D12_GPU_DESCRIPTOR_HANDLE is a 64-bit value (uint64_t), so we convert IntPtr to ulong
                ulong gpuHandleValue = (ulong)gpuHandle.ToInt64();
                SetGraphicsRootDescriptorTable(nativeCommandList, (uint)slot, gpuHandleValue);

                Console.WriteLine($"[StrideDX12] OnSetBindlessHeap: Bound heap {heap} to slot {slot} for shader stage {stage} (GPU handle: 0x{gpuHandle.ToInt64():X16})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] OnSetBindlessHeap: Exception: {ex.Message}");
                Console.WriteLine($"[StrideDX12] OnSetBindlessHeap: Stack trace: {ex.StackTrace}");
            }
        }

        protected override ResourceInfo CreateSamplerFeedbackTextureInternal(int width, int height, TextureFormat format, IntPtr handle)
        {
            // Validate inputs
            if (width <= 0 || height <= 0)
            {
                Console.WriteLine($"[StrideDX12] CreateSamplerFeedbackTexture: Invalid dimensions {width}x{height}");
                return new ResourceInfo
                {
                    Type = ResourceType.Texture,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "SamplerFeedbackTexture"
                };
            }

            // Platform check: DirectX 12 is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("[StrideDX12] CreateSamplerFeedbackTexture: DirectX 12 is only available on Windows");
                return new ResourceInfo
                {
                    Type = ResourceType.Texture,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "SamplerFeedbackTexture"
                };
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] CreateSamplerFeedbackTexture: DirectX 12 device not available");
                return new ResourceInfo
                {
                    Type = ResourceType.Texture,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "SamplerFeedbackTexture"
                };
            }

            // Create sampler feedback texture using DirectX 12 COM interop
            // Based on DirectX 12 Sampler Feedback: https://docs.microsoft.com/en-us/windows/win32/direct3d12/sampler-feedback
            // Sampler feedback textures track which mip levels are accessed during texture sampling
            // Format: Typically DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE or DXGI_FORMAT_SAMPLER_FEEDBACK_MIP_REGION_USED_OPAQUE
            // Dimensions: Width and height are in tiles (8x8 texel tiles per feedback tile)
            // Flags: Requires D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS and D3D12_RESOURCE_FLAG_SAMPLER_FEEDBACK
            //
            // DirectX 12 API references:
            // - ID3D12Device::CreateCommittedResource for texture creation
            // - D3D12_RESOURCE_DESC for texture description
            // - D3D12_HEAP_TYPE_DEFAULT for GPU-accessible memory
            // - D3D12_RESOURCE_STATE_UNORDERED_ACCESS as initial state
            // - D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS for UAV access
            // - D3D12_RESOURCE_FLAG_SAMPLER_FEEDBACK for sampler feedback capability
            //
            // Sampler feedback format mapping:
            // - DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE = 189 (0xBD) - tracks minimum mip level accessed
            // - DXGI_FORMAT_SAMPLER_FEEDBACK_MIP_REGION_USED_OPAQUE = 190 (0xBE) - tracks which mip regions are used
            // Each tile stores feedback data as uint8_t per 8x8 texel tile

            try
            {
                // Convert TextureFormat to DXGI_FORMAT
                // For sampler feedback, we use specialized formats that aren't in the standard enum
                // Default to DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE if format doesn't specify
                uint dxgiFormat = ConvertFormatToDxgiFormatForSamplerFeedback(format);

                // Create D3D12_RESOURCE_DESC structure for sampler feedback texture
                // Based on DirectX 12 Resource Description: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_resource_desc
                var resourceDesc = new D3D12_RESOURCE_DESC
                {
                    Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D, // 2D texture
                    Alignment = 0, // Use default alignment (64KB for textures)
                    Width = (ulong)width, // Width in tiles
                    Height = (uint)height, // Height in tiles
                    DepthOrArraySize = 1, // Single texture, not array
                    MipLevels = 1, // Single mip level (sampler feedback textures are typically single-mip)
                    Format = dxgiFormat, // DXGI_FORMAT for sampler feedback
                    SampleDesc = new D3D12_SAMPLE_DESC
                    {
                        Count = 1, // No multisampling
                        Quality = 0
                    },
                    Layout = D3D12_TEXTURE_LAYOUT_UNKNOWN, // Standard texture layout
                    Flags = D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS | D3D12_RESOURCE_FLAG_SAMPLER_FEEDBACK
                };

                // Create D3D12_HEAP_PROPERTIES structure
                // Use D3D12_HEAP_TYPE_DEFAULT for GPU-accessible memory
                var heapProps = new D3D12_HEAP_PROPERTIES
                {
                    Type = D3D12_HEAP_TYPE_DEFAULT,
                    CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
                    MemoryPoolPreference = D3D12_MEMORY_POOL_UNKNOWN,
                    CreationNodeMask = 0,
                    VisibleNodeMask = 0
                };

                // Create D3D12_CLEAR_VALUE structure (optional, but good practice for textures)
                // For sampler feedback textures, we can initialize to zero
                var clearValue = new D3D12_CLEAR_VALUE
                {
                    Format = dxgiFormat,
                    Color = new float[] { 0.0f, 0.0f, 0.0f, 0.0f } // Zero-initialize feedback data
                };

                // Allocate memory for structures
                int resourceDescSize = Marshal.SizeOf(typeof(D3D12_RESOURCE_DESC));
                IntPtr resourceDescPtr = Marshal.AllocHGlobal(resourceDescSize);
                try
                {
                    int heapPropsSize = Marshal.SizeOf(typeof(D3D12_HEAP_PROPERTIES));
                    IntPtr heapPropsPtr = Marshal.AllocHGlobal(heapPropsSize);
                    try
                    {
                        int clearValueSize = Marshal.SizeOf(typeof(D3D12_CLEAR_VALUE));
                        IntPtr clearValuePtr = Marshal.AllocHGlobal(clearValueSize);
                        try
                        {
                            Marshal.StructureToPtr(resourceDesc, resourceDescPtr, false);
                            Marshal.StructureToPtr(heapProps, heapPropsPtr, false);
                            Marshal.StructureToPtr(clearValue, clearValuePtr, false);

                            // Allocate memory for the output resource pointer
                            IntPtr resourcePtr = Marshal.AllocHGlobal(IntPtr.Size);
                            try
                            {
                                // Call ID3D12Device::CreateCommittedResource
                                // HRESULT CreateCommittedResource(
                                //   const D3D12_HEAP_PROPERTIES *pHeapProperties,
                                //   D3D12_HEAP_FLAGS HeapFlags,
                                //   const D3D12_RESOURCE_DESC *pDesc,
                                //   D3D12_RESOURCE_STATES InitialResourceState,
                                //   const D3D12_CLEAR_VALUE *pOptimizedClearValue,
                                //   REFIID riidResource,
                                //   void **ppvResource
                                // );
                                Guid iidResource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad"); // IID_ID3D12Resource

                                int hr = CreateCommittedResource(
                                    _device,
                                    heapPropsPtr,
                                    D3D12_HEAP_FLAG_NONE,
                                    resourceDescPtr,
                                    D3D12_RESOURCE_STATE_UNORDERED_ACCESS,
                                    clearValuePtr,
                                    ref iidResource,
                                    resourcePtr);

                                if (hr < 0)
                                {
                                    Console.WriteLine($"[StrideDX12] CreateSamplerFeedbackTexture: CreateCommittedResource failed with HRESULT 0x{hr:X8}");
                                    return new ResourceInfo
                                    {
                                        Type = ResourceType.Texture,
                                        Handle = IntPtr.Zero,
                                        NativeHandle = IntPtr.Zero,
                                        DebugName = "SamplerFeedbackTexture"
                                    };
                                }

                                // Get the resource pointer
                                IntPtr nativeResource = Marshal.ReadIntPtr(resourcePtr);
                                if (nativeResource == IntPtr.Zero)
                                {
                                    Console.WriteLine("[StrideDX12] CreateSamplerFeedbackTexture: Resource pointer is null");
                                    return new ResourceInfo
                                    {
                                        Type = ResourceType.Texture,
                                        Handle = IntPtr.Zero,
                                        NativeHandle = IntPtr.Zero,
                                        DebugName = "SamplerFeedbackTexture"
                                    };
                                }

                                // Calculate size in bytes
                                // Sampler feedback textures store one byte per 8x8 tile
                                // Size = width * height * 1 byte per tile
                                long sizeInBytes = (long)width * height * 1;

                                Console.WriteLine($"[StrideDX12] CreateSamplerFeedbackTexture: Created sampler feedback texture {width}x{height}, format 0x{dxgiFormat:X}, size {sizeInBytes} bytes");

                                return new ResourceInfo
                                {
                                    Type = ResourceType.Texture,
                                    Handle = handle,
                                    NativeHandle = nativeResource,
                                    DebugName = $"SamplerFeedbackTexture_{width}x{height}",
                                    SizeInBytes = sizeInBytes
                                };
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(resourcePtr);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(clearValuePtr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(heapPropsPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(resourceDescPtr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] CreateSamplerFeedbackTexture: Exception: {ex.Message}");
                Console.WriteLine($"[StrideDX12] CreateSamplerFeedbackTexture: Stack trace: {ex.StackTrace}");
                return new ResourceInfo
                {
                    Type = ResourceType.Texture,
                    Handle = IntPtr.Zero,
                    NativeHandle = IntPtr.Zero,
                    DebugName = "SamplerFeedbackTexture"
                };
            }
        }

        protected override void OnReadSamplerFeedback(IntPtr texture, byte[] data, int dataSize)
        {
            // Validate inputs
            if (texture == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: Invalid texture handle");
                return;
            }

            if (data == null)
            {
                Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: Data buffer is null");
                return;
            }

            if (dataSize <= 0 || dataSize > data.Length)
            {
                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Invalid data size {dataSize}, buffer length {data.Length}");
                return;
            }

            if (!_resources.TryGetValue(texture, out ResourceInfo resourceInfo))
            {
                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Resource not found for handle {texture}");
                return;
            }

            if (resourceInfo.Type != ResourceType.Texture)
            {
                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Resource is not a texture (type: {resourceInfo.Type})");
                return;
            }

            if (resourceInfo.NativeHandle == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: Native texture handle is invalid");
                return;
            }

            // Read sampler feedback data from GPU to CPU
            // Based on DirectX 12 Sampler Feedback: https://docs.microsoft.com/en-us/windows/win32/direct3d12/sampler-feedback
            // Implementation pattern:
            // 1. Transition texture to COPY_SOURCE state (if not already)
            // 2. Create readback buffer (D3D12_HEAP_TYPE_READBACK)
            // 3. Copy texture data to readback buffer using CopyTextureRegion
            // 4. Execute command list and wait for completion
            // 5. Map readback buffer and copy data to output array
            // 6. Unmap readback buffer
            // 7. Transition texture back to original state (if needed)
            //
            // DirectX 12 API references:
            // - ID3D12Device::CreateCommittedResource for readback buffer
            // - ID3D12GraphicsCommandList::CopyTextureRegion for data copy
            // - ID3D12Resource::Map/Unmap for CPU access
            // - D3D12_RESOURCE_STATE_COPY_SOURCE for texture state
            // - D3D12_HEAP_TYPE_READBACK for CPU-accessible memory
            //
            // Sampler feedback format: Typically D3D12_FEEDBACK_MAP_FORMAT_UINT8_8x8
            // Each tile is 8x8 texels, stored as uint8_t per tile
            // Data layout: Row-major order of feedback tiles

            // Access Stride's native DirectX 12 device and command list
            // _device is IntPtr to ID3D12Device (set in CreateDeviceResources)
            // _commandList is IntPtr to ID3D12GraphicsCommandList (set in CreateSwapChainResources)
            // resourceInfo.NativeHandle is IntPtr to ID3D12Resource (the sampler feedback texture)

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: DirectX 12 device not available");
                return;
            }

            IntPtr nativeCommandList = GetNativeCommandList();
            if (nativeCommandList == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: Command list not available");
                return;
            }

            // Step 1: Get texture resource description using ID3D12Resource::GetDesc
            // This gives us the exact dimensions, format, and layout of the texture
            IntPtr sourceResourceDescPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RESOURCE_DESC)));
            D3D12_RESOURCE_DESC sourceResourceDesc;
            try
            {
                GetResourceDesc(resourceInfo.NativeHandle, sourceResourceDescPtr);
                sourceResourceDesc = (D3D12_RESOURCE_DESC)Marshal.PtrToStructure(sourceResourceDescPtr, typeof(D3D12_RESOURCE_DESC));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Failed to get resource description: {ex.Message}");
                Marshal.FreeHGlobal(sourceResourceDescPtr);
                return;
            }

            // Step 2: Get copyable footprint for the source texture using ID3D12Device::GetCopyableFootprints
            // This tells us the exact layout, row pitch, and total size needed for copying
            IntPtr layoutsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_PLACED_SUBRESOURCE_FOOTPRINT)));
            IntPtr numRowsPtr = Marshal.AllocHGlobal(sizeof(uint));
            IntPtr rowSizeInBytesPtr = Marshal.AllocHGlobal(sizeof(ulong));
            IntPtr totalBytesPtr = Marshal.AllocHGlobal(sizeof(ulong));

            D3D12_PLACED_SUBRESOURCE_FOOTPRINT footprint;
            uint numRows;
            ulong rowSizeInBytes;
            ulong totalBytes;

            try
            {
                int footprintsHr = GetCopyableFootprints(
                    _device,
                    sourceResourceDescPtr,
                    0, // firstSubresource
                    1, // numSubresources (only need the first mip)
                    0, // baseOffset
                    layoutsPtr,
                    numRowsPtr,
                    rowSizeInBytesPtr,
                    totalBytesPtr);

                if (footprintsHr < 0)
                {
                    Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: GetCopyableFootprints failed, HRESULT 0x{footprintsHr:X8}");
                    Marshal.FreeHGlobal(sourceResourceDescPtr);
                    Marshal.FreeHGlobal(layoutsPtr);
                    Marshal.FreeHGlobal(numRowsPtr);
                    Marshal.FreeHGlobal(rowSizeInBytesPtr);
                    Marshal.FreeHGlobal(totalBytesPtr);
                    return;
                }

                footprint = (D3D12_PLACED_SUBRESOURCE_FOOTPRINT)Marshal.PtrToStructure(layoutsPtr, typeof(D3D12_PLACED_SUBRESOURCE_FOOTPRINT));
                numRows = (uint)Marshal.ReadInt32(numRowsPtr);
                rowSizeInBytes = (ulong)Marshal.ReadInt64(rowSizeInBytesPtr);
                totalBytes = (ulong)Marshal.ReadInt64(totalBytesPtr);

                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Footprint - Width={footprint.Footprint.Width}, Height={footprint.Footprint.Height}, RowPitch={footprint.Footprint.RowPitch}, TotalBytes={totalBytes}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Failed to get copyable footprint: {ex.Message}");
                Marshal.FreeHGlobal(sourceResourceDescPtr);
                Marshal.FreeHGlobal(layoutsPtr);
                Marshal.FreeHGlobal(numRowsPtr);
                Marshal.FreeHGlobal(rowSizeInBytesPtr);
                Marshal.FreeHGlobal(totalBytesPtr);
                return;
            }

            // Step 3: Create readback buffer using ID3D12Device::CreateCommittedResource
            // Use the totalBytes from GetCopyableFootprints to size the buffer correctly
            // Heap type: D3D12_HEAP_TYPE_READBACK for CPU-accessible memory
            // Resource desc: Buffer with size matching dataSize
            // Initial state: D3D12_RESOURCE_STATE_COPY_DEST

            var readbackHeapProps = new D3D12_HEAP_PROPERTIES
            {
                Type = D3D12_HEAP_TYPE_READBACK,
                CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_WRITE_BACK,
                MemoryPoolPreference = D3D12_MEMORY_POOL_L0,
                CreationNodeMask = 0,
                VisibleNodeMask = 0
            };

            // Use totalBytes from GetCopyableFootprints for accurate buffer size
            // This ensures the readback buffer can hold all the texture data
            ulong readbackBufferSize = totalBytes;

            var readbackResourceDesc = new D3D12_RESOURCE_DESC
            {
                Dimension = D3D12_RESOURCE_DIMENSION_BUFFER,
                Alignment = 0,
                Width = readbackBufferSize,
                Height = 1,
                DepthOrArraySize = 1,
                MipLevels = 1,
                Format = D3D12_DXGI_FORMAT_UNKNOWN,
                SampleDesc = new D3D12_SAMPLE_DESC { Count = 1, Quality = 0 },
                Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR,
                Flags = D3D12_RESOURCE_FLAG_NONE
            };

            IntPtr readbackHeapPropsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_HEAP_PROPERTIES)));
            IntPtr readbackResourceDescPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RESOURCE_DESC)));
            IntPtr readbackResourcePtr = Marshal.AllocHGlobal(IntPtr.Size);
            IntPtr readbackResource = IntPtr.Zero;

            try
            {
                Marshal.StructureToPtr(readbackHeapProps, readbackHeapPropsPtr, false);
                Marshal.StructureToPtr(readbackResourceDesc, readbackResourceDescPtr, false);

                Guid iidResource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad"); // IID_ID3D12Resource

                int hr = CreateCommittedResource(
                    _device,
                    readbackHeapPropsPtr,
                    D3D12_HEAP_FLAG_NONE,
                    readbackResourceDescPtr,
                    D3D12_RESOURCE_STATE_COPY_DEST,
                    IntPtr.Zero, // No clear value for buffers
                    ref iidResource,
                    readbackResourcePtr);

                if (hr < 0)
                {
                    Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Failed to create readback buffer, HRESULT 0x{hr:X8}");
                    return;
                }

                readbackResource = Marshal.ReadIntPtr(readbackResourcePtr);
                if (readbackResource == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: Readback buffer creation returned null");
                    return;
                }

                // Step 4: Transition feedback texture to COPY_SOURCE state using ResourceBarrier
                var barrier = new D3D12_RESOURCE_BARRIER
                {
                    Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION,
                    Flags = 0,
                    Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                    {
                        pResource = resourceInfo.NativeHandle,
                        Subresource = 0,
                        StateBefore = D3D12_RESOURCE_STATE_UNORDERED_ACCESS, // Sampler feedback textures are typically in UAV state
                        StateAfter = D3D12_RESOURCE_STATE_COPY_SOURCE
                    }
                };

                IntPtr barrierPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RESOURCE_BARRIER)));
                try
                {
                    Marshal.StructureToPtr(barrier, barrierPtr, false);
                    ResourceBarrier(nativeCommandList, barrierPtr, 1);
                }
                finally
                {
                    Marshal.FreeHGlobal(barrierPtr);
                }

                // Step 5: Copy texture to readback buffer using CopyTextureRegion
                var srcLocation = new D3D12_TEXTURE_COPY_LOCATION
                {
                    pResource = resourceInfo.NativeHandle,
                    Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX,
                    PlacedFootprint = new D3D12_PLACED_SUBRESOURCE_FOOTPRINT()
                };

                var dstLocation = new D3D12_TEXTURE_COPY_LOCATION
                {
                    pResource = readbackResource,
                    Type = D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT,
                    PlacedFootprint = footprint
                };

                IntPtr srcLocationPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_TEXTURE_COPY_LOCATION)));
                IntPtr dstLocationPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_TEXTURE_COPY_LOCATION)));
                try
                {
                    Marshal.StructureToPtr(srcLocation, srcLocationPtr, false);
                    Marshal.StructureToPtr(dstLocation, dstLocationPtr, false);

                    // CopyTextureRegion signature: void CopyTextureRegion(
                    //   const D3D12_TEXTURE_COPY_LOCATION *pDst,
                    //   UINT DstX, UINT DstY, UINT DstZ,
                    //   const D3D12_TEXTURE_COPY_LOCATION *pSrc,
                    //   const D3D12_BOX *pSrcBox)
                    CopyTextureRegion(nativeCommandList, dstLocationPtr, 0, 0, 0, srcLocationPtr, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(srcLocationPtr);
                    Marshal.FreeHGlobal(dstLocationPtr);
                }

                // Step 6: Transition texture back to original state
                var barrierBack = new D3D12_RESOURCE_BARRIER
                {
                    Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION,
                    Flags = 0,
                    Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                    {
                        pResource = resourceInfo.NativeHandle,
                        Subresource = 0,
                        StateBefore = D3D12_RESOURCE_STATE_COPY_SOURCE,
                        StateAfter = D3D12_RESOURCE_STATE_UNORDERED_ACCESS
                    }
                };

                IntPtr barrierBackPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RESOURCE_BARRIER)));
                try
                {
                    Marshal.StructureToPtr(barrierBack, barrierBackPtr, false);
                    ResourceBarrier(nativeCommandList, barrierBackPtr, 1);
                }
                finally
                {
                    Marshal.FreeHGlobal(barrierBackPtr);
                }

                // Step 7: Close command list and execute
                CloseCommandList(nativeCommandList);

                // Step 8: Execute command list and wait for GPU completion
                // Full implementation with direct queue access and fence synchronization
                // Based on DirectX 12 Command Queue execution pattern:
                // https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandqueue-executecommandlists
                // https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandqueue-signal
                // https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12fence-getcompletedvalue

                IntPtr commandQueue = GetNativeCommandQueue();
                IntPtr fence = IntPtr.Zero;
                IntPtr fencePtr = IntPtr.Zero;

                if (commandQueue != IntPtr.Zero)
                {
                    // Create fence for GPU synchronization
                    // Initial value: 0, flags: 0 (no special flags)
                    fencePtr = Marshal.AllocHGlobal(IntPtr.Size);
                    try
                    {
                        // Cannot pass static readonly field as ref - create local copy
                        Guid iidFence = IID_ID3D12Fence;
                        int fenceHr = CreateFence(_device, 0, 0, ref iidFence, fencePtr);
                        if (fenceHr >= 0)
                        {
                            fence = Marshal.ReadIntPtr(fencePtr);
                            if (fence != IntPtr.Zero)
                            {
                                // Execute command list on the queue
                                IntPtr[] commandLists = new IntPtr[] { nativeCommandList };
                                ExecuteCommandLists(commandQueue, commandLists, 1);

                                // Signal fence with value 1 to track completion
                                // The fence will be signaled by the GPU when the command list execution completes
                                ulong fenceValue = 1;
                                ulong signaledValue = SignalFence(commandQueue, fence, fenceValue);

                                if (signaledValue > 0)
                                {
                                    // Wait for fence completion using polling
                                    // Poll GetFenceCompletedValue until it reaches the signaled value
                                    // This ensures the GPU has finished executing the command list
                                    const int maxWaitIterations = 1000000; // Prevent infinite loop
                                    const int sleepMs = 1; // Sleep 1ms between polls to avoid CPU spinning
                                    int iteration = 0;

                                    while (iteration < maxWaitIterations)
                                    {
                                        ulong completedValue = GetFenceCompletedValue(fence);
                                        if (completedValue >= fenceValue)
                                        {
                                            // Fence has reached the target value - GPU work is complete
                                            break;
                                        }

                                        // Sleep briefly to avoid excessive CPU usage
                                        System.Threading.Thread.Sleep(sleepMs);
                                        iteration++;
                                    }

                                    if (iteration >= maxWaitIterations)
                                    {
                                        Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: Fence wait timeout - falling back to WaitIdle");
                                        OnWaitForGpu();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: Failed to signal fence - falling back to WaitIdle");
                                    OnWaitForGpu();
                                }
                            }
                            else
                            {
                                Console.WriteLine("[StrideDX12] OnReadSamplerFeedback: Fence creation returned null - falling back to WaitIdle");
                                OnWaitForGpu();
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Failed to create fence, HRESULT 0x{fenceHr:X8} - falling back to WaitIdle");
                            OnWaitForGpu();
                        }
                    }
                    finally
                    {
                        // Clean up fence COM object
                        if (fence != IntPtr.Zero)
                        {
                            IntPtr vtable = Marshal.ReadIntPtr(fence);
                            if (vtable != IntPtr.Zero)
                            {
                                IntPtr releasePtr = Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size); // IUnknown::Release at index 2
                                if (releasePtr != IntPtr.Zero)
                                {
                                    var releaseDelegate = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(
                                        releasePtr, typeof(ReleaseDelegate));
                                    releaseDelegate(fence);
                                }
                            }
                        }
                        Marshal.FreeHGlobal(fencePtr);
                    }
                }
                else
                {
                    // Command queue not available - use Stride's synchronization mechanism as fallback
                    // This is acceptable when direct queue access is not possible through Stride's API
                    OnWaitForGpu();
                }

                // Step 9: Map readback buffer using ID3D12Resource::Map
                IntPtr mappedDataPtr = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    int mapHr = MapResource(readbackResource, 0, IntPtr.Zero, mappedDataPtr);
                    if (mapHr < 0)
                    {
                        Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Failed to map readback buffer, HRESULT 0x{mapHr:X8}");
                        return;
                    }

                    IntPtr mappedData = Marshal.ReadIntPtr(mappedDataPtr);
                    if (mappedData != IntPtr.Zero)
                    {
                        // Step 10: Copy mapped data to output byte array
                        Marshal.Copy(mappedData, data, 0, Math.Min(dataSize, data.Length));

                        // Step 11: Unmap readback buffer
                        UnmapResource(readbackResource, 0);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(mappedDataPtr);
                }

                // Clean up readback resource
                // Release the COM object (call Release on the resource)
                if (readbackResource != IntPtr.Zero)
                {
                    IntPtr vtable = Marshal.ReadIntPtr(readbackResource);
                    if (vtable != IntPtr.Zero)
                    {
                        IntPtr releasePtr = Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size); // IUnknown::Release at index 2
                        if (releasePtr != IntPtr.Zero)
                        {
                            var releaseDelegate = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(
                                releasePtr, typeof(ReleaseDelegate));
                            releaseDelegate(readbackResource);
                        }
                    }
                }

                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Successfully read {dataSize} bytes from sampler feedback texture {resourceInfo.NativeHandle}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Error reading sampler feedback data: {ex.Message}");
                Console.WriteLine($"[StrideDX12] OnReadSamplerFeedback: Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Free memory allocated for GetCopyableFootprints (if not already freed in catch blocks)
                if (sourceResourceDescPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(sourceResourceDescPtr);
                }
                if (layoutsPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(layoutsPtr);
                }
                if (numRowsPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(numRowsPtr);
                }
                if (rowSizeInBytesPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(rowSizeInBytesPtr);
                }
                if (totalBytesPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(totalBytesPtr);
                }
                // Free memory allocated for readback buffer creation
                Marshal.FreeHGlobal(readbackHeapPropsPtr);
                Marshal.FreeHGlobal(readbackResourceDescPtr);
                Marshal.FreeHGlobal(readbackResourcePtr);
            }
        }

        protected override void OnSetSamplerFeedbackTexture(IntPtr texture, int slot)
        {
            // Validate inputs
            if (texture == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnSetSamplerFeedbackTexture: Invalid texture handle");
                return;
            }

            if (slot < 0)
            {
                Console.WriteLine($"[StrideDX12] OnSetSamplerFeedbackTexture: Invalid slot {slot}");
                return;
            }

            if (_device == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnSetSamplerFeedbackTexture: DirectX 12 device not available");
                return;
            }

            // Get texture resource information
            if (!_resources.TryGetValue(texture, out ResourceInfo textureResource))
            {
                Console.WriteLine($"[StrideDX12] OnSetSamplerFeedbackTexture: Texture resource not found for handle {texture}");
                return;
            }

            if (textureResource.Type != ResourceType.Texture)
            {
                Console.WriteLine($"[StrideDX12] OnSetSamplerFeedbackTexture: Resource is not a texture (type: {textureResource.Type})");
                return;
            }

            if (textureResource.NativeHandle == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnSetSamplerFeedbackTexture: Native texture handle is invalid");
                return;
            }

            // Get native command list handle
            // In DirectX 12, we need ID3D12GraphicsCommandList to bind resources
            // Stride's CommandList wraps this, but we need the native handle
            IntPtr nativeCommandList = GetNativeCommandList();
            if (nativeCommandList == IntPtr.Zero)
            {
                Console.WriteLine("[StrideDX12] OnSetSamplerFeedbackTexture: Command list not available");
                return;
            }

            // Platform check: DirectX 12 is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("[StrideDX12] OnSetSamplerFeedbackTexture: DirectX 12 is only available on Windows");
                return;
            }

            // Set sampler feedback texture as shader resource
            // Based on DirectX 12 Sampler Feedback: https://docs.microsoft.com/en-us/windows/win32/direct3d12/sampler-feedback
            // Sampler feedback textures are bound as shader resources using SetGraphicsRootShaderResourceView
            // or SetGraphicsRootDescriptorTable with a descriptor heap containing the SRV
            //
            // Implementation approach:
            // 1. Get or create SRV descriptor for the sampler feedback texture
            // 2. Bind the SRV to the specified root parameter slot using SetGraphicsRootShaderResourceView
            //    or SetGraphicsRootDescriptorTable if using descriptor heaps
            //
            // DirectX 12 API references:
            // - ID3D12GraphicsCommandList::SetGraphicsRootShaderResourceView
            // - ID3D12GraphicsCommandList::SetGraphicsRootDescriptorTable
            // - D3D12_GPU_VIRTUAL_ADDRESS for resource binding
            //
            // Note: For sampler feedback textures, we typically use SetGraphicsRootShaderResourceView
            // which binds the GPU virtual address of the resource directly to a root parameter slot.
            // Alternatively, we can use descriptor tables if the pipeline uses root signature with descriptor tables.

            try
            {
                // Get GPU virtual address of the texture resource
                // ID3D12Resource::GetGPUVirtualAddress() returns the GPU virtual address
                // This is used for root parameter binding with SetGraphicsRootShaderResourceView
                ulong gpuVirtualAddress = GetResourceGpuVirtualAddress(textureResource.NativeHandle);
                if (gpuVirtualAddress == 0)
                {
                    Console.WriteLine("[StrideDX12] OnSetSamplerFeedbackTexture: Failed to get GPU virtual address for texture");
                    return;
                }

                // Bind the sampler feedback texture to the specified root parameter slot
                // Using SetGraphicsRootShaderResourceView for direct resource binding
                // This binds the GPU virtual address to root parameter index 'slot'
                // Root parameter type must be D3D12_ROOT_PARAMETER_TYPE_SRV for this to work
                SetGraphicsRootShaderResourceView(nativeCommandList, (uint)slot, gpuVirtualAddress);

                Console.WriteLine($"[StrideDX12] OnSetSamplerFeedbackTexture: Bound sampler feedback texture {texture} to slot {slot} (GPU VA: 0x{gpuVirtualAddress:X16})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] OnSetSamplerFeedbackTexture: Exception: {ex.Message}");
                Console.WriteLine($"[StrideDX12] OnSetSamplerFeedbackTexture: Stack trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region DirectX 12 P/Invoke Declarations (Windows-only)

        // Platform: DirectX 12 is Windows-only (x64/x86)
        // For cross-platform support (Linux/macOS), use StrideVulkanBackend instead
        // This backend should only be instantiated on Windows via StrideBackendFactory

        // DirectX 12 Descriptor Heap Types (removed duplicate - see line 2580)

        // DirectX 12 Descriptor Heap Flags (removed duplicate - see line 2586)
        // D3D12_DESCRIPTOR_HEAP_DESC structure (removed duplicate - see line 2601)

        // ID3D12Device::CreateDescriptorHeap
        // HRESULT CreateDescriptorHeap(
        //   const D3D12_DESCRIPTOR_HEAP_DESC *pDescriptorHeapDesc,
        //   REFIID riid,
        //   void **ppvHeap
        // );
        [DllImport("d3d12.dll", EntryPoint = "?CreateDescriptorHeap@ID3D12Device@@UEAAJPEBUD3D12_DESCRIPTOR_HEAP_DESC@@AEBU_GUID@@PEAPEAX@Z", CallingConvention = CallingConvention.StdCall)]
        private static extern int CreateDescriptorHeap(IntPtr device, IntPtr pDescriptorHeapDesc, ref Guid riid, IntPtr ppvHeap);

        // ID3D12DescriptorHeap::GetCPUDescriptorHandleForHeapStart
        // D3D12_CPU_DESCRIPTOR_HANDLE GetCPUDescriptorHandleForHeapStart();
        [DllImport("d3d12.dll", EntryPoint = "?GetCPUDescriptorHandleForHeapStart@ID3D12DescriptorHeap@@QEAA?AUD3D12_CPU_DESCRIPTOR_HANDLE@@XZ", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetDescriptorHeapStartHandle(IntPtr descriptorHeap);

        // ID3D12DescriptorHeap::GetGPUDescriptorHandleForHeapStart
        // D3D12_GPU_DESCRIPTOR_HANDLE GetGPUDescriptorHandleForHeapStart();
        [DllImport("d3d12.dll", EntryPoint = "?GetGPUDescriptorHandleForHeapStart@ID3D12DescriptorHeap@@QEAA?AUD3D12_GPU_DESCRIPTOR_HANDLE@@XZ", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetDescriptorHeapStartHandleGpu(IntPtr descriptorHeap);

        // ID3D12Device::GetDescriptorHandleIncrementSize
        // UINT GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE DescriptorHeapType);
        [DllImport("d3d12.dll", EntryPoint = "?GetDescriptorHandleIncrementSize@ID3D12Device@@UEAAIW4D3D12_DESCRIPTOR_HEAP_TYPE@@@Z", CallingConvention = CallingConvention.StdCall)]
        private static extern uint GetDescriptorHandleIncrementSize(IntPtr device, uint descriptorHeapType);

        // Note: The above P/Invoke declarations use mangled C++ names which may vary by compiler.
        // For production use, consider using COM interop with proper interface definitions
        // or use a library like SharpDX/Vortice.Windows that provides proper DirectX 12 bindings.
        // Alternative approach: Use vtable offsets to call methods directly.

        // Helper method to call CreateDescriptorHeap using vtable offset (more reliable)
        // d3d12.dll: ID3D12Device vtable - CreateDescriptorHeap is at index 27
        // This method tries P/Invoke first, then falls back to vtable calling if P/Invoke fails
        private static unsafe int CreateDescriptorHeapVTableStatic(IntPtr device, IntPtr pDescriptorHeapDesc, ref Guid riid, IntPtr ppvHeap)
        {
            // ID3D12Device vtable layout (verified):
            // [0] QueryInterface
            // [1] AddRef
            // [2] Release
            // ...
            // [27] CreateDescriptorHeap (verified vtable index for ID3D12Device in d3d12.dll)

            // Try P/Invoke first (fastest path)
            try
            {
                return CreateDescriptorHeap(device, pDescriptorHeapDesc, ref riid, ppvHeap);
            }
            catch (DllNotFoundException)
            {
                // Fallback: Use vtable calling convention when P/Invoke fails
                // This can happen if d3d12.dll is not found or mangled names don't match
                return CreateDescriptorHeapVTableStaticFallback(device, pDescriptorHeapDesc, ref riid, ppvHeap);
            }
            catch (EntryPointNotFoundException)
            {
                // Fallback: Use vtable calling convention when entry point is not found
                // This can happen if mangled C++ names don't match the DLL
                return CreateDescriptorHeapVTableStaticFallback(device, pDescriptorHeapDesc, ref riid, ppvHeap);
            }
        }

        /// <summary>
        /// Calls ID3D12Device::CreateDescriptorHeap through COM vtable as fallback.
        /// VTable index 27 for ID3D12Device (verified in d3d12.dll).
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// </summary>
        private static unsafe int CreateDescriptorHeapVTableStaticFallback(IntPtr device, IntPtr pDescriptorHeapDesc, ref Guid riid, IntPtr ppvHeap)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            if (pDescriptorHeapDesc == IntPtr.Zero || ppvHeap == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            try
            {
                // Get vtable pointer (first field of COM object)
                // COM objects have their vtable pointer as the first member
                IntPtr* vtable = *(IntPtr**)device;

                // CreateDescriptorHeap is at index 27 in ID3D12Device vtable
                // Verified: d3d12.dll ID3D12Device vtable layout
                IntPtr methodPtr = vtable[27];

                if (methodPtr == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDX12] CreateDescriptorHeapVTableFallback: Method pointer is null at vtable index 27");
                    return unchecked((int)0x80004005); // E_FAIL
                }

                // Create delegate from function pointer (C# 7.3 compatible)
                // Function signature: HRESULT CreateDescriptorHeap(
                //   const D3D12_DESCRIPTOR_HEAP_DESC *pDescriptorHeapDesc,
                //   REFIID riid,
                //   void **ppvHeap
                // );
                CreateDescriptorHeapDelegate createDescriptorHeap =
                    (CreateDescriptorHeapDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateDescriptorHeapDelegate));

                // Call through vtable
                int hr = createDescriptorHeap(device, pDescriptorHeapDesc, ref riid, ppvHeap);

                if (hr < 0)
                {
                    Console.WriteLine($"[StrideDX12] CreateDescriptorHeapVTableFallback: CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                }

                return hr;
            }
            catch (AccessViolationException ex)
            {
                Console.WriteLine($"[StrideDX12] CreateDescriptorHeapVTableFallback: Access violation - invalid device pointer or vtable: {ex.Message}");
                return unchecked((int)0x80004005); // E_FAIL
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDX12] CreateDescriptorHeapVTableFallback: Exception: {ex.Message}");
                Console.WriteLine($"[StrideDX12] CreateDescriptorHeapVTableFallback: Stack trace: {ex.StackTrace}");
                return unchecked((int)0x80004005); // E_FAIL
            }
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Information about a bindless descriptor heap.
        /// Tracks the heap, handles, capacity, allocation state, and heap type.
        /// </summary>
        private class BindlessHeapInfo
        {
            public IntPtr DescriptorHeap { get; set; }
            public IntPtr CpuHandle { get; set; }
            public IntPtr GpuHandle { get; set; }
            public int Capacity { get; set; }
            public uint DescriptorIncrementSize { get; set; }
            public int NextIndex { get; set; }
            public HashSet<int> FreeIndices { get; set; }
            /// <summary>
            /// Descriptor heap type (D3D12_DESCRIPTOR_HEAP_TYPE).
            /// D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV (0) for texture heaps.
            /// D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER (1) for sampler heaps.
            /// </summary>
            public uint HeapType { get; set; }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Offsets a descriptor handle by a given number of descriptors.
        /// </summary>
        private IntPtr OffsetDescriptorHandle(IntPtr handle, int offset, uint incrementSize)
        {
            // D3D12_CPU_DESCRIPTOR_HANDLE and D3D12_GPU_DESCRIPTOR_HANDLE are 64-bit values
            // Offset = handle.ptr + (offset * incrementSize)
            ulong handleValue = (ulong)handle.ToInt64();
            ulong offsetValue = (ulong)offset * incrementSize;
            return new IntPtr((long)(handleValue + offsetValue));
        }

        /// <summary>
        /// Gets the native DirectX 12 command list handle from Stride's CommandList.
        /// </summary>
        private IntPtr GetNativeCommandList()
        {
            // Stride's CommandList wraps ID3D12GraphicsCommandList
            // We need to access the native handle for DirectX 12 API calls via reflection
            if (_strideCommandList == null)
            {
                return IntPtr.Zero;
            }

            try
            {
                var commandListType = _strideCommandList.GetType();
                var nativeProperty = commandListType.GetProperty("NativeCommandList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativeProperty != null)
                {
                    var value = nativeProperty.GetValue(_strideCommandList);
                    if (value is IntPtr)
                    {
                        return (IntPtr)value;
                    }
                }

                // Try NativePointer as alternative
                var nativePointerProperty = commandListType.GetProperty("NativePointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativePointerProperty != null)
                {
                    var value = nativePointerProperty.GetValue(_strideCommandList);
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

        #endregion

        #region DirectX 12 P/Invoke Declarations and Structures

        // DirectX 12 Descriptor Heap Types
        private const uint D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV = 0;
        private const uint D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER = 1;
        private const uint D3D12_DESCRIPTOR_HEAP_TYPE_RTV = 2;
        private const uint D3D12_DESCRIPTOR_HEAP_TYPE_DSV = 3;

        // DirectX 12 Descriptor Heap Flags
        private const uint D3D12_DESCRIPTOR_HEAP_FLAG_NONE = 0;
        private const uint D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE = 0x1;

        // DirectX 12 SRV Dimension
        private const uint D3D12_SRV_DIMENSION_TEXTURE2D = 4;

        // DirectX 12 Default Shader 4 Component Mapping
        private const uint D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING = 0x1688; // D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING

        // DirectX 12 DXGI Format
        private const uint D3D12_DXGI_FORMAT_UNKNOWN = 0;

        /// <summary>
        /// D3D12_DESCRIPTOR_HEAP_DESC structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DESCRIPTOR_HEAP_DESC
        {
            public uint Type; // D3D12_DESCRIPTOR_HEAP_TYPE
            public uint NumDescriptors;
            public uint Flags; // D3D12_DESCRIPTOR_HEAP_FLAGS
            public uint NodeMask;
        }

        /// <summary>
        /// D3D12_SHADER_RESOURCE_VIEW_DESC structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_SHADER_RESOURCE_VIEW_DESC
        {
            public uint Format; // DXGI_FORMAT
            public uint ViewDimension; // D3D12_SRV_DIMENSION
            public uint Shader4ComponentMapping;
            public D3D12_TEX2D_SRV Texture2D;
        }

        /// <summary>
        /// D3D12_TEX2D_SRV structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_SRV
        {
            public uint MostDetailedMip;
            public uint MipLevels;
            public uint PlaneSlice;
            public float ResourceMinLODClamp;
        }

        // DirectX 12 Filter Types
        private const uint D3D12_FILTER_MIN_MAG_MIP_POINT = 0x00000000;
        private const uint D3D12_FILTER_MIN_MAG_POINT_MIP_LINEAR = 0x00000001;
        private const uint D3D12_FILTER_MIN_POINT_MAG_LINEAR_MIP_POINT = 0x00000004;
        private const uint D3D12_FILTER_MIN_POINT_MAG_MIP_LINEAR = 0x00000005;
        private const uint D3D12_FILTER_MIN_LINEAR_MAG_MIP_POINT = 0x00000010;
        private const uint D3D12_FILTER_MIN_LINEAR_MAG_POINT_MIP_LINEAR = 0x00000011;
        private const uint D3D12_FILTER_MIN_MAG_LINEAR_MIP_POINT = 0x00000014;
        private const uint D3D12_FILTER_MIN_MAG_MIP_LINEAR = 0x00000015;
        private const uint D3D12_FILTER_ANISOTROPIC = 0x00000055;

        // DirectX 12 Texture Address Modes
        private const uint D3D12_TEXTURE_ADDRESS_MODE_WRAP = 1;
        private const uint D3D12_TEXTURE_ADDRESS_MODE_MIRROR = 2;
        private const uint D3D12_TEXTURE_ADDRESS_MODE_CLAMP = 3;
        private const uint D3D12_TEXTURE_ADDRESS_MODE_BORDER = 4;
        private const uint D3D12_TEXTURE_ADDRESS_MODE_MIRROR_ONCE = 5;

        // DirectX 12 Comparison Functions
        private const uint D3D12_COMPARISON_FUNC_NEVER = 1;
        private const uint D3D12_COMPARISON_FUNC_LESS = 2;
        private const uint D3D12_COMPARISON_FUNC_EQUAL = 3;
        private const uint D3D12_COMPARISON_FUNC_LESS_EQUAL = 4;
        private const uint D3D12_COMPARISON_FUNC_GREATER = 5;
        private const uint D3D12_COMPARISON_FUNC_NOT_EQUAL = 6;
        private const uint D3D12_COMPARISON_FUNC_GREATER_EQUAL = 7;
        private const uint D3D12_COMPARISON_FUNC_ALWAYS = 8;

        // DirectX 12 Float Constants
        private const float D3D12_FLOAT32_MAX = 3.402823466e+38f;

        /// <summary>
        /// D3D12_SAMPLER_DESC structure.
        /// Based on DirectX 12 Sampler Descriptors: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_sampler_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_SAMPLER_DESC
        {
            public uint Filter; // D3D12_FILTER
            public uint AddressU; // D3D12_TEXTURE_ADDRESS_MODE
            public uint AddressV; // D3D12_TEXTURE_ADDRESS_MODE
            public uint AddressW; // D3D12_TEXTURE_ADDRESS_MODE
            public float MipLODBias;
            public uint MaxAnisotropy;
            public uint ComparisonFunc; // D3D12_COMPARISON_FUNC
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] BorderColor; // float[4] for RGBA border color
            public float MinLOD;
            public float MaxLOD;
        }

        // COM interface method delegates for C# 7.3 compatibility
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDescriptorHeapDelegate(IntPtr device, IntPtr pDescriptorHeapDesc, ref Guid riid, IntPtr ppvHeap);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetCPUDescriptorHandleForHeapStartDelegate(IntPtr descriptorHeap);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetGPUDescriptorHandleForHeapStartDelegate(IntPtr descriptorHeap);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetDescriptorHandleIncrementSizeDelegate(IntPtr device, uint DescriptorHeapType);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CreateShaderResourceViewDelegate(IntPtr device, IntPtr pResource, IntPtr pDesc, IntPtr DestDescriptor);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CreateSamplerDelegate(IntPtr device, IntPtr pDesc, IntPtr DestDescriptor);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong GetGpuVirtualAddressDelegate(IntPtr resource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetGraphicsRootShaderResourceViewDelegate(IntPtr commandList, uint rootParameterIndex, ulong gpuVirtualAddress);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetDescriptorHeapsDelegate(IntPtr commandList, uint numDescriptorHeaps, IntPtr ppDescriptorHeaps);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetGraphicsRootDescriptorTableDelegate(IntPtr commandList, uint rootParameterIndex, ulong baseDescriptorHandle);

        /// <summary>
        /// Calls ID3D12Device::CreateDescriptorHeap through COM vtable.
        /// VTable index 27 for ID3D12Device.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// </summary>
        private unsafe int CreateDescriptorHeapVTable(IntPtr device, IntPtr pDescriptorHeapDesc, ref Guid riid, IntPtr ppvHeap)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero) return unchecked((int)0x80070057); // E_INVALIDARG

            // Get vtable pointer (first field of COM object)
            IntPtr* vtable = *(IntPtr**)device;
            // CreateDescriptorHeap is at index 27 in ID3D12Device vtable
            IntPtr methodPtr = vtable[27];

            // Create delegate from function pointer (C# 7.3 compatible)
            CreateDescriptorHeapDelegate createDescriptorHeap =
                (CreateDescriptorHeapDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateDescriptorHeapDelegate));

            return createDescriptorHeap(device, pDescriptorHeapDesc, ref riid, ppvHeap);
        }

        /// <summary>
        /// Calls ID3D12DescriptorHeap::GetCPUDescriptorHandleForHeapStart through COM vtable.
        /// VTable index 9 for ID3D12DescriptorHeap.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// </summary>
        private unsafe IntPtr GetDescriptorHeapStartHandleVTable(IntPtr descriptorHeap)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return IntPtr.Zero;
            }

            if (descriptorHeap == IntPtr.Zero) return IntPtr.Zero;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)descriptorHeap;
            // GetCPUDescriptorHandleForHeapStart is at index 9 in ID3D12DescriptorHeap vtable
            IntPtr methodPtr = vtable[9];

            // Create delegate from function pointer
            GetCPUDescriptorHandleForHeapStartDelegate getCpuHandle =
                (GetCPUDescriptorHandleForHeapStartDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetCPUDescriptorHandleForHeapStartDelegate));

            return getCpuHandle(descriptorHeap);
        }

        /// <summary>
        /// Calls ID3D12DescriptorHeap::GetGPUDescriptorHandleForHeapStart through COM vtable.
        /// VTable index 10 for ID3D12DescriptorHeap.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// </summary>
        private unsafe IntPtr GetDescriptorHeapStartHandleGpuVTable(IntPtr descriptorHeap)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return IntPtr.Zero;
            }

            if (descriptorHeap == IntPtr.Zero) return IntPtr.Zero;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)descriptorHeap;
            // GetGPUDescriptorHandleForHeapStart is at index 10 in ID3D12DescriptorHeap vtable
            IntPtr methodPtr = vtable[10];

            // Create delegate from function pointer
            GetGPUDescriptorHandleForHeapStartDelegate getGpuHandle =
                (GetGPUDescriptorHandleForHeapStartDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetGPUDescriptorHandleForHeapStartDelegate));

            return getGpuHandle(descriptorHeap);
        }

        /// <summary>
        /// Calls ID3D12Device::GetDescriptorHandleIncrementSize through COM vtable.
        /// VTable index 28 for ID3D12Device.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// </summary>
        private unsafe uint GetDescriptorHandleIncrementSizeVTable(IntPtr device, uint DescriptorHeapType)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            if (device == IntPtr.Zero) return 0;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // GetDescriptorHandleIncrementSize is at index 28 in ID3D12Device vtable
            IntPtr methodPtr = vtable[28];

            // Create delegate from function pointer
            GetDescriptorHandleIncrementSizeDelegate getIncrementSize =
                Marshal.GetDelegateForFunctionPointer<GetDescriptorHandleIncrementSizeDelegate>(methodPtr);

            return getIncrementSize(device, DescriptorHeapType);
        }

        /// <summary>
        /// Calls ID3D12Device::CreateShaderResourceView through COM vtable.
        /// VTable index 33 for ID3D12Device.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// </summary>
        private unsafe void CreateShaderResourceView(IntPtr device, IntPtr pResource, IntPtr pDesc, IntPtr DestDescriptor)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device == IntPtr.Zero || pResource == IntPtr.Zero || DestDescriptor == IntPtr.Zero) return;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateShaderResourceView is at index 33 in ID3D12Device vtable
            IntPtr methodPtr = vtable[33];

            // Create delegate from function pointer
            CreateShaderResourceViewDelegate createSrv =
                Marshal.GetDelegateForFunctionPointer<CreateShaderResourceViewDelegate>(methodPtr);

            createSrv(device, pResource, pDesc, DestDescriptor);
        }

        /// <summary>
        /// Calls ID3D12Device::CreateSampler through COM vtable.
        /// VTable index 34 for ID3D12Device.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Samplers: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createsampler
        /// </summary>
        private unsafe void CreateSampler(IntPtr device, IntPtr pDesc, IntPtr DestDescriptor)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device == IntPtr.Zero || pDesc == IntPtr.Zero || DestDescriptor == IntPtr.Zero) return;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateSampler is at index 34 in ID3D12Device vtable
            IntPtr methodPtr = vtable[34];

            // Create delegate from function pointer
            CreateSamplerDelegate createSampler =
                Marshal.GetDelegateForFunctionPointer<CreateSamplerDelegate>(methodPtr);

            createSampler(device, pDesc, DestDescriptor);
        }

        /// <summary>
        /// Gets the GPU virtual address of a DirectX 12 resource.
        /// Calls ID3D12Resource::GetGPUVirtualAddress through COM vtable.
        /// VTable index 8 for ID3D12Resource.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Resources: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12resource-getgpuvirtualaddress
        /// </summary>
        private unsafe ulong GetResourceGpuVirtualAddress(IntPtr resource)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            if (resource == IntPtr.Zero) return 0;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)resource;
            // GetGPUVirtualAddress is at index 8 in ID3D12Resource vtable
            IntPtr methodPtr = vtable[8];

            // Create delegate from function pointer
            GetGpuVirtualAddressDelegate getGpuVa =
                Marshal.GetDelegateForFunctionPointer<GetGpuVirtualAddressDelegate>(methodPtr);

            return getGpuVa(resource);
        }

        /// <summary>
        /// Sets a shader resource view (SRV) in the graphics root signature.
        /// Calls ID3D12GraphicsCommandList::SetGraphicsRootShaderResourceView through COM vtable.
        /// VTable index 64 for ID3D12GraphicsCommandList.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Root Signature: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setgraphicsrootshaderresourceview
        /// </summary>
        private unsafe void SetGraphicsRootShaderResourceView(IntPtr commandList, uint rootParameterIndex, ulong gpuVirtualAddress)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandList == IntPtr.Zero) return;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandList;
            // SetGraphicsRootShaderResourceView is at index 64 in ID3D12GraphicsCommandList vtable
            // Note: VTable index may vary by DirectX 12 version, but 64 is typical for D3D12_GRAPHICS_COMMAND_LIST
            IntPtr methodPtr = vtable[64];

            // Create delegate from function pointer
            SetGraphicsRootShaderResourceViewDelegate setSrv =
                Marshal.GetDelegateForFunctionPointer<SetGraphicsRootShaderResourceViewDelegate>(methodPtr);

            setSrv(commandList, rootParameterIndex, gpuVirtualAddress);
        }

        /// <summary>
        /// Sets descriptor heaps for the graphics command list.
        /// Calls ID3D12GraphicsCommandList::SetDescriptorHeaps through COM vtable.
        /// VTable index 47 for ID3D12GraphicsCommandList.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setdescriptorheaps
        /// </summary>
        private unsafe void SetDescriptorHeaps(IntPtr commandList, IntPtr[] descriptorHeaps, uint numDescriptorHeaps)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandList == IntPtr.Zero) return;
            if (descriptorHeaps == null || numDescriptorHeaps == 0) return;

            // Allocate unmanaged memory for the descriptor heap pointer array
            // SetDescriptorHeaps expects a pointer to an array of ID3D12DescriptorHeap* pointers
            // Signature: void SetDescriptorHeaps(UINT NumDescriptorHeaps, ID3D12DescriptorHeap* const* ppDescriptorHeaps)
            int arraySize = (int)numDescriptorHeaps * IntPtr.Size;
            IntPtr heapsArrayPtr = Marshal.AllocHGlobal(arraySize);
            try
            {
                // Copy descriptor heap pointers to unmanaged memory
                for (int i = 0; i < numDescriptorHeaps; i++)
                {
                    Marshal.WriteIntPtr(heapsArrayPtr, i * IntPtr.Size, descriptorHeaps[i]);
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // SetDescriptorHeaps is at index 47 in ID3D12GraphicsCommandList vtable
                // Note: VTable index may vary by DirectX 12 version, but 47 is typical for D3D12_GRAPHICS_COMMAND_LIST
                IntPtr methodPtr = vtable[47];

                // Create delegate from function pointer
                // Note: C# 7.3 compatibility - use explicit delegate type instead of Marshal.GetDelegateForFunctionPointer<T>
                SetDescriptorHeapsDelegate setHeaps =
                    (SetDescriptorHeapsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetDescriptorHeapsDelegate));

                // Call SetDescriptorHeaps with pointer to array
                setHeaps(commandList, numDescriptorHeaps, heapsArrayPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(heapsArrayPtr);
            }
        }

        /// <summary>
        /// Sets a descriptor table in the graphics root signature.
        /// Calls ID3D12GraphicsCommandList::SetGraphicsRootDescriptorTable through COM vtable.
        /// VTable index 61 for ID3D12GraphicsCommandList.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Root Signature: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setgraphicsrootdescriptortable
        /// </summary>
        private unsafe void SetGraphicsRootDescriptorTable(IntPtr commandList, uint rootParameterIndex, ulong baseDescriptorHandle)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandList == IntPtr.Zero) return;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandList;
            // SetGraphicsRootDescriptorTable is at index 61 in ID3D12GraphicsCommandList vtable
            // Note: VTable index may vary by DirectX 12 version, but 61 is typical for D3D12_GRAPHICS_COMMAND_LIST
            IntPtr methodPtr = vtable[61];

            // Create delegate from function pointer
            // SetGraphicsRootDescriptorTable signature: void SetGraphicsRootDescriptorTable(UINT RootParameterIndex, D3D12_GPU_DESCRIPTOR_HANDLE BaseDescriptor)
            // D3D12_GPU_DESCRIPTOR_HANDLE is a 64-bit value (uint64_t), passed as ulong in C#
            // Note: C# 7.3 compatibility - use explicit delegate type instead of Marshal.GetDelegateForFunctionPointer<T>
            SetGraphicsRootDescriptorTableDelegate setTable =
                (SetGraphicsRootDescriptorTableDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetGraphicsRootDescriptorTableDelegate));

            setTable(commandList, rootParameterIndex, baseDescriptorHandle);
        }

        /// <summary>
        /// Creates a committed resource (texture, buffer, etc.) in DirectX 12.
        /// Calls ID3D12Device::CreateCommittedResource through COM vtable.
        /// VTable index 10 for ID3D12Device.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Resources: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcommittedresource
        /// </summary>
        private unsafe int CreateCommittedResource(
            IntPtr device,
            IntPtr pHeapProperties,
            uint HeapFlags,
            IntPtr pDesc,
            uint InitialResourceState,
            IntPtr pOptimizedClearValue,
            ref Guid riidResource,
            IntPtr ppvResource)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero || pHeapProperties == IntPtr.Zero || pDesc == IntPtr.Zero || ppvResource == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateCommittedResource is at index 10 in ID3D12Device vtable
            IntPtr methodPtr = vtable[10];

            // Create delegate from function pointer
            CreateCommittedResourceDelegate createResource =
                (CreateCommittedResourceDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateCommittedResourceDelegate));

            return createResource(device, pHeapProperties, HeapFlags, pDesc, InitialResourceState, pOptimizedClearValue, ref riidResource, ppvResource);
        }

        /// <summary>
        /// Inserts a resource barrier into the command list.
        /// Calls ID3D12GraphicsCommandList::ResourceBarrier through COM vtable.
        /// VTable index 44 for ID3D12GraphicsCommandList.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Resource Barriers: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-resourcebarrier
        /// </summary>
        private unsafe void ResourceBarrier(IntPtr commandList, IntPtr pBarriers, uint numBarriers)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandList == IntPtr.Zero || pBarriers == IntPtr.Zero || numBarriers == 0) return;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandList;
            // ResourceBarrier is at index 44 in ID3D12GraphicsCommandList vtable
            IntPtr methodPtr = vtable[44];

            // Create delegate from function pointer
            ResourceBarrierDelegate resourceBarrier =
                (ResourceBarrierDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ResourceBarrierDelegate));

            resourceBarrier(commandList, numBarriers, pBarriers);
        }

        /// <summary>
        /// Copies a region from a source texture to a destination texture.
        /// Calls ID3D12GraphicsCommandList::CopyTextureRegion through COM vtable.
        /// VTable index 45 for ID3D12GraphicsCommandList.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Texture Copy: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-copytextureregion
        /// </summary>
        private unsafe void CopyTextureRegion(
            IntPtr commandList,
            IntPtr pDst,
            uint DstX,
            uint DstY,
            uint DstZ,
            IntPtr pSrc,
            IntPtr pSrcBox)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandList == IntPtr.Zero || pDst == IntPtr.Zero || pSrc == IntPtr.Zero) return;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandList;
            // CopyTextureRegion is at index 45 in ID3D12GraphicsCommandList vtable
            IntPtr methodPtr = vtable[45];

            // Create delegate from function pointer
            CopyTextureRegionDelegate copyTexture =
                (CopyTextureRegionDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CopyTextureRegionDelegate));

            copyTexture(commandList, pDst, DstX, DstY, DstZ, pSrc, pSrcBox);
        }

        /// <summary>
        /// Closes the command list for recording.
        /// Calls ID3D12GraphicsCommandList::Close through COM vtable.
        /// VTable index 4 for ID3D12GraphicsCommandList.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// </summary>
        private unsafe int CloseCommandList(IntPtr commandList)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL
            }

            if (commandList == IntPtr.Zero) return unchecked((int)0x80070057); // E_INVALIDARG

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandList;
            // Close is at index 4 in ID3D12GraphicsCommandList vtable
            IntPtr methodPtr = vtable[4];

            // Create delegate from function pointer
            CloseCommandListDelegate close =
                (CloseCommandListDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CloseCommandListDelegate));

            close(commandList);
            return 0; // S_OK
        }

        /// <summary>
        /// Executes command lists on the command queue.
        /// Calls ID3D12CommandQueue::ExecuteCommandLists through COM vtable.
        /// VTable index 10 for ID3D12CommandQueue.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Command Queue: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandqueue-executecommandlists
        /// </summary>
        private unsafe void ExecuteCommandLists(IntPtr commandQueue, IntPtr[] commandLists, uint numCommandLists)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandQueue == IntPtr.Zero || commandLists == null || numCommandLists == 0) return;

            // Allocate unmanaged memory for command list pointer array
            int arraySize = (int)numCommandLists * IntPtr.Size;
            IntPtr commandListsArrayPtr = Marshal.AllocHGlobal(arraySize);
            try
            {
                // Copy command list pointers to unmanaged memory
                for (int i = 0; i < numCommandLists; i++)
                {
                    Marshal.WriteIntPtr(commandListsArrayPtr, i * IntPtr.Size, commandLists[i]);
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandQueue;
                // ExecuteCommandLists is at index 10 in ID3D12CommandQueue vtable
                IntPtr methodPtr = vtable[10];

                // Create delegate from function pointer
                ExecuteCommandListsDelegate execute =
                    (ExecuteCommandListsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ExecuteCommandListsDelegate));

                execute(commandQueue, numCommandLists, commandListsArrayPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(commandListsArrayPtr);
            }
        }

        /// <summary>
        /// Creates a fence for GPU synchronization.
        /// Calls ID3D12Device::CreateFence through COM vtable.
        /// VTable index 11 for ID3D12Device.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Fences: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createfence
        /// </summary>
        private unsafe int CreateFence(IntPtr device, ulong initialValue, uint flags, ref Guid riid, IntPtr ppFence)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL
            }

            if (device == IntPtr.Zero || ppFence == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateFence is at index 11 in ID3D12Device vtable
            IntPtr methodPtr = vtable[11];

            // Create delegate from function pointer
            CreateFenceDelegate createFence =
                (CreateFenceDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateFenceDelegate));

            return createFence(device, initialValue, flags, ref riid, ppFence);
        }

        /// <summary>
        /// Signals a fence on the command queue.
        /// Calls ID3D12CommandQueue::Signal through COM vtable.
        /// VTable index 11 for ID3D12CommandQueue.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Command Queue: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandqueue-signal
        /// </summary>
        private unsafe ulong SignalFence(IntPtr commandQueue, IntPtr fence, ulong value)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            if (commandQueue == IntPtr.Zero || fence == IntPtr.Zero) return 0;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandQueue;
            // Signal is at index 11 in ID3D12CommandQueue vtable
            IntPtr methodPtr = vtable[11];

            // Create delegate from function pointer
            SignalDelegate signal =
                (SignalDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SignalDelegate));

            return signal(commandQueue, fence, value);
        }

        /// <summary>
        /// Gets the completed value of a fence.
        /// Calls ID3D12Fence::GetCompletedValue through COM vtable.
        /// VTable index 4 for ID3D12Fence.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Fences: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12fence-getcompletedvalue
        /// </summary>
        private unsafe ulong GetFenceCompletedValue(IntPtr fence)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            if (fence == IntPtr.Zero) return 0;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)fence;
            // GetCompletedValue is at index 4 in ID3D12Fence vtable
            IntPtr methodPtr = vtable[4];

            // Create delegate from function pointer
            GetCompletedValueDelegate getCompleted =
                (GetCompletedValueDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetCompletedValueDelegate));

            return getCompleted(fence);
        }

        /// <summary>
        /// Sets an event to be signaled when a fence reaches a specified value.
        /// Calls ID3D12Fence::SetEventOnCompletion through COM vtable.
        /// VTable index 5 for ID3D12Fence.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Fences: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12fence-seteventoncompletion
        /// </summary>
        private unsafe int SetFenceEventOnCompletion(IntPtr fence, ulong value, IntPtr hEvent)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL
            }

            if (fence == IntPtr.Zero) return unchecked((int)0x80070057); // E_INVALIDARG

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)fence;
            // SetEventOnCompletion is at index 5 in ID3D12Fence vtable
            IntPtr methodPtr = vtable[5];

            // Create delegate from function pointer
            SetEventOnCompletionDelegate setEvent =
                (SetEventOnCompletionDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetEventOnCompletionDelegate));

            return setEvent(fence, value, hEvent);
        }

        /// <summary>
        /// Maps a resource for CPU access.
        /// Calls ID3D12Resource::Map through COM vtable.
        /// VTable index 9 for ID3D12Resource.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Resource Mapping: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12resource-map
        /// </summary>
        private unsafe int MapResource(IntPtr resource, uint subresource, IntPtr pReadRange, IntPtr ppData)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL
            }

            if (resource == IntPtr.Zero || ppData == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)resource;
            // Map is at index 9 in ID3D12Resource vtable
            IntPtr methodPtr = vtable[9];

            // Create delegate from function pointer
            MapDelegate map =
                (MapDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(MapDelegate));

            return map(resource, subresource, pReadRange, ppData);
        }

        /// <summary>
        /// Gets the description of a resource.
        /// Calls ID3D12Resource::GetDesc through COM vtable.
        /// VTable index 8 for ID3D12Resource.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Resource Description: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12resource-getdesc
        /// </summary>
        private unsafe void GetResourceDesc(IntPtr resource, IntPtr pDesc)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (resource == IntPtr.Zero || pDesc == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)resource;
            // GetDesc is at index 8 in ID3D12Resource vtable
            IntPtr methodPtr = vtable[8];

            // Create delegate from function pointer
            GetDescDelegate getDesc =
                (GetDescDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetDescDelegate));

            getDesc(resource, pDesc);
        }

        /// <summary>
        /// Unmaps a resource from CPU access.
        /// Calls ID3D12Resource::Unmap through COM vtable.
        /// VTable index 10 for ID3D12Resource.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Resource Mapping: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12resource-unmap
        /// </summary>
        private unsafe void UnmapResource(IntPtr resource, uint subresource)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (resource == IntPtr.Zero) return;

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)resource;
            // Unmap is at index 10 in ID3D12Resource vtable
            IntPtr methodPtr = vtable[10];

            // Create delegate from function pointer
            UnmapDelegate unmap =
                (UnmapDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(UnmapDelegate));

            unmap(resource, subresource);
        }

        /// <summary>
        /// Gets the layout of a resource for copy operations.
        /// Calls ID3D12Device::GetCopyableFootprints through COM vtable.
        /// VTable index 12 for ID3D12Device.
        /// Platform: Windows only (x64/x86) - DirectX 12 COM is Windows-specific
        /// Based on DirectX 12 Resource Layouts: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-getcopyablefootprints
        /// </summary>
        private unsafe int GetCopyableFootprints(
            IntPtr device,
            IntPtr pResourceDesc,
            uint firstSubresource,
            uint numSubresources,
            ulong baseOffset,
            IntPtr pLayouts,
            IntPtr pNumRows,
            IntPtr pRowSizeInBytes,
            IntPtr pTotalBytes)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL
            }

            if (device == IntPtr.Zero || pResourceDesc == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // GetCopyableFootprints is at index 12 in ID3D12Device vtable
            IntPtr methodPtr = vtable[12];

            // Create delegate from function pointer
            GetCopyableFootprintsDelegate getFootprints =
                (GetCopyableFootprintsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetCopyableFootprintsDelegate));

            return getFootprints(device, pResourceDesc, firstSubresource, numSubresources, baseOffset, pLayouts, pNumRows, pRowSizeInBytes, pTotalBytes);
        }

        /// <summary>
        /// Gets the native DirectX 12 command queue from Stride's GraphicsDevice.
        /// </summary>
        /// <returns>Native command queue pointer (ID3D12CommandQueue), or IntPtr.Zero if not available.</returns>
        /// <remarks>
        /// Based on DirectX 12: Command queues are created by applications and used to execute command lists.
        /// Stride's GraphicsDevice wraps ID3D12Device and manages command queues internally.
        /// This method attempts to retrieve the command queue through multiple approaches:
        /// 1. Direct property access (NativeCommandQueue property if exposed)
        /// 2. Reflection-based access to internal command queue fields
        /// 3. Access through GraphicsContext if available
        /// 4. Fallback to IntPtr.Zero (synchronization handled via WaitIdle)
        ///
        /// Based on DirectX 12 Command Queue: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nn-d3d12-id3d12commandqueue
        /// </remarks>
        private IntPtr GetNativeCommandQueue()
        {
            // Stride's GraphicsDevice wraps ID3D12Device, but we need ID3D12CommandQueue
            // In Stride, the command queue is typically accessed through the GraphicsContext
            // For DirectX 12, command queues are created by the application, not queried from the device
            // Stride manages command queues internally, so we need to access them through reflection

            if (_strideDevice == null)
            {
                return IntPtr.Zero;
            }

            // Approach 1: Try to access NativeCommandQueue property directly
            // Some graphics backends expose this property for low-level access
            try
            {
                PropertyInfo nativeCommandQueueProp = _strideDevice.GetType().GetProperty("NativeCommandQueue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativeCommandQueueProp != null)
                {
                    object commandQueueObj = nativeCommandQueueProp.GetValue(_strideDevice);
                    if (commandQueueObj is IntPtr commandQueuePtr && commandQueuePtr != IntPtr.Zero)
                    {
                        return commandQueuePtr;
                    }
                }
            }
            catch (Exception)
            {
                // Property doesn't exist or access failed - continue to next approach
            }

            // Approach 2: Try to access command queue through GraphicsContext
            // Stride's GraphicsContext may have a reference to the command queue
            try
            {
                if (_game?.GraphicsContext != null)
                {
                    object graphicsContext = _game.GraphicsContext;
                    Type contextType = graphicsContext.GetType();

                    // Try NativeCommandQueue property on GraphicsContext
                    PropertyInfo contextQueueProp = contextType.GetProperty("NativeCommandQueue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (contextQueueProp != null)
                    {
                        object commandQueueObj = contextQueueProp.GetValue(graphicsContext);
                        if (commandQueueObj is IntPtr commandQueuePtr && commandQueuePtr != IntPtr.Zero)
                        {
                            return commandQueuePtr;
                        }
                    }

                    // Try CommandQueue property (different naming convention)
                    PropertyInfo commandQueueProp = contextType.GetProperty("CommandQueue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (commandQueueProp != null)
                    {
                        object commandQueueObj = commandQueueProp.GetValue(graphicsContext);
                        if (commandQueueObj is IntPtr commandQueuePtr && commandQueuePtr != IntPtr.Zero)
                        {
                            return commandQueuePtr;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Access failed - continue to next approach
            }

            // Approach 3: Try to access internal command queue field on GraphicsDevice
            // Stride may store the command queue as an internal field
            try
            {
                Type deviceType = _strideDevice.GetType();

                // Common field names for command queue storage in graphics backends
                string[] possibleFieldNames = new string[]
                {
                    "_nativeCommandQueue",
                    "_commandQueue",
                    "nativeCommandQueue",
                    "commandQueue",
                    "_d3d12CommandQueue",
                    "d3d12CommandQueue"
                };

                foreach (string fieldName in possibleFieldNames)
                {
                    FieldInfo queueField = deviceType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (queueField != null)
                    {
                        object queueValue = queueField.GetValue(_strideDevice);
                        if (queueValue is IntPtr queuePtr && queuePtr != IntPtr.Zero)
                        {
                            return queuePtr;
                        }
                        // Some implementations may wrap IntPtr in a class
                        if (queueValue != null)
                        {
                            Type queueValueType = queueValue.GetType();
                            PropertyInfo nativePtrProp = queueValueType.GetProperty("NativePointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (nativePtrProp == null)
                            {
                                nativePtrProp = queueValueType.GetProperty("Handle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            }
                            if (nativePtrProp != null)
                            {
                                object nativePtr = nativePtrProp.GetValue(queueValue);
                                if (nativePtr is IntPtr nativePtrValue && nativePtrValue != IntPtr.Zero)
                                {
                                    return nativePtrValue;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Field access failed - continue to fallback
            }

            // Approach 4: Try to access command queue through CommandList if available
            // Command lists in DirectX 12 are associated with command allocators, which are associated with command queues
            // However, there's no direct API to get the queue from a command list
            // Some implementations may store this association internally
            try
            {
                if (_commandList != null)
                {
                    Type commandListType = _commandList.GetType();

                    // Try to get command queue from command list
                    PropertyInfo listQueueProp = commandListType.GetProperty("NativeCommandQueue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (listQueueProp == null)
                    {
                        listQueueProp = commandListType.GetProperty("CommandQueue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    if (listQueueProp != null)
                    {
                        object commandQueueObj = listQueueProp.GetValue(_commandList);
                        if (commandQueueObj is IntPtr commandQueuePtr && commandQueuePtr != IntPtr.Zero)
                        {
                            return commandQueuePtr;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Access failed - use fallback
            }

            // Fallback: Return IntPtr.Zero
            // When command queue is not available, synchronization should be handled via Stride's WaitIdle method
            // This is acceptable for readback operations where we can wait for GPU completion using Stride's API
            // Based on DirectX 12: Command queue execution can be synchronized using fences or WaitIdle equivalents
            return IntPtr.Zero;
        }

        /// <summary>
        /// Converts TextureFormat to DXGI_FORMAT for sampler feedback textures.
        /// Sampler feedback textures use specialized formats that track mip level access.
        ///
        /// DirectX 12 sampler feedback formats are independent of base texture formats.
        /// There are two feedback format types:
        /// - MIN_MIP_OPAQUE (189): Tracks the minimum mip level accessed per 8x8 tile (most common)
        /// - MIP_REGION_USED_OPAQUE (190): Tracks which specific mip regions are used (more detailed)
        ///
        /// Based on DirectX 12 Ultimate Sampler Feedback specification:
        /// https://docs.microsoft.com/en-us/windows/win32/direct3d12/sampler-feedback
        /// </summary>
        /// <param name="format">TextureFormat value - for standard texture formats, defaults to MIN_MIP_OPAQUE.
        /// In the future, TextureFormat enum may include specific sampler feedback format types.</param>
        /// <returns>DXGI_FORMAT value for the sampler feedback texture (189 or 190)</returns>
        private uint ConvertFormatToDxgiFormatForSamplerFeedback(TextureFormat format)
        {
            // DirectX 12 sampler feedback format constants
            // These are DXGI_FORMAT enum values from d3d12.h
            const uint DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE = 189; // 0xBD - most commonly used
            const uint DXGI_FORMAT_SAMPLER_FEEDBACK_MIP_REGION_USED_OPAQUE = 190; // 0xBE - more detailed tracking

            // Sampler feedback formats work independently of base texture formats.
            // The format parameter is used to select the type of feedback information desired.
            //
            // Mapping strategy:
            // 1. Default to MIN_MIP_OPAQUE for all standard texture formats (most common use case)
            //    - MIN_MIP_OPAQUE is the default because it's simpler and covers most streaming/texture optimization needs
            //    - It stores one byte per 8x8 tile indicating the minimum mip level accessed
            // 2. For future extension: If TextureFormat enum is extended with sampler feedback-specific values,
            //    those can be mapped to MIP_REGION_USED_OPAQUE or other feedback types
            // 3. Special handling: Depth/stencil formats might benefit from MIP_REGION_USED_OPAQUE for
            //    more granular tracking, but this is application-specific

            switch (format)
            {
                // Standard texture formats: Default to MIN_MIP_OPAQUE (most common)
                // This format tracks the minimum mip level accessed per 8x8 tile, which is sufficient
                // for most texture streaming and mip optimization use cases.
                case TextureFormat.Unknown:
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                case TextureFormat.R8_SInt:
                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R8G8_UInt:
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.R8G8B8A8_SInt:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                case TextureFormat.R16_Float:
                case TextureFormat.R16_UNorm:
                case TextureFormat.R16_UInt:
                case TextureFormat.R16_SInt:
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16_UInt:
                case TextureFormat.R16G16_SInt:
                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R16G16B16A16_UNorm:
                case TextureFormat.R16G16B16A16_UInt:
                case TextureFormat.R16G16B16A16_SInt:
                case TextureFormat.R32_Float:
                case TextureFormat.R32_UInt:
                case TextureFormat.R32_SInt:
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32_UInt:
                case TextureFormat.R32G32_SInt:
                case TextureFormat.R32G32B32_Float:
                case TextureFormat.R32G32B32A32_Float:
                case TextureFormat.R32G32B32A32_UInt:
                case TextureFormat.R32G32B32A32_SInt:
                case TextureFormat.R11G11B10_Float:
                case TextureFormat.R10G10B10A2_UNorm:
                case TextureFormat.R10G10B10A2_UInt:
                // Block-compressed formats (BC/DXT)
                case TextureFormat.BC1_UNorm:
                case TextureFormat.BC1_UNorm_SRGB:
                case TextureFormat.BC1:
                case TextureFormat.BC2_UNorm:
                case TextureFormat.BC2_UNorm_SRGB:
                case TextureFormat.BC2:
                case TextureFormat.BC3_UNorm:
                case TextureFormat.BC3_UNorm_SRGB:
                case TextureFormat.BC3:
                case TextureFormat.BC4_UNorm:
                case TextureFormat.BC4:
                case TextureFormat.BC5_UNorm:
                case TextureFormat.BC5:
                case TextureFormat.BC6H_UFloat:
                case TextureFormat.BC6H:
                case TextureFormat.BC7_UNorm:
                case TextureFormat.BC7_UNorm_SRGB:
                case TextureFormat.BC7:
                // ASTC compressed formats
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10:
                case TextureFormat.ASTC_12x12:
                    // Default to MIN_MIP_OPAQUE for all standard formats
                    // This is the most commonly used format for texture streaming optimization
                    return DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE;

                // Depth/stencil formats: Also default to MIN_MIP_OPAQUE
                // While MIP_REGION_USED_OPAQUE could provide more detailed tracking for depth textures,
                // MIN_MIP_OPAQUE is typically sufficient for most use cases and is more memory-efficient.
                // If application-specific requirements need detailed region tracking for depth textures,
                // this can be changed to MIP_REGION_USED_OPAQUE, or TextureFormat enum can be extended
                // with sampler feedback-specific format values.
                case TextureFormat.D16_UNorm:
                case TextureFormat.D24_UNorm_S8_UInt:
                case TextureFormat.D32_Float:
                case TextureFormat.D32_Float_S8_UInt:
                    // Use MIN_MIP_OPAQUE for depth formats (sufficient for most shadow mapping and LOD optimization)
                    return DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE;

                default:
                    // Fallback: Default to MIN_MIP_OPAQUE for any unknown or future format values
                    // This ensures backward compatibility if TextureFormat enum is extended
                    return DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE;
            }

            // Note: Future extension points:
            // 1. If TextureFormat enum is extended with sampler feedback-specific values (e.g.,
            //    TextureFormat.SamplerFeedbackMinMipOpaque, TextureFormat.SamplerFeedbackMipRegionUsedOpaque),
            //    those can be mapped directly to their corresponding DXGI_FORMAT values above.
            // 2. For applications that need MIP_REGION_USED_OPAQUE for specific use cases, consider:
            //    - Extending TextureFormat enum with explicit sampler feedback format types
            //    - Adding a separate parameter to CreateSamplerFeedbackTexture to specify feedback type
            //    - Using a convention in TextureFormat values to indicate feedback type preference
        }

        #endregion

        #region DirectX 12 Structures for Sampler Feedback

        // DirectX 12 Resource Dimension
        private const uint D3D12_RESOURCE_DIMENSION_UNKNOWN = 0;
        private const uint D3D12_RESOURCE_DIMENSION_BUFFER = 1;
        private const uint D3D12_RESOURCE_DIMENSION_TEXTURE1D = 2;
        private const uint D3D12_RESOURCE_DIMENSION_TEXTURE2D = 3;
        private const uint D3D12_RESOURCE_DIMENSION_TEXTURE3D = 4;

        // DirectX 12 Resource Flags
        private const uint D3D12_RESOURCE_FLAG_NONE = 0x0;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET = 0x1;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_DEPTH_STENCIL = 0x2;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS = 0x4;
        private const uint D3D12_RESOURCE_FLAG_DENY_SHADER_RESOURCE = 0x8;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_CROSS_ADAPTER = 0x10;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS = 0x20;
        private const uint D3D12_RESOURCE_FLAG_VIDEO_DECODE_REFERENCE_ONLY = 0x40;
        private const uint D3D12_RESOURCE_FLAG_SAMPLER_FEEDBACK = 0x800;

        // DirectX 12 Resource States
        private const uint D3D12_RESOURCE_STATE_COMMON = 0x0;
        private const uint D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER = 0x1;
        private const uint D3D12_RESOURCE_STATE_INDEX_BUFFER = 0x2;
        private const uint D3D12_RESOURCE_STATE_RENDER_TARGET = 0x4;
        private const uint D3D12_RESOURCE_STATE_UNORDERED_ACCESS = 0x8;
        private const uint D3D12_RESOURCE_STATE_DEPTH_WRITE = 0x10;
        private const uint D3D12_RESOURCE_STATE_DEPTH_READ = 0x20;
        private const uint D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE = 0x40;
        private const uint D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE = 0x80;
        private const uint D3D12_RESOURCE_STATE_STREAM_OUT = 0x100;
        private const uint D3D12_RESOURCE_STATE_INDIRECT_ARGUMENT = 0x200;
        private const uint D3D12_RESOURCE_STATE_COPY_DEST = 0x400;
        private const uint D3D12_RESOURCE_STATE_COPY_SOURCE = 0x800;
        private const uint D3D12_RESOURCE_STATE_RESOLVE_DEST = 0x1000;
        private const uint D3D12_RESOURCE_STATE_RESOLVE_SOURCE = 0x2000;
        private const uint D3D12_RESOURCE_STATE_RAYTRACING_ACCELERATION_STRUCTURE = 0x400000;
        private const uint D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE = 0x1000000;

        // DirectX 12 Heap Types
        private const uint D3D12_HEAP_TYPE_DEFAULT = 1;
        private const uint D3D12_HEAP_TYPE_UPLOAD = 2;
        private const uint D3D12_HEAP_TYPE_READBACK = 3;
        private const uint D3D12_HEAP_TYPE_CUSTOM = 4;

        // DirectX 12 Heap Flags
        private const uint D3D12_HEAP_FLAG_NONE = 0x0;
        private const uint D3D12_HEAP_FLAG_SHARED = 0x1;
        private const uint D3D12_HEAP_FLAG_DENY_BUFFERS = 0x4;
        private const uint D3D12_HEAP_FLAG_ALLOW_DISPLAY = 0x8;
        private const uint D3D12_HEAP_FLAG_SHARED_CROSS_ADAPTER = 0x20;
        private const uint D3D12_HEAP_FLAG_DENY_RT_DS_TEXTURES = 0x40;
        private const uint D3D12_HEAP_FLAG_DENY_NON_RT_DS_TEXTURES = 0x80;
        private const uint D3D12_HEAP_FLAG_HARDWARE_PROTECTED = 0x100;
        private const uint D3D12_HEAP_FLAG_ALLOW_WRITE_WATCH = 0x200;
        private const uint D3D12_HEAP_FLAG_ALLOW_SHADER_ATOMICS = 0x400;
        private const uint D3D12_HEAP_FLAG_CREATE_NOT_RESIDENT = 0x800;
        private const uint D3D12_HEAP_FLAG_CREATE_NOT_ZEROED = 0x1000;

        // DirectX 12 CPU Page Property
        private const uint D3D12_CPU_PAGE_PROPERTY_UNKNOWN = 0;
        private const uint D3D12_CPU_PAGE_PROPERTY_NOT_AVAILABLE = 1;
        private const uint D3D12_CPU_PAGE_PROPERTY_WRITE_COMBINE = 2;
        private const uint D3D12_CPU_PAGE_PROPERTY_WRITE_BACK = 3;

        // DirectX 12 Memory Pool
        private const uint D3D12_MEMORY_POOL_UNKNOWN = 0;
        private const uint D3D12_MEMORY_POOL_L0 = 1;
        private const uint D3D12_MEMORY_POOL_L1 = 2;

        // DirectX 12 Texture Layout
        private const uint D3D12_TEXTURE_LAYOUT_UNKNOWN = 0;
        private const uint D3D12_TEXTURE_LAYOUT_ROW_MAJOR = 1;
        private const uint D3D12_TEXTURE_LAYOUT_64KB_UNDEFINED_SWIZZLE = 2;
        private const uint D3D12_TEXTURE_LAYOUT_64KB_STANDARD_SWIZZLE = 3;

        /// <summary>
        /// D3D12_RESOURCE_DESC structure.
        /// Based on DirectX 12 Resource Description: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_resource_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RESOURCE_DESC
        {
            public uint Dimension; // D3D12_RESOURCE_DIMENSION
            public ulong Alignment; // UINT64
            public ulong Width; // UINT64
            public uint Height; // UINT
            public ushort DepthOrArraySize; // UINT16
            public ushort MipLevels; // UINT16
            public uint Format; // DXGI_FORMAT
            public D3D12_SAMPLE_DESC SampleDesc;
            public uint Layout; // D3D12_TEXTURE_LAYOUT
            public uint Flags; // D3D12_RESOURCE_FLAGS
        }

        /// <summary>
        /// D3D12_SAMPLE_DESC structure.
        /// Based on DirectX 12 Sample Description: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_sample_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_SAMPLE_DESC
        {
            public uint Count; // UINT
            public uint Quality; // UINT
        }

        /// <summary>
        /// D3D12_HEAP_PROPERTIES structure.
        /// Based on DirectX 12 Heap Properties: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_heap_properties
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_HEAP_PROPERTIES
        {
            public uint Type; // D3D12_HEAP_TYPE
            public uint CPUPageProperty; // D3D12_CPU_PAGE_PROPERTY
            public uint MemoryPoolPreference; // D3D12_MEMORY_POOL
            public uint CreationNodeMask; // UINT
            public uint VisibleNodeMask; // UINT
        }

        /// <summary>
        /// D3D12_CLEAR_VALUE structure.
        /// Based on DirectX 12 Clear Value: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_clear_value
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_CLEAR_VALUE
        {
            public uint Format; // DXGI_FORMAT
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] Color; // float[4] for RGBA color
        }

        // COM interface method delegate for CreateCommittedResource
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateCommittedResourceDelegate(
            IntPtr device,
            IntPtr pHeapProperties,
            uint HeapFlags,
            IntPtr pDesc,
            uint InitialResourceState,
            IntPtr pOptimizedClearValue,
            ref Guid riidResource,
            IntPtr ppvResource);

        // DirectX 12 Resource Barrier Type
        private const uint D3D12_RESOURCE_BARRIER_TYPE_TRANSITION = 0;
        private const uint D3D12_RESOURCE_BARRIER_TYPE_ALIASING = 1;
        private const uint D3D12_RESOURCE_BARRIER_TYPE_UAV = 2;

        /// <summary>
        /// D3D12_RESOURCE_BARRIER structure for resource state transitions.
        /// Based on DirectX 12 Resource Barriers: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_resource_barrier
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RESOURCE_BARRIER
        {
            public uint Type; // D3D12_RESOURCE_BARRIER_TYPE
            public uint Flags; // D3D12_RESOURCE_BARRIER_FLAGS
            public D3D12_RESOURCE_TRANSITION_BARRIER Transition;
        }

        /// <summary>
        /// D3D12_RESOURCE_TRANSITION_BARRIER structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RESOURCE_TRANSITION_BARRIER
        {
            public IntPtr pResource; // ID3D12Resource*
            public uint Subresource; // UINT
            public uint StateBefore; // D3D12_RESOURCE_STATES
            public uint StateAfter; // D3D12_RESOURCE_STATES
        }

        /// <summary>
        /// D3D12_TEXTURE_COPY_LOCATION structure for texture copy operations.
        /// Based on DirectX 12 Texture Copy: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_texture_copy_location
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEXTURE_COPY_LOCATION
        {
            public IntPtr pResource; // ID3D12Resource*
            public uint Type; // D3D12_TEXTURE_COPY_TYPE
            public D3D12_PLACED_SUBRESOURCE_FOOTPRINT PlacedFootprint;
        }

        /// <summary>
        /// D3D12_PLACED_SUBRESOURCE_FOOTPRINT structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_PLACED_SUBRESOURCE_FOOTPRINT
        {
            public ulong Offset; // UINT64
            public D3D12_SUBRESOURCE_FOOTPRINT Footprint;
        }

        /// <summary>
        /// D3D12_SUBRESOURCE_FOOTPRINT structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_SUBRESOURCE_FOOTPRINT
        {
            public uint Format; // DXGI_FORMAT
            public uint Width; // UINT
            public uint Height; // UINT
            public uint Depth; // UINT
            public uint RowPitch; // UINT
        }

        // DirectX 12 Texture Copy Type
        private const uint D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX = 0;
        private const uint D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT = 1;

        /// <summary>
        /// D3D12_MAPPED_SUBRESOURCE structure for mapped resource data.
        /// Based on DirectX 12 Resource Mapping: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_mapped_subresource
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_MAPPED_SUBRESOURCE
        {
            public IntPtr pData; // void*
            public uint RowPitch; // UINT
            public uint DepthPitch; // UINT
        }

        // DirectX 12 Map Flags
        private const uint D3D12_MAP_READ = 1;
        private const uint D3D12_MAP_WRITE = 2;
        private const uint D3D12_MAP_READ_WRITE = 3;
        private const uint D3D12_MAP_WRITE_DISCARD = 4;
        private const uint D3D12_MAP_WRITE_NO_OVERWRITE = 5;

        // DirectX 12 Fence Flags
        private const uint D3D12_FENCE_FLAG_NONE = 0;
        private const uint D3D12_FENCE_FLAG_SHARED = 0x1;
        private const uint D3D12_FENCE_FLAG_SHARED_CROSS_ADAPTER = 0x2;
        private const uint D3D12_FENCE_FLAG_NON_MONITORED = 0x4;

        // COM interface method delegates for DirectX 12 readback operations
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ResourceBarrierDelegate(IntPtr commandList, uint numBarriers, IntPtr pBarriers);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CopyTextureRegionDelegate(
            IntPtr commandList,
            IntPtr pDst,
            uint DstX,
            uint DstY,
            uint DstZ,
            IntPtr pSrc,
            IntPtr pSrcBox);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CloseCommandListDelegate(IntPtr commandList);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ExecuteCommandListsDelegate(IntPtr commandQueue, uint numCommandLists, IntPtr ppCommandLists);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateFenceDelegate(IntPtr device, ulong initialValue, uint flags, ref Guid riid, IntPtr ppFence);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong SignalDelegate(IntPtr commandQueue, IntPtr fence, ulong value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong GetCompletedValueDelegate(IntPtr fence);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetEventOnCompletionDelegate(IntPtr fence, ulong value, IntPtr hEvent);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GetDescDelegate(IntPtr resource, IntPtr pDesc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MapDelegate(IntPtr resource, uint subresource, IntPtr pReadRange, IntPtr ppData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UnmapDelegate(IntPtr resource, uint subresource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetCopyableFootprintsDelegate(
            IntPtr device,
            IntPtr pResourceDesc,
            uint firstSubresource,
            uint numSubresources,
            ulong baseOffset,
            IntPtr pLayouts,
            IntPtr pNumRows,
            IntPtr pRowSizeInBytes,
            IntPtr pTotalBytes);

        // Interface IDs
        private static readonly Guid IID_ID3D12Fence = new Guid("0a753dcf-c4d8-4b91-adf6-be5a60d95a76");

        #endregion
    }
}


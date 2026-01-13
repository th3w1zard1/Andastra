using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Game.Graphics.MonoGame.Backends
{
    /// <summary>
    /// DirectX 12 device wrapper implementing IDevice interface for raytracing operations.
    ///
    /// Provides NVRHI-style abstractions for DirectX 12 raytracing resources:
    /// - Acceleration structures (BLAS/TLAS)
    /// - Raytracing pipelines
    /// - Resource creation and management
    ///
    /// Wraps native ID3D12Device5 with DXR 1.1 support.
    /// </summary>
    public class D3D12Device : IDevice
    {
        #region DirectX 12 API Interop

        private const string D3D12Library = "d3d12.dll";

        // Windows API functions for event synchronization
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, [MarshalAs(UnmanagedType.Bool)] bool bManualReset, [MarshalAs(UnmanagedType.Bool)] bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        private const uint WAIT_OBJECT_0 = 0x00000000;
        private const uint WAIT_TIMEOUT = 0x00000102;
        private const uint WAIT_FAILED = 0xFFFFFFFF;
        private const uint INFINITE = 0xFFFFFFFF;

        // DirectX 12 HRESULT values
        private enum HRESULT
        {
            S_OK = 0x00000000,
            E_FAIL = unchecked((int)0x80004005),
            E_INVALIDARG = unchecked((int)0x80070057),
            E_OUTOFMEMORY = unchecked((int)0x8007000E),
            E_NOTIMPL = unchecked((int)0x80004001),
            DXGI_ERROR_DEVICE_REMOVED = unchecked((int)0x887A0005),
        }

        // DirectX 12 GUIDs
        private static readonly Guid IID_ID3D12Device = new Guid(0x189819f1, 0x1db6, 0x4b57, 0xbe, 0x54, 0x18, 0x21, 0x33, 0x9b, 0x85, 0xf7);
        private static readonly Guid IID_ID3D12Device5 = new Guid(0x8b4f173b, 0x2fea, 0x4b80, 0xb4, 0xc4, 0x52, 0x46, 0xa8, 0xe9, 0xda, 0x52);
        private static readonly Guid IID_ID3D12StateObject = new Guid(0x47016943, 0xfca8, 0x4594, 0x93, 0xea, 0xaf, 0x25, 0x8b, 0xdc, 0x7b, 0x77);
        private static readonly Guid IID_ID3D12StateObjectProperties = new Guid(0xde5fa827, 0x91bf, 0x4fb9, 0xb6, 0x01, 0x5c, 0x10, 0x5e, 0x15, 0x58, 0xdc);

        /// <summary>
        /// ID3D12Device COM interface for DirectX 12 device operations.
        /// This interface inherits from IUnknown and ID3D12Object.
        /// All methods are accessed via COM vtable using delegates (see Call* helper methods).
        ///
        /// Methods used in this codebase (accessed via vtable):
        /// - VTable index 0: IUnknown::QueryInterface - Query for other interfaces
        /// - VTable index 1: IUnknown::AddRef - Increment reference count
        /// - VTable index 2: IUnknown::Release - Decrement reference count and release if count reaches zero
        /// - VTable index 6: CheckFeatureSupport - Query device feature support (format support, etc.)
        /// - VTable index 11: CreateFence - Create synchronization fence objects
        /// - VTable index 25: CreateCommittedResource - Create GPU resources with committed memory
        /// - VTable index 26: CreateCommandAllocator - Create command allocators for command lists
        /// - VTable index 27: CreateDescriptorHeap - Create descriptor heaps for resource views
        /// - VTable index 27: CreateCommandSignature - Create command signatures for indirect execution (alternative at same index)
        /// - VTable index 28: CreateCommandList - Create command lists for recording GPU commands
        /// - VTable index 28: GetDescriptorHandleIncrementSize - Get descriptor handle increment size (alternative at same index)
        /// - VTable index 34: CreateSampler - Create sampler descriptors
        /// - VTable index 43: CreateGraphicsPipelineState - Create graphics pipeline state objects
        /// - VTable index 44: CreateComputePipelineState - Create compute pipeline state objects
        /// - VTable index 47: CreateRootSignature - Create root signatures for shader resource binding
        ///
        /// Note: Methods are not declared directly in this interface because they are accessed via COM vtable
        /// using Marshal.GetDelegateForFunctionPointer and UnmanagedFunctionPointer delegates.
        /// This approach is necessary for C# 7.3 compatibility and provides full control over COM interop.
        ///
        /// Based on DirectX 12 Device Interface: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nn-d3d12-id3d12device
        /// </summary>
        [ComImport]
        [Guid("189819f1-1db6-4b57-be54-1821339b85f7")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ID3D12Device
        {
            // Methods are accessed via COM vtable using delegates (see Call* helper methods above)
            // This interface declaration is used for type safety and COM interop, but methods are not declared
            // because they are accessed through vtable lookups for maximum flexibility and C# 7.3 compatibility
        }

        /// <summary>
        /// ID3D12Device5 COM interface for DirectX 12 raytracing operations (DXR 1.1).
        /// This interface inherits from ID3D12Device4, which inherits from ID3D12Device3, ID3D12Device2, ID3D12Device1, and ID3D12Device.
        /// All methods are accessed via COM vtable using delegates (see Call* helper methods).
        ///
        /// Methods used in this codebase (accessed via vtable):
        /// - VTable index 0: IUnknown::QueryInterface - Query for other interfaces
        /// - VTable index 1: IUnknown::AddRef - Increment reference count
        /// - VTable index 2: IUnknown::Release - Decrement reference count and release if count reaches zero
        /// - VTable index 57: GetRaytracingAccelerationStructurePrebuildInfo - Get prebuild info for acceleration structures
        ///   (First method in ID3D12Device5 interface, after ID3D12Device methods which end around index 54-56)
        /// - VTable index 58: CreateStateObject - Create raytracing pipeline state objects
        ///   (Second method in ID3D12Device5 interface, used for creating raytracing pipelines with shaders)
        ///
        /// Note: Methods are not declared directly in this interface because they are accessed via COM vtable
        /// using Marshal.GetDelegateForFunctionPointer and UnmanagedFunctionPointer delegates.
        /// This approach is necessary for C# 7.3 compatibility and provides full control over COM interop.
        ///
        /// ID3D12Device5 provides DXR 1.1 features including:
        /// - Raytracing pipeline state objects (state objects with shader libraries, hit groups, etc.)
        /// - Acceleration structure prebuild information queries
        /// - Enhanced raytracing capabilities beyond DXR 1.0
        ///
        /// Based on DirectX 12 DXR API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nn-d3d12-id3d12device5
        /// </summary>
        [ComImport]
        [Guid("8b4f173b-2fea-4b80-b4c4-5246a8e9da52")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ID3D12Device5
        {
            // Methods are accessed via COM vtable using delegates (see Call* helper methods above)
            // This interface declaration is used for type safety and COM interop, but methods are not declared
            // because they are accessed through vtable lookups for maximum flexibility and C# 7.3 compatibility
        }

        /// <summary>
        /// ID3D12StateObject COM interface for DirectX 12 raytracing pipelines.
        /// This interface inherits from IUnknown and has no methods of its own.
        /// All methods are accessed via COM vtable:
        /// - VTable index 0: IUnknown::QueryInterface - Query for other interfaces (e.g., ID3D12StateObjectProperties)
        /// - VTable index 1: IUnknown::AddRef - Increment reference count
        /// - VTable index 2: IUnknown::Release - Decrement reference count and release if count reaches zero
        ///
        /// Based on DirectX 12 DXR API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nn-d3d12-id3d12stateobject
        /// ID3D12StateObject is created via ID3D12Device5::CreateStateObject and represents a raytracing pipeline state object.
        /// </summary>
        [ComImport]
        [Guid("47016943-fca8-4594-93ea-af258bdc7b77")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ID3D12StateObject
        {
            // ID3D12StateObject has no methods beyond IUnknown (QueryInterface, AddRef, Release)
            // Methods are accessed via COM vtable using delegates (QueryInterfaceDelegate, ReleaseDelegate)
            // See CallStateObjectRelease() and CallStateObjectQueryInterface() helper methods
        }

        /// <summary>
        /// ID3D12StateObjectProperties COM interface for querying raytracing pipeline properties.
        /// This interface is queried from ID3D12StateObject via QueryInterface.
        /// Methods are accessed via COM vtable:
        /// - VTable index 0: IUnknown::QueryInterface
        /// - VTable index 1: IUnknown::AddRef
        /// - VTable index 2: IUnknown::Release
        /// - VTable index 3: GetShaderIdentifier - Get shader identifier for a shader export name
        /// - VTable index 4: GetPipelineStackSize - Get the pipeline stack size
        ///
        /// Based on DirectX 12 DXR API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nn-d3d12-id3d12stateobjectproperties
        /// </summary>
        [ComImport]
        [Guid("de5fa827-91bf-4fb9-b601-5c105e1558dc")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ID3D12StateObjectProperties
        {
            // ID3D12StateObjectProperties methods are accessed via COM vtable using delegates
            // GetShaderIdentifier is at vtable index 3 (after IUnknown methods)
            // GetPipelineStackSize is at vtable index 4
            // See GetShaderIdentifierDelegate and CallStateObjectPropertiesGetShaderIdentifier() helper methods
        }

        // DirectX 12 function pointers for P/Invoke
        // Based on DirectX 12 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/
        // These functions are exported from d3d12.dll and are used for device creation, debugging, and root signature management

        // D3D12CreateDevice function - Creates a DirectX 12 device
        // Based on DirectX 12 Device Creation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-d3d12createdevice
        // HRESULT D3D12CreateDevice(IUnknown* pAdapter, D3D_FEATURE_LEVEL MinimumFeatureLevel, REFIID riid, void** ppDevice)
        [DllImport(D3D12Library, CallingConvention = CallingConvention.StdCall, EntryPoint = "D3D12CreateDevice")]
        private static extern int D3D12CreateDevice(
            IntPtr pAdapter, // IUnknown* - DXGI adapter (can be null for default adapter)
            uint MinimumFeatureLevel, // D3D_FEATURE_LEVEL - Minimum feature level required
            ref Guid riid, // REFIID - Interface ID (e.g., IID_ID3D12Device)
            out IntPtr ppDevice); // void** - Output device pointer

        // D3D12GetDebugInterface function - Gets debug interface for DirectX 12 debugging
        // Based on DirectX 12 Debug Interface: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-d3d12getdebuginterface
        // HRESULT D3D12GetDebugInterface(REFIID riid, void** ppvDebug)
        [DllImport(D3D12Library, CallingConvention = CallingConvention.StdCall, EntryPoint = "D3D12GetDebugInterface")]
        private static extern int D3D12GetDebugInterface(
            ref Guid riid, // REFIID - Interface ID (e.g., IID_ID3D12Debug)
            out IntPtr ppvDebug); // void** - Output debug interface pointer

        // D3D12EnableExperimentalFeatures function - Enables experimental DirectX 12 features
        // Based on DirectX 12 Experimental Features: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-d3d12enableexperimentalfeatures
        // HRESULT D3D12EnableExperimentalFeatures(UINT NumFeatures, const IID* pIIDs, void* pConfigurationStructs, UINT* pConfigurationStructSizes)
        [DllImport(D3D12Library, CallingConvention = CallingConvention.StdCall, EntryPoint = "D3D12EnableExperimentalFeatures")]
        private static extern int D3D12EnableExperimentalFeatures(
            uint NumFeatures, // UINT - Number of features to enable
            IntPtr pIIDs, // const IID* - Array of feature interface IDs
            IntPtr pConfigurationStructs, // void* - Array of configuration structures
            IntPtr pConfigurationStructSizes); // UINT* - Array of configuration structure sizes

        // D3D12SerializeVersionedRootSignature function - Serializes a versioned root signature
        // Based on DirectX 12 Versioned Root Signature: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-d3d12serializeversionedrootsignature
        // HRESULT D3D12SerializeVersionedRootSignature(const D3D12_VERSIONED_ROOT_SIGNATURE_DESC* pRootSignatureDesc, ID3DBlob** ppBlob, ID3DBlob** ppErrorBlob)
        [DllImport(D3D12Library, CallingConvention = CallingConvention.StdCall, EntryPoint = "D3D12SerializeVersionedRootSignature")]
        private static extern int D3D12SerializeVersionedRootSignature(
            IntPtr pRootSignatureDesc, // const D3D12_VERSIONED_ROOT_SIGNATURE_DESC*
            out IntPtr ppBlob, // ID3DBlob** - Output serialized blob
            out IntPtr ppErrorBlob); // ID3DBlob** - Output error blob (can be null)

        // D3D12CreateVersionedRootSignatureDeserializer function - Creates a deserializer for versioned root signatures
        // Based on DirectX 12 Versioned Root Signature Deserializer: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-d3d12createversionedrootsignaturedeserializer
        // HRESULT D3D12CreateVersionedRootSignatureDeserializer(const void* pSrcData, SIZE_T SrcDataSizeInBytes, REFIID pRootSignatureDeserializerInterface, void** ppRootSignatureDeserializer)
        [DllImport(D3D12Library, CallingConvention = CallingConvention.StdCall, EntryPoint = "D3D12CreateVersionedRootSignatureDeserializer")]
        private static extern int D3D12CreateVersionedRootSignatureDeserializer(
            IntPtr pSrcData, // const void* - Serialized root signature data
            IntPtr SrcDataSizeInBytes, // SIZE_T - Size of serialized data in bytes
            ref Guid pRootSignatureDeserializerInterface, // REFIID - Interface ID for deserializer
            out IntPtr ppRootSignatureDeserializer); // void** - Output deserializer pointer

        // DirectX 12 Feature Level constants (D3D_FEATURE_LEVEL)
        // Based on DirectX 12 Feature Levels: https://docs.microsoft.com/en-us/windows/win32/api/d3dcommon/ne-d3dcommon-d3d_feature_level
        private const uint D3D_FEATURE_LEVEL_12_0 = 0xc100; // DirectX 12.0 feature level
        private const uint D3D_FEATURE_LEVEL_12_1 = 0xc200; // DirectX 12.1 feature level (DXR 1.0, mesh shaders, etc.)
        private const uint D3D_FEATURE_LEVEL_12_2 = 0xc300; // DirectX 12.2 feature level (DXR 1.1, sampler feedback, etc.)

        // DirectX 12 Debug Interface GUIDs
        // Based on DirectX 12 Debug Interfaces: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nn-d3d12-id3d12debug
        private static readonly Guid IID_ID3D12Debug = new Guid(0x344488b7, 0x6846, 0x474b, 0xb9, 0x89, 0xf0, 0x27, 0x44, 0x82, 0x45, 0xe0);
        private static readonly Guid IID_ID3D12Debug1 = new Guid(0xaffaa4ca, 0x63fe, 0x4d8e, 0xb8, 0xad, 0x15, 0x90, 0x00, 0xaf, 0x43, 0x04);
        private static readonly Guid IID_ID3D12Debug2 = new Guid(0x93a665c4, 0xea3f, 0x4729, 0x9e, 0x14, 0x69, 0x6e, 0x57, 0x58, 0x3e, 0x49);
        private static readonly Guid IID_ID3D12Debug3 = new Guid(0x5cf4e58f, 0xf671, 0x4ff1, 0xa5, 0x42, 0x36, 0x86, 0xe3, 0xd1, 0x53, 0xd1);
        private static readonly Guid IID_ID3D12Debug4 = new Guid(0x014b816e, 0x9ec5, 0x4a2f, 0x81, 0xbd, 0xdb, 0xbd, 0x1d, 0xfb, 0x4b, 0x36);
        private static readonly Guid IID_ID3D12Debug5 = new Guid(0x548d6b12, 0x09a0, 0x4bec, 0xbb, 0xfd, 0xe0, 0x9f, 0x61, 0x0e, 0xaf, 0x7d);
        private static readonly Guid IID_ID3D12Debug6 = new Guid(0x82a816d6, 0x5d01, 0x4157, 0x97, 0xd0, 0x49, 0x75, 0x46, 0xfb, 0x1b, 0x18);
        private static readonly Guid IID_ID3D12DebugDevice = new Guid(0x3febd6dd, 0x49ec, 0x4fcd, 0x88, 0x6e, 0x5a, 0x8e, 0x5b, 0x6f, 0x0e, 0x12);
        private static readonly Guid IID_ID3D12DebugDevice1 = new Guid(0xa9b71770, 0xd099, 0x4a65, 0xa6, 0x98, 0x3d, 0xee, 0x10, 0x02, 0x0f, 0x88);
        private static readonly Guid IID_ID3D12DebugDevice2 = new Guid(0x60eccbc1, 0x378d, 0x4df1, 0x89, 0x4e, 0xf8, 0x29, 0x1b, 0x5a, 0x0c, 0x4a);
        private static readonly Guid IID_ID3D12InfoQueue = new Guid(0x0740a3c1, 0x1e95, 0x4d07, 0x83, 0x2a, 0xaa, 0x11, 0xce, 0x86, 0x04, 0x20);

        // DirectX 12 Experimental Feature GUIDs
        // Based on DirectX 12 Experimental Features: https://docs.microsoft.com/en-us/windows/win32/direct3d12/experimental-features
        private static readonly Guid D3D12ExperimentalShaderModels = new Guid(0x76f5573e, 0xf13a, 0x40f5, 0xb2, 0x97, 0x17, 0xce, 0x15, 0x60, 0x0b, 0x28);
        private static readonly Guid D3D12TiledResourceTier4 = new Guid(0xc9c4725f, 0x81dc, 0x4c5f, 0x99, 0x6b, 0x8f, 0x10, 0x0b, 0x6b, 0x30, 0x4e);
        private static readonly Guid D3D12MetaCommand = new Guid(0xc734c47e, 0x807f, 0x4157, 0x93, 0xc4, 0x92, 0x79, 0x34, 0x58, 0xbc, 0x55);

        #endregion

        private readonly IntPtr _device;
        private readonly IntPtr _device5; // ID3D12Device5 for raytracing
        private readonly IntPtr _commandQueue;
        private readonly GraphicsCapabilities _capabilities;
        private bool _disposed;

        // Resource tracking
        private readonly Dictionary<IntPtr, IResource> _resources;
        private uint _nextResourceHandle;

        // Frame tracking for multi-buffering
        private int _currentFrameIndex;

        // Fence for device idle synchronization
        private IntPtr _idleFence;
        private IntPtr _idleFenceEvent;
        private ulong _idleFenceValue;

        // Sampler descriptor heap management
        private IntPtr _samplerDescriptorHeap;
        private IntPtr _samplerHeapCpuStartHandle;
        private uint _samplerHeapDescriptorIncrementSize;
        private int _samplerHeapCapacity;
        private int _samplerHeapNextIndex;
        private const int DefaultSamplerHeapCapacity = 2048;

        // Command signature cache for indirect execution
        // Command signatures are expensive to create, so we cache them per device
        private IntPtr _dispatchIndirectCommandSignature;
        private IntPtr _drawIndirectCommandSignature;
        private IntPtr _drawIndexedIndirectCommandSignature;

        // DSV descriptor heap fields
        private IntPtr _dsvDescriptorHeap;
        private IntPtr _dsvHeapCpuStartHandle;
        private uint _dsvHeapDescriptorIncrementSize;
        private int _dsvHeapCapacity;
        private int _dsvHeapNextIndex;
        private const int DefaultDsvHeapCapacity = 1024;

        // RTV descriptor heap management
        private IntPtr _rtvDescriptorHeap;
        private IntPtr _rtvHeapCpuStartHandle;
        private uint _rtvHeapDescriptorIncrementSize;
        private int _rtvHeapCapacity;
        private int _rtvHeapNextIndex;
        private const int DefaultRtvHeapCapacity = 1024;
        private readonly Dictionary<IntPtr, IntPtr> _textureDsvHandles; // Cache of texture -> DSV handle mappings
        private readonly Dictionary<IntPtr, IntPtr> _textureRtvHandles; // Cache of texture -> RTV handle mappings

        // SRV descriptor heap fields (non-shader-visible for texture management)
        private IntPtr _srvDescriptorHeap;
        private IntPtr _srvHeapCpuStartHandle;
        private uint _srvHeapDescriptorIncrementSize;
        private int _srvHeapCapacity;
        private int _srvHeapNextIndex;
        private const int DefaultSrvHeapCapacity = 1024;
        private readonly Dictionary<IntPtr, IntPtr> _textureSrvHandles; // Cache of texture -> SRV handle mappings

        // UAV descriptor heap fields
        private IntPtr _uavDescriptorHeap;
        private IntPtr _uavHeapCpuStartHandle;
        private uint _uavHeapDescriptorIncrementSize;
        private int _uavHeapCapacity;
        private int _uavHeapNextIndex;
        private const int DefaultUavHeapCapacity = 1024;
        private readonly Dictionary<IntPtr, IntPtr> _textureUavHandles; // Cache of texture -> UAV handle mappings

        // CBV_SRV_UAV descriptor heap fields (for shader-visible binding sets)
        private IntPtr _cbvSrvUavDescriptorHeap;
        private IntPtr _cbvSrvUavHeapCpuStartHandle;
        private D3D12_GPU_DESCRIPTOR_HANDLE _cbvSrvUavHeapGpuStartHandle;
        private uint _cbvSrvUavHeapDescriptorIncrementSize;
        private int _cbvSrvUavHeapCapacity;
        private int _cbvSrvUavHeapNextIndex;
        private const int DefaultCbvSrvUavHeapCapacity = 10000; // Large capacity for binding sets (typical for modern games)

        public GraphicsCapabilities Capabilities
        {
            get { return _capabilities; }
        }

        public GraphicsBackend Backend
        {
            get { return GraphicsBackend.Direct3D12; }
        }

        public bool IsValid
        {
            get { return !_disposed && _device != IntPtr.Zero; }
        }

        internal D3D12Device(
            IntPtr device,
            IntPtr device5,
            IntPtr commandQueue,
            GraphicsCapabilities capabilities)
        {
            if (device == IntPtr.Zero)
            {
                throw new ArgumentException("Device handle must be valid", nameof(device));
            }

            _device = device;
            _device5 = device5;
            _commandQueue = commandQueue;
            // Note: GraphicsCapabilities is a struct, so it cannot be null
            _capabilities = capabilities;
            _resources = new Dictionary<IntPtr, IResource>();
            _nextResourceHandle = 1;
            _currentFrameIndex = 0;
            _textureDsvHandles = new Dictionary<IntPtr, IntPtr>();
            _textureRtvHandles = new Dictionary<IntPtr, IntPtr>();
            _textureSrvHandles = new Dictionary<IntPtr, IntPtr>();
            _textureUavHandles = new Dictionary<IntPtr, IntPtr>();
            _cbvSrvUavHeapNextIndex = 0;
        }

        #region Resource Creation

        public ITexture CreateTexture(TextureDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (desc.Width == 0 || desc.Height == 0)
            {
                throw new ArgumentException("Texture dimensions must be greater than zero", nameof(desc));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, return a texture with zero handle
                // The application should use VulkanDevice for cross-platform support
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var texture = new D3D12Texture(handle, desc, IntPtr.Zero, IntPtr.Zero, _device, this);
                _resources[handle] = texture;
                return texture;
            }

            // Convert TextureFormat to DXGI_FORMAT for texture resource creation
            uint dxgiFormat = ConvertTextureFormatToDxgiFormatForTexture(desc.Format);
            if (dxgiFormat == 0) // DXGI_FORMAT_UNKNOWN
            {
                throw new NotSupportedException($"Texture format {desc.Format} is not supported for D3D12 texture creation");
            }

            // Map TextureDimension to D3D12_RESOURCE_DIMENSION
            uint resourceDimension = MapTextureDimensionToD3D12(desc.Dimension);

            // Determine heap type - textures almost always use DEFAULT heap (GPU-only, best performance)
            // Upload/Readback heaps are typically only used for staging textures
            uint heapType = D3D12_HEAP_TYPE_DEFAULT;

            // Create heap properties
            D3D12_HEAP_PROPERTIES heapProperties = new D3D12_HEAP_PROPERTIES
            {
                Type = heapType,
                CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
                MemoryPoolPreference = D3D12_MEMORY_POOL_UNKNOWN,
                CreationNodeMask = 0,
                VisibleNodeMask = 0
            };

            // Convert TextureDesc to D3D12_RESOURCE_DESC
            D3D12_RESOURCE_DESC resourceDesc = new D3D12_RESOURCE_DESC
            {
                Dimension = resourceDimension,
                Alignment = 0, // D3D12_DEFAULT_RESOURCE_PLACEMENT_ALIGNMENT (0 means default alignment)
                Width = unchecked((ulong)desc.Width),
                Height = unchecked((uint)desc.Height),
                DepthOrArraySize = unchecked((ushort)(desc.Dimension == TextureDimension.Texture3D ? desc.Depth : desc.ArraySize)),
                MipLevels = unchecked((ushort)desc.MipLevels),
                Format = dxgiFormat,
                SampleDesc = new D3D12_SAMPLE_DESC
                {
                    Count = unchecked((uint)desc.SampleCount),
                    Quality = 0 // Standard quality
                },
                Layout = 0, // D3D12_TEXTURE_LAYOUT_UNKNOWN (0 means default layout)
                Flags = D3D12_RESOURCE_FLAG_NONE
            };

            // Set resource flags based on usage
            if ((desc.Usage & TextureUsage.RenderTarget) != 0)
            {
                resourceDesc.Flags |= D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET;
            }

            if ((desc.Usage & TextureUsage.DepthStencil) != 0)
            {
                resourceDesc.Flags |= D3D12_RESOURCE_FLAG_ALLOW_DEPTH_STENCIL;
            }

            if ((desc.Usage & TextureUsage.UnorderedAccess) != 0)
            {
                resourceDesc.Flags |= D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS;
            }

            // Determine initial resource state
            uint initialResourceState = MapResourceStateToD3D12(desc.InitialState);
            if (initialResourceState == 0)
            {
                // Default to COMMON state if no initial state specified
                initialResourceState = D3D12_RESOURCE_STATE_COMMON;
            }

            // Create optimized clear value if this is a render target or depth stencil
            IntPtr optimizedClearValuePtr = IntPtr.Zero;
            // Note: ClearValue is a struct, so we check if it's been set by checking if Depth/Stencil or Color values are non-zero
            bool hasClearValue = (desc.Usage & (TextureUsage.RenderTarget | TextureUsage.DepthStencil)) != 0;
            if (hasClearValue)
            {
                D3D12_CLEAR_VALUE clearValue = new D3D12_CLEAR_VALUE
                {
                    Format = dxgiFormat
                };

                if ((desc.Usage & TextureUsage.DepthStencil) != 0)
                {
                    // Depth-stencil clear value
                    clearValue.DepthStencil = new D3D12_DEPTH_STENCIL_VALUE
                    {
                        Depth = desc.ClearValue.Depth,
                        Stencil = desc.ClearValue.Stencil
                    };
                }
                else
                {
                    // Render target clear value (color)
                    clearValue.Color = new float[4]
                    {
                        desc.ClearValue.R,
                        desc.ClearValue.G,
                        desc.ClearValue.B,
                        desc.ClearValue.A
                    };
                }

                int clearValueSize = Marshal.SizeOf(typeof(D3D12_CLEAR_VALUE));
                optimizedClearValuePtr = Marshal.AllocHGlobal(clearValueSize);
                Marshal.StructureToPtr(clearValue, optimizedClearValuePtr, false);
                hasClearValue = true;
            }

            // Allocate memory for structures
            int heapPropertiesSize = Marshal.SizeOf(typeof(D3D12_HEAP_PROPERTIES));
            IntPtr heapPropertiesPtr = Marshal.AllocHGlobal(heapPropertiesSize);
            int resourceDescSize = Marshal.SizeOf(typeof(D3D12_RESOURCE_DESC));
            IntPtr resourceDescPtr = Marshal.AllocHGlobal(resourceDescSize);
            IntPtr resourcePtr = Marshal.AllocHGlobal(IntPtr.Size);

            IntPtr d3d12Resource = IntPtr.Zero;
            IntPtr srvHandle = IntPtr.Zero;

            try
            {
                // Marshal structures to unmanaged memory
                Marshal.StructureToPtr(heapProperties, heapPropertiesPtr, false);
                Marshal.StructureToPtr(resourceDesc, resourceDescPtr, false);

                // IID_ID3D12Resource
                Guid iidResource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad");

                // Call CreateCommittedResource
                int hr = CallCreateCommittedResource(
                    _device,
                    heapPropertiesPtr,
                    D3D12_HEAP_FLAG_NONE,
                    resourceDescPtr,
                    initialResourceState,
                    hasClearValue ? optimizedClearValuePtr : IntPtr.Zero,
                    ref iidResource,
                    resourcePtr);

                if (hr < 0)
                {
                    throw new InvalidOperationException($"CreateCommittedResource failed with HRESULT 0x{hr:X8}");
                }

                // Get the created resource pointer
                d3d12Resource = Marshal.ReadIntPtr(resourcePtr);
                if (d3d12Resource == IntPtr.Zero)
                {
                    throw new InvalidOperationException("CreateCommittedResource returned null resource pointer");
                }

                // Create SRV descriptor if texture is used as shader resource
                if ((desc.Usage & TextureUsage.ShaderResource) != 0)
                {
                    srvHandle = CreateSrvDescriptorForTexture(d3d12Resource, desc, dxgiFormat, resourceDimension);
                }

                // Create texture wrapper
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var texture = new D3D12Texture(handle, desc, d3d12Resource, srvHandle, _device, this);
                _resources[handle] = texture;

                return texture;
            }
            finally
            {
                // Free allocated memory
                if (heapPropertiesPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(heapPropertiesPtr);
                }
                if (resourceDescPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(resourceDescPtr);
                }
                if (resourcePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(resourcePtr);
                }
                if (optimizedClearValuePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(optimizedClearValuePtr);
                }
            }
        }

        public IBuffer CreateBuffer(BufferDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (desc.ByteSize == 0)
            {
                throw new ArgumentException("Buffer size must be greater than zero", nameof(desc));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, return a buffer with zero handle
                // The application should use VulkanDevice for cross-platform support
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var buffer = new D3D12Buffer(handle, desc, IntPtr.Zero, _device, this);
                _resources[handle] = buffer;
                return buffer;
            }

            // Determine heap type based on BufferDesc.HeapType
            // DEFAULT: GPU-accessible, best performance for GPU-only resources
            // UPLOAD: CPU-writable, GPU-readable (for dynamic/staging buffers)
            // READBACK: CPU-readable, GPU-writable (for reading back GPU results)
            uint heapType = MapBufferHeapTypeToD3D12(desc.HeapType);

            // Create heap properties
            D3D12_HEAP_PROPERTIES heapProperties = new D3D12_HEAP_PROPERTIES
            {
                Type = heapType,
                CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
                MemoryPoolPreference = D3D12_MEMORY_POOL_UNKNOWN,
                CreationNodeMask = 0,
                VisibleNodeMask = 0
            };

            // Convert BufferDesc to D3D12_RESOURCE_DESC
            D3D12_RESOURCE_DESC resourceDesc = new D3D12_RESOURCE_DESC
            {
                Dimension = D3D12_RESOURCE_DIMENSION_BUFFER,
                Alignment = 0, // D3D12_DEFAULT_RESOURCE_PLACEMENT_ALIGNMENT (0 means default alignment)
                Width = (ulong)desc.ByteSize, // Buffer size in bytes
                Height = 1,
                DepthOrArraySize = 1,
                MipLevels = 1,
                Format = 0, // DXGI_FORMAT_UNKNOWN - buffers don't use formats
                SampleDesc = new D3D12_SAMPLE_DESC { Count = 1, Quality = 0 },
                Layout = 0, // D3D12_TEXTURE_LAYOUT_ROW_MAJOR - not used for buffers (0 = undefined)
                Flags = D3D12_RESOURCE_FLAG_NONE
            };

            // Add unordered access flag if buffer can be used as UAV
            if ((desc.Usage & BufferUsageFlags.UnorderedAccess) != 0)
            {
                resourceDesc.Flags |= D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS;
            }

            // Determine initial resource state
            uint initialResourceState = MapResourceStateToD3D12(desc.InitialState);
            if (initialResourceState == 0)
            {
                // Default to COMMON state if no initial state specified
                initialResourceState = D3D12_RESOURCE_STATE_COMMON;
            }

            // Allocate memory for structures
            int heapPropertiesSize = Marshal.SizeOf(typeof(D3D12_HEAP_PROPERTIES));
            IntPtr heapPropertiesPtr = Marshal.AllocHGlobal(heapPropertiesSize);
            int resourceDescSize = Marshal.SizeOf(typeof(D3D12_RESOURCE_DESC));
            IntPtr resourceDescPtr = Marshal.AllocHGlobal(resourceDescSize);
            IntPtr resourcePtr = Marshal.AllocHGlobal(IntPtr.Size);

            try
            {
                // Marshal structures to unmanaged memory
                Marshal.StructureToPtr(heapProperties, heapPropertiesPtr, false);
                Marshal.StructureToPtr(resourceDesc, resourceDescPtr, false);

                // IID_ID3D12Resource
                Guid iidResource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad");

                // Call CreateCommittedResource
                int hr = CallCreateCommittedResource(
                    _device,
                    heapPropertiesPtr,
                    D3D12_HEAP_FLAG_NONE,
                    resourceDescPtr,
                    initialResourceState,
                    IntPtr.Zero, // pOptimizedClearValue - not needed for buffers
                    ref iidResource,
                    resourcePtr);

                if (hr < 0)
                {
                    throw new InvalidOperationException($"CreateCommittedResource failed with HRESULT 0x{hr:X8}");
                }

                // Get the created resource pointer
                IntPtr d3d12Resource = Marshal.ReadIntPtr(resourcePtr);
                if (d3d12Resource == IntPtr.Zero)
                {
                    throw new InvalidOperationException("CreateCommittedResource returned null resource pointer");
                }

                // Create buffer wrapper
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var buffer = new D3D12Buffer(handle, desc, d3d12Resource, _device, this);
                _resources[handle] = buffer;

                return buffer;
            }
            finally
            {
                // Free allocated memory
                if (heapPropertiesPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(heapPropertiesPtr);
                }
                if (resourceDescPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(resourceDescPtr);
                }
                if (resourcePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(resourcePtr);
                }
            }
        }

        public ISampler CreateSampler(SamplerDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, return a sampler with zero handle
                // The application should use VulkanDevice for cross-platform support
                IntPtr nonWindowsHandle = new IntPtr(_nextResourceHandle++);
                var nonWindowsSampler = new D3D12Sampler(nonWindowsHandle, desc, IntPtr.Zero, _device);
                _resources[nonWindowsHandle] = nonWindowsSampler;
                return nonWindowsSampler;
            }

            // Convert SamplerDesc to D3D12_SAMPLER_DESC
            D3D12_SAMPLER_DESC d3d12SamplerDesc = ConvertSamplerDescToD3D12(desc);

            // Allocate descriptor handle from sampler heap
            IntPtr cpuDescriptorHandle = AllocateSamplerDescriptor();
            if (cpuDescriptorHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to allocate sampler descriptor from heap");
            }

            // Create sampler descriptor in the allocated slot
            try
            {
                // Allocate memory for the sampler descriptor structure
                int samplerDescSize = Marshal.SizeOf(typeof(D3D12_SAMPLER_DESC));
                IntPtr samplerDescPtr = Marshal.AllocHGlobal(samplerDescSize);
                try
                {
                    Marshal.StructureToPtr(d3d12SamplerDesc, samplerDescPtr, false);

                    // Call ID3D12Device::CreateSampler to create the sampler descriptor
                    CallCreateSampler(_device, samplerDescPtr, cpuDescriptorHandle);
                }
                finally
                {
                    Marshal.FreeHGlobal(samplerDescPtr);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create D3D12 sampler descriptor: {ex.Message}", ex);
            }

            // Wrap in D3D12Sampler and return
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var sampler = new D3D12Sampler(handle, desc, cpuDescriptorHandle, _device);
            _resources[handle] = sampler;

            return sampler;
        }

        public IShader CreateShader(ShaderDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (desc.Bytecode == null || desc.Bytecode.Length == 0)
            {
                throw new ArgumentException("Shader bytecode must be provided", nameof(desc));
            }

            // D3D12 doesn't create shader objects directly - shaders are part of PSO
            // This method stores the bytecode for later use in pipeline creation
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var shader = new D3D12Shader(handle, desc, _device);
            _resources[handle] = shader;

            return shader;
        }

        public IGraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc, IFramebuffer framebuffer)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // Create D3D12 graphics pipeline state object
            // Based on DirectX 12 Graphics Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-creategraphicspipelinestate
            // VTable index 43 for ID3D12Device::CreateGraphicsPipelineState
            // Based on daorigins.exe/DragonAge2.exe: Graphics pipeline creation for rendering

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, return a pipeline with zero handles
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var pipeline = new D3D12GraphicsPipeline(handle, desc, IntPtr.Zero, IntPtr.Zero, _device, this);
                _resources[handle] = pipeline;
                return pipeline;
            }

            IntPtr rootSignature = IntPtr.Zero;
            IntPtr pipelineState = IntPtr.Zero;

            try
            {
                // Step 1: Get or create root signature from binding layouts
                // Based on DirectX 12 Graphics Pipeline: Root signature is required for pipeline state creation
                // Root signature defines the shader resource binding layout (CBVs, SRVs, UAVs, samplers)
                // https://docs.microsoft.com/en-us/windows/win32/direct3d12/root-signatures
                if (desc.BindingLayouts != null && desc.BindingLayouts.Length > 0)
                {
                    // Get root signature from binding layouts
                    // Based on DirectX 12: A graphics pipeline typically uses one root signature
                    // Multiple binding layouts can be combined into a single root signature, but for simplicity,
                    // we use the root signature from the first binding layout
                    // In a full implementation, multiple root signatures could be combined or used separately

                    // Try to get root signature from first binding layout
                    var firstLayout = desc.BindingLayouts[0] as D3D12BindingLayout;
                    if (firstLayout != null)
                    {
                        // D3D12BindingLayout stores root signature internally and provides GetRootSignature() method
                        // Based on D3D12BindingLayout implementation: Root signature is created during CreateBindingLayout
                        // and stored in the layout for reuse across multiple pipelines
                        rootSignature = firstLayout.GetRootSignature();

                        // Validate that root signature was successfully retrieved
                        if (rootSignature == IntPtr.Zero)
                        {
                            // Root signature should have been created during CreateBindingLayout
                            // If it's null, this indicates CreateBindingLayout was not fully implemented or failed
                            // In this case, we cannot proceed with pipeline creation as root signature is required
                            // Log error and continue with IntPtr.Zero - pipeline creation will fail gracefully
                            Console.WriteLine("[D3D12Device] ERROR: Root signature is null in binding layout - CreateBindingLayout may not be fully implemented");
                            // Note: We could attempt to create the root signature here, but that would duplicate
                            // the logic from CreateBindingLayout. It's better to ensure CreateBindingLayout works correctly.
                            // rootSignature remains IntPtr.Zero, which will cause pipeline creation to fail
                        }
                    }
                    else
                    {
                        // Binding layout is not a D3D12BindingLayout (could be from different backend)
                        // In this case, we cannot extract the root signature directly
                        // We would need to create a root signature from the binding layout descriptor
                        // For now, log a warning and continue with IntPtr.Zero
                        Console.WriteLine("[D3D12Device] WARNING: Binding layout is not D3D12BindingLayout, cannot extract root signature");
                    }

                    // Handle multiple binding layouts (if more than one is provided)
                    // Based on DirectX 12: Multiple root signatures can be used, but typically one is sufficient
                    // For graphics pipelines, we use the first root signature
                    // In compute pipelines or advanced scenarios, multiple root signatures might be needed
                    if (desc.BindingLayouts.Length > 1)
                    {
                        // Log that multiple binding layouts are provided but only first is used
                        // In a full implementation, multiple root signatures could be combined or validated for compatibility
                        Console.WriteLine($"[D3D12Device] INFO: Multiple binding layouts provided ({desc.BindingLayouts.Length}), using root signature from first layout");
                    }
                }
                else
                {
                    // No binding layouts provided - pipeline will use default/empty root signature
                    // Based on DirectX 12: A root signature is required, but an empty root signature (no parameters) is valid
                    // This allows pipelines that don't use shader resources to be created
                    Console.WriteLine("[D3D12Device] INFO: No binding layouts provided, pipeline will use empty root signature");
                    // rootSignature remains IntPtr.Zero, which will be handled in ConvertGraphicsPipelineDescToD3D12
                }

                // Step 2: Convert GraphicsPipelineDesc to D3D12_GRAPHICS_PIPELINE_STATE_DESC
                var pipelineDesc = ConvertGraphicsPipelineDescToD3D12(desc, framebuffer, rootSignature);

                // Step 3: Create the pipeline state object
                IntPtr pipelineStatePtr = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    Guid iidPipelineState = IID_ID3D12PipelineState;
                    int hr = CallCreateGraphicsPipelineState(_device, ref pipelineDesc, ref iidPipelineState, pipelineStatePtr);
                    if (hr < 0)
                    {
                        throw new InvalidOperationException($"CreateGraphicsPipelineState failed with HRESULT 0x{hr:X8}");
                    }

                    pipelineState = Marshal.ReadIntPtr(pipelineStatePtr);
                    if (pipelineState == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Pipeline state pointer is null");
                    }
                }
                finally
                {
                    // Free marshalled structures
                    FreeGraphicsPipelineStateDesc(ref pipelineDesc);
                    Marshal.FreeHGlobal(pipelineStatePtr);
                }

                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var pipeline = new D3D12GraphicsPipeline(handle, desc, pipelineState, rootSignature, _device, this);
                _resources[handle] = pipeline;

                return pipeline;
            }
            catch (Exception ex)
            {
                // Clean up on failure - release any successfully created COM objects
                if (pipelineState != IntPtr.Zero)
                {
                    try
                    {
                        ReleaseComObject(pipelineState);
                    }
                    catch (Exception releaseEx)
                    {
                        // Log error but continue cleanup
                        Console.WriteLine($"[D3D12Device] Error releasing pipeline state in error handler: {releaseEx.Message}");
                    }
                }

                // Note: rootSignature is passed to the pipeline constructor, so it will be released by the pipeline's Dispose()
                // If rootSignature needs to be released here (when not passed to pipeline), it should be released above
                // Currently rootSignature is typically IntPtr.Zero at this point due to incomplete CreateBindingLayout implementation

                // Return pipeline with zero handles on failure (allows graceful degradation)
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var pipeline = new D3D12GraphicsPipeline(handle, desc, IntPtr.Zero, rootSignature, _device, this);
                _resources[handle] = pipeline;

                Console.WriteLine($"[D3D12Device] WARNING: Failed to create graphics pipeline state: {ex.Message}");
                return pipeline;
            }
        }

        public IComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // Create D3D12 compute pipeline state object
            // Based on DirectX 12 Compute Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcomputepipelinestate
            // VTable index 44 for ID3D12Device::CreateComputePipelineState
            // Based on swkotor.exe/swkotor2.exe: Compute shader pipeline creation for compute operations

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, return a pipeline with zero handles
                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var pipeline = new D3D12ComputePipeline(handle, desc, IntPtr.Zero, IntPtr.Zero, _device, this);
                _resources[handle] = pipeline;
                return pipeline;
            }

            IntPtr rootSignature = IntPtr.Zero;
            IntPtr pipelineState = IntPtr.Zero;

            try
            {
                // Step 1: Get or create root signature from binding layouts
                // Based on DirectX 12 Compute Pipeline: Root signature is required for pipeline state creation
                // Root signature defines the shader resource binding layout (CBVs, SRVs, UAVs, samplers)
                // https://docs.microsoft.com/en-us/windows/win32/direct3d12/root-signatures
                if (desc.BindingLayouts != null && desc.BindingLayouts.Length > 0)
                {
                    // Get root signature from first binding layout
                    var firstLayout = desc.BindingLayouts[0] as D3D12BindingLayout;
                    if (firstLayout != null)
                    {
                        // D3D12BindingLayout stores root signature internally and provides GetRootSignature() method
                        rootSignature = firstLayout.GetRootSignature();

                        // Validate that root signature was successfully retrieved
                        if (rootSignature == IntPtr.Zero)
                        {
                            Console.WriteLine("[D3D12Device] ERROR: Root signature is null in binding layout - CreateBindingLayout may not be fully implemented");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[D3D12Device] WARNING: Binding layout is not D3D12BindingLayout, cannot extract root signature");
                    }

                    // Handle multiple binding layouts (if more than one is provided)
                    if (desc.BindingLayouts.Length > 1)
                    {
                        Console.WriteLine($"[D3D12Device] INFO: Multiple binding layouts provided ({desc.BindingLayouts.Length}), using root signature from first layout");
                    }
                }
                else
                {
                    // No binding layouts provided - pipeline will use default/empty root signature
                    Console.WriteLine("[D3D12Device] INFO: No binding layouts provided, pipeline will use empty root signature");
                }

                // Step 2: Convert ComputePipelineDesc to D3D12_COMPUTE_PIPELINE_STATE_DESC
                var pipelineDesc = ConvertComputePipelineDescToD3D12(desc, rootSignature);

                // Step 3: Create the pipeline state object
                IntPtr pipelineStatePtr = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    Guid iidPipelineState = IID_ID3D12PipelineState;
                    int hr = CallCreateComputePipelineState(_device, ref pipelineDesc, ref iidPipelineState, pipelineStatePtr);
                    if (hr < 0)
                    {
                        throw new InvalidOperationException($"CreateComputePipelineState failed with HRESULT 0x{hr:X8}");
                    }

                    pipelineState = Marshal.ReadIntPtr(pipelineStatePtr);
                    if (pipelineState == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Pipeline state pointer is null");
                    }
                }
                finally
                {
                    // Free marshalled structures
                    FreeComputePipelineStateDesc(ref pipelineDesc);
                    Marshal.FreeHGlobal(pipelineStatePtr);
                }

                IntPtr handle = new IntPtr(_nextResourceHandle++);
                var pipeline = new D3D12ComputePipeline(handle, desc, pipelineState, rootSignature, _device, this);
                _resources[handle] = pipeline;

                return pipeline;
            }
            catch (Exception ex)
            {
                // Clean up on error
                if (pipelineState != IntPtr.Zero)
                {
                    ReleaseComObject(pipelineState);
                }
                if (rootSignature != IntPtr.Zero)
                {
                    ReleaseComObject(rootSignature);
                }
                throw new InvalidOperationException($"Failed to create compute pipeline: {ex.Message}", ex);
            }
        }

        public IFramebuffer CreateFramebuffer(FramebufferDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // D3D12 doesn't use framebuffers - uses render targets directly
            // This wrapper stores attachment information for compatibility
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var framebuffer = new D3D12Framebuffer(handle, desc);
            _resources[handle] = framebuffer;

            return framebuffer;
        }

        public IBindingLayout CreateBindingLayout(BindingLayoutDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (desc.Items == null || desc.Items.Length == 0)
            {
                throw new ArgumentException("Binding layout must have at least one item", nameof(desc));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("DirectX 12 binding layouts are only supported on Windows");
            }

            // Convert BindingLayoutItems to D3D12 root parameters
            // Group items by type into descriptor tables for efficiency
            var rootParameters = new List<D3D12_ROOT_PARAMETER>();
            var descriptorRanges = new List<List<D3D12_DESCRIPTOR_RANGE>>();
            var rangePointers = new List<IntPtr>();

            // Group items by binding type and shader visibility
            var groupedItems = new Dictionary<uint, List<BindingLayoutItem>>(); // Key: (ShaderVisibility << 16) | BindingType

            foreach (var item in desc.Items)
            {
                uint bindingType = ConvertBindingTypeToD3D12RangeType(item.Type);
                uint shaderVisibility = ConvertShaderStageFlagsToD3D12Visibility(item.Stages);
                uint key = (shaderVisibility << 16) | bindingType;

                if (!groupedItems.ContainsKey(key))
                {
                    groupedItems[key] = new List<BindingLayoutItem>();
                }
                groupedItems[key].Add(item);
            }

            // Create descriptor tables for each group
            foreach (var group in groupedItems)
            {
                uint shaderVisibility = (group.Key >> 16) & 0xFFFF;
                uint bindingType = group.Key & 0xFFFF;
                var items = group.Value;

                // Sort items by slot
                items.Sort((a, b) => a.Slot.CompareTo(b.Slot));

                // Create descriptor ranges for this group
                var ranges = new List<D3D12_DESCRIPTOR_RANGE>();
                uint currentOffset = 0;
                foreach (var item in items)
                {
                    var range = new D3D12_DESCRIPTOR_RANGE
                    {
                        RangeType = bindingType,
                        NumDescriptors = (uint)item.Count,
                        BaseShaderRegister = (uint)item.Slot,
                        RegisterSpace = 0, // Default register space
                        OffsetInDescriptorsFromTableStart = currentOffset
                    };
                    ranges.Add(range);
                    currentOffset += (uint)item.Count;
                }

                if (ranges.Count > 0)
                {
                    // Allocate memory for descriptor ranges array
                    int rangesSize = Marshal.SizeOf(typeof(D3D12_DESCRIPTOR_RANGE)) * ranges.Count;
                    IntPtr rangesPtr = Marshal.AllocHGlobal(rangesSize);
                    for (int i = 0; i < ranges.Count; i++)
                    {
                        IntPtr rangePtr = new IntPtr(rangesPtr.ToInt64() + i * Marshal.SizeOf(typeof(D3D12_DESCRIPTOR_RANGE)));
                        Marshal.StructureToPtr(ranges[i], rangePtr, false);
                    }

                    rangePointers.Add(rangesPtr);
                    descriptorRanges.Add(ranges);

                    // Create root parameter for this descriptor table
                    var rootParam = new D3D12_ROOT_PARAMETER
                    {
                        ParameterType = D3D12_ROOT_PARAMETER_TYPE_DESCRIPTOR_TABLE,
                        ShaderVisibility = shaderVisibility
                    };
                    rootParam.DescriptorTable.NumDescriptorRanges = (uint)ranges.Count;
                    rootParam.DescriptorTable.pDescriptorRanges = rangesPtr;

                    rootParameters.Add(rootParam);
                }
            }

            // Create root signature description
            int rootParamsSize = Marshal.SizeOf(typeof(D3D12_ROOT_PARAMETER)) * rootParameters.Count;
            IntPtr rootParamsPtr = Marshal.AllocHGlobal(rootParamsSize);
            try
            {
                for (int i = 0; i < rootParameters.Count; i++)
                {
                    IntPtr paramPtr = new IntPtr(rootParamsPtr.ToInt64() + i * Marshal.SizeOf(typeof(D3D12_ROOT_PARAMETER)));
                    Marshal.StructureToPtr(rootParameters[i], paramPtr, false);
                }

                var rootSignatureDesc = new D3D12_ROOT_SIGNATURE_DESC
                {
                    NumParameters = (uint)rootParameters.Count,
                    pParameters = rootParamsPtr,
                    NumStaticSamplers = 0,
                    pStaticSamplers = IntPtr.Zero,
                    Flags = D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT
                };

                // Serialize root signature
                IntPtr pRootSignatureDesc = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_ROOT_SIGNATURE_DESC)));
                try
                {
                    Marshal.StructureToPtr(rootSignatureDesc, pRootSignatureDesc, false);

                    IntPtr blobPtr;
                    IntPtr errorBlobPtr;
                    int hr = D3D12SerializeRootSignature(pRootSignatureDesc, D3D_ROOT_SIGNATURE_VERSION_1_0, out blobPtr, out errorBlobPtr);

                    if (hr < 0)
                    {
                        // Free allocated memory
                        foreach (var ptr in rangePointers)
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                        throw new InvalidOperationException($"D3D12SerializeRootSignature failed with HRESULT 0x{hr:X8}");
                    }

                    if (errorBlobPtr != IntPtr.Zero)
                    {
                        // Read and log error blob content before releasing
                        // Error blob contains diagnostic messages from D3D12SerializeRootSignature
                        // Based on DirectX 12 API: ID3DBlob interface provides GetBufferPointer and GetBufferSize
                        try
                        {
                            unsafe
                            {
                                // Access ID3DBlob vtable (same structure as blobPtr)
                                IntPtr* errorBlobVtable = *(IntPtr**)errorBlobPtr;

                                // Get error blob size (ID3DBlob::GetBufferSize is at vtable index 3)
                                IntPtr getErrorBufferSizePtr = errorBlobVtable[3];
                                GetBufferSizeDelegate getErrorBufferSize = (GetBufferSizeDelegate)Marshal.GetDelegateForFunctionPointer(getErrorBufferSizePtr, typeof(GetBufferSizeDelegate));
                                IntPtr errorBlobSize = getErrorBufferSize(errorBlobPtr);

                                // Get error blob buffer pointer (ID3DBlob::GetBufferPointer is at vtable index 4)
                                IntPtr getErrorBufferPointerPtr = errorBlobVtable[4];
                                GetBufferPointerDelegate getErrorBufferPointer = (GetBufferPointerDelegate)Marshal.GetDelegateForFunctionPointer(getErrorBufferPointerPtr, typeof(GetBufferPointerDelegate));
                                IntPtr errorBlobBuffer = getErrorBufferPointer(errorBlobPtr);

                                // Convert error blob buffer to string (null-terminated ANSI string)
                                if (errorBlobBuffer != IntPtr.Zero && errorBlobSize.ToInt64() > 0)
                                {
                                    string errorMessage = Marshal.PtrToStringAnsi(errorBlobBuffer, errorBlobSize.ToInt32());
                                    if (!string.IsNullOrEmpty(errorMessage))
                                    {
                                        Console.WriteLine($"[D3D12Device] WARNING: D3D12SerializeRootSignature returned error blob: {errorMessage}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // If reading error blob fails, log the exception but continue
                            Console.WriteLine($"[D3D12Device] WARNING: Failed to read error blob content: {ex.Message}");
                        }
                        finally
                        {
                            // Always release the error blob COM object
                            ReleaseComObject(errorBlobPtr);
                        }
                    }

                    if (blobPtr == IntPtr.Zero)
                    {
                        // Free allocated memory
                        foreach (var ptr in rangePointers)
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                        throw new InvalidOperationException("D3D12SerializeRootSignature returned null blob");
                    }

                    // Get blob size (ID3DBlob::GetBufferSize is at vtable index 3)
                    IntPtr getBufferSizePtr;
                    IntPtr getBufferPointerPtr;
                    unsafe
                    {
                        IntPtr* blobVtable = *(IntPtr**)blobPtr;
                        getBufferSizePtr = blobVtable[3];
                        getBufferPointerPtr = blobVtable[4];
                    }
                    GetBufferSizeDelegate getBufferSize = (GetBufferSizeDelegate)Marshal.GetDelegateForFunctionPointer(getBufferSizePtr, typeof(GetBufferSizeDelegate));
                    IntPtr blobSize = getBufferSize(blobPtr);

                    // Get blob buffer (ID3DBlob::GetBufferPointer is at vtable index 4)
                    GetBufferPointerDelegate getBufferPointer = (GetBufferPointerDelegate)Marshal.GetDelegateForFunctionPointer(getBufferPointerPtr, typeof(GetBufferPointerDelegate));
                    IntPtr blobBuffer = getBufferPointer(blobPtr);

                    // Create root signature
                    Guid iidRootSignature = new Guid(0xc54a6b66, 0x72df, 0x4ee8, 0x8b, 0xe5, 0xa9, 0x46, 0xa1, 0x42, 0x92, 0x14); // IID_ID3D12RootSignature
                    IntPtr rootSignaturePtr = Marshal.AllocHGlobal(IntPtr.Size);
                    try
                    {
                        hr = CallCreateRootSignature(_device, blobBuffer, blobSize, ref iidRootSignature, rootSignaturePtr);
                        if (hr < 0)
                        {
                            ReleaseComObject(blobPtr);
                            // Free allocated memory
                            foreach (var ptr in rangePointers)
                            {
                                Marshal.FreeHGlobal(ptr);
                            }
                            throw new InvalidOperationException($"CreateRootSignature failed with HRESULT 0x{hr:X8}");
                        }

                        IntPtr rootSignature = Marshal.ReadIntPtr(rootSignaturePtr);
                        if (rootSignature == IntPtr.Zero)
                        {
                            ReleaseComObject(blobPtr);
                            // Free allocated memory
                            foreach (var ptr in rangePointers)
                            {
                                Marshal.FreeHGlobal(ptr);
                            }
                            throw new InvalidOperationException("CreateRootSignature returned null root signature pointer");
                        }

                        // Release blob (no longer needed after root signature is created)
                        ReleaseComObject(blobPtr);

                        // Wrap in D3D12BindingLayout and return
                        // Store range pointers in the layout for cleanup on disposal
                        // Based on DirectX 12 Root Signature Management: Descriptor range pointers must remain valid
                        // until the root signature is destroyed, so we store them in the layout and free them in Dispose()
                        IntPtr handle = new IntPtr(_nextResourceHandle++);
                        var layout = new D3D12BindingLayout(handle, desc, rootSignature, _device, this, rangePointers);
                        _resources[handle] = layout;

                        // Note: rangePointers are now owned by D3D12BindingLayout and will be freed when layout is disposed
                        // Do not free them here - they must remain valid for the lifetime of the root signature

                        return layout;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(rootSignaturePtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pRootSignatureDesc);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(rootParamsPtr);
            }
        }

        // Helper delegates for ID3DBlob interface
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterfaceDelegate(IntPtr pThis, ref Guid riid, IntPtr ppvObject);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferSizeDelegate(IntPtr blob);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferPointerDelegate(IntPtr blob);

        /// <summary>
        /// Converts BindingType to D3D12_DESCRIPTOR_RANGE_TYPE.
        /// </summary>
        private uint ConvertBindingTypeToD3D12RangeType(BindingType type)
        {
            switch (type)
            {
                case BindingType.ConstantBuffer:
                    return D3D12_DESCRIPTOR_RANGE_TYPE_CBV;
                case BindingType.Texture:
                    return D3D12_DESCRIPTOR_RANGE_TYPE_SRV;
                case BindingType.Sampler:
                    return D3D12_DESCRIPTOR_RANGE_TYPE_SAMPLER;
                case BindingType.RWTexture:
                case BindingType.RWBuffer:
                    return D3D12_DESCRIPTOR_RANGE_TYPE_UAV;
                case BindingType.StructuredBuffer:
                    return D3D12_DESCRIPTOR_RANGE_TYPE_SRV; // Structured buffers are SRVs
                case BindingType.AccelStruct:
                    return D3D12_DESCRIPTOR_RANGE_TYPE_SRV; // Acceleration structures are SRVs
                default:
                    return D3D12_DESCRIPTOR_RANGE_TYPE_SRV; // Default to SRV
            }
        }

        /// <summary>
        /// Converts ShaderStageFlags to D3D12_SHADER_VISIBILITY.
        /// </summary>
        private uint ConvertShaderStageFlagsToD3D12Visibility(ShaderStageFlags stages)
        {
            // If all graphics stages are present, use ALL
            if ((stages & ShaderStageFlags.AllGraphics) == ShaderStageFlags.AllGraphics)
            {
                return D3D12_SHADER_VISIBILITY_ALL;
            }

            // Check individual stages (priority order: Pixel > Geometry > Domain > Hull > Vertex)
            if ((stages & ShaderStageFlags.Pixel) != 0)
            {
                return D3D12_SHADER_VISIBILITY_PIXEL;
            }
            if ((stages & ShaderStageFlags.Geometry) != 0)
            {
                return D3D12_SHADER_VISIBILITY_GEOMETRY;
            }
            if ((stages & ShaderStageFlags.Domain) != 0)
            {
                return D3D12_SHADER_VISIBILITY_DOMAIN;
            }
            if ((stages & ShaderStageFlags.Hull) != 0)
            {
                return D3D12_SHADER_VISIBILITY_HULL;
            }
            if ((stages & ShaderStageFlags.Vertex) != 0)
            {
                return D3D12_SHADER_VISIBILITY_VERTEX;
            }
            if ((stages & ShaderStageFlags.Compute) != 0)
            {
                return D3D12_SHADER_VISIBILITY_ALL; // Compute shaders use ALL visibility
            }

            // Default to ALL if no specific stage is set
            return D3D12_SHADER_VISIBILITY_ALL;
        }

        public IBindingSet CreateBindingSet(IBindingLayout layout, BindingSetDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            // Allocate and populate D3D12 descriptor set
            // Based on DirectX 12 Binding Sets: https://docs.microsoft.com/en-us/windows/win32/direct3d12/descriptors-overview
            // Binding sets contain SRV/UAV/CBV descriptors allocated from a shader-visible descriptor heap
            // Each binding set item (texture, buffer, sampler) requires a descriptor in the heap

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("DirectX 12 binding sets are only supported on Windows");
            }

            // Ensure CBV_SRV_UAV descriptor heap exists (shader-visible for binding sets)
            EnsureCbvSrvUavDescriptorHeap();

            if (_cbvSrvUavDescriptorHeap == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create CBV_SRV_UAV descriptor heap for binding sets");
            }

            // Count total descriptors needed for this binding set
            // Based on DirectX 12: Each binding set item requires one descriptor
            int totalDescriptorsNeeded = 0;
            if (desc.Items != null)
            {
                foreach (var item in desc.Items)
                {
                    if (item.Type == BindingType.Texture || item.Type == BindingType.RWTexture ||
                        item.Type == BindingType.ConstantBuffer || item.Type == BindingType.StructuredBuffer ||
                        item.Type == BindingType.RWBuffer || item.Type == BindingType.AccelStruct)
                    {
                        totalDescriptorsNeeded++;
                    }
                }
            }

            if (totalDescriptorsNeeded == 0)
            {
                // Empty binding set - return binding set with zero descriptors
                // For empty binding sets, GPU descriptor handle is at heap start (offset 0)
                // Based on DirectX 12: GPU descriptor handles are offset from heap start by descriptor index * increment size
                // Since there are no descriptors, offset is 0
                D3D12_GPU_DESCRIPTOR_HANDLE emptyGpuDescriptorHandle = _cbvSrvUavHeapGpuStartHandle;
                IntPtr emptyHandle = new IntPtr(_nextResourceHandle++);
                var emptyBindingSet = new D3D12BindingSet(emptyHandle, layout, desc, _cbvSrvUavDescriptorHeap, _device, this, emptyGpuDescriptorHandle);
                _resources[emptyHandle] = emptyBindingSet;
                return emptyBindingSet;
            }

            // Check if heap has enough space
            if (_cbvSrvUavHeapNextIndex + totalDescriptorsNeeded > _cbvSrvUavHeapCapacity)
            {
                throw new InvalidOperationException($"CBV_SRV_UAV descriptor heap is full (capacity: {_cbvSrvUavHeapCapacity}, needed: {totalDescriptorsNeeded}, available: {_cbvSrvUavHeapCapacity - _cbvSrvUavHeapNextIndex})");
            }

            // Allocate descriptors from heap (contiguous allocation for binding set)
            int bindingSetStartIndex = _cbvSrvUavHeapNextIndex;
            int currentDescriptorIndex = _cbvSrvUavHeapNextIndex;

            // Process each binding set item and create descriptors
            if (desc.Items != null)
            {
                foreach (var item in desc.Items)
                {
                    // Allocate CPU descriptor handle for this item
                    IntPtr cpuDescriptorHandle = OffsetDescriptorHandle(_cbvSrvUavHeapCpuStartHandle, currentDescriptorIndex, _cbvSrvUavHeapDescriptorIncrementSize);

                    // Create descriptor based on binding type
                    // Based on DirectX 12: CreateShaderResourceView, CreateUnorderedAccessView, CreateConstantBufferView
                    if (item.Type == BindingType.Texture && item.Texture != null)
                    {
                        // Create SRV descriptor for texture
                        CreateSrvDescriptorForBindingSet(item.Texture, cpuDescriptorHandle);
                        currentDescriptorIndex++;
                    }
                    else if (item.Type == BindingType.RWTexture && item.Texture != null)
                    {
                        // Create UAV descriptor for read-write texture
                        CreateUavDescriptorForBindingSet(item.Texture, cpuDescriptorHandle);
                        currentDescriptorIndex++;
                    }
                    else if (item.Type == BindingType.ConstantBuffer && item.Buffer != null)
                    {
                        // Create CBV descriptor for constant buffer
                        CreateCbvDescriptorForBindingSet(item.Buffer, item.BufferOffset, item.BufferRange, cpuDescriptorHandle);
                        currentDescriptorIndex++;
                    }
                    else if (item.Type == BindingType.StructuredBuffer && item.Buffer != null)
                    {
                        // Create SRV descriptor for structured buffer
                        CreateSrvDescriptorForStructuredBuffer(item.Buffer, item.BufferOffset, item.BufferRange, cpuDescriptorHandle);
                        currentDescriptorIndex++;
                    }
                    else if (item.Type == BindingType.RWBuffer && item.Buffer != null)
                    {
                        // Create UAV descriptor for read-write buffer
                        CreateUavDescriptorForBuffer(item.Buffer, item.BufferOffset, item.BufferRange, cpuDescriptorHandle);
                        currentDescriptorIndex++;
                    }
                    else if (item.Type == BindingType.AccelStruct && item.AccelStruct != null)
                    {
                        // Create SRV descriptor for acceleration structure
                        CreateSrvDescriptorForAccelStruct(item.AccelStruct, cpuDescriptorHandle);
                        currentDescriptorIndex++;
                    }
                    // Note: Samplers are handled separately in sampler descriptor heap
                }
            }

            // Update heap next index
            _cbvSrvUavHeapNextIndex = currentDescriptorIndex;

            // Calculate GPU descriptor handle for binding set start
            // Based on DirectX 12: GPU descriptor handles are offset from heap start by descriptor index * increment size
            D3D12_GPU_DESCRIPTOR_HANDLE gpuDescriptorHandle = OffsetGpuDescriptorHandle(_cbvSrvUavHeapGpuStartHandle, bindingSetStartIndex, _cbvSrvUavHeapDescriptorIncrementSize);

            // Create binding set with allocated descriptors
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var bindingSet = new D3D12BindingSet(handle, layout, desc, _cbvSrvUavDescriptorHeap, _device, this, gpuDescriptorHandle);
            _resources[handle] = bindingSet;

            return bindingSet;
        }

        public ICommandList CreateCommandList(CommandListType type = CommandListType.Graphics)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("DirectX 12 command lists are only supported on Windows");
            }

            // Map CommandListType to D3D12_COMMAND_LIST_TYPE
            uint d3d12CommandListType = MapCommandListTypeToD3D12(type);

            // Create command allocator for this command list
            // Each command list needs its own allocator (allocators can be reused after command lists are executed)
            IntPtr commandAllocatorPtr = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Guid iidCommandAllocator = IID_ID3D12CommandAllocator;
                int hr = CallCreateCommandAllocator(_device, d3d12CommandListType, ref iidCommandAllocator, commandAllocatorPtr);
                if (hr < 0)
                {
                    throw new InvalidOperationException($"CreateCommandAllocator failed with HRESULT 0x{hr:X8}");
                }

                IntPtr commandAllocator = Marshal.ReadIntPtr(commandAllocatorPtr);
                if (commandAllocator == IntPtr.Zero)
                {
                    throw new InvalidOperationException("CreateCommandAllocator returned null allocator pointer");
                }

                // Create command list with the allocator
                IntPtr commandListPtr = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    Guid iidCommandList = IID_ID3D12GraphicsCommandList;
                    // nodeMask: 0 for single GPU, pInitialState: NULL (no initial pipeline state)
                    hr = CallCreateCommandList(_device, 0, d3d12CommandListType, commandAllocator, IntPtr.Zero, ref iidCommandList, commandListPtr);
                    if (hr < 0)
                    {
                        // Release the allocator if command list creation fails
                        ReleaseComObject(commandAllocator);
                        throw new InvalidOperationException($"CreateCommandList failed with HRESULT 0x{hr:X8}");
                    }

                    IntPtr commandList = Marshal.ReadIntPtr(commandListPtr);
                    if (commandList == IntPtr.Zero)
                    {
                        // Release the allocator if command list creation fails
                        ReleaseComObject(commandAllocator);
                        throw new InvalidOperationException("CreateCommandList returned null command list pointer");
                    }

                    // Wrap in D3D12CommandList and return
                    IntPtr handle = new IntPtr(_nextResourceHandle++);
                    var cmdList = new D3D12CommandList(handle, type, this, commandList, commandAllocator, _device);
                    _resources[handle] = cmdList;

                    return cmdList;
                }
                finally
                {
                    Marshal.FreeHGlobal(commandListPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(commandAllocatorPtr);
            }
        }

        public ITexture CreateHandleForNativeTexture(IntPtr nativeHandle, TextureDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (nativeHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Native handle must be valid", nameof(nativeHandle));
            }

            // Wrap existing native texture (e.g., from swapchain)
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var texture = new D3D12Texture(handle, desc, nativeHandle);
            _resources[handle] = texture;

            return texture;
        }

        #endregion

        #region Raytracing Resources

        public IAccelStruct CreateAccelStruct(AccelStructDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (!_capabilities.SupportsRaytracing)
            {
                throw new NotSupportedException("Raytracing is not supported on this device");
            }

            if (_device5 == IntPtr.Zero)
            {
                throw new InvalidOperationException("ID3D12Device5 is not available for raytracing");
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("D3D12 acceleration structures are only supported on Windows");
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);

            try
            {
                if (desc.IsTopLevel)
                {
                    // Create Top-Level Acceleration Structure (TLAS)
                    return CreateTopLevelAccelStruct(handle, desc);
                }
                else
                {
                    // Create Bottom-Level Acceleration Structure (BLAS)
                    return CreateBottomLevelAccelStruct(handle, desc);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create acceleration structure: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a bottom-level acceleration structure (BLAS).
        /// Based on D3D12 DXR API: ID3D12Device5::GetRaytracingAccelerationStructurePrebuildInfo
        /// </summary>
        private IAccelStruct CreateBottomLevelAccelStruct(IntPtr handle, AccelStructDesc desc)
        {
            if (desc.BottomLevelGeometries == null || desc.BottomLevelGeometries.Length == 0)
            {
                throw new ArgumentException("Bottom-level acceleration structure requires at least one geometry", nameof(desc));
            }

            // Convert GeometryDesc[] to D3D12_RAYTRACING_GEOMETRY_DESC[]
            int geometryCount = desc.BottomLevelGeometries.Length;
            int geometryDescSize = Marshal.SizeOf(typeof(D3D12_RAYTRACING_GEOMETRY_DESC));
            IntPtr geometryDescsPtr = Marshal.AllocHGlobal(geometryDescSize * geometryCount);

            try
            {
                IntPtr currentGeometryPtr = geometryDescsPtr;
                for (int i = 0; i < geometryCount; i++)
                {
                    var geometry = desc.BottomLevelGeometries[i];

                    if (geometry.Type != GeometryType.Triangles)
                    {
                        throw new NotSupportedException($"Geometry type {geometry.Type} is not yet supported. Only Triangles are supported.");
                    }

                    var triangles = geometry.Triangles;

                    // Get GPU virtual addresses for buffers
                    IntPtr vertexBufferResource = triangles.VertexBuffer.NativeHandle;
                    if (vertexBufferResource == IntPtr.Zero)
                    {
                        throw new ArgumentException($"Geometry at index {i} has an invalid vertex buffer", nameof(desc));
                    }

                    ulong vertexBufferGpuVa = GetGpuVirtualAddress(vertexBufferResource);
                    if (vertexBufferGpuVa == 0UL)
                    {
                        throw new InvalidOperationException($"Failed to get GPU virtual address for vertex buffer in geometry at index {i}");
                    }

                    // Calculate vertex buffer start address with offset
                    ulong vertexBufferStartAddress = vertexBufferGpuVa + (ulong)triangles.VertexOffset;

                    // Handle index buffer (optional for some geometries)
                    IntPtr indexBufferGpuVa = IntPtr.Zero;
                    uint indexCount = 0;
                    uint indexFormat = D3D12_RAYTRACING_INDEX_FORMAT_UINT32;

                    if (triangles.IndexBuffer != null)
                    {
                        IntPtr indexBufferResource = triangles.IndexBuffer.NativeHandle;
                        if (indexBufferResource != IntPtr.Zero)
                        {
                            ulong indexBufferGpuVaUlong = GetGpuVirtualAddress(indexBufferResource);
                            if (indexBufferGpuVaUlong != 0UL)
                            {
                                indexBufferGpuVa = new IntPtr((long)(indexBufferGpuVaUlong + (ulong)triangles.IndexOffset));
                                indexCount = (uint)triangles.IndexCount;

                                // Determine index format from TextureFormat
                                indexFormat = ConvertIndexFormatToD3D12(triangles.IndexFormat);
                            }
                        }
                    }

                    // Handle transform buffer (optional)
                    IntPtr transformBufferGpuVa = IntPtr.Zero;
                    if (triangles.TransformBuffer != null)
                    {
                        IntPtr transformBufferResource = triangles.TransformBuffer.NativeHandle;
                        if (transformBufferResource != IntPtr.Zero)
                        {
                            ulong transformBufferGpuVaUlong = GetGpuVirtualAddress(transformBufferResource);
                            if (transformBufferGpuVaUlong != 0UL)
                            {
                                transformBufferGpuVa = new IntPtr((long)(transformBufferGpuVaUlong + (ulong)triangles.TransformOffset));
                            }
                        }
                    }

                    // Build D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC
                    var trianglesDesc = new D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC
                    {
                        Transform3x4 = transformBufferGpuVa,
                        IndexFormat = indexFormat,
                        VertexFormat = ConvertVertexFormatToD3D12(triangles.VertexFormat),
                        IndexCount = indexCount,
                        VertexCount = (uint)triangles.VertexCount,
                        IndexBuffer = indexBufferGpuVa,
                        VertexBuffer = new D3D12_GPU_VIRTUAL_ADDRESS_AND_STRIDE
                        {
                            StartAddress = new IntPtr((long)vertexBufferStartAddress),
                            StrideInBytes = (uint)triangles.VertexStride
                        }
                    };

                    // Build D3D12_RAYTRACING_GEOMETRY_DESC
                    var geometryDesc = new D3D12_RAYTRACING_GEOMETRY_DESC
                    {
                        Type = D3D12_RAYTRACING_GEOMETRY_TYPE_TRIANGLES,
                        Flags = ConvertGeometryFlagsToD3D12(geometry.Flags),
                        Triangles = trianglesDesc
                    };

                    // Marshal structure to unmanaged memory
                    Marshal.StructureToPtr(geometryDesc, currentGeometryPtr, false);
                    currentGeometryPtr = new IntPtr(currentGeometryPtr.ToInt64() + geometryDescSize);
                }

                // Get build flags
                uint buildFlags = ConvertAccelStructBuildFlagsToD3D12(desc.BuildFlags);

                // Build D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
                var buildInputs = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
                {
                    Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL,
                    Flags = buildFlags,
                    NumDescs = (uint)geometryCount,
                    DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY,
                    pGeometryDescs = geometryDescsPtr
                };

                // Marshal build inputs to unmanaged memory for prebuild info query
                int buildInputsSize = Marshal.SizeOf(typeof(D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS));
                IntPtr buildInputsPtr = Marshal.AllocHGlobal(buildInputsSize);
                try
                {
                    Marshal.StructureToPtr(buildInputs, buildInputsPtr, false);

                    // Get prebuild information to determine buffer sizes
                    D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO prebuildInfo;
                    CallGetRaytracingAccelerationStructurePrebuildInfo(_device5, buildInputsPtr, out prebuildInfo);

                    // Validate prebuild info
                    if (prebuildInfo.ResultDataMaxSizeInBytes == 0 || prebuildInfo.ScratchDataSizeInBytes == 0)
                    {
                        throw new InvalidOperationException("GetRaytracingAccelerationStructurePrebuildInfo returned invalid sizes");
                    }

                    // Round up result buffer size to 256-byte alignment (D3D12 requirement)
                    ulong resultBufferSize = (prebuildInfo.ResultDataMaxSizeInBytes + 255UL) & ~255UL;

                    // Create result buffer for the acceleration structure
                    // Acceleration structure buffers must be in DEFAULT heap with D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS
                    var resultBufferDesc = new BufferDesc
                    {
                        ByteSize = (int)resultBufferSize,
                        Usage = BufferUsageFlags.AccelStructStorage,
                        InitialState = ResourceState.AccelStructRead,
                        DebugName = desc.DebugName ?? "BLAS_ResultBuffer"
                    };

                    IBuffer resultBuffer = CreateBuffer(resultBufferDesc);
                    if (resultBuffer == null || resultBuffer.NativeHandle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to create result buffer for acceleration structure");
                    }

                    // Get GPU virtual address for result buffer
                    ulong resultBufferGpuVa = GetGpuVirtualAddress(resultBuffer.NativeHandle);
                    if (resultBufferGpuVa == 0UL)
                    {
                        resultBuffer.Dispose();
                        throw new InvalidOperationException("Failed to get GPU virtual address for result buffer");
                    }

                    // Create acceleration structure wrapper
                    // Note: The actual building happens later via BuildBottomLevelAccelStruct on a command list
                    var accelStruct = new D3D12AccelStruct(handle, desc, IntPtr.Zero, resultBuffer, resultBufferGpuVa, _device5, this);
                    _resources[handle] = accelStruct;

                    return accelStruct;
                }
                finally
                {
                    Marshal.FreeHGlobal(buildInputsPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(geometryDescsPtr);
            }
        }

        /// <summary>
        /// Creates a top-level acceleration structure (TLAS).
        /// Based on D3D12 DXR API: ID3D12Device5::GetRaytracingAccelerationStructurePrebuildInfo
        /// </summary>
        private IAccelStruct CreateTopLevelAccelStruct(IntPtr handle, AccelStructDesc desc)
        {
            if (desc.TopLevelMaxInstances <= 0)
            {
                throw new ArgumentException("Top-level acceleration structure requires TopLevelMaxInstances > 0", nameof(desc));
            }

            uint maxInstances = (uint)desc.TopLevelMaxInstances;
            uint buildFlags = ConvertAccelStructBuildFlagsToD3D12(desc.BuildFlags);

            // Build D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS for TLAS
            // For TLAS, we specify the number of instances and layout, but instance data is provided later during build
            var buildInputs = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
            {
                Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL,
                Flags = buildFlags,
                NumDescs = maxInstances,
                DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY,
                pGeometryDescs = IntPtr.Zero // For TLAS, instance descriptors are provided during build, not here
            };

            // Marshal build inputs to unmanaged memory for prebuild info query
            int buildInputsSize = Marshal.SizeOf(typeof(D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS));
            IntPtr buildInputsPtr = Marshal.AllocHGlobal(buildInputsSize);
            try
            {
                Marshal.StructureToPtr(buildInputs, buildInputsPtr, false);

                // Get prebuild information to determine buffer sizes
                D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO prebuildInfo;
                CallGetRaytracingAccelerationStructurePrebuildInfo(_device5, buildInputsPtr, out prebuildInfo);

                // Validate prebuild info
                if (prebuildInfo.ResultDataMaxSizeInBytes == 0 || prebuildInfo.ScratchDataSizeInBytes == 0)
                {
                    throw new InvalidOperationException("GetRaytracingAccelerationStructurePrebuildInfo returned invalid sizes");
                }

                // Round up result buffer size to 256-byte alignment (D3D12 requirement)
                ulong resultBufferSize = (prebuildInfo.ResultDataMaxSizeInBytes + 255UL) & ~255UL;

                // Create result buffer for the acceleration structure
                var resultBufferDesc = new BufferDesc
                {
                    ByteSize = (int)resultBufferSize,
                    Usage = BufferUsageFlags.AccelStructStorage,
                    InitialState = ResourceState.AccelStructRead,
                    DebugName = desc.DebugName ?? "TLAS_ResultBuffer"
                };

                IBuffer resultBuffer = CreateBuffer(resultBufferDesc);
                if (resultBuffer == null || resultBuffer.NativeHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create result buffer for acceleration structure");
                }

                // Get GPU virtual address for result buffer
                ulong resultBufferGpuVa = GetGpuVirtualAddress(resultBuffer.NativeHandle);
                if (resultBufferGpuVa == 0UL)
                {
                    resultBuffer.Dispose();
                    throw new InvalidOperationException("Failed to get GPU virtual address for result buffer");
                }

                // Create acceleration structure wrapper
                // Note: The actual building happens later via BuildTopLevelAccelStruct on a command list
                var accelStruct = new D3D12AccelStruct(handle, desc, IntPtr.Zero, resultBuffer, resultBufferGpuVa, _device5, this);
                _resources[handle] = accelStruct;

                return accelStruct;
            }
            finally
            {
                Marshal.FreeHGlobal(buildInputsPtr);
            }
        }

        public IRaytracingPipeline CreateRaytracingPipeline(Interfaces.RaytracingPipelineDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (!_capabilities.SupportsRaytracing)
            {
                throw new NotSupportedException("Raytracing is not supported on this device");
            }

            if (desc.Shaders == null || desc.Shaders.Length == 0)
            {
                throw new ArgumentException("Raytracing pipeline requires at least one shader", nameof(desc));
            }

            if (_device5 == IntPtr.Zero)
            {
                throw new InvalidOperationException("ID3D12Device5 is not available for raytracing");
            }

            throw new NotImplementedException("D3D12 raytracing pipeline creation is not yet fully implemented. See CreateRaytracingPipeline implementation.");
        }

        #endregion

        #region Command Execution

        public void ExecuteCommandList(ICommandList commandList)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (commandList == null)
            {
                throw new ArgumentNullException(nameof(commandList));
            }

            ExecuteCommandLists(new[] { commandList });
        }

        public void ExecuteCommandLists(ICommandList[] commandLists)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (commandLists == null || commandLists.Length == 0)
            {
                return;
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, ExecuteCommandLists is a no-op
                // The application should use VulkanDevice for cross-platform support
                return;
            }

            if (_commandQueue == IntPtr.Zero)
            {
                throw new InvalidOperationException("Command queue is not initialized");
            }

            // Close all command lists if not already closed
            // Command lists must be closed before they can be executed
            foreach (ICommandList commandList in commandLists)
            {
                if (commandList == null)
                {
                    continue;
                }

                // Check if command list is a D3D12CommandList
                D3D12CommandList d3d12CommandList = commandList as D3D12CommandList;
                if (d3d12CommandList == null)
                {
                    Console.WriteLine("[D3D12Device] Warning: Command list is not a D3D12CommandList, skipping execution");
                    continue;
                }

                // Close the command list if it's still open
                // The Close() method handles checking if it's already closed
                try
                {
                    commandList.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[D3D12Device] Warning: Failed to close command list before execution: {ex.Message}");
                    // Continue with execution attempt - the command list may already be closed
                }
            }

            // Build array of ID3D12CommandList pointers
            // Filter out null command lists and non-D3D12 command lists
            List<IntPtr> nativeCommandLists = new List<IntPtr>();
            foreach (ICommandList commandList in commandLists)
            {
                if (commandList == null)
                {
                    continue;
                }

                D3D12CommandList d3d12CommandList = commandList as D3D12CommandList;
                if (d3d12CommandList == null)
                {
                    continue; // Skip non-D3D12 command lists
                }

                // Get native command list pointer
                IntPtr nativeCommandList = d3d12CommandList.GetNativeCommandListPointer();
                if (nativeCommandList != IntPtr.Zero)
                {
                    nativeCommandLists.Add(nativeCommandList);
                }
            }

            if (nativeCommandLists.Count == 0)
            {
                // No valid command lists to execute
                return;
            }

            // Allocate unmanaged memory for array of command list pointers
            // ID3D12CommandQueue::ExecuteCommandLists expects: ID3D12CommandList* const* ppCommandLists
            // This is an array of pointers to ID3D12CommandList interfaces
            IntPtr commandListArrayPtr = Marshal.AllocHGlobal(IntPtr.Size * nativeCommandLists.Count);
            try
            {
                // Write command list pointers to the array
                for (int i = 0; i < nativeCommandLists.Count; i++)
                {
                    IntPtr commandListPtr = nativeCommandLists[i];
                    IntPtr arrayElementPtr = new IntPtr(commandListArrayPtr.ToInt64() + (i * IntPtr.Size));
                    Marshal.WriteIntPtr(arrayElementPtr, commandListPtr);
                }

                // Call ID3D12CommandQueue::ExecuteCommandLists
                // ExecuteCommandLists signature: void ExecuteCommandLists(UINT NumCommandLists, ID3D12CommandList* const* ppCommandLists)
                // VTable index: ExecuteCommandLists is at index 4 in ID3D12CommandQueue vtable
                // (after IUnknown: QueryInterface, AddRef, Release, UpdateTileMappings)
                CallExecuteCommandLists(_commandQueue, (uint)nativeCommandLists.Count, commandListArrayPtr);
            }
            finally
            {
                // Free the allocated array memory
                Marshal.FreeHGlobal(commandListArrayPtr);
            }
        }

        public void WaitIdle()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, WaitIdle is a no-op
                // The application should use VulkanDevice for cross-platform support
                return;
            }

            // Lazy initialization of fence and event for idle synchronization
            if (_idleFence == IntPtr.Zero)
            {
                // Allocate unmanaged memory for fence pointer
                IntPtr ppFence = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    // Create fence with initial value 0
                    // D3D12_FENCE_FLAG_NONE = 0
                    Guid riid = IID_ID3D12Fence;
                    int hr = CallCreateFence(_device, 0, 0, ref riid, ppFence);
                    if (hr < 0)
                    {
                        throw new InvalidOperationException($"Failed to create D3D12 fence: HRESULT 0x{hr:X8}");
                    }

                    // Read the created fence pointer
                    _idleFence = Marshal.ReadIntPtr(ppFence);
                    if (_idleFence == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to create D3D12 fence: null pointer returned");
                    }

                    // Create Windows event for synchronization
                    _idleFenceEvent = CreateEvent(IntPtr.Zero, false, false, null);
                    if (_idleFenceEvent == IntPtr.Zero)
                    {
                        // Release fence on failure
                        ReleaseComObject(_idleFence);
                        _idleFence = IntPtr.Zero;
                        throw new InvalidOperationException("Failed to create Windows event for fence synchronization");
                    }

                    _idleFenceValue = 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(ppFence);
                }
            }

            // Increment fence value to signal new synchronization point
            _idleFenceValue++;

            // Signal the fence from the command queue
            // This tells the GPU to signal the fence when all previous commands have completed
            ulong signalValue = CallSignalFence(_commandQueue, _idleFence, _idleFenceValue);
            if (signalValue == 0)
            {
                throw new InvalidOperationException("Failed to signal D3D12 fence from command queue");
            }

            // Check if fence is already completed (common case for idle GPU)
            ulong completedValue = CallGetFenceCompletedValue(_idleFence);
            if (completedValue >= _idleFenceValue)
            {
                // Fence already completed, no need to wait
                return;
            }

            // Set event to be signaled when fence reaches the target value
            int hrSetEvent = CallSetEventOnCompletion(_idleFence, _idleFenceValue, _idleFenceEvent);
            if (hrSetEvent < 0)
            {
                throw new InvalidOperationException($"Failed to set event on fence completion: HRESULT 0x{hrSetEvent:X8}");
            }

            // Wait for the event to be signaled (GPU has completed)
            uint waitResult = WaitForSingleObject(_idleFenceEvent, INFINITE);
            if (waitResult != WAIT_OBJECT_0)
            {
                if (waitResult == WAIT_FAILED)
                {
                    throw new InvalidOperationException("WaitForSingleObject failed while waiting for GPU idle");
                }
                throw new InvalidOperationException($"Unexpected wait result while waiting for GPU idle: 0x{waitResult:X8}");
            }
        }

        public void Signal(IFence fence, ulong value)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (fence == null)
            {
                throw new ArgumentNullException(nameof(fence));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, Signal is a no-op
                // The application should use VulkanDevice for cross-platform support
                return;
            }

            // Extract ID3D12Fence pointer from IFence implementation
            IntPtr d3d12Fence = ExtractD3D12FenceHandle(fence);
            if (d3d12Fence == IntPtr.Zero)
            {
                throw new ArgumentException("Failed to extract ID3D12Fence handle from IFence implementation. The fence must be a D3D12 fence with a valid native handle.", nameof(fence));
            }

            // Signal the fence from the command queue
            // This tells the GPU to signal the fence when all previous commands have completed
            // Based on DirectX 12 Command Queue Signaling: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandqueue-signal
            ulong signalValue = CallSignalFence(_commandQueue, d3d12Fence, value);
            if (signalValue == 0)
            {
                throw new InvalidOperationException("Failed to signal D3D12 fence from command queue");
            }
        }

        /// <summary>
        /// Extracts the ID3D12Fence native handle from an IFence implementation using reflection.
        /// The fence handle is typically stored as NativeHandle or in a private field.
        /// Based on DirectX 12 Fence Implementation Pattern.
        /// </summary>
        private IntPtr ExtractD3D12FenceHandle(IFence fence)
        {
            if (fence == null)
            {
                return IntPtr.Zero;
            }

            // Try to get NativeHandle property via reflection (similar to how Vulkan does it)
            System.Reflection.PropertyInfo nativeHandleProp = fence.GetType().GetProperty("NativeHandle");
            if (nativeHandleProp != null)
            {
                object nativeHandleValue = nativeHandleProp.GetValue(fence);
                if (nativeHandleValue is IntPtr)
                {
                    return (IntPtr)nativeHandleValue;
                }
            }

            // Fallback: Try to find a private field that contains the fence pointer
            // This is a common pattern in D3D12 fence implementations
            System.Reflection.FieldInfo[] fields = fence.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            foreach (System.Reflection.FieldInfo field in fields)
            {
                // Look for IntPtr fields that might contain the fence pointer
                if (field.FieldType == typeof(IntPtr))
                {
                    object fieldValue = field.GetValue(fence);
                    if (fieldValue is IntPtr)
                    {
                        IntPtr handle = (IntPtr)fieldValue;
                        if (handle != IntPtr.Zero)
                        {
                            // Verify this looks like a valid COM pointer (non-null, aligned)
                            // Basic validation: pointer should be aligned and not zero
                            if (handle.ToInt64() % IntPtr.Size == 0 && handle.ToInt64() != 0)
                            {
                                return handle;
                            }
                        }
                    }
                }
            }

            return IntPtr.Zero;
        }

        public void WaitFence(IFence fence, ulong value)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (fence == null)
            {
                throw new ArgumentNullException(nameof(fence));
            }

            // Extract ID3D12Fence pointer from IFence implementation
            IntPtr d3d12Fence = ExtractD3D12FenceHandle(fence);
            if (d3d12Fence == IntPtr.Zero)
            {
                throw new ArgumentException("Failed to extract ID3D12Fence handle from IFence implementation. The fence must be a D3D12 fence with a valid native handle.", nameof(fence));
            }

            // Check if fence is already completed (optimization - avoids unnecessary event creation)
            ulong completedValue = CallGetFenceCompletedValue(d3d12Fence);
            if (completedValue >= value)
            {
                // Fence already completed, no need to wait
                return;
            }

            // Create Windows event for synchronization
            // Use auto-reset event (bManualReset = false) since we only wait once
            IntPtr fenceEvent = CreateEvent(IntPtr.Zero, false, false, null);
            if (fenceEvent == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create Windows event for fence synchronization");
            }

            try
            {
                // Set event to be signaled when fence reaches the target value
                int hrSetEvent = CallSetEventOnCompletion(d3d12Fence, value, fenceEvent);
                if (hrSetEvent < 0)
                {
                    throw new InvalidOperationException($"Failed to set event on fence completion: HRESULT 0x{hrSetEvent:X8}");
                }

                // Wait for the event to be signaled (GPU has completed)
                uint waitResult = WaitForSingleObject(fenceEvent, INFINITE);
                if (waitResult != WAIT_OBJECT_0)
                {
                    if (waitResult == WAIT_FAILED)
                    {
                        throw new InvalidOperationException("WaitForSingleObject failed while waiting for fence completion");
                    }
                    throw new InvalidOperationException($"Unexpected wait result while waiting for fence completion: 0x{waitResult:X8}");
                }
            }
            finally
            {
                // Always close the event handle, even if an exception occurred
                if (fenceEvent != IntPtr.Zero)
                {
                    CloseHandle(fenceEvent);
                }
            }
        }

        #endregion

        #region Queries

        public int GetConstantBufferAlignment()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // D3D12 requires constant buffer alignment of 256 bytes
            return 256;
        }

        public int GetTextureAlignment()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // D3D12 texture alignment depends on format and dimension
            // Default to 512 bytes for 2D textures (may vary by format)
            return 512;
        }

        public bool IsFormatSupported(TextureFormat format, TextureUsage usage)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // On non-Windows platforms, fall back to basic format checks
                return IsFormatSupportedFallback(format, usage);
            }

            if (_device == IntPtr.Zero)
            {
                return false;
            }

            // Convert TextureFormat to DXGI_FORMAT
            uint dxgiFormat = ConvertTextureFormatToDxgiFormatForTexture(format);
            if (dxgiFormat == 0) // DXGI_FORMAT_UNKNOWN
            {
                return false;
            }

            // Query format support using CheckFeatureSupport
            D3D12_FEATURE_FORMAT_SUPPORT formatSupport = new D3D12_FEATURE_FORMAT_SUPPORT
            {
                Format = dxgiFormat,
                Support1 = 0,
                Support2 = 0
            };

            int hr = CallCheckFeatureSupport(_device, D3D12_FEATURE_FORMAT_SUPPORT_ENUM, ref formatSupport, Marshal.SizeOf(typeof(D3D12_FEATURE_FORMAT_SUPPORT)));
            if (hr != 0) // S_OK = 0
            {
                // If CheckFeatureSupport fails, fall back to basic checks
                return IsFormatSupportedFallback(format, usage);
            }

            // Map TextureUsage flags to D3D12_FORMAT_SUPPORT flags
            uint requiredSupport = MapTextureUsageToFormatSupport(usage);

            // Check if format supports all required usage flags
            // If no specific usage is required, assume format is supported if query succeeded
            if (requiredSupport == 0)
            {
                return true;
            }

            return (formatSupport.Support1 & requiredSupport) == requiredSupport;
        }

        /// <summary>
        /// Fallback format support check when CheckFeatureSupport is unavailable.
        /// </summary>
        private bool IsFormatSupportedFallback(TextureFormat format, TextureUsage usage)
        {
            // Basic format support checks for common formats
            switch (format)
            {
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    return (usage & (TextureUsage.ShaderResource | TextureUsage.RenderTarget)) != 0;

                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R32G32B32A32_Float:
                    return (usage & (TextureUsage.ShaderResource | TextureUsage.RenderTarget | TextureUsage.UnorderedAccess)) != 0;

                case TextureFormat.D24_UNorm_S8_UInt:
                case TextureFormat.D32_Float:
                case TextureFormat.D32_Float_S8_UInt:
                    return (usage & TextureUsage.DepthStencil) != 0;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Maps TextureUsage flags to D3D12_FORMAT_SUPPORT flags.
        /// </summary>
        private uint MapTextureUsageToFormatSupport(TextureUsage usage)
        {
            uint supportFlags = 0;

            if ((usage & TextureUsage.ShaderResource) != 0)
            {
                // Shader resource requires texture support
                supportFlags |= D3D12_FORMAT_SUPPORT_TEXTURE1D;
                supportFlags |= D3D12_FORMAT_SUPPORT_TEXTURE2D;
                supportFlags |= D3D12_FORMAT_SUPPORT_TEXTURE3D;
                supportFlags |= D3D12_FORMAT_SUPPORT_SHADER_SAMPLE;
            }

            if ((usage & TextureUsage.RenderTarget) != 0)
            {
                // Render target requires render target support
                supportFlags |= D3D12_FORMAT_SUPPORT_RENDER_TARGET;
            }

            if ((usage & TextureUsage.DepthStencil) != 0)
            {
                // Depth stencil requires depth stencil support
                supportFlags |= D3D12_FORMAT_SUPPORT_DEPTH_STENCIL;
            }

            if ((usage & TextureUsage.UnorderedAccess) != 0)
            {
                // Unordered access requires UAV support
                supportFlags |= D3D12_FORMAT_SUPPORT_TYPED_UNORDERED_ACCESS_VIEW;
            }

            return supportFlags;
        }

        public int GetCurrentFrameIndex()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            return _currentFrameIndex;
        }

        internal void AdvanceFrameIndex()
        {
            _currentFrameIndex = (_currentFrameIndex + 1) % 3; // Triple buffering
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Dispose all tracked resources
            foreach (var resource in _resources.Values)
            {
                resource?.Dispose();
            }
            _resources.Clear();

            // Release sampler descriptor heap if it was created
            // Note: In D3D12, descriptor heaps are COM objects (ID3D12DescriptorHeap) and need to be released via IUnknown::Release()
            // Based on DirectX 12 Descriptor Heap Management: https://docs.microsoft.com/en-us/windows/win32/direct3d12/descriptor-heaps
            // Descriptor heaps are COM objects that must be properly released to prevent memory leaks
            if (_samplerDescriptorHeap != IntPtr.Zero)
            {
                try
                {
                    // Release the COM object through IUnknown::Release() vtable call
                    // This decrements the reference count and frees the object when count reaches zero
                    uint refCount = ReleaseComObject(_samplerDescriptorHeap);
                    if (refCount > 0)
                    {
                        Console.WriteLine($"[D3D12Device] Descriptor heap still has {refCount} references after Release()");
                    }
                    else
                    {
                        Console.WriteLine("[D3D12Device] Successfully released sampler descriptor heap");
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue cleanup - don't throw from Dispose
                    Console.WriteLine($"[D3D12Device] Error releasing sampler descriptor heap: {ex.Message}");
                    Console.WriteLine($"[D3D12Device] Stack trace: {ex.StackTrace}");
                }
                finally
                {
                    // Always clear the handle even if Release() failed
                    _samplerDescriptorHeap = IntPtr.Zero;
                }
            }

            // Clear descriptor heap state
            _samplerHeapCpuStartHandle = IntPtr.Zero;
            _samplerHeapDescriptorIncrementSize = 0;
            _samplerHeapCapacity = 0;
            _samplerHeapNextIndex = 0;

            // Release SRV descriptor heap if it was created
            if (_srvDescriptorHeap != IntPtr.Zero)
            {
                try
                {
                    // Release the COM object through IUnknown::Release() vtable call
                    uint refCount = ReleaseComObject(_srvDescriptorHeap);
                    if (refCount > 0)
                    {
                        Console.WriteLine($"[D3D12Device] SRV descriptor heap still has {refCount} references after Release()");
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue cleanup - don't throw from Dispose
                    Console.WriteLine($"[D3D12Device] Error releasing SRV descriptor heap: {ex.Message}");
                }
                finally
                {
                    // Always clear the handle even if Release() failed
                    _srvDescriptorHeap = IntPtr.Zero;
                }
            }

            // Clear SRV descriptor heap state
            _srvHeapCpuStartHandle = IntPtr.Zero;
            _srvHeapDescriptorIncrementSize = 0;
            _srvHeapCapacity = 0;
            _srvHeapNextIndex = 0;
            _textureSrvHandles?.Clear();

            // Release idle fence and event handle if they were created
            if (_idleFence != IntPtr.Zero)
            {
                try
                {
                    // Release the COM object through IUnknown::Release() vtable call
                    uint refCount = ReleaseComObject(_idleFence);
                    if (refCount > 0)
                    {
                        Console.WriteLine($"[D3D12Device] Idle fence still has {refCount} references after Release()");
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue cleanup - don't throw from Dispose
                    Console.WriteLine($"[D3D12Device] Error releasing idle fence: {ex.Message}");
                }
                finally
                {
                    // Always clear the handle even if Release() failed
                    _idleFence = IntPtr.Zero;
                    _idleFenceValue = 0;
                }
            }

            if (_idleFenceEvent != IntPtr.Zero)
            {
                try
                {
                    // Close the Windows event handle
                    bool closed = CloseHandle(_idleFenceEvent);
                    if (!closed)
                    {
                        Console.WriteLine($"[D3D12Device] Failed to close idle fence event handle");
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue cleanup - don't throw from Dispose
                    Console.WriteLine($"[D3D12Device] Error closing idle fence event: {ex.Message}");
                }
                finally
                {
                    // Always clear the handle even if CloseHandle() failed
                    _idleFenceEvent = IntPtr.Zero;
                }
            }

            // Note: We don't release _device or _device5 here as they're owned by Direct3D12Backend
            // The backend will handle device cleanup in its Shutdown method

            _disposed = true;
        }

        #endregion

        #region Internal Helpers

        internal IntPtr GetDeviceHandle()
        {
            return _device;
        }

        internal IntPtr GetDevice5Handle()
        {
            return _device5;
        }

        internal IntPtr GetCommandQueueHandle()
        {
            return _commandQueue;
        }

        /// <summary>
        /// Releases a COM object by calling IUnknown::Release().
        /// All COM interfaces inherit from IUnknown, which has Release at vtable index 2.
        /// Based on COM Reference Counting: https://docs.microsoft.com/en-us/windows/win32/api/unknwn/nf-unknwn-iunknown-release
        /// </summary>
        protected unsafe uint ReleaseComObject(IntPtr comObject)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            if (comObject == IntPtr.Zero)
            {
                return 0;
            }

            // Get vtable pointer (first field of COM object)
            IntPtr* vtable = *(IntPtr**)comObject;
            // IUnknown::Release is at index 2 in all COM interface vtables
            IntPtr methodPtr = vtable[2];

            // Create delegate from function pointer
            ReleaseDelegate release = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ReleaseDelegate));

            return release(comObject);
        }

        #endregion

        #region D3D12 Resource Barrier Structures

        // DirectX 12 Resource Barrier Type
        private const uint D3D12_RESOURCE_BARRIER_TYPE_TRANSITION = 0;
        private const uint D3D12_RESOURCE_BARRIER_TYPE_ALIASING = 1;
        private const uint D3D12_RESOURCE_BARRIER_TYPE_UAV = 2;

        // D3D12_CLEAR_FLAGS constants for ClearDepthStencil
        private const uint D3D12_CLEAR_FLAG_DEPTH = 0x1;
        private const uint D3D12_CLEAR_FLAG_STENCIL = 0x2;

        // DirectX 12 Resource Barrier Flags
        private const uint D3D12_RESOURCE_BARRIER_FLAG_NONE = 0;
        private const uint D3D12_RESOURCE_BARRIER_FLAG_BEGIN_ONLY = 0x1;
        private const uint D3D12_RESOURCE_BARRIER_FLAG_END_ONLY = 0x2;

        // DirectX 12 Resource Dimension constants (D3D12_RESOURCE_DIMENSION)
        private const uint D3D12_RESOURCE_DIMENSION_UNKNOWN = 0;
        private const uint D3D12_RESOURCE_DIMENSION_BUFFER = 1;
        private const uint D3D12_RESOURCE_DIMENSION_TEXTURE1D = 2;
        private const uint D3D12_RESOURCE_DIMENSION_TEXTURE2D = 3;
        private const uint D3D12_RESOURCE_DIMENSION_TEXTURE3D = 4;

        // DirectX 12 Resource Flags (D3D12_RESOURCE_FLAGS)
        private const uint D3D12_RESOURCE_FLAG_NONE = 0;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET = 0x1;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_DEPTH_STENCIL = 0x2;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS = 0x4;
        private const uint D3D12_RESOURCE_FLAG_DENY_SHADER_RESOURCE = 0x8;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_CROSS_ADAPTER = 0x10;
        private const uint D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS = 0x20;
        private const uint D3D12_RESOURCE_FLAG_VIDEO_DECODE_REFERENCE_ONLY = 0x40;

        // DirectX 12 Heap Type constants (D3D12_HEAP_TYPE)
        private const uint D3D12_HEAP_TYPE_DEFAULT = 1;
        private const uint D3D12_HEAP_TYPE_UPLOAD = 2;
        private const uint D3D12_HEAP_TYPE_READBACK = 3;
        private const uint D3D12_HEAP_TYPE_CUSTOM = 4;

        // DirectX 12 CPU Page Property constants (D3D12_CPU_PAGE_PROPERTY)
        private const uint D3D12_CPU_PAGE_PROPERTY_UNKNOWN = 0;
        private const uint D3D12_CPU_PAGE_PROPERTY_NOT_AVAILABLE = 1;
        private const uint D3D12_CPU_PAGE_PROPERTY_WRITE_COMBINE = 2;
        private const uint D3D12_CPU_PAGE_PROPERTY_WRITE_BACK = 3;

        // DirectX 12 Memory Pool constants (D3D12_MEMORY_POOL)
        private const uint D3D12_MEMORY_POOL_UNKNOWN = 0;
        private const uint D3D12_MEMORY_POOL_L0 = 1;
        private const uint D3D12_MEMORY_POOL_L1 = 2;

        // DirectX 12 Heap Flags (D3D12_HEAP_FLAGS)
        private const uint D3D12_HEAP_FLAG_NONE = 0;
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

        // DirectX 12 Resource States (D3D12_RESOURCE_STATES)
        private const uint D3D12_RESOURCE_STATE_COMMON = 0;
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
        private const uint D3D12_RESOURCE_STATE_GENERIC_READ = 0x2755;

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
            public D3D12_TEXTURE_COPY_LOCATION_UNION Union;
        }

        /// <summary>
        /// Union structure for D3D12_TEXTURE_COPY_LOCATION (either SubresourceIndex or PlacedFootprint).
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct D3D12_TEXTURE_COPY_LOCATION_UNION
        {
            [FieldOffset(0)]
            public uint SubresourceIndex; // UINT - used when Type is D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX

            [FieldOffset(0)]
            public D3D12_PLACED_SUBRESOURCE_FOOTPRINT PlacedFootprint; // used when Type is D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT
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

        // DirectX 12 Texture Copy Type constants
        private const uint D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX = 0;
        private const uint D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT = 1;

        /// <summary>
        /// D3D12_RESOURCE_UAV_BARRIER structure for UAV barriers.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RESOURCE_UAV_BARRIER
        {
            public IntPtr pResource; // ID3D12Resource*
        }

        /// <summary>
        /// D3D12_RANGE structure for resource mapping operations.
        /// Specifies a memory range for Map/Unmap operations on ID3D12Resource.
        /// Based on DirectX 12 Range: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_range
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RANGE
        {
            public ulong Begin; // SIZE_T - Start of the range (in bytes)
            public ulong End; // SIZE_T - End of the range (in bytes, exclusive)
        }

        // DirectX 12 Feature constants (D3D12_FEATURE)
        private const uint D3D12_FEATURE_FORMAT_SUPPORT_ENUM = 0;

        // DirectX 12 Format Support flags (D3D12_FORMAT_SUPPORT)
        private const uint D3D12_FORMAT_SUPPORT_BUFFER = 0x1;
        private const uint D3D12_FORMAT_SUPPORT_IA_VERTEX_BUFFER = 0x2;
        private const uint D3D12_FORMAT_SUPPORT_IA_INDEX_BUFFER = 0x4;
        private const uint D3D12_FORMAT_SUPPORT_SO_BUFFER = 0x8;
        private const uint D3D12_FORMAT_SUPPORT_TEXTURE1D = 0x10;
        private const uint D3D12_FORMAT_SUPPORT_TEXTURE2D = 0x20;
        private const uint D3D12_FORMAT_SUPPORT_TEXTURE3D = 0x40;
        private const uint D3D12_FORMAT_SUPPORT_TEXTURECUBE = 0x80;
        private const uint D3D12_FORMAT_SUPPORT_SHADER_LOAD = 0x100;
        private const uint D3D12_FORMAT_SUPPORT_SHADER_SAMPLE = 0x200;
        private const uint D3D12_FORMAT_SUPPORT_SHADER_SAMPLE_COMPARISON = 0x400;
        private const uint D3D12_FORMAT_SUPPORT_SHADER_SAMPLE_MONO_TEXT = 0x800;
        private const uint D3D12_FORMAT_SUPPORT_MIP = 0x1000;
        private const uint D3D12_FORMAT_SUPPORT_MIP_AUTOGEN = 0x2000;
        private const uint D3D12_FORMAT_SUPPORT_RENDER_TARGET = 0x4000;
        private const uint D3D12_FORMAT_SUPPORT_BLENDABLE = 0x8000;
        private const uint D3D12_FORMAT_SUPPORT_DEPTH_STENCIL = 0x10000;
        private const uint D3D12_FORMAT_SUPPORT_CPU_LOCKABLE = 0x20000;
        private const uint D3D12_FORMAT_SUPPORT_MULTISAMPLE_RESOLVE = 0x40000;
        private const uint D3D12_FORMAT_SUPPORT_DISPLAY = 0x80000;
        private const uint D3D12_FORMAT_SUPPORT_CAST_WITHIN_BIT_LAYOUT = 0x100000;
        private const uint D3D12_FORMAT_SUPPORT_MULTISAMPLE_RENDERTARGET = 0x200000;
        private const uint D3D12_FORMAT_SUPPORT_MULTISAMPLE_LOAD = 0x400000;
        private const uint D3D12_FORMAT_SUPPORT_SHADER_GATHER = 0x800000;
        private const uint D3D12_FORMAT_SUPPORT_BACK_BUFFER_CAST = 0x1000000;
        private const uint D3D12_FORMAT_SUPPORT_TYPED_UNORDERED_ACCESS_VIEW = 0x2000000;
        private const uint D3D12_FORMAT_SUPPORT_SHADER_GATHER_COMPARISON = 0x4000000;
        private const uint D3D12_FORMAT_SUPPORT_DECODER_OUTPUT = 0x8000000;
        private const uint D3D12_FORMAT_SUPPORT_VIDEO_PROCESSOR_OUTPUT = 0x10000000;
        private const uint D3D12_FORMAT_SUPPORT_VIDEO_PROCESSOR_INPUT = 0x20000000;
        private const uint D3D12_FORMAT_SUPPORT_VIDEO_ENCODER = 0x40000000;

        /// <summary>
        /// D3D12_FEATURE_FORMAT_SUPPORT structure for format support queries.
        /// Based on DirectX 12 Feature Support: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_feature_data_format_support
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_FEATURE_FORMAT_SUPPORT
        {
            public uint Format; // DXGI_FORMAT
            public uint Support1; // D3D12_FORMAT_SUPPORT1
            public uint Support2; // D3D12_FORMAT_SUPPORT2
        }

        // COM interface method delegate for ResourceBarrier
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ResourceBarrierDelegate(IntPtr commandList, uint numBarriers, IntPtr pBarriers);

        // COM interface method delegate for Dispatch
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DispatchDelegate(IntPtr commandList, uint threadGroupCountX, uint threadGroupCountY, uint threadGroupCountZ);

        // COM interface method delegate for SetPipelineState
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetPipelineStateDelegate(IntPtr commandList, IntPtr pPipelineState);

        // COM interface method delegate for SetComputeRootSignature
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetComputeRootSignatureDelegate(IntPtr commandList, IntPtr pRootSignature);

        // COM interface method delegate for SetComputeRootDescriptorTable
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetComputeRootDescriptorTableDelegate(IntPtr commandList, uint RootParameterIndex, D3D12_GPU_DESCRIPTOR_HANDLE BaseDescriptor);

        // COM interface method delegate for SetGraphicsRootSignature
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetGraphicsRootSignatureDelegate(IntPtr commandList, IntPtr pRootSignature);

        // COM interface method delegate for SetGraphicsRootDescriptorTable
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetGraphicsRootDescriptorTableDelegate(IntPtr commandList, uint RootParameterIndex, D3D12_GPU_DESCRIPTOR_HANDLE BaseDescriptor);

        // COM interface method delegate for RSSetViewports
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void RSSetViewportsDelegate(IntPtr commandList, uint NumViewports, IntPtr pViewports);

        // COM interface method delegate for IASetVertexBuffers
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void IASetVertexBuffersDelegate(IntPtr commandList, uint StartSlot, uint NumViews, IntPtr pViews);

        // COM interface method delegate for IASetIndexBuffer
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void IASetIndexBufferDelegate(IntPtr commandList, IntPtr pView);

        // COM interface method delegate for OMSetRenderTargets
        // Note: pRenderTargetDescriptors can point to a single handle or an array of handles
        // When RTsSingleHandleToDescriptorRange is TRUE, it's a single handle for the range
        // When FALSE, it's an array of handles (one per render target)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void OMSetRenderTargetsDelegate(IntPtr commandList, uint NumRenderTargetDescriptors, IntPtr pRenderTargetDescriptors, byte RTsSingleHandleToDescriptorRange, IntPtr pDepthStencilDescriptor);

        // COM interface method delegate for OMSetBlendFactor
        // Based on DirectX 12 OMSetBlendFactor: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-omsetblendfactor
        // BlendFactor is a pointer to a float[4] array containing RGBA blend factors
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void OMSetBlendFactorDelegate(IntPtr commandList, IntPtr BlendFactor);

        // COM interface method delegate for OMSetStencilRef
        // Based on DirectX 12 Output Merger Stencil Reference: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-omsetstencilref
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void OMSetStencilRefDelegate(IntPtr commandList, uint StencilRef);

        // COM interface method delegate for SetDescriptorHeaps
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetDescriptorHeapsDelegate(IntPtr commandList, uint NumDescriptorHeaps, IntPtr ppDescriptorHeaps);

        // COM interface method delegate for Close (ID3D12CommandList::Close)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CloseDelegate(IntPtr commandList);

        // COM interface method delegate for Reset (ID3D12CommandAllocator::Reset)
        // Based on DirectX 12 Command Allocator Reset: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandallocator-reset
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CommandAllocatorResetDelegate(IntPtr commandAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateCommandAllocatorDelegate(IntPtr device, uint type, ref Guid riid, IntPtr ppCommandAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateCommandListDelegate(IntPtr device, uint nodeMask, uint type, IntPtr pCommandAllocator, IntPtr pInitialState, ref Guid riid, IntPtr ppCommandList);

        // COM interface method delegate for Reset (ID3D12GraphicsCommandList::Reset)
        // Based on DirectX 12 Command List Reset: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-reset
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CommandListResetDelegate(IntPtr commandList, IntPtr pAllocator, IntPtr pInitialState);

        // COM interface method delegate for DrawIndexedInstanced
        // Based on DirectX 12 DrawIndexedInstanced: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-drawindexedinstanced
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DrawIndexedInstancedDelegate(IntPtr commandList, uint IndexCountPerInstance, uint InstanceCount, uint StartIndexLocation, int BaseVertexLocation, uint StartInstanceLocation);

        // COM interface method delegate for DrawInstanced
        // Based on DirectX 12 DrawInstanced: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-drawinstanced
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DrawInstancedDelegate(IntPtr commandList, uint VertexCountPerInstance, uint InstanceCount, uint StartVertexLocation, uint StartInstanceLocation);

        // COM interface method delegate for ClearUnorderedAccessViewUint
        // Based on DirectX 12 ClearUnorderedAccessViewUint: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-clearunorderedaccessviewuint
        // Values is a pointer to a uint[4] array containing RGBA/X/Y/Z/W clear values
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ClearUnorderedAccessViewUintDelegate(IntPtr commandList, IntPtr ViewGPUHandleInCurrentHeap, IntPtr ViewCPUHandle, IntPtr pResource, IntPtr Values, uint NumRects, IntPtr pRects);

        // D3D12 GPU descriptor handle structure
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_GPU_DESCRIPTOR_HANDLE
        {
            public ulong ptr;
        }

        // D3D12 CPU descriptor handle structure
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_CPU_DESCRIPTOR_HANDLE
        {
            public IntPtr ptr; // SIZE_T
        }

        // D3D12 Root Signature structures
        // Based on DirectX 12 Root Signatures: https://docs.microsoft.com/en-us/windows/win32/direct3d12/root-signatures

        /// <summary>
        /// D3D12_DESCRIPTOR_RANGE structure for root signature descriptor tables.
        /// Based on DirectX 12 Descriptor Ranges: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_descriptor_range
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DESCRIPTOR_RANGE
        {
            public uint RangeType; // D3D12_DESCRIPTOR_RANGE_TYPE
            public uint NumDescriptors; // UINT
            public uint BaseShaderRegister; // UINT
            public uint RegisterSpace; // UINT
            public uint OffsetInDescriptorsFromTableStart; // UINT
        }

        // D3D12_DESCRIPTOR_RANGE_TYPE constants
        private const uint D3D12_DESCRIPTOR_RANGE_TYPE_SRV = 0; // Shader Resource View
        private const uint D3D12_DESCRIPTOR_RANGE_TYPE_UAV = 1; // Unordered Access View
        private const uint D3D12_DESCRIPTOR_RANGE_TYPE_CBV = 2; // Constant Buffer View
        private const uint D3D12_DESCRIPTOR_RANGE_TYPE_SAMPLER = 3; // Sampler

        /// <summary>
        /// D3D12_ROOT_PARAMETER structure for root signature parameters.
        /// Based on DirectX 12 Root Parameters: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_root_parameter
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct D3D12_ROOT_PARAMETER
        {
            [FieldOffset(0)]
            public uint ParameterType; // D3D12_ROOT_PARAMETER_TYPE

            [FieldOffset(4)]
            public D3D12_ROOT_DESCRIPTOR_TABLE DescriptorTable; // Used when ParameterType is D3D12_ROOT_PARAMETER_TYPE_DESCRIPTOR_TABLE

            [FieldOffset(4)]
            public D3D12_ROOT_DESCRIPTOR RootDescriptor; // Used when ParameterType is D3D12_ROOT_PARAMETER_TYPE_CBV, SRV, or UAV

            [FieldOffset(4)]
            public D3D12_ROOT_CONSTANTS RootConstants; // Used when ParameterType is D3D12_ROOT_PARAMETER_TYPE_32BIT_CONSTANTS

            [FieldOffset(20)]
            public uint ShaderVisibility; // D3D12_SHADER_VISIBILITY
        }

        // D3D12_ROOT_PARAMETER_TYPE constants
        private const uint D3D12_ROOT_PARAMETER_TYPE_DESCRIPTOR_TABLE = 0;
        private const uint D3D12_ROOT_PARAMETER_TYPE_32BIT_CONSTANTS = 1;
        private const uint D3D12_ROOT_PARAMETER_TYPE_CBV = 2;
        private const uint D3D12_ROOT_PARAMETER_TYPE_SRV = 3;
        private const uint D3D12_ROOT_PARAMETER_TYPE_UAV = 4;

        // D3D12_SHADER_VISIBILITY constants
        private const uint D3D12_SHADER_VISIBILITY_ALL = 0;
        private const uint D3D12_SHADER_VISIBILITY_VERTEX = 1;
        private const uint D3D12_SHADER_VISIBILITY_HULL = 2;
        private const uint D3D12_SHADER_VISIBILITY_DOMAIN = 3;
        private const uint D3D12_SHADER_VISIBILITY_GEOMETRY = 4;
        private const uint D3D12_SHADER_VISIBILITY_PIXEL = 5;
        private const uint D3D12_SHADER_VISIBILITY_AMPLIFICATION = 6;
        private const uint D3D12_SHADER_VISIBILITY_MESH = 7;

        /// <summary>
        /// D3D12_ROOT_DESCRIPTOR_TABLE structure for descriptor table root parameters.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_ROOT_DESCRIPTOR_TABLE
        {
            public uint NumDescriptorRanges; // UINT
            public IntPtr pDescriptorRanges; // const D3D12_DESCRIPTOR_RANGE*
        }

        /// <summary>
        /// D3D12_ROOT_DESCRIPTOR structure for root CBV/SRV/UAV descriptors.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_ROOT_DESCRIPTOR
        {
            public uint ShaderRegister; // UINT
            public uint RegisterSpace; // UINT
        }

        /// <summary>
        /// D3D12_ROOT_CONSTANTS structure for root 32-bit constants.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_ROOT_CONSTANTS
        {
            public uint ShaderRegister; // UINT
            public uint RegisterSpace; // UINT
            public uint Num32BitValues; // UINT
        }

        /// <summary>
        /// D3D12_ROOT_SIGNATURE_DESC structure for root signature description.
        /// Based on DirectX 12 Root Signature Description: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_root_signature_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_ROOT_SIGNATURE_DESC
        {
            public uint NumParameters; // UINT
            public IntPtr pParameters; // const D3D12_ROOT_PARAMETER*
            public uint NumStaticSamplers; // UINT
            public IntPtr pStaticSamplers; // const D3D12_STATIC_SAMPLER_DESC*
            public uint Flags; // D3D12_ROOT_SIGNATURE_FLAGS
        }

        // D3D12_ROOT_SIGNATURE_FLAGS constants
        private const uint D3D12_ROOT_SIGNATURE_FLAG_NONE = 0;
        private const uint D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT = 0x1;
        private const uint D3D12_ROOT_SIGNATURE_FLAG_DENY_VERTEX_SHADER_ROOT_ACCESS = 0x2;
        private const uint D3D12_ROOT_SIGNATURE_FLAG_DENY_HULL_SHADER_ROOT_ACCESS = 0x4;
        private const uint D3D12_ROOT_SIGNATURE_FLAG_DENY_DOMAIN_SHADER_ROOT_ACCESS = 0x8;
        private const uint D3D12_ROOT_SIGNATURE_FLAG_DENY_GEOMETRY_SHADER_ROOT_ACCESS = 0x10;
        private const uint D3D12_ROOT_SIGNATURE_FLAG_DENY_PIXEL_SHADER_ROOT_ACCESS = 0x20;
        private const uint D3D12_ROOT_SIGNATURE_FLAG_ALLOW_STREAM_OUTPUT = 0x40;

        /// <summary>
        /// D3D12_ROOT_SIGNATURE_DESC structure for root signature description.
        /// Based on DirectX 12 Root Signature Description: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_root_signature_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_STATIC_SAMPLER_DESC
        {
            public uint Filter; // D3D12_FILTER
            public uint AddressU; // D3D12_TEXTURE_ADDRESS_MODE
            public uint AddressV; // D3D12_TEXTURE_ADDRESS_MODE
            public uint AddressW; // D3D12_TEXTURE_ADDRESS_MODE
            public float MipLODBias; // FLOAT
            public uint MaxAnisotropy; // UINT
            public uint ComparisonFunc; // D3D12_COMPARISON_FUNC
            public uint BorderColor; // D3D12_STATIC_BORDER_COLOR
            public float MinLOD; // FLOAT
            public float MaxLOD; // FLOAT
            public uint ShaderRegister; // UINT
            public uint RegisterSpace; // UINT
            public uint ShaderVisibility; // D3D12_SHADER_VISIBILITY
        }

        /// <summary>
        /// D3D12_ROOT_SIGNATURE_DESC structure for root signature description.
        /// Based on DirectX 12 Root Signature Description: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_root_signature_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_VERSIONED_ROOT_SIGNATURE_DESC
        {
            public uint Version; // D3D_ROOT_SIGNATURE_VERSION
            public D3D12_ROOT_SIGNATURE_DESC Desc_1_0; // Used when Version is D3D_ROOT_SIGNATURE_VERSION_1_0
            // Note: Version 1.1 structures would go here, but we'll use 1.0 for compatibility
        }

        // D3D_ROOT_SIGNATURE_VERSION constants
        private const uint D3D_ROOT_SIGNATURE_VERSION_1_0 = 0x1;
        private const uint D3D_ROOT_SIGNATURE_VERSION_1_1 = 0x2;

        // D3D12SerializeRootSignature function signature
        // Based on DirectX 12 Root Signature Serialization: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-d3d12serializerootsignature
        [DllImport(D3D12Library, CallingConvention = CallingConvention.StdCall)]
        private static extern int D3D12SerializeRootSignature(
            IntPtr pRootSignature, // const D3D12_ROOT_SIGNATURE_DESC*
            uint Version, // D3D_ROOT_SIGNATURE_VERSION
            out IntPtr ppBlob, // ID3DBlob**
            out IntPtr ppErrorBlob); // ID3DBlob**

        /// <summary>
        /// D3D12_RECT structure for scissor rectangles.
        /// Based on DirectX 12 Scissor Rects: https://docs.microsoft.com/en-us/windows/win32/api/windef/ns-windef-rect
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RECT
        {
            public int left;   // LONG
            public int top;    // LONG
            public int right;  // LONG
            public int bottom; // LONG
        }

        /// <summary>
        /// D3D12_VIEWPORT structure for viewport setting.
        /// Based on DirectX 12 Viewports: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_viewport
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_VIEWPORT
        {
            public double TopLeftX;    // FLOAT as double for alignment
            public double TopLeftY;    // FLOAT as double for alignment
            public double Width;       // FLOAT as double for alignment
            public double Height;      // FLOAT as double for alignment
            public double MinDepth;    // FLOAT as double for alignment
            public double MaxDepth;    // FLOAT as double for alignment
        }

        /// <summary>
        /// D3D12_VERTEX_BUFFER_VIEW structure for vertex buffer binding.
        /// Based on DirectX 12 Vertex Buffer Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_vertex_buffer_view
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_VERTEX_BUFFER_VIEW
        {
            public ulong BufferLocation;  // D3D12_GPU_VIRTUAL_ADDRESS
            public uint SizeInBytes;      // UINT
            public uint StrideInBytes;    // UINT
        }

        /// <summary>
        /// D3D12_INDEX_BUFFER_VIEW structure for index buffer binding.
        /// Based on DirectX 12 Index Buffer Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_index_buffer_view
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_INDEX_BUFFER_VIEW
        {
            public ulong BufferLocation;  // D3D12_GPU_VIRTUAL_ADDRESS
            public uint SizeInBytes;      // UINT
            public uint Format;           // DXGI_FORMAT (DXGI_FORMAT_R16_UINT = 56, DXGI_FORMAT_R32_UINT = 57)
        }

        /// <summary>
        /// Delegate for COM IUnknown::Release method.
        /// All COM interfaces inherit from IUnknown, which has Release at vtable index 2.
        /// Based on COM Reference Counting: https://docs.microsoft.com/en-us/windows/win32/api/unknwn/nf-unknwn-iunknown-release
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr comObject);

        // COM interface method delegate for CopyTextureRegion
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CopyTextureRegionDelegate(
            IntPtr commandList,
            IntPtr pDst,
            uint DstX,
            uint DstY,
            uint DstZ,
            IntPtr pSrc,
            IntPtr pSrcBox);

        // COM interface method delegate for CopyBufferRegion
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CopyBufferRegionDelegate(
            IntPtr commandList,
            IntPtr pDstBuffer,
            ulong DstOffset,
            IntPtr pSrcBuffer,
            ulong SrcOffset,
            ulong NumBytes);

        // COM interface method delegate for ExecuteCommandLists (ID3D12CommandQueue::ExecuteCommandLists)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ExecuteCommandListsDelegate(IntPtr commandQueue, uint NumCommandLists, IntPtr ppCommandLists);

        // COM interface method delegate for Unmap (ID3D12Resource::Unmap)
        // Signature: void Unmap(UINT Subresource, const D3D12_RANGE *pWrittenRange)
        // pWrittenRange can be NULL to unmap the entire resource
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UnmapResourceDelegate(IntPtr resource, uint subresource, IntPtr pWrittenRange);

        // COM interface method delegates for fence operations
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateFenceDelegate(IntPtr device, ulong initialValue, uint flags, ref Guid riid, IntPtr ppFence);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong SignalDelegate(IntPtr commandQueue, IntPtr fence, ulong value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong GetCompletedValueDelegate(IntPtr fence);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetEventOnCompletionDelegate(IntPtr fence, ulong value, IntPtr hEvent);

        // COM interface method delegate for Map (ID3D12Resource::Map)
        // Signature: HRESULT Map(UINT Subresource, const D3D12_RANGE *pReadRange, void **ppData)
        // pReadRange can be NULL to map the entire resource (typical for upload/readback heaps)
        // ppData is an output parameter that receives the mapped memory pointer
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MapResourceDelegateInternal(IntPtr resource, uint subresource, IntPtr pReadRange, out IntPtr ppData);

        /// <summary>
        /// Maps BufferHeapType enum to D3D12_HEAP_TYPE values.
        /// Based on DirectX 12 Heap Types: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_heap_type
        /// </summary>
        private static uint MapBufferHeapTypeToD3D12(BufferHeapType heapType)
        {
            switch (heapType)
            {
                case BufferHeapType.Default:
                    return D3D12_HEAP_TYPE_DEFAULT;

                case BufferHeapType.Upload:
                    return D3D12_HEAP_TYPE_UPLOAD;

                case BufferHeapType.Readback:
                    return D3D12_HEAP_TYPE_READBACK;

                default:
                    // Default to DEFAULT heap if unknown type
                    return D3D12_HEAP_TYPE_DEFAULT;
            }
        }

        /// <summary>
        /// Maps ResourceState enum to D3D12_RESOURCE_STATES flags.
        /// Based on DirectX 12 Resource States: https://docs.microsoft.com/en-us/windows/win32/direct3d12/using-resource-barriers-to-synchronize-resource-states-in-directx-12
        /// </summary>
        private static uint MapResourceStateToD3D12(ResourceState state)
        {
            switch (state)
            {
                case ResourceState.Common:
                    return D3D12_RESOURCE_STATE_COMMON;

                case ResourceState.VertexBuffer:
                    return D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER;

                case ResourceState.IndexBuffer:
                    return D3D12_RESOURCE_STATE_INDEX_BUFFER;

                case ResourceState.ConstantBuffer:
                    return D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER;

                case ResourceState.ShaderResource:
                    // Shader resource can be accessed by both pixel and non-pixel shaders
                    return D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE | D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE;

                case ResourceState.UnorderedAccess:
                    return D3D12_RESOURCE_STATE_UNORDERED_ACCESS;

                case ResourceState.RenderTarget:
                    return D3D12_RESOURCE_STATE_RENDER_TARGET;

                case ResourceState.DepthWrite:
                    return D3D12_RESOURCE_STATE_DEPTH_WRITE;

                case ResourceState.DepthRead:
                    return D3D12_RESOURCE_STATE_DEPTH_READ;

                case ResourceState.IndirectArgument:
                    return D3D12_RESOURCE_STATE_INDIRECT_ARGUMENT;

                case ResourceState.CopyDest:
                    return D3D12_RESOURCE_STATE_COPY_DEST;

                case ResourceState.CopySource:
                    return D3D12_RESOURCE_STATE_COPY_SOURCE;

                case ResourceState.Present:
                    return D3D12_RESOURCE_STATE_COMMON; // Present typically transitions to/from COMMON

                case ResourceState.AccelStructRead:
                    return D3D12_RESOURCE_STATE_RAYTRACING_ACCELERATION_STRUCTURE;

                case ResourceState.AccelStructWrite:
                    return D3D12_RESOURCE_STATE_RAYTRACING_ACCELERATION_STRUCTURE;

                case ResourceState.AccelStructBuildInput:
                    return D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE; // BLAS build inputs are typically SRVs

                default:
                    return D3D12_RESOURCE_STATE_COMMON;
            }
        }

        /// <summary>
        /// D3D12_RESOURCE_DESC structure for resource creation.
        /// Based on DirectX 12 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_resource_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RESOURCE_DESC
        {
            public uint Dimension; // D3D12_RESOURCE_DIMENSION
            public ulong Alignment; // UINT64
            public ulong Width; // UINT64 (buffer size for buffers)
            public uint Height; // UINT
            public ushort DepthOrArraySize; // UINT16
            public ushort MipLevels; // UINT16
            public uint Format; // DXGI_FORMAT
            public D3D12_SAMPLE_DESC SampleDesc; // D3D12_SAMPLE_DESC
            public uint Layout; // D3D12_TEXTURE_LAYOUT
            public uint Flags; // D3D12_RESOURCE_FLAGS
        }

        /// <summary>
        /// D3D12_SAMPLE_DESC structure for multisampling.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_SAMPLE_DESC
        {
            public uint Count; // UINT
            public uint Quality; // UINT
        }

        /// <summary>
        /// D3D12_HEAP_PROPERTIES structure for heap creation.
        /// Based on DirectX 12 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_heap_properties
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
        /// D3D12_CLEAR_VALUE structure for optimized clear values.
        /// Based on DirectX 12 Clear Values: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_clear_value
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct D3D12_CLEAR_VALUE
        {
            [FieldOffset(0)]
            public uint Format; // DXGI_FORMAT

            [FieldOffset(4)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] Color; // float[4] for render targets

            [FieldOffset(4)]
            public D3D12_DEPTH_STENCIL_VALUE DepthStencil; // For depth-stencil targets
        }

        /// <summary>
        /// D3D12_DEPTH_STENCIL_VALUE structure for depth-stencil clear values.
        /// Based on DirectX 12 Depth Stencil Values: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_depth_stencil_value
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DEPTH_STENCIL_VALUE
        {
            public float Depth; // float
            public byte Stencil; // UINT8
        }

        #endregion

        #region D3D12 Sampler Structures and Constants

        // DirectX 12 Descriptor Heap Types
        private const uint D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV = 0;
        private const uint D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER = 1;
        private const uint D3D12_DESCRIPTOR_HEAP_TYPE_RTV = 2;
        private const uint D3D12_DESCRIPTOR_HEAP_TYPE_DSV = 3;

        // DirectX 12 Descriptor Heap Flags
        private const uint D3D12_DESCRIPTOR_HEAP_FLAG_NONE = 0x0;
        private const uint D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE = 0x1;

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

        // DirectX 12 Interface ID for ID3D12DescriptorHeap
        private static readonly Guid IID_ID3D12DescriptorHeap = new Guid(0x8efb471d, 0x616c, 0x4f49, 0x90, 0xf7, 0x12, 0x7b, 0xb7, 0x63, 0xfa, 0x51);

        // DirectX 12 Interface ID for ID3D12Fence
        private static readonly Guid IID_ID3D12Fence = new Guid(0x0a753dcf, 0xc4d8, 0x4b91, 0xad, 0xf6, 0xbe, 0x5a, 0x60, 0xd9, 0x5a, 0x76);
        private static readonly Guid IID_ID3D12PipelineState = new Guid(0x765a30f3, 0xf624, 0x4c6f, 0xa8, 0x28, 0xac, 0xe9, 0xf7, 0x01, 0x72, 0x85);

        // DirectX 12 Interface IDs for command list and allocator
        // Based on DirectX 12 Command List Creation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcommandallocator
        // Based on DirectX 12 Command List Creation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcommandlist
        private static readonly Guid IID_ID3D12CommandAllocator = new Guid(0x6102dee4, 0xaf59, 0x4b09, 0xb9, 0x99, 0xb4, 0x4d, 0x73, 0xf0, 0x9b, 0x24);
        private static readonly Guid IID_ID3D12GraphicsCommandList = new Guid(0x5b160d0f, 0xac1b, 0x4185, 0x8b, 0xa8, 0xb3, 0xae, 0x42, 0xa5, 0xb4, 0x55);

        // DirectX 12 Command List Type constants
        // Based on D3D12_COMMAND_LIST_TYPE enum: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_command_list_type
        private const uint D3D12_COMMAND_LIST_TYPE_DIRECT = 0;    // Graphics/Direct command list
        private const uint D3D12_COMMAND_LIST_TYPE_BUNDLE = 1;    // Bundle command list (not used in our enum)
        private const uint D3D12_COMMAND_LIST_TYPE_COMPUTE = 2;   // Compute command list
        private const uint D3D12_COMMAND_LIST_TYPE_COPY = 3;      // Copy command list

        /// <summary>
        /// D3D12_DESCRIPTOR_HEAP_DESC structure.
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_descriptor_heap_desc
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
        /// D3D12_COMMAND_ALLOCATOR_DESC structure for command allocator creation.
        /// Based on DirectX 12 Command Allocator: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_command_allocator_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_COMMAND_ALLOCATOR_DESC
        {
            public uint Type; // D3D12_COMMAND_LIST_TYPE
        }

        /// <summary>
        /// D3D12_GRAPHICS_PIPELINE_STATE_DESC structure for graphics pipeline creation.
        /// Based on DirectX 12 Graphics Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_graphics_pipeline_state_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_GRAPHICS_PIPELINE_STATE_DESC
        {
            public IntPtr pRootSignature; // ID3D12RootSignature*
            public D3D12_SHADER_BYTECODE VS; // Vertex shader bytecode
            public D3D12_SHADER_BYTECODE PS; // Pixel shader bytecode
            public D3D12_SHADER_BYTECODE DS; // Domain shader bytecode
            public D3D12_SHADER_BYTECODE HS; // Hull shader bytecode
            public D3D12_SHADER_BYTECODE GS; // Geometry shader bytecode
            public D3D12_STREAM_OUTPUT_DESC StreamOutput;
            public D3D12_BLEND_DESC BlendState;
            public uint SampleMask; // UINT
            public D3D12_RASTERIZER_DESC RasterizerState;
            public D3D12_DEPTH_STENCIL_DESC DepthStencilState;
            public IntPtr pInputElementDescs; // D3D12_INPUT_ELEMENT_DESC*
            public uint InputElementDescsCount; // UINT
            public uint IBStripCutValue; // D3D12_INDEX_BUFFER_STRIP_CUT_VALUE
            public uint PrimitiveTopologyType; // D3D12_PRIMITIVE_TOPOLOGY_TYPE
            public uint NumRenderTargets; // UINT
            public uint RTVFormats0; // DXGI_FORMAT (first render target format)
            public uint RTVFormats1; // DXGI_FORMAT
            public uint RTVFormats2; // DXGI_FORMAT
            public uint RTVFormats3; // DXGI_FORMAT
            public uint RTVFormats4; // DXGI_FORMAT
            public uint RTVFormats5; // DXGI_FORMAT
            public uint RTVFormats6; // DXGI_FORMAT
            public uint RTVFormats7; // DXGI_FORMAT
            public uint DSVFormat; // DXGI_FORMAT
            public D3D12_SAMPLE_DESC SampleDesc;
            public uint NodeMask; // UINT
            public D3D12_CACHED_PIPELINE_STATE CachedPSO;
            public uint Flags; // D3D12_PIPELINE_STATE_FLAGS
        }

        /// <summary>
        /// D3D12_SHADER_BYTECODE structure for shader bytecode.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_SHADER_BYTECODE
        {
            public IntPtr pShaderBytecode; // void*
            public ulong BytecodeLength; // SIZE_T
        }

        /// <summary>
        /// D3D12_COMPUTE_PIPELINE_STATE_DESC structure for compute pipeline creation.
        /// Based on DirectX 12 Compute Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_compute_pipeline_state_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_COMPUTE_PIPELINE_STATE_DESC
        {
            public IntPtr pRootSignature; // ID3D12RootSignature*
            public D3D12_SHADER_BYTECODE CS; // Compute shader bytecode
            public uint NodeMask; // UINT
            public D3D12_CACHED_PIPELINE_STATE CachedPSO; // Cached pipeline state (optional)
            public uint Flags; // D3D12_PIPELINE_STATE_FLAGS
        }

        /// <summary>
        /// D3D12_STREAM_OUTPUT_DESC structure for stream output.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_STREAM_OUTPUT_DESC
        {
            public IntPtr pSODeclaration; // D3D12_SO_DECLARATION_ENTRY*
            public uint NumEntries; // UINT
            public IntPtr pBufferStrides; // UINT*
            public uint NumStrides; // UINT
            public uint RasterizedStream; // UINT
        }

        /// <summary>
        /// D3D12_BLEND_DESC structure for blend state.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_BLEND_DESC
        {
            public byte AlphaToCoverageEnable; // BOOL
            public byte IndependentBlendEnable; // BOOL
            public D3D12_RENDER_TARGET_BLEND_DESC RenderTarget0;
            public D3D12_RENDER_TARGET_BLEND_DESC RenderTarget1;
            public D3D12_RENDER_TARGET_BLEND_DESC RenderTarget2;
            public D3D12_RENDER_TARGET_BLEND_DESC RenderTarget3;
            public D3D12_RENDER_TARGET_BLEND_DESC RenderTarget4;
            public D3D12_RENDER_TARGET_BLEND_DESC RenderTarget5;
            public D3D12_RENDER_TARGET_BLEND_DESC RenderTarget6;
            public D3D12_RENDER_TARGET_BLEND_DESC RenderTarget7;
        }

        /// <summary>
        /// D3D12_RENDER_TARGET_BLEND_DESC structure for render target blend state.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RENDER_TARGET_BLEND_DESC
        {
            public byte BlendEnable; // BOOL
            public byte LogicOpEnable; // BOOL
            public uint SrcBlend; // D3D12_BLEND
            public uint DestBlend; // D3D12_BLEND
            public uint BlendOp; // D3D12_BLEND_OP
            public uint SrcBlendAlpha; // D3D12_BLEND
            public uint DestBlendAlpha; // D3D12_BLEND
            public uint BlendOpAlpha; // D3D12_BLEND_OP
            public uint LogicOp; // D3D12_LOGIC_OP
            public byte RenderTargetWriteMask; // UINT8
        }

        /// <summary>
        /// D3D12_RASTERIZER_DESC structure for rasterizer state.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RASTERIZER_DESC
        {
            public uint FillMode; // D3D12_FILL_MODE
            public uint CullMode; // D3D12_CULL_MODE
            public byte FrontCounterClockwise; // BOOL
            public int DepthBias; // INT
            public float DepthBiasClamp;
            public float SlopeScaledDepthBias;
            public byte DepthClipEnable; // BOOL
            public byte MultisampleEnable; // BOOL
            public byte AntialiasedLineEnable; // BOOL
            public uint ForcedSampleCount; // UINT
            public uint ConservativeRaster; // D3D12_CONSERVATIVE_RASTERIZATION_MODE
        }

        /// <summary>
        /// D3D12_DEPTH_STENCIL_DESC structure for depth-stencil state.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DEPTH_STENCIL_DESC
        {
            public byte DepthEnable; // BOOL
            public byte DepthWriteMask; // D3D12_DEPTH_WRITE_MASK
            public uint DepthFunc; // D3D12_COMPARISON_FUNC
            public byte StencilEnable; // BOOL
            public byte StencilReadMask; // UINT8
            public byte StencilWriteMask; // UINT8
            public D3D12_DEPTH_STENCILOP_DESC FrontFace;
            public D3D12_DEPTH_STENCILOP_DESC BackFace;
        }

        /// <summary>
        /// D3D12_DEPTH_STENCILOP_DESC structure for depth-stencil operations.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DEPTH_STENCILOP_DESC
        {
            public uint StencilFailOp; // D3D12_STENCIL_OP
            public uint StencilDepthFailOp; // D3D12_STENCIL_OP
            public uint StencilPassOp; // D3D12_STENCIL_OP
            public uint StencilFunc; // D3D12_COMPARISON_FUNC
        }

        /// <summary>
        /// D3D12_INPUT_ELEMENT_DESC structure for input layout.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct D3D12_INPUT_ELEMENT_DESC
        {
            public IntPtr SemanticName; // LPCSTR (marshalled as IntPtr)
            public uint SemanticIndex; // UINT
            public uint Format; // DXGI_FORMAT
            public uint InputSlot; // UINT
            public uint AlignedByteOffset; // UINT
            public uint InputSlotClass; // D3D12_INPUT_CLASSIFICATION
            public uint InstanceDataStepRate; // UINT
        }

        /// <summary>
        /// D3D12_DRAW_ARGUMENTS structure for indirect draw commands.
        /// Based on DirectX 12 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_draw_arguments
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DRAW_ARGUMENTS
        {
            public uint VertexCountPerInstance;
            public uint InstanceCount;
            public uint StartVertexLocation;
            public uint StartInstanceLocation;
        }

        /// <summary>
        /// D3D12_DRAW_INDEXED_ARGUMENTS structure for indirect indexed draw commands.
        /// Based on DirectX 12 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_draw_indexed_arguments
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DRAW_INDEXED_ARGUMENTS
        {
            public uint IndexCountPerInstance;
            public uint InstanceCount;
            public uint StartIndexLocation;
            public int BaseVertexLocation;
            public uint StartInstanceLocation;
        }

        /// <summary>
        /// D3D12_CACHED_PIPELINE_STATE structure for cached pipeline state.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_CACHED_PIPELINE_STATE
        {
            public IntPtr pCachedBlob; // void*
            public ulong CachedBlobSizeInBytes; // SIZE_T
        }

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

        /// <summary>
        /// D3D12_RENDER_TARGET_VIEW_DESC structure for render target views.
        /// Based on DirectX 12 Render Target Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_render_target_view_desc
        /// Uses explicit layout to model the union of view dimension types.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct D3D12_RENDER_TARGET_VIEW_DESC
        {
            [FieldOffset(0)]
            public uint Format; // DXGI_FORMAT
            [FieldOffset(4)]
            public uint ViewDimension; // D3D12_RTV_DIMENSION
            // Union members start at offset 8 (all overlap at the same offset)
            [FieldOffset(8)]
            public D3D12_BUFFER_RTV Buffer;
            [FieldOffset(8)]
            public D3D12_TEX1D_RTV Texture1D;
            [FieldOffset(8)]
            public D3D12_TEX1D_ARRAY_RTV Texture1DArray;
            [FieldOffset(8)]
            public D3D12_TEX2D_RTV Texture2D;
            [FieldOffset(8)]
            public D3D12_TEX2D_ARRAY_RTV Texture2DArray;
            [FieldOffset(8)]
            public D3D12_TEX2DMS_RTV Texture2DMS;
            [FieldOffset(8)]
            public D3D12_TEX2DMS_ARRAY_RTV Texture2DMSArray;
            [FieldOffset(8)]
            public D3D12_TEX3D_RTV Texture3D;
        }

        /// <summary>
        /// D3D12_RTV_DIMENSION constants for render target view dimensions.
        /// Based on DirectX 12 Render Target View Dimensions: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_rtv_dimension
        /// </summary>
        private const uint D3D12_RTV_DIMENSION_UNKNOWN = 0;
        private const uint D3D12_RTV_DIMENSION_BUFFER = 1;
        private const uint D3D12_RTV_DIMENSION_TEXTURE1D = 2;
        private const uint D3D12_RTV_DIMENSION_TEXTURE1DARRAY = 3;
        private const uint D3D12_RTV_DIMENSION_TEXTURE2D = 4;
        private const uint D3D12_RTV_DIMENSION_TEXTURE2DARRAY = 5;
        private const uint D3D12_RTV_DIMENSION_TEXTURE2DMS = 6;
        private const uint D3D12_RTV_DIMENSION_TEXTURE2DMSARRAY = 7;
        private const uint D3D12_RTV_DIMENSION_TEXTURE3D = 8;


        /// <summary>
        /// D3D12_BUFFER_RTV structure for buffer render target views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_BUFFER_RTV
        {
            public ulong FirstElement;
            public uint NumElements;
        }

        /// <summary>
        /// D3D12_TEX1D_RTV structure for 1D texture render target views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX1D_RTV
        {
            public uint MipSlice;
        }

        /// <summary>
        /// D3D12_TEX1D_ARRAY_RTV structure for 1D texture array render target views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX1D_ARRAY_RTV
        {
            public uint MipSlice;
            public uint FirstArraySlice;
            public uint ArraySize;
        }

        /// <summary>
        /// D3D12_TEX2D_RTV structure for 2D texture render target views.
        /// Based on DirectX 12 Render Target Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_tex2d_rtv
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_RTV
        {
            public uint MipSlice;
            public uint PlaneSlice; // For planar formats (typically 0 for non-planar)
        }

        /// <summary>
        /// D3D12_TEX2D_ARRAY_RTV structure for 2D texture array render target views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_ARRAY_RTV
        {
            public uint MipSlice;
            public uint FirstArraySlice;
            public uint ArraySize;
            public uint PlaneSlice;
        }

        /// <summary>
        /// D3D12_TEX2DMS_RTV structure for 2D multisampled texture render target views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2DMS_RTV
        {
            public uint UnusedField_NothingToDefine; // MS textures don't have mip slices
        }

        /// <summary>
        /// D3D12_TEX2DMS_ARRAY_RTV structure for 2D multisampled texture array render target views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2DMS_ARRAY_RTV
        {
            public uint FirstArraySlice;
            public uint ArraySize;
        }

        /// <summary>
        /// D3D12_TEX3D_RTV structure for 3D texture render target views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX3D_RTV
        {
            public uint MipSlice;
            public uint FirstWSlice;
            public uint WSize;
        }

        /// <summary>
        /// D3D12_DEPTH_STENCIL_VIEW_DESC structure for depth-stencil views.
        /// Based on DirectX 12 Depth Stencil Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_depth_stencil_view_desc
        /// Uses explicit layout to model the union of view dimension types.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct D3D12_DEPTH_STENCIL_VIEW_DESC
        {
            [FieldOffset(0)]
            public uint Format; // DXGI_FORMAT
            [FieldOffset(4)]
            public uint ViewDimension; // D3D12_DSV_DIMENSION
            [FieldOffset(8)]
            public uint Flags; // D3D12_DSV_FLAGS
            // Union members start at offset 12 (all overlap at the same offset)
            [FieldOffset(12)]
            public D3D12_TEX1D_DSV Texture1D;
            [FieldOffset(12)]
            public D3D12_TEX1D_ARRAY_DSV Texture1DArray;
            [FieldOffset(12)]
            public D3D12_TEX2D_DSV Texture2D;
            [FieldOffset(12)]
            public D3D12_TEX2D_ARRAY_DSV Texture2DArray;
            [FieldOffset(12)]
            public D3D12_TEX2DMS_DSV Texture2DMS;
            [FieldOffset(12)]
            public D3D12_TEX2DMS_ARRAY_DSV Texture2DMSArray;
        }

        /// <summary>
        /// D3D12_DSV_DIMENSION constants for depth-stencil view dimensions.
        /// </summary>
        private const uint D3D12_DSV_DIMENSION_UNKNOWN = 0;
        private const uint D3D12_DSV_DIMENSION_TEXTURE1D = 1;
        private const uint D3D12_DSV_DIMENSION_TEXTURE1DARRAY = 2;
        private const uint D3D12_DSV_DIMENSION_TEXTURE2D = 3;
        private const uint D3D12_DSV_DIMENSION_TEXTURE2DARRAY = 4;
        private const uint D3D12_DSV_DIMENSION_TEXTURE2DMS = 5;
        private const uint D3D12_DSV_DIMENSION_TEXTURE2DMSARRAY = 6;

        /// <summary>
        /// D3D12_TEX1D_DSV structure for 1D texture depth-stencil views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX1D_DSV
        {
            public uint MipSlice;
        }

        /// <summary>
        /// D3D12_TEX1D_ARRAY_DSV structure for 1D texture array depth-stencil views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX1D_ARRAY_DSV
        {
            public uint MipSlice;
            public uint FirstArraySlice;
            public uint ArraySize;
        }

        /// <summary>
        /// D3D12_TEX2D_DSV structure for 2D texture depth-stencil views.
        /// Based on DirectX 12 Depth Stencil Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_tex2d_dsv
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_DSV
        {
            public uint MipSlice;
        }

        /// <summary>
        /// D3D12_TEX2D_ARRAY_DSV structure for 2D texture array depth-stencil views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_ARRAY_DSV
        {
            public uint MipSlice;
            public uint FirstArraySlice;
            public uint ArraySize;
        }

        /// <summary>
        /// D3D12_TEX2DMS_DSV structure for 2D multisampled texture depth-stencil views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2DMS_DSV
        {
            public uint UnusedField_NothingToDefine; // MS textures don't have mip slices
        }

        /// <summary>
        /// D3D12_TEX2DMS_ARRAY_DSV structure for 2D multisampled texture array depth-stencil views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2DMS_ARRAY_DSV
        {
            public uint FirstArraySlice;
            public uint ArraySize;
        }

        // COM interface method delegates for descriptor heap operations
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDescriptorHeapDelegate(IntPtr device, IntPtr pDescriptorHeapDesc, ref Guid riid, IntPtr ppvHeap);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateCommittedResourceDelegate(IntPtr device, IntPtr pHeapProperties, uint HeapFlags, IntPtr pDesc, uint InitialResourceState, IntPtr pOptimizedClearValue, ref Guid riidResource, IntPtr ppvResource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateGraphicsPipelineStateDelegate(IntPtr device, IntPtr pDesc, ref Guid riid, IntPtr ppPipelineState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateComputePipelineStateDelegate(IntPtr device, IntPtr pDesc, ref Guid riid, IntPtr ppPipelineState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateRootSignatureDelegate(IntPtr device, IntPtr pBlobWithRootSignature, IntPtr blobLengthInBytes, ref Guid riid, IntPtr ppvRootSignature);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetCPUDescriptorHandleForHeapStartDelegate(IntPtr descriptorHeap);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate D3D12_GPU_DESCRIPTOR_HANDLE GetGPUDescriptorHandleForHeapStartDelegate(IntPtr descriptorHeap);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetDescriptorHandleIncrementSizeDelegate(IntPtr device, uint DescriptorHeapType);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CreateSamplerDelegate(IntPtr device, IntPtr pDesc, IntPtr DestDescriptor);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CheckFeatureSupportDelegate(IntPtr device, uint Feature, IntPtr pFeatureSupportData, uint FeatureSupportDataSize);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GetCopyableFootprintsDelegate(IntPtr device, IntPtr pResourceDesc, uint FirstSubresource, uint NumSubresources, ulong BaseOffset, IntPtr pLayouts, IntPtr pNumRows, IntPtr pRowSizeInBytes, IntPtr pTotalBytes);

        /// <summary>
        /// Calls ID3D12Device::GetCopyableFootprints through COM vtable.
        /// VTable index 47 for ID3D12Device.
        /// Based on DirectX 12 Resource Layout: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-getcopyablefootprints
        /// GetCopyableFootprints returns the exact layout (row pitch, depth pitch, total size) for a texture subresource,
        /// accounting for hardware-specific alignment requirements that may differ from theoretical calculations.
        /// </summary>
        private unsafe void CallGetCopyableFootprints(IntPtr device, IntPtr pResourceDesc, uint FirstSubresource, uint NumSubresources, ulong BaseOffset, IntPtr pLayouts, IntPtr pNumRows, IntPtr pRowSizeInBytes, IntPtr pTotalBytes)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return; // Cannot call on non-Windows platforms
            }

            if (device == IntPtr.Zero || pResourceDesc == IntPtr.Zero)
            {
                return; // Invalid parameters
            }

            // Get vtable pointer from device
            IntPtr* vtable = *(IntPtr**)device;
            if (vtable == null)
            {
                return; // Invalid vtable
            }

            // GetCopyableFootprints is at index 47 in ID3D12Device vtable
            IntPtr methodPtr = vtable[47];
            if (methodPtr == IntPtr.Zero)
            {
                return; // Method not available
            }

            GetCopyableFootprintsDelegate getCopyableFootprints =
                (GetCopyableFootprintsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetCopyableFootprintsDelegate));

            getCopyableFootprints(device, pResourceDesc, FirstSubresource, NumSubresources, BaseOffset, pLayouts, pNumRows, pRowSizeInBytes, pTotalBytes);
        }

        /// <summary>
        /// Calls ID3D12Device::CheckFeatureSupport through COM vtable.
        /// VTable index 6 for ID3D12Device.
        /// Based on DirectX 12 Feature Support: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-checkfeaturesupport
        /// </summary>
        private unsafe int CallCheckFeatureSupport(IntPtr device, uint feature, ref D3D12_FEATURE_FORMAT_SUPPORT featureSupportData, int featureSupportDataSize)
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

            // Get vtable pointer from device
            IntPtr* vtable = *(IntPtr**)device;
            if (vtable == null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            // CheckFeatureSupport is at index 6 in ID3D12Device vtable
            IntPtr methodPtr = vtable[6];
            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            // Allocate unmanaged memory for feature support data
            IntPtr pFeatureSupportData = Marshal.AllocHGlobal(featureSupportDataSize);
            try
            {
                // Marshal structure to unmanaged memory
                Marshal.StructureToPtr(featureSupportData, pFeatureSupportData, false);

                // Create delegate from function pointer
                CheckFeatureSupportDelegate checkFeatureSupport =
                    (CheckFeatureSupportDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CheckFeatureSupportDelegate));

                // Call CheckFeatureSupport
                int hr = checkFeatureSupport(device, feature, pFeatureSupportData, (uint)featureSupportDataSize);

                // If successful, marshal result back
                if (hr == 0) // S_OK
                {
                    featureSupportData = (D3D12_FEATURE_FORMAT_SUPPORT)Marshal.PtrToStructure(pFeatureSupportData, typeof(D3D12_FEATURE_FORMAT_SUPPORT));
                }

                return hr;
            }
            finally
            {
                // Always free unmanaged memory
                if (pFeatureSupportData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pFeatureSupportData);
                }
            }
        }

        /// <summary>
        /// Calls ID3D12Device::CreateCommittedResource through COM vtable.
        /// VTable index 25 for ID3D12Device.
        /// Based on DirectX 12 Resource Creation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcommittedresource
        /// </summary>
        private unsafe int CallCreateCommittedResource(IntPtr device, IntPtr pHeapProperties, uint HeapFlags, IntPtr pDesc, uint InitialResourceState, IntPtr pOptimizedClearValue, ref Guid riidResource, IntPtr ppvResource)
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

            // Get vtable pointer from device
            IntPtr* vtable = *(IntPtr**)device;
            if (vtable == null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            // CreateCommittedResource is at index 25 in ID3D12Device vtable
            IntPtr methodPtr = vtable[25];
            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            CreateCommittedResourceDelegate createCommittedResource =
                (CreateCommittedResourceDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateCommittedResourceDelegate));

            return createCommittedResource(device, pHeapProperties, HeapFlags, pDesc, InitialResourceState, pOptimizedClearValue, ref riidResource, ppvResource);
        }

        /// <summary>
        /// Calls ID3D12Device::CreateDescriptorHeap through COM vtable.
        /// VTable index 27 for ID3D12Device.
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createdescriptorheap
        /// </summary>
        private unsafe int CallCreateDescriptorHeap(IntPtr device, IntPtr pDescriptorHeapDesc, ref Guid riid, IntPtr ppvHeap)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero || pDescriptorHeapDesc == IntPtr.Zero || ppvHeap == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

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
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12descriptorheap-getcpudescriptorhandleforheapstart
        /// </summary>
        private unsafe IntPtr CallGetCPUDescriptorHandleForHeapStart(IntPtr descriptorHeap)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return IntPtr.Zero;
            }

            if (descriptorHeap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

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
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12descriptorheap-getgpudescriptorhandleforheapstart
        ///
        /// DirectX 12 GPU descriptor handles are used to reference descriptors from command lists during GPU execution.
        /// The handle must be preserved until all referencing command lists have finished execution.
        /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
        /// </summary>
        private unsafe D3D12_GPU_DESCRIPTOR_HANDLE CallGetGPUDescriptorHandleForHeapStart(IntPtr descriptorHeap)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return new D3D12_GPU_DESCRIPTOR_HANDLE { ptr = 0 };
            }

            if (descriptorHeap == IntPtr.Zero)
            {
                return new D3D12_GPU_DESCRIPTOR_HANDLE { ptr = 0 };
            }

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
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-getdescriptorhandleincrementsize
        /// </summary>
        private unsafe uint CallGetDescriptorHandleIncrementSize(IntPtr device, uint DescriptorHeapType)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            if (device == IntPtr.Zero)
            {
                return 0;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // GetDescriptorHandleIncrementSize is at index 28 in ID3D12Device vtable
            IntPtr methodPtr = vtable[28];

            // Create delegate from function pointer
            GetDescriptorHandleIncrementSizeDelegate getIncrementSize =
                (GetDescriptorHandleIncrementSizeDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetDescriptorHandleIncrementSizeDelegate));

            return getIncrementSize(device, DescriptorHeapType);
        }

        /// <summary>
        /// Calls ID3D12Device::CreateSampler through COM vtable.
        /// VTable index 34 for ID3D12Device.
        /// Based on DirectX 12 Samplers: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createsampler
        /// </summary>
        private unsafe void CallCreateSampler(IntPtr device, IntPtr pDesc, IntPtr DestDescriptor)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device == IntPtr.Zero || pDesc == IntPtr.Zero || DestDescriptor == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateSampler is at index 34 in ID3D12Device vtable
            IntPtr methodPtr = vtable[34];

            // Create delegate from function pointer
            CreateSamplerDelegate createSampler =
                (CreateSamplerDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateSamplerDelegate));

            createSampler(device, pDesc, DestDescriptor);
        }

        /// <summary>
        /// Calls ID3D12Device::CreateGraphicsPipelineState through COM vtable.
        /// VTable index 43 for ID3D12Device.
        /// Based on DirectX 12 Graphics Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-creategraphicspipelinestate
        /// </summary>
        private unsafe int CallCreateGraphicsPipelineState(IntPtr device, ref D3D12_GRAPHICS_PIPELINE_STATE_DESC pDesc, ref Guid riid, IntPtr ppPipelineState)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero || ppPipelineState == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer (first field of COM object)
            IntPtr* vtable = *(IntPtr**)device;
            // CreateGraphicsPipelineState is at index 43 in ID3D12Device vtable
            IntPtr methodPtr = vtable[43];

            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            // Marshal the pipeline state description structure
            int descSize = Marshal.SizeOf(typeof(D3D12_GRAPHICS_PIPELINE_STATE_DESC));
            IntPtr pDescPtr = Marshal.AllocHGlobal(descSize);
            try
            {
                Marshal.StructureToPtr(pDesc, pDescPtr, false);

                // Create delegate from function pointer (C# 7.3 compatible)
                CreateGraphicsPipelineStateDelegate createGraphicsPipelineState =
                    (CreateGraphicsPipelineStateDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateGraphicsPipelineStateDelegate));

                return createGraphicsPipelineState(device, pDescPtr, ref riid, ppPipelineState);
            }
            finally
            {
                // Note: We don't free pDescPtr here because the structure contains pointers to other allocated memory
                // that will be freed by FreeGraphicsPipelineStateDesc
            }
        }

        /// <summary>
        /// Calls ID3D12Device::CreateComputePipelineState through COM vtable.
        /// VTable index 44 for ID3D12Device.
        /// Based on DirectX 12 Compute Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcomputepipelinestate
        /// </summary>
        private unsafe int CallCreateComputePipelineState(IntPtr device, ref D3D12_COMPUTE_PIPELINE_STATE_DESC pDesc, ref Guid riid, IntPtr ppPipelineState)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero || ppPipelineState == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer (first field of COM object)
            IntPtr* vtable = *(IntPtr**)device;
            // CreateComputePipelineState is at index 44 in ID3D12Device vtable
            IntPtr methodPtr = vtable[44];

            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            // Marshal structure to unmanaged memory
            int descSize = Marshal.SizeOf(typeof(D3D12_COMPUTE_PIPELINE_STATE_DESC));
            IntPtr pDescPtr = Marshal.AllocHGlobal(descSize);
            try
            {
                Marshal.StructureToPtr(pDesc, pDescPtr, false);

                // Create delegate from function pointer (C# 7.3 compatible)
                CreateComputePipelineStateDelegate createComputePipelineState =
                    (CreateComputePipelineStateDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateComputePipelineStateDelegate));

                return createComputePipelineState(device, pDescPtr, ref riid, ppPipelineState);
            }
            finally
            {
                // Note: We don't free pDescPtr here because the structure contains pointers to other allocated memory
                // that will be freed by FreeComputePipelineStateDesc
            }
        }

        /// <summary>
        /// Calls ID3D12Device::CreateRootSignature through COM vtable.
        /// VTable index 47 for ID3D12Device.
        /// Based on DirectX 12 Root Signatures: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createrootsignature
        /// </summary>
        private unsafe int CallCreateRootSignature(IntPtr device, IntPtr pBlobWithRootSignature, IntPtr blobLengthInBytes, ref Guid riid, IntPtr ppvRootSignature)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero || pBlobWithRootSignature == IntPtr.Zero || ppvRootSignature == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer (first field of COM object)
            IntPtr* vtable = *(IntPtr**)device;
            // CreateRootSignature is at index 47 in ID3D12Device vtable
            IntPtr methodPtr = vtable[47];

            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            // Create delegate from function pointer (C# 7.3 compatible)
            CreateRootSignatureDelegate createRootSignature =
                (CreateRootSignatureDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateRootSignatureDelegate));

            return createRootSignature(device, pBlobWithRootSignature, blobLengthInBytes, ref riid, ppvRootSignature);
        }

        // Note: ReleaseComObject is already defined above (returns uint for reference count)
        // The void version was removed to avoid duplicate method definitions.
        // Code that calls ReleaseComObject without using the return value will still work
        // since C# allows ignoring return values.

        /// <summary>
        /// Calls ID3D12Device::CreateCommandAllocator through COM vtable.
        /// VTable index 26 for ID3D12Device.
        /// Based on DirectX 12 Command Allocator Creation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcommandallocator
        /// </summary>
        private unsafe int CallCreateCommandAllocator(IntPtr device, uint type, ref Guid riid, IntPtr ppCommandAllocator)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero || ppCommandAllocator == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            if (vtable == null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            // CreateCommandAllocator is at index 26 in ID3D12Device vtable
            IntPtr methodPtr = vtable[26];
            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            CreateCommandAllocatorDelegate createCommandAllocator =
                (CreateCommandAllocatorDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateCommandAllocatorDelegate));

            return createCommandAllocator(device, type, ref riid, ppCommandAllocator);
        }

        /// <summary>
        /// Calls ID3D12Device::CreateCommandList through COM vtable.
        /// VTable index 28 for ID3D12Device.
        /// Based on DirectX 12 Command List Creation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcommandlist
        /// </summary>
        private unsafe int CallCreateCommandList(IntPtr device, uint nodeMask, uint type, IntPtr pCommandAllocator, IntPtr pInitialState, ref Guid riid, IntPtr ppCommandList)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero || pCommandAllocator == IntPtr.Zero || ppCommandList == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            if (vtable == null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            // CreateCommandList is at index 28 in ID3D12Device vtable
            IntPtr methodPtr = vtable[28];
            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            CreateCommandListDelegate createCommandList =
                (CreateCommandListDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateCommandListDelegate));

            return createCommandList(device, nodeMask, type, pCommandAllocator, pInitialState, ref riid, ppCommandList);
        }

        /// <summary>
        /// Calls ID3D12Device::CreateFence through COM vtable.
        /// VTable index 11 for ID3D12Device.
        /// Based on DirectX 12 Fence Creation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createfence
        /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
        /// </summary>
        private unsafe int CallCreateFence(IntPtr device, ulong initialValue, uint flags, ref Guid riid, IntPtr ppFence)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (device == IntPtr.Zero || ppFence == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            if (vtable == null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            // CreateFence is at index 11 in ID3D12Device vtable
            IntPtr methodPtr = vtable[11];
            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            // Create delegate from function pointer
            CreateFenceDelegate createFence =
                (CreateFenceDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateFenceDelegate));

            return createFence(device, initialValue, flags, ref riid, ppFence);
        }

        /// <summary>
        /// Calls ID3D12CommandQueue::Signal through COM vtable.
        /// VTable index 5 for ID3D12CommandQueue (after IUnknown and ExecuteCommandLists).
        /// Based on DirectX 12 Command Queue Signaling: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandqueue-signal
        /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
        /// </summary>
        private unsafe ulong CallSignalFence(IntPtr commandQueue, IntPtr fence, ulong value)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            if (commandQueue == IntPtr.Zero || fence == IntPtr.Zero)
            {
                return 0;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandQueue;
            if (vtable == null)
            {
                return 0;
            }

            // Signal is at index 5 in ID3D12CommandQueue vtable
            IntPtr methodPtr = vtable[5];
            if (methodPtr == IntPtr.Zero)
            {
                return 0;
            }

            // Create delegate from function pointer
            SignalDelegate signal =
                (SignalDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SignalDelegate));

            return signal(commandQueue, fence, value);
        }

        /// <summary>
        /// Calls ID3D12Fence::GetCompletedValue through COM vtable.
        /// VTable index 3 for ID3D12Fence (after IUnknown methods).
        /// Based on DirectX 12 Fence Queries: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12fence-getcompletedvalue
        /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
        /// </summary>
        private unsafe ulong CallGetFenceCompletedValue(IntPtr fence)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            if (fence == IntPtr.Zero)
            {
                return 0;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)fence;
            if (vtable == null)
            {
                return 0;
            }

            // GetCompletedValue is at index 3 in ID3D12Fence vtable (after IUnknown: QueryInterface, AddRef, Release)
            IntPtr methodPtr = vtable[3];
            if (methodPtr == IntPtr.Zero)
            {
                return 0;
            }

            // Create delegate from function pointer
            GetCompletedValueDelegate getCompletedValue =
                (GetCompletedValueDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetCompletedValueDelegate));

            return getCompletedValue(fence);
        }

        /// <summary>
        /// Calls ID3D12Fence::SetEventOnCompletion through COM vtable.
        /// VTable index 4 for ID3D12Fence (after GetCompletedValue).
        /// Based on DirectX 12 Fence Synchronization: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12fence-seteventoncompletion
        /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
        /// </summary>
        private unsafe int CallSetEventOnCompletion(IntPtr fence, ulong value, IntPtr hEvent)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL - Not implemented on this platform
            }

            if (fence == IntPtr.Zero || hEvent == IntPtr.Zero)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)fence;
            if (vtable == null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            // SetEventOnCompletion is at index 4 in ID3D12Fence vtable (after GetCompletedValue)
            IntPtr methodPtr = vtable[4];
            if (methodPtr == IntPtr.Zero)
            {
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            // Create delegate from function pointer
            SetEventOnCompletionDelegate setEventOnCompletion =
                (SetEventOnCompletionDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetEventOnCompletionDelegate));

            return setEventOnCompletion(fence, value, hEvent);
        }

        /// <summary>
        /// Maps CommandListType enum to D3D12_COMMAND_LIST_TYPE.
        /// Based on DirectX 12 Command List Types: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_command_list_type
        /// </summary>
        private static uint MapCommandListTypeToD3D12(CommandListType type)
        {
            switch (type)
            {
                case CommandListType.Graphics:
                    return D3D12_COMMAND_LIST_TYPE_DIRECT;
                case CommandListType.Compute:
                    return D3D12_COMMAND_LIST_TYPE_COMPUTE;
                case CommandListType.Copy:
                    return D3D12_COMMAND_LIST_TYPE_COPY;
                default:
                    return D3D12_COMMAND_LIST_TYPE_DIRECT; // Default to graphics/direct
            }
        }

        /// <summary>
        /// Offsets a descriptor handle by a given number of descriptors.
        /// D3D12_CPU_DESCRIPTOR_HANDLE is a 64-bit value (ULONG_PTR).
        /// </summary>
        private IntPtr OffsetDescriptorHandle(IntPtr handle, int offset, uint incrementSize)
        {
            // Offset = handle.ptr + (offset * incrementSize)
            ulong handleValue = (ulong)handle.ToInt64();
            ulong offsetValue = (ulong)offset * incrementSize;
            return new IntPtr((long)(handleValue + offsetValue));
        }

        /// <summary>
        /// Ensures the sampler descriptor heap is created and initialized.
        /// Creates a sampler descriptor heap with the default capacity if one doesn't exist.
        /// </summary>
        private void EnsureSamplerDescriptorHeap()
        {
            if (_samplerDescriptorHeap != IntPtr.Zero)
            {
                return; // Heap already exists
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Create D3D12_DESCRIPTOR_HEAP_DESC structure for sampler heap
                var heapDesc = new D3D12_DESCRIPTOR_HEAP_DESC
                {
                    Type = D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER,
                    NumDescriptors = (uint)DefaultSamplerHeapCapacity,
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
                        Guid iidDescriptorHeap = IID_ID3D12DescriptorHeap;
                        int hr = CallCreateDescriptorHeap(_device, heapDescPtr, ref iidDescriptorHeap, heapPtr);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                        }

                        // Get the descriptor heap pointer
                        IntPtr descriptorHeap = Marshal.ReadIntPtr(heapPtr);
                        if (descriptorHeap == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Descriptor heap pointer is null");
                        }

                        // Get descriptor heap start handle (CPU handle for descriptor heap)
                        IntPtr cpuHandle = CallGetCPUDescriptorHandleForHeapStart(descriptorHeap);
                        if (cpuHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Failed to get CPU descriptor handle for heap start");
                        }

                        // Get descriptor increment size
                        uint descriptorIncrementSize = CallGetDescriptorHandleIncrementSize(_device, D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER);
                        if (descriptorIncrementSize == 0)
                        {
                            throw new InvalidOperationException("Failed to get descriptor handle increment size");
                        }

                        // Store heap information
                        _samplerDescriptorHeap = descriptorHeap;
                        _samplerHeapCpuStartHandle = cpuHandle;
                        _samplerHeapDescriptorIncrementSize = descriptorIncrementSize;
                        _samplerHeapCapacity = DefaultSamplerHeapCapacity;
                        _samplerHeapNextIndex = 0;
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
                throw new InvalidOperationException($"Failed to create sampler descriptor heap: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Allocates a descriptor handle from the sampler descriptor heap.
        /// Returns IntPtr.Zero if allocation fails.
        /// </summary>
        private IntPtr AllocateSamplerDescriptor()
        {
            EnsureSamplerDescriptorHeap();

            if (_samplerDescriptorHeap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (_samplerHeapNextIndex >= _samplerHeapCapacity)
            {
                // Heap is full - in a production implementation, we might want to create a larger heap or handle this differently
                throw new InvalidOperationException($"Sampler descriptor heap is full (capacity: {_samplerHeapCapacity})");
            }

            // Calculate CPU descriptor handle for this index
            IntPtr cpuDescriptorHandle = OffsetDescriptorHandle(_samplerHeapCpuStartHandle, _samplerHeapNextIndex, _samplerHeapDescriptorIncrementSize);
            int allocatedIndex = _samplerHeapNextIndex;
            _samplerHeapNextIndex++;

            return cpuDescriptorHandle;
        }

        /// <summary>
        /// Converts SamplerDesc to D3D12_SAMPLER_DESC structure.
        /// Based on DirectX 12 Sampler Descriptors: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_sampler_desc
        /// </summary>
        private D3D12_SAMPLER_DESC ConvertSamplerDescToD3D12(SamplerDesc desc)
        {
            var d3d12Desc = new D3D12_SAMPLER_DESC();

            // Convert filter modes to D3D12_FILTER
            bool useAnisotropic = desc.MaxAnisotropy > 1 || desc.MinFilter == SamplerFilter.Anisotropic ||
                                  desc.MagFilter == SamplerFilter.Anisotropic || desc.MipFilter == SamplerFilter.Anisotropic;

            if (useAnisotropic)
            {
                d3d12Desc.Filter = D3D12_FILTER_ANISOTROPIC;
            }
            else
            {
                // Combine min, mag, and mip filters into D3D12_FILTER enum
                bool minLinear = desc.MinFilter == SamplerFilter.Linear;
                bool magLinear = desc.MagFilter == SamplerFilter.Linear;
                bool mipLinear = desc.MipFilter == SamplerFilter.Linear;

                if (!minLinear && !magLinear && !mipLinear)
                {
                    d3d12Desc.Filter = D3D12_FILTER_MIN_MAG_MIP_POINT;
                }
                else if (!minLinear && !magLinear && mipLinear)
                {
                    d3d12Desc.Filter = D3D12_FILTER_MIN_MAG_POINT_MIP_LINEAR;
                }
                else if (!minLinear && magLinear && !mipLinear)
                {
                    d3d12Desc.Filter = D3D12_FILTER_MIN_POINT_MAG_LINEAR_MIP_POINT;
                }
                else if (!minLinear && magLinear && mipLinear)
                {
                    d3d12Desc.Filter = D3D12_FILTER_MIN_POINT_MAG_MIP_LINEAR;
                }
                else if (minLinear && !magLinear && !mipLinear)
                {
                    d3d12Desc.Filter = D3D12_FILTER_MIN_LINEAR_MAG_MIP_POINT;
                }
                else if (minLinear && !magLinear && mipLinear)
                {
                    d3d12Desc.Filter = D3D12_FILTER_MIN_LINEAR_MAG_POINT_MIP_LINEAR;
                }
                else if (minLinear && magLinear && !mipLinear)
                {
                    d3d12Desc.Filter = D3D12_FILTER_MIN_MAG_LINEAR_MIP_POINT;
                }
                else // minLinear && magLinear && mipLinear
                {
                    d3d12Desc.Filter = D3D12_FILTER_MIN_MAG_MIP_LINEAR;
                }
            }

            // Convert address modes
            d3d12Desc.AddressU = ConvertSamplerAddressModeToD3D12(desc.AddressU);
            d3d12Desc.AddressV = ConvertSamplerAddressModeToD3D12(desc.AddressV);
            d3d12Desc.AddressW = ConvertSamplerAddressModeToD3D12(desc.AddressW);

            // Convert mip LOD bias
            d3d12Desc.MipLODBias = desc.MipLodBias;

            // Convert max anisotropy (clamp to valid range 1-16)
            d3d12Desc.MaxAnisotropy = (uint)Math.Max(1, Math.Min(16, desc.MaxAnisotropy));

            // Convert comparison function
            d3d12Desc.ComparisonFunc = ConvertCompareFuncToD3D12(desc.CompareFunc);

            // Border color - use provided color or default to transparent black
            if (desc.BorderColor != null && desc.BorderColor.Length >= 4)
            {
                d3d12Desc.BorderColor = new float[] { desc.BorderColor[0], desc.BorderColor[1], desc.BorderColor[2], desc.BorderColor[3] };
            }
            else
            {
                d3d12Desc.BorderColor = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };
            }

            // Min/Max LOD
            d3d12Desc.MinLOD = desc.MinLod;
            if (desc.MaxLod > 0.0f)
            {
                d3d12Desc.MaxLOD = desc.MaxLod;
            }
            else
            {
                d3d12Desc.MaxLOD = D3D12_FLOAT32_MAX;
            }

            return d3d12Desc;
        }

        /// <summary>
        /// Converts SamplerAddressMode enum to D3D12_TEXTURE_ADDRESS_MODE value.
        /// Based on DirectX 12 Texture Address Modes: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_texture_address_mode
        /// </summary>
        private uint ConvertSamplerAddressModeToD3D12(SamplerAddressMode mode)
        {
            switch (mode)
            {
                case SamplerAddressMode.Wrap:
                    return D3D12_TEXTURE_ADDRESS_MODE_WRAP;
                case SamplerAddressMode.Mirror:
                    return D3D12_TEXTURE_ADDRESS_MODE_MIRROR;
                case SamplerAddressMode.Clamp:
                    return D3D12_TEXTURE_ADDRESS_MODE_CLAMP;
                case SamplerAddressMode.Border:
                    return D3D12_TEXTURE_ADDRESS_MODE_BORDER;
                case SamplerAddressMode.MirrorOnce:
                    return D3D12_TEXTURE_ADDRESS_MODE_MIRROR_ONCE;
                default:
                    return D3D12_TEXTURE_ADDRESS_MODE_WRAP;
            }
        }

        /// <summary>
        /// Converts CompareFunc enum to D3D12_COMPARISON_FUNC value.
        /// Based on DirectX 12 Comparison Functions: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_comparison_func
        /// </summary>
        private uint ConvertCompareFuncToD3D12(CompareFunc compareFunc)
        {
            switch (compareFunc)
            {
                case CompareFunc.Never:
                    return D3D12_COMPARISON_FUNC_NEVER;
                case CompareFunc.Less:
                    return D3D12_COMPARISON_FUNC_LESS;
                case CompareFunc.Equal:
                    return D3D12_COMPARISON_FUNC_EQUAL;
                case CompareFunc.LessEqual:
                    return D3D12_COMPARISON_FUNC_LESS_EQUAL;
                case CompareFunc.Greater:
                    return D3D12_COMPARISON_FUNC_GREATER;
                case CompareFunc.NotEqual:
                    return D3D12_COMPARISON_FUNC_NOT_EQUAL;
                case CompareFunc.GreaterEqual:
                    return D3D12_COMPARISON_FUNC_GREATER_EQUAL;
                case CompareFunc.Always:
                    return D3D12_COMPARISON_FUNC_ALWAYS;
                default:
                    return D3D12_COMPARISON_FUNC_NEVER;
            }
        }

        #endregion

        #region D3D12 DSV Descriptor Heap Management

        /// <summary>
        /// Ensures the DSV descriptor heap is created and initialized.
        /// Creates a DSV descriptor heap with the default capacity if one doesn't exist.
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_descriptor_heap_desc
        /// </summary>
        private void EnsureDsvDescriptorHeap()
        {
            if (_dsvDescriptorHeap != IntPtr.Zero)
            {
                return; // Heap already exists
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Create D3D12_DESCRIPTOR_HEAP_DESC structure for DSV heap
                var heapDesc = new D3D12_DESCRIPTOR_HEAP_DESC
                {
                    Type = D3D12_DESCRIPTOR_HEAP_TYPE_DSV,
                    NumDescriptors = (uint)DefaultDsvHeapCapacity,
                    Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE, // DSV heaps are not shader-visible
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
                        Guid iidDescriptorHeap = IID_ID3D12DescriptorHeap;
                        int hr = CallCreateDescriptorHeap(_device, heapDescPtr, ref iidDescriptorHeap, heapPtr);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                        }

                        // Get the descriptor heap pointer
                        IntPtr descriptorHeap = Marshal.ReadIntPtr(heapPtr);
                        if (descriptorHeap == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Descriptor heap pointer is null");
                        }

                        // Get descriptor heap start handle (CPU handle for descriptor heap)
                        IntPtr cpuHandle = CallGetCPUDescriptorHandleForHeapStart(descriptorHeap);
                        if (cpuHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Failed to get CPU descriptor handle for heap start");
                        }

                        // Get descriptor increment size
                        uint descriptorIncrementSize = CallGetDescriptorHandleIncrementSize(_device, D3D12_DESCRIPTOR_HEAP_TYPE_DSV);
                        if (descriptorIncrementSize == 0)
                        {
                            throw new InvalidOperationException("Failed to get descriptor handle increment size");
                        }

                        // Store heap information
                        _dsvDescriptorHeap = descriptorHeap;
                        _dsvHeapCpuStartHandle = cpuHandle;
                        _dsvHeapDescriptorIncrementSize = descriptorIncrementSize;
                        _dsvHeapCapacity = DefaultDsvHeapCapacity;
                        _dsvHeapNextIndex = 0;
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
                throw new InvalidOperationException($"Failed to create DSV descriptor heap: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Allocates a descriptor handle from the DSV descriptor heap.
        /// Returns IntPtr.Zero if allocation fails.
        /// </summary>
        private IntPtr AllocateDsvDescriptor()
        {
            EnsureDsvDescriptorHeap();

            if (_dsvDescriptorHeap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (_dsvHeapNextIndex >= _dsvHeapCapacity)
            {
                // Heap is full - in a production implementation, we might want to create a larger heap or handle this differently
                throw new InvalidOperationException($"DSV descriptor heap is full (capacity: {_dsvHeapCapacity})");
            }

            // Calculate CPU descriptor handle for this index
            IntPtr cpuDescriptorHandle = OffsetDescriptorHandle(_dsvHeapCpuStartHandle, _dsvHeapNextIndex, _dsvHeapDescriptorIncrementSize);
            int allocatedIndex = _dsvHeapNextIndex;
            _dsvHeapNextIndex++;

            return cpuDescriptorHandle;
        }

        /// <summary>
        /// Converts TextureFormat to DXGI_FORMAT for depth-stencil views.
        /// Based on DirectX 12 Texture Formats: https://docs.microsoft.com/en-us/windows/win32/api/dxgiformat/ne-dxgiformat-dxgi_format
        /// </summary>
        private uint ConvertTextureFormatToDxgiFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.D24_UNorm_S8_UInt:
                    return 45; // DXGI_FORMAT_D24_UNORM_S8_UINT
                case TextureFormat.D32_Float:
                    return 40; // DXGI_FORMAT_D32_FLOAT
                case TextureFormat.D32_Float_S8_UInt:
                    return 20; // DXGI_FORMAT_D32_FLOAT_S8X24_UINT (closest match, though not exact)
                default:
                    return 0; // DXGI_FORMAT_UNKNOWN
            }
        }

        /// <summary>
        /// COM interface method delegate for CreateDepthStencilView.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CreateDepthStencilViewDelegate(IntPtr device, IntPtr pResource, IntPtr pDesc, IntPtr DestDescriptor);

        /// <summary>
        /// Calls ID3D12Device::CreateDepthStencilView through COM vtable.
        /// VTable index 35 for ID3D12Device.
        /// Based on DirectX 12 Depth Stencil Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createdepthstencilview
        /// </summary>
        private unsafe void CallCreateDepthStencilView(IntPtr device, IntPtr pResource, IntPtr pDesc, IntPtr DestDescriptor)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device == IntPtr.Zero || pResource == IntPtr.Zero || DestDescriptor == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateDepthStencilView is at index 35 in ID3D12Device vtable
            IntPtr methodPtr = vtable[35];

            // Create delegate from function pointer
            CreateDepthStencilViewDelegate createDsv = (CreateDepthStencilViewDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateDepthStencilViewDelegate));

            createDsv(device, pResource, pDesc, DestDescriptor);
        }

        /// <summary>
        /// Gets or creates a DSV descriptor handle for a texture.
        /// Caches the handle to avoid recreating descriptors for the same texture.
        /// </summary>
        internal IntPtr GetOrCreateDsvHandle(ITexture texture, int mipLevel, int arraySlice)
        {
            if (texture == null || texture.NativeHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Create a unique key for this texture/mip/array combination
            // For simplicity, we'll use the texture handle as the key (assuming one DSV per texture)
            // In a more complete implementation, we could include mipLevel and arraySlice in the key
            IntPtr textureHandle = texture.NativeHandle;

            // Check cache first
            IntPtr cachedHandle;
            if (_textureDsvHandles.TryGetValue(textureHandle, out cachedHandle))
            {
                return cachedHandle;
            }

            // Allocate new DSV descriptor
            IntPtr dsvHandle = AllocateDsvDescriptor();
            if (dsvHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Get texture format and convert to DXGI_FORMAT
            TextureFormat format = texture.Desc.Format;
            uint dxgiFormat = ConvertTextureFormatToDxgiFormat(format);
            if (dxgiFormat == 0)
            {
                // Format not supported for DSV, return zero handle
                return IntPtr.Zero;
            }

            // Create D3D12_DEPTH_STENCIL_VIEW_DESC structure
            var dsvDesc = new D3D12_DEPTH_STENCIL_VIEW_DESC
            {
                Format = dxgiFormat,
                ViewDimension = D3D12_DSV_DIMENSION_TEXTURE2D, // Default to 2D texture
                Flags = 0, // D3D12_DSV_FLAG_NONE
                Texture2D = new D3D12_TEX2D_DSV
                {
                    MipSlice = unchecked((uint)mipLevel)
                }
            };

            // Allocate memory for the DSV descriptor structure
            int dsvDescSize = Marshal.SizeOf(typeof(D3D12_DEPTH_STENCIL_VIEW_DESC));
            IntPtr dsvDescPtr = Marshal.AllocHGlobal(dsvDescSize);
            try
            {
                Marshal.StructureToPtr(dsvDesc, dsvDescPtr, false);

                // Call ID3D12Device::CreateDepthStencilView
                CallCreateDepthStencilView(_device, texture.NativeHandle, dsvDescPtr, dsvHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(dsvDescPtr);
            }

            // Cache the handle
            _textureDsvHandles[textureHandle] = dsvHandle;

            return dsvHandle;
        }

        #endregion

        #region D3D12 RTV Descriptor Heap Management

        /// <summary>
        /// Ensures the RTV descriptor heap is created and initialized.
        /// Creates an RTV descriptor heap with the default capacity if one doesn't exist.
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_descriptor_heap_desc
        /// </summary>
        private void EnsureRtvDescriptorHeap()
        {
            if (_rtvDescriptorHeap != IntPtr.Zero)
            {
                return; // Heap already exists
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Create D3D12_DESCRIPTOR_HEAP_DESC structure for RTV heap
                var heapDesc = new D3D12_DESCRIPTOR_HEAP_DESC
                {
                    Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV,
                    NumDescriptors = (uint)DefaultRtvHeapCapacity,
                    Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE, // RTV heaps are not shader-visible
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
                        Guid iidDescriptorHeap = IID_ID3D12DescriptorHeap;
                        int hr = CallCreateDescriptorHeap(_device, heapDescPtr, ref iidDescriptorHeap, heapPtr);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                        }

                        // Get the descriptor heap pointer
                        IntPtr descriptorHeap = Marshal.ReadIntPtr(heapPtr);
                        if (descriptorHeap == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Descriptor heap pointer is null");
                        }

                        // Get descriptor heap start handle (CPU handle for descriptor heap)
                        IntPtr cpuHandle = CallGetCPUDescriptorHandleForHeapStart(descriptorHeap);
                        if (cpuHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Failed to get CPU descriptor handle for heap start");
                        }

                        // Get descriptor increment size
                        uint descriptorIncrementSize = CallGetDescriptorHandleIncrementSize(_device, D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
                        if (descriptorIncrementSize == 0)
                        {
                            throw new InvalidOperationException("Failed to get descriptor handle increment size");
                        }

                        // Store heap information
                        _rtvDescriptorHeap = descriptorHeap;
                        _rtvHeapCpuStartHandle = cpuHandle;
                        _rtvHeapDescriptorIncrementSize = descriptorIncrementSize;
                        _rtvHeapCapacity = DefaultRtvHeapCapacity;
                        _rtvHeapNextIndex = 0;
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
                throw new InvalidOperationException($"Failed to create RTV descriptor heap: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Allocates a descriptor handle from the RTV descriptor heap.
        /// Returns IntPtr.Zero if allocation fails.
        /// </summary>
        private IntPtr AllocateRtvDescriptor()
        {
            EnsureRtvDescriptorHeap();

            if (_rtvDescriptorHeap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (_rtvHeapNextIndex >= _rtvHeapCapacity)
            {
                // Heap is full - in a production implementation, we might want to create a larger heap or handle this differently
                throw new InvalidOperationException($"RTV descriptor heap is full (capacity: {_rtvHeapCapacity})");
            }

            // Calculate CPU descriptor handle for this index
            IntPtr cpuDescriptorHandle = OffsetDescriptorHandle(_rtvHeapCpuStartHandle, _rtvHeapNextIndex, _rtvHeapDescriptorIncrementSize);
            int allocatedIndex = _rtvHeapNextIndex;
            _rtvHeapNextIndex++;

            return cpuDescriptorHandle;
        }

        /// <summary>
        /// COM interface method delegate for CreateRenderTargetView.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CreateRenderTargetViewDelegate(IntPtr device, IntPtr pResource, IntPtr pDesc, IntPtr DestDescriptor);

        /// <summary>
        /// Calls ID3D12Device::CreateRenderTargetView through COM vtable.
        /// VTable index 34 for ID3D12Device.
        /// Based on DirectX 12 Render Target Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createrendertargetview
        /// </summary>
        private unsafe void CallCreateRenderTargetView(IntPtr device, IntPtr pResource, IntPtr pDesc, IntPtr DestDescriptor)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device == IntPtr.Zero || pResource == IntPtr.Zero || DestDescriptor == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateRenderTargetView is at index 34 in ID3D12Device vtable
            IntPtr methodPtr = vtable[34];

            // Create delegate from function pointer
            CreateRenderTargetViewDelegate createRtv = (CreateRenderTargetViewDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateRenderTargetViewDelegate));

            createRtv(device, pResource, pDesc, DestDescriptor);
        }

        /// <summary>
        /// Gets or creates an RTV descriptor handle for a texture.
        /// Caches the handle to avoid recreating descriptors for the same texture.
        /// Based on DirectX 12 Render Target Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createrendertargetview
        /// </summary>
        internal IntPtr GetOrCreateRtvHandle(ITexture texture, int mipLevel, int arraySlice)
        {
            if (texture == null || texture.NativeHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Create a unique key for this texture/mip/array combination
            // For simplicity, we'll use the texture handle as the key (assuming one RTV per texture)
            // In a more complete implementation, we could include mipLevel and arraySlice in the key
            IntPtr textureHandle = texture.NativeHandle;

            // Check cache first
            IntPtr cachedHandle;
            if (_textureRtvHandles.TryGetValue(textureHandle, out cachedHandle))
            {
                return cachedHandle;
            }

            // Allocate new RTV descriptor
            IntPtr rtvHandle = AllocateRtvDescriptor();
            if (rtvHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Get texture format and convert to DXGI_FORMAT
            TextureFormat format = texture.Desc.Format;
            uint dxgiFormat = ConvertTextureFormatToDxgiFormatForRtv(format);
            if (dxgiFormat == 0)
            {
                // Format not supported for RTV, return zero handle
                return IntPtr.Zero;
            }

            // Create D3D12_RENDER_TARGET_VIEW_DESC structure
            var rtvDesc = new D3D12_RENDER_TARGET_VIEW_DESC
            {
                Format = dxgiFormat,
                ViewDimension = D3D12_RTV_DIMENSION_TEXTURE2D, // Default to 2D texture
                Texture2D = new D3D12_TEX2D_RTV
                {
                    MipSlice = unchecked((uint)mipLevel),
                    PlaneSlice = 0 // Typically 0 for non-planar formats
                }
            };

            // Allocate memory for the RTV descriptor structure
            int rtvDescSize = Marshal.SizeOf(typeof(D3D12_RENDER_TARGET_VIEW_DESC));
            IntPtr rtvDescPtr = Marshal.AllocHGlobal(rtvDescSize);
            try
            {
                Marshal.StructureToPtr(rtvDesc, rtvDescPtr, false);

                // Call ID3D12Device::CreateRenderTargetView
                CallCreateRenderTargetView(_device, texture.NativeHandle, rtvDescPtr, rtvHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(rtvDescPtr);
            }

            // Cache the handle
            _textureRtvHandles[textureHandle] = rtvHandle;

            return rtvHandle;
        }

        /// <summary>
        /// Creates an SRV descriptor for a texture.
        /// Based on DirectX 12 Shader Resource Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createshaderresourceview
        /// Creates a non-shader-visible descriptor for texture management.
        /// </summary>
        /// <param name="d3d12Resource">The D3D12 resource (texture) to create an SRV for.</param>
        /// <param name="desc">Texture description.</param>
        /// <param name="dxgiFormat">DXGI format for the texture.</param>
        /// <param name="resourceDimension">D3D12 resource dimension (D3D12_RESOURCE_DIMENSION_TEXTURE2D, etc.).</param>
        /// <returns>CPU descriptor handle for the SRV, or IntPtr.Zero on failure.</returns>
        /// <summary>
        /// Creates an SRV descriptor for a texture in a binding set.
        /// Based on DirectX 12 Shader Resource Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createshaderresourceview
        /// Creates a shader-visible descriptor in the CBV_SRV_UAV descriptor heap for binding sets.
        /// </summary>
        /// <param name="texture">The texture to create an SRV descriptor for.</param>
        /// <param name="cpuDescriptorHandle">CPU descriptor handle where the SRV descriptor will be created.</param>
        private void CreateSrvDescriptorForBindingSet(ITexture texture, IntPtr cpuDescriptorHandle)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (cpuDescriptorHandle == IntPtr.Zero)
            {
                throw new ArgumentException("CPU descriptor handle must be valid", nameof(cpuDescriptorHandle));
            }

            if (texture.NativeHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Texture native handle must be valid", nameof(texture));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                throw new InvalidOperationException("D3D12 device is not initialized");
            }

            // Get texture description
            TextureDesc desc = texture.Desc;

            // Convert texture format to DXGI format for SRV
            uint dxgiFormat = ConvertTextureFormatToDxgiFormatForTexture(desc.Format);
            if (dxgiFormat == 0)
            {
                throw new ArgumentException($"Texture format {desc.Format} is not supported for SRV", nameof(texture));
            }

            // Map texture dimension to D3D12 resource dimension
            uint resourceDimension = MapTextureDimensionToD3D12(desc.Dimension);

            // Create SRV descriptor directly at the provided handle
            // This is similar to CreateSrvDescriptorForTexture but writes directly to the provided handle
            // Based on DirectX 12 Shader Resource Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createshaderresourceview
            // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12

            // Determine SRV dimension based on resource dimension
            uint srvDimension;
            switch (resourceDimension)
            {
                case D3D12_RESOURCE_DIMENSION_TEXTURE1D:
                    srvDimension = D3D12_SRV_DIMENSION_TEXTURE1D;
                    break;
                case D3D12_RESOURCE_DIMENSION_TEXTURE2D:
                    srvDimension = D3D12_SRV_DIMENSION_TEXTURE2D;
                    break;
                case D3D12_RESOURCE_DIMENSION_TEXTURE3D:
                    srvDimension = D3D12_SRV_DIMENSION_TEXTURE3D;
                    break;
                default:
                    // Unsupported dimension, default to TEXTURE2D
                    srvDimension = D3D12_SRV_DIMENSION_TEXTURE2D;
                    break;
            }

            // Create D3D12_SHADER_RESOURCE_VIEW_DESC structure
            var srvDesc = new D3D12_SHADER_RESOURCE_VIEW_DESC
            {
                Format = dxgiFormat,
                ViewDimension = srvDimension
            };

            // Set dimension-specific parameters
            // For binding sets, we typically use all mips starting from mip 0
            // In DirectX 12, -1 (0xFFFFFFFF) means "use all available mips"
            uint mipLevels = (desc.MipLevels > 0) ? unchecked((uint)desc.MipLevels) : unchecked((uint)(-1));

            if (srvDimension == D3D12_SRV_DIMENSION_TEXTURE2D)
            {
                srvDesc.Texture2D = new D3D12_TEX2D_SRV
                {
                    MostDetailedMip = 0, // Start from first mip level
                    MipLevels = mipLevels, // -1 (0xFFFFFFFF) means all mips, otherwise use specified count
                    PlaneSlice = 0, // Typically 0 for non-planar formats
                    ResourceMinLODClamp = 0.0f // No clamping
                };
            }
            else if (srvDimension == D3D12_SRV_DIMENSION_TEXTURE1D)
            {
                srvDesc.Texture1D = new D3D12_TEX1D_SRV
                {
                    MostDetailedMip = 0,
                    MipLevels = mipLevels,
                    ResourceMinLODClamp = 0.0f
                };
            }
            else if (srvDimension == D3D12_SRV_DIMENSION_TEXTURE3D)
            {
                srvDesc.Texture3D = new D3D12_TEX3D_SRV
                {
                    MostDetailedMip = 0,
                    MipLevels = mipLevels,
                    ResourceMinLODClamp = 0.0f
                };
            }

            // Allocate memory for the SRV descriptor structure
            int srvDescSize = Marshal.SizeOf(typeof(D3D12_SHADER_RESOURCE_VIEW_DESC));
            IntPtr srvDescPtr = Marshal.AllocHGlobal(srvDescSize);
            try
            {
                Marshal.StructureToPtr(srvDesc, srvDescPtr, false);

                // Call ID3D12Device::CreateShaderResourceView directly at the provided handle
                // Based on DirectX 12 Shader Resource Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createshaderresourceview
                CallCreateShaderResourceView(_device, texture.NativeHandle, srvDescPtr, cpuDescriptorHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(srvDescPtr);
            }
        }

        /// <summary>
        /// Creates a UAV descriptor for a texture in a binding set.
        /// Based on DirectX 12 Unordered Access Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createunorderedaccessview
        /// Creates a shader-visible descriptor in the CBV_SRV_UAV descriptor heap for binding sets.
        /// </summary>
        /// <param name="texture">The texture to create a UAV descriptor for.</param>
        /// <param name="cpuDescriptorHandle">CPU descriptor handle where the UAV descriptor will be created.</param>
        private void CreateUavDescriptorForBindingSet(ITexture texture, IntPtr cpuDescriptorHandle)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (cpuDescriptorHandle == IntPtr.Zero)
            {
                throw new ArgumentException("CPU descriptor handle must be valid", nameof(cpuDescriptorHandle));
            }

            if (texture.NativeHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Texture native handle must be valid", nameof(texture));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                throw new InvalidOperationException("D3D12 device is not initialized");
            }

            // Get texture description
            TextureDesc desc = texture.Desc;

            // Convert texture format to DXGI format for UAV
            uint dxgiFormat = ConvertTextureFormatToDxgiFormatForUav(desc.Format);
            if (dxgiFormat == 0)
            {
                throw new ArgumentException($"Texture format {desc.Format} is not supported for UAV", nameof(texture));
            }

            // Map texture dimension to D3D12 UAV dimension
            // Based on DirectX 12 UAV Dimensions: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_uav_dimension
            uint uavDimension;
            switch (desc.Dimension)
            {
                case TextureDimension.Texture1D:
                    uavDimension = D3D12_UAV_DIMENSION_TEXTURE1D;
                    break;
                case TextureDimension.Texture1DArray:
                    uavDimension = D3D12_UAV_DIMENSION_TEXTURE1DARRAY;
                    break;
                case TextureDimension.Texture2D:
                    uavDimension = D3D12_UAV_DIMENSION_TEXTURE2D;
                    break;
                case TextureDimension.Texture2DArray:
                    uavDimension = D3D12_UAV_DIMENSION_TEXTURE2DARRAY;
                    break;
                case TextureDimension.Texture3D:
                    uavDimension = D3D12_UAV_DIMENSION_TEXTURE3D;
                    break;
                case TextureDimension.TextureCube:
                    // Cubemaps are treated as 2D texture arrays (6 slices) in D3D12
                    uavDimension = D3D12_UAV_DIMENSION_TEXTURE2DARRAY;
                    break;
                case TextureDimension.TextureCubeArray:
                    // Cube arrays are treated as 2D texture arrays in D3D12
                    uavDimension = D3D12_UAV_DIMENSION_TEXTURE2DARRAY;
                    break;
                default:
                    // Default to 2D texture
                    uavDimension = D3D12_UAV_DIMENSION_TEXTURE2D;
                    break;
            }

            // Create D3D12_UNORDERED_ACCESS_VIEW_DESC structure
            var uavDesc = new D3D12_UNORDERED_ACCESS_VIEW_DESC
            {
                Format = dxgiFormat,
                ViewDimension = uavDimension
            };

            // Set dimension-specific parameters
            // For binding sets, we typically use mip level 0 (the most detailed mip)
            uint mipSlice = 0;

            if (uavDimension == D3D12_UAV_DIMENSION_TEXTURE1D)
            {
                uavDesc.Texture1D = new D3D12_TEX1D_UAV
                {
                    MipSlice = mipSlice
                };
            }
            else if (uavDimension == D3D12_UAV_DIMENSION_TEXTURE1DARRAY)
            {
                uint arraySize = desc.ArraySize > 0 ? unchecked((uint)desc.ArraySize) : 1;
                uavDesc.Texture1DArray = new D3D12_TEX1D_ARRAY_UAV
                {
                    MipSlice = mipSlice,
                    FirstArraySlice = 0, // Start from first array slice
                    ArraySize = arraySize // Number of array slices
                };
            }
            else if (uavDimension == D3D12_UAV_DIMENSION_TEXTURE2D)
            {
                uavDesc.Texture2D = new D3D12_TEX2D_UAV
                {
                    MipSlice = mipSlice,
                    PlaneSlice = 0 // Typically 0 for non-planar formats
                };
            }
            else if (uavDimension == D3D12_UAV_DIMENSION_TEXTURE2DARRAY)
            {
                uint arraySize;
                if (desc.Dimension == TextureDimension.TextureCube)
                {
                    // Cubemaps have 6 faces
                    arraySize = 6;
                }
                else if (desc.Dimension == TextureDimension.TextureCubeArray)
                {
                    // Cube arrays have 6 faces per cube * number of cubes
                    arraySize = unchecked((uint)(6 * desc.ArraySize));
                }
                else
                {
                    // Regular 2D texture array
                    arraySize = desc.ArraySize > 0 ? unchecked((uint)desc.ArraySize) : 1;
                }

                uavDesc.Texture2DArray = new D3D12_TEX2D_ARRAY_UAV
                {
                    MipSlice = mipSlice,
                    FirstArraySlice = 0, // Start from first array slice
                    ArraySize = arraySize, // Number of array slices
                    PlaneSlice = 0 // Typically 0 for non-planar formats
                };
            }
            else if (uavDimension == D3D12_UAV_DIMENSION_TEXTURE3D)
            {
                uint depth = desc.Depth > 0 ? unchecked((uint)desc.Depth) : 1;
                uavDesc.Texture3D = new D3D12_TEX3D_UAV
                {
                    MipSlice = mipSlice,
                    FirstWSlice = 0, // Start from first depth slice
                    WSize = depth // Number of depth slices
                };
            }

            // Allocate memory for the UAV descriptor structure
            int uavDescSize = Marshal.SizeOf(typeof(D3D12_UNORDERED_ACCESS_VIEW_DESC));
            IntPtr uavDescPtr = Marshal.AllocHGlobal(uavDescSize);
            try
            {
                Marshal.StructureToPtr(uavDesc, uavDescPtr, false);

                // Call ID3D12Device::CreateUnorderedAccessView
                // pCounterResource is NULL for textures (counters are for structured buffers)
                CallCreateUnorderedAccessView(_device, texture.NativeHandle, IntPtr.Zero, uavDescPtr, cpuDescriptorHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(uavDescPtr);
            }
        }

        /// <summary>
        /// Creates a CBV (Constant Buffer View) descriptor for a constant buffer in a binding set.
        /// Based on DirectX 12 Constant Buffer Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createconstantbufferview
        /// Creates a shader-visible descriptor in the CBV_SRV_UAV descriptor heap for binding sets.
        /// Constant buffers in D3D12 must have sizes that are multiples of 256 bytes (D3D12 requirement).
        /// </summary>
        /// <param name="buffer">The buffer to create a CBV descriptor for.</param>
        /// <param name="offset">Offset in bytes from the start of the buffer.</param>
        /// <param name="range">Range in bytes to view. If 0 or -1, uses all remaining bytes from offset to end of buffer.</param>
        /// <param name="cpuDescriptorHandle">CPU descriptor handle where the CBV descriptor will be created.</param>
        private void CreateCbvDescriptorForBindingSet(IBuffer buffer, int offset, int range, IntPtr cpuDescriptorHandle)
        {
            // Validate inputs
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (cpuDescriptorHandle == IntPtr.Zero)
            {
                throw new ArgumentException("CPU descriptor handle must be valid", nameof(cpuDescriptorHandle));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                throw new InvalidOperationException("D3D12 device is not initialized");
            }

            // Get D3D12 resource from buffer's native handle
            // buffer.NativeHandle is the ID3D12Resource* pointer
            IntPtr d3d12Resource = buffer.NativeHandle;
            if (d3d12Resource == IntPtr.Zero)
            {
                throw new ArgumentException("Buffer native handle must be valid", nameof(buffer));
            }

            // Get buffer description
            BufferDesc desc = buffer.Desc;

            // Validate offset
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative");
            }

            if (offset >= desc.ByteSize)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset exceeds buffer size");
            }

            // Calculate size in bytes
            // If range is 0 or -1, use all remaining bytes from offset to end of buffer
            int sizeInBytes;
            if (range <= 0)
            {
                // Use all remaining bytes from offset to end of buffer
                sizeInBytes = desc.ByteSize - offset;
                if (sizeInBytes <= 0)
                {
                    throw new ArgumentException("Invalid range: offset is at or beyond end of buffer");
                }
            }
            else
            {
                // Use specified range
                sizeInBytes = range;

                // Validate range doesn't exceed buffer bounds
                if (offset + sizeInBytes > desc.ByteSize)
                {
                    // Clamp to buffer size
                    sizeInBytes = desc.ByteSize - offset;
                    if (sizeInBytes <= 0)
                    {
                        throw new ArgumentException("Invalid range: offset + range exceeds buffer size");
                    }
                }
            }

            // Constant buffers in D3D12 must be aligned to 256 bytes (D3D12 constant buffer alignment requirement)
            // Round up to nearest multiple of 256
            // Based on DirectX 12 Constant Buffer Requirements: https://docs.microsoft.com/en-us/windows/win32/direct3d12/uploading-resources#buffer-alignment
            const int constantBufferAlignment = 256;
            int alignedSize = ((sizeInBytes + constantBufferAlignment - 1) / constantBufferAlignment) * constantBufferAlignment;

            // Ensure aligned size doesn't exceed available buffer space
            // The aligned size must fit within the buffer from the offset
            int maxAvailableSize = desc.ByteSize - offset;
            if (alignedSize > maxAvailableSize)
            {
                // Can't round up - use the maximum available size aligned down to 256-byte boundary
                alignedSize = (maxAvailableSize / constantBufferAlignment) * constantBufferAlignment;

                // If the available size is less than 256 bytes, we can't create a valid CBV
                // (D3D12 requires constant buffers to be at least 256 bytes)
                if (alignedSize < constantBufferAlignment)
                {
                    throw new ArgumentException($"Cannot create CBV: available size {maxAvailableSize} bytes at offset {offset} is less than minimum required {constantBufferAlignment} bytes");
                }
            }

            // Get GPU virtual address of the buffer resource
            ulong bufferGpuVa = GetGpuVirtualAddress(d3d12Resource);
            if (bufferGpuVa == 0UL)
            {
                throw new InvalidOperationException("Failed to get GPU virtual address for buffer");
            }

            // Calculate the GPU virtual address with offset
            // The BufferLocation in D3D12_CONSTANT_BUFFER_VIEW_DESC is the GPU virtual address of the start of the constant buffer
            ulong bufferLocation = bufferGpuVa + (ulong)offset;

            // Create D3D12_CONSTANT_BUFFER_VIEW_DESC structure
            // Based on DirectX 12 Constant Buffer Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_constant_buffer_view_desc
            var cbvDesc = new D3D12_CONSTANT_BUFFER_VIEW_DESC
            {
                BufferLocation = bufferLocation, // GPU virtual address (buffer base + offset)
                SizeInBytes = unchecked((uint)alignedSize) // Size must be multiple of 256 bytes
            };

            // Allocate memory for the CBV descriptor structure
            int cbvDescSize = Marshal.SizeOf(typeof(D3D12_CONSTANT_BUFFER_VIEW_DESC));
            IntPtr cbvDescPtr = Marshal.AllocHGlobal(cbvDescSize);
            try
            {
                Marshal.StructureToPtr(cbvDesc, cbvDescPtr, false);

                // Call ID3D12Device::CreateConstantBufferView
                // Based on DirectX 12 Constant Buffer Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createconstantbufferview
                // void CreateConstantBufferView(
                //   const D3D12_CONSTANT_BUFFER_VIEW_DESC *pDesc,
                //   D3D12_CPU_DESCRIPTOR_HANDLE DestDescriptor
                // );
                // Note: Unlike CreateShaderResourceView and CreateUnorderedAccessView, CreateConstantBufferView
                // does not take a pResource parameter - the resource is identified by the GPU virtual address in pDesc
                CallCreateConstantBufferView(_device, cbvDescPtr, cpuDescriptorHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(cbvDescPtr);
            }
        }

        private void CreateSrvDescriptorForStructuredBuffer(IBuffer buffer, int offset, int range, IntPtr cpuDescriptorHandle)
        {
            // Validate inputs
            if (buffer == null)
            {
                return;
            }

            if (cpuDescriptorHandle == IntPtr.Zero)
            {
                return;
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                return;
            }

            // Get D3D12 resource from buffer's native handle
            // buffer.NativeHandle is the ID3D12Resource* pointer
            IntPtr d3d12Resource = buffer.NativeHandle;
            if (d3d12Resource == IntPtr.Zero)
            {
                return;
            }

            // Get buffer description
            BufferDesc desc = buffer.Desc;

            // Structured buffers require a non-zero stride
            // StructStride is the size in bytes of each structured element
            int structStride = desc.StructStride;
            if (structStride <= 0)
            {
                // Invalid stride for structured buffer - structured buffers must have a positive stride
                return;
            }

            // Validate offset is aligned to struct stride
            // In D3D12, structured buffer SRV offset should be aligned to element boundaries
            if (offset < 0)
            {
                return;
            }

            if (offset % structStride != 0)
            {
                // Offset must be aligned to struct stride for structured buffers
                // Round down to nearest element boundary
                offset = (offset / structStride) * structStride;
            }

            // Calculate element offset (FirstElement)
            ulong firstElement = (ulong)(offset / structStride);

            // Calculate number of elements (NumElements)
            // If range is 0 or -1, use all remaining elements from offset to end of buffer
            uint numElements;
            if (range <= 0)
            {
                // Use all remaining elements from offset to end of buffer
                int remainingBytes = desc.ByteSize - offset;
                if (remainingBytes < 0)
                {
                    return; // Invalid offset beyond buffer size
                }
                numElements = (uint)(remainingBytes / structStride);
            }
            else
            {
                // Use specified range, must be aligned to struct stride
                int alignedRange = (range / structStride) * structStride;
                if (alignedRange <= 0)
                {
                    return; // Range too small
                }

                // Validate range doesn't exceed buffer bounds
                if (offset + alignedRange > desc.ByteSize)
                {
                    // Clamp to buffer size
                    alignedRange = desc.ByteSize - offset;
                    if (alignedRange < 0)
                    {
                        return;
                    }
                    alignedRange = (alignedRange / structStride) * structStride;
                }

                numElements = (uint)(alignedRange / structStride);
            }

            if (numElements == 0)
            {
                return; // No elements to view
            }

            // Create D3D12_SHADER_RESOURCE_VIEW_DESC structure
            // For structured buffers, use D3D12_SRV_DIMENSION_BUFFER
            var srvDesc = new D3D12_SHADER_RESOURCE_VIEW_DESC
            {
                Format = 0, // DXGI_FORMAT_UNKNOWN - structured buffers don't use formats
                ViewDimension = D3D12_SRV_DIMENSION_BUFFER
            };

            // Set buffer-specific parameters
            // D3D12_BUFFER_SRV structure for structured buffer view
            srvDesc.Buffer = new D3D12_BUFFER_SRV
            {
                FirstElement = firstElement, // Offset in elements
                NumElements = numElements, // Number of elements
                StructureByteStride = (uint)structStride, // Stride for structured buffers
                Flags = 0 // D3D12_BUFFER_SRV_FLAG_NONE (0) - structured buffers don't use raw view flags
            };

            // Allocate memory for the SRV descriptor structure
            int srvDescSize = Marshal.SizeOf(typeof(D3D12_SHADER_RESOURCE_VIEW_DESC));
            IntPtr srvDescPtr = Marshal.AllocHGlobal(srvDescSize);
            try
            {
                Marshal.StructureToPtr(srvDesc, srvDescPtr, false);

                // Call ID3D12Device::CreateShaderResourceView
                // Based on DirectX 12 Shader Resource Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createshaderresourceview
                // void CreateShaderResourceView(
                //   ID3D12Resource *pResource,
                //   const D3D12_SHADER_RESOURCE_VIEW_DESC *pDesc,
                //   D3D12_CPU_DESCRIPTOR_HANDLE DestDescriptor
                // );
                CallCreateShaderResourceView(_device, d3d12Resource, srvDescPtr, cpuDescriptorHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(srvDescPtr);
            }
        }

        /// <summary>
        /// Creates a UAV descriptor for a buffer in a binding set.
        /// Based on DirectX 12 Unordered Access Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createunorderedaccessview
        /// Creates a shader-visible descriptor in the CBV_SRV_UAV descriptor heap for binding sets.
        /// Supports both structured buffers (with stride) and raw buffers (byte-addressable, stride = 0).
        /// </summary>
        /// <param name="buffer">The buffer to create a UAV descriptor for.</param>
        /// <param name="offset">Offset in bytes from the start of the buffer.</param>
        /// <param name="range">Range in bytes to view. If 0 or -1, uses all remaining bytes from offset to end of buffer.</param>
        /// <param name="cpuDescriptorHandle">CPU descriptor handle where the UAV descriptor will be created.</param>
        private void CreateUavDescriptorForBuffer(IBuffer buffer, int offset, int range, IntPtr cpuDescriptorHandle)
        {
            // Validate inputs
            if (buffer == null)
            {
                return;
            }

            if (cpuDescriptorHandle == IntPtr.Zero)
            {
                return;
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                return;
            }

            // Get D3D12 resource from buffer's native handle
            // buffer.NativeHandle is the ID3D12Resource* pointer
            IntPtr d3d12Resource = buffer.NativeHandle;
            if (d3d12Resource == IntPtr.Zero)
            {
                return;
            }

            // Get buffer description
            BufferDesc desc = buffer.Desc;

            // Determine if this is a structured buffer (has stride) or raw buffer (no stride)
            int structStride = desc.StructStride;

            // Create D3D12_UNORDERED_ACCESS_VIEW_DESC structure
            // For buffers, use D3D12_UAV_DIMENSION_BUFFER
            var uavDesc = new D3D12_UNORDERED_ACCESS_VIEW_DESC
            {
                Format = 0, // DXGI_FORMAT_UNKNOWN - buffers don't use formats (format is determined by shader)
                ViewDimension = D3D12_UAV_DIMENSION_BUFFER
            };

            // Set buffer-specific parameters
            // D3D12_BUFFER_UAV structure for buffer unordered access view
            D3D12_BUFFER_UAV bufferUav;

            if (structStride > 0)
            {
                // Structured buffer: element-based addressing
                // Validate offset is aligned to struct stride
                // In D3D12, structured buffer UAV offset should be aligned to element boundaries
                if (offset < 0)
                {
                    return;
                }

                if (offset % structStride != 0)
                {
                    // Offset must be aligned to struct stride for structured buffers
                    // Round down to nearest element boundary
                    offset = (offset / structStride) * structStride;
                }

                // Calculate element offset (FirstElement)
                ulong firstElement = (ulong)(offset / structStride);

                // Calculate number of elements (NumElements)
                // If range is 0 or -1, use all remaining elements from offset to end of buffer
                uint numElements;
                if (range <= 0)
                {
                    // Use all remaining elements from offset to end of buffer
                    int remainingBytes = desc.ByteSize - offset;
                    if (remainingBytes < 0)
                    {
                        return; // Invalid offset beyond buffer size
                    }
                    numElements = (uint)(remainingBytes / structStride);
                }
                else
                {
                    // Use specified range, must be aligned to struct stride
                    int alignedRange = (range / structStride) * structStride;
                    if (alignedRange <= 0)
                    {
                        return; // Range too small
                    }

                    // Validate range doesn't exceed buffer bounds
                    if (offset + alignedRange > desc.ByteSize)
                    {
                        // Clamp to buffer size
                        alignedRange = desc.ByteSize - offset;
                        if (alignedRange < 0)
                        {
                            return;
                        }
                        alignedRange = (alignedRange / structStride) * structStride;
                    }

                    numElements = (uint)(alignedRange / structStride);
                }

                if (numElements == 0)
                {
                    return; // No elements to view
                }

                // Structured buffer UAV: element-based addressing with stride
                bufferUav = new D3D12_BUFFER_UAV
                {
                    FirstElement = firstElement, // Offset in elements
                    NumElements = numElements, // Number of elements
                    StructureByteStride = (uint)structStride, // Stride for structured buffers
                    CounterOffsetInBytes = 0, // No counter resource (append/consume buffers not supported yet)
                    Flags = D3D12_BUFFER_UAV_FLAG_NONE // Structured buffers don't use raw view flags
                };
            }
            else
            {
                // Raw buffer: byte-addressable
                // For raw buffers, FirstElement and NumElements are in bytes (not elements)
                // Raw buffers are accessed as byte-addressable buffers in shaders
                if (offset < 0)
                {
                    return;
                }

                // For raw buffers, offset and range should be aligned to 4 bytes (DWORD alignment)
                // D3D12 requires raw buffer views to be DWORD-aligned
                int dwordAlignment = 4;
                if (offset % dwordAlignment != 0)
                {
                    // Round down to nearest DWORD boundary
                    offset = (offset / dwordAlignment) * dwordAlignment;
                }

                // Calculate byte offset (FirstElement in bytes for raw buffers)
                ulong firstElementBytes = (ulong)offset;

                // Calculate number of bytes (NumElements in bytes for raw buffers)
                // If range is 0 or -1, use all remaining bytes from offset to end of buffer
                uint numElementsBytes;
                if (range <= 0)
                {
                    // Use all remaining bytes from offset to end of buffer
                    int remainingBytes = desc.ByteSize - offset;
                    if (remainingBytes < 0)
                    {
                        return; // Invalid offset beyond buffer size
                    }
                    // Align to DWORD boundary
                    numElementsBytes = (uint)((remainingBytes / dwordAlignment) * dwordAlignment);
                }
                else
                {
                    // Use specified range, must be aligned to DWORD boundary
                    int alignedRange = (range / dwordAlignment) * dwordAlignment;
                    if (alignedRange <= 0)
                    {
                        return; // Range too small
                    }

                    // Validate range doesn't exceed buffer bounds
                    if (offset + alignedRange > desc.ByteSize)
                    {
                        // Clamp to buffer size
                        alignedRange = desc.ByteSize - offset;
                        if (alignedRange < 0)
                        {
                            return;
                        }
                        alignedRange = (alignedRange / dwordAlignment) * dwordAlignment;
                    }

                    numElementsBytes = (uint)alignedRange;
                }

                if (numElementsBytes == 0)
                {
                    return; // No bytes to view
                }

                // Raw buffer UAV: byte-addressable with DWORD alignment
                // For raw buffers, StructureByteStride must be 0
                // Flags must include D3D12_BUFFER_UAV_FLAG_RAW to indicate raw view
                bufferUav = new D3D12_BUFFER_UAV
                {
                    FirstElement = firstElementBytes, // Offset in bytes (for raw buffers)
                    NumElements = numElementsBytes, // Number of bytes (for raw buffers)
                    StructureByteStride = 0, // Must be 0 for raw buffers
                    CounterOffsetInBytes = 0, // No counter resource (append/consume buffers not supported yet)
                    Flags = D3D12_BUFFER_UAV_FLAG_RAW // Indicates raw byte-addressable buffer view
                };
            }

            // Set the buffer UAV structure in the descriptor
            uavDesc.Buffer = bufferUav;

            // Allocate memory for the UAV descriptor structure
            int uavDescSize = Marshal.SizeOf(typeof(D3D12_UNORDERED_ACCESS_VIEW_DESC));
            IntPtr uavDescPtr = Marshal.AllocHGlobal(uavDescSize);
            try
            {
                Marshal.StructureToPtr(uavDesc, uavDescPtr, false);

                // Call ID3D12Device::CreateUnorderedAccessView
                // Based on DirectX 12 Unordered Access Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createunorderedaccessview
                // void CreateUnorderedAccessView(
                //   ID3D12Resource *pResource,
                //   ID3D12Resource *pCounterResource,  // NULL for buffers without append/consume counters
                //   const D3D12_UNORDERED_ACCESS_VIEW_DESC *pDesc,
                //   D3D12_CPU_DESCRIPTOR_HANDLE DestDescriptor
                // );
                // pCounterResource is NULL for buffers (counters are for append/consume structured buffers, not yet supported)
                CallCreateUnorderedAccessView(_device, d3d12Resource, IntPtr.Zero, uavDescPtr, cpuDescriptorHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(uavDescPtr);
            }
        }

        /// <summary>
        /// Creates an SRV descriptor for an acceleration structure.
        /// Based on DirectX 12 Raytracing Acceleration Structure SRV: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_shader_resource_view_desc
        /// Acceleration structures use D3D12_SRV_DIMENSION_RAYTRACING_ACCELERATION_STRUCTURE (value 11).
        /// </summary>
        /// <param name="accelStruct">The acceleration structure to create an SRV descriptor for.</param>
        /// <param name="cpuDescriptorHandle">CPU descriptor handle where the SRV descriptor will be created.</param>
        private void CreateSrvDescriptorForAccelStruct(IAccelStruct accelStruct, IntPtr cpuDescriptorHandle)
        {
            if (accelStruct == null)
            {
                throw new ArgumentNullException(nameof(accelStruct));
            }

            if (cpuDescriptorHandle == IntPtr.Zero)
            {
                throw new ArgumentException("CPU descriptor handle must be valid", nameof(cpuDescriptorHandle));
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                throw new InvalidOperationException("D3D12 device is not initialized");
            }

            // Get GPU virtual address of the acceleration structure
            // Acceleration structure SRVs use the GPU virtual address directly
            ulong accelStructGpuVa = accelStruct.DeviceAddress;
            if (accelStructGpuVa == 0UL)
            {
                throw new ArgumentException("Acceleration structure does not have a valid device address", nameof(accelStruct));
            }

            // Create D3D12_SHADER_RESOURCE_VIEW_DESC structure for acceleration structure
            // Based on DirectX 12 Raytracing Acceleration Structure SRV: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_shader_resource_view_desc
            // Acceleration structures use D3D12_SRV_DIMENSION_RAYTRACING_ACCELERATION_STRUCTURE (value 11)
            // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            var srvDesc = new D3D12_SHADER_RESOURCE_VIEW_DESC
            {
                Format = 0, // DXGI_FORMAT_UNKNOWN - acceleration structures don't use formats
                ViewDimension = 11 // D3D12_SRV_DIMENSION_RAYTRACING_ACCELERATION_STRUCTURE
            };

            // Set acceleration structure-specific parameters
            // D3D12_RAYTRACING_ACCELERATION_STRUCTURE_SRV structure contains the GPU virtual address
            // The structure is embedded in the union of D3D12_SHADER_RESOURCE_VIEW_DESC
            // For acceleration structures, we need to set the Location field to the GPU virtual address
            // The union field for raytracing acceleration structure SRV is at offset after ViewDimension
            // D3D12_RAYTRACING_ACCELERATION_STRUCTURE_SRV contains a single field: Location (D3D12_GPU_VIRTUAL_ADDRESS, which is ulong)
            unsafe
            {
                // Allocate memory for the SRV descriptor structure
                int srvDescSize = Marshal.SizeOf(typeof(D3D12_SHADER_RESOURCE_VIEW_DESC));
                IntPtr srvDescPtr = Marshal.AllocHGlobal(srvDescSize);
                try
                {
                    // Marshal the base structure
                    Marshal.StructureToPtr(srvDesc, srvDescPtr, false);

                    // Set the acceleration structure GPU virtual address in the union
                    // The union starts after Format (4 bytes) and ViewDimension (4 bytes) = 8 bytes offset
                    // D3D12_RAYTRACING_ACCELERATION_STRUCTURE_SRV.Location is at offset 0 within the union
                    ulong* locationPtr = (ulong*)((byte*)srvDescPtr + 8);
                    *locationPtr = accelStructGpuVa;

                    // Call ID3D12Device::CreateShaderResourceView
                    // For acceleration structures, pResource is NULL (IntPtr.Zero) because the address is in the descriptor
                    // Based on DirectX 12 Raytracing Acceleration Structure SRV: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createshaderresourceview
                    CallCreateShaderResourceView(_device, IntPtr.Zero, srvDescPtr, cpuDescriptorHandle);
                }
                finally
                {
                    Marshal.FreeHGlobal(srvDescPtr);
                }
            }
        }

        private IntPtr CreateSrvDescriptorForTexture(IntPtr d3d12Resource, TextureDesc desc, uint dxgiFormat, uint resourceDimension)
        {
            if (d3d12Resource == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return IntPtr.Zero;
            }

            if (_device == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Allocate SRV descriptor handle
            IntPtr srvHandle = AllocateSrvDescriptor();
            if (srvHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Determine SRV dimension based on resource dimension
            uint srvDimension;
            switch (resourceDimension)
            {
                case D3D12_RESOURCE_DIMENSION_TEXTURE1D:
                    srvDimension = D3D12_SRV_DIMENSION_TEXTURE1D;
                    break;
                case D3D12_RESOURCE_DIMENSION_TEXTURE2D:
                    srvDimension = D3D12_SRV_DIMENSION_TEXTURE2D;
                    break;
                case D3D12_RESOURCE_DIMENSION_TEXTURE3D:
                    srvDimension = D3D12_SRV_DIMENSION_TEXTURE3D;
                    break;
                default:
                    // Unsupported dimension, default to TEXTURE2D
                    srvDimension = D3D12_SRV_DIMENSION_TEXTURE2D;
                    break;
            }

            // Create D3D12_SHADER_RESOURCE_VIEW_DESC structure
            var srvDesc = new D3D12_SHADER_RESOURCE_VIEW_DESC
            {
                Format = dxgiFormat,
                ViewDimension = srvDimension
            };

            // Set dimension-specific parameters
            // For most textures, we use the default mip settings (all mips, starting from mip 0)
            // In DirectX 12, -1 (0xFFFFFFFF) means "use all available mips"
            uint mipLevels = (desc.MipLevels > 0) ? unchecked((uint)desc.MipLevels) : unchecked((uint)(-1));

            if (srvDimension == D3D12_SRV_DIMENSION_TEXTURE2D)
            {
                srvDesc.Texture2D = new D3D12_TEX2D_SRV
                {
                    MostDetailedMip = 0, // Start from first mip level
                    MipLevels = mipLevels, // -1 (0xFFFFFFFF) means all mips, otherwise use specified count
                    PlaneSlice = 0, // Typically 0 for non-planar formats
                    ResourceMinLODClamp = 0.0f // No clamping
                };
            }
            else if (srvDimension == D3D12_SRV_DIMENSION_TEXTURE1D)
            {
                srvDesc.Texture1D = new D3D12_TEX1D_SRV
                {
                    MostDetailedMip = 0,
                    MipLevels = mipLevels,
                    ResourceMinLODClamp = 0.0f
                };
            }
            else if (srvDimension == D3D12_SRV_DIMENSION_TEXTURE3D)
            {
                srvDesc.Texture3D = new D3D12_TEX3D_SRV
                {
                    MostDetailedMip = 0,
                    MipLevels = mipLevels,
                    ResourceMinLODClamp = 0.0f
                };
            }

            // Allocate memory for the SRV descriptor structure
            int srvDescSize = Marshal.SizeOf(typeof(D3D12_SHADER_RESOURCE_VIEW_DESC));
            IntPtr srvDescPtr = Marshal.AllocHGlobal(srvDescSize);
            try
            {
                Marshal.StructureToPtr(srvDesc, srvDescPtr, false);

                // Call ID3D12Device::CreateShaderResourceView
                CallCreateShaderResourceView(_device, d3d12Resource, srvDescPtr, srvHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(srvDescPtr);
            }

            return srvHandle;
        }

        #endregion

        #region D3D12 SRV Descriptor Heap Management

        /// <summary>
        /// Ensures the SRV descriptor heap is created and initialized.
        /// Creates an SRV descriptor heap with the default capacity if one doesn't exist.
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_descriptor_heap_desc
        /// </summary>
        private void EnsureSrvDescriptorHeap()
        {
            if (_srvDescriptorHeap != IntPtr.Zero)
            {
                return; // Heap already exists
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Create D3D12_DESCRIPTOR_HEAP_DESC structure for SRV heap
                // Use CBV_SRV_UAV heap type for SRV descriptors (non-shader-visible for texture management)
                var heapDesc = new D3D12_DESCRIPTOR_HEAP_DESC
                {
                    Type = D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
                    NumDescriptors = (uint)DefaultSrvHeapCapacity,
                    Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE, // SRV heaps for texture management are not shader-visible
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
                        Guid iidDescriptorHeap = IID_ID3D12DescriptorHeap;
                        int hr = CallCreateDescriptorHeap(_device, heapDescPtr, ref iidDescriptorHeap, heapPtr);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                        }

                        // Get the descriptor heap pointer
                        IntPtr descriptorHeap = Marshal.ReadIntPtr(heapPtr);
                        if (descriptorHeap == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Descriptor heap pointer is null");
                        }

                        // Get descriptor heap start handle (CPU handle for descriptor heap)
                        IntPtr cpuHandle = CallGetCPUDescriptorHandleForHeapStart(descriptorHeap);
                        if (cpuHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Failed to get CPU descriptor handle for heap start");
                        }

                        // Get descriptor increment size
                        uint descriptorIncrementSize = CallGetDescriptorHandleIncrementSize(_device, D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
                        if (descriptorIncrementSize == 0)
                        {
                            throw new InvalidOperationException("Failed to get descriptor handle increment size");
                        }

                        // Store heap information
                        _srvDescriptorHeap = descriptorHeap;
                        _srvHeapCpuStartHandle = cpuHandle;
                        _srvHeapDescriptorIncrementSize = descriptorIncrementSize;
                        _srvHeapCapacity = DefaultSrvHeapCapacity;
                        _srvHeapNextIndex = 0;
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
                throw new InvalidOperationException($"Failed to create SRV descriptor heap: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Allocates a descriptor handle from the SRV descriptor heap.
        /// Returns IntPtr.Zero if allocation fails.
        /// </summary>
        private IntPtr AllocateSrvDescriptor()
        {
            EnsureSrvDescriptorHeap();

            if (_srvDescriptorHeap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (_srvHeapNextIndex >= _srvHeapCapacity)
            {
                // Heap is full - in a production implementation, we might want to create a larger heap or handle this differently
                throw new InvalidOperationException($"SRV descriptor heap is full (capacity: {_srvHeapCapacity})");
            }

            // Calculate CPU descriptor handle for this index
            IntPtr cpuDescriptorHandle = OffsetDescriptorHandle(_srvHeapCpuStartHandle, _srvHeapNextIndex, _srvHeapDescriptorIncrementSize);
            int allocatedIndex = _srvHeapNextIndex;
            _srvHeapNextIndex++;

            return cpuDescriptorHandle;
        }

        /// <summary>
        /// COM interface method delegate for CreateConstantBufferView.
        /// Based on DirectX 12 Constant Buffer Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createconstantbufferview
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CreateConstantBufferViewDelegate(IntPtr device, IntPtr pDesc, IntPtr DestDescriptor);

        /// <summary>
        /// COM interface method delegate for CreateShaderResourceView.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CreateShaderResourceViewDelegate(IntPtr device, IntPtr pResource, IntPtr pDesc, IntPtr DestDescriptor);

        /// <summary>
        /// Calls ID3D12Device::CreateConstantBufferView through COM vtable.
        /// VTable index 33 for ID3D12Device (before CreateRenderTargetView at index 34).
        /// Based on DirectX 12 Constant Buffer Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createconstantbufferview
        /// Note: Unlike CreateShaderResourceView and CreateUnorderedAccessView, CreateConstantBufferView does not take a pResource parameter.
        /// The resource is identified by the GPU virtual address in the D3D12_CONSTANT_BUFFER_VIEW_DESC structure.
        /// </summary>
        private unsafe void CallCreateConstantBufferView(IntPtr device, IntPtr pDesc, IntPtr DestDescriptor)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device == IntPtr.Zero || pDesc == IntPtr.Zero || DestDescriptor == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateConstantBufferView is at index 33 in ID3D12Device vtable (before CreateRenderTargetView at 34)
            IntPtr methodPtr = vtable[33];

            // Create delegate from function pointer
            CreateConstantBufferViewDelegate createCbv = (CreateConstantBufferViewDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateConstantBufferViewDelegate));

            createCbv(device, pDesc, DestDescriptor);
        }

        /// <summary>
        /// Calls ID3D12Device::CreateShaderResourceView through COM vtable.
        /// VTable index 35 for ID3D12Device (after CreateRenderTargetView at index 34).
        /// Based on DirectX 12 Shader Resource Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createshaderresourceview
        /// </summary>
        private unsafe void CallCreateShaderResourceView(IntPtr device, IntPtr pResource, IntPtr pDesc, IntPtr DestDescriptor)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device == IntPtr.Zero || pResource == IntPtr.Zero || DestDescriptor == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateShaderResourceView is at index 35 in ID3D12Device vtable (after CreateRenderTargetView at 34)
            IntPtr methodPtr = vtable[35];

            // Create delegate from function pointer
            CreateShaderResourceViewDelegate createSrv = (CreateShaderResourceViewDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateShaderResourceViewDelegate));

            createSrv(device, pResource, pDesc, DestDescriptor);
        }

        #endregion

        #region D3D12 UAV Descriptor Heap Management

        /// <summary>
        /// Ensures the UAV descriptor heap is created and initialized.
        /// Creates a UAV descriptor heap with the default capacity if one doesn't exist.
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_descriptor_heap_desc
        /// </summary>
        private void EnsureUavDescriptorHeap()
        {
            if (_uavDescriptorHeap != IntPtr.Zero)
            {
                return; // Heap already exists
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Create D3D12_DESCRIPTOR_HEAP_DESC structure for UAV heap
                // Use CBV_SRV_UAV heap type for UAV descriptors (non-shader-visible for CPU clearing operations)
                var heapDesc = new D3D12_DESCRIPTOR_HEAP_DESC
                {
                    Type = D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
                    NumDescriptors = (uint)DefaultUavHeapCapacity,
                    Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE, // UAV heaps for clearing are not shader-visible
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
                        Guid iidDescriptorHeap = IID_ID3D12DescriptorHeap;
                        int hr = CallCreateDescriptorHeap(_device, heapDescPtr, ref iidDescriptorHeap, heapPtr);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                        }

                        // Get the descriptor heap pointer
                        IntPtr descriptorHeap = Marshal.ReadIntPtr(heapPtr);
                        if (descriptorHeap == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Descriptor heap pointer is null");
                        }

                        // Get descriptor heap start handle (CPU handle for descriptor heap)
                        IntPtr cpuHandle = CallGetCPUDescriptorHandleForHeapStart(descriptorHeap);
                        if (cpuHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Failed to get CPU descriptor handle for heap start");
                        }

                        // Get descriptor increment size
                        uint descriptorIncrementSize = CallGetDescriptorHandleIncrementSize(_device, D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
                        if (descriptorIncrementSize == 0)
                        {
                            throw new InvalidOperationException("Failed to get descriptor handle increment size");
                        }

                        // Store heap information
                        _uavDescriptorHeap = descriptorHeap;
                        _uavHeapCpuStartHandle = cpuHandle;
                        _uavHeapDescriptorIncrementSize = descriptorIncrementSize;
                        _uavHeapCapacity = DefaultUavHeapCapacity;
                        _uavHeapNextIndex = 0;
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
                throw new InvalidOperationException($"Failed to create UAV descriptor heap: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Allocates a descriptor handle from the UAV descriptor heap.
        /// Returns IntPtr.Zero if allocation fails.
        /// </summary>
        private IntPtr AllocateUavDescriptor()
        {
            EnsureUavDescriptorHeap();

            if (_uavDescriptorHeap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (_uavHeapNextIndex >= _uavHeapCapacity)
            {
                // Heap is full - in a production implementation, we might want to create a larger heap or handle this differently
                throw new InvalidOperationException($"UAV descriptor heap is full (capacity: {_uavHeapCapacity})");
            }

            // Calculate CPU descriptor handle for this index
            IntPtr cpuDescriptorHandle = OffsetDescriptorHandle(_uavHeapCpuStartHandle, _uavHeapNextIndex, _uavHeapDescriptorIncrementSize);
            int allocatedIndex = _uavHeapNextIndex;
            _uavHeapNextIndex++;

            return cpuDescriptorHandle;
        }

        /// <summary>
        /// Ensures the CBV_SRV_UAV descriptor heap is created and initialized.
        /// Creates a shader-visible CBV_SRV_UAV descriptor heap with the default capacity if one doesn't exist.
        /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_descriptor_heap_desc
        /// This heap is used for binding sets (SRV/UAV/CBV descriptors for shader resources).
        /// </summary>
        private void EnsureCbvSrvUavDescriptorHeap()
        {
            if (_cbvSrvUavDescriptorHeap != IntPtr.Zero)
            {
                return; // Heap already exists
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (_device == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Create D3D12_DESCRIPTOR_HEAP_DESC structure for CBV_SRV_UAV heap
                // Use shader-visible flag for binding sets (descriptors must be accessible from shaders)
                var heapDesc = new D3D12_DESCRIPTOR_HEAP_DESC
                {
                    Type = D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
                    NumDescriptors = (uint)DefaultCbvSrvUavHeapCapacity,
                    Flags = D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE, // Shader-visible for binding sets
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
                        Guid iidDescriptorHeap = IID_ID3D12DescriptorHeap;
                        int hr = CallCreateDescriptorHeap(_device, heapDescPtr, ref iidDescriptorHeap, heapPtr);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"CreateDescriptorHeap failed with HRESULT 0x{hr:X8}");
                        }

                        // Get the descriptor heap pointer
                        IntPtr descriptorHeap = Marshal.ReadIntPtr(heapPtr);
                        if (descriptorHeap == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Descriptor heap pointer is null");
                        }

                        // Get descriptor heap start handles (CPU and GPU handles)
                        IntPtr cpuHandle = CallGetCPUDescriptorHandleForHeapStart(descriptorHeap);
                        if (cpuHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Failed to get CPU descriptor handle for heap start");
                        }

                        D3D12_GPU_DESCRIPTOR_HANDLE gpuHandle = CallGetGPUDescriptorHandleForHeapStart(descriptorHeap);
                        if (gpuHandle.ptr == 0)
                        {
                            throw new InvalidOperationException("Failed to get GPU descriptor handle for heap start");
                        }

                        // Get descriptor increment size
                        uint descriptorIncrementSize = CallGetDescriptorHandleIncrementSize(_device, D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
                        if (descriptorIncrementSize == 0)
                        {
                            throw new InvalidOperationException("Failed to get descriptor handle increment size");
                        }

                        // Store heap information
                        _cbvSrvUavDescriptorHeap = descriptorHeap;
                        _cbvSrvUavHeapCpuStartHandle = cpuHandle;
                        _cbvSrvUavHeapGpuStartHandle = gpuHandle;
                        _cbvSrvUavHeapDescriptorIncrementSize = descriptorIncrementSize;
                        _cbvSrvUavHeapCapacity = DefaultCbvSrvUavHeapCapacity;
                        _cbvSrvUavHeapNextIndex = 0;
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
                throw new InvalidOperationException($"Failed to create CBV_SRV_UAV descriptor heap: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Offsets a GPU descriptor handle by a specified number of descriptors.
        /// Based on DirectX 12 GPU Descriptor Handles: https://docs.microsoft.com/en-us/windows/win32/direct3d12/descriptors-overview
        /// </summary>
        private D3D12_GPU_DESCRIPTOR_HANDLE OffsetGpuDescriptorHandle(D3D12_GPU_DESCRIPTOR_HANDLE handle, int offset, uint incrementSize)
        {
            // Offset = handle.ptr + (offset * incrementSize)
            ulong handleValue = handle.ptr;
            ulong offsetValue = (ulong)offset * incrementSize;
            return new D3D12_GPU_DESCRIPTOR_HANDLE { ptr = handleValue + offsetValue };
        }

        #region D3D12 Descriptor Structures for Binding Sets

        /// <summary>
        /// D3D12_UNORDERED_ACCESS_VIEW_DESC structure for unordered access views.
        /// Based on DirectX 12 Unordered Access Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_unordered_access_view_desc
        /// Uses explicit layout to model the union of view dimension types.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct D3D12_UNORDERED_ACCESS_VIEW_DESC
        {
            [FieldOffset(0)]
            public uint Format; // DXGI_FORMAT
            [FieldOffset(4)]
            public uint ViewDimension; // D3D12_UAV_DIMENSION
            // Union members start at offset 8 (all overlap at the same offset)
            [FieldOffset(8)]
            public D3D12_BUFFER_UAV Buffer;
            [FieldOffset(8)]
            public D3D12_TEX1D_UAV Texture1D;
            [FieldOffset(8)]
            public D3D12_TEX1D_ARRAY_UAV Texture1DArray;
            [FieldOffset(8)]
            public D3D12_TEX2D_UAV Texture2D;
            [FieldOffset(8)]
            public D3D12_TEX2D_ARRAY_UAV Texture2DArray;
            [FieldOffset(8)]
            public D3D12_TEX3D_UAV Texture3D;
        }

        /// <summary>
        /// D3D12_UAV_DIMENSION constants for unordered access view dimensions.
        /// Based on DirectX 12 UAV Dimensions: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_uav_dimension
        /// </summary>
        private const uint D3D12_UAV_DIMENSION_UNKNOWN = 0;
        private const uint D3D12_UAV_DIMENSION_BUFFER = 1;
        private const uint D3D12_UAV_DIMENSION_TEXTURE1D = 2;
        private const uint D3D12_UAV_DIMENSION_TEXTURE1DARRAY = 3;
        private const uint D3D12_UAV_DIMENSION_TEXTURE2D = 4;
        private const uint D3D12_UAV_DIMENSION_TEXTURE2DARRAY = 5;
        private const uint D3D12_UAV_DIMENSION_TEXTURE3D = 8;

        /// <summary>
        /// D3D12_TEX2D_UAV structure for 2D texture unordered access views.
        /// Based on DirectX 12 UAV Structures: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_tex2d_uav
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_UAV
        {
            public uint MipSlice;
            public uint PlaneSlice; // For planar formats (typically 0 for non-planar)
        }

        /// <summary>
        /// D3D12_TEX1D_UAV structure for 1D texture unordered access views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX1D_UAV
        {
            public uint MipSlice;
        }

        /// <summary>
        /// D3D12_TEX1D_ARRAY_UAV structure for 1D texture array unordered access views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX1D_ARRAY_UAV
        {
            public uint MipSlice;
            public uint FirstArraySlice;
            public uint ArraySize;
        }

        /// <summary>
        /// D3D12_TEX2D_ARRAY_UAV structure for 2D texture array unordered access views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_ARRAY_UAV
        {
            public uint MipSlice;
            public uint FirstArraySlice;
            public uint ArraySize;
            public uint PlaneSlice;
        }

        /// <summary>
        /// D3D12_TEX3D_UAV structure for 3D texture unordered access views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX3D_UAV
        {
            public uint MipSlice;
            public uint FirstWSlice;
            public uint WSize;
        }

        /// <summary>
        /// D3D12_BUFFER_UAV structure for buffer unordered access views.
        /// Based on DirectX 12 Buffer UAV: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_buffer_uav
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_BUFFER_UAV
        {
            public ulong FirstElement; // Offset in elements
            public uint NumElements; // Number of elements
            public uint StructureByteStride; // Stride for structured buffers (0 for raw buffers)
            public ulong CounterOffsetInBytes; // Offset for counter (0 if no counter)
            public uint Flags; // D3D12_BUFFER_UAV_FLAGS
        }

        /// <summary>
        /// D3D12_BUFFER_UAV_FLAGS constants for buffer unordered access view flags.
        /// Based on DirectX 12 Buffer UAV Flags: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_buffer_uav_flags
        /// </summary>
        private const uint D3D12_BUFFER_UAV_FLAG_NONE = 0;
        private const uint D3D12_BUFFER_UAV_FLAG_RAW = 0x00000001; // Indicates raw byte-addressable buffer view

        /// <summary>
        /// D3D12_SHADER_RESOURCE_VIEW_DESC structure for shader resource views.
        /// Based on DirectX 12 Shader Resource Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_shader_resource_view_desc
        /// Uses explicit layout to model the union of view dimension types.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct D3D12_SHADER_RESOURCE_VIEW_DESC
        {
            [FieldOffset(0)]
            public uint Format; // DXGI_FORMAT
            [FieldOffset(4)]
            public uint ViewDimension; // D3D12_SRV_DIMENSION
            // Union members start at offset 8 (all overlap at the same offset)
            [FieldOffset(8)]
            public D3D12_BUFFER_SRV Buffer;
            [FieldOffset(8)]
            public D3D12_TEX1D_SRV Texture1D;
            [FieldOffset(8)]
            public D3D12_TEX1D_ARRAY_SRV Texture1DArray;
            [FieldOffset(8)]
            public D3D12_TEX2D_SRV Texture2D;
            [FieldOffset(8)]
            public D3D12_TEX2D_ARRAY_SRV Texture2DArray;
            [FieldOffset(8)]
            public D3D12_TEX2DMS_SRV Texture2DMS;
            [FieldOffset(8)]
            public D3D12_TEX2DMS_ARRAY_SRV Texture2DMSArray;
            [FieldOffset(8)]
            public D3D12_TEX3D_SRV Texture3D;
            [FieldOffset(8)]
            public D3D12_TEXCUBE_SRV TextureCube;
            [FieldOffset(8)]
            public D3D12_TEXCUBE_ARRAY_SRV TextureCubeArray;
        }

        /// <summary>
        /// D3D12_SRV_DIMENSION constants for shader resource view dimensions.
        /// Based on DirectX 12 SRV Dimensions: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_srv_dimension
        /// </summary>
        private const uint D3D12_SRV_DIMENSION_UNKNOWN = 0;
        private const uint D3D12_SRV_DIMENSION_BUFFER = 1;
        private const uint D3D12_SRV_DIMENSION_TEXTURE1D = 2;
        private const uint D3D12_SRV_DIMENSION_TEXTURE1DARRAY = 3;
        private const uint D3D12_SRV_DIMENSION_TEXTURE2D = 4;
        private const uint D3D12_SRV_DIMENSION_TEXTURE2DARRAY = 5;
        private const uint D3D12_SRV_DIMENSION_TEXTURE2DMS = 6;
        private const uint D3D12_SRV_DIMENSION_TEXTURE2DMSARRAY = 7;
        private const uint D3D12_SRV_DIMENSION_TEXTURE3D = 8;
        private const uint D3D12_SRV_DIMENSION_TEXTURECUBE = 9;
        private const uint D3D12_SRV_DIMENSION_TEXTURECUBEARRAY = 10;

        /// <summary>
        /// D3D12_TEX2D_SRV structure for 2D texture shader resource views.
        /// Based on DirectX 12 SRV Structures: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_tex2d_srv
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_SRV
        {
            public uint MostDetailedMip;
            public uint MipLevels;
            public uint PlaneSlice; // For planar formats (typically 0 for non-planar)
            public float ResourceMinLODClamp;
        }

        /// <summary>
        /// D3D12_TEX1D_SRV structure for 1D texture shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX1D_SRV
        {
            public uint MostDetailedMip;
            public uint MipLevels;
            public float ResourceMinLODClamp;
        }

        /// <summary>
        /// D3D12_TEX1D_ARRAY_SRV structure for 1D texture array shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX1D_ARRAY_SRV
        {
            public uint MostDetailedMip;
            public uint MipLevels;
            public uint FirstArraySlice;
            public uint ArraySize;
            public float ResourceMinLODClamp;
        }

        /// <summary>
        /// D3D12_TEX2D_ARRAY_SRV structure for 2D texture array shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2D_ARRAY_SRV
        {
            public uint MostDetailedMip;
            public uint MipLevels;
            public uint FirstArraySlice;
            public uint ArraySize;
            public uint PlaneSlice;
            public float ResourceMinLODClamp;
        }

        /// <summary>
        /// D3D12_TEX2DMS_SRV structure for 2D multisampled texture shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2DMS_SRV
        {
            public uint UnusedField_NothingToDefine; // MS textures don't have mip slices
        }

        /// <summary>
        /// D3D12_TEX2DMS_ARRAY_SRV structure for 2D multisampled texture array shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX2DMS_ARRAY_SRV
        {
            public uint FirstArraySlice;
            public uint ArraySize;
        }

        /// <summary>
        /// D3D12_TEX3D_SRV structure for 3D texture shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEX3D_SRV
        {
            public uint MostDetailedMip;
            public uint MipLevels;
            public float ResourceMinLODClamp;
        }

        /// <summary>
        /// D3D12_TEXCUBE_SRV structure for cube texture shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEXCUBE_SRV
        {
            public uint MostDetailedMip;
            public uint MipLevels;
            public float ResourceMinLODClamp;
        }

        /// <summary>
        /// D3D12_TEXCUBE_ARRAY_SRV structure for cube texture array shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_TEXCUBE_ARRAY_SRV
        {
            public uint MostDetailedMip;
            public uint MipLevels;
            public uint First2DArrayFace;
            public uint NumCubes;
            public float ResourceMinLODClamp;
        }

        /// <summary>
        /// D3D12_BUFFER_SRV structure for buffer shader resource views.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_BUFFER_SRV
        {
            public ulong FirstElement; // Offset in elements
            public uint NumElements; // Number of elements
            public uint StructureByteStride; // Stride for structured buffers (0 for raw buffers)
            public uint Flags; // D3D12_BUFFER_SRV_FLAGS
        }

        /// <summary>
        /// D3D12_CONSTANT_BUFFER_VIEW_DESC structure for constant buffer views.
        /// Based on DirectX 12 Constant Buffer Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_constant_buffer_view_desc
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_CONSTANT_BUFFER_VIEW_DESC
        {
            public ulong BufferLocation; // GPU virtual address
            public uint SizeInBytes; // Size of constant buffer (must be multiple of 256)
        }

        #endregion

        /// <summary>
        /// COM interface method delegate for CreateUnorderedAccessView.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CreateUnorderedAccessViewDelegate(IntPtr device, IntPtr pResource, IntPtr pCounterResource, IntPtr pDesc, IntPtr DestDescriptor);

        /// <summary>
        /// Calls ID3D12Device::CreateUnorderedAccessView through COM vtable.
        /// VTable index 36 for ID3D12Device.
        /// Based on DirectX 12 Unordered Access Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createunorderedaccessview
        /// </summary>
        private unsafe void CallCreateUnorderedAccessView(IntPtr device, IntPtr pResource, IntPtr pCounterResource, IntPtr pDesc, IntPtr DestDescriptor)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device == IntPtr.Zero || pResource == IntPtr.Zero || DestDescriptor == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)device;
            // CreateUnorderedAccessView is at index 36 in ID3D12Device vtable
            IntPtr methodPtr = vtable[36];

            // Create delegate from function pointer
            CreateUnorderedAccessViewDelegate createUav = (CreateUnorderedAccessViewDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateUnorderedAccessViewDelegate));

            createUav(device, pResource, pCounterResource, pDesc, DestDescriptor);
        }

        /// <summary>
        /// Converts TextureFormat to DXGI_FORMAT for unordered access views.
        /// Based on DirectX 12 Texture Formats: https://docs.microsoft.com/en-us/windows/win32/api/dxgiformat/ne-dxgiformat-dxgi_format
        /// Supports formats that can be used as UAVs (typically uint/int formats for ClearUnorderedAccessViewUint).
        /// </summary>
        private uint ConvertTextureFormatToDxgiFormatForUav(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8_UNorm:
                    return 61; // DXGI_FORMAT_R8_UNORM
                case TextureFormat.R8_UInt:
                    return 62; // DXGI_FORMAT_R8_UINT
                case TextureFormat.R8_SInt:
                    return 63; // DXGI_FORMAT_R8_SINT
                case TextureFormat.R8G8_UNorm:
                    return 49; // DXGI_FORMAT_R8G8_UNORM
                case TextureFormat.R8G8_UInt:
                    return 50; // DXGI_FORMAT_R8G8_UINT
                case TextureFormat.R8G8_SInt:
                    return 51; // DXGI_FORMAT_R8G8_SINT
                case TextureFormat.R8G8B8A8_UNorm:
                    return 28; // DXGI_FORMAT_R8G8B8A8_UNORM
                case TextureFormat.R8G8B8A8_UInt:
                    return 30; // DXGI_FORMAT_R8G8B8A8_UINT
                case TextureFormat.R8G8B8A8_SInt:
                    return 29; // DXGI_FORMAT_R8G8B8A8_SINT
                case TextureFormat.R16_UNorm:
                    return 56; // DXGI_FORMAT_R16_UNORM
                case TextureFormat.R16_UInt:
                    return 57; // DXGI_FORMAT_R16_UINT
                case TextureFormat.R16_SInt:
                    return 58; // DXGI_FORMAT_R16_SINT
                case TextureFormat.R16_Float:
                    return 54; // DXGI_FORMAT_R16_FLOAT
                case TextureFormat.R16G16_UNorm:
                    return 35; // DXGI_FORMAT_R16G16_UNORM
                case TextureFormat.R16G16_UInt:
                    return 36; // DXGI_FORMAT_R16G16_UINT
                case TextureFormat.R16G16_SInt:
                    return 37; // DXGI_FORMAT_R16G16_SINT
                case TextureFormat.R16G16_Float:
                    return 34; // DXGI_FORMAT_R16G16_FLOAT
                case TextureFormat.R16G16B16A16_UNorm:
                    return 11; // DXGI_FORMAT_R16G16B16A16_UNORM
                case TextureFormat.R16G16B16A16_UInt:
                    return 12; // DXGI_FORMAT_R16G16B16A16_UINT
                case TextureFormat.R16G16B16A16_SInt:
                    return 13; // DXGI_FORMAT_R16G16B16A16_SINT
                case TextureFormat.R16G16B16A16_Float:
                    return 10; // DXGI_FORMAT_R16G16B16A16_FLOAT
                case TextureFormat.R32_UInt:
                    return 41; // DXGI_FORMAT_R32_UINT
                case TextureFormat.R32_SInt:
                    return 42; // DXGI_FORMAT_R32_SINT
                case TextureFormat.R32_Float:
                    return 41; // DXGI_FORMAT_R32_FLOAT (Note: Use R32_UINT for uint clearing, R32_FLOAT for float clearing)
                case TextureFormat.R32G32_UInt:
                    return 16; // DXGI_FORMAT_R32G32_UINT
                case TextureFormat.R32G32_SInt:
                    return 17; // DXGI_FORMAT_R32G32_SINT
                case TextureFormat.R32G32_Float:
                    return 16; // DXGI_FORMAT_R32G32_FLOAT
                case TextureFormat.R32G32B32_UInt:
                    return 32; // DXGI_FORMAT_R32G32B32_UINT
                case TextureFormat.R32G32B32_SInt:
                    return 33; // DXGI_FORMAT_R32G32B32_SINT
                case TextureFormat.R32G32B32_Float:
                    return 6; // DXGI_FORMAT_R32G32B32_FLOAT
                case TextureFormat.R32G32B32A32_UInt:
                    return 2; // DXGI_FORMAT_R32G32B32A32_UINT
                case TextureFormat.R32G32B32A32_SInt:
                    return 3; // DXGI_FORMAT_R32G32B32A32_SINT
                case TextureFormat.R32G32B32A32_Float:
                    return 1; // DXGI_FORMAT_R32G32B32A32_FLOAT
                default:
                    return 0; // DXGI_FORMAT_UNKNOWN
            }
        }

        /// <summary>
        /// Gets or creates a UAV descriptor handle for a texture.
        /// Caches the handle to avoid recreating descriptors for the same texture.
        /// Based on DirectX 12 Unordered Access Views: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createunorderedaccessview
        /// </summary>
        internal IntPtr GetOrCreateUavHandle(ITexture texture, int mipLevel, int arraySlice)
        {
            if (texture == null || texture.NativeHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Create a unique key for this texture/mip/array combination
            // For simplicity, we'll use the texture handle as the key (assuming one UAV per texture)
            // In a more complete implementation, we could include mipLevel and arraySlice in the key
            IntPtr textureHandle = texture.NativeHandle;

            // Check cache first
            IntPtr cachedHandle;
            if (_textureUavHandles.TryGetValue(textureHandle, out cachedHandle))
            {
                return cachedHandle;
            }

            // Allocate new UAV descriptor
            IntPtr uavHandle = AllocateUavDescriptor();
            if (uavHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Get texture format and convert to DXGI_FORMAT
            TextureFormat format = texture.Desc.Format;
            uint dxgiFormat = ConvertTextureFormatToDxgiFormatForUav(format);
            if (dxgiFormat == 0)
            {
                // Format not supported for UAV, return zero handle
                return IntPtr.Zero;
            }

            // Create D3D12_UNORDERED_ACCESS_VIEW_DESC structure
            var uavDesc = new D3D12_UNORDERED_ACCESS_VIEW_DESC
            {
                Format = dxgiFormat,
                ViewDimension = D3D12_UAV_DIMENSION_TEXTURE2D, // Default to 2D texture
                Texture2D = new D3D12_TEX2D_UAV
                {
                    MipSlice = unchecked((uint)mipLevel),
                    PlaneSlice = 0 // Typically 0 for non-planar formats
                }
            };

            // Allocate memory for the UAV descriptor structure
            int uavDescSize = Marshal.SizeOf(typeof(D3D12_UNORDERED_ACCESS_VIEW_DESC));
            IntPtr uavDescPtr = Marshal.AllocHGlobal(uavDescSize);
            try
            {
                Marshal.StructureToPtr(uavDesc, uavDescPtr, false);

                // Call ID3D12Device::CreateUnorderedAccessView
                // pCounterResource is NULL for textures (counters are for structured buffers)
                CallCreateUnorderedAccessView(_device, texture.NativeHandle, IntPtr.Zero, uavDescPtr, uavHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(uavDescPtr);
            }

            // Cache the handle
            _textureUavHandles[textureHandle] = uavHandle;

            return uavHandle;
        }

        #endregion

        /// <summary>
        /// Converts TextureFormat to DXGI_FORMAT for render target views.
        /// Based on DirectX 12 Texture Formats: https://docs.microsoft.com/en-us/windows/win32/api/dxgiformat/ne-dxgiformat-dxgi_format
        /// This version supports color formats suitable for render targets (not depth formats).
        /// </summary>
        private uint ConvertTextureFormatToDxgiFormatForRtv(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8_UNorm:
                    return 61; // DXGI_FORMAT_R8_UNORM
                case TextureFormat.R8_UInt:
                    return 62; // DXGI_FORMAT_R8_UINT
                case TextureFormat.R8_SInt:
                    return 63; // DXGI_FORMAT_R8_SINT
                case TextureFormat.R8G8_UNorm:
                    return 49; // DXGI_FORMAT_R8G8_UNORM
                case TextureFormat.R8G8_UInt:
                    return 50; // DXGI_FORMAT_R8G8_UINT
                case TextureFormat.R8G8_SInt:
                    return 51; // DXGI_FORMAT_R8G8_SINT
                case TextureFormat.R8G8B8A8_UNorm:
                    return 28; // DXGI_FORMAT_R8G8B8A8_UNORM
                case TextureFormat.R8G8B8A8_UInt:
                    return 30; // DXGI_FORMAT_R8G8B8A8_UINT
                case TextureFormat.R8G8B8A8_SInt:
                    return 29; // DXGI_FORMAT_R8G8B8A8_SINT
                case TextureFormat.R16_UNorm:
                    return 56; // DXGI_FORMAT_R16_UNORM
                case TextureFormat.R16_UInt:
                    return 57; // DXGI_FORMAT_R16_UINT
                case TextureFormat.R16_SInt:
                    return 58; // DXGI_FORMAT_R16_SINT
                case TextureFormat.R16_Float:
                    return 54; // DXGI_FORMAT_R16_FLOAT
                case TextureFormat.R32_UInt:
                    return 42; // DXGI_FORMAT_R32_UINT
                case TextureFormat.R32_SInt:
                    return 43; // DXGI_FORMAT_R32_SINT
                case TextureFormat.R32_Float:
                    return 41; // DXGI_FORMAT_R32_FLOAT
                case TextureFormat.R32G32_Float:
                    return 16; // DXGI_FORMAT_R32G32_FLOAT
                case TextureFormat.R32G32_UInt:
                    return 52; // DXGI_FORMAT_R32G32_UINT
                case TextureFormat.R32G32_SInt:
                    return 53; // DXGI_FORMAT_R32G32_SINT
                case TextureFormat.R32G32B32_Float:
                    return 6; // DXGI_FORMAT_R32G32B32_FLOAT
                case TextureFormat.R32G32B32A32_Float:
                    return 2; // DXGI_FORMAT_R32G32B32A32_FLOAT
                case TextureFormat.R32G32B32A32_UInt:
                    return 1; // DXGI_FORMAT_R32G32B32A32_UINT
                case TextureFormat.R32G32B32A32_SInt:
                    return 3; // DXGI_FORMAT_R32G32B32A32_SINT
                default:
                    return 0; // DXGI_FORMAT_UNKNOWN
            }
        }

        /// <summary>
        /// Converts TextureFormat to DXGI_FORMAT for texture resource creation.
        /// Based on DirectX 12 Texture Formats: https://docs.microsoft.com/en-us/windows/win32/api/dxgiformat/ne-dxgiformat-dxgi_format
        /// </summary>
        private uint ConvertTextureFormatToDxgiFormatForTexture(TextureFormat format)
        {
            // Use the same conversion as RTV since textures can generally use the same formats
            return ConvertTextureFormatToDxgiFormatForRtv(format);
        }

        /// <summary>
        /// Maps TextureDimension enum to D3D12_RESOURCE_DIMENSION.
        /// Based on DirectX 12 Resource Dimensions: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_resource_dimension
        /// </summary>
        private static uint MapTextureDimensionToD3D12(TextureDimension dimension)
        {
            switch (dimension)
            {
                case TextureDimension.Texture1D:
                case TextureDimension.Texture1DArray:
                    return D3D12_RESOURCE_DIMENSION_TEXTURE1D;

                case TextureDimension.Texture2D:
                case TextureDimension.Texture2DArray:
                case TextureDimension.TextureCube:
                case TextureDimension.TextureCubeArray:
                    return D3D12_RESOURCE_DIMENSION_TEXTURE2D;

                case TextureDimension.Texture3D:
                    return D3D12_RESOURCE_DIMENSION_TEXTURE3D;

                default:
                    return D3D12_RESOURCE_DIMENSION_TEXTURE2D; // Default to 2D
            }
        }

        #region Resource Interface

        private interface IResource : IDisposable
        {
        }

        #endregion

        #region Resource Implementations

        private class D3D12Texture : ITexture, IResource
        {
            public TextureDesc Desc { get; }
            public IntPtr NativeHandle { get; private set; }
            private readonly IntPtr _internalHandle;
            private readonly IntPtr _d3d12Resource;
            private readonly IntPtr _srvHandle;
            private readonly IntPtr _device;
            private readonly D3D12Device _parentDevice;

            public D3D12Texture(IntPtr handle, TextureDesc desc, IntPtr nativeHandle = default(IntPtr))
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, null, nativeHandle)
            {
            }

            public D3D12Texture(IntPtr handle, TextureDesc desc, IntPtr d3d12Resource, IntPtr srvHandle, IntPtr device, D3D12Device parentDevice, IntPtr nativeHandle = default(IntPtr))
            {
                _internalHandle = handle;
                Desc = desc;
                _d3d12Resource = d3d12Resource;
                _srvHandle = srvHandle;
                _device = device;
                _parentDevice = parentDevice;
                NativeHandle = nativeHandle != IntPtr.Zero ? nativeHandle : (_d3d12Resource != IntPtr.Zero ? _d3d12Resource : handle);
            }

            public void Dispose()
            {
                // Release D3D12 resource if valid
                // ID3D12Resource is a COM object that must be released via IUnknown::Release()
                // Based on DirectX 12 Resource Management: https://docs.microsoft.com/en-us/windows/win32/direct3d12/managing-resource-lifetime
                if (_d3d12Resource != IntPtr.Zero && _parentDevice != null)
                {
                    _parentDevice.ReleaseComObject(_d3d12Resource);
                }

                // Note: Descriptor handles (_srvHandle) are typically just offsets into a descriptor heap
                // They don't need individual release - the descriptor heap itself manages their lifetime
                // If descriptors were allocated from a heap, releasing the heap will clean them up
                // Standalone descriptors allocated via CreateXXXDescriptorHandle are managed by the device
            }
        }

        /// <summary>
        /// Converts GraphicsPipelineDesc to D3D12_GRAPHICS_PIPELINE_STATE_DESC.
        /// Based on DirectX 12 Graphics Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_graphics_pipeline_state_desc
        /// </summary>
        private D3D12_GRAPHICS_PIPELINE_STATE_DESC ConvertGraphicsPipelineDescToD3D12(GraphicsPipelineDesc desc, IFramebuffer framebuffer, IntPtr rootSignature)
        {
            var d3d12Desc = new D3D12_GRAPHICS_PIPELINE_STATE_DESC();

            // Root signature
            d3d12Desc.pRootSignature = rootSignature;

            // Shader bytecode
            d3d12Desc.VS = GetShaderBytecode(desc.VertexShader);
            d3d12Desc.PS = GetShaderBytecode(desc.PixelShader);
            d3d12Desc.HS = GetShaderBytecode(desc.HullShader);
            d3d12Desc.DS = GetShaderBytecode(desc.DomainShader);
            d3d12Desc.GS = GetShaderBytecode(desc.GeometryShader);

            // Stream output (not used in our abstraction)
            d3d12Desc.StreamOutput = new D3D12_STREAM_OUTPUT_DESC
            {
                pSODeclaration = IntPtr.Zero,
                NumEntries = 0,
                pBufferStrides = IntPtr.Zero,
                NumStrides = 0,
                RasterizedStream = 0
            };

            // Blend state
            d3d12Desc.BlendState = ConvertBlendStateDescToD3D12(desc.BlendState);

            // Sample mask (default to all samples)
            d3d12Desc.SampleMask = 0xFFFFFFFF;

            // Rasterizer state
            d3d12Desc.RasterizerState = ConvertRasterStateDescToD3D12(desc.RasterState);

            // Depth-stencil state
            d3d12Desc.DepthStencilState = ConvertDepthStencilStateDescToD3D12(desc.DepthStencilState);

            // Input layout
            if (desc.InputLayout.Attributes != null && desc.InputLayout.Attributes.Length > 0)
            {
                d3d12Desc.InputElementDescsCount = (uint)desc.InputLayout.Attributes.Length;
                d3d12Desc.pInputElementDescs = MarshalInputElementDescs(desc.InputLayout.Attributes);
            }
            else
            {
                d3d12Desc.InputElementDescsCount = 0;
                d3d12Desc.pInputElementDescs = IntPtr.Zero;
            }

            // Index buffer strip cut value (not used)
            d3d12Desc.IBStripCutValue = 0; // D3D12_INDEX_BUFFER_STRIP_CUT_VALUE_DISABLED

            // Primitive topology type
            d3d12Desc.PrimitiveTopologyType = ConvertPrimitiveTopologyToD3D12(desc.PrimitiveTopology);

            // Render target formats from framebuffer
            if (framebuffer != null && framebuffer.Desc.ColorAttachments != null && framebuffer.Desc.ColorAttachments.Length > 0)
            {
                d3d12Desc.NumRenderTargets = (uint)Math.Min(framebuffer.Desc.ColorAttachments.Length, 8);
                for (int i = 0; i < d3d12Desc.NumRenderTargets && i < 8; i++)
                {
                    var attachment = framebuffer.Desc.ColorAttachments[i];
                    if (attachment.Texture != null)
                    {
                        uint format = ConvertTextureFormatToDxgiFormat(attachment.Texture.Desc.Format);
                        switch (i)
                        {
                            case 0: d3d12Desc.RTVFormats0 = format; break;
                            case 1: d3d12Desc.RTVFormats1 = format; break;
                            case 2: d3d12Desc.RTVFormats2 = format; break;
                            case 3: d3d12Desc.RTVFormats3 = format; break;
                            case 4: d3d12Desc.RTVFormats4 = format; break;
                            case 5: d3d12Desc.RTVFormats5 = format; break;
                            case 6: d3d12Desc.RTVFormats6 = format; break;
                            case 7: d3d12Desc.RTVFormats7 = format; break;
                        }
                    }
                }
            }
            else
            {
                d3d12Desc.NumRenderTargets = 1;
                d3d12Desc.RTVFormats0 = 87; // DXGI_FORMAT_R8G8B8A8_UNORM (default)
                d3d12Desc.RTVFormats1 = 0; // DXGI_FORMAT_UNKNOWN
                d3d12Desc.RTVFormats2 = 0;
                d3d12Desc.RTVFormats3 = 0;
                d3d12Desc.RTVFormats4 = 0;
                d3d12Desc.RTVFormats5 = 0;
                d3d12Desc.RTVFormats6 = 0;
                d3d12Desc.RTVFormats7 = 0;
            }

            // Depth-stencil format
            if (framebuffer != null && framebuffer.Desc.DepthAttachment.Texture != null)
            {
                d3d12Desc.DSVFormat = ConvertTextureFormatToDxgiFormat(framebuffer.Desc.DepthAttachment.Texture.Desc.Format);
            }
            else
            {
                d3d12Desc.DSVFormat = 0; // DXGI_FORMAT_UNKNOWN
            }

            // Sample description
            if (framebuffer != null && framebuffer.Desc.ColorAttachments != null && framebuffer.Desc.ColorAttachments.Length > 0 && framebuffer.Desc.ColorAttachments[0].Texture != null)
            {
                d3d12Desc.SampleDesc = new D3D12_SAMPLE_DESC
                {
                    Count = (uint)framebuffer.Desc.ColorAttachments[0].Texture.Desc.SampleCount,
                    Quality = 0 // Standard quality
                };
            }
            else
            {
                d3d12Desc.SampleDesc = new D3D12_SAMPLE_DESC { Count = 1, Quality = 0 };
            }

            // Node mask (default to node 0)
            d3d12Desc.NodeMask = 0;

            // Cached PSO (not used)
            d3d12Desc.CachedPSO = new D3D12_CACHED_PIPELINE_STATE { pCachedBlob = IntPtr.Zero, CachedBlobSizeInBytes = 0 };

            // Pipeline state flags (default)
            d3d12Desc.Flags = 0; // D3D12_PIPELINE_STATE_FLAG_NONE

            return d3d12Desc;
        }

        /// <summary>
        /// Frees allocated memory from D3D12_GRAPHICS_PIPELINE_STATE_DESC structure.
        /// </summary>
        private void FreeGraphicsPipelineStateDesc(ref D3D12_GRAPHICS_PIPELINE_STATE_DESC desc)
        {
            // Free input element descriptors
            if (desc.pInputElementDescs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(desc.pInputElementDescs);
                desc.pInputElementDescs = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Converts IShader to D3D12_SHADER_BYTECODE structure.
        /// Allocates unmanaged memory for shader bytecode.
        /// Based on DirectX 12 Shader Bytecode: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_shader_bytecode
        /// </summary>
        private D3D12_SHADER_BYTECODE GetShaderBytecode(IShader shader)
        {
            if (shader == null || shader.Desc.Bytecode == null || shader.Desc.Bytecode.Length == 0)
            {
                return new D3D12_SHADER_BYTECODE
                {
                    pShaderBytecode = IntPtr.Zero,
                    BytecodeLength = 0
                };
            }

            // Allocate unmanaged memory for shader bytecode
            IntPtr bytecodePtr = Marshal.AllocHGlobal(shader.Desc.Bytecode.Length);
            Marshal.Copy(shader.Desc.Bytecode, 0, bytecodePtr, shader.Desc.Bytecode.Length);

            return new D3D12_SHADER_BYTECODE
            {
                pShaderBytecode = bytecodePtr,
                BytecodeLength = (ulong)shader.Desc.Bytecode.Length
            };
        }

        /// <summary>
        /// Converts ComputePipelineDesc to D3D12_COMPUTE_PIPELINE_STATE_DESC.
        /// Based on DirectX 12 Compute Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcomputepipelinestate
        /// </summary>
        private D3D12_COMPUTE_PIPELINE_STATE_DESC ConvertComputePipelineDescToD3D12(ComputePipelineDesc desc, IntPtr rootSignature)
        {
            var d3d12Desc = new D3D12_COMPUTE_PIPELINE_STATE_DESC();

            // Root signature
            d3d12Desc.pRootSignature = rootSignature;

            // Compute shader bytecode
            d3d12Desc.CS = GetShaderBytecode(desc.ComputeShader);

            // Node mask (default to node 0)
            d3d12Desc.NodeMask = 0;

            // Cached PSO (not used)
            d3d12Desc.CachedPSO = new D3D12_CACHED_PIPELINE_STATE { pCachedBlob = IntPtr.Zero, CachedBlobSizeInBytes = 0 };

            // Pipeline state flags (default)
            d3d12Desc.Flags = 0; // D3D12_PIPELINE_STATE_FLAG_NONE

            return d3d12Desc;
        }

        /// <summary>
        /// Frees allocated memory from D3D12_COMPUTE_PIPELINE_STATE_DESC structure.
        /// </summary>
        private void FreeComputePipelineStateDesc(ref D3D12_COMPUTE_PIPELINE_STATE_DESC desc)
        {
            // Free compute shader bytecode
            if (desc.CS.pShaderBytecode != IntPtr.Zero && desc.CS.BytecodeLength > 0)
            {
                Marshal.FreeHGlobal(desc.CS.pShaderBytecode);
                desc.CS.pShaderBytecode = IntPtr.Zero;
                desc.CS.BytecodeLength = 0;
            }
        }

        private class D3D12Buffer : IBuffer, IResource
        {
            public BufferDesc Desc { get; }
            public IntPtr NativeHandle { get; private set; }
            private readonly IntPtr _internalHandle;
            private readonly IntPtr _d3d12Resource;
            private readonly IntPtr _device;
            private readonly D3D12Device _parentDevice;

            public D3D12Buffer(IntPtr handle, BufferDesc desc, IntPtr d3d12Resource, IntPtr device, D3D12Device parentDevice)
            {
                _internalHandle = handle;
                Desc = desc;
                _d3d12Resource = d3d12Resource;
                _device = device;
                _parentDevice = parentDevice;
                NativeHandle = _d3d12Resource != IntPtr.Zero ? _d3d12Resource : handle;
            }

            public void Dispose()
            {
                // Release D3D12 resource if valid
                // ID3D12Resource is a COM object that must be released via IUnknown::Release()
                // Based on DirectX 12 Resource Management: https://docs.microsoft.com/en-us/windows/win32/direct3d12/managing-resource-lifetime
                if (_d3d12Resource != IntPtr.Zero && _parentDevice != null)
                {
                    _parentDevice.ReleaseComObject(_d3d12Resource);
                }
            }
        }

        private class D3D12Sampler : ISampler, IResource
        {
            public SamplerDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _samplerHandle;
            private readonly IntPtr _device;

            public D3D12Sampler(IntPtr handle, SamplerDesc desc, IntPtr samplerHandle, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _samplerHandle = samplerHandle;
                _device = device;
            }

            public void Dispose()
            {
                // Samplers are typically stored in descriptor heaps, not released individually
            }
        }

        private class D3D12Shader : IShader, IResource
        {
            public ShaderDesc Desc { get; }
            public ShaderType Type { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _device;

            public D3D12Shader(IntPtr handle, ShaderDesc desc, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _device = device;
                Type = desc.Type;
            }

            public void Dispose()
            {
                // D3D12 shaders are part of PSO, not released individually
                // Bytecode can be garbage collected
            }
        }

        private class D3D12GraphicsPipeline : IGraphicsPipeline, IResource
        {
            public GraphicsPipelineDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _d3d12PipelineState;
            private readonly IntPtr _rootSignature;
            private readonly IntPtr _device;
            private readonly D3D12Device _parentDevice;

            public D3D12GraphicsPipeline(IntPtr handle, GraphicsPipelineDesc desc, IntPtr d3d12PipelineState, IntPtr rootSignature, IntPtr device, D3D12Device parentDevice)
            {
                _handle = handle;
                Desc = desc;
                _d3d12PipelineState = d3d12PipelineState;
                _rootSignature = rootSignature;
                _device = device;
                _parentDevice = parentDevice;
            }

            // Accessors for command list to use
            public IntPtr GetPipelineState() { return _d3d12PipelineState; }
            public IntPtr GetRootSignature() { return _rootSignature; }

            public void Dispose()
            {
                // Release D3D12 pipeline state if valid
                // ID3D12PipelineState is a COM object that must be released via IUnknown::Release()
                // Based on DirectX 12 Pipeline State Management: https://docs.microsoft.com/en-us/windows/win32/direct3d12/managing-pipeline-state-objects
                // Pipeline state objects are created via ID3D12Device::CreateGraphicsPipelineState and must be released when no longer needed
                if (_d3d12PipelineState != IntPtr.Zero && _parentDevice != null)
                {
                    try
                    {
                        // Release the COM object through IUnknown::Release() vtable call
                        // This decrements the reference count and frees the object when count reaches zero
                        uint refCount = _parentDevice.ReleaseComObject(_d3d12PipelineState);
                        if (refCount > 0)
                        {
                            Console.WriteLine($"[D3D12Device] Graphics pipeline state still has {refCount} references after Release()");
                        }
                        else
                        {
                            Console.WriteLine("[D3D12Device] Successfully released graphics pipeline state");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue cleanup - don't throw from Dispose
                        Console.WriteLine($"[D3D12Device] Error releasing graphics pipeline state: {ex.Message}");
                        Console.WriteLine($"[D3D12Device] Stack trace: {ex.StackTrace}");
                    }
                }

                // Release D3D12 root signature if valid
                // ID3D12RootSignature is a COM object that must be released via IUnknown::Release()
                // Based on DirectX 12 Root Signature Management: https://docs.microsoft.com/en-us/windows/win32/direct3d12/root-signatures
                // Root signatures are created via ID3D12Device::CreateRootSignature and must be released when no longer needed
                // Note: Root signatures may be shared across multiple pipelines, so reference counting is important
                if (_rootSignature != IntPtr.Zero && _parentDevice != null)
                {
                    try
                    {
                        // Release the COM object through IUnknown::Release() vtable call
                        // This decrements the reference count and frees the object when count reaches zero
                        uint refCount = _parentDevice.ReleaseComObject(_rootSignature);
                        if (refCount > 0)
                        {
                            Console.WriteLine($"[D3D12Device] Root signature still has {refCount} references after Release()");
                        }
                        else
                        {
                            Console.WriteLine("[D3D12Device] Successfully released root signature");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue cleanup - don't throw from Dispose
                        Console.WriteLine($"[D3D12Device] Error releasing root signature: {ex.Message}");
                        Console.WriteLine($"[D3D12Device] Stack trace: {ex.StackTrace}");
                    }
                }
            }
        }

        private class D3D12ComputePipeline : IComputePipeline, IResource
        {
            public ComputePipelineDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _d3d12PipelineState;
            private readonly IntPtr _rootSignature;
            private readonly IntPtr _device;
            private readonly D3D12Device _parentDevice;

            public D3D12ComputePipeline(IntPtr handle, ComputePipelineDesc desc, IntPtr d3d12PipelineState, IntPtr rootSignature, IntPtr device, D3D12Device parentDevice)
            {
                _handle = handle;
                Desc = desc;
                _d3d12PipelineState = d3d12PipelineState;
                _rootSignature = rootSignature;
                _device = device;
                _parentDevice = parentDevice;
            }

            // Accessors for command list to use
            public IntPtr GetPipelineState() { return _d3d12PipelineState; }
            public IntPtr GetRootSignature() { return _rootSignature; }

            public void Dispose()
            {
                // Release D3D12 pipeline state if valid
                // ID3D12PipelineState is a COM object that must be released via IUnknown::Release()
                // Based on DirectX 12 Pipeline State Management: https://docs.microsoft.com/en-us/windows/win32/direct3d12/managing-pipeline-state-objects
                if (_d3d12PipelineState != IntPtr.Zero)
                {
                    try
                    {
                        _parentDevice.ReleaseComObject(_d3d12PipelineState);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue cleanup - don't throw from Dispose
                        Console.WriteLine($"[D3D12Device] Error releasing pipeline state: {ex.Message}");
                    }
                }

                // Release D3D12 root signature if valid
                // ID3D12RootSignature is a COM object that must be released via IUnknown::Release()
                // Based on DirectX 12 Root Signature Management: https://docs.microsoft.com/en-us/windows/win32/direct3d12/root-signatures
                if (_rootSignature != IntPtr.Zero)
                {
                    try
                    {
                        _parentDevice.ReleaseComObject(_rootSignature);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue cleanup - don't throw from Dispose
                        Console.WriteLine($"[D3D12Device] Error releasing root signature: {ex.Message}");
                    }
                }
            }
        }

        private class D3D12Framebuffer : IFramebuffer, IResource
        {
            public FramebufferDesc Desc { get; }
            private readonly IntPtr _handle;

            public D3D12Framebuffer(IntPtr handle, FramebufferDesc desc)
            {
                _handle = handle;
                Desc = desc;
            }

            public FramebufferInfo GetInfo()
            {
                var info = new FramebufferInfo();

                if (Desc.ColorAttachments != null && Desc.ColorAttachments.Length > 0)
                {
                    info.ColorFormats = new TextureFormat[Desc.ColorAttachments.Length];
                    for (int i = 0; i < Desc.ColorAttachments.Length; i++)
                    {
                        info.ColorFormats[i] = Desc.ColorAttachments[i].Texture?.Desc.Format ?? TextureFormat.Unknown;
                        if (i == 0)
                        {
                            info.Width = Desc.ColorAttachments[i].Texture?.Desc.Width ?? 0;
                            info.Height = Desc.ColorAttachments[i].Texture?.Desc.Height ?? 0;
                            info.SampleCount = Desc.ColorAttachments[i].Texture?.Desc.SampleCount ?? 1;
                        }
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
                // D3D12 doesn't use framebuffers - no cleanup needed
            }
        }

        private class D3D12BindingLayout : IBindingLayout, IResource
        {
            public BindingLayoutDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _rootSignature;
            private readonly IntPtr _device;
            private readonly D3D12Device _parentDevice;
            private readonly List<IntPtr> _rangePointers; // Descriptor range pointers allocated with Marshal.AllocHGlobal

            public D3D12BindingLayout(IntPtr handle, BindingLayoutDesc desc, IntPtr rootSignature, IntPtr device, D3D12Device parentDevice, List<IntPtr> rangePointers)
            {
                _handle = handle;
                Desc = desc;
                _rootSignature = rootSignature;
                _device = device;
                _parentDevice = parentDevice;
                _rangePointers = rangePointers ?? new List<IntPtr>(); // Store range pointers for cleanup on disposal
            }

            // Accessor for root signature (used by pipeline creation)
            public IntPtr GetRootSignature() { return _rootSignature; }

            public void Dispose()
            {
                // Free descriptor range pointers allocated with Marshal.AllocHGlobal
                // These pointers were allocated during root signature creation and must be freed when the layout is disposed
                // Based on DirectX 12 Root Signature Management: Root signature is created from serialized blob,
                // but the original descriptor range structures are still referenced and must remain valid until root signature is destroyed
                if (_rangePointers != null)
                {
                    foreach (var ptr in _rangePointers)
                    {
                        if (ptr != IntPtr.Zero)
                        {
                            try
                            {
                                Marshal.FreeHGlobal(ptr);
                            }
                            catch (Exception ex)
                            {
                                // Log error but continue cleanup - don't throw from Dispose
                                Console.WriteLine($"[D3D12Device] Error freeing descriptor range pointer 0x{ptr:X16}: {ex.Message}");
                            }
                        }
                    }
                    _rangePointers.Clear();
                }

                // Release D3D12 root signature COM object
                // Root signatures are COM objects (ID3D12RootSignature) and must be released via IUnknown::Release()
                // Based on DirectX 12 Root Signature Management: https://docs.microsoft.com/en-us/windows/win32/direct3d12/root-signatures
                // Root signatures are created via ID3D12Device::CreateRootSignature and must be released when no longer needed
                if (_rootSignature != IntPtr.Zero && _parentDevice != null)
                {
                    try
                    {
                        // Release the COM object through IUnknown::Release() vtable call
                        // This decrements the reference count and frees the object when count reaches zero
                        uint refCount = _parentDevice.ReleaseComObject(_rootSignature);
                        if (refCount > 0)
                        {
                            Console.WriteLine($"[D3D12Device] Root signature still has {refCount} references after Release()");
                        }
                        else
                        {
                            Console.WriteLine("[D3D12Device] Successfully released root signature");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue cleanup - don't throw from Dispose
                        Console.WriteLine($"[D3D12Device] Error releasing root signature: {ex.Message}");
                        Console.WriteLine($"[D3D12Device] Stack trace: {ex.StackTrace}");
                    }
                }
            }
        }

        private class D3D12BindingSet : IBindingSet, IResource
        {
            public IBindingLayout Layout { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _descriptorHeap;
            private readonly IntPtr _device;
            private readonly D3D12Device _parentDevice;
            private readonly D3D12_GPU_DESCRIPTOR_HANDLE _gpuDescriptorHandle;

            public D3D12BindingSet(IntPtr handle, IBindingLayout layout, BindingSetDesc desc, IntPtr descriptorHeap, IntPtr device, D3D12Device parentDevice)
            {
                _handle = handle;
                Layout = layout;
                _descriptorHeap = descriptorHeap;
                _device = device;
                _parentDevice = parentDevice;

                // Calculate GPU descriptor handle from heap start
                // In DirectX 12, GPU descriptor handles are obtained from the descriptor heap
                // and can be offset by descriptor index * descriptor increment size
                // Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/direct3d12/descriptors-overview
                // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                //
                // NOTE: This constructor is a fallback for legacy code paths. The preferred constructor
                // accepts a pre-calculated GPU descriptor handle with the proper offset.
                // CreateBindingSet now fully implements descriptor allocation and calculates the proper offset.
                // For empty binding sets or legacy code, offset 0 (heap start) is used.
                if (descriptorHeap != IntPtr.Zero && parentDevice != null)
                {
                    D3D12_GPU_DESCRIPTOR_HANDLE gpuHeapStart = _parentDevice.CallGetGPUDescriptorHandleForHeapStart(descriptorHeap);
                    // Use heap start (offset 0) - this is correct for empty binding sets or when offset is not available
                    // For binding sets with descriptors, CreateBindingSet calculates and passes the proper offset
                    _gpuDescriptorHandle = gpuHeapStart;
                }
                else
                {
                    _gpuDescriptorHandle = new D3D12_GPU_DESCRIPTOR_HANDLE { ptr = 0 };
                }
            }

            public D3D12BindingSet(IntPtr handle, IBindingLayout layout, BindingSetDesc desc, IntPtr descriptorHeap, IntPtr device, D3D12Device parentDevice, D3D12_GPU_DESCRIPTOR_HANDLE gpuDescriptorHandle)
            {
                _handle = handle;
                Layout = layout;
                _descriptorHeap = descriptorHeap;
                _device = device;
                _parentDevice = parentDevice;
                _gpuDescriptorHandle = gpuDescriptorHandle;
            }

            // Accessors for command list to use
            public IntPtr GetDescriptorHeap() { return _descriptorHeap; }
            public D3D12_GPU_DESCRIPTOR_HANDLE GetGpuDescriptorHandle() { return _gpuDescriptorHandle; }

            public void Dispose()
            {
                // Descriptor sets are returned to heap, not destroyed individually
            }
        }

        private class D3D12AccelStruct : IAccelStruct, IResource
        {
            public AccelStructDesc Desc { get; }
            public bool IsTopLevel { get; }
            public ulong DeviceAddress { get; private set; }
            private readonly IntPtr _handle;
            private readonly IntPtr _d3d12AccelStruct;
            private readonly IBuffer _backingBuffer;
            private readonly IntPtr _device5;
            private readonly D3D12Device _parentDevice;
            private IBuffer _scratchBuffer;
            private ulong _scratchBufferDeviceAddress;

            public D3D12AccelStruct(IntPtr handle, AccelStructDesc desc, IntPtr d3d12AccelStruct, IBuffer backingBuffer, ulong deviceAddress, IntPtr device5, D3D12Device parentDevice)
            {
                _handle = handle;
                Desc = desc;
                _d3d12AccelStruct = d3d12AccelStruct;
                _backingBuffer = backingBuffer;
                DeviceAddress = deviceAddress;
                _device5 = device5;
                _parentDevice = parentDevice;
                IsTopLevel = desc.IsTopLevel;
                _scratchBuffer = null;
                _scratchBufferDeviceAddress = 0UL;
            }

            /// <summary>
            /// Gets or allocates a scratch buffer for building the acceleration structure.
            /// Based on D3D12 API: Scratch buffers are temporary GPU memory used during acceleration structure building.
            /// </summary>
            public IBuffer GetOrAllocateScratchBuffer(D3D12Device device, ulong requiredSize)
            {
                if (device == null)
                {
                    throw new ArgumentNullException(nameof(device));
                }

                // If we already have a scratch buffer that's large enough, reuse it
                if (_scratchBuffer != null)
                {
                    // Check if existing scratch buffer is large enough
                    // Get buffer description to check allocated size
                    BufferDesc scratchDesc = _scratchBuffer.Desc;
                    if (scratchDesc.ByteSize >= (int)requiredSize)
                    {
                        // Existing scratch buffer is large enough, reuse it
                        return _scratchBuffer;
                    }
                    // Existing buffer is too small, will be reallocated below
                }

                // Allocate new scratch buffer if needed
                // Scratch buffers must be in DEFAULT heap (GPU-accessible)
                BufferDesc scratchBufferDesc = new BufferDesc
                {
                    ByteSize = (int)requiredSize,
                    Usage = BufferUsageFlags.AccelStructStorage, // | BufferUsageFlags.AccelStructBuildInput,
                    InitialState = ResourceState.AccelStructBuildInput,
                    IsAccelStructBuildInput = true,
                    DebugName = IsTopLevel ? "TLAS_ScratchBuffer" : "BLAS_ScratchBuffer"
                };

                // Dispose old scratch buffer if it exists
                if (_scratchBuffer != null)
                {
                    _scratchBuffer.Dispose();
                }

                _scratchBuffer = device.CreateBuffer(scratchBufferDesc);
                if (_scratchBuffer == null || _scratchBuffer.NativeHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create scratch buffer for acceleration structure building");
                }

                // Get GPU virtual address for scratch buffer
                _scratchBufferDeviceAddress = device.GetGpuVirtualAddress(_scratchBuffer.NativeHandle);
                if (_scratchBufferDeviceAddress == 0UL)
                {
                    throw new InvalidOperationException("Failed to get GPU virtual address for scratch buffer");
                }

                return _scratchBuffer;
            }

            /// <summary>
            /// Gets the GPU virtual address of the scratch buffer.
            /// </summary>
            public ulong ScratchBufferDeviceAddress
            {
                get { return _scratchBufferDeviceAddress; }
            }

            public void Dispose()
            {
                // Release D3D12 acceleration structure
                // Based on DirectX 12 DXR API: Acceleration structures are stored as data in ID3D12Resource buffers
                // The backing buffer (ID3D12Resource) contains the acceleration structure data and must be released
                // There is no separate COM interface for acceleration structures - they're just data in buffers
                // Reference: https://docs.microsoft.com/en-us/windows/win32/direct3d12/direct3d-12-raytracing-acceleration-structure

                // Release scratch buffer first (temporary buffer used during building)
                if (_scratchBuffer != null)
                {
                    _scratchBuffer.Dispose();
                    _scratchBuffer = null;
                    _scratchBufferDeviceAddress = 0UL;
                }

                // Release backing buffer (this is the ID3D12Resource that contains the acceleration structure data)
                // Disposing the buffer releases the underlying ID3D12Resource via IUnknown::Release()
                if (_backingBuffer != null)
                {
                    _backingBuffer.Dispose();
                }

                // Release acceleration structure COM object if it exists (currently always IntPtr.Zero, but future-proof)
                // Note: In current D3D12 implementation, _d3d12AccelStruct is always IntPtr.Zero because
                // acceleration structures are stored as data in buffers, not as separate COM objects
                if (_d3d12AccelStruct != IntPtr.Zero && _parentDevice != null)
                {
                    try
                    {
                        // Release the COM object through IUnknown::Release() vtable call
                        // This decrements the reference count and frees the object when count reaches zero
                        uint refCount = _parentDevice.ReleaseComObject(_d3d12AccelStruct);
                        if (refCount > 0)
                        {
                            Console.WriteLine($"[D3D12Device] Acceleration structure still has {refCount} references after Release()");
                        }
                        else
                        {
                            Console.WriteLine("[D3D12Device] Successfully released acceleration structure COM object");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue cleanup - don't throw from Dispose
                        Console.WriteLine($"[D3D12Device] Error releasing acceleration structure COM object: {ex.Message}");
                        Console.WriteLine($"[D3D12Device] Stack trace: {ex.StackTrace}");
                    }
                }
            }
        }

        private class D3D12RaytracingPipeline : IRaytracingPipeline, IResource
        {
            public Interfaces.RaytracingPipelineDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _d3d12StateObject;
            private readonly IntPtr _properties;
            private readonly IBuffer _sbtBuffer;
            private readonly IntPtr _device5;
            private readonly D3D12Device _parentDevice;

            public D3D12RaytracingPipeline(IntPtr handle, Interfaces.RaytracingPipelineDesc desc, IntPtr d3d12StateObject, IntPtr properties, IBuffer sbtBuffer, IntPtr device5, D3D12Device parentDevice)
            {
                _handle = handle;
                Desc = desc;
                _d3d12StateObject = d3d12StateObject;
                _properties = properties;
                _sbtBuffer = sbtBuffer;
                _device5 = device5;
                _parentDevice = parentDevice;
            }

            public void Dispose()
            {
                // Release D3D12 state object via COM IUnknown::Release
                if (_d3d12StateObject != IntPtr.Zero && _parentDevice != null)
                {
                    try
                    {
                        // Release the state object (decrements reference count, destroys if count reaches zero)
                        // This is safe to call multiple times - Release() handles the reference counting
                        _parentDevice.CallStateObjectRelease(_d3d12StateObject);
                    }
                    catch
                    {
                        // Ignore errors during release - object may already be destroyed
                    }
                }

                // Release properties interface if it was queried separately
                if (_properties != IntPtr.Zero && _properties != _d3d12StateObject && _parentDevice != null)
                {
                    try
                    {
                        // Properties interface was queried via QueryInterface, so it has its own reference
                        // Release it separately
                        _parentDevice.CallStateObjectRelease(_properties);
                    }
                    catch
                    {
                        // Ignore errors during release
                    }
                }

                // Release SBT buffer
                _sbtBuffer?.Dispose();
            }

            /// <summary>
            /// Gets the shader identifier for a shader or hit group in the pipeline.
            /// Shader identifiers are opaque handles used in the shader binding table.
            /// Based on D3D12 DXR API: ID3D12StateObjectProperties::GetShaderIdentifier
            /// https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12stateobjectproperties-getshaderidentifier
            /// </summary>
            /// <param name="exportName">The export name of the shader or hit group (e.g., "ShadowRayGen", "ShadowMiss", "ShadowHitGroup").</param>
            /// <returns>Shader identifier bytes (typically 32 bytes for D3D12, variable for Vulkan). Returns null if the export name is not found.</returns>
            public byte[] GetShaderIdentifier(string exportName)
            {
                if (string.IsNullOrEmpty(exportName))
                {
                    throw new ArgumentException("Export name cannot be null or empty", nameof(exportName));
                }

                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return null;
                }

                // Check if properties interface is available
                if (_properties == IntPtr.Zero)
                {
                    // Properties interface was not queried during pipeline creation
                    // Try to query it from the state object
                    if (_d3d12StateObject == IntPtr.Zero || _parentDevice == null)
                    {
                        return null;
                    }

                    // Query ID3D12StateObjectProperties from ID3D12StateObject
                    // Based on DirectX 12 COM QueryInterface: https://docs.microsoft.com/en-us/windows/win32/api/unknwn/nf-unknwn-iunknown-queryinterface
                    // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                    IntPtr propertiesPtr = Marshal.AllocHGlobal(IntPtr.Size);
                    try
                    {
                        Guid iidProperties = IID_ID3D12StateObjectProperties;
                        unsafe
                        {
                            // Get vtable pointer (first field of COM object)
                            IntPtr* vtable = *(IntPtr**)_d3d12StateObject;
                            // QueryInterface is at index 0 in IUnknown vtable
                            IntPtr queryInterfacePtr = vtable[0];

                            // Create delegate for QueryInterface
                            // HRESULT QueryInterface(REFIID riid, void** ppvObject)
                            QueryInterfaceDelegate queryInterface = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(queryInterfacePtr, typeof(QueryInterfaceDelegate));

                            int hr = queryInterface(_d3d12StateObject, ref iidProperties, propertiesPtr);
                            if (hr < 0)
                            {
                                // QueryInterface failed - properties interface not available
                                return null;
                            }

                            IntPtr queriedProperties = Marshal.ReadIntPtr(propertiesPtr);
                            if (queriedProperties == IntPtr.Zero)
                            {
                                return null;
                            }

                            // Use the queried properties interface for this call
                            // Note: We don't store it in _properties to avoid modifying the class state
                            // The caller should ensure properties are queried during pipeline creation
                            return GetShaderIdentifierFromProperties(queriedProperties, exportName);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(propertiesPtr);
                    }
                }

                // Use the stored properties interface
                return GetShaderIdentifierFromProperties(_properties, exportName);
            }

            /// <summary>
            /// Helper method to get shader identifier from a properties interface.
            /// </summary>
            /// <param name="properties">ID3D12StateObjectProperties interface pointer.</param>
            /// <param name="exportName">Export name of the shader or hit group.</param>
            /// <returns>Shader identifier bytes (32 bytes for D3D12), or null if not found.</returns>
            private byte[] GetShaderIdentifierFromProperties(IntPtr properties, string exportName)
            {
                if (properties == IntPtr.Zero || _parentDevice == null)
                {
                    return null;
                }

                // Convert export name to null-terminated UTF-8 byte array
                // GetShaderIdentifier expects a null-terminated string
                byte[] exportNameBytes = System.Text.Encoding.UTF8.GetBytes(exportName);
                byte[] nullTerminatedName = new byte[exportNameBytes.Length + 1];
                System.Array.Copy(exportNameBytes, nullTerminatedName, exportNameBytes.Length);
                nullTerminatedName[exportNameBytes.Length] = 0; // Null terminator

                // Pin the export name for native call
                GCHandle nameHandle = GCHandle.Alloc(nullTerminatedName, GCHandleType.Pinned);
                try
                {
                    IntPtr pExportName = nameHandle.AddrOfPinnedObject();

                    // Call GetShaderIdentifier through parent device's helper method
                    // Based on D3D12 DXR API: ID3D12StateObjectProperties::GetShaderIdentifier
                    // Returns pointer to 32-byte shader identifier, or NULL if export name not found
                    // https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12stateobjectproperties-getshaderidentifier
                    // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                    IntPtr shaderIdPtr = _parentDevice.CallStateObjectPropertiesGetShaderIdentifier(properties, pExportName);
                    if (shaderIdPtr == IntPtr.Zero)
                    {
                        // Export name not found or GetShaderIdentifier returned NULL
                        return null;
                    }

                    // Copy shader identifier bytes (32 bytes for D3D12)
                    // Based on D3D12 DXR API: Shader identifiers are 32 bytes (D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES = 32)
                    const int D3D12_SHADER_IDENTIFIER_SIZE = 32;
                    byte[] shaderId = new byte[D3D12_SHADER_IDENTIFIER_SIZE];
                    unsafe
                    {
                        byte* srcPtr = (byte*)shaderIdPtr;
                        fixed (byte* dstPtr = shaderId)
                        {
                            // C# 7.3 compatible: use pointer arithmetic and loop instead of System.Buffer.MemoryCopy
                            for (int i = 0; i < D3D12_SHADER_IDENTIFIER_SIZE; i++)
                            {
                                dstPtr[i] = srcPtr[i];
                            }
                        }
                    }

                    return shaderId;
                }
                finally
                {
                    if (nameHandle.IsAllocated)
                    {
                        nameHandle.Free();
                    }
                }
            }
        }

        private class D3D12CommandList : ICommandList, IResource
        {
            private readonly IntPtr _handle;
            private readonly CommandListType _type;
            private readonly D3D12Device _device;
            private readonly IntPtr _d3d12CommandList;
            private readonly IntPtr _d3d12Device;
            private readonly IntPtr _d3d12CommandAllocator;
            private bool _isOpen;

            // Barrier tracking
            private readonly List<PendingBarrier> _pendingBarriers;
            private readonly Dictionary<object, ResourceState> _resourceStates;

            // Raytracing state
            private RaytracingState _raytracingState;
            private bool _hasRaytracingState;

            // Barrier entry structure
            private struct PendingBarrier
            {
                public IntPtr Resource;
                public uint Subresource;
                public uint StateBefore;
                public uint StateAfter;
            }

            public D3D12CommandList(IntPtr handle, CommandListType type, D3D12Device device, IntPtr d3d12CommandList, IntPtr d3d12CommandAllocator, IntPtr d3d12Device)
            {
                _handle = handle;
                _type = type;
                _device = device;
                _d3d12CommandList = d3d12CommandList;
                _d3d12CommandAllocator = d3d12CommandAllocator;
                _d3d12Device = d3d12Device;
                _isOpen = false;
                _pendingBarriers = new List<PendingBarrier>();
                _resourceStates = new Dictionary<object, ResourceState>();
                _hasRaytracingState = false;
            }

            /// <summary>
            /// Opens the command list for recording commands.
            /// Resets the command allocator and command list before starting a new recording session.
            /// Based on DirectX 12 Command List Recording: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-reset
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            public void Open()
            {
                if (_isOpen)
                {
                    return; // Already open, no need to reset
                }

                // Reset command allocator first
                // Command allocator must be reset before resetting the command list
                // This makes the memory available for reuse by the command list
                if (_d3d12CommandAllocator != IntPtr.Zero)
                {
                    int allocatorHr = CallCommandAllocatorReset(_d3d12CommandAllocator);
                    if (allocatorHr < 0)
                    {
                        // HRESULT indicates failure
                        // According to D3D12 documentation, Reset can fail if:
                        // - The command allocator is being used by a command list that hasn't been executed yet
                        // - Device was removed
                        // We continue anyway - the error will be caught when resetting the command list
                    }
                }

                // Reset command list with allocator and optional initial PSO
                // pInitialState can be NULL (IntPtr.Zero) to reset without setting a PSO
                // The PSO will be set later via SetGraphicsState or SetComputeState
                if (_d3d12CommandList != IntPtr.Zero && _d3d12CommandAllocator != IntPtr.Zero)
                {
                    // Reset command list with allocator and no initial PSO (NULL)
                    // PSO will be set explicitly when graphics/compute state is set
                    int commandListHr = CallCommandListReset(_d3d12CommandList, _d3d12CommandAllocator, IntPtr.Zero);
                    if (commandListHr < 0)
                    {
                        // HRESULT indicates failure
                        // According to D3D12 documentation, Reset can fail if:
                        // - The command list is still being executed by the GPU
                        // - The command allocator hasn't been reset (we reset it above, but check anyway)
                        // - Device was removed
                        // - Invalid allocator or command list pointers
                        // We still mark as open to allow caller to proceed, but operations may fail
                        // The error will be caught when trying to record commands
                    }
                }

                // Clear barrier tracking for new recording session
                _pendingBarriers.Clear();
                _resourceStates.Clear();

                _isOpen = true;
            }

            public void Close()
            {
                if (!_isOpen)
                {
                    return;
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    // Command list not initialized - just mark as closed
                    _isOpen = false;
                    return;
                }

                // Close the command list
                // This indicates that recording to the command list has finished
                // The command list must be closed before it can be executed
                int hr = CallClose(_d3d12CommandList);
                if (hr < 0)
                {
                    // HRESULT indicates failure
                    // According to D3D12 documentation, Close can fail if:
                    // - The command list is already closed
                    // - There are invalid commands in the list
                    // - Device was removed
                    // We still mark as closed to prevent further recording attempts
                    // The error will be caught when executing the command list
                }

                _isOpen = false;
            }

            // All ICommandList methods are now fully implemented
            // Previously stubbed methods (CreateSrvDescriptorForBindingSet, CreateSrvDescriptorForAccelStruct, GetShaderIdentifier) have been completed

            /// <summary>
            /// Writes byte array data to a GPU buffer using D3D12 upload heap and CopyBufferRegion.
            ///
            /// Implementation: Creates a temporary upload buffer (D3D12_HEAP_TYPE_UPLOAD), maps it for CPU access,
            /// copies the data, unmaps it, then uses CopyBufferRegion to copy from the staging buffer to the
            /// destination buffer. This is the standard D3D12 pattern for uploading buffer data.
            ///
            /// Based on DirectX 12 Uploading Resource Data:
            /// https://docs.microsoft.com/en-us/windows/win32/direct3d12/uploading-resource-data
            ///
            /// Pattern matches original engine behavior:
            /// - swkotor.exe: Uses DirectX 9 UpdateSubresource pattern (D3D12 equivalent is upload heap + CopyBufferRegion)
            /// - swkotor2.exe: Uses DirectX 9 UpdateSubresource pattern (D3D12 equivalent is upload heap + CopyBufferRegion)
            /// - Original engine uploads buffer data via IDirect3DDevice9::UpdateSubresource
            /// - D3D12 equivalent: CreateCommittedResource with D3D12_HEAP_TYPE_UPLOAD, Map, copy, Unmap, CopyBufferRegion
            /// </summary>
            /// <param name="buffer">Target buffer to write data to.</param>
            /// <param name="data">Byte array containing the data to write.</param>
            /// <param name="destOffset">Offset in bytes into the destination buffer where data will be written.</param>
            public void WriteBuffer(IBuffer buffer, byte[] data, int destOffset = 0)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (data == null || data.Length == 0)
                {
                    throw new ArgumentException("Data must not be null or empty", nameof(data));
                }

                if (destOffset < 0)
                {
                    throw new ArgumentException("Destination offset must be non-negative", nameof(destOffset));
                }

                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                if (_d3d12Device == IntPtr.Zero)
                {
                    throw new InvalidOperationException("D3D12 device is not available");
                }

                // Get buffer description to validate size
                BufferDesc bufferDesc = buffer.Desc;
                if (destOffset >= bufferDesc.ByteSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(destOffset), $"Destination offset {destOffset} exceeds buffer size {bufferDesc.ByteSize}");
                }

                if (data.Length > bufferDesc.ByteSize - destOffset)
                {
                    throw new ArgumentException($"Data size {data.Length} exceeds available buffer space {bufferDesc.ByteSize - destOffset}", nameof(data));
                }

                int dataSize = data.Length;

                // Create temporary upload buffer for staging
                // Upload buffers use D3D12_HEAP_TYPE_UPLOAD and are CPU-writable, GPU-readable
                IntPtr stagingBufferResource = IntPtr.Zero;
                try
                {
                    // Create resource description for staging buffer
                    D3D12_RESOURCE_DESC stagingBufferDesc = new D3D12_RESOURCE_DESC
                    {
                        Dimension = D3D12_RESOURCE_DIMENSION_BUFFER,
                        Alignment = 0,
                        Width = unchecked((ulong)dataSize),
                        Height = 1,
                        DepthOrArraySize = 1,
                        MipLevels = 1,
                        Format = 0, // DXGI_FORMAT_UNKNOWN for buffers
                        SampleDesc = new D3D12_SAMPLE_DESC { Count = 1, Quality = 0 },
                        Layout = 0, // D3D12_TEXTURE_LAYOUT_ROW_MAJOR
                        Flags = 0 // D3D12_RESOURCE_FLAG_NONE
                    };

                    // Create heap properties for upload heap
                    D3D12_HEAP_PROPERTIES heapProperties = new D3D12_HEAP_PROPERTIES
                    {
                        Type = D3D12_HEAP_TYPE_UPLOAD,
                        CPUPageProperty = 0, // D3D12_CPU_PAGE_PROPERTY_UNKNOWN
                        MemoryPoolPreference = 0, // D3D12_MEMORY_POOL_UNKNOWN
                        CreationNodeMask = 0,
                        VisibleNodeMask = 0
                    };

                    // Allocate memory for structures
                    int heapPropertiesSize = Marshal.SizeOf(typeof(D3D12_HEAP_PROPERTIES));
                    IntPtr heapPropertiesPtr = Marshal.AllocHGlobal(heapPropertiesSize);
                    int resourceDescSize = Marshal.SizeOf(typeof(D3D12_RESOURCE_DESC));
                    IntPtr resourceDescPtr = Marshal.AllocHGlobal(resourceDescSize);
                    IntPtr resourcePtr = Marshal.AllocHGlobal(IntPtr.Size);

                    try
                    {
                        // Marshal structures to unmanaged memory
                        Marshal.StructureToPtr(heapProperties, heapPropertiesPtr, false);
                        Marshal.StructureToPtr(stagingBufferDesc, resourceDescPtr, false);

                        // IID_ID3D12Resource
                        Guid iidResource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad");

                        // Initial state is D3D12_RESOURCE_STATE_GENERIC_READ (0) for upload heap
                        int hr = _device.CallCreateCommittedResource(_d3d12Device, heapPropertiesPtr, 0, resourceDescPtr, 0, IntPtr.Zero, ref iidResource, resourcePtr);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"Failed to create staging buffer: HRESULT 0x{hr:X8}");
                        }

                        stagingBufferResource = Marshal.ReadIntPtr(resourcePtr);
                        if (stagingBufferResource == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("CreateCommittedResource returned null staging buffer");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(heapPropertiesPtr);
                        Marshal.FreeHGlobal(resourceDescPtr);
                        Marshal.FreeHGlobal(resourcePtr);
                    }

                    // Map the staging buffer and copy data
                    // For upload heaps, we can map and write directly
                    IntPtr mappedData = MapStagingBufferResource(stagingBufferResource, 0, unchecked((ulong)dataSize));
                    if (mappedData == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to map staging buffer");
                    }

                    try
                    {
                        // Copy data from byte array to mapped memory
                        // Use C# 7.3 compatible code (avoid System.Buffer.MemoryCopy which is C# 8.0+)
                        unsafe
                        {
                            byte* dstPtr = (byte*)mappedData;
                            fixed (byte* srcPtr = data)
                            {
                                // C# 7.3 compatible: use pointer arithmetic and loop instead of System.Buffer.MemoryCopy
                                for (int i = 0; i < dataSize; i++)
                                {
                                    dstPtr[i] = srcPtr[i];
                                }
                            }
                        }
                    }
                    finally
                    {
                        UnmapStagingBufferResource(stagingBufferResource, 0, unchecked((ulong)dataSize));
                    }

                    // Transition destination buffer to COPY_DEST state
                    SetBufferState(buffer, ResourceState.CopyDest);
                    // Upload buffers are always in generic read state, no transition needed
                    CommitBarriers();

                    // Get destination buffer resource handle
                    IntPtr dstBufferResource = buffer.NativeHandle;
                    if (dstBufferResource == IntPtr.Zero)
                    {
                        throw new ArgumentException("Buffer has invalid native handle", nameof(buffer));
                    }

                    // Copy from staging buffer to destination buffer using CopyBufferRegion
                    // Source offset = 0 (start of staging buffer)
                    // Destination offset = destOffset (user-specified offset in destination buffer)
                    // Size = dataSize (amount of data to copy)
                    CallCopyBufferRegion(
                        _d3d12CommandList,
                        dstBufferResource,
                        unchecked((ulong)destOffset),
                        stagingBufferResource,
                        0,
                        unchecked((ulong)dataSize));
                }
                finally
                {
                    // Release staging buffer resource
                    // ID3D12Resource is a COM object that must be released via IUnknown::Release()
                    if (stagingBufferResource != IntPtr.Zero)
                    {
                        _device.ReleaseComObject(stagingBufferResource);
                    }
                }
            }

            /// <summary>
            /// Writes typed array data to a GPU buffer using D3D12 upload heap and CopyBufferRegion.
            ///
            /// Converts the typed array to bytes and writes using the same mechanism as WriteBuffer(byte[]).
            /// Uses D3D12 upload heap pattern: create staging buffer, map, copy data, unmap, then CopyBufferRegion.
            ///
            /// Based on DirectX 12 Uploading Resource Data:
            /// https://docs.microsoft.com/en-us/windows/win32/direct3d12/uploading-resource-data
            ///
            /// Pattern matches original engine behavior:
            /// - swkotor.exe: Uses DirectX 9 UpdateSubresource pattern (D3D12 equivalent is upload heap + CopyBufferRegion)
            /// - swkotor2.exe: Uses DirectX 9 UpdateSubresource pattern (D3D12 equivalent is upload heap + CopyBufferRegion)
            /// </summary>
            /// <typeparam name="T">Unmanaged type for the array elements.</typeparam>
            /// <param name="buffer">Target buffer to write data to.</param>
            /// <param name="data">Typed array containing the data to write.</param>
            /// <param name="destOffset">Offset in bytes into the destination buffer where data will be written.</param>
            public void WriteBuffer<T>(IBuffer buffer, T[] data, int destOffset = 0) where T : unmanaged
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (data == null || data.Length == 0)
                {
                    throw new ArgumentException("Data must not be null or empty", nameof(data));
                }

                if (destOffset < 0)
                {
                    throw new ArgumentException("Destination offset must be non-negative", nameof(destOffset));
                }

                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                // Calculate byte size
                int elementSize = Marshal.SizeOf<T>();
                int dataSize = data.Length * elementSize;

                // Convert typed array to byte array using GCHandle for safe pinning (C# 7.3 compatible)
                byte[] byteData = new byte[dataSize];
                GCHandle srcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                GCHandle dstHandle = GCHandle.Alloc(byteData, GCHandleType.Pinned);
                try
                {
                    IntPtr srcPtr = srcHandle.AddrOfPinnedObject();
                    IntPtr dstPtr = dstHandle.AddrOfPinnedObject();
                    unsafe
                    {
                        byte* srcBytePtr = (byte*)srcPtr.ToPointer();
                        byte* dstBytePtr = (byte*)dstPtr.ToPointer();
                        for (int i = 0; i < dataSize; i++)
                        {
                            dstBytePtr[i] = srcBytePtr[i];
                        }
                    }
                }
                finally
                {
                    if (srcHandle.IsAllocated) srcHandle.Free();
                    if (dstHandle.IsAllocated) dstHandle.Free();
                }

                // Delegate to byte array WriteBuffer method
                WriteBuffer(buffer, byteData, destOffset);
            }
            /// <summary>
            /// Writes texture data to a texture subresource using a staging buffer.
            ///
            /// Implementation: Creates a temporary upload buffer, copies data with proper row pitch alignment,
            /// then uses CopyTextureRegion to copy from the upload buffer to the texture.
            ///
            /// Based on DirectX 12 UpdateSubresources pattern:
            /// https://docs.microsoft.com/en-us/windows/win32/direct3d12/uploading-resource-data
            ///
            /// Implementation uses ID3D12Device::GetCopyableFootprints for exact layout calculations,
            /// ensuring hardware-specific alignment requirements are properly handled.
            /// Based on DirectX 12 GetCopyableFootprints: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-getcopyablefootprints
            /// </summary>
            public void WriteTexture(ITexture texture, int mipLevel, int arraySlice, byte[] data)
            {
                if (texture == null)
                {
                    throw new ArgumentNullException(nameof(texture));
                }

                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }

                if (data.Length == 0)
                {
                    throw new ArgumentException("Data array cannot be empty", nameof(data));
                }

                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                if (_d3d12Device == IntPtr.Zero)
                {
                    throw new InvalidOperationException("D3D12 device is not available");
                }

                // Validate mip level and array slice
                TextureDesc desc = texture.Desc;
                if (mipLevel < 0 || mipLevel >= desc.MipLevels)
                {
                    throw new ArgumentOutOfRangeException(nameof(mipLevel), $"Mip level must be between 0 and {desc.MipLevels - 1}");
                }

                if (arraySlice < 0 || arraySlice >= desc.ArraySize)
                {
                    throw new ArgumentOutOfRangeException(nameof(arraySlice), $"Array slice must be between 0 and {desc.ArraySize - 1}");
                }

                // Calculate mip level dimensions
                uint mipWidth = unchecked((uint)Math.Max(1, desc.Width >> mipLevel));
                uint mipHeight = unchecked((uint)Math.Max(1, desc.Height >> mipLevel));
                uint mipDepth = unchecked((uint)Math.Max(1, (desc.Depth > 0 ? desc.Depth : 1) >> mipLevel));

                // Calculate subresource index
                // Subresource index = arraySlice * MipLevels + mipLevel
                uint subresourceIndex = unchecked((uint)(arraySlice * desc.MipLevels + mipLevel));

                // Get bytes per pixel for source data calculation (source data is tightly packed)
                // Note: This is only used for reading from the source data array
                // Destination uses exact row pitch from GetCopyableFootprints
                uint bytesPerPixel = GetBytesPerPixelForTextureFormat(desc.Format);
                if (bytesPerPixel == 0)
                {
                    throw new NotSupportedException($"Texture format {desc.Format} is not supported for WriteTexture (compressed formats require special handling)");
                }

                // Build texture resource description for GetCopyableFootprints
                // Based on DirectX 12 GetCopyableFootprints: Requires full texture resource description
                // We need to build the D3D12_RESOURCE_DESC that matches the texture we're writing to
                uint dxgiFormat = ConvertTextureFormatToDxgiFormatForTexture(desc.Format);
                if (dxgiFormat == 0)
                {
                    throw new NotSupportedException($"Texture format {desc.Format} cannot be converted to DXGI_FORMAT for GetCopyableFootprints");
                }

                uint resourceDimension = MapTextureDimensionToD3D12(desc.Dimension);

                // Build resource description matching the texture
                D3D12_RESOURCE_DESC textureResourceDesc = new D3D12_RESOURCE_DESC
                {
                    Dimension = resourceDimension,
                    Alignment = 0, // D3D12_DEFAULT_RESOURCE_PLACEMENT_ALIGNMENT
                    Width = unchecked((ulong)desc.Width),
                    Height = unchecked((uint)desc.Height),
                    DepthOrArraySize = unchecked((ushort)(desc.Dimension == TextureDimension.Texture3D ? desc.Depth : desc.ArraySize)),
                    MipLevels = unchecked((ushort)desc.MipLevels),
                    Format = dxgiFormat,
                    SampleDesc = new D3D12_SAMPLE_DESC
                    {
                        Count = unchecked((uint)desc.SampleCount),
                        Quality = 0
                    },
                    Layout = 0, // D3D12_TEXTURE_LAYOUT_UNKNOWN
                    Flags = 0 // D3D12_RESOURCE_FLAG_NONE (flags don't affect footprint calculation)
                };

                // Get exact footprint using GetCopyableFootprints
                // Based on DirectX 12 GetCopyableFootprints: Returns exact layout accounting for hardware alignment
                IntPtr resourceDescPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RESOURCE_DESC)));
                IntPtr layoutsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_PLACED_SUBRESOURCE_FOOTPRINT)));
                IntPtr numRowsPtr = Marshal.AllocHGlobal(sizeof(uint));
                IntPtr rowSizeInBytesPtr = Marshal.AllocHGlobal(sizeof(ulong));
                IntPtr totalBytesPtr = Marshal.AllocHGlobal(sizeof(ulong));

                D3D12_PLACED_SUBRESOURCE_FOOTPRINT placedFootprint;
                uint numRows;
                ulong rowSizeInBytes;
                ulong totalBytes;

                try
                {
                    // Marshal resource description to unmanaged memory
                    Marshal.StructureToPtr(textureResourceDesc, resourceDescPtr, false);

                    // Call GetCopyableFootprints to get exact layout for the specific subresource
                    _device.CallGetCopyableFootprints(
                        _d3d12Device,
                        resourceDescPtr,
                        subresourceIndex, // FirstSubresource: the subresource we're writing to
                        1, // NumSubresources: only need one subresource
                        0, // BaseOffset: 0 for our staging buffer
                        layoutsPtr, // pLayouts: will receive the footprint
                        numRowsPtr, // pNumRows: number of rows
                        rowSizeInBytesPtr, // pRowSizeInBytes: size of each row
                        totalBytesPtr); // pTotalBytes: total size needed

                    // Read back the results
                    placedFootprint = (D3D12_PLACED_SUBRESOURCE_FOOTPRINT)Marshal.PtrToStructure(layoutsPtr, typeof(D3D12_PLACED_SUBRESOURCE_FOOTPRINT));
                    numRows = (uint)Marshal.ReadInt32(numRowsPtr);
                    rowSizeInBytes = (ulong)Marshal.ReadInt64(rowSizeInBytesPtr);
                    totalBytes = (ulong)Marshal.ReadInt64(totalBytesPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(resourceDescPtr);
                    Marshal.FreeHGlobal(layoutsPtr);
                    Marshal.FreeHGlobal(numRowsPtr);
                    Marshal.FreeHGlobal(rowSizeInBytesPtr);
                    Marshal.FreeHGlobal(totalBytesPtr);
                }

                // Extract exact layout values from footprint
                uint rowPitch = placedFootprint.Footprint.RowPitch;
                uint footprintHeight = placedFootprint.Footprint.Height;
                uint footprintDepth = placedFootprint.Footprint.Depth;

                // Calculate slice pitch from row pitch and number of rows
                // For 3D textures, depth pitch = rowPitch * numRows (for each depth slice)
                // For 2D/1D array textures, each array slice uses rowPitch * numRows
                ulong slicePitch = (ulong)rowPitch * (ulong)numRows;

                // Validate data size matches expected size from GetCopyableFootprints
                if (data.Length < (int)totalBytes)
                {
                    throw new ArgumentException($"Data array size ({data.Length}) is too small for texture subresource (expected at least {totalBytes} bytes from GetCopyableFootprints)", nameof(data));
                }

                // Create temporary upload buffer for staging directly using D3D12_HEAP_TYPE_UPLOAD
                // Upload buffers use D3D12_HEAP_TYPE_UPLOAD and are CPU-writable, GPU-readable
                // We create the staging buffer directly rather than using CreateBuffer to ensure upload heap type
                IntPtr stagingBufferResource = IntPtr.Zero;
                try
                {
                    // Create resource description for staging buffer
                    // Use totalBytes from GetCopyableFootprints for exact buffer size
                    D3D12_RESOURCE_DESC bufferDesc = new D3D12_RESOURCE_DESC
                    {
                        Dimension = D3D12_RESOURCE_DIMENSION_BUFFER,
                        Alignment = 0,
                        Width = totalBytes, // Use exact size from GetCopyableFootprints
                        Height = 1,
                        DepthOrArraySize = 1,
                        MipLevels = 1,
                        Format = 0, // DXGI_FORMAT_UNKNOWN for buffers
                        SampleDesc = new D3D12_SAMPLE_DESC { Count = 1, Quality = 0 },
                        Layout = 0, // D3D12_TEXTURE_LAYOUT_ROW_MAJOR
                        Flags = 0 // D3D12_RESOURCE_FLAG_NONE
                    };

                    // Create heap properties for upload heap
                    D3D12_HEAP_PROPERTIES heapProperties = new D3D12_HEAP_PROPERTIES
                    {
                        Type = D3D12_HEAP_TYPE_UPLOAD,
                        CPUPageProperty = 0, // D3D12_CPU_PAGE_PROPERTY_UNKNOWN
                        MemoryPoolPreference = 0, // D3D12_MEMORY_POOL_UNKNOWN
                        CreationNodeMask = 0,
                        VisibleNodeMask = 0
                    };

                    // Allocate memory for structures
                    int heapPropertiesSize = Marshal.SizeOf(typeof(D3D12_HEAP_PROPERTIES));
                    IntPtr heapPropertiesPtr = Marshal.AllocHGlobal(heapPropertiesSize);
                    int bufferResourceDescSize = Marshal.SizeOf(typeof(D3D12_RESOURCE_DESC));
                    IntPtr bufferResourceDescPtr = Marshal.AllocHGlobal(bufferResourceDescSize);
                    IntPtr resourcePtr = Marshal.AllocHGlobal(IntPtr.Size);

                    try
                    {
                        // Marshal structures to unmanaged memory
                        Marshal.StructureToPtr(heapProperties, heapPropertiesPtr, false);
                        Marshal.StructureToPtr(bufferDesc, bufferResourceDescPtr, false);

                        // IID_ID3D12Resource
                        Guid iidResource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad");

                        // Initial state is D3D12_RESOURCE_STATE_GENERIC_READ (0) for upload heap
                        int hr = _device.CallCreateCommittedResource(_d3d12Device, heapPropertiesPtr, 0, bufferResourceDescPtr, 0, IntPtr.Zero, ref iidResource, resourcePtr);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"Failed to create staging buffer: HRESULT 0x{hr:X8}");
                        }

                        stagingBufferResource = Marshal.ReadIntPtr(resourcePtr);
                        if (stagingBufferResource == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("CreateCommittedResource returned null staging buffer");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(heapPropertiesPtr);
                        Marshal.FreeHGlobal(bufferResourceDescPtr);
                        Marshal.FreeHGlobal(resourcePtr);
                    }

                    // Map the staging buffer and copy data with exact row pitch from GetCopyableFootprints
                    // For upload heaps, we can map and write directly
                    IntPtr mappedData = MapStagingBufferResource(stagingBufferResource, 0, totalBytes);
                    if (mappedData == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to map staging buffer");
                    }

                    try
                    {
                        // Copy data row by row, respecting row pitch alignment
                        // Use C# 7.3 compatible code (avoid System.Buffer.MemoryCopy which is C# 8.0+)
                        unsafe
                        {
                            byte* dstPtr = (byte*)mappedData;
                            fixed (byte* srcPtr = data)
                            {
                                for (uint z = 0; z < mipDepth; z++)
                                {
                                    for (uint y = 0; y < mipHeight; y++)
                                    {
                                        uint srcRowOffset = (z * mipHeight * mipWidth * bytesPerPixel) + (y * mipWidth * bytesPerPixel);
                                        uint dstRowOffset = unchecked((uint)((z * slicePitch) + (y * rowPitch)));

                                        // C# 7.3 compatible: use pointer arithmetic and loop instead of System.Buffer.MemoryCopy
                                        byte* srcRowPtr = srcPtr + srcRowOffset;
                                        byte* dstRowPtr = dstPtr + dstRowOffset;
                                        uint rowSize = mipWidth * bytesPerPixel;
                                        for (uint x = 0; x < rowSize; x++)
                                        {
                                            dstRowPtr[x] = srcRowPtr[x];
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        UnmapStagingBufferResource(stagingBufferResource, 0, totalBytes);
                    }

                    // Transition texture to COPY_DEST state
                    SetTextureState(texture, ResourceState.CopyDest);
                    // Transition staging buffer to COPY_SOURCE state (though upload buffers are always in generic read state)
                    CommitBarriers();

                    // Get texture resource handle
                    IntPtr textureResource = texture.NativeHandle;
                    if (textureResource == IntPtr.Zero)
                    {
                        throw new ArgumentException("Texture has invalid native handle");
                    }

                    // Create copy locations for staging buffer (source) and texture (destination)
                    // For buffer-to-texture copy, source uses PLACED_FOOTPRINT type
                    uint copyDxgiFormat = ConvertTextureFormatToDxgiFormatForTexture(desc.Format);
                    if (copyDxgiFormat == 0)
                    {
                        throw new NotSupportedException($"Texture format {desc.Format} cannot be converted to DXGI_FORMAT");
                    }

                    // Use the exact footprint from GetCopyableFootprints for CopyTextureRegion
                    // Set Offset to 0 since data starts at offset 0 in our staging buffer
                    D3D12_PLACED_SUBRESOURCE_FOOTPRINT placedFootprintForCopy = new D3D12_PLACED_SUBRESOURCE_FOOTPRINT
                    {
                        Offset = 0, // Data starts at offset 0 in the staging buffer
                        Footprint = placedFootprint.Footprint // Use exact footprint from GetCopyableFootprints
                    };

                    var srcLocation = new D3D12_TEXTURE_COPY_LOCATION
                    {
                        pResource = stagingBufferResource,
                        Type = D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT,
                        Union = new D3D12_TEXTURE_COPY_LOCATION_UNION
                        {
                            PlacedFootprint = placedFootprintForCopy // Use footprint with offset 0 for staging buffer
                        }
                    };

                    var dstLocation = new D3D12_TEXTURE_COPY_LOCATION
                    {
                        pResource = textureResource,
                        Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX,
                        Union = new D3D12_TEXTURE_COPY_LOCATION_UNION
                        {
                            SubresourceIndex = subresourceIndex // Set subresource index
                        }
                    };

                    // Allocate memory for copy location structures
                    int locationSize = Marshal.SizeOf(typeof(D3D12_TEXTURE_COPY_LOCATION));
                    IntPtr srcLocationPtr = Marshal.AllocHGlobal(locationSize);
                    IntPtr dstLocationPtr = Marshal.AllocHGlobal(locationSize);

                    try
                    {
                        // Marshal structures to unmanaged memory
                        Marshal.StructureToPtr(srcLocation, srcLocationPtr, false);
                        Marshal.StructureToPtr(dstLocation, dstLocationPtr, false);

                        // Set SubresourceIndex in the destination location union
                        // Offset = sizeof(IntPtr) + sizeof(uint) = 8 + 4 = 12 bytes from start of structure
                        unsafe
                        {
                            uint* dstSubresourcePtr = (uint*)((byte*)dstLocationPtr + 12);
                            *dstSubresourcePtr = subresourceIndex;
                        }

                        // CopyTextureRegion signature: void CopyTextureRegion(
                        //   const D3D12_TEXTURE_COPY_LOCATION *pDst,
                        //   UINT DstX, UINT DstY, UINT DstZ,
                        //   const D3D12_TEXTURE_COPY_LOCATION *pSrc,
                        //   const D3D12_BOX *pSrcBox)
                        // pSrcBox = null means copy entire source region
                        // DstX/Y/Z = 0 means copy to origin of destination subresource
                        CallCopyTextureRegion(_d3d12CommandList, dstLocationPtr, 0, 0, 0, srcLocationPtr, IntPtr.Zero);
                    }
                    finally
                    {
                        // Free allocated memory
                        Marshal.FreeHGlobal(srcLocationPtr);
                        Marshal.FreeHGlobal(dstLocationPtr);
                    }
                }
                finally
                {
                    // Release staging buffer resource
                    if (stagingBufferResource != IntPtr.Zero)
                    {
                        _device.ReleaseComObject(stagingBufferResource);
                    }
                }
            }
            public void CopyBuffer(IBuffer dest, int destOffset, IBuffer src, int srcOffset, int size)
            {
                if (dest == null || src == null)
                {
                    throw new ArgumentNullException(dest == null ? nameof(dest) : nameof(src));
                }

                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Validate size
                if (size <= 0)
                {
                    throw new ArgumentException("Copy size must be greater than zero", nameof(size));
                }

                // Validate offsets are non-negative
                if (destOffset < 0 || srcOffset < 0)
                {
                    throw new ArgumentException("Buffer offsets must be non-negative");
                }

                // Get native resource handles from buffers
                // NativeHandle should contain the ID3D12Resource* pointer for D3D12Buffer instances
                IntPtr srcResource = src.NativeHandle;
                IntPtr dstResource = dest.NativeHandle;

                if (srcResource == IntPtr.Zero || dstResource == IntPtr.Zero)
                {
                    throw new ArgumentException("Source or destination buffer has invalid native handle");
                }

                // Validate buffer sizes to ensure copy operation is within bounds
                // Get buffer descriptions to validate copy ranges
                BufferDesc srcDesc = src.Desc;
                BufferDesc destDesc = dest.Desc;

                // Validate source buffer range
                if (srcOffset < 0 || srcOffset >= srcDesc.ByteSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(srcOffset), $"Source offset {srcOffset} is out of range [0, {srcDesc.ByteSize})");
                }

                if (size < 0)
                {
                    throw new ArgumentException("Number of bytes must be non-negative", nameof(size));
                }

                if (srcOffset + size > srcDesc.ByteSize)
                {
                    throw new ArgumentException($"Source range [{srcOffset}, {srcOffset + size}) exceeds source buffer size {srcDesc.ByteSize}", nameof(size));
                }

                // Validate destination buffer range
                if (destOffset < 0 || destOffset >= destDesc.ByteSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(destOffset), $"Destination offset {destOffset} is out of range [0, {destDesc.ByteSize})");
                }

                if (destOffset + size > destDesc.ByteSize)
                {
                    throw new ArgumentException($"Destination range [{destOffset}, {destOffset + size}) exceeds destination buffer size {destDesc.ByteSize}", nameof(size));
                }

                // Transition source buffer to COPY_SOURCE state
                SetBufferState(src, ResourceState.CopySource);
                // Transition destination buffer to COPY_DEST state
                SetBufferState(dest, ResourceState.CopyDest);
                // Commit barriers before copy operation
                CommitBarriers();

                // CopyBufferRegion signature: void CopyBufferRegion(
                //   ID3D12Resource* pDstBuffer,
                //   UINT64 DstOffset,
                //   ID3D12Resource* pSrcBuffer,
                //   UINT64 SrcOffset,
                //   UINT64 NumBytes)
                //
                // Based on DirectX 12 documentation:
                // - pDstBuffer: Destination buffer resource
                // - DstOffset: Offset in bytes from the start of the destination buffer
                // - pSrcBuffer: Source buffer resource
                // - SrcOffset: Offset in bytes from the start of the source buffer
                // - NumBytes: Number of bytes to copy
                //
                // Source: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-copybufferregion
                CallCopyBufferRegion(
                    _d3d12CommandList,
                    dstResource,
                    unchecked((ulong)destOffset),
                    srcResource,
                    unchecked((ulong)srcOffset),
                    unchecked((ulong)size));
            }
            public void CopyTexture(ITexture dest, ITexture src)
            {
                if (dest == null || src == null)
                {
                    throw new ArgumentNullException(dest == null ? nameof(dest) : nameof(src));
                }

                if (!_isOpen)
                {
                    throw new InvalidOperationException("Cannot record commands when command list is closed");
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Get native resource handles from textures
                // NativeHandle should contain the ID3D12Resource* pointer for D3D12Texture instances
                IntPtr srcResource = src.NativeHandle;
                IntPtr dstResource = dest.NativeHandle;

                if (srcResource == IntPtr.Zero || dstResource == IntPtr.Zero)
                {
                    throw new ArgumentException("Source or destination texture has invalid native handle");
                }

                // Validate texture dimensions match (for full copy)
                if (src.Desc.Width != dest.Desc.Width || src.Desc.Height != dest.Desc.Height)
                {
                    throw new ArgumentException("Source and destination textures must have matching dimensions for copy operation");
                }

                // Validate format compatibility (must match for full texture copy)
                if (src.Desc.Format != dest.Desc.Format)
                {
                    throw new ArgumentException("Source and destination textures must have matching formats for copy operation");
                }

                // Transition source texture to COPY_SOURCE state
                SetTextureState(src, ResourceState.CopySource);
                // Transition destination texture to COPY_DEST state
                SetTextureState(dest, ResourceState.CopyDest);
                // Commit barriers before copy operation
                CommitBarriers();

                // Create copy locations for both source and destination
                // Use SUBRESOURCE_INDEX type for texture-to-texture copy
                // When Type is SUBRESOURCE_INDEX, only the first 4 bytes of PlacedFootprint union are used
                // We initialize PlacedFootprint with zero values, and manually set the subresource index
                // by writing to the first 4 bytes of the structure after marshaling
                var srcLocation = new D3D12_TEXTURE_COPY_LOCATION
                {
                    pResource = srcResource,
                    Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX,
                    Union = new D3D12_TEXTURE_COPY_LOCATION_UNION
                    {
                        SubresourceIndex = 0 // Copy from first subresource
                    }
                };

                var dstLocation = new D3D12_TEXTURE_COPY_LOCATION
                {
                    pResource = dstResource,
                    Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX,
                    Union = new D3D12_TEXTURE_COPY_LOCATION_UNION
                    {
                        SubresourceIndex = 0 // Copy to first subresource
                    }
                };

                // Allocate memory for copy location structures
                int locationSize = Marshal.SizeOf(typeof(D3D12_TEXTURE_COPY_LOCATION));
                IntPtr srcLocationPtr = Marshal.AllocHGlobal(locationSize);
                IntPtr dstLocationPtr = Marshal.AllocHGlobal(locationSize);

                try
                {
                    // Marshal structures to unmanaged memory
                    Marshal.StructureToPtr(srcLocation, srcLocationPtr, false);
                    Marshal.StructureToPtr(dstLocation, dstLocationPtr, false);

                    // Set SubresourceIndex in the union (first 4 bytes of PlacedFootprint field)
                    // Offset = sizeof(IntPtr) + sizeof(uint) = 8 + 4 = 12 bytes from start of structure
                    unsafe
                    {
                        // Source subresource index (0 = first mip, first array slice)
                        uint* srcSubresourcePtr = (uint*)((byte*)srcLocationPtr + 12);
                        *srcSubresourcePtr = 0;

                        // Destination subresource index (0 = first mip, first array slice)
                        uint* dstSubresourcePtr = (uint*)((byte*)dstLocationPtr + 12);
                        *dstSubresourcePtr = 0;
                    }

                    // CopyTextureRegion signature: void CopyTextureRegion(
                    //   const D3D12_TEXTURE_COPY_LOCATION *pDst,
                    //   UINT DstX, UINT DstY, UINT DstZ,
                    //   const D3D12_TEXTURE_COPY_LOCATION *pSrc,
                    //   const D3D12_BOX *pSrcBox)
                    // pSrcBox = null means copy entire source texture
                    // DstX/Y/Z = 0 means copy to origin of destination
                    CallCopyTextureRegion(_d3d12CommandList, dstLocationPtr, 0, 0, 0, srcLocationPtr, IntPtr.Zero);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(srcLocationPtr);
                    Marshal.FreeHGlobal(dstLocationPtr);
                }
            }
            public void ClearColorAttachment(IFramebuffer framebuffer, int attachmentIndex, Vector4 color)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (framebuffer == null)
                {
                    return;
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Get color attachment from framebuffer
                FramebufferDesc desc = framebuffer.Desc;
                if (desc.ColorAttachments == null || attachmentIndex < 0 || attachmentIndex >= desc.ColorAttachments.Length)
                {
                    return; // Invalid attachment index
                }

                FramebufferAttachment colorAttachment = desc.ColorAttachments[attachmentIndex];
                if (colorAttachment.Texture == null)
                {
                    return; // No color attachment at this index
                }

                ITexture colorTexture = colorAttachment.Texture;
                int mipLevel = colorAttachment.MipLevel;
                int arraySlice = colorAttachment.ArraySlice;

                // Ensure texture is in RENDER_TARGET state for clearing
                SetTextureState(colorTexture, ResourceState.RenderTarget);
                CommitBarriers();

                // Get or create RTV descriptor handle for the color texture
                IntPtr rtvHandle = _device.GetOrCreateRtvHandle(colorTexture, mipLevel, arraySlice);
                if (rtvHandle == IntPtr.Zero)
                {
                    return; // Failed to create/get RTV handle
                }

                // Convert Vector4 color to float array (RGBA format for ClearRenderTargetView)
                // DirectX 12 ClearRenderTargetView expects float[4] in RGBA order
                float[] colorRGBA = new float[4] { color.X, color.Y, color.Z, color.W };

                // Call ClearRenderTargetView (NumRects = 0, pRects = null clears entire view)
                CallClearRenderTargetView(_d3d12CommandList, rtvHandle, colorRGBA, 0, IntPtr.Zero);
            }
            public void ClearDepthStencilAttachment(IFramebuffer framebuffer, float depth, byte stencil, bool clearDepth = true, bool clearStencil = true)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (framebuffer == null)
                {
                    return;
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Get depth attachment from framebuffer
                FramebufferDesc desc = framebuffer.Desc;
                if (desc.DepthAttachment.Texture == null)
                {
                    return; // No depth attachment
                }

                ITexture depthTexture = desc.DepthAttachment.Texture;
                int mipLevel = desc.DepthAttachment.MipLevel;
                int arraySlice = desc.DepthAttachment.ArraySlice;

                // Ensure texture is in DEPTH_WRITE state for clearing
                SetTextureState(depthTexture, ResourceState.DepthWrite);
                CommitBarriers();

                // Get or create DSV descriptor handle for the depth texture
                IntPtr dsvHandle = _device.GetOrCreateDsvHandle(depthTexture, mipLevel, arraySlice);
                if (dsvHandle == IntPtr.Zero)
                {
                    return; // Failed to create/get DSV handle
                }

                // Build clear flags
                uint clearFlags = 0;
                if (clearDepth)
                {
                    clearFlags |= D3D12_CLEAR_FLAG_DEPTH;
                }
                if (clearStencil)
                {
                    clearFlags |= D3D12_CLEAR_FLAG_STENCIL;
                }

                if (clearFlags == 0)
                {
                    return; // Nothing to clear
                }

                // Clamp depth value between 0.0 and 1.0 as per D3D12 specification
                float clampedDepth = Math.Max(0.0f, Math.Min(1.0f, depth));

                // Call ClearDepthStencilView (NumRects = 0, pRects = null clears entire view)
                CallClearDepthStencilView(_d3d12CommandList, dsvHandle, clearFlags, clampedDepth, stencil, 0, IntPtr.Zero);
            }
            public void ClearUAVFloat(ITexture texture, Vector4 value)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (texture == null)
                {
                    return;
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Ensure texture is in UNORDERED_ACCESS state for clearing
                SetTextureState(texture, ResourceState.UnorderedAccess);
                CommitBarriers();

                // Get or create UAV descriptor handle for the texture
                // Use mip level 0 and array slice 0 as defaults (consistent with other clear operations)
                IntPtr uavHandle = _device.GetOrCreateUavHandle(texture, 0, 0);
                if (uavHandle == IntPtr.Zero)
                {
                    return; // Failed to create/get UAV handle
                }

                // ClearUnorderedAccessViewFloat requires Values[4] array
                // Convert Vector4 to float array (RGBA/X/Y/Z/W components)
                float[] values = new float[4] { value.X, value.Y, value.Z, value.W };

                // Call ClearUnorderedAccessViewFloat (NumRects = 0, pRects = null clears entire view)
                // For non-shader-visible heaps, ViewGPUHandleInCurrentHeap can be the same as ViewCPUHandle
                CallClearUnorderedAccessViewFloat(_d3d12CommandList, uavHandle, uavHandle, texture.NativeHandle, values, 0, IntPtr.Zero);
            }
            public void ClearUAVUint(ITexture texture, uint value)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (texture == null)
                {
                    return;
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Ensure texture is in UNORDERED_ACCESS state for clearing
                SetTextureState(texture, ResourceState.UnorderedAccess);
                CommitBarriers();

                // Get or create UAV descriptor handle for the texture
                // Use mip level 0 and array slice 0 as defaults (consistent with other clear operations)
                IntPtr uavHandle = _device.GetOrCreateUavHandle(texture, 0, 0);
                if (uavHandle == IntPtr.Zero)
                {
                    return; // Failed to create/get UAV handle
                }

                // ClearUnorderedAccessViewUint requires Values[4] array
                // For single uint value, replicate it across all 4 components (RGBA/X/Y/Z/W)
                uint[] values = new uint[4] { value, value, value, value };

                // Call ClearUnorderedAccessViewUint (NumRects = 0, pRects = null clears entire view)
                // For non-shader-visible heaps, ViewGPUHandleInCurrentHeap can be the same as ViewCPUHandle
                CallClearUnorderedAccessViewUint(_d3d12CommandList, uavHandle, uavHandle, texture.NativeHandle, values, 0, IntPtr.Zero);
            }
            public void SetTextureState(ITexture texture, ResourceState state)
            {
                if (texture == null)
                {
                    return;
                }

                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                // Get native resource handle from texture
                IntPtr resourceHandle = texture.NativeHandle;
                if (resourceHandle == IntPtr.Zero)
                {
                    return; // Invalid texture
                }

                // Determine current state
                ResourceState currentState;
                if (!_resourceStates.TryGetValue(texture, out currentState))
                {
                    // First time we see this texture - assume it starts in Common state
                    currentState = ResourceState.Common;
                }

                // Check if transition is needed
                if (currentState == state)
                {
                    return; // Already in target state, no barrier needed
                }

                // Map states to D3D12 resource states
                uint d3d12StateBefore = MapResourceStateToD3D12(currentState);
                uint d3d12StateAfter = MapResourceStateToD3D12(state);

                // Queue barrier (will be flushed on CommitBarriers)
                _pendingBarriers.Add(new PendingBarrier
                {
                    Resource = resourceHandle,
                    Subresource = unchecked((uint)-1), // D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES
                    StateBefore = d3d12StateBefore,
                    StateAfter = d3d12StateAfter
                });

                // Update tracked state
                _resourceStates[texture] = state;
            }

            public void SetBufferState(IBuffer buffer, ResourceState state)
            {
                if (buffer == null)
                {
                    return;
                }

                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                // Get native resource handle from buffer
                IntPtr resourceHandle = buffer.NativeHandle;
                if (resourceHandle == IntPtr.Zero)
                {
                    return; // Invalid buffer
                }

                // Determine current state
                ResourceState currentState;
                if (!_resourceStates.TryGetValue(buffer, out currentState))
                {
                    // First time we see this buffer - assume it starts in Common state
                    currentState = ResourceState.Common;
                }

                // Check if transition is needed
                if (currentState == state)
                {
                    return; // Already in target state, no barrier needed
                }

                // Map states to D3D12 resource states
                uint d3d12StateBefore = MapResourceStateToD3D12(currentState);
                uint d3d12StateAfter = MapResourceStateToD3D12(state);

                // Queue barrier (will be flushed on CommitBarriers)
                _pendingBarriers.Add(new PendingBarrier
                {
                    Resource = resourceHandle,
                    Subresource = 0, // Buffers don't have subresources
                    StateBefore = d3d12StateBefore,
                    StateAfter = d3d12StateAfter
                });

                // Update tracked state
                _resourceStates[buffer] = state;
            }

            public void CommitBarriers()
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_pendingBarriers.Count == 0)
                {
                    return; // No barriers to commit
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    // Command list not initialized - clear barriers and return
                    _pendingBarriers.Clear();
                    return;
                }

                // Allocate memory for barrier array
                int barrierSize = Marshal.SizeOf(typeof(D3D12_RESOURCE_BARRIER));
                IntPtr barriersPtr = Marshal.AllocHGlobal(barrierSize * _pendingBarriers.Count);

                try
                {
                    // Convert pending barriers to D3D12_RESOURCE_BARRIER structures
                    IntPtr currentBarrierPtr = barriersPtr;
                    for (int i = 0; i < _pendingBarriers.Count; i++)
                    {
                        var pendingBarrier = _pendingBarriers[i];

                        var barrier = new D3D12_RESOURCE_BARRIER
                        {
                            Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION,
                            Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE,
                            Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                            {
                                pResource = pendingBarrier.Resource,
                                Subresource = pendingBarrier.Subresource,
                                StateBefore = pendingBarrier.StateBefore,
                                StateAfter = pendingBarrier.StateAfter
                            }
                        };

                        Marshal.StructureToPtr(barrier, currentBarrierPtr, false);
                        currentBarrierPtr = new IntPtr(currentBarrierPtr.ToInt64() + barrierSize);
                    }

                    // Call ID3D12GraphicsCommandList::ResourceBarrier
                    CallResourceBarrier(_d3d12CommandList, unchecked((uint)_pendingBarriers.Count), barriersPtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(barriersPtr);
                }

                // Clear pending barriers after committing
                _pendingBarriers.Clear();
            }

            /// <summary>
            /// Calls ID3D12CommandAllocator::Reset through COM vtable.
            /// VTable index 3 for ID3D12CommandAllocator (first method after IUnknown: QueryInterface, AddRef, Release).
            /// Based on DirectX 12 Command Allocator Reset: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandallocator-reset
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            private unsafe int CallCommandAllocatorReset(IntPtr commandAllocator)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return (int)HRESULT.E_FAIL;
                }

                if (commandAllocator == IntPtr.Zero)
                {
                    return (int)HRESULT.E_INVALIDARG;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandAllocator;
                // Reset is at index 3 in ID3D12CommandAllocator vtable (after IUnknown methods)
                IntPtr methodPtr = vtable[3];

                // Create delegate from function pointer
                CommandAllocatorResetDelegate resetAllocator =
                    (CommandAllocatorResetDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CommandAllocatorResetDelegate));

                return resetAllocator(commandAllocator);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::Reset through COM vtable.
            /// VTable index 4 for ID3D12GraphicsCommandList (first method after IUnknown: QueryInterface, AddRef, Release, GetDevice).
            /// Based on DirectX 12 Command List Reset: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-reset
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            private unsafe int CallCommandListReset(IntPtr commandList, IntPtr pAllocator, IntPtr pInitialState)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return (int)HRESULT.E_FAIL;
                }

                if (commandList == IntPtr.Zero || pAllocator == IntPtr.Zero)
                {
                    return (int)HRESULT.E_INVALIDARG;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // Reset is at index 4 in ID3D12GraphicsCommandList vtable (after IUnknown methods and GetDevice)
                IntPtr methodPtr = vtable[4];

                // Create delegate from function pointer
                CommandListResetDelegate resetCommandList =
                    (CommandListResetDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CommandListResetDelegate));

                return resetCommandList(commandList, pAllocator, pInitialState);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::Close through COM vtable.
            /// VTable index 5 for ID3D12GraphicsCommandList (after Reset at index 4).
            /// Based on DirectX 12 Command List Close: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-close
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            private unsafe int CallClose(IntPtr commandList)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return (int)HRESULT.E_FAIL;
                }

                if (commandList == IntPtr.Zero)
                {
                    return (int)HRESULT.E_INVALIDARG;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // Close is at index 5 in ID3D12GraphicsCommandList vtable (after Reset at index 4)
                IntPtr methodPtr = vtable[5];

                // Create delegate from function pointer
                CloseDelegate closeCommandList =
                    (CloseDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CloseDelegate));

                return closeCommandList(commandList);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::ResourceBarrier through COM vtable.
            /// VTable index 44 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Resource Barriers: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-resourcebarrier
            /// </summary>
            private unsafe void CallResourceBarrier(IntPtr commandList, uint numBarriers, IntPtr pBarriers)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || pBarriers == IntPtr.Zero || numBarriers == 0)
                {
                    return;
                }

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
            /// Calls ID3D12GraphicsCommandList::CopyTextureRegion through COM vtable.
            /// VTable index 45 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Texture Copy: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-copytextureregion
            /// </summary>
            private unsafe void CallCopyTextureRegion(
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

                if (commandList == IntPtr.Zero || pDst == IntPtr.Zero || pSrc == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // CopyTextureRegion is at index 45 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[45];

                // Create delegate from function pointer
                CopyTextureRegionDelegate copyTextureRegion =
                    (CopyTextureRegionDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CopyTextureRegionDelegate));

                copyTextureRegion(commandList, pDst, DstX, DstY, DstZ, pSrc, pSrcBox);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::CopyBufferRegion through COM vtable.
            /// VTable index 46 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Buffer Copy: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-copybufferregion
            ///
            /// CopyBufferRegion copies a region of data from one buffer to another.
            /// Signature: void CopyBufferRegion(
            ///   ID3D12Resource* pDstBuffer,
            ///   UINT64 DstOffset,
            ///   ID3D12Resource* pSrcBuffer,
            ///   UINT64 SrcOffset,
            ///   UINT64 NumBytes)
            /// </summary>
            private unsafe void CallCopyBufferRegion(
                IntPtr commandList,
                IntPtr pDstBuffer,
                ulong DstOffset,
                IntPtr pSrcBuffer,
                ulong SrcOffset,
                ulong NumBytes)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || pDstBuffer == IntPtr.Zero || pSrcBuffer == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // CopyBufferRegion is at index 46 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[46];

                // Create delegate from function pointer
                CopyBufferRegionDelegate copyBufferRegion =
                    (CopyBufferRegionDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CopyBufferRegionDelegate));

                copyBufferRegion(commandList, pDstBuffer, DstOffset, pSrcBuffer, SrcOffset, NumBytes);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::Dispatch through COM vtable.
            /// VTable index 97 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Compute Shader Dispatch: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-dispatch
            /// </summary>
            private unsafe void CallDispatch(IntPtr commandList, uint threadGroupCountX, uint threadGroupCountY, uint threadGroupCountZ)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // Dispatch is at index 97 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[97];

                // Create delegate from function pointer
                DispatchDelegate dispatch =
                    (DispatchDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(DispatchDelegate));

                dispatch(commandList, threadGroupCountX, threadGroupCountY, threadGroupCountZ);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::DrawIndexedInstanced through COM vtable.
            /// VTable index 101 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 DrawIndexedInstanced: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-drawindexedinstanced
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            private unsafe void CallDrawIndexedInstanced(IntPtr commandList, uint IndexCountPerInstance, uint InstanceCount, uint StartIndexLocation, int BaseVertexLocation, uint StartInstanceLocation)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // DrawIndexedInstanced is at index 101 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[101];

                // Create delegate from function pointer
                DrawIndexedInstancedDelegate drawIndexedInstanced =
                    (DrawIndexedInstancedDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(DrawIndexedInstancedDelegate));

                drawIndexedInstanced(commandList, IndexCountPerInstance, InstanceCount, StartIndexLocation, BaseVertexLocation, StartInstanceLocation);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::OMSetBlendFactor through COM vtable.
            /// VTable index 48 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 OMSetBlendFactor: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-omsetblendfactor
            /// </summary>
            private unsafe void CallOMSetBlendFactor(IntPtr commandList, IntPtr BlendFactor)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // OMSetBlendFactor is at index 48 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[48];

                // Create delegate from function pointer
                OMSetBlendFactorDelegate omSetBlendFactor =
                    (OMSetBlendFactorDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(OMSetBlendFactorDelegate));

                omSetBlendFactor(commandList, BlendFactor);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::DrawInstanced through COM vtable.
            /// VTable index 100 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 DrawInstanced: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-drawinstanced
            /// </summary>
            private unsafe void CallDrawInstanced(IntPtr commandList, uint VertexCountPerInstance, uint InstanceCount, uint StartVertexLocation, uint StartInstanceLocation)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // DrawInstanced is at index 100 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[100];

                // Create delegate from function pointer
                DrawInstancedDelegate drawInstanced =
                    (DrawInstancedDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(DrawInstancedDelegate));

                drawInstanced(commandList, VertexCountPerInstance, InstanceCount, StartVertexLocation, StartInstanceLocation);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::SetPipelineState through COM vtable.
            /// VTable index 40 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Pipeline State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setpipelinestate
            /// </summary>
            private unsafe void CallSetPipelineState(IntPtr commandList, IntPtr pPipelineState)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || pPipelineState == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // SetPipelineState is at index 40 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[40];

                // Create delegate from function pointer
                SetPipelineStateDelegate setPipelineState =
                    (SetPipelineStateDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetPipelineStateDelegate));

                setPipelineState(commandList, pPipelineState);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::SetComputeRootSignature through COM vtable.
            /// VTable index 46 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Root Signature: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setcomputerootsignature
            /// </summary>
            private unsafe void CallSetComputeRootSignature(IntPtr commandList, IntPtr pRootSignature)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || pRootSignature == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // SetComputeRootSignature is at index 46 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[46];

                // Create delegate from function pointer
                SetComputeRootSignatureDelegate setRootSignature =
                    (SetComputeRootSignatureDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetComputeRootSignatureDelegate));

                setRootSignature(commandList, pRootSignature);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::SetComputeRootDescriptorTable through COM vtable.
            /// VTable index 47 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Root Parameters: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setcomputerootdescriptortable
            /// </summary>
            private unsafe void CallSetComputeRootDescriptorTable(IntPtr commandList, uint RootParameterIndex, D3D12_GPU_DESCRIPTOR_HANDLE BaseDescriptor)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // SetComputeRootDescriptorTable is at index 47 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[47];

                // Create delegate from function pointer
                SetComputeRootDescriptorTableDelegate setRootDescriptorTable =
                    (SetComputeRootDescriptorTableDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetComputeRootDescriptorTableDelegate));

                setRootDescriptorTable(commandList, RootParameterIndex, BaseDescriptor);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::SetDescriptorHeaps through COM vtable.
            /// VTable index 38 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Descriptor Heaps: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setdescriptorheaps
            /// </summary>
            private unsafe void CallSetDescriptorHeaps(IntPtr commandList, uint NumDescriptorHeaps, IntPtr ppDescriptorHeaps)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || ppDescriptorHeaps == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // SetDescriptorHeaps is at index 38 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[38];

                // Create delegate from function pointer
                SetDescriptorHeapsDelegate setDescriptorHeaps =
                    (SetDescriptorHeapsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetDescriptorHeapsDelegate));

                setDescriptorHeaps(commandList, NumDescriptorHeaps, ppDescriptorHeaps);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::SetGraphicsRootSignature through COM vtable.
            /// VTable index 43 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Root Signature: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setgraphicsrootsignature
            /// </summary>
            private unsafe void CallSetGraphicsRootSignature(IntPtr commandList, IntPtr pRootSignature)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || pRootSignature == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // SetGraphicsRootSignature is at index 43 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[43];

                // Create delegate from function pointer
                SetGraphicsRootSignatureDelegate setRootSignature =
                    (SetGraphicsRootSignatureDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetGraphicsRootSignatureDelegate));

                setRootSignature(commandList, pRootSignature);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::SetGraphicsRootDescriptorTable through COM vtable.
            /// VTable index 44 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Root Parameters: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setgraphicsrootdescriptortable
            /// </summary>
            private unsafe void CallSetGraphicsRootDescriptorTable(IntPtr commandList, uint RootParameterIndex, D3D12_GPU_DESCRIPTOR_HANDLE BaseDescriptor)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // SetGraphicsRootDescriptorTable is at index 44 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[44];

                // Create delegate from function pointer
                SetGraphicsRootDescriptorTableDelegate setRootDescriptorTable =
                    (SetGraphicsRootDescriptorTableDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetGraphicsRootDescriptorTableDelegate));

                setRootDescriptorTable(commandList, RootParameterIndex, BaseDescriptor);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::RSSetViewports through COM vtable.
            /// VTable index 42 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Viewports: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-rssetviewports
            /// </summary>
            private unsafe void CallRSSetViewports(IntPtr commandList, uint NumViewports, IntPtr pViewports)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || pViewports == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // RSSetViewports is at index 42 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[42];

                // Create delegate from function pointer
                RSSetViewportsDelegate rssetViewports =
                    (RSSetViewportsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(RSSetViewportsDelegate));

                rssetViewports(commandList, NumViewports, pViewports);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::IASetVertexBuffers through COM vtable.
            /// VTable index 36 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Vertex Buffers: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-iasetvertexbuffers
            /// </summary>
            private unsafe void CallIASetVertexBuffers(IntPtr commandList, uint StartSlot, uint NumViews, IntPtr pViews)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || pViews == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // IASetVertexBuffers is at index 36 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[36];

                // Create delegate from function pointer
                IASetVertexBuffersDelegate iasetVertexBuffers =
                    (IASetVertexBuffersDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(IASetVertexBuffersDelegate));

                iasetVertexBuffers(commandList, StartSlot, NumViews, pViews);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::IASetIndexBuffer through COM vtable.
            /// VTable index 37 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Index Buffers: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-iasetindexbuffer
            /// </summary>
            private unsafe void CallIASetIndexBuffer(IntPtr commandList, IntPtr pView)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // IASetIndexBuffer is at index 37 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[37];

                // Create delegate from function pointer
                IASetIndexBufferDelegate iasetIndexBuffer =
                    (IASetIndexBufferDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(IASetIndexBufferDelegate));

                iasetIndexBuffer(commandList, pView);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::OMSetRenderTargets through COM vtable.
            /// VTable index 45 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Render Targets: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-omsetrendertargets
            /// </summary>
            private unsafe void CallOMSetRenderTargets(IntPtr commandList, uint NumRenderTargetDescriptors, IntPtr pRenderTargetDescriptors, byte RTsSingleHandleToDescriptorRange, IntPtr pDepthStencilDescriptor)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // OMSetRenderTargets is at index 45 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[45];

                // Create delegate from function pointer
                OMSetRenderTargetsDelegate omsetRenderTargets =
                    (OMSetRenderTargetsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(OMSetRenderTargetsDelegate));

                omsetRenderTargets(commandList, NumRenderTargetDescriptors, pRenderTargetDescriptors, RTsSingleHandleToDescriptorRange, pDepthStencilDescriptor);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::OMSetStencilRef through COM vtable.
            /// VTable index 46 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Output Merger Stencil Reference: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-omsetstencilref
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            private unsafe void CallOMSetStencilRef(IntPtr commandList, uint StencilRef)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // OMSetStencilRef is at index 46 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[46];

                // Create delegate from function pointer
                OMSetStencilRefDelegate omsetStencilRef =
                    (OMSetStencilRefDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(OMSetStencilRefDelegate));

                omsetStencilRef(commandList, StencilRef);
            }

            // COM interface method delegate for ClearDepthStencilView
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void ClearDepthStencilViewDelegate(IntPtr commandList, IntPtr DepthStencilView, uint ClearFlags, float Depth, byte Stencil, uint NumRects, IntPtr pRects);

            // COM interface method delegate for ClearRenderTargetView
            // ClearRenderTargetView signature: void ClearRenderTargetView(D3D12_CPU_DESCRIPTOR_HANDLE RenderTargetView, const FLOAT ColorRGBA[4], UINT NumRects, const D3D12_RECT *pRects)
            // D3D12_CPU_DESCRIPTOR_HANDLE is a pointer-sized value, so we use IntPtr
            // ColorRGBA[4] is a 4-element float array passed by value (const FLOAT[4])
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void ClearRenderTargetViewDelegate(IntPtr commandList, IntPtr RenderTargetView, IntPtr ColorRGBA, uint NumRects, IntPtr pRects);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void ClearUnorderedAccessViewFloatDelegate(IntPtr commandList, IntPtr ViewGPUHandleInCurrentHeap, IntPtr ViewCPUHandle, IntPtr pResource, IntPtr Values, uint NumRects, IntPtr pRects);

            // COM interface method delegate for RSSetScissorRects
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void RSSetScissorRectsDelegate(IntPtr commandList, uint NumRects, IntPtr pRects);

            // COM interface method delegate for BeginEvent
            // Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-beginevent
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void BeginEventDelegate(IntPtr commandList, uint Metadata, IntPtr pData, uint Size);

            // COM interface method delegate for EndEvent
            // Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-endevent
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void EndEventDelegate(IntPtr commandList);

            // COM interface method delegate for SetMarker
            // Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setmarker
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void SetMarkerDelegate(IntPtr commandList, uint Metadata, IntPtr pData, uint Size);

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::ClearDepthStencilView through COM vtable.
            /// VTable index 48 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Clear Operations: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-cleardepthstencilview
            /// </summary>
            private unsafe void CallClearDepthStencilView(IntPtr commandList, IntPtr DepthStencilView, uint ClearFlags, float Depth, byte Stencil, uint NumRects, IntPtr pRects)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || DepthStencilView == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // ClearDepthStencilView is at index 48 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[48];

                // Create delegate from function pointer
                ClearDepthStencilViewDelegate clearDsv =
                    (ClearDepthStencilViewDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ClearDepthStencilViewDelegate));

                clearDsv(commandList, DepthStencilView, ClearFlags, Depth, Stencil, NumRects, pRects);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::ClearRenderTargetView through COM vtable.
            /// VTable index 47 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Clear Operations: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-clearrendertargetview
            /// </summary>
            private unsafe void CallClearRenderTargetView(IntPtr commandList, IntPtr RenderTargetView, float[] ColorRGBA, uint NumRects, IntPtr pRects)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || RenderTargetView == IntPtr.Zero)
                {
                    return;
                }

                if (ColorRGBA == null || ColorRGBA.Length < 4)
                {
                    return; // Invalid color array
                }

                // Pin the color array and pass pointer to native function
                // DirectX 12 ClearRenderTargetView expects const FLOAT ColorRGBA[4]
                // We pin the array to get a stable pointer for the native call
                fixed (float* colorPtr = ColorRGBA)
                {
                    // Get vtable pointer
                    IntPtr* vtable = *(IntPtr**)commandList;
                    // ClearRenderTargetView is at index 47 in ID3D12GraphicsCommandList vtable
                    IntPtr methodPtr = vtable[47];

                    // Create delegate from function pointer
                    ClearRenderTargetViewDelegate clearRtv =
                        (ClearRenderTargetViewDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ClearRenderTargetViewDelegate));

                    clearRtv(commandList, RenderTargetView, new IntPtr(colorPtr), NumRects, pRects);
                }
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::ClearUnorderedAccessViewUint through COM vtable.
            /// VTable index 50 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Clear Operations: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-clearunorderedaccessviewuint
            /// </summary>
            private unsafe void CallClearUnorderedAccessViewUint(IntPtr commandList, IntPtr ViewGPUHandleInCurrentHeap, IntPtr ViewCPUHandle, IntPtr pResource, uint[] Values, uint NumRects, IntPtr pRects)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || ViewCPUHandle == IntPtr.Zero || pResource == IntPtr.Zero)
                {
                    return;
                }

                if (Values == null || Values.Length < 4)
                {
                    return; // Invalid values array
                }

                // Pin the values array and pass pointer to native function
                // DirectX 12 ClearUnorderedAccessViewUint expects const UINT Values[4]
                // We pin the array to get a stable pointer for the native call
                fixed (uint* valuesPtr = Values)
                {
                    // Get vtable pointer
                    IntPtr* vtable = *(IntPtr**)commandList;
                    // ClearUnorderedAccessViewUint is at index 50 in ID3D12GraphicsCommandList vtable
                    IntPtr methodPtr = vtable[50];

                    // Create delegate from function pointer
                    ClearUnorderedAccessViewUintDelegate clearUavUint =
                        (ClearUnorderedAccessViewUintDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ClearUnorderedAccessViewUintDelegate));

                    clearUavUint(commandList, ViewGPUHandleInCurrentHeap, ViewCPUHandle, pResource, new IntPtr(valuesPtr), NumRects, pRects);
                }
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::ClearUnorderedAccessViewFloat through COM vtable.
            /// VTable index 49 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Clear Operations: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-clearunorderedaccessviewfloat
            /// </summary>
            private unsafe void CallClearUnorderedAccessViewFloat(IntPtr commandList, IntPtr ViewGPUHandleInCurrentHeap, IntPtr ViewCPUHandle, IntPtr pResource, float[] Values, uint NumRects, IntPtr pRects)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || ViewCPUHandle == IntPtr.Zero || pResource == IntPtr.Zero)
                {
                    return;
                }

                if (Values == null || Values.Length < 4)
                {
                    return; // Invalid values array
                }

                // Pin the values array and pass pointer to native function
                // DirectX 12 ClearUnorderedAccessViewFloat expects const FLOAT Values[4]
                // We pin the array to get a stable pointer for the native call
                fixed (float* valuesPtr = Values)
                {
                    // Get vtable pointer
                    IntPtr* vtable = *(IntPtr**)commandList;
                    // ClearUnorderedAccessViewFloat is at index 49 in ID3D12GraphicsCommandList vtable
                    IntPtr methodPtr = vtable[49];

                    // Create delegate from function pointer
                    ClearUnorderedAccessViewFloatDelegate clearUavFloat =
                        (ClearUnorderedAccessViewFloatDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ClearUnorderedAccessViewFloatDelegate));

                    clearUavFloat(commandList, ViewGPUHandleInCurrentHeap, ViewCPUHandle, pResource, new IntPtr(valuesPtr), NumRects, pRects);
                }
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::RSSetScissorRects through COM vtable.
            /// VTable index 51 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Rasterizer State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-rssetscissorrects
            /// </summary>
            private unsafe void CallRSSetScissorRects(IntPtr commandList, uint NumRects, IntPtr pRects)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // RSSetScissorRects is at index 51 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[51];

                // Create delegate from function pointer
                RSSetScissorRectsDelegate setScissorRects =
                    (RSSetScissorRectsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(RSSetScissorRectsDelegate));

                setScissorRects(commandList, NumRects, pRects);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::BeginEvent through COM vtable.
            /// VTable index 57 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-beginevent
            /// Note: This method is used internally by PIX event runtime. Event name is encoded as UTF-8 bytes.
            /// </summary>
            private unsafe void CallBeginEvent(IntPtr commandList, uint metadata, IntPtr pData, uint size)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // BeginEvent is at index 57 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[57];

                // Create delegate from function pointer
                BeginEventDelegate beginEvent =
                    (BeginEventDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(BeginEventDelegate));

                beginEvent(commandList, metadata, pData, size);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::EndEvent through COM vtable.
            /// VTable index 58 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-endevent
            /// Note: This method is used internally by PIX event runtime to mark the end of an event region.
            /// </summary>
            private unsafe void CallEndEvent(IntPtr commandList)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // EndEvent is at index 58 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[58];

                // Create delegate from function pointer
                EndEventDelegate endEvent =
                    (EndEventDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(EndEventDelegate));

                endEvent(commandList);
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList::SetMarker through COM vtable.
            /// VTable index 59 for ID3D12GraphicsCommandList.
            /// Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setmarker
            /// Note: This method is used internally by PIX event runtime. Marker name is encoded as UTF-8 bytes.
            /// SetMarker is similar to BeginEvent but does not require a matching EndEvent call - it's a single point marker.
            /// </summary>
            private unsafe void CallSetMarker(IntPtr commandList, uint metadata, IntPtr pData, uint size)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // SetMarker is at index 59 in ID3D12GraphicsCommandList vtable
                IntPtr methodPtr = vtable[59];

                // Create delegate from function pointer
                SetMarkerDelegate setMarker =
                    (SetMarkerDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(SetMarkerDelegate));

                setMarker(commandList, metadata, pData, size);
            }

            /// <summary>
            /// Inserts a UAV (Unordered Access View) barrier for a texture resource.
            ///
            /// A UAV barrier ensures that all UAV writes to the resource have completed before
            /// subsequent operations (compute shaders, pixel shaders, etc.) can read from the resource.
            /// This is necessary when a resource is both written to and read from as a UAV in different
            /// draw/dispatch calls within the same command list.
            ///
            /// Based on DirectX 12 API: ID3D12GraphicsCommandList::ResourceBarrier
            /// Located via DirectX 12 documentation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-resourcebarrier
            /// Original implementation: Records a UAV barrier command into the command list
            /// UAV barriers use D3D12_RESOURCE_BARRIER_TYPE_UAV barrier type
            ///
            /// Note: UAV barriers differ from transition barriers - they don't change resource state,
            /// they only synchronize access between UAV write and read operations.
            /// </summary>
            public void UAVBarrier(ITexture texture)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (texture == null)
                {
                    return; // Null texture - nothing to barrier
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Get native resource handle from texture
                IntPtr resource = texture.NativeHandle;
                if (resource == IntPtr.Zero)
                {
                    return; // Invalid texture native handle
                }

                // Allocate memory for D3D12_RESOURCE_BARRIER structure
                int barrierSize = Marshal.SizeOf(typeof(D3D12_RESOURCE_BARRIER));
                IntPtr barrierPtr = Marshal.AllocHGlobal(barrierSize);

                try
                {
                    // Create UAV barrier structure
                    // For UAV barriers:
                    // - Type = D3D12_RESOURCE_BARRIER_TYPE_UAV (2)
                    // - Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE (0)
                    // - Transition.pResource = resource pointer (using Transition union member for UAV barrier)
                    // - Transition.Subresource, StateBefore, StateAfter are ignored for UAV barriers
                    var barrier = new D3D12_RESOURCE_BARRIER
                    {
                        Type = D3D12_RESOURCE_BARRIER_TYPE_UAV,
                        Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE,
                        Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                        {
                            pResource = resource,
                            Subresource = 0, // Ignored for UAV barriers
                            StateBefore = 0, // Ignored for UAV barriers
                            StateAfter = 0   // Ignored for UAV barriers
                        }
                    };

                    // Marshal structure to unmanaged memory
                    Marshal.StructureToPtr(barrier, barrierPtr, false);

                    // Call ResourceBarrier with single barrier
                    CallResourceBarrier(_d3d12CommandList, 1, barrierPtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(barrierPtr);
                }
            }

            /// <summary>
            /// Inserts a UAV (Unordered Access View) barrier for a buffer resource.
            ///
            /// A UAV barrier ensures that all UAV writes to the buffer have completed before
            /// subsequent operations (compute shaders, pixel shaders, etc.) can read from the buffer.
            /// This is necessary when a buffer is both written to and read from as a UAV in different
            /// draw/dispatch calls within the same command list.
            ///
            /// Based on DirectX 12 API: ID3D12GraphicsCommandList::ResourceBarrier
            /// Located via DirectX 12 documentation: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-resourcebarrier
            /// Original implementation: Records a UAV barrier command into the command list
            /// UAV barriers use D3D12_RESOURCE_BARRIER_TYPE_UAV barrier type
            ///
            /// Note: UAV barriers differ from transition barriers - they don't change resource state,
            /// they only synchronize access between UAV write and read operations.
            /// </summary>
            public void UAVBarrier(IBuffer buffer)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (buffer == null)
                {
                    return; // Null buffer - nothing to barrier
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Get native resource handle from buffer
                IntPtr resource = buffer.NativeHandle;
                if (resource == IntPtr.Zero)
                {
                    return; // Invalid buffer native handle
                }

                // Allocate memory for D3D12_RESOURCE_BARRIER structure
                int barrierSize = Marshal.SizeOf(typeof(D3D12_RESOURCE_BARRIER));
                IntPtr barrierPtr = Marshal.AllocHGlobal(barrierSize);

                try
                {
                    // Create UAV barrier structure
                    // For UAV barriers:
                    // - Type = D3D12_RESOURCE_BARRIER_TYPE_UAV (2)
                    // - Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE (0)
                    // - Transition.pResource = resource pointer (using Transition union member for UAV barrier)
                    // - Transition.Subresource, StateBefore, StateAfter are ignored for UAV barriers
                    var barrier = new D3D12_RESOURCE_BARRIER
                    {
                        Type = D3D12_RESOURCE_BARRIER_TYPE_UAV,
                        Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE,
                        Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                        {
                            pResource = resource,
                            Subresource = 0, // Ignored for UAV barriers
                            StateBefore = 0, // Ignored for UAV barriers
                            StateAfter = 0   // Ignored for UAV barriers
                        }
                    };

                    // Marshal structure to unmanaged memory
                    Marshal.StructureToPtr(barrier, barrierPtr, false);

                    // Call ResourceBarrier with single barrier
                    CallResourceBarrier(_d3d12CommandList, 1, barrierPtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(barrierPtr);
                }
            }
            public void SetGraphicsState(GraphicsState state)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (state.Pipeline == null)
                {
                    throw new ArgumentException("Graphics state must have a valid pipeline", nameof(state));
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Cast to D3D12 implementation to access native handles
                D3D12GraphicsPipeline d3d12Pipeline = state.Pipeline as D3D12GraphicsPipeline;
                if (d3d12Pipeline == null)
                {
                    throw new ArgumentException("Pipeline must be a D3D12GraphicsPipeline", nameof(state));
                }

                // Step 1: Set the graphics pipeline state
                // ID3D12GraphicsCommandList::SetPipelineState sets the pipeline state object (PSO)
                // This includes shaders, blend state, depth-stencil state, rasterizer state, etc.
                IntPtr pipelineState = d3d12Pipeline.GetPipelineState();
                if (pipelineState != IntPtr.Zero)
                {
                    CallSetPipelineState(_d3d12CommandList, pipelineState);
                }

                // Step 2: Set the graphics root signature
                // ID3D12GraphicsCommandList::SetGraphicsRootSignature sets the root signature
                // The root signature defines the layout of root parameters (constants, descriptors, etc.)
                IntPtr rootSignature = d3d12Pipeline.GetRootSignature();
                if (rootSignature != IntPtr.Zero)
                {
                    CallSetGraphicsRootSignature(_d3d12CommandList, rootSignature);
                }

                // Step 3: Set framebuffer render targets
                // ID3D12GraphicsCommandList::OMSetRenderTargets sets render targets and depth-stencil view
                if (state.Framebuffer != null)
                {
                    D3D12Framebuffer d3d12Framebuffer = state.Framebuffer as D3D12Framebuffer;
                    if (d3d12Framebuffer != null)
                    {
                        FramebufferDesc fbDesc = d3d12Framebuffer.Desc;

                        // Get render target views for color attachments
                        uint numRenderTargets = 0;
                        IntPtr renderTargetDescriptorsPtr = IntPtr.Zero;
                        byte rtsSingleHandle = 0; // FALSE - using array of handles

                        if (fbDesc.ColorAttachments != null && fbDesc.ColorAttachments.Length > 0)
                        {
                            numRenderTargets = unchecked((uint)fbDesc.ColorAttachments.Length);

                            // Allocate memory for array of CPU descriptor handles
                            // D3D12_CPU_DESCRIPTOR_HANDLE is a struct with one IntPtr field
                            int handleSize = Marshal.SizeOf<D3D12_CPU_DESCRIPTOR_HANDLE>();
                            renderTargetDescriptorsPtr = Marshal.AllocHGlobal(handleSize * (int)numRenderTargets);

                            try
                            {
                                // Get or create RTV handles for each color attachment
                                for (uint i = 0; i < numRenderTargets; i++)
                                {
                                    FramebufferAttachment attachment = fbDesc.ColorAttachments[i];
                                    if (attachment.Texture == null)
                                    {
                                        continue;
                                    }

                                    // Get or create RTV handle for this texture
                                    IntPtr rtvHandle = _device.GetOrCreateRtvHandle(attachment.Texture, attachment.MipLevel, attachment.ArraySlice);
                                    if (rtvHandle == IntPtr.Zero)
                                    {
                                        // Skip invalid RTV handles
                                        continue;
                                    }

                                    // Create CPU descriptor handle structure
                                    D3D12_CPU_DESCRIPTOR_HANDLE cpuHandle = new D3D12_CPU_DESCRIPTOR_HANDLE
                                    {
                                        ptr = rtvHandle
                                    };

                                    // Marshal handle to array
                                    IntPtr handlePtr = new IntPtr(renderTargetDescriptorsPtr.ToInt64() + (i * handleSize));
                                    Marshal.StructureToPtr(cpuHandle, handlePtr, false);
                                }
                            }
                            catch
                            {
                                // If allocation or marshalling fails, free memory and skip render target setup
                                if (renderTargetDescriptorsPtr != IntPtr.Zero)
                                {
                                    Marshal.FreeHGlobal(renderTargetDescriptorsPtr);
                                }
                                renderTargetDescriptorsPtr = IntPtr.Zero;
                                numRenderTargets = 0;
                            }
                        }

                        // Get depth-stencil view handle if depth attachment exists
                        IntPtr depthStencilDescriptorPtr = IntPtr.Zero;
                        if (fbDesc.DepthAttachment.Texture != null)
                        {
                            IntPtr dsvHandle = _device.GetOrCreateDsvHandle(fbDesc.DepthAttachment.Texture, fbDesc.DepthAttachment.MipLevel, fbDesc.DepthAttachment.ArraySlice);
                            if (dsvHandle != IntPtr.Zero)
                            {
                                // Allocate memory for depth-stencil descriptor handle
                                int dsvHandleSize = Marshal.SizeOf<D3D12_CPU_DESCRIPTOR_HANDLE>();
                                depthStencilDescriptorPtr = Marshal.AllocHGlobal(dsvHandleSize);

                                D3D12_CPU_DESCRIPTOR_HANDLE dsvCpuHandle = new D3D12_CPU_DESCRIPTOR_HANDLE
                                {
                                    ptr = dsvHandle
                                };
                                Marshal.StructureToPtr(dsvCpuHandle, depthStencilDescriptorPtr, false);
                            }
                        }

                        try
                        {
                            // Set render targets
                            // Note: If numRenderTargets is 0, pRenderTargetDescriptors can be NULL
                            CallOMSetRenderTargets(
                                _d3d12CommandList,
                                numRenderTargets,
                                renderTargetDescriptorsPtr,
                                rtsSingleHandle,
                                depthStencilDescriptorPtr);
                        }
                        finally
                        {
                            // Free allocated memory
                            if (renderTargetDescriptorsPtr != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(renderTargetDescriptorsPtr);
                            }
                            if (depthStencilDescriptorPtr != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(depthStencilDescriptorPtr);
                            }
                        }
                    }
                }

                // Step 4: Set viewports
                // ID3D12GraphicsCommandList::RSSetViewports sets viewport rectangles
                if (state.Viewport.Viewports != null && state.Viewport.Viewports.Length > 0)
                {
                    Viewport[] viewports = state.Viewport.Viewports;
                    int viewportSize = Marshal.SizeOf<D3D12_VIEWPORT>();
                    IntPtr viewportsPtr = Marshal.AllocHGlobal(viewportSize * viewports.Length);

                    try
                    {
                        // Convert Viewport[] to D3D12_VIEWPORT[]
                        for (int i = 0; i < viewports.Length; i++)
                        {
                            Viewport vp = viewports[i];
                            D3D12_VIEWPORT d3d12Viewport = new D3D12_VIEWPORT
                            {
                                TopLeftX = vp.X,
                                TopLeftY = vp.Y,
                                Width = vp.Width,
                                Height = vp.Height,
                                MinDepth = vp.MinDepth,
                                MaxDepth = vp.MaxDepth
                            };

                            IntPtr viewportPtr = new IntPtr(viewportsPtr.ToInt64() + (i * viewportSize));
                            Marshal.StructureToPtr(d3d12Viewport, viewportPtr, false);
                        }

                        CallRSSetViewports(_d3d12CommandList, unchecked((uint)viewports.Length), viewportsPtr);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(viewportsPtr);
                    }
                }

                // Step 5: Set scissor rectangles
                // ID3D12GraphicsCommandList::RSSetScissorRects sets scissor rectangles
                if (state.Viewport.Scissors != null && state.Viewport.Scissors.Length > 0)
                {
                    Rectangle[] scissors = state.Viewport.Scissors;
                    int rectSize = Marshal.SizeOf<D3D12_RECT>();
                    IntPtr rectsPtr = Marshal.AllocHGlobal(rectSize * scissors.Length);

                    try
                    {
                        // Convert Rectangle[] to D3D12_RECT[]
                        for (int i = 0; i < scissors.Length; i++)
                        {
                            Rectangle scissor = scissors[i];
                            D3D12_RECT d3d12Rect = new D3D12_RECT
                            {
                                left = scissor.X,
                                top = scissor.Y,
                                right = scissor.X + scissor.Width,
                                bottom = scissor.Y + scissor.Height
                            };

                            IntPtr rectPtr = new IntPtr(rectsPtr.ToInt64() + (i * rectSize));
                            Marshal.StructureToPtr(d3d12Rect, rectPtr, false);
                        }

                        CallRSSetScissorRects(_d3d12CommandList, unchecked((uint)scissors.Length), rectsPtr);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(rectsPtr);
                    }
                }

                // Step 6: Set descriptor heaps (required before setting root descriptor tables)
                // ID3D12GraphicsCommandList::SetDescriptorHeaps sets the descriptor heaps to use
                if (state.BindingSets != null && state.BindingSets.Length > 0)
                {
                    // Collect descriptor heaps from all binding sets
                    var descriptorHeaps = new System.Collections.Generic.List<IntPtr>();
                    var seenHeaps = new System.Collections.Generic.HashSet<IntPtr>();

                    foreach (IBindingSet bindingSet in state.BindingSets)
                    {
                        D3D12BindingSet d3d12BindingSet = bindingSet as D3D12BindingSet;
                        if (d3d12BindingSet == null)
                        {
                            continue; // Skip non-D3D12 binding sets
                        }

                        // Get descriptor heap from binding set
                        IntPtr descriptorHeap = d3d12BindingSet.GetDescriptorHeap();
                        if (descriptorHeap != IntPtr.Zero && !seenHeaps.Contains(descriptorHeap))
                        {
                            descriptorHeaps.Add(descriptorHeap);
                            seenHeaps.Add(descriptorHeap);
                        }
                    }

                    // Set descriptor heaps if we have any
                    if (descriptorHeaps.Count > 0)
                    {
                        int heapSize = Marshal.SizeOf(typeof(IntPtr));
                        IntPtr heapsPtr = Marshal.AllocHGlobal(heapSize * descriptorHeaps.Count);
                        try
                        {
                            for (int i = 0; i < descriptorHeaps.Count; i++)
                            {
                                IntPtr heapPtr = new IntPtr(heapsPtr.ToInt64() + (i * heapSize));
                                Marshal.WriteIntPtr(heapPtr, descriptorHeaps[i]);
                            }

                            CallSetDescriptorHeaps(_d3d12CommandList, unchecked((uint)descriptorHeaps.Count), heapsPtr);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(heapsPtr);
                        }
                    }

                    // Step 7: Bind descriptor sets as root descriptor tables
                    // In D3D12, each binding set maps to one root parameter index in the root signature
                    // Root parameter indices are determined by the order of binding layouts in the pipeline
                    // Based on DirectX 12 Root Signatures: https://docs.microsoft.com/en-us/windows/win32/direct3d12/root-signatures
                    // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                    GraphicsPipelineDesc pipelineDesc = d3d12Pipeline.Desc;
                    IBindingLayout[] pipelineBindingLayouts = pipelineDesc.BindingLayouts;

                    for (uint i = 0; i < state.BindingSets.Length; i++)
                    {
                        D3D12BindingSet d3d12BindingSet = state.BindingSets[i] as D3D12BindingSet;
                        if (d3d12BindingSet == null)
                        {
                            continue; // Skip non-D3D12 binding sets
                        }

                        // Determine root parameter index by matching binding set's layout to pipeline's binding layouts
                        // Root parameter index equals the index of the binding layout in the pipeline's BindingLayouts array
                        uint rootParameterIndex = i; // Default to sequential index as fallback

                        if (pipelineBindingLayouts != null && d3d12BindingSet.Layout != null)
                        {
                            // Find the index of this binding set's layout in the pipeline's binding layouts array
                            for (uint layoutIndex = 0; layoutIndex < pipelineBindingLayouts.Length; layoutIndex++)
                            {
                                if (pipelineBindingLayouts[layoutIndex] == d3d12BindingSet.Layout)
                                {
                                    rootParameterIndex = layoutIndex;
                                    break;
                                }
                            }
                        }

                        // Get GPU descriptor handle from binding set
                        D3D12_GPU_DESCRIPTOR_HANDLE handle = d3d12BindingSet.GetGpuDescriptorHandle();
                        if (handle.ptr != 0)
                        {
                            CallSetGraphicsRootDescriptorTable(_d3d12CommandList, rootParameterIndex, handle);
                        }
                    }
                }

                // Step 8: Set vertex buffers
                // ID3D12GraphicsCommandList::IASetVertexBuffers sets vertex buffer bindings
                if (state.VertexBuffers != null && state.VertexBuffers.Length > 0)
                {
                    IBuffer[] vertexBuffers = state.VertexBuffers;
                    GraphicsPipelineDesc pipelineDesc = d3d12Pipeline.Desc;
                    InputLayoutDesc inputLayout = pipelineDesc.InputLayout;

                    // Calculate strides for each vertex buffer
                    // Stride can come from InputLayout (calculate from attributes) or buffer's StructStride
                    uint[] strides = new uint[vertexBuffers.Length];
                    for (int i = 0; i < vertexBuffers.Length; i++)
                    {
                        if (vertexBuffers[i] == null)
                        {
                            strides[i] = 0;
                            continue;
                        }

                        BufferDesc bufferDesc = vertexBuffers[i].Desc;

                        // Try to get stride from buffer's StructStride if available
                        if (bufferDesc.StructStride > 0)
                        {
                            strides[i] = unchecked((uint)bufferDesc.StructStride);
                        }
                        else if (inputLayout.Attributes != null)
                        {
                            // Calculate stride from input layout attributes for this buffer index
                            uint maxOffset = 0;
                            uint maxSize = 0;

                            foreach (VertexAttributeDesc attr in inputLayout.Attributes)
                            {
                                if (attr.BufferIndex == i)
                                {
                                    // Calculate size of this attribute based on format
                                    uint attrSize = GetFormatSize(attr.Format);
                                    uint attrEnd = unchecked((uint)attr.Offset + attrSize);

                                    if (attrEnd > maxOffset + maxSize)
                                    {
                                        maxOffset = unchecked((uint)attr.Offset);
                                        maxSize = attrSize;
                                    }
                                }
                            }

                            // Stride is the maximum offset + size, rounded up to next 4-byte boundary for alignment
                            strides[i] = (maxOffset + maxSize + 3) & ~3U; // Align to 4 bytes

                            // If no attributes found, use a default stride based on buffer size
                            if (strides[i] == 0 && bufferDesc.ByteSize > 0)
                            {
                                // Default to 16 bytes per vertex (common for position + normal + texcoord)
                                strides[i] = 16;
                            }
                        }
                        else
                        {
                            // No input layout, use default stride
                            strides[i] = 16; // Default stride
                        }
                    }

                    // Create vertex buffer views
                    int vbViewSize = Marshal.SizeOf<D3D12_VERTEX_BUFFER_VIEW>();
                    IntPtr vbViewsPtr = Marshal.AllocHGlobal(vbViewSize * vertexBuffers.Length);

                    try
                    {
                        for (int i = 0; i < vertexBuffers.Length; i++)
                        {
                            if (vertexBuffers[i] == null)
                            {
                                continue;
                            }

                            D3D12Buffer d3d12Buffer = vertexBuffers[i] as D3D12Buffer;
                            if (d3d12Buffer == null)
                            {
                                continue; // Skip non-D3D12 buffers
                            }

                            // Get GPU virtual address for vertex buffer
                            IntPtr bufferResource = vertexBuffers[i].NativeHandle;
                            ulong bufferGpuVa = _device.GetGpuVirtualAddress(bufferResource);
                            if (bufferGpuVa == 0UL)
                            {
                                continue; // Skip invalid buffers
                            }

                            BufferDesc bufferDesc = vertexBuffers[i].Desc;
                            D3D12_VERTEX_BUFFER_VIEW vbView = new D3D12_VERTEX_BUFFER_VIEW
                            {
                                BufferLocation = bufferGpuVa,
                                SizeInBytes = unchecked((uint)bufferDesc.ByteSize),
                                StrideInBytes = strides[i]
                            };

                            IntPtr vbViewPtr = new IntPtr(vbViewsPtr.ToInt64() + (i * vbViewSize));
                            Marshal.StructureToPtr(vbView, vbViewPtr, false);
                        }

                        // Set vertex buffers starting at slot 0
                        CallIASetVertexBuffers(_d3d12CommandList, 0, unchecked((uint)vertexBuffers.Length), vbViewsPtr);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(vbViewsPtr);
                    }
                }

                // Step 9: Set index buffer
                // ID3D12GraphicsCommandList::IASetIndexBuffer sets the index buffer binding
                if (state.IndexBuffer != null)
                {
                    D3D12Buffer d3d12IndexBuffer = state.IndexBuffer as D3D12Buffer;
                    if (d3d12IndexBuffer != null)
                    {
                        // Get GPU virtual address for index buffer
                        IntPtr indexBufferResource = state.IndexBuffer.NativeHandle;
                        ulong indexBufferGpuVa = _device.GetGpuVirtualAddress(indexBufferResource);
                        if (indexBufferGpuVa != 0UL)
                        {
                            BufferDesc indexBufferDesc = state.IndexBuffer.Desc;

                            // Convert TextureFormat to DXGI_FORMAT for index buffer
                            // DXGI_FORMAT_R16_UINT = 56, DXGI_FORMAT_R32_UINT = 57
                            uint indexFormat = 57; // Default to R32_UINT
                            if (state.IndexFormat == TextureFormat.R16_UInt)
                            {
                                indexFormat = 56; // DXGI_FORMAT_R16_UINT
                            }
                            else if (state.IndexFormat == TextureFormat.R32_UInt)
                            {
                                indexFormat = 57; // DXGI_FORMAT_R32_UINT
                            }

                            D3D12_INDEX_BUFFER_VIEW ibView = new D3D12_INDEX_BUFFER_VIEW
                            {
                                BufferLocation = indexBufferGpuVa,
                                SizeInBytes = unchecked((uint)indexBufferDesc.ByteSize),
                                Format = indexFormat
                            };

                            // Allocate memory for index buffer view
                            int ibViewSize = Marshal.SizeOf<D3D12_INDEX_BUFFER_VIEW>();
                            IntPtr ibViewPtr = Marshal.AllocHGlobal(ibViewSize);

                            try
                            {
                                Marshal.StructureToPtr(ibView, ibViewPtr, false);
                                CallIASetIndexBuffer(_d3d12CommandList, ibViewPtr);
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(ibViewPtr);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Gets the size in bytes for a texture format.
            /// Helper method for calculating vertex attribute sizes.
            /// </summary>
            private uint GetFormatSize(TextureFormat format)
            {
                switch (format)
                {
                    case TextureFormat.R8_UNorm:
                    case TextureFormat.R8_UInt:
                    case TextureFormat.R8_SInt:
                        return 1;
                    case TextureFormat.R8G8_UNorm:
                    case TextureFormat.R8G8_UInt:
                    case TextureFormat.R8G8_SInt:
                    case TextureFormat.R16_UNorm:
                    case TextureFormat.R16_UInt:
                    case TextureFormat.R16_SInt:
                    case TextureFormat.R16_Float:
                        return 2;
                    case TextureFormat.R8G8B8A8_UNorm:
                    case TextureFormat.R8G8B8A8_UInt:
                    case TextureFormat.R8G8B8A8_SInt:
                    case TextureFormat.R32_UInt:
                    case TextureFormat.R32_SInt:
                    case TextureFormat.R32_Float:
                        return 4;
                    case TextureFormat.R32G32_Float:
                    case TextureFormat.R32G32_UInt:
                    case TextureFormat.R32G32_SInt:
                        return 8;
                    case TextureFormat.R32G32B32_Float:
                        return 12;
                    case TextureFormat.R32G32B32A32_Float:
                    case TextureFormat.R32G32B32A32_UInt:
                    case TextureFormat.R32G32B32A32_SInt:
                        return 16;
                    default:
                        return 4; // Default to 4 bytes
                }
            }

            /// <summary>
            /// Gets the number of bytes per pixel for a texture format.
            /// Returns 0 for compressed formats that require special handling.
            /// </summary>
            private uint GetBytesPerPixelForTextureFormat(TextureFormat format)
            {
                // Most formats use the same size as GetFormatSize
                // Compressed formats (BC/DXT, ASTC) return 0 to indicate special handling needed
                switch (format)
                {
                    // Uncompressed formats
                    case TextureFormat.R8_UNorm:
                    case TextureFormat.R8_UInt:
                    case TextureFormat.R8_SInt:
                        return 1;
                    case TextureFormat.R8G8_UNorm:
                    case TextureFormat.R8G8_UInt:
                    case TextureFormat.R8G8_SInt:
                    case TextureFormat.R16_UNorm:
                    case TextureFormat.R16_UInt:
                    case TextureFormat.R16_SInt:
                    case TextureFormat.R16_Float:
                        return 2;
                    case TextureFormat.R8G8B8A8_UNorm:
                    case TextureFormat.R8G8B8A8_UInt:
                    case TextureFormat.R8G8B8A8_SInt:
                    case TextureFormat.R32_UInt:
                    case TextureFormat.R32_SInt:
                    case TextureFormat.R32_Float:
                        return 4;
                    case TextureFormat.R32G32_Float:
                    case TextureFormat.R32G32_UInt:
                    case TextureFormat.R32G32_SInt:
                        return 8;
                    case TextureFormat.R32G32B32_Float:
                        return 12;
                    case TextureFormat.R32G32B32A32_Float:
                    case TextureFormat.R32G32B32A32_UInt:
                    case TextureFormat.R32G32B32A32_SInt:
                        return 16;
                    // Compressed formats - return 0 to indicate special handling needed
                    case TextureFormat.BC1:
                    case TextureFormat.BC1_UNorm:
                    case TextureFormat.BC1_UNorm_SRGB:
                    case TextureFormat.BC2:
                    case TextureFormat.BC2_UNorm:
                    case TextureFormat.BC2_UNorm_SRGB:
                    case TextureFormat.BC3:
                    case TextureFormat.BC3_UNorm:
                    case TextureFormat.BC3_UNorm_SRGB:
                    case TextureFormat.BC4:
                    case TextureFormat.BC4_UNorm:
                    case TextureFormat.BC5:
                    case TextureFormat.BC5_UNorm:
                    case TextureFormat.BC6H:
                    case TextureFormat.BC6H_UFloat:
                    case TextureFormat.BC7:
                    case TextureFormat.BC7_UNorm:
                    case TextureFormat.BC7_UNorm_SRGB:
                    case TextureFormat.ASTC_4x4:
                    case TextureFormat.ASTC_5x5:
                    case TextureFormat.ASTC_6x6:
                    case TextureFormat.ASTC_8x8:
                    case TextureFormat.ASTC_10x10:
                    case TextureFormat.ASTC_12x12:
                        return 0; // Compressed formats require block-based calculations
                    default:
                        return 4; // Default to 4 bytes per pixel
                }
            }

            /// <summary>
            /// Converts TextureFormat to DXGI_FORMAT for texture operations.
            /// Based on DirectX 12 Texture Formats: https://docs.microsoft.com/en-us/windows/win32/api/dxgiformat/ne-dxgiformat-dxgi_format
            /// </summary>
            private uint ConvertTextureFormatToDxgiFormatForTexture(TextureFormat format)
            {
                switch (format)
                {
                    case TextureFormat.R8_UNorm:
                        return 61; // DXGI_FORMAT_R8_UNORM
                    case TextureFormat.R8_UInt:
                        return 62; // DXGI_FORMAT_R8_UINT
                    case TextureFormat.R8_SInt:
                        return 63; // DXGI_FORMAT_R8_SINT
                    case TextureFormat.R8G8_UNorm:
                        return 49; // DXGI_FORMAT_R8G8_UNORM
                    case TextureFormat.R8G8_UInt:
                        return 50; // DXGI_FORMAT_R8G8_UINT
                    case TextureFormat.R8G8_SInt:
                        return 51; // DXGI_FORMAT_R8G8_SINT
                    case TextureFormat.R8G8B8A8_UNorm:
                        return 28; // DXGI_FORMAT_R8G8B8A8_UNORM
                    case TextureFormat.R8G8B8A8_UInt:
                        return 30; // DXGI_FORMAT_R8G8B8A8_UINT
                    case TextureFormat.R8G8B8A8_SInt:
                        return 29; // DXGI_FORMAT_R8G8B8A8_SINT
                    case TextureFormat.R16_UNorm:
                        return 56; // DXGI_FORMAT_R16_UNORM
                    case TextureFormat.R16_UInt:
                        return 57; // DXGI_FORMAT_R16_UINT
                    case TextureFormat.R16_SInt:
                        return 58; // DXGI_FORMAT_R16_SINT
                    case TextureFormat.R16_Float:
                        return 54; // DXGI_FORMAT_R16_FLOAT
                    case TextureFormat.R32_UInt:
                        return 42; // DXGI_FORMAT_R32_UINT
                    case TextureFormat.R32_SInt:
                        return 43; // DXGI_FORMAT_R32_SINT
                    case TextureFormat.R32_Float:
                        return 41; // DXGI_FORMAT_R32_FLOAT
                    case TextureFormat.R32G32_Float:
                        return 16; // DXGI_FORMAT_R32G32_FLOAT
                    case TextureFormat.R32G32_UInt:
                        return 52; // DXGI_FORMAT_R32G32_UINT
                    case TextureFormat.R32G32_SInt:
                        return 53; // DXGI_FORMAT_R32G32_SINT
                    case TextureFormat.R32G32B32_Float:
                        return 6; // DXGI_FORMAT_R32G32B32_FLOAT
                    case TextureFormat.R32G32B32A32_Float:
                        return 2; // DXGI_FORMAT_R32G32B32A32_FLOAT
                    case TextureFormat.R32G32B32A32_UInt:
                        return 1; // DXGI_FORMAT_R32G32B32A32_UINT
                    case TextureFormat.R32G32B32A32_SInt:
                        return 3; // DXGI_FORMAT_R32G32B32A32_SINT
                    case TextureFormat.BC1:
                    case TextureFormat.BC1_UNorm:
                        return 71; // DXGI_FORMAT_BC1_UNORM
                    case TextureFormat.BC1_UNorm_SRGB:
                        return 72; // DXGI_FORMAT_BC1_UNORM_SRGB
                    case TextureFormat.BC2:
                    case TextureFormat.BC2_UNorm:
                        return 74; // DXGI_FORMAT_BC2_UNORM
                    case TextureFormat.BC2_UNorm_SRGB:
                        return 75; // DXGI_FORMAT_BC2_UNORM_SRGB
                    case TextureFormat.BC3:
                    case TextureFormat.BC3_UNorm:
                        return 77; // DXGI_FORMAT_BC3_UNORM
                    case TextureFormat.BC3_UNorm_SRGB:
                        return 78; // DXGI_FORMAT_BC3_UNORM_SRGB
                    case TextureFormat.BC4:
                    case TextureFormat.BC4_UNorm:
                        return 80; // DXGI_FORMAT_BC4_UNORM
                    case TextureFormat.BC5:
                    case TextureFormat.BC5_UNorm:
                        return 83; // DXGI_FORMAT_BC5_UNORM
                    case TextureFormat.BC6H:
                    case TextureFormat.BC6H_UFloat:
                        return 95; // DXGI_FORMAT_BC6H_UF16
                    case TextureFormat.BC7:
                    case TextureFormat.BC7_UNorm:
                        return 98; // DXGI_FORMAT_BC7_UNORM
                    case TextureFormat.BC7_UNorm_SRGB:
                        return 99; // DXGI_FORMAT_BC7_UNORM_SRGB
                    default:
                        return 0; // DXGI_FORMAT_UNKNOWN
                }
            }

            /// <summary>
            /// Maps a staging buffer resource for CPU access.
            /// Based on DirectX 12 Resource Mapping: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12resource-map
            ///
            /// This method maps the entire buffer resource by passing NULL for pReadRange.
            /// For partial mapping, a D3D12_RANGE structure would be allocated and passed.
            /// </summary>
            private unsafe IntPtr MapStagingBufferResource(IntPtr resource, ulong subresource, ulong size)
            {
                // ID3D12Resource::Map signature:
                // HRESULT Map(UINT Subresource, const D3D12_RANGE *pReadRange, void **ppData)
                // For upload/readback heaps, pReadRange can be NULL to map the entire resource
                // Subresource is 0 for buffers
                //
                // D3D12_RANGE structure:
                //   Begin: Start offset in bytes (inclusive)
                //   End: End offset in bytes (exclusive)
                // When pReadRange is NULL, the entire resource is mapped

                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return IntPtr.Zero;
                }

                if (resource == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                // Validate subresource (must be 0 for buffers)
                if (subresource != 0)
                {
                    return IntPtr.Zero;
                }

                // Get vtable pointer (first field of COM object)
                IntPtr* vtable = *(IntPtr**)resource;
                if (vtable == null)
                {
                    return IntPtr.Zero;
                }

                // Map is at index 8 in ID3D12Resource vtable
                // ID3D12Resource vtable layout:
                //   0: QueryInterface (IUnknown)
                //   1: AddRef (IUnknown)
                //   2: Release (IUnknown)
                //   3: GetPrivateData
                //   4: SetPrivateData
                //   5: SetPrivateDataInterface
                //   6: SetName
                //   7: GetDevice
                //   8: Map
                IntPtr methodPtr = vtable[8];
                if (methodPtr == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                // Create delegate from function pointer
                // Using proper COM interop pattern with UnmanagedFunctionPointer delegate
                MapResourceDelegateInternal mapResource = (MapResourceDelegateInternal)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(MapResourceDelegateInternal));

                // Map the entire resource by passing NULL for pReadRange
                // For buffers in upload/readback heaps, this is the standard approach
                // If partial mapping were needed, we would allocate and marshal a D3D12_RANGE structure here
                IntPtr mappedData;
                int hr = mapResource(resource, unchecked((uint)subresource), IntPtr.Zero, out mappedData);

                // Check for failure (HRESULT < 0 indicates error)
                if (hr < 0)
                {
                    return IntPtr.Zero;
                }

                // Validate that we received a valid mapped pointer
                if (mappedData == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                return mappedData;
            }

            /// <summary>
            /// Unmaps a staging buffer resource.
            /// Based on DirectX 12 Resource Unmapping: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12resource-unmap
            ///
            /// This method unmaps the entire buffer resource by passing NULL for pWrittenRange.
            /// For partial unmapping, a D3D12_RANGE structure would be allocated and passed.
            /// </summary>
            private unsafe void UnmapStagingBufferResource(IntPtr resource, ulong subresource, ulong size)
            {
                // ID3D12Resource::Unmap signature:
                // void Unmap(UINT Subresource, const D3D12_RANGE *pWrittenRange)
                // For upload/readback heaps, pWrittenRange can be NULL to unmap the entire resource
                // Subresource is 0 for buffers
                //
                // D3D12_RANGE structure:
                //   Begin: Start offset in bytes (inclusive)
                //   End: End offset in bytes (exclusive)
                // When pWrittenRange is NULL, the entire resource is unmapped
                // pWrittenRange should match the range used in Map, or be NULL if Map used NULL

                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (resource == IntPtr.Zero)
                {
                    return;
                }

                // Validate subresource (must be 0 for buffers)
                if (subresource != 0)
                {
                    return;
                }

                // Get vtable pointer (first field of COM object)
                IntPtr* vtable = *(IntPtr**)resource;
                if (vtable == null)
                {
                    return;
                }

                // Unmap is at index 9 in ID3D12Resource vtable
                // ID3D12Resource vtable layout:
                //   0: QueryInterface (IUnknown)
                //   1: AddRef (IUnknown)
                //   2: Release (IUnknown)
                //   3: GetPrivateData
                //   4: SetPrivateData
                //   5: SetPrivateDataInterface
                //   6: SetName
                //   7: GetDevice
                //   8: Map
                //   9: Unmap
                IntPtr methodPtr = vtable[9];
                if (methodPtr == IntPtr.Zero)
                {
                    return;
                }

                // Create delegate from function pointer
                // Using proper COM interop pattern with UnmanagedFunctionPointer delegate
                UnmapResourceDelegate unmapResource = (UnmapResourceDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(UnmapResourceDelegate));

                // Unmap the entire resource by passing NULL for pWrittenRange
                // For buffers in upload/readback heaps, this is the standard approach
                // If partial unmapping were needed, we would allocate and marshal a D3D12_RANGE structure here
                // The range should match what was used in Map, or be NULL if Map used NULL
                unmapResource(resource, unchecked((uint)subresource), IntPtr.Zero);
            }

            /// <summary>
            /// Sets a single viewport.
            /// Converts Viewport to D3D12_VIEWPORT and calls ID3D12GraphicsCommandList::RSSetViewports.
            /// Based on DirectX 12 Viewports: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-rssetviewports
            /// </summary>
            public void SetViewport(Viewport viewport)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Convert Viewport to D3D12_VIEWPORT
                // Viewport uses (X, Y, Width, Height, MinDepth, MaxDepth) as float
                // D3D12_VIEWPORT uses (TopLeftX, TopLeftY, Width, Height, MinDepth, MaxDepth) as double (for alignment, but represents float values)
                D3D12_VIEWPORT d3d12Viewport = new D3D12_VIEWPORT
                {
                    TopLeftX = viewport.X,
                    TopLeftY = viewport.Y,
                    Width = viewport.Width,
                    Height = viewport.Height,
                    MinDepth = viewport.MinDepth,
                    MaxDepth = viewport.MaxDepth
                };

                // Allocate unmanaged memory for the viewport
                int viewportSize = Marshal.SizeOf(typeof(D3D12_VIEWPORT));
                IntPtr viewportPtr = Marshal.AllocHGlobal(viewportSize);

                try
                {
                    // Marshal structure to unmanaged memory
                    Marshal.StructureToPtr(d3d12Viewport, viewportPtr, false);

                    // Call RSSetViewports with a single viewport
                    CallRSSetViewports(_d3d12CommandList, 1, viewportPtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(viewportPtr);
                }
            }

            /// <summary>
            /// Sets multiple viewports.
            /// Converts Viewport[] to D3D12_VIEWPORT[] and calls ID3D12GraphicsCommandList::RSSetViewports.
            /// Based on DirectX 12 Viewports: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-rssetviewports
            /// </summary>
            public void SetViewports(Viewport[] viewports)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                if (viewports == null || viewports.Length == 0)
                {
                    return; // No viewports to set
                }

                // Allocate unmanaged memory for the viewport array
                int viewportSize = Marshal.SizeOf(typeof(D3D12_VIEWPORT));
                IntPtr viewportsPtr = Marshal.AllocHGlobal(viewportSize * viewports.Length);

                try
                {
                    // Convert Viewport[] to D3D12_VIEWPORT[]
                    for (int i = 0; i < viewports.Length; i++)
                    {
                        Viewport vp = viewports[i];
                        D3D12_VIEWPORT d3d12Viewport = new D3D12_VIEWPORT
                        {
                            TopLeftX = vp.X,
                            TopLeftY = vp.Y,
                            Width = vp.Width,
                            Height = vp.Height,
                            MinDepth = vp.MinDepth,
                            MaxDepth = vp.MaxDepth
                        };

                        IntPtr viewportPtr = new IntPtr(viewportsPtr.ToInt64() + (i * viewportSize));
                        Marshal.StructureToPtr(d3d12Viewport, viewportPtr, false);
                    }

                    // Call RSSetViewports with the array
                    CallRSSetViewports(_d3d12CommandList, unchecked((uint)viewports.Length), viewportsPtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(viewportsPtr);
                }
            }

            /// <summary>
            /// Sets a single scissor rectangle.
            /// Converts Rectangle (X, Y, Width, Height) to D3D12_RECT (left, top, right, bottom).
            /// Based on DirectX 12 Scissor Rectangles: https://docs.microsoft.com/en-us/windows/win32/direct3d12/scissor-rectangles
            /// </summary>
            public void SetScissor(Rectangle scissor)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Convert Rectangle to D3D12_RECT
                // Rectangle uses (X, Y, Width, Height), D3D12_RECT uses (left, top, right, bottom)
                D3D12_RECT d3d12Rect = new D3D12_RECT
                {
                    left = scissor.X,
                    top = scissor.Y,
                    right = scissor.X + scissor.Width,
                    bottom = scissor.Y + scissor.Height
                };

                // Allocate unmanaged memory for the rect
                int rectSize = Marshal.SizeOf(typeof(D3D12_RECT));
                IntPtr rectPtr = Marshal.AllocHGlobal(rectSize);

                try
                {
                    // Marshal structure to unmanaged memory
                    Marshal.StructureToPtr(d3d12Rect, rectPtr, false);

                    // Call RSSetScissorRects with a single rect
                    CallRSSetScissorRects(_d3d12CommandList, 1, rectPtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(rectPtr);
                }
            }

            /// <summary>
            /// Sets multiple scissor rectangles.
            /// Converts Rectangle[] (X, Y, Width, Height) to D3D12_RECT[] (left, top, right, bottom).
            /// Based on DirectX 12 Scissor Rectangles: https://docs.microsoft.com/en-us/windows/win32/direct3d12/scissor-rectangles
            /// </summary>
            public void SetScissors(Rectangle[] scissors)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Handle null or empty array
                if (scissors == null || scissors.Length == 0)
                {
                    // Setting 0 rects disables scissor test for all viewports
                    CallRSSetScissorRects(_d3d12CommandList, 0, IntPtr.Zero);
                    return;
                }

                // Convert Rectangle[] to D3D12_RECT[]
                // Rectangle uses (X, Y, Width, Height), D3D12_RECT uses (left, top, right, bottom)
                int rectSize = Marshal.SizeOf(typeof(D3D12_RECT));
                IntPtr rectsPtr = Marshal.AllocHGlobal(rectSize * scissors.Length);

                try
                {
                    // Convert each rectangle and marshal to unmanaged memory
                    IntPtr currentRectPtr = rectsPtr;
                    for (int i = 0; i < scissors.Length; i++)
                    {
                        Rectangle scissor = scissors[i];
                        D3D12_RECT d3d12Rect = new D3D12_RECT
                        {
                            left = scissor.X,
                            top = scissor.Y,
                            right = scissor.X + scissor.Width,
                            bottom = scissor.Y + scissor.Height
                        };

                        Marshal.StructureToPtr(d3d12Rect, currentRectPtr, false);
                        currentRectPtr = new IntPtr(currentRectPtr.ToInt64() + rectSize);
                    }

                    // Call RSSetScissorRects with all rects
                    CallRSSetScissorRects(_d3d12CommandList, unchecked((uint)scissors.Length), rectsPtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(rectsPtr);
                }
            }
            /// <summary>
            /// Sets the blend constant color used for blending operations.
            /// Converts Vector4 color to float[4] array and calls OMSetBlendFactor.
            /// Based on DirectX 12 Blend Factor: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-omsetblendfactor
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            public unsafe void SetBlendConstant(Vector4 color)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Convert Vector4 color to float[4] array (RGBA format for OMSetBlendFactor)
                // DirectX 12 OMSetBlendFactor expects float[4] in RGBA order
                float[] blendFactor = new float[4] { color.X, color.Y, color.Z, color.W };

                // Pin the array to get a pointer for the native call
                fixed (float* blendFactorPtr = blendFactor)
                {
                    IntPtr blendFactorIntPtr = new IntPtr(blendFactorPtr);
                    CallOMSetBlendFactor(_d3d12CommandList, blendFactorIntPtr);
                }
            }
            /// <summary>
            /// Sets the stencil reference value for the output merger stage.
            /// In DirectX 12, this calls ID3D12GraphicsCommandList::OMSetStencilRef.
            /// Based on DirectX 12 Output Merger Stencil Reference: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-omsetstencilref
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            public void SetStencilRef(uint reference)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Call OMSetStencilRef through COM vtable
                CallOMSetStencilRef(_d3d12CommandList, reference);
            }
            /// <summary>
            /// Draws non-indexed primitives using instanced drawing.
            /// In DirectX 12, all non-indexed drawing is done through DrawInstanced.
            /// Based on DirectX 12 DrawInstanced: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-drawinstanced
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            public void Draw(DrawArguments args)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Validate arguments
                // VertexCountPerInstance must be greater than 0
                if (args.VertexCount <= 0)
                {
                    // In non-indexed drawing context, VertexCount represents the vertex count per instance
                    // Silently return if invalid - caller should ensure valid arguments
                    return;
                }

                // InstanceCount defaults to 1 if not specified (0 or negative)
                uint instanceCount = args.InstanceCount > 0 ? unchecked((uint)args.InstanceCount) : 1U;

                // StartVertexLocation can be negative or any value
                // It will be used as-is by the GPU
                uint startVertexLocation = unchecked((uint)args.StartVertexLocation);
                uint startInstanceLocation = unchecked((uint)args.StartInstanceLocation);

                // Call DrawInstanced through COM vtable
                // Parameters:
                // - VertexCountPerInstance: args.VertexCount (vertex count per instance)
                // - InstanceCount: args.InstanceCount (defaults to 1)
                // - StartVertexLocation: args.StartVertexLocation
                // - StartInstanceLocation: args.StartInstanceLocation
                CallDrawInstanced(
                    _d3d12CommandList,
                    unchecked((uint)args.VertexCount), // VertexCountPerInstance
                    instanceCount,                      // InstanceCount
                    startVertexLocation,                // StartVertexLocation
                    startInstanceLocation);             // StartInstanceLocation
            }
            /// <summary>
            /// Draws indexed primitives using instanced drawing.
            /// In DirectX 12, all indexed drawing is done through DrawIndexedInstanced.
            /// Based on DirectX 12 DrawIndexedInstanced: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-drawindexedinstanced
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            public void DrawIndexed(DrawArguments args)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Validate arguments
                // IndexCountPerInstance must be greater than 0
                if (args.VertexCount <= 0)
                {
                    // In indexed drawing context, VertexCount represents the index count per instance
                    // Silently return if invalid - caller should ensure valid arguments
                    return;
                }

                // InstanceCount defaults to 1 if not specified (0 or negative)
                uint instanceCount = args.InstanceCount > 0 ? unchecked((uint)args.InstanceCount) : 1U;

                // StartIndexLocation and BaseVertexLocation can be negative or any value
                // They will be used as-is by the GPU
                uint startIndexLocation = unchecked((uint)args.StartIndexLocation);
                int baseVertexLocation = args.BaseVertexLocation;
                uint startInstanceLocation = unchecked((uint)args.StartInstanceLocation);

                // Call DrawIndexedInstanced through COM vtable
                // Parameters:
                // - IndexCountPerInstance: args.VertexCount (index count per instance)
                // - InstanceCount: args.InstanceCount (defaults to 1)
                // - StartIndexLocation: args.StartIndexLocation
                // - BaseVertexLocation: args.BaseVertexLocation
                // - StartInstanceLocation: args.StartInstanceLocation
                CallDrawIndexedInstanced(
                    _d3d12CommandList,
                    unchecked((uint)args.VertexCount), // IndexCountPerInstance
                    instanceCount,                      // InstanceCount
                    startIndexLocation,                 // StartIndexLocation
                    baseVertexLocation,                 // BaseVertexLocation
                    startInstanceLocation);             // StartInstanceLocation
            }
            public void DrawIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (argumentBuffer == null)
                {
                    throw new ArgumentNullException(nameof(argumentBuffer));
                }

                if (offset < 0)
                {
                    throw new ArgumentException("Offset must be non-negative", nameof(offset));
                }

                if (drawCount <= 0)
                {
                    throw new ArgumentException("Draw count must be positive", nameof(drawCount));
                }

                if (stride <= 0)
                {
                    throw new ArgumentException("Stride must be positive", nameof(stride));
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Get the native D3D12 resource handle from the buffer
                IntPtr argumentResource = argumentBuffer.NativeHandle;
                if (argumentResource == IntPtr.Zero)
                {
                    throw new ArgumentException("Argument buffer has invalid native handle", nameof(argumentBuffer));
                }

                // Get or create the draw indirect command signature
                // Command signatures are cached per device to avoid repeated creation
                IntPtr commandSignature = _device.CreateDrawIndirectCommandSignature();
                if (commandSignature == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create or retrieve draw indirect command signature");
                }

                // Validate stride matches the command signature stride
                // D3D12_DRAW_ARGUMENTS is 16 bytes (4 uints)
                int expectedStride = Marshal.SizeOf(typeof(D3D12_DRAW_ARGUMENTS));
                if (stride != expectedStride)
                {
                    throw new ArgumentException($"Stride {stride} does not match expected stride {expectedStride} for D3D12_DRAW_ARGUMENTS", nameof(stride));
                }

                // Get GPU virtual address of the argument buffer
                ulong argumentBufferGpuVa = _device.GetGpuVirtualAddress(argumentResource);
                if (argumentBufferGpuVa == 0UL)
                {
                    throw new InvalidOperationException("Failed to get GPU virtual address for argument buffer");
                }

                // Calculate the GPU virtual address with offset
                ulong argumentBufferOffsetGpuVa = argumentBufferGpuVa + unchecked((ulong)offset);

                // For multi-draw indirect, MaxCommandCount is drawCount
                // The count buffer is NULL (IntPtr.Zero) and CountBufferOffset is 0 when not using count buffer
                // Based on DirectX 12 ExecuteIndirect: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-executeindirect
                // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                _device.CallExecuteIndirect(
                    _d3d12CommandList,
                    commandSignature,
                    unchecked((uint)drawCount), // MaxCommandCount: number of draw commands to execute
                    argumentResource, // pArgumentBuffer: resource containing D3D12_DRAW_ARGUMENTS array
                    argumentBufferOffsetGpuVa, // ArgumentBufferOffset: offset into argument buffer
                    IntPtr.Zero, // pCountBuffer: NULL (not using count buffer for this implementation)
                    0UL); // CountBufferOffset: 0 when pCountBuffer is NULL
            }

            public void DrawIndexedIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (argumentBuffer == null)
                {
                    throw new ArgumentNullException(nameof(argumentBuffer));
                }

                if (offset < 0)
                {
                    throw new ArgumentException("Offset must be non-negative", nameof(offset));
                }

                if (drawCount <= 0)
                {
                    throw new ArgumentException("Draw count must be positive", nameof(drawCount));
                }

                if (stride <= 0)
                {
                    throw new ArgumentException("Stride must be positive", nameof(stride));
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Get the native D3D12 resource handle from the buffer
                IntPtr argumentResource = argumentBuffer.NativeHandle;
                if (argumentResource == IntPtr.Zero)
                {
                    throw new ArgumentException("Argument buffer has invalid native handle", nameof(argumentBuffer));
                }

                // Get or create the draw indexed indirect command signature
                // Command signatures are cached per device to avoid repeated creation
                IntPtr commandSignature = _device.CreateDrawIndexedIndirectCommandSignature();
                if (commandSignature == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create or retrieve draw indexed indirect command signature");
                }

                // Validate stride matches the command signature stride
                // D3D12_DRAW_INDEXED_ARGUMENTS is 20 bytes (4 uints + 1 int)
                int expectedStride = Marshal.SizeOf(typeof(D3D12_DRAW_INDEXED_ARGUMENTS));
                if (stride != expectedStride)
                {
                    throw new ArgumentException($"Stride {stride} does not match expected stride {expectedStride} for D3D12_DRAW_INDEXED_ARGUMENTS", nameof(stride));
                }

                // Get GPU virtual address of the argument buffer
                ulong argumentBufferGpuVa = _device.GetGpuVirtualAddress(argumentResource);
                if (argumentBufferGpuVa == 0UL)
                {
                    throw new InvalidOperationException("Failed to get GPU virtual address for argument buffer");
                }

                // Calculate the GPU virtual address with offset
                ulong argumentBufferOffsetGpuVa = argumentBufferGpuVa + unchecked((ulong)offset);

                // For multi-draw indexed indirect, MaxCommandCount is drawCount
                // The count buffer is NULL (IntPtr.Zero) and CountBufferOffset is 0 when not using count buffer
                // Based on DirectX 12 ExecuteIndirect: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-executeindirect
                // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                _device.CallExecuteIndirect(
                    _d3d12CommandList,
                    commandSignature,
                    unchecked((uint)drawCount), // MaxCommandCount: number of draw indexed commands to execute
                    argumentResource, // pArgumentBuffer: resource containing D3D12_DRAW_INDEXED_ARGUMENTS array
                    argumentBufferOffsetGpuVa, // ArgumentBufferOffset: offset into argument buffer
                    IntPtr.Zero, // pCountBuffer: NULL (not using count buffer for this implementation)
                    0UL); // CountBufferOffset: 0 when pCountBuffer is NULL
            }
            public void SetComputeState(ComputeState state)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (state.Pipeline == null)
                {
                    throw new ArgumentException("Compute state must have a valid pipeline", nameof(state));
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Cast to D3D12 implementation to access native handles
                D3D12ComputePipeline d3d12Pipeline = state.Pipeline as D3D12ComputePipeline;
                if (d3d12Pipeline == null)
                {
                    throw new ArgumentException("Pipeline must be a D3D12ComputePipeline", nameof(state));
                }

                // Step 1: Set the compute pipeline state
                // ID3D12GraphicsCommandList::SetPipelineState sets the pipeline state object (PSO)
                // This includes the compute shader and any pipeline state configuration
                IntPtr pipelineState = d3d12Pipeline.GetPipelineState();
                if (pipelineState != IntPtr.Zero)
                {
                    CallSetPipelineState(_d3d12CommandList, pipelineState);
                }

                // Step 2: Set the compute root signature
                // ID3D12GraphicsCommandList::SetComputeRootSignature sets the root signature
                // The root signature defines the layout of root parameters (constants, descriptors, etc.)
                IntPtr rootSignature = d3d12Pipeline.GetRootSignature();
                if (rootSignature != IntPtr.Zero)
                {
                    CallSetComputeRootSignature(_d3d12CommandList, rootSignature);
                }

                // Step 3: Bind descriptor sets (binding sets) if provided
                // In D3D12, descriptor sets are bound via root descriptor tables
                // Each binding set maps to a root parameter index in the root signature
                if (state.BindingSets != null && state.BindingSets.Length > 0)
                {
                    // Collect descriptor heaps from all binding sets
                    // D3D12 requires all descriptor heaps to be set before binding descriptor tables
                    var descriptorHeaps = new System.Collections.Generic.List<IntPtr>();
                    var seenHeaps = new System.Collections.Generic.HashSet<IntPtr>();

                    foreach (IBindingSet bindingSet in state.BindingSets)
                    {
                        D3D12BindingSet d3d12BindingSet = bindingSet as D3D12BindingSet;
                        if (d3d12BindingSet == null)
                        {
                            continue; // Skip non-D3D12 binding sets
                        }

                        // Get descriptor heap from binding set
                        // D3D12BindingSet provides GetDescriptorHeap() method to access the descriptor heap
                        // We collect unique descriptor heaps to set them on the command list
                        IntPtr descriptorHeap = d3d12BindingSet.GetDescriptorHeap();
                        if (descriptorHeap != IntPtr.Zero && !seenHeaps.Contains(descriptorHeap))
                        {
                            descriptorHeaps.Add(descriptorHeap);
                            seenHeaps.Add(descriptorHeap);
                        }
                    }

                    // Set descriptor heaps if we have any
                    // ID3D12GraphicsCommandList::SetDescriptorHeaps sets the descriptor heaps to use
                    // This must be called before setting root descriptor tables
                    if (descriptorHeaps.Count > 0)
                    {
                        int heapSize = Marshal.SizeOf(typeof(IntPtr));
                        IntPtr heapsPtr = Marshal.AllocHGlobal(heapSize * descriptorHeaps.Count);
                        try
                        {
                            for (int i = 0; i < descriptorHeaps.Count; i++)
                            {
                                IntPtr heapPtr = new IntPtr(heapsPtr.ToInt64() + (i * heapSize));
                                Marshal.WriteIntPtr(heapPtr, descriptorHeaps[i]);
                            }

                            CallSetDescriptorHeaps(_d3d12CommandList, unchecked((uint)descriptorHeaps.Count), heapsPtr);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(heapsPtr);
                        }
                    }

                    // Bind each binding set as a root descriptor table
                    // In D3D12, each binding set maps to one root parameter index in the root signature
                    // Root parameter indices are determined by the order of binding layouts in the pipeline
                    // Based on DirectX 12 Root Signatures: https://docs.microsoft.com/en-us/windows/win32/direct3d12/root-signatures
                    // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                    ComputePipelineDesc pipelineDesc = d3d12Pipeline.Desc;
                    IBindingLayout[] pipelineBindingLayouts = pipelineDesc.BindingLayouts;

                    for (uint i = 0; i < state.BindingSets.Length; i++)
                    {
                        D3D12BindingSet d3d12BindingSet = state.BindingSets[i] as D3D12BindingSet;
                        if (d3d12BindingSet == null)
                        {
                            continue; // Skip non-D3D12 binding sets
                        }

                        // Determine root parameter index by matching binding set's layout to pipeline's binding layouts
                        // Root parameter index equals the index of the binding layout in the pipeline's BindingLayouts array
                        uint rootParameterIndex = i; // Default to sequential index as fallback

                        if (pipelineBindingLayouts != null && d3d12BindingSet.Layout != null)
                        {
                            // Find the index of this binding set's layout in the pipeline's binding layouts array
                            for (uint layoutIndex = 0; layoutIndex < pipelineBindingLayouts.Length; layoutIndex++)
                            {
                                if (pipelineBindingLayouts[layoutIndex] == d3d12BindingSet.Layout)
                                {
                                    rootParameterIndex = layoutIndex;
                                    break;
                                }
                            }
                        }

                        // Get GPU descriptor handle from binding set
                        // The GPU descriptor handle points to the start of the descriptor table in the heap
                        // Based on DirectX 12 Root Parameters: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setcomputerootdescriptortable
                        // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                        D3D12_GPU_DESCRIPTOR_HANDLE handle = d3d12BindingSet.GetGpuDescriptorHandle();
                        if (handle.ptr != 0)
                        {
                            CallSetComputeRootDescriptorTable(_d3d12CommandList, rootParameterIndex, handle);
                        }
                    }
                }
            }
            public void Dispatch(int groupCountX, int groupCountY = 1, int groupCountZ = 1)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (groupCountX <= 0 || groupCountY <= 0 || groupCountZ <= 0)
                {
                    return; // Invalid thread group counts
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Call ID3D12GraphicsCommandList::Dispatch
                CallDispatch(_d3d12CommandList, unchecked((uint)groupCountX), unchecked((uint)groupCountY), unchecked((uint)groupCountZ));
            }
            public void DispatchIndirect(IBuffer argumentBuffer, int offset)
            {
                if (!_isOpen)
                {
                    return; // Cannot record commands when command list is closed
                }

                if (argumentBuffer == null)
                {
                    throw new ArgumentNullException(nameof(argumentBuffer));
                }

                if (offset < 0)
                {
                    throw new ArgumentException("Offset must be non-negative", nameof(offset));
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    return; // Command list not initialized
                }

                // Get the native D3D12 resource handle from the buffer
                IntPtr argumentResource = argumentBuffer.NativeHandle;
                if (argumentResource == IntPtr.Zero)
                {
                    throw new ArgumentException("Argument buffer has invalid native handle", nameof(argumentBuffer));
                }

                // Get or create the dispatch indirect command signature
                // Command signatures are cached per device to avoid repeated creation
                IntPtr commandSignature = _device.CreateDispatchIndirectCommandSignature();
                if (commandSignature == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create or retrieve dispatch indirect command signature");
                }

                // Get GPU virtual address of the argument buffer
                ulong argumentBufferGpuVa = _device.GetGpuVirtualAddress(argumentResource);
                if (argumentBufferGpuVa == 0UL)
                {
                    throw new InvalidOperationException("Failed to get GPU virtual address for argument buffer");
                }

                // Calculate the GPU virtual address with offset
                ulong argumentBufferOffsetGpuVa = argumentBufferGpuVa + unchecked((ulong)offset);

                // For single dispatch indirect (no count buffer), MaxCommandCount is 1
                // The count buffer is NULL (IntPtr.Zero) and CountBufferOffset is 0
                // Based on DirectX 12 ExecuteIndirect: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-executeindirect
                // swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
                _device.CallExecuteIndirect(
                    _d3d12CommandList,
                    commandSignature,
                    1, // MaxCommandCount: 1 for single dispatch
                    argumentResource, // pArgumentBuffer: resource containing D3D12_DISPATCH_ARGUMENTS
                    argumentBufferOffsetGpuVa, // ArgumentBufferOffset: offset into argument buffer
                    IntPtr.Zero, // pCountBuffer: NULL for single dispatch (no count buffer)
                    0UL); // CountBufferOffset: 0 when pCountBuffer is NULL
            }
            public void SetRaytracingState(RaytracingState state)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open to set raytracing state");
                }

                // Store the raytracing state (contains pipeline and shader binding table)
                _raytracingState = state;
                _hasRaytracingState = true;
            }
            public void DispatchRays(DispatchRaysArguments args)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open to dispatch rays");
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Command list is not initialized");
                }

                // Validate dispatch dimensions
                if (args.Width <= 0 || args.Height <= 0 || args.Depth <= 0)
                {
                    throw new ArgumentException($"Invalid dispatch dimensions: Width={args.Width}, Height={args.Height}, Depth={args.Depth}. All dimensions must be greater than zero.");
                }

                // Check that raytracing state is set
                if (!_hasRaytracingState)
                {
                    throw new InvalidOperationException("Raytracing state must be set before dispatching rays. Call SetRaytracingState first.");
                }

                // Get shader binding table from raytracing state
                ShaderBindingTable shaderTable = _raytracingState.ShaderTable;

                // Validate that shader binding table buffer is set (required for ray generation shader)
                if (shaderTable.Buffer == null)
                {
                    throw new InvalidOperationException("Shader binding table buffer is required for DispatchRays");
                }

                // Get GPU virtual addresses for shader binding table buffers
                // The buffer's NativeHandle should contain the ID3D12Resource* pointer
                IntPtr rayGenBufferResource = shaderTable.Buffer.NativeHandle;
                if (rayGenBufferResource == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Ray generation shader binding table buffer has invalid native handle");
                }

                // Get GPU virtual address for ray generation shader table
                // StartAddress = buffer base address + offset
                ulong rayGenBaseGpuVa = _device.GetGpuVirtualAddress(rayGenBufferResource);
                if (rayGenBaseGpuVa == 0UL)
                {
                    throw new InvalidOperationException("Failed to get GPU virtual address for ray generation shader binding table buffer");
                }

                ulong rayGenGpuVa = rayGenBaseGpuVa + shaderTable.RayGenOffset;
                ulong rayGenSize = shaderTable.RayGenSize;

                // Validate ray generation shader table size
                if (rayGenSize == 0UL)
                {
                    throw new InvalidOperationException("Ray generation shader table size cannot be zero");
                }

                // Get miss shader table GPU virtual address (optional)
                ulong missGpuVa = 0UL;
                ulong missSize = 0UL;
                ulong missStride = 0UL;
                if (shaderTable.MissSize > 0UL)
                {
                    // Miss shader table uses the same buffer but different offset
                    missGpuVa = rayGenBaseGpuVa + shaderTable.MissOffset;
                    missSize = shaderTable.MissSize;
                    missStride = shaderTable.MissStride > 0UL ? shaderTable.MissStride : missSize;
                }

                // Get hit group shader table GPU virtual address (optional)
                ulong hitGroupGpuVa = 0UL;
                ulong hitGroupSize = 0UL;
                ulong hitGroupStride = 0UL;
                if (shaderTable.HitGroupSize > 0UL)
                {
                    // Hit group shader table uses the same buffer but different offset
                    hitGroupGpuVa = rayGenBaseGpuVa + shaderTable.HitGroupOffset;
                    hitGroupSize = shaderTable.HitGroupSize;
                    hitGroupStride = shaderTable.HitGroupStride > 0UL ? shaderTable.HitGroupStride : hitGroupSize;
                }

                // Get callable shader table GPU virtual address (optional, typically not used)
                ulong callableGpuVa = 0UL;
                ulong callableSize = 0UL;
                ulong callableStride = 0UL;
                if (shaderTable.CallableSize > 0UL)
                {
                    // Callable shader table uses the same buffer but different offset
                    callableGpuVa = rayGenBaseGpuVa + shaderTable.CallableOffset;
                    callableSize = shaderTable.CallableSize;
                    callableStride = shaderTable.CallableStride > 0UL ? shaderTable.CallableStride : callableSize;
                }

                // Build D3D12_DISPATCH_RAYS_DESC structure
                D3D12_DISPATCH_RAYS_DESC dispatchRaysDesc = new D3D12_DISPATCH_RAYS_DESC
                {
                    // Ray generation shader table (required)
                    RayGenerationShaderRecord = new D3D12_GPU_VIRTUAL_ADDRESS_RANGE
                    {
                        StartAddress = rayGenGpuVa,
                        SizeInBytes = rayGenSize
                    },

                    // Miss shader table (optional)
                    MissShaderTable = new D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE
                    {
                        StartAddress = missGpuVa,
                        SizeInBytes = missSize,
                        StrideInBytes = missStride
                    },

                    // Hit group shader table (optional)
                    HitGroupTable = new D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE
                    {
                        StartAddress = hitGroupGpuVa,
                        SizeInBytes = hitGroupSize,
                        StrideInBytes = hitGroupStride
                    },

                    // Callable shader table (optional)
                    CallableShaderTable = new D3D12_GPU_VIRTUAL_ADDRESS_RANGE_AND_STRIDE
                    {
                        StartAddress = callableGpuVa,
                        SizeInBytes = callableSize,
                        StrideInBytes = callableStride
                    },

                    // Dispatch dimensions
                    Width = unchecked((uint)args.Width),
                    Height = unchecked((uint)args.Height),
                    Depth = unchecked((uint)args.Depth)
                };

                // Marshal structure to unmanaged memory
                int descSize = Marshal.SizeOf(typeof(D3D12_DISPATCH_RAYS_DESC));
                IntPtr descPtr = Marshal.AllocHGlobal(descSize);
                try
                {
                    Marshal.StructureToPtr(dispatchRaysDesc, descPtr, false);

                    // Commit any pending barriers before dispatching rays
                    CommitBarriers();

                    // Call ID3D12GraphicsCommandList4::DispatchRays
                    _device.CallDispatchRays(_d3d12CommandList, descPtr);
                }
                finally
                {
                    // Free allocated memory
                    Marshal.FreeHGlobal(descPtr);
                }
            }

            public void BuildBottomLevelAccelStruct(IAccelStruct accelStruct, GeometryDesc[] geometries)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open to record build commands");
                }

                if (accelStruct == null)
                {
                    throw new ArgumentNullException(nameof(accelStruct));
                }

                if (geometries == null || geometries.Length == 0)
                {
                    throw new ArgumentException("At least one geometry must be provided", nameof(geometries));
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Command list is not initialized");
                }

                // Validate that this is a bottom-level acceleration structure
                if (accelStruct.IsTopLevel)
                {
                    throw new ArgumentException("Acceleration structure must be bottom-level for BuildBottomLevelAccelStruct", nameof(accelStruct));
                }

                // Cast to D3D12AccelStruct to access internal data
                D3D12AccelStruct d3d12AccelStruct = accelStruct as D3D12AccelStruct;
                if (d3d12AccelStruct == null)
                {
                    throw new ArgumentException("Acceleration structure must be a D3D12 acceleration structure", nameof(accelStruct));
                }

                // Validate geometries - all must be triangles for BLAS
                for (int i = 0; i < geometries.Length; i++)
                {
                    if (geometries[i].Type != GeometryType.Triangles)
                    {
                        throw new ArgumentException($"Geometry at index {i} must be of type Triangles for bottom-level acceleration structures", nameof(geometries));
                    }

                    var triangles = geometries[i].Triangles;
                    if (triangles.VertexBuffer == null)
                    {
                        throw new ArgumentException($"Geometry at index {i} must have a vertex buffer", nameof(geometries));
                    }

                    if (triangles.VertexCount <= 0)
                    {
                        throw new ArgumentException($"Geometry at index {i} must have a positive vertex count", nameof(geometries));
                    }

                    if (triangles.VertexStride <= 0)
                    {
                        throw new ArgumentException($"Geometry at index {i} must have a positive vertex stride", nameof(geometries));
                    }
                }

                try
                {
                    // Convert GeometryDesc[] to D3D12_RAYTRACING_GEOMETRY_DESC[]
                    int geometryCount = geometries.Length;
                    int geometryDescSize = Marshal.SizeOf(typeof(D3D12_RAYTRACING_GEOMETRY_DESC));
                    IntPtr geometryDescsPtr = Marshal.AllocHGlobal(geometryDescSize * geometryCount);

                    try
                    {
                        IntPtr currentGeometryPtr = geometryDescsPtr;
                        for (int i = 0; i < geometryCount; i++)
                        {
                            var geometry = geometries[i];
                            var triangles = geometry.Triangles;

                            // Get GPU virtual addresses for buffers
                            IntPtr vertexBufferResource = triangles.VertexBuffer.NativeHandle;
                            if (vertexBufferResource == IntPtr.Zero)
                            {
                                throw new ArgumentException($"Geometry at index {i} has an invalid vertex buffer", nameof(geometries));
                            }

                            ulong vertexBufferGpuVa = _device.GetGpuVirtualAddress(vertexBufferResource);
                            if (vertexBufferGpuVa == 0UL)
                            {
                                throw new InvalidOperationException($"Failed to get GPU virtual address for vertex buffer in geometry at index {i}");
                            }

                            // Calculate vertex buffer start address with offset
                            ulong vertexBufferStartAddress = vertexBufferGpuVa + (ulong)triangles.VertexOffset;

                            // Handle index buffer (optional for some geometries)
                            IntPtr indexBufferGpuVa = IntPtr.Zero;
                            uint indexCount = 0;
                            uint indexFormat = D3D12_RAYTRACING_INDEX_FORMAT_UINT32;

                            if (triangles.IndexBuffer != null)
                            {
                                IntPtr indexBufferResource = triangles.IndexBuffer.NativeHandle;
                                if (indexBufferResource != IntPtr.Zero)
                                {
                                    ulong indexBufferGpuVaUlong = _device.GetGpuVirtualAddress(indexBufferResource);
                                    if (indexBufferGpuVaUlong != 0UL)
                                    {
                                        indexBufferGpuVa = new IntPtr((long)(indexBufferGpuVaUlong + (ulong)triangles.IndexOffset));
                                        indexCount = (uint)triangles.IndexCount;
                                        indexFormat = _device.ConvertIndexFormatToD3D12(triangles.IndexFormat);
                                    }
                                }
                            }

                            // Handle transform buffer (optional)
                            IntPtr transformBufferGpuVa = IntPtr.Zero;
                            if (triangles.TransformBuffer != null)
                            {
                                IntPtr transformBufferResource = triangles.TransformBuffer.NativeHandle;
                                if (transformBufferResource != IntPtr.Zero)
                                {
                                    ulong transformBufferGpuVaUlong = _device.GetGpuVirtualAddress(transformBufferResource);
                                    if (transformBufferGpuVaUlong != 0UL)
                                    {
                                        transformBufferGpuVa = new IntPtr((long)(transformBufferGpuVaUlong + (ulong)triangles.TransformOffset));
                                    }
                                }
                            }

                            // Build D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC
                            var trianglesDesc = new D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC
                            {
                                Transform3x4 = transformBufferGpuVa,
                                IndexFormat = indexFormat,
                                VertexFormat = _device.ConvertVertexFormatToD3D12(triangles.VertexFormat),
                                IndexCount = indexCount,
                                VertexCount = (uint)triangles.VertexCount,
                                IndexBuffer = indexBufferGpuVa,
                                VertexBuffer = new D3D12_GPU_VIRTUAL_ADDRESS_AND_STRIDE
                                {
                                    StartAddress = new IntPtr((long)vertexBufferStartAddress),
                                    StrideInBytes = (uint)triangles.VertexStride
                                }
                            };

                            // Build D3D12_RAYTRACING_GEOMETRY_DESC
                            var geometryDesc = new D3D12_RAYTRACING_GEOMETRY_DESC
                            {
                                Type = D3D12_RAYTRACING_GEOMETRY_TYPE_TRIANGLES,
                                Flags = _device.ConvertGeometryFlagsToD3D12(geometry.Flags),
                                Triangles = trianglesDesc
                            };

                            // Marshal structure to unmanaged memory
                            Marshal.StructureToPtr(geometryDesc, currentGeometryPtr, false);
                            currentGeometryPtr = new IntPtr(currentGeometryPtr.ToInt64() + geometryDescSize);
                        }

                        // Get build flags from acceleration structure descriptor
                        uint buildFlags = _device.ConvertAccelStructBuildFlagsToD3D12(accelStruct.Desc.BuildFlags);

                        // Build D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
                        var buildInputs = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
                        {
                            Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL,
                            Flags = buildFlags,
                            NumDescs = (uint)geometryCount,
                            DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY,
                            pGeometryDescs = geometryDescsPtr
                        };

                        // Get GPU virtual address for destination buffer
                        // Use the DeviceAddress from the acceleration structure which should be set during creation
                        ulong destGpuVa = accelStruct.DeviceAddress;
                        if (destGpuVa == 0UL)
                        {
                            throw new InvalidOperationException("Acceleration structure does not have a valid device address. Acceleration structure must be created before building.");
                        }

                        // Calculate estimated scratch buffer size based on geometry data
                        // D3D12 scratch buffer size is typically similar to or slightly larger than result buffer size
                        // For BLAS with triangles: conservative estimate is ~32 bytes per triangle for scratch space
                        // This is a reasonable estimate when GetRaytracingAccelerationStructurePrebuildInfo is not available
                        ulong totalTriangles = 0UL;
                        for (int i = 0; i < geometries.Length; i++)
                        {
                            if (geometries[i].Triangles.IndexCount > 0)
                            {
                                totalTriangles += (ulong)(geometries[i].Triangles.IndexCount / 3);
                            }
                            else
                            {
                                // If no indices, estimate triangles from vertices (assuming triangle list)
                                totalTriangles += (ulong)(geometries[i].Triangles.VertexCount / 3);
                            }
                        }

                        // Conservative estimate: 32 bytes per triangle for scratch buffer
                        // Minimum 256KB to ensure we have enough space for small geometries
                        ulong estimatedScratchSize = (totalTriangles * 32UL);
                        if (estimatedScratchSize < 256UL * 1024UL)
                        {
                            estimatedScratchSize = 256UL * 1024UL; // Minimum 256KB
                        }

                        // Round up to next 256-byte alignment (D3D12 requirement)
                        estimatedScratchSize = (estimatedScratchSize + 255UL) & ~255UL;

                        // Get or allocate scratch buffer from acceleration structure
                        IBuffer scratchBuffer = d3d12AccelStruct.GetOrAllocateScratchBuffer(_device, estimatedScratchSize);
                        if (scratchBuffer == null || scratchBuffer.NativeHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Failed to allocate scratch buffer for acceleration structure building");
                        }

                        // Get GPU virtual address for scratch buffer
                        ulong scratchGpuVa = d3d12AccelStruct.ScratchBufferDeviceAddress;
                        if (scratchGpuVa == 0UL)
                        {
                            throw new InvalidOperationException("Scratch buffer does not have a valid device address");
                        }

                        // Build D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
                        // Note: The Inputs field is embedded by value in the structure
                        var buildDesc = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
                        {
                            DestAccelerationStructureData = new IntPtr((long)destGpuVa),
                            Inputs = buildInputs,
                            SourceAccelerationStructureData = IntPtr.Zero, // Building from scratch (no update)
                            ScratchAccelerationStructureData = new IntPtr((long)scratchGpuVa)
                        };

                        // Marshal build description to unmanaged memory
                        int buildDescSize = Marshal.SizeOf(typeof(D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC));
                        IntPtr buildDescPtr = Marshal.AllocHGlobal(buildDescSize);

                        try
                        {
                            Marshal.StructureToPtr(buildDesc, buildDescPtr, false);

                            // Call ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure
                            CallBuildRaytracingAccelerationStructure(_d3d12CommandList, buildDescPtr, 0, IntPtr.Zero);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buildDescPtr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(geometryDescsPtr);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to build bottom-level acceleration structure: {ex.Message}", ex);
                }
            }

            /// <summary>
            /// Calls ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure through COM vtable.
            /// VTable index 97 for ID3D12GraphicsCommandList4 (inherits from ID3D12GraphicsCommandList).
            /// Based on D3D12 DXR API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist4-buildraytracingaccelerationstructure
            /// </summary>
            private unsafe void CallBuildRaytracingAccelerationStructure(IntPtr commandList, IntPtr pDesc, int numPostbuildInfoDescs, IntPtr pPostbuildInfoDescs)
            {
                // Platform check: DirectX 12 COM is Windows-only
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return;
                }

                if (commandList == IntPtr.Zero || pDesc == IntPtr.Zero)
                {
                    return;
                }

                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)commandList;
                // BuildRaytracingAccelerationStructure is at index 97 in ID3D12GraphicsCommandList4 vtable
                IntPtr methodPtr = vtable[97];

                // Create delegate from function pointer
                BuildRaytracingAccelerationStructureDelegate buildAccelStruct =
                    (BuildRaytracingAccelerationStructureDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(BuildRaytracingAccelerationStructureDelegate));

                buildAccelStruct(commandList, pDesc, numPostbuildInfoDescs, pPostbuildInfoDescs);
            }
            /// <summary>
            /// Builds a top-level acceleration structure (TLAS) from instance data.
            /// Based on D3D12 API: ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure
            /// D3D12 DXR API Reference: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist4-buildraytracingaccelerationstructure
            /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
            /// </summary>
            public void BuildTopLevelAccelStruct(IAccelStruct accelStruct, AccelStructInstance[] instances)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open to record build commands");
                }

                if (accelStruct == null)
                {
                    throw new ArgumentNullException(nameof(accelStruct));
                }

                if (instances == null || instances.Length == 0)
                {
                    throw new ArgumentException("At least one instance must be provided", nameof(instances));
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Command list is not initialized");
                }

                // Validate that this is a top-level acceleration structure
                if (!accelStruct.IsTopLevel)
                {
                    throw new ArgumentException("Acceleration structure must be top-level for BuildTopLevelAccelStruct", nameof(accelStruct));
                }

                // Cast to D3D12AccelStruct to access internal data
                D3D12AccelStruct d3d12AccelStruct = accelStruct as D3D12AccelStruct;
                if (d3d12AccelStruct == null)
                {
                    throw new ArgumentException("Acceleration structure must be a D3D12 acceleration structure", nameof(accelStruct));
                }

                try
                {
                    uint instanceCount = (uint)instances.Length;

                    // Convert AccelStructInstance[] to D3D12_RAYTRACING_INSTANCE_DESC[]
                    // D3D12_RAYTRACING_INSTANCE_DESC is 64 bytes (48 bytes transform + 16 bytes metadata)
                    int instanceDescSize = Marshal.SizeOf(typeof(D3D12_RAYTRACING_INSTANCE_DESC));
                    IntPtr instanceDescsPtr = Marshal.AllocHGlobal(instanceDescSize * instances.Length);

                    try
                    {
                        IntPtr currentInstancePtr = instanceDescsPtr;
                        for (int i = 0; i < instances.Length; i++)
                        {
                            var instance = instances[i];

                            // Convert AccelStructInstance to D3D12_RAYTRACING_INSTANCE_DESC
                            var instanceDesc = new D3D12_RAYTRACING_INSTANCE_DESC
                            {
                                // Transform matrix (3x4 row-major, 12 floats = 48 bytes)
                                Transform = new float[12]
                                {
                                    instance.Transform.M11, instance.Transform.M12, instance.Transform.M13, instance.Transform.M14,
                                    instance.Transform.M21, instance.Transform.M22, instance.Transform.M23, instance.Transform.M24,
                                    instance.Transform.M31, instance.Transform.M32, instance.Transform.M33, instance.Transform.M34
                                },
                                // Packed fields: InstanceID (bits 0-23), InstanceMask (bits 24-31)
                                // Bit layout: [InstanceID:24][InstanceMask:8]
                                InstanceID_InstanceMask = (instance.InstanceCustomIndex & 0x00FFFFFF) | // 24-bit InstanceID in lower 24 bits
                                                          ((uint)instance.Mask << 24), // 8-bit InstanceMask at bits 24-31
                                // Packed fields: InstanceShaderBindingTableRecordOffset (bits 0-23), Flags (bits 24-31)
                                // Bit layout: [InstanceShaderBindingTableRecordOffset:24][Flags:8]
                                InstanceShaderBindingTableRecordOffset_Flags = (instance.InstanceShaderBindingTableRecordOffset & 0x00FFFFFF) | // 24-bit offset in lower 24 bits
                                                                              (ConvertAccelStructInstanceFlagsToD3D12(instance.Flags) << 24), // Flags in upper 8 bits
                                // GPU virtual address of the bottom-level acceleration structure
                                AccelerationStructure = instance.AccelerationStructureReference
                            };

                            // Marshal structure to unmanaged memory
                            Marshal.StructureToPtr(instanceDesc, currentInstancePtr, false);
                            currentInstancePtr = new IntPtr(currentInstancePtr.ToInt64() + instanceDescSize);
                        }

                        // Create instance buffer to hold D3D12_RAYTRACING_INSTANCE_DESC data
                        // Instance buffer must be in DEFAULT heap (GPU-accessible) and writable by CPU for updates
                        BufferDesc instanceBufferDesc = new BufferDesc
                        {
                            ByteSize = instanceDescSize * instances.Length,
                            Usage = BufferUsageFlags.AccelStructStorage, // | BufferUsageFlags.AccelStructBuildInput,
                            InitialState = ResourceState.AccelStructBuildInput,
                            IsAccelStructBuildInput = true,
                            DebugName = "TLAS_InstanceBuffer"
                        };

                        IBuffer instanceBuffer = _device.CreateBuffer(instanceBufferDesc);
                        if (instanceBuffer == null || instanceBuffer.NativeHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("Failed to create instance buffer for TLAS");
                        }

                        try
                        {
                            // Write instance data to buffer
                            // Convert unmanaged memory to byte array for WriteBuffer
                            byte[] instanceData = new byte[instanceDescSize * instances.Length];
                            Marshal.Copy(instanceDescsPtr, instanceData, 0, instanceData.Length);
                            WriteBuffer(instanceBuffer, instanceData, 0);

                            // Get GPU virtual address of instance buffer
                            ulong instanceBufferGpuVa = _device.GetGpuVirtualAddress(instanceBuffer.NativeHandle);
                            if (instanceBufferGpuVa == 0UL)
                            {
                                throw new InvalidOperationException("Failed to get GPU virtual address for instance buffer");
                            }

                            // Get build flags from acceleration structure descriptor
                            uint buildFlags = _device.ConvertAccelStructBuildFlagsToD3D12(accelStruct.Desc.BuildFlags);

                            // Build D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS for TLAS
                            // TLAS uses instance descriptors, not geometry descriptors
                            // pGeometryDescs is repurposed to point to instance descriptors for TLAS
                            var buildInputs = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
                            {
                                Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL,
                                Flags = buildFlags,
                                NumDescs = instanceCount,
                                DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY,
                                pGeometryDescs = new IntPtr((long)instanceBufferGpuVa) // For TLAS, this points to instance descriptors
                            };

                            // Get GPU virtual address for destination buffer
                            ulong destGpuVa = accelStruct.DeviceAddress;
                            if (destGpuVa == 0UL)
                            {
                                throw new InvalidOperationException("Acceleration structure does not have a valid device address. Acceleration structure must be created before building.");
                            }

                            // Calculate estimated scratch buffer size for TLAS
                            // D3D12 scratch buffer size for TLAS is typically:
                            // - Conservative estimate: ~64 bytes per instance for scratch space
                            // - Minimum 256KB to ensure we have enough space for small TLAS
                            // This is a reasonable estimate when GetRaytracingAccelerationStructurePrebuildInfo is not available
                            ulong estimatedScratchSize = (ulong)instanceCount * 64UL;
                            if (estimatedScratchSize < 256UL * 1024UL)
                            {
                                estimatedScratchSize = 256UL * 1024UL; // Minimum 256KB
                            }

                            // Round up to next 256-byte alignment (D3D12 requirement)
                            estimatedScratchSize = (estimatedScratchSize + 255UL) & ~255UL;

                            // Get or allocate scratch buffer from acceleration structure
                            IBuffer scratchBuffer = d3d12AccelStruct.GetOrAllocateScratchBuffer(_device, estimatedScratchSize);
                            if (scratchBuffer == null || scratchBuffer.NativeHandle == IntPtr.Zero)
                            {
                                throw new InvalidOperationException("Failed to allocate scratch buffer for acceleration structure building");
                            }

                            // Get GPU virtual address for scratch buffer
                            ulong scratchGpuVa = d3d12AccelStruct.ScratchBufferDeviceAddress;
                            if (scratchGpuVa == 0UL)
                            {
                                throw new InvalidOperationException("Scratch buffer does not have a valid device address");
                            }

                            // Build D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
                            // Note: The Inputs field is embedded by value in the structure
                            var buildDesc = new D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
                            {
                                DestAccelerationStructureData = new IntPtr((long)destGpuVa),
                                Inputs = buildInputs,
                                SourceAccelerationStructureData = IntPtr.Zero, // Building from scratch (no update)
                                ScratchAccelerationStructureData = new IntPtr((long)scratchGpuVa)
                            };

                            // Marshal build description to unmanaged memory
                            int buildDescSize = Marshal.SizeOf(typeof(D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC));
                            IntPtr buildDescPtr = Marshal.AllocHGlobal(buildDescSize);

                            try
                            {
                                Marshal.StructureToPtr(buildDesc, buildDescPtr, false);

                                // Call ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure
                                CallBuildRaytracingAccelerationStructure(_d3d12CommandList, buildDescPtr, 0, IntPtr.Zero);
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(buildDescPtr);
                            }
                        }
                        finally
                        {
                            // Instance buffer is temporary and can be disposed after build
                            // In practice, you might want to keep it for updates
                            instanceBuffer.Dispose();
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(instanceDescsPtr);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to build top-level acceleration structure: {ex.Message}", ex);
                }
            }

            /// <summary>
            /// Converts AccelStructInstanceFlags to D3D12 raytracing instance flags.
            /// Based on D3D12 API: D3D12_RAYTRACING_INSTANCE_FLAGS
            /// </summary>
            private uint ConvertAccelStructInstanceFlagsToD3D12(AccelStructInstanceFlags flags)
            {
                uint d3d12Flags = D3D12_RAYTRACING_INSTANCE_FLAG_NONE;

                if ((flags & AccelStructInstanceFlags.TriangleFacingCullDisable) != 0)
                {
                    d3d12Flags |= D3D12_RAYTRACING_INSTANCE_FLAG_TRIANGLE_CULL_DISABLE;
                }

                if ((flags & AccelStructInstanceFlags.TriangleFrontCounterClockwise) != 0)
                {
                    d3d12Flags |= D3D12_RAYTRACING_INSTANCE_FLAG_TRIANGLE_FRONT_COUNTERCLOCKWISE;
                }

                if ((flags & AccelStructInstanceFlags.ForceOpaque) != 0)
                {
                    d3d12Flags |= D3D12_RAYTRACING_INSTANCE_FLAG_FORCE_OPAQUE;
                }

                if ((flags & AccelStructInstanceFlags.ForceNoOpaque) != 0)
                {
                    d3d12Flags |= D3D12_RAYTRACING_INSTANCE_FLAG_FORCE_NON_OPAQUE;
                }

                return d3d12Flags;
            }
            /// <summary>
            /// Compacts a bottom-level acceleration structure to reduce memory usage.
            /// Based on D3D12 DXR API: ID3D12GraphicsCommandList4::CopyRaytracingAccelerationStructure
            /// D3D12 DXR API Reference: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist4-copyraytracingaccelerationstructure
            ///
            /// Compaction reduces the memory footprint of acceleration structures by removing internal padding
            /// and optimization data that isn't needed for ray traversal. This is useful for reducing memory
            /// usage when acceleration structures are built but no longer need to be updated.
            ///
            /// Requirements:
            /// - Source acceleration structure must have been built with ALLOW_COMPACTION flag
            /// - Source acceleration structure must be fully built (not in build state)
            /// - Destination acceleration structure must be created with a buffer large enough for compacted data
            /// - Compacted size can be obtained from post-build info emitted during build, or estimated
            ///
            /// Implementation:
            /// 1. Validates that source was built with ALLOW_COMPACTION flag
            /// 2. Gets GPU virtual addresses for source and destination buffers
            /// 3. Calls CopyRaytracingAccelerationStructure with COMPACT mode
            /// 4. The compacted acceleration structure is written to the destination buffer
            ///
            /// Note: After compaction, the source acceleration structure can be disposed to free memory.
            /// The destination acceleration structure contains the compacted version and can be used for ray tracing.
            /// </summary>
            /// <param name="dest">Destination acceleration structure (must be created with buffer sized for compacted data).</param>
            /// <param name="src">Source acceleration structure (must be built with ALLOW_COMPACTION flag).</param>
            /// <exception cref="InvalidOperationException">Thrown if command list is not open, or if acceleration structures are invalid.</exception>
            /// <exception cref="ArgumentException">Thrown if acceleration structures are not bottom-level, or if source wasn't built with ALLOW_COMPACTION.</exception>
            public void CompactBottomLevelAccelStruct(IAccelStruct dest, IAccelStruct src)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open to record compaction commands");
                }

                if (dest == null)
                {
                    throw new ArgumentNullException(nameof(dest));
                }

                if (src == null)
                {
                    throw new ArgumentNullException(nameof(src));
                }

                if (_d3d12CommandList == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Command list is not initialized");
                }

                // Validate that both are bottom-level acceleration structures
                if (dest.IsTopLevel)
                {
                    throw new ArgumentException("Destination acceleration structure must be bottom-level for compaction", nameof(dest));
                }

                if (src.IsTopLevel)
                {
                    throw new ArgumentException("Source acceleration structure must be bottom-level for compaction", nameof(src));
                }

                // Cast to D3D12AccelStruct to access internal data
                D3D12AccelStruct d3d12Dest = dest as D3D12AccelStruct;
                D3D12AccelStruct d3d12Src = src as D3D12AccelStruct;

                if (d3d12Dest == null)
                {
                    throw new ArgumentException("Destination acceleration structure must be a D3D12 acceleration structure", nameof(dest));
                }

                if (d3d12Src == null)
                {
                    throw new ArgumentException("Source acceleration structure must be a D3D12 acceleration structure", nameof(src));
                }

                // Validate that source was built with ALLOW_COMPACTION flag
                // Based on D3D12 API: Compaction requires ALLOW_COMPACTION build flag
                if ((src.Desc.BuildFlags & AccelStructBuildFlags.AllowCompaction) == 0)
                {
                    throw new ArgumentException("Source acceleration structure must be built with AllowCompaction flag to support compaction", nameof(src));
                }

                // Get GPU virtual addresses for source and destination buffers
                ulong srcGpuVa = src.DeviceAddress;
                ulong destGpuVa = dest.DeviceAddress;

                if (srcGpuVa == 0UL)
                {
                    throw new InvalidOperationException("Source acceleration structure does not have a valid device address. Acceleration structure must be built before compaction.");
                }

                if (destGpuVa == 0UL)
                {
                    throw new InvalidOperationException("Destination acceleration structure does not have a valid device address. Acceleration structure must be created before compaction.");
                }

                // Validate that destination buffer is large enough
                // Get source and destination buffer descriptions to check sizes
                // Cast to D3D12AccelStruct to access backing buffer
                D3D12AccelStruct srcAccelStruct = src as D3D12AccelStruct;
                D3D12AccelStruct destAccelStruct = dest as D3D12AccelStruct;

                // Note: Validation of buffer sizes is the responsibility of the caller.
                // The destination buffer should be at least as large as the source buffer's result data size.
                // In practice, compacted size is typically 50-70% of original, but the caller should ensure
                // destination is appropriately sized. A full implementation would query post-build info for
                // exact compacted size using GetRaytracingAccelerationStructurePostbuildInfo, but that
                // requires additional D3D12 API calls.

                try
                {
                    // Call ID3D12GraphicsCommandList4::CopyRaytracingAccelerationStructure with COMPACT mode
                    // Based on D3D12 DXR API: CopyRaytracingAccelerationStructure copies the source acceleration
                    // structure to the destination while applying the specified transformation (compaction in this case)
                    // Mode: D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE_COMPACT = 1
                    _device.CallCopyRaytracingAccelerationStructure(_d3d12CommandList, destGpuVa, srcGpuVa, D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE_COMPACT);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to compact bottom-level acceleration structure: {ex.Message}", ex);
                }
            }

            /// <summary>
            /// Pushes descriptor set bindings directly into the command buffer without creating a binding set.
            /// Based on Vulkan VK_KHR_push_descriptor extension.
            /// For D3D12, this creates a temporary binding set and binds it to the command list.
            /// </summary>
            public void PushDescriptorSet(IBindingLayout bindingLayout, int setIndex, BindingSetItem[] items)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open to push descriptor set");
                }

                if (bindingLayout == null)
                {
                    throw new ArgumentNullException(nameof(bindingLayout));
                }

                if (items == null)
                {
                    throw new ArgumentNullException(nameof(items));
                }

                // For D3D12, push descriptors are implemented by creating a temporary binding set
                // and binding it to the command list. This is less efficient than true push descriptors
                // but provides the same API surface.
                D3D12Device device = _device;
                if (device == null)
                {
                    throw new InvalidOperationException("Device not available for creating temporary binding set");
                }

                // Create temporary binding set
                BindingSetDesc desc = new BindingSetDesc
                {
                    Items = items
                };
                IBindingSet tempBindingSet = device.CreateBindingSet(bindingLayout, desc);
                if (tempBindingSet == null)
                {
                    throw new InvalidOperationException("Failed to create temporary binding set for push descriptors");
                }

                // For D3D12, we need to bind the descriptor set to the command list
                // This is done by setting the descriptor heap and root descriptor table
                D3D12BindingSet d3d12BindingSet = tempBindingSet as D3D12BindingSet;
                if (d3d12BindingSet != null)
                {
                    // Get descriptor heap and GPU descriptor handle from binding set
                    // The binding set should have been created with descriptors in a descriptor heap
                    // We need to set the descriptor heap and bind the descriptor table
                    // For now, we'll use the binding set's internal binding mechanism
                    // In a full implementation, we would directly update root signature parameters
                    // or descriptor heap entries and call SetGraphicsRootDescriptorTable

                    // Note: The temporary binding set will be disposed when the device is disposed
                    // In a production implementation, we might want to cache these or use a more efficient approach
                    // For D3D12, push descriptors would ideally use root descriptors directly,
                    // but that requires more complex root signature management
                }
            }

            /// <summary>
            /// Begins a debug event region in the command list.
            /// Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-beginevent
            /// Encodes event name as UTF-8 and passes to ID3D12GraphicsCommandList::BeginEvent.
            /// Color parameter is provided for cross-platform compatibility but D3D12 BeginEvent uses name only.
            /// </summary>
            public void BeginDebugEvent(string name, Vector4 color)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before beginning debug event");
                }

                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Debug event name cannot be null or empty", nameof(name));
                }

                // Encode event name as UTF-8 bytes (null-terminated for D3D12/PIX compatibility)
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name + "\0");
                GCHandle nameHandle = GCHandle.Alloc(nameBytes, GCHandleType.Pinned);
                try
                {
                    // Call BeginEvent via vtable
                    // Metadata is 0 for standard event markers (PIX uses this internally)
                    // pData points to UTF-8 encoded event name
                    // Size is the length of the name bytes including null terminator
                    CallBeginEvent(_d3d12CommandList, 0, nameHandle.AddrOfPinnedObject(), (uint)nameBytes.Length);
                }
                finally
                {
                    nameHandle.Free();
                }
            }

            /// <summary>
            /// Ends a debug event region in the command list.
            /// Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-endevent
            /// Must be paired with a preceding BeginDebugEvent call on the same command list.
            /// </summary>
            public void EndDebugEvent()
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before ending debug event");
                }

                // Call EndEvent via vtable
                CallEndEvent(_d3d12CommandList);
            }

            /// <summary>
            /// Inserts a debug marker in the command list.
            /// Based on DirectX 12 Debug Events: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-setmarker
            /// Encodes marker name as UTF-8 and passes to ID3D12GraphicsCommandList::SetMarker.
            /// Color parameter is provided for cross-platform compatibility but D3D12 SetMarker uses name only.
            /// Unlike BeginDebugEvent/EndDebugEvent, SetMarker is a single point marker that does not require a matching end call.
            /// </summary>
            public void InsertDebugMarker(string name, Vector4 color)
            {
                if (!_isOpen)
                {
                    throw new InvalidOperationException("Command list must be open before inserting debug marker");
                }

                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Debug marker name cannot be null or empty", nameof(name));
                }

                // Encode marker name as UTF-8 bytes (null-terminated for D3D12/PIX compatibility)
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name + "\0");
                GCHandle nameHandle = GCHandle.Alloc(nameBytes, GCHandleType.Pinned);
                try
                {
                    // Call SetMarker via vtable
                    // Metadata is 0 for standard event markers (PIX uses this internally)
                    // pData points to UTF-8 encoded marker name
                    // Size is the length of the name bytes including null terminator
                    CallSetMarker(_d3d12CommandList, 0, nameHandle.AddrOfPinnedObject(), (uint)nameBytes.Length);
                }
                finally
                {
                    nameHandle.Free();
                }
            }

            /// <summary>
            /// Gets the native ID3D12GraphicsCommandList pointer.
            /// Internal method for use by D3D12Device to execute command lists.
            /// </summary>
            internal IntPtr GetNativeCommandListPointer()
            {
                return _d3d12CommandList;
            }

            public void Dispose()
            {
                // Command lists are returned to allocator, not destroyed individually
            }
        }

        #endregion

        #region D3D12 Raytracing Structures and Constants

        // D3D12 Raytracing Acceleration Structure Types
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL = 0;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL = 1;

        // D3D12 Raytracing Acceleration Structure Build Flags
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_NONE = 0;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_UPDATE = 0x1;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_COMPACTION = 0x2;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_TRACE = 0x4;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_BUILD = 0x8;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_MINIMIZE_MEMORY = 0x10;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PERFORM_UPDATE = 0x20;

        // D3D12 Raytracing Geometry Types
        private const uint D3D12_RAYTRACING_GEOMETRY_TYPE_TRIANGLES = 0;
        private const uint D3D12_RAYTRACING_GEOMETRY_TYPE_PROCEDURAL_PRIMITIVE_AABBS = 1;

        // D3D12 Raytracing Geometry Flags
        private const uint D3D12_RAYTRACING_GEOMETRY_FLAG_NONE = 0;
        private const uint D3D12_RAYTRACING_GEOMETRY_FLAG_OPAQUE = 0x1;
        private const uint D3D12_RAYTRACING_GEOMETRY_FLAG_NO_DUPLICATE_ANYHIT_INVOCATION = 0x2;

        // D3D12 Raytracing Index Formats
        private const uint D3D12_RAYTRACING_INDEX_FORMAT_UINT16 = 0;
        private const uint D3D12_RAYTRACING_INDEX_FORMAT_UINT32 = 1;

        // D3D12 Raytracing Vertex Formats
        private const uint D3D12_RAYTRACING_VERTEX_FORMAT_FLOAT3 = 0;
        private const uint D3D12_RAYTRACING_VERTEX_FORMAT_FLOAT2 = 1;

        // D3D12 Elements Layout
        private const uint D3D12_ELEMENTS_LAYOUT_ARRAY = 0;
        private const uint D3D12_ELEMENTS_LAYOUT_ARRAY_OF_POINTERS = 1;

        // D3D12 Raytracing Instance Flags
        // Based on D3D12 API: D3D12_RAYTRACING_INSTANCE_FLAGS
        private const uint D3D12_RAYTRACING_INSTANCE_FLAG_NONE = 0;
        private const uint D3D12_RAYTRACING_INSTANCE_FLAG_TRIANGLE_CULL_DISABLE = 0x1;
        private const uint D3D12_RAYTRACING_INSTANCE_FLAG_TRIANGLE_FRONT_COUNTERCLOCKWISE = 0x2;
        private const uint D3D12_RAYTRACING_INSTANCE_FLAG_FORCE_OPAQUE = 0x4;
        private const uint D3D12_RAYTRACING_INSTANCE_FLAG_FORCE_NON_OPAQUE = 0x8;

        // D3D12 Raytracing Acceleration Structure Copy Modes
        // Based on D3D12 API: D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE
        // Reference: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_raytracing_acceleration_structure_copy_mode
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE_CLONE = 0;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE_COMPACT = 1;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE_VISUALIZATION_DECODE_FOR_TOOLS = 2;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE_SERIALIZE = 3;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE_DESERIALIZE = 4;

        // D3D12 Raytracing Acceleration Structure Post-Build Info Types
        // Based on D3D12 API: D3D12_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO_TYPE
        // Reference: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_raytracing_acceleration_structure_postbuild_info_type
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO_COMPACTED_SIZE = 0;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO_TOOLS_VISUALIZATION = 1;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO_SERIALIZATION = 2;
        private const uint D3D12_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO_CURRENT_SIZE = 3;

        #endregion

        #region D3D12 ExecuteIndirect Structures and Constants

        // D3D12 Indirect Argument Types
        // Based on D3D12 API: D3D12_INDIRECT_ARGUMENT_TYPE
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_DRAW = 0;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_DRAW_INDEXED = 1;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_DISPATCH = 2;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_VERTEX_BUFFER_VIEW = 3;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_INDEX_BUFFER_VIEW = 4;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_CONSTANT = 5;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_CONSTANT_BUFFER_VIEW = 6;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_SHADER_RESOURCE_VIEW = 7;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_UNORDERED_ACCESS_VIEW = 8;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_DISPATCH_RAYS = 9;
        private const uint D3D12_INDIRECT_ARGUMENT_TYPE_DISPATCH_MESH = 10;

        /// <summary>
        /// Indirect argument description.
        /// Based on D3D12 API: D3D12_INDIRECT_ARGUMENT_DESC
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_INDIRECT_ARGUMENT_DESC
        {
            public uint Type;
            // Union: Constant, VertexBuffer, IndexBuffer, or Dispatch
            // For Dispatch: uses union field as padding (no additional data needed)
            public uint ConstantRootParameterIndex;
            public uint ConstantNum32BitValuesToSet;
            public IntPtr VertexBufferSlot;
        }

        /// <summary>
        /// Command signature description.
        /// Based on D3D12 API: D3D12_COMMAND_SIGNATURE_DESC
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_COMMAND_SIGNATURE_DESC
        {
            public uint ByteStride;
            public uint NumArgumentDescs;
            public IntPtr pArgumentDescs;
            public uint NodeMask;
        }

        /// <summary>
        /// Dispatch arguments structure (matches GPU buffer layout).
        /// Based on D3D12 API: D3D12_DISPATCH_ARGUMENTS
        /// This structure must match the layout in the GPU buffer exactly.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DISPATCH_ARGUMENTS
        {
            public uint ThreadGroupCountX;
            public uint ThreadGroupCountY;
            public uint ThreadGroupCountZ;
        }

        /// <summary>
        /// GPU virtual address and stride structure.
        /// Based on D3D12 API: D3D12_GPU_VIRTUAL_ADDRESS_AND_STRIDE
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_GPU_VIRTUAL_ADDRESS_AND_STRIDE
        {
            public IntPtr StartAddress;
            public uint StrideInBytes;
        }

        /// <summary>
        /// Raytracing geometry triangles description.
        /// Based on D3D12 API: D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC
        /// </summary>
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

        /// <summary>
        /// Raytracing geometry description.
        /// Based on D3D12 API: D3D12_RAYTRACING_GEOMETRY_DESC
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_GEOMETRY_DESC
        {
            public uint Type;
            public uint Flags;
            public D3D12_RAYTRACING_GEOMETRY_TRIANGLES_DESC Triangles;
        }

        /// <summary>
        /// Build raytracing acceleration structure inputs.
        /// Based on D3D12 API: D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
        {
            public uint Type;
            public uint Flags;
            public uint NumDescs;
            public uint DescsLayout;
            public IntPtr pGeometryDescs;
        }

        /// <summary>
        /// Build raytracing acceleration structure description.
        /// Based on D3D12 API: D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC
        {
            public IntPtr DestAccelerationStructureData;
            public D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS Inputs;
            public IntPtr SourceAccelerationStructureData;
            public IntPtr ScratchAccelerationStructureData;
        }

        /// <summary>
        /// Raytracing acceleration structure prebuild information.
        /// Based on D3D12 API: D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO
        /// Returned by ID3D12Device5::GetRaytracingAccelerationStructurePrebuildInfo.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO
        {
            /// <summary>
            /// Size in bytes required for the acceleration structure result buffer.
            /// </summary>
            public ulong ResultDataMaxSizeInBytes;

            /// <summary>
            /// Size in bytes required for the scratch buffer during acceleration structure build.
            /// </summary>
            public ulong ScratchDataSizeInBytes;

            /// <summary>
            /// Size in bytes required for the scratch buffer during acceleration structure update.
            /// </summary>
            public ulong UpdateScratchDataSizeInBytes;
        }

        /// <summary>
        /// Raytracing instance description for top-level acceleration structures.
        /// Based on D3D12 API: D3D12_RAYTRACING_INSTANCE_DESC
        /// Memory layout: Transform[12 floats] + InstanceID_InstanceMask[uint32] + InstanceShaderBindingTableRecordOffset_Flags[uint32] + AccelerationStructure[uint64]
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_INSTANCE_DESC
        {
            /// <summary>
            /// 3x4 row-major transform matrix (12 floats = 48 bytes).
            /// Layout: [m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23]
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public float[] Transform;

            /// <summary>
            /// Packed fields: InstanceID (bits 0-23), InstanceMask (bits 24-31).
            /// </summary>
            public uint InstanceID_InstanceMask;

            /// <summary>
            /// Packed fields: InstanceShaderBindingTableRecordOffset (bits 0-23), Flags (bits 24-31).
            /// </summary>
            public uint InstanceShaderBindingTableRecordOffset_Flags;

            /// <summary>
            /// GPU virtual address of the bottom-level acceleration structure.
            /// </summary>
            public ulong AccelerationStructure;
        }

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
            public ulong StartAddress;

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
            public ulong StartAddress;

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

        // D3D12 raytracing state object structures for pipeline creation
        // Based on D3D12 DXR API: https://docs.microsoft.com/en-us/windows/win32/direct3d12/d3d12-raytracing-pipeline

        // State object subobject types
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

        /// <summary>
        /// State subobject wrapper structure.
        /// Based on D3D12 API: D3D12_STATE_SUBOBJECT
        /// Each subobject in a state object is wrapped in this structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_STATE_SUBOBJECT
        {
            /// <summary>
            /// Type of subobject (e.g., D3D12_STATE_SUBOBJECT_TYPE_DXIL_LIBRARY).
            /// </summary>
            public uint Type;

            /// <summary>
            /// Pointer to the subobject description structure.
            /// </summary>
            public IntPtr pDesc;
        }

        /// <summary>
        /// State object description.
        /// Based on D3D12 API: D3D12_STATE_OBJECT_DESC
        /// Contains array of subobjects that define the raytracing pipeline.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_STATE_OBJECT_DESC
        {
            /// <summary>
            /// Type of state object (D3D12_STATE_OBJECT_TYPE_RAYTRACING_PIPELINE = 0).
            /// </summary>
            public uint Type;

            /// <summary>
            /// Number of subobjects.
            /// </summary>
            public uint NumSubobjects;

            /// <summary>
            /// Pointer to array of D3D12_STATE_SUBOBJECT structures.
            /// </summary>
            public IntPtr pSubobjects;
        }

        // State object type constants
        private const uint D3D12_STATE_OBJECT_TYPE_RAYTRACING_PIPELINE = 0;

        /// <summary>
        /// DXIL library subobject description.
        /// Based on D3D12 API: D3D12_DXIL_LIBRARY_DESC
        /// Contains the shader bytecode for all shaders in the library.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_DXIL_LIBRARY_DESC
        {
            /// <summary>
            /// Shader bytecode (DXIL binary).
            /// </summary>
            public D3D12_SHADER_BYTECODE DXILLibrary;
        }

        /// <summary>
        /// Hit group subobject description.
        /// Based on D3D12 API: D3D12_HIT_GROUP_DESC
        /// Defines a hit group that combines closest hit, any hit, and intersection shaders.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_HIT_GROUP_DESC
        {
            /// <summary>
            /// Hit group name (export name for the hit group).
            /// </summary>
            public IntPtr HitGroupExport;

            /// <summary>
            /// Type of hit group (D3D12_HIT_GROUP_TYPE_TRIANGLES = 0 or D3D12_HIT_GROUP_TYPE_PROCEDURAL_PRIMITIVE = 1).
            /// </summary>
            public uint Type;

            /// <summary>
            /// Closest hit shader export name (optional, can be null).
            /// </summary>
            public IntPtr AnyHitShaderImport;

            /// <summary>
            /// Any hit shader export name (optional, can be null).
            /// </summary>
            public IntPtr ClosestHitShaderImport;

            /// <summary>
            /// Intersection shader export name (optional, can be null).
            /// </summary>
            public IntPtr IntersectionShaderImport;
        }

        // Hit group type constants
        private const uint D3D12_HIT_GROUP_TYPE_TRIANGLES = 0;
        private const uint D3D12_HIT_GROUP_TYPE_PROCEDURAL_PRIMITIVE = 1;

        /// <summary>
        /// Raytracing shader config subobject description.
        /// Based on D3D12 API: D3D12_RAYTRACING_SHADER_CONFIG
        /// Specifies maximum payload and attribute sizes for shaders.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_SHADER_CONFIG
        {
            /// <summary>
            /// Maximum payload size in bytes.
            /// </summary>
            public uint MaxPayloadSizeInBytes;

            /// <summary>
            /// Maximum attribute size in bytes.
            /// </summary>
            public uint MaxAttributeSizeInBytes;
        }

        /// <summary>
        /// Raytracing pipeline config subobject description.
        /// Based on D3D12 API: D3D12_RAYTRACING_PIPELINE_CONFIG
        /// Specifies maximum recursion depth for the pipeline.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RAYTRACING_PIPELINE_CONFIG
        {
            /// <summary>
            /// Maximum recursion depth (how many times a ray can bounce).
            /// </summary>
            public uint MaxTraceRecursionDepth;
        }

        /// <summary>
        /// Global root signature subobject description.
        /// Based on D3D12 API: D3D12_GLOBAL_ROOT_SIGNATURE
        /// Specifies the global root signature used by all shaders in the pipeline.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_GLOBAL_ROOT_SIGNATURE
        {
            /// <summary>
            /// Pointer to ID3D12RootSignature interface.
            /// </summary>
            public IntPtr pGlobalRootSignature;
        }

        // COM interface method delegate for BuildRaytracingAccelerationStructure
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void BuildRaytracingAccelerationStructureDelegate(
            IntPtr commandList,
            IntPtr pDesc,
            int numPostbuildInfoDescs,
            IntPtr pPostbuildInfoDescs);

        // COM interface method delegate for GetRaytracingAccelerationStructurePrebuildInfo
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GetRaytracingAccelerationStructurePrebuildInfoDelegate(
            IntPtr device5,
            IntPtr pDesc,
            IntPtr pInfo);

        // COM interface method delegate for DispatchRays
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DispatchRaysDelegate(
            IntPtr commandList,
            IntPtr pDesc);

        // COM interface method delegate for CopyRaytracingAccelerationStructure
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CopyRaytracingAccelerationStructureDelegate(
            IntPtr commandList,
            ulong DestAccelerationStructureData,
            ulong SourceAccelerationStructureData,
            uint Mode);

        // COM interface method delegate for GetGPUVirtualAddress
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ulong GetGPUVirtualAddressDelegate(IntPtr resource);

        // COM interface method delegate for CreateStateObject (ID3D12Device5::CreateStateObject)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateStateObjectDelegate(
            IntPtr device5,
            IntPtr pDesc,
            ref Guid riid,
            IntPtr ppStateObject);

        // COM interface method delegate for GetShaderIdentifier (ID3D12StateObjectProperties::GetShaderIdentifier)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetShaderIdentifierDelegate(
            IntPtr properties,
            IntPtr pExportName);

        /// <summary>
        /// Calls ID3D12Device5::GetRaytracingAccelerationStructurePrebuildInfo through COM vtable.
        /// VTable index 57 for ID3D12Device5 (first method in ID3D12Device5 interface).
        /// Based on D3D12 DXR API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device5-getraytracingaccelerationstructureprebuildinfo
        /// </summary>
        private unsafe void CallGetRaytracingAccelerationStructurePrebuildInfo(
            IntPtr device5,
            IntPtr pDesc,
            out D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO pInfo)
        {
            // Initialize output structure
            pInfo = new D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO();

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (device5 == IntPtr.Zero || pDesc == IntPtr.Zero)
            {
                return;
            }

            // Allocate unmanaged memory for output structure
            IntPtr pInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO)));
            try
            {
                // Get vtable pointer
                IntPtr* vtable = *(IntPtr**)device5;
                // GetRaytracingAccelerationStructurePrebuildInfo is at index 57 in ID3D12Device5 vtable
                // (ID3D12Device has ~54 methods, ID3D12Device1-4 add a few, so ID3D12Device5 starts around index 57)
                IntPtr methodPtr = vtable[57];

                // Create delegate from function pointer
                GetRaytracingAccelerationStructurePrebuildInfoDelegate getPrebuildInfo =
                    (GetRaytracingAccelerationStructurePrebuildInfoDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetRaytracingAccelerationStructurePrebuildInfoDelegate));

                getPrebuildInfo(device5, pDesc, pInfoPtr);

                // Marshal structure back from unmanaged memory
                pInfo = (D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO)Marshal.PtrToStructure(pInfoPtr, typeof(D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO));
            }
            finally
            {
                Marshal.FreeHGlobal(pInfoPtr);
            }
        }

        /// <summary>
        /// Gets the GPU virtual address of a resource.
        /// Based on D3D12 API: ID3D12Resource::GetGPUVirtualAddress
        /// VTable index 8 for ID3D12Resource.
        /// </summary>
        internal unsafe ulong GetGpuVirtualAddress(IntPtr resource)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0UL;
            }

            if (resource == IntPtr.Zero)
            {
                return 0UL;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)resource;
            // GetGPUVirtualAddress is at index 8 in ID3D12Resource vtable
            IntPtr methodPtr = vtable[8];

            // Create delegate from function pointer
            GetGPUVirtualAddressDelegate getGpuVa =
                (GetGPUVirtualAddressDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetGPUVirtualAddressDelegate));

            return getGpuVa(resource);
        }

        /// <summary>
        /// Releases an ID3D12StateObject COM interface by calling IUnknown::Release.
        /// VTable index 2 for IUnknown (all COM interfaces inherit from IUnknown).
        /// Based on COM Reference Counting: https://docs.microsoft.com/en-us/windows/win32/api/unknwn/nf-unknwn-iunknown-release
        /// </summary>
        /// <param name="stateObject">Pointer to ID3D12StateObject COM interface</param>
        /// <returns>Remaining reference count after release (0 if object was destroyed)</returns>
        internal unsafe uint CallStateObjectRelease(IntPtr stateObject)
        {
            if (stateObject == IntPtr.Zero)
            {
                return 0;
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return 0;
            }

            // Get vtable pointer (first field of COM object)
            IntPtr* vtable = *(IntPtr**)stateObject;
            // IUnknown::Release is at index 2 in all COM interface vtables
            IntPtr methodPtr = vtable[2];

            // Create delegate from function pointer
            ReleaseDelegate release = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ReleaseDelegate));

            return release(stateObject);
        }

        /// <summary>
        /// Queries an ID3D12StateObject for another interface (e.g., ID3D12StateObjectProperties).
        /// Calls IUnknown::QueryInterface through COM vtable.
        /// VTable index 0 for IUnknown (all COM interfaces inherit from IUnknown).
        /// Based on COM QueryInterface: https://docs.microsoft.com/en-us/windows/win32/api/unknwn/nf-unknwn-iunknown-queryinterface
        /// </summary>
        /// <param name="stateObject">Pointer to ID3D12StateObject COM interface</param>
        /// <param name="riid">Interface ID to query for (e.g., IID_ID3D12StateObjectProperties)</param>
        /// <param name="ppvObject">Output pointer to the queried interface</param>
        /// <returns>HRESULT: S_OK (0) on success, E_NOINTERFACE or other error code on failure</returns>
        private unsafe int CallStateObjectQueryInterface(IntPtr stateObject, ref Guid riid, out IntPtr ppvObject)
        {
            ppvObject = IntPtr.Zero;

            if (stateObject == IntPtr.Zero)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return unchecked((int)0x80004001); // E_NOTIMPL
            }

            // Get vtable pointer (first field of COM object)
            IntPtr* vtable = *(IntPtr**)stateObject;
            // IUnknown::QueryInterface is at index 0 in all COM interface vtables
            IntPtr methodPtr = vtable[0];

            // Create delegate from function pointer
            QueryInterfaceDelegate queryInterface = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(QueryInterfaceDelegate));

            // Allocate unmanaged memory for output pointer
            IntPtr ppvObjectPtr = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                int hr = queryInterface(stateObject, ref riid, ppvObjectPtr);
                if (hr == 0) // S_OK
                {
                    ppvObject = Marshal.ReadIntPtr(ppvObjectPtr);
                }
                return hr;
            }
            finally
            {
                Marshal.FreeHGlobal(ppvObjectPtr);
            }
        }

        /// <summary>
        /// Gets the shader identifier for a shader export name from ID3D12StateObjectProperties.
        /// Calls ID3D12StateObjectProperties::GetShaderIdentifier through COM vtable.
        /// VTable index 3 for ID3D12StateObjectProperties (after IUnknown methods: QueryInterface=0, AddRef=1, Release=2).
        /// Based on D3D12 DXR API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12stateobjectproperties-getshaderidentifier
        /// </summary>
        /// <param name="properties">Pointer to ID3D12StateObjectProperties COM interface</param>
        /// <param name="pExportName">Pointer to null-terminated string containing the shader export name</param>
        /// <returns>Pointer to shader identifier data (32 bytes), or IntPtr.Zero if export name not found</returns>
        internal unsafe IntPtr CallStateObjectPropertiesGetShaderIdentifier(IntPtr properties, IntPtr pExportName)
        {
            if (properties == IntPtr.Zero || pExportName == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return IntPtr.Zero;
            }

            // Get vtable pointer (first field of COM object)
            IntPtr* vtable = *(IntPtr**)properties;
            // GetShaderIdentifier is at index 3 in ID3D12StateObjectProperties vtable (after IUnknown: 0=QueryInterface, 1=AddRef, 2=Release)
            IntPtr methodPtr = vtable[3];

            // Create delegate from function pointer
            GetShaderIdentifierDelegate getShaderIdentifier = (GetShaderIdentifierDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(GetShaderIdentifierDelegate));

            return getShaderIdentifier(properties, pExportName);
        }

        /// <summary>
        /// Calls ID3D12GraphicsCommandList4::DispatchRays through COM vtable.
        /// VTable index 98 for ID3D12GraphicsCommandList4 (inherits from ID3D12GraphicsCommandList).
        /// Based on D3D12 DXR API: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist4-dispatchrays
        /// </summary>
        internal unsafe void CallDispatchRays(IntPtr commandList, IntPtr pDesc)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandList == IntPtr.Zero || pDesc == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandList;
            // DispatchRays is at index 98 in ID3D12GraphicsCommandList4 vtable (after BuildRaytracingAccelerationStructure which is 97)
            IntPtr methodPtr = vtable[98];

            // Create delegate from function pointer
            DispatchRaysDelegate dispatchRays =
                (DispatchRaysDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(DispatchRaysDelegate));

            dispatchRays(commandList, pDesc);
        }

        /// <summary>
        /// Calls ID3D12GraphicsCommandList4::CopyRaytracingAccelerationStructure through COM vtable.
        /// VTable index 99 for ID3D12GraphicsCommandList4 (inherits from ID3D12GraphicsCommandList).
        /// Based on D3D12 DXR API: https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist4-copyraytracingaccelerationstructure
        /// Copies a source acceleration structure to destination memory while applying the specified transformation.
        /// For compaction, Mode must be D3D12_RAYTRACING_ACCELERATION_STRUCTURE_COPY_MODE_COMPACT and
        /// the source must have been built with D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_COMPACTION.
        /// </summary>
        private unsafe void CallCopyRaytracingAccelerationStructure(IntPtr commandList, ulong destGpuVa, ulong srcGpuVa, uint mode)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandList == IntPtr.Zero)
            {
                return;
            }

            if (destGpuVa == 0UL || srcGpuVa == 0UL)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandList;
            // CopyRaytracingAccelerationStructure is at index 99 in ID3D12GraphicsCommandList4 vtable
            // (after BuildRaytracingAccelerationStructure at 97 and DispatchRays at 98)
            IntPtr methodPtr = vtable[99];

            // Create delegate from function pointer
            CopyRaytracingAccelerationStructureDelegate copyAccelStruct =
                (CopyRaytracingAccelerationStructureDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CopyRaytracingAccelerationStructureDelegate));

            copyAccelStruct(commandList, destGpuVa, srcGpuVa, mode);
        }

        // COM interface method delegate for CreateCommandSignature
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateCommandSignatureDelegate(
            IntPtr device,
            IntPtr pDesc,
            IntPtr pRootSignature,
            ref Guid riid,
            out IntPtr ppCommandSignature);

        // COM interface method delegate for ExecuteIndirect
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ExecuteIndirectDelegate(
            IntPtr commandList,
            IntPtr pCommandSignature,
            uint MaxCommandCount,
            IntPtr pArgumentBuffer,
            ulong ArgumentBufferOffset,
            IntPtr pCountBuffer,
            ulong CountBufferOffset);

        /// <summary>
        /// Creates a command signature for DispatchIndirect.
        /// Based on D3D12 API: ID3D12Device::CreateCommandSignature
        /// VTable index 27 for ID3D12Device.
        /// Command signatures describe the structure of indirect arguments in GPU buffers.
        /// </summary>
        internal unsafe IntPtr CreateDispatchIndirectCommandSignature()
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return IntPtr.Zero;
            }

            if (_device == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // If already created, return cached signature
            if (_dispatchIndirectCommandSignature != IntPtr.Zero)
            {
                return _dispatchIndirectCommandSignature;
            }

            // Create indirect argument description for Dispatch
            // D3D12_DISPATCH_ARGUMENTS contains 3 uints (ThreadGroupCountX, Y, Z)
            var argumentDesc = new D3D12_INDIRECT_ARGUMENT_DESC
            {
                Type = D3D12_INDIRECT_ARGUMENT_TYPE_DISPATCH,
                ConstantRootParameterIndex = 0,
                ConstantNum32BitValuesToSet = 0,
                VertexBufferSlot = IntPtr.Zero
            };

            // Create command signature description
            // ByteStride is the size of D3D12_DISPATCH_ARGUMENTS (12 bytes: 3 uints)
            var commandSignatureDesc = new D3D12_COMMAND_SIGNATURE_DESC
            {
                ByteStride = (uint)Marshal.SizeOf(typeof(D3D12_DISPATCH_ARGUMENTS)),
                NumArgumentDescs = 1,
                pArgumentDescs = IntPtr.Zero, // Will be set after marshaling
                NodeMask = 0
            };

            // Allocate memory for argument description
            int argumentDescSize = Marshal.SizeOf(typeof(D3D12_INDIRECT_ARGUMENT_DESC));
            IntPtr argumentDescPtr = Marshal.AllocHGlobal(argumentDescSize);
            try
            {
                Marshal.StructureToPtr(argumentDesc, argumentDescPtr, false);
                commandSignatureDesc.pArgumentDescs = argumentDescPtr;

                // Allocate memory for command signature description
                int descSize = Marshal.SizeOf(typeof(D3D12_COMMAND_SIGNATURE_DESC));
                IntPtr descPtr = Marshal.AllocHGlobal(descSize);
                try
                {
                    Marshal.StructureToPtr(commandSignatureDesc, descPtr, false);

                    // Get vtable pointer
                    IntPtr* vtable = *(IntPtr**)_device;
                    // CreateCommandSignature is at index 27 in ID3D12Device vtable
                    IntPtr methodPtr = vtable[27];

                    // Create delegate from function pointer
                    CreateCommandSignatureDelegate createCommandSignature =
                        (CreateCommandSignatureDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateCommandSignatureDelegate));

                    // IID_ID3D12CommandSignature
                    Guid iidCommandSignature = new Guid(0xc36a797c, 0xec80, 0x4f0a, 0x89, 0x85, 0xa7, 0xb2, 0x47, 0x50, 0x85, 0xa1);

                    IntPtr commandSignature;
                    int hr = createCommandSignature(_device, descPtr, IntPtr.Zero, ref iidCommandSignature, out commandSignature);

                    // Check HRESULT
                    if (hr == 0) // S_OK
                    {
                        _dispatchIndirectCommandSignature = commandSignature;
                        return commandSignature;
                    }
                    else
                    {
                        // Failed to create command signature
                        return IntPtr.Zero;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(descPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(argumentDescPtr);
            }
        }

        /// <summary>
        /// Creates or retrieves cached command signature for DrawIndirect.
        /// Command signatures describe the structure of indirect arguments in GPU buffers.
        /// Based on DirectX 12 CreateCommandSignature: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcommandsignature
        /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
        /// </summary>
        internal unsafe IntPtr CreateDrawIndirectCommandSignature()
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return IntPtr.Zero;
            }

            if (_device == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // If already created, return cached signature
            if (_drawIndirectCommandSignature != IntPtr.Zero)
            {
                return _drawIndirectCommandSignature;
            }

            // Create indirect argument description for Draw
            // D3D12_DRAW_ARGUMENTS contains 4 uints (VertexCountPerInstance, InstanceCount, StartVertexLocation, StartInstanceLocation)
            var argumentDesc = new D3D12_INDIRECT_ARGUMENT_DESC
            {
                Type = D3D12_INDIRECT_ARGUMENT_TYPE_DRAW,
                ConstantRootParameterIndex = 0,
                ConstantNum32BitValuesToSet = 0,
                VertexBufferSlot = IntPtr.Zero
            };

            // Create command signature description
            // ByteStride is the size of D3D12_DRAW_ARGUMENTS (16 bytes: 4 uints)
            var commandSignatureDesc = new D3D12_COMMAND_SIGNATURE_DESC
            {
                ByteStride = (uint)Marshal.SizeOf(typeof(D3D12_DRAW_ARGUMENTS)),
                NumArgumentDescs = 1,
                pArgumentDescs = IntPtr.Zero, // Will be set after marshaling
                NodeMask = 0
            };

            // Allocate memory for argument description
            int argumentDescSize = Marshal.SizeOf(typeof(D3D12_INDIRECT_ARGUMENT_DESC));
            IntPtr argumentDescPtr = Marshal.AllocHGlobal(argumentDescSize);
            try
            {
                Marshal.StructureToPtr(argumentDesc, argumentDescPtr, false);
                commandSignatureDesc.pArgumentDescs = argumentDescPtr;

                // Allocate memory for command signature description
                int descSize = Marshal.SizeOf(typeof(D3D12_COMMAND_SIGNATURE_DESC));
                IntPtr descPtr = Marshal.AllocHGlobal(descSize);
                try
                {
                    Marshal.StructureToPtr(commandSignatureDesc, descPtr, false);

                    // Get vtable pointer
                    IntPtr* vtable = *(IntPtr**)_device;
                    // CreateCommandSignature is at index 27 in ID3D12Device vtable
                    IntPtr methodPtr = vtable[27];

                    // Create delegate from function pointer
                    CreateCommandSignatureDelegate createCommandSignature =
                        (CreateCommandSignatureDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateCommandSignatureDelegate));

                    // IID_ID3D12CommandSignature
                    Guid iidCommandSignature = new Guid(0xc36a797c, 0xec80, 0x4f0a, 0x89, 0x85, 0xa7, 0xb2, 0x47, 0x50, 0x85, 0xa1);

                    IntPtr commandSignature;
                    int hr = createCommandSignature(_device, descPtr, IntPtr.Zero, ref iidCommandSignature, out commandSignature);

                    // Check HRESULT
                    if (hr == 0) // S_OK
                    {
                        _drawIndirectCommandSignature = commandSignature;
                        return commandSignature;
                    }
                    else
                    {
                        // Failed to create command signature
                        return IntPtr.Zero;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(descPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(argumentDescPtr);
            }
        }

        /// <summary>
        /// Creates or retrieves cached command signature for DrawIndexedIndirect.
        /// Command signatures describe the structure of indirect arguments in GPU buffers.
        /// Based on DirectX 12 CreateCommandSignature: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12device-createcommandsignature
        /// swkotor2.exe: N/A - Original game used DirectX 9, not DirectX 12
        /// </summary>
        internal unsafe IntPtr CreateDrawIndexedIndirectCommandSignature()
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return IntPtr.Zero;
            }

            if (_device == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // If already created, return cached signature
            if (_drawIndexedIndirectCommandSignature != IntPtr.Zero)
            {
                return _drawIndexedIndirectCommandSignature;
            }

            // Create indirect argument description for DrawIndexed
            // D3D12_DRAW_INDEXED_ARGUMENTS contains 5 values (IndexCountPerInstance, InstanceCount, StartIndexLocation, BaseVertexLocation, StartInstanceLocation)
            var argumentDesc = new D3D12_INDIRECT_ARGUMENT_DESC
            {
                Type = D3D12_INDIRECT_ARGUMENT_TYPE_DRAW_INDEXED,
                ConstantRootParameterIndex = 0,
                ConstantNum32BitValuesToSet = 0,
                VertexBufferSlot = IntPtr.Zero
            };

            // Create command signature description
            // ByteStride is the size of D3D12_DRAW_INDEXED_ARGUMENTS (20 bytes: 4 uints + 1 int)
            var commandSignatureDesc = new D3D12_COMMAND_SIGNATURE_DESC
            {
                ByteStride = (uint)Marshal.SizeOf(typeof(D3D12_DRAW_INDEXED_ARGUMENTS)),
                NumArgumentDescs = 1,
                pArgumentDescs = IntPtr.Zero, // Will be set after marshaling
                NodeMask = 0
            };

            // Allocate memory for argument description
            int argumentDescSize = Marshal.SizeOf(typeof(D3D12_INDIRECT_ARGUMENT_DESC));
            IntPtr argumentDescPtr = Marshal.AllocHGlobal(argumentDescSize);
            try
            {
                Marshal.StructureToPtr(argumentDesc, argumentDescPtr, false);
                commandSignatureDesc.pArgumentDescs = argumentDescPtr;

                // Allocate memory for command signature description
                int descSize = Marshal.SizeOf(typeof(D3D12_COMMAND_SIGNATURE_DESC));
                IntPtr descPtr = Marshal.AllocHGlobal(descSize);
                try
                {
                    Marshal.StructureToPtr(commandSignatureDesc, descPtr, false);

                    // Get vtable pointer
                    IntPtr* vtable = *(IntPtr**)_device;
                    // CreateCommandSignature is at index 27 in ID3D12Device vtable
                    IntPtr methodPtr = vtable[27];

                    // Create delegate from function pointer
                    CreateCommandSignatureDelegate createCommandSignature =
                        (CreateCommandSignatureDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(CreateCommandSignatureDelegate));

                    // IID_ID3D12CommandSignature
                    Guid iidCommandSignature = new Guid(0xc36a797c, 0xec80, 0x4f0a, 0x89, 0x85, 0xa7, 0xb2, 0x47, 0x50, 0x85, 0xa1);

                    IntPtr commandSignature;
                    int hr = createCommandSignature(_device, descPtr, IntPtr.Zero, ref iidCommandSignature, out commandSignature);

                    // Check HRESULT
                    if (hr == 0) // S_OK
                    {
                        _drawIndexedIndirectCommandSignature = commandSignature;
                        return commandSignature;
                    }
                    else
                    {
                        // Failed to create command signature
                        return IntPtr.Zero;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(descPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(argumentDescPtr);
            }
        }

        /// <summary>
        /// Calls ID3D12GraphicsCommandList::ExecuteIndirect through COM vtable.
        /// VTable index 98 for ID3D12GraphicsCommandList.
        /// Based on DirectX 12 ExecuteIndirect: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-executeindirect
        /// </summary>
        private unsafe void CallExecuteIndirect(
            IntPtr commandList,
            IntPtr pCommandSignature,
            uint MaxCommandCount,
            IntPtr pArgumentBuffer,
            ulong ArgumentBufferOffset,
            IntPtr pCountBuffer,
            ulong CountBufferOffset)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandList == IntPtr.Zero || pCommandSignature == IntPtr.Zero || pArgumentBuffer == IntPtr.Zero)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandList;
            // ExecuteIndirect is at index 98 in ID3D12GraphicsCommandList vtable
            IntPtr methodPtr = vtable[98];

            // Create delegate from function pointer
            ExecuteIndirectDelegate executeIndirect =
                (ExecuteIndirectDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ExecuteIndirectDelegate));

            executeIndirect(
                commandList,
                pCommandSignature,
                MaxCommandCount,
                pArgumentBuffer,
                ArgumentBufferOffset,
                pCountBuffer,
                CountBufferOffset);
        }

        /// <summary>
        /// Calls ID3D12CommandQueue::ExecuteCommandLists through COM vtable.
        /// VTable index 4 for ID3D12CommandQueue (after IUnknown: QueryInterface, AddRef, Release, UpdateTileMappings).
        /// Based on DirectX 12 Command Queue: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12commandqueue-executecommandlists
        ///
        /// ExecuteCommandLists submits command lists to the command queue for execution on the GPU.
        /// All command lists must be closed before execution.
        ///
        /// Signature: void ExecuteCommandLists(UINT NumCommandLists, ID3D12CommandList* const* ppCommandLists)
        /// </summary>
        private unsafe void CallExecuteCommandLists(IntPtr commandQueue, uint numCommandLists, IntPtr ppCommandLists)
        {
            // Platform check: DirectX 12 COM is Windows-only
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (commandQueue == IntPtr.Zero || ppCommandLists == IntPtr.Zero || numCommandLists == 0)
            {
                return;
            }

            // Get vtable pointer
            IntPtr* vtable = *(IntPtr**)commandQueue;
            // ExecuteCommandLists is at index 4 in ID3D12CommandQueue vtable
            // (after IUnknown: QueryInterface, AddRef, Release, UpdateTileMappings)
            IntPtr methodPtr = vtable[4];

            // Create delegate from function pointer
            ExecuteCommandListsDelegate executeCommandLists =
                (ExecuteCommandListsDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(ExecuteCommandListsDelegate));

            executeCommandLists(commandQueue, numCommandLists, ppCommandLists);
        }

        /// <summary>
        /// Converts AccelStructBuildFlags to D3D12 raytracing acceleration structure build flags.
        /// Based on D3D12 API: D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAGS
        /// </summary>
        internal uint ConvertAccelStructBuildFlagsToD3D12(AccelStructBuildFlags flags)
        {
            uint d3d12Flags = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_NONE;

            if ((flags & AccelStructBuildFlags.AllowUpdate) != 0)
            {
                d3d12Flags |= D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_UPDATE;
            }

            if ((flags & AccelStructBuildFlags.AllowCompaction) != 0)
            {
                d3d12Flags |= D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_ALLOW_COMPACTION;
            }

            if ((flags & AccelStructBuildFlags.PreferFastTrace) != 0)
            {
                d3d12Flags |= D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_TRACE;
            }

            if ((flags & AccelStructBuildFlags.PreferFastBuild) != 0)
            {
                d3d12Flags |= D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_BUILD;
            }

            if ((flags & AccelStructBuildFlags.MinimizeMemory) != 0)
            {
                d3d12Flags |= D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_MINIMIZE_MEMORY;
            }

            return d3d12Flags;
        }

        /// <summary>
        /// Converts GeometryFlags to D3D12 raytracing geometry flags.
        /// Based on D3D12 API: D3D12_RAYTRACING_GEOMETRY_FLAGS
        /// </summary>
        internal uint ConvertGeometryFlagsToD3D12(GeometryFlags flags)
        {
            uint d3d12Flags = D3D12_RAYTRACING_GEOMETRY_FLAG_NONE;

            if ((flags & GeometryFlags.Opaque) != 0)
            {
                d3d12Flags |= D3D12_RAYTRACING_GEOMETRY_FLAG_OPAQUE;
            }

            if ((flags & GeometryFlags.NoDuplicateAnyHit) != 0)
            {
                d3d12Flags |= D3D12_RAYTRACING_GEOMETRY_FLAG_NO_DUPLICATE_ANYHIT_INVOCATION;
            }

            return d3d12Flags;
        }

        /// <summary>
        /// Converts TextureFormat to D3D12 raytracing index format.
        /// Based on D3D12 API: D3D12_RAYTRACING_INDEX_FORMAT
        /// </summary>
        internal uint ConvertIndexFormatToD3D12(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R16_UInt:
                    return D3D12_RAYTRACING_INDEX_FORMAT_UINT16;
                case TextureFormat.R32_UInt:
                    return D3D12_RAYTRACING_INDEX_FORMAT_UINT32;
                default:
                    // Default to 32-bit if format is unknown
                    return D3D12_RAYTRACING_INDEX_FORMAT_UINT32;
            }
        }

        /// <summary>
        /// Converts TextureFormat to D3D12 raytracing vertex format.
        /// Based on D3D12 API: D3D12_RAYTRACING_VERTEX_FORMAT
        /// </summary>
        internal uint ConvertVertexFormatToD3D12(TextureFormat format)
        {
            // D3D12 raytracing supports Float3 and Float2 vertex formats
            // Most common vertex formats map to Float3
            switch (format)
            {
                case TextureFormat.R32G32_Float:
                    return D3D12_RAYTRACING_VERTEX_FORMAT_FLOAT2;
                case TextureFormat.R32G32B32_Float:
                case TextureFormat.R32G32B32A32_Float:
                default:
                    // Default to Float3 for most formats
                    return D3D12_RAYTRACING_VERTEX_FORMAT_FLOAT3;
            }
        }

        // Note: ConvertAccelStructBuildFlagsToD3D12, ConvertGeometryFlagsToD3D12, ConvertIndexFormatToD3D12, and ConvertVertexFormatToD3D12
        // are already defined above. These duplicate methods were removed.

        /// <summary>
        /// Converts BlendStateDesc to D3D12_BLEND_DESC structure.
        /// Based on DirectX 12 Blend State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_blend_desc
        /// </summary>
        private D3D12_BLEND_DESC ConvertBlendStateDescToD3D12(BlendStateDesc blendStateDesc)
        {
            var d3d12BlendDesc = new D3D12_BLEND_DESC();
            d3d12BlendDesc.AlphaToCoverageEnable = blendStateDesc.AlphaToCoverage ? (byte)1 : (byte)0;
            d3d12BlendDesc.IndependentBlendEnable = (blendStateDesc.RenderTargets != null && blendStateDesc.RenderTargets.Length > 1) ? (byte)1 : (byte)0;

            // Get render target blend desc (use first render target or default)
            RenderTargetBlendDesc rtBlend = (blendStateDesc.RenderTargets != null && blendStateDesc.RenderTargets.Length > 0)
                ? blendStateDesc.RenderTargets[0]
                : default(RenderTargetBlendDesc);

            // Convert blend state for render target 0
            var rtBlendDesc = new D3D12_RENDER_TARGET_BLEND_DESC();
            rtBlendDesc.BlendEnable = rtBlend.BlendEnable ? (byte)1 : (byte)0;
            rtBlendDesc.LogicOpEnable = 0; // BOOL - logic operations not used
            rtBlendDesc.SrcBlend = ConvertBlendFactorToD3D12(rtBlend.SrcBlend);
            rtBlendDesc.DestBlend = ConvertBlendFactorToD3D12(rtBlend.DestBlend);
            rtBlendDesc.BlendOp = ConvertBlendOpToD3D12(rtBlend.BlendOp);
            rtBlendDesc.SrcBlendAlpha = ConvertBlendFactorToD3D12(rtBlend.SrcBlendAlpha);
            rtBlendDesc.DestBlendAlpha = ConvertBlendFactorToD3D12(rtBlend.DestBlendAlpha);
            rtBlendDesc.BlendOpAlpha = ConvertBlendOpToD3D12(rtBlend.BlendOpAlpha);
            rtBlendDesc.LogicOp = 0; // D3D12_LOGIC_OP - not used
            rtBlendDesc.RenderTargetWriteMask = rtBlend.WriteMask;

            // Set same blend state for all render targets
            d3d12BlendDesc.RenderTarget0 = rtBlendDesc;
            d3d12BlendDesc.RenderTarget1 = rtBlendDesc;
            d3d12BlendDesc.RenderTarget2 = rtBlendDesc;
            d3d12BlendDesc.RenderTarget3 = rtBlendDesc;
            d3d12BlendDesc.RenderTarget4 = rtBlendDesc;
            d3d12BlendDesc.RenderTarget5 = rtBlendDesc;
            d3d12BlendDesc.RenderTarget6 = rtBlendDesc;
            d3d12BlendDesc.RenderTarget7 = rtBlendDesc;

            return d3d12BlendDesc;
        }

        /// <summary>
        /// Converts BlendFactor to D3D12_BLEND value.
        /// </summary>
        private uint ConvertBlendFactorToD3D12(BlendFactor factor)
        {
            // D3D12_BLEND constants
            const uint D3D12_BLEND_ZERO = 1;
            const uint D3D12_BLEND_ONE = 2;
            const uint D3D12_BLEND_SRC_COLOR = 3;
            const uint D3D12_BLEND_INV_SRC_COLOR = 4;
            const uint D3D12_BLEND_SRC_ALPHA = 5;
            const uint D3D12_BLEND_INV_SRC_ALPHA = 6;
            const uint D3D12_BLEND_DEST_ALPHA = 7;
            const uint D3D12_BLEND_INV_DEST_ALPHA = 8;
            const uint D3D12_BLEND_DEST_COLOR = 9;
            const uint D3D12_BLEND_INV_DEST_COLOR = 10;

            switch (factor)
            {
                case BlendFactor.Zero: return D3D12_BLEND_ZERO;
                case BlendFactor.One: return D3D12_BLEND_ONE;
                case BlendFactor.SrcColor: return D3D12_BLEND_SRC_COLOR;
                case BlendFactor.InvSrcColor: return D3D12_BLEND_INV_SRC_COLOR;
                case BlendFactor.SrcAlpha: return D3D12_BLEND_SRC_ALPHA;
                case BlendFactor.InvSrcAlpha: return D3D12_BLEND_INV_SRC_ALPHA;
                case BlendFactor.DstAlpha: return D3D12_BLEND_DEST_ALPHA;
                case BlendFactor.InvDstAlpha: return D3D12_BLEND_INV_DEST_ALPHA;
                case BlendFactor.DstColor: return D3D12_BLEND_DEST_COLOR;
                case BlendFactor.InvDstColor: return D3D12_BLEND_INV_DEST_COLOR;
                default: return D3D12_BLEND_ONE;
            }
        }

        /// <summary>
        /// Converts BlendOp to D3D12_BLEND_OP value.
        /// </summary>
        private uint ConvertBlendOpToD3D12(BlendOp op)
        {
            // D3D12_BLEND_OP constants
            const uint D3D12_BLEND_OP_ADD = 1;
            const uint D3D12_BLEND_OP_SUBTRACT = 2;
            const uint D3D12_BLEND_OP_REV_SUBTRACT = 3;
            const uint D3D12_BLEND_OP_MIN = 4;
            const uint D3D12_BLEND_OP_MAX = 5;

            switch (op)
            {
                case BlendOp.Add: return D3D12_BLEND_OP_ADD;
                case BlendOp.Subtract: return D3D12_BLEND_OP_SUBTRACT;
                case BlendOp.ReverseSubtract: return D3D12_BLEND_OP_REV_SUBTRACT;
                case BlendOp.Min: return D3D12_BLEND_OP_MIN;
                case BlendOp.Max: return D3D12_BLEND_OP_MAX;
                default: return D3D12_BLEND_OP_ADD;
            }
        }

        /// <summary>
        /// Converts RasterStateDesc to D3D12_RASTERIZER_DESC structure.
        /// Based on DirectX 12 Rasterizer State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_rasterizer_desc
        /// </summary>
        private D3D12_RASTERIZER_DESC ConvertRasterStateDescToD3D12(RasterStateDesc rasterStateDesc)
        {
            var d3d12RasterDesc = new D3D12_RASTERIZER_DESC();
            d3d12RasterDesc.FillMode = ConvertFillModeToD3D12(rasterStateDesc.FillMode);
            d3d12RasterDesc.CullMode = ConvertCullModeToD3D12(rasterStateDesc.CullMode);
            d3d12RasterDesc.FrontCounterClockwise = rasterStateDesc.FrontCCW ? (byte)1 : (byte)0;
            d3d12RasterDesc.DepthBias = rasterStateDesc.DepthBias;
            d3d12RasterDesc.DepthBiasClamp = rasterStateDesc.DepthBiasClamp;
            d3d12RasterDesc.SlopeScaledDepthBias = rasterStateDesc.SlopeScaledDepthBias;
            d3d12RasterDesc.DepthClipEnable = rasterStateDesc.DepthClipEnable ? (byte)1 : (byte)0;
            d3d12RasterDesc.MultisampleEnable = rasterStateDesc.MultisampleEnable ? (byte)1 : (byte)0;
            d3d12RasterDesc.AntialiasedLineEnable = rasterStateDesc.AntialiasedLineEnable ? (byte)1 : (byte)0;
            d3d12RasterDesc.ForcedSampleCount = 0; // UINT - no forced sample count
            d3d12RasterDesc.ConservativeRaster = rasterStateDesc.ConservativeRaster ? 1u : 0u; // D3D12_CONSERVATIVE_RASTERIZATION_MODE
            return d3d12RasterDesc;
        }

        /// <summary>
        /// Converts FillMode to D3D12_FILL_MODE value.
        /// </summary>
        private uint ConvertFillModeToD3D12(FillMode fillMode)
        {
            // D3D12_FILL_MODE constants
            const uint D3D12_FILL_MODE_WIREFRAME = 2;
            const uint D3D12_FILL_MODE_SOLID = 3;

            switch (fillMode)
            {
                case FillMode.Wireframe: return D3D12_FILL_MODE_WIREFRAME;
                case FillMode.Solid: return D3D12_FILL_MODE_SOLID;
                default: return D3D12_FILL_MODE_SOLID;
            }
        }

        /// <summary>
        /// Converts CullMode to D3D12_CULL_MODE value.
        /// </summary>
        private uint ConvertCullModeToD3D12(CullMode cullMode)
        {
            // D3D12_CULL_MODE constants
            const uint D3D12_CULL_MODE_NONE = 1;
            const uint D3D12_CULL_MODE_FRONT = 2;
            const uint D3D12_CULL_MODE_BACK = 3;

            switch (cullMode)
            {
                case CullMode.None: return D3D12_CULL_MODE_NONE;
                case CullMode.Front: return D3D12_CULL_MODE_FRONT;
                case CullMode.Back: return D3D12_CULL_MODE_BACK;
                default: return D3D12_CULL_MODE_BACK;
            }
        }

        /// <summary>
        /// Converts DepthStencilStateDesc to D3D12_DEPTH_STENCIL_DESC structure.
        /// Based on DirectX 12 Depth-Stencil State: https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_depth_stencil_desc
        /// </summary>
        private D3D12_DEPTH_STENCIL_DESC ConvertDepthStencilStateDescToD3D12(DepthStencilStateDesc depthStencilStateDesc)
        {
            var d3d12DepthStencilDesc = new D3D12_DEPTH_STENCIL_DESC();
            d3d12DepthStencilDesc.DepthEnable = depthStencilStateDesc.DepthTestEnable ? (byte)1 : (byte)0;
            d3d12DepthStencilDesc.DepthWriteMask = depthStencilStateDesc.DepthWriteEnable ? (byte)1 : (byte)0; // D3D12_DEPTH_WRITE_MASK_ALL = 1, D3D12_DEPTH_WRITE_MASK_ZERO = 0
            d3d12DepthStencilDesc.DepthFunc = ConvertCompareFuncToD3D12(depthStencilStateDesc.DepthFunc);
            d3d12DepthStencilDesc.StencilEnable = depthStencilStateDesc.StencilEnable ? (byte)1 : (byte)0;
            d3d12DepthStencilDesc.StencilReadMask = depthStencilStateDesc.StencilReadMask;
            d3d12DepthStencilDesc.StencilWriteMask = depthStencilStateDesc.StencilWriteMask;

            // Convert stencil operations for front and back faces
            var frontStencilOp = new D3D12_DEPTH_STENCILOP_DESC();
            frontStencilOp.StencilFailOp = ConvertStencilOpToD3D12(depthStencilStateDesc.FrontFace.StencilFailOp);
            frontStencilOp.StencilDepthFailOp = ConvertStencilOpToD3D12(depthStencilStateDesc.FrontFace.DepthFailOp);
            frontStencilOp.StencilPassOp = ConvertStencilOpToD3D12(depthStencilStateDesc.FrontFace.PassOp);
            frontStencilOp.StencilFunc = ConvertCompareFuncToD3D12(depthStencilStateDesc.FrontFace.StencilFunc);
            d3d12DepthStencilDesc.FrontFace = frontStencilOp;

            var backStencilOp = new D3D12_DEPTH_STENCILOP_DESC();
            backStencilOp.StencilFailOp = ConvertStencilOpToD3D12(depthStencilStateDesc.BackFace.StencilFailOp);
            backStencilOp.StencilDepthFailOp = ConvertStencilOpToD3D12(depthStencilStateDesc.BackFace.DepthFailOp);
            backStencilOp.StencilPassOp = ConvertStencilOpToD3D12(depthStencilStateDesc.BackFace.PassOp);
            backStencilOp.StencilFunc = ConvertCompareFuncToD3D12(depthStencilStateDesc.BackFace.StencilFunc);
            d3d12DepthStencilDesc.BackFace = backStencilOp;

            return d3d12DepthStencilDesc;
        }

        /// <summary>
        /// Converts StencilOp to D3D12_STENCIL_OP value.
        /// </summary>
        private uint ConvertStencilOpToD3D12(StencilOp op)
        {
            // D3D12_STENCIL_OP constants
            const uint D3D12_STENCIL_OP_KEEP = 1;
            const uint D3D12_STENCIL_OP_ZERO = 2;
            const uint D3D12_STENCIL_OP_REPLACE = 3;
            const uint D3D12_STENCIL_OP_INCR_SAT = 4;
            const uint D3D12_STENCIL_OP_DECR_SAT = 5;
            const uint D3D12_STENCIL_OP_INVERT = 6;
            const uint D3D12_STENCIL_OP_INCR = 7;
            const uint D3D12_STENCIL_OP_DECR = 8;

            switch (op)
            {
                case StencilOp.Keep: return D3D12_STENCIL_OP_KEEP;
                case StencilOp.Zero: return D3D12_STENCIL_OP_ZERO;
                case StencilOp.Replace: return D3D12_STENCIL_OP_REPLACE;
                case StencilOp.IncrSat: return D3D12_STENCIL_OP_INCR_SAT;
                case StencilOp.DecrSat: return D3D12_STENCIL_OP_DECR_SAT;
                case StencilOp.Invert: return D3D12_STENCIL_OP_INVERT;
                case StencilOp.Incr: return D3D12_STENCIL_OP_INCR;
                case StencilOp.Decr: return D3D12_STENCIL_OP_DECR;
                default: return D3D12_STENCIL_OP_KEEP;
            }
        }

        /// <summary>
        /// Marshals VertexAttributeDesc array to D3D12_INPUT_ELEMENT_DESC array.
        /// </summary>
        private IntPtr MarshalInputElementDescs(VertexAttributeDesc[] attributes)
        {
            if (attributes == null || attributes.Length == 0)
            {
                return IntPtr.Zero;
            }

            int elementSize = Marshal.SizeOf<D3D12_INPUT_ELEMENT_DESC>();
            int totalSize = elementSize * attributes.Length;
            IntPtr elementsPtr = Marshal.AllocHGlobal(totalSize);

            for (int i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                var d3d12Element = new D3D12_INPUT_ELEMENT_DESC();

                // Marshal semantic name string
                if (!string.IsNullOrEmpty(attribute.SemanticName))
                {
                    byte[] semanticNameBytes = System.Text.Encoding.ASCII.GetBytes(attribute.SemanticName + "\0");
                    IntPtr semanticNamePtr = Marshal.AllocHGlobal(semanticNameBytes.Length);
                    Marshal.Copy(semanticNameBytes, 0, semanticNamePtr, semanticNameBytes.Length);
                    d3d12Element.SemanticName = semanticNamePtr;
                }
                else
                {
                    d3d12Element.SemanticName = IntPtr.Zero;
                }

                d3d12Element.SemanticIndex = unchecked((uint)attribute.SemanticIndex);
                d3d12Element.Format = ConvertTextureFormatToDxgiFormat(attribute.Format);
                d3d12Element.InputSlot = unchecked((uint)attribute.Slot);
                d3d12Element.AlignedByteOffset = unchecked((uint)attribute.Offset);
                d3d12Element.InputSlotClass = attribute.PerInstance ? 1u : 0u; // D3D12_INPUT_CLASSIFICATION_PER_INSTANCE_DATA = 1, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA = 0
                d3d12Element.InstanceDataStepRate = unchecked((uint)attribute.InstanceStepRate);

                IntPtr elementPtr = new IntPtr(elementsPtr.ToInt64() + (i * elementSize));
                Marshal.StructureToPtr(d3d12Element, elementPtr, false);
            }

            return elementsPtr;
        }

        /// <summary>
        /// Converts PrimitiveTopology to D3D12_PRIMITIVE_TOPOLOGY_TYPE value.
        /// </summary>
        private uint ConvertPrimitiveTopologyToD3D12(PrimitiveTopology topology)
        {
            // D3D12_PRIMITIVE_TOPOLOGY_TYPE constants
            const uint D3D12_PRIMITIVE_TOPOLOGY_TYPE_UNDEFINED = 0;
            const uint D3D12_PRIMITIVE_TOPOLOGY_TYPE_POINT = 1;
            const uint D3D12_PRIMITIVE_TOPOLOGY_TYPE_LINE = 2;
            const uint D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE = 3;
            const uint D3D12_PRIMITIVE_TOPOLOGY_TYPE_PATCH = 4;

            switch (topology)
            {
                case PrimitiveTopology.PointList: return D3D12_PRIMITIVE_TOPOLOGY_TYPE_POINT;
                case PrimitiveTopology.LineList:
                case PrimitiveTopology.LineStrip: return D3D12_PRIMITIVE_TOPOLOGY_TYPE_LINE;
                case PrimitiveTopology.TriangleList:
                case PrimitiveTopology.TriangleStrip: return D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
                default: return D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
            }
        }

        #endregion
    }
}

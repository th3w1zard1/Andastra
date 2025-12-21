using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Backends
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

        // DirectX 12 COM interface declarations (simplified - full implementation would require complete COM interop)
        [ComImport]
        [Guid("189819f1-1db6-4b57-be54-1821339b85f7")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ID3D12Device
        {
            // Methods would be declared here in full implementation
            // This is a placeholder structure
        }

        [ComImport]
        [Guid("8b4f173b-2fea-4b80-b4c4-5246a8e9da52")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ID3D12Device5
        {
            // ID3D12Device5 methods for raytracing
            // This is a placeholder structure
        }

        // DirectX 12 function pointers for P/Invoke (simplified - full implementation requires extensive declarations)
        // In a complete implementation, these would be loaded via GetProcAddress or use SharpDX/Vortice wrapper
        
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
            _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            _resources = new Dictionary<IntPtr, IResource>();
            _nextResourceHandle = 1;
            _currentFrameIndex = 0;
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

            // TODO: IMPLEMENT - Create D3D12 texture resource
            // - Convert TextureDesc to D3D12_RESOURCE_DESC
            // - Determine D3D12_HEAP_TYPE based on usage
            // - Call ID3D12Device::CreateCommittedResource
            // - Create D3D12_CPU_DESCRIPTOR_HANDLE for SRV/RTV/DSV/UAV as needed
            // - Wrap in D3D12Texture and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var texture = new D3D12Texture(handle, desc, IntPtr.Zero, IntPtr.Zero, _device);
            _resources[handle] = texture;

            return texture;
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

            // TODO: IMPLEMENT - Create D3D12 buffer resource
            // - Convert BufferDesc to D3D12_RESOURCE_DESC
            // - Determine D3D12_HEAP_TYPE based on usage (DEFAULT, UPLOAD, READBACK)
            // - Call ID3D12Device::CreateCommittedResource
            // - Create D3D12_CPU_DESCRIPTOR_HANDLE for SRV/UAV/CBV as needed
            // - Wrap in D3D12Buffer and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var buffer = new D3D12Buffer(handle, desc, IntPtr.Zero, _device);
            _resources[handle] = buffer;

            return buffer;
        }

        public ISampler CreateSampler(SamplerDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // TODO: IMPLEMENT - Create D3D12 sampler
            // - Convert SamplerDesc to D3D12_SAMPLER_DESC
            // - Allocate from sampler heap or use static samplers
            // - Wrap in D3D12Sampler and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var sampler = new D3D12Sampler(handle, desc, IntPtr.Zero, _device);
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

            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            // TODO: IMPLEMENT - Create D3D12 graphics pipeline state object
            // - Convert GraphicsPipelineDesc to D3D12_GRAPHICS_PIPELINE_STATE_DESC
            // - Set shader bytecode from IShader objects
            // - Convert InputLayout, BlendState, RasterState, DepthStencilState
            // - Convert RootSignature from BindingLayouts
            // - Call ID3D12Device::CreateGraphicsPipelineState
            // - Wrap in D3D12GraphicsPipeline and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var pipeline = new D3D12GraphicsPipeline(handle, desc, IntPtr.Zero, IntPtr.Zero, _device);
            _resources[handle] = pipeline;

            return pipeline;
        }

        public IComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            // TODO: IMPLEMENT - Create D3D12 compute pipeline state object
            // - Convert ComputePipelineDesc to D3D12_COMPUTE_PIPELINE_STATE_DESC
            // - Set compute shader bytecode
            // - Convert RootSignature from BindingLayouts
            // - Call ID3D12Device::CreateComputePipelineState
            // - Wrap in D3D12ComputePipeline and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var pipeline = new D3D12ComputePipeline(handle, desc, IntPtr.Zero, IntPtr.Zero, _device);
            _resources[handle] = pipeline;

            return pipeline;
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

            // TODO: IMPLEMENT - Create D3D12 root signature
            // - Convert BindingLayoutItems to D3D12_ROOT_PARAMETER or D3D12_ROOT_DESCRIPTOR_TABLE
            // - Convert to D3D12_ROOT_SIGNATURE_DESC
            // - Call D3D12SerializeRootSignature
            // - Call ID3D12Device::CreateRootSignature
            // - Wrap in D3D12BindingLayout and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var layout = new D3D12BindingLayout(handle, desc, IntPtr.Zero, _device);
            _resources[handle] = layout;

            return layout;
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

            // TODO: IMPLEMENT - Allocate and populate D3D12 descriptor set
            // - Allocate descriptor handles from descriptor heap
            // - Create SRV/UAV/CBV descriptors using CreateShaderResourceView, CreateUnorderedAccessView, CreateConstantBufferView
            // - Wrap in D3D12BindingSet and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var bindingSet = new D3D12BindingSet(handle, layout, desc, IntPtr.Zero, _device);
            _resources[handle] = bindingSet;

            return bindingSet;
        }

        public ICommandList CreateCommandList(CommandListType type = CommandListType.Graphics)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // TODO: IMPLEMENT - Allocate D3D12 command list
            // - Get command allocator for current frame
            // - Call ID3D12Device::CreateCommandList with appropriate type
            // - Wrap in D3D12CommandList and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            IntPtr commandList = new IntPtr(_nextResourceHandle++); // Placeholder
            var cmdList = new D3D12CommandList(handle, type, this, commandList, _device);
            _resources[handle] = cmdList;

            return cmdList;
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

            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            if (_device5 == IntPtr.Zero)
            {
                throw new InvalidOperationException("ID3D12Device5 is not available for raytracing");
            }

            // TODO: IMPLEMENT - Create D3D12 acceleration structure
            // - Convert AccelStructDesc to D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS
            // - Call ID3D12Device5::GetRaytracingAccelerationStructurePrebuildInfo
            // - Create committed resources for scratch and result buffers
            // - Build acceleration structure using ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure
            // - Get GPU virtual address for result buffer
            // - Wrap in D3D12AccelStruct and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var accelStruct = new D3D12AccelStruct(handle, desc, IntPtr.Zero, null, 0UL, _device5);
            _resources[handle] = accelStruct;

            return accelStruct;
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

            // TODO: IMPLEMENT - Create D3D12 raytracing pipeline state object
            // - Convert RaytracingPipelineDesc to D3D12_STATE_OBJECT_DESC
            // - Add D3D12_DXIL_LIBRARY_SUBOBJECT for shaders
            // - Add D3D12_HIT_GROUP_SUBOBJECT for hit groups
            // - Add D3D12_RAYTRACING_SHADER_CONFIG_SUBOBJECT
            // - Add D3D12_RAYTRACING_PIPELINE_CONFIG_SUBOBJECT
            // - Add D3D12_GLOBAL_ROOT_SIGNATURE_SUBOBJECT for global binding layout
            // - Call ID3D12Device5::CreateStateObject
            // - Query for ID3D12StateObjectProperties to get shader identifiers
            // - Build shader binding table buffer
            // - Wrap in D3D12RaytracingPipeline and return

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            IBuffer sbtBuffer = null; // Placeholder - would be created from shader identifiers
            var pipeline = new D3D12RaytracingPipeline(handle, desc, IntPtr.Zero, IntPtr.Zero, sbtBuffer, _device5);
            _resources[handle] = pipeline;

            return pipeline;
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

            // TODO: IMPLEMENT - Execute D3D12 command lists
            // - Close command lists if not already closed
            // - Build array of ID3D12CommandList pointers
            // - Call ID3D12CommandQueue::ExecuteCommandLists
            // - Signal fence for synchronization if needed
        }

        public void WaitIdle()
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(D3D12Device));
            }

            // TODO: IMPLEMENT - Wait for GPU to idle
            // - Create or use existing fence
            // - Signal fence from command queue
            // - Wait for fence value on CPU using ID3D12Fence::SetEventOnCompletion
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

            // TODO: IMPLEMENT - Signal D3D12 fence
            // - Extract ID3D12Fence from IFence implementation
            // - Call ID3D12CommandQueue::Signal
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

            // TODO: IMPLEMENT - Wait for D3D12 fence
            // - Extract ID3D12Fence from IFence implementation
            // - Call ID3D12Fence::SetEventOnCompletion with event handle
            // - Wait for event on current thread
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

            // TODO: IMPLEMENT - Query D3D12 format support
            // - Call ID3D12Device::CheckFeatureSupport with D3D12_FEATURE_FORMAT_SUPPORT
            // - Check D3D12_FORMAT_SUPPORT flags against usage requirements
            // - Return true if format supports the requested usage

            // Placeholder: assume common formats are supported
            switch (format)
            {
                case TextureFormat.RGBA8_UNORM:
                case TextureFormat.RGBA8_SRGB:
                    return (usage & (TextureUsage.ShaderResource | TextureUsage.RenderTarget)) != 0;

                case TextureFormat.RGBA16_FLOAT:
                case TextureFormat.RGBA32_FLOAT:
                    return (usage & (TextureUsage.ShaderResource | TextureUsage.RenderTarget | TextureUsage.UnorderedAccess)) != 0;

                case TextureFormat.D24_UNORM_S8_UINT:
                case TextureFormat.D32_FLOAT:
                case TextureFormat.D32_FLOAT_S8_UINT:
                    return (usage & TextureUsage.DepthStencil) != 0;

                default:
                    return false;
            }
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

        #endregion

        #region D3D12 Resource Barrier Structures

        // DirectX 12 Resource Barrier Type
        private const uint D3D12_RESOURCE_BARRIER_TYPE_TRANSITION = 0;
        private const uint D3D12_RESOURCE_BARRIER_TYPE_ALIASING = 1;
        private const uint D3D12_RESOURCE_BARRIER_TYPE_UAV = 2;

        // DirectX 12 Resource Barrier Flags
        private const uint D3D12_RESOURCE_BARRIER_FLAG_NONE = 0;
        private const uint D3D12_RESOURCE_BARRIER_FLAG_BEGIN_ONLY = 0x1;
        private const uint D3D12_RESOURCE_BARRIER_FLAG_END_ONLY = 0x2;

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
        /// D3D12_RESOURCE_UAV_BARRIER structure for UAV barriers.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct D3D12_RESOURCE_UAV_BARRIER
        {
            public IntPtr pResource; // ID3D12Resource*
        }

        // COM interface method delegate for ResourceBarrier
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ResourceBarrierDelegate(IntPtr commandList, uint numBarriers, IntPtr pBarriers);

        // COM interface method delegate for Dispatch
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DispatchDelegate(IntPtr commandList, uint threadGroupCountX, uint threadGroupCountY, uint threadGroupCountZ);

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

        #endregion

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

            public D3D12Texture(IntPtr handle, TextureDesc desc, IntPtr nativeHandle = default(IntPtr))
                : this(handle, desc, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, nativeHandle)
            {
            }

            public D3D12Texture(IntPtr handle, TextureDesc desc, IntPtr d3d12Resource, IntPtr srvHandle, IntPtr device, IntPtr nativeHandle = default(IntPtr))
            {
                _internalHandle = handle;
                Desc = desc;
                _d3d12Resource = d3d12Resource;
                _srvHandle = srvHandle;
                _device = device;
                NativeHandle = nativeHandle != IntPtr.Zero ? nativeHandle : (_d3d12Resource != IntPtr.Zero ? _d3d12Resource : handle);
            }

            public void Dispose()
            {
                // TODO: Release D3D12 resource
                // - Call ID3D12Resource::Release() if _d3d12Resource is valid
                // - Free descriptor handles if allocated from heap
            }
        }

        private class D3D12Buffer : IBuffer, IResource
        {
            public BufferDesc Desc { get; }
            public IntPtr NativeHandle { get; private set; }
            private readonly IntPtr _internalHandle;
            private readonly IntPtr _d3d12Resource;
            private readonly IntPtr _device;

            public D3D12Buffer(IntPtr handle, BufferDesc desc, IntPtr d3d12Resource, IntPtr device)
            {
                _internalHandle = handle;
                Desc = desc;
                _d3d12Resource = d3d12Resource;
                _device = device;
                NativeHandle = _d3d12Resource != IntPtr.Zero ? _d3d12Resource : handle;
            }

            public void Dispose()
            {
                // TODO: Release D3D12 resource
                // - Call ID3D12Resource::Release() if _d3d12Resource is valid
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

            public D3D12GraphicsPipeline(IntPtr handle, GraphicsPipelineDesc desc, IntPtr d3d12PipelineState, IntPtr rootSignature, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _d3d12PipelineState = d3d12PipelineState;
                _rootSignature = rootSignature;
                _device = device;
            }

            public void Dispose()
            {
                // TODO: Release D3D12 pipeline state and root signature
                // - Call ID3D12PipelineState::Release()
                // - Call ID3D12RootSignature::Release()
            }
        }

        private class D3D12ComputePipeline : IComputePipeline, IResource
        {
            public ComputePipelineDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _d3d12PipelineState;
            private readonly IntPtr _rootSignature;
            private readonly IntPtr _device;

            public D3D12ComputePipeline(IntPtr handle, ComputePipelineDesc desc, IntPtr d3d12PipelineState, IntPtr rootSignature, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _d3d12PipelineState = d3d12PipelineState;
                _rootSignature = rootSignature;
                _device = device;
            }

            public void Dispose()
            {
                // TODO: Release D3D12 pipeline state and root signature
                // - Call ID3D12PipelineState::Release()
                // - Call ID3D12RootSignature::Release()
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

            public D3D12BindingLayout(IntPtr handle, BindingLayoutDesc desc, IntPtr rootSignature, IntPtr device)
            {
                _handle = handle;
                Desc = desc;
                _rootSignature = rootSignature;
                _device = device;
            }

            public void Dispose()
            {
                // TODO: Release D3D12 root signature
                // - Call ID3D12RootSignature::Release()
            }
        }

        private class D3D12BindingSet : IBindingSet, IResource
        {
            public IBindingLayout Layout { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _descriptorHeap;
            private readonly IntPtr _device;

            public D3D12BindingSet(IntPtr handle, IBindingLayout layout, BindingSetDesc desc, IntPtr descriptorHeap, IntPtr device)
            {
                _handle = handle;
                Layout = layout;
                _descriptorHeap = descriptorHeap;
                _device = device;
            }

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

            public D3D12AccelStruct(IntPtr handle, AccelStructDesc desc, IntPtr d3d12AccelStruct, IBuffer backingBuffer, ulong deviceAddress, IntPtr device5)
            {
                _handle = handle;
                Desc = desc;
                _d3d12AccelStruct = d3d12AccelStruct;
                _backingBuffer = backingBuffer;
                DeviceAddress = deviceAddress;
                _device5 = device5;
                IsTopLevel = desc.IsTopLevel;
            }

            public void Dispose()
            {
                // TODO: Release D3D12 acceleration structure
                // - Release backing buffer resource
                // Note: Acceleration structures are resources, released via ID3D12Resource::Release()
                _backingBuffer?.Dispose();
            }
        }

        private class D3D12RaytracingPipeline : IRaytracingPipeline, IResource
        {
            public RaytracingPipelineDesc Desc { get; }
            private readonly IntPtr _handle;
            private readonly IntPtr _d3d12StateObject;
            private readonly IntPtr _properties;
            private readonly IBuffer _sbtBuffer;
            private readonly IntPtr _device5;

            public D3D12RaytracingPipeline(IntPtr handle, Interfaces.RaytracingPipelineDesc desc, IntPtr d3d12StateObject, IntPtr properties, IBuffer sbtBuffer, IntPtr device5)
            {
                _handle = handle;
                Desc = desc;
                _d3d12StateObject = d3d12StateObject;
                _properties = properties;
                _sbtBuffer = sbtBuffer;
                _device5 = device5;
            }

            public void Dispose()
            {
                // TODO: Release D3D12 state object
                // - Call ID3D12StateObject::Release()
                // - Release properties interface
                // - Release SBT buffer
                _sbtBuffer?.Dispose();
            }
        }

        private class D3D12CommandList : ICommandList, IResource
        {
            private readonly IntPtr _handle;
            private readonly CommandListType _type;
            private readonly D3D12Device _device;
            private readonly IntPtr _d3d12CommandList;
            private readonly IntPtr _d3d12Device;
            private bool _isOpen;

            // Barrier tracking
            private readonly List<PendingBarrier> _pendingBarriers;
            private readonly Dictionary<object, ResourceState> _resourceStates;

            // Barrier entry structure
            private struct PendingBarrier
            {
                public IntPtr Resource;
                public uint Subresource;
                public uint StateBefore;
                public uint StateAfter;
            }

            public D3D12CommandList(IntPtr handle, CommandListType type, D3D12Device device, IntPtr d3d12CommandList, IntPtr d3d12Device)
            {
                _handle = handle;
                _type = type;
                _device = device;
                _d3d12CommandList = d3d12CommandList;
                _d3d12Device = d3d12Device;
                _isOpen = false;
                _pendingBarriers = new List<PendingBarrier>();
                _resourceStates = new Dictionary<object, ResourceState>();
            }

            public void Open()
            {
                if (_isOpen)
                {
                    return;
                }

                // TODO: Reset command list and allocator
                // - Call ID3D12CommandAllocator::Reset
                // - Call ID3D12GraphicsCommandList::Reset with allocator and PSO (if any)

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

                // TODO: Close command list
                // - Call ID3D12GraphicsCommandList::Close

                _isOpen = false;
            }

            // All ICommandList methods require full implementation
            // These are stubbed with TODO comments indicating D3D12 API calls needed
            // Implementation will be completed when DirectX 12 interop is added

            public void WriteBuffer(IBuffer buffer, byte[] data, int destOffset = 0) { /* TODO: Use upload heap or UpdateSubresources */ }
            public void WriteBuffer<T>(IBuffer buffer, T[] data, int destOffset = 0) where T : unmanaged { /* TODO: Use upload heap or UpdateSubresources */ }
            public void WriteTexture(ITexture texture, int mipLevel, int arraySlice, byte[] data) { /* TODO: UpdateSubresources */ }
            public void CopyBuffer(IBuffer dest, int destOffset, IBuffer src, int srcOffset, int size) { /* TODO: CopyBufferRegion */ }
            public void CopyTexture(ITexture dest, ITexture src) { /* TODO: CopyResource or CopyTextureRegion */ }
            public void ClearColorAttachment(IFramebuffer framebuffer, int attachmentIndex, Vector4 color) { /* TODO: ClearRenderTargetView */ }
            public void ClearDepthStencilAttachment(IFramebuffer framebuffer, float depth, byte stencil, bool clearDepth = true, bool clearStencil = true) { /* TODO: ClearDepthStencilView */ }
            public void ClearUAVFloat(ITexture texture, Vector4 value) { /* TODO: ClearUnorderedAccessViewFloat */ }
            public void ClearUAVUint(ITexture texture, uint value) { /* TODO: ClearUnorderedAccessViewUint */ }
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
            public void UAVBarrier(ITexture texture) { /* TODO: UAVBarrier */ }
            public void UAVBarrier(IBuffer buffer) { /* TODO: UAVBarrier */ }
            public void SetGraphicsState(GraphicsState state) { /* TODO: Set all graphics state */ }
            public void SetViewport(Viewport viewport) { /* TODO: RSSetViewports */ }
            public void SetViewports(Viewport[] viewports) { /* TODO: RSSetViewports */ }
            public void SetScissor(Rectangle scissor) { /* TODO: RSSetScissorRects */ }
            public void SetScissors(Rectangle[] scissors) { /* TODO: RSSetScissorRects */ }
            public void SetBlendConstant(Vector4 color) { /* TODO: OMSetBlendFactor */ }
            public void SetStencilRef(uint reference) { /* TODO: OMSetStencilRef */ }
            public void Draw(DrawArguments args) { /* TODO: DrawInstanced */ }
            public void DrawIndexed(DrawArguments args) { /* TODO: DrawIndexedInstanced */ }
            public void DrawIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride) { /* TODO: ExecuteIndirect */ }
            public void DrawIndexedIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride) { /* TODO: ExecuteIndirect */ }
            public void SetComputeState(ComputeState state) { /* TODO: Set compute state */ }
            public void Dispatch(int groupCountX, int groupCountY = 1, int groupCountZ = 1) { /* TODO: Dispatch */ }
            public void DispatchIndirect(IBuffer argumentBuffer, int offset) { /* TODO: ExecuteIndirect */ }
            public void SetRaytracingState(RaytracingState state) { /* TODO: Set raytracing state */ }
            public void DispatchRays(DispatchRaysArguments args) { /* TODO: DispatchRays */ }
            public void BuildBottomLevelAccelStruct(IAccelStruct accelStruct, GeometryDesc[] geometries) { /* TODO: BuildRaytracingAccelerationStructure */ }
            public void BuildTopLevelAccelStruct(IAccelStruct accelStruct, AccelStructInstance[] instances) { /* TODO: BuildRaytracingAccelerationStructure */ }
            public void CompactBottomLevelAccelStruct(IAccelStruct dest, IAccelStruct src) { /* TODO: CopyRaytracingAccelerationStructure */ }
            public void BeginDebugEvent(string name, Vector4 color) { /* TODO: BeginEvent */ }
            public void EndDebugEvent() { /* TODO: EndEvent */ }
            public void InsertDebugMarker(string name, Vector4 color) { /* TODO: SetMarker */ }

            public void Dispose()
            {
                // Command lists are returned to allocator, not destroyed individually
            }
        }

        #endregion
    }
}

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
    /// Metal device wrapper implementing IDevice interface for raytracing operations.
    ///
    /// Provides NVRHI-style abstractions for Metal raytracing resources:
    /// - Acceleration structures (BLAS/TLAS) using Metal 3.0 raytracing APIs
    /// - Raytracing pipelines with MTLRaytracingPipelineState
    /// - Resource creation and management
    ///
    /// Wraps native MTLDevice with Metal 3.0 raytracing support (macOS 12.0+, iOS 15.0+).
    /// Metal raytracing API: https://developer.apple.com/documentation/metal/metal_ray_tracing
    ///
    /// Based on Metal 3.0 raytracing architecture:
    /// - MTLAccelerationStructure for acceleration structures
    /// - MTLIntersectionFunctionTable for hit groups
    /// - MTLVisibleFunctionTable for shader functions
    /// - Argument buffers for resource binding
    /// </summary>
    public class MetalDevice : IDevice
    {
        private readonly MetalBackend _backend;
        private readonly IntPtr _device; // id<MTLDevice>
        private readonly GraphicsCapabilities _capabilities;
        private bool _disposed;

        // Resource tracking
        private readonly Dictionary<IntPtr, MetalTexture> _textures;
        private readonly Dictionary<IntPtr, MetalBuffer> _buffers;
        private readonly Dictionary<IntPtr, MetalSampler> _samplers;
        private readonly Dictionary<IntPtr, MetalShader> _shaders;
        private readonly Dictionary<IntPtr, MetalPipeline> _pipelines;
        private readonly Dictionary<IntPtr, MetalAccelStruct> _accelStructs;
        private readonly Dictionary<IntPtr, MetalRaytracingPipeline> _raytracingPipelines;
        private readonly Dictionary<IntPtr, MetalFramebuffer> _framebuffers;
        private readonly Dictionary<IntPtr, MetalBindingLayout> _bindingLayouts;
        private readonly Dictionary<IntPtr, MetalBindingSet> _bindingSets;
        private readonly Dictionary<IntPtr, MetalCommandList> _commandLists;
        private readonly Dictionary<IntPtr, MetalFence> _fences;

        private uint _nextResourceHandle;

        public GraphicsCapabilities Capabilities
        {
            get { return _capabilities; }
        }

        public GraphicsBackend Backend
        {
            get { return GraphicsBackend.Metal; }
        }

        public bool IsValid
        {
            get { return !_disposed && _device != IntPtr.Zero && _backend != null && _backend.IsInitialized; }
        }

        /// <summary>
        /// Creates a MetalDevice wrapper around a MetalBackend.
        /// MetalDevice provides IDevice interface for raytracing operations while delegating
        /// general graphics operations to MetalBackend.
        /// </summary>
        /// <param name="backend">Metal backend instance. Must not be null and must be initialized.</param>
        /// <exception cref="ArgumentNullException">Thrown if backend is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if backend is not initialized or doesn't support raytracing.</exception>
        public MetalDevice(MetalBackend backend)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            if (!backend.IsInitialized)
            {
                throw new InvalidOperationException("MetalBackend must be initialized before creating MetalDevice");
            }

            if (!backend.IsRaytracingEnabled)
            {
                throw new InvalidOperationException("MetalBackend must have raytracing enabled to create MetalDevice");
            }

            _backend = backend;
            
            // Get Metal device from backend
            _device = backend.GetMetalDevice();
            
            if (_device == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to obtain Metal device from backend");
            }

            _capabilities = backend.Capabilities;

            // Initialize resource dictionaries
            _textures = new Dictionary<IntPtr, MetalTexture>();
            _buffers = new Dictionary<IntPtr, MetalBuffer>();
            _samplers = new Dictionary<IntPtr, MetalSampler>();
            _shaders = new Dictionary<IntPtr, MetalShader>();
            _pipelines = new Dictionary<IntPtr, MetalPipeline>();
            _accelStructs = new Dictionary<IntPtr, MetalAccelStruct>();
            _raytracingPipelines = new Dictionary<IntPtr, MetalRaytracingPipeline>();
            _framebuffers = new Dictionary<IntPtr, MetalFramebuffer>();
            _bindingLayouts = new Dictionary<IntPtr, MetalBindingLayout>();
            _bindingSets = new Dictionary<IntPtr, MetalBindingSet>();
            _commandLists = new Dictionary<IntPtr, MetalCommandList>();
            _fences = new Dictionary<IntPtr, MetalFence>();

            _nextResourceHandle = 1;
        }

        #region Resource Creation

        public ITexture CreateTexture(TextureDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            // Create texture using MetalBackend
            IntPtr handle = _backend.CreateTexture(new TextureDescription
            {
                Width = desc.Width,
                Height = desc.Height,
                MipLevels = desc.MipLevels,
                Format = ConvertTextureFormat(desc.Format),
                Usage = ConvertTextureUsage(desc.Usage),
                IsCubemap = desc.Dimension == TextureDimension.TextureCube,
                DebugName = desc.DebugName
            });

            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var texture = new MetalTexture(handle, desc, _backend);
            _textures[handle] = texture;
            return texture;
        }

        public IBuffer CreateBuffer(BufferDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            // Create buffer using MetalBackend
            IntPtr handle = _backend.CreateBuffer(new BufferDescription
            {
                SizeInBytes = desc.ByteSize,
                Usage = ConvertBufferUsage(desc.Usage),
                DebugName = desc.DebugName
            });

            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var buffer = new MetalBuffer(handle, desc, _backend);
            _buffers[handle] = buffer;
            return buffer;
        }

        public ISampler CreateSampler(SamplerDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            // Create Metal sampler state
            IntPtr samplerState = MetalNative.CreateSamplerState(_device, ConvertSamplerDesc(desc));
            if (samplerState == IntPtr.Zero)
            {
                return null;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var sampler = new MetalSampler(handle, desc, samplerState);
            _samplers[handle] = sampler;
            return sampler;
        }

        public IShader CreateShader(ShaderDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            // Create shader from bytecode (Metal shader library)
            // For Metal, shaders are compiled from Metal Shading Language source or loaded from library
            // Bytecode for Metal would be Metal IR (AIR) or compiled library
            IntPtr defaultLibrary = _backend.GetDefaultLibrary();
            if (defaultLibrary == IntPtr.Zero)
            {
                return null;
            }
            IntPtr function = MetalNative.CreateFunctionFromLibrary(defaultLibrary, desc.EntryPoint ?? "main");
            if (function == IntPtr.Zero)
            {
                // Try to create from bytecode if provided (would need Metal shader compiler)
                return null;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var shader = new MetalShader(handle, desc, function, ConvertShaderType(desc.Type));
            _shaders[handle] = shader;
            return shader;
        }

        public IGraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc, IFramebuffer framebuffer)
        {
            if (!IsValid)
            {
                return null;
            }

            // Create render pipeline using MetalBackend
            PipelineDescription pipelineDesc = new PipelineDescription
            {
                VertexShader = GetShaderBytecode(desc.VertexShader),
                PixelShader = GetShaderBytecode(desc.PixelShader),
                InputLayout = ConvertInputLayout(desc.InputLayout),
                BlendState = ConvertBlendState(desc.BlendState),
                RasterizerState = ConvertRasterizerState(desc.RasterState),
                DepthStencilState = ConvertDepthStencilState(desc.DepthStencilState),
                DebugName = "GraphicsPipeline"
            };

            IntPtr handle = _backend.CreatePipeline(pipelineDesc);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var pipeline = new MetalPipeline(handle, desc, framebuffer);
            _pipelines[handle] = pipeline;
            return pipeline;
        }

        public IComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            // Create compute pipeline state
            IntPtr computeFunction = desc.ComputeShader != null ? GetShaderFunction(desc.ComputeShader) : IntPtr.Zero;
            if (computeFunction == IntPtr.Zero)
            {
                return null;
            }

            IntPtr computePipelineState = MetalNative.CreateComputePipelineState(_device, computeFunction);
            if (computePipelineState == IntPtr.Zero)
            {
                return null;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var pipeline = new MetalComputePipeline(handle, desc, computePipelineState);
            _pipelines[handle] = pipeline;
            return pipeline;
        }

        public IFramebuffer CreateFramebuffer(FramebufferDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var framebuffer = new MetalFramebuffer(handle, desc);
            _framebuffers[handle] = framebuffer;
            return framebuffer;
        }

        public IBindingLayout CreateBindingLayout(BindingLayoutDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            // Metal uses argument buffers for resource binding
            // Create argument buffer layout descriptor
            IntPtr argumentEncoder = MetalNative.CreateArgumentEncoder(_device, desc);
            if (argumentEncoder == IntPtr.Zero)
            {
                return null;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var layout = new MetalBindingLayout(handle, desc, argumentEncoder);
            _bindingLayouts[handle] = layout;
            return layout;
        }

        public IBindingSet CreateBindingSet(IBindingLayout layout, BindingSetDesc desc)
        {
            if (!IsValid || layout == null)
            {
                return null;
            }

            var metalLayout = layout as MetalBindingLayout;
            if (metalLayout == null)
            {
                return null;
            }

            // Create argument buffer for binding set
            IntPtr argumentBuffer = MetalNative.CreateArgumentBuffer(_device, metalLayout.ArgumentEncoder);
            if (argumentBuffer == IntPtr.Zero)
            {
                return null;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var bindingSet = new MetalBindingSet(handle, layout, desc, argumentBuffer, metalLayout.ArgumentEncoder);
            _bindingSets[handle] = bindingSet;
            return bindingSet;
        }

        public ICommandList CreateCommandList(CommandListType type = CommandListType.Graphics)
        {
            if (!IsValid)
            {
                return null;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var commandList = new MetalCommandList(handle, type, _backend);
            _commandLists[handle] = commandList;
            return commandList;
        }

        public ITexture CreateHandleForNativeTexture(IntPtr nativeHandle, TextureDesc desc)
        {
            if (!IsValid || nativeHandle == IntPtr.Zero)
            {
                return null;
            }

            // Wrap existing Metal texture
            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var texture = new MetalTexture(handle, desc, _backend, nativeHandle);
            _textures[handle] = texture;
            return texture;
        }

        #endregion

        #region Raytracing Resources

        public IAccelStruct CreateAccelStruct(AccelStructDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            // Metal 3.0 raytracing: Create acceleration structure
            // MTLAccelerationStructureDescriptor describes BLAS or TLAS
            IntPtr commandQueue = _backend.GetCommandQueue();
            if (commandQueue == IntPtr.Zero)
            {
                return null;
            }

            IntPtr accelStructDescriptor = MetalNative.CreateAccelerationStructureDescriptor(_device, desc);
            if (accelStructDescriptor == IntPtr.Zero)
            {
                return null;
            }

            // Build acceleration structure
            IntPtr accelStruct = MetalNative.CreateAccelerationStructure(_device, accelStructDescriptor, commandQueue);
            MetalNative.ReleaseAccelerationStructureDescriptor(accelStructDescriptor);

            if (accelStruct == IntPtr.Zero)
            {
                return null;
            }

            // Get device address (Metal uses resource IDs for addressing)
            ulong deviceAddress = MetalNative.GetAccelerationStructureResourceID(accelStruct);

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var accelStructWrapper = new MetalAccelStruct(handle, desc, accelStruct, deviceAddress);
            _accelStructs[handle] = accelStructWrapper;
            return accelStructWrapper;
        }

        public IRaytracingPipeline CreateRaytracingPipeline(Interfaces.RaytracingPipelineDesc desc)
        {
            if (!IsValid)
            {
                return null;
            }

            // Metal 3.0 raytracing: Create raytracing pipeline state
            // MTLRaytracingPipelineStateDescriptor contains shaders, hit groups, etc.
            IntPtr pipelineDescriptor = MetalNative.CreateRaytracingPipelineDescriptor(_device, desc);
            if (pipelineDescriptor == IntPtr.Zero)
            {
                return null;
            }

            IntPtr raytracingPipelineState = MetalNative.CreateRaytracingPipelineState(_device, pipelineDescriptor);
            MetalNative.ReleaseRaytracingPipelineDescriptor(pipelineDescriptor);

            if (raytracingPipelineState == IntPtr.Zero)
            {
                return null;
            }

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            var pipeline = new MetalRaytracingPipeline(handle, desc, raytracingPipelineState);
            _raytracingPipelines[handle] = pipeline;
            return pipeline;
        }

        #endregion

        #region Command Execution

        public void ExecuteCommandList(ICommandList commandList)
        {
            if (!IsValid || commandList == null)
            {
                return;
            }

            // Ensure command list is closed before execution
            if (commandList != null)
            {
                commandList.Close();
                // Command execution is handled by MetalBackend's command queue
                // The command list has already recorded commands via Open()/Close()
            }
        }

        public void ExecuteCommandLists(ICommandList[] commandLists)
        {
            if (!IsValid || commandLists == null)
            {
                return;
            }

            foreach (var commandList in commandLists)
            {
                ExecuteCommandList(commandList);
            }
        }

        public void WaitIdle()
        {
            if (!IsValid)
            {
                return;
            }

            // Wait for all GPU operations to complete
            IntPtr commandQueue = _backend.GetCommandQueue();
            if (commandQueue != IntPtr.Zero)
            {
                MetalNative.WaitUntilCommandQueueCompleted(commandQueue);
            }
        }

        public void Signal(IFence fence, ulong value)
        {
            if (!IsValid || fence == null)
            {
                return;
            }

            var metalFence = fence as MetalFence;
            if (metalFence != null)
            {
                metalFence.Signal(value);
            }
        }

        public void WaitFence(IFence fence, ulong value)
        {
            if (!IsValid || fence == null)
            {
                return;
            }

            var metalFence = fence as MetalFence;
            if (metalFence != null)
            {
                metalFence.Wait(value);
            }
        }

        #endregion

        #region Queries

        public int GetConstantBufferAlignment()
        {
            // Metal constant buffer alignment is typically 256 bytes
            return 256;
        }

        public int GetTextureAlignment()
        {
            // Metal texture data alignment
            return 256;
        }

        public bool IsFormatSupported(TextureFormat format, TextureUsage usage)
        {
            if (!IsValid)
            {
                return false;
            }

            // Query Metal device for format support
            MetalPixelFormat metalFormat = ConvertTextureFormatToMetal(format);
            return MetalNative.SupportsTextureFormat(_device, metalFormat, ConvertTextureUsageToMetal(usage));
        }

        public int GetCurrentFrameIndex()
        {
            if (!IsValid)
            {
                return 0;
            }

            // Return frame index from backend's frame tracking
            // Metal backend tracks frame index for multi-buffering (triple buffering: 0, 1, 2)
            // Frame index is used for per-frame resource management (constant buffers, etc.)
            return _backend.GetCurrentFrameIndex();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Dispose all resources
            foreach (var texture in _textures.Values)
            {
                texture.Dispose();
            }
            _textures.Clear();

            foreach (var buffer in _buffers.Values)
            {
                buffer.Dispose();
            }
            _buffers.Clear();

            foreach (var sampler in _samplers.Values)
            {
                sampler.Dispose();
            }
            _samplers.Clear();

            foreach (var shader in _shaders.Values)
            {
                shader.Dispose();
            }
            _shaders.Clear();

            foreach (var pipeline in _pipelines.Values)
            {
                pipeline.Dispose();
            }
            _pipelines.Clear();

            foreach (var accelStruct in _accelStructs.Values)
            {
                accelStruct.Dispose();
            }
            _accelStructs.Clear();

            foreach (var pipeline in _raytracingPipelines.Values)
            {
                pipeline.Dispose();
            }
            _raytracingPipelines.Clear();

            foreach (var framebuffer in _framebuffers.Values)
            {
                framebuffer.Dispose();
            }
            _framebuffers.Clear();

            foreach (var layout in _bindingLayouts.Values)
            {
                layout.Dispose();
            }
            _bindingLayouts.Clear();

            foreach (var bindingSet in _bindingSets.Values)
            {
                bindingSet.Dispose();
            }
            _bindingSets.Clear();

            foreach (var commandList in _commandLists.Values)
            {
                commandList.Dispose();
            }
            _commandLists.Clear();

            foreach (var fence in _fences.Values)
            {
                fence.Dispose();
            }
            _fences.Clear();

            _disposed = true;
        }

        #endregion

        #region Private Helper Methods

        private TextureFormat ConvertTextureFormat(TextureFormat format)
        {
            // Pass-through conversion: IDevice TextureFormat to IGraphicsBackend TextureFormat
            // Both use the same enum from Andastra.Runtime.MonoGame.Interfaces namespace
            return format;
        }

        private TextureUsage ConvertTextureUsage(TextureUsage usage)
        {
            TextureUsage result = TextureUsage.None;
            if ((usage & TextureUsage.ShaderResource) != 0) result |= Andastra.Runtime.MonoGame.Rendering.TextureUsage.ShaderResource;
            if ((usage & TextureUsage.RenderTarget) != 0) result |= Andastra.Runtime.MonoGame.Rendering.TextureUsage.RenderTarget;
            if ((usage & TextureUsage.DepthStencil) != 0) result |= Andastra.Runtime.MonoGame.Rendering.TextureUsage.DepthStencil;
            if ((usage & TextureUsage.UnorderedAccess) != 0) result |= Andastra.Runtime.MonoGame.Rendering.TextureUsage.UnorderedAccess;
            return result;
        }

        private BufferUsage ConvertBufferUsage(BufferUsageFlags usage)
        {
            BufferUsage result = BufferUsage.None;
            if ((usage & BufferUsageFlags.VertexBuffer) != 0) result |= Andastra.Runtime.MonoGame.Rendering.BufferUsage.Vertex;
            if ((usage & BufferUsageFlags.IndexBuffer) != 0) result |= Andastra.Runtime.MonoGame.Rendering.BufferUsage.Index;
            if ((usage & BufferUsageFlags.ConstantBuffer) != 0) result |= Andastra.Runtime.MonoGame.Rendering.BufferUsage.Constant;
            if ((usage & BufferUsageFlags.UnorderedAccess) != 0) result |= Andastra.Runtime.MonoGame.Rendering.BufferUsage.UnorderedAccess;
            return result;
        }

        private MetalPixelFormat ConvertTextureFormatToMetal(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8G8B8A8_UNorm:
                    return MetalPixelFormat.RGBA8UNorm;
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    return MetalPixelFormat.RGBA8UNormSRGB;
                case TextureFormat.B8G8R8A8_UNorm:
                    return MetalPixelFormat.BGRA8UNorm;
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    return MetalPixelFormat.BGRA8UNormSRGB;
                case TextureFormat.R16G16B16A16_Float:
                    return MetalPixelFormat.RGBA16Float;
                case TextureFormat.R32G32B32A32_Float:
                    return MetalPixelFormat.RGBA32Float;
                case TextureFormat.D32_Float:
                    return MetalPixelFormat.Depth32Float;
                case TextureFormat.D24_UNorm_S8_UInt:
                    return MetalPixelFormat.Depth24Stencil8;
                default:
                    return MetalPixelFormat.RGBA8UNorm;
            }
        }

        private MetalTextureUsage ConvertTextureUsageToMetal(TextureUsage usage)
        {
            MetalTextureUsage result = MetalTextureUsage.None;
            if ((usage & TextureUsage.ShaderResource) != 0) result |= MetalTextureUsage.ShaderRead;
            if ((usage & TextureUsage.RenderTarget) != 0) result |= MetalTextureUsage.RenderTarget;
            if ((usage & TextureUsage.DepthStencil) != 0) result |= MetalTextureUsage.DepthStencil;
            if ((usage & TextureUsage.UnorderedAccess) != 0) result |= MetalTextureUsage.ShaderWrite;
            return result;
        }

        private MetalSamplerDescriptor ConvertSamplerDesc(SamplerDesc desc)
        {
            return new MetalSamplerDescriptor
            {
                MinFilter = ConvertSamplerFilter(desc.MinFilter),
                MagFilter = ConvertSamplerFilter(desc.MagFilter),
                MipFilter = ConvertSamplerFilter(desc.MipFilter),
                AddressModeU = ConvertAddressMode(desc.AddressU),
                AddressModeV = ConvertAddressMode(desc.AddressV),
                AddressModeW = ConvertAddressMode(desc.AddressW),
                MipLodBias = desc.MipLodBias,
                MaxAnisotropy = (uint)desc.MaxAnisotropy,
                CompareFunction = ConvertCompareFunc(desc.CompareFunc),
                MinLod = desc.MinLod,
                MaxLod = desc.MaxLod
            };
        }

        private MetalSamplerFilter ConvertSamplerFilter(SamplerFilter filter)
        {
            switch (filter)
            {
                case SamplerFilter.Point:
                    return MetalSamplerFilter.Nearest;
                case SamplerFilter.Linear:
                    return MetalSamplerFilter.Linear;
                case SamplerFilter.Anisotropic:
                    return MetalSamplerFilter.Linear; // Metal handles anisotropy separately
                default:
                    return MetalSamplerFilter.Linear;
            }
        }

        private MetalSamplerAddressMode ConvertAddressMode(SamplerAddressMode mode)
        {
            switch (mode)
            {
                case SamplerAddressMode.Wrap:
                    return MetalSamplerAddressMode.Repeat;
                case SamplerAddressMode.Mirror:
                    return MetalSamplerAddressMode.MirrorRepeat;
                case SamplerAddressMode.Clamp:
                    return MetalSamplerAddressMode.ClampToEdge;
                case SamplerAddressMode.Border:
                    return MetalSamplerAddressMode.ClampToBorderColor;
                case SamplerAddressMode.MirrorOnce:
                    return MetalSamplerAddressMode.MirrorClampToEdge;
                default:
                    return MetalSamplerAddressMode.Repeat;
            }
        }

        private MetalCompareFunction ConvertCompareFunc(CompareFunc func)
        {
            switch (func)
            {
                case CompareFunc.Never:
                    return MetalCompareFunction.Never;
                case CompareFunc.Less:
                    return MetalCompareFunction.Less;
                case CompareFunc.Equal:
                    return MetalCompareFunction.Equal;
                case CompareFunc.LessEqual:
                    return MetalCompareFunction.LessEqual;
                case CompareFunc.Greater:
                    return MetalCompareFunction.Greater;
                case CompareFunc.NotEqual:
                    return MetalCompareFunction.NotEqual;
                case CompareFunc.GreaterEqual:
                    return MetalCompareFunction.GreaterEqual;
                case CompareFunc.Always:
                    return MetalCompareFunction.Always;
                default:
                    return MetalCompareFunction.Less;
            }
        }

        private ShaderType ConvertShaderType(ShaderType type)
        {
            // ShaderType enum is the same in both namespaces
            return type;
        }

        private byte[] GetShaderBytecode(IShader shader)
        {
            if (shader == null)
            {
                return null;
            }

            var metalShader = shader as MetalShader;
            if (metalShader != null)
            {
                return metalShader.Desc.Bytecode;
            }

            return shader.Desc.Bytecode;
        }

        private IntPtr GetShaderFunction(IShader shader)
        {
            if (shader == null)
            {
                return IntPtr.Zero;
            }

            var metalShader = shader as MetalShader;
            if (metalShader != null)
            {
                return metalShader.Function;
            }

            return IntPtr.Zero;
        }

        private InputLayout ConvertInputLayout(InputLayoutDesc layout)
        {
            if (layout.Attributes == null || layout.Attributes.Length == 0)
            {
                return new InputLayout { Elements = new InputElement[0] };
            }

            var elements = new InputElement[layout.Attributes.Length];
            for (int i = 0; i < layout.Attributes.Length; i++)
            {
                var attr = layout.Attributes[i];
                elements[i] = new InputElement
                {
                    Slot = (byte)attr.BufferIndex,
                    AlignedByteOffset = (ushort)attr.Offset,
                    Format = ConvertTextureFormat(attr.Format)
                };
            }

            return new InputLayout { Elements = elements };
        }

        private BlendState ConvertBlendState(BlendStateDesc state)
        {
            return new BlendState
            {
                BlendEnable = state.RenderTargets != null && state.RenderTargets.Length > 0 && state.RenderTargets[0].BlendEnable,
                SrcBlend = ConvertBlendFactor(state.RenderTargets != null && state.RenderTargets.Length > 0 ? state.RenderTargets[0].SrcBlend : BlendFactor.One),
                DstBlend = ConvertBlendFactor(state.RenderTargets != null && state.RenderTargets.Length > 0 ? state.RenderTargets[0].DestBlend : BlendFactor.Zero),
                SrcBlendAlpha = ConvertBlendFactor(state.RenderTargets != null && state.RenderTargets.Length > 0 ? state.RenderTargets[0].SrcBlendAlpha : BlendFactor.One),
                DstBlendAlpha = ConvertBlendFactor(state.RenderTargets != null && state.RenderTargets.Length > 0 ? state.RenderTargets[0].DestBlendAlpha : BlendFactor.Zero),
                BlendOp = ConvertBlendOp(state.RenderTargets != null && state.RenderTargets.Length > 0 ? state.RenderTargets[0].BlendOp : BlendOp.Add),
                BlendOpAlpha = ConvertBlendOp(state.RenderTargets != null && state.RenderTargets.Length > 0 ? state.RenderTargets[0].BlendOpAlpha : BlendOp.Add)
            };
        }

        private BlendFactor ConvertBlendFactor(BlendFactor factor)
        {
            switch (factor)
            {
                case BlendFactor.Zero:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.Zero;
                case BlendFactor.One:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.One;
                case BlendFactor.SrcColor:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.SrcColor;
                case BlendFactor.InvSrcColor:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.InvSrcColor;
                case BlendFactor.SrcAlpha:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.SrcAlpha;
                case BlendFactor.InvSrcAlpha:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.InvSrcAlpha;
                case BlendFactor.DstAlpha:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.DstAlpha;
                case BlendFactor.InvDstAlpha:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.InvDstAlpha;
                case BlendFactor.DstColor:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.DstColor;
                case BlendFactor.InvDstColor:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.InvDstColor;
                default:
                    return Andastra.Runtime.MonoGame.Rendering.BlendFactor.One;
            }
        }

        private BlendOp ConvertBlendOp(BlendOp op)
        {
            switch (op)
            {
                case BlendOp.Add:
                    return Andastra.Runtime.MonoGame.Rendering.BlendOp.Add;
                case BlendOp.Subtract:
                    return Andastra.Runtime.MonoGame.Rendering.BlendOp.Subtract;
                case BlendOp.ReverseSubtract:
                    return Andastra.Runtime.MonoGame.Rendering.BlendOp.ReverseSubtract;
                case BlendOp.Min:
                    return Andastra.Runtime.MonoGame.Rendering.BlendOp.Min;
                case BlendOp.Max:
                    return Andastra.Runtime.MonoGame.Rendering.BlendOp.Max;
                default:
                    return Andastra.Runtime.MonoGame.Rendering.BlendOp.Add;
            }
        }

        private RasterizerState ConvertRasterizerState(RasterStateDesc state)
        {
            return new RasterizerState
            {
                CullMode = ConvertCullMode(state.CullMode),
                FillMode = ConvertFillMode(state.FillMode),
                FrontCounterClockwise = state.FrontCCW,
                DepthBias = state.DepthBias,
                SlopeScaledDepthBias = state.SlopeScaledDepthBias
            };
        }

        private CullMode ConvertCullMode(CullMode mode)
        {
            switch (mode)
            {
                case CullMode.None:
                    return Andastra.Runtime.MonoGame.Rendering.CullMode.None;
                case CullMode.Front:
                    return Andastra.Runtime.MonoGame.Rendering.CullMode.Front;
                case CullMode.Back:
                    return Andastra.Runtime.MonoGame.Rendering.CullMode.Back;
                default:
                    return Andastra.Runtime.MonoGame.Rendering.CullMode.Back;
            }
        }

        private FillMode ConvertFillMode(FillMode mode)
        {
            switch (mode)
            {
                case FillMode.Solid:
                    return Andastra.Runtime.MonoGame.Rendering.FillMode.Solid;
                case FillMode.Wireframe:
                    return Andastra.Runtime.MonoGame.Rendering.FillMode.Wireframe;
                default:
                    return Andastra.Runtime.MonoGame.Rendering.FillMode.Solid;
            }
        }

        private DepthStencilState ConvertDepthStencilState(DepthStencilStateDesc state)
        {
            return new DepthStencilState
            {
                DepthWriteEnable = state.DepthWriteEnable,
                DepthFunc = ConvertCompareFunc(state.DepthFunc),
                StencilEnable = state.StencilEnable,
                StencilReadMask = state.StencilReadMask,
                StencilWriteMask = state.StencilWriteMask
            };
        }

        #endregion
    }

    #region Metal Resource Wrapper Classes

    // Metal resource wrapper classes implementing IDevice interfaces
    // These wrap native Metal objects and provide IDevice interface compatibility

    internal class MetalTexture : ITexture
    {
        private readonly IntPtr _handle;
        private readonly TextureDesc _desc;
        private readonly MetalBackend _backend;
        private readonly IntPtr _nativeHandle;
        private bool _disposed;

        public TextureDesc Desc { get { return _desc; } }
        public IntPtr NativeHandle { get { return _nativeHandle != IntPtr.Zero ? _nativeHandle : _handle; } }

        public MetalTexture(IntPtr handle, TextureDesc desc, MetalBackend backend, IntPtr nativeHandle = default(IntPtr))
        {
            _handle = handle;
            _desc = desc;
            _backend = backend;
            _nativeHandle = nativeHandle;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero && _nativeHandle == IntPtr.Zero)
                {
                    _backend.DestroyResource(_handle);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalBuffer : IBuffer
    {
        private readonly IntPtr _handle;
        private readonly BufferDesc _desc;
        private readonly MetalBackend _backend;
        private bool _disposed;

        public BufferDesc Desc { get { return _desc; } }
        public IntPtr NativeHandle { get { return _handle; } }

        public MetalBuffer(IntPtr handle, BufferDesc desc, MetalBackend backend)
        {
            _handle = handle;
            _desc = desc;
            _backend = backend;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    _backend.DestroyResource(_handle);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalSampler : ISampler
    {
        private readonly IntPtr _handle;
        private readonly SamplerDesc _desc;
        private readonly IntPtr _samplerState;
        private bool _disposed;

        public SamplerDesc Desc { get { return _desc; } }

        public MetalSampler(IntPtr handle, SamplerDesc desc, IntPtr samplerState)
        {
            _handle = handle;
            _desc = desc;
            _samplerState = samplerState;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_samplerState != IntPtr.Zero)
                {
                    MetalNative.ReleaseSamplerState(_samplerState);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalShader : IShader
    {
        private readonly IntPtr _handle;
        private readonly ShaderDesc _desc;
        private readonly IntPtr _function;
        private readonly ShaderType _type;
        private bool _disposed;

        public ShaderDesc Desc { get { return _desc; } }
        public ShaderType Type { get { return _type; } }
        public IntPtr Function { get { return _function; } }

        public MetalShader(IntPtr handle, ShaderDesc desc, IntPtr function, ShaderType type)
        {
            _handle = handle;
            _desc = desc;
            _function = function;
            _type = type;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_function != IntPtr.Zero)
                {
                    MetalNative.ReleaseFunction(_function);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalPipeline : IGraphicsPipeline
    {
        private readonly IntPtr _handle;
        private readonly GraphicsPipelineDesc _desc;
        private readonly IFramebuffer _framebuffer;
        private bool _disposed;

        public GraphicsPipelineDesc Desc { get { return _desc; } }

        public MetalPipeline(IntPtr handle, GraphicsPipelineDesc desc, IFramebuffer framebuffer)
        {
            _handle = handle;
            _desc = desc;
            _framebuffer = framebuffer;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }

    internal class MetalComputePipeline : IComputePipeline
    {
        private readonly IntPtr _handle;
        private readonly ComputePipelineDesc _desc;
        private readonly IntPtr _computePipelineState;
        private bool _disposed;

        public ComputePipelineDesc Desc { get { return _desc; } }

        public MetalComputePipeline(IntPtr handle, ComputePipelineDesc desc, IntPtr computePipelineState)
        {
            _handle = handle;
            _desc = desc;
            _computePipelineState = computePipelineState;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_computePipelineState != IntPtr.Zero)
                {
                    MetalNative.ReleaseComputePipelineState(_computePipelineState);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalAccelStruct : IAccelStruct
    {
        private readonly IntPtr _handle;
        private readonly AccelStructDesc _desc;
        private readonly IntPtr _accelStruct;
        private readonly ulong _deviceAddress;
        private bool _disposed;

        public AccelStructDesc Desc { get { return _desc; } }
        public bool IsTopLevel { get { return _desc.IsTopLevel; } }
        public ulong DeviceAddress { get { return _deviceAddress; } }

        // Internal accessor for native acceleration structure handle
        internal IntPtr NativeHandle { get { return _accelStruct; } }

        public MetalAccelStruct(IntPtr handle, AccelStructDesc desc, IntPtr accelStruct, ulong deviceAddress)
        {
            _handle = handle;
            _desc = desc;
            _accelStruct = accelStruct;
            _deviceAddress = deviceAddress;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_accelStruct != IntPtr.Zero)
                {
                    MetalNative.ReleaseAccelerationStructure(_accelStruct);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalRaytracingPipeline : IRaytracingPipeline
    {
        private readonly IntPtr _handle;
        private readonly Interfaces.RaytracingPipelineDesc _desc;
        private readonly IntPtr _raytracingPipelineState;
        private bool _disposed;

        public Interfaces.RaytracingPipelineDesc Desc { get { return _desc; } }

        public MetalRaytracingPipeline(IntPtr handle, Interfaces.RaytracingPipelineDesc desc, IntPtr raytracingPipelineState)
        {
            _handle = handle;
            _desc = desc;
            _raytracingPipelineState = raytracingPipelineState;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_raytracingPipelineState != IntPtr.Zero)
                {
                    MetalNative.ReleaseRaytracingPipelineState(_raytracingPipelineState);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalFramebuffer : IFramebuffer
    {
        private readonly IntPtr _handle;
        private readonly FramebufferDesc _desc;
        private bool _disposed;

        public FramebufferDesc Desc { get { return _desc; } }

        public MetalFramebuffer(IntPtr handle, FramebufferDesc desc)
        {
            _handle = handle;
            _desc = desc;
        }

        public FramebufferInfo GetInfo()
        {
            var info = new FramebufferInfo
            {
                Width = 0,
                Height = 0,
                SampleCount = 1
            };

            if (_desc.ColorAttachments != null && _desc.ColorAttachments.Length > 0)
            {
                var texture = _desc.ColorAttachments[0].Texture;
                if (texture != null)
                {
                    info.ColorFormats = new TextureFormat[] { texture.Desc.Format };
                    info.Width = texture.Desc.Width;
                    info.Height = texture.Desc.Height;
                }
            }

            if (_desc.DepthAttachment.Texture != null)
            {
                info.DepthFormat = _desc.DepthAttachment.Texture.Desc.Format;
            }

            return info;
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    internal class MetalBindingLayout : IBindingLayout
    {
        private readonly IntPtr _handle;
        private readonly BindingLayoutDesc _desc;
        private readonly IntPtr _argumentEncoder;
        private bool _disposed;

        public BindingLayoutDesc Desc { get { return _desc; } }
        public IntPtr ArgumentEncoder { get { return _argumentEncoder; } }

        public MetalBindingLayout(IntPtr handle, BindingLayoutDesc desc, IntPtr argumentEncoder)
        {
            _handle = handle;
            _desc = desc;
            _argumentEncoder = argumentEncoder;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_argumentEncoder != IntPtr.Zero)
                {
                    MetalNative.ReleaseArgumentEncoder(_argumentEncoder);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalBindingSet : IBindingSet
    {
        private readonly IntPtr _handle;
        private readonly IBindingLayout _layout;
        private readonly BindingSetDesc _desc;
        private readonly IntPtr _argumentBuffer;
        private readonly IntPtr _argumentEncoder;
        private bool _disposed;

        public IBindingLayout Layout { get { return _layout; } }

        public MetalBindingSet(IntPtr handle, IBindingLayout layout, BindingSetDesc desc, IntPtr argumentBuffer, IntPtr argumentEncoder)
        {
            _handle = handle;
            _layout = layout;
            _desc = desc;
            _argumentBuffer = argumentBuffer;
            _argumentEncoder = argumentEncoder;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_argumentBuffer != IntPtr.Zero)
                {
                    MetalNative.ReleaseArgumentBuffer(_argumentBuffer);
                }
                _disposed = true;
            }
        }
    }

    internal class MetalCommandList : ICommandList
    {
        private readonly IntPtr _handle;
        private readonly CommandListType _type;
        private readonly MetalBackend _backend;
        private bool _disposed;
        private bool _isOpen;
        private IntPtr _currentRenderCommandEncoder; // id<MTLRenderCommandEncoder>

        public MetalCommandList(IntPtr handle, CommandListType type, MetalBackend backend)
        {
            _handle = handle;
            _type = type;
            _backend = backend;
            _isOpen = false;
            _currentRenderCommandEncoder = IntPtr.Zero;
        }

        public void Open()
        {
            if (_disposed || _isOpen)
            {
                return;
            }
            // Begin recording commands to Metal command buffer
            _isOpen = true;
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }
            // End recording commands
            _isOpen = false;
        }

        // Resource Operations - delegate to MetalBackend or implement via Metal command encoder
        public void WriteBuffer(IBuffer buffer, byte[] data, int destOffset = 0)
        {
            if (!_isOpen || buffer == null)
            {
                return;
            }
            // TODO: Implement buffer write via Metal command encoder
            // Metal uses MTLBlitCommandEncoder::copyFromBuffer:sourceOffset:toBuffer:destinationOffset:size
        }

        public void WriteBuffer<T>(IBuffer buffer, T[] data, int destOffset = 0) where T : unmanaged
        {
            if (!_isOpen || buffer == null || data == null)
            {
                return;
            }
            // TODO: Implement typed buffer write
        }

        public void WriteTexture(ITexture texture, int mipLevel, int arraySlice, byte[] data)
        {
            if (!_isOpen || texture == null)
            {
                return;
            }
            // TODO: Implement texture write via Metal command encoder
        }

        public void CopyBuffer(IBuffer dest, int destOffset, IBuffer src, int srcOffset, int size)
        {
            if (!_isOpen || dest == null || src == null)
            {
                return;
            }
            // TODO: Implement buffer copy via Metal blit command encoder
        }

        public void CopyTexture(ITexture dest, ITexture src)
        {
            if (!_isOpen || dest == null || src == null)
            {
                return;
            }
            // TODO: Implement texture copy via Metal blit command encoder
        }

        public void ClearColorAttachment(IFramebuffer framebuffer, int attachmentIndex, Vector4 color)
        {
            if (!_isOpen || framebuffer == null)
            {
                return;
            }
            // TODO: Implement color attachment clear
        }

        public void ClearDepthStencilAttachment(IFramebuffer framebuffer, float depth, byte stencil, bool clearDepth = true, bool clearStencil = true)
        {
            if (!_isOpen || framebuffer == null)
            {
                return;
            }
            // TODO: Implement depth/stencil clear
        }

        public void ClearUAVFloat(ITexture texture, Vector4 value)
        {
            if (!_isOpen || texture == null)
            {
                return;
            }
            // TODO: Implement UAV clear for float
        }

        public void ClearUAVUint(ITexture texture, uint value)
        {
            if (!_isOpen || texture == null)
            {
                return;
            }
            // TODO: Implement UAV clear for uint
        }

        // Resource State Transitions - Metal handles state transitions automatically, but we track them
        public void SetTextureState(ITexture texture, ResourceState state)
        {
            if (!_isOpen || texture == null)
            {
                return;
            }
            // Metal handles texture state transitions automatically via resource barriers
            // This is mainly for tracking/logging purposes
        }

        public void SetBufferState(IBuffer buffer, ResourceState state)
        {
            if (!_isOpen || buffer == null)
            {
                return;
            }
            // Metal handles buffer state transitions automatically
        }

        public void CommitBarriers()
        {
            if (!_isOpen)
            {
                return;
            }
            // Metal commits barriers automatically when encoding commands
        }

        public void UAVBarrier(ITexture texture)
        {
            if (!_isOpen)
            {
                return;
            }
            // Metal handles UAV barriers automatically
        }

        public void UAVBarrier(IBuffer buffer)
        {
            if (!_isOpen)
            {
                return;
            }
            // Metal handles UAV barriers automatically
        }

        // Graphics State
        public void SetGraphicsState(GraphicsState state)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Implement graphics state setting
            // This would set pipeline, framebuffer, viewports, binding sets, etc.
        }

        public void SetViewport(Viewport viewport)
        {
            if (!_isOpen)
            {
                return;
            }

            // Viewport can only be set on an active render command encoder
            // The render command encoder is created by SetGraphicsState when a render pass begins
            // Based on Metal API: MTLRenderCommandEncoder::setViewport(MTLViewport)
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516251-setviewport
            if (_currentRenderCommandEncoder == IntPtr.Zero)
            {
                // Render command encoder not available - SetGraphicsState must be called first to begin render pass
                return;
            }

            // Set viewport on render command encoder
            // Convert Viewport struct to MetalViewport struct
            // Metal viewport uses double precision for coordinates
            MetalViewport metalViewport = new MetalViewport
            {
                OriginX = viewport.X,
                OriginY = viewport.Y,
                Width = viewport.Width,
                Height = viewport.Height,
                Znear = viewport.MinDepth,
                Zfar = viewport.MaxDepth
            };
            MetalNative.SetViewport(_currentRenderCommandEncoder, metalViewport);
        }

        public void SetViewports(Viewport[] viewports)
        {
            if (!_isOpen || viewports == null || viewports.Length == 0)
            {
                return;
            }

            // Multiple viewports can only be set on an active render command encoder
            // Based on Metal API: MTLRenderCommandEncoder::setViewport(MTLViewport)
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516251-setviewport
            // Note: Metal sets one viewport at a time. For viewport arrays, use geometry shader viewport arrays.
            if (_currentRenderCommandEncoder == IntPtr.Zero)
            {
                // Render command encoder not available - SetGraphicsState must be called first to begin render pass
                return;
            }

            // Set multiple viewports on render command encoder
            // Metal supports multiple viewports (viewport arrays) for multi-view rendering
            // Metal API: setViewport: - sets a single viewport. For multiple viewports, set each one individually.
            // TODO: Optimize to use native array marshalling if Metal adds setViewports:count: API in future
            for (int i = 0; i < viewports.Length; i++)
            {
                MetalViewport metalViewport = new MetalViewport
                {
                    OriginX = viewports[i].X,
                    OriginY = viewports[i].Y,
                    Width = viewports[i].Width,
                    Height = viewports[i].Height,
                    Znear = viewports[i].MinDepth,
                    Zfar = viewports[i].MaxDepth
                };
                MetalNative.SetViewport(_currentRenderCommandEncoder, metalViewport);
            }
        }

        public void SetScissor(Rectangle scissor)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Set scissor rectangle
        }

        public void SetScissors(Rectangle[] scissors)
        {
            if (!_isOpen || scissors == null)
            {
                return;
            }
            // TODO: Set multiple scissor rectangles
        }

        public void SetBlendConstant(Vector4 color)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Set blend constant color
        }

        public void SetStencilRef(uint reference)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Set stencil reference value
        }

        // Draw Commands
        public void Draw(DrawArguments args)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Implement draw command via Metal render command encoder
        }

        public void DrawIndexed(DrawArguments args)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Implement indexed draw command
        }

        public void DrawIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride)
        {
            if (!_isOpen || argumentBuffer == null)
            {
                return;
            }
            // TODO: Implement indirect draw
        }

        public void DrawIndexedIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride)
        {
            if (!_isOpen || argumentBuffer == null)
            {
                return;
            }
            // TODO: Implement indexed indirect draw
        }

        // Compute State
        public void SetComputeState(ComputeState state)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Implement compute state setting
        }

        public void Dispatch(int groupCountX, int groupCountY = 1, int groupCountZ = 1)
        {
            if (!_isOpen)
            {
                return;
            }
            // Delegate to MetalBackend
            _backend.DispatchCompute(groupCountX, groupCountY, groupCountZ);
        }

        public void DispatchIndirect(IBuffer argumentBuffer, int offset)
        {
            if (!_isOpen || argumentBuffer == null)
            {
                return;
            }
            // TODO: Implement indirect dispatch
        }

        // Raytracing Commands
        public void SetRaytracingState(RaytracingState state)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Implement raytracing state setting for Metal 3.0
        }

        public void DispatchRays(DispatchRaysArguments args)
        {
            if (!_isOpen)
            {
                return;
            }
            // TODO: Implement dispatch rays for Metal 3.0 raytracing
            // Metal uses MTLIntersectionFunctionTable and MTLVisibleFunctionTable
        }

        public void BuildBottomLevelAccelStruct(IAccelStruct accelStruct, GeometryDesc[] geometries)
        {
            if (!_isOpen || accelStruct == null)
            {
                return;
            }
            // TODO: Implement BLAS build for Metal 3.0
        }

        public void BuildTopLevelAccelStruct(IAccelStruct accelStruct, AccelStructInstance[] instances)
        {
            if (!_isOpen || accelStruct == null)
            {
                return;
            }
            // TODO: Implement TLAS build for Metal 3.0
        }

        public void CompactBottomLevelAccelStruct(IAccelStruct dest, IAccelStruct src)
        {
            if (!_isOpen || dest == null || src == null)
            {
                return;
            }

            // Metal 3.0 acceleration structure compaction
            // Compaction copies the source acceleration structure to the destination, removing fragmentation
            // This is typically used after building with AllowCompaction flag to reduce memory usage
            // Metal API: [MTLAccelerationStructureCommandEncoder copyAccelerationStructure:sourceAccelerationStructure:toAccelerationStructure:destinationAccelerationStructure:]

            // Cast to MetalAccelStruct to access native handles
            MetalAccelStruct destMetal = dest as MetalAccelStruct;
            MetalAccelStruct srcMetal = src as MetalAccelStruct;

            if (destMetal == null || srcMetal == null)
            {
                // Not a Metal acceleration structure - cannot compact
                return;
            }

            // Validate that source is a bottom-level acceleration structure
            if (srcMetal.IsTopLevel)
            {
                // Compaction is typically only done for BLAS, not TLAS
                return;
            }

            // Get native acceleration structure handles
            IntPtr srcAccelStruct = srcMetal.NativeHandle;
            IntPtr destAccelStruct = destMetal.NativeHandle;

            if (srcAccelStruct == IntPtr.Zero || destAccelStruct == IntPtr.Zero)
            {
                return;
            }

            // Get command buffer handle - in Metal, command lists encode into command buffers
            // The handle represents the command buffer for this command list
            // For acceleration structure operations, we need a command buffer to create the encoder
            IntPtr commandBuffer = _handle;
            if (commandBuffer == IntPtr.Zero)
            {
                return;
            }

            // Create acceleration structure command encoder for compaction operation
            // Metal API: MTLCommandBuffer::accelerationStructureCommandEncoder()
            // Note: This requires the actual MTLCommandBuffer object, not just a handle
            // In a real implementation, this would go through Objective-C interop to access the command buffer
            IntPtr accelStructCommandEncoder = MetalNative.CreateAccelerationStructureCommandEncoder(commandBuffer);
            if (accelStructCommandEncoder == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Execute compaction: copy source to destination
                // This operation compacts the acceleration structure by copying it, removing internal fragmentation
                // Metal API: [MTLAccelerationStructureCommandEncoder copyAccelerationStructure:srcAccelStruct toAccelerationStructure:destAccelStruct]
                MetalNative.CopyAccelerationStructure(accelStructCommandEncoder, srcAccelStruct, destAccelStruct);

                // End encoding - Metal requires explicit endEncoding call
                MetalNative.EndAccelerationStructureCommandEncoding(accelStructCommandEncoder);
            }
            finally
            {
                // Command encoder lifetime is managed by Metal, but we release our reference
                // The actual encoder is retained by the command buffer until execution
            }
        }

        // Debug
        public void BeginDebugEvent(string name, Vector4 color)
        {
            if (!_isOpen)
            {
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            // Push debug group on active command encoder if available, otherwise on command buffer
            // Based on Metal API: -[MTLCommandBuffer pushDebugGroup:] and -[MTLRenderCommandEncoder pushDebugGroup:]
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/1458038-pushdebuggroup
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/2866163-pushdebuggroup
            // Debug groups pushed on encoders are scoped to that encoder's commands
            // Debug groups pushed on command buffers apply to all encoders created from that buffer
            IntPtr target = _currentRenderCommandEncoder;
            if (target == IntPtr.Zero)
            {
                // No active encoder, use command buffer from backend
                target = _backend.GetCurrentCommandBuffer();
                if (target == IntPtr.Zero)
                {
                    return;
                }
            }

            MetalNative.PushDebugGroup(target, name);
        }

        public void EndDebugEvent()
        {
            if (!_isOpen)
            {
                return;
            }

            // Pop debug group from active command encoder if available, otherwise from command buffer
            // Based on Metal API: -[MTLCommandBuffer popDebugGroup] and -[MTLRenderCommandEncoder popDebugGroup]
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/1458040-popdebuggroup
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/2866164-popdebuggroup
            IntPtr target = _currentRenderCommandEncoder;
            if (target == IntPtr.Zero)
            {
                // No active encoder, use command buffer from backend
                target = _backend.GetCurrentCommandBuffer();
                if (target == IntPtr.Zero)
                {
                    return;
                }
            }

            MetalNative.PopDebugGroup(target);
        }

        public void InsertDebugMarker(string name, Vector4 color)
        {
            if (!_isOpen)
            {
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            // Insert debug marker by pushing and immediately popping a debug group
            // This creates an instant marker point in the command stream
            // Based on Metal API: pushDebugGroup: followed immediately by popDebugGroup
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/1458038-pushdebuggroup
            // Note: Metal doesn't have a direct "insert marker" API, so we use push/pop pattern
            IntPtr target = _currentRenderCommandEncoder;
            if (target == IntPtr.Zero)
            {
                // No active encoder, use command buffer from backend
                target = _backend.GetCurrentCommandBuffer();
                if (target == IntPtr.Zero)
                {
                    return;
                }
            }

            MetalNative.PushDebugGroup(target, name);
            MetalNative.PopDebugGroup(target);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_isOpen)
            {
                Close();
            }

            _disposed = true;
        }
    }

    internal class MetalFence : IFence
    {
        private readonly IntPtr _handle;
        private ulong _completedValue;
        private bool _disposed;

        public ulong CompletedValue { get { return _completedValue; } }

        public MetalFence(IntPtr handle)
        {
            _handle = handle;
            _completedValue = 0;
        }

        public void Signal(ulong value)
        {
            // Signal fence from GPU
            // Metal uses MTLSharedEvent or MTLEvent for synchronization
            _completedValue = value;
        }

        public void Wait(ulong value)
        {
            // Wait for fence value on CPU
            // Metal uses MTLEvent or MTLSharedEvent for synchronization
            while (_completedValue < value)
            {
                System.Threading.Thread.Sleep(1);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    MetalNative.ReleaseEvent(_handle);
                }
                _disposed = true;
            }
        }
    }

    #endregion

    #region Additional Metal Native Interop Extensions

    /// <summary>
    /// Extended Metal native interop for raytracing and additional functionality.
    /// These functions extend MetalNative with additional P/Invoke declarations needed for IDevice implementation.
    /// </summary>
    internal static partial class MetalNative
    {
        // Additional native interop functions for MetalDevice
        // These extend the MetalNative class in MetalBackend.cs

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateSamplerState(IntPtr device, MetalSamplerDescriptor descriptor);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseSamplerState(IntPtr samplerState);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateComputePipelineState(IntPtr device, IntPtr computeFunction);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseComputePipelineState(IntPtr computePipelineState);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateArgumentEncoder(IntPtr device, BindingLayoutDesc desc);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseArgumentEncoder(IntPtr argumentEncoder);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateArgumentBuffer(IntPtr device, IntPtr argumentEncoder);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseArgumentBuffer(IntPtr argumentBuffer);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateAccelerationStructureDescriptor(IntPtr device, AccelStructDesc desc);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseAccelerationStructureDescriptor(IntPtr descriptor);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateAccelerationStructure(IntPtr device, IntPtr descriptor, IntPtr commandQueue);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseAccelerationStructure(IntPtr accelStruct);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern ulong GetAccelerationStructureResourceID(IntPtr accelStruct);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateRaytracingPipelineDescriptor(IntPtr device, RaytracingPipelineDesc desc);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseRaytracingPipelineDescriptor(IntPtr descriptor);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateRaytracingPipelineState(IntPtr device, IntPtr descriptor);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseRaytracingPipelineState(IntPtr raytracingPipelineState);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern bool SupportsTextureFormat(IntPtr device, MetalPixelFormat format, MetalTextureUsage usage);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern IntPtr CreateEvent(IntPtr device);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void ReleaseEvent(IntPtr evt);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void WaitUntilCommandQueueCompleted(IntPtr commandQueue);

        // Render command encoder state
        // Based on Metal API: MTLRenderCommandEncoder::setViewport(MTLViewport)
        // MTLViewport structure: { double originX, originY, width, height, znear, zfar }
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516251-setviewport
        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void SetViewport(IntPtr renderCommandEncoder, MetalViewport viewport);

        // Objective-C runtime for calling Metal debug methods
        // Metal debug methods (pushDebugGroup:, popDebugGroup) are Objective-C instance methods
        // These require using objc_msgSend to call them from C#
        // On 64-bit systems, objc_msgSend returns a value even for void methods, so we declare it as IntPtr
        private const string LibObjC = "/usr/lib/libobjc.A.dylib";

        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_void(IntPtr receiver, IntPtr selector);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_void_string(IntPtr receiver, IntPtr selector, IntPtr nsString);

        [DllImport(LibObjC, EntryPoint = "sel_registerName")]
        private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string str);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation", EntryPoint = "CFStringCreateWithCString")]
        private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPStr)] string cStr, uint encoding);

        private const uint kCFStringEncodingUTF8 = 0x08000100;

        /// <summary>
        /// Creates an NSString from a C# string for use with Objective-C methods.
        /// The caller is responsible for releasing the NSString using CFRelease.
        /// </summary>
        private static IntPtr CreateNSString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return IntPtr.Zero;
            }
            return CFStringCreateWithCString(IntPtr.Zero, str, kCFStringEncodingUTF8);
        }

        /// <summary>
        /// Releases a CFString/NSString created with CreateNSString.
        /// </summary>
        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFRelease")]
        private static extern void CFRelease(IntPtr cf);

        /// <summary>
        /// Pushes a debug group onto a Metal command buffer or command encoder.
        /// Based on Metal API: -[MTLCommandBuffer pushDebugGroup:] and -[MTLRenderCommandEncoder pushDebugGroup:]
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/1458038-pushdebuggroup
        /// </summary>
        public static void PushDebugGroup(IntPtr commandBufferOrEncoder, string debugGroupName)
        {
            if (commandBufferOrEncoder == IntPtr.Zero || string.IsNullOrEmpty(debugGroupName))
            {
                return;
            }

            IntPtr nsString = CreateNSString(debugGroupName);
            if (nsString == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("pushDebugGroup:");
                // objc_msgSend returns a value even for void methods, we ignore it
                objc_msgSend_void_string(commandBufferOrEncoder, selector, nsString);
            }
            finally
            {
                CFRelease(nsString);
            }
        }

        /// <summary>
        /// Pops a debug group from a Metal command buffer or command encoder.
        /// Based on Metal API: -[MTLCommandBuffer popDebugGroup] and -[MTLRenderCommandEncoder popDebugGroup]
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/1458040-popdebuggroup
        /// </summary>
        public static void PopDebugGroup(IntPtr commandBufferOrEncoder)
        {
            if (commandBufferOrEncoder == IntPtr.Zero)
            {
                return;
            }

            IntPtr selector = sel_registerName("popDebugGroup");
            // objc_msgSend returns a value even for void methods, we ignore it
            objc_msgSend_void(commandBufferOrEncoder, selector);
        }
    }

    #endregion

    #region Supporting Structures

    /// <summary>
    /// Metal viewport structure matching MTLViewport.
    /// Based on Metal API: MTLViewport structure definition
    /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlviewport
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalViewport
    {
        public double OriginX;
        public double OriginY;
        public double Width;
        public double Height;
        public double Znear;
        public double Zfar;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalSamplerDescriptor
    {
        public MetalSamplerFilter MinFilter;
        public MetalSamplerFilter MagFilter;
        public MetalSamplerFilter MipFilter;
        public MetalSamplerAddressMode AddressModeU;
        public MetalSamplerAddressMode AddressModeV;
        public MetalSamplerAddressMode AddressModeW;
        public float MipLodBias;
        public uint MaxAnisotropy;
        public MetalCompareFunction CompareFunction;
        public float MinLod;
        public float MaxLod;
    }

    internal enum MetalSamplerFilter : uint
    {
        Nearest = 0,
        Linear = 1
    }

    internal enum MetalSamplerAddressMode : uint
    {
        ClampToEdge = 0,
        MirrorClampToEdge = 1,
        Repeat = 2,
        MirrorRepeat = 3,
        ClampToBorderColor = 4
    }

    [Flags]
    internal enum MetalTextureUsage : uint
    {
        None = 0,
        ShaderRead = 1,
        ShaderWrite = 2,
        RenderTarget = 4,
        DepthStencil = 8,
        PixelFormatView = 16
    }

    #endregion
}


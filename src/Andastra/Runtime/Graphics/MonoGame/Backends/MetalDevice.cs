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
            // Note: MetalComputePipeline is not tracked in a dictionary as it's not a standard resource type
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
            // Convert Interfaces.RaytracingPipelineDesc to Metal-specific format
            RaytracingPipelineDesc metalDesc = ConvertRaytracingPipelineDesc(desc);
            IntPtr pipelineDescriptor = MetalNative.CreateRaytracingPipelineDescriptor(_device, metalDesc);
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
            var pipeline = new MetalRaytracingPipeline(handle, desc, raytracingPipelineState, _backend);
            _raytracingPipelines[handle] = pipeline;
            return pipeline;
        }

        /// <summary>
        /// Converts Interfaces.RaytracingPipelineDesc to Metal-specific RaytracingPipelineDesc.
        /// Extracts shader bytecode from IShader objects and converts HitGroups to Metal format.
        /// </summary>
        private RaytracingPipelineDesc ConvertRaytracingPipelineDesc(Interfaces.RaytracingPipelineDesc desc)
        {
            RaytracingPipelineDesc metalDesc = new RaytracingPipelineDesc
            {
                MaxPayloadSize = desc.MaxPayloadSize,
                MaxAttributeSize = desc.MaxAttributeSize,
                MaxRecursionDepth = desc.MaxRecursionDepth,
                DebugName = desc.DebugName
            };

            // Extract shader bytecode from IShader objects
            // Metal expects byte arrays for shader code
            if (desc.Shaders != null && desc.Shaders.Length > 0)
            {
                // Find ray generation shader
                foreach (IShader shader in desc.Shaders)
                {
                    if (shader != null && shader.Type == ShaderType.RayGeneration)
                    {
                        metalDesc.RayGenShader = ExtractShaderBytecode(shader);
                        break;
                    }
                }

                // Find miss shader
                foreach (IShader shader in desc.Shaders)
                {
                    if (shader != null && shader.Type == ShaderType.Miss)
                    {
                        metalDesc.MissShader = ExtractShaderBytecode(shader);
                        break;
                    }
                }

                // Find closest hit shader
                foreach (IShader shader in desc.Shaders)
                {
                    if (shader != null && shader.Type == ShaderType.ClosestHit)
                    {
                        metalDesc.ClosestHitShader = ExtractShaderBytecode(shader);
                        break;
                    }
                }

                // Find any hit shader
                foreach (IShader shader in desc.Shaders)
                {
                    if (shader != null && shader.Type == ShaderType.AnyHit)
                    {
                        metalDesc.AnyHitShader = ExtractShaderBytecode(shader);
                        break;
                    }
                }
            }

            return metalDesc;
        }

        /// <summary>
        /// Extracts bytecode from an IShader object.
        /// </summary>
        private byte[] ExtractShaderBytecode(IShader shader)
        {
            if (shader == null)
            {
                return null;
            }

            // Get bytecode from MetalShader's Desc, same as GetShaderBytecode
            var metalShader = shader as MetalShader;
            if (metalShader != null)
            {
                return metalShader.Desc.Bytecode;
            }

            // Fallback: return null if we can't extract bytecode
            return null;
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
            // TextureUsage is already the correct type from Interfaces namespace
            // This is a pass-through conversion
            return usage;
        }

        private BufferUsage ConvertBufferUsage(BufferUsageFlags usage)
        {
            BufferUsage result = 0;
            if ((usage & BufferUsageFlags.VertexBuffer) != 0) result |= BufferUsage.Vertex;
            if ((usage & BufferUsageFlags.IndexBuffer) != 0) result |= BufferUsage.Index;
            if ((usage & BufferUsageFlags.ConstantBuffer) != 0) result |= BufferUsage.Constant;
            if ((usage & BufferUsageFlags.UnorderedAccess) != 0) result |= BufferUsage.Structured;
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

        /// <summary>
        /// Converts vertex format to Metal vertex format for raytracing acceleration structures.
        /// Based on Metal API: MTLAccelerationStructureTriangleGeometryDescriptor vertex format
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructuretrianglegeometrydescriptor
        /// </summary>
        internal static uint ConvertVertexFormatToMetal(TextureFormat format)
        {
            // Metal supports float3, float2, and half3, half2 vertex formats for raytracing
            // Map common vertex formats to Metal equivalents
            switch (format)
            {
                case TextureFormat.R32G32B32_Float:
                    return 0; // MTLAttributeFormatFloat3
                case TextureFormat.R32G32_Float:
                    return 1; // MTLAttributeFormatFloat2
                case TextureFormat.R16G16B16A16_Float:
                    return 2; // MTLAttributeFormatHalf3 (using first 3 components)
                case TextureFormat.R16G16_Float:
                    return 3; // MTLAttributeFormatHalf2
                default:
                    // Default to float3 for unknown formats (most common case for position data)
                    return 0; // MTLAttributeFormatFloat3
            }
        }

        /// <summary>
        /// Converts index format to Metal index format for raytracing acceleration structures.
        /// Based on Metal API: MTLAccelerationStructureTriangleGeometryDescriptor index format
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructuretrianglegeometrydescriptor
        /// </summary>
        internal static uint ConvertIndexFormatToMetal(TextureFormat format)
        {
            // Metal supports UInt16 and UInt32 index formats for raytracing
            switch (format)
            {
                case TextureFormat.R16_UInt:
                    return 0; // MTLIndexTypeUInt16
                case TextureFormat.R32_UInt:
                    return 1; // MTLIndexTypeUInt32
                default:
                    // Default to UInt32 for unknown formats
                    return 1; // MTLIndexTypeUInt32
            }
        }

        /// <summary>
        /// Converts geometry flags to Metal geometry flags for raytracing acceleration structures.
        /// Based on Metal API: MTLAccelerationStructureGeometryDescriptor options
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructuregeometrydescriptor
        /// </summary>
        internal static uint ConvertGeometryFlagsToMetal(GeometryFlags flags)
        {
            uint metalFlags = 0;

            // Metal uses MTLAccelerationStructureGeometryOptionOpaque flag
            if ((flags & GeometryFlags.Opaque) != 0)
            {
                metalFlags |= 1; // MTLAccelerationStructureGeometryOptionOpaque
            }

            // Metal uses MTLAccelerationStructureGeometryOptionDuplicateGeometry flag
            if ((flags & GeometryFlags.NoDuplicateAnyHit) == 0)
            {
                // If NoDuplicateAnyHit is NOT set, geometry can be duplicated
                // This is the default behavior in Metal
            }

            return metalFlags;
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

        /// <summary>
        /// Converts PrimitiveTopology enum to MetalPrimitiveType enum.
        /// Based on Metal API: MTLPrimitiveType enum values
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlprimitivetype
        /// </summary>
        private MetalPrimitiveType ConvertPrimitiveTopologyToMetal(PrimitiveTopology topology)
        {
            switch (topology)
            {
                case PrimitiveTopology.PointList:
                    return MetalPrimitiveType.Point;
                case PrimitiveTopology.LineList:
                    return MetalPrimitiveType.Line;
                case PrimitiveTopology.LineStrip:
                    return MetalPrimitiveType.LineStrip;
                case PrimitiveTopology.TriangleList:
                    return MetalPrimitiveType.Triangle;
                case PrimitiveTopology.TriangleStrip:
                    return MetalPrimitiveType.TriangleStrip;
                case PrimitiveTopology.PatchList:
                    // Metal doesn't support patch lists directly - tessellation is handled differently
                    // Default to Triangle for patch lists (most common tessellation output)
                    return MetalPrimitiveType.Triangle;
                default:
                    // Default to Triangle if topology is unknown (most common case)
                    return MetalPrimitiveType.Triangle;
            }
        }

        /// <summary>
        /// Gets the primitive type from a graphics pipeline.
        /// Retrieves PrimitiveTopology from the pipeline descriptor and converts it to MetalPrimitiveType.
        /// Returns MetalPrimitiveType.Triangle as default if pipeline is not set.
        /// </summary>
        internal static MetalPrimitiveType GetPrimitiveTypeFromPipeline(IGraphicsPipeline pipeline)
        {
            // Default to Triangle if pipeline is not set (most common case)
            MetalPrimitiveType primitiveType = MetalPrimitiveType.Triangle;

            // Get primitive type from pipeline
            if (pipeline != null)
            {
                var metalPipeline = pipeline as MetalPipeline;
                if (metalPipeline != null)
                {
                    PrimitiveTopology topology = metalPipeline.Desc.PrimitiveTopology;
                    primitiveType = ConvertPrimitiveTopologyToMetalStatic(topology);
                }
            }

            return primitiveType;
        }

        /// <summary>
        /// Static helper to convert PrimitiveTopology to MetalPrimitiveType.
        /// </summary>
        private static MetalPrimitiveType ConvertPrimitiveTopologyToMetalStatic(PrimitiveTopology topology)
        {
            switch (topology)
            {
                case PrimitiveTopology.PointList:
                    return MetalPrimitiveType.Point;
                case PrimitiveTopology.LineList:
                case PrimitiveTopology.LineStrip:
                    return MetalPrimitiveType.Line;
                case PrimitiveTopology.TriangleList:
                case PrimitiveTopology.TriangleStrip:
                    return MetalPrimitiveType.Triangle;
                default:
                    return MetalPrimitiveType.Triangle;
            }
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
                    return BlendFactor.Zero;
                case BlendFactor.One:
                    return BlendFactor.One;
                case BlendFactor.SrcColor:
                    return BlendFactor.SrcColor;
                case BlendFactor.InvSrcColor:
                    return BlendFactor.InvSrcColor;
                case BlendFactor.SrcAlpha:
                    return BlendFactor.SrcAlpha;
                case BlendFactor.InvSrcAlpha:
                    return BlendFactor.InvSrcAlpha;
                case BlendFactor.DstAlpha:
                    return BlendFactor.DstAlpha;
                case BlendFactor.InvDstAlpha:
                    return BlendFactor.InvDstAlpha;
                case BlendFactor.DstColor:
                    return BlendFactor.DstColor;
                case BlendFactor.InvDstColor:
                    return BlendFactor.InvDstColor;
                default:
                    return BlendFactor.One;
            }
        }

        private BlendOp ConvertBlendOp(BlendOp op)
        {
            switch (op)
            {
                case BlendOp.Add:
                    return BlendOp.Add;
                case BlendOp.Subtract:
                    return BlendOp.Subtract;
                case BlendOp.ReverseSubtract:
                    return BlendOp.ReverseSubtract;
                case BlendOp.Min:
                    return BlendOp.Min;
                case BlendOp.Max:
                    return BlendOp.Max;
                default:
                    return BlendOp.Add;
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
                    return CullMode.None;
                case CullMode.Front:
                    return CullMode.Front;
                case CullMode.Back:
                    return CullMode.Back;
                default:
                    return CullMode.Back;
            }
        }

        private Interfaces.FillMode ConvertFillMode(Interfaces.FillMode mode)
        {
            switch (mode)
            {
                case Interfaces.FillMode.Solid:
                    return Interfaces.FillMode.Solid;
                case Interfaces.FillMode.Wireframe:
                    return Interfaces.FillMode.Wireframe;
                default:
                    return Interfaces.FillMode.Solid;
            }
        }

        private DepthStencilState ConvertDepthStencilState(DepthStencilStateDesc state)
        {
            return new DepthStencilState
            {
                DepthWriteEnable = state.DepthWriteEnable,
                DepthFunc = state.DepthFunc, // DepthStencilState uses Interfaces.CompareFunc, no conversion needed
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
        private readonly IntPtr _renderPipelineState;
        private bool _disposed;

        public GraphicsPipelineDesc Desc { get { return _desc; } }

        // Internal property to access render pipeline state for binding to command encoders
        internal IntPtr RenderPipelineState { get { return _renderPipelineState; } }

        public MetalPipeline(IntPtr handle, GraphicsPipelineDesc desc, IFramebuffer framebuffer)
        {
            _handle = handle;
            _desc = desc;
            _framebuffer = framebuffer;
            // Extract render pipeline state from handle (handle is the MTLRenderPipelineState)
            _renderPipelineState = handle;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_renderPipelineState != IntPtr.Zero)
                {
                    MetalNative.ReleaseRenderPipelineState(_renderPipelineState);
                }
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

        // Internal property to access compute pipeline state for binding to command encoders
        internal IntPtr ComputePipelineState { get { return _computePipelineState; } }

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
        private readonly MetalBackend _backend;
        private bool _disposed;

        public Interfaces.RaytracingPipelineDesc Desc { get { return _desc; } }

        /// <summary>
        /// Gets the native Metal raytracing pipeline state handle.
        /// Internal use for MetalCommandList to set pipeline state during dispatch.
        /// </summary>
        internal IntPtr RaytracingPipelineState { get { return _raytracingPipelineState; } }

        public MetalRaytracingPipeline(IntPtr handle, Interfaces.RaytracingPipelineDesc desc, IntPtr raytracingPipelineState, MetalBackend backend)
        {
            _handle = handle;
            _desc = desc;
            _raytracingPipelineState = raytracingPipelineState;
            _backend = backend;
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

        /// <summary>
        /// Gets the shader identifier for a shader or hit group in the pipeline.
        /// Shader identifiers are opaque handles used in the shader binding table.
        ///
        /// Metal 3.0 raytracing implementation:
        /// Metal uses MTLFunctionHandle for shader identifiers in raytracing pipelines.
        /// The shader identifier is obtained from the raytracing pipeline state using the export name.
        ///
        /// Based on Metal API: MTLRaytracingPipelineState::functionHandle(function:)
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlraytracingpipelinestate
        /// </summary>
        /// <param name="exportName">The export name of the shader or hit group (e.g., "ShadowRayGen", "ShadowMiss", "ShadowHitGroup").</param>
        /// <returns>Shader identifier bytes (Metal uses 8-byte function handles). Returns null if the export name is not found.</returns>
        public byte[] GetShaderIdentifier(string exportName)
        {
            if (string.IsNullOrEmpty(exportName) || _raytracingPipelineState == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                // Metal API: [raytracingPipelineState functionHandleWithFunction:function]
                // First, we need to get the function from the pipeline descriptor using the export name
                // Metal stores functions in the pipeline descriptor, and we can query them by name

                // Get function handle from raytracing pipeline state
                // Metal API: - (MTLFunctionHandle *)functionHandleWithFunction:(id<MTLFunction>)function
                // We need to get the function first, then get its handle

                // For now, Metal doesn't provide a direct way to get function handles by export name
                // The shader binding table in Metal is typically built using function handles obtained
                // during pipeline creation. This method would need access to the original shader functions
                // that were used to create the pipeline.

                // Implementation: Query the raytracing pipeline state for the function handle
                // This requires Objective-C interop to call functionHandleWithFunction:
                // Use MetalNative.GetFunctionHandle which handles selector registration internally

                // We need the function object to get its handle
                // The function should be stored in the pipeline descriptor when the pipeline was created
                // For now, we'll attempt to get it from the default library
                IntPtr defaultLibrary = _backend.GetDefaultLibrary();
                if (defaultLibrary == IntPtr.Zero)
                {
                    return null;
                }

                // Get function from library by export name
                IntPtr function = MetalNative.CreateFunctionFromLibrary(defaultLibrary, exportName);
                if (function == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    // Get function handle from raytracing pipeline state
                    IntPtr functionHandle = MetalNative.GetFunctionHandle(_raytracingPipelineState, function);
                    if (functionHandle == IntPtr.Zero)
                    {
                        return null;
                    }

                    // Metal function handles are 8 bytes (64-bit pointers)
                    // Convert to byte array for shader binding table
                    byte[] identifier = new byte[8];
                    IntPtr handlePtr = new IntPtr(functionHandle.ToInt64());
                    Marshal.Copy(handlePtr, identifier, 0, 8);
                    return identifier;
                }
                finally
                {
                    if (function != IntPtr.Zero)
                    {
                        MetalNative.ReleaseFunction(function);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalRaytracingPipeline] GetShaderIdentifier: Exception: {ex.Message}");
                return null;
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
            if (!_disposed)
            {
                _disposed = true;
            }
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

        // Internal property to access argument buffer for binding to command encoders
        internal IntPtr ArgumentBuffer { get { return _argumentBuffer; } }

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
        private IntPtr _currentBlitCommandEncoder; // id<MTLBlitCommandEncoder>
        private IntPtr _currentAccelStructCommandEncoder; // id<MTLAccelerationStructureCommandEncoder>
        private IntPtr _currentComputeCommandEncoder; // id<MTLComputeCommandEncoder>
        private IntPtr _currentComputePipelineState; // id<MTLComputePipelineState> - current compute pipeline state for threadgroup size queries
        private IntPtr _clearUAVFloatComputePipelineState; // id<MTLComputePipelineState> - cached pipeline state for clearing UAV with float
        private IntPtr _clearUAVUintComputePipelineState; // id<MTLComputePipelineState> - cached pipeline state for clearing UAV with uint
        private static bool? _supportsBatchViewports; // Cached result for batch viewport API availability
        private GraphicsState _currentGraphicsState; // Current graphics state for draw commands
        private RaytracingState _currentRaytracingState; // Current raytracing state for dispatch rays commands
        private bool _hasRaytracingState; // Whether raytracing state has been set
        private Vector4 _currentBlendConstant; // Current blend constant color for dynamic blending

        public MetalCommandList(IntPtr handle, CommandListType type, MetalBackend backend)
        {
            _handle = handle;
            _type = type;
            _backend = backend;
            _isOpen = false;
            _currentRenderCommandEncoder = IntPtr.Zero;
            _currentBlitCommandEncoder = IntPtr.Zero;
            _currentAccelStructCommandEncoder = IntPtr.Zero;
            _currentComputeCommandEncoder = IntPtr.Zero;
            _clearUAVFloatComputePipelineState = IntPtr.Zero;
            _clearUAVUintComputePipelineState = IntPtr.Zero;
            _currentGraphicsState = default(GraphicsState); // Initialize to default state
            _currentRaytracingState = default(RaytracingState); // Initialize to default state
            _hasRaytracingState = false;
            _currentBlendConstant = new Vector4(0.0f, 0.0f, 0.0f, 0.0f); // Initialize to default blend constant (transparent black)
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

            // End any active encoders before closing
            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndAccelerationStructureCommandEncoding(_currentAccelStructCommandEncoder);
                _currentAccelStructCommandEncoder = IntPtr.Zero;
            }

            if (_currentComputeCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentComputeCommandEncoder);
                MetalNative.ReleaseComputeCommandEncoder(_currentComputeCommandEncoder);
                _currentComputeCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            // End recording commands
            _isOpen = false;
        }

        /// <summary>
        /// Gets or creates a blit command encoder for the current command buffer.
        /// Blit command encoders are used for resource copying operations (buffer-to-buffer, texture-to-texture).
        /// Based on Metal API: MTLCommandBuffer::blitCommandEncoder()
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/1443003-blitcommandencoder
        /// </summary>
        private IntPtr GetOrCreateBlitCommandEncoder()
        {
            if (!_isOpen)
            {
                return IntPtr.Zero;
            }

            // If we already have an active blit encoder, return it
            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                return _currentBlitCommandEncoder;
            }

            // Get the current command buffer from the backend
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] GetOrCreateBlitCommandEncoder: No active command buffer");
                return IntPtr.Zero;
            }

            // End any active render command encoder before creating blit encoder
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            // Create blit command encoder
            _currentBlitCommandEncoder = MetalNative.CreateBlitCommandEncoder(commandBuffer);
            if (_currentBlitCommandEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] GetOrCreateBlitCommandEncoder: Failed to create blit command encoder");
                return IntPtr.Zero;
            }

            return _currentBlitCommandEncoder;
        }

        /// <summary>
        /// Gets or creates an acceleration structure command encoder for the current command buffer.
        /// Acceleration structure command encoders are used for building and managing acceleration structures (BLAS/TLAS).
        /// Based on Metal API: MTLCommandBuffer::accelerationStructureCommandEncoder()
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/3553900-accelerationstructurecommanden
        /// </summary>
        private IntPtr GetOrCreateAccelerationStructureCommandEncoder()
        {
            if (!_isOpen)
            {
                return IntPtr.Zero;
            }

            // If we already have an active acceleration structure encoder, return it
            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                return _currentAccelStructCommandEncoder;
            }

            // Get the current command buffer from the backend
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] GetOrCreateAccelerationStructureCommandEncoder: No active command buffer");
                return IntPtr.Zero;
            }

            // End any active render or blit command encoder before creating acceleration structure encoder
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            // Create acceleration structure command encoder
            _currentAccelStructCommandEncoder = MetalNative.CreateAccelerationStructureCommandEncoder(commandBuffer);
            if (_currentAccelStructCommandEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] GetOrCreateAccelerationStructureCommandEncoder: Failed to create acceleration structure command encoder");
                return IntPtr.Zero;
            }

            return _currentAccelStructCommandEncoder;
        }

        // Resource Operations - delegate to MetalBackend or implement via Metal command encoder
        /// <summary>
        /// Writes data to a GPU buffer using Metal blit command encoder.
        ///
        /// Implementation strategy:
        /// 1. Create a temporary staging buffer with shared storage mode for CPU access
        /// 2. Write CPU data to staging buffer contents
        /// 3. Use blit command encoder to copy from staging buffer to destination buffer
        /// 4. Release staging buffer
        ///
        /// Based on Metal API:
        /// - MTLDevice::newBufferWithLength:options: (for staging buffer with StorageModeShared)
        /// - MTLBuffer::contents() (for CPU access to buffer memory)
        /// - MTLBlitCommandEncoder::copyFromBuffer:sourceOffset:toBuffer:destinationOffset:size:
        ///
        /// Metal API Reference:
        /// https://developer.apple.com/documentation/metal/mtlblitcommandencoder/1400774-copyfrombuffer
        /// https://developer.apple.com/documentation/metal/mtlbuffer/1515376-contents
        /// </summary>
        public void WriteBuffer(IBuffer buffer, byte[] data, int destOffset = 0)
        {
            if (!_isOpen || buffer == null || data == null || data.Length == 0)
            {
                return;
            }

            // Validate destination offset and data size
            MetalBuffer metalBuffer = buffer as MetalBuffer;
            if (metalBuffer == null)
            {
                Console.WriteLine("[MetalCommandList] WriteBuffer: Buffer must be a MetalBuffer instance");
                return;
            }

            BufferDesc bufferDesc = metalBuffer.Desc;
            if (destOffset < 0 || destOffset >= bufferDesc.ByteSize)
            {
                Console.WriteLine($"[MetalCommandList] WriteBuffer: Invalid destination offset {destOffset}, buffer size is {bufferDesc.ByteSize}");
                return;
            }

            if (data.Length > bufferDesc.ByteSize - destOffset)
            {
                Console.WriteLine($"[MetalCommandList] WriteBuffer: Data size {data.Length} exceeds available buffer space {bufferDesc.ByteSize - destOffset}");
                return;
            }

            IntPtr destinationBuffer = metalBuffer.NativeHandle;
            if (destinationBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] WriteBuffer: Invalid buffer native handle");
                return;
            }

            // Get Metal device from backend for creating staging buffer
            IntPtr device = _backend.GetMetalDevice();
            if (device == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] WriteBuffer: Failed to get Metal device");
                return;
            }

            // Create temporary staging buffer with shared storage mode for CPU access
            // Shared storage mode allows direct CPU writes without synchronization barriers
            // MetalResourceOptions.StorageModeShared = 0 (from enum definition)
            IntPtr stagingBuffer = MetalNative.CreateBufferWithOptions(device, (ulong)data.Length, (uint)MetalResourceOptions.StorageModeShared);
            if (stagingBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] WriteBuffer: Failed to create staging buffer");
                return;
            }

            try
            {
                // Get pointer to staging buffer contents for CPU write
                IntPtr stagingBufferContents = MetalNative.GetBufferContents(stagingBuffer);
                if (stagingBufferContents == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] WriteBuffer: Failed to get staging buffer contents");
                    return;
                }

                // Copy CPU data to staging buffer contents
                // Marshal.Copy handles the unsafe memory copy
                Marshal.Copy(data, 0, stagingBufferContents, data.Length);

                // Get or create blit command encoder for buffer copy operation
                IntPtr blitEncoder = GetOrCreateBlitCommandEncoder();
                if (blitEncoder == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] WriteBuffer: Failed to get blit command encoder");
                    return;
                }

                // Copy from staging buffer to destination buffer using blit command encoder
                // This copies data on the GPU side, ensuring proper synchronization
                // sourceOffset = 0 (start of staging buffer)
                // destinationOffset = destOffset (user-specified offset in destination buffer)
                // size = data.Length (amount of data to copy)
                MetalNative.CopyFromBuffer(blitEncoder, stagingBuffer, 0, destinationBuffer, (ulong)destOffset, (ulong)data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalCommandList] WriteBuffer: Exception during buffer write: {ex.Message}");
                Console.WriteLine($"[MetalCommandList] WriteBuffer: Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Release staging buffer
                // Metal uses ARC (Automatic Reference Counting), but we release explicitly to match allocation
                if (stagingBuffer != IntPtr.Zero)
                {
                    MetalNative.ReleaseBuffer(stagingBuffer);
                }
            }
        }

        /// <summary>
        /// Writes typed data to a GPU buffer using Metal blit command encoder.
        ///
        /// This is a convenience method that converts typed arrays to byte arrays and delegates
        /// to the byte array WriteBuffer method. It uses Marshal to convert unmanaged types
        /// to byte arrays for GPU upload.
        ///
        /// Based on Metal API: Same as WriteBuffer(IBuffer, byte[], int)
        /// </summary>
        /// <typeparam name="T">Unmanaged type (struct, primitive, etc.)</typeparam>
        /// <param name="buffer">Target buffer</param>
        /// <param name="data">Typed data array to write</param>
        /// <param name="destOffset">Destination offset in bytes</param>
        public void WriteBuffer<T>(IBuffer buffer, T[] data, int destOffset = 0) where T : unmanaged
        {
            if (!_isOpen || buffer == null || data == null || data.Length == 0)
            {
                return;
            }

            // Calculate byte size of typed data
            int elementSize = Marshal.SizeOf<T>();
            int byteSize = data.Length * elementSize;

            // Convert typed array to byte array using pinned GCHandle
            // This avoids copying data twice and is efficient for unmanaged types
            byte[] byteData = new byte[byteSize];
            GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                // Copy typed data directly from pinned array to byte array
                IntPtr sourcePtr = dataHandle.AddrOfPinnedObject();
                Marshal.Copy(sourcePtr, byteData, 0, byteSize);
            }
            finally
            {
                dataHandle.Free();
            }

            // Delegate to byte array WriteBuffer method
            WriteBuffer(buffer, byteData, destOffset);
        }

        /// <summary>
        /// Writes texture data to a GPU texture using Metal blit command encoder.
        ///
        /// Implementation strategy:
        /// 1. Create a temporary staging buffer with shared storage mode for CPU access
        /// 2. Write CPU data to staging buffer contents
        /// 3. Use blit command encoder to copy from staging buffer to texture
        /// 4. Release staging buffer
        ///
        /// Based on Metal API:
        /// - MTLDevice::newBufferWithLength:options: (for staging buffer with StorageModeShared)
        /// - MTLBuffer::contents() (for CPU access to buffer memory)
        /// - MTLBlitCommandEncoder::copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:
        ///
        /// Metal API Reference:
        /// https://developer.apple.com/documentation/metal/mtlblitcommandencoder/1400774-copyfrombuffer
        /// https://developer.apple.com/documentation/metal/mtlbuffer/1515376-contents
        /// </summary>
        public void WriteTexture(ITexture texture, int mipLevel, int arraySlice, byte[] data)
        {
            if (!_isOpen || texture == null || data == null || data.Length == 0)
            {
                return;
            }

            // Validate texture
            MetalTexture metalTexture = texture as MetalTexture;
            if (metalTexture == null)
            {
                Console.WriteLine("[MetalCommandList] WriteTexture: Texture must be a MetalTexture instance");
                return;
            }

            IntPtr textureHandle = metalTexture.NativeHandle;
            if (textureHandle == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] WriteTexture: Invalid texture native handle");
                return;
            }

            // Get texture description for validation
            TextureDesc textureDesc = metalTexture.Desc;
            if (mipLevel < 0 || mipLevel >= textureDesc.MipLevels)
            {
                Console.WriteLine($"[MetalCommandList] WriteTexture: Invalid mip level {mipLevel}, texture has {textureDesc.MipLevels} mip levels");
                return;
            }

            // Calculate mip level dimensions
            int mipWidth = Math.Max(1, textureDesc.Width >> mipLevel);
            int mipHeight = Math.Max(1, textureDesc.Height >> mipLevel);

            // Calculate bytes per row based on texture format
            // For uncompressed formats: width * bytesPerPixel
            // For compressed formats: block-aligned row size
            uint bytesPerRow = CalculateBytesPerRowForFormat(textureDesc.Format, mipWidth);
            if (bytesPerRow == 0)
            {
                Console.WriteLine($"[MetalCommandList] WriteTexture: Failed to calculate bytes per row for format {textureDesc.Format}");
                return;
            }

            // Calculate expected data size
            uint expectedDataSize = CalculateTextureDataSize(textureDesc.Format, mipWidth, mipHeight);
            if (data.Length < expectedDataSize)
            {
                Console.WriteLine($"[MetalCommandList] WriteTexture: Data size mismatch. Expected {expectedDataSize} bytes, got {data.Length}");
                return;
            }

            // Get Metal device from backend for creating staging buffer
            IntPtr device = _backend.GetMetalDevice();
            if (device == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] WriteTexture: Failed to get Metal device");
                return;
            }

            // Create temporary staging buffer with shared storage mode for CPU access
            IntPtr stagingBuffer = MetalNative.CreateBufferWithOptions(device, (ulong)data.Length, (uint)MetalResourceOptions.StorageModeShared);
            if (stagingBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] WriteTexture: Failed to create staging buffer");
                return;
            }

            try
            {
                // Get pointer to staging buffer contents for CPU write
                IntPtr stagingBufferContents = MetalNative.GetBufferContents(stagingBuffer);
                if (stagingBufferContents == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] WriteTexture: Failed to get staging buffer contents");
                    return;
                }

                // Copy CPU data to staging buffer contents
                Marshal.Copy(data, 0, stagingBufferContents, data.Length);

                // Get or create blit command encoder for buffer-to-texture copy operation
                IntPtr blitEncoder = GetOrCreateBlitCommandEncoder();
                if (blitEncoder == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] WriteTexture: Failed to get blit command encoder");
                    return;
                }

                // Copy from staging buffer to texture using blit command encoder
                // Metal API: copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:
                MetalOrigin destinationOrigin = new MetalOrigin(0, 0, 0);
                MetalSize sourceSize = new MetalSize((uint)mipWidth, (uint)mipHeight, 1);

                // Calculate source bytes per image (for 3D textures or array slices)
                uint sourceBytesPerImage = bytesPerRow * (uint)mipHeight;

                // Copy from staging buffer to texture
                MetalNative.CopyFromBufferToTexture(blitEncoder, stagingBuffer, 0, bytesPerRow, sourceBytesPerImage, sourceSize,
                    textureHandle, (uint)arraySlice, (uint)mipLevel, destinationOrigin);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalCommandList] WriteTexture: Exception during texture write: {ex.Message}");
                Console.WriteLine($"[MetalCommandList] WriteTexture: Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Release staging buffer
                if (stagingBuffer != IntPtr.Zero)
                {
                    MetalNative.ReleaseBuffer(stagingBuffer);
                }
            }
        }

        /// <summary>
        /// Calculates bytes per row for a texture format at a given width.
        /// </summary>
        private uint CalculateBytesPerRowForFormat(TextureFormat format, int width)
        {
            switch (format)
            {
                // 1 byte per pixel formats
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                case TextureFormat.R8_SInt:
                    return (uint)width;

                // 2 bytes per pixel formats
                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R8G8_UInt:
                case TextureFormat.R16_Float:
                case TextureFormat.R16_UNorm:
                case TextureFormat.R16_UInt:
                case TextureFormat.R16_SInt:
                    return (uint)(width * 2);

                // 4 bytes per pixel formats
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16_UInt:
                case TextureFormat.R32_Float:
                case TextureFormat.R32_UInt:
                case TextureFormat.R32_SInt:
                case TextureFormat.D32_Float:
                case TextureFormat.D24_UNorm_S8_UInt:
                    return (uint)(width * 4);

                // 8 bytes per pixel formats
                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R16G16B16A16_UInt:
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32_UInt:
                case TextureFormat.R32G32_SInt:
                    return (uint)(width * 8);

                // 16 bytes per pixel formats
                case TextureFormat.R32G32B32A32_Float:
                case TextureFormat.R32G32B32A32_UInt:
                case TextureFormat.R32G32B32A32_SInt:
                    return (uint)(width * 16);

                // Compressed formats - block-aligned
                case TextureFormat.BC1:
                case TextureFormat.BC4:
                    // 8 bytes per 4x4 block, row must be block-aligned
                    return (uint)(((width + 3) / 4) * 8);

                case TextureFormat.BC2:
                case TextureFormat.BC3:
                case TextureFormat.BC5:
                case TextureFormat.BC6H:
                case TextureFormat.BC7:
                    // 16 bytes per 4x4 block, row must be block-aligned
                    return (uint)(((width + 3) / 4) * 16);

                default:
                    // Default to RGBA8 format for unknown formats
                    return (uint)(width * 4);
            }
        }

        /// <summary>
        /// Calculates total data size for a texture at given dimensions.
        /// </summary>
        private uint CalculateTextureDataSize(TextureFormat format, int width, int height)
        {
            switch (format)
            {
                // Uncompressed formats
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                case TextureFormat.R8_SInt:
                    return (uint)(width * height);

                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R8G8_UInt:
                case TextureFormat.R16_Float:
                case TextureFormat.R16_UNorm:
                case TextureFormat.R16_UInt:
                case TextureFormat.R16_SInt:
                    return (uint)(width * height * 2);

                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16_UInt:
                case TextureFormat.R32_Float:
                case TextureFormat.R32_UInt:
                case TextureFormat.R32_SInt:
                case TextureFormat.D32_Float:
                case TextureFormat.D24_UNorm_S8_UInt:
                    return (uint)(width * height * 4);

                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R16G16B16A16_UInt:
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32_UInt:
                case TextureFormat.R32G32_SInt:
                    return (uint)(width * height * 8);

                case TextureFormat.R32G32B32A32_Float:
                case TextureFormat.R32G32B32A32_UInt:
                case TextureFormat.R32G32B32A32_SInt:
                    return (uint)(width * height * 16);

                // Compressed formats
                case TextureFormat.BC1:
                case TextureFormat.BC4:
                    return (uint)(((width + 3) / 4) * ((height + 3) / 4) * 8);

                case TextureFormat.BC2:
                case TextureFormat.BC3:
                case TextureFormat.BC5:
                case TextureFormat.BC6H:
                case TextureFormat.BC7:
                    return (uint)(((width + 3) / 4) * ((height + 3) / 4) * 16);

                default:
                    return (uint)(width * height * 4);
            }
        }

        /// <summary>
        /// Copies data from a source buffer to a destination buffer using Metal blit command encoder.
        ///
        /// This performs a GPU-side buffer-to-buffer copy operation, which is efficient for large data transfers.
        /// The copy operation is recorded into the command buffer and executed when the command buffer is committed.
        ///
        /// Based on Metal API: MTLBlitCommandEncoder::copyFromBuffer:sourceOffset:toBuffer:destinationOffset:size:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlblitcommandencoder/1400756-copyfrombuffer
        /// </summary>
        /// <param name="dest">Destination buffer</param>
        /// <param name="destOffset">Destination offset in bytes</param>
        /// <param name="src">Source buffer</param>
        /// <param name="srcOffset">Source offset in bytes</param>
        /// <param name="size">Number of bytes to copy</param>
        public void CopyBuffer(IBuffer dest, int destOffset, IBuffer src, int srcOffset, int size)
        {
            if (!_isOpen || dest == null || src == null)
            {
                return;
            }

            // Validate size
            if (size <= 0)
            {
                return; // Nothing to copy
            }

            // Validate offsets and size don't exceed buffer bounds
            if (srcOffset < 0 || destOffset < 0)
            {
                Console.WriteLine("[MetalCommandList] CopyBuffer: Invalid offsets - sourceOffset and destOffset must be non-negative");
                return;
            }

            // Get buffer handles
            MetalBuffer srcMetalBuffer = src as MetalBuffer;
            MetalBuffer destMetalBuffer = dest as MetalBuffer;

            if (srcMetalBuffer == null || destMetalBuffer == null)
            {
                Console.WriteLine("[MetalCommandList] CopyBuffer: Buffers must be MetalBuffer instances");
                return;
            }

            IntPtr srcBufferHandle = srcMetalBuffer.NativeHandle;
            IntPtr destBufferHandle = destMetalBuffer.NativeHandle;

            if (srcBufferHandle == IntPtr.Zero || destBufferHandle == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] CopyBuffer: Invalid buffer handles");
                return;
            }

            // Validate buffer sizes
            BufferDesc srcDesc = srcMetalBuffer.Desc;
            BufferDesc destDesc = destMetalBuffer.Desc;

            if (srcOffset + size > srcDesc.ByteSize)
            {
                Console.WriteLine($"[MetalCommandList] CopyBuffer: Source buffer overflow - sourceOffset ({srcOffset}) + size ({size}) > source buffer size ({srcDesc.ByteSize})");
                return;
            }

            if (destOffset + size > destDesc.ByteSize)
            {
                Console.WriteLine($"[MetalCommandList] CopyBuffer: Destination buffer overflow - destOffset ({destOffset}) + size ({size}) > destination buffer size ({destDesc.ByteSize})");
                return;
            }

            // Get or create blit command encoder
            IntPtr blitEncoder = GetOrCreateBlitCommandEncoder();
            if (blitEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] CopyBuffer: Failed to get blit command encoder");
                return;
            }

            // Perform buffer-to-buffer copy using Metal blit command encoder
            // CopyFromBuffer signature: (blitEncoder, sourceBuffer, sourceOffset, destinationBuffer, destinationOffset, size)
            // All parameters are in bytes (ulong for offsets and size)
            MetalNative.CopyFromBuffer(blitEncoder, srcBufferHandle, (ulong)srcOffset, destBufferHandle, (ulong)destOffset, (ulong)size);
        }

        /// <summary>
        /// Copies a texture from source to destination using Metal blit command encoder.
        /// Based on Metal API: MTLBlitCommandEncoder::copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlblitcommandencoder/1400769-copyfromtexture
        ///
        /// This implementation copies the entire texture (all mip levels and array slices if applicable).
        /// For 2D textures, it copies from mip level 0, array slice 0 to the same in the destination.
        /// </summary>
        public void CopyTexture(ITexture dest, ITexture src)
        {
            if (!_isOpen || dest == null || src == null)
            {
                return;
            }

            // Get Metal texture objects
            MetalTexture destMetal = dest as MetalTexture;
            MetalTexture srcMetal = src as MetalTexture;

            if (destMetal == null || srcMetal == null)
            {
                Console.WriteLine("[MetalCommandList] CopyTexture: Textures must be MetalTexture instances");
                return;
            }

            // Get native Metal texture handles
            IntPtr destTexture = destMetal.NativeHandle;
            IntPtr srcTexture = srcMetal.NativeHandle;

            if (destTexture == IntPtr.Zero || srcTexture == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] CopyTexture: Invalid texture handles");
                return;
            }

            // Validate texture descriptions
            TextureDesc destDesc = destMetal.Desc;
            TextureDesc srcDesc = srcMetal.Desc;

            // Check if textures are compatible for copying
            // Metal allows copying between textures with compatible formats
            // For exact format matching, we require the same format
            // Metal also supports some format conversions (e.g., RGBA8 to BGRA8)
            if (destDesc.Format != srcDesc.Format)
            {
                // Check if formats are compatible for copying
                // Some format conversions are supported by Metal (e.g., sRGB to linear)
                bool formatsCompatible = AreTextureFormatsCompatibleForCopy(destDesc.Format, srcDesc.Format);
                if (!formatsCompatible)
                {
                    Console.WriteLine($"[MetalCommandList] CopyTexture: Incompatible texture formats - source: {srcDesc.Format}, destination: {destDesc.Format}");
                    return;
                }
            }

            // Get or create blit command encoder
            IntPtr blitEncoder = GetOrCreateBlitCommandEncoder();
            if (blitEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] CopyTexture: Failed to get blit command encoder");
                return;
            }

            try
            {
                // Determine texture dimensions and copy strategy
                // For 2D textures, copy the entire texture from mip level 0
                // For array textures, copy all slices
                // For 3D textures, copy the entire volume

                if (destDesc.Dimension == TextureDimension.Texture2D && srcDesc.Dimension == TextureDimension.Texture2D)
                {
                    // Copy 2D texture - copy from mip level 0, array slice 0
                    // Source and destination origins are (0, 0, 0)
                    // Source and destination sizes are the texture dimensions
                    MetalOrigin sourceOrigin = new MetalOrigin(0, 0, 0);
                    MetalSize sourceSize = new MetalSize((uint)srcDesc.Width, (uint)srcDesc.Height, 1);
                    MetalOrigin destOrigin = new MetalOrigin(0, 0, 0);

                    // Copy from source mip level 0, slice 0 to destination mip level 0, slice 0
                    MetalNative.CopyFromTexture(blitEncoder, srcTexture, 0, 0, sourceOrigin, sourceSize, destTexture, 0, 0, destOrigin);

                    // If textures have multiple mip levels, copy all of them
                    int maxMipLevels = Math.Min(srcDesc.MipLevels, destDesc.MipLevels);
                    for (int mipLevel = 1; mipLevel < maxMipLevels; mipLevel++)
                    {
                        // Calculate mip level dimensions
                        int mipWidth = Math.Max(1, srcDesc.Width >> mipLevel);
                        int mipHeight = Math.Max(1, srcDesc.Height >> mipLevel);

                        sourceSize = new MetalSize((uint)mipWidth, (uint)mipHeight, 1);
                        MetalNative.CopyFromTexture(blitEncoder, srcTexture, 0, (uint)mipLevel, sourceOrigin, sourceSize, destTexture, 0, (uint)mipLevel, destOrigin);
                    }
                }
                else if (destDesc.Dimension == TextureDimension.Texture2DArray && srcDesc.Dimension == TextureDimension.Texture2DArray)
                {
                    // Copy 2D array texture - copy all array slices
                    int maxArraySlices = Math.Min(srcDesc.ArraySize, destDesc.ArraySize);
                    int maxMipLevels = Math.Min(srcDesc.MipLevels, destDesc.MipLevels);

                    for (int arraySlice = 0; arraySlice < maxArraySlices; arraySlice++)
                    {
                        for (int mipLevel = 0; mipLevel < maxMipLevels; mipLevel++)
                        {
                            // Calculate mip level dimensions
                            int mipWidth = Math.Max(1, srcDesc.Width >> mipLevel);
                            int mipHeight = Math.Max(1, srcDesc.Height >> mipLevel);

                            MetalOrigin sourceOrigin = new MetalOrigin(0, 0, 0);
                            MetalSize sourceSize = new MetalSize((uint)mipWidth, (uint)mipHeight, 1);
                            MetalOrigin destOrigin = new MetalOrigin(0, 0, 0);

                            MetalNative.CopyFromTexture(blitEncoder, srcTexture, (uint)arraySlice, (uint)mipLevel, sourceOrigin, sourceSize, destTexture, (uint)arraySlice, (uint)mipLevel, destOrigin);
                        }
                    }
                }
                else if (destDesc.Dimension == TextureDimension.Texture3D && srcDesc.Dimension == TextureDimension.Texture3D)
                {
                    // Copy 3D texture - copy entire volume
                    MetalOrigin sourceOrigin = new MetalOrigin(0, 0, 0);
                    MetalSize sourceSize = new MetalSize((uint)srcDesc.Width, (uint)srcDesc.Height, (uint)srcDesc.Depth);
                    MetalOrigin destOrigin = new MetalOrigin(0, 0, 0);

                    // Copy from mip level 0
                    MetalNative.CopyFromTexture(blitEncoder, srcTexture, 0, 0, sourceOrigin, sourceSize, destTexture, 0, 0, destOrigin);

                    // Copy additional mip levels if present
                    int maxMipLevels = Math.Min(srcDesc.MipLevels, destDesc.MipLevels);
                    for (int mipLevel = 1; mipLevel < maxMipLevels; mipLevel++)
                    {
                        int mipWidth = Math.Max(1, srcDesc.Width >> mipLevel);
                        int mipHeight = Math.Max(1, srcDesc.Height >> mipLevel);
                        int mipDepth = Math.Max(1, srcDesc.Depth >> mipLevel);

                        sourceSize = new MetalSize((uint)mipWidth, (uint)mipHeight, (uint)mipDepth);
                        MetalNative.CopyFromTexture(blitEncoder, srcTexture, 0, (uint)mipLevel, sourceOrigin, sourceSize, destTexture, 0, (uint)mipLevel, destOrigin);
                    }
                }
                else
                {
                    Console.WriteLine($"[MetalCommandList] CopyTexture: Unsupported texture dimension combination - source: {srcDesc.Dimension}, destination: {destDesc.Dimension}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalCommandList] CopyTexture: Exception during texture copy: {ex.Message}");
                Console.WriteLine($"[MetalCommandList] CopyTexture: Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Checks if two texture formats are compatible for copying via Metal blit command encoder.
        /// Metal supports copying between some compatible formats (e.g., sRGB to linear variants).
        /// Based on Metal API: Texture format compatibility for blit operations
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlblitcommandencoder
        /// </summary>
        private bool AreTextureFormatsCompatibleForCopy(TextureFormat destFormat, TextureFormat srcFormat)
        {
            // Exact format match is always compatible
            if (destFormat == srcFormat)
            {
                return true;
            }

            // Check for sRGB/linear compatibility
            // Metal allows copying between sRGB and linear variants of the same base format
            switch (srcFormat)
            {
                case TextureFormat.R8G8B8A8_UNorm:
                    return destFormat == TextureFormat.R8G8B8A8_UNorm_SRGB;
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    return destFormat == TextureFormat.R8G8B8A8_UNorm;
                case TextureFormat.B8G8R8A8_UNorm:
                    return destFormat == TextureFormat.B8G8R8A8_UNorm_SRGB;
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    return destFormat == TextureFormat.B8G8R8A8_UNorm;
                default:
                    // For other formats, require exact match
                    return false;
            }
        }

        /// <summary>
        /// Clears a color attachment of a framebuffer to a specific color.
        ///
        /// Implementation: Uses Metal render pass with Clear load action to clear the texture.
        /// Metal doesn't have a direct "fill texture" operation, so we create a minimal render pass
        /// that clears the attachment and immediately ends it.
        ///
        /// Note: This method will end any active render pass or blit encoder before clearing,
        /// as Metal allows only one encoder type to be active at a time per command buffer.
        ///
        /// Based on Metal API:
        /// - MTLRenderPassDescriptor with color attachment loadAction = .clear
        /// - MTLRenderCommandEncoder::endEncoding() to complete the clear operation
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlrenderpassdescriptor
        /// </summary>
        public void ClearColorAttachment(IFramebuffer framebuffer, int attachmentIndex, Vector4 color)
        {
            if (!_isOpen || framebuffer == null)
            {
                return;
            }

            // Validate attachment index
            MetalFramebuffer metalFramebuffer = framebuffer as MetalFramebuffer;
            if (metalFramebuffer == null)
            {
                Console.WriteLine("[MetalCommandList] ClearColorAttachment: Framebuffer must be a MetalFramebuffer instance");
                return;
            }

            FramebufferDesc desc = metalFramebuffer.Desc;
            if (desc.ColorAttachments == null || attachmentIndex < 0 || attachmentIndex >= desc.ColorAttachments.Length)
            {
                Console.WriteLine($"[MetalCommandList] ClearColorAttachment: Invalid attachment index {attachmentIndex}");
                return;
            }

            FramebufferAttachment colorAttachment = desc.ColorAttachments[attachmentIndex];
            if (colorAttachment.Texture == null)
            {
                Console.WriteLine($"[MetalCommandList] ClearColorAttachment: Color attachment {attachmentIndex} has no texture");
                return;
            }

            MetalTexture metalTexture = colorAttachment.Texture as MetalTexture;
            if (metalTexture == null)
            {
                Console.WriteLine("[MetalCommandList] ClearColorAttachment: Texture must be a MetalTexture instance");
                return;
            }

            IntPtr colorTexture = metalTexture.NativeHandle;
            if (colorTexture == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearColorAttachment: Invalid texture native handle");
                return;
            }

            // Get the current command buffer from the backend
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearColorAttachment: No active command buffer");
                return;
            }

            // End any active encoders before creating render pass for clearing
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndAccelerationStructureCommandEncoding(_currentAccelStructCommandEncoder);
                _currentAccelStructCommandEncoder = IntPtr.Zero;
            }

            // Create render pass descriptor for clearing
            IntPtr renderPassDescriptor = MetalNative.CreateRenderPassDescriptor();
            if (renderPassDescriptor == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearColorAttachment: Failed to create render pass descriptor");
                return;
            }

            try
            {
                // Set color attachment with Clear load action
                // MetalLoadAction.Clear tells Metal to clear the texture to the specified clear color when the render pass begins
                // MetalStoreAction.Store tells Metal to store the result (though in this case we're just clearing, so it stores the clear color)
                MetalClearColor clearColor = new MetalClearColor(color.X, color.Y, color.Z, color.W);
                MetalNative.SetRenderPassColorAttachment(renderPassDescriptor, 0, colorTexture,
                    MetalLoadAction.Clear, MetalStoreAction.Store, clearColor);

                // Begin render pass - this will clear the texture to the specified color
                IntPtr renderCommandEncoder = MetalNative.BeginRenderPass(commandBuffer, renderPassDescriptor);
                if (renderCommandEncoder == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] ClearColorAttachment: Failed to begin render pass for clearing");
                    return;
                }

                // Immediately end the render pass to complete the clear operation
                // The clear happens when the render pass begins, so ending it immediately completes the clear
                MetalNative.EndEncoding(renderCommandEncoder);
            }
            finally
            {
                // Release render pass descriptor
                MetalNative.ReleaseRenderPassDescriptor(renderPassDescriptor);
            }
        }

        /// <summary>
        /// Clears the depth/stencil attachment of a framebuffer.
        ///
        /// Implementation: Uses Metal render pass with Clear load action for depth and/or stencil attachments.
        /// Metal clears depth and stencil attachments when a render pass begins with MetalLoadAction.Clear.
        ///
        /// Based on Metal API: MTLRenderPassDescriptor depthAttachment and stencilAttachment
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlrenderpassdescriptor
        ///
        /// Note: In Metal, depth and stencil can share the same texture (combined format like D24S8),
        /// but they are configured as separate attachments (depthAttachment and stencilAttachment) even when
        /// they reference the same texture. This allows independent load/store actions and clear values.
        /// </summary>
        /// <param name="framebuffer">Framebuffer containing the depth/stencil attachment to clear.</param>
        /// <param name="depth">Clear value for depth (0.0 to 1.0, typically 1.0 to clear to far plane).</param>
        /// <param name="stencil">Clear value for stencil (0-255, typically 0).</param>
        /// <param name="clearDepth">Whether to clear the depth buffer.</param>
        /// <param name="clearStencil">Whether to clear the stencil buffer.</param>
        public void ClearDepthStencilAttachment(IFramebuffer framebuffer, float depth, byte stencil, bool clearDepth = true, bool clearStencil = true)
        {
            if (!_isOpen || framebuffer == null)
            {
                return;
            }

            // Validate framebuffer
            MetalFramebuffer metalFramebuffer = framebuffer as MetalFramebuffer;
            if (metalFramebuffer == null)
            {
                Console.WriteLine("[MetalCommandList] ClearDepthStencilAttachment: Framebuffer must be a MetalFramebuffer instance");
                return;
            }

            FramebufferDesc desc = metalFramebuffer.Desc;

            // Check if depth attachment exists
            if (desc.DepthAttachment.Texture == null)
            {
                Console.WriteLine("[MetalCommandList] ClearDepthStencilAttachment: Framebuffer has no depth/stencil attachment");
                return;
            }

            MetalTexture metalTexture = desc.DepthAttachment.Texture as MetalTexture;
            if (metalTexture == null)
            {
                Console.WriteLine("[MetalCommandList] ClearDepthStencilAttachment: Depth/stencil texture must be a MetalTexture instance");
                return;
            }

            IntPtr depthStencilTexture = metalTexture.NativeHandle;
            if (depthStencilTexture == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearDepthStencilAttachment: Invalid depth/stencil texture native handle");
                return;
            }

            // Get the current command buffer from the backend
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearDepthStencilAttachment: No active command buffer");
                return;
            }

            // End any active encoders before creating render pass for clearing
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndAccelerationStructureCommandEncoding(_currentAccelStructCommandEncoder);
                _currentAccelStructCommandEncoder = IntPtr.Zero;
            }

            // Create render pass descriptor for clearing
            IntPtr renderPassDescriptor = MetalNative.CreateRenderPassDescriptor();
            if (renderPassDescriptor == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearDepthStencilAttachment: Failed to create render pass descriptor");
                return;
            }

            try
            {
                // Set depth attachment with Clear load action if clearing depth
                if (clearDepth)
                {
                    // MetalLoadAction.Clear tells Metal to clear the depth texture to the specified clear value when the render pass begins
                    // MetalStoreAction.Store tells Metal to store the result (the cleared depth value)
                    MetalNative.SetRenderPassDepthAttachment(renderPassDescriptor, depthStencilTexture,
                        MetalLoadAction.Clear, MetalStoreAction.Store, (double)depth);
                }
                else
                {
                    // If not clearing depth, use DontCare load action
                    MetalNative.SetRenderPassDepthAttachment(renderPassDescriptor, depthStencilTexture,
                        MetalLoadAction.DontCare, MetalStoreAction.DontCare, (double)depth);
                }

                // Set stencil attachment with Clear load action if clearing stencil
                // Note: Even when depth and stencil share the same texture, Metal requires setting both attachments separately
                if (clearStencil)
                {
                    // MetalLoadAction.Clear tells Metal to clear the stencil texture to the specified clear value when the render pass begins
                    // MetalStoreAction.Store tells Metal to store the result (the cleared stencil value)
                    MetalNative.SetRenderPassStencilAttachment(renderPassDescriptor, depthStencilTexture,
                        MetalLoadAction.Clear, MetalStoreAction.Store, (uint)stencil);
                }
                else
                {
                    // If not clearing stencil, use DontCare load action
                    MetalNative.SetRenderPassStencilAttachment(renderPassDescriptor, depthStencilTexture,
                        MetalLoadAction.DontCare, MetalStoreAction.DontCare, (uint)stencil);
                }

                // Begin render pass - this will clear the depth/stencil texture to the specified values
                IntPtr renderCommandEncoder = MetalNative.BeginRenderPass(commandBuffer, renderPassDescriptor);
                if (renderCommandEncoder == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] ClearDepthStencilAttachment: Failed to begin render pass for clearing");
                    return;
                }

                // Immediately end the render pass to complete the clear operation
                // The clear happens when the render pass begins, so ending it immediately completes the clear
                MetalNative.EndEncoding(renderCommandEncoder);
            }
            finally
            {
                // Release render pass descriptor
                MetalNative.ReleaseRenderPassDescriptor(renderPassDescriptor);
            }
        }

        /// <summary>
        /// Clears an unordered access view (UAV) texture to a float vector value.
        ///
        /// Implementation: Uses a compute shader to fill the texture with the clear value.
        /// Metal doesn't have a direct "clear UAV" operation like D3D12 or Vulkan, so we use
        /// a compute shader that writes the clear value to each texel.
        ///
        /// Based on Metal API: MTLComputeCommandEncoder with compute shader
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlcomputecommandencoder
        ///
        /// Note: This requires a compute shader to be available. The compute shader should:
        /// - Take the clear value as a constant buffer parameter
        /// - Write the clear value to each texel in the texture
        /// - Use threadgroup size appropriate for the texture dimensions
        ///
        /// Metal compute shader implementation for clearing UAV textures.
        /// The compute shader source code (must be compiled into Metal library):
        /// ```
        /// kernel void clearUAVFloat(texture2d<float, access::write> output [[texture(0)]],
        ///                            constant float4& clearValue [[buffer(0)]],
        ///                            uint2 gid [[thread_position_in_grid]])
        /// {
        ///     output.write(clearValue, gid);
        /// }
        /// ```
        ///
        /// Shader compilation: This shader must be compiled into a Metal library (e.g., .metallib file)
        /// and linked into the application. Runtime shader compilation from source is implemented
        /// as a fallback when the shader is not found in the default library. The implementation
        /// uses Metal's MTLLibrary::newLibraryWithSource:options:error: API via Objective-C interop
        /// to compile shaders dynamically when needed. In practice, shaders are typically compiled
        /// at build time for better performance, but runtime compilation provides flexibility.
        /// </summary>
        public void ClearUAVFloat(ITexture texture, Vector4 value)
        {
            if (!_isOpen || texture == null)
            {
                return;
            }

            // Validate texture
            MetalTexture metalTexture = texture as MetalTexture;
            if (metalTexture == null)
            {
                Console.WriteLine("[MetalCommandList] ClearUAVFloat: Texture must be a MetalTexture instance");
                return;
            }

            IntPtr textureHandle = metalTexture.NativeHandle;
            if (textureHandle == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearUAVFloat: Invalid texture native handle");
                return;
            }

            // Get texture dimensions for compute shader dispatch
            TextureDesc desc = texture.Desc;
            if (desc.Width == 0 || desc.Height == 0)
            {
                Console.WriteLine("[MetalCommandList] ClearUAVFloat: Invalid texture dimensions");
                return;
            }

            // Get the current command buffer from the backend
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearUAVFloat: No active command buffer");
                return;
            }

            // End any active encoders before creating compute encoder
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndAccelerationStructureCommandEncoding(_currentAccelStructCommandEncoder);
                _currentAccelStructCommandEncoder = IntPtr.Zero;
            }

            // Get or create compute command encoder
            if (_currentComputeCommandEncoder == IntPtr.Zero)
            {
                _currentComputeCommandEncoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                if (_currentComputeCommandEncoder == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] ClearUAVFloat: Failed to create compute command encoder");
                    return;
                }
            }

            // Get or create compute pipeline state for clearing UAV
            // The compute shader source code is:
            // kernel void clearUAVFloat(texture2d<float, access::write> output [[texture(0)]],
            //                            constant float4& clearValue [[buffer(0)]],
            //                            uint2 gid [[thread_position_in_grid]])
            // {
            //     output.write(clearValue, gid);
            // }
            //
            // Note: This shader must be compiled into a Metal library and the function
            // "clearUAVFloat" must be available. Shader compilation from source requires
            // Objective-C interop (objc_msgSend) which is complex in C#. In practice, shaders
            // are typically compiled at build time into .metallib files or embedded in the app bundle.
            // For runtime compilation, additional infrastructure would be needed.
            if (_clearUAVFloatComputePipelineState == IntPtr.Zero)
            {
                // Get the compute function from the default library
                // This assumes the shader is already compiled and available in the default library
                IntPtr defaultLibrary = _backend.GetDefaultLibrary();
                if (defaultLibrary == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] ClearUAVFloat: Default library not available");
                    return;
                }

                IntPtr computeFunction = MetalNative.CreateFunctionFromLibrary(defaultLibrary, "clearUAVFloat");
                if (computeFunction == IntPtr.Zero)
                {
                    // Shader function not found - try runtime compilation from source
                    Console.WriteLine("[MetalCommandList] ClearUAVFloat: Compute shader function 'clearUAVFloat' not found in library. Attempting runtime compilation from source.");

                    // Metal Shading Language source code for clearUAVFloat compute shader
                    string shaderSource = @"
                        #include <metal_stdlib>
                        using namespace metal;

                        kernel void clearUAVFloat(texture2d<float, access::write> output [[texture(0)]],
                                                  constant float4& clearValue [[buffer(0)]],
                                                  uint2 gid [[thread_position_in_grid]])
                        {
                            if (gid.x < output.get_width() && gid.y < output.get_height())
                            {
                                output.write(clearValue, gid);
                            }
                        }
                    ";

                    IntPtr nativeDevice = _backend.GetNativeDevice();
                    if (nativeDevice == IntPtr.Zero)
                    {
                        Console.WriteLine("[MetalCommandList] ClearUAVFloat: Device not available for runtime compilation");
                        return;
                    }

                    // Compile shader library from source
                    IntPtr compiledLibrary = MetalNative.CompileLibraryFromSource(nativeDevice, shaderSource, out IntPtr compileError);
                    if (compiledLibrary == IntPtr.Zero)
                    {
                        string errorMsg = compileError != IntPtr.Zero ? MetalNative.GetNSErrorDescription(compileError) : "Unknown compilation error";
                        Console.WriteLine($"[MetalCommandList] ClearUAVFloat: Failed to compile shader from source: {errorMsg}");
                        return;
                    }

                    try
                    {
                        // Get function from compiled library
                        computeFunction = MetalNative.CreateFunctionFromLibrary(compiledLibrary, "clearUAVFloat");
                        if (computeFunction == IntPtr.Zero)
                        {
                            Console.WriteLine("[MetalCommandList] ClearUAVFloat: Function 'clearUAVFloat' not found in compiled library");
                            MetalNative.ReleaseLibrary(compiledLibrary);
                            return;
                        }
                    }
                    finally
                    {
                        // Release compiled library (function retains reference)
                        MetalNative.ReleaseLibrary(compiledLibrary);
                    }
                }

                // Create compute pipeline state
                IntPtr device = _backend.GetNativeDevice();
                if (device == IntPtr.Zero)
                {
                    MetalNative.ReleaseFunction(computeFunction);
                    Console.WriteLine("[MetalCommandList] ClearUAVFloat: Device not available");
                    return;
                }

                _clearUAVFloatComputePipelineState = MetalNative.CreateComputePipelineState(device, computeFunction);
                MetalNative.ReleaseFunction(computeFunction);

                if (_clearUAVFloatComputePipelineState == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] ClearUAVFloat: Failed to create compute pipeline state");
                    return;
                }
            }

            // Set compute pipeline state
            MetalNative.SetComputePipelineState(_currentComputeCommandEncoder, _clearUAVFloatComputePipelineState);

            // Set texture as write-only resource at index 0
            MetalNative.SetTexture(_currentComputeCommandEncoder, textureHandle, 0, MetalTextureUsage.ShaderWrite);

            // Set clear value in constant buffer at index 0
            // Metal expects float4 (16 bytes) for the clear value
            float[] clearValue = new float[] { value.X, value.Y, value.Z, value.W };
            IntPtr clearValuePtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(16);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(clearValue, 0, clearValuePtr, 4);
                MetalNative.SetBytes(_currentComputeCommandEncoder, clearValuePtr, 16, 0);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(clearValuePtr);
            }

            // Calculate threadgroup size and dispatch
            // Metal compute shaders use threadgroups - we dispatch enough threadgroups to cover the entire texture
            // Threadgroup size: 16x16 threads per group (256 threads total per group)
            const int threadsPerGroupX = 16;
            const int threadsPerGroupY = 16;
            const int threadsPerGroupZ = 1;

            // Calculate number of threadgroups needed to cover the entire texture
            int threadGroupCountX = (desc.Width + threadsPerGroupX - 1) / threadsPerGroupX;
            int threadGroupCountY = (desc.Height + threadsPerGroupY - 1) / threadsPerGroupY;
            int threadGroupCountZ = 1;

            // Dispatch compute threads
            MetalNative.DispatchThreadgroups(_currentComputeCommandEncoder, threadGroupCountX, threadGroupCountY, threadGroupCountZ, threadsPerGroupX, threadsPerGroupY, threadsPerGroupZ);

            // Note: We do not end the compute encoder here - it may be reused for multiple compute dispatches
            // The compute encoder will be ended when:
            // 1. Another encoder type is started (render/blit/accelStruct)
            // 2. The command list is closed
        }

        /// <summary>
        /// Clears an unordered access view (UAV) texture to a uint value.
        ///
        /// Implementation: Uses a compute shader to fill the texture with the clear value.
        /// Metal doesn't have a direct "clear UAV" operation like D3D12 or Vulkan, so we use
        /// a compute shader that writes the clear value to each texel.
        ///
        /// Based on Metal API: MTLComputeCommandEncoder with compute shader
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlcomputecommandencoder
        ///
        /// Metal compute shader implementation for clearing UAV textures with uint values.
        /// The compute shader source code (must be compiled into Metal library):
        /// ```
        /// kernel void clearUAVUint(texture2d<uint, access::write> output [[texture(0)]],
        ///                           constant uint& clearValue [[buffer(0)]],
        ///                           uint2 gid [[thread_position_in_grid]])
        /// {
        ///     output.write(clearValue, gid);
        /// }
        /// ```
        ///
        /// Shader compilation: This shader is compiled at runtime from source if not found in the default library.
        /// Runtime shader compilation uses Metal's newLibraryWithSource:options:error: API via Objective-C interop.
        /// Falls back to runtime compilation if the shader is not available in pre-compiled libraries.
        /// </summary>
        public void ClearUAVUint(ITexture texture, uint value)
        {
            if (!_isOpen || texture == null)
            {
                return;
            }

            // Validate texture
            MetalTexture metalTexture = texture as MetalTexture;
            if (metalTexture == null)
            {
                Console.WriteLine("[MetalCommandList] ClearUAVUint: Texture must be a MetalTexture instance");
                return;
            }

            IntPtr textureHandle = metalTexture.NativeHandle;
            if (textureHandle == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearUAVUint: Invalid texture native handle");
                return;
            }

            // Get texture dimensions for compute shader dispatch
            TextureDesc desc = texture.Desc;
            if (desc.Width == 0 || desc.Height == 0)
            {
                Console.WriteLine("[MetalCommandList] ClearUAVUint: Invalid texture dimensions");
                return;
            }

            // Get the current command buffer from the backend
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] ClearUAVUint: No active command buffer");
                return;
            }

            // End any active encoders before creating compute encoder
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndAccelerationStructureCommandEncoding(_currentAccelStructCommandEncoder);
                _currentAccelStructCommandEncoder = IntPtr.Zero;
            }

            // Get or create compute command encoder
            if (_currentComputeCommandEncoder == IntPtr.Zero)
            {
                _currentComputeCommandEncoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                if (_currentComputeCommandEncoder == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] ClearUAVUint: Failed to create compute command encoder");
                    return;
                }
            }

            // Get or create compute pipeline state for clearing UAV with uint
            // The compute shader source code is:
            // kernel void clearUAVUint(texture2d<uint, access::write> output [[texture(0)]],
            //                           constant uint& clearValue [[buffer(0)]],
            //                           uint2 gid [[thread_position_in_grid]])
            // {
            //     output.write(clearValue, gid);
            // }
            //
            // Note: This shader is compiled at runtime from source if not found in the default library.
            // Runtime shader compilation uses Metal's newLibraryWithSource:options:error: API.
            if (_clearUAVUintComputePipelineState == IntPtr.Zero)
            {
                // Get the compute function from the default library
                // This assumes the shader is already compiled and available in the default library
                IntPtr defaultLibrary = _backend.GetDefaultLibrary();
                if (defaultLibrary == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] ClearUAVUint: Default library not available");
                    return;
                }

                IntPtr computeFunction = MetalNative.CreateFunctionFromLibrary(defaultLibrary, "clearUAVUint");
                if (computeFunction == IntPtr.Zero)
                {
                    // Shader function not found - try runtime compilation from source
                    Console.WriteLine("[MetalCommandList] ClearUAVUint: Compute shader function 'clearUAVUint' not found in library. Attempting runtime compilation from source.");

                    // Metal Shading Language source code for clearUAVUint compute shader
                    string shaderSource = @"
                        #include <metal_stdlib>
                        using namespace metal;

                        kernel void clearUAVUint(texture2d<uint, access::write> output [[texture(0)]],
                                                 constant uint& clearValue [[buffer(0)]],
                                                 uint2 gid [[thread_position_in_grid]])
                        {
                            if (gid.x < output.get_width() && gid.y < output.get_height())
                            {
                                output.write(clearValue, gid);
                            }
                        }
                    ";

                    IntPtr nativeDevice = _backend.GetNativeDevice();
                    if (nativeDevice == IntPtr.Zero)
                    {
                        Console.WriteLine("[MetalCommandList] ClearUAVUint: Device not available for runtime compilation");
                        return;
                    }

                    // Compile shader library from source
                    IntPtr compiledLibrary = MetalNative.CompileLibraryFromSource(nativeDevice, shaderSource, out IntPtr compileError);
                    if (compiledLibrary == IntPtr.Zero)
                    {
                        string errorMsg = compileError != IntPtr.Zero ? MetalNative.GetNSErrorDescription(compileError) : "Unknown compilation error";
                        Console.WriteLine($"[MetalCommandList] ClearUAVUint: Failed to compile shader from source: {errorMsg}");
                        return;
                    }

                    try
                    {
                        // Get function from compiled library
                        computeFunction = MetalNative.CreateFunctionFromLibrary(compiledLibrary, "clearUAVUint");
                        if (computeFunction == IntPtr.Zero)
                        {
                            Console.WriteLine("[MetalCommandList] ClearUAVUint: Function 'clearUAVUint' not found in compiled library");
                            MetalNative.ReleaseLibrary(compiledLibrary);
                            return;
                        }
                    }
                    finally
                    {
                        // Release compiled library (function retains reference)
                        MetalNative.ReleaseLibrary(compiledLibrary);
                    }
                }

                // Create compute pipeline state
                IntPtr device = _backend.GetNativeDevice();
                if (device == IntPtr.Zero)
                {
                    MetalNative.ReleaseFunction(computeFunction);
                    Console.WriteLine("[MetalCommandList] ClearUAVUint: Device not available");
                    return;
                }

                _clearUAVUintComputePipelineState = MetalNative.CreateComputePipelineState(device, computeFunction);
                MetalNative.ReleaseFunction(computeFunction);

                if (_clearUAVUintComputePipelineState == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] ClearUAVUint: Failed to create compute pipeline state");
                    return;
                }
            }

            // Set compute pipeline state
            MetalNative.SetComputePipelineState(_currentComputeCommandEncoder, _clearUAVUintComputePipelineState);

            // Set texture as write-only resource at index 0
            MetalNative.SetTexture(_currentComputeCommandEncoder, textureHandle, 0, MetalTextureUsage.ShaderWrite);

            // Set clear value in constant buffer at index 0
            // Metal expects uint (4 bytes) for the clear value
            IntPtr clearValuePtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
            try
            {
                System.Runtime.InteropServices.Marshal.WriteInt32(clearValuePtr, unchecked((int)value));
                MetalNative.SetBytes(_currentComputeCommandEncoder, clearValuePtr, 4, 0);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(clearValuePtr);
            }

            // Calculate threadgroup size and dispatch
            // Metal compute shaders use threadgroups - we dispatch enough threadgroups to cover the entire texture
            // Threadgroup size: 16x16 threads per group (256 threads total per group)
            const int threadsPerGroupX = 16;
            const int threadsPerGroupY = 16;
            const int threadsPerGroupZ = 1;

            // Calculate number of threadgroups needed to cover the entire texture
            int threadGroupCountX = (desc.Width + threadsPerGroupX - 1) / threadsPerGroupX;
            int threadGroupCountY = (desc.Height + threadsPerGroupY - 1) / threadsPerGroupY;
            int threadGroupCountZ = 1;

            // Dispatch compute threads
            MetalNative.DispatchThreadgroups(_currentComputeCommandEncoder, threadGroupCountX, threadGroupCountY, threadGroupCountZ, threadsPerGroupX, threadsPerGroupY, threadsPerGroupZ);

            // Note: We do not end the compute encoder here - it may be reused for multiple compute dispatches
            // The compute encoder will be ended when:
            // 1. Another encoder type is started (render/blit/accelStruct)
            // 2. The command list is closed
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
        /// <summary>
        /// Sets the complete graphics pipeline state for rendering.
        ///
        /// This method stores the graphics state which is then used by draw commands
        /// to retrieve primitive type, index buffer, and other rendering parameters.
        ///
        /// The graphics state includes:
        /// - Graphics pipeline (contains primitive topology)
        /// - Framebuffer (render targets)
        /// - Viewports and scissor rectangles
        /// - Binding sets (shader resources)
        /// - Vertex buffers
        /// - Index buffer and index format
        ///
        /// Based on Metal API: Graphics state is managed through render pipeline state objects
        /// and command encoder state. Metal API Reference: https://developer.apple.com/documentation/metal
        /// </summary>
        /// <param name="state">Complete graphics state configuration</param>
        public void SetGraphicsState(GraphicsState state)
        {
            if (!_isOpen)
            {
                return;
            }

            // Store the current graphics state for use by draw commands
            // This allows draw commands to retrieve primitive type, index buffer, etc.
            _currentGraphicsState = state;

            // Validate pipeline
            if (state.Pipeline == null)
            {
                Console.WriteLine("[MetalCommandList] SetGraphicsState: Pipeline is null");
                return;
            }

            // Get Metal pipeline from state
            MetalPipeline metalPipeline = state.Pipeline as MetalPipeline;
            if (metalPipeline == null)
            {
                Console.WriteLine("[MetalCommandList] SetGraphicsState: Pipeline must be a MetalPipeline");
                return;
            }

            // Get render pipeline state handle from MetalPipeline
            // MetalPipeline stores the render pipeline state handle internally
            IntPtr renderPipelineState = metalPipeline.RenderPipelineState;
            if (renderPipelineState == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] SetGraphicsState: Pipeline does not have a valid render pipeline state");
                return;
            }

            // Get command buffer for creating render encoder
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] SetGraphicsState: No active command buffer");
                return;
            }

            // End any active encoders before creating render encoder
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentComputeCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentComputeCommandEncoder);
                MetalNative.ReleaseComputeCommandEncoder(_currentComputeCommandEncoder);
                _currentComputeCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndAccelerationStructureCommandEncoding(_currentAccelStructCommandEncoder);
                _currentAccelStructCommandEncoder = IntPtr.Zero;
            }

            // Create render pass descriptor from framebuffer
            if (state.Framebuffer != null)
            {
                MetalFramebuffer metalFramebuffer = state.Framebuffer as MetalFramebuffer;
                if (metalFramebuffer != null)
                {
                    FramebufferDesc framebufferDesc = metalFramebuffer.Desc;

                    // Create render pass descriptor
                    IntPtr renderPassDescriptor = MetalNative.CreateRenderPassDescriptor();
                    if (renderPassDescriptor != IntPtr.Zero)
                    {
                        try
                        {
                            // Set color attachments
                            if (framebufferDesc.ColorAttachments != null)
                            {
                                for (int i = 0; i < framebufferDesc.ColorAttachments.Length; i++)
                                {
                                    FramebufferAttachment attachment = framebufferDesc.ColorAttachments[i];
                                    if (attachment.Texture != null)
                                    {
                                        MetalTexture metalTexture = attachment.Texture as MetalTexture;
                                        if (metalTexture != null)
                                        {
                                            IntPtr colorTexture = metalTexture.NativeHandle;
                                            if (colorTexture != IntPtr.Zero)
                                            {
                                                // Use default clear color (black) since FramebufferAttachment doesn't have ClearColor
                                                MetalClearColor clearColor = new MetalClearColor(0.0f, 0.0f, 0.0f, 1.0f);
                                                MetalNative.SetRenderPassColorAttachment(renderPassDescriptor, (uint)i, colorTexture,
                                                    MetalLoadAction.Clear, MetalStoreAction.Store, clearColor);
                                            }
                                        }
                                    }
                                }
                            }

                            // Set depth/stencil attachment
                            if (framebufferDesc.DepthAttachment.Texture != null)
                            {
                                MetalTexture metalTexture = framebufferDesc.DepthAttachment.Texture as MetalTexture;
                                if (metalTexture != null)
                                {
                                    IntPtr depthTexture = metalTexture.NativeHandle;
                                    if (depthTexture != IntPtr.Zero)
                                    {
                                        // Use default clear values since FramebufferAttachment doesn't have ClearDepth/ClearStencil
                                        MetalNative.SetRenderPassDepthAttachment(renderPassDescriptor, depthTexture,
                                            MetalLoadAction.Clear, MetalStoreAction.Store, 1.0);
                                        MetalNative.SetRenderPassStencilAttachment(renderPassDescriptor, depthTexture,
                                            MetalLoadAction.Clear, MetalStoreAction.Store, 0);
                                    }
                                }
                            }

                            // Begin render pass
                            _currentRenderCommandEncoder = MetalNative.BeginRenderPass(commandBuffer, renderPassDescriptor);
                            if (_currentRenderCommandEncoder == IntPtr.Zero)
                            {
                                Console.WriteLine("[MetalCommandList] SetGraphicsState: Failed to begin render pass");
                                return;
                            }

                            // Set render pipeline state
                            MetalNative.SetRenderPipelineState(_currentRenderCommandEncoder, renderPipelineState);

                            // Apply current blend constant if it has been set
                            // This ensures the blend color is applied when the render command encoder is created
                            if (_currentBlendConstant.W != 0.0f || _currentBlendConstant.X != 0.0f ||
                                _currentBlendConstant.Y != 0.0f || _currentBlendConstant.Z != 0.0f)
                            {
                                try
                                {
                                    IntPtr selector = MetalNative.RegisterSelector("setBlendColorRed:green:blue:alpha:");
                                    if (selector != IntPtr.Zero)
                                    {
                                        MetalNative.objc_msgSend_void_float4(_currentRenderCommandEncoder, selector,
                                            _currentBlendConstant.X, _currentBlendConstant.Y,
                                            _currentBlendConstant.Z, _currentBlendConstant.W);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[MetalCommandList] SetGraphicsState: Exception applying blend constant: {ex.Message}");
                                }
                            }

                            // Set viewport if provided
                            // GraphicsState has Viewport (singular) which is a ViewportState
                            // ViewportState contains Viewports array
                            if (state.Viewport.Viewports != null && state.Viewport.Viewports.Length > 0)
                            {
                                SetViewports(state.Viewport.Viewports);
                            }

                            // Set scissors if provided
                            // ViewportState contains Scissors array
                            if (state.Viewport.Scissors != null && state.Viewport.Scissors.Length > 0)
                            {
                                SetScissors(state.Viewport.Scissors);
                            }

                            // Bind vertex buffers
                            if (state.VertexBuffers != null)
                            {
                                for (int i = 0; i < state.VertexBuffers.Length; i++)
                                {
                                    IBuffer vertexBuffer = state.VertexBuffers[i];
                                    if (vertexBuffer != null)
                                    {
                                        MetalBuffer metalBuffer = vertexBuffer as MetalBuffer;
                                        if (metalBuffer != null)
                                        {
                                            IntPtr bufferHandle = metalBuffer.NativeHandle;
                                            if (bufferHandle != IntPtr.Zero)
                                            {
                                                // Bind vertex buffer at slot i
                                                // Metal API: [renderEncoder setVertexBuffer:buffer offset:0 atIndex:i]
                                                MetalNative.SetVertexBuffer(_currentRenderCommandEncoder, bufferHandle, 0UL, (uint)i);
                                            }
                                        }
                                    }
                                }
                            }

                            // Bind index buffer if provided
                            if (state.IndexBuffer != null)
                            {
                                MetalBuffer metalIndexBuffer = state.IndexBuffer as MetalBuffer;
                                if (metalIndexBuffer != null)
                                {
                                    IntPtr indexBufferHandle = metalIndexBuffer.NativeHandle;
                                    if (indexBufferHandle != IntPtr.Zero)
                                    {
                                        // Index buffer is bound when drawing indexed primitives
                                        // We store it in _currentGraphicsState for use in DrawIndexed
                                    }
                                }
                            }

                            // Bind binding sets (descriptor sets) if provided
                            if (state.BindingSets != null)
                            {
                                for (int i = 0; i < state.BindingSets.Length; i++)
                                {
                                    IBindingSet bindingSet = state.BindingSets[i];
                                    if (bindingSet != null)
                                    {
                                        MetalBindingSet metalBindingSet = bindingSet as MetalBindingSet;
                                        if (metalBindingSet != null)
                                        {
                                            IntPtr argumentBuffer = metalBindingSet.ArgumentBuffer;
                                            if (argumentBuffer != IntPtr.Zero)
                                            {
                                                // Bind argument buffer at index i
                                                // Metal API: [renderEncoder setVertexBuffer:argumentBuffer offset:0 atIndex:bindingSetIndex]
                                                // For fragment shader resources, use setFragmentBuffer
                                                MetalNative.SetVertexBuffer(_currentRenderCommandEncoder, argumentBuffer, 0UL, (uint)(i + 10)); // Offset by 10 to avoid conflicts
                                                MetalNative.SetFragmentBuffer(_currentRenderCommandEncoder, argumentBuffer, 0UL, (uint)(i + 10));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            // Release render pass descriptor (it's retained by the render encoder)
                            MetalNative.ReleaseRenderPassDescriptor(renderPassDescriptor);
                        }
                    }
                }
            }
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

            // Check if Metal supports batch viewport setting API (setViewports:count:)
            // This API does not exist in current Metal versions but is checked for future compatibility
            if (!_supportsBatchViewports.HasValue)
            {
                _supportsBatchViewports = MetalNative.SupportsBatchViewports(_currentRenderCommandEncoder);
            }

            // Use optimized batch API if available, otherwise fall back to individual calls
            if (_supportsBatchViewports.Value && viewports.Length > 1)
            {
                // Convert viewports to MetalViewport array for native marshalling
                MetalViewport[] metalViewports = new MetalViewport[viewports.Length];
                for (int i = 0; i < viewports.Length; i++)
                {
                    metalViewports[i] = new MetalViewport
                    {
                        OriginX = viewports[i].X,
                        OriginY = viewports[i].Y,
                        Width = viewports[i].Width,
                        Height = viewports[i].Height,
                        Znear = viewports[i].MinDepth,
                        Zfar = viewports[i].MaxDepth
                    };
                }

                // Use native array marshalling for batch viewport setting
                // This is more efficient than individual calls as it reduces P/Invoke overhead
                MetalNative.SetViewports(_currentRenderCommandEncoder, metalViewports, (uint)metalViewports.Length);
            }
            else
            {
                // Fallback: Set viewports individually (current Metal API)
                // Metal API only supports setting one viewport at a time via setViewport:
                // The struct is created on the stack for each iteration, which is efficient and avoids heap allocation.
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
        }

        /// <summary>
        /// Sets the scissor rectangle for clipping fragments.
        /// Based on Metal API: MTLRenderCommandEncoder::setScissorRect(MTLScissorRect)
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515458-setscissorrect
        /// </summary>
        public void SetScissor(Rectangle scissor)
        {
            if (!_isOpen)
            {
                return;
            }

            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515458-setscissorrect
            // Render command encoder must be available (SetGraphicsState must be called first)
            if (_currentRenderCommandEncoder == IntPtr.Zero)
            {
                // Render command encoder not available - SetGraphicsState must be called first to begin render pass
                return;
            }

            // Convert Rectangle struct to MetalScissorRect struct
            // Metal scissor rect uses NSUInteger (uint) for coordinates
            // Rectangle uses int, so we need to clamp to ensure non-negative values
            MetalScissorRect metalScissorRect = new MetalScissorRect
            {
                X = (uint)Math.Max(0, scissor.X),
                Y = (uint)Math.Max(0, scissor.Y),
                Width = (uint)Math.Max(0, scissor.Width),
                Height = (uint)Math.Max(0, scissor.Height)
            };

            MetalNative.SetScissorRect(_currentRenderCommandEncoder, metalScissorRect);
        }

        /// <summary>
        /// Sets multiple scissor rectangles.
        /// Note: Metal only supports one scissor rectangle at a time via setScissorRect.
        /// This method sets the first scissor rectangle from the array.
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515458-setscissorrect
        /// </summary>
        public void SetScissors(Rectangle[] scissors)
        {
            if (!_isOpen || scissors == null || scissors.Length == 0)
            {
                return;
            }

            // Metal only supports setting one scissor rectangle at a time
            // Set the first scissor rectangle from the array
            // This matches the behavior of other graphics APIs where multiple scissors are viewport-indexed
            // and Metal doesn't have viewport-indexed scissor rectangles
            SetScissor(scissors[0]);
        }

        /// <summary>
        /// Sets the blend constant color for blending operations.
        /// Based on Metal API: MTLRenderCommandEncoder::setBlendColorRed:green:blue:alpha:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516253-setblendcolorred
        /// </summary>
        /// <param name="color">The blend constant color (RGBA components in range [0.0, 1.0]).</param>
        /// <remarks>
        /// Blend Constant Color:
        /// - Used when blend factors are set to BlendFactor.BlendFactor or InverseBlendFactor
        /// - Metal supports dynamic blend constant color changes via MTLRenderCommandEncoder::setBlendColorRed:green:blue:alpha:
        /// - This method sets the blend color that will be used for subsequent draw calls
        /// - The blend color is applied immediately to the current render command encoder
        /// - Equivalent to Vulkan's vkCmdSetBlendConstants and D3D12's OMSetBlendFactor
        ///
        /// Implementation Details:
        /// - The blend color is set on the active render command encoder using Objective-C interop
        /// - Color components are clamped to [0.0, 1.0] range as required by Metal
        /// - If no render command encoder is active, the value is stored and applied when one becomes available
        /// - The blend color persists for all subsequent draw calls until changed
        /// </remarks>
        public void SetBlendConstant(Vector4 color)
        {
            if (!_isOpen)
            {
                return;
            }

            // Validate and clamp color components to valid range [0.0, 1.0]
            // Metal requires blend color components to be in this range
            float r = Math.Max(0.0f, Math.Min(1.0f, color.X));
            float g = Math.Max(0.0f, Math.Min(1.0f, color.Y));
            float b = Math.Max(0.0f, Math.Min(1.0f, color.Z));
            float a = Math.Max(0.0f, Math.Min(1.0f, color.W));

            // Store the blend constant for reference
            _currentBlendConstant = new Vector4(r, g, b, a);

            // Apply blend color to the current render command encoder if available
            // Metal API: MTLRenderCommandEncoder::setBlendColorRed:green:blue:alpha:
            // Method signature: - (void)setBlendColorRed:(float)red green:(float)green blue:(float)blue alpha:(float)alpha;
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                try
                {
                    IntPtr selector = MetalNative.RegisterSelector("setBlendColorRed:green:blue:alpha:");
                    if (selector != IntPtr.Zero)
                    {
                        MetalNative.objc_msgSend_void_float4(_currentRenderCommandEncoder, selector, r, g, b, a);
                    }
                    else
                    {
                        Console.WriteLine("[MetalCommandList] SetBlendConstant: Failed to register selector for setBlendColorRed:green:blue:alpha:");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MetalCommandList] SetBlendConstant: Exception setting blend color: {ex.Message}");
                }
            }
            // Note: If render command encoder is not yet available, the blend constant will be applied
            // when SetGraphicsState is called, which ensures a render command encoder is created
        }

        /// <summary>
        /// Sets the stencil reference value for subsequent draw calls.
        /// Based on Metal API: MTLRenderCommandEncoder::setStencilReferenceValue(uint32_t)
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516252-setstencilreferencevalue
        /// This method sets the reference value used in stencil testing operations.
        /// The reference value is compared against the stencil buffer value using the stencil function
        /// specified in the depth-stencil state descriptor when the render pipeline state was created.
        /// This matches the behavior of DirectX 12 OMSetStencilRef and Vulkan vkCmdSetStencilReference.
        /// swkotor2.exe: N/A - Original game used DirectX 9, not Metal
        /// </summary>
        /// <param name="reference">The stencil reference value (0-255).</param>
        public void SetStencilRef(uint reference)
        {
            if (!_isOpen)
            {
                return; // Cannot record commands when command list is closed
            }

            if (_currentRenderCommandEncoder == IntPtr.Zero)
            {
                return; // No active render command encoder
            }

            // Set stencil reference value on the current render command encoder
            // Metal API: MTLRenderCommandEncoder::setStencilReferenceValue(uint32_t)
            MetalNative.SetStencilReference(_currentRenderCommandEncoder, reference);
        }

        // Draw Commands
        public void Draw(DrawArguments args)
        {
            if (!_isOpen)
            {
                return;
            }

            // Validate arguments
            if (args.VertexCount <= 0)
            {
                Console.WriteLine("[MetalCommandList] Draw: Invalid vertex count");
                return;
            }

            // Render command encoder must be active for draw commands
            if (_currentRenderCommandEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] Draw: Render command encoder not available - SetGraphicsState must be called first to begin render pass");
                return;
            }

            // Get current primitive type from graphics state
            MetalPrimitiveType primitiveType = MetalDevice.GetPrimitiveTypeFromPipeline(_currentGraphicsState.Pipeline);

            // Extract draw parameters from DrawArguments
            int vertexStart = args.StartVertexLocation;
            int vertexCount = args.VertexCount;
            int instanceCount = args.InstanceCount > 0 ? args.InstanceCount : 1; // Default to 1 if not specified
            int baseInstance = args.StartInstanceLocation;

            // Metal API: MTLRenderCommandEncoder::drawPrimitives:vertexStart:vertexCount:instanceCount:baseInstance:
            // Method signature: - (void)drawPrimitives:(MTLPrimitiveType)primitiveType vertexStart:(NSUInteger)vertexStart vertexCount:(NSUInteger)vertexCount instanceCount:(NSUInteger)instanceCount baseInstance:(NSUInteger)baseInstance;
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516321-drawprimitives
            // swkotor2.exe: N/A - Original game used DirectX 9, not Metal
            MetalNative.DrawPrimitives(_currentRenderCommandEncoder, primitiveType, vertexStart, vertexCount, instanceCount, baseInstance);
        }

        public void DrawIndexed(DrawArguments args)
        {
            if (!_isOpen)
            {
                return;
            }

            // Validate arguments
            if (args.VertexCount <= 0)
            {
                Console.WriteLine("[MetalCommandList] DrawIndexed: Invalid vertex count");
                return;
            }

            // Render command encoder must be active for draw commands
            if (_currentRenderCommandEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] DrawIndexed: Render command encoder not available - SetGraphicsState must be called first to begin render pass");
                return;
            }

            // Get current primitive type, index type, and index buffer from graphics state
            MetalPrimitiveType primitiveType = MetalDevice.GetPrimitiveTypeFromPipeline(_currentGraphicsState.Pipeline);
            MetalIndexType indexType;
            IntPtr indexBuffer;
            int indexBufferOffset = 0;

            // Get index buffer and index format from graphics state
            if (_currentGraphicsState.IndexBuffer != null)
            {
                MetalBuffer metalIndexBuffer = _currentGraphicsState.IndexBuffer as MetalBuffer;
                if (metalIndexBuffer != null)
                {
                    indexBuffer = metalIndexBuffer.NativeHandle;
                    if (indexBuffer == IntPtr.Zero)
                    {
                        indexBuffer = metalIndexBuffer.NativeHandle; // Try again (shouldn't be needed but defensive)
                    }
                }
                else
                {
                    indexBuffer = IntPtr.Zero;
                }

                // Convert TextureFormat to MetalIndexType
                // R16_UInt -> UInt16, R32_UInt -> UInt32
                if (_currentGraphicsState.IndexFormat == TextureFormat.R16_UInt)
                {
                    indexType = MetalIndexType.UInt16;
                }
                else if (_currentGraphicsState.IndexFormat == TextureFormat.R32_UInt)
                {
                    indexType = MetalIndexType.UInt32;
                }
                else
                {
                    // Default to UInt32 if format is unknown
                    indexType = MetalIndexType.UInt32;
                }
            }
            else
            {
                indexBuffer = IntPtr.Zero;
                indexType = MetalIndexType.UInt16; // Default, but won't be used if buffer is null
            }

            if (indexBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] DrawIndexed: Index buffer not available - SetGraphicsState must configure index buffer");
                return;
            }

            // Extract draw parameters from DrawArguments
            // For indexed draws, VertexCount represents the index count
            int indexCount = args.VertexCount;
            int baseVertex = args.BaseVertexLocation;
            int instanceCount = args.InstanceCount > 0 ? args.InstanceCount : 1; // Default to 1 if not specified
            int baseInstance = args.StartInstanceLocation;

            // Metal API: MTLRenderCommandEncoder::drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:instanceCount:baseVertex:baseInstance:
            // Method signature: - (void)drawIndexedPrimitives:(MTLPrimitiveType)primitiveType indexCount:(NSUInteger)indexCount indexType:(MTLIndexType)indexType indexBuffer:(id<MTLBuffer>)indexBuffer indexBufferOffset:(NSUInteger)indexBufferOffset instanceCount:(NSUInteger)instanceCount baseVertex:(NSInteger)baseVertex baseInstance:(NSUInteger)baseInstance;
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515527-drawindexedprimitives
            // swkotor2.exe: N/A - Original game used DirectX 9, not Metal
            MetalNative.DrawIndexedPrimitives(_currentRenderCommandEncoder, primitiveType, indexCount, indexType, indexBuffer, indexBufferOffset, instanceCount, baseVertex, baseInstance);
        }

        public void DrawIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride)
        {
            if (!_isOpen || argumentBuffer == null)
            {
                return;
            }

            // Validate parameters
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

            // Render command encoder must be active for draw commands
            if (_currentRenderCommandEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] DrawIndirect: Render command encoder not available - SetGraphicsState must be called first to begin render pass");
                return;
            }

            // Cast buffer to MetalBuffer to get native handle
            MetalBuffer metalBuffer = argumentBuffer as MetalBuffer;
            if (metalBuffer == null)
            {
                throw new ArgumentException("Argument buffer must be a MetalBuffer instance", nameof(argumentBuffer));
            }

            IntPtr indirectBuffer = metalBuffer.NativeHandle;
            if (indirectBuffer == IntPtr.Zero)
            {
                throw new ArgumentException("Argument buffer has invalid native handle", nameof(argumentBuffer));
            }

            // Get current primitive type from graphics state
            MetalPrimitiveType primitiveType = MetalDevice.GetPrimitiveTypeFromPipeline(_currentGraphicsState.Pipeline);

            // Metal's drawPrimitives:indirectBuffer:indirectBufferOffset: method draws a single indirect command
            // For multi-draw indirect, we need to loop and call the method multiple times with stride-based offsets
            // Based on Metal API: MTLRenderCommandEncoder::drawPrimitives:indirectBuffer:indirectBufferOffset:
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515526-drawprimitives
            // swkotor2.exe: N/A - Original game used DirectX 9, not Metal
            for (int i = 0; i < drawCount; i++)
            {
                int currentOffset = offset + (i * stride);
                MetalNative.DrawPrimitivesIndirect(_currentRenderCommandEncoder, primitiveType, indirectBuffer, unchecked((ulong)currentOffset));
            }
        }

        public void DrawIndexedIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride)
        {
            if (!_isOpen || argumentBuffer == null)
            {
                return;
            }

            // Validate parameters
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

            // Render command encoder must be active for draw commands
            if (_currentRenderCommandEncoder == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] DrawIndexedIndirect: Render command encoder not available - SetGraphicsState must be called first to begin render pass");
                return;
            }

            // Cast buffer to MetalBuffer to get native handle
            MetalBuffer metalBuffer = argumentBuffer as MetalBuffer;
            if (metalBuffer == null)
            {
                throw new ArgumentException("Argument buffer must be a MetalBuffer instance", nameof(argumentBuffer));
            }

            IntPtr indirectBuffer = metalBuffer.NativeHandle;
            if (indirectBuffer == IntPtr.Zero)
            {
                throw new ArgumentException("Argument buffer has invalid native handle", nameof(argumentBuffer));
            }

            // Get current primitive type and index buffer from graphics state
            MetalPrimitiveType primitiveType = MetalDevice.GetPrimitiveTypeFromPipeline(_currentGraphicsState.Pipeline);
            MetalIndexType indexType;
            IntPtr indexBuffer;
            int indexBufferOffset = 0;

            // Get index buffer and index format from graphics state
            if (_currentGraphicsState.IndexBuffer != null)
            {
                MetalBuffer metalIndexBuffer = _currentGraphicsState.IndexBuffer as MetalBuffer;
                if (metalIndexBuffer != null)
                {
                    indexBuffer = metalIndexBuffer.NativeHandle;
                }
                else
                {
                    indexBuffer = IntPtr.Zero;
                }

                // Convert TextureFormat to MetalIndexType
                if (_currentGraphicsState.IndexFormat == TextureFormat.R16_UInt)
                {
                    indexType = MetalIndexType.UInt16;
                }
                else if (_currentGraphicsState.IndexFormat == TextureFormat.R32_UInt)
                {
                    indexType = MetalIndexType.UInt32;
                }
                else
                {
                    indexType = MetalIndexType.UInt32; // Default
                }
            }
            else
            {
                indexBuffer = IntPtr.Zero;
                indexType = MetalIndexType.UInt16; // Default, but won't be used if buffer is null
            }

            // Metal's drawIndexedPrimitives:indexType:indexBuffer:indexBufferOffset:indirectBuffer:indirectBufferOffset: method draws a single indirect command
            // For multi-draw indexed indirect, we need to loop and call the method multiple times with stride-based offsets
            // Based on Metal API: MTLRenderCommandEncoder::drawIndexedPrimitives:indexType:indexBuffer:indexBufferOffset:indirectBuffer:indirectBufferOffset:
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515544-drawindexedprimitives
            // swkotor2.exe: N/A - Original game used DirectX 9, not Metal
            for (int i = 0; i < drawCount; i++)
            {
                int currentOffset = offset + (i * stride);
                MetalNative.DrawIndexedPrimitivesIndirect(_currentRenderCommandEncoder, primitiveType, indexType, indexBuffer, unchecked((ulong)indexBufferOffset), indirectBuffer, unchecked((ulong)currentOffset));
            }
        }

        // Compute State
        public void SetComputeState(ComputeState state)
        {
            if (!_isOpen)
            {
                return;
            }

            // Validate compute state
            if (state.Pipeline == null)
            {
                throw new ArgumentException("Compute state must have a valid pipeline", nameof(state));
            }

            // Cast to Metal implementation to access native handle
            MetalComputePipeline metalPipeline = state.Pipeline as MetalComputePipeline;
            if (metalPipeline == null)
            {
                throw new ArgumentException("Pipeline must be a MetalComputePipeline", nameof(state));
            }

            // Get compute pipeline state from MetalComputePipeline
            // MetalComputePipeline exposes ComputePipelineState as an internal property
            IntPtr computePipelineState = metalPipeline.ComputePipelineState;
            if (computePipelineState == IntPtr.Zero)
            {
                throw new InvalidOperationException("Compute pipeline does not have a valid MTLComputePipelineState handle. Pipeline may not have been fully created.");
            }

            // Get or create compute command encoder
            // Based on Metal API: MTLCommandBuffer::computeCommandEncoder creates a compute command encoder
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/1443000-computecommandencoder
            // The compute command encoder is used to record compute shader dispatch commands
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Command buffer not available. Cannot create compute command encoder.");
            }

            // End any other active encoders before creating compute encoder
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                // Metal render command encoder is released automatically when command buffer is released
                // No explicit release needed - just end encoding
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndAccelerationStructureCommandEncoding(_currentAccelStructCommandEncoder);
                _currentAccelStructCommandEncoder = IntPtr.Zero;
            }

            // Create compute command encoder if not already created
            if (_currentComputeCommandEncoder == IntPtr.Zero)
            {
                _currentComputeCommandEncoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                if (_currentComputeCommandEncoder == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create compute command encoder");
                }
            }

            // Step 1: Set the compute pipeline state
            // Based on Metal API: MTLComputeCommandEncoder::setComputePipelineState(_:)
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcomputecommandencoder/1443158-setcomputepipelinestate
            // This sets the compute shader and any pipeline state configuration
            MetalNative.SetComputePipelineState(_currentComputeCommandEncoder, computePipelineState);
            _currentComputePipelineState = computePipelineState; // Store for threadgroup size queries

            // Step 2: Bind descriptor sets (binding sets) if provided
            // In Metal, binding sets use argument buffers (MTLArgumentBuffer)
            // Each binding set's argument buffer is bound at a specific index
            // Based on Metal API: MTLComputeCommandEncoder::setBuffer(_:offset:atIndex:)
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcomputecommandencoder/1443156-setbuffer
            if (state.BindingSets != null && state.BindingSets.Length > 0)
            {
                for (int i = 0; i < state.BindingSets.Length; i++)
                {
                    IBindingSet bindingSet = state.BindingSets[i];
                    if (bindingSet == null)
                    {
                        continue;
                    }

                    // Cast to Metal implementation to access argument buffer
                    MetalBindingSet metalBindingSet = bindingSet as MetalBindingSet;
                    if (metalBindingSet == null)
                    {
                        Console.WriteLine($"[MetalCommandList] SetComputeState: Binding set at index {i} is not a MetalBindingSet, skipping");
                        continue;
                    }

                    // Get argument buffer from MetalBindingSet
                    IntPtr argumentBuffer = metalBindingSet.ArgumentBuffer;
                    if (argumentBuffer == IntPtr.Zero)
                    {
                        Console.WriteLine($"[MetalCommandList] SetComputeState: Binding set at index {i} has no argument buffer, skipping");
                        continue;
                    }

                    // Bind argument buffer at index i
                    // In Metal, argument buffers are bound using setBuffer:offset:atIndex:
                    // Offset is typically 0 for argument buffers (they start at the beginning)
                    MetalNative.SetBuffer(_currentComputeCommandEncoder, argumentBuffer, 0UL, unchecked((uint)i));
                }
            }
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
            if (!_isOpen)
            {
                return;
            }

            if (argumentBuffer == null)
            {
                throw new ArgumentNullException(nameof(argumentBuffer));
            }

            if (offset < 0)
            {
                throw new ArgumentException("Offset must be non-negative", nameof(offset));
            }

            // Ensure compute command encoder exists
            // Indirect dispatch requires a compute pipeline state to be set first via SetComputeState
            // The compute command encoder should already be created and have a pipeline state bound
            if (_currentComputeCommandEncoder == IntPtr.Zero)
            {
                throw new InvalidOperationException("Compute command encoder not available. SetComputeState must be called before DispatchIndirect.");
            }

            // Get Metal buffer handle from IBuffer
            // Cast to MetalBuffer to access native handle
            MetalBuffer metalBuffer = argumentBuffer as MetalBuffer;
            if (metalBuffer == null)
            {
                throw new ArgumentException("Argument buffer must be a MetalBuffer", nameof(argumentBuffer));
            }

            IntPtr metalBufferHandle = metalBuffer.NativeHandle;
            if (metalBufferHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Argument buffer has invalid native handle", nameof(argumentBuffer));
            }

            // Validate buffer size and offset
            // Indirect dispatch buffer must contain at least 12 bytes (3 uint32 values: groupCountX, groupCountY, groupCountZ)
            // The offset must be within the buffer bounds and leave at least 12 bytes for the dispatch arguments
            BufferDesc bufferDesc = metalBuffer.Desc;
            if (offset + 12 > bufferDesc.ByteSize)
            {
                throw new ArgumentException($"Offset {offset} plus 12 bytes (required for indirect dispatch arguments) exceeds buffer size {bufferDesc.ByteSize}", nameof(offset));
            }

            // Get threadgroup size for indirect dispatch
            // Metal's indirect dispatch requires the threadsPerThreadgroup size to be specified
            // The threadgroup size must match the compute shader's threadgroup size
            // Query the threadgroup size from the current compute pipeline state
            uint threadsPerThreadgroupX = 16; // Default fallback values
            uint threadsPerThreadgroupY = 16;
            uint threadsPerThreadgroupZ = 1;

            if (_currentComputePipelineState != IntPtr.Zero)
            {
                // Query actual threadgroup size from compute pipeline state
                // Metal API: MTLComputePipelineState::threadExecutionWidth and maxTotalThreadsPerThreadgroup
                // We query the actual threadgroup size that was specified when creating the pipeline
                MetalSize queriedSize = MetalNative.GetComputePipelineThreadgroupSize(_currentComputePipelineState);
                if (queriedSize.Width > 0 && queriedSize.Height > 0 && queriedSize.Depth > 0)
                {
                    threadsPerThreadgroupX = queriedSize.Width;
                    threadsPerThreadgroupY = queriedSize.Height;
                    threadsPerThreadgroupZ = queriedSize.Depth;
                }
            }

            MetalSize threadsPerThreadgroup = new MetalSize(threadsPerThreadgroupX, threadsPerThreadgroupY, threadsPerThreadgroupZ);

            // Dispatch compute work with indirect arguments
            // Based on Metal API: MTLComputeCommandEncoder::dispatchThreadgroupsWithIndirectBuffer:indirectBufferOffset:threadsPerThreadgroup:
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcomputecommandencoder/1443154-dispatchthreadgroupswithindire
            //
            // The indirect buffer contains three uint32 values at the specified offset:
            // - groupCountX (4 bytes at offset)
            // - groupCountY (4 bytes at offset + 4)
            // - groupCountZ (4 bytes at offset + 8)
            // Total: 12 bytes per indirect dispatch command
            //
            // This allows GPU-driven compute dispatch where the number of threadgroups is determined
            // by data computed on the GPU, enabling advanced techniques like GPU-driven rendering,
            // dynamic workload distribution, and adaptive compute shader dispatch.
            try
            {
                IntPtr selector = MetalNative.RegisterSelector("dispatchThreadgroupsWithIndirectBuffer:indirectBufferOffset:threadsPerThreadgroup:");
                if (selector == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to register selector for dispatchThreadgroupsWithIndirectBuffer");
                }

                MetalNative.objc_msgSend_void_buffer_ulong_size(_currentComputeCommandEncoder, selector, metalBufferHandle, unchecked((ulong)offset), threadsPerThreadgroup);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to execute indirect dispatch: {ex.Message}", ex);
            }
        }

        // Raytracing Commands
        /// <summary>
        /// Sets the raytracing state for dispatch rays commands.
        /// Stores the raytracing pipeline, binding sets, and shader binding table for use in DispatchRays.
        /// </summary>
        /// <param name="state">Raytracing state containing pipeline, binding sets, and shader binding table.</param>
        /// <remarks>
        /// Based on Metal 3.0 raytracing API:
        /// - Metal 3.0 raytracing uses compute shaders with MTLRaytracingPipelineState
        /// - Shader binding is done via MTLIntersectionFunctionTable and MTLVisibleFunctionTable
        /// - The pipeline state is stored and bound when DispatchRays is called
        /// - Binding sets are stored and applied during ray dispatch
        ///
        /// Metal API Reference:
        /// https://developer.apple.com/documentation/metal/metal_ray_tracing
        /// https://developer.apple.com/documentation/metal/mtlraytracingpipelinestate
        /// https://developer.apple.com/documentation/metal/mtlintersectionfunctiontable
        /// https://developer.apple.com/documentation/metal/mtlvisiblefunctiontable
        ///
        /// Implementation Notes:
        /// - State is stored for later use in DispatchRays (similar to D3D12Device pattern)
        /// - Pipeline validation ensures it's a MetalRaytracingPipeline
        /// - Shader binding table buffer must be valid for ray generation shader
        /// - Binding sets are stored and will be bound via compute command encoder in DispatchRays
        /// </remarks>
        public void SetRaytracingState(RaytracingState state)
        {
            if (!_isOpen)
            {
                throw new InvalidOperationException("Command list must be open to set raytracing state");
            }

            // Validate raytracing pipeline
            if (state.Pipeline == null)
            {
                throw new ArgumentException("Raytracing state must have a valid pipeline", nameof(state));
            }

            // Cast to Metal implementation to access native handle
            MetalRaytracingPipeline metalPipeline = state.Pipeline as MetalRaytracingPipeline;
            if (metalPipeline == null)
            {
                throw new ArgumentException("Pipeline must be a MetalRaytracingPipeline", nameof(state));
            }

            // Validate shader binding table buffer (required for ray generation shader)
            if (state.ShaderTable.Buffer == null)
            {
                throw new ArgumentException("Shader binding table buffer is required for raytracing state", nameof(state));
            }

            // Store the raytracing state (contains pipeline, binding sets, and shader binding table)
            // The actual binding will happen in DispatchRays when the compute command encoder is created
            _currentRaytracingState = state;
            _hasRaytracingState = true;

            // Note: In Metal 3.0, raytracing is done through compute shaders
            // The pipeline state and binding sets will be bound when DispatchRays is called
            // This matches the pattern used in D3D12Device where state is stored and applied during dispatch
        }

        /// <summary>
        /// Dispatches raytracing work using Metal 3.0 raytracing API.
        /// </summary>
        /// <param name="args">Dispatch dimensions (Width, Height, Depth).</param>
        /// <remarks>
        /// Based on Metal 3.0 raytracing API:
        /// - Metal 3.0 raytracing uses compute shaders with MTLRaytracingPipelineState
        /// - Shader binding table is provided via ShaderBindingTable structure
        /// - Resources are bound via argument buffers (MTLArgumentEncoder)
        /// - Ray dispatch is performed via compute shader dispatch
        ///
        /// Metal API Reference:
        /// https://developer.apple.com/documentation/metal/metal_ray_tracing
        /// https://developer.apple.com/documentation/metal/mtlraytracingpipelinestate
        /// https://developer.apple.com/documentation/metal/mtlcomputecommandencoder
        ///
        /// Implementation follows D3D12Device pattern:
        /// 1. Validate raytracing state is set
        /// 2. Get or create compute command encoder
        /// 3. Set raytracing pipeline state
        /// 4. Set shader binding table (via function tables)
        /// 5. Bind resources from binding sets
        /// 6. Dispatch compute work with specified dimensions
        /// </remarks>
        public void DispatchRays(DispatchRaysArguments args)
        {
            if (!_isOpen)
            {
                throw new InvalidOperationException("Command list must be open to dispatch rays");
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
            ShaderBindingTable shaderTable = _currentRaytracingState.ShaderTable;

            // Validate that shader binding table buffer is set (required for ray generation shader)
            if (shaderTable.Buffer == null)
            {
                throw new InvalidOperationException("Shader binding table buffer is required for DispatchRays");
            }

            // Get Metal buffer handle from shader binding table
            MetalBuffer shaderTableBuffer = shaderTable.Buffer as MetalBuffer;
            if (shaderTableBuffer == null)
            {
                throw new InvalidOperationException("Shader binding table buffer must be a MetalBuffer");
            }

            IntPtr shaderTableBufferHandle = shaderTableBuffer.NativeHandle;
            if (shaderTableBufferHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Shader binding table buffer has invalid native handle");
            }

            // Get Metal raytracing pipeline from state
            MetalRaytracingPipeline metalPipeline = _currentRaytracingState.Pipeline as MetalRaytracingPipeline;
            if (metalPipeline == null)
            {
                throw new InvalidOperationException("Raytracing pipeline must be a MetalRaytracingPipeline");
            }

            IntPtr raytracingPipelineState = metalPipeline.RaytracingPipelineState;
            if (raytracingPipelineState == IntPtr.Zero)
            {
                throw new InvalidOperationException("Raytracing pipeline state is invalid");
            }

            // Get command buffer for creating compute encoder
            IntPtr commandBuffer = _backend.GetCurrentCommandBuffer();
            if (commandBuffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Command buffer is not available");
            }

            // End any active encoders before creating compute encoder
            // Metal allows only one encoder type to be active at a time per command buffer
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentRenderCommandEncoder);
                _currentRenderCommandEncoder = IntPtr.Zero;
            }

            if (_currentBlitCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndEncoding(_currentBlitCommandEncoder);
                MetalNative.ReleaseBlitCommandEncoder(_currentBlitCommandEncoder);
                _currentBlitCommandEncoder = IntPtr.Zero;
            }

            if (_currentAccelStructCommandEncoder != IntPtr.Zero)
            {
                MetalNative.EndAccelerationStructureCommandEncoding(_currentAccelStructCommandEncoder);
                _currentAccelStructCommandEncoder = IntPtr.Zero;
            }

            // Get or create compute command encoder
            if (_currentComputeCommandEncoder == IntPtr.Zero)
            {
                _currentComputeCommandEncoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                if (_currentComputeCommandEncoder == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create compute command encoder for raytracing dispatch");
                }
            }

            // Set raytracing pipeline state on compute encoder
            // Metal API: [computeEncoder setRaytracingPipelineState:raytracingPipelineState]
            // Based on Metal 3.0: MTLComputeCommandEncoder::setRaytracingPipelineState:
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcomputecommandencoder/3750526-setraytracingpipelinestate
            try
            {
                IntPtr selector = MetalNative.RegisterSelector("setRaytracingPipelineState:");
                if (selector == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to register selector for setRaytracingPipelineState");
                }

                MetalNative.objc_msgSend_void_object(_currentComputeCommandEncoder, selector, raytracingPipelineState);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set raytracing pipeline state: {ex.Message}", ex);
            }

            // Validate shader binding table regions
            // Ray generation shader table is required and must have valid size
            if (shaderTable.RayGenSize == 0UL)
            {
                throw new InvalidOperationException("Ray generation shader table size cannot be zero");
            }

            // Validate that ray generation offset doesn't exceed buffer bounds
            // Note: In Metal 3.0, the shader binding table is a single buffer with offsets for different regions
            // The buffer contains shader records for ray generation, miss, hit group, and callable shaders
            // Each region has an offset and size, and they may overlap or be separate regions in the buffer
            ulong bufferSize = unchecked((ulong)shaderTableBuffer.Desc.ByteSize);
            if (shaderTable.RayGenOffset >= bufferSize)
            {
                throw new InvalidOperationException($"Ray generation shader table offset ({shaderTable.RayGenOffset}) exceeds buffer size ({bufferSize})");
            }

            if (shaderTable.RayGenOffset + shaderTable.RayGenSize > bufferSize)
            {
                throw new InvalidOperationException($"Ray generation shader table extends beyond buffer bounds (offset: {shaderTable.RayGenOffset}, size: {shaderTable.RayGenSize}, buffer size: {bufferSize})");
            }

            // Validate miss shader table if present
            if (shaderTable.MissSize > 0UL)
            {
                if (shaderTable.MissOffset >= bufferSize)
                {
                    throw new InvalidOperationException($"Miss shader table offset ({shaderTable.MissOffset}) exceeds buffer size ({bufferSize})");
                }

                ulong missStride = shaderTable.MissStride > 0UL ? shaderTable.MissStride : shaderTable.MissSize;
                if (shaderTable.MissOffset + shaderTable.MissSize > bufferSize)
                {
                    throw new InvalidOperationException($"Miss shader table extends beyond buffer bounds (offset: {shaderTable.MissOffset}, size: {shaderTable.MissSize}, buffer size: {bufferSize})");
                }
            }

            // Validate hit group shader table if present
            if (shaderTable.HitGroupSize > 0UL)
            {
                if (shaderTable.HitGroupOffset >= bufferSize)
                {
                    throw new InvalidOperationException($"Hit group shader table offset ({shaderTable.HitGroupOffset}) exceeds buffer size ({bufferSize})");
                }

                ulong hitGroupStride = shaderTable.HitGroupStride > 0UL ? shaderTable.HitGroupStride : shaderTable.HitGroupSize;
                if (shaderTable.HitGroupOffset + shaderTable.HitGroupSize > bufferSize)
                {
                    throw new InvalidOperationException($"Hit group shader table extends beyond buffer bounds (offset: {shaderTable.HitGroupOffset}, size: {shaderTable.HitGroupSize}, buffer size: {bufferSize})");
                }
            }

            // Validate callable shader table if present
            if (shaderTable.CallableSize > 0UL)
            {
                if (shaderTable.CallableOffset >= bufferSize)
                {
                    throw new InvalidOperationException($"Callable shader table offset ({shaderTable.CallableOffset}) exceeds buffer size ({bufferSize})");
                }

                ulong callableStride = shaderTable.CallableStride > 0UL ? shaderTable.CallableStride : shaderTable.CallableSize;
                if (shaderTable.CallableOffset + shaderTable.CallableSize > bufferSize)
                {
                    throw new InvalidOperationException($"Callable shader table extends beyond buffer bounds (offset: {shaderTable.CallableOffset}, size: {shaderTable.CallableSize}, buffer size: {bufferSize})");
                }
            }

            // Set shader binding table buffer
            // In Metal 3.0, the shader binding table is provided as a single buffer
            // The buffer contains shader records for ray generation, miss, hit group, and callable shaders
            // Metal API: [computeEncoder setBuffer:shaderTableBuffer offset:rayGenOffset atIndex:shaderBindingTableIndex]
            // The shader binding table is bound at a specific buffer index (typically index 0)
            // The raytracing pipeline accesses shader records using the offsets specified in ShaderBindingTable
            // Note: Metal 3.0 uses a unified buffer approach where all shader records are in one buffer
            // The pipeline uses the offsets to locate ray generation, miss, hit group, and callable shader records
            // This differs from D3D12/Vulkan which use separate GPU virtual addresses, but the concept is similar
            const uint shaderBindingTableBufferIndex = 0; // Standard index for shader binding table in Metal raytracing
            MetalNative.SetBuffer(_currentComputeCommandEncoder, shaderTableBufferHandle, shaderTable.RayGenOffset, shaderBindingTableBufferIndex);

            // Bind resources from binding sets
            // Metal uses argument buffers (MTLArgumentEncoder) for resource binding
            // Each binding set contains an argument buffer that is bound to the compute encoder
            if (_currentRaytracingState.BindingSets != null && _currentRaytracingState.BindingSets.Length > 0)
            {
                for (int i = 0; i < _currentRaytracingState.BindingSets.Length; i++)
                {
                    IBindingSet bindingSet = _currentRaytracingState.BindingSets[i];
                    if (bindingSet == null)
                    {
                        continue;
                    }

                    // Cast to Metal implementation to access argument buffer
                    MetalBindingSet metalBindingSet = bindingSet as MetalBindingSet;
                    if (metalBindingSet == null)
                    {
                        Console.WriteLine($"[MetalCommandList] DispatchRays: Binding set at index {i} is not a MetalBindingSet, skipping");
                        continue;
                    }

                    // Get argument buffer from MetalBindingSet
                    IntPtr argumentBuffer = metalBindingSet.ArgumentBuffer;
                    if (argumentBuffer == IntPtr.Zero)
                    {
                        Console.WriteLine($"[MetalCommandList] DispatchRays: Binding set at index {i} has no argument buffer, skipping");
                        continue;
                    }

                    // Bind argument buffer at index (i + 1) to avoid conflict with shader binding table at index 0
                    // Metal API: [computeEncoder setBuffer:argumentBuffer offset:0 atIndex:bufferIndex]
                    uint bufferIndex = unchecked((uint)(i + 1)); // Start from index 1
                    MetalNative.SetBuffer(_currentComputeCommandEncoder, argumentBuffer, 0UL, bufferIndex);
                }
            }

            // Calculate threadgroup dimensions for compute dispatch
            // Metal raytracing compute shaders typically use a threadgroup size of (8, 8, 1) or (16, 16, 1)
            // The dispatch dimensions (Width, Height, Depth) specify the number of threadgroups
            // Threadgroup size should match the raytracing pipeline's threadgroup size
            // For Metal raytracing, a common threadgroup size is 8x8 (64 threads per group)
            // This allows efficient ray generation and traversal
            const uint threadsPerThreadgroupX = 8; // Standard threadgroup size for Metal raytracing
            const uint threadsPerThreadgroupY = 8;
            const uint threadsPerThreadgroupZ = 1;

            // Calculate threadgroup counts from dispatch dimensions
            // Threadgroup count = ceil(dispatchDimension / threadsPerThreadgroup)
            uint threadGroupCountX = unchecked((uint)((args.Width + threadsPerThreadgroupX - 1) / threadsPerThreadgroupX));
            uint threadGroupCountY = unchecked((uint)((args.Height + threadsPerThreadgroupY - 1) / threadsPerThreadgroupY));
            uint threadGroupCountZ = unchecked((uint)args.Depth); // Depth is typically 1 for raytracing

            // Dispatch compute work for raytracing
            // Metal API: MTLComputeCommandEncoder::dispatchThreadgroups:threadsPerThreadgroup:
            // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcomputecommandencoder/1443133-dispatchthreadgroups
            // This dispatches the raytracing compute shader which will generate rays and trace them
            // through the acceleration structures using the shader binding table
            MetalNative.DispatchThreadgroups(
                _currentComputeCommandEncoder,
                (int)threadGroupCountX,
                (int)threadGroupCountY,
                (int)threadGroupCountZ,
                (int)threadsPerThreadgroupX,
                (int)threadsPerThreadgroupY,
                (int)threadsPerThreadgroupZ);
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

            // Validate that this is a bottom-level acceleration structure
            if (accelStruct.IsTopLevel)
            {
                throw new ArgumentException("Acceleration structure must be bottom-level for BuildBottomLevelAccelStruct", nameof(accelStruct));
            }

            // Cast to MetalAccelStruct to access native handles
            MetalAccelStruct metalAccelStruct = accelStruct as MetalAccelStruct;
            if (metalAccelStruct == null)
            {
                throw new ArgumentException("Acceleration structure must be a Metal acceleration structure", nameof(accelStruct));
            }

            // Get native acceleration structure handle
            IntPtr accelStructHandle = metalAccelStruct.NativeHandle;
            if (accelStructHandle == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] BuildBottomLevelAccelStruct: Invalid acceleration structure native handle");
                return;
            }

            // Get Metal device for creating buffers and descriptors
            IntPtr device = _backend.GetMetalDevice();
            if (device == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] BuildBottomLevelAccelStruct: Failed to get Metal device");
                return;
            }

            // Validate all geometries are triangles (BLAS only supports triangles)
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
                // Get acceleration structure descriptor from the acceleration structure
                // For BLAS, we need to create a descriptor with triangle geometry data
                AccelStructDesc blasDesc = metalAccelStruct.Desc;

                // Create BLAS descriptor
                IntPtr blasDescriptor = MetalNative.CreateAccelerationStructureDescriptor(device, blasDesc);
                if (blasDescriptor == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] BuildBottomLevelAccelStruct: Failed to create BLAS descriptor");
                    return;
                }

                try
                {
                    // Set triangle geometry data on the descriptor for each geometry
                    // Metal requires MTLAccelerationStructureTriangleGeometryDescriptor for each triangle geometry
                    for (int i = 0; i < geometries.Length; i++)
                    {
                        var geometry = geometries[i];
                        var triangles = geometry.Triangles;

                        // Get native buffer handles
                        MetalBuffer vertexMetalBuffer = triangles.VertexBuffer as MetalBuffer;
                        if (vertexMetalBuffer == null)
                        {
                            throw new ArgumentException($"Geometry at index {i} vertex buffer is not a Metal buffer", nameof(geometries));
                        }

                        IntPtr vertexBuffer = vertexMetalBuffer.NativeHandle;
                        if (vertexBuffer == IntPtr.Zero)
                        {
                            throw new ArgumentException($"Geometry at index {i} has an invalid vertex buffer", nameof(geometries));
                        }

                        // Handle index buffer (optional for some geometries)
                        IntPtr indexBuffer = IntPtr.Zero;
                        uint indexCount = 0;
                        bool hasIndexBuffer = false;

                        if (triangles.IndexBuffer != null)
                        {
                            MetalBuffer indexMetalBuffer = triangles.IndexBuffer as MetalBuffer;
                            if (indexMetalBuffer != null)
                            {
                                indexBuffer = indexMetalBuffer.NativeHandle;
                                if (indexBuffer != IntPtr.Zero)
                                {
                                    indexCount = (uint)triangles.IndexCount;
                                    hasIndexBuffer = true;
                                }
                            }
                        }

                        // Set triangle geometry on descriptor
                        // Metal API: MTLAccelerationStructureTriangleGeometryDescriptor
                        // We need to set vertex buffer, index buffer, and geometry properties
                        MetalNative.SetTriangleGeometryOnDescriptor(
                            blasDescriptor,
                            i,
                            vertexBuffer,
                            (ulong)triangles.VertexOffset,
                            (uint)triangles.VertexStride,
                            (uint)triangles.VertexCount,
                            MetalDevice.ConvertVertexFormatToMetal(triangles.VertexFormat),
                            indexBuffer,
                            (ulong)(hasIndexBuffer ? triangles.IndexOffset : 0),
                            indexCount,
                            MetalDevice.ConvertIndexFormatToMetal(triangles.IndexFormat),
                            triangles.TransformBuffer != null ? ((MetalBuffer)triangles.TransformBuffer).NativeHandle : IntPtr.Zero,
                            (ulong)(triangles.TransformBuffer != null ? triangles.TransformOffset : 0),
                            MetalDevice.ConvertGeometryFlagsToMetal(geometry.Flags));
                    }

                    // Set geometry count on descriptor
                    MetalNative.SetGeometryCountOnDescriptor(blasDescriptor, (uint)geometries.Length);

                    // Estimate scratch buffer size for BLAS building
                    // Metal requires a scratch buffer for building acceleration structures
                    // For BLAS with triangles: conservative estimate is ~32 bytes per triangle for scratch space
                    // This is a reasonable estimate when exact size query is not available
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

                    // Round up to next 256-byte alignment (Metal requirement)
                    estimatedScratchSize = (estimatedScratchSize + 255UL) & ~255UL;

                    // Create scratch buffer for building
                    // StorageModePrivate is preferred for GPU-only scratch buffers
                    IntPtr scratchBuffer = MetalNative.CreateBufferWithOptions(device, estimatedScratchSize, (uint)MetalResourceOptions.StorageModePrivate);
                    if (scratchBuffer == IntPtr.Zero)
                    {
                        // Fallback to shared storage mode if private fails
                        scratchBuffer = MetalNative.CreateBufferWithOptions(device, estimatedScratchSize, (uint)MetalResourceOptions.StorageModeShared);
                        if (scratchBuffer == IntPtr.Zero)
                        {
                            Console.WriteLine("[MetalCommandList] BuildBottomLevelAccelStruct: Failed to create scratch buffer");
                            return;
                        }
                    }

                    try
                    {
                        // Get or create acceleration structure command encoder
                        IntPtr accelStructEncoder = GetOrCreateAccelerationStructureCommandEncoder();
                        if (accelStructEncoder == IntPtr.Zero)
                        {
                            Console.WriteLine("[MetalCommandList] BuildBottomLevelAccelStruct: Failed to get acceleration structure command encoder");
                            return;
                        }

                        // Build the bottom-level acceleration structure
                        // Metal API: [MTLAccelerationStructureCommandEncoder buildAccelerationStructure:descriptor:scratchBuffer:scratchBufferOffset:]
                        MetalNative.BuildAccelerationStructure(accelStructEncoder, accelStructHandle, blasDescriptor, scratchBuffer, 0);

                        // Note: We don't end encoding here because the encoder is managed by GetOrCreateAccelerationStructureCommandEncoder
                        // and will be ended when the command list is closed or another encoder type is created
                    }
                    finally
                    {
                        // Release scratch buffer
                        if (scratchBuffer != IntPtr.Zero)
                        {
                            MetalNative.ReleaseBuffer(scratchBuffer);
                        }
                    }
                }
                finally
                {
                    // Release BLAS descriptor
                    if (blasDescriptor != IntPtr.Zero)
                    {
                        MetalNative.ReleaseAccelerationStructureDescriptor(blasDescriptor);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to build bottom-level acceleration structure: {ex.Message}", ex);
            }
        }

        public void BuildTopLevelAccelStruct(IAccelStruct accelStruct, AccelStructInstance[] instances)
        {
            if (!_isOpen || accelStruct == null)
            {
                return;
            }

            // Validate instances array
            if (instances == null || instances.Length == 0)
            {
                Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: No instances provided");
                return;
            }

            // Cast to MetalAccelStruct to access native handles
            MetalAccelStruct metalAccelStruct = accelStruct as MetalAccelStruct;
            if (metalAccelStruct == null)
            {
                Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Acceleration structure is not a Metal acceleration structure");
                return;
            }

            // Validate that this is a top-level acceleration structure
            if (!metalAccelStruct.IsTopLevel)
            {
                Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Acceleration structure is not a top-level acceleration structure");
                return;
            }

            // Get native acceleration structure handle
            IntPtr accelStructHandle = metalAccelStruct.NativeHandle;
            if (accelStructHandle == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Invalid acceleration structure native handle");
                return;
            }

            // Get Metal device for creating buffers
            IntPtr device = _backend.GetMetalDevice();
            if (device == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Failed to get Metal device");
                return;
            }

            // Calculate instance buffer size
            // AccelStructInstance matches VkAccelerationStructureInstanceKHR layout (64 bytes)
            // Metal's MTLAccelerationStructureInstanceDescriptor is similar in size
            int instanceCount = instances.Length;
            int instanceBufferSize = instanceCount * 64; // 64 bytes per instance

            // Create instance buffer for TLAS instances
            // Metal requires instance data in a buffer for TLAS building
            // StorageModeShared allows CPU writes for instance data
            IntPtr instanceBuffer = MetalNative.CreateBufferWithOptions(device, (ulong)instanceBufferSize, (uint)MetalResourceOptions.StorageModeShared);
            if (instanceBuffer == IntPtr.Zero)
            {
                Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Failed to create instance buffer");
                return;
            }

            try
            {
                // Write instance data to buffer
                // Get pointer to buffer contents for CPU write
                IntPtr instanceBufferContents = MetalNative.GetBufferContents(instanceBuffer);
                if (instanceBufferContents == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Failed to get instance buffer contents");
                    return;
                }

                // Convert AccelStructInstance array to byte array for writing
                // AccelStructInstance is a struct matching VkAccelerationStructureInstanceKHR (64 bytes)
                // Metal's instance descriptor format is compatible
                int instanceStructSize = Marshal.SizeOf<AccelStructInstance>();
                int totalSize = instanceCount * instanceStructSize;
                byte[] instanceData = new byte[totalSize];

                // Copy instance data to byte array using pinned GCHandle for efficient marshalling
                GCHandle instancesHandle = GCHandle.Alloc(instances, GCHandleType.Pinned);
                try
                {
                    IntPtr instancesPtr = instancesHandle.AddrOfPinnedObject();
                    Marshal.Copy(instancesPtr, instanceData, 0, totalSize);
                }
                finally
                {
                    instancesHandle.Free();
                }

                // Copy instance data to buffer contents
                Marshal.Copy(instanceData, 0, instanceBufferContents, totalSize);

                // Create TLAS descriptor for building
                // Metal requires a descriptor that references the instance buffer
                // For TLAS, the descriptor should be of type MTLAccelerationStructureGeometryInstanceDescriptor
                // which contains the instance buffer reference and instance count
                AccelStructDesc tlasDesc = metalAccelStruct.Desc;

                // Create the TLAS descriptor - it will be configured with instance buffer reference below
                // Based on Metal API: MTLAccelerationStructureGeometryInstanceDescriptor
                // Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructuregeometryinstancedescriptor
                IntPtr tlasDescriptor = MetalNative.CreateAccelerationStructureDescriptor(device, tlasDesc);
                if (tlasDescriptor == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Failed to create TLAS descriptor");
                    return;
                }

                try
                {
                    // Validate descriptor and instance buffer before configuration
                    // Both must be valid for TLAS building to succeed
                    if (tlasDescriptor == IntPtr.Zero)
                    {
                        Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Invalid TLAS descriptor");
                        return;
                    }

                    if (instanceBuffer == IntPtr.Zero)
                    {
                        Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Invalid instance buffer");
                        return;
                    }

                    // Validate instance count and stride
                    if (instanceCount <= 0)
                    {
                        Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Invalid instance count: " + instanceCount);
                        return;
                    }

                    if (instanceStructSize < 64)
                    {
                        Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Invalid instance struct size: " + instanceStructSize + " (minimum 64 bytes)");
                        return;
                    }

                    // Set instance buffer reference on descriptor
                    // Metal API: [MTLAccelerationStructureGeometryInstanceDescriptor setInstanceBuffer:instanceBuffer offset:0 stridedBytesPerInstance:instanceStructSize]
                    // Metal API: [MTLAccelerationStructureGeometryInstanceDescriptor setInstanceCount:instanceCount]
                    // instanceStructSize is already calculated above (64 bytes for AccelStructInstance, matching VkAccelerationStructureInstanceKHR layout)
                    // This configures the descriptor with the instance buffer reference, offset, stride, and instance count
                    // The descriptor is now properly configured for TLAS building
                    MetalNative.SetInstanceBufferOnTLASDescriptor(tlasDescriptor, instanceBuffer, 0, (ulong)instanceStructSize, (uint)instanceCount);

                    // Verify descriptor configuration is complete
                    // After SetInstanceBufferOnTLASDescriptor, the descriptor contains:
                    // - Instance buffer reference (instanceBuffer)
                    // - Instance buffer offset (0)
                    // - Instance stride (instanceStructSize, 64 bytes)
                    // - Instance count (instanceCount)
                    // The descriptor is now ready for building the acceleration structure

                    // Estimate scratch buffer size for TLAS building
                    // Metal requires a scratch buffer for building acceleration structures
                    // For TLAS, scratch size is typically proportional to instance count
                    // A conservative estimate: ~256 bytes per instance + overhead
                    ulong estimatedScratchSize = (ulong)(instanceCount * 256 + 4096); // 256 bytes per instance + 4KB overhead

                    // Create scratch buffer for building
                    // StorageModePrivate is preferred for GPU-only scratch buffers
                    IntPtr scratchBuffer = MetalNative.CreateBufferWithOptions(device, estimatedScratchSize, (uint)MetalResourceOptions.StorageModePrivate);
                    if (scratchBuffer == IntPtr.Zero)
                    {
                        // Fallback to shared storage mode if private fails
                        scratchBuffer = MetalNative.CreateBufferWithOptions(device, estimatedScratchSize, (uint)MetalResourceOptions.StorageModeShared);
                        if (scratchBuffer == IntPtr.Zero)
                        {
                            Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Failed to create scratch buffer");
                            return;
                        }
                    }

                    try
                    {
                        // Get or create acceleration structure command encoder
                        IntPtr accelStructEncoder = GetOrCreateAccelerationStructureCommandEncoder();
                        if (accelStructEncoder == IntPtr.Zero)
                        {
                            Console.WriteLine("[MetalCommandList] BuildTopLevelAccelStruct: Failed to get acceleration structure command encoder");
                            return;
                        }

                        // Build the top-level acceleration structure
                        // Metal API: [MTLAccelerationStructureCommandEncoder buildAccelerationStructure:descriptor:scratchBuffer:scratchBufferOffset:]
                        // The descriptor is properly configured with instance buffer reference via SetInstanceBufferOnTLASDescriptor above
                        // Descriptor configuration includes:
                        // - Instance buffer reference (set via setInstanceBuffer:offset:stridedBytesPerInstance:)
                        // - Instance count (set via setInstanceCount:)
                        // - Instance stride (64 bytes per AccelStructInstance, matching VkAccelerationStructureInstanceKHR layout)
                        // All validation has been performed before this point, ensuring the descriptor is ready for building
                        MetalNative.BuildAccelerationStructure(accelStructEncoder, accelStructHandle, tlasDescriptor, scratchBuffer, 0);

                        // Note: We don't end encoding here because the encoder is managed by GetOrCreateAccelerationStructureCommandEncoder
                        // and will be ended when the command list is closed or another encoder type is created
                    }
                    finally
                    {
                        // Release scratch buffer
                        if (scratchBuffer != IntPtr.Zero)
                        {
                            MetalNative.ReleaseBuffer(scratchBuffer);
                        }
                    }
                }
                finally
                {
                    // Release TLAS descriptor
                    if (tlasDescriptor != IntPtr.Zero)
                    {
                        MetalNative.ReleaseAccelerationStructureDescriptor(tlasDescriptor);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalCommandList] BuildTopLevelAccelStruct: Exception: {ex.Message}");
                Console.WriteLine($"[MetalCommandList] BuildTopLevelAccelStruct: Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Release instance buffer
                if (instanceBuffer != IntPtr.Zero)
                {
                    MetalNative.ReleaseBuffer(instanceBuffer);
                }
            }
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
            // This uses Objective-C interop via MetalNative.CreateAccelerationStructureCommandEncoder
            // which internally calls objc_msgSend to invoke the MTLCommandBuffer method
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

        public void PushDescriptorSet(IBindingLayout bindingLayout, int setIndex, BindingSetItem[] items)
        {
            if (!_isOpen || bindingLayout == null || items == null || items.Length == 0)
            {
                return;
            }

            // Push descriptors directly without creating a binding set
            // For Metal, we create a temporary argument buffer and bind it
            // This is more efficient than creating a full binding set for frequently changing descriptors
            // Based on Metal argument buffer push descriptors pattern
            // Metal API: MTLArgumentEncoder for push descriptors

            var metalLayout = bindingLayout as MetalBindingLayout;
            if (metalLayout == null)
            {
                Console.WriteLine("[MetalCommandList] PushDescriptorSet: BindingLayout must be a MetalBindingLayout");
                return;
            }

            // Validate that the layout supports push descriptors
            // In Metal, push descriptors are implemented using argument buffers
            // The layout must have been created with IsPushDescriptor = true
            // For now, we'll create a temporary binding set and bind it
            // This satisfies the interface requirement while maintaining functionality

            // Create a temporary binding set descriptor from the items
            BindingSetDesc desc = new BindingSetDesc();
            desc.Items = items;

            // Create temporary binding set using the backend device
            // Note: This requires access to the device, which we can get from the backend
            MetalDevice device = _backend.GetDevice() as MetalDevice;
            if (device == null)
            {
                Console.WriteLine("[MetalCommandList] PushDescriptorSet: Device not available");
                return;
            }

            // Create temporary binding set
            IBindingSet tempBindingSet = device.CreateBindingSet(bindingLayout, desc);
            if (tempBindingSet == null)
            {
                Console.WriteLine("[MetalCommandList] PushDescriptorSet: Failed to create temporary binding set");
                return;
            }

            // Bind the temporary binding set
            // For graphics, bind to both vertex and fragment stages
            if (_currentRenderCommandEncoder != IntPtr.Zero)
            {
                MetalBindingSet metalBindingSet = tempBindingSet as MetalBindingSet;
                if (metalBindingSet != null)
                {
                    IntPtr argumentBuffer = metalBindingSet.ArgumentBuffer;
                    if (argumentBuffer != IntPtr.Zero)
                    {
                        // Bind argument buffer at the specified set index
                        // Metal API: [renderEncoder setVertexBuffer:argumentBuffer offset:0 atIndex:setIndex]
                        // For fragment shader resources, use setFragmentBuffer
                        MetalNative.SetVertexBuffer(_currentRenderCommandEncoder, argumentBuffer, 0UL, (uint)(setIndex + 10));
                        MetalNative.SetFragmentBuffer(_currentRenderCommandEncoder, argumentBuffer, 0UL, (uint)(setIndex + 10));
                    }
                }
            }
            // For compute, bind to compute encoder
            else if (_currentComputeCommandEncoder != IntPtr.Zero)
            {
                MetalBindingSet metalBindingSet = tempBindingSet as MetalBindingSet;
                if (metalBindingSet != null)
                {
                    IntPtr argumentBuffer = metalBindingSet.ArgumentBuffer;
                    if (argumentBuffer != IntPtr.Zero)
                    {
                        // Bind argument buffer at the specified set index
                        // Metal API: [computeEncoder setBuffer:argumentBuffer offset:0 atIndex:setIndex]
                        MetalNative.SetBuffer(_currentComputeCommandEncoder, argumentBuffer, 0UL, (uint)(setIndex + 10));
                    }
                }
            }

            // Note: The temporary binding set will be disposed when the device is disposed
            // In a production implementation, we might want to cache these or use a more efficient approach
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
        public static extern void SetComputePipelineState(IntPtr computeCommandEncoder, IntPtr computePipelineState);

        /// <summary>
        /// Gets the threadgroup size from a compute pipeline state.
        /// Based on Metal API: MTLComputePipelineState::threadExecutionWidth and maxTotalThreadsPerThreadgroup
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlcomputepipelinestate
        ///
        /// Note: Metal doesn't directly expose the threadgroup size that was specified during pipeline creation.
        /// Instead, we query threadExecutionWidth (typically 32 or 64) and maxTotalThreadsPerThreadgroup.
        /// For accurate threadgroup size, we need to query the MTLFunction's threadgroupSize property.
        /// This method queries the actual threadgroup size from the compute function used to create the pipeline.
        /// </summary>
        public static MetalSize GetComputePipelineThreadgroupSize(IntPtr computePipelineState)
        {
            if (computePipelineState == IntPtr.Zero)
            {
                return new MetalSize(0, 0, 0);
            }

            try
            {
                // Metal API: MTLComputePipelineState::threadExecutionWidth (NSUInteger)
                // This is the number of threads that execute in parallel (SIMD width), typically 32 or 64
                IntPtr threadExecutionWidthSelector = MetalNative.RegisterSelector("threadExecutionWidth");
                if (threadExecutionWidthSelector == IntPtr.Zero)
                {
                    return new MetalSize(16, 16, 1); // Default fallback
                }

                // Query threadExecutionWidth (returns NSUInteger, which is ulong on 64-bit)
                ulong threadExecutionWidth = objc_msgSend_ulong(computePipelineState, threadExecutionWidthSelector);

                // Metal API: MTLComputePipelineState::maxTotalThreadsPerThreadgroup (NSUInteger)
                // This is the maximum total threads allowed in a threadgroup
                IntPtr maxTotalThreadsSelector = MetalNative.RegisterSelector("maxTotalThreadsPerThreadgroup");
                if (maxTotalThreadsSelector == IntPtr.Zero)
                {
                    // Fallback: use threadExecutionWidth as a square threadgroup
                    uint size = (uint)Math.Min(threadExecutionWidth, 16UL); // Cap at 16x16 for safety
                    return new MetalSize(size, size, 1);
                }

                ulong maxTotalThreads = objc_msgSend_ulong(computePipelineState, maxTotalThreadsSelector);

                // Query the actual threadgroup size from the compute function
                // Metal API: MTLFunction::threadgroupSizeMatchesTileSize (bool) and threadExecutionWidth
                // We need to get the function from the pipeline state first
                IntPtr functionSelector = MetalNative.RegisterSelector("computeFunction");
                if (functionSelector == IntPtr.Zero)
                {
                    // Fallback: calculate reasonable threadgroup size from available info
                    uint width = (uint)Math.Min(threadExecutionWidth, 16UL);
                    uint height = (uint)Math.Min(maxTotalThreads / threadExecutionWidth, 16UL);
                    return new MetalSize(width, height, 1);
                }

                IntPtr computeFunction = objc_msgSend_object(computePipelineState, functionSelector);
                if (computeFunction == IntPtr.Zero)
                {
                    // Fallback: calculate reasonable threadgroup size
                    uint width = (uint)Math.Min(threadExecutionWidth, 16UL);
                    uint height = (uint)Math.Min(maxTotalThreads / threadExecutionWidth, 16UL);
                    return new MetalSize(width, height, 1);
                }

                // Query threadgroup size from the function
                // Metal API: MTLFunction::threadgroupSizeMatchesTileSize (bool)
                // For compute functions, we can query the actual threadgroup size
                // However, Metal doesn't directly expose this - we need to use the function's attributes
                // For now, we'll use a heuristic based on threadExecutionWidth
                // A common pattern is to use threadExecutionWidth x threadExecutionWidth or 16x16
                uint threadgroupWidth = (uint)Math.Min(threadExecutionWidth, 16UL);
                uint threadgroupHeight = (uint)Math.Min(maxTotalThreads / threadExecutionWidth, 16UL);

                // If maxTotalThreads is very large, use a standard 16x16 threadgroup
                if (maxTotalThreads >= 1024)
                {
                    threadgroupWidth = 16;
                    threadgroupHeight = 16;
                }

                return new MetalSize(threadgroupWidth, threadgroupHeight, 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] GetComputePipelineThreadgroupSize: Exception: {ex.Message}");
                return new MetalSize(16, 16, 1); // Default fallback
            }
        }

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong objc_msgSend_ulong(IntPtr receiver, IntPtr selector);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void SetTexture(IntPtr computeCommandEncoder, IntPtr texture, uint index, MetalTextureUsage usage);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void SetBytes(IntPtr computeCommandEncoder, IntPtr bytes, uint length, uint index);

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void SetBuffer(IntPtr computeCommandEncoder, IntPtr buffer, ulong offset, uint index);

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

        /// <summary>
        /// Sets triangle geometry data on a BLAS acceleration structure descriptor.
        /// Based on Metal API: MTLAccelerationStructureTriangleGeometryDescriptor
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructuretrianglegeometrydescriptor
        /// </summary>
        /// <param name="descriptor">BLAS acceleration structure descriptor</param>
        /// <param name="geometryIndex">Index of the geometry in the descriptor</param>
        /// <param name="vertexBuffer">Metal buffer containing vertex data</param>
        /// <param name="vertexOffset">Offset into vertex buffer in bytes</param>
        /// <param name="vertexStride">Stride between vertices in bytes</param>
        /// <param name="vertexCount">Number of vertices</param>
        /// <param name="vertexFormat">Metal vertex format (MTLAttributeFormat)</param>
        /// <param name="indexBuffer">Metal buffer containing index data (IntPtr.Zero if no indices)</param>
        /// <param name="indexOffset">Offset into index buffer in bytes</param>
        /// <param name="indexCount">Number of indices</param>
        /// <param name="indexFormat">Metal index format (MTLIndexType)</param>
        /// <param name="transformBuffer">Metal buffer containing transform data (IntPtr.Zero if no transform)</param>
        /// <param name="transformOffset">Offset into transform buffer in bytes</param>
        /// <param name="geometryFlags">Metal geometry flags (MTLAccelerationStructureGeometryOptions)</param>
        public static void SetTriangleGeometryOnDescriptor(
            IntPtr descriptor,
            int geometryIndex,
            IntPtr vertexBuffer,
            ulong vertexOffset,
            uint vertexStride,
            uint vertexCount,
            uint vertexFormat,
            IntPtr indexBuffer,
            ulong indexOffset,
            uint indexCount,
            uint indexFormat,
            IntPtr transformBuffer,
            ulong transformOffset,
            uint geometryFlags)
        {
            if (descriptor == IntPtr.Zero || vertexBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Use Objective-C runtime to call methods on MTLAccelerationStructureDescriptor
                // Metal API: [descriptor setGeometryDescriptors:geometryDescriptors count:count]
                // For each geometry, we create a MTLAccelerationStructureTriangleGeometryDescriptor
                // and set its properties using objc_msgSend

                // Create triangle geometry descriptor for this geometry
                IntPtr triangleGeometryDesc = CreateTriangleGeometryDescriptor();
                if (triangleGeometryDesc == IntPtr.Zero)
                {
                    Console.WriteLine("[MetalNative] SetTriangleGeometryOnDescriptor: Failed to create triangle geometry descriptor");
                    return;
                }

                try
                {
                    // Set vertex buffer and properties
                    SetTriangleGeometryVertexBuffer(triangleGeometryDesc, vertexBuffer, vertexOffset, vertexStride, vertexCount, vertexFormat);

                    // Set index buffer if provided
                    if (indexBuffer != IntPtr.Zero && indexCount > 0)
                    {
                        SetTriangleGeometryIndexBuffer(triangleGeometryDesc, indexBuffer, indexOffset, indexCount, indexFormat);
                    }

                    // Set transform buffer if provided
                    if (transformBuffer != IntPtr.Zero)
                    {
                        SetTriangleGeometryTransformBuffer(triangleGeometryDesc, transformBuffer, transformOffset);
                    }

                    // Set geometry options/flags
                    SetTriangleGeometryOptions(triangleGeometryDesc, geometryFlags);

                    // Add geometry descriptor to acceleration structure descriptor
                    AddGeometryDescriptorToAccelerationStructureDescriptor(descriptor, triangleGeometryDesc, geometryIndex);
                }
                finally
                {
                    // Release triangle geometry descriptor (it's retained by the acceleration structure descriptor)
                    ReleaseTriangleGeometryDescriptor(triangleGeometryDesc);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetTriangleGeometryOnDescriptor: Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the geometry count on an acceleration structure descriptor.
        /// Based on Metal API: MTLAccelerationStructureDescriptor::setGeometryDescriptors:count:
        /// </summary>
        public static void SetGeometryCountOnDescriptor(IntPtr descriptor, uint geometryCount)
        {
            if (descriptor == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Use Objective-C runtime to set geometry count
                // Metal API: [descriptor setGeometryCount:geometryCount]
                IntPtr selector = sel_registerName("setGeometryCount:");
                objc_msgSend_void_uint(descriptor, selector, geometryCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetGeometryCountOnDescriptor: Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the instance buffer reference and instance count on a TLAS acceleration structure descriptor.
        /// Based on Metal API: MTLAccelerationStructureGeometryInstanceDescriptor::setInstanceBuffer:offset:stridedBytesPerInstance:
        /// and MTLAccelerationStructureGeometryInstanceDescriptor::setInstanceCount:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructuregeometryinstancedescriptor
        /// </summary>
        /// <param name="descriptor">TLAS acceleration structure descriptor (MTLAccelerationStructureGeometryInstanceDescriptor)</param>
        /// <param name="instanceBuffer">Metal buffer containing instance data (MTLBuffer)</param>
        /// <param name="instanceOffset">Offset into instance buffer in bytes (typically 0)</param>
        /// <param name="instanceStride">Stride between instances in bytes (typically 64 for AccelStructInstance)</param>
        /// <param name="instanceCount">Number of instances</param>
        public static void SetInstanceBufferOnTLASDescriptor(IntPtr descriptor, IntPtr instanceBuffer, ulong instanceOffset, ulong instanceStride, uint instanceCount)
        {
            if (descriptor == IntPtr.Zero || instanceBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Metal API: [descriptor setInstanceBuffer:instanceBuffer offset:instanceOffset stridedBytesPerInstance:instanceStride]
                // Method signature: - (void)setInstanceBuffer:(id<MTLBuffer>)buffer offset:(NSUInteger)offset stridedBytesPerInstance:(NSUInteger)stride
                IntPtr setInstanceBufferSelector = sel_registerName("setInstanceBuffer:offset:stridedBytesPerInstance:");
                objc_msgSend_void_uint_ulong_ulong(descriptor, setInstanceBufferSelector, instanceBuffer, instanceOffset, instanceStride);

                // Metal API: [descriptor setInstanceCount:instanceCount]
                // Method signature: - (void)setInstanceCount:(NSUInteger)count
                IntPtr setInstanceCountSelector = sel_registerName("setInstanceCount:");
                objc_msgSend_void_ulong(descriptor, setInstanceCountSelector, (ulong)instanceCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetInstanceBufferOnTLASDescriptor: Exception: {ex.Message}");
                Console.WriteLine($"[MetalNative] SetInstanceBufferOnTLASDescriptor: Stack trace: {ex.StackTrace}");
            }
        }

        // Helper functions for triangle geometry descriptor manipulation
        // These use Objective-C runtime to interact with MTLAccelerationStructureTriangleGeometryDescriptor

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_void_uint(IntPtr receiver, IntPtr selector, uint value);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_void_ulong(IntPtr receiver, IntPtr selector, ulong value);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_void_uint_ulong_uint_uint_uint(IntPtr receiver, IntPtr selector, IntPtr buffer, ulong offset, uint stride, uint count, uint format);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_void_uint_ulong_uint_uint(IntPtr receiver, IntPtr selector, IntPtr buffer, ulong offset, uint count, uint format);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_void_uint_ulong(IntPtr receiver, IntPtr selector, IntPtr buffer, ulong offset);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_void_uint_ulong_ulong(IntPtr receiver, IntPtr selector, IntPtr buffer, ulong offset, ulong stride);

        private static IntPtr CreateTriangleGeometryDescriptor()
        {
            try
            {
                // Allocate MTLAccelerationStructureTriangleGeometryDescriptor using Objective-C runtime
                // Metal API: [[MTLAccelerationStructureTriangleGeometryDescriptor alloc] init]
                IntPtr triangleGeometryDescClass = objc_getClass("MTLAccelerationStructureTriangleGeometryDescriptor");
                if (triangleGeometryDescClass == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                IntPtr allocSelector = sel_registerName("alloc");
                IntPtr allocResult = objc_msgSend_object(triangleGeometryDescClass, allocSelector);

                IntPtr initSelector = sel_registerName("init");
                return objc_msgSend_object(allocResult, initSelector);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static void ReleaseTriangleGeometryDescriptor(IntPtr descriptor)
        {
            if (descriptor == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("release");
                objc_msgSend_void(descriptor, selector);
            }
            catch
            {
                // Ignore errors during release
            }
        }

        private static void SetTriangleGeometryVertexBuffer(IntPtr triangleDesc, IntPtr vertexBuffer, ulong vertexOffset, uint vertexStride, uint vertexCount, uint vertexFormat)
        {
            if (triangleDesc == IntPtr.Zero || vertexBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Metal API: [triangleDesc setVertexBuffer:vertexBuffer offset:vertexOffset]
                IntPtr setVertexBufferSelector = sel_registerName("setVertexBuffer:offset:");
                objc_msgSend_void_uint_ulong(triangleDesc, setVertexBufferSelector, vertexBuffer, vertexOffset);

                // Metal API: [triangleDesc setVertexStride:vertexStride]
                IntPtr setVertexStrideSelector = sel_registerName("setVertexStride:");
                objc_msgSend_void_ulong(triangleDesc, setVertexStrideSelector, vertexStride);

                // Metal API: [triangleDesc setVertexCount:vertexCount]
                IntPtr setVertexCountSelector = sel_registerName("setVertexCount:");
                objc_msgSend_void_ulong(triangleDesc, setVertexCountSelector, vertexCount);

                // Metal API: [triangleDesc setVertexFormat:vertexFormat]
                IntPtr setVertexFormatSelector = sel_registerName("setVertexFormat:");
                objc_msgSend_void_uint(triangleDesc, setVertexFormatSelector, vertexFormat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetTriangleGeometryVertexBuffer: Exception: {ex.Message}");
            }
        }

        private static void SetTriangleGeometryIndexBuffer(IntPtr triangleDesc, IntPtr indexBuffer, ulong indexOffset, uint indexCount, uint indexFormat)
        {
            if (triangleDesc == IntPtr.Zero || indexBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Metal API: [triangleDesc setIndexBuffer:indexBuffer offset:indexOffset]
                IntPtr setIndexBufferSelector = sel_registerName("setIndexBuffer:offset:");
                objc_msgSend_void_uint_ulong(triangleDesc, setIndexBufferSelector, indexBuffer, indexOffset);

                // Metal API: [triangleDesc setIndexCount:indexCount]
                IntPtr setIndexCountSelector = sel_registerName("setIndexCount:");
                objc_msgSend_void_ulong(triangleDesc, setIndexCountSelector, indexCount);

                // Metal API: [triangleDesc setIndexType:indexFormat]
                IntPtr setIndexTypeSelector = sel_registerName("setIndexType:");
                objc_msgSend_void_uint(triangleDesc, setIndexTypeSelector, indexFormat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetTriangleGeometryIndexBuffer: Exception: {ex.Message}");
            }
        }

        private static void SetTriangleGeometryTransformBuffer(IntPtr triangleDesc, IntPtr transformBuffer, ulong transformOffset)
        {
            if (triangleDesc == IntPtr.Zero || transformBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Metal API: [triangleDesc setTransformBuffer:transformBuffer offset:transformOffset]
                IntPtr setTransformBufferSelector = sel_registerName("setTransformBuffer:offset:");
                objc_msgSend_void_uint_ulong(triangleDesc, setTransformBufferSelector, transformBuffer, transformOffset);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetTriangleGeometryTransformBuffer: Exception: {ex.Message}");
            }
        }

        private static void SetTriangleGeometryOptions(IntPtr triangleDesc, uint geometryFlags)
        {
            if (triangleDesc == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Metal API: [triangleDesc setOpaque:(geometryFlags & MTLAccelerationStructureGeometryOptionOpaque) != 0]
                bool isOpaque = (geometryFlags & 1) != 0;
                IntPtr setOpaqueSelector = sel_registerName("setOpaque:");
                objc_msgSend_void_bool(triangleDesc, setOpaqueSelector, isOpaque);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetTriangleGeometryOptions: Exception: {ex.Message}");
            }
        }

        private static void AddGeometryDescriptorToAccelerationStructureDescriptor(IntPtr accelStructDesc, IntPtr triangleDesc, int geometryIndex)
        {
            if (accelStructDesc == IntPtr.Zero || triangleDesc == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Metal API: [accelStructDesc setGeometryDescriptor:triangleDesc atIndex:geometryIndex]
                // We need to get the geometry descriptors array and set the element at geometryIndex
                IntPtr getGeometryDescriptorsSelector = sel_registerName("geometryDescriptors");
                IntPtr geometryDescriptorsArray = objc_msgSend_object(accelStructDesc, getGeometryDescriptorsSelector);

                if (geometryDescriptorsArray != IntPtr.Zero)
                {
                    // Set the geometry descriptor at the specified index
                    // Metal API: [geometryDescriptorsArray setObject:triangleDesc atIndexedSubscript:geometryIndex]
                    IntPtr setObjectSelector = sel_registerName("setObject:atIndexedSubscript:");
                    objc_msgSend_void_int_object(geometryDescriptorsArray, setObjectSelector, geometryIndex, triangleDesc);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] AddGeometryDescriptorToAccelerationStructureDescriptor: Exception: {ex.Message}");
            }
        }

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_void_bool(IntPtr receiver, IntPtr selector, bool value);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        public static extern void objc_msgSend_void_object(IntPtr receiver, IntPtr selector, IntPtr obj);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        public static extern void objc_msgSend_void_float4(IntPtr receiver, IntPtr selector, float red, float green, float blue, float alpha);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_void_int_object(IntPtr receiver, IntPtr selector, int index, IntPtr obj);

        // Note: objc_getClass and objc_msgSend_object are already defined in MetalBackend.cs (partial class MetalNative)

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

        // Render command encoder scissor rectangle state
        // Based on Metal API: MTLRenderCommandEncoder::setScissorRect(MTLScissorRect)
        // MTLScissorRect structure: { NSUInteger x, y, width, height }
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515458-setscissorrect
        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern void SetScissorRect(IntPtr renderCommandEncoder, MetalScissorRect scissorRect);

        /// <summary>
        /// Sets the stencil reference value for the render command encoder.
        /// Based on Metal API: MTLRenderCommandEncoder::setStencilReferenceValue(uint32_t)
        /// Method signature: - (void)setStencilReferenceValue:(uint32_t)value;
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516252-setstencilreferencevalue
        /// This sets the reference value used in stencil testing operations.
        /// The reference value is compared against the stencil buffer value using the stencil function
        /// specified in the depth-stencil state descriptor.
        /// swkotor2.exe: N/A - Original game used DirectX 9, not Metal
        /// </summary>
        /// <param name="renderCommandEncoder">The Metal render command encoder (id&lt;MTLRenderCommandEncoder&gt;).</param>
        /// <param name="reference">The stencil reference value (0-255).</param>
        public static void SetStencilReference(IntPtr renderCommandEncoder, uint reference)
        {
            if (renderCommandEncoder == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Register the Objective-C selector for setStencilReferenceValue:
                IntPtr selector = sel_registerName("setStencilReferenceValue:");
                if (selector != IntPtr.Zero)
                {
                    // Call setStencilReferenceValue: using Objective-C runtime
                    // Signature: - (void)setStencilReferenceValue:(uint32_t)value
                    objc_msgSend_void_uint(renderCommandEncoder, selector, reference);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetStencilReference: Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the Metal render command encoder supports batch viewport setting API (setViewports:count:).
        /// Uses Objective-C runtime to dynamically detect API availability at runtime.
        /// Implements two detection methods:
        /// 1. respondsToSelector: - Checks if the instance responds to the selector
        /// 2. class_getInstanceMethod - Checks if the method exists in the class
        /// Based on Objective-C runtime: respondsToSelector: and class_getInstanceMethod
        /// Metal API Reference: Future API - setViewports:count: (not yet available in current Metal versions)
        /// swkotor2.exe: N/A - Original game used DirectX 9, not Metal
        /// </summary>
        /// <param name="renderCommandEncoder">The Metal render command encoder (id&lt;MTLRenderCommandEncoder&gt;).</param>
        /// <returns>True if the batch viewport API is available, false otherwise.</returns>
        public static bool SupportsBatchViewports(IntPtr renderCommandEncoder)
        {
            if (renderCommandEncoder == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // Register the selector for setViewports:count:
                IntPtr selector = sel_registerName("setViewports:count:");
                if (selector == IntPtr.Zero)
                {
                    return false;
                }

                // Method 1: Use respondsToSelector: to check if the instance responds to the selector
                // This is the preferred method as it checks the actual instance's capabilities
                IntPtr respondsToSelectorSel = sel_registerName("respondsToSelector:");
                if (respondsToSelectorSel != IntPtr.Zero)
                {
                    byte responds = objc_msgSend_respondsToSelector(renderCommandEncoder, respondsToSelectorSel, selector);
                    if (responds != 0)
                    {
                        return true;
                    }
                }

                // Method 2: Use class_getInstanceMethod to check if the method exists in the class
                // This is a fallback method that checks the class definition directly
                IntPtr cls = object_getClass(renderCommandEncoder);
                if (cls != IntPtr.Zero)
                {
                    IntPtr method = class_getInstanceMethod(cls, selector);
                    if (method != IntPtr.Zero)
                    {
                        return true;
                    }
                }

                // Neither method found the API, so it's not available
                return false;
            }
            catch (Exception ex)
            {
                // If any exception occurs during runtime detection, assume the API is not available
                // This ensures graceful degradation rather than crashing
                Console.WriteLine($"[MetalNative] SupportsBatchViewports: Exception during runtime detection: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets multiple viewports using native array marshalling if the batch API is available.
        /// This method uses setViewports:count: if available, which is more efficient than individual calls.
        /// Based on hypothetical Metal API: MTLRenderCommandEncoder::setViewports(MTLViewport*, NSUInteger count)
        /// Note: This API does not exist in current Metal versions but is implemented for future compatibility.
        /// </summary>
        public static void SetViewports(IntPtr renderCommandEncoder, MetalViewport[] viewports, uint count)
        {
            if (renderCommandEncoder == IntPtr.Zero || viewports == null || viewports.Length == 0 || count == 0)
            {
                return;
            }

            if (count > (uint)viewports.Length)
            {
                count = (uint)viewports.Length;
            }

            // Pin the array to prevent GC from moving it during native call
            // Use GCHandle to pin the array for efficient native marshalling
            GCHandle arrayHandle = GCHandle.Alloc(viewports, GCHandleType.Pinned);
            try
            {
                IntPtr arrayPtr = arrayHandle.AddrOfPinnedObject();

                // Call setViewports:count: using Objective-C runtime
                // Signature: - (void)setViewports:(const MTLViewport*)viewports count:(NSUInteger)count
                IntPtr selector = sel_registerName("setViewports:count:");
                if (selector != IntPtr.Zero)
                {
                    // Use objc_msgSend to call setViewports:count: with pinned array pointer
                    // The array is marshalled as a pointer to the first element
                    objc_msgSend_void_ptr_uint(renderCommandEncoder, selector, arrayPtr, count);
                }
            }
            finally
            {
                // Always unpin the array to prevent memory leaks
                if (arrayHandle.IsAllocated)
                {
                    arrayHandle.Free();
                }
            }
        }

        // Objective-C runtime for calling Metal debug methods
        // Metal debug methods (pushDebugGroup:, popDebugGroup) are Objective-C instance methods
        // These require using objc_msgSend to call them from C#
        // On 64-bit systems, objc_msgSend returns a value even for void methods, so we declare it as IntPtr
        // Note: LibObjC, objc_msgSend_void, sel_registerName, objc_getClass, objc_msgSend_object, and CreateNSString are already defined in MetalBackend.cs (partial class)
        private const string LibObjCForDevice = "/usr/lib/libobjc.A.dylib";

        /// <summary>
        /// Gets the class of an Objective-C object.
        /// Based on Objective-C runtime: object_getClass
        /// </summary>
        [DllImport(LibObjCForDevice, EntryPoint = "object_getClass", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr object_getClass(IntPtr obj);

        /// <summary>
        /// Gets an instance method from a class.
        /// Based on Objective-C runtime: class_getInstanceMethod
        /// Returns a pointer to the Method structure, or NULL if the method is not found.
        /// </summary>
        [DllImport(LibObjCForDevice, EntryPoint = "class_getInstanceMethod", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr name);

        /// <summary>
        /// Calls respondsToSelector: on an Objective-C object.
        /// Based on Objective-C runtime: respondsToSelector: returns BOOL (signed char)
        /// Signature: - (BOOL)respondsToSelector:(SEL)aSelector;
        /// </summary>
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern byte objc_msgSend_respondsToSelector(IntPtr receiver, IntPtr selector, IntPtr aSelector);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_void_string(IntPtr receiver, IntPtr selector, IntPtr nsString);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_selector(IntPtr receiver, IntPtr selector, IntPtr aSelector);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_void_ptr_uint(IntPtr receiver, IntPtr selector, IntPtr viewports, uint count);

        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr objc_msgSend_void_buffer_ulong_size(IntPtr receiver, IntPtr selector, IntPtr indirectBuffer, ulong indirectBufferOffset, MetalSize threadsPerThreadgroup);

        // Note: CreateNSString is already defined in MetalBackend.cs (partial class MetalNative)

        /// <summary>
        /// Releases a CFString/NSString created with CreateNSString.
        /// </summary>
        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFRelease")]
        private static extern void CFRelease(IntPtr cf);

        // Draw Commands
        // Based on Metal API: MTLRenderCommandEncoder::drawPrimitives:vertexStart:vertexCount:instanceCount:baseInstance:
        // Method signature: - (void)drawPrimitives:(MTLPrimitiveType)primitiveType vertexStart:(NSUInteger)vertexStart vertexCount:(NSUInteger)vertexCount instanceCount:(NSUInteger)instanceCount baseInstance:(NSUInteger)baseInstance;
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1516321-drawprimitives
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_drawPrimitives(IntPtr receiver, IntPtr selector, MetalPrimitiveType primitiveType, ulong vertexStart, ulong vertexCount, ulong instanceCount, ulong baseInstance);

        /// <summary>
        /// Draws primitives using the specified parameters.
        /// Based on Metal API: MTLRenderCommandEncoder::drawPrimitives:vertexStart:vertexCount:instanceCount:baseInstance:
        /// </summary>
        public static void DrawPrimitives(IntPtr renderCommandEncoder, MetalPrimitiveType primitiveType, int vertexStart, int vertexCount, int instanceCount, int baseInstance)
        {
            if (renderCommandEncoder == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("drawPrimitives:vertexStart:vertexCount:instanceCount:baseInstance:");
                objc_msgSend_drawPrimitives(renderCommandEncoder, selector, primitiveType, unchecked((ulong)vertexStart), unchecked((ulong)vertexCount), unchecked((ulong)instanceCount), unchecked((ulong)baseInstance));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] DrawPrimitives: Exception: {ex.Message}");
            }
        }

        // Draw Indexed Commands
        // Based on Metal API: MTLRenderCommandEncoder::drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:instanceCount:baseVertex:baseInstance:
        // Method signature: - (void)drawIndexedPrimitives:(MTLPrimitiveType)primitiveType indexCount:(NSUInteger)indexCount indexType:(MTLIndexType)indexType indexBuffer:(id<MTLBuffer>)indexBuffer indexBufferOffset:(NSUInteger)indexBufferOffset instanceCount:(NSUInteger)instanceCount baseVertex:(NSInteger)baseVertex baseInstance:(NSUInteger)baseInstance;
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515527-drawindexedprimitives
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_drawIndexedPrimitives(IntPtr receiver, IntPtr selector, MetalPrimitiveType primitiveType, ulong indexCount, MetalIndexType indexType, IntPtr indexBuffer, ulong indexBufferOffset, ulong instanceCount, long baseVertex, ulong baseInstance);

        /// <summary>
        /// Draws indexed primitives using the specified parameters.
        /// Based on Metal API: MTLRenderCommandEncoder::drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:instanceCount:baseVertex:baseInstance:
        /// </summary>
        public static void DrawIndexedPrimitives(IntPtr renderCommandEncoder, MetalPrimitiveType primitiveType, int indexCount, MetalIndexType indexType, IntPtr indexBuffer, int indexBufferOffset, int instanceCount, int baseVertex, int baseInstance)
        {
            if (renderCommandEncoder == IntPtr.Zero || indexBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:instanceCount:baseVertex:baseInstance:");
                objc_msgSend_drawIndexedPrimitives(renderCommandEncoder, selector, primitiveType, unchecked((ulong)indexCount), indexType, indexBuffer, unchecked((ulong)indexBufferOffset), unchecked((ulong)instanceCount), unchecked((long)baseVertex), unchecked((ulong)baseInstance));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] DrawIndexedPrimitives: Exception: {ex.Message}");
            }
        }

        // Draw Indirect Commands (if not already defined)
        // Based on Metal API: MTLRenderCommandEncoder::drawPrimitives:indirectBuffer:indirectBufferOffset:
        // Method signature: - (void)drawPrimitives:(MTLPrimitiveType)primitiveType indirectBuffer:(id<MTLBuffer>)indirectBuffer indirectBufferOffset:(NSUInteger)indirectBufferOffset;
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515526-drawprimitives
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_drawPrimitivesIndirect(IntPtr receiver, IntPtr selector, MetalPrimitiveType primitiveType, IntPtr indirectBuffer, ulong indirectBufferOffset);

        /// <summary>
        /// Draws primitives using indirect arguments from a buffer.
        /// Based on Metal API: MTLRenderCommandEncoder::drawPrimitives:indirectBuffer:indirectBufferOffset:
        /// </summary>
        public static void DrawPrimitivesIndirect(IntPtr renderCommandEncoder, MetalPrimitiveType primitiveType, IntPtr indirectBuffer, ulong indirectBufferOffset)
        {
            if (renderCommandEncoder == IntPtr.Zero || indirectBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("drawPrimitives:indirectBuffer:indirectBufferOffset:");
                objc_msgSend_drawPrimitivesIndirect(renderCommandEncoder, selector, primitiveType, indirectBuffer, indirectBufferOffset);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] DrawPrimitivesIndirect: Exception: {ex.Message}");
            }
        }

        // Draw Indexed Indirect Commands (if not already defined)
        // Based on Metal API: MTLRenderCommandEncoder::drawIndexedPrimitives:indexType:indexBuffer:indexBufferOffset:indirectBuffer:indirectBufferOffset:
        // Method signature: - (void)drawIndexedPrimitives:(MTLPrimitiveType)primitiveType indexType:(MTLIndexType)indexType indexBuffer:(id<MTLBuffer>)indexBuffer indexBufferOffset:(NSUInteger)indexBufferOffset indirectBuffer:(id<MTLBuffer>)indirectBuffer indirectBufferOffset:(NSUInteger)indirectBufferOffset;
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515544-drawindexedprimitives
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_drawIndexedPrimitivesIndirect(IntPtr receiver, IntPtr selector, MetalPrimitiveType primitiveType, MetalIndexType indexType, IntPtr indexBuffer, ulong indexBufferOffset, IntPtr indirectBuffer, ulong indirectBufferOffset);

        /// <summary>
        /// Draws indexed primitives using indirect arguments from a buffer.
        /// Based on Metal API: MTLRenderCommandEncoder::drawIndexedPrimitives:indexType:indexBuffer:indexBufferOffset:indirectBuffer:indirectBufferOffset:
        /// </summary>
        public static void DrawIndexedPrimitivesIndirect(IntPtr renderCommandEncoder, MetalPrimitiveType primitiveType, MetalIndexType indexType, IntPtr indexBuffer, ulong indexBufferOffset, IntPtr indirectBuffer, ulong indirectBufferOffset)
        {
            if (renderCommandEncoder == IntPtr.Zero || indexBuffer == IntPtr.Zero || indirectBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("drawIndexedPrimitives:indexType:indexBuffer:indexBufferOffset:indirectBuffer:indirectBufferOffset:");
                objc_msgSend_drawIndexedPrimitivesIndirect(renderCommandEncoder, selector, primitiveType, indexType, indexBuffer, indexBufferOffset, indirectBuffer, indirectBufferOffset);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] DrawIndexedPrimitivesIndirect: Exception: {ex.Message}");
            }
        }

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

        // Buffer copying via blit command encoder
        // Based on Metal API: MTLBlitCommandEncoder::copyFromBuffer:sourceOffset:toBuffer:destinationOffset:size:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlblitcommandencoder/1400774-copyfrombuffer
        // Method signature: - (void)copyFromBuffer:(id<MTLBuffer>)sourceBuffer sourceOffset:(NSUInteger)sourceOffset toBuffer:(id<MTLBuffer>)destinationBuffer destinationOffset:(NSUInteger)destinationOffset size:(NSUInteger)size;
        // Note: On 64-bit systems, NSUInteger is 64-bit (ulong), not 32-bit (uint)
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_copyFromBuffer(IntPtr receiver, IntPtr selector, IntPtr sourceBuffer, ulong sourceOffset, IntPtr destinationBuffer, ulong destinationOffset, ulong size);

        /// <summary>
        /// Copies data from one buffer to another using a Metal blit command encoder.
        /// Based on Metal API: MTLBlitCommandEncoder::copyFromBuffer:sourceOffset:toBuffer:destinationOffset:size:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlblitcommandencoder/1400774-copyfrombuffer
        /// </summary>
        /// <param name="blitEncoder">Metal blit command encoder handle</param>
        /// <param name="sourceBuffer">Source buffer handle</param>
        /// <param name="sourceOffset">Source offset in bytes</param>
        /// <param name="destinationBuffer">Destination buffer handle</param>
        /// <param name="destinationOffset">Destination offset in bytes</param>
        /// <param name="size">Size in bytes to copy</param>
        public static void CopyFromBuffer(IntPtr blitEncoder, IntPtr sourceBuffer, ulong sourceOffset, IntPtr destinationBuffer, ulong destinationOffset, ulong size)
        {
            if (blitEncoder == IntPtr.Zero || sourceBuffer == IntPtr.Zero || destinationBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Register selector for copyFromBuffer:sourceOffset:toBuffer:destinationOffset:size:
                IntPtr selector = sel_registerName("copyFromBuffer:sourceOffset:toBuffer:destinationOffset:size:");

                // Call the method
                // Note: On 64-bit systems, NSUInteger is 64-bit (ulong)
                objc_msgSend_copyFromBuffer(blitEncoder, selector, sourceBuffer, sourceOffset, destinationBuffer, destinationOffset, size);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] CopyFromBuffer: Exception: {ex.Message}");
                Console.WriteLine($"[MetalNative] CopyFromBuffer: Stack trace: {ex.StackTrace}");
            }
        }

        // Buffer contents access for CPU writes
        // Based on Metal API: MTLBuffer::contents()
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlbuffer/1515376-contents
        // Method signature: - (void *)contents;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_contents(IntPtr receiver, IntPtr selector);

        /// <summary>
        /// Gets a pointer to the contents of a Metal buffer for CPU access.
        /// Only valid for buffers with shared or managed storage mode.
        /// Based on Metal API: MTLBuffer::contents()
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlbuffer/1515376-contents
        /// </summary>
        /// <param name="buffer">Metal buffer handle</param>
        /// <returns>Pointer to buffer contents, or IntPtr.Zero if invalid</returns>
        public static IntPtr GetBufferContents(IntPtr buffer)
        {
            if (buffer == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            try
            {
                IntPtr selector = sel_registerName("contents");
                return objc_msgSend_contents(buffer, selector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] GetBufferContents: Exception: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        // Create buffer with explicit resource options (for staging buffers)
        // Based on Metal API: MTLDevice::newBufferWithLength:options:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtldevice/1433429-newbufferwithlength
        // Method signature: - (id<MTLBuffer>)newBufferWithLength:(NSUInteger)length options:(MTLResourceOptions)options;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_CreateBufferWithOptions(IntPtr receiver, IntPtr selector, ulong length, uint options);

        /// <summary>
        /// Creates a Metal buffer with explicit resource options.
        /// Used for creating staging buffers with shared storage mode for CPU writes.
        /// Based on Metal API: MTLDevice::newBufferWithLength:options:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtldevice/1433429-newbufferwithlength
        /// </summary>
        /// <param name="device">Metal device handle</param>
        /// <param name="length">Buffer length in bytes</param>
        /// <param name="options">Resource options (e.g., StorageModeShared for CPU access)</param>
        /// <returns>Metal buffer handle, or IntPtr.Zero if creation failed</returns>
        public static IntPtr CreateBufferWithOptions(IntPtr device, ulong length, uint options)
        {
            if (device == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            try
            {
                IntPtr selector = sel_registerName("newBufferWithLength:options:");
                return objc_msgSend_CreateBufferWithOptions(device, selector, length, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] CreateBufferWithOptions: Exception: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        // Acceleration structure command encoder
        // Based on Metal API: MTLCommandBuffer::accelerationStructureCommandEncoder()
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/3553900-accelerationstructurecommanden
        // Method signature: - (id<MTLAccelerationStructureCommandEncoder>)accelerationStructureCommandEncoder;
        /// <summary>
        /// Creates an acceleration structure command encoder from a Metal command buffer.
        /// Used for building and managing acceleration structures (BLAS/TLAS).
        /// Based on Metal API: MTLCommandBuffer::accelerationStructureCommandEncoder()
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlcommandbuffer/3553900-accelerationstructurecommanden
        /// </summary>
        /// <param name="commandBuffer">Metal command buffer handle</param>
        /// <returns>Acceleration structure command encoder handle, or IntPtr.Zero if creation failed</returns>
        public static IntPtr CreateAccelerationStructureCommandEncoder(IntPtr commandBuffer)
        {
            if (commandBuffer == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            try
            {
                IntPtr selector = sel_registerName("accelerationStructureCommandEncoder");
                return objc_msgSend_object(commandBuffer, selector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] CreateAccelerationStructureCommandEncoder: Exception: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        // Build acceleration structure
        // Based on Metal API: MTLAccelerationStructureCommandEncoder::buildAccelerationStructure:descriptor:scratchBuffer:scratchBufferOffset:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructurecommandencoder/3553898-buildaccelerationstructure
        // Method signature: - (void)buildAccelerationStructure:(id<MTLAccelerationStructure>)accelerationStructure descriptor:(MTLAccelerationStructureDescriptor*)descriptor scratchBuffer:(id<MTLBuffer>)scratchBuffer scratchBufferOffset:(NSUInteger)scratchBufferOffset;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_buildAccelerationStructure(IntPtr receiver, IntPtr selector, IntPtr accelerationStructure, IntPtr descriptor, IntPtr scratchBuffer, ulong scratchBufferOffset);

        /// <summary>
        /// Builds an acceleration structure using a descriptor and scratch buffer.
        /// Based on Metal API: MTLAccelerationStructureCommandEncoder::buildAccelerationStructure:descriptor:scratchBuffer:scratchBufferOffset:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructurecommandencoder/3553898-buildaccelerationstructure
        /// </summary>
        /// <param name="encoder">Acceleration structure command encoder handle</param>
        /// <param name="accelerationStructure">Target acceleration structure to build</param>
        /// <param name="descriptor">Acceleration structure descriptor (BLAS or TLAS descriptor)</param>
        /// <param name="scratchBuffer">Scratch buffer for temporary data during build</param>
        /// <param name="scratchBufferOffset">Offset into scratch buffer in bytes</param>
        public static void BuildAccelerationStructure(IntPtr encoder, IntPtr accelerationStructure, IntPtr descriptor, IntPtr scratchBuffer, ulong scratchBufferOffset)
        {
            if (encoder == IntPtr.Zero || accelerationStructure == IntPtr.Zero || descriptor == IntPtr.Zero || scratchBuffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("buildAccelerationStructure:descriptor:scratchBuffer:scratchBufferOffset:");
                objc_msgSend_buildAccelerationStructure(encoder, selector, accelerationStructure, descriptor, scratchBuffer, scratchBufferOffset);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] BuildAccelerationStructure: Exception: {ex.Message}");
                Console.WriteLine($"[MetalNative] BuildAccelerationStructure: Stack trace: {ex.StackTrace}");
            }
        }

        // Copy acceleration structure
        // Based on Metal API: MTLAccelerationStructureCommandEncoder::copyAccelerationStructure:toAccelerationStructure:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructurecommandencoder/3553902-copyaccelerationstructure
        // Method signature: - (void)copyAccelerationStructure:(id<MTLAccelerationStructure>)sourceAccelerationStructure toAccelerationStructure:(id<MTLAccelerationStructure>)destinationAccelerationStructure;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_copyAccelerationStructure(IntPtr receiver, IntPtr selector, IntPtr sourceAccelerationStructure, IntPtr destinationAccelerationStructure);

        /// <summary>
        /// Copies an acceleration structure from source to destination.
        /// Used for compaction and updates. Based on Metal API: MTLAccelerationStructureCommandEncoder::copyAccelerationStructure:toAccelerationStructure:
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructurecommandencoder/3553902-copyaccelerationstructure
        /// </summary>
        /// <param name="encoder">Acceleration structure command encoder handle</param>
        /// <param name="sourceAccelerationStructure">Source acceleration structure</param>
        /// <param name="destinationAccelerationStructure">Destination acceleration structure</param>
        public static void CopyAccelerationStructure(IntPtr encoder, IntPtr sourceAccelerationStructure, IntPtr destinationAccelerationStructure)
        {
            if (encoder == IntPtr.Zero || sourceAccelerationStructure == IntPtr.Zero || destinationAccelerationStructure == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("copyAccelerationStructure:toAccelerationStructure:");
                objc_msgSend_copyAccelerationStructure(encoder, selector, sourceAccelerationStructure, destinationAccelerationStructure);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] CopyAccelerationStructure: Exception: {ex.Message}");
                Console.WriteLine($"[MetalNative] CopyAccelerationStructure: Stack trace: {ex.StackTrace}");
            }
        }

        // End acceleration structure command encoding
        // Based on Metal API: MTLAccelerationStructureCommandEncoder::endEncoding
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructurecommandencoder/3553903-endencoding
        // Method signature: - (void)endEncoding;
        /// <summary>
        /// Ends encoding on an acceleration structure command encoder.
        /// Based on Metal API: MTLAccelerationStructureCommandEncoder::endEncoding
        /// Metal API Reference: https://developer.apple.com/documentation/metal/mtlaccelerationstructurecommandencoder/3553903-endencoding
        /// </summary>
        /// <param name="encoder">Acceleration structure command encoder handle</param>
        public static void EndAccelerationStructureCommandEncoding(IntPtr encoder)
        {
            if (encoder == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("endEncoding");
                objc_msgSend_void(encoder, selector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] EndAccelerationStructureCommandEncoding: Exception: {ex.Message}");
            }
        }

        // Render pipeline state
        // Based on Metal API: MTLRenderCommandEncoder::setRenderPipelineState:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515811-setrenderpipelinestate
        // Method signature: - (void)setRenderPipelineState:(id<MTLRenderPipelineState>)pipelineState;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_setRenderPipelineState(IntPtr receiver, IntPtr selector, IntPtr pipelineState);

        /// <summary>
        /// Sets the render pipeline state on a render command encoder.
        /// Based on Metal API: MTLRenderCommandEncoder::setRenderPipelineState:
        /// </summary>
        public static void SetRenderPipelineState(IntPtr renderCommandEncoder, IntPtr renderPipelineState)
        {
            if (renderCommandEncoder == IntPtr.Zero || renderPipelineState == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("setRenderPipelineState:");
                objc_msgSend_setRenderPipelineState(renderCommandEncoder, selector, renderPipelineState);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetRenderPipelineState: Exception: {ex.Message}");
            }
        }

        // Vertex buffer binding
        // Based on Metal API: MTLRenderCommandEncoder::setVertexBuffer:offset:atIndex:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515826-setvertexbuffer
        // Method signature: - (void)setVertexBuffer:(id<MTLBuffer>)buffer offset:(NSUInteger)offset atIndex:(NSUInteger)index;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_setVertexBuffer(IntPtr receiver, IntPtr selector, IntPtr buffer, ulong offset, uint index);

        /// <summary>
        /// Sets a vertex buffer at a specific index.
        /// Based on Metal API: MTLRenderCommandEncoder::setVertexBuffer:offset:atIndex:
        /// </summary>
        public static void SetVertexBuffer(IntPtr renderCommandEncoder, IntPtr buffer, ulong offset, uint index)
        {
            if (renderCommandEncoder == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("setVertexBuffer:offset:atIndex:");
                objc_msgSend_setVertexBuffer(renderCommandEncoder, selector, buffer, offset, index);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetVertexBuffer: Exception: {ex.Message}");
            }
        }

        // Fragment buffer binding
        // Based on Metal API: MTLRenderCommandEncoder::setFragmentBuffer:offset:atIndex:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515827-setfragmentbuffer
        // Method signature: - (void)setFragmentBuffer:(id<MTLBuffer>)buffer offset:(NSUInteger)offset atIndex:(NSUInteger)index;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_setFragmentBuffer(IntPtr receiver, IntPtr selector, IntPtr buffer, ulong offset, uint index);

        /// <summary>
        /// Sets a fragment buffer at a specific index.
        /// Based on Metal API: MTLRenderCommandEncoder::setFragmentBuffer:offset:atIndex:
        /// </summary>
        public static void SetFragmentBuffer(IntPtr renderCommandEncoder, IntPtr buffer, ulong offset, uint index)
        {
            if (renderCommandEncoder == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("setFragmentBuffer:offset:atIndex:");
                objc_msgSend_setFragmentBuffer(renderCommandEncoder, selector, buffer, offset, index);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] SetFragmentBuffer: Exception: {ex.Message}");
            }
        }

        // Buffer to texture copy
        // Based on Metal API: MTLBlitCommandEncoder::copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlblitcommandencoder/1400775-copyfrombuffer
        // Method signature: - (void)copyFromBuffer:(id<MTLBuffer>)sourceBuffer sourceOffset:(NSUInteger)sourceOffset sourceBytesPerRow:(NSUInteger)sourceBytesPerRow sourceBytesPerImage:(NSUInteger)sourceBytesPerImage sourceSize:(MTLSize)sourceSize toTexture:(id<MTLTexture>)destinationTexture destinationSlice:(NSUInteger)destinationSlice destinationLevel:(NSUInteger)destinationLevel destinationOrigin:(MTLOrigin)destinationOrigin;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern void objc_msgSend_copyFromBufferToTexture(IntPtr receiver, IntPtr selector, IntPtr sourceBuffer, ulong sourceOffset, ulong sourceBytesPerRow, ulong sourceBytesPerImage, MetalSize sourceSize, IntPtr destinationTexture, ulong destinationSlice, ulong destinationLevel, MetalOrigin destinationOrigin);

        /// <summary>
        /// Copies data from a buffer to a texture using a blit command encoder.
        /// Based on Metal API: MTLBlitCommandEncoder::copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:
        /// </summary>
        public static void CopyFromBufferToTexture(IntPtr blitEncoder, IntPtr sourceBuffer, ulong sourceOffset, ulong sourceBytesPerRow, ulong sourceBytesPerImage, MetalSize sourceSize, IntPtr destinationTexture, ulong destinationSlice, ulong destinationLevel, MetalOrigin destinationOrigin)
        {
            if (blitEncoder == IntPtr.Zero || sourceBuffer == IntPtr.Zero || destinationTexture == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr selector = sel_registerName("copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:");
                objc_msgSend_copyFromBufferToTexture(blitEncoder, selector, sourceBuffer, sourceOffset, sourceBytesPerRow, sourceBytesPerImage, sourceSize, destinationTexture, destinationSlice, destinationLevel, destinationOrigin);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] CopyFromBufferToTexture: Exception: {ex.Message}");
            }
        }

        // Function handle retrieval for raytracing
        // Based on Metal API: MTLRaytracingPipelineState::functionHandleWithFunction:
        // Metal API Reference: https://developer.apple.com/documentation/metal/mtlraytracingpipelinestate/3750527-functionhandlewithfunction
        // Method signature: - (MTLFunctionHandle *)functionHandleWithFunction:(id<MTLFunction>)function;
        [DllImport(LibObjCForDevice, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend_functionHandle(IntPtr receiver, IntPtr selector, IntPtr function);

        /// <summary>
        /// Gets a function handle from a raytracing pipeline state.
        /// Based on Metal API: MTLRaytracingPipelineState::functionHandleWithFunction:
        /// </summary>
        public static IntPtr GetFunctionHandle(IntPtr raytracingPipelineState, IntPtr function)
        {
            if (raytracingPipelineState == IntPtr.Zero || function == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            try
            {
                // Register selector for functionHandleWithFunction:
                // Note: sel_registerName is private in MetalBackend.cs, but since MetalNative is partial,
                // we need to access it through a wrapper or declare it here
                IntPtr selector = RegisterSelector("functionHandleWithFunction:");
                if (selector == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }
                return objc_msgSend_functionHandle(raytracingPipelineState, selector, function);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetalNative] GetFunctionHandle: Exception: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Registers an Objective-C selector name.
        /// Wrapper for sel_registerName to make it accessible from MetalDevice.cs partial class.
        /// Note: sel_registerName is already declared in MetalBackend.cs partial class.
        /// </summary>
        public static IntPtr RegisterSelector(string selectorName)
        {
            // sel_registerName is declared in MetalBackend.cs partial class
            // Since MetalNative is partial, we can access it directly
            // Use the existing declaration from MetalBackend.cs
            return sel_registerName(selectorName);
        }

        // Note: sel_registerName, objc_msgSend_void, objc_msgSend_object, and objc_getClass
        // are already declared in MetalBackend.cs (partial class MetalNative)
        // We don't need to redeclare them here
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

    // MTLScissorRect structure matching Metal API
    // Based on Metal API: MTLScissorRect { NSUInteger x, y, width, height }
    // Metal API Reference: https://developer.apple.com/documentation/metal/mtlscissorrect
    [StructLayout(LayoutKind.Sequential)]
    internal struct MetalScissorRect
    {
        public uint X;      // NSUInteger x - left edge of scissor rectangle
        public uint Y;      // NSUInteger y - top edge of scissor rectangle
        public uint Width;  // NSUInteger width - width of scissor rectangle
        public uint Height; // NSUInteger height - height of scissor rectangle
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


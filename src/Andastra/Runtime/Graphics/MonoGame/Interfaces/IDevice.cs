using System;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Interfaces
{
    /// <summary>
    /// Graphics device abstraction following NVRHI-style patterns.
    /// Provides a unified interface across Vulkan, D3D12, and D3D11.
    ///
    /// This design follows the industry-standard pattern used by:
    /// - NVRHI (NVIDIA Rendering Hardware Interface)
    /// - Diligent Engine
    /// - BGFX
    /// </summary>
    public interface IDevice : IDisposable
    {
        /// <summary>
        /// Gets the device capabilities and feature support.
        /// </summary>
        GraphicsCapabilities Capabilities { get; }

        /// <summary>
        /// Gets the active graphics backend.
        /// </summary>
        GraphicsBackend Backend { get; }

        /// <summary>
        /// Whether the device is valid and ready for rendering.
        /// </summary>
        bool IsValid { get; }

        #region Resource Creation

        /// <summary>
        /// Creates a texture resource.
        /// </summary>
        ITexture CreateTexture(TextureDesc desc);

        /// <summary>
        /// Creates a buffer resource (vertex, index, constant, structured).
        /// </summary>
        IBuffer CreateBuffer(BufferDesc desc);

        /// <summary>
        /// Creates a sampler state.
        /// </summary>
        ISampler CreateSampler(SamplerDesc desc);

        /// <summary>
        /// Creates a shader module from bytecode.
        /// </summary>
        IShader CreateShader(ShaderDesc desc);

        /// <summary>
        /// Creates a graphics pipeline state object.
        /// </summary>
        IGraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc, IFramebuffer framebuffer);

        /// <summary>
        /// Creates a compute pipeline state object.
        /// </summary>
        IComputePipeline CreateComputePipeline(ComputePipelineDesc desc);

        /// <summary>
        /// Creates a framebuffer (render target collection).
        /// </summary>
        IFramebuffer CreateFramebuffer(FramebufferDesc desc);

        /// <summary>
        /// Creates a binding layout describing shader resource bindings.
        /// </summary>
        IBindingLayout CreateBindingLayout(BindingLayoutDesc desc);

        /// <summary>
        /// Creates a binding set with actual resource bindings.
        /// </summary>
        IBindingSet CreateBindingSet(IBindingLayout layout, BindingSetDesc desc);

        /// <summary>
        /// Creates a command list for recording rendering commands.
        /// </summary>
        ICommandList CreateCommandList(CommandListType type = CommandListType.Graphics);

        /// <summary>
        /// Creates a handle for a native texture (for swap chain integration).
        /// </summary>
        ITexture CreateHandleForNativeTexture(IntPtr nativeHandle, TextureDesc desc);

        #endregion

        #region Raytracing Resources

        /// <summary>
        /// Creates a bottom-level acceleration structure (BLAS).
        /// </summary>
        IAccelStruct CreateAccelStruct(AccelStructDesc desc);

        /// <summary>
        /// Creates a raytracing pipeline state object.
        /// </summary>
        IRaytracingPipeline CreateRaytracingPipeline(RaytracingPipelineDesc desc);

        #endregion

        #region Command Execution

        /// <summary>
        /// Executes a command list.
        /// </summary>
        void ExecuteCommandList(ICommandList commandList);

        /// <summary>
        /// Executes multiple command lists.
        /// </summary>
        void ExecuteCommandLists(ICommandList[] commandLists);

        /// <summary>
        /// Waits for all GPU operations to complete.
        /// </summary>
        void WaitIdle();

        /// <summary>
        /// Signals a fence from the GPU.
        /// </summary>
        void Signal(IFence fence, ulong value);

        /// <summary>
        /// Waits for a fence value on the CPU.
        /// </summary>
        void WaitFence(IFence fence, ulong value);

        #endregion

        #region Queries

        /// <summary>
        /// Gets the required alignment for constant buffer offsets.
        /// </summary>
        int GetConstantBufferAlignment();

        /// <summary>
        /// Gets the required alignment for texture data.
        /// </summary>
        int GetTextureAlignment();

        /// <summary>
        /// Checks if a format is supported for a given usage.
        /// </summary>
        bool IsFormatSupported(TextureFormat format, TextureUsage usage);

        /// <summary>
        /// Gets the current frame index (for multi-buffering).
        /// </summary>
        int GetCurrentFrameIndex();

        #endregion
    }

    /// <summary>
    /// Texture resource interface.
    /// </summary>
    public interface ITexture : IDisposable
    {
        TextureDesc Desc { get; }
        IntPtr NativeHandle { get; }
    }

    /// <summary>
    /// Buffer resource interface.
    /// </summary>
    public interface IBuffer : IDisposable
    {
        BufferDesc Desc { get; }
        IntPtr NativeHandle { get; }
    }

    /// <summary>
    /// Sampler state interface.
    /// </summary>
    public interface ISampler : IDisposable
    {
        SamplerDesc Desc { get; }
    }

    /// <summary>
    /// Shader module interface.
    /// </summary>
    public interface IShader : IDisposable
    {
        ShaderDesc Desc { get; }
        ShaderType Type { get; }
    }

    /// <summary>
    /// Graphics pipeline state interface.
    /// </summary>
    public interface IGraphicsPipeline : IDisposable
    {
        GraphicsPipelineDesc Desc { get; }
    }

    /// <summary>
    /// Compute pipeline state interface.
    /// </summary>
    public interface IComputePipeline : IDisposable
    {
        ComputePipelineDesc Desc { get; }
    }

    /// <summary>
    /// Framebuffer interface.
    /// </summary>
    public interface IFramebuffer : IDisposable
    {
        FramebufferDesc Desc { get; }
        FramebufferInfo GetInfo();
    }

    /// <summary>
    /// Binding layout interface.
    /// </summary>
    public interface IBindingLayout : IDisposable
    {
        BindingLayoutDesc Desc { get; }
    }

    /// <summary>
    /// Binding set interface.
    /// </summary>
    public interface IBindingSet : IDisposable
    {
        IBindingLayout Layout { get; }
    }

    /// <summary>
    /// Acceleration structure interface for raytracing.
    /// </summary>
    public interface IAccelStruct : IDisposable
    {
        AccelStructDesc Desc { get; }
        bool IsTopLevel { get; }
        ulong DeviceAddress { get; }
    }

    /// <summary>
    /// Raytracing pipeline interface.
    /// </summary>
    public interface IRaytracingPipeline : IDisposable
    {
        RaytracingPipelineDesc Desc { get; }

        /// <summary>
        /// Gets the shader identifier for a shader or hit group in the pipeline.
        /// Shader identifiers are opaque handles used in the shader binding table.
        /// </summary>
        /// <param name="exportName">The export name of the shader or hit group (e.g., "ShadowRayGen", "ShadowMiss", "ShadowHitGroup").</param>
        /// <returns>Shader identifier bytes (typically 32 bytes for D3D12, variable for Vulkan). Returns null if the export name is not found.</returns>
        byte[] GetShaderIdentifier(string exportName);
    }

    /// <summary>
    /// Fence for CPU-GPU synchronization.
    /// </summary>
    public interface IFence : IDisposable
    {
        ulong CompletedValue { get; }
    }

    /// <summary>
    /// Shader types.
    /// </summary>
    public enum ShaderType
    {
        Vertex,
        Hull,
        Domain,
        Geometry,
        Pixel,
        Compute,
        // Raytracing shaders
        RayGeneration,
        Miss,
        ClosestHit,
        AnyHit,
        Intersection,
        Callable
    }

    /// <summary>
    /// Command list types.
    /// </summary>
    public enum CommandListType
    {
        Graphics,
        Compute,
        Copy
    }

    #region Descriptor Structs (NVRHI-style)

    /// <summary>
    /// Texture descriptor following NVRHI patterns.
    /// </summary>
    public struct TextureDesc
    {
        public int Width;
        public int Height;
        public int Depth;
        public int ArraySize;
        public int MipLevels;
        public int SampleCount;
        public TextureFormat Format;
        public TextureDimension Dimension;
        public TextureUsage Usage;
        public ResourceState InitialState;
        public bool KeepInitialState;
        public ClearValue ClearValue;
        public string DebugName;

        public static TextureDesc Create2D(int width, int height, TextureFormat format, string debugName = null)
        {
            return new TextureDesc
            {
                Width = width,
                Height = height,
                Depth = 1,
                ArraySize = 1,
                MipLevels = 1,
                SampleCount = 1,
                Format = format,
                Dimension = TextureDimension.Texture2D,
                Usage = TextureUsage.ShaderResource,
                InitialState = ResourceState.Common,
                DebugName = debugName
            };
        }

        public TextureDesc SetIsRenderTarget(bool value)
        {
            if (value) Usage |= TextureUsage.RenderTarget;
            return this;
        }

        public TextureDesc SetIsDepthStencil(bool value)
        {
            if (value) Usage |= TextureUsage.DepthStencil;
            return this;
        }

        public TextureDesc SetIsUAV(bool value)
        {
            if (value) Usage |= TextureUsage.UnorderedAccess;
            return this;
        }
    }

    /// <summary>
    /// Buffer descriptor following NVRHI patterns.
    /// </summary>
    public struct BufferDesc
    {
        public int ByteSize;
        public int StructStride;
        public BufferUsageFlags Usage;
        public ResourceState InitialState;
        public bool KeepInitialState;
        public bool CanHaveRawViews;
        public bool IsAccelStructBuildInput;
        public BufferHeapType HeapType;
        public string DebugName;

        public BufferDesc SetByteSize(int size) { ByteSize = size; return this; }
        public BufferDesc SetStructStride(int stride) { StructStride = stride; return this; }
        public BufferDesc SetIsVertexBuffer(bool v) { if (v) Usage |= BufferUsageFlags.VertexBuffer; return this; }
        public BufferDesc SetIsIndexBuffer(bool v) { if (v) Usage |= BufferUsageFlags.IndexBuffer; return this; }
        public BufferDesc SetIsConstantBuffer(bool v) { if (v) Usage |= BufferUsageFlags.ConstantBuffer; return this; }
        public BufferDesc SetCanHaveRawViews(bool v) { CanHaveRawViews = v; return this; }
        public BufferDesc SetIsAccelStructBuildInput(bool v) { IsAccelStructBuildInput = v; return this; }
        public BufferDesc SetKeepInitialState(bool v) { KeepInitialState = v; return this; }
        public BufferDesc SetInitialState(ResourceState state) { InitialState = state; return this; }
        /// <summary>
        /// Sets the heap type for this buffer. For D3D12 backend, this determines memory placement:
        /// - Default: GPU-accessible only (D3D12_HEAP_TYPE_DEFAULT)
        /// - Upload: CPU-writable, GPU-readable (D3D12_HEAP_TYPE_UPLOAD) - use for staging/dynamic buffers
        /// - Readback: CPU-readable, GPU-writable (D3D12_HEAP_TYPE_READBACK) - use for reading back GPU results
        /// </summary>
        public BufferDesc SetHeapType(BufferHeapType heapType) { HeapType = heapType; return this; }
        public BufferDesc SetDebugName(string name) { DebugName = name; return this; }
    }

    /// <summary>
    /// Sampler descriptor.
    /// </summary>
    public struct SamplerDesc
    {
        public SamplerFilter MinFilter;
        public SamplerFilter MagFilter;
        public SamplerFilter MipFilter;
        public SamplerAddressMode AddressU;
        public SamplerAddressMode AddressV;
        public SamplerAddressMode AddressW;
        public float MipLodBias;
        public int MaxAnisotropy;
        public CompareFunc CompareFunc;
        public float MinLod;
        public float MaxLod;
        public float[] BorderColor;
    }

    /// <summary>
    /// Shader descriptor.
    /// </summary>
    public struct ShaderDesc
    {
        public ShaderType Type;
        public byte[] Bytecode;
        public string EntryPoint;
        public string DebugName;
    }

    /// <summary>
    /// Graphics pipeline descriptor following NVRHI patterns.
    /// </summary>
    public struct GraphicsPipelineDesc
    {
        public IShader VertexShader;
        public IShader HullShader;
        public IShader DomainShader;
        public IShader GeometryShader;
        public IShader PixelShader;
        public InputLayoutDesc InputLayout;
        public BlendStateDesc BlendState;
        public RasterStateDesc RasterState;
        public DepthStencilStateDesc DepthStencilState;
        public PrimitiveTopology PrimitiveTopology;
        public IBindingLayout[] BindingLayouts;

        public GraphicsPipelineDesc SetVertexShader(IShader s) { VertexShader = s; return this; }
        public GraphicsPipelineDesc SetPixelShader(IShader s) { PixelShader = s; return this; }
        public GraphicsPipelineDesc SetInputLayout(InputLayoutDesc l) { InputLayout = l; return this; }
        public GraphicsPipelineDesc AddBindingLayout(IBindingLayout l)
        {
            if (BindingLayouts == null) BindingLayouts = new IBindingLayout[] { l };
            else
            {
                var newArr = new IBindingLayout[BindingLayouts.Length + 1];
                Array.Copy(BindingLayouts, newArr, BindingLayouts.Length);
                newArr[BindingLayouts.Length] = l;
                BindingLayouts = newArr;
            }
            return this;
        }
    }

    /// <summary>
    /// Compute pipeline descriptor.
    /// </summary>
    public struct ComputePipelineDesc
    {
        public IShader ComputeShader;
        public IBindingLayout[] BindingLayouts;
    }

    /// <summary>
    /// Framebuffer descriptor following NVRHI patterns.
    /// </summary>
    public struct FramebufferDesc
    {
        public FramebufferAttachment[] ColorAttachments;
        public FramebufferAttachment DepthAttachment;

        public FramebufferDesc AddColorAttachment(ITexture texture, int mipLevel = 0, int arraySlice = 0)
        {
            var attachment = new FramebufferAttachment { Texture = texture, MipLevel = mipLevel, ArraySlice = arraySlice };
            if (ColorAttachments == null) ColorAttachments = new FramebufferAttachment[] { attachment };
            else
            {
                var newArr = new FramebufferAttachment[ColorAttachments.Length + 1];
                Array.Copy(ColorAttachments, newArr, ColorAttachments.Length);
                newArr[ColorAttachments.Length] = attachment;
                ColorAttachments = newArr;
            }
            return this;
        }

        public FramebufferDesc SetDepthAttachment(ITexture texture, int mipLevel = 0, int arraySlice = 0)
        {
            DepthAttachment = new FramebufferAttachment { Texture = texture, MipLevel = mipLevel, ArraySlice = arraySlice };
            return this;
        }
    }

    /// <summary>
    /// Framebuffer attachment.
    /// </summary>
    public struct FramebufferAttachment
    {
        public ITexture Texture;
        public int MipLevel;
        public int ArraySlice;
    }

    /// <summary>
    /// Framebuffer info for pipeline compatibility.
    /// </summary>
    public struct FramebufferInfo
    {
        public TextureFormat[] ColorFormats;
        public TextureFormat DepthFormat;
        public int SampleCount;
        public int Width;
        public int Height;
    }

    /// <summary>
    /// Binding layout descriptor.
    /// </summary>
    public struct BindingLayoutDesc
    {
        public BindingLayoutItem[] Items;
        public bool IsPushDescriptor;
    }

    /// <summary>
    /// Binding set descriptor.
    /// </summary>
    public struct BindingSetDesc
    {
        public BindingSetItem[] Items;
    }

    /// <summary>
    /// Acceleration structure descriptor for raytracing.
    /// </summary>
    public struct AccelStructDesc
    {
        public bool IsTopLevel;
        public int TopLevelMaxInstances;
        public GeometryDesc[] BottomLevelGeometries;
        public AccelStructBuildFlags BuildFlags;
        public string DebugName;

        public AccelStructDesc SetIsTopLevel(bool v) { IsTopLevel = v; return this; }
        public AccelStructDesc SetTopLevelMaxInstances(int n) { TopLevelMaxInstances = n; return this; }
        public AccelStructDesc SetDebugName(string n) { DebugName = n; return this; }
        public AccelStructDesc AddBottomLevelGeometry(GeometryDesc g)
        {
            if (BottomLevelGeometries == null) BottomLevelGeometries = new GeometryDesc[] { g };
            else
            {
                var newArr = new GeometryDesc[BottomLevelGeometries.Length + 1];
                Array.Copy(BottomLevelGeometries, newArr, BottomLevelGeometries.Length);
                newArr[BottomLevelGeometries.Length] = g;
                BottomLevelGeometries = newArr;
            }
            return this;
        }
    }

    /// <summary>
    /// Raytracing pipeline descriptor.
    /// </summary>
    public struct RaytracingPipelineDesc
    {
        public IShader[] Shaders;
        public HitGroup[] HitGroups;
        public int MaxPayloadSize;
        public int MaxAttributeSize;
        public int MaxRecursionDepth;
        public IBindingLayout GlobalBindingLayout;
        public string DebugName;
    }

    #endregion

    #region Supporting Enums and Structs

    public enum TextureDimension { Texture1D, Texture2D, Texture3D, TextureCube, Texture1DArray, Texture2DArray, TextureCubeArray }

    /// <summary>
    /// Buffer heap type for D3D12 backend, specifying memory placement strategy.
    /// Maps to D3D12_HEAP_TYPE values:
    /// - Default: GPU-accessible only, best performance for GPU-only resources (D3D12_HEAP_TYPE_DEFAULT)
    /// - Upload: CPU-writable, GPU-readable, for staging/upload buffers (D3D12_HEAP_TYPE_UPLOAD)
    /// - Readback: CPU-readable, GPU-writable, for reading back GPU results (D3D12_HEAP_TYPE_READBACK)
    /// </summary>
    public enum BufferHeapType
    {
        /// <summary>
        /// Default heap type - GPU-accessible only, best performance for GPU-only resources.
        /// Use this for vertex buffers, index buffers, constant buffers that are set once and used by GPU.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Upload heap type - CPU-writable, GPU-readable.
        /// Use this for buffers that are frequently updated from CPU (dynamic buffers, staging buffers).
        /// Allows direct CPU writes without needing temporary staging buffers.
        /// </summary>
        Upload = 1,

        /// <summary>
        /// Readback heap type - CPU-readable, GPU-writable.
        /// Use this for buffers that need to be read back by CPU (e.g., query results, GPU compute outputs).
        /// </summary>
        Readback = 2
    }

    public enum ResourceState
    {
        Common, VertexBuffer, IndexBuffer, ConstantBuffer, ShaderResource,
        UnorderedAccess, RenderTarget, DepthWrite, DepthRead, IndirectArgument,
        CopyDest, CopySource, Present, AccelStructRead, AccelStructWrite, AccelStructBuildInput
    }

    public struct ClearValue
    {
        public float R, G, B, A;
        public float Depth;
        public byte Stencil;
    }

    [Flags]
    public enum BufferUsageFlags
    {
        None = 0,
        VertexBuffer = 1 << 0,
        IndexBuffer = 1 << 1,
        ConstantBuffer = 1 << 2,
        ShaderResource = 1 << 3,
        UnorderedAccess = 1 << 4,
        IndirectArgument = 1 << 5,
        AccelStructStorage = 1 << 6,
        ShaderBindingTable = 1 << 7
    }

    public enum SamplerFilter { Point, Linear, Anisotropic }
    public enum SamplerAddressMode { Wrap, Mirror, Clamp, Border, MirrorOnce }
    public enum PrimitiveTopology { PointList, LineList, LineStrip, TriangleList, TriangleStrip, PatchList }

    public struct InputLayoutDesc
    {
        public VertexAttributeDesc[] Attributes;
    }

    public struct VertexAttributeDesc
    {
        public string Name;
        public int BufferIndex;
        public int Offset;
        public TextureFormat Format;
        public bool IsInstanced;

        // Additional properties for D3D12 compatibility
        public string SemanticName
        {
            get { return Name; }
            set { Name = value; }
        }

        public int SemanticIndex { get; set; }

        public int Slot
        {
            get { return BufferIndex; }
            set { BufferIndex = value; }
        }

        public bool PerInstance
        {
            get { return IsInstanced; }
            set { IsInstanced = value; }
        }

        public int InstanceStepRate { get; set; }
    }

    public struct BlendStateDesc
    {
        public bool AlphaToCoverage;
        public RenderTargetBlendDesc[] RenderTargets;
    }

    public struct RenderTargetBlendDesc
    {
        public bool BlendEnable;
        public BlendFactor SrcBlend, DestBlend, SrcBlendAlpha, DestBlendAlpha;
        public BlendOp BlendOp, BlendOpAlpha;
        public byte WriteMask;
    }

    public struct RasterStateDesc
    {
        public CullMode CullMode;
        public FillMode FillMode;
        public bool FrontCCW;
        public int DepthBias;
        public float SlopeScaledDepthBias;
        public float DepthBiasClamp;
        public bool DepthClipEnable;
        public bool ScissorEnable;
        public bool MultisampleEnable;
        public bool AntialiasedLineEnable;
        public bool ConservativeRaster;
    }

    public struct DepthStencilStateDesc
    {
        public bool DepthTestEnable;
        public bool DepthWriteEnable;
        public CompareFunc DepthFunc;
        public bool StencilEnable;
        public byte StencilReadMask;
        public byte StencilWriteMask;
        public StencilOpDesc FrontFace;
        public StencilOpDesc BackFace;
    }

    public struct StencilOpDesc
    {
        public StencilOp StencilFailOp, DepthFailOp, PassOp;
        public CompareFunc StencilFunc;
    }

    public enum StencilOp { Keep, Zero, Replace, IncrSat, DecrSat, Invert, Incr, Decr }

    public struct BindingLayoutItem
    {
        public int Slot;
        public BindingType Type;
        public ShaderStageFlags Stages;
        public int Count;
    }

    public enum BindingType { Texture, Sampler, ConstantBuffer, StructuredBuffer, RWTexture, RWBuffer, AccelStruct }

    [Flags]
    public enum ShaderStageFlags
    {
        None = 0, Vertex = 1, Hull = 2, Domain = 4, Geometry = 8, Pixel = 16, Compute = 32,
        AllGraphics = Vertex | Hull | Domain | Geometry | Pixel,
        RayGen = 64, Miss = 128, ClosestHit = 256, AnyHit = 512,
        AllRaytracing = RayGen | Miss | ClosestHit | AnyHit
    }

    public struct BindingSetItem
    {
        public int Slot;
        public BindingType Type;
        public ITexture Texture;
        public IBuffer Buffer;
        public ISampler Sampler;
        public IAccelStruct AccelStruct;
        /// <summary>
        /// Array of acceleration structures for array bindings.
        /// If provided, this takes precedence over AccelStruct for array bindings.
        /// For single bindings, AccelStruct can still be used for backward compatibility.
        /// </summary>
        public IAccelStruct[] AccelStructs;
        public int BufferOffset;
        public int BufferRange;
    }

    public struct GeometryDesc
    {
        public GeometryType Type;
        public GeometryTriangles Triangles;
        public GeometryAABBs AABBs;
        public GeometryFlags Flags;

        public GeometryDesc SetTriangles(GeometryTriangles t) { Type = GeometryType.Triangles; Triangles = t; return this; }
    }

    public enum GeometryType { Triangles, AABBs }

    [Flags]
    public enum GeometryFlags { None = 0, Opaque = 1, NoDuplicateAnyHit = 2 }

    public struct GeometryTriangles
    {
        public IBuffer VertexBuffer;
        public int VertexOffset;
        public int VertexCount;
        public int VertexStride;
        public TextureFormat VertexFormat;
        public IBuffer IndexBuffer;
        public int IndexOffset;
        public int IndexCount;
        public TextureFormat IndexFormat;
        public IBuffer TransformBuffer;
        public int TransformOffset;

        public GeometryTriangles SetVertexBuffer(IBuffer b) { VertexBuffer = b; return this; }
        public GeometryTriangles SetVertexFormat(TextureFormat f) { VertexFormat = f; return this; }
        public GeometryTriangles SetVertexCount(int n) { VertexCount = n; return this; }
        public GeometryTriangles SetVertexStride(int s) { VertexStride = s; return this; }
    }

    public struct GeometryAABBs
    {
        public IBuffer Buffer;
        public int Offset;
        public int Count;
        public int Stride;
    }

    [Flags]
    public enum AccelStructBuildFlags
    {
        None = 0, AllowUpdate = 1, AllowCompaction = 2, PreferFastTrace = 4, PreferFastBuild = 8, MinimizeMemory = 16
    }

    public struct HitGroup
    {
        public string Name;
        public IShader ClosestHitShader;
        public IShader AnyHitShader;
        public IShader IntersectionShader;
        public bool IsProceduralPrimitive;
    }

    #endregion
}



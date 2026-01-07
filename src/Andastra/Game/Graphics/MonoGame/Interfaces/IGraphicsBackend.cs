using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Interfaces
{
    /// <summary>
    /// Graphics backend capability information.
    /// </summary>
    public struct GraphicsCapabilities
    {
        /// <summary>
        /// Maximum texture dimension (width/height).
        /// </summary>
        public int MaxTextureSize;

        /// <summary>
        /// Maximum number of simultaneous render targets.
        /// </summary>
        public int MaxRenderTargets;

        /// <summary>
        /// Maximum anisotropic filtering level.
        /// </summary>
        public int MaxAnisotropy;

        /// <summary>
        /// Whether compute shaders are supported.
        /// </summary>
        public bool SupportsComputeShaders;

        /// <summary>
        /// Whether geometry shaders are supported.
        /// </summary>
        public bool SupportsGeometryShaders;

        /// <summary>
        /// Whether tessellation shaders are supported.
        /// </summary>
        public bool SupportsTessellation;

        /// <summary>
        /// Whether hardware raytracing is supported (DXR/Vulkan RT).
        /// </summary>
        public bool SupportsRaytracing;

        /// <summary>
        /// Whether mesh shaders are supported.
        /// </summary>
        public bool SupportsMeshShaders;

        /// <summary>
        /// Whether variable rate shading is supported.
        /// </summary>
        public bool SupportsVariableRateShading;

        /// <summary>
        /// Total dedicated video memory in bytes.
        /// </summary>
        public long DedicatedVideoMemory;

        /// <summary>
        /// Total shared system memory in bytes.
        /// </summary>
        public long SharedSystemMemory;

        /// <summary>
        /// GPU vendor name.
        /// </summary>
        public string VendorName;

        /// <summary>
        /// GPU device name.
        /// </summary>
        public string DeviceName;

        /// <summary>
        /// Driver version string.
        /// </summary>
        public string DriverVersion;

        /// <summary>
        /// The active graphics backend.
        /// </summary>
        public GraphicsBackend ActiveBackend;

        /// <summary>
        /// Shader model version (e.g., 6.5 for SM 6.5).
        /// </summary>
        public float ShaderModelVersion;

        /// <summary>
        /// Whether NVIDIA RTX Remix is detected and available.
        /// </summary>
        public bool RemixAvailable;

        /// <summary>
        /// Whether DLSS is available.
        /// </summary>
        public bool DlssAvailable;

        /// <summary>
        /// Whether FSR is available.
        /// </summary>
        public bool FsrAvailable;
    }

    /// <summary>
    /// Abstracts the graphics backend for multi-API support.
    /// </summary>
    public interface IGraphicsBackend : IDisposable
    {
        /// <summary>
        /// Gets the active backend type.
        /// </summary>
        GraphicsBackend BackendType { get; }

        /// <summary>
        /// Gets the hardware capabilities.
        /// </summary>
        GraphicsCapabilities Capabilities { get; }

        /// <summary>
        /// Gets whether the backend is initialized and ready.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initializes the graphics backend with the specified settings.
        /// </summary>
        /// <param name="settings">Renderer settings.</param>
        /// <returns>True if initialization succeeded.</returns>
        bool Initialize(RenderSettings settings);

        /// <summary>
        /// Shuts down the graphics backend.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Begins a new frame for rendering.
        /// </summary>
        void BeginFrame();

        /// <summary>
        /// Ends the current frame and presents to screen.
        /// </summary>
        void EndFrame();

        /// <summary>
        /// Resizes the swap chain / render targets.
        /// </summary>
        /// <param name="width">New width in pixels.</param>
        /// <param name="height">New height in pixels.</param>
        void Resize(int width, int height);

        /// <summary>
        /// Creates a texture resource.
        /// </summary>
        /// <param name="desc">Texture description.</param>
        /// <returns>Handle to the created texture.</returns>
        IntPtr CreateTexture(TextureDescription desc);

        /// <summary>
        /// Uploads texture pixel data to a previously created texture.
        /// Matches original engine behavior: swkotor.exe and swkotor2.exe use glTexImage2D/glCompressedTexImage2D
        /// to upload texture data after creating the texture object.
        /// </summary>
        /// <param name="handle">Handle to the texture created by CreateTexture.</param>
        /// <param name="data">Texture upload data containing mipmap levels.</param>
        /// <returns>True if upload succeeded, false otherwise.</returns>
        bool UploadTextureData(IntPtr handle, TextureUploadData data);

        /// <summary>
        /// Creates a buffer resource.
        /// </summary>
        /// <param name="desc">Buffer description.</param>
        /// <returns>Handle to the created buffer.</returns>
        IntPtr CreateBuffer(BufferDescription desc);

        /// <summary>
        /// Creates a shader pipeline.
        /// </summary>
        /// <param name="desc">Pipeline description.</param>
        /// <returns>Handle to the created pipeline.</returns>
        IntPtr CreatePipeline(PipelineDescription desc);

        /// <summary>
        /// Destroys a resource by handle.
        /// </summary>
        /// <param name="handle">Resource handle.</param>
        void DestroyResource(IntPtr handle);

        /// <summary>
        /// Queries whether raytracing is available and enabled.
        /// </summary>
        bool IsRaytracingEnabled { get; }

        /// <summary>
        /// Enables or disables raytracing features.
        /// </summary>
        /// <param name="level">Raytracing feature level.</param>
        void SetRaytracingLevel(RaytracingLevel level);

        /// <summary>
        /// Gets performance statistics for the last frame.
        /// </summary>
        FrameStatistics GetFrameStatistics();

        /// <summary>
        /// Gets the raytracing-capable device interface.
        /// Returns an IDevice instance that provides access to raytracing resources and operations.
        /// This device can be used for creating acceleration structures, raytracing pipelines, and other raytracing-specific resources.
        /// </summary>
        /// <returns>IDevice instance if available, null if raytracing is not supported or device creation failed.</returns>
        IDevice GetDevice();
    }

    /// <summary>
    /// Texture resource description.
    /// </summary>
    public struct TextureDescription
    {
        public int Width;
        public int Height;
        public int Depth;
        public int MipLevels;
        public int ArraySize;
        public TextureFormat Format;
        public TextureUsage Usage;
        public bool IsCubemap;
        public int SampleCount;
        public string DebugName;
    }

    /// <summary>
    /// Buffer resource description.
    /// </summary>
    public struct BufferDescription
    {
        public int SizeInBytes;
        public BufferUsage Usage;
        public int StructureByteStride;
        public string DebugName;
    }

    /// <summary>
    /// Pipeline state description.
    /// </summary>
    public struct PipelineDescription
    {
        public byte[] VertexShader;
        public byte[] PixelShader;
        public byte[] GeometryShader;
        public byte[] HullShader;
        public byte[] DomainShader;
        public byte[] ComputeShader;
        public BlendState BlendState;
        public RasterizerState RasterizerState;
        public DepthStencilState DepthStencilState;
        public InputLayout InputLayout;
        public string DebugName;
    }

    /// <summary>
    /// Frame performance statistics.
    /// </summary>
    public struct FrameStatistics
    {
        public double FrameTimeMs;
        public double GpuTimeMs;
        public double CpuTimeMs;
        public int DrawCalls;
        public int TrianglesRendered;
        public int TexturesUsed;
        public long VideoMemoryUsed;
        public double RaytracingTimeMs;
    }

    /// <summary>
    /// Texture formats.
    /// </summary>
    public enum TextureFormat
    {
        Unknown,
        R8_UNorm,
        R8_UInt,
        R8_SInt,
        R8_SNorm,
        R8G8_UNorm,
        R8G8_UInt,
        R8G8_SNorm,
        R8G8_SInt,
        R8G8B8A8_UNorm,
        R8G8B8A8_UNorm_SRGB,
        R8G8B8A8_UInt,
        R8G8B8A8_SInt,
        R8G8B8A8_SNorm,
        B8G8R8A8_UNorm,
        B8G8R8A8_UNorm_SRGB,
        R16_Float,
        R16_UNorm,
        R16_UInt,
        R16_SInt,
        R16G16_Float,
        R16G16_UNorm,
        R16G16_UInt,
        R16G16_SInt,
        R16G16B16A16_Float,
        R16G16B16A16_UNorm,
        R16G16B16A16_UInt,
        R16G16B16A16_SInt,
        R32_Float,
        R32_UInt,
        R32_SInt,
        R32G32_Float,
        R32G32_UInt,
        R32G32_SInt,
        R32G32B32_Float,
        R32G32B32_UInt,
        R32G32B32_SInt,
        R32G32B32A32_Float,
        R32G32B32A32_UInt,
        R32G32B32A32_SInt,
        R11G11B10_Float,
        R10G10B10A2_UNorm,
        R10G10B10A2_UInt,
        // Block-compressed formats (BC1-BC7 aliases for DirectX/Metal compatibility)
        BC1_UNorm,
        BC1_UNorm_SRGB,
        BC1,
        BC2_UNorm,
        BC2_UNorm_SRGB,
        BC2,
        BC3_UNorm,
        BC3_UNorm_SRGB,
        BC3,
        BC4_UNorm,
        BC4,
        BC5_UNorm,
        BC5,
        BC6H_UFloat,
        BC6H,
        BC7_UNorm,
        BC7_UNorm_SRGB,
        BC7,
        // ASTC compressed formats (for Metal/mobile)
        ASTC_4x4,
        ASTC_5x5,
        ASTC_6x6,
        ASTC_8x8,
        ASTC_10x10,
        ASTC_12x12,
        // Depth formats
        D16_UNorm,
        D24_UNorm_S8_UInt,
        D32_Float,
        D32_Float_S8_UInt
    }

    /// <summary>
    /// Texture usage flags.
    /// </summary>
    [Flags]
    public enum TextureUsage
    {
        ShaderResource = 1 << 0,
        RenderTarget = 1 << 1,
        DepthStencil = 1 << 2,
        UnorderedAccess = 1 << 3,
        CopyDest = 1 << 4,
        Storage = 1 << 5
    }

    /// <summary>
    /// Buffer usage flags.
    /// </summary>
    [Flags]
    public enum BufferUsage
    {
        Vertex = 1 << 0,
        Index = 1 << 1,
        Constant = 1 << 2,
        Structured = 1 << 3,
        Indirect = 1 << 4,
        AccelerationStructure = 1 << 5
    }

    /// <summary>
    /// Blend state configuration.
    /// </summary>
    public struct BlendState
    {
        public bool BlendEnable;
        public BlendFactor SrcBlend;
        public BlendFactor DstBlend;
        public BlendOp BlendOp;
        public BlendFactor SrcBlendAlpha;
        public BlendFactor DstBlendAlpha;
        public BlendOp BlendOpAlpha;
        public byte RenderTargetWriteMask;
    }

    public enum BlendFactor
    {
        Zero, One, SrcColor, InvSrcColor, SrcAlpha, InvSrcAlpha,
        DstAlpha, InvDstAlpha, DstColor, InvDstColor
    }

    public enum BlendOp { Add, Subtract, ReverseSubtract, Min, Max }

    /// <summary>
    /// Rasterizer state configuration.
    /// </summary>
    public struct RasterizerState
    {
        public CullMode CullMode;
        public FillMode FillMode;
        public bool FrontCounterClockwise;
        public int DepthBias;
        public float SlopeScaledDepthBias;
        public bool ScissorEnable;
        public bool MultisampleEnable;
    }

    public enum CullMode { None, Front, Back }
    public enum FillMode { Solid, Wireframe }

    /// <summary>
    /// Depth-stencil state configuration.
    /// </summary>
    public struct DepthStencilState
    {
        public bool DepthEnable;
        public bool DepthWriteEnable;
        public CompareFunc DepthFunc;
        public bool StencilEnable;
        public byte StencilReadMask;
        public byte StencilWriteMask;
    }

    public enum CompareFunc { Never, Less, Equal, LessEqual, Greater, NotEqual, GreaterEqual, Always }

    /// <summary>
    /// Input layout for vertex data.
    /// </summary>
    public struct InputLayout
    {
        public InputElement[] Elements;
    }

    public struct InputElement
    {
        public string SemanticName;
        public int SemanticIndex;
        public TextureFormat Format;
        public int Slot;
        public int AlignedByteOffset;
        public bool PerInstance;
        public int InstanceDataStepRate;
    }

    /// <summary>
    /// Texture mipmap level data for upload.
    /// </summary>
    public struct TextureMipmapData
    {
        /// <summary>
        /// Mipmap level (0 = base level).
        /// </summary>
        public int Level;

        /// <summary>
        /// Width of this mipmap level.
        /// </summary>
        public int Width;

        /// <summary>
        /// Height of this mipmap level.
        /// </summary>
        public int Height;

        /// <summary>
        /// Pixel data for this mipmap level (RGBA format, width * height * 4 bytes).
        /// </summary>
        public byte[] Data;
    }

    /// <summary>
    /// Texture upload data containing all mipmap levels.
    /// Matches original engine texture upload pattern: TPC files contain multiple mipmap levels
    /// that are uploaded sequentially using glTexImage2D for each level.
    /// </summary>
    public struct TextureUploadData
    {
        /// <summary>
        /// Array of mipmap data, ordered from base level (0) to highest mip level.
        /// </summary>
        public TextureMipmapData[] Mipmaps;

        /// <summary>
        /// Texture format (must match the format used in CreateTexture).
        /// </summary>
        public TextureFormat Format;
    }
}


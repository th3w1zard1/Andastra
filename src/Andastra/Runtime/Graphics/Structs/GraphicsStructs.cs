using System;
using System.Numerics;
using Andastra.Runtime.Graphics.Common.Enums;
using Color = Andastra.Runtime.Graphics.Color;

namespace Andastra.Runtime.Graphics.Common.Structs
{
    /// <summary>
    /// Graphics backend capability information.
    /// </summary>
    /// <remarks>
    /// Graphics Capabilities Struct:
    /// - This struct represents modern graphics hardware capabilities
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game capabilities: Limited to DirectX 9/OpenGL features (no raytracing, compute shaders, mesh shaders, etc.)
    /// - This struct: Represents modern graphics capabilities for advanced APIs, not directly mapped to swkotor2.exe functions
    /// </remarks>
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
        public GraphicsBackendType ActiveBackend;

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
    /// Blend state configuration.
    /// </summary>
    public struct BlendStateDesc
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

    /// <summary>
    /// Rasterizer state configuration.
    /// </summary>
    public struct RasterizerStateDesc
    {
        public CullMode CullMode;
        public FillMode FillMode;
        public bool FrontCounterClockwise;
        public int DepthBias;
        public float SlopeScaledDepthBias;
        public bool ScissorEnable;
        public bool MultisampleEnable;
    }

    /// <summary>
    /// Depth-stencil state configuration.
    /// </summary>
    public struct DepthStencilStateDesc
    {
        public bool DepthEnable;
        public bool DepthWriteEnable;
        public CompareFunc DepthFunc;
        public bool StencilEnable;
        public byte StencilReadMask;
        public byte StencilWriteMask;
    }

    /// <summary>
    /// Input element for vertex layout.
    /// </summary>
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
    /// Input layout for vertex data.
    /// </summary>
    public struct InputLayoutDesc
    {
        public InputElement[] Elements;
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
        public BlendStateDesc BlendState;
        public RasterizerStateDesc RasterizerState;
        public DepthStencilStateDesc DepthStencilState;
        public InputLayoutDesc InputLayout;
        public string DebugName;
    }

    /// <summary>
    /// Raytracing statistics.
    /// </summary>
    public struct RaytracingStatistics
    {
        public int BlasCount;
        public int TlasInstanceCount;
        public long RaysTraced;
        public double BuildTimeMs;
        public double TraceTimeMs;
        public double DenoiseTimeMs;
    }

    /// <summary>
    /// Raytracing settings.
    /// </summary>
    public struct RaytracingSettings
    {
        public RaytracingLevel Level;
        public int SamplesPerPixel;
        public int MaxBounces;
        public bool EnableDenoiser;
        public DenoiserType Denoiser;
        public float RayBudget;
    }

    /// <summary>
    /// Mesh geometry data for raytracing BLAS.
    /// </summary>
    public struct MeshGeometry
    {
        public IntPtr VertexBuffer;
        public IntPtr IndexBuffer;
        public int VertexCount;
        public int IndexCount;
        public int VertexStride;
        public bool IsOpaque;
    }

    /// <summary>
    /// Shadow ray parameters.
    /// </summary>
    public struct ShadowRayParams
    {
        public Vector3 LightDirection;
        public float MaxDistance;
        public int SamplesPerPixel;
        public float ShadowSoftness;
    }

    /// <summary>
    /// Reflection ray parameters.
    /// </summary>
    public struct ReflectionRayParams
    {
        public int SamplesPerPixel;
        public int MaxBounces;
        public float RoughnessThreshold;
    }

    /// <summary>
    /// Global illumination ray parameters.
    /// </summary>
    public struct GiRayParams
    {
        public int SamplesPerPixel;
        public int MaxBounces;
        public float IndirectIntensity;
    }

    /// <summary>
    /// Ambient occlusion ray parameters.
    /// </summary>
    public struct AoRayParams
    {
        public int SamplesPerPixel;
        public float Radius;
        public float Power;
    }

    /// <summary>
    /// Denoiser parameters.
    /// </summary>
    public struct DenoiserParams
    {
        public DenoiserType Type;
        public float TemporalBlend;
        public float SpatialSigma;
    }

    /// <summary>
    /// Vertex structure for position and color.
    /// </summary>
    public struct VertexPositionColor
    {
        public Vector3 Position;
        public Color Color;

        public VertexPositionColor(Vector3 position, Color color)
        {
            Position = position;
            Color = color;
        }
    }
}


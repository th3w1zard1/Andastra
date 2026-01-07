namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Vertex declaration abstraction for defining vertex formats.
    /// </summary>
    /// <remarks>
    /// Vertex Declaration Interface:
    /// - Based on swkotor2.exe DirectX vertex format system
    /// - Located via string references: "Disable Vertex Buffer Objects" @ 0x007b56bc (VBO option)
    /// - "glVertexAttrib4fvNV" @ 0x007b7d24, "glVertexAttrib3fvNV" @ 0x007b7d38, "glVertexAttrib2fvNV" @ 0x007b7d4c
    /// - Original implementation: DirectX 8/9 flexible vertex format (FVF) or vertex declarations
    /// - Vertex formats: Define vertex structure (position, normal, texture coordinates, colors, etc.)
    /// - FVF: Flexible Vertex Format codes define vertex layout (D3DFVF_* constants)
    /// - This interface: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan)
    /// - Note: Modern APIs use vertex declarations/elements instead of FVF codes
    /// </remarks>
    public interface IVertexDeclaration
    {
        /// <summary>
        /// Gets the vertex stride (size in bytes).
        /// </summary>
        int VertexStride { get; }

        /// <summary>
        /// Gets the vertex elements.
        /// </summary>
        VertexElement[] Elements { get; }
    }

    /// <summary>
    /// Vertex element definition.
    /// </summary>
    public struct VertexElement
    {
        public int Offset;
        public VertexElementFormat Format;
        public VertexElementUsage Usage;
        public int UsageIndex;

        public VertexElement(int offset, VertexElementFormat format, VertexElementUsage usage, int usageIndex = 0)
        {
            Offset = offset;
            Format = format;
            Usage = usage;
            UsageIndex = usageIndex;
        }
    }

    /// <summary>
    /// Vertex element format.
    /// </summary>
    public enum VertexElementFormat
    {
        Single,
        Vector2,
        Vector3,
        Vector4,
        Color,
        Byte4,
        Short2,
        Short4,
        NormalizedShort2,
        NormalizedShort4,
        HalfVector2,
        HalfVector4
    }

    /// <summary>
    /// Vertex element usage.
    /// </summary>
    public enum VertexElementUsage
    {
        Position,
        Color,
        TextureCoordinate,
        Normal,
        Binormal,
        Tangent,
        BlendIndices,
        BlendWeight,
        Depth,
        Fog,
        PointSize,
        Sample,
        TessellateFactor
    }
}


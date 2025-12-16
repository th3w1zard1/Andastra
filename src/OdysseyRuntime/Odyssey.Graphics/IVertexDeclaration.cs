namespace Odyssey.Graphics
{
    /// <summary>
    /// Vertex declaration abstraction for defining vertex formats.
    /// </summary>
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


using System;
using System.Numerics;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Vertex structure with position and color (equivalent to MonoGame's VertexPositionColor).
    /// </summary>
    /// <remarks>
    /// Vertex Position Color Structure:
    /// - Based on swkotor2.exe vertex format system
    /// - Located via string references: "Disable Vertex Buffer Objects" @ 0x007b56bc (VBO option)
    /// - Original implementation: DirectX 8/9 flexible vertex format (FVF) with position and color
    /// - Vertex format: D3DFVF_XYZ | D3DFVF_DIFFUSE (position + color)
    /// - Used for: Simple 3D rendering with vertex colors (debug rendering, simple geometry)
    /// - This structure: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan)
    /// </remarks>
    public struct VertexPositionColor : IEquatable<VertexPositionColor>
    {
        public Vector3 Position;
        public Color Color;

        public VertexPositionColor(Vector3 position, Color color)
        {
            Position = position;
            Color = color;
        }

        public bool Equals(VertexPositionColor other)
        {
            return Position.Equals(other.Position) && Color.Equals(other.Color);
        }

        public override bool Equals(object obj)
        {
            if (obj is VertexPositionColor)
            {
                return Equals((VertexPositionColor)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode() ^ Color.GetHashCode();
        }

        public static bool operator ==(VertexPositionColor left, VertexPositionColor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VertexPositionColor left, VertexPositionColor right)
        {
            return !left.Equals(right);
        }
    }
}


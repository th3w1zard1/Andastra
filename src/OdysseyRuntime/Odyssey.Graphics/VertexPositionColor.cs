using System;
using System.Numerics;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Vertex structure with position and color (equivalent to MonoGame's VertexPositionColor).
    /// </summary>
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


using System;
using System.Numerics;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Vertex structure with position and texture coordinates (equivalent to MonoGame's VertexPositionTexture).
    /// Used for sprite rendering with source rectangle support.
    /// </summary>
    /// <remarks>
    /// Vertex Position Texture Structure:
    /// - Based on swkotor2.exe vertex format system for sprite rendering
    /// - Located via string references: Sprite rendering with source rectangles @ 0x007b5680
    /// - Original implementation: DirectX 8/9 flexible vertex format (FVF) with position and texture coordinates
    /// - Vertex format: D3DFVF_XYZ | D3DFVF_TEX1 (position + texture coordinates)
    /// - Used for: 2D sprite rendering with UV coordinate support (GUI elements, sprites, text)
    /// - This structure: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan, Stride)
    /// - Texture coordinates are normalized (0.0 to 1.0) and used to sample specific regions of textures
    /// </remarks>
    public struct VertexPositionTexture : IEquatable<VertexPositionTexture>
    {
        public Vector3 Position;
        public Vector2 TextureCoordinate;

        public VertexPositionTexture(Vector3 position, Vector2 textureCoordinate)
        {
            Position = position;
            TextureCoordinate = textureCoordinate;
        }

        public bool Equals(VertexPositionTexture other)
        {
            return Position.Equals(other.Position) && TextureCoordinate.Equals(other.TextureCoordinate);
        }

        public override bool Equals(object obj)
        {
            if (obj is VertexPositionTexture)
            {
                return Equals((VertexPositionTexture)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode() ^ TextureCoordinate.GetHashCode();
        }

        public static bool operator ==(VertexPositionTexture left, VertexPositionTexture right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VertexPositionTexture left, VertexPositionTexture right)
        {
            return !left.Equals(right);
        }
    }
}


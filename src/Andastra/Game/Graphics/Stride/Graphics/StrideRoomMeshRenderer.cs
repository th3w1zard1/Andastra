using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using BioWare.NET.Resource.Formats.MDLData;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;
using Stride.Graphics;

namespace Andastra.Game.Stride.Graphics
{
    /// <summary>
    /// Vertex structure for position and color.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionColor
    {
        public Vector3 Position;
        public uint Color; // Packed color as uint
    }

    /// <summary>
    /// Stride implementation of IRoomMeshRenderer.
    /// </summary>
    public class StrideRoomMeshRenderer : IRoomMeshRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, StrideRoomMeshData> _loadedMeshes;

        public StrideRoomMeshRenderer([NotNull] GraphicsDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            _graphicsDevice = device;
            _loadedMeshes = new Dictionary<string, StrideRoomMeshData>(StringComparer.OrdinalIgnoreCase);
        }

        public IRoomMeshData LoadRoomMesh(string modelResRef, MDL mdl)
        {
            if (string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            if (_loadedMeshes.TryGetValue(modelResRef, out StrideRoomMeshData cached))
            {
                return cached;
            }

            if (mdl == null)
            {
                return null;
            }

            // Extract geometry from MDL
            var meshData = new StrideRoomMeshData();
            var vertices = new List<VertexPositionColor>();
            var indices = new List<int>();

            // Extract basic geometry from the first trimesh node
            ExtractBasicGeometry(mdl, vertices, indices);

            if (vertices.Count == 0 || indices.Count == 0)
            {
                return null;
            }

            // Create vertex buffer using abstraction layer
            var vertexArray = vertices.ToArray();
            meshData.VertexBuffer = new StrideVertexBuffer(
                global::Stride.Graphics.Buffer.Vertex.New(_graphicsDevice, vertexArray, global::Stride.Graphics.GraphicsResourceUsage.Dynamic),
                vertexArray.Length,
                System.Runtime.InteropServices.Marshal.SizeOf<VertexPositionColor>()
            );

            // Create index buffer using abstraction layer
            meshData.IndexCount = indices.Count;
            var indexArray = indices.ToArray();
            meshData.IndexBuffer = new StrideIndexBuffer(
                global::Stride.Graphics.Buffer.Index.New(_graphicsDevice, indexArray, global::Stride.Graphics.GraphicsResourceUsage.Dynamic),
                indexArray.Length,
                false
            );

            _loadedMeshes[modelResRef] = meshData;
            return meshData;
        }

        public void Clear()
        {
            foreach (StrideRoomMeshData mesh in _loadedMeshes.Values)
            {
                mesh.VertexBuffer?.Dispose();
                mesh.IndexBuffer?.Dispose();
            }
            _loadedMeshes.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        private void ExtractBasicGeometry(MDL mdl, List<VertexPositionColor> vertices, List<int> indices)
        {
            if (mdl == null || mdl.Root == null)
            {
                CreatePlaceholderBox(vertices, indices);
                return;
            }

            // Extract geometry from all mesh nodes recursively
            var identity = Matrix4x4.Identity;
            ExtractNodeGeometry(mdl.Root, identity, vertices, indices);

            // If no geometry found, create placeholder
            if (vertices.Count == 0 || indices.Count == 0)
            {
                CreatePlaceholderBox(vertices, indices);
            }
        }

        private void ExtractNodeGeometry(MDLNode node, Matrix4x4 parentTransform, List<VertexPositionColor> vertices, List<int> indices)
        {
            if (node == null)
            {
                return;
            }

            // Build node transform
            // Create rotation from quaternion (X, Y, Z, W stored in node.Orientation)
            System.Numerics.Quaternion rotation = new System.Numerics.Quaternion(
                node.Orientation.X,
                node.Orientation.Y,
                node.Orientation.Z,
                node.Orientation.W
            );

            // Create translation
            System.Numerics.Vector3 translation = new System.Numerics.Vector3(
                node.Position.X,
                node.Position.Y,
                node.Position.Z
            );

            // Create scale
            System.Numerics.Vector3 scale = new System.Numerics.Vector3(
                node.ScaleX,
                node.ScaleY,
                node.ScaleZ
            );

            // Build transform: Translation * Rotation * Scale
            // Note: In matrix multiplication, A * B * C applies C first, then B, then A
            // For transform order (Scale, then Rotation, then Translation), we need: Translation * Rotation * Scale
            // Reference: vendor/PyKotor/wiki/MDL-File-Format.md - Node Transform Order
            // Reference: MdlToMonoGameModelConverter.CreateNodeTransform for consistent implementation
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(scale);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(translation);
            Matrix4x4 nodeTransform = Matrix4x4.Multiply(Matrix4x4.Multiply(translationMatrix, rotationMatrix), scaleMatrix);
            Matrix4x4 finalTransform = Matrix4x4.Multiply(nodeTransform, parentTransform);

            // Extract mesh geometry if present
            if (node.Mesh != null)
            {
                ExtractMeshGeometry(node.Mesh, finalTransform, vertices, indices);
            }

            // Process children recursively
            if (node.Children != null)
            {
                foreach (MDLNode child in node.Children)
                {
                    ExtractNodeGeometry(child, finalTransform, vertices, indices);
                }
            }
        }

        private void ExtractMeshGeometry(MDLMesh mesh, Matrix4x4 transform, List<VertexPositionColor> vertices, List<int> indices)
        {
            if (mesh == null || mesh.Vertices == null || mesh.Faces == null)
            {
                return;
            }

            int baseVertexIndex = vertices.Count;
            uint meshColor = 0xFF808080; // Gray color as packed uint (ARGB)

            // Transform and add vertices
            foreach (System.Numerics.Vector3 vertex in mesh.Vertices)
            {
                // Transform vertex position using Matrix4x4.Transform
                var vertexVec = new System.Numerics.Vector4(vertex.X, vertex.Y, vertex.Z, 1.0f);
                var transformedVec = System.Numerics.Vector4.Transform(vertexVec, transform);
                var transformedPos = new System.Numerics.Vector3(transformedVec.X, transformedVec.Y, transformedVec.Z);

                vertices.Add(new VertexPositionColor { Position = transformedPos, Color = meshColor });
            }

            // Add faces as indices
            foreach (MDLFace face in mesh.Faces)
            {
                int v1 = face.V1;
                int v2 = face.V2;
                int v3 = face.V3;

                if (v1 >= 0 && v1 < mesh.Vertices.Count &&
                    v2 >= 0 && v2 < mesh.Vertices.Count &&
                    v3 >= 0 && v3 < mesh.Vertices.Count)
                {
                    indices.Add(baseVertexIndex + v1);
                    indices.Add(baseVertexIndex + v2);
                    indices.Add(baseVertexIndex + v3);
                }
            }
        }

        private void CreatePlaceholderBox(List<VertexPositionColor> vertices, List<int> indices)
        {
            float size = 5f;
            uint color = 0xFF808080; // Gray color as packed uint (ARGB)

            // 8 vertices of a box
            vertices.Add(new VertexPositionColor { Position = new System.Numerics.Vector3(-size, -size, -size), Color = color });
            vertices.Add(new VertexPositionColor { Position = new System.Numerics.Vector3(size, -size, -size), Color = color });
            vertices.Add(new VertexPositionColor { Position = new System.Numerics.Vector3(size, size, -size), Color = color });
            vertices.Add(new VertexPositionColor { Position = new System.Numerics.Vector3(-size, size, -size), Color = color });
            vertices.Add(new VertexPositionColor { Position = new System.Numerics.Vector3(-size, -size, size), Color = color });
            vertices.Add(new VertexPositionColor { Position = new System.Numerics.Vector3(size, -size, size), Color = color });
            vertices.Add(new VertexPositionColor { Position = new System.Numerics.Vector3(size, size, size), Color = color });
            vertices.Add(new VertexPositionColor { Position = new System.Numerics.Vector3(-size, size, size), Color = color });

            // 12 triangles (2 per face, 6 faces)
            // Front face
            indices.Add(0); indices.Add(1); indices.Add(2);
            indices.Add(0); indices.Add(2); indices.Add(3);
            // Back face
            indices.Add(4); indices.Add(6); indices.Add(5);
            indices.Add(4); indices.Add(7); indices.Add(6);
            // Top face
            indices.Add(3); indices.Add(2); indices.Add(6);
            indices.Add(3); indices.Add(6); indices.Add(7);
            // Bottom face
            indices.Add(0); indices.Add(4); indices.Add(5);
            indices.Add(0); indices.Add(5); indices.Add(1);
            // Right face
            indices.Add(1); indices.Add(5); indices.Add(6);
            indices.Add(1); indices.Add(6); indices.Add(2);
            // Left face
            indices.Add(0); indices.Add(3); indices.Add(7);
            indices.Add(0); indices.Add(7); indices.Add(4);
        }
    }

    /// <summary>
    /// Stride implementation of IRoomMeshData.
    /// </summary>
    public class StrideRoomMeshData : IRoomMeshData
    {
        public IVertexBuffer VertexBuffer { get; set; }
        public IIndexBuffer IndexBuffer { get; set; }
        public int IndexCount { get; set; }
    }
}


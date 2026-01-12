using System;
using System.Collections.Generic;
using Andastra.Parsing.Resource.Formats.MDL;
using Andastra.Parsing.Resource.Formats.MDLData;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IRoomMeshRenderer.
    /// </summary>
    public class MonoGameRoomMeshRenderer : IRoomMeshRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, MonoGameRoomMeshData> _loadedMeshes;

        public MonoGameRoomMeshRenderer([NotNull] GraphicsDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            _graphicsDevice = device;
            _loadedMeshes = new Dictionary<string, MonoGameRoomMeshData>(StringComparer.OrdinalIgnoreCase);
        }

        public IRoomMeshData LoadRoomMesh(string modelResRef, MDL mdl)
        {
            if (string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            if (_loadedMeshes.TryGetValue(modelResRef, out MonoGameRoomMeshData cached))
            {
                return cached;
            }

            if (mdl == null)
            {
                return null;
            }

            // Extract geometry from MDL
            var meshData = new MonoGameRoomMeshData();
            var vertices = new List<XnaVertexPositionColor>();
            var indices = new List<int>();

            // Extract basic geometry from the first trimesh node
            ExtractBasicGeometry(mdl, vertices, indices);

            if (vertices.Count == 0 || indices.Count == 0)
            {
                return null;
            }

            // Create vertex buffer using abstraction layer
            var vertexArray = vertices.ToArray();
            meshData.VertexBuffer = new MonoGameVertexBuffer(
                new VertexBuffer(_graphicsDevice, typeof(XnaVertexPositionColor), vertexArray.Length, BufferUsage.WriteOnly),
                vertexArray.Length,
                System.Runtime.InteropServices.Marshal.SizeOf<XnaVertexPositionColor>()
            );
            meshData.VertexBuffer.SetData(vertexArray);

            // Create index buffer using abstraction layer
            meshData.IndexCount = indices.Count;
            var indexArray = indices.ToArray();
            meshData.IndexBuffer = new MonoGameIndexBuffer(
                new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, indexArray.Length, BufferUsage.WriteOnly),
                indexArray.Length,
                false
            );
            meshData.IndexBuffer.SetData(indexArray);

            _loadedMeshes[modelResRef] = meshData;
            return meshData;
        }

        public void Clear()
        {
            foreach (MonoGameRoomMeshData mesh in _loadedMeshes.Values)
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

        private void ExtractBasicGeometry(MDL mdl, List<XnaVertexPositionColor> vertices, List<int> indices)
        {
            if (mdl == null || mdl.Root == null)
            {
                CreatePlaceholderBox(vertices, indices);
                return;
            }

            // Extract geometry from all mesh nodes recursively
            ExtractNodeGeometry(mdl.Root, Matrix.Identity, vertices, indices);

            // If no geometry found, create placeholder
            if (vertices.Count == 0 || indices.Count == 0)
            {
                CreatePlaceholderBox(vertices, indices);
            }
        }

        private void ExtractNodeGeometry(MDLNode node, Matrix parentTransform, List<XnaVertexPositionColor> vertices, List<int> indices)
        {
            if (node == null)
            {
                return;
            }

            // Build node transform
            // Create rotation from quaternion (X, Y, Z, W stored in node.Orientation)
            Quaternion rotation = new Quaternion(
                node.Orientation.X,
                node.Orientation.Y,
                node.Orientation.Z,
                node.Orientation.W
            );

            // Create translation
            Vector3 translation = new Vector3(
                node.Position.X,
                node.Position.Y,
                node.Position.Z
            );

            // Create scale
            Vector3 scale = new Vector3(
                node.ScaleX,
                node.ScaleY,
                node.ScaleZ
            );

            // Build transform: Translation * Rotation * Scale
            // Note: In matrix multiplication, A * B * C applies C first, then B, then A
            // For transform order (Scale, then Rotation, then Translation), we need: Translation * Rotation * Scale
            // Reference: vendor/PyKotor/wiki/MDL-File-Format.md - Node Transform Order
            // Reference: MdlToMonoGameModelConverter.CreateNodeTransform for consistent implementation
            Matrix rotationMatrix = Matrix.CreateFromQuaternion(rotation);
            Matrix scaleMatrix = Matrix.CreateScale(scale);
            Matrix translationMatrix = Matrix.CreateTranslation(translation);
            Matrix nodeTransform = translationMatrix * rotationMatrix * scaleMatrix;
            Matrix finalTransform = nodeTransform * parentTransform;

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

        private void ExtractMeshGeometry(MDLMesh mesh, Matrix transform, List<XnaVertexPositionColor> vertices, List<int> indices)
        {
            if (mesh == null || mesh.Vertices == null || mesh.Faces == null)
            {
                return;
            }

            int baseVertexIndex = vertices.Count;
            Microsoft.Xna.Framework.Color meshColor = Microsoft.Xna.Framework.Color.Gray;

            // Transform and add vertices
            foreach (System.Numerics.Vector3 vertex in mesh.Vertices)
            {
                // Transform vertex position
                var transformedPos = Microsoft.Xna.Framework.Vector3.Transform(
                    new Microsoft.Xna.Framework.Vector3(vertex.X, vertex.Y, vertex.Z),
                    transform
                );
                vertices.Add(new XnaVertexPositionColor(
                    new Microsoft.Xna.Framework.Vector3(transformedPos.X, transformedPos.Y, transformedPos.Z),
                    meshColor
                ));
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

        private void CreatePlaceholderBox(List<XnaVertexPositionColor> vertices, List<int> indices)
        {
            float size = 5f;
            Microsoft.Xna.Framework.Color color = Microsoft.Xna.Framework.Color.Gray;

            // 8 vertices of a box
            vertices.Add(new XnaVertexPositionColor(new Microsoft.Xna.Framework.Vector3(-size, -size, -size), color));
            vertices.Add(new XnaVertexPositionColor(new Microsoft.Xna.Framework.Vector3(size, -size, -size), color));
            vertices.Add(new XnaVertexPositionColor(new Microsoft.Xna.Framework.Vector3(size, size, -size), color));
            vertices.Add(new XnaVertexPositionColor(new Microsoft.Xna.Framework.Vector3(-size, size, -size), color));
            vertices.Add(new XnaVertexPositionColor(new Microsoft.Xna.Framework.Vector3(-size, -size, size), color));
            vertices.Add(new XnaVertexPositionColor(new Microsoft.Xna.Framework.Vector3(size, -size, size), color));
            vertices.Add(new XnaVertexPositionColor(new Microsoft.Xna.Framework.Vector3(size, size, size), color));
            vertices.Add(new XnaVertexPositionColor(new Microsoft.Xna.Framework.Vector3(-size, size, size), color));

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
    /// MonoGame implementation of IRoomMeshData.
    /// </summary>
    public class MonoGameRoomMeshData : IRoomMeshData
    {
        public IVertexBuffer VertexBuffer { get; set; }
        public IIndexBuffer IndexBuffer { get; set; }
        public int IndexCount { get; set; }
    }
}


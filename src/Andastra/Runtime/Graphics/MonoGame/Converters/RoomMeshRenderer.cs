using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Formats.MDLData;
using JetBrains.Annotations;

namespace Andastra.Runtime.MonoGame.Converters
{
    /// <summary>
    /// Renders room meshes from MDL models.
    /// TODO: SIMPLIFIED - For a quick demo, extracts basic geometry and renders with MonoGame.
    /// </summary>
    /// <remarks>
    /// Room Mesh Renderer:
    /// - Based on swkotor2.exe room rendering system
    /// - Located via string references: "Rooms" @ 0x007bd490 (room list), "RoomName" @ 0x007bd484 (room name field)
    /// - "roomcount" @ 0x007b96c0 (room count field), "gui3D_room" @ 0x007cc144 (room GUI)
    /// - Original implementation: Renders room MDL models positioned according to LYT layout
    /// - Room models: MDL files containing static geometry for area rooms
    /// - Room positioning: Rooms positioned in 3D space according to LYT file room positions
    /// - Room rendering: Original engine renders rooms with lighting, fog, and visibility culling
    /// - MDL parsing: Extracts vertices, faces, and materials from MDL file format
    /// - This implementation: TODO: SIMPLIFIED - Full MDL parsing with all nodes, materials, textures, animations
    /// - Based on MDL file format documentation in vendor/PyKotor/wiki/MDL-File-Format.md
    /// </remarks>
    public class RoomMeshRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, RoomMeshData> _loadedMeshes;

        public RoomMeshRenderer([NotNull] GraphicsDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }

            _graphicsDevice = device;
            _loadedMeshes = new Dictionary<string, RoomMeshData>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads a room mesh from an MDL model.
        /// </summary>
        public RoomMeshData LoadRoomMesh(string modelResRef, MDL mdl)
        {
            if (string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            if (_loadedMeshes.TryGetValue(modelResRef, out RoomMeshData cached))
            {
                return cached;
            }

            if (mdl == null)
            {
                return null;
            }

            // Extract geometry from MDL
            var meshData = new RoomMeshData();
            var vertices = new List<VertexPositionColor>();
            var indices = new List<int>();

            // TODO: SIMPLIFIED - For a quick demo, extract basic geometry from the first trimesh node
            // TODO: Full MDL parsing with all nodes, materials, etc.
            ExtractBasicGeometry(mdl, vertices, indices);

            if (vertices.Count == 0 || indices.Count == 0)
            {
                return null;
            }

            // Create vertex buffer
            meshData.VertexBuffer = new VertexBuffer(_graphicsDevice, typeof(VertexPositionColor), vertices.Count, BufferUsage.WriteOnly);
            meshData.VertexBuffer.SetData(vertices.ToArray());

            // Create index buffer
            meshData.IndexCount = indices.Count;
            meshData.IndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
            meshData.IndexBuffer.SetData(indices.ToArray());

            _loadedMeshes[modelResRef] = meshData;
            return meshData;
        }

        /// <summary>
        /// TODO: SIMPLIFIED - Extracts basic geometry from MDL (simplified for quick demo).
        /// TODO: SIMPLIFIED - Full MDL parsing with all nodes, materials, textures, animations
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py
        /// </summary>
        private void ExtractBasicGeometry(MDL mdl, List<VertexPositionColor> vertices, List<int> indices)
        {
            if (mdl == null || mdl.Root == null)
            {
                CreatePlaceholderBox(vertices, indices);
                return;
            }

            // Extract geometry from all mesh nodes recursively
            ExtractNodeGeometry(mdl.Root, Matrix.Identity, vertices, indices);

            // TODO: PLACEHOLDER - If no geometry found, create placeholder box (should load actual mesh or fail gracefully)
            if (vertices.Count == 0 || indices.Count == 0)
            {
                CreatePlaceholderBox(vertices, indices);
            }
        }

        /// <summary>
        /// Recursively extracts geometry from MDL nodes.
        /// </summary>
        private void ExtractNodeGeometry(MDLNode node, Matrix parentTransform, List<VertexPositionColor> vertices, List<int> indices)
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

        /// <summary>
        /// Extracts vertices and faces from an MDLMesh.
        /// </summary>
        private void ExtractMeshGeometry(MDLMesh mesh, Matrix transform, List<VertexPositionColor> vertices, List<int> indices)
        {
            if (mesh == null || mesh.Vertices == null || mesh.Faces == null)
            {
                return;
            }

            int baseVertexIndex = vertices.Count;
            Color meshColor = Color.Gray;

            // Transform and add vertices
            foreach (System.Numerics.Vector3 vertex in mesh.Vertices)
            {
                // Transform vertex position
                var transformedPos = Microsoft.Xna.Framework.Vector3.Transform(
                    new Microsoft.Xna.Framework.Vector3(vertex.X, vertex.Y, vertex.Z),
                    transform
                );
                vertices.Add(new VertexPositionColor(transformedPos, meshColor));
            }

            // Add faces as indices
            foreach (MDLFace face in mesh.Faces)
            {
                // MDL faces are 0-indexed in Andastra.Parsing
                // Ensure indices are within valid range
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

        /// <summary>
        /// TODO: PLACEHOLDER - Creates a simple placeholder box mesh.
        /// </summary>
        private void CreatePlaceholderBox(List<VertexPositionColor> vertices, List<int> indices)
        {
            float size = 5f;
            Color color = Color.Gray;

            // 8 vertices of a box
            vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-size, -size, -size), color));
            vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(size, -size, -size), color));
            vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(size, size, -size), color));
            vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-size, size, -size), color));
            vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-size, -size, size), color));
            vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(size, -size, size), color));
            vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(size, size, size), color));
            vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-size, size, size), color));

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

        /// <summary>
        /// Clears all loaded meshes.
        /// </summary>
        public void Clear()
        {
            foreach (RoomMeshData mesh in _loadedMeshes.Values)
            {
                mesh.VertexBuffer?.Dispose();
                mesh.IndexBuffer?.Dispose();
            }
            _loadedMeshes.Clear();
        }
    }

    /// <summary>
    /// Stores mesh data for a room.
    /// </summary>
    public class RoomMeshData
    {
        public VertexBuffer VertexBuffer { get; set; }
        public IndexBuffer IndexBuffer { get; set; }
        public int IndexCount { get; set; }
    }
}


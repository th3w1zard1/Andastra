using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using CSharpKOTOR.Formats.MDLData;
using JetBrains.Annotations;
using Vector2 = Stride.Core.Mathematics.Vector2;
using Vector3 = Stride.Core.Mathematics.Vector3;
using Quaternion = Stride.Core.Mathematics.Quaternion;
using Buffer = Stride.Graphics.Buffer;

namespace Odyssey.Stride.Converters
{
    /// <summary>
    /// Converts CSharpKOTOR MDL model data to Stride Graphics Mesh/Model.
    /// Handles trimesh geometry, UV coordinates, and basic material references.
    /// </summary>
    /// <remarks>
    /// Phase 1 implementation focuses on static geometry (trimesh nodes).
    /// Skeletal animation, skinning, and attachment nodes are deferred.
    /// </remarks>
    public class MdlToStrideModelConverter
    {
        private readonly GraphicsDevice _device;
        private readonly Func<string, Material> _materialResolver;

        /// <summary>
        /// Result of model conversion containing all mesh data.
        /// </summary>
        public class ConversionResult
        {
            /// <summary>
            /// List of converted meshes with their transforms.
            /// </summary>
            public List<MeshData> Meshes { get; private set; }

            /// <summary>
            /// Model name from source MDL.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Bounding box minimum.
            /// </summary>
            public Vector3 BoundsMin { get; set; }

            /// <summary>
            /// Bounding box maximum.
            /// </summary>
            public Vector3 BoundsMax { get; set; }

            /// <summary>
            /// List of texture names referenced by this model.
            /// </summary>
            public List<string> TextureReferences { get; private set; }

            public ConversionResult()
            {
                Meshes = new List<MeshData>();
                TextureReferences = new List<string>();
            }
        }

        /// <summary>
        /// Data for a single mesh node.
        /// </summary>
        public class MeshData
        {
            /// <summary>
            /// Node name from MDL.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Local position relative to parent.
            /// </summary>
            public Vector3 Position { get; set; }

            /// <summary>
            /// Local orientation quaternion.
            /// </summary>
            public Quaternion Orientation { get; set; }

            /// <summary>
            /// Stride mesh draw data.
            /// </summary>
            public MeshDraw MeshDraw { get; set; }

            /// <summary>
            /// Primary diffuse texture name.
            /// </summary>
            public string Texture1 { get; set; }

            /// <summary>
            /// Lightmap texture name.
            /// </summary>
            public string Texture2 { get; set; }

            /// <summary>
            /// Whether this mesh uses a lightmap.
            /// </summary>
            public bool HasLightmap { get; set; }

            /// <summary>
            /// Diffuse color.
            /// </summary>
            public Color4 DiffuseColor { get; set; }

            /// <summary>
            /// Ambient color.
            /// </summary>
            public Color4 AmbientColor { get; set; }

            /// <summary>
            /// Whether to render this mesh.
            /// </summary>
            public bool Render { get; set; }

            /// <summary>
            /// Child mesh nodes.
            /// </summary>
            public List<MeshData> Children { get; private set; }

            public MeshData()
            {
                Render = true;
                Orientation = Quaternion.Identity;
                DiffuseColor = Color4.White;
                AmbientColor = new Color4(0.2f, 0.2f, 0.2f, 1f);
                Children = new List<MeshData>();
            }
        }

        /// <summary>
        /// Creates a new model converter.
        /// </summary>
        /// <param name="device">Graphics device for creating GPU resources.</param>
        /// <param name="materialResolver">Optional function to resolve texture names to materials.</param>
        public MdlToStrideModelConverter([NotNull] GraphicsDevice device, Func<string, Material> materialResolver = null)
        {
            _device = device ?? throw new ArgumentNullException("device");
            _materialResolver = materialResolver;
        }

        /// <summary>
        /// Converts an MDL model to Stride mesh data.
        /// </summary>
        /// <param name="mdl">The source MDL model.</param>
        /// <returns>Conversion result with all mesh data.</returns>
        public ConversionResult Convert([NotNull] MDL mdl)
        {
            if (mdl == null)
            {
                throw new ArgumentNullException("mdl");
            }

            var result = new ConversionResult
            {
                Name = mdl.Name,
                BoundsMin = new Vector3(mdl.BMin.X, mdl.BMin.Y, mdl.BMin.Z),
                BoundsMax = new Vector3(mdl.BMax.X, mdl.BMax.Y, mdl.BMax.Z)
            };

            // Convert node hierarchy starting from root
            if (mdl.Root != null)
            {
                ConvertNode(mdl.Root, result, result.Meshes);
            }

            return result;
        }

        private void ConvertNode(MDLNode node, ConversionResult result, List<MeshData> parentList)
        {
            var meshData = new MeshData
            {
                Name = node.Name,
                Position = new Vector3(node.Position.X, node.Position.Y, node.Position.Z),
                Orientation = new Quaternion(
                    node.Orientation.X,
                    node.Orientation.Y,
                    node.Orientation.Z,
                    node.Orientation.W)
            };

            // Check if this node has mesh data (trimesh)
            if (node.Mesh != null && node.Mesh.Vertices.Count > 0 && node.Mesh.Faces.Count > 0)
            {
                meshData.MeshDraw = ConvertMesh(node.Mesh);
                meshData.Texture1 = CleanTextureName(node.Mesh.Texture1);
                meshData.Texture2 = CleanTextureName(node.Mesh.Texture2);
                meshData.HasLightmap = node.Mesh.Lightmapped > 0.5f;
                meshData.Render = node.Mesh.Render != 0;

                meshData.DiffuseColor = new Color4(
                    node.Mesh.Diffuse.X,
                    node.Mesh.Diffuse.Y,
                    node.Mesh.Diffuse.Z,
                    1f);

                meshData.AmbientColor = new Color4(
                    node.Mesh.Ambient.X,
                    node.Mesh.Ambient.Y,
                    node.Mesh.Ambient.Z,
                    1f);

                // Track texture references
                if (!string.IsNullOrEmpty(meshData.Texture1) && !result.TextureReferences.Contains(meshData.Texture1))
                {
                    result.TextureReferences.Add(meshData.Texture1);
                }
                if (!string.IsNullOrEmpty(meshData.Texture2) && !result.TextureReferences.Contains(meshData.Texture2))
                {
                    result.TextureReferences.Add(meshData.Texture2);
                }

                parentList.Add(meshData);
            }

            // Convert children recursively
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (meshData.MeshDraw != null)
                    {
                        // Child of a mesh node
                        ConvertNode(child, result, meshData.Children);
                    }
                    else
                    {
                        // Empty transform node - attach children to same level
                        ConvertNode(child, result, parentList);
                    }
                }
            }
        }

        private MeshDraw ConvertMesh(MDLMesh mesh)
        {
            // Build vertex and index data
            var vertices = new List<VertexPositionNormalTexture>();
            var indices = new List<int>();

            // Map from original vertex index to new index
            var vertexMap = new Dictionary<int, int>();

            foreach (var face in mesh.Faces)
            {
                // Get or create vertices for this face
                int idx0 = GetOrCreateVertex(vertices, vertexMap, mesh, face.V1);
                int idx1 = GetOrCreateVertex(vertices, vertexMap, mesh, face.V2);
                int idx2 = GetOrCreateVertex(vertices, vertexMap, mesh, face.V3);

                indices.Add(idx0);
                indices.Add(idx1);
                indices.Add(idx2);
            }

            if (vertices.Count == 0 || indices.Count == 0)
            {
                return null;
            }

            // Create vertex buffer
            var vertexBuffer = Buffer.New(
                _device,
                vertices.ToArray(),
                BufferFlags.VertexBuffer,
                GraphicsResourceUsage.Immutable);

            // Create index buffer
            bool use32BitIndices = vertices.Count > 65535;
            Buffer indexBuffer;

            if (use32BitIndices)
            {
                indexBuffer = Buffer.New(
                    _device,
                    indices.ToArray(),
                    BufferFlags.IndexBuffer,
                    GraphicsResourceUsage.Immutable);
            }
            else
            {
                var shortIndices = new ushort[indices.Count];
                for (int i = 0; i < indices.Count; i++)
                {
                    shortIndices[i] = (ushort)indices[i];
                }
                indexBuffer = Buffer.New(
                    _device,
                    shortIndices,
                    BufferFlags.IndexBuffer,
                    GraphicsResourceUsage.Immutable);
            }

            // Create mesh draw
            var meshDraw = new MeshDraw
            {
                PrimitiveType = PrimitiveType.TriangleList,
                DrawCount = indices.Count,
                IndexBuffer = new IndexBufferBinding(
                    indexBuffer,
                    use32BitIndices,
                    indices.Count),
                VertexBuffers = new[]
                {
                    new VertexBufferBinding(
                        vertexBuffer,
                        VertexPositionNormalTexture.Layout,
                        vertices.Count)
                }
            };

            return meshDraw;
        }

        private int GetOrCreateVertex(
            List<VertexPositionNormalTexture> vertices,
            Dictionary<int, int> vertexMap,
            MDLMesh mesh,
            int originalIndex)
        {
            if (vertexMap.TryGetValue(originalIndex, out int existingIndex))
            {
                return existingIndex;
            }

            // Get position
            var pos = mesh.Vertices[originalIndex];
            var position = new Vector3(pos.X, pos.Y, pos.Z);

            // Get normal (if available)
            Vector3 normal = Vector3.UnitY;
            if (mesh.Normals.Count > originalIndex)
            {
                var n = mesh.Normals[originalIndex];
                normal = new Vector3(n.X, n.Y, n.Z);
            }

            // Get UV (if available)
            Vector2 uv = Vector2.Zero;
            if (mesh.UV1.Count > originalIndex)
            {
                var t = mesh.UV1[originalIndex];
                uv = new Vector2(t.X, 1f - t.Y); // Flip V for DirectX/Stride
            }

            var vertex = new VertexPositionNormalTexture(position, normal, uv);
            int newIndex = vertices.Count;
            vertices.Add(vertex);
            vertexMap[originalIndex] = newIndex;

            return newIndex;
        }

        private string CleanTextureName(string name)
        {
            if (string.IsNullOrEmpty(name) ||
                string.Equals(name, "NULL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return name.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Simple vertex structure for MDL mesh data.
    /// </summary>
    public struct VertexPositionNormalTexture
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;

        public static readonly VertexDeclaration Layout = new VertexDeclaration(
            VertexElement.Position<Vector3>(),
            VertexElement.Normal<Vector3>(),
            VertexElement.TextureCoordinate<Vector2>());

        public VertexPositionNormalTexture(Vector3 position, Vector3 normal, Vector2 texCoord)
        {
            Position = position;
            Normal = normal;
            TextureCoordinate = texCoord;
        }
    }
}


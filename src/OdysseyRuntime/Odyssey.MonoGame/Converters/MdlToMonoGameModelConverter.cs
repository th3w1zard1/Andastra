using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CSharpKOTOR.Formats.MDLData;
using JetBrains.Annotations;

namespace Odyssey.MonoGame.Converters
{
    /// <summary>
    /// Converts CSharpKOTOR MDL model data to MonoGame Model.
    /// Handles trimesh geometry, UV coordinates, and basic material references.
    /// </summary>
    /// <remarks>
    /// MDL to MonoGame Model Converter:
    /// - Based on swkotor2.exe model loading system (modern MonoGame adaptation)
    /// - Original implementation: KOTOR loads MDL/MDX files and renders with DirectX 8/9 APIs
    /// - MDL format: Binary model format containing trimesh nodes, bones, animations
    /// - MDX format: Binary geometry format containing vertex positions, normals, UVs, indices
    /// - Original engine: Uses DirectX vertex/index buffers, materials with Blinn-Phong shading
    /// - This MonoGame implementation: Converts to MonoGame Model/VertexBuffer/IndexBuffer structures
    /// - Geometry: Extracts trimesh nodes from MDL, vertex data from MDX, creates MonoGame buffers
    /// - Materials: Converts KOTOR material references to MonoGame BasicEffect or PBR materials
    /// - Note: Original engine used DirectX APIs, this is a modern MonoGame adaptation
    /// </remarks>
    /// <remarks>
    /// IMPORTANT: For optimal performance, use the new MDL loading pipeline:
    /// 
    /// <code>
    /// // New optimized approach (recommended):
    /// using Odyssey.Content.MDL;
    /// using Odyssey.MonoGame.Models;
    /// 
    /// var mdlData = resourceProvider.GetResource(resRef, "mdl");
    /// var mdxData = resourceProvider.GetResource(resRef, "mdx");
    /// using (var reader = new MDLFastReader(mdlData, mdxData))
    /// {
    ///     var model = reader.Load();
    ///     var converter = new MDLModelConverter(graphicsDevice);
    ///     var result = converter.Convert(model);
    ///     // Use result.MeshParts for rendering
    /// }
    /// </code>
    /// 
    /// The new pipeline provides:
    /// - 64KB buffered I/O for efficient disk access
    /// - Pre-allocated arrays based on header counts
    /// - Single-pass header reading followed by batch data loading
    /// - Direct MDX vertex data extraction to GPU buffers
    /// 
    /// See: Odyssey.Content.MDL.MDLFastReader and Odyssey.MonoGame.Models.MDLModelConverter
    /// </remarks>
    public class MdlToMonoGameModelConverter
    {
        private readonly GraphicsDevice _device;
        private readonly Func<string, BasicEffect> _materialResolver;

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

            public ConversionResult()
            {
                Meshes = new List<MeshData>();
            }
        }

        /// <summary>
        /// Mesh data for a single converted mesh.
        /// </summary>
        public class MeshData
        {
            /// <summary>
            /// Vertex buffer containing position, normal, UV data.
            /// </summary>
            public VertexBuffer VertexBuffer { get; set; }

            /// <summary>
            /// Index buffer for triangle indices.
            /// </summary>
            public IndexBuffer IndexBuffer { get; set; }

            /// <summary>
            /// Number of indices to draw.
            /// </summary>
            public int IndexCount { get; set; }

            /// <summary>
            /// Material effect for rendering.
            /// </summary>
            public BasicEffect Effect { get; set; }

            /// <summary>
            /// World transform matrix.
            /// </summary>
            public Matrix WorldTransform { get; set; }

            /// <summary>
            /// Primary texture name.
            /// </summary>
            public string TextureName { get; set; }
        }

        public MdlToMonoGameModelConverter([NotNull] GraphicsDevice device, [NotNull] Func<string, BasicEffect> materialResolver)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }
            if (materialResolver == null)
            {
                throw new ArgumentNullException("materialResolver");
            }

            _device = device;
            _materialResolver = materialResolver;
        }

        /// <summary>
        /// Converts a legacy CSharpKOTOR MDL model to MonoGame rendering structures.
        /// 
        /// NOTE: For better performance, use MDLFastReader + MDLModelConverter instead.
        /// </summary>
        public ConversionResult Convert([NotNull] MDL mdl)
        {
            if (mdl == null)
            {
                throw new ArgumentNullException("mdl");
            }

            var result = new ConversionResult
            {
                Name = mdl.Name ?? "Unnamed"
            };

            // Legacy conversion - traverse node hierarchy
            if (mdl.Root != null)
            {
                ConvertNodeHierarchy(mdl.Root, Matrix.Identity, result.Meshes);
            }

            Console.WriteLine("[MdlToMonoGameModelConverter] Converted model: " + result.Name + 
                " with " + result.Meshes.Count + " mesh parts");

            return result;
        }

        private void ConvertNodeHierarchy(MDLNode node, Matrix parentTransform, List<MeshData> meshes)
        {
            // Calculate local transform
            Matrix localTransform = CreateNodeTransform(node);
            Matrix worldTransform = localTransform * parentTransform;

            // Convert mesh if present
            if (node.Mesh != null && node.Mesh.Vertices != null && node.Mesh.Vertices.Count > 0)
            {
                MeshData meshData = ConvertMesh(node.Mesh, worldTransform);
                if (meshData != null)
                {
                    meshes.Add(meshData);
                }
            }

            // Process children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ConvertNodeHierarchy(child, worldTransform, meshes);
                }
            }
        }

        private Matrix CreateNodeTransform(MDLNode node)
        {
            // Create rotation from quaternion
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

            return Matrix.CreateFromQuaternion(rotation) * Matrix.CreateTranslation(translation);
        }

        private MeshData ConvertMesh(MDLMesh mesh, Matrix worldTransform)
        {
            if (mesh.Vertices == null || mesh.Vertices.Count == 0)
            {
                return null;
            }

            if (mesh.Faces == null || mesh.Faces.Count == 0)
            {
                return null;
            }

            // Build vertex array
            var vertices = new VertexPositionNormalTexture[mesh.Vertices.Count];
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                Vector3 pos = new Vector3(mesh.Vertices[i].X, mesh.Vertices[i].Y, mesh.Vertices[i].Z);
                Vector3 normal = Vector3.Up;
                Vector2 texCoord = Vector2.Zero;

                if (mesh.Normals != null && i < mesh.Normals.Count)
                {
                    normal = new Vector3(mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z);
                }

                if (mesh.UV1 != null && i < mesh.UV1.Count)
                {
                    texCoord = new Vector2(mesh.UV1[i].X, mesh.UV1[i].Y);
                }

                vertices[i] = new VertexPositionNormalTexture(pos, normal, texCoord);
            }

            // Build index array
            var indices = new short[mesh.Faces.Count * 3];
            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                indices[i * 3 + 0] = (short)mesh.Faces[i].V1;
                indices[i * 3 + 1] = (short)mesh.Faces[i].V2;
                indices[i * 3 + 2] = (short)mesh.Faces[i].V3;
            }

            // Create GPU buffers
            VertexBuffer vertexBuffer = new VertexBuffer(
                _device,
                typeof(VertexPositionNormalTexture),
                vertices.Length,
                BufferUsage.WriteOnly
            );
            vertexBuffer.SetData(vertices);

            IndexBuffer indexBuffer = new IndexBuffer(
                _device,
                IndexElementSize.SixteenBit,
                indices.Length,
                BufferUsage.WriteOnly
            );
            indexBuffer.SetData(indices);

            // Get texture name
            string textureName = null;
            if (!string.IsNullOrEmpty(mesh.Texture1) && 
                mesh.Texture1.ToLowerInvariant() != "null" &&
                mesh.Texture1.ToLowerInvariant() != "none")
            {
                textureName = mesh.Texture1.ToLowerInvariant();
            }

            var meshData = new MeshData
            {
                VertexBuffer = vertexBuffer,
                IndexBuffer = indexBuffer,
                IndexCount = indices.Length,
                WorldTransform = worldTransform,
                TextureName = textureName
            };

            // Resolve effect
            if (textureName != null)
            {
                meshData.Effect = _materialResolver(textureName);
            }

            return meshData;
        }
    }
}

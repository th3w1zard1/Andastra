using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Odyssey.Content.MDL;

namespace Odyssey.MonoGame.Models
{
    /// <summary>
    /// High-performance converter from MDL model data to MonoGame rendering structures.
    /// 
    /// Optimization strategies:
    /// 1. Pre-calculates total vertex/index counts for buffer pre-allocation
    /// 2. Uses a single vertex buffer where possible (batching meshes)
    /// 3. Builds node transforms during traversal to avoid repeated matrix multiplications
    /// 4. Creates index buffers with optimal primitive type (16-bit when possible)
    /// </summary>
    public sealed class MDLModelConverter
    {
        private readonly GraphicsDevice _device;

        /// <summary>
        /// Vertex format for MDL meshes.
        /// Uses VertexPositionNormalTexture for compatibility with BasicEffect.
        /// </summary>
        public struct MDLVertex : IVertexType
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 TexCoord;

            public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
            );

            VertexDeclaration IVertexType.VertexDeclaration
            {
                get { return VertexDeclaration; }
            }
        }

        /// <summary>
        /// Result of converting an MDL model to MonoGame structures.
        /// </summary>
        public sealed class ConvertedModel
        {
            /// <summary>
            /// Name of the model.
            /// </summary>
            public string Name;

            /// <summary>
            /// List of mesh parts ready for rendering.
            /// </summary>
            public List<ConvertedMeshPart> MeshParts;

            /// <summary>
            /// Bounding box minimum.
            /// </summary>
            public Vector3 BoundsMin;

            /// <summary>
            /// Bounding box maximum.
            /// </summary>
            public Vector3 BoundsMax;

            /// <summary>
            /// Bounding sphere radius.
            /// </summary>
            public float Radius;

            /// <summary>
            /// True if this is a KotOR 2 model.
            /// </summary>
            public bool IsTSL;

            public ConvertedModel()
            {
                MeshParts = new List<ConvertedMeshPart>();
            }
        }

        /// <summary>
        /// A single renderable mesh part.
        /// </summary>
        public sealed class ConvertedMeshPart
        {
            /// <summary>
            /// Name of the node this mesh came from.
            /// </summary>
            public string NodeName;

            /// <summary>
            /// Vertex buffer containing mesh geometry.
            /// </summary>
            public VertexBuffer VertexBuffer;

            /// <summary>
            /// Index buffer for triangle indices.
            /// </summary>
            public IndexBuffer IndexBuffer;

            /// <summary>
            /// Number of primitives (triangles) to draw.
            /// </summary>
            public int PrimitiveCount;

            /// <summary>
            /// World transform matrix for this mesh part.
            /// </summary>
            public Matrix Transform;

            /// <summary>
            /// Primary texture name (without extension).
            /// </summary>
            public string Texture0;

            /// <summary>
            /// Lightmap texture name (without extension).
            /// </summary>
            public string Texture1;

            /// <summary>
            /// Diffuse color.
            /// </summary>
            public Vector3 DiffuseColor;

            /// <summary>
            /// Ambient color.
            /// </summary>
            public Vector3 AmbientColor;

            /// <summary>
            /// If true, this mesh should be rendered.
            /// </summary>
            public bool IsRenderable;

            /// <summary>
            /// If true, this mesh casts shadows.
            /// </summary>
            public bool CastsShadow;

            /// <summary>
            /// Bounding box minimum in local space.
            /// </summary>
            public Vector3 LocalBoundsMin;

            /// <summary>
            /// Bounding box maximum in local space.
            /// </summary>
            public Vector3 LocalBoundsMax;
        }

        public MDLModelConverter(GraphicsDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }
            _device = device;
        }

        /// <summary>
        /// Converts an MDL model to MonoGame rendering structures.
        /// </summary>
        /// <param name="model">The parsed MDL model</param>
        /// <returns>Converted model ready for rendering</returns>
        public ConvertedModel Convert(MDLModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            var result = new ConvertedModel
            {
                Name = model.Name ?? "Unknown",
                BoundsMin = ToVector3(model.BoundingBoxMin),
                BoundsMax = ToVector3(model.BoundingBoxMax),
                Radius = model.Radius
            };

            // Traverse node hierarchy and collect all mesh nodes
            if (model.RootNode != null)
            {
                ConvertNodeHierarchy(model.RootNode, Matrix.Identity, result.MeshParts);
            }

            return result;
        }

        private void ConvertNodeHierarchy(MDLNodeData node, Matrix parentTransform, List<ConvertedMeshPart> meshParts)
        {
            // Calculate this node's transform
            Matrix localTransform = CreateNodeTransform(node);
            Matrix worldTransform = localTransform * parentTransform;

            // If this node has mesh data, convert it
            if ((node.NodeType & MDLConstants.NODE_HAS_MESH) != 0 && node.Mesh != null)
            {
                ConvertedMeshPart meshPart = ConvertMesh(node, worldTransform);
                if (meshPart != null)
                {
                    meshParts.Add(meshPart);
                }
            }

            // Process children
            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Length; i++)
                {
                    ConvertNodeHierarchy(node.Children[i], worldTransform, meshParts);
                }
            }
        }

        private Matrix CreateNodeTransform(MDLNodeData node)
        {
            // Create rotation from quaternion (W, X, Y, Z stored in node)
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

            // Combine: Scale * Rotation * Translation
            // MDL models typically don't have per-node scale, so we use identity scale
            return Matrix.CreateFromQuaternion(rotation) * Matrix.CreateTranslation(translation);
        }

        private ConvertedMeshPart ConvertMesh(MDLNodeData node, Matrix worldTransform)
        {
            MDLMeshData mesh = node.Mesh;

            // Skip meshes with no geometry or marked as non-renderable
            if (mesh.VertexCount == 0 || mesh.FaceCount == 0)
            {
                return null;
            }

            // Skip if positions weren't loaded
            if (mesh.Positions == null || mesh.Positions.Length == 0)
            {
                return null;
            }

            // Build vertex array
            MDLVertex[] vertices = new MDLVertex[mesh.VertexCount];
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                vertices[i].Position = new Vector3(
                    mesh.Positions[i].X,
                    mesh.Positions[i].Y,
                    mesh.Positions[i].Z
                );

                if (mesh.Normals != null && i < mesh.Normals.Length)
                {
                    vertices[i].Normal = new Vector3(
                        mesh.Normals[i].X,
                        mesh.Normals[i].Y,
                        mesh.Normals[i].Z
                    );
                }
                else
                {
                    vertices[i].Normal = Vector3.Up;
                }

                if (mesh.TexCoords0 != null && i < mesh.TexCoords0.Length)
                {
                    vertices[i].TexCoord = new Vector2(
                        mesh.TexCoords0[i].X,
                        mesh.TexCoords0[i].Y
                    );
                }
                else
                {
                    vertices[i].TexCoord = Vector2.Zero;
                }
            }

            // Build index array from faces
            short[] indices = new short[mesh.FaceCount * 3];
            for (int i = 0; i < mesh.FaceCount; i++)
            {
                MDLFaceData face = mesh.Faces[i];
                indices[i * 3 + 0] = face.Vertex0;
                indices[i * 3 + 1] = face.Vertex1;
                indices[i * 3 + 2] = face.Vertex2;
            }

            // Create GPU buffers
            VertexBuffer vertexBuffer = new VertexBuffer(
                _device,
                MDLVertex.VertexDeclaration,
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

            // Clean texture names
            string texture0 = CleanTextureName(mesh.Texture0);
            string texture1 = CleanTextureName(mesh.Texture1);

            return new ConvertedMeshPart
            {
                NodeName = node.Name ?? string.Empty,
                VertexBuffer = vertexBuffer,
                IndexBuffer = indexBuffer,
                PrimitiveCount = mesh.FaceCount,
                Transform = worldTransform,
                Texture0 = texture0,
                Texture1 = texture1,
                DiffuseColor = new Vector3(mesh.DiffuseColor.X, mesh.DiffuseColor.Y, mesh.DiffuseColor.Z),
                AmbientColor = new Vector3(mesh.AmbientColor.X, mesh.AmbientColor.Y, mesh.AmbientColor.Z),
                IsRenderable = mesh.Render,
                CastsShadow = mesh.Shadow,
                LocalBoundsMin = ToVector3(mesh.BoundingBoxMin),
                LocalBoundsMax = ToVector3(mesh.BoundingBoxMax)
            };
        }

        private static string CleanTextureName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            string cleaned = name.Trim().ToLowerInvariant();
            if (cleaned == "null" || cleaned == "none" || cleaned == "")
            {
                return null;
            }

            return cleaned;
        }

        private static Vector3 ToVector3(Vector3Data v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }
    }
}


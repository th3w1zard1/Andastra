using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.MDLData;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Converters
{
    /// <summary>
    /// Converts BioWare.NET MDL model data to MonoGame Model.
    /// Handles trimesh geometry, UV coordinates, and basic material references.
    /// </summary>
    /// <remarks>
    /// MDL to MonoGame Model Converter:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) model loading system (modern MonoGame adaptation)
    /// - Located via string references: "Model" @ 0x007c1ca8, "ModelName" @ 0x007c1c8c
    /// - "ModelType" @ 0x007c4568, "MODELTYPE" @ 0x007c036c, "ModelVariation" @ 0x007c0990
    /// - "ModelPart" @ 0x007bd42c, "ModelPart1" @ 0x007c0acc, "refModel" @ 0x007babe8
    /// - "DefaultModel" @ 0x007c4530, "VisibleModel" @ 0x007c1c98
    /// - Model LOD: "MODEL01" @ 0x007c4b48, "MODELMIN01" @ 0x007c4b50
    /// - "MODEL02" @ 0x007c4b34, "MODELMIN02" @ 0x007c4b3c
    /// - "MODEL03" @ 0x007c4b20, "MODELMIN03" @ 0x007c4b28
    /// - "Model%d" @ 0x007cae00 (model format string)
    /// - Model directories: "SUPERMODELS" @ 0x007c69b0, ".\supermodels" @ 0x007c69bc, "d:\supermodels" @ 0x007c69cc
    /// - "SUPERMODELS:smseta" @ 0x007c7380, "SUPERMODELS:smsetb" @ 0x007c7394, "SUPERMODELS:smsetc" @ 0x007c73a8
    /// - Special models: "ProjModel" @ 0x007c31c0, "StuntModel" @ 0x007c37e0, "CameraModel" @ 0x007c3908
    /// - "Bullet_Model" @ 0x007cb664, "Gun_Model" @ 0x007cb67c, "RotatingModel" @ 0x007cb928
    /// - "Models" @ 0x007cb938, "modelhook" @ 0x007cb3b4
    /// - Model variables: "LongMdlVar" @ 0x007d05f4, "ShortMdlVar" @ 0x007d05e8, "DoubleMdlVar" @ 0x007d05d8
    /// - GUI: "3D_MODEL" @ 0x007d0954, "3D_MODEL_LS" @ 0x007d0948, "3D_MODEL%d" @ 0x007d1924
    /// - "3D_PlanetModel" @ 0x007cda04, "MODEL_LBL" @ 0x007d27ac
    /// - Error messages:
    ///   - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc
    ///   - "Model %s nor the default model %s could be loaded." @ 0x007cad14
    ///   - "CSWCVisualEffect::LoadModel: Failed to load visual effect model '%s'." @ 0x007cd5a8
    ///   - "CSWCCreatureAppearance::CreateBTypeBody(): Failed to load model '%s'." @ 0x007cdc40
    ///   - "Cannot load door model '%s'." @ 0x007d2488
    ///   - "CSWCAnimBase::LoadModel(): The headconjure dummy has an orientation....It shouldn't!!  The %s model needs to be fixed or else the spell visuals will not be correct." @ 0x007ce278
    ///   - "CSWCAnimBase::LoadModel(): The handconjure dummy has an orientation....It shouldn't!!  The %s model needs to be fixed or else the spell visuals will not be correct." @ 0x007ce320
    /// - Fixed: headconjure and handconjure dummy nodes are forced to identity orientation (0,0,0,1) to ensure spell visuals work correctly
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_006f8590 @ 0x006f8590 checks for headconjure/handconjure nodes and validates orientation
    /// - Original implementation: KOTOR loads MDL/MDX files and renders with DirectX 8/9 APIs
    /// - MDL format: Binary model format containing trimesh nodes, bones, animations
    /// - MDX format: Binary geometry format containing vertex positions, normals, UVs, indices
    /// - Original engine: Uses DirectX vertex/index buffers, materials with Blinn-Phong shading
    /// - This MonoGame implementation: Converts to MonoGame Model/VertexBuffer/IndexBuffer structures
    /// - Geometry: Extracts trimesh nodes from MDL, vertex data from MDX, creates MonoGame buffers
    /// - Materials: Converts KOTOR material references to MonoGame BasicEffect or PBR materials
    /// - Model LOD: Models can have multiple LOD levels (MODEL01-03) for distance-based rendering
    /// - Supermodels: Special model sets (smseta, smsetb, smsetc) for creature appearance variations
    /// - Note: Original engine used DirectX APIs, this is a modern MonoGame adaptation
    /// </remarks>
    /// <remarks>
    /// IMPORTANT: For optimal performance, use the new MDL loading pipeline:
    /// 
    /// <code>
    /// // New optimized approach (recommended):
    /// using Andastra.Runtime.Content.MDL;
    /// using Andastra.Runtime.MonoGame.Models;
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
        /// Converts a legacy BioWare.NET MDL model to MonoGame rendering structures.
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_006f8590 @ 0x006f8590
            // The headconjure and handconjure dummy nodes must have identity orientation (0,0,0,1)
            // Otherwise spell visuals will not be correct
            // Original engine checks: if (node->GetNode("headconjure") && orientation != identity) -> error
            // Original engine checks: if (node->GetNode("handconjure") && orientation != identity) -> error
            // Fix: Force identity orientation for these nodes to match original engine behavior
            Quaternion rotation;
            if (!string.IsNullOrEmpty(node.Name))
            {
                string nodeNameLower = node.Name.ToLowerInvariant();
                if (nodeNameLower == "headconjure" || nodeNameLower == "handconjure")
                {
                    // Force identity orientation for spell visual attachment points
                    // These dummy nodes should not have any rotation - they're just attachment points
                    // swkotor2.exe: FUN_006f8590 validates that these nodes have identity quaternion (0,0,0,1)
                    rotation = Quaternion.Identity;
                }
                else
                {
                    // Use node's orientation for all other nodes
                    rotation = new Quaternion(
                        node.Orientation.X,
                        node.Orientation.Y,
                        node.Orientation.Z,
                        node.Orientation.W
                    );
                }
            }
            else
            {
                // Use node's orientation if name is empty
                rotation = new Quaternion(
                    node.Orientation.X,
                    node.Orientation.Y,
                    node.Orientation.Z,
                    node.Orientation.W
                );
            }

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
                IndexElementSize.SixteenBits,
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

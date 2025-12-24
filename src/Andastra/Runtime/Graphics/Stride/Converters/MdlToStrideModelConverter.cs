using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing.Formats.MDLData;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Stride.Graphics;
using JetBrains.Annotations;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Andastra.Runtime.Stride.Converters
{
    /// <summary>
    /// Converts Andastra.Parsing MDL model data to Stride rendering structures.
    /// Handles trimesh geometry, UV coordinates, and basic material references.
    /// </summary>
    /// <remarks>
    /// MDL to Stride Model Converter:
    /// - Cross-Engine Analysis (Reverse Engineered via Ghidra):
    ///   - Odyssey (swkotor.exe, swkotor2.exe):
    ///     - Model loading: FUN_005261b0 @ 0x005261b0 (swkotor2.exe) loads creature models
    ///     - String references: "Model" @ 0x007c1ca8, "ModelName" @ 0x007c1c8c (swkotor2.exe)
    ///     - "ModelType" @ 0x007c4568, "MODELTYPE" @ 0x007c036c, "ModelVariation" @ 0x007c0990
    ///     - "ModelPart" @ 0x007bd42c, "ModelPart1" @ 0x007c0acc, "refModel" @ 0x007babe8
    ///     - "DefaultModel" @ 0x007c4530, "VisibleModel" @ 0x007c1c98
    ///     - Model LOD: "MODEL01" @ 0x007c4b48, "MODELMIN01" @ 0x007c4b50
    ///     - "MODEL02" @ 0x007c4b34, "MODELMIN02" @ 0x007c4b3c
    ///     - "MODEL03" @ 0x007c4b20, "MODELMIN03" @ 0x007c4b28
    ///     - Model directories: "SUPERMODELS" @ 0x007c69b0, ".\supermodels" @ 0x007c69bc
    ///     - Special models: "ProjModel" @ 0x007c31c0, "StuntModel" @ 0x007c37e0, "CameraModel" @ 0x007c3908
    ///     - Error messages:
    ///       - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (swkotor2.exe)
    ///       - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x0074f85c (swkotor.exe)
    ///       - "Model %s nor the default model %s could be loaded." @ 0x007cad14
    ///       - "CSWCAnimBase::LoadModel(): The headconjure dummy has an orientation....It shouldn't!!  The %s model needs to be fixed or else the spell visuals will not be correct." @ 0x007ce278
    ///       - "CSWCAnimBase::LoadModel(): The handconjure dummy has an orientation....It shouldn't!!  The %s model needs to be fixed or else the spell visuals will not be correct." @ 0x007ce320
    ///     - Fixed: headconjure and handconjure dummy nodes are forced to identity orientation (0,0,0,1) to ensure spell visuals work correctly
    ///     - Based on swkotor2.exe: FUN_006f8590 @ 0x006f8590 checks for headconjure/handconjure nodes and validates orientation
    ///   - Aurora (nwmain.exe):
    ///     - LoadModel @ 0x1400a0130 - loads Model objects from file streams
    ///     - Uses MaxTree::AsModel for model conversion
    ///     - Model caching via global _Models array
    ///   - Eclipse (daorigins.exe, DragonAge2.exe):
    ///     - Different model formats (not MDL-based)
    /// - Original implementation: KOTOR loads MDL/MDX files and renders with DirectX 8/9 APIs
    /// - MDL format: Binary model format containing trimesh nodes, bones, animations
    /// - MDX format: Binary geometry format containing vertex positions, normals, UVs, indices
    /// - Original engine: Uses DirectX vertex/index buffers, materials with Blinn-Phong shading
    /// - This Stride implementation: Converts to Stride Buffer structures for rendering
    /// - Geometry: Extracts trimesh nodes from MDL, vertex data from MDX, creates Stride buffers
    /// - Materials: Converts KOTOR material references to Stride Material or BasicEffect
    /// - Model LOD: Models can have multiple LOD levels (MODEL01-03) for distance-based rendering
    /// - Supermodels: Special model sets (smseta, smsetb, smsetc) for creature appearance variations
    /// - Note: Original engine used DirectX APIs, this is a modern Stride adaptation
    /// </remarks>
    public class MdlToStrideModelConverter
    {
        private readonly GraphicsDevice _device;
        private readonly Func<string, IBasicEffect> _materialResolver;

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
            public global::Stride.Core.Mathematics.Vector3 BoundsMin { get; set; }

            /// <summary>
            /// Bounding box maximum.
            /// </summary>
            public global::Stride.Core.Mathematics.Vector3 BoundsMax { get; set; }

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
            public IVertexBuffer VertexBuffer { get; set; }

            /// <summary>
            /// Index buffer for triangle indices.
            /// </summary>
            public IIndexBuffer IndexBuffer { get; set; }

            /// <summary>
            /// Number of indices to draw.
            /// </summary>
            public int IndexCount { get; set; }

            /// <summary>
            /// Material effect for rendering.
            /// </summary>
            public IBasicEffect Effect { get; set; }

            /// <summary>
            /// World transform matrix.
            /// </summary>
            public Matrix4x4 WorldTransform { get; set; }

            /// <summary>
            /// Primary texture name.
            /// </summary>
            public string TextureName { get; set; }
        }

        /// <summary>
        /// Standard vertex format for MDL meshes in Stride.
        /// Uses Position, Normal, TextureCoordinate for compatibility with Stride rendering.
        /// </summary>
        public struct VertexPositionNormalTexture
        {
            public System.Numerics.Vector3 Position;
            public System.Numerics.Vector3 Normal;
            public System.Numerics.Vector2 TexCoord;

            public VertexPositionNormalTexture(System.Numerics.Vector3 position, System.Numerics.Vector3 normal, System.Numerics.Vector2 texCoord)
            {
                Position = position;
                Normal = normal;
                TexCoord = texCoord;
            }
        }

        public MdlToStrideModelConverter([NotNull] GraphicsDevice device, [NotNull] Func<string, IBasicEffect> materialResolver)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }
            if (materialResolver == null)
            {
                throw new ArgumentNullException(nameof(materialResolver));
            }

            _device = device;
            _materialResolver = materialResolver;
        }

        /// <summary>
        /// Converts a legacy Andastra.Parsing MDL model to Stride rendering structures.
        /// </summary>
        public ConversionResult Convert([NotNull] MDL mdl)
        {
            if (mdl == null)
            {
                throw new ArgumentNullException(nameof(mdl));
            }

            var result = new ConversionResult
            {
                Name = mdl.Name ?? "Unnamed"
            };

            // Legacy conversion - traverse node hierarchy
            if (mdl.Root != null)
            {
                ConvertNodeHierarchy(mdl.Root, Matrix4x4.Identity, result.Meshes);
            }

            Console.WriteLine("[MdlToStrideModelConverter] Converted model: " + result.Name +
                " with " + result.Meshes.Count + " mesh parts");

            return result;
        }

        private void ConvertNodeHierarchy(MDLNode node, Matrix4x4 parentTransform, List<MeshData> meshes)
        {
            // Calculate local transform
            Matrix4x4 localTransform = CreateNodeTransform(node);
            Matrix4x4 worldTransform = Matrix4x4.Multiply(localTransform, parentTransform);

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

        private Matrix4x4 CreateNodeTransform(MDLNode node)
        {
            // Based on swkotor2.exe: FUN_006f8590 @ 0x006f8590
            // The headconjure and handconjure dummy nodes must have identity orientation (0,0,0,1)
            // Otherwise spell visuals will not be correct
            // Original engine checks: if (node->GetNode("headconjure") && orientation != identity) -> error
            // Original engine checks: if (node->GetNode("handconjure") && orientation != identity) -> error
            // Fix: Force identity orientation for these nodes to match original engine behavior
            System.Numerics.Quaternion rotation;
            if (!string.IsNullOrEmpty(node.Name))
            {
                string nodeNameLower = node.Name.ToLowerInvariant();
                if (nodeNameLower == "headconjure" || nodeNameLower == "handconjure")
                {
                    // Force identity orientation for spell visual attachment points
                    // These dummy nodes should not have any rotation - they're just attachment points
                    // swkotor2.exe: FUN_006f8590 validates that these nodes have identity quaternion (0,0,0,1)
                    rotation = System.Numerics.Quaternion.Identity;
                }
                else
                {
                    // Use node's orientation for all other nodes
                    rotation = new System.Numerics.Quaternion(
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
                rotation = new System.Numerics.Quaternion(
                    node.Orientation.X,
                    node.Orientation.Y,
                    node.Orientation.Z,
                    node.Orientation.W
                );
            }

            // Create translation
            System.Numerics.Vector3 translation = new System.Numerics.Vector3(
                node.Position.X,
                node.Position.Y,
                node.Position.Z
            );

            // Create rotation matrix from quaternion
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(translation);

            // Combine: Translation * Rotation (MDL uses this order)
            return Matrix4x4.Multiply(translationMatrix, rotationMatrix);
        }

        private MeshData ConvertMesh(MDLMesh mesh, Matrix4x4 worldTransform)
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
                System.Numerics.Vector3 pos = new System.Numerics.Vector3(mesh.Vertices[i].X, mesh.Vertices[i].Y, mesh.Vertices[i].Z);
                System.Numerics.Vector3 normal = System.Numerics.Vector3.UnitY; // Default to up if no normal
                System.Numerics.Vector2 texCoord = System.Numerics.Vector2.Zero;

                if (mesh.Normals != null && i < mesh.Normals.Count)
                {
                    normal = new System.Numerics.Vector3(mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z);
                }

                if (mesh.UV1 != null && i < mesh.UV1.Count)
                {
                    texCoord = new System.Numerics.Vector2(mesh.UV1[i].X, mesh.UV1[i].Y);
                }

                vertices[i] = new VertexPositionNormalTexture(pos, normal, texCoord);
            }

            // Build index array
            var indices = new int[mesh.Faces.Count * 3];
            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                indices[i * 3 + 0] = mesh.Faces[i].V1;
                indices[i * 3 + 1] = mesh.Faces[i].V2;
                indices[i * 3 + 2] = mesh.Faces[i].V3;
            }

            // Create Stride vertex buffer
            int vertexStride = System.Runtime.InteropServices.Marshal.SizeOf<VertexPositionNormalTexture>();
            global::Stride.Graphics.Buffer vertexBuffer = global::Stride.Graphics.Buffer.Vertex.New(
                _device,
                vertices,
                GraphicsResourceUsage.Default
            );

            // Create Stride index buffer
            // Use 32-bit indices (Stride supports both 16-bit and 32-bit)
            global::Stride.Graphics.Buffer indexBuffer = global::Stride.Graphics.Buffer.Index.New(
                _device,
                indices,
                GraphicsResourceUsage.Default
            );

            // Wrap in abstraction layer
            StrideVertexBuffer strideVertexBuffer = new StrideVertexBuffer(
                vertexBuffer,
                vertices.Length,
                vertexStride
            );

            StrideIndexBuffer strideIndexBuffer = new StrideIndexBuffer(
                indexBuffer,
                indices.Length,
                false // 32-bit indices
            );

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
                VertexBuffer = strideVertexBuffer,
                IndexBuffer = strideIndexBuffer,
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


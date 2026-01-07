using System;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// 3D model abstraction.
    /// </summary>
    /// <remarks>
    /// Model Interface:
    /// - Based on swkotor2.exe model loading and rendering system
    /// - Located via string references: "ModelName" @ 0x007c1c8c, "Model" @ 0x007c1ca8, "VisibleModel" @ 0x007c1c98
    /// - "ModelType" @ 0x007c4568, "MODELTYPE" @ 0x007c036c, "ModelVariation" @ 0x007c0990
    /// - "ModelPart" @ 0x007bd42c, "ModelPart1" @ 0x007c0acc, "ModelA" @ 0x007bf4bc
    /// - "DefaultModel" @ 0x007c4530, "StuntModel" @ 0x007c37e0, "CameraModel" @ 0x007c3908, "ProjModel" @ 0x007c31c0
    /// - CSWCCreature::LoadModel @ 0x007c82fc (creature model loading), FUN_005261b0 @ 0x005261b0 (model loading function)
    /// - Original implementation: Loads and renders 3D models from MDL/MDX files
    /// - Model structure: Meshes, bones, animations stored in MDL (model) and MDX (animation) files
    /// - This interface: Abstraction layer for modern graphics backends (MonoGame Model, Stride Model)
    /// </remarks>
    public interface IModel : IDisposable
    {
        /// <summary>
        /// Gets the meshes in this model.
        /// </summary>
        IModelMesh[] Meshes { get; }

        /// <summary>
        /// Gets the bones in this model.
        /// </summary>
        IModelBone[] Bones { get; }

        /// <summary>
        /// Gets the root bone.
        /// </summary>
        IModelBone Root { get; }
    }

    /// <summary>
    /// Model mesh abstraction.
    /// </summary>
    public interface IModelMesh : IDisposable
    {
        /// <summary>
        /// Gets the name of the mesh.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the parent bone.
        /// </summary>
        IModelBone ParentBone { get; }

        /// <summary>
        /// Gets the bounding sphere.
        /// </summary>
        BoundingSphere BoundingSphere { get; }

        /// <summary>
        /// Gets the mesh parts.
        /// </summary>
        IModelMeshPart[] MeshParts { get; }

        /// <summary>
        /// Gets the effects used by this mesh.
        /// </summary>
        IEffect[] Effects { get; }
    }

    /// <summary>
    /// Model mesh part abstraction.
    /// </summary>
    public interface IModelMeshPart
    {
        /// <summary>
        /// Gets the vertex buffer.
        /// </summary>
        IVertexBuffer VertexBuffer { get; }

        /// <summary>
        /// Gets the index buffer.
        /// </summary>
        IIndexBuffer IndexBuffer { get; }

        /// <summary>
        /// Gets the number of vertices.
        /// </summary>
        int NumVertices { get; }

        /// <summary>
        /// Gets the vertex offset.
        /// </summary>
        int VertexOffset { get; }

        /// <summary>
        /// Gets the number of primitives.
        /// </summary>
        int PrimitiveCount { get; }

        /// <summary>
        /// Gets the start index.
        /// </summary>
        int StartIndex { get; }

        /// <summary>
        /// Gets the effect.
        /// </summary>
        IEffect Effect { get; set; }
    }

    /// <summary>
    /// Model bone abstraction.
    /// </summary>
    public interface IModelBone
    {
        /// <summary>
        /// Gets the name of the bone.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the index of the bone.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets the parent bone.
        /// </summary>
        IModelBone Parent { get; }

        /// <summary>
        /// Gets the child bones.
        /// </summary>
        IModelBone[] Children { get; }

        /// <summary>
        /// Gets or sets the transform matrix.
        /// </summary>
        System.Numerics.Matrix4x4 Transform { get; set; }
    }

    /// <summary>
    /// Bounding sphere structure.
    /// </summary>
    public struct BoundingSphere
    {
        public System.Numerics.Vector3 Center;
        public float Radius;

        public BoundingSphere(System.Numerics.Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }
}


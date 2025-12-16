using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// 3D model abstraction.
    /// </summary>
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


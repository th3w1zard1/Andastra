using System;
using JetBrains.Annotations;
using AuroraEngine.Common.Formats.MDLData;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Room mesh renderer abstraction for loading and rendering room meshes from MDL models.
    /// </summary>
    public interface IRoomMeshRenderer : IDisposable
    {
        /// <summary>
        /// Loads a room mesh from an MDL model.
        /// </summary>
        /// <param name="modelResRef">Model resource reference.</param>
        /// <param name="mdl">MDL model data.</param>
        /// <returns>Room mesh data, or null if loading failed.</returns>
        [CanBeNull]
        IRoomMeshData LoadRoomMesh(string modelResRef, MDL mdl);

        /// <summary>
        /// Clears all loaded meshes.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Room mesh data abstraction.
    /// </summary>
    public interface IRoomMeshData
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
        /// Gets the index count.
        /// </summary>
        int IndexCount { get; }
    }
}


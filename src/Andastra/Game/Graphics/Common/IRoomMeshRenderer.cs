using System;
using Andastra.Parsing;
using Andastra.Parsing.Formats.MDLData;
using JetBrains.Annotations;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Room mesh renderer abstraction for loading and rendering room meshes from MDL models.
    /// </summary>
    /// <remarks>
    /// Room Mesh Renderer:
    /// - Based on swkotor2.exe room mesh rendering system
    /// - Located via string references: "roomcount" @ 0x007b96c0, "RoomName" @ 0x007bd484, "Rooms" @ 0x007bd490
    /// - "trimesh" @ 0x007bac30, "animmesh" @ 0x007bac24, "danglymesh" @ 0x007bac18 (mesh types in MDL)
    /// - "VISIBLEVALUE" @ 0x007b6a58, "%s/%s.VIS" @ 0x007b972c (VIS file for room visibility)
    /// - "VisibleModel" @ 0x007c1c98 (visible model flag), "render" @ 0x007bab34, "renderorder" @ 0x007bab50
    /// - "WillNotRender" @ 0x007c418c (render flag), "Apropagaterender" @ 0x007bb10f (render propagation)
    /// - Area references: "AREANAME" @ 0x007be1dc, "AreaName" @ 0x007be340, "AreaId" @ 0x007bef48
    /// - "AreaObject" @ 0x007c0b70, "AreaProperties" @ 0x007bd228, "AreaMap" @ 0x007bd118
    /// - Original implementation: Loads room meshes from MDL models, uses VIS file for room visibility culling
    /// - Room meshes: Area geometry stored as MDL models with trimesh, animmesh, danglymesh components
    /// - VIS file: Defines which rooms are visible from each room (visibility graph for culling)
    /// - LYT file: Defines room layout and positions (loaded separately)
    /// - Rendering: Rooms are rendered based on VIS visibility and camera position
    /// - Note: This is an abstraction interface for graphics backends (MonoGame, Stride)
    /// </remarks>
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


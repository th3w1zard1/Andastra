using System;
using System.Collections.Generic;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Graphics.Common.Scene
{
    /// <summary>
    /// Base class for building rendering structures from area data across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Scene Builder:
    /// - Common scene building patterns shared across Odyssey, Aurora, Eclipse, s
    /// - Provides base functionality for organizing area geometry into renderable scenes
    /// - Handles common scene management: visibility culling, scene clearing, resource disposal
    /// - Engine-specific implementations handle file format differences (LYT/VIS, ARE tiles, WED, etc.)
    ///
    /// Common Functionality (all engines):
    /// - Scene data structure: Rooms/areas organized for rendering
    /// - Visibility culling: Determine which areas/rooms are visible from current position
    /// - Current area tracking: Track which area/room the player is currently in
    /// - Scene clearing: Dispose resources and reset scene state
    /// - Mesh data management: Store and manage room/area mesh data
    ///
    /// Engine-Specific (implemented in subclasses):
    /// - Odyssey: LYT (layout) and VIS (visibility) files, room-based rendering
    /// - Aurora: ARE tile-based layout, tile visibility and culling
    /// - Eclipse: ARE with advanced features, dynamic geometry, physics-aware rendering
    ///
    /// Inheritance Structure (Graphics-Backend Agnostic):
    /// - BaseSceneBuilder (this class) - Common functionality only
    ///   - OdysseySceneBuilder : BaseSceneBuilder (Runtime.Games.Odyssey.Scene) - LYT/VIS files (swkotor.exe, swkotor2.exe)
    ///   - AuroraSceneBuilder : BaseSceneBuilder (Runtime.Games.Aurora.Scene) - ARE tile-based layout (nwmain.exe)
    ///   - EclipseSceneBuilder : BaseSceneBuilder (Runtime.Games.Eclipse.Scene) - Advanced ARE features (daorigins.exe, DragonAge2.exe)
    ///
    /// Graphics Backend Rendering:
    /// - Scene builders are graphics-backend agnostic (work with MonoGame, Stride, etc.)
    /// - Graphics backends (MonoGame, Stride) consume the abstract scene data structures
    /// - IRoomMeshData interface provides graphics-backend abstraction for mesh data
    /// </remarks>
    public abstract class BaseSceneBuilder
    {
        /// <summary>
        /// Root entity/object for the scene (graphics backend specific).
        /// </summary>
        public object RootEntity { get; protected set; }

        /// <summary>
        /// Gets the visibility of an area/room from the current area/room.
        /// </summary>
        /// <param name="currentArea">Current area/room identifier.</param>
        /// <param name="targetArea">Target area/room identifier to check visibility for.</param>
        /// <returns>True if the target area is visible from the current area.</returns>
        /// <remarks>
        /// Common visibility checking pattern across all engines.
        /// Engine-specific implementations handle different visibility data formats:
        /// - Odyssey: VIS file format (room visibility graph)
        /// - Aurora: Tile-based visibility (tile adjacency and portals)
        /// - Eclipse: Advanced visibility with dynamic obstacles
        /// </remarks>
        public abstract bool IsAreaVisible(string currentArea, string targetArea);

        /// <summary>
        /// Sets the current area/room for visibility culling.
        /// </summary>
        /// <param name="areaIdentifier">Area/room identifier (model name, tile coordinates, etc.).</param>
        /// <remarks>
        /// Common pattern: Track current area for visibility culling.
        /// Engine-specific implementations handle different area identifier formats.
        /// </remarks>
        public abstract void SetCurrentArea(string areaIdentifier);

        /// <summary>
        /// Clears the current scene and disposes resources.
        /// </summary>
        /// <remarks>
        /// Common cleanup pattern: Dispose mesh data and reset scene state.
        /// Engine-specific implementations handle different resource types.
        /// </remarks>
        public abstract void Clear();

        /// <summary>
        /// Builds a scene from area data (engine-specific implementation).
        /// </summary>
        /// <param name="areaData">Area data in engine-specific format.</param>
        /// <remarks>
        /// Abstract method for engine-specific scene building.
        /// Each engine implements this based on its file formats:
        /// - Odyssey: BuildScene(LYT lyt, VIS vis)
        /// - Aurora: BuildScene(ARE areData) - tile-based layout
        /// - Eclipse: BuildScene(ARE areData) - advanced features
        /// </remarks>
        protected abstract void BuildSceneInternal(object areaData);

        /// <summary>
        /// Gets the list of scene rooms/areas for rendering.
        /// </summary>
        /// <returns>List of scene rooms/areas, or null if scene not built.</returns>
        /// <remarks>
        /// Common accessor for scene data.
        /// Engine-specific implementations return appropriate room/area types.
        /// </remarks>
        protected abstract IList<ISceneRoom> GetSceneRooms();

        /// <summary>
        /// Clears mesh data from all rooms/areas in the scene.
        /// </summary>
        /// <remarks>
        /// Common cleanup helper: Iterates through rooms and clears mesh data.
        /// </remarks>
        protected void ClearRoomMeshData()
        {
            IList<ISceneRoom> rooms = GetSceneRooms();
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    room.MeshData = null;
                }
            }
        }
    }

    /// <summary>
    /// Interface for scene room/area data across all engines.
    /// </summary>
    /// <remarks>
    /// Common interface for scene rooms/areas.
    /// Engine-specific implementations provide concrete types:
    /// - Odyssey: SceneRoom with LYT room data
    /// - Aurora: SceneTile with ARE tile data
    /// - Eclipse: SceneArea with advanced features
    /// </remarks>
    public interface ISceneRoom
    {
        /// <summary>
        /// Gets or sets the model/resource reference for this room/area.
        /// </summary>
        string ModelResRef { get; set; }

        /// <summary>
        /// Gets or sets the position of this room/area in world space.
        /// </summary>
        System.Numerics.Vector3 Position { get; set; }

        /// <summary>
        /// Gets or sets whether this room/area is currently visible.
        /// </summary>
        bool IsVisible { get; set; }

        /// <summary>
        /// Gets or sets the mesh data for this room/area.
        /// </summary>
        [CanBeNull]
        IRoomMeshData MeshData { get; set; }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Parsing.Formats.VIS;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Scene;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Odyssey.Scene
{
    /// <summary>
    /// Odyssey engine (KOTOR 1 &amp; 2) scene builder (graphics-backend agnostic).
    /// Builds abstract rendering structures from LYT (layout) and VIS (visibility) files.
    /// Works with both MonoGame and Stride backends.
    /// </summary>
    /// <remarks>
    /// Odyssey Scene Builder:
    /// - Based on swkotor.exe, swkotor2.exe area/room loading system
    /// - Located via string references: "Rooms" @ 0x007bd490 (swkotor2.exe), "RoomName" @ 0x007bd484, "roomcount" @ 0x007b96c0
    /// - Original implementation: Builds rendering structures from LYT (layout) and VIS (visibility) files
    /// - LYT file format: Binary format containing room layout, doorhooks, and room connections
    /// - VIS file format: Binary format containing room visibility data ("%s/%s.VIS" @ 0x007b972c)
    /// - Scene building: Parses LYT room data, creates abstract scene structures, sets up visibility culling from VIS
    /// - Rooms: Organized hierarchically for efficient culling and rendering
    /// - Based on LYT/VIS file format documentation in vendor/PyKotor/wiki/
    /// - Graphics-agnostic: Works with any graphics backend (MonoGame, Stride, etc.)
    ///
    /// Inheritance:
    /// - BaseSceneBuilder (Runtime.Graphics.Common.Scene) - Common scene building patterns
    ///   - OdysseySceneBuilder (this class) - Odyssey-specific LYT/VIS file handling
    /// </remarks>
    public class OdysseySceneBuilder : BaseSceneBuilder
    {
        private readonly IGameResourceProvider _resourceProvider;

        public OdysseySceneBuilder([NotNull] IGameResourceProvider resourceProvider)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException("resourceProvider");
            }

            _resourceProvider = resourceProvider;
        }

        /// <summary>
        /// Builds a scene from LYT and VIS data (Odyssey-specific).
        /// </summary>
        /// <param name="lyt">LYT layout data containing room positions and connections.</param>
        /// <param name="vis">VIS visibility data containing room visibility graph (can be null).</param>
        /// <returns>Scene data structure.</returns>
        /// <remarks>
        /// Scene Building Process (swkotor2.exe):
        /// - Based on area/room loading system
        /// - Located via string references: "Rooms" @ 0x007bd490, "RoomName" @ 0x007bd484
        /// - Original implementation: Builds rendering structures from LYT room positions and VIS visibility
        /// - Process:
        ///   1. Parse LYT room data (room models, positions) - CreateRoomList @ 0x00633580
        ///   2. Create scene structure for each room
        ///   3. Set up visibility culling groups from VIS data
        ///   4. Organize rooms into scene hierarchy for efficient rendering
        /// - VIS culling: Only rooms visible from current room are rendered
        /// - Based on LYT/VIS file format documentation in vendor/PyKotor/wiki/
        /// </remarks>
        public SceneData BuildScene([NotNull] LYT lyt, [CanBeNull] VIS vis)
        {
            if (lyt == null)
            {
                throw new ArgumentNullException("lyt");
            }

            var sceneData = new SceneData();
            sceneData.Rooms = new List<SceneRoom>();

            // Build rooms from LYT data
            foreach (var lytRoom in lyt.Rooms)
            {
                var room = new SceneRoom
                {
                    ModelResRef = lytRoom.Model,
                    Position = new Vector3(lytRoom.Position.X, lytRoom.Position.Y, lytRoom.Position.Z),
                    IsVisible = true, // Initial visibility, updated by VIS culling
                    MeshData = null // Loaded on demand by graphics backend
                };
                sceneData.Rooms.Add(room);
            }

            // Set up visibility graph from VIS data
            sceneData.VisibilityGraph = vis;
            sceneData.CurrentRoom = null;

            RootEntity = sceneData;
            return sceneData;
        }

        /// <summary>
        /// Gets the visibility of a room from the current room (Odyssey-specific).
        /// </summary>
        public override bool IsAreaVisible(string currentArea, string targetArea)
        {
            if (RootEntity is SceneData sceneData && sceneData.VisibilityGraph != null)
            {
                try
                {
                    return sceneData.VisibilityGraph.GetVisible(currentArea, targetArea);
                }
                catch
                {
                    // Room doesn't exist in VIS, default to visible
                    return true;
                }
            }
            return true; // Default to visible if no VIS data
        }

        /// <summary>
        /// Sets the current room for visibility culling (Odyssey-specific).
        /// </summary>
        public override void SetCurrentArea(string areaIdentifier)
        {
            if (RootEntity is SceneData sceneData)
            {
                sceneData.CurrentRoom = areaIdentifier;

                // Update room visibility based on current room
                if (sceneData.VisibilityGraph != null)
                {
                    foreach (var room in sceneData.Rooms)
                    {
                        room.IsVisible = IsAreaVisible(areaIdentifier, room.ModelResRef);
                    }
                }
            }
        }

        /// <summary>
        /// Clears the current scene and disposes resources (Odyssey-specific).
        /// </summary>
        public override void Clear()
        {
            ClearRoomMeshData();
            RootEntity = null;
        }

        /// <summary>
        /// Gets the list of scene rooms for rendering.
        /// </summary>
        protected override IList<ISceneRoom> GetSceneRooms()
        {
            if (RootEntity is SceneData sceneData)
            {
                return sceneData.Rooms.Cast<ISceneRoom>().ToList();
            }
            return null;
        }

        /// <summary>
        /// Builds a scene from area data (internal implementation).
        /// </summary>
        protected override void BuildSceneInternal(object areaData)
        {
            // Not used - BuildScene(LYT, VIS) is the public API for Odyssey
            throw new NotSupportedException("Use BuildScene(LYT, VIS) for Odyssey scene building");
        }
    }

    /// <summary>
    /// Scene data for Odyssey engine (swkotor.exe, swkotor2.exe).
    /// Contains rooms, visibility graph, and current room tracking.
    /// Graphics-backend agnostic.
    /// </summary>
    /// <remarks>
    /// Scene Data Structure:
    /// - Based on swkotor2.exe area/room structure
    /// - Rooms: List of rooms with positions and mesh data references
    /// - VisibilityGraph: VIS data for room visibility culling
    /// - CurrentRoom: Currently active room for visibility determination
    /// - Graphics-agnostic: Can be rendered by any graphics backend
    /// </remarks>
    public class SceneData
    {
        /// <summary>
        /// Gets or sets the list of rooms in the scene.
        /// </summary>
        public List<SceneRoom> Rooms { get; set; }

        /// <summary>
        /// Gets or sets the visibility graph for room culling.
        /// </summary>
        [CanBeNull]
        public VIS VisibilityGraph { get; set; }

        /// <summary>
        /// Gets or sets the current room identifier for visibility culling.
        /// </summary>
        [CanBeNull]
        public string CurrentRoom { get; set; }
    }

    /// <summary>
    /// Scene room data for rendering (Odyssey-specific).
    /// Graphics-backend agnostic.
    /// </summary>
    /// <remarks>
    /// Scene Room:
    /// - Based on swkotor2.exe room structure
    /// - ModelResRef: Model resource reference (e.g., "m01aa")
    /// - Position: World position from LYT data
    /// - IsVisible: Visibility flag updated by VIS culling
    /// - MeshData: Abstract mesh data loaded by graphics backend
    /// </remarks>
    public class SceneRoom : ISceneRoom
    {
        public string ModelResRef { get; set; }
        public Vector3 Position { get; set; }
        public bool IsVisible { get; set; }

        /// <summary>
        /// Room mesh data loaded from MDL model. Null until loaded on demand by graphics backend.
        /// </summary>
        [CanBeNull]
        public IRoomMeshData MeshData { get; set; }
    }
}


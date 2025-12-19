using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Parsing.Formats.LYT;
using Andastra.Parsing.Formats.VIS;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Scene;
using JetBrains.Annotations;

namespace Andastra.Runtime.MonoGame.Scene
{
    /// <summary>
    /// Odyssey engine (KOTOR 1 & 2) scene builder for MonoGame graphics backend.
    /// Builds rendering structures from LYT (layout) and VIS (visibility) files.
    /// </summary>
    /// <remarks>
    /// Odyssey Scene Builder (MonoGame):
    /// - Based on swkotor.exe, swkotor2.exe area/room loading system
    /// - Located via string references: "Rooms" @ 0x007bd490, "RoomName" @ 0x007bd484, "roomcount" @ 0x007b96c0
    /// - Original implementation: Builds rendering structures from LYT (layout) and VIS (visibility) files
    /// - LYT file format: Binary format containing room layout, doorhooks, and room connections
    /// - VIS file format: Binary format containing room visibility data ("%s/%s.VIS" @ 0x007b972c)
    /// - Scene building: Parses LYT room data, creates renderable meshes, sets up visibility culling from VIS
    /// - Rooms: Organized hierarchically for efficient culling and rendering
    /// - Based on LYT/VIS file format documentation in vendor/PyKotor/wiki/
    ///
    /// Inheritance:
    /// - BaseSceneBuilder (Runtime.Graphics.Common.Scene) - Common scene building patterns
    ///   - OdysseySceneBuilder (this class) - Odyssey-specific LYT/VIS file handling for MonoGame backend
    ///   - StrideOdysseySceneBuilder (Runtime.Stride.Scene) - Same functionality for Stride backend
    /// </remarks>
    public class OdysseySceneBuilder : BaseSceneBuilder
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly IGameResourceProvider _resourceProvider;

        public OdysseySceneBuilder([NotNull] GraphicsDevice device, [NotNull] IGameResourceProvider resourceProvider)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }
            if (resourceProvider == null)
            {
                throw new ArgumentNullException("resourceProvider");
            }

            _graphicsDevice = device;
            _resourceProvider = resourceProvider;
        }

        /// <summary>
        /// Builds a scene from LYT and VIS data (Odyssey-specific).
        /// </summary>
        /// <param name="lyt">LYT (layout) file data.</param>
        /// <param name="vis">VIS (visibility) file data.</param>
        public void BuildScene([NotNull] LYT lyt, [NotNull] VIS vis)
        {
            if (lyt == null)
            {
                throw new ArgumentNullException("lyt");
            }
            if (vis == null)
            {
                throw new ArgumentNullException("vis");
            }

            var sceneInput = new { Lyt = lyt, Vis = vis };
            BuildSceneInternal(sceneInput);
        }

        /// <summary>
        /// Builds a scene from area data (Odyssey-specific: LYT and VIS files).
        /// </summary>
        /// <remarks>
        /// Scene Building Process:
        /// - Based on swkotor.exe, swkotor2.exe area/room loading system
        /// - Located via string references: "Rooms" @ 0x007bd490, "RoomName" @ 0x007bd484
        /// - Original implementation: Builds rendering structures from LYT room positions and VIS visibility
        /// - Process:
        ///   1. Parse LYT room data (room models, positions)
        ///   2. Create renderable meshes for each room (via RoomMeshRenderer)
        ///   3. Set up visibility culling groups from VIS data
        ///   4. Organize rooms into scene hierarchy for efficient rendering
        /// - VIS culling: Only rooms visible from current room are rendered
        /// - Based on LYT/VIS file format documentation in vendor/PyKotor/wiki/
        /// </remarks>
        protected override void BuildSceneInternal(object areaData)
        {
            // Extract LYT and VIS from area data
            dynamic data = areaData;
            LYT lyt = data.Lyt;
            VIS vis = data.Vis;

            if (lyt == null || vis == null)
            {
                throw new ArgumentException("Area data must contain Lyt and Vis properties", "areaData");
            }

            Console.WriteLine("[OdysseySceneBuilder] Building scene from LYT/VIS data");
            Console.WriteLine("[OdysseySceneBuilder] Rooms: " + lyt.Rooms.Count);
            Console.WriteLine("[OdysseySceneBuilder] Doorhooks: " + lyt.Doorhooks.Count);

            // Create scene structure to hold room data
            var sceneData = new SceneData
            {
                Rooms = new List<SceneRoom>(),
                VisibilityGraph = vis,
                CurrentRoom = null
            };

            // Build room structures from LYT data
            foreach (var lytRoom in lyt.Rooms)
            {
                var sceneRoom = new SceneRoom
                {
                    ModelResRef = lytRoom.Model,
                    Position = new System.Numerics.Vector3(
                        lytRoom.Position.X,
                        lytRoom.Position.Y,
                        lytRoom.Position.Z
                    ),
                    IsVisible = true, // Default to visible, VIS will control actual visibility
                    MeshData = null // Will be loaded on demand by RoomMeshRenderer
                };

                sceneData.Rooms.Add(sceneRoom);
            }

            // Store scene data
            RootEntity = sceneData;

            Console.WriteLine("[OdysseySceneBuilder] Scene built with " + sceneData.Rooms.Count + " rooms");
        }

        /// <summary>
        /// Gets the visibility of an area/room from the current area/room (Odyssey-specific: uses VIS file).
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
        /// Sets the current area/room for visibility culling (Odyssey-specific).
        /// </summary>
        public override void SetCurrentArea(string areaIdentifier)
        {
            if (RootEntity is SceneData sceneData)
            {
                sceneData.CurrentRoom = areaIdentifier?.ToLowerInvariant();
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
    }

    /// <summary>
    /// Scene data structure holding room information and visibility graph.
    /// </summary>
    internal class SceneData
    {
        public System.Collections.Generic.List<SceneRoom> Rooms { get; set; }
        public VIS VisibilityGraph { get; set; }
        public string CurrentRoom { get; set; }
    }

    /// <summary>
    /// Scene room data for rendering (Odyssey-specific).
    /// </summary>
    internal class SceneRoom : ISceneRoom
    {
        public string ModelResRef { get; set; }
        public System.Numerics.Vector3 Position { get; set; }
        public bool IsVisible { get; set; }
        /// <summary>
        /// Room mesh data loaded from MDL model. Null until loaded on demand by RoomMeshRenderer.
        /// </summary>
        [CanBeNull]
        public IRoomMeshData MeshData { get; set; }
    }
}



using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Odyssey.Content.Interfaces;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using JetBrains.Annotations;

namespace Odyssey.MonoGame.Scene
{
    /// <summary>
    /// Builds MonoGame rendering structures from KOTOR area data (LYT, VIS, GIT).
    /// </summary>
    /// <remarks>
    /// Scene Builder:
    /// - Based on swkotor2.exe area/room loading system
    /// - Located via string references: "Rooms" @ 0x007bd490, "RoomName" @ 0x007bd484, "roomcount" @ 0x007b96c0
    /// - Original implementation: Builds rendering structures from LYT (layout) and VIS (visibility) files
    /// - LYT file format: Binary format containing room layout, doorhooks, and room connections
    /// - VIS file format: Binary format containing room visibility data ("%s/%s.VIS" @ 0x007b972c)
    /// - Scene building: Parses LYT room data, creates renderable meshes, sets up visibility culling from VIS
    /// - Rooms: Organized hierarchically for efficient culling and rendering
    /// - Based on LYT/VIS file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class SceneBuilder
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly IGameResourceProvider _resourceProvider;

        /// <summary>
        /// Root entity/object for the scene (placeholder for MonoGame scene management).
        /// </summary>
        public object RootEntity { get; private set; }

        public SceneBuilder([NotNull] GraphicsDevice device, [NotNull] IGameResourceProvider resourceProvider)
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
        /// Builds a scene from LYT and VIS data.
        /// </summary>
        /// <remarks>
        /// Scene Building Process:
        /// - Based on swkotor2.exe area/room loading system
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

            Console.WriteLine("[SceneBuilder] Building scene from LYT/VIS data");
            Console.WriteLine("[SceneBuilder] Rooms: " + lyt.Rooms.Count);
            Console.WriteLine("[SceneBuilder] Doorhooks: " + lyt.Doorhooks.Count);

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
                    Position = new Microsoft.Xna.Framework.Vector3(
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

            Console.WriteLine("[SceneBuilder] Scene built with " + sceneData.Rooms.Count + " rooms");
        }

        /// <summary>
        /// Gets the visibility of a room from the current room.
        /// </summary>
        public bool IsRoomVisible(string currentRoom, string targetRoom)
        {
            if (RootEntity is SceneData sceneData && sceneData.VisibilityGraph != null)
            {
                try
                {
                    return sceneData.VisibilityGraph.GetVisible(currentRoom, targetRoom);
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
        /// Sets the current room for visibility culling.
        /// </summary>
        public void SetCurrentRoom(string roomModel)
        {
            if (RootEntity is SceneData sceneData)
            {
                sceneData.CurrentRoom = roomModel?.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Clears the current scene.
        /// </summary>
        public void Clear()
        {
            if (RootEntity is SceneData sceneData)
            {
                // Dispose any loaded mesh data
                foreach (var room in sceneData.Rooms)
                {
                    room.MeshData = null;
                }
            }
            RootEntity = null;
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
    /// Scene room data for rendering.
    /// </summary>
    internal class SceneRoom
    {
        public string ModelResRef { get; set; }
        public Microsoft.Xna.Framework.Vector3 Position { get; set; }
        public bool IsVisible { get; set; }
        // TODO: RoomMeshData type needs to be defined
        // public Rendering.RoomMeshRenderer.RoomMeshData MeshData { get; set; }
        public object MeshData { get; set; }
    }
}


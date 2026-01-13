using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF;
using JetBrains.Annotations;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace BioWare.NET.Resource.Formats.LYT
{
    /// <summary>
    /// Represents a LYT (Layout) file defining area spatial structure.
    /// </summary>
    /// <remarks>
    /// WHAT IS A LYT FILE?
    ///
    /// A LYT file is a layout file that tells the game engine how to assemble a game area from
    /// individual room pieces. Think of it like a blueprint that shows where each room should be
    /// placed in 3D space. The game engine reads this file to know which room models to load and
    /// where to position them.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A LYT file contains four main lists:
    ///
    /// 1. ROOMS: A list of room definitions. Each room has:
    ///    - Model: The name of the 3D model file (MDL) that represents the room's visual appearance
    ///    - Position: The X, Y, Z coordinates where the room should be placed in the game world
    ///    - The game engine loads the MDL file and places it at this position
    ///
    /// 2. TRACKS: A list of swoop track booster positions (used in racing mini-games).
    ///    - Each track has a model name and position
    ///    - These are special objects placed on swoop racing tracks
    ///    - When the player drives over them, they get a speed boost
    ///
    /// 3. OBSTACLES: A list of swoop track obstacle positions (used in racing mini-games).
    ///    - Each obstacle has a model name and position
    ///    - These are objects that block the player's path during races
    ///    - The player must avoid them or crash
    ///
    /// 4. DOORHOOKS: A list of door hook points (door placement positions).
    ///    - Each door hook has:
    ///      - Room: The name of the room this door hook belongs to
    ///      - Door: The name of the door that should be placed here
    ///      - Position: The X, Y, Z coordinates where the door should be placed
    ///      - Orientation: A quaternion (4 numbers) that describes how the door should be rotated
    ///    - Door hooks tell the game where doors should be placed to connect rooms together
    ///    - When you walk through a door, the game uses these hooks to know where to place you
    ///
    /// HOW DOES THE GAME ENGINE USE LYT FILES?
    ///
    /// STEP 1: Loading Rooms
    /// - The engine reads the Rooms list
    /// - For each room, it loads the MDL model file
    /// - It places the model at the specified position
    /// - All rooms together form the complete area geometry
    ///
    /// STEP 2: Loading Tracks and Obstacles
    /// - The engine reads the Tracks and Obstacles lists (if present)
    /// - It loads the model files and places them at the specified positions
    /// - These are only used in areas with swoop racing tracks
    ///
    /// STEP 3: Placing Doors
    /// - The engine reads the DoorHooks list
    /// - For each door hook, it places a door at the specified position and orientation
    /// - Doors connect rooms together and allow the player to move between areas
    ///
    /// WHY ARE LYT FILES NEEDED?
    ///
    /// Without LYT files, the game engine wouldn't know:
    /// - Which room models to load
    /// - Where to place each room in 3D space
    /// - Where to place doors to connect rooms
    /// - Where to place special objects like track boosters and obstacles
    ///
    /// The LYT file acts as a map that tells the engine how to assemble the area from its pieces.
    ///
    /// RELATIONSHIP TO OTHER FILES:
    ///
    /// - MDL files: The 3D models referenced by Rooms, Tracks, and Obstacles
    /// - WOK files: The walkmesh files that define where characters can walk (one per room)
    /// - ARE files: The area definition files that contain lighting, fog, and other area properties
    /// - GIT files: The game instance template files that contain creatures, placeables, and other objects
    /// - VIS files: The visibility files that define which rooms can see each other
    ///
    /// Together, these files define a complete game area that the player can explore.
    ///
    /// ORIGINAL IMPLEMENTATION:
    ///
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): LYT files are loaded when an area is initialized. The engine reads
    /// the room definitions and loads the corresponding MDL files, positioning them according to
    /// the LYT file's coordinates. Door hooks are used to place doors at the correct positions
    /// for area transitions.
    ///
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:105-157
    /// </remarks>
    [PublicAPI]
    public sealed class LYT
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:105-106
        // Original: BINARY_TYPE = ResourceType.LYT
        public static readonly ResourceType BinaryType = ResourceType.LYT;

        /// <summary>
        /// List of room definitions (model name + 3D position).
        /// Each room represents a piece of the area that gets loaded and positioned in 3D space.
        /// </summary>
        /// <remarks>
        /// WHAT ARE ROOMS?
        ///
        /// Rooms are individual pieces that make up a game area. Each room is a 3D model (MDL file)
        /// that gets placed at a specific position. When all rooms are loaded and positioned, they
        /// form the complete area that the player can explore.
        ///
        /// HOW ROOMS ARE USED:
        ///
        /// When the game engine loads an area:
        /// 1. It reads the Rooms list from the LYT file
        /// 2. For each room, it loads the MDL model file
        /// 3. It places the model at the room's position (X, Y, Z coordinates)
        /// 4. All rooms together form the complete area geometry
        ///
        /// EXAMPLE:
        ///
        /// If you have a hallway area made of three room pieces:
        /// - Room 1: "hallway_start" at position (0, 0, 0)
        /// - Room 2: "hallway_middle" at position (10, 0, 0)
        /// - Room 3: "hallway_end" at position (20, 0, 0)
        ///
        /// The engine loads all three MDL files and places them in a line, creating a hallway.
        ///
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:113
        /// Original: self.rooms: list[LYTRoom] = []
        /// </remarks>
        public List<LYTRoom> Rooms { get; set; } = new List<LYTRoom>();

        /// <summary>
        /// List of swoop track booster positions (used in racing mini-games).
        /// Each track booster is a special object that gives the player a speed boost when driven over.
        /// </summary>
        /// <remarks>
        /// WHAT ARE TRACKS?
        ///
        /// Tracks are special objects used in swoop racing mini-games. When the player drives over
        /// a track booster, they get a speed boost that makes them go faster.
        ///
        /// HOW TRACKS ARE USED:
        ///
        /// During a swoop race:
        /// 1. The game engine loads track booster models at the positions specified in this list
        /// 2. When the player's swoop bike touches a booster, the game applies a speed boost
        /// 3. The boost makes the player go faster for a short time
        ///
        /// These are only used in areas that have swoop racing tracks. Most areas have an empty
        /// Tracks list.
        ///
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:118
        /// Original: self.tracks: list[LYTTrack] = []
        /// </remarks>
        public List<LYTTrack> Tracks { get; set; } = new List<LYTTrack>();

        /// <summary>
        /// List of swoop track obstacle positions (used in racing mini-games).
        /// Each obstacle is a special object that blocks the player's path during races.
        /// </summary>
        /// <remarks>
        /// WHAT ARE OBSTACLES?
        ///
        /// Obstacles are special objects used in swoop racing mini-games. They block the player's
        /// path and must be avoided. If the player hits an obstacle, they crash and lose the race.
        ///
        /// HOW OBSTACLES ARE USED:
        ///
        /// During a swoop race:
        /// 1. The game engine loads obstacle models at the positions specified in this list
        /// 2. The obstacles are placed on the track to make the race more challenging
        /// 3. The player must steer around them to avoid crashing
        ///
        /// These are only used in areas that have swoop racing tracks. Most areas have an empty
        /// Obstacles list.
        ///
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:123
        /// Original: self.obstacles: list[LYTObstacle] = []
        /// </remarks>
        public List<LYTObstacle> Obstacles { get; set; } = new List<LYTObstacle>();

        /// <summary>
        /// List of door hook points (door placement positions).
        /// Each door hook specifies where a door should be placed to connect rooms together.
        /// </summary>
        /// <remarks>
        /// WHAT ARE DOOR HOOKS?
        ///
        /// Door hooks are special points in 3D space where doors should be placed. They tell the
        /// game engine exactly where to position doors so that rooms can be connected together.
        ///
        /// WHAT DATA DOES EACH DOOR HOOK STORE?
        ///
        /// Each door hook contains:
        /// - Room: The name of the room this door hook belongs to
        /// - Door: The name of the door that should be placed here
        /// - Position: The X, Y, Z coordinates where the door should be placed
        /// - Orientation: A quaternion (4 numbers: X, Y, Z, W) that describes how the door should be rotated
        ///
        /// HOW DOOR HOOKS ARE USED:
        ///
        /// When the game engine loads an area:
        /// 1. It reads the DoorHooks list from the LYT file
        /// 2. For each door hook, it places a door at the specified position
        /// 3. It rotates the door according to the orientation quaternion
        /// 4. The door connects the room to another area or another room
        ///
        /// When the player walks through a door:
        /// 1. The game uses the door hook's position to know where to place the player
        /// 2. It uses the orientation to know which direction the player should face
        /// 3. The player is transported to the connected area or room
        ///
        /// WHY ARE DOOR HOOKS NEEDED?
        ///
        /// Without door hooks, the game engine wouldn't know:
        /// - Where to place doors in the area
        /// - How to rotate doors so they face the correct direction
        /// - Where to place the player when they walk through a door
        ///
        /// Door hooks ensure that doors are placed correctly and that area transitions work properly.
        ///
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:128
        /// Original: self.doorhooks: list[LYTDoorHook] = []
        /// </remarks>
        public List<LYTDoorHook> DoorHooks { get; set; } = new List<LYTDoorHook>();

        /// <summary>
        /// Property alias for DoorHooks to maintain compatibility with code expecting lowercase property name.
        /// </summary>
        public List<LYTDoorHook> Doorhooks
        {
            get { return DoorHooks; }
            set { DoorHooks = value; }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:108
        // Original: def __init__(self):
        public LYT()
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:130-138
        // Original: def __eq__(self, other: object) -> bool:
        public override bool Equals(object obj)
        {
            if (!(obj is LYT other))
            {
                return false;
            }

            return Rooms.SequenceEqual(other.Rooms)
                   && Tracks.SequenceEqual(other.Tracks)
                   && Obstacles.SequenceEqual(other.Obstacles)
                   && DoorHooks.SequenceEqual(other.DoorHooks);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:140-148
        // Original: def __hash__(self) -> int:
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Rooms.GetHashCode();
                hash = hash * 31 + Tracks.GetHashCode();
                hash = hash * 31 + Obstacles.GetHashCode();
                hash = hash * 31 + DoorHooks.GetHashCode();
                return hash;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:150-157
        // Original: def iter_resource_identifiers(self) -> Generator[ResourceIdentifier, Any, None]:
        public IEnumerable<ResourceIdentifier> IterResourceIdentifiers()
        {
            // Rooms
            foreach (LYTRoom room in Rooms)
            {
                yield return new ResourceIdentifier(room.Model, ResourceType.MDL);
            }

            // Tracks
            foreach (LYTTrack track in Tracks)
            {
                yield return new ResourceIdentifier(track.Model, ResourceType.MDL);
            }

            // Obstacles
            foreach (LYTObstacle obstacle in Obstacles)
            {
                yield return new ResourceIdentifier(obstacle.Model, ResourceType.MDL);
            }
        }
    }
}

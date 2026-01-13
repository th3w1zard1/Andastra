using System;
using System.Numerics;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.LYT
{
    /// <summary>
    /// Represents a door hook point in a LYT file.
    /// </summary>
    /// <remarks>
    /// WHAT IS A LYTDoorHook?
    ///
    /// A LYTDoorHook represents a point in 3D space where a door should be placed. It tells the game
    /// engine exactly where to position a door so that rooms can be connected together.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A LYTDoorHook stores:
    /// 1. Room: The name of the room this door hook belongs to
    ///    - This is a string that identifies which room the door connects to
    ///    - Example: "hallway_01" means this door hook belongs to the hallway_01 room
    ///
    /// 2. Door: The name of the door that should be placed here
    ///    - This is a string that identifies which door template to use
    ///    - Example: "door_wooden_01" means use the wooden door template
    ///
    /// 3. Position: The X, Y, Z coordinates where the door should be placed
    ///    - This is the exact location in 3D space where the door center should be
    ///    - Example: Position (10, 5, 0) means the door is at 10 units east, 5 units north, at ground level
    ///
    /// 4. Orientation: A quaternion (4 numbers: X, Y, Z, W) that describes how the door should be rotated
    ///    - Quaternions are a way to represent 3D rotations using 4 numbers
    ///    - They avoid problems with rotation angles (like gimbal lock)
    ///    - The orientation tells the engine which direction the door should face
    ///    - Example: Orientation (0, 0, 0, 1) means no rotation (facing default direction)
    ///
    /// HOW IT WORKS:
    ///
    /// When the game engine loads an area:
    /// 1. It reads all LYTDoorHook objects from the LYT file
    /// 2. For each door hook, it places a door at the specified Position
    /// 3. It rotates the door according to the Orientation quaternion
    /// 4. The door connects the room to another area or another room
    ///
    /// When the player walks through a door:
    /// 1. The game uses the door hook's Position to know where to place the player
    /// 2. It uses the Orientation to know which direction the player should face
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
    /// QUATERNIONS EXPLAINED:
    ///
    /// A quaternion is a way to represent 3D rotations using 4 numbers (X, Y, Z, W). It's more
    /// complex than rotation angles (like pitch, yaw, roll) but avoids problems like gimbal lock.
    ///
    /// The quaternion (0, 0, 0, 1) represents no rotation (identity quaternion).
    /// Other quaternions represent rotations around different axes.
    ///
    /// You don't need to understand quaternions in detail to use door hooks - the game engine
    /// handles all the math automatically. You just need to know that the Orientation quaternion
    /// tells the engine which direction the door should face.
    ///
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py
    /// </remarks>
    [PublicAPI]
    public sealed class LYTDoorHook
    {
        /// <summary>
        /// The name of the room this door hook belongs to.
        /// </summary>
        public string Room { get; set; }

        /// <summary>
        /// The name of the door that should be placed here.
        /// </summary>
        public string Door { get; set; }

        /// <summary>
        /// The X, Y, Z coordinates where the door should be placed.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// A quaternion (X, Y, Z, W) that describes how the door should be rotated.
        /// Quaternions are a way to represent 3D rotations using 4 numbers.
        /// </summary>
        public Quaternion Orientation { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LYTDoorHook()
        {
        }

        /// <summary>
        /// Constructor with room, door, position, and orientation.
        /// </summary>
        /// <param name="room">The room name.</param>
        /// <param name="door">The door name.</param>
        /// <param name="position">The position in 3D space.</param>
        /// <param name="orientation">The orientation quaternion.</param>
        public LYTDoorHook(string room, string door, Vector3 position, Vector4 orientation)
        {
            Room = room;
            Door = door;
            Position = position;
            Orientation = new Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
        }
    }
}


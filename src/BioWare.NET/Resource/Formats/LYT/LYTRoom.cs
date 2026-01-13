using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.LYT
{

    /// <summary>
    /// Represents a single room definition in a LYT file.
    /// </summary>
    /// <remarks>
    /// WHAT IS A LYTRoom?
    ///
    /// A LYTRoom represents one room piece in a game area. It tells the game engine which 3D model
    /// to load and where to place it in 3D space.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A LYTRoom stores:
    /// 1. Model: The name of the MDL file (3D model) that represents this room's visual appearance
    ///    - This is a ResRef (resource reference), which is a string up to 16 characters
    ///    - Example: "hallway_01" refers to the file "hallway_01.mdl"
    ///
    /// 2. Position: The X, Y, Z coordinates where this room should be placed in the game world
    ///    - X: East-west position (positive X = east, negative X = west)
    ///    - Y: North-south position (positive Y = north, negative Y = south)
    ///    - Z: Up-down position (positive Z = up, negative Z = down)
    ///    - Example: Position (10, 5, 0) means the room is 10 units east, 5 units north, at ground level
    ///
    /// HOW IT WORKS:
    ///
    /// When the game engine loads an area:
    /// 1. It reads all LYTRoom objects from the LYT file
    /// 2. For each room, it loads the MDL file specified by Model
    /// 3. It places the 3D model at the Position coordinates
    /// 4. All rooms together form the complete area geometry
    ///
    /// EXAMPLE:
    ///
    /// If you have a hallway made of three rooms:
    /// - Room 1: Model = "hallway_start", Position = (0, 0, 0)
    /// - Room 2: Model = "hallway_middle", Position = (10, 0, 0)
    /// - Room 3: Model = "hallway_end", Position = (20, 0, 0)
    ///
    /// The engine loads three MDL files and places them in a line, creating a hallway that's 30 units long.
    ///
    /// RELATIONSHIP TO OTHER FILES:
    ///
    /// - MDL file: The 3D model file that this room references
    /// - WOK file: The walkmesh file (usually has the same name as the MDL) that defines where characters can walk
    /// - LYT file: The layout file that contains this room definition
    ///
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py
    /// </remarks>
    [PublicAPI]
    public sealed class LYTRoom
    {
        /// <summary>
        /// The name of the MDL file (3D model) that represents this room's visual appearance.
        /// This is a ResRef (resource reference), which is a string up to 16 characters.
        /// </summary>
        public ResRef Model { get; set; }

        /// <summary>
        /// The X, Y, Z coordinates where this room should be placed in the game world.
        /// X = east-west, Y = north-south, Z = up-down.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Set of other rooms this room connects to.
        /// PyKotor-specific field for tracking room connectivity.
        /// Used for pathfinding and area transition logic.
        /// Not present in binary/ASCII format (derived from door hooks).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:269-270
        /// Original: self.connections: set[LYTRoom] = set()
        /// </remarks>
        public HashSet<LYTRoom> Connections { get; set; } = new HashSet<LYTRoom>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LYTRoom()
        {
            Connections = new HashSet<LYTRoom>();
        }

        /// <summary>
        /// Constructor with model and position.
        /// </summary>
        /// <param name="model">The model name (ResRef).</param>
        /// <param name="position">The position in 3D space.</param>
        public LYTRoom(ResRef model, Vector3 position)
        {
            Model = model;
            Position = position;
            Connections = new HashSet<LYTRoom>();
        }

        public LYTRoom(string model, Vector3 position)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:254-270
            // Original: def __init__(self, model: str, position: Vector3)
            Model = model;
            Position = position;
            Connections = new HashSet<LYTRoom>();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:272-277
        // Original: def __add__(self, other: LYTRoom) -> LYTRoom
        public static LYTRoom operator +(LYTRoom left, LYTRoom right)
        {
            Vector3 newPosition = (left.Position + right.Position) * 0.5f;
            LYTRoom newRoom = new LYTRoom($"{left.Model}_{right.Model}", newPosition);
            // Copy connections if left and right are the same type (reference equality or value equality)
            if (left != null && left is LYTRoom && left.Connections != null)
            {
                foreach (var conn in left.Connections)
                {
                    newRoom.AddConnection(conn);
                }
            }
            if (right != null && right is LYTRoom && right.Connections != null)
            {
                foreach (var conn in right.Connections)
                {
                    newRoom.AddConnection(conn);
                }
            }
            return newRoom;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:289-292
        // Original: def add_connection(self, room: LYTRoom) -> None
        public void AddConnection(LYTRoom room)
        {
            if (!Connections.Contains(room))
            {
                Connections.Add(room);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:294-297
        // Original: def remove_connection(self, room: LYTRoom) -> None
        public void RemoveConnection(LYTRoom room)
        {
            Connections.Remove(room);
        }

        public override bool Equals(object obj)
        {
            return obj is LYTRoom other && Equals(other);
        }

        public bool Equals(LYTRoom other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other == null)
            {
                return false;
            }
            return Model.ToLowerInvariant() == other.Model.ToLowerInvariant() && Position.Equals(other.Position);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Model.ToLowerInvariant(), Position);
        }
    }
}


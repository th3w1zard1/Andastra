using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing;

namespace Andastra.Parsing.Formats.LYT
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:210-304
    // Original: class LYTRoom(ComparableMixin)
    public class LYTRoom : IEquatable<LYTRoom>
    {
        public string Model { get; set; }
        public Vector3 Position { get; set; }
        public HashSet<LYTRoom> Connections { get; set; }

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


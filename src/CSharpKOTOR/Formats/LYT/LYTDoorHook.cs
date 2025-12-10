using System;
using CSharpKOTOR.Common;

namespace CSharpKOTOR.Formats.LYT
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:413-511
    // Original: class LYTDoorHook(ComparableMixin)
    public class LYTDoorHook : IEquatable<LYTDoorHook>
    {
        public string Room { get; set; }
        public string Door { get; set; }
        public Vector3 Position { get; set; }
        public Vector4 Orientation { get; set; }

        public LYTDoorHook(string room, string door, Vector3 position, Vector4 orientation)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:467-486
            // Original: def __init__(self, room: str, door: str, position: Vector3, orientation: Vector4)
            Room = room;
            Door = door;
            Position = position;
            Orientation = orientation;
        }

        public override bool Equals(object obj)
        {
            return obj is LYTDoorHook other && Equals(other);
        }

        public bool Equals(LYTDoorHook other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other == null)
            {
                return false;
            }
            return Room == other.Room &&
                   Door == other.Door &&
                   Position.Equals(other.Position) &&
                   Orientation.Equals(other.Orientation);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Room, Door, Position, Orientation);
        }
    }
}


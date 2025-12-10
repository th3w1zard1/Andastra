using System;
using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Formats.LYT
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:57-208
    // Original: class LYT(ComparableMixin)
    public class LYT : IEquatable<LYT>
    {
        public static readonly ResourceType BinaryType = ResourceType.LYT;

        public List<LYTRoom> Rooms { get; set; }
        public List<LYTTrack> Tracks { get; set; }
        public List<LYTObstacle> Obstacles { get; set; }
        public List<LYTDoorHook> Doorhooks { get; set; }

        public LYT()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:108-128
            // Original: def __init__(self)
            Rooms = new List<LYTRoom>();
            Tracks = new List<LYTTrack>();
            Obstacles = new List<LYTObstacle>();
            Doorhooks = new List<LYTDoorHook>();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:168-170
        // Original: def find_room_by_model(self, model: str) -> LYTRoom | None
        public LYTRoom FindRoomByModel(string model)
        {
            return Rooms.FirstOrDefault(room => room.Model.ToLowerInvariant() == model.ToLowerInvariant());
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:172-176
        // Original: def find_nearest_room(self, position: Vector3) -> LYTRoom | None
        public LYTRoom FindNearestRoom(Vector3 position)
        {
            if (Rooms.Count == 0)
            {
                return null;
            }
            return Rooms.OrderBy(room => (room.Position - position).Magnitude()).First();
        }

        public override bool Equals(object obj)
        {
            return obj is LYT other && Equals(other);
        }

        public bool Equals(LYT other)
        {
            if (other == null)
            {
                return false;
            }
            return Rooms.SequenceEqual(other.Rooms) &&
                   Tracks.SequenceEqual(other.Tracks) &&
                   Obstacles.SequenceEqual(other.Obstacles) &&
                   Doorhooks.SequenceEqual(other.Doorhooks);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var room in Rooms)
            {
                hash.Add(room);
            }
            foreach (var track in Tracks)
            {
                hash.Add(track);
            }
            foreach (var obstacle in Obstacles)
            {
                hash.Add(obstacle);
            }
            foreach (var doorhook in Doorhooks)
            {
                hash.Add(doorhook);
            }
            return hash.ToHashCode();
        }
    }
}


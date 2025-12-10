using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Resource.Formats.GFF;
using JetBrains.Annotations;

namespace CSharpKOTOR.Resource.Formats.LYT
{
    /// <summary>
    /// Represents a LYT (Layout) file defining area spatial structure.
    ///
    /// LYT files specify how area geometry is assembled from room models and where
    /// interactive elements (doors, tracks, obstacles) are positioned. The game engine
    /// uses LYT files to load and position room models (MDL files) and determine
    /// door placement points for area transitions.
    /// </summary>
    [PublicAPI]
    public sealed class LYT
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:105-106
        // Original: BINARY_TYPE = ResourceType.LYT
        public static readonly ResourceType BinaryType = ResourceType.LYT;

        // List of room definitions (model name + 3D position)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:113
        // Original: self.rooms: list[LYTRoom] = []
        public List<LYTRoom> Rooms { get; set; } = new List<LYTRoom>();

        // List of swoop track booster positions
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:118
        // Original: self.tracks: list[LYTTrack] = []
        public List<LYTTrack> Tracks { get; set; } = new List<LYTTrack>();

        // List of swoop track obstacle positions
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:123
        // Original: self.obstacles: list[LYTObstacle] = []
        public List<LYTObstacle> Obstacles { get; set; } = new List<LYTObstacle>();

        // List of door hook points (door placement positions)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:128
        // Original: self.doorhooks: list[LYTDoorHook] = []
        public List<LYTDoorHook> DoorHooks { get; set; } = new List<LYTDoorHook>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:108
        // Original: def __init__(self):
        public LYT()
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py:130-138
        // Original: def __eq__(self, other: object) -> bool:
        public override bool Equals(object obj)
        {
            if (obj is not LYT other)
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
                yield return new ResourceIdentifier(room.Model, ResourceType.LYT);
            }

            // Tracks
            foreach (LYTTrack track in Tracks)
            {
                yield return new ResourceIdentifier(track.Model, ResourceType.LYT);
            }

            // Obstacles
            foreach (LYTObstacle obstacle in Obstacles)
            {
                yield return new ResourceIdentifier(obstacle.Model, ResourceType.LYT);
            }
        }
    }

    // Placeholder classes - need to be implemented
    [PublicAPI]
    public sealed class LYTRoom
    {
        public ResRef Model { get; set; }
        public Vector3 Position { get; set; }
    }

    [PublicAPI]
    public sealed class LYTTrack
    {
        public ResRef Model { get; set; }
        public Vector3 Position { get; set; }
    }

    [PublicAPI]
    public sealed class LYTObstacle
    {
        public ResRef Model { get; set; }
        public Vector3 Position { get; set; }
    }

    [PublicAPI]
    public sealed class LYTDoorHook
    {
        public string Room { get; set; }
        public string Door { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Orientation { get; set; }
    }
}

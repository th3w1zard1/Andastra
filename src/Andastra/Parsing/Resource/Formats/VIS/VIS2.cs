using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Formats.VIS
{
    /// <summary>
    /// Represents a VIS (Visibility) file defining room visibility relationships.
    ///
    /// VIS files optimize rendering by specifying which rooms are visible from each
    /// parent room. When the player is in a room, only rooms marked as visible in
    /// the VIS file are rendered. This prevents rendering rooms that are occluded
    /// by walls or geometry, improving performance.
    /// </summary>
    [PublicAPI]
    public sealed class VIS
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:85
        // Original: BINARY_TYPE = ResourceType.VIS
        public static readonly ResourceType BinaryType = ResourceType.VIS;

        // Set of all room names (stored lowercase for case-insensitive comparison)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:95
        // Original: self._rooms: set[str] = set()
        private readonly HashSet<string> _rooms = new HashSet<string>();

        // Dictionary: observer room -> set of visible rooms
        // Used for occlusion culling (only render visible rooms)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:101
        // Original: self._visibility: dict[str, set[str]] = {}
        private readonly Dictionary<string, HashSet<string>> _visibility = new Dictionary<string, HashSet<string>>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:89-91
        // Original: def __init__(self):
        public VIS()
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:103-106
        // Original: def __eq__(self, other):
        public override bool Equals(object obj)
        {
            if (!(obj is VIS other))
            {
                return false;
            }

            return _rooms.SetEquals(other._rooms) && _visibility.Count == other._visibility.Count
                   && _visibility.All(kv =>
                   {
                       if (!other._visibility.TryGetValue(kv.Key, out HashSet<string> otherSet))
                       {
                           return false;
                       }
                       return kv.Value.SetEquals(otherSet);
                   });
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:108-112
        // Original: def __hash__(self):
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (string room in _rooms.OrderBy(r => r))
                {
                    hash = hash * 31 + room.GetHashCode();
                }

                foreach (KeyValuePair<string, HashSet<string>> kv in _visibility.OrderBy(kv => kv.Key))
                {
                    hash = hash * 31 + kv.Key.GetHashCode();
                    foreach (string visible in kv.Value.OrderBy(v => v))
                    {
                        hash = hash * 31 + visible.GetHashCode();
                    }
                }

                return hash;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:114-118
        // Original: def __iter__(self) -> Generator[tuple[str, set[str]], Any, None]:
        public IEnumerable<KeyValuePair<string, HashSet<string>>> GetVisibilityPairs()
        {
            foreach (KeyValuePair<string, HashSet<string>> kv in _visibility)
            {
                yield return new KeyValuePair<string, HashSet<string>>(kv.Key, new HashSet<string>(kv.Value));
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:120-140
        // Original: def all_rooms(self) -> set[str]:
        public HashSet<string> AllRooms()
        {
            return new HashSet<string>(_rooms);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:142-162
        // Original: def add_room(self, model: str):
        public void AddRoom(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return;
            }

            _rooms.Add(model.ToLowerInvariant());
        }

        // Additional method to get visibility for a room
        public HashSet<string> GetVisibleRooms(string observerRoom)
        {
            if (_visibility.TryGetValue(observerRoom.ToLowerInvariant(), out HashSet<string> visible))
            {
                return new HashSet<string>(visible);
            }
            return new HashSet<string>();
        }

        // Additional method to set visibility
        public void SetVisibleRooms(string observerRoom, IEnumerable<string> visibleRooms)
        {
            string observer = observerRoom.ToLowerInvariant();
            _visibility[observer] = new HashSet<string>(visibleRooms.Select(r => r.ToLowerInvariant()));
        }

        // Additional method to add visibility relationship
        public void AddVisibleRoom(string observerRoom, string visibleRoom)
        {
            string observer = observerRoom.ToLowerInvariant();
            string visible = visibleRoom.ToLowerInvariant();

            if (!_visibility.TryGetValue(observer, out HashSet<string> visibleSet))
            {
                visibleSet = new HashSet<string>();
                _visibility[observer] = visibleSet;
            }

            visibleSet.Add(visible);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:164-177
        // Original: def iter_resource_identifiers(self) -> Generator[ResourceIdentifier, Any, None]:
        public IEnumerable<ResourceIdentifier> IterResourceIdentifiers()
        {
            // VIS files don't reference external resources by name
            // They only contain room names which are used internally
            return Enumerable.Empty<ResourceIdentifier>();
        }
    }
}

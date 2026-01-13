using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.VIS
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
    public class VIS : IEquatable<VIS>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:85
        // Original: BINARY_TYPE = ResourceType.VIS
        public static readonly ResourceType BinaryType = ResourceType.VIS;

        // Set of all room names (stored lowercase for case-insensitive comparison)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:95
        // Original: self._rooms: set[str] = set()
        private readonly HashSet<string> _rooms;

        // Dictionary: observer room -> set of visible rooms
        // Used for occlusion culling (only render visible rooms)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:101
        // Original: self._visibility: dict[str, set[str]] = {}
        private readonly Dictionary<string, HashSet<string>> _visibility;
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:89-91
        // Original: def __init__(self):
        public VIS()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:89-101
            // Original: def __init__(self)
            _rooms = new HashSet<string>();
            _visibility = new Dictionary<string, HashSet<string>>();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:114-118
        // Original: def __iter__(self) -> Generator[tuple[str, set[str]], Any, None]
        public IEnumerable<Tuple<string, HashSet<string>>> GetEnumerator()
        {
            foreach (var kvp in _visibility)
            {
                yield return Tuple.Create(kvp.Key, new HashSet<string>(kvp.Value));
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
        // Original: def all_rooms(self) -> set[str]
        public HashSet<string> AllRooms()
        {
            return new HashSet<string>(_rooms);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:142-157
        // Original: def add_room(self, model: str)
        public void AddRoom(string model)
        {
            model = model.ToLowerInvariant();

            if (!_rooms.Contains(model))
            {
                _visibility[model] = new HashSet<string>();
            }

            _rooms.Add(model);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:159-176
        // Original: def remove_room(self, model: str)
        public void RemoveRoom(string model)
        {
            string lowerModel = model.ToLowerInvariant();

            foreach (var room in _rooms)
            {
                if (_visibility[room].Contains(lowerModel))
                {
                    _visibility[room].Remove(lowerModel);
                }
            }

            if (_rooms.Contains(lowerModel))
            {
                _rooms.Remove(lowerModel);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:178-215
        // Original: def rename_room(self, old: str, new: str)
        public void RenameRoom(string old, string new_)
        {
            old = old.ToLowerInvariant();
            new_ = new_.ToLowerInvariant();

            if (old == new_)
            {
                return;
            }

            _rooms.Remove(old);
            _rooms.Add(new_);

            _visibility[new_] = new HashSet<string>(_visibility[old]);
            _visibility.Remove(old);

            foreach (var other in _visibility.Keys.ToList())
            {
                if (other != new_ && _visibility[other].Contains(old))
                {
                    _visibility[other].Remove(old);
                    _visibility[other].Add(new_);
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:217-228
        // Original: def room_exists(self, model: str) -> bool
        public bool RoomExists(string model)
        {
            return _rooms.Contains(model.ToLowerInvariant());
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:230-254
        // Original: def set_visible(self, when_inside: str, show: str, visible: bool)
        public void SetVisible(string whenInside, string show, bool visible)
        {
            whenInside = whenInside.ToLowerInvariant();
            show = show.ToLowerInvariant();

            if (!_rooms.Contains(whenInside) || !_rooms.Contains(show))
            {
                throw new ArgumentException("One of the specified rooms does not exist.");
            }

            if (visible)
            {
                _visibility[whenInside].Add(show);
            }
            else if (_visibility[whenInside].Contains(show))
            {
                _visibility[whenInside].Remove(show);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:256-279
        // Original: def get_visible(self, when_inside: str, show: str) -> bool
        public bool GetVisible(string whenInside, string show)
        {
            whenInside = whenInside.ToLowerInvariant();
            show = show.ToLowerInvariant();

            if (!_rooms.Contains(whenInside) || !_rooms.Contains(show))
            {
                throw new ArgumentException("One of the specified rooms does not exist.");
            }

            return _visibility[whenInside].Contains(show);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:281-294
        // Original: def set_all_visible(self)
        public void SetAllVisible()
        {
            foreach (string whenInside in _rooms)
            {
                foreach (string show in _rooms.Where(room => room != whenInside))
                {
                    SetVisible(whenInside, show, visible: true);
                }
            }
        }

        /// <summary>
        /// Gets the set of rooms visible from the specified observer room.
        /// </summary>
        /// <param name="observerRoom">The room to check visibility from.</param>
        /// <returns>Set of room names that are visible from the observer room, or null if observer room doesn't exist.</returns>
        /// <remarks>
        /// Based on VIS file format: Returns the visibility set for a given room.
        /// Used for VIS culling during area rendering.
        /// </remarks>
        public HashSet<string> GetVisibleRooms(string observerRoom)
        {
            if (string.IsNullOrEmpty(observerRoom))
            {
                return new HashSet<string>();
            }

            string lowerObserver = observerRoom.ToLowerInvariant();
            if (!_visibility.ContainsKey(lowerObserver))
            {
                return new HashSet<string>();
            }

            return new HashSet<string>(_visibility[lowerObserver]);
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

        // Internal access for reader/writer
        internal Dictionary<string, HashSet<string>> Visibility => _visibility;

        public bool Equals(VIS other)
        {
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
    }
}


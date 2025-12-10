using System;
using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Formats.VIS
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_data.py:52-295
    // Original: class VIS(ComparableMixin)
    public class VIS : IEquatable<VIS>
    {
        public static readonly ResourceType BinaryType = ResourceType.VIS;

        private readonly HashSet<string> _rooms;
        private readonly Dictionary<string, HashSet<string>> _visibility;

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

        // Internal access for reader/writer
        internal Dictionary<string, HashSet<string>> Visibility => _visibility;

        public override bool Equals(object obj)
        {
            return obj is VIS other && Equals(other);
        }

        public bool Equals(VIS other)
        {
            if (other == null)
            {
                return false;
            }
            return _rooms.SetEquals(other._rooms) &&
                   _visibility.Count == other._visibility.Count &&
                   _visibility.All(kvp => other._visibility.ContainsKey(kvp.Key) &&
                                          kvp.Value.SetEquals(other._visibility[kvp.Key]));
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var room in _rooms.OrderBy(r => r))
            {
                hash.Add(room);
            }
            foreach (var kvp in _visibility.OrderBy(k => k.Key))
            {
                hash.Add(kvp.Key);
                foreach (var room in kvp.Value.OrderBy(r => r))
                {
                    hash.Add(room);
                }
            }
            return hash.ToHashCode();
        }
    }
}


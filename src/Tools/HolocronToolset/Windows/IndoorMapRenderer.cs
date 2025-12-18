using System;
using System.Collections.Generic;
using System.Linq;
using HolocronToolset.Data;

namespace HolocronToolset.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2428
    // Original: class IndoorMapRenderer(QWidget):
    public class IndoorMapRenderer
    {
        private readonly List<IndoorMapRoom> _selectedRooms = new List<IndoorMapRoom>();
        private IndoorMap _map;
        private bool _dirty = false;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2597-2600
        // Original: def mark_dirty(self):
        public void MarkDirty()
        {
            _dirty = true;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2616-2623
        // Original: def select_room(self, room: IndoorMapRoom, *, clear_existing: bool):
        public void SelectRoom(IndoorMapRoom room, bool clearExisting = true)
        {
            if (room == null)
            {
                return;
            }

            // Matching Python line 2617: if clear_existing:
            if (clearExisting)
            {
                _selectedRooms.Clear();
            }

            // Matching Python lines 2619-2621:
            // if room in self._selected_rooms:
            //     self._selected_rooms.remove(room)
            // self._selected_rooms.append(room)
            // This moves the room to the end if it's already selected
            if (_selectedRooms.Contains(room))
            {
                _selectedRooms.Remove(room);
            }
            _selectedRooms.Add(room);

            // Matching Python line 2622: self.mark_dirty()
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2624-2656
        // Original: def select_rooms(self, rooms: list[IndoorMapRoom], *, clear_existing: bool = True):
        public void SelectRooms(List<IndoorMapRoom> rooms, bool clearExisting = true)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return;
            }

            // Matching Python line 2626: if clear_existing:
            if (clearExisting)
            {
                _selectedRooms.Clear();
            }

            // Matching Python lines 2628-2631:
            // for room in rooms:
            //     if room in self._selected_rooms:
            //         self._selected_rooms.remove(room)
            //     self._selected_rooms.append(room)
            foreach (var room in rooms)
            {
                if (room != null)
                {
                    if (_selectedRooms.Contains(room))
                    {
                        _selectedRooms.Remove(room);
                    }
                    _selectedRooms.Add(room);
                }
            }

            // Matching Python line 2632: self.mark_dirty()
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2657-2659
        // Original: def selected_rooms(self) -> list[IndoorMapRoom]:
        public List<IndoorMapRoom> SelectedRooms()
        {
            // Matching Python line 2658: return self._selected_rooms
            // Note: Python returns the list directly, but we return a copy to prevent external modification
            // This matches the intent while maintaining encapsulation
            return new List<IndoorMapRoom>(_selectedRooms);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2660-2662
        // Original: def clear_selected_rooms(self):
        public void ClearSelectedRooms()
        {
            // Matching Python line 2661: self._selected_rooms.clear()
            _selectedRooms.Clear();
            // Matching Python line 2662: self.mark_dirty()
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2601-2604
        // Original: def set_map(self, indoor_map: IndoorMap):
        public void SetMap(IndoorMap indoorMap)
        {
            // Matching Python line 2602: self._map = indoor_map
            _map = indoorMap;
            // Matching Python line 2603: self._cached_walkmeshes.clear()
            // Note: Cached walkmeshes will be implemented when needed
            // Matching Python line 2604: self.mark_dirty()
            MarkDirty();
        }

        public IndoorMap GetMap()
        {
            return _map;
        }
    }
}


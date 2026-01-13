using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using HolocronToolset.Data;
using KitComponent = HolocronToolset.Data.KitComponent;
using BWM = BioWare.NET.Resource.Formats.BWM.BWM;
using BWMFace = BioWare.NET.Resource.Formats.BWM.BWMFace;
using JetBrains.Annotations;

namespace HolocronToolset.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2428
    // Original: class IndoorMapRenderer(QWidget):
    public class IndoorMapRenderer
    {
        private readonly List<IndoorMapRoom> _selectedRooms = new List<IndoorMapRoom>();
        private IndoorMap _map;
        private bool _dirty = false;
        private UndoStack _undoStack;
        private KitComponent _cursorComponent;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder/renderer.py:206-208
        // Original: self._bwm_surface_cache: dict[int, _BWMSurfaceCache] = {}
        // NOTE: We no longer cache *transformed* room walkmeshes (they require deepcopy + transforms).
        // Instead we cache BWM face paths/indices in local space and apply transforms cheaply.
        private readonly Dictionary<int, BWMSurfaceCache> _bwmSurfaceCache = new Dictionary<int, BWMSurfaceCache>();

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder/renderer.py:105-121
        // Original: @dataclass(frozen=True) class _BWMSurfaceCache
        /// <summary>
        /// Precomputed geometry for a BWM in *local* space.
        /// This cache exists to avoid rebuilding transformed BWMs (deepcopy + rotate/flip/translate)
        /// on every mouse move / repaint. Transforming is handled by the painter + cheap math.
        /// </summary>
        private class BWMSurfaceCache
        {
            public int BwmObjId { get; set; }
            public List<FaceData> FaceDataList { get; set; }
            public Dictionary<int, int> FaceIdToIndex { get; set; }
            // Unique vertex list for operations like marquee selection (local space).
            public List<Vector3> Vertices { get; set; }
            // Local-space AABB for cheap early-out in picking.
            public Vector3 BbMin { get; set; }
            public Vector3 BbMax { get; set; }
        }

        // Face data structure for rendering (replaces QPainterPath in Qt version)
        private class FaceData
        {
            public Vector3 V1 { get; set; }
            public Vector3 V2 { get; set; }
            public Vector3 V3 { get; set; }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2491-2494
        // Original: self.snap_to_grid: bool = False
        // Original: self.snap_to_hooks: bool = True
        // Original: self.grid_size: float = DEFAULT_GRID_SIZE
        // Original: self.rotation_snap: float = float(DEFAULT_ROTATION_SNAP)
        public bool SnapToGrid { get; set; } = false;
        public bool SnapToHooks { get; set; } = true;
        public float GridSize { get; set; } = 1.0f; // DEFAULT_GRID_SIZE = 1.0
        public float RotationSnap { get; set; } = 15.0f; // DEFAULT_ROTATION_SNAP = 15

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2460-2463
        // Original: self._cam_position: Vector2 = Vector2(DEFAULT_CAMERA_POSITION_X, DEFAULT_CAMERA_POSITION_Y)
        // Original: self._cam_scale: float = DEFAULT_CAMERA_ZOOM
        // Original: self._cam_rotation: float = DEFAULT_CAMERA_ROTATION
        private Vector2 _camPosition = new Vector2(0.0f, 0.0f); // DEFAULT_CAMERA_POSITION_X/Y = 0.0
        private float _camScale = 1.0f; // DEFAULT_CAMERA_ZOOM = 1.0
        private float _camRotation = 0.0f; // DEFAULT_CAMERA_ROTATION = 0.0

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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder/renderer.py:282-285
        // Original: def set_map(self, indoor_map: IndoorMap):
        public void SetMap(IndoorMap indoorMap)
        {
            // Matching Python line 283: self._map = indoor_map
            _map = indoorMap;
            // Matching Python line 284: self._bwm_surface_cache.clear()
            _bwmSurfaceCache.Clear();
            // Matching Python line 285: self.mark_dirty()
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder/renderer.py:986-1023
        // Original: def _get_bwm_surface_cache(self, bwm: BWM) -> _BWMSurfaceCache
        /// <summary>
        /// Get (or build) cached local-space geometry for a BWM.
        /// </summary>
        [CanBeNull]
        private BWMSurfaceCache GetBwmSurfaceCache([CanBeNull] BWM bwm)
        {
            if (bwm == null)
            {
                return null;
            }

            // Use RuntimeHelpers.GetHashCode to get object identity (similar to Python's id())
            int key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(bwm);
            if (_bwmSurfaceCache.TryGetValue(key, out BWMSurfaceCache cached))
            {
                return cached;
            }

            // Build face data (local space) and identity->index map
            var faceDataList = new List<FaceData>();
            var faceIdToIndex = new Dictionary<int, int>();

            for (int idx = 0; idx < bwm.Faces.Count; idx++)
            {
                BWMFace face = bwm.Faces[idx];
                faceDataList.Add(new FaceData
                {
                    V1 = face.V1,
                    V2 = face.V2,
                    V3 = face.V3
                });
                // Use RuntimeHelpers.GetHashCode for face identity (similar to Python's id(face))
                int faceId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(face);
                faceIdToIndex[faceId] = idx;
            }

            // Vertex list + AABB for early rejection
            List<Vector3> verts = bwm.Vertices();
            Vector3 bbmin;
            Vector3 bbmax;

            if (verts != null && verts.Count > 0)
            {
                bbmin = new Vector3(
                    verts.Min(v => v.X),
                    verts.Min(v => v.Y),
                    verts.Min(v => v.Z)
                );
                bbmax = new Vector3(
                    verts.Max(v => v.X),
                    verts.Max(v => v.Y),
                    verts.Max(v => v.Z)
                );
            }
            else
            {
                bbmin = Vector3.Zero;
                bbmax = Vector3.Zero;
            }

            cached = new BWMSurfaceCache
            {
                BwmObjId = key,
                FaceDataList = faceDataList,
                FaceIdToIndex = faceIdToIndex,
                Vertices = verts ?? new List<Vector3>(),
                BbMin = bbmin,
                BbMax = bbmax
            };

            _bwmSurfaceCache[key] = cached;
            return cached;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2606-2608
        // Original: def set_undo_stack(self, undo_stack: QUndoStack):
        public void SetUndoStack(UndoStack undoStack)
        {
            // Matching Python line 2607: self._undo_stack = undo_stack
            _undoStack = undoStack;
        }

        // Matching PyKotor implementation - getter for undo stack
        public UndoStack GetUndoStack()
        {
            return _undoStack;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2609-2611
        // Original: def set_cursor_component(self, component: KitComponent | None):
        public void SetCursorComponent(KitComponent component)
        {
            // Matching Python line 2610: self.cursor_component = component
            _cursorComponent = component;
            // Matching Python line 2611: self.mark_dirty()
            MarkDirty();
        }

        // Matching PyKotor implementation - getter for cursor component
        public KitComponent GetCursorComponent()
        {
            return _cursorComponent;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2613-2614
        // Original: def set_status_callback(self, callback: Callable[[QPoint | Vector2 | None, set[int | Qt.MouseButton], set[int | Qt.Key]], None] | None) -> None:
        public delegate void StatusCallback(Vector2? position, HashSet<int> mouseButtons, HashSet<int> keys);

        private StatusCallback _statusCallback;

        public void SetStatusCallback(StatusCallback callback)
        {
            // Matching Python line 2614: self._status_callback = callback
            _statusCallback = callback;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2718-2719
        // Original: def set_snap_to_grid(self, enabled: bool):
        public void SetSnapToGrid(bool enabled)
        {
            SnapToGrid = enabled;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2722-2723
        // Original: def set_snap_to_hooks(self, enabled: bool):
        public void SetSnapToHooks(bool enabled)
        {
            SnapToHooks = enabled;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2726-2727
        // Original: def set_show_grid(self, enabled: bool):
        public void SetShowGrid(bool enabled)
        {
            // Note: ShowGrid property will be added when grid rendering is implemented
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2730-2731
        // Original: def set_hide_magnets(self, enabled: bool):
        public void SetHideMagnets(bool enabled)
        {
            // Note: HideMagnets property will be added when magnet rendering is implemented
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2710-2712
        // Original: def set_material_colors(self, material_colors: dict[SurfaceMaterial, QColor]):
        public void SetMaterialColors(Dictionary<BioWare.NET.Common.SurfaceMaterial, object> materialColors)
        {
            // Note: Material colors will be stored when material rendering is fully implemented
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2714-2715
        // Original: def set_colorize_materials(self, enabled: bool):
        public void SetColorizeMaterials(bool enabled)
        {
            // Note: ColorizeMaterials property will be added when material rendering is implemented
            MarkDirty();
        }

        public IndoorMap GetMap()
        {
            return _map;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2734-2735
        // Original: def set_grid_size(self, size: float):
        public void SetGridSize(float size)
        {
            GridSize = size;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:2738-2739
        // Original: def set_rotation_snap(self, snap: float):
        public void SetRotationSnap(float snap)
        {
            RotationSnap = snap;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:3058-3059
        // Original: def camera_zoom(self) -> float:
        public float CameraZoom()
        {
            return _camScale;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:3061-3063
        // Original: def set_camera_zoom(self, zoom: float):
        public void SetCameraZoom(float zoom)
        {
            // Matching Python: self._cam_scale = max(MIN_CAMERA_ZOOM, min(zoom, MAX_CAMERA_ZOOM))
            _camScale = Math.Max(0.1f, Math.Min(zoom, 10.0f)); // MIN_CAMERA_ZOOM = 0.1, MAX_CAMERA_ZOOM = 10.0
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:3068-3070
        // Original: def camera_position(self) -> Vector2:
        public Vector2 CameraPosition()
        {
            return _camPosition;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:3071-3074
        // Original: def set_camera_position(self, x: float, y: float):
        public void SetCameraPosition(float x, float y)
        {
            _camPosition = new Vector2(x, y);
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:3081-3082
        // Original: def camera_rotation(self) -> float:
        public float CameraRotation()
        {
            return _camRotation;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:3084-3086
        // Original: def set_camera_rotation(self, radians: float):
        public void SetCameraRotation(float radians)
        {
            _camRotation = radians;
            MarkDirty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:3065-3066
        // Original: def zoom_in_camera(self, zoom: float):
        public void ZoomInCamera(float zoom)
        {
            // Matching Python: self.set_camera_zoom(self._cam_scale + zoom)
            SetCameraZoom(_camScale + zoom);
        }
    }
}


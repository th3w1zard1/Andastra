using System;
using System.Collections.Generic;
using System.Linq;
using HolocronToolset.Data;
using IndoorMap = HolocronToolset.Data.IndoorMap;
using IndoorMapRoom = HolocronToolset.Data.IndoorMapRoom;

namespace HolocronToolset.Windows
{
    // Matching PyKotor implementation - QUndoStack equivalent
    // Original: QUndoStack from Qt
    public class UndoStack
    {
        private readonly Stack<IUndoCommand> _undoStack = new Stack<IUndoCommand>();
        private readonly Stack<IUndoCommand> _redoStack = new Stack<IUndoCommand>();

        // Clean index tracking (matching Qt QUndoStack behavior)
        // The clean index represents the position in the undo stack that corresponds to the "saved" state.
        // -1 means no clean state is set (document has unsaved changes or was never saved)
        // 0 means the initial state (no commands) is clean
        // N means the state after N commands is clean
        private int _cleanIndex = 0;

        public event EventHandler<bool> CanUndoChanged;
        public event EventHandler<bool> CanRedoChanged;
        public event EventHandler<string> UndoTextChanged;
        public event EventHandler<string> RedoTextChanged;
        public event EventHandler<bool> CleanChanged;

        // Matching Python: def canUndo(self) -> bool
        public bool CanUndo()
        {
            return _undoStack.Count > 0;
        }

        // Matching Python: def canRedo(self) -> bool
        public bool CanRedo()
        {
            return _redoStack.Count > 0;
        }

        // Matching Qt QUndoStack: bool isClean() const
        // Returns true if the current state matches the clean (saved) state
        public bool IsClean()
        {
            // Current index is the number of commands in the undo stack
            // The document is clean if the current index matches the clean index
            int currentIndex = GetCurrentIndex();
            return _cleanIndex >= 0 && currentIndex == _cleanIndex;
        }

        // Get the current index in the command history
        // Index 0 = initial state (no commands)
        // Index 1 = after first command
        // Index N = after Nth command
        private int GetCurrentIndex()
        {
            // The current index is always the number of commands in the undo stack
            // This represents how many commands have been executed from the initial state
            return _undoStack.Count;
        }

        // Matching Python: def undo(self)
        public void Undo()
        {
            if (!CanUndo())
            {
                return;
            }

            bool wasClean = IsClean();
            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            OnCanUndoChanged();
            OnCanRedoChanged();
            OnUndoTextChanged(GetUndoText());
            OnRedoTextChanged(GetRedoText());
            
            // Check if clean state changed after undo
            if (wasClean != IsClean())
            {
                OnCleanChanged();
            }
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            if (!CanRedo())
            {
                return;
            }

            bool wasClean = IsClean();
            try
            {
                var command = _redoStack.Pop();
                command.Redo();
                _undoStack.Push(command);

                OnCanUndoChanged();
                OnCanRedoChanged();
                OnUndoTextChanged(GetUndoText());
                OnRedoTextChanged(GetRedoText());
                
                // Check if clean state changed after redo
                if (wasClean != IsClean())
                {
                    OnCleanChanged();
                }
            }
            catch (Exception ex)
            {
                // If redo fails, restore the command to the redo stack to maintain consistency
                // This prevents the redo stack from losing commands that failed to redo
                // Note: The command was already popped and pushed to undo stack, so we need to restore it
                if (_undoStack.Count > 0)
                {
                    var failedCommand = _undoStack.Pop();
                    if (failedCommand != null)
                    {
                        _redoStack.Push(failedCommand);
                    }
                }
                throw new InvalidOperationException("Failed to redo command. State may be inconsistent.", ex);
            }
        }

        // Matching Python: def push(self, command: QUndoCommand)
        // In Qt, QUndoCommand.redo() is called automatically when pushed
        public void Push(IUndoCommand command)
        {
            if (command == null)
            {
                return;
            }

            bool wasClean = IsClean();
            
            // Calculate current index before push (needed for clean index tracking)
            int currentIndexBeforePush = GetCurrentIndex();
            
            // Matching Python QUndoStack behavior: redo() is called automatically when command is pushed
            command.Redo();

            _undoStack.Push(command);
            _redoStack.Clear();

            // In Qt QUndoStack, if we push a new command when not at the clean index,
            // the clean index becomes invalid (-1) because the saved state is no longer reachable
            // If we're at the clean index, the clean index moves forward with the new command
            if (_cleanIndex >= 0)
            {
                if (currentIndexBeforePush != _cleanIndex)
                {
                    // We're not at the clean index, so the clean state is invalidated
                    _cleanIndex = -1;
                }
                else
                {
                    // We're at the clean index, so it moves forward with the new command
                    _cleanIndex = GetCurrentIndex();
                }
            }
            else
            {
                // Clean index was already invalid, keep it invalid
                _cleanIndex = -1;
            }

            OnCanUndoChanged();
            OnCanRedoChanged();
            OnUndoTextChanged(GetUndoText());
            OnRedoTextChanged(GetRedoText());
            
            // Check if clean state changed after push
            if (wasClean != IsClean())
            {
                OnCleanChanged();
            }
        }

        // Matching Python: def clear(self)
        public void Clear()
        {
            bool wasClean = IsClean();
            
            _undoStack.Clear();
            _redoStack.Clear();
            
            // After clearing, we're back to the initial state (index 0)
            // If the clean index was 0, it remains 0 (initial state is still clean)
            // Otherwise, reset to 0 to indicate initial state is clean after clear
            _cleanIndex = 0;

            OnCanUndoChanged();
            OnCanRedoChanged();
            OnUndoTextChanged(GetUndoText());
            OnRedoTextChanged(GetRedoText());
            
            // Check if clean state changed after clear
            if (wasClean != IsClean())
            {
                OnCleanChanged();
            }
        }

        // Matching Python: def setClean(self)
        // Matching Qt QUndoStack: void setClean()
        // Marks the current state as "clean" (saved). The clean index is set to the current index.
        public void SetClean()
        {
            bool wasClean = IsClean();
            
            // Set the clean index to the current index
            // This marks the current state as the "saved" state
            _cleanIndex = GetCurrentIndex();
            
            // Notify if clean state changed
            if (wasClean != IsClean())
            {
                OnCleanChanged();
            }
        }

        private string GetUndoText()
        {
            if (_undoStack.Count > 0)
            {
                return _undoStack.Peek().Text;
            }
            return string.Empty;
        }

        private string GetRedoText()
        {
            if (_redoStack.Count > 0)
            {
                return _redoStack.Peek().Text;
            }
            return string.Empty;
        }

        private void OnCanUndoChanged()
        {
            CanUndoChanged?.Invoke(this, CanUndo());
        }

        private void OnCanRedoChanged()
        {
            CanRedoChanged?.Invoke(this, CanRedo());
        }

        private void OnUndoTextChanged(string text)
        {
            UndoTextChanged?.Invoke(this, text);
        }

        private void OnRedoTextChanged(string text)
        {
            RedoTextChanged?.Invoke(this, text);
        }

        private void OnCleanChanged()
        {
            CleanChanged?.Invoke(this, IsClean());
        }
    }

    // Matching PyKotor implementation - QUndoCommand equivalent
    public interface IUndoCommand
    {
        string Text { get; }
        void Undo();
        void Redo();
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:149-176
    // Original: class AddRoomCommand(QUndoCommand):
    public class AddRoomCommand : IUndoCommand
    {
        private readonly IndoorMap _indoorMap;
        private readonly IndoorMapRoom _room;

        public string Text => "Add Room";

        public AddRoomCommand(IndoorMap indoorMap, IndoorMapRoom room)
        {
            _indoorMap = indoorMap;
            _room = room;
        }

        // Matching Python: def undo(self)
        public void Undo()
        {
            if (_indoorMap.Rooms.Contains(_room))
            {
                _indoorMap.Rooms.Remove(_room);
                _indoorMap.RebuildRoomConnections();
            }
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            if (!_indoorMap.Rooms.Contains(_room))
            {
                _indoorMap.Rooms.Add(_room);
                _indoorMap.RebuildRoomConnections();
            }
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:178-210
    // Original: class DeleteRoomsCommand(QUndoCommand):
    public class DeleteRoomsCommand : IUndoCommand
    {
        private readonly IndoorMap _indoorMap;
        private readonly List<IndoorMapRoom> _rooms;
        private readonly List<int> _indices;

        public string Text => $"Delete {_rooms.Count} Room(s)";

        public DeleteRoomsCommand(IndoorMap indoorMap, List<IndoorMapRoom> rooms)
        {
            _indoorMap = indoorMap;
            _rooms = new List<IndoorMapRoom>(rooms);
            // Store indices for proper re-insertion order (matching Python line 192)
            _indices = new List<int>();
            foreach (var room in rooms)
            {
                int index = _indoorMap.Rooms.IndexOf(room);
                if (index >= 0)
                {
                    _indices.Add(index);
                }
            }
        }

        // Matching Python: def undo(self)
        public void Undo()
        {
            // Re-add rooms in original order (matching Python lines 196-198)
            var sortedPairs = _indices.Zip(_rooms, (idx, room) => new { idx, room })
                .OrderBy(p => p.idx)
                .ToList();

            foreach (var pair in sortedPairs)
            {
                if (!_indoorMap.Rooms.Contains(pair.room))
                {
                    _indoorMap.Rooms.Insert(pair.idx, pair.room);
                }
            }
            _indoorMap.RebuildRoomConnections();
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            // Remove rooms (matching Python lines 204-206)
            foreach (var room in _rooms)
            {
                if (_indoorMap.Rooms.Contains(room))
                {
                    _indoorMap.Rooms.Remove(room);
                }
            }
            _indoorMap.RebuildRoomConnections();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:319-365
    // Original: class DuplicateRoomsCommand(QUndoCommand):
    public class DuplicateRoomsCommand : IUndoCommand
    {
        private readonly IndoorMap _indoorMap;
        private readonly List<IndoorMapRoom> _originalRooms;
        private readonly System.Numerics.Vector3 _offset;
        public List<IndoorMapRoom> Duplicates { get; private set; }

        public string Text => $"Duplicate {_originalRooms.Count} Room(s)";

        // Duplicate offset constants (matching Python constants)
        private const float DUPLICATE_OFFSET_X = 2.0f;
        private const float DUPLICATE_OFFSET_Y = 2.0f;
        private const float DUPLICATE_OFFSET_Z = 0.0f;

        public DuplicateRoomsCommand(IndoorMap indoorMap, List<IndoorMapRoom> rooms, System.Numerics.Vector3? offset = null)
        {
            _indoorMap = indoorMap;
            _originalRooms = new List<IndoorMapRoom>(rooms);
            _offset = offset ?? new System.Numerics.Vector3(DUPLICATE_OFFSET_X, DUPLICATE_OFFSET_Y, DUPLICATE_OFFSET_Z);

            // Create duplicates (matching Python lines 335-349)
            Duplicates = new List<IndoorMapRoom>();
            foreach (var room in rooms)
            {
                // Deep copy component so hooks can be edited independently
                // Matching Python line 338: component_copy = deepcopy(room.component)
                KitComponent componentCopy = room.Component?.DeepCopy();

                var newRoom = new IndoorMapRoom(
                    componentCopy,
                    new System.Numerics.Vector3(
                        room.Position.X + _offset.X,
                        room.Position.Y + _offset.Y,
                        room.Position.Z + _offset.Z
                    ),
                    room.Rotation,
                    flipX: room.FlipX,
                    flipY: room.FlipY
                );
                // Matching Python line 346: new_room.walkmesh_override = deepcopy(room.walkmesh_override) if room.walkmesh_override is not None else None
                // Note: walkmesh_override is not currently implemented in IndoorMapRoom, but when it is,
                // it should be deep copied here using the same pattern as component deep copy
                // Initialize hooks connections list to match hooks length (matching Python line 348)
                if (componentCopy != null && componentCopy.Hooks != null)
                {
                    // Hooks list is already initialized in IndoorMapRoom constructor, but ensure it matches
                    if (newRoom.Hooks == null)
                    {
                        newRoom.Hooks = new List<IndoorMapRoom>();
                    }
                    // Ensure hooks list has the correct length (all null initially)
                    while (newRoom.Hooks.Count < componentCopy.Hooks.Count)
                    {
                        newRoom.Hooks.Add(null);
                    }
                    while (newRoom.Hooks.Count > componentCopy.Hooks.Count)
                    {
                        newRoom.Hooks.RemoveAt(newRoom.Hooks.Count - 1);
                    }
                }
                Duplicates.Add(newRoom);
            }
        }

        // Matching Python: def undo(self)
        public void Undo()
        {
            // Remove duplicates (matching Python lines 352-354)
            foreach (var room in Duplicates)
            {
                if (_indoorMap.Rooms.Contains(room))
                {
                    _indoorMap.Rooms.Remove(room);
                }
            }
            _indoorMap.RebuildRoomConnections();
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            // Add duplicates (matching Python lines 360-362)
            foreach (var room in Duplicates)
            {
                if (!_indoorMap.Rooms.Contains(room))
                {
                    _indoorMap.Rooms.Add(room);
                }
            }
            _indoorMap.RebuildRoomConnections();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:213-243
    // Original: class MoveRoomsCommand(QUndoCommand):
    public class MoveRoomsCommand : IUndoCommand
    {
        private readonly IndoorMap _indoorMap;
        private readonly List<IndoorMapRoom> _rooms;
        private readonly List<System.Numerics.Vector3> _oldPositions;
        private readonly List<System.Numerics.Vector3> _newPositions;

        public string Text => $"Move {_rooms.Count} Room(s)";

        public MoveRoomsCommand(
            IndoorMap indoorMap,
            List<IndoorMapRoom> rooms,
            List<System.Numerics.Vector3> oldPositions,
            List<System.Numerics.Vector3> newPositions)
        {
            _indoorMap = indoorMap;
            _rooms = new List<IndoorMapRoom>(rooms);
            _oldPositions = new List<System.Numerics.Vector3>(oldPositions);
            _newPositions = new List<System.Numerics.Vector3>(newPositions);
        }

        // Matching Python: def undo(self)
        public void Undo()
        {
            // Restore old positions (matching Python lines 232-233)
            for (int i = 0; i < _rooms.Count && i < _oldPositions.Count; i++)
            {
                _rooms[i].Position = _oldPositions[i];
            }
            _indoorMap.RebuildRoomConnections();
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            // Apply new positions (matching Python lines 239-240)
            for (int i = 0; i < _rooms.Count && i < _newPositions.Count; i++)
            {
                _rooms[i].Position = _newPositions[i];
            }
            _indoorMap.RebuildRoomConnections();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:246-276
    // Original: class RotateRoomsCommand(QUndoCommand):
    public class RotateRoomsCommand : IUndoCommand
    {
        private readonly IndoorMap _indoorMap;
        private readonly List<IndoorMapRoom> _rooms;
        private readonly List<float> _oldRotations;
        private readonly List<float> _newRotations;

        public string Text => $"Rotate {_rooms.Count} Room(s)";

        public RotateRoomsCommand(
            IndoorMap indoorMap,
            List<IndoorMapRoom> rooms,
            List<float> oldRotations,
            List<float> newRotations)
        {
            _indoorMap = indoorMap;
            _rooms = new List<IndoorMapRoom>(rooms);
            _oldRotations = new List<float>(oldRotations);
            _newRotations = new List<float>(newRotations);
        }

        // Matching Python: def undo(self)
        public void Undo()
        {
            // Restore old rotations (matching Python lines 265-266)
            for (int i = 0; i < _rooms.Count && i < _oldRotations.Count; i++)
            {
                _rooms[i].Rotation = _oldRotations[i];
            }
            _indoorMap.RebuildRoomConnections();
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            // Apply new rotations (matching Python lines 272-273)
            for (int i = 0; i < _rooms.Count && i < _newRotations.Count; i++)
            {
                _rooms[i].Rotation = _newRotations[i];
            }
            _indoorMap.RebuildRoomConnections();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:279-312
    // Original: class FlipRoomsCommand(QUndoCommand):
    public class FlipRoomsCommand : IUndoCommand
    {
        private readonly IndoorMap _indoorMap;
        private readonly List<IndoorMapRoom> _rooms;
        private readonly bool _flipX;
        private readonly bool _flipY;
        private readonly List<bool> _oldFlipX;
        private readonly List<bool> _oldFlipY;

        public string Text => $"Flip {_rooms.Count} Room(s)";

        public FlipRoomsCommand(
            IndoorMap indoorMap,
            List<IndoorMapRoom> rooms,
            bool flipX,
            bool flipY)
        {
            _indoorMap = indoorMap;
            _rooms = new List<IndoorMapRoom>(rooms);
            _flipX = flipX;
            _flipY = flipY;
            // Store original states (matching Python lines 297-298)
            _oldFlipX = new List<bool>();
            _oldFlipY = new List<bool>();
            foreach (var room in rooms)
            {
                _oldFlipX.Add(room.FlipX);
                _oldFlipY.Add(room.FlipY);
            }
        }

        // Matching Python: def undo(self)
        public void Undo()
        {
            // Restore original flip states (matching Python lines 301-303)
            for (int i = 0; i < _rooms.Count && i < _oldFlipX.Count && i < _oldFlipY.Count; i++)
            {
                _rooms[i].FlipX = _oldFlipX[i];
                _rooms[i].FlipY = _oldFlipY[i];
            }
            _indoorMap.RebuildRoomConnections();
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            // Apply flip states (matching Python lines 309-312)
            foreach (var room in _rooms)
            {
                if (_flipX)
                {
                    room.FlipX = !room.FlipX;
                }
                if (_flipY)
                {
                    room.FlipY = !room.FlipY;
                }
            }
            _indoorMap.RebuildRoomConnections();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:368-386
    // Original: class MoveWarpCommand(QUndoCommand):
    public class MoveWarpCommand : IUndoCommand
    {
        private readonly IndoorMap _indoorMap;
        private readonly System.Numerics.Vector3 _oldPosition;
        private readonly System.Numerics.Vector3 _newPosition;

        public string Text => "Move Warp Point";

        public MoveWarpCommand(
            IndoorMap indoorMap,
            System.Numerics.Vector3 oldPosition,
            System.Numerics.Vector3 newPosition)
        {
            _indoorMap = indoorMap;
            _oldPosition = new System.Numerics.Vector3(oldPosition.X, oldPosition.Y, oldPosition.Z);
            _newPosition = new System.Numerics.Vector3(newPosition.X, newPosition.Y, newPosition.Z);
        }

        // Matching Python: def undo(self)
        public void Undo()
        {
            // Restore old position (matching Python line 383)
            _indoorMap.WarpPoint = new System.Numerics.Vector3(_oldPosition.X, _oldPosition.Y, _oldPosition.Z);
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            // Apply new position (matching Python line 386)
            _indoorMap.WarpPoint = new System.Numerics.Vector3(_newPosition.X, _newPosition.Y, _newPosition.Z);
        }
    }
}


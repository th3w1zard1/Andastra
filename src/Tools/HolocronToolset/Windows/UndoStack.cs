using System;
using System.Collections.Generic;
using System.Linq;
using HolocronToolset.Data;

namespace HolocronToolset.Windows
{
    // Matching PyKotor implementation - QUndoStack equivalent
    // Original: QUndoStack from Qt
    public class UndoStack
    {
        private readonly Stack<IUndoCommand> _undoStack = new Stack<IUndoCommand>();
        private readonly Stack<IUndoCommand> _redoStack = new Stack<IUndoCommand>();

        public event EventHandler<bool> CanUndoChanged;
        public event EventHandler<bool> CanRedoChanged;
        public event EventHandler<string> UndoTextChanged;
        public event EventHandler<string> RedoTextChanged;

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

        // Matching Python: def undo(self)
        public void Undo()
        {
            if (!CanUndo())
            {
                return;
            }

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            OnCanUndoChanged();
            OnCanRedoChanged();
            OnUndoTextChanged(GetUndoText());
            OnRedoTextChanged(GetRedoText());
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            if (!CanRedo())
            {
                return;
            }

            var command = _redoStack.Pop();
            command.Redo();
            _undoStack.Push(command);

            OnCanUndoChanged();
            OnCanRedoChanged();
            OnUndoTextChanged(GetUndoText());
            OnRedoTextChanged(GetRedoText());
        }

        // Matching Python: def push(self, command: QUndoCommand)
        // In Qt, QUndoCommand.redo() is called automatically when pushed
        public void Push(IUndoCommand command)
        {
            if (command == null)
            {
                return;
            }

            // Matching Python QUndoStack behavior: redo() is called automatically when command is pushed
            command.Redo();

            _undoStack.Push(command);
            _redoStack.Clear();

            OnCanUndoChanged();
            OnCanRedoChanged();
            OnUndoTextChanged(GetUndoText());
            OnRedoTextChanged(GetRedoText());
        }

        // Matching Python: def clear(self)
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();

            OnCanUndoChanged();
            OnCanRedoChanged();
            OnUndoTextChanged(GetUndoText());
            OnRedoTextChanged(GetRedoText());
        }

        // Matching Python: def setClean(self)
        public void SetClean()
        {
            // In Qt, this marks the current state as "clean" (saved)
            // For now, we just track the state - full implementation would track clean index
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
                // Note: rebuild_room_connections will be implemented when needed
            }
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            if (!_indoorMap.Rooms.Contains(_room))
            {
                _indoorMap.Rooms.Add(_room);
                // Note: rebuild_room_connections will be implemented when needed
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
            // Note: rebuild_room_connections will be implemented when needed
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
            // Note: rebuild_room_connections will be implemented when needed
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
                // Note: For now, we use the same component reference. Full deep copy of component
                // will be implemented when component editing is needed. This matches the basic behavior.
                var newRoom = new IndoorMapRoom(
                    room.Component, // Component reference (shallow for now)
                    new System.Numerics.Vector3(
                        room.Position.X + _offset.X,
                        room.Position.Y + _offset.Y,
                        room.Position.Z + _offset.Z
                    ),
                    room.Rotation,
                    flipX: room.FlipX,
                    flipY: room.FlipY
                );
                // Note: walkmesh_override deep copy will be implemented when needed
                // newRoom.walkmesh_override = deepcopy(room.walkmesh_override) if room.walkmesh_override is not None else None
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
            // Note: rebuild_room_connections will be implemented when needed
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
            // Note: rebuild_room_connections will be implemented when needed
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
            // Note: rebuild_room_connections will be implemented when needed
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            // Apply new positions (matching Python lines 239-240)
            for (int i = 0; i < _rooms.Count && i < _newPositions.Count; i++)
            {
                _rooms[i].Position = _newPositions[i];
            }
            // Note: rebuild_room_connections will be implemented when needed
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
            // Note: rebuild_room_connections will be implemented when needed
        }

        // Matching Python: def redo(self)
        public void Redo()
        {
            // Apply new rotations (matching Python lines 272-273)
            for (int i = 0; i < _rooms.Count && i < _newRotations.Count; i++)
            {
                _rooms[i].Rotation = _newRotations[i];
            }
            // Note: rebuild_room_connections will be implemented when needed
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
            // Note: rebuild_room_connections will be implemented when needed
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
            // Note: rebuild_room_connections will be implemented when needed
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


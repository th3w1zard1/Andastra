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
}


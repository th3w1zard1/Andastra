using System;
using System.Collections.Generic;
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
        public void Push(IUndoCommand command)
        {
            if (command == null)
            {
                return;
            }

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
}


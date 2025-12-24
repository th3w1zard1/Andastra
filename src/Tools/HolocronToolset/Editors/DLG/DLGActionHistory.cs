using System;
using System.Collections.Generic;
using System.Linq;

namespace HolocronToolset.Editors.DLG
{
    // Action history manager for DLG editor undo/redo functionality
    // Implements command pattern with undo/redo stacks
    // C# 7.3 compatible implementation
    public class DLGActionHistory
    {
        private readonly DLGEditor _editor;
        private readonly Stack<IDLGAction> _undoStack;
        private readonly Stack<IDLGAction> _redoStack;

        public DLGActionHistory(DLGEditor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _undoStack = new Stack<IDLGAction>();
            _redoStack = new Stack<IDLGAction>();
        }

        // Check if undo is available
        public bool CanUndo => _undoStack.Count > 0;

        // Check if redo is available
        public bool CanRedo => _redoStack.Count > 0;

        // Apply a new action and add it to undo stack
        // Clears redo stack when new action is applied (standard undo/redo behavior)
        public void Apply(IDLGAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // Clear redo stack when new action is applied
            _redoStack.Clear();

            // Apply the action
            action.Apply(_editor);

            // Add to undo stack
            _undoStack.Push(action);
        }

        // Undo the last action
        public void Undo()
        {
            if (!CanUndo)
            {
                return;
            }

            // Pop from undo stack
            var action = _undoStack.Pop();

            // Undo the action
            action.Undo(_editor);

            // Add to redo stack
            _redoStack.Push(action);
        }

        // Redo the last undone action
        public void Redo()
        {
            if (!CanRedo)
            {
                return;
            }

            // Pop from redo stack
            var action = _redoStack.Pop();

            // Apply the action
            action.Apply(_editor);

            // Add to undo stack
            _undoStack.Push(action);
        }

        // Clear all undo/redo history
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        // Get the number of actions in undo stack (for testing/debugging)
        public int UndoCount => _undoStack.Count;

        // Get the number of actions in redo stack (for testing/debugging)
        public int RedoCount => _redoStack.Count;
    }
}



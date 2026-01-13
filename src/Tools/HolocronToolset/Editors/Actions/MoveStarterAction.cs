using System;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;

namespace HolocronToolset.Editors.Actions
{
    // Action for moving a starter link up or down in the DLG
    // Tracks the link, old index, and new index for undo/redo
    public class MoveStarterAction : IDLGAction
    {
        private readonly DLGLink _link;
        private readonly int _oldIndex;
        private readonly int _newIndex;

        public MoveStarterAction(DLGLink link, int oldIndex, int newIndex)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _oldIndex = oldIndex;
            _newIndex = newIndex;
        }

        public void Apply(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            // Move in CoreDlg
            editor.CoreDlg.Starters.RemoveAt(_oldIndex);
            editor.CoreDlg.Starters.Insert(_newIndex, _link);

            // Move in model
            editor.Model.MoveStarter(_oldIndex, _newIndex);
        }

        public void Undo(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            // Restore original position in CoreDlg
            editor.CoreDlg.Starters.RemoveAt(_newIndex);
            editor.CoreDlg.Starters.Insert(_oldIndex, _link);

            // Restore original position in model
            editor.Model.MoveStarter(_newIndex, _oldIndex);
        }
    }
}



using System;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;

namespace HolocronToolset.Editors.Actions
{
    // Action for removing a starter link from the DLG
    // Tracks the removed link and its index for undo/redo
    public class RemoveStarterAction : IDLGAction
    {
        private readonly DLGLink _link;
        private readonly int _index;

        public RemoveStarterAction(DLGLink link, int index)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _index = index;
        }

        public void Apply(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            // Remove link from CoreDlg and model
            editor.CoreDlg.Starters.Remove(_link);
            editor.Model.RemoveStarter(_link);
        }

        public void Undo(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            // Restore link to CoreDlg and model at original index
            if (_index >= 0 && _index <= editor.CoreDlg.Starters.Count)
            {
                editor.CoreDlg.Starters.Insert(_index, _link);
                editor.Model.InsertStarter(_index, _link);
            }
            else
            {
                editor.CoreDlg.Starters.Add(_link);
                editor.Model.AddStarter(_link);
            }
        }
    }
}



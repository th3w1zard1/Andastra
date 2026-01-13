using System;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;

namespace HolocronToolset.Editors.Actions
{
    // Action for adding a starter link to the DLG
    // Tracks the added link and its index for undo/redo
    public class AddStarterAction : IDLGAction
    {
        private readonly DLGLink _link;
        private readonly int _index;

        public AddStarterAction(DLGLink link, int index)
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

            // Add link to CoreDlg and model
            editor.CoreDlg.Starters.Add(_link);
            editor.Model.AddStarter(_link);
        }

        public void Undo(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            // Remove link from CoreDlg and model
            editor.CoreDlg.Starters.Remove(_link);
            editor.Model.RemoveStarter(_link);
        }
    }
}



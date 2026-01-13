using System;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;
using HolocronToolset.Editors.DLG;

namespace HolocronToolset.Editors.Actions
{
    // Action for adding a root node to the DLG dialog graph
    // Tracks the created link and item for undo/redo
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:846-856
    // Original: def add_root_node(self):
    public class AddRootNodeAction : IDLGAction
    {
        private DLGLink _link;
        private DLGStandardItem _item;
        private int _index;

        public AddRootNodeAction()
        {
            // Constructor doesn't need parameters - the link and item are created during Apply
        }

        public void Apply(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            // Use the model to add the root node (this performs the actual operation)
            // Matching PyKotor: self.model.add_root_node() creates new DLGEntry, DLGLink, and DLGStandardItem
            _item = editor.Model.AddRootNode();
            if (_item == null || _item.Link == null)
            {
                throw new InvalidOperationException("Failed to create root node");
            }

            _link = _item.Link;
            _index = editor.CoreDlg.Starters.IndexOf(_link);

            // Update tree view
            editor.UpdateTreeView();
        }

        public void Undo(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            if (_link == null)
            {
                return; // Nothing to undo
            }

            // Remove link from CoreDlg and model
            // Matching PyKotor: undo would remove the starter link
            editor.CoreDlg.Starters.Remove(_link);
            editor.Model.RemoveStarter(_link);

            // Update tree view
            editor.UpdateTreeView();
        }

        /// <summary>
        /// Gets the created item (for use by the editor to select it).
        /// </summary>
        public DLGStandardItem Item => _item;
    }
}


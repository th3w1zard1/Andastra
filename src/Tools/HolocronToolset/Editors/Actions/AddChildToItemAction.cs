using System;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;

namespace HolocronToolset.Editors.Actions
{
    // Action for adding a child node to a parent item in the DLG tree
    // Tracks the parent item, child item, and child link for undo/redo
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:858-877
    // Original: def add_child_to_item(self, parent_item: DLGStandardItem, link: DLGLink | None = None) -> DLGStandardItem:
    public class AddChildToItemAction : IDLGAction
    {
        private readonly DLGStandardItem _parentItem;
        private DLGStandardItem _childItem;
        private DLGLink _childLink;
        private readonly int _childIndex;

        public AddChildToItemAction(DLGStandardItem parentItem, int childIndex = -1)
        {
            _parentItem = parentItem ?? throw new ArgumentNullException(nameof(parentItem));
            _childIndex = childIndex;
        }

        public void Apply(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            if (_parentItem.Link == null)
            {
                throw new InvalidOperationException("Parent item must have a valid link");
            }

            // Use the model to add the child (this performs the actual operation)
            _childItem = editor.Model.AddChildToItem(_parentItem, null);
            if (_childItem == null || _childItem.Link == null)
            {
                throw new InvalidOperationException("Failed to create child item");
            }

            _childLink = _childItem.Link;

            // Update tree view
            editor.UpdateTreeView();
        }

        public void Undo(DLGEditor editor)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            if (_childItem == null || _childLink == null)
            {
                // Nothing to undo if child wasn't created
                return;
            }

            // Remove child link from parent node's Links collection
            if (_parentItem.Link != null && _parentItem.Link.Node != null)
            {
                _parentItem.Link.Node.Links.Remove(_childLink);
            }

            // Remove child item from parent item in the model
            _parentItem.RemoveChild(_childItem);

            // Update tree view
            editor.UpdateTreeView();
        }

        // Expose properties for testing
        public DLGStandardItem ParentItem => _parentItem;
        public DLGStandardItem ChildItem => _childItem;
        public DLGLink ChildLink => _childLink;
        public int ChildIndex => _childIndex;
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing.Resource.Generics.DLG;
using Avalonia.Controls;
using Avalonia.Input;

namespace HolocronToolset.Editors.DLG
{
    /// <summary>
    /// List widget for displaying DLG links.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/list_widget_base.py:79-184
    /// Original: class DLGListWidget(QListWidget):
    /// </summary>
    public class DLGListWidget : ListBox
    {
        private DLGEditor _editor;
        private DLGListWidgetItem _draggedItem;
        private DLGListWidgetItem _currentlyHoveredItem;
        private bool _useHoverText = true;

        /// <summary>
        /// Initializes a new instance of the DLGListWidget class.
        /// </summary>
        public DLGListWidget()
        {
        }

        /// <summary>
        /// Gets or sets the editor associated with this widget.
        /// </summary>
        public DLGEditor Editor
        {
            get => _editor;
            set => _editor = value;
        }

        /// <summary>
        /// Gets or sets whether to use hover text.
        /// </summary>
        public bool UseHoverText
        {
            get => _useHoverText;
            set => _useHoverText = value;
        }

        /// <summary>
        /// Initializes a new instance of DLGListWidget.
        /// </summary>
        /// <param name="editor">The DLG editor associated with this widget.</param>
        public DLGListWidget(DLGEditor editor)
        {
            _editor = editor;
            SelectionChanged += OnSelectionChanged;
            DoubleTapped += OnDoubleTapped;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_editor != null && SelectedItem is DLGListWidgetItem item && item.Link != null)
            {
                _editor.JumpToNode(item.Link);
            }
        }

        private void OnDoubleTapped(object sender, TappedEventArgs e)
        {
            if (_editor != null && SelectedItem is DLGListWidgetItem item && item.Link != null)
            {
                _editor.FocusOnNode(item.Link);
            }
        }

        /// <summary>
        /// Updates the item text and formatting based on the node data.
        /// Matching PyKotor implementation: def update_item(self, item: DLGListWidgetItem, cached_paths: tuple[str, str, str] | None = None)
        /// </summary>
        public void UpdateItem(DLGListWidgetItem item, Tuple<string, string, string> cachedPaths = null)
        {
            if (_editor == null || item == null || item.Link == null)
            {
                return;
            }

            Tuple<string, string, string> paths = cachedPaths ?? _editor.GetItemDlgPaths(item);
            string linkParentPath = paths.Item1;
            string linkPartialPath = paths.Item2;
            string nodePath = paths.Item3;

            bool isEntry = item.Link.Node is DLGEntry;
            string color = isEntry ? "red" : "blue";

            if (!string.IsNullOrEmpty(linkParentPath))
            {
                linkParentPath += "\\";
            }
            else
            {
                linkParentPath = "";
            }

            string hoverText1 = $"<span style='color:{color}; display:inline-block; vertical-align:top;'>{linkPartialPath} --></span>";
            string displayText2 = $"<div class='link-hover-text' style='display:inline-block; vertical-align:top; color:{color}; text-align:center;'>{nodePath}</div>";

            string defaultDisplay = $"<div class='link-container' style='white-space: nowrap;'>{displayText2}</div>";
            string hoverDisplay = $"<div class='link-container' style='white-space: nowrap;'>{hoverText1}{displayText2}</div>";

            // Store both display texts
            item.SetData(0, defaultDisplay); // DisplayRole
            item.SetData(2, hoverDisplay); // ExtraDisplayRole

            // Get tooltip text
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/list_widget_base.py:172
            // Original: text: str = repr(item.link.node) if self.editor._installation is None else self.editor._installation.string(item.link.node.text)
            string text;
            if (_editor?.Installation == null)
            {
                // When installation is not available, use a proper string representation similar to Python's repr()
                // Format: "DLGEntry(ListIndex=0)" or "DLGReply(ListIndex=1)"
                DLGNode node = item.Link?.Node;
                if (node == null)
                {
                    text = "";
                }
                else
                {
                    string nodeType = node is DLGEntry ? "DLGEntry" : "DLGReply";
                    text = $"{nodeType}(ListIndex={node.ListIndex})";
                }
            }
            else
            {
                // When installation is available, use it to get the localized string from TLK
                DLGNode node = item.Link?.Node;
                if (node?.Text != null)
                {
                    text = _editor.Installation.String(node.Text, "");
                }
                else
                {
                    text = "";
                }
            }
            item.TooltipText = $"{text}\n\n<i>Right click for more options</i>";
        }

        private readonly List<DLGListWidgetItem> _items = new List<DLGListWidgetItem>();

        /// <summary>
        /// Adds an item to the list.
        /// </summary>
        public void AddItem(DLGListWidgetItem item)
        {
            if (item == null)
            {
                return;
            }
            _items.Add(item);
            // In Avalonia ListBox, we need to use Items collection
            if (Items is System.Collections.IList list)
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// Clears all items from the list.
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            if (Items is System.Collections.IList list)
            {
                list.Clear();
            }
        }

        /// <summary>
        /// Gets the item at the specified index.
        /// </summary>
        public DLGListWidgetItem GetItem(int index)
        {
            if (index >= 0 && index < _items.Count)
            {
                return _items[index];
            }
            return null;
        }

        /// <summary>
        /// Gets the number of items in the list.
        /// </summary>
        public int Count => _items.Count;
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Andastra.Parsing.Resource.Generics.DLG;

/// <summary>
/// Represents a standard item in the DLG tree model.
/// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:52-100
/// Original: class DLGStandardItem(QStandardItem):
/// </summary>

namespace HolocronToolset.Editors.DLG
{
    public class DLGStandardItem
    {
        private readonly WeakReference<DLGLink> _linkRef;
        private readonly List<DLGStandardItem> _children = new List<DLGStandardItem>();
        private DLGStandardItem _parent;

        /// <summary>
        /// Gets the link associated with this item, or null if the reference is no longer valid.
        /// Matching PyKotor implementation: property link(self) -> DLGLink | None
        /// </summary>
        public DLGLink Link
        {
            get
            {
                if (_linkRef != null && _linkRef.TryGetTarget(out DLGLink link))
                {
                    return link;
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the number of child items.
        /// </summary>
        public int RowCount => _children.Count;

        /// <summary>
        /// Gets the parent item, or null if this is a root item.
        /// </summary>
        public DLGStandardItem Parent => _parent;

        /// <summary>
        /// Gets all child items.
        /// </summary>
        public IReadOnlyList<DLGStandardItem> Children => _children;

        /// <summary>
        /// Removes a child item from this item.
        /// </summary>
        /// <param name="child">The child item to remove.</param>
        /// <returns>True if the child was removed, false if it was not found.</returns>
        public bool RemoveChild(DLGStandardItem child)
        {
            if (child == null)
            {
                return false;
            }
            if (_children.Remove(child))
            {
                child._parent = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Initializes a new instance of DLGStandardItem with the specified link.
        /// </summary>
        public DLGStandardItem(DLGLink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }
            _linkRef = new WeakReference<DLGLink>(link);
        }

        /// <summary>
        /// Adds a child item to this item.
        /// </summary>
        public void AddChild(DLGStandardItem child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }
            if (child._parent != null)
            {
                child._parent._children.Remove(child);
            }
            child._parent = this;
            _children.Add(child);
        }

        /// <summary>
        /// Gets the index of this item in its parent's children list.
        /// </summary>
        public int GetIndex()
        {
            if (_parent == null)
            {
                return -1;
            }
            return _parent._children.IndexOf(this);
        }

        /// <summary>
        /// Gets the child item at the specified row and column.
        /// Matching PyKotor implementation: def child(self, row: int, column: int = 0) -> QStandardItem | None
        /// </summary>
        public DLGStandardItem Child(int row, int column = 0)
        {
            if (row < 0 || row >= _children.Count || column != 0)
            {
                return null;
            }
            return _children[row];
        }

        /// <summary>
        /// Gets whether this item has children.
        /// Matching PyKotor implementation: def hasChildren(self) -> bool
        /// </summary>
        public bool HasChildren()
        {
            return _children.Count > 0;
        }
    }
}

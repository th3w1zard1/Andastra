using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Andastra.Parsing.Resource.Generics.DLG;
using Avalonia.Controls;

namespace HolocronToolset.Editors
{
    /// <summary>
    /// List widget item for DLG links with weak reference support.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/list_widget_base.py:28-76
    /// Original: class DLGListWidgetItem(QListWidgetItem):
    /// </summary>
    public class DLGListWidgetItem
    {
        private readonly WeakReference<DLGLink> _linkRef;
        private readonly Dictionary<int, object> _dataCache = new Dictionary<int, object>();
        private bool _isOrphaned = false;

        /// <summary>
        /// Gets the link associated with this item, or null if the reference is no longer valid.
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
        /// Gets or sets whether this item is orphaned.
        /// </summary>
        public bool IsOrphaned
        {
            get => _isOrphaned;
            set => _isOrphaned = value;
        }

        /// <summary>
        /// Initializes a new instance of DLGListWidgetItem with the specified link.
        /// </summary>
        /// <param name="link">The DLG link to associate with this item.</param>
        /// <param name="linkRef">Optional weak reference to the link (if already created).</param>
        public DLGListWidgetItem(DLGLink link, WeakReference<DLGLink> linkRef = null)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }
            _linkRef = linkRef ?? new WeakReference<DLGLink>(link);
        }

        /// <summary>
        /// Gets data for the specified role, using cache if the item has been deleted.
        /// Matching PyKotor implementation: def data(self, role: int = Qt.ItemDataRole.UserRole) -> Any
        /// </summary>
        public object GetData(int role)
        {
            if (IsDeleted())
            {
                return _dataCache.ContainsKey(role) ? _dataCache[role] : null;
            }
            // In Avalonia, we don't have the same data role system as Qt
            // We'll use the cache to store display data
            return _dataCache.ContainsKey(role) ? _dataCache[role] : null;
        }

        /// <summary>
        /// Sets data for the specified role and updates the cache.
        /// Matching PyKotor implementation: def setData(self, role: int, value: Any)
        /// </summary>
        public void SetData(int role, object value)
        {
            _dataCache[role] = value;
        }

        /// <summary>
        /// Determines if this object has been deleted.
        /// Matching PyKotor implementation: def isDeleted(self) -> bool
        /// </summary>
        public bool IsDeleted()
        {
            // In C#, we can check if the weak reference is still alive
            if (_linkRef == null)
            {
                return true;
            }
            return !_linkRef.TryGetTarget(out _);
        }

        /// <summary>
        /// Gets the display text for this item.
        /// </summary>
        public string DisplayText
        {
            get => GetData(0) as string ?? "";
            set => SetData(0, value);
        }

        /// <summary>
        /// Gets the tooltip text for this item.
        /// </summary>
        public string TooltipText
        {
            get => GetData(1) as string ?? "";
            set => SetData(1, value);
        }

        /// <summary>
        /// Returns a string representation of this item for display in ListBox.
        /// </summary>
        public override string ToString()
        {
            string display = DisplayText;
            // Strip HTML tags for plain text display if needed
            if (!string.IsNullOrEmpty(display) && display.Contains("<"))
            {
                // Simple HTML tag removal for display
                display = System.Text.RegularExpressions.Regex.Replace(display, "<.*?>", "");
                display = System.Net.WebUtility.HtmlDecode(display);
            }
            return display;
        }
    }
}


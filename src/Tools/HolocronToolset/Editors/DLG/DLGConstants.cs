namespace HolocronToolset.Editors.DLG
{
    /// <summary>
    /// Constants for DLG editor model roles and MIME types.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/constants.py
    /// </summary>
    public static class DLGConstants
    {
        // Role constants (matching Qt ItemDataRole.UserRole + offset)
        public const int LinkParentNodePathRole = 256; // Qt.ItemDataRole.UserRole + 1
        public const int ExtraDisplayRole = 257; // Qt.ItemDataRole.UserRole + 2
        public const int DummyItem = 258; // Qt.ItemDataRole.UserRole + 3
        public const int CopyRole = 259; // Qt.ItemDataRole.UserRole + 4
        public const int DlgMimeDataRole = 260; // Qt.ItemDataRole.UserRole + 5
        public const int ModelInstanceIdRole = 261; // Qt.ItemDataRole.UserRole + 6

        // MIME type constants
        public const string QtStandardItemFormat = "application/x-qabstractitemmodeldatalist";
    }
}


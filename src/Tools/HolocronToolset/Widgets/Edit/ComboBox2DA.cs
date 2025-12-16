using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Resources;
using HolocronToolset.Data;

namespace HolocronToolset.Widgets.Edit
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:31
    // Original: class ComboBox2DA(QComboBox):
    public partial class ComboBox2DA : ComboBox
    {
        private bool _sortAlphabetically = false;
        private object _this2DA; // TODO: Use TwoDA type when available
        private HTInstallation _installation;
        private string _resname;

        // Public parameterless constructor for XAML
        public ComboBox2DA()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:57-68
        // Original: def currentIndex(self) -> int:
        public new int SelectedIndex
        {
            get
            {
                int currentIndex = base.SelectedIndex;
                if (currentIndex == -1)
                {
                    return 0;
                }
                // TODO: Get row index from item data when available
                return currentIndex;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:70-88
        // Original: def setCurrentIndex(self, row_in_2da: int):
        public new void SetSelectedIndex(int rowIn2DA)
        {
            // TODO: Find item with matching row index
            // For now, just set the index directly
            if (rowIn2DA >= 0 && rowIn2DA < Items.Count)
            {
                base.SelectedIndex = rowIn2DA;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:90-113
        // Original: def addItem(self, text: str, row: int | None = None):
        public void AddItem(string text, int? row = null)
        {
            int rowIndex = row ?? Items.Count;
            string displayText = text.StartsWith("[Modded Entry #") ? text : $"{text} [{rowIndex}]";
            Items.Add(displayText);
            // TODO: Store row index and real text in item data when available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:144-165
        // Original: def set_items(self, values: Iterable[str], ...):
        public void SetItems(IEnumerable<string> values, bool sortAlphabetically = true, bool cleanupStrings = true, bool ignoreBlanks = false)
        {
            _sortAlphabetically = sortAlphabetically;
            Items.Clear();

            int index = 0;
            foreach (string text in values)
            {
                string newText = text;
                if (cleanupStrings)
                {
                    newText = text.Replace("TRAP_", "");
                    newText = newText.Replace("GENDER_", "");
                    newText = newText.Replace("_", " ");
                }
                if (!ignoreBlanks || (!string.IsNullOrEmpty(newText) && !string.IsNullOrWhiteSpace(newText)))
                {
                    AddItem(newText, index);
                }
                index++;
            }

            // TODO: Enable/disable sort when available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:175-179
        // Original: def set_context(self, data: TwoDA | None, install: HTInstallation, resname: str):
        public void SetContext(object data, HTInstallation install, string resname)
        {
            if (data != null)
            {
                _this2DA = data;
            }
            _installation = install;
            _resname = resname;
        }
    }
}

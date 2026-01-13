using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource;
using HolocronToolset.Data;

namespace HolocronToolset.Widgets.Edit
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:31
    // Original: class ComboBox2DA(QComboBox):
    public partial class ComboBox2DA : ComboBox
    {
        // Item wrapper to store row index and display text
        // Matching PyKotor: QComboBox.setItemData() stores row index with each item
        private class ComboBoxItem
        {
            public string DisplayText { get; set; }
            public int RowIndex { get; set; }
            public string RealText { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private bool _sortAlphabetically = false;
        private TwoDA _this2DA; // Can be null (matching Python: 2DA | None)
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
                // Get row index from item data (matching PyKotor: itemData(currentIndex))
                if (currentIndex >= 0 && currentIndex < Items.Count)
                {
                    object item = Items[currentIndex];
                    if (item is ComboBoxItem comboItem)
                    {
                        return comboItem.RowIndex;
                    }
                }
                return currentIndex;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:70-88
        // Original: def setCurrentIndex(self, row_in_2da: int):
        // Python implementation: Iterates through items, finds one with matching row index via itemData(), sets currentIndex
        public void SetSelectedIndex(int rowIn2DA)
        {
            // Find item with matching row index (matching PyKotor: searches items for matching itemData)
            for (int i = 0; i < Items.Count; i++)
            {
                object item = Items[i];
                if (item is ComboBoxItem comboItem && comboItem.RowIndex == rowIn2DA)
                {
                    base.SelectedIndex = i;
                    return;
                }
            }
            // If no match found and rowIn2DA is within valid range, set directly (fallback behavior)
            if (rowIn2DA >= 0 && rowIn2DA < Items.Count)
            {
                base.SelectedIndex = rowIn2DA;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:90-113
        // Original: def addItem(self, text: str, row: int | None = None):
        // Python implementation: Stores row index via setItemData(), stores real text separately
        public void AddItem(string text, int? row = null)
        {
            int rowIndex = row ?? Items.Count;
            string displayText = text.StartsWith("[Modded Entry #") ? text : $"{text} [{rowIndex}]";
            // Store row index and real text in item data (matching PyKotor: setItemData())
            ComboBoxItem item = new ComboBoxItem
            {
                DisplayText = displayText,
                RowIndex = rowIndex,
                RealText = text
            };
            Items.Add(item);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:144-165
        // Original: def set_items(self, values: Iterable[str], ...):
        // Python implementation: Clears items, adds each value with cleanup/blank filtering, then sorts if enabled
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

            // Sort items alphabetically by display text if enabled (matching PyKotor: model().sort(0) when sortAlphabetically is True)
            if (_sortAlphabetically && Items.Count > 0)
            {
                var itemsList = Items.Cast<ComboBoxItem>().ToList();
                itemsList.Sort((a, b) => string.Compare(a.DisplayText, b.DisplayText, StringComparison.OrdinalIgnoreCase));
                Items.Clear();
                foreach (var item in itemsList)
                {
                    Items.Add(item);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/combobox_2da.py:175-179
        // Original: def set_context(self, data: 2DA | None, install: HTInstallation, resname: str):
        public void SetContext(TwoDA data, HTInstallation install, string resname)
        {
            _this2DA = data;
            _installation = install;
            _resname = resname;
        }
    }
}

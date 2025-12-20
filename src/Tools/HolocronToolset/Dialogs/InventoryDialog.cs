using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.Capsule;
using HolocronToolset.Data;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py:71
    // Original: class InventoryEditor(QDialog):
    public partial class InventoryDialog : Window
    {
        private HTInstallation _installation;
        private List<InventoryItem> _inventory;
        private Dictionary<EquipmentSlot, InventoryItem> _equipment;
        private bool _droid;
        private bool _isStore;

        // Public parameterless constructor for XAML
        public InventoryDialog() : this(null, null, null, null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py:72-150
        // Original: def __init__(self, parent, installation, capsules, folders, inventory, equipment, ...):
        // Note: PyKotor uses Sequence[LazyCapsule] but UTM/UTP editors pass list[Capsule], so we use List<Capsule> for compatibility
        public InventoryDialog(
            Window parent,
            HTInstallation installation,
            List<Capsule> capsules,
            List<string> folders,
            List<InventoryItem> inventory,
            Dictionary<EquipmentSlot, InventoryItem> equipment,
            bool droid = false,
            bool hideEquipment = false,
            bool isStore = false)
        {
            InitializeComponent();
            _installation = installation;
            _inventory = inventory ?? new List<InventoryItem>();
            _equipment = equipment ?? new Dictionary<EquipmentSlot, InventoryItem>();
            _droid = droid;
            _isStore = isStore;
            SetupUI();
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

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
            }
        }

        private void SetupProgrammaticUI()
        {
            Title = "Inventory Editor";
            Width = 800;
            Height = 600;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Inventory Editor",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            var okButton = new Button { Content = "OK" };
            okButton.Click += (sender, e) => Close();
            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (sender, e) => Close();

            panel.Children.Add(titleLabel);
            panel.Children.Add(okButton);
            panel.Children.Add(cancelButton);
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py
        // Original: self.ui = Ui_Dialog() - UI wrapper class exposing all controls
        public InventoryDialogUi Ui { get; private set; }

        private DataGrid _contentsTable;

        private void SetupUI()
        {
            // Find controls from XAML and set up event handlers
            try
            {
                _contentsTable = this.FindControl<DataGrid>("contentsTable");
            }
            catch
            {
                // XAML not loaded or control not found - will use programmatic UI
                _contentsTable = null;
            }

            // Create UI wrapper for testing
            Ui = new InventoryDialogUi
            {
                ContentsTable = _contentsTable
            };
        }

        public List<InventoryItem> Inventory => _inventory;
        public Dictionary<EquipmentSlot, InventoryItem> Equipment => _equipment;

        // Matching PyKotor implementation: dialog.exec() returns bool
        // For Avalonia compatibility, provide ShowDialog method
        public bool ShowDialog()
        {
            // Show dialog and return result
            // TODO: SIMPLIFIED - This is a simplified implementation - in a full implementation, we'd use ShowDialogAsync
            // For now, we'll track if OK was clicked
            bool result = false;
            var okButton = this.FindControl<Button>("okButton");
            if (okButton != null)
            {
                EventHandler<Avalonia.Interactivity.RoutedEventArgs> okHandler = null;
                okHandler = (s, e) =>
                {
                    result = true;
                    okButton.Click -= okHandler;
                    Close();
                };
                okButton.Click += okHandler;
            }
            this.Show();
            // TODO: SIMPLIFIED - Note: This is a simplified synchronous implementation
            // In a full implementation, we'd use ShowDialogAsync and await the result
            return result;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py
    // Original: self.ui = Ui_Dialog() - UI wrapper class exposing all controls
    public class InventoryDialogUi
    {
        public DataGrid ContentsTable { get; set; }
    }
}

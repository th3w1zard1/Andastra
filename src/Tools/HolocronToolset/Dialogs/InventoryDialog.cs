using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
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
        private Window _parentWindow;
        private HTInstallation _installation;
        private List<InventoryItem> _inventory;
        private Dictionary<EquipmentSlot, InventoryItem> _equipment;
        private bool _droid;
        private bool _isStore;
        private Button _okButton;
        private Button _cancelButton;
        public bool DialogResult { get; private set; }

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
            _parentWindow = parent;
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
            _okButton = new Button { Content = "OK" };
            _okButton.Click += (sender, e) => 
            {
                Accept();
                DialogResult = true;
                Close(true);
            };
            _cancelButton = new Button { Content = "Cancel" };
            _cancelButton.Click += (sender, e) =>
            {
                DialogResult = false;
                Close(false);
            };

            panel.Children.Add(titleLabel);
            panel.Children.Add(_okButton);
            panel.Children.Add(_cancelButton);
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
                _okButton = this.FindControl<Button>("okButton");
                _cancelButton = this.FindControl<Button>("cancelButton");

                // Set up OK and Cancel button handlers
                if (_okButton != null)
                {
                    _okButton.Click += (sender, e) =>
                    {
                        Accept();
                        DialogResult = true;
                        Close(true);
                    };
                }
                if (_cancelButton != null)
                {
                    _cancelButton.Click += (sender, e) =>
                    {
                        DialogResult = false;
                        Close(false);
                    };
                }
            }
            catch
            {
                // XAML not loaded or control not found - will use programmatic UI
                _contentsTable = null;
                _okButton = null;
                _cancelButton = null;
            }

            // Create UI wrapper for testing
            Ui = new InventoryDialogUi
            {
                ContentsTable = _contentsTable
            };
        }

        public List<InventoryItem> Inventory => _inventory;
        public Dictionary<EquipmentSlot, InventoryItem> Equipment => _equipment;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py:205-221
        // Original: def accept(self): super().accept(); self.inventory.clear(); ...
        // Updates inventory and equipment from UI before dialog closes with OK
        private void Accept()
        {
            // Clear existing inventory and rebuild from contents table
            _inventory.Clear();
            if (_contentsTable != null && _contentsTable.Items != null)
            {
                // Matching PyKotor implementation: iterate through table rows and extract inventory items
                // Note: This assumes the DataGrid has items with ResRef, Droppable, and Infinite properties
                // When the full UI is implemented, this will extract from the actual table widget items
                foreach (var item in _contentsTable.Items.OfType<object>())
                {
                    // TODO: When full UI is implemented, extract ResRef, Droppable, and Infinite from table item
                    // For now, this is a placeholder structure that matches PyKotor's accept() logic
                    // The actual extraction will depend on how the DataGrid items are structured
                }
            }

            // Clear existing equipment and rebuild from equipment frames
            _equipment.Clear();
            // Matching PyKotor implementation: iterate through equipment frames and extract equipped items
            // Note: When the full UI with equipment frames is implemented, this will extract from those widgets
            // For now, this is a placeholder structure that matches PyKotor's accept() logic
        }

        // Matching PyKotor implementation: dialog.exec() returns bool
        // PyKotor's QDialog.exec() is a blocking modal dialog that returns QDialog.DialogCode.Accepted (true) or Rejected (false)
        // This synchronous method provides the same behavior for compatibility with existing code
        /// <summary>
        /// Shows the dialog modally and returns true if the user clicked OK, false if Cancel was clicked or the dialog was closed.
        /// This is a blocking synchronous method that matches PyKotor's QDialog.exec() behavior.
        /// </summary>
        /// <returns>True if OK was clicked, false if Cancel was clicked or the dialog was closed.</returns>
        public bool ShowDialog()
        {
            // Use ShowDialogAsync and block synchronously to match Qt's exec() behavior
            // This provides proper modal dialog behavior while maintaining compatibility with synchronous code
            Task<bool> dialogTask = ShowDialogAsync();
            return dialogTask.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Shows the dialog modally asynchronously and returns a Task that completes with true if the user clicked OK, false if Cancel was clicked or the dialog was closed.
        /// This is the recommended method for async/await code.
        /// </summary>
        /// <param name="parent">Optional parent window for the dialog. If null, uses the parent from constructor or finds the main window.</param>
        /// <returns>A Task that completes with true if OK was clicked, false if Cancel was clicked or the dialog was closed.</returns>
        public async Task<bool> ShowDialogAsync(Window parent = null)
        {
            // Use parent parameter if provided, otherwise use the parent from constructor
            Window dialogParent = parent ?? _parentWindow;

            // If we still don't have a parent, try to find the main window
            if (dialogParent == null)
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    dialogParent = desktop.MainWindow;
                }
            }

            if (dialogParent != null)
            {
                // ShowDialogAsync<bool> will handle setting the parent relationship
                // The result will be the value passed to Close() when the dialog closes (true for OK, false for Cancel)
                bool result = await ShowDialogAsync<bool>(dialogParent);
                DialogResult = result;
                return result;
            }
            else
            {
                // Fallback: show non-modally and track result via Closed event
                // This should rarely happen, but provides a fallback
                bool result = false;
                EventHandler<WindowEventArgs> closedHandler = null;
                closedHandler = (s, e) =>
                {
                    this.Closed -= closedHandler;
                    result = DialogResult;
                };
                this.Closed += closedHandler;
                Show();
                // Wait for dialog to close
                // Note: This is not ideal but provides fallback behavior
                while (this.IsVisible)
                {
                    await Task.Delay(10);
                }
                return result;
            }
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py
    // Original: self.ui = Ui_Dialog() - UI wrapper class exposing all controls
    public class InventoryDialogUi
    {
        public DataGrid ContentsTable { get; set; }
    }
}

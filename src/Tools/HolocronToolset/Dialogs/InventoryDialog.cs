using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.Capsule;
using HolocronToolset.Data;
using System.ComponentModel;

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

            // Configure DataGrid if it exists
            if (_contentsTable != null)
            {
                // Set up DataGrid columns if not already configured
                if (_contentsTable.Columns.Count == 0)
                {
                    // Matching PyKotor implementation: InventoryTable has 3 columns:
                    // Column 0: Icon (QTableWidgetItem with icon)
                    // Column 1: ResRef (InventoryTableResnameItem)
                    // Column 2: Name (QTableWidgetItem with name)
                    // For Avalonia DataGrid, we'll use bound properties from InventoryTableRowItem
                    _contentsTable.AutoGenerateColumns = false;
                    _contentsTable.CanUserReorderColumns = true;
                    _contentsTable.CanUserResizeColumns = true;
                    _contentsTable.CanUserSortColumns = true;
                    _contentsTable.GridLinesVisibility = DataGridGridLinesVisibility.All;
                    _contentsTable.SelectionMode = DataGridSelectionMode.Single;

                    // Column 0: ResRef (matching PyKotor column 1 - the ResRef column)
                    _contentsTable.Columns.Add(new DataGridTextColumn
                    {
                        Header = "ResRef",
                        Binding = new Avalonia.Data.Binding("ResRefString"),
                        Width = new DataGridLength(150),
                        IsReadOnly = false
                    });

                    // Column 1: Name (matching PyKotor column 2)
                    _contentsTable.Columns.Add(new DataGridTextColumn
                    {
                        Header = "Name",
                        Binding = new Avalonia.Data.Binding("Name"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                        IsReadOnly = true
                    });

                    // Column 2: Droppable (for non-store inventories) or Infinite (for store inventories)
                    // Matching PyKotor implementation: droppable is shown for non-stores, infinite for stores
                    // In PyKotor, the context menu shows either "Droppable" or "Infinite" based on is_store flag
                    // In Avalonia DataGrid, columns don't have Visibility property, so we conditionally add columns
                    if (_isStore)
                    {
                        // For stores, show Infinite column
                        var infiniteColumn = new DataGridCheckBoxColumn
                        {
                            Header = "Infinite",
                            Binding = new Avalonia.Data.Binding("Infinite"),
                            Width = new DataGridLength(100),
                            IsReadOnly = false
                        };
                        _contentsTable.Columns.Add(infiniteColumn);
                    }
                    else
                    {
                        // For non-stores, show Droppable column
                        var droppableColumn = new DataGridCheckBoxColumn
                        {
                            Header = "Droppable",
                            Binding = new Avalonia.Data.Binding("Droppable"),
                            Width = new DataGridLength(100),
                            IsReadOnly = false
                        };
                        _contentsTable.Columns.Add(droppableColumn);
                    }
                }
            }

            // Create UI wrapper for testing
            Ui = new InventoryDialogUi
            {
                ContentsTable = _contentsTable
            };

            // Populate DataGrid with initial inventory
            PopulateInventoryTable();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py:191-197
        // Original: for item in self.inventory:
        //          try:
        //              self.ui.contentsTable.add_item(str(item.resref), droppable=item.droppable, infinite=item.infinite)
        //          except FileNotFoundError:
        //              RobustLogger().error(f"{item.resref}.uti did not exist in the installation", exc_info=True)
        //          except (OSError, ValueError):
        //              RobustLogger().error(f"{item.resref}.uti is corrupted", exc_info=True)
        // Populates the contents table DataGrid with items from the initial inventory list
        private void PopulateInventoryTable()
        {
            if (_contentsTable == null || _inventory == null)
            {
                return;
            }

            var rowItems = new List<InventoryTableRowItem>();

            foreach (var item in _inventory)
            {
                try
                {
                    // Get item information (filepath, name) from installation if available
                    string filePath = "";
                    string name = item.ResRef?.ToString() ?? "";

                    if (_installation != null && item.ResRef != null)
                    {
                        // Try to get UTI file information
                        // Matching PyKotor: filepath, name, uti = cast("InventoryEditor", self.window()).get_item(resname, "")
                        // TODO: STUB - For now, we'll use the ResRef as the name if we can't get the actual name
                        // In a full implementation, this would call a method similar to get_item() to retrieve UTI data
                        name = item.ResRef.ToString();
                    }

                    // Create row item matching PyKotor: InventoryTableResnameItem(resname, filepath, name, droppable=droppable, infinite=infinite)
                    var rowItem = new InventoryTableRowItem(
                        item.ResRef ?? ResRef.FromBlank(),
                        filePath,
                        name,
                        item.Droppable,
                        item.Infinite);

                    rowItems.Add(rowItem);
                }
                catch (FileNotFoundException)
                {
                    // Matching PyKotor: RobustLogger().error(f"{item.resref}.uti did not exist in the installation", exc_info=True)
                    // TODO: STUB - For now, we'll skip items that don't exist
                    // In a full implementation, this would log an error
                    continue;
                }
                catch (Exception)
                {
                    // Matching PyKotor: RobustLogger().error(f"{item.resref}.uti is corrupted", exc_info=True)
                    // TODO: STUB - For now, we'll skip corrupted items
                    // In a full implementation, this would log an error
                    continue;
                }
            }

            // Set ItemsSource to populate the DataGrid
            _contentsTable.ItemsSource = rowItems;
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
            if (_contentsTable != null && _contentsTable.ItemsSource != null)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py:208-212
                // Original: for i in range(self.ui.contentsTable.rowCount()):
                //          table_item: QTableWidgetItem | None = self.ui.contentsTable.item(i, 1)
                //          if not isinstance(table_item, ItemContainer):
                //              continue
                //          self.inventory.append(InventoryItem(ResRef(table_item.resname), table_item.droppable, table_item.infinite))
                // In PyKotor, column 1 (index 1) contains the InventoryTableResnameItem which extends ItemContainer
                // and has resname, droppable, and infinite properties.
                // In Avalonia DataGrid, each item in ItemsSource represents a row, so we iterate through ItemsSource
                // and extract ResRef, Droppable, and Infinite from each row item.
                var itemsSource = _contentsTable.ItemsSource as System.Collections.IEnumerable;
                if (itemsSource != null)
                {
                    foreach (var rowItem in itemsSource.OfType<object>())
                    {
                        // Try to extract from InventoryTableRowItem (our custom row item class)
                        if (rowItem is InventoryTableRowItem tableRowItem)
                        {
                            // Extract ResRef, Droppable, and Infinite from the row item
                            // Matching PyKotor: InventoryItem(ResRef(table_item.resname), table_item.droppable, table_item.infinite)
                            if (tableRowItem.ResRef != null && !string.IsNullOrEmpty(tableRowItem.ResRef.ToString()))
                            {
                                _inventory.Add(new InventoryItem(tableRowItem.ResRef, tableRowItem.Droppable, tableRowItem.Infinite));
                            }
                        }
                        // Fallback: Try to extract using reflection for compatibility with other row item types
                        else if (rowItem != null)
                        {
                            var rowType = rowItem.GetType();
                            var resRefProperty = rowType.GetProperty("ResRef");
                            var droppableProperty = rowType.GetProperty("Droppable");
                            var infiniteProperty = rowType.GetProperty("Infinite");

                            if (resRefProperty != null)
                            {
                                var resRefValue = resRefProperty.GetValue(rowItem) as ResRef;
                                if (resRefValue != null && !string.IsNullOrEmpty(resRefValue.ToString()))
                                {
                                    bool droppable = false;
                                    bool infinite = false;

                                    if (droppableProperty != null)
                                    {
                                        var droppableValue = droppableProperty.GetValue(rowItem);
                                        if (droppableValue is bool droppableBool)
                                        {
                                            droppable = droppableBool;
                                        }
                                    }

                                    if (infiniteProperty != null)
                                    {
                                        var infiniteValue = infiniteProperty.GetValue(rowItem);
                                        if (infiniteValue is bool infiniteBool)
                                        {
                                            infinite = infiniteBool;
                                        }
                                    }

                                    _inventory.Add(new InventoryItem(resRefValue, droppable, infinite));
                                }
                            }
                        }
                    }
                }
            }

            // Clear existing equipment and rebuild from equipment frames
            _equipment.Clear();
            // Matching PyKotor implementation: iterate through equipment frames and extract equipped items
            // Note: When the full UI with equipment frames is implemented, this will extract from those widgets
            // TODO: STUB - For now, this is a placeholder structure that matches PyKotor's accept() logic
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
                // ShowDialogAsync will handle setting the parent relationship
                // The result will be the value passed to Close() when the dialog closes (true for OK, false for Cancel)
                var resultObj = await ShowDialogAsync(dialogParent);
                bool result = resultObj is bool b ? b : false;
                DialogResult = result;
                return result;
            }
            else
            {
                    // Fallback: show non-modally and track result via Closed event
                    // This should rarely happen, but provides a fallback
                    bool result = false;
                    EventHandler closedHandler = null;
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

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py:607-625
    // Original: class InventoryTableResnameItem(ItemContainer, QTableWidgetItem):
    // This class represents a row item in the inventory DataGrid, containing ResRef, Droppable, and Infinite properties.
    // In PyKotor, InventoryTableResnameItem extends both ItemContainer (which has droppable and infinite) and QTableWidgetItem (which has resname).
    // In Avalonia, we use a simple class with properties that can be bound to DataGrid columns.
    public class InventoryTableRowItem : INotifyPropertyChanged
    {
        private ResRef _resRef;
        private bool _droppable;
        private bool _infinite;
        private string _name;
        private string _filePath;

        // Matching PyKotor: resname property (stored as ResRef)
        public ResRef ResRef
        {
            get { return _resRef; }
            set
            {
                if (_resRef != value)
                {
                    _resRef = value;
                    OnPropertyChanged(nameof(ResRef));
                    OnPropertyChanged(nameof(ResRefString));
                }
            }
        }

        // String representation of ResRef for display in DataGrid
        public string ResRefString
        {
            get { return _resRef?.ToString() ?? ""; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    ResRef = ResRef.FromString(value);
                }
                else
                {
                    ResRef = ResRef.FromBlank();
                }
            }
        }

        // Matching PyKotor: droppable property from ItemContainer
        public bool Droppable
        {
            get { return _droppable; }
            set
            {
                if (_droppable != value)
                {
                    _droppable = value;
                    OnPropertyChanged(nameof(Droppable));
                }
            }
        }

        // Matching PyKotor: infinite property from ItemContainer
        public bool Infinite
        {
            get { return _infinite; }
            set
            {
                if (_infinite != value)
                {
                    _infinite = value;
                    OnPropertyChanged(nameof(Infinite));
                }
            }
        }

        // Matching PyKotor: name property (display name of the item)
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        // Matching PyKotor: filepath property (file path to the item resource)
        public string FilePath
        {
            get { return _filePath; }
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

        // Matching PyKotor: ItemContainer.__init__(self, droppable=droppable, infinite=infinite)
        // and InventoryTableResnameItem.__init__(self, resname, filepath, name, *, droppable, infinite)
        public InventoryTableRowItem(ResRef resRef, string filePath, string name, bool droppable = false, bool infinite = false)
        {
            _resRef = resRef ?? ResRef.FromBlank();
            _filePath = filePath ?? "";
            _name = name ?? "";
            _droppable = droppable;
            _infinite = infinite;
        }

        // Default constructor for XAML binding
        public InventoryTableRowItem()
        {
            _resRef = ResRef.FromBlank();
            _filePath = "";
            _name = "";
            _droppable = false;
            _infinite = false;
        }

        // Matching PyKotor: ItemContainer.set_item(self, resname, filepath, name, *, droppable, infinite)
        public void SetItem(ResRef resRef, string filePath, string name, bool droppable, bool infinite)
        {
            ResRef = resRef ?? ResRef.FromBlank();
            FilePath = filePath ?? "";
            Name = name ?? "";
            Droppable = droppable;
            Infinite = infinite;
        }

        // Matching PyKotor: ItemContainer.toggle_droppable(self)
        public void ToggleDroppable()
        {
            Droppable = !Droppable;
        }

        // Matching PyKotor: ItemContainer.toggle_infinite(self)
        public void ToggleInfinite()
        {
            Infinite = !Infinite;
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

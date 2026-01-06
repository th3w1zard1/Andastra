using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Resource.Generics.UTI;
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
                ContentsTable = _contentsTable,
                // Try to find equipment tabs from XAML if they exist
                StandardEquipmentTab = this.FindControl<Control>("standardEquipmentTab"),
                NaturalEquipmentTab = this.FindControl<Control>("naturalEquipmentTab")
            };

            // Populate DataGrid with initial inventory
            PopulateInventoryTable();
        }

        // Matching PyKotor implementation: get_item method to retrieve UTI data
        // Original: def get_item(self, resname: str, default: str = "") -> tuple[str, str, UTI]:
        // Returns (filepath, name, uti) for the given ResRef, or (None, resname, None) if not found
        private (string filepath, string name, UTI uti) GetItem(string resname)
        {
            if (_installation == null || string.IsNullOrWhiteSpace(resname))
            {
                return (null, resname, null);
            }

            try
            {
                // Try to find the UTI resource
                // Matching PyKotor: uses installation.resource() to get UTI data
                var resRef = ResRef.FromString(resname);
                var resourceResult = _installation.Resource(resRef.ToString(), ResourceType.UTI);

                if (resourceResult == null || resourceResult.Data == null)
                {
                    return (null, resname, null);
                }

                // Parse the UTI data
                // Matching PyKotor: uses UTIHelpers.read_uti() equivalent
                UTI uti = UTIHelpers.ReadUti(resourceResult.Data);

                // Get the display name from the UTI
                // Matching PyKotor: name = uti.name if uti else resname
                string displayName = uti.Name?.ToString() ?? resname;

                // Return filepath, name, and UTI object
                // Matching PyKotor: return filepath, name, uti
                return (resourceResult.FilePath, displayName, uti);
            }
            catch (Exception)
            {
                // If anything fails, return the resname as fallback
                // Matching PyKotor: error handling for corrupted/missing files
                return (null, resname, null);
            }
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
                    // Get item information (filepath, name, uti) from installation if available
                    // Matching PyKotor: filepath, name, uti = cast("InventoryEditor", self.window()).get_item(resname, "")
                    string filePath = "";
                    string name = item.ResRef?.ToString() ?? "";
                    UTI uti = null;

                    if (_installation != null && item.ResRef != null)
                    {
                        // Use get_item method to retrieve UTI data
                        var (utiFilePath, utiName, utiObject) = GetItem(item.ResRef.ToString());
                        filePath = utiFilePath ?? "";
                        name = utiName ?? item.ResRef.ToString();
                        uti = utiObject;
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
                catch (FileNotFoundException ex)
                {
                    // Matching PyKotor: RobustLogger().error(f"{item.resref}.uti did not exist in the installation", exc_info=True)
                    // Log error with exception information (matching exc_info=True in PyKotor)
                    string resrefStr = item.ResRef?.ToString() ?? "unknown";
                    System.Console.WriteLine($"[ERROR] {resrefStr}.uti did not exist in the installation");
                    System.Console.WriteLine($"[ERROR] Exception details: {ex.GetType().Name}: {ex.Message}");
                    if (ex.StackTrace != null)
                    {
                        System.Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                    }
                    // Skip items that don't exist (matching PyKotor behavior - continues after logging)
                    continue;
                }
                catch (Exception ex)
                {
                    // Matching PyKotor: RobustLogger().error(f"{item.resref}.uti is corrupted", exc_info=True)
                    // Log error with exception information (matching exc_info=True in PyKotor)
                    string resrefStr = item.ResRef?.ToString() ?? "unknown";
                    System.Console.WriteLine($"[ERROR] {resrefStr}.uti is corrupted");
                    System.Console.WriteLine($"[ERROR] Exception details: {ex.GetType().Name}: {ex.Message}");
                    if (ex.StackTrace != null)
                    {
                        System.Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                    }
                    // Skip corrupted items (matching PyKotor behavior - continues after logging)
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
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py:214-221
            // Original: self.equipment.clear()
            //          widget: DropFrame | QObject
            //          for widget in self.ui.standardEquipmentTab.children() + self.ui.naturalEquipmentTab.children():
            //              if "DropFrame" in widget.__class__.__name__ and getattr(widget, "resname", None):
            //                  casted_widget: DropFrame = cast("DropFrame", widget)
            //                  self.equipment[casted_widget.slot] = InventoryItem(ResRef(casted_widget.resname), casted_widget.droppable, casted_widget.infinite)
            ExtractEquipmentFromFrames();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/inventory.py:214-221
        // Original: Iterates through standardEquipmentTab and naturalEquipmentTab children to find DropFrame widgets
        // and extract equipment information (slot, resname, droppable, infinite)
        private void ExtractEquipmentFromFrames()
        {
            // Try to find equipment tabs from UI
            // Matching PyKotor: self.ui.standardEquipmentTab.children() + self.ui.naturalEquipmentTab.children()
            var equipmentTabWidgets = new List<Control>();

            // Try to find standardEquipmentTab
            var standardEquipmentTab = Ui?.StandardEquipmentTab ?? this.FindControl<Control>("standardEquipmentTab");
            if (standardEquipmentTab != null)
            {
                equipmentTabWidgets.AddRange(GetAllChildControls(standardEquipmentTab));
            }

            // Try to find naturalEquipmentTab
            var naturalEquipmentTab = Ui?.NaturalEquipmentTab ?? this.FindControl<Control>("naturalEquipmentTab");
            if (naturalEquipmentTab != null)
            {
                equipmentTabWidgets.AddRange(GetAllChildControls(naturalEquipmentTab));
            }

            // Iterate through all widgets found in equipment tabs
            // Matching PyKotor: for widget in self.ui.standardEquipmentTab.children() + self.ui.naturalEquipmentTab.children():
            foreach (var widget in equipmentTabWidgets)
            {
                // Matching PyKotor: if "DropFrame" in widget.__class__.__name__ and getattr(widget, "resname", None):
                // Check if widget has DropFrame-like properties using reflection
                // This works with both actual DropFrame implementations and any widget with the required properties
                var widgetType = widget.GetType();
                string typeName = widgetType.Name;

                // Check if this looks like a DropFrame (has "DropFrame" in class name or has required properties)
                bool isDropFrameLike = typeName.Contains("DropFrame") || HasDropFrameProperties(widgetType);

                if (isDropFrameLike)
                {
                    // Try to get resname property (must be non-null/non-empty to add to equipment)
                    // Matching PyKotor: getattr(widget, "resname", None)
                    var resnameProperty = widgetType.GetProperty("resname") ?? widgetType.GetProperty("Resname") ?? widgetType.GetProperty("ResName");
                    if (resnameProperty != null)
                    {
                        var resnameValue = resnameProperty.GetValue(widget);
                        string resname = resnameValue?.ToString() ?? "";

                        // Only add to equipment if resname is not null/empty
                        // Matching PyKotor: getattr(widget, "resname", None) - only proceed if resname exists
                        if (!string.IsNullOrEmpty(resname))
                        {
                            // Get slot property
                            // Matching PyKotor: casted_widget.slot
                            var slotProperty = widgetType.GetProperty("slot") ?? widgetType.GetProperty("Slot");
                            EquipmentSlot slot = EquipmentSlot.INVALID;
                            if (slotProperty != null)
                            {
                                var slotValue = slotProperty.GetValue(widget);
                                if (slotValue is EquipmentSlot equipmentSlot)
                                {
                                    slot = equipmentSlot;
                                }
                                else if (slotValue != null)
                                {
                                    // Try to convert from int or other types
                                    if (Enum.TryParse<EquipmentSlot>(slotValue.ToString(), out EquipmentSlot parsedSlot))
                                    {
                                        slot = parsedSlot;
                                    }
                                }
                            }

                            // Get droppable property
                            // Matching PyKotor: casted_widget.droppable
                            bool droppable = false;
                            var droppableProperty = widgetType.GetProperty("droppable") ?? widgetType.GetProperty("Droppable");
                            if (droppableProperty != null)
                            {
                                var droppableValue = droppableProperty.GetValue(widget);
                                if (droppableValue is bool droppableBool)
                                {
                                    droppable = droppableBool;
                                }
                            }

                            // Get infinite property
                            // Matching PyKotor: casted_widget.infinite
                            bool infinite = false;
                            var infiniteProperty = widgetType.GetProperty("infinite") ?? widgetType.GetProperty("Infinite");
                            if (infiniteProperty != null)
                            {
                                var infiniteValue = infiniteProperty.GetValue(widget);
                                if (infiniteValue is bool infiniteBool)
                                {
                                    infinite = infiniteBool;
                                }
                            }

                            // Only add to equipment if slot is valid (matching PyKotor behavior)
                            // Matching PyKotor: self.equipment[casted_widget.slot] = InventoryItem(ResRef(casted_widget.resname), casted_widget.droppable, casted_widget.infinite)
                            if (slot != EquipmentSlot.INVALID)
                            {
                                var resRef = ResRef.FromString(resname);
                                _equipment[slot] = new InventoryItem(resRef, droppable, infinite);
                            }
                        }
                    }
                }
            }
        }

        // Helper method to check if a type has DropFrame-like properties (resname, slot, droppable, infinite)
        private bool HasDropFrameProperties(System.Type type)
        {
            var resnameProperty = type.GetProperty("resname") ?? type.GetProperty("Resname") ?? type.GetProperty("ResName");
            var slotProperty = type.GetProperty("slot") ?? type.GetProperty("Slot");
            var droppableProperty = type.GetProperty("droppable") ?? type.GetProperty("Droppable");
            var infiniteProperty = type.GetProperty("infinite") ?? type.GetProperty("Infinite");

            // Consider it DropFrame-like if it has at least resname and slot properties
            return resnameProperty != null && slotProperty != null;
        }

        // Helper method to recursively get all child controls from a parent control
        // Matching PyKotor: widget.children() - gets all child widgets
        private List<Control> GetAllChildControls(Control parent)
        {
            var children = new List<Control>();
            if (parent == null)
            {
                return children;
            }

            // In Avalonia, controls can have children in different ways depending on the control type
            // Try to get children from common container types
            if (parent is Panel panel)
            {
                foreach (var child in panel.Children.OfType<Control>())
                {
                    children.Add(child);
                    // Recursively get children of children
                    children.AddRange(GetAllChildControls(child));
                }
            }
            else if (parent is Decorator decorator && decorator.Child is Control decoratorChild)
            {
                children.Add(decoratorChild);
                children.AddRange(GetAllChildControls(decoratorChild));
            }
            else if (parent is ContentControl contentControl && contentControl.Content is Control contentChild)
            {
                children.Add(contentChild);
                children.AddRange(GetAllChildControls(contentChild));
            }
            else
            {
                // Try to use reflection to find children property
                var childrenProperty = parent.GetType().GetProperty("Children");
                if (childrenProperty != null)
                {
                    var childrenValue = childrenProperty.GetValue(parent);
                    if (childrenValue is System.Collections.IEnumerable childrenEnumerable)
                    {
                        foreach (var child in childrenEnumerable.OfType<Control>())
                        {
                            children.Add(child);
                            children.AddRange(GetAllChildControls(child));
                        }
                    }
                }
            }

            return children;
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

        // Matching PyKotor implementation: self.ui.standardEquipmentTab and self.ui.naturalEquipmentTab
        // Original: QWidget standardEquipmentTab, naturalEquipmentTab (from inventory.ui)
        // These tabs contain DropFrame widgets for each equipment slot
        public Control StandardEquipmentTab { get; set; }
        public Control NaturalEquipmentTab { get; set; }
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

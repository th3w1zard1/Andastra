using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics.DLG;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using HolocronToolset.Editors.Actions;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:88
    // Original: class DLGEditor(Editor):
    public class DLGEditor : Editor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:116
        // Original: self.core_dlg: DLG = DLG()
        private DLG _coreDlg;
        private DLGModel _model;
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:117
        // Original: self.undo_stack: QUndoStack = QUndoStack()
        private DLGActionHistory _actionHistory;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:152
        // Original: self.keys_down: set[int] = set()
        private HashSet<Key> _keysDown = new HashSet<Key>();

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:113
        // Original: self._copy: DLGLink | None = None
        private DLGLink _copy;

        // UI Controls - Animations
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui:966-992
        // Original: QListWidget animsList, QPushButton addAnimButton, removeAnimButton, editAnimButton
        private ListBox _animsList;
        private Button _addAnimButton;
        private Button _removeAnimButton;
        private Button _editAnimButton;

        // Search functionality
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:122-124, 451-465
        // Original: self.search_results: list[DLGStandardItem] = [], self.current_search_text: str = "", self.current_result_index: int = 0
        private List<DLGLink> _searchResults = new List<DLGLink>();
        private string _currentSearchText = "";
        private int _currentResultIndex = 0;

        // Search UI Controls
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:451-465
        // Original: self.find_bar: QWidget, self.find_input: QLineEdit, self.find_button: QPushButton, self.back_button: QPushButton, self.results_label: QLabel
        private Panel _findBar;
        private TextBox _findInput;
        private Button _findButton;
        private Button _backButton;
        private TextBlock _resultsLabel;

        // UI Controls - Link widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QComboBox condition1ResrefEdit, condition2ResrefEdit, QSpinBox logicSpin, QTreeView dialogTree
        private ComboBox _condition1ResrefEdit;
        private ComboBox _condition2ResrefEdit;
        private NumericUpDown _logicSpin;
        private TreeView _dialogTree;
        
        // UI Controls - Node widgets (Quest/Plot)
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QLineEdit questEdit, QSpinBox questEntrySpin, QComboBox plotIndexCombo, QDoubleSpinBox plotXpSpin
        private TextBox _questEdit;
        private NumericUpDown _questEntrySpin;
        
        // UI Controls - Script widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QSpinBox script1Param1Spin, script1Param2Spin, etc.
        private NumericUpDown _script1Param1Spin;
        
        // Flag to track if node is loaded into UI (prevents updates during loading)
        private bool _nodeLoadedIntoUi = false;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:101-177
        // Original: def __init__(self, parent: QWidget | None = None, installation: HTInstallation | None = None):
        public DLGEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Dialog Editor", "dialog",
                new[] { ResourceType.DLG },
                new[] { ResourceType.DLG },
                installation)
        {
            _coreDlg = new DLG();
            _model = new DLGModel(this);
            _actionHistory = new DLGActionHistory(this);
            InitializeComponent();
            SetupUI();
            UpdateTreeView();
            New();
        }

        private void InitializeComponent()
        {
            if (!TryLoadXaml())
            {
                SetupUI();
            }
        }

        private void SetupUI()
        {
            var panel = new StackPanel();
            Content = panel;

            // Initialize dialog tree view
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            _dialogTree = new TreeView();
            _dialogTree.SelectionChanged += (s, e) => OnSelectionChanged();
            panel.Children.Add(_dialogTree);

            // Initialize link condition widgets
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            _condition1ResrefEdit = new ComboBox { IsEditable = true };
            _condition1ResrefEdit.LostFocus += (s, e) => OnNodeUpdate();
            _condition2ResrefEdit = new ComboBox { IsEditable = true };
            _condition2ResrefEdit.LostFocus += (s, e) => OnNodeUpdate();
            _logicSpin = new NumericUpDown { Minimum = 0, Maximum = 1, Value = 0 };
            _logicSpin.ValueChanged += (s, e) => OnNodeUpdate();

            var linkPanel = new StackPanel();
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 ResRef:" });
            linkPanel.Children.Add(_condition1ResrefEdit);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 ResRef:" });
            linkPanel.Children.Add(_condition2ResrefEdit);
            linkPanel.Children.Add(new TextBlock { Text = "Logic:" });
            linkPanel.Children.Add(_logicSpin);
            panel.Children.Add(linkPanel);

            // Initialize script parameter widgets
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QSpinBox script1Param1Spin
            _script1Param1Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _script1Param1Spin.ValueChanged += (s, e) => OnNodeUpdate();

            var scriptPanel = new StackPanel();
            scriptPanel.Children.Add(new TextBlock { Text = "Script1 Param1:" });
            scriptPanel.Children.Add(_script1Param1Spin);
            panel.Children.Add(scriptPanel);

            // Initialize animation UI controls
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui:966-992
            _animsList = new ListBox();
            _addAnimButton = new Button { Content = "Add Animation" };
            _removeAnimButton = new Button { Content = "Remove Animation" };
            _editAnimButton = new Button { Content = "Edit Animation" };

            // Add animation controls to UI (basic layout for now)
            var animPanel = new StackPanel();
            animPanel.Children.Add(_animsList);
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            buttonPanel.Children.Add(_addAnimButton);
            buttonPanel.Children.Add(_removeAnimButton);
            buttonPanel.Children.Add(_editAnimButton);
            animPanel.Children.Add(buttonPanel);
            panel.Children.Add(animPanel);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1135-1171
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes | bytearray):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            _coreDlg = DLGHelper.ReadDlg(data);
            LoadDLG(_coreDlg);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1193-1227
        // Original: def _load_dlg(self, dlg: DLG):
        private void LoadDLG(DLG dlg)
        {
            _coreDlg = dlg;
            _model.ResetModel();
            foreach (DLGLink start in dlg.Starters)
            {
                _model.AddStarter(start);
            }
            // Clear undo/redo history when loading a dialog
            _actionHistory.Clear();
            UpdateTreeView();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1229-1254
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            Game gameToUse = _installation?.Game ?? Game.K2;
            byte[] data = DLGHelper.BytesDlg(_coreDlg, gameToUse, ResourceType.DLG);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1256-1260
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _coreDlg = new DLG();
            _model.ResetModel();
            // Clear undo/redo history when creating new dialog
            _actionHistory.Clear();
            UpdateTreeView();
        }

        public override void SaveAs()
        {
            Save();
        }

        // Properties for tests
        public DLG CoreDlg => _coreDlg;
        public DLGModel Model => _model;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/node_editor.py:1170-1184
        // Original: def undo(self): / def redo(self):
        // Undo/redo functionality for DLG editor
        // Based on QUndoStack pattern from PyKotor implementation

        /// <summary>
        /// Gets whether undo is available.
        /// </summary>
        public bool CanUndo => _actionHistory.CanUndo;

        /// <summary>
        /// Gets whether redo is available.
        /// </summary>
        public bool CanRedo => _actionHistory.CanRedo;

        /// <summary>
        /// Undoes the last action.
        /// </summary>
        public void Undo()
        {
            _actionHistory.Undo();
        }

        /// <summary>
        /// Redoes the last undone action.
        /// </summary>
        public void Redo()
        {
            _actionHistory.Redo();
        }

        /// <summary>
        /// Adds a starter link to the dialog and records it in the action history for undo/redo.
        /// </summary>
        /// <param name="link">The link to add.</param>
        public void AddStarter(DLGLink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            int index = _coreDlg.Starters.Count;
            var action = new AddStarterAction(link, index);
            _actionHistory.Apply(action);
        }

        /// <summary>
        /// Removes a starter link from the dialog and records it in the action history for undo/redo.
        /// </summary>
        /// <param name="link">The link to remove.</param>
        public void RemoveStarter(DLGLink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            int index = _coreDlg.Starters.IndexOf(link);
            if (index < 0)
            {
                return; // Link not found, nothing to remove
            }

            var action = new RemoveStarterAction(link, index);
            _actionHistory.Apply(action);
        }

        /// <summary>
        /// Moves the selected item down in the starter list and records it in the action history for undo/redo.
        /// </summary>
        public void MoveItemDown()
        {
            int selectedIndex = _model.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _coreDlg.Starters.Count - 1)
            {
                return; // No selection or already at bottom
            }

            int newIndex = selectedIndex + 1;
            DLGLink link = _coreDlg.Starters[selectedIndex];
            var action = new MoveStarterAction(link, selectedIndex, newIndex);
            _actionHistory.Apply(action);

            // Update selected index to track the moved item
            _model.SelectedIndex = newIndex;
        }

        /// <summary>
        /// Moves the selected item up in the starter list and records it in the action history for undo/redo.
        /// </summary>
        public void MoveItemUp()
        {
            int selectedIndex = _model.SelectedIndex;
            if (selectedIndex <= 0 || selectedIndex >= _coreDlg.Starters.Count)
            {
                return; // No selection or already at top
            }

            int newIndex = selectedIndex - 1;
            DLGLink link = _coreDlg.Starters[selectedIndex];
            var action = new MoveStarterAction(link, selectedIndex, newIndex);
            _actionHistory.Apply(action);

            // Update selected index to track the moved item
            _model.SelectedIndex = newIndex;
        }

        // Matching PyKotor implementation: Expose UI controls for testing
        // Original: editor.ui.animsList, editor.ui.addAnimButton, etc.
        public ListBox AnimsList => _animsList;
        public Button AddAnimButton => _addAnimButton;
        public Button RemoveAnimButton => _removeAnimButton;
        public Button EditAnimButton => _editAnimButton;
        
        // Expose link widgets for testing
        // Matching PyKotor implementation: editor.ui.condition1ResrefEdit, etc.
        public ComboBox Condition1ResrefEdit => _condition1ResrefEdit;
        public ComboBox Condition2ResrefEdit => _condition2ResrefEdit;
        public NumericUpDown LogicSpin => _logicSpin;
        public TreeView DialogTree => _dialogTree;
        
        // Expose quest widgets for testing
        // Matching PyKotor implementation: editor.ui.questEdit, editor.ui.questEntrySpin
        public TextBox QuestEdit => _questEdit;
        public NumericUpDown QuestEntrySpin => _questEntrySpin;
        
        // Expose script widgets for testing
        // Matching PyKotor implementation: editor.ui.script1Param1Spin
        public NumericUpDown Script1Param1Spin => _script1Param1Spin;

        /// <summary>
        /// Handles selection changes in the dialog tree.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2364-2454
        /// Original: def on_selection_changed(self, selection: QItemSelection):
        /// </summary>
        private void OnSelectionChanged()
        {
            _nodeLoadedIntoUi = false;
            
            if (_dialogTree?.SelectedItem == null)
            {
                // Clear UI when nothing is selected
                if (_condition1ResrefEdit != null)
                {
                    _condition1ResrefEdit.Text = string.Empty;
                }
                if (_condition2ResrefEdit != null)
                {
                    _condition2ResrefEdit.Text = string.Empty;
                }
                if (_logicSpin != null)
                {
                    _logicSpin.Value = 0;
                }
                if (_questEdit != null)
                {
                    _questEdit.Text = string.Empty;
                }
                if (_questEntrySpin != null)
                {
                    _questEntrySpin.Value = 0;
                }
                if (_script1Param1Spin != null)
                {
                    _script1Param1Spin.Value = 0;
                }
                _nodeLoadedIntoUi = true;
                return;
            }

            // Get selected item from tree
            var selectedItem = _dialogTree.SelectedItem;
            if (selectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
            {
                LoadLinkIntoUI(dlgItem);
            }
            else if (selectedItem is DLGStandardItem dlgItemDirect)
            {
                LoadLinkIntoUI(dlgItemDirect);
            }
            
            _nodeLoadedIntoUi = true;
        }

        /// <summary>
        /// Loads link properties into UI controls.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2364-2454
        /// </summary>
        private void LoadLinkIntoUI(DLGStandardItem item)
        {
            if (item?.Link == null)
            {
                return;
            }

            var link = item.Link;
            var node = link.Node;
            
            // Load condition1
            if (_condition1ResrefEdit != null)
            {
                _condition1ResrefEdit.Text = link.Active1?.ToString() ?? string.Empty;
            }
            
            // Load condition2
            if (_condition2ResrefEdit != null)
            {
                _condition2ResrefEdit.Text = link.Active2?.ToString() ?? string.Empty;
            }
            
            // Load logic (0 = AND/false, 1 = OR/true)
            if (_logicSpin != null)
            {
                _logicSpin.Value = link.Logic ? 1 : 0;
            }
            
            // Load quest fields from node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2433-2434
            // Original: self.ui.questEdit.setText(item.link.node.quest), self.ui.questEntrySpin.setValue(item.link.node.quest_entry or 0)
            if (_questEdit != null && node != null)
            {
                _questEdit.Text = node.Quest ?? string.Empty;
            }
            
            if (_questEntrySpin != null && node != null)
            {
                _questEntrySpin.Value = node.QuestEntry ?? 0;
            }
            
            // Load script1 param1
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2410
            // Original: self.ui.script1Param1Spin.setValue(item.link.node.script1_param1)
            if (_script1Param1Spin != null && node != null)
            {
                _script1Param1Spin.Value = node.Script1Param1;
            }
        }

        /// <summary>
        /// Updates node properties based on UI selections.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2456-2491
        /// Original: def on_node_update(self, *args, **kwargs):
        /// </summary>
        public void OnNodeUpdate()
        {
            if (!_nodeLoadedIntoUi)
            {
                return;
            }

            if (_dialogTree?.SelectedItem == null)
            {
                return;
            }

            // Get selected item from tree
            DLGStandardItem item = null;
            var selectedItem = _dialogTree.SelectedItem;
            if (selectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
            {
                item = dlgItem;
            }
            else if (selectedItem is DLGStandardItem dlgItemDirect)
            {
                item = dlgItemDirect;
            }

            if (item?.Link == null)
            {
                return;
            }

            var link = item.Link;
            var node = link.Node;

            // Update condition1
            if (_condition1ResrefEdit != null)
            {
                string text = _condition1ResrefEdit.Text ?? string.Empty;
                link.Active1 = string.IsNullOrEmpty(text) ? ResRef.FromBlank() : new ResRef(text);
            }

            // Update condition2
            if (_condition2ResrefEdit != null)
            {
                string text = _condition2ResrefEdit.Text ?? string.Empty;
                link.Active2 = string.IsNullOrEmpty(text) ? ResRef.FromBlank() : new ResRef(text);
            }

            // Update logic (0 = AND/false, 1 = OR/true)
            if (_logicSpin != null)
            {
                link.Logic = _logicSpin.Value.HasValue && _logicSpin.Value.Value != 0;
            }
            
            // Update quest fields in node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2521-2522
            // Original: item.link.node.quest = self.ui.questEdit.text(), item.link.node.quest_entry = self.ui.questEntrySpin.value()
            if (_questEdit != null && node != null)
            {
                node.Quest = _questEdit.Text ?? string.Empty;
            }
            
            if (_questEntrySpin != null && node != null)
            {
                node.QuestEntry = _questEntrySpin.Value.HasValue ? _questEntrySpin.Value.Value : 0;
            }
            
            // Update script1 param1
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2499
            // Original: item.link.node.script1_param1 = self.ui.script1Param1Spin.value()
            if (_script1Param1Spin != null && node != null)
            {
                node.Script1Param1 = _script1Param1Spin.Value.HasValue ? (int)_script1Param1Spin.Value.Value : 0;
            }
        }

        /// <summary>
        /// Updates the tree view with the current model data.
        /// </summary>
        public void UpdateTreeView()
        {
            if (_dialogTree == null || _model == null)
            {
                return;
            }

            var treeItems = new List<TreeViewItem>();
            foreach (var rootItem in _model.GetRootItems())
            {
                var treeItem = CreateTreeViewItem(rootItem);
                treeItems.Add(treeItem);
            }
            _dialogTree.ItemsSource = treeItems;
        }

        /// <summary>
        /// Creates a TreeViewItem from a DLGStandardItem, recursively creating children.
        /// </summary>
        private TreeViewItem CreateTreeViewItem(DLGStandardItem item)
        {
            var treeItem = new TreeViewItem
            {
                Header = GetItemDisplayText(item),
                Tag = item,
                IsExpanded = true
            };

            var childItems = new List<TreeViewItem>();
            foreach (var child in item.Children)
            {
                childItems.Add(CreateTreeViewItem(child));
            }
            treeItem.ItemsSource = childItems;

            return treeItem;
        }

        /// <summary>
        /// Gets the display text for an item.
        /// </summary>
        private string GetItemDisplayText(DLGStandardItem item)
        {
            if (item?.Link?.Node == null)
            {
                return "Unknown";
            }

            var node = item.Link.Node;
            string nodeType = node is DLGEntry ? "Entry" : "Reply";
            string text = node.Text?.GetString(0) ?? "";
            if (string.IsNullOrEmpty(text))
            {
                text = "<empty>";
            }
            return $"{nodeType}: {text}";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2021-2138
        // Original: def keyPressEvent(self, event: QKeyEvent, *, is_tree_view_call: bool = False):
        /// <summary>
        /// Handles key press events for the DLG editor.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2021-2138
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            Key key = e.Key;
            bool isAutoRepeat = e.IsRepeat;

            // Matching PyKotor implementation: if not is_tree_view_call: check focus
            // For now, we handle all key events at the window level
            // In a full implementation, we would check if dialogTree has focus

            // Matching PyKotor implementation: if not selected_index.isValid(): return
            // For now, we'll handle keys even without selection (for Insert key to add root node)

            // Matching PyKotor implementation: if selected_item is None: handle Insert key
            if (_model.SelectedIndex < 0)
            {
                if (key == Key.Insert)
                {
                    // Matching PyKotor implementation: self.model.add_root_node()
                    AddRootNode();
                    e.Handled = true;
                }
                return;
            }

            // Matching PyKotor implementation: if event.isAutoRepeat() or key in self.keys_down:
            if (isAutoRepeat || _keysDown.Contains(key))
            {
                // Matching PyKotor implementation: handle arrow keys even on auto-repeat
                if (key == Key.Up || key == Key.Down)
                {
                    _keysDown.Add(key);
                    HandleShiftItemKeybind(key);
                }
                e.Handled = true;
                return; // Ignore auto-repeat events and prevent multiple executions on single key
            }

            // Matching PyKotor implementation: if not self.keys_down:
            if (_keysDown.Count == 0)
            {
                _keysDown.Add(key);

                // Matching PyKotor implementation: if key in (Qt.Key.Key_Delete, Qt.Key.Key_Backspace):
                if (key == Key.Delete || key == Key.Back)
                {
                    // Matching PyKotor implementation: self.model.remove_link(selected_item)
                    RemoveSelectedLink();
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: elif key in (Qt.Key.Key_Enter, Qt.Key.Key_Return):
                else if (key == Key.Enter || key == Key.Return)
                {
                    // Matching PyKotor implementation: edit_text based on focus
                    // For now, we'll just handle the basic case
                    EditText();
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: elif key == Qt.Key.Key_F:
                else if (key == Key.F)
                {
                    // Matching PyKotor implementation: self.focus_on_node(selected_item.link)
                    FocusOnSelectedNode();
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: elif key == Qt.Key.Key_Insert:
                else if (key == Key.Insert)
                {
                    // Matching PyKotor implementation: self.model.add_child_to_item(selected_item)
                    AddChildToSelectedItem();
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: elif key == Qt.Key.Key_P:
                else if (key == Key.P)
                {
                    // Matching PyKotor implementation: play sound or blink window
                    PlaySoundOrBlink();
                    e.Handled = true;
                    return;
                }
                return;
            }

            // Matching PyKotor implementation: self.keys_down.add(key)
            _keysDown.Add(key);

            // Matching PyKotor implementation: self._handle_shift_item_keybind(...)
            HandleShiftItemKeybind(key);

            // Matching PyKotor implementation: handle modifier key combinations
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Matching PyKotor implementation: Ctrl+G (show_go_to_bar) - commented out in Python
                if (key == Key.G)
                {
                    // Matching PyKotor implementation: self.show_go_to_bar() - commented out
                    // ShowGoToBar();
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: Ctrl+F
                else if (key == Key.F)
                {
                    // Matching PyKotor implementation: self.show_find_bar()
                    ShowFindBar();
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: Ctrl+C
                else if (key == Key.C)
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                    {
                        // Matching PyKotor implementation: self.copy_path(selected_item.link.node)
                        CopyPath();
                    }
                    else
                    {
                        // Matching PyKotor implementation: self.model.copy_link_and_node(selected_item.link)
                        CopyLinkAndNode();
                    }
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: Ctrl+Enter or Ctrl+Return
                else if (key == Key.Enter || key == Key.Return)
                {
                    // Matching PyKotor implementation: self.jump_to_original(selected_item)
                    JumpToOriginal();
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: Ctrl+V
                else if (key == Key.V)
                {
                    // Matching PyKotor implementation: paste logic
                    PasteItem(e.KeyModifiers.HasFlag(KeyModifiers.Alt));
                    e.Handled = true;
                    return;
                }
                // Matching PyKotor implementation: Ctrl+Delete
                else if (key == Key.Delete)
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        // Matching PyKotor implementation: self.model.delete_node_everywhere(selected_item.link.node)
                        DeleteNodeEverywhere();
                    }
                    else
                    {
                        // Matching PyKotor implementation: self.model.delete_selected_node()
                        DeleteSelectedNode();
                    }
                    e.Handled = true;
                    return;
                }
            }

            // Matching PyKotor implementation: Shift+Enter/Return combinations
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (key == Key.Enter || key == Key.Return))
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    // Matching PyKotor implementation: set_expand_recursively(..., expand=False, maxdepth=-1)
                    SetExpandRecursively(false, -1);
                }
                else
                {
                    // Matching PyKotor implementation: set_expand_recursively(..., expand=True)
                    SetExpandRecursively(true, 0);
                }
                e.Handled = true;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2139-2147
        // Original: def keyReleaseEvent(self, event: QKeyEvent):
        /// <summary>
        /// Handles key release events for the DLG editor.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2139-2147
        /// </summary>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            Key key = e.Key;

            // Matching PyKotor implementation: if key in self.keys_down: self.keys_down.remove(key)
            if (_keysDown.Contains(key))
            {
                _keysDown.Remove(key);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1985-2019
        // Original: def _handle_shift_item_keybind(self, selected_index, selected_item, key):
        /// <summary>
        /// Handles shift+arrow key combinations for moving items.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1985-2019
        /// </summary>
        private void HandleShiftItemKeybind(Key key)
        {
            if (_model.SelectedIndex < 0)
            {
                return;
            }

            // Matching PyKotor implementation: handle Shift+Up/Down combinations
            if (_keysDown.Contains(Key.Shift))
            {
                if (key == Key.Up)
                {
                    // Matching PyKotor implementation: shift_item(selected_item, -1, no_selection_update=True)
                    if (_model.SelectedIndex > 0)
                    {
                        _model.MoveStarter(_model.SelectedIndex, _model.SelectedIndex - 1);
                        _model.SelectedIndex = _model.SelectedIndex - 1;
                    }
                }
                else if (key == Key.Down)
                {
                    // Matching PyKotor implementation: shift_item(selected_item, 1, no_selection_update=True)
                    if (_model.SelectedIndex < _model.RowCount - 1)
                    {
                        _model.MoveStarter(_model.SelectedIndex, _model.SelectedIndex + 1);
                        _model.SelectedIndex = _model.SelectedIndex + 1;
                    }
                }
            }
        }

        // Helper methods for key press actions - these will be fully implemented as the UI is completed
        // Matching PyKotor implementation patterns

        /// <summary>
        /// Adds a root node to the dialog.
        /// Matching PyKotor implementation: self.model.add_root_node()
        /// </summary>
        private void AddRootNode()
        {
            // TODO: PLACEHOLDER - Implement add_root_node when DLGModel is fully implemented
            // This would create a new DLGNode and add it as a starter
        }

        /// <summary>
        /// Removes the selected link.
        /// Matching PyKotor implementation: self.model.remove_link(selected_item)
        /// </summary>
        private void RemoveSelectedLink()
        {
            if (_model.SelectedIndex >= 0 && _model.SelectedIndex < _model.RowCount)
            {
                DLGLink link = _model.GetStarterAt(_model.SelectedIndex);
                if (link != null)
                {
                    RemoveStarter(link);
                }
            }
        }

        /// <summary>
        /// Edits text of the selected item.
        /// Matching PyKotor implementation: self.edit_text(event, selectedIndexes(), view)
        /// </summary>
        private void EditText()
        {
            // TODO: PLACEHOLDER - Implement edit_text when UI text editing is implemented
        }

        /// <summary>
        /// Focuses on the selected node.
        /// Matching PyKotor implementation: self.focus_on_node(selected_item.link)
        /// </summary>
        private void FocusOnSelectedNode()
        {
            // TODO: PLACEHOLDER - Implement focus_on_node when UI tree view is implemented
        }

        /// <summary>
        /// Adds a child to the selected item.
        /// Matching PyKotor implementation: self.model.add_child_to_item(selected_item)
        /// </summary>
        private void AddChildToSelectedItem()
        {
            // TODO: PLACEHOLDER - Implement add_child_to_item when DLGModel tree structure is implemented
        }

        /// <summary>
        /// Plays sound or blinks window.
        /// Matching PyKotor implementation: self.play_sound(...) or self.blink_window()
        /// </summary>
        private void PlaySoundOrBlink()
        {
            // TODO: PLACEHOLDER - Implement play_sound and blink_window when audio system is implemented
        }

        /// <summary>
        /// Shows the find bar.
        /// Matching PyKotor implementation: self.show_find_bar()
        /// </summary>
        private void ShowFindBar()
        {
            // TODO: PLACEHOLDER - Implement show_find_bar when find UI is implemented
        }

        /// <summary>
        /// Copies the path of the selected node.
        /// Matching PyKotor implementation: self.copy_path(selected_item.link.node)
        /// </summary>
        private void CopyPath()
        {
            // TODO: PLACEHOLDER - Implement copy_path when clipboard system is implemented
        }

        /// <summary>
        /// Copies the link and node.
        /// Matching PyKotor implementation: self.model.copy_link_and_node(selected_item.link)
        /// </summary>
        private void CopyLinkAndNode()
        {
            if (_model.SelectedIndex >= 0 && _model.SelectedIndex < _model.RowCount)
            {
                DLGLink link = _model.GetStarterAt(_model.SelectedIndex);
                if (link != null)
                {
                    // Matching PyKotor implementation: self._copy = link
                    _copy = link;
                }
            }
        }

        /// <summary>
        /// Jumps to the original node.
        /// Matching PyKotor implementation: self.jump_to_original(selected_item)
        /// </summary>
        private void JumpToOriginal()
        {
            // TODO: PLACEHOLDER - Implement jump_to_original when reference system is implemented
        }

        /// <summary>
        /// Pastes an item.
        /// Matching PyKotor implementation: self.model.paste_item(selected_item, as_new_branches=...)
        /// </summary>
        private void PasteItem(bool asNewBranches)
        {
            if (_copy == null)
            {
                // Matching PyKotor implementation: print("No node/link copy in memory or on clipboard.")
                // TODO: PLACEHOLDER - Show message to user
                return;
            }

            if (_model.SelectedIndex >= 0 && _model.SelectedIndex < _model.RowCount)
            {
                DLGLink selectedLink = _model.GetStarterAt(_model.SelectedIndex);
                if (selectedLink != null)
                {
                    // Matching PyKotor implementation: check if node types match
                    // For now, we'll allow paste (full implementation would check node types)
                    // TODO: PLACEHOLDER - Implement full paste logic when DLGModel.paste_item is implemented
                }
            }
        }

        /// <summary>
        /// Deletes the selected node.
        /// Matching PyKotor implementation: self.model.delete_selected_node()
        /// </summary>
        private void DeleteSelectedNode()
        {
            if (_model.SelectedIndex >= 0 && _model.SelectedIndex < _model.RowCount)
            {
                DLGLink link = _model.GetStarterAt(_model.SelectedIndex);
                if (link != null)
                {
                    RemoveStarter(link);
                }
            }
        }

        /// <summary>
        /// Deletes the node everywhere.
        /// Matching PyKotor implementation: self.model.delete_node_everywhere(selected_item.link.node)
        /// </summary>
        private void DeleteNodeEverywhere()
        {
            // TODO: PLACEHOLDER - Implement delete_node_everywhere when DLGModel tree structure is implemented
        }

        /// <summary>
        /// Sets expand recursively for tree items.
        /// Matching PyKotor implementation: self.set_expand_recursively(selected_item, set(), expand=..., maxdepth=...)
        /// </summary>
        private void SetExpandRecursively(bool expand, int maxDepth)
        {
            // TODO: PLACEHOLDER - Implement set_expand_recursively when tree view UI is implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:152
        // Original: self.keys_down: set[int] = set()
        // Expose for testing
        public HashSet<Key> KeysDown => _keysDown;
    }

    /// <summary>
    /// Represents a standard item in the DLG tree model.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:52-100
    /// Original: class DLGStandardItem(QStandardItem):
    /// </summary>
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
    }

    // Simple model class for tests (matching Python DLGStandardItemModel)
    public class DLGModel
    {
        private List<DLGStandardItem> _rootItems = new List<DLGStandardItem>();
        private DLGEditor _editor;

        public DLGModel()
        {
        }

        public DLGModel(DLGEditor editor)
        {
            _editor = editor;
        }

        public int RowCount => _rootItems.Count;

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= -1 && value < _rootItems.Count)
                {
                    _selectedIndex = value;
                }
            }
        }

        public void ResetModel()
        {
            _rootItems.Clear();
            _selectedIndex = -1;
        }

        public void AddStarter(DLGLink link)
        {
            if (link == null)
            {
                return;
            }
            var item = new DLGStandardItem(link);
            _rootItems.Add(item);
            
            // Also add to CoreDlg.Starters if editor is available
            if (_editor != null && _editor.CoreDlg != null)
            {
                if (!_editor.CoreDlg.Starters.Contains(link))
                {
                    _editor.CoreDlg.Starters.Add(link);
                }
            }
        }

        /// <summary>
        /// Adds a root node to the dialog graph.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:846-856
        /// Original: def add_root_node(self):
        /// </summary>
        public DLGStandardItem AddRootNode()
        {
            var newEntry = new DLGEntry();
            newEntry.PlotIndex = -1;
            var newLink = new DLGLink(newEntry);
            newLink.Node.ListIndex = GetNewNodeListIndex(newLink.Node);
            
            var newItem = new DLGStandardItem(newLink);
            _rootItems.Add(newItem);
            
            // Add to CoreDlg.Starters
            if (_editor != null && _editor.CoreDlg != null)
            {
                _editor.CoreDlg.Starters.Add(newLink);
            }
            
            UpdateItemDisplayText(newItem);
            
            // Update tree view if editor is available
            if (_editor != null)
            {
                _editor.UpdateTreeView();
            }
            
            return newItem;
        }

        /// <summary>
        /// Adds a child node to the specified parent item.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:858-877
        /// Original: def add_child_to_item(self, parent_item: DLGStandardItem, link: DLGLink | None = None) -> DLGStandardItem:
        /// </summary>
        public DLGStandardItem AddChildToItem(DLGStandardItem parentItem, DLGLink link = null)
        {
            if (parentItem == null)
            {
                throw new ArgumentNullException(nameof(parentItem));
            }
            
            if (parentItem.Link == null)
            {
                throw new InvalidOperationException("Parent item must have a valid link");
            }

            if (link == null)
            {
                // Create new node - if parent is Reply, create Entry; if parent is Entry, create Reply
                DLGNode newNode;
                if (parentItem.Link.Node is DLGReply)
                {
                    newNode = new DLGEntry();
                }
                else
                {
                    newNode = new DLGReply();
                }
                newNode.PlotIndex = -1;
                newNode.ListIndex = GetNewNodeListIndex(newNode);
                link = new DLGLink(newNode);
            }

            // Link the nodes
            if (parentItem.Link.Node != null)
            {
                link.ListIndex = parentItem.Link.Node.Links.Count;
                parentItem.Link.Node.Links.Add(link);
            }

            var newItem = new DLGStandardItem(link);
            parentItem.AddChild(newItem);
            
            UpdateItemDisplayText(newItem);
            UpdateItemDisplayText(parentItem);
            
            return newItem;
        }

        /// <summary>
        /// Gets the item at the specified row and column.
        /// Matching PyKotor implementation: def item(self, row: int, column: int = 0) -> DLGStandardItem | None:
        /// </summary>
        public DLGStandardItem Item(int row, int column = 0)
        {
            if (row < 0 || row >= _rootItems.Count || column != 0)
            {
                return null;
            }
            return _rootItems[row];
        }

        /// <summary>
        /// Gets a new list index for a node.
        /// </summary>
        private int GetNewNodeListIndex(DLGNode node)
        {
            if (_editor?.CoreDlg == null)
            {
                return 0;
            }

            if (node is DLGEntry)
            {
                int maxIndex = -1;
                foreach (var entry in _editor.CoreDlg.AllEntries())
                {
                    if (entry.ListIndex > maxIndex)
                    {
                        maxIndex = entry.ListIndex;
                    }
                }
                return maxIndex + 1;
            }
            else if (node is DLGReply)
            {
                int maxIndex = -1;
                foreach (var reply in _editor.CoreDlg.AllReplies())
                {
                    if (reply.ListIndex > maxIndex)
                    {
                        maxIndex = reply.ListIndex;
                    }
                }
                return maxIndex + 1;
            }
            return 0;
        }

        /// <summary>
        /// Updates the display text for an item.
        /// </summary>
        private void UpdateItemDisplayText(DLGStandardItem item)
        {
            // This would update the display text in the tree view
            // For now, it's a placeholder
        }

        /// <summary>
        /// Inserts a starter link at the specified index.
        /// </summary>
        public void InsertStarter(int index, DLGLink link)
        {
            if (link == null)
            {
                return;
            }
            var item = new DLGStandardItem(link);
            if (index < 0 || index > _rootItems.Count)
            {
                _rootItems.Add(item);
            }
            else
            {
                _rootItems.Insert(index, item);
            }
        }

        /// <summary>
        /// Gets the starter link at the specified index.
        /// </summary>
        public DLGLink GetStarterAt(int index)
        {
            if (index < 0 || index >= _rootItems.Count)
            {
                return null;
            }
            return _rootItems[index].Link;
        }

        // Matching PyKotor implementation
        // Original: def remove_starter(self, link: DLGLink): ...
        /// <summary>
        /// Removes a starter link from the model.
        /// </summary>
        public void RemoveStarter(DLGLink link)
        {
            for (int i = _rootItems.Count - 1; i >= 0; i--)
            {
                if (_rootItems[i].Link == link)
                {
                    _rootItems.RemoveAt(i);
                    break;
                }
            }
        }

        // Matching PyKotor implementation
        // Original: def move_starter(self, old_index: int, new_index: int): ...
        /// <summary>
        /// Moves a starter link from one index to another.
        /// </summary>
        public void MoveStarter(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= _rootItems.Count || newIndex < 0 || newIndex >= _rootItems.Count)
            {
                return;
            }

            var item = _rootItems[oldIndex];
            _rootItems.RemoveAt(oldIndex);
            _rootItems.Insert(newIndex, item);
        }

        /// <summary>
        /// Gets all root items in the model.
        /// </summary>
        public IReadOnlyList<DLGStandardItem> GetRootItems()
        {
            return _rootItems;
        }
    }
}

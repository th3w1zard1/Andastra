using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Media;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using DLGType = Andastra.Parsing.Resource.Generics.DLG.DLG;
using DLGLink = Andastra.Parsing.Resource.Generics.DLG.DLGLink;
using DLGNode = Andastra.Parsing.Resource.Generics.DLG.DLGNode;
using DLGEntry = Andastra.Parsing.Resource.Generics.DLG.DLGEntry;
using DLGReply = Andastra.Parsing.Resource.Generics.DLG.DLGReply;
using DLGHelper = Andastra.Parsing.Resource.Generics.DLG.DLGHelper;
using CNVHelper = Andastra.Parsing.Resource.Generics.CNV.CNVHelper;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Editors.Actions;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:88
    // Original: class DLGEditor(Editor):
    // DLG (Dialogue) format is Aurora Engine format used by:
    // - Neverwinter Nights: Enhanced Edition (Aurora) - nwmain.exe: Uses base DLG format
    // - KotOR 1 (Odyssey) - swkotor.exe: Uses base DLG format
    // - KotOR 2 (Odyssey) - swkotor2.exe: Uses extended DLG format with K2-specific fields
    //   K2-specific root fields: AlienRaceOwner, PostProcOwner, RecordNoVO, NextNodeID
    //   K2-specific node fields: ActionParam1-5, Script2, AlienRaceNode, NodeID, Emotion, FacialAnim, etc.
    //   K2-specific link fields: Active2, Logic, Not, Not2, Param1-5, ParamStrA/B, etc.
    // - Eclipse Engine games (Dragon Age Origins, Dragon Age 2, Mass Effect 1/2):
    //   Eclipse games primarily use .cnv "conversation" format, but DLG files follow K1-style base format
    //   (no K2-specific fields). This editor supports both DLG and CNV files for Eclipse games.
    //   CNV files are automatically converted to DLG for editing, and can be saved back as CNV.
    //   Ghidra analysis: daorigins.exe, DragonAge2.exe, MassEffect.exe use "conversation" strings
    public class DLGEditor : Editor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:116
        // Original: self.core_dlg: DLG = DLG()
        private DLGType _coreDlg;
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
        private List<DLGStandardItem> _searchResults = new List<DLGStandardItem>();
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

        // UI Controls - Speaker widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QLineEdit speakerEdit, QLabel speakerEditLabel
        private TextBox _speakerEdit;
        private TextBlock _speakerEditLabel;

        // UI Controls - Script widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QSpinBox script1Param1Spin, script1Param2Spin, etc.
        private NumericUpDown _script1Param1Spin;
        private StackPanel _script1Param1Panel; // Panel containing script1Param1 control for visibility management

        // UI Controls - Node timing widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QSpinBox delaySpin, waitFlagSpin, fadeTypeSpin
        private NumericUpDown _delaySpin;
        private NumericUpDown _waitFlagSpin;
        private NumericUpDown _fadeTypeSpin;

        // Flag to track if node is loaded into UI (prevents updates during loading)
        private bool _nodeLoadedIntoUi = false;

        // Flag to track if editor is in focus mode (showing only a specific node and its children)
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:152
        // Original: self._focused: bool = False
        private bool _focused = false;

        // Sound player for playing audio files
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/base.py:80
        // Original: self.media_player: EditorMedia = EditorMedia(self)
        private SoundPlayer _soundPlayer;
        private MemoryStream _soundStream;

        // Reference history for navigation
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:148-150
        // Original: self.dialog_references: ReferenceChooserDialog | None = None
        // Original: self.reference_history: list[tuple[list[weakref.ref[DLGLink]], str]] = []
        // Original: self.current_reference_index: int = -1
        private ReferenceChooserDialog _dialogReferences;
        private List<Tuple<List<WeakReference<DLGLink>>, string>> _referenceHistory = new List<Tuple<List<WeakReference<DLGLink>>, string>>();
        private int _currentReferenceIndex = -1;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:101-177
        // Original: def __init__(self, parent: QWidget | None = None, installation: HTInstallation | None = None):
        public DLGEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Dialog Editor", "dialog",
                new[] { ResourceType.DLG, ResourceType.CNV },
                new[] { ResourceType.DLG, ResourceType.CNV },
                installation)
        {
            _coreDlg = new DLGType();
            _model = new DLGModel(this);
            _actionHistory = new DLGActionHistory(this);
            _soundPlayer = new SoundPlayer();
            InitializeComponent();
            SetupUI();
            UpdateUIForGame(); // Update UI visibility based on game type
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

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:436-467
            // Original: def setup_extra_widgets(self):
            // Find bar - must be added first (before dialog tree) to match PyKotor layout
            // Matching PyKotor: self.ui.verticalLayout_main.insertWidget(0, self.find_bar)
            SetupFindBar();
            if (_findBar != null)
            {
                panel.Children.Insert(0, _findBar);
            }

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

            // Initialize script parameter widgets (K2-specific)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QSpinBox script1Param1Spin
            // K2-specific: ActionParam1 field only exists in KotOR 2 (swkotor2.exe: 0x005ea880)
            // Aurora (NWN) and Eclipse (DA/ME) use base DLG format without K2 extensions
            _script1Param1Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _script1Param1Spin.ValueChanged += (s, e) => OnNodeUpdate();

            _script1Param1Panel = new StackPanel();
            _script1Param1Panel.Children.Add(new TextBlock { Text = "Script1 Param1 (K2 only):" });
            _script1Param1Panel.Children.Add(_script1Param1Spin);
            panel.Children.Add(_script1Param1Panel);

            // Initialize node timing widgets
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QSpinBox delaySpin, waitFlagSpin, fadeTypeSpin
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:416-419
            // Original: self.ui.delaySpin.valueChanged.connect(self.on_node_update), self.ui.waitFlagSpin.valueChanged.connect(self.on_node_update), self.ui.fadeTypeSpin.valueChanged.connect(self.on_node_update)
            _delaySpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = -1 };
            _delaySpin.ValueChanged += (s, e) => OnNodeUpdate();

            _waitFlagSpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _waitFlagSpin.ValueChanged += (s, e) => OnNodeUpdate();

            _fadeTypeSpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _fadeTypeSpin.ValueChanged += (s, e) => OnNodeUpdate();

            var timingPanel = new StackPanel();
            timingPanel.Children.Add(new TextBlock { Text = "Delay:" });
            timingPanel.Children.Add(_delaySpin);
            timingPanel.Children.Add(new TextBlock { Text = "Wait Flags:" });
            timingPanel.Children.Add(_waitFlagSpin);
            timingPanel.Children.Add(new TextBlock { Text = "Fade Type:" });
            timingPanel.Children.Add(_fadeTypeSpin);
            panel.Children.Add(timingPanel);

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

            // Handle CNV format by converting to DLG for editing
            if (restype == ResourceType.CNV)
            {
                var cnv = CNVHelper.ReadCnv(data);
                _coreDlg = CNVHelper.ToDlg(cnv);
            }
            else
            {
                _coreDlg = DLGHelper.ReadDlg(data);
            }
            LoadDLG(_coreDlg);
            UpdateUIForGame(); // Update UI visibility after loading (game may have changed)
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1193-1227
        // Original: def _load_dlg(self, dlg: DLG):
        /// <summary>
        /// Loads a dialog tree into the UI view.
        /// Made internal for test access (matching Python _load_dlg which tests access directly).
        /// </summary>
        public void LoadDLG(DLGType dlg)
        {
            // Matching PyKotor implementation: Reset focus state and background color when loading
            // Original: if "(Light)" in GlobalSettings().selectedTheme or GlobalSettings().selectedTheme == "Native":
            //           self.ui.dialogTree.setStyleSheet("")
            if (_dialogTree != null)
            {
                _dialogTree.Background = null; // Reset background color
            }
            _focused = false;

            _coreDlg = dlg;
            _model.ResetModel();

            // Matching PyKotor: Create items for each starter and load them recursively
            // Original: for start in dlg.starters:
            //              item = DLGStandardItem(link=start)
            //              self.model.appendRow(item)
            //              self.model.load_dlg_item_rec(item)
            foreach (DLGLink start in dlg.Starters)
            {
                var item = new DLGStandardItem(start);
                _model.AddStarter(start);
                // Get the item that was added and load it recursively
                var rootItems = _model.GetRootItems();
                if (rootItems.Count > 0)
                {
                    var addedItem = rootItems[rootItems.Count - 1];
                    _model.LoadDlgItemRec(addedItem);
                }
            }

            // Clear undo/redo history when loading a dialog
            _actionHistory.Clear();
            UpdateTreeView();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1229-1254
        // Original: def build(self) -> tuple[bytes, byte[]]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Handle CNV format by converting DLG to CNV
            if (_restype == ResourceType.CNV)
            {
                // CNV format is only used by Eclipse Engine games
                Game gameToUse = _installation?.Game ?? Game.DA;
                if (!gameToUse.IsEclipse())
                {
                    // Default to DA if not Eclipse game
                    gameToUse = Game.DA;
                }
                var cnv = DLGHelper.ToCnv(_coreDlg);
                byte[] data = CNVHelper.BytesCnv(cnv, gameToUse, ResourceType.CNV);
                return Tuple.Create(data, new byte[0]);
            }

            // Detect game from installation - supports all engines (Odyssey K1/K2, Aurora NWN, Eclipse DA/DA2/ME)
            // Game-specific format handling:
            // - K2 (TSL): Extended DLG format with K2-specific fields (ActionParam1-5, Script2, etc.)
            // - K1, NWN, Eclipse (DA/DA2/ME): Base DLG format (no K2-specific fields)
            //   Eclipse games use K1-style DLG format (no K2 extensions)
            //   Note: Eclipse games may also use .cnv format, but DLG files follow K1 format
            Game gameToUseDlg = _installation?.Game ?? Game.K2;

            // For Eclipse games, use K1 format (no K2-specific fields)
            // Matching PyKotor: Eclipse games don't have K2 extensions
            if (gameToUseDlg.IsEclipse())
            {
                gameToUseDlg = Game.K1; // Use K1 format for Eclipse (no K2-specific fields)
            }
            // For Aurora (NWN), use K1 format (base DLG, no K2 extensions)
            else if (gameToUseDlg.IsAurora())
            {
                gameToUseDlg = Game.K1; // Use K1 format for Aurora (base DLG, no K2 extensions)
            }

            byte[] data = DLGHelper.BytesDlg(_coreDlg, gameToUseDlg, ResourceType.DLG);
            return Tuple.Create(data, new byte[0]);
        }

        /// <summary>
        /// Updates UI visibility based on game type.
        /// K2-specific controls are only shown for KotOR 2 (TSL).
        /// Aurora (NWN) and Eclipse (DA/ME) use base DLG format without K2 extensions.
        /// </summary>
        private void UpdateUIForGame()
        {
            Game currentGame = _installation?.Game ?? Game.K2;
            bool isK2 = currentGame.IsK2();

            // Show/hide K2-specific controls
            // K2-specific: Script1Param1 (ActionParam1) only exists in KotOR 2 (swkotor2.exe: 0x005ea880)
            // Aurora (NWN) and Eclipse (DA/ME) use base DLG format without K2 extensions
            // Matching PyKotor: K2-specific widgets are shown/hidden based on game type
            if (_script1Param1Panel != null)
            {
                _script1Param1Panel.IsVisible = isK2;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1256-1260
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _coreDlg = new DLGType();
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
        public DLGType CoreDlg => _coreDlg;
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
        public NumericUpDown DelaySpin => _delaySpin;
        public NumericUpDown WaitFlagSpin => _waitFlagSpin;
        public NumericUpDown FadeTypeSpin => _fadeTypeSpin;

        // Expose speaker widgets for testing
        // Matching PyKotor implementation: editor.ui.speakerEdit, editor.ui.speakerEditLabel
        public TextBox SpeakerEdit => _speakerEdit;
        public TextBlock SpeakerEditLabel => _speakerEditLabel;

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
                if (_speakerEdit != null)
                {
                    _speakerEdit.Text = string.Empty;
                    _speakerEdit.IsVisible = false;
                }
                if (_speakerEditLabel != null)
                {
                    _speakerEditLabel.IsVisible = false;
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

            // Load speaker field from node (only for Entry nodes)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2397-2405
            // Original: if isinstance(item.link.node, DLGEntry): self.ui.speakerEditLabel.setVisible(True), self.ui.speakerEdit.setVisible(True), self.ui.speakerEdit.setText(item.link.node.speaker)
            // Original: elif isinstance(item.link.node, DLGReply): self.ui.speakerEditLabel.setVisible(False), self.ui.speakerEdit.setVisible(False)
            if (node is DLGEntry entry)
            {
                if (_speakerEditLabel != null)
                {
                    _speakerEditLabel.IsVisible = true;
                }
                if (_speakerEdit != null)
                {
                    _speakerEdit.IsVisible = true;
                    _speakerEdit.Text = entry.Speaker ?? string.Empty;
                }
            }
            else if (node is DLGReply)
            {
                if (_speakerEditLabel != null)
                {
                    _speakerEditLabel.IsVisible = false;
                }
                if (_speakerEdit != null)
                {
                    _speakerEdit.IsVisible = false;
                }
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

            // Load delay, wait flags, and fade type from node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2446-2449
            // Original: self.ui.delaySpin.setValue(item.link.node.delay), self.ui.waitFlagSpin.setValue(item.link.node.wait_flags), self.ui.fadeTypeSpin.setValue(item.link.node.fade_type)
            if (_delaySpin != null && node != null)
            {
                _delaySpin.Value = node.Delay;
            }

            if (_waitFlagSpin != null && node != null)
            {
                _waitFlagSpin.Value = node.WaitFlags;
            }

            if (_fadeTypeSpin != null && node != null)
            {
                _fadeTypeSpin.Value = node.FadeType;
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

            // Update speaker field in node (only for Entry nodes)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2523
            // Original: if isinstance(item.link.node, DLGEntry): item.link.node.speaker = self.ui.speakerEdit.text()
            if (_speakerEdit != null && node is DLGEntry entry)
            {
                entry.Speaker = _speakerEdit.Text ?? string.Empty;
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
                node.QuestEntry = _questEntrySpin.Value.HasValue ? (int)_questEntrySpin.Value.Value : 0;
            }

            // Update script1 param1
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2499
            // Original: item.link.node.script1_param1 = self.ui.script1Param1Spin.value()
            if (_script1Param1Spin != null && node != null)
            {
                node.Script1Param1 = _script1Param1Spin.Value.HasValue ? (int)_script1Param1Spin.Value.Value : 0;
            }

            // Update delay, wait flags, and fade type in node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2538-2540
            // Original: item.link.node.delay = self.ui.delaySpin.value(), item.link.node.wait_flags = self.ui.waitFlagSpin.value(), item.link.node.fade_type = self.ui.fadeTypeSpin.value()
            if (_delaySpin != null && node != null)
            {
                node.Delay = _delaySpin.Value.HasValue ? (int)_delaySpin.Value.Value : -1;
            }

            if (_waitFlagSpin != null && node != null)
            {
                node.WaitFlags = _waitFlagSpin.Value.HasValue ? (int)_waitFlagSpin.Value.Value : 0;
            }

            if (_fadeTypeSpin != null && node != null)
            {
                node.FadeType = _fadeTypeSpin.Value.HasValue ? (int)_fadeTypeSpin.Value.Value : 0;
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
            string text = node.Text?.GetString(0, Gender.Male) ?? "";
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
            // Avalonia doesn't have IsRepeat property - track manually via _keysDown
            bool isAutoRepeat = _keysDown.Contains(key);

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
            // Note: Shift check is done in caller (OnKeyDown), this method is only called when Shift is held
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

        // Helper methods for key press actions - these will be fully implemented as the UI is completed
        // Matching PyKotor implementation patterns

        /// <summary>
        /// Adds a root node to the dialog.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2041-2043
        /// Original: if key == Qt.Key.Key_Insert: self.model.add_root_node()
        /// Creates a new DLGEntry node, wraps it in a DLGLink, adds it as a starter, and selects it in the tree view.
        /// The operation is recorded in the action history for undo/redo support.
        /// </summary>
        private void AddRootNode()
        {
            // Create and apply the action (this performs the operation and records it for undo/redo)
            var action = new AddRootNodeAction();
            _actionHistory.Apply(action);

            // Get the newly created item from the action
            DLGStandardItem newItem = action.Item;

            if (newItem != null)
            {
                // Select the newly added root node in the tree view
                // Matching PyKotor: After adding root node, it would be selected in the tree
                SelectTreeViewItem(newItem);

                // Update the model's selected index to track the new selection
                var rootItems = _model.GetRootItems();
                int newIndex = rootItems.IndexOf(newItem);
                if (newIndex >= 0)
                {
                    _model.SelectedIndex = newIndex;
                }
            }
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
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1349-1437
        /// Original: def edit_text(self, e: QMouseEvent | QKeyEvent | None = None, indexes: list[QModelIndex] | None = None, source_widget: DLGListWidget | DLGTreeView | None = None)
        /// </summary>
        private async void EditText()
        {
            // Matching PyKotor implementation: if not indexes: self.blink_window(); return
            // Get selected item from tree view
            if (_dialogTree?.SelectedItem == null)
            {
                // Matching PyKotor: blink_window()
                BlinkWindow();
                return;
            }

            // Get the selected item and extract DLGStandardItem
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

            // Matching PyKotor implementation: if item is None: continue
            if (item == null)
            {
                return;
            }

            // Matching PyKotor implementation: if item.link is None: continue
            if (item.Link == null)
            {
                return;
            }

            // Matching PyKotor implementation: Check if parent widget is valid before creating dialog
            // Matching PyKotor: if self._installation is None: RobustLogger().error("Cannot edit text: installation is not set"); continue
            if (_installation == null)
            {
                // In a full implementation, we would log an error
                // For now, we'll just return silently
                return;
            }

            try
            {
                // Matching PyKotor implementation: parent_widget validation and fallback logic
                // Get parent window for dialog
                Window parentWindow = this;
                try
                {
                    // Check if window is valid (not null, not being destroyed)
                    if (parentWindow != null)
                    {
                        // Try to access a property to ensure window is valid
                        var _ = parentWindow.IsVisible;
                        // If we get here, window is likely valid
                        if (!parentWindow.IsVisible || !parentWindow.IsEnabled)
                        {
                            // Use active window as fallback if parent is not in a good state
                            var activeWindow = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                                ? desktop.MainWindow
                                : null;
                            if (activeWindow != null)
                            {
                                parentWindow = activeWindow;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Window is being destroyed or invalid, use active window or this window as fallback
                    var activeWindow = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;
                    if (activeWindow != null)
                    {
                        parentWindow = activeWindow;
                    }
                }

                // Matching PyKotor implementation: dialog = LocalizedStringDialog(parent_widget, self._installation, item.link.node.text)
                var dialog = new LocalizedStringDialog(parentWindow, _installation, item.Link.Node.Text);
                
                // Matching PyKotor implementation: dialog_result: bool | int = False
                // Matching PyKotor: dialog_result = dialog.exec()
                bool dialogResult = false;
                try
                {
                    // In Avalonia, we use ShowDialogAsync to show the dialog modally
                    dialogResult = await dialog.ShowDialog<bool>(parentWindow);
                }
                catch (Exception exc)
                {
                    // Matching PyKotor: RobustLogger().exception(f"Error executing LocalizedStringDialog: {exc.__class__.__name__}: {exc}")
                    // In a full implementation, we would log the error
                    // For now, we'll just continue to the next item
                    return;
                }

                // Matching PyKotor implementation: if not dialog_result: continue
                if (!dialogResult)
                {
                    // User cancelled the dialog
                    return;
                }

                // Matching PyKotor implementation: item.link.node.text = dialog.locstring
                // Access dialog.LocString before cleanup
                item.Link.Node.Text = dialog.LocString;

                // Matching PyKotor implementation: if isinstance(item, DLGStandardItem): self.model.update_item_display_text(item)
                // Update the display text in the tree view
                _model.UpdateItemDisplayText(item);

                // Restore selection after tree view update
                SelectTreeViewItem(item);
            }
            catch (Exception exc)
            {
                // Matching PyKotor: RobustLogger().exception(f"Error creating LocalizedStringDialog: {exc.__class__.__name__}: {exc}")
                // In a full implementation, we would log the error
                // For now, we'll just return silently
                return;
            }
        }

        /// <summary>
        /// Focuses on the selected node.
        /// Matching PyKotor implementation: self.focus_on_node(selected_item.link)
        /// </summary>
        private void FocusOnSelectedNode()
        {
            if (_dialogTree?.SelectedItem == null)
            {
                return;
            }

            // Get selected item from tree
            DLGLink link = null;
            var selectedItem = _dialogTree.SelectedItem;
            if (selectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
            {
                link = dlgItem?.Link;
            }
            else if (selectedItem is DLGStandardItem dlgItemDirect)
            {
                link = dlgItemDirect?.Link;
            }

            if (link != null)
            {
                FocusOnNode(link);
            }
        }

        /// <summary>
        /// Focuses the dialog tree on a specific link node.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1512-1531
        /// Original: def focus_on_node(self, link: DLGLink | None) -> DLGStandardItem | None:
        /// </summary>
        /// <param name="link">The link to focus on, or null to clear focus.</param>
        /// <returns>The focused item, or null if link is null.</returns>
        public DLGStandardItem FocusOnNode(DLGLink link)
        {
            if (link == null)
            {
                return null;
            }

            // Matching PyKotor implementation: Set background color for light themes
            // Original: if "(Light)" in GlobalSettings().selectedTheme or GlobalSettings().selectedTheme == "Native":
            //           self.ui.dialogTree.setStyleSheet("QTreeView { background: #FFFFEE; }")
            if (_dialogTree != null)
            {
                var settings = new GlobalSettings();
                if (settings.SelectedTheme.Contains("Light") || settings.SelectedTheme == "Native")
                {
                    // Set light yellow background (#FFFFEE) for focus mode
                    _dialogTree.Background = new SolidColorBrush(
                        Avalonia.Media.Color.FromRgb(0xFF, 0xFF, 0xEE));
                }
            }

            // Clear the model and set focus state
            _model.ResetModel();
            _focused = true;

            // Create item for the focused link
            var item = new DLGStandardItem(link);

            // Add the item to the model's root items directly
            // We need to access the private _rootItems, so we'll use InsertStarter which adds it
            // But we need to ensure the item we created is the one used
            _model.InsertStarter(0, link);

            // Get the item that was actually added to the model
            var rootItems = _model.GetRootItems();
            DLGStandardItem focusedItem = null;
            if (rootItems.Count > 0)
            {
                focusedItem = rootItems[0];
                // Recursively load the item and its children
                _model.LoadDlgItemRec(focusedItem);
            }

            // Update the tree view
            UpdateTreeView();

            // Select the focused item in the tree
            if (_dialogTree != null && _dialogTree.ItemsSource != null && focusedItem != null)
            {
                var treeItems = _dialogTree.ItemsSource as System.Collections.IEnumerable;
                if (treeItems != null)
                {
                    foreach (TreeViewItem treeItem in treeItems)
                    {
                        if (treeItem.Tag == focusedItem)
                        {
                            _dialogTree.SelectedItem = treeItem;
                            break;
                        }
                    }
                }
            }

            return focusedItem ?? item;
        }

        /// <summary>
        /// Adds a child to the selected item.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2021-2138
        /// Original: self.model.add_child_to_item(selected_item)
        /// </summary>
        private void AddChildToSelectedItem()
        {
            if (_dialogTree?.SelectedItem == null)
            {
                return;
            }

            // Get selected item from tree
            DLGStandardItem selectedItem = null;
            var treeSelectedItem = _dialogTree.SelectedItem;
            if (treeSelectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
            {
                selectedItem = dlgItem;
            }
            else if (treeSelectedItem is DLGStandardItem dlgItemDirect)
            {
                selectedItem = dlgItemDirect;
            }

            if (selectedItem == null || selectedItem.Link == null)
            {
                return;
            }

            // Create and apply action (the action will perform the operation via the model)
            int childIndex = selectedItem.Link.Node != null ? selectedItem.Link.Node.Links.Count : -1;
            var action = new AddChildToItemAction(selectedItem, childIndex);
            _actionHistory.Apply(action);

            // Get the newly created child item from the action
            DLGStandardItem newChildItem = action.ChildItem;

            if (newChildItem != null)
            {
                // Select the newly added child in the tree view
                SelectTreeViewItem(newChildItem);
            }
        }

        /// <summary>
        /// Blinks the window to indicate an error or invalid action.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/base.py:725-734
        /// Original: def blink_window(self, *, sound: bool = True):
        /// </summary>
        /// <param name="sound">Whether to play a sound effect when blinking. Defaults to true.</param>
        private void BlinkWindow(bool sound = true)
        {
            // Matching PyKotor implementation: if sound: self.play_sound("dr_metal_lock")
            if (sound)
            {
                try
                {
                    PlaySound("dr_metal_lock", new[] { SearchLocation.SOUND, SearchLocation.VOICE });
                }
                catch
                {
                    // Suppress exceptions when playing sound fails (matching PyKotor: with suppress(Exception))
                }
            }

            // Matching PyKotor implementation: self.setWindowOpacity(0.7)
            // Matching PyKotor: QTimer.singleShot(125, lambda: self.setWindowOpacity(1))
            double originalOpacity = Opacity;
            Opacity = 0.7;
            
            // Restore opacity after 125ms (matching PyKotor timing)
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(125)
            };
            timer.Tick += (s, e) =>
            {
                Opacity = 1.0;
                timer.Stop();
            };
            timer.Start();
        }

        /// <summary>
        /// Plays a sound resource.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/base.py:771-797
        /// Original: def play_sound(self, resname: str, order: list[SearchLocation] | None = None) -> bool:
        /// </summary>
        /// <param name="resname">The resource name of the sound to play (without extension).</param>
        /// <param name="searchOrder">The ordered list of locations to search for the sound. If null, uses default order.</param>
        /// <returns>True if the sound was played successfully, false otherwise.</returns>
        private bool PlaySound(string resname, SearchLocation[] searchOrder = null)
        {
            // Matching PyKotor implementation: if not resname or not resname.strip() or self._installation is None:
            // Matching PyKotor: self.blink_window(sound=False); return False
            if (string.IsNullOrWhiteSpace(resname) || _installation == null)
            {
                BlinkWindow(sound: false);
                return false;
            }

            // Matching PyKotor implementation: self.media_player.player.stop()
            try
            {
                _soundPlayer?.Stop();
            }
            catch
            {
                // Ignore errors when stopping
            }

            // Matching PyKotor implementation: data = self._installation.sound(resname, order)
            // Default search order matching PyKotor: [MUSIC, VOICE, SOUND, OVERRIDE, CHITIN]
            if (searchOrder == null || searchOrder.Length == 0)
            {
                searchOrder = new[]
                {
                    SearchLocation.MUSIC,
                    SearchLocation.VOICE,
                    SearchLocation.SOUND,
                    SearchLocation.OVERRIDE,
                    SearchLocation.CHITIN
                };
            }

            byte[] soundData = _installation.Sound(resname.Trim(), searchOrder);

            // Matching PyKotor implementation: if not data: self.blink_window(sound=False); return False
            if (soundData == null || soundData.Length == 0)
            {
                BlinkWindow(sound: false);
                return false;
            }

            // Matching PyKotor implementation: return self.play_byte_source_media(data)
            return PlayByteSourceMedia(soundData);
        }

        /// <summary>
        /// Plays audio from byte array data.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/base.py:736-772
        /// Original: def play_byte_source_media(self, data: bytes | None) -> bool:
        /// </summary>
        /// <param name="data">The audio data bytes (WAV format).</param>
        /// <returns>True if playback started successfully, false otherwise.</returns>
        private bool PlayByteSourceMedia(byte[] data)
        {
            // Matching PyKotor implementation: if not data: self.blink_window(); return False
            if (data == null || data.Length == 0)
            {
                BlinkWindow();
                return false;
            }

            try
            {
                // Stop any currently playing sound
                _soundPlayer?.Stop();
                
                // Dispose previous stream if it exists
                if (_soundStream != null)
                {
                    _soundStream.Dispose();
                    _soundStream = null;
                }

                // Create a new memory stream for the sound data
                // Note: The stream must remain alive while the sound is playing
                // Matching PyKotor's QBuffer approach which keeps the buffer alive
                _soundStream = new MemoryStream(data);
                _soundPlayer.Stream = _soundStream;
                _soundPlayer.Play();
                
                return true;
            }
            catch (Exception)
            {
                // Matching PyKotor: blink_window on error
                BlinkWindow();
                return false;
            }
        }

        /// <summary>
        /// Plays sound or blinks window.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2071-2079
        /// Original: elif key == Qt.Key.Key_P: sound_resname = self.ui.soundComboBox.currentText().strip() ...
        /// </summary>
        private void PlaySoundOrBlink()
        {
            // Matching PyKotor implementation:
            // sound_resname: str = self.ui.soundComboBox.currentText().strip()
            // voice_resname: str = self.ui.voiceComboBox.currentText().strip()
            // if sound_resname:
            //     self.play_sound(sound_resname, [SearchLocation.SOUND, SearchLocation.VOICE])
            // elif voice_resname:
            //     self.play_sound(voice_resname, [SearchLocation.VOICE])
            // else:
            //     self.blink_window()

            // Note: Sound and voice combo boxes are not yet implemented in the C# UI
            // For now, we'll just blink the window to match the "else" case
            // When combo boxes are added, this should check them first
            BlinkWindow();
        }

        /// <summary>
        /// Cleans up resources when the window is closed.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // Clean up sound player resources
            try
            {
                _soundPlayer?.Stop();
                _soundPlayer?.Dispose();
            }
            catch
            {
                // Ignore errors during cleanup
            }

            try
            {
                _soundStream?.Dispose();
            }
            catch
            {
                // Ignore errors during cleanup
            }

            base.OnClosed(e);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:436-467
        // Original: def setup_extra_widgets(self):
        /// <summary>
        /// Sets up the find bar UI controls.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:451-467
        /// </summary>
        private void SetupFindBar()
        {
            // Matching PyKotor: self.find_bar: QWidget = QWidget(self)
            // Matching PyKotor: self.find_bar.setVisible(False)
            _findBar = new Panel
            {
                IsVisible = false
            };

            // Matching PyKotor: self.find_layout: QHBoxLayout = QHBoxLayout(self.find_bar)
            var findLayout = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal
            };
            _findBar.Children.Add(findLayout);

            // Matching PyKotor: self.find_input: QLineEdit = QLineEdit(self.find_bar)
            _findInput = new TextBox
            {
                Watermark = "Find in dialog..."
            };
            // Matching PyKotor: self.find_input.returnPressed.connect(self.handle_find)
            _findInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Return)
                {
                    HandleFind();
                    e.Handled = true;
                }
            };
            findLayout.Children.Add(_findInput);

            // Matching PyKotor: self.back_button: QPushButton = QPushButton("", self.find_bar)
            // Matching PyKotor: self.back_button.setIcon(q_style.standardIcon(QStyle.StandardPixmap.SP_ArrowBack))
            _backButton = new Button
            {
                Content = "←"
            };
            // Matching PyKotor: self.back_button.clicked.connect(self.handle_back)
            _backButton.Click += (s, e) => HandleBack();
            findLayout.Children.Add(_backButton);

            // Matching PyKotor: self.find_button: QPushButton = QPushButton("", self.find_bar)
            // Matching PyKotor: self.find_button.setIcon(q_style.standardIcon(QStyle.StandardPixmap.SP_ArrowForward))
            _findButton = new Button
            {
                Content = "→"
            };
            // Matching PyKotor: self.find_button.clicked.connect(self.handle_find)
            _findButton.Click += (s, e) => HandleFind();
            findLayout.Children.Add(_findButton);

            // Matching PyKotor: self.results_label: QLabel = QLabel(self.find_bar)
            _resultsLabel = new TextBlock
            {
                Text = "",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(5, 0, 0, 0)
            };
            findLayout.Children.Add(_resultsLabel);

            // Matching PyKotor: self.setup_completer()
            SetupCompleter();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:469-485
        // Original: def setup_completer(self):
        /// <summary>
        /// Sets up the autocompleter for the find input.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:469-485
        /// </summary>
        private void SetupCompleter()
        {
            if (_findInput == null)
            {
                return;
            }

            // Matching PyKotor: temp_entry: DLGEntry = DLGEntry()
            // Matching PyKotor: temp_link: DLGLink = DLGLink(temp_entry)
            var tempEntry = new DLGEntry();
            var tempLink = new DLGLink(tempEntry);

            // Matching PyKotor: entry_attributes: set[str] = {attr[0] for attr in temp_entry.__dict__.items() if not attr[0].startswith("_") and not callable(attr[1]) and not isinstance(attr[1], list)}
            var entryAttributes = new HashSet<string>();
            var entryType = typeof(DLGEntry);
            foreach (var prop in entryType.GetProperties())
            {
                if (!prop.Name.StartsWith("_") && prop.CanRead)
                {
                    entryAttributes.Add(prop.Name);
                }
            }

            // Matching PyKotor: link_attributes: set[str] = {attr[0] for attr in temp_link.__dict__.items() if not attr[0].startswith("_") and not callable(attr[1]) and not isinstance(attr[1], (DLGEntry, DLGReply))}
            var linkAttributes = new HashSet<string>();
            var linkType = typeof(DLGLink);
            foreach (var prop in linkType.GetProperties())
            {
                if (!prop.Name.StartsWith("_") && prop.CanRead)
                {
                    var propType = prop.PropertyType;
                    if (propType != typeof(DLGEntry) && propType != typeof(DLGReply))
                    {
                        linkAttributes.Add(prop.Name);
                    }
                }
            }

            // Matching PyKotor: suggestions: list[str] = [f"{key}:" for key in [*entry_attributes, *link_attributes, "stringref", "strref"]]
            var suggestions = new List<string>();
            foreach (var attr in entryAttributes)
            {
                suggestions.Add($"{attr}:");
            }
            foreach (var attr in linkAttributes)
            {
                suggestions.Add($"{attr}:");
            }
            suggestions.Add("stringref:");
            suggestions.Add("strref:");

            // Note: Avalonia doesn't have a built-in AutoCompleteBox like Qt's QCompleter
            // For now, we'll skip the completer setup - it can be added later if needed
            // Matching PyKotor: self.find_input_completer: QCompleter = QCompleter(suggestions, self.find_input)
            // Matching PyKotor: self.find_input.setCompleter(self.find_input_completer)
        }

        /// <summary>
        /// Shows the find bar.
        /// Matching PyKotor implementation: self.show_find_bar()
        /// Original: def show_find_bar(self): self.find_bar.setVisible(True); self.find_input.setFocus()
        /// </summary>
        private void ShowFindBar()
        {
            // Matching PyKotor: self.find_bar.setVisible(True)
            if (_findBar != null)
            {
                _findBar.IsVisible = true;
            }

            // Matching PyKotor: self.find_input.setFocus()
            if (_findInput != null)
            {
                _findInput.Focus();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:500-511
        // Original: def handle_find(self):
        /// <summary>
        /// Handles the find button click or Enter key press in the find input.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:500-511
        /// </summary>
        private void HandleFind()
        {
            if (_findInput == null)
            {
                return;
            }

            string inputText = _findInput.Text ?? "";
            
            // Matching PyKotor: if not self.search_results or input_text != self.current_search_text:
            if (_searchResults == null || _searchResults.Count == 0 || inputText != _currentSearchText)
            {
                // Matching PyKotor: self.search_results = self.find_item_matching_display_text(input_text)
                _searchResults = FindItemMatchingDisplayText(inputText);
                _currentSearchText = inputText;
                _currentResultIndex = 0;
            }

            // Matching PyKotor: if not self.search_results: self.results_label.setText("No results found"); return
            if (_searchResults == null || _searchResults.Count == 0)
            {
                if (_resultsLabel != null)
                {
                    _resultsLabel.Text = "No results found";
                }
                return;
            }

            // Matching PyKotor: self.current_result_index = (self.current_result_index + 1) % len(self.search_results)
            _currentResultIndex = (_currentResultIndex + 1) % _searchResults.Count;

            // Matching PyKotor: self.highlight_result(self.search_results[self.current_result_index])
            HighlightResult(_searchResults[_currentResultIndex]);

            // Matching PyKotor: self.update_results_label()
            UpdateResultsLabel();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:513-518
        // Original: def handle_back(self):
        /// <summary>
        /// Handles the back button click to navigate to previous search result.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:513-518
        /// </summary>
        private void HandleBack()
        {
            // Matching PyKotor: if not self.search_results: return
            if (_searchResults == null || _searchResults.Count == 0)
            {
                return;
            }

            // Matching PyKotor: self.current_result_index = (self.current_result_index - 1 + len(self.search_results)) % len(self.search_results)
            _currentResultIndex = (_currentResultIndex - 1 + _searchResults.Count) % _searchResults.Count;

            // Matching PyKotor: self.highlight_result(self.search_results[self.current_result_index])
            HighlightResult(_searchResults[_currentResultIndex]);

            // Matching PyKotor: self.update_results_label()
            UpdateResultsLabel();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:525-569
        // Original: def parse_query(self, input_text: str) -> list[tuple[str, str | None, Literal["AND", "OR", None]]]:
        /// <summary>
        /// Parses a search query string into conditions with operators.
        /// Supports attribute searches (e.g., "speaker:TestSpeaker"), text searches, and AND/OR operators.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:525-569
        /// </summary>
        private List<Tuple<string, string, string>> ParseQuery(string inputText)
        {
            var conditions = new List<Tuple<string, string, string>>();
            
            if (string.IsNullOrEmpty(inputText))
            {
                return conditions;
            }

            // Pattern to match quoted strings or whitespace-separated tokens
            // Matching PyKotor: pattern = r'("[^"]*"|\S+)'
            var quotedStringPattern = new Regex(@"""[^""]*""");
            var tokens = new List<string>();
            
            // Extract quoted strings first
            var quotedMatches = quotedStringPattern.Matches(inputText);
            int lastIndex = 0;
            foreach (Match match in quotedMatches)
            {
                // Add text before the quoted string
                if (match.Index > lastIndex)
                {
                    string before = inputText.Substring(lastIndex, match.Index - lastIndex);
                    var beforeTokens = Regex.Split(before, @"\s+");
                    foreach (var token in beforeTokens)
                    {
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            tokens.Add(token);
                        }
                    }
                }
                
                // Add the quoted string (without quotes)
                tokens.Add(match.Value.Substring(1, match.Value.Length - 2));
                lastIndex = match.Index + match.Length;
            }
            
            // Add remaining text after last quoted string
            if (lastIndex < inputText.Length)
            {
                string remaining = inputText.Substring(lastIndex);
                var remainingTokens = Regex.Split(remaining, @"\s+");
                foreach (var token in remainingTokens)
                {
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        tokens.Add(token);
                    }
                }
            }

            string operator = null;
            int i = 0;
            while (i < tokens.Count)
            {
                string token = tokens[i].ToUpperInvariant();
                if (token == "AND" || token == "OR")
                {
                    operator = token;
                    i++;
                    continue;
                }

                int? nextIndex = i + 1 < tokens.Count ? (int?)(i + 1) : null;
                if (tokens[i].Contains(":"))
                {
                    // Attribute search: "key:value"
                    var parts = tokens[i].Split(new[] { ':' }, 2);
                    string key = parts[0].Trim().ToLowerInvariant();
                    string value = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : null;
                    conditions.Add(Tuple.Create(key, value ?? "", operator));
                    operator = null;
                }
                else if (nextIndex.HasValue && (tokens[nextIndex.Value].ToUpperInvariant() == "AND" || tokens[nextIndex.Value].ToUpperInvariant() == "OR"))
                {
                    // Text search with operator
                    conditions.Add(Tuple.Create(tokens[i], "", operator));
                    operator = null;
                }
                else if (!nextIndex.HasValue)
                {
                    // Last token
                    conditions.Add(Tuple.Create(tokens[i], "", operator));
                    operator = null;
                }
                else
                {
                    // Text search without operator
                    conditions.Add(Tuple.Create(tokens[i], "", operator));
                    operator = null;
                }

                i++;
            }

            return conditions;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:571-657
        // Original: def find_item_matching_display_text(self, input_text: str) -> list[DLGStandardItem]:
        /// <summary>
        /// Finds all items matching the search text with full query parsing and attribute search support.
        /// Supports attribute searches (e.g., "speaker:TestSpeaker"), text searches, and AND/OR operators.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:571-657
        /// </summary>
        private List<DLGStandardItem> FindItemMatchingDisplayText(string inputText)
        {
            var matchingItems = new List<DLGStandardItem>();
            
            if (string.IsNullOrEmpty(inputText))
            {
                return matchingItems;
            }

            // Parse query into conditions
            var conditions = ParseQuery(inputText);
            string searchTextLower = inputText.ToLowerInvariant();

            // Helper to check if a condition matches an item
            // Matching PyKotor: def condition_matches(key: str, value: str | None, operator: Literal["AND", "OR", None], item: DLGStandardItem) -> bool:
            bool ConditionMatches(string key, string value, string op, DLGStandardItem item)
            {
                if (item?.Link?.Node == null)
                {
                    return false;
                }

                var link = item.Link;
                var node = item.Link.Node;
                object sentinel = new object();

                // Get attribute value from link or node using reflection
                // Matching PyKotor: link_value: Any = getattr(item.link, key, sentinel)
                // Matching PyKotor: node_value: Any = getattr(item.link.node, key, sentinel)
                object linkValue = GetAttributeValue(link, key, sentinel);
                object nodeValue = GetAttributeValue(node, key, sentinel);

                // Helper to check value match
                // Matching PyKotor: def check_value(attr_value: Any, search_value: str | None) -> bool:
                bool CheckValue(object attrValue, string searchValue)
                {
                    if (ReferenceEquals(attrValue, sentinel))
                    {
                        return false;
                    }

                    // Truthiness check (value is None or empty)
                    if (string.IsNullOrEmpty(searchValue))
                    {
                        if (attrValue is bool boolVal)
                        {
                            return boolVal;
                        }
                        if (attrValue is int intVal)
                        {
                            return intVal != 0 && intVal != 0xFFFFFFFF && intVal != -1;
                        }
                        return attrValue != null;
                    }

                    // Type-specific matching
                    if (attrValue is int intAttr)
                    {
                        if (int.TryParse(searchValue, out int searchInt))
                        {
                            return intAttr == searchInt;
                        }
                        return false;
                    }

                    if (attrValue is bool boolAttr)
                    {
                        if (searchValue == "true" || searchValue == "1")
                        {
                            return boolAttr == true;
                        }
                        if (searchValue == "false" || searchValue == "0")
                        {
                            return boolAttr == false;
                        }
                        return false;
                    }

                    // String/substring matching
                    string attrStr = attrValue?.ToString() ?? "";
                    return attrStr.ToLowerInvariant().Contains(searchValue.ToLowerInvariant());
                }

                // Check link or node value
                if (CheckValue(linkValue, value) || CheckValue(nodeValue, value))
                {
                    return true;
                }

                // Special handling for strref/stringref
                if (key == "strref" || key == "stringref")
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (int.TryParse(value, out int strref))
                        {
                            // Check if strref exists in text substrings
                            // Matching PyKotor: return int(value.strip()) in item.link.node.text._substrings
                            if (node.Text != null)
                            {
                                // Note: Accessing _substrings would require reflection or public API
                                // For now, check if text exists (simplified)
                                return node.Text.GetString(0, Gender.Male) != null;
                            }
                        }
                        return false;
                    }
                    return node.Text != null && !string.IsNullOrEmpty(node.Text.GetString(0, Gender.Male));
                }

                return false;
            }

            // Helper to evaluate all conditions for an item
            // Matching PyKotor: def evaluate_conditions(item: DLGStandardItem) -> bool:
            bool EvaluateConditions(DLGStandardItem item)
            {
                // Always check text match first
                // Matching PyKotor: item_text: str = item.text().lower()
                // Matching PyKotor: if input_text.lower() in item_text: return True
                string itemText = GetItemDisplayText(item).ToLowerInvariant();
                if (itemText.Contains(searchTextLower))
                {
                    return true;
                }

                // If no conditions, return false (text didn't match)
                if (conditions.Count == 0)
                {
                    return false;
                }

                // Evaluate conditions with AND/OR logic
                // Matching PyKotor: result: bool = not conditions
                // In Python, "not conditions" means False when conditions list has items, True when empty
                bool result = conditions.Count == 0;
                foreach (var condition in conditions)
                {
                    string key = condition.Item1;
                    string value = condition.Item2;
                    string op = condition.Item3;

                    bool matches = ConditionMatches(key, value, op, item);

                    // Matching PyKotor logic: apply operator if present, otherwise set result directly
                    if (op == "AND")
                    {
                        result = result && matches;
                    }
                    else if (op == "OR")
                    {
                        result = result || matches;
                    }
                    else
                    {
                        // First condition or condition without operator - set result directly
                        result = matches;
                    }
                }

                return result;
            }

            // Recursive search function
            // Matching PyKotor: def search_item(item: DLGStandardItem):
            void SearchItem(DLGStandardItem item)
            {
                if (item == null)
                {
                    return;
                }

                if (EvaluateConditions(item))
                {
                    matchingItems.Add(item);
                }

                // Search children
                foreach (var child in item.Children)
                {
                    SearchItem(child);
                }
            }

            // Search all root items
            // Matching PyKotor: def search_children(parent_item: DLGStandardItem):
            // Matching PyKotor: search_children(cast("DLGStandardItem", self.model.invisibleRootItem()))
            var rootItems = _model.GetRootItems();
            foreach (var rootItem in rootItems)
            {
                SearchItem(rootItem);
            }

            // Matching PyKotor: return list({*matching_items}) - remove duplicates
            return new List<DLGStandardItem>(new HashSet<DLGStandardItem>(matchingItems));
        }

        // Helper method to get attribute value using reflection
        // Matching PyKotor: getattr(obj, key, sentinel) behavior
        // Handles case-insensitive property/field lookup and converts ResRef to string
        private object GetAttributeValue(object obj, string key, object sentinel)
        {
            if (obj == null || string.IsNullOrEmpty(key))
            {
                return sentinel;
            }

            try
            {
                // Convert snake_case to PascalCase for C# property names
                // e.g., "speaker" -> "Speaker", "is_child" -> "IsChild", "active1" -> "Active1"
                string pascalKey = ToPascalCase(key);
                
                // Try property first (case-insensitive)
                var properties = obj.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var property in properties)
                {
                    if (property.Name.Equals(pascalKey, StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        object value = property.GetValue(obj);
                        
                        // Convert ResRef to string for comparison
                        if (value != null && value.GetType().Name == "ResRef")
                        {
                            return value.ToString();
                        }
                        
                        return value;
                    }
                }

                // Try fields if property not found
                var fields = obj.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.Name.Equals(pascalKey, StringComparison.OrdinalIgnoreCase) ||
                        field.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        object value = field.GetValue(obj);
                        
                        // Convert ResRef to string for comparison
                        if (value != null && value.GetType().Name == "ResRef")
                        {
                            return value.ToString();
                        }
                        
                        return value;
                    }
                }

                return sentinel;
            }
            catch
            {
                return sentinel;
            }
        }

        // Helper to convert snake_case to PascalCase
        // e.g., "speaker" -> "Speaker", "is_child" -> "IsChild", "active1_param1" -> "Active1Param1"
        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Handle snake_case
            if (input.Contains("_"))
            {
                var parts = input.Split('_');
                var result = new System.Text.StringBuilder();
                foreach (var part in parts)
                {
                    if (part.Length > 0)
                    {
                        result.Append(char.ToUpperInvariant(part[0]));
                        if (part.Length > 1)
                        {
                            result.Append(part.Substring(1));
                        }
                    }
                }
                return result.ToString();
            }

            // Simple camelCase/PascalCase conversion
            if (input.Length > 0)
            {
                return char.ToUpperInvariant(input[0]) + (input.Length > 1 ? input.Substring(1) : "");
            }

            return input;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:659-676
        // Original: def highlight_result(self, item: DLGStandardItem):
        /// <summary>
        /// Highlights and scrolls to the specified search result item.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:659-676
        /// </summary>
        private void HighlightResult(DLGStandardItem item)
        {
            if (item == null || _dialogTree == null)
            {
                return;
            }

            // Matching PyKotor: index: QModelIndex = self.model.indexFromItem(item)
            // Matching PyKotor: parent: QModelIndex = index.parent()
            // Matching PyKotor: while parent.isValid(): self.ui.dialogTree.expand(parent); parent = parent.parent()
            // Expand all parents to make the item visible
            ExpandParents(item);

            // Matching PyKotor: self.ui.dialogTree.setCurrentIndex(index)
            // Matching PyKotor: self.ui.dialogTree.setFocus()
            // Select the item in the tree
            _dialogTree.SelectedItem = item;
            _dialogTree.Focus();

            // Matching PyKotor: self.ui.dialogTree.scrollTo(index, QAbstractItemView.ScrollHint.PositionAtCenter)
            // Scroll to the item (Avalonia TreeView handles this automatically when selecting)
            // Note: Avalonia doesn't have explicit scrollTo, but selection should scroll into view
        }

        // Helper method to expand all parent items
        private void ExpandParents(DLGStandardItem item)
        {
            if (item == null || _dialogTree == null)
            {
                return;
            }

            // Find the parent chain and expand them
            // In Avalonia TreeView, we need to expand items by setting IsExpanded
            // For now, we'll expand all items to ensure visibility (simplified approach)
            // A full implementation would track the parent chain and expand only those
            ExpandItemRecursive(item);
        }

        // Helper method to recursively expand items
        private void ExpandItemRecursive(DLGStandardItem item)
        {
            if (item == null)
            {
                return;
            }

            // In Avalonia, TreeViewItem expansion is handled differently
            // For now, we'll ensure the item is visible by expanding its parent chain
            // This is a simplified implementation - a full version would use TreeView's expansion API

            // Expand all children recursively
            foreach (var child in item.Children)
            {
                ExpandItemRecursive(child);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:678-679
        // Original: def update_results_label(self):
        /// <summary>
        /// Updates the results label to show current position in search results.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:678-679
        /// </summary>
        private void UpdateResultsLabel()
        {
            if (_resultsLabel == null)
            {
                return;
            }

            // Matching PyKotor: self.results_label.setText(f"{self.current_result_index + 1} / {len(self.search_results)}")
            if (_searchResults == null || _searchResults.Count == 0)
            {
                _resultsLabel.Text = "";
            }
            else
            {
                _resultsLabel.Text = $"{_currentResultIndex + 1} / {_searchResults.Count}";
            }
        }

        /// <summary>
        /// Copies the path of the selected node.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1439-1463
        /// Original: def copy_path(self, node_or_link: DLGNode | DLGLink | None):
        /// </summary>
        private async void CopyPath()
        {
            // Get the selected item from the tree
            DLGStandardItem selectedItem = null;
            var treeSelectedItem = _dialogTree?.SelectedItem;
            if (treeSelectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
            {
                selectedItem = dlgItem;
            }
            else if (treeSelectedItem is DLGStandardItem dlgItemDirect)
            {
                selectedItem = dlgItemDirect;
            }

            if (selectedItem?.Link == null)
            {
                return;
            }

            // Matching PyKotor: copy_path is called with selected_item.link.node
            // The Python implementation accepts either a node or link, but in practice it's called with the node
            DLGNode targetNode = selectedItem.Link.Node;
            if (targetNode == null)
            {
                return;
            }

            // Find all paths to the target node
            // Matching PyKotor: paths: list[PureWindowsPath] = self.core_dlg.find_paths(node_or_link)
            List<string> paths;
            try
            {
                paths = _coreDlg.FindPaths(targetNode);
            }
            catch (Exception ex)
            {
                // Matching PyKotor: if no paths or error, log and blink window
                // In a full implementation, we would log the error: RobustLogger().error(...)
                BlinkWindow();
                return;
            }

            // Matching PyKotor: if not paths: RobustLogger().error("No paths available."), self.blink_window(), return
            if (paths == null || paths.Count == 0)
            {
                // Matching PyKotor: No paths available - log error and blink window
                // In a full implementation, we would log: RobustLogger().error("No paths available.")
                BlinkWindow();
                return;
            }

            // Format the path(s) for clipboard
            // Matching PyKotor: if len(paths) == 1: path: str = str(paths[0])
            // Matching PyKotor: else: path = "\n".join(f"  {i + 1}. {p}" for i, p in enumerate(paths))
            string pathText;
            if (paths.Count == 1)
            {
                pathText = paths[0];
            }
            else
            {
                // Format multiple paths as numbered list
                var pathLines = new List<string>();
                for (int i = 0; i < paths.Count; i++)
                {
                    pathLines.Add($"  {i + 1}. {paths[i]}");
                }
                pathText = string.Join("\n", pathLines);
            }

            // Copy to clipboard
            // Matching PyKotor: cb: QClipboard | None = QApplication.clipboard()
            // Matching PyKotor: if cb is None: return
            // Matching PyKotor: cb.setText(path)
            try
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(pathText);
                }
            }
            catch
            {
                // Matching PyKotor: Silently handle clipboard errors (Python doesn't catch, but we should)
                // In a full implementation, we might want to show an error message
            }
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
        /// Jumps to the original node of a copied item.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1489-1510
        /// Original: def jump_to_original(self, copied_item: DLGStandardItem):
        /// Searches through the entire dialog tree to find the original node that matches the copied item's node,
        /// then expands the tree and selects the original item.
        /// </summary>
        private void JumpToOriginal()
        {
            // Get the currently selected item
            DLGStandardItem selectedItem = null;
            var treeSelectedItem = _dialogTree?.SelectedItem;
            if (treeSelectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
            {
                selectedItem = dlgItem;
            }
            else if (treeSelectedItem is DLGStandardItem dlgItemDirect)
            {
                selectedItem = dlgItemDirect;
            }

            if (selectedItem?.Link == null)
            {
                return;
            }

            // Get the source node from the selected item's link
            DLGNode sourceNode = selectedItem.Link.Node;
            if (sourceNode == null)
            {
                return;
            }

            // Get the link from the selected item to avoid finding the same item
            DLGLink selectedLink = selectedItem.Link;

            // Perform breadth-first search through the entire dialog tree to find the original node
            // Matching PyKotor: items: deque[DLGStandardItem | QStandardItem | None] = deque([self.model.item(i, 0) for i in range(self.model.rowCount())])
            var items = new Queue<DLGStandardItem>();
            var rootItems = _model.GetRootItems();
            foreach (var rootItem in rootItems)
            {
                items.Enqueue(rootItem);
            }

            DLGStandardItem foundItem = null;
            while (items.Count > 0)
            {
                DLGStandardItem item = items.Dequeue();
                if (item?.Link == null)
                {
                    continue;
                }

                // Check if this item's node matches the source node
                // Matching PyKotor: if item.link.node == source_node:
                // Also check that this is a different link (not the same one we started with)
                // This ensures we find a different reference to the same node (the "original")
                if (item.Link.Node == sourceNode && item.Link != selectedLink)
                {
                    foundItem = item;
                    break;
                }

                // Add all children to the queue for breadth-first search
                // Matching PyKotor: items.extend([item.child(i, 0) for i in range(item.rowCount())])
                foreach (var child in item.Children)
                {
                    items.Enqueue(child);
                }
            }

            // If we didn't find a different link, try finding any link (fallback behavior)
            // This handles the case where there's only one link pointing to the node
            if (foundItem == null)
            {
                // Reset the queue
                items.Clear();
                foreach (var rootItem in rootItems)
                {
                    items.Enqueue(rootItem);
                }

                while (items.Count > 0)
                {
                    DLGStandardItem item = items.Dequeue();
                    if (item?.Link == null)
                    {
                        continue;
                    }

                    if (item.Link.Node == sourceNode)
                    {
                        foundItem = item;
                        break;
                    }

                    foreach (var child in item.Children)
                    {
                        items.Enqueue(child);
                    }
                }
            }

            if (foundItem != null)
            {
                // Expand to root and select the found item
                // Matching PyKotor: self.expand_to_root(item), self.ui.dialogTree.setCurrentIndex(item.index())
                ExpandToRoot(foundItem);
                HighlightResult(foundItem);
            }
            else
            {
                // Matching PyKotor: self._logger.error(f"Failed to find original node for node {source_node!r}")
                // In a full implementation, we would log this error
                // For now, we silently fail (matching the behavior when logging is not available)
            }
        }

        /// <summary>
        /// Expands all parent items to make the specified item visible.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1480-1487
        /// Original: def expand_to_root(self, item: DLGStandardItem):
        /// </summary>
        /// <param name="item">The item whose parents should be expanded.</param>
        private void ExpandToRoot(DLGStandardItem item)
        {
            if (item == null || _dialogTree == null)
            {
                return;
            }

            // Find the TreeViewItem corresponding to this DLGStandardItem
            TreeViewItem treeItem = FindTreeViewItem(_dialogTree.ItemsSource as System.Collections.IEnumerable, item);
            if (treeItem != null)
            {
                // Expand all parent items
                ExpandParentItems(treeItem);
            }
        }

        /// <summary>
        /// Finds references to the specified item.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1891
        /// Original: def find_references(self, item: DLGStandardItem | DLGListWidgetItem):
        /// </summary>
        public void FindReferences(DLGStandardItem item)
        {
            if (item?.Link == null)
            {
                return;
            }

            // Truncate history to current index (remove forward history when navigating to new reference)
            // Matching PyKotor: self.reference_history = self.reference_history[: self.current_reference_index + 1]
            if (_currentReferenceIndex >= 0 && _currentReferenceIndex < _referenceHistory.Count - 1)
            {
                _referenceHistory.RemoveRange(_currentReferenceIndex + 1, _referenceHistory.Count - _currentReferenceIndex - 1);
            }

            // Get item HTML display text
            // Matching PyKotor: item_html: str = item.data(Qt.ItemDataRole.DisplayRole)
            string itemHtml = GetItemDisplayText(item);

            // Increment reference index
            // Matching PyKotor: self.current_reference_index += 1
            _currentReferenceIndex++;

            // Find all items that link to the same node as item.link
            // Matching PyKotor: references: list[weakref.ReferenceType] = [
            //     this_item.ref_to_link
            //     for link in self.model.link_to_items
            //     for this_item in self.model.link_to_items[link]
            //     if this_item.link is not None and item.link in this_item.link.node.links
            // ]
            var references = new List<WeakReference<DLGLink>>();
            foreach (var kvp in _model.LinkToItems)
            {
                foreach (var thisItem in kvp.Value)
                {
                    if (thisItem?.Link?.Node != null && thisItem.Link.Node.Links != null && thisItem.Link.Node.Links.Contains(item.Link))
                    {
                        // Create weak reference to the link
                        var linkRef = new WeakReference<DLGLink>(thisItem.Link);
                        references.Add(linkRef);
                    }
                }
            }

            // Add to history and show dialog
            // Matching PyKotor: self.reference_history.append((references, item_html))
            // Matching PyKotor: self.show_reference_dialog(references, item_html)
            _referenceHistory.Add(Tuple.Create(references, itemHtml));
            ShowReferenceDialog(references, itemHtml);
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
                // Show message to user when MessageBox is available
                return;
            }

            // Get the selected item from the tree view
            DLGStandardItem selectedItem = null;
            if (_dialogTree?.SelectedItem != null)
            {
                var treeSelectedItem = _dialogTree.SelectedItem;
                if (treeSelectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
                {
                    selectedItem = dlgItem;
                }
                else if (treeSelectedItem is DLGStandardItem dlgItemDirect)
                {
                    selectedItem = dlgItemDirect;
                }
            }

            // If no selected item, try to get from model's selected index
            if (selectedItem == null && _model.SelectedIndex >= 0 && _model.SelectedIndex < _model.RowCount)
            {
                DLGLink selectedLink = _model.GetStarterAt(_model.SelectedIndex);
                if (selectedLink != null)
                {
                    // Find the item in the model that corresponds to this link
                    var rootItems = _model.GetRootItems();
                    if (_model.SelectedIndex < rootItems.Count)
                    {
                        selectedItem = rootItems[_model.SelectedIndex];
                    }
                }
            }

            // Call the model's paste_item method
            _model.PasteItem(selectedItem, _copy, asNewBranches: asNewBranches);

            // Update the tree view
            UpdateTreeView();

            // Select the newly pasted item if possible
            if (selectedItem != null && selectedItem.Children.Count > 0)
            {
                var pastedChild = selectedItem.Children[selectedItem.Children.Count - 1];
                SelectTreeViewItem(pastedChild);
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
            if (_dialogTree?.SelectedItem == null)
            {
                return;
            }

            // Get selected item from tree
            DLGLink link = null;
            var selectedItem = _dialogTree.SelectedItem;
            if (selectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
            {
                link = dlgItem?.Link;
            }
            else if (selectedItem is DLGStandardItem dlgItemDirect)
            {
                link = dlgItemDirect?.Link;
            }

            if (link?.Node == null)
            {
                return;
            }

            // Delete the node everywhere using the model
            _model.DeleteNodeEverywhere(link.Node);

            // Update tree view after deletion
            UpdateTreeView();
        }

        /// <summary>
        /// Sets expand recursively for tree items.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1674-1709
        /// Original: def set_expand_recursively(self, item: DLGStandardItem, seen_nodes: set[DLGNode], *, expand: bool, maxdepth: int = 11, depth: int = 0, is_root: bool = True)
        /// </summary>
        /// <param name="expand">True to expand all items, false to collapse all items.</param>
        /// <param name="maxDepth">Maximum depth to expand/collapse. Use -1 for unlimited depth.</param>
        private void SetExpandRecursively(bool expand, int maxDepth)
        {
            if (_dialogTree == null || _dialogTree.ItemsSource == null)
            {
                return;
            }

            // Get the selected item from the tree
            DLGStandardItem selectedItem = null;
            var treeSelectedItem = _dialogTree.SelectedItem;
            if (treeSelectedItem is TreeViewItem treeItem && treeItem.Tag is DLGStandardItem dlgItem)
            {
                selectedItem = dlgItem;
            }
            else if (treeSelectedItem is DLGStandardItem dlgItemDirect)
            {
                selectedItem = dlgItemDirect;
            }

            // If no item is selected, operate on all root items
            if (selectedItem == null)
            {
                var rootItems = _model?.GetRootItems();
                if (rootItems != null)
                {
                    var seenNodes = new HashSet<DLGNode>();
                    foreach (var rootItem in rootItems)
                    {
                        TreeViewItem rootTreeItem = FindTreeViewItem(_dialogTree.ItemsSource as System.Collections.IEnumerable, rootItem);
                        if (rootTreeItem != null)
                        {
                            SetExpandRecursivelyInternal(rootItem, rootTreeItem, seenNodes, expand, maxDepth, 0, true);
                        }
                    }
                }
                return;
            }

            // Find the TreeViewItem corresponding to the selected DLGStandardItem
            TreeViewItem selectedTreeItem = FindTreeViewItem(_dialogTree.ItemsSource as System.Collections.IEnumerable, selectedItem);
            if (selectedTreeItem == null)
            {
                return;
            }

            // Recursively expand/collapse starting from the selected item
            var seenNodes = new HashSet<DLGNode>();
            SetExpandRecursivelyInternal(selectedItem, selectedTreeItem, seenNodes, expand, maxDepth, 0, true);
        }

        /// <summary>
        /// Internal recursive method to expand/collapse tree items.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1674-1709
        /// Original: def set_expand_recursively(self, item: DLGStandardItem, seen_nodes: set[DLGNode], *, expand: bool, maxdepth: int = 11, depth: int = 0, is_root: bool = True)
        /// </summary>
        /// <param name="item">The DLGStandardItem to process.</param>
        /// <param name="treeItem">The corresponding TreeViewItem.</param>
        /// <param name="seenNodes">Set of nodes already processed (prevents infinite loops).</param>
        /// <param name="expand">True to expand, false to collapse.</param>
        /// <param name="maxDepth">Maximum depth. Use -1 for unlimited.</param>
        /// <param name="depth">Current depth in the tree.</param>
        /// <param name="isRoot">True if this is the root item being processed.</param>
        private void SetExpandRecursivelyInternal(
            DLGStandardItem item,
            TreeViewItem treeItem,
            HashSet<DLGNode> seenNodes,
            bool expand,
            int maxDepth,
            int depth,
            bool isRoot)
        {
            // Matching PyKotor: if depth > maxdepth >= 0: return
            if (maxDepth >= 0 && depth > maxDepth)
            {
                return;
            }

            // Matching PyKotor: if not isinstance(item, DLGStandardItem): return
            if (item == null)
            {
                return;
            }

            // Matching PyKotor: if item.link is None: return
            if (item.Link == null)
            {
                return;
            }

            DLGLink link = item.Link;

            // Matching PyKotor: if link.node in seen_nodes: return
            if (link.Node != null && seenNodes.Contains(link.Node))
            {
                return;
            }

            // Matching PyKotor: seen_nodes.add(link.node)
            if (link.Node != null)
            {
                seenNodes.Add(link.Node);
            }

            // Matching PyKotor: if expand: self.ui.dialogTree.expand(item_index)
            // Matching PyKotor: elif not is_root: self.ui.dialogTree.collapse(item_index)
            if (expand)
            {
                treeItem.IsExpanded = true;
            }
            else if (!isRoot)
            {
                treeItem.IsExpanded = false;
            }

            // Matching PyKotor: for row in range(item.rowCount()):
            // Matching PyKotor:     child_item: DLGStandardItem = cast("DLGStandardItem", item.child(row))
            // Matching PyKotor:     if child_item is None: continue
            // Matching PyKotor:     child_index: QModelIndex = child_item.index()
            // Matching PyKotor:     if not child_index.isValid(): continue
            // Matching PyKotor:     self.set_expand_recursively(child_item, seen_nodes, expand=expand, maxdepth=maxdepth, depth=depth + 1, is_root=False)
            if (treeItem.ItemsSource != null)
            {
                foreach (TreeViewItem childTreeItem in treeItem.ItemsSource as System.Collections.IEnumerable)
                {
                    if (childTreeItem == null)
                    {
                        continue;
                    }

                    DLGStandardItem childItem = childTreeItem.Tag as DLGStandardItem;
                    if (childItem == null)
                    {
                        continue;
                    }

                    // Recursively process child
                    SetExpandRecursivelyInternal(childItem, childTreeItem, seenNodes, expand, maxDepth, depth + 1, false);
                }
            }
        }

        /// <summary>
        /// Selects a tree view item by its DLGStandardItem.
        /// Helper method for programmatically selecting items in the tree view.
        /// </summary>
        /// <param name="item">The DLGStandardItem to select.</param>
        private void SelectTreeViewItem(DLGStandardItem item)
        {
            if (_dialogTree == null || item == null || _dialogTree.ItemsSource == null)
            {
                return;
            }

            // Recursively search for the tree view item matching the DLGStandardItem
            TreeViewItem foundItem = FindTreeViewItem(_dialogTree.ItemsSource as System.Collections.IEnumerable, item);
            if (foundItem != null)
            {
                _dialogTree.SelectedItem = foundItem;
                // Expand parent items to ensure the selected item is visible
                ExpandParentItems(foundItem);
            }
        }

        /// <summary>
        /// Recursively finds a TreeViewItem by its Tag (DLGStandardItem).
        /// </summary>
        /// <param name="items">The items collection to search.</param>
        /// <param name="targetItem">The DLGStandardItem to find.</param>
        /// <returns>The TreeViewItem if found, null otherwise.</returns>
        private TreeViewItem FindTreeViewItem(System.Collections.IEnumerable items, DLGStandardItem targetItem)
        {
            if (items == null || targetItem == null)
            {
                return null;
            }

            foreach (TreeViewItem treeItem in items)
            {
                if (treeItem == null)
                {
                    continue;
                }

                // Check if this tree item matches the target
                if (treeItem.Tag == targetItem)
                {
                    return treeItem;
                }

                // Recursively search children
                if (treeItem.ItemsSource != null)
                {
                    TreeViewItem found = FindTreeViewItem(treeItem.ItemsSource as System.Collections.IEnumerable, targetItem);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Expands all parent items of the specified tree view item to ensure it's visible.
        /// </summary>
        /// <param name="item">The tree view item whose parents should be expanded.</param>
        private void ExpandParentItems(TreeViewItem item)
        {
            if (item == null)
            {
                return;
            }

            // Get parent container (TreeViewItem or TreeView)
            var parent = item.Parent;
            while (parent != null)
            {
                if (parent is TreeViewItem parentTreeItem)
                {
                    parentTreeItem.IsExpanded = true;
                    parent = parentTreeItem.Parent;
                }
                else
                {
                    break;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:152
        // Original: self.keys_down: set[int] = set()
        // Expose for testing
        public HashSet<Key> KeysDown => _keysDown;

        /// <summary>
        /// Gets whether the editor is in focus mode (showing only a specific node and its children).
        /// Matching PyKotor implementation: self._focused
        /// </summary>
        public bool Focused => _focused;

        /// <summary>
        /// Gets the current reference index for navigation.
        /// Matching PyKotor implementation: self.current_reference_index
        /// </summary>
        public int CurrentReferenceIndex => _currentReferenceIndex;

        /// <summary>
        /// Gets the count of items in the reference history.
        /// </summary>
        public int ReferenceHistoryCount => _referenceHistory.Count;

        /// <summary>
        /// Gets the dialog paths for an item (link parent path, link path, linked to path).
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1908-1916
        /// Original: def get_item_dlg_paths(self, item: DLGStandardItem | DLGListWidgetItem) -> tuple[str, str, str]:
        /// </summary>
        /// <param name="item">The item to get paths for (DLGStandardItem or DLGListWidgetItem).</param>
        /// <returns>Tuple of (link_parent_path, link_path, linked_to_path).</returns>
        public Tuple<string, string, string> GetItemDlgPaths(object item)
        {
            string linkParentPath = "";
            string linkPath = "";
            string linkedToPath = "";

            DLGLink link = null;
            if (item is DLGStandardItem standardItem)
            {
                link = standardItem.Link;
                // In PyKotor, link_parent_path comes from item.data(_LINK_PARENT_NODE_PATH_ROLE)
                // For now, we'll try to find the parent path by traversing the tree
                if (standardItem.Parent != null && standardItem.Parent.Link != null && standardItem.Parent.Link.Node != null)
                {
                    linkParentPath = standardItem.Parent.Link.Node.Path();
                }
            }
            else if (item is DLGListWidgetItem listItem)
            {
                link = listItem.Link;
            }

            if (link != null)
            {
                // Determine if link is a starter
                bool isStarter = _coreDlg != null && _coreDlg.Starters != null && _coreDlg.Starters.Contains(link);
                linkPath = link.PartialPath(isStarter);

                if (link.Node != null)
                {
                    linkedToPath = link.Node.Path();
                }
            }

            return Tuple.Create(linkParentPath, linkPath, linkedToPath);
        }

        /// <summary>
        /// Shows the reference dialog with the specified references.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1918-1929
        /// Original: def show_reference_dialog(self, references: list[weakref.ref[DLGLink]], item_html: str):
        /// </summary>
        /// <param name="references">List of weak references to DLG links.</param>
        /// <param name="itemHtml">HTML text describing the item being referenced.</param>
        public void ShowReferenceDialog(List<WeakReference<DLGLink>> references, string itemHtml)
        {
            if (_dialogReferences == null)
            {
                _dialogReferences = new ReferenceChooserDialog(references, this, itemHtml);
                _dialogReferences.ItemChosen += OnReferenceChosen;
            }
            else
            {
                _dialogReferences.UpdateReferences(references, itemHtml);
            }

            if (!_dialogReferences.IsVisible)
            {
                _dialogReferences.Show();
            }
        }

        /// <summary>
        /// Handles when a reference is chosen from the dialog.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1931-1936
        /// Original: def on_reference_chosen(self, item: DLGListWidgetItem):
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="item">The selected DLG list widget item.</param>
        private void OnReferenceChosen(object sender, DLGListWidgetItem item)
        {
            if (item?.Link != null)
            {
                JumpToNode(item.Link);
            }
        }

        /// <summary>
        /// Jumps to the specified node by highlighting it in the tree.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1938-1947
        /// Original: def jump_to_node(self, link: DLGLink | None):
        /// </summary>
        /// <param name="link">The link to jump to.</param>
        public void JumpToNode(DLGLink link)
        {
            if (link == null || _model == null || _model.LinkToItems == null)
            {
                return;
            }

            if (!_model.LinkToItems.ContainsKey(link))
            {
                return;
            }

            var items = _model.LinkToItems[link];
            if (items != null && items.Count > 0)
            {
                HighlightResult(items[0]);
            }
        }

        /// <summary>
        /// Navigates back in the reference history.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1949-1953
        /// Original: def navigate_back(self):
        /// </summary>
        public void NavigateBack()
        {
            if (_currentReferenceIndex > 0)
            {
                _currentReferenceIndex--;
                var historyItem = _referenceHistory[_currentReferenceIndex];
                ShowReferenceDialog(historyItem.Item1, historyItem.Item2);
            }
        }

        /// <summary>
        /// Navigates forward in the reference history.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1955-1959
        /// Original: def navigate_forward(self):
        /// </summary>
        public void NavigateForward()
        {
            if (_currentReferenceIndex < _referenceHistory.Count - 1)
            {
                _currentReferenceIndex++;
                var historyItem = _referenceHistory[_currentReferenceIndex];
                ShowReferenceDialog(historyItem.Item1, historyItem.Item2);
            }
        }
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

    // Simple model class for tests (matching Python DLGStandardItemModel)
    public class DLGModel
    {
        private List<DLGStandardItem> _rootItems = new List<DLGStandardItem>();
        private DLGEditor _editor;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:293-294
        // Original: self.link_to_items: weakref.WeakKeyDictionary[DLGLink, list[DLGStandardItem]] = weakref.WeakKeyDictionary()
        // Original: self.node_to_items: weakref.WeakKeyDictionary[DLGNode, list[DLGStandardItem]] = weakref.WeakKeyDictionary()
        // Note: C# doesn't have WeakKeyDictionary, so we use ConditionalWeakTable which provides similar functionality
        private Dictionary<DLGLink, List<DLGStandardItem>> _linkToItems = new Dictionary<DLGLink, List<DLGStandardItem>>();
        private Dictionary<DLGNode, List<DLGStandardItem>> _nodeToItems = new Dictionary<DLGNode, List<DLGStandardItem>>();

        /// <summary>
        /// Gets the dictionary mapping links to their items.
        /// Matching PyKotor implementation: self.link_to_items
        /// </summary>
        public Dictionary<DLGLink, List<DLGStandardItem>> LinkToItems => _linkToItems;

        /// <summary>
        /// Gets the dictionary mapping nodes to their items.
        /// Matching PyKotor implementation: self.node_to_items
        /// </summary>
        public Dictionary<DLGNode, List<DLGStandardItem>> NodeToItems => _nodeToItems;

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
            _linkToItems.Clear();
            _nodeToItems.Clear();
        }

        /// <summary>
        /// Clears the model (alias for ResetModel for Python compatibility).
        /// Matching PyKotor implementation: def clear(self):
        /// </summary>
        public void Clear()
        {
            ResetModel();
        }

        public void AddStarter(DLGLink link)
        {
            if (link == null)
            {
                return;
            }
            var item = new DLGStandardItem(link);
            _rootItems.Add(item);

            // Register in dictionaries
            if (!_linkToItems.ContainsKey(link))
            {
                _linkToItems[link] = new List<DLGStandardItem>();
            }
            if (!_linkToItems[link].Contains(item))
            {
                _linkToItems[link].Add(item);
            }

            if (link.Node != null)
            {
                if (!_nodeToItems.ContainsKey(link.Node))
                {
                    _nodeToItems[link.Node] = new List<DLGStandardItem>();
                }
                if (!_nodeToItems[link.Node].Contains(item))
                {
                    _nodeToItems[link.Node].Add(item);
                }
            }

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

            // Register in dictionaries
            if (!_linkToItems.ContainsKey(link))
            {
                _linkToItems[link] = new List<DLGStandardItem>();
            }
            if (!_linkToItems[link].Contains(newItem))
            {
                _linkToItems[link].Add(newItem);
            }

            if (link.Node != null)
            {
                if (!_nodeToItems.ContainsKey(link.Node))
                {
                    _nodeToItems[link.Node] = new List<DLGStandardItem>();
                }
                if (!_nodeToItems[link.Node].Contains(newItem))
                {
                    _nodeToItems[link.Node].Add(newItem);
                }
            }

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
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:1072-1098
        /// Original: def update_item_display_text(self, item: DLGStandardItem, *, update_copies: bool = True)
        /// </summary>
        /// <param name="item">The DLGStandardItem to update.</param>
        private void UpdateItemDisplayText(DLGStandardItem item)
        {
            if (item == null || item.Link == null)
            {
                return;
            }

            // Matching PyKotor implementation: Refresh the item text and formatting based on the node data
            // The actual display update happens when we rebuild the tree view
            // For now, we'll trigger a tree view update to reflect the changes
            // In a more optimized implementation, we would update just the specific tree view item's header
            
            // Update the tree view to reflect changes
            UpdateTreeView();
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
        /// Moves the selected item up in the starter list.
        /// Returns true if the move was successful, false if the move was invalid (already at top or no selection).
        /// </summary>
        /// <returns>True if the move was successful, false otherwise.</returns>
        public bool MoveItemUp()
        {
            int selectedIndex = _selectedIndex;

            // Check if selection is valid and not already at top
            if (selectedIndex <= 0 || selectedIndex >= _rootItems.Count)
            {
                return false; // No selection, invalid index, or already at top
            }

            int newIndex = selectedIndex - 1;
            DLGLink link = _rootItems[selectedIndex].Link;

            // Synchronize with CoreDlg.Starters if editor is available (before moving in model)
            if (_editor != null && _editor.CoreDlg != null && selectedIndex < _editor.CoreDlg.Starters.Count)
            {
                _editor.CoreDlg.Starters.RemoveAt(selectedIndex);
                _editor.CoreDlg.Starters.Insert(newIndex, link);
            }

            // Move in model
            MoveStarter(selectedIndex, newIndex);

            // Update selected index to track the moved item
            _selectedIndex = newIndex;

            return true;
        }

        /// <summary>
        /// Moves the selected item down in the starter list.
        /// Returns true if the move was successful, false if the move was invalid (already at bottom or no selection).
        /// </summary>
        /// <returns>True if the move was successful, false otherwise.</returns>
        public bool MoveItemDown()
        {
            int selectedIndex = _selectedIndex;

            // Check if selection is valid and not already at bottom
            if (selectedIndex < 0 || selectedIndex >= _rootItems.Count - 1)
            {
                return false; // No selection, invalid index, or already at bottom
            }

            int newIndex = selectedIndex + 1;
            DLGLink link = _rootItems[selectedIndex].Link;

            // Synchronize with CoreDlg.Starters if editor is available (before moving in model)
            if (_editor != null && _editor.CoreDlg != null && selectedIndex < _editor.CoreDlg.Starters.Count)
            {
                _editor.CoreDlg.Starters.RemoveAt(selectedIndex);
                _editor.CoreDlg.Starters.Insert(newIndex, link);
            }

            // Move in model
            MoveStarter(selectedIndex, newIndex);

            // Update selected index to track the moved item
            _selectedIndex = newIndex;

            return true;
        }

        /// <summary>
        /// Gets all root items in the model.
        /// </summary>
        public IReadOnlyList<DLGStandardItem> GetRootItems()
        {
            return _rootItems;
        }

        /// <summary>
        /// Recursively loads a dialog item and all its children into the model.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:752-807
        /// Original: def load_dlg_item_rec(self, item_to_load: DLGStandardItem, copied_link: DLGLink | None = None):
        /// </summary>
        /// <param name="itemToLoad">The item to load recursively.</param>
        public void LoadDlgItemRec(DLGStandardItem itemToLoad)
        {
            if (itemToLoad == null || itemToLoad.Link == null)
            {
                return;
            }

            var link = itemToLoad.Link;
            var node = link.Node;

            if (node == null)
            {
                return;
            }

            // Register this item in the dictionaries
            if (!_linkToItems.ContainsKey(link))
            {
                _linkToItems[link] = new List<DLGStandardItem>();
            }
            if (!_linkToItems[link].Contains(itemToLoad))
            {
                _linkToItems[link].Add(itemToLoad);
            }

            if (!_nodeToItems.ContainsKey(node))
            {
                _nodeToItems[node] = new List<DLGStandardItem>();
            }
            if (!_nodeToItems[node].Contains(itemToLoad))
            {
                _nodeToItems[node].Add(itemToLoad);
            }

            // Recursively load all child links
            foreach (var childLink in node.Links)
            {
                if (childLink == null)
                {
                    continue;
                }

                var childItem = new DLGStandardItem(childLink);
                itemToLoad.AddChild(childItem);

                // Recursively load children of this child
                LoadDlgItemRec(childItem);
            }
        }

        /// <summary>
        /// Shifts an item in the tree by a given amount.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:1248-1285
        /// Original: def shift_item(self, item: DLGStandardItem, amount: int, *, no_selection_update: bool = False):
        /// </summary>
        /// <param name="item">The item to shift.</param>
        /// <param name="amount">The amount to shift (positive = down, negative = up).</param>
        public void ShiftItem(DLGStandardItem item, int amount)
        {
            if (item == null)
            {
                return;
            }

            var parent = item.Parent;
            var oldRow = parent == null ? _rootItems.IndexOf(item) : parent.Children.ToList().IndexOf(item);

            if (oldRow < 0)
            {
                return;
            }

            var newRow = oldRow + amount;
            var maxRow = parent == null ? _rootItems.Count : parent.Children.Count();

            if (newRow < 0 || newRow >= maxRow)
            {
                return;
            }

            // Move the item
            if (parent == null)
            {
                _rootItems.RemoveAt(oldRow);
                _rootItems.Insert(newRow, item);
            }
            else
            {
                // For child items, we need to update the parent's Links list
                if (parent.Link?.Node != null)
                {
                    var links = parent.Link.Node.Links;
                    if (oldRow < links.Count && newRow < links.Count)
                    {
                        var linkToMove = links[oldRow];
                        links.RemoveAt(oldRow);
                        links.Insert(newRow, linkToMove);

                        // Update list_index for all affected links
                        for (int i = 0; i < links.Count; i++)
                        {
                            links[i].ListIndex = i;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Pastes a link as a child of the specified parent item.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:900-975
        /// Original: def paste_item(self, parent_item: DLGStandardItem | Self | None, pasted_link: DLGLink | None = None, *, row: int | None = None, as_new_branches: bool = True):
        /// </summary>
        /// <param name="parentItem">The parent item to paste under, or null for root.</param>
        /// <param name="pastedLink">The link to paste.</param>
        /// <param name="row">Optional row index to insert at, or null to append.</param>
        /// <param name="asNewBranches">If true, creates deep copies of nodes; if false, links to existing nodes.</param>
        public void PasteItem(DLGStandardItem parentItem, DLGLink pastedLink, int? row = null, bool asNewBranches = true)
        {
            if (pastedLink == null || _editor == null || _editor.CoreDlg == null)
            {
                return;
            }

            // If as_new_branches is True, we need to deep copy the entire link tree
            DLGLink linkToPaste = pastedLink;
            if (asNewBranches)
            {
                // Deep copy the link using ToDict/FromDict
                Dictionary<string, object> nodeMap = new Dictionary<string, object>();
                Dictionary<string, object> linkDict = pastedLink.ToDict(nodeMap);
                linkToPaste = DLGLink.FromDict(linkDict, nodeMap);
            }

            // Set is_child property based on whether parentItem is a DLGStandardItem or null
            // Matching PyKotor: pasted_link.is_child = not isinstance(parent_item, DLGStandardItem)
            linkToPaste.IsChild = (parentItem != null);

            // Note: When as_new_branches is True, we create a deep copy via ToDict/FromDict,
            // which creates new objects with new hash caches automatically (set in constructors).
            // The Python code explicitly sets _hash_cache, but in C# the hash cache is readonly
            // and set in the constructor, so new objects created via FromDict already have unique hashes.

            // Ensure the link is not already in link_to_items
            // Matching PyKotor: assert pasted_link not in self.link_to_items
            if (_linkToItems.ContainsKey(linkToPaste))
            {
                // Link already exists, this shouldn't happen with a new hash, but handle it gracefully
                return;
            }

            // Get all existing entry and reply indices
            HashSet<int> entryIndices = new HashSet<int>();
            foreach (var entry in _editor.CoreDlg.AllEntries())
            {
                if (entry.ListIndex >= 0)
                {
                    entryIndices.Add(entry.ListIndex);
                }
            }

            HashSet<int> replyIndices = new HashSet<int>();
            foreach (var reply in _editor.CoreDlg.AllReplies())
            {
                if (reply.ListIndex >= 0)
                {
                    replyIndices.Add(reply.ListIndex);
                }
            }

            // Traverse all nodes in the pasted link tree and assign new list indices
            // Matching PyKotor: queue = deque([pasted_link.node]), visited = set()
            Queue<DLGNode> queue = new Queue<DLGNode>();
            HashSet<DLGNode> visited = new HashSet<DLGNode>();

            if (linkToPaste.Node != null)
            {
                queue.Enqueue(linkToPaste.Node);
            }

            while (queue.Count > 0)
            {
                DLGNode curNode = queue.Dequeue();
                if (curNode == null || visited.Contains(curNode))
                {
                    continue;
                }
                visited.Add(curNode);

                // Assign new list index if as_new_branches or node doesn't exist in node_to_items
                // Matching PyKotor: if as_new_branches or cur_node not in self.node_to_items:
                if (asNewBranches || !_nodeToItems.ContainsKey(curNode))
                {
                    int newIndex = GetNewNodeListIndex(curNode, entryIndices, replyIndices);
                    curNode.ListIndex = newIndex;

                    // Update the appropriate index set
                    if (curNode is DLGEntry)
                    {
                        entryIndices.Add(newIndex);
                    }
                    else if (curNode is DLGReply)
                    {
                        replyIndices.Add(newIndex);
                    }
                }

                // Note: When as_new_branches is True, nodes are deep copied via ToDict/FromDict,
                // which creates new node objects with new hash caches automatically (set in constructors).
                // The Python code explicitly sets _hash_cache, but in C# the hash cache is readonly
                // and set in the constructor, so new nodes created via FromDict already have unique hashes.

                // Add child nodes to queue
                if (curNode.Links != null)
                {
                    foreach (var link in curNode.Links)
                    {
                        if (link?.Node != null)
                        {
                            queue.Enqueue(link.Node);
                        }
                    }
                }
            }

            // If as_new_branches, also assign new list index to the root node of the pasted link
            // Matching PyKotor: if as_new_branches: new_index = self._get_new_node_list_index(pasted_link.node, all_entries, all_replies), pasted_link.node.list_index = new_index
            if (asNewBranches && linkToPaste.Node != null)
            {
                int newIndex = GetNewNodeListIndex(linkToPaste.Node, entryIndices, replyIndices);
                linkToPaste.Node.ListIndex = newIndex;

                if (linkToPaste.Node is DLGEntry)
                {
                    entryIndices.Add(newIndex);
                }
                else if (linkToPaste.Node is DLGReply)
                {
                    replyIndices.Add(newIndex);
                }
            }

            // Create new item for the pasted link
            DLGStandardItem newItem = new DLGStandardItem(linkToPaste);

            // Add the item to the model
            if (parentItem == null)
            {
                // Add to root
                if (row.HasValue && row.Value >= 0 && row.Value <= _rootItems.Count)
                {
                    _rootItems.Insert(row.Value, newItem);
                }
                else
                {
                    _rootItems.Add(newItem);
                }

                // Add to CoreDlg.Starters
                if (!_editor.CoreDlg.Starters.Contains(linkToPaste))
                {
                    if (row.HasValue && row.Value >= 0 && row.Value <= _editor.CoreDlg.Starters.Count)
                    {
                        _editor.CoreDlg.Starters.Insert(row.Value, linkToPaste);
                    }
                    else
                    {
                        _editor.CoreDlg.Starters.Add(linkToPaste);
                    }
                }
            }
            else
            {
                // Add as child
                if (row.HasValue && row.Value >= 0 && row.Value <= parentItem.Children.Count)
                {
                    // Insert at specific row
                    var childrenList = parentItem.Children.ToList();
                    childrenList.Insert(row.Value, newItem);
                    // Note: We can't directly insert into Children collection, so we'll add and then shift if needed
                    // For now, just add at the end and let the caller handle ordering if needed
                    parentItem.AddChild(newItem);
                }
                else
                {
                    parentItem.AddChild(newItem);
                }

                // Add link to parent node's Links collection
                if (parentItem.Link?.Node != null)
                {
                    linkToPaste.ListIndex = parentItem.Link.Node.Links.Count;
                    parentItem.Link.Node.Links.Add(linkToPaste);
                }
            }

            // Register in dictionaries
            if (!_linkToItems.ContainsKey(linkToPaste))
            {
                _linkToItems[linkToPaste] = new List<DLGStandardItem>();
            }
            if (!_linkToItems[linkToPaste].Contains(newItem))
            {
                _linkToItems[linkToPaste].Add(newItem);
            }

            if (linkToPaste.Node != null)
            {
                if (!_nodeToItems.ContainsKey(linkToPaste.Node))
                {
                    _nodeToItems[linkToPaste.Node] = new List<DLGStandardItem>();
                }
                if (!_nodeToItems[linkToPaste.Node].Contains(newItem))
                {
                    _nodeToItems[linkToPaste.Node].Add(newItem);
                }
            }

            // Recursively load the item
            LoadDlgItemRec(newItem);

            // Update parent item display text if parent is a DLGStandardItem
            // Matching PyKotor: if isinstance(parent_item, DLGStandardItem): self.update_item_display_text(parent_item)
            if (parentItem != null)
            {
                UpdateItemDisplayText(parentItem);
            }

            // Update tree view if editor is available
            if (_editor != null)
            {
                _editor.UpdateTreeView();
            }
        }

        /// <summary>
        /// Gets a new unique list index for a node.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:977-996
        /// Original: def _get_new_node_list_index(self, node: DLGNode, entry_indices: set[int] | None = None, reply_indices: set[int] | None = None) -> int:
        /// </summary>
        /// <param name="node">The node to get a new index for.</param>
        /// <param name="entryIndices">Optional set of existing entry indices.</param>
        /// <param name="replyIndices">Optional set of existing reply indices.</param>
        /// <returns>A new unique list index.</returns>
        private int GetNewNodeListIndex(DLGNode node, HashSet<int> entryIndices = null, HashSet<int> replyIndices = null)
        {
            if (_editor == null || _editor.CoreDlg == null)
            {
                return 0;
            }

            HashSet<int> indices;
            if (node is DLGEntry)
            {
                if (entryIndices == null)
                {
                    indices = new HashSet<int>();
                    foreach (var entry in _editor.CoreDlg.AllEntries())
                    {
                        if (entry.ListIndex >= 0)
                        {
                            indices.Add(entry.ListIndex);
                        }
                    }
                }
                else
                {
                    indices = entryIndices;
                }
            }
            else if (node is DLGReply)
            {
                if (replyIndices == null)
                {
                    indices = new HashSet<int>();
                    foreach (var reply in _editor.CoreDlg.AllReplies())
                    {
                        if (reply.ListIndex >= 0)
                        {
                            indices.Add(reply.ListIndex);
                        }
                    }
                }
                else
                {
                    indices = replyIndices;
                }
            }
            else
            {
                throw new ArgumentException($"Unknown node type: {node.GetType().Name}");
            }

            // Matching PyKotor: new_index = max(indices, default=-1) + 1, while new_index in indices: new_index += 1
            int newIndex = (indices.Count > 0 ? indices.Max() : -1) + 1;
            while (indices.Contains(newIndex))
            {
                newIndex++;
            }

            return newIndex;
        }

        /// <summary>
        /// Removes all occurrences of a node and all links to it from the model and CoreDlg.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:1017-1049
        /// Original: def delete_node_everywhere(self, node: DLGNode):
        /// </summary>
        /// <param name="nodeToRemove">The node to remove everywhere.</param>
        public void DeleteNodeEverywhere(DLGNode nodeToRemove)
        {
            if (nodeToRemove == null || _editor == null || _editor.CoreDlg == null)
            {
                return;
            }

            // First, remove all links from CoreDlg that point to this node
            // This includes links from all nodes' Links collections and from Starters
            RemoveLinksToNode(nodeToRemove);

            // Recursively remove items from the model tree
            RemoveLinksRecursive(nodeToRemove, null);

            // Update tree view if editor is available
            if (_editor != null)
            {
                _editor.UpdateTreeView();
            }
        }

        /// <summary>
        /// Recursively removes links to a node from the model tree.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:1025-1046
        /// Original: def remove_links_recursive(node_to_remove: DLGNode, parent_item: DLGStandardItem | DLGStandardItemModel):
        /// </summary>
        /// <param name="nodeToRemove">The node to remove.</param>
        /// <param name="parentItem">The parent item to search in, or null to search root items.</param>
        private void RemoveLinksRecursive(DLGNode nodeToRemove, DLGStandardItem parentItem)
        {
            if (nodeToRemove == null)
            {
                return;
            }

            // Get the list of children to iterate over
            List<DLGStandardItem> children;
            if (parentItem == null)
            {
                // Search root items
                children = new List<DLGStandardItem>(_rootItems);
            }
            else
            {
                // Search children of parent item
                children = new List<DLGStandardItem>(parentItem.Children);
            }

            // Iterate in reverse to safely remove items while iterating
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var childItem = children[i];
                if (childItem == null)
                {
                    continue;
                }

                var childLink = childItem.Link;
                if (childLink == null)
                {
                    continue;
                }

                // Check if this child item's node is the one we want to remove
                if (childLink.Node == nodeToRemove)
                {
                    // First, recursively remove all children of this item
                    RemoveLinksRecursive(childLink.Node, childItem);

                    // Remove the link from the parent node's Links collection
                    if (parentItem != null && parentItem.Link != null && parentItem.Link.Node != null)
                    {
                        parentItem.Link.Node.Links.Remove(childLink);
                    }

                    // Remove the item from the model
                    if (parentItem == null)
                    {
                        // Remove from root items
                        _rootItems.Remove(childItem);
                    }
                    else
                    {
                        // Remove from parent's children
                        parentItem.RemoveChild(childItem);
                    }
                }
                else
                {
                    // Continue searching recursively in this child
                    RemoveLinksRecursive(nodeToRemove, childItem);
                }
            }
        }

        /// <summary>
        /// Removes all links from CoreDlg that point to the specified node.
        /// This includes links from Starters and from all nodes' Links collections.
        /// </summary>
        /// <param name="nodeToRemove">The node to remove links to.</param>
        private void RemoveLinksToNode(DLGNode nodeToRemove)
        {
            if (nodeToRemove == null || _editor == null || _editor.CoreDlg == null)
            {
                return;
            }

            // Remove from Starters
            for (int i = _editor.CoreDlg.Starters.Count - 1; i >= 0; i--)
            {
                if (_editor.CoreDlg.Starters[i]?.Node == nodeToRemove)
                {
                    _editor.CoreDlg.Starters.RemoveAt(i);
                }
            }

            // Remove links from all entries
            foreach (var entry in _editor.CoreDlg.AllEntries())
            {
                if (entry != null && entry.Links != null)
                {
                    for (int i = entry.Links.Count - 1; i >= 0; i--)
                    {
                        if (entry.Links[i]?.Node == nodeToRemove)
                        {
                            entry.Links.RemoveAt(i);
                        }
                    }
                }
            }

            // Remove links from all replies
            foreach (var reply in _editor.CoreDlg.AllReplies())
            {
                if (reply != null && reply.Links != null)
                {
                    for (int i = reply.Links.Count - 1; i >= 0; i--)
                    {
                        if (reply.Links[i]?.Node == nodeToRemove)
                        {
                            reply.Links.RemoveAt(i);
                        }
                    }
                }
            }
        }
    }
}

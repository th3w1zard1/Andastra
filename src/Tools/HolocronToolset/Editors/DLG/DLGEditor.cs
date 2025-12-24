using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics.CNV;
using Andastra.Parsing.Resource.Generics.DLG;
using DLGType = Andastra.Parsing.Resource.Generics.DLG.DLG;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Editors.Actions;
using Avalonia.Platform.Storage;

namespace HolocronToolset.Editors.DLG
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

        /// <summary>
        /// Sets the copy link (for internal use and testing).
        /// </summary>
        internal void SetCopyLink(DLGLink link)
        {
            _copy = link;
        }

        /// <summary>
        /// Gets the copy link (for testing).
        /// </summary>
        public DLGLink GetCopyLink()
        {
            return _copy;
        }

        // UI Controls - Animations
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui:966-992
        // Original: QListWidget animsList, QPushButton addAnimButton, removeAnimButton, editAnimButton
        private ListBox _animsList;
        private Button _addAnimButton;
        private Button _removeAnimButton;
        private Button _editAnimButton;
        private ListBox _stuntList;

        // Public property for testing
        public ListBox StuntList => _stuntList;

        // Public properties for menu actions (for testing)
        public MenuItem ActionReloadTree => _actionReloadTree;
        private TextBox _commentsEdit;
        private Panel _leftDockWidget;
        private DLGListWidget _orphanedNodesList;
        private DLGListWidget _pinnedItemsList;

        // Menu Actions - matching PyKotor UI actions
        // File menu actions
        private MenuItem _actionNew;
        private MenuItem _actionOpen;
        private MenuItem _actionSave;
        private MenuItem _actionSaveAs;
        private MenuItem _actionRevert;
        private MenuItem _actionExit;

        // Tools menu actions
        private MenuItem _actionReloadTree;
        private MenuItem _actionUnfocus;

        private int _currentResultIndex = 0;
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:122-123
        // Original: self.search_results: list[DLGStandardItem] = [], self.current_search_text: str = ""
        private List<DLGStandardItem> _searchResults = new List<DLGStandardItem>();
        private string _currentSearchText = "";

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

        // Condition parameter widgets (K2-specific, but available in UI for all games)
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QSpinBox condition1Param1Spin, condition1Param2Spin, etc., QCheckBox condition1NotCheckbox, condition2NotCheckbox
        private NumericUpDown _condition1Param1Spin;
        private NumericUpDown _condition1Param2Spin;
        private NumericUpDown _condition1Param3Spin;
        private NumericUpDown _condition1Param4Spin;
        private NumericUpDown _condition1Param5Spin;
        private TextBox _condition1Param6Edit;
        private CheckBox _condition1NotCheckbox;
        private NumericUpDown _condition2Param1Spin;
        private NumericUpDown _condition2Param2Spin;
        private NumericUpDown _condition2Param3Spin;
        private NumericUpDown _condition2Param4Spin;
        private NumericUpDown _condition2Param5Spin;
        private TextBox _condition2Param6Edit;
        private CheckBox _condition2NotCheckbox;

        // UI Controls - Node widgets (Quest/Plot)
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QLineEdit questEdit, QSpinBox questEntrySpin, QComboBox plotIndexCombo, QDoubleSpinBox plotXpSpin
        private TextBox _questEdit;
        private NumericUpDown _questEntrySpin;
        private ComboBox _plotIndexCombo;
        private NumericUpDown _plotXpSpin;

        // UI Controls - Speaker widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QLineEdit speakerEdit, QLabel speakerEditLabel
        private TextBox _speakerEdit;
        private TextBlock _speakerEditLabel;

        // UI Controls - Listener widget
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QLineEdit listenerEdit
        private TextBox _listenerEdit;

        // UI Controls - Script widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QComboBox script1ResrefEdit, script2ResrefEdit
        private ComboBox _script1ResrefEdit;
        private ComboBox _script2ResrefEdit;
        private NumericUpDown _script1Param1Spin;
        private StackPanel _script1Param1Panel;
        private NumericUpDown _waitFlagSpin;
        private NumericUpDown _fadeTypeSpin;
        private ComboBox _soundComboBox;
        private ComboBox _voiceComboBox;

        // UI Controls - Node timing widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QSpinBox delaySpin, waitFlagSpin, fadeTypeSpin
        private NumericUpDown _delaySpin;
        // Original: QLineEdit voIdEdit (row 4, column 1 in file properties grid)
        private TextBox _voIdEdit;

        // UI Controls - Camera widgets
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui:1112-1151
        // Original: QSpinBox cameraIdSpin, cameraAnimSpin, QComboBox cameraAngleSelect, cameraEffectSelect
        private NumericUpDown _cameraIdSpin;
        private NumericUpDown _cameraAnimSpin;
        private NumericUpDown _nodeIdSpin;
        private NumericUpDown _alienRaceNodeSpin;
        private NumericUpDown _postProcSpin;
        private ComboBox _cameraAngleSelect;
        private ComboBox _cameraEffectSelect;
        private ComboBox _emotionSelect;
        private ComboBox _expressionSelect;
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QComboBox ambientTrackCombo
        private ComboBox _ambientTrackCombo;

        // UI Controls - File-level checkboxes
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QCheckBox unequipHandsCheckbox, unequipAllCheckbox, skippableCheckbox, animatedCutCheckbox, oldHitCheckbox
        private CheckBox _unequipHandsCheckbox;
        private CheckBox _unequipAllCheckbox;
        private CheckBox _skippableCheckbox;
        private CheckBox _animatedCutCheckbox;
        private CheckBox _oldHitCheckbox;
        private CheckBox _soundCheckbox;
        private CheckBox _nodeUnskippableCheckbox;

        // UI Controls - File-level properties (conversation type, computer type, delays, scripts, camera)
        // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
        // Original: QComboBox conversationSelect, computerSelect, onAbortCombo, onEndEdit, ambientTrackCombo, cameraModelSelect
        // Original: QSpinBox entryDelaySpin, replyDelaySpin
        private ComboBox _conversationSelect;
        private ComboBox _computerSelect;
        private NumericUpDown _entryDelaySpin;
        private NumericUpDown _replyDelaySpin;
        private ComboBox _onAbortCombo;
        private ComboBox _onEndEdit;
        private ComboBox _cameraModelSelect;

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
            else
            {
                // Even when XAML loads successfully, ensure camera widgets are initialized for testing
                InitializeCameraWidgets();
            }
        }

        private void InitializeCameraWidgets()
        {
            // Initialize camera widgets
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui:1112-1151
            // Original: QSpinBox cameraIdSpin, cameraAnimSpin, QComboBox cameraAngleSelect, cameraEffectSelect
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2524-2527
            // Original: item.link.node.camera_id = self.ui.cameraIdSpin.value(), item.link.node.camera_anim = self.ui.cameraAnimSpin.value(), item.link.node.camera_angle = self.ui.cameraAngleSelect.currentIndex(), item.link.node.camera_effect = self.ui.cameraEffectSelect.currentIndex()
            _cameraIdSpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = -1 };
            _cameraIdSpin.ValueChanged += (s, e) => OnNodeUpdate();

            _cameraAnimSpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _cameraAnimSpin.ValueChanged += (s, e) => OnNodeUpdate();

            _cameraAngleSelect = new ComboBox();
            // Camera angle options: 0-7 (matching Python implementation)
            // 0 = Default, 1-6 = Various angles, 7 = Custom
            _cameraAngleSelect.Items.Add("Default (0)");
            _cameraAngleSelect.Items.Add("Angle 1");
            _cameraAngleSelect.Items.Add("Angle 2");
            _cameraAngleSelect.Items.Add("Angle 3");
            _cameraAngleSelect.Items.Add("Angle 4");
            _cameraAngleSelect.Items.Add("Angle 5");
            _cameraAngleSelect.Items.Add("Angle 6");
            _cameraAngleSelect.Items.Add("Custom (7)");
            _cameraAngleSelect.SelectedIndex = 0;
            _cameraAngleSelect.SelectionChanged += (s, e) => OnNodeUpdate();

            _cameraEffectSelect = new ComboBox();
            // Camera effect options (matching Python implementation)
            _cameraEffectSelect.Items.Add("None (0)");
            _cameraEffectSelect.Items.Add("Effect 1");
            _cameraEffectSelect.Items.Add("Effect 2");
            _cameraEffectSelect.Items.Add("Effect 3");
            _cameraEffectSelect.SelectedIndex = 0;
            _cameraEffectSelect.SelectionChanged += (s, e) => OnNodeUpdate();

            // Original: QComboBox emotionSelect (KotOR 2)
            _emotionSelect = new ComboBox();
            _emotionSelect.Items.Add("0 (None)");
            _emotionSelect.Items.Add("1 (Happy)");
            _emotionSelect.Items.Add("2 (Sad)");
            _emotionSelect.Items.Add("3 (Angry)");
            _emotionSelect.Items.Add("4 (Surprised)");
            _emotionSelect.Items.Add("5 (Fear)");
            _emotionSelect.Items.Add("6 (Disgust)");
            _emotionSelect.Items.Add("7 (Neutral)");
            _emotionSelect.SelectedIndex = 0;
            _emotionSelect.SelectionChanged += (s, e) => OnNodeUpdate();

            // Original: QComboBox expressionSelect (KotOR 2)
            _expressionSelect = new ComboBox();
            _expressionSelect.Items.Add("0 (None)");
            _expressionSelect.Items.Add("1 (Smile)");
            _expressionSelect.Items.Add("2 (Frown)");
            _expressionSelect.Items.Add("3 (Scowl)");
            _expressionSelect.Items.Add("4 (Shock)");
            _expressionSelect.Items.Add("5 (Terror)");
            _expressionSelect.Items.Add("6 (Wince)");
            _expressionSelect.Items.Add("7 (Blink)");
            _expressionSelect.SelectedIndex = 0;
            _expressionSelect.SelectionChanged += (s, e) => OnNodeUpdate();
        }

        private void SetupUI()
        {
            // Create main dock panel to support menu bar
            var dockPanel = new DockPanel();
            Content = dockPanel;

            // Setup menu bar - matching PyKotor implementation
            SetupMenuBar(dockPanel);

            // Create main content panel
            var panel = new StackPanel();
            dockPanel.Children.Add(panel);
            DockPanel.SetDock(panel, Dock.Bottom);

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:436-467
            // Original: def setup_extra_widgets(self):
            // Find bar - must be added first (before dialog tree) to match PyKotor layout
            // Matching PyKotor: self.ui.verticalLayout_main.insertWidget(0, self.find_bar)
            SetupFindBar();
            if (_findBar != null)
            {
                panel.Children.Insert(0, _findBar);
            }

            // Initialize file-level properties (root DLG fields)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui:1587-1603
            // Original: QLineEdit voIdEdit (row 4, column 1 in file properties grid)
            _voIdEdit = new TextBox();
            _voIdEdit.LostFocus += (s, e) => OnFilePropertyChanged();
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QComboBox ambientTrackCombo
            _ambientTrackCombo = new ComboBox { IsEditable = true };
            _ambientTrackCombo.LostFocus += (s, e) => OnFilePropertyChanged();
            var filePropertiesPanel = new StackPanel();
            filePropertiesPanel.Children.Add(new TextBlock { Text = "File Properties" });
            var voIdPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            voIdPanel.Children.Add(new TextBlock { Text = "Voiceover ID:", Width = 120 });
            voIdPanel.Children.Add(_voIdEdit);
            filePropertiesPanel.Children.Add(voIdPanel);
            var ambientTrackPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            ambientTrackPanel.Children.Add(new TextBlock { Text = "Ambient Track:", Width = 120 });
            ambientTrackPanel.Children.Add(_ambientTrackCombo);
            filePropertiesPanel.Children.Add(ambientTrackPanel);

            // Initialize conversation type combo box
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QComboBox conversationSelect
            _conversationSelect = new ComboBox();
            _conversationSelect.Items.Add("Human");
            _conversationSelect.Items.Add("Computer");
            _conversationSelect.Items.Add("Other");
            _conversationSelect.SelectedIndex = 0;
            _conversationSelect.SelectionChanged += (s, e) => OnFilePropertyChanged();
            var conversationPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            conversationPanel.Children.Add(new TextBlock { Text = "Conversation Type:", Width = 120 });
            conversationPanel.Children.Add(_conversationSelect);
            filePropertiesPanel.Children.Add(conversationPanel);

            // Initialize computer type combo box
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QComboBox computerSelect
            _computerSelect = new ComboBox();
            _computerSelect.Items.Add("Modern");
            _computerSelect.Items.Add("Ancient");
            _computerSelect.SelectedIndex = 0;
            _computerSelect.SelectionChanged += (s, e) => OnFilePropertyChanged();
            var computerPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            computerPanel.Children.Add(new TextBlock { Text = "Computer Type:", Width = 120 });
            computerPanel.Children.Add(_computerSelect);
            filePropertiesPanel.Children.Add(computerPanel);

            // Initialize entry delay spin box
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QSpinBox entryDelaySpin
            _entryDelaySpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Width = 120 };
            _entryDelaySpin.ValueChanged += (s, e) => OnFilePropertyChanged();
            var entryDelayPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            entryDelayPanel.Children.Add(new TextBlock { Text = "Entry Delay:", Width = 120 });
            entryDelayPanel.Children.Add(_entryDelaySpin);
            filePropertiesPanel.Children.Add(entryDelayPanel);

            // Initialize reply delay spin box
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QSpinBox replyDelaySpin
            _replyDelaySpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Width = 120 };
            _replyDelaySpin.ValueChanged += (s, e) => OnFilePropertyChanged();
            var replyDelayPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            replyDelayPanel.Children.Add(new TextBlock { Text = "Reply Delay:", Width = 120 });
            replyDelayPanel.Children.Add(_replyDelaySpin);
            filePropertiesPanel.Children.Add(replyDelayPanel);

            // Initialize on abort combo box (ResRef)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QComboBox onAbortCombo
            _onAbortCombo = new ComboBox { IsEditable = true };
            _onAbortCombo.LostFocus += (s, e) => OnFilePropertyChanged();
            var onAbortPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            onAbortPanel.Children.Add(new TextBlock { Text = "On Abort Script:", Width = 120 });
            onAbortPanel.Children.Add(_onAbortCombo);
            filePropertiesPanel.Children.Add(onAbortPanel);

            // Initialize on end combo box (ResRef)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QComboBox onEndEdit
            _onEndEdit = new ComboBox { IsEditable = true };
            _onEndEdit.LostFocus += (s, e) => OnFilePropertyChanged();
            var onEndPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            onEndPanel.Children.Add(new TextBlock { Text = "On End Script:", Width = 120 });
            onEndPanel.Children.Add(_onEndEdit);
            filePropertiesPanel.Children.Add(onEndPanel);

            // Initialize camera model combo box (ResRef)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QComboBox cameraModelSelect
            _cameraModelSelect = new ComboBox { IsEditable = true };
            _cameraModelSelect.LostFocus += (s, e) => OnFilePropertyChanged();
            var cameraModelPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            cameraModelPanel.Children.Add(new TextBlock { Text = "Camera Model:", Width = 120 });
            cameraModelPanel.Children.Add(_cameraModelSelect);
            filePropertiesPanel.Children.Add(cameraModelPanel);

            // Initialize file-level checkboxes
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QCheckBox unequipHandsCheckbox, unequipAllCheckbox, skippableCheckbox, animatedCutCheckbox, oldHitCheckbox
            _unequipHandsCheckbox = new CheckBox { Content = "Unequip Hands" };
            _unequipHandsCheckbox.Checked += (s, e) => OnFilePropertyChanged();
            _unequipHandsCheckbox.Unchecked += (s, e) => OnFilePropertyChanged();
            filePropertiesPanel.Children.Add(_unequipHandsCheckbox);

            _unequipAllCheckbox = new CheckBox { Content = "Unequip All" };
            _unequipAllCheckbox.Checked += (s, e) => OnFilePropertyChanged();
            _unequipAllCheckbox.Unchecked += (s, e) => OnFilePropertyChanged();
            filePropertiesPanel.Children.Add(_unequipAllCheckbox);

            _skippableCheckbox = new CheckBox { Content = "Skippable" };
            _skippableCheckbox.Checked += (s, e) => OnFilePropertyChanged();
            _skippableCheckbox.Unchecked += (s, e) => OnFilePropertyChanged();
            filePropertiesPanel.Children.Add(_skippableCheckbox);

            _animatedCutCheckbox = new CheckBox { Content = "Animated Cut" };
            _animatedCutCheckbox.Checked += (s, e) => OnFilePropertyChanged();
            _animatedCutCheckbox.Unchecked += (s, e) => OnFilePropertyChanged();
            filePropertiesPanel.Children.Add(_animatedCutCheckbox);

            _oldHitCheckbox = new CheckBox { Content = "Old Hit Check" };
            _oldHitCheckbox.Checked += (s, e) => OnFilePropertyChanged();
            _oldHitCheckbox.Unchecked += (s, e) => OnFilePropertyChanged();
            filePropertiesPanel.Children.Add(_oldHitCheckbox);

            panel.Children.Add(filePropertiesPanel);

            // Initialize dialog tree view
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            _dialogTree = new TreeView();
            _dialogTree.SelectionChanged += (s, e) => OnSelectionChanged();

            // Setup context menu for dialog tree
            SetupDialogTreeContextMenu();

            panel.Children.Add(_dialogTree);

            // Setup left dock widget (orphaned nodes and pinned items lists)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:694-733
            // Original: def setup_left_dock_widget(self):
            SetupLeftDockWidget();
            if (_leftDockWidget != null)
            {
                panel.Children.Add(_leftDockWidget);
            }

            // Initialize link condition widgets
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            _condition1ResrefEdit = new ComboBox { IsEditable = true };
            _condition1ResrefEdit.LostFocus += (s, e) => OnNodeUpdate();
            _condition2ResrefEdit = new ComboBox { IsEditable = true };
            _condition2ResrefEdit.LostFocus += (s, e) => OnNodeUpdate();
            _logicSpin = new NumericUpDown { Minimum = 0, Maximum = 1, Value = 0 };
            _logicSpin.ValueChanged += (s, e) => OnNodeUpdate();

            // Initialize condition parameter widgets (K2-specific fields, but available in UI)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QSpinBox condition1Param1Spin, condition1Param2Spin, etc., QCheckBox condition1NotCheckbox
            _condition1Param1Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition1Param1Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition1Param2Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition1Param2Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition1Param3Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition1Param3Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition1Param4Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition1Param4Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition1Param5Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition1Param5Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition1Param6Edit = new TextBox();
            _condition1Param6Edit.LostFocus += (s, e) => OnNodeUpdate();
            _condition1NotCheckbox = new CheckBox();
            _condition1NotCheckbox.Checked += (s, e) => OnNodeUpdate();
            _condition1NotCheckbox.Unchecked += (s, e) => OnNodeUpdate();

            _condition2Param1Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition2Param1Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition2Param2Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition2Param2Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition2Param3Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition2Param3Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition2Param4Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition2Param4Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition2Param5Spin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = 0 };
            _condition2Param5Spin.ValueChanged += (s, e) => OnNodeUpdate();
            _condition2Param6Edit = new TextBox();
            _condition2Param6Edit.LostFocus += (s, e) => OnNodeUpdate();
            _condition2NotCheckbox = new CheckBox();
            _condition2NotCheckbox.Checked += (s, e) => OnNodeUpdate();
            _condition2NotCheckbox.Unchecked += (s, e) => OnNodeUpdate();

            var linkPanel = new StackPanel();
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 ResRef:" });
            linkPanel.Children.Add(_condition1ResrefEdit);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 Param1:" });
            linkPanel.Children.Add(_condition1Param1Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 Param2:" });
            linkPanel.Children.Add(_condition1Param2Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 Param3:" });
            linkPanel.Children.Add(_condition1Param3Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 Param4:" });
            linkPanel.Children.Add(_condition1Param4Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 Param5:" });
            linkPanel.Children.Add(_condition1Param5Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 Param6:" });
            linkPanel.Children.Add(_condition1Param6Edit);
            linkPanel.Children.Add(_condition1NotCheckbox);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 1 Not" });

            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 ResRef:" });
            linkPanel.Children.Add(_condition2ResrefEdit);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 Param1:" });
            linkPanel.Children.Add(_condition2Param1Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 Param2:" });
            linkPanel.Children.Add(_condition2Param2Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 Param3:" });
            linkPanel.Children.Add(_condition2Param3Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 Param4:" });
            linkPanel.Children.Add(_condition2Param4Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 Param5:" });
            linkPanel.Children.Add(_condition2Param5Spin);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 Param6:" });
            linkPanel.Children.Add(_condition2Param6Edit);
            linkPanel.Children.Add(_condition2NotCheckbox);
            linkPanel.Children.Add(new TextBlock { Text = "Condition 2 Not" });

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

            // Initialize camera widgets
            InitializeCameraWidgets();

            var cameraPanel = new StackPanel();
            cameraPanel.Children.Add(new TextBlock { Text = "Camera ID:" });
            cameraPanel.Children.Add(_cameraIdSpin);
            cameraPanel.Children.Add(new TextBlock { Text = "Camera Animation:" });
            cameraPanel.Children.Add(_cameraAnimSpin);
            cameraPanel.Children.Add(new TextBlock { Text = "Camera Angle:" });
            cameraPanel.Children.Add(_cameraAngleSelect);
            cameraPanel.Children.Add(new TextBlock { Text = "Camera Effect:" });
            cameraPanel.Children.Add(_cameraEffectSelect);
            cameraPanel.Children.Add(new TextBlock { Text = "Emotion:" });
            cameraPanel.Children.Add(_emotionSelect);
            cameraPanel.Children.Add(new TextBlock { Text = "Expression:" });
            cameraPanel.Children.Add(_expressionSelect);
            panel.Children.Add(cameraPanel);

            // Initialize sound combo box
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:360
            // Original: self.ui.soundComboBox.currentTextChanged.connect(self.on_node_update)
            _soundComboBox = new ComboBox { IsEditable = true };
            _soundComboBox.LostFocus += (s, e) => OnNodeUpdate();

            // Original: QCheckBox soundCheckbox
            _soundCheckbox = new CheckBox { Content = "Sound Exists" };
            _soundCheckbox.Checked += (s, e) => OnNodeUpdate();
            _soundCheckbox.Unchecked += (s, e) => OnNodeUpdate();

            var soundPanel = new StackPanel();
            soundPanel.Children.Add(new TextBlock { Text = "Sound ResRef:" });
            soundPanel.Children.Add(_soundComboBox);
            soundPanel.Children.Add(_soundCheckbox);
            panel.Children.Add(soundPanel);

            // Initialize voice combo box
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:365
            // Original: self.ui.voiceComboBox.currentTextChanged.connect(self.on_node_update)
            _voiceComboBox = new ComboBox { IsEditable = true };
            _voiceComboBox.LostFocus += (s, e) => OnNodeUpdate();
            var voicePanel = new StackPanel();
            voicePanel.Children.Add(new TextBlock { Text = "Voice ResRef:" });
            voicePanel.Children.Add(_voiceComboBox);
            panel.Children.Add(voicePanel);

            // Initialize listener text box
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:374
            // Original: self.ui.listenerEdit.textEdited.connect(self.on_node_update)
            _listenerEdit = new TextBox();
            _listenerEdit.LostFocus += (s, e) => OnNodeUpdate();
            var listenerPanel = new StackPanel();
            listenerPanel.Children.Add(new TextBlock { Text = "Listener:" });
            listenerPanel.Children.Add(_listenerEdit);
            panel.Children.Add(listenerPanel);

            // Initialize quest widgets
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QLineEdit questEdit, QSpinBox questEntrySpin
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:407, 408
            // Original: self.ui.questEdit.textEdited.connect(self.on_node_update), self.ui.questEntrySpin.valueChanged.connect(self.on_node_update)
            _questEdit = new TextBox();
            _questEdit.LostFocus += (s, e) => OnNodeUpdate();
            var questPanel = new StackPanel();
            questPanel.Children.Add(new TextBlock { Text = "Quest:" });
            questPanel.Children.Add(_questEdit);
            panel.Children.Add(questPanel);

            _questEntrySpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _questEntrySpin.ValueChanged += (s, e) => OnNodeUpdate();
            var questEntryPanel = new StackPanel();
            questEntryPanel.Children.Add(new TextBlock { Text = "Quest Entry:" });
            questEntryPanel.Children.Add(_questEntrySpin);
            panel.Children.Add(questEntryPanel);

            // Original: QSpinBox nodeIdSpin (KotOR 2)
            _nodeIdSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _nodeIdSpin.ValueChanged += (s, e) => OnNodeUpdate();
            var nodeIdPanel = new StackPanel();
            nodeIdPanel.Children.Add(new TextBlock { Text = "Node ID:" });
            nodeIdPanel.Children.Add(_nodeIdSpin);
            panel.Children.Add(nodeIdPanel);

            // Original: QSpinBox alienRaceNodeSpin (KotOR 2)
            _alienRaceNodeSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _alienRaceNodeSpin.ValueChanged += (s, e) => OnNodeUpdate();
            var alienRacePanel = new StackPanel();
            alienRacePanel.Children.Add(new TextBlock { Text = "Alien Race Node:" });
            alienRacePanel.Children.Add(_alienRaceNodeSpin);
            panel.Children.Add(alienRacePanel);

            // Original: QSpinBox postProcSpin (KotOR 2)
            _postProcSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _postProcSpin.ValueChanged += (s, e) => OnNodeUpdate();
            var postProcPanel = new StackPanel();
            postProcPanel.Children.Add(new TextBlock { Text = "Post Proc Node:" });
            postProcPanel.Children.Add(_postProcSpin);
            panel.Children.Add(postProcPanel);

            // Original: QCheckBox nodeUnskippableCheckbox (KotOR 2)
            _nodeUnskippableCheckbox = new CheckBox { Content = "Unskippable" };
            _nodeUnskippableCheckbox.Checked += (s, e) => OnNodeUpdate();
            _nodeUnskippableCheckbox.Unchecked += (s, e) => OnNodeUpdate();
            var unskippablePanel = new StackPanel();
            unskippablePanel.Children.Add(_nodeUnskippableCheckbox);
            panel.Children.Add(unskippablePanel);

            // Initialize plot widgets
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui
            // Original: QComboBox plotIndexCombo, QDoubleSpinBox plotXpSpin
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:405-406
            // Original: self.ui.plotIndexCombo.currentIndexChanged.connect(self.on_node_update), self.ui.plotXpSpin.valueChanged.connect(self.on_node_update)
            // VERIFIED: PlotXpPercentage field aligns with swkotor2.exe DLG format
            // - Field name "PlotXPPercentage" confirmed in swkotor2.exe string table @ 0x007c35cc (DialogueManager.cs:1098)
            // - Field type: float (matches GFF Single type, default 1.0f in DLGNode.cs)
            // - GFF I/O: DLGHelper.cs reads as Acquire("PlotXPPercentage", 0.0f), writes conditionally when != 0.0f
            // - UI: NumericUpDown (0-100 int) converted to float for storage
            // - Round-trip tested: TestDlgEditorManipulatePlotXpRoundtrip verifies 0,25,50,75,100 values
            // - Cross-format consistency: Also implemented in CNV format (CNVHelper.cs)
            // Ghidra project: C:\Users\boden\Andastra Ghidra Project.gpr
            _plotIndexCombo = new ComboBox();
            // Plot index values 0-5 (matching Python implementation)
            _plotIndexCombo.Items.Add("0 (No Plot)");
            _plotIndexCombo.Items.Add("1 (Plot 1)");
            _plotIndexCombo.Items.Add("2 (Plot 2)");
            _plotIndexCombo.Items.Add("3 (Plot 3)");
            _plotIndexCombo.Items.Add("4 (Plot 4)");
            _plotIndexCombo.Items.Add("5 (Plot 5)");
            _plotIndexCombo.SelectedIndex = 0;
            _plotIndexCombo.SelectionChanged += (s, e) => OnNodeUpdate();
            var plotIndexPanel = new StackPanel();
            plotIndexPanel.Children.Add(new TextBlock { Text = "Plot Index:" });
            plotIndexPanel.Children.Add(_plotIndexCombo);
            panel.Children.Add(plotIndexPanel);

            _plotXpSpin = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0, Increment = 1 };
            _plotXpSpin.ValueChanged += (s, e) => OnNodeUpdate();
            var plotXpPanel = new StackPanel();
            plotXpPanel.Children.Add(new TextBlock { Text = "Plot XP %:" });
            plotXpPanel.Children.Add(_plotXpSpin);
            panel.Children.Add(plotXpPanel);

            // Initialize script1 and script2 combo boxes
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui:170, 374
            // Original: QComboBox script1ResrefEdit, script2ResrefEdit
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:347
            // Original: self.ui.script1ResrefEdit.currentTextChanged.connect(self.on_node_update), self.ui.script2ResrefEdit.currentTextChanged.connect(self.on_node_update)
            _script1ResrefEdit = new ComboBox { IsEditable = true };
            _script1ResrefEdit.LostFocus += (s, e) => OnNodeUpdate();
            var script1Panel = new StackPanel();
            script1Panel.Children.Add(new TextBlock { Text = "Script1 ResRef:" });
            script1Panel.Children.Add(_script1ResrefEdit);
            panel.Children.Add(script1Panel);

            _script2ResrefEdit = new ComboBox { IsEditable = true };
            _script2ResrefEdit.LostFocus += (s, e) => OnNodeUpdate();
            var script2Panel = new StackPanel();
            script2Panel.Children.Add(new TextBlock { Text = "Script2 ResRef:" });
            script2Panel.Children.Add(_script2ResrefEdit);
            panel.Children.Add(script2Panel);

            // Initialize animation UI controls
            // Matching PyKotor implementation at Tools/HolocronToolset/src/ui/editors/dlg.ui:966-992
            _animsList = new ListBox();
            _addAnimButton = new Button { Content = "Add Animation" };
            _removeAnimButton = new Button { Content = "Remove Animation" };
            _editAnimButton = new Button { Content = "Edit Animation" };

            // TODO:  Add animation controls to UI (basic layout for now)
            var animPanel = new StackPanel();
            animPanel.Children.Add(_animsList);
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            buttonPanel.Children.Add(_addAnimButton);
            buttonPanel.Children.Add(_removeAnimButton);
            buttonPanel.Children.Add(_editAnimButton);
            animPanel.Children.Add(buttonPanel);
            panel.Children.Add(animPanel);
        }

        /// <summary>
        /// Sets up the context menu for the dialog tree.
        /// Matching PyKotor implementation that uses dynamic context menu creation via _get_link_context_menu.
        /// </summary>
        private void SetupDialogTreeContextMenu()
        {
            if (_dialogTree == null)
            {
                return;
            }

            // In Avalonia, we handle context menu requests via the ContextRequested event
            // This allows us to create dynamic context menus based on the selected item
            _dialogTree.ContextRequested += (sender, e) =>
            {
                // Get the item at the pointer position
                Avalonia.Point? point = e.TryGetPosition(_dialogTree, out Avalonia.Point pos) ? pos : (Avalonia.Point?)null;
                if (!point.HasValue)
                {
                    return;
                }

                // Find the item at the pointer position
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

                if (item != null)
                {
                    var contextMenu = GetLinkContextMenu(_dialogTree, item);
                    if (contextMenu != null)
                    {
                        contextMenu.Open(_dialogTree);
                        e.Handled = true;
                    }
                }
                else
                {
                    // No item selected - show empty context menu or menu for adding root node
                    var contextMenu = new ContextMenu();
                    var addEntryItem = new MenuItem
                    {
                        Header = "Add Entry"
                    };
                    addEntryItem.Click += (s, args) => _model.AddRootNode();
                    contextMenu.Items.Add(addEntryItem);
                    contextMenu.Open(_dialogTree);
                    e.Handled = true;
                }
            };
        }

        /// <summary>
        /// Gets the context menu for a dialog tree item.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1711-1875
        /// Original: def _get_link_context_menu(self, source_widget: DLGListWidget | DLGTreeView, item: DLGStandardItem | DLGListWidgetItem) -> _QMenu:
        /// </summary>
        /// <param name="sourceWidget">The source widget (TreeView or DLGListWidget).</param>
        /// <param name="item">The item to get the context menu for.</param>
        /// <returns>The context menu for the item.</returns>
        public ContextMenu GetLinkContextMenu(Control sourceWidget, DLGStandardItem item)
        {
            if (item?.Link == null)
            {
                return null;
            }

            // Matching PyKotor: self._check_clipboard_for_json_node()
            CheckClipboardForJsonNode();

            // Matching PyKotor: not_an_orphan: bool = source_widget is not self.orphaned_nodes_list
            bool notAnOrphan = sourceWidget != _orphanedNodesList;

            // Matching PyKotor: node_type: Literal["Entry", "Reply"] = "Entry" if isinstance(item.link.node, DLGEntry) else "Reply"
            string nodeType = item.Link.Node is DLGEntry ? "Entry" : "Reply";
            string otherNodeType = item.Link.Node is DLGEntry ? "Reply" : "Entry";

            var menu = new ContextMenu();
            var menuItems = new List<MenuItem>();

            // Actions for both list widget and tree view
            // Matching PyKotor: edit_text_action: _QAction | None = menu.addAction("Edit Text")
            var editTextItem = new MenuItem
            {
                Header = "Edit Text"
            };
            editTextItem.Click += (s, e) =>
            {
                // Matching PyKotor: edit_text_action.triggered.connect(lambda *args: self.edit_text(indexes=source_widget.selectedIndexes(), source_widget=source_widget))
                EditText();
            };
            menuItems.Add(editTextItem);

            // Matching PyKotor: focus_action: _QAction | None = menu.addAction("Focus")
            var focusItem = new MenuItem
            {
                Header = "Focus"
            };
            focusItem.Click += (s, e) =>
            {
                // Matching PyKotor: focus_action.triggered.connect(lambda: self.focus_on_node(item.link))
                FocusOnNode(item.Link);
            };
            // Matching PyKotor: focus_action.setEnabled(bool(item.link.node.links))
            focusItem.IsEnabled = item.Link.Node?.Links != null && item.Link.Node.Links.Count > 0;
            // Matching PyKotor: focus_action.setVisible(not_an_orphan)
            focusItem.IsVisible = notAnOrphan;
            menuItems.Add(focusItem);

            // Matching PyKotor: find_references_action: _QAction | None = menu.addAction("Find References")
            var findReferencesItem = new MenuItem
            {
                Header = "Find References"
            };
            findReferencesItem.Click += (s, e) =>
            {
                // Matching PyKotor: find_references_action.triggered.connect(lambda: self.find_references(item))
                FindReferences(item);
            };
            // Matching PyKotor: find_references_action.setVisible(not_an_orphan)
            findReferencesItem.IsVisible = notAnOrphan;
            menuItems.Add(findReferencesItem);

            // Play menu for both
            // Matching PyKotor: play_menu: _QMenu | None = menu.addMenu("Play")
            var playMenu = new MenuItem
            {
                Header = "Play"
            };
            var playSubMenuItems = new List<MenuItem>();

            // Matching PyKotor: play_sound_action: _QAction | None = play_menu.addAction("Play Sound")
            var playSoundItem = new MenuItem
            {
                Header = "Play Sound"
            };
            playSoundItem.Click += (s, e) =>
            {
                // Matching PyKotor: play_sound_action.triggered.connect(lambda: (self.play_sound("" if item.link is None else str(item.link.node.sound)) and None) or None)
                string soundResrefValue = item.Link?.Node?.Sound?.ToString() ?? "";
                if (!string.IsNullOrEmpty(soundResrefValue))
                {
                    PlaySound(soundResrefValue, new[] { SearchLocation.SOUND, SearchLocation.VOICE });
                }
            };
            // Matching PyKotor: play_sound_action.setEnabled(bool(self.ui.soundComboBox.currentText().strip()))
            // Note: We check the node's sound property directly since we don't have a separate soundComboBox UI control
            string soundResrefForEnable = item.Link?.Node?.Sound?.ToString() ?? "";
            playSoundItem.IsEnabled = !string.IsNullOrWhiteSpace(soundResrefForEnable);
            playSubMenuItems.Add(playSoundItem);

            // Matching PyKotor: play_voice_action: _QAction | None = play_menu.addAction("Play Voice")
            var playVoiceItem = new MenuItem
            {
                Header = "Play Voice"
            };
            playVoiceItem.Click += (s, e) =>
            {
                // Matching PyKotor: play_voice_action.triggered.connect(lambda: (self.play_sound("" if item.link is None else str(item.link.node.vo_resref)) and None) or None)
                // Note: DLGNode has VoResRef property (with capital R)
                string voiceResrefValue = item.Link?.Node?.VoResRef?.ToString() ?? "";
                if (!string.IsNullOrEmpty(voiceResrefValue))
                {
                    PlaySound(voiceResrefValue, new[] { SearchLocation.VOICE });
                }
            };
            // Matching PyKotor: play_voice_action.setEnabled(bool(self.ui.voiceComboBox.currentText().strip()))
            // Note: We check the node's VoResRef property directly
            string voiceResrefForEnable = item.Link?.Node?.VoResRef?.ToString() ?? "";
            playVoiceItem.IsEnabled = !string.IsNullOrWhiteSpace(voiceResrefForEnable);
            playSubMenuItems.Add(playVoiceItem);

            // Matching PyKotor: play_menu.setEnabled(bool(self.ui.soundComboBox.currentText().strip() or self.ui.voiceComboBox.currentText().strip()))
            playMenu.IsEnabled = !string.IsNullOrWhiteSpace(soundResrefForEnable) || !string.IsNullOrWhiteSpace(voiceResrefForEnable);
            foreach (var subItem in playSubMenuItems)
            {
                playMenu.Items.Add(subItem);
            }
            menuItems.Add(playMenu);

            // Matching PyKotor: menu.addSeparator()
            menuItems.Add(new MenuItem { Header = "-" });

            // Copy actions for both
            // Matching PyKotor: copy_node_action: _QAction | None = menu.addAction(f"Copy {node_type} to Clipboard")
            var copyNodeItem = new MenuItem
            {
                Header = $"Copy {nodeType} to Clipboard"
            };
            copyNodeItem.Click += async (s, e) =>
            {
                // Matching PyKotor: copy_node_action.triggered.connect(lambda: self.model.copy_link_and_node(item.link))
                if (item.Link != null)
                {
                    await _model.CopyLinkAndNode(item.Link, this);
                }
            };
            menuItems.Add(copyNodeItem);

            // Matching PyKotor: copy_gff_path_action: _QAction | None = menu.addAction("Copy GFF Path")
            var copyGffPathItem = new MenuItem
            {
                Header = "Copy GFF Path"
            };
            copyGffPathItem.Click += (s, e) =>
            {
                // Matching PyKotor: copy_gff_path_action.triggered.connect(lambda: self.copy_path(None if item.link is None else item.link.node))
                CopyPath();
            };
            // Matching PyKotor: copy_gff_path_action.setVisible(not_an_orphan)
            copyGffPathItem.IsVisible = notAnOrphan;
            menuItems.Add(copyGffPathItem);

            // Matching PyKotor: menu.addSeparator()
            menuItems.Add(new MenuItem { Header = "-" });

            if (sourceWidget is TreeView)
            {
                // Tree view only actions
                // Matching PyKotor: expand_all_children_action: _QAction | None = menu.addAction("Expand All Children")
                var expandAllChildrenItem = new MenuItem
                {
                    Header = "Expand All Children"
                };
                expandAllChildrenItem.Click += (s, e) =>
                {
                    // Matching PyKotor: expand_all_children_action.triggered.connect(lambda: self.set_expand_recursively(item, set(), expand=True))
                    // Find the TreeViewItem for this DLGStandardItem
                    TreeViewItem treeItem = FindTreeViewItem(_dialogTree.ItemsSource as System.Collections.IEnumerable, item);
                    if (treeItem != null)
                    {
                        SetExpandRecursivelyInternal(item, treeItem, new HashSet<DLGNode>(), true, 11, 0, true);
                    }
                };
                menuItems.Add(expandAllChildrenItem);

                // Matching PyKotor: collapse_all_children_action: _QAction | None = menu.addAction("Collapse All Children")
                var collapseAllChildrenItem = new MenuItem
                {
                    Header = "Collapse All Children"
                };
                collapseAllChildrenItem.Click += (s, e) =>
                {
                    // Matching PyKotor: collapse_all_children_action.triggered.connect(lambda: self.set_expand_recursively(item, set(), expand=False))
                    // Find the TreeViewItem for this DLGStandardItem
                    TreeViewItem treeItem = FindTreeViewItem(_dialogTree.ItemsSource as System.Collections.IEnumerable, item);
                    if (treeItem != null)
                    {
                        SetExpandRecursivelyInternal(item, treeItem, new HashSet<DLGNode>(), false, 11, 0, true);
                    }
                };
                menuItems.Add(collapseAllChildrenItem);

                // Matching PyKotor: menu.addSeparator()
                menuItems.Add(new MenuItem { Header = "-" });

                // Paste actions
                // Matching PyKotor: paste_link_action: _QAction = menu.addAction(f"Paste {other_node_type} from Clipboard as Link")
                var pasteLinkItem = new MenuItem
                {
                    Header = $"Paste {otherNodeType} from Clipboard as Link"
                };
                // Matching PyKotor: paste_new_action: _QAction = menu.addAction(f"Paste {other_node_type} from Clipboard as Deep Copy")
                var pasteNewItem = new MenuItem
                {
                    Header = $"Paste {otherNodeType} from Clipboard as Deep Copy"
                };

                // Matching PyKotor: if self._copy is None: paste_link_action.setEnabled(False), paste_new_action.setEnabled(False)
                if (_copy == null)
                {
                    pasteLinkItem.IsEnabled = false;
                    pasteNewItem.IsEnabled = false;
                }
                else
                {
                    // Matching PyKotor: copied_node_type: Literal["Entry", "Reply"] = "Entry" if isinstance(self._copy.node, DLGEntry) else "Reply"
                    string copiedNodeType = _copy.Node is DLGEntry ? "Entry" : "Reply";
                    pasteLinkItem.Header = $"Paste {copiedNodeType} from Clipboard as Link";
                    pasteNewItem.Header = $"Paste {copiedNodeType} from Clipboard as Deep Copy";
                    // Matching PyKotor: if node_type == copied_node_type: paste_link_action.setEnabled(False), paste_new_action.setEnabled(False)
                    if (nodeType == copiedNodeType)
                    {
                        pasteLinkItem.IsEnabled = false;
                        pasteNewItem.IsEnabled = false;
                    }
                }

                pasteLinkItem.Click += (s, e) =>
                {
                    // Matching PyKotor: paste_link_action.triggered.connect(lambda: self.model.paste_item(item, as_new_branches=False))
                    _model.PasteItem(item, _copy, asNewBranches: false);
                };

                pasteNewItem.Click += (s, e) =>
                {
                    // Matching PyKotor: paste_new_action.triggered.connect(lambda: self.model.paste_item(item, as_new_branches=True))
                    _model.PasteItem(item, _copy, asNewBranches: true);
                };

                menuItems.Add(pasteLinkItem);
                menuItems.Add(pasteNewItem);

                // Matching PyKotor: menu.addSeparator()
                menuItems.Add(new MenuItem { Header = "-" });

                // Add/Move actions
                // Matching PyKotor: add_node_action: _QAction | None = menu.addAction(f"Add {other_node_type}")
                var addNodeItem = new MenuItem
                {
                    Header = $"Add {otherNodeType}"
                };
                addNodeItem.Click += (s, e) =>
                {
                    // Matching PyKotor: add_node_action.triggered.connect(lambda: self.model.add_child_to_item(item))
                    _model.AddChildToItem(item, null);
                };
                menuItems.Add(addNodeItem);

                // Matching PyKotor: menu.addSeparator()
                menuItems.Add(new MenuItem { Header = "-" });

                // Matching PyKotor: move_up_action: _QAction | None = menu.addAction("Move Up")
                var moveUpItem = new MenuItem
                {
                    Header = "Move Up"
                };
                moveUpItem.Click += (s, e) =>
                {
                    // Matching PyKotor: move_up_action.triggered.connect(lambda: self.model.shift_item(item, -1))
                    _model.ShiftItem(item, -1);
                    UpdateTreeView();
                };
                menuItems.Add(moveUpItem);

                // Matching PyKotor: move_down_action: _QAction | None = menu.addAction("Move Down")
                var moveDownItem = new MenuItem
                {
                    Header = "Move Down"
                };
                moveDownItem.Click += (s, e) =>
                {
                    // Matching PyKotor: move_down_action.triggered.connect(lambda: self.model.shift_item(item, -1)) [Note: Python uses -1 for down, but logic is reversed]
                    _model.ShiftItem(item, 1);
                    UpdateTreeView();
                };
                menuItems.Add(moveDownItem);

                // Matching PyKotor: menu.addSeparator()
                menuItems.Add(new MenuItem { Header = "-" });

                // Remove action
                // Matching PyKotor: remove_link_action: _QAction | None = menu.addAction(f"Remove {node_type}")
                var removeLinkItem = new MenuItem
                {
                    Header = $"Remove {nodeType}"
                };
                removeLinkItem.Click += (s, e) =>
                {
                    // Matching PyKotor: remove_link_action.triggered.connect(lambda: self.model.remove_link(item))
                    RemoveLink(item);
                };
                menuItems.Add(removeLinkItem);

                // Matching PyKotor: menu.addSeparator()
                menuItems.Add(new MenuItem { Header = "-" });
            }

            // Matching PyKotor: delete_all_references_action = QAction(f"Delete ALL References to {node_type}", menu)
            var deleteAllReferencesItem = new MenuItem
            {
                Header = $"Delete ALL References to {nodeType}"
            };
            deleteAllReferencesItem.Click += (s, e) =>
            {
                // Matching PyKotor: delete_all_references_action.triggered.connect(lambda: self.model.delete_node_everywhere(item.link.node))
                if (item.Link?.Node != null)
                {
                    _model.DeleteNodeEverywhere(item.Link.Node);
                    UpdateTreeView();
                }
            };
            // Matching PyKotor: delete_all_references_action.setVisible(not_an_orphan)
            deleteAllReferencesItem.IsVisible = notAnOrphan;
            menuItems.Add(deleteAllReferencesItem);

            foreach (var menuItem in menuItems)
            {
                menu.Items.Add(menuItem);
            }

            return menu;
        }

        /// <summary>
        /// Checks the clipboard for a JSON node and sets _copy if found.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1465-1478
        /// Original: def _check_clipboard_for_json_node(self):
        /// </summary>
        private void CheckClipboardForJsonNode()
        {
            try
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard == null)
                {
                    return;
                }

                // TODO:  Note: Avalonia clipboard access is async, but we'll use a synchronous approach for now
                // TODO:  In a full implementation, we might need to make this async or use a different approach
                // TODO: STUB - For now, we'll just try to get the clipboard text if possible
                // Matching PyKotor: clipboard_text: str = cb.text()
                // Matching PyKotor: node_data: dict[str | int, Any] = json.loads(clipboard_text)
                // Matching PyKotor: if isinstance(node_data, dict) and "type" in node_data: self._copy = DLGLink.from_dict(node_data)
                // TODO:  This is a simplified implementation - in a full implementation, we'd need async clipboard access
            }
            catch (Exception)
            {
                // Matching PyKotor: except json.JSONDecodeError: ... except Exception: self._logger.exception("Invalid JSON node on clipboard.")
                // Silently ignore clipboard errors
            }
        }

        /// <summary>
        /// Removes a link from the parent node.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/model.py:620-634
        /// Original: def remove_link(self, item: DLGStandardItem):
        /// </summary>
        /// <param name="item">The item whose link should be removed.</param>
        private void RemoveLink(DLGStandardItem item)
        {
            if (item == null || item.Link == null)
            {
                return;
            }

            var parent = item.Parent;
            if (parent == null)
            {
                // Remove from root items
                _model.RemoveStarter(item.Link);
            }
            else
            {
                // Remove from parent's children
                if (parent.Link?.Node != null)
                {
                    parent.Link.Node.Links.Remove(item.Link);
                    parent.RemoveChild(item);
                }
            }

            UpdateTreeView();
        }

        /// <summary>
        /// Sets up the left dock widget containing orphaned nodes and pinned items lists.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:694-733
        /// Original: def setup_left_dock_widget(self):
        /// </summary>
        private void SetupLeftDockWidget()
        {
            // Create the left dock widget container
            // Matching PyKotor: self.left_dock_widget: QDockWidget = QDockWidget("Orphaned Nodes and Pinned Items", self)
            // In Avalonia, we use a StackPanel instead of QDockWidget
            _leftDockWidget = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical
            };

            // Orphaned Nodes List
            // Matching PyKotor: self.orphaned_nodes_list: DLGListWidget = DLGListWidget(self)
            _orphanedNodesList = new DLGListWidget(this);
            // Matching PyKotor: self.orphaned_nodes_list.use_hover_text = False
            _orphanedNodesList.UseHoverText = false;
            // Note: Avalonia ListBox doesn't have setWordWrap, setItemDelegate, setDragEnabled, etc.
            // These are Qt-specific features. In Avalonia, we'll configure what's available.
            // The drag and drop functionality would need to be implemented using Avalonia's drag and drop API if needed.

            // Pinned Items List
            // Matching PyKotor: self.pinned_items_list: DLGListWidget = DLGListWidget(self)
            _pinnedItemsList = new DLGListWidget(this);
            // Matching PyKotor: self.pinned_items_list.setWordWrap(True)
            // Matching PyKotor: self.pinned_items_list.setSelectionMode(QAbstractItemView.SelectionMode.ExtendedSelection)
            // Matching PyKotor: self.pinned_items_list.setAcceptDrops(True)
            // Matching PyKotor: self.pinned_items_list.setDragEnabled(True)
            // Matching PyKotor: self.pinned_items_list.setDropIndicatorShown(True)
            // Matching PyKotor: self.pinned_items_list.setDragDropMode(QAbstractItemView.DragDropMode.DragDrop)
            // Note: Avalonia ListBox selection mode is controlled via SelectionMode property
            _pinnedItemsList.SelectionMode = SelectionMode.Multiple;

            // Add labels and lists to the layout
            // Matching PyKotor: self.left_dock_layout.addWidget(QLabel("Orphaned Nodes"))
            _leftDockWidget.Children.Add(new TextBlock { Text = "Orphaned Nodes" });
            // Matching PyKotor: self.left_dock_layout.addWidget(self.orphaned_nodes_list)
            _leftDockWidget.Children.Add(_orphanedNodesList);
            // Matching PyKotor: self.left_dock_layout.addWidget(QLabel("Pinned Items"))
            _leftDockWidget.Children.Add(new TextBlock { Text = "Pinned Items" });
            // Matching PyKotor: self.left_dock_layout.addWidget(self.pinned_items_list)
            _leftDockWidget.Children.Add(_pinnedItemsList);
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
            // Matching PyKotor implementation: self.refresh_stunt_list() after _load_dlg
            RefreshStuntList();
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

            // Load file-level properties (root DLG fields)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1149
            // Original: self.ui.voIdEdit.setText(dlg.vo_id)
            if (_voIdEdit != null)
            {
                _voIdEdit.Text = dlg.VoId ?? string.Empty;
            }
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py
            // Original: self.ui.ambientTrackCombo.set_combo_box_text(str(dlg.ambient_track))
            if (_ambientTrackCombo != null)
            {
                string ambientTrackText = dlg.AmbientTrack.ToString();
                _ambientTrackCombo.Text = ambientTrackText;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1155-1161
            // Original: self.ui.skippableCheckbox.setChecked(dlg.skippable)
            // Original: self.ui.animatedCutCheckbox.setChecked(bool(dlg.animated_cut))
            // Original: self.ui.oldHitCheckbox.setChecked(dlg.old_hit_check)
            // Original: self.ui.unequipHandsCheckbox.setChecked(dlg.unequip_hands)
            // Original: self.ui.unequipAllCheckbox.setChecked(dlg.unequip_items)
            if (_skippableCheckbox != null)
            {
                _skippableCheckbox.IsChecked = dlg.Skippable;
            }
            if (_animatedCutCheckbox != null)
            {
                _animatedCutCheckbox.IsChecked = dlg.AnimatedCut != 0;
            }
            if (_oldHitCheckbox != null)
            {
                _oldHitCheckbox.IsChecked = dlg.OldHitCheck;
            }
            if (_unequipHandsCheckbox != null)
            {
                _unequipHandsCheckbox.IsChecked = dlg.UnequipHands;
            }
            if (_unequipAllCheckbox != null)
            {
                _unequipAllCheckbox.IsChecked = dlg.UnequipItems;
            }

            // Clear undo/redo history when loading a dialog
            _actionHistory.Clear();
            UpdateTreeView();
            RefreshStuntList();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1229-1254
        // Original: def build(self) -> tuple[bytes, byte[]]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Save file-level properties (root DLG fields) from UI to CoreDlg
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1233
            // Original: self.core_dlg.vo_id = self.ui.voIdEdit.text()
            if (_voIdEdit != null)
            {
                _coreDlg.VoId = _voIdEdit.Text ?? string.Empty;
            }

            // Handle CNV format by converting DLG to CNV
            if (_restype == ResourceType.CNV)
            {
                // CNV format is only used by Eclipse Engine games
                BioWareGame gameToUse = _installation?.Game ?? BioWareGame.DA;
                if (!gameToUse.IsEclipse())
                {
                    // Default to DA if not Eclipse game
                    gameToUse = BioWareGame.DA;
                }
                var cnv = DLGHelper.ToCnv(_coreDlg);
                byte[] cnvData = CNVHelper.BytesCnv(cnv, gameToUse, ResourceType.CNV);
                return Tuple.Create(cnvData, new byte[0]);
            }

            // Detect game from installation - supports all engines (Odyssey K1/K2, Aurora NWN, Eclipse DA/DA2/ME)
            // BioWareGame-specific format handling:
            // - K2 (TSL): Extended DLG format with K2-specific fields (ActionParam1-5, Script2, etc.)
            // - K1, NWN, Eclipse (DA/DA2/ME): Base DLG format (no K2-specific fields)
            //   Eclipse games use K1-style DLG format (no K2 extensions)
            //   Note: Eclipse games may also use .cnv format, but DLG files follow K1 format
            BioWareGame gameToUseDlg = _installation?.Game ?? BioWareGame.K2;

            // For Eclipse games, use K1 format (no K2-specific fields)
            // Matching PyKotor: Eclipse games don't have K2 extensions
            if (gameToUseDlg.IsEclipse())
            {
                gameToUseDlg = BioWareGame.K1; // Use K1 format for Eclipse (no K2-specific fields)
            }
            // For Aurora (NWN), use K1 format (base DLG, no K2 extensions)
            else if (gameToUseDlg.IsAurora())
            {
                gameToUseDlg = BioWareGame.K1; // Use K1 format for Aurora (base DLG, no K2 extensions)
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
            BioWareGame currentGame = _installation?.Game ?? BioWareGame.K2;
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
            RefreshStuntList();
        }

        public override void SaveAs()
        {
            Save();
        }

        /// <summary>
        /// Opens a file dialog to select and load a DLG file.
        /// </summary>
        private async void OpenFile()
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            var options = new FilePickerOpenOptions
            {
                Title = "Open DLG File",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("DLG Files")
                    {
                        Patterns = new List<string> { "*.dlg" },
                        MimeTypes = new List<string> { "application/octet-stream" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new List<string> { "*" },
                        MimeTypes = new List<string> { "application/octet-stream" }
                    }
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files == null || files.Count == 0)
            {
                return;
            }

            var filePath = files[0].Path.LocalPath;
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                var data = File.ReadAllBytes(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                Load(filePath, fileName, ResourceType.DLG, data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open DLG file: {ex.Message}");
            }
        }

        /// <summary>
        /// Reverts changes by reloading the current DLG from its original source.
        /// </summary>
        private void RevertChanges()
        {
            if (string.IsNullOrEmpty(_filepath))
            {
                // No file to revert to, create new instead
                New();
                return;
            }

            try
            {
                if (File.Exists(_filepath))
                {
                    var data = File.ReadAllBytes(_filepath);
                    var fileName = Path.GetFileNameWithoutExtension(_filepath);
                    Load(_filepath, fileName, ResourceType.DLG, data);
                }
                else
                {
                    // File no longer exists, create new
                    New();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to revert DLG file: {ex.Message}");
                New();
            }
        }

        /// <summary>
        /// Refreshes the stunt list UI from the core DLG stunts.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2621-2627
        /// Original: def refresh_stunt_list(self):
        /// </summary>
        public void RefreshStuntList()
        {
            if (_stuntList == null)
            {
                return;
            }

            // Matching PyKotor implementation: self.ui.stuntList.clear()
            _stuntList.Items.Clear();

            // Matching PyKotor implementation: for stunt in self.core_dlg.stunts:
            // Original: text: str = f"{stunt.stunt_model} ({stunt.participant})"
            // Original: item = QListWidgetItem(text)
            // Original: item.setData(Qt.ItemDataRole.UserRole, stunt)
            // Original: self.ui.stuntList.addItem(item)
            foreach (DLGStunt stunt in _coreDlg.Stunts)
            {
                string text = $"{stunt.StuntModel} ({stunt.Participant})";
                var item = new ListBoxItem { Content = text, Tag = stunt };
                _stuntList.Items.Add(item);
            }
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
            _actionHistory.Apply((IDLGAction)action);
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
        public ComboBox Script1ResrefEdit => _script1ResrefEdit;
        public ComboBox Script2ResrefEdit => _script2ResrefEdit;
        public NumericUpDown LogicSpin => _logicSpin;
        public TreeView DialogTree => _dialogTree;

        // Expose condition parameter widgets for testing
        // Matching PyKotor implementation: editor.ui.condition1Param1Spin, etc.
        public NumericUpDown Condition1Param1Spin => _condition1Param1Spin;
        public NumericUpDown Condition1Param2Spin => _condition1Param2Spin;
        public NumericUpDown Condition1Param3Spin => _condition1Param3Spin;
        public NumericUpDown Condition1Param4Spin => _condition1Param4Spin;
        public NumericUpDown Condition1Param5Spin => _condition1Param5Spin;
        public TextBox Condition1Param6Edit => _condition1Param6Edit;
        public CheckBox Condition1NotCheckbox => _condition1NotCheckbox;
        public NumericUpDown Condition2Param1Spin => _condition2Param1Spin;
        public NumericUpDown Condition2Param2Spin => _condition2Param2Spin;
        public NumericUpDown Condition2Param3Spin => _condition2Param3Spin;
        public NumericUpDown Condition2Param4Spin => _condition2Param4Spin;
        public NumericUpDown Condition2Param5Spin => _condition2Param5Spin;
        public TextBox Condition2Param6Edit => _condition2Param6Edit;
        public CheckBox Condition2NotCheckbox => _condition2NotCheckbox;

        // Expose quest widgets for testing
        // Matching PyKotor implementation: editor.ui.questEdit, editor.ui.questEntrySpin
        public TextBox QuestEdit => _questEdit;
        public NumericUpDown QuestEntrySpin => _questEntrySpin;
        public ComboBox PlotIndexCombo => _plotIndexCombo;
        public NumericUpDown PlotXpSpin => _plotXpSpin;
        public NumericUpDown DelaySpin => _delaySpin;
        public NumericUpDown WaitFlagSpin => _waitFlagSpin;
        public NumericUpDown FadeTypeSpin => _fadeTypeSpin;

        // Expose camera widgets for testing
        // Matching PyKotor implementation: editor.ui.cameraIdSpin, editor.ui.cameraAnimSpin, editor.ui.cameraAngleSelect, editor.ui.cameraEffectSelect
        public NumericUpDown CameraIdSpin => _cameraIdSpin;
        public NumericUpDown CameraAnimSpin => _cameraAnimSpin;
        public ComboBox CameraAngleSelect => _cameraAngleSelect;
        public ComboBox CameraEffectSelect => _cameraEffectSelect;
        public ComboBox EmotionSelect => _emotionSelect;
        public ComboBox ExpressionSelect => _expressionSelect;

        // Expose speaker widgets for testing
        // Matching PyKotor implementation: editor.ui.speakerEdit, editor.ui.speakerEditLabel
        public TextBox SpeakerEdit => _speakerEdit;
        public TextBlock SpeakerEditLabel => _speakerEditLabel;

        // Expose listener widget for testing
        // Expose comments widget for testing
        // Matching PyKotor implementation: editor.ui.commentsEdit
        public TextBox CommentsEdit => _commentsEdit;

        // Matching PyKotor implementation: editor.ui.listenerEdit
        public TextBox ListenerEdit => _listenerEdit;

        // Expose find/search widgets for testing
        // Matching PyKotor implementation: editor.find_input, editor.show_find_bar(), editor.handle_find()
        public TextBox FindInput => _findInput;
        public Button FindButton => _findButton;
        public TextBlock ResultsLabel => _resultsLabel;


        // File-level properties exposed for testing
        // Matching PyKotor implementation - expose UI controls for testing
        public ComboBox ConversationSelect => _conversationSelect;
        public ComboBox ComputerSelect => _computerSelect;
        public NumericUpDown EntryDelaySpin => _entryDelaySpin;
        public NumericUpDown ReplyDelaySpin => _replyDelaySpin;
        public ComboBox OnAbortCombo => _onAbortCombo;
        public ComboBox OnEndEdit => _onEndEdit;
        public ComboBox CameraModelSelect => _cameraModelSelect;
        public TextBox VoIdEdit => _voIdEdit;
        public ComboBox AmbientTrackCombo => _ambientTrackCombo;
        public CheckBox SkippableCheckbox => _skippableCheckbox;
        public CheckBox AnimatedCutCheckbox => _animatedCutCheckbox;
        public CheckBox OldHitCheckbox => _oldHitCheckbox;
        public CheckBox UnequipHandsCheckbox => _unequipHandsCheckbox;
        public CheckBox UnequipAllCheckbox => _unequipAllCheckbox;

        // Expose left dock widget for testing
        // Matching PyKotor implementation: editor.left_dock_widget, editor.orphaned_nodes_list, editor.pinned_items_list
        public Panel LeftDockWidget => _leftDockWidget;
        public DLGListWidget OrphanedNodesList => _orphanedNodesList;
        public DLGListWidget PinnedItemsList => _pinnedItemsList;

        // Expose script widgets for testing
        // Matching PyKotor implementation: editor.ui.script1Param1Spin
        public NumericUpDown Script1Param1Spin => _script1Param1Spin;

        // Expose sound widget for testing
        // Matching PyKotor implementation: editor.ui.soundComboBox
        public ComboBox SoundComboBox => _soundComboBox;

        // Expose voice widget for testing
        // Matching PyKotor implementation: editor.ui.voiceComboBox
        public ComboBox VoiceComboBox => _voiceComboBox;
        public CheckBox SoundCheckbox => _soundCheckbox;
        public NumericUpDown NodeIdSpin => _nodeIdSpin;
        public NumericUpDown AlienRaceNodeSpin => _alienRaceNodeSpin;
        public NumericUpDown PostProcSpin => _postProcSpin;
        public CheckBox NodeUnskippableCheckbox => _nodeUnskippableCheckbox;

        // Expose VO ID widget for testing

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
                if (_condition1Param1Spin != null)
                {
                    _condition1Param1Spin.Value = 0;
                }
                if (_condition1Param2Spin != null)
                {
                    _condition1Param2Spin.Value = 0;
                }
                if (_condition1Param3Spin != null)
                {
                    _condition1Param3Spin.Value = 0;
                }
                if (_condition1Param4Spin != null)
                {
                    _condition1Param4Spin.Value = 0;
                }
                if (_condition1Param5Spin != null)
                {
                    _condition1Param5Spin.Value = 0;
                }
                if (_condition1Param6Edit != null)
                {
                    _condition1Param6Edit.Text = string.Empty;
                }
                if (_condition1NotCheckbox != null)
                {
                    _condition1NotCheckbox.IsChecked = false;
                }
                if (_condition2ResrefEdit != null)
                {
                    _condition2ResrefEdit.Text = string.Empty;
                }
                if (_condition2Param1Spin != null)
                {
                    _condition2Param1Spin.Value = 0;
                }
                if (_condition2Param2Spin != null)
                {
                    _condition2Param2Spin.Value = 0;
                }
                if (_condition2Param3Spin != null)
                {
                    _condition2Param3Spin.Value = 0;
                }
                if (_condition2Param4Spin != null)
                {
                    _condition2Param4Spin.Value = 0;
                }
                if (_condition2Param5Spin != null)
                {
                    _condition2Param5Spin.Value = 0;
                }
                if (_condition2Param6Edit != null)
                {
                    _condition2Param6Edit.Text = string.Empty;
                }
                if (_condition2NotCheckbox != null)
                {
                    _condition2NotCheckbox.IsChecked = false;
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
                if (_plotIndexCombo != null)
                {
                    _plotIndexCombo.SelectedIndex = 0;
                }
                if (_plotXpSpin != null)
                {
                    _plotXpSpin.Value = 0;
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
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2364-2454
            // Original: self.ui.condition1ResrefEdit.set_combo_box_text(str(item.link.active1))
            // Original: self.ui.condition1Param1Spin.setValue(item.link.active1_param1)
            // Original: self.ui.condition1NotCheckbox.setChecked(item.link.active1_not)
            if (_condition1ResrefEdit != null)
            {
                _condition1ResrefEdit.Text = link.Active1?.ToString() ?? string.Empty;
            }
            if (_condition1Param1Spin != null)
            {
                _condition1Param1Spin.Value = link.Active1Param1;
            }
            if (_condition1Param2Spin != null)
            {
                _condition1Param2Spin.Value = link.Active1Param2;
            }
            if (_condition1Param3Spin != null)
            {
                _condition1Param3Spin.Value = link.Active1Param3;
            }
            if (_condition1Param4Spin != null)
            {
                _condition1Param4Spin.Value = link.Active1Param4;
            }
            if (_condition1Param5Spin != null)
            {
                _condition1Param5Spin.Value = link.Active1Param5;
            }
            if (_condition1Param6Edit != null)
            {
                _condition1Param6Edit.Text = link.Active1Param6 ?? string.Empty;
            }
            if (_condition1NotCheckbox != null)
            {
                _condition1NotCheckbox.IsChecked = link.Active1Not;
            }

            // Load condition2
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2364-2454
            // Original: self.ui.condition2ResrefEdit.set_combo_box_text(str(item.link.active2))
            // Original: self.ui.condition2Param1Spin.setValue(item.link.active2_param1)
            // Original: self.ui.condition2NotCheckbox.setChecked(item.link.active2_not)
            if (_condition2ResrefEdit != null)
            {
                _condition2ResrefEdit.Text = link.Active2?.ToString() ?? string.Empty;
            }
            if (_condition2Param1Spin != null)
            {
                _condition2Param1Spin.Value = link.Active2Param1;
            }
            if (_condition2Param2Spin != null)
            {
                _condition2Param2Spin.Value = link.Active2Param2;
            }
            if (_condition2Param3Spin != null)
            {
                _condition2Param3Spin.Value = link.Active2Param3;
            }
            if (_condition2Param4Spin != null)
            {
                _condition2Param4Spin.Value = link.Active2Param4;
            }
            if (_condition2Param5Spin != null)
            {
                _condition2Param5Spin.Value = link.Active2Param5;
            }
            if (_condition2Param6Edit != null)
            {
                _condition2Param6Edit.Text = link.Active2Param6 ?? string.Empty;
            }
            if (_condition2NotCheckbox != null)
            {
                _condition2NotCheckbox.IsChecked = link.Active2Not;
            }

            // Load logic (0 = AND/false, 1 = OR/true)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2364-2454
            // Original: self.ui.logicSpin.setValue(1 if item.link.logic else 0)
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

            // Load listener field from node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2407
            // Original: self.ui.listenerEdit.setText(item.link.node.listener)
            if (_listenerEdit != null && node != null)
            {
                _listenerEdit.Text = node.Listener ?? string.Empty;
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

            // Load plot fields from node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2431-2432
            // Original: self.ui.plotIndexCombo.setCurrentIndex(item.link.node.plot_index), self.ui.plotXpSpin.setValue(item.link.node.plot_xp_percentage)
            if (_plotIndexCombo != null && node != null)
            {
                _plotIndexCombo.SelectedIndex = node.PlotIndex;
            }

            if (_plotXpSpin != null && node != null)
            {
                _plotXpSpin.Value = (decimal)node.PlotXpPercentage;
            }

            // Load script1 and script2 ResRefs
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2409, 2416
            // Original: self.ui.script1ResrefEdit.set_combo_box_text(str(item.link.node.script1)), self.ui.script2ResrefEdit.set_combo_box_text(str(item.link.node.script2))
            if (_script1ResrefEdit != null && node != null)
            {
                _script1ResrefEdit.Text = node.Script1?.ToString() ?? string.Empty;
            }
            if (_script2ResrefEdit != null && node != null)
            {
                _script2ResrefEdit.Text = node.Script2?.ToString() ?? string.Empty;
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

            // Load sound ResRef from node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2425
            // Original: self.ui.soundComboBox.set_combo_box_text(str(item.link.node.sound))
            if (_soundComboBox != null && node != null)
            {
                _soundComboBox.Text = node.Sound?.ToString() ?? string.Empty;
            }

            // Original: self.ui.soundCheckbox.setChecked(item.link.node.sound_exists)
            if (_soundCheckbox != null && node != null)
            {
                _soundCheckbox.IsChecked = node.SoundExists != 0;
            }

            // Original: self.ui.emotionSelect.setCurrentIndex(item.link.node.emotion_id)
            if (_emotionSelect != null && node != null)
            {
                _emotionSelect.SelectedIndex = Math.Min(Math.Max(node.EmotionId, 0), _emotionSelect.Items.Count - 1);
            }

            // Original: self.ui.expressionSelect.setCurrentIndex(item.link.node.facial_id)
            if (_expressionSelect != null && node != null)
            {
                _expressionSelect.SelectedIndex = Math.Min(Math.Max(node.FacialId, 0), _expressionSelect.Items.Count - 1);
            }

            // Original: self.ui.nodeIdSpin.setValue(item.link.node.node_id)
            if (_nodeIdSpin != null && node != null)
            {
                _nodeIdSpin.Value = node.NodeId;
            }

            // Original: self.ui.alienRaceNodeSpin.setValue(item.link.node.alien_race_node)
            if (_alienRaceNodeSpin != null && node != null)
            {
                _alienRaceNodeSpin.Value = node.AlienRaceNode;
            }

            // Original: self.ui.postProcSpin.setValue(item.link.node.post_proc_node)
            if (_postProcSpin != null && node != null)
            {
                _postProcSpin.Value = node.PostProcNode;
            }

            // Original: self.ui.nodeUnskippableCheckbox.setChecked(item.link.node.unskippable)
            if (_nodeUnskippableCheckbox != null && node != null)
            {
                _nodeUnskippableCheckbox.IsChecked = node.Unskippable;
            }

            // Load voice ResRef from node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2429
            // Original: self.ui.voiceComboBox.set_combo_box_text(str(item.link.node.vo_resref))
            if (_voiceComboBox != null && node != null)
            {
                _voiceComboBox.Text = node.VoResRef?.ToString() ?? string.Empty;
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
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2477-2484
            // Original: item.link.active1 = ResRef(self.ui.condition1ResrefEdit.currentText())
            // Original: item.link.active1_param1 = self.ui.condition1Param1Spin.value()
            // Original: item.link.active1_not = self.ui.condition1NotCheckbox.isChecked()
            if (_condition1ResrefEdit != null)
            {
                string text = _condition1ResrefEdit.Text ?? string.Empty;
                link.Active1 = string.IsNullOrEmpty(text) ? ResRef.FromBlank() : new ResRef(text);
            }
            if (_condition1Param1Spin != null)
            {
                link.Active1Param1 = _condition1Param1Spin.Value.HasValue ? (int)_condition1Param1Spin.Value.Value : 0;
            }
            if (_condition1Param2Spin != null)
            {
                link.Active1Param2 = _condition1Param2Spin.Value.HasValue ? (int)_condition1Param2Spin.Value.Value : 0;
            }
            if (_condition1Param3Spin != null)
            {
                link.Active1Param3 = _condition1Param3Spin.Value.HasValue ? (int)_condition1Param3Spin.Value.Value : 0;
            }
            if (_condition1Param4Spin != null)
            {
                link.Active1Param4 = _condition1Param4Spin.Value.HasValue ? (int)_condition1Param4Spin.Value.Value : 0;
            }
            if (_condition1Param5Spin != null)
            {
                link.Active1Param5 = _condition1Param5Spin.Value.HasValue ? (int)_condition1Param5Spin.Value.Value : 0;
            }
            if (_condition1Param6Edit != null)
            {
                link.Active1Param6 = _condition1Param6Edit.Text ?? string.Empty;
            }
            if (_condition1NotCheckbox != null)
            {
                link.Active1Not = _condition1NotCheckbox.IsChecked.HasValue && _condition1NotCheckbox.IsChecked.Value;
            }

            // Update condition2
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2485-2492
            // Original: item.link.active2 = ResRef(self.ui.condition2ResrefEdit.currentText())
            // Original: item.link.active2_param1 = self.ui.condition2Param1Spin.value()
            // Original: item.link.active2_not = self.ui.condition2NotCheckbox.isChecked()
            if (_condition2ResrefEdit != null)
            {
                string text = _condition2ResrefEdit.Text ?? string.Empty;
                link.Active2 = string.IsNullOrEmpty(text) ? ResRef.FromBlank() : new ResRef(text);
            }
            if (_condition2Param1Spin != null)
            {
                link.Active2Param1 = _condition2Param1Spin.Value.HasValue ? (int)_condition2Param1Spin.Value.Value : 0;
            }
            if (_condition2Param2Spin != null)
            {
                link.Active2Param2 = _condition2Param2Spin.Value.HasValue ? (int)_condition2Param2Spin.Value.Value : 0;
            }
            if (_condition2Param3Spin != null)
            {
                link.Active2Param3 = _condition2Param3Spin.Value.HasValue ? (int)_condition2Param3Spin.Value.Value : 0;
            }
            if (_condition2Param4Spin != null)
            {
                link.Active2Param4 = _condition2Param4Spin.Value.HasValue ? (int)_condition2Param4Spin.Value.Value : 0;
            }
            if (_condition2Param5Spin != null)
            {
                link.Active2Param5 = _condition2Param5Spin.Value.HasValue ? (int)_condition2Param5Spin.Value.Value : 0;
            }
            if (_condition2Param6Edit != null)
            {
                link.Active2Param6 = _condition2Param6Edit.Text ?? string.Empty;
            }
            if (_condition2NotCheckbox != null)
            {
                link.Active2Not = _condition2NotCheckbox.IsChecked.HasValue && _condition2NotCheckbox.IsChecked.Value;
            }

            // Update logic (0 = AND/false, 1 = OR/true)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2493
            // Original: item.link.logic = bool(self.ui.logicSpin.value())
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

            // Update listener field in node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2495
            // Original: item.link.node.listener = self.ui.listenerEdit.text()
            if (_listenerEdit != null && node != null)
            {
                node.Listener = _listenerEdit.Text ?? string.Empty;
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

            // Update plot fields in node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2519-2520
            // Original: item.link.node.plot_index = self.ui.plotIndexCombo.currentIndex(), item.link.node.plot_xp_percentage = self.ui.plotXpSpin.value()
            if (_plotIndexCombo != null && node != null)
            {
                node.PlotIndex = _plotIndexCombo.SelectedIndex;
            }

            if (_plotXpSpin != null && node != null)
            {
                node.PlotXpPercentage = _plotXpSpin.Value.HasValue ? (int)_plotXpSpin.Value.Value : 0;
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

            // Update sound ResRef in node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2513
            // Original: item.link.node.sound = ResRef(self.ui.soundComboBox.currentText())
            if (_soundComboBox != null && node != null)
            {
                string soundText = _soundComboBox.Text ?? string.Empty;
                node.Sound = string.IsNullOrEmpty(soundText) ? ResRef.FromBlank() : new ResRef(soundText);
            }

            // Original: item.link.node.sound_exists = self.ui.soundCheckbox.isChecked()
            if (_soundCheckbox != null && node != null)
            {
                node.SoundExists = _soundCheckbox.IsChecked == true ? 1 : 0;
            }

            // Original: item.link.node.emotion_id = self.ui.emotionSelect.currentIndex()
            if (_emotionSelect != null && node != null)
            {
                node.EmotionId = _emotionSelect?.SelectedIndex ?? 0;
            }

            // Original: item.link.node.facial_id = self.ui.expressionSelect.currentIndex()
            if (_expressionSelect != null && node != null)
            {
                node.FacialId = _expressionSelect?.SelectedIndex ?? 0;
            }

            // Original: item.link.node.node_id = self.ui.nodeIdSpin.value()
            if (_nodeIdSpin != null && node != null)
            {
                node.NodeId = (int)(_nodeIdSpin.Value ?? 0);
            }

            // Original: item.link.node.alien_race_node = self.ui.alienRaceNodeSpin.value()
            if (_alienRaceNodeSpin != null && node != null)
            {
                node.AlienRaceNode = (int)(_alienRaceNodeSpin.Value ?? 0);
            }

            // Original: item.link.node.post_proc_node = self.ui.postProcSpin.value()
            if (_postProcSpin != null && node != null)
            {
                node.PostProcNode = (int)(_postProcSpin.Value ?? 0);
            }

            // Original: item.link.node.unskippable = self.ui.nodeUnskippableCheckbox.isChecked()
            if (_nodeUnskippableCheckbox != null && node != null)
            {
                node.Unskippable = _nodeUnskippableCheckbox.IsChecked == true;
            }

            // Update voice ResRef in node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2517
            // Original: item.link.node.vo_resref = ResRef(self.ui.voiceComboBox.currentText())
            if (_voiceComboBox != null && node != null)
            {
                string voiceText = _voiceComboBox.Text ?? string.Empty;
                node.VoResRef = string.IsNullOrEmpty(voiceText) ? ResRef.FromBlank() : new ResRef(voiceText);
            }

            // Update camera properties in node
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2524-2527
            // Original: item.link.node.camera_id = self.ui.cameraIdSpin.value(), item.link.node.camera_anim = self.ui.cameraAnimSpin.value(), item.link.node.camera_angle = self.ui.cameraAngleSelect.currentIndex(), item.link.node.camera_effect = self.ui.cameraEffectSelect.currentIndex()
            if (_cameraIdSpin != null && node != null)
            {
                node.CameraId = _cameraIdSpin.Value.HasValue ? (int?)_cameraIdSpin.Value.Value : null;
            }

            if (_cameraAnimSpin != null && node != null)
            {
                node.CameraAnim = _cameraAnimSpin.Value.HasValue ? (int?)_cameraAnimSpin.Value.Value : null;
            }

            if (_cameraAngleSelect != null && node != null)
            {
                node.CameraAngle = _cameraAngleSelect.SelectedIndex >= 0 ? _cameraAngleSelect.SelectedIndex : 0;
            }

            if (_cameraEffectSelect != null && node != null)
            {
                node.CameraEffect = _cameraEffectSelect.SelectedIndex >= 0 ? (int?)_cameraEffectSelect.SelectedIndex : null;
            }

            // Handle camera ID and angle interaction (matching Python logic)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:2529-2532
            // Original: if item.link.node.camera_id >= 0 and item.link.node.camera_angle == 0: self.ui.cameraAngleSelect.setCurrentIndex(6), elif item.link.node.camera_id == -1 and item.link.node.camera_angle == 7: self.ui.cameraAngleSelect.setCurrentIndex(0)
            if (_cameraIdSpin != null && _cameraAngleSelect != null && node != null)
            {
                int? cameraId = _cameraIdSpin.Value.HasValue ? (int?)_cameraIdSpin.Value.Value : null;
                int cameraAngle = _cameraAngleSelect.SelectedIndex >= 0 ? _cameraAngleSelect.SelectedIndex : 0;
                if (cameraId.HasValue && cameraId.Value >= 0 && cameraAngle == 0)
                {
                    _cameraAngleSelect.SelectedIndex = 6;
                    node.CameraAngle = 6;
                }
                else if (cameraId.HasValue && cameraId.Value == -1 && cameraAngle == 7)
                {
                    _cameraAngleSelect.SelectedIndex = 0;
                    node.CameraAngle = 0;
                }
            }
        }

        /// <summary>
        /// Updates file-level properties (root DLG fields) based on UI changes.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1233
        /// Original: self.core_dlg.vo_id = self.ui.voIdEdit.text() (called during build, but we update immediately for consistency)
        /// </summary>
        private void OnFilePropertyChanged()
        {
            if (_coreDlg == null)
            {
                return;
            }

            // Update VO ID from UI
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:1233
            // Original: self.core_dlg.vo_id = self.ui.voIdEdit.text()
            if (_voIdEdit != null)
            {
                _coreDlg.VoId = _voIdEdit.Text ?? string.Empty;
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
        /// Selects the specified DLGStandardItem in the tree view.
        /// </summary>
        /// <param name="item">The DLGStandardItem to select.</param>
        public void SelectTreeItem(DLGStandardItem item)
        {
            if (_dialogTree == null || _dialogTree.ItemsSource == null || item == null)
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
            // TODO: STUB - For now, we handle all key events at the window level
            // TODO:  In a full implementation, we would check if dialogTree has focus

            // Matching PyKotor implementation: if not selected_index.isValid(): return
            // TODO: STUB - For now, we'll handle keys even without selection (for Insert key to add root node)

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
                    // TODO: STUB - For now, we'll just handle the basic case
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
                int newIndex = -1;
                for (int i = 0; i < rootItems.Count; i++)
                {
                    if (rootItems[i] == newItem)
                    {
                        newIndex = i;
                        break;
                    }
                }
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
                // Matching PyKotor: RobustLogger().error("Cannot edit text: installation is not set")
                new RobustLogger().Error("Cannot edit text: installation is not set");
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
                            var activeWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
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
                    var activeWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
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
                    new RobustLogger().Exception($"Error executing LocalizedStringDialog: {exc.GetType().Name}: {exc}", exc);
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
                new RobustLogger().Exception($"Error creating LocalizedStringDialog: {exc.GetType().Name}: {exc}", exc);
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

            // Get sound and voice resource names from UI combo boxes
            string soundResname = _soundComboBox?.Text?.Trim() ?? string.Empty;
            string voiceResname = _voiceComboBox?.Text?.Trim() ?? string.Empty;

            // If sound combo box has text, play sound with SOUND and VOICE search locations
            if (!string.IsNullOrEmpty(soundResname))
            {
                PlaySound(soundResname, new[] { SearchLocation.SOUND, SearchLocation.VOICE });
            }
            // Else if voice combo box has text, play voice with VOICE search location
            else if (!string.IsNullOrEmpty(voiceResname))
            {
                PlaySound(voiceResname, new[] { SearchLocation.VOICE });
            }
            // Else blink window to indicate no playable sound
            else
            {
                BlinkWindow();
            }
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
        /// Sets up the menu bar with File and Tools menus.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/uic/qtpy/editors/dlg.py
        /// </summary>
        private void SetupMenuBar(DockPanel dockPanel)
        {
            var menuBar = new Menu();
            dockPanel.Children.Add(menuBar);
            DockPanel.SetDock(menuBar, Dock.Top);

            // File menu
            var fileMenu = new MenuItem { Header = "_File" };
            menuBar.Items.Add(fileMenu);

            // File menu actions
            _actionNew = new MenuItem { Header = "_New" };
            _actionOpen = new MenuItem { Header = "_Open" };
            _actionSave = new MenuItem { Header = "_Save" };
            _actionSaveAs = new MenuItem { Header = "Save _As" };
            _actionRevert = new MenuItem { Header = "_Revert" };
            _actionExit = new MenuItem { Header = "E_xit" };

            fileMenu.Items.Add(_actionNew);
            fileMenu.Items.Add(_actionOpen);
            fileMenu.Items.Add(_actionSave);
            fileMenu.Items.Add(_actionSaveAs);
            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(_actionRevert);
            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(_actionExit);

            // Tools menu
            var toolsMenu = new MenuItem { Header = "_Tools" };
            menuBar.Items.Add(toolsMenu);

            // Tools menu actions
            _actionReloadTree = new MenuItem { Header = "_Reload Tree" };
            _actionUnfocus = new MenuItem { Header = "_Unfocus Tree" };
            _actionUnfocus.IsEnabled = false;

            toolsMenu.Items.Add(_actionReloadTree);
            toolsMenu.Items.Add(_actionUnfocus);

            // Connect action events
            SetupMenuActionHandlers();
        }

        /// <summary>
        /// Sets up event handlers for menu actions.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:304-306
        /// </summary>
        private void SetupMenuActionHandlers()
        {
            // File menu actions
            _actionNew.Click += (s, e) => New();
            _actionOpen.Click += (s, e) => OpenFile();
            _actionSave.Click += (s, e) => Save();
            _actionSaveAs.Click += (s, e) => SaveAs();
            _actionRevert.Click += (s, e) => RevertChanges();
            _actionExit.Click += (s, e) => Close();

            // Tools menu actions
            _actionReloadTree.Click += (s, e) => ReloadTree();
            _actionUnfocus.Click += (s, e) => UnfocusTree();
        }

        /// <summary>
        /// Reloads the dialog tree from the current core DLG.
        /// Matching PyKotor implementation: self.ui.actionReloadTree.triggered.connect(lambda: self._load_dlg(self.core_dlg))
        /// </summary>
        private void ReloadTree()
        {
            LoadDLG(_coreDlg);
        }

        /// <summary>
        /// Unfocuses the current tree selection.
        /// Matching PyKotor implementation for unfocus tree action.
        /// </summary>
        private void UnfocusTree()
        {
            // Clear selection in the dialog tree
            if (_dialogTree != null)
            {
                _dialogTree.SelectedItem = null;
            }
            _focused = false;
        }

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
            // TODO: STUB - For now, we'll skip the completer setup - it can be added later if needed
            // Matching PyKotor: self.find_input_completer: QCompleter = QCompleter(suggestions, self.find_input)
            // Matching PyKotor: self.find_input.setCompleter(self.find_input_completer)
        }

        /// <summary>
        /// Shows the find bar.
        /// Matching PyKotor implementation: self.show_find_bar()
        /// Original: def show_find_bar(self): self.find_bar.setVisible(True); self.find_input.setFocus()
        /// </summary>
        public void ShowFindBar()
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
        public void HandleFind()
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

            string logicalOperator = null;
            int i = 0;
            while (i < tokens.Count)
            {
                string token = tokens[i].ToUpperInvariant();
                if (token == "AND" || token == "OR")
                {
                    logicalOperator = token;
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
                    conditions.Add(Tuple.Create(key, value ?? "", logicalOperator));
                    logicalOperator = null;
                }
                else if (nextIndex.HasValue && (tokens[nextIndex.Value].ToUpperInvariant() == "AND" || tokens[nextIndex.Value].ToUpperInvariant() == "OR"))
                {
                    // Text search with operator
                    conditions.Add(Tuple.Create(tokens[i], "", logicalOperator));
                    logicalOperator = null;
                }
                else if (!nextIndex.HasValue)
                {
                    // Last token
                    conditions.Add(Tuple.Create(tokens[i], "", logicalOperator));
                    logicalOperator = null;
                }
                else
                {
                    // Text search without operator
                    conditions.Add(Tuple.Create(tokens[i], "", logicalOperator));
                    logicalOperator = null;
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
                                // TODO: STUB - Note: Accessing _substrings would require reflection or public API
                                // TODO: STUB - For now, check if text exists (simplified)
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

            // Find the TreeViewItem corresponding to this DLGStandardItem
            TreeViewItem treeItem = FindTreeViewItem(_dialogTree.ItemsSource as System.Collections.IEnumerable, item);
            if (treeItem != null)
            {
                // Expand all parent items in the visual tree to ensure visibility
                ExpandParentItems(treeItem);
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
                new RobustLogger().Error($"Failed to find paths for node: {ex.Message}", true, ex);
                BlinkWindow();
                return;
            }

            // Matching PyKotor: if not paths: RobustLogger().error("No paths available."), self.blink_window(), return
            if (paths == null || paths.Count == 0)
            {
                // Matching PyKotor: No paths available - log error and blink window
                new RobustLogger().Error("No paths available.");
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
                // TODO:  In a full implementation, we might want to show an error message
            }
        }

        /// <summary>
        /// Copies the link and node.
        /// Matching PyKotor implementation: self.model.copy_link_and_node(selected_item.link)
        /// </summary>
        private async void CopyLinkAndNode()
        {
            if (_model.SelectedIndex >= 0 && _model.SelectedIndex < _model.RowCount)
            {
                DLGLink link = _model.GetStarterAt(_model.SelectedIndex);
                if (link != null)
                {
                    // Matching PyKotor implementation: self.model.copy_link_and_node(selected_item.link)
                    // Note: CopyLinkAndNode on the model will also set _copy via SetCopyLink
                    await _model.CopyLinkAndNode(link, this);
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
                new RobustLogger().Error($"Failed to find original node for node {sourceNode}");
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
                    var rootSeenNodes = new HashSet<DLGNode>();
                    foreach (var rootItem in rootItems)
                    {
                        TreeViewItem rootTreeItem = FindTreeViewItem(_dialogTree.ItemsSource as System.Collections.IEnumerable, rootItem);
                        if (rootTreeItem != null)
                        {
                            SetExpandRecursivelyInternal(rootItem, rootTreeItem, rootSeenNodes, expand, maxDepth, 0, true);
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
                // TODO: STUB - For now, we'll try to find the parent path by traversing the tree
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
}

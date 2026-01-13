using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using BioWare.NET.Resource;
using BioWare.NET.Tools;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.Capsule;
using HolocronToolset.Common;
using HolocronToolset.Common.Widgets;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Widgets;
using HolocronToolset.Utils;
using System.Text.Json;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using BioWare.NET.Extract;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:115
    // Original: class NSSEditor(Editor):
    public class NSSEditor : Editor
    {
        private CodeEditor _codeEdit;
        private TerminalWidget _terminalWidget;
        private TextBox _outputEdit;
        private bool _isDecompiled;
        private NoScrollEventFilter _noScrollFilter;
        private FindReplaceWidget _findReplaceWidget;
        private TreeView _bookmarkTree;
        private TreeView _outlineView;
        private ListBox _functionList;
        private ListBox _constantList;
        private ListBox _snippetList;
        private TextBox _functionSearchEdit;
        private TextBox _constantSearchEdit;
        private ComboBox _gameSelector;
        private BreadcrumbsWidget _breadcrumbs;
        private bool _isTsl;
        private List<ScriptFunction> _functions;
        private List<ScriptConstant> _constants;
        private GlobalSettings _globalSettings;
        private string _owner;
        private string _repo;
        private string _sourcerepoUrl;
        private Label _statusLabel;
        private Border _statusBar;

        // Tab formatting constants
        private const bool TAB_AS_SPACE = true; // Use spaces instead of tabs
        private const int TAB_SIZE = 4; // Number of spaces per tab

        // Panel container structure for output, terminal, etc.
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        // Original: self.ui.mainSplitter (QSplitter), self.ui.panelTabs (QTabWidget)
        private Grid _mainSplitter;  // Grid used as splitter (Avalonia equivalent of QSplitter)
        private TabControl _panelTabs;  // TabControl for panels (output, terminal, etc.)
        private TabItem _outputTab;  // Output panel tab
        private Control _mainContentContainer;  // Container for main content (code editor)

        // Error and warning line tracking
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:178-179
        // Original: self._error_lines: set[int] = set()  # Line numbers with errors (1-indexed)
        // Original: self._warning_lines: set[int] = set()  # Line numbers with warnings (1-indexed)
        private HashSet<int> _errorLines;  // Line numbers with errors (1-indexed)
        private HashSet<int> _warningLines;  // Line numbers with warnings (1-indexed)

        // Command palette
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2535
        // Original: self._command_palette = CommandPalette(self)
        private CommandPalette _commandPalette;

        // File explorer components
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1351-1398
        // Original: self.file_system_model = QFileSystemModel()
        private NSSEditorFileSystemModel _fileSystemModel;
        private TreeView _fileExplorerView;
        private TextBox _fileExplorerAddressBar;
        private TextBox _fileSearchEdit;
        private Button _refreshFileExplorerButton;
        // File explorer dock/panel container
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2739-2741
        // Original: self.ui.fileExplorerDock.setVisible(not self.ui.fileExplorerDock.isVisible())
        private Panel _fileExplorerDock;

        // Bookmarks and snippets dock/panel containers
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2743-2747
        // Original: self.ui.bookmarksDock.setVisible(not is_visible)
        // Original: self.ui.snippetsDock.setVisible(not is_visible)
        private Panel _bookmarksDock;
        private Panel _snippetsDock;

        // UI wrapper class to match PyKotor's self.ui pattern
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:136
        // Original: self.ui = Ui_MainWindow()
        public class NSSEditorUi
        {
            public MenuItem ActionCompile { get; set; }
            public TextBox OutputEdit { get; set; }

            public NSSEditorUi()
            {
                ActionCompile = null;
                OutputEdit = null;
            }
        }

        private NSSEditorUi _ui;

        // Public property to expose UI for testing and external access
        // Matching PyKotor implementation: self.ui.actionCompile
        public NSSEditorUi Ui => _ui;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:156
        // Original: self._highlighter: SyntaxHighlighter = SyntaxHighlighter(document, self._installation)
        private NWScriptSyntaxHighlighter _highlighter;
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:469-473
        // Original: self.completer: QCompleter = QCompleter(self)
        private Completer _completer;
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1576
        // Original: self._completion_map: dict[str, Any] = {}
        private Dictionary<string, object> _completionMap;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:119-199
        // Original: def __init__(self, parent: QWidget | None = None, installation: HTInstallation | None = None):
        public NSSEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Script Editor", "script",
                new[] { ResourceType.NSS, ResourceType.NCS },
                new[] { ResourceType.NSS, ResourceType.NCS },
                installation)
        {
            _installation = installation;
            _isDecompiled = false;
            _isTsl = installation?.Tsl ?? false;
            _functions = new List<ScriptFunction>();
            _constants = new List<ScriptConstant>();
            _globalSettings = new GlobalSettings();
            _completionMap = new Dictionary<string, object>();
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:178-179
            // Original: self._error_lines: set[int] = set()  # Line numbers with errors (1-indexed)
            // Original: self._warning_lines: set[int] = set()  # Line numbers with warnings (1-indexed)
            _errorLines = new HashSet<int>();  // Line numbers with errors (1-indexed)
            _warningLines = new HashSet<int>();  // Line numbers with warnings (1-indexed)

            // Initialize UI wrapper
            _ui = new NSSEditorUi();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:164-167
            // Original: self.owner: str = "KOTORCommunityPatches"
            _owner = "KOTORCommunityPatches";
            _repo = "Vanilla_KOTOR_Script_Source";
            _sourcerepoUrl = $"https://github.com/{_owner}/{_repo}";

            InitializeComponent();
            SetupUI();
            SetupSyntaxHighlighter();
            SetupTerminal();
            SetupSignals();
            SetupFindReplaceWidget();
            SetupBookmarks();
            SetupSnippets();
            SetupFunctionList();
            SetupBreadcrumbs();
            SetupOutline();
            SetupFileExplorer();
            AddHelpAction();
            SetupStatusBar();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2534-2542
            // Original: Command Palette (VS Code Ctrl+Shift+P)
            // Original: self._command_palette = CommandPalette(self)
            // Original: self._setup_command_palette()
            SetupCommandPalette();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:469-514
            // Original: Setup completer for autocompletion
            SetupCompleter();
            SetupEnhancedCompleter();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:145-148
            // Original: Setup scrollbar event filter to prevent scrollbar interaction with controls
            // Setup scrollbar event filter to prevent scrollbar interaction with controls
            _noScrollFilter = new NoScrollEventFilter();
            _noScrollFilter.SetupFilter(this);

            // Set Content after AddHelpAction (which may wrap it in a DockPanel)
            if (Content == null && _codeEdit != null)
            {
                Content = _codeEdit;
            }

            New();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;

                // Try to find code editor from XAML
                _codeEdit = this.FindControl<CodeEditor>("codeEdit");
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }

            if (!xamlLoaded)
            {
                SetupUI();
            }
        }

        private void SetupUI()
        {
            // Create code editor if not found from XAML
            if (_codeEdit == null)
            {
                _codeEdit = new CodeEditor();
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:479-484
            // Original: self.output_text_edit = self.ui.outputEdit
            // Original: self.output_text_edit.setReadOnly(True)
            // Set up output panel for logging compilation results and other messages
            SetupOutputPanel();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:479-484
        // Original: Use the existing outputEdit widget from UI file with proper encoding
        // Original: self.output_text_edit = self.ui.outputEdit
        // Original: self.output_text_edit.setReadOnly(True)
        // Original: configure_code_editor_font(self.output_text_edit, size=11)
        /// <summary>
        /// Sets up the output panel for displaying compilation results and log messages.
        /// The output panel is read-only and uses monospace font for better readability.
        /// </summary>
        private void SetupOutputPanel()
        {
            // Try to find output edit from XAML first (only if we're in a visual tree)
            _outputEdit = null;
            try
            {
                _outputEdit = this.FindControl<TextBox>("outputEdit");
            }
            catch (InvalidOperationException)
            {
                // Not in a visual tree (e.g., in unit tests) - will create programmatically
                _outputEdit = null;
            }

            // If not found in XAML, create programmatically
            if (_outputEdit == null)
            {
                _outputEdit = new TextBox();
                _outputEdit.Name = "outputEdit";
            }

            // Configure as read-only output panel
            _outputEdit.IsReadOnly = true;
            _outputEdit.AcceptsReturn = true;
            _outputEdit.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

            // Set monospace font for code-like output (matching Python's configure_code_editor_font with size=11)
            _outputEdit.FontFamily = new Avalonia.Media.FontFamily("Consolas, Courier New, monospace");
            _outputEdit.FontSize = 11;

            // Set in UI wrapper for test access (matching PyKotor's self.ui.outputEdit pattern)
            if (_ui != null)
            {
                _ui.OutputEdit = _outputEdit;
            }

            // Add output to panel tabs if panel container is already set up
            if (_panelTabs != null && _outputTab == null)
            {
                _outputTab = new TabItem { Header = "Output", Content = _outputEdit };
                _panelTabs.Items.Add(_outputTab);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        // Original: Panel container structure with mainSplitter (QSplitter) and panelTabs (QTabWidget)
        /// <summary>
        /// Sets up the panel container structure with a splitter for main content and panels.
        /// Creates a Grid-based splitter layout with TabControl for output, terminal, etc.
        /// </summary>
        private void SetupPanelContainer()
        {
            // Create main splitter (Grid with two rows: main content and panels)
            // Matching PyKotor: self.ui.mainSplitter (QSplitter with vertical orientation)
            _mainSplitter = new Grid();
            _mainSplitter.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _mainSplitter.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });  // Initially hidden (0 height)

            // Store current content as main content container
            _mainContentContainer = Content as Control;

            // Create panel tabs (TabControl for output, terminal, etc.)
            // Matching PyKotor: self.ui.panelTabs (QTabWidget)
            _panelTabs = new TabControl();
            _panelTabs.Name = "panelTabs";
            _panelTabs.IsVisible = false;  // Initially hidden

            // Add output tab if output panel is already set up
            // This handles the case where SetupOutputPanel was called before SetupPanelContainer
            if (_outputEdit != null)
            {
                _outputTab = new TabItem { Header = "Output", Content = _outputEdit };
                _panelTabs.Items.Add(_outputTab);
            }

            // Add terminal tab if terminal widget is already set up
            if (_terminalWidget != null)
            {
                var terminalTab = new TabItem { Header = "Terminal", Content = _terminalWidget };
                _panelTabs.Items.Add(terminalTab);
            }

            // Add main content to first row
            if (_mainContentContainer != null)
            {
                _mainSplitter.Children.Add(_mainContentContainer);
                Grid.SetRow(_mainContentContainer, 0);
            }

            // Add panel tabs to second row
            _mainSplitter.Children.Add(_panelTabs);
            Grid.SetRow(_panelTabs, 1);

            // Set the splitter as the new content
            Content = _mainSplitter;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2749-2759
        // Original: def _toggle_output_panel(self):
        /// <summary>
        /// Toggles the output panel visibility.
        /// Shows or hides the panel container (TabControl) by adjusting the splitter sizes.
        /// Matching PyKotor behavior: toggles panelTabs visibility via mainSplitter sizes.
        /// </summary>
        private void ToggleOutputPanel()
        {
            if (_mainSplitter == null || _panelTabs == null)
            {
                // Panel container not set up yet, initialize it
                SetupPanelContainer();
                if (_mainSplitter == null || _panelTabs == null)
                {
                    return;  // Still failed to set up
                }
            }

            // Matching PyKotor: if self.ui.panelTabs.isVisible()
            if (_panelTabs.IsVisible)
            {
                // Hide panel if visible
                // Matching PyKotor: self.ui.mainSplitter.setSizes([999999, 0])
                _mainSplitter.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                _mainSplitter.RowDefinitions[1].Height = new GridLength(0);
                _panelTabs.IsVisible = false;
            }
            else
            {
                // Show panel
                // Matching PyKotor: sizes = self.ui.mainSplitter.sizes()
                // Matching PyKotor: if sizes[1] == 0: self.ui.mainSplitter.setSizes([sizes[0] - 200, 200])
                var currentMainHeight = _mainSplitter.RowDefinitions[0].Height;
                double mainHeightValue = 200;  // Default panel height

                // If main content has a star height, calculate a reasonable split
                if (currentMainHeight.GridUnitType == GridUnitType.Star)
                {
                    // Set panel to 200px, main content takes remaining space
                    _mainSplitter.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                    _mainSplitter.RowDefinitions[1].Height = new GridLength(200);
                }
                else
                {
                    // Use pixel-based sizing
                    double totalHeight = currentMainHeight.Value;
                    if (totalHeight > 200)
                    {
                        _mainSplitter.RowDefinitions[0].Height = new GridLength(totalHeight - 200);
                        _mainSplitter.RowDefinitions[1].Height = new GridLength(200);
                    }
                    else
                    {
                        // Fallback: use star-based sizing
                        _mainSplitter.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                        _mainSplitter.RowDefinitions[1].Height = new GridLength(200);
                    }
                }

                _panelTabs.IsVisible = true;

                // Switch to output tab if available
                if (_outputTab != null && _panelTabs.Items.Contains(_outputTab))
                {
                    _panelTabs.SelectedItem = _outputTab;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:801-816
        // Original: def _log_to_output(self, message: str):
        /// <summary>
        /// Logs a message to the output panel with proper encoding.
        /// </summary>
        /// <param name="message">The message to log.</param>
        private void LogToOutput(string message)
        {
            if (_outputEdit == null)
            {
                return;
            }

            try
            {
                // Ensure proper text encoding - handle bytes if needed
                string textToAppend = message;
                if (message == null)
                {
                    textToAppend = "";
                }

                // Append text to output (matching Python's appendPlainText)
                // In Avalonia TextBox, we append by setting Text to current + new
                string currentText = _outputEdit.Text ?? "";
                if (currentText.Length > 0 && !currentText.EndsWith("\n") && !currentText.EndsWith("\r\n"))
                {
                    _outputEdit.Text = currentText + "\n" + textToAppend;
                }
                else
                {
                    _outputEdit.Text = currentText + textToAppend;
                }

                // Scroll to end to show latest output
                _outputEdit.CaretIndex = _outputEdit.Text?.Length ?? 0;
            }
            catch (Exception)
            {
                // Fallback if there's any encoding issue - try with ToString()
                try
                {
                    string currentText = _outputEdit.Text ?? "";
                    string textToAppend = message?.ToString() ?? "";
                    if (currentText.Length > 0 && !currentText.EndsWith("\n") && !currentText.EndsWith("\r\n"))
                    {
                        _outputEdit.Text = currentText + "\n" + textToAppend;
                    }
                    else
                    {
                        _outputEdit.Text = currentText + textToAppend;
                    }
                    _outputEdit.CaretIndex = _outputEdit.Text?.Length ?? 0;
                }
                catch
                {
                    // If all else fails, silently ignore (matching Python's exception handling)
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:156
        // Original: self._highlighter: SyntaxHighlighter = SyntaxHighlighter(document, self._installation)
        /// <summary>
        /// Sets up the syntax highlighter for the code editor.
        /// Initializes the highlighter with the code editor's document and installation.
        /// </summary>
        private void SetupSyntaxHighlighter()
        {
            if (_codeEdit != null)
            {
                // Get document from code editor (for compatibility with Python interface)
                object document = _codeEdit.Document();
                _highlighter = new NWScriptSyntaxHighlighter(document, _installation);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        // Original: Terminal widget setup for NSS editor
        // Based on industry-standard IDE patterns: integrated terminal for script execution and debugging
        private void SetupTerminal()
        {
            // Initialize terminal widget for script execution and debugging
            // Terminal allows users to run scripts, view compilation output, and execute commands
            _terminalWidget = new TerminalWidget();

            // Terminal widget is available but not automatically visible
            // Users can toggle terminal visibility via View menu or keyboard shortcut
            // This follows common IDE patterns (VS Code, Visual Studio, etc.)
        }

        // Public property to access terminal widget for testing and external access
        // Based on Avalonia control access patterns and testability best practices
        public TerminalWidget TerminalWidget => _terminalWidget;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:480
        // Original: self.output_text_edit = self.ui.outputEdit
        // Public property to access output text edit for testing and external access
        public TextBox OutputTextEdit => _outputEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:969-977
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            // Create and connect compile action
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2412-2413
            // Original: self.ui.actionCompile.setShortcut(QKeySequence("F5"))
            // Original: self.ui.actionCompile.triggered.connect(self.compile_current_script)
            _ui.ActionCompile = new MenuItem
            {
                Header = "Compile",
                HotKey = new KeyGesture(Avalonia.Input.Key.F5)
            };
            _ui.ActionCompile.Click += (s, e) => CompileCurrentScript();

            // Connect constant list double-click
            if (_constantList != null)
            {
                _constantList.DoubleTapped += (s, e) => InsertSelectedConstant();
            }

            // Connect function list double-click
            if (_functionList != null)
            {
                _functionList.DoubleTapped += (s, e) => InsertSelectedFunction();
            }

            // Connect function search text changed
            if (_functionSearchEdit != null)
            {
                _functionSearchEdit.TextChanged += (s, e) => OnFunctionSearch(_functionSearchEdit.Text ?? "");
            }

            // Connect constant search text changed
            if (_constantSearchEdit != null)
            {
                _constantSearchEdit.TextChanged += (s, e) => OnConstantSearch(_constantSearchEdit.Text ?? "");
            }

            // Connect code editor text changed events
            if (_codeEdit != null)
            {
                // Update status bar on text change
                _codeEdit.TextChanged += (s, e) => UpdateStatusBar();

                // Update status bar on focus changes (cursor position may change)
                _codeEdit.GotFocus += (s, e) => UpdateStatusBar();
                _codeEdit.LostFocus += (s, e) => UpdateStatusBar();

                // Update status bar on pointer events (mouse clicks move cursor)
                _codeEdit.PointerPressed += (s, e) => UpdateStatusBar();
                _codeEdit.PointerReleased += (s, e) => UpdateStatusBar();

                // Update status bar on key events (arrow keys, etc. move cursor)
                _codeEdit.KeyDown += (s, e) => UpdateStatusBar();
                _codeEdit.KeyUp += (s, e) => UpdateStatusBar();

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2657-2658
                // Original: self.ui.codeEdit.cursorPositionChanged.connect(self._update_breadcrumbs)
                // Update breadcrumbs on cursor position changes (selection changes include cursor moves)
                // Note: TextBox doesn't have SelectionChanged event, so we use PointerPressed/Released and KeyUp
                _codeEdit.PointerPressed += (s, e) => UpdateBreadcrumbs();
                _codeEdit.PointerReleased += (s, e) => UpdateBreadcrumbs();
                _codeEdit.KeyUp += (s, e) => UpdateBreadcrumbs();
                _codeEdit.KeyDown += (s, e) => UpdateBreadcrumbs();
                _codeEdit.KeyUp += (s, e) => UpdateBreadcrumbs();

                // Validate bookmarks when text changes
                _codeEdit.TextChanged += (s, e) => ValidateBookmarks();

                // Update bookmark visualization when text changes
                _codeEdit.TextChanged += (s, e) => UpdateBookmarkVisualization();

                // Connect text changed to code editor's internal handler (if available)
                // Note: CodeEditor.on_text_changed equivalent would be handled internally
            }

            // Connect game selector changed
            if (_gameSelector != null)
            {
                _gameSelector.SelectionChanged += (s, e) =>
                {
                    if (_gameSelector.SelectedIndex >= 0)
                    {
                        OnGameSelectorChanged(_gameSelector.SelectedIndex);
                    }
                };
            }

            // Connect context menu (if available)
            // Note: Avalonia handles context menus differently than Qt
            // Context menu setup would be done in SetupUI or via XAML

            // Setup keyboard shortcuts for panel toggles
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2544-2547
            // Original: self.ui.actionToggleFileExplorer.setShortcut(QKeySequence("Ctrl+B"))
            SetupPanelShortcuts();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2544-2547
        // Original: Panel toggles shortcuts
        /// <summary>
        /// Set up keyboard shortcuts for panel toggle actions.
        /// </summary>
        private void SetupPanelShortcuts()
        {
            // Add KeyDown event handler for global shortcuts
            // Matching PyKotor: self.ui.actionToggleFileExplorer.setShortcut(QKeySequence("Ctrl+B"))
            // Matching PyKotor: self.ui.actionToggleTerminal.setShortcut(QKeySequence("Ctrl+`"))
            this.KeyDown += (s, e) =>
            {
                // Ctrl+B: Toggle file explorer
                if (e.Key == Key.B && e.KeyModifiers == KeyModifiers.Control)
                {
                    ToggleFileExplorer();
                    e.Handled = true;
                }
                // Ctrl+`: Toggle terminal panel (bookmarks/snippets)
                // Note: Backtick key is Key.Oem3 on Windows, Key.OemTilde on some layouts
                else if ((e.Key == Key.Oem3 || e.Key == Key.OemTilde) && e.KeyModifiers == KeyModifiers.Control)
                {
                    ToggleTerminalPanel();
                    e.Handled = true;
                }
                // F12: Go to Definition
                else if (e.Key == Key.F12 && e.KeyModifiers == KeyModifiers.None)
                {
                    GoToDefinition();
                    e.Handled = true;
                }
                // Shift+F12: Find All References
                else if (e.Key == Key.F12 && e.KeyModifiers == KeyModifiers.Shift)
                {
                    FindAllReferencesAtCursor();
                    e.Handled = true;
                }
            };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3060-3117
        // Original: def _setup_find_replace_widget(self):
        private void SetupFindReplaceWidget()
        {
            // Create find/replace widget
            _findReplaceWidget = new FindReplaceWidget();

            // Connect signals/events
            _findReplaceWidget.FindRequested += OnFindRequested;
            _findReplaceWidget.FindNextRequested += OnFindNextRequested;
            _findReplaceWidget.FindPreviousRequested += OnFindPreviousRequested;
            _findReplaceWidget.ReplaceRequested += OnReplaceRequested;
            _findReplaceWidget.ReplaceAllRequested += OnReplaceAllRequested;
            _findReplaceWidget.CloseRequested += OnFindReplaceClose;

            // Initially hidden
            _findReplaceWidget.IsVisible = false;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3199-3238
        // Original: def _on_find_requested(self, text: str | None = "", case_sensitive: bool = False, whole_words: bool = False, regex: bool = False):
        /// <summary>
        /// Handles find request from the find/replace widget.
        /// Stores the find parameters and performs the initial search.
        /// </summary>
        /// <param name="text">The text to search for.</param>
        /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
        /// <param name="wholeWords">Whether to match whole words only.</param>
        /// <param name="regex">Whether to treat the search text as a regular expression.</param>
        private void OnFindRequested(string text, bool caseSensitive, bool wholeWords, bool regex)
        {
            if (_codeEdit == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            // Store current find parameters for subsequent find next/previous operations
            // Matching PyKotor implementation: self._current_find_text = find_text
            // Note: We'll use the FindNext/FindPrevious methods directly with parameters

            // Perform initial find
            bool found = _codeEdit.FindNext(text, caseSensitive, wholeWords, regex, backward: false);
            if (!found)
            {
                // Matching PyKotor implementation: Don't show message box, just log to output
                // Original: self._log_to_output("Find: No more occurrences found")
                LogToOutput("Find: No more occurrences found");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3228-3238
        // Original: Handle find next request
        /// <summary>
        /// Handles find next request from the find/replace widget.
        /// Finds the next occurrence using the current find parameters.
        /// </summary>
        private void OnFindNextRequested()
        {
            if (_codeEdit == null || _findReplaceWidget == null)
            {
                return;
            }

            // Get find parameters from widget
            string findText = _findReplaceWidget.GetFindText();
            bool caseSensitive = _findReplaceWidget.GetCaseSensitive();
            bool wholeWords = _findReplaceWidget.GetWholeWords();
            bool regex = _findReplaceWidget.GetRegex();

            if (string.IsNullOrEmpty(findText))
            {
                return;
            }

            bool found = _codeEdit.FindNext(findText, caseSensitive, wholeWords, regex, backward: false);
            if (!found)
            {
                // Matching PyKotor implementation: Don't show message box, just log to output
                System.Console.WriteLine("Find: No more occurrences found");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3240-3250
        // Original: Handle find previous request
        /// <summary>
        /// Handles find previous request from the find/replace widget.
        /// Finds the previous occurrence using the current find parameters.
        /// </summary>
        private void OnFindPreviousRequested()
        {
            if (_codeEdit == null || _findReplaceWidget == null)
            {
                return;
            }

            // Get find parameters from widget
            string findText = _findReplaceWidget.GetFindText();
            bool caseSensitive = _findReplaceWidget.GetCaseSensitive();
            bool wholeWords = _findReplaceWidget.GetWholeWords();
            bool regex = _findReplaceWidget.GetRegex();

            if (string.IsNullOrEmpty(findText))
            {
                return;
            }

            bool found = _codeEdit.FindPrevious(findText, caseSensitive, wholeWords, regex);
            if (!found)
            {
                // Matching PyKotor implementation: Don't show message box, just log to output
                System.Console.WriteLine("Find: No more occurrences found");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3252-3265
        // Original: def _on_replace_requested(self, find_text: str, replace_text: str, case_sensitive: bool = False, whole_words: bool = False, regex: bool = False):
        /// <summary>
        /// Handles replace request from the find/replace widget.
        /// Replaces the currently selected text if it matches the find text, then finds the next occurrence.
        /// </summary>
        /// <param name="findText">The text to search for.</param>
        /// <param name="replaceText">The text to replace matches with.</param>
        /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
        /// <param name="wholeWords">Whether to match whole words only.</param>
        /// <param name="regex">Whether to treat the search text as a regular expression.</param>
        private void OnReplaceRequested(string findText, string replaceText, bool caseSensitive, bool wholeWords, bool regex)
        {
            if (_codeEdit == null || string.IsNullOrEmpty(findText))
            {
                return;
            }

            // Check if current selection matches the find text
            string selectedText = _codeEdit.SelectedText;
            if (!string.IsNullOrEmpty(selectedText))
            {
                // Compare selected text with find text (respecting case sensitivity)
                bool matches = false;
                if (regex)
                {
                    // For regex, we need to check if the selection matches the pattern
                    try
                    {
                        RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        Regex pattern = new Regex(findText, options);
                        matches = pattern.IsMatch(selectedText);
                    }
                    catch (ArgumentException)
                    {
                        // Invalid regex - don't replace
                        return;
                    }
                }
                else
                {
                    // For literal text, compare directly
                    StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    matches = string.Equals(selectedText, findText, comparison);
                }

                if (matches)
                {
                    // Replace the selected text
                    int selectionStart = _codeEdit.SelectionStart;
                    int selectionLength = _codeEdit.SelectionEnd - _codeEdit.SelectionStart;
                    string text = _codeEdit.Text;
                    string newText = text.Substring(0, selectionStart) + (replaceText ?? "") + text.Substring(selectionStart + selectionLength);
                    _codeEdit.Text = newText;

                    // Set cursor position after replacement
                    _codeEdit.SelectionStart = selectionStart + (replaceText ?? "").Length;
                    _codeEdit.SelectionEnd = _codeEdit.SelectionStart;
                }
            }

            // Find next occurrence (matching PyKotor behavior)
            bool found = _codeEdit.FindNext(findText, caseSensitive, wholeWords, regex, backward: false);
            if (!found)
            {
                System.Console.WriteLine("Find: No more occurrences found");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3295-3315
        // Original: def _on_replace_all_requested(self, find_text: str, replace_text: str, case_sensitive: bool = False, whole_words: bool = False, regex: bool = False):
        /// <summary>
        /// Handles replace all request from the find/replace widget.
        /// Shows a confirmation dialog before replacing all occurrences, then performs the replacement
        /// and displays the count of replacements made.
        /// </summary>
        /// <param name="findText">The text to search for.</param>
        /// <param name="replaceText">The text to replace matches with.</param>
        /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
        /// <param name="wholeWords">Whether to match whole words only.</param>
        /// <param name="regex">Whether to treat the search text as a regular expression.</param>
        private void OnReplaceAllRequested(string findText, string replaceText, bool caseSensitive, bool wholeWords, bool regex)
        {
            if (_codeEdit == null || string.IsNullOrEmpty(findText))
            {
                return;
            }

            // Matching PyKotor implementation: Confirm before replacing all
            // Original: reply = QMessageBox.question(self, "Replace All", f"Replace all occurrences of '{find_text}'?", ...)
            var confirmBox = MessageBoxManager.GetMessageBoxStandard(
                "Replace All",
                $"Replace all occurrences of '{findText}'?",
                ButtonEnum.YesNo,
                MsBox.Avalonia.Enums.Icon.Question);

            // Show dialog and wait for result (synchronous for confirmation)
            var result = confirmBox.ShowAsync().GetAwaiter().GetResult();

            if (result == ButtonResult.Yes)
            {
                // Perform replace all operation
                int count = _codeEdit.ReplaceAllOccurrences(findText, replaceText ?? "", caseSensitive, wholeWords, regex);

                // Matching PyKotor implementation: Log to output and show information message
                // Original: self._log_to_output(f"Replace All: Replaced {count} occurrence(s)")
                // Original: QMessageBox.information(self, "Replace All", f"Replaced {count} occurrence(s)")
                string message = $"Replace All: Replaced {count} occurrence(s)";
                LogToOutput(message);

                var infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "Replace All",
                    $"Replaced {count} occurrence(s)",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                infoBox.ShowAsync();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        // Original: Handle find/replace close request
        private void OnFindReplaceClose()
        {
            if (_findReplaceWidget != null)
            {
                _findReplaceWidget.IsVisible = false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:670
        // Original: def _show_find(self):
        /// <summary>
        /// Shows the find widget.
        /// </summary>
        private void ShowFind()
        {
            if (_findReplaceWidget != null)
            {
                _findReplaceWidget.IsVisible = true;
                _findReplaceWidget.SetFindMode();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:671
        // Original: def _show_replace(self):
        /// <summary>
        /// Shows the replace widget.
        /// </summary>
        private void ShowReplace()
        {
            if (_findReplaceWidget != null)
            {
                _findReplaceWidget.IsVisible = true;
                _findReplaceWidget.SetReplaceMode();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:684
        // Original: def navigate.gotoLine: lambda: self.ui.codeEdit.go_to_line()
        /// <summary>
        /// Shows a dialog to go to a specific line number.
        /// </summary>
        private async void ShowGotoLine()
        {
            if (_codeEdit == null)
            {
                return;
            }

            int currentLine = GetCurrentLineNumber();
            int totalLines = GetTotalLineCount();

            var dialog = new GoToLineDialog(currentLine, totalLines);
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Set parent window if available
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                await dialog.ShowDialog(parentWindow);
            }
            else
            {
                await dialog.ShowDialog(null);
            }

            int? selectedLine = dialog.GetLineNumber();
            if (selectedLine.HasValue)
            {
                GotoLine(selectedLine.Value);
            }
        }

        /// <summary>
        /// Shows the documentation dialog with the main documentation file.
        /// Opens README.md from the wiki directory, or falls back to other common documentation files.
        /// Matching PyKotor help/documentation behavior.
        /// </summary>
        private void ShowDocumentation()
        {
            // Try common documentation file names in order of preference
            string[] documentationFiles = { "README.md", "index.md", "documentation.md", "help.md" };

            // Get wiki path to check if files exist
            string wikiPath = Dialogs.EditorHelpDialog.GetWikiPath();

            // Find the first existing documentation file
            string wikiFile = null;
            foreach (string filename in documentationFiles)
            {
                string filePath = System.IO.Path.Combine(wikiPath, filename);
                if (System.IO.File.Exists(filePath))
                {
                    wikiFile = filename;
                    break;
                }
            }

            // If no documentation file found, use README.md as default (EditorHelpDialog will show error if missing)
            if (wikiFile == null)
            {
                wikiFile = "README.md";
            }

            // Show help dialog with the documentation file
            // NSSEditor inherits from Editor, which has ShowHelpDialog method
            ShowHelpDialog(wikiFile);
        }

        /// <summary>
        /// Shows the keyboard shortcuts dialog.
        /// Displays all available keyboard shortcuts organized by category.
        /// </summary>
        private void ShowKeyboardShortcuts()
        {
            var dialog = new KeyboardShortcutsDialog();
            dialog.ShowDialog(this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1256-1349
        // Original: def go_to_definition(self):
        /// <summary>
        /// Navigates to the definition of the symbol at the cursor.
        /// First searches the outline view for local definitions, then checks built-in functions/constants,
        /// and finally searches the current file for the symbol definition.
        /// </summary>
        private void GoToDefinition()
        {
            if (_codeEdit == null)
            {
                return;
            }

            // Get word under cursor
            string word = GetWordUnderCursor();
            if (string.IsNullOrEmpty(word) || string.IsNullOrWhiteSpace(word))
            {
                var messageBox = MessageBoxManager.GetMessageBoxStandard(
                    "Go to Definition",
                    "No symbol selected.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                messageBox.ShowAsync();
                return;
            }

            word = word.Trim();

            // First try to find in outline (functions, structs, globals)
            bool found = false;
            if (_outlineView != null && _outlineView.ItemsSource != null)
            {
                var itemsList = _outlineView.ItemsSource as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>();
                foreach (var treeItem in itemsList)
                {
                    if (treeItem == null)
                    {
                        continue;
                    }

                    // Check if this item matches
                    if (treeItem.Tag is OutlineSymbol symbol)
                    {
                        string identifier = symbol.Name;
                        if (!string.IsNullOrEmpty(identifier) &&
                            string.Equals(identifier, word, StringComparison.OrdinalIgnoreCase))
                        {
                            // Navigate to the symbol's line number
                            // OutlineSymbol.LineNumber is 0-based, GotoLine expects 1-based
                            int lineNumber = symbol.LineNumber + 1;
                            GotoLine(lineNumber);
                            found = true;
                            break;
                        }
                    }

                    // Also check children (parameters, members)
                    if (treeItem.ItemsSource != null)
                    {
                        var childrenList = treeItem.ItemsSource as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>();
                        foreach (var child in childrenList)
                        {
                            if (child?.Tag is OutlineSymbol childSymbol)
                            {
                                string childIdentifier = childSymbol.Name;
                                if (!string.IsNullOrEmpty(childIdentifier) &&
                                    string.Equals(childIdentifier, word, StringComparison.OrdinalIgnoreCase))
                                {
                                    // Go to parent definition
                                    if (treeItem.Tag is OutlineSymbol parentSymbol)
                                    {
                                        int lineNumber = parentSymbol.LineNumber + 1;
                                        GotoLine(lineNumber);
                                        found = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (found)
                        {
                            break;
                        }
                    }
                }
            }

            // If not found in outline, check if it's a function or constant
            if (!found)
            {
                // Check functions
                if (_functions != null)
                {
                    foreach (var func in _functions)
                    {
                        if (func != null && !string.IsNullOrEmpty(func.Name) &&
                            string.Equals(func.Name, word, StringComparison.OrdinalIgnoreCase))
                        {
                            // Show in constants/learn tab
                            if (_panelTabs != null)
                            {
                                // Find the learn tab (typically the tab containing function/constant lists)
                                // In Avalonia, we need to find the tab by iterating
                                for (int i = 0; i < _panelTabs.Items.Count; i++)
                                {
                                    var tabItem = _panelTabs.Items[i] as TabItem;
                                    if (tabItem != null && tabItem.Content != null)
                                    {
                                        // Check if this tab contains the function list
                                        if (ContainsControl(tabItem.Content, _functionList))
                                        {
                                            _panelTabs.SelectedIndex = i;
                                            break;
                                        }
                                    }
                                }
                            }

                            // Try to find and select in function list
                            if (_functionList != null)
                            {
                                for (int i = 0; i < _functionList.Items.Count; i++)
                                {
                                    var listItem = _functionList.Items[i];
                                    string itemText = GetListItemText(listItem);
                                    if (!string.IsNullOrEmpty(itemText) &&
                                        string.Equals(itemText, word, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _functionList.SelectedIndex = i;
                                        // Scroll to item
                                        _functionList.ScrollIntoView(i);

                                        var messageBox = MessageBoxManager.GetMessageBoxStandard(
                                            "Go to Definition",
                                            $"Function '{word}' is a built-in function.\n\n" +
                                            $"Return type: {GetFunctionReturnType(func)}\n" +
                                            $"See the Constants tab for more information.",
                                            ButtonEnum.Ok,
                                            MsBox.Avalonia.Enums.Icon.Info);
                                        messageBox.ShowAsync();
                                        return;
                                    }
                                }
                            }

                            // If function list not found, still show message
                            var funcMessageBox = MessageBoxManager.GetMessageBoxStandard(
                                "Go to Definition",
                                $"Function '{word}' is a built-in function.\n\n" +
                                $"Return type: {GetFunctionReturnType(func)}\n" +
                                $"See the Constants tab for more information.",
                                ButtonEnum.Ok,
                                MsBox.Avalonia.Enums.Icon.Info);
                            funcMessageBox.ShowAsync();
                            return;
                        }
                    }
                }

                // Check constants
                if (_constants != null)
                {
                    foreach (var constant in _constants)
                    {
                        if (constant != null && !string.IsNullOrEmpty(constant.Name) &&
                            string.Equals(constant.Name, word, StringComparison.OrdinalIgnoreCase))
                        {
                            // Show in constants/learn tab
                            if (_panelTabs != null)
                            {
                                // Find the learn tab
                                for (int i = 0; i < _panelTabs.Items.Count; i++)
                                {
                                    var tabItem = _panelTabs.Items[i] as TabItem;
                                    if (tabItem != null && tabItem.Content != null)
                                    {
                                        // Check if this tab contains the constant list
                                        if (ContainsControl(tabItem.Content, _constantList))
                                        {
                                            _panelTabs.SelectedIndex = i;
                                            break;
                                        }
                                    }
                                }
                            }

                            // Try to find and select in constant list
                            if (_constantList != null)
                            {
                                for (int i = 0; i < _constantList.Items.Count; i++)
                                {
                                    var listItem = _constantList.Items[i];
                                    string itemText = GetListItemText(listItem);
                                    if (!string.IsNullOrEmpty(itemText) &&
                                        string.Equals(itemText, word, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _constantList.SelectedIndex = i;
                                        // Scroll to item
                                        _constantList.ScrollIntoView(i);

                                        var messageBox = MessageBoxManager.GetMessageBoxStandard(
                                            "Go to Definition",
                                            $"Constant '{word}' is a built-in constant.\n" +
                                            $"See the Constants tab for more information.",
                                            ButtonEnum.Ok,
                                            MsBox.Avalonia.Enums.Icon.Info);
                                        messageBox.ShowAsync();
                                        return;
                                    }
                                }
                            }

                            // If constant list not found, still show message
                            var constMessageBox = MessageBoxManager.GetMessageBoxStandard(
                                "Go to Definition",
                                $"Constant '{word}' is a built-in constant.\n" +
                                $"See the Constants tab for more information.",
                                ButtonEnum.Ok,
                                MsBox.Avalonia.Enums.Icon.Info);
                            constMessageBox.ShowAsync();
                            return;
                        }
                    }
                }

                // If still not found, search the current file
                bool foundInFile = NavigateToSymbol(word);

                if (!foundInFile)
                {
                    var notFoundMessageBox = MessageBoxManager.GetMessageBoxStandard(
                        "Go to Definition",
                        $"Definition for '{word}' not found in current file.",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Info);
                    notFoundMessageBox.ShowAsync();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/file.py:30-41
        // Original: def open(self): Opens a file dialog to select a file to open
        /// <summary>
        /// Opens a file dialog to select a file to open in the editor.
        /// Handles both regular files and capsule files (MOD/RIM/ERF/SAV).
        /// </summary>
        private async void OpenFile()
        {
            // Get the top-level window for the file picker
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            // Build file filters based on supported types
            // Matching PyKotor: builds filter from _readSupported types
            var fileFilters = new List<FilePickerFileType>();

            // Add filter for all supported file types
            var allExtensions = new List<string>();
            foreach (var resType in _readSupported)
            {
                if (resType != null && !string.IsNullOrEmpty(resType.Extension))
                {
                    allExtensions.Add($"*.{resType.Extension}");
                }
            }
            // Add capsule file extensions
            allExtensions.Add("*.mod");
            allExtensions.Add("*.erf");
            allExtensions.Add("*.rim");
            allExtensions.Add("*.sav");

            if (allExtensions.Count > 0)
            {
                fileFilters.Add(new FilePickerFileType("All valid files")
                {
                    Patterns = allExtensions
                });
            }

            // Add individual filters for each supported type
            foreach (var resType in _readSupported)
            {
                if (resType != null && !string.IsNullOrEmpty(resType.Extension) && !string.IsNullOrEmpty(resType.Category))
                {
                    fileFilters.Add(new FilePickerFileType($"{resType.Category} File (*.{resType.Extension})")
                    {
                        Patterns = new[] { $"*.{resType.Extension}" }
                    });
                }
            }

            // Add capsule file filter
            fileFilters.Add(new FilePickerFileType($"Load from module ({CapsuleFilter})")
            {
                Patterns = new[] { "*.mod", "*.erf", "*.rim", "*.sav" }
            });

            // Show file picker dialog
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open file",
                AllowMultiple = false,
                FileTypeFilter = fileFilters
            });

            if (files == null || files.Count == 0)
            {
                // User cancelled
                return;
            }

            var selectedFile = files[0];
            string filePath = selectedFile.Path.LocalPath;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    "Selected file does not exist.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            // Check if it's a capsule file (MOD/RIM/ERF/SAV)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/file.py:36-38
            // Original: if is_capsule_file(r_filepath) and f"Load from module ({self.editor.CAPSULE_FILTER})" in self.editor._open_filter:
            string fileExt = Path.GetExtension(filePath).ToLowerInvariant();
            if (fileExt == ".mod" || fileExt == ".erf" || fileExt == ".rim" || fileExt == ".sav")
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/file.py:43-55
                // Original: def _load_module_from_dialog_info(self, r_filepath: Path):
                // Original: dialog = LoadFromModuleDialog(Capsule(r_filepath), self.editor._read_supported)
                // Original: if dialog.exec():
                // Original:     resname: str | None = dialog.resname()
                // Original:     restype: ResourceType | None = dialog.restype()
                // Original:     data: bytes | None = dialog.data()
                // Original:     assert resname is not None
                // Original:     assert restype is not None
                // Original:     assert data is not None
                // Original:     self.load(r_filepath, resname, restype, data)
                try
                {
                    // Create capsule from file path
                    var capsule = new Capsule(filePath);

                    // Create dialog with capsule and supported resource types
                    var dialog = new LoadFromModuleDialog(capsule, _readSupported);

                    // Show dialog asynchronously and wait for result (matching PyKotor's exec() behavior)
                    // In Avalonia, ShowDialog returns Task<TResult> where TResult is the Close() parameter
                    bool? dialogResult = await dialog.ShowDialog<bool?>(this);

                    if (dialogResult == true)
                    {
                        string resname = dialog.ResName();
                        ResourceType selectedRestype = dialog.ResType();
                        byte[] selectedData = dialog.Data();

                        // Matching PyKotor: assert resname is not None, assert restype is not None, assert data is not None
                        if (resname != null && selectedRestype != null && selectedData != null)
                        {
                            // Load the selected resource
                            // Matching PyKotor: self.load(r_filepath, resname, restype, data)
                            Load(filePath, resname, selectedRestype, selectedData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var errorBox = MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        $"Failed to open capsule file:\n{ex.Message}",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    await errorBox.ShowAsync();
                }
                return;
            }

            // Read file data
            byte[] data;
            try
            {
                data = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Failed to read file:\n{ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            // Determine resource type from file extension
            // Matching PyKotor implementation: ResourceIdentifier.from_path(r_filepath).validate()
            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    "File has no extension. Cannot determine resource type.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            // Remove leading dot from extension
            extension = extension.TrimStart('.').ToLowerInvariant();
            ResourceType restype = ResourceType.FromExtension(extension);

            if (restype == null || restype.IsInvalid)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Unknown or unsupported file extension: .{extension}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            // Check if this resource type is supported by this editor
            bool isSupported = false;
            foreach (var supportedType in _readSupported)
            {
                if (supportedType != null && supportedType.Equals(restype))
                {
                    isSupported = true;
                    break;
                }
            }

            if (!isSupported)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Resource type '{restype.Extension}' is not supported by this editor.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            // Extract resname from file path (filename without extension)
            // Matching PyKotor: ResourceIdentifier.from_path gets resname
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // Load the file into the current editor
            // Matching PyKotor implementation: self.load(r_filepath, res_ident.resname, res_ident.restype, data)
            Load(filePath, fileName, restype, data);
        }

        /// <summary>
        /// Helper method to check if a control or its children contain a specific control.
        /// </summary>
        private bool ContainsControl(object parent, Control target)
        {
            if (parent == target)
            {
                return true;
            }

            if (parent is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (ContainsControl(child, target))
                    {
                        return true;
                    }
                }
            }
            else if (parent is ContentControl contentControl && contentControl.Content != null)
            {
                return ContainsControl(contentControl.Content, target);
            }

            return false;
        }

        /// <summary>
        /// Helper method to get text from a list item (handles both string and ListBoxItem).
        /// </summary>
        private string GetListItemText(object listItem)
        {
            if (listItem == null)
            {
                return "";
            }

            if (listItem is string str)
            {
                return str;
            }

            if (listItem is ListBoxItem listBoxItem)
            {
                if (listBoxItem.Content is string contentStr)
                {
                    return contentStr;
                }
                return listBoxItem.Content?.ToString() ?? "";
            }

            return listItem.ToString() ?? "";
        }

        /// <summary>
        /// Helper method to get the return type of a ScriptFunction.
        /// </summary>
        private string GetFunctionReturnType(ScriptFunction func)
        {
            if (func == null)
            {
                return "void";
            }

            // ScriptFunction has a ReturnType property of type DataType
            // DataType has a ToScriptString() method to convert to string
            if (func.ReturnType != null)
            {
                return func.ReturnType.ToScriptString();
            }

            return "void";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3816-3822
        // Original: def _find_all_references_at_cursor(self):
        /// <summary>
        /// Finds all references to the symbol at the cursor.
        /// </summary>
        private void FindAllReferencesAtCursor()
        {
            if (_codeEdit == null)
            {
                return;
            }

            // Get word under cursor
            string word = GetWordUnderCursor();
            if (string.IsNullOrEmpty(word) || string.IsNullOrWhiteSpace(word))
            {
                return;
            }

            // Find all references
            FindAllReferences(word);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3806-3814
        // Original: def _toggle_bookmark_at_cursor(self):
        /// <summary>
        /// Toggles a bookmark at the current cursor position.
        /// </summary>
        private void ToggleBookmarkAtCursor()
        {
            int currentLine = GetCurrentLineNumber();

            if (HasBookmarkAtLine(currentLine))
            {
                RemoveBookmarkAtLine(currentLine);
            }
            else
            {
                AddBookmark();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2121-2162
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes | bytearray):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            _isDecompiled = false;

            // Reload bookmarks when file is loaded
            LoadBookmarks();

            if (data == null || data.Length == 0)
            {
                if (_codeEdit != null)
                {
                    _codeEdit.SetPlainText("");
                }
                return;
            }

            if (restype == ResourceType.NSS)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2136-2145
                // Original: Try multiple encodings to properly decode the text
                string text = null;
                try
                {
                    text = System.Text.Encoding.UTF8.GetString(data);
                }
                catch (DecoderFallbackException)
                {
                    try
                    {
                        text = System.Text.Encoding.GetEncoding("windows-1252").GetString(data);
                    }
                    catch (DecoderFallbackException)
                    {
                        text = System.Text.Encoding.GetEncoding("latin-1").GetString(data);
                    }
                }

                if (_codeEdit != null)
                {
                    _codeEdit.SetPlainText(text);
                }
            }
            else if (restype == ResourceType.NCS)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2146-2156
                // Original: elif restype is ResourceType.NCS:
                bool errorOccurred = false;
                try
                {
                // Matching PyKotor implementation: self._handle_user_ncs(data, resref)
                HandleUserNcs(data, resref);
                }
                catch (InvalidOperationException ex)
                {
                    // Matching PyKotor implementation: self._handle_exc_debug_mode("Decompilation/Download Failed", e)
                    errorOccurred = HandleExceptionDebugMode("Decompilation/Download Failed", ex);
                }
                catch (Exception ex)
                {
                    // Catch any other exceptions and handle them
                    errorOccurred = HandleExceptionDebugMode("Decompilation/Download Failed", ex);
                }

                if (errorOccurred)
                {
                    New();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2196-2246
        // Original: def _decompile_ncs_dencs(self, ncs_data: bytes) -> str:
        /// <summary>
        /// Decompiles NCS bytecode using the DeNCS decompiler.
        /// Matches PyKotor implementation which loads nwscript.nss from override folder for ActionsData,
        /// creates ActionsData from it (or uses empty ActionsData if not found), and decompiles using decompile_ncs.
        /// </summary>
        /// <param name="ncsData">The bytes of the compiled NCS script.</param>
        /// <returns>The decompiled NSS source code string, or empty string if decompilation fails.</returns>
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2248-2291
        // Original: def _decompile_ncs_dencs(self, ncs_data: bytes) -> str:
        /// <summary>
        /// Decompiles NCS bytecode using the built-in decompiler.
        /// Returns empty string on failure instead of raising exceptions to allow caller to handle gracefully.
        /// This differs from PyKotor which raises ValueError, but provides better error handling for UI scenarios.
        /// </summary>
        /// <param name="ncsData">The bytes of the compiled NCS script</param>
        /// <returns>The decompiled NSS source code string, or empty string if decompilation fails</returns>
        private string DecompileNcsDencs(byte[] ncsData)
        {
            // Validate input - return empty string for invalid input
            if (ncsData == null || ncsData.Length == 0)
            {
                System.Console.WriteLine("Decompilation failed: NCS data is null or empty");
                return "";
            }

            // ScriptDecompiler.HtDecompileScript already implements the full PyKotor logic:
            // 1. Tries to load nwscript.nss from override folder (installationPath/override/nwscript.nss)
            // 2. Creates FileDecompiler with nwscript.nss if found, or without if not found
            // 3. Decompiles using DecompileNcsObject with proper ActionsData handling
            // 4. Returns decompiled source or throws on failure
            if (_installation != null)
            {
                try
                {
                    string decompiled = ScriptDecompiler.HtDecompileScript(ncsData, _installation.Path, _installation.Tsl);
                    if (!string.IsNullOrEmpty(decompiled))
                    {
                        return decompiled;
                    }
                    // If decompilation returned null or empty, return empty string
                    // Note: PyKotor implementation raises ValueError, but we return empty string to allow
                    // caller to handle gracefully without exception handling
                    System.Console.WriteLine("Decompilation failed: decompile_ncs returned null or empty string");
                    return "";
                }
                catch (Exception ex)
                {
                    // Decompilation failed - log error and return empty string
                    // Note: PyKotor implementation raises ValueError, but we return empty string to allow
                    // caller to handle gracefully without exception handling
                    System.Console.WriteLine($"Decompilation failed: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        System.Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    return "";
                }
            }

            // Installation not set - return empty string
            System.Console.WriteLine("Decompilation failed: installation is not set");
            return "";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2269-2291
        // Original: def build(self) -> tuple[bytes | bytearray, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            if (_codeEdit == null)
            {
                return Tuple.Create(new byte[0], new byte[0]);
            }

            string text = _codeEdit.ToPlainText();

            if (_restype == ResourceType.NCS)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2281-2291
                // Original: Compile script if restype is NCS
                if (_installation != null)
                {
                    byte[] compiled = ScriptCompiler.HtCompileScript(text, _installation.Path, _installation.Tsl);
                    if (compiled != null && compiled.Length > 0)
                    {
                        return Tuple.Create(compiled, new byte[0]);
                    }
                    // User cancelled compilation
                    return Tuple.Create(new byte[0], new byte[0]);
                }
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2270-2279
            // Original: Encode with proper error handling
            byte[] data;
            try
            {
                data = System.Text.Encoding.UTF8.GetBytes(text);
            }
            catch (EncoderFallbackException)
            {
                try
                {
                    data = System.Text.Encoding.GetEncoding("windows-1252").GetBytes(text);
                }
                catch (EncoderFallbackException)
                {
                    data = System.Text.Encoding.GetEncoding("latin-1").GetBytes(text);
                }
            }

            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2293-2296
        // Original: def new(self):
        public override void New()
        {
            base.New();
            if (_codeEdit != null)
            {
                _codeEdit.SetPlainText("\n\nvoid main()\n{\n    \n}\n");
            }
        }

        public override void SaveAs()
        {
            Save();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:227-232
        // Original: def _setup_bookmarks(self):
        private void SetupBookmarks()
        {
            _bookmarkTree = new TreeView();

            // Create bookmarks dock panel container
            // Matching PyKotor: self.ui.bookmarksDock
            if (_bookmarksDock == null)
            {
                var bookmarksPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Vertical,
                    Name = "bookmarksDock"
                };

                // Add label for bookmarks
                var bookmarksLabel = new TextBlock
                {
                    Text = "Bookmarks",
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    Margin = new Avalonia.Thickness(5)
                };
                bookmarksPanel.Children.Add(bookmarksLabel);

                // Add bookmark tree view with scroll viewer
                var scrollViewer = new ScrollViewer
                {
                    Content = _bookmarkTree,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                };
                bookmarksPanel.Children.Add(scrollViewer);

                // Set minimum width for the panel
                bookmarksPanel.MinWidth = 200;

                _bookmarksDock = bookmarksPanel;
            }

            // Load bookmarks when editor is initialized
            LoadBookmarks();

            // Integrate bookmarks dock into main UI layout
            IntegrateBookmarksDock();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:327-347
        // Original: def load_snippets(self):
        /// <summary>
        /// Sets up the snippet list widget and loads snippets from settings.
        /// </summary>
        private void SetupSnippets()
        {
            _snippetList = new ListBox();

            // Create snippets dock panel container
            // Matching PyKotor: self.ui.snippetsDock
            if (_snippetsDock == null)
            {
                var snippetsPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Vertical,
                    Name = "snippetsDock"
                };

                // Add label for snippets
                var snippetsLabel = new TextBlock
                {
                    Text = "Snippets",
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    Margin = new Avalonia.Thickness(5)
                };
                snippetsPanel.Children.Add(snippetsLabel);

                // Add snippet list box with scroll viewer
                var scrollViewer = new ScrollViewer
                {
                    Content = _snippetList,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                };
                snippetsPanel.Children.Add(scrollViewer);

                // Set minimum width for the panel
                snippetsPanel.MinWidth = 200;

                _snippetsDock = snippetsPanel;
            }

            // Load snippets when editor is initialized
            LoadSnippets();

            // Integrate snippets dock into main UI layout
            IntegrateSnippetsDock();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:327-347
        // Original: def load_snippets(self):
        /// <summary>
        /// Loads snippets from QSettings into the list widget.
        /// </summary>
        public void LoadSnippets()
        {
            if (_snippetList == null)
            {
                return;
            }

            // Matching Python: settings = QSettings("HolocronToolsetV3", "NSSEditor")
            var settings = new Settings("NSSEditor");

            // Matching Python: snippets_json = settings.value("nss_editor/snippets", "[]")
            string snippetsJson = settings.GetValue("nss_editor/snippets", "[]");

            // Matching Python: snippets = json.loads(snippets_json) if isinstance(snippets_json, str) else []
            List<Dictionary<string, object>> snippets = new List<Dictionary<string, object>>();

            try
            {
                if (!string.IsNullOrEmpty(snippetsJson) && snippetsJson != "[]")
                {
                    // Parse JSON array of snippet dictionaries
                    using (JsonDocument doc = JsonDocument.Parse(snippetsJson))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement element in doc.RootElement.EnumerateArray())
                            {
                                if (element.ValueKind == JsonValueKind.Object)
                                {
                                    var snippet = new Dictionary<string, object>();
                                    foreach (JsonProperty prop in element.EnumerateObject())
                                    {
                                        if (prop.Value.ValueKind == JsonValueKind.String)
                                        {
                                            snippet[prop.Name] = prop.Value.GetString();
                                        }
                                    }
                                    // Matching Python: if isinstance(snippet, dict) and "name" in snippet and "content" in snippet:
                                    if (snippet.ContainsKey("name") && snippet.ContainsKey("content"))
                                    {
                                        snippets.Add(snippet);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // If parsing fails, use empty list
                snippets = new List<Dictionary<string, object>>();
            }

            // Matching Python: self.ui.snippetList.clear()
            _snippetList.Items.Clear();

            // Matching Python: for snippet in snippets:
            foreach (var snippet in snippets)
            {
                // Matching Python: item = QListWidgetItem(snippet["name"])
                // Matching Python: item.setData(Qt.ItemDataRole.UserRole, snippet["content"])
                string name = snippet.ContainsKey("name") ? (snippet["name"] as string ?? "") : "";
                string content = snippet.ContainsKey("content") ? (snippet["content"] as string ?? "") : "";

                if (!string.IsNullOrEmpty(name))
                {
                    // In Avalonia, ListBoxItem is created automatically, but we can use Tag for content
                    var item = new ListBoxItem
                    {
                        Content = name,
                        Tag = content
                    };
                    _snippetList.Items.Add(item);
                }
            }

            // Matching Python: self._filter_snippets() - would be called if filtering is implemented
            // TODO: STUB - For now, we just load the snippets
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:349-360
        // Original: def _save_snippets(self):
        /// <summary>
        /// Saves snippets to QSettings.
        /// </summary>
        public void SaveSnippets()
        {
            if (_snippetList == null)
            {
                return;
            }

            // Matching Python: snippets = []
            var snippets = new List<Dictionary<string, object>>();

            // Matching Python: for i in range(self.ui.snippetList.count()):
            foreach (var itemObj in _snippetList.Items)
            {
                if (itemObj is ListBoxItem item && item != null)
                {
                    // Matching Python: name = item.text() or ""
                    string name = item.Content?.ToString() ?? "";

                    // Matching Python: content = item.data(Qt.ItemDataRole.UserRole) or ""
                    string content = item.Tag as string ?? "";

                    // Matching Python: snippets.append({"name": name, "content": content})
                    snippets.Add(new Dictionary<string, object>
                    {
                        { "name", name },
                        { "content", content }
                    });
                }
            }

            // Matching Python: settings = QSettings("HolocronToolsetV3", "NSSEditor")
            try
            {
                var settings = new Settings("NSSEditor");
                // Matching Python: settings.setValue("nss_editor/snippets", json.dumps(snippets))
                string json = JsonSerializer.Serialize(snippets);
                settings.SetValue("nss_editor/snippets", json);
                // Matching Python: settings.sync() - Save() is called automatically in SetValue
            }
            catch
            {
                // Ignore save errors
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:234-253
        // Original: def add_bookmark(self):
        public void AddBookmark()
        {
            if (_codeEdit == null || _bookmarkTree == null)
            {
                return;
            }

            // Get current line number (1-indexed)
            // For TextBox, we need to calculate line number from cursor position
            int lineNumber = GetCurrentLineNumber();
            string defaultDescription = $"Bookmark at line {lineNumber}";

            var item = new TreeViewItem
            {
                Header = $"{lineNumber} - {defaultDescription}",
                Tag = new BookmarkData { LineNumber = lineNumber, Description = defaultDescription }
            };

            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ?? new List<TreeViewItem>();
            itemsList.Add(item);
            _bookmarkTree.ItemsSource = itemsList;

            SaveBookmarks();
            UpdateBookmarkVisualization();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:255-265
        // Original: def delete_bookmark(self):
        public void DeleteBookmark()
        {
            if (_bookmarkTree == null)
            {
                return;
            }

            var selectedItems = _bookmarkTree.SelectedItems?.Cast<TreeViewItem>().ToList() ?? new List<TreeViewItem>();
            if (selectedItems.Count == 0 && _bookmarkTree.SelectedItem is TreeViewItem selectedItem)
            {
                selectedItems.Add(selectedItem);
            }

            if (selectedItems.Count == 0)
            {
                return;
            }

            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ?? new List<TreeViewItem>();
            foreach (var item in selectedItems)
            {
                if (item != null)
                {
                    itemsList.Remove(item);
                }
            }
            _bookmarkTree.ItemsSource = itemsList;

            SaveBookmarks();
            UpdateBookmarkVisualization();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:267-269
        // Original: def _goto_bookmark(self, item: QTreeWidgetItem):
        /// <summary>
        /// Navigates to the line number associated with a bookmark item.
        /// </summary>
        /// <param name="item">The TreeViewItem representing the bookmark.</param>
        public void GotoBookmark(TreeViewItem item)
        {
            if (item == null || !(item.Tag is BookmarkData bookmarkData))
            {
                return;
            }

            int lineNumber = bookmarkData.LineNumber;
            GotoLine(lineNumber);
        }

        // Helper method to get current line number from TextBox cursor position
        private int GetCurrentLineNumber()
        {
            if (_codeEdit == null || string.IsNullOrEmpty(_codeEdit.Text))
            {
                return 1;
            }

            int cursorPosition = _codeEdit.SelectionStart;
            string text = _codeEdit.Text;

            // Count newlines before cursor position (1-indexed)
            int lineNumber = 1;
            for (int i = 0; i < cursorPosition && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lineNumber++;
                }
            }

            return lineNumber;
        }

        // Public method to get current line number for testing
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        // Original: cursor.blockNumber() + 1 in test_nss_editor_goto_line
        /// <summary>
        /// Gets the current line number (1-indexed) where the cursor is positioned.
        /// Exposed for testing purposes to match Python test behavior.
        /// </summary>
        /// <returns>The current line number (1-indexed), or 1 if no text or cursor position is invalid.</returns>
        public int GetCurrentLine()
        {
            return GetCurrentLineNumber();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3818-3820
        // Original: cursor.select(QTextCursor.SelectionType.WordUnderCursor); word = cursor.selectedText().strip()
        /// <summary>
        /// Gets the word under the current cursor position.
        /// </summary>
        /// <returns>The word under cursor, or empty string if no word found.</returns>
        private string GetWordUnderCursor()
        {
            if (_codeEdit == null || string.IsNullOrEmpty(_codeEdit.Text) || _codeEdit.SelectionStart < 0)
            {
                return "";
            }

            int cursorPos = _codeEdit.SelectionStart;
            string text = _codeEdit.Text;

            // Find word boundaries (letters, digits, underscore)
            int start = cursorPos;
            int end = cursorPos;

            // Move start backward to beginning of word
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
            {
                start--;
            }

            // Move end forward to end of word
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
            {
                end++;
            }

            if (end > start)
            {
                return text.Substring(start, end - start).Trim();
            }

            return "";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1201-1240
        // Original: def _find_all_references(self, word: str):
        /// <summary>
        /// Finds all references to a symbol in the current file.
        /// </summary>
        /// <param name="word">The word/symbol to search for.</param>
        private void FindAllReferences(string word)
        {
            if (string.IsNullOrEmpty(word) || string.IsNullOrWhiteSpace(word))
            {
                return;
            }

            if (_codeEdit == null)
            {
                return;
            }

            // Search in current file
            string text = _codeEdit.Text;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
            var results = new List<FindResult>();

            // Simple word boundary matching (matching PyKotor implementation)
            string escapedWord = Regex.Escape(word);
            string pattern = @"\b" + escapedWord + @"\b";
            Regex searchRegex = new Regex(pattern, RegexOptions.IgnoreCase);

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                string line = lines[lineNum];
                MatchCollection matches = searchRegex.Matches(line);
                foreach (Match match in matches)
                {
                    results.Add(new FindResult
                    {
                        Line = lineNum + 1, // 1-indexed
                        Column = match.Index + 1, // 1-indexed
                        Content = line.Trim().Length > 100 ? line.Trim().Substring(0, 100) : line.Trim()
                    });
                }
            }

            // Show results
            if (results.Count == 0)
            {
                var messageBox = MessageBoxManager.GetMessageBoxStandard(
                    "Find All References",
                    $"No references to '{word}' found in current file.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                messageBox.ShowAsync();
            }
            else
            {
                // Navigate to first result
                if (results.Count > 0)
                {
                    var firstResult = results[0];
                    GotoLine(firstResult.Line);

                    // Try to position cursor at the match column
                    if (_codeEdit != null)
                    {
                        // Calculate position by finding the line start and adding column offset
                        int lineStart = 0;
                        int currentLine = 1;
                        string codeText = _codeEdit.Text;

                        // Find the start of the target line
                        for (int i = 0; i < codeText.Length && currentLine < firstResult.Line; i++)
                        {
                            if (codeText[i] == '\n')
                            {
                                currentLine++;
                                lineStart = i + 1;
                            }
                            else if (codeText[i] == '\r')
                            {
                                // Handle \r\n or \r
                                if (i + 1 < codeText.Length && codeText[i + 1] == '\n')
                                {
                                    currentLine++;
                                    lineStart = i + 2;
                                    i++; // Skip the \n
                                }
                                else
                                {
                                    currentLine++;
                                    lineStart = i + 1;
                                }
                            }
                        }

                        // Calculate position including column (1-indexed to 0-indexed)
                        int targetPosition = lineStart + (firstResult.Column - 1);
                        if (targetPosition >= 0 && targetPosition < text.Length)
                        {
                            // Select the word at that position
                            _codeEdit.SelectionStart = targetPosition;
                            _codeEdit.SelectionEnd = targetPosition + word.Length;
                            _codeEdit.CaretIndex = targetPosition;
                        }
                    }
                }

                // Show message with result count
                var messageBox = MessageBoxManager.GetMessageBoxStandard(
                    "Find All References",
                    $"Found {results.Count} reference(s) to '{word}'. Navigated to first occurrence.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                messageBox.ShowAsync();
            }
        }

        // Helper class to store find results
        private class FindResult
        {
            public int Line { get; set; }
            public int Column { get; set; }
            public string Content { get; set; }
        }

        // Helper method to set cursor to a specific line number (for testing)
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:271-280
        // Original: def _goto_line(self, line_number: int):
        public void GotoLine(int lineNumber)
        {
            if (_codeEdit == null || string.IsNullOrEmpty(_codeEdit.Text))
            {
                return;
            }

            string text = _codeEdit.Text;
            int position = 0;
            int currentLine = 1;

            // Find the position of the specified line (1-indexed)
            for (int i = 0; i < text.Length && currentLine < lineNumber; i++)
            {
                if (text[i] == '\n')
                {
                    currentLine++;
                    position = i + 1;
                }
            }

            // Set cursor position
            _codeEdit.SelectionStart = position;
            _codeEdit.SelectionEnd = position;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1153-1174
        // Original: def _goto_next_bookmark(self):
        /// <summary>
        /// Navigates to the next bookmark after the current line.
        /// If no bookmark exists after the current line, wraps around to the first bookmark.
        /// </summary>
        public void GotoNextBookmark()
        {
            if (_codeEdit == null || _bookmarkTree == null)
            {
                return;
            }

            int currentLine = GetCurrentLineNumber();

            // Collect all bookmarks after current line
            var bookmarks = new List<int>();
            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ??
                          (_bookmarkTree.Items as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>()).ToList();

            foreach (var item in itemsList)
            {
                if (item?.Tag is BookmarkData bookmarkData)
                {
                    int line = bookmarkData.LineNumber;
                    if (line > currentLine)
                    {
                        bookmarks.Add(line);
                    }
                }
            }

            if (bookmarks.Count > 0)
            {
                // Navigate to the closest next bookmark
                int nextLine = bookmarks.Min();
                GotoLine(nextLine);
            }
            else
            {
                // Wrap around to first bookmark
                int? firstLine = null;
                foreach (var item in itemsList)
                {
                    if (item?.Tag is BookmarkData bookmarkData)
                    {
                        int line = bookmarkData.LineNumber;
                        if (!firstLine.HasValue || line < firstLine.Value)
                        {
                            firstLine = line;
                        }
                    }
                }

                if (firstLine.HasValue)
                {
                    GotoLine(firstLine.Value);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1176-1199
        // Original: def _goto_previous_bookmark(self):
        /// <summary>
        /// Navigates to the previous bookmark before the current line.
        /// If no bookmark exists before the current line, wraps around to the last bookmark.
        /// </summary>
        public void GotoPreviousBookmark()
        {
            if (_codeEdit == null || _bookmarkTree == null)
            {
                return;
            }

            int currentLine = GetCurrentLineNumber();

            // Collect all bookmarks before current line
            var bookmarks = new List<int>();
            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ??
                          (_bookmarkTree.Items as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>()).ToList();

            foreach (var item in itemsList)
            {
                if (item?.Tag is BookmarkData bookmarkData)
                {
                    int line = bookmarkData.LineNumber;
                    if (line < currentLine)
                    {
                        bookmarks.Add(line);
                    }
                }
            }

            if (bookmarks.Count > 0)
            {
                // Navigate to the closest previous bookmark
                int previousLine = bookmarks.Max();
                GotoLine(previousLine);
            }
            else
            {
                // Wrap around to last bookmark
                int? lastLine = null;
                foreach (var item in itemsList)
                {
                    if (item?.Tag is BookmarkData bookmarkData)
                    {
                        int line = bookmarkData.LineNumber;
                        if (!lastLine.HasValue || line > lastLine.Value)
                        {
                            lastLine = line;
                        }
                    }
                }

                if (lastLine.HasValue)
                {
                    GotoLine(lastLine.Value);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:282-303
        // Original: def _save_bookmarks(self):
        private void SaveBookmarks()
        {
            if (_bookmarkTree == null)
            {
                return;
            }

            // Collect bookmarks from tree items
            // Matching Python: for i in range(self.ui.bookmarkTree.topLevelItemCount()):
            var bookmarks = new List<Dictionary<string, object>>();
            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ??
                          (_bookmarkTree.Items as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>()).ToList();

            foreach (var item in itemsList)
            {
                if (item?.Tag is BookmarkData bookmarkData)
                {
                    // Matching Python structure: {"line": line_data, "description": item.text(1)}
                    // Skip items without valid line data (matching Python: if line_data is None: continue)
                    if (bookmarkData.LineNumber > 0)
                    {
                        bookmarks.Add(new Dictionary<string, object>
                        {
                            { "line", bookmarkData.LineNumber },
                            { "description", bookmarkData.Description ?? "" }
                        });
                    }
                }
            }

            // Save to application settings using Settings class
            // Matching Python: settings = QSettings("HolocronToolsetV3", "NSSEditor")
            string fileKey = !string.IsNullOrEmpty(_resname)
                ? $"nss_editor/bookmarks/{_resname}"
                : "nss_editor/bookmarks/untitled";

            try
            {
                var settings = new Settings("NSSEditor");
                // Matching Python: settings.setValue(file_key, json.dumps(bookmarks))
                string json = JsonSerializer.Serialize(bookmarks);
                settings.SetValue(fileKey, json);
                // Matching Python: settings.sync() - Save() is called automatically in SetValue
            }
            catch
            {
                // Ignore save errors
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:305-326
        // Original: def load_bookmarks(self):
        private void LoadBookmarks()
        {
            if (_bookmarkTree == null)
            {
                return;
            }

            // Matching Python: settings = QSettings("HolocronToolsetV3", "NSSEditor")
            var settings = new Settings("NSSEditor");

            // Matching Python: file_key = f"nss_editor/bookmarks/{self._resname}" if self._resname else "nss_editor/bookmarks/untitled"
            string fileKey = !string.IsNullOrEmpty(_resname)
                ? $"nss_editor/bookmarks/{_resname}"
                : "nss_editor/bookmarks/untitled";

            // Matching Python: bookmarks_json = settings.value(file_key, "[]")
            string bookmarksJson = settings.GetValue(fileKey, "[]");

            // Matching Python: bookmarks = json.loads(bookmarks_json) if isinstance(bookmarks_json, str) else []
            List<Dictionary<string, object>> bookmarks = new List<Dictionary<string, object>>();

            try
            {
                if (!string.IsNullOrEmpty(bookmarksJson) && bookmarksJson != "[]")
                {
                    // Parse JSON array of bookmark dictionaries
                    using (JsonDocument doc = JsonDocument.Parse(bookmarksJson))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement element in doc.RootElement.EnumerateArray())
                            {
                                if (element.ValueKind == JsonValueKind.Object)
                                {
                                    var bookmark = new Dictionary<string, object>();
                                    foreach (JsonProperty prop in element.EnumerateObject())
                                    {
                                        if (prop.Value.ValueKind == JsonValueKind.Number)
                                        {
                                            bookmark[prop.Name] = prop.Value.GetInt32();
                                        }
                                        else if (prop.Value.ValueKind == JsonValueKind.String)
                                        {
                                            bookmark[prop.Name] = prop.Value.GetString();
                                        }
                                    }
                                    // Matching Python: if isinstance(bookmark, dict) and "line" in bookmark:
                                    if (bookmark.ContainsKey("line"))
                                    {
                                        bookmarks.Add(bookmark);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Matching Python: except json.JSONDecodeError: bookmarks = []
                bookmarks = new List<Dictionary<string, object>>();
            }

            // Matching Python: self.ui.bookmarkTree.clear()
            var itemsList = new List<TreeViewItem>();

            // Matching Python: for bookmark in bookmarks:
            foreach (var bookmark in bookmarks)
            {
                // Matching Python: item = QTreeWidgetItem(self.ui.bookmarkTree)
                // Matching Python: item.setText(0, str(bookmark["line"]))
                // Matching Python: item.setText(1, bookmark.get("description", ""))
                // Matching Python: item.setData(0, Qt.ItemDataRole.UserRole, bookmark["line"])
                int lineNumber = bookmark.ContainsKey("line") && bookmark["line"] is int line ? line : 0;
                string description = bookmark.ContainsKey("description") && bookmark["description"] is string desc ? desc : "";

                if (lineNumber > 0)
                {
                    var item = new TreeViewItem
                    {
                        Header = $"{lineNumber} - {description}",
                        Tag = new BookmarkData { LineNumber = lineNumber, Description = description }
                    };
                    itemsList.Add(item);
                }
            }

            _bookmarkTree.ItemsSource = itemsList;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:541-550
        // Original: def _update_bookmark_visualization(self):
        private void UpdateBookmarkVisualization()
        {
            if (_codeEdit == null)
            {
                return; // No code editor available
            }

            // Collect all bookmark line numbers from the bookmark tree
            var bookmarkedLines = new List<int>();

            // Get bookmark items from the tree view
            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ??
                          (_bookmarkTree.Items as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>()).ToList();

            foreach (var item in itemsList)
            {
                if (item?.Tag is BookmarkData bookmarkData && bookmarkData.LineNumber > 0)
                {
                    bookmarkedLines.Add(bookmarkData.LineNumber);
                }
            }

            // Update the code editor's bookmark visualization
            _codeEdit.SetBookmarkedLines(bookmarkedLines);
        }

        // Public property to access bookmark tree for testing
        public TreeView BookmarkTree => _bookmarkTree;

        // Public property to access snippet list for testing
        // Matching PyKotor: editor.ui.snippetList in test_nss_editor_snippet_persistence
        public ListBox SnippetList => _snippetList;

        // Public property to access resname for testing
        // Matching PyKotor: editor._resname in test_nss_editor_bookmark_persistence
        public string Resname => _resname;

        // Matching PyKotor implementation: highlighter is accessible for testing
        // Original: editor._highlighter in test_nss_editor_syntax_highlighting_game_switch
        /// <summary>
        /// Gets the syntax highlighter instance for testing purposes.
        /// </summary>
        public NWScriptSyntaxHighlighter Highlighter => _highlighter;

        // Original: editor.ui.codeEdit in test_nss_editor_syntax_highlighting_setup
        /// <summary>
        /// Gets the code editor instance for testing purposes.
        /// </summary>
        public CodeEditor CodeEdit => _codeEdit;

        // Matching PyKotor implementation: completer is accessible for testing
        // Original: editor.completer in test_nss_editor_autocompletion_setup
        /// <summary>
        /// Gets the completer instance for testing purposes.
        /// </summary>
        public Completer Completer => _completer;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:979-1085
        // Original: def editor_context_menu(self, pos: QPoint):
        /// <summary>
        /// Enhanced context menu with VS Code-like organization.
        /// Creates a context menu for the code editor with navigation, editing, and code action options.
        /// </summary>
        /// <param name="pos">The position where the context menu should appear (in screen coordinates).</param>
        public void EditorContextMenu(Avalonia.Point pos)
        {
            if (_codeEdit == null)
            {
                return;
            }

            // Create standard context menu (Cut, Copy, Paste, etc.)
            // Matching Python: menu: QMenu | None = self.ui.codeEdit.createStandardContextMenu()
            var contextMenu = new ContextMenu();

            // Get word under cursor for context-aware actions
            // Matching Python: cursor: QTextCursor = self.ui.codeEdit.cursorForPosition(pos)
            // Matching Python: cursor.select(QTextCursor.SelectionType.WordUnderCursor)
            // Matching Python: word_under_cursor: str = cursor.selectedText().strip()
            string wordUnderCursor = "";
            if (!string.IsNullOrEmpty(_codeEdit.Text) && _codeEdit.SelectionStart >= 0)
            {
                int cursorPos = _codeEdit.SelectionStart;
                string text = _codeEdit.Text;

                // Find word boundaries
                int start = cursorPos;
                int end = cursorPos;

                // Move start backward to beginning of word
                while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                {
                    start--;
                }

                // Move end forward to end of word
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                {
                    end++;
                }

                if (end > start)
                {
                    wordUnderCursor = text.Substring(start, end - start).Trim();
                }
            }

            // Standard editing actions (Cut, Copy, Paste)
            var cutItem = new MenuItem { Header = "Cut", HotKey = new KeyGesture(Key.X, KeyModifiers.Control) };
            cutItem.Click += (s, e) => { if (_codeEdit != null) _codeEdit.Cut(); };
            contextMenu.Items.Add(cutItem);

            var copyItem = new MenuItem { Header = "Copy", HotKey = new KeyGesture(Key.C, KeyModifiers.Control) };
            copyItem.Click += (s, e) => { if (_codeEdit != null) _codeEdit.Copy(); };
            contextMenu.Items.Add(copyItem);

            var pasteItem = new MenuItem { Header = "Paste", HotKey = new KeyGesture(Key.V, KeyModifiers.Control) };
            pasteItem.Click += (s, e) => { if (_codeEdit != null) _codeEdit.Paste(); };
            contextMenu.Items.Add(pasteItem);

            contextMenu.Items.Add(new Separator());

            // Code actions
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1050-1054
            // Original: action_toggle_comment: QAction | None = menu.addAction("Toggle Line Comment")
            // Original: action_toggle_comment.setShortcut(QKeySequence("Ctrl+/"))
            // Original: action_toggle_comment.triggered.connect(self.ui.codeEdit.toggle_comment)
            var toggleCommentItem = new MenuItem { Header = "Toggle Line Comment", HotKey = new KeyGesture(Key.OemQuestion, KeyModifiers.Control) };
            toggleCommentItem.Click += (s, e) => { if (_codeEdit != null) _codeEdit.ToggleComment(); };
            contextMenu.Items.Add(toggleCommentItem);

            contextMenu.Items.Add(new Separator());

            // Navigation section (if word under cursor exists)
            if (!string.IsNullOrEmpty(wordUnderCursor))
            {
                // Go to Definition (F12)
                var goToDefItem = new MenuItem { Header = "Go to Definition", HotKey = new KeyGesture(Key.F12) };
                goToDefItem.Click += (s, e) => { GoToDefinition(); };
                contextMenu.Items.Add(goToDefItem);

                // Find All References (Shift+F12)
                var findRefsItem = new MenuItem { Header = "Find All References", HotKey = new KeyGesture(Key.F12, KeyModifiers.Shift) };
                findRefsItem.Click += (s, e) => { FindAllReferencesAtCursor(); };
                contextMenu.Items.Add(findRefsItem);

                contextMenu.Items.Add(new Separator());
            }

            // Go to Line (Ctrl+G)
            var goToLineItem = new MenuItem { Header = "Go to Line...", HotKey = new KeyGesture(Key.G, KeyModifiers.Control) };
            goToLineItem.Click += (s, e) => ShowGotoLine();
            contextMenu.Items.Add(goToLineItem);

            contextMenu.Items.Add(new Separator());

            // Bookmarks section
            int currentLine = GetCurrentLineNumber();
            bool hasBookmark = HasBookmarkAtLine(currentLine);

            if (hasBookmark)
            {
                var removeBookmarkItem = new MenuItem { Header = "Remove Bookmark" };
                removeBookmarkItem.Click += (s, e) => { RemoveBookmarkAtLine(currentLine); };
                contextMenu.Items.Add(removeBookmarkItem);
            }
            else
            {
                var addBookmarkItem = new MenuItem { Header = "Add Bookmark" };
                addBookmarkItem.Click += (s, e) => { AddBookmark(); };
                contextMenu.Items.Add(addBookmarkItem);
            }

            // Show context menu at the specified position
            // Note: In Avalonia, we would typically set ContextMenu property and let the system handle it
            // For this method, we create the menu but don't automatically show it
            // The caller or event handler would show it
            _codeEdit.ContextMenu = contextMenu;
        }

        // Helper method to check if bookmark exists at a line
        private bool HasBookmarkAtLine(int lineNumber)
        {
            if (_bookmarkTree == null)
            {
                return false;
            }

            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ??
                          (_bookmarkTree.Items as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>()).ToList();

            foreach (var item in itemsList)
            {
                if (item?.Tag is BookmarkData bookmarkData && bookmarkData.LineNumber == lineNumber)
                {
                    return true;
                }
            }

            return false;
        }

        // Helper method to remove bookmark at a specific line
        private void RemoveBookmarkAtLine(int lineNumber)
        {
            if (_bookmarkTree == null)
            {
                return;
            }

            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ??
                          (_bookmarkTree.Items as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>()).ToList();

            var itemToRemove = itemsList.FirstOrDefault(item =>
                item?.Tag is BookmarkData bookmarkData && bookmarkData.LineNumber == lineNumber);

            if (itemToRemove != null)
            {
                itemsList.Remove(itemToRemove);
                _bookmarkTree.ItemsSource = itemsList;
                SaveBookmarks();
                UpdateBookmarkVisualization();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:905-950
        // Original: def _update_game_specific_data(self):
        /// <summary>
        /// Updates constants and functions based on the selected game (K1 or TSL).
        /// Populates the function list and constant list with data from ScriptDefs.
        /// Also updates the syntax highlighter rules based on the selected game.
        /// </summary>
        public void UpdateGameSpecificData()
        {
            // Update functions based on the selected game
            _functions.Clear();
            if (_isTsl)
            {
                _functions.AddRange(ScriptDefs.TSL_FUNCTIONS);
            }
            else
            {
                _functions.AddRange(ScriptDefs.KOTOR_FUNCTIONS);
            }

            // Sort functions by name (matching Python implementation)
            _functions = _functions.OrderBy(f => f.Name).ToList();

            // Update constants based on the selected game
            _constants.Clear();
            if (_isTsl)
            {
                _constants.AddRange(ScriptDefs.TSL_CONSTANTS);
            }
            else
            {
                _constants.AddRange(ScriptDefs.KOTOR_CONSTANTS);
            }

            // Sort constants by name (matching Python implementation)
            _constants = _constants.OrderBy(c => c.Name).ToList();

            // Clear and repopulate the function list
            if (_functionList != null)
            {
                _functionList.Items.Clear();

                foreach (var function in _functions)
                {
                    var item = new ListBoxItem
                    {
                        Content = function.Name,
                        Tag = function
                    };
                    _functionList.Items.Add(item);
                }
            }

            // Clear and repopulate the constant list
            if (_constantList != null)
            {
                _constantList.Items.Clear();

                foreach (var constant in _constants)
                {
                    var item = new ListBoxItem
                    {
                        Content = constant.Name,
                        Tag = constant
                    };
                    _constantList.Items.Add(item);
                }
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:955
            // Original: self._highlighter.update_rules(is_tsl=self._is_tsl)
            // Update syntax highlighter rules based on the selected game
            if (_highlighter != null)
            {
                _highlighter.UpdateRules(_isTsl);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:954
            // Original: self._update_completer_model(self.constants, self.functions)
            // Update completer model with new functions and constants
            UpdateCompleterModel(_constants, _functions);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:469-473
        // Original: def _setup_completer (implicit in __init__)
        /// <summary>
        /// Sets up the completer for autocompletion functionality.
        /// </summary>
        private void SetupCompleter()
        {
            if (_codeEdit == null)
            {
                return;
            }

            // Matching PyKotor: self.completer: QCompleter = QCompleter(self)
            _completer = new Completer();
            _completer.SetWidget(_codeEdit);
            _completer.SetCompletionMode(Completer.CompletionMode.PopupCompletion);
            _completer.SetCaseSensitivity(false);  // Case-insensitive matching
            _completer.SetWrapAround(false);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1584-1595
        // Original: def _setup_enhanced_completer(self):
        /// <summary>
        /// Sets up enhanced auto-completion with better presentation.
        /// </summary>
        private void SetupEnhancedCompleter()
        {
            if (_completer == null)
            {
                return;
            }

            // Matching PyKotor: popup.setMaximumHeight(300)
            // Matching PyKotor: popup.setAlternatingRowColors(True)
            // These are handled in the Completer class initialization
            // The popup is configured when it's first created
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1529-1583
        // Original: def _update_completer_model(self, constants: list[ScriptConstant], functions: list[ScriptFunction]):
        /// <summary>
        /// Updates completer model with enhanced information for IntelliSense-style hints.
        /// </summary>
        /// <param name="constants">List of script constants to include in completions.</param>
        /// <param name="functions">List of script functions to include in completions.</param>
        public void UpdateCompleterModel(List<ScriptConstant> constants, List<ScriptFunction> functions)
        {
            if (_completer == null)
            {
                return;
            }

            // Create enriched completion strings with type hints
            var completerList = new List<string>();

            // Add functions with return type hints and parameter info
            if (functions != null)
            {
                foreach (var func in functions)
                {
                    string returnType = func.ReturnType.ToScriptString();

                    // Try to get parameter info
                    if (func.Params != null && func.Params.Count > 0)
                    {
                        var paramStrings = func.Params.Take(3).Select(p =>
                        {
                            string paramType = p.DataType.ToScriptString();
                            string paramName = p.Name ?? "";
                            return $"{paramType} {paramName}";
                        }).ToList();

                        string paramStr = string.Join(", ", paramStrings);
                        if (func.Params.Count > 3)
                        {
                            paramStr += "...";
                        }
                        completerList.Add($"{func.Name}({paramStr}) → {returnType}");
                    }
                    else
                    {
                        completerList.Add($"{func.Name}(...) → {returnType}");
                    }
                }
            }

            // Add constants with type hints
            if (constants != null)
            {
                foreach (var constItem in constants)
                {
                    string constType = constItem.DataType.ToScriptString();
                    string constValue = constItem.Value?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(constType) && !string.IsNullOrEmpty(constValue))
                    {
                        completerList.Add($"{constItem.Name} ({constType} = {constValue})");
                    }
                    else if (!string.IsNullOrEmpty(constType))
                    {
                        completerList.Add($"{constItem.Name} ({constType})");
                    }
                    else if (!string.IsNullOrEmpty(constValue))
                    {
                        completerList.Add($"{constItem.Name} = {constValue}");
                    }
                    else
                    {
                        completerList.Add(constItem.Name);
                    }
                }
            }

            // Also add keywords
            var keywords = new List<string>
            {
                "void", "int", "float", "string", "object", "vector", "location", "effect", "event",
                "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue",
                "return", "struct", "const", "include", "define"
            };
            completerList.AddRange(keywords);

            // Set the model
            _completer.SetModel(completerList);

            // Store mapping for quick lookup
            _completionMap.Clear();
            if (functions != null)
            {
                foreach (var func in functions)
                {
                    _completionMap[func.Name] = func;
                    // Also map with parentheses for function calls
                    _completionMap[$"{func.Name}("] = func;
                }
            }
            if (constants != null)
            {
                foreach (var constItem in constants)
                {
                    _completionMap[constItem.Name] = constItem;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2356-2361
        // Original: def insert_selected_function(self):
        /// <summary>
        /// Inserts the selected function from the function list into the code editor at the cursor position.
        /// Inserts the function name followed by parentheses, with cursor positioned inside the parentheses.
        /// </summary>
        public void InsertSelectedFunction()
        {
            if (_functionList == null || _codeEdit == null)
            {
                return;
            }

            var selectedItem = _functionList.SelectedItem as ListBoxItem;
            if (selectedItem == null)
            {
                return;
            }

            var function = selectedItem.Tag as ScriptFunction;
            if (function == null)
            {
                return;
            }

            // Insert function name with parentheses: "FunctionName()"
            string insert = $"{function.Name}()";

            // Insert text at cursor position
            InsertTextAtCursor(insert, insert.IndexOf("(") + 1);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:774-785
        // Original: def insert_text_at_cursor(self, insert: str, offset: int | None = None):
        /// <summary>
        /// Inserts text at the current cursor position in the code editor.
        /// </summary>
        /// <param name="insert">The text to insert</param>
        /// <param name="offset">Optional offset to position cursor after insertion. If null, cursor is placed at end of inserted text.</param>
        private void InsertTextAtCursor(string insert, int? offset = null)
        {
            if (_codeEdit == null || string.IsNullOrEmpty(insert))
            {
                return;
            }

            int cursorPosition = _codeEdit.SelectionStart;
            string text = _codeEdit.Text ?? "";

            // If there's a selection, remove it first
            if (_codeEdit.SelectionStart != _codeEdit.SelectionEnd)
            {
                int selectionStart = Math.Min(_codeEdit.SelectionStart, _codeEdit.SelectionEnd);
                int selectionEnd = Math.Max(_codeEdit.SelectionStart, _codeEdit.SelectionEnd);
                text = text.Substring(0, selectionStart) + text.Substring(selectionEnd);
                cursorPosition = selectionStart;
            }

            // Insert the text
            text = text.Substring(0, cursorPosition) + insert + text.Substring(cursorPosition);
            _codeEdit.Text = text;

            // Set cursor position (default to end of inserted text if offset not specified)
            int newPosition = cursorPosition + (offset ?? insert.Length);
            _codeEdit.SelectionStart = newPosition;
            _codeEdit.SelectionEnd = newPosition;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:227-232
        // Original: def _setup_bookmarks(self):
        private void SetupFunctionList()
        {
            _functionList = new ListBox();

            // Initialize constant list if not already initialized
            if (_constantList == null)
            {
                _constantList = new ListBox();
            }

            // Initialize search edit boxes if not already initialized
            if (_functionSearchEdit == null)
            {
                _functionSearchEdit = new TextBox();
            }

            if (_constantSearchEdit == null)
            {
                _constantSearchEdit = new TextBox();
            }

            // Initialize game selector if not already initialized
            if (_gameSelector == null)
            {
                _gameSelector = new ComboBox();
                _gameSelector.Items.Add("KOTOR");
                _gameSelector.Items.Add("TSL");
                _gameSelector.SelectedIndex = _isTsl ? 1 : 0;
            }

            // Initialize status label if not already initialized
            if (_statusLabel == null)
            {
                _statusLabel = new Label();
            }

            // Update game-specific data when function list is set up
            UpdateGameSpecificData();
        }

        // Public property to access function list for testing
        public ListBox FunctionList => _functionList;

        // Public property to access constant list for testing
        public ListBox ConstantList => _constantList;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        // Original: Status bar is set up in UI setup, accessible via statusBar() method
        /// <summary>
        /// Sets up the status bar UI element at the bottom of the window.
        /// Creates a status bar with a label that displays cursor position and selection info.
        /// </summary>
        private void SetupStatusBar()
        {
            // Create status bar border (similar to Qt's QStatusBar)
            _statusBar = new Border
            {
                Height = 25,
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(240, 240, 240)),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
            };

            // Create a panel to hold the status label
            var statusPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Avalonia.Thickness(4, 0, 4, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            // Initialize status label if not already initialized
            if (_statusLabel == null)
            {
                _statusLabel = new Label
                {
                    Content = "Ln 1, Col 1 | 1 lines",
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
            }

            statusPanel.Children.Add(_statusLabel);
            _statusBar.Child = statusPanel;

            // Add status bar to window content structure
            // Check if content is already a DockPanel (may have been created by AddHelpAction)
            if (Content is DockPanel dockPanel)
            {
                // Content is already a DockPanel, check if status bar is already added
                if (!dockPanel.Children.Contains(_statusBar))
                {
                    dockPanel.Children.Add(_statusBar);
                    DockPanel.SetDock(_statusBar, Dock.Bottom);
                }
            }
            else if (Content is Control existingContent)
            {
                // Wrap existing content in DockPanel and add status bar
                var newDockPanel = new DockPanel();
                newDockPanel.Children.Add(existingContent);
                newDockPanel.Children.Add(_statusBar);
                DockPanel.SetDock(_statusBar, Dock.Bottom);
                Content = newDockPanel;
            }
            else
            {
                // No content yet, create DockPanel with status bar
                var newDockPanel = new DockPanel();
                if (_codeEdit != null)
                {
                    newDockPanel.Children.Add(_codeEdit);
                }
                newDockPanel.Children.Add(_statusBar);
                DockPanel.SetDock(_statusBar, Dock.Bottom);
                Content = newDockPanel;
            }

            // Initial status bar update
            UpdateStatusBar();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        // Original: def statusBar(self) -> QStatusBar: return self.statusBar()
        /// <summary>
        /// Returns the status bar UI element for test compatibility.
        /// Matches PyKotor's statusBar() method signature.
        /// </summary>
        /// <returns>The status bar Border control, or null if not initialized.</returns>
        public Border StatusBar()
        {
            return _statusBar;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2534-2542
        // Original: Command Palette (VS Code Ctrl+Shift+P)
        // Original: self._command_palette = CommandPalette(self)
        // Original: self._setup_command_palette()
        // Original: command_palette_shortcut = QShortcut(QKeySequence("Ctrl+Shift+P"), self)
        // Original: command_palette_shortcut.activated.connect(self._show_command_palette)
        // Original: quick_open_shortcut = QShortcut(QKeySequence("Ctrl+P"), self)
        // Original: quick_open_shortcut.activated.connect(self._show_command_palette)
        /// <summary>
        /// Sets up the command palette with all available commands and keyboard shortcuts.
        /// Command palette is created lazily on first show (matching PyKotor behavior).
        /// </summary>
        private void SetupCommandPalette()
        {
            // Command palette is created lazily on first show (matching PyKotor: created on first show)
            // Set up keyboard shortcuts for showing command palette
            // Ctrl+Shift+P (VS Code style command palette)
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.P && (e.KeyModifiers & KeyModifiers.Control) != 0 && (e.KeyModifiers & KeyModifiers.Shift) != 0)
                {
                    ShowCommandPalette();
                    e.Handled = true;
                }
                // TODO:  Ctrl+P (Quick Open - for now just show command palette, matching PyKotor)
                else if (e.Key == Key.P && (e.KeyModifiers & KeyModifiers.Control) != 0 && (e.KeyModifiers & KeyModifiers.Shift) == 0)
                {
                    ShowCommandPalette();
                    e.Handled = true;
                }
            };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:603-705
        // Original: def _setup_command_palette(self):
        /// <summary>
        /// Sets up the command palette with all available commands.
        /// Called lazily when command palette is first shown.
        /// </summary>
        private void SetupCommandPaletteCommands()
        {
            if (_commandPalette == null)
            {
                return;
            }

            // File operations
            _commandPalette.RegisterCommand("file.new", "New File", () => New(), "File");
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/file.py:30-41
            // Original: def open(self): Opens a file dialog to select a file to open
            _commandPalette.RegisterCommand("file.open", "Open File...", () => OpenFile(), "File");
            _commandPalette.RegisterCommand("file.save", "Save", () => Save(), "File");
            _commandPalette.RegisterCommand("file.save_as", "Save As...", () => SaveAs(), "File");
            // Note: Save All, Close, Close All are typically handled by the application, not individual editors
            // But we register them for completeness
            _commandPalette.RegisterCommand("file.close", "Close", () => Close(), "File");

            // Edit operations
            if (_codeEdit != null)
            {
                _commandPalette.RegisterCommand("edit.undo", "Undo", () => _codeEdit.Undo(), "Edit");
                _commandPalette.RegisterCommand("edit.redo", "Redo", () => _codeEdit.Redo(), "Edit");
                _commandPalette.RegisterCommand("edit.cut", "Cut", () => _codeEdit.Cut(), "Edit");
                _commandPalette.RegisterCommand("edit.copy", "Copy", () => _codeEdit.Copy(), "Edit");
                _commandPalette.RegisterCommand("edit.paste", "Paste", () => _codeEdit.Paste(), "Edit");
            }
            _commandPalette.RegisterCommand("edit.find", "Find", () => ShowFind(), "Edit");
            _commandPalette.RegisterCommand("edit.replace", "Replace", () => ShowReplace(), "Edit");
            // Code editor operations
            _commandPalette.RegisterCommand("edit.toggleComment", "Toggle Line Comment", () => { if (_codeEdit != null) _codeEdit.ToggleComment(); }, "Edit");
            _commandPalette.RegisterCommand("edit.duplicateLine", "Duplicate Line", () => { if (_codeEdit != null) _codeEdit.DuplicateLine(); }, "Edit");
            _commandPalette.RegisterCommand("edit.deleteLine", "Delete Line", () => { if (_codeEdit != null) _codeEdit.DeleteLine(); }, "Edit");
            _commandPalette.RegisterCommand("edit.moveLineUp", "Move Line Up", () => { if (_codeEdit != null) _codeEdit.MoveLineUp(); }, "Edit");
            _commandPalette.RegisterCommand("edit.moveLineDown", "Move Line Down", () => { if (_codeEdit != null) _codeEdit.MoveLineDown(); }, "Edit");

            // View operations
            // Note: Toggle Explorer, Terminal, Output Panel would need UI actions to trigger
            // TODO: STUB - For now, we register placeholders
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:630, 677
            // Original: {"id": "view.toggleExplorer", "label": "Toggle Explorer", "category": "View"},
            // Original: "view.toggleExplorer": lambda: self.ui.actionToggleFileExplorer.trigger(),
            _commandPalette.RegisterCommand("view.toggleExplorer", "Toggle Explorer", () => ToggleFileExplorer(), "View");
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:631, 678
            // Original: {"id": "view.toggleTerminal", "label": "Toggle Terminal", "category": "View"},
            // Original: "view.toggleTerminal": lambda: self.ui.actionToggleTerminal.trigger(),
            _commandPalette.RegisterCommand("view.toggleTerminal", "Toggle Terminal", () => ToggleTerminalPanel(), "View");
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2749-2759
            // Original: "view.toggleOutput": lambda: self.ui.actionToggle_Output_Panel.trigger()
            _commandPalette.RegisterCommand("view.toggleOutput", "Toggle Output Panel", () => ToggleOutputPanel(), "View");
            _commandPalette.RegisterCommand("view.zoomIn", "Zoom In", () => { if (_codeEdit != null) _codeEdit.ZoomIn(); }, "View");
            _commandPalette.RegisterCommand("view.zoomOut", "Zoom Out", () => { if (_codeEdit != null) _codeEdit.ZoomOut(); }, "View");
            _commandPalette.RegisterCommand("view.resetZoom", "Reset Zoom", () => { if (_codeEdit != null) _codeEdit.ResetZoom(); }, "View");
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2778-2788
            // Original: def _toggle_word_wrap(self):
            _commandPalette.RegisterCommand("view.toggleWordWrap", "Toggle Word Wrap", () =>
            {
                if (_codeEdit != null)
                {
                    bool isEnabled = _codeEdit.ToggleWordWrap();
                    // Matching PyKotor: log to output
                    // Original: self._log_to_output("Word wrap: ON") or "Word wrap: OFF"
                    LogToOutput($"Word wrap: {(isEnabled ? "ON" : "OFF")}");
                }
            }, "View");

            // Navigation
            _commandPalette.RegisterCommand("navigate.gotoLine", "Go to Line...", () => ShowGotoLine(), "Navigation");
            _commandPalette.RegisterCommand("navigate.gotoDefinition", "Go to Definition", () => GoToDefinition(), "Navigation");
            _commandPalette.RegisterCommand("navigate.findReferences", "Find All References", () => FindAllReferencesAtCursor(), "Navigation");

            // Code operations
            _commandPalette.RegisterCommand("code.compile", "Compile Script", () => CompileCurrentScript(), "Code");
            // Note: Format and Analyze would need implementation
            _commandPalette.RegisterCommand("code.format", "Format Document", () => { FormatDocument(); }, "Code");
            _commandPalette.RegisterCommand("code.analyze", "Analyze Code", () => AnalyzeCode(), "Code");

            // Bookmarks
            _commandPalette.RegisterCommand("bookmark.toggle", "Toggle Bookmark", () => ToggleBookmarkAtCursor(), "Bookmarks");
            _commandPalette.RegisterCommand("bookmark.next", "Next Bookmark", () => GotoNextBookmark(), "Bookmarks");
            _commandPalette.RegisterCommand("bookmark.previous", "Previous Bookmark", () => GotoPreviousBookmark(), "Bookmarks");

            // Help
            _commandPalette.RegisterCommand("help.documentation", "Show Documentation", () => ShowDocumentation(), "Help");
            _commandPalette.RegisterCommand("help.shortcuts", "Show Keyboard Shortcuts", () => ShowKeyboardShortcuts(), "Help");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:707-709
        // Original: def _show_command_palette(self):
        /// <summary>
        /// Shows the command palette. Creates it lazily on first show if not already created.
        /// </summary>
        private void ShowCommandPalette()
        {
            // Create command palette lazily on first show (matching PyKotor behavior)
            if (_commandPalette == null)
            {
                _commandPalette = new CommandPalette(this);
                SetupCommandPaletteCommands();
            }

            _commandPalette.ShowPalette();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2363-2373
        // Original: def on_function_search(self):
        /// <summary>
        /// Filters the function list based on the search text.
        /// Hides items that don't match the search string (case-insensitive).
        /// </summary>
        /// <param name="searchText">The text to search for in function names.</param>
        public void OnFunctionSearch(string searchText)
        {
            if (_functionList == null)
            {
                return;
            }

            // If search text is empty or whitespace, show all items
            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var item in _functionList.Items.OfType<ListBoxItem>())
                {
                    item.IsVisible = true;
                }
                return;
            }

            // Filter items based on search text (case-insensitive)
            string lowerSearchText = searchText.ToLowerInvariant();
            foreach (var item in _functionList.Items.OfType<ListBoxItem>())
            {
                if (item?.Content is string itemText)
                {
                    item.IsVisible = itemText.ToLowerInvariant().Contains(lowerSearchText);
                }
                else
                {
                    item.IsVisible = false;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2613-2661
        // Original: def _setup_breadcrumbs(self):
        /// <summary>
        /// Set up breadcrumbs navigation widget above the code editor.
        /// </summary>
        private void SetupBreadcrumbs()
        {
            // Create breadcrumbs widget
            _breadcrumbs = new BreadcrumbsWidget();
            _breadcrumbs.ItemClicked += OnBreadcrumbClicked;

            // Initially clear breadcrumbs
            _breadcrumbs.Clear();
        }

        // Public property to access breadcrumbs for testing
        public BreadcrumbsWidget Breadcrumbs => _breadcrumbs;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:419-454
        // Original: def _setup_outline_view(self):
        /// <summary>
        /// Set up outline view widget for displaying code symbols (functions, variables, structs).
        /// </summary>
        private void SetupOutline()
        {
            // Create outline view TreeView
            _outlineView = new TreeView();

            // Initially clear outline
            _outlineView.ItemsSource = new List<TreeViewItem>();
        }

        // Public property to access outline view for testing
        public TreeView OutlineView => _outlineView;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2385-2392
        // Original: def _update_outline(self):
        /// <summary>
        /// Update outline view by parsing the current NSS code and extracting symbols (functions, variables, structs).
        /// Populates the outline TreeView with hierarchical symbol information.
        /// </summary>
        public void UpdateOutline()
        {
            if (_outlineView == null || _codeEdit == null)
            {
                return;
            }

            // Clear existing outline items
            _outlineView.ItemsSource = new List<TreeViewItem>();

            string code = _codeEdit.ToPlainText();
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            // Extract symbols from code
            var symbols = ExtractSymbolsFromCode(code);

            // Populate TreeView with symbols
            var outlineItems = new List<TreeViewItem>();
            foreach (var symbol in symbols)
            {
                var item = CreateOutlineItem(symbol);
                if (item != null)
                {
                    outlineItems.Add(item);
                }
            }

            _outlineView.ItemsSource = outlineItems;
        }

        /// <summary>
        /// Represents a symbol extracted from NSS code (function, variable, struct, etc.).
        /// </summary>
        private class OutlineSymbol
        {
            public string Name { get; set; }
            public string Kind { get; set; } // "function", "variable", "struct", etc.
            public string Detail { get; set; } // Function signature, variable type, etc.
            public int LineNumber { get; set; } // 0-based line number
            public List<OutlineSymbol> Children { get; set; } // Parameters, nested symbols, etc.

            public OutlineSymbol()
            {
                Children = new List<OutlineSymbol>();
            }
        }

        /// <summary>
        /// Extracts symbols (functions, variables, structs) from NSS source code.
        /// Uses regex patterns to identify top-level declarations.
        /// </summary>
        private List<OutlineSymbol> ExtractSymbolsFromCode(string code)
        {
            var symbols = new List<OutlineSymbol>();
            string[] lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            // Remove comments and preprocessor directives for parsing
            string processedCode = PreprocessCodeForParsing(code);
            string[] processedLines = processedCode.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            // Pattern for function declarations: returnType functionName(parameters) { ... }
            // Examples: "void main()", "int GetValue(int x, int y)", "string GetName()"
            var functionPattern = new Regex(@"^\s*(?:const\s+)?(\w+)\s+(\w+)\s*\([^)]*\)\s*\{?", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            // Pattern for global variable declarations: type variableName [= value];
            // Examples: "int g_globalVar = 10;", "string name;"
            var globalVarPattern = new Regex(@"^\s*(?:const\s+)?(\w+)\s+(\w+)\s*(?:=\s*[^;]+)?\s*;", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            // Pattern for struct declarations: struct StructName { ... }
            var structPattern = new Regex(@"^\s*struct\s+(\w+)\s*\{", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            // Find functions
            foreach (Match match in functionPattern.Matches(processedCode))
            {
                string returnType = match.Groups[1].Value;
                string functionName = match.Groups[2].Value;

                // Skip if it's a variable declaration that looks like a function (e.g., "int x()" as variable)
                if (IsVariableDeclaration(processedCode, match.Index))
                {
                    continue;
                }

                // Get line number
                int lineNumber = GetLineNumberFromIndex(code, match.Index);

                // Extract function signature detail
                string fullMatch = match.Value;
                int parenStart = fullMatch.IndexOf('(');
                string detail = parenStart >= 0 ? fullMatch.Substring(0, parenStart).Trim() + "()" : fullMatch.Trim();

                var symbol = new OutlineSymbol
                {
                    Name = functionName,
                    Kind = "function",
                    Detail = $"{returnType} {functionName}()",
                    LineNumber = lineNumber
                };

                // Extract parameters if available
                ExtractFunctionParameters(processedCode, match, symbol);

                symbols.Add(symbol);
            }

            // Find global variables
            foreach (Match match in globalVarPattern.Matches(processedCode))
            {
                string varType = match.Groups[1].Value;
                string varName = match.Groups[2].Value;

                // Skip if it's inside a function or struct
                if (IsInsideFunctionOrStruct(processedCode, match.Index))
                {
                    continue;
                }

                // Skip common keywords that might be mistaken for types
                if (IsKeyword(varType))
                {
                    continue;
                }

                int lineNumber = GetLineNumberFromIndex(code, match.Index);

                var symbol = new OutlineSymbol
                {
                    Name = varName,
                    Kind = "variable",
                    Detail = $"{varType} {varName}",
                    LineNumber = lineNumber
                };

                symbols.Add(symbol);
            }

            // Find structs
            foreach (Match match in structPattern.Matches(processedCode))
            {
                string structName = match.Groups[1].Value;
                int lineNumber = GetLineNumberFromIndex(code, match.Index);

                var symbol = new OutlineSymbol
                {
                    Name = structName,
                    Kind = "struct",
                    Detail = $"struct {structName}",
                    LineNumber = lineNumber
                };

                symbols.Add(symbol);
            }

            // Sort symbols by line number
            symbols.Sort((a, b) => a.LineNumber.CompareTo(b.LineNumber));

            return symbols;
        }

        /// <summary>
        /// Preprocesses code by removing comments and normalizing whitespace for parsing.
        /// </summary>
        private string PreprocessCodeForParsing(string code)
        {
            var result = new StringBuilder();
            string[] lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            bool inBlockComment = false;

            foreach (string line in lines)
            {
                string processedLine = line;

                // Handle block comments
                if (inBlockComment)
                {
                    int endIndex = processedLine.IndexOf("*/");
                    if (endIndex >= 0)
                    {
                        processedLine = processedLine.Substring(endIndex + 2);
                        inBlockComment = false;
                    }
                    else
                    {
                        processedLine = ""; // Entire line is in block comment
                        result.AppendLine(processedLine);
                        continue;
                    }
                }

                // Check for block comment start
                int blockStart = processedLine.IndexOf("/*");
                if (blockStart >= 0)
                {
                    int blockEnd = processedLine.IndexOf("*/", blockStart);
                    if (blockEnd >= 0)
                    {
                        processedLine = processedLine.Substring(0, blockStart) + processedLine.Substring(blockEnd + 2);
                    }
                    else
                    {
                        processedLine = processedLine.Substring(0, blockStart);
                        inBlockComment = true;
                    }
                }

                // Remove line comments
                int lineCommentIndex = processedLine.IndexOf("//");
                if (lineCommentIndex >= 0)
                {
                    processedLine = processedLine.Substring(0, lineCommentIndex);
                }

                result.AppendLine(processedLine);
            }

            return result.ToString();
        }

        /// <summary>
        /// Checks if a match position is inside a function or struct body.
        /// </summary>
        private bool IsInsideFunctionOrStruct(string code, int position)
        {
            // Simple heuristic: count opening and closing braces before this position
            int braceCount = 0;
            for (int i = 0; i < position && i < code.Length; i++)
            {
                if (code[i] == '{')
                {
                    braceCount++;
                }
                else if (code[i] == '}')
                {
                    braceCount--;
                }
            }
            return braceCount > 0;
        }

        /// <summary>
        /// Checks if a match is actually a variable declaration that looks like a function.
        /// </summary>
        private bool IsVariableDeclaration(string code, int position)
        {
            // Look backwards to see if there's an assignment or semicolon nearby
            int searchStart = Math.Max(0, position - 50);
            string context = code.Substring(searchStart, Math.Min(50, position - searchStart));
            return context.Contains("=") || context.Contains(";");
        }

        /// <summary>
        /// Checks if a string is a C/NSS keyword.
        /// </summary>
        private bool IsKeyword(string word)
        {
            string[] keywords = { "void", "int", "float", "string", "object", "vector", "struct", "const", "if", "else", "for", "while", "return", "break", "continue", "switch", "case", "default" };
            return Array.IndexOf(keywords, word.ToLowerInvariant()) >= 0;
        }

        /// <summary>
        /// Gets the line number (0-based) from a character index in the code.
        /// </summary>
        private int GetLineNumberFromIndex(string code, int index)
        {
            int lineNumber = 0;
            for (int i = 0; i < index && i < code.Length; i++)
            {
                if (code[i] == '\n')
                {
                    lineNumber++;
                }
            }
            return lineNumber;
        }

        /// <summary>
        /// Extracts function parameters from a function declaration match.
        /// </summary>
        private void ExtractFunctionParameters(string code, Match functionMatch, OutlineSymbol symbol)
        {
            try
            {
                int parenStart = functionMatch.Index + functionMatch.Value.IndexOf('(');
                if (parenStart < 0) return;

                int parenEnd = FindMatchingParen(code, parenStart);
                if (parenEnd < 0) return;

                string paramString = code.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
                if (string.IsNullOrEmpty(paramString))
                {
                    return; // No parameters
                }

                // Parse parameters (simple approach: split by comma)
                string[] paramsArray = paramString.Split(',');
                foreach (string param in paramsArray)
                {
                    string trimmedParam = param.Trim();
                    if (string.IsNullOrEmpty(trimmedParam))
                    {
                        continue;
                    }

                    // Extract parameter name and type
                    string[] parts = trimmedParam.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string paramType = parts[parts.Length - 2];
                        string paramName = parts[parts.Length - 1];

                        var paramSymbol = new OutlineSymbol
                        {
                            Name = paramName,
                            Kind = "parameter",
                            Detail = $"{paramType} {paramName}",
                            LineNumber = symbol.LineNumber
                        };
                        symbol.Children.Add(paramSymbol);
                    }
                }
            }
            catch
            {
                // Ignore parameter extraction errors
            }
        }

        /// <summary>
        /// Finds the matching closing parenthesis for an opening parenthesis.
        /// </summary>
        private int FindMatchingParen(string code, int openParenIndex)
        {
            int depth = 1;
            for (int i = openParenIndex + 1; i < code.Length; i++)
            {
                if (code[i] == '(')
                {
                    depth++;
                }
                else if (code[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }
            return -1; // Not found
        }

        /// <summary>
        /// Creates a TreeViewItem from an OutlineSymbol.
        /// </summary>
        private TreeViewItem CreateOutlineItem(OutlineSymbol symbol)
        {
            string displayText = GetSymbolDisplayText(symbol);

            var item = new TreeViewItem
            {
                Header = displayText,
                Tag = symbol
            };

            // Add children (parameters, etc.)
            if (symbol.Children != null && symbol.Children.Count > 0)
            {
                var childItems = new List<TreeViewItem>();
                foreach (var child in symbol.Children)
                {
                    var childItem = new TreeViewItem
                    {
                        Header = GetSymbolDisplayText(child),
                        Tag = child
                    };
                    childItems.Add(childItem);
                }
                item.ItemsSource = childItems;
            }

            return item;
        }

        /// <summary>
        /// Gets the display text for a symbol based on its kind.
        /// </summary>
        private string GetSymbolDisplayText(OutlineSymbol symbol)
        {
            switch (symbol.Kind)
            {
                case "function":
                    return $"🔷 {symbol.Name}";
                case "struct":
                    return $"📦 {symbol.Name}";
                case "variable":
                    return $"🌐 {symbol.Name}";
                case "parameter":
                    return $"  📝 {symbol.Name}: {symbol.Detail}";
                default:
                    return symbol.Name;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2663-2671
        // Original: def _update_breadcrumbs(self):
        /// <summary>
        /// Update breadcrumbs when cursor position changes.
        /// Parses the script to find functions/structs and determines which one contains the current cursor line.
        /// </summary>
        public void UpdateBreadcrumbs()
        {
            if (_breadcrumbs == null || _codeEdit == null)
            {
                return;
            }

            string text = _codeEdit.ToPlainText();
            if (string.IsNullOrEmpty(text))
            {
                _breadcrumbs.Clear();
                return;
            }

            // Get current line number (0-indexed to match Python implementation)
            int currentLine = GetCurrentLineNumber() - 1; // Convert to 0-indexed

            // Parse script to find functions and structs
            var symbols = ParseSymbols(text);

            // Build breadcrumb path
            var breadcrumbPath = new List<string>();

            // Add filename
            if (!string.IsNullOrEmpty(_resname))
            {
                breadcrumbPath.Add(_resname);
            }
            else if (!string.IsNullOrEmpty(_filepath))
            {
                breadcrumbPath.Add(System.IO.Path.GetFileName(_filepath));
            }
            else
            {
                breadcrumbPath.Add("Untitled");
            }

            // Find containing symbol
            foreach (var symbol in symbols)
            {
                if (symbol.StartLine <= currentLine && currentLine <= symbol.EndLine)
                {
                    string kind = symbol.Kind;
                    string name = symbol.Name;
                    if (!string.IsNullOrEmpty(kind) && !string.IsNullOrEmpty(name))
                    {
                        // Capitalize first letter of kind (Function -> Function, Struct -> Struct)
                        string kindTitle = char.ToUpper(kind[0]) + (kind.Length > 1 ? kind.Substring(1).ToLower() : "");
                        breadcrumbPath.Add($"{kindTitle}: {name}");
                    }
                    break;
                }
            }

            // Update breadcrumbs widget
            _breadcrumbs.SetPath(breadcrumbPath);
        }

        // Helper class to represent a symbol (function, struct, variable)
        private class SymbolInfo
        {
            public string Kind { get; set; } // "function", "struct", "variable"
            public string Name { get; set; }
            public int StartLine { get; set; } // 0-indexed
            public int EndLine { get; set; } // 0-indexed
        }

        // Parse script to find functions, structs, and variables
        // Returns list of symbols with their line ranges
        private List<SymbolInfo> ParseSymbols(string text)
        {
            var symbols = new List<SymbolInfo>();
            if (string.IsNullOrEmpty(text))
            {
                return symbols;
            }

            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
            int braceDepth = 0;
            SymbolInfo currentSymbol = null;
            int symbolStartLine = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmedLine = line.Trim();

                // Check for function definition: void funcName(, int funcName(, etc.
                if (currentSymbol == null)
                {
                    // Pattern: returnType funcName(
                    // Match: void, int, float, string, object followed by identifier and (
                    // Use original line (not trimmed) to preserve position for brace tracking
                    var functionMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"^\s*(void|int|float|string|object)\s+(\w+)\s*\(");
                    if (functionMatch.Success)
                    {
                        string funcName = functionMatch.Groups[2].Value;
                        currentSymbol = new SymbolInfo
                        {
                            Kind = "function",
                            Name = funcName,
                            StartLine = i
                        };
                        symbolStartLine = i;
                        braceDepth = 0;

                        // Check if opening brace is on the same line
                        foreach (char c in line)
                        {
                            if (c == '{')
                            {
                                braceDepth++;
                                break; // Found opening brace on same line
                            }
                        }

                        // If we found the opening brace, continue to next iteration to track closing
                        if (braceDepth > 0)
                        {
                            continue;
                        }
                        // Otherwise, continue to next line to look for opening brace
                        continue;
                    }

                    // Check for struct definition: struct StructName
                    var structMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"^\s*struct\s+(\w+)");
                    if (structMatch.Success)
                    {
                        string structName = structMatch.Groups[1].Value;
                        currentSymbol = new SymbolInfo
                        {
                            Kind = "struct",
                            Name = structName,
                            StartLine = i
                        };
                        symbolStartLine = i;
                        braceDepth = 0;

                        // Check if opening brace is on the same line
                        foreach (char c in line)
                        {
                            if (c == '{')
                            {
                                braceDepth++;
                                break; // Found opening brace on same line
                            }
                        }

                        // If we found the opening brace, continue to next iteration to track closing
                        if (braceDepth > 0)
                        {
                            continue;
                        }
                        // Otherwise, continue to next line to look for opening brace
                        continue;
                    }
                }

                // Track brace depth to find end of function/struct
                if (currentSymbol != null)
                {
                    foreach (char c in line)
                    {
                        if (c == '{')
                        {
                            braceDepth++;
                        }
                        else if (c == '}')
                        {
                            braceDepth--;
                            if (braceDepth == 0)
                            {
                                // Found end of function/struct
                                currentSymbol.EndLine = i;
                                symbols.Add(currentSymbol);
                                currentSymbol = null;
                                break;
                            }
                        }
                    }
                }
            }

            // If we have an unclosed symbol (malformed code), set end line to last line
            if (currentSymbol != null)
            {
                currentSymbol.EndLine = lines.Length - 1;
                symbols.Add(currentSymbol);
            }

            return symbols;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2672-2684
        // Original: def _on_breadcrumb_clicked(self, segment: str):
        /// <summary>
        /// Handle breadcrumb segment click - navigate to that context.
        /// </summary>
        /// <param name="segment">The breadcrumb segment that was clicked (e.g., "Function: main", "Struct: MyStruct", "Variable: x")</param>
        private void OnBreadcrumbClicked(string segment)
        {
            // If clicking on filename, do nothing (already there)
            // If clicking on function/struct, navigate to it
            if (segment != null && segment.StartsWith("Function: "))
            {
                string funcName = segment.Substring("Function: ".Length);
                NavigateToSymbol(funcName);
            }
            else if (segment != null && segment.StartsWith("Struct: "))
            {
                string structName = segment.Substring("Struct: ".Length);
                NavigateToSymbol(structName);
            }
            else if (segment != null && segment.StartsWith("Variable: "))
            {
                string varName = segment.Substring("Variable: ".Length);
                NavigateToSymbol(varName);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2686-2705
        // Original: def _navigate_to_symbol(self, symbol_name: str):
        /// <summary>
        /// Navigate to a symbol (function, struct, variable) by name.
        /// Searches through the code editor text to find the symbol definition and moves the cursor to it.
        /// </summary>
        /// <param name="symbolName">The name of the symbol to navigate to</param>
        /// <returns>True if the symbol was found and navigation occurred, false otherwise</returns>
        private bool NavigateToSymbol(string symbolName)
        {
            if (_codeEdit == null || string.IsNullOrEmpty(symbolName))
            {
                return false;
            }

            string text = _codeEdit.ToPlainText();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1; // 1-indexed

                // Look for function definition
                // Check for: void symbolName(, int symbolName(, float symbolName(, etc.
                // Use word boundary matching to avoid partial matches
                string escapedSymbol = Regex.Escape(symbolName);
                string functionPattern = @"\b(void|int|float|string|object)\s+" + escapedSymbol + @"\s*\(";
                if (Regex.IsMatch(line, functionPattern, RegexOptions.IgnoreCase))
                {
                    GotoLine(lineNumber);
                    return true;
                }

                // Look for struct definition
                string structPattern = @"\bstruct\s+" + escapedSymbol + @"\b";
                if (Regex.IsMatch(line, structPattern, RegexOptions.IgnoreCase))
                {
                    GotoLine(lineNumber);
                    return true;
                }

                // Look for variable declaration
                // More specific check: symbol name must be followed by = or ; and line must contain a type keyword
                string varPattern = @"\b(int|float|string|object|void)\s+" + escapedSymbol + @"\s*[=;]";
                if (Regex.IsMatch(line, varPattern, RegexOptions.IgnoreCase))
                {
                    GotoLine(lineNumber);
                    return true;
                }
            }

            return false;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2164-2168
        // Original: def _handle_exc_debug_mode(self, err_msg: str, e: Exception) -> bool:
        /// <summary>
        /// Handles exceptions in debug mode - shows error dialog and optionally rethrows in debug mode.
        /// </summary>
        /// <param name="errMsg">Error message to display</param>
        /// <param name="ex">Exception that occurred</param>
        /// <returns>True if error occurred (always returns true)</returns>
        private bool HandleExceptionDebugMode(string errMsg, Exception ex)
        {
            // Simplify exception message for display
            string exceptionMessage = ex?.Message ?? "Unknown error";
            if (ex?.InnerException != null)
            {
                exceptionMessage = $"{exceptionMessage}\n\nInner Exception: {ex.InnerException.Message}";
            }

            // Show error message box (matching Python QMessageBox.critical)
            var errorBox = MessageBoxManager.GetMessageBoxStandard(
                errMsg,
                exceptionMessage,
                ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error);

            // Show dialog asynchronously (non-blocking)
            errorBox.ShowAsync();

            // In debug mode, rethrow the exception (matching Python behavior)
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                throw ex;
            }
#endif

            return true;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2170-2194
        // Original: def _handle_user_ncs(self, data: bytes, resname: str) -> None:
        /// <summary>
        /// Handles user interaction when loading an NCS file - shows dialog to choose between decompiling or downloading.
        /// </summary>
        /// <param name="data">The NCS bytecode data</param>
        /// <param name="resname">The resource name (resref)</param>
        private void HandleUserNcs(byte[] data, string resname)
        {
            // Show dialog asking user to decompile or download
            var dialog = new DecompileOrDownloadDialog(_sourcerepoUrl);
            dialog.ShowDialog(this);

            string source = null;
            bool errorOccurred = false;

            if (dialog.Choice == DecompileOrDownloadDialog.UserChoice.Decompile)
            {
                // Matching PyKotor implementation: choice == QMessageBox.StandardButton.Yes
                if (_installation == null)
                {
                    errorOccurred = HandleExceptionDebugMode("Installation not set", new InvalidOperationException("Installation not set, cannot determine path"));
                    if (errorOccurred)
                    {
                        New();
                        return;
                    }
                }

                System.Console.WriteLine($"Decompiling NCS data: {data?.Length ?? 0} bytes");
                source = DecompileNcsDencs(data);
                if (string.IsNullOrEmpty(source))
                {
                    errorOccurred = HandleExceptionDebugMode("Decompilation/Download Failed", new InvalidOperationException("Decompilation failed: decompile_ncs returned None or empty string"));
                }
            }
            else if (dialog.Choice == DecompileOrDownloadDialog.UserChoice.Download)
            {
                // Matching PyKotor implementation: choice == QMessageBox.StandardButton.Ok
                System.Console.WriteLine($"Downloading NCS data: {data?.Length ?? 0} bytes");
                try
                {
                    source = DownloadAndLoadRemoteScript(resname);
                }
                catch (Exception ex)
                {
                    errorOccurred = HandleExceptionDebugMode("Decompilation/Download Failed", ex);
                }
            }
            else
            {
                // Matching PyKotor implementation: choice == Cancel
                // User cancelled - return without loading
                return;
            }

            if (errorOccurred)
            {
                New();
                return;
            }

            if (!string.IsNullOrEmpty(source) && _codeEdit != null)
            {
                _codeEdit.SetPlainText(source);
                _isDecompiled = true;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2109-2117
        // Original: def determine_script_path(self, resref: str) -> str:
        /// <summary>
        /// Determines the script path by showing a GitHub file selector dialog.
        /// </summary>
        /// <param name="resref">The resource reference (script name)</param>
        /// <returns>The selected script path from the repository</returns>
        private string DetermineScriptPath(string resref)
        {
            string scriptFilename = $"{resref.ToLowerInvariant()}.nss";
            var selectedFiles = new List<string> { scriptFilename };

            var dialog = new GitHubSelectorDialog(_owner, _repo, selectedFiles, this);
            dialog.ShowDialog(this);

            string selectedPath = dialog.SelectedPath;
            if (string.IsNullOrEmpty(selectedPath) || !selectedPath.Trim().Any())
            {
                throw new InvalidOperationException("No script selected.");
            }

            return selectedPath;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2248-2267
        // Original: def _download_and_load_remote_script(self, resref: str) -> str:
        /// <summary>
        /// Downloads a script from GitHub and loads it into the editor.
        /// </summary>
        /// <param name="resref">The resource reference (script name)</param>
        /// <returns>The downloaded script source code</returns>
        private string DownloadAndLoadRemoteScript(string resref)
        {
            // Determine script path using GitHub selector dialog
            string scriptPath = DetermineScriptPath(resref);

            // Get log directory for download location
            string logDir = LogDirectoryHelper.GetLogDirectory(null, _globalSettings.ExtractPath);
            string localPath = Path.Combine(logDir, Path.GetFileName(scriptPath));

            System.Console.WriteLine($"Local path: {localPath}");

            // Download the script from GitHub
            try
            {
                Task.Run(async () =>
                {
                    await GitHubDownloader.DownloadGitHubFileAsync(_owner, _repo, scriptPath, localPath);
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to download the script: '{localPath}' did not exist after download completed. Error: {ex.Message}", ex);
            }

            if (!File.Exists(localPath))
            {
                throw new InvalidOperationException($"Failed to download the script: '{localPath}' did not exist after download completed.");
            }

            // Try multiple encodings to read the file (matching Python implementation)
            try
            {
                return File.ReadAllText(localPath, System.Text.Encoding.UTF8);
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    return File.ReadAllText(localPath, System.Text.Encoding.GetEncoding("windows-1252"));
                }
                catch (DecoderFallbackException)
                {
                    return File.ReadAllText(localPath, System.Text.Encoding.GetEncoding("latin-1"));
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2298-2320
        // Original: def compile_current_script(self):
        /// <summary>
        /// Compiles the current script and saves it as an NCS file.
        /// Shows a success message when compilation and save are successful.
        /// </summary>
        public void CompileCurrentScript()
        {
            if (_codeEdit == null)
            {
                return;
            }

            try
            {
                // Set resource type to NCS for compilation
                _restype = ResourceType.NCS;

                // Determine filepath for saving
                string filepath = _filepath;
                if (string.IsNullOrEmpty(filepath))
                {
                    filepath = Path.Combine(Directory.GetCurrentDirectory(), "untitled_script.ncs");
                }
                else
                {
                    // Change extension to .ncs if needed
                    if (!filepath.EndsWith(".ncs", StringComparison.OrdinalIgnoreCase))
                    {
                        filepath = Path.ChangeExtension(filepath, ".ncs");
                    }
                }

                // Check if filepath is an ERF/RIM file (would need special handling)
                // TODO: STUB - For now, if installation is set and filepath is empty or BIF, use override path
                if (_installation != null && (string.IsNullOrEmpty(_filepath) || Path.GetFileName(filepath).EndsWith(".bif", StringComparison.OrdinalIgnoreCase)))
                {
                    string overridePath = Path.Combine(_installation.Path, "override");
                    if (!Directory.Exists(overridePath))
                    {
                        Directory.CreateDirectory(overridePath);
                    }
                    filepath = Path.Combine(overridePath, $"{_resname ?? "untitled_script"}.ncs");
                }

                // Save the file (this will trigger Build() which compiles the script)
                _filepath = filepath;
                Save();

                // Show success message
                string displayPath = filepath;
                var infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "Success",
                    $"Compiled script successfully saved to:\n{displayPath}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                infoBox.ShowAsync();
            }
            catch (Exception ex)
            {
                // Show error message
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Failed to compile",
                    ex.Message,
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:3347-3378
        // Original: def _analyze_code(self):
        /// <summary>
        /// Analyzes the current code for potential issues like missing semicolons and empty blocks.
        /// </summary>
        public async void AnalyzeCode()
        {
            if (_codeEdit == null)
            {
                return;
            }

            string text = _codeEdit.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                var infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "Analyze Code",
                    "No code to analyze.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                await infoBox.ShowAsync();
                return;
            }

            var issues = new List<string>();
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string stripped = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(stripped) || stripped.StartsWith("//"))
                {
                    continue;
                }

                int lineNumber = i + 1;

                // Check for missing semicolons after return/break/continue (heuristic)
                if (stripped.Contains("return") || stripped.Contains("break") || stripped.Contains("continue"))
                {
                    if (!stripped.EndsWith(";") && !stripped.EndsWith("{") && !stripped.EndsWith("}"))
                    {
                        issues.Add($"Line {lineNumber}: Possible missing semicolon");
                    }
                }

                // Check for empty if/while/for blocks
                if (stripped.Contains("if") || stripped.Contains("while") || stripped.Contains("for"))
                {
                    if (stripped.EndsWith("{") && i + 1 < lines.Length)
                    {
                        string nextLine = lines[i + 1].Trim();
                        if (nextLine == "}")
                        {
                            issues.Add($"Line {lineNumber}: Empty block detected");
                        }
                    }
                }
            }

            string message;
            if (issues.Count > 0)
            {
                message = "Code Analysis Results:\n\n" + string.Join("\n", issues.Take(20));
                if (issues.Count > 20)
                {
                    message += $"\n\n... and {issues.Count - 20} more issues";
                }
            }
            else
            {
                message = "No issues found!";
            }

            var resultBox = MessageBoxManager.GetMessageBoxStandard(
                "Code Analysis",
                message,
                ButtonEnum.Ok,
                issues.Count > 0 ? MsBox.Avalonia.Enums.Icon.Warning : MsBox.Avalonia.Enums.Icon.Info);
            await resultBox.ShowAsync();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2865-2943
        // Original: def _format_code(self):
        /// <summary>
        /// Formats the code with proper indentation and spacing (VS Code style).
        /// </summary>
        private async void FormatDocument()
        {
            if (_codeEdit == null)
            {
                return;
            }

            string text = _codeEdit.ToPlainText();
            if (string.IsNullOrWhiteSpace(text))
            {
                var infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "Format Document",
                    "No code to format.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                await infoBox.ShowAsync();
                return;
            }

            // Confirm before formatting
            var confirmBox = MessageBoxManager.GetMessageBoxStandard(
                "Format Code",
                "Format the entire document?",
                ButtonEnum.YesNo,
                MsBox.Avalonia.Enums.Icon.Question);
            var result = await confirmBox.ShowAsync();
            if (result != ButtonResult.Yes)
            {
                return;
            }

            // Split text into lines
            string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            List<string> formattedLines = new List<string>();
            int indentLevel = 0;

            // Save cursor position - calculate line and column from caret index
            int oldCaretIndex = _codeEdit.CaretIndex;
            int oldLine = 0;
            int oldColumn = 0;
            if (oldCaretIndex > 0 && !string.IsNullOrEmpty(text))
            {
                int currentPos = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    int lineLength = lines[i].Length;
                    int newlineLength = (i < lines.Length - 1) ? (text.Contains("\r\n") ? 2 : 1) : 0;

                    if (oldCaretIndex >= currentPos && oldCaretIndex <= currentPos + lineLength)
                    {
                        oldLine = i;
                        oldColumn = oldCaretIndex - currentPos;
                        break;
                    }
                    currentPos += lineLength + newlineLength;
                }
                if (oldCaretIndex >= currentPos)
                {
                    oldLine = lines.Length - 1;
                    oldColumn = lines[oldLine].Length;
                }
            }

            // Process each line
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string stripped = line.Trim();

                // Handle comments - preserve them but adjust indentation
                if (stripped.StartsWith("//"))
                {
                    if (!string.IsNullOrEmpty(stripped))
                    {
                        string commentIndent = TAB_AS_SPACE ? new string(' ', indentLevel * TAB_SIZE) : new string('\t', indentLevel);
                        formattedLines.Add(commentIndent + stripped);
                    }
                    else
                    {
                        formattedLines.Add("");
                    }
                    continue;
                }

                // Handle preprocessor directives - no indentation
                if (stripped.StartsWith("#"))
                {
                    formattedLines.Add(stripped);
                    continue;
                }

                // Handle empty lines - preserve them
                if (string.IsNullOrEmpty(stripped))
                {
                    formattedLines.Add("");
                    continue;
                }

                // Decrease indent for closing braces (before the line)
                if (stripped.StartsWith("}"))
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                }

                // Add line with proper indentation
                string lineIndent = TAB_AS_SPACE ? new string(' ', indentLevel * TAB_SIZE) : new string('\t', indentLevel);
                formattedLines.Add(lineIndent + stripped);

                // Increase indent for opening braces (after the line)
                // Count braces in the line
                int openBraces = stripped.Count(c => c == '{');
                int closeBraces = stripped.Count(c => c == '}');
                indentLevel += openBraces - closeBraces;
                indentLevel = Math.Max(0, indentLevel);
            }

            string formattedText = string.Join("\n", formattedLines);

            // Apply formatting
            _codeEdit.SetPlainText(formattedText);

            // Restore cursor position
            if (oldLine < formattedLines.Count)
            {
                // Calculate new character index from line and column
                int newCharIndex = 0;
                for (int i = 0; i < oldLine && i < formattedLines.Count; i++)
                {
                    newCharIndex += formattedLines[i].Length;
                    if (i < formattedLines.Count - 1)
                    {
                        newCharIndex += 1; // newline character
                    }
                }
                // Try to restore column position
                string lineText = formattedLines[oldLine];
                int newColumn = Math.Min(oldColumn, lineText.Length);
                newCharIndex += newColumn;

                // Set cursor position
                if (newCharIndex <= _codeEdit.Text.Length)
                {
                    _codeEdit.CaretIndex = newCharIndex;
                    _codeEdit.SelectionStart = newCharIndex;
                    _codeEdit.SelectionEnd = newCharIndex;
                }
            }

            LogToOutput("Code formatted successfully");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2349-2354
        // Original: def insert_selected_constant(self):
        /// <summary>
        /// Inserts the selected constant from the constant list into the code editor at the cursor position.
        /// Inserts the constant name and value in the format: "ConstantName = value"
        /// </summary>
        public void InsertSelectedConstant()
        {
            if (_constantList == null || _codeEdit == null)
            {
                return;
            }

            var selectedItem = _constantList.SelectedItem as ListBoxItem;
            if (selectedItem == null)
            {
                return;
            }

            var constant = selectedItem.Tag as ScriptConstant;
            if (constant == null)
            {
                return;
            }

            // Insert constant name and value: "ConstantName = value"
            string insert = $"{constant.Name} = {constant.Value}";
            InsertTextAtCursor(insert);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2374-2383
        // Original: def on_constant_search(self):
        /// <summary>
        /// Filters the constant list based on the search text.
        /// Hides items that don't match the search string (case-insensitive).
        /// </summary>
        /// <param name="searchText">The text to search for in constant names.</param>
        public void OnConstantSearch(string searchText)
        {
            if (_constantList == null)
            {
                return;
            }

            // If search text is empty or whitespace, show all items
            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var item in _constantList.Items.OfType<ListBoxItem>())
                {
                    item.IsVisible = true;
                }
                return;
            }

            // Filter items based on search text (case-insensitive)
            string lowerSearchText = searchText.ToLowerInvariant();
            foreach (var item in _constantList.Items.OfType<ListBoxItem>())
            {
                if (item?.Content is string itemText)
                {
                    item.IsVisible = itemText.ToLowerInvariant().Contains(lowerSearchText);
                }
                else
                {
                    item.IsVisible = false;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2707-2737
        // Original: def _update_status_bar(self):
        /// <summary>
        /// Updates the status bar with current cursor position and selection info.
        /// Displays line number, column number, selection info, and total lines.
        /// </summary>
        private void UpdateStatusBar()
        {
            // Check if status label is initialized (may be called during initialization)
            if (_statusLabel == null || _codeEdit == null)
            {
                return;
            }

            // Get current line and column from cursor position
            int lineNumber = GetCurrentLineNumber();
            int columnNumber = GetCurrentColumnNumber();

            // Calculate total lines
            int totalLines = GetTotalLineCount();

            // Get selection info
            string selectedText = _codeEdit.SelectedText ?? "";
            int selectedCount = selectedText.Length;

            // Format like VS Code: "Ln 1, Col 1" or "Ln 1, Col 1 (5 selected)"
            string statusText;
            if (selectedCount > 0)
            {
                // Count lines in selection
                int selectionLines = selectedText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (selectionLines > 1)
                {
                    statusText = $"Ln {lineNumber}, Col {columnNumber} ({selectedCount} chars in {selectionLines} lines) | {totalLines} lines";
                }
                else
                {
                    statusText = $"Ln {lineNumber}, Col {columnNumber} ({selectedCount} selected) | {totalLines} lines";
                }
            }
            else
            {
                statusText = $"Ln {lineNumber}, Col {columnNumber} | {totalLines} lines";
            }

            _statusLabel.Content = statusText;
        }

        // Helper method to get current column number from cursor position
        private int GetCurrentColumnNumber()
        {
            if (_codeEdit == null || string.IsNullOrEmpty(_codeEdit.Text))
            {
                return 1;
            }

            int cursorPosition = _codeEdit.SelectionStart;
            string text = _codeEdit.Text;

            // Find the start of the current line
            int lineStart = 0;
            for (int i = cursorPosition - 1; i >= 0; i--)
            {
                if (text[i] == '\n')
                {
                    lineStart = i + 1;
                    break;
                }
            }

            // Column number is 1-indexed (cursor position - line start + 1)
            return cursorPosition - lineStart + 1;
        }

        // Helper method to get total line count
        private int GetTotalLineCount()
        {
            if (_codeEdit == null || string.IsNullOrEmpty(_codeEdit.Text))
            {
                return 1;
            }

            string text = _codeEdit.Text;
            int lineCount = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lineCount++;
                }
            }
            return lineCount;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:518-550
        // Original: def _validate_bookmarks(self):
        /// <summary>
        /// Validates and updates bookmark line numbers when text changes.
        /// Removes bookmarks for lines that no longer exist.
        /// </summary>
        private void ValidateBookmarks()
        {
            if (_bookmarkTree == null || _codeEdit == null)
            {
                return;
            }

            int maxLines = GetTotalLineCount();
            var itemsToRemove = new List<TreeViewItem>();

            var itemsList = _bookmarkTree.ItemsSource as List<TreeViewItem> ?? new List<TreeViewItem>();
            foreach (var item in itemsList)
            {
                if (item?.Tag is BookmarkData bookmarkData)
                {
                    // Remove bookmarks for lines that no longer exist
                    if (bookmarkData.LineNumber > maxLines)
                    {
                        itemsToRemove.Add(item);
                    }
                }
            }

            // Remove invalid bookmarks
            foreach (var item in itemsToRemove)
            {
                itemsList.Remove(item);
            }

            if (itemsToRemove.Count > 0)
            {
                _bookmarkTree.ItemsSource = itemsList;
                SaveBookmarks();
                UpdateBookmarkVisualization();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:897-902
        // Original: def _on_game_selector_changed(self, index: int):
        /// <summary>
        /// Handles game change from dropdown selector.
        /// Updates game-specific data (functions and constants) based on selected game.
        /// </summary>
        /// <param name="index">The selected index (0 = K1, 1 = TSL)</param>
        private void OnGameSelectorChanged(int index)
        {
            // Update game flag (0 = K1, 1 = TSL)
            _isTsl = index == 1;

            // Update game-specific data
            UpdateGameSpecificData();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1351-1398
        // Original: def _setup_file_explorer(self):
        /// <summary>
        /// Set up the file explorer with filtering and navigation.
        /// Creates a file system model and configures the file explorer TreeView.
        /// </summary>
        private void SetupFileExplorer()
        {
            // Create file system model
            _fileSystemModel = new NSSEditorFileSystemModel();

            // Create file explorer TreeView if not already created
            if (_fileExplorerView == null)
            {
                _fileExplorerView = new TreeView();
            }

            // Create address bar if not already created
            if (_fileExplorerAddressBar == null)
            {
                _fileExplorerAddressBar = new TextBox();
                _fileExplorerAddressBar.Watermark = "Address Bar";
            }

            // Create file search edit if not already created
            if (_fileSearchEdit == null)
            {
                _fileSearchEdit = new TextBox();
                _fileSearchEdit.Watermark = "Search files...";
            }

            // Create refresh button if not already created
            if (_refreshFileExplorerButton == null)
            {
                _refreshFileExplorerButton = new Button();
                _refreshFileExplorerButton.Content = "Refresh";
            }

            // Create file explorer dock panel container
            // Matching PyKotor implementation: self.ui.fileExplorerDock
            if (_fileExplorerDock == null)
            {
                // Create a vertical stack panel to hold all file explorer components
                var explorerPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Vertical,
                    Name = "fileExplorerDock"
                };

                // Add address bar
                explorerPanel.Children.Add(_fileExplorerAddressBar);

                // Add search and refresh controls in a horizontal panel
                var searchPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal
                };
                searchPanel.Children.Add(_fileSearchEdit);
                searchPanel.Children.Add(_refreshFileExplorerButton);
                explorerPanel.Children.Add(searchPanel);

                // Add file explorer TreeView (with scroll viewer for large directories)
                var scrollViewer = new ScrollViewer
                {
                    Content = _fileExplorerView,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                };
                explorerPanel.Children.Add(scrollViewer);

                // Set minimum width for the panel
                explorerPanel.MinWidth = 200;

                _fileExplorerDock = explorerPanel;
            }

            // Determine root path - start with current file's directory or home directory
            string rootPath;
            if (!string.IsNullOrEmpty(_filepath) && File.Exists(_filepath))
            {
                rootPath = Path.GetDirectoryName(_filepath);
            }
            else
            {
                rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            // Set root path in model
            _fileSystemModel.SetRootPath(rootPath);

            // Set address bar text
            _fileExplorerAddressBar.Text = rootPath;

            // Connect address bar return pressed
            _fileExplorerAddressBar.KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    OnAddressBarChanged();
                }
            };

            // Connect file search text changed
            _fileSearchEdit.TextChanged += (s, e) => FilterFileExplorer();

            // Connect refresh button click
            _refreshFileExplorerButton.Click += (s, e) => RefreshFileExplorer();

            // Connect file explorer double-click
            _fileExplorerView.DoubleTapped += (s, e) => OpenFileFromExplorer();

            // Set current file if available
            if (!string.IsNullOrEmpty(_filepath) && File.Exists(_filepath))
            {
                SelectAndScrollToCurrentFile();
            }

            // Integrate file explorer dock into main UI layout
            IntegrateFileExplorerDock();
        }

        /// <summary>
        /// Select and scroll to the current file in the file explorer TreeView.
        /// Expands parent directories as needed and makes the file visible.
        /// </summary>
        private void SelectAndScrollToCurrentFile()
        {
            if (_fileExplorerView == null || _fileSystemModel == null || string.IsNullOrEmpty(_filepath))
            {
                return;
            }

            try
            {
                // Find the TreeViewItem corresponding to the current file
                var fileItem = FindTreeViewItemByPath(_fileExplorerView.ItemsSource, _filepath);
                if (fileItem != null)
                {
                    // Expand all parent directories to make the file visible
                    ExpandParentDirectories(fileItem);

                    // Select the file item
                    _fileExplorerView.SelectedItem = fileItem;

                    // Scroll to make the item visible
                    ScrollToTreeViewItem(fileItem);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the editor initialization
                System.Console.WriteLine($"Error selecting current file in explorer: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively searches for a TreeViewItem with the specified file path.
        /// </summary>
        /// <param name="itemsSource">The items source to search in.</param>
        /// <param name="targetPath">The full file path to find.</param>
        /// <returns>The TreeViewItem if found, null otherwise.</returns>
        private TreeViewItem FindTreeViewItemByPath(IEnumerable<object> itemsSource, string targetPath)
        {
            if (itemsSource == null || string.IsNullOrEmpty(targetPath))
            {
                return null;
            }

            foreach (var item in itemsSource)
            {
                if (!(item is TreeViewItem treeItem))
                {
                    continue;
                }

                // Check if this item matches the target path
                if (treeItem.Tag is string itemPath && string.Equals(itemPath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return treeItem;
                }

                // Recursively search in child items
                if (treeItem.ItemsSource != null)
                {
                    var foundInChildren = FindTreeViewItemByPath(treeItem.ItemsSource, targetPath);
                    if (foundInChildren != null)
                    {
                        return foundInChildren;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Expands all parent directories of the specified TreeViewItem to make it visible.
        /// </summary>
        /// <param name="item">The TreeViewItem to expand parents for.</param>
        private void ExpandParentDirectories(TreeViewItem item)
        {
            if (item == null)
            {
                return;
            }

            // Expand this item if it has children and is not already expanded
            if (item.ItemsSource != null && !item.IsExpanded)
            {
                item.IsExpanded = true;
            }

            // Find the parent item and expand it recursively
            var parent = FindParentTreeViewItem(_fileExplorerView.ItemsSource, item);
            if (parent != null)
            {
                ExpandParentDirectories(parent);
            }
        }

        /// <summary>
        /// Finds the parent TreeViewItem of the specified child item.
        /// </summary>
        /// <param name="itemsSource">The items source to search in.</param>
        /// <param name="childItem">The child item to find the parent for.</param>
        /// <returns>The parent TreeViewItem if found, null otherwise.</returns>
        private TreeViewItem FindParentTreeViewItem(IEnumerable<object> itemsSource, TreeViewItem childItem)
        {
            if (itemsSource == null || childItem == null)
            {
                return null;
            }

            foreach (var item in itemsSource)
            {
                if (!(item is TreeViewItem treeItem))
                {
                    continue;
                }

                // Check if this item contains the child
                if (treeItem.ItemsSource != null)
                {
                    foreach (var child in treeItem.ItemsSource)
                    {
                        if (child == childItem)
                        {
                            return treeItem;
                        }
                    }

                    // Recursively search in grandchildren
                    var foundParent = FindParentTreeViewItem(treeItem.ItemsSource, childItem);
                    if (foundParent != null)
                    {
                        return foundParent;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Scrolls the TreeView to make the specified item visible.
        /// </summary>
        /// <param name="item">The TreeViewItem to scroll to.</param>
        private void ScrollToTreeViewItem(TreeViewItem item)
        {
            if (item == null || _fileExplorerView == null)
            {
                return;
            }

            try
            {
                // Find the ScrollViewer that contains the TreeView
                ScrollViewer scrollViewer = FindParentScrollViewer(_fileExplorerView);
                if (scrollViewer != null)
                {
                    // Use BringIntoView to scroll the item into view
                    item.BringIntoView();

                    // Also try to scroll the TreeView itself if it's in a ScrollViewer
                    _fileExplorerView.BringIntoView(item);
                }
                else
                {
                    // Fallback: try to bring the item into view directly
                    item.BringIntoView();
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Console.WriteLine($"Error scrolling to tree view item: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the parent ScrollViewer of the specified control.
        /// </summary>
        /// <param name="control">The control to find the ScrollViewer for.</param>
        /// <returns>The ScrollViewer if found, null otherwise.</returns>
        private ScrollViewer FindParentScrollViewer(Control control)
        {
            if (control == null)
            {
                return null;
            }

            // Check if the control itself is a ScrollViewer
            if (control is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            // Recursively check parent controls
            Control parent = control.Parent as Control;
            while (parent != null)
            {
                if (parent is ScrollViewer parentScrollViewer)
                {
                    return parentScrollViewer;
                }
                parent = parent.Parent as Control;
            }

            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1399-1410
        // Original: def _on_address_bar_changed(self):
        /// <summary>
        /// Handle address bar path change.
        /// Updates the file explorer root path when user enters a new path.
        /// </summary>
        private void OnAddressBarChanged()
        {
            if (_fileExplorerAddressBar == null || _fileSystemModel == null)
            {
                return;
            }

            string pathText = _fileExplorerAddressBar.Text ?? "";
            if (string.IsNullOrEmpty(pathText))
            {
                return;
            }

            if (Directory.Exists(pathText))
            {
                _fileSystemModel.SetRootPath(pathText);
                // Update TreeView to show new path
                UpdateFileExplorerTreeView();
            }
            else
            {
                // Invalid path - reset to current root
                string currentRoot = _fileSystemModel.RootPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _fileExplorerAddressBar.Text = currentRoot;

                // Show warning (matching Python QMessageBox.warning)
                System.Console.WriteLine($"Invalid Path: The path '{pathText}' does not exist or is not a directory.");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1412-1453
        // Original: def _filter_file_explorer(self):
        /// <summary>
        /// Filter files in the explorer based on search text.
        /// Hides files and directories that don't match the search text.
        /// </summary>
        private void FilterFileExplorer()
        {
            if (_fileSearchEdit == null || _fileSystemModel == null || _fileExplorerView == null)
            {
                return;
            }

            string searchText = (_fileSearchEdit.Text ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(searchText))
            {
                // Show all files when search is empty
                _fileSystemModel.ClearFilter();
                return;
            }

            // Filter files matching search text
            _fileSystemModel.SetFilter(searchText);

            // Update TreeView to reflect filter
            UpdateFileExplorerTreeView();
        }

        /// <summary>
        /// Updates the file explorer TreeView with current file system data.
        /// Builds a tree structure from the file system and binds it to the TreeView.
        /// </summary>
        private void UpdateFileExplorerTreeView()
        {
            if (_fileExplorerView == null || _fileSystemModel == null)
            {
                return;
            }

            string rootPath = _fileSystemModel.RootPath;
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                _fileExplorerView.ItemsSource = new List<TreeViewItem>();
                return;
            }

            string filter = _fileSystemModel.RootPath; // Get filter from model (if exposed)
            // Note: FileSystemModel.SetFilter stores filter but doesn't expose it
            // TODO: STUB - For now, we'll get filter from _fileSearchEdit if available
            string searchFilter = (_fileSearchEdit?.Text ?? "").ToLowerInvariant();

            var rootItems = new List<TreeViewItem>();
            try
            {
                // Build tree structure from directory
                BuildFileSystemTree(rootPath, null, rootItems, searchFilter);
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Console.WriteLine($"Error updating file explorer: {ex.Message}");
                rootItems = new List<TreeViewItem>();
            }

            _fileExplorerView.ItemsSource = rootItems;
        }

        /// <summary>
        /// Recursively builds a tree structure from the file system.
        /// </summary>
        /// <param name="directoryPath">The directory path to build from.</param>
        /// <param name="parentItem">The parent TreeViewItem (null for root).</param>
        /// <param name="itemsList">The list to add items to.</param>
        /// <param name="filter">Optional filter string to match file/directory names.</param>
        private void BuildFileSystemTree(string directoryPath, TreeViewItem parentItem, List<TreeViewItem> itemsList, string filter = "")
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            try
            {
                // Get directories and files
                var directories = Directory.GetDirectories(directoryPath);
                var files = Directory.GetFiles(directoryPath);

                // Apply filter if provided
                bool hasFilter = !string.IsNullOrEmpty(filter);
                if (hasFilter)
                {
                    directories = directories.Where(d => Path.GetFileName(d).ToLowerInvariant().Contains(filter)).ToArray();
                    files = files.Where(f => Path.GetFileName(f).ToLowerInvariant().Contains(filter)).ToArray();
                }

                // Add directories first
                foreach (var dir in directories.OrderBy(d => Path.GetFileName(d)))
                {
                    string dirName = Path.GetFileName(dir);
                    var dirItem = new TreeViewItem
                    {
                        Header = dirName,
                        Tag = dir, // Store full path in Tag
                        IsExpanded = false
                    };

                    // Recursively build children (only if not filtered, to avoid performance issues)
                    if (!hasFilter)
                    {
                        var childItems = new List<TreeViewItem>();
                        BuildFileSystemTree(dir, dirItem, childItems, filter);
                        if (childItems.Count > 0)
                        {
                            dirItem.ItemsSource = childItems;
                        }
                    }

                    if (parentItem == null)
                    {
                        itemsList.Add(dirItem);
                    }
                    else
                    {
                        if (parentItem.ItemsSource == null)
                        {
                            parentItem.ItemsSource = new List<TreeViewItem>();
                        }
                        ((List<TreeViewItem>)parentItem.ItemsSource).Add(dirItem);
                    }
                }

                // Add files
                foreach (var file in files.OrderBy(f => Path.GetFileName(f)))
                {
                    string fileName = Path.GetFileName(file);
                    var fileItem = new TreeViewItem
                    {
                        Header = fileName,
                        Tag = file, // Store full path in Tag
                        IsExpanded = false
                    };

                    if (parentItem == null)
                    {
                        itemsList.Add(fileItem);
                    }
                    else
                    {
                        if (parentItem.ItemsSource == null)
                        {
                            parentItem.ItemsSource = new List<TreeViewItem>();
                        }
                        ((List<TreeViewItem>)parentItem.ItemsSource).Add(fileItem);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have access to
            }
            catch (Exception ex)
            {
                // Log but continue
                System.Console.WriteLine($"Error building file system tree for {directoryPath}: {ex.Message}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1455-1461
        // Original: def _refresh_file_explorer(self):
        /// <summary>
        /// Refresh the file explorer view.
        /// Reloads the file system model to show updated file structure.
        /// </summary>
        private void RefreshFileExplorer()
        {
            if (_fileSystemModel == null || _fileExplorerAddressBar == null)
            {
                return;
            }

            string currentRoot = _fileSystemModel.RootPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _fileSystemModel.SetRootPath(""); // Reset model
            _fileSystemModel.SetRootPath(currentRoot); // Reload
            _fileExplorerAddressBar.Text = currentRoot;

            // Refresh the TreeView
            UpdateFileExplorerTreeView();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1492-1500
        // Original: def _open_file_from_explorer(self, index: QModelIndex):
        /// <summary>
        /// Open a file from the file explorer when double-clicked.
        /// Opens the selected file in an appropriate editor.
        /// </summary>
        private void OpenFileFromExplorer()
        {
            if (_fileExplorerView == null || _fileSystemModel == null)
            {
                return;
            }

            var selectedItem = _fileExplorerView.SelectedItem;
            if (selectedItem == null)
            {
                return;
            }

            // Get file path from selected item - TreeViewItem.Tag contains the full path as string
            string fullPath = (selectedItem as TreeViewItem)?.Tag as string;
            if (string.IsNullOrEmpty(fullPath))
            {
                return;
            }

            // Check if it's a directory
            if (Directory.Exists(fullPath))
            {
                // TODO: Navigate to the directory (change current directory in file explorer)
                // For now, just update the root path to navigate to the selected directory
                _fileSystemModel.SetRootPath(fullPath);
                _fileExplorerAddressBar.Text = fullPath;
                UpdateFileExplorerTreeView();
                return;
            }

            // It's a file - determine resource type from extension
            string extension = Path.GetExtension(fullPath).TrimStart('.').ToLowerInvariant();
            ResourceType resourceType = ResourceType.FromExtension(extension);

            if (resourceType == null || resourceType.IsInvalid)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Unknown or unsupported file extension: .{extension}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            // Check if this is a script file (NSS/NCS) that this editor can handle
            bool isScriptFile = resourceType == ResourceType.NSS || resourceType == ResourceType.NCS;

            if (isScriptFile)
            {
                // Load the script file into this editor instance
                try
                {
                    byte[] fileData = File.ReadAllBytes(fullPath);
                    string fileName = Path.GetFileNameWithoutExtension(fullPath);
                    Load(fullPath, fileName, resourceType, fileData);
                }
                catch (Exception ex)
                {
                    var errorBox = MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        $"Failed to load file:\n{ex.Message}",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    errorBox.ShowAsync();
                }
            }
            else
            {
                // For other file types, delegate to the main window to open appropriate editor
                // Create a FileResource for the file
                var fileResource = new FileResource(fullPath, Path.GetFileNameWithoutExtension(fullPath), resourceType);

                // Use WindowUtils to open the appropriate editor
                // Pass this window as the parent
                WindowUtils.OpenResourceEditor(fileResource, _installation, this);
            }
        }

        // Public properties for testing
        // Matching PyKotor test at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py:928-940
        // Original: assert hasattr(editor, 'file_system_model')
        /// <summary>
        /// Gets the file system model used by the file explorer.
        /// Exposed for testing purposes to match Python test behavior.
        /// </summary>
        public NSSEditorFileSystemModel FileSystemModel => _fileSystemModel;

        /// <summary>
        /// Gets the file explorer TreeView.
        /// Exposed for testing purposes to match Python test behavior.
        /// </summary>
        public TreeView FileExplorerView => _fileExplorerView;

        /// <summary>
        /// Gets the error lines set.
        /// Exposed for testing purposes to match Python test behavior.
        /// Matching PyKotor implementation: editor._error_lines
        /// </summary>
        public HashSet<int> ErrorLines => _errorLines;

        /// <summary>
        /// Gets the warning lines set.
        /// Exposed for testing purposes to match Python test behavior.
        /// Matching PyKotor implementation: editor._warning_lines
        /// </summary>
        public HashSet<int> WarningLines => _warningLines;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2739-2741
        // Original: def _toggle_file_explorer(self):
        /// <summary>
        /// Toggle file explorer dock visibility.
        /// </summary>
        private void ToggleFileExplorer()
        {
            if (_fileExplorerDock == null) return;
            _fileExplorerDock.IsVisible = !_fileExplorerDock.IsVisible;
        }

        /// <summary>
        /// Integrate the file explorer dock into the main UI layout.
        /// </summary>
        private void IntegrateFileExplorerDock()
        {
            if (_fileExplorerDock == null) return;
            _fileExplorerDock.IsVisible = false;
            if (Content is DockPanel mainDockPanel)
            {
                if (!mainDockPanel.Children.Contains(_fileExplorerDock))
                {
                    mainDockPanel.Children.Add(_fileExplorerDock);
                    DockPanel.SetDock(_fileExplorerDock, Dock.Left);
                }
            }
            else
            {
                var newDockPanel = new DockPanel();
                newDockPanel.Children.Add(_fileExplorerDock);
                DockPanel.SetDock(_fileExplorerDock, Dock.Left);
                if (Content != null && Content is Control existingContent)
                    newDockPanel.Children.Add(existingContent);
                else if (_codeEdit != null)
                    newDockPanel.Children.Add(_codeEdit);
                Content = newDockPanel;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2743-2747
        // Original: def _toggle_terminal_panel(self):
        /// <summary>
        /// Toggle terminal/bookmarks/snippets dock visibility.
        /// </summary>
        private void ToggleTerminalPanel()
        {
            if (_bookmarksDock == null || _snippetsDock == null) return;
            bool isVisible = _bookmarksDock.IsVisible;
            _bookmarksDock.IsVisible = !isVisible;
            _snippetsDock.IsVisible = !isVisible;
        }

        /// <summary>
        /// Integrate the bookmarks dock into the main UI layout.
        /// </summary>
        private void IntegrateBookmarksDock()
        {
            if (_bookmarksDock == null) return;
            _bookmarksDock.IsVisible = false;
            if (Content is DockPanel mainDockPanel)
            {
                if (!mainDockPanel.Children.Contains(_bookmarksDock))
                {
                    mainDockPanel.Children.Add(_bookmarksDock);
                    DockPanel.SetDock(_bookmarksDock, Dock.Right);
                }
            }
            else
            {
                var newDockPanel = new DockPanel();
                newDockPanel.Children.Add(_bookmarksDock);
                DockPanel.SetDock(_bookmarksDock, Dock.Right);
                if (Content != null && Content is Control existingContent)
                    newDockPanel.Children.Add(existingContent);
                else if (_codeEdit != null)
                    newDockPanel.Children.Add(_codeEdit);
                Content = newDockPanel;
            }
        }

        /// <summary>
        /// Integrate the snippets dock into the main UI layout.
        /// </summary>
        private void IntegrateSnippetsDock()
        {
            if (_snippetsDock == null) return;
            _snippetsDock.IsVisible = false;
            if (Content is DockPanel mainDockPanel)
            {
                if (!mainDockPanel.Children.Contains(_snippetsDock))
                {
                    mainDockPanel.Children.Add(_snippetsDock);
                    DockPanel.SetDock(_snippetsDock, Dock.Right);
                }
            }
            else
            {
                var newDockPanel = new DockPanel();
                newDockPanel.Children.Add(_snippetsDock);
                DockPanel.SetDock(_snippetsDock, Dock.Right);
                if (Content != null && Content is Control existingContent)
                    newDockPanel.Children.Add(existingContent);
                else if (_codeEdit != null)
                    newDockPanel.Children.Add(_codeEdit);
                Content = newDockPanel;
            }
        }

        // Helper class to store bookmark data
        // Internal class for bookmark data (accessible to tests)
        // Public class for bookmark data, accessible from tests
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        public class BookmarkData
        {
            public int LineNumber { get; set; }
            public string Description { get; set; }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:1351-1356
        // Original: self.file_system_model = QFileSystemModel()
        /// <summary>
        /// Simple file system model for Avalonia TreeView.
        /// Provides file system browsing functionality similar to Qt's QFileSystemModel.
        /// </summary>
        public class NSSEditorFileSystemModel
        {
            private string _rootPath;
            private string _filter;

            /// <summary>
            /// Gets or sets the root path of the file system model.
            /// </summary>
            public string RootPath
            {
                get { return _rootPath; }
            }

            /// <summary>
            /// Sets the root path for the file system model.
            /// </summary>
            /// <param name="path">The root path to set. Use empty string to reset.</param>
            public void SetRootPath(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    _rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
                else if (Directory.Exists(path))
                {
                    _rootPath = path;
                }
                else
                {
                    _rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
            }

            /// <summary>
            /// Sets a filter string for filtering files and directories.
            /// </summary>
            /// <param name="filter">The filter text to apply (case-insensitive).</param>
            public void SetFilter(string filter)
            {
                _filter = filter ?? "";
            }

            /// <summary>
            /// Clears the current filter, showing all files.
            /// </summary>
            public void ClearFilter()
            {
                _filter = "";
            }
        }
    }
}

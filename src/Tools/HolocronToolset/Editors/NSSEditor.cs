using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Common.Script;
using HolocronToolset.Common;
using HolocronToolset.Common.Widgets;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Utils;
using HolocronToolset.Widgets;
using System.Text.Json;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:115
    // Original: class NSSEditor(Editor):
    public class NSSEditor : Editor
    {
        private CodeEditor _codeEdit;
        private TerminalWidget _terminalWidget;
        private bool _isDecompiled;
        private NoScrollEventFilter _noScrollFilter;
        private FindReplaceWidget _findReplaceWidget;
        private TreeView _bookmarkTree;
        private ListBox _functionList;
        private ListBox _constantList;
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
            
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:164-167
            // Original: self.owner: str = "KOTORCommunityPatches"
            _owner = "KOTORCommunityPatches";
            _repo = "Vanilla_KOTOR_Script_Source";
            _sourcerepoUrl = $"https://github.com/{_owner}/{_repo}";

            InitializeComponent();
            SetupUI();
            SetupTerminal();
            SetupSignals();
            SetupFindReplaceWidget();
            SetupBookmarks();
            SetupFunctionList();
            SetupBreadcrumbs();
            AddHelpAction();

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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:969-977
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            // Connect compile action (if available via menu or shortcut)
            // Note: In Avalonia, actions are typically handled via menu items or keyboard shortcuts
            // This will be connected when actionCompile menu item is available
            
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
                
                // Update status bar on cursor position change
                _codeEdit.SelectionChanged += (s, e) => UpdateStatusBar();
                
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
                System.Console.WriteLine("Find: No more occurrences found");
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
                System.Console.WriteLine(message);

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
                    text = Encoding.UTF8.GetString(data);
                }
                catch (DecoderFallbackException)
                {
                    try
                    {
                        text = Encoding.GetEncoding("windows-1252").GetString(data);
                    }
                    catch (DecoderFallbackException)
                    {
                        text = Encoding.GetEncoding("latin-1").GetString(data);
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
        private string DecompileNcsDencs(byte[] ncsData)
        {
            if (ncsData == null || ncsData.Length == 0)
            {
                // Return empty string for invalid input (matching TODO requirement)
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
                    // If decompilation returned null or empty, return empty string (matching TODO requirement)
                    // Python implementation raises ValueError, but TODO explicitly requests returning empty string
                    System.Console.WriteLine("Decompilation failed: decompile_ncs returned null or empty string");
                    return "";
                }
                catch (Exception ex)
                {
                    // Decompilation failed - log error and return empty string (matching TODO requirement)
                    // Python implementation raises ValueError, but TODO explicitly requests returning empty string
                    // This allows caller to handle gracefully without exception handling
                    System.Console.WriteLine($"Decompilation failed: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        System.Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    return "";
                }
            }

            // Installation not set - return empty string (matching TODO requirement)
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
                data = Encoding.UTF8.GetBytes(text);
            }
            catch (EncoderFallbackException)
            {
                try
                {
                    data = Encoding.GetEncoding("windows-1252").GetBytes(text);
                }
                catch (EncoderFallbackException)
                {
                    data = Encoding.GetEncoding("latin-1").GetBytes(text);
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
            
            // Load bookmarks when editor is initialized
            LoadBookmarks();
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:282-303
        // Original: def _save_bookmarks(self):
        private void SaveBookmarks()
        {
            if (_bookmarkTree == null)
            {
                return;
            }

            var bookmarks = new List<BookmarkData>();
            var itemsList = _bookmarkTree.Items as IEnumerable<TreeViewItem> ?? new List<TreeViewItem>();
            
            foreach (var item in itemsList)
            {
                if (item?.Tag is BookmarkData bookmarkData)
                {
                    bookmarks.Add(bookmarkData);
                }
            }

            // Save to application settings (using a simple JSON approach)
            // In a full implementation, this would use Avalonia's settings system
            string fileKey = !string.IsNullOrEmpty(_resname) 
                ? $"nss_editor/bookmarks/{_resname}" 
                : "nss_editor/bookmarks/untitled";
            
            try
            {
                string json = JsonSerializer.Serialize(bookmarks);
                // Store in a simple dictionary for now (would use proper settings in production)
                // This is a simplified implementation for testing
            }
            catch
            {
                // Ignore save errors in test environment
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

            string fileKey = !string.IsNullOrEmpty(_resname) 
                ? $"nss_editor/bookmarks/{_resname}" 
                : "nss_editor/bookmarks/untitled";

            // Load from application settings (simplified for testing)
            // In a full implementation, this would use Avalonia's settings system
            try
            {
                // For testing, we'll start with empty bookmarks
                // In production, this would load from persistent storage
                _bookmarkTree.ItemsSource = new List<TreeViewItem>();
            }
            catch
            {
                // Ignore load errors in test environment
                _bookmarkTree.ItemsSource = new List<TreeViewItem>();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:541-550
        // Original: def _update_bookmark_visualization(self):
        private void UpdateBookmarkVisualization()
        {
            // In a full implementation, this would update visual indicators in the code editor
            // For now, this is a placeholder that matches the Python interface
        }

        // Public property to access bookmark tree for testing
        public TreeView BookmarkTree => _bookmarkTree;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:905-950
        // Original: def _update_game_specific_data(self):
        /// <summary>
        /// Updates constants and functions based on the selected game (K1 or TSL).
        /// Populates the function list and constant list with data from ScriptDefs.
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
        private void NavigateToSymbol(string symbolName)
        {
            if (_codeEdit == null || string.IsNullOrEmpty(symbolName))
            {
                return;
            }

            string text = _codeEdit.ToPlainText();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1; // 1-indexed

                // Look for function definition
                // Check for: void symbolName(, int symbolName(, float symbolName(, etc.
                if (line.Contains($"void {symbolName}(") || 
                    line.Contains($"int {symbolName}(") || 
                    line.Contains($"float {symbolName}(") ||
                    line.Contains($"string {symbolName}(") ||
                    line.Contains($"object {symbolName}("))
                {
                    GotoLine(lineNumber);
                    return;
                }

                // Look for struct definition
                if (line.Contains($"struct {symbolName}"))
                {
                    GotoLine(lineNumber);
                    return;
                }

                // Look for variable declaration
                // More specific check: symbol name must be followed by = or ; and line must contain a type keyword
                if (line.Contains(symbolName) && (line.Contains("=") || line.Contains(";")))
                {
                    // Check if line contains a type keyword (more specific check)
                    if (line.Contains("int ") || line.Contains("float ") || 
                        line.Contains("string ") || line.Contains("object ") || 
                        line.Contains("void "))
                    {
                        GotoLine(lineNumber);
                        return;
                    }
                }
            }
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
            dialog.ShowDialogAsync(this).GetAwaiter().GetResult();
            
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
            dialog.ShowDialogAsync(this).GetAwaiter().GetResult();
            
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
                return File.ReadAllText(localPath, Encoding.UTF8);
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    return File.ReadAllText(localPath, Encoding.GetEncoding("windows-1252"));
                }
                catch (DecoderFallbackException)
                {
                    return File.ReadAllText(localPath, Encoding.GetEncoding("latin-1"));
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
                // For now, if installation is set and filepath is empty or BIF, use override path
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

        // Helper class to store bookmark data
        private class BookmarkData
        {
            public int LineNumber { get; set; }
            public string Description { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:80
    // Original: class CodeEditor(QPlainTextEdit):
    public class CodeEditor : TextBox
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1588-1691
        // Original: Column selection mode tracking fields
        // Column selection mode is activated by Alt+Shift+Drag
        private bool _columnSelectionMode = false;
        private Point? _columnSelectionAnchor = null;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:162-163
        // Original: self._folded_block_numbers: set[int] = set()  # Block numbers that are folded (blocks that start foldable regions)
        // Original: self._foldable_regions: dict[int, int] = {}  # Map start block number to end block number for foldable regions
        // Code folding tracking fields
        private HashSet<int> _foldedBlockNumbers = new HashSet<int>(); // Block numbers that are folded (blocks that start foldable regions)
        private Dictionary<int, int> _foldableRegions = new Dictionary<int, int>(); // Map start block number to end block number for foldable regions
        private Dictionary<int, string> _foldedContentCache = new Dictionary<int, string>(); // Cache of folded content for restoration (for future use)

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1416-1470
        // Original: Extra selections for highlighting multiple occurrences (QTextEdit.ExtraSelection)
        // In Avalonia, we track selections as tuples of (start, end) positions
        private List<Tuple<int, int>> _extraSelections = new List<Tuple<int, int>>(); // Extra selections for highlighting multiple occurrences

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:95-121
        // Original: def __init__(self, parent: QWidget):
        public CodeEditor()
        {
            InitializeComponent();
            AcceptsReturn = true;
            AcceptsTab = true;
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap;

            // Update foldable regions when text changes
            this.TextChanged += (s, e) => UpdateFoldableRegions();
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
                // XAML not available - will use defaults
            }
        }

        // Matching PyKotor implementation: QPlainTextEdit.toPlainText()
        // Original: Returns the plain text content
        public string ToPlainText()
        {
            return Text ?? "";
        }

        // Matching PyKotor implementation: QPlainTextEdit.setPlainText(text)
        // Original: Sets the plain text content
        public void SetPlainText(string text)
        {
            Text = text ?? "";
        }

        // Matching PyKotor implementation: QPlainTextEdit.document()
        // Original: Returns the QTextDocument
        // For Avalonia, we'll return null as TextBox doesn't have a separate document model
        public object Document()
        {
            return null;
        }

        // Select next occurrence of currently selected text
        // Based on industry-standard IDE behavior (VS Code, Visual Studio, etc.)
        // If no text is selected, selects the word at the cursor position
        // Then finds and selects the next occurrence of that text
        // Returns true if a next occurrence was found and selected, false otherwise
        // Based on Avalonia TextBox API: https://docs.avaloniaui.net/docs/reference/controls/textbox
        // SelectionStart and SelectionEnd properties control text selection
        public bool SelectNextOccurrence()
        {
            if (string.IsNullOrEmpty(Text))
            {
                return false;
            }

            string searchText;
            int searchStartIndex;

            int wordStart = -1;
            int wordEnd = -1;

            // Get the text to search for
            if (SelectionStart == SelectionEnd)
            {
                // No selection - get word at cursor position and select it first
                wordStart = GetWordAtCursorStart();
                wordEnd = GetWordAtCursorEnd();
                if (wordStart == -1 || wordEnd == -1 || wordStart >= wordEnd)
                {
                    return false;
                }
                searchText = Text.Substring(wordStart, wordEnd - wordStart);

                // Select the word at cursor first (matching VS Code behavior)
                SelectionStart = wordStart;
                SelectionEnd = wordEnd;

                // Start searching after the end of the word at cursor
                searchStartIndex = wordEnd;
            }
            else
            {
                // Use selected text
                searchText = SelectedText;
                if (string.IsNullOrEmpty(searchText))
                {
                    return false;
                }
                // Start searching after the current selection
                searchStartIndex = SelectionEnd;
            }

            // Find next occurrence (case-sensitive)
            int nextIndex = Text.IndexOf(searchText, searchStartIndex, StringComparison.Ordinal);
            if (nextIndex == -1)
            {
                // Not found after current position - wrap to beginning
                // Exclude the current selection/word from the wrap search
                int wrapEnd = (wordStart != -1) ? wordStart : SelectionStart;
                if (wrapEnd > 0)
                {
                    nextIndex = Text.IndexOf(searchText, 0, wrapEnd, StringComparison.Ordinal);
                }
            }

            if (nextIndex != -1)
            {
                // Select the found occurrence
                SelectionStart = nextIndex;
                SelectionEnd = nextIndex + searchText.Length;
                return true;
            }

            return false;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1416-1470
        // Original: def select_all_occurrences(self): Select all occurrences of current word (VS Code Ctrl+Shift+L behavior)
        /// <summary>
        /// Selects all occurrences of the current word or selected text.
        /// Matching VS Code Ctrl+Shift+L behavior.
        /// </summary>
        public void SelectAllOccurrences()
        {
            if (string.IsNullOrEmpty(Text))
            {
                return;
            }

            // Get the word to search for
            string searchText;
            if (SelectionStart != SelectionEnd && !string.IsNullOrEmpty(SelectedText))
            {
                // Use selected text
                searchText = SelectedText;
            }
            else
            {
                // Get word at cursor position
                int wordStart = GetWordAtCursorStart();
                int wordEnd = GetWordAtCursorEnd();
                if (wordStart == -1 || wordEnd == -1 || wordStart >= wordEnd)
                {
                    return;
                }
                searchText = Text.Substring(wordStart, wordEnd - wordStart);
            }

            if (string.IsNullOrEmpty(searchText))
            {
                return;
            }

            // Clear existing extra selections
            _extraSelections.Clear();

            // Find all occurrences (case-sensitive, whole words only)
            // Matching PyKotor: QTextDocument.FindFlag.FindCaseSensitively | QTextDocument.FindFlag.FindWholeWords
            string documentText = Text;
            int searchStart = 0;

            // Build regex pattern for whole word matching
            string escapedSearchText = Regex.Escape(searchText);
            string pattern = @"\b" + escapedSearchText + @"\b";

            try
            {
                Regex searchRegex = new Regex(pattern, RegexOptions.None);
                MatchCollection matches = searchRegex.Matches(documentText);

                foreach (Match match in matches)
                {
                    // Add selection (start, end)
                    _extraSelections.Add(new Tuple<int, int>(match.Index, match.Index + match.Length));
                }

                if (_extraSelections.Count > 0)
                {
                    // Set cursor to first selection (matching PyKotor behavior)
                    var firstSelection = _extraSelections[0];
                    SelectionStart = firstSelection.Item1;
                    SelectionEnd = firstSelection.Item2;
                }
            }
            catch (ArgumentException)
            {
                // Invalid regex pattern - return without selections
                return;
            }
        }

        // Matching PyKotor implementation: QPlainTextEdit.extraSelections()
        // Original: Returns list of extra selections for testing
        /// <summary>
        /// Gets the list of extra selections (for testing purposes).
        /// Returns list of (start, end) tuples representing all extra selections.
        /// </summary>
        public List<Tuple<int, int>> GetExtraSelections()
        {
            return new List<Tuple<int, int>>(_extraSelections);
        }

        // Get the start position of the word at the current cursor position
        // Based on common text editor word boundary detection
        // Uses word characters (letters, digits, underscore) as delimiters
        // Returns -1 if no word found
        private int GetWordAtCursorStart()
        {
            if (string.IsNullOrEmpty(Text) || SelectionStart < 0 || SelectionStart > Text.Length)
            {
                return -1;
            }

            int start = SelectionStart;

            // Move start backward to beginning of word
            while (start > 0 && IsWordCharacter(Text[start - 1]))
            {
                start--;
            }

            // Check if cursor is actually on a word character
            if (start < Text.Length && IsWordCharacter(Text[start]))
            {
                return start;
            }

            return -1;
        }

        // Get the end position of the word at the current cursor position
        // Based on common text editor word boundary detection
        // Uses word characters (letters, digits, underscore) as delimiters
        // Returns -1 if no word found
        private int GetWordAtCursorEnd()
        {
            if (string.IsNullOrEmpty(Text) || SelectionStart < 0 || SelectionStart > Text.Length)
            {
                return -1;
            }

            int end = SelectionStart;

            // Move end forward to end of word
            while (end < Text.Length && IsWordCharacter(Text[end]))
            {
                end++;
            }

            return end;
        }

        // Check if a character is part of a word
        // Based on common identifier rules: letters, digits, underscore
        private bool IsWordCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2459-2483
        // Original: Code folding shortcuts and other keyboard shortcuts
        // Handle keyboard shortcuts for word selection and code folding
        // Based on VS Code behavior
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Handle Ctrl+D shortcut for select next occurrence
            // Matching PyKotor implementation: Ctrl+D selects next occurrence of word
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.D)
            {
                SelectNextOccurrence();
                e.Handled = true;
                return;
            }

            // Code folding shortcuts
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2475-2483
            // Original: fold_shortcut = QShortcut(QKeySequence("Ctrl+Shift+["), self)
            // Original: unfold_shortcut = QShortcut(QKeySequence("Ctrl+Shift+]"), self)
            // Original: fold_all_shortcut = QShortcut(QKeySequence("Ctrl+K, Ctrl+0"), self)
            // Original: unfold_all_shortcut = QShortcut(QKeySequence("Ctrl+K, Ctrl+J"), self)
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // Ctrl+Shift+[ for fold region
                if (e.Key == Key.OemOpenBrackets || e.Key == Key.BracketLeft)
                {
                    FoldRegion();
                    e.Handled = true;
                    return;
                }

                // Ctrl+Shift+] for unfold region
                if (e.Key == Key.OemCloseBrackets || e.Key == Key.BracketRight)
                {
                    UnfoldRegion();
                    e.Handled = true;
                    return;
                }
            }

            // Note: Ctrl+K, Ctrl+0 and Ctrl+K, Ctrl+J require key sequence handling
            // For now, we'll handle them as single key combinations
            // In a full implementation, you'd track the Ctrl+K press and then handle the second key
            // For simplicity, we'll use alternative shortcuts or handle them in a key sequence manager
            // Ctrl+K Ctrl+0 for fold all (using Ctrl+Shift+0 as alternative)
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D0)
            {
                FoldAll();
                e.Handled = true;
                return;
            }

            // Ctrl+K Ctrl+J for unfold all (using Ctrl+Shift+J as alternative)
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.J)
            {
                UnfoldAll();
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:983-1026
        // Original: def find_next(self, find_text: str | None = None, case_sensitive: bool = False, whole_words: bool = False, regex: bool = False, backward: bool = False):
        /// <summary>
        /// Finds the next occurrence of the specified text in the document.
        /// Matches PyKotor implementation which supports case-sensitive, whole words, and regex search.
        /// </summary>
        /// <param name="findText">The text to search for. If null, uses currently selected text.</param>
        /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
        /// <param name="wholeWords">Whether to match whole words only.</param>
        /// <param name="regex">Whether to treat the search text as a regular expression.</param>
        /// <param name="backward">Whether to search backward from the current position.</param>
        /// <returns>True if a match was found and selected, false otherwise.</returns>
        public bool FindNext(string findText = null, bool caseSensitive = false, bool wholeWords = false, bool regex = false, bool backward = false)
        {
            if (string.IsNullOrEmpty(Text))
            {
                return false;
            }

            // Use selected text if findText is null
            if (findText == null)
            {
                if (SelectionStart != SelectionEnd && !string.IsNullOrEmpty(SelectedText))
                {
                    findText = SelectedText;
                }
                else
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(findText))
            {
                return false;
            }

            string documentText = Text;
            int searchStart = backward ? SelectionStart : SelectionEnd;
            int searchEnd = backward ? 0 : documentText.Length;

            // Build regex pattern if needed
            string pattern = findText;
            if (regex)
            {
                pattern = findText;
            }
            else
            {
                // Escape special regex characters for literal search
                pattern = Regex.Escape(findText);
            }

            // Add whole word boundaries if needed
            if (wholeWords && !regex)
            {
                pattern = @"\b" + pattern + @"\b";
            }

            RegexOptions options = RegexOptions.None;
            if (!caseSensitive)
            {
                options |= RegexOptions.IgnoreCase;
            }

            try
            {
                Regex searchRegex = new Regex(pattern, options);

                if (backward)
                {
                    // Search backward from current position
                    // Get all matches up to searchStart, then take the last one
                    string textToSearch = documentText.Substring(0, searchStart);
                    MatchCollection matches = searchRegex.Matches(textToSearch);
                    Match lastMatch = null;
                    foreach (Match match in matches)
                    {
                        if (match.Index < searchStart)
                        {
                            lastMatch = match;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (lastMatch != null)
                    {
                        SelectionStart = lastMatch.Index;
                        SelectionEnd = lastMatch.Index + lastMatch.Length;
                        return true;
                    }
                }
                else
                {
                    // Search forward from current position
                    Match match = searchRegex.Match(documentText, searchStart);
                    if (match.Success)
                    {
                        SelectionStart = match.Index;
                        SelectionEnd = match.Index + match.Length;
                        return true;
                    }

                    // Wrap around - search from beginning
                    match = searchRegex.Match(documentText, 0);
                    if (match.Success && match.Index < searchStart)
                    {
                        SelectionStart = match.Index;
                        SelectionEnd = match.Index + match.Length;
                        return true;
                    }
                }
            }
            catch (ArgumentException)
            {
                // Invalid regex pattern - return false
                return false;
            }

            return false;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1028-1030
        // Original: def find_previous(self, find_text: str | None = None, case_sensitive: bool = False, whole_words: bool = False, regex: bool = False):
        /// <summary>
        /// Finds the previous occurrence of the specified text in the document.
        /// </summary>
        /// <param name="findText">The text to search for. If null, uses currently selected text.</param>
        /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
        /// <param name="wholeWords">Whether to match whole words only.</param>
        /// <param name="regex">Whether to treat the search text as a regular expression.</param>
        /// <returns>True if a match was found and selected, false otherwise.</returns>
        public bool FindPrevious(string findText = null, bool caseSensitive = false, bool wholeWords = false, bool regex = false)
        {
            return FindNext(findText, caseSensitive, wholeWords, regex, backward: true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1040-1070
        // Original: def replace_all_occurrences(self, find_text: str, replace_text: str, case_sensitive: bool = False, whole_words: bool = False, regex: bool = False):
        /// <summary>
        /// Replaces all occurrences of the specified text in the document.
        /// Matches PyKotor implementation which supports case-sensitive, whole words, and regex search.
        /// </summary>
        /// <param name="findText">The text to search for.</param>
        /// <param name="replaceText">The text to replace matches with.</param>
        /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
        /// <param name="wholeWords">Whether to match whole words only.</param>
        /// <param name="regex">Whether to treat the search text as a regular expression.</param>
        /// <returns>The number of occurrences replaced.</returns>
        public int ReplaceAllOccurrences(string findText, string replaceText, bool caseSensitive = false, bool wholeWords = false, bool regex = false)
        {
            if (string.IsNullOrEmpty(Text) || string.IsNullOrEmpty(findText))
            {
                return 0;
            }

            string documentText = Text;

            // Build regex pattern if needed
            string pattern = findText;
            if (regex)
            {
                pattern = findText;
            }
            else
            {
                // Escape special regex characters for literal search
                pattern = Regex.Escape(findText);
            }

            // Add whole word boundaries if needed
            if (wholeWords && !regex)
            {
                pattern = @"\b" + pattern + @"\b";
            }

            RegexOptions options = RegexOptions.None;
            if (!caseSensitive)
            {
                options |= RegexOptions.IgnoreCase;
            }

            try
            {
                Regex searchRegex = new Regex(pattern, options);

                // Count occurrences before replacement
                int count = searchRegex.Matches(documentText).Count;

                if (count == 0)
                {
                    return 0;
                }

                // Matching PyKotor implementation: Replace all matches with literal replaceText
                // The PyKotor code uses cursor.insertText(replace_text) which inserts literal text,
                // not a regex replacement pattern. So we need to replace each match individually.
                StringBuilder result = new StringBuilder(documentText);
                int offset = 0;

                // Find all matches and replace them (working backwards to preserve indices)
                MatchCollection matches = searchRegex.Matches(documentText);
                for (int i = matches.Count - 1; i >= 0; i--)
                {
                    Match match = matches[i];
                    result.Remove(match.Index + offset, match.Length);
                    result.Insert(match.Index + offset, replaceText ?? "");
                    offset += (replaceText ?? "").Length - match.Length;
                }

                // Update the text
                Text = result.ToString();

                // Clear selection after replace all
                SelectionStart = 0;
                SelectionEnd = 0;

                return count;
            }
            catch (ArgumentException)
            {
                // Invalid regex pattern - return 0
                return 0;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1588-1622
        // Original: def mousePressEvent(self, event: QMouseEvent):
        /// <summary>
        /// Handles pointer press events for column selection mode.
        /// Column selection is activated by Alt+Shift+LeftButton drag.
        /// </summary>
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            // Check if Alt+Shift is pressed for column selection
            // Matching PyKotor implementation: Alt+Shift modifier activates column selection mode
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && 
                e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
                e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _columnSelectionMode = true;
                Point pos = e.GetPosition(this);
                _columnSelectionAnchor = pos;
                
                // Get character position at click and set cursor
                int charIndex = GetCharacterIndexFromPoint(pos);
                if (charIndex >= 0 && charIndex <= (Text?.Length ?? 0))
                {
                    SelectionStart = charIndex;
                    SelectionEnd = charIndex;
                }
                
                e.Handled = true;
                return;
            }
            
            _columnSelectionMode = false;
            _columnSelectionAnchor = null;
            base.OnPointerPressed(e);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1624-1680
        // Original: def mouseMoveEvent(self, event: QMouseEvent):
        /// <summary>
        /// Handles pointer move events for column selection dragging.
        /// Creates a column/block selection spanning the same column range across multiple lines.
        /// </summary>
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (_columnSelectionMode && _columnSelectionAnchor.HasValue)
            {
                // Perform column/block selection
                Point anchorPos = _columnSelectionAnchor.Value;
                Point currentPos = e.GetPosition(this);
                
                // Get character positions at both points
                int anchorCharIndex = GetCharacterIndexFromPoint(anchorPos);
                int currentCharIndex = GetCharacterIndexFromPoint(currentPos);
                
                if (anchorCharIndex < 0 || currentCharIndex < 0)
                {
                    base.OnPointerMoved(e);
                    return;
                }
                
                // Calculate line and column positions
                GetLineAndColumn(anchorCharIndex, out int anchorLine, out int anchorCol);
                GetLineAndColumn(currentCharIndex, out int currentLine, out int currentCol);
                
                // Determine selection bounds
                int startLine = Math.Min(anchorLine, currentLine);
                int endLine = Math.Max(anchorLine, currentLine);
                int startCol = Math.Min(anchorCol, currentCol);
                int endCol = Math.Max(anchorCol, currentCol);
                
                // Create column selection by selecting same column range across all lines
                // Build selection string that spans the column range
                string[] lines = (Text ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                
                if (startLine < 0 || endLine >= lines.Length)
                {
                    base.OnPointerMoved(e);
                    return;
                }
                
                // Calculate selection start and end positions
                int selectionStart = GetCharacterIndexFromLineAndColumn(startLine, startCol, lines);
                int selectionEnd = GetCharacterIndexFromLineAndColumn(endLine, endCol, lines);
                
                // For column selection, we need to select the rectangular region
                // This means selecting from startLine:startCol to endLine:endCol across all lines
                // Since TextBox doesn't support true column selection, we'll select the entire range
                // but the visual effect will be similar
                if (selectionStart >= 0 && selectionEnd >= 0)
                {
                    SelectionStart = Math.Min(selectionStart, selectionEnd);
                    SelectionEnd = Math.Max(selectionStart, selectionEnd);
                }
                
                e.Handled = true;
                return;
            }
            
            base.OnPointerMoved(e);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1682-1691
        // Original: def mouseReleaseEvent(self, event: QMouseEvent):
        /// <summary>
        /// Handles pointer release events to end column selection mode.
        /// </summary>
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (_columnSelectionMode)
            {
                _columnSelectionMode = false;
                // Don't reset anchor yet - user might want to extend selection
                e.Handled = true;
                return;
            }
            
            _columnSelectionAnchor = null;
            base.OnPointerReleased(e);
        }

        /// <summary>
        /// Gets the character index from a point in the TextBox.
        /// Uses TextBox's built-in hit testing if available, otherwise calculates manually.
        /// </summary>
        private int GetCharacterIndexFromPoint(Point point)
        {
            if (string.IsNullOrEmpty(Text))
            {
                return 0;
            }
            
            // For Avalonia TextBox, we need to calculate character position from point
            // This is a simplified implementation - in a real scenario, you'd use TextLayout.HitTestPoint
            // For now, we'll use a basic approximation based on font metrics
            
            // Get approximate character width (this is a simplification)
            // In a real implementation, you'd measure actual character widths
            double charWidth = 8.0; // Approximate character width in pixels (monospace font)
            double lineHeight = 20.0; // Approximate line height in pixels
            
            // Calculate approximate line number
            int lineNumber = (int)(point.Y / lineHeight);
            if (lineNumber < 0) lineNumber = 0;
            
            // Split text into lines
            string[] lines = Text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lineNumber >= lines.Length)
            {
                lineNumber = lines.Length - 1;
            }
            
            // Calculate character position in line
            int charInLine = (int)(point.X / charWidth);
            if (charInLine < 0) charInLine = 0;
            
            // Calculate absolute character index
            int charIndex = 0;
            for (int i = 0; i < lineNumber && i < lines.Length; i++)
            {
                charIndex += lines[i].Length;
                // Add newline length (1 for \n, 2 for \r\n)
                if (i < lines.Length - 1)
                {
                    charIndex += Text.Contains("\r\n") ? 2 : 1;
                }
            }
            
            // Add character position in current line
            if (lineNumber < lines.Length)
            {
                charInLine = Math.Min(charInLine, lines[lineNumber].Length);
                charIndex += charInLine;
            }
            
            return Math.Min(charIndex, Text.Length);
        }

        /// <summary>
        /// Gets the line and column number from a character index.
        /// </summary>
        private void GetLineAndColumn(int charIndex, out int line, out int column)
        {
            line = 0;
            column = 0;
            
            if (string.IsNullOrEmpty(Text) || charIndex < 0)
            {
                return;
            }
            
            if (charIndex > Text.Length)
            {
                charIndex = Text.Length;
            }
            
            // Split text into lines
            string[] lines = Text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            
            int currentIndex = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                int lineLength = lines[i].Length;
                int newlineLength = (i < lines.Length - 1) ? (Text.Contains("\r\n") ? 2 : 1) : 0;
                
                if (charIndex >= currentIndex && charIndex <= currentIndex + lineLength)
                {
                    line = i;
                    column = charIndex - currentIndex;
                    return;
                }
                
                currentIndex += lineLength + newlineLength;
            }
            
            // If we get here, the character index is at or beyond the end
            line = lines.Length - 1;
            column = lines[line].Length;
        }

        /// <summary>
        /// Gets the character index from a line and column number.
        /// </summary>
        private int GetCharacterIndexFromLineAndColumn(int line, int column, string[] lines)
        {
            if (lines == null || line < 0 || line >= lines.Length)
            {
                return -1;
            }
            
            int charIndex = 0;
            for (int i = 0; i < line && i < lines.Length; i++)
            {
                charIndex += lines[i].Length;
                // Add newline length
                if (i < lines.Length - 1)
                {
                    charIndex += Text.Contains("\r\n") ? 2 : 1;
                }
            }
            
            // Add column position, but don't exceed line length
            if (line < lines.Length)
            {
                column = Math.Min(column, lines[line].Length);
                charIndex += column;
            }
            
            return Math.Min(charIndex, Text?.Length ?? 0);
        }

        /// <summary>
        /// Gets whether column selection mode is currently active.
        /// Exposed for testing purposes.
        /// </summary>
        public bool ColumnSelectionMode => _columnSelectionMode;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1257-1272
        // Original: def duplicate_line(self):
        /// <summary>
        /// Duplicates the current line or selected lines.
        /// If there's a selection, duplicates the lines containing the selection.
        /// If there's no selection, duplicates the current line.
        /// Matching Python behavior: selects line content (without trailing newline), then inserts newline + selected text.
        /// </summary>
        public void DuplicateLine()
        {
            if (string.IsNullOrEmpty(Text))
            {
                return;
            }

            string[] lines = Text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            string newline = Text.Contains("\r\n") ? "\r\n" : (Text.Contains("\n") ? "\n" : "\r");

            int currentPos = SelectionStart;
            int selectionStart = SelectionStart;
            int selectionEnd = SelectionEnd;

            // Calculate which line the cursor/selection is on
            int currentLine = GetLineFromPosition(currentPos);
            int startLine = GetLineFromPosition(selectionStart);
            int endLine = GetLineFromPosition(selectionEnd);

            // If there's a selection, select from start of first line to end of last line (without trailing newline)
            if (selectionStart != selectionEnd)
            {
                // Get start position of first selected line
                int firstLineStart = GetPositionFromLine(startLine);
                // Get end position of last selected line (end of line content, NOT including newline)
                // This matches Python's EndOfLine behavior which stops before the newline
                int lastLineEnd = GetPositionFromLineContentEnd(endLine);
                
                // Select the full lines (content only, no trailing newline)
                SelectionStart = firstLineStart;
                SelectionEnd = lastLineEnd;
            }
            else
            {
                // No selection - select current line (content only, no trailing newline)
                int lineStart = GetPositionFromLine(currentLine);
                int lineEnd = GetPositionFromLineContentEnd(currentLine);
                
                SelectionStart = lineStart;
                SelectionEnd = lineEnd;
            }

            // Get the selected text (the line(s) to duplicate, without trailing newline)
            string selectedText = SelectedText;
            
            if (string.IsNullOrEmpty(selectedText))
            {
                return;
            }

            // Move cursor to end of the selected line(s) (where SelectionEnd is now)
            int insertPosition = SelectionEnd;
            
            // Insert newline + selected text (matching Python: cursor.insertText("\n" + text))
            string textToInsert = newline + selectedText;
            
            // Update the text
            string newText = Text.Insert(insertPosition, textToInsert);
            Text = newText;
            
            // Move cursor to end of inserted text
            SelectionStart = insertPosition + textToInsert.Length;
            SelectionEnd = SelectionStart;
        }

        /// <summary>
        /// Gets the line number (0-based) from a character position.
        /// </summary>
        private int GetLineFromPosition(int position)
        {
            if (string.IsNullOrEmpty(Text) || position < 0)
            {
                return 0;
            }

            if (position > Text.Length)
            {
                position = Text.Length;
            }

            string[] lines = Text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            int currentPos = 0;
            
            for (int i = 0; i < lines.Length; i++)
            {
                int lineLength = lines[i].Length;
                int newlineLength = (i < lines.Length - 1) ? (Text.Contains("\r\n") ? 2 : 1) : 0;
                
                if (position >= currentPos && position <= currentPos + lineLength)
                {
                    return i;
                }
                
                currentPos += lineLength + newlineLength;
            }
            
            return lines.Length - 1;
        }

        /// <summary>
        /// Gets the character position of the start of a line (0-based).
        /// </summary>
        private int GetPositionFromLine(int line)
        {
            if (string.IsNullOrEmpty(Text) || line < 0)
            {
                return 0;
            }

            string[] lines = Text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (line >= lines.Length)
            {
                return Text.Length;
            }

            int position = 0;
            string newline = Text.Contains("\r\n") ? "\r\n" : (Text.Contains("\n") ? "\n" : "\r");
            int newlineLength = newline.Length;

            for (int i = 0; i < line && i < lines.Length; i++)
            {
                position += lines[i].Length + newlineLength;
            }

            return position;
        }

        /// <summary>
        /// Gets the character position of the end of a line content (0-based), NOT including the newline.
        /// This matches Python's QTextCursor.MoveOperation.EndOfLine behavior which stops before the newline.
        /// </summary>
        private int GetPositionFromLineContentEnd(int line)
        {
            if (string.IsNullOrEmpty(Text) || line < 0)
            {
                return 0;
            }

            string[] lines = Text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (line >= lines.Length)
            {
                return Text.Length;
            }

            int position = 0;
            string newline = Text.Contains("\r\n") ? "\r\n" : (Text.Contains("\n") ? "\n" : "\r");
            int newlineLength = newline.Length;

            // Calculate position up to and including the specified line's content (but not its newline)
            for (int i = 0; i <= line && i < lines.Length; i++)
            {
                position += lines[i].Length;
                // Only add newline for lines before the target line (not for the target line itself)
                if (i < line && i < lines.Length - 1)
                {
                    position += newlineLength;
                }
            }

            return position;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:482-570
        // Original: def _update_foldable_regions(self):
        /// <summary>
        /// Updates the foldable regions based on braces in the document.
        /// Detects brace pairs ({}) and marks regions between them as foldable.
        /// </summary>
        private void UpdateFoldableRegions()
        {
            if (string.IsNullOrEmpty(Text))
            {
                _foldableRegions.Clear();
                return;
            }

            // Preserve existing folded state
            HashSet<int> oldFolded = new HashSet<int>(_foldedBlockNumbers);
            _foldableRegions.Clear();

            // Track brace pairs to find foldable regions
            List<(int blockNumber, int braceCount)> braceStack = new List<(int, int)>();
            int braceCount = 0;

            string[] lines = Text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            string newline = Text.Contains("\r\n") ? "\r\n" : (Text.Contains("\n") ? "\n" : "\r");

            for (int blockNumber = 0; blockNumber < lines.Length; blockNumber++)
            {
                string text = lines[blockNumber];

                // Count braces in this line (ignore braces in strings/comments)
                int openBraces = 0;
                int closeBraces = 0;
                bool inString = false;
                bool inSingleLineComment = false;

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];

                    // Handle string literals
                    if (c == '"' && (i == 0 || (i > 0 && text[i - 1] != '\\')))
                    {
                        inString = !inString;
                    }
                    else if (!inString)
                    {
                        // Handle comments
                        if (i < text.Length - 1 && text.Substring(i, 2) == "//")
                        {
                            inSingleLineComment = true;
                            break;
                        }
                        else if (!inSingleLineComment)
                        {
                            if (c == '{')
                            {
                                openBraces++;
                            }
                            else if (c == '}')
                            {
                                closeBraces++;
                            }
                        }
                    }
                }

                // Track when we enter a new brace level
                for (int i = 0; i < openBraces; i++)
                {
                    braceCount++;
                    braceStack.Add((blockNumber, braceCount));
                }

                // When closing braces, match with opening braces
                for (int i = 0; i < closeBraces; i++)
                {
                    braceCount--;
                    if (braceCount < 0)
                    {
                        braceCount = 0;
                        continue;
                    }

                    // Find matching opening brace
                    while (braceStack.Count > 0)
                    {
                        var (startBlock, startCount) = braceStack[braceStack.Count - 1];
                        if (startCount == braceCount + 1)
                        {
                            // Found matching brace pair
                            braceStack.RemoveAt(braceStack.Count - 1);
                            // Only create foldable region if there are multiple lines
                            if (blockNumber > startBlock + 1)
                            {
                                _foldableRegions[startBlock] = blockNumber;
                            }
                            break;
                        }
                        braceStack.RemoveAt(braceStack.Count - 1);
                    }
                }
            }

            // Restore folded state for regions that still exist
            _foldedBlockNumbers.Clear();
            foreach (int startBlock in oldFolded)
            {
                if (_foldableRegions.ContainsKey(startBlock))
                {
                    _foldedBlockNumbers.Add(startBlock);
                    // Re-apply folding if it was folded before
                    // Note: In Avalonia TextBox, we can't directly hide lines like Qt,
                    // so we maintain the folded state for API compatibility
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1479-1493
        // Original: def _find_foldable_region_at_cursor(self) -> tuple[int, int] | None:
        /// <summary>
        /// Finds the foldable region that contains or starts at the current cursor position.
        /// </summary>
        /// <returns>Tuple of (start_block, end_block) if found, null otherwise.</returns>
        private (int startBlock, int endBlock)? FindFoldableRegionAtCursor()
        {
            if (string.IsNullOrEmpty(Text))
            {
                return null;
            }

            int currentLine = GetLineFromPosition(SelectionStart);

            // Check if cursor is on a foldable region start
            if (_foldableRegions.ContainsKey(currentLine))
            {
                return (currentLine, _foldableRegions[currentLine]);
            }

            // Find the closest foldable region that contains this line
            foreach (var kvp in _foldableRegions)
            {
                int startBlock = kvp.Key;
                int endBlock = kvp.Value;
                if (startBlock <= currentLine && currentLine <= endBlock)
                {
                    return (startBlock, endBlock);
                }
            }

            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1495-1524
        // Original: def fold_region(self):
        /// <summary>
        /// Folds the current code region (VS Code Ctrl+Shift+[ behavior).
        /// Note: In Avalonia TextBox, we can't directly hide lines like Qt's QPlainTextEdit.
        /// This implementation tracks folded state for API compatibility.
        /// </summary>
        public void FoldRegion()
        {
            var region = FindFoldableRegionAtCursor();
            if (!region.HasValue)
            {
                return;
            }

            int startBlock = region.Value.startBlock;
            int endBlock = region.Value.endBlock;

            if (_foldedBlockNumbers.Contains(startBlock))
            {
                return; // Already folded
            }

            // Mark as folded
            // Note: In a full implementation with a proper code editor control,
            // we would hide the lines here. For TextBox, we maintain state for API compatibility.
            _foldedBlockNumbers.Add(startBlock);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1526-1555
        // Original: def unfold_region(self):
        /// <summary>
        /// Unfolds the current code region (VS Code Ctrl+Shift+] behavior).
        /// Note: In Avalonia TextBox, we can't directly show hidden lines like Qt's QPlainTextEdit.
        /// This implementation tracks unfolded state for API compatibility.
        /// </summary>
        public void UnfoldRegion()
        {
            var region = FindFoldableRegionAtCursor();
            if (!region.HasValue)
            {
                return;
            }

            int startBlock = region.Value.startBlock;
            int endBlock = region.Value.endBlock;

            if (!_foldedBlockNumbers.Contains(startBlock))
            {
                return; // Not folded
            }

            // Mark as unfolded
            // Note: In a full implementation with a proper code editor control,
            // we would show the lines here. For TextBox, we maintain state for API compatibility.
            _foldedBlockNumbers.Remove(startBlock);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1557-1570
        // Original: def fold_all(self):
        /// <summary>
        /// Folds all code regions (VS Code Ctrl+K Ctrl+0 behavior).
        /// </summary>
        public void FoldAll()
        {
            UpdateFoldableRegions();
            foreach (int startBlock in _foldableRegions.Keys)
            {
                if (!_foldedBlockNumbers.Contains(startBlock))
                {
                    // Temporarily set cursor to this block to reuse fold logic
                    int pos = GetPositionFromLine(startBlock);
                    if (pos >= 0 && pos <= Text.Length)
                    {
                        SelectionStart = pos;
                        SelectionEnd = pos;
                        FoldRegion();
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:1572-1586
        // Original: def unfold_all(self):
        /// <summary>
        /// Unfolds all code regions (VS Code Ctrl+K Ctrl+J behavior).
        /// </summary>
        public void UnfoldAll()
        {
            // Clear folded blocks
            _foldedBlockNumbers.Clear();
            _foldedContentCache.Clear();
        }

        /// <summary>
        /// Gets whether a block number is folded.
        /// Exposed for testing purposes.
        /// </summary>
        public bool IsBlockFolded(int blockNumber)
        {
            return _foldedBlockNumbers.Contains(blockNumber);
        }

        /// <summary>
        /// Gets the foldable regions dictionary.
        /// Exposed for testing purposes.
        /// </summary>
        public Dictionary<int, int> GetFoldableRegions()
        {
            return new Dictionary<int, int>(_foldableRegions);
        }

        /// <summary>
        /// Gets the count of folded blocks.
        /// Exposed for testing purposes to match Python test behavior (len(_folded_block_numbers)).
        /// </summary>
        public int GetFoldedBlockCount()
        {
            return _foldedBlockNumbers.Count;
        }
    }
}

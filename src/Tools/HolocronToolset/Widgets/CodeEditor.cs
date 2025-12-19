using System;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:80
    // Original: class CodeEditor(QPlainTextEdit):
    public class CodeEditor : TextBox
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/code_editor.py:95-121
        // Original: def __init__(self, parent: QWidget):
        public CodeEditor()
        {
            InitializeComponent();
            AcceptsReturn = true;
            AcceptsTab = true;
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2459-2461
        // Original: select_next_shortcut = QShortcut(QKeySequence("Ctrl+D"), self)
        // Original: select_next_shortcut.activated.connect(self.ui.codeEdit.select_next_occurrence)
        // Handle keyboard shortcuts for word selection (Ctrl+D for select next occurrence)
        // Based on VS Code behavior: Ctrl+D selects next occurrence of word at cursor
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
                    MatchCollection matches = searchRegex.Matches(documentText, 0, searchStart);
                    if (matches.Count > 0)
                    {
                        Match lastMatch = matches[matches.Count - 1];
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
                    match = searchRegex.Match(documentText, 0, searchStart);
                    if (match.Success)
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
    }
}

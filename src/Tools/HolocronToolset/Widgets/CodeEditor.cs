using System;
using System.Text;
using Avalonia.Controls;
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
    }
}

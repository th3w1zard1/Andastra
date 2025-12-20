using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HolocronToolset.Data;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/syntax_highlighter.py:21
    // Original: class SyntaxHighlighter(QSyntaxHighlighter):
    /// <summary>
    /// Syntax highlighter for NWScript (NSS) code in the NSS Editor.
    /// Provides syntax highlighting patterns for keywords, functions, numbers, strings, and comments.
    /// Updates highlighting rules based on the selected game (K1 or TSL).
    /// </summary>
    public class NWScriptSyntaxHighlighter
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/syntax_highlighter.py:22-40
        // Original: KEYWORDS: ClassVar[list[str]]
        private static readonly string[] Keywords = new[]
        {
            "#include",
            "action",
            "effect",
            "FALSE",
            "float",
            "for",
            "if",
            "int",
            "location",
            "object",
            "return",
            "string",
            "talent",
            "TRUE",
            "vector",
            "void",
            "while"
        };

        private static readonly string[] Operators = new[]
        {
            "=", "==", "!=", "<", "<=", ">", ">=", "!", "+", "-", "/", "<<", ">>", "&", "|"
        };

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/syntax_highlighter.py:49-58
        // Original: def __init__(self, parent: QTextDocument, installation: HTInstallation | None = None):
        /// <summary>
        /// Initializes a new instance of the NWScriptSyntaxHighlighter.
        /// </summary>
        /// <param name="document">The document to highlight (for compatibility with Python interface).</param>
        /// <param name="installation">The installation to determine game-specific highlighting rules.</param>
        public NWScriptSyntaxHighlighter(object document, HTInstallation installation = null)
        {
            Document = document;
            Installation = installation;
            IsTsl = installation?.Tsl ?? false;
            SetupRules();
        }

        /// <summary>
        /// Gets or sets the document being highlighted.
        /// </summary>
        public object Document { get; set; }

        /// <summary>
        /// Gets or sets the installation used for game-specific highlighting.
        /// </summary>
        public HTInstallation Installation { get; set; }

        /// <summary>
        /// Gets or sets whether the highlighter is configured for TSL (KOTOR 2).
        /// </summary>
        public bool IsTsl { get; set; }

        /// <summary>
        /// Gets the highlighting rules currently in use.
        /// </summary>
        public List<HighlightingRule> Rules { get; private set; }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/syntax_highlighter.py:60-81
        // Original: def _setupRules(self):
        /// <summary>
        /// Sets up the highlighting rules based on the current game configuration.
        /// </summary>
        private void SetupRules()
        {
            Rules = new List<HighlightingRule>();

            // Keyword format (blue)
            var keywordFormat = new HighlightingFormat { Color = "blue", Bold = false, Italic = false };
            foreach (string keyword in Keywords)
            {
                Rules.Add(new HighlightingRule
                {
                    Pattern = new Regex(@"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.Compiled),
                    Format = keywordFormat
                });
            }

            // Function format (darkGreen) - matches function calls like functionName(
            var functionFormat = new HighlightingFormat { Color = "darkGreen", Bold = false, Italic = false };
            Rules.Add(new HighlightingRule
            {
                Pattern = new Regex(@"\b[A-Za-z0-9_]+(?=\()", RegexOptions.Compiled),
                Format = functionFormat
            });

            // Number format (brown)
            var numberFormat = new HighlightingFormat { Color = "brown", Bold = false, Italic = false };
            Rules.Add(new HighlightingRule
            {
                Pattern = new Regex(@"\b[0-9]+\b", RegexOptions.Compiled),
                Format = numberFormat
            });

            // String format (darkMagenta)
            var stringFormat = new HighlightingFormat { Color = "darkMagenta", Bold = false, Italic = false };
            Rules.Add(new HighlightingRule
            {
                Pattern = new Regex(@""".*?""", RegexOptions.Compiled),
                Format = stringFormat
            });

            // Comment format (gray, italic)
            var commentFormat = new HighlightingFormat { Color = "gray", Bold = false, Italic = true };
            Rules.Add(new HighlightingRule
            {
                Pattern = new Regex(@"//[^\n]*", RegexOptions.Compiled),
                Format = commentFormat
            });

            // Multi-line comment format
            MultilineCommentFormat = commentFormat;
        }

        /// <summary>
        /// Gets or sets the format for multi-line comments.
        /// </summary>
        public HighlightingFormat MultilineCommentFormat { get; set; }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/syntax_highlighter.py:148-157
        // Original: def update_rules(self, is_tsl: bool):
        /// <summary>
        /// Updates the highlighting rules based on the selected game.
        /// Reinitializes rules and triggers re-highlighting if the document is available.
        /// </summary>
        /// <param name="isTsl">True if TSL (KOTOR 2), false if K1.</param>
        public void UpdateRules(bool isTsl)
        {
            IsTsl = isTsl;
            SetupRules();
            // Note: In Avalonia, actual re-highlighting would need to be implemented
            // in the UI layer. This method matches the Python interface.
        }

        /// <summary>
        /// Represents a highlighting rule with a pattern and format.
        /// </summary>
        public class HighlightingRule
        {
            public Regex Pattern { get; set; }
            public HighlightingFormat Format { get; set; }
        }

        /// <summary>
        /// Represents formatting information for highlighted text.
        /// </summary>
        public class HighlightingFormat
        {
            public string Color { get; set; }
            public bool Bold { get; set; }
            public bool Italic { get; set; }
        }
    }
}


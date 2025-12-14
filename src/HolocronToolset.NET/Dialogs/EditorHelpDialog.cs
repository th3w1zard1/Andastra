using System;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Markdig;

namespace HolocronToolset.NET.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:46
    // Original: class EditorHelpDialog(QDialog):
    public partial class EditorHelpDialog : Window
    {
        private TextBox _textBrowser;

        // Expose TextBrowser for testing
        public TextBox TextBrowser => _textBrowser;

        // Public parameterless constructor for XAML
        public EditorHelpDialog() : this(null, "")
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:49-82
        // Original: def __init__(self, parent, wiki_filename):
        public EditorHelpDialog(Window parent, string wikiFilename)
        {
            InitializeComponent();
            Title = $"Help - {wikiFilename}";
            Width = 900;
            Height = 700;
            SetupUI();
            LoadWikiFile(wikiFilename);
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
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };
            _textBrowser = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                AcceptsTab = false,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas")
            };
            scrollViewer.Content = _textBrowser;
            Content = scrollViewer;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _textBrowser = this.FindControl<TextBox>("textBrowser");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:19-43
        // Original: def get_wiki_path() -> Path:
        public static string GetWikiPath()
        {
            // Check if frozen (EXE mode)
            // When frozen, wiki should be bundled in the same directory as the executable
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(exePath))
            {
                string exeDir = Path.GetDirectoryName(exePath);
                string wikiPath = Path.Combine(exeDir, "wiki");
                if (Directory.Exists(wikiPath))
                {
                    return wikiPath;
                }
            }

            // Development mode: check toolset/wiki first, then root wiki
            // Get the directory where EditorHelpDialog.cs is located
            string currentDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(currentDir))
            {
                // Navigate up from bin/Debug/net9/ to src/HolocronToolset.NET/Dialogs/ then up to root
                var toolsetWiki = Path.Combine(currentDir, "..", "..", "..", "..", "wiki");
                toolsetWiki = Path.GetFullPath(toolsetWiki);
                if (Directory.Exists(toolsetWiki))
                {
                    return toolsetWiki;
                }

                // Check root wiki (one more level up)
                var rootWiki = Path.Combine(currentDir, "..", "..", "..", "..", "..", "wiki");
                rootWiki = Path.GetFullPath(rootWiki);
                if (Directory.Exists(rootWiki))
                {
                    return rootWiki;
                }
            }

            // Fallback
            return "./wiki";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:254-296
        // Original: def load_wiki_file(self, wiki_filename: str):
        private void LoadWikiFile(string wikiFilename)
        {
            if (string.IsNullOrEmpty(wikiFilename))
            {
                return;
            }

            string wikiPath = GetWikiPath();
            string filePath = Path.Combine(wikiPath, wikiFilename);

            if (!File.Exists(filePath))
            {
                string errorHtml = $@"
<html>
<body>
<h1>Help File Not Found</h1>
<p>Could not find help file: <code>{wikiFilename}</code></p>
<p>Expected location: <code>{filePath}</code></p>
<p>Wiki path: <code>{wikiPath}</code></p>
</body>
</html>";
                if (_textBrowser != null)
                {
                    _textBrowser.Text = errorHtml;
                }
                return;
            }

            try
            {
                string text = File.ReadAllText(filePath, Encoding.UTF8);
                // Convert markdown to HTML using Markdig
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                string htmlBody = Markdown.ToHtml(text, pipeline);
                string html = WrapHtmlWithStyles(htmlBody);

                if (_textBrowser != null)
                {
                    // TODO: Use HTML rendering when available in Avalonia
                    // For now, show the markdown text (HTML rendering requires WebView which may not be available)
                    // Strip HTML tags for plain text display
                    string plainText = System.Text.RegularExpressions.Regex.Replace(htmlBody, "<.*?>", "");
                    plainText = System.Net.WebUtility.HtmlDecode(plainText);
                    _textBrowser.Text = plainText;
                }
            }
            catch (Exception ex)
            {
                string errorHtml = $@"
<html>
<body>
<h1>Error Loading Help File</h1>
<p>Could not load help file: <code>{wikiFilename}</code></p>
<p>Error: {ex.Message}</p>
</body>
</html>";
                if (_textBrowser != null)
                {
                    _textBrowser.Text = errorHtml;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:84-252
        // Original: def _wrap_html_with_styles(self, html_body: str) -> str:
        private string WrapHtmlWithStyles(string htmlBody)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Fira Sans', 'Droid Sans', 'Helvetica Neue', sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 100%;
            margin: 0;
            padding: 24px;
            background-color: #ffffff;
        }}
        h1 {{ font-size: 2em; font-weight: 600; margin-top: 0; margin-bottom: 16px; padding-bottom: 12px; border-bottom: 2px solid #e1e4e8; color: #24292e; }}
        h2 {{ font-size: 1.5em; font-weight: 600; margin-top: 32px; margin-bottom: 16px; padding-bottom: 8px; border-bottom: 1px solid #e1e4e8; color: #24292e; }}
        h3 {{ font-size: 1.25em; font-weight: 600; margin-top: 24px; margin-bottom: 12px; color: #24292e; }}
        p {{ margin-top: 0; margin-bottom: 16px; }}
        ul, ol {{ margin-top: 0; margin-bottom: 16px; padding-left: 32px; }}
        li {{ margin-bottom: 8px; }}
        code {{ font-family: 'SFMono-Regular', 'Consolas', 'Liberation Mono', 'Menlo', 'Courier', monospace; font-size: 0.9em; padding: 2px 6px; background-color: #f6f8fa; border-radius: 3px; color: #e83e8c; }}
        pre {{ font-family: 'SFMono-Regular', 'Consolas', 'Liberation Mono', 'Menlo', 'Courier', monospace; font-size: 0.9em; padding: 16px; background-color: #f6f8fa; border-radius: 6px; overflow-x: auto; margin: 16px 0; border: 1px solid #e1e4e8; }}
        pre code {{ padding: 0; background-color: transparent; color: #24292e; border-radius: 0; }}
        a {{ color: #0366d6; text-decoration: none; }}
        a:hover {{ text-decoration: underline; }}
    </style>
</head>
<body>
{htmlBody}
</body>
</html>";
        }
    }
}

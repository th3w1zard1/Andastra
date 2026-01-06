using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Layout;
using Markdig;
using HtmlAgilityPack;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:46
    // Original: class EditorHelpDialog(QDialog):
    public partial class EditorHelpDialog : Window
    {
        private ScrollViewer _scrollViewer;
        private Panel _htmlContainer;
        private string _htmlContent;

        // Expose HTML container for testing
        public Panel HtmlContainer => _htmlContainer;

        // Expose HTML content for testing (matching PyKotor text_browser.toHtml())
        public string HtmlContent => _htmlContent ?? "";

        // Expose text content for testing (extracted from HTML)
        public string TextContent
        {
            get
            {
                if (string.IsNullOrEmpty(_htmlContent))
                {
                    return "";
                }

                // Extract text from HTML using HtmlAgilityPack
                try
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(_htmlContent);
                    return doc.DocumentNode.InnerText ?? "";
                }
                catch
                {
                    return "";
                }
            }
        }

        // Compatibility property for tests (matching PyKotor text_browser)
        public TextBlock TextBrowser
        {
            get
            {
                // Return a TextBlock with the text content for compatibility with tests
                return new TextBlock { Text = TextContent };
            }
        }

        // Public parameterless constructor for XAML
        public EditorHelpDialog() : this(null, new string[0])
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:49-82
        // Original: def __init__(self, parent, wiki_filename):
        public EditorHelpDialog(Window parent, string wikiFilename)
            : this(parent, new[] { wikiFilename })
        {
        }

        // Constructor accepting multiple wiki files
        public EditorHelpDialog(Window parent, string[] wikiFilenames)
        {
            InitializeComponent();

            // Set title based on files
            if (wikiFilenames != null && wikiFilenames.Length > 0)
            {
                if (wikiFilenames.Length == 1)
                {
                    Title = $"Help - {wikiFilenames[0]}";
                }
                else
                {
                    Title = $"Help - {wikiFilenames.Length} Documents";
                }
            }
            else
            {
                Title = "Help";
            }

            Width = 900;
            Height = 700;
            SetupUI();
            LoadWikiFiles(wikiFilenames);
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
            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };
            _htmlContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(24)
            };
            _scrollViewer.Content = _htmlContainer;
            Content = _scrollViewer;
        }

        private void SetupUI()
        {
            // If _htmlContainer is already initialized (e.g., by SetupProgrammaticUI), skip control finding
            if (_htmlContainer != null)
            {
                return;
            }

            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            try
            {
                // Find controls from XAML
                _scrollViewer = this.FindControl<ScrollViewer>("scrollViewer");
                _htmlContainer = this.FindControl<Panel>("htmlContainer");

                // If XAML doesn't have htmlContainer, create it
                if (_htmlContainer == null && _scrollViewer != null)
                {
                    _htmlContainer = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(24)
                    };
                    _scrollViewer.Content = _htmlContainer;
                }
            }
            catch
            {
                // XAML controls not available - create programmatic UI for tests
                SetupProgrammaticUI();
            }
        }

        // Test override for wiki path (allows mocking in tests)
        // Matching PyKotor: monkeypatch.get_wiki_path in tests
        // This is set to non-null only during tests to override the wiki path resolution
        private static string _testWikiPathOverride = null;

        /// <summary>
        /// Sets a test override for the wiki path. Used only in unit tests.
        /// Matching PyKotor: monkeypatch.get_wiki_path in test_editor_help_dialog_load_existing_file
        /// </summary>
        /// <param name="wikiPath">The wiki path to use for testing, or null to clear the override.</param>
        internal static void SetTestWikiPathOverride(string wikiPath)
        {
            _testWikiPathOverride = wikiPath;
        }

        /// <summary>
        /// Gets the test override wiki path if set, otherwise null.
        /// </summary>
        internal static string GetTestWikiPathOverride()
        {
            return _testWikiPathOverride;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:19-43
        // Original: def get_wiki_path() -> Path:
        public static string GetWikiPath()
        {
            // Check for test override first (allows mocking in tests)
            // Matching PyKotor: with patch("toolset.gui.dialogs.editor_help.get_wiki_path", return_value=wiki_dir):
            if (_testWikiPathOverride != null)
            {
                return _testWikiPathOverride;
            }

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
                // Navigate up from bin/Debug/net9.0/ to src/HolocronToolset.NET/Dialogs/ then up to root
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
            LoadWikiFiles(new[] { wikiFilename });
        }

        // Load multiple wiki files and combine them
        private void LoadWikiFiles(string[] wikiFilenames)
        {
            if (wikiFilenames == null || wikiFilenames.Length == 0)
            {
                return;
            }

            string wikiPath = GetWikiPath();
            var htmlBodies = new List<string>();

            foreach (string wikiFilename in wikiFilenames)
            {
                if (string.IsNullOrEmpty(wikiFilename))
                {
                    continue;
                }

                string filePath = Path.Combine(wikiPath, wikiFilename);

                if (!File.Exists(filePath))
                {
                    // Add error message for this file
                    htmlBodies.Add($@"
<div>
<h2>Help File Not Found</h2>
<p>Could not find help file: <code>{wikiFilename}</code></p>
<p>Expected location: <code>{filePath}</code></p>
</div>");
                    continue;
                }

                try
                {
                    string text = File.ReadAllText(filePath, Encoding.UTF8);
                    // Convert markdown to HTML using Markdig
                    var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                    string htmlBody = Markdown.ToHtml(text, pipeline);

                    // Add a separator between documents if multiple files
                    if (htmlBodies.Count > 0)
                    {
                        htmlBodies.Add("<hr style=\"margin: 48px 0;\" />");
                    }

                    htmlBodies.Add(htmlBody);
                }
                catch (Exception ex)
                {
                    // Add error message for this file
                    htmlBodies.Add($@"
<div>
<h2>Error Loading Help File</h2>
<p>Could not load help file: <code>{wikiFilename}</code></p>
<p>Error: {ex.Message}</p>
</div>");
                }
            }

            if (htmlBodies.Count > 0 && _htmlContainer != null)
            {
                string combinedHtmlBody = string.Join("\n", htmlBodies);
                string html = WrapHtmlWithStyles(combinedHtmlBody);
                _htmlContent = html; // Store HTML for testing
                RenderHtml(html);
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

        h1 {{
            font-size: 2em;
            font-weight: 600;
            margin-top: 0;
            margin-bottom: 16px;
            padding-bottom: 12px;
            border-bottom: 2px solid #e1e4e8;
            color: #24292e;
        }}

        h2 {{
            font-size: 1.5em;
            font-weight: 600;
            margin-top: 32px;
            margin-bottom: 16px;
            padding-bottom: 8px;
            border-bottom: 1px solid #e1e4e8;
            color: #24292e;
        }}

        h3 {{
            font-size: 1.25em;
            font-weight: 600;
            margin-top: 24px;
            margin-bottom: 12px;
            color: #24292e;
        }}

        h4, h5, h6 {{
            font-size: 1.1em;
            font-weight: 600;
            margin-top: 20px;
            margin-bottom: 10px;
            color: #24292e;
        }}

        p {{
            margin-top: 0;
            margin-bottom: 16px;
        }}

        ul, ol {{
            margin-top: 0;
            margin-bottom: 16px;
            padding-left: 32px;
        }}

        li {{
            margin-bottom: 8px;
        }}

        li > p {{
            margin-bottom: 8px;
        }}

        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 24px 0;
            display: block;
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }}

        table thead {{
            background-color: #f6f8fa;
        }}

        table th {{
            font-weight: 600;
            text-align: left;
            padding: 12px 16px;
            border: 1px solid #d1d5da;
            background-color: #f6f8fa;
            color: #24292e;
        }}

        table td {{
            padding: 12px 16px;
            border: 1px solid #d1d5da;
            vertical-align: top;
        }}

        table tbody tr:nth-child(even) {{
            background-color: #f9fafb;
        }}

        table tbody tr:hover {{
            background-color: #f1f3f5;
        }}

        code {{
            font-family: 'SFMono-Regular', 'Consolas', 'Liberation Mono', 'Menlo', 'Courier', monospace;
            font-size: 0.9em;
            padding: 2px 6px;
            background-color: #f6f8fa;
            border-radius: 3px;
            color: #e83e8c;
        }}

        pre {{
            font-family: 'SFMono-Regular', 'Consolas', 'Liberation Mono', 'Menlo', 'Courier', monospace;
            font-size: 0.9em;
            padding: 16px;
            background-color: #f6f8fa;
            border-radius: 6px;
            overflow-x: auto;
            margin: 16px 0;
            border: 1px solid #e1e4e8;
        }}

        pre code {{
            padding: 0;
            background-color: transparent;
            color: #24292e;
            border-radius: 0;
        }}

        a {{
            color: #0366d6;
            text-decoration: none;
        }}

        a:hover {{
            text-decoration: underline;
        }}

        hr {{
            height: 0;
            margin: 24px 0;
            background: transparent;
            border: 0;
            border-top: 1px solid #e1e4e8;
        }}

        blockquote {{
            margin: 16px 0;
            padding: 0 16px;
            color: #6a737d;
            border-left: 4px solid #dfe2e5;
        }}

        strong {{
            font-weight: 600;
            color: #24292e;
        }}
    </style>
</head>
<body>
{htmlBody}
</body>
</html>";
        }

        // Render HTML content to Avalonia controls
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/editor_help.py:254-296
        // Original: def load_wiki_file(self, wiki_filename: str) -> None: ... self.text_browser.setHtml(html)
        private void RenderHtml(string html)
        {
            if (_htmlContainer == null)
            {
                return;
            }

            // Clear existing content
            _htmlContainer.Children.Clear();

            try
            {
                // Parse HTML using HtmlAgilityPack
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Find body element
                var body = doc.DocumentNode.SelectSingleNode("//body");
                if (body == null)
                {
                    body = doc.DocumentNode;
                }

                // Render body content
                RenderNode(body, _htmlContainer);
            }
            catch (Exception ex)
            {
                // Fallback: show error message
                var errorText = new TextBlock
                {
                    Text = $"Error rendering HTML: {ex.Message}",
                    Foreground = new SolidColorBrush(Colors.Red),
                    Margin = new Thickness(16)
                };
                _htmlContainer.Children.Add(errorText);
            }
        }

        // Recursively render HTML nodes to Avalonia controls
        private void RenderNode(HtmlNode node, Panel parent)
        {
            if (node == null)
            {
                return;
            }

            // Handle text nodes
            if (node.NodeType == HtmlNodeType.Text)
            {
                string text = node.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Find the nearest parent that can contain text
                    var textParent = parent;
                    if (parent is StackPanel stackPanel && stackPanel.Children.Count > 0)
                    {
                        var lastChild = stackPanel.Children.LastOrDefault();
                        if (lastChild is TextBlock textBlock)
                        {
                            // Append to existing TextBlock
                            textBlock.Text += text;
                            return;
                        }
                    }

                    // Create new TextBlock for text
                    var newTextBlock = new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    ApplyTextStyles(node, newTextBlock);
                    parent.Children.Add(newTextBlock);
                }
                return;
            }

            // Handle element nodes
            if (node.NodeType == HtmlNodeType.Element)
            {
                string tagName = node.Name.ToLowerInvariant();

                switch (tagName)
                {
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        RenderHeading(node, parent, tagName);
                        break;

                    case "p":
                        RenderParagraph(node, parent);
                        break;

                    case "ul":
                    case "ol":
                        RenderList(node, parent, tagName == "ol");
                        break;

                    case "li":
                        RenderListItem(node, parent);
                        break;

                    case "code":
                        RenderCode(node, parent);
                        break;

                    case "pre":
                        RenderPre(node, parent);
                        break;

                    case "a":
                        RenderLink(node, parent);
                        break;

                    case "strong":
                    case "b":
                        RenderStrong(node, parent);
                        break;

                    case "em":
                    case "i":
                        RenderEmphasis(node, parent);
                        break;

                    case "table":
                        RenderTable(node, parent);
                        break;

                    case "hr":
                        RenderHorizontalRule(parent);
                        break;

                    case "blockquote":
                        RenderBlockquote(node, parent);
                        break;

                    case "br":
                        RenderLineBreak(parent);
                        break;

                    default:
                        // For unknown tags, render children
                        foreach (var child in node.ChildNodes)
                        {
                            RenderNode(child, parent);
                        }
                        break;
                }
            }
        }

        private void RenderHeading(HtmlNode node, Panel parent, string level)
        {
            var heading = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, level == "h1" ? 0 : level == "h2" ? 32 : 24, 0, 16)
            };

            // Set font size based on heading level
            double fontSize;
            switch (level)
            {
                case "h1":
                    fontSize = 32;
                    break;
                case "h2":
                    fontSize = 24;
                    break;
                case "h3":
                    fontSize = 20;
                    break;
                case "h4":
                    fontSize = 18;
                    break;
                case "h5":
                    fontSize = 16;
                    break;
                case "h6":
                    fontSize = 14;
                    break;
                default:
                    fontSize = 16;
                    break;
            }

            heading.FontSize = fontSize;
            heading.FontWeight = FontWeight.SemiBold;
            heading.Foreground = new SolidColorBrush(Color.FromRgb(36, 41, 46));

            // Add border for h1 and h2
            if (level == "h1")
            {
                heading.Margin = new Thickness(0, 0, 0, 16);
                // Border is handled by a separator line
            }
            else if (level == "h2")
            {
                heading.Margin = new Thickness(0, 32, 0, 16);
            }

            // Render children (text and inline elements)
            var inlinePanel = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var child in node.ChildNodes)
            {
                RenderInlineNode(child, inlinePanel);
            }

            // Extract text from inline panel
            var textBuilder = new StringBuilder();
            ExtractTextFromPanel(inlinePanel, textBuilder);
            heading.Text = textBuilder.ToString();

            parent.Children.Add(heading);

            // Add border line for h1 and h2
            if (level == "h1" || level == "h2")
            {
                var border = new Border
                {
                    Height = level == "h1" ? 2 : 1,
                    Background = new SolidColorBrush(Color.FromRgb(225, 228, 232)),
                    Margin = new Thickness(0, 0, 0, 16)
                };
                parent.Children.Add(border);
            }
        }

        private void RenderParagraph(HtmlNode node, Panel parent)
        {
            var paragraph = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16),
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
            };

            // Build Inlines collection for mixed formatting
            paragraph.Inlines.Clear();
            BuildInlinesFromNode(node, paragraph.Inlines);

            // If no inlines were created, use plain text as fallback
            if (paragraph.Inlines.Count == 0)
            {
                paragraph.Text = node.InnerText;
            }

            parent.Children.Add(paragraph);
        }

        private void BuildInlinesFromNode(HtmlNode node, InlineCollection inlines)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Text)
                {
                    string text = child.InnerText;
                    if (!string.IsNullOrEmpty(text))
                    {
                        inlines.Add(new Run { Text = text });
                    }
                }
                else if (child.NodeType == HtmlNodeType.Element)
                {
                    string tagName = child.Name.ToLowerInvariant();
                    Inline inline = null;

                    switch (tagName)
                    {
                        case "strong":
                        case "b":
                            var strongText = child.InnerText;
                            if (!string.IsNullOrEmpty(strongText))
                            {
                                inline = new Run
                                {
                                    Text = strongText,
                                    FontWeight = FontWeight.SemiBold,
                                    Foreground = new SolidColorBrush(Color.FromRgb(36, 41, 46))
                                };
                            }
                            break;

                        case "em":
                        case "i":
                            var emText = child.InnerText;
                            if (!string.IsNullOrEmpty(emText))
                            {
                                inline = new Run
                                {
                                    Text = emText,
                                    FontStyle = FontStyle.Italic
                                };
                            }
                            break;

                        case "code":
                            var codeText = child.InnerText;
                            if (!string.IsNullOrEmpty(codeText))
                            {
                                inline = new Run
                                {
                                    Text = codeText,
                                    FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                                    FontSize = 14.4,
                                    Foreground = new SolidColorBrush(Color.FromRgb(232, 62, 140)),
                                    Background = new SolidColorBrush(Color.FromRgb(246, 248, 250))
                                };
                            }
                            break;

                        case "a":
                            var linkText = child.InnerText;
                            if (!string.IsNullOrEmpty(linkText))
                            {
                                string linkHref = child.GetAttributeValue("href", "");
                                inline = new Run
                                {
                                    Text = linkText,
                                    Foreground = new SolidColorBrush(Color.FromRgb(3, 102, 214)),
                                    TextDecorations = TextDecorations.Underline
                                };
                                // Store href in Tag property for click handling
                                // Note: Inline Run elements don't support click events directly,
                                // but parent TextBlock can handle pointer events for styled text
                            }
                            break;

                        default:
                            // For other inline elements, recursively build inlines
                            BuildInlinesFromNode(child, inlines);
                            break;
                    }

                    if (inline != null)
                    {
                        inlines.Add(inline);
                    }
                }
            }
        }

        private void RenderList(HtmlNode node, Panel parent, bool isOrdered)
        {
            var listPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 16)
            };

            foreach (var child in node.ChildNodes)
            {
                if (child.Name.ToLowerInvariant() == "li")
                {
                    RenderListItem(child, listPanel);
                }
            }

            parent.Children.Add(listPanel);
        }

        private void RenderListItem(HtmlNode node, Panel parent)
        {
            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(32, 0, 0, 8)
            };

            // Bullet or number
            var marker = new TextBlock
            {
                Text = "• ",
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
            };
            itemPanel.Children.Add(marker);

            // Content
            var content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
            };

            // Build Inlines collection for mixed formatting
            content.Inlines.Clear();
            BuildInlinesFromNode(node, content.Inlines);

            // If no inlines were created, use plain text as fallback
            if (content.Inlines.Count == 0)
            {
                content.Text = node.InnerText;
            }

            itemPanel.Children.Add(content);
            parent.Children.Add(itemPanel);
        }

        private void RenderCode(HtmlNode node, Panel parent)
        {
            var code = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(246, 248, 250)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(2, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var textBlock = new TextBlock
            {
                Text = node.InnerText,
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = 14.4, // 0.9em of 16px
                Foreground = new SolidColorBrush(Color.FromRgb(232, 62, 140))
            };

            code.Child = textBlock;
            parent.Children.Add(code);
        }

        private void RenderPre(HtmlNode node, Panel parent)
        {
            var pre = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(246, 248, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(225, 228, 232)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 16, 0, 16)
            };

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            };

            var textBlock = new TextBlock
            {
                Text = node.InnerText,
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = 14.4,
                Foreground = new SolidColorBrush(Color.FromRgb(36, 41, 46)),
                TextWrapping = TextWrapping.NoWrap
            };

            scrollViewer.Content = textBlock;
            pre.Child = scrollViewer;
            parent.Children.Add(pre);
        }

        // Matching PyKotor implementation: QTextBrowser link handling with setSearchPaths
        // Original: self.text_browser.setSearchPaths([str(wiki_path)]) enables relative link resolution
        private void RenderLink(HtmlNode node, Panel parent)
        {
            var link = new TextBlock
            {
                Text = node.InnerText,
                Foreground = new SolidColorBrush(Color.FromRgb(3, 102, 214)),
                TextWrapping = TextWrapping.Wrap,
                Cursor = new Cursor(StandardCursorType.Hand),
                TextDecorations = TextDecorations.Underline
            };

            string href = node.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(href))
            {
                link.PointerPressed += (s, e) =>
                {
                    HandleLinkClick(href);
                };
            }

            parent.Children.Add(link);
        }

        // Handle link clicks - matching PyKotor QTextBrowser link navigation behavior
        // Supports: relative wiki file links, external URLs, and anchor links
        private void HandleLinkClick(string href)
        {
            if (string.IsNullOrEmpty(href))
            {
                return;
            }

            try
            {
                // Check if it's an external URL (http/https/mailto)
                if (href.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
                    href.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase) ||
                    href.StartsWith("mailto:", System.StringComparison.OrdinalIgnoreCase))
                {
                    // External link - open in default browser
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = href,
                        UseShellExecute = true
                    });
                    return;
                }

                // Check if it's an anchor link (starts with #)
                if (href.StartsWith("#", System.StringComparison.Ordinal))
                {
                    // Anchor link - scroll to anchor within current document
                    // TODO: STUB - Note: Full anchor scrolling would require more complex implementation
                    // TODO: STUB - For now, we'll just handle file links
                    return;
                }

                // Relative wiki file link - resolve and open in new dialog
                // Matching PyKotor behavior: QTextBrowser.setSearchPaths enables relative link resolution
                string wikiPath = GetWikiPath();
                string targetFile = href;

                // Remove leading ./ if present
                if (targetFile.StartsWith("./", System.StringComparison.Ordinal))
                {
                    targetFile = targetFile.Substring(2);
                }

                // Remove anchor part if present
                string anchor = null;
                if (targetFile.Contains("#"))
                {
                    var parts = targetFile.Split(new[] { '#' }, 2);
                    targetFile = parts[0];
                    anchor = parts[1];
                }

                // Resolve wiki file path
                string filePath = Path.Combine(wikiPath, targetFile);

                // Try with .md extension if file doesn't exist
                if (!File.Exists(filePath) && !targetFile.EndsWith(".md", System.StringComparison.OrdinalIgnoreCase))
                {
                    filePath = Path.Combine(wikiPath, targetFile + ".md");
                }

                // Check if file exists in wiki directory
                if (File.Exists(filePath))
                {
                    // Get relative filename for dialog title
                    string wikiFilename = Path.GetFileName(filePath);

                    // Open in new EditorHelpDialog (matching PyKotor behavior)
                    var helpDialog = new EditorHelpDialog(this, new[] { wikiFilename });
                    helpDialog.Show();
                }
                else
                {
                    // File not found - try as external link or show error
                    // For robustness, try opening as-is (might be a file:// URL or other scheme)
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = href,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // Link cannot be resolved - silently fail (matching QTextBrowser behavior)
                    }
                }
            }
            catch
            {
                // Ignore errors opening links (matching PyKotor QTextBrowser behavior)
            }
        }

        private void RenderStrong(HtmlNode node, Panel parent)
        {
            var strong = new TextBlock
            {
                Text = node.InnerText,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(36, 41, 46)),
                TextWrapping = TextWrapping.Wrap
            };
            parent.Children.Add(strong);
        }

        private void RenderEmphasis(HtmlNode node, Panel parent)
        {
            var emphasis = new TextBlock
            {
                Text = node.InnerText,
                FontStyle = FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap
            };
            parent.Children.Add(emphasis);
        }

        private void RenderTable(HtmlNode node, Panel parent)
        {
            var tablePanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 24, 0, 24)
            };

            var thead = node.SelectSingleNode(".//thead");
            var tbody = node.SelectSingleNode(".//tbody") ?? node;

            // Render header if exists
            if (thead != null)
            {
                var headerRow = thead.SelectSingleNode(".//tr");
                if (headerRow != null)
                {
                    var headerPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Background = new SolidColorBrush(Color.FromRgb(246, 248, 250))
                    };

                    foreach (var th in headerRow.SelectNodes(".//th"))
                    {
                        var cell = new Border
                        {
                            BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 218)),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(12, 12, 16, 12),
                            Child = new TextBlock
                            {
                                Text = th.InnerText,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.FromRgb(36, 41, 46)),
                                TextWrapping = TextWrapping.Wrap
                            }
                        };
                        headerPanel.Children.Add(cell);
                    }

                    tablePanel.Children.Add(headerPanel);
                }
            }

            // Render body rows
            int rowIndex = 0;
            foreach (var tr in tbody.SelectNodes(".//tr"))
            {
                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal
                };

                if (rowIndex % 2 == 1)
                {
                    rowPanel.Background = new SolidColorBrush(Color.FromRgb(249, 250, 251));
                }

                foreach (var td in tr.SelectNodes(".//td"))
                {
                    var cell = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 218)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(12, 12, 16, 12),
                        Child = new TextBlock
                        {
                            Text = td.InnerText,
                            Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                            TextWrapping = TextWrapping.Wrap
                        }
                    };
                    rowPanel.Children.Add(cell);
                }

                tablePanel.Children.Add(rowPanel);
                rowIndex++;
            }

            // Wrap in scroll viewer for horizontal scrolling
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = tablePanel
            };

            parent.Children.Add(scrollViewer);
        }

        private void RenderHorizontalRule(Panel parent)
        {
            var hr = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(225, 228, 232)),
                Margin = new Thickness(0, 24, 0, 24)
            };
            parent.Children.Add(hr);
        }

        private void RenderBlockquote(HtmlNode node, Panel parent)
        {
            var blockquote = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 226, 229)),
                BorderThickness = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(16, 0, 0, 0),
                Margin = new Thickness(0, 16, 0, 16)
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            foreach (var child in node.ChildNodes)
            {
                RenderNode(child, content);
            }

            blockquote.Child = content;
            parent.Children.Add(blockquote);
        }

        private void RenderLineBreak(Panel parent)
        {
            var br = new TextBlock
            {
                Text = "\n",
                Height = 16
            };
            parent.Children.Add(br);
        }

        private void RenderInlineNode(HtmlNode node, Panel parent)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var text = new TextBlock
                {
                    Text = node.InnerText,
                    TextWrapping = TextWrapping.Wrap
                };
                parent.Children.Add(text);
            }
            else if (node.NodeType == HtmlNodeType.Element)
            {
                string tagName = node.Name.ToLowerInvariant();
                switch (tagName)
                {
                    case "strong":
                    case "b":
                        var strong = new TextBlock
                        {
                            Text = node.InnerText,
                            FontWeight = FontWeight.SemiBold
                        };
                        parent.Children.Add(strong);
                        break;

                    case "em":
                    case "i":
                        var em = new TextBlock
                        {
                            Text = node.InnerText,
                            FontStyle = FontStyle.Italic
                        };
                        parent.Children.Add(em);
                        break;

                    case "code":
                        RenderCode(node, parent);
                        break;

                    case "a":
                        RenderLink(node, parent);
                        break;

                    default:
                        foreach (var child in node.ChildNodes)
                        {
                            RenderInlineNode(child, parent);
                        }
                        break;
                }
            }
        }

        private void ExtractTextFromNode(HtmlNode node, StringBuilder builder)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                builder.Append(node.InnerText);
            }
            else
            {
                foreach (var child in node.ChildNodes)
                {
                    ExtractTextFromNode(child, builder);
                }
            }
        }

        private void ExtractTextFromPanel(Panel panel, StringBuilder builder)
        {
            foreach (var child in panel.Children)
            {
                if (child is TextBlock textBlock)
                {
                    builder.Append(textBlock.Text);
                }
                else if (child is Panel childPanel)
                {
                    ExtractTextFromPanel(childPanel, builder);
                }
            }
        }

        private void ApplyTextStyles(HtmlNode node, TextBlock textBlock)
        {
            // Apply styles based on parent nodes
            var parent = node.ParentNode;
            while (parent != null)
            {
                string tagName = parent.Name.ToLowerInvariant();
                switch (tagName)
                {
                    case "strong":
                    case "b":
                        textBlock.FontWeight = FontWeight.SemiBold;
                        break;

                    case "em":
                    case "i":
                        textBlock.FontStyle = FontStyle.Italic;
                        break;

                    case "code":
                        textBlock.FontFamily = new FontFamily("Consolas, 'Courier New', monospace");
                        textBlock.Foreground = new SolidColorBrush(Color.FromRgb(232, 62, 140));
                        break;
                }
                parent = parent.ParentNode;
            }
        }

    }
}

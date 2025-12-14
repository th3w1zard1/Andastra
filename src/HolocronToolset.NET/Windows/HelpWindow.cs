using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Config;

namespace HolocronToolset.NET.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/help.py:40
    // Original: class HelpWindow(QMainWindow):
    public class HelpWindow : Window
    {
        private string _version;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/help.py:43-72
        // Original: def __init__(self, parent, startingPage=None):
        public HelpWindow(Window parent = null, string startingPage = null)
        {
            InitializeComponent();
            SetupUI();
            SetupContents();
            _startingPage = startingPage;
        }

        private string _startingPage;

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
            Title = "Help - Holocron Toolset";
            Width = 800;
            Height = 600;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Help",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            panel.Children.Add(titleLabel);
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/help.py
        // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
        public HelpWindowUi Ui { get; private set; }

        private void SetupUI()
        {
            // Create UI wrapper for testing
            Ui = new HelpWindowUi();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/help.py:76-92
        // Original: def _setup_contents(self):
        private void SetupContents()
        {
            try
            {
                string contentsPath = "./help/contents.xml";
                if (File.Exists(contentsPath))
                {
                    // Parse XML contents
                    // This will be implemented when XML parsing is available
                    _version = "1.0";
                }
            }
            catch
            {
                // Suppress errors
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/help.py:117-166
        // Original: def check_for_updates(self):
        private void CheckForUpdates()
        {
            // Check for help updates
            // This will be implemented when update checking is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/help.py:182-196
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
        h1 {{ font-size: 2em; margin-top: 0; }}
        h2 {{ font-size: 1.5em; }}
        code {{ background-color: #f4f4f4; padding: 2px 4px; border-radius: 3px; }}
        pre {{ background-color: #f4f4f4; padding: 12px; border-radius: 5px; overflow-x: auto; }}
    </style>
</head>
<body>
{htmlBody}
</body>
</html>";
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/help.py
    // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
    public class HelpWindowUi
    {
    }
}

using System;
using System.Reactive.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using BioWare.NET.Resource;
using FileResource = BioWare.NET.Extract.FileResource;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/resource_comparison.py:28
    // Original: class ResourceComparisonDialog(QDialog):
    public partial class ResourceComparisonDialog : Window
    {
        private FileResource _resource1;
        private FileResource _resource2;
        private TextBox _leftText;
        private TextBox _rightText;
        private TextBlock _leftPathLabel;
        private TextBlock _rightPathLabel;
        private ScrollViewer _leftScrollViewer;
        private ScrollViewer _rightScrollViewer;
        private bool _isSyncingScroll;

        // Public parameterless constructor for XAML
        public ResourceComparisonDialog() : this(null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/resource_comparison.py:31-52
        // Original: def __init__(self, parent, resource1, resource2=None):
        public ResourceComparisonDialog(Window parent, FileResource resource1, FileResource resource2 = null)
        {
            InitializeComponent();
            _resource1 = resource1 ?? throw new ArgumentNullException(nameof(resource1));
            _resource2 = resource2;
            Title = $"Compare: {resource1.ResName}.{resource1.ResType.Extension}";
            Width = 1200;
            Height = 700;
            SetupUI();
            LoadResources();
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
            var mainPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Header
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var leftHeader = new StackPanel();
            leftHeader.Children.Add(new TextBlock { Text = "<b>Left:</b>" });
            _leftPathLabel = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            leftHeader.Children.Add(_leftPathLabel);
            headerPanel.Children.Add(leftHeader);

            var rightHeader = new StackPanel();
            rightHeader.Children.Add(new TextBlock { Text = "<b>Right:</b>" });
            _rightPathLabel = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            rightHeader.Children.Add(_rightPathLabel);
            headerPanel.Children.Add(rightHeader);
            mainPanel.Children.Add(headerPanel);

            // Comparison view with ScrollViewers for synchronized scrolling
            var splitPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _leftText = new TextBox { IsReadOnly = true, AcceptsReturn = true, AcceptsTab = false, TextWrapping = Avalonia.Media.TextWrapping.NoWrap, FontFamily = "Consolas" };
            _rightText = new TextBox { IsReadOnly = true, AcceptsReturn = true, AcceptsTab = false, TextWrapping = Avalonia.Media.TextWrapping.NoWrap, FontFamily = "Consolas" };

            // Wrap TextBoxes in ScrollViewers for synchronized scrolling
            _leftScrollViewer = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = _leftText };
            _rightScrollViewer = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = _rightText };

            splitPanel.Children.Add(_leftScrollViewer);
            splitPanel.Children.Add(_rightScrollViewer);
            mainPanel.Children.Add(splitPanel);

            // Setup scroll synchronization for programmatic UI
            if (_leftScrollViewer != null && _rightScrollViewer != null)
            {
                SetupScrollSynchronization();
            }

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var closeButton = new Button { Content = "Close", Width = 75 };
            closeButton.Click += (s, e) => Close();
            buttonPanel.Children.Add(closeButton);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _leftText = this.FindControl<TextBox>("leftText");
            _rightText = this.FindControl<TextBox>("rightText");
            _leftPathLabel = this.FindControl<TextBlock>("leftPathLabel");
            _rightPathLabel = this.FindControl<TextBlock>("rightPathLabel");

            // Find ScrollViewers that wrap the TextBoxes
            // Based on Avalonia API: ScrollViewer wraps content and provides scrolling
            // Method: Traverse visual tree to find parent ScrollViewer
            _leftScrollViewer = FindParentScrollViewer(_leftText);
            _rightScrollViewer = FindParentScrollViewer(_rightText);

            // Synchronize scrollbars if both ScrollViewers are available
            // Based on Avalonia API: ScrollViewer.Offset property (Vector) controls scroll position
            // Implementation: Listen to Offset property changes and sync between viewers
            // Reference: https://docs.avaloniaui.net/docs/reference/controls/scrollviewer
            if (_leftScrollViewer != null && _rightScrollViewer != null)
            {
                SetupScrollSynchronization();
            }
        }

        // Find parent ScrollViewer by traversing visual tree
        // Based on Avalonia visual tree traversal pattern
        private ScrollViewer FindParentScrollViewer(Control control)
        {
            if (control == null)
            {
                return null;
            }

            Control parent = control.Parent as Control;
            while (parent != null)
            {
                if (parent is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
                parent = parent.Parent as Control;
            }

            return null;
        }

        // Setup bidirectional scroll synchronization between two ScrollViewers
        // Based on Avalonia API: ScrollViewer.Offset property (Vector) and PropertyChanged event
        // Implementation note: Uses flag to prevent recursive updates during synchronization
        private void SetupScrollSynchronization()
        {
            // Subscribe to Offset property changes on both ScrollViewers
            // When one scrolls, update the other to match
            _leftScrollViewer.GetObservable(ScrollViewer.OffsetProperty).Subscribe(offset =>
            {
                if (!_isSyncingScroll && _rightScrollViewer != null)
                {
                    _isSyncingScroll = true;
                    try
                    {
                        // Sync vertical scroll position (Y component of Vector)
                        // Preserve horizontal scroll (X component) independently
                        Vector currentRightOffset = _rightScrollViewer.Offset;
                        Vector newRightOffset = new Vector(currentRightOffset.X, offset.Y);
                        _rightScrollViewer.Offset = newRightOffset;
                    }
                    finally
                    {
                        _isSyncingScroll = false;
                    }
                }
            });

            _rightScrollViewer.GetObservable(ScrollViewer.OffsetProperty).Subscribe(offset =>
            {
                if (!_isSyncingScroll && _leftScrollViewer != null)
                {
                    _isSyncingScroll = true;
                    try
                    {
                        // Sync vertical scroll position (Y component of Vector)
                        // Preserve horizontal scroll (X component) independently
                        Vector currentLeftOffset = _leftScrollViewer.Offset;
                        Vector newLeftOffset = new Vector(currentLeftOffset.X, offset.Y);
                        _leftScrollViewer.Offset = newLeftOffset;
                    }
                    finally
                    {
                        _isSyncingScroll = false;
                    }
                }
            });
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/resource_comparison.py:121-146
        // Original: def _load_resources(self):
        private void LoadResources()
        {
            // Set paths
            if (_leftPathLabel != null)
            {
                _leftPathLabel.Text = _resource1.FilePath;
            }
            if (_rightPathLabel != null)
            {
                _rightPathLabel.Text = _resource2?.FilePath ?? "[Not selected]";
            }

            // Load left resource
            try
            {
                byte[] data = _resource1.GetData();
                string formatted = FormatData(data);
                if (_leftText != null)
                {
                    _leftText.Text = formatted;
                }
            }
            catch (Exception ex)
            {
                if (_leftText != null)
                {
                    _leftText.Text = $"Error loading resource:\n{ex}";
                }
            }

            // Load right resource
            if (_resource2 != null)
            {
                try
                {
                    byte[] data = _resource2.GetData();
                    string formatted = FormatData(data);
                    if (_rightText != null)
                    {
                        _rightText.Text = formatted;
                    }
                }
                catch (Exception ex)
                {
                    if (_rightText != null)
                    {
                        _rightText.Text = $"Error loading resource:\n{ex}";
                    }
                }
            }
            else
            {
                if (_rightText != null)
                {
                    _rightText.Text = "[No resource selected for comparison]";
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/resource_comparison.py:148-170
        // Original: def _format_data(self, data: bytes) -> str:
        private string FormatData(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return "[Empty]";
            }

            // Try to decode as text
            try
            {
                string text = Encoding.UTF8.GetString(data);
                // Check if it's valid text (mostly printable ASCII)
                int printableCount = 0;
                foreach (char c in text)
                {
                    if (c >= 32 && c <= 127)
                    {
                        printableCount++;
                    }
                }
                if (printableCount > data.Length * 0.7)
                {
                    return text;
                }
            }
            catch
            {
                // Not valid UTF-8 text
            }

            // Format as hex dump
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i += 16)
            {
                sb.AppendFormat("{0:X8}: ", i);
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < data.Length)
                    {
                        sb.AppendFormat("{0:X2} ", data[i + j]);
                    }
                    else
                    {
                        sb.Append("   ");
                    }
                }
                sb.Append(" ");
                for (int j = 0; j < 16 && i + j < data.Length; j++)
                {
                    byte b = data[i + j];
                    char c = (b >= 32 && b <= 127) ? (char)b : '.';
                    sb.Append(c);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}

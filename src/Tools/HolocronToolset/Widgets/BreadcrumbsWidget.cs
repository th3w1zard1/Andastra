using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/breadcrumbs_widget.py:14
    // Original: class BreadcrumbsWidget(QWidget):
    public partial class BreadcrumbsWidget : UserControl
    {
        private List<string> _path;
        private string _separator;
        private StackPanel _layout;
        private List<Button> _buttons;

        public event Action<string> ItemClicked;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/breadcrumbs_widget.py:19-23
        // Original: def __init__(self, parent: QWidget | None = None):
        public BreadcrumbsWidget()
        {
            InitializeComponent();
            _path = new List<string>();
            _separator = " > ";
            _buttons = new List<Button>();
            SetupUI();
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
            _layout = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Avalonia.Thickness(4, 2, 4, 2),
                Spacing = 2
            };
            Content = _layout;
        }

        private void SetupUI()
        {
            // If already set up programmatically, skip
            if (_layout != null)
            {
                return;
            }

            // Find controls from XAML (may fail if not in visual tree)
            try
            {
                _layout = this.FindControl<StackPanel>("layout");
            }
            catch (InvalidOperationException)
            {
                // Not in a visual tree (e.g., in unit tests) - will create programmatically
                _layout = null;
            }

            if (_layout == null)
            {
                _layout = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Margin = new Avalonia.Thickness(4, 2, 4, 2),
                    Spacing = 2
                };
                Content = _layout;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/breadcrumbs_widget.py:53-61
        // Original: def set_path(self, path: list[str]):
        public void SetPath(List<string> path)
        {
            _path = path ?? new List<string>();
            UpdateDisplay();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/breadcrumbs_widget.py:63-90
        // Original: def _update_display(self):
        private void UpdateDisplay()
        {
            if (_layout == null)
            {
                return;
            }

            _layout.Children.Clear();
            _buttons.Clear();

            for (int i = 0; i < _path.Count; i++)
            {
                if (i > 0)
                {
                    var separator = new TextBlock { Text = _separator };
                    _layout.Children.Add(separator);
                }

                int index = i; // Capture for closure
                var button = new Button
                {
                    Content = _path[i],
                    Background = Avalonia.Media.Brushes.Transparent
                };
                button.Click += (s, e) => OnSegmentClicked(index);
                _layout.Children.Add(button);
                _buttons.Add(button);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/breadcrumbs_widget.py:92-95
        // Original: def _on_segment_clicked(self, index: int):
        private void OnSegmentClicked(int index)
        {
            if (index >= 0 && index < _path.Count)
            {
                ItemClicked?.Invoke(_path[index]);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/breadcrumbs_widget.py:97-99
        // Original: def clear(self):
        public void Clear()
        {
            SetPath(new List<string>());
        }

        // Public property to expose path for testing
        public List<string> Path => _path ?? new List<string>();
    }
}

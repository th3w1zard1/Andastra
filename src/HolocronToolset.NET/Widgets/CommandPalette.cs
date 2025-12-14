using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.NET.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/command_palette.py:25
    // Original: class CommandPalette(QDialog):
    public partial class CommandPalette : Window
    {
        private Dictionary<string, Dictionary<string, object>> _commands;
        private ListBox _commandList;
        private TextBox _searchEdit;
        private TextBlock _statusLabel;
        private List<string> _filteredCommandIds;

        public event Action<string> CommandSelected;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/command_palette.py:30-41
        // Original: def __init__(self, parent: QWidget | None = None):
        public CommandPalette(Window parent = null)
        {
            InitializeComponent();
            _commands = new Dictionary<string, Dictionary<string, object>>();
            _filteredCommandIds = new List<string>();
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
            Title = "Command Palette";
            MinWidth = 500;
            MaxWidth = 700;
            Width = 600;
            Height = 400;

            var panel = new StackPanel { Margin = new Avalonia.Thickness(0), Spacing = 0 };

            _searchEdit = new TextBox
            {
                Watermark = "Type to search commands...",
                Margin = new Avalonia.Thickness(8)
            };
            _searchEdit.TextChanged += (s, e) => FilterCommands();
            _searchEdit.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ExecuteSelected();
                }
            };

            _commandList = new ListBox();
            _commandList.DoubleTapped += (s, e) => OnItemDoubleClicked();
            _commandList.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ExecuteSelected();
                }
            };

            _statusLabel = new TextBlock
            {
                Text = "",
                Margin = new Avalonia.Thickness(4, 8, 4, 8)
            };

            panel.Children.Add(_searchEdit);
            panel.Children.Add(_commandList);
            panel.Children.Add(_statusLabel);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _searchEdit = this.FindControl<TextBox>("searchEdit");
            _commandList = this.FindControl<ListBox>("commandList");
            _statusLabel = this.FindControl<TextBlock>("statusLabel");

            if (_searchEdit != null)
            {
                _searchEdit.TextChanged += (s, e) => FilterCommands();
                _searchEdit.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        ExecuteSelected();
                    }
                };
            }
            if (_commandList != null)
            {
                _commandList.DoubleTapped += (s, e) => OnItemDoubleClicked();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/command_palette.py:102-120
        // Original: def register_command(self, command_id, label, callback, category=None):
        public void RegisterCommand(string commandId, string label, Action callback, string category = null)
        {
            _commands[commandId] = new Dictionary<string, object>
            {
                { "label", label },
                { "callback", callback },
                { "category", category ?? "" }
            };
            FilterCommands();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/command_palette.py:122-140
        // Original: def _filter_commands(self):
        private void FilterCommands()
        {
            if (_commandList == null || _searchEdit == null)
            {
                return;
            }

            string searchText = _searchEdit.Text?.ToLowerInvariant() ?? "";
            _filteredCommandIds.Clear();
            _commandList.Items.Clear();

            foreach (var kvp in _commands)
            {
                string label = kvp.Value.ContainsKey("label") ? kvp.Value["label"]?.ToString() ?? "" : "";
                if (string.IsNullOrEmpty(searchText) || label.ToLowerInvariant().Contains(searchText))
                {
                    _filteredCommandIds.Add(kvp.Key);
                    _commandList.Items.Add(label);
                }
            }

            if (_statusLabel != null)
            {
                _statusLabel.Text = $"{_filteredCommandIds.Count} command(s)";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/command_palette.py:142-150
        // Original: def _execute_selected(self):
        private void ExecuteSelected()
        {
            if (_commandList == null)
            {
                return;
            }

            int selectedIndex = _commandList.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _filteredCommandIds.Count)
            {
                string commandId = _filteredCommandIds[selectedIndex];
                CommandSelected?.Invoke(commandId);
                if (_commands.ContainsKey(commandId) && _commands[commandId].ContainsKey("callback"))
                {
                    var callback = _commands[commandId]["callback"] as Action;
                    callback?.Invoke();
                }
                Close();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/command_palette.py:152-155
        // Original: def _on_item_double_clicked(self, item):
        private void OnItemDoubleClicked()
        {
            ExecuteSelected();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/command_palette.py:157-160
        // Original: def show_palette(self):
        public void ShowPalette()
        {
            Show();
            if (_searchEdit != null)
            {
                _searchEdit.Focus();
            }
        }
    }
}

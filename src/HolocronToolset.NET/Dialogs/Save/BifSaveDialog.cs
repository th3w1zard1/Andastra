using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.NET.Dialogs.Save
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_bif.py:13
    // Original: class BifSaveOption(IntEnum):
    public enum BifSaveOption
    {
        Nothing = 0,
        MOD = 1,
        Override = 2
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_bif.py:19
    // Original: class BifSaveDialog(QDialog):
    public partial class BifSaveDialog : Window
    {
        private BifSaveOption _option = BifSaveOption.Nothing;
        private Button _modSaveButton;
        private Button _overrideSaveButton;
        private Button _cancelButton;

        // Public parameterless constructor for XAML
        public BifSaveDialog() : this(null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_bif.py:20-46
        // Original: def __init__(self, parent):
        public BifSaveDialog(Window parent)
        {
            InitializeComponent();
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
            Title = "Save in BIF";
            Width = 300;
            Height = 150;

            var panel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };
            _modSaveButton = new Button { Content = "Save as MOD" };
            _modSaveButton.Click += (s, e) => SaveAsMod();
            _overrideSaveButton = new Button { Content = "Save as Override" };
            _overrideSaveButton.Click += (s, e) => SaveAsOverride();
            _cancelButton = new Button { Content = "Cancel" };
            _cancelButton.Click += (s, e) => Close();

            panel.Children.Add(_modSaveButton);
            panel.Children.Add(_overrideSaveButton);
            panel.Children.Add(_cancelButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _modSaveButton = this.FindControl<Button>("modSaveButton");
            _overrideSaveButton = this.FindControl<Button>("overrideSaveButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_modSaveButton != null)
            {
                _modSaveButton.Click += (s, e) => SaveAsMod();
            }
            if (_overrideSaveButton != null)
            {
                _overrideSaveButton.Click += (s, e) => SaveAsOverride();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_bif.py:48-50
        // Original: def save_as_mod(self):
        private void SaveAsMod()
        {
            _option = BifSaveOption.MOD;
            Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_bif.py:52-54
        // Original: def save_as_override(self):
        private void SaveAsOverride()
        {
            _option = BifSaveOption.Override;
            Close();
        }

        public BifSaveOption Option => _option;
    }
}

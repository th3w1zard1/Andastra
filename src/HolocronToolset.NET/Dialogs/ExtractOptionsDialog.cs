using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.NET.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/extract_options.py:8
    // Original: class ExtractOptionsDialog(QDialog):
    public partial class ExtractOptionsDialog : Window
    {
        private bool _tpcDecompile;
        private bool _tpcExtractTxi;
        private bool _mdlDecompile;
        private bool _mdlExtractTextures;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/extract_options.py:11-40
        // Original: def __init__(self, parent=None):
        public ExtractOptionsDialog(Window parent = null)
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
            Title = "Extract Options";
            Width = 400;
            Height = 300;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Extract Options",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            var okButton = new Button { Content = "OK" };
            okButton.Click += (sender, e) => Close();

            panel.Children.Add(titleLabel);
            panel.Children.Add(okButton);
            Content = panel;
        }

        private CheckBox _tpcDecompileCheckbox;
        private CheckBox _tpcTxiCheckbox;
        private CheckBox _mdlDecompileCheckbox;
        private CheckBox _mdlTexturesCheckbox;
        private Button _okButton;
        private Button _cancelButton;

        private void SetupUI()
        {
            // Find controls from XAML
            _tpcDecompileCheckbox = this.FindControl<CheckBox>("tpcDecompileCheckbox");
            _tpcTxiCheckbox = this.FindControl<CheckBox>("tpcTxiCheckbox");
            _mdlDecompileCheckbox = this.FindControl<CheckBox>("mdlDecompileCheckbox");
            _mdlTexturesCheckbox = this.FindControl<CheckBox>("mdlTexturesCheckbox");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_okButton != null)
            {
                _okButton.Click += (s, e) => { UpdateValues(); Close(); };
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }

            // Sync checkbox states with properties
            if (_tpcDecompileCheckbox != null)
            {
                _tpcDecompileCheckbox.IsCheckedChanged += (s, e) => _tpcDecompile = _tpcDecompileCheckbox.IsChecked ?? false;
            }
            if (_tpcTxiCheckbox != null)
            {
                _tpcTxiCheckbox.IsCheckedChanged += (s, e) => _tpcExtractTxi = _tpcTxiCheckbox.IsChecked ?? false;
            }
            if (_mdlDecompileCheckbox != null)
            {
                _mdlDecompileCheckbox.IsCheckedChanged += (s, e) => _mdlDecompile = _mdlDecompileCheckbox.IsChecked ?? false;
            }
            if (_mdlTexturesCheckbox != null)
            {
                _mdlTexturesCheckbox.IsCheckedChanged += (s, e) => _mdlExtractTextures = _mdlTexturesCheckbox.IsChecked ?? false;
            }
        }

        private void UpdateValues()
        {
            if (_tpcDecompileCheckbox != null)
            {
                _tpcDecompile = _tpcDecompileCheckbox.IsChecked ?? false;
            }
            if (_tpcTxiCheckbox != null)
            {
                _tpcExtractTxi = _tpcTxiCheckbox.IsChecked ?? false;
            }
            if (_mdlDecompileCheckbox != null)
            {
                _mdlDecompile = _mdlDecompileCheckbox.IsChecked ?? false;
            }
            if (_mdlTexturesCheckbox != null)
            {
                _mdlExtractTextures = _mdlTexturesCheckbox.IsChecked ?? false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/extract_options.py:22-40
        // Original: @property def tpc_decompile(self) -> bool:
        public bool TpcDecompile
        {
            get => _tpcDecompile;
            set => _tpcDecompile = value;
        }

        public bool TpcExtractTxi
        {
            get => _tpcExtractTxi;
            set => _tpcExtractTxi = value;
        }

        public bool MdlDecompile
        {
            get => _mdlDecompile;
            set => _mdlDecompile = value;
        }

        public bool MdlExtractTextures
        {
            get => _mdlExtractTextures;
            set => _mdlExtractTextures = value;
        }
    }
}

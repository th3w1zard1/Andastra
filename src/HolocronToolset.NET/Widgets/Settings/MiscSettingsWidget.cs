using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/misc.py:15
    // Original: class MiscWidget(QWidget):
    public partial class MiscSettingsWidget : UserControl
    {
        private GlobalSettings _settings;
        private CheckBox _useBetaChannel;
        private CheckBox _attemptKeepOldGFFFields;
        private CheckBox _saveRimCheck;
        private CheckBox _mergeRimCheck;
        private ComboBox _moduleSortOptionComboBox;
        private CheckBox _greyRimCheck;
        private CheckBox _showPreviewUTCCheck;
        private CheckBox _showPreviewUTPCheck;
        private CheckBox _showPreviewUTDCheck;
        private TextBox _tempDirEdit;
        private ComboBox _gffEditorCombo;
        private TextBox _ncsToolEdit;
        private TextBox _nssCompEdit;

        public MiscSettingsWidget()
        {
            InitializeComponent();
            _settings = new GlobalSettings();
            SetupUI();
            SetupValues();
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
            var panel = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

            _useBetaChannel = new CheckBox { Content = "Use Beta Channel" };
            _attemptKeepOldGFFFields = new CheckBox { Content = "Attempt Keep Old GFF Fields" };
            _saveRimCheck = new CheckBox { Content = "Save RIM" };
            _mergeRimCheck = new CheckBox { Content = "Merge RIM" };
            _greyRimCheck = new CheckBox { Content = "Grey RIM" };
            _showPreviewUTCCheck = new CheckBox { Content = "Show Preview UTC" };
            _showPreviewUTPCheck = new CheckBox { Content = "Show Preview UTP" };
            _showPreviewUTDCheck = new CheckBox { Content = "Show Preview UTD" };

            _moduleSortOptionComboBox = new ComboBox();
            _moduleSortOptionComboBox.Items.Add("Name");
            _moduleSortOptionComboBox.Items.Add("Date");

            _tempDirEdit = new TextBox { Watermark = "Extract Path" };
            _gffEditorCombo = new ComboBox();
            _gffEditorCombo.Items.Add("Standard");
            _gffEditorCombo.Items.Add("Specialized");
            _ncsToolEdit = new TextBox { Watermark = "NCS Decompiler Path" };
            _nssCompEdit = new TextBox { Watermark = "NSS Compiler Path" };

            panel.Children.Add(_useBetaChannel);
            panel.Children.Add(_attemptKeepOldGFFFields);
            panel.Children.Add(_saveRimCheck);
            panel.Children.Add(_mergeRimCheck);
            panel.Children.Add(_greyRimCheck);
            panel.Children.Add(_showPreviewUTCCheck);
            panel.Children.Add(_showPreviewUTPCheck);
            panel.Children.Add(_showPreviewUTDCheck);
            panel.Children.Add(new TextBlock { Text = "Module Sort Option:" });
            panel.Children.Add(_moduleSortOptionComboBox);
            panel.Children.Add(new TextBlock { Text = "Extract Path:" });
            panel.Children.Add(_tempDirEdit);
            panel.Children.Add(new TextBlock { Text = "GFF Editor:" });
            panel.Children.Add(_gffEditorCombo);
            panel.Children.Add(new TextBlock { Text = "NCS Tool:" });
            panel.Children.Add(_ncsToolEdit);
            panel.Children.Add(new TextBlock { Text = "NSS Compiler:" });
            panel.Children.Add(_nssCompEdit);

            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _useBetaChannel = this.FindControl<CheckBox>("useBetaChannel");
            _attemptKeepOldGFFFields = this.FindControl<CheckBox>("attemptKeepOldGFFFields");
            _saveRimCheck = this.FindControl<CheckBox>("saveRimCheck");
            _mergeRimCheck = this.FindControl<CheckBox>("mergeRimCheck");
            _moduleSortOptionComboBox = this.FindControl<ComboBox>("moduleSortOptionComboBox");
            _greyRimCheck = this.FindControl<CheckBox>("greyRimCheck");
            _showPreviewUTCCheck = this.FindControl<CheckBox>("showPreviewUTCCheck");
            _showPreviewUTPCheck = this.FindControl<CheckBox>("showPreviewUTPCheck");
            _showPreviewUTDCheck = this.FindControl<CheckBox>("showPreviewUTDCheck");
            _tempDirEdit = this.FindControl<TextBox>("tempDirEdit");
            _gffEditorCombo = this.FindControl<ComboBox>("gffEditorCombo");
            _ncsToolEdit = this.FindControl<TextBox>("ncsToolEdit");
            _nssCompEdit = this.FindControl<TextBox>("nssCompEdit");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/misc.py:52-65
        // Original: def setup_values(self):
        private void SetupValues()
        {
            if (_useBetaChannel != null)
            {
                _useBetaChannel.IsChecked = _settings.UseBetaChannel;
            }
            if (_attemptKeepOldGFFFields != null)
            {
                _attemptKeepOldGFFFields.IsChecked = _settings.GetValue<bool>("AttemptKeepOldGFFFields", false);
            }
            if (_saveRimCheck != null)
            {
                _saveRimCheck.IsChecked = !_settings.GetValue<bool>("DisableRIMSaving", false);
            }
            if (_mergeRimCheck != null)
            {
                _mergeRimCheck.IsChecked = _settings.JoinRIMsTogether;
            }
            if (_moduleSortOptionComboBox != null)
            {
                _moduleSortOptionComboBox.SelectedIndex = _settings.GetValue<int>("ModuleSortOption", 0);
            }
            if (_greyRimCheck != null)
            {
                _greyRimCheck.IsChecked = _settings.GetValue<bool>("GreyRIMText", false);
            }
            if (_showPreviewUTCCheck != null)
            {
                _showPreviewUTCCheck.IsChecked = _settings.GetValue<bool>("ShowPreviewUTC", false);
            }
            if (_showPreviewUTPCheck != null)
            {
                _showPreviewUTPCheck.IsChecked = _settings.GetValue<bool>("ShowPreviewUTP", false);
            }
            if (_showPreviewUTDCheck != null)
            {
                _showPreviewUTDCheck.IsChecked = _settings.GetValue<bool>("ShowPreviewUTD", false);
            }
            if (_tempDirEdit != null)
            {
                _tempDirEdit.Text = _settings.ExtractPath;
            }
            if (_gffEditorCombo != null)
            {
                _gffEditorCombo.SelectedIndex = _settings.GetGffSpecializedEditors() ? 1 : 0;
            }
            if (_ncsToolEdit != null)
            {
                _ncsToolEdit.Text = _settings.NcsDecompilerPath;
            }
            if (_nssCompEdit != null)
            {
                _nssCompEdit.Text = _settings.NssCompilerPath;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/misc.py:67-80
        // Original: def save(self):
        public void Save()
        {
            if (_useBetaChannel != null)
            {
                _settings.UseBetaChannel = _useBetaChannel.IsChecked ?? false;
                _settings.SetValue("UseBetaChannel", _settings.UseBetaChannel);
            }
            if (_attemptKeepOldGFFFields != null)
            {
                _settings.SetValue("AttemptKeepOldGFFFields", _attemptKeepOldGFFFields.IsChecked ?? false);
            }
            if (_saveRimCheck != null)
            {
                _settings.SetValue("DisableRIMSaving", !(_saveRimCheck.IsChecked ?? false));
            }
            if (_mergeRimCheck != null)
            {
                _settings.JoinRIMsTogether = _mergeRimCheck.IsChecked ?? false;
                _settings.SetValue("JoinRIMsTogether", _settings.JoinRIMsTogether);
            }
            if (_moduleSortOptionComboBox != null)
            {
                _settings.SetValue("ModuleSortOption", _moduleSortOptionComboBox.SelectedIndex);
            }
            if (_greyRimCheck != null)
            {
                _settings.SetValue("GreyRIMText", _greyRimCheck.IsChecked ?? false);
            }
            if (_showPreviewUTCCheck != null)
            {
                _settings.SetValue("ShowPreviewUTC", _showPreviewUTCCheck.IsChecked ?? false);
            }
            if (_showPreviewUTPCheck != null)
            {
                _settings.SetValue("ShowPreviewUTP", _showPreviewUTPCheck.IsChecked ?? false);
            }
            if (_showPreviewUTDCheck != null)
            {
                _settings.SetValue("ShowPreviewUTD", _showPreviewUTDCheck.IsChecked ?? false);
            }
            if (_tempDirEdit != null)
            {
                _settings.ExtractPath = _tempDirEdit.Text ?? "";
                _settings.SetValue("ExtractPath", _settings.ExtractPath);
            }
            if (_gffEditorCombo != null)
            {
                _settings.SetGffSpecializedEditors(_gffEditorCombo.SelectedIndex == 1);
            }
            if (_ncsToolEdit != null)
            {
                _settings.NcsDecompilerPath = _ncsToolEdit.Text ?? "";
                _settings.SetValue("NcsDecompilerPath", _settings.NcsDecompilerPath);
            }
            if (_nssCompEdit != null)
            {
                _settings.NssCompilerPath = _nssCompEdit.Text ?? "";
                _settings.SetValue("NssCompilerPath", _settings.NssCompilerPath);
            }
        }
    }
}

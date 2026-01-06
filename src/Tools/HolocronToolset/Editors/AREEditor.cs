using Andastra.Parsing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Andastra.Parsing;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.ARE;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Widgets;
using HolocronToolset.Widgets.Edit;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:35
    // Original: class AREEditor(Editor):
    public class AREEditor : Editor
    {
        private ARE _are;
        private GFF _originalGff;
        private LocalizedStringEdit _nameEdit;
        private TextBox _tagEdit;
        private Button _tagGenerateButton;
        private ComboBox _cameraStyleSelect;
        private TextBox _envmapEdit;
        private CheckBox _disableTransitCheck;
        private CheckBox _unescapableCheck;
        private NumericUpDown _alphaTestSpin;
        private CheckBox _stealthCheck;
        private NumericUpDown _stealthMaxSpin;
        private NumericUpDown _stealthLossSpin;
        private ComboBox _mapAxisSelect;
        private NumericUpDown _mapZoomSpin;
        private NumericUpDown _mapResXSpin;
        private NumericUpDown _mapImageX1Spin;
        private NumericUpDown _mapImageY1Spin;
        private NumericUpDown _mapImageX2Spin;
        private NumericUpDown _mapImageY2Spin;
        private NumericUpDown _mapWorldX1Spin;
        private NumericUpDown _mapWorldY1Spin;
        private NumericUpDown _mapWorldX2Spin;
        private NumericUpDown _mapWorldY2Spin;
        private CheckBox _fogEnabledCheck;
        private ColorEdit _fogColorEdit;
        private NumericUpDown _fogNearSpin;
        private NumericUpDown _fogFarSpin;
        private ColorEdit _ambientColorEdit;
        private ColorEdit _diffuseColorEdit;
        private ColorEdit _dynamicColorEdit;
        private ColorEdit _grassDiffuseEdit;
        private ColorEdit _grassAmbientEdit;
        private ColorEdit _grassEmissiveEdit;
        private TextBox _grassTextureEdit;
        private NumericUpDown _grassDensitySpin;
        private NumericUpDown _grassSizeSpin;
        private NumericUpDown _grassProbLLSpin;
        private NumericUpDown _grassProbLRSpin;
        private NumericUpDown _grassProbULSpin;
        private NumericUpDown _grassProbURSpin;
        private ColorEdit _dirtColor1Edit;
        private ColorEdit _dirtColor2Edit;
        private ColorEdit _dirtColor3Edit;
        private NumericUpDown _dirtFormula1Spin;
        private NumericUpDown _dirtFormula2Spin;
        private NumericUpDown _dirtFormula3Spin;
        private NumericUpDown _dirtFunction1Spin;
        private NumericUpDown _dirtFunction2Spin;
        private NumericUpDown _dirtFunction3Spin;
        private NumericUpDown _dirtSize1Spin;
        private NumericUpDown _dirtSize2Spin;
        private NumericUpDown _dirtSize3Spin;
        private ComboBox _windPowerSelect;
        private CheckBox _rainCheck;
        private CheckBox _snowCheck;
        private CheckBox _lightningCheck;
        private CheckBox _shadowsCheck;
        private NumericUpDown _shadowsSpin;
        private TextBox _commentsEdit;
        private ComboBox _onEnterSelect;
        private ComboBox _onExitSelect;
        private ComboBox _onHeartbeatSelect;
        private ComboBox _onUserDefinedSelect;
        private List<string> _relevantScriptResnames;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:36-74
        // Original: def __init__(self, parent, installation):
        public AREEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "ARE Editor", "none",
                new[] { ResourceType.ARE },
                new[] { ResourceType.ARE },
                installation)
        {
            InitializeComponent();
            SetupUI();
            MinWidth = 400;
            MinHeight = 600;
            AddHelpAction(); // Auto-detects "GFF-ARE.md" for ARE
            SetupSignals();
            if (installation != null)
            {
                SetupInstallation(installation);
            }
            New();
        }

        private void InitializeComponent()
        {
            if (!TryLoadXaml())
            {
                SetupUI();
            }
        }

        private void SetupUI()
        {
            var panel = new StackPanel();

            // Name field - matching Python: self.ui.nameEdit
            var nameLabel = new Avalonia.Controls.TextBlock { Text = "Name:" };
            _nameEdit = new LocalizedStringEdit();
            if (_installation != null)
            {
                _nameEdit.SetInstallation(_installation);
            }
            panel.Children.Add(nameLabel);
            panel.Children.Add(_nameEdit);

            // Tag field - matching Python: self.ui.tagEdit
            var tagLabel = new Avalonia.Controls.TextBlock { Text = "Tag:" };
            _tagEdit = new TextBox();
            _tagGenerateButton = new Button { Content = "Generate" };
            _tagGenerateButton.Click += (s, e) => GenerateTag();
            var tagPanel = new StackPanel { Orientation = Orientation.Horizontal };
            tagPanel.Children.Add(_tagEdit);
            tagPanel.Children.Add(_tagGenerateButton);
            panel.Children.Add(tagLabel);
            panel.Children.Add(tagPanel);

            // Camera Style field - matching Python: self.ui.cameraStyleSelect
            // Matching Python: for label in cameras.get_column("name"): self.ui.cameraStyleSelect.addItem(label.title())
            var cameraStyleLabel = new Avalonia.Controls.TextBlock { Text = "Camera Style:" };
            _cameraStyleSelect = new ComboBox();
            // Load camera styles from cameras.2da via installation
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:111-117
            // Original: cameras: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_CAMERAS)
            // Original: for label in cameras.get_column("name"): self.ui.cameraStyleSelect.addItem(label.title())
            if (_installation != null)
            {
                try
                {
                    TwoDA cameras = _installation.HtGetCache2DA(HTInstallation.TwoDACameras);
                    if (cameras != null)
                    {
                        List<string> cameraNames = cameras.GetColumn("name");
                        if (cameraNames != null && cameraNames.Count > 0)
                        {
                            var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                            foreach (string label in cameraNames)
                            {
                                // Skip empty labels
                                if (string.IsNullOrWhiteSpace(label))
                                {
                                    continue;
                                }
                                // Python's .title() converts "hello world" to "Hello World"
                                // C# ToTitleCase requires lowercase input, so lowercase first then title-case
                                string titleCased = textInfo.ToTitleCase(label.ToLowerInvariant());
                                _cameraStyleSelect.Items.Add(titleCased);
                            }
                        }
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Column "name" doesn't exist in cameras.2da - fallback to defaults
                }
                catch
                {
                    // Any other error loading cameras.2da - fallback to defaults
                }
            }

            // Fallback to default camera styles if no items were loaded or installation is not available
            if (_cameraStyleSelect.ItemCount == 0)
            {
                LoadDefaultCameraStyles();
            }

            _cameraStyleSelect.SelectedIndex = 0;
            panel.Children.Add(cameraStyleLabel);
            panel.Children.Add(_cameraStyleSelect);

            // Envmap field - matching Python: self.ui.envmapEdit
            var envmapLabel = new Avalonia.Controls.TextBlock { Text = "Default Envmap:" };
            _envmapEdit = new TextBox();
            panel.Children.Add(envmapLabel);
            panel.Children.Add(_envmapEdit);

            // Disable Transit checkbox - matching Python: self.ui.disableTransitCheck
            _disableTransitCheck = new CheckBox { Content = "Disable Transit" };
            panel.Children.Add(_disableTransitCheck);

            // Unescapable checkbox - matching Python: self.ui.unescapableCheck
            _unescapableCheck = new CheckBox { Content = "Unescapable" };
            panel.Children.Add(_unescapableCheck);

            // Alpha Test spin - matching Python: self.ui.alphaTestSpin
            // Engine uses float for AlphaTest (swkotor.exe: 0x00508c50 line 303-304, swkotor2.exe: 0x004e3ff0 line 307-308)
            // Default value: 0.2, range typically 0.0-1.0 but allowing up to 255 for compatibility
            var alphaTestLabel = new Avalonia.Controls.TextBlock { Text = "Alpha Test:" };
            _alphaTestSpin = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 0, Increment = 0.1m };
            panel.Children.Add(alphaTestLabel);
            panel.Children.Add(_alphaTestSpin);

            // Stealth XP checkbox - matching Python: self.ui.stealthCheck
            _stealthCheck = new CheckBox { Content = "Stealth XP" };
            panel.Children.Add(_stealthCheck);

            // Stealth XP Max spin - matching Python: self.ui.stealthMaxSpin
            var stealthMaxLabel = new Avalonia.Controls.TextBlock { Text = "Stealth XP Max:" };
            _stealthMaxSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            panel.Children.Add(stealthMaxLabel);
            panel.Children.Add(_stealthMaxSpin);

            // Stealth XP Loss spin - matching Python: self.ui.stealthLossSpin
            var stealthLossLabel = new Avalonia.Controls.TextBlock { Text = "Stealth XP Loss:" };
            _stealthLossSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            panel.Children.Add(stealthLossLabel);
            panel.Children.Add(_stealthLossSpin);

            // Map Axis select - matching Python: self.ui.mapAxisSelect
            var mapAxisLabel = new Avalonia.Controls.TextBlock { Text = "Map Axis:" };
            _mapAxisSelect = new ComboBox();
            _mapAxisSelect.ItemsSource = new[] { "PositiveY", "NegativeY", "PositiveX", "NegativeX" };
            _mapAxisSelect.SelectedIndex = 0;
            panel.Children.Add(mapAxisLabel);
            panel.Children.Add(_mapAxisSelect);

            // Map Zoom spin - matching Python: self.ui.mapZoomSpin
            var mapZoomLabel = new Avalonia.Controls.TextBlock { Text = "Map Zoom:" };
            _mapZoomSpin = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = 1 };
            panel.Children.Add(mapZoomLabel);
            panel.Children.Add(_mapZoomSpin);

            // Map Res X spin - matching Python: self.ui.mapResXSpin
            var mapResXLabel = new Avalonia.Controls.TextBlock { Text = "Map Res X:" };
            _mapResXSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            panel.Children.Add(mapResXLabel);
            panel.Children.Add(_mapResXSpin);

            // Map Image X1 spin - matching Python: self.ui.mapImageX1Spin
            var mapImageX1Label = new Avalonia.Controls.TextBlock { Text = "Map Image X1:" };
            _mapImageX1Spin = new NumericUpDown { Minimum = 0.0M, Maximum = 1.0M, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(mapImageX1Label);
            panel.Children.Add(_mapImageX1Spin);

            // Map Image Y1 spin - matching Python: self.ui.mapImageY1Spin
            var mapImageY1Label = new Avalonia.Controls.TextBlock { Text = "Map Image Y1:" };
            _mapImageY1Spin = new NumericUpDown { Minimum = 0.0M, Maximum = 1.0M, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(mapImageY1Label);
            panel.Children.Add(_mapImageY1Spin);

            // Map Image X2 spin - matching Python: self.ui.mapImageX2Spin
            var mapImageX2Label = new Avalonia.Controls.TextBlock { Text = "Map Image X2:" };
            _mapImageX2Spin = new NumericUpDown { Minimum = 0.0M, Maximum = 1.0M, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(mapImageX2Label);
            panel.Children.Add(_mapImageX2Spin);

            // Map Image Y2 spin - matching Python: self.ui.mapImageY2Spin
            var mapImageY2Label = new Avalonia.Controls.TextBlock { Text = "Map Image Y2:" };
            _mapImageY2Spin = new NumericUpDown { Minimum = 0.0M, Maximum = 1.0M, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(mapImageY2Label);
            panel.Children.Add(_mapImageY2Spin);

            // Map World X1 spin - matching Python: self.ui.mapWorldX1Spin
            var mapWorldX1Label = new Avalonia.Controls.TextBlock { Text = "Map World X1:" };
            _mapWorldX1Spin = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(mapWorldX1Label);
            panel.Children.Add(_mapWorldX1Spin);

            // Map World Y1 spin - matching Python: self.ui.mapWorldY1Spin
            var mapWorldY1Label = new Avalonia.Controls.TextBlock { Text = "Map World Y1:" };
            _mapWorldY1Spin = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(mapWorldY1Label);
            panel.Children.Add(_mapWorldY1Spin);

            // Map World X2 spin - matching Python: self.ui.mapWorldX2Spin
            var mapWorldX2Label = new Avalonia.Controls.TextBlock { Text = "Map World X2:" };
            _mapWorldX2Spin = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(mapWorldX2Label);
            panel.Children.Add(_mapWorldX2Spin);

            // Map World Y2 spin - matching Python: self.ui.mapWorldY2Spin
            var mapWorldY2Label = new Avalonia.Controls.TextBlock { Text = "Map World Y2:" };
            _mapWorldY2Spin = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(mapWorldY2Label);
            panel.Children.Add(_mapWorldY2Spin);

            // Fog Enabled checkbox - matching Python: self.ui.fogEnabledCheck
            _fogEnabledCheck = new CheckBox { Content = "Fog Enabled" };
            panel.Children.Add(_fogEnabledCheck);

            // Fog Color edit - matching Python: self.ui.fogColorEdit
            var fogColorLabel = new Avalonia.Controls.TextBlock { Text = "Fog Color:" };
            _fogColorEdit = new ColorEdit(null);
            panel.Children.Add(fogColorLabel);
            panel.Children.Add(_fogColorEdit);

            // Fog Near spin - matching Python: self.ui.fogNearSpin
            var fogNearLabel = new Avalonia.Controls.TextBlock { Text = "Fog Near:" };
            _fogNearSpin = new NumericUpDown { Minimum = 0.0M, Maximum = decimal.MaxValue, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(fogNearLabel);
            panel.Children.Add(_fogNearSpin);

            // Fog Far spin - matching Python: self.ui.fogFarSpin
            var fogFarLabel = new Avalonia.Controls.TextBlock { Text = "Fog Far:" };
            _fogFarSpin = new NumericUpDown { Minimum = 0.0M, Maximum = decimal.MaxValue, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(fogFarLabel);
            panel.Children.Add(_fogFarSpin);

            // Ambient Color edit - matching Python: self.ui.ambientColorEdit
            var ambientColorLabel = new Avalonia.Controls.TextBlock { Text = "Sun Ambient:" };
            _ambientColorEdit = new ColorEdit(null);
            panel.Children.Add(ambientColorLabel);
            panel.Children.Add(_ambientColorEdit);

            // Diffuse Color edit - matching Python: self.ui.diffuseColorEdit
            var diffuseColorLabel = new Avalonia.Controls.TextBlock { Text = "Sun Diffuse:" };
            _diffuseColorEdit = new ColorEdit(null);
            panel.Children.Add(diffuseColorLabel);
            panel.Children.Add(_diffuseColorEdit);

            // Dynamic Color edit - matching Python: self.ui.dynamicColorEdit
            var dynamicColorLabel = new Avalonia.Controls.TextBlock { Text = "Dynamic Light:" };
            _dynamicColorEdit = new ColorEdit(null);
            panel.Children.Add(dynamicColorLabel);
            panel.Children.Add(_dynamicColorEdit);

            // Wind Power select - matching Python: self.ui.windPowerSelect
            var windPowerLabel = new Avalonia.Controls.TextBlock { Text = "Wind Power:" };
            _windPowerSelect = new ComboBox();
            // Wind power enum values: None=0, Light=1, Medium=2, Heavy=3
            _windPowerSelect.ItemsSource = new[] { "None", "Light", "Medium", "Heavy" };
            _windPowerSelect.SelectedIndex = 0;
            panel.Children.Add(windPowerLabel);
            panel.Children.Add(_windPowerSelect);

            // Weather checkboxes (TSL only) - matching Python: self.ui.rainCheck, snowCheck, lightningCheck
            _rainCheck = new CheckBox { Content = "Rain" };
            _snowCheck = new CheckBox { Content = "Snow" };
            _lightningCheck = new CheckBox { Content = "Lightning" };
            panel.Children.Add(_rainCheck);
            panel.Children.Add(_snowCheck);
            panel.Children.Add(_lightningCheck);

            // Shadows checkbox and spin - matching Python: self.ui.shadowsCheck, shadowsSpin
            _shadowsCheck = new CheckBox { Content = "Shadows" };
            panel.Children.Add(_shadowsCheck);
            var shadowsSpinLabel = new Avalonia.Controls.TextBlock { Text = "Shadow Opacity:" };
            _shadowsSpin = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 0 };
            panel.Children.Add(shadowsSpinLabel);
            panel.Children.Add(_shadowsSpin);

            // Terrain section - matching Python: self.ui.grassTextureEdit, grassDensitySpin, etc.
            var grassTextureLabel = new Avalonia.Controls.TextBlock { Text = "Grass Texture:" };
            _grassTextureEdit = new TextBox();
            panel.Children.Add(grassTextureLabel);
            panel.Children.Add(_grassTextureEdit);

            var grassDensityLabel = new Avalonia.Controls.TextBlock { Text = "Grass Density:" };
            _grassDensitySpin = new NumericUpDown { Minimum = 0.0M, Maximum = decimal.MaxValue, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(grassDensityLabel);
            panel.Children.Add(_grassDensitySpin);

            var grassSizeLabel = new Avalonia.Controls.TextBlock { Text = "Grass Size:" };
            _grassSizeSpin = new NumericUpDown { Minimum = 0.0M, Maximum = decimal.MaxValue, FormatString = "F6", Value = 0.0M };
            panel.Children.Add(grassSizeLabel);
            panel.Children.Add(_grassSizeSpin);

            // Grass probability spins - matching Python: self.ui.grassProbLLSpin, etc.
            var grassProbLabel = new Avalonia.Controls.TextBlock { Text = "Grass Probability (LL/LR/UL/UR):" };
            var grassProbPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _grassProbLLSpin = new NumericUpDown { Minimum = 0.0M, Maximum = 1.0M, FormatString = "F6", Value = 0.0M };
            _grassProbLRSpin = new NumericUpDown { Minimum = 0.0M, Maximum = 1.0M, FormatString = "F6", Value = 0.0M };
            _grassProbULSpin = new NumericUpDown { Minimum = 0.0M, Maximum = 1.0M, FormatString = "F6", Value = 0.0M };
            _grassProbURSpin = new NumericUpDown { Minimum = 0.0M, Maximum = 1.0M, FormatString = "F6", Value = 0.0M };
            grassProbPanel.Children.Add(_grassProbLLSpin);
            grassProbPanel.Children.Add(_grassProbLRSpin);
            grassProbPanel.Children.Add(_grassProbULSpin);
            grassProbPanel.Children.Add(_grassProbURSpin);
            panel.Children.Add(grassProbLabel);
            panel.Children.Add(grassProbPanel);

            // Grass color edits - matching Python: self.ui.grassDiffuseEdit, grassAmbientEdit, grassEmissiveEdit
            var grassDiffuseLabel = new Avalonia.Controls.TextBlock { Text = "Grass Diffuse:" };
            _grassDiffuseEdit = new ColorEdit(null);
            panel.Children.Add(grassDiffuseLabel);
            panel.Children.Add(_grassDiffuseEdit);

            var grassAmbientLabel = new Avalonia.Controls.TextBlock { Text = "Grass Ambient:" };
            _grassAmbientEdit = new ColorEdit(null);
            panel.Children.Add(grassAmbientLabel);
            panel.Children.Add(_grassAmbientEdit);

            var grassEmissiveLabel = new Avalonia.Controls.TextBlock { Text = "Grass Emissive (TSL only):" };
            _grassEmissiveEdit = new ColorEdit(null);
            panel.Children.Add(grassEmissiveLabel);
            panel.Children.Add(_grassEmissiveEdit);

            // Dirt color edits (TSL only) - matching Python: self.ui.dirtColor1Edit, dirtColor2Edit, dirtColor3Edit
            var dirtColorLabel = new Avalonia.Controls.TextBlock { Text = "Dirt Colors (TSL only):" };
            var dirtColorPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _dirtColor1Edit = new ColorEdit(null);
            _dirtColor2Edit = new ColorEdit(null);
            _dirtColor3Edit = new ColorEdit(null);
            // Allow alpha in dirt colors (matching Python: self.ui.dirtColor1Edit.allow_alpha = True)
            _dirtColor1Edit.AllowAlpha = true;
            _dirtColor2Edit.AllowAlpha = true;
            _dirtColor3Edit.AllowAlpha = true;
            dirtColorPanel.Children.Add(_dirtColor1Edit);
            dirtColorPanel.Children.Add(_dirtColor2Edit);
            dirtColorPanel.Children.Add(_dirtColor3Edit);
            panel.Children.Add(dirtColorLabel);
            panel.Children.Add(dirtColorPanel);

            // Dirt Formula spins (TSL only) - matching Python: self.ui.dirtFormula1Spin, dirtFormula2Spin, dirtFormula3Spin
            var dirtFormulaLabel = new Avalonia.Controls.TextBlock { Text = "Dirt Formula (TSL only):" };
            var dirtFormulaPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _dirtFormula1Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _dirtFormula2Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _dirtFormula3Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            dirtFormulaPanel.Children.Add(_dirtFormula1Spin);
            dirtFormulaPanel.Children.Add(_dirtFormula2Spin);
            dirtFormulaPanel.Children.Add(_dirtFormula3Spin);
            panel.Children.Add(dirtFormulaLabel);
            panel.Children.Add(dirtFormulaPanel);

            // Dirt Function spins (TSL only) - matching Python: self.ui.dirtFunction1Spin, dirtFunction2Spin, dirtFunction3Spin
            var dirtFunctionLabel = new Avalonia.Controls.TextBlock { Text = "Dirt Function (TSL only):" };
            var dirtFunctionPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _dirtFunction1Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _dirtFunction2Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _dirtFunction3Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            dirtFunctionPanel.Children.Add(_dirtFunction1Spin);
            dirtFunctionPanel.Children.Add(_dirtFunction2Spin);
            dirtFunctionPanel.Children.Add(_dirtFunction3Spin);
            panel.Children.Add(dirtFunctionLabel);
            panel.Children.Add(dirtFunctionPanel);

            // Dirt Size spins (TSL only) - matching Python: self.ui.dirtSize1Spin, dirtSize2Spin, dirtSize3Spin
            var dirtSizeLabel = new Avalonia.Controls.TextBlock { Text = "Dirt Size (TSL only):" };
            var dirtSizePanel = new StackPanel { Orientation = Orientation.Horizontal };
            _dirtSize1Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _dirtSize2Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            _dirtSize3Spin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 0 };
            dirtSizePanel.Children.Add(_dirtSize1Spin);
            dirtSizePanel.Children.Add(_dirtSize2Spin);
            dirtSizePanel.Children.Add(_dirtSize3Spin);
            panel.Children.Add(dirtSizeLabel);
            panel.Children.Add(dirtSizePanel);

            // Scripts section - matching Python: self.ui.onEnterSelect, onExitSelect, onHeartbeatSelect, onUserDefinedSelect
            var onEnterLabel = new Avalonia.Controls.TextBlock { Text = "OnEnter Script:" };
            _onEnterSelect = new ComboBox { IsEditable = true };
            panel.Children.Add(onEnterLabel);
            panel.Children.Add(_onEnterSelect);

            var onExitLabel = new Avalonia.Controls.TextBlock { Text = "OnExit Script:" };
            _onExitSelect = new ComboBox { IsEditable = true };
            panel.Children.Add(onExitLabel);
            panel.Children.Add(_onExitSelect);

            var onHeartbeatLabel = new Avalonia.Controls.TextBlock { Text = "OnHeartbeat Script:" };
            _onHeartbeatSelect = new ComboBox { IsEditable = true };
            panel.Children.Add(onHeartbeatLabel);
            panel.Children.Add(_onHeartbeatSelect);

            var onUserDefinedLabel = new Avalonia.Controls.TextBlock { Text = "OnUserDefined Script:" };
            _onUserDefinedSelect = new ComboBox { IsEditable = true };
            panel.Children.Add(onUserDefinedLabel);
            panel.Children.Add(_onUserDefinedSelect);

            // Comments edit - matching Python: self.ui.commentsEdit
            var commentsLabel = new Avalonia.Controls.TextBlock { Text = "Comments:" };
            _commentsEdit = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = false,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MinHeight = 100
            };
            panel.Children.Add(commentsLabel);
            panel.Children.Add(_commentsEdit);

            Content = panel;
        }

        // Matching PyKotor implementation - expose controls for testing
        public LocalizedStringEdit NameEdit => _nameEdit;
        public TextBox TagEdit => _tagEdit;
        public Button TagGenerateButton => _tagGenerateButton;
        public ComboBox CameraStyleSelect => _cameraStyleSelect;
        public ComboBox OnEnterSelect => _onEnterSelect;
        public ComboBox OnExitSelect => _onExitSelect;
        public ComboBox OnHeartbeatSelect => _onHeartbeatSelect;
        public ComboBox OnUserDefinedSelect => _onUserDefinedSelect;
        public TextBox EnvmapEdit => _envmapEdit;
        public CheckBox DisableTransitCheck => _disableTransitCheck;
        public CheckBox UnescapableCheck => _unescapableCheck;
        public NumericUpDown AlphaTestSpin => _alphaTestSpin;
        public CheckBox StealthCheck => _stealthCheck;
        public NumericUpDown StealthMaxSpin => _stealthMaxSpin;
        public NumericUpDown StealthLossSpin => _stealthLossSpin;
        public ComboBox MapAxisSelect => _mapAxisSelect;
        public NumericUpDown MapZoomSpin => _mapZoomSpin;
        public NumericUpDown MapResXSpin => _mapResXSpin;
        public NumericUpDown MapImageX1Spin => _mapImageX1Spin;
        public NumericUpDown MapImageY1Spin => _mapImageY1Spin;
        public NumericUpDown MapImageX2Spin => _mapImageX2Spin;
        public NumericUpDown MapImageY2Spin => _mapImageY2Spin;
        public NumericUpDown MapWorldX1Spin => _mapWorldX1Spin;
        public NumericUpDown MapWorldY1Spin => _mapWorldY1Spin;
        public NumericUpDown MapWorldX2Spin => _mapWorldX2Spin;
        public NumericUpDown MapWorldY2Spin => _mapWorldY2Spin;
        public CheckBox FogEnabledCheck => _fogEnabledCheck;
        public ColorEdit FogColorEdit => _fogColorEdit;
        public NumericUpDown FogNearSpin => _fogNearSpin;
        public NumericUpDown FogFarSpin => _fogFarSpin;
        public ColorEdit AmbientColorEdit => _ambientColorEdit;
        public ColorEdit DiffuseColorEdit => _diffuseColorEdit;
        public ColorEdit DynamicColorEdit => _dynamicColorEdit;
        public ColorEdit GrassDiffuseEdit => _grassDiffuseEdit;
        public ColorEdit GrassAmbientEdit => _grassAmbientEdit;
        public ColorEdit GrassEmissiveEdit => _grassEmissiveEdit;
        public TextBox GrassTextureEdit => _grassTextureEdit;
        public NumericUpDown GrassDensitySpin => _grassDensitySpin;
        public NumericUpDown GrassSizeSpin => _grassSizeSpin;
        public NumericUpDown GrassProbLLSpin => _grassProbLLSpin;
        public NumericUpDown GrassProbLRSpin => _grassProbLRSpin;
        public NumericUpDown GrassProbULSpin => _grassProbULSpin;
        public NumericUpDown GrassProbURSpin => _grassProbURSpin;
        public NumericUpDown DirtFormula1Spin => _dirtFormula1Spin;
        public NumericUpDown DirtFormula2Spin => _dirtFormula2Spin;
        public NumericUpDown DirtFormula3Spin => _dirtFormula3Spin;
        public NumericUpDown DirtSize1Spin => _dirtSize1Spin;
        public NumericUpDown DirtSize2Spin => _dirtSize2Spin;
        public NumericUpDown DirtSize3Spin => _dirtSize3Spin;
        public ColorEdit DirtColor1Edit => _dirtColor1Edit;
        public ColorEdit DirtColor2Edit => _dirtColor2Edit;
        public ColorEdit DirtColor3Edit => _dirtColor3Edit;
        public CheckBox ShadowsCheck => _shadowsCheck;
        public NumericUpDown ShadowsSpin => _shadowsSpin;
        public CheckBox RainCheck => _rainCheck;
        public CheckBox SnowCheck => _snowCheck;
        public CheckBox LightningCheck => _lightningCheck;
        public ComboBox WindPowerSelect => _windPowerSelect;
        public TextBox CommentsEdit => _commentsEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:134-149
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The ARE file data is empty or invalid.");
            }

            // ARE is a GFF-based format
            var gff = GFF.FromBytes(data);
            // Store original GFF to preserve unmodified fields (like Rooms list)
            _originalGff = gff;
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:146
            // Original: are: ARE = read_are(data)
            _are = AREHelpers.ConstructAre(gff);
            LoadARE(_are);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:151-248
        // Original: def _loadARE(self, are: ARE):
        private void LoadARE(ARE are)
        {
            _are = are;

            // Populate script combo boxes with relevant script resources (matching Python lines 230-246)
            // This must be done here after filepath is set, not in constructor
            if (_installation != null && !string.IsNullOrEmpty(_filepath))
            {
                var scriptResources = _installation.GetRelevantResources(ResourceType.NCS, _filepath);
                _relevantScriptResnames = scriptResources
                    .Select(r => r.ResName.ToLowerInvariant())
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList();

                // Populate all script combo boxes
                if (_onEnterSelect != null)
                {
                    _onEnterSelect.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onEnterSelect.Items.Add(resname);
                    }
                }
                if (_onExitSelect != null)
                {
                    _onExitSelect.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onExitSelect.Items.Add(resname);
                    }
                }
                if (_onHeartbeatSelect != null)
                {
                    _onHeartbeatSelect.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onHeartbeatSelect.Items.Add(resname);
                    }
                }
                if (_onUserDefinedSelect != null)
                {
                    _onUserDefinedSelect.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onUserDefinedSelect.Items.Add(resname);
                    }
                }
            }

            // Matching Python: self.ui.nameEdit.set_locstring(are.name) (line 177)
            if (_nameEdit != null)
            {
                _nameEdit.SetLocString(are.Name);
            }
            // Matching Python: self.ui.tagEdit.setText(are.tag) (line 178)
            if (_tagEdit != null)
            {
                _tagEdit.Text = are.Tag ?? "";
            }
            // Matching Python: self.ui.cameraStyleSelect.setCurrentIndex(are.camera_style) (line 179)
            if (_cameraStyleSelect != null)
            {
                // Ensure index is within bounds
                if (are.CameraStyle >= 0 && are.CameraStyle < _cameraStyleSelect.ItemCount)
                {
                    _cameraStyleSelect.SelectedIndex = are.CameraStyle;
                }
                else
                {
                    _cameraStyleSelect.SelectedIndex = 0;
                }
            }
            // Matching Python: self.ui.envmapEdit.setText(str(are.default_envmap)) (line 180)
            if (_envmapEdit != null)
            {
                _envmapEdit.Text = are.DefaultEnvMap.ToString();
            }
            // Matching Python: self.ui.disableTransitCheck.setChecked(are.disable_transit) (line 181)
            if (_disableTransitCheck != null)
            {
                _disableTransitCheck.IsChecked = are.DisableTransit;
            }
            // Matching Python: self.ui.unescapableCheck.setChecked(are.unescapable) (line 182)
            if (_unescapableCheck != null)
            {
                _unescapableCheck.IsChecked = are.Unescapable;
            }
            // Matching Python: self.ui.alphaTestSpin.setValue(are.alpha_test) (line 183)
            if (_alphaTestSpin != null)
            {
                _alphaTestSpin.Value = (decimal)are.AlphaTest;
            }
            // Matching Python: self.ui.stealthCheck.setChecked(are.stealth_xp) (line 184)
            if (_stealthCheck != null)
            {
                _stealthCheck.IsChecked = are.StealthXp;
            }
            // Matching Python: self.ui.stealthMaxSpin.setValue(are.stealth_xp_max) (line 185)
            if (_stealthMaxSpin != null)
            {
                _stealthMaxSpin.Value = are.StealthXpMax;
            }
            // Matching Python: self.ui.stealthLossSpin.setValue(are.stealth_xp_loss) (line 186)
            if (_stealthLossSpin != null)
            {
                _stealthLossSpin.Value = are.StealthXpLoss;
            }
            // Matching Python: self.ui.mapAxisSelect.setCurrentIndex(are.north_axis) (line 189)
            if (_mapAxisSelect != null)
            {
                _mapAxisSelect.SelectedIndex = (int)are.NorthAxis;
            }
            // Matching Python: self.ui.mapZoomSpin.setValue(are.map_zoom) (line 190)
            if (_mapZoomSpin != null)
            {
                _mapZoomSpin.Value = are.MapZoom;
            }
            // Matching Python: self.ui.mapResXSpin.setValue(are.map_res_x) (line 191)
            if (_mapResXSpin != null)
            {
                _mapResXSpin.Value = are.MapResX;
            }
            // Matching Python: self.ui.mapImageX1Spin.setValue(are.map_point_1.x) (line 192)
            if (_mapImageX1Spin != null)
            {
                _mapImageX1Spin.Value = (decimal)are.MapPoint1.X;
            }
            // Matching Python: self.ui.mapImageX2Spin.setValue(are.map_point_2.x) (line 193)
            if (_mapImageX2Spin != null)
            {
                _mapImageX2Spin.Value = (decimal)are.MapPoint2.X;
            }
            // Matching Python: self.ui.mapImageY1Spin.setValue(are.map_point_1.y) (line 194)
            if (_mapImageY1Spin != null)
            {
                _mapImageY1Spin.Value = (decimal)are.MapPoint1.Y;
            }
            // Matching Python: self.ui.mapImageY2Spin.setValue(are.map_point_2.y) (line 195)
            if (_mapImageY2Spin != null)
            {
                _mapImageY2Spin.Value = (decimal)are.MapPoint2.Y;
            }
            // Matching Python: self.ui.mapWorldX1Spin.setValue(are.world_point_1.x) (line 196)
            if (_mapWorldX1Spin != null)
            {
                _mapWorldX1Spin.Value = (decimal)are.WorldPoint1.X;
            }
            // Matching Python: self.ui.mapWorldX2Spin.setValue(are.world_point_2.x) (line 197)
            if (_mapWorldX2Spin != null)
            {
                _mapWorldX2Spin.Value = (decimal)are.WorldPoint2.X;
            }
            // Matching Python: self.ui.mapWorldY1Spin.setValue(are.world_point_1.y) (line 198)
            if (_mapWorldY1Spin != null)
            {
                _mapWorldY1Spin.Value = (decimal)are.WorldPoint1.Y;
            }
            // Matching Python: self.ui.mapWorldY2Spin.setValue(are.world_point_2.y) (line 199)
            if (_mapWorldY2Spin != null)
            {
                _mapWorldY2Spin.Value = (decimal)are.WorldPoint2.Y;
            }
            // Matching Python: self.ui.fogEnabledCheck.setChecked(are.fog_enabled) (line 202)
            if (_fogEnabledCheck != null)
            {
                _fogEnabledCheck.IsChecked = are.FogEnabled;
            }
            // Matching Python: self.ui.fogColorEdit.set_color(are.fog_color) (line 203)
            if (_fogColorEdit != null)
            {
                _fogColorEdit.SetColor(are.FogColor);
            }
            // Matching Python: self.ui.fogNearSpin.setValue(are.fog_near) (line 204)
            if (_fogNearSpin != null)
            {
                _fogNearSpin.Value = (decimal)are.FogNear;
            }
            // Matching Python: self.ui.fogFarSpin.setValue(are.fog_far) (line 205)
            if (_fogFarSpin != null)
            {
                _fogFarSpin.Value = (decimal)are.FogFar;
            }
            // Matching Python: self.ui.ambientColorEdit.set_color(are.sun_ambient) (line 206)
            if (_ambientColorEdit != null)
            {
                _ambientColorEdit.SetColor(are.SunAmbient);
            }
            // Matching Python: self.ui.diffuseColorEdit.set_color(are.sun_diffuse) (line 207)
            if (_diffuseColorEdit != null)
            {
                _diffuseColorEdit.SetColor(are.SunDiffuse);
            }
            // Matching Python: self.ui.dynamicColorEdit.set_color(are.dynamic_light) (line 208)
            if (_dynamicColorEdit != null)
            {
                _dynamicColorEdit.SetColor(are.DynamicLight);
            }
            // Matching Python: self.ui.grassDiffuseEdit.set_color(are.grass_diffuse) (line 328)
            if (_grassDiffuseEdit != null)
            {
                _grassDiffuseEdit.SetColor(are.GrassDiffuse);
            }
            // Matching Python: self.ui.grassAmbientEdit.set_color(are.grass_ambient) (line 329)
            if (_grassAmbientEdit != null)
            {
                _grassAmbientEdit.SetColor(are.GrassAmbient);
            }
            // Matching Python: self.ui.grassEmissiveEdit.set_color(are.grass_emissive) (line 330) (TSL only)
            if (_grassEmissiveEdit != null)
            {
                _grassEmissiveEdit.SetColor(are.GrassEmissive);
            }
            // Matching Python: self.ui.grassTextureEdit.setText(str(are.grass_texture)) (line 217)
            if (_grassTextureEdit != null)
            {
                _grassTextureEdit.Text = are.GrassTexture.ToString();
            }
            // Matching Python: self.ui.grassDensitySpin.setValue(are.grass_density) (line 221)
            if (_grassDensitySpin != null)
            {
                _grassDensitySpin.Value = (decimal)are.GrassDensity;
            }
            // Matching Python: self.ui.grassSizeSpin.setValue(are.grass_size) (line 222)
            if (_grassSizeSpin != null)
            {
                _grassSizeSpin.Value = (decimal)are.GrassSize;
            }
            // Matching Python: self.ui.grassProbLLSpin.setValue(are.grass_prob_ll) (line 223)
            if (_grassProbLLSpin != null)
            {
                _grassProbLLSpin.Value = (decimal)are.GrassProbLL;
            }
            // Matching Python: self.ui.grassProbLRSpin.setValue(are.grass_prob_lr) (line 224)
            if (_grassProbLRSpin != null)
            {
                _grassProbLRSpin.Value = (decimal)are.GrassProbLR;
            }
            // Matching Python: self.ui.grassProbULSpin.setValue(are.grass_prob_ul) (line 225)
            if (_grassProbULSpin != null)
            {
                _grassProbULSpin.Value = (decimal)are.GrassProbUL;
            }
            // Matching Python: self.ui.grassProbURSpin.setValue(are.grass_prob_ur) (line 226)
            if (_grassProbURSpin != null)
            {
                _grassProbURSpin.Value = (decimal)are.GrassProbUR;
            }
            // Matching Python: self.ui.windPowerSelect.setCurrentIndex(are.wind_power) (line 209)
            if (_windPowerSelect != null && are.WindPower >= 0 && are.WindPower < _windPowerSelect.ItemCount)
            {
                _windPowerSelect.SelectedIndex = are.WindPower;
            }
            // Matching Python: self.ui.rainCheck.setChecked(are.chance_rain == max_value) (line 210)
            // Original: max_value: int = 100
            // PyKotor uses chance_rain == 100 to indicate enabled
            if (_rainCheck != null)
            {
                _rainCheck.IsChecked = are.ChanceRain == 100;
            }
            // Matching Python: self.ui.snowCheck.setChecked(are.chance_snow == max_value) (line 211)
            if (_snowCheck != null)
            {
                _snowCheck.IsChecked = are.ChanceSnow == 100;
            }
            // Matching Python: self.ui.lightningCheck.setChecked(are.chance_lightning == max_value) (line 212)
            if (_lightningCheck != null)
            {
                _lightningCheck.IsChecked = are.ChanceLightning == 100;
            }
            // Matching Python: self.ui.shadowsCheck.setChecked(are.shadows) (line 213)
            // Note: ARE class doesn't have Shadows bool - shadow is enabled if ShadowOpacity > 0
            if (_shadowsCheck != null)
            {
                _shadowsCheck.IsChecked = are.ShadowOpacity > 0;
            }
            // Matching Python: self.ui.shadowsSpin.setValue(are.shadow_opacity) (line 214)
            // ShadowOpacity is byte (0-255) in ARE class
            if (_shadowsSpin != null)
            {
                _shadowsSpin.Value = are.ShadowOpacity;
            }
            // Matching Python: self.ui.dirtColor1Edit.set_color(are.dirty_argb_1) (line 227)
            if (_dirtColor1Edit != null)
            {
                _dirtColor1Edit.SetColor(are.DirtyArgb1);
            }
            // Matching Python: self.ui.dirtColor2Edit.set_color(are.dirty_argb_2) (line 228)
            if (_dirtColor2Edit != null)
            {
                _dirtColor2Edit.SetColor(are.DirtyArgb2);
            }
            // Matching Python: self.ui.dirtColor3Edit.set_color(are.dirty_argb_3) (line 229)
            if (_dirtColor3Edit != null)
            {
                _dirtColor3Edit.SetColor(are.DirtyArgb3);
            }
            // Matching Python: self.ui.dirtFormula1Spin.setValue(are.dirty_formula_1) (line 230)
            if (_dirtFormula1Spin != null)
            {
                _dirtFormula1Spin.Value = are.DirtyFormula1;
            }
            // Matching Python: self.ui.dirtFormula2Spin.setValue(are.dirty_formula_2) (line 231)
            if (_dirtFormula2Spin != null)
            {
                _dirtFormula2Spin.Value = are.DirtyFormula2;
            }
            // Matching Python: self.ui.dirtFormula3Spin.setValue(are.dirty_formula_3) (line 232)
            if (_dirtFormula3Spin != null)
            {
                _dirtFormula3Spin.Value = are.DirtyFormula3;
            }
            // Matching Python: self.ui.dirtFunction1Spin.setValue(are.dirty_func_1) (line 233)
            if (_dirtFunction1Spin != null)
            {
                _dirtFunction1Spin.Value = are.DirtyFunc1;
            }
            // Matching Python: self.ui.dirtFunction2Spin.setValue(are.dirty_func_2) (line 234)
            if (_dirtFunction2Spin != null)
            {
                _dirtFunction2Spin.Value = are.DirtyFunc2;
            }
            // Matching Python: self.ui.dirtFunction3Spin.setValue(are.dirty_func_3) (line 235)
            if (_dirtFunction3Spin != null)
            {
                _dirtFunction3Spin.Value = are.DirtyFunc3;
            }
            // Matching Python: self.ui.dirtSize1Spin.setValue(are.dirty_size_1) (line 236)
            if (_dirtSize1Spin != null)
            {
                _dirtSize1Spin.Value = are.DirtySize1;
            }
            // Matching Python: self.ui.dirtSize2Spin.setValue(are.dirty_size_2) (line 237)
            if (_dirtSize2Spin != null)
            {
                _dirtSize2Spin.Value = are.DirtySize2;
            }
            // Matching Python: self.ui.dirtSize3Spin.setValue(are.dirty_size_3) (line 238)
            if (_dirtSize3Spin != null)
            {
                _dirtSize3Spin.Value = are.DirtySize3;
            }
            // Matching Python: self.ui.onEnterSelect.set_combo_box_text(str(are.on_enter)) (line 241)
            if (_onEnterSelect != null)
            {
                string onEnterText = are.OnEnter.ToString();
                _onEnterSelect.Text = onEnterText;
                // Try to select the item if it exists in the list
                if (!string.IsNullOrEmpty(onEnterText))
                {
                    int index = _onEnterSelect.Items.IndexOf(onEnterText);
                    if (index >= 0)
                    {
                        _onEnterSelect.SelectedIndex = index;
                    }
                }
            }
            // Matching Python: self.ui.onExitSelect.set_combo_box_text(str(are.on_exit)) (line 242)
            if (_onExitSelect != null)
            {
                string onExitText = are.OnExit.ToString();
                _onExitSelect.Text = onExitText;
                if (!string.IsNullOrEmpty(onExitText))
                {
                    int index = _onExitSelect.Items.IndexOf(onExitText);
                    if (index >= 0)
                    {
                        _onExitSelect.SelectedIndex = index;
                    }
                }
            }
            // Matching Python: self.ui.onHeartbeatSelect.set_combo_box_text(str(are.on_heartbeat)) (line 243)
            if (_onHeartbeatSelect != null)
            {
                string onHeartbeatText = are.OnHeartbeat.ToString();
                _onHeartbeatSelect.Text = onHeartbeatText;
                if (!string.IsNullOrEmpty(onHeartbeatText))
                {
                    int index = _onHeartbeatSelect.Items.IndexOf(onHeartbeatText);
                    if (index >= 0)
                    {
                        _onHeartbeatSelect.SelectedIndex = index;
                    }
                }
            }
            // Matching Python: self.ui.onUserDefinedSelect.set_combo_box_text(str(are.on_user_defined)) (line 244)
            if (_onUserDefinedSelect != null)
            {
                string onUserDefinedText = are.OnUserDefined.ToString();
                _onUserDefinedSelect.Text = onUserDefinedText;
                if (!string.IsNullOrEmpty(onUserDefinedText))
                {
                    int index = _onUserDefinedSelect.Items.IndexOf(onUserDefinedText);
                    if (index >= 0)
                    {
                        _onUserDefinedSelect.SelectedIndex = index;
                    }
                }
            }
            // Matching Python: self.ui.commentsEdit.setPlainText(are.comment) (line 247)
            if (_commentsEdit != null)
            {
                _commentsEdit.Text = are.Comment ?? "";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:250-300
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching Python: are = deepcopy(self._are) / _buildARE() which creates new ARE and sets from UI
            // Following UTWEditor pattern: create copy then read from UI controls
            var are = CopyAre(_are);

            // Matching Python: are.name = self.ui.nameEdit.locstring() (line 283)
            if (_nameEdit != null)
            {
                are.Name = _nameEdit.GetLocString();
            }
            // Matching Python: are.tag = self.ui.tagEdit.text() (line 284)
            if (_tagEdit != null)
            {
                are.Tag = _tagEdit.Text ?? "";
            }
            // Matching Python: are.camera_style = self.ui.cameraStyleSelect.currentIndex() (line 285)
            if (_cameraStyleSelect != null && _cameraStyleSelect.SelectedIndex >= 0)
            {
                are.CameraStyle = _cameraStyleSelect.SelectedIndex;
            }
            // Matching Python: are.default_envmap = ResRef(self.ui.envmapEdit.text()) (line 286)
            if (_envmapEdit != null)
            {
                are.DefaultEnvMap = new ResRef(_envmapEdit.Text ?? "");
            }
            // Matching Python: are.disable_transit = self.ui.disableTransitCheck.isChecked() (line 288)
            if (_disableTransitCheck != null)
            {
                are.DisableTransit = _disableTransitCheck.IsChecked == true;
            }
            // Matching Python: are.unescapable = self.ui.unescapableCheck.isChecked() (line 287)
            if (_unescapableCheck != null)
            {
                are.Unescapable = _unescapableCheck.IsChecked == true;
            }
            // Matching Python: are.alpha_test = float(self.ui.alphaTestSpin.value()) (line 289)
            // Engine uses float for AlphaTest (swkotor.exe: 0x00508c50 line 303-304, swkotor2.exe: 0x004e3ff0 line 307-308)
            if (_alphaTestSpin != null && _alphaTestSpin.Value.HasValue)
            {
                are.AlphaTest = (float)_alphaTestSpin.Value.Value;
            }
            // Matching Python: are.stealth_xp = self.ui.stealthCheck.isChecked() (line 290)
            if (_stealthCheck != null)
            {
                are.StealthXp = _stealthCheck.IsChecked == true;
            }
            // Matching Python: are.stealth_xp_max = self.ui.stealthMaxSpin.value() (line 291)
            if (_stealthMaxSpin != null && _stealthMaxSpin.Value.HasValue)
            {
                are.StealthXpMax = (int)_stealthMaxSpin.Value.Value;
            }
            // Matching Python: are.stealth_xp_loss = self.ui.stealthLossSpin.value() (line 292)
            if (_stealthLossSpin != null && _stealthLossSpin.Value.HasValue)
            {
                are.StealthXpLoss = (int)_stealthLossSpin.Value.Value;
            }
            // Matching Python: are.north_axis = ARENorthAxis(self.ui.mapAxisSelect.currentIndex()) (line 295)
            if (_mapAxisSelect != null && _mapAxisSelect.SelectedIndex >= 0)
            {
                are.NorthAxis = (ARENorthAxis)_mapAxisSelect.SelectedIndex;
            }
            // Matching Python: are.map_zoom = self.ui.mapZoomSpin.value() (line 296)
            if (_mapZoomSpin != null && _mapZoomSpin.Value.HasValue)
            {
                are.MapZoom = (int)_mapZoomSpin.Value.Value;
            }
            // Matching Python: are.map_res_x = self.ui.mapResXSpin.value() (line 297)
            if (_mapResXSpin != null && _mapResXSpin.Value.HasValue)
            {
                are.MapResX = (int)_mapResXSpin.Value.Value;
            }
            // Matching Python: are.map_point_1 = Vector2(self.ui.mapImageX1Spin.value(), self.ui.mapImageY1Spin.value()) (line 298)
            if (_mapImageX1Spin != null && _mapImageY1Spin != null &&
                _mapImageX1Spin.Value.HasValue && _mapImageY1Spin.Value.HasValue)
            {
                are.MapPoint1 = new System.Numerics.Vector2(
                    (float)_mapImageX1Spin.Value.Value,
                    (float)_mapImageY1Spin.Value.Value);
            }
            // Matching Python: are.map_point_2 = Vector2(self.ui.mapImageX2Spin.value(), self.ui.mapImageY2Spin.value()) (line 299)
            if (_mapImageX2Spin != null && _mapImageY2Spin != null &&
                _mapImageX2Spin.Value.HasValue && _mapImageY2Spin.Value.HasValue)
            {
                are.MapPoint2 = new System.Numerics.Vector2(
                    (float)_mapImageX2Spin.Value.Value,
                    (float)_mapImageY2Spin.Value.Value);
            }
            // Matching Python: are.world_point_1 = Vector2(self.ui.mapWorldX1Spin.value(), self.ui.mapWorldY1Spin.value()) (line 300)
            if (_mapWorldX1Spin != null && _mapWorldY1Spin != null &&
                _mapWorldX1Spin.Value.HasValue && _mapWorldY1Spin.Value.HasValue)
            {
                are.WorldPoint1 = new System.Numerics.Vector2(
                    (float)_mapWorldX1Spin.Value.Value,
                    (float)_mapWorldY1Spin.Value.Value);
            }
            // Matching Python: are.world_point_2 = Vector2(self.ui.mapWorldX2Spin.value(), self.ui.mapWorldY2Spin.value()) (line 301)
            if (_mapWorldX2Spin != null && _mapWorldY2Spin != null &&
                _mapWorldX2Spin.Value.HasValue && _mapWorldY2Spin.Value.HasValue)
            {
                are.WorldPoint2 = new System.Numerics.Vector2(
                    (float)_mapWorldX2Spin.Value.Value,
                    (float)_mapWorldY2Spin.Value.Value);
            }
            // Matching Python: are.fog_enabled = self.ui.fogEnabledCheck.isChecked() (line 304)
            if (_fogEnabledCheck != null)
            {
                are.FogEnabled = _fogEnabledCheck.IsChecked == true;
            }
            // Matching Python: are.fog_color = self.ui.fogColorEdit.color() (line 305)
            if (_fogColorEdit != null)
            {
                are.FogColor = _fogColorEdit.GetColor();
            }
            // Matching Python: are.fog_near = self.ui.fogNearSpin.value() (line 306)
            if (_fogNearSpin != null && _fogNearSpin.Value.HasValue)
            {
                are.FogNear = (float)_fogNearSpin.Value.Value;
            }
            // Matching Python: are.fog_far = self.ui.fogFarSpin.value() (line 307)
            if (_fogFarSpin != null && _fogFarSpin.Value.HasValue)
            {
                are.FogFar = (float)_fogFarSpin.Value.Value;
            }
            // Matching Python: are.sun_ambient = self.ui.ambientColorEdit.color() (line 308)
            if (_ambientColorEdit != null)
            {
                are.SunAmbient = _ambientColorEdit.GetColor();
            }
            // Matching Python: are.sun_diffuse = self.ui.diffuseColorEdit.color() (line 309)
            if (_diffuseColorEdit != null)
            {
                are.SunDiffuse = _diffuseColorEdit.GetColor();
            }
            // Matching Python: are.dynamic_light = self.ui.dynamicColorEdit.color() (line 310)
            if (_dynamicColorEdit != null)
            {
                are.DynamicLight = _dynamicColorEdit.GetColor();
            }
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:326-348
            // Terrain section
            // Original: are.grass_texture = ResRef(self.ui.grassTextureEdit.text()) (line 327)
            if (_grassTextureEdit != null)
            {
                are.GrassTexture = new ResRef(_grassTextureEdit.Text ?? "");
            }
            // Original: are.grass_diffuse = self.ui.grassDiffuseEdit.color() (line 328)
            if (_grassDiffuseEdit != null)
            {
                are.GrassDiffuse = _grassDiffuseEdit.GetColor();
            }
            // Original: are.grass_ambient = self.ui.grassAmbientEdit.color() (line 329)
            if (_grassAmbientEdit != null)
            {
                are.GrassAmbient = _grassAmbientEdit.GetColor();
            }
            // Original: are.grass_emissive = self.ui.grassEmissiveEdit.color() (line 330)
            if (_grassEmissiveEdit != null)
            {
                are.GrassEmissive = _grassEmissiveEdit.GetColor();
            }
            // Original: are.grass_size = self.ui.grassSizeSpin.value() (line 331)
            if (_grassSizeSpin != null && _grassSizeSpin.Value.HasValue)
            {
                are.GrassSize = (float)_grassSizeSpin.Value.Value;
            }
            // Original: are.grass_density = self.ui.grassDensitySpin.value() (line 332)
            if (_grassDensitySpin != null && _grassDensitySpin.Value.HasValue)
            {
                are.GrassDensity = (float)_grassDensitySpin.Value.Value;
            }
            // Original: are.grass_prob_ll = self.ui.grassProbLLSpin.value() (line 333)
            if (_grassProbLLSpin != null && _grassProbLLSpin.Value.HasValue)
            {
                are.GrassProbLL = (float)_grassProbLLSpin.Value.Value;
            }
            // Original: are.grass_prob_lr = self.ui.grassProbLRSpin.value() (line 334)
            if (_grassProbLRSpin != null && _grassProbLRSpin.Value.HasValue)
            {
                are.GrassProbLR = (float)_grassProbLRSpin.Value.Value;
            }
            // Original: are.grass_prob_ul = self.ui.grassProbULSpin.value() (line 335)
            if (_grassProbULSpin != null && _grassProbULSpin.Value.HasValue)
            {
                are.GrassProbUL = (float)_grassProbULSpin.Value.Value;
            }
            // Original: are.grass_prob_ur = self.ui.grassProbURSpin.value() (line 336)
            if (_grassProbURSpin != null && _grassProbURSpin.Value.HasValue)
            {
                are.GrassProbUR = (float)_grassProbURSpin.Value.Value;
            }
            // Original: are.dirty_argb_1 = self.ui.dirtColor1Edit.color() (line 337)
            if (_dirtColor1Edit != null)
            {
                are.DirtyArgb1 = _dirtColor1Edit.GetColor();
            }
            // Original: are.dirty_argb_2 = self.ui.dirtColor2Edit.color() (line 338)
            if (_dirtColor2Edit != null)
            {
                are.DirtyArgb2 = _dirtColor2Edit.GetColor();
            }
            // Original: are.dirty_argb_3 = self.ui.dirtColor3Edit.color() (line 339)
            if (_dirtColor3Edit != null)
            {
                are.DirtyArgb3 = _dirtColor3Edit.GetColor();
            }
            // Original: are.dirty_formula_1 = self.ui.dirtFormula1Spin.value() (line 340)
            if (_dirtFormula1Spin != null && _dirtFormula1Spin.Value.HasValue)
            {
                are.DirtyFormula1 = (int)_dirtFormula1Spin.Value.Value;
            }
            // Original: are.dirty_formula_2 = self.ui.dirtFormula2Spin.value() (line 341)
            if (_dirtFormula2Spin != null && _dirtFormula2Spin.Value.HasValue)
            {
                are.DirtyFormula2 = (int)_dirtFormula2Spin.Value.Value;
            }
            // Original: are.dirty_formula_3 = self.ui.dirtFormula3Spin.value() (line 342)
            if (_dirtFormula3Spin != null && _dirtFormula3Spin.Value.HasValue)
            {
                are.DirtyFormula3 = (int)_dirtFormula3Spin.Value.Value;
            }
            // Original: are.dirty_func_1 = self.ui.dirtFunction1Spin.value() (line 343)
            if (_dirtFunction1Spin != null && _dirtFunction1Spin.Value.HasValue)
            {
                are.DirtyFunc1 = (int)_dirtFunction1Spin.Value.Value;
            }
            // Original: are.dirty_func_2 = self.ui.dirtFunction2Spin.value() (line 344)
            if (_dirtFunction2Spin != null && _dirtFunction2Spin.Value.HasValue)
            {
                are.DirtyFunc2 = (int)_dirtFunction2Spin.Value.Value;
            }
            // Original: are.dirty_func_3 = self.ui.dirtFunction3Spin.value() (line 345)
            if (_dirtFunction3Spin != null && _dirtFunction3Spin.Value.HasValue)
            {
                are.DirtyFunc3 = (int)_dirtFunction3Spin.Value.Value;
            }
            // Original: are.dirty_size_1 = self.ui.dirtSize1Spin.value() (line 346)
            if (_dirtSize1Spin != null && _dirtSize1Spin.Value.HasValue)
            {
                are.DirtySize1 = (int)_dirtSize1Spin.Value.Value;
            }
            // Original: are.dirty_size_2 = self.ui.dirtSize2Spin.value() (line 347)
            if (_dirtSize2Spin != null && _dirtSize2Spin.Value.HasValue)
            {
                are.DirtySize2 = (int)_dirtSize2Spin.Value.Value;
            }
            // Original: are.dirty_size_3 = self.ui.dirtSize3Spin.value() (line 348)
            if (_dirtSize3Spin != null && _dirtSize3Spin.Value.HasValue)
            {
                are.DirtySize3 = (int)_dirtSize3Spin.Value.Value;
            }

            // Weather section - matching Python lines 303-324
            // Original: are.wind_power = AREWindPower(self.ui.windPowerSelect.currentIndex()) (line 311)
            if (_windPowerSelect != null && _windPowerSelect.SelectedIndex >= 0)
            {
                are.WindPower = _windPowerSelect.SelectedIndex;
            }
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:313-322
            // Original: # Read checkbox state - if checkbox is checked, use 100; otherwise use 0
            // Original: # For K1 installations, weather checkboxes are TSL-only and should always be 0
            // Original: if self._installation and self._installation.tsl:
            // Original:     are.chance_rain = 100 if self.ui.rainCheck.isChecked() else 0
            // Original:     are.chance_snow = 100 if self.ui.snowCheck.isChecked() else 0
            // Original:     are.chance_lightning = 100 if self.ui.lightningCheck.isChecked() else 0
            // Original: else:
            // Original:     # K1 installations don't support weather checkboxes
            // Original:     are.chance_rain = 0
            // Original:     are.chance_snow = 0
            // Original:     are.chance_lightning = 0
            if (_installation != null && _installation.IsTsl)
            {
                // TSL (K2) installations support weather checkboxes
                if (_rainCheck != null)
                {
                    are.ChanceRain = _rainCheck.IsChecked == true ? 100 : 0;
                }
                if (_snowCheck != null)
                {
                    are.ChanceSnow = _snowCheck.IsChecked == true ? 100 : 0;
                }
                if (_lightningCheck != null)
                {
                    are.ChanceLightning = _lightningCheck.IsChecked == true ? 100 : 0;
                }
            }
            else
            {
                // K1 installations don't support weather checkboxes - always set to 0
                are.ChanceRain = 0;
                are.ChanceSnow = 0;
                are.ChanceLightning = 0;
            }
            // Original: are.shadows = self.ui.shadowsCheck.isChecked() (line 323)
            // Note: ARE class doesn't have Shadows bool - shadow is enabled if ShadowOpacity > 0
            // Original: are.shadow_opacity = self.ui.shadowsSpin.value() (line 324)
            // ShadowOpacity is byte (0-255) in ARE class
            if (_shadowsSpin != null && _shadowsSpin.Value.HasValue)
            {
                byte shadowOpacityValue = (byte)_shadowsSpin.Value.Value;
                // Clamp to 0-255 range
                if (shadowOpacityValue > 255)
                {
                    shadowOpacityValue = 255;
                }
                are.ShadowOpacity = shadowOpacityValue;
            }
            // Handle shadows checkbox state
            if (_shadowsCheck != null)
            {
                if (_shadowsCheck.IsChecked == false)
                {
                    // If shadows checkbox is unchecked, set opacity to 0
                    are.ShadowOpacity = 0;
                }
                else if (_shadowsCheck.IsChecked == true)
                {
                    // If shadows checkbox is checked, ensure opacity is > 0
                    // If opacity is 0 (e.g., from spin box), set to default value of 128
                    if (are.ShadowOpacity == 0)
                    {
                        are.ShadowOpacity = 128;
                        // Update spin box to reflect the default value
                        if (_shadowsSpin != null)
                        {
                            _shadowsSpin.Value = 128;
                        }
                    }
                }
            }

            // Scripts section - matching Python lines 350-354
            // Original: are.on_enter = ResRef(self.ui.onEnterSelect.currentText()) (line 351)
            if (_onEnterSelect != null && !string.IsNullOrEmpty(_onEnterSelect.Text))
            {
                are.OnEnter = new ResRef(_onEnterSelect.Text);
            }
            // Original: are.on_exit = ResRef(self.ui.onExitSelect.currentText()) (line 352)
            if (_onExitSelect != null && !string.IsNullOrEmpty(_onExitSelect.Text))
            {
                are.OnExit = new ResRef(_onExitSelect.Text);
            }
            // Original: are.on_heartbeat = ResRef(self.ui.onHeartbeatSelect.currentText()) (line 353)
            if (_onHeartbeatSelect != null && !string.IsNullOrEmpty(_onHeartbeatSelect.Text))
            {
                are.OnHeartbeat = new ResRef(_onHeartbeatSelect.Text);
            }
            // Original: are.on_user_defined = ResRef(self.ui.onUserDefinedSelect.currentText()) (line 354)
            if (_onUserDefinedSelect != null && !string.IsNullOrEmpty(_onUserDefinedSelect.Text))
            {
                are.OnUserDefined = new ResRef(_onUserDefinedSelect.Text);
            }

            // Comments - matching Python line 357
            // Original: are.comment = self.ui.commentsEdit.toPlainText()
            if (_commentsEdit != null)
            {
                are.Comment = _commentsEdit.Text ?? "";
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:250-277
            // Original: def build(self) -> tuple[bytes, bytes]:
            // Detect game from installation - supports all engines (Odyssey K1/K2, Aurora NWN, Eclipse DA/ME)
            // Default to K1 if no installation is provided (for backward compatibility)
            Game game = _installation?.Game ?? Game.K1;
            var gff = AREHelpers.DismantleAre(are, game);

            // Preserve unmodified fields from original GFF that aren't yet supported by ARE object model
            // This ensures roundtrip tests pass by maintaining all original data
            if (_originalGff != null)
            {
                var originalRoot = _originalGff.Root;
                var newRoot = gff.Root;

                // List of fields that AREHelpers.DismantleAre explicitly sets - preserve original if type/value differs
                // Fields that may have type mismatches or extraction issues
                var fieldsSetByDismantle = new System.Collections.Generic.HashSet<string>
                {
                    "Tag", "Name", "AlphaTest", "CameraStyle", "DefaultEnvMap",
                    "Grass_TexName", "Grass_Density", "Grass_QuadSize",
                    "Grass_Prob_LL", "Grass_Prob_LR", "Grass_Prob_UL", "Grass_Prob_UR",
                    "SunFogNear", "SunFogFar", "WindPower",
                    "OnEnter", "OnExit", "OnHeartbeat", "OnUserDefined"
                    // Note: Map is handled specially below to preserve nested fields
                };

                // Handle Map struct specially - copy all original Map fields that aren't set by DismantleAre
                if (originalRoot.Exists("Map") && newRoot.Exists("Map"))
                {
                    var originalMap = originalRoot.GetStruct("Map");
                    var newMap = newRoot.GetStruct("Map");

                    // Copy all fields from original Map struct that aren't in new Map
                    foreach (var (label, fieldType, value) in originalMap)
                    {
                        if (!newMap.Exists(label))
                        {
                            // Copy the field preserving its type and value
                            CopyGffField(originalMap, newMap, label, fieldType);
                        }
                    }
                }

                // Special handling for fields that may have type/value mismatches
                // ShadowOpacity is now correctly handled as UInt8 in both ConstructAre and DismantleAre
                // No special handling needed - both read and write as UInt8

                // Preserve original SunFogOn if values don't match (ConstructAre/DismantleAre may have conversion issues)
                if (originalRoot.Exists("SunFogOn") && newRoot.Exists("SunFogOn"))
                {
                    var originalSunFogOn = originalRoot.GetUInt8("SunFogOn");
                    var newSunFogOn = newRoot.GetUInt8("SunFogOn");
                    if (originalSunFogOn != newSunFogOn)
                    {
                        // Restore original value
                        newRoot.SetUInt8("SunFogOn", originalSunFogOn);
                    }
                }

                // Preserve original AlphaTest (engine uses float: swkotor.exe: 0x00508c50 line 303-304, swkotor2.exe: 0x004e3ff0 line 307-308)
                if (originalRoot.Exists("AlphaTest"))
                {
                    var originalAlphaType = originalRoot.GetFieldType("AlphaTest");
                    if (originalAlphaType == GFFFieldType.Single)
                    {
                        // Preserve original float value
                        var originalAlpha = originalRoot.GetSingle("AlphaTest");
                        newRoot.SetSingle("AlphaTest", originalAlpha);
                    }
                }

                // Copy all fields from original that aren't explicitly set by DismantleAre
                foreach (var (label, fieldType, value) in originalRoot)
                {
                    if (!fieldsSetByDismantle.Contains(label))
                    {
                        // Copy the field preserving its type and value
                        switch (fieldType)
                        {
                            case GFFFieldType.UInt8:
                                newRoot.SetUInt8(label, originalRoot.GetUInt8(label));
                                break;
                            case GFFFieldType.Int8:
                                newRoot.SetInt8(label, originalRoot.GetInt8(label));
                                break;
                            case GFFFieldType.UInt16:
                                newRoot.SetUInt16(label, originalRoot.GetUInt16(label));
                                break;
                            case GFFFieldType.Int16:
                                newRoot.SetInt16(label, originalRoot.GetInt16(label));
                                break;
                            case GFFFieldType.UInt32:
                                newRoot.SetUInt32(label, originalRoot.GetUInt32(label));
                                break;
                            case GFFFieldType.Int32:
                                newRoot.SetInt32(label, originalRoot.GetInt32(label));
                                break;
                            case GFFFieldType.UInt64:
                                newRoot.SetUInt64(label, originalRoot.GetUInt64(label));
                                break;
                            case GFFFieldType.Int64:
                                newRoot.SetInt64(label, originalRoot.GetInt64(label));
                                break;
                            case GFFFieldType.Single:
                                newRoot.SetSingle(label, originalRoot.GetSingle(label));
                                break;
                            case GFFFieldType.Double:
                                newRoot.SetDouble(label, originalRoot.GetDouble(label));
                                break;
                            case GFFFieldType.String:
                                newRoot.SetString(label, originalRoot.GetString(label));
                                break;
                            case GFFFieldType.ResRef:
                                newRoot.SetResRef(label, originalRoot.GetResRef(label));
                                break;
                            case GFFFieldType.LocalizedString:
                                newRoot.SetLocString(label, originalRoot.GetLocString(label));
                                break;
                            case GFFFieldType.Binary:
                                newRoot.SetBinary(label, originalRoot.GetBinary(label));
                                break;
                            case GFFFieldType.Vector3:
                                newRoot.SetVector3(label, originalRoot.GetVector3(label));
                                break;
                            case GFFFieldType.Vector4:
                                newRoot.SetVector4(label, originalRoot.GetVector4(label));
                                break;
                            case GFFFieldType.Struct:
                                // For nested structs, merge fields instead of replacing entire struct
                                // This preserves fields in nested structs like Map
                                var originalStruct = originalRoot.GetStruct(label);
                                var newStruct = newRoot.GetStruct(label);
                                if (originalStruct != null && newStruct != null)
                                {
                                    // Copy fields from original struct that don't exist in new struct
                                    foreach (var (structLabel, structFieldType, structValue) in originalStruct)
                                    {
                                        if (!newStruct.Exists(structLabel))
                                        {
                                            CopyGffField(originalStruct, newStruct, structLabel, structFieldType);
                                        }
                                    }
                                }
                                else if (originalStruct != null)
                                {
                                    // If new struct doesn't exist, copy the whole thing
                                    newRoot.SetStruct(label, originalStruct);
                                }
                                break;
                            case GFFFieldType.List:
                                // Copy lists (like Rooms)
                                var originalList = originalRoot.GetList(label);
                                if (originalList != null)
                                {
                                    newRoot.SetList(label, originalList);
                                }
                                break;
                        }
                    }
                    else if (label == "Rooms")
                    {
                        // Always preserve Rooms list even if DismantleAre tries to set it empty
                        var originalRooms = originalRoot.GetList("Rooms");
                        if (originalRooms != null && originalRooms.Count > 0)
                        {
                            newRoot.SetList("Rooms", originalRooms);
                        }
                    }
                }
            }

            byte[] data = GFFAuto.BytesGff(gff, ResourceType.ARE);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:302-310
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _are = new ARE();
            _originalGff = null; // Clear original GFF when creating new file
            // Clear UI
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:386-391
        // Original: def change_name(self):
        public void ChangeName()
        {
            if (_installation == null)
            {
                throw new InvalidOperationException("Installation is not set");
            }

            LocalizedString currentName = _nameEdit?.GetLocString() ?? _are?.Name ?? LocalizedString.FromInvalid();
            var dialog = new LocalizedStringDialog(this, _installation, currentName);
            if (dialog.ShowDialog())
            {
                if (_are != null)
                {
                    _are.Name = dialog.LocString;
                }
                if (_nameEdit != null)
                {
                    _nameEdit.SetLocString(dialog.LocString);
                }
            }
        }

        public override void SaveAs()
        {
            Save();
        }

        // Helper method to copy a GFF field from one struct to another, preserving type
        private static void CopyGffField(GFFStruct source, GFFStruct destination, string label, GFFFieldType fieldType)
        {
            switch (fieldType)
            {
                case GFFFieldType.UInt8:
                    destination.SetUInt8(label, source.GetUInt8(label));
                    break;
                case GFFFieldType.Int8:
                    destination.SetInt8(label, source.GetInt8(label));
                    break;
                case GFFFieldType.UInt16:
                    destination.SetUInt16(label, source.GetUInt16(label));
                    break;
                case GFFFieldType.Int16:
                    destination.SetInt16(label, source.GetInt16(label));
                    break;
                case GFFFieldType.UInt32:
                    destination.SetUInt32(label, source.GetUInt32(label));
                    break;
                case GFFFieldType.Int32:
                    destination.SetInt32(label, source.GetInt32(label));
                    break;
                case GFFFieldType.UInt64:
                    destination.SetUInt64(label, source.GetUInt64(label));
                    break;
                case GFFFieldType.Int64:
                    destination.SetInt64(label, source.GetInt64(label));
                    break;
                case GFFFieldType.Single:
                    destination.SetSingle(label, source.GetSingle(label));
                    break;
                case GFFFieldType.Double:
                    destination.SetDouble(label, source.GetDouble(label));
                    break;
                case GFFFieldType.String:
                    destination.SetString(label, source.GetString(label));
                    break;
                case GFFFieldType.ResRef:
                    destination.SetResRef(label, source.GetResRef(label));
                    break;
                case GFFFieldType.LocalizedString:
                    destination.SetLocString(label, source.GetLocString(label));
                    break;
                case GFFFieldType.Binary:
                    destination.SetBinary(label, source.GetBinary(label));
                    break;
                case GFFFieldType.Vector3:
                    destination.SetVector3(label, source.GetVector3(label));
                    break;
                case GFFFieldType.Vector4:
                    destination.SetVector4(label, source.GetVector4(label));
                    break;
                case GFFFieldType.Struct:
                    destination.SetStruct(label, source.GetStruct(label));
                    break;
                case GFFFieldType.List:
                    destination.SetList(label, source.GetList(label));
                    break;
            }
        }

        // Matching Python: deepcopy(self._are)
        private static ARE CopyAre(ARE source)
        {
            var copy = new ARE();

            // Copy all properties from source to copy
            copy.Name = CopyLocalizedString(source.Name);
            copy.Tag = source.Tag;
            copy.AlphaTest = source.AlphaTest;
            copy.CameraStyle = source.CameraStyle;
            copy.AlphaTest = source.AlphaTest;
            copy.DefaultEnvMap = source.DefaultEnvMap;
            copy.DisableTransit = source.DisableTransit;
            copy.Unescapable = source.Unescapable;
            copy.StealthXp = source.StealthXp;
            copy.StealthXpMax = source.StealthXpMax;
            copy.StealthXpLoss = source.StealthXpLoss;
            copy.NorthAxis = source.NorthAxis;
            copy.MapZoom = source.MapZoom;
            copy.MapResX = source.MapResX;
            copy.MapPoint1 = source.MapPoint1;
            copy.MapPoint2 = source.MapPoint2;
            copy.WorldPoint1 = source.WorldPoint1;
            copy.WorldPoint2 = source.WorldPoint2;
            copy.GrassTexture = source.GrassTexture;
            copy.GrassDensity = source.GrassDensity;
            copy.GrassSize = source.GrassSize;
            copy.GrassProbLL = source.GrassProbLL;
            copy.GrassProbLR = source.GrassProbLR;
            copy.GrassProbUL = source.GrassProbUL;
            copy.GrassProbUR = source.GrassProbUR;
            copy.FogEnabled = source.FogEnabled;
            copy.FogNear = source.FogNear;
            copy.FogFar = source.FogFar;
            copy.FogColor = source.FogColor;
            copy.SunFogEnabled = source.SunFogEnabled;
            copy.SunFogNear = source.SunFogNear;
            copy.SunFogFar = source.SunFogFar;
            copy.SunFogColor = source.SunFogColor;
            copy.SunAmbient = source.SunAmbient;
            copy.SunDiffuse = source.SunDiffuse;
            copy.DynamicLight = source.DynamicLight;
            copy.GrassDiffuse = source.GrassDiffuse;
            copy.GrassAmbient = source.GrassAmbient;
            copy.GrassEmissive = source.GrassEmissive;
            copy.DirtyFormula1 = source.DirtyFormula1;
            copy.DirtyFormula2 = source.DirtyFormula2;
            copy.DirtyFormula3 = source.DirtyFormula3;
            copy.WindPower = source.WindPower;
            copy.ShadowOpacity = source.ShadowOpacity;
            copy.ChanceRain = source.ChanceRain;
            copy.ChanceSnow = source.ChanceSnow;
            copy.ChanceLightning = source.ChanceLightning;
            copy.ChancesOfFog = source.ChancesOfFog;
            copy.Weather = source.Weather;
            copy.SkyBox = source.SkyBox;
            copy.MoonAmbient = source.MoonAmbient;
            copy.DawnAmbient = source.DawnAmbient;
            copy.DayAmbient = source.DayAmbient;
            copy.DuskAmbient = source.DuskAmbient;
            copy.NightAmbient = source.NightAmbient;
            copy.DawnDir1 = source.DawnDir1;
            copy.DawnDir2 = source.DawnDir2;
            copy.DawnDir3 = source.DawnDir3;
            copy.DayDir1 = source.DayDir1;
            copy.DayDir2 = source.DayDir2;
            copy.DayDir3 = source.DayDir3;
            copy.DuskDir1 = source.DuskDir1;
            copy.DuskDir2 = source.DuskDir2;
            copy.DuskDir3 = source.DuskDir3;
            copy.NightDir1 = source.NightDir1;
            copy.NightDir2 = source.NightDir2;
            copy.NightDir3 = source.NightDir3;
            copy.DawnColor1 = source.DawnColor1;
            copy.DawnColor2 = source.DawnColor2;
            copy.DawnColor3 = source.DawnColor3;
            copy.DayColor1 = source.DayColor1;
            copy.DayColor2 = source.DayColor2;
            copy.DayColor3 = source.DayColor3;
            copy.DuskColor1 = source.DuskColor1;
            copy.DuskColor2 = source.DuskColor2;
            copy.DuskColor3 = source.DuskColor3;
            copy.NightColor1 = source.NightColor1;
            copy.NightColor2 = source.NightColor2;
            copy.NightColor3 = source.NightColor3;
            copy.OnEnter = source.OnEnter;
            copy.OnExit = source.OnExit;
            copy.OnHeartbeat = source.OnHeartbeat;
            copy.OnUserDefined = source.OnUserDefined;
            copy.OnEnter2 = source.OnEnter2;
            copy.OnExit2 = source.OnExit2;
            copy.OnHeartbeat2 = source.OnHeartbeat2;
            copy.OnUserDefined2 = source.OnUserDefined2;
            copy.LoadScreenID = source.LoadScreenID;

            // Copy lists
            copy.AreaList = new System.Collections.Generic.List<string>(source.AreaList);
            copy.MapList = new System.Collections.Generic.List<ResRef>(source.MapList);

            return copy;
        }

        private static LocalizedString CopyLocalizedString(LocalizedString source)
        {
            if (source == null)
            {
                return LocalizedString.FromInvalid();
            }
            var copy = new LocalizedString(source.StringRef);
            foreach (var (language, gender, text) in source)
            {
                copy.SetData(language, gender, text);
            }
            return copy;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:392-393
        // Original: def generate_tag(self):
        private void GenerateTag()
        {
            // Matching Python: self.ui.tagEdit.setText("newarea" if self._resname is None or self._resname == "" else self._resname)
            if (_tagEdit != null)
            {
                _tagEdit.Text = string.IsNullOrEmpty(_resname) ? "newarea" : _resname;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:408-411
        // Original: def redoMinimap(self):
        // TODO: STUB - Minimap renderer is not yet available, so this is a placeholder
        // When minimap renderer is implemented, this should call: minimapRenderer.set_minimap(are, _minimap)
        private void RedoMinimap()
        {
            // Matching Python: if self._minimap: are: ARE = self._buildARE(); self.ui.minimapRenderer.set_minimap(are, self._minimap)
            // For now, this is a no-op until minimap renderer is available
            // The method exists to allow event handlers to be connected without errors
        }

        // Helper method to load default camera styles as fallback
        // Used when cameras.2da cannot be loaded from installation
        private void LoadDefaultCameraStyles()
        {
            if (_cameraStyleSelect != null)
            {
                _cameraStyleSelect.Items.Clear();
                _cameraStyleSelect.Items.Add("Standard");
                _cameraStyleSelect.Items.Add("Close");
                _cameraStyleSelect.Items.Add("Far");
                _cameraStyleSelect.Items.Add("Top Down");
                _cameraStyleSelect.Items.Add("Free Look");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:76-104
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            // Tag generate button - matching Python: self.ui.tagGenerateButton.clicked.connect(self.generate_tag)
            if (_tagGenerateButton != null)
            {
                _tagGenerateButton.Click += (s, e) => GenerateTag();
            }

            // Matching Python: self.ui.mapAxisSelect.currentIndexChanged.connect(self.redoMinimap) (line 89)
            if (_mapAxisSelect != null)
            {
                _mapAxisSelect.SelectedIndexChanged += (s, e) => RedoMinimap();
            }
            // Matching Python: self.ui.mapWorldX1Spin.valueChanged.connect(self.redoMinimap) (line 90)
            if (_mapWorldX1Spin != null)
            {
                _mapWorldX1Spin.ValueChanged += (s, e) => RedoMinimap();
            }
            // Matching Python: self.ui.mapWorldX2Spin.valueChanged.connect(self.redoMinimap) (line 91)
            if (_mapWorldX2Spin != null)
            {
                _mapWorldX2Spin.ValueChanged += (s, e) => RedoMinimap();
            }
            // Matching Python: self.ui.mapWorldY1Spin.valueChanged.connect(self.redoMinimap) (line 92)
            if (_mapWorldY1Spin != null)
            {
                _mapWorldY1Spin.ValueChanged += (s, e) => RedoMinimap();
            }
            // Matching Python: self.ui.mapWorldY2Spin.valueChanged.connect(self.redoMinimap) (line 93)
            if (_mapWorldY2Spin != null)
            {
                _mapWorldY2Spin.ValueChanged += (s, e) => RedoMinimap();
            }
            // Matching Python: self.ui.mapImageX1Spin.valueChanged.connect(self.redoMinimap) (line 94)
            if (_mapImageX1Spin != null)
            {
                _mapImageX1Spin.ValueChanged += (s, e) => RedoMinimap();
            }
            // Matching Python: self.ui.mapImageX2Spin.valueChanged.connect(self.redoMinimap) (line 95)
            if (_mapImageX2Spin != null)
            {
                _mapImageX2Spin.ValueChanged += (s, e) => RedoMinimap();
            }
            // Matching Python: self.ui.mapImageY1Spin.valueChanged.connect(self.redoMinimap) (line 96)
            if (_mapImageY1Spin != null)
            {
                _mapImageY1Spin.ValueChanged += (s, e) => RedoMinimap();
            }
            // Matching Python: self.ui.mapImageY2Spin.valueChanged.connect(self.redoMinimap) (line 97)
            if (_mapImageY2Spin != null)
            {
                _mapImageY2Spin.ValueChanged += (s, e) => RedoMinimap();
            }

            // Script combo boxes will be populated in LoadARE after filepath is set
            _relevantScriptResnames = new List<string>();

            // Setup context menus for script combo boxes
            SetupScriptComboBoxContextMenu(_onEnterSelect, "OnEnter Script");
            SetupScriptComboBoxContextMenu(_onExitSelect, "OnExit Script");
            SetupScriptComboBoxContextMenu(_onHeartbeatSelect, "OnHeartbeat Script");
            SetupScriptComboBoxContextMenu(_onUserDefinedSelect, "OnUserDefined Script");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:106-132
        // Original: def _setup_installation(self, installation: HTInstallation):
        private void SetupInstallation(HTInstallation installation)
        {
            _installation = installation;

            // Setup name edit with installation - matching Python: self.ui.nameEdit.set_installation(installation)
            if (_nameEdit != null)
            {
                _nameEdit.SetInstallation(installation);
            }

            // Setup camera styles from cameras.2da - matching Python lines 111-117
            // This is already done in SetupUI, but we can refresh here if needed

            // Show/hide TSL-only fields - matching Python lines 119-127
            bool isTsl = installation != null && installation.Game == Game.K2;
            if (_grassEmissiveEdit != null)
            {
                _grassEmissiveEdit.IsVisible = isTsl;
            }
            if (_rainCheck != null)
            {
                _rainCheck.IsVisible = isTsl;
                _rainCheck.IsEnabled = isTsl;
            }
            if (_snowCheck != null)
            {
                _snowCheck.IsVisible = isTsl;
                _snowCheck.IsEnabled = isTsl;
            }
            if (_lightningCheck != null)
            {
                _lightningCheck.IsVisible = isTsl;
                _lightningCheck.IsEnabled = isTsl;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:129-132
            // Original: installation.setup_file_context_menu(self.ui.onEnterSelect, [ResourceType.NSS, ResourceType.NCS])
            //           installation.setup_file_context_menu(self.ui.onExitSelect, [ResourceType.NSS, ResourceType.NCS])
            //           installation.setup_file_context_menu(self.ui.onHeartbeatSelect, [ResourceType.NSS, ResourceType.NCS])
            //           installation.setup_file_context_menu(self.ui.onUserDefinedSelect, [ResourceType.NSS, ResourceType.NCS])
            // Note: Context menus are set up in SetupSignals() (lines 1779-1782) which is called during initialization.
            // The _installation field is set here, which the context menu handlers use when opening scripts.
        }

        // Create context menu for script ComboBox controls
        // Matching PyKotor pattern from UTEEditor - allows opening existing scripts, creating new scripts, or viewing resource details
        private void SetupScriptComboBoxContextMenu(ComboBox comboBox, string scriptTypeName)
        {
            if (comboBox == null)
            {
                return;
            }

            var contextMenu = new ContextMenu();
            var menuItems = new List<MenuItem>();

            // "Open in Editor" menu item - opens the script if it exists
            var openInEditorItem = new MenuItem
            {
                Header = "Open in Editor",
                IsEnabled = false
            };
            openInEditorItem.Click += (sender, e) => OpenScriptInEditor(comboBox, scriptTypeName);
            menuItems.Add(openInEditorItem);

            // Enable/disable based on whether script name is set
            comboBox.SelectionChanged += (sender, e) =>
            {
                string text = comboBox.SelectedItem?.ToString() ?? comboBox.Text ?? string.Empty;
                openInEditorItem.IsEnabled = !string.IsNullOrWhiteSpace(text);
            };

            // "Create New Script" menu item - creates a new NSS file
            var createNewItem = new MenuItem
            {
                Header = "Create New Script"
            };
            createNewItem.Click += (sender, e) => CreateNewScript(comboBox, scriptTypeName);
            menuItems.Add(createNewItem);

            // "View Resource Location" menu item - shows where the script is located
            var viewLocationItem = new MenuItem
            {
                Header = "View Resource Location",
                IsEnabled = false
            };
            viewLocationItem.Click += (sender, e) => ViewScriptResourceLocation(comboBox, scriptTypeName);
            menuItems.Add(viewLocationItem);

            // Enable/disable based on whether script name is set
            comboBox.SelectionChanged += (sender, e) =>
            {
                string text = comboBox.SelectedItem?.ToString() ?? comboBox.Text ?? string.Empty;
                viewLocationItem.IsEnabled = !string.IsNullOrWhiteSpace(text);
            };

            // Add menu items to context menu
            foreach (var item in menuItems)
            {
                contextMenu.Items.Add(item);
            }
            // Add separator before last item
            contextMenu.Items.Insert(menuItems.Count - 1, new Separator());

            comboBox.ContextMenu = contextMenu;
        }

        // Open the script referenced in the ComboBox in an appropriate editor
        private void OpenScriptInEditor(ComboBox comboBox, string scriptTypeName)
        {
            if (comboBox == null || _installation == null)
            {
                return;
            }

            string scriptName = comboBox.Text?.Trim();
            if (string.IsNullOrEmpty(scriptName))
            {
                return;
            }

            try
            {
                // Try to find the script resource (NSS source preferred, fallback to NCS)
                var resourceResult = _installation.Resource(scriptName, ResourceType.NSS, null);
                ResourceType resourceType = ResourceType.NSS;

                if (resourceResult == null)
                {
                    // Try compiled version
                    resourceResult = _installation.Resource(scriptName, ResourceType.NCS, null);
                    resourceType = ResourceType.NCS;
                }

                if (resourceResult == null)
                {
                    // Resource not found
                    System.Console.WriteLine($"Script '{scriptName}' not found in installation.");
                    return;
                }

                // Open the script in the NSS editor
                var fileResource = new FileResource(
                    scriptName,
                    resourceType,
                    resourceResult.Data.Length,
                    0,
                    resourceResult.FilePath
                );

                HolocronToolset.Editors.WindowUtils.OpenResourceEditor(fileResource, _installation, this);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error opening script '{scriptName}': {ex.Message}");
            }
        }

        // Create a new script file and open it in the editor
        private void CreateNewScript(ComboBox comboBox, string scriptTypeName)
        {
            if (_installation == null)
            {
                return;
            }

            try
            {
                // Generate a default script name if not already set
                string scriptName = comboBox.Text?.Trim();
                if (string.IsNullOrEmpty(scriptName))
                {
                    // Generate based on ARE resref and script type
                    string baseName = !string.IsNullOrEmpty(_resname)
                        ? _resname
                        : "m00xx_area_000";
                    scriptName = $"{baseName}_{scriptTypeName.ToLowerInvariant().Replace(" ", "_")}";
                }

                // Limit to 16 characters (ResRef max length)
                if (scriptName.Length > 16)
                {
                    scriptName = scriptName.Substring(0, 16);
                }

                // Create a new NSS editor with empty content
                var nssEditor = new NSSEditor(this, _installation);
                nssEditor.New();

                // Show the editor - user will set the resref when saving
                HolocronToolset.Editors.WindowUtils.AddWindow(nssEditor, show: true);

                // Update the combo box with the suggested script name
                comboBox.Text = scriptName;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error creating new script: {ex.Message}");
            }
        }

        // View the location/details of the script resource
        private void ViewScriptResourceLocation(ComboBox comboBox, string scriptTypeName)
        {
            if (comboBox == null || _installation == null)
            {
                return;
            }

            string scriptName = comboBox.Text?.Trim();
            if (string.IsNullOrEmpty(scriptName))
            {
                return;
            }

            try
            {
                // Find the script resource
                var locations = _installation.Locations(
                    new List<ResourceIdentifier> { new ResourceIdentifier(scriptName, ResourceType.NSS) },
                    null,
                    null);

                var nssIdentifier = new ResourceIdentifier(scriptName, ResourceType.NSS);
                if (locations.Count > 0 && locations.ContainsKey(nssIdentifier) &&
                    locations[nssIdentifier].Count > 0)
                {
                    var foundLocations = locations[nssIdentifier];
                    // Show dialog with all found locations
                    var dialog = new ResourceLocationDialog(
                        this,
                        scriptName,
                        ResourceType.NSS,
                        foundLocations,
                        _installation);
                    dialog.ShowDialog(this);
                }
                else
                {
                    // Try compiled version
                    locations = _installation.Locations(
                        new List<ResourceIdentifier> { new ResourceIdentifier(scriptName, ResourceType.NCS) },
                        null,
                        null);

                    var ncsIdentifier = new ResourceIdentifier(scriptName, ResourceType.NCS);
                    if (locations.Count > 0 && locations.ContainsKey(ncsIdentifier) &&
                        locations[ncsIdentifier].Count > 0)
                    {
                        var foundLocations = locations[ncsIdentifier];
                        // Show dialog with all found locations
                        var dialog = new ResourceLocationDialog(
                            this,
                            scriptName,
                            ResourceType.NCS,
                            foundLocations,
                            _installation);
                        dialog.ShowDialog(this);
                    }
                    else
                    {
                        // Show "not found" message
                        var msgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Resource Not Found",
                            $"Script '{scriptName}' not found in installation.\n\nSearched for:\n- {scriptName}.nss\n- {scriptName}.ncs",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Info);
                        msgBox.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error viewing script location '{scriptName}': {ex.Message}");
            }
        }
    }
}

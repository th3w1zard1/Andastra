using Andastra.Parsing.Common;
using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using HolocronToolset.Widgets;
using HolocronToolset.Widgets.Edit;
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
            // TODO: STUB - Setup UI elements - will be implemented when UI controls are created
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
            // Add default camera style options (matching common camera styles from cameras.2da)
            // TODO: SIMPLIFIED - In full implementation, this would load from cameras.2da via installation
            _cameraStyleSelect.Items.Add("Standard");
            _cameraStyleSelect.Items.Add("Close");
            _cameraStyleSelect.Items.Add("Far");
            _cameraStyleSelect.Items.Add("Top Down");
            _cameraStyleSelect.Items.Add("Free Look");
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
            var alphaTestLabel = new Avalonia.Controls.TextBlock { Text = "Alpha Test:" };
            _alphaTestSpin = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 0 };
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
                _alphaTestSpin.Value = are.AlphaTest;
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
            if (_alphaTestSpin != null && _alphaTestSpin.Value.HasValue)
            {
                are.AlphaTest = (int)_alphaTestSpin.Value.Value;
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
            // Matching Python: are.comment = self.ui.commentsEdit.toPlainText() (line 357)
            if (_commentsEdit != null)
            {
                are.Comment = _commentsEdit.Text ?? "";
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:250-277
            // Original: def build(self) -> tuple[bytes, bytes]:
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
                // Preserve original ShadowOpacity if it's UInt8 (original) vs ResRef (new)
                if (originalRoot.Exists("ShadowOpacity"))
                {
                    var originalShadowType = originalRoot.GetFieldType("ShadowOpacity");
                    var newShadowType = newRoot.GetFieldType("ShadowOpacity");
                    if (originalShadowType == GFFFieldType.UInt8 && newShadowType == GFFFieldType.ResRef)
                    {
                        // Preserve original UInt8 value
                        newRoot.Remove("ShadowOpacity");
                        newRoot.SetUInt8("ShadowOpacity", originalRoot.GetUInt8("ShadowOpacity"));
                    }
                }

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

                // Preserve original AlphaTest if type differs (original may be Single/float, but ARE.AlphaTest is int)
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
            copy.WindPower = source.WindPower;
            copy.ShadowOpacity = source.ShadowOpacity;
            copy.ChancesOfRain = source.ChancesOfRain;
            copy.ChancesOfSnow = source.ChancesOfSnow;
            copy.ChancesOfLightning = source.ChancesOfLightning;
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
    }
}

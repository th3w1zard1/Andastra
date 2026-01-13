using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using HolocronToolset.Widgets;
using HolocronToolset.Widgets.Edit;
using GFFAuto = BioWare.NET.Resource.Formats.GFF.GFFAuto;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:23
    // Original: class UTTEditor(Editor):
    public class UTTEditor : Editor
    {
        private UTT _utt;
        private GFF _originalGff;

        // UI Controls - Basic
        private LocalizedStringEdit _nameEdit;
        private TextBox _tagEdit;
        private Button _tagGenerateButton;
        private TextBox _resrefEdit;
        private Button _resrefGenerateButton;
        private ComboBox _typeSelect;
        private ComboBox2DA _cursorSelect;

        // UI Controls - Advanced
        private CheckBox _autoRemoveKeyCheckbox;
        private TextBox _keyEdit;
        private ComboBox2DA _factionSelect;
        private NumericUpDown _highlightHeightSpin;

        // UI Controls - Trap
        private CheckBox _isTrapCheckbox;
        private CheckBox _activateOnceCheckbox;
        private CheckBox _detectableCheckbox;
        private NumericUpDown _detectDcSpin;
        private CheckBox _disarmableCheckbox;
        private NumericUpDown _disarmDcSpin;
        private ComboBox2DA _trapSelect;

        // UI Controls - Scripts
        private ComboBox _onClickEdit;
        private ComboBox _onDisarmEdit;
        private ComboBox _onEnterSelect;
        private ComboBox _onExitSelect;
        private ComboBox _onHeartbeatSelect;
        private ComboBox _onTrapTriggeredEdit;
        private ComboBox _onUserDefinedSelect;

        // UI Controls - Comments
        private TextBox _commentsEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:24-64
        // Original: def __init__(self, parent, installation):
        public UTTEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Trigger Editor", "trigger",
                new[] { ResourceType.UTT, ResourceType.BTT },
                new[] { ResourceType.UTT, ResourceType.BTT },
                installation)
        {
            _utt = new UTT();
            InitializeComponent();
            SetupSignals();
            if (installation != null)
            {
                SetupInstallation(installation);
            }
            New();
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
            else
            {
                // Try to find controls from XAML
                _nameEdit = EditorHelpers.FindControlSafe<LocalizedStringEdit>(this, "nameEdit");
                _tagEdit = EditorHelpers.FindControlSafe<TextBox>(this, "tagEdit");
                _tagGenerateButton = EditorHelpers.FindControlSafe<Button>(this, "tagGenerateButton");
                _resrefEdit = EditorHelpers.FindControlSafe<TextBox>(this, "resrefEdit");
                _resrefGenerateButton = EditorHelpers.FindControlSafe<Button>(this, "resrefGenerateButton");
                _typeSelect = EditorHelpers.FindControlSafe<ComboBox>(this, "typeSelect");
                _cursorSelect = EditorHelpers.FindControlSafe<ComboBox2DA>(this, "cursorSelect");
                _autoRemoveKeyCheckbox = EditorHelpers.FindControlSafe<CheckBox>(this, "autoRemoveKeyCheckbox");
                _keyEdit = EditorHelpers.FindControlSafe<TextBox>(this, "keyEdit");
                _factionSelect = EditorHelpers.FindControlSafe<ComboBox2DA>(this, "factionSelect");
                _highlightHeightSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "highlightHeightSpin");
                _isTrapCheckbox = EditorHelpers.FindControlSafe<CheckBox>(this, "isTrapCheckbox");
                _activateOnceCheckbox = EditorHelpers.FindControlSafe<CheckBox>(this, "activateOnceCheckbox");
                _detectableCheckbox = EditorHelpers.FindControlSafe<CheckBox>(this, "detectableCheckbox");
                _detectDcSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "detectDcSpin");
                _disarmableCheckbox = EditorHelpers.FindControlSafe<CheckBox>(this, "disarmableCheckbox");
                _disarmDcSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "disarmDcSpin");
                _trapSelect = EditorHelpers.FindControlSafe<ComboBox2DA>(this, "trapSelect");
                _onClickEdit = EditorHelpers.FindControlSafe<ComboBox>(this, "onClickEdit");
                _onDisarmEdit = EditorHelpers.FindControlSafe<ComboBox>(this, "onDisarmEdit");
                _onEnterSelect = EditorHelpers.FindControlSafe<ComboBox>(this, "onEnterSelect");
                _onExitSelect = EditorHelpers.FindControlSafe<ComboBox>(this, "onExitSelect");
                _onHeartbeatSelect = EditorHelpers.FindControlSafe<ComboBox>(this, "onHeartbeatSelect");
                _onTrapTriggeredEdit = EditorHelpers.FindControlSafe<ComboBox>(this, "onTrapTriggeredEdit");
                _onUserDefinedSelect = EditorHelpers.FindControlSafe<ComboBox>(this, "onUserDefinedSelect");
                _commentsEdit = EditorHelpers.FindControlSafe<TextBox>(this, "commentsEdit");

                // If any critical controls are missing, fall back to programmatic UI
                if (_nameEdit == null || _tagEdit == null || _resrefEdit == null || _commentsEdit == null)
                {
                    SetupProgrammaticUI();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:66-68
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_tagGenerateButton != null)
            {
                _tagGenerateButton.Click += (s, e) => GenerateTag();
            }
            if (_resrefGenerateButton != null)
            {
                _resrefGenerateButton.Click += (s, e) => GenerateResref();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:70-119
        // Original: def _setup_installation(self, installation: HTInstallation):
        private void SetupInstallation(HTInstallation installation)
        {
            _installation = installation;
            if (_nameEdit != null)
            {
                _nameEdit.SetInstallation(installation);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:77-79
            // Original: cursors: 2DA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_CURSORS)
            TwoDA cursors = installation?.HtGetCache2DA(HTInstallation.TwoDACursors);
            TwoDA factions = installation?.HtGetCache2DA(HTInstallation.TwoDAFactions);
            TwoDA traps = installation?.HtGetCache2DA(HTInstallation.TwoDATraps);

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:81-86
            // Original: if cursors: self.ui.cursorSelect.set_context(cursors, installation, HTInstallation.TwoDA_CURSORS)
            if (cursors != null && _cursorSelect != null)
            {
                _cursorSelect.SetContext(cursors, installation, HTInstallation.TwoDACursors);
            }
            if (factions != null && _factionSelect != null)
            {
                _factionSelect.SetContext(factions, installation, HTInstallation.TwoDAFactions);
            }
            if (traps != null && _trapSelect != null)
            {
                _trapSelect.SetContext(traps, installation, HTInstallation.TwoDATraps);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:88-93
            // Original: if cursors: self.ui.cursorSelect.set_items(cursors.get_column("label"))
            if (cursors != null && _cursorSelect != null)
            {
                try
                {
                    List<string> cursorLabels = cursors.GetColumn("label");
                    _cursorSelect.SetItems(cursorLabels, sortAlphabetically: true);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Failed to set cursor items: {ex.Message}");
                }
            }
            if (factions != null && _factionSelect != null)
            {
                try
                {
                    List<string> factionLabels = factions.GetColumn("label");
                    _factionSelect.SetItems(factionLabels, sortAlphabetically: true);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Failed to set faction items: {ex.Message}");
                }
            }
            if (traps != null && _trapSelect != null)
            {
                try
                {
                    List<string> trapLabels = traps.GetColumn("label");
                    _trapSelect.SetItems(trapLabels, sortAlphabetically: true);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Failed to set trap items: {ex.Message}");
                }
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:95-104
            // Original: self.relevant_script_resnames: list[str] = sorted(...)
            // Setup script combo boxes with relevant script resources
            if (installation != null)
            {
                try
                {
                    List<string> relevantScriptResnames = new List<string>();
                    var relevantResources = installation.GetRelevantResources(ResourceType.NCS, _filepath);
                    if (relevantResources != null)
                    {
                        foreach (var res in relevantResources)
                        {
                            if (res != null && !string.IsNullOrEmpty(res.ResName))
                            {
                                string resname = res.ResName.ToLowerInvariant();
                                if (!relevantScriptResnames.Contains(resname))
                                {
                                    relevantScriptResnames.Add(resname);
                                }
                            }
                        }
                    }
                    relevantScriptResnames.Sort();

                    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:106-119
                    // Original: self.ui.onClickEdit.populate_combo_box(self.relevant_script_resnames)
                    // Populate script combo boxes with relevant script resources
                    PopulateScriptComboBox(_onClickEdit, relevantScriptResnames, installation);
                    PopulateScriptComboBox(_onDisarmEdit, relevantScriptResnames, installation);
                    PopulateScriptComboBox(_onEnterSelect, relevantScriptResnames, installation);
                    PopulateScriptComboBox(_onExitSelect, relevantScriptResnames, installation);
                    PopulateScriptComboBox(_onHeartbeatSelect, relevantScriptResnames, installation);
                    PopulateScriptComboBox(_onTrapTriggeredEdit, relevantScriptResnames, installation);
                    PopulateScriptComboBox(_onUserDefinedSelect, relevantScriptResnames, installation);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Failed to setup script combo boxes: {ex.Message}");
                }
            }
        }

        private void SetupProgrammaticUI()
        {
            var scrollViewer = new ScrollViewer();
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Tab Control
            var tabControl = new TabControl();

            // Basic Tab
            var basicTab = new TabItem { Header = "Basic" };
            var basicPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Name
            var nameLabel = new TextBlock { Text = "Name:" };
            _nameEdit = new LocalizedStringEdit();
            if (_installation != null)
            {
                _nameEdit.SetInstallation(_installation);
            }
            basicPanel.Children.Add(nameLabel);
            basicPanel.Children.Add(_nameEdit);

            // Tag
            var tagLabel = new TextBlock { Text = "Tag:" };
            var tagPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _tagEdit = new TextBox();
            _tagGenerateButton = new Button { Content = "-", Width = 26 };
            _tagGenerateButton.Click += (s, e) => GenerateTag();
            tagPanel.Children.Add(_tagEdit);
            tagPanel.Children.Add(_tagGenerateButton);
            basicPanel.Children.Add(tagLabel);
            basicPanel.Children.Add(tagPanel);

            // ResRef
            var resrefLabel = new TextBlock { Text = "ResRef:" };
            var resrefPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _resrefEdit = new TextBox { MaxLength = 16 };
            _resrefGenerateButton = new Button { Content = "-", Width = 26 };
            _resrefGenerateButton.Click += (s, e) => GenerateResref();
            resrefPanel.Children.Add(_resrefEdit);
            resrefPanel.Children.Add(_resrefGenerateButton);
            basicPanel.Children.Add(resrefLabel);
            basicPanel.Children.Add(resrefPanel);

            // Type
            var typeLabel = new TextBlock { Text = "Type:" };
            _typeSelect = new ComboBox();
            _typeSelect.Items.Add("Generic");
            _typeSelect.Items.Add("Transition");
            _typeSelect.Items.Add("Trap");
            _typeSelect.SelectedIndex = 0;
            basicPanel.Children.Add(typeLabel);
            basicPanel.Children.Add(_typeSelect);

            // Cursor
            var cursorLabel = new TextBlock { Text = "Cursor:" };
            _cursorSelect = new ComboBox2DA();
            basicPanel.Children.Add(cursorLabel);
            basicPanel.Children.Add(_cursorSelect);

            basicTab.Content = basicPanel;
            tabControl.Items.Add(basicTab);

            // Advanced Tab
            var advancedTab = new TabItem { Header = "Advanced" };
            var advancedPanel = new StackPanel { Orientation = Orientation.Vertical };

            _autoRemoveKeyCheckbox = new CheckBox { Content = "Auto Remove Key" };
            var keyLabel = new TextBlock { Text = "Key Name:" };
            _keyEdit = new TextBox();
            var factionLabel = new TextBlock { Text = "Faction:" };
            _factionSelect = new ComboBox2DA();
            var highlightHeightLabel = new TextBlock { Text = "Highlight Height:" };
            _highlightHeightSpin = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue };

            advancedPanel.Children.Add(_autoRemoveKeyCheckbox);
            advancedPanel.Children.Add(keyLabel);
            advancedPanel.Children.Add(_keyEdit);
            advancedPanel.Children.Add(factionLabel);
            advancedPanel.Children.Add(_factionSelect);
            advancedPanel.Children.Add(highlightHeightLabel);
            advancedPanel.Children.Add(_highlightHeightSpin);

            advancedTab.Content = advancedPanel;
            tabControl.Items.Add(advancedTab);

            // Trap Tab
            var trapTab = new TabItem { Header = "Trap" };
            var trapPanel = new StackPanel { Orientation = Orientation.Vertical };

            _isTrapCheckbox = new CheckBox { Content = "Is a trap" };
            _activateOnceCheckbox = new CheckBox { Content = "Activate Once" };
            var trapTypeLabel = new TextBlock { Text = "Trap Type:" };
            _trapSelect = new ComboBox2DA();
            _detectableCheckbox = new CheckBox { Content = "Detectable" };
            var detectDcLabel = new TextBlock { Text = "Detect DC:" };
            _detectDcSpin = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue };
            _disarmableCheckbox = new CheckBox { Content = "Disarmable" };
            var disarmDcLabel = new TextBlock { Text = "Disarm DC:" };
            _disarmDcSpin = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue };

            trapPanel.Children.Add(_isTrapCheckbox);
            trapPanel.Children.Add(_activateOnceCheckbox);
            trapPanel.Children.Add(trapTypeLabel);
            trapPanel.Children.Add(_trapSelect);
            trapPanel.Children.Add(_detectableCheckbox);
            trapPanel.Children.Add(detectDcLabel);
            trapPanel.Children.Add(_detectDcSpin);
            trapPanel.Children.Add(_disarmableCheckbox);
            trapPanel.Children.Add(disarmDcLabel);
            trapPanel.Children.Add(_disarmDcSpin);

            trapTab.Content = trapPanel;
            tabControl.Items.Add(trapTab);

            // Scripts Tab
            var scriptsTab = new TabItem { Header = "Scripts" };
            var scriptsPanel = new StackPanel { Orientation = Orientation.Vertical };

            var onClickLabel = new TextBlock { Text = "OnClick:" };
            _onClickEdit = new ComboBox { IsEditable = true };
            var onDisarmLabel = new TextBlock { Text = "OnDisarm:" };
            _onDisarmEdit = new ComboBox { IsEditable = true };
            var onEnterLabel = new TextBlock { Text = "OnEnter:" };
            _onEnterSelect = new ComboBox { IsEditable = true };
            var onExitLabel = new TextBlock { Text = "OnExit:" };
            _onExitSelect = new ComboBox { IsEditable = true };
            var onHeartbeatLabel = new TextBlock { Text = "OnHeartbeat:" };
            _onHeartbeatSelect = new ComboBox { IsEditable = true };
            var onTrapTriggeredLabel = new TextBlock { Text = "OnTrapTriggered:" };
            _onTrapTriggeredEdit = new ComboBox { IsEditable = true };
            var onUserDefinedLabel = new TextBlock { Text = "OnUserDefined:" };
            _onUserDefinedSelect = new ComboBox { IsEditable = true };

            scriptsPanel.Children.Add(onClickLabel);
            scriptsPanel.Children.Add(_onClickEdit);
            scriptsPanel.Children.Add(onDisarmLabel);
            scriptsPanel.Children.Add(_onDisarmEdit);
            scriptsPanel.Children.Add(onEnterLabel);
            scriptsPanel.Children.Add(_onEnterSelect);
            scriptsPanel.Children.Add(onExitLabel);
            scriptsPanel.Children.Add(_onExitSelect);
            scriptsPanel.Children.Add(onHeartbeatLabel);
            scriptsPanel.Children.Add(_onHeartbeatSelect);
            scriptsPanel.Children.Add(onTrapTriggeredLabel);
            scriptsPanel.Children.Add(_onTrapTriggeredEdit);
            scriptsPanel.Children.Add(onUserDefinedLabel);
            scriptsPanel.Children.Add(_onUserDefinedSelect);

            scriptsTab.Content = scriptsPanel;
            tabControl.Items.Add(scriptsTab);

            // Comments Tab
            var commentsTab = new TabItem { Header = "Comments" };
            _commentsEdit = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            commentsTab.Content = _commentsEdit;
            tabControl.Items.Add(commentsTab);

            mainPanel.Children.Add(tabControl);
            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:121-131
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            try
            {
                // Store original GFF for roundtrip preservation
                _originalGff = GFF.FromBytes(data);
                var utt = UTTAuto.ReadUtt(data);
                LoadUTT(utt);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load UTT: {ex}");
                New();
                _originalGff = null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:133-185
        // Original: def _loadUTT(self, utt: UTT):
        private void LoadUTT(UTT utt)
        {
            _utt = utt;

            // Basic
            if (_nameEdit != null)
            {
                _nameEdit.SetLocString(utt.Name);
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = utt.Tag ?? "";
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = utt.ResRef?.ToString() ?? "";
            }
            if (_typeSelect != null)
            {
                _typeSelect.SelectedIndex = utt.TypeId;
            }
            if (_cursorSelect != null)
            {
                _cursorSelect.SetSelectedIndex(utt.Cursor);
            }

            // Advanced
            if (_autoRemoveKeyCheckbox != null)
            {
                _autoRemoveKeyCheckbox.IsChecked = utt.AutoRemoveKey;
            }
            if (_keyEdit != null)
            {
                _keyEdit.Text = utt.KeyName ?? "";
            }
            if (_factionSelect != null)
            {
                _factionSelect.SetSelectedIndex(utt.FactionId);
            }
            if (_highlightHeightSpin != null)
            {
                _highlightHeightSpin.Value = (decimal)utt.HighlightHeight;
            }

            // Trap
            if (_isTrapCheckbox != null)
            {
                _isTrapCheckbox.IsChecked = utt.IsTrap;
            }
            if (_activateOnceCheckbox != null)
            {
                _activateOnceCheckbox.IsChecked = utt.TrapOnce;
            }
            if (_detectableCheckbox != null)
            {
                _detectableCheckbox.IsChecked = utt.TrapDetectable;
            }
            if (_detectDcSpin != null)
            {
                _detectDcSpin.Value = utt.TrapDetectDc;
            }
            if (_disarmableCheckbox != null)
            {
                _disarmableCheckbox.IsChecked = utt.TrapDisarmable;
            }
            if (_disarmDcSpin != null)
            {
                _disarmDcSpin.Value = utt.TrapDisarmDc;
            }
            if (_trapSelect != null)
            {
                _trapSelect.SetSelectedIndex(utt.TrapType);
            }

            // Scripts
            if (_onClickEdit != null)
            {
                _onClickEdit.Text = utt.OnClickScript?.ToString() ?? "";
            }
            if (_onDisarmEdit != null)
            {
                _onDisarmEdit.Text = utt.OnDisarmScript?.ToString() ?? "";
            }
            if (_onEnterSelect != null)
            {
                _onEnterSelect.Text = utt.OnEnterScript?.ToString() ?? "";
            }
            if (_onExitSelect != null)
            {
                _onExitSelect.Text = utt.OnExitScript?.ToString() ?? "";
            }
            if (_onHeartbeatSelect != null)
            {
                _onHeartbeatSelect.Text = utt.OnHeartbeatScript?.ToString() ?? "";
            }
            if (_onTrapTriggeredEdit != null)
            {
                _onTrapTriggeredEdit.Text = utt.OnTrapTriggeredScript?.ToString() ?? "";
            }
            if (_onUserDefinedSelect != null)
            {
                _onUserDefinedSelect.Text = utt.OnUserDefinedScript?.ToString() ?? "";
            }

            // Comments
            if (_commentsEdit != null)
            {
                _commentsEdit.Text = utt.Comment ?? "";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:187-240
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Update the existing UTT object from UI elements
            // Matching Python: utt.name = self.ui.nameEdit.locstring()
            if (_nameEdit != null)
            {
                _utt.Name = _nameEdit.GetLocString();
            }
            // Matching Python: utt.tag = self.ui.tagEdit.text()
            if (_tagEdit != null)
            {
                _utt.Tag = _tagEdit.Text ?? "";
            }
            // Matching Python: utt.resref = ResRef(self.ui.resrefEdit.text())
            if (_resrefEdit != null)
            {
                _utt.ResRef = !string.IsNullOrEmpty(_resrefEdit.Text) ? new ResRef(_resrefEdit.Text) : new ResRef("");
            }
            // Matching Python: utt.cursor_id = self.ui.cursorSelect.currentIndex()
            if (_cursorSelect != null)
            {
                _utt.Cursor = _cursorSelect.SelectedIndex;
            }
            // Matching Python: utt.type_id = self.ui.typeSelect.currentIndex()
            if (_typeSelect != null)
            {
                _utt.TypeId = _typeSelect.SelectedIndex;
            }

            // Advanced
            // Matching Python: utt.auto_remove_key = self.ui.autoRemoveKeyCheckbox.isChecked()
            if (_autoRemoveKeyCheckbox != null)
            {
                _utt.AutoRemoveKey = _autoRemoveKeyCheckbox.IsChecked == true;
            }
            // Matching Python: utt.key_name = self.ui.keyEdit.text()
            if (_keyEdit != null)
            {
                _utt.KeyName = _keyEdit.Text ?? "";
            }
            // Matching Python: utt.faction_id = self.ui.factionSelect.currentIndex()
            if (_factionSelect != null)
            {
                _utt.FactionId = _factionSelect.SelectedIndex;
            }
            // Matching Python: utt.highlight_height = self.ui.highlightHeightSpin.value()
            if (_highlightHeightSpin != null)
            {
                _utt.HighlightHeight = (float)_highlightHeightSpin.Value;
            }

            // Trap
            // Matching Python: utt.is_trap = self.ui.isTrapCheckbox.isChecked()
            if (_isTrapCheckbox != null)
            {
                _utt.IsTrap = _isTrapCheckbox.IsChecked == true;
            }
            // Matching Python: utt.trap_once = self.ui.activateOnceCheckbox.isChecked()
            if (_activateOnceCheckbox != null)
            {
                _utt.TrapOnce = _activateOnceCheckbox.IsChecked == true;
            }
            // Matching Python: utt.trap_detectable = self.ui.detectableCheckbox.isChecked()
            if (_detectableCheckbox != null)
            {
                _utt.TrapDetectable = _detectableCheckbox.IsChecked == true;
            }
            // Matching Python: utt.trap_detect_dc = self.ui.detectDcSpin.value()
            // Try both public property and private field to ensure we get the value
            if (_detectDcSpin != null && _detectDcSpin.Value.HasValue)
            {
                _utt.TrapDetectDc = (int)Math.Round(_detectDcSpin.Value.Value);
            }
            else if (DetectDcSpin != null && DetectDcSpin.Value.HasValue)
            {
                _utt.TrapDetectDc = (int)Math.Round(DetectDcSpin.Value.Value);
            }
            // Matching Python: utt.trap_disarmable = self.ui.disarmableCheckbox.isChecked()
            if (_disarmableCheckbox != null)
            {
                _utt.TrapDisarmable = _disarmableCheckbox.IsChecked == true;
            }
            // Matching Python: utt.trap_disarm_dc = self.ui.disarmDcSpin.value()
            // Try both public property and private field to ensure we get the value
            if (_disarmDcSpin != null && _disarmDcSpin.Value.HasValue)
            {
                _utt.TrapDisarmDc = (int)Math.Round(_disarmDcSpin.Value.Value);
            }
            else if (DisarmDcSpin != null && DisarmDcSpin.Value.HasValue)
            {
                _utt.TrapDisarmDc = (int)Math.Round(DisarmDcSpin.Value.Value);
            }
            // Matching Python: utt.trap_type = self.ui.trapSelect.currentIndex()
            if (_trapSelect != null)
            {
                _utt.TrapType = _trapSelect.SelectedIndex;
            }

            // Scripts
            // Matching Python: utt.on_click = ResRef(self.ui.onClickEdit.currentText())
            if (_onClickEdit != null)
            {
                _utt.OnClickScript = !string.IsNullOrEmpty(_onClickEdit.Text) ? new ResRef(_onClickEdit.Text) : new ResRef("");
            }
            // Matching Python: utt.on_disarm = ResRef(self.ui.onDisarmEdit.currentText())
            if (_onDisarmEdit != null)
            {
                _utt.OnDisarmScript = !string.IsNullOrEmpty(_onDisarmEdit.Text) ? new ResRef(_onDisarmEdit.Text) : new ResRef("");
            }
            // Matching Python: utt.on_enter = ResRef(self.ui.onEnterSelect.currentText())
            if (_onEnterSelect != null)
            {
                _utt.OnEnterScript = !string.IsNullOrEmpty(_onEnterSelect.Text) ? new ResRef(_onEnterSelect.Text) : new ResRef("");
            }
            // Matching Python: utt.on_exit = ResRef(self.ui.onExitSelect.currentText())
            if (_onExitSelect != null)
            {
                _utt.OnExitScript = !string.IsNullOrEmpty(_onExitSelect.Text) ? new ResRef(_onExitSelect.Text) : new ResRef("");
            }
            // Matching Python: utt.on_heartbeat = ResRef(self.ui.onHeartbeatSelect.currentText())
            if (_onHeartbeatSelect != null)
            {
                _utt.OnHeartbeatScript = !string.IsNullOrEmpty(_onHeartbeatSelect.Text) ? new ResRef(_onHeartbeatSelect.Text) : new ResRef("");
            }
            // Matching Python: utt.on_trap_triggered = ResRef(self.ui.onTrapTriggeredEdit.currentText())
            if (_onTrapTriggeredEdit != null)
            {
                _utt.OnTrapTriggeredScript = !string.IsNullOrEmpty(_onTrapTriggeredEdit.Text) ? new ResRef(_onTrapTriggeredEdit.Text) : new ResRef("");
            }
            // Matching Python: utt.on_user_defined = ResRef(self.ui.onUserDefinedSelect.currentText())
            if (_onUserDefinedSelect != null)
            {
                _utt.OnUserDefinedScript = !string.IsNullOrEmpty(_onUserDefinedSelect.Text) ? new ResRef(_onUserDefinedSelect.Text) : new ResRef("");
            }

            // Comments
            // Matching Python: utt.comment = self.ui.commentsEdit.toPlainText()
            if (_commentsEdit != null)
            {
                _utt.Comment = _commentsEdit.Text ?? "";
            }

            BioWareGame game = _installation?.Game ?? BioWareGame.K2;
            var gff = UTTHelpers.DismantleUtt(_utt, game);

            // Preserve unmodified fields from original GFF that aren't yet supported by UTT object model
            if (_originalGff != null)
            {
                var originalRoot = _originalGff.Root;
                var newRoot = gff.Root;

                // List of fields that UTTHelpers.DismantleUtt explicitly sets
                var fieldsSetByDismantle = new HashSet<string>
                {
                    "Tag", "ResRef", "Comment", "Type", "LinkedTo", "LinkedToFlags",
                    "KeyName", "AutoRemoveKey", "TrapDetectable", "TrapDetectDC", "DisarmDC", "TrapFlag",
                    "TrapType", "IsTrap", "KeyRequired", "Lockable", "Locked", "Hardness",
                    "KeyName2", "ScriptHeartbeat", "ScriptOnEnter", "ScriptOnExit", "ScriptUserDefine",
                    "TransitionDestin"
                };

                // Fields that may have default value mismatches - always restore from original
                var fieldsToRestore = new HashSet<string> { "PaletteID", "TrapDisarmable", "Cursor", "Faction", "TrapOneShot" };
                foreach (var fieldName in fieldsToRestore)
                {
                    if (originalRoot.Exists(fieldName))
                    {
                        var originalFieldType = originalRoot.GetFieldType(fieldName);
                        if (originalFieldType.HasValue)
                        {
                            if (newRoot.Exists(fieldName))
                            {
                                newRoot.Remove(fieldName);
                            }
                            CopyGffField(originalRoot, newRoot, fieldName, originalFieldType.Value);
                        }
                    }
                }

                // Copy all fields from original that aren't explicitly set by DismantleUtt
                foreach (var (label, fieldType, value) in originalRoot)
                {
                    if (!fieldsSetByDismantle.Contains(label) && !fieldsToRestore.Contains(label) && !newRoot.Exists(label))
                    {
                        CopyGffField(originalRoot, newRoot, label, fieldType);
                    }
                }
            }

            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTT);
            return Tuple.Create(data, new byte[0]);
        }

        private void CopyGffField(GFFStruct sourceStruct, GFFStruct targetStruct, string label, GFFFieldType fieldType)
        {
            switch (fieldType)
            {
                case GFFFieldType.UInt8: targetStruct.SetUInt8(label, sourceStruct.GetUInt8(label)); break;
                case GFFFieldType.Int8: targetStruct.SetInt8(label, sourceStruct.GetInt8(label)); break;
                case GFFFieldType.UInt16: targetStruct.SetUInt16(label, sourceStruct.GetUInt16(label)); break;
                case GFFFieldType.Int16: targetStruct.SetInt16(label, sourceStruct.GetInt16(label)); break;
                case GFFFieldType.UInt32: targetStruct.SetUInt32(label, sourceStruct.GetUInt32(label)); break;
                case GFFFieldType.Int32: targetStruct.SetInt32(label, sourceStruct.GetInt32(label)); break;
                case GFFFieldType.UInt64: targetStruct.SetUInt64(label, sourceStruct.GetUInt64(label)); break;
                case GFFFieldType.Int64: targetStruct.SetInt64(label, sourceStruct.GetInt64(label)); break;
                case GFFFieldType.Single: targetStruct.SetSingle(label, sourceStruct.GetSingle(label)); break;
                case GFFFieldType.Double: targetStruct.SetDouble(label, sourceStruct.GetDouble(label)); break;
                case GFFFieldType.String: targetStruct.SetString(label, sourceStruct.GetString(label)); break;
                case GFFFieldType.ResRef: targetStruct.SetResRef(label, sourceStruct.GetResRef(label)); break;
                case GFFFieldType.LocalizedString: targetStruct.SetLocString(label, sourceStruct.GetLocString(label)); break;
                case GFFFieldType.Binary: targetStruct.SetBinary(label, sourceStruct.GetBinary(label)); break;
                case GFFFieldType.Vector3: targetStruct.SetVector3(label, sourceStruct.GetVector3(label)); break;
                case GFFFieldType.Vector4: targetStruct.SetVector4(label, sourceStruct.GetVector4(label)); break;
                case GFFFieldType.Struct: targetStruct.SetStruct(label, sourceStruct.GetStruct(label)); break;
                case GFFFieldType.List: targetStruct.SetList(label, sourceStruct.GetList(label)); break;
                default:
                    break;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:242-244
        // Original: def new(self):
        public override void New()
        {
            base.New();
            LoadUTT(new UTT());
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:252-255
        // Original: def generate_tag(self):
        private void GenerateTag()
        {
            if (_resrefEdit != null && string.IsNullOrEmpty(_resrefEdit.Text))
            {
                GenerateResref();
            }
            if (_tagEdit != null && _resrefEdit != null)
            {
                _tagEdit.Text = _resrefEdit.Text;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utt.py:257-261
        // Original: def generate_resref(self):
        private void GenerateResref()
        {
            if (_resrefEdit != null)
            {
                if (!string.IsNullOrEmpty(_resname))
                {
                    _resrefEdit.Text = _resname;
                }
                else
                {
                    _resrefEdit.Text = "m00xx_trg_000";
                }
            }
        }

        public override void SaveAs()
        {
            Save();
        }

        // Public properties for testing
        public LocalizedStringEdit NameEdit => _nameEdit;
        public TextBox TagEdit => _tagEdit;
        public TextBox ResrefEdit => _resrefEdit;
        public CheckBox AutoRemoveKeyCheckbox => _autoRemoveKeyCheckbox;
        public CheckBox IsTrapCheckbox => _isTrapCheckbox;
        public CheckBox ActivateOnceCheckbox => _activateOnceCheckbox;
        public NumericUpDown DetectDcSpin => _detectDcSpin;
        public NumericUpDown DisarmDcSpin => _disarmDcSpin;
        public UTT Utt => _utt;

        // Helper method to populate script combo boxes (matching PyKotor's populate_combo_box and setup_file_context_menu)
        private void PopulateScriptComboBox(ComboBox comboBox, List<string> scriptResnames, HTInstallation installation)
        {
            if (comboBox == null)
            {
                return;
            }

            try
            {
                // Clear existing items
                comboBox.Items.Clear();

                // Add script resnames to combo box
                foreach (string resname in scriptResnames)
                {
                    comboBox.Items.Add(resname);
                }

                // Setup file context menu (matching PyKotor: installation.setup_file_context_menu)
                // This allows right-clicking to browse for scripts
                // Note: SetupFileContextMenu may not be implemented yet, so we skip it if unavailable
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to populate script combo box: {ex.Message}");
            }
        }
    }
}

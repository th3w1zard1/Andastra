using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:28
    // Original: class UTSEditor(Editor):
    public class UTSEditor : Editor
    {
        private UTS _uts;
        private HTInstallation _installation;

        // UI Controls - Basic
        private TextBox _nameEdit;
        private Button _nameEditBtn;
        private TextBox _tagEdit;
        private Button _tagGenerateBtn;
        private TextBox _resrefEdit;
        private Button _resrefGenerateBtn;
        private Slider _volumeSlider;
        private CheckBox _activeCheckbox;

        // UI Controls - Advanced
        private RadioButton _playRandomRadio;
        private RadioButton _playSpecificRadio;
        private RadioButton _playEverywhereRadio;
        private RadioButton _orderSequentialRadio;
        private RadioButton _orderRandomRadio;
        private NumericUpDown _intervalSpin;
        private NumericUpDown _intervalVariationSpin;
        private Slider _volumeVariationSlider;
        private Slider _pitchVariationSlider;

        // UI Controls - Sounds
        private ListBox _soundList;
        private Button _addSoundBtn;
        private Button _removeSoundBtn;
        private Button _playSoundBtn;
        private Button _stopSoundBtn;
        private Button _moveUpBtn;
        private Button _moveDownBtn;

        // UI Controls - Positioning
        private RadioButton _styleOnceRadio;
        private RadioButton _styleSeamlessRadio;
        private RadioButton _styleRepeatRadio;
        private NumericUpDown _cutoffSpin;
        private NumericUpDown _maxVolumeDistanceSpin;
        private NumericUpDown _heightSpin;
        private NumericUpDown _northRandomSpin;
        private NumericUpDown _eastRandomSpin;

        // UI Controls - Comments
        private TextBox _commentsEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:29-73
        // Original: def __init__(self, parent, installation):
        public UTSEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Sound Editor", "sound",
                new[] { ResourceType.UTS },
                new[] { ResourceType.UTS },
                installation)
        {
            _installation = installation;
            _uts = new UTS();

            InitializeComponent();
            SetupUI();
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
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:75-106
        // Original: def _setup_signals(self):
        private void SetupProgrammaticUI()
        {
            var scrollViewer = new ScrollViewer();
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Basic Group
            var basicGroup = new Expander { Header = "Basic", IsExpanded = true };
            var basicPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Name
            var nameLabel = new TextBlock { Text = "Name:" };
            _nameEdit = new TextBox { IsReadOnly = true };
            _nameEditBtn = new Button { Content = "Edit Name" };
            _nameEditBtn.Click += (s, e) => EditName();
            basicPanel.Children.Add(nameLabel);
            basicPanel.Children.Add(_nameEdit);
            basicPanel.Children.Add(_nameEditBtn);

            // Tag
            var tagLabel = new TextBlock { Text = "Tag:" };
            _tagEdit = new TextBox();
            _tagGenerateBtn = new Button { Content = "Generate" };
            _tagGenerateBtn.Click += (s, e) => GenerateTag();
            basicPanel.Children.Add(tagLabel);
            basicPanel.Children.Add(_tagEdit);
            basicPanel.Children.Add(_tagGenerateBtn);

            // ResRef
            var resrefLabel = new TextBlock { Text = "ResRef:" };
            _resrefEdit = new TextBox();
            _resrefGenerateBtn = new Button { Content = "Generate" };
            _resrefGenerateBtn.Click += (s, e) => GenerateResref();
            basicPanel.Children.Add(resrefLabel);
            basicPanel.Children.Add(_resrefEdit);
            basicPanel.Children.Add(_resrefGenerateBtn);

            // Volume
            var volumeLabel = new TextBlock { Text = "Volume:" };
            _volumeSlider = new Slider { Minimum = 0, Maximum = 255, Value = 127 };
            basicPanel.Children.Add(volumeLabel);
            basicPanel.Children.Add(_volumeSlider);

            // Active
            _activeCheckbox = new CheckBox { Content = "Active" };
            basicPanel.Children.Add(_activeCheckbox);

            basicGroup.Content = basicPanel;
            mainPanel.Children.Add(basicGroup);

            // Advanced Group
            var advancedGroup = new Expander { Header = "Advanced", IsExpanded = false };
            var advancedPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Play Mode
            var playModeLabel = new TextBlock { Text = "Play Mode:" };
            _playRandomRadio = new RadioButton { Content = "Random Position", GroupName = "PlayMode" };
            _playSpecificRadio = new RadioButton { Content = "Specific Position", GroupName = "PlayMode" };
            _playEverywhereRadio = new RadioButton { Content = "Everywhere", GroupName = "PlayMode", IsChecked = true };
            _playRandomRadio.Checked += (s, e) => ChangePlay();
            _playSpecificRadio.Checked += (s, e) => ChangePlay();
            _playEverywhereRadio.Checked += (s, e) => ChangePlay();

            // Order
            var orderLabel = new TextBlock { Text = "Order:" };
            _orderSequentialRadio = new RadioButton { Content = "Sequential", GroupName = "Order", IsChecked = true };
            _orderRandomRadio = new RadioButton { Content = "Random", GroupName = "Order" };

            // Interval
            var intervalLabel = new TextBlock { Text = "Interval:" };
            _intervalSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue };
            var intervalVariationLabel = new TextBlock { Text = "Interval Variation:" };
            _intervalVariationSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue };

            // Variation
            var volumeVariationLabel = new TextBlock { Text = "Volume Variation:" };
            _volumeVariationSlider = new Slider { Minimum = 0, Maximum = 255 };
            var pitchVariationLabel = new TextBlock { Text = "Pitch Variation:" };
            _pitchVariationSlider = new Slider { Minimum = 0, Maximum = 100 };

            advancedPanel.Children.Add(playModeLabel);
            advancedPanel.Children.Add(_playRandomRadio);
            advancedPanel.Children.Add(_playSpecificRadio);
            advancedPanel.Children.Add(_playEverywhereRadio);
            advancedPanel.Children.Add(orderLabel);
            advancedPanel.Children.Add(_orderSequentialRadio);
            advancedPanel.Children.Add(_orderRandomRadio);
            advancedPanel.Children.Add(intervalLabel);
            advancedPanel.Children.Add(_intervalSpin);
            advancedPanel.Children.Add(intervalVariationLabel);
            advancedPanel.Children.Add(_intervalVariationSpin);
            advancedPanel.Children.Add(volumeVariationLabel);
            advancedPanel.Children.Add(_volumeVariationSlider);
            advancedPanel.Children.Add(pitchVariationLabel);
            advancedPanel.Children.Add(_pitchVariationSlider);

            advancedGroup.Content = advancedPanel;
            mainPanel.Children.Add(advancedGroup);

            // Sounds Group
            var soundsGroup = new Expander { Header = "Sounds", IsExpanded = false };
            var soundsPanel = new StackPanel { Orientation = Orientation.Vertical };
            var soundsLabel = new TextBlock { Text = "Sound List:" };
            _soundList = new ListBox();
            var soundButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _addSoundBtn = new Button { Content = "Add" };
            _addSoundBtn.Click += (s, e) => AddSound();
            _removeSoundBtn = new Button { Content = "Remove" };
            _removeSoundBtn.Click += (s, e) => RemoveSound();
            _playSoundBtn = new Button { Content = "Play" };
            _playSoundBtn.Click += (s, e) => PlaySound();
            _stopSoundBtn = new Button { Content = "Stop" };
            _stopSoundBtn.Click += (s, e) => StopSound();
            _moveUpBtn = new Button { Content = "Up" };
            _moveUpBtn.Click += (s, e) => MoveSoundUp();
            _moveDownBtn = new Button { Content = "Down" };
            _moveDownBtn.Click += (s, e) => MoveSoundDown();
            soundButtonsPanel.Children.Add(_addSoundBtn);
            soundButtonsPanel.Children.Add(_removeSoundBtn);
            soundButtonsPanel.Children.Add(_playSoundBtn);
            soundButtonsPanel.Children.Add(_stopSoundBtn);
            soundButtonsPanel.Children.Add(_moveUpBtn);
            soundButtonsPanel.Children.Add(_moveDownBtn);
            soundsPanel.Children.Add(soundsLabel);
            soundsPanel.Children.Add(_soundList);
            soundsPanel.Children.Add(soundButtonsPanel);
            soundsGroup.Content = soundsPanel;
            mainPanel.Children.Add(soundsGroup);

            // Positioning Group
            var positioningGroup = new Expander { Header = "Positioning", IsExpanded = false };
            var positioningPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Style
            var styleLabel = new TextBlock { Text = "Style:" };
            _styleOnceRadio = new RadioButton { Content = "Once", GroupName = "Style", IsChecked = true };
            _styleSeamlessRadio = new RadioButton { Content = "Seamless", GroupName = "Style" };
            _styleRepeatRadio = new RadioButton { Content = "Repeat", GroupName = "Style" };
            _styleOnceRadio.Checked += (s, e) => ChangeStyle();
            _styleSeamlessRadio.Checked += (s, e) => ChangeStyle();
            _styleRepeatRadio.Checked += (s, e) => ChangeStyle();

            // Distances
            var cutoffLabel = new TextBlock { Text = "Cutoff Distance:" };
            _cutoffSpin = new NumericUpDown { Minimum = 0, Maximum = decimal.MaxValue };
            var maxVolumeDistanceLabel = new TextBlock { Text = "Max Volume Distance:" };
            _maxVolumeDistanceSpin = new NumericUpDown { Minimum = 0, Maximum = decimal.MaxValue };
            var heightLabel = new TextBlock { Text = "Height:" };
            _heightSpin = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue };
            var northRandomLabel = new TextBlock { Text = "North Random:" };
            _northRandomSpin = new NumericUpDown { Minimum = 0, Maximum = decimal.MaxValue };
            var eastRandomLabel = new TextBlock { Text = "East Random:" };
            _eastRandomSpin = new NumericUpDown { Minimum = 0, Maximum = decimal.MaxValue };

            positioningPanel.Children.Add(styleLabel);
            positioningPanel.Children.Add(_styleOnceRadio);
            positioningPanel.Children.Add(_styleSeamlessRadio);
            positioningPanel.Children.Add(_styleRepeatRadio);
            positioningPanel.Children.Add(cutoffLabel);
            positioningPanel.Children.Add(_cutoffSpin);
            positioningPanel.Children.Add(maxVolumeDistanceLabel);
            positioningPanel.Children.Add(_maxVolumeDistanceSpin);
            positioningPanel.Children.Add(heightLabel);
            positioningPanel.Children.Add(_heightSpin);
            positioningPanel.Children.Add(northRandomLabel);
            positioningPanel.Children.Add(_northRandomSpin);
            positioningPanel.Children.Add(eastRandomLabel);
            positioningPanel.Children.Add(_eastRandomSpin);

            positioningGroup.Content = positioningPanel;
            mainPanel.Children.Add(positioningGroup);

            // Comments Group
            var commentsGroup = new Expander { Header = "Comments", IsExpanded = false };
            var commentsPanel = new StackPanel { Orientation = Orientation.Vertical };
            var commentsLabel = new TextBlock { Text = "Comment:" };
            _commentsEdit = new TextBox { AcceptsReturn = true, AcceptsTab = true };
            commentsPanel.Children.Add(commentsLabel);
            commentsPanel.Children.Add(_commentsEdit);
            commentsGroup.Content = commentsPanel;
            mainPanel.Children.Add(commentsGroup);

            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
        }

        private void SetupUI()
        {
            // Try to find controls from XAML if available
            // For now, programmatic UI is set up in SetupProgrammaticUI
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:111-121
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The UTS file data is empty or invalid.");
            }

            var gff = GFF.FromBytes(data);
            _uts = UTSHelpers.ConstructUts(gff);
            LoadUTS(_uts);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:123-195
        // Original: def _loadUTS(self, uts):
        private void LoadUTS(UTS uts)
        {
            _uts = uts;

            // Basic
            if (_nameEdit != null)
            {
                _nameEdit.Text = _installation != null ? _installation.String(uts.Name) : uts.Name.StringRef.ToString();
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = uts.Tag;
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = uts.ResRef.ToString();
            }
            if (_volumeSlider != null)
            {
                _volumeSlider.Value = uts.Volume;
            }
            if (_activeCheckbox != null)
            {
                _activeCheckbox.IsChecked = uts.Active;
            }

            // Advanced
            if (uts.RandomRangeX != 0 && uts.RandomRangeY != 0)
            {
                if (_playRandomRadio != null) _playRandomRadio.IsChecked = true;
            }
            else if (uts.Positional)
            {
                if (_playSpecificRadio != null) _playSpecificRadio.IsChecked = true;
            }
            else
            {
                if (_playEverywhereRadio != null) _playEverywhereRadio.IsChecked = true;
            }

            if (_orderSequentialRadio != null) _orderSequentialRadio.IsChecked = !uts.Random;
            if (_orderRandomRadio != null) _orderRandomRadio.IsChecked = uts.Random;
            if (_intervalSpin != null) _intervalSpin.Value = uts.Interval;
            if (_intervalVariationSpin != null) _intervalVariationSpin.Value = uts.IntervalVariance;
            if (_volumeVariationSlider != null) _volumeVariationSlider.Value = uts.VolumeVariance;
            if (_pitchVariationSlider != null) _pitchVariationSlider.Value = (int)(uts.PitchVariance * 100);

            // Sounds
            if (_soundList != null)
            {
                _soundList.Items.Clear();
                if (uts.Sounds != null)
                {
                    foreach (var sound in uts.Sounds)
                    {
                        _soundList.Items.Add(sound.ToString());
                    }
                }
            }

            // Positioning
            if (uts.Continuous && uts.Looping)
            {
                if (_styleSeamlessRadio != null) _styleSeamlessRadio.IsChecked = true;
            }
            else if (uts.Looping)
            {
                if (_styleRepeatRadio != null) _styleRepeatRadio.IsChecked = true;
            }
            else
            {
                if (_styleOnceRadio != null) _styleOnceRadio.IsChecked = true;
            }

            if (_cutoffSpin != null) _cutoffSpin.Value = (decimal?)uts.MinDistance;
            if (_maxVolumeDistanceSpin != null) _maxVolumeDistanceSpin.Value = (decimal?)uts.MaxDistance;
            if (_heightSpin != null) _heightSpin.Value = (decimal?)uts.Elevation;
            if (_northRandomSpin != null) _northRandomSpin.Value = (decimal?)uts.RandomRangeY;
            if (_eastRandomSpin != null) _eastRandomSpin.Value = (decimal?)uts.RandomRangeX;

            // Comments
            if (_commentsEdit != null) _commentsEdit.Text = uts.Comment;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:196-252
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Basic
            _uts.Name = _uts.Name ?? LocalizedString.FromInvalid();
            _uts.Tag = _tagEdit?.Text ?? "";
            _uts.ResRef = new ResRef(_resrefEdit?.Text ?? "");
            _uts.Volume = (int)(_volumeSlider?.Value ?? 127);
            _uts.Active = _activeCheckbox?.IsChecked ?? false;

            // Advanced
            _uts.Positional = _playSpecificRadio?.IsChecked ?? false;
            _uts.Random = _orderRandomRadio?.IsChecked ?? false;
            _uts.Interval = (int)(_intervalSpin?.Value ?? 0);
            _uts.IntervalVariance = (int)(_intervalVariationSpin?.Value ?? 0);
            _uts.VolumeVariance = (int)(_volumeVariationSlider?.Value ?? 0);
            _uts.PitchVariance = (float)((_pitchVariationSlider?.Value ?? 0) / 100.0);

            // Sounds
            _uts.Sounds.Clear();
            if (_soundList?.Items != null)
            {
                foreach (string item in _soundList.Items)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        _uts.Sounds.Add(new ResRef(item));
                    }
                }
            }

            // Positioning
            _uts.Continuous = _styleSeamlessRadio?.IsChecked ?? false;
            _uts.Looping = (_styleSeamlessRadio?.IsChecked ?? false) || (_styleRepeatRadio?.IsChecked ?? false);
            _uts.MaxDistance = (float)(_maxVolumeDistanceSpin?.Value ?? 0);
            _uts.MinDistance = (float)(_cutoffSpin?.Value ?? 0);
            _uts.Elevation = (float)(_heightSpin?.Value ?? 0);
            _uts.RandomRangeY = (float)(_northRandomSpin?.Value ?? 0);
            _uts.RandomRangeX = (float)(_eastRandomSpin?.Value ?? 0);

            // Comments
            _uts.Comment = _commentsEdit?.Text ?? "";

            // Build GFF
            Game game = _installation?.Game ?? Game.K2;
            var gff = UTSHelpers.DismantleUts(_uts, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTS);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:254-256
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _uts = new UTS();
            LoadUTS(_uts);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:258-262
        // Original: def change_name(self):
        private void EditName()
        {
            if (_installation == null) return;
            var dialog = new LocalizedStringDialog(this, _installation, _uts.Name);
            if (dialog.ShowDialog())
            {
                _uts.Name = dialog.LocString;
                if (_nameEdit != null)
                {
                    _nameEdit.Text = _installation.String(_uts.Name);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:264-267
        // Original: def generate_tag(self):
        private void GenerateTag()
        {
            if (string.IsNullOrEmpty(_resrefEdit?.Text))
            {
                GenerateResref();
            }
            if (_tagEdit != null && _resrefEdit != null)
            {
                _tagEdit.Text = _resrefEdit.Text;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:269-273
        // Original: def generate_resref(self):
        private void GenerateResref()
        {
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = !string.IsNullOrEmpty(base._resname) ? base._resname : "m00xx_trg_000";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:275-287
        // Original: def change_style(self):
        private void ChangeStyle()
        {
            // Enable/disable interval and variation groups based on style
            bool enableGroups = !(_styleSeamlessRadio?.IsChecked ?? false);
            if (_intervalSpin != null) _intervalSpin.IsEnabled = enableGroups;
            if (_intervalVariationSpin != null) _intervalVariationSpin.IsEnabled = enableGroups;
            if (_volumeVariationSlider != null) _volumeVariationSlider.IsEnabled = enableGroups;
            if (_pitchVariationSlider != null) _pitchVariationSlider.IsEnabled = enableGroups;
            if (_orderSequentialRadio != null) _orderSequentialRadio.IsEnabled = enableGroups;
            if (_orderRandomRadio != null) _orderRandomRadio.IsEnabled = enableGroups;

            if (_styleOnceRadio?.IsChecked ?? false)
            {
                if (_intervalSpin != null) _intervalSpin.IsEnabled = false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:289-303
        // Original: def change_play(self):
        private void ChangePlay()
        {
            // Enable/disable range and distance groups based on play mode
            bool enableGroups = !(_playEverywhereRadio?.IsChecked ?? false);
            if (_cutoffSpin != null) _cutoffSpin.IsEnabled = enableGroups;
            if (_maxVolumeDistanceSpin != null) _maxVolumeDistanceSpin.IsEnabled = enableGroups;
            if (_heightSpin != null) _heightSpin.IsEnabled = enableGroups;
            if (_northRandomSpin != null) _northRandomSpin.IsEnabled = enableGroups;
            if (_eastRandomSpin != null) _eastRandomSpin.IsEnabled = enableGroups;

            if (_playSpecificRadio?.IsChecked ?? false)
            {
                if (_northRandomSpin != null) _northRandomSpin.Value = 0;
                if (_eastRandomSpin != null) _eastRandomSpin.Value = 0;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:305-321
        // Original: def play_sound(self):
        private void PlaySound()
        {
            // Placeholder for sound playback
            // Will be implemented when audio playback is available
            System.Console.WriteLine("Sound playback not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:323-326
        // Original: def stop_sound(self):
        private void StopSound()
        {
            // Placeholder for sound stopping
            System.Console.WriteLine("Sound stopping not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:328-331
        // Original: def add_sound(self):
        private void AddSound()
        {
            if (_soundList != null)
            {
                _soundList.Items.Add("new sound");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:333-336
        // Original: def remove_sound(self):
        private void RemoveSound()
        {
            if (_soundList?.SelectedItem != null)
            {
                _soundList.Items.Remove(_soundList.SelectedItem);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:338-348
        // Original: def move_sound_up(self):
        private void MoveSoundUp()
        {
            if (_soundList?.SelectedIndex > 0 && _soundList?.SelectedIndex < _soundList.Items.Count)
            {
                int index = _soundList.SelectedIndex;
                var item = _soundList.Items[index];
                _soundList.Items.RemoveAt(index);
                _soundList.Items.Insert(index - 1, item);
                _soundList.SelectedIndex = index - 1;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uts.py:350-360
        // Original: def move_sound_down(self):
        private void MoveSoundDown()
        {
            if (_soundList?.SelectedIndex >= 0 && _soundList.SelectedIndex < _soundList.Items.Count - 1)
            {
                int index = _soundList.SelectedIndex;
                var item = _soundList.Items[index];
                _soundList.Items.RemoveAt(index);
                _soundList.Items.Insert(index + 1, item);
                _soundList.SelectedIndex = index + 1;
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}

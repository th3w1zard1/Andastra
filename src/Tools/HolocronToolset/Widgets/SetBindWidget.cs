using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/set_bind.py:17
    // Original: class SetBindWidget(QWidget):
    public partial class SetBindWidget : UserControl
    {
        private HashSet<Key> _keybind = new HashSet<Key>();
        private bool _recordBind = false;
        private ComboBox _mouseCombo;
        private TextBox _setKeysEdit;
        private Button _setButton;
        private Button _clearButton;

        // Public parameterless constructor for XAML
        public SetBindWidget()
        {
            InitializeComponent();
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
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            _mouseCombo = new ComboBox { MinWidth = 80 };
            _mouseCombo.Items.Add("Left");
            _mouseCombo.Items.Add("Middle");
            _mouseCombo.Items.Add("Right");
            _mouseCombo.Items.Add("Any");
            _mouseCombo.Items.Add("None");
            _setKeysEdit = new TextBox { IsReadOnly = true, Watermark = "none" };
            _setButton = new Button { Content = "Set", MaxWidth = 40 };
            _setButton.Click += (s, e) => StartRecording();
            _clearButton = new Button { Content = "Clear", MaxWidth = 40 };
            _clearButton.Click += (s, e) => ClearKeybind();
            panel.Children.Add(_mouseCombo);
            panel.Children.Add(_setKeysEdit);
            panel.Children.Add(_setButton);
            panel.Children.Add(_clearButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _mouseCombo = this.FindControl<ComboBox>("mouseCombo");
            _setKeysEdit = this.FindControl<TextBox>("setKeysEdit");
            _setButton = this.FindControl<Button>("setButton");
            _clearButton = this.FindControl<Button>("clearButton");

            if (_setButton != null)
            {
                _setButton.Click += (s, e) => StartRecording();
            }
            if (_clearButton != null)
            {
                _clearButton.Click += (s, e) => ClearKeybind();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/set_bind.py:39-44
        // Original: def start_recording(self):
        private void StartRecording()
        {
            _recordBind = true;
            _keybind.Clear();
            UpdateKeybindText();
            if (_setKeysEdit != null)
            {
                _setKeysEdit.Watermark = "Enter a key...";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/set_bind.py:46-50
        // Original: def clear_keybind(self):
        private void ClearKeybind()
        {
            _keybind.Clear();
            if (_setKeysEdit != null)
            {
                _setKeysEdit.Watermark = "none";
            }
            UpdateKeybindText();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/set_bind.py:52-57
        // Original: def keyPressEvent(self, a0: QKeyEvent):
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_recordBind)
            {
                _keybind.Add(e.Key);
                UpdateKeybindText();
            }
            base.OnKeyDown(e);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/set_bind.py:58-61
        // Original: def keyReleaseEvent(self, e: QKeyEvent):
        protected override void OnKeyUp(KeyEventArgs e)
        {
            _recordBind = false;
            System.Console.WriteLine($"Set keybind to {string.Join(",", _keybind)}");
            base.OnKeyUp(e);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/set_bind.py:93-98
        // Original: def update_keybind_text(self):
        private void UpdateKeybindText()
        {
            if (_setKeysEdit == null)
            {
                return;
            }

            // TODO: Implement key string localization when available
            var sortedKeys = _keybind.OrderBy(k => k.ToString()).ToList();
            string text = string.Join("+", sortedKeys.Select(k => k.ToString().ToUpperInvariant()));
            _setKeysEdit.Text = text;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/set_bind.py:63-85
        // Original: def set_mouse_and_key_binds(self, bind: Bind):
        public void SetMouseAndKeyBinds(Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> bind)
        {
            if (bind != null)
            {
                if (bind.Item1 != null)
                {
                    _keybind = new HashSet<Key>(bind.Item1);
                }
                else
                {
                    _keybind.Clear();
                }

                if (bind.Item2 != null)
                {
                    _mouseButtons = new HashSet<PointerUpdateKind>(bind.Item2);
                    UpdateMouseComboFromBinds();
                }
                else
                {
                    _mouseButtons.Clear();
                    if (_mouseCombo != null)
                    {
                        _mouseCombo.SelectedIndex = 4; // "None"
                    }
                }

                UpdateKeybindText();
            }
        }

        // Helper method to update mouse combo box selection based on stored mouse buttons
        private void UpdateMouseComboFromBinds()
        {
            if (_mouseCombo == null)
            {
                return;
            }

            if (_mouseButtons.Count == 0)
            {
                _mouseCombo.SelectedIndex = 4; // "None"
                return;
            }

            // Check for specific button combinations
            bool hasLeft = _mouseButtons.Contains(PointerUpdateKind.LeftButtonPressed);
            bool hasMiddle = _mouseButtons.Contains(PointerUpdateKind.MiddleButtonPressed);
            bool hasRight = _mouseButtons.Contains(PointerUpdateKind.RightButtonPressed);

            if (hasLeft && !hasMiddle && !hasRight)
            {
                _mouseCombo.SelectedIndex = 0; // "Left"
            }
            else if (hasMiddle && !hasLeft && !hasRight)
            {
                _mouseCombo.SelectedIndex = 1; // "Middle"
            }
            else if (hasRight && !hasLeft && !hasMiddle)
            {
                _mouseCombo.SelectedIndex = 2; // "Right"
            }
            else if (hasLeft || hasMiddle || hasRight)
            {
                _mouseCombo.SelectedIndex = 3; // "Any"
            }
            else
            {
                _mouseCombo.SelectedIndex = 4; // "None"
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/set_bind.py:87-91
        // Original: def get_mouse_and_key_binds(self) -> Bind:
        public Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> GetMouseAndKeyBinds()
        {
            // Extract mouse button selection from combo box
            HashSet<PointerUpdateKind> mouseBinds = new HashSet<PointerUpdateKind>();
            
            if (_mouseCombo != null)
            {
                int selectedIndex = _mouseCombo.SelectedIndex;
                string selectedText = _mouseCombo.SelectedItem?.ToString() ?? "";

                // Map combo box selection to PointerUpdateKind values
                switch (selectedIndex)
                {
                    case 0: // "Left"
                        mouseBinds.Add(PointerUpdateKind.LeftButtonPressed);
                        break;
                    case 1: // "Middle"
                        mouseBinds.Add(PointerUpdateKind.MiddleButtonPressed);
                        break;
                    case 2: // "Right"
                        mouseBinds.Add(PointerUpdateKind.RightButtonPressed);
                        break;
                    case 3: // "Any"
                        mouseBinds.Add(PointerUpdateKind.LeftButtonPressed);
                        mouseBinds.Add(PointerUpdateKind.MiddleButtonPressed);
                        mouseBinds.Add(PointerUpdateKind.RightButtonPressed);
                        break;
                    case 4: // "None"
                    default:
                        // Empty set for "None"
                        break;
                }
            }

            // Update internal state to match combo box
            _mouseButtons = mouseBinds;

            return Tuple.Create(new HashSet<Key>(_keybind), mouseBinds);
        }
    }
}

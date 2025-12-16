using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AuroraEngine.Common.Resource.Generics.DLG;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Dialogs.Edit
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_animation.py:17
    // Original: class EditAnimationDialog(QDialog):
    public partial class DialogAnimationDialog : Window
    {
        private HTInstallation _installation;
        private DLGAnimation _animation;
        private ComboBox _animationSelect;
        private TextBox _participantEdit;
        private Button _okButton;
        private Button _cancelButton;

        // Public parameterless constructor for XAML
        public DialogAnimationDialog() : this(null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_animation.py:18-55
        // Original: def __init__(self, parent, installation, animation_arg=None):
        public DialogAnimationDialog(Window parent, HTInstallation installation, DLGAnimation animationArg = null)
        {
            InitializeComponent();
            _installation = installation;
            _animation = animationArg ?? new DLGAnimation();
            SetupUI();
            LoadAnimationData();
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
            Title = "Edit Animation";
            Width = 400;
            Height = 200;

            var panel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };
            var animationLabel = new TextBlock { Text = "Animation:" };
            _animationSelect = new ComboBox();
            var participantLabel = new TextBlock { Text = "Participant:" };
            _participantEdit = new TextBox();
            var okButton = new Button { Content = "OK" };
            okButton.Click += (s, e) => Close();
            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (s, e) => Close();

            panel.Children.Add(animationLabel);
            panel.Children.Add(_animationSelect);
            panel.Children.Add(participantLabel);
            panel.Children.Add(_participantEdit);
            panel.Children.Add(okButton);
            panel.Children.Add(cancelButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _animationSelect = this.FindControl<ComboBox>("animationSelect");
            _participantEdit = this.FindControl<TextBox>("participantEdit");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Close();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_animation.py:43-48
        // Original: Load animation list from 2DA
        private void LoadAnimationData()
        {
            if (_installation == null || _animationSelect == null)
            {
                return;
            }

            // TODO: Load animation list from 2DA when HTInstallation.ht_get_cache_2da is available
            // For now, just set the current values
            if (_participantEdit != null)
            {
                _participantEdit.Text = _animation.Participant ?? "";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_animation.py:51-55
        // Original: def animation(self) -> DLGAnimation:
        public DLGAnimation GetAnimation()
        {
            var animation = new DLGAnimation();
            if (_animationSelect != null)
            {
                animation.AnimationId = _animationSelect.SelectedIndex;
            }
            if (_participantEdit != null)
            {
                animation.Participant = _participantEdit.Text ?? "";
            }
            return animation;
        }
    }
}

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resource.Generics.DLG;
using HolocronToolset.Data;
using HolocronToolset.Widgets.Edit;

namespace HolocronToolset.Dialogs.Edit
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_animation.py:17
    // Original: class EditAnimationDialog(QDialog):
    public partial class DialogAnimationDialog : Window
    {
        private HTInstallation _installation;
        private DLGAnimation _animation;
        private ComboBox2DA _animationSelect;
        private TextBox _participantEdit;
        private Button _okButton;
        private Button _cancelButton;
        private bool _dialogResult = false;

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
            _animationSelect = new ComboBox2DA();
            var participantLabel = new TextBlock { Text = "Participant:" };
            _participantEdit = new TextBox();
            var okButton = new Button { Content = "OK" };
            okButton.Click += (s, e) =>
            {
                _dialogResult = true;
                Close();
            };
            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (s, e) =>
            {
                _dialogResult = false;
                Close();
            };

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
            _animationSelect = this.FindControl<ComboBox2DA>("animationSelect");
            _participantEdit = this.FindControl<TextBox>("participantEdit");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_okButton != null)
            {
                _okButton.Click += (s, e) =>
                {
                    _dialogResult = true;
                    Close();
                };
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) =>
                {
                    _dialogResult = false;
                    Close();
                };
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

            // Matching PyKotor: anim_list: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_DIALOG_ANIMS)
            TwoDA animList = _installation.HtGetCache2DA(HTInstallation.TwoDADialogAnims);
            if (animList == null)
            {
                System.Console.WriteLine($"LoadAnimationData: {HTInstallation.TwoDADialogAnims} not found, the Animation List will not function!!");
                // Set participant even if 2DA is not available
                if (_participantEdit != null)
                {
                    _participantEdit.Text = _animation.Participant ?? "";
                }
                return;
            }

            // Matching PyKotor: self.ui.animationSelect.set_items(anim_list.get_column("name"), sort_alphabetically=True, cleanup_strings=True, ignore_blanks=True)
            List<string> nameColumn = animList.GetColumn("name");
            _animationSelect.SetItems(nameColumn, sortAlphabetically: true, cleanupStrings: true, ignoreBlanks: true);

            // Matching PyKotor: self.ui.animationSelect.setCurrentIndex(animation.animation_id)
            _animationSelect.SetSelectedIndex(_animation.AnimationId);

            // Matching PyKotor: self.ui.animationSelect.set_context(anim_list, installation, HTInstallation.TwoDA_DIALOG_ANIMS)
            _animationSelect.SetContext(animList, _installation, HTInstallation.TwoDADialogAnims);

            // Matching PyKotor: self.ui.participantEdit.setText(animation.participant)
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
                // Matching PyKotor: animation.animation_id = self.ui.animationSelect.currentIndex()
                // ComboBox2DA.SelectedIndex returns the 2DA row index (not the combo box item index)
                animation.AnimationId = _animationSelect.SelectedIndex;
            }
            if (_participantEdit != null)
            {
                animation.Participant = _participantEdit.Text ?? "";
            }
            return animation;
        }

        /// <summary>
        /// Shows the dialog modally and returns true if OK was clicked, false if Cancel was clicked or the dialog was closed.
        /// Matching PyKotor QDialog.exec() behavior.
        /// </summary>
        /// <param name="parent">The parent window for the dialog.</param>
        /// <returns>True if OK was clicked, false otherwise.</returns>
        public new bool ShowDialog(Window parent = null)
        {
            _dialogResult = false;
            if (parent != null)
            {
                var task = base.ShowDialog<bool>(parent);
                task.Wait();
                return task.Result;
            }
            else
            {
                Show();
                // For non-modal case, we can't wait, so return false
                // This shouldn't happen in normal usage
                return false;
            }
        }
    }
}

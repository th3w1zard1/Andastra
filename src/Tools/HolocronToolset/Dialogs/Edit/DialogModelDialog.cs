using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;

namespace HolocronToolset.Dialogs.Edit
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_model.py:15
    // Original: class CutsceneModelDialog(QDialog):
    public partial class DialogModelDialog : Window
    {
        private DLGStunt _stunt;
        private TextBox _participantEdit;
        private TextBox _stuntEdit;
        private Button _okButton;
        private Button _cancelButton;

        // Public parameterless constructor for XAML
        public DialogModelDialog() : this(null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_model.py:16-47
        // Original: def __init__(self, parent, stunt=None):
        public DialogModelDialog(Window parent, DLGStunt stunt = null)
        {
            InitializeComponent();
            _stunt = stunt ?? new DLGStunt();
            SetupUI();
            LoadStuntData();
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
            Title = "Edit Cutscene Model";
            Width = 400;
            Height = 200;

            var panel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };
            var participantLabel = new TextBlock { Text = "Participant:" };
            _participantEdit = new TextBox();
            var stuntLabel = new TextBlock { Text = "Stunt Model:" };
            _stuntEdit = new TextBox();
            var okButton = new Button { Content = "OK" };
            okButton.Click += (s, e) => Close();
            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (s, e) => Close();

            panel.Children.Add(participantLabel);
            panel.Children.Add(_participantEdit);
            panel.Children.Add(stuntLabel);
            panel.Children.Add(_stuntEdit);
            panel.Children.Add(okButton);
            panel.Children.Add(cancelButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _participantEdit = this.FindControl<TextBox>("participantEdit");
            _stuntEdit = this.FindControl<TextBox>("stuntEdit");
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_model.py:39-41
        // Original: Load stunt data
        private void LoadStuntData()
        {
            if (_participantEdit != null)
            {
                _participantEdit.Text = _stunt.Participant ?? "";
            }
            if (_stuntEdit != null)
            {
                _stuntEdit.Text = _stunt.StuntModel?.ToString() ?? "";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/dialog_model.py:43-47
        // Original: def stunt(self) -> DLGStunt:
        public DLGStunt GetStunt()
        {
            var stunt = new DLGStunt();
            if (_participantEdit != null)
            {
                stunt.Participant = _participantEdit.Text ?? "";
            }
            if (_stuntEdit != null)
            {
                string stuntText = _stuntEdit.Text ?? "";
                if (ResRef.IsValid(stuntText))
                {
                    stunt.StuntModel = new ResRef(stuntText);
                }
            }
            return stunt;
        }
    }
}

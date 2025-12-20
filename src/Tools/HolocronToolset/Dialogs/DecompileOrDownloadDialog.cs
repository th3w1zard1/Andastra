using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Dialogs
{
    /// <summary>
    /// Dialog for choosing between decompiling an NCS file or downloading it from GitHub.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2170-2194
    /// </summary>
    public partial class DecompileOrDownloadDialog : Window
    {
        public enum UserChoice
        {
            None,
            Decompile,
            Download,
            Cancel
        }

        public UserChoice Choice { get; private set; } = UserChoice.None;

        public DecompileOrDownloadDialog(string sourceRepoUrl)
        {
            InitializeComponent();
            SetupUI(sourceRepoUrl);
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
            Title = "Decompile or Download";
            MinWidth = 400;
            MinHeight = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(15), Spacing = 15 };

            var messageText = new TextBlock
            {
                Text = "Would you like to decompile this script, or download it from Vanilla Source Repository?",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            mainPanel.Children.Add(messageText);

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };

            var decompileButton = new Button { Content = "Decompile", MinWidth = 100 };
            decompileButton.Click += (s, e) => { Choice = UserChoice.Decompile; Close(); };
            buttonPanel.Children.Add(decompileButton);

            var downloadButton = new Button { Content = "Download", MinWidth = 100 };
            downloadButton.Click += (s, e) => { Choice = UserChoice.Download; Close(); };
            buttonPanel.Children.Add(downloadButton);

            var cancelButton = new Button { Content = "Cancel", MinWidth = 100 };
            cancelButton.Click += (s, e) => { Choice = UserChoice.Cancel; Close(); };
            buttonPanel.Children.Add(cancelButton);

            mainPanel.Children.Add(buttonPanel);
            Content = mainPanel;
        }

        private void SetupUI(string sourceRepoUrl)
        {
            // Find controls from XAML if available
            var decompileButton = this.FindControl<Button>("decompileButton");
            var downloadButton = this.FindControl<Button>("downloadButton");
            var cancelButton = this.FindControl<Button>("cancelButton");

            if (decompileButton != null)
            {
                decompileButton.Click += (s, e) => { Choice = UserChoice.Decompile; Close(); };
            }
            if (downloadButton != null)
            {
                downloadButton.Click += (s, e) => { Choice = UserChoice.Download; Close(); };
            }
            if (cancelButton != null)
            {
                cancelButton.Click += (s, e) => { Choice = UserChoice.Cancel; Close(); };
            }
        }
    }
}


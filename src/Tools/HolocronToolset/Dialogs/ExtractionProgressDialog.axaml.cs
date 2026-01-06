using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace HolocronToolset.Dialogs
{
    /// <summary>
    /// Progress dialog for displaying resource extraction progress.
    /// </summary>
    public partial class ExtractionProgressDialog : Window
    {
        private TextBlock _statusText;
        private ProgressBar _progressBar;
        private TextBlock _progressText;
        private TextBlock _detailText;
        private readonly int _totalItems;
        private int _completedItems;
        private bool _allowClose;

        public ExtractionProgressDialog(int totalItems)
        {
            InitializeComponent();
            _totalItems = totalItems;
            _completedItems = 0;
            _allowClose = false;
            Title = $"Extracting Resources... (0/{_totalItems})";
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _statusText = this.FindControl<TextBlock>("StatusText");
            _progressBar = this.FindControl<ProgressBar>("ProgressBar");
            _progressText = this.FindControl<TextBlock>("ProgressText");
            _detailText = this.FindControl<TextBlock>("DetailText");
        }

        public void UpdateProgress(string statusText)
        {
            // Read _completedItems atomically to avoid race conditions
            int currentCount = Volatile.Read(ref _completedItems);
            UpdateProgress(statusText, currentCount);
        }

        /// <summary>
        /// Updates the progress UI with the specified status text and completed item count.
        /// Thread-safe: UI updates are marshaled to the UI thread.
        /// Note: This method does NOT modify _completedItems - it only updates the UI display.
        /// </summary>
        /// <param name="statusText">Status text to display.</param>
        /// <param name="completedItems">Number of completed items to display.</param>
        public void UpdateProgress(string statusText, int completedItems)
        {
            // Do NOT write back to _completedItems - this would cause race conditions
            // The field is only modified via Interlocked.Increment in IncrementProgress
            Dispatcher.UIThread.Post(() =>
            {
                _statusText.Text = statusText;
                Title = $"Extracting Resources... ({completedItems}/{_totalItems})";

                if (_totalItems > 0)
                {
                    double progressPercent = (double)completedItems / _totalItems * 100;
                    _progressBar.Value = progressPercent;
                    _progressText.Text = $"{(int)progressPercent}%";
                }
            }, DispatcherPriority.Normal);
        }

        /// <summary>
        /// Increments the progress counter and updates the UI.
        /// Thread-safe: counter increment is atomic, and all UI updates are marshaled to the UI thread.
        /// The _completedItems field is only modified via Interlocked.Increment to ensure thread-safety.
        /// </summary>
        /// <param name="statusText">Optional status text to display. If null, preserves current status text.</param>
        public void IncrementProgress(string statusText = null)
        {
            // Increment counter atomically (thread-safe)
            int newCount = Interlocked.Increment(ref _completedItems);

            // Marshal UI update to UI thread
            // Use the captured newCount value - do NOT read _completedItems again
            Dispatcher.UIThread.Post(() =>
            {
                // Use provided status text, or preserve current status text if not provided
                string textToUse = statusText ?? _statusText?.Text ?? $"Extracted {newCount}/{_totalItems} resources";
                // Pass newCount directly - UpdateProgress will NOT write back to _completedItems
                UpdateProgress(textToUse, newCount);
            }, DispatcherPriority.Normal);
        }

        public void AllowClose()
        {
            _allowClose = true;
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}

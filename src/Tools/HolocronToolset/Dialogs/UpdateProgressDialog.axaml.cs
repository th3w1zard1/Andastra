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
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:24
    // Original: class ProgressDialog(QDialog):
    /// <summary>
    /// Progress dialog for displaying update progress. Monitors a progress queue and updates the UI accordingly.
    /// </summary>
    public partial class UpdateProgressDialog : Window
    {
        private TextBlock _statusText;
        private ProgressBar _progressBar;
        private TextBlock _progressText;
        private TextBlock _detailText;
        private readonly Queue<Dictionary<string, object>> _progressQueue;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _allowClose;

        public UpdateProgressDialog(Queue<Dictionary<string, object>> progressQueue)
        {
            InitializeComponent();
            _progressQueue = progressQueue ?? throw new ArgumentNullException(nameof(progressQueue));
            _allowClose = false;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _statusText = this.FindControl<TextBlock>("StatusText");
            _progressBar = this.FindControl<ProgressBar>("ProgressBar");
            _progressText = this.FindControl<TextBlock>("ProgressText");
            _detailText = this.FindControl<TextBlock>("DetailText");
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            StartMonitoring();
        }

        private void StartMonitoring()
        {
            Task.Run(() => MonitorProgress(_cancellationTokenSource.Token));
        }

        private void MonitorProgress(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Dictionary<string, object> data = null;
                    lock (_progressQueue)
                    {
                        if (_progressQueue.Count > 0)
                        {
                            data = _progressQueue.Dequeue();
                        }
                    }

                    if (data != null)
                    {
                        Dispatcher.UIThread.Post(() => UpdateProgress(data), DispatcherPriority.Normal);
                    }
                    else
                    {
                        Thread.Sleep(100); // Wait a bit before checking again
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error monitoring progress: {ex}");
                }
            }
        }

        private void UpdateProgress(Dictionary<string, object> data)
        {
            if (data.ContainsKey("action"))
            {
                string action = data["action"].ToString();
                if (action == "shutdown")
                {
                    _allowClose = true;
                    Close();
                    return;
                }
                if (action == "update_status" && data.ContainsKey("text"))
                {
                    _statusText.Text = data["text"].ToString();
                }
            }

            if (data.ContainsKey("status"))
            {
                string status = data["status"].ToString();
                if (status == "downloading")
                {
                    if (data.ContainsKey("percent"))
                    {
                        int percent = Convert.ToInt32(data["percent"]);
                        _progressBar.Value = percent;
                        _progressText.Text = $"{percent}%";
                    }
                    if (data.ContainsKey("downloaded") && data.ContainsKey("total"))
                    {
                        long downloaded = Convert.ToInt64(data["downloaded"]);
                        long total = Convert.ToInt64(data["total"]);
                        string downloadedStr = FormatBytes(downloaded);
                        string totalStr = FormatBytes(total);
                        _detailText.Text = $"{downloadedStr} / {totalStr}";
                    }
                }
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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
                _cancellationTokenSource?.Cancel();
                base.OnClosing(e);
            }
        }
    }
}


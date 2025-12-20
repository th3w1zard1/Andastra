using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HolocronToolset.Widgets;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:112
    // Original: class AsyncLoader(QDialog, Generic[T]):
    public class AsyncLoaderDialog : Window
    {
        private string _title;
        private Func<object> _task;
        private List<Func<object>> _tasks;
        private string _errorTitle;
        private object _result;
        private Exception _error;
        private List<Exception> _errors;
        private AnimatedProgressBar _progressBar;
        private TextBlock _mainTaskText;
        private TextBlock _subTaskText;
        private TextBlock _taskProgressText;
        private bool _realtimeProgress;
        private bool _startImmediately;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:116-184
        // Original: def __init__(self, parent, title, task, error_title=None, ...):
        public AsyncLoaderDialog(Window parent = null, string title = "Loading...", Func<object> task = null, string errorTitle = null, bool startImmediately = true, bool realtimeProgress = false)
        {
            InitializeComponent();
            _title = title;
            _task = task;
            _tasks = task != null ? new List<Func<object>> { task } : new List<Func<object>>();
            _errorTitle = errorTitle ?? "Error";
            _result = null;
            _error = null;
            _errors = new List<Exception>();
            _realtimeProgress = realtimeProgress;
            _startImmediately = startImmediately;
            SetupUI();
            if (startImmediately)
            {
                StartWorker();
            }
        }

        public AsyncLoaderDialog(Window parent, string title, List<Func<object>> tasks, string errorTitle = null, bool startImmediately = true, bool realtimeProgress = false)
        {
            InitializeComponent();
            _title = title;
            _task = null;
            _tasks = tasks ?? new List<Func<object>>();
            _errorTitle = errorTitle ?? "Error";
            _result = null;
            _error = null;
            _errors = new List<Exception>();
            _realtimeProgress = realtimeProgress;
            _startImmediately = startImmediately;
            SetupUI();
            if (startImmediately)
            {
                StartWorker();
            }
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
            Title = _title;
            MinWidth = 260;
            MinHeight = 40;

            var panel = new StackPanel { Spacing = 6, Margin = new Avalonia.Thickness(20) };

            _mainTaskText = new TextBlock
            {
                Text = "",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                IsVisible = _realtimeProgress || _tasks.Count > 1
            };

            _progressBar = new AnimatedProgressBar
            {
                Minimum = 0,
                Maximum = _tasks.Count > 1 ? _tasks.Count : (_realtimeProgress ? 1 : 0),
                IsVisible = true
            };

            _subTaskText = new TextBlock
            {
                Text = "",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                IsVisible = _realtimeProgress
            };

            _taskProgressText = new TextBlock
            {
                Text = "",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                IsVisible = _tasks.Count > 1
            };

            panel.Children.Add(_mainTaskText);
            panel.Children.Add(_progressBar);
            panel.Children.Add(_subTaskText);
            panel.Children.Add(_taskProgressText);

            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML if available
            _progressBar = this.FindControl<AnimatedProgressBar>("progressBar");
            _mainTaskText = this.FindControl<TextBlock>("mainTaskText");
            _subTaskText = this.FindControl<TextBlock>("subTaskText");
            _taskProgressText = this.FindControl<TextBlock>("taskProgressText");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:242-243
        // Original: def start_worker(self):
        public void StartWorker()
        {
            Task.Run(() => RunTasks());
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:336-351
        // Original: def run(self) in AsyncWorker:
        private void RunTasks()
        {
            object result = null;
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks.Count > 1)
                {
                    Dispatcher.UIThread.Post(() => OnProgress(1, "increment"));
                }

                try
                {
                    result = _tasks[i]();
                    Dispatcher.UIThread.Post(() => OnSuccessful(result));
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => OnFailed(ex));
                }
            }

            Dispatcher.UIThread.Post(() => OnCompleted());
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:251-256
        // Original: def _on_successful(self, result: Any):
        private void OnSuccessful(object result)
        {
            _result = result;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:258-267
        // Original: def _on_failed(self, error: Exception):
        private void OnFailed(Exception error)
        {
            _errors.Add(error);
            if (_errors.Count == 1)
            {
                _error = error;
            }
            System.Console.WriteLine($"AsyncLoader error: {error}");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:269-275
        // Original: def _on_completed(self):
        private void OnCompleted()
        {
            if (_error != null)
            {
                Close();
                ShowErrorDialog();
            }
            else
            {
                Close();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:277-287
        // Original: def _show_error_dialog(self):
        private void ShowErrorDialog()
        {
            if (string.IsNullOrWhiteSpace(_errorTitle))
            {
                System.Console.WriteLine($"Error: {_error}");
                return;
            }

            string errorMessage = _error?.Message ?? "An unknown error occurred.";
            if (_errors != null && _errors.Count > 1)
            {
                errorMessage = $"Multiple errors occurred:\n\n";
                for (int i = 0; i < _errors.Count; i++)
                {
                    errorMessage += $"Error in task {i + 1}: {_errors[i].Message}\n";
                }
            }

            var errorBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                _errorTitle,
                errorMessage,
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error);
            errorBox.ShowAsync();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:289-306
        // Original: def _on_progress(self, value, task_type):
        private void OnProgress(int value, string taskType)
        {
            if (taskType == "increment")
            {
                if (_progressBar != null)
                {
                    _progressBar.Value = Math.Min(_progressBar.Value + value, _progressBar.Maximum);
                }
            }
            else if (taskType == "set_maximum")
            {
                if (_progressBar != null)
                {
                    _progressBar.Maximum = value;
                }
            }
            else if (taskType == "update_maintask_text")
            {
                if (_mainTaskText != null)
                {
                    _mainTaskText.Text = value.ToString();
                }
            }
            else if (taskType == "update_subtask_text")
            {
                if (_subTaskText != null)
                {
                    _subTaskText.Text = value.ToString();
                }
            }

            if (_taskProgressText != null && _progressBar != null)
            {
                _taskProgressText.Text = $"{_progressBar.Value}/{_progressBar.Maximum}";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/asyncloader.py:235-240
        // Original: def progress_callback_api(self, data, mtype):
        public void ProgressCallbackApi(int data, string mtype)
        {
            OnProgress(data, mtype);
        }

        public void ProgressCallbackApi(string data, string mtype)
        {
            if (mtype == "update_maintask_text" && _mainTaskText != null)
            {
                _mainTaskText.Text = data;
            }
            else if (mtype == "update_subtask_text" && _subTaskText != null)
            {
                _subTaskText.Text = data;
            }
        }

        public object Result => _result;
        public Exception Error => _error;
        public List<Exception> Errors => _errors;
    }
}

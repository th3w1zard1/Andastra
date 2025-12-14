using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace HolocronToolset.NET.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:19
    // Original: class TerminalWidget(QWidget):
    public partial class TerminalWidget : UserControl
    {
        private TextBox _terminalOutput;
        private List<string> _commandHistory;
        private int _historyIndex;
        private string _currentCommand;
        private string _prompt;
        private Process _process;

        // Public parameterless constructor for XAML
        public TerminalWidget()
        {
            InitializeComponent();
            _commandHistory = new List<string>();
            _historyIndex = -1;
            _currentCommand = "";
            _prompt = GetPrompt();
            SetupUI();
            SetupProcess();
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
            var panel = new StackPanel();

            _terminalOutput = new TextBox
            {
                IsReadOnly = false,
                AcceptsReturn = true,
                AcceptsTab = false,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10
            };

            ApplyTerminalTheme();
            panel.Children.Add(_terminalOutput);
            Content = panel;

            WriteOutput("Holocron Toolset Terminal\n");
            WriteOutput("Type 'help' for available commands.\n\n");
            WritePrompt();
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _terminalOutput = this.FindControl<TextBox>("terminalOutput");
            if (_terminalOutput != null)
            {
                ApplyTerminalTheme();
                _terminalOutput.KeyDown += OnKeyDown;
                WriteOutput("Holocron Toolset Terminal\n");
                WriteOutput("Type 'help' for available commands.\n\n");
                WritePrompt();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:74-106
        // Original: def _apply_terminal_theme(self):
        private void ApplyTerminalTheme()
        {
            if (_terminalOutput != null)
            {
                _terminalOutput.Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(30, 30, 30));
                _terminalOutput.Foreground = new SolidColorBrush(Avalonia.Media.Color.FromRgb(204, 204, 204));
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:108-114
        // Original: def _setup_process(self):
        private void SetupProcess()
        {
            // TODO: Set up process for command execution when Process integration is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:116-121
        // Original: def _get_prompt(self) -> str:
        private string GetPrompt()
        {
            string cwd = Directory.GetCurrentDirectory();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return $"{cwd}> ";
            }
            return $"{cwd}$ ";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:123-144
        // Original: def _write_output(self, text: str):
        private void WriteOutput(string text)
        {
            if (_terminalOutput != null)
            {
                _terminalOutput.Text += text;
                // Scroll to end
                _terminalOutput.CaretIndex = _terminalOutput.Text.Length;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:149-251
        // Original: def _write_prompt(self) and other methods:
        private void WritePrompt()
        {
            WriteOutput(_prompt);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // TODO: Handle keyboard input for terminal commands
            if (e.Key == Key.Enter)
            {
                ExecuteCommand();
            }
            else if (e.Key == Key.Up)
            {
                NavigateHistory(-1);
            }
            else if (e.Key == Key.Down)
            {
                NavigateHistory(1);
            }
        }

        private void ExecuteCommand()
        {
            if (_terminalOutput == null)
            {
                return;
            }

            string text = _terminalOutput.Text;
            int lastPromptIndex = text.LastIndexOf(_prompt);
            if (lastPromptIndex >= 0)
            {
                string command = text.Substring(lastPromptIndex + _prompt.Length).Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    _commandHistory.Add(command);
                    _historyIndex = _commandHistory.Count;
                    WriteOutput("\n");
                    ProcessCommand(command);
                    WritePrompt();
                }
            }
        }

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0)
            {
                return;
            }

            _historyIndex += direction;
            if (_historyIndex < 0)
            {
                _historyIndex = 0;
            }
            else if (_historyIndex >= _commandHistory.Count)
            {
                _historyIndex = _commandHistory.Count - 1;
            }

            if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count)
            {
                _currentCommand = _commandHistory[_historyIndex];
                // TODO: Replace current line with history command
            }
        }

        private void ProcessCommand(string command)
        {
            if (command == "help")
            {
                WriteOutput("Available commands:\n");
                WriteOutput("  help - Show this help message\n");
                WriteOutput("  clear - Clear the terminal\n");
                WriteOutput("  exit - Exit the terminal\n");
            }
            else if (command == "clear")
            {
                if (_terminalOutput != null)
                {
                    _terminalOutput.Text = "";
                    WritePrompt();
                }
            }
            else if (command == "exit")
            {
                // TODO: Handle exit command
            }
            else
            {
                WriteOutput($"Command not found: {command}\n");
            }
        }
    }
}

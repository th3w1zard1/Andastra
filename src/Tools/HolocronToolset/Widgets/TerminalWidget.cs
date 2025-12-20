using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:19
    // Original: class TerminalWidget(QWidget):
    public partial class TerminalWidget : UserControl
    {
        private TextBox _terminalOutput;
        private List<string> _commandHistory;
        private int _historyIndex;
        private string _prompt;
        private Process _process;
        private int _promptStartPos;

        // Public parameterless constructor for XAML
        public TerminalWidget()
        {
            InitializeComponent();
            _commandHistory = new List<string>();
            _historyIndex = -1;
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
            _terminalOutput.KeyDown += OnKeyDown;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:149-153
        // Original: def _write_prompt(self):
        private void WritePrompt()
        {
            _prompt = GetPrompt();
            WriteOutput(_prompt);
            MarkPromptStart();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:155-157
        // Original: def _mark_prompt_start(self):
        private void MarkPromptStart()
        {
            if (_terminalOutput != null)
            {
                _promptStartPos = _terminalOutput.CaretIndex;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:159-164
        // Original: def _get_current_command(self) -> str:
        private string GetCurrentCommand()
        {
            if (_terminalOutput == null)
            {
                return "";
            }

            string text = _terminalOutput.Text;
            if (_promptStartPos >= text.Length)
            {
                return "";
            }

            return text.Substring(_promptStartPos).Trim();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:166-171
        // Original: def _clear_current_command(self):
        private void ClearCurrentCommand()
        {
            if (_terminalOutput == null)
            {
                return;
            }

            string text = _terminalOutput.Text;
            if (_promptStartPos < text.Length)
            {
                _terminalOutput.Text = text.Substring(0, _promptStartPos);
                _terminalOutput.CaretIndex = _promptStartPos;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:173-176
        // Original: def _replace_current_command(self, text: str):
        private void ReplaceCurrentCommand(string text)
        {
            ClearCurrentCommand();
            if (_terminalOutput != null && !string.IsNullOrEmpty(text))
            {
                _terminalOutput.Text += text;
                _terminalOutput.CaretIndex = _terminalOutput.Text.Length;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:178-252
        // Original: def _handle_key_press(self, event: QKeyEvent):
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_terminalOutput == null)
            {
                return;
            }

            Key key = e.Key;
            KeyModifiers modifiers = e.KeyModifiers;

            // Prevent editing before the prompt
            int cursorPos = _terminalOutput.CaretIndex;
            if (cursorPos < _promptStartPos)
            {
                // Prevent deletion/navigation before prompt
                if (key == Key.Back || key == Key.Left || key == Key.Delete)
                {
                    e.Handled = true;
                    return;
                }
                // Move cursor to end if trying to type (unless Ctrl/Alt modifier)
                if (!modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Alt))
                {
                    _terminalOutput.CaretIndex = _terminalOutput.Text.Length;
                    e.Handled = false; // Allow the key press to proceed
                }
            }

            // Handle Enter key - execute command
            if (key == Key.Enter || key == Key.Return)
            {
                e.Handled = true;
                string command = GetCurrentCommand().Trim();
                WriteOutput("\n");

                if (!string.IsNullOrEmpty(command))
                {
                    _commandHistory.Add(command);
                    _historyIndex = _commandHistory.Count;
                    ExecuteCommand(command);
                }
                else
                {
                    WritePrompt();
                }
                return;
            }

            // Handle Up arrow - previous command
            if (key == Key.Up)
            {
                e.Handled = true;
                if (_commandHistory.Count > 0)
                {
                    if (_historyIndex > 0)
                    {
                        _historyIndex--;
                        ReplaceCurrentCommand(_commandHistory[_historyIndex]);
                    }
                    else if (_historyIndex == -1 || _historyIndex == _commandHistory.Count)
                    {
                        // Start from the last command in history
                        _historyIndex = _commandHistory.Count - 1;
                        ReplaceCurrentCommand(_commandHistory[_historyIndex]);
                    }
                }
                return;
            }

            // Handle Down arrow - next command
            if (key == Key.Down)
            {
                e.Handled = true;
                if (_commandHistory.Count > 0)
                {
                    if (_historyIndex < _commandHistory.Count - 1)
                    {
                        _historyIndex++;
                        ReplaceCurrentCommand(_commandHistory[_historyIndex]);
                    }
                    else if (_historyIndex == _commandHistory.Count - 1)
                    {
                        _historyIndex = _commandHistory.Count;
                        ClearCurrentCommand();
                    }
                }
                return;
            }

            // Handle Ctrl+C - cancel current command
            if (key == Key.C && modifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                if (_process != null && !_process.HasExited)
                {
                    try
                    {
                        _process.Kill();
                        WriteOutput("\n^C\n");
                    }
                    catch
                    {
                        WriteOutput("\n^C\n");
                    }
                }
                else
                {
                    WriteOutput("^C\n");
                }
                WritePrompt();
                return;
            }

            // Handle Ctrl+L - clear screen
            if (key == Key.L && modifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                ClearTerminal();
                return;
            }

            // Handle Backspace - don't delete prompt
            if (key == Key.Back)
            {
                if (cursorPos <= _promptStartPos)
                {
                    e.Handled = true;
                    return;
                }
                // Allow default handling for backspace
            }

            // For all other keys, allow default handling
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:254-285
        // Original: def _execute_command(self, command: str):
        private void ExecuteCommand(string command)
        {
            // Handle built-in commands
            if (command == "clear" || command == "cls")
            {
                ClearTerminal();
                return;
            }
            else if (command == "help")
            {
                ShowHelp();
                return;
            }
            else if (command.StartsWith("cd "))
            {
                ChangeDirectory(command.Substring(3).Trim());
                return;
            }
            else if (command == "exit")
            {
                // Exit command - in a real terminal this might close the terminal
                // For now, just output a message
                WriteOutput("Use the close button to exit the terminal.\n");
                WritePrompt();
                return;
            }

            // Execute external command (when process integration is available)
            // TODO: Set up process for command execution when Process integration is available
            WriteOutput($"Command not found: {command}\n");
            WritePrompt();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:328-355
        // Original: def _change_directory(self, path: str):
        private void ChangeDirectory(string path)
        {
            try
            {
                // Expand user home directory
                if (path.StartsWith("~"))
                {
                    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (path.Length > 1)
                    {
                        path = Path.Combine(homeDir, path.Substring(2));
                    }
                    else
                    {
                        path = homeDir;
                    }
                }

                // Handle relative paths
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(Directory.GetCurrentDirectory(), path);
                }

                // Normalize the path
                path = Path.GetFullPath(path);

                if (Directory.Exists(path))
                {
                    Directory.SetCurrentDirectory(path);
                    WriteOutput($"Changed directory to: {path}\n");
                }
                else
                {
                    WriteOutput($"Error: Directory not found: {path}\n");
                }
            }
            catch (Exception ex)
            {
                WriteOutput($"Error changing directory: {ex.Message}\n");
            }

            WritePrompt();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:357-374
        // Original: def _show_help(self):
        private void ShowHelp()
        {
            string helpText = @"
Available built-in commands:
  clear/cls  - Clear the terminal screen
  cd <path>  - Change the current directory
  help       - Show this help message
  
Keyboard shortcuts:
  Ctrl+C     - Cancel current command
  Ctrl+L     - Clear screen
  Up Arrow   - Previous command
  Down Arrow - Next command

You can also run any system command directly.
";
            WriteOutput(helpText);
            WritePrompt();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/terminal_widget.py:376-380
        // Original: def clear(self):
        private void ClearTerminal()
        {
            if (_terminalOutput != null)
            {
                _terminalOutput.Text = "";
                WriteOutput("Holocron Toolset Terminal\n\n");
                WritePrompt();
            }
        }

    }
}

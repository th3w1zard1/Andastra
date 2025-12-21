using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using HolocronToolset.Common;

namespace HolocronToolset.Common.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:30
    // Original: class FindReplaceWidget(QWidget):
    public class FindReplaceWidget : UserControl
    {
        private TextBox _findInput;
        private TextBox _replaceInput;
        private Button _prevButton;
        private Button _nextButton;
        private Button _replaceButton;
        private Button _replaceAllButton;
        private Button _closeButton;
        private CheckBox _caseSensitiveCheck;
        private CheckBox _wholeWordsCheck;
        private CheckBox _regexCheck;
        private bool _showReplace;

        public event Action<string, bool, bool, bool> FindRequested;
        public event Action<string, string, bool, bool, bool> ReplaceRequested;
        public event Action<string, string, bool, bool, bool> ReplaceAllRequested;
        public event Action CloseRequested;
        public event Action FindNextRequested;
        public event Action FindPreviousRequested;

        public FindReplaceWidget()
        {
            SetupUI();
            IsVisible = false;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:45-152
        // Original: def setup_ui(self):
        private void SetupUI()
        {
            var layout = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Avalonia.Thickness(8, 4, 8, 4),
                Spacing = 6
            };

            // Find input
            var findLabel = new TextBlock { Text = Localization.Translate("Find:") + " " };
            _findInput = new TextBox
            {
                Watermark = Localization.Translate("Find..."),
                MinWidth = 200
            };
            _findInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    OnFindNext();
                    e.Handled = true;
                }
            };
            _findInput.TextChanged += (s, e) => OnFindTextChanged();

            // Replace input (initially hidden)
            var replaceLabel = new TextBlock { Text = Localization.Translate("Replace:") + " ", IsVisible = false };
            _replaceInput = new TextBox
            {
                Watermark = Localization.Translate("Replace..."),
                MinWidth = 200,
                IsVisible = false
            };
            _replaceInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    OnReplaceNext();
                    e.Handled = true;
                }
            };

            // Buttons
            _prevButton = new Button
            {
                Content = "↑",
                MaxWidth = 30
            };
            ToolTip.SetTip(_prevButton, Localization.Translate("Find Previous (Shift+F3)"));
            _prevButton.Click += (s, e) => OnFindPrevious();

            _nextButton = new Button
            {
                Content = "↓",
                MaxWidth = 30
            };
            ToolTip.SetTip(_nextButton, Localization.Translate("Find Next (F3)"));
            _nextButton.Click += (s, e) => OnFindNext();

            _replaceButton = new Button
            {
                Content = Localization.Translate("Replace"),
                IsEnabled = false
            };
            _replaceButton.Click += (s, e) => OnReplace();

            _replaceAllButton = new Button
            {
                Content = Localization.Translate("Replace All"),
                IsEnabled = false
            };
            _replaceAllButton.Click += (s, e) => OnReplaceAll();

            _closeButton = new Button
            {
                Content = "✕",
                MaxWidth = 25
            };
            ToolTip.SetTip(_closeButton, Localization.Translate("Close (Escape)"));
            _closeButton.Click += (s, e) => { IsVisible = false; CloseRequested?.Invoke(); };

            // Options
            _caseSensitiveCheck = new CheckBox
            {
                Content = "Aa"
            };
            ToolTip.SetTip(_caseSensitiveCheck, Localization.Translate("Match Case"));
            _caseSensitiveCheck.IsCheckedChanged += (s, e) => OnOptionsChanged();

            _wholeWordsCheck = new CheckBox
            {
                Content = "Ab"
            };
            ToolTip.SetTip(_wholeWordsCheck, Localization.Translate("Match Whole Word"));
            _wholeWordsCheck.IsCheckedChanged += (s, e) => OnOptionsChanged();

            _regexCheck = new CheckBox
            {
                Content = ".*"
            };
            ToolTip.SetTip(_regexCheck, Localization.Translate("Use Regular Expression"));
            _regexCheck.IsCheckedChanged += (s, e) => OnOptionsChanged();

            // Add to layout
            layout.Children.Add(findLabel);
            layout.Children.Add(_findInput);
            layout.Children.Add(_prevButton);
            layout.Children.Add(_nextButton);
            layout.Children.Add(replaceLabel);
            layout.Children.Add(_replaceInput);
            layout.Children.Add(_replaceButton);
            layout.Children.Add(_replaceAllButton);
            layout.Children.Add(_caseSensitiveCheck);
            layout.Children.Add(_wholeWordsCheck);
            layout.Children.Add(_regexCheck);
            layout.Children.Add(_closeButton);

            Content = layout;
            _showReplace = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:156-169
            // Original: def keyPressEvent(self, event: QKeyEvent):
            if (e.Key == Key.Escape)
            {
                IsVisible = false;
                CloseRequested?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    OnFindPrevious();
                }
                else
                {
                    OnFindNext();
                }
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.H)
            {
                ToggleReplace();
                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:171-186
        // Original: def show_find(self, text: str | None = None):
        public void ShowFind(string text = null)
        {
            _showReplace = false;
            _replaceInput.IsVisible = false;
            _replaceButton.IsVisible = false;
            _replaceAllButton.IsVisible = false;
            IsVisible = true;
            if (!string.IsNullOrEmpty(text))
            {
                _findInput.Text = text;
                _findInput.SelectAll();
            }
            else
            {
                _findInput.SelectAll();
            }
            _findInput.Focus();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:188-202
        // Original: def show_replace(self, text: str | None = None):
        public void ShowReplace(string text = null)
        {
            _showReplace = true;
            _replaceInput.IsVisible = true;
            _replaceButton.IsVisible = true;
            _replaceAllButton.IsVisible = true;
            IsVisible = true;
            if (!string.IsNullOrEmpty(text))
            {
                _findInput.Text = text;
                _findInput.SelectAll();
            }
            else
            {
                _findInput.SelectAll();
            }
            _findInput.Focus();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:204-209
        // Original: def toggle_replace(self):
        public void ToggleReplace()
        {
            if (_showReplace)
            {
                ShowFind();
            }
            else
            {
                ShowReplace();
            }
        }

        // Matching PyKotor implementation - convenience methods for setting mode
        // Original: Methods to set find or replace mode explicitly
        public void SetFindMode()
        {
            ShowFind();
        }

        public void SetReplaceMode()
        {
            ShowReplace();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:211-221
        // Original: def _on_find_text_changed(self):
        private void OnFindTextChanged()
        {
            bool hasText = !string.IsNullOrEmpty(_findInput.Text);
            _nextButton.IsEnabled = hasText;
            _prevButton.IsEnabled = hasText;
            _replaceButton.IsEnabled = hasText;
            _replaceAllButton.IsEnabled = hasText && !string.IsNullOrEmpty(_replaceInput.Text);

            // Auto-search as user types
            if (hasText)
            {
                OnFindNext();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:223-233
        // Original: def _on_find_next(self):
        private void OnFindNext()
        {
            string text = _findInput.Text;
            if (!string.IsNullOrEmpty(text))
            {
                FindRequested?.Invoke(
                    text,
                    _caseSensitiveCheck.IsChecked == true,
                    _wholeWordsCheck.IsChecked == true,
                    _regexCheck.IsChecked == true);
                FindNextRequested?.Invoke();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:235-245
        // Original: def _on_find_previous(self):
        private void OnFindPrevious()
        {
            string text = _findInput.Text;
            if (!string.IsNullOrEmpty(text))
            {
                FindRequested?.Invoke(
                    text,
                    _caseSensitiveCheck.IsChecked == true,
                    _wholeWordsCheck.IsChecked == true,
                    _regexCheck.IsChecked == true);
                FindPreviousRequested?.Invoke();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:247-258
        // Original: def _on_replace(self):
        private void OnReplace()
        {
            string findText = _findInput.Text;
            string replaceText = _replaceInput.Text ?? "";
            if (!string.IsNullOrEmpty(findText))
            {
                ReplaceRequested?.Invoke(
                    findText,
                    replaceText,
                    _caseSensitiveCheck.IsChecked == true,
                    _wholeWordsCheck.IsChecked == true,
                    _regexCheck.IsChecked == true);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:260-265
        // Original: def _on_replace_next(self):
        private void OnReplaceNext()
        {
            OnReplace();
            OnFindNext();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:267-278
        // Original: def _on_replace_all(self):
        private void OnReplaceAll()
        {
            string findText = _findInput.Text;
            string replaceText = _replaceInput.Text ?? "";
            if (!string.IsNullOrEmpty(findText))
            {
                ReplaceAllRequested?.Invoke(
                    findText,
                    replaceText,
                    _caseSensitiveCheck.IsChecked == true,
                    _wholeWordsCheck.IsChecked == true,
                    _regexCheck.IsChecked == true);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/find_replace_widget.py:280-283
        // Original: def _on_options_changed(self):
        private void OnOptionsChanged()
        {
            if (!string.IsNullOrEmpty(_findInput.Text))
            {
                OnFindNext();
            }
        }

        // Getter methods for accessing current find/replace parameters
        // These methods allow the editor to access the current state of the find/replace widget
        public string GetFindText()
        {
            return _findInput?.Text ?? "";
        }

        public string GetReplaceText()
        {
            return _replaceInput?.Text ?? "";
        }

        public bool GetCaseSensitive()
        {
            return _caseSensitiveCheck?.IsChecked == true;
        }

        public bool GetWholeWords()
        {
            return _wholeWordsCheck?.IsChecked == true;
        }

        public bool GetRegex()
        {
            return _regexCheck?.IsChecked == true;
        }
    }
}

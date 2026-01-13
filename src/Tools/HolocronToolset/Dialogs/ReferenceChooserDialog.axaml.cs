using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Editors;
using HolocronToolset.Editors.DLG;

namespace HolocronToolset.Dialogs
{
    /// <summary>
    /// Dialog for choosing and navigating between DLG node references.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/widget_windows.py:31-204
    /// Original: class ReferenceChooserDialog(QDialog):
    /// </summary>
    public partial class ReferenceChooserDialog : Window
    {
        private DLGListWidget _listWidget;
        private Button _backButton;
        private Button _forwardButton;
        private Button _okButton;
        private Button _cancelButton;
        private DLGEditor _editor;
        private List<WeakReference<DLGLink>> _references;
        private string _itemHtml;

        /// <summary>
        /// Event raised when an item is chosen.
        /// </summary>
        public event EventHandler<DLGListWidgetItem> ItemChosen;

        /// <summary>
        /// Initializes a new instance of ReferenceChooserDialog.
        /// </summary>
        /// <param name="references">List of weak references to DLG links.</param>
        /// <param name="editor">The DLG editor parent.</param>
        /// <param name="itemText">HTML text describing the item being referenced.</param>
        public ReferenceChooserDialog(List<WeakReference<DLGLink>> references, DLGEditor editor, string itemText)
        {
            if (editor == null)
            {
                throw new ArgumentNullException(nameof(editor));
            }
            _editor = editor;
            _references = references ?? new List<WeakReference<DLGLink>>();
            _itemHtml = itemText ?? "";

            InitializeComponent();
            SetupUI();
            UpdateReferences(_references, _itemHtml);
        }

        private void InitializeComponent()
        {
            try
            {
                AvaloniaXamlLoader.Load(this);
                _listWidget = this.FindControl<DLGListWidget>("ListWidget");
                _backButton = this.FindControl<Button>("BackButton");
                _forwardButton = this.FindControl<Button>("ForwardButton");
                _okButton = this.FindControl<Button>("OkButton");
                _cancelButton = this.FindControl<Button>("CancelButton");
            }
            catch
            {
                // XAML loading failed, use programmatic UI
            }

            if (_listWidget == null || _backButton == null || _forwardButton == null || _okButton == null || _cancelButton == null)
            {
                // Fallback to programmatic UI if XAML loading fails or controls not found
                SetupProgrammaticUI();
            }
        }

        private void SetupProgrammaticUI()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            _listWidget = new DLGListWidget(_editor);
            Grid.SetRow(_listWidget, 0);
            grid.Children.Add(_listWidget);

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(10)
            };

            _backButton = new Button { Content = "◄", Width = 40, Margin = new Avalonia.Thickness(5, 0) };
            _forwardButton = new Button { Content = "►", Width = 40, Margin = new Avalonia.Thickness(5, 0) };
            _okButton = new Button { Content = "OK", Width = 80, Margin = new Avalonia.Thickness(10, 0, 5, 0) };
            _cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Avalonia.Thickness(5, 0) };

            buttonPanel.Children.Add(_backButton);
            buttonPanel.Children.Add(_forwardButton);
            buttonPanel.Children.Add(_okButton);
            buttonPanel.Children.Add(_cancelButton);

            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }

        private void SetupUI()
        {
            if (_listWidget != null)
            {
                _listWidget.Editor = _editor;
                _listWidget.SelectionChanged += (s, e) =>
                {
                    // Handle selection change if needed
                };
            }

            if (_backButton != null)
            {
                _backButton.Click += (s, e) => GoBack();
            }

            if (_forwardButton != null)
            {
                _forwardButton.Click += (s, e) => GoForward();
            }

            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Accept();
            }

            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }

            UpdateButtonStates();
        }

        /// <summary>
        /// Updates the references displayed in the dialog.
        /// Matching PyKotor implementation: def update_references(self, referenceItems: list[weakref.ref[DLGLink]], item_text: str)
        /// </summary>
        public void UpdateReferences(List<WeakReference<DLGLink>> referenceItems, string itemText)
        {
            _references = referenceItems ?? new List<WeakReference<DLGLink>>();
            _itemHtml = itemText ?? "";

            if (_listWidget == null)
            {
                return;
            }

            _listWidget.Clear();

            string nodePath = "";
            foreach (var linkRef in _references)
            {
                if (linkRef == null || !linkRef.TryGetTarget(out DLGLink link) || link == null)
                {
                    continue;
                }

                var listItem = new DLGListWidgetItem(link, linkRef);
                _listWidget.UpdateItem(listItem);
                _listWidget.AddItem(listItem);

                if (string.IsNullOrEmpty(nodePath) && link.Node != null)
                {
                    nodePath = link.Node.Path() ?? "";
                }
            }

            Title = string.IsNullOrEmpty(nodePath) ? "Node References" : $"Node References: {nodePath}";
            UpdateButtonStates();
        }

        /// <summary>
        /// Accepts the dialog and raises the ItemChosen event.
        /// Matching PyKotor implementation: def accept(self)
        /// </summary>
        private void Accept()
        {
            if (_listWidget != null && _listWidget.SelectedItem is DLGListWidgetItem selectedItem)
            {
                ItemChosen?.Invoke(this, selectedItem);
            }
            Close();
        }

        /// <summary>
        /// Navigates back in the reference history.
        /// Matching PyKotor implementation: def go_back(self)
        /// </summary>
        private void GoBack()
        {
            if (_editor != null)
            {
                _editor.NavigateBack();
                UpdateButtonStates();
            }
        }

        /// <summary>
        /// Navigates forward in the reference history.
        /// Matching PyKotor implementation: def go_forward(self)
        /// </summary>
        private void GoForward()
        {
            if (_editor != null)
            {
                _editor.NavigateForward();
                UpdateButtonStates();
            }
        }

        /// <summary>
        /// Updates the enabled state of navigation buttons.
        /// Matching PyKotor implementation: def update_button_states(self)
        /// </summary>
        private void UpdateButtonStates()
        {
            if (_editor == null)
            {
                return;
            }

            if (_backButton != null)
            {
                _backButton.IsEnabled = _editor.CurrentReferenceIndex > 0;
            }

            if (_forwardButton != null)
            {
                _forwardButton.IsEnabled = _editor.CurrentReferenceIndex < _editor.ReferenceHistoryCount - 1;
            }
        }
    }
}


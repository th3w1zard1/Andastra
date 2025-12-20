using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvRichTextBox;
using HoloPatcher.UI.Rte;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using AvaloniaTextElement = Avalonia.Controls.Documents.TextElement;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using TextRange = AvRichTextBox.TextRange;
using TextAlignment = Avalonia.Media.TextAlignment;
using TextDecorationCollection = Avalonia.Media.TextDecorationCollection;
using TextDecoration = Avalonia.Media.TextDecoration;
using FontWeight = Avalonia.Media.FontWeight;
using FontStyle = Avalonia.Media.FontStyle;
using FontFamily = Avalonia.Media.FontFamily;
using SolidColorBrush = Avalonia.Media.SolidColorBrush;
using Color = Avalonia.Media.Color;
using Colors = Avalonia.Media.Colors;

namespace HoloPatcher.UI.Views
{
    public partial class RteEditorWindow : Window
    {
        private readonly string _initialDirectory;
        private string _currentFilePath;
        private bool _isDirty;

        // Control fields - initialized in InitializeComponent
        private ComboBox FontSizeComboBox;
        private ComboBox FontFamilyComboBox;
        private ComboBox ForegroundComboBox;
        private ComboBox BackgroundComboBox;
        private RichTextBox Editor;

        public RteEditorWindow(string initialDirectory = null)
        {
            InitializeComponent();
            _initialDirectory = initialDirectory;
            PopulateFontSelector();
            // These fields are initialized by InitializeComponent from the XAML x:Name attributes
            if (FontSizeComboBox != null) FontSizeComboBox.SelectedIndex = 2;
            if (ForegroundComboBox != null) ForegroundComboBox.SelectedIndex = 0;
            if (BackgroundComboBox != null) BackgroundComboBox.SelectedIndex = 0;
            if (Editor != null)
            {
                Editor.FlowDocument.Selection_Changed += OnSelectionChanged;
                Editor.AddHandler(KeyUpEvent, OnEditorKeyUp, Avalonia.Interactivity.RoutingStrategies.Bubble);
            }
            _ = InitializeNewDocumentAsync();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            FontSizeComboBox = this.FindControl<ComboBox>("FontSizeComboBox");
            FontFamilyComboBox = this.FindControl<ComboBox>("FontFamilyComboBox");
            ForegroundComboBox = this.FindControl<ComboBox>("ForegroundComboBox");
            BackgroundComboBox = this.FindControl<ComboBox>("BackgroundComboBox");
            Editor = this.FindControl<RichTextBox>("Editor");
        }

        private void PopulateFontSelector()
        {
            System.Collections.Generic.IEnumerable<string> fonts = FontManager.Current.SystemFonts
                .Select(f => f.Name)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .Take(30); // keep list manageable

            foreach (string font in fonts)
            {
                FontFamilyComboBox.Items.Add(new ComboBoxItem { Content = font });
            }

            FontFamilyComboBox.SelectedIndex = 0;
        }

        private async Task InitializeNewDocumentAsync()
        {
            if (!await ConfirmDiscardChangesAsync())
            {
                return;
            }

            Editor.FlowDocument = new FlowDocument();
            _currentFilePath = null;
            _isDirty = false;
            UpdateTitle();
        }

        private void OnNewDocument(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _ = InitializeNewDocumentAsync();
        }

        private async void OnOpenDocument(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!await ConfirmDiscardChangesAsync())
            {
                return;
            }

            var options = new FilePickerOpenOptions
            {
                Title = "Open info.rte",
                SuggestedStartLocation = await GetStartLocationAsync(),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Rich Text Editor (*.rte)") { Patterns = new[] { "*.rte" } }
                }
            };

            System.Collections.Generic.IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
            {
                return;
            }

            string path = files[0].TryGetLocalPath();
            if (path is null)
            {
                return;
            }

            string json = await File.ReadAllTextAsync(path);
            var document = RteDocument.Parse(json);
            RteDocumentConverter.ApplyToRichTextBox(Editor, document);
            _currentFilePath = path;
            _isDirty = false;
            UpdateTitle();
        }

        private async void OnSaveDocument(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveDocumentAsync(false);
        }

        private async void OnSaveDocumentAs(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveDocumentAsync(true);
        }

        private async Task SaveDocumentAsync(bool saveAs)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || saveAs)
            {
                var options = new FilePickerSaveOptions
                {
                    Title = "Save info.rte",
                    SuggestedStartLocation = await GetStartLocationAsync(),
                    SuggestedFileName = string.IsNullOrEmpty(_currentFilePath) ? "info.rte" : Path.GetFileName(_currentFilePath),
                    DefaultExtension = "rte",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Rich Text Editor (*.rte)") { Patterns = new[] { "*.rte" } }
                    }
                };

                IStorageFile file = await StorageProvider.SaveFilePickerAsync(options);
                if (file is null)
                {
                    return;
                }
                _currentFilePath = file.Path.LocalPath;
            }

            RteDocument rte = RteDocumentConverter.FromFlowDocument(Editor.FlowDocument);
            await File.WriteAllTextAsync(_currentFilePath, rte.ToJson());
            _isDirty = false;
            UpdateTitle();
        }

        private void OnCloseEditor(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (!await ConfirmDiscardChangesAsync())
            {
                e.Cancel = true;
                return;
            }
            base.OnClosing(e);
        }

        private async Task<bool> ConfirmDiscardChangesAsync()
        {
            if (!_isDirty)
            {
                return true;
            }

            MsBox.Avalonia.Base.IMsBox<ButtonResult> messageBox = MessageBoxManager.GetMessageBoxStandard(
                "Unsaved changes",
                "You have unsaved changes. Do you want to discard them?",
                ButtonEnum.YesNo,
                MsBox.Avalonia.Enums.Icon.Warning);

            return await messageBox.ShowAsync() == ButtonResult.Yes;
        }

        private async Task<IStorageFolder> GetStartLocationAsync()
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                string folder = Path.GetDirectoryName(_currentFilePath);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    return await StorageProvider.TryGetFolderFromPathAsync(folder);
                }
            }

            if (!string.IsNullOrEmpty(_initialDirectory) && Directory.Exists(_initialDirectory))
            {
                return await StorageProvider.TryGetFolderFromPathAsync(_initialDirectory);
            }

            return null;
        }

        private void UpdateTitle()
        {
            string name = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : Path.GetFileName(_currentFilePath);
            Title = _isDirty ? $"{name}* - RTE Editor" : $"{name} - RTE Editor";
        }

        private void MarkDirty()
        {
            _isDirty = true;
            UpdateTitle();
        }

        private void OnBoldClicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleFontWeight(FontWeight.Bold);
        }

        private void OnItalicClicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleFontStyle(FontStyle.Italic);
        }

        private void OnUnderlineClicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleTextDecoration(TextDecorationLocation.Underline);
        }

        private void OnStrikeClicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleTextDecoration(TextDecorationLocation.Strikethrough);
        }

        private void ToggleFontWeight(FontWeight weight)
        {
            TextRange selection = Editor.FlowDocument.Selection;
            FontWeight current = selection.GetFormatting(AvaloniaTextElement.FontWeightProperty) as FontWeight? ?? FontWeight.Normal;
            FontWeight newValue = current == weight ? FontWeight.Normal : weight;
            selection.ApplyFormatting(AvaloniaTextElement.FontWeightProperty, newValue);
            MarkDirty();
        }

        private void ToggleFontStyle(FontStyle style)
        {
            TextRange selection = Editor.FlowDocument.Selection;
            FontStyle current = selection.GetFormatting(AvaloniaTextElement.FontStyleProperty) as FontStyle? ?? FontStyle.Normal;
            FontStyle newValue = current == style ? FontStyle.Normal : style;
            selection.ApplyFormatting(AvaloniaTextElement.FontStyleProperty, newValue);
            MarkDirty();
        }

        private void ToggleTextDecoration(TextDecorationLocation location)
        {
            TextRange selection = Editor.FlowDocument.Selection;

            // Use Inline.TextDecorationsProperty directly (the correct property for text decorations)
            Avalonia.AvaloniaProperty textDecorationsProp = Avalonia.Controls.Documents.Inline.TextDecorationsProperty;

            try
            {
                var existing = selection.GetFormatting(textDecorationsProp) as TextDecorationCollection;
                var collection = new TextDecorationCollection(existing ?? new TextDecorationCollection());

                bool hasDecoration = collection.Any(dec => dec.Location == location);
                if (hasDecoration)
                {
                    collection = new TextDecorationCollection(collection.Where(dec => dec.Location != location));
                }
                else
                {
                    collection.Add(new TextDecoration { Location = location });
                }

                selection.ApplyFormatting(textDecorationsProp, collection);
                MarkDirty();
            }
            catch
            {
                // Fallback: work directly with inlines if ApplyFormatting fails
                // This handles edge cases where the formatting system might not work as expected
                ApplyTextDecorationDirectly(selection, location);
                MarkDirty();
            }
        }

        private void ApplyTextDecorationDirectly(TextRange selection, TextDecorationLocation location)
        {
            // Get the paragraphs that contain the selection
            Paragraph startPar = selection.GetStartPar();
            Paragraph endPar = selection.GetEndPar();

            if (startPar == null || endPar == null)
            {
                return;
            }

            // Get all paragraphs in the range
            var paragraphs = new List<Paragraph>();
            bool collecting = false;
            foreach (var block in Editor.FlowDocument.Blocks)
            {
                if (block is Paragraph par)
                {
                    if (par == startPar)
                    {
                        collecting = true;
                    }
                    if (collecting)
                    {
                        paragraphs.Add(par);
                        if (par == endPar)
                        {
                            break;
                        }
                    }
                }
            }

            // Process each paragraph's inlines that are within the selection
            foreach (Paragraph par in paragraphs)
            {
                int parStart = par.StartInDoc;
                bool isStartPar = (par == startPar);
                bool isEndPar = (par == endPar);

                foreach (IEditable inline in par.Inlines)
                {
                    if (inline is EditableRun run)
                    {
                        int inlineStart = parStart + inline.TextPositionOfInlineInParagraph;
                        int inlineEnd = inlineStart + inline.InlineLength;

                        // Check if this inline is within the selection range
                        bool isInSelection = false;
                        if (isStartPar && isEndPar)
                        {
                            // Selection is within a single paragraph
                            isInSelection = inlineEnd > selection.Start && inlineStart < selection.End;
                        }
                        else if (isStartPar)
                        {
                            // First paragraph - check if inline extends beyond selection start
                            isInSelection = inlineEnd > selection.Start;
                        }
                        else if (isEndPar)
                        {
                            // Last paragraph - check if inline starts before selection end
                            isInSelection = inlineStart < selection.End;
                        }
                        else
                        {
                            // Middle paragraphs - all inlines are in selection
                            isInSelection = true;
                        }

                        if (isInSelection)
                        {
                            // Get current text decorations
                            TextDecorationCollection currentDecs = run.TextDecorations ?? new TextDecorationCollection();
                            var newDecs = new TextDecorationCollection(currentDecs);

                            // Check if decoration at this location already exists
                            bool hasDecoration = newDecs.Any(dec => dec.Location == location);

                            if (hasDecoration)
                            {
                                // Remove decoration at this location
                                newDecs = new TextDecorationCollection(newDecs.Where(dec => dec.Location != location));
                            }
                            else
                            {
                                // Add decoration at this location
                                newDecs.Add(new TextDecoration { Location = location });
                            }

                            // Apply the new decorations
                            run.TextDecorations = newDecs.Count > 0 ? newDecs : null;
                        }
                    }
                }

                // Request update for the paragraph to reflect changes
                par.CallRequestInlinesUpdate();
            }
        }

        private void OnFontSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            double size;
            var item = FontSizeComboBox.SelectedItem as ComboBoxItem;
            if (item != null && double.TryParse(item.Content?.ToString(), out size))
            {
                Editor.FlowDocument.Selection.ApplyFormatting(AvaloniaTextElement.FontSizeProperty, size);
                MarkDirty();
            }
        }

        private void OnFontFamilyChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = FontFamilyComboBox.SelectedItem as ComboBoxItem;
            string familyName = item?.Content as string;
            if (item != null && familyName != null)
            {
                Editor.FlowDocument.Selection.ApplyFormatting(AvaloniaTextElement.FontFamilyProperty, new FontFamily(familyName));
                MarkDirty();
            }
        }

        private void OnForegroundChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ForegroundComboBox.SelectedItem as ComboBoxItem;
            string name = item?.Content as string;
            if (item != null && name != null)
            {
                var brush = new SolidColorBrush(ColorFromName(name));
                Editor.FlowDocument.Selection.ApplyFormatting(AvaloniaTextElement.ForegroundProperty, brush);
                MarkDirty();
            }
        }

        private void OnBackgroundChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = BackgroundComboBox.SelectedItem as ComboBoxItem;
            string name = item?.Content as string;
            if (item != null && name != null)
            {
                IBrush brush = name.Equals("Transparent", StringComparison.OrdinalIgnoreCase)
                    ? (IBrush)Brushes.Transparent
                    : new SolidColorBrush(ColorFromName(name));
                Editor.FlowDocument.Selection.ApplyFormatting(AvaloniaTextElement.BackgroundProperty, brush);
                MarkDirty();
            }
        }

        private static Color ColorFromName(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "red": return Colors.Red;
                case "green": return Colors.Green;
                case "blue": return Colors.Blue;
                case "gray": return Colors.Gray;
                case "yellow": return Colors.Yellow;
                case "lightblue": return Colors.LightBlue;
                case "lightgreen": return Colors.LightGreen;
                default: return Colors.Black;
            }
        }

        private void OnAlignLeft(object sender, Avalonia.Interactivity.RoutedEventArgs e) { ApplyAlignment(TextAlignment.Left); }

        private void OnAlignCenter(object sender, Avalonia.Interactivity.RoutedEventArgs e) { ApplyAlignment(TextAlignment.Center); }

        private void OnAlignRight(object sender, Avalonia.Interactivity.RoutedEventArgs e) { ApplyAlignment(TextAlignment.Right); }

        private void ApplyAlignment(TextAlignment alignment)
        {
            foreach (Paragraph paragraph in Editor.FlowDocument.GetSelectedParagraphs)
            {
                paragraph.TextAlignment = alignment;
            }
            MarkDirty();
        }

        private void OnSelectionChanged(TextRange range)
        {
            // Update toolbar state to reflect current selection
            if (range is null)
            {
                return;
            }

            var weight = range.GetFormatting(AvaloniaTextElement.FontWeightProperty) as FontWeight?;
            var style = range.GetFormatting(AvaloniaTextElement.FontStyleProperty) as FontStyle?;
            // TextDecorationsProperty may not be available
            TextDecorationCollection decorations = null;
            try
            {
                Type textElementType = typeof(AvaloniaTextElement);
                System.Reflection.PropertyInfo prop = textElementType.GetProperty("TextDecorationsProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    var textDecorationsProp = prop.GetValue(null) as Avalonia.AvaloniaProperty;
                    if (textDecorationsProp != null)
                    {
                        decorations = range.GetFormatting(textDecorationsProp) as TextDecorationCollection;
                    }
                }
            }
            catch
            {
                // Property not available
            }

            // Update toggle buttons appearance if desired in future.
        }

        private void OnEditorKeyUp(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                MarkDirty();
            }
        }
    }
}


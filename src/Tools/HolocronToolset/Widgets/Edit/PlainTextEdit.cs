using BioWare.NET.Common;
using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using BioWare.NET;

namespace HolocronToolset.Widgets.Edit
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/plaintext.py:14
    // Original: class HTPlainTextEdit(QPlainTextEdit):
    public partial class PlainTextEdit : TextBox
    {
        private LocalizedString _locstring;

        // Public parameterless constructor for XAML
        public PlainTextEdit()
        {
            InitializeComponent();
            AcceptsReturn = true;
            AcceptsTab = false;
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
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/plaintext.py:18-20
        // Original: def __init__(self, *args, **kwargs):
        public PlainTextEdit(LocalizedString locstring = null)
        {
            InitializeComponent();
            _locstring = locstring;
            AcceptsReturn = true;
            AcceptsTab = false;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/plaintext.py:22-24
        // Original: def keyReleaseEvent(self, e: QKeyEvent):
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            KeyReleased?.Invoke();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/plaintext.py:26-28
        // Original: def mouseDoubleClickEvent(self, e: QMouseEvent):
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.ClickCount == 2)
            {
                DoubleClicked?.Invoke();
            }
        }

        public LocalizedString Locstring
        {
            get => _locstring;
            set => _locstring = value;
        }

        public event Action KeyReleased;
        public event Action DoubleClicked;
    }
}

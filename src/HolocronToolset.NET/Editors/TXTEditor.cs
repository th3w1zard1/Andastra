using System;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:19
    // Original: class TXTEditor(Editor):
    public class TXTEditor : Editor
    {
        private TextBox _textEdit;
        private bool _wordWrap;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:20-59
        // Original: def __init__(self, parent, installation):
        public TXTEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Text Editor", "none", GetSupportedTypes(), GetSupportedTypes(), installation)
        {
            InitializeComponent();
            Width = 400;
            Height = 250;

            _wordWrap = false;
            New();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;
                _textEdit = this.FindControl<TextBox>("TextEdit");
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }
            
            if (_textEdit == null)
            {
                SetupProgrammaticUI();
            }
        }

        private void SetupProgrammaticUI()
        {
            // Create a simple text box if XAML doesn't exist
            _textEdit = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap
            };
            Content = _textEdit;
        }

        private static ResourceType[] GetSupportedTypes()
        {
            // Get all resource types that are plaintext
            // For now, return common text types
            return new[]
            {
                ResourceType.TXT,
                ResourceType.NSS
            };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:64-72
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            // Decode bytes with fallbacks (UTF-8 -> Windows-1252 -> Latin-1)
            string text = DecodeBytesWithFallbacks(data);
            _textEdit.Text = text;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:74-83
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            string text = _textEdit?.Text ?? string.Empty;

            // Try UTF-8 first, then Windows-1252, then Latin-1
            try
            {
                return Tuple.Create(Encoding.UTF8.GetBytes(text), new byte[0]);
            }
            catch
            {
                try
                {
                    return Tuple.Create(Encoding.GetEncoding("windows-1252").GetBytes(text), new byte[0]);
                }
                catch
                {
                    return Tuple.Create(Encoding.GetEncoding("latin-1").GetBytes(text), new byte[0]);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:85-87
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _textEdit.Text = "";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:89-96
        // Original: def toggle_word_wrap(self):
        public void ToggleWordWrap()
        {
            _wordWrap = !_wordWrap;
            _textEdit.TextWrapping = _wordWrap
                ? Avalonia.Media.TextWrapping.Wrap
                : Avalonia.Media.TextWrapping.NoWrap;
        }

        public override void SaveAs()
        {
            // Will be implemented with file dialogs
            Save();
        }

        private string DecodeBytesWithFallbacks(byte[] data)
        {
            // Try UTF-8 first
            try
            {
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                // Try Windows-1252
                try
                {
                    return Encoding.GetEncoding("windows-1252").GetString(data);
                }
                catch
                {
                    // Fall back to Latin-1
                    return Encoding.GetEncoding("latin-1").GetString(data);
                }
            }
        }
    }
}

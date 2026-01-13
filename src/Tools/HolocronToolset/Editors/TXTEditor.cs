using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
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
                _textEdit = EditorHelpers.FindControlSafe<TextBox>(this, "TextEdit");
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
            // Create a multiline text box if XAML doesn't exist
            // Matching PyKotor implementation: QPlainTextEdit with Courier New font
            _textEdit = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
                FontFamily = "Courier New"
            };
            Content = _textEdit;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:41
        // Original: supported: list[ResourceType] = [member for member in ResourceType if member.contents == "plaintext"]
        private static ResourceType[] GetSupportedTypes()
        {
            // Get all resource types that are plaintext
            // Matching PyKotor: filter ResourceType members where contents == "plaintext"
            // Use reflection to get all static ResourceType fields, similar to FromId/FromExtension
            return typeof(ResourceType).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(ResourceType))
                .Select(f => (ResourceType)f.GetValue(null))
                .Where(rt => rt != null && rt.Contents == "plaintext")
                .ToArray();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:64-72
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:72
            // Original: self.ui.textEdit.setPlainText(decode_bytes_with_fallbacks(data))
            // Decode bytes with fallbacks (UTF-8 -> Windows-1252 -> Latin-1)
            // QPlainTextEdit normalizes \r\n to \n internally, so we normalize here to match
            // This ensures consistent behavior: Load normalizes to \n, Build converts \n to Environment.NewLine
            string text = DecodeBytesWithFallbacks(data);
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            _textEdit.Text = text;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:74-83
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:75
            // Original: text = self.ui.textEdit.toPlainText().replace("\r\n", os.linesep).replace("\n", os.linesep)
            string text = _textEdit?.Text ?? string.Empty;
            
            // Normalize line endings to Environment.NewLine (C# equivalent of os.linesep)
            // TextBox uses \n internally (from Load normalization), so we convert \n to Environment.NewLine
            // This matches Python: QPlainTextEdit uses \n internally, build() converts to os.linesep
            // Process in order: first normalize \r\n to \n, then convert all \n to Environment.NewLine
            // This prevents double conversion
            text = text.Replace("\r\n", "\n");
            text = text.Replace("\r", "\n");
            text = text.Replace("\n", Environment.NewLine);

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:77-83
            // Original: try/except encoding with errors="replace"
            // Try UTF-8 first, then Windows-1252, then Latin-1
            try
            {
                return Tuple.Create(Encoding.UTF8.GetBytes(text), new byte[0]);
            }
            catch (EncoderFallbackException)
            {
                try
                {
                    // Use Encoding.GetEncoding with error handling
                    Encoding win1252 = Encoding.GetEncoding("windows-1252");
                    Encoder encoder = win1252.GetEncoder();
                    byte[] result = new byte[encoder.GetByteCount(text.ToCharArray(), 0, text.Length, true)];
                    encoder.GetBytes(text.ToCharArray(), 0, text.Length, result, 0, true);
                    return Tuple.Create(result, new byte[0]);
                }
                catch
                {
                    // Fall back to Latin-1
                    Encoding latin1 = Encoding.GetEncoding("latin-1");
                    Encoder encoder = latin1.GetEncoder();
                    byte[] result = new byte[encoder.GetByteCount(text.ToCharArray(), 0, text.Length, true)];
                    encoder.GetBytes(text.ToCharArray(), 0, text.Length, result, 0, true);
                    return Tuple.Create(result, new byte[0]);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:85-87
        // Original: def new(self):
        public override void New()
        {
            base.New();
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:87
            // Original: self.ui.textEdit.setPlainText("")
            _textEdit.Text = "";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:89-96
        // Original: def toggle_word_wrap(self):
        public void ToggleWordWrap()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:90
            // Original: self._word_wrap = not self._word_wrap
            _wordWrap = !_wordWrap;
            
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:91-95
            // Original: self.ui.actionWord_Wrap.setChecked(self._word_wrap)
            // Original: self.ui.textEdit.setLineWrapMode(...)
            _textEdit.TextWrapping = _wordWrap
                ? Avalonia.Media.TextWrapping.Wrap
                : Avalonia.Media.TextWrapping.NoWrap;
        }

        public override void SaveAs()
        {
            // Will be implemented with file dialogs
            Save();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/txt.py:72
        // Original: decode_bytes_with_fallbacks(data)
        private string DecodeBytesWithFallbacks(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

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

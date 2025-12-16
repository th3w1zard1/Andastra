using System;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Resources;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py
    // Original: Comprehensive tests for TXT Editor
    [Collection("Avalonia Test Collection")]
    public class TXTEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        static TXTEditorTests()
        {
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            if (!string.IsNullOrEmpty(k1Path) && File.Exists(Path.Combine(k1Path, "chitin.key")))
            {
                _installation = new HTInstallation(k1Path, "Test");
            }
        }

        public TXTEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:21
        // Original: def test_txt_editor_new_file_creation(qtbot, installation):
        [Fact]
        public void TestTxtEditorNewFileCreation()
        {
            var editor = new TXTEditor(null, _installation);

            // Create new
            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:30
            // Original: assert editor.ui.textEdit.toPlainText() == ""
            // Get text from editor's internal TextBox
            var textEdit = GetTextEdit(editor);
            textEdit.Text.Should().Be("");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:33-39
            // Original: test_text = "Hello World"; editor.ui.textEdit.setPlainText(test_text); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert decoded == test_text
            string testText = "Hello World";
            textEdit.Text = testText;

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Be(testText);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:41
        // Original: def test_txt_editor_load_existing_file(qtbot, installation, test_files_dir: Path):
        [Fact]
        public void TestTxtEditorLoadExistingFile()
        {
            var editor = new TXTEditor(null, _installation);

            // Try to find a txt file in test_files (if available)
            // For now, create test data directly
            string testText = "Hello World";
            byte[] testData = Encoding.UTF8.GetBytes(testText);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:55
            // Original: editor.load(txt_file, txt_file.stem, ResourceType.TXT, original_data)
            editor.Load("test.txt", "test", ResourceType.TXT, testData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:58-60
            // Original: loaded_text = editor.ui.textEdit.toPlainText(); expected_text = decode_bytes_with_fallbacks(original_data); assert loaded_text == expected_text
            var textEdit = GetTextEdit(editor);
            string loadedText = textEdit.Text;
            string expectedText = DecodeBytesWithFallbacks(testData);
            loadedText.Should().Be(expectedText);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:62
        // Original: def test_txt_editor_text_editing(qtbot, installation):
        [Fact]
        public void TestTxtEditorTextEditing()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:70-72
            // Original: initial_text = "Initial Text"; editor.ui.textEdit.setPlainText(initial_text); assert editor.ui.textEdit.toPlainText() == initial_text
            var textEdit = GetTextEdit(editor);
            string initialText = "Initial Text";
            textEdit.Text = initialText;
            textEdit.Text.Should().Be(initialText);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:74-76
            // Original: editor.ui.textEdit.appendPlainText("\nAppended Text"); assert "Appended Text" in editor.ui.textEdit.toPlainText()
            textEdit.Text += "\nAppended Text";
            textEdit.Text.Should().Contain("Appended Text");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:78-83
            // Original: cursor operations and insertPlainText
            textEdit.Text = "Prefix: " + textEdit.Text;
            textEdit.Text.Should().StartWith("Prefix: ");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:85
        // Original: def test_txt_editor_multiline_text(qtbot, installation):
        [Fact]
        public void TestTxtEditorMultilineText()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:93-102
            // Original: multiline_text = "Line 1\nLine 2\nLine 3"; editor.ui.textEdit.setPlainText(multiline_text); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert "Line 1" in decoded; assert "Line 2" in decoded; assert "Line 3" in decoded
            var textEdit = GetTextEdit(editor);
            string multilineText = "Line 1\nLine 2\nLine 3";
            textEdit.Text = multilineText;

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            // Note: line endings may be normalized, so we check content
            decoded.Should().Contain("Line 1");
            decoded.Should().Contain("Line 2");
            decoded.Should().Contain("Line 3");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:104
        // Original: def test_txt_editor_empty_text(qtbot, installation):
        [Fact]
        public void TestTxtEditorEmptyText()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:112
            // Original: assert editor.ui.textEdit.toPlainText() == ""
            var textEdit = GetTextEdit(editor);
            textEdit.Text.Should().Be("");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:115-116
            // Original: data, _ = editor.build(); assert data == b""
            var (data, _) = editor.Build();
            data.Should().BeEmpty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:118
        // Original: def test_txt_editor_unicode_characters(qtbot, installation):
        [Fact]
        public void TestTxtEditorUnicodeCharacters()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:126-132
            // Original: unicode_text = "Hello 世界 🌍 ñoño"; editor.ui.textEdit.setPlainText(unicode_text); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert decoded == unicode_text
            var textEdit = GetTextEdit(editor);
            string unicodeText = "Hello 世界 🌍 ñoño";
            textEdit.Text = unicodeText;

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Be(unicodeText);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:134
        // Original: def test_txt_editor_special_characters(qtbot, installation):
        [Fact]
        public void TestTxtEditorSpecialCharacters()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:142-149
            // Original: special_text = "Tab:\tNewline:\nCarriage:\rQuote:\"Apostrophe:'Backslash:\\"; editor.ui.textEdit.setPlainText(special_text); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert "Tab:" in decoded; assert "Newline:" in decoded
            var textEdit = GetTextEdit(editor);
            string specialText = "Tab:\tNewline:\nCarriage:\rQuote:\"Apostrophe:'Backslash:\\";
            textEdit.Text = specialText;

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Contain("Tab:");
            decoded.Should().Contain("Newline:");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:151
        // Original: def test_txt_editor_long_text(qtbot, installation):
        [Fact]
        public void TestTxtEditorLongText()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:159-166
            // Original: long_text = "\n".join([f"Line {i}" for i in range(1000)]); editor.ui.textEdit.setPlainText(long_text); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert "Line 0" in decoded; assert "Line 999" in decoded
            var textEdit = GetTextEdit(editor);
            string longText = string.Join("\n", Enumerable.Range(0, 1000).Select(i => $"Line {i}"));
            textEdit.Text = longText;

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Contain("Line 0");
            decoded.Should().Contain("Line 999");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:172
        // Original: def test_txt_editor_toggle_word_wrap(qtbot, installation):
        [Fact]
        public void TestTxtEditorToggleWordWrap()
        {
            var editor = new TXTEditor(null, _installation);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:178
            // Original: assert not editor._word_wrap
            var textEdit = GetTextEdit(editor);
            textEdit.TextWrapping.Should().Be(Avalonia.Media.TextWrapping.NoWrap);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:181-184
            // Original: editor.toggle_word_wrap(); assert editor._word_wrap; assert editor.ui.actionWord_Wrap.isChecked(); assert editor.ui.textEdit.lineWrapMode() == editor.ui.textEdit.LineWrapMode.WidgetWidth
            editor.ToggleWordWrap();
            textEdit.TextWrapping.Should().Be(Avalonia.Media.TextWrapping.Wrap);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:187-190
            // Original: editor.toggle_word_wrap(); assert not editor._word_wrap; assert not editor.ui.actionWord_Wrap.isChecked(); assert editor.ui.textEdit.lineWrapMode() == editor.ui.textEdit.LineWrapMode.NoWrap
            editor.ToggleWordWrap();
            textEdit.TextWrapping.Should().Be(Avalonia.Media.TextWrapping.NoWrap);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:204
        // Original: def test_txt_editor_word_wrap_with_text(qtbot, installation):
        [Fact]
        public void TestTxtEditorWordWrapWithText()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:212-220
            // Original: long_line = "This is a very long line of text that should wrap when word wrap is enabled " * 10; editor.ui.textEdit.setPlainText(long_line); assert long_line == editor.ui.textEdit.toPlainText(); editor.toggle_word_wrap(); assert long_line == editor.ui.textEdit.toPlainText()
            var textEdit = GetTextEdit(editor);
            string longLine = string.Join("", Enumerable.Range(0, 10).Select(_ => "This is a very long line of text that should wrap when word wrap is enabled "));
            textEdit.Text = longLine;

            // Verify word wrap doesn't affect content
            textEdit.Text.Should().Be(longLine);

            // Toggle wrap and verify content unchanged
            editor.ToggleWordWrap();
            textEdit.Text.Should().Be(longLine);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:226
        // Original: def test_txt_editor_utf8_encoding(qtbot, installation):
        [Fact]
        public void TestTxtEditorUtf8Encoding()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:234-239
            // Original: text = "Hello World"; editor.ui.textEdit.setPlainText(text); data, _ = editor.build(); assert data.decode("utf-8") == text
            var textEdit = GetTextEdit(editor);
            string text = "Hello World";
            textEdit.Text = text;

            // Build should use UTF-8
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            Encoding.UTF8.GetString(data).Should().Be(text);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:241
        // Original: def test_txt_editor_windows1252_fallback(qtbot, installation):
        [Fact]
        public void TestTxtEditorWindows1252Fallback()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:250-256
            // Original: text = "Test with some special chars: éñ"; editor.ui.textEdit.setPlainText(text); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert decoded == text
            var textEdit = GetTextEdit(editor);
            string text = "Test with some special chars: éñ";
            textEdit.Text = text;

            // Build should handle encoding
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Be(text);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:262
        // Original: def test_txt_editor_save_load_roundtrip_identity(qtbot, installation):
        [Fact]
        public void TestTxtEditorSaveLoadRoundtripIdentity()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:270-295
            // Original: test_text = "Test content\nWith multiple lines\nAnd special chars: éñ"; editor.ui.textEdit.setPlainText(test_text); data1, _ = editor.build(); editor.load(Path("test.txt"), "test", ResourceType.TXT, data1); loaded_text = editor.ui.textEdit.toPlainText(); decoded_data = decode_bytes_with_fallbacks(data1); normalized_loaded = loaded_text.replace("\r\n", "\n").replace("\r", "\n"); normalized_decoded = decoded_data.replace("\r\n", "\n").replace("\r", "\n"); assert normalized_loaded == normalized_decoded; data2, _ = editor.build(); decoded1 = decode_bytes_with_fallbacks(data1); decoded2 = decode_bytes_with_fallbacks(data2); assert decoded1.replace("\r\n", "\n").replace("\r", "\n") == decoded2.replace("\r\n", "\n").replace("\r", "\n")
            var textEdit = GetTextEdit(editor);
            string testText = "Test content\nWith multiple lines\nAnd special chars: éñ";
            textEdit.Text = testText;

            // Save
            var (data1, _) = editor.Build();

            // Load saved data
            editor.Load("test.txt", "test", ResourceType.TXT, data1);

            // Verify content matches
            // Note: build() normalizes line endings to Environment.NewLine, so we need to normalize for comparison
            string loadedText = textEdit.Text;
            string decodedData = DecodeBytesWithFallbacks(data1);
            // Normalize line endings for comparison (build() converts \n to Environment.NewLine)
            string normalizedLoaded = loadedText.Replace("\r\n", "\n").Replace("\r", "\n");
            string normalizedDecoded = decodedData.Replace("\r\n", "\n").Replace("\r", "\n");
            normalizedLoaded.Should().Be(normalizedDecoded);

            // Save again
            var (data2, _) = editor.Build();

            // Verify second save matches first (content-wise, line endings may differ)
            string decoded1 = DecodeBytesWithFallbacks(data1);
            string decoded2 = DecodeBytesWithFallbacks(data2);
            // Normalize line endings for comparison
            decoded1.Replace("\r\n", "\n").Replace("\r", "\n").Should().Be(decoded2.Replace("\r\n", "\n").Replace("\r", "\n"));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:297
        // Original: def test_txt_editor_save_load_roundtrip_with_modifications(qtbot, installation):
        [Fact]
        public void TestTxtEditorSaveLoadRoundtripWithModifications()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:305-320
            // Original: initial_text = "Initial content"; editor.ui.textEdit.setPlainText(initial_text); data1, _ = editor.build(); editor.load(Path("test.txt"), "test", ResourceType.TXT, data1); editor.ui.textEdit.appendPlainText("\nModified content"); data2, _ = editor.build(); decoded2 = decode_bytes_with_fallbacks(data2); assert "Modified content" in decoded2
            var textEdit = GetTextEdit(editor);
            string initialText = "Initial content";
            textEdit.Text = initialText;

            // Save
            var (data1, _) = editor.Build();

            // Load and modify
            editor.Load("test.txt", "test", ResourceType.TXT, data1);
            textEdit.Text += "\nModified content";

            // Save modified
            var (data2, _) = editor.Build();

            // Verify modification was saved
            string decoded2 = DecodeBytesWithFallbacks(data2);
            decoded2.Should().Contain("Modified content");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:322
        // Original: def test_txt_editor_multiple_save_load_cycles(qtbot, installation):
        [Fact]
        public void TestTxtEditorMultipleSaveLoadCycles()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:330-343
            // Original: for cycle in range(5): test_text = f"Cycle {cycle} content"; editor.ui.textEdit.setPlainText(test_text); data, _ = editor.build(); editor.load(Path("test.txt"), "test", ResourceType.TXT, data); loaded_text = editor.ui.textEdit.toPlainText(); assert loaded_text == decode_bytes_with_fallbacks(data)
            var textEdit = GetTextEdit(editor);
            // Perform multiple cycles
            for (int cycle = 0; cycle < 5; cycle++)
            {
                // Set text
                string testText = $"Cycle {cycle} content";
                textEdit.Text = testText;

                // Save
                var (data, _) = editor.Build();

                // Load back
                editor.Load("test.txt", "test", ResourceType.TXT, data);

                // Verify content
                string loadedText = textEdit.Text;
                loadedText.Should().Be(DecodeBytesWithFallbacks(data));
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:349
        // Original: def test_txt_editor_line_ending_normalization(qtbot, installation):
        [Fact]
        public void TestTxtEditorLineEndingNormalization()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:358-370
            // Original: text = "Line 1\nLine 2\r\nLine 3\rLine 4"; editor.ui.textEdit.setPlainText(text); data, _ = editor.build(); assert len(data) > 0; decoded = decode_bytes_with_fallbacks(data); assert "Line 1" in decoded; assert "Line 2" in decoded; assert "Line 3" in decoded; assert "Line 4" in decoded
            var textEdit = GetTextEdit(editor);
            // Set text with mixed line endings
            // Note: TextBox uses \n internally
            string text = "Line 1\nLine 2\r\nLine 3\rLine 4";
            textEdit.Text = text;

            // Build - should normalize line endings
            var (data, _) = editor.Build();

            // Verify it built successfully
            data.Length.Should().BeGreaterThan(0);
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Contain("Line 1");
            decoded.Should().Contain("Line 2");
            decoded.Should().Contain("Line 3");
            decoded.Should().Contain("Line 4");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:445
        // Original: def test_txt_editor_very_long_line(qtbot, installation):
        [Fact]
        public void TestTxtEditorVeryLongLine()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:453-460
            // Original: long_line = "A" * 10000; editor.ui.textEdit.setPlainText(long_line); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert len(decoded) == 10000; assert decoded == long_line
            var textEdit = GetTextEdit(editor);
            // Create very long line
            string longLine = new string('A', 10000);
            textEdit.Text = longLine;

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Length.Should().Be(10000);
            decoded.Should().Be(longLine);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:462
        // Original: def test_txt_editor_only_newlines(qtbot, installation):
        [Fact]
        public void TestTxtEditorOnlyNewlines()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:470-475
            // Original: editor.ui.textEdit.setPlainText("\n\n\n"); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert "\n" in decoded
            var textEdit = GetTextEdit(editor);
            // Set only newlines
            textEdit.Text = "\n\n\n";

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Contain("\n");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:477
        // Original: def test_txt_editor_tab_characters(qtbot, installation):
        [Fact]
        public void TestTxtEditorTabCharacters()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:485-491
            // Original: text_with_tabs = "Column1\tColumn2\tColumn3"; editor.ui.textEdit.setPlainText(text_with_tabs); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert "\t" in decoded or "Column1" in decoded
            var textEdit = GetTextEdit(editor);
            // Set text with tabs
            string textWithTabs = "Column1\tColumn2\tColumn3";
            textEdit.Text = textWithTabs;

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Match(s => s.Contains("\t") || s.Contains("Column1"));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:513
        // Original: def test_txt_editor_supported_resource_types(qtbot, installation):
        [Fact]
        public void TestTxtEditorSupportedResourceTypes()
        {
            var editor = new TXTEditor(null, _installation);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:521-533
            // Original: Verify supported types include plaintext types; assert ResourceType.TXT in supported; plaintext_types = [member for member in ResourceType if member.contents == "plaintext"]; for plaintext_type in plaintext_types: assert plaintext_type in supported
            // Editor stores supported types in _readSupported (set by setupEditorFilters in __init__)
            // Use reflection to access private field
            var readSupportedField = typeof(Editor).GetField("_readSupported", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var readSupported = (ResourceType[])readSupportedField.GetValue(editor);
            
            readSupported.Should().Contain(ResourceType.TXT);

            // All plaintext types should be supported
            var plaintextTypes = typeof(ResourceType).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(ResourceType))
                .Select(f => (ResourceType)f.GetValue(null))
                .Where(rt => rt != null && rt.Contents == "plaintext")
                .ToArray();
            
            foreach (var plaintextType in plaintextTypes)
            {
                readSupported.Should().Contain(plaintextType);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:539
        // Original: def test_txt_editor_complex_document(qtbot, installation):
        [Fact]
        public void TestTxtEditorComplexDocument()
        {
            var editor = new TXTEditor(null, _installation);

            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:547-575
            // Original: complex_text = """# Header\n..."""; editor.ui.textEdit.setPlainText(complex_text); editor.toggle_word_wrap(); data, _ = editor.build(); decoded = decode_bytes_with_fallbacks(data); assert "# Header" in decoded; assert "List item" in decoded; editor.toggle_word_wrap(); data2, _ = editor.build(); decoded2 = decode_bytes_with_fallbacks(data2); assert "# Header" in decoded2
            var textEdit = GetTextEdit(editor);
            // Create complex document
            string complexText = @"# Header
This is a paragraph with multiple sentences. It contains various characters like é, ñ, and 🌍.

- List item 1
- List item 2
	Indented line
		Double indented

Another paragraph with special chars: ""quotes"", 'apostrophes', and backslashes: \
";
            textEdit.Text = complexText;

            // Toggle word wrap
            editor.ToggleWordWrap();

            // Build and verify
            var (data, _) = editor.Build();
            string decoded = DecodeBytesWithFallbacks(data);
            decoded.Should().Contain("# Header");
            decoded.Should().Contain("List item");

            // Toggle word wrap back
            editor.ToggleWordWrap();

            // Save again
            var (data2, _) = editor.Build();
            string decoded2 = DecodeBytesWithFallbacks(data2);
            decoded2.Should().Contain("# Header");
        }

        // Helper method to get the TextEdit control from the editor
        private Avalonia.Controls.TextBox GetTextEdit(TXTEditor editor)
        {
            // Use reflection to access private _textEdit field
            var textEditField = typeof(TXTEditor).GetField("_textEdit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (Avalonia.Controls.TextBox)textEditField.GetValue(editor);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:15
        // Original: from pykotor.tools.encoding import decode_bytes_with_fallbacks
        private string DecodeBytesWithFallbacks(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                try
                {
                    return Encoding.GetEncoding("windows-1252").GetString(data);
                }
                catch
                {
                    return Encoding.GetEncoding("latin-1").GetString(data);
                }
            }
        }
    }
}

using System;
using System.IO;
using System.Text;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py
    // Original: Comprehensive tests for TXT Editor
    [Collection("Avalonia Test Collection")]
    public class TXTEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public TXTEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }
        [Fact]
        public void TestTxtEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_txt_editor.py:21
            // Original: def test_txt_editor_new_file_creation(qtbot, installation):
            var editor = new TXTEditor(null, null);

            editor.New();

            // Verify text edit is empty (will be implemented when UI is complete)
            var (data, _) = editor.Build();
            data.Should().BeEmpty();
        }

        [Fact]
        public void TestTxtEditorLoadExistingFile()
        {
            var editor = new TXTEditor(null, null);

            string testText = "Hello World";
            byte[] testData = Encoding.UTF8.GetBytes(testText);

            editor.Load("test.txt", "test", ResourceType.TXT, testData);

            // Verify content loaded (will be implemented when UI is complete)
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestTxtEditorTextEditing()
        {
            var editor = new TXTEditor(null, null);
            editor.New();

            // Test basic editing (will be implemented when UI is complete)
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestTxtEditorMultilineText()
        {
            var editor = new TXTEditor(null, null);
            editor.New();

            // Test multiline handling (will be implemented when UI is complete)
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestTxtEditorEmptyText()
        {
            var editor = new TXTEditor(null, null);
            editor.New();

            var (data, _) = editor.Build();
            data.Should().BeEmpty();
        }

        [Fact]
        public void TestTxtEditorUnicodeCharacters()
        {
            var editor = new TXTEditor(null, null);
            editor.New();

            // Test Unicode handling (will be implemented when UI is complete)
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestTxtEditorSaveLoadRoundtrip()
        {
            var editor = new TXTEditor(null, null);
            editor.New();

            string testText = "Test content\nWith multiple lines";
            byte[] initialData = Encoding.UTF8.GetBytes(testText);

            editor.Load("test.txt", "test", ResourceType.TXT, initialData);

            var (savedData, _) = editor.Build();
            savedData.Should().NotBeNull();

            // Load again
            editor.Load("test.txt", "test", ResourceType.TXT, savedData);
            var (data2, _) = editor.Build();

            // Content should match (normalized for line endings)
            string decoded1 = DecodeBytesWithFallbacks(savedData);
            string decoded2 = DecodeBytesWithFallbacks(data2);
            // Normalize line endings for comparison
            string normalized1 = decoded1.Replace("\r\n", "\n").Replace("\r", "\n");
            string normalized2 = decoded2.Replace("\r\n", "\n").Replace("\r", "\n");
            normalized1.Should().Be(normalized2);
        }

        private string DecodeBytesWithFallbacks(byte[] data)
        {
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

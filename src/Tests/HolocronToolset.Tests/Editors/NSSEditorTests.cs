using System;
using System.Text;
using Andastra.Parsing.Resources;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py
    // Original: Comprehensive tests for NSS Editor
    [Collection("Avalonia Test Collection")]
    public class NSSEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public NSSEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestNssEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py
            // Original: def test_nss_editor_new_file_creation(qtbot, installation):
            var editor = new NSSEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestNssEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py:75-96
            // Original: def test_nss_editor_document_layout(qtbot, installation):
            var editor = new NSSEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py:186-203
        // Original: def test_nss_editor_load_nss_file(qtbot, installation: HTInstallation, tmp_path: Path):
        [Fact]
        public void TestNssEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find an NSS file
            string nssFile = null;
            string[] commonNssFiles = { "test.nss", "script.nss", "main.nss" };
            foreach (string nssName in commonNssFiles)
            {
                string testNssPath = System.IO.Path.Combine(testFilesDir, nssName);
                if (System.IO.File.Exists(testNssPath))
                {
                    nssFile = testNssPath;
                    break;
                }
            }

            // Try alternative location
            if (nssFile == null)
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

                foreach (string nssName in commonNssFiles)
                {
                    string testNssPath = System.IO.Path.Combine(testFilesDir, nssName);
                    if (System.IO.File.Exists(testNssPath))
                    {
                        nssFile = testNssPath;
                        break;
                    }
                }
            }

            // If no NSS file found, create a temporary one for testing
            if (nssFile == null)
            {
                string tempDir = System.IO.Path.GetTempPath();
                nssFile = System.IO.Path.Combine(tempDir, "test_nss_editor.nss");
                string scriptContent = "void main() { int x = 5; }";
                System.IO.File.WriteAllText(nssFile, scriptContent, Encoding.UTF8);
            }

            if (!System.IO.File.Exists(nssFile))
            {
                // Skip if no NSS files available for testing (matching Python pytest.skip behavior)
                return;
            }

            // Get installation if available (K2 preferred for NSS files)
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }
            else
            {
                // Fallback to K1
                string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
                if (string.IsNullOrEmpty(k1Path))
                {
                    k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
                }

                if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
                {
                    installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
                }
            }

            var editor = new NSSEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(nssFile);
            string resref = System.IO.Path.GetFileNameWithoutExtension(nssFile);
            editor.Load(nssFile, resref, ResourceType.NSS, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify the script content is in the data
            string dataText = Encoding.UTF8.GetString(data);
            dataText.Should().Contain("void main");
        }
    }
}

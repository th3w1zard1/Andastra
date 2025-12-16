using System;
using Andastra.Parsing.Resources;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_mdl_editor.py
    // Original: Comprehensive tests for MDL Editor
    [Collection("Avalonia Test Collection")]
    public class MDLEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public MDLEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestMdlEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_mdl_editor.py:18-28
            // Original: def test_mdl_editor_new_file_creation(qtbot, installation):
            var editor = new MDLEditor(null, null);

            editor.New();

            // Verify MDL object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestMdlEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_mdl_editor.py:30-39
            // Original: def test_mdl_editor_initialization(qtbot, installation):
            var editor = new MDLEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_mdl_editor.py:72-84
        // Original: def test_mdl_editor_load_requires_mdx(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMdlEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find an MDL file (MDL files require both .mdl and .mdx files)
            string mdlFile = null;
            string mdxFile = null;

            // Look for common MDL files
            string[] commonMdlFiles = { "p_robe_01.mdl", "p_robe_02.mdl", "p_jedi_robe.mdl" };
            foreach (string mdlName in commonMdlFiles)
            {
                string testMdlPath = System.IO.Path.Combine(testFilesDir, mdlName);
                string testMdxPath = System.IO.Path.ChangeExtension(testMdlPath, ".mdx");

                if (System.IO.File.Exists(testMdlPath) && System.IO.File.Exists(testMdxPath))
                {
                    mdlFile = testMdlPath;
                    mdxFile = testMdxPath;
                    break;
                }
            }

            // Try alternative location
            if (mdlFile == null)
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

                foreach (string mdlName in commonMdlFiles)
                {
                    string testMdlPath = System.IO.Path.Combine(testFilesDir, mdlName);
                    string testMdxPath = System.IO.Path.ChangeExtension(testMdlPath, ".mdx");

                    if (System.IO.File.Exists(testMdlPath) && System.IO.File.Exists(testMdxPath))
                    {
                        mdlFile = testMdlPath;
                        mdxFile = testMdxPath;
                        break;
                    }
                }
            }

            if (mdlFile == null || !System.IO.File.Exists(mdlFile) || !System.IO.File.Exists(mdxFile))
            {
                // Skip if no MDL/MDX files available for testing (matching Python pytest.skip behavior)
                return;
            }

            // Get installation if available (K2 preferred for MDL files)
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

            var editor = new MDLEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(mdlFile);
            string resref = System.IO.Path.GetFileNameWithoutExtension(mdlFile);
            editor.Load(mdlFile, resref, ResourceType.MDL, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, dataExt) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
            dataExt.Should().NotBeNull();
        }
    }
}

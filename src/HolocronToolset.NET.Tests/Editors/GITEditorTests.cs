using System;
using System.Collections.Generic;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_git_editor.py
    // Original: Comprehensive tests for GIT Editor
    [Collection("Avalonia Test Collection")]
    public class GITEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public GITEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestGitEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_git_editor.py
            // Original: def test_git_editor_new_file_creation(qtbot, installation):
            var editor = new GITEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestGitEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_git_editor.py
            // Original: def test_git_editor_initialization(qtbot, installation):
            var editor = new GITEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_git_editor.py:151-190
        // Original: def test_git_editor_headless_ui_load_build(qtbot, installation: HTInstallation, test_files_dir: pathlib.Path):
        [Fact]
        public void TestGitEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a GIT file
            string gitFile = System.IO.Path.Combine(testFilesDir, "zio001.git");
            if (!System.IO.File.Exists(gitFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                gitFile = System.IO.Path.Combine(testFilesDir, "zio001.git");
            }

            byte[] originalData = null;
            string resname = "zio001";

            if (System.IO.File.Exists(gitFile))
            {
                originalData = System.IO.File.ReadAllBytes(gitFile);
            }
            else
            {
                // Try to get one from installation
                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (string.IsNullOrEmpty(k2Path))
                {
                    k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
                }

                HTInstallation installation = null;
                if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
                {
                    installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
                    var result = installation.Resource("zio001", ResourceType.GIT);
                    if (result != null && result.Data != null && result.Data.Length > 0)
                    {
                        originalData = result.Data;
                    }
                }

                if (originalData == null || originalData.Length == 0)
                {
                    // Skip if no GIT files available for testing (matching Python pytest.skip behavior)
                    return;
                }

                var editor = new GITEditor(null, installation);
                editor.Load(gitFile ?? "zio001.git", resname, ResourceType.GIT, originalData);

                // Verify editor loaded the data
                editor.Should().NotBeNull();

                // Build and verify it works
                var (data, _) = editor.Build();
                data.Should().NotBeNull();
                data.Length.Should().BeGreaterThan(0);

                // Verify we can read it back
                GFF gff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);
                gff.Should().NotBeNull();
                return;
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation2 = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation2 = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor2 = new GITEditor(null, installation2);
            editor2.Load(gitFile, resname, ResourceType.GIT, originalData);

            // Verify editor loaded the data
            editor2.Should().NotBeNull();

            // Build and verify it works
            var (data2, _) = editor2.Build();
            data2.Should().NotBeNull();
            data2.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            GFF gff2 = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data2);
            gff2.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_git_editor.py:90-101
        // Original: def test_save_and_load(self):
        [Fact]
        public void TestGitEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string gitFile = System.IO.Path.Combine(testFilesDir, "zio001.git");
            if (!System.IO.File.Exists(gitFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                gitFile = System.IO.Path.Combine(testFilesDir, "zio001.git");
            }

            if (!System.IO.File.Exists(gitFile))
            {
                // Skip if test file not available (matching Python pytest.skip behavior)
                return;
            }

            // Get installation if available (K2 preferred for GIT files)
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

            var editor = new GITEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            byte[] data = System.IO.File.ReadAllBytes(gitFile);
            var old = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);
            editor.Load(gitFile, "zio001", ResourceType.GIT, data);
            var (newData, _) = editor.Build();
            GFF newGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(newData);

            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = old.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: false);
            diff.Should().BeTrue($"GFF comparison failed. Log messages: {string.Join(Environment.NewLine, logMessages)}");
        }
    }
}

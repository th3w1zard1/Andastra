using System;
using System.Collections.Generic;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py
    // Original: Comprehensive tests for UTI Editor
    [Collection("Avalonia Test Collection")]
    public class UTIEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public UTIEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static UTIEditorTests()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            if (!string.IsNullOrEmpty(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                _installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }
            else
            {
                // Fallback to K1
                string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
                if (string.IsNullOrEmpty(k1Path))
                {
                    k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
                }

                if (!string.IsNullOrEmpty(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
                {
                    _installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:147-180
        // Original: def test_uti_editor_all_widgets_exist(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtiEditorAllWidgetsExist()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();

            // Basic tab widgets
            // Matching Python: assert hasattr(editor.ui, 'nameEdit')
            editor.NameEdit.Should().NotBeNull();
            editor.DescEdit.Should().NotBeNull();
            editor.TagEdit.Should().NotBeNull();
            editor.ResrefEdit.Should().NotBeNull();
            editor.BaseSelect.Should().NotBeNull();
            editor.CostSpin.Should().NotBeNull();
            editor.AdditionalCostSpin.Should().NotBeNull();
            editor.UpgradeSpin.Should().NotBeNull();
            editor.PlotCheckbox.Should().NotBeNull();
            editor.ChargesSpin.Should().NotBeNull();
            editor.StackSpin.Should().NotBeNull();
            editor.ModelVarSpin.Should().NotBeNull();
            editor.BodyVarSpin.Should().NotBeNull();
            editor.TextureVarSpin.Should().NotBeNull();
            editor.TagGenerateBtn.Should().NotBeNull();
            editor.ResrefGenerateBtn.Should().NotBeNull();

            // Properties tab widgets
            // Matching Python: assert hasattr(editor.ui, 'availablePropertyList')
            editor.AvailablePropertyList.Should().NotBeNull();
            editor.AssignedPropertiesList.Should().NotBeNull();
            editor.AddPropertyBtn.Should().NotBeNull();
            editor.RemovePropertyBtn.Should().NotBeNull();
            editor.EditPropertyBtn.Should().NotBeNull();

            // Comments tab widgets
            // Matching Python: assert hasattr(editor.ui, 'commentsEdit')
            editor.CommentsEdit.Should().NotBeNull();

            editor.Close();
        }

        [Fact]
        public void TestUtiEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py
            // Original: def test_uti_editor_new_file_creation(qtbot, installation):
            var editor = new UTIEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:400-427
        // Original: def test_uti_editor_load_real_file(qtbot, installation: HTInstallation, test_files_dir):
        [Fact]
        public void TestUtiEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a UTI file
            string utiFile = System.IO.Path.Combine(testFilesDir, "baragwin.uti");
            if (!System.IO.File.Exists(utiFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utiFile = System.IO.Path.Combine(testFilesDir, "baragwin.uti");
            }

            if (!System.IO.File.Exists(utiFile))
            {
                // Skip if no UTI files available for testing (matching Python pytest.skip behavior)
                return;
            }

            // Get installation if available (K2 preferred for UTI files)
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

            var editor = new UTIEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(utiFile);
            editor.Load(utiFile, "baragwin", ResourceType.UTI, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            var gff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:81-92
        // Original: def test_save_and_load(self):
        [Fact]
        public void TestUtiEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find baragwin.uti
            string utiFile = System.IO.Path.Combine(testFilesDir, "baragwin.uti");
            if (!System.IO.File.Exists(utiFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utiFile = System.IO.Path.Combine(testFilesDir, "baragwin.uti");
            }

            if (!System.IO.File.Exists(utiFile))
            {
                // Skip if test file not available
                return;
            }

            // Get installation if available (K2 preferred for UTI files)
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

            if (installation == null)
            {
                // Skip if no installation available
                return;
            }

            var editor = new UTIEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:84
            // Original: data = filepath.read_bytes()
            byte[] data = System.IO.File.ReadAllBytes(utiFile);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:85
            // Original: old = read_gff(data)
            var old = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:86
            // Original: self.editor.load(filepath, "baragwin", ResourceType.UTI, data)
            editor.Load(utiFile, "baragwin", ResourceType.UTI, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:88
            // Original: data, _ = self.editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:89
            // Original: new = read_gff(data)
            var newGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(newData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:91
            // Original: diff = old.compare(new, self.log_func, ignore_default_changes=True)
            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = old.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:92
            // Original: assert diff, os.linesep.join(self.log_messages)
            diff.Should().BeTrue($"GFF comparison failed. Log messages: {string.Join(Environment.NewLine, logMessages)}");
        }
    }
}

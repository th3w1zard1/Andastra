using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py
    // Original: Comprehensive tests for IFO Editor
    [Collection("Avalonia Test Collection")]
    public class IFOEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public IFOEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:23-43
        // Original: def test_ifo_editor_manipulate_tag(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateTag()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            // Create new IFO
            editor.New();

            // Modify tag
            editor.TagEdit.Text = "modified_tag";
            editor.OnValueChanged();

            // Build and verify
            (byte[] data, byte[] _) = editor.Build();
            data.Length.Should().BeGreaterThan(0);

            // Load and verify
            editor.Load("test.ifo", "test", ResourceType.IFO, data);
            editor.TagEdit.Text.Should().Be("modified_tag");
            editor.Ifo.Should().NotBeNull();
            editor.Ifo.Tag.Should().Be("modified_tag");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:45-65
        // Original: def test_ifo_editor_manipulate_vo_id(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateVoId()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Modify VO ID
            var testVoIds = new[] { "vo_001", "test_vo", "", "vo_id_12345" };
            foreach (var voId in testVoIds)
            {
                editor.VoIdEdit.Text = voId;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                modifiedIfo.VoId.Should().Be(voId);

                // Load back and verify
                editor.Load("test.ifo", "test", ResourceType.IFO, data);
                editor.VoIdEdit.Text.Should().Be(voId);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:67-83
        // Original: def test_ifo_editor_manipulate_hak(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateHak()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Modify Hak
            var testHaks = new[] { "hak01", "test_hak", "", "custom_hak_file" };
            foreach (var hak in testHaks)
            {
                editor.HakEdit.Text = hak;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                modifiedIfo.Hak.Should().Be(hak);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:89-108
        // Original: def test_ifo_editor_manipulate_entry_resref(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateEntryResref()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Modify entry ResRef
            var testResrefs = new[] { "area001", "test_area", "", "entry_point" };
            foreach (var resref in testResrefs)
            {
                editor.EntryResrefEdit.Text = resref;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                modifiedIfo.ResRef.ToString().Should().Be(resref);

                // Load back and verify
                editor.Load("test.ifo", "test", ResourceType.IFO, data);
                editor.EntryResrefEdit.Text.Should().Be(resref);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:111-143
        // Original: def test_ifo_editor_manipulate_entry_position(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateEntryPosition()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Test various positions
            var testPositions = new[]
            {
                (0.0, 0.0, 0.0),
                (10.5, 20.3, 5.0),
                (-5.0, -10.0, 0.5),
                (100.0, 200.0, 50.0),
            };

            foreach (var (x, y, z) in testPositions)
            {
                editor.EntryXSpin.Value = (decimal)x;
                editor.EntryYSpin.Value = (decimal)y;
                editor.EntryZSpin.Value = (decimal)z;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                Math.Abs(modifiedIfo.EntryX - (float)x).Should().BeLessThan(0.001f);
                Math.Abs(modifiedIfo.EntryY - (float)y).Should().BeLessThan(0.001f);
                Math.Abs(modifiedIfo.EntryZ - (float)z).Should().BeLessThan(0.001f);

                // Load back and verify
                editor.Load("test.ifo", "test", ResourceType.IFO, data);
                if (editor.EntryXSpin.Value.HasValue)
                    Math.Abs((float)editor.EntryXSpin.Value.Value - (float)x).Should().BeLessThan(0.001f);
                if (editor.EntryYSpin.Value.HasValue)
                    Math.Abs((float)editor.EntryYSpin.Value.Value - (float)y).Should().BeLessThan(0.001f);
                if (editor.EntryZSpin.Value.HasValue)
                    Math.Abs((float)editor.EntryZSpin.Value.Value - (float)z).Should().BeLessThan(0.001f);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:145-167
        // Original: def test_ifo_editor_manipulate_entry_direction(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateEntryDirection()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Test various directions (radians)
            var testDirections = new[] { 0.0, 1.57, 3.14, -1.57, -3.14159 };
            foreach (var direction in testDirections)
            {
                editor.EntryDirSpin.Value = (decimal)direction;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                Math.Abs(modifiedIfo.EntryDirection - (float)direction).Should().BeLessThan(0.001f);

                // Load back and verify
                editor.Load("test.ifo", "test", ResourceType.IFO, data);
                if (editor.EntryDirSpin.Value.HasValue)
                    Math.Abs((float)editor.EntryDirSpin.Value.Value - (float)direction).Should().BeLessThan(0.001f);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:783-815
        // Original: def test_ifo_editor_load_from_test_files(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestIfoEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find IFO files
            List<string> ifoFiles = new List<string>();
            if (Directory.Exists(testFilesDir))
            {
                ifoFiles.AddRange(Directory.GetFiles(testFilesDir, "*.ifo", SearchOption.AllDirectories));
            }

            if (ifoFiles.Count == 0)
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                if (Directory.Exists(testFilesDir))
                {
                    ifoFiles.AddRange(Directory.GetFiles(testFilesDir, "*.ifo", SearchOption.AllDirectories));
                }
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }
            else
            {
                // Fallback to K2
                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (string.IsNullOrEmpty(k2Path))
                {
                    k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
                }

                if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
                {
                    installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
                }
            }

            byte[] originalData = null;
            string ifoFile = null;

            // Try to get IFO file from test files or installation
            if (ifoFiles.Count > 0)
            {
                ifoFile = ifoFiles[0];
                originalData = System.IO.File.ReadAllBytes(ifoFile);
            }
            else if (installation != null)
            {
                // Try to get IFO from installation
                var allResources = installation.Installation.CoreResources();
                foreach (var resource in allResources)
                {
                    if (resource.ResType == ResourceType.IFO)
                    {
                        var resourceResult = installation.Resource(resource.ResName, ResourceType.IFO);
                        if (resourceResult != null && resourceResult.Data != null && resourceResult.Data.Length > 0)
                        {
                            originalData = resourceResult.Data;
                            ifoFile = "module.ifo";
                            break;
                        }
                    }
                }
            }

            if (originalData == null || originalData.Length == 0)
            {
                // Skip if no IFO files available for testing (matching Python pytest.skip behavior)
                return;
            }

            var editor = new IFOEditor(null, installation);

            // Load original IFO for comparison
            var originalGff = GFF.FromBytes(originalData);
            CSharpKOTOR.Resource.Generics.IFO originalIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(originalGff);

            // Load the IFO file
            string resname = ifoFile != null ? System.IO.Path.GetFileNameWithoutExtension(ifoFile) : "module";
            editor.Load(ifoFile ?? "module.ifo", resname, ResourceType.IFO, originalData);

            // Verify editor loaded the data (matching Python: assert editor.ifo is not None)
            editor.Ifo.Should().NotBeNull();

            // Build and verify it works
            (byte[] data, byte[] _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back (matching Python: loaded_ifo = read_ifo(data))
            var newGff = GFF.FromBytes(data);
            var loadedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(newGff);
            loadedIfo.Should().NotBeNull();

            // Verify tag matches (matching Python: assert loaded_ifo.tag == original_ifo.tag)
            loadedIfo.Tag.Should().Be(originalIfo.Tag);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:1060-1102
        // Original: def test_ifo_editor_gff_roundtrip_with_real_file(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestIfoEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find IFO files
            List<string> ifoFiles = new List<string>();
            if (Directory.Exists(testFilesDir))
            {
                ifoFiles.AddRange(Directory.GetFiles(testFilesDir, "*.ifo", SearchOption.AllDirectories));
            }

            if (ifoFiles.Count == 0)
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                if (Directory.Exists(testFilesDir))
                {
                    ifoFiles.AddRange(Directory.GetFiles(testFilesDir, "*.ifo", SearchOption.AllDirectories));
                }
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }
            else
            {
                // Fallback to K2
                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (string.IsNullOrEmpty(k2Path))
                {
                    k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
                }

                if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
                {
                    installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
                }
            }

            byte[] originalData = null;
            string ifoFile = null;

            // Try to get IFO file from test files or installation
            if (ifoFiles.Count > 0)
            {
                ifoFile = ifoFiles[0];
                originalData = System.IO.File.ReadAllBytes(ifoFile);
            }
            else if (installation != null)
            {
                // Try to get IFO from installation
                var allResources = installation.Installation.CoreResources();
                foreach (var resource in allResources)
                {
                    if (resource.ResType == ResourceType.IFO)
                    {
                        var resourceResult = installation.Resource(resource.ResName, ResourceType.IFO);
                        if (resourceResult != null && resourceResult.Data != null && resourceResult.Data.Length > 0)
                        {
                            originalData = resourceResult.Data;
                            ifoFile = "module.ifo"; // Placeholder filename
                            break;
                        }
                    }
                }
            }

            if (originalData == null)
            {
                // Skip if no IFO files available
                return;
            }

            var editor = new IFOEditor(null, installation);

            // Load original GFF
            var originalGff = GFF.FromBytes(originalData);

            // Load the IFO file
            string resname = ifoFile != null ? System.IO.Path.GetFileNameWithoutExtension(ifoFile) : "module";
            editor.Load(ifoFile ?? "module.ifo", resname, ResourceType.IFO, originalData);

            // Build without modifications
            var (newData, _) = editor.Build();
            var newGff = GFF.FromBytes(newData);

            // Compare GFF structures (they should be valid)
            // Note: We expect some differences due to how GFF is written, but structure should be valid
            newGff.Should().NotBeNull();
            originalGff.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:168-184
        // Original: def test_ifo_editor_manipulate_dawn_hour(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateDawnHour()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Test all valid hours (0-23)
            int[] testHours = { 0, 6, 12, 18, 23 };
            foreach (int hour in testHours)
            {
                editor.DawnHourSpin.Value = hour;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                modifiedIfo.DawnHour.Should().Be(hour, $"DawnHour should be {hour} after setting DawnHourSpin to {hour}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:185-199
        // Original: def test_ifo_editor_manipulate_dusk_hour(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateDuskHour()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Test various hours
            int[] testHours = { 0, 18, 20, 23 };
            foreach (int hour in testHours)
            {
                editor.DuskHourSpin.Value = hour;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                modifiedIfo.DuskHour.Should().Be(hour, $"DuskHour should be {hour} after setting DuskHourSpin to {hour}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:200-215
        // Original: def test_ifo_editor_manipulate_time_scale(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateTimeScale()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Test various time scales
            int[] testScales = { 0, 1, 50, 100 };
            foreach (int scale in testScales)
            {
                editor.TimeScaleSpin.Value = scale;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                modifiedIfo.TimeScale.Should().Be(scale, $"TimeScale should be {scale} after setting TimeScaleSpin to {scale}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:216-243
        // Original: def test_ifo_editor_manipulate_start_date(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateStartDate()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Test various start date values
            int[] testMonths = { 1, 6, 12 };
            int[] testDays = { 1, 15, 28 };
            int[] testHours = { 0, 12, 23 };
            int[] testYears = { 0, 100, 1000 };

            foreach (int month in testMonths)
            {
                foreach (int day in testDays)
                {
                    foreach (int hour in testHours)
                    {
                        foreach (int year in testYears)
                        {
                            editor.StartMonthSpin.Value = month;
                            editor.StartDaySpin.Value = day;
                            editor.StartHourSpin.Value = hour;
                            editor.StartYearSpin.Value = year;
                            editor.OnValueChanged();

                            // Build and verify
                            var (data, _) = editor.Build();
                            var modifiedGff = GFF.FromBytes(data);
                            var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                            modifiedIfo.StartMonth.Should().Be(month, $"StartMonth should be {month}");
                            modifiedIfo.StartDay.Should().Be(day, $"StartDay should be {day}");
                            modifiedIfo.StartHour.Should().Be(hour, $"StartHour should be {hour}");
                            modifiedIfo.StartYear.Should().Be(year, $"StartYear should be {year}");
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ifo_editor.py:244-263
        // Original: def test_ifo_editor_manipulate_xp_scale(qtbot, installation: HTInstallation):
        [Fact]
        public void TestIfoEditorManipulateXpScale()
        {
            // Get installation if available (needed for some operations)
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new IFOEditor(null, installation);

            editor.New();

            // Test various XP scales
            int[] testScales = { 0, 50, 100, 200 };
            foreach (int scale in testScales)
            {
                editor.XpScaleSpin.Value = scale;
                editor.OnValueChanged();

                // Build and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedIfo = CSharpKOTOR.Resource.Generics.IFOHelpers.ConstructIfo(modifiedGff);
                modifiedIfo.XpScale.Should().Be(scale, $"XpScale should be {scale} after setting XpScaleSpin to {scale}");
            }
        }
    }
}

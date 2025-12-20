using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resource;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:59
    // Original: class TwoDAEditorTest(TestCase):
    [Collection("Avalonia Test Collection")]
    public class TwoDAEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;
        private readonly List<string> _logMessages = new List<string> { Environment.NewLine };

        public TwoDAEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:60-65
        // Original: @classmethod def setUpClass(cls):
        static TwoDAEditorTests()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            if (!string.IsNullOrEmpty(k2Path) && File.Exists(Path.Combine(k2Path, "chitin.key")))
            {
                _installation = new HTInstallation(k2Path, "", tsl: true);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:77-78
        // Original: def log_func(self, *args):
        private void LogFunc(string message)
        {
            _logMessages.Add(message);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:80-92
        // Original: def test_save_and_load(self):
        [Fact]
        public void TestSaveAndLoad()
        {
            if (_installation == null)
            {
                return; // Skip if K2_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:81
            // Original: filepath = TESTS_FILES_PATH / "appearance.2da"
            string testFilesPath = GetTestFilesPath();
            string filepath = Path.Combine(testFilesPath, "appearance.2da");

            // If test file doesn't exist, try to get it from installation
            if (!File.Exists(filepath))
            {
                var resource = _installation.Resource("appearance", ResourceType.TwoDA);
                if (resource == null || resource.Data == null)
                {
                    return; // Skip if appearance.2da not available
                }
                // Use the resource data directly
                byte[] data = resource.Data;
                2DA old = new TwoDABinaryReader(data).Load();
                var editor = new TwoDAEditor(null, _installation);
                editor.Load("appearance.2da", "appearance", ResourceType.TwoDA, data);

                var (newData, _) = editor.Build();
                2DA newTwoda = new TwoDABinaryReader(newData).Load();

                bool diff = old.Compare(newTwoda, LogFunc);
                diff.Should().BeTrue($"2DA comparison failed. Log messages: {string.Join(Environment.NewLine, _logMessages)}");
                AssertDeepEqual(old, newTwoda);
            }
            else
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:83
                // Original: data = filepath.read_bytes()
                byte[] data = File.ReadAllBytes(filepath);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:84
                // Original: old = read_2da(data)
                2DA old = new TwoDABinaryReader(data).Load();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:85
                // Original: self.editor.load(filepath, "appearance", ResourceType.TwoDA, data)
                var editor = new TwoDAEditor(null, _installation);
                editor.Load(filepath, "appearance", ResourceType.TwoDA, data);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:87
                // Original: data, _ = self.editor.build()
                var (newData, _) = editor.Build();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:88
                // Original: new = read_2da(data)
                2DA newTwoda = new TwoDABinaryReader(newData).Load();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:90
                // Original: diff = old.compare(new, self.log_func)
                bool diff = old.Compare(newTwoda, LogFunc);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:91
                // Original: assert diff
                diff.Should().BeTrue($"2DA comparison failed. Log messages: {string.Join(Environment.NewLine, _logMessages)}");

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:92
                // Original: self.assertDeepEqual(old, new)
                AssertDeepEqual(old, newTwoda);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:126-143
        // Original: def assertDeepEqual(self, obj1, obj2, context=""):
        private void AssertDeepEqual(2DA obj1, 2DA obj2, string context = "")
        {
            // Compare headers
            var oldHeaders = new HashSet<string>(obj1.GetHeaders());
            var newHeaders = new HashSet<string>(obj2.GetHeaders());
            oldHeaders.SetEquals(newHeaders).Should().BeTrue($"Headers mismatch at {context}");

            // Compare row count
            obj1.GetHeight().Should().Be(obj2.GetHeight(), $"Row count mismatch at {context}");

            // Compare each row
            for (int i = 0; i < obj1.GetHeight(); i++)
            {
                string rowContext = string.IsNullOrEmpty(context) ? $"[{i}]" : $"{context}[{i}]";

                // Compare labels
                obj1.GetLabel(i).Should().Be(obj2.GetLabel(i), $"Label mismatch at {rowContext}");

                // Compare cell values for each header
                foreach (string header in oldHeaders)
                {
                    string cellContext = $"{rowContext}.{header}";
                    string oldValue = obj1.GetCellString(i, header);
                    string newValue = obj2.GetCellString(i, header);
                    oldValue.Should().Be(newValue, $"Cell value mismatch at {cellContext}");
                }
            }
        }

        private string GetTestFilesPath()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:15-16
            // Original: absolute_file_path = pathlib.Path(__file__).resolve()
            // Original: TESTS_FILES_PATH = next(f for f in absolute_file_path.parents if f.name == "tests") / "test_files"
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyLocation);

            // Find the tests directory by going up from the test assembly
            DirectoryInfo current = new DirectoryInfo(assemblyDir);
            while (current != null && current.Name != "tests" && current.Name != "HolocronToolset.Tests")
            {
                current = current.Parent;
            }

            if (current != null)
            {
                // Look for test_files directory
                string testFilesPath = Path.Combine(current.FullName, "test_files");
                if (Directory.Exists(testFilesPath))
                {
                    return testFilesPath;
                }

                // Also check in vendor/PyKotor/Tools/HolocronToolset/tests/test_files
                string vendorTestFiles = Path.Combine(
                    current.FullName,
                    "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                vendorTestFiles = Path.GetFullPath(vendorTestFiles);
                if (Directory.Exists(vendorTestFiles))
                {
                    return vendorTestFiles;
                }
            }

            return Path.Combine(assemblyDir, "test_files");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:110-124
        // Original: def test_2da_save_load_from_k2_installation(self):
        [Fact]
        public void Test2DASaveLoadFromK2Installation()
        {
            if (_installation == null)
            {
                return; // Skip if K2_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:115
            // Original: self.installation = Installation(K2_PATH)
            // Note: _installation is already set up in static constructor for K2

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:116
            // Original: for twoda_resource in (resource for resource in self.installation if resource.restype() is ResourceType.TwoDA):
            var chitinResources = _installation.Installation.Resources.GetChitinResources();
            var twodaResources = chitinResources.Where(r => r.ResType == ResourceType.TwoDA).ToList();

            if (twodaResources.Count == 0)
            {
                return; // Skip if no 2DA resources found
            }

            var editor = new TwoDAEditor(null, _installation);

            foreach (var twodaResource in twodaResources)
            {
                _logMessages.Clear();
                _logMessages.Add(Environment.NewLine);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:117
                // Original: old = read_2da(twoda_resource.data())
                byte[] resourceData = twodaResource.GetData();
                if (resourceData == null || resourceData.Length == 0)
                {
                    continue; // Skip if resource data is invalid
                }

                2DA old = new TwoDABinaryReader(resourceData).Load();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:118
                // Original: self.editor.load(twoda_resource.filepath(), twoda_resource.resname(), twoda_resource.restype(), twoda_resource.data())
                editor.Load(twodaResource.FilePath, twodaResource.ResName, twodaResource.ResType, resourceData);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:120
                // Original: data, _ = self.editor.build()
                var (newData, _) = editor.Build();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:121
                // Original: new = read_2da(data)
                2DA newTwoda = new TwoDABinaryReader(newData).Load();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:123
                // Original: diff = old.compare(new, self.log_func)
                bool diff = old.Compare(newTwoda, LogFunc);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:124
                // Original: assert diff, os.linesep.join(self.log_messages)
                string message = "2DA comparison failed for " + twodaResource.ResName + "." + twodaResource.ResType.Extension + ". Log messages: " + string.Join(Environment.NewLine, _logMessages);
                diff.Should().BeTrue(message);
            }
        }

        [Fact]
        public void TestTwoDAEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_2da_editor.py:21
            // Original: def test_twoda_editor_new_file_creation(qtbot, installation):
            var editor = new TwoDAEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }
    }
}

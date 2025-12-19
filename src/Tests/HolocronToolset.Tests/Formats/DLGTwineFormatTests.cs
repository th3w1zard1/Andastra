using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource.Generics.DLG;
using Andastra.Parsing.Resource.Generics.DLG.IO;
using FluentAssertions;
using Xunit;

namespace HolocronToolset.Tests.Formats
{
    /// <summary>
    /// Tests for Twine format support in dialog system.
    /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py
    /// </summary>
    public class DLGTwineFormatTests
    {
        /// <summary>
        /// Create a sample dialog for testing.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:21-45
        /// </summary>
        private DLG CreateSampleDlg()
        {
            var dlg = new DLG();

            // Create some entries and replies
            var entry1 = new DLGEntry();
            entry1.Speaker = "NPC";
            entry1.Text.SetData(Language.English, Gender.Male, "Hello there!");

            var reply1 = new DLGReply();
            reply1.Text.SetData(Language.English, Gender.Male, "General Kenobi!");

            var entry2 = new DLGEntry();
            entry2.Speaker = "NPC";
            entry2.Text.SetData(Language.English, Gender.Male, "You are a bold one.");

            // Link them together
            entry1.Links.Add(new DLGLink(reply1));
            reply1.Links.Add(new DLGLink(entry2));

            // Add starting node
            dlg.Starters.Add(new DLGLink(entry1));

            return dlg;
        }

        /// <summary>
        /// Test writing and reading dialog in JSON format.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:48-68
        /// </summary>
        [Fact]
        public void TestWriteReadJson()
        {
            var sampleDlg = CreateSampleDlg();
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string jsonFile = Path.Combine(tempPath, "test.json");

            try
            {
                // Write to JSON
                Twine.WriteTwine(sampleDlg, jsonFile, "json");

                // Verify JSON structure
                string content = File.ReadAllText(jsonFile);
                content.Should().Contain("\"name\"");
                content.Should().Contain("\"passages\"");
                content.Should().Contain("Hello there!");
                content.Should().Contain("General Kenobi!");

                // Read back
                var dlg = Twine.ReadTwine(jsonFile);
                dlg.Starters.Count.Should().Be(1);
                dlg.Starters[0].Node.Should().BeOfType<DLGEntry>();
                dlg.Starters[0].Node.Text.GetString(Language.English, Gender.Male).Should().Be("Hello there!");
            }
            finally
            {
                if (File.Exists(jsonFile))
                {
                    File.Delete(jsonFile);
                }
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath);
                }
            }
        }

        /// <summary>
        /// Test writing and reading dialog in HTML format.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:71-81
        /// </summary>
        [Fact]
        public void TestWriteReadHtml()
        {
            var sampleDlg = CreateSampleDlg();
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string htmlFile = Path.Combine(tempPath, "test.html");

            try
            {
                // Write to HTML
                Twine.WriteTwine(sampleDlg, htmlFile, "html");

                // Read back
                var dlg = Twine.ReadTwine(htmlFile);
                dlg.Starters.Count.Should().Be(1);
                dlg.Starters[0].Node.Should().BeOfType<DLGEntry>();
                dlg.Starters[0].Node.Text.GetString(Language.English, Gender.Male).Should().Be("Hello there!");
            }
            finally
            {
                if (File.Exists(htmlFile))
                {
                    File.Delete(htmlFile);
                }
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath);
                }
            }
        }

        /// <summary>
        /// Test that metadata is preserved during write/read.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:84-107
        /// </summary>
        [Fact]
        public void TestMetadataPreservation()
        {
            var sampleDlg = CreateSampleDlg();
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string jsonFile = Path.Combine(tempPath, "test.json");

            try
            {
                var metadata = new Dictionary<string, object>
                {
                    { "name", "Test Dialog" },
                    { "format", "Harlowe" },
                    { "format-version", "3.3.7" },
                    { "tag-colors", new Dictionary<string, Color>() },
                    { "style", "body { color: red; }" },
                    { "script", "window.setup = {};" },
                };

                // Write with metadata
                Twine.WriteTwine(sampleDlg, jsonFile, "json", metadata);

                // Verify metadata in JSON
                string content = File.ReadAllText(jsonFile);
                content.Should().Contain("Test Dialog");
                content.Should().Contain("Harlowe");
                content.Should().Contain("3.3.7");
                content.Should().Contain("body { color: red; }");
                content.Should().Contain("window.setup = {};");
            }
            finally
            {
                if (File.Exists(jsonFile))
                {
                    File.Delete(jsonFile);
                }
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath);
                }
            }
        }

        /// <summary>
        /// Test that links between nodes are preserved.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:132-153
        /// </summary>
        [Fact]
        public void TestLinkPreservation()
        {
            var sampleDlg = CreateSampleDlg();
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string jsonFile = Path.Combine(tempPath, "test.json");

            try
            {
                // Write to JSON
                Twine.WriteTwine(sampleDlg, jsonFile, "json");

                // Read back
                var dlg = Twine.ReadTwine(jsonFile);

                // Verify links
                dlg.Starters.Count.Should().Be(1);
                var entry1 = dlg.Starters[0].Node as DLGEntry;
                entry1.Should().NotBeNull();
                entry1.Links.Count.Should().Be(1);

                var reply1 = entry1.Links[0].Node as DLGReply;
                reply1.Should().NotBeNull();
                reply1.Links.Count.Should().Be(1);

                var entry2 = reply1.Links[0].Node as DLGEntry;
                entry2.Should().NotBeNull();
                entry2.Text.GetString(Language.English, Gender.Male).Should().Be("You are a bold one.");
            }
            finally
            {
                if (File.Exists(jsonFile))
                {
                    File.Delete(jsonFile);
                }
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath);
                }
            }
        }

        /// <summary>
        /// Test handling of invalid JSON input.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:156-162
        /// </summary>
        [Fact]
        public void TestInvalidJson()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string invalidFile = Path.Combine(tempPath, "invalid.json");

            try
            {
                File.WriteAllText(invalidFile, "invalid json");
                // read_twine checks format first (starts with { or <), so invalid content raises ArgumentException
                Assert.Throws<ArgumentException>(() => Twine.ReadTwine(invalidFile));
            }
            finally
            {
                if (File.Exists(invalidFile))
                {
                    File.Delete(invalidFile);
                }
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath);
                }
            }
        }

        /// <summary>
        /// Test handling of more complex dialog structures.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:173-222
        /// </summary>
        [Fact]
        public void TestComplexDialogStructure()
        {
            var dlg = new DLG();

            // Create a branching dialog
            var entry1 = new DLGEntry();
            entry1.Speaker = "NPC";
            entry1.Text.SetData(Language.English, Gender.Male, "Choose your path:");

            var reply1 = new DLGReply();
            reply1.Text.SetData(Language.English, Gender.Male, "Path 1");

            var reply2 = new DLGReply();
            reply2.Text.SetData(Language.English, Gender.Male, "Path 2");

            var entry2 = new DLGEntry();
            entry2.Speaker = "NPC";
            entry2.Text.SetData(Language.English, Gender.Male, "Path 1 chosen");

            var entry3 = new DLGEntry();
            entry3.Speaker = "NPC";
            entry3.Text.SetData(Language.English, Gender.Male, "Path 2 chosen");

            // Link them
            entry1.Links.Add(new DLGLink(reply1));
            entry1.Links.Add(new DLGLink(reply2));
            reply1.Links.Add(new DLGLink(entry2));
            reply2.Links.Add(new DLGLink(entry3));

            dlg.Starters.Add(new DLGLink(entry1));

            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string jsonFile = Path.Combine(tempPath, "complex.json");

            try
            {
                // Write and read back
                Twine.WriteTwine(dlg, jsonFile, "json");
                var loadedDlg = Twine.ReadTwine(jsonFile);

                // Verify structure
                loadedDlg.Starters.Count.Should().Be(1);
                var loadedEntry1 = loadedDlg.Starters[0].Node as DLGEntry;
                loadedEntry1.Should().NotBeNull();
                loadedEntry1.Links.Count.Should().Be(2);

                // Verify both paths
                foreach (var link in loadedEntry1.Links)
                {
                    var reply = link.Node as DLGReply;
                    reply.Should().NotBeNull();
                    reply.Links.Count.Should().Be(1);
                    var nextEntry = reply.Links[0].Node as DLGEntry;
                    nextEntry.Should().NotBeNull();
                    string text = nextEntry.Text.GetString(Language.English, Gender.Male);
                    (text == "Path 1 chosen" || text == "Path 2 chosen").Should().BeTrue();
                }
            }
            finally
            {
                if (File.Exists(jsonFile))
                {
                    File.Delete(jsonFile);
                }
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath);
                }
            }
        }
    }
}


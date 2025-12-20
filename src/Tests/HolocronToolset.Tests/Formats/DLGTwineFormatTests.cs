using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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

        /// <summary>
        /// Test that passage metadata (position, size) is preserved.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:110-129
        /// </summary>
        [Fact]
        public void TestPassageMetadata()
        {
            var sampleDlg = CreateSampleDlg();
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string jsonFile = Path.Combine(tempPath, "test.json");

            try
            {
                // Write to JSON
                Twine.WriteTwine(sampleDlg, jsonFile, "json");

                // Verify metadata in JSON (defaults should be present)
                string content = File.ReadAllText(jsonFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;
                var passages = root.GetProperty("passages");
                
                // Find the NPC passage
                bool foundNpcPassage = false;
                foreach (var passage in passages.EnumerateArray())
                {
                    if (passage.TryGetProperty("name", out var nameProp) && nameProp.GetString() == "NPC")
                    {
                        foundNpcPassage = true;
                        if (passage.TryGetProperty("metadata", out var metadataProp))
                        {
                            // Default position is "0.0,0.0" and size is "100.0,100.0"
                            metadataProp.TryGetProperty("position", out var positionProp).Should().BeTrue();
                            metadataProp.TryGetProperty("size", out var sizeProp).Should().BeTrue();
                            positionProp.GetString().Should().Be("0.0,0.0");
                            sizeProp.GetString().Should().Be("100.0,100.0");
                        }
                        break;
                    }
                }
                foundNpcPassage.Should().BeTrue("NPC passage should exist");
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
        /// Test handling of invalid HTML input.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:165-170
        /// </summary>
        [Fact]
        public void TestInvalidHtml()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string invalidFile = Path.Combine(tempPath, "invalid.html");

            try
            {
                File.WriteAllText(invalidFile, "<not valid html>");
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
        /// The first starter should be used as startnode when exporting to Twine JSON.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:256-277
        /// </summary>
        [Fact]
        public void TestFirstStarterBecomesStartPid()
        {
            var dlg = new DLG();
            var entry1 = new DLGEntry();
            entry1.Speaker = "First";
            entry1.Text.SetData(Language.English, Gender.Male, "First line");

            var entry2 = new DLGEntry();
            entry2.Speaker = "Second";
            entry2.Text.SetData(Language.English, Gender.Male, "Second line");

            dlg.Starters.Add(new DLGLink(entry1));
            dlg.Starters.Add(new DLGLink(entry2));

            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string jsonFile = Path.Combine(tempPath, "multi_start.json");

            try
            {
                Twine.WriteTwine(dlg, jsonFile, "json");

                string content = File.ReadAllText(jsonFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;
                
                // startnode should be recorded
                root.TryGetProperty("startnode", out var startNodeProp).Should().BeTrue();
                startNodeProp.GetString().Should().NotBeNullOrEmpty();
                
                var passages = root.GetProperty("passages");
                passages.GetArrayLength().Should().Be(2);
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
        /// Metadata supplied to write_twine should be persisted in dlg.comment.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:302-328
        /// </summary>
        [Fact]
        public void TestMetadataCommentRoundtripForTagColorsAndZoom()
        {
            var dlg = new DLG();
            var entry = new DLGEntry();
            entry.Speaker = "Meta";
            entry.Text.SetData(Language.English, Gender.Male, "meta");
            dlg.Starters.Add(new DLGLink(entry));

            var metadata = new Dictionary<string, object>
            {
                { "name", "Meta Story" },
                { "format", "Harlowe" },
                { "format-version", "3.3.7" },
                { "tag-colors", new Dictionary<string, object> { { "entry", "1 0 0 1" }, { "reply", "0 1 0 1" } } },
                { "style", "body { color: green; }" },
                { "script", "window.meta = true;" },
                { "zoom", 2.5 },
            };

            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string jsonFile = Path.Combine(tempPath, "meta.json");

            try
            {
                Twine.WriteTwine(dlg, jsonFile, "json", metadata);

                var restoredDlg = Twine.ReadTwine(jsonFile);
                restoredDlg.Comment.Should().NotBeNullOrEmpty();
                
                // Parse the comment as JSON
                var restoredMeta = System.Text.Json.JsonDocument.Parse(restoredDlg.Comment);
                var root = restoredMeta.RootElement;
                
                root.GetProperty("style").GetString().Should().Be("body { color: green; }");
                root.GetProperty("script").GetString().Should().Be("window.meta = true;");
                root.GetProperty("tag_colors").GetProperty("entry").GetString().Should().Be("1 0 0 1");
                root.GetProperty("tag_colors").GetProperty("reply").GetString().Should().Be("0 1 0 1");
                root.GetProperty("zoom").GetDouble().Should().Be(2.5);
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
        /// Ensure Twine metadata embedded in DLG.comment is surfaced in TwineStory.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:225-253
        /// </summary>
        [Fact]
        public void TestCommentMetadataIsRestoredIntoTwineStory()
        {
            var dlg = new DLG();
            var entry = new DLGEntry();
            entry.Speaker = "NPC";
            entry.Text.SetData(Language.English, Gender.Male, "Hi");
            dlg.Starters.Add(new DLGLink(entry));
            
            // Set comment with JSON metadata
            var commentData = new Dictionary<string, object>
            {
                { "style", "body { color: blue; }" },
                { "script", "window.custom = true;" },
                { "tag_colors", new Dictionary<string, object> { { "entry", "1 0 0 1" } } },
                { "format", "SugarCube" },
                { "format_version", "2.0.0" },
                { "creator", "Tester" },
                { "creator_version", "0.1" },
                { "zoom", 1.25 },
            };
            dlg.Comment = JsonSerializer.Serialize(commentData);

            // Use reflection to call private DlgToStory method
            var twineType = typeof(Twine);
            var dlgToStoryMethod = twineType.GetMethod("DlgToStory", BindingFlags.NonPublic | BindingFlags.Static);
            dlgToStoryMethod.Should().NotBeNull("DlgToStory method should exist");
            
            var story = (TwineStory)dlgToStoryMethod.Invoke(null, new object[] { dlg, null });
            story.Should().NotBeNull();
            story.Metadata.Style.Should().Be("body { color: blue; }");
            story.Metadata.Script.Should().Be("window.custom = true;");
            story.Metadata.Format.Should().Be("SugarCube");
            story.Metadata.FormatVersion.Should().Be("2.0.0");
            story.Metadata.Creator.Should().Be("Tester");
            story.Metadata.CreatorVersion.Should().Be("0.1");
            story.Metadata.Zoom.Should().Be(1.25f);
            story.Metadata.TagColors.Should().NotBeNull();
            story.Metadata.TagColors.ContainsKey("entry").Should().BeTrue();
        }

        /// <summary>
        /// TwineStory.GetLinksTo should report all linking passages.
        /// Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg_twine.py:280-299
        /// </summary>
        [Fact]
        public void TestGetLinksToReportsSources()
        {
            var dlg = new DLG();
            var entry = new DLGEntry();
            entry.Speaker = "A";
            entry.Text.SetData(Language.English, Gender.Male, "Hi");
            var reply = new DLGReply();
            reply.Text.SetData(Language.English, Gender.Male, "R");
            entry.Links.Add(new DLGLink(reply));
            reply.Links.Add(new DLGLink(entry)); // Circular reference
            dlg.Starters.Add(new DLGLink(entry));

            // Use reflection to call private DlgToStory method
            var twineType = typeof(Twine);
            var dlgToStoryMethod = twineType.GetMethod("DlgToStory", BindingFlags.NonPublic | BindingFlags.Static);
            dlgToStoryMethod.Should().NotBeNull("DlgToStory method should exist");
            
            var story = (TwineStory)dlgToStoryMethod.Invoke(null, new object[] { dlg, null });
            story.Should().NotBeNull();
            
            var target = story.GetPassage("A");
            target.Should().NotBeNull();
            
            var linking = story.GetLinksTo(target);
            linking.Should().NotBeNull();
            linking.Count.Should().BeGreaterThan(0);
            
            var firstLink = linking[0];
            firstLink.Item1.Type.Should().Be(PassageType.Reply);
            firstLink.Item2.Target.Should().Be(target.Name);
        }
    }
}


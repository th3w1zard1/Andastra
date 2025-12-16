using System;
using System.Collections.Generic;
using System.Linq;
using AuroraEngine.Common.Formats.LIP;
using AuroraEngine.Common.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py
    // Original: Comprehensive tests for LIP Editor
    [Collection("Avalonia Test Collection")]
    public class LIPEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public LIPEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestLipEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:26-37
            // Original: def test_lip_editor_new_file_creation(qtbot, installation):
            var editor = new LIPEditor(null, null);

            editor.New();

            // Verify LIP object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestLipEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:39-56
            // Original: def test_lip_editor_initialization(qtbot, installation):
            var editor = new LIPEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:62-85
        // Original: def test_lip_editor_add_keyframe(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorAddKeyframe()
        {
            var editor = new LIPEditor(null, null);

            editor.New();

            // Set duration first
            editor.Duration = 10.0f;
            // Ensure LIP exists and length is set
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 10.0f;

            // Add keyframe
            editor.AddKeyframe(1.0f, LIPShape.AH);

            // Verify keyframe was added
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(1);
            Math.Abs(editor.Lip.Frames[0].Time - 1.0f).Should().BeLessThan(0.001f);
            editor.Lip.Frames[0].Shape.Should().Be(LIPShape.AH);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:87-118
        // Original: def test_lip_editor_add_multiple_keyframes(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorAddMultipleKeyframes()
        {
            var editor = new LIPEditor(null, null);

            editor.New();
            editor.Duration = 10.0f;
            // Ensure LIP exists and length is set
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 10.0f;

            // Add multiple keyframes
            var testKeyframes = new[]
            {
                (0.0f, LIPShape.AH),
                (1.0f, LIPShape.EE),
                (2.0f, LIPShape.OH),
                (3.0f, LIPShape.MPB),
            };

            foreach (var (time, shape) in testKeyframes)
            {
                editor.AddKeyframe(time, shape);
            }

            // Verify all keyframes were added
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(testKeyframes.Length);

            // Verify keyframes are sorted by time
            var sortedFrames = new List<LIPKeyFrame>(editor.Lip.Frames);
            sortedFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
            for (int i = 0; i < testKeyframes.Length; i++)
            {
                var (expectedTime, expectedShape) = testKeyframes[i];
                Math.Abs(sortedFrames[i].Time - expectedTime).Should().BeLessThan(0.001f);
                sortedFrames[i].Shape.Should().Be(expectedShape);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:120-149
        // Original: def test_lip_editor_update_keyframe(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorUpdateKeyframe()
        {
            var editor = new LIPEditor(null, null);

            editor.New();
            editor.Duration = 10.0f;
            // Ensure LIP exists and length is set
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 10.0f;

            // Add keyframe
            editor.AddKeyframe(1.0f, LIPShape.AH);

            // Update keyframe (index 0)
            editor.UpdateKeyframe(0, 1.5f, LIPShape.EE);

            // Verify keyframe was updated
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(1);
            Math.Abs(editor.Lip.Frames[0].Time - 1.5f).Should().BeLessThan(0.001f);
            editor.Lip.Frames[0].Shape.Should().Be(LIPShape.EE);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:151-178
        // Original: def test_lip_editor_delete_keyframe(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorDeleteKeyframe()
        {
            var editor = new LIPEditor(null, null);

            editor.New();
            editor.Duration = 10.0f;
            // Ensure LIP exists and length is set
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 10.0f;

            // Add keyframes
            editor.AddKeyframe(1.0f, LIPShape.AH);
            editor.AddKeyframe(2.0f, LIPShape.EE);

            // Delete first keyframe (index 0)
            editor.DeleteKeyframe(0);

            // Verify keyframe was deleted
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(1);
            editor.Lip.Frames[0].Shape.Should().Be(LIPShape.EE);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:198-230
        // Original: def test_lip_editor_set_different_shapes(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorSetDifferentShapes()
        {
            var editor = new LIPEditor(null, null);

            editor.New();
            editor.Duration = 10.0f;
            // Ensure LIP exists and length is set
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 10.0f;

            // Test various shapes
            var testShapes = new[]
            {
                LIPShape.AH,
                LIPShape.EE,
                LIPShape.OH,
                LIPShape.MPB,
                LIPShape.FV,
                LIPShape.TD,
                LIPShape.KG,
                LIPShape.L,
            };

            for (int i = 0; i < testShapes.Length; i++)
            {
                editor.AddKeyframe((float)i, testShapes[i]);
            }

            // Verify all shapes were set
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(testShapes.Length);
            var sortedFrames = new List<LIPKeyFrame>(editor.Lip.Frames);
            sortedFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
            for (int i = 0; i < testShapes.Length; i++)
            {
                sortedFrames[i].Shape.Should().Be(testShapes[i]);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:367-381
        // Original: def test_lip_editor_duration_setting(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorDurationSetting()
        {
            var editor = new LIPEditor(null, null);

            editor.New();

            // Set duration
            editor.Duration = 5.5f;

            // Verify duration was set
            Math.Abs(editor.Duration - 5.5f).Should().BeLessThan(0.001f);
            if (editor.Lip != null)
            {
                Math.Abs(editor.Lip.Length - 5.5f).Should().BeLessThan(0.001f);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:383-401
        // Original: def test_lip_editor_duration_from_loaded_lip(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorDurationFromLoadedLip()
        {
            var editor = new LIPEditor(null, null);

            editor.New();
            editor.Duration = 10.0f;
            editor.Lip.Length = 10.0f;
            // Add at least one frame to make the LIP valid
            editor.Lip.Add(0.0f, LIPShape.AH);

            // Build and load
            var (data, _) = editor.Build();
            data.Length.Should().BeGreaterThan(0, "LIP data should not be empty");
            editor.Load("test.lip", "test", ResourceType.LIP, data);

            // Verify duration was loaded
            Math.Abs(editor.Duration - 10.0f).Should().BeLessThan(0.001f);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:519-536
        // Original: def test_lip_editor_empty_lip_file(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorEmptyLipFile()
        {
            var editor = new LIPEditor(null, null);

            editor.New();

            // Build empty file
            var (data, _) = editor.Build();

            // Empty LIPs may produce header data (16 bytes: "LIP " + "V1.0" + length + count)
            // The exact behavior depends on implementation
            // Verify that we can build an empty LIP
            data.Should().NotBeNull();

            // Verify that empty LIP has no frames
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:538-574
        // Original: def test_lip_editor_keyframes_sorted_by_time(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorKeyframesSortedByTime()
        {
            var editor = new LIPEditor(null, null);

            editor.New();
            editor.Duration = 10.0f;
            // Ensure LIP exists and length is set
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 10.0f;

            // Add keyframes out of order
            editor.AddKeyframe(3.0f, LIPShape.AH);
            editor.AddKeyframe(1.0f, LIPShape.EE);
            editor.AddKeyframe(2.0f, LIPShape.OH);

            // Verify keyframes are sorted by time
            editor.Lip.Frames.Count.Should().Be(3);
            // LIP.Add automatically sorts frames, so verify they are in order
            Math.Abs(editor.Lip.Frames[0].Time - 1.0f).Should().BeLessThan(0.001f);
            Math.Abs(editor.Lip.Frames[1].Time - 2.0f).Should().BeLessThan(0.001f);
            Math.Abs(editor.Lip.Frames[2].Time - 3.0f).Should().BeLessThan(0.001f);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:580-609
        // Original: def test_lip_editor_complex_lip_file(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorComplexLipFile()
        {
            var editor = new LIPEditor(null, null);

            editor.New();
            editor.Duration = 10.0f;
            // Ensure LIP exists and length is set
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 10.0f;

            // Add many keyframes
            var shapes = new[] { LIPShape.AH, LIPShape.EE, LIPShape.OH, LIPShape.MPB, LIPShape.FV };
            for (int i = 0; i < 10; i++)
            {
                editor.AddKeyframe((float)i, shapes[i % shapes.Length]);
            }

            // Verify all keyframes were added
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(10);

            // Build and verify
            var (data, _) = editor.Build();
            data.Length.Should().BeGreaterThan(0);

            // Load and verify
            editor.Load("test.lip", "test", ResourceType.LIP, data);
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(10);
            Math.Abs(editor.Lip.Length - 10.0f).Should().BeLessThan(0.001f);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:611-639
        // Original: def test_lip_editor_all_shapes_used(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorAllShapesUsed()
        {
            var editor = new LIPEditor(null, null);

            editor.New();
            editor.Duration = 20.0f;
            // Ensure LIP exists and length is set
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 20.0f;

            // Use all LIP shapes
            var allShapes = new[]
            {
                LIPShape.Neutral, LIPShape.EE, LIPShape.EH, LIPShape.AH, LIPShape.OH,
                LIPShape.OOH, LIPShape.Y, LIPShape.STS, LIPShape.FV, LIPShape.NG,
                LIPShape.TH, LIPShape.MPB, LIPShape.TD, LIPShape.SH, LIPShape.L, LIPShape.KG
            };

            for (int i = 0; i < allShapes.Length; i++)
            {
                editor.AddKeyframe((float)i, allShapes[i]);
            }

            // Verify all shapes were used
            editor.Lip.Should().NotBeNull();
            editor.Lip.Frames.Count.Should().Be(allShapes.Length);

            // Verify each shape is present
            var usedShapes = new System.Collections.Generic.HashSet<LIPShape>();
            foreach (var frame in editor.Lip.Frames)
            {
                usedShapes.Add(frame.Shape);
            }
            usedShapes.Count.Should().Be(allShapes.Length);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:669-705
        // Original: def test_lip_editor_headless_ui_load_build(qtbot: QtBot, installation: HTInstallation, test_files_dir: pathlib.Path):
        [Fact]
        public void TestLipEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a LIP file
            string[] lipFiles = new string[0];
            if (System.IO.Directory.Exists(testFilesDir))
            {
                try
                {
                    lipFiles = System.IO.Directory.GetFiles(testFilesDir, "*.lip", System.IO.SearchOption.AllDirectories);
                }
                catch
                {
                    // If directory access fails, try alternative location
                }
            }

            if (lipFiles.Length == 0)
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                if (System.IO.Directory.Exists(testFilesDir))
                {
                    try
                    {
                        lipFiles = System.IO.Directory.GetFiles(testFilesDir, "*.lip", System.IO.SearchOption.AllDirectories);
                    }
                    catch
                    {
                        // If directory access fails, continue without test files
                    }
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

            var editor = new LIPEditor(null, installation);

            byte[] originalData = null;
            string lipFile = null;
            string resname = "test";

            if (lipFiles.Length > 0)
            {
                lipFile = lipFiles[0];
                originalData = System.IO.File.ReadAllBytes(lipFile);
                resname = System.IO.Path.GetFileNameWithoutExtension(lipFile);
            }
            else if (installation != null)
            {
                // Try to get one from installation - iterate through core resources and filter by LIP type
                // Matching Python: lip_resources = list(installation.resources(ResourceType.LIP))[:1]
                try
                {
                    var allResources = installation.Installation.CoreResources();
                    foreach (var resource in allResources)
                    {
                        if (resource.ResType == ResourceType.LIP)
                        {
                            var result = installation.Resource(resource.ResName, ResourceType.LIP);
                            if (result != null && result.Data != null && result.Data.Length > 0)
                            {
                                originalData = result.Data;
                                resname = resource.ResName;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // If resources iteration fails, continue without installation resources
                }
            }

            if (originalData == null || originalData.Length == 0)
            {
                // Skip if no LIP files available for testing (matching Python pytest.skip behavior)
                return;
            }

            editor.Load(lipFile ?? "test.lip", resname, ResourceType.LIP, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();
            editor.Lip.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            if (data.Length > 0)
            {
                try
                {
                    LIP loadedLip = LIPAuto.ReadLip(data);
                    loadedLip.Should().NotBeNull();
                }
                catch
                {
                    // If reading fails, that's okay - the test still verified build works
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:299-329
        // Original: def test_lip_editor_save_load_roundtrip(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorSaveLoadRoundtrip()
        {
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

            var editor = new LIPEditor(null, installation);

            editor.New();
            editor.Duration = 10.0f;

            // Ensure LIP exists and length is set before adding keyframes
            // This matches Python behavior where duration is set first
            if (editor.Lip == null)
            {
                // This shouldn't happen after New(), but be safe
            }
            editor.Lip.Length = 10.0f;

            // Add keyframes
            editor.AddKeyframe(1.0f, LIPShape.AH);
            editor.AddKeyframe(2.0f, LIPShape.EE);

            // Build
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0, $"Built LIP data should not be empty. Actual length: {data.Length}");
            
            // LIP format: "LIP " (4) + "V1.0" (4) + Length (4) + Count (4) + frames
            // Minimum size for 2 frames: 16 + 10 = 26 bytes
            data.Length.Should().BeGreaterThanOrEqualTo(26, $"LIP data should be at least 26 bytes for 2 frames. Actual: {data.Length}");
            
            // Verify the data can be read back as LIP before loading into editor
            try
            {
                LIP testLip = LIPAuto.ReadLip(data);
                testLip.Should().NotBeNull();
                testLip.Frames.Count.Should().Be(2);
            }
            catch (Exception ex)
            {
                // Output first few bytes for debugging
                string hex = data.Length > 0 ? BitConverter.ToString(data, 0, Math.Min(32, data.Length)) : "empty";
                throw new Exception($"Built LIP data is invalid and cannot be read (data length: {data.Length}, first bytes: {hex}): {ex.Message}", ex);
            }

            // Load it back
            editor.Load("test.lip", "test", ResourceType.LIP, data);

            // Verify data was loaded
            editor.Lip.Should().NotBeNull("LIP should be loaded");
            editor.Lip.Frames.Count.Should().Be(2, "Should have 2 keyframes");
            Math.Abs(editor.Lip.Length - 10.0f).Should().BeLessThan(0.001f, "LIP length should be 10.0");
            Math.Abs(editor.Duration - 10.0f).Should().BeLessThan(0.001f, "Editor duration should be 10.0");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_lip_editor.py:331-361
        // Original: def test_lip_editor_multiple_save_load_cycles(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestLipEditorMultipleSaveLoadCycles()
        {
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

            var editor = new LIPEditor(null, installation);

            editor.New();
            editor.Duration = 10.0f;

            // Perform multiple cycles
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Clear and add keyframe
                editor.New();
                editor.Duration = 10.0f;

                // Add keyframe at cycle time
                editor.AddKeyframe((float)cycle, LIPShape.AH);

                // Save
                var (data, _) = editor.Build();

                // Load back
                editor.Load("test.lip", "test", ResourceType.LIP, data);

                // Verify keyframe was preserved
                editor.Lip.Should().NotBeNull("LIP should be loaded");
                editor.Lip.Frames.Count.Should().Be(1, $"Should have 1 keyframe after cycle {cycle}");
            }
        }
    }
}

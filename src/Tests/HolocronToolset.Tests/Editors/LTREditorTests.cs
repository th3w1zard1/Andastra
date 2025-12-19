using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Formats.LTR;
using Andastra.Parsing.Resource;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py
    // Original: Comprehensive tests for LTR Editor - testing EVERY possible manipulation.
    [Collection("Avalonia Test Collection")]
    public class LTREditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public LTREditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        // ============================================================================
        // BASIC FIELD MANIPULATIONS
        // ============================================================================

        [Fact]
        public void TestLtrEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:26-42
            // Original: def test_ltr_editor_new_file_creation(qtbot, installation):
            var editor = new LTREditor(null, null);

            editor.New();

            // Verify UI is populated
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestLtrEditorLoadEmptyFile()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:44-58
            // Original: def test_ltr_editor_load_empty_file(qtbot, installation):
            var editor = new LTREditor(null, null);

            editor.New();

            // Build empty file
            var (data, _) = editor.Build();

            // Load it back
            editor.Load("test.ltr", "test", ResourceType.LTR, data);

            // Verify it loaded correctly
            var (data2, _) = editor.Build();
            data2.Should().NotBeNull();
        }

        // ============================================================================
        // SINGLE CHARACTER MANIPULATIONS
        // ============================================================================

        [Fact]
        public void TestLtrEditorManipulateSingleCharacter()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:64-91
            // Original: def test_ltr_editor_manipulate_single_character(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Test setting single character values
            string testChar = "A";
            decimal startVal = 10;
            decimal middleVal = 20;
            decimal endVal = 30;

            // Set values via combo box and spin boxes
            var comboBox = editor.GetType().GetField("_comboBoxSingleChar", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spinBoxStart = editor.GetType().GetField("_spinBoxSingleStart", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spinBoxMiddle = editor.GetType().GetField("_spinBoxSingleMiddle", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spinBoxEnd = editor.GetType().GetField("_spinBoxSingleEnd", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (comboBox?.GetValue(editor) is Avalonia.Controls.ComboBox cb &&
                spinBoxStart?.GetValue(editor) is Avalonia.Controls.NumericUpDown start &&
                spinBoxMiddle?.GetValue(editor) is Avalonia.Controls.NumericUpDown middle &&
                spinBoxEnd?.GetValue(editor) is Avalonia.Controls.NumericUpDown end)
            {
                // Find and select character
                var items = cb.ItemsSource as System.Collections.IEnumerable;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item.ToString() == testChar)
                        {
                            cb.SelectedItem = item;
                            break;
                        }
                    }
                }

                start.Value = startVal;
                middle.Value = middleVal;
                end.Value = endVal;

                // Apply changes via reflection
                var setMethod = editor.GetType().GetMethod("SetSingleCharacter", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                setMethod?.Invoke(editor, null);

                // Verify values were set by building and reading back
                var (data, _) = editor.Build();
                var loadedLtr = LTRAuto.ReadLtr(data, 0, null);
                loadedLtr.GetSinglesStart(testChar).Should().BeApproximately((float)startVal, 0.01f);
                loadedLtr.GetSinglesMiddle(testChar).Should().BeApproximately((float)middleVal, 0.01f);
                loadedLtr.GetSinglesEnd(testChar).Should().BeApproximately((float)endVal, 0.01f);
            }
        }

        [Fact]
        public void TestLtrEditorManipulateMultipleSingleCharacters()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:93-119
            // Original: def test_ltr_editor_manipulate_multiple_single_characters(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Test multiple characters
            string[] testChars = { "A", "B", "C", "Z" };
            var ltr = new LTR();

            // Set values directly on LTR for testing
            for (int i = 0; i < testChars.Length; i++)
            {
                ltr.SetSinglesStart(testChars[i], 10 + i);
                ltr.SetSinglesMiddle(testChars[i], 20 + i);
                ltr.SetSinglesEnd(testChars[i], 30 + i);
            }

            // Load into editor
            byte[] testData = LTRAuto.BytesLtr(ltr);
            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Build and verify
            var (data, _) = editor.Build();
            var modifiedLtr = LTRAuto.ReadLtr(data, 0, null);

            // Verify all characters were set
            for (int i = 0; i < testChars.Length; i++)
            {
                modifiedLtr.GetSinglesStart(testChars[i]).Should().BeApproximately(10 + i, 0.01f);
                modifiedLtr.GetSinglesMiddle(testChars[i]).Should().BeApproximately(20 + i, 0.01f);
                modifiedLtr.GetSinglesEnd(testChars[i]).Should().BeApproximately(30 + i, 0.01f);
            }
        }

        // ============================================================================
        // DOUBLE CHARACTER MANIPULATIONS
        // ============================================================================

        [Fact]
        public void TestLtrEditorManipulateDoubleCharacter()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:125-156
            // Original: def test_ltr_editor_manipulate_double_character(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Test setting double character values
            string prevChar = "A";
            string char_ = "B";
            decimal startVal = 5;
            decimal middleVal = 10;
            decimal endVal = 15;

            var ltr = new LTR();
            ltr.SetDoublesStart(prevChar, char_, (float)startVal);
            ltr.SetDoublesMiddle(prevChar, char_, (float)middleVal);
            ltr.SetDoublesEnd(prevChar, char_, (float)endVal);

            // Load into editor
            byte[] testData = LTRAuto.BytesLtr(ltr);
            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Build and verify
            var (data, _) = editor.Build();
            var modifiedLtr = LTRAuto.ReadLtr(data, 0, null);

            // Verify values were set
            modifiedLtr.GetDoublesStart(prevChar, char_).Should().BeApproximately((float)startVal, 0.01f);
            modifiedLtr.GetDoublesMiddle(prevChar, char_).Should().BeApproximately((float)middleVal, 0.01f);
            modifiedLtr.GetDoublesEnd(prevChar, char_).Should().BeApproximately((float)endVal, 0.01f);
        }

        [Fact]
        public void TestLtrEditorManipulateMultipleDoubleCharacters()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:158-188
            // Original: def test_ltr_editor_manipulate_multiple_double_characters(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Test multiple double combinations
            var testCombos = new[] { ("A", "B"), ("B", "C"), ("C", "D") };
            var ltr = new LTR();

            for (int i = 0; i < testCombos.Length; i++)
            {
                var (prev, char_) = testCombos[i];
                ltr.SetDoublesStart(prev, char_, 5 + i);
                ltr.SetDoublesMiddle(prev, char_, 10 + i);
                ltr.SetDoublesEnd(prev, char_, 15 + i);
            }

            // Load into editor
            byte[] testData = LTRAuto.BytesLtr(ltr);
            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Build and verify
            var (data, _) = editor.Build();
            var modifiedLtr = LTRAuto.ReadLtr(data, 0, null);

            // Verify combinations were set
            for (int i = 0; i < testCombos.Length; i++)
            {
                var (prev, char_) = testCombos[i];
                modifiedLtr.GetDoublesStart(prev, char_).Should().BeApproximately(5 + i, 0.01f);
            }
        }

        // ============================================================================
        // TRIPLE CHARACTER MANIPULATIONS
        // ============================================================================

        [Fact]
        public void TestLtrEditorManipulateTripleCharacter()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:193-230
            // Original: def test_ltr_editor_manipulate_triple_character(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Test setting triple character values
            string prev2Char = "A";
            string prev1Char = "B";
            string char_ = "C";
            decimal startVal = 3;
            decimal middleVal = 6;
            decimal endVal = 9;

            var ltr = new LTR();
            ltr.SetTriplesStart(prev2Char, prev1Char, char_, (float)startVal);
            ltr.SetTriplesMiddle(prev2Char, prev1Char, char_, (float)middleVal);
            ltr.SetTriplesEnd(prev2Char, prev1Char, char_, (float)endVal);

            // Load into editor
            byte[] testData = LTRAuto.BytesLtr(ltr);
            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Build and verify
            var (data, _) = editor.Build();
            var modifiedLtr = LTRAuto.ReadLtr(data, 0, null);

            // Verify values were set
            modifiedLtr.GetTriplesStart(prev2Char, prev1Char, char_).Should().BeApproximately((float)startVal, 0.01f);
            modifiedLtr.GetTriplesMiddle(prev2Char, prev1Char, char_).Should().BeApproximately((float)middleVal, 0.01f);
            modifiedLtr.GetTriplesEnd(prev2Char, prev1Char, char_).Should().BeApproximately((float)endVal, 0.01f);
        }

        // ============================================================================
        // NAME GENERATION TESTS
        // ============================================================================

        [Fact]
        public void TestLtrEditorGenerateName()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:236-252
            // Original: def test_ltr_editor_generate_name(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Generate name via reflection
            var generateMethod = editor.GetType().GetMethod("GenerateName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            generateMethod?.Invoke(editor, null);

            // Verify generated name is displayed
            var lineEditField = editor.GetType().GetField("_lineEditGeneratedName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (lineEditField?.GetValue(editor) is Avalonia.Controls.TextBox textBox)
            {
                textBox.Text.Should().NotBeNullOrEmpty();
                
                // Verify it was generated from LTR
                var ltrField = editor.GetType().GetField("_ltr", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (ltrField?.GetValue(editor) is LTR ltr)
                {
                    // Generate again to verify consistency
                    string expectedName = ltr.Generate();
                    // Names may differ due to randomness, but should be non-empty
                    expectedName.Should().NotBeNullOrEmpty();
                }
            }
        }

        [Fact]
        public void TestLtrEditorGenerateMultipleNames()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:254-269
            // Original: def test_ltr_editor_generate_multiple_names(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Generate multiple names
            var generatedNames = new List<string>();
            var generateMethod = editor.GetType().GetMethod("GenerateName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lineEditField = editor.GetType().GetField("_lineEditGeneratedName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            for (int i = 0; i < 10; i++)
            {
                generateMethod?.Invoke(editor, null);
                if (lineEditField?.GetValue(editor) is Avalonia.Controls.TextBox textBox)
                {
                    generatedNames.Add(textBox.Text);
                }
            }

            // Verify all names were generated (may be different due to randomness)
            generatedNames.Should().NotBeEmpty();
            generatedNames.All(n => !string.IsNullOrEmpty(n)).Should().BeTrue();
        }

        // ============================================================================
        // TABLE MANIPULATIONS
        // ============================================================================

        [Fact]
        public void TestLtrEditorTableRowAddRemoveSingles()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:275-291
            // Original: def test_ltr_editor_table_row_add_remove_singles(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Get initial count via reflection
            var singlesDataField = editor.GetType().GetField("_singlesData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (singlesDataField?.GetValue(editor) is System.Collections.ObjectModel.ObservableCollection<List<string>> singlesData)
            {
                int initialCount = singlesData.Count;

                // Add row via reflection
                var addMethod = editor.GetType().GetMethod("AddSingleRow", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                addMethod?.Invoke(editor, null);
                singlesData.Count.Should().Be(initialCount + 1);

                // Remove row via reflection
                var removeMethod = editor.GetType().GetMethod("RemoveSingleRow", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                // Select the last item first
                var tableField = editor.GetType().GetField("_tableSingles", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tableField?.GetValue(editor) is Avalonia.Controls.DataGrid table)
                {
                    table.SelectedItem = singlesData.Last();
                    removeMethod?.Invoke(editor, null);
                    singlesData.Count.Should().Be(initialCount);
                }
            }
        }

        [Fact]
        public void TestLtrEditorTableRowAddRemoveDoubles()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:293-309
            // Original: def test_ltr_editor_table_row_add_remove_doubles(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            var doublesDataField = editor.GetType().GetField("_doublesData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (doublesDataField?.GetValue(editor) is System.Collections.ObjectModel.ObservableCollection<List<string>> doublesData)
            {
                int initialCount = doublesData.Count;

                // Add row
                var addMethod = editor.GetType().GetMethod("AddDoubleRow", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                addMethod?.Invoke(editor, null);
                doublesData.Count.Should().Be(initialCount + 1);

                // Remove row
                var removeMethod = editor.GetType().GetMethod("RemoveDoubleRow", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var tableField = editor.GetType().GetField("_tableDoubles", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tableField?.GetValue(editor) is Avalonia.Controls.DataGrid table)
                {
                    table.SelectedItem = doublesData.Last();
                    removeMethod?.Invoke(editor, null);
                    doublesData.Count.Should().Be(initialCount);
                }
            }
        }

        [Fact]
        public void TestLtrEditorTableRowAddRemoveTriples()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:311-327
            // Original: def test_ltr_editor_table_row_add_remove_triples(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            var triplesDataField = editor.GetType().GetField("_triplesData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (triplesDataField?.GetValue(editor) is System.Collections.ObjectModel.ObservableCollection<List<string>> triplesData)
            {
                int initialCount = triplesData.Count;

                // Add row
                var addMethod = editor.GetType().GetMethod("AddTripleRow", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                addMethod?.Invoke(editor, null);
                triplesData.Count.Should().Be(initialCount + 1);

                // Remove row
                var removeMethod = editor.GetType().GetMethod("RemoveTripleRow", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var tableField = editor.GetType().GetField("_tableTriples", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tableField?.GetValue(editor) is Avalonia.Controls.DataGrid table)
                {
                    table.SelectedItem = triplesData.Last();
                    removeMethod?.Invoke(editor, null);
                    triplesData.Count.Should().Be(initialCount);
                }
            }
        }

        // ============================================================================
        // SAVE/LOAD ROUNDTRIP VALIDATION TESTS
        // ============================================================================

        [Fact]
        public void TestLtrEditorSaveLoadRoundtripIdentity()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:333-371
            // Original: def test_ltr_editor_save_load_roundtrip_identity(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Set some values
            string char_ = "A";
            var ltr = new LTR();
            ltr.SetSinglesStart(char_, 10);
            ltr.SetSinglesMiddle(char_, 20);
            ltr.SetSinglesEnd(char_, 30);

            // Load into editor
            byte[] testData = LTRAuto.BytesLtr(ltr);
            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Save
            var (data1, _) = editor.Build();
            var savedLtr1 = LTRAuto.ReadLtr(data1, 0, null);

            // Load saved data
            editor.Load("test.ltr", "test", ResourceType.LTR, data1);

            // Verify modifications preserved
            var ltrField = editor.GetType().GetField("_ltr", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ltrField?.GetValue(editor) is LTR loadedLtr)
            {
                loadedLtr.GetSinglesStart(char_).Should().BeApproximately(10, 0.01f);
                loadedLtr.GetSinglesMiddle(char_).Should().BeApproximately(20, 0.01f);
                loadedLtr.GetSinglesEnd(char_).Should().BeApproximately(30, 0.01f);
            }

            // Save again
            var (data2, _) = editor.Build();
            var savedLtr2 = LTRAuto.ReadLtr(data2, 0, null);

            // Verify second save matches first
            savedLtr2.GetSinglesStart(char_).Should().BeApproximately(savedLtr1.GetSinglesStart(char_), 0.01f);
            savedLtr2.GetSinglesMiddle(char_).Should().BeApproximately(savedLtr1.GetSinglesMiddle(char_), 0.01f);
            savedLtr2.GetSinglesEnd(char_).Should().BeApproximately(savedLtr1.GetSinglesEnd(char_), 0.01f);
        }

        [Fact]
        public void TestLtrEditorMultipleSaveLoadCycles()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:373-409
            // Original: def test_ltr_editor_multiple_save_load_cycles(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            string char_ = "B";

            // Perform multiple cycles
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Modify
                var ltr = new LTR();
                ltr.SetSinglesStart(char_, 10 + cycle);
                ltr.SetSinglesMiddle(char_, 20 + cycle);
                ltr.SetSinglesEnd(char_, 30 + cycle);

                // Load into editor
                byte[] testData = LTRAuto.BytesLtr(ltr);
                editor.Load("test.ltr", "test", ResourceType.LTR, testData);

                // Save
                var (data, _) = editor.Build();
                var savedLtr = LTRAuto.ReadLtr(data, 0, null);

                // Verify
                savedLtr.GetSinglesStart(char_).Should().BeApproximately(10 + cycle, 0.01f);
                savedLtr.GetSinglesMiddle(char_).Should().BeApproximately(20 + cycle, 0.01f);
                savedLtr.GetSinglesEnd(char_).Should().BeApproximately(30 + cycle, 0.01f);

                // Load back
                editor.Load("test.ltr", "test", ResourceType.LTR, data);

                // Verify loaded
                var ltrField = editor.GetType().GetField("_ltr", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (ltrField?.GetValue(editor) is LTR loadedLtr)
                {
                    loadedLtr.GetSinglesStart(char_).Should().BeApproximately(10 + cycle, 0.01f);
                    loadedLtr.GetSinglesMiddle(char_).Should().BeApproximately(20 + cycle, 0.01f);
                    loadedLtr.GetSinglesEnd(char_).Should().BeApproximately(30 + cycle, 0.01f);
                }
            }
        }

        // ============================================================================
        // UI FEATURE TESTS
        // ============================================================================

        [Fact]
        public void TestLtrEditorTableSorting()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:415-425
            // Original: def test_ltr_editor_table_sorting(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Verify sorting is enabled
            var tableSinglesField = editor.GetType().GetField("_tableSingles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tableDoublesField = editor.GetType().GetField("_tableDoubles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tableTriplesField = editor.GetType().GetField("_tableTriples", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (tableSinglesField?.GetValue(editor) is Avalonia.Controls.DataGrid singlesTable)
            {
                singlesTable.CanUserSortColumns.Should().BeTrue();
            }
            if (tableDoublesField?.GetValue(editor) is Avalonia.Controls.DataGrid doublesTable)
            {
                doublesTable.CanUserSortColumns.Should().BeTrue();
            }
            if (tableTriplesField?.GetValue(editor) is Avalonia.Controls.DataGrid triplesTable)
            {
                triplesTable.CanUserSortColumns.Should().BeTrue();
            }
        }

        [Fact]
        public void TestLtrEditorAutoFitColumns()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:427-440
            // Original: def test_ltr_editor_auto_fit_columns(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Toggle auto-fit on
            editor.ToggleAutoFitColumns(true);
            var autoResizeField = editor.GetType().GetField("_autoResizeEnabled", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (autoResizeField?.GetValue(editor) is bool enabled)
            {
                enabled.Should().BeTrue();
            }

            // Toggle auto-fit off
            editor.ToggleAutoFitColumns(false);
            if (autoResizeField?.GetValue(editor) is bool disabled)
            {
                disabled.Should().BeFalse();
            }
        }

        [Fact]
        public void TestLtrEditorAlternateRowColors()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:442-458
            // Original: def test_ltr_editor_alternate_row_colors(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Get initial state - check if alternating row style is applied
            var tableSinglesField = editor.GetType().GetField("_tableSingles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tableSinglesField?.GetValue(editor) is Avalonia.Controls.DataGrid table)
            {
                // Get the _alternateRowColorsEnabled field to check initial state
                var alternateRowColorsEnabledField = editor.GetType().GetField("_alternateRowColorsEnabled",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                bool initialState = alternateRowColorsEnabledField?.GetValue(editor) is bool initial && initial;

                // Verify initial state is false
                initialState.Should().BeFalse("Alternate row colors should be disabled initially");

                // Toggle alternate row colors
                editor.ToggleAlternateRowColors();

                // Verify state changed
                bool newState = alternateRowColorsEnabledField?.GetValue(editor) is bool ns && ns;
                newState.Should().BeTrue("Alternate row colors should be enabled after toggle");
                newState.Should().NotBe(initialState, "State should have changed");

                // Verify style was added to table
                table.Styles.Count.Should().BeGreaterThan(0, "Style should be added to table");

                // Toggle back
                editor.ToggleAlternateRowColors();

                // Verify state changed back
                bool finalState = alternateRowColorsEnabledField?.GetValue(editor) is bool fs && fs;
                finalState.Should().BeFalse("Alternate row colors should be disabled after second toggle");
                finalState.Should().Be(initialState, "State should match initial state");
            }
        }

        [Fact]
        public void TestLtrEditorComboBoxPopulation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:460-478
            // Original: def test_ltr_editor_combo_box_population(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Verify combo boxes have items
            var comboBoxSingleField = editor.GetType().GetField("_comboBoxSingleChar", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (comboBoxSingleField?.GetValue(editor) is Avalonia.Controls.ComboBox comboBox)
            {
                var items = comboBox.ItemsSource as System.Collections.IEnumerable;
                items.Should().NotBeNull();
                
                int count = 0;
                foreach (var item in items)
                {
                    count++;
                }
                count.Should().BeGreaterThan(0);
                count.Should().Be(LTR.CharacterSet.Length);
            }
        }

        // ============================================================================
        // EDGE CASES
        // ============================================================================

        [Fact]
        public void TestLtrEditorExtremeValues()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:484-504
            // Original: def test_ltr_editor_extreme_values(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            string char_ = "A";
            var ltr = new LTR();
            // Test extreme values
            ltr.SetSinglesStart(char_, 0);
            ltr.SetSinglesMiddle(char_, 1.0f);
            ltr.SetSinglesEnd(char_, 0.5f);

            // Load into editor
            byte[] testData = LTRAuto.BytesLtr(ltr);
            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Build and verify
            var (data, _) = editor.Build();
            var loadedLtr = LTRAuto.ReadLtr(data, 0, null);

            // Verify values were set
            loadedLtr.GetSinglesStart(char_).Should().BeApproximately(0, 0.01f);
            loadedLtr.GetSinglesMiddle(char_).Should().BeApproximately(1.0f, 0.01f);
            loadedLtr.GetSinglesEnd(char_).Should().BeApproximately(0.5f, 0.01f);
        }

        [Fact]
        public void TestLtrEditorEmptyTables()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:506-519
            // Original: def test_ltr_editor_empty_tables(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Verify tables are populated after new()
            var singlesDataField = editor.GetType().GetField("_singlesData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var doublesDataField = editor.GetType().GetField("_doublesData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var triplesDataField = editor.GetType().GetField("_triplesData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (singlesDataField?.GetValue(editor) is System.Collections.ObjectModel.ObservableCollection<List<string>> singlesData)
            {
                singlesData.Count.Should().BeGreaterThan(0);
            }
            if (doublesDataField?.GetValue(editor) is System.Collections.ObjectModel.ObservableCollection<List<string>> doublesData)
            {
                doublesData.Count.Should().BeGreaterThan(0);
            }
            if (triplesDataField?.GetValue(editor) is System.Collections.ObjectModel.ObservableCollection<List<string>> triplesData)
            {
                triplesData.Count.Should().BeGreaterThan(0);
            }

            // Verify LTR object is not None
            var ltrField = editor.GetType().GetField("_ltr", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ltrField?.GetValue(editor).Should().NotBeNull();
        }

        // ============================================================================
        // COMBINATION TESTS
        // ============================================================================

        [Fact]
        public void TestLtrEditorManipulateAllCharacterTypes()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py:525-580
            // Original: def test_ltr_editor_manipulate_all_character_types(qtbot, installation):
            var editor = new LTREditor(null, null);
            editor.New();

            // Set single
            string char_ = "A";
            var ltr = new LTR();
            ltr.SetSinglesStart(char_, 10);
            ltr.SetSinglesMiddle(char_, 20);
            ltr.SetSinglesEnd(char_, 30);

            // Set double
            string prevChar = "A";
            string char2 = "B";
            ltr.SetDoublesStart(prevChar, char2, 5);
            ltr.SetDoublesMiddle(prevChar, char2, 10);
            ltr.SetDoublesEnd(prevChar, char2, 15);

            // Set triple
            string prev2 = "A";
            string prev1 = "B";
            string char3 = "C";
            ltr.SetTriplesStart(prev2, prev1, char3, 3);
            ltr.SetTriplesMiddle(prev2, prev1, char3, 6);
            ltr.SetTriplesEnd(prev2, prev1, char3, 9);

            // Load into editor
            byte[] testData = LTRAuto.BytesLtr(ltr);
            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Build and verify all values
            var (data, _) = editor.Build();
            var modifiedLtr = LTRAuto.ReadLtr(data, 0, null);

            modifiedLtr.GetSinglesStart(char_).Should().BeApproximately(10, 0.01f);
            modifiedLtr.GetDoublesStart(prevChar, char2).Should().BeApproximately(5, 0.01f);
            modifiedLtr.GetTriplesStart(prev2, prev1, char3).Should().BeApproximately(3, 0.01f);
        }

        // ============================================================================
        // ADDITIONAL TESTS
        // ============================================================================

        [Fact]
        public void TestLtrEditorLoadExistingFile()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py
            var editor = new LTREditor(null, null);

            // Create minimal LTR data
            var ltr = new LTR();
            ltr.SetSinglesStart("a", 0.1f);
            ltr.SetSinglesMiddle("a", 0.2f);
            ltr.SetSinglesEnd("a", 0.3f);
            byte[] testData = LTRAuto.BytesLtr(ltr);

            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Verify content loaded
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestLtrEditorSaveLoadRoundtrip()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py
            var editor = new LTREditor(null, null);
            editor.New();

            // Test save/load roundtrip
            var (data, _) = editor.Build();
            data.Should().NotBeNull();

            var editor2 = new LTREditor(null, null);
            editor2.Load("test.ltr", "test", ResourceType.LTR, data);
            var (data2, _) = editor2.Build();
            data2.Should().Equal(data);
        }
    }
}

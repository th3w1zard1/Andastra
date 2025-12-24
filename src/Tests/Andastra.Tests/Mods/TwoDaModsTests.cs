using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.TwoDA;
using FluentAssertions;
using Xunit;
using TwoDAFile = Andastra.Parsing.Formats.TwoDA.TwoDA;

namespace Andastra.Parsing.Tests.Mods
{

    /// <summary>
    /// Tests for 2DA modification functionality
    /// 1:1 port from tests/tslpatcher/test_mods.py (TestManipulate2DA)
    /// </summary>
    public class TwoDaModsTests
    {
        #region Change Row Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_ExistingRowByIndex()
        {
            // Python test: test_change_existing_rowindex
            // Modifies row at index 1, changes Col1 from "d" to "X"

            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("X") } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "X");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "e");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_ExistingRowByLabel()
        {
            // Python test: test_change_existing_rowlabel
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_LABEL, "1"),
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("X") } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "X");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "e");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_ExistingRowByLabelColumn()
        {
            // Python test: test_change_existing_labelindex
            var twoda = new TwoDAFile(new List<string> { "label", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "label", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "label", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.LABEL_COLUMN, "d"),
                new Dictionary<string, RowValue> { { "Col2", new RowValueConstant("X") } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("label").Should().BeEquivalentTo("a", "d");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "X");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_AssignFromTLKMemory()
        {
            // Python test: test_change_assign_tlkmemory
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory { MemoryStr = { [0] = 0, [1] = 1 } };
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 0),
                new Dictionary<string, RowValue> { { "Col1", new RowValueTLKMemory(0) } }));
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue> { { "Col1", new RowValueTLKMemory(1) } }));

            var writer = new TwoDABinaryWriter(twoda);
            byte[] twodaBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(twodaBytes, memory, logger, BioWareGame.K2);
            TwoDAFile patchedTwoda = new TwoDABinaryReader(patchedBytes).Load();

            patchedTwoda.GetColumn("Col1").Should().BeEquivalentTo("0", "1");
            patchedTwoda.GetColumn("Col2").Should().BeEquivalentTo("b", "e");
            patchedTwoda.GetColumn("Col3").Should().BeEquivalentTo("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_AssignFrom2DAMemory()
        {
            // Python test: test_change_assign_2damemory
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory { Memory2DA = { [0] = "mem0", [1] = "mem1" } };
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 0),
                new Dictionary<string, RowValue> { { "Col1", new RowValue2DAMemory(0) } }));
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue> { { "Col1", new RowValue2DAMemory(1) } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("mem0", "mem1");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "e");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_AssignHigh()
        {
            // Python test: test_change_assign_high
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", " " }, { "Col2", "3" }, { "Col3", "5" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "2" }, { "Col2", "4" }, { "Col3", "6" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 0),
                new Dictionary<string, RowValue> { { "Col1", new RowValueHigh("Col1") } }));
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 0),
                new Dictionary<string, RowValue> { { "Col2", new RowValueHigh("Col2") } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("3", "2");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("5", "4");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("5", "6");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Store2DAMemoryRowIndex()
        {
            // Python test: test_set_2damemory_rowindex
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, RowValue> { { 5, new RowValueRowIndex() } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            memory.Memory2DA[5].Should().Be("1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Store2DAMemoryRowLabel()
        {
            // Python test: test_set_2damemory_rowlabel
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("r1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, RowValue> { { 5, new RowValueRowLabel() } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            memory.Memory2DA[5].Should().Be("r1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Store2DAMemoryColumnCell()
        {
            // Python test: test_set_2damemory_columnlabel
            var twoda = new TwoDAFile(new List<string> { "label", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "label", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "label", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory { Memory2DA = { [5] = "d" } };
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, RowValue> { { 5, new RowValueRowCell("label") } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            memory.Memory2DA[5].Should().Be("d");
        }

        #endregion

        #region Add Row Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_UseMaxRowLabel()
        {
            // Python test: test_add_rowlabel_use_maxrowlabel
            var twoda = new TwoDAFile(new List<string> { "Col1" });
            twoda.AddRow("0", new Dictionary<string, object>());

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, null, new Dictionary<string, RowValue>()));
            config.Modifiers.Add(new AddRow2DA("", null, null, new Dictionary<string, RowValue>()));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(3);
            twoda.GetLabel(0).Should().Be("0");
            twoda.GetLabel(1).Should().Be("1");
            twoda.GetLabel(2).Should().Be("2");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_UseConstantLabel()
        {
            // Python test: test_add_rowlabel_use_constant
            var twoda = new TwoDAFile(new List<string> { "Col1" });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "r1", new Dictionary<string, RowValue>()));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(1);
            twoda.GetLabel(0).Should().Be("r1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ExclusiveColumnNotExists()
        {
            // Python test: test_add_exclusive_notexists
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", "Col1", "2",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("g") },
                { "Col2", new RowValueConstant("h") },
                { "Col3", new RowValueConstant("i") }
                }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(3);
            twoda.GetLabel(2).Should().Be("2");
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "d", "g");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "e", "h");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("c", "f", "i");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ExclusiveColumnExists()
        {
            // Python test: test_add_exclusive_exists
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });
            twoda.AddRow("2", new Dictionary<string, object> { { "Col1", "g" }, { "Col2", "h" }, { "Col3", "i" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", "Col1", "3",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("g") },
                { "Col2", new RowValueConstant("X") },
                { "Col3", new RowValueConstant("Y") }
                }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(3);
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "d", "g");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "e", "X");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("c", "f", "Y");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ExclusiveColumnNone()
        {
            // Python test: test_add_exclusive_none
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", "", "2",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("g") },
                { "Col2", new RowValueConstant("h") },
                { "Col3", new RowValueConstant("i") }
                }));
            config.Modifiers.Add(new AddRow2DA("", null, "3",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("j") },
                { "Col2", new RowValueConstant("k") },
                { "Col3", new RowValueConstant("l") }
                }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(4);
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "d", "g", "j");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "e", "h", "k");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("c", "f", "i", "l");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_AssignHigh()
        {
            // Python test: test_add_assign_high
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "1" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "2" }, { "Col2", "e" }, { "Col3", "f" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", "", "2",
                new Dictionary<string, RowValue> { { "Col1", new RowValueHigh("Col1") } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("1", "2", "3");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_AssignFromTLKMemory()
        {
            // Python test: test_add_assign_tlkmemory
            var twoda = new TwoDAFile(new List<string> { "Col1" });

            var memory = new PatcherMemory();
            memory.MemoryStr[0] = 5;
            memory.MemoryStr[1] = 6;
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "0",
                new Dictionary<string, RowValue> { { "Col1", new RowValueTLKMemory(0) } }));
            config.Modifiers.Add(new AddRow2DA("", null, "1",
                new Dictionary<string, RowValue> { { "Col1", new RowValueTLKMemory(1) } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("5", "6");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_AssignFrom2DAMemory()
        {
            // Python test: test_add_assign_2damemory
            var twoda = new TwoDAFile(new List<string> { "Col1" });

            var memory = new PatcherMemory();
            memory.Memory2DA[0] = "5";
            memory.Memory2DA[1] = "6";
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "0",
                new Dictionary<string, RowValue> { { "Col1", new RowValue2DAMemory(0) } }));
            config.Modifiers.Add(new AddRow2DA("", null, "1",
                new Dictionary<string, RowValue> { { "Col1", new RowValue2DAMemory(1) } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("5", "6");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Store2DAMemoryRowIndex()
        {
            // Python test: test_add_2damemory_rowindex
            var twoda = new TwoDAFile(new List<string> { "Col1" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "X" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", "Col1", "1",
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("X") } },
                new Dictionary<int, RowValue> { { 5, new RowValueRowIndex() } }));
            config.Modifiers.Add(new AddRow2DA("", null, "2",
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("Y") } },
                new Dictionary<int, RowValue> { { 6, new RowValueRowIndex() } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(2);
            twoda.GetColumn("Col1").Should().BeEquivalentTo("X", "Y");
            memory.Memory2DA[5].Should().Be("0");
            memory.Memory2DA[6].Should().Be("1");
        }

        #endregion

        #region Copy Row Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ByRowIndex()
        {
            // Python test: test_copy_existing_rowindex
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), null, null,
                new Dictionary<string, RowValue> { { "Col2", new RowValueConstant("X") } }));

            var writer = new TwoDABinaryWriter(twoda);
            byte[] twodaBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(twodaBytes, memory, logger, BioWareGame.K2);
            TwoDAFile patchedTwoda = new TwoDABinaryReader(patchedBytes).Load();

            patchedTwoda.GetHeight().Should().Be(3);
            patchedTwoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c", "a");
            patchedTwoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d", "X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ByRowLabel()
        {
            // Python test: test_copy_existing_rowlabel
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_LABEL, "1"), null, null,
                new Dictionary<string, RowValue> { { "Col2", new RowValueConstant("X") } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(3);
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d", "X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ExclusiveColumnNotExists()
        {
            // Python test: test_copy_exclusive_notexists
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), "Col1", null,
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("c") },
                { "Col2", new RowValueConstant("d") }
                }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(2);
            twoda.GetLabel(1).Should().Be("1");
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ExclusiveColumnExists()
        {
            // Python test: test_copy_exclusive_exists
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), "Col1", null,
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("a") },
                { "Col2", new RowValueConstant("X") }
                }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(1);
            twoda.GetLabel(0).Should().Be("0");
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ExclusiveColumnNone()
        {
            // Python test: test_copy_exclusive_none
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), null, null,
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("c") },
                { "Col2", new RowValueConstant("d") }
                }));
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), "", "r2",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("e") },
                { "Col2", new RowValueConstant("f") }
                }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(3);
            twoda.GetLabel(1).Should().Be("1");
            twoda.GetLabel(2).Should().Be("r2");
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c", "e");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_SetNewRowLabel()
        {
            // Python test: test_copy_set_newrowlabel
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), null, "r2",
                new Dictionary<string, RowValue>()));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetLabel(2).Should().Be("r2");
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c", "a");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d", "b");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_AssignHigh()
        {
            // Python test: test_copy_assign_high
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "1" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "2" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), null, null,
                new Dictionary<string, RowValue> { { "Col2", new RowValueHigh("Col2") } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetHeight().Should().Be(3);
            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c", "a");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("1", "2", "3");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_AssignFromTLKMemory()
        {
            // Python test: test_copy_assign_tlkmemory
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "1" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "2" } });

            var memory = new PatcherMemory { MemoryStr = { [0] = 5 } };
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), null, null,
                new Dictionary<string, RowValue> { { "Col2", new RowValueTLKMemory(0) } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c", "a");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("1", "2", "5");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_AssignFrom2DAMemory()
        {
            // Python test: test_copy_assign_2damemory
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "1" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "2" } });

            var memory = new PatcherMemory { Memory2DA = { [0] = "5" } };
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), null, null,
                new Dictionary<string, RowValue> { { "Col2", new RowValue2DAMemory(0) } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c", "a");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("1", "2", "5");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Store2DAMemoryRowIndex()
        {
            // Python test: test_copy_2damemory_rowindex
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), null, null,
                new Dictionary<string, RowValue>(),
                new Dictionary<int, RowValue> { { 5, new RowValueRowIndex() } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c", "a");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d", "b");
            memory.Memory2DA[5].Should().Be("2");
        }

        #endregion

        #region Add Column Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_Empty()
        {
            // Python test: test_addcolumn_empty
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue>(), new Dictionary<int, string>()));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("", "");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_WithDefault()
        {
            // Python test: test_addcolumn_default
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "X", new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue>(), new Dictionary<int, string>()));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("X", "X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowIndexConstant()
        {
            // Python test: test_addcolumn_rowindex_constant
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "",
                new Dictionary<int, RowValue> { { 0, new RowValueConstant("X") } },
                new Dictionary<string, RowValue>(), new Dictionary<int, string>()));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("X", "");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowLabel2DAMemory()
        {
            // Python test: test_addcolumn_rowlabel_2damemory
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory { Memory2DA = { [5] = "ABC" } };
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue> { { "1", new RowValue2DAMemory(5) } },
                new Dictionary<int, string>()));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("", "ABC");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowLabelTLKMemory()
        {
            // Python test: test_addcolumn_rowlabel_tlkmemory
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory { MemoryStr = { [5] = 123 } };
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue> { { "1", new RowValueTLKMemory(5) } },
                new Dictionary<int, string>()));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("", "123");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_Store2DAMemoryIndex()
        {
            // Python test: test_addcolumn_2damemory_index
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "",
                new Dictionary<int, RowValue>
                {
                { 0, new RowValueConstant("X") },
                { 1, new RowValueConstant("Y") }
                },
                new Dictionary<string, RowValue>(),
                new Dictionary<int, string> { { 0, "I0" } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("X", "Y");
            memory.Memory2DA[0].Should().Be("X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_Store2DAMemoryLine()
        {
            // Python test: test_addcolumn_2damemory_line
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "",
                new Dictionary<int, RowValue>
                {
                { 0, new RowValueConstant("X") },
                { 1, new RowValueConstant("Y") }
                },
                new Dictionary<string, RowValue>(),
                new Dictionary<int, string> { { 0, "L1" } }));

            config.Apply(twoda, memory, logger, BioWareGame.K2);

            twoda.GetColumn("Col1").Should().BeEquivalentTo("a", "c");
            twoda.GetColumn("Col2").Should().BeEquivalentTo("b", "d");
            twoda.GetColumn("Col3").Should().BeEquivalentTo("X", "Y");
            memory.Memory2DA[0].Should().Be("Y");
        }

        #endregion
    }
}

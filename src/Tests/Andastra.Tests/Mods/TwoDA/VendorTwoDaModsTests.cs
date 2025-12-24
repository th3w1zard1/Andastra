using System.Collections.Generic;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.TwoDA;
using Xunit;
using TwoDAFile = Andastra.Parsing.Formats.TwoDA.TwoDA;

namespace Andastra.Parsing.Tests.Mods.TwoDA
{

    /// <summary>
    /// Port of vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/mods/test_vendor_twoda_mods.py.
    /// </summary>
    public class VendorTwoDaModsTests
    {
        private static TwoDAFile CreateBaseTwoDa()
        {
            var twoda = new TwoDAFile();
            twoda.AddColumn("C1");
            twoda.AddColumn("C2");
            twoda.AddColumn("C3");
            int row0 = twoda.AddRow("l0");
            int row1 = twoda.AddRow("l1");
            twoda.SetCellString(row0, "C1", "a0");
            twoda.SetCellString(row0, "C2", string.Empty);
            twoda.SetCellString(row0, "C3", string.Empty);
            twoda.SetCellString(row1, "C1", "a1");
            twoda.SetCellString(row1, "C2", string.Empty);
            twoda.SetCellString(row1, "C3", string.Empty);
            return twoda;
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_ShouldAppendHeader()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new AddColumn2DA("add", "NewColumn", string.Empty, new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());

            modifier.Apply(twoda, memory);

            List<string> headers = twoda.GetHeaders();
            Assert.Equal(4, headers.Count);
            Assert.Equal("NewColumn", headers[3]);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_ShouldPopulateDefaultValues()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new AddColumn2DA("add", "NewColumn", "xyz", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());

            modifier.Apply(twoda, memory);

            Assert.Equal("xyz", twoda.GetCellString(0, "NewColumn"));
            Assert.Equal("xyz", twoda.GetCellString(1, "NewColumn"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ShouldAppendWhenNotExclusive()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new AddRow2DA(
                "add",
                null,
                null,
                new Dictionary<string, RowValue>
                {
                    { "C1", new RowValueConstant("a") },
                    { "C2", new RowValueConstant("b") },
                    { "C3", new RowValueConstant("c") }
                });

            modifier.Apply(twoda, memory);

            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal("2", twoda.GetLabel(2));
            Assert.Equal("a", twoda.GetCellString(2, "C1"));
            Assert.Equal("b", twoda.GetCellString(2, "C2"));
            Assert.Equal("c", twoda.GetCellString(2, "C3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ShouldRespectRowLabel()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new AddRow2DA(
                "add",
                null,
                "somelabel",
                new Dictionary<string, RowValue>
                {
                    { "C1", new RowValueConstant("a") },
                    { "C2", new RowValueConstant("b") },
                    { "C3", new RowValueConstant("c") }
                });

            modifier.Apply(twoda, memory);

            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal("somelabel", twoda.GetLabel(2));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ShouldEditWhenExclusiveValueExists()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();
            twoda.SetCellString(0, "C1", "a");

            var modifier = new AddRow2DA(
                "add",
                "C1",
                null,
                new Dictionary<string, RowValue>
                {
                    { "C1", new RowValueConstant("a") },
                    { "C2", new RowValueConstant("b") },
                    { "C3", new RowValueConstant("c") }
                });

            modifier.Apply(twoda, memory);

            Assert.Equal(2, twoda.GetHeight());
            Assert.Equal("a", twoda.GetCellString(0, "C1"));
            Assert.Equal("b", twoda.GetCellString(0, "C2"));
            Assert.Equal("c", twoda.GetCellString(0, "C3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ShouldAddWhenExclusiveValueMissing()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new AddRow2DA(
                "add",
                "C1",
                null,
                new Dictionary<string, RowValue>
                {
                    { "C1", new RowValueConstant("a") },
                    { "C2", new RowValueConstant("b") },
                    { "C3", new RowValueConstant("c") }
                });

            modifier.Apply(twoda, memory);

            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal("a", twoda.GetCellString(2, "C1"));
            Assert.Equal("b", twoda.GetCellString(2, "C2"));
            Assert.Equal("c", twoda.GetCellString(2, "C3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ShouldStoreRowIndex()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new AddRow2DA(
                "add",
                null,
                null,
                new Dictionary<string, RowValue>
                {
                    { "C1", new RowValueConstant("a") },
                    { "C2", new RowValueConstant("b") },
                    { "C3", new RowValueConstant("c") }
                },
                new Dictionary<int, RowValue> { { 30, new RowValueRowIndex() } });

            modifier.Apply(twoda, memory);

            Assert.Equal("2", memory.Memory2DA[30]);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ShouldAddCopyWhenExclusiveIsNull()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new CopyRow2DA(
                "copy",
                new Target(TargetType.ROW_INDEX, 1),
                null,
                null,
                new Dictionary<string, RowValue> { { "C1", new RowValueConstant("a") } },
                new Dictionary<int, RowValue>());

            modifier.Apply(twoda, memory);

            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal("2", twoda.GetLabel(2));
            Assert.Equal("a", twoda.GetCellString(2, "C1"));
            Assert.Equal(string.Empty, twoda.GetCellString(2, "C2"));
            Assert.Equal(string.Empty, twoda.GetCellString(2, "C3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ShouldEditWhenExclusiveMatchesExistingRow()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();
            twoda.SetCellString(0, "C1", "a");
            twoda.SetCellString(1, "C2", "x");
            twoda.SetCellString(1, "C3", "y");

            var modifier = new CopyRow2DA(
                "copy",
                new Target(TargetType.ROW_INDEX, 1),
                "C1",
                null,
                new Dictionary<string, RowValue>
                {
                    { "C1", new RowValueConstant("a") },
                    { "C2", new RowValueConstant("00") }
                },
                new Dictionary<int, RowValue>());

            modifier.Apply(twoda, memory);

            Assert.Equal(2, twoda.GetHeight());
            Assert.Equal("l0", twoda.GetLabel(0));
            Assert.Equal("a", twoda.GetCellString(0, "C1"));
            Assert.Equal("00", twoda.GetCellString(0, "C2"));
            Assert.Equal("y", twoda.GetCellString(0, "C3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_ShouldUpdateCells()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new ChangeRow2DA(
                "change",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>
                {
                    { "C1", new RowValueConstant("a") },
                    { "C2", new RowValueConstant("b") },
                    { "C3", new RowValueConstant("c") }
                });

            modifier.Apply(twoda, memory);

            Assert.Equal(2, twoda.GetHeight());
            Assert.Equal("a", twoda.GetCellString(1, "C1"));
            Assert.Equal("b", twoda.GetCellString(1, "C2"));
            Assert.Equal("c", twoda.GetCellString(1, "C3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_ShouldStoreRowIndex()
        {
            var memory = new PatcherMemory();
            TwoDAFile twoda = CreateBaseTwoDa();

            var modifier = new ChangeRow2DA(
                "change",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>
                {
                    { "C1", new RowValueConstant("a") },
                    { "C2", new RowValueConstant("b") },
                    { "C3", new RowValueConstant("c") }
                },
                new Dictionary<int, RowValue> { { 30, new RowValueRowIndex() } });

            modifier.Apply(twoda, memory);

            Assert.Equal("1", memory.Memory2DA[30]);
        }
    }
}


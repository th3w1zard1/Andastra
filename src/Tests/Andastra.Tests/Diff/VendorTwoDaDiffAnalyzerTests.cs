using System.Collections.Generic;
using Andastra.Parsing.Diff;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Mods.TwoDA;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Diff
{

    /// <summary>
    /// Port of vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/diff/test_vendor_twoda_diff.py: TestVendorTwoDADiffAnalyzer.
    /// </summary>
    public class VendorTwoDaDiffAnalyzerTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Analyze_ShouldFindNewColumn()
        {
            var older = new TwoDA();
            older.AddColumn("A");
            int olderRow0 = older.AddRow("0");
            int olderRow1 = older.AddRow("1");
            older.SetCellString(olderRow0, "A", "a0");
            older.SetCellString(olderRow1, "A", "a1");

            var newer = new TwoDA();
            newer.AddColumn("A");
            newer.AddColumn("B");
            int newerRow0 = newer.AddRow("0");
            int newerRow1 = newer.AddRow("1");
            newer.SetCellString(newerRow0, "A", "a0");
            newer.SetCellString(newerRow1, "A", "a1");
            newer.SetCellString(newerRow0, "B", "b1");

            byte[] olderBytes = new TwoDABinaryWriter(older).Write();
            byte[] newerBytes = new TwoDABinaryWriter(newer).Write();

            var analyzer = new TwoDaDiffAnalyzer();
            Modifications2DA modifications = analyzer.Analyze(olderBytes, newerBytes, "test.2da");

            modifications.Should().NotBeNull();
            modifications.Modifiers.Should().HaveCount(1);
            modifications.Modifiers[0].Should().BeOfType<AddColumn2DA>();

            var modifier = (AddColumn2DA)modifications.Modifiers[0];
            modifier.Header.Should().Be("B");
            modifier.Default.Should().Be("****");
            modifier.IndexInsert.Should().ContainKey(0);
            modifier.IndexInsert.Should().ContainKey(1);
            modifier.IndexInsert[0].Should().BeOfType<RowValueConstant>();
            modifier.IndexInsert[1].Should().BeOfType<RowValueConstant>();
            ((RowValueConstant)modifier.IndexInsert[0]).String.Should().Be("b1");
            ((RowValueConstant)modifier.IndexInsert[1]).String.Should().Be(string.Empty);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Analyze_ShouldFindNewRow()
        {
            var older = new TwoDA();
            older.AddColumn("A");
            older.AddColumn("B");
            int olderRow0 = older.AddRow("0");
            int olderRow1 = older.AddRow("1");
            older.SetCellString(olderRow0, "A", "a0");
            older.SetCellString(olderRow1, "A", "a1");
            older.SetCellString(olderRow1, "B", "b1");

            var newer = new TwoDA();
            newer.AddColumn("A");
            newer.AddColumn("B");
            int newerRow0 = newer.AddRow("0");
            int newerRow1 = newer.AddRow("1");
            int newerRow2 = newer.AddRow("2");
            newer.SetCellString(newerRow0, "A", "a0");
            newer.SetCellString(newerRow1, "A", "a1");
            newer.SetCellString(newerRow1, "B", "b1");
            newer.SetCellString(newerRow2, "A", "a2");
            newer.SetCellString(newerRow2, "B", "b2");

            byte[] olderBytes = new TwoDABinaryWriter(older).Write();
            byte[] newerBytes = new TwoDABinaryWriter(newer).Write();

            var analyzer = new TwoDaDiffAnalyzer();
            Modifications2DA modifications = analyzer.Analyze(olderBytes, newerBytes, "test.2da");

            modifications.Should().NotBeNull();
            modifications.Modifiers.Should().HaveCount(1);
            modifications.Modifiers[0].Should().BeOfType<AddRow2DA>();

            var modifier = (AddRow2DA)modifications.Modifiers[0];
            modifier.RowLabel.Should().Be("2");
            modifier.Cells.Should().ContainKey("A");
            modifier.Cells.Should().ContainKey("B");
            modifier.Cells["A"].Should().BeOfType<RowValueConstant>();
            modifier.Cells["B"].Should().BeOfType<RowValueConstant>();
            ((RowValueConstant)modifier.Cells["A"]).String.Should().Be("a2");
            ((RowValueConstant)modifier.Cells["B"]).String.Should().Be("b2");
        }
    }
}


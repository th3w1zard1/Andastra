using System.Collections.Generic;
using Andastra.Parsing.Diff;
using Andastra.Parsing.Formats.TwoDA;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Diff
{

    /// <summary>
    /// Tests for 2DA diff functionality
    /// Ported from tests/tslpatcher/diff/test_twoda.py
    /// </summary>
    public class TwoDaDiffTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Compare_ShouldDetectAddedRows()
        {
            var original = new TwoDA(new List<string> { "col1", "col2" });
            original.AddRow("0", new Dictionary<string, object> { { "col1", "a" }, { "col2", "b" } });

            var modified = new TwoDA(new List<string> { "col1", "col2" });
            modified.AddRow("0", new Dictionary<string, object> { { "col1", "a" }, { "col2", "b" } });
            modified.AddRow("1", new Dictionary<string, object> { { "col1", "c" }, { "col2", "d" } });

            TwoDaCompareResult result = TwoDaDiff.Compare(original, modified);

            result.AddedRows.Should().HaveCount(1);
            result.AddedRows[0].Label.Should().Be("1");
            result.AddedRows[0].Cells["col1"].Should().Be("c");
            result.AddedRows[0].Cells["col2"].Should().Be("d");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Compare_ShouldDetectChangedRows()
        {
            var original = new TwoDA(new List<string> { "col1" });
            original.AddRow("0", new Dictionary<string, object> { { "col1", "old" } });

            var modified = new TwoDA(new List<string> { "col1" });
            modified.AddRow("0", new Dictionary<string, object> { { "col1", "new" } });

            TwoDaCompareResult result = TwoDaDiff.Compare(original, modified);

            result.ChangedRows.Should().ContainKey(0);
            result.ChangedRows[0]["col1"].Should().Be("new");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Compare_ShouldDetectAddedColumns()
        {
            var original = new TwoDA(new List<string> { "col1" });
            original.AddRow("0", new Dictionary<string, object> { { "col1", "val" } });

            var modified = new TwoDA(new List<string> { "col1", "col2" });
            modified.AddRow("0", new Dictionary<string, object> { { "col1", "val" }, { "col2", "new_col_val" } });

            TwoDaCompareResult result = TwoDaDiff.Compare(original, modified);

            result.AddedColumns.Should().Contain("col2");
            // Also check if the value in the new column is detected as a change for the existing row
            result.ChangedRows.Should().ContainKey(0);
            result.ChangedRows[0]["col2"].Should().Be("new_col_val");
        }

    }
}

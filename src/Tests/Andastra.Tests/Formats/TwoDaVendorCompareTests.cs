using System.Collections.Generic;
using Andastra.Parsing.Formats.TwoDA;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Port of vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/diff/test_vendor_twoda_diff.py: TestVendorTwoDAComparison.
    /// </summary>
    public class TwoDaVendorCompareTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Compare_ShouldReturnFalse_WhenColumnCountsDiffer()
        {
            var older = new TwoDA(new List<string> { "ABC", "123" });
            older.AddRow("0");

            var newer = new TwoDA(new List<string> { "ABC" });
            newer.AddRow("0");

            bool result = older.Compare(newer);

            Assert.False(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Compare_ShouldReturnFalse_WhenRowCountsDiffer()
        {
            var older = new TwoDA(new List<string> { "A", "B" });
            older.AddRow("0");
            older.AddRow("1");

            var newer = new TwoDA(new List<string> { "A", "B" });
            newer.AddRow("0");

            bool result = older.Compare(newer);

            Assert.False(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Compare_ShouldReturnFalse_WhenCellsDiffer()
        {
            var older = new TwoDA(new List<string> { "A", "B" });
            older.AddRow("0");
            older.AddRow("1");

            var newer = new TwoDA(new List<string> { "A", "B" });
            newer.AddRow("0");
            newer.AddRow("1");
            newer.SetCellString(0, "A", "asdf");

            bool result = older.Compare(newer);

            Assert.False(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Compare_ShouldReturnTrue_WhenTablesMatch()
        {
            var older = new TwoDA(new List<string> { "A", "B" });
            older.AddRow("0");
            older.AddRow("1");
            older.SetCellString(0, "A", "asdf");

            var newer = new TwoDA(new List<string> { "A", "B" });
            newer.AddRow("0");
            newer.AddRow("1");
            newer.SetCellString(0, "A", "asdf");

            bool result = older.Compare(newer);

            Assert.True(result);
        }
    }
}


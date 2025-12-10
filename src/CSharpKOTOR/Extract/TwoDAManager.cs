using System.Collections.Generic;
using CSharpKOTOR.Formats.TwoDA;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Extract
{
    public struct LookupResult2DA
    {
        public string Filepath { get; }
        public int RowIndex { get; }
        public string ColumnName { get; }
        public string Contents { get; }
        public TwoDARow EntireRow { get; }

        public LookupResult2DA(string filepath, int rowIndex, string columnName, string contents, TwoDARow entireRow)
        {
            Filepath = filepath;
            RowIndex = rowIndex;
            ColumnName = columnName;
            Contents = contents;
            EntireRow = entireRow;
        }
    }

    // Minimal TwoDA manager placeholder to match extract/twoda.py
    public class TwoDAManager
    {
        public static List<string> GetColumnNames(string dataType)
        {
            var cols = new List<string>();
            foreach (var set in TwoDARegistry.ColumnsFor(dataType).Values)
            {
                cols.AddRange(set);
            }
            return cols;
        }
    }
}


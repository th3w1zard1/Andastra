using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Extract
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

    // Minimal 2DA manager to match extract/twoda.py lookup behavior.
    public class TwoDAManager
    {
        public static List<string> GetColumnNames(string dataType)
        {
            var cols = new List<string>();
            foreach (var set in TwoDARegistry.ColumnsFor(dataType).Values)
            {
                cols.AddRange(set);
            }
            return cols.Distinct().ToList();
        }

        public static LookupResult2DA LookupInInstallation(Installation.Installation installation, string query, string dataType)
        {
            if (string.IsNullOrEmpty(query))
            {
                return default;
            }

            // Prefer K2 metadata if installation is TSL; otherwise default to K1
            bool isK2 = installation.Game == BioWareGame.TSL;
            var targets = TwoDARegistry.ColumnsFor(dataType, isK2);
            foreach (var kvp in targets)
            {
                string fileKey = kvp.Key;
                var columns = kvp.Value;
                string filename = fileKey.Split('-')[0]; // normalize suffixed keys
                string resname = Path.GetFileNameWithoutExtension(filename);

                var res = installation.Resources.LookupResource(resname, ResourceType.TwoDA);
                if (res == null || res.Data == null)
                {
                    continue;
                }

                var twoda = new TwoDABinaryReader(res.Data).Load();
                for (int rowIndex = 0; rowIndex < twoda.GetHeight(); rowIndex++)
                {
                    var row = twoda.GetRow(rowIndex);
                    foreach (string col in columns)
                    {
                        string cell;
                        try
                        {
                            cell = row.GetString(col);
                        }
                        catch
                        {
                            continue;
                        }
                        if (cell == query)
                        {
                            return new LookupResult2DA(filename, rowIndex, col, cell ?? string.Empty, row);
                        }
                    }
                }
            }

            return default;
        }
    }
}

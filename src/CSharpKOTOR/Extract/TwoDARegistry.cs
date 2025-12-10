using System.Collections.Generic;

namespace CSharpKOTOR.Extract
{
    // Minimal registry matching PyKotor extract/twoda.py structure.
    public static class TwoDARegistry
    {
        private static readonly Dictionary<string, HashSet<string>> StrRefColumns = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, HashSet<string>> ResRefColumns = new Dictionary<string, HashSet<string>>();

        public static Dictionary<string, HashSet<string>> ColumnsFor(string dataType)
        {
            return dataType == "strref" ? StrRefColumns : ResRefColumns;
        }

        public static HashSet<string> Files()
        {
            var files = new HashSet<string>();
            foreach (var k in StrRefColumns.Keys) files.Add(k);
            foreach (var k in ResRefColumns.Keys) files.Add(k);
            return files;
        }
    }
}


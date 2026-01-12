using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Extract.Capsule;
using Andastra.Parsing.Extract.Capsule;
using Andastra.Parsing.Resource.Formats.GFF;
using Andastra.Parsing.Resource.Formats.SSF;
using Andastra.Parsing.Resource.Formats.TwoDA;
// Removed: using Andastra.Parsing.Installation; // Using fully qualified names to break circular dependency
using Andastra.Parsing.Common.Logger;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:40-47
    // Original: _SCAN_RESULTS_CACHE: dict[tuple, list[tuple[int, str]]] = {}
    internal static class ScanResultsCache
    {
        private static readonly Dictionary<string, List<(int strref, string location)>> _cache = new Dictionary<string, List<(int, string)>>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:45-47
        // Original: def clear_scan_cache() -> None:
        public static void Clear()
        {
            _cache.Clear();
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:88-119
    // Original: @dataclass class TwoDARefLocation, SSFRefLocation, GFFRefLocation, NCSRefLocation:
    public class TwoDARefLocation
    {
        public int RowIndex { get; set; }
        public string ColumnName { get; set; }

        public TwoDARefLocation(int rowIndex, string columnName)
        {
            RowIndex = rowIndex;
            ColumnName = columnName;
        }
    }

    public class SSFRefLocation
    {
        public SSFSound Sound { get; set; }

        public SSFRefLocation(SSFSound sound)
        {
            Sound = sound;
        }
    }

    public class GFFRefLocation
    {
        public string FieldPath { get; set; }

        public GFFRefLocation(string fieldPath)
        {
            FieldPath = fieldPath;
        }
    }

    public class NCSRefLocation
    {
        public int ByteOffset { get; set; }

        public NCSRefLocation(int byteOffset)
        {
            ByteOffset = byteOffset;
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:122-128
    // Original: @dataclass class StrRefSearchResult:
    public class StrRefSearchResult
    {
        public FileResource Resource { get; set; }
        public List<object> Locations { get; set; }

        public StrRefSearchResult(FileResource resource, List<object> locations)
        {
            Resource = resource;
            Locations = locations;
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:130-479
    // Original: class StrRefReferenceCache:
    /// <summary>
    /// Cache of StrRef references found during resource scanning.
    /// Maps StrRef -> list of (resource_identifier, locations) where it's referenced.
    /// </summary>
    public class StrRefReferenceCache
    {
        private readonly BioWareGame _game;
        private readonly Dictionary<int, Dictionary<ResourceIdentifier, List<string>>> _cache = new Dictionary<int, Dictionary<ResourceIdentifier, List<string>>>();
        private readonly Dictionary<string, HashSet<string>> _strref2daColumns;
        private int _totalReferencesFound;
        private readonly HashSet<string> _filesWithStrrefs = new HashSet<string>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:136-159
        // Original: def __init__(self, game: Game):
        public StrRefReferenceCache(BioWareGame game)
        {
            _game = game;

            // Get game-specific 2DA column definitions
            if (_game == BioWareGame.K1)
            {
                _strref2daColumns = TwoDARegistry.ColumnsFor("strref", false);
            }
            else if (_game == BioWareGame.TSL)
            {
                _strref2daColumns = TwoDARegistry.ColumnsFor("strref", true);
            }
            else
            {
                _strref2daColumns = new Dictionary<string, HashSet<string>>();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:161-201
        // Original: def scan_resource(self, resource: FileResource, data: bytes) -> None:
        public void ScanResource(FileResource resource, byte[] data)
        {
            if (resource == null || data == null)
            {
                return;
            }

            ResourceIdentifier identifier = resource.Identifier;
            ResourceType restype = resource.ResType;
            string filename = resource.Filename().ToLowerInvariant();

            try
            {
                // 2DA files
                if (restype == ResourceType.TwoDA && _strref2daColumns.ContainsKey(filename))
                {
                    Scan2DA(identifier, data, filename);
                }
                // SSF files
                else if (restype == ResourceType.SSF)
                {
                    ScanSSF(identifier, data);
                }
                // NCS files - NCS scanning temporarily disabled pending bytecode analysis implementation
                // NCS files contain compiled NWScript bytecode which requires specialized parsing
                // GFF files
                else if (restype.IsGff())
                {
                    try
                    {
                        var reader = new GFFBinaryReader(data);
                        var gffObj = reader.Load();
                        if (gffObj != null)
                        {
                            ScanGFF(identifier, gffObj.Root);
                        }
                    }
                    catch
                    {
                        // Failed to parse GFF, skip
                    }
                }
            }
            catch
            {
                // Skip files that fail to scan
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:203-223
        // Original: def _scan_2da(self, identifier: ResourceIdentifier, data: bytes, filename: str) -> None:
        private void Scan2DA(ResourceIdentifier identifier, byte[] data, string filename)
        {
            var twodaObj = new TwoDABinaryReader(data).Load();
            if (!_strref2daColumns.TryGetValue(filename, out HashSet<string> columnsWithStrrefs))
            {
                return;
            }

            for (int rowIdx = 0; rowIdx < twodaObj.GetHeight(); rowIdx++)
            {
                foreach (string columnName in columnsWithStrrefs)
                {
                    if (columnName == ">>##HEADER##<<")
                    {
                        continue;
                    }

                    string cell = twodaObj.GetCellString(rowIdx, columnName);
                    if (!string.IsNullOrEmpty(cell) && cell.Trim().All(char.IsDigit))
                    {
                        int strref = int.Parse(cell.Trim());
                        string location = $"row_{rowIdx}.{columnName}";
                        LogDebug($"Found StrRef {strref} in 2DA file '{filename}' at row {rowIdx}, column '{columnName}'");
                        AddReference(strref, identifier, location);
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:225-239
        // Original: def _scan_ssf(self, identifier: ResourceIdentifier, data: bytes) -> None:
        private void ScanSSF(ResourceIdentifier identifier, byte[] data)
        {
            var reader = new SSFBinaryReader(data);
            var ssfObj = reader.Load();
            string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";

            foreach (SSFSound sound in Enum.GetValues(typeof(SSFSound)))
            {
                int? strref = ssfObj.Get(sound);
                if (strref.HasValue && strref.Value != -1)
                {
                    string location = $"sound_{sound}";
                    LogDebug($"Found StrRef {strref.Value} in SSF file '{filename}' at sound slot '{sound}'");
                    AddReference(strref.Value, identifier, location);
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:308-346
        // Original: def _scan_gff(self, identifier: ResourceIdentifier, gff_struct: GFFStruct, current_path: str = "") -> None:
        private void ScanGFF(ResourceIdentifier identifier, GFFStruct gffStruct, string currentPath = "")
        {
            foreach (var field in gffStruct)
            {
                // Build field path
                string fieldPath = string.IsNullOrEmpty(currentPath) ? field.label : $"{currentPath}.{field.label}";

                // LocalizedString fields
                if (field.fieldType == GFFFieldType.LocalizedString && field.value is LocalizedString locstring)
                {
                    if (locstring.StringRef != -1)
                    {
                        string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";
                        LogDebug($"Found StrRef {locstring.StringRef} in GFF file '{filename}' at field path '{fieldPath}'");
                        AddReference(locstring.StringRef, identifier, fieldPath);
                    }
                }

                // Nested structs
                if (field.fieldType == GFFFieldType.Struct && field.value is GFFStruct nestedStruct)
                {
                    ScanGFF(identifier, nestedStruct, fieldPath);
                }

                // Lists
                if (field.fieldType == GFFFieldType.List && field.value is GFFList list)
                {
                    for (int idx = 0; idx < list.Count; idx++)
                    {
                        if (list[idx] is GFFStruct listStruct)
                        {
                            string listPath = $"{fieldPath}[{idx}]";
                            ScanGFF(identifier, listStruct, listPath);
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:348-375
        // Original: def _add_reference(self, strref: int, identifier: ResourceIdentifier, location: str) -> None:
        private void AddReference(int strref, ResourceIdentifier identifier, string location)
        {
            string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";

            // Track statistics
            _totalReferencesFound++;
            _filesWithStrrefs.Add(filename);

            // Initialize dict for this StrRef if needed
            if (!_cache.ContainsKey(strref))
            {
                _cache[strref] = new Dictionary<ResourceIdentifier, List<string>>();
                LogVerbose($"  → Cached new StrRef {strref} from '{filename}' at '{location}'");
            }

            // O(1) dictionary lookup instead of O(n) linear search
            if (_cache[strref].ContainsKey(identifier))
            {
                // Identifier already exists, append location
                _cache[strref][identifier].Add(location);
            }
            else
            {
                // New identifier for this StrRef
                _cache[strref][identifier] = new List<string> { location };
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:377-388
        // Original: def get_references(self, strref: int) -> list[tuple[ResourceIdentifier, list[str]]]:
        public List<(ResourceIdentifier identifier, List<string> locations)> GetReferences(int strref)
        {
            if (!_cache.TryGetValue(strref, out Dictionary<ResourceIdentifier, List<string>> strrefDict))
            {
                return new List<(ResourceIdentifier, List<string>)>();
            }
            return strrefDict.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:390-392
        // Original: def has_references(self, strref: int) -> bool:
        public bool HasReferences(int strref)
        {
            return _cache.ContainsKey(strref);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:394-404
        // Original: def get_statistics(self) -> dict[str, int]:
        public Dictionary<string, int> GetStatistics()
        {
            return new Dictionary<string, int>
            {
                { "unique_strrefs", _cache.Count },
                { "total_references", _totalReferencesFound },
                { "files_with_strrefs", _filesWithStrrefs.Count }
            };
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:406-414
        // Original: def log_summary(self) -> None:
        public void LogSummary()
        {
            var stats = GetStatistics();
            LogVerbose(
                $"\nStrRef Cache Summary:\n" +
                $"  • {stats["unique_strrefs"]} unique StrRefs cached\n" +
                $"  • {stats["total_references"]} total StrRef references found\n" +
                $"  • {stats["files_with_strrefs"]} files contain StrRef references"
            );
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:416-434
        // Original: def to_dict(self) -> dict[str, list[dict[str, str | list[str]]]]:
        public Dictionary<string, List<Dictionary<string, object>>> ToDict()
        {
            var serialized = new Dictionary<string, List<Dictionary<string, object>>>();

            foreach (var kvp in _cache)
            {
                int strref = kvp.Key;
                var referencesDict = kvp.Value;
                serialized[strref.ToString()] = referencesDict.Select(identifierKvp => new Dictionary<string, object>
                {
                    { "resname", identifierKvp.Key.ResName },
                    { "restype", identifierKvp.Key.ResType.Extension },
                    { "locations", identifierKvp.Value }
                }).ToList();
            }

            return serialized;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:436-479
        // Original: @classmethod def from_dict(cls, game: Game, data: dict[str, list[dict[str, str | list[str]]]]) -> StrRefReferenceCache:
        public static StrRefReferenceCache FromDict(BioWareGame game, Dictionary<string, List<Dictionary<string, object>>> data)
        {
            var cache = new StrRefReferenceCache(game);

            foreach (var kvp in data)
            {
                string strrefStr = kvp.Key;
                var references = kvp.Value;
                int strref = int.Parse(strrefStr);
                cache._cache[strref] = new Dictionary<ResourceIdentifier, List<string>>();

                foreach (var refData in references)
                {
                    string resname = refData["resname"].ToString();
                    string restypeExt = refData["restype"].ToString();
                    var locationsList = refData["locations"] as List<object>;
                    var locations = locationsList?.Cast<string>().ToList() ?? new List<string>();

                    // Recreate ResourceIdentifier
                    ResourceType restype = ResourceType.FromExtension(restypeExt);
                    if (restype == null || !restype.IsValid())
                    {
                        continue;
                    }

                    var identifier = new ResourceIdentifier(resname, restype);

                    // Use dict assignment
                    cache._cache[strref][identifier] = locations;

                    // Update statistics
                    cache._totalReferencesFound += locations.Count;
                    string filename = $"{resname}.{restypeExt}";
                    cache._filesWithStrrefs.Add(filename);
                }
            }

            LogVerbose($"Restored StrRef cache from saved data: {cache._cache.Count} StrRefs, {cache._totalReferencesFound} references");

            return cache;
        }

        private static void LogDebug(string msg)
        {
            int logLevel = Environment.GetEnvironmentVariable("KOTORDIFF_DEBUG") != null ? 2 : (Environment.GetEnvironmentVariable("KOTORDIFF_VERBOSE") != null ? 1 : 0);
            if (logLevel >= 2)
            {
                Console.WriteLine($"[DEBUG] {msg}");
            }
        }

        private static void LogVerbose(string msg)
        {
            int logLevel = Environment.GetEnvironmentVariable("KOTORDIFF_DEBUG") != null ? 2 : (Environment.GetEnvironmentVariable("KOTORDIFF_VERBOSE") != null ? 1 : 0);
            if (logLevel >= 1)
            {
                Console.WriteLine($"[VERBOSE] {msg}");
            }
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:482-755
    // Original: class TwoDAMemoryReferenceCache:
    /// <summary>
    /// Cache of 2DA memory token references found during resource scanning.
    /// Maps (2da_filename, row_index) -> {resource_identifier: [field_paths]} where that row is referenced.
    /// </summary>
    public class TwoDAMemoryReferenceCache
    {
        private readonly BioWareGame _game;
        private readonly Dictionary<(string twodaFilename, int rowIndex), Dictionary<ResourceIdentifier, List<string>>> _cache = new Dictionary<(string, int), Dictionary<ResourceIdentifier, List<string>>>();
        private int _totalReferencesFound;
        private readonly HashSet<string> _filesWith2daRefs = new HashSet<string>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:492-498
        // Original: def __init__(self, game: Game):
        public TwoDAMemoryReferenceCache(BioWareGame game)
        {
            _game = game;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:508-535
        // Original: def scan_resource(self, resource: FileResource, data: bytes) -> None:
        public void ScanResource(FileResource resource, byte[] data)
        {
            if (resource == null || data == null)
            {
                return;
            }

            ResourceIdentifier identifier = resource.Identifier;
            ResourceType restype = resource.ResType;

            try
            {
                // Only scan GFF files for 2DA references
                if (restype.IsGff())
                {
                    try
                    {
                        var reader = new GFFBinaryReader(data);
                        var gffObj = reader.Load();
                        if (gffObj != null)
                        {
                            ScanGFF(identifier, gffObj.Root);
                        }
                    }
                    catch
                    {
                        // Failed to parse GFF, skip
                    }
                }
            }
            catch
            {
                // Skip files that fail to scan
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:537-592
        // Original: def _scan_gff(self, identifier: ResourceIdentifier, gff_struct: GFFStruct, current_path: str = "") -> None:
        private void ScanGFF(ResourceIdentifier identifier, GFFStruct gffStruct, string currentPath = "")
        {
            // Get the mapping lazily to avoid circular dependency
            var gffFieldTo2daMapping = GetGffFieldTo2daMapping();

            foreach (var field in gffStruct)
            {
                // Build field path
                string fieldPath = string.IsNullOrEmpty(currentPath) ? field.label : $"{currentPath}.{field.label}";

                // Check if this field references a 2DA
                if (gffFieldTo2daMapping.TryGetValue(field.label, out ResourceIdentifier twodaIdentifier))
                {
                    // This field references a 2DA file
                    string twodaFilename = $"{twodaIdentifier.ResName}.{twodaIdentifier.ResType.Extension}";

                    // Extract the numeric value (row index)
                    int? rowIndex = null;
                    if (field.fieldType == GFFFieldType.Int8 || field.fieldType == GFFFieldType.Int16 || field.fieldType == GFFFieldType.Int32 || field.fieldType == GFFFieldType.Int64)
                    {
                        if (field.value is int intVal)
                        {
                            rowIndex = intVal;
                        }
                    }
                    else if (field.fieldType == GFFFieldType.UInt8 || field.fieldType == GFFFieldType.UInt16 || field.fieldType == GFFFieldType.UInt32 || field.fieldType == GFFFieldType.UInt64)
                    {
                        if (field.value is uint uintVal)
                        {
                            rowIndex = (int)uintVal;
                        }
                        else if (field.value is int intVal)
                        {
                            rowIndex = intVal;
                        }
                    }

                    if (rowIndex.HasValue && rowIndex.Value >= 0)
                    {
                        AddReference(twodaFilename, rowIndex.Value, identifier, fieldPath);
                    }
                }

                // Recurse into nested structures
                if (field.fieldType == GFFFieldType.Struct && field.value is GFFStruct nestedStruct)
                {
                    ScanGFF(identifier, nestedStruct, fieldPath);
                }
                else if (field.fieldType == GFFFieldType.List && field.value is GFFList list)
                {
                    for (int idx = 0; idx < list.Count; idx++)
                    {
                        if (list[idx] is GFFStruct listStruct)
                        {
                            string listPath = $"{fieldPath}[{idx}]";
                            ScanGFF(identifier, listStruct, listPath);
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:57-61
        // Original: def _get_gff_field_to_2da_mapping():
        private Dictionary<string, ResourceIdentifier> GetGffFieldTo2daMapping()
        {
            return Extract.TwoDARegistry.GffFieldMapping();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:594-628
        // Original: def _add_reference(self, twoda_filename: str, row_index: int, identifier: ResourceIdentifier, location: str) -> None:
        private void AddReference(string twodaFilename, int rowIndex, ResourceIdentifier identifier, string location)
        {
            var key = (twodaFilename.ToLowerInvariant(), rowIndex);
            string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";

            // Track statistics
            _totalReferencesFound++;
            _filesWith2daRefs.Add(filename);

            // Initialize dict for this 2DA row if needed
            if (!_cache.ContainsKey(key))
            {
                _cache[key] = new Dictionary<ResourceIdentifier, List<string>>();
            }

            // O(1) dictionary lookup instead of O(n) linear search
            if (_cache[key].ContainsKey(identifier))
            {
                // Identifier already exists, append location
                _cache[key][identifier].Add(location);
            }
            else
            {
                // New identifier for this 2DA row
                _cache[key][identifier] = new List<string> { location };
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:630-647
        // Original: def get_references(self, twoda_filename: str, row_index: int) -> list[tuple[ResourceIdentifier, list[str]]]:
        public List<(ResourceIdentifier identifier, List<string> locations)> GetReferences(string twodaFilename, int rowIndex)
        {
            var key = (twodaFilename.ToLowerInvariant(), rowIndex);
            if (!_cache.TryGetValue(key, out Dictionary<ResourceIdentifier, List<string>> twodaDict))
            {
                return new List<(ResourceIdentifier, List<string>)>();
            }
            return twodaDict.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:649-664
        // Original: def has_references(self, twoda_filename: str, row_index: int) -> bool:
        public bool HasReferences(string twodaFilename, int rowIndex)
        {
            var key = (twodaFilename.ToLowerInvariant(), rowIndex);
            return _cache.ContainsKey(key);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:666-677
        // Original: def get_statistics(self) -> dict[str, int]:
        public Dictionary<string, int> GetStatistics()
        {
            return new Dictionary<string, int>
            {
                { "unique_2da_refs", _cache.Count },
                { "total_references", _totalReferencesFound },
                { "files_with_2da_refs", _filesWith2daRefs.Count }
            };
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:679-684
        // Original: def log_summary(self) -> None:
        public void LogSummary()
        {
            var stats = GetStatistics();
            LogVerbose($"2DA Memory Reference Cache: {stats["unique_2da_refs"]} unique 2DA rows referenced");
            LogVerbose($"  Total references: {stats["total_references"]}");
            LogVerbose($"  Files with 2DA refs: {stats["files_with_2da_refs"]}");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:686-705
        // Original: def to_dict(self) -> dict[str, list[dict[str, str | int | list[str]]]]:
        public Dictionary<string, List<Dictionary<string, object>>> ToDict()
        {
            var result = new Dictionary<string, List<Dictionary<string, object>>>();

            foreach (var kvp in _cache)
            {
                var (twodaFilename, rowIndex) = kvp.Key;
                var referencesDict = kvp.Value;
                string key = $"{twodaFilename}:{rowIndex}";
                result[key] = referencesDict.Select(identifierKvp => new Dictionary<string, object>
                {
                    { "resname", identifierKvp.Key.ResName },
                    { "restype", identifierKvp.Key.ResType.Extension },
                    { "locations", identifierKvp.Value }
                }).ToList();
            }

            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:707-755
        // Original: @classmethod def from_dict(cls, game: Game, data: dict[str, list[dict[str, str | int | list[str]]]]) -> TwoDAMemoryReferenceCache:
        public static TwoDAMemoryReferenceCache FromDict(BioWareGame game, Dictionary<string, List<Dictionary<string, object>>> data)
        {
            var cache = new TwoDAMemoryReferenceCache(game);

            foreach (var kvp in data)
            {
                string keyStr = kvp.Key;
                var references = kvp.Value;

                // Parse key: "soundset.2da:123" -> ("soundset.2da", 123)
                string[] parts = keyStr.Split(':');
                if (parts.Length != 2)
                {
                    continue;
                }

                string twodaFilename = parts[0];
                if (!int.TryParse(parts[1], out int rowIndex))
                {
                    continue;
                }

                var cacheKey = (twodaFilename.ToLowerInvariant(), rowIndex);
                cache._cache[cacheKey] = new Dictionary<ResourceIdentifier, List<string>>();

                foreach (var refData in references)
                {
                    string resname = refData["resname"].ToString();
                    string restypeExt = refData["restype"].ToString();
                    var locationsList = refData["locations"] as List<object>;
                    var locations = locationsList?.Cast<string>().ToList() ?? new List<string>();

                    // Recreate ResourceIdentifier
                    ResourceType restype = ResourceType.FromExtension(restypeExt);
                    if (restype == null || !restype.IsValid())
                    {
                        continue;
                    }

                    var identifier = new ResourceIdentifier(resname, restype);

                    // Use dict assignment
                    cache._cache[cacheKey][identifier] = locations;

                    // Update statistics
                    cache._totalReferencesFound += locations.Count;
                    string filename = $"{resname}.{restypeExt}";
                    cache._filesWith2daRefs.Add(filename);
                }
            }

            return cache;
        }

        private static void LogVerbose(string msg)
        {
            int logLevel = Environment.GetEnvironmentVariable("KOTORDIFF_DEBUG") != null ? 2 : (Environment.GetEnvironmentVariable("KOTORDIFF_VERBOSE") != null ? 1 : 0);
            if (logLevel >= 1)
            {
                Console.WriteLine($"[VERBOSE] {msg}");
            }
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:758-903
    // Original: def find_all_strref_references(...) -> tuple[dict[int, list[StrRefSearchResult]], StrRefReferenceCache]:
    /// <summary>
    /// Find all references to multiple StrRefs in an Andastra.Parsing.Installation.Installation using batch processing.
    /// This function scans resources once and finds references to all requested StrRefs,
    /// providing significant performance improvement over calling FindStrRefReferences multiple times.
    /// </summary>
    public static class ReferenceCacheHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:758-903
        // Original: def find_all_strref_references(...):
        public static (Dictionary<int, List<StrRefSearchResult>> results, StrRefReferenceCache cache) FindAllStrRefReferences(
            Andastra.Parsing.Installation.Installation installation,
            List<int> strrefs,
            StrRefReferenceCache cache = null,
            Action<string> logger = null)
        {
            if (strrefs == null || strrefs.Count == 0)
            {
                return (new Dictionary<int, List<StrRefSearchResult>>(), cache ?? new StrRefReferenceCache(installation.Game));
            }

            // Build cache if not provided
            if (cache == null)
            {
                logger?.Invoke($"Building StrRef cache for {strrefs.Count} StrRefs for Andastra.Parsing.Installation.Installation {installation.Path}...");
                cache = new StrRefReferenceCache(installation.Game);

                // Scan all resources to build the cache
                int resourceCount = 0;
                int skippedCount = 0;
                int lastLoggedCount = 0;
                int logInterval = 500; // Log progress every 500 resources

                var allResources = GetAllResources(installation);
                foreach (var resource in allResources)
                {
                    try
                    {
                        // Skip RIM files - they're not used at runtime
                        string filePath = resource.FilePath;
                        if (filePath.IndexOf("rims", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            skippedCount++;
                            continue;
                        }

                        // Filter by resource type: Only scan types that can contain StrRefs
                        ResourceType restype = resource.ResType;
                        bool canContainStrref = restype.IsGff() || restype == ResourceType.TwoDA || restype == ResourceType.SSF || restype == ResourceType.NCS;
                        if (!canContainStrref)
                        {
                            skippedCount++;
                            continue;
                        }

                        byte[] data = resource.GetData();
                        cache.ScanResource(resource, data);
                        resourceCount++;

                        // Log progress periodically
                        if (logger != null && resourceCount - lastLoggedCount >= logInterval)
                        {
                            logger($"  Scanning for StrRefs... {resourceCount} resources processed for Andastra.Parsing.Installation.Installation {installation.Path}");
                            lastLoggedCount = resourceCount;
                        }
                    }
                    catch
                    {
                        skippedCount++;
                    }
                }

                logger?.Invoke($"Cache built: scanned {resourceCount} resources (skipped {skippedCount} files) for Andastra.Parsing.Installation.Installation {installation.Path}");
            }

            // Convert cache entries to StrRefSearchResult format
            var results = new Dictionary<int, List<StrRefSearchResult>>();

            // Build a map of ResourceIdentifier -> FileResource by iterating Andastra.Parsing.Installation.Installation ONCE
            var identifierToResource = new Dictionary<ResourceIdentifier, FileResource>();
            var allResourcesForMapping = GetAllResources(installation);
            foreach (var res in allResourcesForMapping)
            {
                try
                {
                    identifierToResource[res.Identifier] = res;
                }
                catch
                {
                    // Skip invalid resources
                }
            }

            foreach (int strref in strrefs)
            {
                var cacheEntries = cache.GetReferences(strref);
                if (cacheEntries.Count == 0)
                {
                    results[strref] = new List<StrRefSearchResult>();
                    continue;
                }

                // Convert cache format to StrRefSearchResult format
                var strrefResults = new List<StrRefSearchResult>();

                foreach (var (identifier, locationStrings) in cacheEntries)
                {
                    try
                    {
                        if (!identifierToResource.TryGetValue(identifier, out FileResource foundResource) || foundResource == null)
                        {
                            continue;
                        }

                        // Convert location strings to proper location objects
                        var locations = new List<object>();

                        foreach (string locStr in locationStrings)
                        {
                            // Parse location string format: "row_12.name", "sound_Battlecry 1", "field_path", or byte offset
                            if (locStr.StartsWith("row_"))
                            {
                                // 2DA reference: "row_12.name"
                                string[] parts = locStr.Replace("row_", "").Split(new[] { '.' }, 2);
                                if (parts.Length == 2 && int.TryParse(parts[0], out int rowIdx))
                                {
                                    locations.Add(new TwoDARefLocation(rowIdx, parts[1]));
                                }
                            }
                            else if (locStr.StartsWith("sound_"))
                            {
                                // SSF reference: "sound_Battlecry 1"
                                string soundName = locStr.Replace("sound_", "");
                                if (Enum.TryParse<SSFSound>(soundName, out SSFSound sound))
                                {
                                    locations.Add(new SSFRefLocation(sound));
                                }
                            }
                            else if (locStr.StartsWith("offset_"))
                            {
                                // NCS reference: "offset_1234"
                                if (int.TryParse(locStr.Replace("offset_", ""), out int byteOffset))
                                {
                                    locations.Add(new NCSRefLocation(byteOffset));
                                }
                            }
                            else
                            {
                                // GFF reference: field path
                                locations.Add(new GFFRefLocation(locStr));
                            }
                        }

                        if (locations.Count > 0)
                        {
                            strrefResults.Add(new StrRefSearchResult(foundResource, locations));
                        }
                    }
                    catch
                    {
                        // Skip invalid entries
                    }
                }

                results[strref] = strrefResults;
            }

            return (results, cache);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:906-1254
        // Original: def find_strref_references(...) -> list[StrRefSearchResult]:
        /// <summary>
        /// Find all references to a specific StrRef in an installation.
        /// This function scans 2DA, SSF, GFF, and NCS files for references to the given StrRef
        /// and returns complete location information for each reference.
        /// </summary>
        public static List<StrRefSearchResult> FindStrRefReferences(
            Andastra.Parsing.Installation.Installation installation,
            int strref,
            StrRefReferenceCache cache = null,
            Action<string> logger = null)
        {
            // If cache is provided, use it for faster lookup
            if (cache != null)
            {
                var cacheEntries = cache.GetReferences(strref);
                if (cacheEntries.Count == 0)
                {
                    return new List<StrRefSearchResult>();
                }

                // Build a map of ResourceIdentifier -> FileResource
                var identifierToResource = new Dictionary<ResourceIdentifier, FileResource>();
                var allResourcesForMap = GetAllResources(installation);
                foreach (var res in allResourcesForMap)
                {
                    try
                    {
                        identifierToResource[res.Identifier] = res;
                    }
                    catch
                    {
                        // Skip invalid resources
                    }
                }

                // Convert cache format to StrRefSearchResult format
                var results = new List<StrRefSearchResult>();
                foreach (var (identifier, locationStrings) in cacheEntries)
                {
                    try
                    {
                        if (!identifierToResource.TryGetValue(identifier, out FileResource foundResource) || foundResource == null)
                        {
                            continue;
                        }

                        var locations = new List<object>();
                        foreach (string locStr in locationStrings)
                        {
                            if (locStr.StartsWith("row_"))
                            {
                                string[] parts = locStr.Replace("row_", "").Split(new[] { '.' }, 2);
                                if (parts.Length == 2 && int.TryParse(parts[0], out int rowIdx))
                                {
                                    locations.Add(new TwoDARefLocation(rowIdx, parts[1]));
                                }
                            }
                            else if (locStr.StartsWith("sound_"))
                            {
                                string soundName = locStr.Replace("sound_", "");
                                if (Enum.TryParse<SSFSound>(soundName, out SSFSound sound))
                                {
                                    locations.Add(new SSFRefLocation(sound));
                                }
                            }
                            else if (locStr.StartsWith("offset_"))
                            {
                                if (int.TryParse(locStr.Replace("offset_", ""), out int byteOffset))
                                {
                                    locations.Add(new NCSRefLocation(byteOffset));
                                }
                            }
                            else
                            {
                                locations.Add(new GFFRefLocation(locStr));
                            }
                        }

                        if (locations.Count > 0)
                        {
                            results.Add(new StrRefSearchResult(foundResource, locations));
                        }
                    }
                    catch
                    {
                        // Skip invalid entries
                    }
                }

                return results;
            }

            // No cache available - scan all resources (slower path)
            var scanResults = new List<StrRefSearchResult>();
            var allResources = GetAllResources(installation);

            foreach (var resource in allResources)
            {
                ResourceType restype = resource.ResType;

                // Skip RIM files
                if (resource.FilePath.IndexOf("rims", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                // Check 2DA files
                if (restype == ResourceType.TwoDA)
                {
                    var result = Scan2DAForStrRef(resource, installation, strref, logger);
                    if (result != null)
                    {
                        scanResults.Add(result);
                    }
                }
                // Check SSF files
                else if (restype == ResourceType.SSF)
                {
                    var result = ScanSSFForStrRef(resource, installation, strref, logger);
                    if (result != null)
                    {
                        scanResults.Add(result);
                    }
                }
                // Check GFF files
                else if (restype.IsGff())
                {
                    var result = ScanGFFForStrRef(resource, installation, strref, logger);
                    if (result != null)
                    {
                        scanResults.Add(result);
                    }
                }
            }

            return scanResults;
        }

        // Helper method to get all resources from an Installation
        private static List<FileResource> GetAllResources(Andastra.Parsing.Installation.Installation installation)
        {
            var allResources = new List<FileResource>();

            // Get chitin resources
            allResources.AddRange(installation.ChitinResources());

            // Get core resources (includes patch.erf for K1)
            allResources.AddRange(installation.CoreResources());

            // Get override resources
            string overridePath = installation.OverridePath();
            if (Directory.Exists(overridePath))
            {
                var overrideFiles = Directory.GetFiles(overridePath, "*.*", SearchOption.AllDirectories);
                foreach (string file in overrideFiles)
                {
                    try
                    {
                        var identifier = ResourceIdentifier.FromPath(file);
                        if (identifier.ResType != ResourceType.INVALID && !identifier.ResType.IsInvalid)
                        {
                            var fileInfo = new FileInfo(file);
                            allResources.Add(new FileResource(identifier.ResName, identifier.ResType, (int)fileInfo.Length, 0, file));
                        }
                    }
                    catch
                    {
                        // Skip invalid files
                    }
                }
            }

            // Get module resources
            // TODO: HACK - Using fully qualified name to avoid circular dependency
            // Installation.GetModulesPath is a static method, so we can call it without a project reference
            string modulesPath = Andastra.Parsing.Installation.Installation.GetModulesPath(installation.Path);
            if (Directory.Exists(modulesPath))
            {
                var moduleFiles = Directory.GetFiles(modulesPath, "*.rim")
                    .Concat(Directory.GetFiles(modulesPath, "*.mod"))
                    .Concat(Directory.GetFiles(modulesPath, "*.erf"))
                    .ToList();

                foreach (string moduleFile in moduleFiles)
                {
                    try
                    {
                        var capsule = new LazyCapsule(moduleFile);
                        allResources.AddRange(capsule.GetResources());
                    }
                    catch
                    {
                        // Skip invalid modules
                    }
                }
            }

            return allResources;
        }

        // Helper to scan 2DA file for StrRef
        private static StrRefSearchResult Scan2DAForStrRef(FileResource resource, Andastra.Parsing.Installation.Installation installation, int strref, Action<string> logger)
        {
            try
            {
                var twoda = new TwoDABinaryReader(resource.GetData()).Load();
                string filename = resource.Filename().ToLowerInvariant();

                bool isK2 = installation.Game == BioWareGame.TSL;
                var strref2daColumns = TwoDARegistry.ColumnsFor("strref", isK2);

                if (!strref2daColumns.TryGetValue(filename, out HashSet<string> columns))
                {
                    return null;
                }

                var locations = new List<object>();
                for (int rowIdx = 0; rowIdx < twoda.GetHeight(); rowIdx++)
                {
                    foreach (string columnName in columns)
                    {
                        if (columnName == ">>##HEADER##<<")
                        {
                            continue;
                        }

                        string cell = twoda.GetCellString(rowIdx, columnName);
                        if (!string.IsNullOrEmpty(cell) && cell.Trim().All(char.IsDigit) && int.Parse(cell.Trim()) == strref)
                        {
                            locations.Add(new TwoDARefLocation(rowIdx, columnName));
                            logger?.Invoke($"    Found at: row {rowIdx}, column '{columnName}' at {resource.FilePath}");
                        }
                    }
                }

                return locations.Count > 0 ? new StrRefSearchResult(resource, locations) : null;
            }
            catch
            {
                return null;
            }
        }

        // Helper to scan SSF file for StrRef
        private static StrRefSearchResult ScanSSFForStrRef(FileResource resource, Andastra.Parsing.Installation.Installation installation, int strref, Action<string> logger)
        {
            try
            {
                var ssf = new SSFBinaryReader(resource.GetData()).Load();
                var locations = new List<object>();

                foreach (SSFSound sound in Enum.GetValues(typeof(SSFSound)))
                {
                    int? soundStrref = ssf.Get(sound);
                    if (soundStrref.HasValue && soundStrref.Value == strref)
                    {
                        locations.Add(new SSFRefLocation(sound));
                        logger?.Invoke($"    Found at: sound slot '{sound}' at {resource.FilePath}");
                    }
                }

                return locations.Count > 0 ? new StrRefSearchResult(resource, locations) : null;
            }
            catch
            {
                return null;
            }
        }

        // Helper to scan GFF file for StrRef
        private static StrRefSearchResult ScanGFFForStrRef(FileResource resource, Andastra.Parsing.Installation.Installation installation, int strref, Action<string> logger)
        {
            try
            {
                var gff = new GFFBinaryReader(resource.GetData()).Load();
                var locations = new List<object>();

                void RecurseGFF(GFFStruct gffStruct, string pathPrefix = "")
                {
                    foreach (var (label, fieldType, value) in gffStruct)
                    {
                        string fieldPath = string.IsNullOrEmpty(pathPrefix) ? label : $"{pathPrefix}.{label}";

                        try
                        {
                            // Check LocalizedString fields
                            if (fieldType == GFFFieldType.LocalizedString && value is LocalizedString locstring)
                            {
                                if (locstring.StringRef == strref)
                                {
                                    locations.Add(new GFFRefLocation(fieldPath));
                                    logger?.Invoke($"    Found at: field path '{fieldPath}' at {resource.FilePath}");
                                }
                            }

                            // Recurse into nested structs
                            if (fieldType == GFFFieldType.Struct && value is GFFStruct nestedStruct)
                            {
                                RecurseGFF(nestedStruct, fieldPath);
                            }

                            // Recurse into list items
                            if (fieldType == GFFFieldType.List && value is GFFList list)
                            {
                                for (int idx = 0; idx < list.Count; idx++)
                                {
                                    if (list[idx] is GFFStruct listStruct)
                                    {
                                        string listPath = $"{fieldPath}[{idx}]";
                                        RecurseGFF(listStruct, listPath);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Individual field errors - continue processing other fields
                        }
                    }
                }

                RecurseGFF(gff.Root);

                return locations.Count > 0 ? new StrRefSearchResult(resource, locations) : null;
            }
            catch
            {
                return null;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/reference_cache.py:57-61
        // Original: def _get_gff_field_to_2da_mapping(): ...
        public static Dictionary<string, ResourceIdentifier> GffFieldTo2daMapping()
        {
            return Extract.TwoDARegistry.GffFieldMapping();
        }
    }
}

// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:32-111
// Original: @dataclass class DiffCache: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KotorDiff.NET.Diff;
using JetBrains.Annotations;

namespace KotorDiff.NET.Cache
{
    /// <summary>
    /// Cache of diff results that can be saved/loaded.
    /// 1:1 port of DiffCache from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:32-111
    /// </summary>
    public class DiffCache
    {
        public string Version { get; set; }
        public string Mine { get; set; }
        public string Older { get; set; }
        [CanBeNull] public string Yours { get; set; }
        public string Timestamp { get; set; }
        [CanBeNull] public List<CachedFileComparison> Files { get; set; }

        // StrRef cache data (for TLK linking patches)
        [CanBeNull] public string StrrefCacheGame { get; set; } // Game type (K1/K2) for StrRef cache
        [CanBeNull] public Dictionary<string, object> StrrefCacheData { get; set; } // Serialized StrRef cache

        public DiffCache()
        {
            Files = new List<CachedFileComparison>();
        }

        /// <summary>
        /// Convert to dictionary for YAML serialization.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:62-88
        /// </summary>
        public Dictionary<string, object> ToDict()
        {
            var result = new Dictionary<string, object>
            {
                { "version", Version },
                { "mine", Mine },
                { "older", Older },
                { "yours", Yours },
                { "timestamp", Timestamp },
                {
                    "files",
                    (Files ?? new List<CachedFileComparison>()).Select(f => new Dictionary<string, object>
                    {
                        { "rel_path", f.RelPath },
                        { "status", f.Status },
                        { "ext", f.Ext },
                        { "left_exists", f.LeftExists },
                        { "right_exists", f.RightExists }
                    }).ToList()
                }
            };

            // Add StrRef cache data if present
            if (StrrefCacheGame != null)
            {
                result["strref_cache_game"] = StrrefCacheGame;
            }
            if (StrrefCacheData != null)
            {
                result["strref_cache_data"] = StrrefCacheData;
            }

            return result;
        }

        /// <summary>
        /// Load from dictionary (YAML deserialization).
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:90-111
        /// </summary>
        public static DiffCache FromDict(Dictionary<string, object> data)
        {
            var files = new List<CachedFileComparison>();
            if (data.ContainsKey("files") && data["files"] is List<object> filesList)
            {
                foreach (var f in filesList)
                {
                    if (f is Dictionary<string, object> fileDict)
                    {
                        files.Add(new CachedFileComparison
                        {
                            RelPath = fileDict.ContainsKey("rel_path") ? fileDict["rel_path"]?.ToString() : "",
                            Status = fileDict.ContainsKey("status") ? fileDict["status"]?.ToString() : "",
                            Ext = fileDict.ContainsKey("ext") ? fileDict["ext"]?.ToString() : "",
                            LeftExists = fileDict.ContainsKey("left_exists") && fileDict["left_exists"] is bool leftExists && leftExists,
                            RightExists = fileDict.ContainsKey("right_exists") && fileDict["right_exists"] is bool rightExists && rightExists
                        });
                    }
                }
            }

            return new DiffCache
            {
                Version = data.ContainsKey("version") ? data["version"]?.ToString() : "",
                Mine = data.ContainsKey("mine") ? data["mine"]?.ToString() : "",
                Older = data.ContainsKey("older") ? data["older"]?.ToString() : "",
                Yours = data.ContainsKey("yours") ? data["yours"]?.ToString() : null,
                Timestamp = data.ContainsKey("timestamp") ? data["timestamp"]?.ToString() : "",
                Files = files,
                StrrefCacheGame = data.ContainsKey("strref_cache_game") ? data["strref_cache_game"]?.ToString() : null,
                StrrefCacheData = data.ContainsKey("strref_cache_data") ? data["strref_cache_data"] as Dictionary<string, object> : null
            };
        }
    }
}


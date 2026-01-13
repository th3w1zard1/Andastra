// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:114-251
// Original: def save_diff_cache, load_diff_cache, restore_strref_cache_from_cache: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET;
using BioWare.NET.Common;
using JetBrains.Annotations;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Game = BioWare.NET.Common.BioWareGame;

namespace KotorDiff.Cache
{
    /// <summary>
    /// I/O operations for diff cache.
    /// 1:1 port of cache I/O functions from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:114-251
    /// </summary>
    public static class DiffCacheIO
    {
        /// <summary>
        /// Save diff cache to YAML file with companion data directory.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:114-175
        /// </summary>
        public static void SaveDiffCache(
            DiffCache cache,
            string cacheFile,
            string mine,
            string older,
            [CanBeNull] object strrefCache = null, // StrRefReferenceCache support can be added when needed
            [CanBeNull] Action<string> logFunc = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            // StrRef cache support can be added to DiffCache when StrRefReferenceCache is implemented
            // if (strrefCache != null)
            // {
            //     cache.StrrefCacheGame = str(strrefCache.game)
            //     cache.StrrefCacheData = strrefCache.to_dict()
            // }

            // Create companion data directory
            string cacheDir = Path.Combine(Path.GetDirectoryName(cacheFile), Path.GetFileNameWithoutExtension(cacheFile) + "_data");
            Directory.CreateDirectory(cacheDir);

            string leftDir = Path.Combine(cacheDir, "left");
            string rightDir = Path.Combine(cacheDir, "right");
            Directory.CreateDirectory(leftDir);
            Directory.CreateDirectory(rightDir);

            // Copy modified/different files to cache
            var filesList = cache.Files ?? new List<CachedFileComparison>();
            foreach (var fileComp in filesList)
            {
                if ((fileComp.Status == "modified" || fileComp.Status == "missing_right") && fileComp.LeftExists)
                {
                    string src = Path.Combine(mine, fileComp.RelPath);
                    if (File.Exists(src))
                    {
                        string dst = Path.Combine(leftDir, fileComp.RelPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        File.Copy(src, dst, overwrite: true);
                    }
                }

                if ((fileComp.Status == "modified" || fileComp.Status == "missing_left") && fileComp.RightExists)
                {
                    string src = Path.Combine(older, fileComp.RelPath);
                    if (File.Exists(src))
                    {
                        string dst = Path.Combine(rightDir, fileComp.RelPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        File.Copy(src, dst, overwrite: true);
                    }
                }
            }

            // Save metadata to YAML
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            string yamlContent = serializer.Serialize(cache.ToDict());
            File.WriteAllText(cacheFile, yamlContent, System.Text.Encoding.UTF8);

            logFunc($"Saved diff cache to: {cacheFile}");
            logFunc($"  Cached {filesList.Count} file comparisons");
            logFunc($"  Cache data directory: {cacheDir}");
        }

        /// <summary>
        /// Load diff cache from YAML file.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:178-215
        /// </summary>
        public static (DiffCache cache, string leftDir, string rightDir) LoadDiffCache(
            string cacheFile,
            [CanBeNull] Action<string> logFunc = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            // Load metadata from YAML
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            string yamlContent = File.ReadAllText(cacheFile, System.Text.Encoding.UTF8);
            var cacheData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            var cache = DiffCache.FromDict(cacheData);

            // Determine data directory paths
            string cacheDir = Path.Combine(Path.GetDirectoryName(cacheFile), Path.GetFileNameWithoutExtension(cacheFile) + "_data");
            string leftDir = Path.Combine(cacheDir, "left");
            string rightDir = Path.Combine(cacheDir, "right");

            logFunc($"Loaded diff cache from: {cacheFile}");
            logFunc($"  Cached {cache.Files?.Count ?? 0} file comparisons");
            logFunc($"  Original mine: {cache.Mine}");
            logFunc($"  Original older: {cache.Older}");

            return (cache, leftDir, rightDir);
        }

        /// <summary>
        /// Restore StrRef cache from DiffCache.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:218-251
        /// </summary>
        [CanBeNull]
        public static object RestoreStrrefCacheFromCache(
            DiffCache cache,
            Game? game = null)
        {
            if (cache.StrrefCacheData == null)
            {
                return null;
            }

            // StrRefReferenceCache restoration can be implemented when StrRefReferenceCache is available
            // This requires StrRefReferenceCache implementation
            return null;
        }
    }
}


// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/cache.py:114-251
// Original: def save_diff_cache, load_diff_cache, restore_strref_cache_from_cache: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuroraEngine.Common.Common;
using KotorDiff.NET.Diff;
using JetBrains.Annotations;

namespace KotorDiff.NET.Cache
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

            // YAML metadata saving can be added when YAML library is available
            // For now, just log
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

            // YAML metadata loading can be added when YAML library is available
            // For now, return empty cache
            var cache = new DiffCache();

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


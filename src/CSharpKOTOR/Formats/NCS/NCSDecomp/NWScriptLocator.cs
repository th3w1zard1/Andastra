//
using System;
using System.Collections.Generic;
using System.IO;
using File = System.IO.FileInfo;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Utility class to locate nwscript.nss files for K1 and TSL decompilation.
    /// </summary>
    public static class NWScriptLocator
    {
        public enum GameType
        {
            K1,
            TSL
        }

        /// <summary>
        /// Attempts to locate nwscript.nss file for the specified game type.
        /// </summary>
        /// <param name="gameType">Game type (K1 or TSL)</param>
        /// <param name="settings">Settings instance to check configured path</param>
        /// <returns>FileInfo for nwscript.nss if found, null otherwise</returns>
        public static File FindNWScriptFile(GameType gameType, Settings settings)
        {
            // 1. Check settings-configured path first
            string settingsPath = settings.GetProperty("NWScript Path");
            if (!string.IsNullOrEmpty(settingsPath))
            {
                File settingsFile = new File(settingsPath);
                if (settingsFile.IsFile())
                {
                    return settingsFile;
                }
            }

            // 2. Check current directory
            File currentDirFile = new File(Path.Combine(JavaSystem.GetProperty("user.dir"), "nwscript.nss"));
            if (currentDirFile.IsFile())
            {
                return currentDirFile;
            }

            // 3. Check vendor directories (relative to project root)
            List<string> candidatePaths = new List<string>();

            // Try to find project root by looking for vendor directory
            string currentDir = JavaSystem.GetProperty("user.dir");
            string searchDir = currentDir;
            for (int i = 0; i < 5; i++) // Search up to 5 levels up
            {
                string vendorPath = Path.Combine(searchDir, "vendor", "PyKotor", "vendor", "KotOR-Scripting-Tool", "NWN Script");
                if (Directory.Exists(vendorPath))
                {
                    candidatePaths.Add(Path.Combine(vendorPath, gameType == GameType.K1 ? "k1" : "k2", "nwscript.nss"));
                    candidatePaths.Add(Path.Combine(vendorPath, "k1", "nwscript.nss")); // Fallback to k1
                    candidatePaths.Add(Path.Combine(vendorPath, "k2", "nwscript.nss")); // Fallback to k2
                    break;
                }
                searchDir = Path.GetDirectoryName(searchDir);
                if (string.IsNullOrEmpty(searchDir))
                {
                    break;
                }
            }

            // Also check if we're already in a subdirectory that has vendor
            string altVendorPath = Path.Combine(currentDir, "..", "..", "..", "vendor", "PyKotor", "vendor", "KotOR-Scripting-Tool", "NWN Script");
            string resolvedAltPath = Path.GetFullPath(altVendorPath);
            if (Directory.Exists(resolvedAltPath))
            {
                candidatePaths.Add(Path.Combine(resolvedAltPath, gameType == GameType.K1 ? "k1" : "k2", "nwscript.nss"));
                candidatePaths.Add(Path.Combine(resolvedAltPath, "k1", "nwscript.nss"));
                candidatePaths.Add(Path.Combine(resolvedAltPath, "k2", "nwscript.nss"));
            }

            // Check each candidate path
            foreach (string candidatePath in candidatePaths)
            {
                File candidateFile = new File(candidatePath);
                if (candidateFile.IsFile())
                {
                    return candidateFile;
                }
            }

            // 4. Check game installation directories (if GameDirectoryLocator is available)
            try
            {
                // Use reflection to avoid hard dependency on Kotor.NET
                var gameLocatorType = Type.GetType("Kotor.NET.Helpers.GameDirectoryLocator, Kotor.NET");
                if (gameLocatorType != null)
                {
                    var instanceProperty = gameLocatorType.GetProperty("Instance");
                    if (instanceProperty != null)
                    {
                        object locator = instanceProperty.GetValue(null);
                        var locateMethod = gameLocatorType.GetMethod("Locate");
                        if (locateMethod != null)
                        {
                            var directories = locateMethod.Invoke(locator, null) as System.Array;
                            if (directories != null)
                            {
                                foreach (var dir in directories)
                                {
                                    var pathProperty = dir.GetType().GetProperty("Path");
                                    var engineProperty = dir.GetType().GetProperty("Engine");
                                    if (pathProperty != null && engineProperty != null)
                                    {
                                        string gamePath = pathProperty.GetValue(dir) as string;
                                        object engine = engineProperty.GetValue(dir);

                                        // Check if this is the right game type
                                        bool isK1 = engine != null && engine.ToString().Contains("K1");
                                        bool isTSL = engine != null && engine.ToString().Contains("K2");

                                        if ((gameType == GameType.K1 && isK1) || (gameType == GameType.TSL && isTSL))
                                        {
                                            if (!string.IsNullOrEmpty(gamePath))
                                            {
                                                string nwscriptPath = Path.Combine(gamePath, "nwscript.nss");
                                                File gameFile = new File(nwscriptPath);
                                                if (gameFile.IsFile())
                                                {
                                                    return gameFile;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore reflection errors - GameDirectoryLocator might not be available
            }

            return null;
        }

        /// <summary>
        /// Gets all possible candidate paths for nwscript.nss (for error messages).
        /// </summary>
        public static List<string> GetCandidatePaths(GameType gameType)
        {
            List<string> paths = new List<string>();

            string currentDir = JavaSystem.GetProperty("user.dir");
            paths.Add(Path.Combine(currentDir, "nwscript.nss"));

            // Vendor paths
            string searchDir = currentDir;
            for (int i = 0; i < 5; i++)
            {
                string vendorPath = Path.Combine(searchDir, "vendor", "PyKotor", "vendor", "KotOR-Scripting-Tool", "NWN Script");
                if (Directory.Exists(vendorPath))
                {
                    paths.Add(Path.Combine(vendorPath, gameType == GameType.K1 ? "k1" : "k2", "nwscript.nss"));
                    break;
                }
                searchDir = Path.GetDirectoryName(searchDir);
                if (string.IsNullOrEmpty(searchDir))
                {
                    break;
                }
            }

            return paths;
        }
    }
}






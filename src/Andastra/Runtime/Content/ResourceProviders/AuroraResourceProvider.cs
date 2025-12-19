using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Content.ResourceProviders
{
    /// <summary>
    /// Resource provider for Aurora Engine games (Neverwinter Nights, Neverwinter Nights 2).
    /// </summary>
    /// <remarks>
    /// Aurora Resource Provider:
    /// - Based on Aurora Engine resource loading system (Neverwinter Nights, Neverwinter Nights 2)
    /// - Aurora Engine uses HAK (Hak Archive) files, module files, and override directory for resource management
    /// - Resource precedence: OVERRIDE > MODULE > HAK (in load order) > BASE_GAME > HARDCODED
    /// - Original implementation: Aurora Engine uses CExoResMan for resource loading
    /// - Resource lookup: Searches override directory first, then module-specific resources, then HAK files, then base game
    /// - Module context: Sets current module for module-specific resource lookups
    /// - Search order: Override directory → Module resources → HAK files (in load order) → Base game resources → Hardcoded resources
    /// - Resource types: Supports Aurora Engine resource types (ARE, GIT, IFO, MDL, TGA, WAV, NCS, DLG, etc.)
    /// - Async loading: Provides async resource access for streaming and background loading
    /// - Resource enumeration: Can enumerate resources by type from HAK files, module directories, and override directory
    /// - Based on Aurora Engine's CExoResMan resource management system
    /// 
    /// Cross-Engine Resource Loading Patterns (from Ghidra analysis):
    /// - Aurora Engine (nwmain.exe): Similar to Odyssey but with HAK file support
    ///   - OVERRIDE directory: "override" (highest priority, allows modding)
    ///   - MODULES directory: "modules" (module-specific resources)
    ///   - HAK files: Hak Archive files containing additional module resources (loaded in order from Module.ifo)
    ///   - Base game: Core game resources
    /// - Resource table management: CExoResMan handles resource tracking and lookup (similar to CExoKeyTable in Odyssey)
    ///   - nwmain.exe: "MODULES=" @ 0x140d80d20 (module directory configuration)
    ///   - nwmain.exe: "OVERRIDE=" @ 0x140d80d50 (override directory configuration)
    ///   - nwmain.exe: "modules" @ 0x140d80f38, "override" @ 0x140d80f40 (directory names)
    /// - Aurora-specific: HAK file support for module resource archives
    ///   - HAK files are loaded in order specified in Module.ifo
    ///   - Resources in later HAK files override resources in earlier HAK files
    ///   - Module overrides take precedence over HAK files
    /// 
    /// TODO: Reverse engineer specific function addresses from Aurora Engine executables using Ghidra MCP
    ///   - Neverwinter Nights: nwmain.exe resource loading functions (CExoResMan initialization, HAK loading, module resource lookup)
    ///   - Neverwinter Nights 2: nwn2main.exe resource loading functions
    /// </remarks>
    public class AuroraResourceProvider : IGameResourceProvider
    {
        private readonly string _installationPath;
        private readonly GameType _gameType;
        private string _currentModule;
        private readonly Dictionary<string, byte[]> _overrideCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _hakFiles = new List<string>();

        public AuroraResourceProvider(string installationPath, GameType gameType = GameType.Unknown)
        {
            if (string.IsNullOrEmpty(installationPath))
            {
                throw new ArgumentException("Installation path cannot be null or empty", nameof(installationPath));
            }

            if (!Directory.Exists(installationPath))
            {
                throw new DirectoryNotFoundException($"Installation path does not exist: {installationPath}");
            }

            _installationPath = installationPath;
            _gameType = gameType;

            // Initialize HAK file list (will be populated when module is loaded)
            InitializeHakFiles();
        }

        public GameType GameType { get { return _gameType; } }

        /// <summary>
        /// Gets the path to the modules directory.
        /// </summary>
        public string ModulePath()
        {
            return Path.Combine(_installationPath, "modules");
        }

        /// <summary>
        /// Gets the path to the override directory.
        /// </summary>
        public string OverridePath()
        {
            return Path.Combine(_installationPath, "override");
        }

        /// <summary>
        /// Gets the path to the HAK directory.
        /// </summary>
        public string HakPath()
        {
            return Path.Combine(_installationPath, "hak");
        }

        /// <summary>
        /// Sets the current module context for resource lookups.
        /// </summary>
        public void SetCurrentModule(string moduleResRef)
        {
            _currentModule = moduleResRef;
            // Reload HAK files for the new module
            InitializeHakFiles();
        }

        /// <summary>
        /// Sets the HAK files to use for resource lookup (called by module loader).
        /// </summary>
        public void SetHakFiles(IEnumerable<string> hakFiles)
        {
            _hakFiles.Clear();
            if (hakFiles != null)
            {
                _hakFiles.AddRange(hakFiles);
            }
        }

        public async Task<Stream> OpenResourceAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                byte[] data = LookupResource(id);
                if (data == null || data.Length == 0)
                {
                    return null;
                }

                return new MemoryStream(data, writable: false);
            }, ct);
        }

        public async Task<bool> ExistsAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                return LookupResource(id) != null;
            }, ct);
        }

        public async Task<IReadOnlyList<LocationResult>> LocateAsync(
            ResourceIdentifier id,
            SearchLocation[] order,
            CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                var results = new List<LocationResult>();

                // Use provided order or default precedence
                SearchLocation[] searchOrder = order ?? new[]
                {
                    SearchLocation.Override,
                    SearchLocation.Module,
                    SearchLocation.Chitin, // HAK files
                    SearchLocation.Hardcoded
                };

                foreach (SearchLocation location in searchOrder)
                {
                    string path = LocateResourceInLocation(id, location);
                    if (!string.IsNullOrEmpty(path))
                    {
                        long size = 0;
                        long offset = 0;

                        if (File.Exists(path))
                        {
                            FileInfo fileInfo = new FileInfo(path);
                            size = fileInfo.Length;
                        }

                        results.Add(new LocationResult
                        {
                            Location = location,
                            Path = path,
                            Size = size,
                            Offset = offset
                        });
                    }
                }

                return (IReadOnlyList<LocationResult>)results;
            }, ct);
        }

        public IEnumerable<ResourceIdentifier> EnumerateResources(ResourceType type)
        {
            // Enumerate from override directory
            string overridePath = Path.Combine(_installationPath, "override");
            if (Directory.Exists(overridePath))
            {
                string extension = type?.Extension ?? "";
                if (extension.StartsWith("."))
                {
                    extension = extension.Substring(1);
                }

                string searchPattern = "*." + extension;
                foreach (string filePath in Directory.EnumerateFiles(overridePath, searchPattern, SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        yield return new ResourceIdentifier(fileName, type);
                    }
                }
            }

            // Enumerate from module directory
            if (!string.IsNullOrEmpty(_currentModule))
            {
                string modulePath = Path.Combine(_installationPath, "modules", _currentModule);
                if (Directory.Exists(modulePath))
                {
                    string extension = type?.Extension ?? "";
                    if (extension.StartsWith("."))
                    {
                        extension = extension.Substring(1);
                    }

                    string searchPattern = "*." + extension;
                    foreach (string filePath in Directory.EnumerateFiles(modulePath, searchPattern, SearchOption.AllDirectories))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            yield return new ResourceIdentifier(fileName, type);
                        }
                    }
                }
            }

            // TODO: Enumerate from HAK files
            // This requires HAK file parsing which is not yet implemented in Andastra.Parsing
        }

        public async Task<byte[]> GetResourceBytesAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                return LookupResource(id);
            }, ct);
        }

        #region Private Helper Methods

        private byte[] LookupResource(ResourceIdentifier id)
        {
            // Search in precedence order: Override -> Module -> HAK -> Base Game -> Hardcoded
            byte[] data = LookupInOverride(id);
            if (data != null)
            {
                return data;
            }

            data = LookupInModule(id);
            if (data != null)
            {
                return data;
            }

            data = LookupInHak(id);
            if (data != null)
            {
                return data;
            }

            data = LookupInBaseGame(id);
            if (data != null)
            {
                return data;
            }

            data = LookupHardcoded(id);
            return data;
        }

        private byte[] LookupInOverride(ResourceIdentifier id)
        {
            string overridePath = Path.Combine(_installationPath, "override");
            if (!Directory.Exists(overridePath))
            {
                return null;
            }

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            string resourcePath = Path.Combine(overridePath, id.ResName + "." + extension);
            if (File.Exists(resourcePath))
            {
                // Cache for performance
                string cacheKey = id.ResName + "." + extension;
                if (!_overrideCache.ContainsKey(cacheKey))
                {
                    _overrideCache[cacheKey] = File.ReadAllBytes(resourcePath);
                }
                return _overrideCache[cacheKey];
            }

            return null;
        }

        private byte[] LookupInModule(ResourceIdentifier id)
        {
            if (string.IsNullOrEmpty(_currentModule))
            {
                return null;
            }

            string modulePath = Path.Combine(_installationPath, "modules", _currentModule);
            if (!Directory.Exists(modulePath))
            {
                return null;
            }

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            string moduleResourcePath = Path.Combine(modulePath, id.ResName + "." + extension);
            if (File.Exists(moduleResourcePath))
            {
                return File.ReadAllBytes(moduleResourcePath);
            }

            return null;
        }

        private byte[] LookupInHak(ResourceIdentifier id)
        {
            // Search HAK files in reverse order (later HAK files override earlier ones)
            for (int i = _hakFiles.Count - 1; i >= 0; i--)
            {
                string hakPath = _hakFiles[i];
                if (File.Exists(hakPath))
                {
                    // TODO: Extract resource from HAK file
                    // This requires HAK file parsing which is not yet implemented in Andastra.Parsing
                    // For now, return null - HAK parsing will be implemented when Andastra.Parsing supports it
                }
            }

            return null;
        }

        private byte[] LookupInBaseGame(ResourceIdentifier id)
        {
            // Base game resources are typically in data directory or core game archives
            // Aurora Engine may use different base game resource locations
            // For now, check common locations
            string[] basePaths = new[]
            {
                Path.Combine(_installationPath, "data"),
                Path.Combine(_installationPath, "data", "core"),
                Path.Combine(_installationPath, "core")
            };

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            foreach (string basePath in basePaths)
            {
                if (Directory.Exists(basePath))
                {
                    string resourcePath = Path.Combine(basePath, id.ResName + "." + extension);
                    if (File.Exists(resourcePath))
                    {
                        return File.ReadAllBytes(resourcePath);
                    }
                }
            }

            return null;
        }

        private byte[] LookupHardcoded(ResourceIdentifier id)
        {
            // Hardcoded resources are engine-specific fallbacks
            // Aurora Engine may have some hardcoded resources, but this is engine-specific
            // For now, return null - hardcoded resources can be added later if needed
            return null;
        }

        private string LocateResourceInLocation(ResourceIdentifier id, SearchLocation location)
        {
            switch (location)
            {
                case SearchLocation.Override:
                    return LocateInOverride(id);
                case SearchLocation.Module:
                    return LocateInModule(id);
                case SearchLocation.Chitin:
                    return LocateInHak(id);
                case SearchLocation.Hardcoded:
                    return null; // Hardcoded resources don't have file paths
                default:
                    return null;
            }
        }

        private string LocateInOverride(ResourceIdentifier id)
        {
            string overridePath = Path.Combine(_installationPath, "override");
            if (!Directory.Exists(overridePath))
            {
                return null;
            }

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            string resourcePath = Path.Combine(overridePath, id.ResName + "." + extension);
            return File.Exists(resourcePath) ? resourcePath : null;
        }

        private string LocateInModule(ResourceIdentifier id)
        {
            if (string.IsNullOrEmpty(_currentModule))
            {
                return null;
            }

            string modulePath = Path.Combine(_installationPath, "modules", _currentModule);
            if (!Directory.Exists(modulePath))
            {
                return null;
            }

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            string moduleResourcePath = Path.Combine(modulePath, id.ResName + "." + extension);
            return File.Exists(moduleResourcePath) ? moduleResourcePath : null;
        }

        private string LocateInHak(ResourceIdentifier id)
        {
            // Search HAK files in reverse order (later HAK files override earlier ones)
            for (int i = _hakFiles.Count - 1; i >= 0; i--)
            {
                string hakPath = _hakFiles[i];
                if (File.Exists(hakPath))
                {
                    // TODO: Check if resource exists in HAK file
                    // This requires HAK file parsing which is not yet implemented
                    // For now, return HAK file path if it exists
                    return hakPath;
                }
            }

            return null;
        }

        private void InitializeHakFiles()
        {
            // HAK files are loaded per-module and specified in Module.ifo
            // This will be populated by the module loader when a module is loaded
            // For now, check for common HAK file locations
            string hakPath = Path.Combine(_installationPath, "hak");
            if (Directory.Exists(hakPath))
            {
                // TODO: Load HAK files from Module.ifo when module is loaded
                // For now, HAK files list remains empty until module loader sets it
            }
        }

        #endregion
    }
}


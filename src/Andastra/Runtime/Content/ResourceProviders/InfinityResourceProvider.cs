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
    /// Resource provider for Infinity Engine games (Baldur's Gate, Icewind Dale, Planescape: Torment).
    /// </summary>
    /// <remarks>
    /// Infinity Resource Provider:
    /// - Based on Infinity Engine resource loading system (Baldur's Gate, Icewind Dale, Planescape: Torment)
    /// - Infinity Engine uses BIF (BioWare Infinity Format) files and KEY index files for resource management
    /// - Resource precedence: OVERRIDE > MODULE > BIF (via KEY index) > HARDCODED
    /// - Original implementation: Infinity Engine uses CResMan/CResManager for resource loading
    /// - Resource lookup: Searches override directory first, then module-specific resources, then BIF archives via KEY file
    /// - Module context: Sets current module for module-specific resource lookups
    /// - Search order: Override directory → Module resources → BIF archives (indexed by KEY file) → Hardcoded resources
    /// - Resource types: Supports Infinity Engine resource types (ARE, WED, GAM, BIF, BCS, CRE, ITM, SPL, etc.)
    /// - Async loading: Provides async resource access for streaming and background loading
    /// - Resource enumeration: Can enumerate resources by type from BIF archives and override directory
    /// - Based on Infinity Engine's CResMan resource management system
    /// - TODO: Reverse engineer specific function addresses from Infinity Engine executables using Ghidra MCP
    ///   - Baldur's Gate: BaldurGate.exe resource loading functions
    ///   - Icewind Dale: IcewindDale.exe resource loading functions
    ///   - Planescape: Torment: PlanescapeTorment.exe resource loading functions
    /// </remarks>
    public class InfinityResourceProvider : IGameResourceProvider
    {
        private readonly string _installationPath;
        private readonly GameType _gameType;
        private string _currentModule;
        private readonly Dictionary<string, byte[]> _overrideCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _bifIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public InfinityResourceProvider(string installationPath, GameType gameType = GameType.Unknown)
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

            // Initialize BIF index from KEY file
            InitializeBifIndex();
        }

        public GameType GameType { get { return _gameType; } }

        /// <summary>
        /// Sets the current module context for resource lookups.
        /// </summary>
        public void SetCurrentModule(string moduleResRef)
        {
            _currentModule = moduleResRef;
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
                    SearchLocation.Chitin,
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

            // TODO: Enumerate from BIF archives via KEY index
            // This requires BIF file parsing which is not yet implemented in Andastra.Parsing
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
            // Search in precedence order: Override -> Module -> BIF -> Hardcoded
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

            data = LookupInBif(id);
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

            // Infinity Engine modules may have module-specific resources
            // Check in module directory if it exists
            string modulePath = Path.Combine(_installationPath, "modules");
            if (!Directory.Exists(modulePath))
            {
                return null;
            }

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            // Check for module-specific resource files
            string moduleResourcePath = Path.Combine(modulePath, _currentModule, id.ResName + "." + extension);
            if (File.Exists(moduleResourcePath))
            {
                return File.ReadAllBytes(moduleResourcePath);
            }

            return null;
        }

        private byte[] LookupInBif(ResourceIdentifier id)
        {
            // Look up resource in BIF index
            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            string resourceKey = id.ResName + "." + extension;
            if (_bifIndex.ContainsKey(resourceKey))
            {
                string bifPath = _bifIndex[resourceKey];
                if (File.Exists(bifPath))
                {
                    // TODO: Extract resource from BIF file
                    // This requires BIF file parsing which is not yet implemented in Andastra.Parsing
                    // For now, return null - BIF parsing will be implemented when Andastra.Parsing supports it
                    return null;
                }
            }

            return null;
        }

        private byte[] LookupHardcoded(ResourceIdentifier id)
        {
            // Hardcoded resources are engine-specific fallbacks
            // Infinity Engine may have some hardcoded resources, but this is engine-specific
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
                    return LocateInBif(id);
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

            string modulePath = Path.Combine(_installationPath, "modules");
            if (!Directory.Exists(modulePath))
            {
                return null;
            }

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            string moduleResourcePath = Path.Combine(modulePath, _currentModule, id.ResName + "." + extension);
            return File.Exists(moduleResourcePath) ? moduleResourcePath : null;
        }

        private string LocateInBif(ResourceIdentifier id)
        {
            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            string resourceKey = id.ResName + "." + extension;
            if (_bifIndex.ContainsKey(resourceKey))
            {
                return _bifIndex[resourceKey];
            }

            return null;
        }

        private void InitializeBifIndex()
        {
            // Look for KEY file (chitin.key or similar)
            // Infinity Engine uses KEY files to index BIF archives
            string[] keyFiles = new[]
            {
                Path.Combine(_installationPath, "chitin.key"),
                Path.Combine(_installationPath, "data", "chitin.key"),
                Path.Combine(_installationPath, "KEYFILE.KEY")
            };

            string keyFilePath = null;
            foreach (string keyFile in keyFiles)
            {
                if (File.Exists(keyFile))
                {
                    keyFilePath = keyFile;
                    break;
                }
            }

            if (keyFilePath == null)
            {
                // No KEY file found - BIF index will be empty
                // Resources can still be loaded from override directory
                return;
            }

            // TODO: Parse KEY file to build BIF index
            // KEY file format:
            // - Header with BIF file count and resource count
            // - BIF file entries (filename, size, etc.)
            // - Resource entries (resource name, type, BIF index, offset, size)
            // This requires KEY file parsing which is not yet implemented in Andastra.Parsing
            // For now, BIF index remains empty - KEY parsing will be implemented when Andastra.Parsing supports it
        }

        #endregion
    }
}


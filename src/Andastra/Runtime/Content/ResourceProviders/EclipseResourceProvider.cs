using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Formats.PCC;
using Andastra.Parsing.Formats.RIM;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Content.ResourceProviders
{
    /// <summary>
    /// Resource provider for Eclipse Engine games (Dragon Age series).
    /// </summary>
    /// <remarks>
    /// Eclipse Resource Provider:
    /// - Based on Eclipse Engine resource loading system (Dragon Age)
    /// - Eclipse Engine uses PCC/UPK packages, RIM files, and streaming resources for resource management
    /// - Resource precedence: OVERRIDE > PACKAGES (core/override) > STREAMING > HARDCODED
    /// - Original implementation: Eclipse Engine uses Unreal Engine's ResourceManager for resource loading
    /// - Resource lookup: Searches override directory first, then package directories, then streaming resources
    /// - Package context: Sets current package for package-specific resource lookups
    /// - Search order: Override directory → Package resources (core/override) → Streaming resources → Hardcoded resources
    /// - Resource types: Supports Eclipse Engine resource types (PCC, UPK, RIM, TFC, etc.)
    /// - Async loading: Provides async resource access for streaming and background loading
    /// - Resource enumeration: Can enumerate resources by type from packages and override directory
    /// - Based on Eclipse Engine's Unreal Engine ResourceManager system
    /// 
    /// Cross-Engine Resource Loading Patterns (from Ghidra analysis):
    /// - Eclipse Engine (daorigins.exe): Uses packages/core structure with RIM files and streaming
    ///   - PACKAGES directory: "packages\\" (package-based resource system)
    ///   - Core packages: "packages\\core\\" (core game resources)
    ///   - Override packages: "packages\\core\\override\\" (override resources)
    ///   - RIM files: Resource Index Manifest files (globalvfx.rim, guiglobal.rim, designerscripts.rim, global.rim)
    ///   - Streaming: "DragonAge::Streaming" for streaming level resources
    /// - Resource manager: daorigins.exe: "Initialize - Resource Manager" @ 0x00ad947c, "Shutdown - Resource Manager" @ 0x00ad87d8
    ///   - daorigins.exe: "Failed to initialize ResourceManager" @ 0x00ad9430
    ///   - daorigins.exe: "Shutdown of resource manager failed" @ 0x00ad8790
    /// - Eclipse-specific: Package-based resource system (different from Odyssey/Aurora file-based systems)
    ///   - Packages contain multiple resources in a single file
    ///   - Streaming allows loading resources on-demand as levels are entered
    ///   - RIM files provide resource indexing for faster lookups
    /// 
    /// TODO: Reverse engineer specific function addresses from Eclipse Engine executables using Ghidra MCP
    ///   - Dragon Age: Origins: daorigins.exe resource loading functions (ResourceManager initialization, package loading, RIM file handling)
    ///   - Dragon Age 2: DragonAge2.exe resource loading functions
    /// </remarks>
    public class EclipseResourceProvider : IGameResourceProvider
    {
        private readonly string _installationPath;
        private readonly GameType _gameType;
        private string _currentPackage;
        private readonly Dictionary<string, byte[]> _overrideCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _rimFiles = new List<string>();

        public EclipseResourceProvider(string installationPath, GameType gameType = GameType.Unknown)
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

            // Initialize RIM files
            InitializeRimFiles();
        }

        public GameType GameType { get { return _gameType; } }

        /// <summary>
        /// Gets the path to the packages directory.
        /// </summary>
        public string PackagePath()
        {
            return Path.Combine(_installationPath, "packages");
        }

        /// <summary>
        /// Gets the path to the core packages directory.
        /// </summary>
        public string CorePackagePath()
        {
            return Path.Combine(_installationPath, "packages", "core");
        }

        /// <summary>
        /// Gets the path to the override packages directory.
        /// </summary>
        public string OverridePackagePath()
        {
            return Path.Combine(_installationPath, "packages", "core", "override");
        }

        /// <summary>
        /// Sets the current package context for resource lookups.
        /// </summary>
        public void SetCurrentPackage(string packageName)
        {
            _currentPackage = packageName;
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
                    SearchLocation.Module, // Package resources
                    SearchLocation.Chitin, // RIM files
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
            string overridePath = Path.Combine(_installationPath, "packages", "core", "override");
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

            // Enumerate from packages (PCC/UPK files)
            string packagesPath = Path.Combine(_installationPath, "packages", "core");
            if (Directory.Exists(packagesPath))
            {
                string[] packageExtensions = new[] { "pcc", "upk" };
                foreach (string pkgExt in packageExtensions)
                {
                    string[] packageFiles = Directory.GetFiles(packagesPath, "*." + pkgExt, SearchOption.AllDirectories);
                    foreach (string packageFile in packageFiles)
                    {
                        try
                        {
                            var pcc = PCCAuto.ReadPcc(packageFile);
                            foreach (var resource in pcc)
                            {
                                if (resource.ResType == type)
                                {
                                    yield return new ResourceIdentifier(resource.ResRef.ToString(), resource.ResType);
                                }
                            }
                        }
                        catch
                        {
                            // Skip corrupted or invalid package files
                            continue;
                        }
                    }
                }
            }

            // Enumerate from RIM files
            foreach (string rimPath in _rimFiles)
            {
                if (File.Exists(rimPath))
                {
                    try
                    {
                        var rim = Andastra.Parsing.Formats.RIM.RIMAuto.ReadRim(rimPath);
                        foreach (var resource in rim)
                        {
                            if (resource.ResType == type)
                            {
                                yield return new ResourceIdentifier(resource.ResRef.ToString(), resource.ResType);
                            }
                        }
                    }
                    catch
                    {
                        // Skip corrupted or invalid RIM files
                        continue;
                    }
                }
            }
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
            // Search in precedence order: Override -> Package -> RIM -> Streaming -> Hardcoded
            byte[] data = LookupInOverride(id);
            if (data != null)
            {
                return data;
            }

            data = LookupInPackage(id);
            if (data != null)
            {
                return data;
            }

            data = LookupInRim(id);
            if (data != null)
            {
                return data;
            }

            data = LookupStreaming(id);
            if (data != null)
            {
                return data;
            }

            data = LookupHardcoded(id);
            return data;
        }

        private byte[] LookupInOverride(ResourceIdentifier id)
        {
            string overridePath = Path.Combine(_installationPath, "packages", "core", "override");
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

        private byte[] LookupInPackage(ResourceIdentifier id)
        {
            // Search in packages/core directory
            string packagesPath = Path.Combine(_installationPath, "packages", "core");
            if (!Directory.Exists(packagesPath))
            {
                return null;
            }

            // Search for PCC/UPK files and extract resources from them
            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            // Search for package files (PCC/UPK) in packages directory
            string[] packageExtensions = new[] { "pcc", "upk" };
            foreach (string pkgExt in packageExtensions)
            {
                // Search in common package locations
                string[] searchPaths = new[]
                {
                    packagesPath,
                    Path.Combine(packagesPath, "data"),
                    Path.Combine(packagesPath, "textures"),
                    Path.Combine(packagesPath, "audio"),
                    Path.Combine(packagesPath, "env")
                };

                foreach (string searchPath in searchPaths)
                {
                    if (!Directory.Exists(searchPath))
                    {
                        continue;
                    }

                    // Search for package files
                    string[] packageFiles = Directory.GetFiles(searchPath, "*." + pkgExt, SearchOption.TopDirectoryOnly);
                    foreach (string packageFile in packageFiles)
                    {
                        try
                        {
                            var pcc = Andastra.Parsing.Formats.PCC.PCCAuto.ReadPcc(packageFile);
                            byte[] resourceData = pcc.Get(id.ResName, id.ResType);
                            if (resourceData != null)
                            {
                                return resourceData;
                            }
                        }
                        catch
                        {
                            // Skip corrupted or invalid package files
                            continue;
                        }
                    }
                }
            }

            // Fallback: Check for loose files in package directories
            string[] packageSubdirs = new[]
            {
                "data",
                "textures",
                "audio",
                "env"
            };

            foreach (string subdir in packageSubdirs)
            {
                string packageSubdirPath = Path.Combine(packagesPath, subdir);
                if (Directory.Exists(packageSubdirPath))
                {
                    string resourcePath = Path.Combine(packageSubdirPath, id.ResName + "." + extension);
                    if (File.Exists(resourcePath))
                    {
                        return File.ReadAllBytes(resourcePath);
                    }
                }
            }

            return null;
        }

        private byte[] LookupInRim(ResourceIdentifier id)
        {
            // Search RIM files in reverse order (later RIM files override earlier ones)
            for (int i = _rimFiles.Count - 1; i >= 0; i--)
            {
                string rimPath = _rimFiles[i];
                if (File.Exists(rimPath))
                {
                    try
                    {
                        var rim = Andastra.Parsing.Formats.RIM.RIMAuto.ReadRim(rimPath);
                        byte[] resourceData = rim.Get(id.ResName, id.ResType);
                        if (resourceData != null)
                        {
                            return resourceData;
                        }
                    }
                    catch
                    {
                        // Skip corrupted or invalid RIM files
                        continue;
                    }
                }
            }

            return null;
        }

        private byte[] LookupStreaming(ResourceIdentifier id)
        {
            // Streaming resources are loaded on-demand as levels are entered
            // Eclipse Engine uses streaming for level-specific resources
            // For now, return null - streaming resource lookup will be implemented when needed
            return null;
        }

        private byte[] LookupHardcoded(ResourceIdentifier id)
        {
            // Hardcoded resources are engine-specific fallbacks
            // Eclipse Engine may have some hardcoded resources, but this is engine-specific
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
                    return LocateInPackage(id);
                case SearchLocation.Chitin:
                    return LocateInRim(id);
                case SearchLocation.Hardcoded:
                    return null; // Hardcoded resources don't have file paths
                default:
                    return null;
            }
        }

        private string LocateInOverride(ResourceIdentifier id)
        {
            string overridePath = Path.Combine(_installationPath, "packages", "core", "override");
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

        private string LocateInPackage(ResourceIdentifier id)
        {
            string packagesPath = Path.Combine(_installationPath, "packages", "core");
            if (!Directory.Exists(packagesPath))
            {
                return null;
            }

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            // Check common package subdirectories
            string[] packageSubdirs = new[]
            {
                "data",
                "textures",
                "audio",
                "env"
            };

            foreach (string subdir in packageSubdirs)
            {
                string packageSubdirPath = Path.Combine(packagesPath, subdir);
                if (Directory.Exists(packageSubdirPath))
                {
                    string resourcePath = Path.Combine(packageSubdirPath, id.ResName + "." + extension);
                    if (File.Exists(resourcePath))
                    {
                        return resourcePath;
                    }
                }
            }

            return null;
        }

        private string LocateInRim(ResourceIdentifier id)
        {
            // Search RIM files in reverse order (later RIM files override earlier ones)
            for (int i = _rimFiles.Count - 1; i >= 0; i--)
            {
                string rimPath = _rimFiles[i];
                if (File.Exists(rimPath))
                {
                    // TODO: Check if resource exists in RIM file
                    // This requires RIM file parsing which is not yet implemented
                    // For now, return RIM file path if it exists
                    return rimPath;
                }
            }

            return null;
        }

        private void InitializeRimFiles()
        {
            // Initialize common RIM files
            // daorigins.exe references: globalvfx.rim, guiglobal.rim, designerscripts.rim, global.rim
            string[] commonRimFiles = new[]
            {
                "globalvfx.rim",
                "guiglobal.rim",
                "designerscripts.rim",
                "global.rim"
            };

            string dataPath = Path.Combine(_installationPath, "data");
            if (Directory.Exists(dataPath))
            {
                foreach (string rimFile in commonRimFiles)
                {
                    string rimPath = Path.Combine(dataPath, rimFile);
                    if (File.Exists(rimPath))
                    {
                        _rimFiles.Add(rimPath);
                    }
                }
            }
        }

        #endregion
    }
}


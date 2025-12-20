using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
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

            // Enumerate from HAK files
            string hakPath = HakPath();
            if (Directory.Exists(hakPath))
            {
                string[] hakFiles = Directory.GetFiles(hakPath, "*.hak", SearchOption.TopDirectoryOnly);
                foreach (string hakFile in hakFiles)
                {
                    var resources = new List<ResourceIdentifier>();
                    try
                    {
                        // HAK files are ERF format
                        var erf = ERFAuto.ReadErf(hakFile);
                        foreach (var resource in erf)
                        {
                            if (resource.ResType == type)
                            {
                                resources.Add(new ResourceIdentifier(resource.ResRef.ToString(), resource.ResType));
                            }
                        }
                    }
                    catch
                    {
                        // Skip corrupted or invalid HAK files
                        continue;
                    }
                    foreach (var resource in resources)
                    {
                        yield return resource;
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
                    try
                    {
                        // HAK files are ERF format
                        var erf = ERFAuto.ReadErf(hakPath);
                        byte[] resourceData = erf.Get(id.ResName, id.ResType);
                        if (resourceData != null)
                        {
                            return resourceData;
                        }
                    }
                    catch
                    {
                        // Skip corrupted or invalid HAK files
                        continue;
                    }
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
            // TODO: implement hardcoded resource lookups and fully match 1:1 accuracy and exhaustively to nwn/nwn2/nwn:ee, providing commonalities here and implementing this function into subclasses for individual specifics.
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
            // Based on nwmain.exe: CExoResMan resource lookup searches HAK files in load order
            // HAK files are ERF format archives, resources are checked using ERF.Get()
            for (int i = _hakFiles.Count - 1; i >= 0; i--)
            {
                string hakPath = _hakFiles[i];
                if (File.Exists(hakPath))
                {
                    try
                    {
                        // HAK files are ERF format - check if resource exists in this HAK file
                        // Based on nwmain.exe: CExoResMan checks resource existence in HAK archives
                        var erf = ERFAuto.ReadErf(hakPath);
                        byte[] resourceData = erf.Get(id.ResName, id.ResType);
                        if (resourceData != null)
                        {
                            // Resource exists in this HAK file - return the HAK file path
                            // The offset within the HAK file would be available from ERFResource if needed
                            return hakPath;
                        }
                    }
                    catch
                    {
                        // Skip corrupted or invalid HAK files
                        continue;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Initializes HAK files list from Module.ifo for the current module.
        /// </summary>
        /// <remarks>
        /// HAK File Loading (Aurora):
        /// - HAK files are specified in Module.ifo in two ways:
        ///   1. Mod_HakList (preferred): List of HAK file entries (StructID 8), each with Mod_Hak field
        ///   2. Mod_Hak (obsolete): Single semicolon-separated string of HAK filenames
        /// - HAK files are loaded in order specified in Module.ifo
        /// - Resources in earlier HAK files override resources in later HAK files
        /// - HAK files are located in the "hak" directory under installation path
        /// - HAK filenames in Module.ifo do not include the .hak extension
        /// 
        /// Based on nwmain.exe: CExoResMan::LoadHakFiles (needs Ghidra address verification)
        /// - nwmain.exe: Module.ifo parsing extracts Mod_HakList or Mod_Hak field
        /// - nwmain.exe: HAK files are loaded in order and registered with resource manager
        /// - nwmain.exe: HAK file paths resolved from "hak" directory + filename + ".hak" extension
        /// 
        /// Official BioWare Documentation:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-IFO.md: Mod_HakList structure (StructID 8)
        /// - vendor/PyKotor/wiki/Bioware-Aurora-IFO.md: Mod_Hak field (obsolete, semicolon-separated)
        /// - HAK files are ERF format archives containing module resources
        /// </remarks>
        private void InitializeHakFiles()
        {
            // Clear existing HAK files list
            _hakFiles.Clear();

            // HAK files are loaded per-module and specified in Module.ifo
            // If no module is set, HAK files list remains empty
            if (string.IsNullOrEmpty(_currentModule))
            {
                return;
            }

            try
            {
                // Load Module.ifo from module directory
                string modulePath = ModulePath();
                string moduleIfoPath = Path.Combine(modulePath, _currentModule, "Module.ifo");
                
                if (!File.Exists(moduleIfoPath))
                {
                    // Module.ifo not found - HAK files list remains empty
                    return;
                }

                // Parse Module.ifo as GFF file
                byte[] ifoData = File.ReadAllBytes(moduleIfoPath);
                var gff = GFF.FromBytes(ifoData);
                if (gff?.Root == null)
                {
                    // Invalid GFF - HAK files list remains empty
                    return;
                }

                // Extract HAK file list from Module.ifo
                // Priority: Mod_HakList (preferred) > Mod_Hak (obsolete fallback)
                List<string> hakFileNames = new List<string>();

                // Try Mod_HakList first (preferred method)
                // Mod_HakList is a List field containing structs with StructID 8
                // Each struct has a Mod_Hak field (CExoString) with the HAK filename (without .hak extension)
                if (gff.Root.TryGetList("Mod_HakList", out GFFList hakList))
                {
                    // Iterate through HAK list entries
                    // Based on nwmain.exe: HAK files are loaded in order specified in list
                    // Earlier HAK files in list have higher priority (override later ones)
                    foreach (GFFStruct hakEntry in hakList)
                    {
                        // Extract Mod_Hak field from each entry
                        // Mod_Hak is a CExoString containing the HAK filename without .hak extension
                        string hakFileName = hakEntry.GetString("Mod_Hak");
                        if (!string.IsNullOrEmpty(hakFileName))
                        {
                            // Trim whitespace and add to list
                            hakFileName = hakFileName.Trim();
                            if (!string.IsNullOrEmpty(hakFileName))
                            {
                                hakFileNames.Add(hakFileName);
                            }
                        }
                    }
                }
                // Fallback to Mod_Hak (obsolete method) if Mod_HakList doesn't exist
                // Mod_Hak is a semicolon-separated string of HAK filenames (without .hak extension)
                else
                {
                    string hakField = gff.Root.GetString("Mod_Hak");
                    if (!string.IsNullOrEmpty(hakField))
                    {
                        // Split by semicolon and add each HAK filename
                        // Based on nwmain.exe: Mod_Hak field uses semicolon as separator
                        string[] hakNames = hakField.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string hakName in hakNames)
                        {
                            string trimmedName = hakName.Trim();
                            if (!string.IsNullOrEmpty(trimmedName))
                            {
                                hakFileNames.Add(trimmedName);
                            }
                        }
                    }
                }

                // Resolve HAK file paths and add to list
                // HAK files are located in the "hak" directory under installation path
                // HAK filenames in Module.ifo do not include the .hak extension
                string hakPath = HakPath();
                if (Directory.Exists(hakPath))
                {
                    foreach (string hakFileName in hakFileNames)
                    {
                        // Construct full path: hak directory + filename + .hak extension
                        // Based on nwmain.exe: HAK file path resolution pattern
                        string hakFilePath = Path.Combine(hakPath, hakFileName + ".hak");
                        
                        // Only add HAK file if it exists
                        // Based on nwmain.exe: Missing HAK files are skipped (not an error)
                        if (File.Exists(hakFilePath))
                        {
                            _hakFiles.Add(hakFilePath);
                        }
                    }
                }
            }
            catch
            {
                // On any error (file read, parsing, etc.), HAK files list remains empty
                // This is non-fatal - module can still load without HAK files
                _hakFiles.Clear();
            }
        }

        #endregion
    }
}


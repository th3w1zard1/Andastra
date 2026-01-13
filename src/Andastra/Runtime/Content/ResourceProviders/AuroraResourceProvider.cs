using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.KEY;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.BIF;
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
    /// Reverse Engineered Function Addresses (from Ghidra MCP analysis of nwmain.exe):
    ///
    /// CExoResMan Resource Manager:
    /// - CExoResMan::CExoResMan (constructor) @ 0x14018d6f0
    ///   - Initializes resource manager with memory management
    ///   - Sets up key table storage and resource tracking structures
    ///   - Configures memory limits based on system physical memory
    ///   - Initializes NWSync subsystem for resource synchronization
    ///
    /// Resource Loading Functions:
    /// - CExoResMan::Demand @ 0x14018ef90
    ///   - Main resource loading function that services resource requests
    ///   - Handles resource caching and demand tracking
    ///   - Routes to appropriate service function based on resource source type:
    ///     - Type 1: ServiceFromResFile (RES files)
    ///     - Type 2: ServiceFromEncapsulated (ERF/HAK files)
    ///     - Type 3: ServiceFromDirectory (override/modules directories)
    ///     - Type 5: ServiceFromManifest (manifest-based resources)
    /// - CRes::Demand @ 0x14018f300
    ///   - Wrapper function that calls CExoResMan::Demand via global g_pExoResMan
    ///   - Validates global resource manager is initialized
    ///
    /// Resource Service Functions:
    /// - CExoResMan::ServiceFromDirectory @ 0x140191e80
    ///   - Loads resources from directory-based sources (override/modules)
    ///   - Used for override directory and module-specific resource directories
    /// - CExoResMan::ServiceFromEncapsulated @ 0x140192cf0
    ///   - Loads resources from ERF-format archives (HAK files, module files)
    ///   - Handles encapsulated resource file format parsing
    /// - CExoResMan::ServiceFromResFile @ 0x140193b80
    ///   - Loads resources from RES format files (legacy resource format)
    ///
    /// Key Table Management (HAK File Registration):
    /// - CExoResMan::AddKeyTable @ 0x14018e330
    ///   - Registers a key table (resource index) with the resource manager
    ///   - Used to register HAK files, module files, and other resource archives
    ///   - Parameters: resource ID, path (CExoString), source type, priority, callback
    ///   - Source types: 1=FixedKeyTableFile, 2=ResourceDirectory, 3=EncapsulatedResourceFile
    ///   - Maintains ordered list of key tables for resource lookup precedence
    ///   - Checks for duplicate registrations and rebuilds table if re-added
    /// - CExoResMan::AddKeyTableContents @ 0x140189280
    ///   - Adds resource entries from a key table file to an existing key table
    ///
    /// Module.ifo HAK File Parsing:
    /// - Module.ifo parsing functions reference Mod_HakList and Mod_Hak strings:
    ///   - "Mod_HakList" string @ 0x140def690 (preferred method, GFF List field)
    ///   - "Mod_Hak" string @ 0x140def6a0 (obsolete method, semicolon-separated string)
    ///   - Functions at 0x14047f60e, 0x1404862d9, 0x140486325 reference these strings
    ///   - Module loading code extracts HAK file list from Module.ifo GFF structure
    ///   - HAK files are registered via AddKeyTable in the order specified in Module.ifo
    ///
    /// Directory Configuration:
    /// - "MODULES=" configuration string @ 0x140d80d20
    ///   - Referenced by function @ 0x14003f569 for module directory path construction
    /// - "OVERRIDE=" configuration string @ 0x140d80d50
    /// - Directory name strings: "modules" @ 0x140d80f38, "override" @ 0x140d80f40
    ///
    /// Resource Lookup Precedence (as implemented in CExoResMan::Demand):
    /// 1. Override directory (ServiceFromDirectory, type 3)
    /// 2. Module-specific resources (ServiceFromDirectory, type 3)
    /// 3. HAK files in load order (ServiceFromEncapsulated, type 2, registered via AddKeyTable)
    /// 4. Base game resources (ServiceFromResFile/ServiceFromEncapsulated)
    /// 5. Hardcoded resources (engine-specific fallbacks)
    ///
    /// Note: Neverwinter Nights 2 (nwn2main.exe) uses similar architecture but function addresses differ.
    /// Additional reverse engineering required for nwn2main.exe specific addresses.
    /// </remarks>
    public class AuroraResourceProvider : IGameResourceProvider
    {
        private readonly string _installationPath;
        private readonly GameType _gameType;
        private string _currentModule;
        private readonly Dictionary<string, byte[]> _overrideCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _hakFiles = new List<string>();

        // Base game resource caches (KEY/BIF files, ERF archives)
        private readonly Dictionary<string, KEY> _keyFileCache = new Dictionary<string, KEY>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BIF> _bifFileCache = new Dictionary<string, BIF>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ERF> _erfFileCache = new Dictionary<string, ERF>(StringComparer.OrdinalIgnoreCase);
        private readonly object _baseGameCacheLock = new object();

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

        /// <summary>
        /// Looks up base game resources from KEY/BIF files, ERF archives, and directories.
        /// </summary>
        /// <remarks>
        /// Base Game Resource Lookup (Aurora Engine):
        /// - Base game resources are loaded from KEY/BIF files, ERF archives, and directory structures
        /// - Based on nwmain.exe: CExoResMan::ServiceFromResFile @ 0x140193b80 (RES files) and
        ///   CExoResMan::ServiceFromEncapsulated @ 0x140192cf0 (ERF files) are used for base game resources
        /// - Resource lookup order (matching xoreos and original engine):
        ///   1. KEY/BIF files (chitin.key, patch.key, xp1.key, xp2.key, xp3.key, etc.)
        ///   2. ERF archives (gui_32bit.erf, xp1_gui.erf, xp2_gui.erf)
        ///   3. Base game directories (data, ambient, music, movies, portraits, tlk, database)
        /// - KEY files contain indexes mapping resource names to BIF files
        /// - BIF files contain the actual resource data
        /// - Later KEY files (xp3.key > xp2.key > xp1.key > patch.key > chitin.key) override earlier ones
        /// - Based on nwmain.exe: KEY files are registered via CExoResMan::AddKeyTable @ 0x14018e330
        /// - Based on vendor/xoreos/src/engines/nwn/nwn.cpp: initResources function loads KEY files in priority order
        /// </remarks>
        private byte[] LookupInBaseGame(ResourceIdentifier id)
        {
            if (id == null || id.ResType == null)
            {
                return null;
            }

            // 1. Search KEY/BIF files (highest priority for base game resources)
            // KEY files are loaded in reverse priority order (later files override earlier ones)
            // Based on nwmain.exe: CExoResMan loads KEY files and searches them in registration order
            byte[] resourceData = LookupInKeyBifFiles(id);
            if (resourceData != null)
            {
                return resourceData;
            }

            // 2. Search ERF archives (GUI textures, expansion resources)
            // Based on vendor/xoreos/src/engines/nwn/nwn.cpp: gui_32bit.erf, xp1_gui.erf, xp2_gui.erf
            resourceData = LookupInErfArchives(id);
            if (resourceData != null)
            {
                return resourceData;
            }

            // 3. Search base game directories (loose files)
            // Based on vendor/xoreos/src/engines/nwn/nwn.cpp: data, ambient, music, movies, portraits, tlk, database
            resourceData = LookupInBaseGameDirectories(id);
            if (resourceData != null)
            {
                return resourceData;
            }

            return null;
        }

        /// <summary>
        /// Looks up resources in KEY/BIF files.
        /// </summary>
        /// <remarks>
        /// KEY/BIF Resource Lookup:
        /// - KEY files are loaded in priority order: chitin.key, patch.key, xp1.key, xp1patch.key, xp2.key, xp2patch.key, xp3.key, xp3patch.key
        /// - Later KEY files override earlier ones (xp3.key has highest priority)
        /// - For each KEY file, look up the resource by ResRef and ResType
        /// - If found, extract BIF index and resource index from ResourceId
        /// - Load the corresponding BIF file and extract the resource data
        /// - Based on nwmain.exe: CExoResMan::ServiceFromResFile @ 0x140193b80 handles KEY/BIF resource loading
        /// - Based on vendor/xoreos/src/engines/nwn/nwn.cpp: KEY files are indexed in priority order 10-17
        /// </remarks>
        private byte[] LookupInKeyBifFiles(ResourceIdentifier id)
        {
            // KEY files to search in priority order (later files override earlier ones)
            // Based on vendor/xoreos/src/engines/nwn/nwn.cpp: initResources function
            string[] keyFiles = new[]
            {
                "chitin.key",      // Base game (priority 10)
                "patch.key",       // Base game patch (priority 11)
                "xp1.key",         // Shadows of Undrentide (priority 12)
                "xp1patch.key",    // SoU patch (priority 13)
                "xp2.key",         // Hordes of the Underdark (priority 14)
                "xp2patch.key",    // HotU patch (priority 15)
                "xp3.key",         // Kingmaker (priority 16)
                "xp3patch.key"     // Kingmaker patch (priority 17)
            };

            // Search KEY files in reverse order (highest priority first: xp3patch.key -> chitin.key)
            // This matches the engine behavior where later registered KEY files override earlier ones
            for (int i = keyFiles.Length - 1; i >= 0; i--)
            {
                string keyFileName = keyFiles[i];
                string keyFilePath = Path.Combine(_installationPath, keyFileName);

                if (!File.Exists(keyFilePath))
                {
                    continue;
                }

                try
                {
                    // Load and cache KEY file
                    // KEYBinaryReader.Load() already calls BuildLookupTables(), so no need to call it again
                    KEY keyFile;
                    lock (_baseGameCacheLock)
                    {
                        if (!_keyFileCache.TryGetValue(keyFilePath, out keyFile))
                        {
                            keyFile = KEYAuto.ReadKey(keyFilePath);
                            if (keyFile != null)
                            {
                                _keyFileCache[keyFilePath] = keyFile;
                            }
                        }
                    }

                    if (keyFile == null)
                    {
                        continue;
                    }

                    // Look up resource in KEY file
                    // Based on KEY.cs: GetResource method
                    KeyEntry keyEntry = keyFile.GetResource(id.ResName, id.ResType);
                    if (keyEntry == null)
                    {
                        continue;
                    }

                    // Extract BIF index and resource index from ResourceId
                    // ResourceId format: top 12 bits = BIF index, bottom 20 bits = resource index
                    int bifIndex = keyEntry.BifIndex;
                    int resIndex = keyEntry.ResIndex;

                    if (bifIndex < 0 || bifIndex >= keyFile.BifEntries.Count)
                    {
                        continue;
                    }

                    // Get BIF filename from KEY file
                    BifEntry bifEntry = keyFile.BifEntries[bifIndex];
                    string bifFileName = bifEntry.Filename;

                    // Resolve BIF file path (BIF files are relative to installation path)
                    // Based on vendor/xoreos/src/engines/nwn/nwn.cpp: BIF paths are resolved from installation root
                    string bifFilePath = Path.Combine(_installationPath, bifFileName.Replace('/', Path.DirectorySeparatorChar));

                    // Try case-insensitive search if exact path doesn't exist
                    if (!File.Exists(bifFilePath))
                    {
                        string bifDirectory = Path.GetDirectoryName(bifFilePath);
                        string bifFileNameOnly = Path.GetFileName(bifFileName);
                        if (Directory.Exists(bifDirectory))
                        {
                            string[] candidates = Directory.GetFiles(bifDirectory, "*.bif", SearchOption.TopDirectoryOnly)
                                .Concat(Directory.GetFiles(bifDirectory, "*.bzf", SearchOption.TopDirectoryOnly))
                                .ToArray();
                            foreach (string candidate in candidates)
                            {
                                if (string.Equals(Path.GetFileName(candidate), bifFileNameOnly, StringComparison.OrdinalIgnoreCase))
                                {
                                    bifFilePath = candidate;
                                    break;
                                }
                            }
                        }
                    }

                    if (!File.Exists(bifFilePath))
                    {
                        continue;
                    }

                    // Load and cache BIF file
                    BIF bifFile;
                    lock (_baseGameCacheLock)
                    {
                        if (!_bifFileCache.TryGetValue(bifFilePath, out bifFile))
                        {
                            try
                            {
                                var bifReader = new BIFBinaryReader(bifFilePath);
                                bifFile = bifReader.Load();

                                // Merge KEY data to populate ResRef names in BIF resources
                                // This matches PyKotor behavior where KEY data is merged into BIF for name resolution
                                MergeKeyDataIntoBif(bifFile, keyFilePath, bifFilePath);

                                if (bifFile != null)
                                {
                                    bifFile.BuildLookupTables();
                                    _bifFileCache[bifFilePath] = bifFile;
                                }
                            }
                            catch
                            {
                                // Skip corrupted BIF files
                                continue;
                            }
                        }
                    }

                    if (bifFile == null)
                    {
                        continue;
                    }

                    // Extract resource from BIF file
                    // Based on BIF.cs: TryGetResource method
                    (bool found, BIFResource resource) = bifFile.TryGetResource(id.ResName, id.ResType);
                    if (found && resource != null && resource.Data != null && resource.Data.Length > 0)
                    {
                        return resource.Data;
                    }

                    // Also try by resource index if direct lookup fails (for cases where ResRef doesn't match)
                    if (resIndex >= 0 && resIndex < bifFile.Resources.Count)
                    {
                        BIFResource resourceByIndex = bifFile.Resources[resIndex];
                        if (resourceByIndex != null && resourceByIndex.ResType == id.ResType &&
                            resourceByIndex.Data != null && resourceByIndex.Data.Length > 0)
                        {
                            return resourceByIndex.Data;
                        }
                    }
                }
                catch
                {
                    // Skip corrupted KEY files or BIF files
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// Merges KEY file data into BIF resources to populate ResRef names.
        /// </summary>
        /// <remarks>
        /// KEY Data Merging:
        /// - BIF files contain resource data but may not have ResRef names
        /// - KEY files map ResourceId to ResRef names
        /// - This method merges KEY data into BIF resources for name-based lookups
        /// - Based on PyKotor: read_bif accepts key_source parameter and merges resource names
        /// - Based on Archives.cs: MergeKeyDataIntoBif method
        /// </remarks>
        private void MergeKeyDataIntoBif(BIF bifFile, string keyFilePath, string bifFilePath)
        {
            if (bifFile == null || string.IsNullOrEmpty(keyFilePath) || !File.Exists(keyFilePath))
            {
                return;
            }

            try
            {
                KEY keyFile;
                lock (_baseGameCacheLock)
                {
                    if (!_keyFileCache.TryGetValue(keyFilePath, out keyFile))
                    {
                        keyFile = KEYAuto.ReadKey(keyFilePath);
                        if (keyFile != null)
                        {
                            // KEYBinaryReader.Load() already calls BuildLookupTables(), so no need to call it again
                            _keyFileCache[keyFilePath] = keyFile;
                        }
                    }
                }

                if (keyFile == null)
                {
                    return;
                }

                // Find the BIF index for this BIF file
                string bifFileName = Path.GetFileName(bifFilePath);
                int bifIndex = -1;
                for (int i = 0; i < keyFile.BifEntries.Count; i++)
                {
                    if (string.Equals(keyFile.BifEntries[i].Filename, bifFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        bifIndex = i;
                        break;
                    }
                }

                if (bifIndex < 0)
                {
                    return;
                }

                // Create lookup: ResourceId -> KeyEntry for this BIF
                Dictionary<uint, KeyEntry> resourceIdToKeyEntry = new Dictionary<uint, KeyEntry>();
                foreach (KeyEntry keyEntry in keyFile.KeyEntries)
                {
                    if (keyEntry.BifIndex == bifIndex)
                    {
                        resourceIdToKeyEntry[keyEntry.ResourceId] = keyEntry;
                    }
                }

                // Merge ResRef names from KEY into BIF resources
                foreach (BIFResource bifResource in bifFile.Resources)
                {
                    uint resourceId = (uint)((bifIndex << 20) | bifResource.ResnameKeyIndex);
                    if (resourceIdToKeyEntry.TryGetValue(resourceId, out KeyEntry matchingKeyEntry))
                    {
                        // Update BIF resource ResRef from KEY entry
                        bifResource.ResRef = matchingKeyEntry.ResRef;
                    }
                }
            }
            catch
            {
                // Ignore merge errors - BIF may still be usable without KEY data
            }
        }

        /// <summary>
        /// Looks up resources in ERF archives (GUI textures, expansion resources).
        /// </summary>
        /// <remarks>
        /// ERF Archive Lookup:
        /// - ERF archives contain base game resources like GUI textures
        /// - Based on vendor/xoreos/src/engines/nwn/nwn.cpp: gui_32bit.erf, xp1_gui.erf, xp2_gui.erf are loaded
        /// - ERF files are searched in priority order (later files override earlier ones)
        /// - Based on nwmain.exe: CExoResMan::ServiceFromEncapsulated @ 0x140192cf0 handles ERF resource loading
        /// </remarks>
        private byte[] LookupInErfArchives(ResourceIdentifier id)
        {
            // ERF archives to search in priority order (later files override earlier ones)
            // Based on vendor/xoreos/src/engines/nwn/nwn.cpp: initResources function
            string[] erfFiles = new[]
            {
                "gui_32bit.erf",   // Base game GUI textures (priority 50)
                "xp1_gui.erf",     // SoU GUI textures (priority 51)
                "xp2_gui.erf"      // HotU GUI textures (priority 52)
            };

            // Search ERF files in reverse order (highest priority first)
            for (int i = erfFiles.Length - 1; i >= 0; i--)
            {
                string erfFileName = erfFiles[i];
                string erfFilePath = Path.Combine(_installationPath, erfFileName);

                if (!File.Exists(erfFilePath))
                {
                    continue;
                }

                try
                {
                    // Load and cache ERF file
                    ERF erfFile;
                    lock (_baseGameCacheLock)
                    {
                        if (!_erfFileCache.TryGetValue(erfFilePath, out erfFile))
                        {
                            erfFile = ERFAuto.ReadErf(erfFilePath);
                            if (erfFile != null)
                            {
                                _erfFileCache[erfFilePath] = erfFile;
                            }
                        }
                    }

                    if (erfFile == null)
                    {
                        continue;
                    }

                    // Look up resource in ERF file
                    // Based on ERF.cs: Get method
                    byte[] resourceData = erfFile.Get(id.ResName, id.ResType);
                    if (resourceData != null && resourceData.Length > 0)
                    {
                        return resourceData;
                    }
                }
                catch
                {
                    // Skip corrupted ERF files
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// Looks up resources in base game directories (loose files).
        /// </summary>
        /// <remarks>
        /// Base Game Directory Lookup:
        /// - Base game resources may be stored as loose files in various directories
        /// - Based on vendor/xoreos/src/engines/nwn/nwn.cpp: data, ambient, music, movies, portraits, tlk, database directories
        /// - Directories are searched in priority order (earlier directories have higher priority)
        /// - Based on nwmain.exe: CExoResMan::ServiceFromDirectory @ 0x140191e80 handles directory-based resource loading
        /// </remarks>
        private byte[] LookupInBaseGameDirectories(ResourceIdentifier id)
        {
            // Base game directories to search (in priority order)
            // Based on vendor/xoreos/src/engines/nwn/nwn.cpp: initResources function
            string[] baseDirectories = new[]
            {
                "data",        // Main data directory (priority 2)
                "ambient",     // Ambient sounds (priority 100)
                "music",       // Music files (priority 101)
                "movies",      // Movie files (priority 102)
                "portraits",   // Portrait images (priority 103)
                "tlk",         // Talk table files (priority 105)
                "database"     // Database files (priority 106)
            };

            string extension = id.ResType?.Extension ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            // Search directories in order (earlier directories have higher priority)
            foreach (string baseDir in baseDirectories)
            {
                string baseDirPath = Path.Combine(_installationPath, baseDir);
                if (!Directory.Exists(baseDirPath))
                {
                    continue;
                }

                // Search for resource file (case-insensitive)
                string resourceFileName = id.ResName + "." + extension;
                string resourceFilePath = Path.Combine(baseDirPath, resourceFileName);

                if (File.Exists(resourceFilePath))
                {
                    try
                    {
                        return File.ReadAllBytes(resourceFilePath);
                    }
                    catch
                    {
                        // Skip files that can't be read
                        continue;
                    }
                }

                // Try case-insensitive search if exact match doesn't exist
                try
                {
                    string[] candidates = Directory.GetFiles(baseDirPath, "*." + extension, SearchOption.TopDirectoryOnly);
                    foreach (string candidate in candidates)
                    {
                        string candidateFileName = Path.GetFileNameWithoutExtension(candidate);
                        if (string.Equals(candidateFileName, id.ResName, StringComparison.OrdinalIgnoreCase))
                        {
                            return File.ReadAllBytes(candidate);
                        }
                    }
                }
                catch
                {
                    // Skip directories that can't be searched
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// Looks up hardcoded (fallback) resources when normal resource lookup fails.
        /// </summary>
        /// <remarks>
        /// Hardcoded Resource Lookup (Aurora Engine):
        /// - Hardcoded resources are engine-specific fallback resources provided when normal resource lookup fails
        /// - Based on nwmain.exe: Hardcoded resources are used as last-resort fallbacks in CExoResMan::Demand @ 0x14018ef90
        /// - Common hardcoded resources (across all Aurora games):
        ///   - DefaultModel (MDL): Fallback model when model resource cannot be found
        ///   - DefaultIcon (TGA/DDS): Fallback icon when icon resource cannot be found
        ///   - DefaultACSounds (2DA): Default action/combat sounds table
        ///   - fnt_default (FNT): Default font when font resource cannot be found
        /// - Game-specific hardcoded resources are implemented in subclasses:
        ///   - NwnResourceProvider: NWN-specific hardcoded resources
        ///   - Nwn2ResourceProvider: NWN2-specific hardcoded resources
        ///   - NwnEEResourceProvider: NWN:EE-specific hardcoded resources
        /// - Resource lookup order: Override → Module → HAK → Base Game → Hardcoded (this function)
        /// - Based on nwmain.exe: DefaultModel string @ 0x140dc3a68, DefaultIcon string @ 0x140dc3a78, DefaultACSounds string @ 0x140dc6db8
        /// - nwmain.exe: DefaultModel referenced @ 0x14029e7f7, DefaultACSounds referenced in Load2DArrays function
        /// </remarks>
        protected virtual byte[] LookupHardcoded(ResourceIdentifier id)
        {
            if (id == null || id.ResType == null)
            {
                return null;
            }

            // Common hardcoded resources across all Aurora Engine games
            // These are fallback resources that exist in all NWN/NWN2/NWN:EE games

            // DefaultModel (MDL): Fallback model when model resource cannot be found
            // Based on nwmain.exe: DefaultModel string @ 0x140dc3a68, referenced @ 0x14029e7f7
            if (id.ResType == ResourceType.MDL && string.Equals(id.ResName, "DefaultModel", StringComparison.OrdinalIgnoreCase))
            {
                return GetHardcodedDefaultModel();
            }

            // DefaultIcon (TGA/DDS): Fallback icon when icon resource cannot be found
            // Based on nwmain.exe: DefaultIcon string @ 0x140dc3a78
            if ((id.ResType == ResourceType.TGA || id.ResType == ResourceType.DDS) &&
                string.Equals(id.ResName, "DefaultIcon", StringComparison.OrdinalIgnoreCase))
            {
                return GetHardcodedDefaultIcon(id.ResType);
            }

            // DefaultACSounds (2DA): Default action/combat sounds table
            // Based on nwmain.exe: DefaultACSounds string @ 0x140dc6db8, referenced in Load2DArrays function
            if (id.ResType == ResourceType.TwoDA &&
                string.Equals(id.ResName, "DefaultACSounds", StringComparison.OrdinalIgnoreCase))
            {
                return GetHardcodedDefaultACSounds();
            }

            // fnt_default (FNT): Default font when font resource cannot be found
            // Based on nwmain.exe: "fnt_default" string references in font loading code
            if (id.ResType == ResourceType.FNT &&
                (string.Equals(id.ResName, "fnt_default", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(id.ResName, "fnt_default_hr", StringComparison.OrdinalIgnoreCase)))
            {
                return GetHardcodedDefaultFont(id.ResName);
            }

            // No hardcoded resource found - return null
            return null;
        }

        /// <summary>
        /// Gets hardcoded DefaultModel resource data.
        /// </summary>
        /// <remarks>
        /// DefaultModel (MDL):
        /// - Fallback model used when model resource cannot be found
        /// - Based on nwmain.exe: DefaultModel string @ 0x140dc3a68, referenced @ 0x14029e7f7
        /// - Returns a minimal valid MDL file (simple box model) as fallback
        /// - Game-specific implementations can override this to provide game-specific default models
        /// </remarks>
        protected virtual byte[] GetHardcodedDefaultModel()
        {
            // Minimal valid MDL file: Simple box model as fallback
            // MDL format: ASCII text format with model definition
            // This is a minimal valid MDL that represents a simple box (1x1x1 unit cube)
            string mdlContent = @"newmodel defaultmodel
setsupermodel defaultmodel defaultmodel
beginmodel defaultmodel
  node ""box""
    parent ""
    position 0.0 0.0 0.0
    orientation 0.0 0.0 0.0
    scale 1.0 1.0 1.0
    trimesh ""box""
      vertices 8
        0.5 0.5 0.5
        -0.5 0.5 0.5
        -0.5 -0.5 0.5
        0.5 -0.5 0.5
        0.5 0.5 -0.5
        -0.5 0.5 -0.5
        -0.5 -0.5 -0.5
        0.5 -0.5 -0.5
      faces 12
        0 1 2
        0 2 3
        4 7 6
        4 6 5
        0 4 5
        0 5 1
        2 6 7
        2 7 3
        0 3 7
        0 7 4
        1 5 6
        1 6 2
endmodel
";
            return System.Text.Encoding.ASCII.GetBytes(mdlContent);
        }

        /// <summary>
        /// Gets hardcoded DefaultIcon resource data.
        /// </summary>
        /// <remarks>
        /// DefaultIcon (TGA/DDS):
        /// - Fallback icon used when icon resource cannot be found
        /// - Based on nwmain.exe: DefaultIcon string @ 0x140dc3a78
        /// - Returns a minimal valid TGA or DDS file (16x16 solid color icon) as fallback
        /// - Game-specific implementations can override this to provide game-specific default icons
        /// </remarks>
        protected virtual byte[] GetHardcodedDefaultIcon(ResourceType iconType)
        {
            if (iconType == ResourceType.TGA)
            {
                // Minimal valid TGA file: 16x16 RGBA image (gray color)
                // TGA header: 18 bytes
                // Image data: 16x16x4 = 1024 bytes (RGBA)
                byte[] tgaHeader = new byte[]
                {
                    0x00, // ID length
                    0x00, // Color map type (no color map)
                    0x02, // Image type (uncompressed true-color)
                    0x00, 0x00, 0x00, 0x00, 0x00, // Color map specification (not used)
                    0x00, 0x00, // X origin
                    0x00, 0x00, // Y origin
                    0x10, 0x00, // Width (16)
                    0x10, 0x00, // Height (16)
                    0x20, // Pixel depth (32-bit RGBA)
                    0x00  // Image descriptor
                };

                // Image data: 16x16 RGBA (gray color: 128, 128, 128, 255)
                byte[] imageData = new byte[16 * 16 * 4];
                for (int i = 0; i < imageData.Length; i += 4)
                {
                    imageData[i] = 128;     // B
                    imageData[i + 1] = 128; // G
                    imageData[i + 2] = 128; // R
                    imageData[i + 3] = 255; // A
                }

                byte[] tgaFile = new byte[tgaHeader.Length + imageData.Length];
                System.Buffer.BlockCopy(tgaHeader, 0, tgaFile, 0, tgaHeader.Length);
                System.Buffer.BlockCopy(imageData, 0, tgaFile, tgaHeader.Length, imageData.Length);
                return tgaFile;
            }
            else if (iconType == ResourceType.DDS)
            {
                // Minimal valid DDS file: 16x16 DXT1 compressed image (gray color)
                // DDS header: 128 bytes (DDS_MAGIC + DDS_HEADER)
                // Image data: DXT1 compressed (16x16 = 128 bytes compressed)
                byte[] ddsMagic = new byte[] { 0x44, 0x44, 0x53, 0x20 }; // "DDS "

                // DDS_HEADER structure (124 bytes)
                byte[] ddsHeader = new byte[124];
                ddsHeader[0] = 0x7C; // dwSize = 124
                ddsHeader[4] = 0x07; // dwFlags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT
                ddsHeader[8] = 0x10; // dwHeight = 16 (little-endian)
                ddsHeader[12] = 0x10; // dwWidth = 16 (little-endian)
                ddsHeader[16] = 0x00; // dwPitchOrLinearSize (DXT1: 128 bytes for 16x16)
                ddsHeader[20] = 0x80;
                ddsHeader[76] = 0x20; // dwSize = 32 (DDS_PIXELFORMAT size)
                ddsHeader[80] = 0x04; // dwFlags = DDPF_FOURCC
                ddsHeader[84] = 0x44; // dwFourCC = "DXT1"
                ddsHeader[85] = 0x58;
                ddsHeader[86] = 0x54;
                ddsHeader[87] = 0x31;
                ddsHeader[108] = 0x04; // dwCaps = DDSCAPS_TEXTURE

                // DXT1 compressed data: 16x16 image = 128 bytes (4x4 blocks, 8 bytes per block)
                // Gray color block: RGB(128, 128, 128) encoded as DXT1
                byte[] dxt1Data = new byte[128];
                // Each 4x4 block is 8 bytes: color0 (RGB565), color1 (RGB565), 32-bit index bits
                // Gray: RGB(128, 128, 128) ≈ RGB565(0x4210, 0x4210)
                for (int i = 0; i < dxt1Data.Length; i += 8)
                {
                    dxt1Data[i] = 0x10;     // color0 low byte
                    dxt1Data[i + 1] = 0x42; // color0 high byte
                    dxt1Data[i + 2] = 0x10; // color1 low byte
                    dxt1Data[i + 3] = 0x42; // color1 high byte
                    dxt1Data[i + 4] = 0x00; // index bits (all pixels use color0)
                    dxt1Data[i + 5] = 0x00;
                    dxt1Data[i + 6] = 0x00;
                    dxt1Data[i + 7] = 0x00;
                }

                byte[] ddsFile = new byte[ddsMagic.Length + ddsHeader.Length + dxt1Data.Length];
                System.Buffer.BlockCopy(ddsMagic, 0, ddsFile, 0, ddsMagic.Length);
                System.Buffer.BlockCopy(ddsHeader, 0, ddsFile, ddsMagic.Length, ddsHeader.Length);
                System.Buffer.BlockCopy(dxt1Data, 0, ddsFile, ddsMagic.Length + ddsHeader.Length, dxt1Data.Length);
                return ddsFile;
            }

            return null;
        }

        /// <summary>
        /// Gets hardcoded DefaultACSounds resource data.
        /// </summary>
        /// <remarks>
        /// DefaultACSounds (2DA):
        /// - Default action/combat sounds table used when DefaultACSounds.2da cannot be found
        /// - Based on nwmain.exe: DefaultACSounds string @ 0x140dc6db8, referenced in Load2DArrays function
        /// - Returns a minimal valid 2DA file with default action/combat sound mappings
        /// - Game-specific implementations can override this to provide game-specific default sound tables
        /// </remarks>
        protected virtual byte[] GetHardcodedDefaultACSounds()
        {
            // Minimal valid 2DA file: Default action/combat sounds table
            // 2DA format: Tab-separated values with header row
            // Based on nwmain.exe: Load2DArrays function loads DefaultACSounds.2da
            string twoDAContent = @"2DA V2.0

	LABEL	SOUND
0	*
1	*
2	*
3	*
4	*
5	*
6	*
7	*
8	*
9	*
10	*
";
            return System.Text.Encoding.ASCII.GetBytes(twoDAContent);
        }

        /// <summary>
        /// Gets hardcoded default font resource data.
        /// </summary>
        /// <remarks>
        /// Default Font (FNT):
        /// - Default font used when font resource cannot be found
        /// - Based on nwmain.exe: "fnt_default" and "fnt_default_hr" string references in font loading code
        /// - Returns a complete valid FNT file as fallback
        /// - FNT format: Binary format with font metadata, metrics, and texture reference
        /// - Based on Aurora engine font system: Fonts are texture-based (TGA files) with metadata in FNT
        /// - Game-specific implementations can override this to provide game-specific default fonts
        ///
        /// FNT File Format Structure (Aurora Engine):
        /// - Header (4 bytes): Magic "FNT " (0x46 0x4E 0x54 0x20)
        /// - Version (4 bytes): Format version (1 = V1.0)
        /// - Font Name (16 bytes): ResRef of font (null-terminated, padded)
        /// - Texture ResRef (16 bytes): ResRef of font texture (TGA file, null-terminated, padded)
        /// - Font Height (4 bytes, float): Font height in pixels
        /// - Font Width (4 bytes, float): Average character width in pixels
        /// - Baseline Height (4 bytes, float): Baseline offset from top
        /// - Spacing R (4 bytes, float): Horizontal spacing between characters
        /// - Spacing B (4 bytes, float): Vertical spacing between lines
        /// - Character Count (4 bytes, uint32): Number of characters in font (typically 256 for ASCII)
        /// - Texture Width (4 bytes, uint32): Font texture width in pixels
        /// - Texture Height (4 bytes, uint32): Font texture height in pixels
        /// - Characters Per Row (4 bytes, uint32): Characters per row in texture grid
        /// - Characters Per Column (4 bytes, uint32): Characters per column in texture grid
        /// - Reserved (128 bytes): Reserved for future use
        /// Total size: 224 bytes minimum
        ///
        /// Based on vendor/xoreos/src/graphics/aurora/texturefont.cpp: TextureFont loads fonts from textures
        /// Based on vendor/reone/src/libs/resource/provider/fonts.cpp: Fonts loaded by ResRef, actual resource is texture
        /// </remarks>
        protected virtual byte[] GetHardcodedDefaultFont(string fontName)
        {
            // Complete FNT file structure
            // Based on Aurora engine font system: FNT contains metadata, actual font is texture-based
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                // Header: Magic "FNT " (0x46 0x4E 0x54 0x20)
                writer.Write((byte)0x46); // 'F'
                writer.Write((byte)0x4E); // 'N'
                writer.Write((byte)0x54); // 'T'
                writer.Write((byte)0x20); // ' ' (space)

                // Version: 1 (V1.0)
                writer.Write((uint)1);

                // Font Name: ResRef (16 bytes, null-terminated, padded with zeros)
                // Default fonts: "fnt_default" or "fnt_default_hr"
                string fontResRef = fontName.Length > 15 ? fontName.Substring(0, 15) : fontName;
                byte[] fontNameBytes = System.Text.Encoding.ASCII.GetBytes(fontResRef);
                writer.Write(fontNameBytes);
                if (fontNameBytes.Length < 16)
                {
                    writer.Write(new byte[16 - fontNameBytes.Length]); // Pad with zeros
                }

                // Texture ResRef: Font texture (TGA file) - same as font name for default fonts
                // Aurora fonts use TGA textures, so texture ResRef matches font ResRef
                byte[] textureNameBytes = System.Text.Encoding.ASCII.GetBytes(fontResRef);
                writer.Write(textureNameBytes);
                if (textureNameBytes.Length < 16)
                {
                    writer.Write(new byte[16 - textureNameBytes.Length]); // Pad with zeros
                }

                // Font metrics (floats, 4 bytes each)
                // Default values for standard 16x16 font
                float fontHeight = 16.0f;
                float fontWidth = 16.0f;
                float baselineHeight = 14.0f; // Baseline slightly below top
                float spacingR = 0.0f; // No extra horizontal spacing
                float spacingB = 0.0f; // No extra vertical spacing

                // Adjust for high-resolution font if requested
                if (fontName.EndsWith("_hr", System.StringComparison.OrdinalIgnoreCase))
                {
                    fontHeight = 32.0f;
                    fontWidth = 32.0f;
                    baselineHeight = 28.0f;
                }

                writer.Write(fontHeight);
                writer.Write(fontWidth);
                writer.Write(baselineHeight);
                writer.Write(spacingR);
                writer.Write(spacingB);

                // Character Count: 256 (standard ASCII font)
                writer.Write((uint)256);

                // Texture dimensions (uint32, 4 bytes each)
                // Default font texture: 256x256 for 16x16 grid (16 chars per row/column)
                // High-res font texture: 512x512 for 32x32 grid (16 chars per row/column)
                uint textureWidth = fontName.EndsWith("_hr", System.StringComparison.OrdinalIgnoreCase) ? (uint)512 : (uint)256;
                uint textureHeight = textureWidth;
                uint charsPerRow = 16;
                uint charsPerCol = 16;

                writer.Write(textureWidth);
                writer.Write(textureHeight);
                writer.Write(charsPerRow);
                writer.Write(charsPerCol);

                // Reserved: 128 bytes for future use
                writer.Write(new byte[128]);

                return stream.ToArray();
            }
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
            // Based on nwmain.exe: CExoResMan::Demand @ 0x14018ef90 searches key tables in registration order
            // HAK files are ERF format archives, loaded via CExoResMan::ServiceFromEncapsulated @ 0x140192cf0
            for (int i = _hakFiles.Count - 1; i >= 0; i--)
            {
                string hakPath = _hakFiles[i];
                if (File.Exists(hakPath))
                {
                    try
                    {
                        // HAK files are ERF format - check if resource exists in this HAK file
                        // Based on nwmain.exe: CExoResMan::ServiceFromEncapsulated @ 0x140192cf0 checks HAK archives
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
        /// Based on nwmain.exe reverse engineering (Ghidra MCP analysis):
        /// - Module.ifo parsing functions reference Mod_HakList/Mod_Hak strings:
        ///   - "Mod_HakList" string @ 0x140def690, referenced by functions @ 0x14047f60e, 0x1404862d9
        ///   - "Mod_Hak" string @ 0x140def6a0, referenced by functions @ 0x14047f6b9, 0x140486325, 0x1409d5658
        /// - HAK files are registered via CExoResMan::AddKeyTable @ 0x14018e330
        ///   - Each HAK file is registered as an encapsulated resource file (type 3)
        ///   - HAK files are registered in the order specified in Module.ifo
        ///   - Earlier HAK files in the list have higher priority (checked first during resource lookup)
        /// - HAK file paths resolved from "hak" directory + filename + ".hak" extension
        /// - Resource lookup uses CExoResMan::ServiceFromEncapsulated @ 0x140192cf0 for HAK files
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
                    // nwmain.exe: Functions @ 0x14047f60e, 0x1404862d9 parse Mod_HakList from Module.ifo
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
                        // nwmain.exe: Functions @ 0x14047f6b9, 0x140486325, 0x1409d5658 parse Mod_Hak field
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
                        // nwmain.exe: HAK files registered via CExoResMan::AddKeyTable @ 0x14018e330
                        string hakFilePath = Path.Combine(hakPath, hakFileName + ".hak");

                        // Only add HAK file if it exists
                        // Based on nwmain.exe: Missing HAK files are skipped (not an error)
                        // nwmain.exe: AddKeyTable @ 0x14018e330 handles missing file gracefully
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

        public bool Exists(ResourceIdentifier id)
        {
            return ExistsAsync(id, CancellationToken.None).GetAwaiter().GetResult();
        }

        public byte[] GetResourceBytes(ResourceIdentifier id)
        {
            return GetResourceBytesAsync(id, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Loads a resource by ResRef and ResourceType synchronously.
        /// </summary>
        /// <param name="resRef">Resource reference.</param>
        /// <param name="resourceType">Resource type.</param>
        /// <returns>Resource data or null if not found.</returns>
        /// <remarks>
        /// This method provides synchronous resource loading by ResRef and ResourceType parameters.
        /// Creates a ResourceIdentifier internally and delegates to GetResourceBytes(ResourceIdentifier).
        /// Based on Aurora Engine resource loading system (KEY/BIF files, ERF archives, HAK files, Override directory).
        /// </remarks>
        public byte[] LoadResource(BioWare.NET.Common.ResRef resRef, ResourceType resourceType)
        {
            if (resRef == null || resRef.IsBlank() || resourceType == null || resourceType.IsInvalid)
            {
                return null;
            }

            var identifier = new ResourceIdentifier(resRef.ToString(), resourceType);
            return GetResourceBytes(identifier);
        }

        #endregion
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using BioWare.NET.Extract.Capsule;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Extract
{
    /// <summary>
    /// Manages resource lookup and location for a game installation.
    /// Handles searching across override, modules, chitin, texture packs, and stream directories.
    /// </summary>
    public class InstallationResourceManager : IResourceLookup
    {
        private readonly string _installPath;
        private readonly BioWareGame _game;
        private readonly Dictionary<string, Extract.Chitin.Chitin> _chitinCache = new Dictionary<string, Extract.Chitin.Chitin>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LazyCapsule> _capsuleCache = new Dictionary<string, LazyCapsule>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<FileResource>> _overrideCache = new Dictionary<string, List<FileResource>>(StringComparer.OrdinalIgnoreCase);

        public InstallationResourceManager(string installPath, BioWareGame game)
        {
            _installPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            _game = game;
        }

        /// <summary>
        /// Looks up a single resource by name and type.
        /// </summary>
        [CanBeNull]
        public ResourceResult LookupResource(
            string resname,
            ResourceType restype,
            [CanBeNull] SearchLocation[] searchOrder = null,
            [CanBeNull] string moduleRoot = null)
        {
            if (string.IsNullOrWhiteSpace(resname))
                return null;

            // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/installation.py:1151-1160
            // Original: if order is an empty list, return None. If None, use default order.
            // If searchOrder is an empty array (not null), return null immediately
            if (searchOrder != null && searchOrder.Length == 0)
            {
                return null;
            }

            // Default search order if not specified (null)
            if (searchOrder is null)
            {
                searchOrder = new[]
                {
                    SearchLocation.OVERRIDE,
                    SearchLocation.MODULES,
                    SearchLocation.CHITIN,
                    SearchLocation.TEXTURES_TPA,
                    SearchLocation.TEXTURES_TPB,
                    SearchLocation.TEXTURES_TPC,
                    SearchLocation.TEXTURES_GUI,
                    SearchLocation.MUSIC,
                    SearchLocation.SOUND,
                    SearchLocation.VOICE,
                    SearchLocation.LIPS,
                    SearchLocation.RIMS
                };
            }

            // Search in order
            foreach (SearchLocation location in searchOrder)
            {
                FileResource fileResource = SearchInLocation(resname, restype, location, moduleRoot);
                if (fileResource != null)
                {
                    byte[] data = fileResource.GetData();
                    var result = new ResourceResult(resname, restype, fileResource.FilePath, data);
                    result.SetFileResource(fileResource);
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Locates all instances of a resource across the installation.
        /// </summary>
        public List<LocationResult> LocateResource(
            string resname,
            ResourceType restype,
            [CanBeNull] SearchLocation[] searchOrder = null,
            [CanBeNull] string moduleRoot = null)
        {
            var results = new List<LocationResult>();

            if (string.IsNullOrWhiteSpace(resname))
                return results;

            // Default search order if not specified
            if (searchOrder is null || searchOrder.Length == 0)
            {
                searchOrder = new[]
                {
                    SearchLocation.OVERRIDE,
                    SearchLocation.MODULES,
                    SearchLocation.CHITIN,
                    SearchLocation.TEXTURES_TPA,
                    SearchLocation.TEXTURES_TPB,
                    SearchLocation.TEXTURES_TPC,
                    SearchLocation.TEXTURES_GUI,
                    SearchLocation.MUSIC,
                    SearchLocation.SOUND,
                    SearchLocation.VOICE,
                    SearchLocation.LIPS,
                    SearchLocation.RIMS
                };
            }

            // Search all locations and collect all matches
            foreach (SearchLocation location in searchOrder)
            {
                List<FileResource> resources = SearchLocationAll(resname, restype, location, moduleRoot);
                foreach (FileResource fileResource in resources)
                {
                    var locationResult = new LocationResult(fileResource.FilePath, fileResource.Offset, fileResource.Size);
                    locationResult.SetFileResource(fileResource);
                    results.Add(locationResult);
                }
            }

            return results;
        }

        /// <summary>
        /// Clears all cached resources, forcing a reload on next access.
        /// </summary>
        public void ClearCache()
        {
            _chitinCache.Clear();
            _capsuleCache.Clear();
            _overrideCache.Clear();
        }

        /// <summary>
        /// Reloads a specific module's resources.
        /// </summary>
        public void ReloadModule(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                return;

            string modulesPath = Installation.GetModulesPath(_installPath);
            if (!Directory.Exists(modulesPath))
                return;

            // Find the exact set of module files for this root, respecting .mod override behavior.
            string moduleRoot = Installation.GetModuleRoot(moduleName);
            List<string> moduleFiles = ModuleFileDiscovery.GetModuleFilePaths(modulesPath, moduleRoot, _game);

            // Remove from cache
            foreach (string moduleFile in moduleFiles)
            {
                _capsuleCache.Remove(moduleFile);
            }
        }

        private FileResource SearchInLocation(string resname, ResourceType restype, SearchLocation location, string moduleRoot)
        {
            List<FileResource> resources = SearchLocationAll(resname, restype, location, moduleRoot);
            return resources.FirstOrDefault();
        }

        private List<FileResource> SearchLocationAll(string resname, ResourceType restype, SearchLocation location, string moduleRoot)
        {
            var results = new List<FileResource>();

            switch (location)
            {
                case SearchLocation.OVERRIDE:
                    results.AddRange(SearchOverride(resname, restype));
                    break;

                case SearchLocation.MODULES:
                    results.AddRange(SearchModules(resname, restype, moduleRoot));
                    break;

                case SearchLocation.CHITIN:
                    results.AddRange(SearchChitin(resname, restype));
                    break;

                case SearchLocation.TEXTURES_TPA:
                    results.AddRange(SearchTexturePack(resname, restype, "swpc_tex_tpa.erf"));
                    break;

                case SearchLocation.TEXTURES_TPB:
                    results.AddRange(SearchTexturePack(resname, restype, "swpc_tex_tpb.erf"));
                    break;

                case SearchLocation.TEXTURES_TPC:
                    results.AddRange(SearchTexturePack(resname, restype, "swpc_tex_tpc.erf"));
                    break;

                case SearchLocation.TEXTURES_GUI:
                    results.AddRange(SearchTexturePack(resname, restype, "swpc_tex_gui.erf"));
                    break;

                case SearchLocation.MUSIC:
                    results.AddRange(SearchStreamDirectory(resname, restype, Installation.GetStreamMusicPath(_installPath)));
                    break;

                case SearchLocation.SOUND:
                    results.AddRange(SearchStreamDirectory(resname, restype, Installation.GetStreamSoundsPath(_installPath)));
                    break;

                case SearchLocation.VOICE:
                    // Try StreamVoice first (TSL), then StreamWaves (K1)
                    string voicePath = Installation.GetStreamVoicePath(_installPath);
                    if (Directory.Exists(voicePath))
                    {
                        results.AddRange(SearchStreamDirectory(resname, restype, voicePath));
                    }
                    else
                    {
                        string wavesPath = Installation.GetStreamWavesPath(_installPath);
                        results.AddRange(SearchStreamDirectory(resname, restype, wavesPath));
                    }
                    break;

                case SearchLocation.LIPS:
                    results.AddRange(SearchLipsDirectory(resname, restype));
                    break;

                case SearchLocation.RIMS:
                    results.AddRange(SearchRimsDirectory(resname, restype));
                    break;
            }

            return results;
        }

        private List<FileResource> SearchOverride(string resname, ResourceType restype)
        {
            var results = new List<FileResource>();
            string overridePath = Installation.GetOverridePath(_installPath);

            if (!Directory.Exists(overridePath))
                return results;

            // Check cache first
            string cacheKey = $"override_{resname}_{restype.Extension}";
            if (_overrideCache.TryGetValue(cacheKey, out List<FileResource> cached))
            {
                return cached;
            }

            // Search recursively in override directory
            string searchPattern = $"{resname}.{restype.Extension}";
            var files = Directory.GetFiles(overridePath, searchPattern, SearchOption.AllDirectories)
                .Select(f => FileResource.FromPath(f))
                .ToList();

            _overrideCache[cacheKey] = files;
            return files;
        }

        private List<FileResource> SearchModules(string resname, ResourceType restype, string moduleRoot)
        {
            var results = new List<FileResource>();
            string modulesPath = Installation.GetModulesPath(_installPath);

            if (!Directory.Exists(modulesPath))
                return results;

            List<string> moduleFiles;
            if (!string.IsNullOrWhiteSpace(moduleRoot))
            {
                // When moduleRoot is known, respect engine module piece selection:
                // - Prefer <root>.mod and ignore rim-like pieces
                // - Otherwise use <root>.rim + optional <root>_s.rim + (K2) <root>_dlg.erf
                moduleFiles = ModuleFileDiscovery.GetModuleFilePaths(modulesPath, moduleRoot, _game);
            }
            else
            {
                // No module root specified: search all recognized module containers in modules directory.
                moduleFiles = Directory.GetFiles(modulesPath)
                    .Where(f => ModuleFileDiscovery.IsModuleFile(Path.GetFileName(f)))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (moduleFiles.Count == 0)
            {
                return results;
            }

            // Search each module file
            foreach (string moduleFile in moduleFiles)
            {
                try
                {
                    LazyCapsule capsule = GetCapsule(moduleFile);
                    if (capsule is null)
                        continue;

                    List<FileResource> resources = capsule.GetResources();
                    FileResource match = resources.FirstOrDefault(r =>
                        r.ResName.Equals(resname, StringComparison.OrdinalIgnoreCase) &&
                        r.ResType == restype);

                    if (match != null)
                    {
                        results.Add(match);
                    }
                }
                catch
                {
                    // Skip corrupted or invalid module files
                    continue;
                }
            }

            return results;
        }

        private List<FileResource> SearchChitin(string resname, ResourceType restype)
        {
            var results = new List<FileResource>();
            string chitinPath = Installation.GetChitinPath(_installPath);

            if (!File.Exists(chitinPath))
                return results;

            Chitin.Chitin chitin = GetChitin(chitinPath);
            if (chitin is null)
                return results;

            // Search all resources in chitin
            foreach (FileResource resource in chitin)
            {
                if (resource.ResName.Equals(resname, StringComparison.OrdinalIgnoreCase) &&
                    resource.ResType == restype)
                {
                    results.Add(resource);
                }
            }

            return results;
        }

        private List<FileResource> SearchTexturePack(string resname, ResourceType restype, string packFileName)
        {
            var results = new List<FileResource>();
            string texturePacksPath = Installation.GetTexturePacksPath(_installPath);
            string packPath = Path.Combine(texturePacksPath, packFileName);

            if (!File.Exists(packPath))
                return results;

            LazyCapsule capsule = GetCapsule(packPath);
            if (capsule is null)
                return results;

            List<FileResource> resources = capsule.GetResources();
            FileResource match = resources.FirstOrDefault(r =>
                r.ResName.Equals(resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);

            if (match != null)
            {
                results.Add(match);
            }

            return results;
        }

        private List<FileResource> SearchStreamDirectory(string resname, ResourceType restype, string directoryPath)
        {
            var results = new List<FileResource>();

            if (!Directory.Exists(directoryPath))
                return results;

            // Search recursively for files matching the resource name and type
            string searchPattern = $"{resname}.{restype.Extension}";
            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories)
                .Select(f => FileResource.FromPath(f))
                .ToList();

            return files;
        }

        private List<FileResource> SearchLipsDirectory(string resname, ResourceType restype)
        {
            var results = new List<FileResource>();
            string lipsPath = Installation.GetLipsPath(_installPath);

            if (!Directory.Exists(lipsPath))
                return results;

            // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/installation.py:479
            // Original: self.load_resources_dict(self.lips_path(), capsule_check=is_mod_file)
            // Search all capsule files (MOD, ERF, RIM) in lips directory
            var capsuleFiles = Directory.GetFiles(lipsPath)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".mod" || ext == ".erf" || ext == ".rim";
                })
                .ToList();

            foreach (string capsuleFile in capsuleFiles)
            {
                try
                {
                    LazyCapsule capsule = GetCapsule(capsuleFile);
                    if (capsule is null)
                        continue;

                    List<FileResource> resources = capsule.GetResources();
                    FileResource match = resources.FirstOrDefault(r =>
                        r.ResName.Equals(resname, StringComparison.OrdinalIgnoreCase) &&
                        r.ResType == restype);

                    if (match != null)
                    {
                        results.Add(match);
                    }
                }
                catch
                {
                    // Skip corrupted or invalid ERF files
                    continue;
                }
            }

            return results;
        }

        private List<FileResource> SearchRimsDirectory(string resname, ResourceType restype)
        {
            var results = new List<FileResource>();
            string rimsPath = Installation.GetRimsPath(_installPath);

            if (!Directory.Exists(rimsPath))
                return results;

            // Search all RIM files in rims directory
            var rimFiles = Directory.GetFiles(rimsPath, "*.rim", SearchOption.TopDirectoryOnly);

            foreach (string rimFile in rimFiles)
            {
                LazyCapsule capsule = GetCapsule(rimFile);
                if (capsule is null)
                    continue;

                List<FileResource> resources = capsule.GetResources();
                FileResource match = resources.FirstOrDefault(r =>
                    r.ResName.Equals(resname, StringComparison.OrdinalIgnoreCase) &&
                    r.ResType == restype);

                if (match != null)
                {
                    results.Add(match);
                }
            }

            return results;
        }

        private Chitin.Chitin GetChitin(string chitinPath)
        {
            if (_chitinCache.TryGetValue(chitinPath, out Chitin.Chitin cached))
            {
                return cached;
            }

            try
            {
                var chitin = new Chitin.Chitin(chitinPath);
                _chitinCache[chitinPath] = chitin;
                return chitin;
            }
            catch
            {
                return null;
            }
        }

        private LazyCapsule GetCapsule(string capsulePath)
        {
            if (_capsuleCache.TryGetValue(capsulePath, out LazyCapsule cached))
            {
                return cached;
            }

            try
            {
                var capsule = new LazyCapsule(capsulePath);
                _capsuleCache[capsulePath] = capsule;
                return capsule;
            }
            catch
            {
                return null;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/installation.py:935-942
        // Original: def chitin_resources(self) -> list[FileResource]:
        /// <summary>
        /// Returns a shallow copy of the list of FileResources stored in the Extract.Chitin.Chitin linked to the Installation.
        /// </summary>
        public List<FileResource> GetChitinResources()
        {
            string chitinPath = Installation.GetChitinPath(_installPath);
            Chitin.Chitin chitin = GetChitin(chitinPath);
            if (chitin == null)
            {
                return new List<FileResource>();
            }
            return chitin.GetResources();
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/installation.py:699-709
        // Original: def _load_patch_erf(self):
        /// <summary>
        /// Returns the list of FileResources stored in patch.erf (K1 only).
        /// </summary>
        public List<FileResource> GetPatchErfResources(BioWareGame game)
        {
            var results = new List<FileResource>();
            if (!game.IsK1())
            {
                return results;
            }

            string patchErfPath = Path.Combine(_installPath, "patch.erf");
            if (!File.Exists(patchErfPath))
            {
                return results;
            }

            LazyCapsule capsule = GetCapsule(patchErfPath);
            if (capsule != null)
            {
                results.AddRange(capsule.GetResources());
            }

            return results;
        }
    }
}

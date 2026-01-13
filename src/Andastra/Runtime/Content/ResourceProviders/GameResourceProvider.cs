using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using BioWare.NET.Extract.Capsule;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Content.ResourceProviders
{
    /// <summary>
    /// Resource provider that wraps BioWare.NET.Extract for unified resource access.
    /// </summary>
    /// <remarks>
    /// Game Resource Provider:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) resource loading system
    /// - Located via string references: "Resource" @ 0x007c14d4 (resource field)
    /// - Resource table errors: "CExoKeyTable::DestroyTable: Resource %s still in demand during table deletion" @ 0x007b6078
    /// - "CExoKeyTable::AddKey: Duplicate Resource " @ 0x007b6124 (duplicate resource key error)
    /// - Original implementation: Wraps BioWare.NET.Extract for resource access
    /// - Resource lookup: Uses installation resource system to locate files in archives (RIM, ERF, BIF, MOD)
    /// - Module context: Sets current module for module-specific resource lookups (module RIMs loaded first)
    /// - Search order: Module RIMs → Override directory → Main game archives (chitin.key/BIF files)
    /// - Resource types: Supports all KOTOR resource types (MDL, MDX, TPC, WAV, NCS, DLG, etc.)
    /// - Async loading: Provides async resource access for streaming and background loading
    /// - Resource enumeration: Can enumerate resources by type from installation archives
    /// - Based on BioWare.NET resource system which mirrors original engine's CExoKeyTable/ResourceManager
    /// </remarks>
    public class GameResourceProvider : IGameResourceProvider
    {
        private readonly Installation _installation;
        private readonly GameType _gameType;
        private string _currentModule;

        public GameResourceProvider(Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException("installation");
            _gameType = installation.Game == BioWareGame.K1 ? GameType.K1 : GameType.K2;
        }

        public GameType GameType { get { return _gameType; } }

        /// <summary>
        /// The installation this provider wraps.
        /// </summary>
        public Installation Installation { get { return _installation; } }

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

                BioWare.NET.Extract.ResourceResult result = _installation.Resources.LookupResource(
                    id.ResName,
                    id.ResType,
                    null,
                    _currentModule
                );

                if (result == null)
                {
                    return null;
                }

                byte[] data = result.Data;
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

                BioWare.NET.Extract.ResourceResult result = _installation.Resources.LookupResource(
                    id.ResName,
                    id.ResType,
                    null,
                    _currentModule
                );

                return result != null;
            }, ct);
        }

        public async Task<IReadOnlyList<Runtime.Content.Interfaces.LocationResult>> LocateAsync(
            ResourceIdentifier id,
            Runtime.Content.Interfaces.SearchLocation[] order,
            CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                BioWare.NET.Extract.SearchLocation[] kotorOrder = order != null
                    ? order.Select(ConvertSearchLocation).Where(l => l.HasValue).Select(l => l.Value).ToArray()
                    : null;

                List<BioWare.NET.Extract.LocationResult> results = _installation.Resources.LocateResource(
                    id.ResName,
                    id.ResType,
                    kotorOrder,
                    _currentModule
                );

                var converted = new List<Runtime.Content.Interfaces.LocationResult>();
                foreach (BioWare.NET.Extract.LocationResult r in results)
                {
                    converted.Add(new Runtime.Content.Interfaces.LocationResult
                    {
                        Location = ConvertBackSearchLocation(r.FilePath),
                        Path = r.FilePath,
                        Size = r.Size,
                        Offset = r.Offset
                    });
                }

                return (IReadOnlyList<Runtime.Content.Interfaces.LocationResult>)converted;
            }, ct);
        }

        /// <summary>
        /// Enumerates all resources of a specific type from all archives in the installation.
        /// </summary>
        /// <remarks>
        /// Resource enumeration implementation:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) resource enumeration system (CExoKeyTable resource table iteration)
        /// - Scans all archives and directories in precedence order: OVERRIDE > MODULES > CHITIN > TEXTUREPACKS > STREAMS > RIMS
        /// - Uses HashSet to track unique resources (duplicates from lower-priority locations are ignored)
        /// - Archive types scanned: ERF, MOD, RIM, BIF (via chitin.key), patch.erf (K1 only)
        /// - Directory types scanned: Override, StreamMusic, StreamSounds, StreamVoice, StreamWaves, Lips, Rims
        /// - Original implementation: Original engine enumerates resources via CExoKeyTable iteration
        /// - Matching PyKotor behavior: Enumerates all resources matching type from installation archives
        /// </remarks>
        public IEnumerable<ResourceIdentifier> EnumerateResources(ResourceType type)
        {
            if (type == null || type.IsInvalid)
            {
                yield break;
            }

            // Use HashSet to track unique resources (ResourceIdentifier already implements case-insensitive comparison)
            // Later resources from higher-priority locations will overwrite earlier ones
            var uniqueResources = new HashSet<ResourceIdentifier>();

            string installPath = _installation.Path;

            // 1. Enumerate from OVERRIDE directory (highest priority)
            string overridePath = BioWare.NET.Extract.Installation.GetOverridePath(installPath);
            if (Directory.Exists(overridePath))
            {
                string extension = type.Extension ?? "";
                if (extension.StartsWith("."))
                {
                    extension = extension.Substring(1);
                }

                string searchPattern = "*." + extension;
                foreach (string filePath in Directory.EnumerateFiles(overridePath, searchPattern, SearchOption.AllDirectories))
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            var identifier = new ResourceIdentifier(fileName, type);
                            uniqueResources.Add(identifier);
                        }
                    }
                    catch
                    {
                        // Skip files that can't be parsed as resources
                        continue;
                    }
                }
            }

            // 2. Enumerate from MODULES directory (module RIM/ERF/MOD files)
            string modulesPath = BioWare.NET.Extract.Installation.GetModulesPath(installPath);
            if (Directory.Exists(modulesPath))
            {
                var moduleFiles = Directory.GetFiles(modulesPath)
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext == ".rim" || ext == ".mod" || ext == ".erf";
                    })
                    .ToList();

                // Filter by current module if specified
                if (!string.IsNullOrWhiteSpace(_currentModule))
                {
                    string moduleRoot = BioWare.NET.Extract.Installation.GetModuleRoot(_currentModule);
                    moduleFiles = moduleFiles.Where(f =>
                    {
                        string fileRoot = BioWare.NET.Extract.Installation.GetModuleRoot(Path.GetFileName(f));
                        return fileRoot.Equals(moduleRoot, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                foreach (string moduleFile in moduleFiles)
                {
                    try
                    {
                        var capsule = new LazyCapsule(moduleFile);
                        List<FileResource> resources = capsule.GetResources();
                        foreach (FileResource resource in resources)
                        {
                            if (resource.ResType == type)
                            {
                                var identifier = new ResourceIdentifier(resource.ResName, resource.ResType);
                                uniqueResources.Add(identifier);
                            }
                        }
                    }
                    catch
                    {
                        // Skip corrupted or invalid module files
                        continue;
                    }
                }
            }

            // 3. Enumerate from CHITIN (BIF files via chitin.key)
            List<FileResource> chitinResources = _installation.Resources.GetChitinResources();
            foreach (FileResource resource in chitinResources)
            {
                if (resource.ResType == type)
                {
                    var identifier = new ResourceIdentifier(resource.ResName, resource.ResType);
                    uniqueResources.Add(identifier);
                }
            }

            // 4. Enumerate from patch.erf (K1 only)
            if (_gameType == GameType.K1)
            {
                List<FileResource> patchResources = _installation.Resources.GetPatchErfResources(_installation.Game);
                foreach (FileResource resource in patchResources)
                {
                    if (resource.ResType == type)
                    {
                        var identifier = new ResourceIdentifier(resource.ResName, resource.ResType);
                        uniqueResources.Add(identifier);
                    }
                }
            }

            // 5. Enumerate from TEXTURE_PACKS (ERF files: swpc_tex_tpa.erf, swpc_tex_tpb.erf, swpc_tex_tpc.erf, swpc_tex_gui.erf)
            string texturePacksPath = BioWare.NET.Extract.Installation.GetTexturePacksPath(installPath);
            if (Directory.Exists(texturePacksPath))
            {
                string[] texturePackFiles = new[]
                {
                    "swpc_tex_tpa.erf",
                    "swpc_tex_tpb.erf",
                    "swpc_tex_tpc.erf",
                    "swpc_tex_gui.erf"
                };

                foreach (string packFileName in texturePackFiles)
                {
                    string packPath = Path.Combine(texturePacksPath, packFileName);
                    if (!File.Exists(packPath))
                    {
                        continue;
                    }

                    try
                    {
                        var capsule = new LazyCapsule(packPath);
                        List<FileResource> resources = capsule.GetResources();
                        foreach (FileResource resource in resources)
                        {
                            if (resource.ResType == type)
                            {
                                var identifier = new ResourceIdentifier(resource.ResName, resource.ResType);
                                uniqueResources.Add(identifier);
                            }
                        }
                    }
                    catch
                    {
                        // Skip corrupted or invalid texture pack files
                        continue;
                    }
                }
            }

            // 6. Enumerate from STREAM directories (Music, Sounds, Voice, Waves, Lips)
            string[] streamDirectories = new[]
            {
                BioWare.NET.Extract.Installation.GetStreamMusicPath(installPath),
                BioWare.NET.Extract.Installation.GetStreamSoundsPath(installPath),
                BioWare.NET.Extract.Installation.GetStreamVoicePath(installPath),
                BioWare.NET.Extract.Installation.GetStreamWavesPath(installPath),
                BioWare.NET.Extract.Installation.GetLipsPath(installPath)
            };

            foreach (string streamDir in streamDirectories)
            {
                if (!Directory.Exists(streamDir))
                {
                    continue;
                }

                string extension = type.Extension ?? "";
                if (extension.StartsWith("."))
                {
                    extension = extension.Substring(1);
                }

                string searchPattern = "*." + extension;
                foreach (string filePath in Directory.EnumerateFiles(streamDir, searchPattern, SearchOption.AllDirectories))
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            var identifier = new ResourceIdentifier(fileName, type);
                            uniqueResources.Add(identifier);
                        }
                    }
                    catch
                    {
                        // Skip files that can't be parsed as resources
                        continue;
                    }
                }
            }

            // 7. Enumerate from LIPS directory (ERF/RIM capsule files)
            string lipsPath = BioWare.NET.Extract.Installation.GetLipsPath(installPath);
            if (Directory.Exists(lipsPath))
            {
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
                        var capsule = new LazyCapsule(capsuleFile);
                        List<FileResource> resources = capsule.GetResources();
                        foreach (FileResource resource in resources)
                        {
                            if (resource.ResType == type)
                            {
                                var identifier = new ResourceIdentifier(resource.ResName, resource.ResType);
                                uniqueResources.Add(identifier);
                            }
                        }
                    }
                    catch
                    {
                        // Skip corrupted or invalid capsule files
                        continue;
                    }
                }
            }

            // 8. Enumerate from RIMS directory (RIM files, TSL only)
            if (_gameType == GameType.K2)
            {
                string rimsPath = BioWare.NET.Extract.Installation.GetRimsPath(installPath);
                if (Directory.Exists(rimsPath))
                {
                    var rimFiles = Directory.GetFiles(rimsPath, "*.rim", SearchOption.TopDirectoryOnly);
                    foreach (string rimFile in rimFiles)
                    {
                        try
                        {
                            var capsule = new LazyCapsule(rimFile);
                            List<FileResource> resources = capsule.GetResources();
                            foreach (FileResource resource in resources)
                            {
                                if (resource.ResType == type)
                                {
                                    var identifier = new ResourceIdentifier(resource.ResName, resource.ResType);
                                    uniqueResources.Add(identifier);
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

            // Yield all unique resources
            foreach (ResourceIdentifier identifier in uniqueResources)
            {
                yield return identifier;
            }
        }

        public async Task<byte[]> GetResourceBytesAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                BioWare.NET.Extract.ResourceResult result = _installation.Resources.LookupResource(
                    id.ResName,
                    id.ResType,
                    null,
                    _currentModule
                );

                return result?.Data;
            }, ct);
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Resource lookup uses CExoKeyTable for resource resolution.
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

        #region Type Conversion

        private static BioWare.NET.Extract.SearchLocation? ConvertSearchLocation(Runtime.Content.Interfaces.SearchLocation location)
        {
            switch (location)
            {
                case Runtime.Content.Interfaces.SearchLocation.Override: return BioWare.NET.Extract.SearchLocation.OVERRIDE;
                case Runtime.Content.Interfaces.SearchLocation.Module: return BioWare.NET.Extract.SearchLocation.MODULES;
                case Runtime.Content.Interfaces.SearchLocation.Chitin: return BioWare.NET.Extract.SearchLocation.CHITIN;
                case Runtime.Content.Interfaces.SearchLocation.TexturePacks: return BioWare.NET.Extract.SearchLocation.TEXTURES_TPA;
                default: return null;
            }
        }

        private static Runtime.Content.Interfaces.SearchLocation ConvertBackSearchLocation(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Runtime.Content.Interfaces.SearchLocation.Chitin;
            }

            string lower = path.ToLowerInvariant();
            if (lower.Contains("override"))
            {
                return Runtime.Content.Interfaces.SearchLocation.Override;
            }
            if (lower.Contains("modules"))
            {
                return Runtime.Content.Interfaces.SearchLocation.Module;
            }
            if (lower.Contains("texturepacks"))
            {
                return Runtime.Content.Interfaces.SearchLocation.TexturePacks;
            }

            return Runtime.Content.Interfaces.SearchLocation.Chitin;
        }

        #endregion
    }
}


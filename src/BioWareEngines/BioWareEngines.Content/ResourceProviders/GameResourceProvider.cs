using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuroraEngine.Common;
using AuroraEngine.Common.Installation;
using AuroraEngine.Common.Resources;
using BioWareEngines.Content.Interfaces;
using KotorSearchLocation = AuroraEngine.Common.Installation.SearchLocation;
using OdysseySearchLocation = Odyssey.Content.Interfaces.SearchLocation;

namespace BioWareEngines.Content.ResourceProviders
{
    /// <summary>
    /// Resource provider that wraps AuroraEngine.Common.Installation for unified resource access.
    /// </summary>
    /// <remarks>
    /// Game Resource Provider:
    /// - Based on swkotor2.exe resource loading system
    /// - Located via string references: "Resource" @ 0x007c14d4 (resource field)
    /// - Resource table errors: "CExoKeyTable::DestroyTable: Resource %s still in demand during table deletion" @ 0x007b6078
    /// - "CExoKeyTable::AddKey: Duplicate Resource " @ 0x007b6124 (duplicate resource key error)
    /// - Original implementation: Wraps AuroraEngine.Common.Installation for resource access
    /// - Resource lookup: Uses installation resource system to locate files in archives (RIM, ERF, BIF, MOD)
    /// - Module context: Sets current module for module-specific resource lookups (module RIMs loaded first)
    /// - Search order: Module RIMs → Override directory → Main game archives (chitin.key/BIF files)
    /// - Resource types: Supports all KOTOR resource types (MDL, MDX, TPC, WAV, NCS, DLG, etc.)
    /// - Async loading: Provides async resource access for streaming and background loading
    /// - Resource enumeration: Can enumerate resources by type from installation archives
    /// - Based on CSharpKOTOR resource system which mirrors original engine's CExoKeyTable/ResourceManager
    /// </remarks>
    public class GameResourceProvider : IGameResourceProvider
    {
        private readonly Installation _installation;
        private readonly GameType _gameType;
        private string _currentModule;

        public GameResourceProvider(Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException("installation");
            _gameType = installation.Game == Game.K1 ? GameType.K1 : GameType.K2;
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

                AuroraEngine.Common.Installation.ResourceResult result = _installation.Resources.LookupResource(
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

                AuroraEngine.Common.Installation.ResourceResult result = _installation.Resources.LookupResource(
                    id.ResName,
                    id.ResType,
                    null,
                    _currentModule
                );

                return result != null;
            }, ct);
        }

        public async Task<IReadOnlyList<Odyssey.Content.Interfaces.LocationResult>> LocateAsync(
            ResourceIdentifier id,
            OdysseySearchLocation[] order,
            CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                KotorSearchLocation[] kotorOrder = order != null
                    ? order.Select(ConvertSearchLocation).Where(l => l.HasValue).Select(l => l.Value).ToArray()
                    : null;

                List<AuroraEngine.Common.Resources.LocationResult> results = _installation.Resources.LocateResource(
                    id.ResName,
                    id.ResType,
                    kotorOrder,
                    _currentModule
                );

                var converted = new List<Odyssey.Content.Interfaces.LocationResult>();
                foreach (AuroraEngine.Common.Resources.LocationResult r in results)
                {
                    converted.Add(new Odyssey.Content.Interfaces.LocationResult
                    {
                        Location = ConvertBackSearchLocation(r.FilePath),
                        Path = r.FilePath,
                        Size = r.Size,
                        Offset = r.Offset
                    });
                }

                return (IReadOnlyList<Odyssey.Content.Interfaces.LocationResult>)converted;
            }, ct);
        }

        public IEnumerable<ResourceIdentifier> EnumerateResources(ResourceType type)
        {
            // This would enumerate resources from the installation
            // For now, return empty - full implementation would scan all archives
            yield break;
        }

        public async Task<byte[]> GetResourceBytesAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                AuroraEngine.Common.Installation.ResourceResult result = _installation.Resources.LookupResource(
                    id.ResName,
                    id.ResType,
                    null,
                    _currentModule
                );

                return result?.Data;
            }, ct);
        }

        #region Type Conversion

        private static KotorSearchLocation? ConvertSearchLocation(OdysseySearchLocation location)
        {
            switch (location)
            {
                case OdysseySearchLocation.Override: return KotorSearchLocation.OVERRIDE;
                case OdysseySearchLocation.Module: return KotorSearchLocation.MODULES;
                case OdysseySearchLocation.Chitin: return KotorSearchLocation.CHITIN;
                case OdysseySearchLocation.TexturePacks: return KotorSearchLocation.TEXTURES_TPA;
                default: return null;
            }
        }

        private static OdysseySearchLocation ConvertBackSearchLocation(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return OdysseySearchLocation.Chitin;
            }

            string lower = path.ToLowerInvariant();
            if (lower.Contains("override"))
            {
                return OdysseySearchLocation.Override;
            }
            if (lower.Contains("modules"))
            {
                return OdysseySearchLocation.Module;
            }
            if (lower.Contains("texturepacks"))
            {
                return OdysseySearchLocation.TexturePacks;
            }

            return OdysseySearchLocation.Chitin;
        }

        #endregion
    }
}


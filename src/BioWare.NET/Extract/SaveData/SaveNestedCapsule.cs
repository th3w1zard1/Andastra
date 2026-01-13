using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.RIM;
using BioWare.NET.Resource;

namespace BioWare.NET.Extract.SaveData
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/savedata.py:1567-1848
    // Original: class SaveNestedCapsule
    public class SaveNestedCapsule
    {
        public List<ResourceIdentifier> ResourceOrder { get; } = new List<ResourceIdentifier>();
        public Dictionary<ResourceIdentifier, byte[]> ResourceData { get; } = new Dictionary<ResourceIdentifier, byte[]>();
        public Dictionary<ResourceIdentifier, ERF> CachedModules { get; } = new Dictionary<ResourceIdentifier, ERF>();
        public Dictionary<ResourceIdentifier, RIM> CachedRimModules { get; } = new Dictionary<ResourceIdentifier, RIM>();
        public Dictionary<ResourceIdentifier, byte[]> CachedCharacters { get; } = new Dictionary<ResourceIdentifier, byte[]>();
        public Dictionary<int, ResourceIdentifier> CachedCharacterIndices { get; } = new Dictionary<int, ResourceIdentifier>();
        public GFF InventoryGff { get; private set; }
        public ResourceIdentifier InventoryIdentifier { get; private set; }
        public GFF ReputeGff { get; private set; }
        public ResourceIdentifier ReputeIdentifier { get; private set; }

        private readonly string _path;

        public SaveNestedCapsule(string folderPath)
        {
            _path = Path.Combine(folderPath, "savegame.sav");
        }

        public void Load()
        {
            ResourceOrder.Clear();
            ResourceData.Clear();
            CachedModules.Clear();
            CachedRimModules.Clear();
            CachedCharacters.Clear();
            CachedCharacterIndices.Clear();
            InventoryGff = null;
            InventoryIdentifier = null;
            ReputeGff = null;
            ReputeIdentifier = null;

            if (!File.Exists(_path))
            {
                return;
            }

            byte[] bytes = File.ReadAllBytes(_path);
            ERF erf = ERFAuto.ReadErf(bytes);
            foreach (var res in erf)
            {
                var ident = new ResourceIdentifier(res.ResRef.ToString(), res.ResType);
                ResourceOrder.Add(ident);
                ResourceData[ident] = res.Data;

                if (ident.ResType == ResourceType.SAV)
                {
                    // Cached modules can be either ERF or RIM format
                    // Try ERF first (most common), then RIM
                    try
                    {
                        ERF cachedErf = ERFAuto.ReadErf(res.Data);
                        CachedModules[ident] = cachedErf;
                    }
                    catch
                    {
                        // Not ERF format, try RIM
                        try
                        {
                            RIM rim = RIMAuto.ReadRim(res.Data);
                            CachedRimModules[ident] = rim;
                        }
                        catch
                        {
                            // Neither ERF nor RIM - store as raw data
                            // This should not happen in valid save files, but we preserve the data
                        }
                    }
                }
                else if (ident.ResType == ResourceType.UTC)
                {
                    CachedCharacters[ident] = res.Data;
                    int? idx = ExtractCompanionIndex(ident.ResName);
                    if (idx.HasValue)
                    {
                        CachedCharacterIndices[idx.Value] = ident;
                    }
                }
                else if (ident.ResType == ResourceType.RES && ident.ResName.ToLowerInvariant() == "inventory")
                {
                    InventoryGff = GFF.FromBytes(res.Data);
                    InventoryIdentifier = ident;
                }
                else if (ident.ResType == ResourceType.FAC && ident.ResName.ToLowerInvariant() == "repute")
                {
                    ReputeGff = GFF.FromBytes(res.Data);
                    ReputeIdentifier = ident;
                }
            }
        }

        public void Save()
        {
            var erf = new ERF(ERFType.ERF, isSave: true);

            // Update ResourceData from cached modules before saving
            foreach (var kvp in CachedModules)
            {
                byte[] erfData = ERFAuto.BytesErf(kvp.Value, ResourceType.SAV);
                ResourceData[kvp.Key] = erfData;
            }
            foreach (var kvp in CachedRimModules)
            {
                byte[] rimData = RIMAuto.BytesRim(kvp.Value);
                ResourceData[kvp.Key] = rimData;
            }

            // Insert resources in preserved order
            foreach (var ident in ResourceOrder)
            {
                if (ResourceData.TryGetValue(ident, out var data))
                {
                    erf.SetData(ident.ResName, ident.ResType, data);
                }
            }

            // Include any resources not in ResourceOrder
            foreach (var kvp in ResourceData)
            {
                if (!ResourceOrder.Contains(kvp.Key))
                {
                    erf.SetData(kvp.Key.ResName, kvp.Key.ResType, kvp.Value);
                }
            }

            byte[] bytes = ERFAuto.BytesErf(erf, ResourceType.SAV);
            File.WriteAllBytes(_path, bytes);
        }

        public IEnumerable<KeyValuePair<ResourceIdentifier, byte[]>> IterSerializedResources()
        {
            HashSet<ResourceIdentifier> yielded = new HashSet<ResourceIdentifier>();
            foreach (var ident in ResourceOrder)
            {
                if (ResourceData.TryGetValue(ident, out var data))
                {
                    yielded.Add(ident);
                    yield return new KeyValuePair<ResourceIdentifier, byte[]>(ident, data);
                }
            }

            foreach (var kvp in ResourceData)
            {
                if (!yielded.Contains(kvp.Key))
                {
                    yield return kvp;
                }
            }
        }

        public void SetResource(ResourceIdentifier ident, byte[] data)
        {
            ResourceData[ident] = data;
            if (!ResourceOrder.Contains(ident))
            {
                ResourceOrder.Add(ident);
            }
        }

        public void RemoveResource(ResourceIdentifier ident)
        {
            ResourceData.Remove(ident);
            ResourceOrder.Remove(ident);
            CachedModules.Remove(ident);
            CachedRimModules.Remove(ident);
            CachedCharacters.Remove(ident);
            CachedCharacterIndices.Where(kvp => kvp.Value.Equals(ident)).ToList().ForEach(k => CachedCharacterIndices.Remove(k.Key));
            if (InventoryIdentifier != null && ident.Equals(InventoryIdentifier))
            {
                InventoryIdentifier = null;
                InventoryGff = null;
            }
            if (ReputeIdentifier != null && ident.Equals(ReputeIdentifier))
            {
                ReputeIdentifier = null;
                ReputeGff = null;
            }
        }

        // Convenience helpers for inventory/repute replacement
        public void SetInventory(byte[] inventoryRes)
        {
            var ident = new ResourceIdentifier("inventory", ResourceType.RES);
            InventoryIdentifier = ident;
            InventoryGff = GFF.FromBytes(inventoryRes);
            SetResource(ident, inventoryRes);
        }

        public void SetRepute(byte[] reputeFac)
        {
            var ident = new ResourceIdentifier("repute", ResourceType.FAC);
            ReputeIdentifier = ident;
            ReputeGff = GFF.FromBytes(reputeFac);
            SetResource(ident, reputeFac);
        }

        private static int? ExtractCompanionIndex(string resname)
        {
            string lower = resname.ToLowerInvariant();
            if (!lower.StartsWith("availnpc"))
            {
                return null;
            }
            string suffix = lower.Substring("availnpc".Length);
            if (int.TryParse(suffix, out int idx))
            {
                return idx;
            }
            return null;
        }

        /// <summary>
        /// Gets a cached module by name (ResRef).
        /// Cached modules can be either ERF or RIM format.
        /// </summary>
        /// <param name="moduleName">Module ResRef (e.g., "danm13", "ebo_m12aa")</param>
        /// <returns>ERF object if found as ERF, null if not found or is RIM format</returns>
        /// <remarks>
        /// Cached modules are stored as ResourceType.SAV (2057) in savegame.sav.
        /// The actual data inside can be either ERF or RIM format.
        /// Use GetCachedRimModule to check for RIM format modules.
        /// </remarks>
        public ERF GetCachedModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return null;
            }

            string moduleNameLower = moduleName.ToLowerInvariant();

            foreach (var kvp in CachedModules)
            {
                if (kvp.Key.ResName.ToLowerInvariant().StartsWith(moduleNameLower))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a cached RIM module by name (ResRef).
        /// </summary>
        /// <param name="moduleName">Module ResRef (e.g., "danm13", "ebo_m12aa")</param>
        /// <returns>RIM object if found as RIM, null if not found or is ERF format</returns>
        public RIM GetCachedRimModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return null;
            }

            string moduleNameLower = moduleName.ToLowerInvariant();

            foreach (var kvp in CachedRimModules)
            {
                if (kvp.Key.ResName.ToLowerInvariant().StartsWith(moduleNameLower))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets a cached module as ERF format.
        /// </summary>
        /// <param name="moduleName">Module ResRef</param>
        /// <param name="erf">ERF object containing module data</param>
        public void SetCachedModule(string moduleName, ERF erf)
        {
            if (string.IsNullOrEmpty(moduleName) || erf == null)
            {
                return;
            }

            var ident = new ResourceIdentifier(moduleName, ResourceType.SAV);
            CachedModules[ident] = erf;
            CachedRimModules.Remove(ident); // Remove from RIM cache if it was there

            // Serialize ERF to bytes and store in ResourceData
            byte[] erfData = ERFAuto.BytesErf(erf, ResourceType.SAV);
            SetResource(ident, erfData);
        }

        /// <summary>
        /// Sets a cached module as RIM format.
        /// </summary>
        /// <param name="moduleName">Module ResRef</param>
        /// <param name="rim">RIM object containing module data</param>
        public void SetCachedRimModule(string moduleName, RIM rim)
        {
            if (string.IsNullOrEmpty(moduleName) || rim == null)
            {
                return;
            }

            var ident = new ResourceIdentifier(moduleName, ResourceType.SAV);
            CachedRimModules[ident] = rim;
            CachedModules.Remove(ident); // Remove from ERF cache if it was there

            // Serialize RIM to bytes and store in ResourceData
            byte[] rimData = RIMAuto.BytesRim(rim);
            SetResource(ident, rimData);
        }

        /// <summary>
        /// Removes a cached module (both ERF and RIM formats).
        /// </summary>
        /// <param name="moduleName">Module ResRef</param>
        public void RemoveCachedModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return;
            }

            string moduleNameLower = moduleName.ToLowerInvariant();

            var toRemove = new List<ResourceIdentifier>();
            foreach (var kvp in CachedModules)
            {
                if (kvp.Key.ResName.ToLowerInvariant().StartsWith(moduleNameLower))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var kvp in CachedRimModules)
            {
                if (kvp.Key.ResName.ToLowerInvariant().StartsWith(moduleNameLower))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var ident in toRemove)
            {
                RemoveResource(ident);
            }
        }

        /// <summary>
        /// Gets all cached module names (ResRefs).
        /// </summary>
        /// <returns>List of module ResRefs</returns>
        public List<string> GetCachedModuleNames()
        {
            var names = new HashSet<string>();
            foreach (var kvp in CachedModules)
            {
                names.Add(kvp.Key.ResName);
            }
            foreach (var kvp in CachedRimModules)
            {
                names.Add(kvp.Key.ResName);
            }
            return names.ToList();
        }
    }
}

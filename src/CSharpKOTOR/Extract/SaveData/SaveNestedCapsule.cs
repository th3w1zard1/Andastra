using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Formats.ERF;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Extract.SaveData
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/savedata.py:1567-1848
    // Original: class SaveNestedCapsule
    public class SaveNestedCapsule
    {
        public List<ResourceIdentifier> ResourceOrder { get; } = new List<ResourceIdentifier>();
        public Dictionary<ResourceIdentifier, byte[]> ResourceData { get; } = new Dictionary<ResourceIdentifier, byte[]>();
        public Dictionary<ResourceIdentifier, ERF> CachedModules { get; } = new Dictionary<ResourceIdentifier, ERF>();
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
                    CachedModules[ident] = ERFAuto.ReadErf(res.Data);
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
    }
    }
}


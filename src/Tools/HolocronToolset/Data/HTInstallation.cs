using Andastra.Parsing.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Extract.Capsule;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;
using ResourceResult = Andastra.Parsing.Installation.ResourceResult;
using LocationResult = Andastra.Parsing.Extract.LocationResult;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:48
    // Original: class HTInstallation(Installation):
    public class HTInstallation
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:49-91
        // Original: TwoDA_PORTRAITS: str = TwoDARegistry.PORTRAITS
        public const string TwoDAAppearances = "appearance.2da";
        public const string TwoDABaseitems = "baseitems.2da";
        public const string TwoDACameras = "cameras.2da";
        public const string TwoDAClasses = "classes.2da";
        public const string TwoDACursors = "cursors.2da";
        public const string TwoDADialogAnims = "dialoganimations.2da";
        public const string TwoDADoors = "doortypes.2da";
        public const string TwoDAEmotions = "emotions.2da";
        public const string TwoDAEncDifficulties = "encdifficulty.2da";
        public const string TwoDAExpressions = "expressions.2da";
        public const string TwoDAFactions = "factions.2da";
        public const string TwoDAFeats = "feat.2da";
        public const string TwoDAGenders = "genders.2da";
        public const string TwoDAIprpAbilities = "iprp_abilities.2da";
        public const string TwoDAIprpAcmodtype = "iprp_acmodtype.2da";
        public const string TwoDAIprpAligngrp = "iprp_aligngrp.2da";
        public const string TwoDAIprpAmmotype = "iprp_ammotype.2da";
        public const string TwoDAIprpCombatdam = "iprp_combatdam.2da";
        public const string TwoDAIprpCosttable = "iprp_costtable.2da";
        public const string TwoDAIprpDamagetype = "iprp_damagetype.2da";
        public const string TwoDAIprpImmunity = "iprp_immunity.2da";
        public const string TwoDAIprpMonsterhit = "iprp_monsterhit.2da";
        public const string TwoDAIprpOnhit = "iprp_onhit.2da";
        public const string TwoDAIprpParamtable = "iprp_paramtable.2da";
        public const string TwoDAIprpProtection = "iprp_protection.2da";
        public const string TwoDAIprpSaveelement = "iprp_saveelement.2da";
        public const string TwoDAIprpSavingthrow = "iprp_savingthrow.2da";
        public const string TwoDAIprpWalk = "iprp_walk.2da";
        public const string TwoDAItemProperties = "itempropdef";
        public const string TwoDAPerceptions = "perceptions.2da";
        public const string TwoDAPlaceables = "placeables.2da";
        public const string TwoDAPlanets = "planetary.2da";
        public const string TwoDAPlot = "plot.2da";
        public const string TwoDAPortraits = "portraits.2da";
        public const string TwoDAPowers = "spells.2da";
        public const string TwoDARaces = "racialtypes.2da";
        public const string TwoDASkills = "skills.2da";
        public const string TwoDASoundsets = "soundset.2da";
        public const string TwoDASpeeds = "speeds.2da";
        public const string TwoDASubraces = "subraces.2da";
        public const string TwoDATraps = "traps.2da";
        public const string TwoDAUpgrades = "upcrystals.2da";
        public const string TwoDAVideoEffects = "videoeffects.2da";

        private readonly Installation _installation;
        private readonly Dictionary<string, TwoDA> _cache2da = new Dictionary<string, TwoDA>();
        private readonly Dictionary<string, Andastra.Parsing.Formats.TPC.TPC> _cacheTpc = new Dictionary<string, Andastra.Parsing.Formats.TPC.TPC>();
        private bool? _tsl;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:93-120
        // Original: def __init__(self, path: str | os.PathLike, name: str, *, tsl: bool | None = None, ...):
        public HTInstallation(string path, string name, bool? tsl = null)
        {
            _installation = new Installation(path);
            Name = name;
            _tsl = tsl;
        }

        public string Name { get; set; }
        public Installation Installation => _installation;
        public Game Game => _installation.Game;
        public string Path => _installation.Path;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def resource(self, resname: str, restype: ResourceType, ...) -> ResourceResult | None:
        [CanBeNull]
        public ResourceResult Resource(string resname, ResourceType restype, SearchLocation[] searchOrder = null, List<LazyCapsule> capsules = null)
        {
            if (capsules != null && capsules.Count > 0)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
                // Original: Use Resources method with capsules to get the resource
                var query = new ResourceIdentifier(resname, restype);
                var resources = Resources(new List<ResourceIdentifier> { query }, searchOrder, capsules);
                if (resources.ContainsKey(query) && resources[query] != null)
                {
                    return resources[query];
                }
            }
            return _installation.Resource(resname, restype, searchOrder);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1209-1285
        // Original: def resources(self, queries: list[ResourceIdentifier], ...) -> dict[ResourceIdentifier, ResourceResult | None]:
        public Dictionary<ResourceIdentifier, ResourceResult> Resources(
            List<ResourceIdentifier> queries,
            SearchLocation[] searchOrder = null,
            List<LazyCapsule> capsules = null)
        {
            var results = new Dictionary<ResourceIdentifier, ResourceResult>();
            if (queries == null || queries.Count == 0)
            {
                return results;
            }

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1239-1285
            // Original: locations: dict[ResourceIdentifier, list[LocationResult]] = self.locations(...)
            var locations = _installation.Locations(queries, searchOrder, capsules);
            var handles = new Dictionary<ResourceIdentifier, FileStream>();

            foreach (var query in queries)
            {
                if (locations.ContainsKey(query) && locations[query].Count > 0)
                {
                    var location = locations[query][0];
                    try
                    {
                        FileStream handle = null;
                        if (!handles.ContainsKey(query))
                        {
                            if (File.Exists(location.FilePath))
                            {
                                handle = File.OpenRead(location.FilePath);
                                handles[query] = handle;
                            }
                        }
                        else
                        {
                            handle = handles[query];
                        }

                        if (handle != null)
                        {
                            handle.Seek(location.Offset, SeekOrigin.Begin);
                            byte[] data = new byte[location.Size];
                            handle.Read(data, 0, location.Size);

                            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1272-1278
                            // Original: result = ResourceResult(...); result.set_file_resource(FileResource(...))
                            var result = new ResourceResult(query.ResName, query.ResType, location.FilePath, data);
                            // Create a new FileResource without circular reference - don't use location.FileResource
                            var fileResource = new FileResource(query.ResName, query.ResType, location.Size, location.Offset, location.FilePath);
                            result.SetFileResource(fileResource);
                            results[query] = result;
                        }
                        else
                        {
                            results[query] = null;
                        }
                    }
                    catch
                    {
                        results[query] = null;
                    }
                }
                else
                {
                    results[query] = null;
                }
            }

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1282-1283
            // Original: for handle in handles.values(): handle.close()
            foreach (var handle in handles.Values)
            {
                handle?.Dispose();
            }

            return results;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1297-1360
        // Original: def location(self, resname: str, restype: ResourceType, ...) -> list[LocationResult]:
        public List<LocationResult> Location(
            string resname,
            ResourceType restype,
            SearchLocation[] searchOrder = null,
            List<LazyCapsule> capsules = null)
        {
            var query = new ResourceIdentifier(resname, restype);
            var locations = _installation.Locations(new List<ResourceIdentifier> { query }, searchOrder, capsules);
            return locations.ContainsKey(query) ? locations[query] : new List<LocationResult>();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:444-469
        // Original: def ht_get_cache_2da(self, resname: str) -> 2DA | None:
        [CanBeNull]
        public TwoDA HtGetCache2DA(string resname)
        {
            resname = resname.ToLowerInvariant();
            if (!_cache2da.ContainsKey(resname))
            {
                ResourceResult result = _installation.Resource(
                    resname,
                    ResourceType.TwoDA,
                    new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN });
                if (result == null)
                {
                    return null;
                }
                var reader = new TwoDABinaryReader(result.Data);
                _cache2da[resname] = reader.Load();
            }
            return _cache2da[resname];
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:520-547
        // Original: def ht_batch_cache_2da(self, resnames: list[str], *, reload: bool = False):
        public void HtBatchCache2DA(List<string> resnames, bool reload = false)
        {
            var queries = new List<ResourceIdentifier>();
            if (reload)
            {
                queries.AddRange(resnames.Select(resname => new ResourceIdentifier(resname, ResourceType.TwoDA)));
            }
            else
            {
                queries.AddRange(resnames.Where(resname => !_cache2da.ContainsKey(resname.ToLowerInvariant()))
                    .Select(resname => new ResourceIdentifier(resname, ResourceType.TwoDA)));
            }

            if (queries.Count == 0)
            {
                return;
            }

            var resources = _installation.Locations(queries, new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN });
            foreach (var kvp in resources)
            {
                var locations = kvp.Value;
                if (locations == null || locations.Count == 0)
                {
                    continue;
                }

                // Get the first location result
                var location = locations[0];
                var resource = _installation.Resource(kvp.Key.ResName, kvp.Key.ResType, new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN });
                if (resource != null)
                {
                    var reader = new TwoDABinaryReader(resource.Data);
                    _cache2da[kvp.Key.ResName.ToLowerInvariant()] = reader.Load();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:673-677
        // Original: @property def tsl(self) -> bool:
        public bool Tsl
        {
            get
            {
                if (!_tsl.HasValue)
                {
                    _tsl = Game == Game.TSL;
                }
                return _tsl.Value;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:549-550
        // Original: def htClearCache2DA(self):
        public void HtClearCache2DA()
        {
            _cache2da.Clear();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:146-151
        // Original: def clear_all_caches(self):
        public void ClearAllCaches()
        {
            _cache2da.Clear();
            _cacheTpc.Clear();
            _installation.ClearCache();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:554-567
        // Original: def ht_get_cache_tpc(self, resname: str) -> TPC | None:
        [CanBeNull]
        public Andastra.Parsing.Formats.TPC.TPC HtGetCacheTpc(string resname)
        {
            resname = resname.ToLowerInvariant();
            if (!_cacheTpc.ContainsKey(resname))
            {
                var tex = _installation.Texture(
                    resname,
                    new[] { SearchLocation.OVERRIDE, SearchLocation.TEXTURES_TPA, SearchLocation.TEXTURES_GUI });
                if (tex != null)
                {
                    _cacheTpc[resname] = tex;
                }
            }
            return _cacheTpc.ContainsKey(resname) ? _cacheTpc[resname] : null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:569-584
        // Original: def ht_batch_cache_tpc(self, names: list[str], *, reload: bool = False):
        public void HtBatchCacheTpc(List<string> names, bool reload = false)
        {
            var queries = reload ? names.ToList() : names.Where(name => !_cacheTpc.ContainsKey(name.ToLowerInvariant())).ToList();

            if (queries.Count == 0)
            {
                return;
            }

            foreach (var resname in queries)
            {
                var tex = _installation.Texture(
                    resname,
                    new[] { SearchLocation.TEXTURES_TPA, SearchLocation.TEXTURES_GUI });
                if (tex != null)
                {
                    _cacheTpc[resname.ToLowerInvariant()] = tex;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:586-587
        // Original: def ht_clear_cache_tpc(self):
        public void HtClearCacheTpc()
        {
            _cacheTpc.Clear();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:608-619
        // Original: def get_item_base_name(self, base_item: int) -> str:
        public string GetItemBaseName(int baseItem)
        {
            try
            {
                TwoDA baseitems = HtGetCache2DA(TwoDABaseitems);
                if (baseitems == null)
                {
                    System.Console.WriteLine("Failed to retrieve `baseitems.2da` from your installation.");
                    return "Unknown";
                }
                return baseitems.GetCellString(baseItem, "label") ?? "Unknown";
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"An exception occurred while retrieving `baseitems.2da` from your installation: {ex.Message}");
                return "Unknown";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:621-623
        // Original: def get_model_var_name(self, model_variation: int) -> str:
        public string GetModelVarName(int modelVariation)
        {
            return modelVariation == 0 ? "Default" : $"Variation {modelVariation}";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:625-627
        // Original: def get_texture_var_name(self, texture_variation: int) -> str:
        public string GetTextureVarName(int textureVariation)
        {
            return textureVariation == 0 ? "Default" : $"Texture {textureVariation}";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:629-642
        // Original: def get_item_icon_path(self, base_item: int, model_variation: int, texture_variation: int) -> str:
        public string GetItemIconPath(int baseItem, int modelVariation, int textureVariation)
        {
            TwoDA baseitems = HtGetCache2DA(TwoDABaseitems);
            if (baseitems == null)
            {
                System.Console.WriteLine("Failed to retrieve `baseitems.2da` from your installation.");
                return "Unknown";
            }
            try
            {
                string itemClass = baseitems.GetCellString(baseItem, "itemclass") ?? "";
                int variation = modelVariation != 0 ? modelVariation : textureVariation;
                // Pad variation to 3 digits with leading zeros
                string variationStr = variation.ToString().PadLeft(3, '0');
                return $"i{itemClass}_{variationStr}";
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"An exception occurred while getting cell '{baseItem}' from `baseitems.2da`: {ex.Message}");
                return "Unknown";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:644-664
        // Original: def get_item_icon(self, base_item: int, model_variation: int, texture_variation: int) -> QPixmap:
        [CanBeNull]
        public Avalonia.Media.Imaging.Bitmap GetItemIcon(int baseItem, int modelVariation, int textureVariation)
        {
            // In Avalonia, we return Bitmap instead of QPixmap
            // Default icon would be loaded from resources, but for now we'll return null if icon can't be loaded
            string iconPath = GetItemIconPath(baseItem, modelVariation, textureVariation);
            System.Console.WriteLine($"Icon path: '{iconPath}'");
            try
            {
                // Extract just the filename (basename) and convert to lowercase
                string resname = System.IO.Path.GetFileName(iconPath).ToLowerInvariant();
                Andastra.Parsing.Formats.TPC.TPC texture = HtGetCacheTpc(resname);
                if (texture == null)
                {
                    return null;
                }
                // TODO: Convert TPC texture to Avalonia Bitmap
                // This would require decoding the TPC texture format and converting to Bitmap
                // For now, we'll just return null as a placeholder
                // The actual conversion would be: decode TPC -> get mipmap data -> create Bitmap from pixel data
                return null;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"An error occurred loading the icon at '{iconPath}' model variation '{modelVariation}' and texture variation '{textureVariation}': {ex.Message}");
                return null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def module_path(self) -> Path:
        public string ModulePath()
        {
            return Installation.GetModulesPath(Path);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def override_path(self) -> Path:
        public string OverridePath()
        {
            return Installation.GetOverridePath(Path);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def save_locations(self) -> list[Path]:
        public List<string> SaveLocations()
        {
            var locations = new List<string>();
            // Get save locations from installation
            // This will be implemented when save location detection is available
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (Tsl)
            {
                locations.Add(System.IO.Path.Combine(documentsPath, "Knights of the Old Republic II", "saves"));
            }
            else
            {
                locations.Add(System.IO.Path.Combine(documentsPath, "Knights of the Old Republic", "saves"));
            }
            return locations.Where(loc => Directory.Exists(loc)).ToList();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def core_resources(self) -> list[FileResource]:
        public List<FileResource> CoreResources()
        {
            return _installation.CoreResources();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def modules_list(self) -> list[str]:
        public virtual List<string> ModulesList()
        {
            var modules = new List<string>();
            string modulesPath = ModulePath();
            if (!Directory.Exists(modulesPath))
            {
                return modules;
            }

            // Get module files
            var moduleFiles = Directory.GetFiles(modulesPath, "*.rim")
                .Concat(Directory.GetFiles(modulesPath, "*.mod"))
                .Concat(Directory.GetFiles(modulesPath, "*.erf"))
                .Select(f => System.IO.Path.GetFileName(f))
                .ToList();

            modules.AddRange(moduleFiles);
            return modules;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def module_names(self) -> dict[str, str | None]:
        public virtual Dictionary<string, string> ModuleNames()
        {
            var moduleNames = new Dictionary<string, string>();
            string modulesPath = ModulePath();
            if (!Directory.Exists(modulesPath))
            {
                return moduleNames;
            }

            // Get module files
            var moduleFiles = Directory.GetFiles(modulesPath, "*.rim")
                .Concat(Directory.GetFiles(modulesPath, "*.mod"))
                .Concat(Directory.GetFiles(modulesPath, "*.erf"))
                .Select(f => System.IO.Path.GetFileName(f))
                .ToList();

            foreach (var moduleFile in moduleFiles)
            {
                // Try to get area name from module
                string areaName = GetModuleAreaName(moduleFile);
                moduleNames[moduleFile] = areaName;
            }

            return moduleNames;
        }

        private string GetModuleAreaName(string moduleFile)
        {
            // Try to get area name from module.ifo in the module
            // This will be implemented when module reading is available
            return "<Unknown Area>";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def override_list(self) -> list[str]:
        public List<string> OverrideList()
        {
            return _installation.OverrideList();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def texturepacks_list(self) -> list[str]:
        public List<string> TexturepacksList()
        {
            var texturePacks = new List<string>();
            string texturePacksPath = Installation.GetTexturePacksPath(Path);
            if (!Directory.Exists(texturePacksPath))
            {
                return texturePacks;
            }

            // Get texture pack files
            var packFiles = Directory.GetFiles(texturePacksPath, "*.erf")
                .Select(f => System.IO.Path.GetFileName(f))
                .ToList();
            texturePacks.AddRange(packFiles);
            return texturePacks;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def module_resources(self, module_name: str) -> list[FileResource]:
        public List<FileResource> ModuleResources(string moduleName)
        {
            var resources = new List<FileResource>();
            string modulesPath = ModulePath();
            if (!Directory.Exists(modulesPath))
            {
                return resources;
            }

            string moduleFile = System.IO.Path.Combine(modulesPath, moduleName);
            if (!File.Exists(moduleFile))
            {
                return resources;
            }

            try
            {
                // Use LazyCapsule to read module resources
                var capsule = new LazyCapsule(moduleFile);
                resources.AddRange(capsule.GetResources());
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load resources from module '{moduleName}': {ex}");
            }

            return resources;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def override_resources(self, subfolder: str | None = None) -> list[FileResource]:
        public List<FileResource> OverrideResources(string subfolder = null)
        {
            return _installation.OverrideResources(subfolder);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def texturepack_resources(self, texturepack_name: str) -> list[FileResource]:
        public List<FileResource> TexturepackResources(string texturepackName)
        {
            var resources = new List<FileResource>();
            // Get resources from texture pack
            // This will be implemented when texture pack resource enumeration is available
            return resources;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:2239-2258
        // Original: def string(self, locstring: LocalizedString, default: str = "") -> str:
        public string String(LocalizedString locstring, string defaultStr = "")
        {
            if (locstring == null)
            {
                return defaultStr;
            }

            var results = Strings(new List<LocalizedString> { locstring }, defaultStr);
            return results.ContainsKey(locstring) ? results[locstring] : defaultStr;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:2260-2299
        // Original: def strings(self, queries: list[LocalizedString], default: str = "") -> dict[LocalizedString, str]:
        public Dictionary<LocalizedString, string> Strings(List<LocalizedString> queries, string defaultStr = "")
        {
            var results = new Dictionary<LocalizedString, string>();
            if (queries == null || queries.Count == 0)
            {
                return results;
            }

            string tlkPath = System.IO.Path.Combine(Path, "dialog.tlk");
            if (!File.Exists(tlkPath))
            {
                foreach (var locstring in queries)
                {
                    results[locstring] = defaultStr;
                }
                return results;
            }

            try
            {
                var talkTable = new Andastra.Parsing.Extract.TalkTable(tlkPath);
                var stringrefs = queries.Select(q => q.StringRef).ToList();
                var batch = talkTable.Batch(stringrefs);

                string femaleTlkPath = System.IO.Path.Combine(Path, "dialogf.tlk");
                Dictionary<int, Andastra.Parsing.Extract.StringResult> femaleBatch = new Dictionary<int, Andastra.Parsing.Extract.StringResult>();
                if (File.Exists(femaleTlkPath))
                {
                    try
                    {
                        var femaleTalkTable = new Andastra.Parsing.Extract.TalkTable(femaleTlkPath);
                        var femaleBatchDict = femaleTalkTable.Batch(stringrefs);
                        foreach (var kvp in femaleBatchDict)
                        {
                            femaleBatch[kvp.Key] = kvp.Value;
                        }
                    }
                    catch
                    {
                        // Ignore female talktable errors
                    }
                }

                foreach (var locstring in queries)
                {
                    if (locstring.StringRef != -1)
                    {
                        if (batch.ContainsKey(locstring.StringRef))
                        {
                            results[locstring] = batch[locstring.StringRef].Text;
                        }
                        else if (femaleBatch.ContainsKey(locstring.StringRef))
                        {
                            results[locstring] = femaleBatch[locstring.StringRef].Text;
                        }
                        else
                        {
                            results[locstring] = defaultStr;
                        }
                    }
                    else if (locstring.Count > 0)
                    {
                        // Get first text from localized string
                        foreach (var entry in locstring)
                        {
                            results[locstring] = entry.Item3; // (Language, Gender, string) - Item3 is the string
                            break;
                        }
                    }
                    else
                    {
                        results[locstring] = defaultStr;
                    }
                }
            }
            catch
            {
                foreach (var locstring in queries)
                {
                    results[locstring] = defaultStr;
                }
            }

            return results;
        }

        // Matching PyKotor implementation: Helper method to get string from stringref (for use in editors)
        // Original: installation.talktable().string(stringref)
        public string GetStringFromStringRef(int stringref)
        {
            if (stringref == -1)
            {
                return "";
            }

            string tlkPath = System.IO.Path.Combine(Path, "dialog.tlk");
            if (!File.Exists(tlkPath))
            {
                return "";
            }

            try
            {
                var talkTable = new Andastra.Parsing.Extract.TalkTable(tlkPath);
                return talkTable.GetString(stringref);
            }
            catch
            {
                return "";
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1117-1124
        // Original: def talktable(self) -> TalkTable:
        /// <summary>
        /// Returns the TalkTable linked to the Installation.
        /// </summary>
        /// <returns>A TalkTable object.</returns>
        public Andastra.Parsing.Extract.TalkTable TalkTable()
        {
            string tlkPath = System.IO.Path.Combine(Path, "dialog.tlk");
            return new Andastra.Parsing.Extract.TalkTable(tlkPath);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def reload_module(self, module_name: str):
        public void ReloadModule(string moduleName)
        {
            _installation.ReloadModule(moduleName);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def load_override(self, directory: str | None = None):
        public void LoadOverride(string directory = null)
        {
            // Clear override cache to force reload
            // The actual loading will happen on next access via OverrideResources
            _installation.ClearCache();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def reload_override_file(self, filepath: Path):
        public void ReloadOverrideFile(string filepath)
        {
            // Clear override cache to force reload
            _installation.ClearCache();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def module_id(self, module_file_name: str, use_alternate: bool = False) -> str:
        public string ModuleId(string moduleFileName, bool useAlternate = false)
        {
            // Extract module root from filename
            string root = Andastra.Parsing.Installation.Installation.GetModuleRoot(moduleFileName);
            if (useAlternate)
            {
                // Try to get area name from module
                var moduleNames = ModuleNames();
                if (moduleNames.ContainsKey(moduleFileName))
                {
                    string areaName = moduleNames[moduleFileName];
                    if (!string.IsNullOrEmpty(areaName) && areaName != "<Unknown Area>")
                    {
                        return areaName.ToLowerInvariant();
                    }
                }
            }
            return root.ToLowerInvariant();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: def locations(self, queries: list[ResourceIdentifier], order: list[SearchLocation] | None = None) -> dict[ResourceIdentifier, list[LocationResult]]:
        public Dictionary<ResourceIdentifier, List<LocationResult>> Locations(
            List<ResourceIdentifier> queries,
            SearchLocation[] order = null,
            List<LazyCapsule> capsules = null)
        {
            return _installation.Locations(queries, order, capsules);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1807-1843
        // Original: def texture(self, resname: str, order: Sequence[SearchLocation] | None = None, ...) -> TPC | None:
        [CanBeNull]
        public Andastra.Parsing.Formats.TPC.TPC Texture(string resname, SearchLocation[] searchOrder = null)
        {
            return _installation.Texture(resname, searchOrder);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1845-1888
        // Original: def textures(self, resnames: Iterable[str], order: Sequence[SearchLocation] | None = None, ...) -> CaseInsensitiveDict[TPC | None]:
        public Andastra.Utility.CaseInsensitiveDict<Andastra.Parsing.Formats.TPC.TPC> Textures(
            List<string> resnames,
            SearchLocation[] searchOrder = null)
        {
            var textures = new Andastra.Utility.CaseInsensitiveDict<Andastra.Parsing.Formats.TPC.TPC>();
            if (resnames == null)
            {
                return textures;
            }

            if (searchOrder == null || searchOrder.Length == 0)
            {
                searchOrder = new[]
                {
                    SearchLocation.CUSTOM_FOLDERS,
                    SearchLocation.OVERRIDE,
                    SearchLocation.CUSTOM_MODULES,
                    SearchLocation.TEXTURES_TPA,
                    SearchLocation.CHITIN
                };
            }

            foreach (var resname in resnames)
            {
                var texture = _installation.Texture(resname, searchOrder);
                textures[resname] = texture;
            }

            return textures;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1918-2042
        // Original: def sounds(self, resnames: Iterable[str], order: Sequence[SearchLocation] | None = None, ...) -> CaseInsensitiveDict[bytes | None]:
        public Andastra.Utility.CaseInsensitiveDict<byte[]> Sounds(
            List<string> resnames,
            SearchLocation[] searchOrder = null)
        {
            var sounds = new Andastra.Utility.CaseInsensitiveDict<byte[]>();
            if (resnames == null)
            {
                return sounds;
            }

            if (searchOrder == null || searchOrder.Length == 0)
            {
                searchOrder = new[]
                {
                    SearchLocation.CUSTOM_FOLDERS,
                    SearchLocation.OVERRIDE,
                    SearchLocation.CUSTOM_MODULES,
                    SearchLocation.SOUND,
                    SearchLocation.CHITIN
                };
            }

            var soundFormats = new[] { ResourceType.WAV, ResourceType.MP3 };

            foreach (var resname in resnames)
            {
                sounds[resname] = null;
            }

            // Search for sounds in each location
            foreach (var location in searchOrder)
            {
                if (location == SearchLocation.CHITIN)
                {
                    var chitinResources = _installation.ChitinResources();
                    foreach (var resource in chitinResources)
                    {
                        if (Array.IndexOf(soundFormats, resource.ResType) >= 0)
                        {
                            string lowerResname = resource.ResName.ToLowerInvariant();
                            if (resnames.Any(r => r.ToLowerInvariant() == lowerResname))
                            {
                                try
                                {
                                    var soundData = resource.Data();
                                    if (soundData != null)
                                    {
                                        sounds[resource.ResName] = soundData;
                                    }
                                }
                                catch
                                {
                                    // Skip if can't read
                                }
                            }
                        }
                    }
                }
                else if (location == SearchLocation.SOUND)
                {
                    string streamSoundsPath = Installation.GetStreamSoundsPath(Path);
                    if (Directory.Exists(streamSoundsPath))
                    {
                        foreach (var file in Directory.GetFiles(streamSoundsPath, "*.*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var identifier = ResourceIdentifier.FromPath(file);
                                if (Array.IndexOf(soundFormats, identifier.ResType) >= 0)
                                {
                                    string lowerResname = identifier.ResName.ToLowerInvariant();
                                    if (resnames.Any(r => r.ToLowerInvariant() == lowerResname))
                                    {
                                        sounds[identifier.ResName] = File.ReadAllBytes(file);
                                    }
                                }
                            }
                            catch
                            {
                                // Skip invalid files
                            }
                        }
                    }
                }
                else if (location == SearchLocation.MUSIC)
                {
                    string streamMusicPath = Installation.GetStreamMusicPath(Path);
                    if (Directory.Exists(streamMusicPath))
                    {
                        foreach (var file in Directory.GetFiles(streamMusicPath, "*.*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var identifier = ResourceIdentifier.FromPath(file);
                                if (Array.IndexOf(soundFormats, identifier.ResType) >= 0)
                                {
                                    string lowerResname = identifier.ResName.ToLowerInvariant();
                                    if (resnames.Any(r => r.ToLowerInvariant() == lowerResname))
                                    {
                                        sounds[identifier.ResName] = File.ReadAllBytes(file);
                                    }
                                }
                            }
                            catch
                            {
                                // Skip invalid files
                            }
                        }
                    }
                }
                else if (location == SearchLocation.VOICE)
                {
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1918-2042
                    // Original: Try StreamVoice first (TSL), then StreamWaves (K1)
                    string streamVoicePath = Installation.GetStreamVoicePath(Path);
                    if (Directory.Exists(streamVoicePath))
                    {
                        foreach (var file in Directory.GetFiles(streamVoicePath, "*.*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var identifier = ResourceIdentifier.FromPath(file);
                                if (Array.IndexOf(soundFormats, identifier.ResType) >= 0)
                                {
                                    string lowerResname = identifier.ResName.ToLowerInvariant();
                                    if (resnames.Any(r => r.ToLowerInvariant() == lowerResname))
                                    {
                                        sounds[identifier.ResName] = File.ReadAllBytes(file);
                                    }
                                }
                            }
                            catch
                            {
                                // Skip invalid files
                            }
                        }
                    }
                    else
                    {
                        // Fallback to StreamWaves for K1
                        string streamWavesPath = Installation.GetStreamWavesPath(Path);
                        if (Directory.Exists(streamWavesPath))
                        {
                            foreach (var file in Directory.GetFiles(streamWavesPath, "*.*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    var identifier = ResourceIdentifier.FromPath(file);
                                    if (Array.IndexOf(soundFormats, identifier.ResType) >= 0)
                                    {
                                        string lowerResname = identifier.ResName.ToLowerInvariant();
                                        if (resnames.Any(r => r.ToLowerInvariant() == lowerResname))
                                        {
                                            sounds[identifier.ResName] = File.ReadAllBytes(file);
                                        }
                                    }
                                }
                                catch
                                {
                                    // Skip invalid files
                                }
                            }
                        }
                    }
                }
                else if (location == SearchLocation.OVERRIDE)
                {
                    var overrideResources = _installation.OverrideResources();
                    foreach (var resource in overrideResources)
                    {
                        if (Array.IndexOf(soundFormats, resource.ResType) >= 0)
                        {
                            string lowerResname = resource.ResName.ToLowerInvariant();
                            if (resnames.Any(r => r.ToLowerInvariant() == lowerResname))
                            {
                                try
                                {
                                    var soundData = resource.Data();
                                    if (soundData != null)
                                    {
                                        sounds[resource.ResName] = soundData;
                                    }
                                }
                                catch
                                {
                                    // Skip if can't read
                                }
                            }
                        }
                    }
                }
            }

            return sounds;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:471-518
        // Original: def get_relevant_resources(self, restype: ResourceType, src_filepath: Path | None = None) -> set[FileResource]:
        public HashSet<FileResource> GetRelevantResources(ResourceType restype, string srcFilepath = null)
        {
            if (srcFilepath == null)
            {
                // Return all resources of the specified type
                var allResources = new HashSet<FileResource>();
                allResources.UnionWith(CoreResources().Where(r => r.ResType == restype));
                allResources.UnionWith(OverrideResources().Where(r => r.ResType == restype));
                return allResources;
            }

            var relevantResources = new HashSet<FileResource>();
            relevantResources.UnionWith(OverrideResources().Where(r => r.ResType == restype));
            relevantResources.UnionWith(_installation.ChitinResources().Where(r => r.ResType == restype));

            string srcAbsolute = System.IO.Path.GetFullPath(srcFilepath);
            string modulePath = System.IO.Path.GetFullPath(ModulePath());
            string overridePath = System.IO.Path.GetFullPath(OverridePath());

            bool IsWithin(string child, string parent)
            {
                try
                {
                    var childUri = new Uri(child);
                    var parentUri = new Uri(parent);
                    return parentUri.IsBaseOf(childUri);
                }
                catch
                {
                    return false;
                }
            }

            if (IsWithin(srcAbsolute, modulePath))
            {
                // Add resources from matching modules
                string moduleFileName = System.IO.Path.GetFileName(srcFilepath);
                var moduleResources = ModuleResources(moduleFileName);
                relevantResources.UnionWith(moduleResources.Where(r => r.ResType == restype));
            }
            else if (IsWithin(srcAbsolute, overridePath))
            {
                // Add resources from override
                var overrideResources = OverrideResources();
                relevantResources.UnionWith(overrideResources.Where(r => r.ResType == restype));
            }

            return relevantResources;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py
        // Original: property saves -> dict[Path, dict[Path, list[FileResource]]]:
        public Dictionary<string, Dictionary<string, List<FileResource>>> Saves
        {
            get
            {
                var saves = new Dictionary<string, Dictionary<string, List<FileResource>>>();
                var saveLocations = SaveLocations();
                foreach (var saveLocation in saveLocations)
                {
                    if (!Directory.Exists(saveLocation))
                    {
                        continue;
                    }

                    var saveDict = new Dictionary<string, List<FileResource>>();
                    foreach (var saveDir in Directory.GetDirectories(saveLocation))
                    {
                        var saveResources = new List<FileResource>();
                        foreach (var file in Directory.GetFiles(saveDir))
                        {
                            try
                            {
                                var identifier = ResourceIdentifier.FromPath(file);
                                if (identifier.ResType != ResourceType.INVALID && !identifier.ResType.IsInvalid)
                                {
                                    var fileInfo = new FileInfo(file);
                                    saveResources.Add(new FileResource(
                                        identifier.ResName,
                                        identifier.ResType,
                                        (int)fileInfo.Length,
                                        0,
                                        file
                                    ));
                                }
                            }
                            catch
                            {
                                // Skip invalid files
                            }
                        }
                        saveDict[System.IO.Path.GetFileName(saveDir)] = saveResources;
                    }
                    saves[saveLocation] = saveDict;
                }
                return saves;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:679-699
        // Original: def is_save_corrupted(self, save_path: Path) -> bool:
        public bool IsSaveCorrupted(string savePath)
        {
            try
            {
                return CheckSaveCorruptionLightweight(savePath);
            }
            catch
            {
                // If we can't check the save, assume it's not corrupted (safer than false positives)
                return false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:701-751
        // Original: def _check_save_corruption_lightweight(self, save_path: Path) -> bool:
        private bool CheckSaveCorruptionLightweight(string savePath)
        {
            string savegameSav = System.IO.Path.Combine(savePath, "SAVEGAME.sav");
            if (!File.Exists(savegameSav))
            {
                return false;
            }

            try
            {
                // Read the outer ERF (SAVEGAME.sav)
                var outerErf = Andastra.Parsing.Formats.ERF.ERFAuto.ReadErf(savegameSav);

                // Check each .sav resource (cached modules) for EventQueue corruption
                foreach (var resource in outerErf)
                {
                    if (resource.ResType != ResourceType.SAV)
                    {
                        continue;
                    }

                    try
                    {
                        // Read the nested module ERF
                        var innerErf = Andastra.Parsing.Formats.ERF.ERFAuto.ReadErf(resource.Data);

                        // Look for module.ifo in this cached module
                        foreach (var innerResource in innerErf)
                        {
                            if (innerResource.ResRef.ToString().ToLowerInvariant() == "module" && innerResource.ResType == ResourceType.IFO)
                            {
                                // Check for EventQueue
                                var ifoGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(innerResource.Data);
                                if (ifoGff.Root.Exists("EventQueue"))
                                {
                                    var eventQueue = ifoGff.Root.GetList("EventQueue");
                                    if (eventQueue != null && eventQueue.Count > 0)
                                    {
                                        return true; // Corrupted!
                                    }
                                }
                                break; // Only one module.ifo per cached module
                            }
                        }
                    }
                    catch
                    {
                        continue; // Skip malformed nested ERFs
                    }
                }

                return false; // No corruption found
            }
            catch
            {
                return false; // If we can't parse, assume not corrupted
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/installation.py:753-825
        // Original: def fix_save_corruption(self, save_path: Path) -> bool:
        public bool FixSaveCorruption(string savePath)
        {
            string savegameSav = System.IO.Path.Combine(savePath, "SAVEGAME.sav");
            if (!File.Exists(savegameSav))
            {
                return false;
            }

            try
            {
                // Read the outer ERF (SAVEGAME.sav)
                var outerErf = Andastra.Parsing.Formats.ERF.ERFAuto.ReadErf(savegameSav);
                bool anyFixed = false;

                // Process each .sav resource (cached modules)
                foreach (var resource in outerErf)
                {
                    if (resource.ResType != ResourceType.SAV)
                    {
                        continue;
                    }

                    try
                    {
                        var innerErf = Andastra.Parsing.Formats.ERF.ERFAuto.ReadErf(resource.Data);
                        bool innerModified = false;

                        // Look for module.ifo in this cached module
                        foreach (var innerResource in innerErf)
                        {
                            if (innerResource.ResRef.ToString().ToLowerInvariant() == "module" && innerResource.ResType == ResourceType.IFO)
                            {
                                // Check and clear EventQueue
                                var ifoGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(innerResource.Data);
                                if (ifoGff.Root.Exists("EventQueue"))
                                {
                                    var eventQueue = ifoGff.Root.GetList("EventQueue");
                                    if (eventQueue != null && eventQueue.Count > 0)
                                    {
                                        // Clear the EventQueue
                                        ifoGff.Root.SetList("EventQueue", new Andastra.Parsing.Formats.GFF.GFFList());
                                        // Update the resource data
                                        byte[] ifoData = Andastra.Parsing.Formats.GFF.GFFAuto.BytesGff(ifoGff, ResourceType.IFO);
                                        innerErf.SetData(innerResource.ResRef.ToString(), innerResource.ResType, ifoData);
                                        innerModified = true;
                                        anyFixed = true;
                                    }
                                }
                                break;
                            }
                        }

                        if (innerModified)
                        {
                            // Update the outer ERF with the modified inner ERF
                            byte[] innerErfData = Andastra.Parsing.Formats.ERF.ERFAuto.BytesErf(innerErf, ResourceType.SAV);
                            outerErf.SetData(resource.ResRef.ToString(), resource.ResType, innerErfData);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Failed to process cached module {resource.ResRef}: {ex}");
                        continue;
                    }
                }

                if (anyFixed)
                {
                    // Write the fixed outer ERF back to disk
                    Andastra.Parsing.Formats.ERF.ERFAuto.WriteErf(outerErf, savegameSav, ResourceType.SAV);
                    System.Console.WriteLine($"Fixed EventQueue corruption in save: {System.IO.Path.GetFileName(savePath)}");
                    return true;
                }

                return false; // No corruption to fix
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to fix corruption for save at '{savePath}': {ex}");
                return false;
            }
        }
    }
}

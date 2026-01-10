using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.LYT;
using Andastra.Parsing.Formats.VIS;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Module;
using JetBrains.Annotations;
using LYT = Andastra.Parsing.Resource.Formats.LYT.LYT;
using Vector3 = System.Numerics.Vector3;

namespace Andastra.Runtime.Engines.Odyssey.Loading
{
    /// <summary>
    /// Loads KotOR modules from Andastra.Parsing data structures into Odyssey runtime format.
    /// </summary>
    /// <remarks>
    /// Module Loading Sequence (from IFO spec):
    /// - Based on swkotor2.exe module loading system
    /// - LoadModule @ 0x004f20d0 - Main module loading function (located via "MODULES:" @ 0x007b58b4)
    ///   - Loads IFO file, checks module requirements, mounts HAK files, loads entry area, spawns player at entry position, fires OnModLoad and OnModStart scripts
    /// - LoadModuleFromPath @ 0x004f3460 - Loads module from path with save integration (located via "MODULES:" @ 0x007b58b4 and "modulesave" @ 0x007bde20)
    ///   - Constructs "MODULES:\{modulePath}" path, checks for module save state, loads module resources, calls auto-save if enabled. Calls SaveModuleState after module is loaded if save data exists
    /// - SaveModuleState @ 0x004f0c50 - Saves module state to save game (located via "SAVES:" @ 0x007c6bcc)
    ///   - Saves module-specific state including party table, area states, entity states. Constructs save path "SAVES:\{modulePath}", writes module save GFF with "MOD V1.0" signature
    /// - Located via string references: "MODULES:" @ 0x007b58b4, ":MODULES" @ 0x007be258
    /// - "Module" @ 0x007bc4e0, "MODULE" @ 0x007beab8, "ModuleName" @ 0x007bde2c
    /// - "LASTMODULE" @ 0x007be1d0, "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58
    /// - "ModuleList" @ 0x007bdd3c, "GetModuleList" @ 0x007bdd48
    /// - "MODULES" @ 0x007c6bc4, ".\modules" @ 0x007c6bcc, "d:\modules" @ 0x007c6bd8
    /// - "LIVE%d:MODULES\" @ 0x007be680 (live module directory format)
    /// - Module save: "modulesave" @ 0x007bde20, ":: Module savegame list: %s.\n" @ 0x007cbbb4
    /// - ":: Server module list: " @ 0x007cbc2c, ":: Server mode: Module Running.\n" @ 0x007cbc44
    /// - ":: Server mode: Module Loaded.\n" @ 0x007cbc68, "Module: %s" @ 0x007c79c8
    /// - "module000" @ 0x007cb9cc (module save prefix), "PlayModuleCharacterList" @ 0x007c2428
    /// - Script events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948
    /// - GUI: "LB_MODULES" @ 0x007cbff4, ":modules" @ 0x007cc0d8
    /// - Original implementation: FUN_00633270 @ 0x00633270 (sets up all game directories including MODULES)
    ///   - Function signature: `void FUN_00633270(undefined4 *param_1)`
    ///   - param_1: Directory alias manager structure pointer
    ///   - Sets up directory aliases for resource lookup (via FUN_00632f30):
    ///     - HD0: Maps to "d:\" (absolute) or ".\" (relative) - Hard drive root
    ///     - CD0: Maps to "d:\" (absolute) or "d:\" (relative) - CD drive root
    ///     - OVERRIDE: Maps to "d:\override" (absolute) or ".\override" (relative) - Override directory
    ///     - ERRORTEX: Maps to "d:\errortex" (absolute) or ".\errortex" (relative) - Error texture directory
    ///     - TEMP: Maps to "d:\temp" (absolute) or ".\temp" (relative) - Temporary files
    ///     - MODULES: Maps to "d:\modules" (absolute) or ".\modules" (relative) - Module directory
    ///     - NWMFILES: Maps to "d:\nwm" (absolute) or ".\nwm" (relative) - NWM files directory
    ///     - LOGS: Maps to "d:\logs" (absolute) or ".\logs" (relative) - Log files directory
    ///     - LOCALVAULT: Maps to "d:\localvault" (absolute) or ".\localvault" (relative) - Local vault
    ///     - DMVAULT: Maps to "d:\dmvault" (absolute) or ".\dmvault" (relative) - DM vault
    ///     - SERVERVAULT: Maps to "d:\servervault" (absolute) or ".\servervault" (relative) - Server vault
    ///     - SAVES: Maps to "u:\" (absolute) or ".\saves" (relative) - Save game directory
    ///     - MUSIC: Maps to "d:\music" (absolute) or ".\music" (relative) - Music directory
    ///     - STREAMMUSIC: Maps to "d:\streammusic" (absolute) or ".\streammusic" (relative) - Streamed music
    ///     - MOVIES: Maps to "d:\movies" (absolute) or ".\movies" (relative) - Movie files
    ///     - TEMPCLIENT: Maps to "d:\tempclient" (absolute) or ".\tempclient" (relative) - Client temp files
    ///     - CURRENTGAME: Maps to "d:\currentgame" (absolute) or ".\currentgame" (relative) - Current game data
    ///     - HAK: Maps to "d:\hak" (absolute) or ".\hak" (relative) - HAK files directory
    ///     - TEXTUREPACKS: Maps to "d:\texturepacks" (absolute) or ".\texturepacks" (relative) - Texture packs
    ///     - STREAMVOICE: Maps to "d:\streamvoice" (absolute) or ".\streamvoice" (relative) - Streamed voice
    ///     - SUPERMODELS: Maps to "d:\supermodels" (absolute) or ".\supermodels" (relative) - Super models
    ///     - DOWNLOADS: Maps to "t:\" (absolute) or ".\downloads" (relative) - Downloads directory
    ///     - OPTIONS: Maps to "t:\" (absolute) or ".\" (relative) - Options directory
    ///     - AMBIENT: Maps to "d:\ambient" (absolute) or ".\ambient" (relative) - Ambient sounds
    ///     - PATCH: Maps to "d:\patch" (absolute) or ".\patch" (relative) - Patch files
    ///     - PORTRAITS: Maps to "d:\portraits" (absolute) or ".\portraits" (relative) - Portrait files
    ///     - GAMEINPROGRESS: Maps to "z:\gameinprogress" (absolute) or ".\gameinprogress" (relative) - Game in progress
    ///     - FUTUREGAME: Maps to "z:\futuregame" (absolute) or ".\futuregame" (relative) - Future game data
    ///     - RIMS: Maps to "d:\rims" (absolute) or ".\rims" (relative) - RIM archive files
    ///     - RIMSXBOX: Maps to "d:\rimsxbox" (absolute) or ".\rimsxbox" (relative) - Xbox RIM files
    ///     - REBOOTDATA: Maps to "d:\rebootdata" (absolute) or ".\rebootdata" (relative) - Reboot data
    ///     - CACHE: Maps to "z:\cache" (absolute) or ".\" (relative) - Cache directory
    ///     - LIPS: Maps to "d:\lips" (absolute) or ".\lips" (relative) - LIP sync files
    ///   - Directory aliases used throughout engine for resource path resolution (e.g., "MODULES:\{moduleName}\module.ifo")
    /// 1. Read IFO - Parse module metadata
    /// 2. Check Requirements - Verify Expansion_Pack and MinGameVer
    /// 3. Load HAKs - Mount HAK files in order
    /// 4. Play Movie - Show Mod_StartMovie if set
    /// 5. Load Entry Area - Read ARE + GIT for Mod_Entry_Area
    /// 6. Spawn Player - Place at Entry position/direction
    /// 7. Fire OnModLoad - Execute module load script
    /// 8. Fire OnModStart - Execute module start script
    /// 9. Start Gameplay - Enable player control
    /// </remarks>
    public class ModuleLoader
    {
        private readonly Installation _installation;
        private readonly EntityFactory _entityFactory;
        private readonly NavigationMeshFactory _navMeshFactory;

        public ModuleLoader(Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException("installation");
            _entityFactory = new EntityFactory();
            _navMeshFactory = new NavigationMeshFactory();
        }

        /// <summary>
        /// Gets the Installation instance used by this ModuleLoader.
        /// </summary>
        /// <returns>The Installation instance, or null if not available.</returns>
        public Installation GetInstallation()
        {
            return _installation;
        }

        /// <summary>
        /// Loads a module by name.
        /// </summary>
        /// <param name="moduleName">Module name (e.g., "end_m01aa" for Endar Spire)</param>
        /// <returns>Loaded RuntimeModule</returns>
        public RuntimeModule LoadModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", "moduleName");
            }

            // Create Andastra.Parsing Module wrapper
            var module = new Module(moduleName, _installation);

            // Cache the Module object for efficient resource access
            // Based on swkotor2.exe: Module objects are cached and reused for resource lookups
            _cachedParsingModule = module;

            // Create runtime module
            var runtimeModule = new RuntimeModule();

            // Load IFO (module info)
            LoadModuleInfo(module, runtimeModule);

            // Load entry area
            string entryAreaResRef = runtimeModule.EntryArea;
            if (!string.IsNullOrEmpty(entryAreaResRef))
            {
                RuntimeArea area = LoadArea(module, entryAreaResRef);
                if (area != null)
                {
                    runtimeModule.AddArea(area);
                }
            }

            // Store as current module
            _currentModule = runtimeModule;

            return runtimeModule;
        }

        /// <summary>
        /// Loads module info from IFO.
        /// </summary>
        private void LoadModuleInfo(Module module, RuntimeModule runtimeModule)
        {
            ModuleResource ifoResource = module.Info();
            if (ifoResource == null)
            {
                throw new InvalidOperationException($"Module '{module.GetRoot()}' has no IFO resource. Ensure the module archive contains 'module.ifo'.");
            }

            // Check if the resource has any locations
            List<string> locations = ifoResource.Locations();
            if (locations == null || locations.Count == 0)
            {
                string modulePath = System.IO.Path.Combine(module.Installation.Path, "Modules", module.GetRoot() + ".rim");
                throw new InvalidOperationException($"Module '{module.GetRoot()}' has no IFO resource locations. Expected to find 'module.ifo' in '{modulePath}'. Ensure the module archive exists and contains the IFO file.");
            }

            object ifoData = null;
            try
            {
                ifoData = ifoResource.Resource();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load module IFO for '{module.GetRoot()}': {ex.Message}", ex);
            }

            if (ifoData == null)
            {
                string locationsStr = locations.Count > 0 ? string.Join(", ", locations) : "none";
                throw new InvalidOperationException($"Failed to load module IFO for '{module.GetRoot()}': Resource() returned null. Locations searched: {locationsStr}. The IFO file may be corrupted or missing.");
            }

            // IFO is a GFF file
            GFF ifoGff = ifoData as GFF;
            if (ifoGff == null)
            {
                throw new InvalidOperationException($"IFO resource for '{module.GetRoot()}' is not a valid GFF. Expected GFF type, got {ifoData.GetType().Name}.");
            }

            GFFStruct root = ifoGff.Root;

            // Basic info
            runtimeModule.ResRef = module.GetRoot();
            runtimeModule.Tag = GetStringField(root, "Mod_Tag");

            // Display name (localized string)
            if (root.Exists("Mod_Name"))
            {
                LocalizedString nameLocStr = root.GetLocString("Mod_Name");
                runtimeModule.DisplayName = nameLocStr != null ? nameLocStr.ToString() : string.Empty;
            }

            // Entry area
            if (root.Exists("Mod_Entry_Area"))
            {
                ResRef entryAreaRef = root.GetResRef("Mod_Entry_Area");
                runtimeModule.EntryArea = entryAreaRef != null ? entryAreaRef.ToString() : string.Empty;
            }

            // Entry position
            if (root.Exists("Mod_Entry_X") && root.Exists("Mod_Entry_Y") && root.Exists("Mod_Entry_Z"))
            {
                float x = root.GetSingle("Mod_Entry_X");
                float y = root.GetSingle("Mod_Entry_Y");
                float z = root.GetSingle("Mod_Entry_Z");
                runtimeModule.EntryPosition = new Vector3(x, y, z);
            }

            // Entry direction
            if (root.Exists("Mod_Entry_Dir_X") && root.Exists("Mod_Entry_Dir_Y"))
            {
                runtimeModule.EntryDirectionX = root.GetSingle("Mod_Entry_Dir_X");
                runtimeModule.EntryDirectionY = root.GetSingle("Mod_Entry_Dir_Y");
            }

            // Time settings
            runtimeModule.DawnHour = GetIntField(root, "Mod_DawnHour", 6);
            runtimeModule.DuskHour = GetIntField(root, "Mod_DuskHour", 18);
            runtimeModule.MinutesPastMidnight = GetIntField(root, "Mod_MinPerHour", 2) * 60; // Convert to minutes
            runtimeModule.Day = GetIntField(root, "Mod_StartDay", 1);
            runtimeModule.Month = GetIntField(root, "Mod_StartMonth", 1);
            runtimeModule.Year = GetIntField(root, "Mod_StartYear", 3951);

            // XP scale
            runtimeModule.XPScale = GetIntField(root, "Mod_XPScale", 100);

            // Start movie
            if (root.Exists("Mod_StartMovie"))
            {
                ResRef movieRef = root.GetResRef("Mod_StartMovie");
                runtimeModule.StartMovie = movieRef != null ? movieRef.ToString() : string.Empty;
            }

            // Scripts
            LoadModuleScripts(root, runtimeModule);

            // Area list from Mod_Area_list
            LoadAreaList(root, runtimeModule);
        }

        /// <summary>
        /// Loads module scripts from IFO.
        /// </summary>
        private void LoadModuleScripts(GFFStruct root, RuntimeModule module)
        {
            // Map IFO script fields to ScriptEvent enum
            var scriptMappings = new Dictionary<string, ScriptEvent>
            {
                { "Mod_OnAcquirItem", ScriptEvent.OnAcquireItem },
                { "Mod_OnActvtItem", ScriptEvent.OnActivateItem },
                { "Mod_OnClientEntr", ScriptEvent.OnClientEnter },
                { "Mod_OnClientLeav", ScriptEvent.OnClientLeave },
                { "Mod_OnHeartbeat", ScriptEvent.OnHeartbeat },
                { "Mod_OnModLoad", ScriptEvent.OnModuleLoad },
                { "Mod_OnModStart", ScriptEvent.OnModuleStart },
                { "Mod_OnPlrDeath", ScriptEvent.OnPlayerDeath },
                { "Mod_OnPlrDying", ScriptEvent.OnPlayerDying },
                { "Mod_OnPlrLvlUp", ScriptEvent.OnPlayerLevelUp },
                { "Mod_OnPlrRest", ScriptEvent.OnPlayerRest },
                { "Mod_OnSpawnBtnDn", ScriptEvent.OnSpawnButtonDown },
                { "Mod_OnUnAqreItem", ScriptEvent.OnUnacquireItem },
                { "Mod_OnUsrDefined", ScriptEvent.OnUserDefined }
            };

            foreach (KeyValuePair<string, ScriptEvent> mapping in scriptMappings)
            {
                if (root.Exists(mapping.Key))
                {
                    ResRef scriptRef = root.GetResRef(mapping.Key);
                    if (scriptRef != null && !string.IsNullOrEmpty(scriptRef.ToString()))
                    {
                        module.SetScript(mapping.Value, scriptRef.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Loads area list from Mod_Area_list field in IFO.
        /// </summary>
        /// <param name="root">IFO GFF root structure.</param>
        /// <param name="runtimeModule">Runtime module to store area list in.</param>
        /// <remarks>
        /// Based on swkotor2.exe: Mod_Area_list is stored in module IFO file.
        /// Each entry contains Area_Name field with the area ResRef.
        /// This list is used for resolving transition targets by index (TransPendNextID).
        /// This matches the IFO format specification in vendor/PyKotor/wiki/GFF-IFO.md.
        /// </remarks>
        private void LoadAreaList(GFFStruct root, RuntimeModule runtimeModule)
        {
            if (root == null || runtimeModule == null)
            {
                return;
            }

            try
            {
                // Check if Mod_Area_list exists
                if (!root.Exists("Mod_Area_list"))
                {
                    Console.WriteLine($"[ModuleLoader] LoadAreaList: Mod_Area_list not found in IFO for module {runtimeModule.ResRef}");
                    runtimeModule.AreaList = new List<string>();
                    return;
                }

                // Read Mod_Area_list (list of structs)
                GFFList areaList = root.GetList("Mod_Area_list");
                if (areaList == null || areaList.Count == 0)
                {
                    Console.WriteLine($"[ModuleLoader] LoadAreaList: Mod_Area_list is empty for module {runtimeModule.ResRef}");
                    runtimeModule.AreaList = new List<string>();
                    return;
                }

                List<string> areaResRefs = new List<string>();

                // Each entry in Mod_Area_list has an Area_Name field (ResRef)
                foreach (GFFStruct areaEntry in areaList)
                {
                    if (areaEntry.Exists("Area_Name"))
                    {
                        ResRef areaName = areaEntry.GetResRef("Area_Name");
                        if (areaName != null && !string.IsNullOrEmpty(areaName.ToString()))
                        {
                            areaResRefs.Add(areaName.ToString());
                        }
                    }
                }

                runtimeModule.AreaList = areaResRefs;

                if (areaResRefs.Count > 0)
                {
                    Console.WriteLine($"[ModuleLoader] LoadAreaList: Found {areaResRefs.Count} areas in Mod_Area_list for module {runtimeModule.ResRef}: {string.Join(", ", areaResRefs)}");
                }
                else
                {
                    Console.WriteLine($"[ModuleLoader] LoadAreaList: No valid area entries found in Mod_Area_list for module {runtimeModule.ResRef}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModuleLoader] LoadAreaList: Exception reading Mod_Area_list for module {runtimeModule.ResRef}: {ex.Message}");
                runtimeModule.AreaList = new List<string>();
            }
        }

        /// <summary>
        /// Loads an area from ARE + GIT + LYT + VIS.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: LoadAreaProperties @ 0x004e26d0
        /// Loads area resources by ResRef from module archives.
        ///
        /// Area loading sequence:
        /// 1. Load ARE (area properties) - GFF with "ARE " signature
        /// 2. Load GIT (game instance data) - GFF with "GIT " signature containing entities
        /// 3. Load LYT (room layout) - Binary format for room organization (module-level, not area-specific)
        /// 4. Load VIS (visibility) - Binary format for room visibility (module-level, not area-specific)
        /// 5. Load navigation mesh from BWM files - Walkmesh for pathfinding
        ///
        /// Resource lookup:
        /// - ARE and GIT files are area-specific (loaded by areaResRef)
        /// - LYT and VIS files are module-level (loaded by module ID)
        /// - Resources are searched in: override directory, module archives, base game resources
        /// </remarks>
        [CanBeNull]
        public RuntimeArea LoadArea(Module module, string areaResRef)
        {
            if (module == null)
            {
                throw new ArgumentNullException("module");
            }
            if (string.IsNullOrEmpty(areaResRef))
            {
                throw new ArgumentException("Area ResRef cannot be null or empty", "areaResRef");
            }

            var area = new RuntimeArea();
            area.ResRef = areaResRef;

            // Load ARE (area properties) - area-specific resource
            // Based on swkotor2.exe: ARE files contain area lighting, fog, grass, walkmesh references
            // Use module.Resource() to get ARE file by areaResRef (not module ID)
            ModuleResource areResource = module.Resource(areaResRef, ResourceType.ARE);
            if (areResource != null)
            {
                LoadAreaProperties(areResource, area);
            }
            else
            {
                Console.WriteLine($"[ModuleLoader] LoadArea: ARE file not found for area {areaResRef}");
                // Continue loading other resources even if ARE is missing
            }

            // Load GIT (dynamic objects) - area-specific resource
            // Based on swkotor2.exe: GIT files contain creature, door, placeable, trigger, waypoint instances
            // Use module.Resource() to get GIT file by areaResRef (not module ID)
            ModuleResource gitResource = module.Resource(areaResRef, ResourceType.GIT);
            if (gitResource != null)
            {
                LoadGitObjects(gitResource, area, module);
            }
            else
            {
                Console.WriteLine($"[ModuleLoader] LoadArea: GIT file not found for area {areaResRef}");
                // Continue loading other resources even if GIT is missing
            }

            // Load LYT (room layout) - module-level resource
            // Based on swkotor2.exe: LYT files are module-level, not area-specific
            // Use module.Layout() to get LYT file by module ID
            ModuleResource lytResource = module.Layout();
            if (lytResource != null)
            {
                LoadLayout(lytResource, area);
            }

            // Load VIS (visibility) - module-level resource
            // Based on swkotor2.exe: VIS files are module-level, not area-specific
            // Use module.Vis() to get VIS file by module ID
            ModuleResource visResource = module.Vis();
            if (visResource != null)
            {
                LoadVisibility(visResource, area);
            }

            // Load navigation mesh from BWM files
            // Based on swkotor2.exe: Walkmesh is loaded from BWM files referenced in ARE
            LoadNavigationMesh(module, area);

            return area;
        }

        /// <summary>
        /// Loads area properties from ARE.
        /// </summary>
        private void LoadAreaProperties(ModuleResource areResource, RuntimeArea area)
        {
            object areData = areResource.Resource();
            if (areData == null)
            {
                return;
            }

            GFF areGff = areData as GFF;
            if (areGff == null)
            {
                return;
            }

            GFFStruct root = areGff.Root;

            // Basic info
            if (root.Exists("Name"))
            {
                LocalizedString nameLocStr = root.GetLocString("Name");
                area.DisplayName = nameLocStr != null ? nameLocStr.ToString() : string.Empty;
            }

            area.Tag = GetStringField(root, "Tag");

            // Lighting
            area.AmbientColor = (uint)GetIntField(root, "AmbientColor", 0);
            area.DynamicAmbientColor = (uint)GetIntField(root, "DynAmbientColor", 0);
            area.SunAmbientColor = (uint)GetIntField(root, "SunAmbientColor", 0);
            area.SunDiffuseColor = (uint)GetIntField(root, "SunDiffuseColor", 0);
            area.SunFogColor = (uint)GetIntField(root, "SunFogColor", 0);

            // Fog
            area.FogEnabled = GetIntField(root, "SunFogOn", 0) != 0;
            area.FogNear = root.Exists("SunFogNear") ? root.GetSingle("SunFogNear") : 0f;
            area.FogFar = root.Exists("SunFogFar") ? root.GetSingle("SunFogFar") : 0f;
            area.FogColor = (uint)GetIntField(root, "FogColor", 0);

            // Grass
            area.GrassEnabled = GetIntField(root, "Grass_TexName", 0) != 0; // Has grass if texture specified
            if (root.Exists("Grass_TexName"))
            {
                ResRef grassTex = root.GetResRef("Grass_TexName");
                area.GrassTexture = grassTex != null ? grassTex.ToString() : string.Empty;
            }
            area.GrassDensity = root.Exists("Grass_Density") ? root.GetSingle("Grass_Density") : 0f;
            area.GrassQuadSize = root.Exists("Grass_QuadSize") ? root.GetSingle("Grass_QuadSize") : 0f;

            // Audio
            area.MusicDay = GetIntField(root, "MusicDay", -1);
            area.MusicNight = GetIntField(root, "MusicNight", -1);
            area.MusicBattle = GetIntField(root, "MusicBattle", -1);
            area.AmbientSndDay = GetIntField(root, "AmbientSndDay", -1);
            area.AmbientSndNight = GetIntField(root, "AmbientSndNight", -1);

            // Flags
            int flags = GetIntField(root, "Flags", 0);
            area.IsInterior = (flags & 0x0001) != 0;
            area.IsUnderground = (flags & 0x0002) != 0;
            area.HasWeather = (flags & 0x0004) != 0;

            // Weather
            area.WeatherType = GetIntField(root, "ChanceSnow", 0) > 0 ? 2 :
                              GetIntField(root, "ChanceRain", 0) > 0 ? 1 : 0;

            // Area scripts
            LoadAreaScripts(root, area);
        }

        /// <summary>
        /// Loads area scripts from ARE.
        /// </summary>
        private void LoadAreaScripts(GFFStruct root, RuntimeArea area)
        {
            var scriptMappings = new Dictionary<string, ScriptEvent>
            {
                { "OnEnter", ScriptEvent.OnEnter },
                { "OnExit", ScriptEvent.OnExit },
                { "OnHeartbeat", ScriptEvent.OnHeartbeat },
                { "OnUserDefined", ScriptEvent.OnUserDefined }
            };

            foreach (KeyValuePair<string, ScriptEvent> mapping in scriptMappings)
            {
                if (root.Exists(mapping.Key))
                {
                    ResRef scriptRef = root.GetResRef(mapping.Key);
                    if (scriptRef != null && !string.IsNullOrEmpty(scriptRef.ToString()))
                    {
                        area.SetScript(mapping.Value, scriptRef.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Loads room layout from LYT.
        /// </summary>
        private void LoadLayout(ModuleResource lytResource, RuntimeArea area)
        {
            object lytData = lytResource.Resource();
            if (lytData == null)
            {
                return;
            }

            LYT lyt = lytData as LYT;
            if (lyt == null)
            {
                return;
            }

            area.Rooms = new List<RoomInfo>();

            foreach (Andastra.Parsing.Resource.Formats.LYT.LYTRoom room in lyt.Rooms)
            {
                var roomInfo = new RoomInfo
                {
                    ModelName = room.Model,
                    Position = new Vector3(room.Position.X, room.Position.Y, room.Position.Z)
                };
                area.Rooms.Add(roomInfo);
            }
        }

        /// <summary>
        /// Loads visibility info from VIS.
        /// </summary>
        private void LoadVisibility(ModuleResource visResource, RuntimeArea area)
        {
            object visData = visResource.Resource();
            if (visData == null)
            {
                return;
            }

            VIS vis = visData as VIS;
            if (vis == null)
            {
                return;
            }

            // Get all room names from VIS
            var allRoomNames = vis.AllRooms().ToList();

            // Map visibility to rooms - each room tracks which other rooms are visible from it
            for (int i = 0; i < area.Rooms.Count; i++)
            {
                string roomName = area.Rooms[i].ModelName.ToLowerInvariant();
                if (!vis.RoomExists(roomName))
                {
                    continue;
                }

                area.Rooms[i].VisibleRooms = new List<int>();

                // Check visibility against all other rooms
                for (int j = 0; j < area.Rooms.Count; j++)
                {
                    string otherRoomName = area.Rooms[j].ModelName.ToLowerInvariant();
                    if (!vis.RoomExists(otherRoomName))
                    {
                        continue;
                    }

                    try
                    {
                        if (vis.GetVisible(roomName, otherRoomName))
                        {
                            area.Rooms[i].VisibleRooms.Add(j);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Room doesn't exist in VIS, skip
                    }
                }
            }
        }

        /// <summary>
        /// Loads dynamic objects from GIT.
        /// </summary>
        private void LoadGitObjects(ModuleResource gitResource, RuntimeArea area, Module module)
        {
            object gitData = gitResource.Resource();
            if (gitData == null)
            {
                return;
            }

            GFF gitGff = gitData as GFF;
            if (gitGff == null)
            {
                return;
            }

            GFFStruct root = gitGff.Root;

            // Load creatures
            if (root.Exists("Creature List"))
            {
                GFFList creatureList = root.GetList("Creature List");
                if (creatureList != null)
                {
                    foreach (GFFStruct creatureStruct in creatureList)
                    {
                        IEntity entity = _entityFactory.CreateCreatureFromGit(creatureStruct, module);
                        if (entity != null)
                        {
                            area.AddEntity(entity);
                        }
                    }
                }
            }

            // Load doors
            if (root.Exists("Door List"))
            {
                GFFList doorList = root.GetList("Door List");
                if (doorList != null)
                {
                    foreach (GFFStruct doorStruct in doorList)
                    {
                        IEntity entity = _entityFactory.CreateDoorFromGit(doorStruct, module);
                        if (entity != null)
                        {
                            area.AddEntity(entity);
                        }
                    }
                }
            }

            // Load placeables
            if (root.Exists("Placeable List"))
            {
                GFFList placeableList = root.GetList("Placeable List");
                if (placeableList != null)
                {
                    foreach (GFFStruct placeableStruct in placeableList)
                    {
                        IEntity entity = _entityFactory.CreatePlaceableFromGit(placeableStruct, module);
                        if (entity != null)
                        {
                            area.AddEntity(entity);
                        }
                    }
                }
            }

            // Load triggers
            if (root.Exists("TriggerList"))
            {
                GFFList triggerList = root.GetList("TriggerList");
                if (triggerList != null)
                {
                    foreach (GFFStruct triggerStruct in triggerList)
                    {
                        IEntity entity = _entityFactory.CreateTriggerFromGit(triggerStruct);
                        if (entity != null)
                        {
                            area.AddEntity(entity);
                        }
                    }
                }
            }

            // Load waypoints
            if (root.Exists("WaypointList"))
            {
                GFFList waypointList = root.GetList("WaypointList");
                if (waypointList != null)
                {
                    foreach (GFFStruct waypointStruct in waypointList)
                    {
                        IEntity entity = _entityFactory.CreateWaypointFromGit(waypointStruct);
                        if (entity != null)
                        {
                            area.AddEntity(entity);
                        }
                    }
                }
            }

            // Load sounds
            if (root.Exists("SoundList"))
            {
                GFFList soundList = root.GetList("SoundList");
                if (soundList != null)
                {
                    foreach (GFFStruct soundStruct in soundList)
                    {
                        IEntity entity = _entityFactory.CreateSoundFromGit(soundStruct);
                        if (entity != null)
                        {
                            area.AddEntity(entity);
                        }
                    }
                }
            }

            // Load stores
            if (root.Exists("StoreList"))
            {
                GFFList storeList = root.GetList("StoreList");
                if (storeList != null)
                {
                    foreach (GFFStruct storeStruct in storeList)
                    {
                        IEntity entity = _entityFactory.CreateStoreFromGit(storeStruct, module);
                        if (entity != null)
                        {
                            area.AddEntity(entity);
                        }
                    }
                }
            }

            // Load encounters
            if (root.Exists("Encounter List"))
            {
                GFFList encounterList = root.GetList("Encounter List");
                if (encounterList != null)
                {
                    foreach (GFFStruct encounterStruct in encounterList)
                    {
                        IEntity entity = _entityFactory.CreateEncounterFromGit(encounterStruct);
                        if (entity != null)
                        {
                            area.AddEntity(entity);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads navigation mesh from BWM files.
        /// </summary>
        private void LoadNavigationMesh(Module module, RuntimeArea area)
        {
            // For each room, load its walkmesh and combine
            INavigationMesh combinedNavMesh = _navMeshFactory.CreateFromModule(module, area.Rooms);
            area.NavigationMesh = combinedNavMesh;
        }

        /// <summary>
        /// Gets the currently loaded module (if any).
        /// </summary>
        private RuntimeModule _currentModule;

        /// <summary>
        /// Cached Andastra.Parsing Module object for the currently loaded module.
        /// This avoids creating a new Module object every time resources are accessed.
        /// Based on swkotor2.exe: Module objects are cached and reused for resource lookups.
        /// </summary>
        private Andastra.Parsing.Installation.Module _cachedParsingModule;

        /// <summary>
        /// Gets the currently loaded module.
        /// </summary>
        public RuntimeModule CurrentModule => _currentModule;

        /// <summary>
        /// Gets the entity factory for creating entities from templates.
        /// </summary>
        public EntityFactory EntityFactory => _entityFactory;

        /// <summary>
        /// Gets the cached Andastra.Parsing Module for the currently loaded runtime module.
        /// Returns the cached Module object to avoid recreating it on every access.
        /// Based on swkotor2.exe: Module objects are cached and reused for resource lookups.
        /// </summary>
        public Andastra.Parsing.Installation.Module GetCurrentModule()
        {
            return _cachedParsingModule;
        }

        /// <summary>
        /// Loads a dialogue by ResRef.
        /// </summary>
        public Andastra.Parsing.Resource.Generics.DLG.DLG LoadDialogue(string resRef)
        {
            if (_cachedParsingModule == null)
            {
                return null;
            }

            // Load from module resources using cached Module object
            ModuleResource dlgResource = _cachedParsingModule.Resource(resRef, ResourceType.DLG);
            if (dlgResource == null)
            {
                return null;
            }

            object dlgData = dlgResource.Resource();
            return dlgData as Andastra.Parsing.Resource.Generics.DLG.DLG;
        }

        /// <summary>
        /// Loads a script (NCS) by ResRef.
        /// </summary>
        public byte[] LoadScript(string resRef)
        {
            if (_cachedParsingModule == null)
            {
                return null;
            }

            // Load from module resources using cached Module object
            ModuleResource ncsResource = _cachedParsingModule.Resource(resRef, ResourceType.NCS);
            if (ncsResource == null)
            {
                return null;
            }

            // Activate the resource to get the file path
            string activePath = ncsResource.Activate();
            if (string.IsNullOrEmpty(activePath))
            {
                return null;
            }

            // Read NCS bytes using ResourceAuto
            return Andastra.Parsing.Resource.ResourceAuto.ReadResource(activePath, ResourceType.NCS);
        }

        /// <summary>
        /// Gets the navigation mesh for the current module's entry area.
        /// </summary>
        public Andastra.Runtime.Core.Navigation.NavigationMesh GetNavigationMesh()
        {
            if (_currentModule == null)
            {
                return null;
            }

            IArea entryArea = _currentModule.GetArea(_currentModule.EntryArea);
            if (entryArea == null)
            {
                return null;
            }

            return entryArea.NavigationMesh as Andastra.Runtime.Core.Navigation.NavigationMesh;
        }

        /// <summary>
        /// Gets the entry position for the current module.
        /// </summary>
        public System.Numerics.Vector3 GetEntryPosition()
        {
            if (_currentModule == null)
            {
                return System.Numerics.Vector3.Zero;
            }

            return _currentModule.EntryPosition;
        }

        /// <summary>
        /// Gets the entry facing angle for the current module.
        /// </summary>
        public float GetEntryFacing()
        {
            if (_currentModule == null)
            {
                return 0f;
            }

            return _currentModule.EntryFacing;
        }

        #region Helper Methods

        private static string GetStringField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                ResRef resRef = root.GetResRef(fieldName);
                if (resRef != null)
                {
                    return resRef.ToString();
                }
                // Try as string
                return root.GetString(fieldName) ?? string.Empty;
            }
            return string.Empty;
        }

        private static int GetIntField(GFFStruct root, string fieldName, int defaultValue = 0)
        {
            if (root.Exists(fieldName))
            {
                return root.GetInt32(fieldName);
            }
            return defaultValue;
        }

        #endregion
    }
}

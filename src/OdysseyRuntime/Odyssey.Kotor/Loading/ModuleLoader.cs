using System;
using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using Vector3 = System.Numerics.Vector3;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using Odyssey.Content.Interfaces;
using CSharpKOTOR.Installation;
using JetBrains.Annotations;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Module;

namespace Odyssey.Kotor.Loading
{
    /// <summary>
    /// Loads KotOR modules from CSharpKOTOR data structures into Odyssey runtime format.
    /// </summary>
    /// <remarks>
    /// Module Loading Sequence (from IFO spec):
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

            // Create CSharpKOTOR Module wrapper
            var module = new Module(moduleName, _installation);
            
            // Create runtime module
            var runtimeModule = new RuntimeModule();
            
            // Load IFO (module info)
            LoadModuleInfo(module, runtimeModule);
            
            // Load entry area
            string entryAreaResRef = runtimeModule.EntryArea;
            if (!string.IsNullOrEmpty(entryAreaResRef))
            {
                var area = LoadArea(module, entryAreaResRef);
                if (area != null)
                {
                    runtimeModule.AddArea(area);
                }
            }

            return runtimeModule;
        }

        /// <summary>
        /// Loads module info from IFO.
        /// </summary>
        private void LoadModuleInfo(Module module, RuntimeModule runtimeModule)
        {
            var ifoResource = module.Info();
            if (ifoResource == null)
            {
                throw new InvalidOperationException("Module has no IFO resource");
            }

            object ifoData = ifoResource.Resource();
            if (ifoData == null)
            {
                throw new InvalidOperationException("Failed to load module IFO");
            }

            // IFO is a GFF file
            GFF ifoGff = ifoData as GFF;
            if (ifoGff == null)
            {
                throw new InvalidOperationException("IFO resource is not a valid GFF");
            }

            GFFStruct root = ifoGff.Root;

            // Basic info
            runtimeModule.ResRef = module.GetRoot();
            runtimeModule.Tag = GetStringField(root, "Mod_Tag");

            // Display name (localized string)
            if (root.Exists("Mod_Name"))
            {
                var nameLocStr = root.GetLocString("Mod_Name");
                runtimeModule.DisplayName = nameLocStr != null ? nameLocStr.ToString() : string.Empty;
            }

            // Entry area
            if (root.Exists("Mod_Entry_Area"))
            {
                var entryAreaRef = root.GetResRef("Mod_Entry_Area");
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
                var movieRef = root.GetResRef("Mod_StartMovie");
                runtimeModule.StartMovie = movieRef != null ? movieRef.ToString() : string.Empty;
            }

            // Scripts
            LoadModuleScripts(root, runtimeModule);
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

            foreach (var mapping in scriptMappings)
            {
                if (root.Exists(mapping.Key))
                {
                    var scriptRef = root.GetResRef(mapping.Key);
                    if (scriptRef != null && !string.IsNullOrEmpty(scriptRef.ToString()))
                    {
                        module.SetScript(mapping.Value, scriptRef.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Loads an area from ARE + GIT + LYT + VIS.
        /// </summary>
        [CanBeNull]
        public RuntimeArea LoadArea(Module module, string areaResRef)
        {
            var area = new RuntimeArea();
            area.ResRef = areaResRef;

            // Load ARE (area properties)
            var areResource = module.Are();
            if (areResource != null)
            {
                LoadAreaProperties(areResource, area);
            }

            // Load LYT (room layout)
            var lytResource = module.Layout();
            if (lytResource != null)
            {
                LoadLayout(lytResource, area);
            }

            // Load VIS (visibility)
            var visResource = module.Vis();
            if (visResource != null)
            {
                LoadVisibility(visResource, area);
            }

            // Load GIT (dynamic objects)
            var gitResource = module.Git();
            if (gitResource != null)
            {
                LoadGitObjects(gitResource, area, module);
            }

            // Load navigation mesh from BWM files
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
                var nameLocStr = root.GetLocString("Name");
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
                var grassTex = root.GetResRef("Grass_TexName");
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

            foreach (var mapping in scriptMappings)
            {
                if (root.Exists(mapping.Key))
                {
                    var scriptRef = root.GetResRef(mapping.Key);
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

            foreach (var room in lyt.Rooms)
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
                var creatureList = root.GetList("Creature List");
                if (creatureList != null)
                {
                    foreach (var creatureStruct in creatureList)
                    {
                        var entity = _entityFactory.CreateCreatureFromGit(creatureStruct, module);
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
                var doorList = root.GetList("Door List");
                if (doorList != null)
                {
                    foreach (var doorStruct in doorList)
                    {
                        var entity = _entityFactory.CreateDoorFromGit(doorStruct, module);
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
                var placeableList = root.GetList("Placeable List");
                if (placeableList != null)
                {
                    foreach (var placeableStruct in placeableList)
                    {
                        var entity = _entityFactory.CreatePlaceableFromGit(placeableStruct, module);
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
                var triggerList = root.GetList("TriggerList");
                if (triggerList != null)
                {
                    foreach (var triggerStruct in triggerList)
                    {
                        var entity = _entityFactory.CreateTriggerFromGit(triggerStruct);
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
                var waypointList = root.GetList("WaypointList");
                if (waypointList != null)
                {
                    foreach (var waypointStruct in waypointList)
                    {
                        var entity = _entityFactory.CreateWaypointFromGit(waypointStruct);
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
                var soundList = root.GetList("SoundList");
                if (soundList != null)
                {
                    foreach (var soundStruct in soundList)
                    {
                        var entity = _entityFactory.CreateSoundFromGit(soundStruct);
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
                var storeList = root.GetList("StoreList");
                if (storeList != null)
                {
                    foreach (var storeStruct in storeList)
                    {
                        var entity = _entityFactory.CreateStoreFromGit(storeStruct);
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
                var encounterList = root.GetList("Encounter List");
                if (encounterList != null)
                {
                    foreach (var encounterStruct in encounterList)
                    {
                        var entity = _entityFactory.CreateEncounterFromGit(encounterStruct);
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
            var combinedNavMesh = _navMeshFactory.CreateFromModule(module, area.Rooms);
            area.NavigationMesh = combinedNavMesh;
        }

        #region Helper Methods

        private static string GetStringField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                var resRef = root.GetResRef(fieldName);
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

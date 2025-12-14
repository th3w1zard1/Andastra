using System;
using System.Collections.Generic;
using System.Numerics;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Module;

namespace Odyssey.Kotor.Loading
{
    /// <summary>
    /// Loads modules from game resources (IFO/ARE/GIT files).
    /// </summary>
    /// <remarks>
    /// Module Loading Sequence (matches original engine):
    /// 1. Load module.ifo - Module metadata
    /// 2. Load area ARE - Area properties
    /// 3. Load layout LYT - Room positions
    /// 4. Load visibility VIS - Room visibility
    /// 5. Load area GIT - Entity instances
    /// 6. Spawn entities from GIT
    /// </remarks>
    public class ModuleLoader
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly EntityFactory _entityFactory;
        private readonly IWorld _world;

        public ModuleLoader(IGameResourceProvider resourceProvider, IWorld world)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _world = world ?? throw new ArgumentNullException("world");
            _entityFactory = new EntityFactory(world);
        }

        /// <summary>
        /// Loads a module from its resource name.
        /// </summary>
        public RuntimeModule LoadModule(string moduleResRef)
        {
            if (string.IsNullOrEmpty(moduleResRef))
            {
                throw new ArgumentNullException("moduleResRef");
            }

            // Load module.ifo
            var ifoGff = LoadGff(moduleResRef, "module", "ifo");
            if (ifoGff == null)
            {
                throw new InvalidOperationException("Failed to load module.ifo for module: " + moduleResRef);
            }

            var module = ParseModuleInfo(ifoGff, moduleResRef);

            // Load entry area
            if (!string.IsNullOrEmpty(module.EntryArea))
            {
                var entryArea = LoadArea(moduleResRef, module.EntryArea);
                if (entryArea != null)
                {
                    module.AddArea(entryArea);
                }
            }

            return module;
        }

        /// <summary>
        /// Loads an area from its resource name.
        /// </summary>
        public RuntimeArea LoadArea(string moduleResRef, string areaResRef)
        {
            if (string.IsNullOrEmpty(areaResRef))
            {
                return null;
            }

            // Load ARE (area properties)
            var areGff = LoadGff(moduleResRef, areaResRef, "are");
            if (areGff == null)
            {
                return null;
            }

            var area = ParseAreaProperties(areGff, areaResRef);

            // Load LYT (layout)
            var lyt = LoadLyt(moduleResRef, areaResRef);
            if (lyt != null)
            {
                ParseLayout(area, lyt);
            }

            // Load VIS (visibility)
            var vis = LoadVis(moduleResRef, areaResRef);
            if (vis != null)
            {
                ParseVisibility(area, vis);
            }

            // Load GIT (instances) and spawn entities
            var gitGff = LoadGff(moduleResRef, areaResRef, "git");
            if (gitGff != null)
            {
                SpawnEntities(area, gitGff);
            }

            return area;
        }

        #region IFO Parsing

        private RuntimeModule ParseModuleInfo(GFF ifo, string moduleResRef)
        {
            var module = new RuntimeModule();
            var root = ifo.Root;

            module.ResRef = moduleResRef;

            // Name and identifiers
            if (root.TryGetLocString("Mod_Name", out LocalizedString modName))
            {
                module.DisplayName = modName.GetString(Language.English) ?? string.Empty;
            }
            module.Tag = root.GetString("Mod_Tag");

            // Entry point
            module.EntryArea = root.GetResRef("Mod_Entry_Area").Value ?? string.Empty;
            module.EntryPosition = new Vector3(
                root.GetSingle("Mod_Entry_X"),
                root.GetSingle("Mod_Entry_Y"),
                root.GetSingle("Mod_Entry_Z")
            );
            module.EntryDirectionX = root.GetSingle("Mod_Entry_Dir_X");
            module.EntryDirectionY = root.GetSingle("Mod_Entry_Dir_Y");

            // Time settings
            module.DawnHour = root.GetInt32("Mod_DawnHour");
            module.DuskHour = root.GetInt32("Mod_DuskHour");
            module.MinutesPastMidnight = root.GetInt32("Mod_MinPerHour");
            module.Day = root.GetInt32("Mod_StartDay");
            module.Month = root.GetInt32("Mod_StartMonth");
            module.Year = root.GetInt32("Mod_StartYear");

            // Game settings
            module.XPScale = root.GetInt32("Mod_XPScale");
            module.StartMovie = root.GetString("Mod_StartMovie");
            module.VoiceOverId = root.GetString("Mod_VO_ID");
            module.ExpansionPack = root.GetInt32("Expansion_Pack");
            module.MinGameVersion = root.GetString("Mod_MinGameVer");
            module.HakFiles = root.GetString("Mod_Hak");
            module.CacheNSSData = root.GetUInt8("Mod_CacheNSSData") != 0;

            // Module ID (GUID)
            module.ModuleId = root.GetBinary("Mod_ID");

            // Module scripts
            SetModuleScript(module, root, "Mod_OnAcquirItem", ScriptEvent.OnAcquireItem);
            SetModuleScript(module, root, "Mod_OnActvtItem", ScriptEvent.OnActivateItem);
            SetModuleScript(module, root, "Mod_OnClientEntr", ScriptEvent.OnClientEnter);
            SetModuleScript(module, root, "Mod_OnClientLeav", ScriptEvent.OnClientLeave);
            SetModuleScript(module, root, "Mod_OnHeartbeat", ScriptEvent.OnHeartbeat);
            SetModuleScript(module, root, "Mod_OnModLoad", ScriptEvent.OnModuleLoad);
            SetModuleScript(module, root, "Mod_OnModStart", ScriptEvent.OnModuleStart);
            SetModuleScript(module, root, "Mod_OnPlrDeath", ScriptEvent.OnPlayerDeath);
            SetModuleScript(module, root, "Mod_OnPlrDying", ScriptEvent.OnPlayerDying);
            SetModuleScript(module, root, "Mod_OnPlrLvlUp", ScriptEvent.OnPlayerLevelUp);
            SetModuleScript(module, root, "Mod_OnPlrRest", ScriptEvent.OnPlayerRest);
            SetModuleScript(module, root, "Mod_OnSpawnBtnDn", ScriptEvent.OnSpawnButtonDown);
            SetModuleScript(module, root, "Mod_OnUnAqreItem", ScriptEvent.OnUnacquireItem);
            SetModuleScript(module, root, "Mod_OnUsrDefined", ScriptEvent.OnUserDefined);

            return module;
        }

        private void SetModuleScript(RuntimeModule module, GFFStruct root, string fieldName, ScriptEvent eventType)
        {
            var script = root.GetResRef(fieldName);
            if (script != null && !string.IsNullOrEmpty(script.Value))
            {
                module.SetScript(eventType, script.Value);
            }
        }

        #endregion

        #region ARE Parsing

        private RuntimeArea ParseAreaProperties(GFF are, string areaResRef)
        {
            var area = new RuntimeArea();
            var root = are.Root;

            area.ResRef = areaResRef;
            area.Tag = root.GetString("Tag");

            // Name
            if (root.TryGetLocString("Name", out LocalizedString areaName))
            {
                area.DisplayName = areaName.GetString(Language.English) ?? string.Empty;
            }

            // Ambient lighting
            area.AmbientColor = root.GetUInt32("AmbientSndDay");
            area.DynamicAmbientColor = root.GetUInt32("DynAmbientColor");

            // Fog settings
            area.FogEnabled = root.GetUInt8("SunFogOn") != 0;
            area.FogColor = root.GetUInt32("FogColor");
            area.FogNear = root.GetSingle("FogNear");
            area.FogFar = root.GetSingle("FogFar");
            area.SunFogColor = root.GetUInt32("SunFogColor");

            // Sun colors
            area.SunDiffuseColor = root.GetUInt32("SunDiffuseColor");
            area.SunAmbientColor = root.GetUInt32("SunAmbientColor");

            // Grass
            area.GrassEnabled = root.GetUInt8("Grass_Ambient") != 0;
            area.GrassTexture = root.GetString("Grass_TexName");
            area.GrassDensity = root.GetSingle("Grass_Density");
            area.GrassQuadSize = root.GetSingle("Grass_QuadSize");

            // Music
            area.MusicDay = root.GetInt32("MusicDay");
            area.MusicNight = root.GetInt32("MusicNight");
            area.MusicBattle = root.GetInt32("MusicBattle");

            // Ambient sounds
            area.AmbientSndDay = root.GetInt32("AmbientSndDay");
            area.AmbientSndNight = root.GetInt32("AmbientSndNight");

            // Flags
            area.IsInterior = (root.GetUInt32("Flags") & 1) != 0;
            area.IsUnderground = (root.GetUInt32("Flags") & 2) != 0;

            // Weather
            area.HasWeather = root.GetUInt8("ChanceRain") > 0 ||
                              root.GetUInt8("ChanceSnow") > 0 ||
                              root.GetUInt8("ChanceLightning") > 0;
            area.WeatherType = root.GetInt32("ChanceRain") > 50 ? 1 :
                               root.GetInt32("ChanceSnow") > 50 ? 2 : 0;

            // Area scripts
            SetAreaScript(area, root, "OnEnter", ScriptEvent.OnEnter);
            SetAreaScript(area, root, "OnExit", ScriptEvent.OnExit);
            SetAreaScript(area, root, "OnHeartbeat", ScriptEvent.OnHeartbeat);
            SetAreaScript(area, root, "OnUserDefined", ScriptEvent.OnUserDefined);

            return area;
        }

        private void SetAreaScript(RuntimeArea area, GFFStruct root, string fieldName, ScriptEvent eventType)
        {
            var script = root.GetResRef(fieldName);
            if (script != null && !string.IsNullOrEmpty(script.Value))
            {
                area.SetScript(eventType, script.Value);
            }
        }

        #endregion

        #region LYT/VIS Parsing

        private void ParseLayout(RuntimeArea area, LYT lyt)
        {
            area.Rooms.Clear();

            for (int i = 0; i < lyt.Rooms.Count; i++)
            {
                var lytRoom = lyt.Rooms[i];
                var roomInfo = new RoomInfo
                {
                    ModelName = lytRoom.Model,
                    Position = new Vector3(lytRoom.Position.X, lytRoom.Position.Y, lytRoom.Position.Z),
                    Rotation = 0f // LYT doesn't store rotation
                };
                area.Rooms.Add(roomInfo);
            }
        }

        private void ParseVisibility(RuntimeArea area, VIS vis)
        {
            // Build visibility groups from VIS data
            var roomNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < area.Rooms.Count; i++)
            {
                roomNames[area.Rooms[i].ModelName] = i;
            }

            foreach (var entry in vis.GetEnumerator())
            {
                string whenInside = entry.Item1;
                HashSet<string> visibleRooms = entry.Item2;

                int roomIndex;
                if (roomNames.TryGetValue(whenInside, out roomIndex))
                {
                    var roomInfo = area.Rooms[roomIndex];
                    roomInfo.VisibleRooms.Clear();

                    foreach (string visibleRoom in visibleRooms)
                    {
                        int visibleIndex;
                        if (roomNames.TryGetValue(visibleRoom, out visibleIndex))
                        {
                            roomInfo.VisibleRooms.Add(visibleIndex);
                        }
                    }
                }
            }
        }

        #endregion

        #region GIT Parsing and Entity Spawning

        private void SpawnEntities(RuntimeArea area, GFF git)
        {
            var root = git.Root;

            // Spawn creatures
            GFFList creatureList;
            if (root.TryGetList("Creature List", out creatureList))
            {
                foreach (var instance in creatureList)
                {
                    var entity = _entityFactory.SpawnCreature(instance);
                    if (entity != null)
                    {
                        area.AddEntity(entity);
                    }
                }
            }

            // Spawn placeables
            GFFList placeableList;
            if (root.TryGetList("Placeable List", out placeableList))
            {
                foreach (var instance in placeableList)
                {
                    var entity = _entityFactory.SpawnPlaceable(instance);
                    if (entity != null)
                    {
                        area.AddEntity(entity);
                    }
                }
            }

            // Spawn doors
            GFFList doorList;
            if (root.TryGetList("Door List", out doorList))
            {
                foreach (var instance in doorList)
                {
                    var entity = _entityFactory.SpawnDoor(instance);
                    if (entity != null)
                    {
                        area.AddEntity(entity);
                    }
                }
            }

            // Spawn triggers
            GFFList triggerList;
            if (root.TryGetList("TriggerList", out triggerList))
            {
                foreach (var instance in triggerList)
                {
                    var entity = _entityFactory.SpawnTrigger(instance);
                    if (entity != null)
                    {
                        area.AddEntity(entity);
                    }
                }
            }

            // Spawn waypoints
            GFFList waypointList;
            if (root.TryGetList("WaypointList", out waypointList))
            {
                foreach (var instance in waypointList)
                {
                    var entity = _entityFactory.SpawnWaypoint(instance);
                    if (entity != null)
                    {
                        area.AddEntity(entity);
                    }
                }
            }

            // Spawn sounds
            GFFList soundList;
            if (root.TryGetList("SoundList", out soundList))
            {
                foreach (var instance in soundList)
                {
                    var entity = _entityFactory.SpawnSound(instance);
                    if (entity != null)
                    {
                        area.AddEntity(entity);
                    }
                }
            }

            // Spawn stores
            GFFList storeList;
            if (root.TryGetList("StoreList", out storeList))
            {
                foreach (var instance in storeList)
                {
                    var entity = _entityFactory.SpawnStore(instance);
                    if (entity != null)
                    {
                        area.AddEntity(entity);
                    }
                }
            }

            // Spawn encounters
            GFFList encounterList;
            if (root.TryGetList("Encounter List", out encounterList))
            {
                foreach (var instance in encounterList)
                {
                    var entity = _entityFactory.SpawnEncounter(instance);
                    if (entity != null)
                    {
                        area.AddEntity(entity);
                    }
                }
            }
        }

        #endregion

        #region Resource Loading Helpers

        private GFF LoadGff(string moduleResRef, string resRef, string extension)
        {
            try
            {
                // Resource loading would use the provider
                // For now, return null - actual implementation needs async resource loading
                return null;
            }
            catch
            {
                return null;
            }
        }

        private LYT LoadLyt(string moduleResRef, string areaResRef)
        {
            try
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        private VIS LoadVis(string moduleResRef, string areaResRef)
        {
            try
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}

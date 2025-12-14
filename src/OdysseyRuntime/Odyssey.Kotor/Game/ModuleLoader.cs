using System;
using System.IO;
using System.Collections.Generic;
using Odyssey.Core.Entities;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Module;
using Odyssey.Core.Navigation;
using CSharpKOTOR.Common;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using CSharpKOTOR.Formats.BWM;
using CSharpKOTOR.Formats.MDL;
using CSharpKOTOR.Formats.TPC;
using MDLData = CSharpKOTOR.Formats.MDLData;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resource.Generics.DLG;
using JetBrains.Annotations;

// Explicit type aliases to resolve ambiguity
using SysVector3 = System.Numerics.Vector3;
using KotorVector3 = CSharpKOTOR.Common.Vector3;
using OdyObjectType = Odyssey.Core.Enums.ObjectType;
using InstResourceResult = CSharpKOTOR.Installation.ResourceResult;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.Game
{
    /// <summary>
    /// Loads modules from KOTOR game files using CSharpKOTOR resource infrastructure.
    /// </summary>
    public class ModuleLoader
    {
        private readonly string _gamePath;
        private readonly World _world;
        private readonly Installation _installation;
        private NavigationMesh _currentNavMesh;
        private RuntimeArea _currentArea;
        private string _currentModuleRoot;

        // Cached module resources
        private IFO _currentIfo;
        private GIT _currentGit;
        private LYT _currentLyt;
        private VIS _currentVis;

        /// <summary>
        /// Gets the current IFO (module info).
        /// </summary>
        [CanBeNull]
        public IFO CurrentIFO
        {
            get { return _currentIfo; }
        }

        /// <summary>
        /// Gets the current GIT (game instance data).
        /// </summary>
        [CanBeNull]
        public GIT CurrentGIT
        {
            get { return _currentGit; }
        }

        /// <summary>
        /// Gets the current LYT (area layout).
        /// </summary>
        [CanBeNull]
        public LYT CurrentLYT
        {
            get { return _currentLyt; }
        }

        /// <summary>
        /// Gets the current VIS (visibility data).
        /// </summary>
        [CanBeNull]
        public VIS CurrentVIS
        {
            get { return _currentVis; }
        }

        public ModuleLoader(string gamePath, World world)
        {
            _gamePath = gamePath;
            _world = world;

            try
            {
                _installation = new Installation(gamePath);
                Console.WriteLine("[ModuleLoader] Installation initialized: " + _installation.Game);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] WARNING: Failed to initialize Installation: " + ex.Message);
                _installation = null;
            }
        }

        /// <summary>
        /// Gets the navigation mesh for the current module.
        /// </summary>
        [CanBeNull]
        public NavigationMesh GetNavigationMesh()
        {
            return _currentNavMesh;
        }

        /// <summary>
        /// Loads a dialogue by ResRef.
        /// </summary>
        [CanBeNull]
        public DLG LoadDialogue(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return null;
            }

            try
            {
                InstResourceResult result = _installation?.Resource(resRef, ResourceType.DLG, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    GFF gff = GFF.FromBytes(result.Data);
                    return DLGHelper.ConstructDlg(gff);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load dialogue " + resRef + ": " + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Loads a script by ResRef.
        /// </summary>
        [CanBeNull]
        public byte[] LoadScript(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return null;
            }

            try
            {
                InstResourceResult result = _installation?.Resource(resRef, ResourceType.NCS, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    return result.Data;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load script " + resRef + ": " + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Load a module by name.
        /// </summary>
        public void LoadModule(string moduleName)
        {
            Console.WriteLine("[ModuleLoader] Loading module: " + moduleName);
            _currentModuleRoot = moduleName;

            // Clear previous module data
            ClearWorld();
            _currentIfo = null;
            _currentGit = null;
            _currentLyt = null;
            _currentVis = null;

            if (_installation == null)
            {
                Console.WriteLine("[ModuleLoader] WARNING: No installation - using placeholder data");
                CreatePlaceholderModule(moduleName);
                return;
            }

            // Load IFO (module info)
            LoadIFO(moduleName);

            // Load LYT (area layout)
            LoadLYT(moduleName);

            // Load VIS (visibility)
            LoadVIS(moduleName);

            // Load GIT (game instance data - entities)
            LoadGIT(moduleName);

            // Load walkmesh for navigation
            LoadWalkmesh(moduleName);

            // Create runtime module and area
            RuntimeModule runtimeModule = CreateRuntimeModule(moduleName);
            _world.CurrentModule = runtimeModule;

            RuntimeArea runtimeArea = CreateRuntimeArea(moduleName);
            _world.CurrentArea = runtimeArea;
            _currentArea = runtimeArea;

            // Spawn entities from GIT
            if (_currentGit != null)
            {
                SpawnEntitiesFromGIT(_currentGit, runtimeArea);
            }

            Console.WriteLine("[ModuleLoader] Module loaded: " + moduleName);
        }

        private void ClearWorld()
        {
            // Get all entities and destroy them
            var entities = new List<IEntity>(_world.GetAllEntities());
            foreach (IEntity entity in entities)
            {
                _world.DestroyEntity(entity.ObjectId);
            }
        }

        private void LoadIFO(string moduleName)
        {
            try
            {
                InstResourceResult result = _installation.Resource("module", ResourceType.IFO, null, moduleName);
                if (result != null && result.Data != null)
                {
                    GFF gff = GFF.FromBytes(result.Data);
                    _currentIfo = IFOHelpers.ConstructIfo(gff);
                    Console.WriteLine("[ModuleLoader] Loaded IFO - Entry: " + _currentIfo.EntryArea +
                                      " at (" + _currentIfo.EntryX + ", " + _currentIfo.EntryY + ", " + _currentIfo.EntryZ + ")");
                }
                else
                {
                    Console.WriteLine("[ModuleLoader] WARNING: module.ifo not found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load IFO: " + ex.Message);
            }
        }

        private void LoadLYT(string moduleName)
        {
            try
            {
                InstResourceResult result = _installation.Resource(moduleName, ResourceType.LYT, null, moduleName);
                if (result != null && result.Data != null)
                {
                    _currentLyt = LYTAuto.ReadLyt(result.Data);
                    Console.WriteLine("[ModuleLoader] Loaded LYT - " + _currentLyt.Rooms.Count + " rooms, " +
                                      _currentLyt.Doorhooks.Count + " doorhooks");
                }
                else
                {
                    Console.WriteLine("[ModuleLoader] WARNING: LYT not found for " + moduleName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load LYT: " + ex.Message);
            }
        }

        private void LoadVIS(string moduleName)
        {
            try
            {
                InstResourceResult result = _installation.Resource(moduleName, ResourceType.VIS, null, moduleName);
                if (result != null && result.Data != null)
                {
                    _currentVis = VISAuto.ReadVis(result.Data);
                    Console.WriteLine("[ModuleLoader] Loaded VIS");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load VIS: " + ex.Message);
            }
        }

        private void LoadGIT(string moduleName)
        {
            try
            {
                InstResourceResult result = _installation.Resource(moduleName, ResourceType.GIT, null, moduleName);
                if (result != null && result.Data != null)
                {
                    GFF gff = GFF.FromBytes(result.Data);
                    _currentGit = GITHelpers.ConstructGit(gff);
                    Console.WriteLine("[ModuleLoader] Loaded GIT - " +
                                      _currentGit.Creatures.Count + " creatures, " +
                                      _currentGit.Doors.Count + " doors, " +
                                      _currentGit.Placeables.Count + " placeables, " +
                                      _currentGit.Triggers.Count + " triggers, " +
                                      _currentGit.Waypoints.Count + " waypoints");
                }
                else
                {
                    Console.WriteLine("[ModuleLoader] WARNING: GIT not found for " + moduleName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load GIT: " + ex.Message);
            }
        }

        private void LoadWalkmesh(string moduleName)
        {
            _currentNavMesh = new NavigationMesh();

            // Load WOK (area walkmesh) files for each room in LYT
            if (_currentLyt != null)
            {
                var allVertices = new List<SysVector3>();
                var allIndices = new List<int>();

                foreach (LYTRoom room in _currentLyt.Rooms)
                {
                    try
                    {
                        string wokName = room.Model.ToLowerInvariant();
                        InstResourceResult result = _installation.Resource(wokName, ResourceType.WOK, null, moduleName);
                        if (result != null && result.Data != null)
                        {
                            BWM bwm = BWMAuto.ReadBwm(result.Data);

                            // Room offset
                            KotorVector3 roomOffset = room.Position;
                            int vertexOffset = allVertices.Count;

                            // Get unique vertices from BWM
                            List<KotorVector3> bwmVertices = bwm.Vertices();

                            // Add vertices with room offset
                            foreach (KotorVector3 vertex in bwmVertices)
                            {
                                allVertices.Add(new SysVector3(
                                    vertex.X + roomOffset.X,
                                    vertex.Y + roomOffset.Y,
                                    vertex.Z + roomOffset.Z));
                            }

                            // Add face indices (only walkable faces)
                            // BWMFace stores actual vertex positions, so we need to find indices
                            foreach (BWMFace face in bwm.Faces)
                            {
                                // Check if face is walkable (material check)
                                if (IsFaceWalkable(face))
                                {
                                    int idx1 = FindVertexIndex(bwmVertices, face.V1);
                                    int idx2 = FindVertexIndex(bwmVertices, face.V2);
                                    int idx3 = FindVertexIndex(bwmVertices, face.V3);

                                    if (idx1 >= 0 && idx2 >= 0 && idx3 >= 0)
                                    {
                                        allIndices.Add(idx1 + vertexOffset);
                                        allIndices.Add(idx2 + vertexOffset);
                                        allIndices.Add(idx3 + vertexOffset);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ModuleLoader] Failed to load WOK for room " + room.Model + ": " + ex.Message);
                    }
                }

                if (allVertices.Count > 0 && allIndices.Count > 0)
                {
                    _currentNavMesh.BuildFromTriangles(allVertices, allIndices);
                    Console.WriteLine("[ModuleLoader] Built NavMesh: " + allVertices.Count + " vertices, " +
                                      (allIndices.Count / 3) + " faces");
                }
                else
                {
                    Console.WriteLine("[ModuleLoader] WARNING: No walkmesh data loaded, creating placeholder");
                    CreatePlaceholderNavMesh();
                }
            }
            else
            {
                Console.WriteLine("[ModuleLoader] WARNING: No LYT data, creating placeholder navmesh");
                CreatePlaceholderNavMesh();
            }
        }

        private int FindVertexIndex(List<KotorVector3> vertices, KotorVector3 target)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertices[i].Equals(target))
                {
                    return i;
                }
            }
            return -1;
        }

        private bool IsFaceWalkable(BWMFace face)
        {
            // Surface material 0-30 are typically walkable
            // Material 7 = non-walk, 10+ = special surfaces
            // This is a simplified check - could use surfacemat.2da for full accuracy
            int material = (int)face.Material;
            return material < 7 || (material >= 8 && material <= 10);
        }

        private void CreatePlaceholderNavMesh()
        {
            var vertices = new List<SysVector3>
            {
                new SysVector3(-50, -50, 0),
                new SysVector3(50, -50, 0),
                new SysVector3(50, 50, 0),
                new SysVector3(-50, 50, 0)
            };
            var indices = new List<int> { 0, 1, 2, 0, 2, 3 };
            _currentNavMesh.BuildFromTriangles(vertices, indices);
        }

        private RuntimeModule CreateRuntimeModule(string moduleName)
        {
            var module = new RuntimeModule();
            module.ResRef = moduleName;

            if (_currentIfo != null)
            {
                module.DisplayName = _currentIfo.ModName?.ToString() ?? ("Module: " + moduleName);
                module.EntryArea = _currentIfo.EntryArea?.ToString() ?? moduleName;
                module.DawnHour = _currentIfo.DawnHour;
                module.DuskHour = _currentIfo.DuskHour;
            }
            else
            {
                module.DisplayName = "Module: " + moduleName;
                module.EntryArea = moduleName;
                module.DawnHour = 6;
                module.DuskHour = 18;
            }

            return module;
        }

        private RuntimeArea CreateRuntimeArea(string areaName)
        {
            var area = new RuntimeArea();
            area.ResRef = areaName;
            area.DisplayName = "Area: " + areaName;
            area.Tag = areaName;

            // Load ARE file for ambient lighting, fog, etc.
            LoadAreaProperties(area, areaName);
            return area;
        }

        /// <summary>
        /// Loads ARE file properties (lighting, fog, etc.) into the runtime area.
        /// </summary>
        private void LoadAreaProperties(RuntimeArea area, string areaResRef)
        {
            try
            {
                InstResourceResult result = _installation.Resource(areaResRef, ResourceType.ARE, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    GFF gff = GFF.FromBytes(result.Data);
                    ARE are = AREHelpers.ConstructAre(gff);

                    // Apply ARE properties to runtime area
                    area.FogEnabled = are.FogEnabled;
                    area.FogNear = are.FogNear;
                    area.FogFar = are.FogFar;
                    
                    // Convert Color to RGBA uint (ARGB format)
                    if (are.FogColor != null)
                    {
                        area.FogColor = (uint)((are.FogColor.A << 24) | (are.FogColor.R << 16) | (are.FogColor.G << 8) | are.FogColor.B);
                    }
                    if (are.SunFogColor != null)
                    {
                        area.SunFogColor = (uint)((are.SunFogColor.A << 24) | (are.SunFogColor.R << 16) | (are.SunFogColor.G << 8) | are.SunFogColor.B);
                    }
                    if (are.DawnColor1 != null)
                    {
                        area.SunAmbientColor = (uint)((are.DawnColor1.A << 24) | (are.DawnColor1.R << 16) | (are.DawnColor1.G << 8) | are.DawnColor1.B);
                    }
                    if (are.DayColor1 != null)
                    {
                        area.SunDiffuseColor = (uint)((are.DayColor1.A << 24) | (are.DayColor1.R << 16) | (are.DayColor1.G << 8) | are.DayColor1.B);
                    }

                    // Grass properties
                    area.GrassEnabled = !string.IsNullOrEmpty(are.GrassTexture.ToString());
                    area.GrassTexture = are.GrassTexture.ToString();
                    area.GrassDensity = are.GrassDensity;
                    area.GrassQuadSize = are.GrassSize;

                    // Script hooks
                    if (!string.IsNullOrEmpty(are.OnEnter.ToString()))
                    {
                        area.SetScript(Core.Enums.ScriptEvent.OnEnter, are.OnEnter.ToString());
                    }
                    if (!string.IsNullOrEmpty(are.OnExit.ToString()))
                    {
                        area.SetScript(Core.Enums.ScriptEvent.OnExit, are.OnExit.ToString());
                    }
                    if (!string.IsNullOrEmpty(are.OnHeartbeat.ToString()))
                    {
                        area.SetScript(Core.Enums.ScriptEvent.OnHeartbeat, are.OnHeartbeat.ToString());
                    }

                    Console.WriteLine("[ModuleLoader] Loaded ARE properties for " + areaResRef);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load ARE file " + areaResRef + ": " + ex.Message);
            }
        }

        private void SpawnEntitiesFromGIT(GIT git, RuntimeArea area)
        {
            Console.WriteLine("[ModuleLoader] Spawning entities from GIT...");
            int count = 0;

            // Spawn waypoints
            foreach (GITWaypoint waypoint in git.Waypoints)
            {
                SpawnWaypoint(waypoint, area);
                count++;
            }

            // Spawn doors
            foreach (GITDoor door in git.Doors)
            {
                SpawnDoor(door, area);
                count++;
            }

            // Spawn placeables
            foreach (GITPlaceable placeable in git.Placeables)
            {
                SpawnPlaceable(placeable, area);
                count++;
            }

            // Spawn creatures
            foreach (GITCreature creature in git.Creatures)
            {
                SpawnCreature(creature, area);
                count++;
            }

            // Spawn triggers
            foreach (GITTrigger trigger in git.Triggers)
            {
                SpawnTrigger(trigger, area);
                count++;
            }

            // Spawn sounds
            foreach (GITSound sound in git.Sounds)
            {
                SpawnSound(sound, area);
                count++;
            }

            // Spawn stores
            foreach (GITStore store in git.Stores)
            {
                SpawnStore(store, area);
                count++;
            }

            // Spawn encounters
            foreach (GITEncounter encounter in git.Encounters)
            {
                SpawnEncounter(encounter, area);
                count++;
            }

            Console.WriteLine("[ModuleLoader] Spawned " + count + " entities");
        }

        private SysVector3 ToSysVector3(KotorVector3 v)
        {
            return new SysVector3(v.X, v.Y, v.Z);
        }

        private void SpawnWaypoint(GITWaypoint waypoint, RuntimeArea area)
        {
            IEntity entity = _world.CreateEntity(OdyObjectType.Waypoint, ToSysVector3(waypoint.Position), waypoint.Bearing);
            entity.Tag = waypoint.Tag;
            area.AddEntity(entity);
        }

        private void SpawnDoor(GITDoor door, RuntimeArea area)
        {
            IEntity entity = _world.CreateEntity(OdyObjectType.Door, ToSysVector3(door.Position), door.Bearing);
            entity.Tag = door.Tag;

            // Load door template
            if (!string.IsNullOrEmpty(door.ResRef?.ToString()))
            {
                LoadDoorTemplate(entity, door.ResRef.ToString());
            }

            // Set door-specific properties from GIT
            IDoorComponent doorComponent = entity.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                doorComponent.LinkedToModule = door.LinkedToModule?.ToString();
                doorComponent.LinkedTo = door.LinkedTo;
            }

            area.AddEntity(entity);
        }

        private void SpawnPlaceable(GITPlaceable placeable, RuntimeArea area)
        {
            IEntity entity = _world.CreateEntity(OdyObjectType.Placeable, ToSysVector3(placeable.Position), placeable.Bearing);

            // Load placeable template
            if (!string.IsNullOrEmpty(placeable.ResRef?.ToString()))
            {
                LoadPlaceableTemplate(entity, placeable.ResRef.ToString());
            }

            area.AddEntity(entity);
        }

        private void SpawnCreature(GITCreature creature, RuntimeArea area)
        {
            IEntity entity = _world.CreateEntity(OdyObjectType.Creature, ToSysVector3(creature.Position), creature.Bearing);

            // Load creature template
            if (!string.IsNullOrEmpty(creature.ResRef?.ToString()))
            {
                LoadCreatureTemplate(entity, creature.ResRef.ToString());
            }

            area.AddEntity(entity);
        }

        private void SpawnTrigger(GITTrigger trigger, RuntimeArea area)
        {
            IEntity entity = _world.CreateEntity(OdyObjectType.Trigger, ToSysVector3(trigger.Position), 0);
            entity.Tag = trigger.Tag;

            // Set trigger geometry
            ITriggerComponent triggerComponent = entity.GetComponent<ITriggerComponent>();
            if (triggerComponent != null)
            {
                var geometryList = new List<SysVector3>();
                foreach (KotorVector3 point in trigger.Geometry)
                {
                    geometryList.Add(ToSysVector3(point));
                }
                triggerComponent.Geometry = geometryList;
                triggerComponent.LinkedToModule = trigger.LinkedToModule?.ToString();
                triggerComponent.LinkedTo = trigger.LinkedTo;
            }

            area.AddEntity(entity);
        }

        private void SpawnSound(GITSound sound, RuntimeArea area)
        {
            IEntity entity = _world.CreateEntity(OdyObjectType.Sound, ToSysVector3(sound.Position), 0);

            // Load sound template (UTS)
            if (!string.IsNullOrEmpty(sound.TemplateResRef))
            {
                LoadSoundTemplate(entity, sound.TemplateResRef);
            }

            area.AddEntity(entity);
        }

        private void SpawnStore(GITStore store, RuntimeArea area)
        {
            IEntity entity = _world.CreateEntity(OdyObjectType.Store, ToSysVector3(store.Position), store.Bearing);

            // Load store template (UTM)
            if (!string.IsNullOrEmpty(store.ResRef?.ToString()))
            {
                LoadStoreTemplate(entity, store.ResRef.ToString());
            }

            // Set store tag
            if (!string.IsNullOrEmpty(store.Tag))
            {
                entity.Tag = store.Tag;
            }

            area.AddEntity(entity);
        }

        /// <summary>
        /// Loads UTM store template and applies properties to entity.
        /// </summary>
        private void LoadStoreTemplate(IEntity entity, string utmResRef)
        {
            try
            {
                InstResourceResult result = _installation.Resource(utmResRef, ResourceType.UTM, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    var gff = GFF.FromBytes(result.Data);
                    UTM utm = UTMHelpers.ConstructUtm(gff);

                    // Apply UTM properties to store component
                    StoreComponent storeComponent = entity.GetComponent<StoreComponent>();
                    if (storeComponent != null)
                    {
                        storeComponent.TemplateResRef = utmResRef;
                        storeComponent.MarkUp = utm.MarkUp;
                        storeComponent.MarkDown = utm.MarkDown;
                        storeComponent.CanBuy = utm.CanBuy;
                        storeComponent.OnOpenStore = utm.OnOpenScript.ToString();
                        
                        // Load items for sale
                        storeComponent.ItemsForSale = new List<StoreItem>();
                        foreach (UTMItem utmItem in utm.Items)
                        {
                            var storeItem = new StoreItem
                            {
                                ResRef = utmItem.ResRef.ToString(),
                                StackSize = 1, // UTM doesn't store stack size, default to 1
                                Infinite = utmItem.Infinite != 0
                            };
                            storeComponent.ItemsForSale.Add(storeItem);
                        }
                    }

                    // Set entity tag
                    if (!string.IsNullOrEmpty(utm.Tag))
                    {
                        entity.Tag = utm.Tag;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load UTM template " + utmResRef + ": " + ex.Message);
            }
        }

        private void SpawnEncounter(GITEncounter encounter, RuntimeArea area)
        {
            IEntity entity = _world.CreateEntity(OdyObjectType.Encounter, ToSysVector3(encounter.Position), 0);

            // Load encounter template (UTE)
            if (!string.IsNullOrEmpty(encounter.ResRef?.ToString()))
            {
                LoadEncounterTemplate(entity, encounter.ResRef.ToString());
            }

            // Set encounter geometry from GIT
            EncounterComponent encounterComponent = entity.GetComponent<EncounterComponent>();
            if (encounterComponent != null)
            {
                // Set geometry vertices
                encounterComponent.Vertices = new List<SysVector3>();
                foreach (KotorVector3 vertex in encounter.Geometry)
                {
                    encounterComponent.Vertices.Add(ToSysVector3(vertex));
                }

                // Set spawn points
                encounterComponent.SpawnPoints = new List<EncounterSpawnPoint>();
                foreach (GITEncounterSpawnPoint spawnPoint in encounter.SpawnPoints)
                {
                    var point = new EncounterSpawnPoint
                    {
                        Position = new SysVector3(spawnPoint.X, spawnPoint.Y, spawnPoint.Z),
                        Orientation = spawnPoint.Orientation
                    };
                    encounterComponent.SpawnPoints.Add(point);
                }
            }

            area.AddEntity(entity);
        }

        /// <summary>
        /// Loads UTE encounter template and applies properties to entity.
        /// </summary>
        private void LoadEncounterTemplate(IEntity entity, string uteResRef)
        {
            try
            {
                InstResourceResult result = _installation.Resource(uteResRef, ResourceType.UTE, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    var gff = GFF.FromBytes(result.Data);
                    UTE ute = UTEHelpers.ConstructUte(gff);

                    // Apply UTE properties to encounter component
                    EncounterComponent encounterComponent = entity.GetComponent<EncounterComponent>();
                    if (encounterComponent != null)
                    {
                        encounterComponent.TemplateResRef = uteResRef;
                        encounterComponent.Active = ute.Active;
                        encounterComponent.Difficulty = ute.DifficultyId;
                        encounterComponent.DifficultyIndex = ute.DifficultyIndex;
                        encounterComponent.MaxCreatures = ute.MaxCreatures;
                        encounterComponent.RecCreatures = ute.RecCreatures;
                        encounterComponent.Reset = ute.Reset != 0;
                        encounterComponent.ResetTime = ute.ResetTime;
                        encounterComponent.SpawnOption = ute.SingleSpawn; // SpawnOption maps to SingleSpawn
                        encounterComponent.PlayerOnly = ute.PlayerOnly != 0;
                        encounterComponent.Faction = ute.Faction;

                        // Load creature templates
                        encounterComponent.CreatureTemplates = new List<EncounterCreature>();
                        foreach (UTECreature uteCreature in ute.Creatures)
                        {
                            var creature = new EncounterCreature
                            {
                                ResRef = uteCreature.ResRef.ToString(),
                                Appearance = uteCreature.Appearance,
                                CR = uteCreature.CR,
                                SingleSpawn = uteCreature.SingleSpawn != 0
                            };
                            encounterComponent.CreatureTemplates.Add(creature);
                        }

                        // Set script hooks
                        IScriptHooksComponent scriptsComponent = entity.GetComponent<IScriptHooksComponent>();
                        if (scriptsComponent != null)
                        {
                            if (!string.IsNullOrEmpty(ute.OnEntered.ToString()))
                            {
                                scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnEnter, ute.OnEntered.ToString());
                            }
                            if (!string.IsNullOrEmpty(ute.OnExit.ToString()))
                            {
                                scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnExit, ute.OnExit.ToString());
                            }
                            if (!string.IsNullOrEmpty(ute.OnHeartbeat.ToString()))
                            {
                                scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnHeartbeat, ute.OnHeartbeat.ToString());
                            }
                        }
                    }

                    // Set entity tag
                    if (!string.IsNullOrEmpty(ute.Tag))
                    {
                        entity.Tag = ute.Tag;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load UTE template " + uteResRef + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Loads UTS sound template and applies properties to entity.
        /// </summary>
        private void LoadSoundTemplate(IEntity entity, string utsResRef)
        {
            try
            {
                InstResourceResult result = _installation.Resource(utsResRef, ResourceType.UTS, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    var gff = GFF.FromBytes(result.Data);
                    UTS uts = UTSHelpers.ConstructUts(gff);

                    // Apply UTS properties to sound component
                    SoundComponent soundComponent = entity.GetComponent<SoundComponent>();
                    if (soundComponent != null)
                    {
                        soundComponent.Active = uts.Active;
                        soundComponent.Continuous = uts.Continuous;
                        soundComponent.Looping = uts.Looping;
                        soundComponent.Positional = uts.Positional;
                        soundComponent.RandomPosition = uts.RandomPosition;
                        soundComponent.Random = uts.Random;
                        soundComponent.Volume = uts.Volume;
                        soundComponent.VolumeVrtn = uts.VolumeVariance;
                        soundComponent.PitchVariation = uts.PitchVariance;
                        soundComponent.MinDistance = uts.MinDistance;
                        soundComponent.MaxDistance = uts.MaxDistance;
                        soundComponent.Interval = (uint)uts.Interval;
                        soundComponent.IntervalVrtn = (uint)uts.IntervalVariance;
                        soundComponent.Hours = (uint)uts.Hours;
                        soundComponent.TemplateResRef = utsResRef;
                        soundComponent.SoundFiles = new List<string>();
                        if (!string.IsNullOrEmpty(uts.Sound.ToString()))
                        {
                            soundComponent.SoundFiles.Add(uts.Sound.ToString());
                        }
                        foreach (ResRef soundRef in uts.Sounds)
                        {
                            if (!string.IsNullOrEmpty(soundRef.ToString()))
                            {
                                soundComponent.SoundFiles.Add(soundRef.ToString());
                            }
                        }
                    }

                    // Set entity tag
                    if (!string.IsNullOrEmpty(uts.Tag))
                    {
                        entity.Tag = uts.Tag;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load UTS template " + utsResRef + ": " + ex.Message);
            }
        }

        private void LoadDoorTemplate(IEntity entity, string resRef)
        {
            try
            {
                InstResourceResult result = _installation.Resource(resRef, ResourceType.UTD, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    GFF gff = GFF.FromBytes(result.Data);
                    UTD utd = UTDHelpers.ConstructUtd(gff);

                    entity.Tag = utd.Tag;

                    // Set door component properties
                    IDoorComponent doorComponent = entity.GetComponent<IDoorComponent>();
                    if (doorComponent != null)
                    {
                        doorComponent.IsLocked = utd.Locked;
                        doorComponent.IsOpen = utd.OpenState > 0;
                        doorComponent.KeyRequired = utd.KeyRequired;
                        doorComponent.KeyTag = utd.KeyName;
                    }

                    // Set scripts
                    IScriptHooksComponent scriptsComponent = entity.GetComponent<IScriptHooksComponent>();
                    if (scriptsComponent != null)
                    {
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnOpen, utd.OnOpen?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnClose, utd.OnClosed?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnFailToOpen, utd.OnOpenFailed?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnHeartbeat, utd.OnHeartbeat?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnClick, utd.OnClick?.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load UTD " + resRef + ": " + ex.Message);
            }
        }

        private void LoadPlaceableTemplate(IEntity entity, string resRef)
        {
            try
            {
                InstResourceResult result = _installation.Resource(resRef, ResourceType.UTP, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    GFF gff = GFF.FromBytes(result.Data);
                    UTP utp = UTPHelpers.ConstructUtp(gff);

                    entity.Tag = utp.Tag;

                    // Set scripts
                    IScriptHooksComponent scriptsComponent = entity.GetComponent<IScriptHooksComponent>();
                    if (scriptsComponent != null)
                    {
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnUsed, utp.OnUsed?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnHeartbeat, utp.OnHeartbeat?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnOpen, utp.OnOpen?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnClose, utp.OnClosed?.ToString());
                    }

                    // Store appearance for visual creation
                    IPlaceableComponent placeableComponent = entity.GetComponent<IPlaceableComponent>();
                    if (placeableComponent != null)
                    {
                        placeableComponent.IsUseable = utp.Useable;
                        placeableComponent.IsStatic = utp.Static;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load UTP " + resRef + ": " + ex.Message);
            }
        }

        private void LoadCreatureTemplate(IEntity entity, string resRef)
        {
            try
            {
                InstResourceResult result = _installation.Resource(resRef, ResourceType.UTC, null, _currentModuleRoot);
                if (result != null && result.Data != null)
                {
                    GFF gff = GFF.FromBytes(result.Data);
                    UTC utc = UTCHelpers.ConstructUtc(gff);

                    entity.Tag = utc.Tag;

                    // Set stats
                    IStatsComponent statsComponent = entity.GetComponent<IStatsComponent>();
                    if (statsComponent != null)
                    {
                        statsComponent.CurrentHP = utc.CurrentHp;
                        statsComponent.MaxHP = utc.MaxHp;
                        statsComponent.CurrentFP = utc.Fp;
                        statsComponent.MaxFP = utc.MaxFp;
                    }

                    // Set scripts
                    IScriptHooksComponent scriptsComponent = entity.GetComponent<IScriptHooksComponent>();
                    if (scriptsComponent != null)
                    {
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnSpawn, utc.OnSpawn?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnDeath, utc.OnDeath?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnHeartbeat, utc.OnHeartbeat?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnConversation, utc.OnDialog?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnDamaged, utc.OnDamaged?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnPerception, utc.OnNotice?.ToString());
                        scriptsComponent.SetLocalString("Conversation", utc.Conversation?.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load UTC " + resRef + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Loads a model by resref.
        /// </summary>
        [CanBeNull]
        public MDLData.MDL LoadModel(string resRef)
        {
            if (string.IsNullOrEmpty(resRef) || _installation == null)
            {
                return null;
            }

            try
            {
                // Load MDL
                InstResourceResult mdlResult = _installation.Resource(resRef, ResourceType.MDL, null, _currentModuleRoot);
                if (mdlResult == null || mdlResult.Data == null)
                {
                    return null;
                }

                // Load MDX
                InstResourceResult mdxResult = _installation.Resource(resRef, ResourceType.MDX, null, _currentModuleRoot);
                byte[] mdxData = mdxResult?.Data;

                return MDLAuto.ReadMdl(mdlResult.Data, sourceExt: mdxData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load model " + resRef + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Loads a texture by resref.
        /// </summary>
        [CanBeNull]
        public TPC LoadTexture(string resRef)
        {
            if (string.IsNullOrEmpty(resRef) || _installation == null)
            {
                return null;
            }

            return _installation.Texture(resRef);
        }

        /// <summary>
        /// Gets the entry position from the current module's IFO.
        /// </summary>
        public SysVector3 GetEntryPosition()
        {
            if (_currentIfo != null)
            {
                return new SysVector3(_currentIfo.EntryX, _currentIfo.EntryY, _currentIfo.EntryZ);
            }
            return SysVector3.Zero;
        }

        /// <summary>
        /// Gets the entry facing from the current module's IFO.
        /// </summary>
        public float GetEntryFacing()
        {
            if (_currentIfo != null)
            {
                return _currentIfo.EntryDirection;
            }
            return 0;
        }

        private void CreatePlaceholderModule(string moduleName)
        {
            var runtimeModule = new RuntimeModule();
            runtimeModule.ResRef = moduleName;
            runtimeModule.DisplayName = "Module: " + moduleName;
            runtimeModule.EntryArea = moduleName;
            runtimeModule.DawnHour = 6;
            runtimeModule.DuskHour = 18;
            _world.CurrentModule = runtimeModule;

            var runtimeArea = new RuntimeArea();
            runtimeArea.ResRef = moduleName;
            runtimeArea.DisplayName = "Area: " + moduleName;
            runtimeArea.Tag = moduleName;
            _world.CurrentArea = runtimeArea;
            _currentArea = runtimeArea;

            // Create placeholder waypoint
            IEntity playerSpawn = _world.CreateEntity(OdyObjectType.Waypoint, SysVector3.Zero, 0);
            playerSpawn.Tag = "wp_player_spawn";
            runtimeArea.AddEntity(playerSpawn);

            // Create placeholder navmesh
            CreatePlaceholderNavMesh();
        }
    }
}

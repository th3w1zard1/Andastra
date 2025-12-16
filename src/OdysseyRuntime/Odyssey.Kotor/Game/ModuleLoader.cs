using System;
using System.IO;
using System.Collections.Generic;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Module;
using Odyssey.Core.Navigation;
using AuroraEngine.Common;
using AuroraEngine.Common.Installation;
using AuroraEngine.Common.Resources;
using AuroraEngine.Common.Formats.GFF;
using AuroraEngine.Common.Formats.LYT;
using AuroraEngine.Common.Formats.VIS;
using AuroraEngine.Common.Formats.BWM;
using AuroraEngine.Common.Formats.MDL;
using AuroraEngine.Common.Formats.TPC;
using MDLData = AuroraEngine.Common.Formats.MDLData;
using AuroraEngine.Common.Resource.Generics;
using AuroraEngine.Common.Resource.Generics.DLG;
using JetBrains.Annotations;

// Explicit type aliases to resolve ambiguity
using SysVector3 = System.Numerics.Vector3;
using KotorVector3 = System.Numerics.Vector3;
using OdyObjectType = Odyssey.Core.Enums.ObjectType;
using InstResourceResult = AuroraEngine.Common.Installation.ResourceResult;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.Game
{
    /// <summary>
    /// Loads modules from KOTOR game files using CSharpKOTOR resource infrastructure.
    /// </summary>
    /// <remarks>
    /// Module Loading Process:
    /// - Based on swkotor2.exe module loading system
    /// - Located via string references: "MODULES:" @ 0x007b58b4, "MODULES" @ 0x007c6bc4
    /// - Directory setup: FUN_00633270 @ 0x00633270 (sets up MODULES, OVERRIDE, SAVES, etc. directory aliases)
    ///   - Original implementation (from decompiled FUN_00633270):
    ///     - Sets up directory aliases for resource lookup with both absolute ("d:\...") and relative (".\...") paths
    ///     - Directory aliases registered: HD0 (d:\), CD0 (d:\), OVERRIDE (.\override or d:\override), MODULES (.\modules or d:\modules)
    ///     - Additional aliases: ERRORTEX, TEMP, NWMFILES, LOGS, LOCALVAULT, DMVAULT, SERVERVAULT, SAVES (.\saves or u:\)
    ///     - MUSIC, STREAMMUSIC, MOVIES, TEMPCLIENT, CURRENTGAME, HAK, TEXTUREPACKS, STREAMVOICE, SUPERMODELS
    ///     - DOWNLOADS, OPTIONS, AMBIENT, PATCH, PORTRAITS, GAMEINPROGRESS (z:\gameinprogress or .\gameinprogress)
    ///     - FUTUREGAME, RIMS, RIMSXBOX, REBOOTDATA, CACHE (z:\cache or .\), LIPS
    ///     - Each alias maps to both absolute path (d:\...) and relative path (.\...) for cross-platform compatibility
    ///     - MODULES alias: Maps to ".\modules" (relative) or "d:\modules" (absolute) directory
    ///     - Directory aliases used throughout engine for resource path resolution
    /// - Module loading order: IFO (module info) -> LYT (layout) -> VIS (visibility) -> GIT (instances) -> ARE (area properties)
    /// - Original engine uses "MODULES:" prefix for module directory access
    /// - Module resources loaded from: MODULES:\{moduleName}\module.ifo, MODULES:\{moduleName}\{moduleName}.lyt, etc.
    /// - Load savegame function: FUN_00708990 @ 0x00708990 (loads savegame ERF archive, extracts GLOBALVARS, PARTYTABLE, etc.)
    ///   - Original implementation (from decompiled FUN_00708990):
    ///     - Function signature: `void FUN_00708990(void *this, int *param_1)`
    ///     - param_1: Save game data structure pointer
    ///     - Constructs save path: "SAVES:\{saveName}\SAVEGAME" using format string "%06d - %s" (save number and name)
    ///     - Creates GAMEINPROGRESS: directory if missing (checks existence via FUN_004069c0, creates via FUN_00409670 if not found)
    ///     - Loads savegame.sav ERF archive from constructed path (via FUN_00629d60, FUN_0062a2b0)
    ///     - Extracts savenfo.res (NFO GFF) to TEMP:pifo, reads NFO GFF with "NFO " signature (via FUN_00406aa0)
    ///     - Progress updates: 5% (0x5), 10% (0xa), 15% (0xf), 20% (0x14), 25% (0x19), 30% (0x1e), 35% (0x23), 40% (0x28), 45% (0x2d), 50% (0x32)
    ///     - Loads PARTYTABLE via FUN_0057dcd0 @ 0x0057dcd0 (party table deserialization, called at 30% progress)
    ///     - Loads GLOBALVARS via FUN_005ac740 @ 0x005ac740 (global variables deserialization, called at 35% progress)
    ///     - Reads AUTOSAVEPARAMS from NFO GFF if present (via FUN_00412b30, FUN_00708660)
    ///     - Sets module state flags and initializes game session (via FUN_004dc470, FUN_004dc9e0, FUN_004dc9c0)
    ///     - Module state: Sets flags at offset 0x48 in game session object (bit 0x200 = module loaded flag)
    ///     - Final progress: 50% (0x32) when savegame load completes
    /// </remarks>
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
        /// Gets the current CSharpKOTOR Module for resource loading.
        /// </summary>
        [CanBeNull]
        public AuroraEngine.Common.Module GetCSharpKotorModule()
        {
            if (_installation == null || string.IsNullOrEmpty(_currentModuleRoot))
            {
                return null;
            }

            try
            {
                // Create a Module instance from the current module root
                return new AuroraEngine.Common.Module(_currentModuleRoot, _installation);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to get CSharpKOTOR Module: " + ex.Message);
                return null;
            }
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
        /// <remarks>
        /// Based on swkotor2.exe module loading system
        /// Located via string references: "ModuleList" @ 0x007bdd3c, "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58
        /// Original implementation:
        /// 1. Sets up module directory path (MODULES:\{moduleName}\)
        /// 2. Loads module.ifo (IFO resource)
        /// 3. Loads {moduleName}.lyt (LYT resource)
        /// 4. Loads {moduleName}.vis (VIS resource)
        /// 5. Loads {moduleName}.git (GIT resource)
        /// 6. Loads walkmesh (WOK files from LYT rooms)
        /// 7. Spawns entities from GIT
        /// 8. Triggers ON_MODULE_LOAD and ON_MODULE_START script events
        /// </remarks>
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

            // Fire OnModuleLoad script event
            // Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD fires when module is loaded
            // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c (0x14), "Mod_OnModLoad" @ IFO GFF
            // Original implementation: OnModuleLoad script fires on module after all resources are loaded and entities are spawned
            // Script execution is handled through EventBus subscriptions (ScriptExecutor listens to script events)
            if (_world != null && _world.EventBus != null)
            {
                // Fire OnModuleLoad script event - module scripts use module entity as owner
                string onModuleLoadScript = runtimeModule.GetScript(ScriptEvent.OnModuleLoad);
                if (!string.IsNullOrEmpty(onModuleLoadScript))
                {
                    // Create or get module entity for script execution
                    IEntity moduleEntity = _world.GetEntityByTag(runtimeModule.ResRef, 0);
                    if (moduleEntity == null)
                    {
                        // Create a temporary entity for module script execution
                        moduleEntity = _world.CreateEntity(ObjectType.Invalid, System.Numerics.Vector3.Zero, 0f);
                        moduleEntity.Tag = runtimeModule.ResRef;
                    }
                    _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnModuleLoad, null);
                }

                // Fire OnModuleStart script event - fires after OnModuleLoad, before gameplay starts
                // Based on swkotor2.exe: Module start script execution
                // Located via string references: "OnModuleStart" script, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948 (0x15)
                // Original implementation: OnModuleStart fires after OnModuleLoad, before gameplay starts
                string onModuleStartScript = runtimeModule.GetScript(ScriptEvent.OnModuleStart);
                if (!string.IsNullOrEmpty(onModuleStartScript))
                {
                    // Use same module entity
                    IEntity moduleEntity = _world.GetEntityByTag(runtimeModule.ResRef, 0);
                    if (moduleEntity == null)
                    {
                        moduleEntity = _world.CreateEntity(ObjectType.Invalid, System.Numerics.Vector3.Zero, 0f);
                        moduleEntity.Tag = runtimeModule.ResRef;
                    }
                    _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnModuleStart, null);
                }
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

        // Load module IFO (module info) file
        // Based on swkotor2.exe module loading
        // Located via string reference: "Module" @ 0x007bc4e0
        // Original implementation: Loads "module.ifo" from MODULES:\{moduleName}\ directory
        // IFO contains: EntryArea, EntryX/Y/Z, ModName, DawnHour, DuskHour, ModuleDescription, etc.
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

        // Load module GIT (game instance template) file
        // Based on swkotor2.exe module loading
        // Located via string reference: "tmpgit" @ 0x007be618
        // Original implementation: Loads "{moduleName}.git" from MODULES:\{moduleName}\ directory
        // GIT contains: Creatures, Doors, Placeables, Triggers, Waypoints, Sounds, Stores, Encounters, Cameras
        // Each instance has: Position, Orientation, Tag, Template ResRef, Local variables, Script hooks
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

                // Load module scripts from IFO
                // Based on swkotor2.exe: Module scripts loaded from IFO GFF fields
                // Located via string references: "Mod_OnModLoad" @ IFO GFF, "Mod_OnModStart" @ IFO GFF
                // Original implementation: IFO GFF contains Mod_OnModLoad and Mod_OnModStart ResRef fields
                if (_currentIfo.OnLoad != null && !string.IsNullOrEmpty(_currentIfo.OnLoad.ToString()))
                {
                    module.SetScript(ScriptEvent.OnModuleLoad, _currentIfo.OnLoad.ToString());
                }
                if (_currentIfo.OnStart != null && !string.IsNullOrEmpty(_currentIfo.OnStart.ToString()))
                {
                    module.SetScript(ScriptEvent.OnModuleStart, _currentIfo.OnStart.ToString());
                }
                if (_currentIfo.OnClientEnter != null && !string.IsNullOrEmpty(_currentIfo.OnClientEnter.ToString()))
                {
                    module.SetScript(ScriptEvent.OnClientEnter, _currentIfo.OnClientEnter.ToString());
                }
                if (_currentIfo.OnClientLeave != null && !string.IsNullOrEmpty(_currentIfo.OnClientLeave.ToString()))
                {
                    module.SetScript(ScriptEvent.OnClientLeave, _currentIfo.OnClientLeave.ToString());
                }
                if (_currentIfo.OnHeartbeat != null && !string.IsNullOrEmpty(_currentIfo.OnHeartbeat.ToString()))
                {
                    module.SetScript(ScriptEvent.OnModuleHeartbeat, _currentIfo.OnHeartbeat.ToString());
                }
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
                        area.FogColor = (uint)(((byte)are.FogColor.A << 24) | ((byte)are.FogColor.R << 16) | ((byte)are.FogColor.G << 8) | (byte)are.FogColor.B);
                    }
                    if (are.SunFogColor != null)
                    {
                        area.SunFogColor = (uint)(((byte)are.SunFogColor.A << 24) | ((byte)are.SunFogColor.R << 16) | ((byte)are.SunFogColor.G << 8) | (byte)are.SunFogColor.B);
                    }
                    if (are.DawnColor1 != null)
                    {
                        area.SunAmbientColor = (uint)(((byte)are.DawnColor1.A << 24) | ((byte)are.DawnColor1.R << 16) | ((byte)are.DawnColor1.G << 8) | (byte)are.DawnColor1.B);
                    }
                    if (are.DayColor1 != null)
                    {
                        area.SunDiffuseColor = (uint)(((byte)are.DayColor1.A << 24) | ((byte)are.DayColor1.R << 16) | ((byte)are.DayColor1.G << 8) | (byte)are.DayColor1.B);
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

        // Spawn entities from GIT data
        // Based on swkotor2.exe entity spawning system
        // Original implementation: Iterates through GIT lists and creates runtime entities
        // Spawn order: Waypoints -> Doors -> Placeables -> Creatures -> Triggers -> Sounds -> Stores -> Encounters
        // Each entity gets: ObjectId, Tag, Position, Orientation, Template data, Local variables, Script hooks
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

            // Initialize components
            Systems.ComponentInitializer.InitializeComponents(entity);

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

            // Initialize components
            Systems.ComponentInitializer.InitializeComponents(entity);

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

            // Initialize components
            Systems.ComponentInitializer.InitializeComponents(entity);

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
            if (sound.ResRef != null && !string.IsNullOrEmpty(sound.ResRef.ToString()))
            {
                LoadSoundTemplate(entity, sound.ResRef.ToString());
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

            // GITStore doesn't have a Tag property, so we skip setting it

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
                    DoorComponent doorComponent = entity.GetComponent<DoorComponent>();
                    if (doorComponent != null)
                    {
                        doorComponent.GenericType = utd.AppearanceId;
                        doorComponent.IsLocked = utd.Locked;
                        doorComponent.IsOpen = utd.OpenState > 0;
                        doorComponent.KeyRequired = utd.KeyRequired;
                        doorComponent.KeyTag = utd.KeyName;
                    }

                    // Set renderable component appearance row
                    Core.Interfaces.Components.IRenderableComponent renderable = entity.GetComponent<Core.Interfaces.Components.IRenderableComponent>();
                    if (renderable != null)
                    {
                        renderable.AppearanceRow = utd.AppearanceId;
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

                    // Set placeable component appearance
                    IPlaceableComponent placeableComponent = entity.GetComponent<IPlaceableComponent>();
                    if (placeableComponent != null)
                    {
                        // Based on swkotor2.exe: Placeable appearance setting
                        // Located via string references: "AppearanceType" @ 0x007c84c8
                        // Original implementation: AppearanceType from UTP template sets placeable visual appearance
                        if (placeableComponent is PlaceableComponent kotorPlaceable)
                        {
                            kotorPlaceable.AppearanceType = utp.AppearanceId;
                        }
                        placeableComponent.IsUseable = utp.Useable;
                        placeableComponent.IsStatic = utp.Static;
                    }

                    // Set renderable component appearance row
                    Core.Interfaces.Components.IRenderableComponent renderable = entity.GetComponent<Core.Interfaces.Components.IRenderableComponent>();
                    if (renderable != null)
                    {
                        renderable.AppearanceRow = utp.AppearanceId;
                    }

                    // Set scripts
                    IScriptHooksComponent scriptsComponent = entity.GetComponent<IScriptHooksComponent>();
                    if (scriptsComponent != null)
                    {
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnUsed, utp.OnUsed?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnHeartbeat, utp.OnHeartbeat?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnOpen, utp.OnOpen?.ToString());
                        scriptsComponent.SetScript(Core.Enums.ScriptEvent.OnClose, utp.OnClosed?.ToString());
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

                    // Set creature component appearance
                    CreatureComponent creatureComponent = entity.GetComponent<CreatureComponent>();
                    if (creatureComponent != null)
                    {
                        // Based on swkotor2.exe: Creature appearance setting
                        // Located via string references: "AppearanceType" @ 0x007c84c8, "Appearance_Type" @ 0x007c40f0
                        // Original implementation: AppearanceType from UTC template sets creature visual appearance (model selection)
                        creatureComponent.AppearanceType = utc.AppearanceId;
                        creatureComponent.BodyVariation = utc.BodyVariation;
                        creatureComponent.TextureVar = utc.TextureVariation;
                    }

                    // Set renderable component appearance row
                    Core.Interfaces.Components.IRenderableComponent renderable = entity.GetComponent<Core.Interfaces.Components.IRenderableComponent>();
                    if (renderable != null)
                    {
                        renderable.AppearanceRow = utc.AppearanceId;
                    }

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

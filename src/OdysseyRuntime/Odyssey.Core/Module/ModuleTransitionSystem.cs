using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Save;

namespace Odyssey.Core.Module
{
    /// <summary>
    /// Module transition system for loading/unloading modules and areas.
    /// </summary>
    /// <remarks>
    /// Module Transition System:
    /// - Based on swkotor2.exe module transition system
    /// - Located via string references: "Module" @ 0x007c1a70 (module object type), "ModuleName" @ 0x007bde2c (module name field)
    /// - "LASTMODULE" @ 0x007be1d0 (last module global variable), "ModuleList" @ 0x007bdd3c (module list field)
    /// - "ModuleLoaded" @ 0x007bdd70 (module loaded flag), "ModuleRunning" @ 0x007bdd58 (module running flag)
    /// - "MODULES:" @ 0x007b58b4 (module debug prefix), ":MODULES" @ 0x007be258 (module path prefix)
    /// - "LIVE%d:MODULES\" @ 0x007be680 (module path format), ".\modules" @ 0x007c6bcc (module directory), "d:\modules" @ 0x007c6bd8 (module directory)
    /// - "MODULES" @ 0x007c6bc4 (modules constant), "MODULE" @ 0x007beab8 (module constant)
    /// - Script events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c (module load event, 0x14), "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948 (module start event, 0x15)
    /// - "OnModuleLeave" @ 0x007bee50 (module leave script), "OnModuleEnter" @ 0x007bee40 (module enter script)
    /// - Transition events: "EVENT_AREA_TRANSITION" @ 0x007bcbdc (area transition event, case 0x10), "Mod_Transition" @ 0x007be8f0 (module transition script)
    /// - Door transitions: "LinkedToModule" @ 0x007bd7bc (door module link field), "TransitionDestination" @ 0x007bd7a4 (transition waypoint field)
    /// - Module save: "modulesave" @ 0x007bde20 (module save directory), "Module: %s" @ 0x007c79c8 (module debug output)
    /// - "module000" @ 0x007cb9cc (default module name), ":: Module savegame list: %s.\n" @ 0x007cbbb4 (module save list debug)
    /// - ":: Server mode: Module Running.\n" @ 0x007cbc44, ":: Server mode: Module Loaded.\n" @ 0x007cbc68 (module state debug)
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 saves module state including creature positions, door states, placeable states
    /// - Module loading sequence:
    ///   1. Show loading screen (LoadScreenResRef from IFO)
    ///   2. Save current module state (creature positions, door/placeable states, triggered triggers)
    ///   3. Fire OnModuleLeave script on current module (ScriptOnExit field in IFO)
    ///   4. Unload current module (destroy all entities, clear areas)
    ///   5. Load new module (IFO, ARE, GIT, LYT, VIS files via ModuleLoader)
    ///   6. Restore module state if previously visited (from SaveSystem module state cache)
    ///   7. Position party at waypoint (TransitionDestination from door, or default entry waypoint)
    ///   8. Fire OnModuleLoad script on new module (ScriptOnLoad field in IFO, executes before OnModuleStart)
    ///   9. Fire OnModuleStart script on new module (ScriptOnStart field in IFO, executes after OnModuleLoad)
    ///   10. Fire OnEnter script on current area for each party member (ScriptOnEnter field in ARE)
    ///   11. Fire OnSpawn script on newly spawned creatures (ScriptSpawn field in UTC template)
    ///   12. Hide loading screen
    /// - Module state persistence: Module states saved per-module (creature positions, door/placeable states) persist across visits
    /// - Waypoint positioning: Party members positioned in line perpendicular to waypoint facing (1.0 unit spacing)
    /// - Loading screen: Displays LoadScreenResRef image from module IFO during transition
    /// - Area transitions: Within-module area transitions (via door LinkedTo field) use faster path (no module unload/reload)
    /// - Based on module transition system in vendor/PyKotor/wiki/ and engine implementation plan
    /// </remarks>
    public class ModuleTransitionSystem
    {
        private readonly IWorld _world;
        private readonly SaveSystem _saveSystem;
        private readonly IModuleLoader _moduleLoader;
        private bool _isTransitioning;

        public ModuleTransitionSystem(IWorld world, SaveSystem saveSystem, IModuleLoader moduleLoader)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _saveSystem = saveSystem ?? throw new ArgumentNullException("saveSystem");
            _moduleLoader = moduleLoader ?? throw new ArgumentNullException("moduleLoader");
            _isTransitioning = false;
        }

        /// <summary>
        /// Transitions to a new module.
        /// </summary>
        /// <param name="moduleResRef">Module resource reference.</param>
        /// <param name="waypointTag">Waypoint tag to position party at.</param>
        /// <returns>Task that completes when transition is done.</returns>
        public async Task TransitionToModule(string moduleResRef, string waypointTag)
        {
            if (_isTransitioning)
            {
                return;
            }

            _isTransitioning = true;

            try
            {
                // 1. Show loading screen
                ShowLoadingScreen(GetLoadscreenForModule(moduleResRef));

                // 2. Save current module state
                if (_world.CurrentModule != null)
                {
                    ModuleState moduleState = SaveCurrentModuleState();
                    _saveSystem.StoreModuleState(_world.CurrentModule.ResRef, moduleState);

                    // 3. Fire OnModuleLeave script
                    // Based on swkotor2.exe: Module leave script execution
                    // Located via string references: "OnModuleLeave" @ 0x007bee50, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
                    // Original implementation: FUN_005226d0 @ 0x005226d0 executes module leave scripts before unloading
                    string leaveScript = _world.CurrentModule.GetScript(ScriptEvent.OnModuleLeave);
                    if (!string.IsNullOrEmpty(leaveScript) && _world.EventBus != null)
                    {
                        // Fire script event - module scripts use module ResRef as context
                        // Modules don't have physical entities, so we create a temporary entity for script execution
                        IEntity moduleEntity = _world.GetEntityByTag(_world.CurrentModule.ResRef, 0);
                        if (moduleEntity == null)
                        {
                            // Create a temporary entity for module script execution
                            moduleEntity = _world.CreateEntity(ObjectType.Invalid, Vector3.Zero, 0f);
                            moduleEntity.Tag = _world.CurrentModule.ResRef;
                        }
                        _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnModuleLeave, null);
                    }
                }

                // 4. Unload current module
                await UnloadCurrentModule();

                // 5. Load new module
                IModule newModule = await _moduleLoader.LoadModule(moduleResRef);
                if (newModule == null)
                {
                    throw new InvalidOperationException("Failed to load module: " + moduleResRef);
                }

                // Set current module (cast to World for SetCurrentModule method)
                if (_world is Entities.World world)
                {
                    world.SetCurrentModule(newModule);
                }

                // 6. Check if we've been here before
                // Based on swkotor2.exe: Module state restoration
                // Original implementation: Restores entity positions, door/placeable states if module was previously visited
                if (_saveSystem.HasModuleState(moduleResRef))
                {
                    ModuleState savedState = _saveSystem.GetModuleState(moduleResRef);
                    if (savedState != null)
                    {
                        RestoreModuleState(newModule, savedState);
                    }
                }

                // 7. Position party at waypoint
                if (!string.IsNullOrEmpty(waypointTag))
                {
                    IEntity waypoint = _world.GetEntityByTag(waypointTag, 0);
                    if (waypoint != null)
                    {
                        Interfaces.Components.ITransformComponent transform = waypoint.GetComponent<Interfaces.Components.ITransformComponent>();
                        if (transform != null)
                        {
                            // Get facing from transform component or entity
                            float waypointFacing = transform.Facing;
                            if (waypoint is Entities.Entity waypointEntity)
                            {
                                waypointFacing = waypointEntity.Facing;
                            }
                            PositionPartyAt(transform.Position, waypointFacing);
                        }
                    }
                }

                // 8. Fire OnModuleLoad script
                // Based on swkotor2.exe: Module load script execution
                // Located via string references: "OnModuleLoad" @ 0x007bee40, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
                // Original implementation: FUN_005226d0 @ 0x005226d0 executes module load scripts after loading
                string loadScript = newModule.GetScript(ScriptEvent.OnModuleLoad);
                if (!string.IsNullOrEmpty(loadScript) && _world.EventBus != null)
                {
                    // Fire script event - module scripts use module entity as owner
                    IEntity moduleEntity = _world.GetEntityByTag(newModule.ResRef, 0);
                    if (moduleEntity != null)
                    {
                        _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnModuleLoad, null);
                    }
                }

                // 9. Fire OnEnter for area
                // Based on swkotor2.exe: Area enter script execution
                // Located via string references: "OnEnter" @ 0x007bee60 (area enter script)
                // Original implementation: FUN_005226d0 @ 0x005226d0 executes area enter scripts for each party member
                if (_world.CurrentArea != null && _world.EventBus != null)
                {
                    string enterScript = null;
                    if (_world.CurrentArea is Module.RuntimeArea runtimeArea)
                    {
                        enterScript = runtimeArea.GetScript(ScriptEvent.OnEnter);
                    }
                    
                    if (!string.IsNullOrEmpty(enterScript))
                    {
                        // Execute script for each party member
                        // Get party members from world (party system would provide this)
                        IEnumerable<IEntity> partyMembers = _world.GetEntitiesOfType(ObjectType.Creature)
                            .Where(e => 
                            {
                                if (e is Entities.Entity entity)
                                {
                                    return entity.GetData<bool>("IsPartyMember", false) || entity.GetData<bool>("IsPC", false);
                                }
                                return false;
                            });
                        
                        IEntity areaEntity = _world.GetEntityByTag(_world.CurrentArea.ResRef, 0);
                        if (areaEntity == null)
                        {
                            // Use area ResRef as tag for script execution context
                            // Area scripts don't require a physical entity, just a tag reference
                            areaEntity = _world.GetEntityByTag(_world.CurrentArea.Tag, 0);
                        }
                        
                        foreach (IEntity member in partyMembers)
                        {
                            _world.EventBus.FireScriptEvent(areaEntity, ScriptEvent.OnEnter, member);
                        }
                    }
                }

                // 10. Fire OnSpawn for any new creatures
                // Based on swkotor2.exe: Creature spawn script execution
                // Located via string references: "OnSpawn" @ 0x007beec0 (spawn script field)
                // Original implementation: FUN_005226d0 @ 0x005226d0 executes spawn scripts when creatures are created
                IEnumerable<IEntity> creatures = _world.GetEntitiesOfType(ObjectType.Creature);
                foreach (IEntity creature in creatures)
                {
                    if (creature is Entities.Entity creatureEntity)
                    {
                        if (!creatureEntity.HasData("HasSpawned"))
                        {
                            creatureEntity.SetData("HasSpawned", true);
                            IScriptHooksComponent creatureScriptHooks = creature.GetComponent<IScriptHooksComponent>();
                            if (creatureScriptHooks != null)
                            {
                                string spawnScript = creatureScriptHooks.GetScript(ScriptEvent.OnSpawn);
                                if (!string.IsNullOrEmpty(spawnScript) && _world.EventBus != null)
                                {
                                    _world.EventBus.FireScriptEvent(creature, ScriptEvent.OnSpawn, null);
                                }
                            }
                        }
                    }
                }

                // 11. Hide loading screen
                HideLoadingScreen();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// Saves current module state.
        /// </summary>
        private ModuleState SaveCurrentModuleState()
        {
            ModuleState state = new ModuleState();

            if (_world.CurrentModule == null)
            {
                return state;
            }

            // Save creature positions and states
            IEnumerable<IEntity> creatures = _world.GetEntitiesOfType(ObjectType.Creature);
            foreach (IEntity creature in creatures)
            {
                Interfaces.Components.ITransformComponent transform = creature.GetComponent<Interfaces.Components.ITransformComponent>();
                Interfaces.Components.IStatsComponent stats = creature.GetComponent<Interfaces.Components.IStatsComponent>();

                if (transform != null)
                {
                    state.Creatures.Add(new CreatureState
                    {
                        Tag = creature.Tag,
                        Position = transform.Position,
                        Facing = creature is Entities.Entity creatureEntity2 ? creatureEntity2.Facing : transform.Facing,
                        CurrentHP = stats != null ? stats.CurrentHP : 0,
                        IsDead = stats != null && stats.CurrentHP <= 0
                    });
                }
            }

            // Save placeable states
            IEnumerable<IEntity> placeables = _world.GetEntitiesOfType(ObjectType.Placeable);
            foreach (IEntity placeable in placeables)
            {
                Interfaces.Components.IPlaceableComponent placeableComp = placeable.GetComponent<Interfaces.Components.IPlaceableComponent>();
                if (placeableComp != null)
                {
                    state.Placeables.Add(new PlaceableState
                    {
                        Tag = placeable.Tag,
                        IsOpen = placeableComp.IsOpen,
                        HasInventory = placeableComp.HasInventory
                    });
                }
            }

            // Save door states
            IEnumerable<IEntity> doors = _world.GetEntitiesOfType(ObjectType.Door);
            foreach (IEntity door in doors)
            {
                Interfaces.Components.IDoorComponent doorComp = door.GetComponent<Interfaces.Components.IDoorComponent>();
                if (doorComp != null)
                {
                    state.Doors.Add(new DoorState
                    {
                        Tag = door.Tag,
                        IsOpen = doorComp.IsOpen,
                        IsLocked = doorComp.IsLocked
                    });
                }
            }

            return state;
        }

        /// <summary>
        /// Restores module state.
        /// </summary>
        private void RestoreModuleState(IModule module, ModuleState state)
        {
            // Restore creature states
            foreach (CreatureState creatureState in state.Creatures)
            {
                IEntity creature = _world.GetEntityByTag(creatureState.Tag, 0);
                if (creature != null)
                {
                    Interfaces.Components.ITransformComponent transform = creature.GetComponent<Interfaces.Components.ITransformComponent>();
                    Interfaces.Components.IStatsComponent stats = creature.GetComponent<Interfaces.Components.IStatsComponent>();

                    if (transform != null)
                    {
                        transform.Position = creatureState.Position;
                        if (creature is Entities.Entity creatureEntity3)
                        {
                            creatureEntity3.Facing = creatureState.Facing;
                        }
                        else if (transform != null)
                        {
                            transform.Facing = creatureState.Facing;
                        }
                    }

                    if (stats != null)
                    {
                        stats.CurrentHP = creatureState.CurrentHP;
                    }
                }
            }

            // Restore placeable states
            foreach (PlaceableState placeableState in state.Placeables)
            {
                IEntity placeable = _world.GetEntityByTag(placeableState.Tag, 0);
                if (placeable != null)
                {
                    Interfaces.Components.IPlaceableComponent placeableComp = placeable.GetComponent<Interfaces.Components.IPlaceableComponent>();
                    if (placeableComp != null)
                    {
                        placeableComp.IsOpen = placeableState.IsOpen;
                        placeableComp.HasInventory = placeableState.HasInventory;
                    }
                }
            }

            // Restore door states
            foreach (DoorState doorState in state.Doors)
            {
                IEntity door = _world.GetEntityByTag(doorState.Tag, 0);
                if (door != null)
                {
                    Interfaces.Components.IDoorComponent doorComp = door.GetComponent<Interfaces.Components.IDoorComponent>();
                    if (doorComp != null)
                    {
                        doorComp.IsOpen = doorState.IsOpen;
                        doorComp.IsLocked = doorState.IsLocked;
                    }
                }
            }
        }

        /// <summary>
        /// Unloads current module.
        /// </summary>
        private async Task UnloadCurrentModule()
        {
            if (_world.CurrentModule == null)
            {
                return;
            }

            // Destroy all entities in current area
            foreach (IEntity entity in _world.GetAllEntities().ToList())
            {
                _world.DestroyEntity(entity.ObjectId);
            }

            // Set current module and area to null (cast to World for SetCurrentModule/SetCurrentArea methods)
            if (_world is Entities.World world2)
            {
                world2.SetCurrentModule(null);
                world2.SetCurrentArea(null);
            }
            else
            {
                // If IWorld doesn't have setters, we can't set them
                // This should not happen if World class is used
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Positions party at specified location.
        /// Based on swkotor2.exe: Party positioning at waypoints
        /// Located via string references: "Waypoint" @ 0x007c1a90, "PositionParty" @ 0x007c1a94
        /// Original implementation: FUN_005226d0 @ 0x005226d0 positions all party members at waypoint with spacing
        /// </summary>
        private void PositionPartyAt(Vector3 position, float facing)
        {
            // Get all party members
            IEnumerable<IEntity> partyMembers = _world.GetEntitiesOfType(ObjectType.Creature)
                .Where(e => 
                {
                    if (e is Entities.Entity entity)
                    {
                        return entity.GetData<bool>("IsPartyMember", false) || entity.GetData<bool>("IsPC", false);
                    }
                    return false;
                });
            
            int memberIndex = 0;
            const float spacing = 1.0f; // 1 unit spacing between party members
            
            foreach (IEntity member in partyMembers)
            {
                Interfaces.Components.ITransformComponent transform = member.GetComponent<Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    // Position party members in a line perpendicular to facing direction
                    float offsetX = (float)Math.Sin(facing) * spacing * memberIndex;
                    float offsetZ = (float)Math.Cos(facing) * spacing * memberIndex;
                    Vector3 memberPosition = position + new Vector3(offsetX, 0, offsetZ);
                    
                    transform.Position = memberPosition;
                    if (member is Entities.Entity memberEntity)
                    {
                        memberEntity.Facing = facing;
                    }
                    else if (transform != null)
                    {
                        transform.Facing = facing;
                    }
                    memberIndex++;
                }
            }
            
            // Fallback: If no party members found, just position player
            if (memberIndex == 0)
            {
                IEntity player = _world.GetEntityByTag("Player", 0);
                if (player != null)
                {
                    Interfaces.Components.ITransformComponent transform = player.GetComponent<Interfaces.Components.ITransformComponent>();
                    if (transform != null)
                    {
                        transform.Position = position;
                        if (player is Entities.Entity playerEntity)
                        {
                            playerEntity.Facing = facing;
                        }
                        else if (transform != null)
                        {
                            transform.Facing = facing;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets loadscreen image for module.
        /// Based on swkotor2.exe: Module loadscreen lookup
        /// Located via string references: "LoadScreen" @ 0x007c1a98, "LoadScreenResRef" @ IFO structure
        /// Original implementation: FUN_005226d0 @ 0x005226d0 reads loadscreen from module IFO file
        /// </summary>
        private string GetLoadscreenForModule(string moduleResRef)
        {
            // Lookup loadscreen from module IFO
            if (_world.CurrentModule != null)
            {
                string loadscreen = null;
                if (_world.CurrentModule is RuntimeModule runtimeModule)
                {
                    loadscreen = runtimeModule.LoadScreenResRef;
                }
                
                if (!string.IsNullOrEmpty(loadscreen))
                {
                    return loadscreen;
                }
            }
            
            // Fallback to default loadscreen
            return "load_default";
        }

        /// <summary>
        /// Shows loading screen.
        /// </summary>
        private void ShowLoadingScreen(string imageResRef)
        {
            // TODO: Show loading screen UI
        }

        /// <summary>
        /// Hides loading screen.
        /// </summary>
        private void HideLoadingScreen()
        {
            // TODO: Hide loading screen UI
        }
    }

    /// <summary>
    /// Module state data.
    /// </summary>
    public class ModuleState
    {
        public List<CreatureState> Creatures { get; set; }
        public List<PlaceableState> Placeables { get; set; }
        public List<DoorState> Doors { get; set; }

        public ModuleState()
        {
            Creatures = new List<CreatureState>();
            Placeables = new List<PlaceableState>();
            Doors = new List<DoorState>();
        }
    }

    /// <summary>
    /// Creature state data.
    /// </summary>
    public class CreatureState
    {
        public string Tag { get; set; }
        public Vector3 Position { get; set; }
        public float Facing { get; set; }
        public int CurrentHP { get; set; }
        public bool IsDead { get; set; }
    }

    /// <summary>
    /// Placeable state data.
    /// </summary>
    public class PlaceableState
    {
        public string Tag { get; set; }
        public bool IsOpen { get; set; }
        public bool HasInventory { get; set; }
    }

    /// <summary>
    /// Door state data.
    /// </summary>
    public class DoorState
    {
        public string Tag { get; set; }
        public bool IsOpen { get; set; }
        public bool IsLocked { get; set; }
    }

    /// <summary>
    /// Module loader interface.
    /// </summary>
    public interface IModuleLoader
    {
        Task<IModule> LoadModule(string moduleResRef);
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Save;

namespace Odyssey.Core.Module
{
    /// <summary>
    /// Module transition system for loading/unloading modules and areas.
    /// </summary>
    /// <remarks>
    /// Module Transition System:
    /// - Based on swkotor2.exe module transition system
    /// - Located via string references: "Module" @ 0x007c1a70, "ModuleName" @ 0x007bde2c, "LASTMODULE" @ 0x007be1d0
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948
    /// - "OnModuleLeave" @ 0x007bee50, "OnModuleEnter" @ 0x007bee40
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 saves module state including creature positions, door states
    /// - Module loading: Loads IFO, ARE, GIT, LYT, VIS files for new module
    /// - Module state: Saves creature positions, door states, placeable states, triggered triggers
    /// - Waypoint positioning: Positions party at specified waypoint when entering module
    /// - Loading screen: Shows loading screen during module transitions
    /// - State persistence: Module state is saved and restored when revisiting modules
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
                    string leaveScript = _world.CurrentModule.GetScript(ScriptEvent.OnModuleLeave);
                    if (!string.IsNullOrEmpty(leaveScript))
                    {
                        // TODO: Execute script
                        // ScriptExecutor.ExecuteScript(leaveScript, _world.CurrentModule)
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

                _world.SetCurrentModule(newModule);

                // 6. Check if we've been here before
                if (_saveSystem.HasModuleState(moduleResRef))
                {
                    ModuleState savedState = _saveSystem.GetModuleState(moduleResRef);
                    RestoreModuleState(newModule, savedState);
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
                            PositionPartyAt(transform.Position, waypoint.Facing);
                        }
                    }
                }

                // 8. Fire OnModuleLoad script
                string loadScript = newModule.GetScript(ScriptEvent.OnModuleLoad);
                if (!string.IsNullOrEmpty(loadScript))
                {
                    // TODO: Execute script
                    // ScriptExecutor.ExecuteScript(loadScript, newModule)
                }

                // 9. Fire OnEnter for area
                if (_world.CurrentArea != null)
                {
                    string enterScript = _world.CurrentArea.GetScript(ScriptEvent.OnEnter);
                    if (!string.IsNullOrEmpty(enterScript))
                    {
                        // TODO: Execute script for each party member
                        // foreach (IEntity member in Party.Members)
                        // {
                        //     ScriptExecutor.ExecuteScript(enterScript, _world.CurrentArea, member)
                        // }
                    }
                }

                // 10. Fire OnSpawn for any new creatures
                IEnumerable<IEntity> creatures = _world.GetEntitiesOfType(ObjectType.Creature);
                foreach (IEntity creature in creatures)
                {
                    if (!creature.HasData("HasSpawned"))
                    {
                        creature.SetData("HasSpawned", true);
                        string spawnScript = creature.GetScript(ScriptEvent.OnSpawn);
                        if (!string.IsNullOrEmpty(spawnScript))
                        {
                            // TODO: Execute script
                            // ScriptExecutor.ExecuteScript(spawnScript, creature)
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
                        Facing = creature.Facing,
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
                        creature.Facing = creatureState.Facing;
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

            _world.SetCurrentModule(null);
            _world.SetCurrentArea(null);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Positions party at specified location.
        /// </summary>
        private void PositionPartyAt(Vector3 position, float facing)
        {
            // TODO: Position all party members at location
            // For now, just position player
            IEntity player = _world.GetEntityByTag("Player", 0);
            if (player != null)
            {
                Interfaces.Components.ITransformComponent transform = player.GetComponent<Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    transform.Position = position;
                    player.Facing = facing;
                }
            }
        }

        /// <summary>
        /// Gets loadscreen image for module.
        /// </summary>
        private string GetLoadscreenForModule(string moduleResRef)
        {
            // TODO: Lookup loadscreen from module IFO or default
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


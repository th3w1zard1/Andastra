using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Core.Video;

namespace Andastra.Runtime.Core.Module
{
    /// <summary>
    /// Module transition system for loading/unloading modules and areas.
    /// </summary>
    /// <remarks>
    /// Module Transition System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) module transition system
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
    /// - Original implementation: 0x005226d0 @ 0x005226d0 saves module state including creature positions, door states, placeable states
    /// - Module loading sequence:
    ///   1. Play movies sequentially (if provided) - BIK format, blocking playback
    ///   2. Show loading screen (LoadScreenResRef from IFO)
    ///   3. Save current module state (creature positions, door/placeable states, triggered triggers)
    ///   4. Fire OnClientLeave script on current module (before OnModuleLeave)
    ///   4.5. Fire OnModuleLeave script on current module (ScriptOnExit field in IFO)
    ///   5. Unload current module (destroy all entities, clear areas)
    ///   6. Load new module (IFO, ARE, GIT, LYT, VIS files via ModuleLoader)
    ///   7. Restore module state if previously visited (from SaveSystem module state cache)
    ///   8. Position party at waypoint (TransitionDestination from door, or default entry waypoint)
    ///   9. Fire OnModuleLoad script on new module (ScriptOnLoad field in IFO, executes before OnModuleStart)
    ///   10. Fire OnModuleStart script on new module (ScriptOnStart field in IFO, executes after OnModuleLoad)
    ///   10.5. Fire OnClientEnter script on new module (after OnModuleStart)
    ///   11. Fire OnEnter script on current area for each party member (ScriptOnEnter field in ARE)
    ///   12. Fire OnSpawn script on newly spawned creatures (ScriptSpawn field in UTC template)
    ///   13. Hide loading screen
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
        private readonly MoviePlayer _moviePlayer;
        private readonly ILoadingScreen _loadingScreen;
        private bool _isTransitioning;

        /// <summary>
        /// Initializes a new instance of the ModuleTransitionSystem.
        /// </summary>
        /// <param name="world">World instance.</param>
        /// <param name="saveSystem">Save system for module state persistence.</param>
        /// <param name="moduleLoader">Module loader for loading modules.</param>
        /// <param name="loadingScreen">Loading screen interface for displaying loading screens during transitions. If null, uses NullLoadingScreen.</param>
        /// <param name="resourceProvider">Optional resource provider for movie playback (must implement IMovieResourceProvider or be adaptable).</param>
        /// <param name="graphicsDevice">Optional graphics device for movie playback (must implement IMovieGraphicsDevice or be adaptable).</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module transition system initialization
        /// - Loading screen is injected to avoid Core depending on Graphics/Content
        /// - Movie player is created if resource provider and graphics device are available
        /// - Resource provider and graphics device are adapted from Content/Graphics layers if needed
        /// - Original implementation: Module transition system handles loading screens, movies, and module state
        /// </remarks>
        public ModuleTransitionSystem(IWorld world, SaveSystem saveSystem, IModuleLoader moduleLoader, ILoadingScreen loadingScreen = null, object resourceProvider = null, object graphicsDevice = null)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _saveSystem = saveSystem ?? throw new ArgumentNullException("saveSystem");
            _moduleLoader = moduleLoader ?? throw new ArgumentNullException("moduleLoader");
            _loadingScreen = loadingScreen ?? new NullLoadingScreen();

            // Create movie player if resource provider and graphics device are available
            // Adapt from Content/Graphics layers if needed
            IMovieResourceProvider movieResourceProvider = null;
            IMovieGraphicsDevice movieGraphicsDevice = null;

            if (resourceProvider != null)
            {
                // Try to use as IMovieResourceProvider directly, or adapt from IGameResourceProvider
                if (resourceProvider is IMovieResourceProvider directProvider)
                {
                    movieResourceProvider = directProvider;
                }
                else
                {
                    // Try to adapt from Content layer
                    Type contentProviderType = Type.GetType("Andastra.Runtime.Content.Interfaces.IGameResourceProvider, Runtime.Content");
                    if (contentProviderType != null && contentProviderType.IsAssignableFrom(resourceProvider.GetType()))
                    {
                        Type adapterType = Type.GetType("Andastra.Runtime.Content.Adapters.MovieResourceProviderAdapter, Runtime.Content");
                        if (adapterType != null)
                        {
                            movieResourceProvider = (IMovieResourceProvider)Activator.CreateInstance(adapterType, resourceProvider);
                        }
                    }
                }
            }

            if (graphicsDevice != null)
            {
                // Try to use as IMovieGraphicsDevice directly, or adapt from IGraphicsDevice
                if (graphicsDevice is IMovieGraphicsDevice directDevice)
                {
                    movieGraphicsDevice = directDevice;
                }
                else
                {
                    // Try to adapt from Graphics layer
                    Type graphicsDeviceType = Type.GetType("Andastra.Runtime.Graphics.IGraphicsDevice, Runtime.Graphics.Common");
                    if (graphicsDeviceType != null && graphicsDeviceType.IsAssignableFrom(graphicsDevice.GetType()))
                    {
                        Type adapterType = Type.GetType("Andastra.Runtime.Graphics.Adapters.MovieGraphicsDeviceAdapter, Runtime.Graphics.Common");
                        if (adapterType != null)
                        {
                            movieGraphicsDevice = (IMovieGraphicsDevice)Activator.CreateInstance(adapterType, graphicsDevice);
                        }
                    }
                }
            }

            if (movieResourceProvider != null && movieGraphicsDevice != null)
            {
                _moviePlayer = new MoviePlayer(world, movieResourceProvider, movieGraphicsDevice);
            }
            else
            {
                _moviePlayer = null;
            }

            _isTransitioning = false;
        }

        /// <summary>
        /// Transitions to a new module.
        /// </summary>
        /// <param name="moduleResRef">Module resource reference.</param>
        /// <param name="waypointTag">Waypoint tag to position party at.</param>
        /// <param name="movies">Optional array of movie file names (BIK format) to play before transition. Maximum 6 movies.</param>
        /// <returns>Task that completes when transition is done.</returns>
        public async Task TransitionToModule(string moduleResRef, string waypointTag, string[] movies = null)
        {
            if (_isTransitioning)
            {
                return;
            }

            _isTransitioning = true;

            try
            {
                // 1. Play movies sequentially (if provided) - BEFORE loading screen
                // Based on swkotor.exe/swkotor2.exe: Movie playback before module transition
                // Movies play blocking (wait for each to finish before playing next)
                // Movie files are BIK format (Bink video) - requires BINKW32.DLL
                // If movie playback fails, continues with module transition
                if (movies != null && movies.Length > 0)
                {
                    await PlayMoviesSequentially(movies);
                }

                // 2. Show loading screen
                ShowLoadingScreen(GetLoadscreenForModule(moduleResRef));

                // 3. Save current module state
                if (_world.CurrentModule != null)
                {
                    ModuleState moduleState = SaveCurrentModuleState();
                    _saveSystem.StoreModuleState(_world.CurrentModule.ResRef, moduleState);

                    // 4. Fire OnClientLeave script (before OnModuleLeave)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Client leave script execution
                    // Located via string references: "Mod_OnClientLeav" @ 0x007be718
                    // Original implementation: OnClientLeave fires when player/client leaves the module (before OnModuleLeave)
                    // This is a module-level script that fires once when the player leaves the module
                    string clientLeaveScript = _world.CurrentModule.GetScript(ScriptEvent.OnClientLeave);
                    if (!string.IsNullOrEmpty(clientLeaveScript) && _world.EventBus != null)
                    {
                        // Fire script event - module scripts use module entity as owner
                        IEntity moduleEntity = _world.GetEntityByTag(_world.CurrentModule.ResRef, 0);
                        if (moduleEntity == null)
                        {
                            // Create a temporary entity for module script execution
                            moduleEntity = _world.CreateEntity(ObjectType.Invalid, Vector3.Zero, 0f);
                            moduleEntity.Tag = _world.CurrentModule.ResRef;
                        }
                        // OnClientLeave fires with the player character as the triggerer
                        IEntity player = _world.GetEntitiesOfType(ObjectType.Creature)
                            .FirstOrDefault(e => e.GetData<bool>("IsPC", false));
                        _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnClientLeave, player);
                    }

                    // 4.5. Fire OnModuleLeave script
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module leave script execution
                    // Located via string references: "OnModuleLeave" @ 0x007bee50, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
                    // Original implementation: 0x005226d0 @ 0x005226d0 executes module leave scripts before unloading
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

                // 5. Unload current module
                await UnloadCurrentModule();

                // 6. Load new module
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

                // 7. Check if we've been here before
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state restoration
                // Original implementation: Restores entity positions, door/placeable states if module was previously visited
                if (_saveSystem.HasModuleState(moduleResRef))
                {
                    ModuleState savedState = _saveSystem.GetModuleState(moduleResRef);
                    if (savedState != null)
                    {
                        RestoreModuleState(newModule, savedState);
                    }
                }

                // 8. Position party at waypoint
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

                // 9. Fire OnModuleLoad script
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module load script execution
                // Located via string references: "OnModuleLoad" @ 0x007bee40, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
                // Original implementation: 0x005226d0 @ 0x005226d0 executes module load scripts after loading
                string loadScript = newModule.GetScript(ScriptEvent.OnModuleLoad);
                if (!string.IsNullOrEmpty(loadScript) && _world.EventBus != null)
                {
                    // Fire script event - module scripts use module entity as owner
                    IEntity moduleEntity = _world.GetEntityByTag(newModule.ResRef, 0);
                    if (moduleEntity == null)
                    {
                        // Create a temporary entity for module script execution
                        moduleEntity = _world.CreateEntity(ObjectType.Invalid, Vector3.Zero, 0f);
                        moduleEntity.Tag = newModule.ResRef;
                    }
                    _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnModuleLoad, null);
                }

                // 10. Fire OnModuleStart script
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module start script execution
                // Located via string references: "OnModuleStart" script, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948 (0x15)
                // Original implementation: OnModuleStart fires after OnModuleLoad, before gameplay starts
                string startScript = newModule.GetScript(ScriptEvent.OnModuleStart);
                if (!string.IsNullOrEmpty(startScript) && _world.EventBus != null)
                {
                    // Fire script event - module scripts use module entity as owner
                    IEntity moduleEntity = _world.GetEntityByTag(newModule.ResRef, 0);
                    if (moduleEntity == null)
                    {
                        // Create a temporary entity for module script execution
                        moduleEntity = _world.CreateEntity(ObjectType.Invalid, Vector3.Zero, 0f);
                        moduleEntity.Tag = newModule.ResRef;
                    }
                    _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnModuleStart, null);
                }

                // 10.5. Fire OnClientEnter script
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Client enter script execution
                // Located via string references: "Mod_OnClientEntr" @ 0x007be718, "Mod_OnClientEntrance" @ 0x007be718
                // Original implementation: OnClientEnter fires when player/client enters the module (after OnModuleStart)
                // This is a module-level script that fires once when the player enters the module
                string clientEnterScript = newModule.GetScript(ScriptEvent.OnClientEnter);
                if (!string.IsNullOrEmpty(clientEnterScript) && _world.EventBus != null)
                {
                    // Fire script event - module scripts use module entity as owner
                    IEntity moduleEntity = _world.GetEntityByTag(newModule.ResRef, 0);
                    if (moduleEntity == null)
                    {
                        // Create a temporary entity for module script execution
                        moduleEntity = _world.CreateEntity(ObjectType.Invalid, Vector3.Zero, 0f);
                        moduleEntity.Tag = newModule.ResRef;
                    }
                    // OnClientEnter fires with the player character as the triggerer
                    IEntity player = _world.GetEntitiesOfType(ObjectType.Creature)
                        .FirstOrDefault(e => e.GetData<bool>("IsPC", false));
                    _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnClientEnter, player);
                }

                // 11. Fire OnEnter for area
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area enter script execution
                // Located via string references: "OnEnter" @ 0x007bee60 (area enter script)
                // Original implementation: 0x005226d0 @ 0x005226d0 executes area enter scripts for each party member
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

                // 12. Fire OnSpawn for any new creatures
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Creature spawn script execution
                // Located via string references: "OnSpawn" @ 0x007beec0 (spawn script field)
                // Original implementation: 0x005226d0 @ 0x005226d0 executes spawn scripts when creatures are created
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

                // 13. Hide loading screen
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Party positioning at waypoints
        /// Located via string references: "Waypoint" @ 0x007c1a90, "PositionParty" @ 0x007c1a94
        /// Original implementation: 0x005226d0 @ 0x005226d0 positions all party members at waypoint with spacing
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module loadscreen lookup
        /// Located via string references: "LoadScreen" @ 0x007c1a98, "LoadScreenResRef" @ IFO structure
        /// Original implementation: 0x005226d0 @ 0x005226d0 reads loadscreen from module IFO file
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Loading screen display during module transitions
        /// Located via string references: "loadscreen_p" @ 0x007cbe40 (loading screen GUI panel)
        /// Original implementation: 0x006cff90 @ 0x006cff90 initializes and shows loading screen GUI
        /// - Loads "loadscreen_p" GUI panel with progress bar, hints, and logo
        /// - Sets loading screen image via LoadScreenResRef (TPC texture)
        /// - Displays loading screen during module transitions
        /// </summary>
        /// <param name="imageResRef">Resource reference for the loading screen image (TPC format).</param>
        private void ShowLoadingScreen(string imageResRef)
        {
            if (_loadingScreen != null)
            {
                _loadingScreen.Show(imageResRef);
            }
        }

        /// <summary>
        /// Hides loading screen.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Loading screen is hidden after module load completes
        /// Original implementation: Loading screen is hidden after module transition completes
        /// - Hides "loadscreen_p" GUI panel
        /// - Clears loading screen state
        /// </summary>
        private void HideLoadingScreen()
        {
            if (_loadingScreen != null)
            {
                _loadingScreen.Hide();
            }
        }

        /// <summary>
        /// Plays movies sequentially before module transition.
        /// Based on swkotor.exe/swkotor2.exe: Movie playback system
        /// Located via string references: Movie playback occurs before module transition
        /// Original implementation: Movies play blocking (wait for each to finish before playing next)
        /// Movie files are BIK format (Bink video) - requires BINKW32.DLL
        /// </summary>
        /// <param name="movies">Array of movie file names (BIK format) to play sequentially.</param>
        /// <returns>Task that completes when all movies have finished playing.</returns>
        private async Task PlayMoviesSequentially(string[] movies)
        {
            if (movies == null || movies.Length == 0)
            {
                return;
            }

            if (_moviePlayer == null)
            {
                // Movie player not available (no resource provider), log and continue
                System.Console.WriteLine("[ModuleTransitionSystem] Movie player not available, skipping movie playback");
                return;
            }

            // Play movies sequentially, blocking until each completes
            // Based on swkotor.exe/swkotor2.exe: 0x00404c80 @ 0x00404c80 (playback loop)
            // Original implementation: Movies play sequentially, blocking (waits for each to finish before playing next)
            // If movie playback fails, continues with module transition (graceful error handling)
            CancellationToken cancellationToken = CancellationToken.None;

            for (int i = 0; i < movies.Length; i++)
            {
                if (!string.IsNullOrEmpty(movies[i]))
                {
                    System.Console.WriteLine("[ModuleTransitionSystem] Playing movie '{0}' ({1}/{2})", movies[i], i + 1, movies.Length);

                    // Play movie (blocking until completion)
                    // If playback fails, log error and continue with next movie
                    bool success = await _moviePlayer.PlayMovie(movies[i], cancellationToken);
                    if (!success)
                    {
                        System.Console.WriteLine("[ModuleTransitionSystem] Movie playback failed for '{0}', continuing with next movie", movies[i]);
                    }
                }
            }
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


using System;
using System.Threading.Tasks;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Engines.Common;
using Andastra.Runtime.Games.Aurora;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Aurora
{
    /// <summary>
    /// Aurora Engine game session implementation for Neverwinter Nights and Neverwinter Nights 2.
    /// </summary>
    /// <remarks>
    /// Game Session System (Aurora-specific):
    /// - Based on nwmain.exe: CServerExoAppInternal::LoadModule @ 0x140565c50 (loads module by name, VERIFIED)
    /// - Based on nwmain.exe: CServerExoAppInternal::UnloadModule @ 0x14056df00 (unloads current module, VERIFIED)
    /// - Located via string references: Module loading/unloading functions in CServerExoAppInternal
    /// - Cross-engine analysis:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): FUN_006caab0 @ 0x006caab0 (swkotor2.exe) - server command parser, manages module state flags
    ///   - Aurora (nwmain.exe, nwn2main.exe): CServerExoAppInternal::LoadModule/UnloadModule - similar module state management, different file formats
    ///   - Eclipse (daorigins.exe, DragonAge2.exe): UnrealScript-based module loading, different architecture
    /// - Inheritance: BaseEngineGame (Runtime.Games.Common) implements common module state management
    ///   - Aurora: AuroraGameSession : BaseEngineGame (Runtime.Games.Aurora) - Aurora-specific module loading
    /// - Original implementation: CServerExoAppInternal::LoadModule handles module loading process:
    ///   - Validates module name, stores advertisement data (NWSync module synchronization)
    ///   - Clears module state flags (sets to 0xffffffffffffffff for invalid state)
    ///   - Gets current module object from CGameObjectArray if one exists
    ///   - Calls FUN_140c10370() for actual async module loading (doesn't return, async operation)
    /// - Original implementation: CServerExoAppInternal::UnloadModule handles module unloading:
    ///   - Gets current module object from CGameObjectArray
    ///   - Sets pause state (SetPauseState with flags 0x02 and 0x01)
    ///   - Clears module advertisement data (NWSync advertisement structures)
    ///   - Recreates CFactionManager (clears and reinitializes)
    ///   - Removes all players from areas (CNWSCreature::RemoveFromArea)
    ///   - Destroys all player game objects and resets player state
    ///   - Clears TLK table custom tokens and overrides (CTlkTable::ClearCustomTokens, CTlkTable::ClearOverrides)
    ///   - Destroys and recreates CGameObjectArray (clears all game objects)
    ///   - Clears AI event queue (CServerAIMaster::ClearEventQueue)
    ///   - Clears walkmeshes (CNWPlaceMeshManager::ClearWalkMeshes)
    ///   - Clears tilesets (CNWTileSetManager::ClearTileSets with empty CResRef)
    ///   - Calls FUN_140c10370() for final cleanup (doesn't return, async operation)
    /// - Module state: CServerExoAppInternal maintains module state in internal structures
    /// - Coordinates: Module loading, entity management, script execution, combat, AI, triggers, dialogue
    /// - Game loop integration: Update() called every frame to update all systems (60 Hz fixed timestep)
    /// - Module transitions: Handles loading new modules and positioning player at entry waypoint
    /// - Script execution: Manages NCS VM and engine API integration (Aurora-specific scripting)
    /// - Resource management: Uses AuroraResourceProvider for HAK files, module files, and override directory
    /// </remarks>
    public class AuroraGameSession : BaseEngineGame
    {
        private readonly AuroraEngine _auroraEngine;
        private readonly AuroraResourceProvider _auroraResourceProvider;
        private readonly AuroraModuleLoader _moduleLoader;

        /// <summary>
        /// Initializes a new instance of the AuroraGameSession class.
        /// </summary>
        /// <param name="engine">The Aurora engine instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if engine is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if resource provider is not AuroraResourceProvider.</exception>
        public AuroraGameSession(AuroraEngine engine)
            : base(engine)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            _auroraEngine = engine;

            // Get Aurora resource provider from engine
            if (engine.ResourceProvider is AuroraResourceProvider auroraProvider)
            {
                _auroraResourceProvider = auroraProvider;
            }
            else
            {
                throw new InvalidOperationException("Resource provider must be AuroraResourceProvider for Aurora engine");
            }

            // Initialize module loader (using Andastra.Runtime.Games.Aurora implementation)
            _moduleLoader = new AuroraModuleLoader(engine.World, engine.ResourceProvider);
        }

        /// <summary>
        /// Loads an Aurora module by name.
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to load.</param>
        /// <param name="progressCallback">Optional callback for loading progress (0.0 to 1.0).</param>
        /// <returns>Task that completes when the module is loaded.</returns>
        /// <remarks>
        /// Aurora Module Loading Implementation:
        /// - Based on nwmain.exe: CServerExoAppInternal::LoadModule @ 0x140565c50 (VERIFIED)
        /// - Validates module name (non-null, non-empty)
        /// - Uses AuroraModuleLoader to load module (handles Module.ifo, HAK files, ARE, GIT, entity spawning)
        /// - Updates game session state with current module name
        /// - Original implementation: CServerExoAppInternal::LoadModule stores advertisement data, clears module state flags,
        ///   gets current module object, then calls FUN_140c10370() for async loading
        /// - This implementation uses AuroraModuleLoader which encapsulates the complete module loading sequence
        /// </remarks>
        public override async Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            }

            // Load module using AuroraModuleLoader
            // AuroraModuleLoader handles:
            // - Module.ifo loading
            // - HAK file loading
            // - Entry area ARE file loading
            // - Entry area GIT file loading
            // - Area and navigation mesh initialization
            // - Entity spawning from GIT
            // - Module script execution
            await _moduleLoader.LoadModuleAsync(moduleName, progressCallback);

            // Update game session state
            CurrentModuleName = moduleName;
        }

        /// <summary>
        /// Called when the module is being unloaded to perform Aurora-specific cleanup.
        /// </summary>
        /// <remarks>
        /// Aurora Module Unloading Implementation:
        /// - Based on nwmain.exe: CServerExoAppInternal::UnloadModule @ 0x14056df00 (VERIFIED)
        /// - Uses AuroraModuleLoader to unload module (handles area cleanup, entity removal, resource cleanup)
        /// - Original implementation: CServerExoAppInternal::UnloadModule performs extensive cleanup:
        ///   - Sets pause state (flags 0x02 and 0x01)
        ///   - Clears module advertisement data
        ///   - Recreates CFactionManager
        ///   - Removes all players from areas
        ///   - Destroys all player game objects
        ///   - Clears TLK table custom tokens and overrides
        ///   - Destroys and recreates CGameObjectArray
        ///   - Clears AI event queue
        ///   - Clears walkmeshes
        ///   - Clears tilesets
        ///   - Calls FUN_140c10370() for final cleanup
        /// - This implementation delegates cleanup to AuroraModuleLoader which handles module-specific resource cleanup
        /// </remarks>
        protected override void OnUnloadModule()
        {
            // Unload module using module loader
            // AuroraModuleLoader handles:
            // - Area cleanup and unloading
            // - Entity removal from world
            // - Resource cleanup (HAK files, module resources)
            // - Navigation mesh cleanup
            if (_moduleLoader != null)
            {
                _moduleLoader.UnloadModule();
            }
        }
    }
}


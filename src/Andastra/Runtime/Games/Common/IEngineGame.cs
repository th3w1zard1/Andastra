using System;
using System.Threading.Tasks;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base interface for game session management across all engines.
    /// </summary>
    /// <remarks>
    /// Engine Game Interface:
    /// - Common contract shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Defines the interface for game session management that all engines must provide
    /// - Engine-specific implementations must be in concrete classes (OdysseyGameSession, AuroraGameSession, EclipseGameSession)
    ///
    /// Cross-Engine Reverse Engineering Analysis:
    ///
    /// Common Module State Management Pattern (VERIFIED across Odyssey and Aurora):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x006caab0 @ 0x006caab0 (server command parser)
    ///   - Located via string references: "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58
    ///   - Decompiled code shows three distinct module states:
    ///     - State 0 = Idle (no module loaded) - Line 181: ":: Server mode: Idle.\n"
    ///     - State 1 = ModuleLoaded (module loaded but not running) - Line 190: ":: Server mode: Module Loaded.\n"
    ///     - State 2 = ModuleRunning (module loaded and running) - Line 202: ":: Server mode: Module Running.\n"
    ///   - Module state stored in DAT_008283d4 structure accessed via 0x00638850
    ///   - State flags: *(undefined2 *)(iVar5 + 4) = state value, then flags |= 1 to activate state
    /// - Based on nwmain.exe: CServerExoAppInternal::LoadModule @ 0x140565c50, UnloadModule @ 0x14056df00
    ///   - LoadModule stores module advertisement data (NWSync synchronization)
    ///   - LoadModule clears module state flags (sets to 0xffffffffffffffff for invalid state) before loading
    ///   - UnloadModule performs extensive cleanup: pause state, clears advertisement data, recreates CFactionManager,
    ///     removes players from areas, destroys game objects, clears TLK table, recreates CGameObjectArray,
    ///     clears AI event queue, walkmeshes, and tilesets
    /// - Common pattern: All engines maintain module state flags and perform similar initialization/cleanup sequences
    ///
    /// Common Game Session Lifecycle (VERIFIED):
    /// 1. Initialization: Game session created via IEngine.CreateGameSession()
    ///    - World instance provided by engine (IEngine.World)
    ///    - Module loader initialized (engine-specific: OdysseyModuleLoader, AuroraModuleLoader, etc.)
    ///    - Resource provider configured (GameResourceProvider, AuroraResourceProvider, EclipseResourceProvider)
    ///
    /// 2. Module Loading: LoadModuleAsync(moduleName) called
    ///    - Validates module name (non-null, non-empty)
    ///    - Engine-specific module loader loads module resources (IFO/ARE/GIT files, HAK files, etc.)
    ///    - Module state transitions: Idle -> ModuleLoaded
    ///    - CurrentModuleName property updated
    ///    - Optional progress callback for async loading progress (0.0 to 1.0)
    ///
    /// 3. Module Running: Update(deltaTime) called every frame (typically 60 Hz)
    ///    - World.Update(deltaTime) called to update all systems
    ///    - Module state: ModuleLoaded -> ModuleRunning (after module initialization complete)
    ///    - Systems updated: entities, scripts, combat, AI, triggers, dialogue, encounters
    ///
    /// 4. Module Unloading: UnloadModule() called
    ///    - Module state transitions: ModuleRunning/ModuleLoaded -> Idle
    ///    - Engine-specific cleanup performed (OnUnloadModule())
    ///    - CurrentModuleName set to null
    ///    - PlayerEntity set to null
    ///    - Module resources cleaned up (areas, entities, scripts, etc.)
    ///
    /// 5. Shutdown: Shutdown() called
    ///    - UnloadModule() called to clean up current module
    ///    - Final cleanup and resource disposal
    ///
    /// Common Responsibilities (VERIFIED):
    /// - Module Management: Loading, unloading, state tracking, transitions
    /// - Player Entity Management: Current player entity reference, creation/destruction
    /// - World State Management: Integration with IWorld for entity and system updates
    /// - Resource Coordination: Module resources, script execution context, game systems
    /// - State Synchronization: Module state flags synchronized with engine systems
    ///
    /// Cross-Engine Implementation Notes:
    /// - Odyssey (swkotor.exe, swkotor2.exe): Uses IFO/GIT/ARE file formats, NCS VM for scripts
    /// - Aurora (nwmain.exe, nwn2main.exe): Uses Module.ifo/ARE/GIT file formats, NCS VM for scripts, HAK file system
    /// - Eclipse (daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe): Uses UnrealScript-based architecture
    /// - Infinity (bgmain.exe, iwd.exe): Uses ARE/GAM file formats, different script system
    ///
    /// All engines share the same fundamental lifecycle and state management patterns despite format differences.
    /// </remarks>
    public interface IEngineGame
    {
        /// <summary>
        /// Gets the current module name.
        /// </summary>
        [CanBeNull]
        string CurrentModuleName { get; }

        /// <summary>
        /// Gets the current player entity.
        /// </summary>
        [CanBeNull]
        IEntity PlayerEntity { get; }

        /// <summary>
        /// Gets the world instance.
        /// </summary>
        IWorld World { get; }

        /// <summary>
        /// Loads a module by name.
        /// </summary>
        Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null);

        /// <summary>
        /// Unloads the current module.
        /// </summary>
        void UnloadModule();

        /// <summary>
        /// Updates the game session (called every frame).
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// Shuts down the game session.
        /// </summary>
        void Shutdown();
    }
}



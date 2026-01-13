using System;
using System.Threading.Tasks;
using BioWare.NET.Extract.Installation;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Engines.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Engines.Odyssey
{
    /// <summary>
    /// Odyssey Engine game session implementation for KOTOR 1/2.
    /// </summary>
    /// <remarks>
    /// Game Session System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_006caab0 @ 0x006caab0 (server command parser, handles module commands)
    /// - Located via string references: "GAMEINPROGRESS" @ 0x007c15c8 (game in progress flag), "GameSession" @ 0x007be620
    /// - "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58 (module state tracking, referenced by FUN_006caab0)
    /// - Cross-engine: Similar functions in swkotor.exe (K1), nwmain.exe (Aurora), daorigins.exe (Eclipse)
    /// - Inheritance: BaseEngineGame (Runtime.Games.Common) implements common module state management
    ///   - Odyssey: OdysseyGameSession : BaseEngineGame (Runtime.Games.Odyssey) - Odyssey-specific module loading
    /// - Original implementation: FUN_006caab0 parses server commands starting with 'S', manages module state flags:
    ///   - State 0 = Idle (no module loaded)
    ///   - State 1 = ModuleLoaded (module loaded but not running)
    ///   - State 2 = ModuleRunning (module loaded and running)
    /// - Module state: FUN_006caab0 sets module state flags in DAT_008283d4 structure, accessed via FUN_00638850
    /// - Coordinates: Module loading, entity management, script execution, combat, AI, triggers, dialogue, party
    /// - Game loop integration: Update() called every frame to update all systems (60 Hz fixed timestep)
    /// - Module transitions: Handles loading new modules and positioning player at entry waypoint
    /// - Script execution: Manages NCS VM and engine API integration (K1 vs K2 API based on game version)
    /// </remarks>
    public class OdysseyGameSession : BaseEngineGame
    {
        private readonly OdysseyEngine _odysseyEngine;
        private readonly Installation _installation;
        private readonly OdysseyModuleLoader _moduleLoader;

        public OdysseyGameSession(OdysseyEngine engine)
            : base(engine)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            _odysseyEngine = engine;

            // Get installation from resource provider
            if (engine.ResourceProvider is GameResourceProvider gameResourceProvider)
            {
                _installation = gameResourceProvider.Installation;
            }
            else
            {
                throw new InvalidOperationException("Resource provider must be GameResourceProvider for Odyssey engine");
            }

            // Initialize module loader (now using Andastra.Runtime.Engines.Odyssey implementation)
            _moduleLoader = new OdysseyModuleLoader(engine.World, engine.ResourceProvider);
        }

        public override async Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            }

            // Load module using OdysseyModuleLoader
            await _moduleLoader.LoadModuleAsync(moduleName, progressCallback);

            // Update game session state
            CurrentModuleName = moduleName;
        }

        protected override void OnUnloadModule()
        {
            // Unload module using module loader
            if (_moduleLoader != null)
            {
                _moduleLoader.UnloadModule();
            }
        }
    }
}


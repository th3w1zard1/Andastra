using System;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Engines.Common;
using Andastra.Runtime.Games.Odyssey;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Odyssey
{
    /// <summary>
    /// Odyssey Engine implementation for KOTOR 1/2.
    /// </summary>
    /// <remarks>
    /// Engine Initialization:
    /// - Based on swkotor2.exe: FUN_00404250 @ 0x00404250 (WinMain equivalent, engine initialization)
    /// - Located via string references: "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58, engine initialization in FUN_00404250 @ 0x00404250
    /// - Cross-engine: Similar functions in swkotor.exe (K1), nwmain.exe (Aurora), daorigins.exe (Eclipse)
    /// - Inheritance: BaseEngine (Runtime.Games.Common) implements common engine initialization
    ///   - Odyssey: OdysseyEngine : BaseEngine (Runtime.Games.Odyssey) - Odyssey-specific resource provider (GameResourceProvider wrapping Installation)
    /// - Original implementation: FUN_00404250 @ 0x00404250 initializes engine objects, loads configuration (swkotor2.ini), creates game instance
    /// - Resource provider: CExoKeyTable handles resource loading, tracks loaded resources, FUN_00633270 @ 0x00633270 sets up resource directories
    /// - Game session: Coordinates module loading, entity management, script execution, combat, AI
    /// - Module loader: Handles loading module files (MOD, ARE, GIT, etc.) and spawning entities
    /// - Module state: FUN_006caab0 @ 0x006caab0 sets module state flags (ModuleLoaded, ModuleRunning)
    /// - Engine initializes resource provider, world, engine API, game session
    /// </remarks>
    public class OdysseyEngine : BaseEngine
    {
        private string _installationPath;

        public OdysseyEngine(IEngineProfile profile)
            : base(profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.EngineFamily != EngineFamily.Odyssey)
            {
                throw new ArgumentException("Profile must be for Odyssey engine family", nameof(profile));
            }
        }

        public override IEngineGame CreateGameSession()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Engine must be initialized before creating game session");
            }

            return new OdysseyGameSession(this);
        }

        protected override IGameResourceProvider CreateResourceProvider(string installationPath)
        {
            if (string.IsNullOrEmpty(installationPath))
            {
                throw new ArgumentException("Installation path cannot be null or empty", nameof(installationPath));
            }

            _installationPath = installationPath;
            Installation installation = new Installation(installationPath);
            return new GameResourceProvider(installation);
        }

        protected override World CreateWorld()
        {
            var timeManager = new OdysseyTimeManager();
            return new World(timeManager);
        }
    }
}


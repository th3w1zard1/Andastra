using System;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Core.Audio;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Engines.Odyssey.UI;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Engines.Odyssey.Game
{
    /// <summary>
    /// Game services context implementation for Odyssey engine.
    /// Provides access to game systems from script execution context.
    /// </summary>
    /// <remarks>
    /// Odyssey Game Services Context Implementation:
    /// - Inherits from BaseGameServicesContext (Runtime.Games.Common) with Odyssey-specific services
    /// - Based on swkotor.exe and swkotor2.exe script execution context system
    /// - Located via string references: Script execution context provides access to game systems
    /// - Original implementation: NWScript execution context (IExecutionContext) provides access to game services
    /// - Services accessible from scripts: DialogueManager, PlayerEntity, CombatManager, PartyManager, ModuleLoader, UISystem
    /// - Based on swkotor.exe: Script execution context setup (KOTOR1)
    /// - Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 (script execution context setup, KOTOR2)
    /// </remarks>
    internal class GameServicesContext : BaseGameServicesContext
    {
        private readonly GameSession _gameSession;
        private readonly object _combatManager;
        private readonly object _partyManager;
        private readonly object _moduleLoader;
        private readonly object _factionManager;
        private readonly object _perceptionManager;
        private readonly object _cameraController;
        private readonly object _journalSystem;

        public GameServicesContext(
            GameSession gameSession,
            Installation installation,
            IWorld world,
            object combatManager = null,
            object partyManager = null,
            object moduleLoader = null,
            object factionManager = null,
            object perceptionManager = null,
            object cameraController = null,
            ISoundPlayer soundPlayer = null,
            IMusicPlayer musicPlayer = null,
            object journalSystem = null)
            : base(soundPlayer, musicPlayer, new OdysseyUISystem(installation, world))
        {
            if (gameSession == null)
            {
                throw new ArgumentNullException("gameSession");
            }
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            _gameSession = gameSession;
            _combatManager = combatManager;
            _partyManager = partyManager;
            _moduleLoader = moduleLoader;
            _factionManager = factionManager;
            _perceptionManager = perceptionManager;
            _cameraController = cameraController;
            _journalSystem = journalSystem;
        }

        public override object DialogueManager
        {
            get { return _gameSession.DialogueManager; }
        }

        public override IEntity PlayerEntity
        {
            get { return _gameSession.PlayerEntity; }
        }

        public override object CombatManager
        {
            get { return _combatManager; }
        }

        public override object PartyManager
        {
            get { return _partyManager; }
        }

        public override object ModuleLoader
        {
            get { return _moduleLoader; }
        }

        public override object FactionManager
        {
            get { return _factionManager; }
        }

        public override object PerceptionManager
        {
            get { return _perceptionManager; }
        }

        public override object GameSession
        {
            get { return _gameSession; }
        }

        public override object CameraController
        {
            get { return _cameraController; }
        }

        public override object JournalSystem
        {
            get { return _journalSystem; }
        }
    }
}


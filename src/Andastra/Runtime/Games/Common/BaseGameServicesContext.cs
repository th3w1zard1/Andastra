using System;
using Andastra.Runtime.Core.Audio;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of game services context shared across all BioWare engines.
    /// Provides access to game systems from script execution context.
    /// </summary>
    /// <remarks>
    /// Base Game Services Context Implementation:
    /// - Common game services accessible from script execution context across all engines
    /// - Script execution context provides access to game systems for engine API functions
    /// - Services accessible from scripts: PlayerEntity, SoundPlayer, UISystem, IsLoadingFromSave
    /// - Engine-specific services (DialogueManager, CombatManager, etc.) are provided by subclasses
    ///
    /// Based on reverse engineering of script execution context systems:
    /// - Common across all engines: Script execution context provides access to game services
    /// - Original implementation: NWScript execution context (IExecutionContext) provides access to game services
    /// - Engine API functions access AdditionalContext as IGameServicesContext to access game services
    /// - Script execution: Engine API functions check for VMExecutionContext.AdditionalContext
    ///   and cast to IGameServicesContext to access game services
    ///
    /// Common functionality across all engines:
    /// - PlayerEntity: Current player character entity (IEntity - common interface)
    /// - SoundPlayer: Audio playback system (ISoundPlayer - common interface)
    /// - UISystem: UI screen and overlay management (IUISystem - common interface)
    /// - IsLoadingFromSave: Flag indicating if game is loading from save file
    ///
    /// Engine-specific services (provided by subclasses):
    /// - DialogueManager: Dialogue system for conversation management (engine-specific)
    /// - CombatManager: Combat system for battle management (engine-specific)
    /// - PartyManager: Party management for party member operations (engine-specific)
    /// - ModuleLoader: Module loading system for area transitions (engine-specific)
    /// - FactionManager: Faction relationship management (engine-specific)
    /// - PerceptionManager: Creature perception system (engine-specific)
    /// - CameraController: Camera positioning and movement (engine-specific)
    /// - GameSession: Game state management (engine-specific)
    /// - JournalSystem: Quest and journal management (engine-specific)
    /// </remarks>
    [PublicAPI]
    public abstract class BaseGameServicesContext : IGameServicesContext
    {
        protected readonly ISoundPlayer _soundPlayer;
        protected readonly IMusicPlayer _musicPlayer;
        protected readonly IUISystem _uiSystem;
        protected bool _isLoadingFromSave;

        /// <summary>
        /// Creates a new base game services context.
        /// </summary>
        /// <param name="soundPlayer">The sound player for audio playback.</param>
        /// <param name="musicPlayer">The music player for background music playback.</param>
        /// <param name="uiSystem">The UI system for screen management.</param>
        protected BaseGameServicesContext(ISoundPlayer soundPlayer, IMusicPlayer musicPlayer, IUISystem uiSystem)
        {
            _soundPlayer = soundPlayer;
            _musicPlayer = musicPlayer;
            _uiSystem = uiSystem;
        }

        /// <summary>
        /// Gets the player entity.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Player entity is accessible from script execution context.
        /// Engine-specific subclasses provide the actual player entity implementation.
        /// </remarks>
        public abstract IEntity PlayerEntity { get; }

        /// <summary>
        /// Gets the sound player.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Sound player provides audio playback functionality.
        /// </remarks>
        public ISoundPlayer SoundPlayer
        {
            get { return _soundPlayer; }
        }

        /// <summary>
        /// Gets the music player for background music playback.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Music player provides looping background music functionality.
        /// </remarks>
        public IMusicPlayer MusicPlayer
        {
            get { return _musicPlayer; }
        }

        /// <summary>
        /// Gets the UI system for managing UI screens and overlays.
        /// </summary>
        /// <remarks>
        /// Common across all engines: UI system manages screen state and transitions.
        /// </remarks>
        public IUISystem UISystem
        {
            get { return _uiSystem; }
        }

        /// <summary>
        /// Gets or sets whether the game is loading from a save.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Flag prevents script execution during save loading.
        /// </remarks>
        public bool IsLoadingFromSave
        {
            get { return _isLoadingFromSave; }
            set { _isLoadingFromSave = value; }
        }

        /// <summary>
        /// Gets the dialogue manager (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Dialogue system varies by engine (Odyssey, Aurora, Eclipse).
        /// Subclasses provide engine-specific dialogue manager implementations.
        /// </remarks>
        public abstract object DialogueManager { get; }

        /// <summary>
        /// Gets the combat manager (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Combat system varies by engine.
        /// Subclasses provide engine-specific combat manager implementations.
        /// </remarks>
        public abstract object CombatManager { get; }

        /// <summary>
        /// Gets the party manager (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Party system varies by engine.
        /// Subclasses provide engine-specific party manager implementations.
        /// </remarks>
        public abstract object PartyManager { get; }

        /// <summary>
        /// Gets the module loader (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Module loading system varies by engine.
        /// Subclasses provide engine-specific module loader implementations.
        /// </remarks>
        public abstract object ModuleLoader { get; }

        /// <summary>
        /// Gets the faction manager (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Faction system varies by engine.
        /// Subclasses provide engine-specific faction manager implementations.
        /// </remarks>
        public abstract object FactionManager { get; }

        /// <summary>
        /// Gets the perception manager (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Perception system varies by engine.
        /// Subclasses provide engine-specific perception manager implementations.
        /// </remarks>
        public abstract object PerceptionManager { get; }

        /// <summary>
        /// Gets the game session (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Game session management varies by engine.
        /// Subclasses provide engine-specific game session implementations.
        /// </remarks>
        public abstract object GameSession { get; }

        /// <summary>
        /// Gets the camera controller (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Camera system varies by engine.
        /// Subclasses provide engine-specific camera controller implementations.
        /// </remarks>
        public abstract object CameraController { get; }

        /// <summary>
        /// Gets the journal system (engine-specific, accessed as object to avoid dependency).
        /// </summary>
        /// <remarks>
        /// Engine-specific: Journal system varies by engine.
        /// Subclasses provide engine-specific journal system implementations.
        /// </remarks>
        public abstract object JournalSystem { get; }
    }
}


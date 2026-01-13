using System.Collections.Generic;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for creature perception (sight and hearing).
    /// </summary>
    /// <remarks>
    /// Perception Component Interface:
    /// - Common interface for perception systems across all BioWare engines
    /// - Cross-engine analysis:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): PerceptionData/PerceptionList structures, 0x005fb0f0 @ 0x005fb0f0 (swkotor2.exe), 0x005afce0 @ 0x005afce0 (swkotor.exe)
    ///   - Aurora (nwmain.exe): DoPerceptionUpdateOnCreature @ 0x14038b0c0, PerceptionList/PerceptionData structures
    ///   - Eclipse (daorigins.exe, DragonAge2.exe): PerceptionClass found, uses different AI system
    ///   - Infinity (, ): DisplayPerceptionList found, squad-based perception system
    /// - Each creature has sight and hearing ranges (SightRange, HearingRange)
    /// - Perception is updated periodically (not every frame)
    /// - Events fire when perception state changes: OnPerceive (new object seen/heard), OnVanish (object no longer seen)
    /// - Sight checks: Distance within sight range, line of sight (optional raycasting), not invisible
    /// - Hearing checks: Distance within hearing range, sound source is active, not silenced
    /// - Engine-specific implementations inherit from BasePerceptionComponent in Runtime.Games.Common.Components
    /// </remarks>
    public interface IPerceptionComponent : IComponent
    {
        /// <summary>
        /// Sight perception range.
        /// </summary>
        float SightRange { get; set; }

        /// <summary>
        /// Hearing perception range.
        /// </summary>
        float HearingRange { get; set; }

        /// <summary>
        /// Gets entities that are currently seen.
        /// </summary>
        IEnumerable<IEntity> GetSeenObjects();

        /// <summary>
        /// Gets entities that are currently heard.
        /// </summary>
        IEnumerable<IEntity> GetHeardObjects();

        /// <summary>
        /// Checks if a specific entity was seen.
        /// </summary>
        bool WasSeen(IEntity entity);

        /// <summary>
        /// Checks if a specific entity was heard.
        /// </summary>
        bool WasHeard(IEntity entity);

        /// <summary>
        /// Updates perception state for an entity.
        /// </summary>
        void UpdatePerception(IEntity entity, bool canSee, bool canHear);

        /// <summary>
        /// Clears all perception data.
        /// </summary>
        void ClearPerception();
    }
}


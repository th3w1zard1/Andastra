using System.Collections.Generic;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for creature perception (sight and hearing).
    /// </summary>
    /// <remarks>
    /// Perception Component Interface:
    /// - Based on swkotor2.exe perception system
    /// - Located via string references: "PERCEPTIONDIST" @ 0x007c4070, "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68
    /// - Original implementation: FUN_005fb0f0 @ 0x005fb0f0 (perception update)
    /// - Each creature has sight and hearing ranges (SightRange, HearingRange)
    /// - Perception is updated periodically (not every frame)
    /// - Events fire when perception state changes: OnPerceive (new object seen/heard), OnVanish (object no longer seen)
    /// - Sight checks: Distance within sight range, line of sight (optional raycasting), not invisible
    /// - Hearing checks: Distance within hearing range, sound source is active, not silenced
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


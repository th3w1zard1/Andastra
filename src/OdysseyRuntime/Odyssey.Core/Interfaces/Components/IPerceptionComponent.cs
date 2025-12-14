using System.Collections.Generic;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for creature perception (sight and hearing).
    /// </summary>
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


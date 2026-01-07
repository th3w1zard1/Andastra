using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Games.Common.Components
{
    /// <summary>
    /// Base class for perception components shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Perception Component:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses:
    ///   - Odyssey: PerceptionComponent (swkotor.exe: FUN_005afce0 @ 0x005afce0, swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0)
    ///   - Aurora: AuroraPerceptionComponent (nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0)
    ///   - Eclipse: EclipsePerceptionComponent (daorigins.exe: PerceptionClass found, different AI system)
    ///   - Infinity: InfinityPerceptionComponent (, : DisplayPerceptionList, squad-based)
    /// - Common: SightRange, HearingRange, GetSeenObjects(), GetHeardObjects(), WasSeen(), WasHeard(), UpdatePerception(), ClearPerception()
    /// - Engine-specific: PerceptionManager integration, perception event types, serialization format, update frequency
    ///
    /// Cross-Engine Analysis:
    /// - All engines share: Sight/hearing ranges, perception state tracking (seen/heard), perception queries
    /// - Odyssey-specific: PerceptionData/PerceptionList structures, CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION events
    /// - Aurora-specific: CNWSCreature::DoPerceptionUpdateOnCreature, CNWSCreature::SpawnInHeartbeatPerception
    /// - Eclipse-specific: PerceptionClass, COMMAND_CLEARPERCEPTIONLIST, COMMAND_TRIGGERPERCEPTION
    /// - Infinity-specific: Squad-based perception (AddSquadToPerception, RemoveSquadFromPerception), DisplayPerceptionList
    /// </remarks>
    public abstract class BasePerceptionComponent : IPerceptionComponent
    {
        /// <summary>
        /// Information about a perceived entity (common across all engines).
        /// </summary>
        protected class PerceptionInfo
        {
            public bool Seen { get; set; }
            public bool Heard { get; set; }
            public bool WasSeen { get; set; }
            public bool WasHeard { get; set; }
        }

        protected readonly Dictionary<uint, PerceptionInfo> _perceivedEntities;

        protected BasePerceptionComponent()
        {
            _perceivedEntities = new Dictionary<uint, PerceptionInfo>();
            SightRange = DefaultSightRange;
            HearingRange = DefaultHearingRange;
        }

        #region IComponent Implementation

        public IEntity Owner { get; set; }

        public virtual void OnAttach()
        {
            // Default ranges - can be customized after creation
            // Common pattern: Hearing range is typically 75% of sight range
            if (HearingRange <= 0)
            {
                HearingRange = SightRange * 0.75f;
            }
        }

        public virtual void OnDetach()
        {
            ClearPerception();
        }

        #endregion

        #region IPerceptionComponent Implementation

        /// <summary>
        /// Sight perception range in meters.
        /// </summary>
        /// <remarks>
        /// Common across all engines:
        /// - Odyssey: From appearances.2da PERSPACE column (~20m sight for standard creatures)
        /// - Aurora: From creature stats PerceptionRange field
        /// - Eclipse: From PerceptionClass data
        /// - Infinity: From squad/pawn perception settings
        /// </remarks>
        public float SightRange { get; set; }

        /// <summary>
        /// Hearing perception range in meters.
        /// </summary>
        /// <remarks>
        /// Common across all engines:
        /// - Typically 75% of sight range by default
        /// - Can be modified by effects/feats/abilities
        /// </remarks>
        public float HearingRange { get; set; }

        /// <summary>
        /// Gets entities that are currently seen.
        /// </summary>
        /// <remarks>
        /// Common implementation pattern across all engines:
        /// - Iterates through tracked perceived entities
        /// - Returns only entities where Seen is true
        /// - Engine-specific subclasses may override to use PerceptionManager
        /// </remarks>
        public virtual IEnumerable<IEntity> GetSeenObjects()
        {
            if (Owner != null && Owner.World != null)
            {
                foreach (KeyValuePair<uint, PerceptionInfo> kvp in _perceivedEntities)
                {
                    if (kvp.Value.Seen)
                    {
                        IEntity entity = Owner.World.GetEntity(kvp.Key);
                        if (entity != null && entity.IsValid)
                        {
                            yield return entity;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets entities that are currently heard.
        /// </summary>
        /// <remarks>
        /// Common implementation pattern across all engines:
        /// - Iterates through tracked perceived entities
        /// - Returns only entities where Heard is true
        /// - Engine-specific subclasses may override to use PerceptionManager
        /// </remarks>
        public virtual IEnumerable<IEntity> GetHeardObjects()
        {
            if (Owner != null && Owner.World != null)
            {
                foreach (KeyValuePair<uint, PerceptionInfo> kvp in _perceivedEntities)
                {
                    if (kvp.Value.Heard)
                    {
                        IEntity entity = Owner.World.GetEntity(kvp.Key);
                        if (entity != null && entity.IsValid)
                        {
                            yield return entity;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a specific entity was seen.
        /// </summary>
        /// <remarks>
        /// Common implementation pattern across all engines:
        /// - Checks current Seen state or historical WasSeen state
        /// - Engine-specific subclasses may override to use PerceptionManager
        /// </remarks>
        public virtual bool WasSeen(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            PerceptionInfo info;
            if (_perceivedEntities.TryGetValue(entity.ObjectId, out info))
            {
                return info.Seen || info.WasSeen;
            }
            return false;
        }

        /// <summary>
        /// Checks if a specific entity was heard.
        /// </summary>
        /// <remarks>
        /// Common implementation pattern across all engines:
        /// - Checks current Heard state or historical WasHeard state
        /// - Engine-specific subclasses may override to use PerceptionManager
        /// </remarks>
        public virtual bool WasHeard(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            PerceptionInfo info;
            if (_perceivedEntities.TryGetValue(entity.ObjectId, out info))
            {
                return info.Heard || info.WasHeard;
            }
            return false;
        }

        /// <summary>
        /// Updates perception state for an entity.
        /// </summary>
        /// <remarks>
        /// Common implementation pattern across all engines:
        /// - Tracks current and previous perception state
        /// - Used to detect state changes (new perception, vanish events)
        /// - Engine-specific subclasses may override to integrate with PerceptionManager
        /// </remarks>
        public virtual void UpdatePerception(IEntity entity, bool canSee, bool canHear)
        {
            if (entity == null)
            {
                return;
            }

            PerceptionInfo info;
            if (!_perceivedEntities.TryGetValue(entity.ObjectId, out info))
            {
                info = new PerceptionInfo();
                _perceivedEntities[entity.ObjectId] = info;
            }

            // Track previous state (common pattern across all engines)
            info.WasSeen = info.Seen;
            info.WasHeard = info.Heard;

            // Update current state
            info.Seen = canSee;
            info.Heard = canHear;
        }

        /// <summary>
        /// Clears all perception data.
        /// </summary>
        /// <remarks>
        /// Common implementation pattern across all engines:
        /// - Clears all tracked perception state
        /// - Called when component is detached or perception is reset
        /// </remarks>
        public virtual void ClearPerception()
        {
            _perceivedEntities.Clear();
        }

        #endregion

        #region Common Constants

        /// <summary>
        /// Default sight range in meters (common across all engines).
        /// </summary>
        /// <remarks>
        /// Common default: ~20 meters for standard creatures
        /// Engine-specific defaults may override this in constructors
        /// </remarks>
        protected const float DefaultSightRange = 20.0f;

        /// <summary>
        /// Default hearing range in meters (common across all engines).
        /// </summary>
        /// <remarks>
        /// Common default: ~15 meters (75% of sight range)
        /// Engine-specific defaults may override this in constructors
        /// </remarks>
        protected const float DefaultHearingRange = 15.0f;

        #endregion

        #region Common Helper Methods

        /// <summary>
        /// Gets the perception info for an entity, creating it if it doesn't exist.
        /// </summary>
        protected PerceptionInfo GetOrCreatePerceptionInfo(uint entityId)
        {
            PerceptionInfo info;
            if (!_perceivedEntities.TryGetValue(entityId, out info))
            {
                info = new PerceptionInfo();
                _perceivedEntities[entityId] = info;
            }
            return info;
        }

        /// <summary>
        /// Gets the perception info for an entity, or null if it doesn't exist.
        /// </summary>
        protected PerceptionInfo GetPerceptionInfo(uint entityId)
        {
            PerceptionInfo info;
            _perceivedEntities.TryGetValue(entityId, out info);
            return info;
        }

        #endregion
    }
}


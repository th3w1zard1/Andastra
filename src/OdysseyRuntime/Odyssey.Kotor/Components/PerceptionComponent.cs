using System.Collections.Generic;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Kotor.Systems;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Concrete implementation of perception component for KOTOR.
    /// </summary>
    /// <remarks>
    /// Perception in KOTOR:
    /// - Creatures have sight and hearing ranges
    /// - Perception updates periodically
    /// - Scripts can query GetLastPerceived, GetObjectSeen, etc.
    /// - Perception fires OnPerception event on creature
    /// 
    /// Default ranges (from appearances.2da PERSPACE column):
    /// - Standard creature: ~20m sight, ~15m hearing
    /// - Can be modified by effects/feats
    /// </remarks>
    public class PerceptionComponent : IPerceptionComponent
    {
        private readonly Dictionary<uint, PerceptionInfo> _perceivedEntities;
        private PerceptionManager _perceptionManager;

        /// <summary>
        /// Information about a perceived entity.
        /// </summary>
        private class PerceptionInfo
        {
            public bool Seen { get; set; }
            public bool Heard { get; set; }
            public bool WasSeen { get; set; }
            public bool WasHeard { get; set; }
        }

        public PerceptionComponent()
        {
            _perceivedEntities = new Dictionary<uint, PerceptionInfo>();
            SightRange = PerceptionManager.DefaultSightRange;
            HearingRange = PerceptionManager.DefaultHearingRange;
        }

        public PerceptionComponent(PerceptionManager perceptionManager) : this()
        {
            _perceptionManager = perceptionManager;
        }

        #region IComponent Implementation

        public IEntity Owner { get; set; }

        public void OnAttach()
        {
            // Load perception ranges from entity data if available
            if (Owner != null)
            {
                if (Owner.HasData("PerceptionRange"))
                {
                    SightRange = Owner.GetData<float>("PerceptionRange", PerceptionManager.DefaultSightRange);
                }
                // Hearing is typically slightly less than sight
                HearingRange = SightRange * 0.75f;
            }
        }

        public void OnDetach()
        {
            ClearPerception();
        }

        #endregion

        #region IPerceptionComponent Implementation

        /// <summary>
        /// Sight perception range in meters.
        /// </summary>
        public float SightRange { get; set; }

        /// <summary>
        /// Hearing perception range in meters.
        /// </summary>
        public float HearingRange { get; set; }

        /// <summary>
        /// Gets entities that are currently seen.
        /// </summary>
        public IEnumerable<IEntity> GetSeenObjects()
        {
            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.GetSeenObjects(Owner);
            }

            // Fall back to local tracking
            foreach (var kvp in _perceivedEntities)
            {
                if (kvp.Value.Seen && Owner != null)
                {
                    // Would need World reference to look up entity
                    // For now, skip
                }
            }

            return new List<IEntity>();
        }

        /// <summary>
        /// Gets entities that are currently heard.
        /// </summary>
        public IEnumerable<IEntity> GetHeardObjects()
        {
            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.GetHeardObjects(Owner);
            }

            return new List<IEntity>();
        }

        /// <summary>
        /// Checks if a specific entity was seen.
        /// </summary>
        public bool WasSeen(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.HasSeen(Owner, entity);
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
        public bool WasHeard(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.HasHeard(Owner, entity);
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
        public void UpdatePerception(IEntity entity, bool canSee, bool canHear)
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

            // Track previous state
            info.WasSeen = info.Seen;
            info.WasHeard = info.Heard;

            // Update current state
            info.Seen = canSee;
            info.Heard = canHear;
        }

        /// <summary>
        /// Clears all perception data.
        /// </summary>
        public void ClearPerception()
        {
            _perceivedEntities.Clear();
        }

        #endregion

        #region Extended Methods

        /// <summary>
        /// Sets the perception manager reference.
        /// </summary>
        public void SetPerceptionManager(PerceptionManager manager)
        {
            _perceptionManager = manager;
        }

        /// <summary>
        /// Gets the last perceived object (for GetLastPerceived NWScript).
        /// </summary>
        public IEntity LastPerceived { get; set; }

        /// <summary>
        /// Whether the last perception event was a sight event.
        /// </summary>
        public bool LastPerceptionSeen { get; set; }

        /// <summary>
        /// Whether the last perception event was a hearing event.
        /// </summary>
        public bool LastPerceptionHeard { get; set; }

        /// <summary>
        /// Whether the last perception was a vanish (no longer seen).
        /// </summary>
        public bool LastPerceptionVanished { get; set; }

        /// <summary>
        /// Whether the last perception was inaudible (no longer heard).
        /// </summary>
        public bool LastPerceptionInaudible { get; set; }

        /// <summary>
        /// Records a perception event.
        /// </summary>
        public void RecordPerceptionEvent(IEntity perceived, bool seen, bool heard, bool vanished, bool inaudible)
        {
            LastPerceived = perceived;
            LastPerceptionSeen = seen;
            LastPerceptionHeard = heard;
            LastPerceptionVanished = vanished;
            LastPerceptionInaudible = inaudible;
        }

        /// <summary>
        /// Checks if this creature can currently see a target.
        /// </summary>
        public bool CanSee(IEntity target)
        {
            if (target == null)
            {
                return false;
            }

            PerceptionInfo info;
            if (_perceivedEntities.TryGetValue(target.ObjectId, out info))
            {
                return info.Seen;
            }
            return false;
        }

        /// <summary>
        /// Checks if this creature can currently hear a target.
        /// </summary>
        public bool CanHear(IEntity target)
        {
            if (target == null)
            {
                return false;
            }

            PerceptionInfo info;
            if (_perceivedEntities.TryGetValue(target.ObjectId, out info))
            {
                return info.Heard;
            }
            return false;
        }

        /// <summary>
        /// Gets the count of seen objects.
        /// </summary>
        public int SeenCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _perceivedEntities)
                {
                    if (kvp.Value.Seen)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Gets the count of heard objects.
        /// </summary>
        public int HeardCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _perceivedEntities)
                {
                    if (kvp.Value.Heard)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        #endregion
    }
}

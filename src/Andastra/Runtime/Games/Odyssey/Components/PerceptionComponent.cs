using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Systems;

namespace Andastra.Runtime.Engines.Odyssey.Components
{
    /// <summary>
    /// Concrete implementation of perception component for KOTOR.
    /// </summary>
    /// <remarks>
    /// Perception Component (Odyssey-specific):
    /// - Based on swkotor.exe: FUN_00500610 @ 0x00500610, FUN_005afce0 @ 0x005afce0 (perception data serialization)
    /// - Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception checking), FUN_005226d0 @ 0x005226d0 (perception data serialization)
    /// - Located via string references: "PerceptionData" @ 0x007bf6c4 (swkotor2.exe), "PerceptionList" @ 0x007bf6d4 (swkotor2.exe)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68, "PerceptionRange" @ 0x007c4080
    /// - "PERCEPTIONDIST" @ 0x007c4070
    /// - Original implementation: Creatures have sight and hearing perception ranges
    /// - Perception updates periodically (checked during heartbeat/update loop)
    /// - Scripts can query GetLastPerceived, GetObjectSeen, etc. (NWScript engine API)
    /// - Perception fires OnPerception script event on creature when new entities are detected
    /// - Default ranges: From appearances.2da PERSPACE column (~20m sight, ~15m hearing for standard creatures)
    /// - Can be modified by effects/feats (perception bonuses)
    /// 
    /// Cross-Engine Analysis:
    /// - Odyssey (swkotor.exe, swkotor2.exe): PerceptionData/PerceptionList structures, FUN_005fb0f0 perception checking
    /// - Aurora (nwmain.exe): PerceptionList/PerceptionData in SaveCreature @ 0x1403a0a60 (perception data serialization)
    /// - Eclipse (daorigins.exe, DragonAge2.exe): No direct perception component found (uses different AI system)
    /// - Infinity (MassEffect.exe, MassEffect2.exe): DisplayPerceptionList found but no component implementation
    /// 
    /// Inheritance Structure:
    /// - Currently only Odyssey has PerceptionComponent implementation
    /// - When other engines implement PerceptionComponent, common functionality should be extracted to BasePerceptionComponent
    /// - Common functionality: SightRange, HearingRange, GetSeenObjects(), GetHeardObjects(), WasSeen(), WasHeard()
    /// - Engine-specific: PerceptionManager integration, perception event types, serialization format
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
            // Default ranges - can be customized after creation
            HearingRange = SightRange * 0.75f;
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
        /// <remarks>
        /// Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception checking)
        /// When perception manager is available, uses centralized tracking.
        /// Falls back to local component tracking using Owner.World.GetEntity() for entity lookup.
        /// </remarks>
        public IEnumerable<IEntity> GetSeenObjects()
        {
            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.GetSeenObjects(Owner);
            }

            // Fall back to local tracking
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
        /// Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception checking)
        /// When perception manager is available, uses centralized tracking.
        /// Falls back to local component tracking using Owner.World.GetEntity() for entity lookup.
        /// </remarks>
        public IEnumerable<IEntity> GetHeardObjects()
        {
            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.GetHeardObjects(Owner);
            }

            // Fall back to local tracking
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
                foreach (KeyValuePair<uint, PerceptionInfo> kvp in _perceivedEntities)
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
                foreach (KeyValuePair<uint, PerceptionInfo> kvp in _perceivedEntities)
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

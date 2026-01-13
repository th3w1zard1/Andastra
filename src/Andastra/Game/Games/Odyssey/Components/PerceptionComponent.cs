using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Odyssey.Systems;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Odyssey.Components
{
    /// <summary>
    /// Odyssey-specific implementation of perception component for KOTOR (K1 and K2).
    /// </summary>
    /// <remarks>
    /// Perception Component (Odyssey-specific):
    /// - Inherits from BasePerceptionComponent (common functionality)
    /// - Based on swkotor.exe: 0x00500610 @ 0x00500610, 0x005afce0 @ 0x005afce0 (perception data serialization)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 @ 0x005fb0f0 (perception checking), 0x005226d0 @ 0x005226d0 (perception data serialization)
    /// - Located via string references: "PerceptionData" @ 0x007bf6c4 (swkotor2.exe), "PerceptionList" @ 0x007bf6d4 (swkotor2.exe)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68 (swkotor2.exe), "PerceptionRange" @ 0x007c4080 (swkotor2.exe)
    /// - "PERCEPTIONDIST" @ 0x007c4070 (swkotor2.exe), "PERCEPTIONDIST" @ 0x0074ae10 (swkotor.exe)
    /// - Original implementation: Creatures have sight and hearing perception ranges
    /// - Perception updates periodically (checked during heartbeat/update loop)
    /// - Scripts can query GetLastPerceived, GetObjectSeen, etc. (NWScript engine API)
    /// - Perception fires OnPerception script event on creature when new entities are detected
    /// - Default ranges: From appearances.2da PERSPACE column (~20m sight, ~15m hearing for standard creatures)
    /// - Can be modified by effects/feats (perception bonuses)
    ///
    /// Odyssey-Specific Features:
    /// - Integration with OdysseyPerceptionManager for centralized perception tracking
    /// - CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION script events
    /// - PerceptionData/PerceptionList serialization format (GFF structures)
    /// - Extended methods: LastPerceived, RecordPerceptionEvent, CanSee, CanHear, SeenCount, HeardCount
    /// </remarks>
    public class PerceptionComponent : BasePerceptionComponent
    {
        private PerceptionManager _perceptionManager;

        public PerceptionComponent()
        {
            SightRange = PerceptionManager.DefaultSightRange;
            HearingRange = PerceptionManager.DefaultHearingRange;
        }

        public PerceptionComponent(PerceptionManager perceptionManager) : this()
        {
            _perceptionManager = perceptionManager;
        }

        #region IPerceptionComponent Implementation (Odyssey-specific overrides)

        /// <summary>
        /// Gets entities that are currently seen.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Uses PerceptionManager for centralized tracking when available.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 @ 0x005fb0f0 (perception checking)
        /// Falls back to base class implementation for local tracking.
        /// </remarks>
        public override IEnumerable<IEntity> GetSeenObjects()
        {
            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.GetSeenObjects(Owner);
            }

            // Fall back to base class implementation
            return base.GetSeenObjects();
        }

        /// <summary>
        /// Gets entities that are currently heard.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Uses PerceptionManager for centralized tracking when available.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 @ 0x005fb0f0 (perception checking)
        /// Falls back to base class implementation for local tracking.
        /// </remarks>
        public override IEnumerable<IEntity> GetHeardObjects()
        {
            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.GetHeardObjects(Owner);
            }

            // Fall back to base class implementation
            return base.GetHeardObjects();
        }

        /// <summary>
        /// Checks if a specific entity was seen.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Uses PerceptionManager for centralized tracking when available.
        /// Falls back to base class implementation for local tracking.
        /// </remarks>
        public override bool WasSeen(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.HasSeen(Owner, entity);
            }

            // Fall back to base class implementation
            return base.WasSeen(entity);
        }

        /// <summary>
        /// Checks if a specific entity was heard.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Uses PerceptionManager for centralized tracking when available.
        /// Falls back to base class implementation for local tracking.
        /// </remarks>
        public override bool WasHeard(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            if (_perceptionManager != null && Owner != null)
            {
                return _perceptionManager.HasHeard(Owner, entity);
            }

            // Fall back to base class implementation
            return base.WasHeard(entity);
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
        /// <remarks>
        /// Odyssey-specific: Uses local perception tracking.
        /// </remarks>
        public bool CanSee(IEntity target)
        {
            if (target == null)
            {
                return false;
            }

            PerceptionInfo info = GetPerceptionInfo(target.ObjectId);
            if (info != null)
            {
                return info.Seen;
            }
            return false;
        }

        /// <summary>
        /// Checks if this creature can currently hear a target.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Uses local perception tracking.
        /// </remarks>
        public bool CanHear(IEntity target)
        {
            if (target == null)
            {
                return false;
            }

            PerceptionInfo info = GetPerceptionInfo(target.ObjectId);
            if (info != null)
            {
                return info.Heard;
            }
            return false;
        }

        /// <summary>
        /// Gets the count of seen objects.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Counts entities in local perception tracking.
        /// </remarks>
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
        /// <remarks>
        /// Odyssey-specific: Counts entities in local perception tracking.
        /// </remarks>
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

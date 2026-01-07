using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Aurora.Components
{
    /// <summary>
    /// Aurora-specific implementation of perception component for Neverwinter Nights.
    /// </summary>
    /// <remarks>
    /// Aurora Perception Component:
    /// - Inherits from BasePerceptionComponent (common functionality)
    /// - Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0 (nwmain.exe)
    /// - Located via string references: "PerceptionRange" @ 0x140dde0e0 (nwmain.exe)
    /// - Original implementation: 
    ///   1. Gets creature's perception range (sight/hearing ranges from PerceptionRange field)
    ///   2. Iterates through all creatures in the same area
    ///   3. Calculates distance between creatures
    ///   4. Checks if within perception range (sight or hearing)
    ///   5. Performs line-of-sight check using CNWSArea::ClearLineOfSight for sight-based perception
    ///   6. Checks visibility and stealth detection (DoStealthDetection)
    ///   7. Updates perception state and fires OnPerception events for newly detected entities
    /// - Perception range: From creature stats PerceptionRange field (typically 20m sight, 15m hearing)
    /// - Line-of-sight: Uses navigation mesh raycasting (CNWSArea::ClearLineOfSight)
    /// - Stealth detection: Uses DoStealthDetection to check if stealthy creatures are detected
    /// - Perception updates periodically (not every frame) during heartbeat/update loop
    /// - Scripts can query GetLastPerceived, GetObjectSeen, etc. (NWScript engine API)
    /// - Perception fires OnPerception script event on creature when new entities are detected
    /// - Default ranges: From creature stats PerceptionRange field (~20m sight, ~15m hearing for standard creatures)
    /// - Can be modified by effects/feats (perception bonuses)
    ///
    /// Aurora-Specific Features:
    /// - Integration with AuroraAIController for centralized perception tracking
    /// - D20 perception system with line-of-sight checks
    /// - Stealth detection system (DoStealthDetection)
    /// - CNWSArea::ClearLineOfSight for navigation mesh raycasting
    /// - PerceptionList/PerceptionData serialization format (GFF structures)
    /// </remarks>
    public class AuroraPerceptionComponent : BasePerceptionComponent
    {
        public AuroraPerceptionComponent()
        {
            // Default ranges for Aurora creatures
            // Based on nwmain.exe: Default perception ranges from creature stats
            // Typical values: 20m sight, 15m hearing for standard creatures
            SightRange = 20.0f; // Default sight range in meters
            HearingRange = 15.0f; // Default hearing range in meters
        }

        #region IPerceptionComponent Implementation (Aurora-specific overrides)

        /// <summary>
        /// Gets entities that are currently seen.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Uses base class implementation for local tracking.
        /// Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0
        /// Perception state is updated by AuroraAIController during heartbeat/update loop.
        /// </remarks>
        public override IEnumerable<IEntity> GetSeenObjects()
        {
            // Use base class implementation for local tracking
            // AuroraAIController updates perception state via UpdatePerception calls
            return base.GetSeenObjects();
        }

        /// <summary>
        /// Gets entities that are currently heard.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Uses base class implementation for local tracking.
        /// Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0
        /// Perception state is updated by AuroraAIController during heartbeat/update loop.
        /// </remarks>
        public override IEnumerable<IEntity> GetHeardObjects()
        {
            // Use base class implementation for local tracking
            // AuroraAIController updates perception state via UpdatePerception calls
            return base.GetHeardObjects();
        }

        /// <summary>
        /// Checks if a specific entity was seen.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Uses base class implementation for local tracking.
        /// Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0
        /// </remarks>
        public override bool WasSeen(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            // Use base class implementation for local tracking
            return base.WasSeen(entity);
        }

        /// <summary>
        /// Checks if a specific entity was heard.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Uses base class implementation for local tracking.
        /// Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0
        /// </remarks>
        public override bool WasHeard(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            // Use base class implementation for local tracking
            return base.WasHeard(entity);
        }

        #endregion
    }
}


using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Aurora.Components
{
    /// <summary>
    /// Aurora engine-specific implementation of faction component for Neverwinter Nights.
    /// </summary>
    /// <remarks>
    /// Aurora Faction Component:
    /// - Inherits common functionality from BaseFactionComponent
    /// - Implements Aurora-specific faction system using CNWSFaction and CFactionManager
    /// - Based on nwmain.exe faction system
    ///
    /// Aurora-specific details:
    /// - nwmain.exe: Aurora faction system using CNWSFaction and CFactionManager classes
    /// - CNWSFaction constructor @ 0x1404ad3e0 (CNWSFaction::CNWSFaction)
    /// - CFactionManager::GetFaction @ 0x140357900 (gets faction by ID)
    /// - Located via string references: "FactionID" @ 0x140de5930
    /// - "FactionName" @ 0x140dda160, "FactionParentID" @ 0x140dda170
    /// - "FactionGlobal" @ 0x140dda180, "FactionID1" @ 0x140dda190, "FactionID2" @ 0x140dda1a0
    /// - "FactionRep" @ 0x140dda1b0, "FactionList" @ 0x140defe58
    /// - "FACTIONREP" @ 0x140dfc4e0, "Faction: " @ 0x140e421f8
    /// - Error: "Unable to load faction table!" @ 0x140defe38
    /// - "Source faction id invalid" @ 0x140de1500
    /// - Original implementation: FactionId references faction table entry
    /// - Faction relationships stored in faction table (similar to repute.2da but Aurora-specific format)
    /// - Faction reputation values determine hostility (0-10 = hostile, 11-89 = neutral, 90-100 = friendly)
    /// - Personal reputation can override faction reputation for specific entity pairs
    /// - Temporary hostility tracked per-entity (stored in TemporaryHostileTargets HashSet from base class)
    /// - CFactionManager handles complex faction relationships and reputation lookups
    /// - CNWSFaction represents individual faction with member list and reputation matrix
    /// </remarks>
    public class AuroraFactionComponent : BaseFactionComponent
    {
        /// <summary>
        /// Initializes a new instance of the Aurora faction component.
        /// </summary>
        public AuroraFactionComponent()
        {
            FactionId = 0; // Default to neutral/unassigned faction
        }

        /// <summary>
        /// Checks if this entity is hostile to another.
        /// </summary>
        /// <param name="other">The other entity to check hostility against.</param>
        /// <returns>True if hostile, false otherwise.</returns>
        public override bool IsHostile(IEntity other)
        {
            if (other == null || other == Owner)
            {
                return false;
            }

            // Check temporary hostility first (from base class)
            if (TemporaryHostileTargets.Contains(other.ObjectId))
            {
                return true;
            }

            // TODO: PLACEHOLDER - Implement Aurora-specific faction relationship lookup
            // Aurora uses CFactionManager::GetFactionReputation to get reputation between factions
            // Need to integrate with Aurora's faction manager system when implemented
            // For now, fall back to simple faction comparison

            IFactionComponent otherFaction = other.GetComponent<IFactionComponent>();
            if (otherFaction == null)
            {
                return false;
            }

            // Same faction = friendly
            if (FactionId == otherFaction.FactionId)
            {
                return false;
            }

            // Default: different factions are neutral (Aurora-specific logic would check reputation table)
            return false;
        }

        /// <summary>
        /// Checks if this entity is friendly to another.
        /// </summary>
        /// <param name="other">The other entity to check friendliness against.</param>
        /// <returns>True if friendly, false otherwise.</returns>
        public override bool IsFriendly(IEntity other)
        {
            if (other == null)
            {
                return false;
            }

            if (other == Owner)
            {
                return true;
            }

            // Temporarily hostile = not friendly (from base class)
            if (TemporaryHostileTargets.Contains(other.ObjectId))
            {
                return false;
            }

            // TODO: PLACEHOLDER - Implement Aurora-specific faction relationship lookup
            // Aurora uses CFactionManager::GetFactionReputation to get reputation between factions
            // Need to integrate with Aurora's faction manager system when implemented
            // For now, fall back to simple faction comparison

            IFactionComponent otherFaction = other.GetComponent<IFactionComponent>();
            if (otherFaction == null)
            {
                return false;
            }

            // Same faction = friendly
            return FactionId == otherFaction.FactionId;
        }
    }
}


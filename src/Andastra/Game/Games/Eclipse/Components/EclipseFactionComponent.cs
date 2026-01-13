using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Systems;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse engine-specific implementation of faction component for Dragon Age and .
    /// </summary>
    /// <remarks>
    /// Eclipse Faction Component:
    /// - Inherits common functionality from BaseFactionComponent
    /// - Implements Eclipse-specific faction system using EclipseFactionManager
    /// - Based on daorigins.exe and DragonAge2.exe faction systems
    ///
    /// Eclipse-specific details:
    /// - daorigins.exe: Eclipse faction system with IsHostile/IsFriendly checks
    /// - DragonAge2.exe: Enhanced Eclipse faction system with similar structure
    /// - Located via string references:
    ///   - ["IsHostile"] @ (daorigin.exe: 0x00af7904, dragonage2.exe: 0x00bf4e84)
    ///   - ["IsConsideredHostile"] @ (daorigins.exe: 0x00af6fca, dragonage2.exe: 0x00bf2728)
    ///   - ["GroupHostile"] @ (daorigins.exe: 0x00aedc68, dragonage2.exe: 0x00bf8370)
    ///   - ["ShowAsAllyOnMap"] @ (daorigins.exe: 0x00af75c8, dragonage2.exe: 0x00bf4b1c)
    ///   - ["CursorOverHostileNPC"] @ (daorigins.exe: 0x00af97d4, dragonage2.exe: 0x00be9f04)
    ///   - ["CursorOverFriendlyNPC"] @ (daorigins.exe: 0x00af9828, dragonage2.exe: 0x00be9f30)
    /// - Original implementation: Eclipse engines use IsHostile/IsConsideredHostile checks for faction relationships
    ///   - **EclipseFactionManager**: handles complex faction relationships and reputation lookups
    ///   - **Faction relationships**: Similar to Odyssey/Aurora but Eclipse-specific implementation
    ///   - **Personal reputation**: Individual entity overrides (stored per entity pair, overrides faction reputation)
    ///   - **Temporary hostility** tracked per-entity (stored in TemporaryHostileTargets HashSet from base class)
    ///   - **Eclipse-specific**: Uses reputation-based hostility calculation
    ///     - 0-10 = hostile
    ///     - 11-89 = neutral
    ///     - 90-100 = friendly
    /// - **Combat triggers**:
    ///   - Attacking a creature makes attacker hostile to target's faction
    ///   - Temporary hostility set immediately on attack (SetTemporaryHostile)
    ///   - Faction-wide hostility: All members of target's faction become hostile to attacker
    ///   - Hostility can be permanent (persists after combat) or temporary (cleared on combat end)
    ///   - Personal reputation can override faction reputation for specific entity pairs
    /// </remarks>
    public class EclipseFactionComponent : BaseFactionComponent
    {
        private EclipseFactionManager _factionManager;

        /// <summary>
        /// Initializes a new instance of the Eclipse faction component.
        /// </summary>
        public EclipseFactionComponent()
        {
            FactionId = 0; // Default to neutral/unassigned faction
        }

        /// <summary>
        /// Initializes a new instance of the Eclipse faction component with a faction manager.
        /// </summary>
        /// <param name="factionManager">The faction manager to use for reputation lookups.</param>
        public EclipseFactionComponent(EclipseFactionManager factionManager) : this()
        {
            _factionManager = factionManager;
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public override void OnAttach()
        {
            // FactionId is set during entity creation
            base.OnAttach();
        }

        /// <summary>
        /// Checks if this entity is hostile to another.
        /// </summary>
        /// <param name="other">The other entity to check hostility against.</param>
        /// <returns>True if hostile, false otherwise.</returns>
        /// <remarks>
        /// Uses EclipseFactionManager for reputation-based hostility determination.
        /// Based on Eclipse engine:
        ///   - ["CheckHostility"] @ (daorigins.exe: 0x00af7904, dragonage2.exe: 0x00bf4e84)
        /// </remarks>
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

            // Use faction manager if available
            if (_factionManager != null)
            {
                return _factionManager.IsHostile(Owner, other);
            }

            // Fall back to simple faction comparison
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

            // Default: different factions are neutral (Eclipse-specific logic would check hostility flags)
            return false;
        }

        /// <summary>
        /// Checks if this entity is friendly to another.
        /// </summary>
        /// <param name="other">The other entity to check friendliness against.</param>
        /// <returns>True if friendly, false otherwise.</returns>
        /// <remarks>
        /// Based on Eclipse engine:
        ///   - [CheckFriendliness] @ (daorigins.exe: TODO: Find this address, dragonage2.exe: TODO: Find this address)
        /// Uses EclipseFactionManager for reputation-based friendliness determination.
        /// </remarks>
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

            // Use faction manager if available
            if (_factionManager != null)
            {
                return _factionManager.IsFriendly(Owner, other);
            }

            // Fall back to simple faction comparison
            IFactionComponent otherFaction = other.GetComponent<IFactionComponent>();
            if (otherFaction == null)
            {
                return false;
            }

            // Same faction = friendly
            return FactionId == otherFaction.FactionId;
        }

        /// <summary>
        /// Sets temporary hostility toward a target.
        /// </summary>
        /// <param name="target">The target entity.</param>
        /// <param name="hostile">True to set as hostile, false to clear hostility.</param>
        public override void SetTemporaryHostile(IEntity target, bool hostile)
        {
            base.SetTemporaryHostile(target, hostile);

            // Also update faction manager if available
            if (_factionManager != null)
            {
                _factionManager.SetTemporaryHostile(Owner, target, hostile);
            }
        }

        /// <summary>
        /// Sets the faction manager reference.
        /// </summary>
        /// <param name="manager">The faction manager to use for reputation lookups.</param>
        public void SetFactionManager(EclipseFactionManager manager)
        {
            _factionManager = manager;
        }
    }
}


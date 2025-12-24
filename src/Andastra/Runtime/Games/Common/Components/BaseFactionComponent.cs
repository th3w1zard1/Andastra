using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common.Components
{
    /// <summary>
    /// Base implementation of faction component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Faction Component Implementation:
    /// - Common faction properties and methods across all engines
    /// - Handles faction ID, hostility checks, friendly checks, neutral checks, temporary hostility
    /// - Provides base for engine-specific faction component implementations
    /// - Cross-engine analysis: All engines share common faction structure patterns
    /// - Common functionality: FactionId, IsHostile, IsFriendly, IsNeutral, SetTemporaryHostile
    /// - Engine-specific: Faction relationship tables, reputation systems, hostility calculations
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Faction component system with repute.2da table (FUN_005b1b90 @ 0x005b1b90 loads faction data)
    /// - swkotor2.exe: Enhanced faction system with repute.2da table (FUN_005fb0f0 @ 0x005fb0f0 loads faction data)
    /// - nwmain.exe: Aurora faction system using CNWSFaction and CFactionManager (CNWSFaction @ 0x1404ad3e0, GetFaction @ 0x140357900)
    /// - daorigins.exe: Eclipse faction system with IsHostile/IsFriendly checks
    /// - DragonAge2.exe: Enhanced Eclipse faction system
    /// - /: Infinity faction system with BioFaction classes
    ///
    /// Common structure across engines:
    /// - FactionId (int): Faction identifier from faction relationship table (repute.2da, faction.2da, etc.)
    /// - IsHostile (method): Checks if this entity is hostile to another entity
    /// - IsFriendly (method): Checks if this entity is friendly to another entity
    /// - IsNeutral (method): Checks if this entity is neutral to another entity
    /// - SetTemporaryHostile (method): Sets temporary hostility toward a target (overrides faction relationships)
    /// - Temporary hostility: Combat-triggered hostility that can override faction-based relationships
    ///
    /// Common faction relationships across engines:
    /// - Faction relationships stored in 2DA tables (repute.2da for Odyssey, faction.2da for Aurora, etc.)
    /// - Faction reputation values determine hostility (0-10 = hostile, 11-89 = neutral, 90-100 = friendly)
    /// - Temporary hostility can override faction relationships for scripted encounters
    /// - Personal reputation can override faction reputation for specific entity pairs
    /// </remarks>
    [PublicAPI]
    public abstract class BaseFactionComponent : IFactionComponent
    {
        /// <summary>
        /// Set of entity IDs that are temporarily hostile to this entity.
        /// </summary>
        protected readonly HashSet<uint> TemporaryHostileTargets;

        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// The faction ID.
        /// </summary>
        public int FactionId { get; set; }

        /// <summary>
        /// Initializes a new instance of the base faction component.
        /// </summary>
        protected BaseFactionComponent()
        {
            TemporaryHostileTargets = new HashSet<uint>();
            FactionId = 0; // Default to neutral/unassigned faction
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public virtual void OnAttach()
        {
            // Base implementation does nothing - engine-specific implementations can override
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public virtual void OnDetach()
        {
            // Clear temporary hostility when component is detached
            TemporaryHostileTargets.Clear();
        }

        /// <summary>
        /// Checks if this entity is hostile to another.
        /// </summary>
        /// <param name="other">The other entity to check hostility against.</param>
        /// <returns>True if hostile, false otherwise.</returns>
        public abstract bool IsHostile(IEntity other);

        /// <summary>
        /// Checks if this entity is friendly to another.
        /// </summary>
        /// <param name="other">The other entity to check friendliness against.</param>
        /// <returns>True if friendly, false otherwise.</returns>
        public abstract bool IsFriendly(IEntity other);

        /// <summary>
        /// Checks if this entity is neutral to another.
        /// </summary>
        /// <param name="other">The other entity to check neutrality against.</param>
        /// <returns>True if neutral, false otherwise.</returns>
        public virtual bool IsNeutral(IEntity other)
        {
            // Default implementation: not hostile and not friendly = neutral
            return !IsHostile(other) && !IsFriendly(other);
        }

        /// <summary>
        /// Sets temporary hostility toward a target.
        /// </summary>
        /// <param name="target">The target entity.</param>
        /// <param name="hostile">True to set as hostile, false to clear hostility.</param>
        public virtual void SetTemporaryHostile(IEntity target, bool hostile)
        {
            if (target == null)
            {
                return;
            }

            if (hostile)
            {
                TemporaryHostileTargets.Add(target.ObjectId);
            }
            else
            {
                TemporaryHostileTargets.Remove(target.ObjectId);
            }
        }

        /// <summary>
        /// Clears all temporary hostility.
        /// </summary>
        public virtual void ClearTemporaryHostility()
        {
            TemporaryHostileTargets.Clear();
        }

        /// <summary>
        /// Gets the number of temporarily hostile targets.
        /// </summary>
        public virtual int TemporaryHostileCount
        {
            get { return TemporaryHostileTargets.Count; }
        }

        /// <summary>
        /// Checks if a specific target is temporarily hostile.
        /// </summary>
        /// <param name="target">The target entity to check.</param>
        /// <returns>True if temporarily hostile, false otherwise.</returns>
        public virtual bool IsTemporarilyHostileTo(IEntity target)
        {
            return target != null && TemporaryHostileTargets.Contains(target.ObjectId);
        }
    }
}


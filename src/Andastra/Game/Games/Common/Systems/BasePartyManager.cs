using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common.Systems
{
    /// <summary>
    /// Abstract base class for party managers across all engines.
    /// </summary>
    /// <remarks>
    /// Base Party Manager:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyPartyManager, AuroraPartyManager, EclipsePartyManager)
    /// - Common: Party leader, active party members, party change events
    /// </remarks>
    public abstract class BasePartyManager
    {
        protected readonly IWorld _world;
        protected readonly List<IEntity> _activeParty;

        /// <summary>
        /// Event fired when party composition changes.
        /// </summary>
        public event EventHandler<PartyChangedEventArgs> OnPartyChanged;

        /// <summary>
        /// Event fired when the party leader changes.
        /// </summary>
        public event EventHandler<PartyChangedEventArgs> OnLeaderChanged;

        protected BasePartyManager(IWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            _world = world;
            _activeParty = new List<IEntity>();
        }

        /// <summary>
        /// Gets the current party leader (typically the PC).
        /// </summary>
        [CanBeNull]
        public IEntity Leader
        {
            get { return _activeParty.Count > 0 ? _activeParty[0] : null; }
        }

        /// <summary>
        /// Gets the current active party members.
        /// </summary>
        public IReadOnlyList<IEntity> ActiveParty
        {
            get { return _activeParty; }
        }

        /// <summary>
        /// Gets the maximum active party size (engine-specific).
        /// </summary>
        public abstract int MaxActivePartySize { get; }

        /// <summary>
        /// Protected accessor for active party list (for subclasses).
        /// </summary>
        protected List<IEntity> ActivePartyList
        {
            get { return _activeParty; }
        }

        /// <summary>
        /// Adds a member to the active party.
        /// </summary>
        public abstract bool AddMember(IEntity member, int slot = -1);

        /// <summary>
        /// Removes a member from the active party.
        /// </summary>
        public abstract bool RemoveMember(IEntity member);

        /// <summary>
        /// Sets the party leader.
        /// </summary>
        public abstract void SetLeader(IEntity leader);

        /// <summary>
        /// Fires the OnPartyChanged event.
        /// </summary>
        protected void FireOnPartyChanged(PartyChangedEventArgs args)
        {
            OnPartyChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Fires the OnLeaderChanged event.
        /// </summary>
        protected void FireOnLeaderChanged(PartyChangedEventArgs args)
        {
            OnLeaderChanged?.Invoke(this, args);
        }
    }

    /// <summary>
    /// Party change event arguments (common across all engines).
    /// </summary>
    public class PartyChangedEventArgs : EventArgs
    {
        public IEntity Member { get; set; }
        public int Slot { get; set; }
        public bool Added { get; set; }
    }
}


using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common.Dialogue
{
    /// <summary>
    /// Abstract base class for conversation context across all engines.
    /// </summary>
    /// <remarks>
    /// Base Conversation Context:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyConversationContext, AuroraConversationContext, EclipseConversationContext)
    /// - Common: Owner, PC, World, participant registration, speaker/listener lookup
    /// </remarks>
    public abstract class BaseConversationContext
    {
        protected readonly Dictionary<string, IEntity> _participants;
        protected readonly IWorld _world;

        /// <summary>
        /// Protected accessor for participants dictionary (for subclasses).
        /// </summary>
        protected Dictionary<string, IEntity> Participants
        {
            get { return _participants; }
        }

        protected BaseConversationContext(IEntity owner, IEntity pc, IWorld world)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }
            if (pc == null)
            {
                throw new ArgumentNullException(nameof(pc));
            }
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            Owner = owner;
            PC = pc;
            _world = world;
            _participants = new Dictionary<string, IEntity>(StringComparer.OrdinalIgnoreCase);

            // Register owner and PC by their tags
            if (!string.IsNullOrEmpty(owner.Tag))
            {
                _participants[owner.Tag] = owner;
            }
            if (!string.IsNullOrEmpty(pc.Tag))
            {
                _participants[pc.Tag] = pc;
            }

            // By default, PC speaker is the PC
            PCSpeaker = pc;
        }

        /// <summary>
        /// The object that owns/initiated the dialogue.
        /// </summary>
        public IEntity Owner { get; protected set; }

        /// <summary>
        /// The player character.
        /// </summary>
        public IEntity PC { get; protected set; }

        /// <summary>
        /// The specific party member speaking for the player.
        /// </summary>
        public IEntity PCSpeaker { get; set; }

        /// <summary>
        /// The game world for entity lookups.
        /// </summary>
        public IWorld World
        {
            get { return _world; }
        }

        /// <summary>
        /// Registers a participant by tag.
        /// </summary>
        public void RegisterParticipant(string tag, IEntity entity)
        {
            if (!string.IsNullOrEmpty(tag) && entity != null)
            {
                _participants[tag] = entity;
            }
        }

        /// <summary>
        /// Finds a speaker by tag.
        /// </summary>
        [CanBeNull]
        public abstract IEntity FindSpeaker(string tag);

        /// <summary>
        /// Finds a listener by tag.
        /// </summary>
        [CanBeNull]
        public abstract IEntity FindListener(string tag);

        /// <summary>
        /// Gets the OBJECT_SELF for script execution (the owner).
        /// </summary>
        public IEntity GetObjectSelf()
        {
            return Owner;
        }

        /// <summary>
        /// Gets the PC speaker for script execution.
        /// </summary>
        public IEntity GetPCSpeaker()
        {
            return PCSpeaker;
        }

        /// <summary>
        /// Gets all registered participants.
        /// </summary>
        public IEnumerable<KeyValuePair<string, IEntity>> GetParticipants()
        {
            return _participants;
        }
    }
}


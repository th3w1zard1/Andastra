using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Dialogue
{
    /// <summary>
    /// Provides context for a dialogue conversation.
    /// </summary>
    /// <remarks>
    /// Conversation participants:
    /// - Owner: The object that owns the dialogue (NPC, placeable, etc.)
    /// - PC: The player character
    /// - PCSpeaker: The specific party member speaking (may differ from PC)
    /// - Additional participants identified by tag
    /// </remarks>
    public class ConversationContext
    {
        private readonly Dictionary<string, IEntity> _participants;

        public ConversationContext(IEntity owner, IEntity pc, IWorld world)
        {
            Owner = owner ?? throw new ArgumentNullException("owner");
            PC = pc ?? throw new ArgumentNullException("pc");
            World = world ?? throw new ArgumentNullException("world");
            
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
        public IEntity Owner { get; private set; }

        /// <summary>
        /// The player character.
        /// </summary>
        public IEntity PC { get; private set; }

        /// <summary>
        /// The specific party member speaking for the player.
        /// </summary>
        public IEntity PCSpeaker { get; set; }

        /// <summary>
        /// The game world for entity lookups.
        /// </summary>
        public IWorld World { get; private set; }

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
        public IEntity FindSpeaker(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return Owner;
            }

            // Check registered participants first
            IEntity entity;
            if (_participants.TryGetValue(tag, out entity))
            {
                return entity;
            }

            // Try to find in world by tag
            if (World != null)
            {
                var module = World.CurrentModule;
                if (module != null)
                {
                    foreach (var area in module.Areas)
                    {
                        var found = area.GetObjectByTag(tag);
                        if (found != null)
                        {
                            // Cache for future lookups
                            _participants[tag] = found;
                            return found;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a listener by tag.
        /// </summary>
        [CanBeNull]
        public IEntity FindListener(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return PC;
            }

            // Same lookup as speaker
            return FindSpeaker(tag);
        }

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

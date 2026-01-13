using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common.Dialogue;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey.Dialogue
{
    /// <summary>
    /// Provides context for a dialogue conversation in Odyssey engine (DLG-based).
    /// </summary>
    /// <remarks>
    /// Conversation Context (Odyssey-specific):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ExecuteDialogue @ 0x005e9920 (dialogue execution)
    /// - Located via string references: "Conversation" @ 0x007c1abc, "ConversationType" @ 0x007c38b0, "GetPCSpeaker" @ 0x007c1e98
    /// - Error: "Error: dialogue can't find object '%s'!" @ 0x007c3730 (dialogue object lookup failure)
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe): CNWSDialog class, "ScriptDialogue" @ 0x140dddb80, "BeginConversation" @ ExecuteCommandBeginConversation, "EndConversation" @ 0x140de6f70
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): "Conversation" class, "ShowConversationGUIMessage", "Conversation.HandleResponseSelection" - UnrealScript-based dialogue system
    /// - Inheritance: Base class BaseConversationContext (Runtime.Games.Common) - abstract conversation context, Odyssey override (Runtime.Games.Odyssey) - DLG-based dialogue
    /// - Original implementation: Manages conversation participants and speaker lookup for DLG-based dialogue system
    /// - Conversation participants: Owner (OBJECT_SELF), PC, PCSpeaker (GetPCSpeaker()), additional participants by tag (Speaker/Listener fields in DLG entries)
    /// </remarks>
    public class ConversationContext : BaseConversationContext
    {
        public ConversationContext(IEntity owner, IEntity pc, IWorld world)
            : base(owner, pc, world)
        {
        }

        /// <summary>
        /// Finds a speaker by tag (Odyssey-specific: searches module areas).
        /// </summary>
        [CanBeNull]
        public override IEntity FindSpeaker(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return Owner;
            }

            // Check registered participants first
            IEntity entity;
            if (Participants.TryGetValue(tag, out entity))
            {
                return entity;
            }

            // Try to find in world by tag (Odyssey-specific: searches module areas)
            if (World != null)
            {
                Runtime.Core.Interfaces.IModule module = World.CurrentModule;
                if (module != null)
                {
                    foreach (IArea area in module.Areas)
                    {
                        IEntity found = area.GetObjectByTag(tag);
                        if (found != null)
                        {
                            // Cache for future lookups
                            Participants[tag] = found;
                            return found;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a listener by tag (Odyssey-specific: defaults to PC).
        /// </summary>
        [CanBeNull]
        public override IEntity FindListener(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return PC;
            }

            // Same lookup as speaker
            return FindSpeaker(tag);
        }
    }
}

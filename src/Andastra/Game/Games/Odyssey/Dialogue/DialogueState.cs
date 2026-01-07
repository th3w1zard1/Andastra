using System.Collections.Generic;
using Andastra.Parsing.Resource.Generics.DLG;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common.Dialogue;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Odyssey.Dialogue
{
    /// <summary>
    /// Tracks the current state of a dialogue conversation in Odyssey engine (DLG-based).
    /// </summary>
    /// <remarks>
    /// Dialogue State (Odyssey-specific):
    /// - Based on swkotor2.exe: ExecuteDialogue @ 0x005e9920 (dialogue execution)
    /// - Located via string references: "Conversation" @ 0x007c1abc, "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0
    /// - Error: "Error: dialogue can't find object '%s'!" @ 0x007c3730, "CONVERSATION ERROR: Last Conversation Node Contains Either an END NODE or CONTINUE NODE" @ 0x007c3768
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe): CNWSDialog class, "ScriptDialogue" @ 0x140dddb80, "EndConversation" @ 0x140de6f70, RunEndConversationScript
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): "Conversation" class, "Conversation.HandleResponseSelection", "Conversation.OnNPCLineFinished" - UnrealScript-based dialogue system
    /// - Inheritance: Base class BaseDialogueState (Runtime.Games.Common) - abstract dialogue state, Odyssey override (Runtime.Games.Odyssey) - DLG-based dialogue state
    /// - Original implementation: Tracks current conversation state and node traversal for DLG-based dialogue system
    /// - Dialogue state: StartingList -> DLGEntry (NPC) -> DLGReply options -> DLGEntry (next NPC) -> repeat until no more links or aborted
    /// - State tracks: current node, available replies, voice-over playback status, dialogue history for conditional checks
    /// </remarks>
    public class DialogueState : BaseDialogueState
    {
        public DialogueState(DLG dialog, ConversationContext context)
            : base(context)
        {
            Dialog = dialog;
            CurrentNode = null;
            CurrentEntry = null;
            AvailableReplies = new List<DLGReply>();
        }

        /// <summary>
        /// The dialogue tree being traversed (Odyssey-specific: DLG format).
        /// </summary>
        public DLG Dialog { get; private set; }

        /// <summary>
        /// The conversation context (participants) - Odyssey-specific type.
        /// </summary>
        public new ConversationContext Context
        {
            get { return (ConversationContext)base.Context; }
        }

        /// <summary>
        /// The current node being displayed.
        /// </summary>
        [CanBeNull]
        public DLGNode CurrentNode { get; set; }

        /// <summary>
        /// The current NPC entry being displayed.
        /// </summary>
        [CanBeNull]
        public DLGEntry CurrentEntry { get; set; }

        /// <summary>
        /// Available player replies for the current entry.
        /// </summary>
        public List<DLGReply> AvailableReplies { get; private set; }

        // IsActive, IsPaused, WaitingForVoiceover, TimeRemaining inherited from BaseDialogueState

        /// <summary>
        /// Whether the current node can be skipped.
        /// </summary>
        public bool CanSkip
        {
            get
            {
                if (CurrentNode == null)
                {
                    return true;
                }
                return !CurrentNode.Unskippable;
            }
        }

        /// <summary>
        /// History of traversed nodes (Odyssey-specific: DLGNode type).
        /// </summary>
        public new IReadOnlyList<DLGNode> History
        {
            get
            {
                var history = new List<DLGNode>();
                foreach (object node in HistoryList)
                {
                    if (node is DLGNode dlgNode)
                    {
                        history.Add(dlgNode);
                    }
                }
                return history;
            }
        }

        /// <summary>
        /// Pushes a node to the history (Odyssey-specific: DLGNode type).
        /// </summary>
        public void PushHistory(DLGNode node)
        {
            base.PushHistory(node);
        }

        /// <summary>
        /// Clears the available replies list.
        /// </summary>
        public void ClearReplies()
        {
            AvailableReplies.Clear();
        }

        /// <summary>
        /// Adds a reply to the available list.
        /// </summary>
        public void AddReply(DLGReply reply)
        {
            if (reply != null)
            {
                AvailableReplies.Add(reply);
            }
        }

        /// <summary>
        /// Gets the speaker entity for the current entry (Odyssey-specific: uses DLGEntry.Speaker field).
        /// </summary>
        [CanBeNull]
        public override IEntity GetCurrentSpeaker()
        {
            if (CurrentEntry == null)
            {
                return Context.Owner;
            }

            string speakerTag = CurrentEntry.Speaker;
            if (string.IsNullOrEmpty(speakerTag))
            {
                return Context.Owner;
            }

            // Find speaker by tag
            return Context.FindSpeaker(speakerTag);
        }

        /// <summary>
        /// Gets the listener entity for the current entry (Odyssey-specific: uses DLGEntry.Listener field).
        /// </summary>
        [CanBeNull]
        public override IEntity GetCurrentListener()
        {
            if (CurrentEntry == null)
            {
                return Context.PC;
            }

            string listenerTag = CurrentEntry.Listener;
            if (string.IsNullOrEmpty(listenerTag))
            {
                return Context.PC;
            }

            return Context.FindListener(listenerTag);
        }

        /// <summary>
        /// Resets the state for a new conversation.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            CurrentNode = null;
            CurrentEntry = null;
            AvailableReplies.Clear();
        }
    }
}

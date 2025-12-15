using System.Collections.Generic;
using CSharpKOTOR.Resource.Generics.DLG;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Dialogue
{
    /// <summary>
    /// Tracks the current state of a dialogue conversation.
    /// </summary>
    /// <remarks>
    /// Dialogue State:
    /// - Based on swkotor2.exe dialogue system
    /// - Located via string references: "Conversation" @ 0x007c1abc, "ConversationType" @ 0x007c38e0
    /// - "EndConversation" @ 0x007c38e0, "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0
    /// - "CONVERSATION ERROR: Last Conversation Node Contains Either an END NODE or CONTINUE NODE.  Please contact a Designer!" @ 0x007c3768
    /// - Dialogue script hooks: "k_level_dlg" @ 0x007c3f88, "000_Level_Dlg_Fired" @ 0x007c3f94 (level-up dialogue)
    /// - Error: "Error: dialogue can't find object '%s'!" @ 0x007c3730 (dialogue object lookup failure)
    /// - Original implementation: Tracks current conversation state and node traversal
    /// - Dialogue state progresses through:
    ///   1. StartingList - Initial entry selection
    ///   2. DLGEntry (NPC speaks) -> DLGReply options shown
    ///   3. Player selects DLGReply -> DLGEntry (next NPC line)
    ///   4. Repeat until no more links or aborted
    /// - State tracks current node, available replies, voice-over playback status
    /// - Dialogue history maintained for conditional checks and script evaluation
    /// - WaitingForVoiceover: Dialogue waits for voice-over completion before advancing
    /// - CanSkip: Dialogue nodes can be skipped unless Unskippable flag is set
    /// </remarks>
    public class DialogueState
    {
        private readonly List<DLGNode> _history;

        public DialogueState(DLG dialog, ConversationContext context)
        {
            Dialog = dialog;
            Context = context;
            _history = new List<DLGNode>();
            CurrentNode = null;
            CurrentEntry = null;
            AvailableReplies = new List<DLGReply>();
            IsActive = true;
            IsPaused = false;
            WaitingForVoiceover = false;
        }

        /// <summary>
        /// The dialogue tree being traversed.
        /// </summary>
        public DLG Dialog { get; private set; }

        /// <summary>
        /// The conversation context (participants).
        /// </summary>
        public ConversationContext Context { get; private set; }

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

        /// <summary>
        /// Whether the conversation is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether the conversation is paused (e.g., during cutscene).
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Whether we're waiting for a voiceover to complete.
        /// </summary>
        public bool WaitingForVoiceover { get; set; }

        /// <summary>
        /// Time remaining on current node display (for auto-advance).
        /// </summary>
        public float TimeRemaining { get; set; }

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
        /// History of traversed nodes.
        /// </summary>
        public IReadOnlyList<DLGNode> History
        {
            get { return _history; }
        }

        /// <summary>
        /// Pushes a node to the history.
        /// </summary>
        public void PushHistory(DLGNode node)
        {
            if (node != null)
            {
                _history.Add(node);
            }
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
        /// Gets the speaker entity for the current entry.
        /// </summary>
        [CanBeNull]
        public IEntity GetCurrentSpeaker()
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
        /// Gets the listener entity for the current entry.
        /// </summary>
        [CanBeNull]
        public IEntity GetCurrentListener()
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
        public void Reset()
        {
            _history.Clear();
            CurrentNode = null;
            CurrentEntry = null;
            AvailableReplies.Clear();
            IsActive = true;
            IsPaused = false;
            WaitingForVoiceover = false;
            TimeRemaining = 0f;
        }
    }
}

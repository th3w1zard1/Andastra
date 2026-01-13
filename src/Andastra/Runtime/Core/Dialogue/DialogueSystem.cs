using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Dialogue
{
    /// <summary>
    /// State of a dialogue conversation.
    /// </summary>
    public enum DialogueState
    {
        /// <summary>
        /// No active conversation.
        /// </summary>
        Inactive,

        /// <summary>
        /// NPC is speaking (VO playing or text displayed).
        /// </summary>
        Speaking,

        /// <summary>
        /// Waiting for player to select a reply.
        /// </summary>
        WaitingForReply,

        /// <summary>
        /// Conversation is paused (via ActionPauseConversation).
        /// </summary>
        Paused,

        /// <summary>
        /// Conversation ending (fade out, cleanup).
        /// </summary>
        Ending
    }

    /// <summary>
    /// Manages dialogue conversations at runtime.
    /// Handles DLG graph traversal, conditional checks, script execution,
    /// VO playback, and lipsync.
    /// </summary>
    /// <remarks>
    /// Dialogue System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) dialogue system
    /// - ExecuteDialogue @ 0x005e9920 - Main dialogue execution function (located via "Error: dialogue can't find object '%s'!" @ 0x007c3730)
    /// - LoadScriptHooks @ 0x0050c510 - Loads script hook references including ScriptDialogue/ScriptEndDialogue (located via "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0)
    /// - SaveEntityState @ 0x005226d0 - Saves entity state including ScriptDialogue/ScriptEndDialogue (located via "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0)
    /// - Located via string references: "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4, "OnEndDialogue" @ 0x007c1f60
    /// - "Conversation" @ 0x007c1abc, "ConversationType" @ 0x007c38e0, "EndConversation" @ 0x007c38e0
    /// - "Conversation File: " @ 0x007cb1ac (conversation file debug message)
    /// - Dialogue script hooks: "k_level_dlg" @ 0x007c3f88, "000_Level_Dlg_Fired" @ 0x007c3f94 (level-up dialogue)
    /// - Dialogue examples: "k_hen_dialogue01" @ 0x007bf548 (example dialogue ResRef)
    /// - Error messages:
    ///   - "Error: dialogue can't find object '%s'!" @ 0x007c3730 (dialogue object lookup failure)
    ///   - "CONVERSATION ERROR: Last Conversation Node Contains Either an END NODE or CONTINUE NODE.  Please contact a Designer!" @ 0x007c3768
    /// - DLG file format: GFF with "DLG " signature containing dialogue tree
    /// - Original implementation: Parses DLG GFF structure, evaluates condition scripts, executes entry/reply scripts
    /// - Dialogue messages stored in PARTYTABLE: PT_DLG_MSG_LIST @ 0x007c1650 (dialogue message history)
    ///   - PT_DLG_MSG_SPKR @ 0x007c1640: Speaker name
    ///   - PT_DLG_MSG_MSG @ 0x007c1630: Message text
    ///
    /// Dialogue Flow:
    /// 1. StartConversation - Load DLG, find first valid entry (StartingList)
    /// 2. EnterNode - Execute entry script (Script1), play VO, gather replies
    /// 3. WaitForReply - Show player choices (or auto-advance for single [Continue])
    /// 4. SelectReply - Execute reply script, find next valid entry (EndConversation or next node)
    /// 5. Repeat 2-4 until no more entries, then EndConversation (fires OnEnd script)
    /// </remarks>
    public class DialogueSystem
    {
        private readonly IWorld _world;
        private readonly IDialogueLoader _dialogueLoader;
        private readonly IScriptExecutor _scriptExecutor;
        private readonly IVoicePlayer _voicePlayer;
        private readonly ILipSyncController _lipSyncController;

        private RuntimeDialogue _currentDialogue;
        private DialogueNode _currentNode;
        private IEntity _owner;      // NPC being talked to
        private IEntity _pc;         // Player character
        private List<DialogueReply> _availableReplies;
        private float _nodeTimer;
        private bool _waitingForVO;
        private bool _autoAdvanceEnabled;

        /// <summary>
        /// Current dialogue state.
        /// </summary>
        public DialogueState State { get; private set; }

        /// <summary>
        /// The current dialogue owner (NPC).
        /// </summary>
        public IEntity Owner { get { return _owner; } }

        /// <summary>
        /// The current player in the conversation.
        /// </summary>
        public IEntity Player { get { return _pc; } }

        /// <summary>
        /// Available replies for the current node.
        /// </summary>
        public IReadOnlyList<DialogueReply> AvailableReplies
        {
            get { return _availableReplies ?? new List<DialogueReply>(); }
        }

        /// <summary>
        /// Whether the dialogue can be skipped.
        /// </summary>
        public bool IsSkippable
        {
            get { return _currentDialogue != null && _currentDialogue.Skippable; }
        }

        /// <summary>
        /// Event fired when dialogue text should be displayed.
        /// </summary>
        public event Action<IEntity, string> OnDialogueText;

        /// <summary>
        /// Event fired when player replies are available.
        /// </summary>
        public event Action<IReadOnlyList<DialogueReply>> OnRepliesAvailable;

        /// <summary>
        /// Event fired when the conversation ends.
        /// </summary>
        public event Action OnConversationEnded;

        /// <summary>
        /// Event fired when the camera should focus on a speaker.
        /// </summary>
        public event Action<IEntity, IEntity> OnCameraFocus;

        public DialogueSystem(
            IWorld world,
            IDialogueLoader dialogueLoader,
            IScriptExecutor scriptExecutor,
            IVoicePlayer voicePlayer = null,
            ILipSyncController lipSyncController = null)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _dialogueLoader = dialogueLoader ?? throw new ArgumentNullException("dialogueLoader");
            _scriptExecutor = scriptExecutor ?? throw new ArgumentNullException("scriptExecutor");
            _voicePlayer = voicePlayer;
            _lipSyncController = lipSyncController;

            State = DialogueState.Inactive;
            _availableReplies = new List<DialogueReply>();
            _autoAdvanceEnabled = true;
        }

        /// <summary>
        /// Starts a conversation with an NPC.
        /// </summary>
        /// <param name="owner">The NPC owning the conversation.</param>
        /// <param name="initiator">The player initiating the conversation.</param>
        /// <param name="dlgResRef">The dialogue resource reference.</param>
        /// <returns>True if conversation started successfully.</returns>
        public bool StartConversation(IEntity owner, IEntity initiator, string dlgResRef)
        {
            if (State != DialogueState.Inactive)
            {
                return false;
            }

            if (owner == null || initiator == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(dlgResRef))
            {
                return false;
            }

            // Load the dialogue
            _currentDialogue = _dialogueLoader.LoadDialogue(dlgResRef);
            if (_currentDialogue == null)
            {
                return false;
            }

            _owner = owner;
            _pc = initiator;
            _availableReplies = new List<DialogueReply>();

            // Fire OnConversation script event
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE fires when conversation begins
            // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4 (0x7), "ScriptDialogue" @ 0x007bee40
            // Original implementation: OnConversation script fires on owner entity (NPC) when conversation starts
            if (_world != null && _world.EventBus != null)
            {
                _world.EventBus.FireScriptEvent(owner, Andastra.Runtime.Core.Enums.ScriptEvent.OnConversation, initiator);
            }

            // Find first valid entry point
            foreach (int starterIndex in _currentDialogue.StarterIndices)
            {
                DialogueEntry entry = _currentDialogue.GetEntry(starterIndex);
                if (entry != null && EvaluateCondition(entry.ConditionalScript))
                {
                    EnterNode(entry);
                    return true;
                }
            }

            // No valid entries - conversation fails to start
            EndConversation();
            return false;
        }

        /// <summary>
        /// Enters a dialogue node (entry or reply that leads to an entry).
        /// </summary>
        private void EnterNode(DialogueEntry entry)
        {
            _currentNode = entry;
            State = DialogueState.Speaking;
            _waitingForVO = false;

            // Execute entry script (Script1)
            if (!string.IsNullOrEmpty(entry.Script1))
            {
                _scriptExecutor.ExecuteScript(entry.Script1, _owner, _pc);
            }

            // Get text from the entry
            string text = entry.Text ?? string.Empty;

            // Play voice-over if available
            if (!string.IsNullOrEmpty(entry.VoiceResRef) && _voicePlayer != null)
            {
                _waitingForVO = true;
                _voicePlayer.Play(entry.VoiceResRef, _owner, OnVOComplete);

                // Start lipsync if available
                if (_lipSyncController != null && !string.IsNullOrEmpty(entry.VoiceResRef))
                {
                    _lipSyncController.Start(_owner, entry.VoiceResRef);
                }
            }
            else
            {
                // No VO - set a timer based on text length
                _nodeTimer = CalculateTextDisplayTime(text);
            }

            // Notify UI of dialogue text
            OnDialogueText?.Invoke(_owner, text);

            // Set camera focus on speaker
            OnCameraFocus?.Invoke(_owner, _pc);

            // Gather available replies
            GatherReplies(entry);

            // Execute Script2 after gathering replies
            if (!string.IsNullOrEmpty(entry.Script2))
            {
                _scriptExecutor.ExecuteScript(entry.Script2, _owner, _pc);
            }
        }

        /// <summary>
        /// Gathers available replies for the current entry.
        /// </summary>
        private void GatherReplies(DialogueEntry entry)
        {
            _availableReplies = new List<DialogueReply>();

            foreach (int linkIndex in entry.ReplyLinks)
            {
                DialogueReply reply = _currentDialogue.GetReply(linkIndex);
                if (reply != null && EvaluateCondition(reply.ConditionalScript))
                {
                    _availableReplies.Add(reply);
                }
            }
        }

        /// <summary>
        /// Called when voice-over playback completes.
        /// </summary>
        private void OnVOComplete()
        {
            _waitingForVO = false;

            if (_lipSyncController != null)
            {
                _lipSyncController.Stop();
            }
        }

        /// <summary>
        /// Selects a player reply.
        /// </summary>
        /// <param name="index">Index into the available replies list.</param>
        public void SelectReply(int index)
        {
            if (State != DialogueState.WaitingForReply)
            {
                return;
            }

            if (index < 0 || index >= _availableReplies.Count)
            {
                return;
            }

            DialogueReply reply = _availableReplies[index];

            // Execute reply script
            if (!string.IsNullOrEmpty(reply.Script1))
            {
                _scriptExecutor.ExecuteScript(reply.Script1, _pc, _owner);
            }

            // Notify UI of player's reply text
            OnDialogueText?.Invoke(_pc, reply.Text);

            // Find next entry
            foreach (int entryIndex in reply.EntryLinks)
            {
                DialogueEntry entry = _currentDialogue.GetEntry(entryIndex);
                if (entry != null && EvaluateCondition(entry.ConditionalScript))
                {
                    EnterNode(entry);
                    return;
                }
            }

            // No more entries - conversation ends
            EndConversation();
        }

        /// <summary>
        /// Skips the current dialogue node (if skippable).
        /// </summary>
        public void SkipNode()
        {
            if (State != DialogueState.Speaking)
            {
                return;
            }

            if (!IsSkippable)
            {
                return;
            }

            // Stop VO
            if (_waitingForVO && _voicePlayer != null)
            {
                _voicePlayer.Stop();
            }

            _waitingForVO = false;
            _nodeTimer = 0;

            if (_lipSyncController != null)
            {
                _lipSyncController.Stop();
            }
        }

        /// <summary>
        /// Pauses the conversation.
        /// </summary>
        public void PauseConversation()
        {
            if (State == DialogueState.Speaking || State == DialogueState.WaitingForReply)
            {
                State = DialogueState.Paused;
            }
        }

        /// <summary>
        /// Resumes a paused conversation.
        /// </summary>
        public void ResumeConversation()
        {
            if (State == DialogueState.Paused)
            {
                if (_availableReplies != null && _availableReplies.Count > 0)
                {
                    State = DialogueState.WaitingForReply;
                }
                else
                {
                    State = DialogueState.Speaking;
                }
            }
        }

        /// <summary>
        /// Ends the conversation.
        /// </summary>
        public void EndConversation()
        {
            if (State == DialogueState.Inactive)
            {
                return;
            }

            State = DialogueState.Ending;

            // Fire OnEndDialogue script event
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ScriptEndDialogue fires when conversation ends
            // Located via string references: "ScriptEndDialogue" @ 0x007bede0
            // Original implementation: OnEndDialogue script fires on owner entity (NPC) when conversation ends
            if (_world != null && _world.EventBus != null && _owner != null)
            {
                _world.EventBus.FireScriptEvent(_owner, Andastra.Runtime.Core.Enums.ScriptEvent.OnEndDialogue, _pc);
            }

            // Execute OnEnd script
            if (_currentDialogue != null && !string.IsNullOrEmpty(_currentDialogue.OnEndScript))
            {
                _scriptExecutor.ExecuteScript(_currentDialogue.OnEndScript, _owner, _pc);
            }

            // Stop any playing VO
            _voicePlayer?.Stop();

            _lipSyncController?.Stop();

            // Cleanup
            _currentDialogue = null;
            _currentNode = null;
            _owner = null;
            _pc = null;
            _availableReplies = new List<DialogueReply>();

            State = DialogueState.Inactive;

            OnConversationEnded?.Invoke();
        }

        /// <summary>
        /// Aborts the conversation (runs OnAbort script instead of OnEnd).
        /// </summary>
        public void AbortConversation()
        {
            if (State == DialogueState.Inactive)
            {
                return;
            }

            State = DialogueState.Ending;

            // Execute OnAbort script
            if (_currentDialogue != null && !string.IsNullOrEmpty(_currentDialogue.OnAbortScript))
            {
                _scriptExecutor.ExecuteScript(_currentDialogue.OnAbortScript, _owner, _pc);
            }

            // Stop any playing VO
            _voicePlayer?.Stop();

            _lipSyncController?.Stop();

            // Cleanup
            _currentDialogue = null;
            _currentNode = null;
            _owner = null;
            _pc = null;
            _availableReplies = new List<DialogueReply>();

            State = DialogueState.Inactive;

            OnConversationEnded?.Invoke();
        }

        /// <summary>
        /// Updates the dialogue system each frame.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds.</param>
        public void Update(float deltaTime)
        {
            if (State == DialogueState.Inactive || State == DialogueState.Paused)
            {
                return;
            }

            // Update lipsync
            if (_lipSyncController != null && State == DialogueState.Speaking)
            {
                _lipSyncController.Update(deltaTime);
            }

            if (State == DialogueState.Speaking)
            {
                if (!_waitingForVO)
                {
                    // Text-only node - wait for timer
                    _nodeTimer -= deltaTime;
                    if (_nodeTimer <= 0)
                    {
                        TransitionToReplies();
                    }
                }
            }
        }

        /// <summary>
        /// Transitions from speaking to waiting for replies.
        /// </summary>
        private void TransitionToReplies()
        {
            if (_availableReplies.Count == 0)
            {
                EndConversation();
                return;
            }

            // Check for auto-advance (single [Continue] style reply)
            if (_autoAdvanceEnabled && _availableReplies.Count == 1 && IsAutoAdvanceReply(_availableReplies[0]))
            {
                SelectReply(0);
                return;
            }

            State = DialogueState.WaitingForReply;

            if (OnRepliesAvailable != null)
            {
                OnRepliesAvailable(_availableReplies);
            }
        }

        /// <summary>
        /// Evaluates a conditional script.
        /// </summary>
        /// <param name="scriptResRef">The script resource reference.</param>
        /// <returns>True if the condition passes (or no script).</returns>
        private bool EvaluateCondition(string scriptResRef)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return true;
            }

            int result = _scriptExecutor.ExecuteScript(scriptResRef, _owner, _pc);
            return result != 0; // Non-zero = TRUE
        }

        /// <summary>
        /// Checks if a reply should auto-advance (empty or "[Continue]" text).
        /// </summary>
        private bool IsAutoAdvanceReply(DialogueReply reply)
        {
            if (string.IsNullOrEmpty(reply.Text))
            {
                return true;
            }

            string text = reply.Text.Trim().ToLowerInvariant();
            return text == "[continue]" || text == "[end]";
        }

        /// <summary>
        /// Calculates display time for text without VO.
        /// </summary>
        private float CalculateTextDisplayTime(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1.0f;
            }

            // Approximately 150 words per minute reading speed
            int wordCount = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            float baseTime = wordCount / 2.5f; // ~2.5 words per second

            // Minimum 2 seconds, maximum 10 seconds
            return Math.Max(2.0f, Math.Min(10.0f, baseTime));
        }
    }
}

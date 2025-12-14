using System;
using System.Collections.Generic;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using Odyssey.Core.Interfaces;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Kotor.GameSystems
{
    /// <summary>
    /// Handles dialogue/conversation playback.
    /// </summary>
    /// <remarks>
    /// DLG files are GFF structures containing conversation trees.
    /// Entries are NPC lines, Replies are PC responses.
    /// Each node can have scripts for conditions and actions.
    /// </remarks>
    public class DialogueSystem
    {
        private readonly IWorld _world;
        private readonly INcsVm _scriptVm;

        private DialogueData _currentDialogue;
        private DialogueNode _currentNode;
        private IEntity _owner;
        private IEntity _pc;
        private List<DialogueReply> _availableReplies;
        private float _nodeTimer;
        private bool _waitingForVO;
        private bool _isPaused;

        public event Action<IEntity, string> OnDialogueText;
        public event Action<IReadOnlyList<DialogueReply>> OnRepliesAvailable;
        public event Action OnDialogueEnd;

        public DialogueSystem(IWorld world, INcsVm scriptVm)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _scriptVm = scriptVm;
            _availableReplies = new List<DialogueReply>();
        }

        public DialogueState State { get; private set; }

        /// <summary>
        /// Starts a conversation with an entity.
        /// </summary>
        public bool StartConversation(IEntity owner, IEntity initiator, string dlgResRef)
        {
            if (string.IsNullOrEmpty(dlgResRef))
            {
                return false;
            }

            _owner = owner;
            _pc = initiator;

            // Load DLG file
            _currentDialogue = LoadDialogue(dlgResRef);
            if (_currentDialogue == null)
            {
                return false;
            }

            // Find first valid entry point
            foreach (int entryIndex in _currentDialogue.StartingList)
            {
                if (entryIndex >= 0 && entryIndex < _currentDialogue.Entries.Count)
                {
                    var entry = _currentDialogue.Entries[entryIndex];
                    if (EvaluateCondition(entry.ActiveScript))
                    {
                        EnterNode(entry);
                        return true;
                    }
                }
            }

            // No valid entries - conversation fails to start
            EndConversation();
            return false;
        }

        private void EnterNode(DialogueEntry entry)
        {
            _currentNode = entry;
            State = DialogueState.Speaking;
            _waitingForVO = false;
            _nodeTimer = 0f;

            // Execute entry script
            if (!string.IsNullOrEmpty(entry.Script))
            {
                ExecuteScript(entry.Script);
            }

            // Get text
            string text = GetText(entry.Text);

            // Notify UI
            if (OnDialogueText != null)
            {
                OnDialogueText(_owner, text);
            }

            // Play voice-over if available
            if (!string.IsNullOrEmpty(entry.VO))
            {
                _waitingForVO = true;
                // Audio playback would be handled here
                // For now, simulate with timer
                _nodeTimer = EstimateTextDuration(text);
            }
            else
            {
                // Text-only node - wait brief time
                _nodeTimer = EstimateTextDuration(text);
            }

            // Gather available replies
            _availableReplies.Clear();
            foreach (int replyIndex in entry.RepliesList)
            {
                if (replyIndex >= 0 && replyIndex < _currentDialogue.Replies.Count)
                {
                    var reply = _currentDialogue.Replies[replyIndex];
                    if (EvaluateCondition(reply.ActiveScript))
                    {
                        _availableReplies.Add(reply);
                    }
                }
            }
        }

        /// <summary>
        /// Updates the dialogue system.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (State == DialogueState.Inactive || _isPaused)
            {
                return;
            }

            if (State == DialogueState.Speaking)
            {
                _nodeTimer -= deltaTime;
                if (_nodeTimer <= 0 && !_waitingForVO)
                {
                    TransitionToReplies();
                }
            }
        }

        /// <summary>
        /// Notifies that voice-over has completed.
        /// </summary>
        public void OnVOComplete()
        {
            _waitingForVO = false;
            if (State == DialogueState.Speaking && _nodeTimer <= 0)
            {
                TransitionToReplies();
            }
        }

        private void TransitionToReplies()
        {
            if (_availableReplies.Count == 0)
            {
                EndConversation();
                return;
            }

            // Single continue reply - auto-proceed
            if (_availableReplies.Count == 1 && IsAutoAdvance(_availableReplies[0]))
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

        private bool IsAutoAdvance(DialogueReply reply)
        {
            // Check if this is a [Continue] style reply
            string text = GetText(reply.Text);
            return string.IsNullOrEmpty(text) ||
                   text.Equals("[Continue]", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("[End]", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Selects a reply from the available choices.
        /// </summary>
        public void SelectReply(int index)
        {
            if (State != DialogueState.WaitingForReply || index < 0 || index >= _availableReplies.Count)
            {
                return;
            }

            var reply = _availableReplies[index];

            // Execute reply script
            if (!string.IsNullOrEmpty(reply.Script))
            {
                ExecuteScript(reply.Script);
            }

            // Find next entry
            foreach (int entryIndex in reply.EntriesList)
            {
                if (entryIndex >= 0 && entryIndex < _currentDialogue.Entries.Count)
                {
                    var entry = _currentDialogue.Entries[entryIndex];
                    if (EvaluateCondition(entry.ActiveScript))
                    {
                        EnterNode(entry);
                        return;
                    }
                }
            }

            // No more entries - conversation ends
            EndConversation();
        }

        /// <summary>
        /// Ends the current conversation.
        /// </summary>
        public void EndConversation()
        {
            State = DialogueState.Inactive;
            _currentDialogue = null;
            _currentNode = null;
            _owner = null;
            _pc = null;
            _availableReplies.Clear();

            if (OnDialogueEnd != null)
            {
                OnDialogueEnd();
            }
        }

        /// <summary>
        /// Pauses the conversation.
        /// </summary>
        public void PauseConversation()
        {
            _isPaused = true;
        }

        /// <summary>
        /// Resumes a paused conversation.
        /// </summary>
        public void ResumeConversation()
        {
            _isPaused = false;
        }

        private bool EvaluateCondition(string scriptResRef)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return true;
            }

            if (_scriptVm == null)
            {
                return true; // No VM - assume true
            }

            // Execute script and check return value
            // Script returns TRUE (1) if condition passes
            try
            {
                // Implementation would call VM with proper context
                return true;
            }
            catch
            {
                return true; // On error, assume condition passes
            }
        }

        private void ExecuteScript(string scriptResRef)
        {
            if (string.IsNullOrEmpty(scriptResRef) || _scriptVm == null)
            {
                return;
            }

            try
            {
                // Implementation would call VM with proper context
            }
            catch
            {
                // Script error - log but don't crash
            }
        }

        private string GetText(LocalizedStringReference textRef)
        {
            if (textRef == null)
            {
                return string.Empty;
            }

            // TLK lookup would happen here
            // For now, return the string reference index as placeholder
            return textRef.StringRef >= 0 ? "[TLK:" + textRef.StringRef + "]" : string.Empty;
        }

        private float EstimateTextDuration(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1.0f;
            }

            // Roughly 150 words per minute reading speed
            int wordCount = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Max(2.0f, wordCount / 2.5f);
        }

        private DialogueData LoadDialogue(string resRef)
        {
            // Implementation would load from resource provider
            // For now, return null - actual implementation needs resource loading
            return null;
        }
    }

    /// <summary>
    /// Dialogue system state.
    /// </summary>
    public enum DialogueState
    {
        Inactive,
        Speaking,
        WaitingForReply,
        Paused
    }

    /// <summary>
    /// Parsed dialogue data from DLG file.
    /// </summary>
    public class DialogueData
    {
        public DialogueData()
        {
            Entries = new List<DialogueEntry>();
            Replies = new List<DialogueReply>();
            StartingList = new List<int>();
        }

        public List<DialogueEntry> Entries { get; set; }
        public List<DialogueReply> Replies { get; set; }
        public List<int> StartingList { get; set; }
        public int DelayEntry { get; set; }
        public int DelayReply { get; set; }
        public bool Skippable { get; set; }
        public string EndConverAbort { get; set; }
        public string EndConversation { get; set; }
    }

    /// <summary>
    /// Base class for dialogue nodes.
    /// </summary>
    public abstract class DialogueNode
    {
        public LocalizedStringReference Text { get; set; }
        public string Script { get; set; }
        public string ActiveScript { get; set; }
        public string VO { get; set; }
        public string Sound { get; set; }
        public int Delay { get; set; }
        public string Speaker { get; set; }
        public string Listener { get; set; }
        public int CameraAngle { get; set; }
        public int CameraId { get; set; }

        protected DialogueNode()
        {
            Text = new LocalizedStringReference();
            Script = string.Empty;
            ActiveScript = string.Empty;
            VO = string.Empty;
            Sound = string.Empty;
            Speaker = string.Empty;
            Listener = string.Empty;
        }
    }

    /// <summary>
    /// NPC dialogue entry.
    /// </summary>
    public class DialogueEntry : DialogueNode
    {
        public DialogueEntry()
        {
            RepliesList = new List<int>();
        }

        public List<int> RepliesList { get; set; }
        public int Quest { get; set; }
        public string QuestEntry { get; set; }
    }

    /// <summary>
    /// PC dialogue reply.
    /// </summary>
    public class DialogueReply : DialogueNode
    {
        public DialogueReply()
        {
            EntriesList = new List<int>();
        }

        public List<int> EntriesList { get; set; }
        public int Quest { get; set; }
        public string QuestEntry { get; set; }
    }

    /// <summary>
    /// Reference to a localized string.
    /// </summary>
    public class LocalizedStringReference
    {
        public int StringRef { get; set; }
        public string OverrideText { get; set; }

        public LocalizedStringReference()
        {
            StringRef = -1;
            OverrideText = string.Empty;
        }
    }
}

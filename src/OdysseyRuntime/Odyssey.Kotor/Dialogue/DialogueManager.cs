using System;
using System.Collections.Generic;
using AuroraEngine.Common;
using AuroraEngine.Common.Formats.TLK;
using AuroraEngine.Common.Resource.Generics.DLG;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Dialogue;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Kotor.Dialogue
{
    /// <summary>
    /// Event arguments for dialogue events.
    /// </summary>
    public class DialogueEventArgs : EventArgs
    {
        public DialogueState State { get; set; }
        public DLGNode Node { get; set; }
        public string Text { get; set; }
        public IEntity Speaker { get; set; }
        public IEntity Listener { get; set; }
    }

    /// <summary>
    /// Manages dialogue conversations in the game.
    /// </summary>
    /// <remarks>
    /// Dialogue System:
    /// - Based on swkotor2.exe dialogue system
    /// - Located via string references: "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4, "OnEndDialogue" @ 0x007c1f60
    /// - Error: "Error: dialogue can't find object '%s'!" @ 0x007c3730
    /// - Original implementation: DLG files contain dialogue tree with nodes, entries, replies, scripts
    /// - Dialogue flow:
    ///   1. StartConversation - Begin dialogue with a DLG file
    ///   2. Evaluate StartingList - Find first valid starter
    ///   3. EnterNode - Execute scripts, fire events
    ///   4. GetReplies - Evaluate and filter reply options
    ///   5. SelectReply - Player chooses reply
    ///   6. Continue to next entry or end
    ///
    /// Script execution:
    /// - Active1/Active2 on links: Condition scripts (return TRUE/FALSE)
    /// - Script1 on nodes: Fires when node is entered
    /// - Script2 on nodes: Fires when node is exited
    /// - OnAbort on DLG: Fires if conversation is aborted
    /// - OnEnd on DLG: Fires when conversation ends normally
    /// </remarks>
    public class DialogueManager
    {
        private readonly INcsVm _vm;
        private readonly IWorld _world;
        private readonly Func<string, DLG> _dialogueLoader;
        private readonly Func<string, byte[]> _scriptLoader;
        private readonly IVoicePlayer _voicePlayer;
        private readonly ILipSyncController _lipSyncController;

        private TLK _baseTlk;
        private TLK _customTlk;

        /// <summary>
        /// Event fired when a dialogue node is entered.
        /// </summary>
        public event EventHandler<DialogueEventArgs> OnNodeEnter;

        /// <summary>
        /// Event fired when a dialogue node is exited.
        /// </summary>
        public event EventHandler<DialogueEventArgs> OnNodeExit;

        /// <summary>
        /// Event fired when replies are ready to be shown.
        /// </summary>
        public event EventHandler<DialogueEventArgs> OnRepliesReady;

        /// <summary>
        /// Event fired when the conversation ends.
        /// </summary>
        public event EventHandler<DialogueEventArgs> OnConversationEnd;

        /// <summary>
        /// The current active dialogue state.
        /// </summary>
        [CanBeNull]
        public DialogueState CurrentState { get; private set; }

        /// <summary>
        /// Whether a conversation is currently active.
        /// </summary>
        public bool IsConversationActive
        {
            get { return CurrentState != null && CurrentState.IsActive; }
        }

        public DialogueManager(
            INcsVm vm,
            IWorld world,
            Func<string, DLG> dialogueLoader,
            Func<string, byte[]> scriptLoader,
            IVoicePlayer voicePlayer = null,
            ILipSyncController lipSyncController = null)
        {
            _vm = vm ?? throw new ArgumentNullException("vm");
            _world = world ?? throw new ArgumentNullException("world");
            _dialogueLoader = dialogueLoader ?? throw new ArgumentNullException("dialogueLoader");
            _scriptLoader = scriptLoader ?? throw new ArgumentNullException("scriptLoader");
            _voicePlayer = voicePlayer;
            _lipSyncController = lipSyncController;
        }

        /// <summary>
        /// Sets the talk tables for text lookup.
        /// </summary>
        public void SetTalkTables(TLK baseTlk, TLK customTlk = null)
        {
            _baseTlk = baseTlk;
            _customTlk = customTlk;
        }

        /// <summary>
        /// Starts a conversation with the specified dialogue.
        /// </summary>
        /// <param name="dialogueResRef">ResRef of the DLG file</param>
        /// <param name="owner">The owner object (NPC, placeable, etc.)</param>
        /// <param name="pc">The player character</param>
        /// <returns>True if conversation started successfully</returns>
        public bool StartConversation(string dialogueResRef, IEntity owner, IEntity pc)
        {
            if (string.IsNullOrEmpty(dialogueResRef))
            {
                return false;
            }

            // Load dialogue
            DLG dialog;
            try
            {
                dialog = _dialogueLoader(dialogueResRef);
            }
            catch
            {
                dialog = null;
            }

            if (dialog == null)
            {
                return false;
            }

            return StartConversation(dialog, owner, pc);
        }

        /// <summary>
        /// Starts a conversation with the specified dialogue.
        /// </summary>
        public bool StartConversation(DLG dialog, IEntity owner, IEntity pc)
        {
            if (dialog == null || owner == null || pc == null)
            {
                return false;
            }

            // End any existing conversation
            if (CurrentState != null)
            {
                EndConversation(true);
            }

            // Create context and state
            var context = new ConversationContext(owner, pc, _world);
            CurrentState = new DialogueState(dialog, context);

            // Find first valid starting entry
            DLGEntry startEntry = FindValidStarter(dialog, context);
            if (startEntry == null)
            {
                // No valid starter found
                CurrentState = null;
                return false;
            }

            // Enter the starting node
            EnterEntry(startEntry);

            return true;
        }

        /// <summary>
        /// Advances the conversation by selecting a reply.
        /// </summary>
        /// <param name="replyIndex">Index of the reply in AvailableReplies</param>
        public void SelectReply(int replyIndex)
        {
            if (CurrentState == null || !CurrentState.IsActive)
            {
                return;
            }

            if (replyIndex < 0 || replyIndex >= CurrentState.AvailableReplies.Count)
            {
                return;
            }

            DLGReply reply = CurrentState.AvailableReplies[replyIndex];

            // Exit current entry
            ExitNode(CurrentState.CurrentEntry);

            // Enter the reply
            EnterReply(reply);

            // Find next entry from reply's links
            DLGEntry nextEntry = FindNextEntry(reply, CurrentState.Context);

            if (nextEntry != null)
            {
                // Continue conversation
                EnterEntry(nextEntry);
            }
            else
            {
                // End of conversation
                EndConversation(false);
            }
        }

        /// <summary>
        /// Skips the current node if skippable.
        /// </summary>
        public void SkipNode()
        {
            if (CurrentState == null || !CurrentState.CanSkip)
            {
                return;
            }

            CurrentState.WaitingForVoiceover = false;
            CurrentState.TimeRemaining = 0f;
        }

        /// <summary>
        /// Pauses the current conversation.
        /// </summary>
        public void PauseConversation()
        {
            if (CurrentState != null && CurrentState.IsActive)
            {
                CurrentState.IsPaused = true;
            }
        }

        /// <summary>
        /// Resumes a paused conversation.
        /// </summary>
        public void ResumeConversation()
        {
            if (CurrentState != null && CurrentState.IsPaused)
            {
                CurrentState.IsPaused = false;
            }
        }

        /// <summary>
        /// Aborts the current conversation.
        /// </summary>
        public void AbortConversation()
        {
            // Stop voiceover and lip sync
            if (_voicePlayer != null)
            {
                _voicePlayer.Stop();
            }
            if (_lipSyncController != null)
            {
                _lipSyncController.Stop();
            }

            EndConversation(true);
        }

        /// <summary>
        /// Updates the dialogue system (call each frame).
        /// </summary>
        public void Update(float deltaTime)
        {
            if (CurrentState == null || !CurrentState.IsActive || CurrentState.IsPaused)
            {
                return;
            }

            // Update lip sync
            if (_lipSyncController != null)
            {
                _lipSyncController.Update(deltaTime);
            }

            // Check if voiceover finished
            if (CurrentState.WaitingForVoiceover && _voicePlayer != null && !_voicePlayer.IsPlaying)
            {
                CurrentState.WaitingForVoiceover = false;
            }

            // Handle auto-advance timing
            if (CurrentState.TimeRemaining > 0)
            {
                CurrentState.TimeRemaining -= deltaTime;
                if (CurrentState.TimeRemaining <= 0 && !CurrentState.WaitingForVoiceover)
                {
                    // Auto-advance (for entries with no player replies)
                    AutoAdvance();
                }
            }
        }

        #region Node Navigation

        /// <summary>
        /// Finds the first valid starting entry.
        /// </summary>
        [CanBeNull]
        private DLGEntry FindValidStarter(DLG dialog, ConversationContext context)
        {
            foreach (DLGLink link in dialog.Starters)
            {
                if (EvaluateLinkCondition(link, context))
                {
                    DLGEntry entry = link.Node as DLGEntry;
                    if (entry != null)
                    {
                        return entry;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the next valid entry from a reply's links.
        /// </summary>
        [CanBeNull]
        private DLGEntry FindNextEntry(DLGReply reply, ConversationContext context)
        {
            foreach (DLGLink link in reply.Links)
            {
                if (EvaluateLinkCondition(link, context))
                {
                    DLGEntry entry = link.Node as DLGEntry;
                    if (entry != null)
                    {
                        return entry;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets valid replies for an entry.
        /// </summary>
        private List<DLGReply> GetValidReplies(DLGEntry entry, ConversationContext context)
        {
            var replies = new List<DLGReply>();

            foreach (DLGLink link in entry.Links)
            {
                if (EvaluateLinkCondition(link, context))
                {
                    DLGReply reply = link.Node as DLGReply;
                    if (reply != null)
                    {
                        replies.Add(reply);
                    }
                }
            }

            return replies;
        }

        /// <summary>
        /// Enters an NPC entry node.
        /// </summary>
        private void EnterEntry(DLGEntry entry)
        {
            if (CurrentState == null)
            {
                return;
            }

            CurrentState.CurrentNode = entry;
            CurrentState.CurrentEntry = entry;
            CurrentState.PushHistory(entry);

            // Execute Script1 (on-enter)
            ExecuteNodeScript(entry.Script1, entry);

            // Get display text
            string text = GetNodeText(entry);

            // Get speaker/listener
            IEntity speaker = CurrentState.GetCurrentSpeaker();
            IEntity listener = CurrentState.GetCurrentListener();

            // Play voiceover if available
            // Based on swkotor2.exe: FUN_005e6ac0 @ 0x005e6ac0
            // Located via string reference: "VO_ResRef" @ 0x007c4eb8, "SoundExists" @ 0x007c4ec8
            // Original implementation: Plays voiceover and waits for completion before advancing
            string voResRef = GetVoiceoverResRef(entry);
            if (!string.IsNullOrEmpty(voResRef) && _voicePlayer != null && speaker != null)
            {
                CurrentState.WaitingForVoiceover = true;
                _voicePlayer.Play(voResRef, speaker, () =>
                {
                    CurrentState.WaitingForVoiceover = false;
                    // When voiceover completes, check if we should auto-advance
                    // Based on original engine: voiceover completion triggers node transition
                    if (CurrentState.TimeRemaining <= 0 && CurrentState.AvailableReplies.Count == 0)
                    {
                        AutoAdvance();
                    }
                });
            }

            // Start lip sync if available
            if (!string.IsNullOrEmpty(voResRef) && _lipSyncController != null && speaker != null)
            {
                // LIP file has same resref as VO file
                _lipSyncController.Start(speaker, voResRef);
            }

            // Calculate display time based on text length or delay
            // Based on swkotor2.exe: FUN_005e6ac0 @ 0x005e6ac0
            // Located via string reference: "Delay" @ 0x007c35b0
            // Original implementation: If Delay == -1 and voiceover exists, uses voiceover duration
            // If Delay == -1 and no voiceover, uses default delay from WaitFlags
            // If Delay >= 0, uses Delay value directly
            if (entry.Delay >= 0)
            {
                CurrentState.TimeRemaining = entry.Delay;
            }
            else if (CurrentState.WaitingForVoiceover && _voicePlayer != null)
            {
                // Wait for voiceover to complete - don't use a timer based on CurrentTime
                // The voiceover callback will set WaitingForVoiceover = false when done
                // Original engine waits for voiceover completion, not a timer
                CurrentState.TimeRemaining = 0f; // Voiceover callback handles timing
            }
            else
            {
                // Estimate based on text length (rough approximation)
                // Original engine uses default delay when Delay == -1 and no voiceover
                CurrentState.TimeRemaining = Math.Max(2f, text.Length * 0.05f);
            }

            // Fire event
            OnNodeEnter?.Invoke(this, new DialogueEventArgs
            {
                State = CurrentState,
                Node = entry,
                Text = text,
                Speaker = speaker,
                Listener = listener
            });

            // Get available replies
            CurrentState.ClearReplies();
            List<DLGReply> replies = GetValidReplies(entry, CurrentState.Context);
            foreach (DLGReply reply in replies)
            {
                CurrentState.AddReply(reply);
            }

            // Fire replies ready event
            OnRepliesReady?.Invoke(this, new DialogueEventArgs
            {
                State = CurrentState,
                Node = entry,
                Text = text,
                Speaker = speaker,
                Listener = listener
            });
        }

        /// <summary>
        /// Enters a player reply node.
        /// </summary>
        private void EnterReply(DLGReply reply)
        {
            if (CurrentState == null)
            {
                return;
            }

            CurrentState.CurrentNode = reply;
            CurrentState.PushHistory(reply);

            // Execute Script1 (on-enter)
            ExecuteNodeScript(reply.Script1, reply);

            // Get display text
            string text = GetNodeText(reply);

            // Fire event
            OnNodeEnter?.Invoke(this, new DialogueEventArgs
            {
                State = CurrentState,
                Node = reply,
                Text = text,
                Speaker = CurrentState.Context.PCSpeaker,
                Listener = CurrentState.Context.Owner
            });
        }

        /// <summary>
        /// Exits a node.
        /// </summary>
        private void ExitNode(DLGNode node)
        {
            if (node == null || CurrentState == null)
            {
                return;
            }

            // Stop voiceover and lip sync
            if (_voicePlayer != null)
            {
                _voicePlayer.Stop();
            }
            if (_lipSyncController != null)
            {
                _lipSyncController.Stop();
            }

            // Execute Script2 (on-exit)
            ExecuteNodeScript(node.Script2, node);

            // Fire event
            OnNodeExit?.Invoke(this, new DialogueEventArgs
            {
                State = CurrentState,
                Node = node
            });
        }

        /// <summary>
        /// Auto-advances when no player input is needed.
        /// </summary>
        private void AutoAdvance()
        {
            if (CurrentState == null || CurrentState.AvailableReplies.Count == 0)
            {
                // No replies means end of conversation or continue with first link
                DLGEntry entry = CurrentState?.CurrentEntry;
                if (entry != null && entry.Links.Count > 0)
                {
                    DLGEntry nextEntry = FindNextEntry(CreateDummyReplyForEntry(entry), CurrentState.Context);
                    if (nextEntry != null)
                    {
                        ExitNode(entry);
                        EnterEntry(nextEntry);
                        return;
                    }
                }
                EndConversation(false);
            }
            else if (CurrentState.AvailableReplies.Count == 1)
            {
                // Single reply - auto-select
                string replyText = GetNodeText(CurrentState.AvailableReplies[0]);
                if (string.IsNullOrEmpty(replyText))
                {
                    // Empty reply text = auto-continue
                    SelectReply(0);
                }
            }
        }

        /// <summary>
        /// Creates a dummy reply wrapper for entry continuation.
        /// </summary>
        private DLGReply CreateDummyReplyForEntry(DLGEntry entry)
        {
            var dummy = new DLGReply();
            dummy.Links.AddRange(entry.Links);
            return dummy;
        }

        /// <summary>
        /// Ends the current conversation.
        /// </summary>
        private void EndConversation(bool aborted)
        {
            if (CurrentState == null)
            {
                return;
            }

            // Stop voiceover and lip sync
            if (_voicePlayer != null)
            {
                _voicePlayer.Stop();
            }
            if (_lipSyncController != null)
            {
                _lipSyncController.Stop();
            }

            DLG dialog = CurrentState.Dialog;
            ConversationContext context = CurrentState.Context;

            // Exit current node
            ExitNode(CurrentState.CurrentNode);

            // Execute OnAbort or OnEnd script
            if (aborted)
            {
                if (dialog.OnAbort != null && !string.IsNullOrEmpty(dialog.OnAbort.ToString()))
                {
                    ExecuteScript(dialog.OnAbort.ToString(), context.Owner);
                }
            }
            else
            {
                if (dialog.OnEnd != null && !string.IsNullOrEmpty(dialog.OnEnd.ToString()))
                {
                    ExecuteScript(dialog.OnEnd.ToString(), context.Owner);
                }
            }

            // Fire end event
            OnConversationEnd?.Invoke(this, new DialogueEventArgs
            {
                State = CurrentState
            });

            CurrentState.IsActive = false;
            CurrentState = null;
        }

        #endregion

        #region Script Execution

        /// <summary>
        /// Evaluates a link's condition scripts.
        /// </summary>
        private bool EvaluateLinkCondition(DLGLink link, ConversationContext context)
        {
            if (link == null)
            {
                return false;
            }

            // If no condition scripts, link is always valid
            bool hasActive1 = link.Active1 != null && !string.IsNullOrEmpty(link.Active1.ToString());
            bool hasActive2 = link.Active2 != null && !string.IsNullOrEmpty(link.Active2.ToString());

            if (!hasActive1 && !hasActive2)
            {
                return true;
            }

            bool result1 = true;
            bool result2 = true;

            // Evaluate Active1
            if (hasActive1)
            {
                int ret = ExecuteConditionScript(link.Active1.ToString(), context.Owner);
                result1 = (ret != 0) ^ link.Active1Not;
            }

            // Evaluate Active2
            if (hasActive2)
            {
                int ret = ExecuteConditionScript(link.Active2.ToString(), context.Owner);
                result2 = (ret != 0) ^ link.Active2Not;
            }

            // Combine results based on Logic (true = AND, false = OR)
            if (link.Logic)
            {
                return result1 && result2;
            }
            else
            {
                return result1 || result2;
            }
        }

        /// <summary>
        /// Executes a condition script and returns the result.
        /// </summary>
        private int ExecuteConditionScript(string scriptResRef, IEntity caller)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return 1; // TRUE by default
            }

            byte[] scriptBytes;
            try
            {
                scriptBytes = _scriptLoader(scriptResRef);
            }
            catch
            {
                return 1; // TRUE if script not found
            }

            if (scriptBytes == null || scriptBytes.Length == 0)
            {
                return 1;
            }

            // Create execution context
            IExecutionContext ctx = CreateExecutionContext(caller);

            try
            {
                return _vm.Execute(scriptBytes, ctx);
            }
            catch
            {
                return 1; // TRUE on error
            }
        }

        /// <summary>
        /// Executes a node's action script.
        /// </summary>
        private void ExecuteNodeScript(ResRef scriptRef, DLGNode node)
        {
            if (scriptRef == null || string.IsNullOrEmpty(scriptRef.ToString()))
            {
                return;
            }

            IEntity caller = CurrentState?.Context.Owner;
            if (caller == null)
            {
                return;
            }

            ExecuteScript(scriptRef.ToString(), caller);
        }

        /// <summary>
        /// Executes a script by ResRef.
        /// </summary>
        private void ExecuteScript(string scriptResRef, IEntity caller)
        {
            if (string.IsNullOrEmpty(scriptResRef) || caller == null)
            {
                return;
            }

            byte[] scriptBytes;
            try
            {
                scriptBytes = _scriptLoader(scriptResRef);
            }
            catch
            {
                return;
            }

            if (scriptBytes == null || scriptBytes.Length == 0)
            {
                return;
            }

            IExecutionContext ctx = CreateExecutionContext(caller);

            try
            {
                _vm.Execute(scriptBytes, ctx);
            }
            catch
            {
                // Script execution failed - log but continue
            }
        }

        /// <summary>
        /// Creates an execution context for script execution.
        /// </summary>
        private IExecutionContext CreateExecutionContext(IEntity caller)
        {
            // This would typically be implemented by the scripting system
            // For now, return null as a placeholder
            return null;
        }

        #endregion

        #region Text Handling

        /// <summary>
        /// Gets the display text for a node.
        /// </summary>
        public string GetNodeText(DLGNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            LocalizedString locStr = node.Text;
            if (locStr == null)
            {
                return string.Empty;
            }

            // Try to get localized text
            return ResolveLocalizedString(locStr);
        }

        /// <summary>
        /// Resolves a localized string to display text.
        /// </summary>
        private string ResolveLocalizedString(LocalizedString locStr)
        {
            if (locStr == null)
            {
                return string.Empty;
            }

            // First check if there's a string reference
            int stringRef = locStr.StringRef;
            if (stringRef >= 0)
            {
                // Look up in custom TLK first, then base TLK
                string text = LookupString(stringRef);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            // Fall back to inline substrings
            // Try English (Male) first
            string substring = locStr.Get(Language.English, Gender.Male, false);
            if (!string.IsNullOrEmpty(substring))
            {
                return substring;
            }

            // Try any available substring via the enumerator
            foreach ((Language, Gender, string) tuple in locStr)
            {
                if (!string.IsNullOrEmpty(tuple.Item3))
                {
                    return tuple.Item3;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Looks up a string by reference in the talk tables.
        /// </summary>
        /// <summary>
        /// Looks up a string by string reference in the talk tables.
        /// </summary>
        public string LookupString(int stringRef)
        {
            if (stringRef < 0)
            {
                return string.Empty;
            }

            // Custom TLK entries start at 0x01000000 (high bit set)
            const int CUSTOM_TLK_START = 0x01000000;

            if (stringRef >= CUSTOM_TLK_START)
            {
                // Look up in custom TLK
                if (_customTlk != null)
                {
                    int customRef = stringRef - CUSTOM_TLK_START;
                    return _customTlk.String(customRef);
                }
            }
            else
            {
                // Look up in base TLK
                if (_baseTlk != null)
                {
                    return _baseTlk.String(stringRef);
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the voiceover ResRef for a node.
        /// </summary>
        [CanBeNull]
        public string GetVoiceoverResRef(DLGNode node)
        {
            if (node == null)
            {
                return null;
            }

            // Check VoResRef first
            if (node.VoResRef != null && !string.IsNullOrEmpty(node.VoResRef.ToString()))
            {
                return node.VoResRef.ToString();
            }

            // Check Sound field
            if (node.Sound != null && !string.IsNullOrEmpty(node.Sound.ToString()))
            {
                return node.Sound.ToString();
            }

            // Check TLK entry for voiceover
            if (node.Text != null && node.Text.StringRef >= 0)
            {
                // TLK entries can have associated voiceover ResRefs
                int stringRef = node.Text.StringRef;
                const int CUSTOM_TLK_START = 0x01000000;

                if (stringRef >= CUSTOM_TLK_START && _customTlk != null)
                {
                    TLKEntry entry = _customTlk.Get(stringRef - CUSTOM_TLK_START);
                    if (entry != null && entry.Voiceover != null)
                    {
                        return entry.Voiceover.ToString();
                    }
                }
                else if (_baseTlk != null)
                {
                    TLKEntry entry = _baseTlk.Get(stringRef);
                    if (entry != null && entry.Voiceover != null)
                    {
                        return entry.Voiceover.ToString();
                    }
                }
            }

            return null;
        }

        #endregion
    }
}

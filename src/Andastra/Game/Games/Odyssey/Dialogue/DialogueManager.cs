using System;
using System.Collections.Generic;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Journal;
using Andastra.Runtime.Core.Party;
using Andastra.Runtime.Core.Plot;
using Andastra.Game.Games.Odyssey.Data;
using Andastra.Game.Games.Common.Dialogue;
using Andastra.Game.Scripting.Interfaces;
using JetBrains.Annotations;
using ResRef = BioWare.NET.Common.ResRef;

namespace Andastra.Game.Games.Odyssey.Dialogue
{
    /// <summary>
    /// Event arguments for dialogue events (Odyssey-specific: DLG-based).
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
    /// Manages dialogue conversations in Odyssey engine (DLG-based).
    /// </summary>
    /// <remarks>
    /// Dialogue System (Odyssey-specific):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) dialogue system
    /// - Located via string references: "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4, "OnEndDialogue" @ 0x007c1f60
    /// - Error: "Error: dialogue can't find object '%s'!" @ 0x007c3730
    /// - Inheritance: Base class BaseDialogueManager (Runtime.Games.Common) - abstract dialogue manager, Odyssey override (Runtime.Games.Odyssey) - DLG-based dialogue
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
    public class DialogueManager : BaseDialogueManager
    {
        // Plot XP calculation multipliers (swkotor2.exe data addresses)
        // swkotor2.exe: 0x005e6870 (ProcessPlotXPThreshold) - Checks threshold and calculates base multiplier
        // swkotor2.exe: 0x0057eb20 (AwardPlotXP) - Calculates final XP and awards it to party
        // K1 (swkotor.exe): TODO - Find equivalent addresses (needs reverse engineering)
        // _DAT_007b99b4: Base multiplier applied to plotXpPercentage
        // _DAT_007b5f88: Additional multiplier applied to final XP calculation
        // Verified reverse engineering:
        // - 0x005e6870 (ProcessPlotXPThreshold) line 17: param_2 * _DAT_007b99b4
        // - 0x0057eb20 (AwardPlotXP) line 30: (float)(local_18 * param_2) * _DAT_007b5f88
        // Memory read from swkotor2.exe:
        // - 0x007b99b4: 00 00 C8 42 = 100.0f
        // - 0x007b5f88: 0A D7 23 3C = 0.009999999776482582f (approximately 0.01f)
        // Note: Function names are descriptive based on reverse engineering analysis and need Ghidra verification
        private const float PLOT_XP_BASE_MULTIPLIER = 100.0f; // _DAT_007b99b4 @ 0x007b99b4 - Verified 
        private const float PLOT_XP_ADDITIONAL_MULTIPLIER = 0.009999999776482582f; // _DAT_007b5f88 @ 0x007b5f88 - Verified 

        // Plot XP threshold check (swkotor2.exe: 0x005e6870 (ProcessPlotXPThreshold))
        // Only processes XP if threshold < plotXpPercentage
        // Verified reverse engineering:
        // - 0x005e6870 (ProcessPlotXPThreshold) line 13: if ((param_1 != -1) && (_DAT_007b56fc < param_2))
        // - Memory read from swkotor2.exe @ 0x007b56fc: 00 00 00 00 = 0.0f
        // Logic verification: Comparison "threshold < plotXpPercentage" with threshold=0.0f correctly filters:
        //   - plotXpPercentage = 0.0f → 0.0 < 0.0 = false → No XP (correct, 0% means no XP award)
        //   - plotXpPercentage > 0.0f → 0.0 < percentage = true → XP processed (correct, any positive % processes XP)
        // This matches the original engine behavior where plot XP is only awarded when the percentage value is positive
        // Original implementation: ProcessPlotXPThreshold checks "if (threshold < plotXpPercentage)" before processing XP
        private const float PLOT_XP_THRESHOLD = 0.0f; // _DAT_007b56fc @ 0x007b56fc - Verified 

        private readonly Func<string, DLG> _dialogueLoader;
        private readonly Func<string, byte[]> _scriptLoader;
        private readonly IVoicePlayer _voicePlayer;
        private readonly ILipSyncController _lipSyncController;
        [CanBeNull]
        private readonly JournalSystem _journalSystem;
        [CanBeNull]
        private readonly GameDataManager _gameDataManager;
        [CanBeNull]
        private readonly PartySystem _partySystem;
        [CanBeNull]
        private readonly PlotSystem _plotSystem;
        [CanBeNull]
        private readonly JRLLoader _jrlLoader;

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
        /// The current active dialogue state (Odyssey-specific: DialogueState type).
        /// </summary>
        [CanBeNull]
        private DialogueState _currentState;

        /// <summary>
        /// The current active dialogue state (base class property).
        /// </summary>
        [CanBeNull]
        public override BaseDialogueState CurrentState
        {
            get { return _currentState; }
        }

        /// <summary>
        /// The current active dialogue state (Odyssey-specific type accessor).
        /// </summary>
        [CanBeNull]
        public DialogueState CurrentDialogueState
        {
            get { return _currentState; }
            private set { _currentState = value; }
        }

        public DialogueManager(
            INcsVm vm,
            IWorld world,
            IEngineApi engineApi,
            IScriptGlobals globals,
            Func<string, DLG> dialogueLoader,
            Func<string, byte[]> scriptLoader,
            IVoicePlayer voicePlayer = null,
            ILipSyncController lipSyncController = null,
            [CanBeNull] JournalSystem journalSystem = null,
            [CanBeNull] GameDataManager gameDataManager = null,
            [CanBeNull] PartySystem partySystem = null,
            [CanBeNull] PlotSystem plotSystem = null,
            [CanBeNull] JRLLoader jrlLoader = null)
            : base(vm, world, engineApi, globals)
        {
            _dialogueLoader = dialogueLoader ?? throw new ArgumentNullException("dialogueLoader");
            _scriptLoader = scriptLoader ?? throw new ArgumentNullException("scriptLoader");
            _voicePlayer = voicePlayer;
            _lipSyncController = lipSyncController;
            _journalSystem = journalSystem;
            _gameDataManager = gameDataManager;
            _partySystem = partySystem;
            _plotSystem = plotSystem;
            _jrlLoader = jrlLoader;
        }

        /// <summary>
        /// Sets the talk tables for text lookup.
        /// </summary>
        public void SetTalkTables(TLK baseTlk, TLK customTlk = null)
        {
            _baseTlk = baseTlk;
            _customTlk = customTlk;

            // Also set talk tables for JRL loader
            if (_jrlLoader != null)
            {
                _jrlLoader.SetTalkTables(baseTlk, customTlk);
            }
        }

        /// <summary>
        /// Starts a conversation with the specified dialogue.
        /// </summary>
        /// <param name="dialogueResRef">ResRef of the DLG file</param>
        /// <param name="owner">The owner object (NPC, placeable, etc.)</param>
        /// <param name="pc">The player character</param>
        /// <returns>True if conversation started successfully</returns>
        public override bool StartConversation(string dialogueResRef, IEntity owner, IEntity pc)
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
            if (_currentState != null)
            {
                AbortConversation();
            }

            // Create context and state
            var context = new ConversationContext(owner, pc, _world);
            CurrentDialogueState = new DialogueState(dialog, context);

            // Find first valid starting entry
            DLGEntry startEntry = FindValidStarter(dialog, context);
            if (startEntry == null)
            {
                // No valid starter found
                CurrentDialogueState = null;
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
        public override void SelectReply(int replyIndex)
        {
            if (_currentState == null || !_currentState.IsActive)
            {
                return;
            }

            if (replyIndex < 0 || replyIndex >= _currentState.AvailableReplies.Count)
            {
                return;
            }

            DLGReply reply = _currentState.AvailableReplies[replyIndex];

            // Exit current entry
            ExitNode(_currentState.CurrentEntry);

            // Enter the reply
            EnterReply(reply);

            // Find next entry from reply's links
            DLGEntry nextEntry = FindNextEntry(reply, _currentState.Context);

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
        public override void SkipNode()
        {
            if (_currentState == null || !_currentState.CanSkip)
            {
                return;
            }

            _currentState.WaitingForVoiceover = false;
            _currentState.TimeRemaining = 0f;
        }

        /// <summary>
        /// Aborts the current conversation.
        /// </summary>
        public override void AbortConversation()
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
        public override void Update(float deltaTime)
        {
            if (_currentState == null || !_currentState.IsActive || _currentState.IsPaused)
            {
                return;
            }

            // Update lip sync
            if (_lipSyncController != null)
            {
                _lipSyncController.Update(deltaTime);
            }

            // Check if voiceover finished
            if (_currentState.WaitingForVoiceover && _voicePlayer != null && !_voicePlayer.IsPlaying)
            {
                _currentState.WaitingForVoiceover = false;
            }

            // Handle auto-advance timing
            if (_currentState.TimeRemaining > 0)
            {
                _currentState.TimeRemaining -= deltaTime;
                if (_currentState.TimeRemaining <= 0 && !_currentState.WaitingForVoiceover)
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
            if (_currentState == null)
            {
                return;
            }

            _currentState.CurrentNode = entry;
            _currentState.CurrentEntry = entry;
            _currentState.PushHistory(entry);

            // Execute Script1 (on-enter)
            ExecuteNodeScript(entry.Script1, entry);

            // Process quest fields if present
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005e61d0 @ 0x005e61d0
            // Located via string references: "Quest" @ 0x007c35e4, "QuestEntry" @ 0x007c35d8
            // Called from 0x005e7cb0 @ 0x005e7f85 and 0x005ec340 @ 0x005ec6a9
            // Original implementation: Processes quest fields when dialogue entry node is entered
            // - Checks if quest string is not empty
            // - Gets current quest entry count
            // - If quest entry index < current count, updates existing entry
            // - Otherwise, adds new quest entry
            // - Updates journal UI and notifies journal system
            ProcessQuestFields(entry);

            // Get display text
            string text = GetNodeText(entry);

            // Get speaker/listener
            IEntity speaker = _currentState.GetCurrentSpeaker();
            IEntity listener = _currentState.GetCurrentListener();

            // Play voiceover if available
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005e6ac0 @ 0x005e6ac0
            // Located via string reference: "VO_ResRef" @ 0x007c4eb8, "SoundExists" @ 0x007c4ec8
            // Original implementation: Plays voiceover and waits for completion before advancing
            string voResRef = GetVoiceoverResRef(entry);
            if (!string.IsNullOrEmpty(voResRef) && _voicePlayer != null && speaker != null)
            {
                _currentState.WaitingForVoiceover = true;
                _voicePlayer.Play(voResRef, speaker, () =>
                {
                    _currentState.WaitingForVoiceover = false;
                    // When voiceover completes, check if we should auto-advance
                    // Based on original engine: voiceover completion triggers node transition
                    if (_currentState.TimeRemaining <= 0 && _currentState.AvailableReplies.Count == 0)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005e6ac0 @ 0x005e6ac0
            // Located via string reference: "Delay" @ 0x007c35b0
            // Original implementation: If Delay == -1 and voiceover exists, uses voiceover duration
            // If Delay == -1 and no voiceover, uses default delay from WaitFlags
            // If Delay >= 0, uses Delay value directly
            if (entry.Delay >= 0)
            {
                _currentState.TimeRemaining = entry.Delay;
            }
            else if (_currentState.WaitingForVoiceover && _voicePlayer != null)
            {
                // Wait for voiceover to complete - don't use a timer based on CurrentTime
                // The voiceover callback will set WaitingForVoiceover = false when done
                // Original engine waits for voiceover completion, not a timer
                _currentState.TimeRemaining = 0f; // Voiceover callback handles timing
            }
            else
            {
                // Estimate based on text length (rough approximation)
                // Original engine uses default delay when Delay == -1 and no voiceover
                _currentState.TimeRemaining = Math.Max(2f, text.Length * 0.05f);
            }

            // Fire event
            OnNodeEnter?.Invoke(this, new DialogueEventArgs
            {
                State = _currentState,
                Node = entry,
                Text = text,
                Speaker = speaker,
                Listener = listener
            });

            // Get available replies
            _currentState.ClearReplies();
            List<DLGReply> replies = GetValidReplies(entry, _currentState.Context);
            foreach (DLGReply reply in replies)
            {
                _currentState.AddReply(reply);
            }

            // Fire replies ready event
            OnRepliesReady?.Invoke(this, new DialogueEventArgs
            {
                State = _currentState,
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
            if (_currentState == null)
            {
                return;
            }

            _currentState.CurrentNode = reply;
            _currentState.PushHistory(reply);

            // Execute Script1 (on-enter)
            ExecuteNodeScript(reply.Script1, reply);

            // Process quest fields if present
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005e61d0 @ 0x005e61d0
            // Called from 0x005ec340 @ 0x005ec6a9 (dialogue reply processing)
            // Reply nodes also have quest fields that need processing
            ProcessQuestFields(reply);

            // Get display text
            string text = GetNodeText(reply);

            // Fire event
            OnNodeEnter?.Invoke(this, new DialogueEventArgs
            {
                State = _currentState,
                Node = reply,
                Text = text,
                Speaker = _currentState.Context.PCSpeaker,
                Listener = _currentState.Context.Owner
            });
        }

        /// <summary>
        /// Exits a node.
        /// </summary>
        private void ExitNode(DLGNode node)
        {
            if (node == null || _currentState == null)
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
                State = _currentState,
                Node = node
            });
        }

        /// <summary>
        /// Auto-advances when no player input is needed.
        /// </summary>
        private void AutoAdvance()
        {
            if (_currentState == null || _currentState.AvailableReplies.Count == 0)
            {
                // No replies means end of conversation or continue with first link
                DLGEntry entry = _currentState?.CurrentEntry;
                if (entry != null && entry.Links.Count > 0)
                {
                    DLGEntry nextEntry = FindNextEntry(CreateDummyReplyForEntry(entry), _currentState.Context);
                    if (nextEntry != null)
                    {
                        ExitNode(entry);
                        EnterEntry(nextEntry);
                        return;
                    }
                }
                EndConversation(false);
            }
            else if (_currentState.AvailableReplies.Count == 1)
            {
                // Single reply - auto-select
                string replyText = GetNodeText(_currentState.AvailableReplies[0]);
                if (string.IsNullOrEmpty(replyText))
                {
                    // Empty reply text = auto-continue
                    SelectReply(0);
                }
            }
        }

        /// <summary>
        /// Creates a comprehensive dummy reply wrapper for entry continuation.
        /// This method creates a fully-featured DLGReply that represents continuing
        /// from the given entry, copying all relevant properties for proper dialogue flow.
        /// </summary>
        /// <param name="entry">The entry to create a continuation reply for</param>
        /// <returns>A comprehensive dummy DLGReply with all relevant properties copied</returns>
        private DLGReply CreateDummyReplyForEntry(DLGEntry entry)
        {
            var dummy = new DLGReply();

            // Essential: Copy links for dialogue flow navigation
            dummy.Links.AddRange(entry.Links);

            // Scripts: Copy action scripts that should execute on continuation
            dummy.Script1 = entry.Script1;
            dummy.Script2 = entry.Script2;
            dummy.Script1Param1 = entry.Script1Param1;
            dummy.Script1Param2 = entry.Script1Param2;
            dummy.Script1Param3 = entry.Script1Param3;
            dummy.Script1Param4 = entry.Script1Param4;
            dummy.Script1Param5 = entry.Script1Param5;
            dummy.Script1Param6 = entry.Script1Param6;
            dummy.Script2Param1 = entry.Script2Param1;
            dummy.Script2Param2 = entry.Script2Param2;
            dummy.Script2Param3 = entry.Script2Param3;
            dummy.Script2Param4 = entry.Script2Param4;
            dummy.Script2Param5 = entry.Script2Param5;
            dummy.Script2Param6 = entry.Script2Param6;

            // Quest/Plot: Copy quest and plot data for proper processing
            dummy.Quest = entry.Quest;
            dummy.QuestEntry = entry.QuestEntry;
            dummy.PlotIndex = entry.PlotIndex;
            dummy.PlotXpPercentage = entry.PlotXpPercentage;

            // Camera settings: Copy visual camera configuration
            dummy.CameraAngle = entry.CameraAngle;
            dummy.CameraAnim = entry.CameraAnim;
            dummy.CameraId = entry.CameraId;
            dummy.CameraFov = entry.CameraFov;
            dummy.CameraHeight = entry.CameraHeight;
            dummy.CameraEffect = entry.CameraEffect;

            // Timing: Copy delay and fade settings for proper timing
            dummy.Delay = entry.Delay;
            dummy.FadeType = entry.FadeType;
            dummy.FadeColor = entry.FadeColor;
            dummy.FadeDelay = entry.FadeDelay;
            dummy.FadeLength = entry.FadeLength;
            dummy.WaitFlags = entry.WaitFlags;

            // Animations: Copy animation sequences
            dummy.Animations.AddRange(entry.Animations);
            dummy.EmotionId = entry.EmotionId;
            dummy.FacialId = entry.FacialId;

            // Audio: Copy sound and voiceover settings
            dummy.Sound = entry.Sound;
            dummy.SoundExists = entry.SoundExists;
            dummy.VoResRef = entry.VoResRef;

            // Other properties: Copy remaining relevant settings
            dummy.Listener = entry.Listener;
            dummy.TargetHeight = entry.TargetHeight;
            dummy.AlienRaceNode = entry.AlienRaceNode;
            dummy.NodeId = entry.NodeId;
            dummy.PostProcNode = entry.PostProcNode;
            dummy.Unskippable = entry.Unskippable;
            dummy.RecordNoVoOverride = entry.RecordNoVoOverride;
            dummy.RecordVo = entry.RecordVo;
            dummy.VoTextChanged = entry.VoTextChanged;

            // Dummy-specific properties
            dummy.ListIndex = -1; // Indicate this is a dummy/generated reply
            dummy.Text = LocalizedString.FromInvalid(); // No text for continuation replies
            dummy.Comment = $"Dummy reply for entry continuation (original entry: {entry.ListIndex})";

            return dummy;
        }

        /// <summary>
        /// Ends the current conversation.
        /// </summary>
        private void EndConversation(bool aborted)
        {
            if (_currentState == null)
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

            DLG dialog = _currentState.Dialog;
            ConversationContext context = _currentState.Context;

            // Exit current node
            ExitNode(_currentState.CurrentNode);

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
                State = _currentState
            });

            _currentState.IsActive = false;
            CurrentDialogueState = null;
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

            IEntity caller = _currentState?.Context.Owner;
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
        /// <param name="caller">The calling entity (OBJECT_SELF).</param>
        /// <returns>Execution context with engine API, world, globals.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Script execution context for dialogue scripts
        /// - Located via string references: "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0
        /// - ExecuteDialogue @ 0x005e9920: Creates execution context with caller entity, world, engine API, globals
        /// - Original implementation: Execution context provides script access to:
        ///   - Caller: The entity that owns the script (OBJECT_SELF, used by GetObjectSelf NWScript function)
        ///   - World: Reference to game world for entity lookups and engine API calls
        ///   - EngineApi: Reference to NWScript engine API implementation (Kotor1 or TheSithLords)
        ///   - Globals: Reference to script globals system for global/local variable access
        /// - Script context is passed to NCS VM for ACTION opcode execution (engine function calls)
        /// - Cross-engine analysis:
        ///   - Aurora (nwmain.exe): CNWSDialog::RunScript @ 0x140dddb80 - similar execution context structure
        ///   - Eclipse (daorigins.exe): Conversation::ExecuteScript - UnrealScript-based, different but equivalent
        /// </remarks>
        private IExecutionContext CreateExecutionContext(IEntity caller)
        {
            // Use base class implementation which provides common execution context creation
            // This ensures consistency across all engines while allowing engine-specific overrides if needed
            return base.CreateExecutionContext(caller, null);
        }

        /// <summary>
        /// Processes quest fields from a dialogue node (entry or reply).
        /// </summary>
        /// <param name="node">The dialogue node (DLGEntry or DLGReply)</param>
        /// <remarks>
        /// Quest Field Processing (swkotor2.exe: 0x005e61d0 @ 0x005e61d0):
        /// - Called from 0x005e7cb0 @ 0x005e7f85 (dialogue entry processing)
        /// - Called from 0x005ec340 @ 0x005ec6a9 (dialogue reply processing)
        /// - Located via string references: "Quest" @ 0x007c35e4, "QuestEntry" @ 0x007c35d8
        /// - Original implementation:
        ///   1. Checks if quest string is not null/empty
        ///   2. Gets player object from world
        ///   3. Gets journal system
        ///   4. Gets current quest entry count for the quest
        ///   5. If quest entry index < current count: Updates existing entry
        ///   6. Otherwise: Adds new quest entry
        ///   7. Updates journal UI and notifies journal system
        /// - Quest field offsets in node structure:
        ///   - Quest string: offset 0x98 (param_1)
        ///   - QuestEntry: offset 0xa0 (param_2)
        ///   - Player ID: param_3
        /// - Cross-engine analysis:
        ///   - swkotor.exe: Similar quest processing (needs reverse engineering)
        ///   - nwmain.exe: Journal system uses different format (JRL files)
        ///   - daorigins.exe: Quest system may differ (needs reverse engineering)
        /// </remarks>
        private void ProcessQuestFields(DLGNode node)
        {
            if (node == null || _journalSystem == null)
            {
                return;
            }

            // Check if quest field is present
            string questTag = node.Quest;
            if (string.IsNullOrEmpty(questTag))
            {
                return;
            }

            // Get quest entry index (defaults to 0 if not set)
            int questEntryIndex = node.QuestEntry ?? 0;

            // Get current quest entry count
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00600c30 - Get quest entry count
            // Original implementation checks if entry index < current count
            List<JournalEntry> existingEntries = _journalSystem.GetEntriesForQuest(questTag);
            int currentEntryCount = existingEntries.Count;

            // Process quest entry
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): If entry index < current count, update existing entry
            // Otherwise, add new entry
            if (questEntryIndex < currentEntryCount)
            {
                // Update existing entry
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00601780 - Set quest entry text
                // Original implementation updates existing entry text and state
                JournalEntry existingEntry = existingEntries[questEntryIndex];
                if (existingEntry != null)
                {
                    // Look up quest entry text from JRL file
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Quest entry text is loaded from JRL files
                    // Original implementation: Looks up text from JRL file using quest tag and entry ID
                    string entryText = GetQuestEntryTextFromJRL(questTag, questEntryIndex);

                    // Fallback to quest stage data if JRL lookup fails
                    if (string.IsNullOrEmpty(entryText))
                    {
                        QuestData quest = _journalSystem.GetQuest(questTag);
                        if (quest != null)
                        {
                            QuestStage stage = quest.GetStage(questEntryIndex);
                            if (stage != null)
                            {
                                entryText = stage.Text;
                            }
                        }
                    }

                    // If still no text, keep existing text
                    if (string.IsNullOrEmpty(entryText))
                    {
                        entryText = existingEntry.Text ?? string.Empty;
                    }

                    // Update the entry
                    _journalSystem.UpdateEntry(questTag, questEntryIndex, entryText, existingEntry.XPReward);

                    // Ensure quest state is set
                    int currentState = _journalSystem.GetQuestState(questTag);
                    if (currentState == 0)
                    {
                        // Quest not started yet - start it
                        _journalSystem.SetQuestState(questTag, 1);
                    }
                }
            }
            else
            {
                // Add new quest entry (swkotor2.exe: 0x00600dd0 - AddQuestEntry)
                // Called from ProcessQuestFields (0x005e61d0) when quest entry index >= current entry count
                // Original implementation (swkotor2.exe: 0x00600dd0):
                // - Looks up quest entry text from JRL file using quest tag and entry ID
                // - Falls back to quest stage data if JRL lookup fails
                // - Uses empty string if no text found (allows entries without text for state tracking)
                // - Adds entry to journal via JournalSystem.AddEntry (which fires OnEntryAdded event to update UI)
                // - Updates quest state if quest not started (sets state to 1)
                // - Notifies journal system via event handlers
                // - Journal UI is updated via OnEntryAdded event handler in JournalSystem
                // K1 (swkotor.exe): TODO - Find equivalent address (needs reverse engineering)

                // Look up quest entry text from JRL file
                // Original implementation (swkotor2.exe): Looks up text from JRL file using quest tag and entry ID
                // JRL files are typically named after quest tags (e.g., "quest_001.jrl")
                // Falls back to global.jrl if quest-specific JRL not found
                string entryText = GetQuestEntryTextFromJRL(questTag, questEntryIndex);

                // Fallback to quest stage data if JRL lookup fails
                // Original implementation: Uses quest stage data if JRL lookup fails
                if (string.IsNullOrEmpty(entryText))
                {
                    QuestData quest = _journalSystem.GetQuest(questTag);
                    if (quest != null)
                    {
                        QuestStage stage = quest.GetStage(questEntryIndex);
                        if (stage != null)
                        {
                            entryText = stage.Text;
                        }
                    }
                }

                // If still no text, use empty string (original engine behavior)
                // Original implementation (swkotor2.exe: 0x00600dd0): Allows entries without text for state tracking
                // Empty text entries are valid and used for quest state progression without journal text updates
                if (string.IsNullOrEmpty(entryText))
                {
                    entryText = string.Empty;
                }

                // Add journal entry (swkotor2.exe: 0x00600dd0 calls journal system AddEntry function)
                // Original implementation: JournalSystem.AddEntry creates entry, adds to list, and fires OnEntryAdded event
                // OnEntryAdded event notifies journal UI to update display with new entry
                // XP reward defaults to 0 for dialogue-triggered entries (plot XP is handled separately via PlotIndex)
                _journalSystem.AddEntry(questTag, questEntryIndex, entryText, 0);

                // Update quest state if needed (swkotor2.exe: 0x00600dd0 sets quest state if not started)
                // Original implementation: If quest state is 0 (not started), sets state to 1 (in progress)
                // This ensures quest is marked as started when first entry is added
                int currentState = _journalSystem.GetQuestState(questTag);
                if (currentState == 0)
                {
                    // Quest not started yet - start it
                    _journalSystem.SetQuestState(questTag, 1);
                }
            }

            // Process plot index and XP percentage if present
            // swkotor2.exe: 0x005e6870 (ProcessPlotXPThreshold) -> 0x0057eb20 (AwardPlotXP)
            // K1 (swkotor.exe): TODO - Find equivalent addresses (needs reverse engineering)
            // Located via string references: "PlotIndex" @ 0x007c35c4, "PlotXPPercentage" @ 0x007c35cc
            // Original implementation: Updates plot flags and awards XP based on PlotIndex and PlotXpPercentage
            // Flow: ProcessPlotXPThreshold (0x005e6870) -> AwardPlotXP (0x0057eb20)
            // AwardPlotXP: Looks up "XP" column from plot.2da using PlotIndex, calculates final XP, awards XP
            // Comprehensive PlotIndex processing:
            // 1. Process plot XP (if PlotXpPercentage > 0)
            // 2. Register plot in plot system (if not already registered)
            // 3. Trigger plot state tracking
            // 4. Update quest/journal state if plot label matches a quest
            if (node.PlotIndex >= 0)
            {
                ProcessPlotIndex(node.PlotIndex, node.PlotXpPercentage, questTag, questEntryIndex);
            }
        }

        /// <summary>
        /// Comprehensively processes PlotIndex including XP, state tracking, and quest integration.
        /// </summary>
        /// <param name="plotIndex">Plot index from plot.2da</param>
        /// <param name="plotXpPercentage">XP percentage multiplier (0.0-1.0)</param>
        /// <param name="questTag">Quest tag from dialogue node (if available)</param>
        /// <param name="questEntryIndex">Quest entry index from dialogue node (if available)</param>
        /// <remarks>
        /// Comprehensive PlotIndex Processing (swkotor2.exe: 0x005e6870 (ProcessPlotXPThreshold) -> 0x0057eb20 (AwardPlotXP)):
        /// - ProcessPlotXPThreshold (0x005e6870): Checks if plotIndex != -1 and threshold < plotXpPercentage
        ///   - Calculates: plotXpPercentage * _DAT_007b99b4 (base multiplier)
        ///   - Calls AwardPlotXP (0x0057eb20) with plotIndex and calculated value
        /// - AwardPlotXP (0x0057eb20): Looks up "XP" column from plot.2da using plotIndex
        ///   - Calculates: (plotXP * param_2) * _DAT_007b5f88 (additional multiplier)
        ///   - Awards XP via 0x0057ccd0 (party XP award function)
        ///   - Notifies journal system via 0x00681a10
        /// - Complete PlotIndex processing includes:
        ///   1. Look up plot.2da row using PlotIndex
        ///   2. Register plot in plot system (if not already registered)
        ///   3. Get plot label and XP from plot.2da
        ///   4. Process plot XP (if PlotXpPercentage > 0)
        ///   5. Trigger plot state tracking
        ///   6. Update quest/journal state if plot label matches a quest
        ///   7. Mark plot as triggered/completed
        /// - Cross-engine analysis:
        ///   - swkotor.exe: Similar plot system (needs reverse engineering)
        ///   - nwmain.exe: Different plot system (needs reverse engineering)
        ///   - daorigins.exe: Plot system may differ (needs reverse engineering)
        /// </remarks>
        private void ProcessPlotIndex(int plotIndex, float plotXpPercentage, [CanBeNull] string questTag = null, int questEntryIndex = -1)
        {
            if (_gameDataManager == null)
            {
                return;
            }

            // Look up plot.2da row using PlotIndex
            // swkotor2.exe: 0x0057eb20 (AwardPlotXP) looks up plot.2da data
            // K1 (swkotor.exe): TODO - Find equivalent address (needs reverse engineering)
            TwoDA plotTable = _gameDataManager.GetTable("plot");
            if (plotTable == null)
            {
                return;
            }

            // Get row by index (PlotIndex is row index in plot.2da)
            if (plotIndex < 0 || plotIndex >= plotTable.GetHeight())
            {
                return;
            }

            TwoDARow plotRow = plotTable.GetRow(plotIndex);
            if (plotRow == null)
            {
                return;
            }

            // Get plot label and XP from plot.2da
            string plotLabel = plotRow.GetString("label", string.Empty);
            int? baseXP = plotRow.GetInteger("xp");

            // Register plot in plot system (if plot system is available)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Plot state is tracked to prevent duplicate processing
            // Original implementation tracks which plots have been triggered
            if (_plotSystem != null)
            {
                _plotSystem.RegisterPlot(plotIndex, plotLabel);
            }

            // Check if plot has already been triggered to prevent duplicate XP awards
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Plot completion is tracked to prevent duplicate XP awards
            // Original implementation: Only awards XP if plot has not been triggered before
            bool plotAlreadyTriggered = false;
            if (_plotSystem != null)
            {
                plotAlreadyTriggered = _plotSystem.IsPlotTriggered(plotIndex);
            }

            // Process plot XP (swkotor2.exe: 0x005e6870 (ProcessPlotXPThreshold) -> 0x0057eb20 (AwardPlotXP))
            // K1 (swkotor.exe): TODO - Find equivalent addresses (needs reverse engineering)
            // Original implementation flow:
            //   1. ProcessPlotXPThreshold checks if plotIndex != -1 and threshold < plotXpPercentage
            //   2. Calculate: plotXpPercentage * _DAT_007b99b4 (base multiplier)
            //   3. Call AwardPlotXP with plotIndex and calculated value
            //   4. AwardPlotXP calculates: (plotXP * param_2) * _DAT_007b5f88 (additional multiplier)
            //   5. Award XP via 0x0057ccd0 (party XP award function)
            //   6. Notify journal system via 0x00681a10
            if (!plotAlreadyTriggered &&
                PLOT_XP_THRESHOLD < plotXpPercentage &&
                _partySystem != null &&
                baseXP.HasValue &&
                baseXP.Value > 0)
            {
                // Step 1: Calculate base multiplier value (swkotor2.exe: 0x005e6870 (ProcessPlotXPThreshold))
                // Calculates: plotXpPercentage * _DAT_007b99b4 (base multiplier)
                // This is param_2 passed to AwardPlotXP
                float multiplierValue = plotXpPercentage * PLOT_XP_BASE_MULTIPLIER;

                // Step 2: Calculate final XP (swkotor2.exe: 0x0057eb20 (AwardPlotXP))
                // Calculates: (plotXP * param_2) * _DAT_007b5f88 (additional multiplier)
                // Where plotXP is baseXP from plot.2da and param_2 is multiplierValue from step 1
                // Original implementation: (baseXP * multiplierValue) * additionalMultiplier
                int finalXP = (int)((baseXP.Value * multiplierValue) * PLOT_XP_ADDITIONAL_MULTIPLIER);

                if (finalXP > 0)
                {
                    // Award XP to all active party members (swkotor2.exe: 0x0057ccd0 @ 0x0057ccd0)
                    // Original implementation awards XP to all active party members
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057ccd0 awards XP to party
                    _partySystem.AwardXP(finalXP);

                    // Notify journal system (swkotor2.exe: 0x00681a10 @ 0x00681a10)
                    // Original implementation: Journal system is notified when XP is awarded via plot
                    // This allows journal to track XP rewards and update UI accordingly
                    // 0x00681a10: Updates journal entry XPReward when plot XP is awarded
                    // Original implementation flow:
                    //   1. Finds journal entry associated with plot/quest
                    //   2. Updates entry XPReward with plot XP amount
                    //   3. Triggers journal UI update to display XP reward
                    NotifyJournalOfPlotXP(plotLabel, finalXP, questTag, questEntryIndex);
                }
            }

            // Trigger plot state tracking
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Plot state is tracked to prevent duplicate processing
            // Original implementation tracks which plots have been triggered
            if (_plotSystem != null)
            {
                _plotSystem.TriggerPlot(plotIndex, plotLabel);
            }

            // Update quest/journal state if plot label matches a quest
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Plot labels can be used as quest identifiers
            // Original implementation: If plot label matches a quest tag, update quest state
            if (_journalSystem != null && !string.IsNullOrEmpty(plotLabel))
            {
                // Check if plot label matches a registered quest
                QuestData quest = _journalSystem.GetQuest(plotLabel);
                if (quest != null)
                {
                    // Update quest state to indicate plot has been triggered
                    // Original implementation may set quest state to "in progress" or "completed"
                    int currentState = _journalSystem.GetQuestState(plotLabel);
                    if (currentState == 0)
                    {
                        // Quest not started yet - start it
                        _journalSystem.SetQuestState(plotLabel, 1);
                    }
                }
            }
        }

        /// <summary>
        /// Notifies journal system when plot XP is awarded (swkotor2.exe: 0x00681a10 @ 0x00681a10).
        /// </summary>
        /// <param name="plotLabel">Plot label from plot.2da</param>
        /// <param name="plotXP">XP amount awarded via plot</param>
        /// <param name="questTag">Quest tag from dialogue node (if available)</param>
        /// <param name="questEntryIndex">Quest entry index from dialogue node (if available)</param>
        /// <remarks>
        /// Journal Notification for Plot XP (swkotor2.exe: 0x00681a10 @ 0x00681a10):
        /// - Called from AwardPlotXP (0x0057eb20) when plot XP is awarded
        /// - Original implementation:
        ///   1. Finds journal entry associated with plot/quest
        ///   2. Updates entry XPReward with plot XP amount
        ///   3. Triggers journal UI update to display XP reward
        /// - Entry lookup priority:
        ///   1. Use questTag and questEntryIndex from dialogue node (most accurate)
        ///   2. Use plotLabel as quest tag and find latest entry (fallback)
        ///   3. If no entry found, create one with plot XP (ensures XP is tracked)
        /// - Original engine behavior: Journal entries track XP rewards from plot awards
        ///   This allows the journal UI to display XP rewards associated with quest progress
        /// </remarks>
        private void NotifyJournalOfPlotXP(string plotLabel, int plotXP, [CanBeNull] string questTag, int questEntryIndex)
        {
            if (_journalSystem == null || plotXP <= 0)
            {
                return;
            }

            // Priority 1: Use quest tag and entry index from dialogue node (most accurate)
            // This matches the journal entry that was just created/updated in ProcessQuestFields
            if (!string.IsNullOrEmpty(questTag) && questEntryIndex >= 0)
            {
                JournalEntry entry = _journalSystem.GetEntryByState(questTag, questEntryIndex);
                if (entry != null)
                {
                    // Update the entry's XPReward with plot XP
                    // Original implementation: Plot XP replaces or adds to existing XPReward
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) behavior: Plot XP is the actual reward, so we set it
                    string entryText = entry.Text ?? string.Empty;
                    _journalSystem.UpdateEntry(questTag, questEntryIndex, entryText, plotXP);
                    return;
                }

                // If entry doesn't exist yet, try to find latest entry for this quest
                JournalEntry latestEntry = _journalSystem.GetLatestEntryForQuest(questTag);
                if (latestEntry != null)
                {
                    // Update latest entry with plot XP
                    string entryText = latestEntry.Text ?? string.Empty;
                    _journalSystem.UpdateEntry(questTag, latestEntry.State, entryText, plotXP);
                    return;
                }
            }

            // Priority 2: Use plotLabel as quest tag (fallback)
            // Plot labels from plot.2da can match quest tags
            if (!string.IsNullOrEmpty(plotLabel))
            {
                // Check if plotLabel matches a registered quest
                QuestData quest = _journalSystem.GetQuest(plotLabel);
                if (quest != null)
                {
                    // Find latest entry for this quest
                    JournalEntry latestEntry = _journalSystem.GetLatestEntryForQuest(plotLabel);
                    if (latestEntry != null)
                    {
                        // Update latest entry with plot XP
                        string latestEntryText = latestEntry.Text ?? string.Empty;
                        _journalSystem.UpdateEntry(plotLabel, latestEntry.State, latestEntryText, plotXP);
                        return;
                    }

                    // If no entry exists, create one with plot XP
                    // This ensures XP rewards are tracked even if no journal entry was created yet
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Journal entries can be created when plot XP is awarded
                    string entryText = GetQuestEntryTextFromJRL(plotLabel, 0);
                    if (string.IsNullOrEmpty(entryText))
                    {
                        // Use empty text if JRL lookup fails (original engine behavior)
                        entryText = string.Empty;
                    }
                    _journalSystem.AddEntry(plotLabel, 0, entryText, plotXP);
                }
            }
        }

        /// <summary>
        /// Gets quest entry text from JRL file.
        /// </summary>
        /// <param name="questTag">Quest tag to look up.</param>
        /// <param name="entryId">Entry ID (0-based index in entry list).</param>
        /// <returns>The entry text, or null if not found.</returns>
        /// <remarks>
        /// Quest Entry Text Lookup (swkotor2.exe):
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Quest entry text is loaded from JRL files
        /// - Original implementation:
        ///   1. Loads JRL file by quest tag (or uses global.jrl)
        ///   2. Finds quest by tag in JRL
        ///   3. Finds entry by EntryId in quest's entry list
        ///   4. Returns entry Text (LocalizedString) resolved to string
        /// - JRL files are typically named after quest tags (e.g., "quest_001.jrl")
        /// - Fallback: Uses global.jrl if quest-specific JRL not found
        /// </remarks>
        [CanBeNull]
        private string GetQuestEntryTextFromJRL(string questTag, int entryId)
        {
            if (string.IsNullOrEmpty(questTag) || _jrlLoader == null)
            {
                return null;
            }

            // Try to get text from quest-specific JRL file first
            string entryText = _jrlLoader.GetQuestEntryText(questTag, entryId, questTag);
            if (!string.IsNullOrEmpty(entryText))
            {
                return entryText;
            }

            // Fallback to global.jrl
            entryText = _jrlLoader.GetQuestEntryTextFromGlobal(questTag, entryId);
            if (!string.IsNullOrEmpty(entryText))
            {
                return entryText;
            }

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

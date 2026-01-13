using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common.Dialogue
{
    /// <summary>
    /// Abstract base class for dialogue state across all engines.
    /// </summary>
    /// <remarks>
    /// Base Dialogue State:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyDialogueState, AuroraDialogueState, EclipseDialogueState)
    /// - Common: Active state, paused state, voiceover waiting, time remaining, history tracking
    /// </remarks>
    public abstract class BaseDialogueState
    {
        protected readonly List<object> _history;

        /// <summary>
        /// Protected accessor for history list (for subclasses).
        /// </summary>
        protected List<object> HistoryList
        {
            get { return _history; }
        }

        protected BaseDialogueState(BaseConversationContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            Context = context;
            _history = new List<object>();
            IsActive = true;
            IsPaused = false;
            WaitingForVoiceover = false;
            TimeRemaining = 0f;
        }

        /// <summary>
        /// The conversation context (participants).
        /// </summary>
        public BaseConversationContext Context { get; protected set; }

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
        /// History of traversed nodes.
        /// </summary>
        public IReadOnlyList<object> History
        {
            get { return _history; }
        }

        /// <summary>
        /// Pushes a node to the history.
        /// </summary>
        public void PushHistory(object node)
        {
            if (node != null)
            {
                _history.Add(node);
            }
        }

        /// <summary>
        /// Resets the state for a new conversation.
        /// </summary>
        public virtual void Reset()
        {
            _history.Clear();
            IsActive = true;
            IsPaused = false;
            WaitingForVoiceover = false;
            TimeRemaining = 0f;
        }

        /// <summary>
        /// Gets the speaker entity for the current entry.
        /// </summary>
        [CanBeNull]
        public abstract IEntity GetCurrentSpeaker();

        /// <summary>
        /// Gets the listener entity for the current entry.
        /// </summary>
        [CanBeNull]
        public abstract IEntity GetCurrentListener();
    }
}


using System;
using System.Collections.Generic;

namespace Odyssey.Core.Dialogue
{
    /// <summary>
    /// Runtime representation of a DLG dialogue file.
    /// Contains the dialogue graph structure with entries, replies, and links.
    /// </summary>
    public class RuntimeDialogue
    {
        private readonly List<int> _starterIndices;
        private readonly Dictionary<int, DialogueEntry> _entries;
        private readonly Dictionary<int, DialogueReply> _replies;

        /// <summary>
        /// The dialogue resource reference.
        /// </summary>
        public string ResRef { get; set; }

        /// <summary>
        /// Whether the dialogue can be skipped by the player.
        /// </summary>
        public bool Skippable { get; set; }

        /// <summary>
        /// Conversation type (Human, Computer, Other).
        /// </summary>
        public int ConversationType { get; set; }

        /// <summary>
        /// Computer type for computer dialogues.
        /// </summary>
        public int ComputerType { get; set; }

        /// <summary>
        /// Camera model override.
        /// </summary>
        public string CameraModel { get; set; }

        /// <summary>
        /// Script to execute when the conversation ends normally.
        /// </summary>
        public string OnEndScript { get; set; }

        /// <summary>
        /// Script to execute when the conversation is aborted.
        /// </summary>
        public string OnAbortScript { get; set; }

        /// <summary>
        /// Ambient track to play during conversation.
        /// </summary>
        public string AmbientTrack { get; set; }

        /// <summary>
        /// Voice-over folder ID.
        /// </summary>
        public string VoiceOverId { get; set; }

        /// <summary>
        /// Whether to unequip items during conversation.
        /// </summary>
        public bool UnequipItems { get; set; }

        /// <summary>
        /// Whether to unequip hands during conversation.
        /// </summary>
        public bool UnequipHands { get; set; }

        /// <summary>
        /// Indices of starter entries.
        /// </summary>
        public IReadOnlyList<int> StarterIndices { get { return _starterIndices; } }

        /// <summary>
        /// Number of entries in the dialogue.
        /// </summary>
        public int EntryCount { get { return _entries.Count; } }

        /// <summary>
        /// Number of replies in the dialogue.
        /// </summary>
        public int ReplyCount { get { return _replies.Count; } }

        public RuntimeDialogue()
        {
            _starterIndices = new List<int>();
            _entries = new Dictionary<int, DialogueEntry>();
            _replies = new Dictionary<int, DialogueReply>();

            ResRef = string.Empty;
            CameraModel = string.Empty;
            OnEndScript = string.Empty;
            OnAbortScript = string.Empty;
            AmbientTrack = string.Empty;
            VoiceOverId = string.Empty;
            Skippable = true;
        }

        /// <summary>
        /// Adds a starter entry index.
        /// </summary>
        public void AddStarter(int entryIndex)
        {
            _starterIndices.Add(entryIndex);
        }

        /// <summary>
        /// Adds an entry to the dialogue.
        /// </summary>
        public void AddEntry(DialogueEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }
            _entries[entry.Index] = entry;
        }

        /// <summary>
        /// Adds a reply to the dialogue.
        /// </summary>
        public void AddReply(DialogueReply reply)
        {
            if (reply == null)
            {
                throw new ArgumentNullException("reply");
            }
            _replies[reply.Index] = reply;
        }

        /// <summary>
        /// Gets an entry by index.
        /// </summary>
        public DialogueEntry GetEntry(int index)
        {
            DialogueEntry entry;
            if (_entries.TryGetValue(index, out entry))
            {
                return entry;
            }
            return null;
        }

        /// <summary>
        /// Gets a reply by index.
        /// </summary>
        public DialogueReply GetReply(int index)
        {
            DialogueReply reply;
            if (_replies.TryGetValue(index, out reply))
            {
                return reply;
            }
            return null;
        }

        /// <summary>
        /// Gets all entries.
        /// </summary>
        public IEnumerable<DialogueEntry> GetAllEntries()
        {
            return _entries.Values;
        }

        /// <summary>
        /// Gets all replies.
        /// </summary>
        public IEnumerable<DialogueReply> GetAllReplies()
        {
            return _replies.Values;
        }
    }

    /// <summary>
    /// Base class for dialogue nodes (entries and replies).
    /// </summary>
    public abstract class DialogueNode
    {
        /// <summary>
        /// Index in the entry/reply list.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Displayed text (localized).
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Script to execute when this node is entered (Script1).
        /// </summary>
        public string Script1 { get; set; }

        /// <summary>
        /// Additional script (Script2).
        /// </summary>
        public string Script2 { get; set; }

        /// <summary>
        /// Conditional script for link availability.
        /// </summary>
        public string ConditionalScript { get; set; }

        /// <summary>
        /// Voice-over resource reference.
        /// </summary>
        public string VoiceResRef { get; set; }

        /// <summary>
        /// Sound resource reference.
        /// </summary>
        public string SoundResRef { get; set; }

        /// <summary>
        /// Comment (for toolset use).
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Camera angle index.
        /// </summary>
        public int CameraAngle { get; set; }

        /// <summary>
        /// Camera animation ID.
        /// </summary>
        public int CameraAnimation { get; set; }

        /// <summary>
        /// Delay before advancing (in milliseconds, -1 = auto).
        /// </summary>
        public int Delay { get; set; }

        /// <summary>
        /// Emotion ID for facial animation.
        /// </summary>
        public int EmotionId { get; set; }

        /// <summary>
        /// Facial animation ID.
        /// </summary>
        public int FacialId { get; set; }

        /// <summary>
        /// Quest identifier.
        /// </summary>
        public string Quest { get; set; }

        /// <summary>
        /// Quest entry to set.
        /// </summary>
        public int QuestEntry { get; set; }

        /// <summary>
        /// Plot index for XP rewards.
        /// </summary>
        public int PlotIndex { get; set; }

        /// <summary>
        /// Plot XP percentage (0.0-1.0).
        /// </summary>
        public float PlotXpPercentage { get; set; }

        protected DialogueNode()
        {
            Text = string.Empty;
            Script1 = string.Empty;
            Script2 = string.Empty;
            ConditionalScript = string.Empty;
            VoiceResRef = string.Empty;
            SoundResRef = string.Empty;
            Comment = string.Empty;
            Quest = string.Empty;
            Delay = -1;
            PlotXpPercentage = 1.0f;
        }
    }

    /// <summary>
    /// Dialogue entry - NPC line.
    /// </summary>
    public class DialogueEntry : DialogueNode
    {
        private readonly List<int> _replyLinks;

        /// <summary>
        /// Speaker tag (empty = owner, otherwise a creature tag).
        /// </summary>
        public string Speaker { get; set; }

        /// <summary>
        /// Listener tag (empty = player).
        /// </summary>
        public string Listener { get; set; }

        /// <summary>
        /// Indices of linked replies.
        /// </summary>
        public IReadOnlyList<int> ReplyLinks { get { return _replyLinks; } }

        public DialogueEntry()
        {
            _replyLinks = new List<int>();
            Speaker = string.Empty;
            Listener = string.Empty;
        }

        /// <summary>
        /// Adds a link to a reply.
        /// </summary>
        public void AddReplyLink(int replyIndex)
        {
            _replyLinks.Add(replyIndex);
        }
    }

    /// <summary>
    /// Dialogue reply - Player choice.
    /// </summary>
    public class DialogueReply : DialogueNode
    {
        private readonly List<int> _entryLinks;

        /// <summary>
        /// Indices of linked entries.
        /// </summary>
        public IReadOnlyList<int> EntryLinks { get { return _entryLinks; } }

        public DialogueReply()
        {
            _entryLinks = new List<int>();
        }

        /// <summary>
        /// Adds a link to an entry.
        /// </summary>
        public void AddEntryLink(int entryIndex)
        {
            _entryLinks.Add(entryIndex);
        }
    }
}

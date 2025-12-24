using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:83-88
    // Original: class JRLQuestPriority(IntEnum):
    public enum JRLQuestPriority
    {
        Highest = 0,
        High = 1,
        Medium = 2,
        Low = 3,
        Lowest = 4
    }

    /// <summary>
    /// Stores journal (quest) data.
    ///
    /// JRL files are GFF-based format files that store journal/quest data including
    /// quest entries, priorities, and planet associations.
    /// </summary>
    [PublicAPI]
    public sealed class JRL
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:29
        // Original: BINARY_TYPE = ResourceType.JRL
        public static readonly ResourceType BinaryType = ResourceType.JRL;

        public List<JRLQuest> Quests { get; set; } = new List<JRLQuest>();

        public JRL()
        {
        }
    }

    /// <summary>
    /// Stores data of an individual quest.
    /// </summary>
    [PublicAPI]
    public sealed class JRLQuest
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:37-60
        // Original: class JRLQuest:
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public int PlanetId { get; set; }
        public int PlotIndex { get; set; }
        public JRLQuestPriority Priority { get; set; } = JRLQuestPriority.Lowest;
        public string Tag { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public List<JRLQuestEntry> Entries { get; set; } = new List<JRLQuestEntry>();

        public JRLQuest()
        {
        }
    }

    /// <summary>
    /// Stores a quest entry (journal entry).
    /// </summary>
    [PublicAPI]
    public sealed class JRLQuestEntry
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:63-80
        // Original: class JRLEntry:
        public LocalizedString Text { get; set; } = LocalizedString.FromInvalid();
        public bool End { get; set; }
        public int EntryId { get; set; }
        public float XpPercentage { get; set; }

        public JRLQuestEntry()
        {
        }
    }

}

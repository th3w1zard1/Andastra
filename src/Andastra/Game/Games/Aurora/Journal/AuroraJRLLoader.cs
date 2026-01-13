using System;
using System.Collections.Generic;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using Andastra.Game.Games.Common.Journal;

namespace Andastra.Game.Games.Aurora.Journal
{
    /// <summary>
    /// Aurora-specific JRL loader implementation (nwmain.exe - Neverwinter Nights).
    /// </summary>
    /// <remarks>
    /// Aurora JRL Loader (nwmain.exe):
    /// - Based on nwmain.exe: CNWSCreature::ReloadJournalEntries @ 0x14039ddb0
    /// - Located via string references: "Journal" @ 0x140dc9e38, "Quest" @ 0x140de6ff8, "QuestEntry" @ 0x140de7000
    /// - JRL file format: Similar to Odyssey (GFF with "JRL " signature) but may have structural differences
    /// - CNWSDialog::AddJournalEntry @ 0x140419e70 - adds journal entry from dialogue
    /// - Journal entries loaded from JRL files and stored per-creature
    /// - Original implementation: JRL files contain quest definitions similar to Odyssey
    /// - Text lookup process similar to Odyssey but may use different TLK resolution
    /// - JRL files are typically named after quest tags (e.g., "quest_001.jrl")
    /// - Fallback: Uses global.jrl if quest-specific JRL not found
    /// </remarks>
    public class AuroraJRLLoader : BaseJRLLoader
    {
        private TLK _baseTlk;
        private TLK _customTlk;

        public AuroraJRLLoader(Installation installation)
            : base(installation)
        {
            _jrlCache = new Dictionary<string, JRL>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sets the talk tables for LocalizedString resolution.
        /// </summary>
        public override void SetTalkTables(object baseTlk, object customTlk = null)
        {
            _baseTlk = baseTlk as TLK;
            _customTlk = customTlk as TLK;
        }

        /// <summary>
        /// Loads a JRL file by ResRef.
        /// </summary>
        public override JRL LoadJRL(string jrlResRef)
        {
            if (string.IsNullOrEmpty(jrlResRef))
            {
                return null;
            }

            // Check cache first
            if (_jrlCache.TryGetValue(jrlResRef, out JRL cachedJrl))
            {
                return cachedJrl;
            }

            try
            {
                // Load JRL file from installation
                // Based on nwmain.exe: JRL files are loaded from resource system
                // Original implementation: Loads JRL files from chitin.key or module archives
                ResourceResult resource = _installation.Resources.LookupResource(jrlResRef, ResourceType.JRL);
                if (resource == null || resource.Data == null || resource.Data.Length == 0)
                {
                    return null;
                }

                byte[] jrlData = resource.Data;

                // Parse JRL file (Aurora uses same GFF format as Odyssey)
                JRL jrl = JRLHelpers.ReadJrl(jrlData);
                if (jrl != null)
                {
                    // Cache the loaded JRL
                    _jrlCache[jrlResRef] = jrl;
                }

                return jrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuroraJRLLoader] Error loading JRL file '{jrlResRef}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets quest entry text from a JRL file.
        /// </summary>
        public override string GetQuestEntryText(string questTag, int entryId, string jrlResRef = null)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return null;
            }

            // Use quest tag as JRL ResRef if not provided
            if (string.IsNullOrEmpty(jrlResRef))
            {
                jrlResRef = questTag;
            }

            // Load JRL file
            JRL jrl = LoadJRL(jrlResRef) as JRL;
            if (jrl == null)
            {
                return null;
            }

            // Find quest by tag
            JRLQuest quest = null;
            foreach (JRLQuest q in jrl.Quests)
            {
                if (string.Equals(q.Tag, questTag, StringComparison.OrdinalIgnoreCase))
                {
                    quest = q;
                    break;
                }
            }

            if (quest == null)
            {
                return null;
            }

            // Find entry by EntryId
            // Note: EntryId in JRL is the actual ID stored in the file
            // We need to match by EntryId, not by index
            JRLQuestEntry entry = null;
            foreach (JRLQuestEntry e in quest.Entries)
            {
                if (e.EntryId == entryId)
                {
                    entry = e;
                    break;
                }
            }

            // If not found by EntryId, try by index (0-based)
            if (entry == null && entryId >= 0 && entryId < quest.Entries.Count)
            {
                entry = quest.Entries[entryId];
            }

            if (entry == null)
            {
                return null;
            }

            // Resolve LocalizedString to text
            LocalizedString locString = entry.Text;
            if (locString == null || locString.IsInvalid)
            {
                return null;
            }

            // Resolve using TLK tables (Aurora uses same TLK format as Odyssey)
            if (_baseTlk != null)
            {
                string text = _baseTlk.String(locString.StringRef);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            if (_customTlk != null)
            {
                string text = _customTlk.String(locString.StringRef);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            // Fallback: return string ID if TLK not available
            return locString.StringRef.ToString();
        }

        /// <summary>
        /// Gets quest entry text from global.jrl file.
        /// </summary>
        public override string GetQuestEntryTextFromGlobal(string questTag, int entryId)
        {
            return GetQuestEntryText(questTag, entryId, "global");
        }

        /// <summary>
        /// Gets a quest by tag from a JRL file.
        /// </summary>
        public override object GetQuestByTag(string questTag, string jrlResRef = null)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return null;
            }

            // Use quest tag as JRL ResRef if not provided
            if (string.IsNullOrEmpty(jrlResRef))
            {
                jrlResRef = questTag;
            }

            // Load JRL file
            JRL jrl = LoadJRL(jrlResRef) as JRL;
            if (jrl == null)
            {
                return null;
            }

            // Find quest by tag
            foreach (JRLQuest quest in jrl.Quests)
            {
                if (string.Equals(quest.Tag, questTag, StringComparison.OrdinalIgnoreCase))
                {
                    return quest;
                }
            }

            return null;
        }

        /// <summary>
        /// Clears the JRL cache.
        /// </summary>
        public override void ClearCache()
        {
            base.ClearCache();
            _jrlCache.Clear();
        }
    }
}

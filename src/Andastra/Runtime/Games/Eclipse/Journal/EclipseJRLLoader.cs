using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TLK;
using Andastra.Runtime.Games.Common.Journal;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Journal
{
    /// <summary>
    /// Eclipse-specific JRL loader implementation (daorigins.exe, DragonAge2.exe - Dragon Age series).
    /// </summary>
    /// <remarks>
    /// Eclipse JRL Loader (daorigins.exe, DragonAge2.exe):
    /// - Based on daorigins.exe: Quest system
    /// - Located via string references: "Quest" @ 0x00b0849c, "QuestResRef" @ 0x00b084b0
    /// - Eclipse may use a different file format for quest/journal data
    /// - Quest system structure differs from Odyssey/Aurora
    /// - Journal entries may be stored in a different format or location
    /// - Text lookup may use different mechanisms (possibly embedded in quest files or different format)
    /// - For now, we'll attempt to use JRL format similar to Odyssey/Aurora as a fallback
    /// - Full implementation requires reverse engineering of daorigins.exe quest system
    /// </remarks>
    public class EclipseJRLLoader : BaseJRLLoader
    {
        private TLK _baseTlk;
        private TLK _customTlk;
        private readonly Dictionary<string, JRL> _jrlCache;

        public EclipseJRLLoader(Installation installation)
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
        /// Loads a JRL file by ResRef (Eclipse may use different format).
        /// </summary>
        public override object LoadJRL(string jrlResRef)
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
                // Based on daorigins.exe: Quest files may be in different format
                // TODO: STUB - For now, attempt to load as JRL (GFF format) similar to Odyssey/Aurora
                ResourceResult resource = _installation.Resources.LookupResource(jrlResRef, ResourceType.JRL);
                if (resource == null || resource.Data == null || resource.Data.Length == 0)
                {
                    return null;
                }

                byte[] jrlData = resource.Data;

                // Parse JRL file (attempt GFF format first, Eclipse may use different format)
                JRL jrl = JRLHelpers.ReadJrl(jrlData);
                if (jrl != null)
                {
                    // Cache the loaded JRL
                    _jrlCache[jrlResRef] = jrl;
                    base._jrlCache[jrlResRef] = jrl; // Also cache in base class (as object)
                }

                return jrl;
            }
            catch (Exception ex)
            {
                // Eclipse may use different file format - log but don't fail
                Console.WriteLine($"[EclipseJRLLoader] Error loading JRL file '{jrlResRef}': {ex.Message}");
                Console.WriteLine($"[EclipseJRLLoader] Note: Eclipse may use different quest file format");
                return null;
            }
        }

        /// <summary>
        /// Gets quest entry text from a JRL file (Eclipse-specific implementation).
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
            if (locString == null || locString.StringRef < 0)
            {
                return null;
            }

            // Resolve using TLK tables (Eclipse may use different TLK format)
            if (_baseTlk != null)
            {
                TLKEntry tlkEntry = _baseTlk.Get(locString.StringRef);
                if (tlkEntry != null && !string.IsNullOrEmpty(tlkEntry.Text))
                {
                    return tlkEntry.Text;
                }
            }

            if (_customTlk != null)
            {
                TLKEntry tlkEntry = _customTlk.Get(locString.StringRef);
                if (tlkEntry != null && !string.IsNullOrEmpty(tlkEntry.Text))
                {
                    return tlkEntry.Text;
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


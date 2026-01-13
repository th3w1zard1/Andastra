using System;
using System.Collections.Generic;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using Andastra.Game.Games.Common.Journal;
using JetBrains.Annotations;
using JRL = BioWare.NET.Resource.Formats.GFF.Generics.JRL;

namespace Andastra.Game.Games.Odyssey.Journal
{
    /// <summary>
    /// Odyssey-specific JRL loader implementation (swkotor.exe, swkotor2.exe).
    /// </summary>
    /// <remarks>
    /// Odyssey JRL Loader:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): JRL file loading (GFF with "JRL " signature)
    /// - Based on swkotor.exe: Similar JRL system (needs reverse engineering)
    /// - JRL file format: GFF with "JRL " signature containing journal entry definitions
    /// - JRL structure: JRL -> JRLQuest -> JRLQuestEntry
    /// - Each quest has a Tag and a list of entries
    /// - Each entry has EntryId and Text (LocalizedString)
    /// - Quest entry text is looked up from JRL files using quest tag and entry ID
    /// - JRL files are typically named after quest tags (e.g., "quest_001.jrl")
    /// - Fallback: Uses global.jrl if quest-specific JRL not found
    /// </remarks>
    public class OdysseyJRLLoader : BaseJRLLoader
    {
        private TLK _baseTlk;
        private TLK _customTlk;

        public OdysseyJRLLoader(Installation installation)
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
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): JRL files are loaded from resource system
                // Original implementation: Loads JRL files from chitin.key or module archives
                ResourceResult resource = _installation.Resources.LookupResource(jrlResRef, ResourceType.JRL);
                if (resource == null || resource.Data == null || resource.Data.Length == 0)
                {
                    return null;
                }

                byte[] jrlData = resource.Data;

                // Parse JRL file
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
                Console.WriteLine($"[OdysseyJRLLoader] Error loading JRL file '{jrlResRef}': {ex.Message}");
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
            if (locString == null || locString.StringRef == -1)
            {
                return null;
            }

            // Resolve using TLK tables
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


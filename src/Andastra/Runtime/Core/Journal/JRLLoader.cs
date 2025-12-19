using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TLK;
using JetBrains.Annotations;

namespace Andastra.Runtime.Core.Journal
{
    /// <summary>
    /// Loads and caches JRL (Journal) files for quest entry text lookup.
    /// </summary>
    /// <remarks>
    /// JRL Loader (Odyssey-specific):
    /// - Based on swkotor2.exe journal system
    /// - Located via string references: "JOURNAL" @ 0x007bdf44, "NW_JOURNAL" @ 0x007c20e8
    /// - JRL file format: GFF with "JRL " signature containing journal entry definitions
    /// - Original implementation:
    ///   1. JRL files contain quest definitions with entry lists
    ///   2. Each quest has a Tag and a list of entries
    ///   3. Each entry has EntryId and Text (LocalizedString)
    ///   4. Quest entry text is looked up from JRL files using quest tag and entry ID
    ///   5. JRL files are typically named after quest tags (e.g., "quest_001.jrl")
    /// - Text lookup process:
    ///   1. Load JRL file by quest tag (or use global.jrl)
    ///   2. Find quest by tag in JRL
    ///   3. Find entry by EntryId in quest's entry list
    ///   4. Return entry Text (LocalizedString) resolved to string
    /// - Cross-engine analysis:
    ///   - swkotor.exe: Similar JRL system (needs reverse engineering)
    ///   - nwmain.exe: Different journal format (needs reverse engineering)
    ///   - daorigins.exe: Journal system may differ (needs reverse engineering)
    /// </remarks>
    public class JRLLoader
    {
        private readonly Installation _installation;
        private readonly Dictionary<string, JRL> _jrlCache;
        private TLK _baseTlk;
        private TLK _customTlk;

        /// <summary>
        /// Creates a new JRL loader.
        /// </summary>
        /// <param name="installation">The game installation to load JRL files from.</param>
        public JRLLoader(Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException("installation");
            _jrlCache = new Dictionary<string, JRL>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sets the talk tables for LocalizedString resolution.
        /// </summary>
        /// <param name="baseTlk">Base talk table.</param>
        /// <param name="customTlk">Custom talk table (optional).</param>
        public void SetTalkTables(TLK baseTlk, TLK customTlk = null)
        {
            _baseTlk = baseTlk;
            _customTlk = customTlk;
        }

        /// <summary>
        /// Loads a JRL file by ResRef.
        /// </summary>
        /// <param name="jrlResRef">ResRef of the JRL file (without extension).</param>
        /// <returns>The loaded JRL, or null if not found.</returns>
        [CanBeNull]
        public JRL LoadJRL(string jrlResRef)
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
                // Based on swkotor2.exe: JRL files are loaded from resource system
                // Original implementation: Loads JRL files from chitin.key or module archives
                ResourceResult resource = _installation.Resources.LookupResource(jrlResRef, ResourceType.JRL);
                if (resource == null || resource.Data == null || resource.Data.Length == 0)
                {
                    return null;
                }

                byte[] jrlData = resource.Data;
                if (jrlData == null || jrlData.Length == 0)
                {
                    return null;
                }

                // Parse JRL file
                JRL jrl = JRLHelper.ReadJrl(jrlData);
                if (jrl != null)
                {
                    // Cache the loaded JRL
                    _jrlCache[jrlResRef] = jrl;
                }

                return jrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JRLLoader] Error loading JRL file '{jrlResRef}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets quest entry text from a JRL file.
        /// </summary>
        /// <param name="questTag">Quest tag to look up.</param>
        /// <param name="entryId">Entry ID (0-based index in entry list).</param>
        /// <param name="jrlResRef">Optional JRL ResRef. If null, uses quest tag as ResRef.</param>
        /// <returns>The entry text, or null if not found.</returns>
        [CanBeNull]
        public string GetQuestEntryText(string questTag, int entryId, string jrlResRef = null)
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
            JRL jrl = LoadJRL(jrlResRef);
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
            if (locString == null || !locString.IsValid)
            {
                return null;
            }

            // Resolve using TLK tables
            if (_baseTlk != null)
            {
                string text = _baseTlk.GetString(locString.StringId);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            if (_customTlk != null)
            {
                string text = _customTlk.GetString(locString.StringId);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            // Fallback: return string ID if TLK not available
            return locString.StringId.ToString();
        }

        /// <summary>
        /// Gets quest entry text from global.jrl file.
        /// </summary>
        /// <param name="questTag">Quest tag to look up.</param>
        /// <param name="entryId">Entry ID (0-based index in entry list).</param>
        /// <returns>The entry text, or null if not found.</returns>
        [CanBeNull]
        public string GetQuestEntryTextFromGlobal(string questTag, int entryId)
        {
            return GetQuestEntryText(questTag, entryId, "global");
        }

        /// <summary>
        /// Clears the JRL cache.
        /// </summary>
        public void ClearCache()
        {
            _jrlCache.Clear();
        }

        /// <summary>
        /// Gets all quests from a JRL file.
        /// </summary>
        /// <param name="jrlResRef">ResRef of the JRL file.</param>
        /// <returns>List of quests, or null if JRL not found.</returns>
        [CanBeNull]
        public List<JRLQuest> GetQuestsFromJRL(string jrlResRef)
        {
            JRL jrl = LoadJRL(jrlResRef);
            if (jrl == null)
            {
                return null;
            }

            return jrl.Quests;
        }

        /// <summary>
        /// Gets a quest by tag from a JRL file.
        /// </summary>
        /// <param name="questTag">Quest tag to look up.</param>
        /// <param name="jrlResRef">Optional JRL ResRef. If null, uses quest tag as ResRef.</param>
        /// <returns>The quest, or null if not found.</returns>
        [CanBeNull]
        public JRLQuest GetQuestByTag(string questTag, string jrlResRef = null)
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
            JRL jrl = LoadJRL(jrlResRef);
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
    }
}


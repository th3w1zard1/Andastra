using System;
using System.Collections.Generic;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Extract;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Core.Journal
{
    /// <summary>
    /// Loads and caches JRL (Journal) files for quest entry text lookup (Odyssey-specific implementation).
    /// </summary>
    /// <remarks>
    /// Odyssey JRL Loader (swkotor.exe, swkotor2.exe):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) journal system
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
    ///
    /// This class is maintained for backward compatibility.
    /// New code should use OdysseyJRLLoader directly.
    /// Core cannot depend on Odyssey due to circular dependency, so this is a standalone implementation.
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
                Console.WriteLine($"[JRLLoader] Error loading JRL file '{jrlResRef}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets quest entry text from a JRL file.
        /// </summary>
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
            if (locString == null)
            {
                return null;
            }

            // If StringRef is valid (>= 0), use TLK lookup
            if (locString.StringRef >= 0)
            {
                // Resolve using TLK tables
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

            // If StringRef is -1, use stored substrings (get English male as fallback)
            return locString.Get(Language.English, Gender.Male, useFallback: true);
        }

        /// <summary>
        /// Gets quest entry text from global.jrl file.
        /// </summary>
        [CanBeNull]
        public string GetQuestEntryTextFromGlobal(string questTag, int entryId)
        {
            return GetQuestEntryText(questTag, entryId, "global");
        }

        /// <summary>
        /// Gets a quest by tag from a JRL file.
        /// </summary>
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

        /// <summary>
        /// Clears the JRL cache.
        /// </summary>
        public void ClearCache()
        {
            _jrlCache.Clear();
        }
    }
}

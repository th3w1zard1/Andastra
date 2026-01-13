using System;
using System.Collections.Generic;
using BioWare.NET.Extract.Installation;
using JetBrains.Annotations;
using JRL = BioWare.NET.Resource.Formats.GFF.Generics.JRL;

namespace Andastra.Runtime.Games.Common.Journal
{
    /// <summary>
    /// Base implementation of JRL (Journal) file loader shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base JRL Loader Implementation:
    /// - Common JRL file loading functionality across all engines
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyJRLLoader, AuroraJRLLoader, EclipseJRLLoader)
    ///
    /// Based on reverse engineering of JRL file loading systems across multiple BioWare engines.
    ///
    /// Common functionality across engines:
    /// - JRL file loading from installation
    /// - JRL file caching for performance
    /// - Quest entry text lookup by quest tag and entry ID
    /// - Talk table (TLK) integration for LocalizedString resolution
    ///
    /// Engine-specific differences:
    /// - Odyssey (swkotor.exe, swkotor2.exe): GFF with "JRL " signature, JRLQuest/JRLQuestEntry structure
    /// - Aurora (nwmain.exe): Different JRL format, CNWSJournal structure
    /// - Eclipse (daorigins.exe): May use different file format or structure
    /// </remarks>
    public abstract class BaseJRLLoader
    {
        protected readonly Installation _installation;
        protected Dictionary<string, JRL> _jrlCache;

        /// <summary>
        /// Creates a new JRL loader.
        /// </summary>
        /// <param name="installation">The game installation to load JRL files from.</param>
        protected BaseJRLLoader(Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException("installation");
            _jrlCache = new Dictionary<string, JRL>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sets the talk tables for LocalizedString resolution (engine-specific implementation).
        /// </summary>
        /// <remarks>
        /// Engine-specific subclasses implement this to handle engine-specific TLK formats.
        /// </remarks>
        public abstract void SetTalkTables(object baseTlk, object customTlk = null);

        /// <summary>
        /// Loads a JRL file by ResRef (engine-specific implementation).
        /// </summary>
        /// <param name="jrlResRef">ResRef of the JRL file (without extension).</param>
        /// <returns>The loaded JRL (engine-specific type), or null if not found.</returns>
        /// <remarks>
        /// Engine-specific subclasses implement this to handle engine-specific JRL file formats.
        /// </remarks>
        [CanBeNull]
        public abstract JRL LoadJRL(string jrlResRef);

        /// <summary>
        /// Gets quest entry text from a JRL file (engine-specific implementation).
        /// </summary>
        /// <param name="questTag">Quest tag to look up.</param>
        /// <param name="entryId">Entry ID (0-based index in entry list).</param>
        /// <param name="jrlResRef">Optional JRL ResRef. If null, uses quest tag as ResRef.</param>
        /// <returns>The entry text, or null if not found.</returns>
        /// <remarks>
        /// Engine-specific subclasses implement this to handle engine-specific JRL structures and text lookup.
        /// </remarks>
        [CanBeNull]
        public abstract string GetQuestEntryText(string questTag, int entryId, string jrlResRef = null);

        /// <summary>
        /// Gets quest entry text from global.jrl file (engine-specific implementation).
        /// </summary>
        /// <param name="questTag">Quest tag to look up.</param>
        /// <param name="entryId">Entry ID (0-based index in entry list).</param>
        /// <returns>The entry text, or null if not found.</returns>
        /// <remarks>
        /// Engine-specific subclasses implement this to handle engine-specific global.jrl lookup.
        /// </remarks>
        [CanBeNull]
        public abstract string GetQuestEntryTextFromGlobal(string questTag, int entryId);

        /// <summary>
        /// Clears the JRL cache.
        /// </summary>
        public virtual void ClearCache()
        {
            // Subclasses override this to clear their own caches
        }

        /// <summary>
        /// Gets a quest by tag from a JRL file (engine-specific implementation).
        /// </summary>
        /// <param name="questTag">Quest tag to look up.</param>
        /// <param name="jrlResRef">Optional JRL ResRef. If null, uses quest tag as ResRef.</param>
        /// <returns>The quest (engine-specific type), or null if not found.</returns>
        /// <remarks>
        /// Engine-specific subclasses implement this to handle engine-specific quest structures.
        /// </remarks>
        [CanBeNull]
        public abstract object GetQuestByTag(string questTag, string jrlResRef = null);
    }
}


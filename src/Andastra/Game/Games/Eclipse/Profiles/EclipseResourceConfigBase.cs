using System.Collections.Generic;
using Andastra.Runtime.Engines.Common;

namespace Andastra.Runtime.Engines.Eclipse.Profiles
{
    /// <summary>
    /// Base class for Eclipse engine resource configuration.
    /// Consolidates common resource paths shared by Dragon Age: Origins and Dragon Age 2.
    /// </summary>
    /// <remarks>
    /// Common Eclipse Resource Paths:
    /// - Based on daorigins.exe and DragonAge2.exe resource path resolution
    /// - Eclipse Engine uses package-based resource system (packages/core structure)
    /// - Both DA:O and DA2 use: "packages/core", "packages/core/override", "saves"
    /// - Eclipse does not use chitin.key or dialog.tlk (Odyssey/Aurora specific)
    /// - Eclipse uses RIM files (Resource Index Manifest) instead of key files
    /// - Eclipse uses packages/core structure with PCC/UPK files
    /// - Based on daorigins.exe: "packages\\" @ 0x00ad9810, "packages\\core\\" @ 0x00ad9798
    /// - Based on daorigins.exe: "packages\\core\\override\\" for override resources
    /// - Based on daorigins.exe: "DragonAge::Streaming" @ 0x00ad7a34 for streaming resources
    /// </remarks>
    public abstract class EclipseResourceConfigBase : IResourceConfig
    {
        /// <summary>
        /// Eclipse Engine does not use chitin.key files (Odyssey/Aurora specific).
        /// Returns empty string as Eclipse uses RIM files instead.
        /// </summary>
        public string ChitinKeyFile
        {
            get { return string.Empty; }
        }

        /// <summary>
        /// Eclipse Engine does not use texture pack ERF files (Odyssey specific).
        /// Returns empty list as Eclipse uses package-based texture system.
        /// </summary>
        public abstract IReadOnlyList<string> TexturePackFiles { get; }

        /// <summary>
        /// Eclipse Engine does not use dialog.tlk files (Odyssey/Aurora specific).
        /// Returns empty string as Eclipse uses different dialogue system.
        /// </summary>
        public string DialogTlkFile
        {
            get { return string.Empty; }
        }

        /// <summary>
        /// Eclipse Engine uses "packages/core" as the main resource directory.
        /// This is equivalent to "modules" in Odyssey/Aurora engines.
        /// </summary>
        public string ModulesDirectory
        {
            get { return "packages/core"; }
        }

        /// <summary>
        /// Eclipse Engine uses "packages/core/override" for override resources.
        /// This is equivalent to "override" in Odyssey/Aurora engines.
        /// </summary>
        public string OverrideDirectory
        {
            get { return "packages/core/override"; }
        }

        /// <summary>
        /// Eclipse Engine uses "saves" directory for save games.
        /// Same as Odyssey/Aurora engines.
        /// </summary>
        public string SavesDirectory
        {
            get { return "saves"; }
        }
    }
}


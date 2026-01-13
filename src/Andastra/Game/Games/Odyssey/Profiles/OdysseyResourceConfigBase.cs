using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Game.Games.Odyssey.Profiles
{
    /// <summary>
    /// Base class for Odyssey engine resource configuration.
    /// Consolidates common resource paths shared by K1 and K2.
    /// </summary>
    /// <remarks>
    /// Common Odyssey Resource Paths:
    /// - Based on swkotor.exe and swkotor2.exe resource path resolution
    /// - Both K1 and K2 use: "chitin.key", "dialog.tlk", "modules", "override", "saves"
    /// - Only TexturePackFiles differs between K1 and K2
    /// </remarks>
    public abstract class OdysseyResourceConfigBase : Andastra.Game.Games.Common.IResourceConfig
    {
        public string ChitinKeyFile
        {
            get { return "chitin.key"; }
        }

        public abstract IReadOnlyList<string> TexturePackFiles { get; }

        public string DialogTlkFile
        {
            get { return "dialog.tlk"; }
        }

        public string ModulesDirectory
        {
            get { return "modules"; }
        }

        public string OverrideDirectory
        {
            get { return "override"; }
        }

        public string SavesDirectory
        {
            get { return "saves"; }
        }
    }
}


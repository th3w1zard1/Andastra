using System.Collections.Generic;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    /// <summary>
    /// Stores faction data.
    ///
    /// FAC files are GFF-based format files that store faction information including
    /// faction names, parent relationships, global flags, and reputation values between factions.
    /// </summary>
    [PublicAPI]
    public sealed class FAC
    {
        // Matching PyKotor implementation pattern
        // Original: BINARY_TYPE = ResourceType.FAC
        public static readonly ResourceType BinaryType = ResourceType.FAC;

        // List of factions
        public List<FACFaction> Factions { get; set; } = new List<FACFaction>();

        // List of reputation entries (relationships between factions)
        public List<FACReputation> Reputations { get; set; } = new List<FACReputation>();

        public FAC()
        {
        }
    }

    /// <summary>
    /// Stores data of an individual faction.
    /// </summary>
    [PublicAPI]
    public sealed class FACFaction
    {
        // Engine references: swkotor2.exe:0x005acf30 line 40, swkotor.exe:0x0052b5c0 line 40
        // Engine default: "" (swkotor2.exe:0x005acf30 line 38, swkotor.exe:0x0052b5c0 line 38)
        public string Name { get; set; } = string.Empty;

        // Engine default: 0 (swkotor2.exe:0x005acf30 line 47, swkotor.exe:0x0052b5c0 line 47)
        // Standard factions use 0xFFFFFFFF (-1) for no parent
        public int ParentId { get; set; } = unchecked((int)0xFFFFFFFF);

        // Engine default: 0, but if field missing defaults to 1 (swkotor2.exe:0x005acf30 lines 48-52, swkotor.exe:0x0052b5c0 lines 48-52)
        public bool IsGlobal { get; set; }

        public FACFaction()
        {
        }
    }

    /// <summary>
    /// Stores reputation data between two factions.
    /// </summary>
    [PublicAPI]
    public sealed class FACReputation
    {
        // Engine reference: swkotor2.exe:0x005ad1a0 line 23, swkotor.exe:0x0052b830 line 23
        public int FactionId1 { get; set; }

        // Engine reference: swkotor2.exe:0x005ad1a0 line 24, swkotor.exe:0x0052b830 line 24
        public int FactionId2 { get; set; }

        // Engine default: 100 (swkotor2.exe:0x005ad1a0 line 21, swkotor.exe:0x0052b830 line 20)
        // Note: Only written if != 100, so default is 100
        public int Reputation { get; set; } = 100;

        public FACReputation()
        {
        }
    }
}


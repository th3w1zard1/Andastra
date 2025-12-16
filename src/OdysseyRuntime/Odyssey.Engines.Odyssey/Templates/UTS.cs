using System.Collections.Generic;
using AuroraEngine.Common;
using AuroraEngine.Common.Resources;
using JetBrains.Annotations;

namespace Odyssey.Engines.Odyssey.Templates
{
    // Moved from AuroraEngine.Common.Resource.Generics.UTS to Odyssey.Engines.Odyssey.Templates
    // This is KOTOR/Odyssey-specific GFF template structure
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py:16
    /// <summary>
    /// Stores sound data.
    ///
    /// UTS files are GFF-based format files that store sound object definitions including
    /// audio settings, positioning, looping, and volume controls.
    /// </summary>
    /// <remarks>
    /// UTS (Sound Template) Format:
    /// - Based on swkotor2.exe sound template system
    /// - Located via string references: "Sound" @ 0x007bc544, "SoundList" @ 0x007c0c80, "Sound template '%s' doesn't exist.\n" @ 0x007bf78c
    /// - Sound loading: FUN_005223a0 @ 0x005223a0 loads sound from GFF (construct_uts equivalent)
    /// - Sound saving: FUN_005226d0 @ 0x005226d0 saves sound to GFF (dismantle_uts equivalent)
    /// - Original implementation: UTS files are GFF with "UTS " signature containing sound template data
    /// - GFF fields: TemplateResRef, Tag, LocName, Active, Continuous, Looping, Positional, RandomPosition, Random, Volume, etc.
    /// - Audio settings: Volume, VolumeVrtn, PitchVariation, Elevation, MinDistance, MaxDistance, DistanceCutoff, Priority
    /// - Timing: Hours, Times, Interval, IntervalVrtn
    /// - Sound list: Sounds contains list of ResRef sound files
    /// - Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py:16
    /// </remarks>
    [PublicAPI]
    public sealed class UTS
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py:16
        // Original: BINARY_TYPE = ResourceType.UTS
        public static readonly ResourceType BinaryType = ResourceType.UTS;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py:16-137
        // Basic UTS properties
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public string Tag { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool Continuous { get; set; }
        public bool Looping { get; set; }
        public bool Positional { get; set; }
        public bool RandomPosition { get; set; }
        public bool Random { get; set; }
        public int Volume { get; set; }
        public int VolumeVariance { get; set; }
        public float PitchVariance { get; set; }
        public float Elevation { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        public float DistanceCutoff { get; set; }
        public int Priority { get; set; }
        public int Hours { get; set; }
        public int Times { get; set; }
        public int Interval { get; set; }
        public int IntervalVariance { get; set; }
        public ResRef Sound { get; set; } = ResRef.FromBlank();
        public string Comment { get; set; } = string.Empty;
        public List<ResRef> Sounds { get; set; } = new List<ResRef>();
        public float RandomRangeX { get; set; }
        public float RandomRangeY { get; set; }

        public UTS()
        {
        }
    }
}


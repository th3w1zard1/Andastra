using AuroraEngine.Common;
using AuroraEngine.Common.Resources;
using JetBrains.Annotations;

namespace Odyssey.Engines.Odyssey.Templates
{
    // Moved from AuroraEngine.Common.Resource.Generics.UTW to Odyssey.Engines.Odyssey.Templates
    // This is KOTOR/Odyssey-specific GFF template structure
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:15
    /// <summary>
    /// Stores waypoint data.
    ///
    /// UTW files are GFF-based format files that store waypoint definitions including
    /// map notes, appearance, and location data.
    /// </summary>
    /// <remarks>
    /// UTW (Waypoint Template) Format:
    /// - Based on swkotor2.exe waypoint template system
    /// - Located via string references: "Waypoint" @ 0x007bc544, "WaypointList" @ 0x007c0c80, "Waypoint template '%s' doesn't exist.\n" @ 0x007bf78c
    /// - Waypoint loading: FUN_005223a0 @ 0x005223a0 loads waypoint from GFF (construct_utw equivalent)
    /// - Waypoint saving: FUN_005226d0 @ 0x005226d0 saves waypoint to GFF (dismantle_utw equivalent)
    /// - Original implementation: UTW files are GFF with "UTW " signature containing waypoint template data
    /// - GFF fields: TemplateResRef, Tag, LocalizedName, Appearance, HasMapNote, MapNote, MapNoteEnabled, etc.
    /// - Map notes: HasMapNote, MapNote (LocalizedString), MapNoteEnabled for waypoint map display
    /// - Appearance: Appearance (UInt8) for waypoint visual representation
    /// - Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:15
    /// </remarks>
    [PublicAPI]
    public sealed class UTW
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:15
        // Original: BINARY_TYPE = ResourceType.UTW
        public static readonly ResourceType BinaryType = ResourceType.UTW;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:89-107
        // Original: UTW properties initialization
        // Basic UTW properties
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public string Tag { get; set; } = string.Empty;
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public bool HasMapNote { get; set; }
        public bool MapNoteEnabled { get; set; }
        public LocalizedString MapNote { get; set; } = LocalizedString.FromInvalid();
        public int AppearanceId { get; set; }
        public int PaletteId { get; set; }
        public string Comment { get; set; } = string.Empty;
        
        // Deprecated fields
        public string LinkedTo { get; set; } = string.Empty;
        public LocalizedString Description { get; set; } = LocalizedString.FromInvalid();

        public UTW()
        {
        }
    }
}


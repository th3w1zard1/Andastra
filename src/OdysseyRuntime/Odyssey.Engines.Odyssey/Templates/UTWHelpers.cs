using AuroraEngine.Common;
using AuroraEngine.Common.Formats.GFF;
using AuroraEngine.Common.Resources;
using static AuroraEngine.Common.GameExtensions;

namespace Odyssey.Engines.Odyssey.Templates
{
    // Moved from AuroraEngine.Common.Resource.Generics.UTWHelpers to Odyssey.Engines.Odyssey.Templates
    // This is KOTOR/Odyssey-specific GFF template helper functions
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:110-152
    // Original: construct_utw and dismantle_utw functions
    /// <remarks>
    /// UTW Helper Functions:
    /// - Based on swkotor2.exe waypoint template loading/saving system
    /// - Located via string references: "Waypoint" @ 0x007bc544, "WaypointList" @ 0x007c0c80, "Waypoint template '%s' doesn't exist.\n" @ 0x007bf78c
    /// - Waypoint loading: FUN_005223a0 @ 0x005223a0 loads waypoint from GFF (construct_utw equivalent)
    /// - Waypoint saving: FUN_005226d0 @ 0x005226d0 saves waypoint to GFF (dismantle_utw equivalent)
    /// - ConstructUtw: Parses GFF structure into UTW object, extracts all fields (map notes, appearance, location data)
    /// - DismantleUtw: Converts UTW object back to GFF structure, writes all fields in correct format
    /// - GFF field names match original engine exactly (TemplateResRef, Tag, LocalizedName, Appearance, HasMapNote, MapNote, MapNoteEnabled, etc.)
    /// - Map notes: HasMapNote (UInt8), MapNote (LocalizedString), MapNoteEnabled (UInt8) for waypoint map display
    /// - Appearance: Appearance (UInt8) for waypoint visual representation
    /// - Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:110-152
    /// </remarks>
    public static class UTWHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:110-128
        // Original: def construct_utw(gff: GFF) -> UTW:
        public static UTW ConstructUtw(GFF gff)
        {
            var utw = new UTW();
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:116-126
            // Original: Extract all UTW fields from GFF root
            utw.AppearanceId = root.Acquire<int>("Appearance", 0);
            utw.LinkedTo = root.Acquire<string>("LinkedTo", "");
            utw.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            utw.Tag = root.Acquire<string>("Tag", "");
            utw.Name = root.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
            utw.Description = root.Acquire<LocalizedString>("Description", LocalizedString.FromInvalid());
            utw.HasMapNote = root.Acquire<bool>("HasMapNote", false);
            utw.MapNote = root.Acquire<LocalizedString>("MapNote", LocalizedString.FromInvalid());
            utw.MapNoteEnabled = root.Acquire<bool>("MapNoteEnabled", false);
            utw.PaletteId = root.Acquire<int>("PaletteID", 0);
            utw.Comment = root.Acquire<string>("Comment", "");

            return utw;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:131-152
        // Original: def dismantle_utw(utw: UTW, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtw(UTW utw, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTW);
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:140-150
            // Original: Set all UTW fields in GFF root
            root.SetUInt8("Appearance", (byte)utw.AppearanceId);
            root.SetString("LinkedTo", utw.LinkedTo);
            root.SetResRef("TemplateResRef", utw.ResRef);
            root.SetString("Tag", utw.Tag);
            root.SetLocString("LocalizedName", utw.Name);
            root.SetLocString("Description", utw.Description);
            root.SetUInt8("HasMapNote", utw.HasMapNote ? (byte)1 : (byte)0);
            root.SetLocString("MapNote", utw.MapNote);
            root.SetUInt8("MapNoteEnabled", utw.MapNoteEnabled ? (byte)1 : (byte)0);
            root.SetUInt8("PaletteID", (byte)utw.PaletteId);
            root.SetString("Comment", utw.Comment);

            return gff;
        }
    }
}


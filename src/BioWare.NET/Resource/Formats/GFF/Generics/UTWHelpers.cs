using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:110-152
    // Original: construct_utw and dismantle_utw functions
    public static class UTWHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:110-128
        // Original: def construct_utw(gff: GFF) -> UTW:
        // Engine loading functions: swkotor2.exe:0x0056f5a0, swkotor.exe:0x005c7f30
        public static UTW ConstructUtw(GFF gff)
        {
            var utw = new UTW();
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:116-126
            // Original: Extract all UTW fields from GFF root
            // Note: Appearance is NOT read by the engine (swkotor2.exe:0x0056f5a0, swkotor.exe:0x005c7f30)
            utw.AppearanceId = root.Acquire<int>("Appearance", 0);
            // Note: LinkedTo is NOT read by the engine (swkotor2.exe:0x0056f5a0, swkotor.exe:0x005c7f30)
            utw.LinkedTo = root.Acquire<string>("LinkedTo", "");
            // Note: TemplateResRef is NOT read by the engine (swkotor2.exe:0x0056f5a0, swkotor.exe:0x005c7f30)
            utw.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // Engine default: "" (swkotor2.exe:0x0056f5a0 line 43, swkotor.exe:0x005c7f30 line 43)
            utw.Tag = root.Acquire<string>("Tag", "");
            // Engine default: existing value (invalid LocalizedString for new objects) (swkotor2.exe:0x0056f5a0 line 52, swkotor.exe:0x005c7f30 line 52)
            utw.Name = root.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
            // Note: Description is NOT read by the engine (swkotor2.exe:0x0056f5a0, swkotor.exe:0x005c7f30)
            utw.Description = root.Acquire<LocalizedString>("Description", LocalizedString.FromInvalid());
            // HasMapNote and MapNoteEnabled are stored as UInt8 (0 or 1), need to read as byte and convert
            // Engine default: 0 (swkotor2.exe:0x0056f5a0 line 77, swkotor.exe:0x005c7f30 line 77)
            utw.HasMapNote = root.GetUInt8("HasMapNote") != 0;
            // Engine default: existing value (invalid LocalizedString for new objects), only read if HasMapNote is true (swkotor2.exe:0x0056f5a0 line 84, swkotor.exe:0x005c7f30 line 84)
            utw.MapNote = root.Acquire<LocalizedString>("MapNote", LocalizedString.FromInvalid());
            // Engine default: 0, only read if HasMapNote is true (swkotor2.exe:0x0056f5a0 line 80, swkotor.exe:0x005c7f30 line 80)
            utw.MapNoteEnabled = root.GetUInt8("MapNoteEnabled") != 0;
            // Note: PaletteID is NOT read by the engine (swkotor2.exe:0x0056f5a0, swkotor.exe:0x005c7f30)
            utw.PaletteId = root.Acquire<int>("PaletteID", 0);
            // Note: Comment is NOT read by the engine (swkotor2.exe:0x0056f5a0, swkotor.exe:0x005c7f30)
            utw.Comment = root.Acquire<string>("Comment", "");

            return utw;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:131-152
        // Original: def dismantle_utw(utw: UTW, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtw(UTW utw, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
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

using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using static Andastra.Parsing.Common.GameExtensions;

namespace Andastra.Parsing.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py
    // Original: construct_uts and dismantle_uts functions
    public static class UTSHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py:199-234
        // Original: def construct_uts(gff: GFF) -> UTS:
        // Engine loading functions: swkotor2.exe:0x005706b0, swkotor.exe:0x005c6cd0
        public static UTS ConstructUts(GFF gff)
        {
            var uts = new UTS();
            var root = gff.Root;

            // Extract basic fields
            // Engine default: "" (swkotor2.exe:0x005706b0 line 43, swkotor.exe:0x005c6cd0 line 43)
            uts.Tag = root.Acquire<string>("Tag", "");
            // Note: TemplateResRef is NOT read by the engine (swkotor2.exe:0x005706b0, swkotor.exe:0x005c6cd0)
            uts.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // Boolean fields stored as UInt8 - use GetUInt8() != 0 (matching UTW fix)
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 49, swkotor.exe:0x005c6cd0 line 49)
            uts.Active = root.GetUInt8("Active") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 82, swkotor.exe:0x005c6cd0 line 82)
            uts.Continuous = root.GetUInt8("Continuous") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 54, swkotor.exe:0x005c6cd0 line 54)
            uts.Looping = root.GetUInt8("Looping") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 51, swkotor.exe:0x005c6cd0 line 51)
            uts.Positional = root.GetUInt8("Positional") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 90, swkotor.exe:0x005c6cd0 line 90)
            uts.RandomPosition = root.GetUInt8("RandomPosition") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 85, swkotor.exe:0x005c6cd0 line 85)
            uts.Random = root.GetUInt8("Random") != 0;
            // Note: LocName is NOT read by the engine (swkotor2.exe:0x005706b0, swkotor.exe:0x005c6cd0)
            uts.Name = root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid());
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 57, swkotor.exe:0x005c6cd0 line 57)
            uts.Volume = root.Acquire<int>("Volume", 0);
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 59, swkotor.exe:0x005c6cd0 line 59)
            uts.VolumeVariance = root.Acquire<int>("VolumeVrtn", 0);
            // Engine default: existing value, but 0.0 for new objects (swkotor2.exe:0x005706b0 line 65, swkotor.exe:0x005c6cd0 line 65)
            uts.PitchVariance = root.Acquire<float>("PitchVariation", 0.0f);
            // Note: Elevation is NOT read by the engine (swkotor2.exe:0x005706b0, swkotor.exe:0x005c6cd0)
            uts.Elevation = root.Acquire<float>("Elevation", 0.0f);
            // Engine default: existing value, but 0.0 for new objects (swkotor2.exe:0x005706b0 line 77, swkotor.exe:0x005c6cd0 line 77)
            uts.MinDistance = root.Acquire<float>("MinDistance", 0.0f);
            // Engine default: existing value, but 0.0 for new objects (swkotor2.exe:0x005706b0 line 80, swkotor.exe:0x005c6cd0 line 80)
            uts.MaxDistance = root.Acquire<float>("MaxDistance", 0.0f);
            // Note: DistanceCutoff is NOT read by the engine (swkotor2.exe:0x005706b0, swkotor.exe:0x005c6cd0)
            uts.DistanceCutoff = root.Acquire<float>("DistanceCutoff", 0.0f);
            // Note: Priority is NOT read by the engine (swkotor2.exe:0x005706b0, swkotor.exe:0x005c6cd0)
            uts.Priority = root.Acquire<int>("Priority", 0);
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 67, swkotor.exe:0x005c6cd0 line 67)
            uts.Hours = root.Acquire<int>("Hours", 0);
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 62, swkotor.exe:0x005c6cd0 line 62)
            uts.Times = root.Acquire<int>("Times", 0);
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 72, swkotor.exe:0x005c6cd0 line 72)
            uts.Interval = (int)root.GetUInt32("Interval");
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x005706b0 line 74, swkotor.exe:0x005c6cd0 line 74)
            uts.IntervalVariance = (int)root.GetUInt32("IntervalVrtn");
            // Note: Sound is NOT read by the engine (swkotor2.exe:0x005706b0, swkotor.exe:0x005c6cd0)
            uts.Sound = root.Acquire<ResRef>("Sound", ResRef.FromBlank());
            // Note: Comment is NOT read by the engine (swkotor2.exe:0x005706b0, swkotor.exe:0x005c6cd0)
            uts.Comment = root.Acquire<string>("Comment", "");
            // Engine default: existing value, but 0.0 for new objects (swkotor2.exe:0x005706b0 line 94, swkotor.exe:0x005c6cd0 line 94)
            uts.RandomRangeX = root.Acquire<float>("RandomRangeX", 0.0f);
            // Engine default: existing value, but 0.0 for new objects (swkotor2.exe:0x005706b0 line 97, swkotor.exe:0x005c6cd0 line 97)
            uts.RandomRangeY = root.Acquire<float>("RandomRangeY", 0.0f);

            // Extract sounds list
            // Engine default: empty list (engine skips if missing) (swkotor2.exe:0x005706b0 line 106, swkotor.exe:0x005c6cd0 line 106)
            var soundsList = root.Acquire<GFFList>("Sounds", new GFFList());
            uts.Sounds.Clear();
            foreach (var soundStruct in soundsList)
            {
                // Engine default: "" (blank ResRef) (swkotor2.exe:0x005706b0 line 115, swkotor.exe:0x005c6cd0 line 115)
                var sound = soundStruct.Acquire<ResRef>("Sound", ResRef.FromBlank());
                if (sound != null && !string.IsNullOrEmpty(sound.ToString()))
                {
                    uts.Sounds.Add(sound);
                }
            }

            return uts;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py:237-311
        // Original: def dismantle_uts(uts: UTS, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUts(UTS uts, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTS);
            var root = gff.Root;

            // Set basic fields
            root.SetString("Tag", uts.Tag);
            root.SetResRef("TemplateResRef", uts.ResRef);
            root.SetUInt8("Active", uts.Active ? (byte)1 : (byte)0);
            root.SetUInt8("Continuous", uts.Continuous ? (byte)1 : (byte)0);
            root.SetUInt8("Looping", uts.Looping ? (byte)1 : (byte)0);
            root.SetUInt8("Positional", uts.Positional ? (byte)1 : (byte)0);
            root.SetUInt8("RandomPosition", uts.RandomPosition ? (byte)1 : (byte)0);
            root.SetUInt8("Random", uts.Random ? (byte)1 : (byte)0);
            root.SetUInt8("Volume", (byte)uts.Volume);
            root.SetUInt8("VolumeVrtn", (byte)uts.VolumeVariance);
            root.SetSingle("PitchVariation", uts.PitchVariance);
            root.SetSingle("Elevation", uts.Elevation);
            root.SetSingle("MinDistance", uts.MinDistance);
            root.SetSingle("MaxDistance", uts.MaxDistance);
            root.SetSingle("DistanceCutoff", uts.DistanceCutoff);
            root.SetUInt8("Priority", (byte)uts.Priority);
            root.SetUInt32("Interval", (uint)uts.Interval);
            root.SetUInt32("IntervalVrtn", (uint)uts.IntervalVariance);
            root.SetResRef("Sound", uts.Sound);
            root.SetString("Comment", uts.Comment);
            root.SetSingle("RandomRangeX", uts.RandomRangeX);
            root.SetSingle("RandomRangeY", uts.RandomRangeY);
            root.SetUInt8("PaletteID", 0);
            if (useDeprecated)
            {
                root.SetLocString("LocName", uts.Name);
                root.SetUInt32("Hours", (uint)uts.Hours);
                root.SetUInt8("Times", (byte)uts.Times);
            }
            else
            {
                // Always set LocName even if not deprecated
                root.SetLocString("LocName", uts.Name);
            }

            // Set sounds list
            var soundsList = new GFFList();
            root.SetList("Sounds", soundsList);
            if (uts.Sounds != null)
            {
                foreach (var sound in uts.Sounds)
                {
                    var soundStruct = soundsList.Add(2);
                    soundStruct.SetResRef("Sound", sound);
                }
            }

            return gff;
        }
    }
}

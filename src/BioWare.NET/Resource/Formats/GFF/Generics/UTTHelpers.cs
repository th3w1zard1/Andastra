using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:212-264
    // Original: construct_utt and dismantle_utt functions
    public static class UTTHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:212-264
        // Original: def construct_utt(gff: GFF) -> UTT:
        // Engine loading functions: swkotor2.exe:0x00584f40, swkotor.exe:0x0058da80
        public static UTT ConstructUtt(GFF gff)
        {
            var utt = new UTT();
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:236-262
            // Original: Extract all UTT fields from GFF root
            // Engine default: "" (swkotor2.exe:0x00584f40 line 192, swkotor.exe:0x0058da80 line 184)
            utt.Tag = root.Acquire<string>("Tag", "");
            // Note: TemplateResRef is NOT read by the engine (swkotor2.exe:0x00584f40, swkotor.exe:0x0058da80)
            utt.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // Engine default: 0 (swkotor2.exe:0x00584f40 line 184, swkotor.exe:0x0058da80 line 176)
            utt.AutoRemoveKey = root.Acquire<int>("AutoRemoveKey", 0) != 0;
            // Engine default: 0 (swkotor2.exe:0x00584f40 line 211, swkotor.exe:0x0058da80 line 204)
            utt.FactionId = root.Acquire<int>("Faction", 0);
            // Engine default: 0 (swkotor2.exe:0x00584f40 line 250, swkotor.exe:0x0058da80 line 242)
            utt.Cursor = root.Acquire<int>("Cursor", 0);
            // Engine default: 0.1 (but only if > _DAT_007b56fc in K2, > _DAT_0073d700 in K1) (swkotor2.exe:0x00584f40 line 273, swkotor.exe:0x0058da80 line 266)
            utt.HighlightHeight = root.Acquire<float>("HighlightHeight", 0.0f);
            // Engine default: "" (swkotor2.exe:0x00584f40 line 217, swkotor.exe:0x0058da80 line 210)
            utt.KeyName = root.Acquire<string>("KeyName", "");
            // Engine default: 0 (swkotor2.exe:0x00584f40 line 262, swkotor.exe:0x0058da80 line 255)
            utt.TypeId = root.Acquire<int>("Type", 0);
            // Engine default: 0 (swkotor2.exe:0x00584f40 line 226, swkotor.exe:0x0058da80 line 220)
            utt.TrapDetectable = root.Acquire<int>("TrapDetectable", 0) != 0;
            // Note: TrapDetectDC is calculated from TrapType and OwnerDemolitionsSkill, not read directly (swkotor2.exe:0x00584f40 line 107, swkotor.exe:0x0058da80 line 107)
            utt.TrapDetectDc = root.Acquire<int>("TrapDetectDC", 0);
            // Engine default: 0 (swkotor2.exe:0x00584f40 line 224, swkotor.exe:0x0058da80 line 218)
            utt.TrapDisarmable = root.Acquire<int>("TrapDisarmable", 0) != 0;
            // Note: DisarmDC is calculated from TrapType and OwnerDemolitionsSkill, not read directly (swkotor2.exe:0x00584f40 line 92, swkotor.exe:0x0058da80 line 92)
            utt.TrapDisarmDc = root.Acquire<int>("DisarmDC", 0);
            // Note: TrapFlag is NOT read by the engine (swkotor2.exe:0x00584f40, swkotor.exe:0x0058da80)
            utt.IsTrap = root.Acquire<int>("TrapFlag", 0) != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00584f40 line 157, swkotor.exe:0x0058da80 line 147)
            utt.TrapOnce = root.Acquire<int>("TrapOneShot", 0) != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00584f40 line 139, swkotor.exe:0x0058da80 line 128)
            utt.TrapType = root.Acquire<int>("TrapType", 0);
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00584f40 line 130, swkotor.exe:0x0058da80 line 117)
            utt.OnDisarmScript = root.Acquire<ResRef>("OnDisarm", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00584f40 line 125, swkotor.exe:0x0058da80 line 111)
            utt.OnTrapTriggeredScript = root.Acquire<ResRef>("OnTrapTriggered", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00584f40 line 135, swkotor.exe:0x0058da80 line 123)
            utt.OnClickScript = root.Acquire<ResRef>("OnClick", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00584f40 line 105, swkotor.exe:0x0058da80 line 87)
            utt.OnHeartbeatScript = root.Acquire<ResRef>("ScriptHeartbeat", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00584f40 line 110, swkotor.exe:0x0058da80 line 93)
            utt.OnEnterScript = root.Acquire<ResRef>("ScriptOnEnter", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00584f40 line 115, swkotor.exe:0x0058da80 line 99)
            utt.OnExitScript = root.Acquire<ResRef>("ScriptOnExit", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00584f40 line 120, swkotor.exe:0x0058da80 line 105)
            utt.OnUserDefinedScript = root.Acquire<ResRef>("ScriptUserDefine", ResRef.FromBlank());
            // Note: Comment is NOT read by the engine (swkotor2.exe:0x00584f40, swkotor.exe:0x0058da80)
            utt.Comment = root.Acquire<string>("Comment", "");
            // Engine default: existing value (invalid LocalizedString for new objects) (swkotor2.exe:0x00584f40 line 204, swkotor.exe:0x0058da80 line 196)
            utt.Name = root.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
            // Engine default: 0 (swkotor2.exe:0x00584f40 line 397, swkotor.exe:0x0058da80 line 339)
            utt.LoadscreenId = root.Acquire<int>("LoadScreenID", 0);
            // Engine default: 0xffff (special case, reads Portrait ResRef if 0xffff) (swkotor2.exe:0x00584f40 line 89, swkotor.exe:0x0058da80 line 71)
            utt.PortraitId = root.Acquire<int>("PortraitId", 0);
            // Note: PaletteID is NOT read by the engine (swkotor2.exe:0x00584f40, swkotor.exe:0x0058da80)
            utt.PaletteId = root.Acquire<int>("PaletteID", 0);

            return utt;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:267-324
        // Original: def dismantle_utt(utt: UTT, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtt(UTT utt, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTT);
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:293-323
            // Original: Set all UTT fields in GFF root
            root.SetString("Tag", utt.Tag);
            root.SetResRef("TemplateResRef", utt.ResRef);
            root.SetUInt8("AutoRemoveKey", (byte)(utt.AutoRemoveKey ? 1 : 0));
            root.SetUInt32("Faction", (uint)utt.FactionId);
            root.SetUInt8("Cursor", (byte)utt.Cursor);
            root.SetSingle("HighlightHeight", utt.HighlightHeight);
            root.SetString("KeyName", utt.KeyName);
            root.SetInt32("Type", utt.TypeId);
            root.SetUInt8("TrapDetectable", (byte)(utt.TrapDetectable ? 1 : 0));
            root.SetUInt8("TrapDetectDC", (byte)utt.TrapDetectDc);
            root.SetUInt8("TrapDisarmable", (byte)(utt.TrapDisarmable ? 1 : 0));
            root.SetUInt8("DisarmDC", (byte)utt.TrapDisarmDc);
            root.SetUInt8("TrapFlag", (byte)(utt.IsTrap ? 1 : 0));
            root.SetUInt8("TrapOneShot", (byte)(utt.TrapOnce ? 1 : 0));
            root.SetUInt8("TrapType", (byte)utt.TrapType);
            root.SetResRef("OnDisarm", utt.OnDisarmScript);
            root.SetResRef("OnTrapTriggered", utt.OnTrapTriggeredScript);
            root.SetResRef("OnClick", utt.OnClickScript);
            root.SetResRef("ScriptHeartbeat", utt.OnHeartbeatScript);
            root.SetResRef("ScriptOnEnter", utt.OnEnterScript);
            root.SetResRef("ScriptOnExit", utt.OnExitScript);
            root.SetResRef("ScriptUserDefine", utt.OnUserDefinedScript);
            root.SetString("Comment", utt.Comment);

            root.SetUInt8("PaletteID", (byte)utt.PaletteId);

            if (useDeprecated)
            {
                root.SetLocString("LocalizedName", utt.Name);
                root.SetUInt16("LoadScreenID", (ushort)utt.LoadscreenId);
                root.SetUInt16("PortraitId", (ushort)utt.PortraitId);
            }

            return gff;
        }
    }
}

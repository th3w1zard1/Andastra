using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using static BioWare.NET.Common.GameExtensions;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py
    // Original: construct_ute and dismantle_ute functions
    // Engine references: swkotor2.exe:0x0056d770 (FUN_0056d770), swkotor2.exe:0x0056c010 (FUN_0056c010), swkotor.exe:0x00592430 (FUN_00592430), swkotor.exe:0x00590820 (FUN_00590820)
    public static class UTEHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:235-274
        // Original: def construct_ute(gff: GFF) -> UTE:
        // Engine references: swkotor2.exe:0x0056d770, swkotor.exe:0x00592430
        public static UTE ConstructUte(GFF gff)
        {
            var ute = new UTE();
            var root = gff.Root;

            // Extract basic fields
            // Engine default: existing value (empty string for new objects) (swkotor2.exe:0x0056d770 line 112, swkotor.exe:0x00592430 line 111)
            ute.Tag = root.Acquire<string>("Tag", "");
            // Engine default: existing value (blank ResRef for new objects) - TemplateResRef is not read by engine, only written
            ute.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 60, swkotor.exe:0x00592430 line 59)
            ute.Active = root.Acquire<int>("Active", 0) != 0;
            // Engine default: -1 (special case!) (swkotor2.exe:0x0056d770 line 78, swkotor.exe:0x00592430 line 77)
            ute.DifficultyId = root.Acquire<int>("DifficultyIndex", -1);
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 82, swkotor.exe:0x00592430 line 81)
            ute.DifficultyIndex = root.Acquire<int>("Difficulty", 0);
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 76, swkotor.exe:0x00592430 line 75)
            ute.Faction = root.Acquire<int>("Faction", 0);
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 70, swkotor.exe:0x00592430 line 69)
            ute.MaxCreatures = root.Acquire<int>("MaxCreatures", 0);
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 74, swkotor.exe:0x00592430 line 73)
            ute.PlayerOnly = root.Acquire<int>("PlayerOnly", 0) != 0 ? 1 : 0;
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 72, swkotor.exe:0x00592430 line 71)
            ute.RecCreatures = root.Acquire<int>("RecCreatures", 0);
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 62, swkotor.exe:0x00592430 line 61)
            ute.Reset = root.Acquire<int>("Reset", 0) != 0 ? 1 : 0;
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 64, swkotor.exe:0x00592430 line 63)
            ute.ResetTime = root.Acquire<int>("ResetTime", 0);
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 66, swkotor.exe:0x00592430 line 65)
            ute.Respawn = root.Acquire<int>("Respawns", 0);
            // Engine default: existing value (0 for new objects) (swkotor2.exe:0x0056d770 line 68, swkotor.exe:0x00592430 line 67)
            ute.SingleSpawn = root.Acquire<int>("SpawnOption", 0) != 0 ? 1 : 0;
            // Engine default: existing value (blank ResRef for new objects) (swkotor2.exe:0x0056c010 line 23, swkotor.exe:0x00590820 line 23)
            ute.OnEnteredScript = root.Acquire<ResRef>("OnEntered", ResRef.FromBlank());
            // Engine default: existing value (blank ResRef for new objects) (swkotor2.exe:0x0056c010 line 31, swkotor.exe:0x00590820 line 31)
            ute.OnExitScript = root.Acquire<ResRef>("OnExit", ResRef.FromBlank());
            // Engine default: existing value (blank ResRef for new objects) (swkotor2.exe:0x0056c010 line 47, swkotor.exe:0x00590820 line 47)
            ute.OnExhaustedScript = root.Acquire<ResRef>("OnExhausted", ResRef.FromBlank());
            // Engine default: existing value (blank ResRef for new objects) (swkotor2.exe:0x0056c010 line 39, swkotor.exe:0x00590820 line 39)
            ute.OnHeartbeatScript = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            // Engine default: existing value (blank ResRef for new objects) (swkotor2.exe:0x0056c010 line 55, swkotor.exe:0x00590820 line 55)
            ute.OnUserDefinedScript = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            // Engine default: Comment field is NOT read by the engine
            ute.Comment = root.Acquire<string>("Comment", "");
            // Engine default: existing value (invalid LocalizedString for new objects) (swkotor2.exe:0x0056d770 line 105, swkotor.exe:0x00592430 line 104)
            ute.Name = root.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
            // Engine default: PaletteID field is NOT read by the engine (toolset only)
            ute.PaletteId = root.Acquire<int>("PaletteID", 0);

            // Extract creature list
            // Engine behavior: if "CreatureList" list is missing, engine skips processing. Default is empty list.
            // (swkotor2.exe:0x0056d770 line 183, swkotor.exe:0x00592430 line 182)
            var creatureList = root.Acquire<GFFList>("CreatureList", new GFFList());
            ute.Creatures.Clear();
            foreach (var creatureStruct in creatureList)
            {
                var creature = new UTECreature();
                // Engine default: Appearance field is NOT read by the engine (toolset only)
                creature.Appearance = creatureStruct.Acquire<int>("Appearance", 0);
                // Engine default: 0.0 (swkotor2.exe:0x0056d770 line 217, swkotor.exe:0x00592430 line 216)
                creature.CR = (int)creatureStruct.Acquire<float>("CR", 0.0f);
                // Engine default: 0 (swkotor2.exe:0x0056d770 line 220, swkotor.exe:0x00592430 line 221)
                creature.SingleSpawn = creatureStruct.Acquire<int>("SingleSpawn", 0) != 0 ? 1 : 0;
                // Engine default: existing value (blank ResRef for new objects) (swkotor2.exe:0x0056d770 line 215, swkotor.exe:0x00592430 line 214)
                creature.ResRef = creatureStruct.Acquire<ResRef>("ResRef", ResRef.FromBlank());
                // Engine default: 0 (K2 only) (swkotor2.exe:0x0056d770 line 223)
                creature.GuaranteedCount = creatureStruct.Acquire<int>("GuaranteedCount", 0);
                ute.Creatures.Add(creature);
            }

            return ute;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:277-322
        // Original: def dismantle_ute(ute: UTE, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUte(UTE ute, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTE);
            var root = gff.Root;

            // Set basic fields
            root.SetString("Tag", ute.Tag);
            root.SetResRef("TemplateResRef", ute.ResRef);
            root.SetUInt8("Active", (byte)(ute.Active ? 1 : 0));
            root.SetInt32("DifficultyIndex", ute.DifficultyId);
            root.SetUInt32("Faction", (uint)ute.Faction);
            root.SetInt32("MaxCreatures", ute.MaxCreatures);
            root.SetUInt8("PlayerOnly", (byte)(ute.PlayerOnly != 0 ? 1 : 0));
            root.SetInt32("RecCreatures", ute.RecCreatures);
            root.SetUInt8("Reset", (byte)(ute.Reset != 0 ? 1 : 0));
            root.SetInt32("ResetTime", ute.ResetTime);
            root.SetInt32("Respawns", ute.Respawn);
            root.SetInt32("SpawnOption", ute.SingleSpawn);
            root.SetResRef("OnEntered", ute.OnEnteredScript);
            root.SetResRef("OnExit", ute.OnExitScript);
            root.SetResRef("OnExhausted", ute.OnExhaustedScript);
            root.SetResRef("OnHeartbeat", ute.OnHeartbeatScript);
            root.SetResRef("OnUserDefined", ute.OnUserDefinedScript);
            root.SetString("Comment", ute.Comment);

            // Set creature list
            if (ute.Creatures.Count > 0)
            {
                var creatureList = new GFFList();
                foreach (var creature in ute.Creatures)
                {
                    var creatureStruct = creatureList.Add();
                    // Matching PyKotor implementation: creature_struct.set_int32("Appearance", creature.appearance_id)
                    creatureStruct.SetInt32("Appearance", creature.Appearance);
                    // Matching PyKotor implementation: creature_struct.set_single("CR", creature.challenge_rating)
                    creatureStruct.SetSingle("CR", creature.CR);
                    creatureStruct.SetUInt8("SingleSpawn", (byte)(creature.SingleSpawn != 0 ? 1 : 0));
                    creatureStruct.SetResRef("ResRef", creature.ResRef);
                    if (game.IsK2())
                    {
                        creatureStruct.SetInt32("GuaranteedCount", creature.GuaranteedCount);
                    }
                }
                root.SetList("CreatureList", creatureList);
            }

            if (useDeprecated)
            {
                // Matching PyKotor implementation: root.set_locstring("LocalizedName", ute.name)
                root.SetLocString("LocalizedName", ute.Name);
                // Matching PyKotor implementation: root.set_int32("Difficulty", ute.unused_difficulty)
                root.SetInt32("Difficulty", ute.DifficultyIndex);
                // Matching PyKotor implementation: root.set_uint8("PaletteID", ute.palette_id)
                root.SetUInt8("PaletteID", (byte)ute.PaletteId);
            }

            return gff;
        }
    }
}

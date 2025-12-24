using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:91-112
    // Original: def construct_jrl(gff: GFF) -> JRL:
    // Engine references: swkotor2.exe:0x00600dd0, swkotor.exe:0x005c5a40
    public static class JRLHelpers
    {
        public static JRL ConstructJrl(GFF gff)
        {
            var jrl = new JRL();

            // Engine reference: swkotor2.exe:0x00600dd0 line 130, swkotor.exe:0x005c5a40 line 130
            GFFList categories = gff.Root.Acquire("Categories", new GFFList());
            foreach (GFFStruct categoryStruct in categories)
            {
                var quest = new JRLQuest();
                jrl.Quests.Add(quest);

                // Engine default: "" (swkotor2.exe:0x00600dd0 line 138, swkotor.exe:0x005c5a40 line 138)
                // Note: Comment field is written but not read in engine loading function - optional field
                quest.Comment = categoryStruct.Acquire("Comment", string.Empty);

                // Engine default: LocalizedString (swkotor2.exe:0x00600dd0 line 151, swkotor.exe:0x005c5a40 line 151)
                quest.Name = categoryStruct.Acquire("Name", LocalizedString.FromInvalid());

                // Engine default: 0 (swkotor2.exe:0x00600dd0 line 186, swkotor.exe:0x005c5a40 line 186)
                quest.PlanetId = categoryStruct.Acquire("PlanetID", 0);

                // Engine default: 0 (swkotor2.exe:0x00600dd0 line 180, swkotor.exe:0x005c5a40 line 180)
                quest.PlotIndex = categoryStruct.Acquire("PlotIndex", 0);

                // Engine default: 0 (swkotor2.exe:0x00600dd0 line 165, swkotor.exe:0x005c5a40 line 165)
                int priorityValue = categoryStruct.Acquire("Priority", 0);
                quest.Priority = (JRLQuestPriority)priorityValue;

                // Engine default: "" (swkotor2.exe:0x00600dd0 line 138, swkotor.exe:0x005c5a40 line 138)
                quest.Tag = categoryStruct.Acquire("Tag", string.Empty);

                // Engine reference: swkotor2.exe:0x00600dd0 line 192, swkotor.exe:0x005c5a40 line 192
                GFFList entryList = categoryStruct.Acquire("EntryList", new GFFList());
                foreach (GFFStruct entryStruct in entryList)
                {
                    var entry = new JRLQuestEntry();
                    quest.Entries.Add(entry);

                    // Engine default: 0 (swkotor2.exe:0x00600dd0 line 237, swkotor.exe:0x005c5a40 line 237)
                    entry.End = entryStruct.Acquire("End", (ushort)0) != 0;

                    // Engine default: 0 (swkotor2.exe:0x00600dd0 line 205, swkotor.exe:0x005c5a40 line 205)
                    entry.EntryId = (int)entryStruct.Acquire("ID", (uint)0);

                    // Engine default: LocalizedString (swkotor2.exe:0x00600dd0 line 209, swkotor.exe:0x005c5a40 line 209)
                    entry.Text = entryStruct.Acquire("Text", LocalizedString.FromInvalid());

                    // Engine default: 0.0 (swkotor2.exe:0x00600dd0 line 222, swkotor.exe:0x005c5a40 line 222)
                    entry.XpPercentage = entryStruct.Acquire("XP_Percentage", 0.0f);
                }
            }

            return jrl;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:115-141
        // Original: def dismantle_jrl(jrl: JRL, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleJrl(JRL jrl)
        {
            var gff = new GFF(GFFContent.JRL);

            var categoryList = new GFFList();
            gff.Root.SetList("Categories", categoryList);
            for (int i = 0; i < jrl.Quests.Count; i++)
            {
                JRLQuest quest = jrl.Quests[i];
                GFFStruct categoryStruct = categoryList.Add(i);
                categoryStruct.SetString("Comment", quest.Comment);
                categoryStruct.SetLocString("Name", quest.Name);
                categoryStruct.SetInt32("PlanetID", quest.PlanetId);
                categoryStruct.SetInt32("PlotIndex", quest.PlotIndex);
                categoryStruct.SetUInt32("Priority", (uint)quest.Priority);
                categoryStruct.SetString("Tag", quest.Tag);

                var entryList = new GFFList();
                categoryStruct.SetList("EntryList", entryList);
                for (int j = 0; j < quest.Entries.Count; j++)
                {
                    JRLQuestEntry entry = quest.Entries[j];
                    GFFStruct entryStruct = entryList.Add(j);
                    entryStruct.SetUInt16("End", (ushort)(entry.End ? 1 : 0));
                    entryStruct.SetUInt32("ID", (uint)entry.EntryId);
                    entryStruct.SetLocString("Text", entry.Text);
                    entryStruct.SetSingle("XP_Percentage", entry.XpPercentage);
                }
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:144-150
        // Original: def read_jrl(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> JRL:
        public static JRL ReadJrl(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructJrl(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:163-168
        // Original: def bytes_jrl(jrl: JRL, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesJrl(JRL jrl, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.JRL;
            }
            GFF gff = DismantleJrl(jrl);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}


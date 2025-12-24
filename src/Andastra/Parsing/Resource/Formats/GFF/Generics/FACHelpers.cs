using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Resource.Generics
{
    // Engine references: swkotor2.exe:0x005acf30, swkotor.exe:0x0052b5c0 (FactionList loading)
    // Engine references: swkotor2.exe:0x005ad1a0, swkotor.exe:0x0052b830 (RepList loading)
    // Engine references: swkotor2.exe:0x004fcab0, swkotor.exe:0x004c3960 (FAC file loading)
    public static class FACHelpers
    {
        public static FAC ConstructFac(GFF gff)
        {
            var fac = new FAC();
            var root = gff.Root;

            // Extract FactionList
            // Engine reference: swkotor2.exe:0x004fcab0 line 45, swkotor.exe:0x004c3960 line 45
            var factionList = root.Acquire<GFFList>("FactionList", new GFFList());
            if (factionList != null && factionList.Count > 0)
            {
                foreach (var factionStruct in factionList)
                {
                    var faction = new FACFaction();

                    // Engine default: "" (swkotor2.exe:0x005acf30 line 38, swkotor.exe:0x0052b5c0 line 38)
                    faction.Name = factionStruct.Acquire<string>("FactionName", string.Empty);

                    // Engine default: 0 (swkotor2.exe:0x005acf30 line 47, swkotor.exe:0x0052b5c0 line 47)
                    // Standard factions use 0xFFFFFFFF (-1) for no parent
                    int parentIdVal = factionStruct.Acquire<int>("FactionParentID", -1);
                    // Handle both signed and unsigned representations of 0xFFFFFFFF
                    faction.ParentId = (parentIdVal == -1 || parentIdVal == 0xFFFFFFFF) ? unchecked((int)0xFFFFFFFF) : parentIdVal;

                    // Engine default: 0, but if field missing defaults to 1 (swkotor2.exe:0x005acf30 lines 48-52, swkotor.exe:0x0052b5c0 lines 48-52)
                    ushort globalValue = factionStruct.Acquire<ushort>("FactionGlobal", 0);
                    // Engine behavior: If field is missing (local_4c == 0), default to 1
                    if (!factionStruct.Exists("FactionGlobal"))
                    {
                        globalValue = 1;
                    }
                    faction.IsGlobal = globalValue != 0;

                    fac.Factions.Add(faction);
                }
            }

            // Extract RepList
            // Engine reference: swkotor2.exe:0x004fcab0 line 47, swkotor.exe:0x004c3960 line 47
            var repList = root.Acquire<GFFList>("RepList", new GFFList());
            if (repList != null && repList.Count > 0)
            {
                foreach (var repStruct in repList)
                {
                    var rep = new FACReputation();

                    // Engine reference: swkotor2.exe:0x005ad1a0 line 23, swkotor.exe:0x0052b830 line 23
                    rep.FactionId1 = repStruct.Acquire<int>("FactionID1", 0);

                    // Engine reference: swkotor2.exe:0x005ad1a0 line 24, swkotor.exe:0x0052b830 line 24
                    rep.FactionId2 = repStruct.Acquire<int>("FactionID2", 0);

                    // Engine default: 100 (swkotor2.exe:0x005ad1a0 line 21, swkotor.exe:0x0052b830 line 20)
                    // Note: Engine only writes if != 100, so default is 100
                    rep.Reputation = repStruct.Acquire<int>("FactionRep", 100);

                    fac.Reputations.Add(rep);
                }
            }

            return fac;
        }

        public static GFF DismantleFac(FAC fac)
        {
            var gff = new GFF(GFFContent.FAC);
            var root = gff.Root;

            // Set FactionList
            var factionList = new GFFList();
            root.SetList("FactionList", factionList);
            for (int i = 0; i < fac.Factions.Count; i++)
            {
                FACFaction faction = fac.Factions[i];
                GFFStruct factionStruct = factionList.Add(i);
                factionStruct.SetString("FactionName", faction.Name);
                // FactionParentID as uint32, handle 0xFFFFFFFF correctly
                if (faction.ParentId == unchecked((int)0xFFFFFFFF))
                {
                    factionStruct.SetUInt32("FactionParentID", 0xFFFFFFFF);
                }
                else
                {
                    factionStruct.SetUInt32("FactionParentID", (uint)faction.ParentId);
                }
                factionStruct.SetUInt16("FactionGlobal", (ushort)(faction.IsGlobal ? 1 : 0));
            }

            // Set RepList
            var repList = new GFFList();
            root.SetList("RepList", repList);
            for (int i = 0; i < fac.Reputations.Count; i++)
            {
                FACReputation rep = fac.Reputations[i];
                GFFStruct repStruct = repList.Add(i);
                repStruct.SetInt32("FactionID1", rep.FactionId1);
                repStruct.SetInt32("FactionID2", rep.FactionId2);
                // Engine only writes if != 100 (swkotor2.exe:0x005ad1a0 line 21, swkotor.exe:0x0052b830 line 20)
                if (rep.Reputation != 100)
                {
                    repStruct.SetInt32("FactionRep", rep.Reputation);
                }
            }

            return gff;
        }

        public static FAC ReadFac(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructFac(gff);
        }

        public static byte[] BytesFac(FAC fac, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.FAC;
            }
            GFF gff = DismantleFac(fac);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}


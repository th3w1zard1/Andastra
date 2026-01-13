using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    // Engine references: swkotor2.exe:0x004e3650, swkotor.exe:0x00508400 (PTH loading)
    public static class PTHHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:160-182
        // Original: def construct_pth(gff: GFF) -> PTH:
        // Engine reference: swkotor2.exe:0x004e3650, swkotor.exe:0x00508400
        public static PTH ConstructPth(GFF gff)
        {
            var pth = new PTH();
            var root = gff.Root;

            // Engine default: Empty list if Path_Conections missing
            // Engine checks if list exists (swkotor2.exe:0x004e3650 line 78, swkotor.exe:0x00508400 line 78)
            // If missing, engine skips connection processing
            var connectionsList = root.Acquire<GFFList>("Path_Conections", new GFFList());

            // Engine default: Empty list if Path_Points missing
            // Engine checks if list exists (swkotor2.exe:0x004e3650 line 44, swkotor.exe:0x00508400 line 44)
            // If missing, engine skips point processing
            var pointsList = root.Acquire<GFFList>("Path_Points", new GFFList());

            foreach (var pointStruct in pointsList)
            {
                // Engine default: 0 (swkotor2.exe:0x004e3650 line 66, swkotor.exe:0x00508400 line 66)
                int connections = pointStruct.Acquire<int>("Conections", 0);

                // Engine default: 0 (swkotor2.exe:0x004e3650 line 69, swkotor.exe:0x00508400 line 69)
                int firstConnection = pointStruct.Acquire<int>("First_Conection", 0);

                // Engine default: 0.0 (swkotor2.exe:0x004e3650 line 60, swkotor.exe:0x00508400 line 60)
                float x = pointStruct.Acquire<float>("X", 0.0f);

                // Engine default: 0.0 (swkotor2.exe:0x004e3650 line 63, swkotor.exe:0x00508400 line 63)
                float y = pointStruct.Acquire<float>("Y", 0.0f);

                int sourceIndex = pth.Add(x, y);

                for (int i = firstConnection; i < firstConnection + connections; i++)
                {
                    var connectionStruct = connectionsList.At(i);
                    if (connectionStruct == null)
                    {
                        continue;
                    }
                    // Engine default: 0 (swkotor2.exe:0x004e3650 line 89, swkotor.exe:0x00508400 line 89)
                    int target = connectionStruct.Acquire<int>("Destination", 0);
                    pth.Connect(sourceIndex, target);
                }
            }

            return pth;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:185-209
        // Original: def dismantle_pth(pth: PTH, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantlePth(PTH pth, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.PTH);
            var root = gff.Root;

            var connectionsList = new GFFList();
            root.SetList("Path_Conections", connectionsList);
            var pointsList = new GFFList();
            root.SetList("Path_Points", pointsList);

            int pointIndex = 0;
            foreach (var point in pth)
            {
                var outgoingConnections = pth.Outgoing(pointIndex);

                var pointStruct = pointsList.Add(2);
                pointStruct.SetUInt32("Conections", (uint)outgoingConnections.Count);
                pointStruct.SetUInt32("First_Conection", (uint)connectionsList.Count);
                pointStruct.SetSingle("X", point.X);
                pointStruct.SetSingle("Y", point.Y);

                foreach (var outgoing in outgoingConnections)
                {
                    var connectionStruct = connectionsList.Add(3);
                    connectionStruct.SetUInt32("Destination", (uint)outgoing.TargetIndex);
                }

                pointIndex++;
            }

            return gff;
        }
    }
}

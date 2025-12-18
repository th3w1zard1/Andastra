using System;
using Andastra.Parsing.Formats.NCS;
var reader1 = new NCSBinaryReader(@"src/Andastra/Tests/bin/Debug/net9/test-work/roundtrip-work/k1/K1/Data/scripts.bif/k_act_com41.ncs");
var ncs1 = reader1.Load();
var reader2 = new NCSBinaryReader(@"src/Andastra/Tests/bin/Debug/net9/test-work/roundtrip-work/k1/K1/Data/scripts.bif/k_act_com41.rt.ncs");
var ncs2 = reader2.Load();
Console.WriteLine($"Original: {ncs1.Instructions.Count} instructions");
Console.WriteLine($"Roundtrip: {ncs2.Instructions.Count} instructions");

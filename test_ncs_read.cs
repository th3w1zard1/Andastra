using System;
using Andastra.Parsing.Formats.NCS;
var reader = new NCSBinaryReader("src/Andastra/Tests/bin/Debug/net9/test-work/roundtrip-work/k1/K1/Data/scripts.bif/01_test.ncs");
var ncs = reader.Load();
Console.WriteLine($"Instructions: {ncs.Instructions.Count}");

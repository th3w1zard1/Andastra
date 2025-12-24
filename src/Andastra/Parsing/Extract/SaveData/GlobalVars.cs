using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Extract.SaveData
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/savedata.py:1021-1329
    // Original: class GlobalVars
    public class GlobalVars
    {
        public List<Tuple<string, bool>> GlobalBools { get; } = new List<Tuple<string, bool>>();
        public List<Tuple<string, Vector4>> GlobalLocations { get; } = new List<Tuple<string, Vector4>>();
        public List<Tuple<string, int>> GlobalNumbers { get; } = new List<Tuple<string, int>>();
        public List<Tuple<string, string>> GlobalStrings { get; } = new List<Tuple<string, string>>();

        private readonly string _globalsPath;

        public GlobalVars(string folderPath)
        {
            _globalsPath = Path.Combine(folderPath, "globalvars.res");
        }

        public void Load()
        {
            byte[] data = File.ReadAllBytes(_globalsPath);
            GFF gff = GFF.FromBytes(data);
            GFFStruct root = gff.Root;

            // Booleans
            GlobalBools.Clear();
            GFFList catBool = root.GetList("CatBoolean");
            byte[] valBool = root.GetBinary("ValBoolean");
            if (catBool != null && valBool != null)
            {
                int count = catBool.Count;
                for (int i = 0; i < count; i++)
                {
                    int byteIndex = i / 8;
                    int bitIndex = i % 8;
                    if (byteIndex < valBool.Length)
                    {
                        bool value = ((valBool[byteIndex] >> bitIndex) & 1) != 0;
                        string name = catBool[i].GetString("Name");
                        GlobalBools.Add(Tuple.Create(name, value));
                    }
                }
            }

            // Locations (12 floats per entry)
            GlobalLocations.Clear();
            GFFList catLoc = root.GetList("CatLocation");
            byte[] valLoc = root.GetBinary("ValLocation");
            if (catLoc != null && valLoc != null)
            {
                using (var br = new System.IO.BinaryReader(new MemoryStream(valLoc)))
                {
                    foreach (var cat in catLoc)
                    {
                        float x = br.ReadSingle();
                        float y = br.ReadSingle();
                        float z = br.ReadSingle();
                        float oriX = br.ReadSingle();
                        br.ReadSingle(); // ori_y (unused)
                        br.ReadSingle(); // ori_z (unused)
                        br.ReadBytes(24); // padding floats 6-11
                        string name = cat.GetString("Name");
                        GlobalLocations.Add(Tuple.Create(name, new Vector4(x, y, z, oriX)));
                    }
                }
            }

            // Numbers (one byte each)
            GlobalNumbers.Clear();
            GFFList catNum = root.GetList("CatNumber");
            byte[] valNum = root.GetBinary("ValNumber");
            if (catNum != null && valNum != null)
            {
                using (var br = new System.IO.BinaryReader(new MemoryStream(valNum)))
                {
                    foreach (var cat in catNum)
                    {
                        string name = cat.GetString("Name");
                        int value = br.ReadByte();
                        GlobalNumbers.Add(Tuple.Create(name, value));
                    }
                }
            }

            // Strings (parallel lists)
            GlobalStrings.Clear();
            GFFList catStr = root.GetList("CatString");
            GFFList valStr = root.GetList("ValString");
            if (catStr != null && valStr != null)
            {
                int count = Math.Min(catStr.Count, valStr.Count);
                for (int i = 0; i < count; i++)
                {
                    string name = catStr[i].GetString("Name");
                    string value = valStr[i].GetString("String");
                    GlobalStrings.Add(Tuple.Create(name, value));
                }
            }
        }

        public void Save()
        {
            GFF gff = new GFF(GFFContent.GVT);
            GFFStruct root = gff.Root;

            // Booleans: pack bits LSB first
            int boolCount = GlobalBools.Count;
            int boolBytes = (boolCount + 7) / 8;
            byte[] valBool = new byte[boolBytes];
            GFFList catBool = new GFFList();
            for (int i = 0; i < boolCount; i++)
            {
                catBool.Add().SetString("Name", GlobalBools[i].Item1);
                if (GlobalBools[i].Item2)
                {
                    int byteIndex = i / 8;
                    int bitIndex = i % 8;
                    valBool[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
            if (boolCount > 0)
            {
                root.SetList("CatBoolean", catBool);
                root.SetBinary("ValBoolean", valBool);
            }

            // Locations: 12 floats per entry, with padding zeros
            if (GlobalLocations.Count > 0)
            {
                GFFList catLoc = new GFFList();
                using (var ms = new MemoryStream())
                using (var bw = new System.IO.BinaryWriter(ms))
                {
                    foreach (var entry in GlobalLocations)
                    {
                        catLoc.Add().SetString("Name", entry.Item1);
                        Vector4 v = entry.Item2;
                        bw.Write(v.X);
                        bw.Write(v.Y);
                        bw.Write(v.Z);
                        bw.Write(v.W); // ori_x
                        bw.Write(0.0f); // ori_y
                        bw.Write(0.0f); // ori_z
                        for (int i = 0; i < 6; i++) bw.Write(0.0f); // padding
                    }
                    root.SetList("CatLocation", catLoc);
                    root.SetBinary("ValLocation", ms.ToArray());
                }
            }

            // Numbers: one byte each
            if (GlobalNumbers.Count > 0)
            {
                GFFList catNum = new GFFList();
                using (var ms = new MemoryStream())
                {
                    foreach (var entry in GlobalNumbers)
                    {
                        catNum.Add().SetString("Name", entry.Item1);
                        ms.WriteByte((byte)entry.Item2);
                    }
                    root.SetList("CatNumber", catNum);
                    root.SetBinary("ValNumber", ms.ToArray());
                }
            }

            // Strings: parallel lists
            if (GlobalStrings.Count > 0)
            {
                GFFList catStr = new GFFList();
                GFFList valStr = new GFFList();
                foreach (var entry in GlobalStrings)
                {
                    catStr.Add().SetString("Name", entry.Item1);
                    valStr.Add().SetString("String", entry.Item2);
                }
                root.SetList("CatString", catStr);
                root.SetList("ValString", valStr);
            }

            byte[] bytes = new GFFBinaryWriter(gff).Write();
            File.WriteAllBytes(_globalsPath, bytes);
        }

        // Convenience getters/setters
        public bool? GetBool(string name)
        {
            foreach (var pair in GlobalBools)
            {
                if (pair.Item1.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Item2;
                }
            }
            return null;
        }

        public void SetBool(string name, bool value)
        {
            for (int i = 0; i < GlobalBools.Count; i++)
            {
                if (GlobalBools[i].Item1.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    GlobalBools[i] = Tuple.Create(GlobalBools[i].Item1, value);
                    return;
                }
            }
            GlobalBools.Add(Tuple.Create(name, value));
        }

        public int? GetNumber(string name)
        {
            foreach (var pair in GlobalNumbers)
            {
                if (pair.Item1.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Item2;
                }
            }
            return null;
        }

        public void SetNumber(string name, int value)
        {
            for (int i = 0; i < GlobalNumbers.Count; i++)
            {
                if (GlobalNumbers[i].Item1.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    GlobalNumbers[i] = Tuple.Create(GlobalNumbers[i].Item1, value);
                    return;
                }
            }
            GlobalNumbers.Add(Tuple.Create(name, value));
        }

        public string GetString(string name)
        {
            foreach (var pair in GlobalStrings)
            {
                if (pair.Item1.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Item2;
                }
            }
            return null;
        }

        public void SetString(string name, string value)
        {
            for (int i = 0; i < GlobalStrings.Count; i++)
            {
                if (GlobalStrings[i].Item1.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    GlobalStrings[i] = Tuple.Create(GlobalStrings[i].Item1, value);
                    return;
                }
            }
            GlobalStrings.Add(Tuple.Create(name, value));
        }

        public Vector4? GetLocation(string name)
        {
            foreach (var pair in GlobalLocations)
            {
                if (pair.Item1.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Item2;
                }
            }
            return null;
        }

        public void SetLocation(string name, Vector4 value)
        {
            for (int i = 0; i < GlobalLocations.Count; i++)
            {
                if (GlobalLocations[i].Item1.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    GlobalLocations[i] = Tuple.Create(GlobalLocations[i].Item1, value);
                    return;
                }
            }
            GlobalLocations.Add(Tuple.Create(name, value));
        }
    }
}



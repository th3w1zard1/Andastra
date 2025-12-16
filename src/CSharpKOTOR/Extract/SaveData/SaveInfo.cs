using System;
using System.IO;
using BioWareCSharp.Common;
using BioWareCSharp.Common.Formats.GFF;
using BioWareCSharp.Common.Resources;

namespace BioWareCSharp.Common.Extract.SaveData
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/savedata.py:206-475
    // Original: class SaveInfo
    public class SaveInfo
    {
        public string AreaName { get; set; }
        public string LastModule { get; set; }
        public string SavegameName { get; set; }
        public int TimePlayed { get; set; }
        public ulong? Timestamp { get; set; }
        public bool CheatUsed { get; set; }
        public byte GameplayHint { get; set; }
        public byte StoryHint { get; set; }
        public ResRef Portrait0 { get; set; }
        public ResRef Portrait1 { get; set; }
        public ResRef Portrait2 { get; set; }
        public string Live1 { get; set; }
        public string Live2 { get; set; }
        public string Live3 { get; set; }
        public string Live4 { get; set; }
        public string Live5 { get; set; }
        public string Live6 { get; set; }
        public byte LiveContent { get; set; }
        public string PcName { get; set; }

        private readonly string _saveInfoPath;

        public SaveInfo(string folderPath)
        {
            _saveInfoPath = Path.Combine(folderPath, "savenfo.res");
            AreaName = string.Empty;
            LastModule = string.Empty;
            SavegameName = string.Empty;
            TimePlayed = 0;
            Timestamp = null;
            CheatUsed = false;
            GameplayHint = 0;
            StoryHint = 0;
            Portrait0 = ResRef.FromBlank();
            Portrait1 = ResRef.FromBlank();
            Portrait2 = ResRef.FromBlank();
            Live1 = string.Empty;
            Live2 = string.Empty;
            Live3 = string.Empty;
            Live4 = string.Empty;
            Live5 = string.Empty;
            Live6 = string.Empty;
            LiveContent = 0;
            PcName = string.Empty;
        }

        public void Load()
        {
            byte[] data = File.ReadAllBytes(_saveInfoPath);
            GFF gff = GFF.FromBytes(data);
            GFFStruct root = gff.Root;

            AreaName = root.Acquire("AREANAME", string.Empty);
            LastModule = root.Acquire("LASTMODULE", string.Empty);
            SavegameName = root.Acquire("SAVEGAMENAME", string.Empty);
            TimePlayed = root.Acquire("TIMEPLAYED", 0);
            Timestamp = root.Exists("TIMESTAMP") ? (ulong?)root.GetUInt64("TIMESTAMP") : null;

            CheatUsed = root.Acquire("CHEATUSED", (byte)0) != 0;
            GameplayHint = root.Acquire("GAMEPLAYHINT", (byte)0);
            StoryHint = root.Acquire("STORYHINT", (byte)0);

            Portrait0 = root.Acquire("PORTRAIT0", ResRef.FromBlank());
            Portrait1 = root.Acquire("PORTRAIT1", ResRef.FromBlank());
            Portrait2 = root.Acquire("PORTRAIT2", ResRef.FromBlank());

            Live1 = root.Acquire("LIVE1", string.Empty);
            Live2 = root.Acquire("LIVE2", string.Empty);
            Live3 = root.Acquire("LIVE3", string.Empty);
            Live4 = root.Acquire("LIVE4", string.Empty);
            Live5 = root.Acquire("LIVE5", string.Empty);
            Live6 = root.Acquire("LIVE6", string.Empty);
            LiveContent = root.Acquire("LIVECONTENT", (byte)0);

            PcName = root.Acquire("PCNAME", string.Empty);
        }

        public void Save()
        {
            GFF gff = new GFF(GFFContent.NFO);
            GFFStruct root = gff.Root;

            root.SetString("AREANAME", AreaName);
            root.SetString("LASTMODULE", LastModule);
            root.SetString("SAVEGAMENAME", SavegameName);
            root.SetUInt32("TIMEPLAYED", (uint)TimePlayed);
            if (Timestamp.HasValue)
            {
                root.SetUInt64("TIMESTAMP", Timestamp.Value);
            }

            root.SetUInt8("CHEATUSED", (byte)(CheatUsed ? 1 : 0));
            root.SetUInt8("GAMEPLAYHINT", GameplayHint);
            root.SetUInt8("STORYHINT", StoryHint);

            root.SetResRef("PORTRAIT0", Portrait0);
            root.SetResRef("PORTRAIT1", Portrait1);
            root.SetResRef("PORTRAIT2", Portrait2);

            root.SetString("LIVE1", Live1);
            root.SetString("LIVE2", Live2);
            root.SetString("LIVE3", Live3);
            root.SetString("LIVE4", Live4);
            root.SetString("LIVE5", Live5);
            root.SetString("LIVE6", Live6);
            root.SetUInt8("LIVECONTENT", LiveContent);

            if (!string.IsNullOrEmpty(PcName))
            {
                root.SetString("PCNAME", PcName);
            }

            byte[] bytes = new GFFBinaryWriter(gff).Write();
            File.WriteAllBytes(_saveInfoPath, bytes);
        }
    }
}


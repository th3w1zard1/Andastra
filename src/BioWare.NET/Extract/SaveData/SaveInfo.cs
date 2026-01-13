using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;

namespace BioWare.NET.Extract.SaveData
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
            NFOData nfo = NFOAuto.ReadNfo(data);

            AreaName = nfo.AreaName ?? string.Empty;
            LastModule = nfo.LastModule ?? string.Empty;
            SavegameName = nfo.SavegameName ?? string.Empty;
            TimePlayed = nfo.TimePlayedSeconds;
            Timestamp = nfo.TimestampFileTime;

            CheatUsed = nfo.CheatUsed;
            GameplayHint = nfo.GameplayHint;
            StoryHint = nfo.StoryHintLegacy;

            Portrait0 = nfo.Portrait0 ?? ResRef.FromBlank();
            Portrait1 = nfo.Portrait1 ?? ResRef.FromBlank();
            Portrait2 = nfo.Portrait2 ?? ResRef.FromBlank();

            Live1 = nfo.LiveEntries.Count > 0 ? (nfo.LiveEntries[0] ?? string.Empty) : string.Empty;
            Live2 = nfo.LiveEntries.Count > 1 ? (nfo.LiveEntries[1] ?? string.Empty) : string.Empty;
            Live3 = nfo.LiveEntries.Count > 2 ? (nfo.LiveEntries[2] ?? string.Empty) : string.Empty;
            Live4 = nfo.LiveEntries.Count > 3 ? (nfo.LiveEntries[3] ?? string.Empty) : string.Empty;
            Live5 = nfo.LiveEntries.Count > 4 ? (nfo.LiveEntries[4] ?? string.Empty) : string.Empty;
            Live6 = nfo.LiveEntries.Count > 5 ? (nfo.LiveEntries[5] ?? string.Empty) : string.Empty;
            LiveContent = nfo.LiveContentBitmask;

            PcName = nfo.PcName ?? string.Empty;
        }

        public void Save()
        {
            var nfo = new NFOData
            {
                AreaName = AreaName ?? string.Empty,
                LastModule = LastModule ?? string.Empty,
                SavegameName = SavegameName ?? string.Empty,
                TimePlayedSeconds = TimePlayed,
                TimestampFileTime = Timestamp,
                CheatUsed = CheatUsed,
                GameplayHint = GameplayHint,
                StoryHintLegacy = StoryHint,
                Portrait0 = Portrait0 ?? ResRef.FromBlank(),
                Portrait1 = Portrait1 ?? ResRef.FromBlank(),
                Portrait2 = Portrait2 ?? ResRef.FromBlank(),
                LiveContentBitmask = LiveContent,
                PcName = PcName ?? string.Empty,
            };

            // Mirror legacy SaveInfo fields (LIVE1..6).
            nfo.LiveEntries[0] = Live1 ?? string.Empty;
            nfo.LiveEntries[1] = Live2 ?? string.Empty;
            nfo.LiveEntries[2] = Live3 ?? string.Empty;
            nfo.LiveEntries[3] = Live4 ?? string.Empty;
            nfo.LiveEntries[4] = Live5 ?? string.Empty;
            nfo.LiveEntries[5] = Live6 ?? string.Empty;

            byte[] bytes = NFOAuto.BytesNfo(nfo);
            File.WriteAllBytes(_saveInfoPath, bytes);
        }
    }
}

using System.IO;

namespace BioWare.NET.Extract.SaveData
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/savedata.py:2122-2535 (orchestration)
    public class SaveFolderEntry
    {
        public string FolderPath { get; }
        public SaveInfo SaveInfo { get; }
        public GlobalVars GlobalVars { get; }
        public PartyTable PartyTable { get; }
        public SaveNestedCapsule NestedCapsule { get; }

        public SaveFolderEntry(string folderPath)
        {
            FolderPath = folderPath;
            SaveInfo = new SaveInfo(folderPath);
            GlobalVars = new GlobalVars(folderPath);
            PartyTable = new PartyTable(folderPath);
            NestedCapsule = new SaveNestedCapsule(folderPath);
        }

        public void Load()
        {
            if (!Directory.Exists(FolderPath))
            {
                throw new DirectoryNotFoundException(FolderPath);
            }

            SaveInfo.Load();
            GlobalVars.Load();
            PartyTable.Load();
            NestedCapsule.Load();
        }

        public void Save()
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            SaveInfo.Save();
            GlobalVars.Save();
            PartyTable.Save();
            NestedCapsule.Save();
        }
    }
}


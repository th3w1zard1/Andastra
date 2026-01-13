using System;
using System.Collections.Generic;
using System.IO;
using BioWare.NET.Extract.SaveData;
using Andastra.Runtime.Core.Save;

namespace Andastra.Runtime.Content.Save
{
    /// <summary>
    /// Provides save data persistence using KOTOR save file format.
    /// </summary>
    /// <remarks>
    /// KOTOR Save Structure:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_004eb750 @ 0x004eb750 (save metadata serialization), FUN_00708990 @ 0x00708990 (save file operations)
    /// - Located via string references: "SAVES:" @ 0x007be284 (save directory prefix), "savenfo" @ 0x007be1f0 (save metadata file name)
    /// - "SAVEGAME" @ 0x007be28c (save game file name), "SAVENUMBER" @ 0x007be188 (save number field)
    /// - "SAVEGAMENAME" @ 0x007be1a8 (save game name field), "SAVES:\{saveName}\" (save path format)
    /// - Save path format: "SAVES:\{saveName}\" (original uses "SAVES:" prefix)
    /// - Save name format: "%06d - %s" (6-digit number - name) @ 0x007be298 ("SAVES:%06d - %s" format string)
    /// - ".\saves" @ 0x007c6b0c (save directory path), "modulesave" @ 0x007bde20 (module save directory)
    /// - Auto-save: "AutoSave" @ 0x007bd9e8 (auto-save flag), "AutoSaveOnEnter" @ 0x007be248 (auto-save on enter flag)
    /// - "QUICKSAVE" @ 0x007c7368 (quick save constant), "REBOOTAUTOSAVE" @ 0x007cea14 (reboot auto-save constant)
    /// - "PCAUTOSAVE" @ 0x007be320 (PC auto-save), "AUTOSAVE" @ 0x007be34c (auto-save constant)
    /// - "AUTOSAVEPARAMS" @ 0x007be304 (auto-save parameters), "AtSavePoints" @ 0x007bd9cc (at save points flag)
    /// - Save enumeration: "LoadSavegame" @ 0x007bdc90 (load save game function), "GetSavegameList" @ 0x007bdcb0 (get save game list function)
    /// - "SavegameList" @ 0x007bdca0 (save game list field), ":: Module savegame list: %s.\n" @ 0x007cbbb4 (module save list debug)
    /// - GUI: "savename_p" @ 0x007cec48 (save name panel), "saveload_p" @ 0x007cede8 (save/load panel)
    /// - "BTN_SAVELOAD" @ 0x007ced68 (save/load button), "BTN_SAVEGAME" @ 0x007d0dbc (save game button)
    /// - "BTN_LASTSAVE" @ 0x007c8db0 (last save button), "SaveLoad" @ 0x007cb2ac (save/load constant)
    /// - "CB_AUTOSAVE" @ 0x007d2918 (auto-save checkbox), "Old Save Game" @ 0x007cea24 (old save game message)
    /// - Save structure: saves/[SaveName]/
    ///   - savenfo.res      - Save metadata (GFF with "NFO " signature) - FUN_004eb750 writes this
    ///   - savegame.sav     - ERF archive with "MOD V1.0" signature containing:
    ///     - GLOBALVARS.res - Global variables (GFF with "VAR " signature)
    ///     - PARTYTABLE.res - Party state (GFF with "PT  " signature)
    ///     - [module]_s.rim - Per-module state data (ERF archive per module)
    ///   - screen.tga       - Screenshot (TGA format image)
    ///   - SAVEGAME.xxx     - Platform-specific data (varies by platform)
    /// </remarks>
    public class SaveDataProvider : ISaveDataProvider
    {
        private readonly string _savesDirectory;
        private readonly ISaveSerializer _serializer;

        /// <summary>
        /// Gets the base saves directory path.
        /// </summary>
        public string SavesDirectory
        {
            get { return _savesDirectory; }
        }

        public SaveDataProvider(string savesDirectory, ISaveSerializer serializer)
        {
            if (string.IsNullOrEmpty(savesDirectory))
            {
                throw new ArgumentNullException("savesDirectory");
            }

            _savesDirectory = savesDirectory;
            _serializer = serializer ?? throw new ArgumentNullException("serializer");

            // Ensure saves directory exists
            SaveFolderIO.EnsureDirectoryExists(_savesDirectory);

            // If serializer supports saves directory setting, configure it
            // This allows the serializer to resolve save paths from save names
            if (_serializer is SaveSerializer saveSerializer)
            {
                saveSerializer.SetSavesDirectory(_savesDirectory);
            }
        }

        /// <summary>
        /// Gets the path to a specific save folder.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_004eb750 @ 0x004eb750
        /// Original implementation constructs path as "SAVES:\{saveName}\"
        /// Save name format: "%06d - %s" (6-digit number - name) from string @ 0x007be298
        /// </remarks>
        private string GetSavePath(string saveName)
        {
            // Original engine uses "SAVES:" prefix, but we use filesystem path
            // Sanitize save name for filesystem
            string safeName = SanitizeSaveName(saveName);
            return Path.Combine(_savesDirectory, safeName);
        }

        /// <summary>
        /// Formats save name in original engine format: "%06d - %s"
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00708990 @ 0x00708990
        /// Located via string reference: "%06d - %s" @ 0x007be298
        /// Original uses 6-digit zero-padded number followed by " - " and save name
        /// </remarks>
        private string FormatSaveName(int saveNumber, string saveName)
        {
            return string.Format("{0:D6} - {1}", saveNumber, saveName ?? "");
        }

        /// <summary>
        /// Gets the path to the slot folder for numbered saves.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00708990 @ 0x00708990
        /// Original uses format "%06d - %s" for save names
        /// </remarks>
        private string GetSlotPath(int slotNumber)
        {
            // Use original engine format: 6-digit zero-padded number
            string slotName = string.Format("{0:D6}", slotNumber);
            return Path.Combine(_savesDirectory, slotName);
        }

        private string SanitizeSaveName(string name)
        {
            // Remove or replace invalid filename characters
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        #region ISaveDataProvider Implementation

        public bool WriteSave(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return false;
            }

            string savePath = GetSavePath(saveData.Name);

            try
            {
                // Create save directory if it doesn't exist
                SaveFolderIO.EnsureDirectoryExists(savePath);

                // Write save NFO (metadata)
                WriteSaveNfo(savePath, saveData);

                // Write main save archive
                WriteSaveArchive(savePath, saveData);

                // Write screenshot if present
                if (saveData.Screenshot != null && saveData.Screenshot.Length > 0)
                {
                    WriteScreenshot(savePath, saveData.Screenshot);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public SaveGameData ReadSave(string saveName)
        {
            string savePath = GetSavePath(saveName);

            if (!SaveFolderIO.DirectoryExists(savePath))
            {
                return null;
            }

            try
            {
                // Read save NFO for metadata
                SaveGameData saveData = ReadSaveNfo(savePath);
                if (saveData == null)
                {
                    return null;
                }

                // Read main save archive
                ReadSaveArchive(savePath, saveData);

                // Read screenshot if present
                saveData.Screenshot = ReadScreenshot(savePath);

                return saveData;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public IEnumerable<SaveGameInfo> EnumerateSaves()
        {
            var saves = new List<SaveGameInfo>();

            if (!SaveFolderIO.DirectoryExists(_savesDirectory))
            {
                return saves;
            }

            foreach (string dir in SaveFolderIO.GetDirectories(_savesDirectory))
            {
                SaveGameInfo info = TryReadSaveInfo(dir);
                if (info != null)
                {
                    saves.Add(info);
                }
            }

            // Sort by save time descending
            saves.Sort((a, b) => b.SaveTime.CompareTo(a.SaveTime));

            return saves;
        }

        public bool DeleteSave(string saveName)
        {
            string savePath = GetSavePath(saveName);

            if (!SaveFolderIO.DirectoryExists(savePath))
            {
                return false;
            }

            try
            {
                SaveFolderIO.DeleteDirectoryRecursive(savePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SaveExists(string saveName)
        {
            string savePath = GetSavePath(saveName);
            return SaveFolderIO.DirectoryExists(savePath);
        }

        #endregion

        #region File Operations

        // Write save metadata to savenfo.res
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_004eb750 @ 0x004eb750
        // Located via string reference: "savenfo" @ 0x007be1f0
        // Original implementation: Constructs path "SAVES:\{saveName}\savenfo", writes GFF with "NFO " signature
        private void WriteSaveNfo(string savePath, SaveGameData saveData)
        {
            // Convert save metadata to bytes
            byte[] nfoData = _serializer.SerializeSaveNfo(saveData);
            SaveFolderIO.WriteSaveNfo(savePath, nfoData);
        }

        private SaveGameData ReadSaveNfo(string savePath)
        {
            byte[] nfoData = SaveFolderIO.ReadSaveNfo(savePath);
            if (nfoData == null || nfoData.Length == 0) return null;
            return _serializer.DeserializeSaveNfo(nfoData);
        }

        // Write save archive to savegame.sav
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_004eb750 @ 0x004eb750
        // Located via string reference: "SAVEGAME" @ 0x007be28c, "MOD V1.0" @ 0x007be0d4
        // Original implementation: Constructs path "SAVES:\{saveName}\SAVEGAME", writes ERF with "MOD V1.0" signature
        private void WriteSaveArchive(string savePath, SaveGameData saveData)
        {
            // Build ERF archive containing save data
            byte[] archiveData = _serializer.SerializeSaveArchive(saveData);
            SaveFolderIO.WriteSaveArchive(savePath, archiveData);
        }

        private void ReadSaveArchive(string savePath, SaveGameData saveData)
        {
            byte[] archiveData = SaveFolderIO.ReadSaveArchive(savePath);
            if (archiveData == null || archiveData.Length == 0) return;
            _serializer.DeserializeSaveArchive(archiveData, saveData);
        }

        private void WriteScreenshot(string savePath, byte[] screenshot)
        {
            SaveFolderIO.WriteScreenshot(savePath, screenshot);
        }

        private byte[] ReadScreenshot(string savePath)
        {
            return SaveFolderIO.ReadScreenshot(savePath);
        }

        private SaveGameInfo TryReadSaveInfo(string savePath)
        {
            try
            {
                byte[] nfoData = SaveFolderIO.ReadSaveNfo(savePath);
                if (nfoData == null || nfoData.Length == 0)
                {
                    return null;
                }

                SaveGameData saveData = _serializer.DeserializeSaveNfo(nfoData);

                if (saveData == null)
                {
                    return null;
                }

                return new SaveGameInfo
                {
                    Name = saveData.Name,
                    SaveType = saveData.SaveType,
                    SaveTime = saveData.SaveTime,
                    ModuleName = saveData.CurrentModule,
                    PlayTime = saveData.PlayTime,
                    SavePath = savePath
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

        #region Slot-Based Saves

        /// <summary>
        /// Gets the next available save slot number.
        /// </summary>
        public int GetNextSlotNumber()
        {
            int slot = 1;
            while (SaveFolderIO.DirectoryExists(GetSlotPath(slot)))
            {
                slot++;
                if (slot > 999)
                {
                    throw new InvalidOperationException("No available save slots");
                }
            }
            return slot;
        }

        /// <summary>
        /// Writes to a specific slot number.
        /// </summary>
        public bool WriteToSlot(int slotNumber, SaveGameData saveData)
        {
            string slotName = string.Format("Save{0:D2}", slotNumber);
            saveData.Name = slotName;
            return WriteSave(saveData);
        }

        /// <summary>
        /// Gets all auto-saves.
        /// </summary>
        public IEnumerable<SaveGameInfo> GetAutoSaves()
        {
            var saves = new List<SaveGameInfo>();

            foreach (SaveGameInfo info in EnumerateSaves())
            {
                if (info.SaveType == SaveType.Auto)
                {
                    saves.Add(info);
                }
            }

            return saves;
        }

        /// <summary>
        /// Gets the quick save.
        /// </summary>
        public SaveGameInfo GetQuickSave()
        {
            string quickSavePath = Path.Combine(_savesDirectory, "quicksave");

            if (!SaveFolderIO.DirectoryExists(quickSavePath))
            {
                return null;
            }

            return TryReadSaveInfo(quickSavePath);
        }

        #endregion
    }

    /// <summary>
    /// Interface for save data serialization.
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>
        /// Serializes save metadata to GFF bytes.
        /// </summary>
        byte[] SerializeSaveNfo(SaveGameData saveData);

        /// <summary>
        /// Deserializes save metadata from GFF bytes.
        /// </summary>
        SaveGameData DeserializeSaveNfo(byte[] data);

        /// <summary>
        /// Serializes save data to ERF archive bytes.
        /// </summary>
        byte[] SerializeSaveArchive(SaveGameData saveData);

        /// <summary>
        /// Deserializes save archive into existing SaveGameData.
        /// </summary>
        void DeserializeSaveArchive(byte[] data, SaveGameData saveData);
    }
}

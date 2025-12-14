using System;
using System.Collections.Generic;
using System.IO;
using Odyssey.Core.Save;

namespace Odyssey.Content.Save
{
    /// <summary>
    /// Provides save data persistence using KOTOR save file format.
    /// </summary>
    /// <remarks>
    /// KOTOR Save Structure:
    /// - saves/[SaveName]/
    ///   - savenfo.res      - Save metadata (GFF)
    ///   - savegame.sav     - ERF archive containing:
    ///     - GLOBALVARS.res - Global variables (GFF)
    ///     - PARTYTABLE.res - Party state (GFF)
    ///     - [module]_s.rim - Per-module state data
    ///   - screen.tga       - Screenshot
    ///   - SAVEGAME.xxx     - Platform-specific data
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
            if (!Directory.Exists(_savesDirectory))
            {
                Directory.CreateDirectory(_savesDirectory);
            }
        }

        /// <summary>
        /// Gets the path to a specific save folder.
        /// </summary>
        private string GetSavePath(string saveName)
        {
            // Sanitize save name for filesystem
            string safeName = SanitizeSaveName(saveName);
            return Path.Combine(_savesDirectory, safeName);
        }

        /// <summary>
        /// Gets the path to the slot folder for numbered saves.
        /// </summary>
        private string GetSlotPath(int slotNumber)
        {
            string slotName = string.Format("Save{0:D2}", slotNumber);
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
                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                }

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

            if (!Directory.Exists(savePath))
            {
                return null;
            }

            try
            {
                // Read save NFO for metadata
                var saveData = ReadSaveNfo(savePath);
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

            if (!Directory.Exists(_savesDirectory))
            {
                return saves;
            }

            foreach (string dir in Directory.GetDirectories(_savesDirectory))
            {
                var info = TryReadSaveInfo(dir);
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

            if (!Directory.Exists(savePath))
            {
                return false;
            }

            try
            {
                Directory.Delete(savePath, true);
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
            return Directory.Exists(savePath);
        }

        #endregion

        #region File Operations

        private void WriteSaveNfo(string savePath, SaveGameData saveData)
        {
            string nfoPath = Path.Combine(savePath, "savenfo.res");

            // Convert save metadata to bytes
            byte[] nfoData = _serializer.SerializeSaveNfo(saveData);

            File.WriteAllBytes(nfoPath, nfoData);
        }

        private SaveGameData ReadSaveNfo(string savePath)
        {
            string nfoPath = Path.Combine(savePath, "savenfo.res");

            if (!File.Exists(nfoPath))
            {
                return null;
            }

            byte[] nfoData = File.ReadAllBytes(nfoPath);
            return _serializer.DeserializeSaveNfo(nfoData);
        }

        private void WriteSaveArchive(string savePath, SaveGameData saveData)
        {
            string savFile = Path.Combine(savePath, "savegame.sav");

            // Build ERF archive containing save data
            byte[] archiveData = _serializer.SerializeSaveArchive(saveData);

            File.WriteAllBytes(savFile, archiveData);
        }

        private void ReadSaveArchive(string savePath, SaveGameData saveData)
        {
            string savFile = Path.Combine(savePath, "savegame.sav");

            if (!File.Exists(savFile))
            {
                return;
            }

            byte[] archiveData = File.ReadAllBytes(savFile);
            _serializer.DeserializeSaveArchive(archiveData, saveData);
        }

        private void WriteScreenshot(string savePath, byte[] screenshot)
        {
            string screenshotPath = Path.Combine(savePath, "screen.tga");
            File.WriteAllBytes(screenshotPath, screenshot);
        }

        private byte[] ReadScreenshot(string savePath)
        {
            string screenshotPath = Path.Combine(savePath, "screen.tga");

            if (!File.Exists(screenshotPath))
            {
                return null;
            }

            return File.ReadAllBytes(screenshotPath);
        }

        private SaveGameInfo TryReadSaveInfo(string savePath)
        {
            try
            {
                string nfoPath = Path.Combine(savePath, "savenfo.res");

                if (!File.Exists(nfoPath))
                {
                    return null;
                }

                byte[] nfoData = File.ReadAllBytes(nfoPath);
                var saveData = _serializer.DeserializeSaveNfo(nfoData);

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
            while (Directory.Exists(GetSlotPath(slot)))
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

            foreach (var info in EnumerateSaves())
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

            if (!Directory.Exists(quickSavePath))
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

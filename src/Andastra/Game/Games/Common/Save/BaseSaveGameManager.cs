using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Runtime.Core.Save;

namespace Andastra.Game.Games.Common.Save
{
    /// <summary>
    /// Base implementation of save game management shared across engines that use numbered save directory naming.
    /// </summary>
    /// <remarks>
    /// Base Save Game Manager Implementation:
    /// - Common save directory naming format: "%06d - %s" (6-digit number - name)
    /// - Save number auto-generation and parsing
    /// - Directory name formatting and parsing
    /// - Common across engines: Odyssey (swkotor.exe, swkotor2.exe) and Aurora (nwmain.exe)
    ///
    /// Based on reverse engineering of save game management across multiple BioWare engines.
    ///
    /// Common functionality:
    /// - Save directory naming: "%06d - %s" format (6-digit zero-padded number, " - ", save name)
    /// - Save number auto-generation: Scans existing saves to find highest number, increments
    /// - Directory name parsing: Extracts save number and name from formatted directory names
    /// - Backward compatibility: Supports both formatted and unformatted directory names
    ///
    /// Engine-specific details (in subclasses):
    /// - Save file format (ERF for Odyssey, different for Aurora)
    /// - Save metadata structure (NFO GFF format specifics)
    /// - Save file location and naming conventions
    /// - Engine-specific save data serialization
    /// </remarks>
    public abstract class BaseSaveGameManager
    {
        protected readonly string _savesDirectory;

        /// <summary>
        /// Initializes a new instance of the BaseSaveGameManager class.
        /// </summary>
        /// <param name="savesDirectory">Base directory for save games.</param>
        protected BaseSaveGameManager(string savesDirectory)
        {
            _savesDirectory = savesDirectory ?? throw new ArgumentNullException("savesDirectory");

            // Ensure saves directory exists
            if (!Directory.Exists(_savesDirectory))
            {
                Directory.CreateDirectory(_savesDirectory);
            }
        }

        /// <summary>
        /// Gets the base saves directory path.
        /// </summary>
        public string SavesDirectory => _savesDirectory;

        #region Common Save Directory Naming

        /// <summary>
        /// Formats a save directory name using the common engine format: "%06d - %s"
        /// </summary>
        /// <remarks>
        /// Common format across Odyssey and Aurora engines:
        /// Format: 6-digit zero-padded save number, followed by " - ", followed by save name
        /// Example: "000001 - MySave" for save number 1 with name "MySave"
        /// </remarks>
        protected string FormatSaveDirectoryName(int saveNumber, string saveName)
        {
            if (string.IsNullOrEmpty(saveName))
            {
                throw new ArgumentException("Save name cannot be null or empty", "saveName");
            }

            // Format: 6-digit zero-padded number, " - ", save name
            // Common format string "%06d - %s" used across Odyssey and Aurora engines
            return string.Format("{0:D6} - {1}", saveNumber, saveName);
        }

        /// <summary>
        /// Parses the save number from a formatted directory name.
        /// </summary>
        /// <remarks>
        /// Parses format "%06d - %s" to extract the 6-digit save number
        /// Returns 0 if the format doesn't match (for backward compatibility with unformatted names)
        /// Common parsing logic across Odyssey and Aurora engines
        /// </remarks>
        protected int ParseSaveNumberFromDirectory(string directoryName)
        {
            if (string.IsNullOrEmpty(directoryName))
            {
                return 0;
            }

            // Format: "000001 - SaveName" or just "SaveName" (backward compatibility)
            int dashIndex = directoryName.IndexOf(" - ");
            if (dashIndex > 0 && dashIndex == 6)
            {
                // Check if first 6 characters are digits
                string numberPart = directoryName.Substring(0, 6);
                int saveNumber;
                if (int.TryParse(numberPart, out saveNumber))
                {
                    return saveNumber;
                }
            }

            // Not in formatted format, return 0 (backward compatibility)
            return 0;
        }

        /// <summary>
        /// Parses the save name from a formatted directory name.
        /// </summary>
        /// <remarks>
        /// Parses format "%06d - %s" to extract the save name part (after " - ")
        /// Returns the original directory name if the format doesn't match (for backward compatibility)
        /// Common parsing logic across Odyssey and Aurora engines
        /// </remarks>
        protected string ParseSaveNameFromDirectory(string directoryName)
        {
            if (string.IsNullOrEmpty(directoryName))
            {
                return directoryName;
            }

            // Format: "000001 - SaveName" or just "SaveName" (backward compatibility)
            int dashIndex = directoryName.IndexOf(" - ");
            if (dashIndex > 0 && dashIndex == 6)
            {
                // Check if first 6 characters are digits
                string numberPart = directoryName.Substring(0, 6);
                int saveNumber;
                if (int.TryParse(numberPart, out saveNumber))
                {
                    // Extract name part (after " - ")
                    if (directoryName.Length > dashIndex + 3)
                    {
                        return directoryName.Substring(dashIndex + 3);
                    }
                }
            }

            // Not in formatted format, return original (backward compatibility)
            return directoryName;
        }

        /// <summary>
        /// Gets the next available save number by scanning existing saves.
        /// </summary>
        /// <remarks>
        /// Common save number auto-increment logic across Odyssey and Aurora engines
        /// Scans save directory, parses save numbers from directory names,
        /// finds the highest number, and returns the next available number (highest + 1)
        /// Starts at 1 if no saves exist
        /// </remarks>
        protected int GetNextSaveNumber()
        {
            int maxSaveNumber = 0;

            if (Directory.Exists(_savesDirectory))
            {
                foreach (string saveDir in Directory.GetDirectories(_savesDirectory))
                {
                    string dirName = Path.GetFileName(saveDir);
                    int saveNumber = ParseSaveNumberFromDirectory(dirName);
                    if (saveNumber > maxSaveNumber)
                    {
                        maxSaveNumber = saveNumber;
                    }
                }
            }

            // Return next available number (highest + 1, or 1 if no saves exist)
            return maxSaveNumber + 1;
        }

        #endregion

        #region Abstract Methods (Engine-Specific)

        /// <summary>
        /// Saves the current game state to a save file.
        /// </summary>
        /// <param name="saveData">Save game data to save.</param>
        /// <param name="saveName">Name for the save game.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if save succeeded.</returns>
        public abstract Task<bool> SaveGameAsync(Runtime.Core.Save.SaveGameData saveData, string saveName, CancellationToken ct = default);

        /// <summary>
        /// Loads a save game from a save file.
        /// </summary>
        /// <param name="saveName">Name of the save game to load.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Loaded save game data, or null if not found.</returns>
        public abstract Task<Runtime.Core.Save.SaveGameData> LoadGameAsync(string saveName, CancellationToken ct = default);

        /// <summary>
        /// Lists all available save games.
        /// </summary>
        /// <returns>Enumerable of save game information.</returns>
        public abstract IEnumerable<SaveGameInfo> ListSaves();

        #endregion
    }
}


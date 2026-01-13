using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Extract.Installation;

namespace Andastra.Game.Games.Aurora.Data
{
    /// <summary>
    /// Manages loading and caching of 2DA tables for game data lookup (Aurora engine).
    /// </summary>
    /// <remarks>
    /// Aurora 2DA Table Manager:
    /// - Based on nwmain.exe: C2DA::Load2DArray @ 0x1401a73a0, CTwoDimArrays::Load2DArrays @ 0x1402b3920
    /// - Located via string references: "2DA has no rows: '%s.2da'" @ 0x140da5e80, "C2DA::Load2DArray(): No row label: %s.2da; Row: %d" @ 0x140da5ea0
    /// - Error messages: "Already loaded Appearance.TwoDA!" @ 0x140dc5dd8, "Failed to load Appearance.TwoDA!" @ 0x140dc5e08
    /// - Cross-engine analysis:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): Similar 2DA loading via resource system
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): 2DA system is UnrealScript-based (different architecture)
    /// - Original implementation: Loads 2DA files from installation archives (hak files, module files, etc.) via resource system
    /// - Resource precedence: override → module → hak → base game archives
    /// - Table lookup: Uses row label (string) or row index (int) to access data
    /// - Column access: Column names are case-insensitive (e.g., "ModelA", "modela" both work)
    /// - Based on BioWare.NET.Resource.Formats.TwoDA.TwoDA for parsing
    /// - Key 2DA tables (Aurora-specific): appearance.2da, baseitems.2da, classes.2da, feat.2da, spells.2da, skills.2da, surfacemat.2da, portraits.2da, placeables.2da, doors.2da
    /// </remarks>
    public class AuroraTwoDATableManager
    {
        private readonly Installation _installation;
        private readonly Dictionary<string, TwoDA> _cachedTables;

        /// <summary>
        /// Initializes a new instance of the Aurora 2DA table manager.
        /// </summary>
        /// <param name="installation">The game installation for accessing 2DA files.</param>
        public AuroraTwoDATableManager(Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException("installation");
            _cachedTables = new Dictionary<string, TwoDA>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a 2DA table by name (e.g., "appearance", "baseitems").
        /// </summary>
        /// <param name="tableName">Table name without extension (e.g., "baseitems")</param>
        /// <returns>The loaded 2DA, or null if not found</returns>
        /// <remarks>
        /// Based on nwmain.exe: C2DA::Load2DArray @ 0x1401a73a0
        /// Loads 2DA files from installation resource system with caching
        /// </remarks>
        public TwoDA GetTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return null;
            }

            // Check cache first
            TwoDA cached;
            if (_cachedTables.TryGetValue(tableName, out cached))
            {
                return cached;
            }

            // Load from installation
            try
            {
                ResourceResult resource = _installation.Resources.LookupResource(tableName, BioWare.NET.Common.ResourceType.TwoDA);
                if (resource == null || resource.Data == null)
                {
                    return null;
                }

                TwoDA table = TwoDA.FromBytes(resource.Data);
                _cachedTables[tableName] = table;
                return table;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a row from a 2DA table by row index.
        /// </summary>
        /// <param name="tableName">Table name without extension</param>
        /// <param name="rowIndex">Row index (0-based)</param>
        /// <returns>The TwoDARow, or null if not found</returns>
        /// <remarks>
        /// Based on nwmain.exe: C2DA row access via index
        /// </remarks>
        public TwoDARow GetRow(string tableName, int rowIndex)
        {
            TwoDA table = GetTable(tableName);
            if (table == null)
            {
                return null;
            }

            return table.GetRow(rowIndex);
        }

        /// <summary>
        /// Gets a row from a 2DA table by row label.
        /// </summary>
        /// <param name="tableName">Table name without extension</param>
        /// <param name="rowLabel">Row label (string identifier)</param>
        /// <returns>The TwoDARow, or null if not found</returns>
        /// <remarks>
        /// Based on nwmain.exe: C2DA row access via label
        /// </remarks>
        public TwoDARow GetRowByLabel(string tableName, string rowLabel)
        {
            TwoDA table = GetTable(tableName);
            if (table == null)
            {
                return null;
            }

            return table.FindRow(rowLabel);
        }

        /// <summary>
        /// Gets a value from a 2DA table cell.
        /// </summary>
        /// <param name="tableName">Table name without extension</param>
        /// <param name="rowIndex">Row index (0-based)</param>
        /// <param name="columnName">Column name</param>
        /// <returns>The cell value as string, or empty string if not found</returns>
        public string GetCellValue(string tableName, int rowIndex, string columnName)
        {
            TwoDARow row = GetRow(tableName, rowIndex);
            if (row == null)
            {
                return string.Empty;
            }

            return row.GetString(columnName);
        }

        /// <summary>
        /// Gets an integer value from a 2DA table cell.
        /// </summary>
        /// <param name="tableName">Table name without extension</param>
        /// <param name="rowIndex">Row index (0-based)</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultValue">Default value if not found or invalid</param>
        /// <returns>The integer value, or defaultValue if not found</returns>
        public int? GetCellInt(string tableName, int rowIndex, string columnName, int? defaultValue = null)
        {
            TwoDARow row = GetRow(tableName, rowIndex);
            if (row == null)
            {
                return defaultValue;
            }

            return row.GetInteger(columnName, defaultValue);
        }

        /// <summary>
        /// Gets a float value from a 2DA table cell.
        /// </summary>
        /// <param name="tableName">Table name without extension</param>
        /// <param name="rowIndex">Row index (0-based)</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultValue">Default value if not found or invalid</param>
        /// <returns>The float value, or defaultValue if not found</returns>
        public float? GetCellFloat(string tableName, int rowIndex, string columnName, float? defaultValue = null)
        {
            TwoDARow row = GetRow(tableName, rowIndex);
            if (row == null)
            {
                return defaultValue;
            }

            return row.GetFloat(columnName, defaultValue);
        }

        /// <summary>
        /// Gets a boolean value from a 2DA table cell.
        /// </summary>
        /// <param name="tableName">Table name without extension</param>
        /// <param name="rowIndex">Row index (0-based)</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultValue">Default value if not found or invalid</param>
        /// <returns>The boolean value, or defaultValue if not found</returns>
        public bool? GetCellBool(string tableName, int rowIndex, string columnName, bool? defaultValue = null)
        {
            TwoDARow row = GetRow(tableName, rowIndex);
            if (row == null)
            {
                return defaultValue;
            }

            return row.GetBoolean(columnName, defaultValue);
        }

        /// <summary>
        /// Clears the table cache.
        /// </summary>
        public void ClearCache()
        {
            _cachedTables.Clear();
        }

        /// <summary>
        /// Clears a specific table from the cache.
        /// </summary>
        /// <param name="tableName">Table name to clear from cache</param>
        public void ClearTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return;
            }

            _cachedTables.Remove(tableName);
        }
    }
}


using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Extract.Installation;

namespace Andastra.Game.Games.Eclipse.Data
{
    /// <summary>
    /// Manages loading and caching of 2DA tables for game data lookup (Eclipse engine).
    /// </summary>
    /// <remarks>
    /// Eclipse 2DA Table Manager:
    /// - Based on daorigins.exe, DragonAge2.exe, , 
    /// - Eclipse engines use 2DA files similar to Odyssey/Aurora, accessed through Installation.Resources
    /// - Original implementation: Loads 2DA files from installation archives via resource system
    /// - Resource precedence: override → module → base game archives
    /// - Table lookup: Uses row label (string) or row index (int) to access data
    /// - Column access: Column names are case-insensitive (e.g., "ModelA", "modela" both work)
    /// - Based on BioWare.NET.Resource.Formats.TwoDA.TwoDA for parsing
    /// - Key 2DA tables (Eclipse-specific): appearance.2da, baseitems.2da, classes.2da, spells.2da, skills.2da, portraits.2da
    /// - Cross-engine analysis:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): Similar 2DA loading via resource system
    ///   - Aurora (nwmain.exe): C2DA::Load2DArray @ 0x1401a73a0 - loads 2DA files via C2DA class
    ///   - Eclipse: Uses same Installation.Resources.LookupResource pattern as Odyssey/Aurora
    /// </remarks>
    public class EclipseTwoDATableManager
    {
        private readonly Installation _installation;
        private readonly Dictionary<string, TwoDA> _cachedTables;

        /// <summary>
        /// Initializes a new instance of the Eclipse 2DA table manager.
        /// </summary>
        /// <param name="installation">The game installation for accessing 2DA files.</param>
        public EclipseTwoDATableManager(Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException("installation");
            _cachedTables = new Dictionary<string, TwoDA>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a 2DA table by name (e.g., "appearance", "baseitems").
        /// </summary>
        /// <param name="tableName">Table name without extension (e.g., "appearance")</param>
        /// <returns>The loaded 2DA, or null if not found</returns>
        /// <remarks>
        /// Based on Eclipse engine: Loads 2DA files from installation resource system with caching
        /// Eclipse engines use the same 2DA file format and resource lookup system as Odyssey/Aurora
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


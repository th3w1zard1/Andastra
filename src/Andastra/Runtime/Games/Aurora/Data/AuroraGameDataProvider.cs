using System;
using JetBrains.Annotations;
using Andastra.Runtime.Games.Common;
using Andastra.Parsing.Installation;

namespace Andastra.Runtime.Games.Aurora.Data
{
    /// <summary>
    /// Aurora-specific implementation of IGameDataProvider.
    /// </summary>
    /// <remarks>
    /// Aurora Game Data Provider:
    /// - Based on nwmain.exe: C2DA::Load2DArray @ 0x1401a73a0, CTwoDimArrays::Load2DArrays @ 0x1402b3920
    /// - Located via string references: "Already loaded Appearance.TwoDA!" @ 0x140dc5dd8, "Failed to load Appearance.TwoDA!" @ 0x140dc5e08
    /// - Error messages: "2DA has no rows: '%s.2da'" @ 0x140da5e80, "C2DA::Load2DArray(): No row label: %s.2da; Row: %d" @ 0x140da5ea0
    /// - Original implementation: Uses C2DA class to access 2DA tables (appearance.2da, etc.)
    /// - Uses AuroraTwoDATableManager to access 2DA tables (appearance.2da, baseitems.2da, etc.)
    /// - Wraps AuroraTwoDATableManager to provide engine-agnostic interface
    /// - Cross-engine analysis:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): Uses GameDataManager for 2DA access, FUN_0041d2c0 @ 0x0041d2c0 (2DA table lookup), FUN_0065a380 @ 0x0065a380 (GetCreatureRadius)
    ///   - Aurora (nwmain.exe): Uses C2DA class for 2DA access, C2DA::Load2DArray @ 0x1401a73a0
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): Uses EclipseTwoDATableManager (same pattern as Aurora)
    ///   - Infinity (, ): Uses 2DA tables (similar to Eclipse)
    /// - Common pattern: All engines store creature properties (radius, speed, etc.) in data tables indexed by appearance type
    /// </remarks>
    [PublicAPI]
    public class AuroraGameDataProvider : IGameDataProvider
    {
        private readonly AuroraTwoDATableManager _tableManager;

        /// <summary>
        /// Initializes a new instance of the Aurora game data provider.
        /// </summary>
        /// <param name="installation">The game installation for accessing 2DA files.</param>
        /// <remarks>
        /// Based on nwmain.exe: Aurora engine uses C2DA class for 2DA table access
        /// - Creates AuroraTwoDATableManager to handle 2DA table loading and caching
        /// - Resource precedence: override → module → hak → base game archives
        /// </remarks>
        public AuroraGameDataProvider(Installation installation)
        {
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }

            _tableManager = new AuroraTwoDATableManager(installation);
        }

        /// <summary>
        /// Gets the underlying AuroraTwoDATableManager.
        /// </summary>
        public AuroraTwoDATableManager TableManager
        {
            get { return _tableManager; }
        }

        /// <summary>
        /// Gets the creature collision radius for a given appearance type.
        /// </summary>
        /// <param name="appearanceType">The appearance type index (row index in appearance.2da).</param>
        /// <param name="defaultRadius">Default radius to return if lookup fails.</param>
        /// <returns>The creature collision radius, or defaultRadius if lookup fails.</returns>
        /// <remarks>
        /// Based on nwmain.exe: Aurora engine uses C2DA class for 2DA table access
        /// - Looks up "hitradius" column from appearance.2da table using appearanceType as row index
        /// - Falls back to size-based defaults if appearance data unavailable:
        ///   - Size 0 (Small): 0.3
        ///   - Size 1 (Medium): 0.5 (default)
        ///   - Size 2 (Large): 0.7
        ///   - Size 3 (Huge): 1.0
        ///   - Size 4 (Gargantuan): 1.5
        /// - Cross-engine pattern: Same as Odyssey (swkotor2.exe: FUN_0065a380 @ 0x0065a380) and Eclipse
        /// - Original implementation: C2DA::GetFloatingPoint @ nwmain.exe accesses 2DA cell values
        /// </remarks>
        public float GetCreatureRadius(int appearanceType, float defaultRadius = 0.5f)
        {
            if (appearanceType < 0)
            {
                return defaultRadius;
            }

            // Get appearance data from table manager
            Parsing.Formats.TwoDA.TwoDA appearanceTable = _tableManager.GetTable("appearance");
            if (appearanceTable == null || appearanceType >= appearanceTable.GetHeight())
            {
                return defaultRadius;
            }

            Parsing.Formats.TwoDA.TwoDARow row = appearanceTable.GetRow(appearanceType);
            if (row == null)
            {
                return defaultRadius;
            }

            // Try to get hitradius from appearance data
            // Based on nwmain.exe: C2DA::GetFloatingPoint accesses "hitradius" column from appearance.2da
            float? hitRadius = row.GetFloat("hitradius");
            if (hitRadius.HasValue && hitRadius.Value > 0.0f)
            {
                return hitRadius.Value;
            }

            // Fall back to size-based defaults if appearance data unavailable
            // Based on nwmain.exe: Size categories determine default radius
            // Size categories: 0=Small, 1=Medium, 2=Large, 3=Huge, 4=Gargantuan
            int? sizeCategory = row.GetInteger("sizecategory");
            if (sizeCategory.HasValue)
            {
                switch (sizeCategory.Value)
                {
                    case 0: // Small
                        return 0.3f;
                    case 1: // Medium
                        return 0.5f;
                    case 2: // Large
                        return 0.7f;
                    case 3: // Huge
                        return 1.0f;
                    case 4: // Gargantuan
                        return 1.5f;
                    default:
                        return defaultRadius;
                }
            }

            return defaultRadius;
        }

        /// <summary>
        /// Gets a float value from a game data table.
        /// </summary>
        /// <param name="tableName">The table name (e.g., "appearance" for appearance.2da).</param>
        /// <param name="rowIndex">The row index.</param>
        /// <param name="columnName">The column name (case-insensitive).</param>
        /// <param name="defaultValue">Default value to return if lookup fails.</param>
        /// <returns>The float value, or defaultValue if lookup fails.</returns>
        /// <remarks>
        /// Based on nwmain.exe: Aurora engine uses C2DA class for 2DA table access
        /// - C2DA::GetFloatingPoint @ nwmain.exe accesses 2DA cell values by row index and column name
        /// - Uses AuroraTwoDATableManager to access 2DA tables
        /// - Cross-engine pattern: Same as Odyssey (swkotor2.exe: FUN_0041d2c0 @ 0x0041d2c0) and Eclipse
        /// - Original implementation: C2DA::Load2DArray @ 0x1401a73a0 loads tables, C2DA::GetFloatingPoint retrieves float values
        /// </remarks>
        public float GetTableFloat(string tableName, int rowIndex, string columnName, float defaultValue = 0.0f)
        {
            if (string.IsNullOrEmpty(tableName) || rowIndex < 0 || string.IsNullOrEmpty(columnName))
            {
                return defaultValue;
            }

            // Get table from table manager
            Parsing.Formats.TwoDA.TwoDA table = _tableManager.GetTable(tableName);
            if (table == null)
            {
                return defaultValue;
            }

            // Get row by index
            if (rowIndex >= table.GetHeight())
            {
                return defaultValue;
            }

            Parsing.Formats.TwoDA.TwoDARow row = table.GetRow(rowIndex);
            if (row == null)
            {
                return defaultValue;
            }

            // Get float value from column
            // Based on nwmain.exe: C2DA::GetFloatingPoint retrieves float values from 2DA cells
            float? value = row.GetFloat(columnName);
            return value ?? defaultValue;
        }
    }
}


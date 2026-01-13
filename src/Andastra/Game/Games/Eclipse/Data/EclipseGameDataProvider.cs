using System;
using BioWare.NET.Extract;
using Andastra.Game.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Eclipse.Data
{
    /// <summary>
    /// Eclipse-specific implementation of IGameDataProvider.
    /// </summary>
    /// <remarks>
    /// Eclipse Game Data Provider:
    /// - Based on daorigins.exe, DragonAge2.exe, , 
    /// - Eclipse engines use 2DA files similar to Odyssey/Aurora, accessed through Installation.Resources
    /// - Original implementation: Loads 2DA tables from installation archives via resource system
    /// - Uses EclipseTwoDATableManager to access 2DA tables (appearance.2da, baseitems.2da, etc.)
    /// - Wraps EclipseTwoDATableManager to provide engine-agnostic interface
    /// - Cross-engine analysis:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): Uses GameDataManager for 2DA access, 0x0041d2c0 @ 0x0041d2c0 (2DA table lookup), 0x0065a380 @ 0x0065a380 (GetCreatureRadius)
    ///   - Aurora (nwmain.exe): Uses C2DA class for 2DA access, C2DA::Load2DArray @ 0x1401a73a0
    ///   - Eclipse: Uses same Installation.Resources.LookupResource pattern as Odyssey/Aurora
    /// </remarks>
    [PublicAPI]
    public class EclipseGameDataProvider : IGameDataProvider
    {
        private readonly EclipseTwoDATableManager _tableManager;

        /// <summary>
        /// Initializes a new instance of the Eclipse game data provider.
        /// </summary>
        /// <param name="installation">The game installation for accessing 2DA files.</param>
        public EclipseGameDataProvider(Installation installation)
        {
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }

            _tableManager = new EclipseTwoDATableManager(installation);
        }

        /// <summary>
        /// Gets the creature collision radius for a given appearance type.
        /// </summary>
        /// <param name="appearanceType">The appearance type index (row index in appearance.2da).</param>
        /// <param name="defaultRadius">Default radius to return if lookup fails.</param>
        /// <returns>The creature collision radius, or defaultRadius if lookup fails.</returns>
        /// <remarks>
        /// Based on Eclipse engine: Looks up creature properties from appearance.2da using EclipseTwoDATableManager
        /// - Looks up "hitradius" column from appearance.2da table using appearanceType as row index
        /// - Falls back to size-based defaults if appearance data unavailable:
        ///   - Size 0 (Small): 0.3
        ///   - Size 1 (Medium): 0.5 (default)
        ///   - Size 2 (Large): 0.7
        ///   - Size 3 (Huge): 1.0
        ///   - Size 4 (Gargantuan): 1.5
        /// - Cross-engine pattern: Same as Odyssey (swkotor2.exe: 0x0065a380 @ 0x0065a380) and Aurora
        /// </remarks>
        public float GetCreatureRadius(int appearanceType, float defaultRadius = 0.5f)
        {
            if (appearanceType < 0)
            {
                return defaultRadius;
            }

            // Get appearance data from table manager
            BioWare.NET.Resource.Formats.TwoDA.TwoDA appearanceTable = _tableManager.GetTable("appearance");
            if (appearanceTable == null || appearanceType >= appearanceTable.GetHeight())
            {
                return defaultRadius;
            }

            BioWare.NET.Resource.Formats.TwoDA.TwoDARow row = appearanceTable.GetRow(appearanceType);
            if (row == null)
            {
                return defaultRadius;
            }

            // Try to get hitradius from appearance data
            float? hitRadius = row.GetFloat("hitradius");
            if (hitRadius.HasValue && hitRadius.Value > 0.0f)
            {
                return hitRadius.Value;
            }

            // Fall back to size-based defaults if appearance data unavailable
            // Based on Eclipse engine: Size categories determine default radius
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
        /// Based on Eclipse engine: Looks up float value from 2DA table by row index and column name
        /// - Uses EclipseTwoDATableManager to access 2DA tables
        /// - Cross-engine pattern: Same as Odyssey (swkotor2.exe: 0x0041d2c0 @ 0x0041d2c0) and Aurora
        /// </remarks>
        public float GetTableFloat(string tableName, int rowIndex, string columnName, float defaultValue = 0.0f)
        {
            if (string.IsNullOrEmpty(tableName) || rowIndex < 0 || string.IsNullOrEmpty(columnName))
            {
                return defaultValue;
            }

            // Get table from table manager
            BioWare.NET.Resource.Formats.TwoDA.TwoDA table = _tableManager.GetTable(tableName);
            if (table == null)
            {
                return defaultValue;
            }

            // Get row by index
            if (rowIndex >= table.GetHeight())
            {
                return defaultValue;
            }

            BioWare.NET.Resource.Formats.TwoDA.TwoDARow row = table.GetRow(rowIndex);
            if (row == null)
            {
                return defaultValue;
            }

            // Get float value from column
            float? value = row.GetFloat(columnName);
            return value ?? defaultValue;
        }

        /// <summary>
        /// Gets a 2DA table by name.
        /// </summary>
        /// <param name="tableName">The table name without extension (e.g., "appearance" for appearance.2da, "itempropdef" for itempropdef.2da).</param>
        /// <returns>The loaded 2DA table, or null if not found.</returns>
        /// <remarks>
        /// Based on Eclipse engine: Looks up 2DA tables using EclipseTwoDATableManager
        /// - Uses EclipseTwoDATableManager to access 2DA tables with caching
        /// - Cross-engine pattern: Same as Odyssey and Aurora
        /// </remarks>
        [CanBeNull]
        public BioWare.NET.Resource.Formats.TwoDA.TwoDA GetTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return null;
            }

            return _tableManager.GetTable(tableName);
        }
    }
}


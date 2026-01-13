using Andastra.Game.Games.Common;
using JetBrains.Annotations;
using AppearanceData = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.AppearanceData;
using GameDataManager = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager;

namespace Andastra.Game.Games.Odyssey.Data
{
    /// <summary>
    /// Odyssey-specific implementation of IGameDataProvider.
    /// </summary>
    /// <remarks>
    /// Odyssey Game Data Provider:
    /// - Based on swkotor.exe, swkotor2.exe: FUN_0041d2c0 @ 0x0041d2c0 (2DA table lookup), FUN_0065a380 @ 0x0065a380 (GetCreatureRadius)
    /// - Located via string references: "GetCreatureRadius" @ 0x007bb128 (swkotor2.exe), @ 0x00742f1c (swkotor.exe)
    /// - Original implementation: Looks up creature properties from appearance.2da using GameDataManager
    /// - Uses GameDataManager to access 2DA tables (appearance.2da, baseitems.2da, etc.)
    /// - Wraps GameDataManager to provide engine-agnostic interface
    /// </remarks>
    [PublicAPI]
    public class OdysseyGameDataProvider : IGameDataProvider
    {
        private readonly GameDataManager _gameDataManager;

        public OdysseyGameDataProvider(GameDataManager gameDataManager)
        {
            _gameDataManager = gameDataManager ?? throw new System.ArgumentNullException("gameDataManager");
        }

        /// <summary>
        /// Gets the underlying GameDataManager.
        /// </summary>
        public GameDataManager GameDataManager
        {
            get { return _gameDataManager; }
        }

        /// <summary>
        /// Gets the creature collision radius for a given appearance type.
        /// </summary>
        /// <param name="appearanceType">The appearance type index (row index in appearance.2da).</param>
        /// <param name="defaultRadius">Default radius to return if lookup fails.</param>
        /// <returns>The creature collision radius, or defaultRadius if lookup fails.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0065a380 @ 0x0065a380 (GetCreatureRadius)
        /// - Calls FUN_0041d2c0 to lookup "hitradius" column from appearance.2da table
        /// - Uses appearanceType as row index
        /// - Falls back to size-based defaults if appearance data unavailable:
        ///   - Size 0 (Small): 0.3
        ///   - Size 1 (Medium): 0.5 (default)
        ///   - Size 2 (Large): 0.7
        ///   - Size 3 (Huge): 1.0
        ///   - Size 4 (Gargantuan): 1.5
        /// </remarks>
        public float GetCreatureRadius(int appearanceType, float defaultRadius = 0.5f)
        {
            if (appearanceType < 0)
            {
                return defaultRadius;
            }

            // Get appearance data from GameDataManager
            AppearanceData appearance = _gameDataManager.GetAppearance(appearanceType);
            if (appearance != null && appearance.HitRadius > 0.0f)
            {
                return appearance.HitRadius;
            }

            // Fall back to size-based defaults if appearance data unavailable
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Size categories determine default radius
            // Size categories: 0=Small, 1=Medium, 2=Large, 3=Huge, 4=Gargantuan
            if (appearance != null)
            {
                int sizeCategory = appearance.SizeCategory;
                switch (sizeCategory)
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0041d2c0 @ 0x0041d2c0 (2DA table lookup)
        /// - Looks up float value from 2DA table by row index and column name
        /// - Uses GameDataManager to access 2DA tables
        /// </remarks>
        public float GetTableFloat(string tableName, int rowIndex, string columnName, float defaultValue = 0.0f)
        {
            if (string.IsNullOrEmpty(tableName) || rowIndex < 0 || string.IsNullOrEmpty(columnName))
            {
                return defaultValue;
            }

            // Get table from GameDataManager
            Parsing.Formats.TwoDA.TwoDA table = _gameDataManager.GetTable(tableName);
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
            float? value = row.GetFloat(columnName);
            return value ?? defaultValue;
        }

        /// <summary>
        /// Gets a 2DA table by name.
        /// </summary>
        /// <param name="tableName">The table name without extension (e.g., "appearance" for appearance.2da, "itempropdef" for itempropdef.2da).</param>
        /// <returns>The loaded 2DA table, or null if not found.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0041d2c0 @ 0x0041d2c0 (2DA table lookup)
        /// - Uses GameDataManager to access 2DA tables with caching
        /// - Cross-engine pattern: Same as Aurora and Eclipse
        /// </remarks>
        [CanBeNull]
        public Parsing.Formats.TwoDA.TwoDA GetTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return null;
            }

            return _gameDataManager.GetTable(tableName);
        }
    }
}


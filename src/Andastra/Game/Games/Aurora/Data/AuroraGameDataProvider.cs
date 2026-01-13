using System;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Extract.Installation;
using Andastra.Game.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora.Data
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

        /// <summary>
        /// Gets a 2DA table by name.
        /// </summary>
        /// <param name="tableName">The table name without extension (e.g., "appearance" for appearance.2da, "itempropdef" for itempropdef.2da).</param>
        /// <returns>The loaded 2DA table, or null if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: C2DA::Load2DArray @ 0x1401a73a0 for 2DA loading
        /// - Uses AuroraTwoDATableManager to access 2DA tables with caching
        /// - Cross-engine pattern: Same as Odyssey and Eclipse
        /// </remarks>
        [CanBeNull]
        public TwoDA GetTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return null;
            }

            return _tableManager.GetTable(tableName);
        }

        /// <summary>
        /// Gets feat data from feat.2da table.
        /// </summary>
        /// <param name="featId">The feat ID (row index in feat.2da).</param>
        /// <returns>Feat data if found, null otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: C2DA::Load2DArray @ 0x1401a73a0, C2DA::GetInteger @ nwmain.exe
        /// - Original implementation: Aurora uses C2DA class to access feat.2da table
        /// - Located via string references: Feat data in 2DA tables
        /// - Feat data lookup: Accesses feat.2da table using featId as row index
        /// - Column access: Uses C2DA::GetInteger, C2DA::GetString, C2DA::GetFloatingPoint to retrieve feat properties
        /// - Cross-engine pattern: Same as Odyssey (swkotor.exe, swkotor2.exe) but uses C2DA instead of direct 2DA access
        /// </remarks>
        [CanBeNull]
        public AuroraFeatData GetFeat(int featId)
        {
            if (featId < 0)
            {
                return null;
            }

            // Get feat.2da table from table manager
            // Based on nwmain.exe: C2DA::Load2DArray loads feat.2da table
            TwoDA table = _tableManager.GetTable("feat");
            if (table == null || featId >= table.GetHeight())
            {
                return null;
            }

            // Get row by feat ID (row index)
            // Based on nwmain.exe: C2DA row access via index
            TwoDARow row = table.GetRow(featId);
            if (row == null)
            {
                return null;
            }

            // Extract feat data from 2DA row
            // Based on nwmain.exe: C2DA::GetInteger, C2DA::GetString access feat.2da columns
            // Column names match feat.2da format: name, description, icon, usesperday, prereqfeat1, prereqfeat2, etc.
            // Handle missing columns gracefully (some feat.2da files may not have all columns)
            int? usesPerDay = SafeGetInteger(row, "usesperday");
            string name = SafeGetString(row, "name");
            string description = SafeGetString(row, "description");
            int? descriptionStrRef = SafeGetInteger(row, "description");
            string icon = SafeGetString(row, "icon");

            return new AuroraFeatData
            {
                RowIndex = featId,
                Label = row.Label(),
                Name = name ?? string.Empty,
                Description = description ?? string.Empty,
                DescriptionStrRef = descriptionStrRef ?? -1,
                Icon = icon ?? string.Empty,
                FeatCategory = SafeGetInteger(row, "category") ?? 0,
                MaxRanks = SafeGetInteger(row, "maxrank") ?? 0,
                PrereqFeat1 = SafeGetInteger(row, "prereqfeat1") ?? -1,
                PrereqFeat2 = SafeGetInteger(row, "prereqfeat2") ?? -1,
                MinLevel = SafeGetInteger(row, "minlevel") ?? 1,
                MinLevelClass = SafeGetInteger(row, "minlevelclass") ?? -1,
                Selectable = (SafeGetInteger(row, "allclassescanuse") == 1) || (SafeGetInteger(row, "selectable") == 1),
                RequiresAction = SafeGetInteger(row, "requiresaction") == 1,
                UsesPerDay = usesPerDay ?? -1 // -1 = unlimited or special handling, 0+ = daily limit
            };
        }

        /// <summary>
        /// Gets feat data by label (row label in feat.2da).
        /// </summary>
        /// <param name="featLabel">The feat label (e.g., "FEAT_SEE_INVISIBILITY").</param>
        /// <returns>Feat data if found, null otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: C2DA::Load2DArray @ 0x1401a73a0, C2DA row access via label
        /// - Original implementation: Looks up feat by row label in feat.2da table
        /// - Row labels in feat.2da match feat constant names (e.g., "FEAT_SEE_INVISIBILITY")
        /// - Uses FindRow to locate feat by label, then returns AuroraFeatData with row index as FeatId
        /// </remarks>
        [CanBeNull]
        public AuroraFeatData GetFeatByLabel(string featLabel)
        {
            if (string.IsNullOrEmpty(featLabel))
            {
                return null;
            }

            // Get feat.2da table from table manager
            TwoDA table = _tableManager.GetTable("feat");
            if (table == null)
            {
                return null;
            }

            // Find row by label
            // Based on nwmain.exe: C2DA row access via label
            TwoDARow row = table.FindRow(featLabel);
            if (row == null)
            {
                return null;
            }

            // Get row index for the found row
            int? rowIndex = table.RowIndex(row);
            if (!rowIndex.HasValue)
            {
                return null;
            }

            // Extract feat data from 2DA row
            // Handle missing columns gracefully (some feat.2da files may not have all columns)
            int? usesPerDay = SafeGetInteger(row, "usesperday");
            string name = SafeGetString(row, "name");
            string description = SafeGetString(row, "description");
            int? descriptionStrRef = SafeGetInteger(row, "description");
            string icon = SafeGetString(row, "icon");

            return new AuroraFeatData
            {
                RowIndex = rowIndex.Value,
                Label = row.Label(),
                Name = name ?? string.Empty,
                Description = description ?? string.Empty,
                DescriptionStrRef = descriptionStrRef ?? -1,
                Icon = icon ?? string.Empty,
                FeatCategory = SafeGetInteger(row, "category") ?? 0,
                MaxRanks = SafeGetInteger(row, "maxrank") ?? 0,
                PrereqFeat1 = SafeGetInteger(row, "prereqfeat1") ?? -1,
                PrereqFeat2 = SafeGetInteger(row, "prereqfeat2") ?? -1,
                MinLevel = SafeGetInteger(row, "minlevel") ?? 1,
                MinLevelClass = SafeGetInteger(row, "minlevelclass") ?? -1,
                Selectable = (SafeGetInteger(row, "allclassescanuse") == 1) || (SafeGetInteger(row, "selectable") == 1),
                RequiresAction = SafeGetInteger(row, "requiresaction") == 1,
                UsesPerDay = usesPerDay ?? -1 // -1 = unlimited or special handling, 0+ = daily limit
            };
        }

        /// <summary>
        /// Safely gets a string value from a 2DA row, returning null if the column doesn't exist.
        /// </summary>
        /// <param name="row">The 2DA row.</param>
        /// <param name="columnName">The column name.</param>
        /// <returns>The string value, or null if the column doesn't exist.</returns>
        [CanBeNull]
        private static string SafeGetString(TwoDARow row, string columnName)
        {
            try
            {
                return row.GetString(columnName);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Safely gets an integer value from a 2DA row, returning null if the column doesn't exist.
        /// </summary>
        /// <param name="row">The 2DA row.</param>
        /// <param name="columnName">The column name.</param>
        /// <returns>The integer value, or null if the column doesn't exist or is invalid.</returns>
        [CanBeNull]
        private static int? SafeGetInteger(TwoDARow row, string columnName)
        {
            try
            {
                return row.GetInteger(columnName);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Feat data from feat.2da (Aurora engine).
    /// </summary>
    /// <remarks>
    /// Aurora Feat Data:
    /// - Based on nwmain.exe: C2DA class accesses feat.2da table
    /// - Located via string references: Feat data in 2DA tables
    /// - Original implementation: Aurora uses C2DA::GetInteger, C2DA::GetString to retrieve feat properties from feat.2da
    /// - Column structure matches feat.2da format: name, description, icon, usesperday, prereqfeat1, prereqfeat2, category, etc.
    /// - Cross-engine: Similar to Odyssey FeatData but uses C2DA access pattern instead of direct 2DA access
    /// </remarks>
    public class AuroraFeatData : Andastra.Runtime.Games.Common.Components.BaseCreatureComponent.IFeatData
    {
        /// <summary>
        /// Row index in feat.2da (also the feat ID).
        /// </summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// Feat ID (same as RowIndex).
        /// </summary>
        public int FeatId { get { return RowIndex; } set { RowIndex = value; } }

        /// <summary>
        /// Feat label (row label in feat.2da, e.g., "FEAT_SEE_INVISIBILITY").
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Feat name (string reference from feat.2da "name" column).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Feat description (string reference from feat.2da "description" column).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Feat description string reference (integer value from feat.2da "description" column).
        /// </summary>
        public int DescriptionStrRef { get; set; }

        /// <summary>
        /// Feat icon ResRef (from feat.2da "icon" column).
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Feat category identifier (from feat.2da "category" column).
        /// </summary>
        public int FeatCategory { get; set; }

        /// <summary>
        /// Maximum ranks for stackable feats (from feat.2da "maxrank" column).
        /// </summary>
        public int MaxRanks { get; set; }

        /// <summary>
        /// First prerequisite feat ID (from feat.2da "prereqfeat1" column).
        /// </summary>
        public int PrereqFeat1 { get; set; }

        /// <summary>
        /// Second prerequisite feat ID (from feat.2da "prereqfeat2" column).
        /// </summary>
        public int PrereqFeat2 { get; set; }

        /// <summary>
        /// Minimum character level requirement (from feat.2da "minlevel" column).
        /// </summary>
        public int MinLevel { get; set; }

        /// <summary>
        /// Minimum class level requirement (from feat.2da "minlevelclass" column).
        /// </summary>
        public int MinLevelClass { get; set; }

        /// <summary>
        /// Whether the feat is selectable (from feat.2da "selectable" or "allclassescanuse" columns).
        /// </summary>
        public bool Selectable { get; set; }

        /// <summary>
        /// Whether the feat requires an action to use (from feat.2da "requiresaction" column).
        /// </summary>
        public bool RequiresAction { get; set; }

        /// <summary>
        /// Uses per day limit for the feat (from feat.2da "usesperday" column).
        /// -1 = unlimited uses or special handling (e.g., based on class levels)
        /// 0+ = maximum uses per day
        /// </summary>
        public int UsesPerDay { get; set; }
    }
}


using System.Collections.Generic;
using Andastra.Runtime.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Aurora.Components
{
    /// <summary>
    /// Component for creature entities in Aurora engine (Neverwinter Nights).
    /// </summary>
    /// <remarks>
    /// Aurora Creature Component:
    /// - Based on nwmain.exe creature system (CNWSCreatureStats)
    /// - Located via string references: "Creature" @ various addresses, "FeatList" in creature data
    /// - Original implementation: CNWSCreatureStats::HasFeat @ 0x14040b900 (nwmain.exe)
    /// - Feat checking: HasFeat checks two feat lists (primary feat list at offset 0, bonus feat list at offset 0x20)
    /// - Special handling: Feats 41 (0x29, Armor Proficiency) and 1 (Shield Proficiency) have special armor checks
    /// - Feat storage: Two separate lists - normal feats and bonus feats (CExoArrayList&lt;unsigned_short&gt;)
    /// - Inheritance: BaseCreatureComponent (Runtime.Games.Common.Components) contains common functionality
    ///   - Aurora: AuroraCreatureComponent : BaseCreatureComponent (Runtime.Games.Aurora) - two feat lists
    ///   - Odyssey: CreatureComponent : BaseCreatureComponent (Runtime.Games.Odyssey) - single feat list + force powers
    /// </remarks>
    public class AuroraCreatureComponent : BaseCreatureComponent
    {
        public AuroraCreatureComponent()
            : base()
        {
            FeatList = new List<int>();
            BonusFeatList = new List<int>();
            // ClassList is initialized in base class, but we need to ensure it's the right type
            // For now, base class uses BaseCreatureClass which is compatible
        }


        #region Feats

        /// <summary>
        /// Primary feat list (normal feats acquired through leveling).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Primary feat list is at offset 0 in CNWSCreatureStats structure
        /// CExoArrayList&lt;unsigned_short&gt; containing normal feats
        /// </remarks>
        public List<int> FeatList { get; set; }

        /// <summary>
        /// Bonus feat list (feats granted by class, race, or other sources).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Bonus feat list is at offset 0x20 in CNWSCreatureStats structure
        /// CExoArrayList&lt;unsigned_short&gt; containing bonus feats
        /// </remarks>
        public List<int> BonusFeatList { get; set; }

        /// <summary>
        /// Checks if creature has a feat (Aurora-specific implementation).
        /// </summary>
        /// <param name="featId">The feat ID to check for.</param>
        /// <returns>True if the creature has the feat, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Original implementation:
        /// 1. Checks primary feat list (CExoArrayList at offset 0)
        /// 2. If not found, checks bonus feat list (CExoArrayList at offset 0x20)
        /// 3. Special handling for feats 41 (0x29, Armor Proficiency) and 1 (Shield Proficiency):
        ///    - Checks if creature has appropriate class that grants the proficiency
        ///    - For armor proficiency: Checks if equipped armor AC is <= 3 (light armor)
        ///    - For shield proficiency: Checks if creature has shield equipped
        ///
        /// Current implementation: Simplified version that checks both feat lists
        /// Full implementation would include special armor/shield proficiency checks
        /// </remarks>
        public override bool HasFeat(int featId)
        {
            // Check primary feat list (nwmain.exe: offset 0)
            if (FeatList != null && FeatList.Contains(featId))
            {
                return true;
            }

            // Check bonus feat list (nwmain.exe: offset 0x20)
            if (BonusFeatList != null && BonusFeatList.Contains(featId))
            {
                return true;
            }

            // Special handling for armor/shield proficiency feats (feats 41 and 1)
            // Based on nwmain.exe: Special checks for these feats involve class checks and armor AC
            // TODO: Implement full armor/shield proficiency checks when class system is complete
            // For now, return false if not in either list
            if (featId == 41 || featId == 1)
            {
                // Full implementation would:
                // 1. Check if creature has class that grants the proficiency
                // 2. For armor (41): Check if equipped armor AC <= 3
                // 3. For shield (1): Check if shield is equipped
                // This requires class system and inventory system to be fully implemented
            }

            return false;
        }

        /// <summary>
        /// Gets feat data from game data provider (Aurora-specific implementation).
        /// </summary>
        /// <param name="featId">The feat ID to look up.</param>
        /// <param name="gameDataProvider">GameDataProvider instance (AuroraGameDataProvider or AuroraTwoDATableManager).</param>
        /// <returns>IFeatData if found, null otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: Feat data lookup from feat.2da
        /// - Located via string references: Feat data in 2DA tables
        /// - Original implementation: Aurora uses C2DA class to access feat.2da table
        /// - C2DA::Load2DArray @ 0x1401a73a0 loads feat.2da, C2DA::GetInteger/C2DA::GetString retrieve feat properties
        /// - Supports both AuroraGameDataProvider and AuroraTwoDATableManager for flexibility
        /// - Returns AuroraFeatData which implements IFeatData interface for daily usage tracking
        /// </remarks>
        protected override IFeatData GetFeatData(int featId, object gameDataProvider)
        {
            if (gameDataProvider == null)
            {
                return null;
            }

            // Try AuroraGameDataProvider first (preferred interface)
            Data.AuroraGameDataProvider auroraProvider = gameDataProvider as Data.AuroraGameDataProvider;
            if (auroraProvider != null)
            {
                return auroraProvider.GetFeat(featId);
            }

            // Try AuroraTwoDATableManager (direct table access)
            Data.AuroraTwoDATableManager tableManager = gameDataProvider as Data.AuroraTwoDATableManager;
            if (tableManager != null)
            {
                // Use table manager to get feat data directly
                // Based on nwmain.exe: C2DA::Load2DArray loads feat.2da table
                Parsing.Formats.TwoDA.TwoDA table = tableManager.GetTable("feat");
                if (table == null || featId < 0 || featId >= table.GetHeight())
                {
                    return null;
                }

                // Get row by feat ID (row index)
                // Based on nwmain.exe: C2DA row access via index
                Parsing.Formats.TwoDA.TwoDARow row = table.GetRow(featId);
                if (row == null)
                {
                    return null;
                }

                // Extract feat data from 2DA row
                // Based on nwmain.exe: C2DA::GetInteger, C2DA::GetString access feat.2da columns
                int? usesPerDay = SafeGetInteger(row, "usesperday");
                return new Data.AuroraFeatData
                {
                    RowIndex = featId,
                    Label = row.Label(),
                    Name = SafeGetString(row, "name") ?? string.Empty,
                    Description = SafeGetString(row, "description") ?? string.Empty,
                    DescriptionStrRef = SafeGetInteger(row, "description") ?? -1,
                    Icon = SafeGetString(row, "icon") ?? string.Empty,
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

            // Unknown game data provider type
            return null;
        }

        /// <summary>
        /// Safely gets a string value from a 2DA row, returning null if the column doesn't exist.
        /// </summary>
        /// <param name="row">The 2DA row.</param>
        /// <param name="columnName">The column name.</param>
        /// <returns>The string value, or null if the column doesn't exist.</returns>
        [CanBeNull]
        private static string SafeGetString(Parsing.Formats.TwoDA.TwoDARow row, string columnName)
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
        private static int? SafeGetInteger(Parsing.Formats.TwoDA.TwoDARow row, string columnName)
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

        /// <summary>
        /// Gets the list of all feat IDs for this creature (for daily use reset).
        /// </summary>
        /// <returns>Enumerable of feat IDs.</returns>
        private System.Collections.Generic.IEnumerable<int> GetAllFeatIds()
        {
            System.Collections.Generic.List<int> allFeats = new System.Collections.Generic.List<int>();
            if (FeatList != null)
            {
                allFeats.AddRange(FeatList);
            }
            if (BonusFeatList != null)
            {
                allFeats.AddRange(BonusFeatList);
            }
            return allFeats;
        }

        /// <summary>
        /// Resets daily feat uses (Aurora-specific override).
        /// </summary>
        /// <param name="gameDataProvider">GameDataProvider to look up feat data.</param>
        /// <remarks>
        /// Based on nwmain.exe: Daily feat use reset on rest
        /// Uses Aurora-specific feat list (both FeatList and BonusFeatList)
        /// </remarks>
        public void ResetDailyFeatUses(object gameDataProvider)
        {
            ResetDailyFeatUses(gameDataProvider, GetAllFeatIds);
        }

        #endregion
    }
}


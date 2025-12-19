using System.Collections.Generic;
using Andastra.Runtime.Games.Common.Components;

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
        /// Located via string references: Feat data in 2DA tables
        /// Original implementation: Aurora uses C2DA class to access feat.2da table
        /// TODO: Implement full Aurora FeatData class and lookup when Aurora data structures are complete
        /// For now, returns null which makes feats always usable if creature has them (acceptable fallback)
        /// </remarks>
        protected override IFeatData GetFeatData(int featId, object gameDataProvider)
        {
            // TODO: Implement Aurora FeatData lookup from AuroraTwoDATableManager
            // For now, return null which will make feats always usable if creature has them
            // This is acceptable as a fallback until Aurora data structures are fully implemented
            return null;
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


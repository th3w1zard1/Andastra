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

        #endregion
    }
}


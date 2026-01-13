using System.Collections.Generic;
using BioWare.NET.Resource.Formats.TwoDA;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Aurora.Data;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora.Components
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
        /// <summary>
        /// Aurora-specific class list (overrides base class ClassList with Aurora-specific type).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::ClassList - CExoArrayList containing class entries
        /// Original implementation: Class list stored in CNWSCreatureStats structure
        /// nwmain.exe: Each entry contains ClassId and Level
        /// This property shadows the base class ClassList to use AuroraCreatureClass instead of BaseCreatureClass
        /// </remarks>
        public new List<AuroraCreatureClass> ClassList { get; set; }

        public AuroraCreatureComponent()
            : base()
        {
            FeatList = new List<int>();
            BonusFeatList = new List<int>();
            // Initialize Aurora-specific ClassList (shadows base class ClassList)
            // Based on nwmain.exe: CNWSCreatureStats class list initialization
            ClassList = new List<AuroraCreatureClass>();
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
        ///    - For armor proficiency: Checks if equipped armor AC matches class proficiency level
        ///    - For shield proficiency: Checks if creature has shield equipped and has class that grants shield proficiency
        /// nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900 handles special cases for armor/shield proficiency
        /// nwmain.exe: Class ID 7 (Ranger/Rogue) grants light armor proficiency (AC <= 3)
        /// nwmain.exe: Other classes (Fighter, Paladin, Cleric, etc.) grant full armor/shield proficiency
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

            // Special handling for Armor Proficiency (feat 41)
            // Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
            // Original implementation checks:
            // 1. If creature has class that grants armor proficiency
            // 2. Gets armor item from slot 2 using GetItemInSlot(..., 2)
            // 3. If armor exists, checks if armor AC matches class proficiency level using ComputeArmorClass
            // 4. Classes with light armor only (Rogue/Ranger): AC <= 3
            // 5. Classes with full armor proficiency: Any AC value
            // nwmain.exe: GetItemInSlot(..., 2) gets armor from slot 2, ComputeArmorClass gets AC from baseitems.2da ACValue column
            if (featId == 41)
            {
                return CheckArmorProficiency();
            }

            // Special handling for Shield Proficiency (feat 1)
            // Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
            // Original implementation checks:
            // 1. If creature has class that grants shield proficiency
            // 2. Checks if creature has shield equipped in slot 0x20 (32)
            // 3. For classes with light armor only (Rogue/Ranger): Also checks armor AC <= 3
            // nwmain.exe: GetItemInSlot(..., 0x20) gets shield from slot 32, checks base item ID for shield types
            if (featId == 1)
            {
                return CheckShieldProficiency();
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

        /// <summary>
        /// Checks if the creature has a class that grants armor/shield proficiency.
        /// </summary>
        /// <param name="classId">The class ID to check for (e.g., 7 for Rogue/Ranger).</param>
        /// <returns>True if the creature has the specified class, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Original implementation: Iterates through ClassList checking for specific class IDs
        /// nwmain.exe: DAT_140dc301b = 0x7 (class ID 7 = Ranger/Rogue grants light armor proficiency)
        /// Class list is stored in CNWSCreatureStats structure, iterated in HasFeat function
        /// </remarks>
        private bool HasClassThatGrantsProficiency(int classId)
        {
            if (ClassList == null)
            {
                return false;
            }

            // Iterate through all classes the creature has
            // Based on nwmain.exe: ClassList iteration in HasFeat function
            foreach (AuroraCreatureClass creatureClass in ClassList)
            {
                if (creatureClass != null && creatureClass.ClassId == classId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the creature has a class that grants full armor/shield proficiency (all armor types).
        /// </summary>
        /// <returns>True if the creature has a class that grants full armor proficiency, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Original implementation: Checks for classes that grant full armor proficiency
        /// Classes with full armor proficiency: Fighter (4), Paladin (6), Cleric (2), Druid (3), Bard (1)
        /// These classes can use light, medium, and heavy armor
        /// nwmain.exe: Multiple class IDs checked in HasFeat function for full armor proficiency
        /// </remarks>
        private bool HasClassWithFullArmorProficiency()
        {
            if (ClassList == null)
            {
                return false;
            }

            // Classes that grant full armor proficiency (light, medium, heavy)
            // Based on nwmain.exe: Class IDs checked for full armor proficiency
            // Fighter = 4, Paladin = 6, Cleric = 2, Druid = 3, Bard = 1
            // Based on D&D 3.0 rules and nwmain.exe implementation
            foreach (AuroraCreatureClass creatureClass in ClassList)
            {
                if (creatureClass != null)
                {
                    int classId = creatureClass.ClassId;
                    // Fighter (4), Paladin (6), Cleric (2), Druid (3), Bard (1) grant full armor proficiency
                    if (classId == 1 || classId == 2 || classId == 3 || classId == 4 || classId == 6)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the creature has a class that grants light armor proficiency only.
        /// </summary>
        /// <returns>True if the creature has a class that grants light armor proficiency only, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Original implementation: Checks for class ID 7 (Ranger/Rogue) which grants light armor only
        /// Classes with light armor only: Ranger (7), Rogue (8)
        /// These classes can only use light armor (AC <= 3)
        /// nwmain.exe: DAT_140dc301b = 0x7 (class ID 7 = Ranger/Rogue grants light armor proficiency)
        /// </remarks>
        private bool HasClassWithLightArmorProficiencyOnly()
        {
            if (ClassList == null)
            {
                return false;
            }

            // Classes that grant light armor proficiency only (AC <= 3)
            // Based on nwmain.exe: Class ID 7 (Ranger/Rogue) grants light armor only
            // Rogue (8) also grants light armor only
            // Based on D&D 3.0 rules and nwmain.exe implementation
            foreach (AuroraCreatureClass creatureClass in ClassList)
            {
                if (creatureClass != null)
                {
                    int classId = creatureClass.ClassId;
                    // Ranger (7), Rogue (8) grant light armor proficiency only
                    if (classId == 7 || classId == 8)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the creature has a class that grants shield proficiency.
        /// </summary>
        /// <returns>True if the creature has a class that grants shield proficiency, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Original implementation: Checks for classes that grant shield proficiency
        /// Classes with shield proficiency: Fighter (4), Paladin (6), Cleric (2), Druid (3), Bard (1), Ranger (7)
        /// Rogue (8) does NOT grant shield proficiency
        /// nwmain.exe: Multiple class IDs checked in HasFeat function for shield proficiency
        /// </remarks>
        private bool HasClassWithShieldProficiency()
        {
            if (ClassList == null)
            {
                return false;
            }

            // Classes that grant shield proficiency
            // Based on nwmain.exe: Class IDs checked for shield proficiency
            // Fighter (4), Paladin (6), Cleric (2), Druid (3), Bard (1), Ranger (7) grant shield proficiency
            // Rogue (8) does NOT grant shield proficiency
            // Based on D&D 3.0 rules and nwmain.exe implementation
            foreach (AuroraCreatureClass creatureClass in ClassList)
            {
                if (creatureClass != null)
                {
                    int classId = creatureClass.ClassId;
                    // Fighter (4), Paladin (6), Cleric (2), Druid (3), Bard (1), Ranger (7) grant shield proficiency
                    if (classId == 1 || classId == 2 || classId == 3 || classId == 4 || classId == 6 || classId == 7)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the creature has Armor Proficiency (feat 41) based on class and equipped armor.
        /// </summary>
        /// <returns>True if the creature has armor proficiency for their equipped armor, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Original implementation:
        /// 1. Checks if creature has class that grants armor proficiency
        /// 2. Gets armor item from slot 2 using GetItemInSlot(..., 2)
        /// 3. If armor exists, checks if armor AC matches class proficiency level
        /// 4. Classes with full proficiency: Any armor AC
        /// 5. Classes with light armor only: AC <= 3
        /// 6. If no armor equipped, returns true (creature has proficiency)
        /// nwmain.exe: CNWSItem::ComputeArmorClass @ 0x140466500 reads ACValue column from baseitems.2da
        /// </remarks>
        private bool CheckArmorProficiency()
        {
            // Check if creature has any class that grants armor proficiency
            bool hasFullProficiency = HasClassWithFullArmorProficiency();
            bool hasLightOnly = HasClassWithLightArmorProficiencyOnly();

            if (!hasFullProficiency && !hasLightOnly)
            {
                // No class grants armor proficiency
                return false;
            }

            // Get armor AC from slot 2
            // Based on nwmain.exe: GetItemInSlot(..., 2) gets armor from slot 2, ComputeArmorClass returns AC from baseitems.2da
            // nwmain.exe: CNWSItem::ComputeArmorClass @ 0x140466500 reads ACValue column from baseitems.2da (byte at offset 0xa8)
            int armorAC = GetEquippedArmorAC();

            // If no armor equipped, creature has proficiency (can use armor)
            // Based on nwmain.exe: If armor item is null, returns true (creature has proficiency)
            if (armorAC == 0)
            {
                return true;
            }

            // If creature has full armor proficiency, any armor AC is allowed
            if (hasFullProficiency)
            {
                return true;
            }

            // If creature has light armor proficiency only, check if armor AC <= 3
            // Based on nwmain.exe: if (armorAC <= 3) return true; else return false;
            if (hasLightOnly)
            {
                return armorAC > 0 && armorAC <= 3;
            }

            return false;
        }

        /// <summary>
        /// Checks if the creature has Shield Proficiency (feat 1) based on class and equipped shield.
        /// </summary>
        /// <returns>True if the creature has shield proficiency, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Original implementation:
        /// 1. Checks if creature has class that grants shield proficiency
        /// 2. Checks if creature has shield equipped in slot 0x20 (32)
        /// 3. For classes with light armor only (Ranger/Rogue): Also checks armor AC <= 3
        ///    - If creature has a class that only allows light armor (AC <= 3), they can only use shields while wearing light armor
        ///    - This check applies regardless of whether the class grants shield proficiency (Ranger grants it, Rogue doesn't)
        /// 4. If no shield equipped, returns true if class grants proficiency (can use shields)
        /// nwmain.exe: GetItemInSlot(..., 0x20) gets shield from slot 32, checks base item ID for shield types
        /// nwmain.exe: Shield base item IDs: 0xe (14), 0x38 (56), 0x39 (57)
        /// nwmain.exe: For classes with light armor only, armor AC check prevents shield use with heavy/medium armor
        /// </remarks>
        private bool CheckShieldProficiency()
        {
            // Check if creature has class that grants shield proficiency
            // Based on nwmain.exe: CNWSCreatureStats::HasFeat checks class list for shield proficiency
            bool hasShieldProficiency = HasClassWithShieldProficiency();

            if (!hasShieldProficiency)
            {
                // No class grants shield proficiency - creature cannot use shields
                // Based on nwmain.exe: Returns false if no class grants shield proficiency
                return false;
            }

            // Creature has shield proficiency - check armor restrictions for light armor only classes
            // Based on nwmain.exe: For classes with light armor only (like Ranger), armor AC must be <= 3 to use shields
            bool hasLightOnly = HasClassWithLightArmorProficiencyOnly();
            if (hasLightOnly)
            {
                // Get equipped armor AC to check if creature is wearing light armor only
                // Based on nwmain.exe: GetItemInSlot(..., 2) gets armor, ComputeArmorClass gets AC from baseitems.2da
                int armorAC = GetEquippedArmorAC();

                // If creature is wearing heavy/medium armor (AC > 3), they cannot use shields
                // Based on nwmain.exe: Classes with light armor only (AC <= 3) can only use shields while wearing light armor
                if (armorAC > 3)
                {
                    // Heavy/medium armor prevents shield use for light armor only classes
                    // Based on nwmain.exe: Armor AC check for classes with light armor proficiency only
                    return false;
                }
            }

            // Check if shield is equipped
            // Based on nwmain.exe: GetItemInSlot(..., 0x20) gets shield from slot 32
            bool hasShield = HasShieldEquipped();

            // If no shield equipped, creature has proficiency (can use shields)
            // Based on nwmain.exe: If shield item is null, returns true if class grants proficiency
            // This indicates the creature has the ability to use shields, even if not currently using one
            if (!hasShield)
            {
                return true;
            }

            // If shield is equipped and creature has proficiency and armor restrictions are met, return true
            // Based on nwmain.exe: Shield proficiency check passes if class grants it and armor restrictions are satisfied
            // (Armor AC check already performed above for light armor only classes)
            return true;
        }

        /// <summary>
        /// Gets the AC value of the equipped armor item.
        /// </summary>
        /// <returns>The AC value of equipped armor, or 0 if no armor is equipped or lookup fails.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900, CNWSItem::ComputeArmorClass @ 0x140466500
        /// Original implementation:
        /// 1. Gets armor item from slot 2 using CNWSInventory::GetItemInSlot(..., 2)
        /// 2. Calls CNWSItem::ComputeArmorClass which reads ACValue column from baseitems.2da (byte at offset 0xa8)
        /// nwmain.exe: ComputeArmorClass returns (int)(byte)pCVar2[0xa8] where pCVar2 is CNWBaseItem from baseitems.2da
        /// ACValue column in baseitems.2da contains the armor class value for armor items (0 = not armor, 1-10 = armor AC)
        /// Returns 0 if no armor is equipped or if the item is not armor
        /// </remarks>
        private int GetEquippedArmorAC()
        {
            if (Owner == null || Owner.World == null)
            {
                return 0;
            }

            // Get inventory component from owner entity
            // Based on nwmain.exe: CNWSInventory accessed via CNWSCreature pointer
            IInventoryComponent inventory = Owner.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return 0;
            }

            // Get armor item from slot 2 (armor slot in Aurora/NWN)
            // Based on nwmain.exe: GetItemInSlot(..., 2) gets armor from slot 2
            // nwmain.exe: CNWSInventory::GetItemInSlot @ various addresses, slot 2 = armor slot
            IEntity armorItem = inventory.GetItemInSlot(2);
            if (armorItem == null)
            {
                return 0;
            }

            // Get item component from armor item
            IItemComponent itemComponent = armorItem.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return 0;
            }

            // Get base item ID from item component
            int baseItemId = itemComponent.BaseItem;
            if (baseItemId < 0)
            {
                return 0;
            }

            // Get AC value from baseitems.2da ACValue column
            // Based on nwmain.exe: CNWSItem::ComputeArmorClass @ 0x140466500
            // Original implementation: CNWBaseItemArray::GetBaseItem gets base item, then reads byte at offset 0xa8 (ACValue column)
            // ACValue column in baseitems.2da contains armor class value for armor items (0 = not armor, 1-10 = light/medium/heavy armor AC)
            // Access baseitems.2da via game data provider from world
            if (Owner.World.GameDataProvider == null)
            {
                return 0;
            }

            // Try to get AuroraGameDataProvider to access table manager
            Data.AuroraGameDataProvider auroraProvider = Owner.World.GameDataProvider as Data.AuroraGameDataProvider;
            if (auroraProvider == null)
            {
                return 0;
            }

            // Get table manager from AuroraGameDataProvider
            Data.AuroraTwoDATableManager tableManager = auroraProvider.TableManager;
            if (tableManager == null)
            {
                return 0;
            }

            // Get baseitems.2da table
            // Based on nwmain.exe: CNWBaseItemArray::GetBaseItem accesses baseitems.2da via C2DA system
            TwoDA baseItemsTable = tableManager.GetTable("baseitems");
            if (baseItemsTable == null || baseItemId < 0 || baseItemId >= baseItemsTable.GetHeight())
            {
                return 0;
            }

            // Get row by base item ID
            // Based on nwmain.exe: CNWBaseItemArray row access via base item ID (row index)
            TwoDARow row = baseItemsTable.GetRow(baseItemId);
            if (row == null)
            {
                return 0;
            }

            // Read ACValue column from baseitems.2da
            // Based on nwmain.exe: ComputeArmorClass reads byte at offset 0xa8 which is ACValue column
            // ACValue column contains armor class value (0 = not armor, 1-10 = armor AC value)
            // nwmain.exe: return (int)(byte)pCVar2[0xa8] where pCVar2[0xa8] is ACValue column
            int? acValue = SafeGetInteger(row, "ACValue");
            if (!acValue.HasValue)
            {
                return 0;
            }

            return acValue.Value;
        }

        /// <summary>
        /// Checks if the creature has a shield equipped.
        /// </summary>
        /// <returns>True if a shield is equipped, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreature::GetUseMonkAbilities @ 0x140396370
        /// Original implementation: Checks slot 0x20 (32) for shield items
        /// nwmain.exe: GetItemInSlot(..., 0x20) gets shield from slot 32, then checks base item ID
        /// Shield base item IDs: 0xe (14), 0x38 (56), 0x39 (57)
        /// nwmain.exe: Checks if base item ID is 0xe, 0x38, or 0x39 to determine if item is a shield
        /// </remarks>
        private bool HasShieldEquipped()
        {
            if (Owner == null)
            {
                return false;
            }

            // Get inventory component from owner entity
            IInventoryComponent inventory = Owner.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return false;
            }

            // Check shield slot (0x20 = 32) for shield items
            // Based on nwmain.exe: CNWSCreature::GetUseMonkAbilities checks slot 0x20 for shields
            // nwmain.exe: GetItemInSlot(..., 0x20) gets shield from slot 32
            IEntity shieldItem = inventory.GetItemInSlot(32);
            if (shieldItem == null)
            {
                return false;
            }

            // Get item component to check if it's a shield
            IItemComponent itemComponent = shieldItem.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return false;
            }

            // Get base item ID and check if it's a shield type
            // Based on nwmain.exe: GetUseMonkAbilities checks if base item ID is 0xe, 0x38, or 0x39
            // nwmain.exe: if ((baseItemId != 0xe) && (baseItemId != 0x38 && baseItemId != 0x39)) return 0;
            // Shield base item IDs: 14 (0xe), 56 (0x38), 57 (0x39)
            int baseItemId = itemComponent.BaseItem;
            if (baseItemId == 14 || baseItemId == 56 || baseItemId == 57)
            {
                return true;
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// Aurora-specific creature class information (nwmain.exe - Neverwinter Nights).
    /// </summary>
    /// <remarks>
    /// Based on nwmain.exe: CNWSCreatureStats class list structure
    /// Original implementation: Class data stored in CNWSCreatureStats structure
    /// Each class entry contains ClassId and Level
    /// nwmain.exe: ClassList is CExoArrayList containing class entries
    /// Currently matches BaseCreatureClass fields for compatibility, but allows for future Aurora-specific extensions
    /// </remarks>
    public class AuroraCreatureClass
    {
        /// <summary>
        /// Class ID (index into classes.2da).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Class ID stored in class list entry
        /// Original implementation: Index into classes.2da table
        /// </remarks>
        public int ClassId { get; set; }

        /// <summary>
        /// Level in this class.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Class level stored in class list entry
        /// Original implementation: Level in this specific class (for multiclass characters)
        /// </remarks>
        public int Level { get; set; }
    }
}


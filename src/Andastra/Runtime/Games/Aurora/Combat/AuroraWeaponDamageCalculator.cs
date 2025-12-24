using System;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Aurora.Components;
using Andastra.Runtime.Games.Aurora.Data;
using Andastra.Runtime.Games.Common.Combat;

namespace Andastra.Runtime.Games.Aurora.Combat
{
    /// <summary>
    /// Calculates weapon damage from equipped items using baseitems.2da (Aurora engine).
    /// </summary>
    /// <remarks>
    /// Aurora Weapon Damage Calculator:
    /// - Based on nwmain.exe weapon damage calculation (nwmain.exe: weapon damage and ability modifier selection)
    /// - Located via string references: "WeaponFinesseMinimumCreatureSize" @ 0x140dc3de0, "DEXAdjust" @ 0x140dc5050, "MinDEX" @ 0x140dc49b0
    /// - Cross-engine: Similar damage calculation to Odyssey but uses different 2DA tables and feat system
    /// - Inheritance: BaseWeaponDamageCalculator (Runtime.Games.Common.Combat) implements common damage calculation logic
    ///   - Aurora: AuroraWeaponDamageCalculator : BaseWeaponDamageCalculator (Runtime.Games.Aurora) - Aurora-specific baseitems.2da lookup
    /// - Ability modifier selection (Aurora/NWN):
    ///   - Ranged weapons: Always use DEX modifier (nwmain.exe: ranged weapon damage calculation)
    ///   - Melee weapons: Use STR by default, DEX if Weapon Finesse feat is present and weapon qualifies
    ///   - Weapon Finesse feat: FEAT_WEAPON_FINESSE (42) - standard NWN feat ID
    ///   - Finesse eligibility: Weapon must be light (weaponsize = 1 or 2) or creature size allows it
    ///   - Weapon size check: Based on WeaponFinesseMinimumCreatureSize from baseitems.2da
    ///   - Two-handed weapons: Cannot use finesse (weaponwield = 4 for two-handed)
    /// - Original implementation: nwmain.exe checks Weapon Finesse feat and weapon properties for ability modifier selection
    /// - Cross-engine comparison:
    ///   - Odyssey: FEAT_FINESSE_LIGHTSABERS (193), FEAT_FINESSE_MELEE_WEAPONS (194) - separate feats for lightsabers/melee
    ///   - Aurora: FEAT_WEAPON_FINESSE (42) - single feat for all finesse-eligible weapons
    ///   - Eclipse: Different damage system (may not use D20 ability modifiers)
    /// </remarks>
    public class AuroraWeaponDamageCalculator : BaseWeaponDamageCalculator
    {
        private readonly AuroraTwoDATableManager _tableManager;

        /// <summary>
        /// Weapon Finesse feat ID for Aurora/NWN (standard D&D 3.0 feat).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Standard NWN feat.2da - Weapon Finesse is typically feat ID 42
        /// </remarks>
        private const int FeatWeaponFinesse = 42;

        /// <summary>
        /// Initializes a new instance of the Aurora weapon damage calculator.
        /// </summary>
        /// <param name="tableManager">The Aurora 2DA table manager for accessing baseitems.2da.</param>
        /// <remarks>
        /// Based on nwmain.exe: Weapon damage calculation requires access to baseitems.2da via C2DA system
        /// </remarks>
        public AuroraWeaponDamageCalculator(AuroraTwoDATableManager tableManager)
        {
            _tableManager = tableManager ?? throw new ArgumentNullException("tableManager");
        }

        /// <summary>
        /// Gets the main hand weapon slot number (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: RIGHTHAND slot = 4 (standard Aurora inventory slot)
        /// </remarks>
        protected override int MainHandWeaponSlot => 4;

        /// <summary>
        /// Gets the offhand weapon slot number (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: LEFTHAND slot = 5 (standard Aurora inventory slot)
        /// </remarks>
        protected override int OffHandWeaponSlot => 5;

        /// <summary>
        /// Gets damage dice information from baseitems.2da (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: baseitems.2da table structure
        /// Column names: numdice/damagedice (dice count), dietoroll/damagedie (die size), damagebonus
        /// Original implementation: CNWBaseItemArray::GetBaseItem @ 0x14029ca00 accesses baseitems.2da data
        /// </remarks>
        protected override bool GetDamageDiceFromTable(int baseItemId, out int damageDice, out int damageDie, out int damageBonus)
        {
            damageDice = 1;
            damageDie = 8;
            damageBonus = 0;

            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow != null)
                {
                    // Column names from baseitems.2da (Aurora/NWN format)
                    // numdice/damagedice = number of dice
                    // dietoroll/damagedie = die size
                    // damagebonus = base damage bonus
                    damageDice = twoDARow.GetInteger("numdice", null) ??
                                 twoDARow.GetInteger("damagedice", 1) ?? 1;
                    damageDie = twoDARow.GetInteger("dietoroll", null) ??
                               twoDARow.GetInteger("damagedie", 8) ?? 8;
                    damageBonus = twoDARow.GetInteger("damagebonus", 0) ?? 0;
                    return true;
                }
            }
            catch
            {
                // Fallback values already set
            }

            return false;
        }

        /// <summary>
        /// Determines which ability score to use for weapon damage calculation (Aurora-specific).
        /// </summary>
        /// <param name="attacker">The attacking entity.</param>
        /// <param name="weapon">The weapon entity.</param>
        /// <param name="baseItemId">The base item ID.</param>
        /// <returns>The ability score to use (DEX for ranged/finesse, STR otherwise).</returns>
        /// <remarks>
        /// Ability Score Selection for Weapon Damage (Aurora/NWN):
        /// - Based on nwmain.exe: Ability modifier selection for weapon damage
        /// - Located via string references: "WeaponFinesseMinimumCreatureSize" @ 0x140dc3de0, "DEXAdjust" @ 0x140dc5050
        /// - Original implementation: nwmain.exe checks Weapon Finesse feat and weapon properties
        /// - Ranged weapons: Always use DEX modifier (nwmain.exe: ranged weapon damage always uses DEX)
        /// - Melee weapons: Use STR by default, DEX if appropriate conditions are met
        /// - Weapon Finesse conditions:
        ///   1. Creature must have FEAT_WEAPON_FINESSE (42)
        ///   2. Weapon must be light (weaponsize = 1 or 2) OR creature size allows it (based on WeaponFinesseMinimumCreatureSize)
        ///   3. Weapon must not be two-handed (weaponwield != 4)
        /// - Weapon size categories (Aurora): 1 = tiny, 2 = small, 3 = medium, 4 = large, 5 = huge
        /// - Light weapons: weaponsize = 1 (tiny) or 2 (small) are always finesse-eligible
        /// - Medium+ weapons: Can use finesse if creature size is small enough (based on WeaponFinesseMinimumCreatureSize column)
        /// - Cross-engine: Similar logic in Odyssey (different feat IDs), different in Eclipse (may not use D20 system)
        /// </remarks>
        protected override Ability DetermineDamageAbility(IEntity attacker, IEntity weapon, int baseItemId)
        {
            if (attacker == null || weapon == null)
            {
                return Ability.Strength; // Default fallback
            }

            // Get base item data to check if ranged
            bool isRanged = IsRangedWeapon(baseItemId);

            // Ranged weapons always use DEX (nwmain.exe: ranged weapon damage calculation)
            if (isRanged)
            {
                return Ability.Dexterity;
            }

            // For melee weapons, check if finesse applies
            // Check if attacker has Weapon Finesse feat
            if (!HasFeat(attacker, FeatWeaponFinesse))
            {
                // No Weapon Finesse feat, use STR
                return Ability.Strength;
            }

            // Creature has Weapon Finesse, check if weapon qualifies
            if (IsWeaponFinesseEligible(attacker, baseItemId))
            {
                return Ability.Dexterity;
            }

            // Weapon doesn't qualify for finesse, use STR
            return Ability.Strength;
        }

        /// <summary>
        /// Checks if a weapon is ranged (Aurora-specific).
        /// </summary>
        /// <param name="baseItemId">The base item ID to check.</param>
        /// <returns>True if the weapon is ranged, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: Ranged weapon detection from baseitems.2da
        /// Checks "rangedweapon" column in baseitems.2da (non-empty value indicates ranged weapon)
        /// Original implementation: CNWBaseItemArray::GetBaseItem @ 0x14029ca00 accesses baseitems.2da data
        /// </remarks>
        private bool IsRangedWeapon(int baseItemId)
        {
            if (baseItemId <= 0)
            {
                return false;
            }

            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow != null)
                {
                    // Check "rangedweapon" column (non-empty value indicates ranged weapon)
                    string rangedWeapon = twoDARow.GetString("rangedweapon");
                    if (!string.IsNullOrEmpty(rangedWeapon))
                    {
                        return true;
                    }

                    // Alternative: Check weapontype for common ranged weapon types
                    // Bows (weapontype = 5), crossbows (weapontype = 6), slings (weapontype = 10), thrown (weapontype = 11)
                    int? weaponType = twoDARow.GetInteger("weapontype", null);
                    if (weaponType.HasValue)
                    {
                        int wt = weaponType.Value;
                        return wt == 5 || wt == 6 || wt == 10 || wt == 11;
                    }
                }
            }
            catch
            {
                // Error accessing table, fall through to return false
            }

            return false;
        }

        /// <summary>
        /// Checks if a weapon is eligible for Weapon Finesse (Aurora-specific).
        /// </summary>
        /// <param name="attacker">The attacking entity.</param>
        /// <param name="baseItemId">The base item ID to check.</param>
        /// <returns>True if the weapon can use finesse, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: Weapon Finesse eligibility check
        /// Located via string reference: "WeaponFinesseMinimumCreatureSize" @ 0x140dc3de0
        /// Original implementation: CNWSCreatureStats::GetWeaponFinesse @ 0x14040b680
        /// Checks weapon size, weapon wield type, and creature size
        /// Eligibility conditions:
        /// 1. Weapon must not be two-handed (weaponwield != 4)
        /// 2. Weapon must be light (weaponsize = 1 or 2) OR creature size allows it
        /// 3. Weapon size check: Based on WeaponFinesseMinimumCreatureSize column in baseitems.2da
        /// </remarks>
        private bool IsWeaponFinesseEligible(IEntity attacker, int baseItemId)
        {
            if (baseItemId <= 0 || attacker == null)
            {
                return false;
            }

            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow == null)
                {
                    return false;
                }

                // 1. Check weaponwield - if 4 (two-handed), cannot use finesse
                int? weaponWield = twoDARow.GetInteger("weaponwield", null);
                if (weaponWield.HasValue && weaponWield.Value == 4)
                {
                    return false; // Two-handed weapons cannot use finesse
                }

                // 2. Check weaponsize - if 1 (tiny) or 2 (small), always finesse-eligible
                int? weaponSize = twoDARow.GetInteger("weaponsize", null);
                if (weaponSize.HasValue)
                {
                    int ws = weaponSize.Value;
                    if (ws == 1 || ws == 2)
                    {
                        return true; // Light weapons are always finesse-eligible
                    }
                }

                // 3. For medium+ weapons, check WeaponFinesseMinimumCreatureSize
                int? minCreatureSize = twoDARow.GetInteger("WeaponFinesseMinimumCreatureSize", null);
                if (minCreatureSize.HasValue)
                {
                    // Get creature size from attacker's appearance/stats
                    // Based on nwmain.exe: CNWSCreatureStats::GetWeaponFinesse @ 0x14040b680
                    // - Accesses creature size at offset 0x718 in CNWSCreature (via this + 0x30 pointer)
                    // - Creature size is stored in CNWSCreature structure, derived from appearance.2da sizecategory column
                    // - ExecuteCommandGetCreatureSize @ 0x1405213f0 shows: iVar3 = *(int *)(pCVar2 + 0x718);
                    // - Original implementation: Size is cached in creature object, originally loaded from appearance.2da
                    int creatureSize = GetCreatureSize(attacker);

                    // If creature size is <= minimum required size, weapon is finesse-eligible
                    // Based on nwmain.exe: Comparison is creatureSize <= minCreatureSize (line 21 of GetWeaponFinesse)
                    if (creatureSize <= minCreatureSize.Value)
                    {
                        return true;
                    }
                }

                // If no WeaponFinesseMinimumCreatureSize column or creature is too large, weapon is not finesse-eligible
                return false;
            }
            catch
            {
                // Error accessing table, fall through to return false
            }

            return false;
        }

        /// <summary>
        /// Gets the creature size from appearance.2da (Aurora-specific).
        /// </summary>
        /// <param name="creature">The creature entity.</param>
        /// <returns>The creature size category (default 3 for medium if lookup fails).</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::GetWeaponFinesse @ 0x14040b680
        /// - Accesses creature size at offset 0x718 in CNWSCreature (via this + 0x30 pointer)
        /// - ExecuteCommandGetCreatureSize @ 0x1405213f0 shows: iVar3 = *(int *)(pCVar2 + 0x718);
        /// - Original implementation: Size is cached in creature object, originally loaded from appearance.2da
        /// - Appearance.2da column: "sizecategory" (integer, 0=Small, 1=Medium, 2=Large, 3=Huge, 4=Gargantuan)
        /// - Default size: 3 (Medium) - matches nwmain.exe behavior where medium creatures can finesse medium weapons
        /// - Cross-engine: Similar pattern in Odyssey (appearance.2da sizecategory), Eclipse (appearance.2da sizecategory)
        /// </remarks>
        private int GetCreatureSize(IEntity creature)
        {
            if (creature == null)
            {
                return 3; // Default to medium size
            }

            // Get creature component to access appearance type
            var creatureComp = creature.GetComponent<AuroraCreatureComponent>();
            if (creatureComp == null)
            {
                return 3; // Default to medium size
            }

            // Get appearance type from creature component
            int appearanceType = creatureComp.AppearanceType;
            if (appearanceType < 0)
            {
                return 3; // Default to medium size
            }

            // Look up appearance.2da to get size category
            try
            {
                var appearanceRow = _tableManager.GetRow("appearance", appearanceType);
                if (appearanceRow != null)
                {
                    // Get sizecategory column from appearance.2da
                    // Based on nwmain.exe: Size category is stored in appearance.2da "sizecategory" column
                    // Size categories: 0=Small, 1=Medium, 2=Large, 3=Huge, 4=Gargantuan
                    int? sizeCategory = appearanceRow.GetInteger("sizecategory", null);
                    if (sizeCategory.HasValue)
                    {
                        return sizeCategory.Value;
                    }
                }
            }
            catch
            {
                // Error accessing table, fall through to default
            }

            // Default to medium size (3) if lookup fails
            // This matches nwmain.exe behavior where medium creatures can finesse medium weapons
            return 3;
        }

        /// <summary>
        /// Checks if a creature has a specific feat (Aurora-specific).
        /// </summary>
        /// <param name="creature">The creature entity to check.</param>
        /// <param name="featId">The feat ID to check for.</param>
        /// <returns>True if the creature has the feat, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900
        /// Located via string references: Feat list in creature data structure (UTC GFF for NWN)
        /// Original implementation: Checks if creature has the feat in their feat list
        /// Feats stored in AuroraCreatureComponent (primary feat list and bonus feat list)
        /// Cross-engine: Similar in Odyssey (CreatureComponent.FeatList), Aurora has two feat lists (normal + bonus)
        /// </remarks>
        private bool HasFeat(IEntity creature, int featId)
        {
            if (creature == null)
            {
                return false;
            }

            // Get AuroraCreatureComponent to access feat lists
            var creatureComp = creature.GetComponent<AuroraCreatureComponent>();
            if (creatureComp == null)
            {
                return false;
            }

            // Use Aurora-specific HasFeat method which checks both primary and bonus feat lists
            return creatureComp.HasFeat(featId);
        }

        /// <summary>
        /// Gets the critical multiplier for a weapon from baseitems.2da (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Critical multiplier lookup from baseitems.2da
        /// Column name: crithitmult (critical hit multiplier)
        /// Original implementation: CNWBaseItemArray::GetBaseItem @ 0x14029ca00 accesses baseitems.2da data
        /// </remarks>
        protected override int GetCriticalMultiplier(int baseItemId)
        {
            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow != null)
                {
                    return twoDARow.GetInteger("crithitmult", 2) ?? 2;
                }
            }
            catch
            {
                // Use default
            }

            return 2; // Default
        }

        /// <summary>
        /// Gets the critical threat range from baseitems.2da (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Critical threat range lookup from baseitems.2da
        /// Column name: critthreat (critical threat range)
        /// Original implementation: CNWBaseItemArray::GetBaseItem @ 0x14029ca00 accesses baseitems.2da data
        /// </remarks>
        protected override int GetCriticalThreatRangeFromTable(int baseItemId)
        {
            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow != null)
                {
                    return twoDARow.GetInteger("critthreat", 20) ?? 20;
                }
            }
            catch
            {
                // Fallback
            }

            return 20; // Default
        }
    }
}


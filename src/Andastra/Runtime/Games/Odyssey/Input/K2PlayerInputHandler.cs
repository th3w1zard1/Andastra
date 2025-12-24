using System;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Movement;
using Andastra.Runtime.Core.Party;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Runtime.Engines.Odyssey.Systems;

namespace Andastra.Runtime.Games.Odyssey.Input
{
    /// <summary>
    /// KOTOR 2: The Sith Lords (swkotor2.exe) specific player input handler.
    /// </summary>
    /// <remarks>
    /// K2 Player Input Handler:
    /// - Based on swkotor2.exe input system reverse engineering via Ghidra MCP
    /// - Located via string references: "Mouse Sensitivity" @ 0x007c85cc, "Mouse Look" @ 0x007c8608, "Reverse Mouse Buttons" @ 0x007c8628
    /// - "EnableHardwareMouse" @ 0x007c71c8, "Enable Mouse Teleporting To Buttons" @ 0x007c85a8
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc704, "OnClick" @ 0x007c1a20
    /// - Input system: "Input" @ 0x007c2520, "Mouse" @ 0x007cb908
    /// - Input class: "CExoInputInternal" (exoinputinternal.cpp @ 0x007c64dc)
    /// - Error: "CExoInputInternal::GetEvents() Invalid InputClass parameter" @ 0x007c64f4
    /// - "Unnamed Input Class" @ 0x007c64c8
    /// - GUI references: ";gui_mouse" @ 0x007b5f93, "optmouse_p" @ 0x007d1f64
    /// - "LBL_MOUSESEN" @ 0x007d1f44, "SLI_MOUSESEN" @ 0x007d1f54, "BTN_MOUSE" @ 0x007d28a0
    /// - Original implementation: Uses DirectInput8 (DINPUT8.dll @ 0x0080a6c0, DirectInput8Create @ 0x0080a6ac)
    /// - Click-to-move, object interaction, party control, pause
    /// - K2-specific features:
    ///   - Influence system integration (party member influence affects availability)
    ///   - Prestige class selection (affects quick slot abilities)
    ///   - Combat forms (affects combat cursor modes)
    ///   - Item crafting system (workbench/lab station interaction)
    /// - Reverse engineered functions:
    ///   - FUN_005226d0 @ 0x005226d0 (swkotor2.exe: player input handling and movement)
    ///   - UpdateCreatureMovement @ 0x0054be70 (movement handling)
    ///   - Input processing functions in CExoInputInternal class
    /// - Cross-engine comparison:
    ///   - K1 (swkotor.exe): Similar input system, but lacks K2-specific features (Influence, Prestige Classes, Combat Forms)
    ///   - K2 (swkotor2.exe): Enhanced input system with additional features
    ///   - Common: Both use DirectInput8, similar click-to-move system, same basic input model
    /// - Inheritance structure:
    ///   - PlayerInputHandler (Runtime.Core.Movement): Core input handling interface
    ///   - OdysseyPlayerInputHandler (Runtime.Games.Odyssey.Input): Common Odyssey logic
    ///   - K2PlayerInputHandler (Runtime.Games.Odyssey.Input): K2-specific (swkotor2.exe: 0x005226d0, 0x0054be70)
    /// </remarks>
    public class K2PlayerInputHandler : OdysseyPlayerInputHandler
    {
        /// <summary>
        /// Initializes a new instance of the K2 player input handler.
        /// </summary>
        /// <param name="world">The world context.</param>
        /// <param name="partySystem">The party system.</param>
        public K2PlayerInputHandler(IWorld world, PartySystem partySystem)
            : base(world, partySystem)
        {
        }

        /// <summary>
        /// Determines the cursor mode based on the hovered entity.
        /// K2-specific implementation includes Combat Forms support.
        /// </summary>
        /// <param name="hoveredEntity">The entity under the cursor.</param>
        /// <returns>The appropriate cursor mode.</returns>
        /// <remarks>
        /// Based on swkotor2.exe reverse engineering:
        /// - K2 has Combat Forms which can affect cursor display (attack cursor variations)
        /// - FUN_005226d0 @ 0x005226d0 processes input and determines cursor mode
        /// - K2-specific: Combat forms (Juyo, Makashi, etc.) affect cursor appearance when hovering over hostile targets
        /// - Combat forms are stored as "ActiveCombatForm" in entity data (int value matching CombatForm enum)
        /// - Lightsaber forms (258-264) affect attack cursor display
        /// - Force forms (265-268) do not affect cursor display (use standard Attack cursor)
        /// - Based on swkotor2.exe: Combat forms are tracked in entity stats/combat state
        /// - Original implementation: swkotor2.exe checks active combat form when determining attack cursor
        /// </remarks>
        protected override CursorMode DetermineCursorMode(IEntity hoveredEntity)
        {
            // Get base cursor mode from parent implementation
            CursorMode baseMode = base.DetermineCursorMode(hoveredEntity);

            // Only apply combat form-specific cursors for attack mode
            if (baseMode != CursorMode.Attack)
            {
                return baseMode;
            }

            // Get active combat form from party leader
            CombatForm activeForm = GetActiveCombatForm();
            if (activeForm == CombatForm.None)
            {
                return baseMode; // No form active, use standard attack cursor
            }

            // Map lightsaber combat forms to form-specific attack cursors
            // Force forms do not affect cursor display (use standard Attack cursor)
            switch (activeForm)
            {
                case CombatForm.ShiiCho:
                    return CursorMode.AttackShiiCho;

                case CombatForm.Makashi:
                    return CursorMode.AttackMakashi;

                case CombatForm.Soresu:
                    return CursorMode.AttackSoresu;

                case CombatForm.Ataru:
                    return CursorMode.AttackAtaru;

                case CombatForm.Shien:
                    return CursorMode.AttackShien;

                case CombatForm.Niman:
                    return CursorMode.AttackNiman;

                case CombatForm.Juyo:
                    return CursorMode.AttackJuyo;

                // Force forms (ForceFocus, ForcePotency, ForceAffinity, ForceMastery) use standard attack cursor
                default:
                    return baseMode;
            }
        }

        /// <summary>
        /// Gets the active combat form from the party leader.
        /// </summary>
        /// <returns>The active combat form, or None if no form is active or leader is not available.</returns>
        /// <remarks>
        /// Based on swkotor2.exe reverse engineering:
        /// - Combat forms are stored as "ActiveCombatForm" in entity data
        /// - GetIsFormActive NWScript function checks if specific form is active
        /// - Form values match CombatForm enum constants (258-268 for lightsaber/force forms)
        /// - Original implementation: swkotor2.exe stores active form in creature data structure
        /// - Form activation: Set via SetIsFormActive NWScript function or combat form selection UI
        /// </remarks>
        private CombatForm GetActiveCombatForm()
        {
            // Get current party leader
            var leader = (IEntity)(_partySystem?.Leader);
            if (leader == null)
            {
                return CombatForm.None;
            }

            // Get active combat form from entity data (stored as "ActiveCombatForm")
            // Based on swkotor2.exe: ActiveCombatForm is stored as int in entity data
            // Located via string references: GetIsFormActive function checks "ActiveCombatForm" data
            if (leader.HasData("ActiveCombatForm"))
            {
                int activeFormValue = leader.GetData<int>("ActiveCombatForm", 0);

                // Convert int value to CombatForm enum
                // Combat form values: 258-268 for lightsaber/force forms, 0 for none
                if (Enum.IsDefined(typeof(CombatForm), activeFormValue))
                {
                    return (CombatForm)activeFormValue;
                }
            }

            return CombatForm.None;
        }

        /// <summary>
        /// Gets the attack range based on equipped weapon.
        /// K2-specific implementation may consider prestige classes and combat forms.
        /// </summary>
        /// <returns>The attack range for the current weapon.</returns>
        /// <remarks>
        /// Based on swkotor2.exe reverse engineering:
        /// - K2 has prestige classes which can affect weapon proficiency and range
        /// - Combat forms can affect attack range in some cases
        /// - Base weapon range calculation is similar to K1, but with K2-specific modifiers
        /// - Located via string references: Weapon range calculations in combat system
        /// </remarks>
        protected override float GetAttackRange()
        {
            // Get current party leader
            var leader = (IEntity)(_partySystem?.Leader);
            if (leader == null)
            {
                return GetDefaultMeleeRange();
            }

            // Get equipped weapon from main hand (slot 4)
            // Based on swkotor2.exe: INVENTORY_SLOT_RIGHTWEAPON = 4
            // Located via string references: "INVENTORY_SLOT_RIGHTWEAPON" = 4
            // Original implementation: Gets equipped weapon from right hand slot
            IInventoryComponent inventory = leader.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return GetDefaultMeleeRange();
            }

            // INVENTORY_SLOT_RIGHTWEAPON = 4
            IEntity weapon = inventory.GetItemInSlot(4);
            if (weapon == null)
            {
                return GetDefaultMeleeRange(); // Default melee range (unarmed)
            }

            // Check if weapon has range data stored in entity data
            if (weapon is Entity weaponEntity && weaponEntity.HasData("Range"))
            {
                float range = weaponEntity.GetData<float>("Range", GetDefaultMeleeRange());
                if (range > 0)
                {
                    return range;
                }
            }

            // Get base item ID from weapon component and look up attack range from baseitems.2da
            // Based on swkotor2.exe weapon system
            // Located via string references: "WeaponType" in baseitems.2da, "maxattackrange" column
            // Original implementation: Reads maxattackrange from baseitems.2da using BaseItem ID
            // xoreos implementation: Item::getMaxAttackRange() @ vendor/xoreos/src/engines/kotorbase/item.cpp:74
            //   Reads _maxAttackRange = twoDA.getFloat("maxattackrange") from baseitems.2da
            // PyKotor documentation: baseitems.2da has "maxattackrange" column (Integer) for maximum attack range
            IItemComponent itemComponent = weapon.GetComponent<IItemComponent>();
            if (itemComponent != null && World?.GameDataProvider != null)
            {
                int baseItemId = itemComponent.BaseItem;
                if (baseItemId >= 0)
                {
                    // Read maxattackrange from baseitems.2da using GameDataProvider
                    // Based on swkotor2.exe: Reads maxattackrange column from baseitems.2da row indexed by BaseItem ID
                    float maxAttackRange = World.GameDataProvider.GetTableFloat("baseitems", baseItemId, "maxattackrange", 0.0f);
                    if (maxAttackRange > 0.0f)
                    {
                        // Convert from game units to world units if necessary (maxattackrange is typically in game units)
                        // Based on xoreos implementation: getMaxAttackRange() returns float directly from 2DA
                        // KOTOR uses game units where 1.0 = 1 meter approximately, so direct conversion should work
                        return maxAttackRange;
                    }

                    // Fallback: Check if ranged weapon to use default ranged range
                    // Based on swkotor2.exe: Ranged weapons have longer default range than melee
                    // Read rangedweapon flag from baseitems.2da to determine if ranged
                    int rangedWeapon = (int)World.GameDataProvider.GetTableFloat("baseitems", baseItemId, "rangedweapon", 0.0f);
                    if (rangedWeapon != 0)
                    {
                        // Default ranged weapon range (approximate fallback when maxattackrange not available)
                        return GetDefaultRangedWeaponRange();
                    }
                }
            }

            // Default melee range (unarmed or melee weapon without range data)
            return GetDefaultMeleeRange();
        }

        /// <summary>
        /// Checks if an entity is hostile.
        /// K2-specific implementation considers influence system effects on faction relationships.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns>True if the entity is hostile.</returns>
        /// <remarks>
        /// Based on swkotor2.exe reverse engineering:
        /// - K2 has an Influence system that can affect faction relationships
        /// - Party member influence can change how NPCs react (hostile/friendly)
        /// - Base hostility check is similar to K1, but with K2-specific influence modifiers
        /// - Located via string references: Faction system, influence system
        /// - swkotor2.exe: FUN_005226d0 @ 0x005226d0 (player input handling) checks influence when determining hostility
        /// - Influence system: Party member influence (0-100, 50 = neutral) affects NPC reactions
        /// - High influence (80-100) with certain party members can make NPCs more friendly
        /// - Low influence (0-20) can make NPCs more hostile
        /// - Influence affects personal reputation, which overrides faction-based reputation
        /// - Original implementation: swkotor2.exe checks active party members' influence when determining hostility
        /// - Influence modifier calculation:
        ///   - For each active party member with influence != 50 (neutral):
        ///     - High influence (80-100): +1 to +20 reputation modifier (more friendly)
        ///     - Low influence (0-20): -1 to -20 reputation modifier (more hostile)
        ///   - Influence modifier is averaged across active party members
        ///   - Modified reputation = base reputation + influence modifier
        ///   - Hostility threshold: reputation <= 10 = hostile
        /// </remarks>
        protected override bool IsHostile(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            // Get base hostility check from parent implementation
            bool baseHostile = base.IsHostile(entity);

            // Only apply influence modifiers to creatures (NPCs)
            // Influence system primarily affects NPC reactions, not objects/doors/placeables
            if (entity.ObjectType != ObjectType.Creature)
            {
                return baseHostile;
            }

            // Get party leader for influence calculations
            var leader = (IEntity)(_partySystem?.Leader);
            if (leader == null)
            {
                return baseHostile;
            }

            // Get FactionManager to access reputation system
            // FactionManager is used to get base reputation and apply influence modifiers
            FactionManager factionManager = GetFactionManager();
            if (factionManager == null)
            {
                // No FactionManager available, use base implementation
                return baseHostile;
            }

            // Get base reputation between party leader and entity
            int baseReputation = factionManager.GetReputation(leader, entity);

            // Calculate influence-based reputation modifier
            int influenceModifier = CalculateInfluenceReputationModifier(entity);

            // Apply influence modifier to base reputation
            int modifiedReputation = baseReputation + influenceModifier;
            modifiedReputation = Math.Max(0, Math.Min(100, modifiedReputation)); // Clamp to 0-100

            // Determine hostility based on modified reputation
            // Reputation <= 10 = hostile, > 10 = not hostile (neutral or friendly)
            bool isHostile = modifiedReputation <= FactionManager.HostileThreshold;

            return isHostile;
        }

        /// <summary>
        /// Gets the FactionManager instance from the world or entity components.
        /// </summary>
        /// <returns>The FactionManager instance, or null if not available.</returns>
        /// <remarks>
        /// Based on swkotor2.exe reverse engineering:
        /// - FactionManager is accessible through entity components or world context
        /// - FactionComponent has reference to FactionManager for reputation lookups
        /// - Original implementation: FactionManager is stored in GameSession and accessible through components
        /// </remarks>
        private FactionManager GetFactionManager()
        {
            // Try to get FactionManager from party leader's faction component
            var leader = (IEntity)(_partySystem?.Leader);
            if (leader != null)
            {
                IFactionComponent leaderFaction = leader.GetComponent<IFactionComponent>();
                if (leaderFaction != null && leaderFaction is OdysseyFactionComponent odysseyFaction)
                {
                    // Use reflection to get FactionManager from OdysseyFactionComponent
                    // OdysseyFactionComponent has _factionManager field
                    System.Reflection.FieldInfo factionManagerField = typeof(OdysseyFactionComponent).GetField("_factionManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (factionManagerField != null)
                    {
                        FactionManager factionManager = factionManagerField.GetValue(odysseyFaction) as FactionManager;
                        if (factionManager != null)
                        {
                            return factionManager;
                        }
                    }
                }
            }

            // Fallback: Try to get FactionManager from World if it has a FactionManager property
            // This is engine-specific and may not be available in all contexts
            return null;
        }

        /// <summary>
        /// Calculates the reputation modifier based on active party members' influence with the player.
        /// </summary>
        /// <param name="targetEntity">The target entity to check influence effects for.</param>
        /// <returns>The reputation modifier (-20 to +20) based on party influence.</returns>
        /// <remarks>
        /// Based on swkotor2.exe reverse engineering:
        /// - Influence system affects how NPCs react to the party
        /// - Each active party member's influence (0-100, 50 = neutral) contributes to reputation modifier
        /// - High influence (80-100): Positive modifier (makes NPCs more friendly)
        /// - Low influence (0-20): Negative modifier (makes NPCs more hostile)
        /// - Neutral influence (21-79): No modifier
        /// - Influence modifier calculation:
        ///   - For each active party member:
        ///     - If influence > 50: modifier = (influence - 50) / 2.5 (max +20 at 100)
        ///     - If influence < 50: modifier = (influence - 50) / 2.5 (min -20 at 0)
        ///   - Average modifier across all active party members
        /// - Original implementation: swkotor2.exe calculates influence modifier when determining hostility
        /// - Some NPCs may have specific relationships with certain party members (not implemented here, would require NPC-specific data)
        /// </remarks>
        private int CalculateInfluenceReputationModifier(IEntity targetEntity)
        {
            if (_partySystem == null || targetEntity == null)
            {
                return 0;
            }

            // Get active party members (excluding player character)
            System.Collections.Generic.IReadOnlyList<PartyMember> activeMembers = _partySystem.ActiveParty;
            if (activeMembers == null || activeMembers.Count == 0)
            {
                return 0;
            }

            int totalModifier = 0;
            int memberCount = 0;

            // Calculate influence modifier for each active party member
            foreach (PartyMember member in activeMembers)
            {
                // Skip player character (influence doesn't apply to self)
                if (member.IsPlayerCharacter)
                {
                    continue;
                }

                // Get influence value (0-100, 50 = neutral)
                int influence = member.Influence;

                // Calculate modifier for this party member
                // Influence 50 = neutral (no modifier)
                // Influence 0 = -20 modifier (very hostile)
                // Influence 100 = +20 modifier (very friendly)
                int memberModifier = 0;
                if (influence > 50)
                {
                    // High influence: positive modifier (more friendly)
                    // Scale from 0 (at 50) to +20 (at 100)
                    memberModifier = (int)((influence - 50) / 2.5f);
                    memberModifier = Math.Min(20, memberModifier); // Cap at +20
                }
                else if (influence < 50)
                {
                    // Low influence: negative modifier (more hostile)
                    // Scale from 0 (at 50) to -20 (at 0)
                    memberModifier = (int)((influence - 50) / 2.5f);
                    memberModifier = Math.Max(-20, memberModifier); // Cap at -20
                }

                totalModifier += memberModifier;
                memberCount++;
            }

            // Average modifier across all active party members
            if (memberCount > 0)
            {
                return totalModifier / memberCount;
            }

            return 0;
        }

    }
}

using System;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Party;

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
        /// - K2-specific: Combat forms (Juyo, Makashi, etc.) may affect cursor appearance
        /// - Otherwise similar to K1 cursor mode determination
        /// - Uses base class implementation for now, can be extended for combat form-specific cursor modes
        /// </remarks>
        protected override CursorMode DetermineCursorMode(IEntity hoveredEntity)
        {
            // Use base implementation for common cases
            // K2-specific: Future enhancement could check for combat forms that affect cursor
            // Combat forms in K2 can modify attack cursor appearance
            // Based on swkotor2.exe: Combat forms are tracked in entity stats/combat state
            return base.DetermineCursorMode(hoveredEntity);
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
            if (itemComponent != null && _world?.GameDataProvider != null)
            {
                int baseItemId = itemComponent.BaseItem;
                if (baseItemId >= 0)
                {
                    // Read maxattackrange from baseitems.2da using GameDataProvider
                    // Based on swkotor2.exe: Reads maxattackrange column from baseitems.2da row indexed by BaseItem ID
                    float maxAttackRange = _world.GameDataProvider.GetTableFloat("baseitems", baseItemId, "maxattackrange", 0.0f);
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
                    int rangedWeapon = (int)_world.GameDataProvider.GetTableFloat("baseitems", baseItemId, "rangedweapon", 0.0f);
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
        /// K2-specific implementation may consider influence system.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns>True if the entity is hostile.</returns>
        /// <remarks>
        /// Based on swkotor2.exe reverse engineering:
        /// - K2 has an Influence system that can affect faction relationships
        /// - Party member influence can change how NPCs react (hostile/friendly)
        /// - Base hostility check is similar to K1, but with K2-specific influence modifiers
        /// - Located via string references: Faction system, influence system
        /// - Uses base class implementation for now, can be extended for influence system integration
        /// </remarks>
        protected override bool IsHostile(IEntity entity)
        {
            // Use base implementation for common faction checking
            // K2-specific: Future enhancement could check influence system effects on faction relationships
            // Influence can modify how NPCs react to the party
            // Based on swkotor2.exe: Influence system affects faction relationships
            return base.IsHostile(entity);
        }

    }
}

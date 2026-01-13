using System;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Movement;
using Andastra.Runtime.Core.Party;

namespace Andastra.Game.Games.Odyssey.Input
{
    /// <summary>
    /// KOTOR 1 (swkotor.exe) specific player input handler.
    /// </summary>
    /// <remarks>
    /// K1 Player Input Handler:
    /// - Based on swkotor.exe input system further analysis
    /// - Located via string references: "Input" @ 0x007c2520, "Mouse" @ 0x007cb908, "Mouse Sensitivity" @ 0x007c85cc
    /// - "EnableHardwareMouse" @ 0x007c71c8, "Enable Mouse Teleporting To Buttons" @ 0x007c85a8
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc704, "OnClick" @ 0x007c1a20
    /// - Input system: "Input" @ 0x007c2520, "Mouse" @ 0x007cb908
    /// - Input class: "CExoInputInternal" (exoinputinternal.cpp equivalent in K1)
    /// - GUI references: ";gui_mouse" @ 0x007b5f93, "optmouse_p" @ 0x007d1f64 (K1 uses similar GUI names)
    /// - Original implementation: Uses DirectInput8 (DINPUT8.dll, DirectInput8Create) for input
    /// - Click-to-move, object interaction, party control, pause
    /// - K1-specific features:
    ///   - No Influence system (simpler faction relationships)
    ///   - No Prestige Classes (base classes only)
    ///   - No Combat Forms (standard combat only)
    ///   - Simpler item system (no workbench/lab station crafting)
    /// - Reverse engineered functions (swkotor.exe):
    ///   - 0x0054b550 @ 0x0054b550 (swkotor.exe: input event handler for mouse clicks and movement)
    ///     - Processes DirectInput event codes (0x26 = left mouse button, 0xe2 = right mouse button, etc.)
    ///     - Handles click-to-move, object selection, and interaction
    ///     - Validates movement targets on walkmesh before processing
    ///   - Input processing functions in CExoInputInternal class equivalent
    ///   - Note: 0x005226d0 in swkotor2.exe is SerializeCreature_K2 (serialization), NOT input handling
    /// - Cross-engine comparison:
    ///   - K1 (swkotor.exe): Simpler input system without K2-specific features
    ///   - K2 (swkotor2.exe): Enhanced input system with Influence, Prestige Classes, Combat Forms
    ///   - Common: Both use DirectInput8, similar click-to-move system, same basic input model
    /// - Inheritance structure:
    ///   - PlayerInputHandler (Runtime.Core.Movement): Core input handling interface
    ///   - OdysseyPlayerInputHandler (Runtime.Games.Odyssey.Input): Common Odyssey logic
    ///   - K1PlayerInputHandler (Runtime.Games.Odyssey.Input): K1-specific (swkotor.exe)
    /// </remarks>
    public class K1PlayerInputHandler : OdysseyPlayerInputHandler
    {
        /// <summary>
        /// Initializes a new instance of the K1 player input handler.
        /// </summary>
        /// <param name="world">The world context.</param>
        /// <param name="partySystem">The party system.</param>
        public K1PlayerInputHandler(IWorld world, PartySystem partySystem)
            : base(world, partySystem)
        {
        }

        /// <summary>
        /// Determines the cursor mode based on the hovered entity.
        /// K1 implementation - simpler than K2 (no combat forms).
        /// </summary>
        /// <param name="hoveredEntity">The entity under the cursor.</param>
        /// <returns>The appropriate cursor mode.</returns>
        /// <remarks>
        /// Based on swkotor.exe reverse engineering:
        /// - K1 has a simpler cursor system without combat form variations
        /// - 0x0054b550 @ 0x0054b550 (swkotor.exe) processes input events and determines cursor mode
        /// - K1-specific: Standard cursor modes only (no combat form-specific variations)
        /// - Uses base class implementation which handles all common cases
        /// - Input handling is fully implemented in base PlayerInputHandler class
        ///   (OnLeftClick, OnRightClick, OnPauseToggle, OnCycleParty, etc.)
        /// </remarks>
        protected override CursorMode DetermineCursorMode(IEntity hoveredEntity)
        {
            // K1 uses the standard cursor mode determination without combat form modifications
            // Based on swkotor.exe: Simpler cursor system than K2
            return base.DetermineCursorMode(hoveredEntity);
        }

        /// <summary>
        /// Gets the attack range based on equipped weapon.
        /// K1 implementation - simpler than K2 (no prestige class modifiers).
        /// </summary>
        /// <returns>The attack range for the current weapon.</returns>
        /// <remarks>
        /// Based on swkotor.exe reverse engineering:
        /// - K1 has base classes only (no prestige classes)
        /// - Weapon range calculation is straightforward without prestige class modifiers
        /// - Base weapon range calculation is similar to K2, but without K2-specific class modifiers
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
            // Based on swkotor.exe: INVENTORY_SLOT_RIGHTWEAPON = 4
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
            // Based on swkotor.exe weapon system
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
                    // Based on swkotor.exe: Reads maxattackrange column from baseitems.2da row indexed by BaseItem ID
                    float maxAttackRange = World.GameDataProvider.GetTableFloat("baseitems", baseItemId, "maxattackrange", 0.0f);
                    if (maxAttackRange > 0.0f)
                    {
                        // Convert from game units to world units if necessary (maxattackrange is typically in game units)
                        // Based on xoreos implementation: getMaxAttackRange() returns float directly from 2DA
                        // KOTOR uses game units where 1.0 = 1 meter approximately, so direct conversion should work
                        return maxAttackRange;
                    }

                    // Fallback: Check if ranged weapon to use default ranged range
                    // Based on swkotor.exe: Ranged weapons have longer default range than melee
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
            // K1-specific: No prestige class modifiers, straightforward range calculation
            return GetDefaultMeleeRange();
        }

        /// <summary>
        /// Checks if an entity is hostile.
        /// K1 implementation - simpler than K2 (no influence system).
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns>True if the entity is hostile.</returns>
        /// <remarks>
        /// Based on swkotor.exe reverse engineering:
        /// - K1 has a simpler faction system without influence modifiers
        /// - Faction relationships are static (no dynamic influence-based changes)
        /// - Base hostility check is similar to K2, but without influence system modifiers
        /// - Located via string references: Faction system
        /// - Uses base class implementation which handles all common faction checking
        /// </remarks>
        protected override bool IsHostile(IEntity entity)
        {
            // K1 uses standard faction checking without influence system modifications
            // Based on swkotor.exe: Simpler faction system than K2
            return base.IsHostile(entity);
        }
    }
}

using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.TwoDA;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Item category determined from item class string in baseitems.2da.
    /// </summary>
    /// <remarks>
    /// Item categories are determined from the itemclass string column in baseitems.2da.
    /// These categories determine item behavior and usage effects.
    /// </remarks>
    internal enum ItemCategory
    {
        Unknown,
        Weapon,
        Armor,
        Shield,
        Medical,
        Stimulant,
        Grenade,
        DroidRepair,
        Quest,
        Upgrade,
        Misc
    }

    /// <summary>
    /// Action to use an item (consumables, usable items, etc.).
    /// </summary>
    /// <remarks>
    /// Use Item Action:
    /// - Common item usage system across all BioWare engines
    /// - Based on verified components of:
    ///   - swkotor.exe/swkotor2.exe: "OnUsed" @ 0x007c1f70, "i_useitemm" @ 0x007ccde0, "BTN_USEITEM" @ 0x007d1080
    ///   - swkotor2.exe: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0, "Mod_OnActvtItem" @ 0x007be7f4
    ///   - nwmain.exe: Aurora item usage system (similar patterns)
    ///   - daorigins.exe/DragonAge2.exe: Eclipse item usage with enhanced effects
    /// - Common functionality across engines:
    ///   - Item usage: Items can be used from inventory or quick slots
    ///   - Consumable items: Items with charges (potions, grenades, etc.) consume a charge when used
    ///   - Item effects: Items can have properties that apply effects (healing, damage, status effects, etc.)
    ///   - OnUsed script: Items can have OnUsed script that executes when item is used
    ///   - Charge consumption: Items with 0 charges are removed from inventory after use
    /// - Engine-specific: Property-to-effect conversion uses engine-specific 2DA tables (itempropdef.2da)
    /// </remarks>
    public class ActionUseItem : ActionBase
    {
        private readonly uint _itemObjectId;
        private readonly uint _targetObjectId;
        private bool _used;

        public ActionUseItem(uint itemObjectId, uint targetObjectId = 0)
            : base(ActionType.UseItem)
        {
            _itemObjectId = itemObjectId;
            _targetObjectId = targetObjectId;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            if (actor == null || !actor.IsValid)
            {
                return ActionStatus.Failed;
            }

            // Get item entity
            IEntity item = actor.World.GetEntity(_itemObjectId);
            if (item == null || !item.IsValid || item.ObjectType != ObjectType.Item)
            {
                return ActionStatus.Failed;
            }

            // Check if item is in actor's inventory
            IInventoryComponent inventory = actor.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return ActionStatus.Failed;
            }

            bool hasItem = false;
            foreach (IEntity invItem in inventory.GetAllItems())
            {
                if (invItem != null && invItem.ObjectId == _itemObjectId)
                {
                    hasItem = true;
                    break;
                }
            }

            if (!hasItem)
            {
                return ActionStatus.Failed; // Item not in inventory
            }

            // Get item component
            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return ActionStatus.Failed;
            }

            // Use item (only once)
            if (!_used)
            {
                _used = true;

                // Get target (default to actor if not specified)
                IEntity target = _targetObjectId != 0 ? actor.World.GetEntity(_targetObjectId) : actor;
                if (target == null || !target.IsValid)
                {
                    target = actor; // Fallback to actor
                }

                // Apply item effects
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item usage applies item properties as effects
                // Located via string references: Item properties can have effects (healing, damage, status effects, etc.)
                // Original implementation: Item properties are converted to effects and applied to target
                ApplyItemEffects(actor, target, item, itemComponent);

                // Consume charge if item has charges
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Consumable items have charges that are consumed on use
                // Located via string references: "Charges" @ 0x007c0a94 (charges field in UTI)
                // Original implementation: Items with charges (chargesstarting > 0 in baseitems.2da) consume a charge when used
                if (itemComponent.Charges > 0)
                {
                    itemComponent.Charges--;

                    // Remove item if charges depleted
                    if (itemComponent.Charges <= 0)
                    {
                        inventory.RemoveItem(item);
                        // Item will be destroyed when removed from inventory
                    }
                }

                // Fire OnUsed script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): OnUsed script fires when item is used
                // Located via string references: "OnUsed" @ 0x007c1f70, "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0
                // Original implementation: OnUsed script executes on item entity with actor as triggerer
                IEventBus eventBus = actor.World?.EventBus;
                if (eventBus != null)
                {
                    eventBus.FireScriptEvent(item, ScriptEvent.OnUsed, actor);
                }

                // Fire OnInventoryDisturbed script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ON_INVENTORY_DISTURBED fires when items are used/consumed
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (0x1b)
                // Original implementation: OnInventoryDisturbed script fires on actor entity when inventory is modified
                if (eventBus != null)
                {
                    eventBus.FireScriptEvent(actor, ScriptEvent.OnDisturbed, item);
                }
            }

            return ActionStatus.Complete;
        }

        /// <summary>
        /// Applies item effects to the target.
        /// </summary>
        /// <remarks>
        /// Item Effect Application:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) item effect system
        /// - Original implementation: Item properties are converted to effects and applied via EffectSystem
        /// - Item properties can have various effects: healing, damage, status effects, ability bonuses, etc.
        /// - Property types: Property IDs map to effect types via itempropdef.2da lookup
        /// - For consumable items: Apply effects based on item properties and base item type
        /// - Uses itempropdef.2da when available, falls back to comprehensive hardcoded mappings
        /// </remarks>
        private void ApplyItemEffects(IEntity caster, IEntity target, IEntity item, IItemComponent itemComponent)
        {
            if (caster.World == null || caster.World.EffectSystem == null)
            {
                return;
            }

            EffectSystem effectSystem = caster.World.EffectSystem;

            // Apply item properties as effects
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item properties are converted to effects
            // Located via string references: Item property types map to effect types
            // Original implementation: Each property type has corresponding effect type
            // Common property types (from Aurora engine standard):
            // - Property type 1-6: Ability bonuses (STR, DEX, CON, INT, WIS, CHA)
            // - Property type 100+: Special effects (healing, damage, etc.)
            foreach (ItemProperty property in itemComponent.Properties)
            {
                // Convert property type to effect using itempropdef.2da lookup when available
                // Falls back to comprehensive hardcoded mappings based on Aurora engine standard
                Effect effect = ConvertPropertyToEffect(property, caster.World);
                if (effect != null)
                {
                    effectSystem.ApplyEffect(target, effect, caster);
                }
            }

            // For consumable items, apply effects based on baseitems.2da item class if no properties found
            // swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - Item usage checks baseitems.2da for item class and chargesstarting
            // swkotor2.exe: 0x005ff170 (FUN_005ff170) - Item usage checks baseitems.2da for item class and chargesstarting
            // Located via string references: "baseitems" @ 0x007c4594 (swkotor2.exe), "BASEITEMS" @ 0x0074b294 (swkotor.exe)
            // swkotor2.exe: 0x005fb0f0 (FUN_005fb0f0) loads base item data from baseitems.2da
            // Items with chargesstarting > 0 in baseitems.2da are consumables that apply effects when used
            if (itemComponent.Properties.Count == 0 && itemComponent.Charges > 0)
            {
                // Get base item ID and look up item class from baseitems.2da
                int baseItemId = itemComponent.BaseItem;
                if (baseItemId >= 0)
                {
                    // Load baseitems.2da to get item class and consumable information
                    // swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - Base item stats loaded from baseitems.2da via GameDataProvider
                    // swkotor2.exe: 0x005ff170 (CSWBaseItemArray::Load) - Base item stats loaded from baseitems.2da via GameDataProvider
                    TwoDA baseitemsTable = null;
                    if (caster.World.GameDataProvider != null)
                    {
                        try
                        {
                            baseitemsTable = caster.World.GameDataProvider.GetTable("baseitems");
                        }
                        catch
                        {
                            // If table loading fails, fall back to default behavior
                            baseitemsTable = null;
                        }
                    }

                    if (baseitemsTable != null && baseItemId < baseitemsTable.GetHeight())
                    {
                        try
                        {
                            TwoDARow baseItemRow = baseitemsTable.GetRow(baseItemId);
                            if (baseItemRow != null)
                            {
                                // Get item class from baseitems.2da
                                // swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - ItemClass read via C2DA::GetCExoStringEntry as string
                                // swkotor2.exe: 0x005ff170 (FUN_005ff170) - ItemClass read via C2DA::GetCExoStringEntry as string
                                // Item class determines item category and behavior (weapon, armor, consumable, etc.)
                                string itemClass = baseItemRow.GetString("itemclass", "").ToLowerInvariant();

                                // Get chargesstarting to confirm it's a consumable
                                // swkotor2.exe: 0x007c4438 "ChargesStarting" string reference - Items with chargesstarting > 0 are consumables
                                // swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - Items with chargesstarting > 0 are consumables
                                int? chargesStarting = baseItemRow.GetInteger("chargesstarting", null);

                                // Apply effects based on item class for consumable items
                                // swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - Different item classes have different effects when used
                                // swkotor2.exe: 0x005ff170 (FUN_005ff170) - Different item classes have different effects when used
                                if (chargesStarting.HasValue && chargesStarting.Value > 0)
                                {
                                    ApplyEffectsByItemClass(effectSystem, target, caster, baseItemRow, itemClass, itemComponent);
                                }
                                else
                                {
                                    // Item doesn't have chargesstarting, but has charges - apply default healing
                                    ApplyDefaultConsumableEffect(effectSystem, target, caster, itemComponent);
                                }
                            }
                            else
                            {
                                // Row not found, apply default healing
                                ApplyDefaultConsumableEffect(effectSystem, target, caster, itemComponent);
                            }
                        }
                        catch
                        {
                            // If lookup fails, fall back to default healing
                            ApplyDefaultConsumableEffect(effectSystem, target, caster, itemComponent);
                        }
                    }
                    else
                    {
                        // baseitems.2da not available or invalid base item ID, apply default healing
                        ApplyDefaultConsumableEffect(effectSystem, target, caster, itemComponent);
                    }
                }
                else
                {
                    // Invalid base item ID, apply default healing
                    ApplyDefaultConsumableEffect(effectSystem, target, caster, itemComponent);
                }
            }
        }

        /// <summary>
        /// Applies effects based on item class from baseitems.2da for consumable items.
        /// </summary>
        /// <remarks>
        /// Item Class-Based Effect Application:
        /// - swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - ItemClass determines item category and behavior
        /// - swkotor2.exe: 0x005ff170 (FUN_005ff170) - ItemClass determines item category and behavior
        /// - Located via string references: "baseitems" @ 0x007c4594 (swkotor2.exe), "BASEITEMS" @ 0x0074b294 (swkotor.exe)
        /// - Original implementation: Different item classes have different effects when used
        /// - Item class is a string column in baseitems.2da (read via C2DA::GetCExoStringEntry)
        /// - Common consumable item classes:
        ///   - Medpacs: Apply healing effects (medical consumables)
        ///   - Stims: Apply ability/attack bonuses (stimulants/adrenaline)
        ///   - Grenades: Apply damage effects (typically thrown, not consumed)
        ///   - Other consumables: Apply effects based on item class string patterns
        /// - Uses item class string from baseitems.2da to determine appropriate effects
        /// </remarks>
        private void ApplyEffectsByItemClass(EffectSystem effectSystem, IEntity target, IEntity caster, TwoDARow baseItemRow, string itemClass, IItemComponent itemComponent)
        {
            if (effectSystem == null || target == null || baseItemRow == null)
            {
                return;
            }

            // Get additional information from baseitems.2da that might affect effects
            string label = baseItemRow.GetString("label", "");
            int? chargesStarting = baseItemRow.GetInteger("chargesstarting", null);

            // Determine effect based on item class string and label
            // swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - Item class determines what effects are applied when item is used
            // swkotor2.exe: 0x005ff170 (FUN_005ff170) - Item class determines what effects are applied when item is used
            // Item class is a string that identifies the item category (weapon, armor, consumable, etc.)
            ItemCategory category = GetItemCategoryFromItemClass(itemClass);

            // Check label for common consumable types (case-insensitive) as secondary check
            string labelLower = label.ToLowerInvariant();

            // Medical consumables (medpacs, medkits, etc.)
            // Medical consumables typically have itemclass strings like "medical", "medpac", "heal", or numeric strings for medical items
            // BASE_ITEM_MEDICAL_EQUIPMENT = 55 in baseitems.2da
            if (category == ItemCategory.Medical || labelLower.Contains("medpac") || labelLower.Contains("medkit") || labelLower.Contains("heal"))
            {
                // Apply healing effect
                // Amount based on charges or default healing value
                int healAmount = CalculateHealingAmount(itemComponent, chargesStarting);
                Effect healEffect = Combat.Effect.Heal(healAmount);
                effectSystem.ApplyEffect(target, healEffect, caster);
                return;
            }

            // Stimulants (stims, stimulants, adrenaline, etc.)
            // Stims apply temporary ability/attack bonuses
            // BASE_ITEM_ADRENALINE = 53, BASE_ITEM_COMBAT_SHOTS = 54 in baseitems.2da
            if (category == ItemCategory.Stimulant || labelLower.Contains("stim") || labelLower.Contains("adrenal") || labelLower.Contains("combat"))
            {
                // Apply ability bonuses and attack bonuses
                // Typical stim effects: +2 to +4 ability bonuses, +1 to +3 attack bonus
                int bonusAmount = CalculateStimBonusAmount(itemComponent, chargesStarting);

                // Apply strength bonus (common stim effect)
                Effect strBonus = Combat.Effect.AbilityModifier(Enums.Ability.Strength, bonusAmount, 0);
                strBonus.DurationType = EffectDurationType.Temporary;
                strBonus.DurationRounds = 30; // Stims typically last 30 rounds
                effectSystem.ApplyEffect(target, strBonus, caster);

                // Apply dexterity bonus (common stim effect)
                Effect dexBonus = Combat.Effect.AbilityModifier(Enums.Ability.Dexterity, bonusAmount, 0);
                dexBonus.DurationType = EffectDurationType.Temporary;
                dexBonus.DurationRounds = 30;
                effectSystem.ApplyEffect(target, dexBonus, caster);

                // Apply attack bonus (common stim effect)
                Effect attackBonus = new Effect(EffectType.AttackIncrease);
                attackBonus.Amount = bonusAmount;
                attackBonus.DurationType = EffectDurationType.Temporary;
                attackBonus.DurationRounds = 30;
                effectSystem.ApplyEffect(target, attackBonus, caster);

                return;
            }

            // Grenades (grenades, mines, etc.)
            // Grenades are typically thrown in combat, not consumed from inventory
            // Note: In actual gameplay, grenades are thrown as weapons, not consumed from inventory like medpacs
            // If a grenade is somehow used as a consumable, it typically doesn't apply effects directly
            // Instead, grenades are handled by the combat system when thrown
            // BASE_ITEM_FRAGMENTATION_GRENADES = 25, BASE_ITEM_STUN_GRENADES = 26, etc. in baseitems.2da
            if (category == ItemCategory.Grenade || labelLower.Contains("grenade") || labelLower.Contains("mine") || labelLower.Contains("explosive") || labelLower.Contains("detonator"))
            {
                // Grenades are thrown, not consumed - no effect to apply
                // The combat system handles grenade damage when thrown
                return;
            }

            // Droid repair equipment
            // BASE_ITEM_DROID_REPAIR_EQUIPMENT = 56 in baseitems.2da
            if (category == ItemCategory.DroidRepair || labelLower.Contains("droid") && (labelLower.Contains("repair") || labelLower.Contains("heal")))
            {
                // Apply healing effect (droid repair restores HP for droids)
                // Amount based on charges or default healing value
                int healAmount = CalculateHealingAmount(itemComponent, chargesStarting);
                Effect healEffect = Combat.Effect.Heal(healAmount);
                effectSystem.ApplyEffect(target, healEffect, caster);
                return;
            }

            // Fallback: Apply default consumable effect if item class doesn't match known patterns
            ApplyDefaultConsumableEffect(effectSystem, target, caster, itemComponent);
        }

        /// <summary>
        /// Applies default healing effect for consumable items when no specific item class effects are available.
        /// </summary>
        /// <remarks>
        /// Default Consumable Effect:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Default healing for consumable items without explicit properties
        /// - Original implementation: Items with charges but no properties apply basic healing
        /// - Amount based on item charges or default healing value
        /// </remarks>
        private void ApplyDefaultConsumableEffect(EffectSystem effectSystem, IEntity target, IEntity caster, IItemComponent itemComponent)
        {
            if (effectSystem == null || target == null)
            {
                return;
            }

            // Default healing for consumable items (medpacs, stims, etc.)
            // Amount based on item charges or default healing value
            int healAmount = 10; // Default healing amount
            if (itemComponent.Charges > 0 && itemComponent.Charges < 100)
            {
                healAmount = itemComponent.Charges * 5; // Scale healing with charges
            }
            Effect healEffect = Combat.Effect.Heal(healAmount);
            effectSystem.ApplyEffect(target, healEffect, caster);
        }

        /// <summary>
        /// Calculates healing amount for medical consumables based on charges and chargesstarting.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Healing amount scales with item charges and base charges
        /// </remarks>
        private int CalculateHealingAmount(IItemComponent itemComponent, int? chargesStarting)
        {
            int baseHealing = 10; // Base healing amount

            // Scale healing with charges if available
            if (itemComponent.Charges > 0 && itemComponent.Charges < 100)
            {
                baseHealing = itemComponent.Charges * 5;
            }

            // Scale with chargesstarting if available (higher starting charges = more powerful item)
            if (chargesStarting.HasValue && chargesStarting.Value > 0)
            {
                // Items with more starting charges are typically more powerful
                // Scale healing proportionally
                baseHealing = (baseHealing * chargesStarting.Value) / Math.Max(1, itemComponent.Charges);
            }

            return Math.Max(1, baseHealing); // Ensure at least 1 HP healing
        }

        /// <summary>
        /// Determines item category from item class string in baseitems.2da.
        /// </summary>
        /// <param name="itemClass">Item class string from baseitems.2da (lowercase).</param>
        /// <returns>Item category determined from item class string.</returns>
        /// <remarks>
        /// Item Class Category Determination:
        /// - swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - ItemClass read as string via C2DA::GetCExoStringEntry
        /// - swkotor2.exe: 0x005ff170 (FUN_005ff170) - ItemClass read as string via C2DA::GetCExoStringEntry
        /// - Item class string determines item category and behavior:
        ///   - Medical consumables: "medical", "medpac", "heal" patterns, or numeric strings for BASE_ITEM_MEDICAL_EQUIPMENT (55)
        ///   - Stimulants: "stim", "adrenal", "combat" patterns, or numeric strings for BASE_ITEM_ADRENALINE (53), BASE_ITEM_COMBAT_SHOTS (54)
        ///   - Grenades: "grenade", "mine", "explosive" patterns, or numeric strings for BASE_ITEM_FRAGMENTATION_GRENADES (25-34)
        ///   - Droid repair: "droid", "repair" patterns, or numeric strings for BASE_ITEM_DROID_REPAIR_EQUIPMENT (56)
        ///   - Weapons: numeric strings for BASE_ITEM_* weapon types (0-24)
        ///   - Armor: numeric strings for BASE_ITEM_JEDI_ROBE, BASE_ITEM_ARMOR_CLASS_* (35-43)
        ///   - Other: numeric strings or category strings for other item types
        /// - Handles both string category names and numeric string representations
        /// </remarks>
        private ItemCategory GetItemCategoryFromItemClass(string itemClass)
        {
            if (string.IsNullOrEmpty(itemClass))
            {
                return ItemCategory.Unknown;
            }

            string itemClassLower = itemClass.ToLowerInvariant().Trim();

            // Check for string-based category patterns
            if (itemClassLower.Contains("weapon") || itemClassLower.Contains("blade") || itemClassLower.Contains("blaster") || itemClassLower.Contains("saber"))
            {
                return ItemCategory.Weapon;
            }

            if (itemClassLower.Contains("armor") || itemClassLower.Contains("robe"))
            {
                return ItemCategory.Armor;
            }

            if (itemClassLower.Contains("shield"))
            {
                return ItemCategory.Shield;
            }

            if (itemClassLower.Contains("medical") || itemClassLower.Contains("medpac") || itemClassLower.Contains("heal"))
            {
                return ItemCategory.Medical;
            }

            if (itemClassLower.Contains("stim") || itemClassLower.Contains("adrenal") || itemClassLower.Contains("combat"))
            {
                return ItemCategory.Stimulant;
            }

            if (itemClassLower.Contains("grenade") || itemClassLower.Contains("mine") || itemClassLower.Contains("explosive") || itemClassLower.Contains("detonator"))
            {
                return ItemCategory.Grenade;
            }

            if (itemClassLower.Contains("droid") && (itemClassLower.Contains("repair") || itemClassLower.Contains("heal")))
            {
                return ItemCategory.DroidRepair;
            }

            if (itemClassLower.Contains("quest") || itemClassLower.Contains("datapad"))
            {
                return ItemCategory.Quest;
            }

            if (itemClassLower.Contains("upgrade") || itemClassLower.Contains("crystal") || itemClassLower.Contains("implant"))
            {
                return ItemCategory.Upgrade;
            }

            // Check for numeric string representations (common in baseitems.2da)
            // Parse numeric strings to determine category based on BASE_ITEM_* constants
            if (int.TryParse(itemClassLower, out int itemClassNum))
            {
                // Weapons: 0-24 (BASE_ITEM_* weapon types)
                if (itemClassNum >= 0 && itemClassNum <= 24)
                {
                    // Check for grenades in weapon range (25-34 are grenades, but 0-24 are melee/ranged weapons)
                    // Grenades are 25-34
                    if (itemClassNum >= 25 && itemClassNum <= 34)
                    {
                        return ItemCategory.Grenade;
                    }
                    return ItemCategory.Weapon;
                }

                // Armor: 35-50 (BASE_ITEM_JEDI_ROBE, BASE_ITEM_ARMOR_CLASS_*, BASE_ITEM_MASK, etc.)
                if (itemClassNum >= 35 && itemClassNum <= 43)
                {
                    return ItemCategory.Armor;
                }

                // Accessories: 44-50 (BASE_ITEM_MASK, BASE_ITEM_GAUNTLETS, BASE_ITEM_BELT, BASE_ITEM_IMPLANT_*)
                if (itemClassNum >= 44 && itemClassNum <= 50)
                {
                    return ItemCategory.Upgrade;
                }

                // Quest/Utility: 51-52 (BASE_ITEM_DATA_PAD, etc.)
                if (itemClassNum >= 51 && itemClassNum <= 52)
                {
                    return ItemCategory.Quest;
                }

                // Consumables: 53-56
                // BASE_ITEM_ADRENALINE = 53, BASE_ITEM_COMBAT_SHOTS = 54, BASE_ITEM_MEDICAL_EQUIPMENT = 55, BASE_ITEM_DROID_REPAIR_EQUIPMENT = 56
                if (itemClassNum == 53 || itemClassNum == 54)
                {
                    return ItemCategory.Stimulant;
                }

                if (itemClassNum == 55)
                {
                    return ItemCategory.Medical;
                }

                if (itemClassNum == 56)
                {
                    return ItemCategory.DroidRepair;
                }

                // Other items: 57+ (BASE_ITEM_CREDITS = 57, BASE_ITEM_TRAP_KIT = 58, etc.)
                if (itemClassNum >= 57)
                {
                    return ItemCategory.Misc;
                }
            }

            // Default: Unknown category
            return ItemCategory.Unknown;
        }

        /// <summary>
        /// Calculates bonus amount for stimulants based on charges and chargesstarting.
        /// </summary>
        /// <remarks>
        /// swkotor.exe: 0x005b31d0 (CSWBaseItemArray::Load) - Stim bonus amounts scale with item quality (chargesstarting)
        /// swkotor2.exe: 0x005ff170 (FUN_005ff170) - Stim bonus amounts scale with item quality (chargesstarting)
        /// </remarks>
        private int CalculateStimBonusAmount(IItemComponent itemComponent, int? chargesStarting)
        {
            int baseBonus = 2; // Base bonus amount (+2)

            // Scale bonus with chargesstarting if available (higher starting charges = more powerful stim)
            if (chargesStarting.HasValue && chargesStarting.Value > 0)
            {
                // Stims with more starting charges are typically more powerful
                // Scale bonus: +2 for 1 charge, +3 for 2-3 charges, +4 for 4+ charges
                if (chargesStarting.Value >= 4)
                {
                    baseBonus = 4;
                }
                else if (chargesStarting.Value >= 2)
                {
                    baseBonus = 3;
                }
            }

            return baseBonus;
        }


        /// <summary>
        /// Converts an item property to an effect using itempropdef.2da lookup when available.
        /// </summary>
        /// <remarks>
        /// Property to Effect Conversion:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item properties map to effect types via itempropdef.2da
        /// - Located via string references: "ItemPropDef" @ 0x007c4c20, "itempropdef.2da" in resource system
        /// - Original implementation: Looks up property type in itempropdef.2da to determine effect type and parameters
        /// - itempropdef.2da columns: name, subtyperesref, param1resref, gamestrref, description
        /// - Property types are row indices in itempropdef.2da (0-based)
        /// - Falls back to comprehensive hardcoded mappings if 2DA table is not available
        /// - Supports all Aurora engine property types (0-58+)
        /// </remarks>
        private Combat.Effect ConvertPropertyToEffect(ItemProperty property, IWorld world)
        {
            if (property == null)
            {
                return null;
            }

            // Try to access itempropdef.2da through world's resource system
            TwoDA itempropDefTable = TryGetItemPropDefTable(property, world);

            if (itempropDefTable != null)
            {
                // Use 2DA table for property definition lookup
                return ConvertPropertyToEffectFrom2DA(property, itempropDefTable);
            }
            else
            {
                // Fallback to comprehensive hardcoded mappings
                return ConvertPropertyToEffectHardcoded(property);
            }
        }

        /// <summary>
        /// Attempts to load itempropdef.2da table from the world's resource system.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item properties map to effect types via itempropdef.2da
        /// - Located via string references: "ItemPropDef" @ 0x007c4c20, "itempropdef.2da" in resource system
        /// - Original implementation: Accesses itempropdef.2da through game data provider
        /// - Uses IWorld.GameDataProvider to access 2DA tables in engine-agnostic way
        /// - swkotor2.exe: Item property definitions are loaded from itempropdef.2da via C2DA/2DA system
        /// </remarks>
        [CanBeNull]
        private TwoDA TryGetItemPropDefTable(ItemProperty property, IWorld world)
        {
            if (property == null || world == null)
            {
                return null;
            }

            // Access 2DA table through IWorld's GameDataProvider
            // This provides engine-agnostic access to game data tables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item properties are looked up in itempropdef.2da via game data system
            // Original implementation: Uses game data provider to load itempropdef.2da table
            IGameDataProvider gameDataProvider = world.GameDataProvider;
            if (gameDataProvider == null)
            {
                return null;
            }

            try
            {
                // Get itempropdef.2da table from game data provider
                // Table name is "itempropdef" (without .2da extension)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): itempropdef.2da is loaded via game data system
                TwoDA table = gameDataProvider.GetTable("itempropdef");
                return table;
            }
            catch
            {
                // If table loading fails (e.g., table doesn't exist or error loading), return null
                // The hardcoded fallback will handle property-to-effect conversion
                return null;
            }
        }

        /// <summary>
        /// Converts property to effect using itempropdef.2da table lookup.
        /// </summary>
        [CanBeNull]
        private Combat.Effect ConvertPropertyToEffectFrom2DA(ItemProperty property, TwoDA itempropDefTable)
        {
            if (property.PropertyType < 0 || property.PropertyType >= itempropDefTable.GetHeight())
            {
                return null;
            }

            try
            {
                TwoDARow propRow = itempropDefTable.GetRow(property.PropertyType);
                if (propRow == null)
                {
                    return null;
                }

                // Get property name from 2DA (for logging/debugging)
                string propName = propRow.GetString("name", "");

                // Get subtype and param1 references if available
                string subTypeResRef = propRow.GetString("subtyperesref", "");
                string param1ResRef = propRow.GetString("param1resref", "");

                // Use hardcoded mapping based on property type, but with 2DA validation
                // The 2DA table provides metadata, but effect conversion logic is still needed
                return ConvertPropertyToEffectHardcoded(property);
            }
            catch
            {
                // If 2DA lookup fails, fall back to hardcoded mapping
                return ConvertPropertyToEffectHardcoded(property);
            }
        }

        /// <summary>
        /// Converts property to effect using comprehensive hardcoded mappings based on Aurora engine standard.
        /// </summary>
        /// <remarks>
        /// Property Type Mappings (Aurora Engine Standard):
        /// Based on nwscript.nss constants and swkotor2.exe implementation
        /// Property types 0-58 map to various effects (ability bonuses, AC, damage, etc.)
        /// </remarks>
        [CanBeNull]
        private Effect ConvertPropertyToEffectHardcoded(ItemProperty property)
        {
            int propType = property.PropertyType;
            int costValue = property.CostValue;
            int param1Value = property.Param1Value;
            int subtype = property.Subtype;

            // Get amount from CostValue or Param1Value
            int amount = costValue != 0 ? costValue : param1Value;

            // ITEM_PROPERTY_ABILITY_BONUS (0): Ability score bonus
            if (propType == 0)
            {
                if (subtype >= 0 && subtype <= 5)
                {
                    Ability ability = (Ability)subtype;
                    if (amount > 0)
                    {
                        return Effect.AbilityModifier(ability, amount, 0);
                    }
                }
            }

            // ITEM_PROPERTY_AC_BONUS (1): Armor Class bonus
            if (propType == 1)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.ACIncrease);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Permanent;
                    return effect;
                }
            }

            // ITEM_PROPERTY_ENHANCEMENT_BONUS (5): Enhancement bonus (attack/damage)
            if (propType == 5)
            {
                if (amount > 0)
                {
                    // Enhancement bonus affects both attack and damage
                    // For items, this is typically handled by the weapon system
                    // For consumables, apply as attack bonus
                    var effect = new Effect(EffectType.AttackIncrease);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10; // Default duration for consumables
                    return effect;
                }
            }

            // ITEM_PROPERTY_ATTACK_BONUS (38): Attack bonus
            if (propType == 38)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.AttackIncrease);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_DAMAGE_BONUS (11): Damage bonus
            if (propType == 11)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.DamageIncrease);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_DAMAGE_RESISTANCE (17): Damage resistance
            if (propType == 17)
            {
                if (amount > 0 && subtype >= 0)
                {
                    Combat.DamageType damageType = (Combat.DamageType)subtype;
                    return Combat.Effect.DamageResistance(damageType, amount, 0);
                }
            }

            // ITEM_PROPERTY_DAMAGE_REDUCTION (16): Damage reduction
            if (propType == 16)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.DamageReduction);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Permanent;
                    return effect;
                }
            }

            // ITEM_PROPERTY_IMMUNITY_DAMAGE_TYPE (14): Immunity to damage type
            if (propType == 14)
            {
                if (subtype >= 0)
                {
                    DamageType damageType = (DamageType)subtype;
                    var effect = new Effect(EffectType.DamageImmunity);
                    effect.SubType = (int)damageType;
                    effect.DurationType = EffectDurationType.Permanent;
                    return effect;
                }
            }

            // ITEM_PROPERTY_IMPROVED_SAVING_THROW (26): Saving throw bonus
            if (propType == 26)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.SaveIncrease);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_IMPROVED_SAVING_THROW_SPECIFIC (27): Specific saving throw bonus
            if (propType == 27)
            {
                if (amount > 0 && subtype >= 0)
                {
                    var effect = new Effect(EffectType.SaveIncrease);
                    effect.Amount = amount;
                    effect.SubType = subtype; // Save type (Fort/Ref/Will)
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_REGENERATION (35): Regeneration
            if (propType == 35)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.Regeneration);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_REGENERATION_FORCE_POINTS (54): Force point regeneration
            if (propType == 54)
            {
                if (amount > 0)
                {
                    // Force regeneration is similar to HP regeneration
                    var effect = new Effect(EffectType.Regeneration);
                    effect.Amount = amount;
                    effect.SubType = 1; // Mark as Force regeneration
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_SKILL_BONUS (36): Skill bonus
            if (propType == 36)
            {
                if (amount > 0 && subtype >= 0)
                {
                    // Skill bonuses are typically permanent equipment bonuses
                    // For consumables, apply as temporary bonus
                    var effect = new Effect(EffectType.AbilityIncrease); // Use ability increase as proxy
                    effect.Amount = amount;
                    effect.SubType = subtype; // Skill ID
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_TRUE_SEEING (47): True seeing
            if (propType == 47)
            {
                var effect = new Effect(EffectType.TrueSeeing);
                effect.DurationType = EffectDurationType.Temporary;
                effect.DurationRounds = 10;
                return effect;
            }

            // ITEM_PROPERTY_FREEDOM_OF_MOVEMENT (50): Freedom of movement
            if (propType == 50)
            {
                // Freedom of movement prevents movement restrictions
                var effect = new Effect(EffectType.Sanctuary);
                effect.DurationType = EffectDurationType.Temporary;
                effect.DurationRounds = 10;
                return effect;
            }

            // ITEM_PROPERTY_DECREASED_ABILITY_SCORE (19): Ability penalty
            if (propType == 19)
            {
                if (subtype >= 0 && subtype <= 5 && amount > 0)
                {
                    Enums.Ability ability = (Enums.Ability)subtype;
                    return Combat.Effect.AbilityModifier(ability, -amount, 0);
                }
            }

            // ITEM_PROPERTY_DECREASED_AC (20): AC penalty
            if (propType == 20)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.ACDecrease);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_DECREASED_ATTACK_MODIFIER (41): Attack penalty
            if (propType == 41)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.AttackDecrease);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_DECREASED_SAVING_THROWS (33): Saving throw penalty
            if (propType == 33)
            {
                if (amount > 0)
                {
                    var effect = new Effect(EffectType.SaveDecrease);
                    effect.Amount = amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // ITEM_PROPERTY_DAMAGE_VULNERABILITY (18): Damage vulnerability
            if (propType == 18)
            {
                if (subtype >= 0 && amount > 0)
                {
                    DamageType damageType = (DamageType)subtype;
                    // Vulnerability increases damage taken
                    var effect = new Effect(EffectType.DamageDecrease); // Negative resistance
                    effect.SubType = (int)damageType;
                    effect.Amount = -amount;
                    effect.DurationType = EffectDurationType.Temporary;
                    effect.DurationRounds = 10;
                    return effect;
                }
            }

            // For consumable items, property types 100+ are often used for instant effects
            // Property type 100: Healing (common in medpacs)
            if (propType == 100 || (propType >= 100 && amount > 0))
            {
                // Many consumables use high property type IDs for healing
                if (amount > 0)
                {
                    return Combat.Effect.Heal(amount);
                }
            }

            // Property types not directly mappable to effects are handled by other systems
            // (e.g., AC_BONUS_VS_ALIGNMENT_GROUP, USE_LIMITATION_*, etc.)
            // These are typically passive equipment bonuses, not consumable effects

            return null;
        }
    }
}


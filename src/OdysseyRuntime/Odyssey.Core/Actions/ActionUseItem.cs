using System;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to use an item (consumables, usable items, etc.).
    /// </summary>
    /// <remarks>
    /// Use Item Action:
    /// - Based on swkotor2.exe item usage system
    /// - Located via string references: "OnUsed" @ 0x007c1f70, "i_useitemm" @ 0x007ccde0, "BTN_USEITEM" @ 0x007d1080
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0, "Mod_OnActvtItem" @ 0x007be7f4
    /// - Item usage: Items can be used from inventory or quick slots
    /// - Consumable items: Items with charges (potions, grenades, etc.) consume a charge when used
    /// - Item effects: Items can have properties that apply effects (healing, damage, status effects, etc.)
    /// - OnUsed script: Items can have OnUsed script that executes when item is used
    /// - Original implementation: Uses item, applies effects, consumes charge if applicable, fires OnUsed script
    /// - Items with 0 charges are removed from inventory after use
    /// - Based on swkotor2.exe: Item usage system handles consumables, usable items, and item effects
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
                // Based on swkotor2.exe: Item usage applies item properties as effects
                // Located via string references: Item properties can have effects (healing, damage, status effects, etc.)
                // Original implementation: Item properties are converted to effects and applied to target
                ApplyItemEffects(actor, target, item, itemComponent);

                // Consume charge if item has charges
                // Based on swkotor2.exe: Consumable items have charges that are consumed on use
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
                // Based on swkotor2.exe: OnUsed script fires when item is used
                // Located via string references: "OnUsed" @ 0x007c1f70, "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0
                // Original implementation: OnUsed script executes on item entity with actor as triggerer
                IEventBus eventBus = actor.World?.EventBus;
                if (eventBus != null)
                {
                    eventBus.FireScriptEvent(item, ScriptEvent.OnUsed, actor);
                }

                // Fire OnInventoryDisturbed script event
                // Based on swkotor2.exe: ON_INVENTORY_DISTURBED fires when items are used/consumed
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
        /// - Based on swkotor2.exe item effect system
        /// - Original implementation: Item properties are converted to effects and applied via EffectSystem
        /// - Item properties can have various effects: healing, damage, status effects, ability bonuses, etc.
        /// - Property types: Common property IDs map to effect types (healing, damage, ability bonuses, etc.)
        /// - For consumable items: Apply effects based on item properties and base item type
        /// - Full implementation would look up itempropdef.2da for complete property definitions
        /// </remarks>
        private void ApplyItemEffects(IEntity caster, IEntity target, IEntity item, IItemComponent itemComponent)
        {
            if (caster.World == null || caster.World.EffectSystem == null)
            {
                return;
            }

            EffectSystem effectSystem = caster.World.EffectSystem;

            // Apply item properties as effects
            // Based on swkotor2.exe: Item properties are converted to effects
            // Located via string references: Item property types map to effect types
            // Original implementation: Each property type has corresponding effect type
            // Common property types (from Aurora engine standard):
            // - Property type 1-6: Ability bonuses (STR, DEX, CON, INT, WIS, CHA)
            // - Property type 100+: Special effects (healing, damage, etc.)
            foreach (ItemProperty property in itemComponent.Properties)
            {
                // Convert property type to effect based on common Aurora property IDs
                // This is a simplified mapping - full implementation would use itempropdef.2da
                Effect effect = ConvertPropertyToEffect(property);
                if (effect != null)
                {
                    effectSystem.ApplyEffect(target, effect, caster);
                }
            }

            // For consumable items, apply basic healing if no properties found
            // This handles common consumables like medpacs that may not have explicit properties
            // Full implementation would check baseitems.2da for item class and apply appropriate effects
            if (itemComponent.Properties.Count == 0 && itemComponent.Charges > 0)
            {
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
        }

        /// <summary>
        /// Converts an item property to an effect.
        /// </summary>
        /// <remarks>
        /// Property to Effect Conversion:
        /// - Based on swkotor2.exe: Item properties map to effect types
        /// - Common property types (simplified mapping):
        ///   - Property type 1-6: Ability bonuses (STR, DEX, CON, INT, WIS, CHA)
        ///   - Property type 100+: Special effects (healing, damage, etc.)
        /// - Full implementation would use itempropdef.2da to determine exact effect type
        /// </remarks>
        private Combat.Effect ConvertPropertyToEffect(ItemProperty property)
        {
            if (property == null)
            {
                return null;
            }

            // Common property type mappings (simplified)
            // Property type 1-6: Ability bonuses
            if (property.PropertyType >= 1 && property.PropertyType <= 6)
            {
                // Map property type to ability: 1=STR, 2=DEX, 3=CON, 4=INT, 5=WIS, 6=CHA
                Enums.Ability ability = (Enums.Ability)(property.PropertyType - 1);
                int amount = property.CostValue; // Use CostValue as the bonus amount
                if (amount == 0)
                {
                    amount = property.Param1Value; // Fallback to Param1Value
                }
                if (amount > 0)
                {
                    return Combat.Effect.AbilityModifier(ability, amount, 0); // Permanent ability bonus
                }
            }

            // Property type 100+: Special effects
            // Property type 100: Healing
            if (property.PropertyType == 100)
            {
                int healAmount = property.CostValue;
                if (healAmount == 0)
                {
                    healAmount = property.Param1Value;
                }
                if (healAmount > 0)
                {
                    return Combat.Effect.Heal(healAmount);
                }
            }

            // Property type 101: Damage
            if (property.PropertyType == 101)
            {
                int damageAmount = property.CostValue;
                if (damageAmount == 0)
                {
                    damageAmount = property.Param1Value;
                }
                if (damageAmount > 0)
                {
                    // Create damage effect (would need Damage effect type)
                    // For now, return null - damage effects need target validation
                    return null;
                }
            }

            // Additional property types can be added here
            // Full implementation would use itempropdef.2da to map all property types

            return null;
        }
    }
}


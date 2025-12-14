using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to unequip an item from a specific inventory slot.
    /// </summary>
    public class ActionUnequipItem : ActionBase
    {
        private readonly int _inventorySlot;

        public ActionUnequipItem(int inventorySlot)
            : base(ActionType.UnequipItem)
        {
            _inventorySlot = inventorySlot;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            if (actor == null || !actor.IsValid)
            {
                return ActionStatus.Failed;
            }

            IInventoryComponent inventory = actor.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return ActionStatus.Failed;
            }

            // Clear the slot (unequip)
            inventory.SetItemInSlot(_inventorySlot, null);

            return ActionStatus.Complete;
        }
    }
}


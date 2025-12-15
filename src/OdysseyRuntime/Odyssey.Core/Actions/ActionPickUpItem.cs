using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to pick up an item from the world.
    /// </summary>
    /// <remarks>
    /// Pick Up Item Action:
    /// - Based on swkotor2.exe ActionPickUpItem NWScript function
    /// - Original implementation: Moves entity to item location, then picks up item into inventory
    /// - Pickup range: ~1.5 units (PickupRange)
    /// - Item removed from world after being picked up
    /// - Action fails if inventory is full
    /// </remarks>
    public class ActionPickUpItem : ActionBase
    {
        private readonly uint _itemObjectId;
        private bool _approached;
        private const float PickupRange = 1.5f;

        public ActionPickUpItem(uint itemObjectId)
            : base(ActionType.TakeItem)
        {
            _itemObjectId = itemObjectId;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            if (actor == null || !actor.IsValid)
            {
                return ActionStatus.Failed;
            }

            IEntity item = actor.World.GetEntity(_itemObjectId);
            if (item == null || !item.IsValid || item.ObjectType != ObjectType.Item)
            {
                return ActionStatus.Failed;
            }

            ITransformComponent actorTransform = actor.GetComponent<ITransformComponent>();
            ITransformComponent itemTransform = item.GetComponent<ITransformComponent>();
            if (actorTransform == null || itemTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toItem = itemTransform.Position - actorTransform.Position;
            toItem.Y = 0;
            float distance = toItem.Length();

            // Move towards item if not in range
            if (distance > PickupRange && !_approached)
            {
                IStatsComponent stats = actor.GetComponent<IStatsComponent>();
                float speed = stats != null ? stats.WalkSpeed : 2.5f;

                Vector3 direction = Vector3.Normalize(toItem);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - PickupRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                actorTransform.Position += direction * moveDistance;
                // Y-up system: Atan2(Y, X) for 2D plane facing
                actorTransform.Facing = (float)Math.Atan2(direction.Y, direction.X);

                return ActionStatus.InProgress;
            }

            _approached = true;

            // Pick up the item
            IInventoryComponent inventory = actor.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return ActionStatus.Failed;
            }

            // Add item to inventory
            if (inventory.AddItem(item))
            {
                // Remove item from world (or just hide it)
                // In KOTOR, items are typically removed from the area when picked up
                actor.World.DestroyEntity(item.ObjectId);
                return ActionStatus.Complete;
            }

            return ActionStatus.Failed; // Inventory full
        }
    }
}


using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to open a door.
    /// </summary>
    public class ActionOpenDoor : ActionBase
    {
        private readonly uint _doorObjectId;
        private bool _approached;
        private const float InteractRange = 2.0f;

        public ActionOpenDoor(uint doorObjectId)
            : base(ActionType.OpenDoor)
        {
            _doorObjectId = doorObjectId;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            var transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Get door entity
            var door = actor.World.GetEntity(_doorObjectId);
            if (door == null || !door.IsValid)
            {
                return ActionStatus.Failed;
            }

            var doorTransform = door.GetComponent<ITransformComponent>();
            if (doorTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = doorTransform.Position - transform.Position;
            toTarget.Y = 0;
            float distance = toTarget.Length();

            // Move towards door if not in range
            if (distance > InteractRange && !_approached)
            {
                var stats = actor.GetComponent<IStatsComponent>();
                float speed = stats != null ? stats.WalkSpeed : 2.5f;

                Vector3 direction = Vector3.Normalize(toTarget);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - InteractRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                transform.Position += direction * moveDistance;
                transform.Facing = (float)Math.Atan2(direction.X, direction.Z);

                return ActionStatus.InProgress;
            }

            _approached = true;

            // Open the door
            var doorState = door.GetComponent<IDoorComponent>();
            if (doorState != null)
            {
                if (doorState.IsLocked)
                {
                    // Fire locked door event
                    var eventBus = actor.World.EventBus;
                    if (eventBus != null)
                    {
                        eventBus.Publish(new DoorLockedEvent { Actor = actor, Door = door });
                    }
                    return ActionStatus.Failed;
                }

                doorState.IsOpen = true;

                // Fire opened event
                var eventBus2 = actor.World.EventBus;
                if (eventBus2 != null)
                {
                    eventBus2.Publish(new DoorOpenedEvent { Actor = actor, Door = door });
                }
            }

            return ActionStatus.Complete;
        }
    }

    /// <summary>
    /// Action to close a door.
    /// </summary>
    public class ActionCloseDoor : ActionBase
    {
        private readonly uint _doorObjectId;
        private bool _approached;
        private const float InteractRange = 2.0f;

        public ActionCloseDoor(uint doorObjectId)
            : base(ActionType.CloseDoor)
        {
            _doorObjectId = doorObjectId;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            var transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            var door = actor.World.GetEntity(_doorObjectId);
            if (door == null || !door.IsValid)
            {
                return ActionStatus.Failed;
            }

            var doorTransform = door.GetComponent<ITransformComponent>();
            if (doorTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = doorTransform.Position - transform.Position;
            toTarget.Y = 0;
            float distance = toTarget.Length();

            if (distance > InteractRange && !_approached)
            {
                var stats = actor.GetComponent<IStatsComponent>();
                float speed = stats != null ? stats.WalkSpeed : 2.5f;

                Vector3 direction = Vector3.Normalize(toTarget);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - InteractRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                transform.Position += direction * moveDistance;
                transform.Facing = (float)Math.Atan2(direction.X, direction.Z);

                return ActionStatus.InProgress;
            }

            _approached = true;

            var doorState = door.GetComponent<IDoorComponent>();
            if (doorState != null)
            {
                doorState.IsOpen = false;

                var eventBus = actor.World.EventBus;
                if (eventBus != null)
                {
                    eventBus.Publish(new DoorClosedEvent { Actor = actor, Door = door });
                }
            }

            return ActionStatus.Complete;
        }
    }

    /// <summary>
    /// Event fired when a door is opened.
    /// </summary>
    public class DoorOpenedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Door { get; set; }
        public IEntity Entity { get { return Door; } }
    }

    /// <summary>
    /// Event fired when a door is closed.
    /// </summary>
    public class DoorClosedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Door { get; set; }
        public IEntity Entity { get { return Door; } }
    }

    /// <summary>
    /// Event fired when trying to open a locked door.
    /// </summary>
    public class DoorLockedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Door { get; set; }
        public IEntity Entity { get { return Door; } }
    }
}


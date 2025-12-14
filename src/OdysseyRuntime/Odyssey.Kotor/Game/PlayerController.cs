using System;
using System.Numerics;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Actions;
using Odyssey.Core.Enums;

namespace Odyssey.Kotor.Game
{
    /// <summary>
    /// Handles player input and movement.
    /// TODO: Implement full click-to-move with pathfinding
    /// TODO: Implement object interaction
    /// TODO: Implement combat mode
    /// </summary>
    public class PlayerController
    {
        private readonly IEntity _playerEntity;
        private readonly IWorld _world;
        private readonly float _moveSpeed = 5.0f;
        private readonly float _runSpeed = 8.0f;

        private Vector3 _targetPosition;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future pathfinding logic
        private bool _hasTarget;
#pragma warning restore CS0414
        private bool _isRunning;

        // TODO: Action queue integration
        // TODO: Selection system
        // TODO: Party management

        public PlayerController(IEntity playerEntity, IWorld world)
        {
            _playerEntity = playerEntity;
            _world = world;
        }

        /// <summary>
        /// Gets the player entity.
        /// </summary>
        public IEntity Player { get { return _playerEntity; } }

        /// <summary>
        /// Gets or sets whether the player is running.
        /// </summary>
        public bool IsRunning
        {
            get { return _isRunning; }
            set { _isRunning = value; }
        }

        /// <summary>
        /// Handle a click at a world position.
        /// </summary>
        public void HandleClick(Vector3 worldPosition, bool isRightClick)
        {
            if (_playerEntity == null)
            {
                return;
            }

            var area = _world.CurrentArea;
            if (area == null)
            {
                return;
            }

            // Check if clicking on an object
            var clickedEntity = FindEntityAt(worldPosition);
            if (clickedEntity != null)
            {
                HandleObjectClick(clickedEntity, isRightClick);
                return;
            }

            // Move to location
            MoveToLocation(worldPosition);
        }

        /// <summary>
        /// Move to a target location.
        /// </summary>
        public void MoveToLocation(Vector3 position)
        {
            // TODO: Use pathfinding via NavigationMesh
            // TODO: Project position onto walkmesh

            _targetPosition = position;
            _hasTarget = true;

            // Queue a move action
            var actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Clear();
                actionQueue.Add(new ActionMoveToLocation(position, _isRunning));
            }

            Console.WriteLine("[PlayerController] Moving to: " + position);
        }

        /// <summary>
        /// Handle clicking on an object.
        /// </summary>
        private void HandleObjectClick(IEntity target, bool isRightClick)
        {
            if (target == null)
            {
                return;
            }

            Console.WriteLine("[PlayerController] Clicked on: " + target.Tag + " (type: " + target.ObjectType + ")");

            switch (target.ObjectType)
            {
                case ObjectType.Creature:
                    HandleCreatureClick(target, isRightClick);
                    break;

                case ObjectType.Door:
                    HandleDoorClick(target);
                    break;

                case ObjectType.Placeable:
                    HandlePlaceableClick(target);
                    break;

                case ObjectType.Trigger:
                    // Triggers are usually invisible, but clicking might do something
                    break;

                default:
                    // Default behavior - move to object location
                    MoveToEntity(target);
                    break;
            }
        }

        private void HandleCreatureClick(IEntity creature, bool isRightClick)
        {
            // TODO: Check if hostile
            // TODO: Start dialogue if friendly
            // TODO: Attack if hostile

            // For now, just move toward the creature
            MoveToEntity(creature);
        }

        private void HandleDoorClick(IEntity door)
        {
            var doorComponent = door.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                if (doorComponent.IsLocked)
                {
                    // TODO: Try to unlock or bash
                    Console.WriteLine("[PlayerController] Door is locked");
                }
                else if (!doorComponent.IsOpen)
                {
                    // Queue open door action
                    var actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
                    if (actionQueue != null)
                    {
                        actionQueue.Clear();
                        actionQueue.Add(new ActionOpenDoor(door.ObjectId));
                    }
                }
            }
            else
            {
                // No door component, just move to it
                MoveToEntity(door);
            }
        }

        private void HandlePlaceableClick(IEntity placeable)
        {
            var placeableComponent = placeable.GetComponent<IPlaceableComponent>();
            if (placeableComponent != null && placeableComponent.IsUseable)
            {
                // TODO: Queue use action
                Console.WriteLine("[PlayerController] FIXME: Use placeable not implemented");
            }

            // Move to the placeable
            MoveToEntity(placeable);
        }

        private void MoveToEntity(IEntity target)
        {
            var targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform != null)
            {
                MoveToLocation(targetTransform.Position);
            }
        }

        /// <summary>
        /// Find an entity at a world position.
        /// TODO: Use proper spatial queries
        /// </summary>
        private IEntity FindEntityAt(Vector3 position)
        {
            var area = _world.CurrentArea;
            if (area == null)
            {
                return null;
            }

            float closestDistance = 2.0f; // Click radius
            IEntity closest = null;

            // Check all interactable entities
            foreach (var entity in area.Creatures)
            {
                float dist = CheckEntityDistance(entity, position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = entity;
                }
            }

            foreach (var entity in area.Placeables)
            {
                float dist = CheckEntityDistance(entity, position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = entity;
                }
            }

            foreach (var entity in area.Doors)
            {
                float dist = CheckEntityDistance(entity, position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = entity;
                }
            }

            return closest;
        }

        private float CheckEntityDistance(IEntity entity, Vector3 position)
        {
            var transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return float.MaxValue;
            }

            return Vector3.Distance(transform.Position, position);
        }

        /// <summary>
        /// Update the player controller each frame.
        /// </summary>
        public void Update(float deltaTime)
        {
            // Movement is handled by action queue
            // This could be used for direct movement controls (WASD) if desired

            // TODO: Update selection highlight
            // TODO: Update targeting cursor
        }

        /// <summary>
        /// Handle keyboard movement (WASD direct control).
        /// </summary>
        public void HandleDirectMovement(Vector3 direction, float deltaTime)
        {
            if (_playerEntity == null)
            {
                return;
            }

            if (direction == Vector3.Zero)
            {
                return;
            }

            var transform = _playerEntity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            float speed = _isRunning ? _runSpeed : _moveSpeed;
            Vector3 movement = Vector3.Normalize(direction) * speed * deltaTime;
            Vector3 newPosition = transform.Position + movement;

            // TODO: Check walkmesh validity
            // TODO: Update facing direction

            transform.Position = newPosition;
        }

        /// <summary>
        /// Start a dialogue with an NPC.
        /// </summary>
        public void StartDialogue(IEntity npc)
        {
            // TODO: Implement dialogue system integration
            Console.WriteLine("[PlayerController] FIXME: Dialogue not implemented with: " + npc.Tag);
        }
    }
}


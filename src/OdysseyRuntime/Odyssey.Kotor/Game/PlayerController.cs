using System;
using System.Numerics;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Actions;
using Odyssey.Core.Enums;
using Odyssey.Kotor.Systems;
using Odyssey.Kotor.Dialogue;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.Game
{
    /// <summary>
    /// Handles player input and movement.
    /// </summary>
    /// <remarks>
    /// Player Controller:
    /// - Based on swkotor2.exe player input/movement system
    /// - Original implementation: Click-to-move with pathfinding, object selection, party control
    /// - Click-to-move: Click world position -> pathfind -> queue ActionMoveToLocation
    /// - Object interaction: Click entity -> queue ActionUseObject or ActionMoveToObject
    /// - Right-click: Context menu or alternative action
    /// - Party members follow leader, respond to same commands
    /// - Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 (player input handling)
    /// </remarks>
    public class PlayerController
    {
        private readonly IEntity _playerEntity;
        private readonly IWorld _world;
        private readonly FactionManager _factionManager;
        private readonly DialogueManager _dialogueManager;
        private readonly float _moveSpeed = 5.0f;
        private readonly float _runSpeed = 8.0f;

        private Vector3 _targetPosition;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future pathfinding logic
        private bool _hasTarget;
#pragma warning restore CS0414
        private bool _isRunning;

        public PlayerController(IEntity playerEntity, IWorld world, FactionManager factionManager = null, DialogueManager dialogueManager = null)
        {
            _playerEntity = playerEntity;
            _world = world;
            _factionManager = factionManager;
            _dialogueManager = dialogueManager;
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

            IArea area = _world.CurrentArea;
            if (area == null)
            {
                return;
            }

            // Check if clicking on an object
            IEntity clickedEntity = FindEntityAt(worldPosition);
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
            IArea area = _world.CurrentArea;
            if (area == null || area.NavigationMesh == null)
            {
                Console.WriteLine("[PlayerController] No navigation mesh available");
                return;
            }

            // Project position onto walkmesh
            Vector3? projectedPosition = area.NavigationMesh.GetNearestPoint(position);
            if (projectedPosition == null)
            {
                Console.WriteLine("[PlayerController] Could not project position onto walkmesh");
                return;
            }

            _targetPosition = projectedPosition.Value;
            _hasTarget = true;

            // Queue a move action (ActionMoveToLocation uses pathfinding internally)
            IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Clear();
                actionQueue.Add(new ActionMoveToLocation(_targetPosition, _isRunning));
            }

            Console.WriteLine("[PlayerController] Moving to: " + _targetPosition);
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

        /// <summary>
        /// Handles clicking on a creature.
        /// </summary>
        /// <remarks>
        /// Creature Click Handling:
        /// - Based on swkotor2.exe creature interaction system
        /// - Original implementation: Check hostility -> attack if hostile, dialogue if friendly
        /// - Hostility check via FactionManager.IsHostile
        /// - Dialogue started via DialogueManager if creature has dialogue ResRef
        /// </remarks>
        private void HandleCreatureClick(IEntity creature, bool isRightClick)
        {
            if (creature == null || _playerEntity == null)
            {
                return;
            }

            // Check if hostile
            bool isHostile = false;
            if (_factionManager != null)
            {
                isHostile = _factionManager.IsHostile(_playerEntity, creature);
            }

            if (isHostile)
            {
                // Attack if hostile
                IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
                if (actionQueue != null)
                {
                    actionQueue.Clear();
                    actionQueue.Add(new ActionAttack(creature.ObjectId));
                }
            }
            else
            {
                // Start dialogue if friendly
                CreatureComponent creatureComp = creature.GetComponent<CreatureComponent>();
                if (creatureComp != null && !string.IsNullOrEmpty(creatureComp.DialogueResRef))
                {
                    if (_dialogueManager != null)
                    {
                        _dialogueManager.StartConversation(creatureComp.DialogueResRef, creature, _playerEntity);
                    }
                    else
                    {
                        // Fallback: just move to creature if dialogue manager not available
                        MoveToEntity(creature);
                    }
                }
                else
                {
                    // No dialogue, just move to creature
                    MoveToEntity(creature);
                }
            }
        }

        /// <summary>
        /// Handles clicking on a door.
        /// </summary>
        /// <remarks>
        /// Door Click Handling:
        /// - Based on swkotor2.exe door interaction system
        /// - Original implementation: Check lock -> try key -> try lockpicking -> try bashing -> open
        /// - Key check: HasItemByTag(KeyName)
        /// - Lockpicking: Security skill check vs LockDC
        /// - Bashing: Attack door until destroyed (damage - hardness)
        /// </remarks>
        private void HandleDoorClick(IEntity door)
        {
            if (door == null || _playerEntity == null)
            {
                return;
            }

            IDoorComponent doorComponent = door.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                if (doorComponent.IsLocked)
                {
                    // Try to unlock the door
                    bool unlocked = TryUnlockDoor(doorComponent);
                    if (!unlocked)
                    {
                        // Could not unlock - could try bashing or show message
                        Console.WriteLine("[PlayerController] Door is locked and cannot be unlocked");
                        return;
                    }
                }

                // Door is now unlocked (or was never locked), try to open it
                if (!doorComponent.IsOpen)
                {
                    // Queue open door action
                    IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
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

        /// <summary>
        /// Attempts to unlock a door using key, lockpicking, or bashing.
        /// </summary>
        /// <remarks>
        /// Door Unlocking Logic:
        /// - Based on swkotor2.exe door unlocking system
        /// - Priority: 1) Key check, 2) Lockpicking (Security skill), 3) Bashing (if no key required)
        /// - Key check: Inventory.HasItemByTag(KeyName)
        /// - Lockpicking: Security skill + d20 vs LockDC
        /// - Bashing: Attack door (damage - hardness) until destroyed
        /// </remarks>
        private bool TryUnlockDoor(IDoorComponent doorComponent)
        {
            if (doorComponent == null || !doorComponent.IsLocked)
            {
                return true; // Already unlocked
            }

            // 1. Try key first
            if (doorComponent.KeyRequired && !string.IsNullOrEmpty(doorComponent.KeyName))
            {
                IInventoryComponent inventory = _playerEntity.GetComponent<IInventoryComponent>();
                if (inventory != null && inventory.HasItemByTag(doorComponent.KeyName))
                {
                    doorComponent.Unlock();
                    Console.WriteLine("[PlayerController] Door unlocked with key: " + doorComponent.KeyName);
                    return true;
                }
            }

            // 2. Try lockpicking (Security skill check)
            if (doorComponent.LockableByScript && doorComponent.LockDC > 0)
            {
                // Get player's Security skill
                IStatsComponent stats = _playerEntity.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    // Get Security skill from stats component
                    IStatsComponent stats = _playerEntity.GetComponent<IStatsComponent>();
                    Random random = new Random();
                    int roll = random.Next(1, 21);
                    int securitySkill = 0;
                    if (stats != null)
                    {
                        // SKILL_SECURITY = 6
                        securitySkill = stats.GetSkillRank(6);
                    }
                    int total = roll + securitySkill;

                    if (total >= doorComponent.LockDC)
                    {
                        doorComponent.Unlock();
                        Console.WriteLine("[PlayerController] Door lockpicked (roll: " + roll + " + skill: " + securitySkill + " >= DC: " + doorComponent.LockDC + ")");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("[PlayerController] Lockpicking failed (roll: " + roll + " + skill: " + securitySkill + " < DC: " + doorComponent.LockDC + ")");
                    }
                }
            }

            // 3. If no key required and not lockable, could try bashing
            // For now, return false - bashing would be a separate action
            return false;
        }

        /// <summary>
        /// Handles clicking on a placeable.
        /// </summary>
        /// <remarks>
        /// Placeable Click Handling:
        /// - Based on swkotor2.exe placeable interaction system
        /// - Original implementation: Queue ActionUseObject if placeable is useable
        /// - ActionUseObject moves to use point and triggers OnUsed script
        /// </remarks>
        private void HandlePlaceableClick(IEntity placeable)
        {
            if (placeable == null || _playerEntity == null)
            {
                return;
            }

            IPlaceableComponent placeableComponent = placeable.GetComponent<IPlaceableComponent>();
            if (placeableComponent != null && placeableComponent.IsUseable)
            {
                // Queue use action
                IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
                if (actionQueue != null)
                {
                    actionQueue.Clear();
                    actionQueue.Add(new ActionUseObject(placeable.ObjectId));
                }
            }
            else
            {
                // Not useable, just move to it
                MoveToEntity(placeable);
            }
        }

        private void MoveToEntity(IEntity target)
        {
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
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
            IArea area = _world.CurrentArea;
            if (area == null)
            {
                return null;
            }

            float closestDistance = 2.0f; // Click radius
            IEntity closest = null;

            // Check all interactable entities
            foreach (IEntity entity in area.Creatures)
            {
                float dist = CheckEntityDistance(entity, position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = entity;
                }
            }

            foreach (IEntity entity in area.Placeables)
            {
                float dist = CheckEntityDistance(entity, position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = entity;
                }
            }

            foreach (IEntity entity in area.Doors)
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
            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
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

            ITransformComponent transform = _playerEntity.GetComponent<ITransformComponent>();
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
            if (npc == null)
            {
                return;
            }

            // Get dialogue ResRef from NPC component
            ICreatureComponent creatureComponent = npc.GetComponent<ICreatureComponent>();
            if (creatureComponent == null)
            {
                Console.WriteLine("[PlayerController] NPC has no creature component: " + npc.Tag);
                return;
            }

            string dialogueResRef = creatureComponent.DialogueResRef;
            if (string.IsNullOrEmpty(dialogueResRef))
            {
                Console.WriteLine("[PlayerController] NPC has no dialogue: " + npc.Tag);
                return;
            }

            // Start dialogue via dialogue manager
            if (_dialogueManager != null)
            {
                _dialogueManager.StartConversation(dialogueResRef, npc, _playerEntity);
            }
            else
            {
                Console.WriteLine("[PlayerController] Dialogue manager not available for " + npc.Tag);
            }
        }
    }
}


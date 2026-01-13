using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Runtime.Engines.Odyssey.Dialogue;
using Andastra.Runtime.Engines.Odyssey.Systems;

namespace Andastra.Game.Engines.Odyssey.Game
{
    /// <summary>
    /// Handles player input and movement.
    /// </summary>
    /// <remarks>
    /// Player Controller:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) player input/movement system
    /// - Located via string references: "Player" @ 0x007be628 (player entity type), "PlayerList" @ 0x007bdcf4 (player list field)
    /// - "GetPlayerList" @ 0x007bdd00 (get player list function), "Mod_PlayerList" @ 0x007be060 (module player list)
    /// - "SetByPlayerParty" @ 0x007c1d04 (set by player party flag), "MaxPlayers" @ 0x007bdb48 (max players constant)
    /// - "OnPlayerChange" @ 0x007bd9bc (player change event), "PlayerCreated" @ 0x007bf624 (player created flag)
    /// - "PlayerOnly" @ 0x007c0ca8 (player only flag), "HD0:players" @ 0x007be674 (player directory path)
    /// - "player" @ 0x007c28d8 (player constant), "PLAYER" @ 0x007c36f0 (player constant uppercase)
    /// - Player data: "PCNAME" @ 0x007be194 (PC name field), "PT_PCNAME" @ 0x007c1904 (party table PC name)
    /// - "GAMEINPROGRESS:PC" @ 0x007c1948 (game in progress PC flag), "PCAUTOSAVE" @ 0x007be320 (PC auto-save)
    /// - "PCLevelAtSpawn" @ 0x007c1968 (PC level at spawn field), " [Player]" @ 0x007be200 (player suffix)
    /// - "Players: " @ 0x007be20c (players prefix), ":: Server player list ::\n%s\n" @ 0x007cbbd4 (player list debug)
    /// - Player stats: "G_PC_LEVEL" @ 0x007bf150 (global PC level), "G_PC_Light_Total" @ 0x007c2944 (global PC light side points)
    /// - "G_PC_Dark_Total" @ 0x007c2958 (global PC dark side points), "PCGender" @ 0x007c84d8 (PC gender field)
    /// - "PlayerClass" @ 0x007c2adc (player class field), "PlayerRace" @ 0x007c2c04 (player race field)
    /// - Script events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP" @ 0x007bc5bc (player level up event, 0x6)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST" @ 0x007bc620 (player rest event, 0x8)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING" @ 0x007bc6ac (player dying event, 0x9)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT" @ 0x007bc974 (player exit event, 0x19)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER" @ 0x007bc9a0 (player enter event, 0x1a)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE" @ 0x007bc5ec (destroy player creature event, 0x5)
    /// - Player animations: "DriveAnimRun_PC" @ 0x007c50cc (PC run animation)
    /// - Player audio: "Volume_PC" @ 0x007c6110 (PC volume field), "MinVolumeDist_PC" @ 0x007c60c4, "MaxVolumeDist_PC" @ 0x007c60d8
    /// - Player visual: "pz_playerliteoff" @ 0x007cc6ac, "PZ_PLAYERLITEOFF" @ 0x007cc6c0, "pz_playerliteon" @ 0x007cc6d4 (player lighting effects)
    /// - "HEAL_PLAYER" @ 0x007cbd20 (heal player constant), "CSWMiniPlayer: Recreating gun bank %d" @ 0x007cb5c0 (player weapon bank error)
    /// - Original implementation: Click-to-move with pathfinding, object selection, party control
    /// - Click-to-move: Click world position -> project onto walkmesh -> pathfind -> queue ActionMoveToLocation
    /// - Object interaction: Click entity -> queue ActionUseObject or ActionMoveToObject (based on entity type)
    /// - Right-click: Context menu or alternative action (examine, attack, etc.)
    /// - Party members follow leader, respond to same commands (party control system)
    /// - Movement speeds: Walk speed (default 2.5 units/sec), run speed (default 5.0 units/sec, can be modified by effects)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005226d0 @ 0x005226d0 (player input handling and movement)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Projects click position onto walkmesh surface
            // INavigationMesh.ProjectToSurface projects point onto walkmesh surface
            Vector3? projectedPosition = null;
            if (area.NavigationMesh != null)
            {
                Vector3 result;
                float height;
                if (area.NavigationMesh.ProjectToSurface(position, out result, out height))
                {
                    projectedPosition = result;
                }
                else
                {
                    projectedPosition = position; // Fallback to original position
                }
            }
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
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) creature interaction system
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
                // Check for Conversation property (loaded from UTC template ScriptDialogue field)
                string conversation = string.Empty;
                if (creature is Entity entity)
                {
                    conversation = entity.GetData<string>("Conversation", string.Empty);
                    if (string.IsNullOrEmpty(conversation))
                    {
                        // Also check ScriptDialogue entity data
                        conversation = entity.GetData<string>("ScriptDialogue", string.Empty);
                    }
                }

                if (!string.IsNullOrEmpty(conversation))
                {
                    if (_dialogueManager != null)
                    {
                        _dialogueManager.StartConversation(conversation, creature, _playerEntity);
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
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door interaction system
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
                    // Try to unlock the door (key, lockpicking, or bashing)
                    bool unlocked = TryUnlockDoor(door, doorComponent);
                    if (!unlocked)
                    {
                        // Could not unlock - could try bashing or show message
                        Console.WriteLine("[PlayerController] Door is locked and cannot be unlocked");
                        return;
                    }

                    // If door is still locked after TryUnlockDoor (bashing is in progress), don't try to open it yet
                    // The bashing action will open it when the door is destroyed
                    if (doorComponent.IsLocked)
                    {
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
        /// <param name="door">The door entity.</param>
        /// <param name="doorComponent">The door component.</param>
        /// <remarks>
        /// Door Unlocking Logic:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door unlocking system
        /// - Priority: 1) Key check, 2) Lockpicking (Security skill), 3) Bashing (if no key required)
        /// - Key check: Inventory.HasItemByTag(KeyName)
        /// - Lockpicking: Security skill + d20 vs LockDC
        /// - Bashing: Attack door (damage - hardness) until destroyed
        /// </remarks>
        private bool TryUnlockDoor(IEntity door, IDoorComponent doorComponent)
        {
            if (doorComponent == null || !doorComponent.IsLocked)
            {
                return true; // Already unlocked
            }

            // 1. Try key first
            if (doorComponent.KeyRequired && !string.IsNullOrEmpty(doorComponent.KeyTag))
            {
                IInventoryComponent inventory = _playerEntity.GetComponent<IInventoryComponent>();
                if (inventory != null && inventory.HasItemByTag(doorComponent.KeyTag))
                {
                    doorComponent.Unlock();
                    Console.WriteLine("[PlayerController] Door unlocked with key: " + doorComponent.KeyTag);
                    return true;
                }
            }

            // 2. Try lockpicking (Security skill check)
            if (doorComponent.LockableByScript && doorComponent.LockDC > 0)
            {
                // Get player's Security skill
                IStatsComponent playerStats = _playerEntity.GetComponent<IStatsComponent>();
                if (playerStats != null)
                {
                    Random random = new Random();
                    int roll = random.Next(1, 21);
                    int securitySkill = 0;
                    // SKILL_SECURITY = 6
                    securitySkill = playerStats.GetSkillRank(6);
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


            // 3. Try bashing: Queue bash door action to repeatedly attack door until destroyed
            // Based on swkotor.exe and swkotor2.exe: Door bashing system
            // Located via string references: "gui_mp_bashdp" @ 0x007b5e04, "gui_mp_bashup" @ 0x007b5e14 (swkotor2.exe door bash GUI panels)
            // Original implementation: If key and lockpicking fail, player can bash door to destroy it
            // Bashing repeatedly applies damage (STR modifier + 1d4) until door HP reaches 0
            // Damage is reduced by door Hardness (minimum 1 damage per hit)
            // When door HP <= 0, door is marked as bashed (IsBashed=true), unlocked, and opened
            if (door != null)
            {
                IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
                if (actionQueue != null)
                {
                    actionQueue.Clear();
                    actionQueue.Add(new ActionBashDoor(door.ObjectId));
                    Console.WriteLine("[PlayerController] Door is locked and cannot be unlocked - attempting to bash door");
                    // Return true to indicate we're attempting to unlock (via bashing)
                    // The action will handle the actual bashing process
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Handles clicking on a placeable.
        /// </summary>
        /// <remarks>
        /// Placeable Click Handling:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) placeable interaction system
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
                    // Use ActionUseObject to interact with placeable (moves to use point and triggers OnUsed script)
                    // Use ActionUseObject to interact with placeable (moves to use point and triggers OnUsed script)
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
        /// Find an entity at a world position using spatial queries.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) entity selection system
        /// Located via string references: Entity selection/picking functions
        /// Original implementation: Uses spatial queries to find entities near click position
        /// </summary>
        private IEntity FindEntityAt(Vector3 position)
        {
            IArea area = _world.CurrentArea;
            if (area == null)
            {
                return null;
            }

            float clickRadius = 2.0f; // Click radius in world units
            IEntity closest = null;
            float closestDistance = clickRadius;

            // Use spatial query to find all interactable entities within click radius
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Uses GetEntitiesInRadius for efficient spatial queries
            // Original implementation: Queries entities by type mask (Creature | Placeable | Door)
            ObjectType interactableTypes = ObjectType.Creature | ObjectType.Placeable | ObjectType.Door | ObjectType.Item;
            IEnumerable<IEntity> nearbyEntities = _world.GetEntitiesInRadius(position, clickRadius, interactableTypes);

            foreach (IEntity entity in nearbyEntities)
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

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

            // Selection highlight and targeting cursor updates would be handled by rendering system
            // These are UI/rendering concerns, not controller logic
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

            // Check walkmesh validity using area's walkmesh
            IArea area = _world?.CurrentArea;
            if (area != null)
            {
                // Project the new position onto the walkmesh to ensure it's on valid ground
                if (area.ProjectToWalkmesh(newPosition, out Vector3 projectedPosition, out float height))
                {
                    // Use the projected position (snapped to walkmesh)
                    newPosition = projectedPosition;
                }
                else if (!area.IsPointWalkable(newPosition))
                {
                    // Position is not walkable, don't move
                    return;
                }
            }

            // Update facing direction to match movement direction
            if (movement.LengthSquared() > 0.001f) // Only update if moving
            {
                // Calculate facing angle from movement direction
                // Facing is stored as radians (0 = north, PI/2 = east, PI = south, 3*PI/2 = west)
                float facing = (float)Math.Atan2(movement.X, movement.Z);
                transform.Facing = facing;
            }

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

            // Get dialogue ResRef from NPC entity data (loaded from UTC template ScriptDialogue field)
            string conversation = string.Empty;
            if (npc is Entity entity)
            {
                conversation = entity.GetData<string>("Conversation", string.Empty);
                if (string.IsNullOrEmpty(conversation))
                {
                    // Also check ScriptDialogue entity data
                    conversation = entity.GetData<string>("ScriptDialogue", string.Empty);
                }
            }

            if (string.IsNullOrEmpty(conversation))
            {
                Console.WriteLine("[PlayerController] NPC has no dialogue: " + npc.Tag);
                return;
            }

            // Start dialogue via dialogue manager
            if (_dialogueManager != null)
            {
                _dialogueManager.StartConversation(conversation, npc, _playerEntity);
            }
            else
            {
                Console.WriteLine("[PlayerController] Dialogue manager not available for " + npc.Tag);
            }
        }
    }
}


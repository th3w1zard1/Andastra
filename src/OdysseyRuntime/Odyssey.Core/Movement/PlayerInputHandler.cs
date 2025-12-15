using System;
using System.Numerics;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Movement
{
    /// <summary>
    /// Handles player input for character control.
    /// </summary>
    /// <remarks>
    /// Player Input Handler:
    /// - Based on swkotor2.exe input system
    /// - Located via string references: "Mouse Sensitivity" @ 0x007c85cc, "Mouse Look" @ 0x007c8608, "Reverse Mouse Buttons" @ 0x007c8628
    /// - "EnableHardwareMouse" @ 0x007c71c8, "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc704, "OnClick" @ 0x007c1a20
    /// - Original implementation: Click-to-move, object interaction, party control, pause
    /// - KOTOR Input Model:
    ///   - Left-click: Move to point / Attack target
    ///   - Right-click: Context action (open, talk, etc.)
    ///   - Tab: Cycle party leader
    ///   - Space: Pause combat
    ///   - Number keys: Quick slot abilities
    ///   - Mouse wheel: Zoom camera
    /// - Click-to-move uses pathfinding to navigate to clicked position
    /// - Object selection uses raycasting to determine clicked entity
    /// </remarks>
    public class PlayerInputHandler
    {
        private readonly IWorld _world;
        private readonly Party.PartySystem _partySystem;
        private CharacterController _currentController;

        /// <summary>
        /// Whether the game is paused.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Current cursor mode.
        /// </summary>
        public CursorMode CursorMode { get; private set; }

        /// <summary>
        /// Entity currently targeted by cursor.
        /// </summary>
        public IEntity HoveredEntity { get; private set; }

        /// <summary>
        /// World position under cursor.
        /// </summary>
        public Vector3 CursorWorldPosition { get; private set; }

        /// <summary>
        /// Whether cursor is over a valid movement target.
        /// </summary>
        public bool IsValidMoveTarget { get; private set; }

        /// <summary>
        /// Event fired when move command issued.
        /// </summary>
        public event Action<Vector3> OnMoveCommand;

        /// <summary>
        /// Event fired when attack command issued.
        /// </summary>
        public event Action<IEntity> OnAttackCommand;

        /// <summary>
        /// Event fired when interact command issued.
        /// </summary>
        public event Action<IEntity> OnInteractCommand;

        /// <summary>
        /// Event fired when talk command issued.
        /// </summary>
        public event Action<IEntity> OnTalkCommand;

        /// <summary>
        /// Event fired when pause state changes.
        /// </summary>
        public event Action<bool> OnPauseChanged;

        /// <summary>
        /// Event fired when party leader changes.
        /// </summary>
        public event Action OnLeaderCycled;

        /// <summary>
        /// Event fired when quick slot used.
        /// </summary>
        public event Action<int> OnQuickSlotUsed;

        public PlayerInputHandler(IWorld world, Party.PartySystem partySystem)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _partySystem = partySystem ?? throw new ArgumentNullException("partySystem");

            CursorMode = CursorMode.Default;
        }

        /// <summary>
        /// Sets the character controller for the current leader.
        /// </summary>
        public void SetController(CharacterController controller)
        {
            _currentController = controller;
        }

        #region Input Processing

        /// <summary>
        /// Updates cursor hover state.
        /// </summary>
        /// <param name="worldPosition">World position under cursor.</param>
        /// <param name="hoveredEntity">Entity under cursor (if any).</param>
        public void UpdateCursorHover(Vector3 worldPosition, IEntity hoveredEntity)
        {
            CursorWorldPosition = worldPosition;
            HoveredEntity = hoveredEntity;

            // Update cursor mode based on what's under the cursor
            CursorMode = DetermineCursorMode(hoveredEntity);

            // Check if valid move target (on navmesh)
            IsValidMoveTarget = _world.CurrentArea != null;
        }

        /// <summary>
        /// Processes left-click input.
        /// </summary>
        public void OnLeftClick()
        {
            if (IsPaused)
            {
                // In pause mode, allow queuing commands
            }

            switch (CursorMode)
            {
                case CursorMode.Walk:
                case CursorMode.Run:
                    IssueMoveCommand(CursorWorldPosition);
                    break;

                case CursorMode.Attack:
                    if (HoveredEntity != null)
                    {
                        IssueAttackCommand(HoveredEntity);
                    }
                    break;

                case CursorMode.Talk:
                    if (HoveredEntity != null)
                    {
                        IssueTalkCommand(HoveredEntity);
                    }
                    break;

                case CursorMode.Use:
                case CursorMode.Door:
                    if (HoveredEntity != null)
                    {
                        IssueInteractCommand(HoveredEntity);
                    }
                    break;

                case CursorMode.Pickup:
                    if (HoveredEntity != null)
                    {
                        IssuePickupCommand(HoveredEntity);
                    }
                    break;

                case CursorMode.Transition:
                    if (HoveredEntity != null)
                    {
                        IssueTransitionCommand(HoveredEntity);
                    }
                    break;
            }
        }

        /// <summary>
        /// Processes right-click input.
        /// </summary>
        public void OnRightClick()
        {
            if (HoveredEntity != null)
            {
                // Context action - typically talk or examine
                Enums.ObjectType objectType = HoveredEntity.ObjectType;

                switch (objectType)
                {
                    case Enums.ObjectType.Creature:
                        // Talk to friendly, attack hostile
                        if (IsHostile(HoveredEntity))
                        {
                            IssueAttackCommand(HoveredEntity);
                        }
                        else
                        {
                            IssueTalkCommand(HoveredEntity);
                        }
                        break;

                    case Enums.ObjectType.Door:
                    case Enums.ObjectType.Placeable:
                        IssueInteractCommand(HoveredEntity);
                        break;

                    default:
                        IssueExamineCommand(HoveredEntity);
                        break;
                }
            }
        }

        /// <summary>
        /// Processes pause toggle (Space).
        /// </summary>
        public void OnPauseToggle()
        {
            IsPaused = !IsPaused;

            OnPauseChanged?.Invoke(IsPaused);
        }

        /// <summary>
        /// Processes party cycle (Tab).
        /// </summary>
        public void OnCycleParty()
        {
            _partySystem.CycleLeader();

            OnLeaderCycled?.Invoke();
        }

        /// <summary>
        /// Processes quick slot key (1-9).
        /// </summary>
        public void OnQuickSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex > 9)
            {
                return;
            }

            OnQuickSlotUsed?.Invoke(slotIndex);
        }

        /// <summary>
        /// Processes solo mode toggle (V).
        /// </summary>
        public void OnSoloModeToggle()
        {
            _partySystem.SoloMode = !_partySystem.SoloMode;
        }

        #endregion

        #region Command Issuance

        private void IssueMoveCommand(Vector3 destination)
        {
            if (_currentController != null)
            {
                _currentController.MoveTo(destination, true);
            }

            OnMoveCommand?.Invoke(destination);
        }

        private void IssueAttackCommand(IEntity target)
        {
            OnAttackCommand?.Invoke(target);

            // Move to attack range then attack
            if (_currentController != null)
            {
                float attackRange = GetAttackRange();
                _currentController.MoveToEntity(target, attackRange, true);
            }
        }

        private void IssueTalkCommand(IEntity target)
        {
            OnTalkCommand?.Invoke(target);

            // Move to conversation range
            _currentController?.MoveToEntity(target, 2.0f, true);
        }

        private void IssueInteractCommand(IEntity target)
        {
            OnInteractCommand?.Invoke(target);

            // Move to interaction range
            _currentController?.MoveToEntity(target, 1.5f, true);
        }

        private void IssuePickupCommand(IEntity target)
        {
            // Same as interact for items
            IssueInteractCommand(target);
        }

        private void IssueTransitionCommand(IEntity target)
        {
            // Move to transition trigger/door and interact
            IssueInteractCommand(target);
        }

        private void IssueExamineCommand(IEntity target)
        {
            // Show examine tooltip/description
        }

        #endregion

        #region Cursor Mode Logic

        private CursorMode DetermineCursorMode(IEntity hoveredEntity)
        {
            if (hoveredEntity == null)
            {
                return IsValidMoveTarget ? CursorMode.Walk : CursorMode.NoWalk;
            }

            Enums.ObjectType objectType = hoveredEntity.ObjectType;

            switch (objectType)
            {
                case Enums.ObjectType.Creature:
                    if (IsHostile(hoveredEntity))
                    {
                        return CursorMode.Attack;
                    }
                    else if (HasConversation(hoveredEntity))
                    {
                        return CursorMode.Talk;
                    }
                    else
                    {
                        return CursorMode.Default;
                    }

                case Enums.ObjectType.Door:
                    Interfaces.Components.IDoorComponent door = hoveredEntity.GetComponent<Interfaces.Components.IDoorComponent>();
                    if (door != null)
                    {
                        if (!string.IsNullOrEmpty(door.LinkedToModule))
                        {
                            return CursorMode.Transition;
                        }
                    }
                    return CursorMode.Door;

                case Enums.ObjectType.Placeable:
                    Interfaces.Components.IPlaceableComponent placeable = hoveredEntity.GetComponent<Interfaces.Components.IPlaceableComponent>();
                    if (placeable != null)
                    {
                        if (placeable.HasInventory)
                        {
                            return CursorMode.Use;
                        }
                    }
                    return CursorMode.Use;

                case Enums.ObjectType.Trigger:
                    Interfaces.Components.ITriggerComponent trigger = hoveredEntity.GetComponent<Interfaces.Components.ITriggerComponent>();
                    if (trigger != null)
                    {
                        if (!string.IsNullOrEmpty(trigger.LinkedToModule))
                        {
                            return CursorMode.Transition;
                        }
                    }
                    return CursorMode.Default;

                case Enums.ObjectType.Item:
                    return CursorMode.Pickup;

                default:
                    return CursorMode.Default;
            }
        }

        private bool IsHostile(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            // Check faction component for hostility
            // Based on swkotor2.exe faction system
            // Located via string references: "Faction" @ 0x007c24dc, "IsHostile" checks
            // Original implementation: Checks faction relationships via FactionManager
            // For PlayerInputHandler, we check IFactionComponent which provides IsHostile method
            Interfaces.Components.IFactionComponent faction = entity.GetComponent<Interfaces.Components.IFactionComponent>();
            if (faction != null)
            {
                // Get current party leader for hostility check
                var leader = (IEntity)(_partySystem?.Leader);
                if (leader != null)
                {
                    return faction.IsHostile(leader);
                }
            }

            // Fallback: Check if entity is a creature (could be hostile)
            // In KOTOR, most hostile entities are creatures
            return entity.ObjectType == Enums.ObjectType.Creature;
        }

        private bool HasConversation(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            // Check DoorComponent for conversation
            // Based on swkotor2.exe door system
            // Located via string references: "Conversation" @ door components
            // Original implementation: Doors can have conversation scripts
            Interfaces.Components.IDoorComponent door = entity.GetComponent<Interfaces.Components.IDoorComponent>();
            if (door != null)
            {
                // Doors with OnUsed scripts typically have conversations
                // This is a simplified check - full implementation would check Conversation property
                // For now, assume doors might have conversations
                return true;
            }

            // Check PlaceableComponent for conversation
            Interfaces.Components.IPlaceableComponent placeable = entity.GetComponent<Interfaces.Components.IPlaceableComponent>();
            if (placeable != null)
            {
                // Placeables with OnUsed scripts typically have conversations
                // This is a simplified check - full implementation would check Conversation property
                return true;
            }

            // For creatures, assume they might have conversations (NPCs typically do)
            // This is a fallback - ideally conversation would be stored in CreatureComponent
            return entity.ObjectType == Enums.ObjectType.Creature;
        }

        private float GetAttackRange()
        {
            // Get current party leader
            var leader = (IEntity)(_partySystem?.Leader);
            if (leader == null)
            {
                return 2.0f; // Default melee range
            }

            // Get equipped weapon from main hand (slot 4)
            // Based on swkotor2.exe inventory system
            // Located via string references: "INVENTORY_SLOT_RIGHTWEAPON" = 4
            // Original implementation: Gets equipped weapon from right hand slot
            // TODO: Re-enable when project references are fixed
            // Interfaces.Components.IInventoryComponent inventory = leader.GetComponent<Interfaces.Components.IInventoryComponent>();
            // if (inventory == null)
            // {
            //     return 2.0f; // Default melee range
            // }
            //
            // // INVENTORY_SLOT_RIGHTWEAPON = 4
            // IEntity weapon = inventory.GetItemInSlot(4);
            // if (weapon == null)
            // {
            //     return 2.0f; // Default melee range (unarmed)
            // }

            // TODO: Check if weapon has range data
            // if (weapon.HasData("Range"))
            // {
            //     float range = weapon.GetData<float>("Range", 2.0f);
            //     if (range > 0)
            //     {
            //         return range;
            //     }
            // }

            // TODO: Check weapon type to determine range
            // Ranged weapons typically have longer range than melee
            // if (weapon.HasData("WeaponType"))
            // {
            //     int weaponType = weapon.GetData<int>("WeaponType", 0);
            //     // Weapon type constants would be defined elsewhere
            //     // For now, default to melee range
            // }

            // Default melee range
            return 2.0f;
        }

        #endregion

        #region Selection

        private IEntity _selectedEntity;

        /// <summary>
        /// Currently selected entity.
        /// </summary>
        public IEntity SelectedEntity
        {
            get { return _selectedEntity; }
        }

        /// <summary>
        /// Selects an entity.
        /// </summary>
        public void Select(IEntity entity)
        {
            _selectedEntity = entity;
        }

        /// <summary>
        /// Clears selection.
        /// </summary>
        public void ClearSelection()
        {
            _selectedEntity = null;
        }

        #endregion
    }

    /// <summary>
    /// Cursor display modes.
    /// </summary>
    public enum CursorMode
    {
        /// <summary>
        /// Default cursor.
        /// </summary>
        Default,

        /// <summary>
        /// Walk cursor (valid ground).
        /// </summary>
        Walk,

        /// <summary>
        /// Run cursor.
        /// </summary>
        Run,

        /// <summary>
        /// Invalid move target.
        /// </summary>
        NoWalk,

        /// <summary>
        /// Attack cursor (hostile target).
        /// </summary>
        Attack,

        /// <summary>
        /// Talk cursor (friendly NPC).
        /// </summary>
        Talk,

        /// <summary>
        /// Use/interact cursor.
        /// </summary>
        Use,

        /// <summary>
        /// Door cursor.
        /// </summary>
        Door,

        /// <summary>
        /// Pickup item cursor.
        /// </summary>
        Pickup,

        /// <summary>
        /// Transition cursor (area/module transition).
        /// </summary>
        Transition,

        /// <summary>
        /// Magic/ability targeting cursor.
        /// </summary>
        Magic,

        /// <summary>
        /// Examine cursor.
        /// </summary>
        Examine
    }
}

using System;
using System.Numerics;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Movement
{
    /// <summary>
    /// Handles player input for character control.
    /// </summary>
    /// <remarks>
    /// KOTOR Input Model:
    /// - Left-click: Move to point / Attack target
    /// - Right-click: Context action (open, talk, etc.)
    /// - Tab: Cycle party leader
    /// - Space: Pause combat
    /// - Number keys: Quick slot abilities
    /// - Mouse wheel: Zoom camera
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
                var objectType = HoveredEntity.ObjectType;

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

            if (OnPauseChanged != null)
            {
                OnPauseChanged(IsPaused);
            }
        }

        /// <summary>
        /// Processes party cycle (Tab).
        /// </summary>
        public void OnCycleParty()
        {
            _partySystem.CycleLeader();

            if (OnLeaderCycled != null)
            {
                OnLeaderCycled();
            }
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

            if (OnQuickSlotUsed != null)
            {
                OnQuickSlotUsed(slotIndex);
            }
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

            if (OnMoveCommand != null)
            {
                OnMoveCommand(destination);
            }
        }

        private void IssueAttackCommand(IEntity target)
        {
            if (OnAttackCommand != null)
            {
                OnAttackCommand(target);
            }

            // Move to attack range then attack
            if (_currentController != null)
            {
                float attackRange = GetAttackRange();
                _currentController.MoveToEntity(target, attackRange, true);
            }
        }

        private void IssueTalkCommand(IEntity target)
        {
            if (OnTalkCommand != null)
            {
                OnTalkCommand(target);
            }

            // Move to conversation range
            if (_currentController != null)
            {
                _currentController.MoveToEntity(target, 2.0f, true);
            }
        }

        private void IssueInteractCommand(IEntity target)
        {
            if (OnInteractCommand != null)
            {
                OnInteractCommand(target);
            }

            // Move to interaction range
            if (_currentController != null)
            {
                _currentController.MoveToEntity(target, 1.5f, true);
            }
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

            var objectType = hoveredEntity.ObjectType;

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
                    var door = hoveredEntity.GetComponent<Interfaces.Components.IDoorComponent>();
                    if (door != null)
                    {
                        if (!string.IsNullOrEmpty(door.LinkedToModule))
                        {
                            return CursorMode.Transition;
                        }
                    }
                    return CursorMode.Door;

                case Enums.ObjectType.Placeable:
                    var placeable = hoveredEntity.GetComponent<Interfaces.Components.IPlaceableComponent>();
                    if (placeable != null)
                    {
                        if (placeable.HasInventory)
                        {
                            return CursorMode.Use;
                        }
                    }
                    return CursorMode.Use;

                case Enums.ObjectType.Trigger:
                    var trigger = hoveredEntity.GetComponent<Interfaces.Components.ITriggerComponent>();
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
            // TODO: Implement faction hostility check
            // For now, check if entity has hostile flag or is in combat
            return false;
        }

        private bool HasConversation(IEntity entity)
        {
            // TODO: Check if entity has a DLG file
            // For now, assume NPCs have conversations
            return entity.ObjectType == Enums.ObjectType.Creature;
        }

        private float GetAttackRange()
        {
            // TODO: Get from equipped weapon
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

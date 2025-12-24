using System;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Movement
{
    /// <summary>
    /// Handles player input for character control.
    /// </summary>
    /// <remarks>
    /// Player Input Handler:
    /// - Base class for player input handling across all engines
    /// - Odyssey-specific implementations: K1PlayerInputHandler (swkotor.exe) and K2PlayerInputHandler (swkotor2.exe) in Runtime.Games.Odyssey.Input
    /// - Located via string references: "Mouse Sensitivity" @ 0x007c85cc, "Mouse Look" @ 0x007c8608, "Reverse Mouse Buttons" @ 0x007c8628
    /// - "EnableHardwareMouse" @ 0x007c71c8, "Enable Mouse Teleporting To Buttons" @ 0x007c85a8
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc704, "OnClick" @ 0x007c1a20
    /// - Input system: "Input" @ 0x007c2520, "Mouse" @ 0x007cb908
    /// - Input class: "CExoInputInternal" (exoinputinternal.cpp @ 0x007c64dc)
    /// - Error: "CExoInputInternal::GetEvents() Invalid InputClass parameter" @ 0x007c64f4
    /// - "Unnamed Input Class" @ 0x007c64c8
    /// - GUI references: ";gui_mouse" @ 0x007b5f93, "optmouse_p" @ 0x007d1f64
    /// - "LBL_MOUSESEN" @ 0x007d1f44, "SLI_MOUSESEN" @ 0x007d1f54, "BTN_MOUSE" @ 0x007d28a0
    /// - Original implementation: Uses DirectInput8 (DINPUT8.dll @ 0x0080a6c0, DirectInput8Create @ 0x0080a6ac)
    /// - Click-to-move, object interaction, party control, pause
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
        protected readonly Party.PartySystem _partySystem;
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
                case CursorMode.AttackShiiCho:
                case CursorMode.AttackMakashi:
                case CursorMode.AttackSoresu:
                case CursorMode.AttackAtaru:
                case CursorMode.AttackShien:
                case CursorMode.AttackNiman:
                case CursorMode.AttackJuyo:
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

        protected virtual CursorMode DetermineCursorMode(IEntity hoveredEntity)
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

        protected virtual bool IsHostile(IEntity entity)
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
            // Located via string references: "Conversation" @ 0x007c1abc
            // Original implementation: FUN_00580330 @ 0x00580330 saves door data including Conversation field
            // Conversation field in UTD template contains dialogue ResRef
            Interfaces.Components.IDoorComponent door = entity.GetComponent<Interfaces.Components.IDoorComponent>();
            if (door != null)
            {
                // Check Conversation property from door component
                if (!string.IsNullOrEmpty(door.Conversation))
                {
                    return true;
                }
            }

            // Check PlaceableComponent for conversation
            // Based on swkotor2.exe placeable system
            // Located via string references: "Conversation" @ 0x007c1abc
            // Original implementation: FUN_00588010 @ 0x00588010 loads placeable data including Conversation field
            // Conversation field in UTP template contains dialogue ResRef
            Interfaces.Components.IPlaceableComponent placeable = entity.GetComponent<Interfaces.Components.IPlaceableComponent>();
            if (placeable != null)
            {
                // Check Conversation property from placeable component
                if (!string.IsNullOrEmpty(placeable.Conversation))
                {
                    return true;
                }
            }

            // Check for creature component conversation
            // Based on swkotor2.exe creature system
            // Located via string references: "Conversation" @ 0x007c1abc, "ScriptDialogue" @ 0x007bee40
            // Original implementation: FUN_0050c510 @ 0x0050c510 loads creature data including ScriptDialogue field
            // ScriptDialogue field in UTC template contains dialogue ResRef (stored as Conversation property)
            if (entity.ObjectType == Enums.ObjectType.Creature)
            {
                // Try to get conversation from entity data (loaded from UTC template)
                // This is a fallback for creatures that don't have a component yet
                if (entity is Entity creatureEntity)
                {
                    string conversation = creatureEntity.GetData<string>("Conversation", string.Empty);
                    if (string.IsNullOrEmpty(conversation))
                    {
                        // Also check ScriptDialogue entity data (alternative field name)
                        conversation = creatureEntity.GetData<string>("ScriptDialogue", string.Empty);
                    }
                    if (!string.IsNullOrEmpty(conversation))
                    {
                        return true;
                    }
                }

                // For creatures, assume they might have conversations (NPCs typically do)
                // This is a fallback when conversation data is not available
                return true;
            }

            return false;
        }

        protected virtual float GetAttackRange()
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
            Interfaces.Components.IInventoryComponent inventory = leader.GetComponent<Interfaces.Components.IInventoryComponent>();
            if (inventory == null)
            {
                return 2.0f; // Default melee range
            }

            // INVENTORY_SLOT_RIGHTWEAPON = 4
            IEntity weapon = inventory.GetItemInSlot(4);
            if (weapon == null)
            {
                return 2.0f; // Default melee range (unarmed)
            }

            // Check if weapon has range data stored in entity data
            if (weapon is Entities.Entity weaponEntity && weaponEntity.HasData("Range"))
            {
                float range = weaponEntity.GetData<float>("Range", 2.0f);
                if (range > 0)
                {
                    return range;
                }
            }

            // Get base item ID from weapon component and look up attack range from baseitems.2da
            // Based on swkotor2.exe weapon system
            // Located via string references: "WeaponType" in baseitems.2da, "maxattackrange" column
            // Original implementation: Reads maxattackrange from baseitems.2da using BaseItem ID
            // xoreos implementation: Item::getMaxAttackRange() @ vendor/xoreos/src/engines/kotorbase/item.cpp:74
            //   Reads _maxAttackRange = twoDA.getFloat("maxattackrange") from baseitems.2da
            // PyKotor documentation: baseitems.2da has "maxattackrange" column (Integer) for maximum attack range
            Interfaces.Components.IItemComponent itemComponent = weapon.GetComponent<Interfaces.Components.IItemComponent>();
            if (itemComponent != null && _world?.GameDataProvider != null)
            {
                int baseItemId = itemComponent.BaseItem;
                if (baseItemId >= 0)
                {
                    // Read maxattackrange from baseitems.2da using GameDataProvider
                    // Based on swkotor2.exe: Reads maxattackrange column from baseitems.2da row indexed by BaseItem ID
                    float maxAttackRange = _world.GameDataProvider.GetTableFloat("baseitems", baseItemId, "maxattackrange", 0.0f);
                    if (maxAttackRange > 0.0f)
                    {
                        // Convert from game units to world units if necessary (maxattackrange is typically in game units)
                        // Based on xoreos implementation: getMaxAttackRange() returns float directly from 2DA
                        // KOTOR uses game units where 1.0 = 1 meter approximately, so direct conversion should work
                        return maxAttackRange;
                    }

                    // Fallback: Check if ranged weapon to use default ranged range
                    // Based on swkotor2.exe: Ranged weapons have longer default range than melee
                    // Read rangedweapon flag from baseitems.2da to determine if ranged
                    int rangedWeapon = (int)_world.GameDataProvider.GetTableFloat("baseitems", baseItemId, "rangedweapon", 0.0f);
                    if (rangedWeapon != 0)
                    {
                        // Default ranged weapon range (approximate fallback when maxattackrange not available)
                        return 10.0f;
                    }
                }
            }

            // Default melee range (unarmed or melee weapon without range data)
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
        /// Attack cursor with Shii-Cho form (Form I).
        /// </summary>
        AttackShiiCho,

        /// <summary>
        /// Attack cursor with Makashi form (Form II).
        /// </summary>
        AttackMakashi,

        /// <summary>
        /// Attack cursor with Soresu form (Form III).
        /// </summary>
        AttackSoresu,

        /// <summary>
        /// Attack cursor with Ataru form (Form IV).
        /// </summary>
        AttackAtaru,

        /// <summary>
        /// Attack cursor with Shien form (Form V).
        /// </summary>
        AttackShien,

        /// <summary>
        /// Attack cursor with Niman form (Form VI).
        /// </summary>
        AttackNiman,

        /// <summary>
        /// Attack cursor with Juyo form (Form VII).
        /// </summary>
        AttackJuyo,

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

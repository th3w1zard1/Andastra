using System;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Templates
{
    /// <summary>
    /// Door template implementation for spawning doors from UTD data.
    /// </summary>
    /// <remarks>
    /// Door Template:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door system
    /// - Located via string references: "Door" @ 0x007bc538, "Door List" @ 0x007bd270
    /// - Template loading: 0x005226d0 @ 0x005226d0 (entity serialization references door templates)
    /// - Original implementation: UTD (Door) GFF templates define door properties
    /// - UTD file format: GFF with "UTD " signature containing door data
    /// - Doors have appearance, open/locked states, transitions, HP, saves
    /// - Transitions link to other areas/modules (LinkedTo, LinkedToModule, TransitionDestination)
    /// - Script events: OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
    /// - Based on UTD file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class DoorTemplate : IDoorTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Door; } }
        public string DisplayName { get; set; }
        public int AppearanceId { get; set; }
        public bool IsLocked { get; set; }
        public int UnlockDc { get; set; }
        public bool KeyRequired { get; set; }
        public string KeyName { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }

        // Additional properties
        public string Conversation { get; set; }
        public int FactionId { get; set; }
        public int AnimationState { get; set; }
        public bool AutoRemoveKey { get; set; }
        public bool Lockable { get; set; }
        public int OpenState { get; set; }
        public bool IsPlot { get; set; }
        public bool IsStatic { get; set; }
        public int Hardness { get; set; }
        public int FortitudeBonus { get; set; }
        public int ReflexBonus { get; set; }
        public int WillBonus { get; set; }
        public bool NotBlastable { get; set; }
        public bool Min1Hp { get; set; }

        // Script hooks
        public string OnClick { get; set; }
        public string OnOpen { get; set; }
        public string OnClosed { get; set; }
        public string OnDamaged { get; set; }
        public string OnDeath { get; set; }
        public string OnUnlock { get; set; }
        public string OnLock { get; set; }
        public string OnOpenFailed { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnMeleeAttacked { get; set; }
        public string OnSpellCastAt { get; set; }
        public string OnUserDefined { get; set; }

        // Transition info
        public string LinkedTo { get; set; }
        public int LinkedToFlags { get; set; }
        public string LinkedToModule { get; set; }
        public string TransitionDestination { get; set; }

        #endregion

        public DoorTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            DisplayName = string.Empty;
            KeyName = string.Empty;
            Conversation = string.Empty;
            LinkedTo = string.Empty;
            LinkedToModule = string.Empty;
            TransitionDestination = string.Empty;

            OnClick = string.Empty;
            OnOpen = string.Empty;
            OnClosed = string.Empty;
            OnDamaged = string.Empty;
            OnDeath = string.Empty;
            OnUnlock = string.Empty;
            OnLock = string.Empty;
            OnOpenFailed = string.Empty;
            OnHeartbeat = string.Empty;
            OnMeleeAttacked = string.Empty;
            OnSpellCastAt = string.Empty;
            OnUserDefined = string.Empty;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var entity = new Entity(ObjectType.Door, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position and facing
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = position;
                transform.Facing = facing;
            }

            // Apply door-specific components
            Interfaces.Components.IDoorComponent door = entity.GetComponent<Interfaces.Components.IDoorComponent>();
            if (door != null)
            {
                door.IsLocked = IsLocked;
                door.LockDC = UnlockDc;
                door.KeyRequired = KeyRequired;
                door.KeyTag = KeyName;
                door.OpenState = OpenState;
            }

            // Apply script hooks
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Door script hooks from UTD template
            // Located via string references: "ScriptOnOpen" @ 0x007c1a54, "ScriptOnClose" @ 0x007c1a8c, "ScriptOnClick" @ 0x007c1a20
            // Original implementation: 0x00580ed0 @ 0x00580ed0 loads door script hooks from UTD template
            Interfaces.Components.IScriptHooksComponent scripts = entity.GetComponent<Interfaces.Components.IScriptHooksComponent>();
            if (scripts != null)
            {
                if (!string.IsNullOrEmpty(OnOpen))
                    scripts.SetScript(ScriptEvent.OnOpen, OnOpen);
                if (!string.IsNullOrEmpty(OnClosed))
                    scripts.SetScript(ScriptEvent.OnClose, OnClosed);
                if (!string.IsNullOrEmpty(OnClick))
                    scripts.SetScript(ScriptEvent.OnClick, OnClick);
                if (!string.IsNullOrEmpty(OnLock))
                    scripts.SetScript(ScriptEvent.OnLock, OnLock);
                if (!string.IsNullOrEmpty(OnUnlock))
                    scripts.SetScript(ScriptEvent.OnUnlock, OnUnlock);
                if (!string.IsNullOrEmpty(OnOpenFailed))
                    scripts.SetScript(ScriptEvent.OnFailToOpen, OnOpenFailed);
                if (!string.IsNullOrEmpty(OnDamaged))
                    scripts.SetScript(ScriptEvent.OnDamaged, OnDamaged);
                if (!string.IsNullOrEmpty(OnDeath))
                    scripts.SetScript(ScriptEvent.OnDeath, OnDeath);
                if (!string.IsNullOrEmpty(OnHeartbeat))
                    scripts.SetScript(ScriptEvent.OnHeartbeat, OnHeartbeat);
                if (!string.IsNullOrEmpty(OnMeleeAttacked))
                    scripts.SetScript(ScriptEvent.OnPhysicalAttacked, OnMeleeAttacked);
                if (!string.IsNullOrEmpty(OnSpellCastAt))
                    scripts.SetScript(ScriptEvent.OnSpellCastAt, OnSpellCastAt);
                if (!string.IsNullOrEmpty(OnUserDefined))
                    scripts.SetScript(ScriptEvent.OnUserDefined, OnUserDefined);
            }

            // Register in world
            world.RegisterEntity(entity);

            return entity;
        }
    }
}

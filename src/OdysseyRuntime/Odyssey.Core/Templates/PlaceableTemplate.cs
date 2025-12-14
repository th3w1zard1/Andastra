using System;
using System.Numerics;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Templates
{
    /// <summary>
    /// Placeable template implementation for spawning placeables from UTP data.
    /// </summary>
    public class PlaceableTemplate : IPlaceableTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Placeable; } }
        public string DisplayName { get; set; }
        public int AppearanceId { get; set; }
        public bool IsUseable { get; set; }
        public bool HasInventory { get; set; }
        public bool IsStatic { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }

        // Additional properties
        public string Conversation { get; set; }
        public int FactionId { get; set; }
        public int AnimationState { get; set; }
        public bool AutoRemoveKey { get; set; }
        public string KeyName { get; set; }
        public bool KeyRequired { get; set; }
        public bool IsLocked { get; set; }
        public bool Lockable { get; set; }
        public int UnlockDc { get; set; }
        public bool IsPlot { get; set; }
        public bool PartyInteract { get; set; }
        public int Hardness { get; set; }
        public int FortitudeBonus { get; set; }
        public int ReflexBonus { get; set; }
        public int WillBonus { get; set; }
        public bool NotBlastable { get; set; }
        public bool Min1Hp { get; set; }
        public string Description { get; set; }

        // Script hooks
        public string OnClick { get; set; }
        public string OnOpen { get; set; }
        public string OnClosed { get; set; }
        public string OnDamaged { get; set; }
        public string OnDeath { get; set; }
        public string OnUnlock { get; set; }
        public string OnLock { get; set; }
        public string OnUsed { get; set; }
        public string OnOpenFailed { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnInventory { get; set; }
        public string OnMeleeAttacked { get; set; }
        public string OnEndDialog { get; set; }
        public string OnSpellCastAt { get; set; }
        public string OnUserDefined { get; set; }

        #endregion

        public PlaceableTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            DisplayName = string.Empty;
            Conversation = string.Empty;
            KeyName = string.Empty;
            Description = string.Empty;

            OnClick = string.Empty;
            OnOpen = string.Empty;
            OnClosed = string.Empty;
            OnDamaged = string.Empty;
            OnDeath = string.Empty;
            OnUnlock = string.Empty;
            OnLock = string.Empty;
            OnUsed = string.Empty;
            OnOpenFailed = string.Empty;
            OnHeartbeat = string.Empty;
            OnInventory = string.Empty;
            OnMeleeAttacked = string.Empty;
            OnEndDialog = string.Empty;
            OnSpellCastAt = string.Empty;
            OnUserDefined = string.Empty;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var entity = new Entity(ObjectType.Placeable, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position and facing
            var transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = position;
                transform.Facing = facing;
            }

            // Apply placeable-specific components
            var placeable = entity.GetComponent<Interfaces.Components.IPlaceableComponent>();
            if (placeable != null)
            {
                placeable.IsUseable = IsUseable;
                placeable.HasInventory = HasInventory;
                placeable.IsStatic = IsStatic;
                placeable.AnimationState = AnimationState;
            }

            // Apply script hooks
            var scripts = entity.GetComponent<Interfaces.Components.IScriptHooksComponent>();
            if (scripts != null)
            {
                if (!string.IsNullOrEmpty(OnUsed))
                    scripts.SetScript(ScriptEvent.OnUsed, OnUsed);
                if (!string.IsNullOrEmpty(OnClick))
                    scripts.SetScript(ScriptEvent.OnUsed, OnClick);
                if (!string.IsNullOrEmpty(OnDamaged))
                    scripts.SetScript(ScriptEvent.OnDamaged, OnDamaged);
                if (!string.IsNullOrEmpty(OnDeath))
                    scripts.SetScript(ScriptEvent.OnDeath, OnDeath);
                if (!string.IsNullOrEmpty(OnHeartbeat))
                    scripts.SetScript(ScriptEvent.OnHeartbeat, OnHeartbeat);
                if (!string.IsNullOrEmpty(OnUserDefined))
                    scripts.SetScript(ScriptEvent.OnUserDefined, OnUserDefined);
            }

            // Register in world
            world.RegisterEntity(entity);

            return entity;
        }
    }
}

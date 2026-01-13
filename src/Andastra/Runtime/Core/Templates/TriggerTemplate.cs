using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Templates
{
    /// <summary>
    /// Trigger template implementation for spawning triggers from UTT data.
    /// </summary>
    /// <remarks>
    /// Trigger Template:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) trigger system
    /// - Located via string references: "Trigger" @ 0x007bc51c, "TriggerList" @ 0x007bd254
    /// - "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "EVENT_LEFT_TRIGGER" @ 0x007bcdf4
    /// - "OnTrapTriggered" @ 0x007c1a34, "CB_TRIGGERS" @ 0x007d29c8
    /// - Template loading: 0x005226d0 @ 0x005226d0 (entity serialization references trigger templates)
    /// - Original implementation: UTT (Trigger) GFF templates define trigger properties
    /// - UTT file format: GFF with "UTT " signature containing trigger data
    /// - Triggers are invisible volumes defined by polygon geometry
    /// - Triggers fire events (OnEnter, OnExit) when entities enter/exit trigger volume
    /// - Triggers can link to other areas/modules for transitions (LinkedTo, LinkedToModule)
    /// - Triggers can have trap properties (TrapDetectable, TrapDisarmable, TrapType)
    /// - Based on UTT file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class TriggerTemplate : ITriggerTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Trigger; } }
        public int TriggerType { get; set; }
        public string LinkedTo { get; set; }
        public string LinkedToModule { get; set; }

        // Additional properties
        public string DisplayName { get; set; }
        public int FactionId { get; set; }
        public int Cursor { get; set; }
        public string HighlightHeight { get; set; }
        public int LoadScreenId { get; set; }
        public int LinkedToFlags { get; set; }
        public string TransitionDestination { get; set; }
        public bool TrapDetectable { get; set; }
        public bool TrapDisarmable { get; set; }
        public int TrapDetectDc { get; set; }
        public int TrapDisarmDc { get; set; }
        public bool TrapFlag { get; set; }
        public bool TrapOneShot { get; set; }
        public int TrapType { get; set; }

        // Geometry (for trigger shape)
        public List<Vector3> Geometry { get; set; }

        // Script hooks
        public string OnEnter { get; set; }
        public string OnExit { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnClick { get; set; }
        public string OnDisarm { get; set; }
        public string OnTrapTriggered { get; set; }
        public string OnUserDefined { get; set; }

        #endregion

        public TriggerTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            LinkedTo = string.Empty;
            LinkedToModule = string.Empty;
            DisplayName = string.Empty;
            HighlightHeight = string.Empty;
            TransitionDestination = string.Empty;
            Geometry = new List<Vector3>();

            OnEnter = string.Empty;
            OnExit = string.Empty;
            OnHeartbeat = string.Empty;
            OnClick = string.Empty;
            OnDisarm = string.Empty;
            OnTrapTriggered = string.Empty;
            OnUserDefined = string.Empty;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var entity = new Entity(ObjectType.Trigger, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = position;
                transform.Facing = facing;
            }

            // Apply trigger-specific components
            Interfaces.Components.ITriggerComponent trigger = entity.GetComponent<Interfaces.Components.ITriggerComponent>();
            if (trigger != null)
            {
                trigger.TriggerType = TriggerType;
                trigger.LinkedTo = LinkedTo;
                trigger.LinkedToModule = LinkedToModule;
                trigger.Geometry = Geometry;
                trigger.IsTrap = TrapFlag;
            }

            // Apply script hooks
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Trigger script hooks from UTT template
            // Located via string references: "ScriptOnEnter" @ 0x007c1a10, "ScriptOnExit" @ 0x007c1a0c, "OnTrapTriggered" @ 0x007c1a34
            // Original implementation: 0x005226d0 @ 0x005226d0 loads trigger script hooks from UTT template
            Interfaces.Components.IScriptHooksComponent scripts = entity.GetComponent<Interfaces.Components.IScriptHooksComponent>();
            if (scripts != null)
            {
                if (!string.IsNullOrEmpty(OnEnter))
                    scripts.SetScript(ScriptEvent.OnEnter, OnEnter);
                if (!string.IsNullOrEmpty(OnExit))
                    scripts.SetScript(ScriptEvent.OnExit, OnExit);
                if (!string.IsNullOrEmpty(OnHeartbeat))
                    scripts.SetScript(ScriptEvent.OnHeartbeat, OnHeartbeat);
                if (!string.IsNullOrEmpty(OnClick))
                    scripts.SetScript(ScriptEvent.OnClick, OnClick);
                if (!string.IsNullOrEmpty(OnDisarm))
                    scripts.SetScript(ScriptEvent.OnDisarm, OnDisarm);
                if (!string.IsNullOrEmpty(OnTrapTriggered))
                    scripts.SetScript(ScriptEvent.OnTrapTriggered, OnTrapTriggered);
                if (!string.IsNullOrEmpty(OnUserDefined))
                    scripts.SetScript(ScriptEvent.OnUserDefined, OnUserDefined);
            }

            // Register in world
            world.RegisterEntity(entity);

            return entity;
        }
    }
}

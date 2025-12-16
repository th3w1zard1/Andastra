using System.Collections.Generic;
using System.Numerics;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;

namespace BioWareEngines.Kotor.Components
{
    /// <summary>
    /// Component for trigger entities.
    /// </summary>
    /// <remarks>
    /// Trigger Component:
    /// - Based on swkotor2.exe trigger system
    /// - Located via string references: "Trigger" @ 0x007bc51c, "TriggerList" @ 0x007bd254
    /// - "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "EVENT_LEFT_TRIGGER" @ 0x007bcdf4
    /// - "OnTrapTriggered" @ 0x007c1a34, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED" @ 0x007bc7ac
    /// - Transition fields: "LinkedTo" @ 0x007bd798, "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788
    /// - "TransitionDestination" @ 0x007bd7a4 (waypoint tag for positioning after transition)
    /// - Original implementation: UTT (Trigger) GFF templates define trigger properties and geometry
    /// - Triggers are invisible polygonal volumes that fire scripts on enter/exit
    /// - Trigger types: Generic (0), Transition (1), Trap (2)
    /// - Transition triggers: LinkedTo, LinkedToModule, LinkedToFlags for area/module transitions
    /// - Trap triggers: OnTrapTriggered script fires when trap is activated
    /// - Geometry: Triggers have polygon geometry (Geometry field in GIT) defining trigger volume
    /// - Based on UTT file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class TriggerComponent : ITriggerComponent
    {
        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public TriggerComponent()
        {
            TemplateResRef = string.Empty;
            LinkedTo = string.Empty;
            LinkedToModule = string.Empty;
            TransitionDestination = string.Empty;
            Vertices = new List<Vector3>();
            EnteredBy = new HashSet<uint>();
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Trigger type (0 = generic, 1 = transition, 2 = trap).
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// Linked waypoint/door tag for transitions.
        /// </summary>
        public string LinkedTo { get; set; }

        /// <summary>
        /// Linked module for transitions.
        /// </summary>
        public string LinkedToModule { get; set; }

        /// <summary>
        /// Linked flags.
        /// </summary>
        public int LinkedToFlags { get; set; }

        /// <summary>
        /// Transition destination waypoint tag.
        /// </summary>
        public string TransitionDestination { get; set; }

        /// <summary>
        /// Trigger polygon vertices.
        /// </summary>
        public List<Vector3> Vertices { get; set; }

        // ITriggerComponent implementation
        IList<Vector3> ITriggerComponent.Geometry
        {
            get { return Vertices; }
            set { Vertices = value != null ? new List<Vector3>(value) : new List<Vector3>(); }
        }

        public bool IsEnabled { get; set; } = true;

        public int TriggerType
        {
            get { return Type; }
            set { Type = value; }
        }

        string ITriggerComponent.LinkedTo
        {
            get { return LinkedTo; }
            set { LinkedTo = value ?? string.Empty; }
        }

        string ITriggerComponent.LinkedToModule
        {
            get { return LinkedToModule; }
            set { LinkedToModule = value ?? string.Empty; }
        }

        public bool IsTrap
        {
            get { return TrapFlag; }
            set { TrapFlag = value; }
        }

        public bool TrapActive { get; set; } = true;

        public bool TrapDisarmed { get; set; } = false;

        public int TrapDisarmDC
        {
            get { return DisarmDC; }
            set { DisarmDC = value; }
        }

        public bool FireOnce
        {
            get { return TrapOneShot; }
            set { TrapOneShot = value; }
        }

        public bool HasFired { get; set; } = false;

        /// <summary>
        /// Faction ID.
        /// </summary>
        public int FactionId { get; set; }

        /// <summary>
        /// Whether the trigger has a trap.
        /// </summary>
        public bool TrapFlag { get; set; }

        /// <summary>
        /// Trap type.
        /// </summary>
        public int TrapType { get; set; }

        /// <summary>
        /// Whether the trap is detectable.
        /// </summary>
        public bool TrapDetectable { get; set; }

        /// <summary>
        /// Trap detect DC.
        /// </summary>
        public int TrapDetectDC { get; set; }

        /// <summary>
        /// Whether the trap is disarmable.
        /// </summary>
        public bool TrapDisarmable { get; set; }

        /// <summary>
        /// Trap disarm DC.
        /// </summary>
        public int DisarmDC { get; set; }

        /// <summary>
        /// Whether the trap is detected.
        /// </summary>
        public bool TrapDetected { get; set; }

        /// <summary>
        /// Whether the trap is one-shot.
        /// </summary>
        public bool TrapOneShot { get; set; }

        /// <summary>
        /// Set of entity IDs currently inside this trigger.
        /// </summary>
        public HashSet<uint> EnteredBy { get; set; }

        /// <summary>
        /// Tests if an entity is inside the trigger volume.
        /// </summary>
        bool ITriggerComponent.ContainsEntity(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return false;
            }

            return ContainsPoint(transform.Position);
        }

        /// <summary>
        /// Tests if a point is inside the trigger polygon (ITriggerComponent implementation).
        /// </summary>
        bool ITriggerComponent.ContainsPoint(Vector3 point)
        {
            return ContainsPoint(point);
        }

        /// <summary>
        /// Tests if a point is inside the trigger polygon.
        /// </summary>
        public bool ContainsPoint(Vector3 point)
        {
            if (Vertices.Count < 3)
            {
                return false;
            }

            // Ray casting algorithm for point-in-polygon test (2D projection)
            int crossings = 0;
            for (int i = 0; i < Vertices.Count; i++)
            {
                int j = (i + 1) % Vertices.Count;
                Vector3 v1 = Vertices[i];
                Vector3 v2 = Vertices[j];

                if ((v1.Y <= point.Y && v2.Y > point.Y) ||
                    (v2.Y <= point.Y && v1.Y > point.Y))
                {
                    float x = v1.X + (point.Y - v1.Y) / (v2.Y - v1.Y) * (v2.X - v1.X);
                    if (point.X < x)
                    {
                        crossings++;
                    }
                }
            }

            return (crossings % 2) == 1;
        }

        /// <summary>
        /// Whether this trigger is a module transition.
        /// </summary>
        public bool IsModuleTransition
        {
            get { return Type == 1 && !string.IsNullOrEmpty(LinkedToModule); }
        }

        /// <summary>
        /// Whether this trigger is an area transition.
        /// </summary>
        public bool IsAreaTransition
        {
            get { return Type == 1 && !string.IsNullOrEmpty(LinkedTo) && string.IsNullOrEmpty(LinkedToModule); }
        }
    }
}

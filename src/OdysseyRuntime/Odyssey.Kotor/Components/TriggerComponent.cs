using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for trigger entities.
    /// </summary>
    /// <remarks>
    /// Based on UTT file format documentation.
    /// Triggers are invisible polygonal regions that fire scripts on enter/exit.
    /// </remarks>
    public class TriggerComponent : IComponent
    {
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

using System;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Templates
{
    /// <summary>
    /// Waypoint template implementation for spawning waypoints from UTW data.
    /// </summary>
    /// <remarks>
    /// Waypoint Template:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) waypoint system
    /// - Located via string references: "Waypoint" @ 0x007bc510 (waypoint entity type), "WaypointList" @ 0x007bd060 (GIT waypoint list)
    /// - "STARTWAYPOINT" @ 0x007be034 (start waypoint constant), "Waypoint template %s doesn't exist.\n" @ 0x007c0f24 (template not found error)
    /// - Template loading: 0x004e08e0 @ 0x004e08e0 loads waypoint instances from GIT
    /// - 0x005226d0 @ 0x005226d0 (entity serialization references waypoint templates)
    /// - Original implementation: UTW (Waypoint) GFF templates define waypoint properties
    /// - UTW file format: GFF with "UTW " signature containing waypoint data (Tag, XPosition, YPosition, ZPosition, MapNote, MapNoteEnabled)
    /// - Waypoints are invisible markers used for scripting and navigation (GetWaypointByTag NWScript function)
    /// - Waypoints can have map notes for player reference (MapNote field, MapNoteEnabled flag, displayed on minimap when enabled)
    /// - GetWaypointByTag NWScript function finds waypoints by tag (searches all waypoints in current area)
    /// - Waypoints used for: Module transitions (LinkedTo field), script positioning, area navigation
    /// - Based on UTW file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class WaypointTemplate : IWaypointTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Waypoint; } }
        public string DisplayName { get; set; }
        public bool HasMapNote { get; set; }
        public string MapNote { get; set; }
        public bool MapNoteEnabled { get; set; }

        // Additional properties
        public string Description { get; set; }
        public int AppearanceType { get; set; }
        public string LinkedTo { get; set; }

        #endregion

        public WaypointTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            DisplayName = string.Empty;
            MapNote = string.Empty;
            Description = string.Empty;
            LinkedTo = string.Empty;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var entity = new Entity(ObjectType.Waypoint, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position and facing
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = position;
                transform.Facing = facing;
            }

            // Register in world
            world.RegisterEntity(entity);

            return entity;
        }
    }
}

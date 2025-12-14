using System;
using System.Numerics;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Templates
{
    /// <summary>
    /// Waypoint template implementation for spawning waypoints from UTW data.
    /// </summary>
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
            var transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
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

using System;
using System.Numerics;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Templates
{
    /// <summary>
    /// Sound template implementation for spawning sounds from UTS data.
    /// </summary>
    public class SoundTemplate : ISoundTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Sound; } }
        public string[] Sounds { get; set; }
        public int Volume { get; set; }
        public bool Active { get; set; }
        public bool IsPositional { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }

        // Additional properties
        public string DisplayName { get; set; }
        public bool Looping { get; set; }
        public bool Random { get; set; }
        public bool RandomPosition { get; set; }
        public int Interval { get; set; }
        public int IntervalVariation { get; set; }
        public float PitchVariation { get; set; }
        public int Hours { get; set; }
        public int Priority { get; set; }
        public bool Continuous { get; set; }
        public float RandomRangeX { get; set; }
        public float RandomRangeY { get; set; }
        public float Elevation { get; set; }

        #endregion

        public SoundTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            DisplayName = string.Empty;
            Sounds = new string[0];
            Volume = 127;
            Active = true;
            IsPositional = true;
            MinDistance = 1.0f;
            MaxDistance = 50.0f;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var entity = new Entity(ObjectType.Sound, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position
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

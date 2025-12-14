using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Kotor.Systems
{
    /// <summary>
    /// Perception event type.
    /// </summary>
    public enum PerceptionEventType
    {
        /// <summary>
        /// Entity has been seen.
        /// </summary>
        Seen,

        /// <summary>
        /// Entity was seen but is no longer visible.
        /// </summary>
        Vanished,

        /// <summary>
        /// Entity has been heard.
        /// </summary>
        Heard,

        /// <summary>
        /// Entity was heard but is no longer audible.
        /// </summary>
        Inaudible
    }

    /// <summary>
    /// Event arguments for perception changes.
    /// </summary>
    public class PerceptionEventArgs : EventArgs
    {
        public IEntity Perceiver { get; set; }
        public IEntity Perceived { get; set; }
        public PerceptionEventType EventType { get; set; }
    }

    /// <summary>
    /// Perception data for a single creature.
    /// </summary>
    internal class PerceptionData
    {
        public HashSet<uint> SeenObjects { get; private set; }
        public HashSet<uint> HeardObjects { get; private set; }
        public HashSet<uint> LastSeenObjects { get; private set; }
        public HashSet<uint> LastHeardObjects { get; private set; }

        public PerceptionData()
        {
            SeenObjects = new HashSet<uint>();
            HeardObjects = new HashSet<uint>();
            LastSeenObjects = new HashSet<uint>();
            LastHeardObjects = new HashSet<uint>();
        }

        public void SwapBuffers()
        {
            // Swap current to last for delta detection
            var tempSeen = LastSeenObjects;
            LastSeenObjects = SeenObjects;
            SeenObjects = tempSeen;
            SeenObjects.Clear();

            var tempHeard = LastHeardObjects;
            LastHeardObjects = HeardObjects;
            HeardObjects = tempHeard;
            HeardObjects.Clear();
        }
    }

    /// <summary>
    /// Manages creature perception (sight and hearing).
    /// </summary>
    /// <remarks>
    /// Perception System:
    /// - Each creature has sight and hearing ranges
    /// - Perception is updated periodically (not every frame)
    /// - Events fire when perception state changes:
    ///   - OnPerceive: New object seen/heard
    ///   - OnVanish: Object no longer seen
    ///   - OnInaudible: Object no longer heard
    /// 
    /// Sight checks:
    /// - Distance within sight range
    /// - Line of sight (optional raycasting)
    /// - Not invisible (unless has See Invisibility)
    /// 
    /// Hearing checks:
    /// - Distance within hearing range
    /// - Sound source is active
    /// - Not silenced
    /// </remarks>
    public class PerceptionManager
    {
        private readonly IWorld _world;
        private readonly Dictionary<uint, PerceptionData> _perceptionData;

        /// <summary>
        /// Default sight range in meters.
        /// </summary>
        public const float DefaultSightRange = 20.0f;

        /// <summary>
        /// Default hearing range in meters.
        /// </summary>
        public const float DefaultHearingRange = 15.0f;

        /// <summary>
        /// Update interval in seconds.
        /// </summary>
        public float UpdateInterval { get; set; } = 0.5f;

        private float _timeSinceUpdate;

        /// <summary>
        /// Event fired when perception changes.
        /// </summary>
        public event EventHandler<PerceptionEventArgs> OnPerceptionChanged;

        public PerceptionManager(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _perceptionData = new Dictionary<uint, PerceptionData>();
            _timeSinceUpdate = 0f;
        }

        /// <summary>
        /// Updates perception for all creatures.
        /// </summary>
        public void Update(float deltaTime)
        {
            _timeSinceUpdate += deltaTime;
            if (_timeSinceUpdate < UpdateInterval)
            {
                return;
            }
            _timeSinceUpdate = 0f;

            // Update perception for all creatures
            var creatures = _world.GetEntitiesOfType(ObjectType.Creature);
            foreach (var creature in creatures)
            {
                UpdateCreaturePerception(creature);
            }
        }

        /// <summary>
        /// Updates perception for a single creature.
        /// </summary>
        public void UpdateCreaturePerception(IEntity creature)
        {
            if (creature == null || creature.ObjectType != ObjectType.Creature)
            {
                return;
            }

            // Get or create perception data
            PerceptionData data;
            if (!_perceptionData.TryGetValue(creature.ObjectId, out data))
            {
                data = new PerceptionData();
                _perceptionData[creature.ObjectId] = data;
            }

            // Swap buffers to track changes
            data.SwapBuffers();

            // Get creature's perception component
            var perception = creature.GetComponent<IPerceptionComponent>();
            float sightRange = perception != null ? perception.SightRange : DefaultSightRange;
            float hearingRange = perception != null ? perception.HearingRange : DefaultHearingRange;

            // Get creature position
            var transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            Vector3 position = transform.Position;
            float sightRangeSq = sightRange * sightRange;
            float hearingRangeSq = hearingRange * hearingRange;

            // Check all potential targets
            foreach (var target in _world.GetAllEntities())
            {
                if (target == creature)
                {
                    continue;
                }

                // Skip non-perceivable types
                if (target.ObjectType != ObjectType.Creature &&
                    target.ObjectType != ObjectType.Placeable &&
                    target.ObjectType != ObjectType.Door)
                {
                    continue;
                }

                var targetTransform = target.GetComponent<ITransformComponent>();
                if (targetTransform == null)
                {
                    continue;
                }

                Vector3 targetPosition = targetTransform.Position;
                float distSq = Vector3.DistanceSquared(position, targetPosition);

                // Check sight
                if (distSq <= sightRangeSq && CanSee(creature, target, position, targetPosition))
                {
                    data.SeenObjects.Add(target.ObjectId);
                }

                // Check hearing
                if (distSq <= hearingRangeSq && CanHear(creature, target))
                {
                    data.HeardObjects.Add(target.ObjectId);
                }
            }

            // Fire events for changes
            FirePerceptionEvents(creature, data);

            // Update perception component if present
            if (perception != null)
            {
                foreach (var seenId in data.SeenObjects)
                {
                    var seen = _world.GetEntity(seenId);
                    if (seen != null)
                    {
                        bool wasHeard = data.HeardObjects.Contains(seenId);
                        perception.UpdatePerception(seen, true, wasHeard);
                    }
                }

                foreach (var heardId in data.HeardObjects)
                {
                    if (!data.SeenObjects.Contains(heardId))
                    {
                        var heard = _world.GetEntity(heardId);
                        if (heard != null)
                        {
                            perception.UpdatePerception(heard, false, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if creature can see target.
        /// </summary>
        private bool CanSee(IEntity creature, IEntity target, Vector3 creaturePos, Vector3 targetPos)
        {
            // Basic distance check already done

            // Check if target is invisible
            // TODO: Check for invisibility effect and See Invisibility ability

            // Check line of sight
            // TODO: Raycast through navigation mesh or world geometry

            // For now, just assume visible if in range
            return true;
        }

        /// <summary>
        /// Checks if creature can hear target.
        /// </summary>
        private bool CanHear(IEntity creature, IEntity target)
        {
            // Check if target is making sound
            // - Creatures make sound when moving or fighting
            // - Sounds have a radius

            // Check if creature is deafened
            // TODO: Check for deafness effect

            // For now, assume creatures are always audible if in range
            if (target.ObjectType == ObjectType.Creature)
            {
                return true;
            }

            // Placeables/doors only audible if activated
            return false;
        }

        /// <summary>
        /// Fires perception change events.
        /// </summary>
        private void FirePerceptionEvents(IEntity creature, PerceptionData data)
        {
            // New objects seen
            foreach (var seenId in data.SeenObjects)
            {
                if (!data.LastSeenObjects.Contains(seenId))
                {
                    var seen = _world.GetEntity(seenId);
                    if (seen != null)
                    {
                        OnPerceptionChanged?.Invoke(this, new PerceptionEventArgs
                        {
                            Perceiver = creature,
                            Perceived = seen,
                            EventType = PerceptionEventType.Seen
                        });
                    }
                }
            }

            // Objects that vanished
            foreach (var lastSeenId in data.LastSeenObjects)
            {
                if (!data.SeenObjects.Contains(lastSeenId))
                {
                    var vanished = _world.GetEntity(lastSeenId);
                    if (vanished != null)
                    {
                        OnPerceptionChanged?.Invoke(this, new PerceptionEventArgs
                        {
                            Perceiver = creature,
                            Perceived = vanished,
                            EventType = PerceptionEventType.Vanished
                        });
                    }
                }
            }

            // New objects heard
            foreach (var heardId in data.HeardObjects)
            {
                if (!data.LastHeardObjects.Contains(heardId))
                {
                    var heard = _world.GetEntity(heardId);
                    if (heard != null)
                    {
                        OnPerceptionChanged?.Invoke(this, new PerceptionEventArgs
                        {
                            Perceiver = creature,
                            Perceived = heard,
                            EventType = PerceptionEventType.Heard
                        });
                    }
                }
            }

            // Objects that became inaudible
            foreach (var lastHeardId in data.LastHeardObjects)
            {
                if (!data.HeardObjects.Contains(lastHeardId))
                {
                    var inaudible = _world.GetEntity(lastHeardId);
                    if (inaudible != null)
                    {
                        OnPerceptionChanged?.Invoke(this, new PerceptionEventArgs
                        {
                            Perceiver = creature,
                            Perceived = inaudible,
                            EventType = PerceptionEventType.Inaudible
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Gets all entities currently seen by a creature.
        /// </summary>
        public IEnumerable<IEntity> GetSeenObjects(IEntity creature)
        {
            if (creature == null)
            {
                yield break;
            }

            PerceptionData data;
            if (_perceptionData.TryGetValue(creature.ObjectId, out data))
            {
                foreach (var seenId in data.SeenObjects)
                {
                    var seen = _world.GetEntity(seenId);
                    if (seen != null)
                    {
                        yield return seen;
                    }
                }
            }
        }

        /// <summary>
        /// Gets all entities currently heard by a creature.
        /// </summary>
        public IEnumerable<IEntity> GetHeardObjects(IEntity creature)
        {
            if (creature == null)
            {
                yield break;
            }

            PerceptionData data;
            if (_perceptionData.TryGetValue(creature.ObjectId, out data))
            {
                foreach (var heardId in data.HeardObjects)
                {
                    var heard = _world.GetEntity(heardId);
                    if (heard != null)
                    {
                        yield return heard;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a creature has seen a specific target.
        /// </summary>
        public bool HasSeen(IEntity creature, IEntity target)
        {
            if (creature == null || target == null)
            {
                return false;
            }

            PerceptionData data;
            if (_perceptionData.TryGetValue(creature.ObjectId, out data))
            {
                return data.SeenObjects.Contains(target.ObjectId);
            }
            return false;
        }

        /// <summary>
        /// Checks if a creature has heard a specific target.
        /// </summary>
        public bool HasHeard(IEntity creature, IEntity target)
        {
            if (creature == null || target == null)
            {
                return false;
            }

            PerceptionData data;
            if (_perceptionData.TryGetValue(creature.ObjectId, out data))
            {
                return data.HeardObjects.Contains(target.ObjectId);
            }
            return false;
        }

        /// <summary>
        /// Clears perception data for a creature.
        /// </summary>
        public void ClearPerception(IEntity creature)
        {
            if (creature == null)
            {
                return;
            }

            _perceptionData.Remove(creature.ObjectId);
        }

        /// <summary>
        /// Clears all perception data.
        /// </summary>
        public void ClearAllPerception()
        {
            _perceptionData.Clear();
        }

        /// <summary>
        /// Gets the nearest enemy (hostile creature) for a creature.
        /// </summary>
        public IEntity GetNearestEnemy(IEntity creature, FactionManager factionManager)
        {
            if (creature == null || factionManager == null)
            {
                return null;
            }

            var transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return null;
            }

            Vector3 position = transform.Position;
            IEntity nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (var seen in GetSeenObjects(creature))
            {
                if (seen.ObjectType != ObjectType.Creature)
                {
                    continue;
                }

                if (!factionManager.IsHostile(creature, seen))
                {
                    continue;
                }

                var seenTransform = seen.GetComponent<ITransformComponent>();
                if (seenTransform == null)
                {
                    continue;
                }

                float distSq = Vector3.DistanceSquared(position, seenTransform.Position);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = seen;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Gets the nearest friend (friendly creature) for a creature.
        /// </summary>
        public IEntity GetNearestFriend(IEntity creature, FactionManager factionManager)
        {
            if (creature == null || factionManager == null)
            {
                return null;
            }

            var transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return null;
            }

            Vector3 position = transform.Position;
            IEntity nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (var seen in GetSeenObjects(creature))
            {
                if (seen.ObjectType != ObjectType.Creature)
                {
                    continue;
                }

                if (!factionManager.IsFriendly(creature, seen))
                {
                    continue;
                }

                var seenTransform = seen.GetComponent<ITransformComponent>();
                if (seenTransform == null)
                {
                    continue;
                }

                float distSq = Vector3.DistanceSquared(position, seenTransform.Position);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = seen;
                }
            }

            return nearest;
        }
    }
}

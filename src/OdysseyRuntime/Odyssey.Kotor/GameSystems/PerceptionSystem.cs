using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.GameSystems
{
    /// <summary>
    /// Handles perception (sight/sound) for AI awareness.
    /// </summary>
    /// <remarks>
    /// Perception drives AI behavior and script events.
    /// Creatures detect other creatures based on sight/hearing range.
    /// Line-of-sight is tested through the walkmesh.
    /// </remarks>
    public class PerceptionSystem
    {
        private readonly IWorld _world;
        private readonly Dictionary<uint, PerceptionList> _perceptionLists;
        private readonly List<PerceptionChange> _pendingChanges;

        /// <summary>
        /// Interval between perception checks in seconds.
        /// </summary>
        public const float UpdateInterval = 0.5f;

        /// <summary>
        /// Default sight range in meters.
        /// </summary>
        public const float DefaultSightRange = 20f;

        /// <summary>
        /// Default hearing range in meters.
        /// </summary>
        public const float DefaultHearingRange = 30f;

        /// <summary>
        /// Height offset for line-of-sight tests (eye level).
        /// </summary>
        public const float EyeHeight = 1.6f;

        public event Action<IEntity, IEntity, PerceptionType> OnPerceptionChanged;

        public PerceptionSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _perceptionLists = new Dictionary<uint, PerceptionList>();
            _pendingChanges = new List<PerceptionChange>();
        }

        /// <summary>
        /// Updates perception for all creatures.
        /// </summary>
        public void Update(float deltaTime)
        {
            _pendingChanges.Clear();

            // Get all creatures
            var creatures = _world.GetEntitiesOfType(ObjectType.Creature);

            foreach (var subject in creatures)
            {
                UpdatePerception(subject);
            }

            // Fire perception events
            FirePendingEvents();
        }

        /// <summary>
        /// Updates perception for a single creature.
        /// </summary>
        public void UpdatePerception(IEntity subject)
        {
            if (subject == null)
            {
                return;
            }

            var creature = subject.GetComponent<CreatureComponent>();
            if (creature == null)
            {
                return;
            }

            var transform = subject.GetComponent<TransformComponent>();
            if (transform == null)
            {
                return;
            }

            float sightRange = creature.PerceptionRange > 0 ? creature.PerceptionRange : DefaultSightRange;
            float hearRange = DefaultHearingRange;
            float maxRange = Math.Max(sightRange, hearRange);

            // Get perception list for this creature
            PerceptionList perceptionList = GetOrCreatePerceptionList(subject.ObjectId);

            // Check all nearby creatures
            var nearbyCreatures = _world.GetEntitiesInRadius(transform.Position, maxRange, ObjectType.Creature);

            foreach (var other in nearbyCreatures)
            {
                if (other == subject)
                {
                    continue;
                }

                var otherTransform = other.GetComponent<TransformComponent>();
                if (otherTransform == null)
                {
                    continue;
                }

                bool wasSeen = perceptionList.WasSeen(other.ObjectId);
                bool wasHeard = perceptionList.WasHeard(other.ObjectId);

                bool canSee = CanSee(subject, other, sightRange);
                bool canHear = CanHear(subject, other, hearRange);

                // Record perception changes
                if (canSee && !wasSeen)
                {
                    _pendingChanges.Add(new PerceptionChange
                    {
                        Subject = subject,
                        Object = other,
                        Type = PerceptionType.Seen
                    });
                }
                else if (!canSee && wasSeen)
                {
                    _pendingChanges.Add(new PerceptionChange
                    {
                        Subject = subject,
                        Object = other,
                        Type = PerceptionType.NotSeen
                    });
                }

                if (canHear && !wasHeard)
                {
                    _pendingChanges.Add(new PerceptionChange
                    {
                        Subject = subject,
                        Object = other,
                        Type = PerceptionType.Heard
                    });
                }
                else if (!canHear && wasHeard)
                {
                    _pendingChanges.Add(new PerceptionChange
                    {
                        Subject = subject,
                        Object = other,
                        Type = PerceptionType.NotHeard
                    });
                }

                // Update perception list
                perceptionList.Update(other.ObjectId, canSee, canHear);
            }

            // Remove entries for creatures no longer in range
            perceptionList.RemoveOutOfRange(transform.Position, maxRange * 2, _world);
        }

        private bool CanSee(IEntity subject, IEntity target, float range)
        {
            var subjectTransform = subject.GetComponent<TransformComponent>();
            var targetTransform = target.GetComponent<TransformComponent>();

            if (subjectTransform == null || targetTransform == null)
            {
                return false;
            }

            // Check distance
            float distance = Vector3.Distance(subjectTransform.Position, targetTransform.Position);
            if (distance > range)
            {
                return false;
            }

            // Check visibility (could check for invisibility effects here)
            var targetCreature = target.GetComponent<CreatureComponent>();
            if (targetCreature != null)
            {
                // Check for invisibility, stealth, etc.
            }

            // Line-of-sight check through walkmesh
            var area = _world.CurrentArea;
            if (area != null && area.NavigationMesh != null)
            {
                Vector3 eyePos = subjectTransform.Position + new Vector3(0, 0, EyeHeight);
                Vector3 targetEyePos = targetTransform.Position + new Vector3(0, 0, EyeHeight);

                if (!area.NavigationMesh.TestLineOfSight(eyePos, targetEyePos))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanHear(IEntity subject, IEntity target, float range)
        {
            var subjectTransform = subject.GetComponent<TransformComponent>();
            var targetTransform = target.GetComponent<TransformComponent>();

            if (subjectTransform == null || targetTransform == null)
            {
                return false;
            }

            // Check distance
            float distance = Vector3.Distance(subjectTransform.Position, targetTransform.Position);
            if (distance > range)
            {
                return false;
            }

            // Hearing doesn't require line of sight
            // Could check for silence effects here

            return true;
        }

        private void FirePendingEvents()
        {
            foreach (var change in _pendingChanges)
            {
                // Fire OnPerception script
                if (OnPerceptionChanged != null)
                {
                    OnPerceptionChanged(change.Subject, change.Object, change.Type);
                }
            }
        }

        private PerceptionList GetOrCreatePerceptionList(uint objectId)
        {
            PerceptionList list;
            if (!_perceptionLists.TryGetValue(objectId, out list))
            {
                list = new PerceptionList();
                _perceptionLists[objectId] = list;
            }
            return list;
        }

        /// <summary>
        /// Gets the perception list for an entity.
        /// </summary>
        public PerceptionList GetPerceptionList(IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }
            return GetOrCreatePerceptionList(entity.ObjectId);
        }

        /// <summary>
        /// Clears perception data for an entity (on despawn).
        /// </summary>
        public void ClearPerception(uint objectId)
        {
            _perceptionLists.Remove(objectId);

            // Also remove from other creatures' perception lists
            foreach (var list in _perceptionLists.Values)
            {
                list.Remove(objectId);
            }
        }
    }

    /// <summary>
    /// Stores perception state for a single creature.
    /// </summary>
    public class PerceptionList
    {
        private readonly Dictionary<uint, PerceptionEntry> _entries;

        public PerceptionList()
        {
            _entries = new Dictionary<uint, PerceptionEntry>();
        }

        public bool WasSeen(uint objectId)
        {
            PerceptionEntry entry;
            if (_entries.TryGetValue(objectId, out entry))
            {
                return entry.Seen;
            }
            return false;
        }

        public bool WasHeard(uint objectId)
        {
            PerceptionEntry entry;
            if (_entries.TryGetValue(objectId, out entry))
            {
                return entry.Heard;
            }
            return false;
        }

        public void Update(uint objectId, bool seen, bool heard)
        {
            PerceptionEntry entry;
            if (!_entries.TryGetValue(objectId, out entry))
            {
                entry = new PerceptionEntry();
                _entries[objectId] = entry;
            }

            entry.Seen = seen;
            entry.Heard = heard;
            entry.LastUpdateTime = DateTime.UtcNow;
        }

        public void Remove(uint objectId)
        {
            _entries.Remove(objectId);
        }

        public void RemoveOutOfRange(Vector3 position, float maxRange, IWorld world)
        {
            var toRemove = new List<uint>();

            foreach (var kvp in _entries)
            {
                uint objectId = kvp.Key;
                var entity = world.GetEntity(objectId);

                if (entity == null)
                {
                    toRemove.Add(objectId);
                    continue;
                }

                var transform = entity.GetComponent<TransformComponent>();
                if (transform == null)
                {
                    toRemove.Add(objectId);
                    continue;
                }

                float distance = Vector3.Distance(position, transform.Position);
                if (distance > maxRange)
                {
                    toRemove.Add(objectId);
                }
            }

            foreach (uint id in toRemove)
            {
                _entries.Remove(id);
            }
        }

        /// <summary>
        /// Gets all objects that are currently seen.
        /// </summary>
        public IEnumerable<uint> GetSeenObjects()
        {
            foreach (var kvp in _entries)
            {
                if (kvp.Value.Seen)
                {
                    yield return kvp.Key;
                }
            }
        }

        /// <summary>
        /// Gets all objects that are currently heard.
        /// </summary>
        public IEnumerable<uint> GetHeardObjects()
        {
            foreach (var kvp in _entries)
            {
                if (kvp.Value.Heard)
                {
                    yield return kvp.Key;
                }
            }
        }
    }

    /// <summary>
    /// Perception state for a single perceived object.
    /// </summary>
    public class PerceptionEntry
    {
        public bool Seen { get; set; }
        public bool Heard { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// Perception change event data.
    /// </summary>
    public struct PerceptionChange
    {
        public IEntity Subject;
        public IEntity Object;
        public PerceptionType Type;
    }

    /// <summary>
    /// Types of perception events.
    /// </summary>
    public enum PerceptionType
    {
        Seen,
        NotSeen,
        Heard,
        NotHeard
    }
}

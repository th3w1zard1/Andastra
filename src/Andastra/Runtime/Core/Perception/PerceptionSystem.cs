using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Perception
{
    /// <summary>
    /// Perception system for sight and hearing detection.
    /// </summary>
    /// <remarks>
    /// Perception System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) perception system
    /// - Located via string references: "OnPerception" @ 0x007bee80 (perception script field), "OnNotice" @ 0x007beea0 (notice script field)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68 (perception script event type, 0x1)
    /// - "PerceptionData" @ 0x007bf6c4 (perception data structure), "PerceptionList" @ 0x007bf6d4 (perception list field)
    /// - "PERCEPTIONDIST" @ 0x007c4070 (perception distance field), "PerceptionRange" @ 0x007c4080 (perception range field)
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 saves perception data including sight/hearing ranges
    /// - Perception checks: Entities check sight/hearing ranges periodically (every 0.5 seconds)
    /// - Line-of-sight: Raycast through walkmesh to determine if target is visible (uses NavigationMesh.Raycast)
    /// - Perception events: OnPerception fires when entity sees/hears another entity for first time
    /// - OnNotice fires when entity notices another entity (combat awareness)
    /// - Sight range: Default 10.0 units, configurable per entity via IPerceptionComponent (PERCEPTIONDIST field)
    /// - Hearing range: Default 20.0 units, configurable per entity via IPerceptionComponent
    /// - Eye height: Default 1.5 units above entity position for line-of-sight checks (used for raycast origin)
    /// - Perception state: Tracks which entities were seen/heard to detect state changes (first-time perception)
    /// </remarks>
    public class PerceptionSystem
    {
        private const float UpdateInterval = 0.5f; // seconds between perception checks
        private const float DefaultEyeHeight = 1.5f; // units above entity position

        private readonly IWorld _world;
        private readonly Dictionary<IEntity, PerceptionState> _perceptionStates;
        private float _updateTimer;

        public PerceptionSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _perceptionStates = new Dictionary<IEntity, PerceptionState>();
            _updateTimer = 0f;
        }

        /// <summary>
        /// Updates the perception system.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds.</param>
        public void Update(float deltaTime)
        {
            _updateTimer += deltaTime;

            if (_updateTimer >= UpdateInterval)
            {
                _updateTimer -= UpdateInterval;
                ProcessPerceptionChecks();
            }
        }

        /// <summary>
        /// Processes perception checks for all entities.
        /// </summary>
        private void ProcessPerceptionChecks()
        {
            foreach (IEntity entity in _world.GetAllEntities())
            {
                IPerceptionComponent perception = entity.GetComponent<IPerceptionComponent>();
                if (perception == null)
                {
                    continue;
                }

                UpdatePerception(entity, perception);
            }
        }

        /// <summary>
        /// Updates perception for a single entity.
        /// </summary>
        private void UpdatePerception(IEntity subject, IPerceptionComponent perception)
        {
            float sightRange = perception.SightRange;
            float hearRange = perception.HearingRange;

            // Get all entities in perception range
            float maxRange = Math.Max(sightRange, hearRange);
            ITransformComponent subjectTransform = subject.GetComponent<ITransformComponent>();
            if (subjectTransform == null)
            {
                return;
            }
            IEnumerable<IEntity> nearbyEntities = _world.GetEntitiesInRadius(subjectTransform.Position, maxRange);

            // Get or create perception state
            if (!_perceptionStates.TryGetValue(subject, out PerceptionState state))
            {
                state = new PerceptionState();
                _perceptionStates[subject] = state;
            }

            foreach (IEntity other in nearbyEntities)
            {
                if (other == subject || !other.IsValid)
                {
                    continue;
                }

                bool wasSeen = state.WasSeen(other);
                bool wasHeard = state.WasHeard(other);

                bool canSee = CanSee(subject, other, sightRange);
                bool canHear = CanHear(subject, other, hearRange);

                // Fire perception events
                if (canSee && !wasSeen)
                {
                    FirePerceptionEvent(subject, other, PerceptionType.Seen);
                }

                if (!canSee && wasSeen)
                {
                    FirePerceptionEvent(subject, other, PerceptionType.NotSeen);
                }

                if (canHear && !wasHeard)
                {
                    FirePerceptionEvent(subject, other, PerceptionType.Heard);
                }

                // Update perception state
                state.Update(other, canSee, canHear);
            }
        }

        /// <summary>
        /// Checks if subject can see target.
        /// </summary>
        private bool CanSee(IEntity subject, IEntity target, float range)
        {
            ITransformComponent subjectTransform = subject.GetComponent<ITransformComponent>();
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (subjectTransform == null || targetTransform == null)
            {
                return false;
            }

            float dist = Vector3.Distance(subjectTransform.Position, targetTransform.Position);
            if (dist > range)
            {
                return false;
            }

            // Check if target is visible
            IRenderableComponent renderable = target.GetComponent<IRenderableComponent>();
            if (renderable != null && !renderable.Visible)
            {
                return false;
            }

            // Line-of-sight check through walkmesh
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Eye height uses Z axis (vertical) - TransformComponent.Up = UnitZ
            // Located via string references: Position offsets use Z for vertical, Y for horizontal (2D movement plane)
            // Original implementation: Eye position offset by DefaultEyeHeight along Z axis (vertical)
            Vector3 eyePos = subjectTransform.Position + Vector3.UnitZ * DefaultEyeHeight;
            Vector3 targetEyePos = targetTransform.Position + Vector3.UnitZ * DefaultEyeHeight;
            Vector3 direction = targetEyePos - eyePos;
            float distance = direction.Length();

            // Raycast through walkmesh to check line-of-sight
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Line-of-sight raycast implementation
            // Located via string references: "Raycast" @ navigation mesh functions
            // Original implementation: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts for visibility checks
            // Walking collision function: UpdateCreatureMovement @ 0x0054be70 handles creature collision and line-of-sight checks
            // Located via string reference: "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
            if (_world.CurrentArea != null && _world.CurrentArea.NavigationMesh != null)
            {
                if (distance > 0.1f)
                {
                    direction = Vector3.Normalize(direction);
                    Vector3 hitPoint;
                    int hitFace;
                    if (_world.CurrentArea.NavigationMesh.Raycast(eyePos, direction, distance, out hitPoint, out hitFace))
                    {
                        // Something blocked the line-of-sight
                        // Check if the hit point is very close to the target (within tolerance)
                        float hitDist = Vector3.Distance(eyePos, hitPoint);
                        float targetDist = Vector3.Distance(eyePos, targetEyePos);
                        // Allow small tolerance for walkmesh precision
                        return (targetDist - hitDist) < 0.5f;
                    }
                }
                // Raycast didn't hit anything, line-of-sight is clear
                return true;
            }

            // No navigation mesh available, assume line-of-sight is clear if within range
            return true;
        }

        /// <summary>
        /// Checks if subject can hear target.
        /// </summary>
        private bool CanHear(IEntity subject, IEntity target, float range)
        {
            ITransformComponent subjectTransform = subject.GetComponent<ITransformComponent>();
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (subjectTransform == null || targetTransform == null)
            {
                return false;
            }

            float dist = Vector3.Distance(subjectTransform.Position, targetTransform.Position);
            if (dist > range)
            {
                return false;
            }

            // Hearing doesn't require line-of-sight, just distance
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Hearing perception with occlusion checks
            // Located via string references: Hearing range checks in perception system
            // Original implementation: Hearing can be occluded by walls/doors, but has longer range than sight
            // Check for occlusion through walkmesh (walls block sound, doors may block depending on state)
            if (_world.CurrentArea != null && _world.CurrentArea.NavigationMesh != null)
            {
                Vector3 subjectPos = subjectTransform.Position;
                Vector3 targetPos = targetTransform.Position;
                Vector3 direction = targetPos - subjectPos;
                float distance = direction.Length();

                if (distance > 0.1f)
                {
                    direction = Vector3.Normalize(direction);
                    Vector3 hitPoint;
                    int hitFace;
                    // Raycast to check for walls/obstacles between subject and target
                    if (_world.CurrentArea.NavigationMesh.Raycast(subjectPos, direction, distance, out hitPoint, out hitFace))
                    {
                        // Something is blocking - check if it's a door that might be open
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Sound occlusion through walls and doors
                        // Located via string references: Door entity checks in perception system
                        // Original implementation: Doors that are open allow sound to pass, closed doors and walls block sound
                        // Check for door entities near the hit point
                        const float doorCheckRadius = 2.0f; // Check for doors within 2 units of hit point
                        IEnumerable<IEntity> nearbyDoors = _world.GetEntitiesInRadius(hitPoint, doorCheckRadius, ObjectType.Door);

                        bool foundDoor = false;
                        foreach (IEntity doorEntity in nearbyDoors)
                        {
                            if (doorEntity == null || !doorEntity.IsValid)
                            {
                                continue;
                            }

                            // Found a door entity near the hit point
                            foundDoor = true;

                            // Check if door is open
                            IDoorComponent doorComponent = doorEntity.GetComponent<IDoorComponent>();
                            if (doorComponent != null && doorComponent.IsOpen)
                            {
                                // Door is open - allow sound to pass through
                                return true;
                            }
                            else
                            {
                                // Door is closed - block sound
                                return false;
                            }
                        }

                        // No door found at hit point - it's a wall, fully block sound
                        if (!foundDoor)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Fires a perception event.
        /// </summary>
        private void FirePerceptionEvent(IEntity subject, IEntity target, PerceptionType type)
        {
            // Fire OnPerception script
            IScriptHooksComponent scriptHooks = subject.GetComponent<IScriptHooksComponent>();
            string script = scriptHooks?.GetScript(ScriptEvent.OnPerception);
            if (!string.IsNullOrEmpty(script))
            {
                // Execute perception script with subject as owner and target as triggerer
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Perception script execution
                // Located via string references: "OnPerception" @ 0x007bee80, "OnNotice" @ 0x007beea0
                // Original implementation: FUN_004dfbb0 @ 0x004dfbb0 executes perception scripts when entities are detected
                if (_world.EventBus != null)
                {
                    _world.EventBus.FireScriptEvent(subject, ScriptEvent.OnPerception, target);
                }
            }

            // Fire world event
            if (_world.EventBus != null)
            {
                _world.EventBus.Publish(new PerceptionEvent
                {
                    Subject = subject,
                    Target = target,
                    Type = type
                });
            }
        }

        /// <summary>
        /// Clears perception state for an entity (when destroyed).
        /// </summary>
        public void ClearPerceptionState(IEntity entity)
        {
            _perceptionStates.Remove(entity);
        }
    }

    /// <summary>
    /// Perception state for an entity.
    /// </summary>
    internal class PerceptionState
    {
        private readonly Dictionary<IEntity, bool> _seenEntities;
        private readonly Dictionary<IEntity, bool> _heardEntities;

        public PerceptionState()
        {
            _seenEntities = new Dictionary<IEntity, bool>();
            _heardEntities = new Dictionary<IEntity, bool>();
        }

        public bool WasSeen(IEntity entity)
        {
            return _seenEntities.TryGetValue(entity, out bool seen) && seen;
        }

        public bool WasHeard(IEntity entity)
        {
            return _heardEntities.TryGetValue(entity, out bool heard) && heard;
        }

        public void Update(IEntity entity, bool canSee, bool canHear)
        {
            _seenEntities[entity] = canSee;
            _heardEntities[entity] = canHear;
        }
    }

    /// <summary>
    /// Type of perception.
    /// </summary>
    public enum PerceptionType
    {
        Seen,
        NotSeen,
        Heard
    }

    /// <summary>
    /// Perception event.
    /// </summary>
    public class PerceptionEvent : Interfaces.IGameEvent
    {
        public IEntity Subject { get; set; }
        public IEntity Target { get; set; }
        public PerceptionType Type { get; set; }

        /// <summary>
        /// The entity this event relates to (the subject).
        /// </summary>
        public IEntity Entity { get { return Subject; } }
    }
}


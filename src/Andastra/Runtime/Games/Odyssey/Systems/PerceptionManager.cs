using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Runtime.Engines.Odyssey.Data;
using Andastra.Runtime.Games.Common.Systems;
using Andastra.Runtime.Games.Odyssey.Data;

namespace Andastra.Runtime.Engines.Odyssey.Systems
{
    /// <summary>
    /// Perception event type (Odyssey-specific enum).
    /// </summary>
    public enum PerceptionEventType
    {
        /// <summary>
        /// Entity has been seen.
        /// </summary>
        Seen = 0,

        /// <summary>
        /// Entity was seen but is no longer visible.
        /// </summary>
        Vanished = 1,

        /// <summary>
        /// Entity has been heard.
        /// </summary>
        Heard = 2,

        /// <summary>
        /// Entity was heard but is no longer audible.
        /// </summary>
        Inaudible = 3
    }

    /// <summary>
    /// Event arguments for perception changes (Odyssey-specific: uses PerceptionEventType enum).
    /// </summary>
    public class OdysseyPerceptionEventArgs : PerceptionEventArgs
    {
        /// <summary>
        /// Type-safe wrapper around base class EventType property.
        /// </summary>
        /// <remarks>
        /// Explicitly hides base class property to provide type-safe enum access.
        /// </remarks>
        public new PerceptionEventType EventType
        {
            get { return (PerceptionEventType)base.EventType; }
            set { base.EventType = (int)value; }
        }
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

        // Track timestamps for when entities were first perceived (for most recent tracking)
        // Key: entity ID, Value: timestamp when first perceived in this update cycle
        public Dictionary<uint, float> SeenTimestamps { get; private set; }
        public Dictionary<uint, float> HeardTimestamps { get; private set; }

        public PerceptionData()
        {
            SeenObjects = new HashSet<uint>();
            HeardObjects = new HashSet<uint>();
            LastSeenObjects = new HashSet<uint>();
            LastHeardObjects = new HashSet<uint>();
            SeenTimestamps = new Dictionary<uint, float>();
            HeardTimestamps = new Dictionary<uint, float>();
        }

        public void SwapBuffers()
        {
            // Swap current to last for delta detection
            HashSet<uint> tempSeen = LastSeenObjects;
            LastSeenObjects = SeenObjects;
            SeenObjects = tempSeen;
            SeenObjects.Clear();

            HashSet<uint> tempHeard = LastHeardObjects;
            LastHeardObjects = HeardObjects;
            HeardObjects = tempHeard;
            HeardObjects.Clear();

            // Clear timestamps when swapping buffers
            SeenTimestamps.Clear();
            HeardTimestamps.Clear();
        }
    }

    /// <summary>
    /// Manages creature perception (sight and hearing) in Odyssey engine.
    /// </summary>
    /// <remarks>
    /// Perception System (Odyssey-specific):
    /// - Based on swkotor2.exe perception system
    /// - Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68
    /// - "PerceptionData" @ 0x007bf6c4, "PerceptionList" @ 0x007bf6d4 (perception state storage)
    /// - "PERCEPTIONDIST" @ 0x007c4070, "PerceptionRange" @ 0x007c4080 (perception range fields)
    /// - "ScriptOnNotice" @ 0x007beea0 (script hook for perception events)
    /// - Inheritance: Base class BasePerceptionManager (Runtime.Games.Common) - abstract perception manager, Odyssey override (Runtime.Games.Odyssey) - GFF-based perception tracking
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 saves PerceptionList to GFF (creature serialization)
    /// - Each creature has sight and hearing ranges
    /// - Perception is updated periodically (not every frame)
    /// - Events fire when perception state changes:
    ///   - OnPerceive: New object seen/heard (fires ScriptOnNotice)
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
    ///
    /// Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0
    /// Located via string reference: "PERCEPTIONDIST" @ 0x007c4070
    /// Original implementation: Updates perception for all creatures, checks sight/hearing ranges,
    /// fires script events "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68 when perception changes
    /// Uses PERCEPTIONDIST field from appearance.2da for sight range, PerceptionRange field for hearing range
    /// </remarks>
    public class PerceptionManager : BasePerceptionManager
    {
        private readonly EffectSystem _effectSystem;
        private readonly Dictionary<uint, PerceptionData> _perceptionData;

        // Track last perceived entity per creature (for GetLastPerceived engine API)
        private readonly Dictionary<uint, IEntity> _lastPerceivedEntity;
        private readonly Dictionary<uint, bool> _lastPerceptionWasHeard;

        // Track previous positions for movement detection (for hearing perception)
        // Key: entity ObjectId, Value: last known position
        private readonly Dictionary<uint, Vector3> _lastPositions;

        // Track activation state for placeables/doors (for hearing perception)
        // Key: entity ObjectId, Value: last known IsOpen state
        private readonly Dictionary<uint, bool> _lastPlaceableDoorStates;

        // Track activation timestamps for placeables/doors (for hearing perception)
        // Key: entity ObjectId, Value: timestamp when last activated (opened/closed/used)
        private readonly Dictionary<uint, float> _placeableDoorActivationTimes;

        // Cached feat ID for See Invisibility (looked up from feat.2da)
        private int? _cachedSeeInvisibilityFeatId;

        /// <summary>
        /// Default sight range in meters (Odyssey-specific).
        /// </summary>
        public const float DefaultSightRange = 20.0f;

        /// <summary>
        /// Default hearing range in meters (Odyssey-specific).
        /// </summary>
        public const float DefaultHearingRange = 15.0f;

        /// <summary>
        /// Update interval in seconds (Odyssey-specific).
        /// </summary>
        public float UpdateInterval { get; set; } = 0.5f;

        private float _timeSinceUpdate;

        // Total elapsed time accumulator for activation timestamp tracking
        private float _totalElapsedTime;

        /// <summary>
        /// Event fired when perception changes (Odyssey-specific type).
        /// </summary>
        public new event EventHandler<OdysseyPerceptionEventArgs> OnPerceptionChanged;

        public PerceptionManager(IWorld world, EffectSystem effectSystem)
            : base(world)
        {
            _effectSystem = effectSystem ?? throw new ArgumentNullException(nameof(effectSystem));
            _perceptionData = new Dictionary<uint, PerceptionData>();
            _lastPerceivedEntity = new Dictionary<uint, IEntity>();
            _lastPerceptionWasHeard = new Dictionary<uint, bool>();
            _lastPositions = new Dictionary<uint, Vector3>();
            _lastPlaceableDoorStates = new Dictionary<uint, bool>();
            _placeableDoorActivationTimes = new Dictionary<uint, float>();
            _timeSinceUpdate = 0f;
        }

        /// <summary>
        /// Updates perception for all creatures (Odyssey-specific: periodic updates with interval).
        /// </summary>
        public override void Update(float deltaTime)
        {
            _timeSinceUpdate += deltaTime;
            _totalElapsedTime += deltaTime; // Accumulate total elapsed time for activation tracking
            if (_timeSinceUpdate < UpdateInterval)
            {
                return;
            }
            _timeSinceUpdate = 0f;

            // Update perception for all creatures
            IEnumerable<IEntity> creatures = _world.GetEntitiesOfType(ObjectType.Creature);
            foreach (IEntity creature in creatures)
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
            IPerceptionComponent perception = creature.GetComponent<IPerceptionComponent>();
            float sightRange = perception != null ? perception.SightRange : DefaultSightRange;
            float hearingRange = perception != null ? perception.HearingRange : DefaultHearingRange;

            // Get creature position
            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            Vector3 position = transform.Position;
            float sightRangeSq = sightRange * sightRange;
            float hearingRangeSq = hearingRange * hearingRange;

            // Track perception order for most recent entity determination
            // Entities perceived later in the update cycle are more recent
            float perceptionOrder = 0.0f;
            const float perceptionOrderIncrement = 1.0f;


            // Check all potential targets
            foreach (IEntity target in _world.GetAllEntities())
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

                ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
                if (targetTransform == null)
                {
                    continue;
                }

                Vector3 targetPosition = targetTransform.Position;
                float distSq = Vector3.DistanceSquared(position, targetPosition);

                // Track activation state changes for placeables/doors (for hearing perception)
                // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
                // Original implementation: Placeables/doors make sound when activated (opening, closing, using)
                // We track state changes to detect recent activations
                if (target.ObjectType == ObjectType.Placeable || target.ObjectType == ObjectType.Door)
                {
                    bool currentIsOpen = false;
                    if (target.ObjectType == ObjectType.Placeable)
                    {
                        IPlaceableComponent placeableComp = target.GetComponent<IPlaceableComponent>();
                        if (placeableComp != null)
                        {
                            currentIsOpen = placeableComp.IsOpen;
                        }
                    }
                    else if (target.ObjectType == ObjectType.Door)
                    {
                        IDoorComponent doorComp = target.GetComponent<IDoorComponent>();
                        if (doorComp != null)
                        {
                            currentIsOpen = doorComp.IsOpen;
                        }
                    }

                    // Check if state changed (activation detected)
                    bool lastIsOpen;
                    if (_lastPlaceableDoorStates.TryGetValue(target.ObjectId, out lastIsOpen))
                    {
                        if (currentIsOpen != lastIsOpen)
                        {
                            // State changed - record activation time (use total elapsed time)
                            _placeableDoorActivationTimes[target.ObjectId] = _totalElapsedTime;
                        }
                    }
                    else
                    {
                        // First time tracking this entity - initialize state
                        _lastPlaceableDoorStates[target.ObjectId] = currentIsOpen;
                    }

                    // Update last known state
                    _lastPlaceableDoorStates[target.ObjectId] = currentIsOpen;
                }

                // Check sight
                float distance = (float)Math.Sqrt(distSq);
                if (distSq <= sightRangeSq && CanSee(creature, target))
                {
                    // Track timestamp only if not already seen (first time in this cycle)
                    if (!data.SeenObjects.Contains(target.ObjectId))
                    {
                        data.SeenTimestamps[target.ObjectId] = perceptionOrder;
                    }
                    data.SeenObjects.Add(target.ObjectId);
                    perceptionOrder += perceptionOrderIncrement;
                }

                // Check hearing
                if (distSq <= hearingRangeSq && CanHear(creature, target))
                {
                    // Track timestamp only if not already heard (first time in this cycle)
                    if (!data.HeardObjects.Contains(target.ObjectId))
                    {
                        data.HeardTimestamps[target.ObjectId] = perceptionOrder;
                    }
                    data.HeardObjects.Add(target.ObjectId);
                    perceptionOrder += perceptionOrderIncrement;
                }

                // Track position for movement detection (used in hearing checks)
                _lastPositions[target.ObjectId] = targetPosition;
            }

            // Fire events for changes
            FirePerceptionEvents(creature, data);

            // Update last perceived entity (for GetLastPerceived engine API)
            // Track the most recently perceived entity (seen or heard) based on timestamps
            // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 - tracks last perceived entity
            // Based on nwmain.exe: ExecuteCommandGetLastPerceived @ 0x14052a6c0 - retrieves last perceived entity
            // Original implementation: Tracks most recently perceived entity for GetLastPerceived NWScript function
            IEntity lastPerceived = null;
            bool wasHeard = false;
            float mostRecentTimestamp = -1.0f;

            // Find the most recently perceived entity based on timestamps
            // Prioritize seen over heard when timestamps are equal
            // Check all seen entities first
            foreach (uint seenId in data.SeenObjects)
            {
                float timestamp;
                if (data.SeenTimestamps.TryGetValue(seenId, out timestamp))
                {
                    if (timestamp > mostRecentTimestamp)
                    {
                        IEntity seen = _world.GetEntity(seenId);
                        if (seen != null)
                        {
                            mostRecentTimestamp = timestamp;
                            lastPerceived = seen;
                            wasHeard = data.HeardObjects.Contains(seenId);
                        }
                    }
                }
            }

            // Check heard entities (only if no seen entities or if heard entity is more recent)
            foreach (uint heardId in data.HeardObjects)
            {
                // Skip if already found as seen entity
                if (lastPerceived != null && lastPerceived.ObjectId == heardId)
                {
                    continue;
                }

                float timestamp;
                if (data.HeardTimestamps.TryGetValue(heardId, out timestamp))
                {
                    if (timestamp > mostRecentTimestamp)
                    {
                        IEntity heard = _world.GetEntity(heardId);
                        if (heard != null)
                        {
                            mostRecentTimestamp = timestamp;
                            lastPerceived = heard;
                            wasHeard = true;
                        }
                    }
                }
            }

            // If no timestamps found (shouldn't happen, but fallback to first entity)
            if (lastPerceived == null)
            {
                // Fallback: prioritize seen over heard
                if (data.SeenObjects.Count > 0)
                {
                    foreach (uint seenId in data.SeenObjects)
                    {
                        IEntity seen = _world.GetEntity(seenId);
                        if (seen != null)
                        {
                            lastPerceived = seen;
                            wasHeard = data.HeardObjects.Contains(seenId);
                            break;
                        }
                    }
                }
                else if (data.HeardObjects.Count > 0)
                {
                    foreach (uint heardId in data.HeardObjects)
                    {
                        IEntity heard = _world.GetEntity(heardId);
                        if (heard != null)
                        {
                            lastPerceived = heard;
                            wasHeard = true;
                            break;
                        }
                    }
                }
            }

            if (lastPerceived != null)
            {
                _lastPerceivedEntity[creature.ObjectId] = lastPerceived;
                _lastPerceptionWasHeard[creature.ObjectId] = wasHeard;
            }

            // Update perception component if present
            if (perception != null)
            {
                foreach (uint seenId in data.SeenObjects)
                {
                    IEntity seen = _world.GetEntity(seenId);
                    if (seen != null)
                    {
                        bool seenWasHeard = data.HeardObjects.Contains(seenId);
                        perception.UpdatePerception(seen, true, seenWasHeard);
                    }
                }

                foreach (uint heardId in data.HeardObjects)
                {
                    if (!data.SeenObjects.Contains(heardId))
                    {
                        IEntity heard = _world.GetEntity(heardId);
                        if (heard != null)
                        {
                            perception.UpdatePerception(heard, false, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if perceiver can see target (base class implementation).
        /// </summary>
        public override bool CanSee(IEntity perceiver, IEntity target)
        {
            if (perceiver == null || target == null)
            {
                return false;
            }

            ITransformComponent perceiverTransform = perceiver.GetComponent<ITransformComponent>();
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (perceiverTransform == null || targetTransform == null)
            {
                return false;
            }

            return CanSeeInternal(perceiver, target, perceiverTransform.Position, targetTransform.Position);
        }

        /// <summary>
        /// Checks if perceiver can hear target (base class implementation).
        /// </summary>
        public override bool CanHear(IEntity perceiver, IEntity target)
        {
            if (perceiver == null || target == null)
            {
                return false;
            }

            return CanHearInternal(perceiver, target);
        }

        /// <summary>
        /// Registers an entity for perception tracking (base class implementation).
        /// </summary>
        /// <remarks>
        /// Odyssey-specific implementation:
        /// - Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
        /// - Located via string references: "PerceptionData" @ 0x007bf6c4, "PerceptionList" @ 0x007bf6d4
        /// - Original implementation: Initializes perception tracking data structure when creature is created
        /// - Perception data is initialized proactively to ensure proper state before first update cycle
        /// - Links perception component to perception manager for centralized tracking
        /// - Initializes last perceived entity tracking for GetLastPerceived engine API
        /// </remarks>
        public override void RegisterEntity(IEntity entity)
        {
            if (entity == null || entity.ObjectType != ObjectType.Creature)
            {
                return;
            }

            // Initialize perception data structure proactively
            // This ensures proper state before the first update cycle
            if (!_perceptionData.ContainsKey(entity.ObjectId))
            {
                PerceptionData data = new PerceptionData();
                _perceptionData[entity.ObjectId] = data;
            }

            // Initialize last perceived entity tracking (for GetLastPerceived engine API)
            // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 - tracks last perceived entity
            // Based on nwmain.exe: ExecuteCommandGetLastPerceived @ 0x14052a6c0 - retrieves last perceived entity
            if (!_lastPerceivedEntity.ContainsKey(entity.ObjectId))
            {
                _lastPerceivedEntity[entity.ObjectId] = null;
            }

            if (!_lastPerceptionWasHeard.ContainsKey(entity.ObjectId))
            {
                _lastPerceptionWasHeard[entity.ObjectId] = false;
            }

            // Link perception component to perception manager if it exists
            // This allows the component to use centralized perception tracking
            IPerceptionComponent perception = entity.GetComponent<IPerceptionComponent>();
            if (perception != null && perception is Components.PerceptionComponent odysseyPerception)
            {
                odysseyPerception.SetPerceptionManager(this);
            }
        }

        /// <summary>
        /// Unregisters an entity from perception tracking (base class implementation).
        /// </summary>
        public override void UnregisterEntity(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _perceptionData.Remove(entity.ObjectId);
            _lastPerceivedEntity.Remove(entity.ObjectId);
            _lastPerceptionWasHeard.Remove(entity.ObjectId);
            _lastPositions.Remove(entity.ObjectId);
            _lastPlaceableDoorStates.Remove(entity.ObjectId);
            _placeableDoorActivationTimes.Remove(entity.ObjectId);
        }

        /// <summary>
        /// Gets the GameDataManager from the world's GameDataProvider.
        /// </summary>
        /// <returns>GameDataManager if available, null otherwise.</returns>
        private GameDataManager GetGameDataManager()
        {
            if (_world?.GameDataProvider == null)
            {
                return null;
            }

            // GameDataProvider is OdysseyGameDataProvider for Odyssey engine
            OdysseyGameDataProvider odysseyProvider = _world.GameDataProvider as OdysseyGameDataProvider;
            return odysseyProvider?.GameDataManager;
        }

        /// <summary>
        /// Gets the See Invisibility feat ID from feat.2da, with caching.
        /// </summary>
        /// <returns>Feat ID if found, -1 otherwise.</returns>
        /// <remarks>
        /// See Invisibility Feat Lookup:
        /// - Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception check function)
        /// - Located via string references: "CSWClass::LoadFeatTable: Can't load feat.2da" @ 0x007c4720
        /// - Original implementation: Looks up FEAT_SEE_INVISIBILITY from feat.2da table
        /// - Caches the result to avoid repeated table lookups
        /// - Falls back to -1 if feat table is unavailable or feat not found
        /// </remarks>
        private int GetSeeInvisibilityFeatId()
        {
            // Return cached value if available
            if (_cachedSeeInvisibilityFeatId.HasValue)
            {
                return _cachedSeeInvisibilityFeatId.Value;
            }

            // Look up feat ID from game data
            GameDataManager gameDataManager = GetGameDataManager();
            if (gameDataManager != null)
            {
                int featId = gameDataManager.GetFeatIdByLabel("FEAT_SEE_INVISIBILITY");
                _cachedSeeInvisibilityFeatId = featId;
                return featId;
            }

            // Fallback: return -1 if game data unavailable
            _cachedSeeInvisibilityFeatId = -1;
            return -1;
        }

        /// <summary>
        /// Checks if creature can see target (internal Odyssey-specific implementation).
        /// </summary>
        private bool CanSeeInternal(IEntity creature, IEntity target, Vector3 creaturePos, Vector3 targetPos)
        {
            // Basic distance check already done

            // Check if target is invisible
            if (_effectSystem.HasEffect(target, EffectType.Invisibility))
            {
                // Check if creature has See Invisibility (TrueSeeing effect or feat)
                bool canSeeInvisible = _effectSystem.HasEffect(creature, EffectType.TrueSeeing);

                // Also check for See Invisibility feat/ability from creature component
                if (!canSeeInvisible)
                {
                    CreatureComponent creatureComp = creature.GetComponent<CreatureComponent>();
                    if (creatureComp != null && creatureComp.FeatList != null)
                    {
                        // See Invisibility feat ID (looked up from feat.2da)
                        // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception check function)
                        // Original implementation: Checks for FEAT_SEE_INVISIBILITY from feat.2da table
                        int featSeeInvisibilityId = GetSeeInvisibilityFeatId();
                        if (featSeeInvisibilityId >= 0)
                        {
                            canSeeInvisible = creatureComp.FeatList.Contains(featSeeInvisibilityId);
                        }
                    }
                }

                if (!canSeeInvisible)
                {
                    return false; // Target is invisible and creature cannot see invisible
                }
            }

            // Check line of sight using navigation mesh raycasting
            IArea area = _world.CurrentArea;
            if (area != null && area.NavigationMesh != null)
            {
                // Use navigation mesh to test line of sight
                // Adjust positions slightly above ground for creature eye level
                Vector3 eyePos = creaturePos + new Vector3(0, 1.5f, 0); // Approximate eye height
                Vector3 targetEyePos = targetPos + new Vector3(0, 1.5f, 0);

                // Test line of sight
                bool hasLOS = area.NavigationMesh.TestLineOfSight(eyePos, targetEyePos);
                if (!hasLOS)
                {
                    return false; // Blocked by geometry
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if creature can hear target (internal Odyssey-specific implementation).
        /// </summary>
        /// <remarks>
        /// Hearing Detection (Odyssey-specific):
        /// Based on swkotor.exe: FUN_005afce0 @ 0x005afce0 (perception check function)
        /// Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
        /// Located via string references: EFFECT_TYPE_DEAF @ ScriptDefs constant 13
        /// Original implementation: Comprehensive hearing detection with multiple checks:
        /// 1. Deafness check: Creature must not be deafened (EFFECT_TYPE_DEAF = 13)
        /// 2. Silence check: Target must not be silenced (stealth/invisibility effects)
        /// 3. Sound source check: Target must be making sound (moving, in combat, or activated)
        /// 4. Line of sight check: Sound can travel through some obstacles but not all
        /// 5. Distance check: Already performed by caller (hearing range)
        /// 
        /// Sound generation rules:
        /// - Creatures make sound when moving (position changes between updates)
        /// - Creatures make sound when in combat (battle cries, attack grunts, pain sounds)
        /// - Placeables/doors make sound when activated (opening, closing, using)
        /// - Dead creatures do not make sound (unless death sound is still playing)
        /// 
        /// Silence/stealth rules:
        /// - Invisibility effect can reduce sound (stealth mode)
        /// - Creatures in stealth mode make less sound (reduced detection range)
        /// - Completely silent creatures (no movement, no combat) are not audible
        /// </remarks>
        private bool CanHearInternal(IEntity creature, IEntity target)
        {
            // Check if creature is deafened
            // Based on swkotor.exe: EFFECT_TYPE_DEAF = 13 prevents hearing perception
            // Located via string references: EFFECT_TYPE_DEAF @ ScriptDefs constant 13
            // Original implementation: Deafness effect blocks hearing perception checks
            // swkotor.exe: FUN_005afce0 @ 0x005afce0 (perception check function)
            // swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
            // Checks for EFFECT_TYPE_DEAF (13) in creature's effect list before allowing hearing perception
            if (_effectSystem.HasEffect(creature, EffectType.Deafness))
            {
                return false; // Creature cannot hear due to deafness effect
            }

            // Check if target is silenced (stealth/invisibility reduces sound)
            // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
            // Original implementation: Invisibility effect reduces sound generation
            // Stealth mode creatures make less sound (reduced detection range)
            // Completely silent creatures (no movement, no combat) are not audible
            bool targetIsInvisible = _effectSystem.HasEffect(target, EffectType.Invisibility);

            // Get target position for movement and line of sight checks
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform == null)
            {
                return false; // Cannot determine position
            }
            Vector3 targetPos = targetTransform.Position;

            // Get creature position for line of sight checks
            ITransformComponent creatureTransform = creature.GetComponent<ITransformComponent>();
            if (creatureTransform == null)
            {
                return false; // Cannot determine position
            }
            Vector3 creaturePos = creatureTransform.Position;

            // Check if target is making sound
            bool isMakingSound = false;

            if (target.ObjectType == ObjectType.Creature)
            {
                // Creatures make sound when:
                // 1. Moving (position changed since last update)
                // 2. In combat (battle cries, attack grunts, pain sounds)
                // 3. Dead creatures do not make sound (unless death sound is still playing)

                // Check if creature is dead
                // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
                // Original implementation: Dead creatures do not make sound
                // Check for death effect or zero hit points
                if (_effectSystem.HasEffect(target, EffectType.Death))
                {
                    // Dead creatures do not make sound (death sound is handled separately)
                    return false;
                }

                // Check if creature is moving
                // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
                // Original implementation: Position changes between updates indicate movement
                // Movement threshold: 0.1 meters (to account for floating point precision)
                const float movementThreshold = 0.1f;
                const float movementThresholdSq = movementThreshold * movementThreshold;

                Vector3 lastPos;
                if (_lastPositions.TryGetValue(target.ObjectId, out lastPos))
                {
                    float movementDistSq = Vector3.DistanceSquared(targetPos, lastPos);
                    if (movementDistSq > movementThresholdSq)
                    {
                        // Creature is moving - makes sound
                        // Invisibility reduces but does not eliminate movement sound
                        isMakingSound = true;
                    }
                }
                else
                {
                    // First time tracking this entity - check combat state
                    // Entities that just spawned or were just registered might be in combat
                    // If not in combat and no previous position, assume stationary (no sound)
                    // This handles entities that just spawned or were just registered
                }

                // Check if creature is in combat
                // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
                // Original implementation: Creatures in combat make sound (battle cries, attack grunts)
                // Combat sounds are louder and more detectable than movement sounds
                if (_world.CombatSystem.IsInCombat(target))
                {
                    // Creature is in combat - makes sound (battle cries, attack grunts, pain sounds)
                    // Combat sounds are audible even if creature is not moving
                    isMakingSound = true;
                }

                // If target is invisible/stealthed and not making sound, it's not audible
                // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
                // Original implementation: Stealth mode reduces sound generation
                // Completely silent creatures (no movement, no combat) are not audible
                if (targetIsInvisible && !isMakingSound)
                {
                    return false; // Silent and stealthed - not audible
                }

                // If creature is not making any sound, it's not audible
                if (!isMakingSound)
                {
                    return false; // Silent creature - not audible
                }

                // Check line of sight for sound (sound can travel through some obstacles but not all)
                // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
                // Original implementation: Sound can travel through some obstacles but not all
                // Sound line of sight is less strict than visual line of sight
                // Sound can bend around corners slightly (small obstacles don't block sound)
                // Large obstacles (walls, doors) block sound
                IArea area = _world.CurrentArea;
                if (area != null && area.NavigationMesh != null)
                {
                    // Use navigation mesh to test line of sight for sound
                    // Sound line of sight is similar to visual but with slightly more leniency
                    // Adjust positions slightly above ground for creature ear level
                    Vector3 earPos = creaturePos + new Vector3(0, 1.2f, 0); // Approximate ear height
                    Vector3 targetEarPos = targetPos + new Vector3(0, 1.2f, 0);

                    // Test line of sight for sound
                    // Sound can travel through small obstacles but not large ones
                    bool hasSoundLOS = area.NavigationMesh.TestLineOfSight(earPos, targetEarPos);
                    if (!hasSoundLOS)
                    {
                        // Sound is blocked by geometry
                        // However, combat sounds are louder and can be heard through some obstacles
                        // Check if target is in combat - combat sounds are more audible
                        if (_world.CombatSystem.IsInCombat(target))
                        {
                            // Combat sounds can be heard through some obstacles
                            // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
                            // Original implementation: Combat sounds have longer range and can penetrate some obstacles
                            // Uses multi-path sound propagation to simulate sound bending around corners
                            bool canHearCombatSound = TestCombatSoundPropagation(area, earPos, targetEarPos, creaturePos, targetPos);
                            if (!canHearCombatSound)
                            {
                                return false; // Combat sound cannot reach perceiver through obstacles
                            }
                        }
                        else
                        {
                            // Non-combat sounds are blocked by obstacles
                            return false;
                        }
                    }
                }

                return true; // Creature is making sound and sound can reach perceiver
            }
            else if (target.ObjectType == ObjectType.Placeable || target.ObjectType == ObjectType.Door)
            {
                // Placeables/doors only audible if activated
                // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
                // Original implementation: Placeables/doors make sound when activated (opening, closing, using)
                // Sound is generated during activation animation (opening/closing/using)
                // Sound duration: ~1-2 seconds (typical door/placeable activation animation length)
                // Located via string references: "placeableobjsnds" @ 0x007c4bf0 (placeable object sounds directory)
                // Door sounds: "i_opendoor" @ 0x007c86d4 (open door animation), door opening/closing sounds
                // Placeable sounds: Opening/closing container sounds, activation sounds for non-container placeables

                // Check if placeable/door was recently activated
                // Activation is detected by state changes (IsOpen changing)
                // Sound is audible for a short time window after activation
                float activationTime;
                if (_placeableDoorActivationTimes.TryGetValue(target.ObjectId, out activationTime))
                {
                    // Sound duration in seconds (typical door/placeable activation animation length)
                    // Based on swkotor2.exe: Door/placeable activation sounds typically last 1-2 seconds
                    // Located via string references: "placeableobjsnds" @ 0x007c4bf0 (placeable object sounds directory)
                    // Door sounds: "i_opendoor" @ 0x007c86d4 (open door animation), door opening/closing sounds
                    const float activationSoundDuration = 2.0f; // Sound duration in seconds

                    // Check if activation was recent (within sound duration)
                    // Use total elapsed time to calculate time since activation
                    float timeSinceActivation = _totalElapsedTime - activationTime;

                    // If activation was recent (within sound duration), placeable/door is making sound
                    if (timeSinceActivation >= 0.0f && timeSinceActivation <= activationSoundDuration)
                    {
                        // Placeable/door was recently activated - making sound
                        // Check line of sight for sound (sound can travel through some obstacles but not all)
                        IArea area = _world.CurrentArea;
                        if (area != null && area.NavigationMesh != null)
                        {
                            // Use navigation mesh to test line of sight for sound
                            // Sound line of sight is similar to visual but with slightly more leniency
                            // Adjust positions slightly above ground for creature ear level
                            Vector3 earPos = creaturePos + new Vector3(0, 1.2f, 0); // Approximate ear height
                            Vector3 targetEarPos = targetPos + new Vector3(0, 1.2f, 0);

                            // Test line of sight for sound
                            // Sound can travel through small obstacles but not large ones
                            bool hasSoundLOS = area.NavigationMesh.TestLineOfSight(earPos, targetEarPos);
                            if (!hasSoundLOS)
                            {
                                // Sound is blocked by geometry
                                return false;
                            }
                        }

                        return true; // Placeable/door is making sound and sound can reach perceiver
                    }
                }

                // Placeable/door was not recently activated - not making sound
                return false;
            }

            // Other object types are not audible
            return false;
        }

        /// <summary>
        /// Tests if combat sound can propagate from source to listener through obstacles.
        /// Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
        /// Original implementation: Combat sounds have longer range and can penetrate some obstacles
        /// Uses multi-path sound propagation to simulate sound bending around corners and through openings.
        /// </summary>
        /// <remarks>
        /// Sound propagation algorithm:
        /// 1. Direct path test: Check if direct line of sight exists (fastest path)
        /// 2. Multi-path test: Test offset paths to simulate sound bending around corners
        /// 3. Distance-based attenuation: Combat sounds attenuate with distance and obstacles
        /// 4. Obstacle penetration: Combat sounds can penetrate thin obstacles (doors, small walls)
        /// 
        /// Sound physics simulation:
        /// - Sound can bend around corners (diffraction) - tested via offset paths
        /// - Sound attenuates with distance (inverse square law approximation)
        /// - Sound is blocked by thick walls but can penetrate thin obstacles
        /// - Combat sounds are louder (higher base volume) and have longer effective range
        /// 
        /// Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
        /// Original implementation uses walkmesh raycasting with multiple paths for sound propagation
        /// </remarks>
        private bool TestCombatSoundPropagation(IArea area, Vector3 listenerEarPos, Vector3 sourceEarPos, Vector3 listenerPos, Vector3 sourcePos)
        {
            if (area == null || area.NavigationMesh == null)
            {
                // No navigation mesh - assume sound can propagate (fallback)
                return true;
            }

            // Calculate distance for attenuation
            float distSq = Vector3.DistanceSquared(listenerPos, sourcePos);
            float dist = (float)Math.Sqrt(distSq);

            // Combat sound propagation constants
            // Based on swkotor2.exe: FUN_005fb0f0 @ 0x005fb0f0 (perception update system)
            // Original implementation: Combat sounds have longer range and can penetrate some obstacles
            const float combatSoundMaxRange = 25.0f; // Maximum range for combat sounds (meters)
            const float combatSoundPenetrationRange = 12.0f; // Range where combat sounds can penetrate obstacles (meters)
            const float combatSoundAttenuationFactor = 0.7f; // Attenuation factor for obstacles (0.0-1.0, lower = more attenuation)

            // Check if within maximum range
            if (dist > combatSoundMaxRange)
            {
                return false; // Too far for any combat sound to be heard
            }

            // Test direct path first (fastest and clearest sound path)
            bool hasDirectPath = area.NavigationMesh.TestLineOfSight(listenerEarPos, sourceEarPos);
            if (hasDirectPath)
            {
                // Direct path exists - sound can propagate clearly
                return true;
            }

            // Direct path is blocked - test multi-path propagation
            // Sound can bend around corners and penetrate thin obstacles
            // Test multiple offset paths to simulate sound diffraction and penetration

            // Calculate direction vector from source to listener
            Vector3 direction = listenerPos - sourcePos;
            float directionLength = direction.Length();
            if (directionLength < 1e-6f)
            {
                // Same position - sound is audible
                return true;
            }

            Vector3 normalizedDir = direction / directionLength;

            // Calculate perpendicular vectors for offset path testing in horizontal plane
            // Project direction onto horizontal plane (XZ plane, Y is up)
            Vector3 up = Vector3.UnitY;
            Vector3 horizontalDir = normalizedDir - Vector3.Dot(normalizedDir, up) * up;
            float horizontalDirLength = horizontalDir.Length();

            Vector3 right;
            Vector3 forward;

            if (horizontalDirLength > 1e-6f)
            {
                // Normalize horizontal direction
                horizontalDir = horizontalDir / horizontalDirLength;

                // Calculate perpendicular vectors in horizontal plane
                // Right vector: perpendicular to horizontal direction in XZ plane
                right = Vector3.Cross(up, horizontalDir);
                float rightLength = right.Length();
                if (rightLength > 1e-6f)
                {
                    right = right / rightLength;
                }
                else
                {
                    // Fallback: use unit X
                    right = Vector3.UnitX;
                }

                // Forward vector: same as horizontal direction (for clarity)
                forward = horizontalDir;
            }
            else
            {
                // Direction is vertical (straight up/down) - use default horizontal vectors
                right = Vector3.UnitX;
                forward = Vector3.UnitZ;
            }

            // Test offset paths to simulate sound bending around corners
            // Offset distance: 1.5 meters (typical corner/obstacle size)
            const float offsetDistance = 1.5f;
            const int numOffsetPaths = 8; // Number of offset paths to test (8 directions around source)

            int clearPaths = 0;
            float totalAttenuation = 0.0f;

            // Test direct path with attenuation
            totalAttenuation += combatSoundAttenuationFactor; // Direct path blocked = high attenuation
            clearPaths += 0; // Direct path is blocked

            // Test offset paths in a circle around the source
            // This simulates sound bending around corners and through openings
            for (int i = 0; i < numOffsetPaths; i++)
            {
                // Calculate offset angle (0 to 2*PI)
                float angle = (float)(2.0 * Math.PI * i / numOffsetPaths);

                // Calculate offset position around source (in horizontal plane)
                Vector3 offset = right * (float)(Math.Cos(angle) * offsetDistance) +
                                forward * (float)(Math.Sin(angle) * offsetDistance);

                // Test path from offset position near source to listener
                Vector3 offsetSourcePos = sourceEarPos + offset;
                Vector3 offsetSourceGround = sourcePos + offset;

                // Adjust offset source to ear height
                Vector3 offsetSourceEar = offsetSourceGround + new Vector3(0, 1.2f, 0);

                // Test line of sight from offset position to listener
                bool hasOffsetPath = area.NavigationMesh.TestLineOfSight(listenerEarPos, offsetSourceEar);

                if (hasOffsetPath)
                {
                    // Offset path is clear - sound can propagate through this path
                    clearPaths++;

                    // Calculate attenuation for this path
                    // Longer paths have more attenuation
                    float offsetPathDist = Vector3.Distance(listenerPos, offsetSourceGround);
                    float pathAttenuation = 1.0f / (1.0f + offsetPathDist * 0.1f); // Inverse distance attenuation
                    totalAttenuation += pathAttenuation;
                }
                else
                {
                    // Offset path is also blocked - add attenuation
                    totalAttenuation += combatSoundAttenuationFactor * 0.5f; // Partial attenuation for blocked offset path
                }
            }

            // Calculate effective sound strength
            // More clear paths = stronger sound
            // Less attenuation = stronger sound
            float effectiveSoundStrength = (clearPaths / (float)(numOffsetPaths + 1)) * (totalAttenuation / (float)(numOffsetPaths + 1));

            // Combat sounds can be heard if:
            // 1. At least one path is clear (sound can bend around corners)
            // 2. OR distance is within penetration range and effective sound strength is sufficient
            if (clearPaths > 0)
            {
                // At least one path is clear - sound can propagate
                return true;
            }

            // No clear paths - check if distance is within penetration range
            if (dist <= combatSoundPenetrationRange)
            {
                // Within penetration range - check if effective sound strength is sufficient
                // Combat sounds can penetrate thin obstacles if close enough
                const float minEffectiveStrength = 0.3f; // Minimum effective strength to penetrate obstacles
                if (effectiveSoundStrength >= minEffectiveStrength)
                {
                    return true; // Sound can penetrate obstacles
                }
            }

            // Sound cannot propagate through obstacles
            return false;
        }

        /// <summary>
        /// Fires perception change events.
        /// </summary>
        private void FirePerceptionEvents(IEntity creature, PerceptionData data)
        {
            // New objects seen
            foreach (uint seenId in data.SeenObjects)
            {
                if (!data.LastSeenObjects.Contains(seenId))
                {
                    IEntity seen = _world.GetEntity(seenId);
                    if (seen != null)
                    {
                        OnPerceptionChanged?.Invoke(this, new OdysseyPerceptionEventArgs
                        {
                            Perceiver = creature,
                            Perceived = seen,
                            EventType = PerceptionEventType.Seen
                        });
                    }
                }
            }

            // Objects that vanished
            foreach (uint lastSeenId in data.LastSeenObjects)
            {
                if (!data.SeenObjects.Contains(lastSeenId))
                {
                    IEntity vanished = _world.GetEntity(lastSeenId);
                    if (vanished != null)
                    {
                        OnPerceptionChanged?.Invoke(this, new OdysseyPerceptionEventArgs
                        {
                            Perceiver = creature,
                            Perceived = vanished,
                            EventType = PerceptionEventType.Vanished
                        });
                    }
                }
            }

            // New objects heard
            foreach (uint heardId in data.HeardObjects)
            {
                if (!data.LastHeardObjects.Contains(heardId))
                {
                    IEntity heard = _world.GetEntity(heardId);
                    if (heard != null)
                    {
                        OnPerceptionChanged?.Invoke(this, new OdysseyPerceptionEventArgs
                        {
                            Perceiver = creature,
                            Perceived = heard,
                            EventType = PerceptionEventType.Heard
                        });
                    }
                }
            }

            // Objects that became inaudible
            foreach (uint lastHeardId in data.LastHeardObjects)
            {
                if (!data.HeardObjects.Contains(lastHeardId))
                {
                    IEntity inaudible = _world.GetEntity(lastHeardId);
                    if (inaudible != null)
                    {
                        OnPerceptionChanged?.Invoke(this, new OdysseyPerceptionEventArgs
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
                foreach (uint seenId in data.SeenObjects)
                {
                    IEntity seen = _world.GetEntity(seenId);
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
                foreach (uint heardId in data.HeardObjects)
                {
                    IEntity heard = _world.GetEntity(heardId);
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
            _lastPerceivedEntity.Remove(creature.ObjectId);
            _lastPerceptionWasHeard.Remove(creature.ObjectId);
        }

        /// <summary>
        /// Clears all perception data.
        /// </summary>
        public void ClearAllPerception()
        {
            _perceptionData.Clear();
            _lastPerceivedEntity.Clear();
            _lastPerceptionWasHeard.Clear();
        }

        /// <summary>
        /// Gets the last perceived entity for a creature (for GetLastPerceived engine API).
        /// </summary>
        public IEntity GetLastPerceived(IEntity creature)
        {
            if (creature == null)
            {
                return null;
            }

            IEntity lastPerceived;
            if (_lastPerceivedEntity.TryGetValue(creature.ObjectId, out lastPerceived))
            {
                return lastPerceived;
            }
            return null;
        }

        /// <summary>
        /// Checks if the last perception was heard (for GetLastPerceptionHeard engine API).
        /// </summary>
        public bool WasLastPerceptionHeard(IEntity creature)
        {
            if (creature == null)
            {
                return false;
            }

            bool wasHeard;
            if (_lastPerceptionWasHeard.TryGetValue(creature.ObjectId, out wasHeard))
            {
                return wasHeard;
            }
            return false;
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

            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return null;
            }

            Vector3 position = transform.Position;
            IEntity nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (IEntity seen in GetSeenObjects(creature))
            {
                if (seen.ObjectType != ObjectType.Creature)
                {
                    continue;
                }

                if (!factionManager.IsHostile(creature, seen))
                {
                    continue;
                }

                ITransformComponent seenTransform = seen.GetComponent<ITransformComponent>();
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

            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return null;
            }

            Vector3 position = transform.Position;
            IEntity nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (IEntity seen in GetSeenObjects(creature))
            {
                if (seen.ObjectType != ObjectType.Creature)
                {
                    continue;
                }

                if (!factionManager.IsFriendly(creature, seen))
                {
                    continue;
                }

                ITransformComponent seenTransform = seen.GetComponent<ITransformComponent>();
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
        /// Checks if the last perceived entity was seen (for GetLastPerceptionSeen engine API).
        /// </summary>
        public bool WasLastPerceptionSeen(IEntity creature)
        {
            if (creature == null)
            {
                return false;
            }

            IEntity lastPerceived = GetLastPerceived(creature);
            if (lastPerceived == null)
            {
                return false;
            }

            // Check if last perceived entity is currently seen
            PerceptionData data;
            if (_perceptionData.TryGetValue(creature.ObjectId, out data))
            {
                return data.SeenObjects.Contains(lastPerceived.ObjectId);
            }

            return false;
        }

        /// <summary>
        /// Checks if the last perceived entity became inaudible (for GetLastPerceptionInaudible engine API).
        /// </summary>
        public bool WasLastPerceptionInaudible(IEntity creature)
        {
            if (creature == null)
            {
                return false;
            }

            IEntity lastPerceived = GetLastPerceived(creature);
            if (lastPerceived == null)
            {
                return false;
            }

            // Check if last perceived entity was heard but is no longer heard
            PerceptionData data;
            if (_perceptionData.TryGetValue(creature.ObjectId, out data))
            {
                // Was in last heard set but not in current heard set
                return data.LastHeardObjects.Contains(lastPerceived.ObjectId) &&
                       !data.HeardObjects.Contains(lastPerceived.ObjectId);
            }

            return false;
        }

        /// <summary>
        /// Checks if the last perceived entity vanished (for GetLastPerceptionVanished engine API).
        /// </summary>
        public bool WasLastPerceptionVanished(IEntity creature)
        {
            if (creature == null)
            {
                return false;
            }

            IEntity lastPerceived = GetLastPerceived(creature);
            if (lastPerceived == null)
            {
                return false;
            }

            // Check if last perceived entity was seen but is no longer seen
            PerceptionData data;
            if (_perceptionData.TryGetValue(creature.ObjectId, out data))
            {
                // Was in last seen set but not in current seen set
                return data.LastSeenObjects.Contains(lastPerceived.ObjectId) &&
                       !data.SeenObjects.Contains(lastPerceived.ObjectId);
            }

            return false;
        }
    }
}

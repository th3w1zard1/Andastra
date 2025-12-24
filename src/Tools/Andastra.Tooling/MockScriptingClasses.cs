using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.EngineApi;
using Andastra.Runtime.Scripting.Types;
using Andastra.Runtime.Scripting.VM;
using Andastra.Parsing.Formats.TwoDA;
using Perception = Andastra.Runtime.Core.Perception;
using Combat = Andastra.Runtime.Core.Combat;
using Triggers = Andastra.Runtime.Core.Triggers;
using AI = Andastra.Runtime.Core.AI;
using Animation = Andastra.Runtime.Core.Animation;

namespace Andastra.Runtime.Tooling
{
    /// <summary>
    // TODO: / Mock entity for CLI script execution. Provides minimal implementation for testing scripts.
    /// </summary>
    public class MockEntity : IEntity
    {
        private static uint _nextObjectId = 0x7F000001; // Start at OBJECT_SELF

        private readonly Dictionary<Type, IComponent> _components;
        private readonly Dictionary<string, object> _data;
        private bool _isValid;

        public MockEntity(string tag = "MOCK_ENTITY")
        {
            ObjectId = _nextObjectId++;
            Tag = tag;
            ObjectType = ObjectType.Creature;
            AreaId = 1;
            _components = new Dictionary<Type, IComponent>();
            _data = new Dictionary<string, object>();
            _isValid = true;
        }

        public uint ObjectId { get; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get; }
        public uint AreaId { get; set; }
        public IWorld World { get; set; }
        public bool IsValid
        {
            get { return _isValid; }
            private set { _isValid = value; }
        }

        internal void SetValid(bool valid)
        {
            _isValid = valid;
        }

        public T GetComponent<T>() where T : class, IComponent
        {
            if (_components.TryGetValue(typeof(T), out IComponent component))
            {
                return component as T;
            }
            return null;
        }

        public void AddComponent<T>(T component) where T : class, IComponent
        {
            _components[typeof(T)] = component;
        }

        public bool RemoveComponent<T>() where T : class, IComponent
        {
            return _components.Remove(typeof(T));
        }

        public bool HasComponent<T>() where T : class, IComponent
        {
            return _components.ContainsKey(typeof(T));
        }

        public IEnumerable<IComponent> GetAllComponents()
        {
            return _components.Values;
        }

        public void SetData(string key, object value)
        {
            _data[key] = value;
        }

        public T GetData<T>(string key, T defaultValue = default(T))
        {
            if (_data.TryGetValue(key, out object value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        public object GetData(string key)
        {
            if (_data.TryGetValue(key, out object value))
            {
                return value;
            }
            return null;
        }

        public bool HasData(string key)
        {
            return _data.ContainsKey(key);
        }
    }

    /// <summary>
    // TODO: / Mock world for CLI script execution. Provides minimal implementation for testing scripts.
    /// </summary>
    public class MockWorld : IWorld
    {
        private readonly Dictionary<uint, IEntity> _entities;
        private readonly Dictionary<uint, IArea> _areas;

        public MockWorld()
        {
            _entities = new Dictionary<uint, IEntity>();
            _areas = new Dictionary<uint, IArea>();

            // Initialize mock systems
            TimeManager = new MockTimeManager();
            EventBus = new MockEventBus();
            DelayScheduler = new MockDelayScheduler();
            GameDataProvider = new MockGameDataProvider();

            // Initialize concrete systems (they require IWorld reference)
            EffectSystem = new Combat.EffectSystem(this);
            PerceptionSystem = new Perception.PerceptionSystem(this);
            CombatSystem = new Combat.CombatSystem(this);
            TriggerSystem = new Triggers.TriggerSystem(this);
            AIController = new AI.AIController(this, CombatSystem);
            AnimationSystem = new Animation.AnimationSystem(this);

            // Initialize current area and module
            var mockArea = new MockArea();
            var mockModule = new MockModule();
            RegisterArea(mockArea);
            CurrentArea = mockArea;
            CurrentModule = mockModule;
        }

        public IEntity GetEntity(uint objectId)
        {
            if (_entities.TryGetValue(objectId, out IEntity entity))
            {
                return entity;
            }
            return null;
        }

        public IEntity GetEntityByTag(string tag, int nth = 0)
        {
            int count = 0;
            foreach (var entity in _entities.Values)
            {
                if (string.Equals(entity.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    if (count == nth)
                    {
                        return entity;
                    }
                    count++;
                }
            }
            return null;
        }

        public void RegisterEntity(IEntity entity)
        {
            if (entity != null)
            {
                _entities[entity.ObjectId] = entity;
                if (entity is MockEntity mockEntity)
                {
                    mockEntity.World = this;
                }
            }
        }

        public void UnregisterEntity(IEntity entity)
        {
            if (entity != null)
            {
                _entities.Remove(entity.ObjectId);
            }
        }

        public IEnumerable<IEntity> GetEntitiesInRadius(Vector3 center, float radius, ObjectType typeMask = ObjectType.All)
        {
            // Return empty for CLI tooling
            yield break;
        }

        public IArea GetArea(uint areaId)
        {
            if (_areas.TryGetValue(areaId, out IArea area))
            {
                return area;
            }
            return null;
        }

        public IEntity CreateEntity(Andastra.Runtime.Core.Templates.IEntityTemplate template, Vector3 position, float facing)
        {
            var entity = new MockEntity(template?.Tag ?? "CREATED_ENTITY");
            entity.ObjectType = template?.ObjectType ?? ObjectType.Creature;
            entity.AreaId = CurrentArea != null ? GetAreaId(CurrentArea) : 1;
            RegisterEntity(entity);
            return entity;
        }

        public IEntity CreateEntity(ObjectType objectType, Vector3 position, float facing)
        {
            var entity = new MockEntity("CREATED_ENTITY");
            entity.ObjectType = objectType;
            entity.AreaId = CurrentArea != null ? GetAreaId(CurrentArea) : 1;
            RegisterEntity(entity);
            return entity;
        }

        public void DestroyEntity(uint objectId)
        {
            if (_entities.TryGetValue(objectId, out IEntity entity) && entity is MockEntity mockEntity)
            {
                mockEntity.SetValid(false);
            }
            _entities.Remove(objectId);
        }

        public IEnumerable<IEntity> GetEntitiesOfType(ObjectType type)
        {
            foreach (var entity in _entities.Values)
            {
                if (entity.ObjectType == type)
                {
                    yield return entity;
                }
            }
        }

        public IEnumerable<IEntity> GetAllEntities()
        {
            return _entities.Values;
        }

        public IArea CurrentArea { get; set; }

        public IModule CurrentModule { get; set; }

        public ITimeManager TimeManager { get; set; }

        public IEventBus EventBus { get; set; }

        public IDelayScheduler DelayScheduler { get; set; }

        public Andastra.Runtime.Core.Combat.EffectSystem EffectSystem { get; set; }

        public Andastra.Runtime.Core.Perception.PerceptionSystem PerceptionSystem { get; set; }

        public Andastra.Runtime.Core.Combat.CombatSystem CombatSystem { get; set; }

        public Andastra.Runtime.Core.Triggers.TriggerSystem TriggerSystem { get; set; }

        public Andastra.Runtime.Core.AI.AIController AIController { get; set; }

        public Animation.AnimationSystem AnimationSystem { get; set; }

        public IGameDataProvider GameDataProvider { get; set; }

        public void RegisterArea(IArea area)
        {
            if (area != null)
            {
                // Get or assign AreaId for the area
                uint areaId = GetAreaId(area);
                if (areaId == 0)
                {
                    // Assign new AreaId if not already assigned
                    areaId = (uint)(_areas.Count + 1);
                }
                _areas[areaId] = area;
            }
        }

        public void UnregisterArea(IArea area)
        {
            if (area != null)
            {
                uint areaId = GetAreaId(area);
                if (areaId != 0)
                {
                    _areas.Remove(areaId);
                }
            }
        }

        public uint GetAreaId(IArea area)
        {
            if (area != null)
            {
                // Find the AreaId by searching the dictionary
                foreach (var kvp in _areas)
                {
                    if (kvp.Value == area)
                    {
                        return kvp.Key;
                    }
                }
            }
            return 0;
        }

        public IEnumerable<IArea> GetAllAreas()
        {
            return _areas.Values;
        }

        public uint GetModuleId(IModule module)
        {
            // Module ID is fixed at 0x7F000002 in original engine
            return 0x7F000002;
        }

        public void Update(float deltaTime)
        {
            // Update time manager
            TimeManager?.Update(deltaTime);

            // Update delay scheduler
            DelayScheduler?.Update(deltaTime);

            // Update other systems
            AnimationSystem?.Update(deltaTime);
        }
    }

    /// <summary>
    /// Mock delay scheduler for CLI script execution. Provides basic delayed action scheduling for testing scripts.
    /// </summary>
    public class MockDelayScheduler : IDelayScheduler
    {
        public void ScheduleDelay(float delaySeconds, IAction action, IEntity target)
        {
            // No-op for CLI tooling - delayed actions not executed
        }

        public void Update(float deltaTime)
        {
            // No-op for CLI tooling - time not advanced
        }

        public void ClearForEntity(IEntity entity)
        {
            // No-op for CLI tooling - no actions to clear
        }

        public void ClearAll()
        {
            // No-op for CLI tooling - no actions to clear
        }

        public int PendingCount => 0;
    }

    /// <summary>
    /// Mock event bus for CLI script execution. Provides basic event handling for testing scripts.
    /// </summary>
    public class MockEventBus : IEventBus
    {
        public void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            // No-op for CLI tooling - events not processed
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            // No-op for CLI tooling - events not processed
        }

        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            // No-op for CLI tooling - events not processed
        }

        public void QueueEvent<T>(T gameEvent) where T : IGameEvent
        {
            // No-op for CLI tooling - events not processed
        }

        public void DispatchQueuedEvents()
        {
            // No-op for CLI tooling - events not processed
        }

        public void FireScriptEvent(IEntity entity, ScriptEvent eventType, IEntity triggerer = null)
        {
            // No-op for CLI tooling - script events not processed
        }
    }

    /// <summary>
    /// Mock time manager for CLI script execution. Provides basic time management for testing scripts.
    /// </summary>
    public class MockTimeManager : ITimeManager
    {
        private float _simulationTime;
        private float _realTime;
        private float _timeScale = 1.0f;
        private bool _isPaused;
        private float _deltaTime;
        private int _gameTimeHour;
        private int _gameTimeMinute;
        private int _gameTimeSecond;
        private int _gameTimeMillisecond;

        public MockTimeManager()
        {
            FixedTimestep = 1.0f / 60.0f; // 60 FPS
            _simulationTime = 0.0f;
            _realTime = 0.0f;
            _deltaTime = FixedTimestep;
            SetGameTime(12, 0, 0, 0); // Start at noon
        }

        public float FixedTimestep { get; }

        public float SimulationTime => _simulationTime;

        public float RealTime => _realTime;

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Math.Max(0.0f, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        public float DeltaTime => _deltaTime;

        public float InterpolationAlpha => 0.0f; // Not used in CLI tooling

        public void Tick()
        {
            if (!_isPaused)
            {
                _simulationTime += FixedTimestep * _timeScale;
                _deltaTime = FixedTimestep * _timeScale;
            }
        }

        public void Update(float realDeltaTime)
        {
            _realTime += realDeltaTime;
            if (!_isPaused)
            {
                _deltaTime = realDeltaTime * _timeScale;
            }
        }

        public bool HasPendingTicks()
        {
            return !_isPaused;
        }

        public int GameTimeHour => _gameTimeHour;

        public int GameTimeMinute => _gameTimeMinute;

        public int GameTimeSecond => _gameTimeSecond;

        public int GameTimeMillisecond => _gameTimeMillisecond;

        public void SetGameTime(int hour, int minute, int second, int millisecond)
        {
            _gameTimeHour = Math.Max(0, Math.Min(23, hour));
            _gameTimeMinute = Math.Max(0, Math.Min(59, minute));
            _gameTimeSecond = Math.Max(0, Math.Min(59, second));
            _gameTimeMillisecond = Math.Max(0, Math.Min(999, millisecond));
        }
    }

    /// <summary>
    /// Mock game data provider for CLI script execution. Provides basic game data access for testing scripts.
    /// </summary>
    public class MockGameDataProvider : IGameDataProvider
    {
        public TwoDA GetTable(string tableName)
        {
            // Return null for CLI tooling - no game data tables loaded
            return null;
        }
    }

    /// <summary>
    /// Mock navigation mesh for CLI script execution. Provides basic navigation functionality for testing scripts.
    /// </summary>
    public class MockNavigationMesh : INavigationMesh
    {
        public bool IsPointWalkable(Vector3 point)
        {
            // All points are walkable in CLI tooling
            return true;
        }

        public bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            // Return the same point for CLI tooling
            result = point;
            height = point.Y;
            return true;
        }

        public bool FindPath(Vector3 start, Vector3 end, out Vector3[] path)
        {
            // Direct path for CLI tooling
            path = new[] { start, end };
            return true;
        }

        public float GetHeightAtPoint(Vector3 point)
        {
            return point.Y;
        }
    }

    /// <summary>
    /// Mock area for CLI script execution. Provides basic area functionality for testing scripts.
    /// </summary>
    public class MockArea : IArea
    {
        private readonly List<IEntity> _creatures = new List<IEntity>();
        private readonly List<IEntity> _placeables = new List<IEntity>();
        private readonly List<IEntity> _doors = new List<IEntity>();
        private readonly List<IEntity> _triggers = new List<IEntity>();
        private readonly List<IEntity> _waypoints = new List<IEntity>();
        private readonly List<IEntity> _sounds = new List<IEntity>();

        public MockArea(string resRef = "MOCK_AREA", string tag = "MOCK_AREA")
        {
            ResRef = resRef;
            Tag = tag;
            DisplayName = resRef;
            NavigationMesh = new MockNavigationMesh();
        }

        public string ResRef { get; }
        public string DisplayName { get; }
        public string Tag { get; }

        public IEnumerable<IEntity> Creatures => _creatures;
        public IEnumerable<IEntity> Placeables => _placeables;
        public IEnumerable<IEntity> Doors => _doors;
        public IEnumerable<IEntity> Triggers => _triggers;
        public IEnumerable<IEntity> Waypoints => _waypoints;
        public IEnumerable<IEntity> Sounds => _sounds;

        public IEntity GetObjectByTag(string tag, int nth = 0)
        {
            // Search through all entity lists for the tag
            var allEntities = new[] { _creatures, _placeables, _doors, _triggers, _waypoints, _sounds };
            int count = 0;
            foreach (var entityList in allEntities)
            {
                foreach (var entity in entityList)
                {
                    if (string.Equals(entity.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        if (count == nth)
                            return entity;
                        count++;
                    }
                }
            }
            return null;
        }

        public INavigationMesh NavigationMesh { get; }

        public bool IsPointWalkable(Vector3 point)
        {
            return NavigationMesh.IsPointWalkable(point);
        }

        public bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            return NavigationMesh.ProjectToWalkmesh(point, out result, out height);
        }

        public bool IsUnescapable { get; set; }
        public bool StealthXPEnabled { get; set; }

        public void Update(float deltaTime)
        {
            // No-op for CLI tooling
        }
    }

    /// <summary>
    /// Mock module for CLI script execution. Provides basic module functionality for testing scripts.
    /// </summary>
    public class MockModule : IModule
    {
        private readonly List<IArea> _areas = new List<IArea>();

        public MockModule(string resRef = "MOCK_MODULE")
        {
            ResRef = resRef;
            DisplayName = resRef;
            EntryArea = "mockarea";
            DawnHour = 6;
            DuskHour = 18;
            MinutesPastMidnight = 12 * 60; // Noon
            Day = 1;
            Month = 1;
            Year = 1372; // Default NWN year
        }

        public string ResRef { get; }
        public string DisplayName { get; }
        public string EntryArea { get; }

        public IEnumerable<IArea> Areas => _areas;

        public IArea GetArea(string resRef)
        {
            return _areas.Find(a => string.Equals(a.ResRef, resRef, StringComparison.OrdinalIgnoreCase));
        }

        public string GetScript(Enums.ScriptEvent eventType)
        {
            // Return null for CLI tooling - no scripts
            return null;
        }

        public int DawnHour { get; }
        public int DuskHour { get; }
        public int MinutesPastMidnight { get; set; }
        public int Day { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
    }

    /// <summary>
    // TODO: / Mock engine API for CLI script execution. Provides basic function implementations for testing scripts.
    /// Extends BaseEngineApi with minimal implementations of common NWScript functions.
    /// </summary>
    /// <remarks>
    /// Based on swkotor2.exe: Engine API function implementations
    /// Provides implementations for common functions like PrintString, Random, etc.
    // TODO: / TODO: STUB - Functions not implemented will return default values (0, empty string, OBJECT_INVALID, etc.)
    /// </remarks>
    public class MockEngineApi : BaseEngineApi
    {
        public MockEngineApi()
        {
        }

        protected override void RegisterFunctions()
        {
            // Register common functions for CLI tooling
            // Function IDs based on ScriptDefs routine IDs
            // K1 and K2 share common function IDs, so we'll register based on common set
            _functionNames[0] = "PrintString";
            _functionNames[1] = "Random";
            _implementedFunctions.Add(0);
            _implementedFunctions.Add(1);
        }

        public override Variable CallEngineFunction(int routineId, System.Collections.Generic.IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Handle implemented functions
            if (routineId == 0)
            {
                // PrintString
                return Func_PrintString(args, ctx);
            }
            else if (routineId == 1)
            {
                // Random
                return Func_Random(args, ctx);
            }

            // For unimplemented functions, return appropriate default based on expected return type
            // This allows scripts to run without crashing even if functions aren't fully implemented
            string functionName = GetFunctionName(routineId);
            Console.WriteLine($"[Script] Unimplemented function call: {functionName} (routineId={routineId}, args={args?.Count ?? 0})");

            // Default return: int 0
            // Scripts that expect other return types may not work correctly, but won't crash
            return Variable.FromInt(0);
        }
    }
}


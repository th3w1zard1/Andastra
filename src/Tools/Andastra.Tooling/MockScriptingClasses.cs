using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.EngineApi;
using Andastra.Runtime.Scripting.Types;
using Andastra.Runtime.Scripting.VM;

namespace Andastra.Runtime.Tooling
{
    /// <summary>
    /// Mock entity for CLI script execution. Provides minimal implementation for testing scripts.
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
    /// Mock world for CLI script execution. Provides minimal implementation for testing scripts.
    /// </summary>
    public class MockWorld : IWorld
    {
        private readonly Dictionary<uint, IEntity> _entities;
        private readonly Dictionary<uint, IArea> _areas;

        public MockWorld()
        {
            _entities = new Dictionary<uint, IEntity>();
            _areas = new Dictionary<uint, IArea>();
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

        public IEntity CreateEntity(IEntityTemplate template, Vector3 position, float facing)
        {
            // Not implemented for CLI tooling
            return null;
        }

        public IEntity CreateEntity(ObjectType objectType, Vector3 position, float facing)
        {
            // Not implemented for CLI tooling
            return null;
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

        public Combat.EffectSystem EffectSystem { get; set; }

        public Perception.PerceptionSystem PerceptionSystem { get; set; }

        public Combat.CombatSystem CombatSystem { get; set; }

        public Triggers.TriggerSystem TriggerSystem { get; set; }

        public AI.AIController AIController { get; set; }

        public Animation.AnimationSystem AnimationSystem { get; set; }

        public IGameDataProvider GameDataProvider { get; set; }

        public void RegisterArea(IArea area)
        {
            if (area != null)
            {
                _areas[area.AreaId] = area;
            }
        }

        public void UnregisterArea(IArea area)
        {
            if (area != null)
            {
                _areas.Remove(area.AreaId);
            }
        }

        public uint GetAreaId(IArea area)
        {
            if (area != null)
            {
                return area.AreaId;
            }
            return 0;
        }

        public uint GetModuleId(IModule module)
        {
            // Module ID is fixed at 0x7F000002 in original engine
            return 0x7F000002;
        }

        public void Update(float deltaTime)
        {
            // Not implemented for CLI tooling
        }
    }

    /// <summary>
    /// Mock engine API for CLI script execution. Provides basic function implementations for testing scripts.
    /// Extends BaseEngineApi with minimal implementations of common NWScript functions.
    /// </summary>
    /// <remarks>
    /// Based on swkotor2.exe: Engine API function implementations
    /// Provides implementations for common functions like PrintString, Random, etc.
    /// Functions not implemented will return default values (0, empty string, OBJECT_INVALID, etc.)
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


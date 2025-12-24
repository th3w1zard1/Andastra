using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.AI;
using Andastra.Runtime.Core.Animation;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Perception;
using Andastra.Runtime.Core.Templates;
using Andastra.Runtime.Core.Triggers;
using Andastra.Runtime.Games.Common.Components;
using Moq;

namespace Andastra.Tests.Runtime.TestHelpers
{
    /// <summary>
    /// Test game event for use in unit tests.
    /// </summary>
    internal class TestGameEvent : IGameEvent
    {
        public IEntity Entity { get; set; }

        public TestGameEvent(IEntity entity = null)
        {
            Entity = entity;
        }
    }

    /// <summary>
    /// Helper class for creating test entities and components.
    /// </summary>
    public static class EntityTestHelper
    {
        /// <summary>
        /// Creates a mock world for testing with full functionality.
        /// </summary>
        /// <remarks>
        /// Creates a comprehensive mock world that maintains internal state for entities, areas, and modules.
        /// All IWorld interface methods and properties are properly set up with reasonable default behavior.
        /// The mock world supports:
        /// - Entity management (CreateEntity, GetEntity, GetEntityByTag, RegisterEntity, UnregisterEntity)
        /// - Area management (GetArea, RegisterArea, UnregisterArea, GetAreaId)
        /// - Spatial queries (GetEntitiesInRadius, GetEntitiesOfType, GetAllEntities)
        /// - All world systems (TimeManager, EventBus, DelayScheduler, EffectSystem, CombatSystem, etc.)
        /// - Module management (GetModuleId, CurrentModule)
        /// </remarks>
        public static IWorld CreateMockWorld()
        {
            // Internal state for entity and area management
            var entitiesById = new Dictionary<uint, IEntity>();
            var entitiesByTag = new Dictionary<string, List<IEntity>>(StringComparer.OrdinalIgnoreCase);
            var entitiesByType = new Dictionary<ObjectType, List<IEntity>>();
            var allEntities = new List<IEntity>();
            var areasById = new Dictionary<uint, IArea>();
            var areaIds = new Dictionary<IArea, uint>();
            uint nextObjectId = 1;
            uint nextAreaId = 1;
            const uint ModuleObjectId = 0x7F000002;

            // Create mocks for interfaces
            var mockTimeManager = new Mock<ITimeManager>(MockBehavior.Loose);
            mockTimeManager.Setup(t => t.FixedTimestep).Returns(1.0f / 60.0f);
            mockTimeManager.Setup(t => t.SimulationTime).Returns(0.0f);
            mockTimeManager.Setup(t => t.RealTime).Returns(0.0f);
            mockTimeManager.SetupProperty(t => t.TimeScale, 1.0f);
            mockTimeManager.Setup(t => t.Update(It.IsAny<float>())).Callback<float>(deltaTime => { });

            var mockEventBus = new Mock<IEventBus>(MockBehavior.Loose);
            // Setup Subscribe/Unsubscribe/Publish/QueueEvent with proper IGameEvent constraint
            // Use TestGameEvent as a concrete type that implements IGameEvent to satisfy the generic constraint
            mockEventBus.Setup(e => e.Subscribe<TestGameEvent>(It.IsAny<Action<TestGameEvent>>())).Callback(() => { });
            mockEventBus.Setup(e => e.Unsubscribe<TestGameEvent>(It.IsAny<Action<TestGameEvent>>())).Callback(() => { });
            mockEventBus.Setup(e => e.Publish<TestGameEvent>(It.IsAny<TestGameEvent>())).Callback(() => { });
            mockEventBus.Setup(e => e.QueueEvent<TestGameEvent>(It.IsAny<TestGameEvent>())).Callback(() => { });
            mockEventBus.Setup(e => e.DispatchQueuedEvents()).Callback(() => { });

            var mockDelayScheduler = new Mock<IDelayScheduler>(MockBehavior.Loose);
            mockDelayScheduler.Setup(d => d.ScheduleDelay(It.IsAny<float>(), It.IsAny<IAction>(), It.IsAny<IEntity>())).Callback(() => { });
            mockDelayScheduler.Setup(d => d.Update(It.IsAny<float>())).Callback(() => { });
            mockDelayScheduler.Setup(d => d.ClearForEntity(It.IsAny<IEntity>())).Callback(() => { });
            mockDelayScheduler.Setup(d => d.ClearAll()).Callback(() => { });

            var mockGameDataProvider = new Mock<IGameDataProvider>(MockBehavior.Loose);
            mockGameDataProvider.Setup(g => g.GetTable(It.IsAny<string>())).Returns((Andastra.Parsing.Formats.TwoDA.TwoDA)null);

            // Create mock world
            var mockWorld = new Mock<IWorld>(MockBehavior.Loose);

            // Set up properties with mocks/interfaces
            mockWorld.SetupProperty(w => w.CurrentArea, (IArea)null);
            mockWorld.SetupProperty(w => w.CurrentModule, (IModule)null);
            mockWorld.Setup(w => w.TimeManager).Returns(mockTimeManager.Object);
            mockWorld.Setup(w => w.EventBus).Returns(mockEventBus.Object);
            mockWorld.Setup(w => w.DelayScheduler).Returns(mockDelayScheduler.Object);
            mockWorld.Setup(w => w.GameDataProvider).Returns(mockGameDataProvider.Object);

            // Create real system instances (these are concrete classes that require IWorld)
            // We use the mock world object so they can interact with it
            var worldObject = mockWorld.Object;
            var combatSystem = new CombatSystem(worldObject);
            var effectSystem = new EffectSystem(worldObject);
            var perceptionSystem = new PerceptionSystem(worldObject);
            var triggerSystem = new TriggerSystem(worldObject);
            var animationSystem = new AnimationSystem(worldObject);
            var aiController = new AIController(worldObject, combatSystem);

            mockWorld.Setup(w => w.EffectSystem).Returns(effectSystem);
            mockWorld.Setup(w => w.PerceptionSystem).Returns(perceptionSystem);
            mockWorld.Setup(w => w.CombatSystem).Returns(combatSystem);
            mockWorld.Setup(w => w.TriggerSystem).Returns(triggerSystem);
            mockWorld.Setup(w => w.AIController).Returns(aiController);
            mockWorld.Setup(w => w.AnimationSystem).Returns(animationSystem);

            // Set up CreateEntity methods
            mockWorld.Setup(w => w.CreateEntity(It.IsAny<IEntityTemplate>(), It.IsAny<Vector3>(), It.IsAny<float>()))
                .Returns<IEntityTemplate, Vector3, float>((template, position, facing) =>
                {
                    var mockEntity = new Mock<IEntity>(MockBehavior.Loose);
                    uint objectId = nextObjectId++;
                    mockEntity.Setup(e => e.ObjectId).Returns(objectId);
                    mockEntity.SetupProperty(e => e.World, mockWorld.Object);
                    mockEntity.SetupProperty(e => e.AreaId, 0u);
                    mockEntity.Setup(e => e.IsValid).Returns(true);
                    if (template != null)
                    {
                        mockEntity.SetupProperty(e => e.Tag, template.Tag ?? "");
                        mockEntity.Setup(e => e.ObjectType).Returns(template.ObjectType);
                    }
                    var entity = mockEntity.Object;
                    entitiesById[objectId] = entity;
                    allEntities.Add(entity);
                    return entity;
                });

            mockWorld.Setup(w => w.CreateEntity(It.IsAny<ObjectType>(), It.IsAny<Vector3>(), It.IsAny<float>()))
                .Returns<ObjectType, Vector3, float>((objectType, position, facing) =>
                {
                    var mockEntity = new Mock<IEntity>(MockBehavior.Loose);
                    uint objectId = nextObjectId++;
                    mockEntity.Setup(e => e.ObjectId).Returns(objectId);
                    mockEntity.SetupProperty(e => e.World, mockWorld.Object);
                    mockEntity.SetupProperty(e => e.AreaId, 0u);
                    mockEntity.Setup(e => e.IsValid).Returns(true);
                    mockEntity.Setup(e => e.ObjectType).Returns(objectType);
                    mockEntity.SetupProperty(e => e.Tag, "");
                    var entity = mockEntity.Object;
                    entitiesById[objectId] = entity;
                    allEntities.Add(entity);
                    return entity;
                });

            // Set up GetEntity
            mockWorld.Setup(w => w.GetEntity(It.IsAny<uint>()))
                .Returns<uint>(objectId => entitiesById.ContainsKey(objectId) ? entitiesById[objectId] : null);

            // Set up GetEntityByTag
            mockWorld.Setup(w => w.GetEntityByTag(It.IsAny<string>(), It.IsAny<int>()))
                .Returns<string, int>((tag, nth) =>
                {
                    if (string.IsNullOrEmpty(tag) || !entitiesByTag.ContainsKey(tag))
                        return null;
                    var entities = entitiesByTag[tag];
                    if (nth < 0 || nth >= entities.Count)
                        return null;
                    return entities[nth];
                });

            // Set up RegisterEntity
            mockWorld.Setup(w => w.RegisterEntity(It.IsAny<IEntity>()))
                .Callback<IEntity>(entity =>
                {
                    if (entity == null) return;
                    entitiesById[entity.ObjectId] = entity;
                    if (!allEntities.Contains(entity))
                        allEntities.Add(entity);
                    if (!string.IsNullOrEmpty(entity.Tag))
                    {
                        if (!entitiesByTag.ContainsKey(entity.Tag))
                            entitiesByTag[entity.Tag] = new List<IEntity>();
                        if (!entitiesByTag[entity.Tag].Contains(entity))
                            entitiesByTag[entity.Tag].Add(entity);
                    }
                    if (!entitiesByType.ContainsKey(entity.ObjectType))
                        entitiesByType[entity.ObjectType] = new List<IEntity>();
                    if (!entitiesByType[entity.ObjectType].Contains(entity))
                        entitiesByType[entity.ObjectType].Add(entity);
                });

            // Set up UnregisterEntity
            mockWorld.Setup(w => w.UnregisterEntity(It.IsAny<IEntity>()))
                .Callback<IEntity>(entity =>
                {
                    if (entity == null) return;
                    entitiesById.Remove(entity.ObjectId);
                    allEntities.Remove(entity);
                    if (!string.IsNullOrEmpty(entity.Tag) && entitiesByTag.ContainsKey(entity.Tag))
                        entitiesByTag[entity.Tag].Remove(entity);
                    if (entitiesByType.ContainsKey(entity.ObjectType))
                        entitiesByType[entity.ObjectType].Remove(entity);
                });

            // Set up DestroyEntity
            mockWorld.Setup(w => w.DestroyEntity(It.IsAny<uint>()))
                .Callback<uint>(objectId =>
                {
                    if (entitiesById.ContainsKey(objectId))
                    {
                        var entity = entitiesById[objectId];
                        entitiesById.Remove(objectId);
                        allEntities.Remove(entity);
                        if (!string.IsNullOrEmpty(entity.Tag) && entitiesByTag.ContainsKey(entity.Tag))
                            entitiesByTag[entity.Tag].Remove(entity);
                        if (entitiesByType.ContainsKey(entity.ObjectType))
                            entitiesByType[entity.ObjectType].Remove(entity);
                    }
                });

            // Set up GetEntitiesInRadius
            mockWorld.Setup(w => w.GetEntitiesInRadius(It.IsAny<Vector3>(), It.IsAny<float>(), It.IsAny<ObjectType>()))
                .Returns<Vector3, float, ObjectType>((center, radius, typeMask) =>
                {
                    var results = new List<IEntity>();
                    foreach (var entity in allEntities)
                    {
                        if ((entity.ObjectType & typeMask) == 0)
                            continue;
                        var transform = entity.GetComponent<ITransformComponent>();
                        if (transform == null)
                            continue;
                        float distance = Vector3.Distance(center, transform.Position);
                        if (distance <= radius)
                            results.Add(entity);
                    }
                    return results;
                });

            // Set up GetEntitiesOfType
            mockWorld.Setup(w => w.GetEntitiesOfType(It.IsAny<ObjectType>()))
                .Returns<ObjectType>(type =>
                {
                    var results = new List<IEntity>();
                    foreach (var entity in allEntities)
                    {
                        if ((entity.ObjectType & type) != 0)
                            results.Add(entity);
                    }
                    return results;
                });

            // Set up GetAllEntities
            mockWorld.Setup(w => w.GetAllEntities())
                .Returns(() => new List<IEntity>(allEntities));

            // Set up GetArea
            mockWorld.Setup(w => w.GetArea(It.IsAny<uint>()))
                .Returns<uint>(areaId => areasById.ContainsKey(areaId) ? areasById[areaId] : null);

            // Set up RegisterArea
            mockWorld.Setup(w => w.RegisterArea(It.IsAny<IArea>()))
                .Callback<IArea>(area =>
                {
                    if (area == null) return;
                    if (!areaIds.ContainsKey(area))
                    {
                        uint areaId = nextAreaId++;
                        areaIds[area] = areaId;
                        areasById[areaId] = area;
                    }
                });

            // Set up UnregisterArea
            mockWorld.Setup(w => w.UnregisterArea(It.IsAny<IArea>()))
                .Callback<IArea>(area =>
                {
                    if (area == null) return;
                    if (areaIds.ContainsKey(area))
                    {
                        uint areaId = areaIds[area];
                        areaIds.Remove(area);
                        areasById.Remove(areaId);
                    }
                });

            // Set up GetAreaId
            mockWorld.Setup(w => w.GetAreaId(It.IsAny<IArea>()))
                .Returns<IArea>(area => area != null && areaIds.ContainsKey(area) ? areaIds[area] : 0u);

            // Set up GetAllAreas
            mockWorld.Setup(w => w.GetAllAreas())
                .Returns(() => new List<IArea>(areasById.Values));

            // Set up GetModuleId
            mockWorld.Setup(w => w.GetModuleId(It.IsAny<IModule>()))
                .Returns<IModule>(module =>
                {
                    if (module == null)
                        return 0u;
                    if (module == mockWorld.Object.CurrentModule)
                        return ModuleObjectId;
                    return 0u;
                });

            // Set up Update
            mockWorld.Setup(w => w.Update(It.IsAny<float>()))
                .Callback<float>(deltaTime =>
                {
                    // Update time manager and delay scheduler
                    mockTimeManager.Object.Update(deltaTime);
                    mockDelayScheduler.Object.Update(deltaTime);
                    // Note: Real system instances (EffectSystem, CombatSystem, etc.) would update themselves
                    // but for a mock world, we just update the mocked interfaces
                });

            return mockWorld.Object;
        }

        /// <summary>
        /// Creates a test transform component with specified values.
        /// </summary>
        public static ITransformComponent CreateTestTransformComponent(
            float x = 10.0f,
            float y = 20.0f,
            float z = 30.0f,
            float facing = 1.57f,
            float scaleX = 1.0f,
            float scaleY = 1.0f,
            float scaleZ = 1.0f)
        {
            // Use TestTransformComponent as a minimal concrete implementation of BaseTransformComponent
            // This provides real implementation behavior instead of mocking, making tests more reliable
            var transform = new TestTransformComponent(new Vector3(x, y, z), facing);
            transform.Scale = new Vector3(scaleX, scaleY, scaleZ);
            return transform;
        }

        /// <summary>
        /// Creates a test stats component with specified values.
        /// </summary>
        public static IStatsComponent CreateTestStatsComponent(
            int currentHP = 100,
            int maxHP = 100,
            int currentFP = 50,
            int maxFP = 50)
        {
            var mockStats = new Mock<IStatsComponent>(MockBehavior.Strict);
            mockStats.Setup(s => s.CurrentHP).Returns(currentHP);
            mockStats.Setup(s => s.MaxHP).Returns(maxHP);
            mockStats.Setup(s => s.CurrentFP).Returns(currentFP);
            mockStats.Setup(s => s.MaxFP).Returns(maxFP);
            mockStats.Setup(s => s.IsDead).Returns(currentHP <= 0);
            mockStats.Setup(s => s.BaseAttackBonus).Returns(5);
            mockStats.Setup(s => s.ArmorClass).Returns(15);
            mockStats.Setup(s => s.FortitudeSave).Returns(8);
            mockStats.Setup(s => s.ReflexSave).Returns(6);
            mockStats.Setup(s => s.WillSave).Returns(4);
            mockStats.Setup(s => s.WalkSpeed).Returns(2.0f);
            mockStats.Setup(s => s.RunSpeed).Returns(4.0f);
            mockStats.Setup(s => s.Level).Returns(5);

            // Setup ability getters
            mockStats.Setup(s => s.GetAbility(Ability.Strength)).Returns(16);
            mockStats.Setup(s => s.GetAbility(Ability.Dexterity)).Returns(14);
            mockStats.Setup(s => s.GetAbility(Ability.Constitution)).Returns(15);
            mockStats.Setup(s => s.GetAbility(Ability.Intelligence)).Returns(12);
            mockStats.Setup(s => s.GetAbility(Ability.Wisdom)).Returns(13);
            mockStats.Setup(s => s.GetAbility(Ability.Charisma)).Returns(10);

            // Setup ability modifiers
            mockStats.Setup(s => s.GetAbilityModifier(Ability.Strength)).Returns(3);
            mockStats.Setup(s => s.GetAbilityModifier(Ability.Dexterity)).Returns(2);
            mockStats.Setup(s => s.GetAbilityModifier(Ability.Constitution)).Returns(2);
            mockStats.Setup(s => s.GetAbilityModifier(Ability.Intelligence)).Returns(1);
            mockStats.Setup(s => s.GetAbilityModifier(Ability.Wisdom)).Returns(1);
            mockStats.Setup(s => s.GetAbilityModifier(Ability.Charisma)).Returns(0);

            // Setup setters
            mockStats.SetupProperty(s => s.CurrentHP, currentHP);
            mockStats.SetupProperty(s => s.MaxHP, maxHP);
            mockStats.SetupProperty(s => s.CurrentFP, currentFP);
            mockStats.SetupProperty(s => s.MaxFP, maxFP);
            mockStats.SetupProperty(s => s.WalkSpeed, 2.0f);
            mockStats.SetupProperty(s => s.RunSpeed, 4.0f);

            // Setup SetAbility
            var abilityScores = new Dictionary<Ability, int>
            {
                { Ability.Strength, 16 },
                { Ability.Dexterity, 14 },
                { Ability.Constitution, 15 },
                { Ability.Intelligence, 12 },
                { Ability.Wisdom, 13 },
                { Ability.Charisma, 10 }
            };

            mockStats.Setup(s => s.SetAbility(It.IsAny<Ability>(), It.IsAny<int>()))
                .Callback<Ability, int>((ability, value) => abilityScores[ability] = value);
            mockStats.Setup(s => s.GetAbility(It.IsAny<Ability>()))
                .Returns<Ability>(ability => abilityScores[ability]);

            return mockStats.Object;
        }

        /// <summary>
        /// Creates a test script hooks component with specified scripts and local variables.
        /// </summary>
        public static IScriptHooksComponent CreateTestScriptHooksComponent(
            Dictionary<ScriptEvent, string> scripts = null,
            Dictionary<string, int> localInts = null,
            Dictionary<string, float> localFloats = null,
            Dictionary<string, string> localStrings = null)
        {
            scripts = scripts ?? new Dictionary<ScriptEvent, string>();
            localInts = localInts ?? new Dictionary<string, int>();
            localFloats = localFloats ?? new Dictionary<string, float>();
            localStrings = localStrings ?? new Dictionary<string, string>();

            // Use BaseScriptHooksComponent as concrete implementation
            var component = new BaseScriptHooksComponent();

            // Set scripts
            foreach (var kvp in scripts)
            {
                component.SetScript(kvp.Key, kvp.Value);
            }

            // Set local variables using reflection
            Type componentType = typeof(BaseScriptHooksComponent);
            FieldInfo localIntsField = componentType.GetField("_localInts", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo localFloatsField = componentType.GetField("_localFloats", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo localStringsField = componentType.GetField("_localStrings", BindingFlags.NonPublic | BindingFlags.Instance);

            if (localIntsField != null)
            {
                var dict = localIntsField.GetValue(component) as Dictionary<string, int>;
                if (dict != null)
                {
                    foreach (var kvp in localInts)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                }
            }

            if (localFloatsField != null)
            {
                var dict = localFloatsField.GetValue(component) as Dictionary<string, float>;
                if (dict != null)
                {
                    foreach (var kvp in localFloats)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                }
            }

            if (localStringsField != null)
            {
                var dict = localStringsField.GetValue(component) as Dictionary<string, string>;
                if (dict != null)
                {
                    foreach (var kvp in localStrings)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                }
            }

            return component;
        }

        /// <summary>
        /// Creates a test inventory component with specified items.
        /// </summary>
        public static IInventoryComponent CreateTestInventoryComponent(
            Dictionary<int, IEntity> items = null)
        {
            items = items ?? new Dictionary<int, IEntity>();

            var mockInventory = new Mock<IInventoryComponent>(MockBehavior.Strict);

            // Setup GetItemInSlot
            mockInventory.Setup(i => i.GetItemInSlot(It.IsAny<int>()))
                .Returns<int>(slot => items.ContainsKey(slot) ? items[slot] : null);

            // Setup HasItemByTag
            mockInventory.Setup(i => i.HasItemByTag(It.IsAny<string>()))
                .Returns<string>(tag =>
                {
                    foreach (var item in items.Values)
                    {
                        if (item != null && item.Tag == tag)
                            return true;
                    }
                    return false;
                });

            // Setup GetAllItems
            mockInventory.Setup(i => i.GetAllItems())
                .Returns(() => new List<IEntity>(items.Values));

            return mockInventory.Object;
        }

        /// <summary>
        /// Creates a test door component with specified values.
        /// </summary>
        public static IDoorComponent CreateTestDoorComponent(
            bool isOpen = false,
            bool isLocked = false,
            int lockDC = 20,
            int hitPoints = 50,
            int maxHitPoints = 50)
        {
            var mockDoor = new Mock<IDoorComponent>(MockBehavior.Strict);
            mockDoor.SetupProperty(d => d.IsOpen, isOpen);
            mockDoor.SetupProperty(d => d.IsLocked, isLocked);
            mockDoor.SetupProperty(d => d.LockableByScript, true);
            mockDoor.SetupProperty(d => d.LockDC, lockDC);
            mockDoor.SetupProperty(d => d.IsBashed, false);
            mockDoor.SetupProperty(d => d.HitPoints, hitPoints);
            mockDoor.SetupProperty(d => d.MaxHitPoints, maxHitPoints);
            mockDoor.SetupProperty(d => d.Hardness, 5);
            mockDoor.SetupProperty(d => d.KeyTag, "");
            mockDoor.SetupProperty(d => d.KeyRequired, false);
            mockDoor.SetupProperty(d => d.OpenState, 0);
            mockDoor.SetupProperty(d => d.LinkedTo, "");
            mockDoor.SetupProperty(d => d.LinkedToModule, "");
            return mockDoor.Object;
        }

        /// <summary>
        /// Creates a test placeable component with specified values.
        /// </summary>
        public static IPlaceableComponent CreateTestPlaceableComponent(
            bool isUseable = true,
            bool hasInventory = false,
            bool isOpen = false,
            bool isLocked = false)
        {
            var mockPlaceable = new Mock<IPlaceableComponent>(MockBehavior.Strict);
            mockPlaceable.SetupProperty(p => p.IsUseable, isUseable);
            mockPlaceable.SetupProperty(p => p.HasInventory, hasInventory);
            mockPlaceable.SetupProperty(p => p.IsStatic, false);
            mockPlaceable.SetupProperty(p => p.IsOpen, isOpen);
            mockPlaceable.SetupProperty(p => p.IsLocked, isLocked);
            mockPlaceable.SetupProperty(p => p.LockDC, 15);
            mockPlaceable.SetupProperty(p => p.KeyTag, "");
            mockPlaceable.SetupProperty(p => p.HitPoints, 30);
            mockPlaceable.SetupProperty(p => p.MaxHitPoints, 30);
            mockPlaceable.SetupProperty(p => p.Hardness, 3);
            mockPlaceable.SetupProperty(p => p.AnimationState, 0);
            return mockPlaceable.Object;
        }

        /// <summary>
        /// Sets custom data on an entity using reflection.
        /// </summary>
        public static void SetCustomData(IEntity entity, string key, object value)
        {
            Type baseEntityType = typeof(Andastra.Runtime.Games.Common.BaseEntity);
            FieldInfo dataField = baseEntityType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            if (dataField != null)
            {
                var data = dataField.GetValue(entity) as Dictionary<string, object>;
                if (data == null)
                {
                    data = new Dictionary<string, object>();
                    dataField.SetValue(entity, data);
                }
                data[key] = value;
            }
        }

        /// <summary>
        /// Gets custom data from an entity using reflection.
        /// </summary>
        public static object GetCustomData(IEntity entity, string key)
        {
            Type baseEntityType = typeof(Andastra.Runtime.Games.Common.BaseEntity);
            FieldInfo dataField = baseEntityType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            if (dataField != null)
            {
                var data = dataField.GetValue(entity) as Dictionary<string, object>;
                if (data != null && data.ContainsKey(key))
                {
                    return data[key];
                }
            }
            return null;
        }

        /// <summary>
        /// Creates a simple test entity for inventory items.
        /// </summary>
        public static IEntity CreateTestItemEntity(uint objectId, string tag, ObjectType objectType)
        {
            var mockItem = new Mock<IEntity>(MockBehavior.Strict);
            mockItem.Setup(i => i.ObjectId).Returns(objectId);
            mockItem.SetupProperty(i => i.Tag, tag);
            mockItem.Setup(i => i.ObjectType).Returns(objectType);
            mockItem.Setup(i => i.IsValid).Returns(true);
            mockItem.SetupProperty(i => i.AreaId, 0u);
            mockItem.SetupProperty(i => i.World, (IWorld)null);
            return mockItem.Object;
        }
    }

    /// <summary>
    /// Minimal concrete implementation of BaseTransformComponent for testing purposes.
    /// </summary>
    /// <remarks>
    /// This class provides a concrete implementation of BaseTransformComponent that can be instantiated
    /// in tests. BaseTransformComponent is abstract to prevent direct instantiation in production code,
    /// but for testing we need a concrete class that uses the real implementation logic.
    ///
    /// This implementation uses all the functionality from BaseTransformComponent including:
    /// - Position, Facing, Scale properties with proper change tracking
    /// - Forward/Right direction vectors calculated from facing angle
    /// - WorldMatrix computation with caching and parent transform support
    /// - All utility methods (Translate, MoveForward, Rotate, LookAt, DistanceTo, etc.)
    ///
    /// This is superior to mocking because:
    /// - Uses real implementation logic, making tests more reliable
    /// - Avoids the complexity of mocking all methods and properties
    /// - Tests actual behavior rather than mock setup
    /// - Automatically includes all BaseTransformComponent functionality
    /// </remarks>
    internal class TestTransformComponent : BaseTransformComponent
    {
        /// <summary>
        /// Creates a new TestTransformComponent with default values.
        /// </summary>
        public TestTransformComponent()
            : base()
        {
        }

        /// <summary>
        /// Creates a new TestTransformComponent with specified position and facing.
        /// </summary>
        /// <param name="position">Initial position.</param>
        /// <param name="facing">Initial facing angle in radians.</param>
        public TestTransformComponent(Vector3 position, float facing)
            : base(position, facing)
        {
        }
    }
}


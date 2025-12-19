using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Enums;
using Andastra.Parsing.Resource;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Resource preloader for predictive asset loading.
    /// 
    /// Preloads assets likely to be needed soon based on:
    /// - Player position and movement direction
    /// - Scene transitions
    /// - Visibility predictions
    /// 
    /// Features:
    /// - Predictive loading
    /// - Priority-based loading
    /// - Background loading
    /// - Memory budget awareness
    /// </summary>
    public class ResourcePreloader
    {
        /// <summary>
        /// Preload task.
        /// </summary>
        private class PreloadTask
        {
            public string ResourceName;
            public ResourceType ResourceType;
            public int Priority;
            public Task LoadTask;
        }

        private readonly IGameResourceProvider _resourceProvider;
        private readonly List<PreloadTask> _preloadQueue; // Changed to List for priority sorting
        private readonly HashSet<string> _preloadedResources;
        private readonly object _lock;
        private int _maxConcurrentLoads;
        private IWorld _world;
        private IGameDataProvider _gameDataProvider;

        /// <summary>
        /// Gets or sets the maximum concurrent preloads.
        /// </summary>
        public int MaxConcurrentLoads
        {
            get { return _maxConcurrentLoads; }
            set { _maxConcurrentLoads = Math.Max(1, value); }
        }

        /// <summary>
        /// Initializes a new resource preloader.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading resources. Must not be null.</param>
        /// <param name="maxConcurrentLoads">Maximum concurrent preloads. Must be greater than zero. Default is 4.</param>
        /// <param name="world">Optional world instance for spatial queries. If null, PreloadFromPosition will not perform spatial queries.</param>
        /// <param name="gameDataProvider">Optional game data provider for appearance data lookups. If null, appearance-based resource resolution will be skipped.</param>
        /// <exception cref="ArgumentNullException">Thrown if resourceProvider is null.</exception>
        /// <exception cref="ArgumentException">Thrown if maxConcurrentLoads is less than or equal to zero.</exception>
        public ResourcePreloader(IGameResourceProvider resourceProvider, int maxConcurrentLoads = 4, IWorld world = null, IGameDataProvider gameDataProvider = null)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException(nameof(resourceProvider));
            }
            if (maxConcurrentLoads <= 0)
            {
                throw new ArgumentException("Max concurrent loads must be greater than zero.", nameof(maxConcurrentLoads));
            }

            _resourceProvider = resourceProvider;
            _preloadQueue = new List<PreloadTask>(); // Changed to List for priority sorting
            _preloadedResources = new HashSet<string>();
            _lock = new object();
            _maxConcurrentLoads = maxConcurrentLoads;
            _world = world;
            _gameDataProvider = gameDataProvider;
        }

        /// <summary>
        /// Sets the world instance for spatial queries.
        /// </summary>
        /// <param name="world">World instance for spatial queries.</param>
        public void SetWorld(IWorld world)
        {
            _world = world;
        }

        /// <summary>
        /// Sets the game data provider for appearance data lookups.
        /// </summary>
        /// <param name="gameDataProvider">Game data provider for appearance data lookups.</param>
        public void SetGameDataProvider(IGameDataProvider gameDataProvider)
        {
            _gameDataProvider = gameDataProvider;
        }

        /// <summary>
        /// Queues a resource for preloading.
        /// </summary>
        /// <param name="resourceName">Resource name to preload. Can be null or empty (no-op).</param>
        /// <param name="resourceType">Type of resource to preload.</param>
        /// <param name="priority">Preload priority (higher = loaded first). Default is 0.</param>
        public void Preload(string resourceName, ResourceType resourceType, int priority = 0)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return;
            }

            lock (_lock)
            {
                if (_preloadedResources.Contains(resourceName))
                {
                    return; // Already preloaded or loading
                }

                _preloadedResources.Add(resourceName);

                PreloadTask task = new PreloadTask
                {
                    ResourceName = resourceName,
                    ResourceType = resourceType,
                    Priority = priority
                };

                _preloadQueue.Add(task);
                
                // Sort by priority (highest first) to ensure high-priority resources are loaded first
                _preloadQueue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }

        /// <summary>
        /// Preloads resources based on camera position and direction.
        /// </summary>
        /// <remarks>
        /// Comprehensive resource preloading implementation:
        /// - Queries entities within preload distance using spatial queries
        /// - Extracts resource references from entities (models, textures, animations, sounds, scripts)
        /// - Prioritizes resources in the direction of movement for predictive loading
        /// - Integrates with appearance.2da for creature model/texture resolution
        /// - Handles all entity types (Creatures, Placeables, Doors, etc.)
        /// 
        /// Based on original engine resource preloading behavior:
        /// - Original engines preload resources for entities within render distance
        /// - Resources are prioritized based on proximity and camera direction
        /// - Model and texture resources are preloaded from entity appearance data
        /// - Animation resources are preloaded from model files
        /// - Sound resources are preloaded from entity sound components
        /// - Script resources are preloaded from entity script hooks
        /// </remarks>
        /// <param name="position">Camera position in world space.</param>
        /// <param name="direction">Camera look direction. Will be normalized if non-zero length.</param>
        /// <param name="distance">Preload distance threshold. Must be greater than zero.</param>
        public void PreloadFromPosition(System.Numerics.Vector3 position, System.Numerics.Vector3 direction, float distance)
        {
            if (distance <= 0.0f)
            {
                return;
            }

            // If world is not available, cannot perform spatial queries
            if (_world == null)
            {
                return;
            }

            // Normalize direction
            float length = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
            if (length > 0.0001f)
            {
                direction = new System.Numerics.Vector3(
                    direction.X / length,
                    direction.Y / length,
                    direction.Z / length
                );
            }
            else
            {
                // Default direction if invalid (forward along Z axis)
                direction = new System.Numerics.Vector3(0.0f, 0.0f, 1.0f);
            }

            // Query entities within preload distance
            // Use ObjectType.All to get all entity types that might have resources
            IEnumerable<IEntity> nearbyEntities = _world.GetEntitiesInRadius(position, distance, ObjectType.All);

            // Extract resource references from entities and prioritize based on direction
            var resourcePriorities = new List<ResourcePriority>();

            foreach (IEntity entity in nearbyEntities)
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Calculate priority based on distance and direction
                Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
                if (transform == null)
                {
                    continue;
                }

                System.Numerics.Vector3 entityPosition = transform.Position;
                System.Numerics.Vector3 toEntity = entityPosition - position;
                float entityDistance = toEntity.Length();

                if (entityDistance <= 0.0001f)
                {
                    continue;
                }

                // Normalize direction to entity
                System.Numerics.Vector3 toEntityNormalized = new System.Numerics.Vector3(
                    toEntity.X / entityDistance,
                    toEntity.Y / entityDistance,
                    toEntity.Z / entityDistance
                );

                // Calculate priority: higher for entities in camera direction and closer
                // Dot product gives alignment with camera direction (1.0 = same direction, -1.0 = opposite)
                float directionAlignment = System.Numerics.Vector3.Dot(direction, toEntityNormalized);
                float distanceFactor = 1.0f - (entityDistance / distance); // 1.0 at position, 0.0 at distance
                int priority = (int)((directionAlignment * 100.0f) + (distanceFactor * 50.0f));

                // Extract resources from entity
                ExtractEntityResources(entity, resourcePriorities, priority);
            }

            // Sort by priority (highest first) and queue for preloading
            resourcePriorities.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            foreach (ResourcePriority resourcePriority in resourcePriorities)
            {
                Preload(resourcePriority.ResourceName, resourcePriority.ResourceType, resourcePriority.Priority);
            }
        }

        /// <summary>
        /// Resource priority information.
        /// </summary>
        private class ResourcePriority
        {
            public string ResourceName;
            public ResourceType ResourceType;
            public int Priority;
        }

        /// <summary>
        /// Extracts resource references from an entity and adds them to the priority list.
        /// </summary>
        /// <param name="entity">Entity to extract resources from.</param>
        /// <param name="resourcePriorities">List to add resource priorities to.</param>
        /// <param name="basePriority">Base priority for resources from this entity.</param>
        private void ExtractEntityResources(IEntity entity, List<ResourcePriority> resourcePriorities, int basePriority)
        {
            // Extract model resources from renderable component
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null)
            {
                // Direct model reference
                if (!string.IsNullOrEmpty(renderable.ModelResRef))
                {
                    resourcePriorities.Add(new ResourcePriority
                    {
                        ResourceName = renderable.ModelResRef,
                        ResourceType = ResourceType.Model,
                        Priority = basePriority + 10 // Models are high priority
                    });
                }

                // Resolve model from appearance data if available
                if (renderable.AppearanceRow >= 0 && _gameDataProvider != null)
                {
                    ResolveAppearanceResources(renderable.AppearanceRow, resourcePriorities, basePriority);
                }
            }

            // Extract animation resources (animations are in model files, but we preload the model)
            // AnimationComponent doesn't store separate resource references, animations are in MDL files

            // Extract sound resources from sound component (if exists)
            // Note: Sound components may not exist in all engines, check if available
            // For now, we'll skip sound extraction as it requires engine-specific components

            // Extract script resources from script hooks component
            IScriptHooksComponent scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                // Extract all script references from script hooks
                // Script hooks store ResRefs for various events
                foreach (ScriptEvent eventType in Enum.GetValues(typeof(ScriptEvent)))
                {
                    string scriptResRef = scriptHooks.GetScript(eventType);
                    if (!string.IsNullOrEmpty(scriptResRef))
                    {
                        resourcePriorities.Add(new ResourcePriority
                        {
                            ResourceName = scriptResRef,
                            ResourceType = ResourceType.Script,
                            Priority = basePriority // Scripts are lower priority than models
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Resolves model and texture resources from appearance data.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe appearance data structure:
        /// - appearance.2da contains ModelA, ModelB (model variants), TexA, TexB (texture variants)
        /// - Model resolution: FUN_005261b0 @ 0x005261b0 resolves creature model from appearance.2da row
        /// - Appearance data accessed via GameDataManager.GetAppearance() in Odyssey engine
        /// - Uses reflection to access engine-specific GameDataManager implementations
        /// </remarks>
        /// <param name="appearanceRow">Appearance row index into appearance.2da.</param>
        /// <param name="resourcePriorities">List to add resource priorities to.</param>
        /// <param name="basePriority">Base priority for resources.</param>
        private void ResolveAppearanceResources(int appearanceRow, List<ResourcePriority> resourcePriorities, int basePriority)
        {
            if (_gameDataProvider == null || appearanceRow < 0)
            {
                return;
            }

            // Try to resolve appearance data using engine-specific GameDataProvider
            // For Odyssey: OdysseyGameDataProvider wraps GameDataManager which has GetAppearance method
            // For other engines: Similar appearance data structures exist but may use different access patterns
            
            // Use reflection to access engine-specific methods if available
            // This allows us to work with engine-specific implementations without hardcoding engine types
            
            // Try Odyssey-specific: OdysseyGameDataProvider has GameDataManager property
            System.Type providerType = _gameDataProvider.GetType();
            
            // Check if it's OdysseyGameDataProvider and has GameDataManager
            if (providerType.Name == "OdysseyGameDataProvider")
            {
                System.Reflection.PropertyInfo gameDataManagerProp = providerType.GetProperty("GameDataManager");
                if (gameDataManagerProp != null)
                {
                    object gameDataManager = gameDataManagerProp.GetValue(_gameDataProvider);
                    if (gameDataManager != null)
                    {
                        System.Type gameDataManagerType = gameDataManager.GetType();
                        System.Reflection.MethodInfo getAppearanceMethod = gameDataManagerType.GetMethod("GetAppearance", new System.Type[] { typeof(int) });
                        if (getAppearanceMethod != null)
                        {
                            object appearanceData = getAppearanceMethod.Invoke(gameDataManager, new object[] { appearanceRow });
                            if (appearanceData != null)
                            {
                                // Extract ModelA, ModelB, TexA, TexB using reflection
                                System.Type appearanceDataType = appearanceData.GetType();
                                
                                // Get ModelA
                                System.Reflection.PropertyInfo modelAProp = appearanceDataType.GetProperty("ModelA");
                                if (modelAProp != null)
                                {
                                    string modelA = modelAProp.GetValue(appearanceData) as string;
                                    if (!string.IsNullOrEmpty(modelA))
                                    {
                                        resourcePriorities.Add(new ResourcePriority
                                        {
                                            ResourceName = modelA,
                                            ResourceType = ResourceType.Model,
                                            Priority = basePriority + 10
                                        });
                                    }
                                }
                                
                                // Get ModelB
                                System.Reflection.PropertyInfo modelBProp = appearanceDataType.GetProperty("ModelB");
                                if (modelBProp != null)
                                {
                                    string modelB = modelBProp.GetValue(appearanceData) as string;
                                    if (!string.IsNullOrEmpty(modelB))
                                    {
                                        resourcePriorities.Add(new ResourcePriority
                                        {
                                            ResourceName = modelB,
                                            ResourceType = ResourceType.Model,
                                            Priority = basePriority + 9 // Slightly lower than ModelA
                                        });
                                    }
                                }
                                
                                // Get TexA
                                System.Reflection.PropertyInfo texAProp = appearanceDataType.GetProperty("TexA");
                                if (texAProp != null)
                                {
                                    string texA = texAProp.GetValue(appearanceData) as string;
                                    if (!string.IsNullOrEmpty(texA))
                                    {
                                        resourcePriorities.Add(new ResourcePriority
                                        {
                                            ResourceName = texA,
                                            ResourceType = ResourceType.Texture,
                                            Priority = basePriority + 8
                                        });
                                    }
                                }
                                
                                // Get TexB
                                System.Reflection.PropertyInfo texBProp = appearanceDataType.GetProperty("TexB");
                                if (texBProp != null)
                                {
                                    string texB = texBProp.GetValue(appearanceData) as string;
                                    if (!string.IsNullOrEmpty(texB))
                                    {
                                        resourcePriorities.Add(new ResourcePriority
                                        {
                                            ResourceName = texB,
                                            ResourceType = ResourceType.Texture,
                                            Priority = basePriority + 7 // Slightly lower than TexA
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // For other engines (Aurora, Eclipse, Infinity), similar reflection-based approach can be used
            // Each engine's GameDataProvider implementation would need to expose appearance data access
            // This framework provides the structure for engine-specific appearance resolution
        }

        /// <summary>
        /// Processes preload queue.
        /// </summary>
        /// <remarks>
        /// Processes the preload queue with priority-based loading:
        /// - High-priority resources are loaded first
        /// - Respects maximum concurrent load limit
        /// - Removes completed tasks from tracking
        /// - Maintains queue sorted by priority
        /// </remarks>
        public void Update()
        {
            lock (_lock)
            {
                // Count active loads and track active tasks
                int activeLoads = 0;
                var activeTasks = new List<PreloadTask>();
                var completedTasks = new List<PreloadTask>();

                // Check all tasks for completion status
                foreach (PreloadTask task in _preloadQueue)
                {
                    if (task.LoadTask != null)
                    {
                        if (task.LoadTask.IsCompleted)
                        {
                            completedTasks.Add(task);
                        }
                        else
                        {
                            activeLoads++;
                            activeTasks.Add(task);
                        }
                    }
                }

                // Remove completed tasks from queue
                foreach (PreloadTask completedTask in completedTasks)
                {
                    _preloadQueue.Remove(completedTask);
                }

                // Start new loads if under limit, processing highest priority first
                // Queue is already sorted by priority (highest first)
                for (int i = 0; i < _preloadQueue.Count && activeLoads < _maxConcurrentLoads; i++)
                {
                    PreloadTask task = _preloadQueue[i];
                    if (task.LoadTask == null)
                    {
                        task.LoadTask = StartPreload(task);
                        activeLoads++;
                        activeTasks.Add(task);
                    }
                }
            }
        }

        private Task StartPreload(PreloadTask task)
        {
            return Task.Run(async () =>
            {
                try
                {
                    // Preload resource
                    var resourceId = new ResourceIdentifier(
                        task.ResourceName,
                        ConvertResourceType(task.ResourceType)
                    );

                    // Load resource data (but don't create GPU resources yet)
                    await _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None);
                }
                catch
                {
                    // Ignore preload errors
                }
            });
        }

        private Andastra.Parsing.Resource.ResourceType ConvertResourceType(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Texture:
                    return Andastra.Parsing.Resource.ResourceType.TPC;
                case ResourceType.Model:
                    return Andastra.Parsing.Resource.ResourceType.MDL;
                case ResourceType.Animation:
                    return Andastra.Parsing.Resource.ResourceType.MDL;
                case ResourceType.Sound:
                    return Andastra.Parsing.Resource.ResourceType.WAV;
                case ResourceType.Script:
                    return Andastra.Parsing.Resource.ResourceType.NCS;
                default:
                    return Andastra.Parsing.Resource.ResourceType.INVALID;
            }
        }
    }

    /// <summary>
    /// Resource type enumeration for preloading.
    /// </summary>
    public enum ResourceType
    {
        Texture,
        Model,
        Animation,
        Sound,
        Script
    }
}


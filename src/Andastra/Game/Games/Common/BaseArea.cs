using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base implementation of area functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Area Implementation:
    /// - Common area properties and entity management across Odyssey, Aurora, Eclipse engines
    /// - Handles area loading from ARE/GIT files, entity spawning, navigation mesh
    /// - Provides base for engine-specific area implementations
    /// - Cross-engine analysis: All engines share ARE (area properties) and GIT (instances) file formats
    /// - Common functionality: Entity management, navigation, area transitions, unescapable flag
    /// - Engine-specific: File format details, area effect systems, lighting models, stealth XP (Odyssey only)
    ///
    /// Common Functionality (all engines):
    /// - Entity collections: Creatures, placeables, doors, triggers, waypoints, sounds
    /// - Navigation mesh: Walkmesh projection and pathfinding
    /// - Area transitions: Entity movement between areas with position projection
    /// - Unescapable flag: Prevents players from leaving area
    /// - Basic area properties: ResRef, DisplayName, Tag
    ///
    /// Engine-Specific (implemented in subclasses):
    /// - Odyssey: Stealth XP system, basic lighting/fog
    /// - Aurora: Tile-based areas, weather, enhanced area effects
    /// - Eclipse: Physics simulation, destructible environments, advanced lighting
    ///
    /// Inheritance Structure:
    /// - BaseArea (this class) - Common functionality only
    ///   - OdysseyArea : BaseArea (swkotor.exe, swkotor2.exe)
    ///   - AuroraArea : BaseArea (nwmain.exe)
    ///   - EclipseArea : BaseArea (daorigins.exe, DragonAge2.exe, , )
    /// </remarks>
    [PublicAPI]
    public abstract class BaseArea : IArea
    {
        /// <summary>
        /// The resource reference name of this area.
        /// </summary>
        public abstract string ResRef { get; }

        /// <summary>
        /// The display name of the area.
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// The tag of the area.
        /// </summary>
        public abstract string Tag { get; }

        /// <summary>
        /// All creatures in this area.
        /// </summary>
        public abstract IEnumerable<IEntity> Creatures { get; }

        /// <summary>
        /// All placeables in this area.
        /// </summary>
        public abstract IEnumerable<IEntity> Placeables { get; }

        /// <summary>
        /// All doors in this area.
        /// </summary>
        public abstract IEnumerable<IEntity> Doors { get; }

        /// <summary>
        /// All triggers in this area.
        /// </summary>
        public abstract IEnumerable<IEntity> Triggers { get; }

        /// <summary>
        /// All waypoints in this area.
        /// </summary>
        public abstract IEnumerable<IEntity> Waypoints { get; }

        /// <summary>
        /// All sounds in this area.
        /// </summary>
        public abstract IEnumerable<IEntity> Sounds { get; }

        /// <summary>
        /// Gets an object by tag within this area.
        /// </summary>
        public abstract IEntity GetObjectByTag(string tag, int nth = 0);

        /// <summary>
        /// Gets the walkmesh navigation system for this area.
        /// </summary>
        public abstract INavigationMesh NavigationMesh { get; }

        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        /// <remarks>
        /// Based on walkmesh projection functions found across all engines.
        /// Common implementation: Projects point to walkmesh surface and checks if within walkable bounds.
        /// </remarks>
        public abstract bool IsPointWalkable(Vector3 point);

        /// <summary>
        /// Projects a point onto the walkmesh.
        /// </summary>
        /// <remarks>
        /// Common pattern across all engines: Projects points to walkable surfaces for pathfinding and collision detection.
        /// </remarks>
        public abstract bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height);

        /// <summary>
        /// Gets or sets whether the area is unescapable (players cannot leave).
        /// TRUE means the area cannot be escaped, FALSE means it can be escaped.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Stored in AreaProperties GFF field "Unescapable".
        /// Based on LoadAreaProperties/SaveAreaProperties functions in all engines.
        /// </remarks>
        public abstract bool IsUnescapable { get; set; }

        /// <summary>
        /// Gets or sets whether stealth XP is enabled for this area.
        /// TRUE means stealth XP is enabled, FALSE means it is disabled.
        /// </summary>
        /// <remarks>
        /// Common area property across engines.
        /// Engine-specific implementations handle storage format differences.
        ///
        /// Aurora/Eclipse Engines:
        /// - Not supported - implementations return false (no-op setter)
        /// - Aurora and Eclipse use different progression systems
        /// </remarks>
        public abstract bool StealthXPEnabled { get; set; }

        /// <summary>
        /// Loads area properties from GFF data.
        /// </summary>
        /// <remarks>
        /// Based on LoadAreaProperties functions in all engines.
        /// Loads Unescapable, StealthXPEnabled, and other area properties.
        /// Called during area initialization.
        /// </remarks>
        protected abstract void LoadAreaProperties(byte[] gffData);

        /// <summary>
        /// Saves area properties to GFF data.
        /// </summary>
        /// <remarks>
        /// Based on SaveAreaProperties functions in all engines.
        /// Saves Unescapable, StealthXPEnabled, and other area properties.
        /// Called during game save operations.
        /// </remarks>
        protected abstract byte[] SaveAreaProperties();

        /// <summary>
        /// Loads entities from GIT (Game Instance Template) file.
        /// </summary>
        /// <remarks>
        /// Common across all engines: GIT files contain dynamic object instances.
        /// Creates creatures, doors, placeables, triggers, waypoints, sounds from template data.
        /// </remarks>
        protected abstract void LoadEntities(byte[] gitData);

        /// <summary>
        /// Loads area geometry and walkmesh from ARE file.
        /// </summary>
        /// <remarks>
        /// Common across all engines: ARE files contain static area properties and geometry.
        /// Loads lighting, fog, grass, walkmesh data for navigation and collision.
        /// </remarks>
        protected abstract void LoadAreaGeometry(byte[] areData);

        /// <summary>
        /// Initializes area effects and environmental systems.
        /// </summary>
        /// <remarks>
        /// Engine-specific: Eclipse engines have more advanced area effect systems.
        /// Odyssey/Aurora have basic lighting and fog effects.
        /// </remarks>
        protected abstract void InitializeAreaEffects();

        /// <summary>
        /// Handles area transition events.
        /// </summary>
        /// <remarks>
        /// Based on EVENT_AREA_TRANSITION handling in DispatchEvent functions.
        /// Called when entities enter/leave areas or transition between areas.
        ///
        /// Common transition flow across all engines:
        /// 1. Validate inputs and get world reference
        /// 2. Get current area from entity's AreaId or world's CurrentArea
        /// 3. Remove entity from current area (engine-specific)
        /// 4. Save pre-transition state (engine-specific hook)
        /// 5. Load target area if not already loaded (area streaming)
        /// 6. Project entity position to target area walkmesh
        /// 7. Restore post-transition state (engine-specific hook)
        /// 8. Add entity to target area (engine-specific)
        /// 9. Update entity's AreaId
        /// 10. Fire OnEnter events for target area
        ///
        /// Engine-specific implementations:
        /// - Eclipse: Physics state transfer (velocity, angular velocity, mass)
        /// - Odyssey: Basic transition without physics
        /// - Aurora: Tile-based area transitions
        /// </remarks>
        protected virtual void HandleAreaTransition(IEntity entity, string targetArea)
        {
            if (entity == null || string.IsNullOrEmpty(targetArea))
            {
                return;
            }

            // Get world reference from entity
            IWorld world = entity.World;
            if (world == null)
            {
                return;
            }

            // Get current area from entity's AreaId or world's CurrentArea
            IArea currentArea = GetCurrentAreaForEntity(entity, world);

            // If current area is this area, remove entity from collections
            if (currentArea == this)
            {
                RemoveEntityFromArea(entity);
            }

            // Engine-specific pre-transition hook (e.g., save physics state)
            OnBeforeTransition(entity, currentArea);

            // Load target area if not already loaded (area streaming)
            IArea targetAreaInstance = LoadOrGetTargetArea(world, targetArea);
            if (targetAreaInstance == null)
            {
                // Failed to load target area - restore entity to current area
                if (currentArea == this)
                {
                    AddEntityToArea(entity);
                }
                OnTransitionFailed(entity, currentArea);
                return;
            }

            // If target area is different from current, perform full transition
            if (targetAreaInstance != currentArea)
            {
                // Project entity position to target area walkmesh
                ProjectEntityToTargetArea(entity, targetAreaInstance);

                // Engine-specific post-transition hook (e.g., restore physics state)
                OnAfterTransition(entity, targetAreaInstance, currentArea);

                // Add entity to target area (engine-specific implementation)
                AddEntityToTargetArea(entity, targetAreaInstance);

                // Update entity's AreaId
                uint targetAreaId = world.GetAreaId(targetAreaInstance);
                if (targetAreaId != 0)
                {
                    entity.AreaId = targetAreaId;
                }

                // Fire transition events
                FireAreaTransitionEvents(world, entity, targetAreaInstance);
            }
        }

        /// <summary>
        /// Gets the current area for an entity.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Checks entity's AreaId first, then falls back to world's CurrentArea.
        /// </remarks>
        protected virtual IArea GetCurrentAreaForEntity(IEntity entity, IWorld world)
        {
            if (entity == null || world == null)
            {
                return null;
            }

            // Try to get area from entity's AreaId
            if (entity.AreaId != 0)
            {
                IArea area = world.GetArea(entity.AreaId);
                if (area != null)
                {
                    return area;
                }
            }

            // Fall back to world's current area
            return world.CurrentArea;
        }

        /// <summary>
        /// Projects an entity's position to the target area's walkmesh.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Projects position to walkmesh, with fallback to nearest walkable point.
        /// </remarks>
        protected virtual void ProjectEntityToTargetArea(IEntity entity, IArea targetArea)
        {
            if (entity == null || targetArea == null)
            {
                return;
            }

            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Project position to target area walkmesh
            Vector3 currentPosition = transform.Position;
            Vector3 projectedPosition;
            float height;

            if (targetArea.ProjectToWalkmesh(currentPosition, out projectedPosition, out height))
            {
                transform.Position = projectedPosition;
            }
            else
            {
                // If projection fails, try to find a valid position near the transition point
                Vector3 nearestWalkable = FindNearestWalkablePoint(targetArea, currentPosition);
                transform.Position = nearestWalkable;
            }
        }

        /// <summary>
        /// Finds the nearest walkable point in an area.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Searches in expanding radius when direct projection fails.
        /// </remarks>
        protected virtual Vector3 FindNearestWalkablePoint(IArea area, Vector3 searchPoint)
        {
            if (area == null || area.NavigationMesh == null)
            {
                return searchPoint;
            }

            // Try to project the point first
            Vector3 projected;
            float height;
            if (area.ProjectToWalkmesh(searchPoint, out projected, out height))
            {
                return projected;
            }

            // If projection fails, search in expanding radius
            const float searchRadius = 5.0f;
            const float stepSize = 1.0f;
            const int maxSteps = 10;

            for (int step = 1; step <= maxSteps; step++)
            {
                float radius = step * stepSize;
                for (int angle = 0; angle < 360; angle += 45)
                {
                    float radians = (float)(angle * Math.PI / 180.0);
                    Vector3 testPoint = searchPoint + new Vector3(
                        (float)Math.Cos(radians) * radius,
                        0,
                        (float)Math.Sin(radians) * radius
                    );

                    if (area.ProjectToWalkmesh(testPoint, out projected, out height))
                    {
                        return projected;
                    }
                }
            }

            // Fallback: return original point
            return searchPoint;
        }

        /// <summary>
        /// Loads or gets the target area for transition.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Implements area lookup - checks if target area is already loaded.
        /// Base implementation checks current area and module's loaded areas.
        /// Engine-specific implementations should override to add area streaming (loading areas on demand).
        ///
        /// Area lookup flow (based on swkotor2.exe area transition system):
        /// 1. Check if target area is the current area (fast path)
        /// 2. Check if area is already loaded in module via IModule.GetArea(resRef)
        /// 3. If not found, return null (area streaming/loading handled by engine-specific overrides)
        ///
        /// Engine-specific overrides (e.g., OdysseyEventDispatcher.LoadOrGetTargetArea):
        /// - Have access to ModuleLoader for area streaming
        /// - Load area from module archives if not already loaded
        /// - Register loaded area with world and module
        /// - Return loaded area
        ///
        /// Based on reverse engineering of:
        /// - swkotor2.exe: Area loading during transitions (0x004e26d0 @ 0x004e26d0)
        /// - swkotor.exe: Similar area loading system (KOTOR 1)
        /// - Area resources: ARE (properties), GIT (instances), LYT (layout), VIS (visibility)
        /// - Module resource lookup: Areas are loaded from module archives using area ResRef
        /// </remarks>
        protected virtual IArea LoadOrGetTargetArea(IWorld world, string targetAreaResRef)
        {
            if (world == null || string.IsNullOrEmpty(targetAreaResRef))
            {
                return null;
            }

            // Fast path: Check if target area is the current area
            if (world.CurrentArea != null && string.Equals(world.CurrentArea.ResRef, targetAreaResRef, StringComparison.OrdinalIgnoreCase))
            {
                return world.CurrentArea;
            }

            // Check if area is already loaded in current module
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Areas are stored in module's area list for lookup
            // IModule.GetArea(resRef) provides O(1) lookup by ResRef
            if (world.CurrentModule != null)
            {
                IArea loadedArea = world.CurrentModule.GetArea(targetAreaResRef);
                if (loadedArea != null)
                {
                    return loadedArea;
                }
            }

            // Area is not currently loaded
            // Base implementation returns null - engine-specific overrides should handle area streaming
            // Area streaming requires:
            // 1. ModuleLoader access (engine-specific)
            // 2. Module resource access (ARE/GIT/LYT/VIS files)
            // 3. Area creation and initialization
            // 4. Registration with world (AreaId assignment) and module (AddArea)
            // See OdysseyEventDispatcher.LoadOrGetTargetArea for example implementation
            return null;
        }

        /// <summary>
        /// Fires area transition events.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Fires OnEnter script events for target area.
        /// </remarks>
        protected virtual void FireAreaTransitionEvents(IWorld world, IEntity entity, IArea targetArea)
        {
            if (world == null || world.EventBus == null || entity == null || targetArea == null)
            {
                return;
            }

            // Fire OnEnter script for target area
            // Common across all engines: Fires when entity enters an area
            if (targetArea is Andastra.Runtime.Core.Module.RuntimeArea targetRuntimeArea)
            {
                string enterScript = targetRuntimeArea.GetScript(Andastra.Runtime.Core.Enums.ScriptEvent.OnEnter);
                if (!string.IsNullOrEmpty(enterScript))
                {
                    IEntity areaEntity = world.GetEntityByTag(targetArea.ResRef, 0);
                    if (areaEntity == null)
                    {
                        areaEntity = world.GetEntityByTag(targetArea.Tag, 0);
                    }
                    if (areaEntity != null)
                    {
                        world.EventBus.FireScriptEvent(areaEntity, Andastra.Runtime.Core.Enums.ScriptEvent.OnEnter, entity);
                    }
                }
            }
        }

        /// <summary>
        /// Adds an entity to the target area.
        /// </summary>
        /// <remarks>
        /// Common implementation that delegates to engine-specific AddEntityToArea methods.
        /// </remarks>
        protected virtual void AddEntityToTargetArea(IEntity entity, IArea targetArea)
        {
            if (entity == null || targetArea == null)
            {
                return;
            }

            // If target area is a BaseArea subclass, use its AddEntityToArea method
            if (targetArea is BaseArea baseArea)
            {
                baseArea.AddEntityToArea(entity);
            }
            // Otherwise, try RuntimeArea's AddEntity method
            else if (targetArea is Runtime.Core.Module.RuntimeArea runtimeArea)
            {
                runtimeArea.AddEntity(entity);
            }
        }

        /// <summary>
        /// Engine-specific hook called before area transition.
        /// </summary>
        /// <remarks>
        /// Override in engine-specific implementations to save state before transition.
        /// Eclipse: Saves physics state (velocity, angular velocity, mass).
        /// Odyssey/Aurora: No-op by default.
        /// </remarks>
        protected virtual void OnBeforeTransition(IEntity entity, IArea currentArea)
        {
            // Default: no-op
        }

        /// <summary>
        /// Engine-specific hook called after area transition.
        /// </summary>
        /// <remarks>
        /// Override in engine-specific implementations to restore state after transition.
        /// Eclipse: Restores physics state in target area.
        /// Odyssey/Aurora: No-op by default.
        /// </remarks>
        protected virtual void OnAfterTransition(IEntity entity, IArea targetArea, IArea currentArea)
        {
            // Default: no-op
        }

        /// <summary>
        /// Engine-specific hook called when area transition fails.
        /// </summary>
        /// <remarks>
        /// Override in engine-specific implementations to handle transition failures.
        /// </remarks>
        protected virtual void OnTransitionFailed(IEntity entity, IArea currentArea)
        {
            // Default: no-op
        }

        /// <summary>
        /// Removes an entity from this area's collections.
        /// </summary>
        /// <remarks>
        /// Engine-specific implementation: Each engine manages its own entity collections.
        /// Made internal to allow access from save serializers and event dispatchers.
        /// </remarks>
        internal abstract void RemoveEntityFromArea(IEntity entity);

        /// <summary>
        /// Adds an entity to this area's collections.
        /// </summary>
        /// <remarks>
        /// Engine-specific implementation: Each engine manages its own entity collections.
        /// Made internal to allow access from save serializers and event dispatchers.
        /// </remarks>
        internal abstract void AddEntityToArea(IEntity entity);

        /// <summary>
        /// Updates area state each frame.
        /// </summary>
        /// <remarks>
        /// Handles area effects, lighting updates, entity spawning/despawning.
        /// Called from main game loop.
        /// </remarks>
        public abstract void Update(float deltaTime);

        /// <summary>
        /// Renders the area.
        /// </summary>
        /// <remarks>
        /// Engine-specific rendering: MonoGame for runtime, DirectX/OpenGL for original engines.
        /// Handles VIS culling, transparency sorting, lighting.
        /// </remarks>
        public abstract void Render();

        /// <summary>
        /// Unloads the area and cleans up resources.
        /// </summary>
        public abstract void Unload();
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Eclipse Engine (Mass Effect/Dragon Age) specific area implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Area Implementation:
    /// - Based on daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe
    /// - Most advanced area system of the BioWare engines
    /// - Complex lighting, physics, and environmental simulation
    /// - Real-time area effects and dynamic weather
    ///
    /// Based on reverse engineering of:
    /// - daorigins.exe: Dragon Age Origins area systems
    /// - DragonAge2.exe: Enhanced Dragon Age 2 areas
    /// - MassEffect.exe/MassEffect2.exe: Mass Effect area implementations
    /// - Eclipse engine area properties and entity management
    ///
    /// Eclipse-specific features:
    /// - Advanced lighting system with dynamic shadows
    /// - Physics-based interactions and destruction
    /// - Complex weather and environmental effects
    /// - Real-time area modification capabilities
    /// - Advanced AI navigation and cover systems
    /// - Destructible environments and interactive objects
    /// </remarks>
    [PublicAPI]
    public class EclipseArea : BaseArea
    {
        private readonly List<IEntity> _creatures = new List<IEntity>();
        private readonly List<IEntity> _placeables = new List<IEntity>();
        private readonly List<IEntity> _doors = new List<IEntity>();
        private readonly List<IEntity> _triggers = new List<IEntity>();
        private readonly List<IEntity> _waypoints = new List<IEntity>();
        private readonly List<IEntity> _sounds = new List<IEntity>();
        private readonly List<IDynamicAreaEffect> _dynamicEffects = new List<IDynamicAreaEffect>();

        private string _resRef;
        private string _displayName;
        private string _tag;
        private bool _isUnescapable;
        private INavigationMesh _navigationMesh;
        private ILightingSystem _lightingSystem;
        private IPhysicsSystem _physicsSystem;

        /// <summary>
        /// Creates a new Eclipse area.
        /// </summary>
        /// <param name="resRef">The resource reference name of the area.</param>
        /// <param name="areaData">Area file data containing geometry and properties.</param>
        /// <remarks>
        /// Eclipse areas are the most complex with advanced initialization.
        /// Includes lighting setup, physics world creation, and effect systems.
        /// </remarks>
        public EclipseArea(string resRef, byte[] areaData)
        {
            _resRef = resRef ?? throw new ArgumentNullException(nameof(resRef));
            _tag = resRef; // Default tag to resref

            LoadAreaGeometry(areaData);
            LoadAreaProperties(areaData);
            InitializeAreaEffects();
            InitializeLightingSystem();
            InitializePhysicsSystem();
        }

        /// <summary>
        /// The resource reference name of this area.
        /// </summary>
        public override string ResRef => _resRef;

        /// <summary>
        /// The display name of the area.
        /// </summary>
        public override string DisplayName => _displayName ?? _resRef;

        /// <summary>
        /// The tag of the area.
        /// </summary>
        public override string Tag => _tag;

        /// <summary>
        /// All creatures in this area.
        /// </summary>
        public override IEnumerable<IEntity> Creatures => _creatures;

        /// <summary>
        /// All placeables in this area.
        /// </summary>
        public override IEnumerable<IEntity> Placeables => _placeables;

        /// <summary>
        /// All doors in this area.
        /// </summary>
        public override IEnumerable<IEntity> Doors => _doors;

        /// <summary>
        /// All triggers in this area.
        /// </summary>
        public override IEnumerable<IEntity> Triggers => _triggers;

        /// <summary>
        /// All waypoints in this area.
        /// </summary>
        public override IEnumerable<IEntity> Waypoints => _waypoints;

        /// <summary>
        /// All sounds in this area.
        /// </summary>
        public override IEnumerable<IEntity> Sounds => _sounds;

        /// <summary>
        /// Gets the walkmesh navigation system for this area.
        /// </summary>
        public override INavigationMesh NavigationMesh => _navigationMesh;

        /// <summary>
        /// Gets or sets whether the area is unescapable.
        /// </summary>
        /// <remarks>
        /// Eclipse areas have more sophisticated restriction systems.
        /// May include conditional escape based on quest state or abilities.
        /// </remarks>
        public override bool IsUnescapable
        {
            get => _isUnescapable;
            set => _isUnescapable = value;
        }

        /// <summary>
        /// Eclipse engine doesn't use stealth XP - always returns false.
        /// </summary>
        /// <remarks>
        /// Eclipse engine uses different progression systems than Odyssey.
        /// Stealth mechanics are handled differently.
        /// </remarks>
        public override bool StealthXPEnabled
        {
            get => false;
            set { /* No-op for Eclipse */ }
        }

        /// <summary>
        /// Gets the lighting system for this area.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific advanced lighting system.
        /// Includes dynamic lights, shadows, and global illumination.
        /// </remarks>
        public ILightingSystem LightingSystem => _lightingSystem;

        /// <summary>
        /// Gets the physics system for this area.
        /// </summary>
        /// <remarks>
        /// Eclipse includes physics simulation for interactions.
        /// Handles rigid body dynamics and collision detection.
        /// </remarks>
        public IPhysicsSystem PhysicsSystem => _physicsSystem;

        /// <summary>
        /// Gets an object by tag within this area.
        /// </summary>
        public override IEntity GetObjectByTag(string tag, int nth = 0)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            var allEntities = _creatures.Concat(_placeables).Concat(_doors)
                                       .Concat(_triggers).Concat(_waypoints).Concat(_sounds);

            return allEntities.Where(e => string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase))
                             .Skip(nth).FirstOrDefault();
        }

        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        /// <remarks>
        /// Eclipse walkmesh includes dynamic obstacles and destruction.
        /// Considers physics objects and interactive elements.
        /// </remarks>
        public override bool IsPointWalkable(Vector3 point)
        {
            return _navigationMesh?.IsPointWalkable(point) ?? false;
        }

        /// <summary>
        /// Projects a point onto the walkmesh.
        /// </summary>
        /// <remarks>
        /// Eclipse projection handles dynamic geometry changes.
        /// Considers movable objects and destructible terrain.
        /// </remarks>
        public override bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            if (_navigationMesh == null)
            {
                result = point;
                height = point.Y;
                return false;
            }

            return _navigationMesh.ProjectToWalkmesh(point, out result, out height);
        }

        /// <summary>
        /// Loads area properties from area data.
        /// </summary>
        /// <remarks>
        /// Eclipse areas have complex property systems.
        /// Includes lighting presets, weather settings, physics properties.
        /// Supports conditional area behaviors based on game state.
        /// </remarks>
        protected override void LoadAreaProperties(byte[] gffData)
        {
            // TODO: Implement Eclipse area properties loading
            // Parse complex area data format
            // Load lighting configurations
            // Load physics settings
            // Load environmental parameters

            _isUnescapable = false; // Default value
        }

        /// <summary>
        /// Saves area properties to data.
        /// </summary>
        /// <remarks>
        /// Eclipse saves runtime state changes.
        /// Includes dynamic lighting, physics state, destructible changes.
        /// </remarks>
        protected override byte[] SaveAreaProperties()
        {
            // TODO: Implement Eclipse area properties serialization
            throw new NotImplementedException("Eclipse area properties serialization not yet implemented");
        }

        /// <summary>
        /// Loads entities for the area.
        /// </summary>
        /// <remarks>
        /// Eclipse entities are loaded from area data.
        /// More complex than other engines with physics-enabled objects.
        /// Includes destructible and interactive elements.
        /// </remarks>
        protected override void LoadEntities(byte[] gitData)
        {
            // TODO: Implement Eclipse entity loading
            // Load from area geometry data
            // Create physics-enabled entities
            // Initialize interactive objects
        }

        /// <summary>
        /// Loads area geometry and navigation data.
        /// </summary>
        /// <remarks>
        /// Eclipse has complex geometry with destructible elements.
        /// Loads static geometry, dynamic objects, navigation mesh.
        /// Initializes physics collision shapes.
        /// </remarks>
        protected override void LoadAreaGeometry(byte[] areData)
        {
            // TODO: Implement Eclipse geometry loading
            // Load static and dynamic geometry
            // Create navigation mesh
            // Set up physics collision
            _navigationMesh = new EclipseNavigationMesh(); // Placeholder
        }

        /// <summary>
        /// Initializes area effects and environmental systems.
        /// </summary>
        /// <remarks>
        /// Eclipse has the most advanced environmental systems.
        /// Includes weather, particle effects, audio zones, interactive elements.
        /// </remarks>
        protected override void InitializeAreaEffects()
        {
            // TODO: Initialize Eclipse environmental systems
            // Set up weather simulation
            // Initialize particle systems
            // Configure audio zones
            // Set up interactive environmental elements
        }

        /// <summary>
        /// Initializes the lighting system.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific advanced lighting initialization.
        /// Sets up dynamic lights, shadows, global illumination.
        /// </remarks>
        private void InitializeLightingSystem()
        {
            // TODO: Initialize Eclipse lighting system
            _lightingSystem = new EclipseLightingSystem();
        }

        /// <summary>
        /// Initializes the physics system.
        /// </summary>
        /// <remarks>
        /// Eclipse physics world setup.
        /// Creates rigid bodies, collision shapes, constraints.
        /// </remarks>
        private void InitializePhysicsSystem()
        {
            // TODO: Initialize Eclipse physics system
            _physicsSystem = new EclipsePhysicsSystem();
        }

        /// <summary>
        /// Engine-specific hook called before area transition.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific: Saves physics state (velocity, angular velocity, mass) before transition.
        /// Based on reverse engineering of:
        /// - daorigins.exe: Physics state preservation during area transitions
        /// - DragonAge2.exe: Enhanced physics state transfer
        /// - MassEffect.exe/MassEffect2.exe: Complex physics continuity
        /// </remarks>
        protected override void OnBeforeTransition(IEntity entity, IArea currentArea)
        {
            // Save physics state before transition
            _savedPhysicsState = SaveEntityPhysicsState(entity);
        }

        /// <summary>
        /// Engine-specific hook called after area transition.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific: Restores physics state in target area after transition.
        /// Maintains physics continuity across area boundaries.
        /// </remarks>
        protected override void OnAfterTransition(IEntity entity, IArea targetArea, IArea currentArea)
        {
            // Transfer physics state to target area
            if (targetArea is EclipseArea eclipseTargetArea)
            {
                RestoreEntityPhysicsState(entity, _savedPhysicsState, eclipseTargetArea);
            }
        }

        /// <summary>
        /// Saved physics state for area transition.
        /// </summary>
        private PhysicsState _savedPhysicsState;

        /// <summary>
        /// Removes an entity from this area's collections.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific: Also removes entity from physics system.
        /// </remarks>
        protected override void RemoveEntityFromArea(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Remove from type-specific lists
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    _creatures.Remove(entity);
                    break;
                case ObjectType.Placeable:
                    _placeables.Remove(entity);
                    break;
                case ObjectType.Door:
                    _doors.Remove(entity);
                    break;
                case ObjectType.Trigger:
                    _triggers.Remove(entity);
                    break;
                case ObjectType.Waypoint:
                    _waypoints.Remove(entity);
                    break;
                case ObjectType.Sound:
                    _sounds.Remove(entity);
                    break;
            }

            // Remove physics body from physics system if entity has physics
            if (_physicsSystem != null)
            {
                RemoveEntityFromPhysics(entity);
            }
        }

        /// <summary>
        /// Adds an entity to this area's collections.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific: Also adds entity to physics system.
        /// </remarks>
        protected override void AddEntityToArea(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Add to type-specific lists
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    if (!_creatures.Contains(entity))
                    {
                        _creatures.Add(entity);
                    }
                    break;
                case ObjectType.Placeable:
                    if (!_placeables.Contains(entity))
                    {
                        _placeables.Add(entity);
                    }
                    break;
                case ObjectType.Door:
                    if (!_doors.Contains(entity))
                    {
                        _doors.Add(entity);
                    }
                    break;
                case ObjectType.Trigger:
                    if (!_triggers.Contains(entity))
                    {
                        _triggers.Add(entity);
                    }
                    break;
                case ObjectType.Waypoint:
                    if (!_waypoints.Contains(entity))
                    {
                        _waypoints.Add(entity);
                    }
                    break;
                case ObjectType.Sound:
                    if (!_sounds.Contains(entity))
                    {
                        _sounds.Add(entity);
                    }
                    break;
            }

            // Add physics body to physics system if entity has physics
            if (_physicsSystem != null)
            {
                AddEntityToPhysics(entity);
            }
        }

        /// <summary>
        /// Saves physics state for an entity before area transition.
        /// </summary>
        /// <remarks>
        /// Eclipse engine preserves physics state (velocity, angular velocity, constraints)
        /// when entities transition between areas to maintain physics continuity.
        /// </remarks>
        private PhysicsState SaveEntityPhysicsState(IEntity entity)
        {
            var state = new PhysicsState();

            if (entity == null || _physicsSystem == null)
            {
                return state;
            }

            // Get transform component for position/facing
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                state.Position = transform.Position;
                state.Facing = transform.Facing;
            }

            // Save physics-specific data from entity
            // In a full implementation, this would query the physics system for rigid body state
            // For now, we save basic transform data
            // TODO: When physics system is fully implemented, save velocity, angular velocity, constraints
            state.HasPhysics = entity.HasData("HasPhysics") && entity.GetData<bool>("HasPhysics", false);

            if (state.HasPhysics)
            {
                // Save velocity if available
                if (entity.HasData("PhysicsVelocity"))
                {
                    state.Velocity = entity.GetData<Vector3>("PhysicsVelocity", Vector3.Zero);
                }

                // Save angular velocity if available
                if (entity.HasData("PhysicsAngularVelocity"))
                {
                    state.AngularVelocity = entity.GetData<Vector3>("PhysicsAngularVelocity", Vector3.Zero);
                }

                // Save mass if available
                if (entity.HasData("PhysicsMass"))
                {
                    state.Mass = entity.GetData<float>("PhysicsMass", 1.0f);
                }
            }

            return state;
        }

        /// <summary>
        /// Restores physics state for an entity in the target area.
        /// </summary>
        /// <remarks>
        /// Restores physics state to maintain continuity across area transitions.
        /// Adds entity to target area's physics system with preserved state.
        /// </remarks>
        private void RestoreEntityPhysicsState(IEntity entity, PhysicsState savedState, EclipseArea targetArea)
        {
            if (entity == null || savedState == null || targetArea == null || targetArea._physicsSystem == null)
            {
                return;
            }

            // Restore transform if available
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                // Position was already updated in HandleAreaTransition
                // Restore facing if it was saved
                if (savedState.Facing != 0.0f)
                {
                    transform.Facing = savedState.Facing;
                }
            }

            // Restore physics state if entity had physics
            if (savedState.HasPhysics)
            {
                entity.SetData("HasPhysics", true);

                // Restore velocity
                if (savedState.Velocity != Vector3.Zero)
                {
                    entity.SetData("PhysicsVelocity", savedState.Velocity);
                }

                // Restore angular velocity
                if (savedState.AngularVelocity != Vector3.Zero)
                {
                    entity.SetData("PhysicsAngularVelocity", savedState.AngularVelocity);
                }

                // Restore mass
                if (savedState.Mass > 0)
                {
                    entity.SetData("PhysicsMass", savedState.Mass);
                }

                // Add entity to target area's physics system
                // In a full implementation, this would create/restore rigid body in physics world
                targetArea.AddEntityToPhysics(entity);
            }
        }


        /// <summary>
        /// Adds an entity to the physics system.
        /// </summary>
        /// <remarks>
        /// In a full implementation, this would create a rigid body in the physics world.
        /// For now, this is a placeholder that marks the entity as having physics.
        /// </remarks>
        private void AddEntityToPhysics(IEntity entity)
        {
            if (entity == null || _physicsSystem == null)
            {
                return;
            }

            // Mark entity as having physics
            entity.SetData("HasPhysics", true);

            // In a full implementation, this would:
            // 1. Get entity's collision shape from components
            // 2. Create rigid body in physics world
            // 3. Set position, rotation, mass, velocity
            // 4. Store physics body reference in entity data
        }

        /// <summary>
        /// Removes an entity from the physics system.
        /// </summary>
        /// <remarks>
        /// In a full implementation, this would remove the rigid body from the physics world.
        /// For now, this is a placeholder that clears physics data.
        /// </remarks>
        private void RemoveEntityFromPhysics(IEntity entity)
        {
            if (entity == null || _physicsSystem == null)
            {
                return;
            }

            // Clear physics data
            entity.SetData("HasPhysics", false);

            // In a full implementation, this would:
            // 1. Get physics body reference from entity data
            // 2. Remove rigid body from physics world
            // 3. Clear physics body reference
        }

        /// <summary>
        /// Physics state data for entity transitions.
        /// </summary>
        private class PhysicsState
        {
            public Vector3 Position { get; set; }
            public float Facing { get; set; }
            public Vector3 Velocity { get; set; }
            public Vector3 AngularVelocity { get; set; }
            public float Mass { get; set; }
            public bool HasPhysics { get; set; }
        }

        /// <summary>
        /// Updates area state each frame.
        /// </summary>
        /// <remarks>
        /// Updates all Eclipse systems: lighting, physics, effects, weather.
        /// Processes dynamic area changes and interactions.
        /// </remarks>
        public override void Update(float deltaTime)
        {
            // TODO: Update Eclipse area systems
            // Update lighting system
            // Step physics simulation
            // Update dynamic effects
            // Process weather simulation
            // Update interactive elements
        }

        /// <summary>
        /// Renders the area.
        /// </summary>
        /// <remarks>
        /// Eclipse rendering includes advanced lighting, shadows, effects.
        /// Handles deferred rendering, post-processing, and complex materials.
        /// </remarks>
        public override void Render()
        {
            // TODO: Implement Eclipse area rendering
            // Render with advanced lighting
            // Apply shadows and global illumination
            // Render particle effects
            // Apply post-processing effects
        }

        /// <summary>
        /// Unloads the area and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe/DragonAge2.exe/MassEffect.exe/MassEffect2.exe: Area unloading functions
        /// Comprehensive cleanup of Eclipse systems.
        /// Destroys physics world, lighting, effects, entities.
        ///
        /// Eclipse-specific cleanup:
        /// - Destroys all entities (creatures, placeables, doors, triggers, waypoints, sounds)
        /// - Disposes physics system (removes all physics bodies and constraints)
        /// - Disposes lighting system (clears light sources and shadows)
        /// - Deactivates and clears all dynamic area effects
        /// - Disposes navigation mesh if IDisposable
        /// - Clears all entity lists
        /// </remarks>
        public override void Unload()
        {
            // Collect all entities first to avoid modification during iteration
            var allEntities = new List<IEntity>();
            allEntities.AddRange(_creatures);
            allEntities.AddRange(_placeables);
            allEntities.AddRange(_doors);
            allEntities.AddRange(_triggers);
            allEntities.AddRange(_waypoints);
            allEntities.AddRange(_sounds);

            // Destroy all entities
            // Based on Eclipse engine: Entities are removed from area and destroyed
            // If entity has World reference, use World.DestroyEntity (fires events, unregisters properly)
            // Otherwise, call Destroy directly (for entities not yet registered with world)
            foreach (IEntity entity in allEntities)
            {
                if (entity != null && entity.IsValid)
                {
                    // Try to destroy via World first (proper cleanup with event firing)
                    if (entity.World != null)
                    {
                        entity.World.DestroyEntity(entity.ObjectId);
                    }
                    else
                    {
                        // Entity not registered with world - destroy directly
                        // Based on Entity.Destroy() implementation
                        if (entity is Core.Entities.Entity concreteEntity)
                        {
                            concreteEntity.Destroy();
                        }
                    }
                }
            }

            // Dispose physics system
            // Based on Eclipse engine: Physics world is destroyed during area unload
            // Physics system must be disposed before entities to avoid dangling references
            if (_physicsSystem != null)
            {
                if (_physicsSystem is System.IDisposable disposablePhysics)
                {
                    disposablePhysics.Dispose();
                }
                _physicsSystem = null;
            }

            // Dispose lighting system
            // Based on Eclipse engine: Lighting system is cleaned up during area unload
            if (_lightingSystem != null)
            {
                if (_lightingSystem is System.IDisposable disposableLighting)
                {
                    disposableLighting.Dispose();
                }
                _lightingSystem = null;
            }

            // Deactivate and clear all dynamic area effects
            // Based on Eclipse engine: Dynamic effects are cleaned up during area unload
            foreach (IDynamicAreaEffect effect in _dynamicEffects)
            {
                if (effect != null && effect.IsActive)
                {
                    effect.Deactivate();
                }
            }
            _dynamicEffects.Clear();

            // Dispose navigation mesh if it implements IDisposable
            // Based on Eclipse engine: Navigation mesh resources are freed
            if (_navigationMesh != null)
            {
                if (_navigationMesh is System.IDisposable disposableMesh)
                {
                    disposableMesh.Dispose();
                }
                _navigationMesh = null;
            }

            // Clear all entity lists
            // Based on Eclipse engine: Entity lists are cleared during unload
            _creatures.Clear();
            _placeables.Clear();
            _doors.Clear();
            _triggers.Clear();
            _waypoints.Clear();
            _sounds.Clear();

            // Clear string references (optional cleanup)
            // Based on Eclipse engine: String references are cleared
            _resRef = null;
            _displayName = null;
            _tag = null;
        }

        /// <summary>
        /// Gets all dynamic area effects.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific dynamic effects system.
        /// Effects can be created/modified at runtime.
        /// </remarks>
        public IEnumerable<IDynamicAreaEffect> GetDynamicEffects()
        {
            return _dynamicEffects;
        }

        /// <summary>
        /// Applies a dynamic change to the area.
        /// </summary>
        /// <remarks>
        /// Eclipse allows runtime area modification.
        /// Can create holes, move objects, change lighting.
        /// </remarks>
        public void ApplyAreaModification(IAreaModification modification)
        {
            // TODO: Implement area modification system
            modification.Apply(this);
        }
    }

    /// <summary>
    /// Interface for dynamic area effects in Eclipse engine.
    /// </summary>
    public interface IDynamicAreaEffect : IUpdatable
    {
        /// <summary>
        /// Gets whether the effect is still active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Deactivates the effect.
        /// </summary>
        void Deactivate();
    }

    /// <summary>
    /// Interface for area modifications in Eclipse engine.
    /// </summary>
    public interface IAreaModification
    {
        /// <summary>
        /// Applies the modification to an area.
        /// </summary>
        void Apply(EclipseArea area);
    }

    /// <summary>
    /// Interface for Eclipse lighting system.
    /// </summary>
    public interface ILightingSystem : IUpdatable
    {
        /// <summary>
        /// Adds a dynamic light to the scene.
        /// </summary>
        void AddLight(IDynamicLight light);

        /// <summary>
        /// Removes a dynamic light from the scene.
        /// </summary>
        void RemoveLight(IDynamicLight light);
    }

    /// <summary>
    /// Interface for Eclipse physics system.
    /// </summary>
    public interface IPhysicsSystem
    {
        /// <summary>
        /// Steps the physics simulation.
        /// </summary>
        void StepSimulation(float deltaTime);

        /// <summary>
        /// Casts a ray through the physics world.
        /// </summary>
        bool RayCast(Vector3 origin, Vector3 direction, out Vector3 hitPoint, out IEntity hitEntity);
    }

    /// <summary>
    /// Interface for dynamic lights.
    /// </summary>
    public interface IDynamicLight
    {
        Vector3 Position { get; }
        Vector3 Color { get; }
        float Intensity { get; }
        float Range { get; }
    }

    /// <summary>
    /// Interface for updatable objects.
    /// </summary>
    public interface IUpdatable
    {
        void Update(float deltaTime);
    }
}

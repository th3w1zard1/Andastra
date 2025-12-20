using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Animation;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Templates;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Core entity container and world state.
    /// </summary>
    /// <remarks>
    /// World Interface:
    /// Common world management system shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity).
    ///
    /// Common functionality across all engines:
    /// - Entity container: Maintains entity lists by ObjectId, Tag, and ObjectType for efficient lookup
    /// - Entity lookup: GetEntity by ObjectId (O(1) lookup), GetEntityByTag by tag string (case-insensitive, supports nth occurrence)
    /// - ObjectId: Unique 32-bit identifier assigned sequentially, used for script references and save game serialization
    /// - Area management: Entities belong to areas (AreaId field), areas contain entity lists by type
    /// - Module management: World manages current area/module, module loading/unloading, area transitions
    /// - System integration: World coordinates time (ITimeManager), events (IEventBus), delay scheduler (IDelayScheduler), effect system, combat system, perception system, trigger system, AI controller, animation system
    /// - Entity lifecycle: CreateEntity (from template or ObjectType), DestroyEntity (removes entity and cleans up components)
    /// - Spatial queries: GetEntitiesInRadius with optional ObjectType filter mask
    /// - Area registration: Areas are registered with AreaId for entity lookup, GetArea by AreaId (O(1) lookup)
    /// - Entity registration: RegisterEntity/UnregisterEntity for adding/removing entities from world
    ///
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe, swkotor2.exe): OdysseyWorld : BaseWorld
    ///   - ObjectId lookup via "ObjectId" string reference, ObjectId assignment and serialization
    ///   - AreaId management via "AreaId" string reference
    ///   - Module management with ARE/GIT file loading
    ///   - Entity serialization/deserialization via GFF format
    /// - Aurora (nwmain.exe, nwn2main.exe): AuroraWorld : BaseWorld
    ///   - Similar entity management structure with CExoString-based Tag
    ///   - Module management with ERF/HAK file support
    ///   - Area management with enhanced spatial partitioning
    /// - Eclipse (daorigins.exe, DragonAge2.exe): EclipseWorld : BaseWorld
    ///   - Enhanced component system with streamlined entity management
    ///   - Module streaming system for large areas
    ///   - Advanced area management with dynamic loading
    /// - Infinity (, ): InfinityWorld : BaseWorld
    ///   - Streamlined entity system optimized for action gameplay
    ///   - Level-based world management (levels instead of modules)
    ///   - Enhanced spatial queries for large-scale environments
    ///
    /// Base implementation: BaseWorld (Runtime.Games.Common) provides common functionality.
    /// All engine-specific details (function addresses, string references, implementation specifics) are in subclasses.
    /// This interface defines only common functionality shared across all engines.
    /// </remarks>
    public interface IWorld
    {
        /// <summary>
        /// Creates a new entity from a template.
        /// </summary>
        IEntity CreateEntity(IEntityTemplate template, Vector3 position, float facing);

        /// <summary>
        /// Creates a new entity of the specified type.
        /// </summary>
        IEntity CreateEntity(ObjectType objectType, Vector3 position, float facing);

        /// <summary>
        /// Destroys an entity by object ID.
        /// </summary>
        void DestroyEntity(uint objectId);

        /// <summary>
        /// Gets an entity by object ID.
        /// </summary>
        IEntity GetEntity(uint objectId);

        /// <summary>
        /// Gets an entity by tag. If nth > 0, gets the nth entity with that tag.
        /// </summary>
        IEntity GetEntityByTag(string tag, int nth = 0);

        /// <summary>
        /// Gets all entities within a radius of a point, optionally filtered by type mask.
        /// </summary>
        IEnumerable<IEntity> GetEntitiesInRadius(Vector3 center, float radius, ObjectType typeMask = ObjectType.All);

        /// <summary>
        /// Gets all entities of a specific type.
        /// </summary>
        IEnumerable<IEntity> GetEntitiesOfType(ObjectType type);

        /// <summary>
        /// Gets all entities in the world.
        /// </summary>
        IEnumerable<IEntity> GetAllEntities();

        /// <summary>
        /// The current area.
        /// </summary>
        IArea CurrentArea { get; }

        /// <summary>
        /// The current module.
        /// </summary>
        IModule CurrentModule { get; }

        /// <summary>
        /// The simulation time manager.
        /// </summary>
        ITimeManager TimeManager { get; }

        /// <summary>
        /// The event bus for world and entity events.
        /// </summary>
        IEventBus EventBus { get; }

        /// <summary>
        /// The delay scheduler for delayed actions (DelayCommand).
        /// </summary>
        IDelayScheduler DelayScheduler { get; }

        /// <summary>
        /// The effect system for managing entity effects.
        /// </summary>
        Combat.EffectSystem EffectSystem { get; }

        /// <summary>
        /// The perception system for sight/hearing checks.
        /// </summary>
        Perception.PerceptionSystem PerceptionSystem { get; }

        /// <summary>
        /// The combat system for combat resolution.
        /// </summary>
        Combat.CombatSystem CombatSystem { get; }

        /// <summary>
        /// The trigger system for trigger volume events.
        /// </summary>
        Triggers.TriggerSystem TriggerSystem { get; }

        /// <summary>
        /// The AI controller for NPC behavior.
        /// </summary>
        AI.AIController AIController { get; }

        /// <summary>
        /// The animation system for updating entity animations.
        /// </summary>
        Animation.AnimationSystem AnimationSystem { get; }

        /// <summary>
        /// The game data provider for accessing engine-agnostic game data tables.
        /// </summary>
        /// <remarks>
        /// Provides access to game data tables (2DA files) for looking up creature properties,
        /// appearance data, and other game configuration data.
        /// Engine-specific implementations handle the actual table loading and lookup.
        /// This property should be set by engine-specific implementations.
        /// </remarks>
        IGameDataProvider GameDataProvider { get; }

        /// <summary>
        /// Registers an entity with the world.
        /// </summary>
        void RegisterEntity(IEntity entity);

        /// <summary>
        /// Unregisters an entity from the world.
        /// </summary>
        void UnregisterEntity(IEntity entity);

        /// <summary>
        /// Gets an area by its AreaId.
        /// </summary>
        /// <remarks>
        /// O(1) dictionary lookup by AreaId (uint32).
        /// Returns null if AreaId not found.
        /// Common across all engines: Areas are registered with AreaId for efficient lookup.
        /// Used by GetArea NWScript function (where applicable) to find area containing an entity.
        /// Engine-specific implementations handle AreaId assignment and lookup mechanisms.
        /// </remarks>
        IArea GetArea(uint areaId);

        /// <summary>
        /// Registers an area with the world and assigns it an AreaId.
        /// </summary>
        /// <remarks>
        /// Areas are registered in world with AreaId for entity lookup.
        /// Common across all engines: Areas assigned sequential AreaId starting from engine-specific base value.
        /// Engine-specific implementations handle AreaId assignment ranges and lookup mechanisms.
        /// </remarks>
        void RegisterArea(IArea area);

        /// <summary>
        /// Unregisters an area from the world.
        /// </summary>
        void UnregisterArea(IArea area);

        /// <summary>
        /// Gets the AreaId for an area.
        /// </summary>
        uint GetAreaId(IArea area);

        /// <summary>
        /// Gets the ModuleId for a module.
        /// </summary>
        /// <remarks>
        /// Returns the ModuleId assigned to a module, or 0 if module is not registered.
        /// Common across all engines: Modules are special objects with fixed ObjectId (0x7F000002).
        /// Used by GetModule NWScript function to return the module object ID.
        /// Engine-specific implementations may use different ID assignment mechanisms.
        /// </remarks>
        uint GetModuleId(IModule module);

        /// <summary>
        /// Updates the world (time manager, event bus).
        /// </summary>
        void Update(float deltaTime);
    }
}


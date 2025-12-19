using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Games.Eclipse.Components;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Common.Components;
using Andastra.Runtime.Engines.Odyssey.Components;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Eclipse Engine (Dragon Age Origins, Dragon Age 2) specific entity implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Entity Implementation:
    /// - Based on daorigins.exe (Dragon Age Origins) and DragonAge2.exe (Dragon Age 2) entity systems
    /// - Implements ObjectId, Tag, ObjectType structure
    /// - Enhanced component-based architecture for modular functionality
    /// - Script hooks for events and behaviors
    ///
    /// Based on reverse engineering of:
    /// - daorigins.exe: Entity creation and management
    /// - DragonAge2.exe: Enhanced entity system
    /// - ObjectId: Located via string reference "ObjectId" @ 0x00af4e74 (daorigins.exe), "ObjectId" @ 0x00bf1a3c (DragonAge2.exe)
    /// - Tag: Located via "ItemTag" @ 0x00ae96dc (daorigins.exe), "TagName" @ 0x00b14a00 (daorigins.exe)
    /// - Entity structure: ObjectId (uint32), Tag (string), ObjectType (enum)
    /// - Component system: Enhanced transform, stats, inventory, script hooks, etc.
    ///
    /// Entity lifecycle:
    /// - Created from template files or script instantiation
    /// - Assigned sequential ObjectId for uniqueness
    /// - Components attached based on ObjectType
    /// - Registered with area and world systems
    /// - Updated each frame, destroyed when no longer needed
    ///
    /// Eclipse-specific details:
    /// - Enhanced component system compared to Odyssey/Aurora
    /// - Different property calculations and upgrade mechanics
    /// - Uses talents/abilities system instead of feats
    /// - Different save/load format than Odyssey/Aurora
    /// </remarks>
    [PublicAPI]
    public class EclipseEntity : BaseEntity
    {
        private uint _objectId;
        private string _tag;
        private readonly ObjectType _objectType;
        private IWorld _world;
        private bool _isValid = true;
        private uint _areaId;

        /// <summary>
        /// Creates a new Eclipse entity.
        /// </summary>
        /// <param name="objectId">Unique object identifier.</param>
        /// <param name="objectType">The type of object this entity represents.</param>
        /// <param name="tag">Tag string for script lookups.</param>
        /// <remarks>
        /// Based on entity creation in daorigins.exe and DragonAge2.exe.
        /// ObjectId must be unique within the game session.
        /// ObjectType determines available components and behaviors.
        /// </remarks>
        public EclipseEntity(uint objectId, ObjectType objectType, string tag = null)
        {
            _objectId = objectId;
            _objectType = objectType;
            _tag = tag ?? string.Empty;

            Initialize();
        }

        /// <summary>
        /// Unique object ID for this entity.
        /// </summary>
        /// <remarks>
        /// Based on ObjectId field in Eclipse entity structure.
        /// Located via string reference "ObjectId" @ 0x00af4e74 in daorigins.exe.
        /// Located via string reference "ObjectId" @ 0x00bf1a3c in DragonAge2.exe.
        /// Assigned sequentially and must be unique across all entities.
        /// Used for script references and save game serialization.
        /// </remarks>
        public override uint ObjectId => _objectId;

        /// <summary>
        /// Tag string for script lookups.
        /// </summary>
        /// <remarks>
        /// Script-accessible identifier for GetObjectByTag functions.
        /// Based on "ItemTag" @ 0x00ae96dc and "TagName" @ 0x00b14a00 in daorigins.exe.
        /// Can be changed at runtime for dynamic lookups.
        /// </remarks>
        public override string Tag
        {
            get => _tag;
            set => _tag = value ?? string.Empty;
        }

        /// <summary>
        /// The type of this object.
        /// </summary>
        /// <remarks>
        /// Determines available components and behaviors.
        /// Cannot be changed after entity creation.
        /// </remarks>
        public override ObjectType ObjectType => _objectType;

        /// <summary>
        /// Whether this entity is valid and not destroyed.
        /// </summary>
        /// <remarks>
        /// Entity validity prevents use-after-free issues.
        /// Becomes invalid when Destroy() is called.
        /// </remarks>
        public override bool IsValid => _isValid;

        /// <summary>
        /// The world this entity belongs to.
        /// </summary>
        /// <remarks>
        /// Reference to containing world for cross-entity operations.
        /// Set when entity is added to an area.
        /// </remarks>
        public override IWorld World
        {
            get => _world;
            set => _world = value;
        }

        /// <summary>
        /// Gets or sets the area ID this entity belongs to.
        /// </summary>
        /// <remarks>
        /// AreaId identifies which area the entity is located in.
        /// Set when entity is registered to an area in the world.
        /// </remarks>
        public override uint AreaId
        {
            get => _areaId;
            set => _areaId = value;
        }

        /// <summary>
        /// Initializes the entity after creation.
        /// </summary>
        /// <remarks>
        /// Attaches default components based on ObjectType.
        /// Registers with necessary systems.
        /// Called automatically in constructor.
        /// </remarks>
        protected override void Initialize()
        {
            // Attach components based on object type
            switch (_objectType)
            {
                case ObjectType.Creature:
                    AttachCreatureComponents();
                    break;
                case ObjectType.Door:
                    AttachDoorComponents();
                    break;
                case ObjectType.Placeable:
                    AttachPlaceableComponents();
                    break;
                case ObjectType.Trigger:
                    AttachTriggerComponents();
                    break;
                case ObjectType.Waypoint:
                    AttachWaypointComponents();
                    break;
                case ObjectType.Sound:
                    AttachSoundComponents();
                    break;
            }

            // All entities get transform and script hooks
            AttachCommonComponents();
        }

        /// <summary>
        /// Attaches components common to all entity types.
        /// </summary>
        /// <remarks>
        /// Common components attached to all entities:
        /// - TransformComponent: Position, orientation, scale for all entities
        /// - ScriptHooksComponent: Script event hooks and local variables for all entities
        ///
        /// Based on daorigins.exe and DragonAge2.exe: All entities have transform and script hooks capability.
        /// Transform data is loaded from area files and templates.
        /// Script hooks are loaded from entity templates and can be set/modified at runtime.
        /// Event system uses UnrealScript-based event dispatching through BioEventDispatcher interface.
        /// </remarks>
        private void AttachCommonComponents()
        {
            // Attach transform component for all entities
            // Based on daorigins.exe and DragonAge2.exe: All entities have transform data (position, orientation, scale)
            // Transform data is loaded from area files and templates
            // XPosition @ 0x00af4f68, YPosition @ 0x00af4f5c, ZPosition @ 0x00af4f50 (daorigins.exe)
            // XOrientation @ 0x00af4f40, YOrientation @ 0x00af4f30, ZOrientation @ 0x00af4f20 (daorigins.exe)
            if (!HasComponent<ITransformComponent>())
            {
                var transformComponent = new EclipseTransformComponent();
                AddComponent<ITransformComponent>(transformComponent);
            }

            // Attach script hooks component for all entities
            // Based on daorigins.exe and DragonAge2.exe: All entities support script hooks
            // Script hooks are loaded from entity templates and can be set/modified at runtime
            // Event system: "EventListeners" @ 0x00ae8194 (daorigins.exe), 0x00bf543c (DragonAge2.exe)
            // Event scripts: "EventScripts" @ 0x00ae81bc (daorigins.exe), 0x00bf5464 (DragonAge2.exe)
            // Command processing: "COMMAND_SIGNALEVENT" @ 0x00af4180 (daorigins.exe) processes event commands
            // UnrealScript event dispatcher: Uses BioEventDispatcher interface for event routing
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            if (!HasComponent<IScriptHooksComponent>())
            {
                var scriptHooksComponent = new EclipseScriptHooksComponent();
                AddComponent<IScriptHooksComponent>(scriptHooksComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to creatures.
        /// </summary>
        /// <remarks>
        /// Creatures have stats, inventory, combat capabilities, etc.
        /// Based on creature component structure in daorigins.exe and DragonAge2.exe.
        /// Uses talents/abilities system instead of feats.
        /// 
        /// Component attachment pattern:
        /// - Based on daorigins.exe and DragonAge2.exe: Creature components are attached during entity creation from templates
        /// - Component provides: Stats (HP, abilities, skills), Inventory (equipped items and inventory bag), Faction (hostility relationships),
        ///   Animation (model animations), Renderable (3D model rendering), Perception (sight/hearing detection)
        /// - Eclipse-specific: Uses talents/abilities system instead of feats (different from Odyssey/Aurora)
        /// - Component initialization: Properties loaded from entity template files and can be modified at runtime
        /// 
        /// Cross-engine analysis:
        /// - Odyssey (swkotor.exe, swkotor2.exe): Uses CreatureComponent, StatsComponent, InventoryComponent, QuickSlotComponent, OdysseyFactionComponent
        ///   - ComponentInitializer @ Odyssey/Systems/ComponentInitializer.cs attaches these components
        /// - Aurora (nwmain.exe, nwn2main.exe): Similar component structure with AuroraCreatureComponent, StatsComponent, InventoryComponent, AuroraFactionComponent
        /// - Eclipse (daorigins.exe, DragonAge2.exe): Enhanced component system with StatsComponent, InventoryComponent, EclipseFactionComponent, EclipseAnimationComponent
        ///   - Eclipse-specific: Talents/abilities system, different property calculations, enhanced component interactions
        /// - Infinity (MassEffect.exe, MassEffect2.exe): Streamlined component system (to be reverse engineered)
        /// 
        /// Note: Currently using Odyssey StatsComponent and InventoryComponent implementations as they implement the common interfaces.
        /// TODO: Create Eclipse-specific StatsComponent and InventoryComponent implementations if Eclipse-specific behavior is needed.
        /// </remarks>
        private void AttachCreatureComponents()
        {
            // Attach stats component for creatures
            // Based on daorigins.exe and DragonAge2.exe: Creatures have stats (HP, abilities, skills, saves)
            // Stats component provides: CurrentHP, MaxHP, Abilities (STR, DEX, CON, INT, WIS, CHA), Skills, Saves, BaseAttackBonus, ArmorClass
            // Eclipse-specific: Different ability score calculations and skill systems compared to Odyssey/Aurora
            // Stats are loaded from entity templates and can be modified at runtime
            if (!HasComponent<IStatsComponent>())
            {
                // TODO: Create EclipseStatsComponent if Eclipse-specific stats behavior is needed
                // For now, using Odyssey StatsComponent as it implements IStatsComponent interface
                // Eclipse may have different ability score calculations, skill systems, or HP regeneration mechanics
                var statsComponent = new StatsComponent();
                AddComponent<IStatsComponent>(statsComponent);
            }

            // Attach inventory component for creatures
            // Based on daorigins.exe and DragonAge2.exe: Creatures have inventory (equipped items and inventory bag)
            // Inventory component provides: Equipped items (weapon, armor, shield, etc.), Inventory bag (array of item slots)
            // Eclipse-specific: Different inventory slot system and equipment types compared to Odyssey/Aurora
            // Inventory is loaded from entity templates and can be modified at runtime
            if (!HasComponent<IInventoryComponent>())
            {
                // TODO: Create EclipseInventoryComponent if Eclipse-specific inventory behavior is needed
                // For now, using Odyssey InventoryComponent as it implements IInventoryComponent interface
                // Eclipse may have different inventory slot numbering, equipment types, or storage formats
                var inventoryComponent = new InventoryComponent(this);
                AddComponent<IInventoryComponent>(inventoryComponent);
            }

            // Attach faction component for creatures
            // Based on daorigins.exe and DragonAge2.exe: Creatures have faction relationships (hostility, friendliness)
            // Faction component provides: FactionId, IsHostile, IsFriendly checks
            // Eclipse-specific: Uses EclipseFactionManager for reputation-based hostility determination
            // Faction relationships: Similar to Odyssey/Aurora but Eclipse-specific implementation with reputation system
            if (!HasComponent<IFactionComponent>())
            {
                var factionComponent = new EclipseFactionComponent();
                factionComponent.Owner = this;
                // Set FactionID from entity data if available (loaded from entity template)
                if (GetData("FactionID") is int factionId)
                {
                    factionComponent.FactionId = factionId;
                }
                AddComponent<IFactionComponent>(factionComponent);
            }

            // Attach animation component for creatures
            // Based on daorigins.exe and DragonAge2.exe: Creatures have animation capabilities (model animations)
            // Animation component provides: PlayAnimation, StopAnimation, GetCurrentAnimation, AnimationState
            // Eclipse-specific: Uses EclipseAnimationComponent for Eclipse-specific animation system
            // Animations are loaded from model files and can be triggered by scripts or game systems
            if (!HasComponent<IAnimationComponent>())
            {
                var animationComponent = new EclipseAnimationComponent();
                AddComponent<IAnimationComponent>(animationComponent);
            }

            // Attach renderable component for creatures
            // Based on daorigins.exe and DragonAge2.exe: Creatures are renderable entities (3D models)
            // Renderable component provides: ModelResRef, AppearanceRow, TextureResRefs, ModelType
            // Eclipse-specific: Uses RenderableComponent for 3D model rendering
            // Renderable data is loaded from entity templates and appearance.2da table
            if (!HasComponent<IRenderableComponent>())
            {
                // TODO: Create EclipseRenderableComponent if Eclipse-specific rendering behavior is needed
                // For now, using Odyssey RenderableComponent as it implements IRenderableComponent interface
                // Eclipse may have different model formats, texture systems, or appearance data structures
                var renderableComponent = new RenderableComponent();
                AddComponent<IRenderableComponent>(renderableComponent);
            }

            // Attach perception component for creatures (optional, may be attached by other systems)
            // Based on daorigins.exe and DragonAge2.exe: Creatures have perception capabilities (sight, hearing)
            // Perception component provides: PerceptionRange, CanSee, CanHear, Perception checks
            // Eclipse-specific: Uses BasePerceptionComponent for perception system
            // Perception data is loaded from appearance.2da table (PERCEPTIONDIST, PerceptionRange columns)
            // Note: Perception component may be attached by PerceptionManager system, so we check before attaching
            if (!HasComponent<IPerceptionComponent>())
            {
                // Perception component will be attached by PerceptionManager if needed
                // We don't attach it here to avoid duplicate attachment
            }
        }

        /// <summary>
        /// Attaches components specific to doors.
        /// </summary>
        /// <remarks>
        /// Doors have open/close state, lock state, transition logic.
        /// Based on door component structure in daorigins.exe and DragonAge2.exe.
        /// - Note: Eclipse engines may not have traditional door systems like Odyssey/Aurora
        /// - If doors are supported, they would use Eclipse-specific file formats and systems
        /// - Original implementation: Needs reverse engineering from daorigins.exe and DragonAge2.exe
        /// - Door component attached during entity creation if door support exists
        /// </remarks>
        private void AttachDoorComponents()
        {
            // Attach door component if not already present
            // Based on Eclipse engine: Door component attachment (if doors are supported)
            // Note: Eclipse engines may not support traditional doors, but component exists for compatibility
            if (!HasComponent<IDoorComponent>())
            {
                var doorComponent = new EclipseDoorComponent();
                doorComponent.Owner = this;
                AddComponent<IDoorComponent>(doorComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to placeables.
        /// </summary>
        /// <remarks>
        /// Placeables have interaction state, inventory, use logic.
        /// Based on placeable component structure in daorigins.exe and DragonAge2.exe.
        /// - PlaceableList @ 0x00af5028 (daorigins.exe) - Placeable list in area data
        /// - CPlaceable @ 0x00b0d488 (daorigins.exe) - Placeable class name
        /// - CCPlaceable class (daorigins.exe, DragonAge2.exe) - Placeable class implementation
        /// - COMMAND_GETPLACEABLE* and COMMAND_SETPLACEABLE* functions (daorigins.exe) - Placeable property access
        /// - Eclipse uses UnrealScript message passing system instead of direct function calls
        /// - Placeables have appearance, useability, locks, inventory, HP, physics-based interactions
        /// - Script events: OnUsed, OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
        /// - Containers (HasInventory=true) can store items, open/close states
        /// - Lock system: KeyRequired flag, KeyName tag, LockDC difficulty class
        /// - Eclipse-specific: Physics-based placeables, state-based system, different trap system, treasure categories
        /// 
        /// Component attachment pattern:
        /// - Based on daorigins.exe: Placeable components are attached during entity creation from area templates
        /// - Component provides: IsUseable, HasInventory, IsStatic, IsOpen, IsLocked, LockDC, KeyTag, HitPoints, MaxHitPoints, Hardness, AnimationState, Conversation
        /// - Eclipse-specific properties: BaseType, Action, State, TreasureCategory, TreasureRank, PickLockLevel, AutoRemoveKey, PopupText
        /// - Component initialization: Properties loaded from area template files and can be modified at runtime
        /// </remarks>
        private void AttachPlaceableComponents()
        {
            // Attach placeable component if not already present
            // Based on daorigins.exe and DragonAge2.exe: Placeable component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            if (!HasComponent<IPlaceableComponent>())
            {
                var placeableComponent = new EclipsePlaceableComponent();
                placeableComponent.Owner = this;
                AddComponent<IPlaceableComponent>(placeableComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to triggers.
        /// </summary>
        /// <remarks>
        /// Triggers have enter/exit detection, script firing.
        /// Based on trigger component structure in daorigins.exe and DragonAge2.exe.
        /// </remarks>
        private void AttachTriggerComponents()
        {
            // TODO: Attach trigger-specific components
            // TriggerComponent with enter/exit detection
        }

        /// <summary>
        /// Attaches components specific to waypoints.
        /// </summary>
        /// <remarks>
        /// Waypoints have position data, pathfinding integration.
        /// Based on waypoint component structure in daorigins.exe and DragonAge2.exe.
        /// </remarks>
        private void AttachWaypointComponents()
        {
            if (!HasComponent<IWaypointComponent>())
            {
                var waypointComponent = new EclipseWaypointComponent();
                AddComponent<IWaypointComponent>(waypointComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to sounds.
        /// </summary>
        /// <remarks>
        /// Sounds have audio playback, spatial positioning.
        /// Based on sound component structure in daorigins.exe and DragonAge2.exe.
        /// </remarks>
        private void AttachSoundComponents()
        {
            // TODO: Attach sound-specific components
            // SoundComponent with audio playback capabilities
        }

        /// <summary>
        /// Updates the entity each frame.
        /// </summary>
        /// <remarks>
        /// Updates all attached components.
        /// Processes any pending script events.
        /// Handles component interactions.
        /// 
        /// Based on daorigins.exe and DragonAge2.exe: Entity update loop processes components in dependency order.
        /// Component update order:
        /// 1. TransformComponent (position, orientation updates)
        /// 2. ActionQueueComponent (action execution, may modify transform)
        /// 3. StatsComponent (HP regeneration, stat updates)
        /// 4. PerceptionComponent (perception checks, uses transform position)
        /// 5. Other components (in arbitrary order)
        /// 
        /// Component interactions:
        /// - Transform changes trigger perception updates
        /// - HP changes trigger death state updates
        /// - Action queue execution may modify transform
        /// - Inventory changes affect encumbrance and movement speed
        /// </remarks>
        public override void Update(float deltaTime)
        {
            if (!IsValid)
                return;

            // Update components in dependency order
            // 1. TransformComponent first (position/orientation)
            var transformComponent = GetComponent<ITransformComponent>();
            if (transformComponent is IUpdatableComponent updatableTransform)
            {
                updatableTransform.Update(deltaTime);
            }

            // 2. ActionQueueComponent (may modify transform through movement actions)
            var actionQueueComponent = GetComponent<IActionQueueComponent>();
            if (actionQueueComponent != null)
            {
                actionQueueComponent.Update(this, deltaTime);
            }

            // 3. StatsComponent (HP regeneration, stat updates)
            var statsComponent = GetComponent<IStatsComponent>();
            if (statsComponent is IUpdatableComponent updatableStats)
            {
                updatableStats.Update(deltaTime);
            }

            // 4. PerceptionComponent (uses transform position)
            var perceptionComponent = GetComponent<IPerceptionComponent>();
            if (perceptionComponent is IUpdatableComponent updatablePerception)
            {
                updatablePerception.Update(deltaTime);
            }

            // 5. Other components (in arbitrary order)
            foreach (var component in GetAllComponents())
            {
                // Skip already-updated components
                if (component == transformComponent || 
                    component == actionQueueComponent || 
                    component == statsComponent || 
                    component == perceptionComponent)
                {
                    continue;
                }

                if (component is IUpdatableComponent updatable)
                {
                    updatable.Update(deltaTime);
                }
            }

            // Handle component interactions after all components are updated
            HandleComponentInteractions(deltaTime);

            // Process script events and hooks
            // Script events are processed by the game loop, not here
            // But we could fire heartbeat events here if needed
        }

        /// <summary>
        /// Destroys the entity and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Removes from world and area systems.
        /// Cleans up all components and resources.
        /// Marks entity as invalid.
        /// </remarks>
        public override void Destroy()
        {
            if (!IsValid)
                return;

            _isValid = false;

            // Remove from world/area
            if (_world != null)
            {
                // TODO: Remove from world's entity collections
            }

            // Clean up components
            foreach (var component in GetAllComponents())
            {
                if (component is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            // Clear component references
            var componentTypes = GetAllComponents().Select(c => c.GetType()).ToArray();
            foreach (var componentType in componentTypes)
            {
                // Remove component by type - this is a bit hacky but works
                var method = GetType().GetMethod("RemoveComponent")?.MakeGenericMethod(componentType);
                method?.Invoke(this, null);
            }
        }

        /// <summary>
        /// Serializes entity data for save games.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse entity serialization functions in daorigins.exe and DragonAge2.exe.
        /// Serializes ObjectId, Tag, components, and custom data.
        /// Uses Eclipse-specific save format.
        ///
        /// Serialized data includes:
        /// - Basic entity properties (ObjectId, Tag, ObjectType, AreaId)
        /// - Transform component (position, facing, scale)
        /// - Stats component (HP, abilities, skills, saves)
        /// - Door component (open/locked state, HP, transitions)
        /// - Placeable component (open/locked state, HP, useability)
        /// - Inventory component (equipped items and inventory bag)
        /// - Script hooks component (script ResRefs and local variables)
        /// - Custom data dictionary (arbitrary key-value pairs)
        /// </remarks>
        public override byte[] Serialize()
        {
            // TODO: Implement Eclipse entity serialization
            // Based on daorigins.exe and DragonAge2.exe save format
            // Serialize ObjectId, Tag, components, and custom data
            throw new NotImplementedException("Eclipse entity serialization not yet implemented");
        }

        /// <summary>
        /// Deserializes entity data from save games.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse entity deserialization functions in daorigins.exe and DragonAge2.exe.
        /// Restores ObjectId, Tag, components, and custom data.
        /// Recreates component attachments and state.
        /// </remarks>
        public override void Deserialize(byte[] data)
        {
            // TODO: Implement Eclipse entity deserialization
            // Read ObjectId, Tag, ObjectType
            // Recreate and deserialize components
            // Restore custom data dictionary
            throw new NotImplementedException("Eclipse entity deserialization not yet implemented");
        }
    }

    /// <summary>
    /// Interface for components that need per-frame updates.
    /// </summary>
    internal interface IUpdatableComponent
    {
        void Update(float deltaTime);
    }
}


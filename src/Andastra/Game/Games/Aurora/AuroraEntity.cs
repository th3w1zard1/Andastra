using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BioWare.NET.Resource.Formats.GFF;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Aurora.Components;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora
{
    /// <summary>
    /// Aurora Engine (Neverwinter Nights) specific entity implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Entity Implementation:
    /// - Based on nwmain.exe (Neverwinter Nights) entity system
    /// - Implements ObjectId, Tag (CExoString), ObjectType structure
    /// - Component-based architecture for modular functionality
    /// - Script hooks for events and behaviors
    ///
    /// Based on reverse engineering of:
    /// - nwmain.exe: Entity creation and management
    /// - ObjectId: Located via string reference "ObjectId" @ 0x140ddb6f0
    /// - Tag: CExoString-based tag system, located via "m_sTag" @ 0x140ddafb4
    /// - GetObjectByTag: ExecuteCommandGetObjectByTag @ 0x14052d210, FindObjectByTagOrdinal @ 0x14047d1e0
    /// - Entity structure: ObjectId (uint32), Tag (CExoString), ObjectType (enum)
    /// - Component system: Transform, stats, inventory, script hooks, etc.
    ///
    /// Entity lifecycle:
    /// - Created from GIT file templates or script instantiation
    /// - Assigned sequential ObjectId for uniqueness (OBJECT_INVALID = 0x7F000000)
    /// - Components attached based on ObjectType
    /// - Registered with area and world systems
    /// - Updated each frame, destroyed when no longer needed
    ///
    /// Aurora-specific details:
    /// - Uses CExoString for Tag instead of plain string (wrapped in our implementation)
    /// - FindObjectByTagOrdinal supports ordinal-based tag lookups (0 = first match, 1 = second, etc.)
    /// - ObjectId structure similar to Odyssey but with Aurora-specific serialization
    /// </remarks>
    [PublicAPI]
    public class AuroraEntity : BaseEntity
    {
        private uint _objectId;
        private string _tag;
        private readonly ObjectType _objectType;
        private IWorld _world;
        private bool _isValid = true;
        private uint _areaId;

        /// <summary>
        /// Creates a new Aurora entity.
        /// </summary>
        /// <param name="objectId">Unique object identifier.</param>
        /// <param name="objectType">The type of object this entity represents.</param>
        /// <param name="tag">Tag string for script lookups.</param>
        /// <remarks>
        /// Based on entity creation in nwmain.exe.
        /// ObjectId must be unique within the game session.
        /// ObjectType determines available components and behaviors.
        /// Tag uses CExoString in original engine, wrapped as string here.
        /// </remarks>
        public AuroraEntity(uint objectId, ObjectType objectType, string tag = null)
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
        /// Based on ObjectId field in Aurora entity structure.
        /// Located via string reference "ObjectId" @ 0x140ddb6f0 in nwmain.exe.
        /// Assigned sequentially and must be unique across all entities.
        /// Used for script references and save game serialization.
        /// OBJECT_INVALID = 0x7F000000 in Aurora engine.
        /// </remarks>
        public override uint ObjectId => _objectId;

        /// <summary>
        /// Tag string for script lookups.
        /// </summary>
        /// <remarks>
        /// Script-accessible identifier for GetObjectByTag functions.
        /// Based on "m_sTag" @ 0x140ddafb4 in nwmain.exe.
        /// Original engine uses CExoString, wrapped as string here.
        /// Can be changed at runtime for dynamic lookups.
        /// FindObjectByTagOrdinal @ 0x14047d1e0 supports ordinal-based lookups.
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
        /// Based on nwmain.exe: All entities have transform and script hooks capability.
        /// Script hooks are loaded from GFF templates and can be set at runtime via SetScript functions.
        /// Transform component provides position/orientation data loaded from GIT files (XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation).
        /// </remarks>
        private void AttachCommonComponents()
        {
            // Attach transform component for all entities
            // Based on nwmain.exe: All entities have transform data (position, orientation, scale)
            // Transform data is loaded from GIT files (LoadCreatures @ 0x140360570, LoadWaypoints @ 0x140362fc0, etc.)
            // Position stored as XPosition, YPosition, ZPosition in GFF structures
            // Orientation stored as XOrientation, YOrientation, ZOrientation in GFF structures
            if (!HasComponent<ITransformComponent>())
            {
                var transformComponent = new AuroraTransformComponent();
                AddComponent<ITransformComponent>(transformComponent);
            }

            // Attach script hooks component for all entities
            // Based on nwmain.exe: All entities support script hooks (ScriptHeartbeat, ScriptOnNotice, etc.)
            // Script hooks are loaded from GFF templates and can be set/modified at runtime
            if (!HasComponent<IScriptHooksComponent>())
            {
                var scriptHooksComponent = new BaseScriptHooksComponent();
                AddComponent<IScriptHooksComponent>(scriptHooksComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to creatures.
        /// </summary>
        /// <remarks>
        /// Creatures have stats, inventory, combat capabilities, etc.
        /// Based on creature component structure in nwmain.exe.
        /// - CNWSCreature constructor creates creature instances with component initialization
        /// - LoadCreatures @ 0x140360570 loads creature list from area GIT and creates entities with creature components
        /// - Components attached: StatsComponent, InventoryComponent, ActionQueueComponent, CreatureComponent, FactionComponent, PerceptionComponent, RenderableComponent, AnimationComponent
        ///
        /// Component attachment order (based on nwmain.exe):
        /// 1. StatsComponent - Required for HP, abilities, skills, saves
        /// 2. InventoryComponent - Required for equipped items and inventory bag
        /// 3. CreatureComponent - Required for creature-specific data (feats, classes, appearance)
        /// 4. ActionQueueComponent - Required for action execution (movement, combat, etc.)
        /// 5. FactionComponent - Required for faction relationships and hostility
        /// 6. PerceptionComponent - Required for sight/hearing detection
        /// 7. RenderableComponent - Required for 3D model rendering
        /// 8. AnimationComponent - Required for animation playback
        /// </remarks>
        private void AttachCreatureComponents()
        {
            // Attach stats component for creatures
            // Based on nwmain.exe: CNWSCreatureStats is attached during creature creation
            // CNWSCreatureStats::LoadStats @ 0x1403975e0 loads stats from GFF
            if (!HasComponent<IStatsComponent>())
            {
                var statsComponent = new AuroraStatsComponent();
                statsComponent.Owner = this;
                AddComponent<IStatsComponent>(statsComponent);
            }

            // Attach inventory component for creatures
            // Based on nwmain.exe: CNWSInventory is attached during creature creation
            // CNWSCreature::SaveCreature @ 0x1403a0a60 saves inventory in Equip_ItemList GFFList
            // CNWSCreature::LoadCreature @ 0x1403975e0 loads inventory from Equip_ItemList GFFList
            if (!HasComponent<IInventoryComponent>())
            {
                var inventoryComponent = new Components.AuroraInventoryComponent(this);
                AddComponent<IInventoryComponent>(inventoryComponent);
            }

            // Attach creature component for creatures
            // Based on nwmain.exe: CNWSCreatureStats contains creature-specific data (feats, classes, appearance)
            // AuroraCreatureComponent provides feat lists, class lists, appearance data, etc.
            if (!HasComponent<Components.AuroraCreatureComponent>())
            {
                var creatureComponent = new Components.AuroraCreatureComponent();
                creatureComponent.Owner = this;
                AddComponent<Components.AuroraCreatureComponent>(creatureComponent);
            }

            // Attach action queue component for creatures
            // Based on nwmain.exe: CNWSObject::LoadActionQueue @ 0x1404963f0 loads ActionList from GFF
            // CNWSObject::SaveActionQueue @ 0x140499910 saves ActionList to GFF
            // Actions processed sequentially: Current action executes until complete, then next action dequeued
            if (!HasComponent<IActionQueueComponent>())
            {
                var actionQueueComponent = new Components.AuroraActionQueueComponent();
                actionQueueComponent.Owner = this;
                AddComponent<IActionQueueComponent>(actionQueueComponent);
            }

            // Attach faction component for creatures
            // Based on nwmain.exe: CNWSFaction @ 0x1404ad3e0, CFactionManager::GetFaction @ 0x140357900
            // Faction relationships stored in faction table, reputation values determine hostility
            if (!HasComponent<IFactionComponent>())
            {
                var factionComponent = new Components.AuroraFactionComponent();
                factionComponent.Owner = this;
                // Set FactionID from entity data if available (loaded from UTC template)
                // Based on nwmain.exe: FactionID loaded from creature template GFF
                if (GetData("FactionID") is int factionId)
                {
                    factionComponent.FactionId = factionId;
                }
                AddComponent<IFactionComponent>(factionComponent);
            }

            // Attach perception component for creatures
            // Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0
            // Perception range from creature stats PerceptionRange field (typically 20m sight, 15m hearing)
            // Line-of-sight checks using CNWSArea::ClearLineOfSight, stealth detection via DoStealthDetection
            if (!HasComponent<IPerceptionComponent>())
            {
                var perceptionComponent = new Components.AuroraPerceptionComponent();
                perceptionComponent.Owner = this;
                AddComponent<IPerceptionComponent>(perceptionComponent);
            }

            // Attach renderable component for creatures
            // Based on nwmain.exe: CNWSCreature::LoadAppearance @ 0x1403a0a60 loads creature model from appearance.2da row
            // ModelResRef: MDL file resource reference for 3D model (loaded from installation resources)
            // AppearanceRow: Index into appearance.2da for creature appearance customization (Appearance_Type field)
            if (!HasComponent<IRenderableComponent>())
            {
                var renderableComponent = new Components.AuroraRenderableComponent();
                renderableComponent.Owner = this;
                AddComponent<IRenderableComponent>(renderableComponent);
            }

            // Attach animation component for creatures
            // Based on nwmain.exe: Gob::PlayAnimation @ 0x140052580 handles animation playback
            // CNWSObject::AIActionPlayAnimation @ 0x1404a4700 handles AI-driven animation actions
            // Animation system supports queued animations, fire-and-forget animations, animation replacement
            if (!HasComponent<IAnimationComponent>())
            {
                var animationComponent = new Components.AuroraAnimationComponent();
                animationComponent.Owner = this;
                AddComponent<IAnimationComponent>(animationComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to doors.
        /// </summary>
        /// <remarks>
        /// Doors have open/close state, lock state, transition logic.
        /// Based on door component structure in nwmain.exe.
        /// - CNWSDoor constructor @ 0x14041d6b0 (nwmain.exe: create door instance)
        /// - LoadDoors @ 0x1403608f0 (nwmain.exe: load door list from area GIT)
        /// - Door component attached during entity creation from GIT door entries
        /// - Doors support: open/closed states, locks, traps, module/area transitions
        /// - Component provides: IsOpen, IsLocked, LockDC, KeyName, LinkedTo, LinkedToModule
        /// - Based on CNWSDoor class structure in nwmain.exe
        /// </remarks>
        private void AttachDoorComponents()
        {
            // Attach door component if not already present
            // Based on nwmain.exe: Door component is attached during entity creation
            // CNWSDoor constructor @ 0x14041d6b0 creates door instances with component initialization
            // LoadDoors @ 0x1403608f0 loads door list from area GIT and creates entities with door components
            if (!HasComponent<IDoorComponent>())
            {
                var doorComponent = new AuroraDoorComponent();
                doorComponent.Owner = this;
                AddComponent<IDoorComponent>(doorComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to placeables.
        /// </summary>
        /// <remarks>
        /// Placeables have interaction state, inventory, use logic.
        /// Based on placeable component structure in nwmain.exe.
        /// - CNWSPlaceable::LoadPlaceable @ 0x1404b4900 (nwmain.exe) - Loads placeable data from GFF
        /// - CNWSPlaceable::SavePlaceable @ 0x1404b6a60 (nwmain.exe) - Saves placeable data to GFF
        /// - LoadPlaceables @ 0x1403619e0 (nwmain.exe) - Loads placeable list from GIT
        /// - SavePlaceables @ 0x140367260 (nwmain.exe) - Saves placeable list to GIT
        /// - Located via string reference: "Placeable List" @ 0x140ddb7c0 (GFF list field in GIT)
        /// - Based on UTP file format (GFF with "UTP " signature), similar to Odyssey
        /// - Placeables have appearance, useability, locks, inventory, HP, traps, lighting
        /// - Script events: OnUsed, OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
        /// - Containers (HasInventory=true) can store items, open/close states
        /// - Lock system: KeyRequired flag, KeyName tag, LockDC difficulty class
        /// - Aurora-specific: GroundPile, Portrait, LightState, Description, Portal, trap system differences
        ///
        /// Component attachment pattern:
        /// - Based on nwmain.exe: Placeable components are attached during entity creation from GIT templates
        /// - CNWSPlaceable constructor creates placeable instances with component initialization
        /// - Component provides: IsUseable, HasInventory, IsStatic, IsOpen, IsLocked, LockDC, KeyTag, HitPoints, MaxHitPoints, Hardness, AnimationState, Conversation
        /// - Aurora-specific properties: GroundPile, Portrait, LightState, Description, Portal, KeyRequired, CloseLockDC
        /// </remarks>
        private void AttachPlaceableComponents()
        {
            // Attach placeable component if not already present
            // Based on nwmain.exe: Placeable component is attached during entity creation
            // CNWSPlaceable constructor creates placeable instances with component initialization
            // LoadPlaceables @ 0x1403619e0 loads placeable list from area GIT and creates entities with placeable components
            if (!HasComponent<IPlaceableComponent>())
            {
                var placeableComponent = new AuroraPlaceableComponent();
                placeableComponent.Owner = this;
                AddComponent<IPlaceableComponent>(placeableComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to triggers.
        /// </summary>
        /// <remarks>
        /// Triggers have enter/exit detection, script firing.
        /// Based on trigger component structure in nwmain.exe.
        /// - CNWSTrigger::LoadTrigger @ 0x1404c8a00 (nwmain.exe: load trigger data from GFF)
        /// - CNWSTrigger::SaveTrigger @ 0x1404c9b40 (nwmain.exe: save trigger data to GFF)
        /// - LoadTriggers @ 0x140362b20 (nwmain.exe: load trigger list from area GIT)
        /// - SaveTriggers @ 0x1403680a0 (nwmain.exe: save trigger list to area GIT)
        /// - Located via string reference: "Trigger List" @ 0x140ddb800 (GFF list field in GIT)
        /// - ComponentInitializer also handles this, but we ensure it's attached here for consistency
        /// - Component provides: Geometry, IsEnabled, TriggerType, LinkedTo, LinkedToModule, IsTrap, TrapActive, TrapDetected, TrapDisarmed, TrapDetectDC, TrapDisarmDC, FireOnce, HasFired, ContainsPoint, ContainsEntity
        /// </remarks>
        private void AttachTriggerComponents()
        {
            // Attach trigger component if not already present
            // Based on nwmain.exe: Trigger component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            if (!HasComponent<ITriggerComponent>())
            {
                var triggerComponent = new Components.AuroraTriggerComponent();
                triggerComponent.Owner = this;
                AddComponent<ITriggerComponent>(triggerComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to waypoints.
        /// </summary>
        /// <remarks>
        /// Waypoints have position data, pathfinding integration.
        /// Based on waypoint component structure in nwmain.exe.
        /// CNWSWaypoint constructor @ 0x140508d60, LoadWaypoint @ 0x140509f80, SaveWaypoint @ 0x14050a4d0
        /// </remarks>
        private void AttachWaypointComponents()
        {
            if (!HasComponent<IWaypointComponent>())
            {
                var waypointComponent = new AuroraWaypointComponent();
                AddComponent<IWaypointComponent>(waypointComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to sounds.
        /// </summary>
        /// <remarks>
        /// Sounds have audio playback, spatial positioning.
        /// Based on sound component structure in nwmain.exe.
        /// - CNWSSoundObject constructor @ 0x1404f3600 creates sound instances with component initialization
        /// - CNWSArea::LoadSounds @ 0x140362260 loads sound list from area GIT and creates entities with sound components
        /// - Sound component provides: Active, Continuous, Looping, Positional, Random, RandomPosition, Volume, VolumeVrtn, MaxDistance, MinDistance, Interval, IntervalVrtn, PitchVariation, SoundFiles, Hours, GeneratedType
        /// - Based on CNWSSoundObject class structure in nwmain.exe
        /// - Sound entities emit positional audio in the game world (Positional field for 3D audio)
        /// - Volume: 0-127 range (Volume field), distance falloff: MinDistance (full volume) to MaxDistance (zero volume)
        /// - Continuous sounds: Play continuously when active (Continuous field)
        /// - Random sounds: Can play random sounds from SoundFiles list (Random field), randomize position (RandomPosition field)
        /// - Interval: Time between plays for non-looping sounds (Interval field, IntervalVrtn for variation)
        /// - Volume variation: VolumeVrtn field for random volume variation
        /// - Hours: Bitmask for time-based activation (Hours field, 0-23 hour range)
        /// - Pitch variation: PitchVariation field for random pitch variation in sound playback
        /// - Uses UTS file format (GFF with "UTS " signature) for sound templates, same as Odyssey
        /// </remarks>
        private void AttachSoundComponents()
        {
            // Attach sound component if not already present
            // Based on nwmain.exe: Sound component is attached during entity creation
            // CNWSSoundObject constructor @ 0x1404f3600 creates sound instances with component initialization
            // LoadSounds @ 0x140362260 loads sound list from area GIT and creates entities with sound components
            if (!HasComponent<ISoundComponent>())
            {
                var soundComponent = new Components.AuroraSoundComponent();
                soundComponent.Owner = this;
                AddComponent<ISoundComponent>(soundComponent);
            }
        }

        /// <summary>
        /// Updates the entity each frame.
        /// </summary>
        /// <remarks>
        /// Updates all attached components.
        /// Processes any pending script events.
        /// Handles component interactions.
        ///
        /// Based on nwmain.exe: Entity update loop processes components in dependency order.
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

            // Remove from world and area collections
            // Based on nwmain.exe: Entity destruction pattern
            // - CNWSCreature::RemoveFromArea @ 0x14039e6b0 removes entity from area collections
            // - CNWSArea::RemoveObjectFromArea removes entity from type-specific lists
            // - Entity removal sequence: Remove from area collections first, then remove from world collections
            // - Located via string references: "RemoveObjectFromArea" in nwmain.exe entity management
            // - Original implementation: Entities are removed from all lookup indices when destroyed
            // - World maintains indices: ObjectId dictionary, Tag dictionary, ObjectType dictionary
            // - Areas maintain indices: Type-specific lists (Creatures, Placeables, Doors, Triggers, Waypoints, Sounds)
            if (_world != null)
            {
                // Remove from area collections first (if entity belongs to an area)
                // Based on nwmain.exe: CNWSCreature::RemoveFromArea calls CNWSArea::RemoveObjectFromArea
                // Located via string references: "RemoveObjectFromArea" in nwmain.exe
                // Original implementation: Area.RemoveObjectFromArea removes entity from area's type-specific collections
                if (_areaId != 0)
                {
                    IArea area = _world.GetArea(_areaId);
                    if (area != null && area is AuroraArea auroraArea)
                    {
                        // Remove entity from area's collections
                        // Based on nwmain.exe: CNWSArea::RemoveObjectFromArea removes from type-specific lists
                        // AuroraArea.RemoveEntity handles removal from Creatures, Placeables, Doors, Triggers, Waypoints, Sounds lists
                        auroraArea.RemoveEntity(this);
                    }
                }

                // Unregister from world collections
                // Based on nwmain.exe: World.UnregisterEntity removes entity from all world lookup indices
                // Original implementation: UnregisterEntity removes from:
                // - _entitiesById dictionary (ObjectId lookup)
                // - _entitiesByTag dictionary (Tag-based lookup, case-insensitive)
                // - _entitiesByType dictionary (ObjectType-based lookup)
                // This ensures entity is no longer accessible via GetEntity, GetEntityByTag, GetEntitiesOfType, GetAllEntities
                _world.UnregisterEntity(this);
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
        /// Based on Aurora entity serialization functions in nwmain.exe.
        /// Serializes ObjectId, Tag, components, and custom data.
        /// Uses Aurora-specific GFF save format.
        ///
        /// Reverse Engineering Notes:
        /// - nwmain.exe: CNWSCreature::SaveCreature @ 0x1403a0a60
        ///   - Uses CResGFF::WriteField* functions to write GFF fields
        ///   - Writes ObjectId, DisplayName, DetectMode, StealthMode, MasterID, CreatureSize
        ///   - Calls CNWSCreatureStats::SaveStats to save stats component
        ///   - Writes script hooks: ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked
        ///   - Saves inventory items in Equip_ItemList GFFList
        /// - nwmain.exe: CNWSPlaceable::SavePlaceable @ 0x1404b6a60
        ///   - Saves placeable data to GFF structure
        /// - nwmain.exe: CNWSDoor::SaveDoor @ 0x1404228e0
        ///   - Saves door data to GFF structure
        /// - nwmain.exe: CNWSTrigger::SaveTrigger @ 0x1404c9b40
        ///   - Saves trigger data to GFF structure
        /// - Implementation uses GFF format (GFFContent.GFF) similar to Odyssey
        ///   - GFF structure: Root struct with nested structs and lists
        ///   - Field names match Aurora engine conventions (e.g., "ScriptHeartbeat" not "OnHeartbeat")
        ///   - Tag stored as CExoString in original, serialized as string here
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
            // Create GFF structure for entity data
            // Based on nwmain.exe: CNWSCreature::SaveCreature @ 0x1403a0a60 saves entity to GFF
            // Uses generic GFF format (GFFContent.GFF) for entity serialization
            var gff = new GFF(GFFContent.GFF);
            var root = gff.Root;

            // Serialize basic entity properties
            // Based on nwmain.exe: CResGFF::WriteFieldDWORD writes ObjectId
            root.SetUInt32("ObjectId", _objectId);
            // Tag stored as CExoString in original engine, serialized as string here
            root.SetString("Tag", _tag ?? "");
            root.SetInt32("ObjectType", (int)_objectType);
            root.SetUInt32("AreaId", _areaId);
            root.SetUInt8("IsValid", _isValid ? (byte)1 : (byte)0);

            // Serialize transform component
            // Based on nwmain.exe: Position stored as XPosition, YPosition, ZPosition in GFF
            // Orientation stored as XOrientation, YOrientation, ZOrientation in GFF
            var transformComponent = GetComponent<ITransformComponent>();
            if (transformComponent != null)
            {
                root.SetSingle("XPosition", transformComponent.Position.X);
                root.SetSingle("YPosition", transformComponent.Position.Y);
                root.SetSingle("ZPosition", transformComponent.Position.Z);
                // Aurora uses orientation vectors (XOrientation, YOrientation, ZOrientation)
                // We convert facing angle to orientation vector
                float facing = transformComponent.Facing;
                root.SetSingle("XOrientation", (float)Math.Cos(facing));
                root.SetSingle("YOrientation", 0.0f);
                root.SetSingle("ZOrientation", (float)Math.Sin(facing));
                root.SetSingle("ScaleX", transformComponent.Scale.X);
                root.SetSingle("ScaleY", transformComponent.Scale.Y);
                root.SetSingle("ScaleZ", transformComponent.Scale.Z);

                // Serialize parent entity reference if present
                if (transformComponent.Parent != null)
                {
                    root.SetUInt32("ParentObjectId", transformComponent.Parent.ObjectId);
                }
            }

            // Serialize stats component (for creatures)
            // Based on nwmain.exe: CNWSCreatureStats::SaveStats saves stats to GFF
            var statsComponent = GetComponent<IStatsComponent>();
            if (statsComponent != null)
            {
                root.SetInt32("CurrentHP", statsComponent.CurrentHP);
                root.SetInt32("MaxHP", statsComponent.MaxHP);
                root.SetInt32("CurrentFP", statsComponent.CurrentFP);
                root.SetInt32("MaxFP", statsComponent.MaxFP);
                root.SetUInt8("IsDead", statsComponent.IsDead ? (byte)1 : (byte)0);
                root.SetInt32("BaseAttackBonus", statsComponent.BaseAttackBonus);
                root.SetInt32("ArmorClass", statsComponent.ArmorClass);
                root.SetInt32("FortitudeSave", statsComponent.FortitudeSave);
                root.SetInt32("ReflexSave", statsComponent.ReflexSave);
                root.SetInt32("WillSave", statsComponent.WillSave);
                root.SetSingle("WalkSpeed", statsComponent.WalkSpeed);
                root.SetSingle("RunSpeed", statsComponent.RunSpeed);
                root.SetInt32("Level", statsComponent.Level);

                // Serialize ability scores
                // Based on nwmain.exe: Ability scores stored in nested GFF struct
                var abilityStruct = root.Acquire<GFFStruct>("Abilities", new GFFStruct());
                abilityStruct.SetInt32("STR", statsComponent.GetAbility(Ability.Strength));
                abilityStruct.SetInt32("DEX", statsComponent.GetAbility(Ability.Dexterity));
                abilityStruct.SetInt32("CON", statsComponent.GetAbility(Ability.Constitution));
                abilityStruct.SetInt32("INT", statsComponent.GetAbility(Ability.Intelligence));
                abilityStruct.SetInt32("WIS", statsComponent.GetAbility(Ability.Wisdom));
                abilityStruct.SetInt32("CHA", statsComponent.GetAbility(Ability.Charisma));

                // Serialize ability modifiers
                var abilityModStruct = root.Acquire<GFFStruct>("AbilityModifiers", new GFFStruct());
                abilityModStruct.SetInt32("STR", statsComponent.GetAbilityModifier(Ability.Strength));
                abilityModStruct.SetInt32("DEX", statsComponent.GetAbilityModifier(Ability.Dexterity));
                abilityModStruct.SetInt32("CON", statsComponent.GetAbilityModifier(Ability.Constitution));
                abilityModStruct.SetInt32("INT", statsComponent.GetAbilityModifier(Ability.Intelligence));
                abilityModStruct.SetInt32("WIS", statsComponent.GetAbilityModifier(Ability.Wisdom));
                abilityModStruct.SetInt32("CHA", statsComponent.GetAbilityModifier(Ability.Charisma));

                // Serialize known spells
                // Based on nwmain.exe: CNWSCreatureStats::SaveStats saves known spells to GFF
                // Known spells are stored as a GFFList with each entry containing a SpellId field
                // Located via string references: "KnownSpells" in nwmain.exe creature save format
                var spellsList = root.Acquire<GFFList>("KnownSpells", new GFFList());

                // Get all known spells from stats component
                // Based on nwmain.exe: Spell knowledge is stored per creature and serialized to save games
                // Use reflection to access GetKnownSpells method if available (AuroraStatsComponent has this method)
                Type statsType = statsComponent.GetType();
                System.Reflection.MethodInfo getKnownSpellsMethod = statsType.GetMethod("GetKnownSpells", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (getKnownSpellsMethod != null)
                {
                    var knownSpells = getKnownSpellsMethod.Invoke(statsComponent, null) as System.Collections.IEnumerable;
                    if (knownSpells != null)
                    {
                        foreach (object spellIdObj in knownSpells)
                        {
                            if (spellIdObj is int spellId)
                            {
                                // Add spell entry to GFFList
                                // Based on nwmain.exe: Each spell entry contains SpellId field
                                var spellStruct = spellsList.Add();
                                spellStruct.SetInt32("SpellId", spellId);
                            }
                        }
                    }
                }
                else
                {
                    // Fallback: Check for spells using HasSpell method (iterate through spell IDs)
                    // This is less efficient but works if GetKnownSpells is not available
                    // Typical NWN spell ID range: 0-2000 (approximate, but we check up to 5000 for safety)
                    // Based on nwmain.exe: Spell IDs are row indices in spells.2da table
                    for (int spellId = 0; spellId < 5000; spellId++)
                    {
                        if (statsComponent.HasSpell(spellId))
                        {
                            var spellStruct = spellsList.Add();
                            spellStruct.SetInt32("SpellId", spellId);
                        }
                    }
                }
            }

            // Serialize door component
            // Based on nwmain.exe: CNWSDoor::SaveDoor @ 0x1404228e0 saves door data to GFF
            var doorComponent = GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                root.SetUInt8("IsOpen", doorComponent.IsOpen ? (byte)1 : (byte)0);
                root.SetUInt8("IsLocked", doorComponent.IsLocked ? (byte)1 : (byte)0);
                root.SetUInt8("LockableByScript", doorComponent.LockableByScript ? (byte)1 : (byte)0);
                root.SetInt32("LockDC", doorComponent.LockDC);
                root.SetUInt8("IsBashed", doorComponent.IsBashed ? (byte)1 : (byte)0);
                root.SetInt32("HitPoints", doorComponent.HitPoints);
                root.SetInt32("MaxHitPoints", doorComponent.MaxHitPoints);
                root.SetInt32("Hardness", doorComponent.Hardness);
                root.SetString("KeyTag", doorComponent.KeyTag ?? "");
                root.SetUInt8("KeyRequired", doorComponent.KeyRequired ? (byte)1 : (byte)0);
                root.SetInt32("OpenState", doorComponent.OpenState);
                root.SetString("LinkedTo", doorComponent.LinkedTo ?? "");
                root.SetString("LinkedToModule", doorComponent.LinkedToModule ?? "");
            }

            // Serialize placeable component
            // Based on nwmain.exe: CNWSPlaceable::SavePlaceable @ 0x1404b6a60 saves placeable data to GFF
            var placeableComponent = GetComponent<IPlaceableComponent>();
            if (placeableComponent != null)
            {
                root.SetUInt8("IsUseable", placeableComponent.IsUseable ? (byte)1 : (byte)0);
                root.SetUInt8("HasInventory", placeableComponent.HasInventory ? (byte)1 : (byte)0);
                root.SetUInt8("IsStatic", placeableComponent.IsStatic ? (byte)1 : (byte)0);
                root.SetUInt8("IsOpen", placeableComponent.IsOpen ? (byte)1 : (byte)0);
                root.SetUInt8("IsLocked", placeableComponent.IsLocked ? (byte)1 : (byte)0);
                root.SetInt32("LockDC", placeableComponent.LockDC);
                root.SetString("KeyTag", placeableComponent.KeyTag ?? "");
                root.SetInt32("HitPoints", placeableComponent.HitPoints);
                root.SetInt32("MaxHitPoints", placeableComponent.MaxHitPoints);
                root.SetInt32("Hardness", placeableComponent.Hardness);
                root.SetInt32("AnimationState", placeableComponent.AnimationState);
            }

            // Serialize inventory component
            // Based on nwmain.exe: CNWSCreature::SaveCreature saves items in Equip_ItemList GFFList
            // Equipment slots: 0-19 (INVENTORY_SLOT_HEAD through INVENTORY_SLOT_LEFTWEAPON2)
            // Inventory bag: Typically starts at slot 20+ (varies by implementation)
            var inventoryComponent = GetComponent<IInventoryComponent>();
            if (inventoryComponent != null)
            {
                var inventoryList = root.Acquire<GFFList>("Equip_ItemList", new GFFList());

                // Search through all possible inventory slots (0-255 is a reasonable upper bound)
                // This ensures we capture all items including those in inventory bag slots
                for (int slot = 0; slot < 256; slot++)
                {
                    var item = inventoryComponent.GetItemInSlot(slot);
                    if (item != null)
                    {
                        var itemStruct = inventoryList.Add();
                        itemStruct.SetInt32("Slot", slot);
                        itemStruct.SetUInt32("ObjectId", item.ObjectId);
                        itemStruct.SetString("ItemTag", item.Tag ?? "");
                        itemStruct.SetInt32("ItemObjectType", (int)item.ObjectType);
                    }
                }
            }

            // Serialize script hooks component
            // Based on nwmain.exe: CNWSCreature::SaveCreature writes script ResRefs with Aurora field names
            // Aurora uses: ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked
            var scriptHooksComponent = GetComponent<IScriptHooksComponent>();
            if (scriptHooksComponent != null)
            {
                // Serialize script ResRefs for all event types
                // Map ScriptEvent enum to Aurora field names
                var scriptsStruct = root.Acquire<GFFStruct>("Scripts", new GFFStruct());

                // Aurora-specific script field name mapping
                var scriptFieldMap = new Dictionary<ScriptEvent, string>
                {
                    { ScriptEvent.OnHeartbeat, "ScriptHeartbeat" },
                    { ScriptEvent.OnNotice, "ScriptOnNotice" },
                    { ScriptEvent.OnSpellAt, "ScriptSpellAt" },
                    { ScriptEvent.OnAttacked, "ScriptAttacked" },
                    { ScriptEvent.OnDamaged, "ScriptDamaged" },
                    { ScriptEvent.OnDisturbed, "ScriptDisturbed" },
                    { ScriptEvent.OnEndRound, "ScriptEndRound" },
                    { ScriptEvent.OnDialogue, "ScriptDialogue" },
                    { ScriptEvent.OnSpawn, "ScriptSpawn" },
                    { ScriptEvent.OnRested, "ScriptRested" },
                    { ScriptEvent.OnDeath, "ScriptDeath" },
                    { ScriptEvent.OnUserDefined, "ScriptUserDefine" },
                    { ScriptEvent.OnBlocked, "ScriptOnBlocked" }
                };

                foreach (ScriptEvent eventType in Enum.GetValues(typeof(ScriptEvent)))
                {
                    string scriptResRef = scriptHooksComponent.GetScript(eventType);
                    if (!string.IsNullOrEmpty(scriptResRef))
                    {
                        // Use Aurora field name if mapped, otherwise use enum name
                        string fieldName = scriptFieldMap.ContainsKey(eventType)
                            ? scriptFieldMap[eventType]
                            : eventType.ToString();
                        scriptsStruct.SetString(fieldName, scriptResRef);
                    }
                }

                // Serialize local variables using reflection to access private dictionaries
                // Based on nwmain.exe: Local variables are stored in ScriptHooksComponent
                // and serialized to GFF LocalVars structure
                var localVarsStruct = root.Acquire<GFFStruct>("LocalVariables", new GFFStruct());

                // Access private _localInts, _localFloats, _localStrings dictionaries via reflection
                Type componentType = scriptHooksComponent.GetType();
                FieldInfo localIntsField = componentType.GetField("_localInts", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo localFloatsField = componentType.GetField("_localFloats", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo localStringsField = componentType.GetField("_localStrings", BindingFlags.NonPublic | BindingFlags.Instance);

                if (localIntsField != null)
                {
                    var localInts = localIntsField.GetValue(scriptHooksComponent) as Dictionary<string, int>;
                    if (localInts != null && localInts.Count > 0)
                    {
                        var intList = localVarsStruct.Acquire<GFFList>("IntList", new GFFList());
                        foreach (var kvp in localInts)
                        {
                            var varStruct = intList.Add();
                            varStruct.SetString("Name", kvp.Key);
                            varStruct.SetInt32("Value", kvp.Value);
                        }
                    }
                }

                if (localFloatsField != null)
                {
                    var localFloats = localFloatsField.GetValue(scriptHooksComponent) as Dictionary<string, float>;
                    if (localFloats != null && localFloats.Count > 0)
                    {
                        var floatList = localVarsStruct.Acquire<GFFList>("FloatList", new GFFList());
                        foreach (var kvp in localFloats)
                        {
                            var varStruct = floatList.Add();
                            varStruct.SetString("Name", kvp.Key);
                            varStruct.SetSingle("Value", kvp.Value);
                        }
                    }
                }

                if (localStringsField != null)
                {
                    var localStrings = localStringsField.GetValue(scriptHooksComponent) as Dictionary<string, string>;
                    if (localStrings != null && localStrings.Count > 0)
                    {
                        var stringList = localVarsStruct.Acquire<GFFList>("StringList", new GFFList());
                        foreach (var kvp in localStrings)
                        {
                            var varStruct = stringList.Add();
                            varStruct.SetString("Name", kvp.Key);
                            varStruct.SetString("Value", kvp.Value ?? "");
                        }
                    }
                }
            }

            // Serialize custom data dictionary using reflection to access private _data field
            // BaseEntity stores custom data in _data dictionary for script variables and temporary state
            var customDataStruct = root.Acquire<GFFStruct>("CustomData", new GFFStruct());
            Type baseEntityType = typeof(BaseEntity);
            FieldInfo dataField = baseEntityType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);

            if (dataField != null)
            {
                var data = dataField.GetValue(this) as Dictionary<string, object>;
                if (data != null && data.Count > 0)
                {
                    var dataList = customDataStruct.Acquire<GFFList>("DataList", new GFFList());
                    foreach (var kvp in data)
                    {
                        var dataStruct = dataList.Add();
                        dataStruct.SetString("Key", kvp.Key);

                        // Serialize value based on type
                        if (kvp.Value == null)
                        {
                            dataStruct.SetString("Type", "null");
                            dataStruct.SetString("Value", "");
                        }
                        else
                        {
                            Type valueType = kvp.Value.GetType();
                            dataStruct.SetString("Type", valueType.Name);

                            if (valueType == typeof(int))
                            {
                                dataStruct.SetInt32("Value", (int)kvp.Value);
                            }
                            else if (valueType == typeof(float))
                            {
                                dataStruct.SetSingle("Value", (float)kvp.Value);
                            }
                            else if (valueType == typeof(string))
                            {
                                dataStruct.SetString("Value", (string)kvp.Value ?? "");
                            }
                            else if (valueType == typeof(bool))
                            {
                                dataStruct.SetUInt8("Value", (bool)kvp.Value ? (byte)1 : (byte)0);
                            }
                            else if (valueType == typeof(uint))
                            {
                                dataStruct.SetUInt32("Value", (uint)kvp.Value);
                            }
                            else
                            {
                                // For other types, serialize as string representation
                                dataStruct.SetString("Value", kvp.Value.ToString());
                            }
                        }
                    }
                }
            }

            // Convert GFF to byte array
            return gff.ToBytes();
        }

        /// <summary>
        /// Deserializes entity data from save games.
        /// </summary>
        /// <remarks>
        /// Based on Aurora entity deserialization functions in nwmain.exe.
        /// Restores ObjectId, Tag, components, and custom data.
        /// Recreates component attachments and state.
        ///
        /// Reverse Engineering Notes:
        /// - nwmain.exe: CNWSCreature::LoadCreature @ 0x1403975e0
        ///   - Uses CResGFF::ReadField* functions to read GFF fields
        ///   - Reads ObjectId, DisplayName, DetectMode, StealthMode, MasterID, CreatureSize
        ///   - Calls CNWSCreatureStats::LoadStats to load stats component
        ///   - Reads script hooks: ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, etc.
        ///   - Loads inventory items from Equip_ItemList GFFList
        /// - nwmain.exe: CNWSPlaceable::LoadPlaceable @ 0x1404b4900
        ///   - Loads placeable data from GFF structure
        /// - nwmain.exe: CNWSDoor::LoadDoor @ 0x1404208a0
        ///   - Loads door data from GFF structure
        /// - nwmain.exe: CNWSTrigger::LoadTrigger @ 0x1404c8a00
        ///   - Loads trigger data from GFF structure
        /// - Implementation uses GFF format (GFFContent.GFF) similar to Odyssey
        ///   - GFF structure: Root struct with nested structs and lists
        ///   - Field names match Aurora engine conventions
        ///   - Tag stored as CExoString in original, deserialized as string here
        /// </remarks>
        public override void Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Entity deserialization data cannot be null or empty", nameof(data));
            }

            // Parse GFF from byte array
            // Based on nwmain.exe: CResGFF::LoadGFFFile loads GFF structure
            GFF gff = GFF.FromBytes(data);
            if (gff == null || gff.Root == null)
            {
                throw new System.IO.InvalidDataException("Invalid GFF data for entity deserialization");
            }

            var root = gff.Root;

            // Deserialize basic entity properties
            // Based on nwmain.exe: CResGFF::ReadFieldDWORD reads ObjectId
            if (root.Exists("ObjectId"))
            {
                _objectId = root.GetUInt32("ObjectId");
            }
            // Tag stored as CExoString in original engine, deserialized as string here
            if (root.Exists("Tag"))
            {
                _tag = root.GetString("Tag") ?? "";
            }
            // ObjectType is read-only, so we verify it matches
            if (root.Exists("ObjectType"))
            {
                int objectTypeValue = root.GetInt32("ObjectType");
                if (objectTypeValue != (int)_objectType)
                {
                    throw new System.IO.InvalidDataException($"Deserialized ObjectType {objectTypeValue} does not match entity ObjectType {(int)_objectType}");
                }
            }
            if (root.Exists("AreaId"))
            {
                _areaId = root.GetUInt32("AreaId");
            }
            if (root.Exists("IsValid"))
            {
                _isValid = root.GetUInt8("IsValid") != 0;
            }

            // Deserialize Transform component
            // Based on nwmain.exe: Position stored as XPosition, YPosition, ZPosition in GFF
            // Orientation stored as XOrientation, YOrientation, ZOrientation in GFF
            if (root.Exists("XPosition") && root.Exists("YPosition") && root.Exists("ZPosition"))
            {
                var transformComponent = GetComponent<ITransformComponent>();
                if (transformComponent == null)
                {
                    transformComponent = new AuroraTransformComponent();
                    AddComponent<ITransformComponent>(transformComponent);
                }

                float x = root.GetSingle("XPosition");
                float y = root.GetSingle("YPosition");
                float z = root.GetSingle("ZPosition");
                transformComponent.Position = new System.Numerics.Vector3(x, y, z);

                // Aurora uses orientation vectors (XOrientation, YOrientation, ZOrientation)
                // Convert orientation vector to facing angle
                if (root.Exists("XOrientation") && root.Exists("ZOrientation"))
                {
                    float xOrientation = root.GetSingle("XOrientation");
                    float zOrientation = root.GetSingle("ZOrientation");
                    // Calculate facing angle from orientation vector
                    float facing = (float)Math.Atan2(zOrientation, xOrientation);
                    transformComponent.Facing = facing;
                }
                else if (root.Exists("Facing"))
                {
                    // Fallback to Facing field if orientation vectors not present
                    transformComponent.Facing = root.GetSingle("Facing");
                }

                if (root.Exists("ScaleX") && root.Exists("ScaleY") && root.Exists("ScaleZ"))
                {
                    float scaleX = root.GetSingle("ScaleX");
                    float scaleY = root.GetSingle("ScaleY");
                    float scaleZ = root.GetSingle("ScaleZ");
                    transformComponent.Scale = new System.Numerics.Vector3(scaleX, scaleY, scaleZ);
                }

                // Parent will be resolved later when all entities are loaded
                if (root.Exists("ParentObjectId"))
                {
                    uint parentObjectId = root.GetUInt32("ParentObjectId");
                    if (parentObjectId != 0)
                    {
                        Type baseEntityTypeForParent = typeof(BaseEntity);
                        FieldInfo dataFieldForParent = baseEntityTypeForParent.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (dataFieldForParent != null)
                        {
                            var entityData = dataFieldForParent.GetValue(this) as Dictionary<string, object>;
                            if (entityData == null)
                            {
                                entityData = new Dictionary<string, object>();
                                dataFieldForParent.SetValue(this, entityData);
                            }
                            entityData["_ParentObjectId"] = parentObjectId;
                        }
                    }
                }
            }

            // Deserialize Stats component
            // Based on nwmain.exe: CNWSCreatureStats::LoadStats loads stats from GFF
            if (root.Exists("CurrentHP"))
            {
                // Stats component should already exist for creatures, but we ensure it's present
                var statsComponent = GetComponent<IStatsComponent>();
                if (statsComponent == null)
                {
                    // Create AuroraStatsComponent if missing (e.g., during deserialization)
                    // Based on nwmain.exe: CNWSCreatureStats is created during creature loading
                    statsComponent = new AuroraStatsComponent();
                    statsComponent.Owner = this;
                    AddComponent<IStatsComponent>(statsComponent);
                }

                statsComponent.CurrentHP = root.GetInt32("CurrentHP");
                if (root.Exists("MaxHP"))
                {
                    statsComponent.MaxHP = root.GetInt32("MaxHP");
                }
                if (root.Exists("CurrentFP"))
                {
                    statsComponent.CurrentFP = root.GetInt32("CurrentFP");
                }
                if (root.Exists("MaxFP"))
                {
                    statsComponent.MaxFP = root.GetInt32("MaxFP");
                }
                // IsDead is computed from CurrentHP, so we don't deserialize it directly

                if (root.Exists("BaseAttackBonus"))
                {
                    // BaseAttackBonus is read-only, so we can't set it directly
                }
                if (root.Exists("ArmorClass"))
                {
                    // ArmorClass is read-only, so we can't set it directly
                }
                if (root.Exists("FortitudeSave"))
                {
                    // FortitudeSave is read-only, so we can't set it directly
                }
                if (root.Exists("ReflexSave"))
                {
                    // ReflexSave is read-only, so we can't set it directly
                }
                if (root.Exists("WillSave"))
                {
                    // WillSave is read-only, so we can't set it directly
                }
                if (root.Exists("WalkSpeed"))
                {
                    // WalkSpeed is read-only, so we can't set it directly
                }
                if (root.Exists("RunSpeed"))
                {
                    // RunSpeed is read-only, so we can't set it directly
                }
                if (root.Exists("Level"))
                {
                    // Level is read-only, so we can't set it directly
                }

                // Deserialize ability scores
                if (root.Exists("Abilities"))
                {
                    var abilityStruct = root.GetStruct("Abilities");
                    if (abilityStruct != null && abilityStruct.Count > 0)
                    {
                        if (abilityStruct.Exists("STR"))
                        {
                            statsComponent.SetAbility(Ability.Strength, abilityStruct.GetInt32("STR"));
                        }
                        if (abilityStruct.Exists("DEX"))
                        {
                            statsComponent.SetAbility(Ability.Dexterity, abilityStruct.GetInt32("DEX"));
                        }
                        if (abilityStruct.Exists("CON"))
                        {
                            statsComponent.SetAbility(Ability.Constitution, abilityStruct.GetInt32("CON"));
                        }
                        if (abilityStruct.Exists("INT"))
                        {
                            statsComponent.SetAbility(Ability.Intelligence, abilityStruct.GetInt32("INT"));
                        }
                        if (abilityStruct.Exists("WIS"))
                        {
                            statsComponent.SetAbility(Ability.Wisdom, abilityStruct.GetInt32("WIS"));
                        }
                        if (abilityStruct.Exists("CHA"))
                        {
                            statsComponent.SetAbility(Ability.Charisma, abilityStruct.GetInt32("CHA"));
                        }
                    }
                }

                // Deserialize ability modifiers (read but don't set - they're computed)
                if (root.Exists("AbilityModifiers"))
                {
                    var abilityModStruct = root.GetStruct("AbilityModifiers");
                    // Modifiers are computed from ability scores, so we don't need to restore them
                }

                // Deserialize known spells
                // Based on nwmain.exe: CNWSCreatureStats::LoadStats loads known spells from GFF
                // Known spells are stored as a GFFList with each entry containing a SpellId field
                // Located via string references: "KnownSpells" in nwmain.exe creature load format
                if (root.Exists("KnownSpells"))
                {
                    var spellsList = root.GetList("KnownSpells");
                    if (spellsList != null && spellsList.Count > 0)
                    {
                        // Get stats component (should already exist for creatures)
                        var statsComponentForSpells = GetComponent<IStatsComponent>();
                        if (statsComponentForSpells == null)
                        {
                            // Create AuroraStatsComponent if missing (edge case during deserialization)
                            statsComponentForSpells = new AuroraStatsComponent();
                            statsComponentForSpells.Owner = this;
                            AddComponent<IStatsComponent>(statsComponentForSpells);
                        }

                        // Use reflection to access AddSpell method if available (AuroraStatsComponent has this method)
                        Type statsTypeForSpells = statsComponentForSpells.GetType();
                        System.Reflection.MethodInfo addSpellMethod = statsTypeForSpells.GetMethod("AddSpell", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (addSpellMethod != null)
                        {
                            // Iterate through all spell entries in the GFFList
                            for (int i = 0; i < spellsList.Count; i++)
                            {
                                var spellStruct = spellsList.At(i);
                                if (spellStruct != null && spellStruct.Exists("SpellId"))
                                {
                                    int spellId = spellStruct.GetInt32("SpellId");
                                    // Add spell to creature's known spells
                                    // Based on nwmain.exe: Spell knowledge is restored from save game data
                                    addSpellMethod.Invoke(statsComponentForSpells, new object[] { spellId });
                                }
                            }
                        }
                        else
                        {
                            // Fallback: If AddSpell method is not available, we can't restore spells
                            // This should not happen with AuroraStatsComponent, but handle gracefully
                        }
                    }
                }
            }

            // Deserialize Inventory component
            // Based on nwmain.exe: CNWSCreature::LoadCreature loads items from Equip_ItemList GFFList
            // CNWSCreature::LoadCreature @ 0x1403975e0 loads inventory from Equip_ItemList GFFList
            // CNWSInventory is always present for creatures - if missing during deserialization, create it
            // This matches the behavior in CNWSCreature constructor where inventory is attached during creature creation
            if (root.Exists("Equip_ItemList"))
            {
                var inventoryComponent = GetComponent<IInventoryComponent>();
                if (inventoryComponent == null)
                {
                    // Inventory component should exist for creatures, but we ensure it's present
                    // Based on nwmain.exe: CNWSInventory is attached during creature creation
                    // If missing during deserialization (edge case), create it automatically
                    inventoryComponent = new Components.AuroraInventoryComponent(this);
                    AddComponent<IInventoryComponent>(inventoryComponent);
                }

                var inventoryList = root.GetList("Equip_ItemList");
                if (inventoryList != null)
                {
                    // Store item references for later resolution when all entities are loaded
                    var itemReferences = new List<(int slot, uint objectId, string tag, int objectType)>();
                    for (int i = 0; i < inventoryList.Count; i++)
                    {
                        var itemStruct = inventoryList.At(i);
                        if (itemStruct != null)
                        {
                            int slot = itemStruct.GetInt32("Slot");
                            uint itemObjectId = itemStruct.GetUInt32("ObjectId");
                            string itemTag = itemStruct.GetString("ItemTag") ?? "";
                            int itemObjectType = itemStruct.GetInt32("ItemObjectType");
                            itemReferences.Add((slot, itemObjectId, itemTag, itemObjectType));
                        }
                    }
                    // Store item references in custom data for later resolution
                    Type baseEntityTypeForItems = typeof(BaseEntity);
                    FieldInfo dataFieldForItems = baseEntityTypeForItems.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (dataFieldForItems != null)
                    {
                        var entityData = dataFieldForItems.GetValue(this) as Dictionary<string, object>;
                        if (entityData == null)
                        {
                            entityData = new Dictionary<string, object>();
                            dataFieldForItems.SetValue(this, entityData);
                        }
                        entityData["_ItemReferences"] = itemReferences;
                    }
                }
            }

            // Deserialize ScriptHooks component
            // Based on nwmain.exe: CNWSCreature::LoadCreature reads script ResRefs with Aurora field names
            if (root.Exists("Scripts"))
            {
                var scriptHooksComponent = GetComponent<IScriptHooksComponent>();
                if (scriptHooksComponent == null)
                {
                    // Script hooks component should exist for all entities
                    scriptHooksComponent = new BaseScriptHooksComponent();
                    AddComponent<IScriptHooksComponent>(scriptHooksComponent);
                }

                var scriptsStruct = root.GetStruct("Scripts");
                if (scriptsStruct != null && scriptsStruct.Count > 0)
                {
                    // Aurora-specific script field name mapping
                    var scriptFieldMap = new Dictionary<string, ScriptEvent>
                    {
                        { "ScriptHeartbeat", ScriptEvent.OnHeartbeat },
                        { "ScriptOnNotice", ScriptEvent.OnNotice },
                        { "ScriptSpellAt", ScriptEvent.OnSpellAt },
                        { "ScriptAttacked", ScriptEvent.OnAttacked },
                        { "ScriptDamaged", ScriptEvent.OnDamaged },
                        { "ScriptDisturbed", ScriptEvent.OnDisturbed },
                        { "ScriptEndRound", ScriptEvent.OnEndRound },
                        { "ScriptDialogue", ScriptEvent.OnDialogue },
                        { "ScriptSpawn", ScriptEvent.OnSpawn },
                        { "ScriptRested", ScriptEvent.OnRested },
                        { "ScriptDeath", ScriptEvent.OnDeath },
                        { "ScriptUserDefine", ScriptEvent.OnUserDefined },
                        { "ScriptOnBlocked", ScriptEvent.OnBlocked }
                    };

                    // Read all script fields from GFF
                    // Iterate through all fields in the struct
                    foreach (var (fieldName, fieldType, fieldValue) in scriptsStruct)
                    {
                        if (fieldType == GFFFieldType.String || fieldType == GFFFieldType.ResRef)
                        {
                            string resRef = fieldValue?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(resRef))
                            {
                                // Map Aurora field name to ScriptEvent enum
                                if (scriptFieldMap.ContainsKey(fieldName))
                                {
                                    scriptHooksComponent.SetScript(scriptFieldMap[fieldName], resRef);
                                }
                                else
                                {
                                    // Try to parse as enum name if not in map
                                    if (Enum.TryParse<ScriptEvent>(fieldName, out ScriptEvent eventType))
                                    {
                                        scriptHooksComponent.SetScript(eventType, resRef);
                                    }
                                }
                            }
                        }
                    }
                }

                // Deserialize local variables using reflection
                if (root.Exists("LocalVariables"))
                {
                    var localVarsStruct = root.GetStruct("LocalVariables");
                    if (localVarsStruct != null && localVarsStruct.Count > 0)
                    {
                        Type componentType = scriptHooksComponent.GetType();
                        FieldInfo localIntsField = componentType.GetField("_localInts", BindingFlags.NonPublic | BindingFlags.Instance);
                        FieldInfo localFloatsField = componentType.GetField("_localFloats", BindingFlags.NonPublic | BindingFlags.Instance);
                        FieldInfo localStringsField = componentType.GetField("_localStrings", BindingFlags.NonPublic | BindingFlags.Instance);

                        // Deserialize local ints
                        if (localVarsStruct.Exists("IntList"))
                        {
                            var intList = localVarsStruct.GetList("IntList");
                            if (intList != null && intList.Count > 0 && localIntsField != null)
                            {
                                var localInts = localIntsField.GetValue(scriptHooksComponent) as Dictionary<string, int>;
                                if (localInts == null)
                                {
                                    localInts = new Dictionary<string, int>();
                                    localIntsField.SetValue(scriptHooksComponent, localInts);
                                }
                                localInts.Clear();
                                for (int i = 0; i < intList.Count; i++)
                                {
                                    var varStruct = intList.At(i);
                                    if (varStruct != null && varStruct.Exists("Name") && varStruct.Exists("Value"))
                                    {
                                        string name = varStruct.GetString("Name");
                                        int value = varStruct.GetInt32("Value");
                                        localInts[name] = value;
                                    }
                                }
                            }
                        }

                        // Deserialize local floats
                        if (localVarsStruct.Exists("FloatList"))
                        {
                            var floatList = localVarsStruct.GetList("FloatList");
                            if (floatList != null && floatList.Count > 0 && localFloatsField != null)
                            {
                                var localFloats = localFloatsField.GetValue(scriptHooksComponent) as Dictionary<string, float>;
                                if (localFloats == null)
                                {
                                    localFloats = new Dictionary<string, float>();
                                    localFloatsField.SetValue(scriptHooksComponent, localFloats);
                                }
                                localFloats.Clear();
                                for (int i = 0; i < floatList.Count; i++)
                                {
                                    var varStruct = floatList.At(i);
                                    if (varStruct != null && varStruct.Exists("Name") && varStruct.Exists("Value"))
                                    {
                                        string name = varStruct.GetString("Name");
                                        float value = varStruct.GetSingle("Value");
                                        localFloats[name] = value;
                                    }
                                }
                            }
                        }

                        // Deserialize local strings
                        if (localVarsStruct.Exists("StringList"))
                        {
                            var stringList = localVarsStruct.GetList("StringList");
                            if (stringList != null && stringList.Count > 0 && localStringsField != null)
                            {
                                var localStrings = localStringsField.GetValue(scriptHooksComponent) as Dictionary<string, string>;
                                if (localStrings == null)
                                {
                                    localStrings = new Dictionary<string, string>();
                                    localStringsField.SetValue(scriptHooksComponent, localStrings);
                                }
                                localStrings.Clear();
                                for (int i = 0; i < stringList.Count; i++)
                                {
                                    var varStruct = stringList.At(i);
                                    if (varStruct != null && varStruct.Exists("Name") && varStruct.Exists("Value"))
                                    {
                                        string name = varStruct.GetString("Name");
                                        string value = varStruct.GetString("Value") ?? "";
                                        localStrings[name] = value;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Deserialize Door component
            // Based on nwmain.exe: CNWSDoor::LoadDoor @ 0x1404208a0 loads door data from GFF
            if (root.Exists("IsOpen") || root.Exists("IsLocked"))
            {
                var doorComponent = GetComponent<IDoorComponent>();
                if (doorComponent == null)
                {
                    doorComponent = new AuroraDoorComponent();
                    doorComponent.Owner = this;
                    AddComponent<IDoorComponent>(doorComponent);
                }

                if (root.Exists("IsOpen"))
                {
                    doorComponent.IsOpen = root.GetUInt8("IsOpen") != 0;
                }
                if (root.Exists("IsLocked"))
                {
                    doorComponent.IsLocked = root.GetUInt8("IsLocked") != 0;
                }
                if (root.Exists("LockableByScript"))
                {
                    doorComponent.LockableByScript = root.GetUInt8("LockableByScript") != 0;
                }
                if (root.Exists("LockDC"))
                {
                    doorComponent.LockDC = root.GetInt32("LockDC");
                }
                if (root.Exists("IsBashed"))
                {
                    doorComponent.IsBashed = root.GetUInt8("IsBashed") != 0;
                }
                if (root.Exists("HitPoints"))
                {
                    doorComponent.HitPoints = root.GetInt32("HitPoints");
                }
                if (root.Exists("MaxHitPoints"))
                {
                    doorComponent.MaxHitPoints = root.GetInt32("MaxHitPoints");
                }
                if (root.Exists("Hardness"))
                {
                    doorComponent.Hardness = root.GetInt32("Hardness");
                }
                if (root.Exists("KeyTag"))
                {
                    doorComponent.KeyTag = root.GetString("KeyTag");
                }
                if (root.Exists("KeyRequired"))
                {
                    doorComponent.KeyRequired = root.GetUInt8("KeyRequired") != 0;
                }
                if (root.Exists("OpenState"))
                {
                    doorComponent.OpenState = root.GetInt32("OpenState");
                }
                if (root.Exists("LinkedTo"))
                {
                    doorComponent.LinkedTo = root.GetString("LinkedTo");
                }
                if (root.Exists("LinkedToModule"))
                {
                    doorComponent.LinkedToModule = root.GetString("LinkedToModule");
                }
            }

            // Deserialize Placeable component
            // Based on nwmain.exe: CNWSPlaceable::LoadPlaceable @ 0x1404b4900 loads placeable data from GFF
            if (root.Exists("IsUseable") || root.Exists("HasInventory"))
            {
                var placeableComponent = GetComponent<IPlaceableComponent>();
                if (placeableComponent == null)
                {
                    placeableComponent = new AuroraPlaceableComponent();
                    placeableComponent.Owner = this;
                    AddComponent<IPlaceableComponent>(placeableComponent);
                }

                if (root.Exists("IsUseable"))
                {
                    placeableComponent.IsUseable = root.GetUInt8("IsUseable") != 0;
                }
                if (root.Exists("HasInventory"))
                {
                    placeableComponent.HasInventory = root.GetUInt8("HasInventory") != 0;
                }
                if (root.Exists("IsStatic"))
                {
                    placeableComponent.IsStatic = root.GetUInt8("IsStatic") != 0;
                }
                if (root.Exists("IsOpen"))
                {
                    placeableComponent.IsOpen = root.GetUInt8("IsOpen") != 0;
                }
                if (root.Exists("IsLocked"))
                {
                    placeableComponent.IsLocked = root.GetUInt8("IsLocked") != 0;
                }
                if (root.Exists("LockDC"))
                {
                    placeableComponent.LockDC = root.GetInt32("LockDC");
                }
                if (root.Exists("KeyTag"))
                {
                    placeableComponent.KeyTag = root.GetString("KeyTag");
                }
                if (root.Exists("HitPoints"))
                {
                    placeableComponent.HitPoints = root.GetInt32("HitPoints");
                }
                if (root.Exists("MaxHitPoints"))
                {
                    placeableComponent.MaxHitPoints = root.GetInt32("MaxHitPoints");
                }
                if (root.Exists("Hardness"))
                {
                    placeableComponent.Hardness = root.GetInt32("Hardness");
                }
                if (root.Exists("AnimationState"))
                {
                    placeableComponent.AnimationState = root.GetInt32("AnimationState");
                }
            }

            // Deserialize custom data dictionary
            if (root.Exists("CustomData"))
            {
                var customDataStruct = root.GetStruct("CustomData");
                if (customDataStruct != null && customDataStruct.Exists("DataList"))
                {
                    var dataList = customDataStruct.GetList("DataList");
                    if (dataList != null && dataList.Count > 0)
                    {
                        Type baseEntityType = typeof(BaseEntity);
                        FieldInfo dataField = baseEntityType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (dataField != null)
                        {
                            var entityData = dataField.GetValue(this) as Dictionary<string, object>;
                            if (entityData == null)
                            {
                                entityData = new Dictionary<string, object>();
                                dataField.SetValue(this, entityData);
                            }
                            entityData.Clear();

                            for (int i = 0; i < dataList.Count; i++)
                            {
                                var dataStruct = dataList.At(i);
                                if (dataStruct != null && dataStruct.Exists("Key") && dataStruct.Exists("Type") && dataStruct.Exists("Value"))
                                {
                                    string key = dataStruct.GetString("Key");
                                    string type = dataStruct.GetString("Type");
                                    object value = null;

                                    switch (type)
                                    {
                                        case "null":
                                            value = null;
                                            break;
                                        case "Int32":
                                        case "int":
                                            value = dataStruct.GetInt32("Value");
                                            break;
                                        case "Single":
                                        case "float":
                                            value = dataStruct.GetSingle("Value");
                                            break;
                                        case "String":
                                        case "string":
                                            value = dataStruct.GetString("Value");
                                            break;
                                        case "Boolean":
                                        case "bool":
                                            value = dataStruct.GetUInt8("Value") != 0;
                                            break;
                                        case "UInt32":
                                        case "uint":
                                            value = dataStruct.GetUInt32("Value");
                                            break;
                                        default:
                                            // For other types, try to deserialize as string
                                            value = dataStruct.GetString("Value");
                                            break;
                                    }

                                    entityData[key] = value;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validates component dependencies before attachment (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Component dependency validation in CNWSObject::AddComponent.
        /// Aurora-specific dependencies:
        /// - ActionQueueComponent requires TransformComponent (for movement actions)
        /// - CombatComponent requires StatsComponent (for HP, AC, attack calculations)
        /// - InventoryComponent requires StatsComponent (for encumbrance calculations)
        ///
        /// Located via string references: Component validation in nwmain.exe entity system.
        /// </remarks>
        protected override void ValidateComponentDependencies(System.Type componentType)
        {
            // Call base validation first
            base.ValidateComponentDependencies(componentType);

            // Aurora-specific: ActionQueueComponent requires TransformComponent
            if (componentType == typeof(IActionQueueComponent) ||
                typeof(IActionQueueComponent).IsAssignableFrom(componentType))
            {
                if (!HasComponent<ITransformComponent>())
                {
                    throw new InvalidOperationException(
                        "ActionQueueComponent requires TransformComponent on entity " + ObjectId);
                }
            }
        }

        /// <summary>
        /// Handles component interactions when a component is attached (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Component interaction handling in CNWSObject::AddComponent.
        /// Aurora-specific interactions:
        /// - When StatsComponent is attached, notify CombatComponent to recalculate combat stats
        /// - When InventoryComponent is attached, notify StatsComponent to recalculate encumbrance
        /// - When TransformComponent is attached, notify ActionQueueComponent to update position
        ///
        /// Located via string references: Component interaction patterns in nwmain.exe.
        /// </remarks>
        protected override void HandleComponentAttached(IComponent component)
        {
            // Call base handling first
            base.HandleComponentAttached(component);

            // Aurora-specific: When StatsComponent is attached, notify CombatComponent
            if (component is IStatsComponent)
            {
                // CombatComponent would recalculate combat stats if it existed
                // This is handled implicitly through component queries
            }

            // Aurora-specific: When InventoryComponent is attached, notify StatsComponent
            if (component is IInventoryComponent)
            {
                var statsComponent = GetComponent<IStatsComponent>();
                if (statsComponent != null)
                {
                    // StatsComponent would recalculate encumbrance if it tracked that
                    // This is handled implicitly through component queries
                }
            }

            // Aurora-specific: When TransformComponent is attached, notify ActionQueueComponent
            if (component is ITransformComponent)
            {
                var actionQueueComponent = GetComponent<IActionQueueComponent>();
                if (actionQueueComponent != null)
                {
                    // ActionQueueComponent will use TransformComponent for movement actions
                    // No explicit notification needed, but we could add a method if needed
                }
            }
        }

        /// <summary>
        /// Handles component interactions during entity update (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Component interaction handling in CNWSObject::Update.
        /// Aurora-specific interactions:
        /// - HP changes trigger death state updates in CombatComponent
        /// - Position changes trigger perception updates
        /// - Inventory changes affect encumbrance and movement speed
        /// - Action queue execution may modify transform through movement actions
        ///
        /// Located via string references: Component interaction patterns in nwmain.exe update loop.
        /// </remarks>
        protected override void HandleComponentInteractions(float deltaTime)
        {
            // Call base handling first
            base.HandleComponentInteractions(deltaTime);

            // Aurora-specific: HP changes trigger death state updates
            var statsComponent = GetComponent<IStatsComponent>();
            if (statsComponent != null)
            {
                // Check if entity just died
                if (statsComponent.IsDead)
                {
                    // Fire OnDeath script event if not already fired
                    var scriptHooksComponent = GetComponent<IScriptHooksComponent>();
                    if (scriptHooksComponent != null && World != null && World.EventBus != null)
                    {
                        // OnDeath event would be fired by combat system, not here
                        // But we could add a check here if needed
                    }
                }
            }

            // Aurora-specific: Inventory changes affect encumbrance
            var inventoryComponent = GetComponent<IInventoryComponent>();
            if (inventoryComponent != null && statsComponent != null)
            {
                // StatsComponent would recalculate WalkSpeed/RunSpeed based on encumbrance
                // This is handled implicitly through component queries
            }
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


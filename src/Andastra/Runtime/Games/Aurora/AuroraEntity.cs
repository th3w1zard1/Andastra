using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Aurora
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
        /// </remarks>
        private void AttachCommonComponents()
        {
            // Attach script hooks component for all entities
            // Based on nwmain.exe: All entities support script hooks (ScriptHeartbeat, ScriptOnNotice, etc.)
            // Script hooks are loaded from GFF templates and can be set/modified at runtime
            if (!HasComponent<IScriptHooksComponent>())
            {
                var scriptHooksComponent = new BaseScriptHooksComponent();
                AddComponent<IScriptHooksComponent>(scriptHooksComponent);
            }

            // TODO: Attach transform component
            // TODO: Attach any other common components
        }

        /// <summary>
        /// Attaches components specific to creatures.
        /// </summary>
        /// <remarks>
        /// Creatures have stats, inventory, combat capabilities, etc.
        /// Based on creature component structure in nwmain.exe.
        /// </remarks>
        private void AttachCreatureComponents()
        {
            // TODO: Attach creature-specific components
            // StatsComponent, InventoryComponent, CombatComponent, etc.
        }

        /// <summary>
        /// Attaches components specific to doors.
        /// </summary>
        /// <remarks>
        /// Doors have open/close state, lock state, transition logic.
        /// Based on door component structure in nwmain.exe.
        /// </remarks>
        private void AttachDoorComponents()
        {
            // TODO: Attach door-specific components
            // DoorComponent with open/closed states, locks, transitions
        }

        /// <summary>
        /// Attaches components specific to placeables.
        /// </summary>
        /// <remarks>
        /// Placeables have interaction state, inventory, use logic.
        /// Based on placeable component structure in nwmain.exe.
        /// </remarks>
        private void AttachPlaceableComponents()
        {
            // TODO: Attach placeable-specific components
            // PlaceableComponent with use/interaction state
        }

        /// <summary>
        /// Attaches components specific to triggers.
        /// </summary>
        /// <remarks>
        /// Triggers have enter/exit detection, script firing.
        /// Based on trigger component structure in nwmain.exe.
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
        /// Based on waypoint component structure in nwmain.exe.
        /// </remarks>
        private void AttachWaypointComponents()
        {
            // TODO: Attach waypoint-specific components
            // WaypointComponent with position and path data
        }

        /// <summary>
        /// Attaches components specific to sounds.
        /// </summary>
        /// <remarks>
        /// Sounds have audio playback, spatial positioning.
        /// Based on sound component structure in nwmain.exe.
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
        /// </remarks>
        public override void Update(float deltaTime)
        {
            if (!IsValid)
                return;

            // Update all components
            foreach (var component in GetAllComponents())
            {
                if (component is IUpdatableComponent updatable)
                {
                    updatable.Update(deltaTime);
                }
            }

            // TODO: Process script events and hooks
            // TODO: Handle component interactions
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
        /// Based on Aurora entity serialization functions in nwmain.exe.
        /// Serializes ObjectId, Tag, components, and custom data.
        /// Uses Aurora-specific save format.
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
            // TODO: Implement Aurora entity serialization
            // Based on nwmain.exe save format
            // Serialize ObjectId, Tag (CExoString), components, and custom data
            throw new NotImplementedException("Aurora entity serialization not yet implemented");
        }

        /// <summary>
        /// Deserializes entity data from save games.
        /// </summary>
        /// <remarks>
        /// Based on Aurora entity deserialization functions in nwmain.exe.
        /// Restores ObjectId, Tag, components, and custom data.
        /// Recreates component attachments and state.
        /// </remarks>
        public override void Deserialize(byte[] data)
        {
            // TODO: Implement Aurora entity deserialization
            // Read ObjectId, Tag (CExoString), ObjectType
            // Recreate and deserialize components
            // Restore custom data dictionary
            throw new NotImplementedException("Aurora entity deserialization not yet implemented");
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


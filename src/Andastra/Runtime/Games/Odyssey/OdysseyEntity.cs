using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using JetBrains.Annotations;
using Andastra.Parsing.Formats.GFF;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Odyssey.Components;

namespace Andastra.Runtime.Games.Odyssey
{
    /// <summary>
    /// Odyssey Engine (KotOR/KotOR2) specific entity implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Entity Implementation:
    /// - Based on swkotor.exe and swkotor2.exe entity systems
    /// - Implements ObjectId, Tag, ObjectType structure
    /// - Component-based architecture for modular functionality
    /// - Script hooks for events and behaviors
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Entity creation and management
    ///   - ObjectId string reference: "ObjectId" @ 0x00744c24
    ///   - ObjectIDList string reference: "ObjectIDList" @ 0x007465cc
    ///   - Object events: "EVENT_DESTROY_OBJECT" @ 0x00744b10, "EVENT_OPEN_OBJECT" @ 0x00744b68, "EVENT_CLOSE_OBJECT" @ 0x00744b7c
    ///   - "EVENT_LOCK_OBJECT" @ 0x00744ae8, "EVENT_UNLOCK_OBJECT" @ 0x00744afc
    /// - swkotor2.exe: ObjectId at offset +4, FUN_004e28c0 (save), FUN_005fb0f0 (load)
    ///   - ObjectId string reference: "ObjectId" @ 0x007bce5c
    ///   - ObjectIDList string reference: "ObjectIDList" @ 0x007bfd7c
    ///   - Object logging format: "OID: %08x, Tag: %s, %s" @ 0x007c76b8 used for debug/error logging
    ///   - Object list handling: "ObjectList" @ 0x007bfdbc, "ObjectValue" @ 0x007bfd70
    ///   - Entity serialization: FUN_004e28c0 @ 0x004e28c0 saves Creature List with ObjectId fields (offset +4 in object structure)
    ///   - Entity deserialization: FUN_005fb0f0 @ 0x005fb0f0 loads creature data from GFF, reads ObjectId at offset +4
    ///   - AreaId: FUN_005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90, "AreaId" @ 0x007bef48
    /// - Entity structure: ObjectId (uint32) at offset +4, Tag (string), ObjectType (enum), AreaId (uint32)
    /// - Component system: Transform, stats, inventory, script hooks, etc.
    ///
    /// Entity lifecycle:
    /// - Created from GIT file templates or script instantiation
    /// - Assigned sequential ObjectId for uniqueness (OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001)
    /// - Components attached based on ObjectType
    /// - Registered with area and world systems
    /// - Updated each frame, destroyed when no longer needed
    /// </remarks>
    [PublicAPI]
    public class OdysseyEntity : BaseEntity
    {
        private uint _objectId;
        private string _tag;
        private readonly ObjectType _objectType;
        private IWorld _world;
        private bool _isValid = true;
        private uint _areaId;

        /// <summary>
        /// Creates a new Odyssey entity.
        /// </summary>
        /// <param name="objectId">Unique object identifier.</param>
        /// <param name="objectType">The type of object this entity represents.</param>
        /// <param name="tag">Tag string for script lookups.</param>
        /// <remarks>
        /// Based on entity creation in swkotor2.exe.
        /// ObjectId must be unique within the game session.
        /// ObjectType determines available components and behaviors.
        /// </remarks>
        public OdysseyEntity(uint objectId, ObjectType objectType, string tag = null)
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
        /// Based on ObjectId field at offset +4 in entity structure.
        /// Located via string references: "ObjectId" @ 0x007bce5c (swkotor2.exe), "ObjectId" @ 0x00744c24 (swkotor.exe)
        /// Assigned sequentially and must be unique across all entities.
        /// Used for script references and save game serialization.
        /// OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001
        /// </remarks>
        public override uint ObjectId => _objectId;

        /// <summary>
        /// Tag string for script lookups.
        /// </summary>
        /// <remarks>
        /// Script-accessible identifier for GetObjectByTag functions.
        /// Located via string references in swkotor.exe and swkotor2.exe (various locations).
        /// Object logging format: "OID: %08x, Tag: %s, %s" @ 0x007c76b8 (swkotor2.exe) used for debug/error logging
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
        /// Based on swkotor2.exe: FUN_005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90
        /// Located via string reference: "AreaId" @ 0x007bef48
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
        /// Based on swkotor2.exe: All entities have transform and script hooks capability.
        /// Script hooks are loaded from GFF templates (UTC, UTD, UTP, etc.) and can be
        /// set at runtime via SetScript functions.
        /// </remarks>
        private void AttachCommonComponents()
        {
            // Attach script hooks component for all entities
            // Based on swkotor2.exe: All entities support script hooks (ScriptHeartbeat, ScriptOnNotice, etc.)
            // ComponentInitializer also attaches this, but we ensure it's attached here for consistency
            // Script hooks are loaded from GFF templates and can be set/modified at runtime
            if (!HasComponent<IScriptHooksComponent>())
            {
                var scriptHooksComponent = new ScriptHooksComponent();
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
        /// Based on creature component structure in swkotor2.exe.
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
        /// Based on door component structure in swkotor2.exe.
        /// - FUN_005838d0 @ 0x005838d0: Door initialization from GIT/GFF
        /// - FUN_00580ed0 @ 0x00580ed0: Door loading function that loads door properties
        /// - Door component attached during entity creation from UTD templates
        /// - Doors support: open/closed states, locks, traps, module/area transitions
        /// - Component provides: IsOpen, IsLocked, LockDC, KeyTag, LinkedTo, LinkedToModule
        /// </remarks>
        private void AttachDoorComponents()
        {
            // Attach door component if not already present
            // Based on swkotor2.exe: Door component is attached during entity creation
            // ComponentInitializer also handles this, but we ensure it's attached here for consistency
            if (!HasComponent<IDoorComponent>())
            {
                var doorComponent = new OdysseyDoorComponent();
                doorComponent.Owner = this;
                AddComponent<IDoorComponent>(doorComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to placeables.
        /// </summary>
        /// <remarks>
        /// Placeables have interaction state, inventory, use logic.
        /// Based on placeable component structure in swkotor2.exe.
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
        /// Based on trigger component structure in swkotor2.exe.
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
        /// Based on waypoint component structure in swkotor2.exe.
        /// FUN_004e08e0 @ 0x004e08e0 loads waypoint instances from GIT
        /// </remarks>
        private void AttachWaypointComponents()
        {
            if (!HasComponent<IWaypointComponent>())
            {
                var waypointComponent = new OdysseyWaypointComponent();
                AddComponent<IWaypointComponent>(waypointComponent);
            }
        }

        /// <summary>
        /// Attaches components specific to sounds.
        /// </summary>
        /// <remarks>
        /// Sounds have audio playback, spatial positioning.
        /// Based on sound component structure in swkotor2.exe.
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
        /// Based on swkotor2.exe: Entity update loop processes components in dependency order.
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
        /// Based on FUN_004e28c0 @ 0x004e28c0 in swkotor2.exe.
        /// Serializes ObjectId, Tag, components, and custom data.
        /// Uses GFF format for structured data storage.
        ///
        /// Serialized data includes:
        /// - Basic entity properties (ObjectId, Tag, ObjectType, AreaId)
        /// - Transform component (position, facing, scale)
        /// - Stats component (HP, FP, abilities, skills, saves)
        /// - Door component (open/locked state, HP, transitions)
        /// - Placeable component (open/locked state, HP, useability)
        /// - Inventory component (equipped items and inventory bag)
        /// - Script hooks component (script ResRefs and local variables)
        /// - Custom data dictionary (arbitrary key-value pairs)
        /// </remarks>
        public override byte[] Serialize()
        {
            // Create GFF structure for entity data
            // Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 saves entity to GFF
            // Uses generic GFF format (GFFContent.GFF) for entity serialization
            var gff = new GFF(GFFContent.GFF);
            var root = gff.Root;

            // Serialize basic entity properties
            root.SetUInt32("ObjectId", _objectId);
            root.SetString("Tag", _tag ?? "");
            root.SetInt32("ObjectType", (int)_objectType);
            root.SetUInt32("AreaId", _areaId);
            root.SetUInt8("IsValid", _isValid ? (byte)1 : (byte)0);

            // Serialize transform component
            var transformComponent = GetComponent<ITransformComponent>();
            if (transformComponent != null)
            {
                root.SetSingle("X", transformComponent.Position.X);
                root.SetSingle("Y", transformComponent.Position.Y);
                root.SetSingle("Z", transformComponent.Position.Z);
                root.SetSingle("Facing", transformComponent.Facing);
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

                // Serialize known spells (if any)
                // Note: This is a simplified implementation - full version would iterate through all spell IDs
                var spellsList = root.Acquire<GFFList>("KnownSpells", new GFFList());
                // In a full implementation, we would iterate through all possible spell IDs and check HasSpell
                // For now, we serialize an empty list as a placeholder for the structure
            }

            // Serialize door component
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
            // Based on swkotor2.exe: Inventory items are serialized with their slot indices
            // Equipment slots: 0-19 (INVENTORY_SLOT_HEAD through INVENTORY_SLOT_LEFTWEAPON2)
            // Inventory bag: Typically starts at slot 20+ (varies by implementation)
            var inventoryComponent = GetComponent<IInventoryComponent>();
            if (inventoryComponent != null)
            {
                var inventoryList = root.Acquire<GFFList>("Inventory", new GFFList());

                // Search through all possible inventory slots (0-255 is a reasonable upper bound)
                // This ensures we capture all items including those in inventory bag slots
                for (int slot = 0; slot < 256; slot++)
                {
                    var item = inventoryComponent.GetItemInSlot(slot);
                    if (item != null)
                    {
                        var itemStruct = inventoryList.Add();
                        itemStruct.SetInt32("Slot", slot);
                        itemStruct.SetUInt32("ItemObjectId", item.ObjectId);
                        itemStruct.SetString("ItemTag", item.Tag ?? "");
                        itemStruct.SetInt32("ItemObjectType", (int)item.ObjectType);
                    }
                }
            }

            // Serialize script hooks component
            var scriptHooksComponent = GetComponent<IScriptHooksComponent>();
            if (scriptHooksComponent != null)
            {
                // Serialize script ResRefs for all event types
                var scriptsStruct = root.Acquire<GFFStruct>("Scripts", new GFFStruct());
                foreach (ScriptEvent eventType in System.Enum.GetValues(typeof(ScriptEvent)))
                {
                    string scriptResRef = scriptHooksComponent.GetScript(eventType);
                    if (!string.IsNullOrEmpty(scriptResRef))
                    {
                        scriptsStruct.SetString(eventType.ToString(), scriptResRef);
                    }
                }

                // Serialize local variables using reflection to access private dictionaries
                // Based on swkotor2.exe: Local variables are stored in ScriptHooksComponent
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
        /// Based on FUN_005fb0f0 @ 0x005fb0f0 in swkotor2.exe.
        /// Restores ObjectId, Tag, components, and custom data.
        /// Recreates component attachments and state.
        /// </remarks>
        public override void Deserialize(byte[] data)
        {
            // TODO: Implement entity deserialization
            // Read ObjectId, Tag, ObjectType
            // Recreate and deserialize components
            // Restore custom data dictionary
            throw new NotImplementedException("Entity deserialization not yet implemented");
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

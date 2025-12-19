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
    /// - swkotor2.exe: ObjectId at offset +4, FUN_004e28c0 (save), FUN_005fb0f0 (load)
    /// - Entity structure: ObjectId (uint32), Tag (string), ObjectType (enum)
    /// - Component system: Transform, stats, inventory, script hooks, etc.
    ///
    /// Entity lifecycle:
    /// - Created from GIT file templates or script instantiation
    /// - Assigned sequential ObjectId for uniqueness
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
        /// Assigned sequentially and must be unique across all entities.
        /// Used for script references and save game serialization.
        /// </remarks>
        public override uint ObjectId => _objectId;

        /// <summary>
        /// Tag string for script lookups.
        /// </summary>
        /// <remarks>
        /// Script-accessible identifier for GetObjectByTag functions.
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
        private void AttachCommonComponents()
        {
            // TODO: Attach transform component
            // TODO: Attach script hooks component
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
        /// </remarks>
        private void AttachDoorComponents()
        {
            // TODO: Attach door-specific components
            // DoorComponent with open/close/lock state
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

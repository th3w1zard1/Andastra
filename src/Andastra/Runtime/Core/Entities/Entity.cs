using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Entities
{
    /// <summary>
    /// Base entity implementation.
    /// </summary>
    /// <remarks>
    /// Entity System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) entity system
    /// - Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c
    /// - "AreaId" @ 0x007bef48 (entity area association)
    /// - Original engine: Entities have ObjectId (uint32), Tag (string), ObjectType (enum), AreaId (uint32)
    /// - ObjectId assignment: Sequential uint32 starting from 1 (OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001)
    /// - Component system: Entities use component-based architecture for stats, transform, inventory, etc.
    /// - Script hooks: Entities store script ResRefs for various events (OnHeartbeat, OnAttacked, etc.)
    /// - Original entity structure includes: Position (Vector3), Orientation (Vector3), AreaId, ObjectId, Tag
    /// - Entity serialization: FUN_005226d0 @ 0x005226d0 saves entity state to GFF
    ///   - Saves DetectMode (byte), StealthMode (byte), CreatureSize (float)
    ///   - Saves IsDestroyable (byte), IsRaiseable (byte), DeadSelectable (byte)
    ///   - Saves script hooks: ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked, ScriptEndDialogue
    ///   - Saves Equip_ItemList (equipped items with ObjectId), ItemList (inventory items with ObjectId)
    ///   - Saves PerceptionList (perception data with ObjectId and PerceptionData byte)
    ///   - Saves CombatRoundData (if in combat, via FUN_00529470)
    ///   - Saves AreaId (int32), AmbientAnimState (byte), Animation (float), CreatnScrptFird (byte)
    ///   - Saves PM_IsDisguised (byte), PM_Appearance (int16 if disguised), Listening (byte), ForceAlwaysUpdate (byte)
    ///   - Saves Position: XPosition, YPosition, ZPosition (floats)
    ///   - Saves Orientation: XOrientation, YOrientation, ZOrientation (floats)
    ///   - Saves JoiningXP (float), BonusForcePoints (float), AssignedPup (float), PlayerCreated (float)
    ///   - Saves FollowInfo (if following, via FUN_0050b920), ActionList (via FUN_00508200)
    /// - Entity deserialization: FUN_005223a0 @ 0x005223a0 (load creature data from GFF)
    ///   - Loads AreaId from GFF at offset 0x90 (via FUN_00412d40 with "AreaId" field name)
    ///   - Loads creature template data via FUN_005fb0f0 (creature template loading)
    ///   - Loads DetectMode, StealthMode, CreatureSize, IsDestroyable, IsRaiseable, DeadSelectable
    ///   - Loads BonusForcePoints, AssignedPup, PlayerCreated, AmbientAnimState, Animation, CreatnScrptFird
    ///   - Loads PM_IsDisguised, PM_Appearance, Listening, ForceAlwaysUpdate
    ///   - Calls FUN_0050c510 (load creature stats/scripts), FUN_00521d40 (load creature equipment), FUN_005f9e00 (load creature inventory)
    ///   - Calls FUN_00509bf0, FUN_00513440, FUN_0051d4d0, FUN_0050b650 for additional creature data loading
    /// - Creature list serialization: FUN_004e28c0 @ 0x004e28c0 (save creature list to GFF)
    ///   - Iterates through creature list (Creature List GFF field), saves each creature's ObjectId and full creature data
    ///   - For each creature: Creates GFF struct, writes ObjectId (via FUN_00413880 with "ObjectId" field name), calls FUN_005226d0 to save full creature data
    ///   - Only saves creatures that are not player-controlled (checks IsPC flag) and not destroyed
    /// - Creature deserialization: FUN_005fb0f0 @ 0x005fb0f0 loads creature data from GFF format, reads ObjectId at offset +4 in structure
    /// - Object logging format: "OID: %08x, Tag: %s, %s" @ 0x007c76b8 used for debug/error logging
    /// - Object events: "EVENT_DESTROY_OBJECT" @ 0x007bcd48, "EVENT_OPEN_OBJECT" @ 0x007bcda0, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
    /// - Script hook fields: "ScriptHeartbeat" @ 0x007beeb0, "ScriptOnNotice" @ 0x007beea0, plus ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked, ScriptEndDialogue
    /// </remarks>
    public class Entity : IEntity
    {
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ObjectId assignment system
        // Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c
        // Original implementation: ObjectId is unique 32-bit identifier assigned sequentially
        // OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001, OBJECT_TYPE_INVALID = 0x7F000002
        // ObjectId starts from 1 and increments for each new entity
        private static uint _nextObjectId = 1;
        private readonly Dictionary<Type, IComponent> _components;
        private readonly Dictionary<string, object> _data;
        private readonly Dictionary<ScriptEvent, string> _scripts;
        private bool _isValid;

        /// <summary>
        /// Creates a new entity with the specified object ID and type.
        /// </summary>
        public Entity(uint objectId, ObjectType objectType)
        {
            ObjectId = objectId;
            ObjectType = objectType;
            World = null;
            _components = new Dictionary<Type, IComponent>();
            _data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _scripts = new Dictionary<ScriptEvent, string>();
            _isValid = true;
            Tag = string.Empty;
            _position = Vector3.Zero;
            _facing = 0f;
            AreaId = 0;
        }

        /// <summary>
        /// Creates a new entity with auto-assigned object ID.
        /// </summary>
        public Entity(ObjectType objectType, IWorld world)
        {
            ObjectId = _nextObjectId++;
            ObjectType = objectType;
            World = world;
            _components = new Dictionary<Type, IComponent>();
            _data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _scripts = new Dictionary<ScriptEvent, string>();
            _isValid = true;
            Tag = string.Empty;
            _position = Vector3.Zero;
            _facing = 0f;
            AreaId = 0;
        }

        public uint ObjectId { get; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get; }
        public bool IsValid { get { return _isValid; } }
        public IWorld World { get; set; }

        /// <summary>
        /// Gets or sets the entity position in world space.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005226d0 @ 0x005226d0 saves XPosition, YPosition, ZPosition
        /// Synchronized with TransformComponent if present.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                // If TransformComponent exists, use it as source of truth
                ITransformComponent transform = GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    return transform.Position;
                }
                return _position;
            }
            set
            {
                _position = value;
                // Synchronize with TransformComponent if present
                ITransformComponent transform = GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    transform.Position = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the entity facing angle in radians.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005226d0 @ 0x005226d0 saves XOrientation, YOrientation, ZOrientation
        /// Synchronized with TransformComponent if present.
        /// </summary>
        public float Facing
        {
            get
            {
                // If TransformComponent exists, use it as source of truth
                ITransformComponent transform = GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    return transform.Facing;
                }
                return _facing;
            }
            set
            {
                _facing = value;
                // Synchronize with TransformComponent if present
                ITransformComponent transform = GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    transform.Facing = value;
                }
            }
        }

        // Backing fields for Position and Facing (used when TransformComponent is not present)
        private Vector3 _position;
        private float _facing;

        /// <summary>
        /// Gets or sets the area ID this entity belongs to.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005223a0 @ 0x005223a0 loads AreaId from GFF at offset 0x90
        /// Located via string reference: "AreaId" @ 0x007bef48
        /// </summary>
        public uint AreaId { get; set; }

        /// <summary>
        /// The resource reference of the template this entity was spawned from.
        /// </summary>
        public string TemplateResRef { get; set; }

        public T GetComponent<T>() where T : class, IComponent
        {
            if (_components.TryGetValue(typeof(T), out IComponent component))
            {
                return component as T;
            }

            // Check for interface implementations
            foreach (KeyValuePair<Type, IComponent> kvp in _components)
            {
                if (kvp.Value is T typedComponent)
                {
                    return typedComponent;
                }
            }

            return null;
        }

        public void AddComponent<T>(T component) where T : class, IComponent
        {
            if (component == null)
            {
                throw new ArgumentNullException("component");
            }

            Type type = typeof(T);
            if (_components.ContainsKey(type))
            {
                throw new InvalidOperationException("Component of type " + type.Name + " already exists on entity " + ObjectId);
            }

            _components[type] = component;
            component.Owner = this;
            component.OnAttach();
        }

        public bool RemoveComponent<T>() where T : class, IComponent
        {
            Type type = typeof(T);
            if (_components.TryGetValue(type, out IComponent component))
            {
                component.OnDetach();
                component.Owner = null;
                _components.Remove(type);
                return true;
            }
            return false;
        }

        public bool HasComponent<T>() where T : class, IComponent
        {
            if (_components.ContainsKey(typeof(T)))
            {
                return true;
            }

            foreach (KeyValuePair<Type, IComponent> kvp in _components)
            {
                if (kvp.Value is T)
                {
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<IComponent> GetAllComponents()
        {
            return _components.Values;
        }

        /// <summary>
        /// Marks this entity as destroyed.
        /// </summary>
        public void Destroy()
        {
            if (!_isValid)
            {
                return;
            }

            foreach (IComponent component in _components.Values)
            {
                component.OnDetach();
                component.Owner = null;
            }

            _components.Clear();
            _isValid = false;
        }

        /// <summary>
        /// Resets the object ID counter (for testing).
        /// </summary>
        public static void ResetObjectIdCounter()
        {
            _nextObjectId = 1;
        }

        #region Data Storage

        /// <summary>
        /// Sets arbitrary data on the entity.
        /// </summary>
        public void SetData(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (value == null)
            {
                _data.Remove(key);
            }
            else
            {
                _data[key] = value;
            }
        }

        /// <summary>
        /// Gets arbitrary data from the entity.
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default(T))
        {
            if (string.IsNullOrEmpty(key))
            {
                return defaultValue;
            }

            object value;
            if (_data.TryGetValue(key, out value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Gets arbitrary data from the entity.
        /// </summary>
        public object GetData(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            object value;
            if (_data.TryGetValue(key, out value))
            {
                return value;
            }

            return null;
        }

        /// <summary>
        /// Checks if the entity has data with the specified key.
        /// </summary>
        public bool HasData(string key)
        {
            return !string.IsNullOrEmpty(key) && _data.ContainsKey(key);
        }

        #endregion

        #region Script Hooks

        /// <summary>
        /// Sets a script for the specified event.
        /// </summary>
        public void SetScript(ScriptEvent eventType, string scriptResRef)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                _scripts.Remove(eventType);
            }
            else
            {
                _scripts[eventType] = scriptResRef;
            }
        }

        /// <summary>
        /// Gets the script for the specified event.
        /// </summary>
        public string GetScript(ScriptEvent eventType)
        {
            string script;
            if (_scripts.TryGetValue(eventType, out script))
            {
                return script;
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets all script events registered on this entity.
        /// </summary>
        public IEnumerable<ScriptEvent> GetScriptEvents()
        {
            return _scripts.Keys;
        }

        #endregion
    }
}


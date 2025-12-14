using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Entities
{
    /// <summary>
    /// Base entity implementation.
    /// </summary>
    public class Entity : IEntity
    {
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
            Position = Vector3.Zero;
            Facing = 0f;
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
            Position = Vector3.Zero;
            Facing = 0f;
        }

        public uint ObjectId { get; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get; }
        public bool IsValid { get { return _isValid; } }
        public IWorld World { get; set; }

        /// <summary>
        /// Gets or sets the entity position in world space.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Gets or sets the entity facing angle in radians.
        /// </summary>
        public float Facing { get; set; }

        public T GetComponent<T>() where T : class, IComponent
        {
            if (_components.TryGetValue(typeof(T), out IComponent component))
            {
                return component as T;
            }

            // Check for interface implementations
            foreach (var kvp in _components)
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

            foreach (var kvp in _components)
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

            foreach (var component in _components.Values)
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


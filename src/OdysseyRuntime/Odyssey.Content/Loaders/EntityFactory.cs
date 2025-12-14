using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Content.Loaders
{
    /// <summary>
    /// Factory for spawning entities from templates and GIT instances.
    /// </summary>
    public class EntityFactory
    {
        private readonly TemplateLoader _templateLoader;
        private readonly IWorld _world;

        public EntityFactory(TemplateLoader templateLoader, IWorld world)
        {
            _templateLoader = templateLoader ?? throw new ArgumentNullException("templateLoader");
            _world = world ?? throw new ArgumentNullException("world");
        }

        /// <summary>
        /// Spawns a creature from a template at the specified position.
        /// </summary>
        public async Task<IEntity> SpawnCreatureAsync(
            string templateResRef,
            Vector3 position,
            float facing = 0f,
            CancellationToken ct = default(CancellationToken))
        {
            var template = await _templateLoader.LoadCreatureTemplateAsync(templateResRef, ct);
            if (template == null)
            {
                return null;
            }

            var entity = new Entity(ObjectType.Creature, _world);
            entity.Tag = template.Tag;

            // Add transform component
            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent<ITransformComponent>(transform);

            // Add stats component
            var stats = new StatsComponent
            {
                CurrentHP = template.CurrentHP,
                MaxHP = template.MaxHP,
                CurrentFP = template.CurrentFP,
                MaxFP = template.MaxFP,
                ArmorClass = 10 + template.NaturalAC,
                Strength = template.Strength,
                Dexterity = template.Dexterity,
                Constitution = template.Constitution,
                Intelligence = template.Intelligence,
                Wisdom = template.Wisdom,
                Charisma = template.Charisma
            };
            entity.AddComponent<IStatsComponent>(stats);

            // Add script hooks
            var scriptHooks = new ScriptHooksComponent();
            SetScriptHook(scriptHooks, ScriptEvent.OnSpawn, template.OnSpawn);
            SetScriptHook(scriptHooks, ScriptEvent.OnDeath, template.OnDeath);
            SetScriptHook(scriptHooks, ScriptEvent.OnHeartbeat, template.OnHeartbeat);
            SetScriptHook(scriptHooks, ScriptEvent.OnPerception, template.OnPerception);
            SetScriptHook(scriptHooks, ScriptEvent.OnDamaged, template.OnDamaged);
            SetScriptHook(scriptHooks, ScriptEvent.OnAttacked, template.OnAttacked);
            SetScriptHook(scriptHooks, ScriptEvent.OnEndRound, template.OnEndRound);
            SetScriptHook(scriptHooks, ScriptEvent.OnDialogue, template.OnDialogue);
            SetScriptHook(scriptHooks, ScriptEvent.OnDisturbed, template.OnDisturbed);
            SetScriptHook(scriptHooks, ScriptEvent.OnBlocked, template.OnBlocked);
            SetScriptHook(scriptHooks, ScriptEvent.OnUserDefined, template.OnUserDefined);
            entity.AddComponent<IScriptHooksComponent>(scriptHooks);

            // Add renderable component
            var renderable = new RenderableComponent
            {
                AppearanceId = template.Appearance,
                BodyVariation = template.BodyVariation,
                TextureVariation = template.TextureVar
            };
            entity.AddComponent<IRenderableComponent>(renderable);

            // Register with world
            _world.AddEntity(entity);

            return entity;
        }

        /// <summary>
        /// Spawns a placeable from a template at the specified position.
        /// </summary>
        public async Task<IEntity> SpawnPlaceableAsync(
            string templateResRef,
            Vector3 position,
            float facing = 0f,
            CancellationToken ct = default(CancellationToken))
        {
            var template = await _templateLoader.LoadPlaceableTemplateAsync(templateResRef, ct);
            if (template == null)
            {
                return null;
            }

            var entity = new Entity(ObjectType.Placeable, _world);
            entity.Tag = template.Tag;

            // Add transform component
            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent<ITransformComponent>(transform);

            // Add script hooks
            var scriptHooks = new ScriptHooksComponent();
            SetScriptHook(scriptHooks, ScriptEvent.OnUsed, template.OnUsed);
            SetScriptHook(scriptHooks, ScriptEvent.OnHeartbeat, template.OnHeartbeat);
            SetScriptHook(scriptHooks, ScriptEvent.OnDisturbed, template.OnInvDisturbed);
            SetScriptHook(scriptHooks, ScriptEvent.OnOpen, template.OnOpen);
            SetScriptHook(scriptHooks, ScriptEvent.OnClosed, template.OnClosed);
            SetScriptHook(scriptHooks, ScriptEvent.OnLock, template.OnLock);
            SetScriptHook(scriptHooks, ScriptEvent.OnUnlock, template.OnUnlock);
            SetScriptHook(scriptHooks, ScriptEvent.OnDamaged, template.OnDamaged);
            SetScriptHook(scriptHooks, ScriptEvent.OnDeath, template.OnDeath);
            SetScriptHook(scriptHooks, ScriptEvent.OnUserDefined, template.OnUserDefined);
            entity.AddComponent<IScriptHooksComponent>(scriptHooks);

            // Add renderable component
            var renderable = new RenderableComponent
            {
                AppearanceId = template.Appearance,
                IsStatic = template.Static
            };
            entity.AddComponent<IRenderableComponent>(renderable);

            // Register with world
            _world.AddEntity(entity);

            return entity;
        }

        /// <summary>
        /// Spawns a door from a template at the specified position.
        /// </summary>
        public async Task<IEntity> SpawnDoorAsync(
            string templateResRef,
            Vector3 position,
            float facing = 0f,
            CancellationToken ct = default(CancellationToken))
        {
            var template = await _templateLoader.LoadDoorTemplateAsync(templateResRef, ct);
            if (template == null)
            {
                return null;
            }

            var entity = new Entity(ObjectType.Door, _world);
            entity.Tag = template.Tag;

            // Add transform component
            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent<ITransformComponent>(transform);

            // Add script hooks
            var scriptHooks = new ScriptHooksComponent();
            SetScriptHook(scriptHooks, ScriptEvent.OnClick, template.OnClick);
            SetScriptHook(scriptHooks, ScriptEvent.OnOpen, template.OnOpen);
            SetScriptHook(scriptHooks, ScriptEvent.OnClosed, template.OnClosed);
            SetScriptHook(scriptHooks, ScriptEvent.OnDamaged, template.OnDamaged);
            SetScriptHook(scriptHooks, ScriptEvent.OnDeath, template.OnDeath);
            SetScriptHook(scriptHooks, ScriptEvent.OnLock, template.OnLock);
            SetScriptHook(scriptHooks, ScriptEvent.OnUnlock, template.OnUnlock);
            SetScriptHook(scriptHooks, ScriptEvent.OnHeartbeat, template.OnHeartbeat);
            SetScriptHook(scriptHooks, ScriptEvent.OnUserDefined, template.OnUserDefined);
            entity.AddComponent<IScriptHooksComponent>(scriptHooks);

            // Add door-specific component
            var doorComponent = new DoorComponent
            {
                IsLocked = template.Locked,
                LockDC = template.LockDC,
                KeyRequired = template.KeyRequired,
                KeyName = template.KeyName,
                LinkedTo = template.LinkedTo,
                LinkedToModule = template.LinkedToModule,
                LinkedToFlags = template.LinkedToFlags,
                IsOpen = template.AnimationState == 1
            };
            entity.AddComponent<IDoorComponent>(doorComponent);

            // Add renderable component
            var renderable = new RenderableComponent
            {
                AppearanceId = template.GenericType,
                IsStatic = template.Static
            };
            entity.AddComponent<IRenderableComponent>(renderable);

            // Register with world
            _world.AddEntity(entity);

            return entity;
        }

        /// <summary>
        /// Spawns a trigger from a template with the specified geometry.
        /// </summary>
        public async Task<IEntity> SpawnTriggerAsync(
            string templateResRef,
            Vector3 position,
            IReadOnlyList<Vector3> geometry,
            CancellationToken ct = default(CancellationToken))
        {
            var template = await _templateLoader.LoadTriggerTemplateAsync(templateResRef, ct);
            if (template == null)
            {
                return null;
            }

            var entity = new Entity(ObjectType.Trigger, _world);
            entity.Tag = template.Tag;

            // Add transform component
            var transform = new TransformComponent
            {
                Position = position
            };
            entity.AddComponent<ITransformComponent>(transform);

            // Add script hooks
            var scriptHooks = new ScriptHooksComponent();
            SetScriptHook(scriptHooks, ScriptEvent.OnEnter, template.OnEnter);
            SetScriptHook(scriptHooks, ScriptEvent.OnExit, template.OnExit);
            SetScriptHook(scriptHooks, ScriptEvent.OnHeartbeat, template.OnHeartbeat);
            SetScriptHook(scriptHooks, ScriptEvent.OnUserDefined, template.OnUserDefined);
            entity.AddComponent<IScriptHooksComponent>(scriptHooks);

            // Add trigger-specific component
            var triggerComponent = new TriggerComponent
            {
                TriggerType = template.Type,
                Faction = template.Faction,
                IsTrap = template.Trapable,
                TrapOneShot = template.TrapOneShot,
                TrapType = template.TrapType,
                DisarmDC = template.DisarmDC,
                DetectDC = template.DetectDC
            };
            if (geometry != null)
            {
                foreach (var vertex in geometry)
                {
                    triggerComponent.Geometry.Add(vertex);
                }
            }
            entity.AddComponent<ITriggerComponent>(triggerComponent);

            // Register with world
            _world.AddEntity(entity);

            return entity;
        }

        /// <summary>
        /// Spawns a waypoint from a template at the specified position.
        /// </summary>
        public async Task<IEntity> SpawnWaypointAsync(
            string templateResRef,
            Vector3 position,
            float facing = 0f,
            CancellationToken ct = default(CancellationToken))
        {
            var template = await _templateLoader.LoadWaypointTemplateAsync(templateResRef, ct);
            if (template == null)
            {
                return null;
            }

            var entity = new Entity(ObjectType.Waypoint, _world);
            entity.Tag = template.Tag;

            // Add transform component
            var transform = new TransformComponent
            {
                Position = position,
                Facing = facing
            };
            entity.AddComponent<ITransformComponent>(transform);

            // Waypoints are invisible markers, no renderable component

            // Register with world
            _world.AddEntity(entity);

            return entity;
        }

        /// <summary>
        /// Spawns a sound emitter from a template at the specified position.
        /// </summary>
        public async Task<IEntity> SpawnSoundAsync(
            string templateResRef,
            Vector3 position,
            CancellationToken ct = default(CancellationToken))
        {
            var template = await _templateLoader.LoadSoundTemplateAsync(templateResRef, ct);
            if (template == null)
            {
                return null;
            }

            var entity = new Entity(ObjectType.Sound, _world);
            entity.Tag = template.Tag;

            // Add transform component
            var transform = new TransformComponent
            {
                Position = position
            };
            entity.AddComponent<ITransformComponent>(transform);

            // Add audio component
            var audioComponent = new AudioComponent
            {
                IsActive = template.Active,
                IsContinuous = template.Continuous,
                IsLooping = template.Looping,
                IsPositional = template.Positional,
                IsRandom = template.Random,
                Volume = template.Volume / 127f,
                Interval = template.Interval / 1000f,
                MaxDistance = template.MaxDistance,
                MinDistance = template.MinDistance,
                Elevation = template.Elevation
            };
            entity.AddComponent<IAudioComponent>(audioComponent);

            // Register with world
            _world.AddEntity(entity);

            return entity;
        }

        private void SetScriptHook(ScriptHooksComponent hooks, ScriptEvent evt, string script)
        {
            if (!string.IsNullOrEmpty(script))
            {
                hooks.SetScript(evt, script);
            }
        }
    }

    #region Component Implementations

    /// <summary>
    /// Basic transform component implementation.
    /// </summary>
    internal class TransformComponent : ITransformComponent
    {
        public IEntity Owner { get; set; }
        public Vector3 Position { get; set; }
        public float Facing { get; set; }
        
        public void OnAttach() { }
        public void OnDetach() { }
    }

    /// <summary>
    /// Basic stats component implementation.
    /// </summary>
    internal class StatsComponent : IStatsComponent
    {
        public IEntity Owner { get; set; }
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
        public int CurrentFP { get; set; }
        public int MaxFP { get; set; }
        public int ArmorClass { get; set; }
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }
    }

    /// <summary>
    /// Basic renderable component implementation.
    /// </summary>
    internal class RenderableComponent : IRenderableComponent
    {
        public IEntity Owner { get; set; }
        public int AppearanceId { get; set; }
        public int BodyVariation { get; set; }
        public int TextureVariation { get; set; }
        public bool IsVisible { get; set; }
        public bool IsStatic { get; set; }

        public RenderableComponent()
        {
            IsVisible = true;
        }

        public void OnAttach() { }
        public void OnDetach() { }
    }

    /// <summary>
    /// Script hooks component implementation.
    /// </summary>
    internal class ScriptHooksComponent : IScriptHooksComponent
    {
        private readonly Dictionary<ScriptEvent, string> _scripts;

        public IEntity Owner { get; set; }

        public ScriptHooksComponent()
        {
            _scripts = new Dictionary<ScriptEvent, string>();
        }

        public string GetScript(ScriptEvent evt)
        {
            if (_scripts.TryGetValue(evt, out string script))
            {
                return script;
            }
            return null;
        }

        public void SetScript(ScriptEvent evt, string script)
        {
            if (!string.IsNullOrEmpty(script))
            {
                _scripts[evt] = script;
            }
            else
            {
                _scripts.Remove(evt);
            }
        }

        public bool HasScript(ScriptEvent evt)
        {
            return _scripts.ContainsKey(evt) && !string.IsNullOrEmpty(_scripts[evt]);
        }

        public void OnAttach() { }
        public void OnDetach() { }
    }

    /// <summary>
    /// Door-specific component.
    /// </summary>
    internal class DoorComponent : IDoorComponent
    {
        public IEntity Owner { get; set; }
        public bool IsOpen { get; set; }
        public bool IsLocked { get; set; }
        public int LockDC { get; set; }
        public bool KeyRequired { get; set; }
        public string KeyName { get; set; }
        public string LinkedTo { get; set; }
        public string LinkedToModule { get; set; }
        public int LinkedToFlags { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }
    }

    /// <summary>
    /// Trigger-specific component.
    /// </summary>
    internal class TriggerComponent : ITriggerComponent
    {
        private readonly List<Vector3> _geometry;

        public IEntity Owner { get; set; }
        public int TriggerType { get; set; }
        public int Faction { get; set; }
        public bool IsTrap { get; set; }
        public bool TrapOneShot { get; set; }
        public int TrapType { get; set; }
        public int DisarmDC { get; set; }
        public int DetectDC { get; set; }
        public bool HasTriggered { get; set; }
        public List<Vector3> Geometry { get { return _geometry; } }

        public TriggerComponent()
        {
            _geometry = new List<Vector3>();
        }

        public bool ContainsPoint(Vector3 point)
        {
            // Simple 2D polygon containment test
            if (_geometry.Count < 3)
            {
                return false;
            }

            bool inside = false;
            int j = _geometry.Count - 1;
            for (int i = 0; i < _geometry.Count; i++)
            {
                if (((_geometry[i].Y < point.Y && _geometry[j].Y >= point.Y) ||
                     (_geometry[j].Y < point.Y && _geometry[i].Y >= point.Y)) &&
                    (_geometry[i].X + (point.Y - _geometry[i].Y) / (_geometry[j].Y - _geometry[i].Y) * (_geometry[j].X - _geometry[i].X) < point.X))
                {
                    inside = !inside;
                }
                j = i;
            }
            return inside;
        }

        public void OnAttach() { }
        public void OnDetach() { }
    }

    /// <summary>
    /// Audio component for sound emitters.
    /// </summary>
    internal class AudioComponent : IAudioComponent
    {
        public IEntity Owner { get; set; }
        public bool IsActive { get; set; }
        public bool IsContinuous { get; set; }
        public bool IsLooping { get; set; }
        public bool IsPositional { get; set; }
        public bool IsRandom { get; set; }
        public float Volume { get; set; }
        public float Interval { get; set; }
        public float MaxDistance { get; set; }
        public float MinDistance { get; set; }
        public float Elevation { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }
    }

    #endregion

    #region Component Interfaces

    /// <summary>
    /// Door-specific component interface.
    /// </summary>
    public interface IDoorComponent : IComponent
    {
        bool IsOpen { get; set; }
        bool IsLocked { get; set; }
        int LockDC { get; set; }
        bool KeyRequired { get; set; }
        string KeyName { get; set; }
        string LinkedTo { get; set; }
        string LinkedToModule { get; set; }
        int LinkedToFlags { get; set; }
    }

    /// <summary>
    /// Trigger-specific component interface.
    /// </summary>
    public interface ITriggerComponent : IComponent
    {
        int TriggerType { get; set; }
        int Faction { get; set; }
        bool IsTrap { get; set; }
        bool TrapOneShot { get; set; }
        int TrapType { get; set; }
        int DisarmDC { get; set; }
        int DetectDC { get; set; }
        bool HasTriggered { get; set; }
        List<Vector3> Geometry { get; }
        bool ContainsPoint(Vector3 point);
    }

    /// <summary>
    /// Audio component interface for sound emitters.
    /// </summary>
    public interface IAudioComponent : IComponent
    {
        bool IsActive { get; set; }
        bool IsContinuous { get; set; }
        bool IsLooping { get; set; }
        bool IsPositional { get; set; }
        bool IsRandom { get; set; }
        float Volume { get; set; }
        float Interval { get; set; }
        float MaxDistance { get; set; }
        float MinDistance { get; set; }
        float Elevation { get; set; }
    }

    #endregion
}

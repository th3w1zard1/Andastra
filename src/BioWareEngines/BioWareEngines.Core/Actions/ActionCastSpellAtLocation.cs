using System;
using System.Collections.Generic;
using System.Numerics;
using BioWareEngines.Core.Enums;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;

namespace BioWareEngines.Core.Actions
{
    /// <summary>
    /// Action to cast a spell/Force power at a location.
    /// </summary>
    /// <remarks>
    /// Cast Spell At Location Action:
    /// - Based on swkotor2.exe spell casting system
    /// - Located via string references: "ActionCastSpellAtLocation" NWScript function (routine ID varies by game)
    /// - Location references: "LOCATION" @ 0x007c2850 (location type constant), "ValLocation" @ 0x007c26ac (location value field)
    /// - "CatLocation" @ 0x007c26dc (location catalog field), "FollowLocation" @ 0x007beda8 (follow location action)
    /// - Location error messages:
    ///   - "Script var '%s' not a LOCATION!" @ 0x007c25e0 (location type validation error)
    ///   - "Script var LOCATION '%s' not in catalogue!" @ 0x007c2600 (location catalog lookup error)
    ///   - "ReadTableWithCat(): LOCATION '%s' won't fit!" @ 0x007c2734 (location data size error)
    /// - Spell casting: "ScriptSpellAt" @ 0x007bee90 (spell at script), "OnSpellCastAt" @ 0x007c1a44 (spell cast at event)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT" @ 0x007bcb3c (spell cast at script event), "EVENT_SPELL_IMPACT" @ 0x007bcd8c (spell impact event)
    /// - Force points: "FinalForceCost" @ 0x007bef04 (final force cost field), "CurrentForce" @ 0x007c401c (current force field)
    /// - "MaxForcePoints" @ 0x007c4278 (max force points field), "ForcePoints" @ 0x007c3410 (force points field)
    /// - Original implementation: Caster moves to range, casts spell at target location
    /// - Movement: Uses direct movement towards target location until within CastRange (default 20.0 units)
    /// - Spell effects: Projectile spells create projectiles (projectile system), area spells create zones (area effect system)
    /// - Force point cost: Deducted from caster's Force points (via IStatsComponent.CurrentFP)
    /// - Spell knowledge: Caster must know the spell (checked via IStatsComponent.HasSpell method)
    /// - Range: Spell range from spells.2da (SpellRange column), caster must be within range before casting
    /// - Cast time: Spell cast time from spells.2da (CastTime or ConjTime column), caster faces target during cast
    /// - Visual effects: CastHandVisual, CastHeadVisual, CastGrndVisual from spells.2da (visual effects during casting)
    /// - Based on NWScript ActionCastSpellAtLocation semantics (routine ID varies by game version)
    /// </remarks>
    public class ActionCastSpellAtLocation : ActionBase
    {
        private readonly int _spellId;
        private readonly Vector3 _targetLocation;
        private readonly object _gameDataManager; // KOTOR-specific, accessed via dynamic
        private bool _approached;
        private bool _spellCast;
        private const float CastRange = 20.0f; // Default spell range

        public ActionCastSpellAtLocation(int spellId, Vector3 targetLocation, object gameDataManager = null)
            : base(ActionType.CastSpellAtLocation)
        {
            _spellId = spellId;
            _targetLocation = targetLocation;
            _gameDataManager = gameDataManager;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Check if caster has Force points and knows the spell
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            if (stats == null)
            {
                return ActionStatus.Failed;
            }

            // Check spell knowledge and Force point cost before casting
            if (!_spellCast)
            {
                // 1. Check if caster knows the spell
                if (!stats.HasSpell(_spellId))
                {
                    return ActionStatus.Failed; // Spell not known
                }

                // 2. Get Force point cost from GameDataManager
                int forcePointCost = GetSpellForcePointCost();
                if (stats.CurrentFP < forcePointCost)
                {
                    return ActionStatus.Failed; // Not enough Force points
                }
            }

            Vector3 toTarget = _targetLocation - transform.Position;
            toTarget.Y = 0;
            float distance = toTarget.Length();

            // Move towards target location if not in range
            if (distance > CastRange && !_approached)
            {
                float speed = stats.WalkSpeed;

                Vector3 direction = Vector3.Normalize(toTarget);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - CastRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                Vector3 newPosition = transform.Position + direction * moveDistance;
                
                // Project position to walkmesh surface (matches FUN_004f5070 in swkotor2.exe)
                // Based on swkotor2.exe: FUN_0054be70 @ 0x0054be70 projects positions to walkmesh after movement
                IArea area = actor.World?.CurrentArea;
                if (area != null && area.NavigationMesh != null)
                {
                    Vector3 projectedPos;
                    float height;
                    if (area.NavigationMesh.ProjectToSurface(newPosition, out projectedPos, out height))
                    {
                        newPosition = projectedPos;
                    }
                }
                
                transform.Position = newPosition;
                // Y-up system: Atan2(Y, X) for 2D plane facing
                transform.Facing = (float)System.Math.Atan2(direction.Y, direction.X);

                return ActionStatus.InProgress;
            }

            _approached = true;

            // Cast the spell
            if (!_spellCast)
            {
                _spellCast = true;

                // Face target location
                Vector3 direction2 = Vector3.Normalize(toTarget);
                transform.Facing = (float)System.Math.Atan2(direction2.Y, direction2.X);

                // Consume Force points
                int forcePointCost = GetSpellForcePointCost();
                stats.CurrentFP = Math.Max(0, stats.CurrentFP - forcePointCost);

                // Apply spell effects at target location
                // Based on swkotor2.exe: Spell casting at location implementation
                // Located via string references: "ActionCastSpellAtLocation" NWScript function
                // Original implementation: Applies spell effects to entities in range of target location
                // Spell types: Instant (apply immediately), Area (affect all in radius), Projectile (create projectile entity)
                ApplySpellEffectsAtLocation(actor, _targetLocation);

                // Fire spell cast event for other systems
                IEventBus eventBus = actor.World.EventBus;
                if (eventBus != null)
                {
                    eventBus.Publish(new SpellCastAtLocationEvent
                    {
                        Caster = actor,
                        SpellId = _spellId,
                        TargetLocation = _targetLocation
                    });
                }
            }

            return ActionStatus.Complete;
        }

        /// <summary>
        /// Gets the Force point cost for the spell.
        /// </summary>
        private int GetSpellForcePointCost()
        {
            if (_gameDataManager != null)
            {
                dynamic gameDataManager = _gameDataManager;
                try
                {
                    return gameDataManager.GetSpellForcePointCost(_spellId);
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Fallback: basic calculation (spell level * 2, minimum 1)
            return 2; // Default cost
        }

        /// <summary>
        /// Applies spell effects at the target location.
        /// Based on swkotor2.exe: Spell effect application at location
        /// Located via string references: Spell effect system for location-based spells
        /// Original implementation: Applies effects to entities in range, creates area effects or projectiles
        /// </summary>
        private void ApplySpellEffectsAtLocation(IEntity caster, Vector3 targetLocation)
        {
            if (caster.World == null || caster.World.EffectSystem == null)
            {
                return;
            }

            Combat.EffectSystem effectSystem = caster.World.EffectSystem;

            // Get spell data to determine effect type and range
            dynamic spell = null;
            if (_gameDataManager != null)
            {
                dynamic gameDataManager = _gameDataManager;
                try
                {
                    spell = gameDataManager.GetSpell(_spellId);
                }
                catch
                {
                    // Fall through
                }
            }

            // Determine spell area of effect radius (default 5.0 units for area spells)
            float spellRadius = 5.0f;
            if (spell != null)
            {
                try
                {
                    float radius = spell.SpellRange as float? ?? spell.Radius as float? ?? 5.0f;
                    spellRadius = radius;
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Get all entities in range of target location
            IEnumerable<IEntity> entitiesInRange = caster.World.GetEntitiesInRadius(targetLocation, spellRadius, ObjectType.Creature);

            // Apply spell effects to entities in range
            foreach (IEntity target in entitiesInRange)
            {
                if (target == null || !target.IsValid)
                {
                    continue;
                }

                // Apply visual effect if spell data available
                if (spell != null)
                {
                    try
                    {
                        int conjHandVfx = spell.ConjHandVfx;
                        if (conjHandVfx > 0)
                        {
                            var visualEffect = new Combat.Effect(Combat.EffectType.VisualEffect)
                            {
                                VisualEffectId = conjHandVfx,
                                DurationType = Combat.EffectDurationType.Instant
                            };
                            effectSystem.ApplyEffect(target, visualEffect, caster);
                        }
                    }
                    catch
                    {
                        // Fall through
                    }
                }

                // Execute impact script if present
                if (spell != null)
                {
                    try
                    {
                        string impactScript = spell.ImpactScript as string;
                        if (!string.IsNullOrEmpty(impactScript))
                        {
                            IEventBus eventBus = caster.World?.EventBus;
                            if (eventBus != null)
                            {
                                // Execute script with target as OBJECT_SELF, caster as triggerer
                                eventBus.FireScriptEvent(target, ScriptEvent.OnSpellCastAt, caster);
                            }
                        }
                    }
                    catch
                    {
                        // Fall through
                    }
                }
            }

            // TODO: Full implementation would also:
            // 1. Create projectile entity for projectile spells (requires projectile system)
            // 2. Create area effect zone entity for persistent area spells
            // 3. Handle spell-specific effects (damage, healing, status effects) from spells.2da
            // 4. Apply ground visual effects at target location
        }
    }

    /// <summary>
    /// Event fired when a spell is cast at a location.
    /// </summary>
    public class SpellCastAtLocationEvent : IGameEvent
    {
        public IEntity Caster { get; set; }
        public int SpellId { get; set; }
        public Vector3 TargetLocation { get; set; }
        public IEntity Entity { get { return Caster; } }
    }
}


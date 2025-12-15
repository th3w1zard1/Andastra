using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Combat;
using Odyssey.Kotor.Components;
using Odyssey.Kotor.Data;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to cast a spell at a target object.
    /// </summary>
    /// <remarks>
    /// Cast Spell Action:
    /// - Based on swkotor2.exe spell casting system
    /// - Located via string references: Spell casting functions handle Force powers and spells
    /// - Original implementation: Moves caster to range, faces target, plays casting animation, applies spell effects
    /// - Spell casting range: ~10.0 units (CastRange)
    /// - Checks Force points, spell knowledge, applies effects via EffectSystem
    /// - Spell effects applied to target based on spell ID (lookup via spells.2da)
    /// - Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 (spell casting logic)
    /// - Force point consumption: GetSpellBaseForcePointCost calculates cost from spell level
    /// </remarks>
    public class ActionCastSpellAtObject : ActionBase
    {
        private readonly int _spellId;
        private readonly uint _targetObjectId;
        private readonly GameDataManager _gameDataManager;
        private bool _approached;
        private bool _castStarted;
        private float _castTimer;
        private const float CastRange = 10.0f; // Spell casting range

        public ActionCastSpellAtObject(int spellId, uint targetObjectId, GameDataManager gameDataManager = null)
            : base(ActionType.CastSpellAtObject)
        {
            _spellId = spellId;
            _targetObjectId = targetObjectId;
            _gameDataManager = gameDataManager;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Get target entity
            IEntity target = actor.World.GetEntity(_targetObjectId);
            if (target == null || !target.IsValid)
            {
                return ActionStatus.Failed;
            }

            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = targetTransform.Position - transform.Position;
            toTarget.Y = 0;
            float distance = toTarget.Length();

            // Move towards target if not in range
            if (distance > CastRange && !_approached)
            {
                IStatsComponent stats = actor.GetComponent<IStatsComponent>();
                float speed = stats != null ? stats.WalkSpeed : 2.5f;

                Vector3 direction = Vector3.Normalize(toTarget);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - CastRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                transform.Position += direction * moveDistance;
                // Y-up system: Atan2(Y, X) for 2D plane facing
                transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

                return ActionStatus.InProgress;
            }

            _approached = true;

            // Check prerequisites before casting
            if (!_castStarted)
            {
                // 1. Check if caster has enough Force points
                IStatsComponent stats = actor.GetComponent<IStatsComponent>();
                if (stats == null)
                {
                    return ActionStatus.Failed;
                }

                int forcePointCost = GetSpellForcePointCost();
                if (stats.CurrentFP < forcePointCost)
                {
                    // Not enough Force points
                    return ActionStatus.Failed;
                }

                // 2. Check if spell is known
                CreatureComponent creature = actor.GetComponent<CreatureComponent>();
                if (creature != null && !creature.KnownPowers.Contains(_spellId))
                {
                    // Spell not known
                    return ActionStatus.Failed;
                }

                // 3. Start casting (play animation would go here)
                _castStarted = true;
                _castTimer = 0f;

                // Get cast time from spell data
                float castTime = GetSpellCastTime();
                if (castTime <= 0f)
                {
                    castTime = 1.0f; // Default cast time
                }

                // Consume Force points immediately
                stats.CurrentFP = Math.Max(0, stats.CurrentFP - forcePointCost);
            }

            // Wait for cast time
            _castTimer += deltaTime;
            float castTime = GetSpellCastTime();
            if (castTime <= 0f)
            {
                castTime = 1.0f;
            }

            if (_castTimer < castTime)
            {
                // Still casting - face target
                if (distance > 0.1f)
                {
                    Vector3 direction = Vector3.Normalize(toTarget);
                    transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
                }
                return ActionStatus.InProgress;
            }

            // Cast complete - apply spell effects
            ApplySpellEffects(actor, target);

            return ActionStatus.Complete;
        }

        /// <summary>
        /// Gets the Force point cost for the spell.
        /// </summary>
        private int GetSpellForcePointCost()
        {
            if (_gameDataManager != null)
            {
                return _gameDataManager.GetSpellForcePointCost(_spellId);
            }

            // Fallback: basic calculation (spell level * 2, minimum 1)
            return 2; // Default cost
        }

        /// <summary>
        /// Gets the cast time for the spell.
        /// </summary>
        private float GetSpellCastTime()
        {
            if (_gameDataManager != null)
            {
                SpellData spell = _gameDataManager.GetSpell(_spellId);
                if (spell != null)
                {
                    return spell.CastTime > 0f ? spell.CastTime : spell.ConjTime;
                }
            }

            return 1.0f; // Default cast time
        }

        /// <summary>
        /// Applies spell effects to the target.
        /// </summary>
        /// <remarks>
        /// Spell Effect Application:
        /// - Based on swkotor2.exe spell effect system
        /// - Original implementation: Effects created from spell ID, applied via EffectSystem
        /// - Spell effects can be: damage, healing, status effects, visual effects
        /// - Impact scripts (impactscript column) can also be executed for custom effects
        /// - For now, we apply a basic effect - full implementation would resolve effects from spell data
        /// </remarks>
        private void ApplySpellEffects(IEntity caster, IEntity target)
        {
            if (caster.World == null || caster.World.EffectSystem == null)
            {
                return;
            }

            EffectSystem effectSystem = caster.World.EffectSystem;

            // Get spell data to determine effect type
            SpellData spell = null;
            if (_gameDataManager != null)
            {
                spell = _gameDataManager.GetSpell(_spellId);
            }

            // For now, apply a basic visual effect
            // Full implementation would:
            // 1. Look up spell effects from spells.2da or spell scripts
            // 2. Create appropriate Effect objects based on spell type
            // 3. Apply effects via EffectSystem
            // 4. Execute impact script if present

            // Basic effect: Apply visual effect if spell data available
            if (spell != null && spell.ConjHandVfx > 0)
            {
                var visualEffect = new Effect(EffectType.VisualEffect)
                {
                    VisualEffectId = spell.ConjHandVfx,
                    DurationType = EffectDurationType.Instant
                };
                effectSystem.ApplyEffect(target, visualEffect, caster);
            }

            // Execute impact script if present
            if (spell != null && !string.IsNullOrEmpty(spell.ImpactScript))
            {
                // TODO: Execute impact script via script system
                // This would require access to script execution context
            }
        }
    }
}


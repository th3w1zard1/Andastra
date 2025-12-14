using System;
using System.Collections.Generic;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Combat
{
    /// <summary>
    /// Effect type identifier.
    /// </summary>
    public enum EffectType
    {
        // Attribute modifiers
        AbilityIncrease,
        AbilityDecrease,
        AttackIncrease,
        AttackDecrease,
        DamageIncrease,
        DamageDecrease,
        ACIncrease,
        ACDecrease,
        SaveIncrease,
        SaveDecrease,

        // Status effects
        Paralysis,
        Stun,
        Sleep,
        Confusion,
        Frightened,
        Dazed,
        Entangle,
        Slow,
        Haste,
        Invisibility,

        // Damage effects
        DamageResistance,
        DamageImmunity,
        DamageReduction,
        Regeneration,
        TemporaryHitpoints,

        // Force effects
        ForceResistance,
        ForceSuppression,
        ForcePush,
        ForceChoke,
        MindAffecting,

        // Visual effects
        VisualEffect,
        Beam,

        // Other
        Polymorph,
        Sanctuary,
        TrueSeeing,
        Charmed,
        Dominated,
        Death,
        Knockdown,
        Heal,
        Poison,
        Disease
    }

    /// <summary>
    /// Duration type for effects.
    /// </summary>
    public enum EffectDurationType
    {
        /// <summary>
        /// Effect lasts until removed.
        /// </summary>
        Permanent,

        /// <summary>
        /// Effect lasts for a specific number of rounds.
        /// </summary>
        Temporary,

        /// <summary>
        /// Effect is instantaneous (applies once).
        /// </summary>
        Instant
    }

    /// <summary>
    /// Manages active effects on entities.
    /// </summary>
    public class EffectSystem
    {
        private readonly Dictionary<uint, List<ActiveEffect>> _entityEffects;
        private readonly IWorld _world;

        public EffectSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _entityEffects = new Dictionary<uint, List<ActiveEffect>>();
        }

        /// <summary>
        /// Applies an effect to an entity.
        /// </summary>
        public void ApplyEffect(IEntity target, Effect effect, IEntity creator = null)
        {
            if (target == null || effect == null)
            {
                return;
            }

            // Handle instant effects immediately
            if (effect.DurationType == EffectDurationType.Instant)
            {
                ApplyInstantEffect(target, effect, creator);
                return;
            }

            // Get or create effect list for entity
            List<ActiveEffect> effects;
            if (!_entityEffects.TryGetValue(target.ObjectId, out effects))
            {
                effects = new List<ActiveEffect>();
                _entityEffects[target.ObjectId] = effects;
            }

            // Create active effect
            var activeEffect = new ActiveEffect(effect, target, creator);
            effects.Add(activeEffect);

            // Apply initial effect
            ApplyEffectModifiers(target, effect, true);
        }

        /// <summary>
        /// Removes an effect from an entity.
        /// </summary>
        public void RemoveEffect(IEntity target, ActiveEffect effect)
        {
            if (target == null || effect == null)
            {
                return;
            }

            List<ActiveEffect> effects;
            if (_entityEffects.TryGetValue(target.ObjectId, out effects))
            {
                if (effects.Remove(effect))
                {
                    // Remove effect modifiers
                    ApplyEffectModifiers(target, effect.Effect, false);
                }
            }
        }

        /// <summary>
        /// Removes all effects of a specific type from an entity.
        /// </summary>
        public void RemoveEffectsByType(IEntity target, EffectType type)
        {
            if (target == null)
            {
                return;
            }

            List<ActiveEffect> effects;
            if (_entityEffects.TryGetValue(target.ObjectId, out effects))
            {
                var toRemove = new List<ActiveEffect>();
                foreach (var effect in effects)
                {
                    if (effect.Effect.Type == type)
                    {
                        toRemove.Add(effect);
                    }
                }

                foreach (var effect in toRemove)
                {
                    effects.Remove(effect);
                    ApplyEffectModifiers(target, effect.Effect, false);
                }
            }
        }

        /// <summary>
        /// Removes all effects from an entity.
        /// </summary>
        public void RemoveAllEffects(IEntity target)
        {
            if (target == null)
            {
                return;
            }

            List<ActiveEffect> effects;
            if (_entityEffects.TryGetValue(target.ObjectId, out effects))
            {
                foreach (var effect in effects)
                {
                    ApplyEffectModifiers(target, effect.Effect, false);
                }
                effects.Clear();
            }
        }

        /// <summary>
        /// Gets all active effects on an entity.
        /// </summary>
        public IEnumerable<ActiveEffect> GetEffects(IEntity entity)
        {
            if (entity == null)
            {
                yield break;
            }

            List<ActiveEffect> effects;
            if (_entityEffects.TryGetValue(entity.ObjectId, out effects))
            {
                foreach (var effect in effects)
                {
                    yield return effect;
                }
            }
        }

        /// <summary>
        /// Checks if an entity has an effect of a specific type.
        /// </summary>
        public bool HasEffect(IEntity entity, EffectType type)
        {
            if (entity == null)
            {
                return false;
            }

            List<ActiveEffect> effects;
            if (_entityEffects.TryGetValue(entity.ObjectId, out effects))
            {
                foreach (var effect in effects)
                {
                    if (effect.Effect.Type == type)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Updates all effects (called each combat round).
        /// </summary>
        public void UpdateRound()
        {
            var toRemove = new List<KeyValuePair<uint, ActiveEffect>>();

            foreach (var kvp in _entityEffects)
            {
                foreach (var effect in kvp.Value)
                {
                    if (effect.Effect.DurationType == EffectDurationType.Temporary)
                    {
                        effect.RemainingRounds--;

                        if (effect.RemainingRounds <= 0)
                        {
                            toRemove.Add(new KeyValuePair<uint, ActiveEffect>(kvp.Key, effect));
                        }
                    }
                }
            }

            // Remove expired effects
            foreach (var removal in toRemove)
            {
                var entity = _world.GetEntity(removal.Key);
                if (entity != null)
                {
                    RemoveEffect(entity, removal.Value);
                }
            }
        }

        private void ApplyInstantEffect(IEntity target, Effect effect, IEntity creator)
        {
            switch (effect.Type)
            {
                case EffectType.Heal:
                    var stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
                    if (stats != null)
                    {
                        stats.CurrentHP = Math.Min(stats.CurrentHP + effect.Amount, stats.MaxHP);
                    }
                    break;

                case EffectType.Death:
                    var deathStats = target.GetComponent<Interfaces.Components.IStatsComponent>();
                    if (deathStats != null)
                    {
                        deathStats.CurrentHP = 0;
                    }
                    break;

                // Other instant effects...
            }
        }

        private void ApplyEffectModifiers(IEntity target, Effect effect, bool apply)
        {
            // Apply or remove stat modifiers based on effect type
            // This would integrate with the stats component
            // Implementation depends on stat system details
        }
    }

    /// <summary>
    /// Defines an effect that can be applied to entities.
    /// </summary>
    public class Effect
    {
        /// <summary>
        /// Type of effect.
        /// </summary>
        public EffectType Type { get; set; }

        /// <summary>
        /// Duration type.
        /// </summary>
        public EffectDurationType DurationType { get; set; }

        /// <summary>
        /// Duration in rounds (for temporary effects).
        /// </summary>
        public int DurationRounds { get; set; }

        /// <summary>
        /// Effect amount/magnitude.
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Secondary amount (e.g., ability type for ability modifiers).
        /// </summary>
        public int SubType { get; set; }

        /// <summary>
        /// Visual effect to display.
        /// </summary>
        public int VisualEffectId { get; set; }

        /// <summary>
        /// Whether this effect is supernatural (vs extraordinary).
        /// </summary>
        public bool IsSupernatural { get; set; }

        public Effect(EffectType type)
        {
            Type = type;
            DurationType = EffectDurationType.Temporary;
            DurationRounds = 1;
        }

        /// <summary>
        /// Creates a damage resistance effect.
        /// </summary>
        public static Effect DamageResistance(DamageType damageType, int amount, int rounds = 0)
        {
            var effect = new Effect(EffectType.DamageResistance);
            effect.SubType = (int)damageType;
            effect.Amount = amount;
            effect.DurationRounds = rounds;
            effect.DurationType = rounds > 0 ? EffectDurationType.Temporary : EffectDurationType.Permanent;
            return effect;
        }

        /// <summary>
        /// Creates a heal effect.
        /// </summary>
        public static Effect Heal(int amount)
        {
            var effect = new Effect(EffectType.Heal);
            effect.Amount = amount;
            effect.DurationType = EffectDurationType.Instant;
            return effect;
        }

        /// <summary>
        /// Creates a stun effect.
        /// </summary>
        public static Effect Stun(int rounds)
        {
            var effect = new Effect(EffectType.Stun);
            effect.DurationRounds = rounds;
            return effect;
        }

        /// <summary>
        /// Creates an ability modifier effect.
        /// </summary>
        public static Effect AbilityModifier(Enums.Ability ability, int amount, int rounds = 0)
        {
            var type = amount >= 0 ? EffectType.AbilityIncrease : EffectType.AbilityDecrease;
            var effect = new Effect(type);
            effect.SubType = (int)ability;
            effect.Amount = Math.Abs(amount);
            effect.DurationRounds = rounds;
            effect.DurationType = rounds > 0 ? EffectDurationType.Temporary : EffectDurationType.Permanent;
            return effect;
        }
    }

    /// <summary>
    /// An active effect on an entity.
    /// </summary>
    public class ActiveEffect
    {
        /// <summary>
        /// The effect definition.
        /// </summary>
        public Effect Effect { get; }

        /// <summary>
        /// The entity the effect is on.
        /// </summary>
        public IEntity Target { get; }

        /// <summary>
        /// The entity that created the effect.
        /// </summary>
        public IEntity Creator { get; }

        /// <summary>
        /// Remaining rounds for temporary effects.
        /// </summary>
        public int RemainingRounds { get; set; }

        /// <summary>
        /// When the effect was applied.
        /// </summary>
        public float AppliedAt { get; }

        public ActiveEffect(Effect effect, IEntity target, IEntity creator)
        {
            Effect = effect;
            Target = target;
            Creator = creator;
            RemainingRounds = effect.DurationRounds;
            AppliedAt = 0f; // Would be set from game time
        }
    }
}

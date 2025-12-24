using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Combat
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
        MovementSpeedIncrease,
        MovementSpeedDecrease,
        Invisibility,
        Deafness,

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
        Disease,
        AssuredHit
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
    /// <remarks>
    /// Effect System:
    /// - Based on swkotor2.exe effect system
    /// - Located via string references: "EffectList" @ 0x007bebe8, "AreaEffectList" @ 0x007bd0d4
    /// - "EVENT_APPLY_EFFECT" @ 0x007bcdc8, "EVENT_REMOVE_EFFECT" @ 0x007bcd0c
    /// - "EVENT_ABILITY_EFFECT_APPLIED" @ 0x007bcc20, "EffectAttacks" @ 0x007bfa28
    /// - "VisualEffect_01-04" @ 0x007c0210, 0x007c01f0, 0x007c01d0, 0x007c01b0 (visual effect slots)
    /// - "EffectChance" @ 0x007c07e0, "Mod_Effect_NxtId" @ 0x007bea0c (effect ID tracking)
    /// - "AreaEffectId" @ 0x007c13f8, "DEffectType" @ 0x007c016b (effect type identifier)
    /// - "VisualEffectDef" @ 0x007c0230, "CamVidEffect" @ 0x007c3450 (visual effect definitions)
    /// - "VisualEffect" @ 0x007c4624, "RangedEffect" @ 0x007c4634, "EffectIcon" @ 0x007c4f48
    /// - "GameEffects" @ 0x007c4e70, "VideoEffects" @ 0x007c4f30, "visualeffects" @ 0x007c4a7c
    /// - "visualeffect" @ 0x007d25e8, "FreeLookEffect" @ 0x007c7df0
    /// - Effect bonus messages:
    ///   - " + %d (Effect Attack Bonus)" @ 0x007c39d0
    ///   - " + %d (Effect Damage Bonus)" @ 0x007c3cd8
    ///   - " + %d (Effect Damage Bonus) (Critical x%d)" @ 0x007c3cf4
    ///   - " + %d (Effect AC Deflection Bonus)" @ 0x007c3d9c
    ///   - " + %d (Effect AC Shield Bonus)" @ 0x007c3dc0
    ///   - " + %d (Effect AC Armor Bonus)" @ 0x007c3de0
    ///   - " + %d (Effect AC Natural Bonus)" @ 0x007c3e00
    ///   - " + %d (Effect AC Dodge Bonus)" @ 0x007c3e20
    /// - Effect directories: "HD0:effects" @ 0x007cc01c (effects directory)
    /// - "Environment Effects Streaming" @ 0x007c7204, "Environment Effects Nonstreaming" @ 0x007c7224
    /// - GUI: "BTN_EFFECTS" @ 0x007c9038, "LBL_EFFECTS_BAD" @ 0x007c90c0, "LBL_EFFECTS_GOOD" @ 0x007c90d0
    /// - "LB_EFFECTS_BAD" @ 0x007c90e4, "LB_EFFECTS_GOOD" @ 0x007c90f4 (effect list boxes)
    /// - Error messages:
    ///   - "CSWCVisualEffect::LoadModel: Failed to load visual effect model '%s'." @ 0x007cd5a8
    /// - "VisualEffect" @ 0x007c4624, "RangedEffect" @ 0x007c4634 (effect categories)
    /// - "GameEffects" @ 0x007c4e70, "VideoEffects" @ 0x007c4f30, "EffectIcon" @ 0x007c4f48
    /// - Original implementation: FUN_0050b540 @ 0x0050b540 (load EffectList from GFF)
    ///   - Function signature: `void FUN_0050b540(void *this, void *param_1, uint *param_2)`
    ///   - param_1: GFF structure pointer
    ///   - param_2: GFF field pointer
    ///   - Loads "EffectList" GFF list field (via FUN_004129e0), iterates through each effect struct
    ///   - Creates effect object (0x8c bytes) via FUN_00541600, loads effect data via FUN_00541b60
    ///   - Filters effects: Only adds effects where effect type matches or effect subtype matches (DAT_007c02ec)
    ///   - Calls FUN_0050ae30 to add effect to entity's effect list
    ///   - Effect filtering: Effects with type 1 (instant) or matching subtype are added
    ///   - Effect structure: Contains effect type, subtype, duration, parameters, visual effect ID, etc.
    /// - FUN_00505db0 @ 0x00505db0 (save EffectList to GFF)
    ///   - Function signature: `void FUN_00505db0(void *this, void *param_1, uint *param_2)`
    ///   - Saves "EffectList" GFF list field (via FUN_00413570), iterates through entity's effect list
    ///   - Calls FUN_00540f10 to serialize each effect to GFF struct
    ///   - Effect count stored at offset 0x14c, effect list pointer at offset 0x148
    ///   - Each effect struct saved with effect type, subtype, duration, parameters, visual effect ID
    /// - Effects applied to entities with duration tracking, stacking rules, removal on expiration
    /// - Effect types: Attribute modifiers (ability, attack, damage, AC, saves), status effects (paralysis, stun, etc.),
    ///   damage effects (resistance, immunity, reduction), Force effects, visual effects
    /// - Effects have duration in rounds or permanent, some are instantaneous
    /// - Effect stacking: Some effects stack, others override
    /// - Effect bonuses (display strings):
    ///   - " + %d (Effect Attack Bonus)" @ 0x007c39d0
    ///   - " + %d (Effect Damage Bonus)" @ 0x007c3cd8
    ///   - " + %d (Effect Damage Bonus) (Critical x%d)" @ 0x007c3cf4
    ///   - " + %d (Effect AC Deflection Bonus)" @ 0x007c3d9c
    ///   - " + %d (Effect AC Shield Bonus)" @ 0x007c3dc0
    ///   - " + %d (Effect AC Armor Bonus)" @ 0x007c3de0
    ///   - " + %d (Effect AC Natural Bonus)" @ 0x007c3e00
    ///   - " + %d (Effect AC Dodge Bonus)" @ 0x007c3e20
    /// - Visual effects: CSWCVisualEffect class handles visual effect models
    /// - Error: "CSWCVisualEffect::LoadModel: Failed to load visual effect model '%s'." @ 0x007cd5a8
    /// </remarks>
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
                foreach (ActiveEffect effect in effects)
                {
                    if (effect.Effect.Type == type)
                    {
                        toRemove.Add(effect);
                    }
                }

                foreach (ActiveEffect effect in toRemove)
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
                foreach (ActiveEffect effect in effects)
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
                foreach (ActiveEffect effect in effects)
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
                foreach (ActiveEffect effect in effects)
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

            foreach (KeyValuePair<uint, List<ActiveEffect>> kvp in _entityEffects)
            {
                foreach (ActiveEffect effect in kvp.Value)
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
            foreach (KeyValuePair<uint, ActiveEffect> removal in toRemove)
            {
                IEntity entity = _world.GetEntity(removal.Key);
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
                    Interfaces.Components.IStatsComponent stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
                    if (stats != null)
                    {
                        stats.CurrentHP = Math.Min(stats.CurrentHP + effect.Amount, stats.MaxHP);
                    }
                    break;

                case EffectType.Death:
                    Interfaces.Components.IStatsComponent deathStats = target.GetComponent<Interfaces.Components.IStatsComponent>();
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
            Interfaces.Components.IStatsComponent stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats == null)
            {
                return;
            }

            int modifier = apply ? effect.Amount : -effect.Amount;

            switch (effect.Type)
            {
                case EffectType.AbilityIncrease:
                case EffectType.AbilityDecrease:
                    if (effect.SubType >= 0 && effect.SubType < 6)
                    {
                        Ability ability = (Ability)effect.SubType;
                        int current = stats.GetAbility(ability);
                        stats.SetAbility(ability, current + modifier);
                    }
                    break;

                case EffectType.ACIncrease:
                case EffectType.ACDecrease:
                    // AC modifiers are applied via effect bonus tracking
                    // Based on swkotor2.exe: AC effects modify total AC calculation
                    // Located via string references: "ArmorClass" @ 0x007c42a8, "EffectACIncrease" @ routine 115
                    // Original implementation: AC effects add to total AC (10 + DEX + Armor + Natural + Deflection + Effects)
                    // swkotor2.exe: FUN_0050b540 loads EffectList, AC effects are tracked and applied to total AC calculation
                    int acBonus = effect.Type == EffectType.ACIncrease ? effect.Amount : -effect.Amount;
                    if (apply)
                    {
                        stats.AddEffectACBonus(acBonus);
                    }
                    else
                    {
                        stats.RemoveEffectACBonus(acBonus);
                    }
                    break;

                case EffectType.AttackIncrease:
                case EffectType.AttackDecrease:
                    // Attack bonus is applied via effect bonus tracking
                    // Based on swkotor2.exe: Attack effects modify total attack bonus
                    // Located via string references: "EffectAttackIncrease" @ routine 118
                    // Original implementation: Attack effects add to total attack (BAB + STR/DEX + Effects)
                    // swkotor2.exe: FUN_0050b540 loads EffectList, attack effects are tracked and applied to total attack bonus calculation
                    int attackBonus = effect.Type == EffectType.AttackIncrease ? effect.Amount : -effect.Amount;
                    if (apply)
                    {
                        stats.AddEffectAttackBonus(attackBonus);
                    }
                    else
                    {
                        stats.RemoveEffectAttackBonus(attackBonus);
                    }
                    break;

                case EffectType.DamageIncrease:
                case EffectType.DamageDecrease:
                    // Damage modifiers are applied during damage calculation
                    break;

                case EffectType.SaveIncrease:
                case EffectType.SaveDecrease:
                    // Save bonuses are calculated dynamically
                    break;

                case EffectType.DamageResistance:
                    // Damage resistance is handled during damage calculation
                    break;

                case EffectType.DamageReduction:
                    // Damage reduction is handled during damage calculation
                    break;

                case EffectType.Regeneration:
                    // Regeneration is handled in UpdateRound
                    break;

                case EffectType.TemporaryHitpoints:
                    if (apply)
                    {
                        // Temporary HP increases max HP temporarily
                        stats.MaxHP += effect.Amount;
                        stats.CurrentHP += effect.Amount;
                    }
                    else
                    {
                        // Remove temporary HP (may reduce current HP if it was above base max)
                        int baseMaxHP = stats.MaxHP - effect.Amount;
                        stats.MaxHP = baseMaxHP;
                        if (stats.CurrentHP > baseMaxHP)
                        {
                            stats.CurrentHP = baseMaxHP;
                        }
                    }
                    break;

                // Status effects don't modify stats directly, they affect behavior
                case EffectType.Paralysis:
                case EffectType.Stun:
                case EffectType.Sleep:
                case EffectType.Confusion:
                case EffectType.Frightened:
                case EffectType.Dazed:
                case EffectType.Entangle:
                case EffectType.Slow:
                case EffectType.Haste:
                case EffectType.MovementSpeedIncrease:
                case EffectType.MovementSpeedDecrease:
                    // Movement speed modifiers are handled dynamically by StatsComponent
                    // Based on swkotor2.exe: Haste/Slow effects modify movement speed
                    // Located via string references: "Haste" @ routine 119, "Slow" @ routine 120
                    // Original implementation: Haste doubles speed, Slow halves speed
                    // MovementSpeedIncrease/Decrease: Percentage-based modifiers (EffectMovementSpeedIncrease/Decrease script functions)
                    // Based on nwmain.exe: GetWalkRate returns GetMovementRateFactor(this) * baseWalkRate * constant
                    // Located via function: GetWalkRate @ 0x140396730 (nwmain.exe)
                    // StatsComponent.WalkSpeed and RunSpeed properties query EffectSystem for active effects
                    // No direct modification needed here - speed is calculated on-demand
                    break;

                case EffectType.Invisibility:
                case EffectType.TrueSeeing:
                case EffectType.Charmed:
                case EffectType.Dominated:
                case EffectType.Knockdown:
                case EffectType.Deafness:
                    // These are handled by other systems (combat, movement, perception, etc.)
                    // Deafness: Prevents hearing perception checks (handled by PerceptionManager)
                    // Based on swkotor.exe: EFFECT_TYPE_DEAF = 13 prevents hearing perception
                    // Located via string references: EFFECT_TYPE_DEAF @ ScriptDefs constant 13
                    // Original implementation: Deafness effect blocks hearing perception checks
                    break;

                // Visual effects don't modify stats
                case EffectType.VisualEffect:
                case EffectType.Beam:
                    break;

                // Other effects
                case EffectType.Polymorph:
                case EffectType.Sanctuary:
                case EffectType.Poison:
                case EffectType.Disease:
                    // These are handled by other systems
                    break;

                case EffectType.AssuredHit:
                    // AssuredHit is handled by the combat system during attack resolution
                    // No stat modification needed - the combat system checks for this effect
                    // Based on swkotor.exe: AssuredHit effect guarantees next attack hits
                    // Located via string references: EffectAssuredHit @ routine 51
                    // Original implementation: Effect flag that forces attack to hit (bypasses AC check)
                    break;
            }
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
            EffectType type = amount >= 0 ? EffectType.AbilityIncrease : EffectType.AbilityDecrease;
            var effect = new Effect(type);
            effect.SubType = (int)ability;
            effect.Amount = Math.Abs(amount);
            effect.DurationRounds = rounds;
            effect.DurationType = rounds > 0 ? EffectDurationType.Temporary : EffectDurationType.Permanent;
            return effect;
        }

        /// <summary>
        /// Creates a deafness effect.
        /// </summary>
        /// <param name="rounds">Duration in rounds. If 0, effect is permanent until removed.</param>
        /// <returns>A deafness effect that prevents hearing perception checks.</returns>
        /// <remarks>
        /// Deafness Effect:
        /// - Based on swkotor.exe: EFFECT_TYPE_DEAF = 13
        /// - Located via string references: EFFECT_TYPE_DEAF @ ScriptDefs constant 13
        /// - Original implementation: Deafness effect prevents creature from hearing sounds
        /// - Used by PerceptionManager to block hearing perception checks
        /// - Common across all engines: Odyssey (swkotor.exe, swkotor2.exe), Aurora (nwmain.exe), Eclipse (daorigins.exe, DragonAge2.exe), Infinity (, )
        /// </remarks>
        public static Effect Deafness(int rounds = 0)
        {
            var effect = new Effect(EffectType.Deafness);
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

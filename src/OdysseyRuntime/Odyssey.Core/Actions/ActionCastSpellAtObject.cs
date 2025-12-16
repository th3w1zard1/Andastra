using System;
using System.Numerics;
using BioWareEngines.Core.Enums;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;
using BioWareEngines.Core.Combat;

namespace BioWareEngines.Core.Actions
{
    /// <summary>
    /// Action to cast a spell at a target object.
    /// </summary>
    /// <remarks>
    /// Cast Spell Action:
    /// - Based on swkotor2.exe spell casting system
    /// - Located via string references: "ScriptSpellAt" @ 0x007bee90, "OnSpellCastAt" @ 0x007c1a44
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT" @ 0x007bcb3c, "EVENT_SPELL_IMPACT" @ 0x007bcd8c
    /// - "EVENT_ITEM_ON_HIT_SPELL_IMPACT" @ 0x007bcc8c (item spell impact event)
    /// - Spell data fields: "SpellId" @ 0x007bef68, "SpellLevel" @ 0x007c13c8, "SpellSaveDC" @ 0x007c13d4
    /// - "SpellCaster" @ 0x007c2ad0, "SpellCasterLevel" @ 0x007c3eb4, "SpellFlags" @ 0x007c3ec8
    /// - "SpellDesc" @ 0x007c33f8, "MasterSpell" @ 0x007c341c, "SPELLS" @ 0x007c3438
    /// - Spell tables: "SpellKnownTable" @ 0x007c2b08, "SpellGainTable" @ 0x007c2b18
    /// - "SpellsPerDayList" @ 0x007c3f74, "NumSpellsLeft" @ 0x007c3f64, "NumSpellLevels" @ 0x007c47e8
    /// - "SpellLevel%d" @ 0x007c4888 (spell level format string), "Spells" @ 0x007c4ed0, "spells" @ 0x007c494c
    /// - Spell casting: "SpellCastRound" @ 0x007bfb60, "ArcaneSpellFail" @ 0x007c2df8, "MinSpellLvl" @ 0x007c2eb4
    /// - Caster fields: "CasterLevel" @ 0x007beb4c, "CasterId" @ 0x007bef5c
    /// - Cast visuals: "CastGrndVisual" @ 0x007c3240, "CastHandVisual" @ 0x007c325c, "CastHeadVisual" @ 0x007c326c
    /// - "CastSound" @ 0x007c3250, "CastAnim" @ 0x007c32dc, "CastTime" @ 0x007c32e8
    /// - "castgroundvisual" @ 0x007cdbb8, "castvisual" @ 0x007cdbd4, "cast01" @ 0x007cdbcc
    /// - Force points: "ForcePoints" @ 0x007c3410, "CurrentForce" @ 0x007c401c, "MaxForcePoints" @ 0x007c4278
    /// - "FinalForceCost" @ 0x007bef04, "BonusForcePoints" @ 0x007bf640, "LvlStatForce" @ 0x007c3f28
    /// - "ForceAdjust" @ 0x007c4e64, "ForceResistance" @ 0x007c2e08, "ForceDie" @ 0x007c2b68
    /// - Force alignment: "FORCEPASSIVE" @ 0x007c31e0, "FORCEHOSTILE" @ 0x007c31f0, "FORCEFRIENDLY" @ 0x007c3210
    /// - "FORCEPRIORITY" @ 0x007c3200, "ForceRating" @ 0x007bd45c, "ForceShields" @ 0x007c4f0c
    /// - Events: "EVENT_FORCED_ACTION" @ 0x007bccac, "EVENT_BROADCAST_SAFE_PROJECTILE" @ 0x007bcc58, "EVENT_BROADCAST_AOO" @ 0x007bcc78
    /// - Error messages:
    ///   - "CSWClass::LoadSpellGainTable: Can't load ClassPowerGain" @ 0x007c47f8
    ///   - "CSWClass::LoadSpellGainTable: Can't load CLS_SPGN_JEDI" @ 0x007c4840
    ///   - "CSWClass::LoadSpellKnownTable: Can't load" @ 0x007c4898
    ///   - "CSWClass::LoadSpellsTable: Can't load spells.2da" @ 0x007c4918
    /// - Debug: "        SpellsPerDayLeft: " @ 0x007cafe4, "KnownSpells: " @ 0x007cb010
    /// - Script hooks: "k_def_spellat01" @ 0x007c7ed4 (spell defense script example)
    /// - Visual effect errors:
    ///   - "CSWCAnimBase::LoadModel(): The headconjure dummy has an orientation....It shouldn't!!  The %s model needs to be fixed or else the spell visuals will not be correct." @ 0x007ce278
    ///   - "CSWCAnimBase::LoadModel(): The handconjure dummy has an orientation....It shouldn't!!  The %s model needs to be fixed or else the spell visuals will not be correct." @ 0x007ce320
    /// - GUI: "LBL_FORCE" @ 0x007cfc30, "LBL_FORCE_STAT" @ 0x007cfc5c, "LBL_FORCEMASTERY" @ 0x007cfd20
    /// - "PB_FORCE%d" @ 0x007ccf6c (force progress bar format), "ForceDisplay" @ 0x007d2e70
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
        private readonly object _gameDataManager; // KOTOR-specific, accessed via dynamic
        private bool _approached;
        private bool _castStarted;
        private float _castTimer;
        private const float CastRange = 10.0f; // Spell casting range

        public ActionCastSpellAtObject(int spellId, uint targetObjectId, object gameDataManager = null)
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
                if (!stats.HasSpell(_spellId))
                {
                    return ActionStatus.Failed; // Spell not known
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
            float requiredCastTime = GetSpellCastTime();
            if (requiredCastTime <= 0f)
            {
                requiredCastTime = 1.0f;
            }

            if (_castTimer < requiredCastTime)
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
        /// Gets the cast time for the spell.
        /// </summary>
        private float GetSpellCastTime()
        {
            if (_gameDataManager != null)
            {
                dynamic gameDataManager = _gameDataManager;
                try
                {
                    dynamic spell = gameDataManager.GetSpell(_spellId);
                    if (spell != null)
                    {
                        float castTime = spell.CastTime;
                        if (castTime > 0f)
                        {
                            return castTime;
                        }
                        float conjTime = spell.ConjTime;
                        if (conjTime > 0f)
                        {
                            return conjTime;
                        }
                    }
                }
                catch
                {
                    // Fall through to default
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
            // Using dynamic to avoid dependency on Odyssey.Kotor.Data
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

            // For now, apply a basic visual effect
            // Full implementation would:
            // 1. Look up spell effects from spells.2da or spell scripts
            // 2. Create appropriate Effect objects based on spell type
            // 3. Apply effects via EffectSystem
            // 4. Execute impact script if present

            // Basic effect: Apply visual effect if spell data available
            if (spell != null)
            {
                try
                {
                    int conjHandVfx = spell.ConjHandVfx;
                    if (conjHandVfx > 0)
                    {
                        var visualEffect = new Effect(EffectType.VisualEffect)
                        {
                            VisualEffectId = conjHandVfx,
                            DurationType = EffectDurationType.Instant
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
                        // Fire impact script event via world event bus
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
    }
}


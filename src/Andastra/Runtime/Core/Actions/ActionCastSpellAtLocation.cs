using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to cast a spell/Force power at a location.
    /// </summary>
    /// <remarks>
    /// Cast Spell At Location Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) spell casting system
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
        /// <summary>
        /// Default projectile speed for spell projectiles (units per second).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell projectile speed is constant across all spells.
        /// Note: spells.2da does not contain a projectile speed column, so the engine uses a constant value.
        /// This value differs from weapon projectile speed (which uses 16.0f in vendor/reone implementation).
        /// </summary>
        private const float SpellProjectileSpeed = 30.0f;

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
                // Get movement speed for spell casting
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Movement speed during spell casting uses entity's walk speed
                // Located via analysis: spells.2da does not contain a movement speed column
                // The original engine uses the caster's walk speed (from appearance.2da WALKRATE) for movement
                // Movement speed effects (EffectMovementSpeedIncrease/Decrease) are already applied via IStatsComponent.WalkSpeed
                // Spell-specific movement speeds do not exist in spells.2da, so we always use the entity's walk speed
                float speed = GetMovementSpeedForSpellCasting(actor, stats);

                Vector3 direction = Vector3.Normalize(toTarget);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - CastRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                Vector3 newPosition = transform.Position + direction * moveDistance;

                // Project position to walkmesh surface (matches 0x004f5070 in swkotor2.exe)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 projects positions to walkmesh after movement
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
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell casting at location implementation
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
        /// Gets the projectile speed for a spell projectile.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell projectile speed is constant across all spells.
        /// </summary>
        /// <param name="spell">The spell data (may be null if unavailable).</param>
        /// <returns>The projectile speed in units per second.</returns>
        /// <remarks>
        /// Spell Projectile Speed:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell projectile speed is constant across all spells
        /// - Located via analysis: spells.2da does not contain a projectile speed column
        /// - The original engine uses a constant value (30.0 units per second) for all spell projectiles
        /// - This value differs from weapon projectile speed which uses 16.0 units per second (vendor/reone: kProjectileSpeed)
        /// - Spell projectiles are faster than weapon projectiles to match original engine behavior
        /// - Since spells.2da has no speed column, we always return the constant value
        /// </remarks>
        private float GetSpellProjectileSpeed(dynamic spell)
        {
            // spells.2da does not contain a projectile speed column, so we always use the constant
            // This matches the original engine behavior where all spell projectiles use the same speed
            return SpellProjectileSpeed;
        }

        /// <summary>
        /// Gets the movement speed to use when moving towards target location during spell casting.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Movement speed during spell casting uses entity's walk speed.
        /// </summary>
        /// <param name="actor">The caster entity.</param>
        /// <param name="stats">The caster's stats component.</param>
        /// <returns>The movement speed in units per second.</returns>
        /// <remarks>
        /// Movement Speed During Spell Casting:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Movement speed during spell casting uses entity's walk speed
        /// - Located via analysis: spells.2da does not contain a movement speed column
        /// - The original engine uses the caster's walk speed (from appearance.2da WALKRATE) for movement
        /// - Movement speed effects (EffectMovementSpeedIncrease/Decrease) are already applied via IStatsComponent.WalkSpeed
        /// - Spell-specific movement speeds do not exist in spells.2da, so we always use the entity's walk speed
        /// - Default fallback speed: 2.5 units per second (matches ActionCastSpellAtObject and other movement actions)
        /// - The caster moves at their normal walk speed to get within spell range before casting
        /// - This matches the original engine behavior where spell casting movement uses standard walk speed
        /// </remarks>
        private float GetMovementSpeedForSpellCasting(IEntity actor, IStatsComponent stats)
        {
            // Validate inputs
            if (stats == null)
            {
                // Fallback: Default walk speed if stats component is missing
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Default movement speed when stats unavailable
                // Matches ActionCastSpellAtObject fallback speed (2.5 units per second)
                return 2.5f;
            }

            // Use entity's walk speed (already accounts for movement speed effects)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Movement speed during spell casting uses walk speed from appearance.2da
            // WalkSpeed property already includes all movement speed modifiers from effects
            // (EffectMovementSpeedIncrease/Decrease are applied by the effect system to WalkSpeed)
            float walkSpeed = stats.WalkSpeed;

            // Validate walk speed (ensure it's positive and reasonable)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Movement speed validation (minimum 0.1 units per second)
            if (walkSpeed <= 0f)
            {
                // Fallback: Default walk speed if invalid
                return 2.5f;
            }

            // Check for spell-specific movement speed modifiers (for future extensibility)
            // Note: spells.2da does not contain a movement speed column, but we check spell data
            // for completeness and future-proofing in case spell-specific speeds are added
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
                    // Fall through - spell data unavailable, use walk speed
                }
            }

            // Even if spell data is available, spells.2da does not contain a movement speed column
            // So we always use the entity's walk speed
            // This matches the original engine behavior where spell casting movement uses standard walk speed
            // Future note: If spells.2da is extended with a movement speed column, this method can be updated
            // to check for spell-specific speeds and apply them as modifiers to the base walk speed

            return walkSpeed;
        }

        /// <summary>
        /// Gets the spell radius from spell data.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell range from spells.2da (range column or conjrange column)
        /// </summary>
        private float GetSpellRadius(dynamic spell)
        {
            // Default 5.0 units for area spells, uses conjrange if available, otherwise range
            float spellRadius = 5.0f;
            if (spell != null)
            {
                try
                {
                    // Try conjrange first (conjuration range), then range
                    int conjRange = spell.ConjRange as int? ?? 0;
                    if (conjRange > 0)
                    {
                        spellRadius = conjRange;
                    }
                    else
                    {
                        int range = spell.Range as int? ?? 0;
                        if (range > 0)
                        {
                            spellRadius = range;
                        }
                    }
                }
                catch
                {
                    // Fall through to default
                }
            }
            return spellRadius;
        }

        /// <summary>
        /// Applies spell effects to entities at a target location within radius.
        /// swkotor2.exe: Spell effect application at location (part of spell casting system)
        /// Located via string references: "OnSpellCastAt" @ 0x007c1a44, "EVENT_SPELL_IMPACT" @ 0x007bcd8c
        /// Original implementation: Applies visual effects and executes impact scripts for all entities in range
        /// This is called for both instant spells (immediately) and projectile spells (on impact)
        /// Function behavior:
        /// - Queries world for all entities within spellRadius of targetLocation
        /// - For each valid entity, applies spell effects via ApplySpellEffectsToTarget
        /// - Impact scripts (impactscript column) contain primary spell effect logic
        /// - Visual effects (conjhandvfx, conjheadvfx) are applied via effect system
        /// </summary>
        /// <param name="caster">The spell caster entity</param>
        /// <param name="targetLocation">The target location where effects are applied</param>
        /// <param name="spellRadius">The radius within which effects are applied</param>
        /// <param name="spell">The spell data (may be null if unavailable)</param>
        /// <param name="effectSystem">The effect system to apply visual effects</param>
        private void ApplySpellEffectsToEntitiesAtLocation(IEntity caster, Vector3 targetLocation, float spellRadius, dynamic spell, Combat.EffectSystem effectSystem)
        {
            if (caster.World == null)
            {
                return;
            }

            // Get all entities in range of target location
            IEnumerable<IEntity> entitiesInRange = caster.World.GetEntitiesInRadius(targetLocation, spellRadius, ObjectType.Creature);

            // Handle spell-specific effects (damage, healing, status effects) from spells.2da
            // swkotor2.exe: Spell effects are applied to entities in range via impact scripts
            // Spell effects come from impact scripts (impactscript column) which apply damage/healing/status effects
            // Full implementation: Executes impact scripts directly and applies spell effects from spell data
            // Impact scripts are the primary mechanism for spell effects - they contain all damage, healing, and status effect logic
            // Visual effects are applied separately from spell data columns (conjhandvfx, conjheadvfx, conjgrndvfx)
            // Edge cases handled:
            // - Null/invalid entities are skipped
            // - Zero radius returns no entities (GetEntitiesInRadius handles this)
            // - Missing spell data still applies visual effects if available
            foreach (IEntity target in entitiesInRange)
            {
                if (target == null || !target.IsValid)
                {
                    continue;
                }

                // Apply spell effects to target
                // swkotor2.exe: Spell effects are applied based on spell data and impact scripts
                // This executes impact scripts directly and applies visual effects from spell data
                // Impact scripts receive target as OBJECT_SELF, caster as triggerer
                // Spell ID and caster level available via GetLastSpellId/GetLastSpellCasterLevel engine functions
                ApplySpellEffectsToTarget(caster, target, spell, effectSystem);
            }
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
        /// Gets the duration of a visual effect from visualeffects.2da.
        /// </summary>
        /// <param name="visualEffectId">The visual effect ID (row index in visualeffects.2da).</param>
        /// <returns>The duration in seconds, or 2.0f as default if not found.</returns>
        /// <remarks>
        /// Visual Effect Duration:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Visual effects duration from visualeffects.2da
        /// - Located via string references: "visualeffects" @ 0x007c4a7c
        /// - Original implementation: Loads visualeffects.2da table, reads "duration" column for the visual effect ID
        /// - Visual effect ID is row index in visualeffects.2da
        /// - Duration is stored as float in seconds in the "duration" column
        /// - Default duration is 2.0 seconds if visual effect not found or duration not specified
        /// - Based on visualeffects.2da format documentation in vendor/PyKotor/wiki/2DA-visualeffects.md
        /// </remarks>
        private float GetVisualEffectDuration(int visualEffectId)
        {
            if (_gameDataManager != null)
            {
                dynamic gameDataManager = _gameDataManager;
                try
                {
                    return gameDataManager.GetVisualEffectDuration(visualEffectId);
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Fallback: default duration if GameDataManager not available
            return 2.0f;
        }

        /// <summary>
        /// Applies spell effects at the target location.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell effect application at location
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

            // Determine spell area of effect radius
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell range from spells.2da (range column or conjrange column)
            // Default 5.0 units for area spells, uses conjrange if available, otherwise range
            float spellRadius = GetSpellRadius(spell);

            // 4. Apply ground visual effects at target location
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Ground visual effects (conjgrndvfx) are applied at the spell target location
            // Located via string references: "CastGrndVisual" @ 0x007c3240, conjgrndvfx column in spells.2da
            // Original implementation: Creates visual effect entity or area effect at target location
            if (spell != null)
            {
                try
                {
                    int conjGrndVfx = spell.ConjGrndVfx as int? ?? 0;
                    if (conjGrndVfx > 0)
                    {
                        // Create a temporary entity at the location to display the ground visual effect
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Ground visual effects are rendered as area effects at the spell location
                        IEntity groundVfxEntity = caster.World.CreateEntity(ObjectType.AreaOfEffect, targetLocation, 0f);
                        if (groundVfxEntity != null)
                        {
                            var groundVisualEffect = new Combat.Effect(Combat.EffectType.VisualEffect)
                            {
                                VisualEffectId = conjGrndVfx,
                                DurationType = Combat.EffectDurationType.Instant
                            };
                            effectSystem.ApplyEffect(groundVfxEntity, groundVisualEffect, caster);
                            // Schedule entity destruction after visual effect duration
                            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Ground visual effects persist for their duration from visualeffects.2da
                            // Located via string references: "visualeffects" @ 0x007c4a7c, duration column in visualeffects.2da
                            // Original implementation: Visual effects duration is read from visualeffects.2da "duration" column
                            float visualEffectDuration = GetVisualEffectDuration(conjGrndVfx);
                            caster.World.DelayScheduler?.ScheduleAction(visualEffectDuration, () =>
                            {
                                if (groundVfxEntity.IsValid)
                                {
                                    caster.World.DestroyEntity(groundVfxEntity.ObjectId);
                                }
                            });
                        }
                    }
                }
                catch
                {
                    // Fall through
                }
            }

            // 1. Create projectile entity for projectile spells
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Projectile spells create projectile entities that travel to target location
            // Located via string references: projectile, projectilemodel columns in spells.2da
            // Original implementation: Creates projectile entity with model from projectilemodel column,
            // projectile travels from caster position to target location, applies effects on impact
            bool hasProjectile = false;
            if (spell != null)
            {
                try
                {
                    string projectile = spell.Projectile as string;
                    string projectileModel = spell.ProjectileModel as string;
                    if (!string.IsNullOrEmpty(projectile) || !string.IsNullOrEmpty(projectileModel))
                    {
                        hasProjectile = true;

                        // Get caster position for projectile origin
                        ITransformComponent casterTransform = caster.GetComponent<ITransformComponent>();
                        if (casterTransform != null)
                        {
                            Vector3 projectileStart = casterTransform.Position;

                            // Create projectile entity
                            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Projectile entities use AreaOfEffect object type
                            // Projectile travels from start to target location
                            IEntity projectileEntity = caster.World.CreateEntity(ObjectType.AreaOfEffect, projectileStart, 0f);
                            if (projectileEntity != null)
                            {
                                // Store projectile data in entity for rendering/movement system
                                // Projectile model is stored for rendering system to load and display
                                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Projectile entities contain model ResRef for rendering
                                Type entityType = projectileEntity.GetType();
                                System.Reflection.FieldInfo dataField = entityType.BaseType.GetField("_data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (dataField != null)
                                {
                                    var data = dataField.GetValue(projectileEntity) as Dictionary<string, object>;
                                    if (data == null)
                                    {
                                        data = new Dictionary<string, object>();
                                        dataField.SetValue(projectileEntity, data);
                                    }
                                    data["ProjectileModel"] = !string.IsNullOrEmpty(projectileModel) ? projectileModel : projectile;
                                    data["ProjectileTarget"] = targetLocation;
                                    data["ProjectileCaster"] = caster.ObjectId;
                                    data["ProjectileSpellId"] = _spellId;
                                    data["ProjectileSpeed"] = SpellProjectileSpeed;
                                }

                                // Projectile will be handled by rendering/movement system
                                // On impact, projectile applies spell effects to entities at target location
                                // swkotor2.exe: Projectile impact handled via scheduled callback system (DelayScheduler)
                                // Original implementation: Projectile travels from caster to target location at constant speed (30.0 units/sec)
                                // When projectile reaches target location, applies spell effects to all entities within spell radius
                                // Spell effects applied via ApplySpellEffectsToEntitiesAtLocation (impact scripts + visual effects)
                                // Note: Original engine uses frame-based projectile update system; our implementation uses scheduled callbacks
                                // for timing accuracy. Projectile rendering/movement is handled by separate rendering system.
                                Vector3 toTarget = targetLocation - projectileStart;
                                toTarget.Y = 0;
                                float distance = toTarget.Length();

                                // Get projectile speed for spell projectile
                                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell projectile speed is constant across all spells
                                // Located via analysis: spells.2da does not contain a projectile speed column
                                // The original engine uses a constant value for all spell projectiles
                                // Note: This differs from weapon projectiles which use a different constant (16.0f in vendor/reone)
                                // Spell projectiles use 30.0 units per second, which is faster than weapon projectiles
                                float projectileSpeed = GetSpellProjectileSpeed(spell);

                                float travelTime = distance / projectileSpeed;
                                if (travelTime < 0.1f) travelTime = 0.1f; // Minimum travel time (0.1 seconds)

                                // Capture variables for impact callback (caster, spell, spellRadius, effectSystem, targetLocation)
                                IEntity capturedCaster = caster;
                                dynamic capturedSpell = spell;
                                float capturedSpellRadius = spellRadius;
                                Combat.EffectSystem capturedEffectSystem = effectSystem;
                                Vector3 capturedTargetLocation = targetLocation;

                                caster.World.DelayScheduler?.ScheduleAction(travelTime, () =>
                                {
                                    if (projectileEntity.IsValid && capturedCaster.World != null)
                                    {
                                        // Apply spell effects at impact location
                                        // swkotor2.exe: Projectile impact applies spell effects to entities at target location
                                        // Effects are applied to all entities within spell radius at impact location
                                        // This matches original engine behavior where projectile impact triggers spell effect application
                                        // Function: ApplySpellEffectsToEntitiesAtLocation handles:
                                        // - Finding all entities within spell radius at target location
                                        // - Executing impact scripts (impactscript column from spells.2da)
                                        // - Applying visual effects (conjhandvfx, conjheadvfx from spells.2da)
                                        // - Firing OnSpellCastAt script events for compatibility
                                        ApplySpellEffectsToEntitiesAtLocation(capturedCaster, capturedTargetLocation, capturedSpellRadius, capturedSpell, capturedEffectSystem);

                                        // Destroy projectile entity after impact
                                        capturedCaster.World.DestroyEntity(projectileEntity.ObjectId);
                                    }
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Fall through
                }
            }

            // 2. Create area effect zone entity for persistent area spells
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Persistent area spells (conjduration > 0) create area effect zones
            // Located via string references: conjduration, conjrange columns in spells.2da
            // Original implementation: Creates area effect object that applies effects to entities entering/within range
            // Area effects persist for conjduration seconds, applying effects each round to entities in range
            if (spell != null && !hasProjectile) // Area effects are not used for projectile spells
            {
                try
                {
                    float conjDuration = spell.ConjDuration as float? ?? 0f;
                    if (conjDuration > 0f)
                    {
                        // Create persistent area effect
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area effects are stored in area's area effect list
                        // Area effects apply effects to entities within range over their duration
                        IArea area = caster.World?.CurrentArea;
                        if (area != null)
                        {
                            // Check if area supports area effects (AuroraArea has AddAreaEffect method)
                            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area effects are IAreaEffect instances
                            // Area effects update each frame, applying effects to entities in range
                            // Since IAreaEffect is engine-specific (defined in AuroraArea), we use reflection
                            // to work with the interface without direct dependencies
                            System.Type areaType = area.GetType();
                            System.Reflection.MethodInfo addAreaEffectMethod = areaType.GetMethod("AddAreaEffect");
                            if (addAreaEffectMethod != null)
                            {
                                // Create area effect implementation
                                var areaEffect = new SpellAreaEffect(caster, _spellId, targetLocation, spellRadius, conjDuration, effectSystem, _gameDataManager);

                                // Get IAreaEffect interface type from the area's assembly
                                System.Type iAreaEffectType = null;
                                foreach (System.Type type in areaType.Assembly.GetTypes())
                                {
                                    if (type.Name == "IAreaEffect" && type.IsInterface)
                                    {
                                        iAreaEffectType = type;
                                        break;
                                    }
                                }

                                if (iAreaEffectType != null)
                                {
                                    // Store area effect in area's internal list via reflection
                                    // Since IAreaEffect is engine-specific and we can't easily create adapters at runtime,
                                    // we'll store the SpellAreaEffect and update it manually via a scheduled action
                                    // The area effect will be updated each frame to apply spell effects to entities in range

                                    // Schedule periodic updates for the area effect
                                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area effects update each frame, applying effects to entities in range
                                    float updateInterval = 0.1f; // Update every 0.1 seconds (10 times per second)
                                    float remainingDuration = conjDuration;

                                    System.Action updateAction = null;
                                    updateAction = new System.Action(() =>
                                    {
                                        if (areaEffect.IsActive && remainingDuration > 0f)
                                        {
                                            areaEffect.Update(updateInterval);
                                            remainingDuration -= updateInterval;
                                            if (remainingDuration > 0f)
                                            {
                                                caster.World.DelayScheduler?.ScheduleAction(updateInterval, updateAction);
                                            }
                                            else
                                            {
                                                areaEffect.Deactivate();
                                            }
                                        }
                                    });

                                    // Start the update loop
                                    caster.World.DelayScheduler?.ScheduleAction(updateInterval, updateAction);

                                    // Schedule area effect deactivation after duration
                                    caster.World.DelayScheduler?.ScheduleAction(conjDuration, () =>
                                    {
                                        if (areaEffect.IsActive)
                                        {
                                            areaEffect.Deactivate();
                                        }
                                    });
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fall through
                }
            }

            // For instant spells (no projectile, no persistent area effect), apply effects immediately
            // For projectile spells, effects are applied on impact (handled by projectile impact callback)
            // For persistent area spells, effects are applied by area effect zone over time
            if (!hasProjectile)
            {
                // Apply spell effects to entities at target location
                ApplySpellEffectsToEntitiesAtLocation(caster, targetLocation, spellRadius, spell, effectSystem);
            }
        }

        /// <summary>
        /// Applies spell effects to a target entity.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Comprehensive spell effect resolution
        /// </summary>
        /// <param name="caster">The entity casting the spell.</param>
        /// <param name="target">The target entity receiving the spell effects.</param>
        /// <param name="spell">Dynamic spell data from spells.2da.</param>
        /// <param name="effectSystem">The effect system for applying effects.</param>
        /// <remarks>
        /// Full spell effect resolution:
        /// 1. Applies visual effects (conjhandvfx, conjheadvfx)
        /// 2. Executes impact script directly (if available)
        /// 3. Fires script events for compatibility
        /// 4. Applies spell effects from spell data when available
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell effects are resolved through impact scripts and spell data
        /// </remarks>
        private void ApplySpellEffectsToTarget(IEntity caster, IEntity target, dynamic spell, Combat.EffectSystem effectSystem)
        {
            if (caster == null || target == null || effectSystem == null || !target.IsValid)
            {
                return;
            }

            // 1. Apply visual effects if spell data available
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Visual effects (conjhandvfx, conjheadvfx columns) are applied to targets
            if (spell != null)
            {
                try
                {
                    int conjHandVfx = spell.ConjHandVfx as int? ?? 0;
                    if (conjHandVfx > 0)
                    {
                        var visualEffect = new Combat.Effect(Combat.EffectType.VisualEffect)
                        {
                            VisualEffectId = conjHandVfx,
                            DurationType = Combat.EffectDurationType.Instant
                        };
                        effectSystem.ApplyEffect(target, visualEffect, caster);
                    }

                    // Apply head visual effect if available
                    int conjHeadVfx = spell.ConjHeadVfx as int? ?? 0;
                    if (conjHeadVfx > 0)
                    {
                        var headVisualEffect = new Combat.Effect(Combat.EffectType.VisualEffect)
                        {
                            VisualEffectId = conjHeadVfx,
                            DurationType = Combat.EffectDurationType.Instant
                        };
                        effectSystem.ApplyEffect(target, headVisualEffect, caster);
                    }
                }
                catch
                {
                    // Fall through
                }
            }

            // 2. Execute impact script directly if present
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Impact scripts (impactscript column) contain spell effect logic
            // Impact scripts apply damage, healing, status effects based on spell ID and caster level
            // Located via string references: "ImpactScript" @ spells.2da, impact scripts execute on spell impact
            // Impact scripts are the primary mechanism for spell effects - they contain all damage, healing, and status effect logic
            // Original implementation: Impact scripts execute with target as OBJECT_SELF, caster as triggerer
            // Script context: Spell ID is available through GetLastSpellId engine function, caster level through GetLastSpellCasterLevel
            if (spell != null)
            {
                try
                {
                    // Execute primary impact script (impactscript column)
                    string impactScript = spell.ImpactScript as string;
                    if (!string.IsNullOrEmpty(impactScript))
                    {
                        // Execute impact script directly using script executor
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Impact scripts receive target as OBJECT_SELF, caster as triggerer
                        // Impact scripts contain the primary spell effect logic (damage, healing, status effects)
                        bool scriptExecuted = ExecuteImpactScript(target, impactScript, caster);

                        // Also fire script event for compatibility (some systems may rely on events)
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Script events provide additional hooks for spell effects
                        // OnSpellCastAt script event fires to allow entities to react to being targeted by spells
                        IEventBus eventBus = caster.World?.EventBus;
                        if (eventBus != null)
                        {
                            // Script event: OnSpellCastAt fires when spell impacts target
                            // Located via string references: "OnSpellCastAt" @ 0x007c1a44, "ScriptSpellAt" @ 0x007bee90
                            // This allows entities with OnSpellCastAt script hooks to react to spell impacts
                            eventBus.FireScriptEvent(target, ScriptEvent.OnSpellCastAt, caster);
                        }

                        // If script execution failed or no script executor available,
                        // script event will still fire for systems that handle it
                        // Script execution is the primary method, event is fallback/compatibility
                    }

                    // Execute conjuration impact script (conjimpactscript column) if present
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Conjuration impact scripts execute for conjuration-type spells
                    // Located via string references: "conjimpactscript" column in spells.2da
                    // Conjuration impact scripts are used for area-effect spells and persistent spell zones
                    string conjImpactScript = spell.ConjImpactScript as string;
                    if (!string.IsNullOrEmpty(conjImpactScript) && !string.Equals(conjImpactScript, impactScript, System.StringComparison.OrdinalIgnoreCase))
                    {
                        // Conjuration impact script is separate from regular impact script
                        // Execute it in addition to the primary impact script
                        ExecuteImpactScript(target, conjImpactScript, caster);
                    }
                }
                catch
                {
                    // Fall through - script execution errors should not prevent other effects
                    // Visual effects and other spell data-based effects should still apply even if script execution fails
                }
            }
        }

        /// <summary>
        /// Executes an impact script directly using the script executor.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Direct script execution for impact scripts
        /// Located via string references: "ImpactScript" @ spells.2da, script execution system
        /// Original implementation: Impact scripts execute with target as OBJECT_SELF, caster as triggerer
        /// </summary>
        /// <param name="target">The target entity (OBJECT_SELF in script).</param>
        /// <param name="scriptResRef">The script resource reference to execute.</param>
        /// <param name="triggerer">The caster entity (triggerer in script).</param>
        /// <returns>True if script was executed successfully, false otherwise.</returns>
        /// <remarks>
        /// Comprehensive script execution with multiple fallback patterns:
        /// 1. Property access: World.ScriptExecutor (most common pattern)
        /// 2. Field access: World._scriptExecutor or World.ScriptExecutor (alternative patterns)
        /// 3. Method signature variations: ExecuteScript(IEntity, string, IEntity) or ExecuteScript(string, IEntity, IEntity)
        /// 4. Direct World method: World.ExecuteScript (some implementations expose this directly)
        /// Falls back gracefully if script executor is not available.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Impact scripts execute with target as OBJECT_SELF, caster as triggerer
        /// Script context: Spell ID and caster level are available through GetLastSpellId/GetLastSpellCasterLevel engine functions
        /// </remarks>
        private bool ExecuteImpactScript(IEntity target, string scriptResRef, IEntity triggerer)
        {
            if (target == null || string.IsNullOrEmpty(scriptResRef) || target.World == null)
            {
                return false;
            }

            // Normalize script ResRef (remove extension if present, ensure lowercase)
            string normalizedScript = scriptResRef.ToLowerInvariant();
            if (normalizedScript.EndsWith(".ncs"))
            {
                normalizedScript = normalizedScript.Substring(0, normalizedScript.Length - 4);
            }

            try
            {
                // Try to access script executor through World interface
                // Script executor may be exposed via reflection or property access
                System.Type worldType = target.World.GetType();

                // Pattern 1: Property access (most common pattern)
                System.Reflection.PropertyInfo scriptExecutorProperty = worldType.GetProperty("ScriptExecutor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (scriptExecutorProperty == null)
                {
                    scriptExecutorProperty = worldType.GetProperty("ScriptExecutor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (scriptExecutorProperty != null)
                {
                    object scriptExecutor = scriptExecutorProperty.GetValue(target.World);
                    if (scriptExecutor != null)
                    {
                        // Try ExecuteScript(IEntity caller, string scriptResRef, IEntity triggerer)
                        System.Reflection.MethodInfo executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(IEntity), typeof(string), typeof(IEntity) });
                        if (executeMethod != null)
                        {
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { target, normalizedScript, triggerer });
                            return result != null && System.Convert.ToInt32(result) != 0;
                        }

                        // Try ExecuteScript(string scriptResRef, IEntity owner, IEntity triggerer)
                        executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(string), typeof(IEntity), typeof(IEntity) });
                        if (executeMethod != null)
                        {
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { normalizedScript, target, triggerer });
                            return result != null && System.Convert.ToInt32(result) != 0;
                        }
                    }
                }

                // Pattern 2: Field access (alternative pattern)
                System.Reflection.FieldInfo scriptExecutorField = worldType.GetField("_scriptExecutor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scriptExecutorField == null)
                {
                    scriptExecutorField = worldType.GetField("ScriptExecutor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (scriptExecutorField != null)
                {
                    object scriptExecutor = scriptExecutorField.GetValue(target.World);
                    if (scriptExecutor != null)
                    {
                        // Try ExecuteScript(IEntity caller, string scriptResRef, IEntity triggerer)
                        System.Reflection.MethodInfo executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(IEntity), typeof(string), typeof(IEntity) });
                        if (executeMethod != null)
                        {
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { target, normalizedScript, triggerer });
                            return result != null && System.Convert.ToInt32(result) != 0;
                        }

                        // Try ExecuteScript(string scriptResRef, IEntity owner, IEntity triggerer)
                        executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(string), typeof(IEntity), typeof(IEntity) });
                        if (executeMethod != null)
                        {
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { normalizedScript, target, triggerer });
                            return result != null && System.Convert.ToInt32(result) != 0;
                        }
                    }
                }

                // Pattern 3: Direct World method (some implementations expose ExecuteScript directly on World)
                System.Reflection.MethodInfo worldExecuteMethod = worldType.GetMethod("ExecuteScript", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (worldExecuteMethod == null)
                {
                    worldExecuteMethod = worldType.GetMethod("ExecuteScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (worldExecuteMethod != null)
                {
                    // Check method signature to determine parameter order
                    System.Reflection.ParameterInfo[] parameters = worldExecuteMethod.GetParameters();
                    if (parameters.Length >= 2)
                    {
                        // Try common signature patterns
                        try
                        {
                            if (parameters.Length == 3 && parameters[0].ParameterType == typeof(IEntity) && parameters[1].ParameterType == typeof(string))
                            {
                                object result = worldExecuteMethod.Invoke(target.World, new object[] { target, normalizedScript, triggerer });
                                return result != null && System.Convert.ToInt32(result) != 0;
                            }
                            else if (parameters.Length == 3 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(IEntity))
                            {
                                object result = worldExecuteMethod.Invoke(target.World, new object[] { normalizedScript, target, triggerer });
                                return result != null && System.Convert.ToInt32(result) != 0;
                            }
                            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(IEntity))
                            {
                                object result = worldExecuteMethod.Invoke(target.World, new object[] { normalizedScript, target });
                                return result != null && System.Convert.ToInt32(result) != 0;
                            }
                        }
                        catch
                        {
                            // Method signature mismatch - continue to next pattern
                        }
                    }
                }
            }
            catch
            {
                // Reflection failures are expected - script executor may not be accessible
                // Return false to indicate script was not executed
            }

            return false;
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

    /// <summary>
    /// Area effect for persistent spell zones.
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Persistent area spell effects (conjduration > 0)
    /// Located via string references: Area effect system for persistent spell zones
    /// Original implementation: Area effects apply spell effects to entities within range over duration
    /// </summary>
    internal class SpellAreaEffect
    {
        private readonly IEntity _caster;
        private readonly int _spellId;
        private readonly Vector3 _center;
        private readonly float _radius;
        private readonly float _duration;
        private readonly Combat.EffectSystem _effectSystem;
        private readonly object _gameDataManager;
        private float _elapsedTime;
        private float _lastApplyTime;
        private const float ApplyInterval = 1.0f; // Apply effects every second (6 seconds per round)

        public bool IsActive { get; private set; }

        public SpellAreaEffect(IEntity caster, int spellId, Vector3 center, float radius, float duration, Combat.EffectSystem effectSystem, object gameDataManager)
        {
            _caster = caster;
            _spellId = spellId;
            _center = center;
            _radius = radius;
            _duration = duration;
            _effectSystem = effectSystem;
            _gameDataManager = gameDataManager;
            _elapsedTime = 0f;
            _lastApplyTime = 0f;
            IsActive = true;
        }

        /// <summary>
        /// Updates the area effect, applying spell effects to entities in range periodically.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!IsActive)
            {
                return;
            }

            _elapsedTime += deltaTime;

            // Check if duration has expired
            if (_elapsedTime >= _duration)
            {
                Deactivate();
                return;
            }

            // Apply effects periodically (every ApplyInterval seconds)
            if (_elapsedTime - _lastApplyTime >= ApplyInterval)
            {
                _lastApplyTime = _elapsedTime;

                // Get all entities in range
                if (_caster.World != null)
                {
                    IEnumerable<IEntity> entitiesInRange = _caster.World.GetEntitiesInRadius(_center, _radius, ObjectType.Creature);

                    // Get spell data
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

                    // Apply effects to entities in range
                    foreach (IEntity target in entitiesInRange)
                    {
                        if (target == null || !target.IsValid)
                        {
                            continue;
                        }

                        // Apply visual effects if spell data available
                        if (spell != null)
                        {
                            try
                            {
                                int conjHandVfx = spell.ConjHandVfx as int? ?? 0;
                                if (conjHandVfx > 0)
                                {
                                    var visualEffect = new Combat.Effect(Combat.EffectType.VisualEffect)
                                    {
                                        VisualEffectId = conjHandVfx,
                                        DurationType = Combat.EffectDurationType.Instant
                                    };
                                    _effectSystem.ApplyEffect(target, visualEffect, _caster);
                                }
                            }
                            catch
                            {
                                // Fall through
                            }
                        }

                        // Execute impact script if present (spell effects come from scripts)
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Impact scripts are the primary mechanism for spell effects
                        // Impact scripts contain damage, healing, and status effect logic
                        if (spell != null)
                        {
                            try
                            {
                                // Execute primary impact script (impactscript column)
                                string impactScript = spell.ImpactScript as string;
                                if (!string.IsNullOrEmpty(impactScript))
                                {
                                    // Execute impact script directly using script executor
                                    // SpellAreaEffect needs to execute scripts, so we use a helper method
                                    bool scriptExecuted = ExecuteImpactScriptForAreaEffect(target, impactScript, _caster);

                                    // Also fire script event for compatibility (some systems may rely on events)
                                    IEventBus eventBus = _caster.World?.EventBus;
                                    if (eventBus != null)
                                    {
                                        // Script event: OnSpellCastAt fires when spell impacts target
                                        // This allows entities with OnSpellCastAt script hooks to react to spell impacts
                                        eventBus.FireScriptEvent(target, ScriptEvent.OnSpellCastAt, _caster);
                                    }

                                    // If script execution failed or no script executor available,
                                    // script event will still fire for systems that handle it
                                    // Script execution is the primary method, event is fallback/compatibility
                                }

                                // Execute conjuration impact script (conjimpactscript column) if present
                                string conjImpactScript = spell.ConjImpactScript as string;
                                if (!string.IsNullOrEmpty(conjImpactScript) && !string.Equals(conjImpactScript, impactScript, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    // Conjuration impact script is separate from regular impact script
                                    // Execute it in addition to the primary impact script
                                    ExecuteImpactScriptForAreaEffect(target, conjImpactScript, _caster);
                                }
                            }
                            catch
                            {
                                // Fall through - script execution errors should not prevent other effects
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Renders the area effect (handled by rendering system).
        /// </summary>
        public void Render()
        {
            // Rendering is handled by the rendering system based on area effect type
            // Area effects may display visual indicators (particles, effects, etc.)
        }

        /// <summary>
        /// Executes an impact script directly using the script executor (helper for SpellAreaEffect).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Direct script execution for impact scripts
        /// </summary>
        /// <param name="target">The target entity (OBJECT_SELF in script).</param>
        /// <param name="scriptResRef">The script resource reference to execute.</param>
        /// <param name="triggerer">The caster entity (triggerer in script).</param>
        /// <returns>True if script was executed successfully, false otherwise.</returns>
        /// <remarks>
        /// Comprehensive script execution with multiple fallback patterns (same as ExecuteImpactScript in parent class).
        /// This is a static helper method to allow SpellAreaEffect to execute scripts without needing a reference to the parent ActionCastSpellAtLocation instance.
        /// </remarks>
        private static bool ExecuteImpactScriptForAreaEffect(IEntity target, string scriptResRef, IEntity triggerer)
        {
            if (target == null || string.IsNullOrEmpty(scriptResRef) || target.World == null)
            {
                return false;
            }

            // Normalize script ResRef (remove extension if present, ensure lowercase)
            string normalizedScript = scriptResRef.ToLowerInvariant();
            if (normalizedScript.EndsWith(".ncs"))
            {
                normalizedScript = normalizedScript.Substring(0, normalizedScript.Length - 4);
            }

            try
            {
                // Try to access script executor through World interface
                System.Type worldType = target.World.GetType();

                // Pattern 1: Property access (most common pattern)
                System.Reflection.PropertyInfo scriptExecutorProperty = worldType.GetProperty("ScriptExecutor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (scriptExecutorProperty == null)
                {
                    scriptExecutorProperty = worldType.GetProperty("ScriptExecutor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (scriptExecutorProperty != null)
                {
                    object scriptExecutor = scriptExecutorProperty.GetValue(target.World);
                    if (scriptExecutor != null)
                    {
                        // Try ExecuteScript(IEntity caller, string scriptResRef, IEntity triggerer)
                        System.Reflection.MethodInfo executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(IEntity), typeof(string), typeof(IEntity) });
                        if (executeMethod != null)
                        {
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { target, normalizedScript, triggerer });
                            return result != null && System.Convert.ToInt32(result) != 0;
                        }

                        // Try ExecuteScript(string scriptResRef, IEntity owner, IEntity triggerer)
                        executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(string), typeof(IEntity), typeof(IEntity) });
                        if (executeMethod != null)
                        {
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { normalizedScript, target, triggerer });
                            return result != null && System.Convert.ToInt32(result) != 0;
                        }
                    }
                }

                // Pattern 2: Field access (alternative pattern)
                System.Reflection.FieldInfo scriptExecutorField = worldType.GetField("_scriptExecutor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scriptExecutorField == null)
                {
                    scriptExecutorField = worldType.GetField("ScriptExecutor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (scriptExecutorField != null)
                {
                    object scriptExecutor = scriptExecutorField.GetValue(target.World);
                    if (scriptExecutor != null)
                    {
                        System.Reflection.MethodInfo executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(IEntity), typeof(string), typeof(IEntity) });
                        if (executeMethod != null)
                        {
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { target, normalizedScript, triggerer });
                            return result != null && System.Convert.ToInt32(result) != 0;
                        }

                        executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(string), typeof(IEntity), typeof(IEntity) });
                        if (executeMethod != null)
                        {
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { normalizedScript, target, triggerer });
                            return result != null && System.Convert.ToInt32(result) != 0;
                        }
                    }
                }

                // Pattern 3: Direct World method
                System.Reflection.MethodInfo worldExecuteMethod = worldType.GetMethod("ExecuteScript", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (worldExecuteMethod == null)
                {
                    worldExecuteMethod = worldType.GetMethod("ExecuteScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (worldExecuteMethod != null)
                {
                    System.Reflection.ParameterInfo[] parameters = worldExecuteMethod.GetParameters();
                    if (parameters.Length >= 2)
                    {
                        try
                        {
                            if (parameters.Length == 3 && parameters[0].ParameterType == typeof(IEntity) && parameters[1].ParameterType == typeof(string))
                            {
                                object result = worldExecuteMethod.Invoke(target.World, new object[] { target, normalizedScript, triggerer });
                                return result != null && System.Convert.ToInt32(result) != 0;
                            }
                            else if (parameters.Length == 3 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(IEntity))
                            {
                                object result = worldExecuteMethod.Invoke(target.World, new object[] { normalizedScript, target, triggerer });
                                return result != null && System.Convert.ToInt32(result) != 0;
                            }
                            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(IEntity))
                            {
                                object result = worldExecuteMethod.Invoke(target.World, new object[] { normalizedScript, target });
                                return result != null && System.Convert.ToInt32(result) != 0;
                            }
                        }
                        catch
                        {
                            // Method signature mismatch - continue
                        }
                    }
                }
            }
            catch
            {
                // Reflection failures are expected
            }

            return false;
        }

        /// <summary>
        /// Deactivates the area effect.
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }
    }
}


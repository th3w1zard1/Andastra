using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to cast a spell/Force power at a location.
    /// </summary>
    /// <remarks>
    /// Cast Spell At Location Action:
    /// - Based on swkotor2.exe spell casting system
    /// - Located via string references: "ActionCastSpellAtLocation" @ NWScript function
    /// - Original implementation: Caster moves to range, casts spell at target location
    /// - Spell effects: Projectile spells create projectiles, area spells create zones
    /// - Force point cost: Deducted from caster's Force points
    /// - Spell knowledge: Caster must know the spell (checked via spell list)
    /// - Range: Spell range from spells.2da, caster must be within range
    /// - Based on NWScript ActionCastSpellAtLocation semantics
    /// </remarks>
    public class ActionCastSpellAtLocation : ActionBase
    {
        private readonly int _spellId;
        private readonly Vector3 _targetLocation;
        private bool _approached;
        private bool _spellCast;
        private const float CastRange = 20.0f; // Default spell range

        public ActionCastSpellAtLocation(int spellId, Vector3 targetLocation)
            : base(ActionType.CastSpellAtLocation)
        {
            _spellId = spellId;
            _targetLocation = targetLocation;
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

            // Get spell data (would need GameDataManager/TwoDA access)
            // TODO: Check spell knowledge and get Force point cost from spells.2da
            // This requires:
            // 1. Access to spells.2da table to get spell data (ForceCost column)
            // 2. Spell knowledge checking (IStatsComponent.HasSpell or similar method)
            // 3. Verify caster has sufficient Force points (CurrentFP >= ForceCost)
            // For now, assume spell is valid if we have Force points

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

                transform.Position += direction * moveDistance;
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

                // Apply spell effects at target location
                // TODO: Implement full spell casting with projectiles and effects
                // This requires:
                // 1. Spell effect system (projectiles, area effects, instant effects)
                // 2. Projectile creation and movement (for projectile spells)
                // 3. Area effect zone creation (for area spells)
                // 4. Effect application to entities in range
                // 5. Force point deduction (CurrentFP -= ForceCost from spells.2da)
                // For now, spell cast event is fired for other systems to handle

                // Fire spell cast event
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


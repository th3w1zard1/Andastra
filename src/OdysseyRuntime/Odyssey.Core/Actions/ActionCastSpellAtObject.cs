using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

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
    /// </remarks>
    public class ActionCastSpellAtObject : ActionBase
    {
        private readonly int _spellId;
        private readonly uint _targetObjectId;
        private bool _approached;
        private const float CastRange = 10.0f; // Spell casting range

        public ActionCastSpellAtObject(int spellId, uint targetObjectId)
            : base(ActionType.CastSpellAtObject)
        {
            _spellId = spellId;
            _targetObjectId = targetObjectId;
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

            // TODO: Implement actual spell casting logic
            // For now, just complete the action
            // In the future, this should:
            // 1. Check if caster has enough Force points
            // 2. Check if spell is known
            // 3. Play casting animation
            // 4. Apply spell effects to target
            // 5. Consume Force points

            return ActionStatus.Complete;
        }
    }
}


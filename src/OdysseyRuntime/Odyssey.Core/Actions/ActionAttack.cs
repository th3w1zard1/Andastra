using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to attack a target entity.
    /// </summary>
    public class ActionAttack : ActionBase
    {
        private readonly uint _targetObjectId;
        private readonly bool _passive;
        private float _attackTimer;
        private const float AttackRange = 2.0f;
        private const float AttackInterval = 2.0f; // Time between attacks

        public ActionAttack(uint targetObjectId, bool passive = false)
            : base(ActionType.AttackObject)
        {
            _targetObjectId = targetObjectId;
            _passive = passive;
            _attackTimer = 0;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            var transform = actor.GetComponent<ITransformComponent>();
            var stats = actor.GetComponent<IStatsComponent>();

            if (transform == null || stats == null)
            {
                return ActionStatus.Failed;
            }

            // Get target entity
            var target = actor.World.GetEntity(_targetObjectId);
            if (target == null || !target.IsValid)
            {
                return ActionStatus.Complete; // Target gone
            }

            var targetTransform = target.GetComponent<ITransformComponent>();
            var targetStats = target.GetComponent<IStatsComponent>();

            if (targetTransform == null)
            {
                return ActionStatus.Failed;
            }

            // Check if target is dead
            if (targetStats != null && targetStats.CurrentHP <= 0)
            {
                return ActionStatus.Complete;
            }

            Vector3 toTarget = targetTransform.Position - transform.Position;
            toTarget.Y = 0;
            float distance = toTarget.Length();

            // Face target
            if (distance > 0.1f)
            {
                Vector3 direction = Vector3.Normalize(toTarget);
                transform.Facing = (float)Math.Atan2(direction.X, direction.Z);
            }

            // If out of range, move towards target
            if (distance > AttackRange)
            {
                float speed = stats.RunSpeed;
                Vector3 direction = Vector3.Normalize(toTarget);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - AttackRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                transform.Position += direction * moveDistance;
                return ActionStatus.InProgress;
            }

            // In range - attack
            _attackTimer += deltaTime;
            if (_attackTimer >= AttackInterval)
            {
                _attackTimer = 0;
                PerformAttack(actor, target, stats, targetStats);
            }

            return ActionStatus.InProgress;
        }

        private void PerformAttack(IEntity attacker, IEntity target, IStatsComponent attackerStats, IStatsComponent targetStats)
        {
            if (targetStats == null)
            {
                return;
            }

            // Simple attack calculation (to be expanded with proper combat system)
            // Attack roll: d20 + attack bonus vs target AC
            Random rand = new Random();
            int attackRoll = rand.Next(1, 21);
            int attackBonus = attackerStats.BaseAttackBonus;
            int targetAC = targetStats.ArmorClass;

            if (attackRoll == 20 || attackRoll + attackBonus >= targetAC)
            {
                // Hit - deal damage
                int damage = 1 + rand.Next(0, 8); // 1d8 base damage
                targetStats.CurrentHP -= damage;

                // Fire damage event
                var eventBus = attacker.World.EventBus;
                if (eventBus != null)
                {
                    eventBus.Publish(new DamageEvent
                    {
                        Attacker = attacker,
                        Target = target,
                        Damage = damage,
                        DamageType = DamageType.Physical
                    });
                }
            }
        }
    }

    /// <summary>
    /// Event fired when damage is dealt.
    /// </summary>
    public class DamageEvent : IEvent
    {
        public IEntity Attacker { get; set; }
        public IEntity Target { get; set; }
        public int Damage { get; set; }
        public DamageType DamageType { get; set; }
    }

    /// <summary>
    /// Types of damage.
    /// </summary>
    public enum DamageType
    {
        Physical,
        Energy,
        Fire,
        Cold,
        Electric,
        Sonic,
        Acid,
        Ion,
        DarkSide,
        LightSide
    }
}

